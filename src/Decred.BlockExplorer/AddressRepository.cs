using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

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

    /// <summary>
    /// Readonly client to read data from the dcrdata postgres database.
    /// </summary>
    public class AddressRepository : IAddressRepository
    {
        private readonly IDbConnection _dbConnection;

        public AddressRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<AddressBalance[]> GetAddressBalancesAsync(string[] addresses, long blockHeight)
        {
            const string query =
                @"select
                    user_address as Address,
                    coalesce(max(block_height), @blockHeight) as BlockHeight,
                    coalesce(sum(case when matching_tx_hash = '' then value else 0 end), 0) as Balance
                from unnest(@addresses) user_address
                left join addresses on user_address = addresses.address
                left join transactions on transactions.tx_hash = addresses.tx_hash
                where block_height <= @blockHeight or block_height is null
                group by user_address";

            var results = await _dbConnection.QueryAsync<AddressBalance>(query, new { blockHeight, addresses });
            return results.ToArray();
        }
    }
}
