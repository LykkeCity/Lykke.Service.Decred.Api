using System;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Service.Decred.Api.Common.Services;

namespace Lykke.Service.Decred.Api.Workflow.PeriodicalHandlers
{
    public class UpdateHealStatusPeriodicalHandler : IStartable, IStopable
    {
        private readonly ILog _log;
        private readonly TimerTrigger _timerTrigger;
        private readonly IHealthService _healthService;

        public UpdateHealStatusPeriodicalHandler(TimeSpan timerPeriod, 
            ILogFactory logFactory, 
            IHealthService healthService)
        {
            _healthService = healthService;
            _log = logFactory.CreateLog(this);

            _timerTrigger = new TimerTrigger(nameof(UpdateHealStatusPeriodicalHandler), timerPeriod, logFactory);
            _timerTrigger.Triggered += (trigger, args, token) => Execute();
        }

        public Task Execute()
        {
            return _healthService.UpdateHealthStatus();
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
