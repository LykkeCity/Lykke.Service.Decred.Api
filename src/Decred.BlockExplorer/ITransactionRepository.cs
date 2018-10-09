using System.Threading.Tasks;

namespace Decred.BlockExplorer
{
    public interface ITransactionRepository
    {
        /// <summary>
        /// Retrieves transactions spent by the address in ascending order (oldest first)
        /// </summary>
        /// <param name="address"></param>
        /// <param name="take"></param>
        /// <param name="afterHash"></param>
        /// <returns></returns>
        Task<TxHistoryResult[]> GetTransactionsFromAddress(string address, int take, string afterHash);

        /// <summary>
        /// Retrieves transactions spent to the address in ascending order (oldest first)
        /// </summary>
        /// <param name="address"></param>
        /// <param name="take"></param>
        /// <param name="afterHash"></param>
        /// <returns></returns>
        Task<TxHistoryResult[]> GetTransactionsToAddress(string address, int take, string afterHash);

        /// <summary>
        /// Returns all unspent outpoints for this address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<UnspentTxOutput[]> GetConfirmedUtxos(string address);

        /// <summary>
        /// Returns
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<UnspentTxOutput[]> GetMempoolUtxos(string address);

        /// <summary>
        /// Determines if a transaction is known, given its hash.
        /// </summary>
        /// <param name="transactionHash"></param>
        /// <returns></returns>
        Task<TxInfo> GetTxInfoByHash(string transactionHash, long blockHeight);
    }
}