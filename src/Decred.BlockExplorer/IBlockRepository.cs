using System.Threading.Tasks;

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
}