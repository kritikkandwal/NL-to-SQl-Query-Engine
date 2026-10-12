using NlpToSqlApi.Models;

namespace NlpToSqlApi.Services
{
    public interface INaturalLanguageQueryService
    {
        Task<QueryResponse> ProcessAsync(UniversalDatabaseManager dbManager, DatabaseType dbType, string question);
    }

    /// <summary>
    /// Reads the schema of a connected database, builds SQL for an English question and runs it.
    /// </summary>
    public class NaturalLanguageQueryService : INaturalLanguageQueryService
    {
        private readonly INaturalLanguageSqlBuilder _builder;

        public NaturalLanguageQueryService(INaturalLanguageSqlBuilder builder)
        {
            _builder = builder;
        }

        public async Task<QueryResponse> ProcessAsync(UniversalDatabaseManager dbManager, DatabaseType dbType, string question)
        {
            var schema = await LoadSchemaAsync(dbManager, dbType);
            var built = _builder.Build(question, schema, dbType);

            if (!built.Success)
            {
                return new QueryResponse
                {
                    Success = false,
                    Query = question,
                    Message = "Could not understand the question",
                    Error = built.Error
                };
            }

            var results = await dbManager.ExecuteQueryAsync(built.Sql!);
            return new QueryResponse
            {
                Success = true,
                Query = question,
                GeneratedSql = built.Sql,
                Results = results,
                Message = "Natural language query processed successfully",
                RowCount = results.Count,
                Metadata = new QueryMetadata
                {
                    Operation = built.Operation,
                    TablesUsed = built.TablesUsed,
                    ColumnsUsed = built.ColumnsUsed,
                    HasJoins = built.HasJoins,
                    HasAggregations = built.HasAggregations
                }
            };
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime At, List<string> Values)> ValueCache = new();
        private static readonly TimeSpan ValueCacheLifetime = TimeSpan.FromMinutes(10);
        private const int MaxDistinctValues = 200;

        /// <summary>Distinct values of a short text column, or an empty list when the column holds free text.</summary>
        private static async Task<List<string>> LoadValuesAsync(UniversalDatabaseManager dbManager, DatabaseType dbType, string table, ColumnInfo column)
        {
            var key = $"{dbManager.CacheKey}|{table}|{column.Name}";
            if (ValueCache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < ValueCacheLifetime)
                return hit.Values;

            string Q(string id) => dbType switch
            {
                DatabaseType.MySQL => $"`{id}`",
                DatabaseType.PostgreSQL => $"\"{id}\"",
                _ => $"[{id}]"
            };

            var values = new List<string>();
            try
            {
                var sql = dbType switch
                {
                    DatabaseType.MSSQL => $"SELECT DISTINCT TOP {MaxDistinctValues + 1} {Q(column.Name)} AS V FROM {Q(table)} WHERE {Q(column.Name)} IS NOT NULL",
                    _ => $"SELECT DISTINCT {Q(column.Name)} AS V FROM {Q(table)} WHERE {Q(column.Name)} IS NOT NULL LIMIT {MaxDistinctValues + 1}"
                };
                var rows = await dbManager.ExecuteQueryAsync(sql);
                values = rows.Select(r => r["V"]?.ToString()?.Trim() ?? "").Where(v => v.Length > 0).ToList();
                // too many distinct values or long values: free text, not a category-like column
                if (values.Count > MaxDistinctValues || values.Any(v => v.Length > 60) || values.Any(v => v.Contains('@')))
                    values = new List<string>();
            }
            catch
            {
                values = new List<string>();   // value matching is optional; never fail the question because of it
            }

            ValueCache[key] = (DateTime.UtcNow, values);
            return values;
        }

        private static bool IsTextType(string type)
        {
            type = type.ToLowerInvariant();
            return type.Contains("char") || type.Contains("text") || type.Contains("string");
        }

        private static async Task<DatabaseSchema> LoadSchemaAsync(UniversalDatabaseManager dbManager, DatabaseType dbType)
        {
            var schema = new DatabaseSchema();
            foreach (var table in await dbManager.GetTablesAsync())
            {
                if (table.StartsWith("__", StringComparison.Ordinal) || table.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase))
                    continue;

                var columns = await dbManager.GetTableSchemaAsync(table);
                schema.Tables[table] = new TableInfo
                {
                    Name = table,
                    Columns = columns,
                    ColumnNames = columns.Select(c => c.Name).ToList(),
                    PrimaryKeys = columns.Where(c => c.PrimaryKey).Select(c => c.Name).ToList()
                };
                schema.Relationships[table] = await dbManager.GetForeignKeysAsync(table);

                foreach (var column in columns.Where(c => !c.PrimaryKey && IsTextType(c.Type)))
                {
                    var values = await LoadValuesAsync(dbManager, dbType, table, column);
                    if (values.Count > 0) schema.Tables[table].ColumnValues[column.Name] = values;
                }
            }
            return schema;
        }
    }
}
