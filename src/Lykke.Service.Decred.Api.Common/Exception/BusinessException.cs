using System;

namespace Lykke.Service.Decred.Api.Common
{
    public class BusinessException : Exception
    {
        public ErrorReason Reason { get; }

        public BusinessException(ErrorReason reason, string message = null, Exception innerException = null) 
            : base(message ?? reason.ToString(), innerException)
        {
            Reason = reason;
        }
    }

    public enum ErrorReason : byte
    {
        Unknown = 0xff,
        DuplicateRecord = 0,
        RecordNotFound = 1,
        InvalidAddress = 2,
        AmountTooSmall = 3,
        NotEnoughBalance = 4,
        BadRequest = 5
    }
}
