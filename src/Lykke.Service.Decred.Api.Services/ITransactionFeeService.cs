using System.Threading.Tasks;

namespace Lykke.Service.Decred.Api.Services
{
    public interface ITransactionFeeService
    {
        /// <summary>
        /// Returns fee per kb in atoms.
        /// </summary>
        /// <returns></returns>
        Task<long> GetFeePerKb();
        long CalculateFee(long feePerKb, int numInputs, int numOutputs, decimal feeFactor);
    }
}