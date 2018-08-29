using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Service.Decred.Api.Common.Entity
{
    public class UnsignedTransactionEntity : TableEntity
    {
        private Guid _operationId;

        public UnsignedTransactionEntity()
        {
            PartitionKey = "ByRowKey";
        }

        public UnsignedTransactionEntity(Guid operationId, string requestJson, string responseJson) : this()
        {
            OperationId = operationId;
            RequestJson = requestJson;
            ResponseJson = responseJson;
        }

        public Guid OperationId
        {
            get { return _operationId; }
            set { 
                _operationId = value;
                RowKey = value.ToString();
            }
        }
        
        public string RequestJson { get; set; }
        public string ResponseJson { get; set; }
    }
}
