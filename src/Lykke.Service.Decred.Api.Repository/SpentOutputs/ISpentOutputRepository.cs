using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.Decred.Api.Common.Domain;

namespace Lykke.Service.Decred.Api.Repository.SpentOutputs
{
    public interface ISpentOutputRepository
    {
        Task InsertSpentOutputsAsync(Guid transactionId, IEnumerable<Output> outputs);

        Task<IEnumerable<Output>> GetSpentOutputsAsync(IEnumerable<Output> outputs);

        Task RemoveOldOutputsAsync(DateTime bound);
    }
}
