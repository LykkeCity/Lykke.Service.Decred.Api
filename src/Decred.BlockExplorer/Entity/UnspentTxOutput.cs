using NDecred.Common;

namespace Decred.BlockExplorer
{
    public class UnspentTxOutput
    {
        public byte Tree { get; set; }
        public string Hash { get; set; }
        public long OutputVersion { get; set; }
        public uint OutputIndex { get; set; }
        public long OutputValue { get; set; }
        public uint BlockHeight { get; set; }
        public uint BlockIndex { get; set; }
        public byte[] PkScript { get; set; }
    }
}