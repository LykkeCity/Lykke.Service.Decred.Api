using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Decred.BlockExplorer;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Common.Entity;
using NDecred.Common;

namespace Lykke.Service.Decred.Api.Services
{
    public class TransactionHistoryService
    {
        private readonly ITransactionRepository _txRepo;
        private readonly IAddressValidationService _addressValidator;
        private readonly INosqlRepo<BroadcastedTransactionByHash> _broadcastTxHashRepo;
        private readonly INosqlRepo<ObservableAddressEntity> _operationRepo;

        public TransactionHistoryService(
            ITransactionRepository txRepo,
            IAddressValidationService addressValidator,
            INosqlRepo<BroadcastedTransactionByHash> broadcastTxHashRepo,
            INosqlRepo<ObservableAddressEntity> operationRepo)
        {
            _txRepo = txRepo;
            _addressValidator = addressValidator;
            _broadcastTxHashRepo = broadcastTxHashRepo;
            _operationRepo = operationRepo;
        }
        
        /// <summary>
        /// Observe receiving transactions for this address.
        /// Only need this to return expected errors.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async Task SubscribeAddressFrom(string address)
        {
            _addressValidator.AssertValid(address);
            var entity = new ObservableAddressEntity(address, TxDirection.Outgoing);
            await _operationRepo.InsertAsync(entity);
        }

        /// <summary>
        /// Observe spending transactions for this address.
        /// Only need this to return expected errors.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async Task SubscribeAddressTo(string address)
        {
            _addressValidator.AssertValid(address);
            var entity = new ObservableAddressEntity(address, TxDirection.Incoming);
            await _operationRepo.InsertAsync(entity);
        }
        
        /// <summary>
        /// Stop watching for receiving transactions
        /// </summary>
        /// <param name="address"></param>
        /// <param name="take"></param>
        /// <param name="afterHash"></param>
        /// <returns></returns>
        public async Task UnsubscribeAddressFromHistory(string address)
        {
            _addressValidator.AssertValid(address);
            var entity = new ObservableAddressEntity(address, TxDirection.Outgoing);
            await _operationRepo.DeleteAsync(entity);
        }
        
        /// <summary>
        /// Stop observing spending transactions
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async Task UnsubscribeAddressToHistory(string address)
        {
            _addressValidator.AssertValid(address);
            var entity = new ObservableAddressEntity(address, TxDirection.Incoming);
            await _operationRepo.DeleteAsync(entity);
        }

        /// <summary>
        /// Finds known transactions from a particular address occuring after the transaction with the given hash
        /// 
        /// If afterHash is null, the earliest known transaction for the given address is returned first.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="take"></param>
        /// <param name="afterHash"></param>
        /// <returns></returns>
        public async Task<HistoricalTransactionContract[]> GetTransactionsFromAddress(string address, int take, string afterHash)
        {
            _addressValidator.AssertValid(address);
            var transactions = await _txRepo.GetTransactionsFromAddress(address, take, afterHash);
            return await GetHistoricalTransactionContracts(transactions);
        }

        public async Task<HistoricalTransactionContract[]> GetTransactionsToAddress(string address, int take, string afterHash = null)
        {
            _addressValidator.AssertValid(address);
            var transactions = await _txRepo.GetTransactionsToAddress(address, take, afterHash);
            return await GetHistoricalTransactionContracts(transactions);
        }
        
        private async Task<HistoricalTransactionContract[]> GetHistoricalTransactionContracts(TxHistoryResult[] transactions)
        {
            return transactions.Select(tx => new HistoricalTransactionContract
            {
                Amount = tx.Amount.ToString(),
                AssetId = "DCR",
                FromAddress = tx.FromAddress,
                ToAddress = tx.ToAddress,
                Hash = tx.Hash,
                Timestamp = DateTimeUtil.FromUnixTime(tx.BlockTime)
            }).ToArray();
        }
    }
}
