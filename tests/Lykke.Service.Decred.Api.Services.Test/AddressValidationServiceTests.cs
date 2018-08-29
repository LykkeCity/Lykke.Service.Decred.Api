using NDecred.Common;
using Xunit;

namespace Lykke.Service.Decred.Api.Services.Test
{
    public class AddressValidationServiceTests
    {
        private readonly (string network, string address, bool isValid)[] _tests = {
            ("mainnet", "DsUZxxoHJSty8DCfwfartwTYbuhmVct7tJu", true),
            ("mainnet", "DsU7xcg53nxaKLLcAUSKyRndjG78Z2VZnX9", true),
            ("mainnet", "DsU7xcg53nxaKLLcAUSKyRndjG78Z2VZnX0", false),
            ("testnet", "Tso2MVTUeVrjHTBFedFhiyM7yVTbieqp91h", true),
            ("testnet", "TsmWaPM77WSyA3aiQ2Q1KnwGDVWvEkhip23", false),
        };

        /// <summary>
        /// Verify that each address is valid / not valid on the respective network
        /// </summary>
        [Fact]
        public void AddressValidationService_IsValid_ReturnsExpectedResult()
        {
            foreach (var test in _tests)
            {
                var network = Network.ByName(test.network);
                var service = new AddressValidationService(network);
                var result = service.IsValid(test.address);
                Assert.Equal(test.isValid, result);
            }
        }
    }
}
