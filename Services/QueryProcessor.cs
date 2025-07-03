using System.Text.RegularExpressions;

namespace NlpToSqlApi.Services
{
    public class QueryProcessor
    {
        private readonly Dictionary<string, string[]> _operationKeywords;
        private readonly Dictionary<string, string[]> _comparisonKeywords;
        private readonly string[] _joinKeywords;

        public QueryProcessor()
        {
            _operationKeywords = new Dictionary<string, string[]>
            {
                ["select"] = new[] { "show", "display", "list", "get", "find", "give", "see", "view" },
                ["count"] = new[] { "count", "number of", "how many", "total" },
                ["max"] = new[] { "maximum", "max", "highest", "largest", "most", "biggest" },
                ["min"] = new[] { "minimum", "min", "lowest", "smallest", "least" },
                ["avg"] = new[] { "average", "avg", "mean" },
                ["sum"] = new[] { "sum", "total", "add up" }
            };

            _comparisonKeywords = new Dictionary<string, string[]>
            {
                [">"] = new[] { "more than", "greater than", "above", "over", "higher than", "bigger than" },
                ["<"] = new[] { "less than", "below", "under", "lower than", "smaller than" },
                [">="] = new[] { "at least", "minimum of", "or more", "or higher" },
                ["<="] = new[] { "at most", "maximum of", "or less", "or lower" },
                ["="] = new[] { "equal to", "equals", "is", "exactly" },
                ["!="] = new[] { "not equal to", "not", "different from" }
            };

            _joinKeywords = new[] { "with", "and", "along with", "including", "together with" };
        }

        public ProcessedQuery ProcessQuery(string query)
        {
            var lowerQuery = query.ToLower().Trim();

            return new ProcessedQuery
            {
                OriginalQuery = query,
                Operation = DetectOperation(lowerQuery),
                Tables = ExtractTableHints(lowerQuery),
                Columns = ExtractColumnHints(lowerQuery),
                Conditions = ExtractConditions(lowerQuery),
                Aggregations = DetectAggregations(lowerQuery),
                JoinsNeeded = DetectJoinsNeeded(lowerQuery)
            };
        }

        private string DetectOperation(string query)
        {
            foreach (var (operation, keywords) in _operationKeywords)
            {
                if (keywords.Any(keyword => query.Contains(keyword)))
                {
                    return operation;
                }
            }
            return "select";
        }

        private List<string> ExtractTableHints(string query)
        {
            var tableHints = new List<string>();
            var commonTables = new[] 
            { 
                "employee", "employees", "department", "departments",
                "salary", "salaries", "user", "users", "customer", "customers",
                "order", "orders", "product", "products", "company", "companies"
            };

            foreach (var table in commonTables)
            {
                if (query.Contains(table))
                {
                    tableHints.Add(table);
                }
            }

            return tableHints.Distinct().ToList();
        }

        private List<string> ExtractColumnHints(string query)
        {
            var columnHints = new List<string>();
            var commonColumns = new[]
            {
                "name", "id", "salary", "age", "department", "email",
                "phone", "address", "title", "position", "manager",
                "created", "updated", "status", "price", "cost"
            };

            foreach (var column in commonColumns)
            {
                if (query.Contains(column))
                {
                    columnHints.Add(column);
                }
            }

            return columnHints.Distinct().ToList();
        }

        private List<QueryCondition> ExtractConditions(string query)
        {
            var conditions = new List<QueryCondition>();

            // Extract numeric conditions
            var numericPatterns = new[]
            {
                @"(salary|age|price|cost|amount)\s*(more than|greater than|>)\s*(\d+)",
                @"(salary|age|price|cost|amount)\s*(less than|<)\s*(\d+)",
                @"(salary|age|price|cost|amount)\s*(equal to|equals|=)\s*(\d+)",
                @"(salary|age|price|cost|amount)\s*(at least|>=)\s*(\d+)",
                @"(salary|age|price|cost|amount)\s*(at most|<=)\s*(\d+)"
            };

            foreach (var pattern in numericPatterns)
            {
                var matches = Regex.Matches(query, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var column = match.Groups[1].Value;
                    var operatorText = match.Groups[2].Value;
                    var value = match.Groups[3].Value;

                    conditions.Add(new QueryCondition
                    {
                        Column = column,
                        Operator = TextToOperator(operatorText),
                        Value = int.Parse(value),
                        Type = "numeric"
                    });
                }
            }

            // Extract string conditions
            var stringPatterns = new[]
            {
                @"(department|name|title|position)\s+(is|equals?)\s+([""']?)(\w+)\3",
                @"in\s+(the\s+)?(\w+)\s+(department|team|group)",
                @"(\w+)\s+(department|team|group)"
            };

            foreach (var pattern in stringPatterns)
            {
                var matches = Regex.Matches(query, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (pattern.Contains("department"))
                    {
                        string value;
                        if (match.Groups.Count >= 3 && !string.IsNullOrEmpty(match.Groups[2].Value))
                        {
                            value = match.Groups[2].Value;
                        }
                        else if (match.Groups.Count >= 2)
                        {
                            value = match.Groups[1].Value;
                        }
                        else
                        {
                            continue;
                        }

                        conditions.Add(new QueryCondition
                        {
                            Column = "department",
                            Operator = "=",
                            Value = value,
                            Type = "string"
                        });
                    }
                    else if (match.Groups.Count >= 4)
                    {
                        conditions.Add(new QueryCondition
                        {
                            Column = match.Groups[1].Value,
                            Operator = "=",
                            Value = match.Groups[4].Value,
                            Type = "string"
                        });
                    }
                }
            }

            return conditions;
        }

        private List<AggregationFunction> DetectAggregations(string query)
        {
            var aggregations = new List<AggregationFunction>();

            foreach (var (function, keywords) in _operationKeywords)
            {
                if (new[] { "count", "max", "min", "avg", "sum" }.Contains(function))
                {
                    foreach (var keyword in keywords)
                    {
                        if (query.Contains(keyword))
                        {
                            aggregations.Add(new AggregationFunction
                            {
                                Function = function.ToUpper(),
                                Keyword = keyword
                            });
                            break;
                        }
                    }
                }
            }

            return aggregations;
        }

        private bool DetectJoinsNeeded(string query)
        {
            var tableHints = ExtractTableHints(query);
            
            if (tableHints.Count > 1) return true;

            foreach (var keyword in _joinKeywords)
            {
                if (query.Contains(keyword)) return true;
            }

            var relationshipPatterns = new[]
            {
                @"(\w+)\s+(and|with)\s+their\s+(\w+)",
                @"(\w+)\s+in\s+(\w+)",
                @"(\w+)\s+from\s+(\w+)"
            };

            return relationshipPatterns.Any(pattern => Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase));
        }

        private string TextToOperator(string text)
        {
            foreach (var (op, keywords) in _comparisonKeywords)
            {
                if (keywords.Contains(text.ToLower()))
                {
                    return op;
                }
            }
            return "=";
        }
    }

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

    public class QueryCondition
    {
        public string Column { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public object Value { get; set; } = new();
        public string Type { get; set; } = string.Empty;
    }

    public class AggregationFunction
    {
        public string Function { get; set; } = string.Empty;
        public string Keyword { get; set; } = string.Empty;
    }
}