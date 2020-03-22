using System;

namespace Decred.BlockExplorer
{
    public class TxInfo
    {
        public string TxHash { get; set; }
        public DateTimeOffset BlockTime { get; set; }
        public long BlockHeight { get; set; }
    }
}
