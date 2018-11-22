using Newtonsoft.Json;

namespace DcrdClient
{
    public class TxVout
    {
        [JsonProperty("value")]
        public decimal Value { get; set; }

        [JsonProperty("n")]
        public long N { get; set; }

        [JsonProperty("version")]
        public long Version { get; set; }

        [JsonProperty("scriptPubKey")]
        public ScriptPubKey ScriptPubKey { get; set; }
    }
}
