using System;
using Lykke.Common.Api.Contract.Responses;
using Newtonsoft.Json;

namespace Lykke.Service.Decred.Api.ViewModels
{
    public class TimeStampIsAliveResponse:IsAliveResponse
    {
        [JsonProperty("updated")]
        public DateTime Updated { get; set; }
    }
}
