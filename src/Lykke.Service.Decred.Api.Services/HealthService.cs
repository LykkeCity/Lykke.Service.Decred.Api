using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using DcrdClient;
using Decred.BlockExplorer;
using Lykke.Common.Health;
using Lykke.Common.Log;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Common.Entity;
using Lykke.Service.Decred.Api.Common.Services;

namespace Lykke.Service.Decred.Api.Services
{
    // NOTE: See https://lykkex.atlassian.net/wiki/spaces/LKEWALLET/pages/35755585/Add+your+app+to+Monitoring
    public class HealthService : IHealthService
    {
        private readonly ILog _log;
        private readonly IDcrdClient _dcrdClient;
        private readonly IBlockRepository _blockRepository;
        private readonly INosqlRepo<HealthStatusEntity> _healthStatusRepo;

        public HealthService(
            ILogFactory lf,
            IDcrdClient client,
            IBlockRepository blockRepository, INosqlRepo<HealthStatusEntity> healthStatusRepo)
        {
            _log = lf.CreateLog(this);
            _dcrdClient = client;
            _blockRepository = blockRepository;
            _healthStatusRepo = healthStatusRepo;
        }

        public string GetHealthViolationMessage()
        {
            return null;
        }

        public async Task<(DateTime updated, IEnumerable<HealthIssue> issues)> GetHealthStatusAsync()
        {
            var result = (await _healthStatusRepo.GetAsync(HealthStatusEntity.RowKeyDefaultValue));
            if (result != null)
            {
                return (updated: result.Updated, result.HealthIssues.Select(p => HealthIssue.Create(p.Type, p.Value)));
            }

            return (DateTime.MinValue, Enumerable.Empty<HealthIssue>());
        }

        public async Task UpdateHealthStatus()
        {
            var result = new List<HealthIssue>();
            try
            {
                result.AddRange(await GetDcrdHealthIssues());
                result.AddRange(await GetDcrdataHealthIssues());
            }
            catch (Exception e)
            {
                _log.Error(e);

                result.Add(HealthIssue.Create("UnknownHealthIssue", e.Message));
            }

            await _healthStatusRepo.InsertAsync(new HealthStatusEntity
            {
                HealthIssues = result.Select(p => new HealthStatusEntity.HealthIssue
                {
                    Type = p.Type,
                    Value = p.Value
                }).ToArray(),
                Updated = DateTime.UtcNow
            });
        }

        private async Task<HealthIssue[]> GetDcrdHealthIssues()
        {
            try
            {
                await _dcrdClient.PingAsync();
                return new HealthIssue[0];
            }
            catch (Exception e)
            {
                _log.Error(e,process: nameof(GetDcrdHealthIssues));
                return new[]
                {
                    HealthIssue.Create("DcrdPingFailure",
                        $"Failed to ping dcrd.  {e}".Trim())
                };
            }
        }

        private async Task<HealthIssue[]> GetDcrdataHealthIssues()
        {
            var dcrdataTopBlock = await _blockRepository.GetHighestBlock();
            if (dcrdataTopBlock == null)
            {
                return new []
                {
                    HealthIssue.Create("NoDcrdataBestBlock",
                        "No blocks found in dcrdata database"),
                };
            }

            // Get dcrd block height.  If dcrdata out of sync, raise failure.
            var dcrdTopBlock = await _dcrdClient.GetBestBlockAsync();
            if (dcrdTopBlock == null)
            {
                return new []
                {
                    HealthIssue.Create("NoDcrdBestBlock",
                        "No blocks found with dcrd getbestblock"),
                };
            }

            // If the difference in block height
            const int unsyncedThreshold = 3;
            var isUnsynced = Math.Abs(dcrdTopBlock.Height - dcrdataTopBlock.Height) > unsyncedThreshold;
            if (isUnsynced)
            {
                return new []
                {
                    HealthIssue.Create("BlockHeightOutOfSync",
                        $"dcrd at blockheight {dcrdTopBlock.Height} while dcrdata at blockheight {dcrdataTopBlock.Height}"),
                };
            }

            return new HealthIssue[0];
        }
    }
}
