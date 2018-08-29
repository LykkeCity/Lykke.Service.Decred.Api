using System.Linq;
using System.Threading.Tasks;
using DcrdClient;
using MoreLinq;
using Paymetheus.Decred;

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
    
    public class TransactionFeeService : ITransactionFeeService
    {
        private const int AtomsPerDcr = 100000000;

        private readonly IDcrdClient _dcrdClient;
        private static readonly Transaction.Output[] _dummyOutput = { new Transaction.Output(-1, 0, new byte[25]) };

        public TransactionFeeService(IDcrdClient dcrdClient)
        {
            _dcrdClient = dcrdClient;
        }

        public async Task<long> GetFeePerKb()
        {
            const int numBlocks = 12 * 6;
            return (long)(await _dcrdClient.EstimateFeeAsync(numBlocks) * AtomsPerDcr);
        }

        public long CalculateFee(long feePerKb, int numInputs, int numOutputs, decimal feeFactor)
        {
            var outputs = _dummyOutput.Repeat(numOutputs).ToArray();
            var serializeSizeBytes = Transaction.EstimateSerializeSize(numInputs, outputs, true);
            var serializeSizeKb = serializeSizeBytes / 1024m;
            return (long)(serializeSizeKb * feePerKb * feeFactor);
        }
    }
}
