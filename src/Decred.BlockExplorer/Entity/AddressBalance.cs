namespace Decred.BlockExplorer
{
    public class AddressBalance
    {
        public string Address { get; set; }
        public long Balance { get; set; }
        public long BlockHeight { get; set; }

        public AddressBalance()
        {
        }
    }
}