using System;
using Lykke.Service.Decred.Api.Common;
using NDecred.Common;
using Paymetheus.Decred.Wallet;

namespace Lykke.Service.Decred.Api.Services
{
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
