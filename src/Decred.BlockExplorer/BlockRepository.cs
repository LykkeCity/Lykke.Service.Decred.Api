using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace Decred.BlockExplorer
{
    public interface IBlockRepository
    {
        /// <summary>
        /// Returns the highest known valid block's height.
        /// </summary>
        /// <returns>block height</returns>
        Task<Block> GetHighestBlock();
    }
    
    public class BlockRepository : IBlockRepository
    {
        private readonly IDbConnection _dbConnection;

        public BlockRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }
        
        public async Task<Block> GetHighestBlock()
        {
            var result = await _dbConnection.QueryAsync<Block>("select max(height) as Height from blocks");
            return result.First();
        }
    }

    public class Block
    {
        public long Height { get; set; }
    }
}
