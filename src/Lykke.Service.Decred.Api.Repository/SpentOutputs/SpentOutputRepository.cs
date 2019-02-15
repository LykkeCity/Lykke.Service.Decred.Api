using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Service.Decred.Api.Common.Domain;
using Lykke.Service.Decred.Api.Common.Extensions;

namespace Lykke.Service.Decred.Api.Repository.SpentOutputs
{
    public class SpentOutputRepository: ISpentOutputRepository
    {
        private readonly INoSQLTableStorage<SpentOutputEntity> _table;

        public SpentOutputRepository(INoSQLTableStorage<SpentOutputEntity> table)
        {
            _table = table;
        }

        public async Task InsertSpentOutputsAsync(Guid transactionId, IEnumerable<Output> outputs)
        {
            var entities = outputs.Select(o => SpentOutputEntity.Create(o.Hash, o.OutputIndex, transactionId));

            await entities.GroupBy(o => o.PartitionKey)
                .ForEachAsyncSemaphore(8, group => _table.InsertOrReplaceAsync(group));
        }

        public async Task<IEnumerable<Output>> GetSpentOutputsAsync(IEnumerable<Output> outputs)
        {
            return (await _table.GetDataAsync(outputs.Select(o =>
                    new Tuple<string, string>(SpentOutputEntity.GeneratePartitionKey(o.Hash),
                        SpentOutputEntity.GenerateRowKey(o.OutputIndex)))))
                .Select(p => new Output(p.Hash, p.OutputIndex));
        }

        public async Task RemoveOldOutputsAsync(DateTime bound)
        {
            string continuation = null;
            do
            {
                IEnumerable<SpentOutputEntity> outputs;
                (outputs, continuation) = await _table.GetDataWithContinuationTokenAsync(100, continuation);

                
                await outputs.Where(o => o.Timestamp < bound)
                    .ForEachAsyncSemaphore(8, async output =>
                        {
                            await _table.DeleteIfExistAsync(output.PartitionKey, output.RowKey);
                        });

            } while (continuation != null);
        }
    }
}
