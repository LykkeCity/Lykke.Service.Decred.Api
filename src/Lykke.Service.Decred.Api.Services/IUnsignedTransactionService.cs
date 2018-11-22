using System.Threading.Tasks;
using Lykke.Service.BlockchainApi.Contract.Transactions;

namespace Lykke.Service.Decred.Api.Services
{
    public interface IUnsignedTransactionService
    {
        Task<BuildTransactionResponse> BuildSingleTransactionAsync(
            BuildSingleTransactionRequest request,
            decimal feeFactor);
    }
}