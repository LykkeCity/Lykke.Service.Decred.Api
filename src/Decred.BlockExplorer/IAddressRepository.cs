using System.Threading.Tasks;

namespace Decred.BlockExplorer
{
    public interface IAddressRepository
    {
        /// <summary>
        /// Determines the unspent balance of each address at a point in time.
        /// </summary>
        /// <param name="maxBlockHeight"></param>
        /// <param name="addresses"></param>
        /// <returns></returns>
        Task<AddressBalance[]> GetAddressBalancesAsync(string[] addresses, long blockHeight);
    }
}