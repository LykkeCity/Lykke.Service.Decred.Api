using System.Threading.Tasks;
using Common;

namespace Lykke.Service.Decred.Api.Common.Services
{
    public interface IShutdownManager
    {
        Task StopAsync();

        void Register(IStopable stopable);
    }
}
