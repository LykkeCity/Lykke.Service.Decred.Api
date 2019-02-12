using System;
using System.Data;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Service.Decred.Api.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.Service.Decred.Api.Workflow.PeriodicalHandlers
{
    public class UpdateHealStatusPeriodicalHandler : IStartable, IStopable
    {
        private readonly ILog _log;
        private readonly TimerTrigger _timerTrigger;
        private IServiceScopeFactory _serviceScopeFactory;

        public UpdateHealStatusPeriodicalHandler(TimeSpan timerPeriod, 
            ILogFactory logFactory, 
            IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _log = logFactory.CreateLog(this);

            _timerTrigger = new TimerTrigger(nameof(UpdateHealStatusPeriodicalHandler), timerPeriod, logFactory);
            _timerTrigger.Triggered += (trigger, args, token) => Execute();
        }

        public async Task Execute()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IHealthService>().UpdateHealthStatus();
            }
        }

        public void Start()
        {
            _log.Info("Starting");

            _timerTrigger.Start();
        }

        public void Dispose()
        {
            _timerTrigger.Dispose();
        }

        public void Stop()
        {
            _timerTrigger.Stop();
        }
    }
}
