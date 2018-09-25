using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Common.Health;

namespace Lykke.Service.Decred.Api.Common.Services
{
    // NOTE: See https://lykkex.atlassian.net/wiki/spaces/LKEWALLET/pages/35755585/Add+your+app+to+Monitoring
    public interface IHealthService
    {
        string GetHealthViolationMessage();
        Task<IEnumerable<HealthIssue>> GetHealthIssuesAsync();
    }
}
