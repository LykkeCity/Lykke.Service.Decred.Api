using System;
using Lykke.AzureStorage.Tables;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Service.Decred.Api.Common.Entity
{
    public class BroadcastedTransaction : AzureTableEntity
    {
        private Guid _operationId;

        public BroadcastedTransaction()
        {
            PartitionKey = "ByRowKey";
        }

        public Guid OperationId
        {
            get { return _operationId; }
            set { 
                _operationId = value;
                RowKey = value.ToString();
            }
        }

        public string Hash { get; set; }     
        public string EncodedTransaction { get; set; }
    }
}
