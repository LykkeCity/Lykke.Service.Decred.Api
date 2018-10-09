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
}