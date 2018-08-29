namespace Decred.BlockExplorer
{
    public class TxHistoryResult
    {
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public long Amount { get; set; }
        public string Hash { get; set; }
        
        public long BlockHeight { get; set; }
        public long BlockTime { get; set; }
    }
}