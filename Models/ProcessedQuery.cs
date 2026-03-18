namespace NlpToSqlApi.Models
{
    public class ProcessedQuery
    {
        public string OriginalQuery { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public List<string> Tables { get; set; } = new();
        public List<string> Columns { get; set; } = new();
        public List<QueryCondition> Conditions { get; set; } = new();
        public List<AggregationFunction> Aggregations { get; set; } = new();
        public bool JoinsNeeded { get; set; }
    }
}
