using NlpToSqlApi.Models;

namespace NlpToSqlApi.Services
{
    public class SqlGenerator
    {
        private readonly SchemaAnalyzer _schemaAnalyzer;

        public SqlGenerator(SchemaAnalyzer schemaAnalyzer)
        {
            _schemaAnalyzer = schemaAnalyzer;
        }

        public async Task<string?> GenerateSqlAsync(ProcessedQuery processedQuery)
        {
            try
            {
                var schema = await _schemaAnalyzer.AnalyzeSchemaAsync();
                
                // Resolve tables to use
                var tables = ResolveTables(processedQuery, schema);
                if (!tables.Any()) return null;

                // Resolve columns to select
                var columns = ResolveColumns(processedQuery, tables, schema);

                // Build SQL components
                var selectClause = processedQuery.Aggregations.Any() 
                    ? BuildAggregationSelect(processedQuery, tables, schema)
                    : BuildSelectClause(columns, tables, schema);

                var fromClause = BuildFromClause(tables, schema);
                var whereClause = BuildWhereClause(processedQuery, tables, schema);
                var groupByClause = BuildGroupByClause(processedQuery, tables, schema);
                var orderByClause = BuildOrderByClause(processedQuery, tables, schema);

                // Combine all parts
                var sqlParts = new List<string> { selectClause, fromClause };
                
                if (!string.IsNullOrEmpty(whereClause))
                    sqlParts.Add(whereClause);
                
                if (!string.IsNullOrEmpty(groupByClause))
                    sqlParts.Add(groupByClause);
                
                if (!string.IsNullOrEmpty(orderByClause))
                    sqlParts.Add(orderByClause);

                return string.Join(" ", sqlParts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating SQL: {ex.Message}");
                return null;
            }
        }

        private List<string> ResolveTables(ProcessedQuery processedQuery, DatabaseSchema schema)
        {
            var tables = new List<string>();

            // Map hints to actual table names
            foreach (var hint in processedQuery.Tables)
            {
                var matchingTables = _schemaAnalyzer.FindTablesByKeyword(hint);
                tables.AddRange(matchingTables);
            }

            // If no tables found from hints, try to infer from conditions
            if (!tables.Any())
            {
                foreach (var condition in processedQuery.Conditions)
                {
                    var columnMatches = _schemaAnalyzer.FindColumnsByKeyword(condition.Column);
                    foreach (var (tableName, _) in columnMatches)
                    {
                        if (!tables.Contains(tableName))
                            tables.Add(tableName);
                    }
                }
            }

            return tables.Distinct().ToList();
        }

        private List<string> ResolveColumns(ProcessedQuery processedQuery, List<string> tables, DatabaseSchema schema)
        {
            var columns = new List<string>();

            // If specific columns are mentioned, try to find them
            foreach (var hint in processedQuery.Columns)
            {
                foreach (var table in tables)
                {
                    if (schema.Tables.ContainsKey(table))
                    {
                        var tableInfo = schema.Tables[table];
                        foreach (var col in tableInfo.ColumnNames)
                        {
                            if (col.ToLower().Contains(hint.ToLower()))
                            {
                                columns.Add($"{table}.{col}");
                            }
                        }
                    }
                }
            }

            // If no specific columns found, select all from main table
            if (!columns.Any() && tables.Any())
            {
                var mainTable = tables[0];
                if (schema.Tables.ContainsKey(mainTable))
                {
                    var tableInfo = schema.Tables[mainTable];
                    foreach (var col in tableInfo.ColumnNames)
                    {
                        columns.Add($"{mainTable}.{col}");
                    }
                }
            }

            return columns.Any() ? columns : new List<string> { "*" };
        }

        private string BuildSelectClause(List<string> columns, List<string> tables, DatabaseSchema schema)
        {
            if (columns.Contains("*") && tables.Count > 1)
            {
                // For multiple tables, specify table prefixes
                var allColumns = new List<string>();
                foreach (var table in tables)
                {
                    if (schema.Tables.ContainsKey(table))
                    {
                        var tableInfo = schema.Tables[table];
                        foreach (var col in tableInfo.ColumnNames)
                        {
                            allColumns.Add($"{table}.{col}");
                        }
                    }
                }
                return $"SELECT {string.Join(", ", allColumns)}";
            }
            
            return $"SELECT {string.Join(", ", columns)}";
        }

        private string BuildAggregationSelect(ProcessedQuery processedQuery, List<string> tables, DatabaseSchema schema)
        {
            var selectParts = new List<string>();

            foreach (var agg in processedQuery.Aggregations)
            {
                var function = agg.Function;

                if (function == "COUNT")
                {
                    var countLabel = processedQuery.OriginalQuery.ToLower().Contains("employee") 
                        ? "employee_count" : "count";
                    selectParts.Add($"COUNT(*) as {countLabel}");
                }
                else if (new[] { "MAX", "MIN", "AVG", "SUM" }.Contains(function))
                {
                    // Find numeric columns
                    var numericCols = new List<string>();
                    foreach (var table in tables)
                    {
                        if (schema.Tables.ContainsKey(table))
                        {
                            var tableInfo = schema.Tables[table];
                            foreach (var col in tableInfo.Columns)
                            {
                                if (_schemaAnalyzer.IsNumericColumn(table, col.Name))
                                {
                                    if (col.Name.ToLower().Contains("salary") || col.Name.ToLower().Contains("amount"))
                                    {
                                        numericCols.Add($"{table}.{col.Name}");
                                    }
                                }
                            }
                        }
                    }

                    foreach (var col in numericCols)
                    {
                        var columnName = col.Split('.').Last();
                        selectParts.Add($"{function}({col}) as {function.ToLower()}_{columnName}");
                    }
                }
            }

            // Add grouping columns if needed
            if (processedQuery.OriginalQuery.ToLower().Contains("department"))
            {
                foreach (var table in tables)
                {
                    if (schema.Tables.ContainsKey(table))
                    {
                        var tableInfo = schema.Tables[table];
                        foreach (var col in tableInfo.ColumnNames)
                        {
                            if (col.ToLower().Contains("department"))
                            {
                                selectParts.Insert(0, $"{table}.{col}");
                            }
                        }
                    }
                }
            }

            return selectParts.Any() ? $"SELECT {string.Join(", ", selectParts)}" : "SELECT COUNT(*)";
        }

        private string BuildFromClause(List<string> tables, DatabaseSchema schema)
        {
            if (tables.Count == 1)
            {
                return $"FROM {tables[0]}";
            }

            // For multiple tables, build JOINs
            var mainTable = tables[0];
            var fromClause = $"FROM {mainTable}";

            for (int i = 1; i < tables.Count; i++)
            {
                var table = tables[i];
                var joinConditions = _schemaAnalyzer.SuggestJoinConditions(new List<string> { mainTable, table });
                
                if (joinConditions.Any())
                {
                    fromClause += $" JOIN {table} ON {joinConditions[0]}";
                }
                else
                {
                    // Try to find a relationship through common columns or naming conventions
                    if (schema.Tables.ContainsKey(mainTable) && schema.Tables.ContainsKey(table))
                    {
                        var mainTableInfo = schema.Tables[mainTable];
                        var tableInfo = schema.Tables[table];

                        // Look for id relationships
                        var idColumn = tableInfo.ColumnNames.FirstOrDefault(c => c.EndsWith("_id") || c == "id");
                        if (idColumn != null)
                        {
                            var refColumn = idColumn.Replace("_id", "");
                            if (refColumn == mainTable || mainTableInfo.ColumnNames.Any(c => c == "id"))
                            {
                                fromClause += $" JOIN {table} ON {mainTable}.id = {table}.{idColumn}";
                            }
                        }
                    }
                }
            }

            return fromClause;
        }

        private string BuildWhereClause(ProcessedQuery processedQuery, List<string> tables, DatabaseSchema schema)
        {
            if (!processedQuery.Conditions.Any()) return string.Empty;

            var whereParts = new List<string>();

            foreach (var condition in processedQuery.Conditions)
            {
                var columnName = condition.Column;
                var op = condition.Operator;
                var value = condition.Value;

                // Find the actual column in the tables
                string? fullColumn = null;
                foreach (var table in tables)
                {
                    if (schema.Tables.ContainsKey(table))
                    {
                        var tableInfo = schema.Tables[table];
                        foreach (var col in tableInfo.ColumnNames)
                        {
                            if (columnName.ToLower().Contains(col.ToLower()) || col.ToLower().Contains(columnName.ToLower()))
                            {
                                fullColumn = $"{table}.{col}";
                                break;
                            }
                        }
                        if (fullColumn != null) break;
                    }
                }

                if (fullColumn != null)
                {
                    if (condition.Type == "string")
                    {
                        whereParts.Add($"{fullColumn} {op} '{value}'");
                    }
                    else
                    {
                        whereParts.Add($"{fullColumn} {op} {value}");
                    }
                }
            }

            return whereParts.Any() ? $"WHERE {string.Join(" AND ", whereParts)}" : string.Empty;
        }

        private string BuildGroupByClause(ProcessedQuery processedQuery, List<string> tables, DatabaseSchema schema)
        {
            if (!processedQuery.Aggregations.Any()) return string.Empty;

            var groupByCols = new List<string>();

            // If we're grouping by department or similar
            if (processedQuery.OriginalQuery.ToLower().Contains("department"))
            {
                foreach (var table in tables)
                {
                    if (schema.Tables.ContainsKey(table))
                    {
                        var tableInfo = schema.Tables[table];
                        foreach (var col in tableInfo.ColumnNames)
                        {
                            if (col.ToLower().Contains("department"))
                            {
                                groupByCols.Add($"{table}.{col}");
                            }
                        }
                    }
                }
            }

            return groupByCols.Any() ? $"GROUP BY {string.Join(", ", groupByCols)}" : string.Empty;
        }

        private string BuildOrderByClause(ProcessedQuery processedQuery, List<string> tables, DatabaseSchema schema)
        {
            // Add ordering for aggregations
            foreach (var agg in processedQuery.Aggregations)
            {
                if (new[] { "MAX", "MIN" }.Contains(agg.Function))
                {
                    if (processedQuery.OriginalQuery.ToLower().Contains("salary"))
                    {
                        return agg.Function == "MAX" ? "ORDER BY salary DESC" : "ORDER BY salary ASC";
                    }
                }
            }

            return string.Empty;
        }
    }
}