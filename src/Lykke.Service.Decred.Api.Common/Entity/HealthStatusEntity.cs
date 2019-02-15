using System;
using System.Linq;
using Lykke.AzureStorage.Tables;
using Lykke.AzureStorage.Tables.Entity.Annotation;

namespace Lykke.Service.Decred.Api.Common.Entity
{
    public class HealthStatusEntity:AzureTableEntity
    {
        public const string RowKeyDefaultValue = "_";
        public const string PartitionKeyDefaultValue = "ByRowKey";

        public HealthStatusEntity()
        {
            PartitionKey = PartitionKeyDefaultValue;
            RowKey = RowKeyDefaultValue;

            HealthIssues = Array.Empty<HealthIssue>();
        }

        [JsonValueSerializer]
        public HealthIssue[] HealthIssues { get; set; }
        
        public class HealthIssue
        {
            public string Type { get; set; }

            public string Value { get; set; }
        }
    }
}
