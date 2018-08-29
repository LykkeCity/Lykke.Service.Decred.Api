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
        
        public async Task<long?> GetTransactionRowId(string hash)
        {
            if(hash == null) return null;
            return await _dbConnection.ExecuteScalarAsync<long?>(
                "select id from transactions where tx_hash = @txHash",
                new { txHash = hash });
        }

        public async Task<TxHistoryResult[]> GetTransactionsFromAddress(string address, int take, string afterHash)
        {
            if(take < 1)
                throw new ArgumentException("Take argument must be >= 1");

            const string query = 
                @"select
                    from_addr.address as FromAddress,
                    to_addr.address as ToAddress,
                    to_addr.value as Amount,
                    to_addr.funding_tx_hash as Hash,
                    tx.block_height as BlockHeight,
                    tx.block_time as BlockTime
                from addresses from_addr
                join addresses to_addr on to_addr.funding_tx_hash = from_addr.spending_tx_hash
                join transactions tx on tx.tx_hash = to_addr.funding_tx_hash
                where from_addr.address = @address and to_addr.funding_tx_row_id > @minTxId
                group by 
                    from_addr.address, 
                    to_addr.address, 
                    to_addr.value, to_addr.funding_tx_hash, to_addr.funding_tx_row_id, 
                    tx.block_height, tx.block_time
                order by to_addr.funding_tx_row_id asc
                limit @take";
            
            var minTxIdExclusive = await GetTransactionRowId(afterHash) ?? 0;
            var results = await _dbConnection.QueryAsync<TxHistoryResult>(query,
                new { address = address, take = take, minTxId = minTxIdExclusive });
            return results.ToArray();
        }
        
        public async Task<TxHistoryResult[]> GetTransactionsToAddress(string address, int take, string afterHash)
        {
            if(take < 1)
                throw new ArgumentException("Take argument must be >= 1");
            
            const string query = 
                @"select
                    from_addr.address as FromAddress,
                    to_addr.address as ToAddress,
                    to_addr.value as Amount,
                    to_addr.funding_tx_hash as Hash,
                    tx.block_height as BlockHeight,
                    tx.block_time as BlockTime
                from addresses from_addr
                join addresses to_addr on to_addr.funding_tx_hash = from_addr.spending_tx_hash
                join transactions tx on tx.tx_hash = to_addr.funding_tx_hash
                where to_addr.address = @address and to_addr.funding_tx_row_id > @minTxId
                group by 
                    from_addr.address, 
                    to_addr.address, 
                    to_addr.value, to_addr.funding_tx_hash, to_addr.funding_tx_row_id, 
                    tx.block_height, tx.block_time
                order by to_addr.funding_tx_row_id asc
                limit @take";

            var minTxIdExclusive = await GetTransactionRowId(afterHash) ?? 0;
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
                    addr.funding_tx_vout_index OutputIndex,
                    vouts.version as OutputVersion,
                    vouts.value as OutputValue,
                    tx.block_height as BlockHeight,
                    tx.block_index as BlockIndex,
                    vouts.pkscript as PkScript
                from addresses addr
                join vouts on addr.vout_row_id = vouts.id
                join transactions tx on tx.tx_hash = vouts.tx_hash
                where
                    addr.address = @address
                    and addr.spending_tx_hash is null
                    and vouts.script_type = 'pubkeyhash'";
            
            var results = (await _dbConnection.QueryAsync<UnspentTxOutput>(
                query, new { address = address }
            )).ToArray();
            
            return results;
        }

        public async Task<UnspentTxOutput[]> GetMempoolUtxos(string address)
        {
            var transactions =  await _dcrdClient.SearchRawTransactions(address,
                count: 100,
                reverse: true);

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
