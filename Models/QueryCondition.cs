namespace NlpToSqlApi.Models
{
    public class QueryCondition
    {
        public string Column { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public object Value { get; set; } = new();
        public string Type { get; set; } = string.Empty;
    }
}
