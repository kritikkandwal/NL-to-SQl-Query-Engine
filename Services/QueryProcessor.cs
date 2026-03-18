using System.Globalization;
using System.Text.RegularExpressions;
using NlpToSqlApi.Models; // ✅ Updated to reflect correct namespace


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

        // ✅ Updated
        public ProcessedQuery ProcessQuery(string query, string language = "en")
        {
            // ✅ Explicit resource selection
            object resources;
            if (language == "it")
                resources = new ItalianLanguageResources();
            else
                resources = new EnglishLanguageResources();

            var lowerQuery = query.ToLower(CultureInfo.CurrentCulture);

            // ✅ Apply schema term translations
            if (language == "it")
            {
                var italianResources = (ItalianLanguageResources)resources;
                foreach (var term in italianResources.SchemaTerms)
                {
                    if (lowerQuery.Contains(term.Value))
                        lowerQuery = lowerQuery.Replace(term.Value, term.Key);
                }
            }

            return new ProcessedQuery
            {
                OriginalQuery = query,
                Operation = DetectOperation(lowerQuery, resources, language),
                Tables = ExtractTableHints(lowerQuery),
                Columns = ExtractColumnHints(lowerQuery),
                Conditions = ExtractConditions(lowerQuery, resources, language),
                Aggregations = DetectAggregations(lowerQuery, resources, language),
                JoinsNeeded = DetectJoinsNeeded(lowerQuery, resources, language)
            };
        }


        // ✅ Updated
        private string DetectOperation(string query, object resources, string language)
        {
            Dictionary<string, string[]> operationKeywords;

            if (language == "it")
                operationKeywords = ((ItalianLanguageResources)resources).OperationKeywords;
            else
                operationKeywords = ((EnglishLanguageResources)resources).OperationKeywords;

            foreach (var operation in operationKeywords)
            {
                if (operation.Value.Any(keyword => query.Contains(keyword)))
                {
                    return operation.Key;
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
                "order", "orders", "product", "products", "company", "companies","bookings","vehicles","employees",
                "departments","users","customers","orders","products","companies"
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
                "created", "updated", "status", "price","BookingId","Source","", "cost",
                "price","BookingId","Source","TotalFare", "cost","bookingid","source","totalfare"
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

        // ✅ Updated
        private List<QueryCondition> ExtractConditions(string query, object resources, string language)
        {
            var conditions = new List<QueryCondition>();

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
                        Operator = TextToOperator(operatorText, resources, language),
                        Value = int.Parse(value),
                        Type = "numeric"
                    });
                }
            }

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


        // ✅ Updated
        private List<AggregationFunction> DetectAggregations(string query, object resources, string language)
        {
            var aggregations = new List<AggregationFunction>();

            Dictionary<string, string[]> aggregationKeywords;
            if (language == "it")
                aggregationKeywords = ((ItalianLanguageResources)resources).AggregationKeywords;
            else
                aggregationKeywords = ((EnglishLanguageResources)resources).AggregationKeywords;

            foreach (var (function, keywords) in aggregationKeywords)
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

            return aggregations;
        }



        // ✅ Updated
        private bool DetectJoinsNeeded(string query, object resources, string language)
        {
            var tableHints = ExtractTableHints(query);
            if (tableHints.Count > 1) return true;

            string[] joinKeywords;
            if (language == "it")
                joinKeywords = ((ItalianLanguageResources)resources).JoinKeywords;
            else
                joinKeywords = ((EnglishLanguageResources)resources).JoinKeywords;

            foreach (var keyword in joinKeywords)
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


        // ✅ Updated
        private string TextToOperator(string text, object resources, string language) // ✅ Updated
        {
            Dictionary<string, string[]> comparisonKeywords;

            if (language == "it")
                comparisonKeywords = ((ItalianLanguageResources)resources).ComparisonKeywords;
            else
                comparisonKeywords = ((EnglishLanguageResources)resources).ComparisonKeywords;

            foreach (var comparison in comparisonKeywords)
            {
                if (comparison.Value.Contains(text.ToLower()))
                {
                    return comparison.Key;
                }
            }

            return "=";
        }
    }
}