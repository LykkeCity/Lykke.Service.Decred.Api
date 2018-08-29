using System;
using System.Threading.Tasks;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Common.Entity;
using Newtonsoft.Json;

namespace Lykke.Service.Decred.Api.Services
{
    public interface IUnsignedTransactionService
    {
        Task<BuildTransactionResponse> BuildSingleTransactionAsync(
            BuildSingleTransactionRequest request,
            decimal feeFactor);
    }
    
    public class UnsignedTransactionService : IUnsignedTransactionService
    {
        private readonly ITransactionBuilder _builder;
        private readonly INosqlRepo<UnsignedTransactionEntity> _unsignedTxRepo;

        public UnsignedTransactionService(
            ITransactionBuilder builder,
            INosqlRepo<UnsignedTransactionEntity> unsignedTxRepo)
        {
            _builder = builder;
            _unsignedTxRepo = unsignedTxRepo;
        }

        public async Task<BuildTransactionResponse> BuildSingleTransactionAsync(BuildSingleTransactionRequest request,
            decimal feeFactor)
        {
            if (request.OperationId == Guid.Empty)
                throw new BusinessException(ErrorReason.BadRequest, "Operation id is invalid");
            
            var unsignedTx = await _unsignedTxRepo.GetAsync(request.OperationId.ToString());
            if (unsignedTx?.ResponseJson != null)
                return JsonConvert.DeserializeObject<BuildTransactionResponse>(unsignedTx.ResponseJson);

            var response = await _builder.BuildSingleTransactionAsync(request, feeFactor);

            var entity = new UnsignedTransactionEntity(
                request.OperationId,
                JsonConvert.SerializeObject(request),
                JsonConvert.SerializeObject(response)
            );

            await _unsignedTxRepo.InsertAsync(entity);

            return response;
        }
    }
}
