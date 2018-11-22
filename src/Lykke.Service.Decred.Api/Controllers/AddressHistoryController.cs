using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lykke.Service.Decred.Api.Controllers
{
    public class AddressHistoryController : Controller
    {
        private readonly IAddressValidationService _addressValidator;
        private readonly TransactionHistoryService _transactionHistoryService;

        public AddressHistoryController(
            IAddressValidationService addressValidator,
            TransactionHistoryService transactionHistoryService)
        {
            _addressValidator = addressValidator;
            _transactionHistoryService = transactionHistoryService;
        }
        
        [HttpPost("api/transactions/history/from/{address}/observation")]
        public async Task<IActionResult> SubscribeAddressFrom(string address)
        {
            _addressValidator.AssertValid(address);
            
            try
            {
                await _transactionHistoryService.SubscribeAddressFrom(address);
                return Ok();
            }
            catch (BusinessException e) when(e.Reason == ErrorReason.DuplicateRecord)
            {
                return new StatusCodeResult(409);
            }
        }
        
        [HttpPost("api/transactions/history/to/{address}/observation")]
        public async Task<IActionResult> SubscribeAddressTo(string address)
        {
            _addressValidator.AssertValid(address);

            try
            {
                await _transactionHistoryService.SubscribeAddressTo(address);
                return Ok();
            }
            catch (BusinessException e) when(e.Reason == ErrorReason.DuplicateRecord)
            {
                return new StatusCodeResult(409);
            }
        }
        
        [HttpGet("api/transactions/history/from/{address}")]
        public async Task<IEnumerable<HistoricalTransactionContract>> GetTransactionsFromAddress(string address, int take, string afterHash = null)
        {
            _addressValidator.AssertValid(address);
            return await _transactionHistoryService.GetTransactionsFromAddress(address, take, afterHash);
        }
        
        [HttpGet("api/transactions/history/to/{address}")]
        public async Task<IEnumerable<HistoricalTransactionContract>> GetTransactionsToAddress(string address, int take, string afterHash = null)
        {
            _addressValidator.AssertValid(address);
            return await _transactionHistoryService.GetTransactionsToAddress(address, take, afterHash);
        }
        
        [HttpDelete("api/transactions/history/from/{address}/observation")]
        public async Task<IActionResult> UnsubscribeFromAddressHistory(string address)
        {
            _addressValidator.AssertValid(address);

            try
            {
                await _transactionHistoryService.UnsubscribeAddressFromHistory(address);
                return Ok();
            }
            catch (BusinessException e) when(e.Reason == ErrorReason.RecordNotFound)
            {
                return NoContent();
            }
        }
        
        [HttpDelete("api/transactions/history/to/{address}/observation")]
        public async Task<IActionResult> UnsubscribeToAddressHistory(string address)
        {
            _addressValidator.AssertValid(address);

            try
            {
                await _transactionHistoryService.UnsubscribeAddressToHistory(address);
                return Ok();
            }
            catch (BusinessException e) when(e.Reason == ErrorReason.RecordNotFound)
            {
                return NoContent();
            }
        }
    }
}
