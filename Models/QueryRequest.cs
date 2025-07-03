namespace NlpToSqlApi.Models
{
    public class QueryRequest
    {
        public string Query { get; set; } = string.Empty;
        public string? DatabasePath { get; set; }
    }
}