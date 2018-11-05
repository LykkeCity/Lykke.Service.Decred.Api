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
        private readonly IHttpClientFactory _httpClientFactory;

        public DcrdHttpClient(DcrdHttpConfig config, IHttpClientFactory httpClientFactory)
        {
            _apiUrl = config.ApiUrl;
            _httpClientFactory = httpClientFactory;
            _minConfirmations = config.MinConfirmations;
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
            var httpClient = _httpClientFactory.CreateClient();

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

            return deserializedResponse;
        }

        public async Task<string> PingAsync()
        {
            var result = await PerformAsync<string>("ping");
            if (result.HasError) throw new DcrdException(result.Error.ToString());
            return result.Result;
        }

        public async Task<DcrdRpcResponse<string>> SendRawTransactionAsync(string hexTransaction)
        {
            return await PerformAsync<string>("sendrawtransaction", hexTransaction);
        }

        public async Task<GetBestBlockResult> GetBestBlockAsync()
        {
            var result = await PerformAsync<GetBestBlockResult>("getbestblock");
            if (result.HasError) throw new DcrdException(result.Error.ToString());
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

        public async Task<decimal> EstimateFeeAsync(int numBlocks)
        {
            var result = await PerformAsync<decimal>("estimatefee", numBlocks);
            if (result.HasError) throw new DcrdException(result.Error.ToString());
            return result.Result;
        }
    }
}
