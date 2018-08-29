using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Service.Decred.Api.Common.Entity
{
    public enum TxDirection
    {
        Incoming = 0,
        Outgoing = 1
    }

    public class ObservableAddressEntity : TableEntity
    {
        private string _address;

        public ObservableAddressEntity()
        {
            PartitionKey = "ByDirectedAddress";
        }
        
        public ObservableAddressEntity(string address, TxDirection direction) : this()
        {
            Address = address;
            TxDirection = direction;
        }

        public string Address
        {
            get { return _address; }
            set { _address = value;
                RowKey = value;
            }
        }

        public TxDirection TxDirection { get; set; }
        public string DirectedAddress {
            get { return Address + TxDirection; }
            set {  }
        }
    }
}
