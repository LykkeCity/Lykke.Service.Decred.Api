using System.Collections.Generic;
using Decred.BlockExplorer;

namespace Lykke.Service.Decred.Api.Repository
{
    public class Output
    {
        public string Hash { get; set; }

        private sealed class HashOutputIndexEqualityComparer : IEqualityComparer<Output>
        {
            public bool Equals(Output x, Output y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return string.Equals(x.Hash, y.Hash) && x.OutputIndex == y.OutputIndex;
            }

            public int GetHashCode(Output obj)
            {
                unchecked
                {
                    return ((obj.Hash != null ? obj.Hash.GetHashCode() : 0) * 397) ^ (int) obj.OutputIndex;
                }
            }
        }

        public static IEqualityComparer<Output> HashOutputIndexComparer { get; } = new HashOutputIndexEqualityComparer();

        public uint OutputIndex { get; set; }


        public Output(string txHash, uint n)
        {
            Hash = txHash;
            OutputIndex = n;
        }

        public Output(UnspentTxOutput unspent)
        {
            Hash = unspent.Hash;
            OutputIndex = unspent.OutputIndex;
        }
    }
}
