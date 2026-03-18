// ✅ Fixed class definition
namespace NlpToSqlApi.Models
{
    public class EnglishLanguageResources
    {
        public Dictionary<string, string[]> OperationKeywords { get; } = new()
        {
            ["select"] = new[] { "show", "display", "list", "get", "find", "give", "see", "view" },
            ["count"] = new[] { "count", "number of", "how many", "total" },
            ["max"] = new[] { "maximum", "max", "highest", "largest", "most", "biggest" },
            ["min"] = new[] { "minimum", "min", "lowest", "smallest", "least" },
            ["avg"] = new[] { "average", "avg", "mean" },
            ["sum"] = new[] { "sum", "total", "add up" }
        };

        public Dictionary<string, string[]> ComparisonKeywords { get; } = new()
        {
            [">"] = new[] { "more than", "greater than", "above", "over", "higher than", "bigger than" },
            ["<"] = new[] { "less than", "below", "under", "lower than", "smaller than" },
            [">="] = new[] { "at least", "minimum of", "or more", "or higher" },
            ["<="] = new[] { "at most", "maximum of", "or less", "or lower" },
            ["="] = new[] { "equal to", "equals", "is", "exactly" },
            ["!="] = new[] { "not equal to", "not", "different from" }
        };
        public Dictionary<string, string[]> AggregationKeywords { get; } = new()
        {
            ["count"] = new[] { "count", "number of", "how many", "total" },
            ["max"] = new[] { "maximum", "max", "highest", "largest", "most", "biggest" },
            ["min"] = new[] { "minimum", "min", "lowest", "smallest", "least" },
            ["avg"] = new[] { "average", "avg", "mean" },
            ["sum"] = new[] { "sum", "total", "add up" }
        };


        public string[] JoinKeywords { get; } = new[] { "with", "and", "along with", "including", "together with" };

        public Dictionary<string, string> SchemaTerms { get; } = new()
        {
            ["employee"] = "employee",
            ["department"] = "department",
            ["salary"] = "salary",
            ["customer"] = "customer",
            ["order"] = "order",
            ["product"] = "product",
            ["price"] = "price",
            ["date"] = "date",
            ["name"] = "name",
            ["id"] = "id"
        };
    }
}