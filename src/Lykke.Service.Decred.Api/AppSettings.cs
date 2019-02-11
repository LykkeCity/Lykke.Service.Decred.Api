using System;
using Lykke.Service.Decred.Api.Services;
using Lykke.SettingsReader.Attributes;
using Newtonsoft.Json;

namespace Lykke.Service.Decred.Api
{
    public class AppSettings
    {
        public ServiceSettings ServiceSettings { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }
        [Optional] public MonitoringServiceClientSettings MonitoringServiceClient { get; set; }
    }

    public class AssetConfig
    {
        public string AssetId { get; set; }
        public string Name { get; set; }
        public int Precision { get; set; }
    }
    
    public class DcrdSettings
    {
        public string RpcEndpoint { get; set; }
        public string RpcUser { get; set; }
        public string RpcPass { get; set; }
    }

    public class DbSettings
    {
        [AzureTableCheck]
        public string LogsConnString { get; set; }
        
        public string Azure { get; set; }
        public string Dcrdata { get; set; }
    }
    
    public class ServiceSettings
    {
        public string NetworkType { get; set; }
        public int ConfirmationDepth { get; set; } = 6;
        public AssetConfig Asset { get; set; }
        public DcrdSettings Dcrd { get; set; }
        public DbSettings Db { get; set; }
        [Optional]
        public TimeSpan UpdateHealthStatusTimerPeriod { get; set; } = TimeSpan.FromSeconds(30);
        [Optional] public TimeSpan SpentOutputsExpiration { get; set; } = TimeSpan.FromDays(1);

        [Optional] public TimeSpan SpentOutputsExpirationTimerPeriod { get; set; } = TimeSpan.FromMinutes(10);
    }
    
    public class SlackNotificationsSettings
    {
        public AzureQueuePublicationSettings AzureQueue { get; set; }
    }

    public class AzureQueuePublicationSettings
    {
        public string ConnectionString { get; set; }
        public string QueueName { get; set; }
    }

    public class MonitoringServiceClientSettings
    {
        /// <summary>
        ///     Gets or sets the monitoring service URL.
        /// </summary>
        [HttpCheck("api/isalive")]
        public string MonitoringServiceUrl { get; set; }
    }
}
