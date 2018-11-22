using System;
using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.BlockchainApi.Contract.Assets;
using Lykke.Service.Decred.Api.Common;
using Lykke.SettingsReader;
using Microsoft.AspNetCore.Mvc;

namespace Lykke.Service.Decred.Api.Controllers
{
    public class AssetsController : Controller
    {
        private readonly AssetResponse _assetResponse;

        public AssetsController(IReloadingManager<AppSettings> settings)
        {
            var assetConfig = settings.CurrentValue.ServiceSettings.Asset;

            if(assetConfig == null)
                throw new ArgumentException("Asset property must be set.");
            if(assetConfig.Precision < 0)
                throw new ArgumentException("Asset precision must be greater than zero.");
            if(string.IsNullOrWhiteSpace(assetConfig.AssetId))
                throw new ArgumentException("Asset id cannot be null.");
            if (string.IsNullOrWhiteSpace(assetConfig.Name))
                throw new ArgumentException("Asset name cannot be null.");

            _assetResponse = new AssetResponse
            {
                Accuracy = assetConfig.Precision,
                AssetId = assetConfig.AssetId,
                Name = assetConfig.Name
            };
        }
        
        [HttpGet("api/assets")]
        public PaginationResponse<AssetResponse> GetAssets([FromQuery] int? take, [FromQuery] string continuation)
        {
            if(take == null || take < 1)
                throw new BusinessException(ErrorReason.BadRequest, "Invalid take parameter");
            if(continuation != null)
                throw new BusinessException(ErrorReason.BadRequest, "Continuation token not expected at this endpoint");
            
            return PaginationResponse.From(null, new [] { _assetResponse });
        }
        
        [HttpGet("api/assets/{assetId}")]
        public IActionResult GetById(string assetId)
        {
            if (assetId == _assetResponse.AssetId)
                return Ok(_assetResponse);

            return NoContent();
        }
    }
}
