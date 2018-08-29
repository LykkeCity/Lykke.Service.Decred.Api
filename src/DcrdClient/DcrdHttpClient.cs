using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DcrdClient
{
    /// <summary>
    /// Http client to communicate with dcrd.
    /// </summary>
    public class DcrdHttpClient : IDcrdClient
    {
        private readonly string _apiUrl;
        private readonly int _minConfirmations;
        private readonly HttpClientHandler _httpClientHandler;

        public DcrdHttpClient(string apiUrl, HttpClientHandler httpClientHandler, int minConfirmations = 6)
        {
            _apiUrl = apiUrl;
            _httpClientHandler = httpClientHandler;
            _minConfirmations = minConfirmations;
        }

        private async Task<DcrdRpcResponse<T>> Perform<T>(string method, params object[] parameters)
        {
            using (var httpClient = new HttpClient(_httpClientHandler, false))
            {
                var request = new
                {
                    jsonrpc = "1.0",
                    id = "0",
                    method = method,
                    @params = parameters
                };
                
                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(_apiUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException(responseString);
                
                return JsonConvert.DeserializeObject<DcrdRpcResponse<T>>(responseString);
            }
        }

        public async Task<DcrdRpcResponse<string>> PingAsync()
        {
            return await Perform<string>("ping");
        }

        public async Task<DcrdRpcResponse<string>> SendRawTransactionAsync(string hexTransaction)
        {
            return await Perform<string>("sendrawtransaction", hexTransaction);
        }

        public async Task<GetBestBlockResult> GetBestBlockAsync()
        {
            var result = await Perform<GetBestBlockResult>("getbestblock");
            return result.Result;
        }
        
        public async Task<SearchRawTransactionsResult[]> SearchRawTransactions(
            string address, 
            int skip = 0, 
            int count = 100,
            int vinExtra = 0,
            bool reverse = false)
        {
            const int verbose = 1;
            
            // Documented in: dcrctl searchrawtransactions
            // verbose=1 skip=0 count=100 vinextra=0 reverse=false
            
            var result = await Perform<SearchRawTransactionsResult[]>("searchrawtransactions", 
                address, verbose, skip, count, vinExtra, reverse);
            
            return result.Result;
        }

        
        public async Task<long> GetMaxConfirmedBlockHeight()
        {
            var result = await GetBestBlockAsync();
            return result.Height - _minConfirmations;
        }

        public int GetConfirmationDepth()
        {
            return _minConfirmations;
        }

        public async Task<decimal> EstimateFeeAsync(int numBlocks)
        {
            var result = await Perform<decimal>("estimatefee", numBlocks);
            return result.Result;
        }
    }
}
