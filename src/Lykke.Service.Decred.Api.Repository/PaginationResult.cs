using System.Collections.Generic;

namespace Lykke.Service.Decred.Api.Repository
{
    public class PaginationResult<T>
    {
        public IEnumerable<T> Items { get; }
        public string Continuation { get; }

        public PaginationResult(IEnumerable<T> items, string continuation)
        {
            Items = items;
            Continuation = continuation;
        }
    }
}
