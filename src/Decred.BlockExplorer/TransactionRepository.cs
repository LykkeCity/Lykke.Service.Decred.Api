using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
                "select id from transactions where is_valid and is_mainchain and tx_hash = @txHash",
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

            // Create a hash set of transaction vins.
            var vinSet = transactions
                .SelectMany(tx => tx.Vin)
                .Select(vin => $"{vin.TxId}:{vin.Vout}")
                .ToImmutableHashSet();

            // Check if an outpoint is the input to another known transaction.
            bool IsSpent(string txId, TxVout txOut) => vinSet.Contains($"{txId}:{txOut.N}");

            // Filter out transactions that have a spent outpoint
            // Only grab transactions that spend to the provided address.
            return (
                from transaction in transactions
                where transaction.Confirmations == 0
                from txOut in transaction.Vout
                where txOut.ScriptPubKey.Addresses.Contains(address)
                where !IsSpent(transaction.TxId, txOut)
                select new UnspentTxOutput
                {
                    BlockHeight = 0,
                    BlockIndex = 4294967295,
                    Hash = transaction.TxId,
                    OutputIndex = (uint) txOut.N,
                    OutputValue = (long) (txOut.Value * (decimal) Math.Pow(10, 8)),
                    OutputVersion = txOut.Version,
                    PkScript = HexUtil.ToByteArray(txOut.ScriptPubKey.Hex),
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
                from transactions
                where is_valid and is_mainchain
                  and tx_hash = @txHash
                  and block_height <= @blockHeight";

            var results = await _dbConnection.QueryAsync<TxInfo>(query, new
            {
                txHash = transactionHash,
                blockHeight = blockHeight
            });

            return results.FirstOrDefault();
        }
    }
}
