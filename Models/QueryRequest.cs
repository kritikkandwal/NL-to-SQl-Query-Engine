namespace NlpToSqlApi.Models
{
    public class QueryRequest
    {
        public string Query { get; set; } = string.Empty;
        public string? DatabasePath { get; set; }
        public string Language { get; set; } = "en"; // New property
    }
}