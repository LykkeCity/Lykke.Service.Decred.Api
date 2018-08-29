using System;
using Lykke.Service.Decred.Api.Common;
using NDecred.Common;
using Paymetheus.Decred.Wallet;

namespace Lykke.Service.Decred.Api.Services
{
    public interface IAddressValidationService
    {
        /// <summary>
        /// Returns whether or not a given address is valid on the current network.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        bool IsValid(string address);

        /// <summary>
        /// Checks if the supplied address is valid.
        /// Throws a business exception with reason BadRequest
        /// if the address is not valid.
        /// </summary>
        /// <param name="address"></param>
        void AssertValid(string address);
    }

    public class AddressValidationService : IAddressValidationService
    {
        private readonly Network _network;
        
        public AddressValidationService(Network network)
        {
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (network.Name == null) throw new ArgumentNullException(nameof(network.Name));
            
            switch (network.Name.ToLower())
            {
                case "mainnet":
                case "testnet":
                    _network = network;
                    break;
                default:
                    throw new ArgumentException("Invalid network");
            }
        }
        
        public bool IsValid(string address)
        {            
            return Address.TryDecode(address, out var addr) && addr.IntendedBlockChain.Name == _network.Name.ToLower();            
        }

        public void AssertValid(string address)
        {
            if(string.IsNullOrWhiteSpace(address))
                throw new BusinessException(ErrorReason.BadRequest, "Address required");
            if(!IsValid(address))
                throw new BusinessException(ErrorReason.BadRequest, "Address is not valid");
        }
    }
}
