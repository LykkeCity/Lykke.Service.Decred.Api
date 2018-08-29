using Lykke.Service.BlockchainApi.Contract.Addresses;
using Lykke.Service.Decred.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lykke.Service.Decred.Api.Controllers
{
    public class AddressController : Controller
    {
        private readonly IAddressValidationService _addressValidator;
        
        public AddressController(IAddressValidationService addressValidator)
        {
            _addressValidator = addressValidator;
        }
        
        /// <summary>
        /// Determines if an address is valid on the current network.
        /// </summary>
        /// <param name="address">the address to validate</param>
        /// <returns></returns>
        [HttpGet("api/addresses/{address}/validity")]
        public AddressValidationResponse IsValid(string address)
        {
            return new AddressValidationResponse
            {
                IsValid = _addressValidator.IsValid(address)
            };
        }
    }
}
