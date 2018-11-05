namespace DcrdClient
{
    public class DcrdRpcResponse<T>
    {
        public string Id { get; set; }
        public string Jsonrpc { get; set; }
        public T Result { get; set; }
        public DcrdRpcError Error { get; set; }
        public bool HasError => Error != null;

        public class DcrdRpcError
        {
            public int? Code { get; set; }
            public string Message { get; set; }

            public override string ToString()
            {
                return $"[{Code}]: {Message}";
            }
        }
    }
}
