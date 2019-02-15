using System.Linq;
using System.Threading.Tasks;
using Lykke.Common.Api.Contract.Responses;
using Lykke.Service.Decred.Api.Common.Services;
using Lykke.Service.Decred.Api.ViewModels;
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
            var status = await _healthService.GetHealthStatusAsync();
            return Ok(new TimeStampIsAliveResponse
            {
                Name = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationName,
                Version = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion,
                Env = Program.EnvInfo,
                IsDebug = true,
                IssueIndicators = status.issues
                    .Select(i => new IsAliveResponse.IssueIndicator
                    {
                        Type = i.Type,
                        Value = i.Value
                    }),
                Updated = status.updated
            });
        }

    }
}
