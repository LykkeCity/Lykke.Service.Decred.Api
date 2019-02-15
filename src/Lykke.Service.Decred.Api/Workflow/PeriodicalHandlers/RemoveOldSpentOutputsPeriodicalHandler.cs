using System;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Service.Decred.Api.Repository.SpentOutputs;

namespace Lykke.Service.Decred.Api.Workflow.PeriodicalHandlers
{
    public class RemoveOldSpentOutputsPeriodicalHandler : IStartable, IStopable
    {
        private readonly ILog _log;
        private readonly TimerTrigger _timerTrigger;
        private readonly TimeSpan _expiration;
        private readonly ISpentOutputRepository _spentOutputRepository;

        public RemoveOldSpentOutputsPeriodicalHandler(
            ILogFactory logFactory,
            TimeSpan timerPeriod,
            TimeSpan expiration, 
            ISpentOutputRepository spentOutputRepository)
        {
            _expiration = expiration;
            _spentOutputRepository = spentOutputRepository;
            _log = logFactory.CreateLog(this);

            _timerTrigger = new TimerTrigger(nameof(RemoveOldSpentOutputsPeriodicalHandler), timerPeriod, logFactory);
            _timerTrigger.Triggered += (trigger, args, token) => Execute();
        }

        public async Task Execute()
        {
            var bound = DateTime.UtcNow.Add(-_expiration);
            await _spentOutputRepository.RemoveOldOutputsAsync(bound);
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
