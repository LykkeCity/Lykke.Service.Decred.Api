using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Service.Decred.Api.Common
{
    /// <summary>
    /// Wraps all observable repositories.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface INosqlRepo<T>
    {
        Task<T> GetAsync(string key);
        Task<IEnumerable<T>> GetAsync(IEnumerable<string> keys);
        
        Task InsertAsync(T entity, bool upsert = true);
        Task DeleteAsync(T entity);
        Task<(IEnumerable<T> Entities, string ContinuationToken)> GetDataWithContinuationTokenAsync(int take, string continuation);
    }
}
