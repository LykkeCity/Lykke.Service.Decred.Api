using System.Linq;
using System.Threading.Tasks;
using Lykke.Common.Api.Contract.Responses;
using Lykke.Service.Decred.Api.Common.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lykke.Service.Decred.Api.Controllers
{
    public class StatusController : Controller
    {
        private readonly IHealthService _healthService;

        public StatusController(IHealthService healthService)
        {
            _healthService = healthService;
        }
        
        [HttpGet("/api/isalive")]
        public async Task<IActionResult> GetStatus()
        {
            var healthIssues = await _healthService.GetHealthIssuesAsync();
            return Ok(new IsAliveResponse
            {
                Name = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationName,
                Version = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion,
                Env = Program.EnvInfo,
                IsDebug = true,
                IssueIndicators = healthIssues
                    .Select(i => new IsAliveResponse.IssueIndicator
                    {
                        Type = i.Type,
                        Value = i.Value
                    })
            });
        }

    }
}
