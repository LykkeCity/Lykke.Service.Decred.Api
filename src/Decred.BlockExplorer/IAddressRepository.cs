using System.Threading.Tasks;

namespace Decred.BlockExplorer
{
    public interface IAddressRepository
    {
        /// <summary>
        /// Determines the unspent balance of each address at a point in time.
        /// </summary>
        /// <param name="maxBlockHeight">Maximum block height to scan up to</param>
        /// <param name="addresses">List of addresses to retrive balances for</param>
        /// <returns>Collection of address balances.</returns>
        Task<AddressBalance[]> GetAddressBalancesAsync(string[] addresses, long blockHeight);
    }
}
