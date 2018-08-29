using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Service.Decred.Api.Common.Entity
{
    public class BroadcastedOutpoint : TableEntity
    {
        private string _value;
    
        public BroadcastedOutpoint()
        {
            PartitionKey = "ByRowKey";
        }
    
        public string Value
        {
            get { return _value; }
            set { 
                _value = value;
                RowKey = value;
            }
        }
    }
}