using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DcrdClient;
using NDecred.Common;
using Paymetheus.Decred.Script;
using Paymetheus.Decred.Wallet;

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

    public class TransactionRepository : ITransactionRepository
    {
        private readonly IDcrdClient _dcrdClient;
        private readonly IDbConnection _dbConnection;

        public TransactionRepository(IDcrdClient dcrdClient, IDbConnection dbConnection)
        {
            _dcrdClient = dcrdClient;
            _dbConnection = dbConnection;
        }

        public async Task<long> GetTransactionRowId(string hash)
        {
            if(hash == null) return 0;
            return await _dbConnection.ExecuteScalarAsync<long?>(
                "select id from transactions where tx_hash = @txHash",
                new { txHash = hash }) ?? 0;
        }

        enum TxAddrOp { From, To }
        private string GetAddressTransactionsQuery(TxAddrOp direction)
        {
            var filterByTable =
                direction == TxAddrOp.From ? "from_addr" :
                direction == TxAddrOp.To ? "to_addr" :
                throw new InvalidOperationException();

            return
               $@"select
                  from_addr.address as FromAddress,
                  to_addr.address as ToAddress,
                  to_addr.value as Amount,
                  to_addr.tx_hash as Hash,
                  tx.block_height as BlockHeight,
                  tx.block_time as BlockTime
                from addresses from_addr
                join addresses to_addr on to_addr.tx_hash = from_addr.matching_tx_hash
                join transactions tx on tx.tx_hash = to_addr.tx_hash
                where from_addr.is_funding = true
                  and from_addr.valid_mainchain = true
                  and to_addr.is_funding = true
                  and to_addr.valid_mainchain = true
                  and {filterByTable}.address = @address
                  and tx.id > @minTxId
                order by to_addr.id asc
                limit @take";
        }

        public async Task<TxHistoryResult[]> GetTransactionsFromAddress(string address, int take, string afterHash)
        {
            if(take < 1)
                throw new ArgumentException("Take argument must be >= 1");

            var query = GetAddressTransactionsQuery(TxAddrOp.From);
            var minTxIdExclusive = await GetTransactionRowId(afterHash);
            var results = await _dbConnection.QueryAsync<TxHistoryResult>(query,
                new { address = address, take = take, minTxId = minTxIdExclusive });
            return results.ToArray();
        }

        public async Task<TxHistoryResult[]> GetTransactionsToAddress(string address, int take, string afterHash)
        {
            if(take < 1)
                throw new ArgumentException("Take argument must be >= 1");

            var query = GetAddressTransactionsQuery(TxAddrOp.To);
            var minTxIdExclusive = await GetTransactionRowId(afterHash);
            var results = await _dbConnection.QueryAsync<TxHistoryResult>(query,
                new { address = address, take = take, minTxId = minTxIdExclusive });
            return results.ToArray();
        }


        public async Task<UnspentTxOutput[]> GetConfirmedUtxos(string address)
        {
            const string query =
              @"select
                  vouts.tx_tree Tree,
                  vouts.tx_hash as Hash,
                  addr.tx_vin_vout_index OutputIndex,
                  vouts.version as OutputVersion,
                  vouts.value as OutputValue,
                  tx.block_height as BlockHeight,
                  tx.block_index as BlockIndex,
                  vouts.pkscript as PkScript
                from addresses addr
                join vouts on addr.tx_vin_vout_row_id = vouts.id
                join transactions tx on tx.tx_hash = vouts.tx_hash
                where
                  addr.is_funding = true
                  and addr.valid_mainchain = true
                  and addr.address = @address
                  and addr.matching_tx_hash = ''
                  and vouts.script_type = 'pubkeyhash'";

            var results = (await _dbConnection.QueryAsync<UnspentTxOutput>(
                query, new { address = address }
            )).ToArray();

            return results;
        }

        private async Task<SearchRawTransactionsResult[]> GetMempoolUtxosInternal(string address)
        {
            var empty = new SearchRawTransactionsResult[0];

            try
            {
                var results = await _dcrdClient.SearchRawTransactions(address, count: 100, reverse: true);
                return results.Result ?? empty;
            }
            catch (DcrdException)
            {
                return empty;
            }
        }

        public async Task<UnspentTxOutput[]> GetMempoolUtxos(string address)
        {
            var transactions =  await GetMempoolUtxosInternal(address);

            // Check if an outpoint is the input to another known transaction.
            bool IsSpent(string txId, TxVout txOut) =>
                transactions.SelectMany(tx => tx.vin)
                    .Any(vin => vin.txid == txId && vin.vout == txOut.n);

            // Filter out transactions that have a spent outpoint
            // Only grab transactions that spend to the provided address.
            return (
                from transaction in transactions
                where transaction.confirmations == 0
                from txOut in transaction.vout
                where txOut.scriptPubKey.addresses.Contains(address)
                where !IsSpent(transaction.txid, txOut)
                select new UnspentTxOutput
                {
                    BlockHeight = 0,
                    BlockIndex = 4294967295,
                    Hash = transaction.txid,
                    OutputIndex = (uint) txOut.n,
                    OutputValue = (long) (txOut.value * (decimal) Math.Pow(10, 8)),
                    OutputVersion = txOut.version,
                    PkScript = HexUtil.ToByteArray(txOut.scriptPubKey.hex),
                    Tree = 0
                }).ToArray();
        }

        public async Task<TxInfo> GetTxInfoByHash(string transactionHash, long blockHeight)
        {
            const string query =
                @"select
                    tx_hash as TxHash,
                    block_height as  BlockHeight,
                    block_time as BlockTime
                from transactions where tx_hash = @txHash and block_height <= @blockHeight";

            var results = await _dbConnection.QueryAsync<TxInfo>(query, new
            {
                txHash = transactionHash,
                blockHeight = blockHeight
            });

            return results.FirstOrDefault();
        }
    }
}
