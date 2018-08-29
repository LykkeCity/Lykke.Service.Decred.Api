using System;
using System.Linq;
using System.Threading.Tasks;
using DcrdClient;
using Decred.BlockExplorer;
using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Common.Entity;
using NDecred.Common;
using Paymetheus.Decred;

namespace Lykke.Service.Decred.Api.Services
{
    public interface ITransactionBroadcastService
    {
        Task Broadcast(Guid operationId, string hexTransaction);
        Task UnsubscribeBroadcastedTx(Guid operationId);
        Task<BroadcastedSingleTransactionResponse> GetBroadcastedTxSingle(Guid operationId);
    }
    
    public class TransactionBroadcastService : ITransactionBroadcastService
    {
        private readonly IDcrdClient _dcrdClient;
        private readonly IBlockRepository _blockRepository;
        private readonly ITransactionRepository _txRepo;

        private readonly INosqlRepo<BroadcastedTransaction> _broadcastTxRepo;
        private readonly INosqlRepo<BroadcastedTransactionByHash> _broadcastTxHashRepo;
        private readonly INosqlRepo<BroadcastedOutpoint> _broadcastedOutpointRepo;

        public TransactionBroadcastService(
            IDcrdClient dcrdClient, 
            IBlockRepository blockRepository,
            ITransactionRepository txRepo,
            INosqlRepo<BroadcastedOutpoint> broadcastedOutpointRepo,
            INosqlRepo<BroadcastedTransaction> broadcastTxRepo,
            INosqlRepo<BroadcastedTransactionByHash> broadcastTxHashRepo)
        {
            _dcrdClient = dcrdClient;
            _blockRepository = blockRepository;
            _txRepo = txRepo;
            
            _broadcastTxRepo = broadcastTxRepo;
            _broadcastTxHashRepo = broadcastTxHashRepo;
            _broadcastedOutpointRepo = broadcastedOutpointRepo;
        }

        private string[] GetOutpointKeysForRawTransaction(string hexTransaction)
        {
            var txBytes = HexUtil.ToByteArray(hexTransaction);
            return GetOutpointKeysForRawTransaction(txBytes);
        }

        private string[] GetOutpointKeysForRawTransaction(byte[] txBytes)
        {
            var tx = Transaction.Deserialize(txBytes);
            return tx.Inputs.Select(input => input.PreviousOutpoint.ToString()).ToArray();
        }
        
        /// <summary>
        /// Broadcasts a signed transaction to the Decred network
        /// </summary>
        /// <param name="operationId"></param>
        /// <param name="hexTransaction"></param>
        /// <returns></returns>
        /// <exception cref="TransactionBroadcastException"></exception>
        public async Task Broadcast(Guid operationId, string hexTransaction)
        {               
            if (operationId == Guid.Empty)
                throw new BusinessException(ErrorReason.BadRequest, "Operation id is invalid");
            if (string.IsNullOrWhiteSpace(hexTransaction))
                throw new BusinessException(ErrorReason.BadRequest, "SignedTransaction is invalid");
            
            var txBytes = HexUtil.ToByteArray(hexTransaction);
            var msgTx = new MsgTx();
            msgTx.Decode(txBytes);

            // If the operation exists in the cache, throw exception
            var cachedResult = await _broadcastTxRepo.GetAsync(operationId.ToString());
            if (cachedResult != null)
                throw new BusinessException(ErrorReason.DuplicateRecord, "Operation already broadcast");
            
            // Submit the transaction to the network via dcrd
            var result = await _dcrdClient.SendRawTransactionAsync(hexTransaction);                
            if (result.Error != null)
                throw new TransactionBroadcastException($"[{result.Error.Code}] {result.Error.Message}");

            // Flag the consumed outpoints as spent.
            var outpoints = GetOutpointKeysForRawTransaction(txBytes);
            foreach (var outpoint in outpoints)
                _broadcastedOutpointRepo.InsertAsync(new BroadcastedOutpoint {Value = outpoint});
            
            var txHash = HexUtil.FromByteArray(msgTx.GetHash().Reverse().ToArray());
            await SaveBroadcastedTransaction(new BroadcastedTransaction
            {
                OperationId = operationId,
                Hash = txHash,
                EncodedTransaction = hexTransaction
            });   
        }
        
        /// <summary>
        /// Determines the state of a broadcasted transaction
        /// </summary>
        /// <param name="operationId"></param>
        /// <returns></returns>
        /// <exception cref="BusinessException"></exception>
        public async Task<BroadcastedSingleTransactionResponse> GetBroadcastedTxSingle(Guid operationId)
        {
            if (operationId == Guid.Empty)
                throw new BusinessException(ErrorReason.BadRequest, "Operation id is invalid");

            // Retrieve the broadcasted transaction and deserialize it.
            var broadcastedTransaction = await GetBroadcastedTransaction(operationId);
            var transaction = new MsgTx();
            transaction.Decode(HexUtil.ToByteArray(broadcastedTransaction.EncodedTransaction));
            
            // Calculate the fee and total amount spent from the transaction.
            var fee = transaction.TxIn.Sum(t => t.ValueIn) - transaction.TxOut.Sum(t => t.Value);
            var amount = transaction.TxOut.Sum(t => t.Value);
            
            // Check to see if the transaction has been included in a block.
            var safeBlockHeight = await _dcrdClient.GetMaxConfirmedBlockHeight();
            var knownTx = await _txRepo.GetTxInfoByHash(broadcastedTransaction.Hash, safeBlockHeight);
            var txState = knownTx == null
                ? BroadcastedTransactionState.InProgress
                : BroadcastedTransactionState.Completed;
            
            // If the tx has been included in a block,
            // use the block height + timestamp from the block
            var txBlockHeight = knownTx?.BlockHeight ?? safeBlockHeight;
            var timestamp = knownTx == null ? DateTime.UtcNow : DateTimeUtil.FromUnixTime(knownTx.BlockTime);
            
            return new BroadcastedSingleTransactionResponse
            {
                Block = txBlockHeight,
                State = txState,
                Hash = broadcastedTransaction.Hash,
                Amount = amount.ToString(),
                Fee = fee.ToString(),
                Error = "",
                ErrorCode = null,
                OperationId = operationId,
                Timestamp = timestamp
            };
        }

        public async Task UnsubscribeBroadcastedTx(Guid operationId)
        {
            if (operationId == Guid.Empty)
                throw new BusinessException(ErrorReason.BadRequest, "Operation id is invalid");

            var operation = await _broadcastTxRepo.GetAsync(operationId.ToString());
            if (operation == null)
                throw new BusinessException(ErrorReason.RecordNotFound, "Record not found");
            
            // Unflag outpoints as spent.
            var outpoints = GetOutpointKeysForRawTransaction(operation.EncodedTransaction);
            foreach (var outpoint in outpoints)
                await _broadcastedOutpointRepo.DeleteAsync(new BroadcastedOutpoint{Value = outpoint});
    
            await _broadcastTxRepo.DeleteAsync(operation);
        }
        
        private async Task SaveBroadcastedTransaction(BroadcastedTransaction broadcastedTx)
        {            
            // Store tx Hash to OperationId lookup
            await _broadcastTxHashRepo.InsertAsync(
                new BroadcastedTransactionByHash
                {
                    Hash = broadcastedTx.Hash,
                    OperationId = broadcastedTx.OperationId
                }
            );

            // Store operation
            await _broadcastTxRepo.InsertAsync(broadcastedTx);
        }

        private async Task<BroadcastedTransaction> GetBroadcastedTransaction(Guid operationId)
        {
            if (operationId == Guid.Empty)
                throw new BusinessException(ErrorReason.BadRequest, "Operation id is invalid");

            // Retrieve previously saved BroadcastedTransaction record.
            var broadcastedTx = await _broadcastTxRepo.GetAsync(operationId.ToString());
            return broadcastedTx ?? throw new BusinessException(ErrorReason.RecordNotFound, "Record not found");
        }
    }
}
