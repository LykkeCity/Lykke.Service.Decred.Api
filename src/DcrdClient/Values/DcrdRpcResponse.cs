namespace DcrdClient
{
    public class DcrdRpcResponse<T>
    {
        public string Id { get; set; }
        public string Jsonrpc { get; set; }
        public T Result { get; set; }
        public DcrdRpcError Error { get; set; }
        
        public class DcrdRpcError
        {
            public int? Code { get; set; }
            public string Message { get; set; }
        }
    }
}