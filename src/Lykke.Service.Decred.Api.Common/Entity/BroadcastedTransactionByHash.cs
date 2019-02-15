using System;
using Lykke.AzureStorage.Tables;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Service.Decred.Api.Common.Entity
{
    public class BroadcastedTransactionByHash : AzureTableEntity
    {
        private string _hash;

        public BroadcastedTransactionByHash()
        {
            PartitionKey = "RowKey";
        }
        
        public string Hash
        {
            get { return _hash; }
            set { _hash = value;
                RowKey = value;
            }
        }

        public Guid OperationId { get; set; }
    }
}
