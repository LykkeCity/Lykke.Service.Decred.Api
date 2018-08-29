using System;

namespace DcrdClient
{
    public class DcrdException : Exception
    {
        public DcrdException(string message, Exception innerException = null)
            : base(message, innerException)
        {

        }
    }
}
