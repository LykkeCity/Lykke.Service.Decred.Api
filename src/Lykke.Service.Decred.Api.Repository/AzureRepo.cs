using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Service.Decred.Api.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Service.Decred.Api.Repository
{
    public class AzureRepo<T> : INosqlRepo<T> where T : TableEntity, new()
    {
        private const int RecordNotFoundStatus = 404;
        private const int DuplicateRecordStatus = 409;
        private const int InvalidRequestStatus = 500;

        private readonly INoSQLTableStorage<T> _azureRepo;
        
        public AzureRepo(INoSQLTableStorage<T> azureRepo)
        {
            _azureRepo = azureRepo;
        }

        public async Task<bool> ExistsAsync(string key)
        {
            var t = new T
            {
                RowKey = key,
                PartitionKey = "ByRowKey"
            };
            
            return await _azureRepo.RecordExistsAsync(t);
        }
        
        public async Task<T> GetAsync(string key)
        {
            return await _azureRepo.GetDataAsync("ByRowKey", key);
        }

        public async Task<IEnumerable<T>> GetAsync(IEnumerable<string> keys)
        {
            return await _azureRepo.GetDataAsync("ByRowKey", keys);
        }

        public async Task InsertAsync(T value, bool upsert)
        {
            try
            {
                if (upsert)
                    await _azureRepo.InsertOrReplaceAsync(value);
                else
                    await _azureRepo.InsertAsync(value);
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == DuplicateRecordStatus)
            {
                throw new BusinessException(ErrorReason.DuplicateRecord, $"{typeof(T)} already being observed", e);
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == InvalidRequestStatus)
            {
                throw new BusinessException(ErrorReason.BadRequest, $"{typeof(T)} Bad request", e);
            }
        }

        public async Task DeleteAsync(T entity)
        {
            try
            {
                entity.ETag = "*";
                await _azureRepo.DeleteAsync(entity);
            }
            catch (StorageException ex) when(ex.RequestInformation.HttpStatusCode == RecordNotFoundStatus)
            {
                throw new BusinessException(ErrorReason.RecordNotFound, $"{typeof(T)} is not being observed", ex);
            }
            
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == InvalidRequestStatus)
            {
                throw new BusinessException(ErrorReason.BadRequest, $"{typeof(T)} Bad request", e);
            }
        }

        public async Task<(IEnumerable<T> Entities, string ContinuationToken)> GetDataWithContinuationTokenAsync(int take, string continuation)
        {
            try
            {
                return await _azureRepo.GetDataWithContinuationTokenAsync(take, continuation);
            }
            catch (Exception ex)
            {
                throw new BusinessException(ErrorReason.BadRequest, 
                    $"Invalid take or continuation token", ex);
            }
        }
    }
}
