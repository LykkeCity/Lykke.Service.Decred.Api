using System;
using Lykke.AzureStorage.Tables;

namespace Lykke.Service.Decred.Api.Repository.SpentOutputs
{
    public class SpentOutputEntity : AzureTableEntity
    {
        public Guid OperationId { get; set; }

        public string Hash { get; set; }
        public uint OutputIndex { get; set; }

        public static SpentOutputEntity Create(string transactionHash, uint n, Guid operationId)
        {
            return new SpentOutputEntity
            {
                PartitionKey = GeneratePartitionKey(transactionHash),
                RowKey = GenerateRowKey(n),
                OperationId = operationId,
                Hash = transactionHash,
                OutputIndex = n
            };
        }

        public static string GenerateRowKey(uint n)
        {
            return n.ToString();
        }

        public static string GeneratePartitionKey(string transactionHash)
        {
            return transactionHash;
        }
    }
}
