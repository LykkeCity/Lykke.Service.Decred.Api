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
        private readonly HttpMessageHandler _httpMessageHandler;

        public DcrdHttpClient(string apiUrl, HttpMessageHandler httpMessageHandler, int minConfirmations = 6)
        {
            _apiUrl = apiUrl;
            _httpMessageHandler = httpMessageHandler;
            _minConfirmations = minConfirmations;
        }

        private static DcrdRpcResponse<T> ParseResponse<T>(string responseBody)
        {
            try
            {
                return JsonConvert.DeserializeObject<DcrdRpcResponse<T>>(responseBody);
            }
            catch (Exception)
            {
                throw new DcrdException($"Failed to deserialize dcrd response: {responseBody}");
            }
        }

        public async Task<DcrdRpcResponse<T>> PerformAsync<T>(string method, params object[] parameters)
        {
            using (var httpClient = new HttpClient(_httpMessageHandler, false))
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
                    throw new DcrdException(responseString);

                var deserializedResponse = ParseResponse<T>(responseString);

                if (deserializedResponse == null)
                    throw new DcrdException($"Failed to deserialize dcrd response: {responseString}");

                if (deserializedResponse.Error != null)
                    throw new DcrdException(deserializedResponse.Error.Message);

                return deserializedResponse;
            }
        }

        public async Task<DcrdRpcResponse<string>> PingAsync()
        {
            return await PerformAsync<string>("ping");
        }

        public async Task<DcrdRpcResponse<string>> SendRawTransactionAsync(string hexTransaction)
        {
            return await PerformAsync<string>("sendrawtransaction", hexTransaction);
        }

        public async Task<GetBestBlockResult> GetBestBlockAsync()
        {
            var result = await PerformAsync<GetBestBlockResult>("getbestblock");
            return result.Result;
        }

        public async Task<DcrdRpcResponse<SearchRawTransactionsResult[]>> SearchRawTransactions(
            string address,
            int skip = 0,
            int count = 100,
            int vinExtra = 0,
            bool reverse = false)
        {
            const int verbose = 1;

            // Documented in: dcrctl searchrawtransactions
            // verbose=1 skip=0 count=100 vinextra=0 reverse=false

            return await PerformAsync<SearchRawTransactionsResult[]>("searchrawtransactions",
                address, verbose, skip, count, vinExtra, reverse);
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
            var result = await PerformAsync<decimal>("estimatefee", numBlocks);
            return result.Result;
        }
    }
}
