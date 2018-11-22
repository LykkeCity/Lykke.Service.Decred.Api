using System;
using System.Threading.Tasks;
using Lykke.Service.BlockchainApi.Contract.Transactions;

namespace Lykke.Service.Decred.Api.Services
{
    public interface ITransactionBroadcastService
    {
        Task Broadcast(Guid operationId, string hexTransaction);
        Task UnsubscribeBroadcastedTx(Guid operationId);
        Task<BroadcastedSingleTransactionResponse> GetBroadcastedTxSingle(Guid operationId);
    }
}