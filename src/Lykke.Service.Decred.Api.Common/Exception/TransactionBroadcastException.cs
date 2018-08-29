using System;

namespace Lykke.Service.Decred.Api.Services
{
    public class TransactionBroadcastException : Exception
    {
        public TransactionBroadcastException(string message = null, Exception innerException = null) 
            : base(message, innerException)
        {
        }
    }
}