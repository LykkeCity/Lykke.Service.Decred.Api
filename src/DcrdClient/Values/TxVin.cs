using Newtonsoft.Json;

namespace DcrdClient
{
    public class TxVin
    {
        [JsonProperty("txid")]
        public string TxId { get; set; }

        [JsonProperty("vout")]
        public long Vout { get; set; }

        [JsonProperty("tree")]
        public long Tree { get; set; }

        [JsonProperty("amountin")]
        public decimal AmountIn { get; set; }

        [JsonProperty("blockheight")]
        public long BlockHeight { get; set; }

        [JsonProperty("blockindex")]
        public long BlockIndex { get; set; }

        [JsonProperty("scriptSig")]
        public ScriptSig ScriptSig { get; set; }

        [JsonProperty("sequence")]
        public object Sequence { get; set; }
    }
}
