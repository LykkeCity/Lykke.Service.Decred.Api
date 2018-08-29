using System;
using System.Net;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Service.BlockchainApi.Contract.Common;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lykke.Service.Decred.Api.Controllers
{
    public class TransactionController : Controller
    {
        private readonly ILog _log;
        private readonly IAddressValidationService _addressValidationService;
        private readonly IUnsignedTransactionService _txBuilderService;
        private readonly ITransactionBroadcastService _txBroadcastService;

        public TransactionController(
            ILog log,
            IAddressValidationService addressValidationService,
            IUnsignedTransactionService txBuilderService,
            ITransactionBroadcastService txBroadcastService)
        {
            _log = log;
            _addressValidationService = addressValidationService;
            _txBuilderService = txBuilderService;
            _txBroadcastService = txBroadcastService;
        }
        
        [HttpGet("api/capabilities")]
        public CapabilitiesResponse GetCapabilities()
        {
            return new CapabilitiesResponse
            {
                AreManyInputsSupported = false,
                AreManyOutputsSupported = false,
                IsTransactionsRebuildingSupported = false
            };
        }
        
        [HttpPost("api/transactions/single")]
        public async Task<IActionResult> BuildSingleTransaction([FromBody] BuildSingleTransactionRequest request)
        {
            // Do not scale the fee
            const int feeFactor = 1;
            return await BuildTxInternal(request, feeFactor);
        }
        
        private async Task<IActionResult> BuildTxInternal(BuildSingleTransactionRequest request, decimal feeFactor)
        {
            _addressValidationService.AssertValid(request?.FromAddress);
            _addressValidationService.AssertValid(request?.ToAddress);

            try
            {
                var response = await _txBuilderService.BuildSingleTransactionAsync(request, feeFactor);
                return Json(response);
            }

            catch (BusinessException exception) when (exception.Reason == ErrorReason.AmountTooSmall)
            {
                Response.StatusCode = (int) HttpStatusCode.Conflict;
                return Json(new
                {
                    errorCode = "amountIsTooSmall",
                    transactionContext = (string) null
                });
            }

            catch (BusinessException exception) when (exception.Reason == ErrorReason.NotEnoughBalance)
            {
                Response.StatusCode = (int) HttpStatusCode.Conflict;
                return Json(new
                {
                    errorCode = "notEnoughBalance",
                    transactionContext = (string) null
                });
            }
        }
        
        [HttpPost("api/transactions/broadcast")]
        public async Task<IActionResult> Broadcast([FromBody] BroadcastTransactionRequest request)
        {
            try
            {
                // Broadcast the signed transaction
                await _txBroadcastService.Broadcast(request.OperationId, request.SignedTransaction);
                Response.StatusCode = (int) HttpStatusCode.OK;
                return Json(new { errorMessage = "" });
            }
            catch (BusinessException ex) when (ex.Reason == ErrorReason.DuplicateRecord)
            {
                // If this operationid was already broadcast, return 409 conflict.
                return await GenericErrorResponse(ex, request.OperationId, HttpStatusCode.Conflict);
            }
        }
        
        [HttpGet("api/transactions/broadcast/single/{operationId}")]
        public async Task<IActionResult> GetBroadcastedSingleTx(Guid operationId)
        {
            if(operationId == Guid.Empty)
                throw new BusinessException(ErrorReason.BadRequest, "Invalid operation id");
                
            try
            {
                // Retrieves a broadcasted transaction
                var result = await _txBroadcastService.GetBroadcastedTxSingle(operationId);
                return Json(result);
            }
            catch (BusinessException e) when (e.Reason == ErrorReason.RecordNotFound)
            {
                return NotFound();
            }

            catch (Exception e)
            {
                return await GenericErrorResponse(e, operationId, HttpStatusCode.BadRequest);
            }
        }

        [HttpDelete("api/transactions/broadcast/{operationId}")]
        public async Task<IActionResult> RemoveObservableOperation(Guid operationId)
        {
            if (operationId == Guid.Empty)
                throw new BusinessException(ErrorReason.BadRequest, "Operation id is invalid");

            try
            {
                await _txBroadcastService.UnsubscribeBroadcastedTx(operationId);                
                return Ok();
            }
            catch (BusinessException e) when(e.Reason == ErrorReason.RecordNotFound)
            {
                return NoContent();
            }
            catch (Exception e)
            {
                return await GenericErrorResponse(e, operationId, HttpStatusCode.InternalServerError);
            }
        }
        
        private async Task<JsonResult> GenericErrorResponse(Exception ex, Guid operationId, HttpStatusCode status)
        {
            Response.StatusCode = (int) status;
            await _log.WriteErrorAsync(nameof(TransactionController), nameof(GenericErrorResponse), operationId.ToString(), ex);
            return Json(new { errorMessage = ex.ToString() });
        }

        #region Not implemented endpoints

        [HttpPost("api/transactions/many-inputs")]
        public IActionResult BuildManyInputsTransaction([FromBody] BuildTransactionWithManyInputsRequest request)
        {
            return StatusCode((int) HttpStatusCode.NotImplemented);
        }
        
        [HttpPost("api/transactions/many-outputs")]
        public IActionResult BuildManyOutputsTransaction([FromBody] BuildTransactionWithManyOutputsRequest request)
        {
            return StatusCode((int) HttpStatusCode.NotImplemented);
        }

        [HttpPut("api/transactions")]
        public IActionResult RebuildTransaction([FromBody] RebuildTransactionRequest request)
        {
            return StatusCode((int) HttpStatusCode.NotImplemented);
        }

        [HttpGet("api/transactions/broadcast/many-inputs/{operationId}")]
        public IActionResult GetBroadcastedManyInputsTx(Guid operationId)
        {
            return StatusCode((int) HttpStatusCode.NotImplemented);
        }
        
        [HttpGet("api/transactions/broadcast/many-outputs/{operationId}")]
        public IActionResult GetBroadcastedManyOutputsTx(Guid operationId)
        {
            return StatusCode((int) HttpStatusCode.NotImplemented);
        }
        
        #endregion
    }
}
