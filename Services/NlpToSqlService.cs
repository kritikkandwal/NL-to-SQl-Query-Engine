using NlpToSqlApi.Models;

namespace NlpToSqlApi.Services
{
    public class NlpToSqlService : IDisposable
    {
        private readonly DatabaseManager _databaseManager;
        private readonly SchemaAnalyzer _schemaAnalyzer;
        private readonly QueryProcessor _queryProcessor;
        private readonly SqlGenerator _sqlGenerator;

        public NlpToSqlService(string databasePath)
        {
            _databaseManager = new DatabaseManager(databasePath);   
            _schemaAnalyzer = new SchemaAnalyzer(_databaseManager);
            _queryProcessor = new QueryProcessor();
            _sqlGenerator = new SqlGenerator(_schemaAnalyzer);
        }

        public async Task<QueryResponse> ProcessQueryAsync(string naturalLanguageQuery, string language = "en") // ✅ Added language param

        {
            try
            {
                // Process the natural language query
                var processedQuery = _queryProcessor.ProcessQuery(naturalLanguageQuery, language); // ✅ Pass language


                // Generate SQL from processed query
                var sql = await _sqlGenerator.GenerateSqlAsync(processedQuery);
                
                if (string.IsNullOrEmpty(sql))
                {
                    return new QueryResponse
                    {
                        Success = false,
                        ErrorMessage = "Could not generate SQL query from the input. Please try rephrasing your question."
                    };
                }

                // Execute the SQL query
                var results = await _databaseManager.ExecuteQueryAsync(sql);
                
                // Prepare metadata
                var schema = await _schemaAnalyzer.AnalyzeSchemaAsync();
                var tablesUsed = processedQuery.Tables.Where(t => schema.Tables.ContainsKey(t)).ToList();
                var columnsUsed = processedQuery.Columns;

                return new QueryResponse
                {
                    Success = true,
                    GeneratedSql = sql,
                    Results = results,
                    RowCount = results.Count,
                    Metadata = new QueryMetadata
                    {
                        Operation = processedQuery.Operation,
                        TablesUsed = tablesUsed,
                        ColumnsUsed = columnsUsed,
                        HasJoins = processedQuery.JoinsNeeded,
                        HasAggregations = processedQuery.Aggregations.Any()
                    }
                };
            }
            catch (Exception ex)
            {
                return new QueryResponse
                {
                    Success = false,
                    ErrorMessage = $"Error processing query: {ex.Message}"
                };
            }
        }

        public async Task<Dictionary<string, List<string>>> GetDatabaseSchemaAsync()
        {
            return _schemaAnalyzer.GetSchemaSummary();
        }

        public async Task<bool> TestConnectionAsync()
        {
            return await _databaseManager.TestConnectionAsync();
        }

        public void Dispose()
        {
            _databaseManager?.Dispose();
        }
    }
}