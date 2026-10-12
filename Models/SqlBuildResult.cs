namespace NlpToSqlApi.Models
{
    public class SqlBuildResult
    {
        public bool Success { get; set; }
        public string? Sql { get; set; }
        public string? Error { get; set; }
        public string Operation { get; set; } = "select";
        public List<string> TablesUsed { get; set; } = new();
        public List<string> ColumnsUsed { get; set; } = new();
        public bool HasJoins { get; set; }
        public bool HasAggregations { get; set; }

        public static SqlBuildResult Fail(string error) => new() { Success = false, Error = error };
    }
}
