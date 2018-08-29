using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using DcrdClient;
using Decred.BlockExplorer;
using Lykke.Service.Decred.Api.Common.Domain.Health;
using Lykke.Service.Decred.Api.Common.Services;

namespace Lykke.Service.Decred.Api.Services
{
    // NOTE: See https://lykkex.atlassian.net/wiki/spaces/LKEWALLET/pages/35755585/Add+your+app+to+Monitoring
    public class HealthService : IHealthService
    {
        private readonly ILog _log;
        private readonly IDcrdClient _dcrdClient;
        private readonly IBlockRepository _blockRepository;

        public HealthService(
            ILog log, 
            IDcrdClient client,
            IBlockRepository blockRepository)
        {
            _log = log;
            _dcrdClient = client;
            _blockRepository = blockRepository;
        }
        
        public string GetHealthViolationMessage()
        {
            return null;
        }

        public async Task<IEnumerable<HealthIssue>> GetHealthIssuesAsync()
        {
            try
            {
                var dcrdIssues = await GetDcrdHealthIssues();
                if (dcrdIssues.Any())
                    return dcrdIssues;
            
                var dcrdataIssues = await GetDcrdataHealthIssues();
                if (dcrdataIssues.Any())
                    return dcrdataIssues;
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(HealthService), nameof(GetDcrdataHealthIssues), "", e);
                return new[]
                {
                    HealthIssue.Create("UnknownHealthIssue", e.Message), 
                };
            }

            return Enumerable.Empty<HealthIssue>();
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
                await _log.WriteErrorAsync(nameof(HealthService), nameof(GetDcrdHealthIssues), "", e);
                return new[]
                {
                    HealthIssue.Create("DcrdPingFailure", 
                        $"Failed to ping dcrd.  {e.Message}".Trim())
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
