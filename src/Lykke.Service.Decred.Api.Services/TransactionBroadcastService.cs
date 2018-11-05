using System;
using System.Linq;
using System.Threading.Tasks;
using Common;
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
    public class TransactionBroadcastService : ITransactionBroadcastService
    {
        private readonly IDcrdClient _dcrdClient;
        private readonly ITransactionRepository _txRepo;

        private readonly INosqlRepo<BroadcastedTransaction> _broadcastTxRepo;
        private readonly INosqlRepo<BroadcastedTransactionByHash> _broadcastTxHashRepo;

        public TransactionBroadcastService(
            IDcrdClient dcrdClient,
            ITransactionRepository txRepo,
            INosqlRepo<BroadcastedTransaction> broadcastTxRepo,
            INosqlRepo<BroadcastedTransactionByHash> broadcastTxHashRepo)
        {
            _dcrdClient = dcrdClient;
            _txRepo = txRepo;

            _broadcastTxRepo = broadcastTxRepo;
            _broadcastTxHashRepo = broadcastTxHashRepo;
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

            // Deserialize the raw transaction
            var txBytes = HexUtil.ToByteArray(hexTransaction);
            var msgTx = new MsgTx();
            msgTx.Decode(txBytes);

            // Calculate the hash of the transaction
            var txHash = HexUtil.FromByteArray(msgTx.GetHash().Reverse().ToArray());

            // If the operation exists in the cache, throw exception
            var cachedResult = await _broadcastTxRepo.GetAsync(operationId.ToString());
            if (cachedResult != null)
                throw new BusinessException(ErrorReason.DuplicateRecord, "Operation already broadcast");

            // Submit the transaction to the network via dcrd
            var result = await _dcrdClient.SendRawTransactionAsync(hexTransaction);

            var wasBroadcast =
                result.Error == null ||
                result.Error.Code == (int) RpcErrorCode.DuplicateTx ||
                result.Error.Message.Contains("transaction already exists");

            if (wasBroadcast)
            {
                // Flag the transaction + operation id as broadcasted.
                await SaveBroadcastedTransaction(new BroadcastedTransaction
                {
                    OperationId = operationId,
                    Hash = txHash,
                    EncodedTransaction = hexTransaction
                });
            }

            else
            {
                throw new DcrdException(
                    "Broadcast failed due to unhandled dcrd error.\n" +
                    $"{result.ToJson()}"
                );
            }
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
            var timestamp = knownTx == null ? DateTime.UtcNow : DateTimeOffset.FromUnixTimeSeconds(knownTx.BlockTime).UtcDateTime;

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
                }, true
            );

            // Store operation
            await _broadcastTxRepo.InsertAsync(broadcastedTx, true);
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
