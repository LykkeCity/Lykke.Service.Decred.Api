using System.Threading.Tasks;

namespace DcrdClient
{
    public interface IDcrdClient
    {
        Task<DcrdRpcResponse<string>> PingAsync();
        Task<DcrdRpcResponse<string>> SendRawTransactionAsync(string hexTransaction);

        /// <summary>
        /// Returns transactions related to an address.
        /// Includes transactions up to a certain confirmation level.
        /// </summary>
        /// <param name="maxConfirmations"></param>
        /// <returns></returns>
        Task<DcrdRpcResponse<SearchRawTransactionsResult[]>> SearchRawTransactions(
            string address,
            int skip = 0,
            int count = 100,
            int vinExtra = 0,
            bool reverse = false);

        Task<GetBestBlockResult> GetBestBlockAsync();
        Task<long> GetMaxConfirmedBlockHeight();
        int GetConfirmationDepth();

        // Returns estimated fee as dcr/kb
        Task<decimal> EstimateFeeAsync(int numBlocks);
    }
}
