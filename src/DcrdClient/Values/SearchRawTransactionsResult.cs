using System.Collections.Generic;

namespace DcrdClient
{
    public class ScriptSig
    {
        public string asm { get; set; }
        public string hex { get; set; }
    }

    public class TxVin
    {
        public string txid { get; set; }
        public long vout { get; set; }
        public long tree { get; set; }
        public decimal amountin { get; set; }
        public long blockheight { get; set; }
        public long blockindex { get; set; }
        public ScriptSig scriptSig { get; set; }
        public object sequence { get; set; }
    }

    public class ScriptPubKey
    {
        public string asm { get; set; }
        public string hex { get; set; }
        public long reqSigs { get; set; }
        public string type { get; set; }
        public string[] addresses { get; set; }
    }

    public class TxVout
    {
        public decimal value { get; set; }
        public long n { get; set; }
        public long version { get; set; }
        public ScriptPubKey scriptPubKey { get; set; }
    }

    public class SearchRawTransactionsResult
    {
        public string hex { get; set; }
        public string txid { get; set; }
        public long version { get; set; }
        public long locktime { get; set; }
        public TxVin[] vin { get; set; }
        public TxVout[] vout { get; set; }
        public string blockhash { get; set; }
        public long confirmations { get; set; }
        public long time { get; set; }
        public long blocktime { get; set; }
    }
}