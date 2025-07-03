using System.Data;

namespace NlpToSqlApi.Models
{
    public class QueryResponse
    {
        public bool Success { get; set; }
        public string? Query { get; set; }
        public string? GeneratedSql { get; set; }
        public List<Dictionary<string, object>>? Results { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
        public string? DatabaseId { get; set; }
        public int RowCount { get; set; }
        public QueryMetadata? Metadata { get; set; }
    }

    public class QueryMetadata
    {
        public string? Operation { get; set; }
        public List<string>? TablesUsed { get; set; }
        public List<string>? ColumnsUsed { get; set; }
        public bool HasJoins { get; set; }
        public bool HasAggregations { get; set; }
    }
}