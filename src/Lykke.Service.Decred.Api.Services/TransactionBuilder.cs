using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Decred.BlockExplorer;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Common.Entity;
using NDecred.Common;
using Paymetheus.Decred;
using Paymetheus.Decred.Wallet;

namespace Lykke.Service.Decred.Api.Services
{
    public interface ITransactionBuilder
    {
        Task<BuildTransactionResponse> BuildSingleTransactionAsync(
            BuildSingleTransactionRequest request,
            decimal feeFactor);
    }
    
    public class TransactionBuilder : ITransactionBuilder
    {
        private readonly ITransactionFeeService _feeService;
        private readonly ITransactionRepository _txRepo;
        private readonly INosqlRepo<BroadcastedOutpoint> _broadcastedOutpointRepo;

        public TransactionBuilder(
            ITransactionFeeService feeService, 
            ITransactionRepository txRepo,
            INosqlRepo<BroadcastedOutpoint> broadcastedOutpointRepo)
        {
            _feeService = feeService;
            _txRepo = txRepo;
            _broadcastedOutpointRepo = broadcastedOutpointRepo;
        }

        private async Task<bool> IsBroadcastedUtxo(Transaction.OutPoint outpoint)
        {
            return await _broadcastedOutpointRepo.GetAsync(outpoint.ToString()) != null;
        }
        
        /// <summary>
        /// Retrieves all utxos for a single address found in dcrdata's db + mempool.
        /// Performs basic checking to prevent double-spending of mempool transactions.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private async Task<IEnumerable<Transaction.Input>> GetUtxosForAddress(string address)
        {
            const uint sequence = uint.MaxValue;
            
            // Grab transactions from dcrdata + the mempool.
            var confirmedResults = await _txRepo.GetConfirmedUtxos(address);
            var mempoolResults = await _txRepo.GetMempoolUtxos(address);

            // Remove duplicate outpoints.
            var allUtxos = confirmedResults.Concat(mempoolResults)
                .OrderBy(r => r.BlockHeight)
                .GroupBy(r => new { r.Hash, r.OutputIndex })
                .Select(g => g.First())
                .ToArray();
            
            // Get all unspent transaction outputs to address
            // and map as inputs to new transaction
            return 
                from output in allUtxos
                let txHash = new Blake256Hash(HexUtil.ToByteArray(output.Hash).Reverse().ToArray())
                let outpoint = new Transaction.OutPoint(txHash, output.OutputIndex, output.Tree)
                group new {outpoint, output}
                by outpoint.ToString() into outpointGroup
                let element = outpointGroup.First()
                let outpoint = element.outpoint
                let output = element.output
                select new Transaction.Input(
                    outpoint,
                    sequence,
                    output.OutputValue,
                    output.BlockHeight,
                    output.BlockIndex,
                    output.PkScript
                );
        }

        /// <summary>
        /// Builds a transaction that sends value from one address to another.
        /// Change is spent to the source address, if necessary.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="feeFactor"></param>
        /// <returns></returns>
        /// <exception cref="BusinessException"></exception>
        public async Task<BuildTransactionResponse> BuildSingleTransactionAsync(BuildSingleTransactionRequest request, decimal feeFactor)
        {
            if(string.IsNullOrWhiteSpace(request.FromAddress))
                throw new BusinessException(ErrorReason.BadRequest, "FromAddress missing");
            if(string.IsNullOrWhiteSpace(request.ToAddress))
                throw new BusinessException(ErrorReason.BadRequest, "ToAddress missing");
            if(!long.TryParse(request.Amount, out var amount) || amount <= 0)
                throw new BusinessException(ErrorReason.BadRequest, $"Invalid amount {amount}");

            var feePerKb = await _feeService.GetFeePerKb();
            if (TransactionRules.IsDustAmount(amount, Transaction.PayToPubKeyHashPkScriptSize, new Amount(feePerKb)))
                throw new BusinessException(ErrorReason.AmountTooSmall, "Amount is dust");

            const int outputVersion = 0;
            const int lockTime = 0;
            const int expiry = 0;

            // Number of outputs newly build tx will contain,
            // not including the change address
            const int numOutputs = 1;

            // Lykke api doesn't have option to specify a change address.
            var changeAddress = Address.Decode(request.FromAddress);
            var toAddress = Address.Decode(request.ToAddress);
            var allInputs = (await GetUtxosForAddress(request.FromAddress)).ToList();
            
            long estFee = 0;
            long totalSpent = 0;
            var consumedInputs = new List<Transaction.Input>();

            bool HasEnoughInputs(out long fee)
            {
                var calculateWithChange = false;
                while (true)
                {
                    var changeCount = calculateWithChange ? 1 : 0;
                    fee = _feeService.CalculateFee(feePerKb, consumedInputs.Count, numOutputs + changeCount, feeFactor);
                    var estAmount = amount + (request.IncludeFee ? 0 : fee);
                    
                    if (totalSpent < estAmount) return false;
                    if (totalSpent == estAmount) return true;
                    if (totalSpent > estAmount && calculateWithChange) return true;
                    
                    // Loop one more time but make sure change is accounted for this time.
                    if (totalSpent > estAmount) calculateWithChange = true;
                }
            }

            // Accumulate inputs until we have enough to cover the cost
            // of the amount + fee
            foreach (var input in allInputs)
            {
                // Don't consume an outpoint if it's spent.
                if (await IsBroadcastedUtxo(input.PreviousOutpoint))
                    continue;

                consumedInputs.Add(input);
                totalSpent += input.InputAmount;

                if (HasEnoughInputs(out estFee))
                    break;
            }
                        
            // If all inputs do not have enough value to fund the transaction.
            if(totalSpent < amount + (request.IncludeFee ? 0 : estFee))
                throw new BusinessException(ErrorReason.NotEnoughBalance, "Address balance too low");
            
            // The fee either comes from the change or the sent amount
            var send = amount - (request.IncludeFee ? estFee : 0 );
            var change = (totalSpent - amount) - (request.IncludeFee ? 0 : estFee);

            // If all inputs do not have enough value to fund the transaction, throw error.
            if(request.IncludeFee && estFee > amount)
                throw new BusinessException(ErrorReason.AmountTooSmall, "Amount not enough to include fee");

            // If all inputs do not have enough value to fund the transaction, throw error.
            if(totalSpent < amount + (request.IncludeFee ? 0 : estFee))
                throw new BusinessException(ErrorReason.NotEnoughBalance, "Address balance too low");

            // Build outputs to address + change address.
            // If any of the outputs is zero value, exclude it.  For example, if there is no change.
            var outputs = new[] {
                new Transaction.Output(send, outputVersion, toAddress.BuildScript().Script),
                new Transaction.Output(change, outputVersion, changeAddress.BuildScript().Script)
            }.Where(o => o.Amount != 0).ToArray();

            var newTx = new Transaction(
                Transaction.SupportedVersion,
                consumedInputs.ToArray(), 
                outputs, 
                lockTime, 
                expiry
            );
            
            return new BuildTransactionResponse
            {
                TransactionContext = HexUtil.FromByteArray(newTx.Serialize())
            };
        }

    }
}
