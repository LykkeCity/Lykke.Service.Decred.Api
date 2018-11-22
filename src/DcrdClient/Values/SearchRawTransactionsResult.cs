using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DcrdClient
{
    public class SearchRawTransactionsResult
    {
        [JsonProperty("hex")]
        public string Hex { get; set; }

        [JsonProperty("txid")]
        public string TxId { get; set; }

        [JsonProperty("version")]
        public long Version { get; set; }

        [JsonProperty("locktime")]
        public long LockTime { get; set; }

        [JsonProperty("vin")]
        public TxVin[] Vin { get; set; }

        [JsonProperty("vout")]
        public TxVout[] Vout { get; set; }

        [JsonProperty("blockhash")]
        public string BlockHash { get; set; }

        [JsonProperty("confirmations")]
        public long Confirmations { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("blocktime")]
        public long BlockTime { get; set; }
    }
}
