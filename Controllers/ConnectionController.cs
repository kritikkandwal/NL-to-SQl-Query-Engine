using Microsoft.AspNetCore.Mvc;
using NlpToSqlApi.Models;
using NlpToSqlApi.Services;
using System.Text.Json;

namespace NlpToSqlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectionController : ControllerBase
    {
        private static readonly List<DatabaseConnection> _connections = new();
        private static readonly string _connectionsFile = "connections.json";

        static ConnectionController()
        {
            LoadConnections();
        }

        [HttpGet("list")]
        public ActionResult<List<DatabaseConnection>> GetConnections()
        {
            return Ok(_connections);
        }

        [HttpPost("add")]
        public async Task<ActionResult<DatabaseConnection>> AddConnection([FromBody] DatabaseConnectionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ConnectionString))
            {
                return BadRequest("Name and connection string are required");
            }

            // Test the connection first
            using var dbManager = new UniversalDatabaseManager(request.Type, request.ConnectionString);
            var testResult = await dbManager.TestConnectionAsync();
            
            if (!testResult.Success)
            {
                return BadRequest(new { 
                    message = "Connection test failed", 
                    error = testResult.ErrorDetails 
                });
            }

            var connection = new DatabaseConnection
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Type = request.Type,
                ConnectionString = request.ConnectionString,
                Description = request.Description,
                TableCount = testResult.Tables.Count,
                Tables = testResult.Tables,
                CreatedDate = DateTime.UtcNow
            };

            _connections.Add(connection);
            SaveConnections();

            return Ok(connection);
        }

        [HttpPost("{id}/test")]
        public async Task<ActionResult<ConnectionTestResult>> TestConnection(string id)
        {
            var connection = _connections.FirstOrDefault(c => c.Id == id);
            if (connection == null)
            {
                return NotFound("Connection not found");
            }

            using var dbManager = new UniversalDatabaseManager(connection.Type, connection.ConnectionString);
            var result = await dbManager.TestConnectionAsync();
            
            return Ok(result);
        }

        [HttpGet("{id}/schema")]
        public async Task<ActionResult> GetConnectionSchema(string id)
        {
            var connection = _connections.FirstOrDefault(c => c.Id == id);
            if (connection == null)
            {
                return NotFound("Connection not found");
            }

            try
            {
                using var dbManager = new UniversalDatabaseManager(connection.Type, connection.ConnectionString);
                var tables = await dbManager.GetTablesAsync();
                var schema = new Dictionary<string, object>();

                foreach (var tableName in tables)
                {
                    var columns = await dbManager.GetTableSchemaAsync(tableName);
                    var foreignKeys = await dbManager.GetForeignKeysAsync(tableName);
                    var sampleData = await dbManager.GetSampleDataAsync(tableName);

                    schema[tableName] = new
                    {
                        columns = columns,
                        foreignKeys = foreignKeys,
                        sampleData = sampleData,
                        rowCount = 0 // Would need separate query for exact count
                    };
                }

                return Ok(new
                {
                    success = true,
                    connectionId = id,
                    databaseType = connection.Type.ToString(),
                    schema = schema,
                    summary = new { }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Failed to retrieve schema",
                    error = ex.Message
                });
            }
        }

        [HttpPost("{id}/query")]
        public async Task<ActionResult> ExecuteQuery(string id, [FromBody] QueryRequest request)
        {
            var connection = _connections.FirstOrDefault(c => c.Id == id);
            if (connection == null)
            {
                return NotFound("Connection not found");
            }

            try
            {
                using var dbManager = new UniversalDatabaseManager(connection.Type, connection.ConnectionString);
                
                // Check if query is already SQL
                if (request.Query.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    var results = await dbManager.ExecuteQueryAsync(request.Query);
                    return Ok(new QueryResponse
                    {
                        Success = true,
                        Query = request.Query,
                        GeneratedSql = request.Query,
                        Results = results,
                        Message = "Query executed successfully",
                        DatabaseId = id
                    });
                }
                else
                {
                    // Process natural language query using simplified NLP
                    var nlpResult = await ProcessNaturalLanguageQuery(dbManager, request.Query, connection.Type);
                    nlpResult.DatabaseId = id;
                    return Ok(nlpResult);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new QueryResponse
                {
                    Success = false,
                    Query = request.Query,
                    Message = "Query execution failed",
                    Error = ex.Message,
                    DatabaseId = id
                });
            }
        }

        private async Task<QueryResponse> ProcessNaturalLanguageQuery(UniversalDatabaseManager dbManager, string query, DatabaseType dbType)
        {
            try
            {
                // Get schema information
                var tables = await dbManager.GetTablesAsync();
                if (!tables.Any())
                {
                    return new QueryResponse
                    {
                        Success = false,
                        Query = query,
                        Message = "No tables found in database"
                    };
                }

                // Simple NLP processing
                var lowerQuery = query.ToLower();
                var selectedTables = new List<string>();
                var whereConditions = new List<string>();
                var selectColumns = new List<string>();

                // Find relevant tables based on keywords and column analysis
                var allTableSchemas = new Dictionary<string, List<ColumnInfo>>();
                
                // First, get schemas for all tables to analyze columns
                foreach (var table in tables)
                {
                    try
                    {
                        allTableSchemas[table] = await dbManager.GetTableSchemaAsync(table);
                    }
                    catch
                    {
                        // Skip tables that can't be analyzed
                        continue;
                    }
                }

                // Detect multi-table queries that need JOINs
                bool needsJoin = false;
                if ((lowerQuery.Contains("city") && lowerQuery.Contains("country")) ||
                    (lowerQuery.Contains("customer") && lowerQuery.Contains("order")) ||
                    (lowerQuery.Contains("product") && lowerQuery.Contains("category")) ||
                    lowerQuery.Contains("with") || lowerQuery.Contains("and"))
                {
                    needsJoin = true;
                }

                // Find tables by table name keywords
                foreach (var table in tables)
                {
                    var tableLower = table.ToLower();
                    if (lowerQuery.Contains(tableLower) || 
                        lowerQuery.Contains(tableLower + "s") || // handle singular->plural
                        lowerQuery.Contains(tableLower + "ies") || // city->cities
                        (tableLower.EndsWith("y") && lowerQuery.Contains(tableLower.TrimEnd('y') + "ies")) || // city->cities
                        (tableLower.Contains("employee") && (lowerQuery.Contains("employee") || lowerQuery.Contains("staff") || lowerQuery.Contains("worker"))) ||
                        (tableLower.Contains("customer") && (lowerQuery.Contains("customer") || lowerQuery.Contains("client"))) ||
                        (tableLower.Contains("product") && (lowerQuery.Contains("product") || lowerQuery.Contains("item"))) ||
                        (tableLower.Contains("order") && (lowerQuery.Contains("order") || lowerQuery.Contains("purchase"))) ||
                        (tableLower.Contains("contact") && lowerQuery.Contains("contact")) ||
                        (tableLower.Contains("enquiry") && (lowerQuery.Contains("enquiry") || lowerQuery.Contains("inquiry"))) ||
                        (tableLower.Contains("user") && lowerQuery.Contains("user")) ||
                        (tableLower.Contains("signup") && lowerQuery.Contains("signup")))
                    {
                        selectedTables.Add(table);
                    }
                }

                // Special handling for city/country queries
                if (needsJoin && (lowerQuery.Contains("city") && lowerQuery.Contains("country")))
                {
                    selectedTables.Clear();
                    if (tables.Contains("City")) selectedTables.Add("City");
                    if (tables.Contains("Country")) selectedTables.Add("Country");
                }

                // Find tables by column name keywords (this is the key improvement)
                if (!selectedTables.Any())
                {
                    var candidateTables = new List<(string tableName, int relevanceScore)>();
                    
                    foreach (var tableSchema in allTableSchemas)
                    {
                        var relevanceScore = 0;
                        var hasRelevantColumn = false;
                        
                        foreach (var column in tableSchema.Value)
                        {
                            var colName = column.Name.ToLower();
                            if ((lowerQuery.Contains("email") && (colName.Contains("email") || colName.Contains("mail"))) ||
                                (lowerQuery.Contains("phone") && (colName.Contains("phone") || colName.Contains("mobile") || colName.Contains("contact"))) ||
                                (lowerQuery.Contains("name") && (colName.Contains("name") || colName.Contains("firstname") || colName.Contains("lastname"))) ||
                                (lowerQuery.Contains("address") && colName.Contains("address")) ||
                                (lowerQuery.Contains("price") && (colName.Contains("price") || colName.Contains("cost") || colName.Contains("amount"))) ||
                                (lowerQuery.Contains("date") && (colName.Contains("date") || colName.Contains("time") || colName.Contains("created"))) ||
                                (lowerQuery.Contains("status") && colName.Contains("status")) ||
                                (lowerQuery.Contains("id") && colName.Contains("id")))
                            {
                                hasRelevantColumn = true;
                                relevanceScore++;
                                
                                // Boost score for exact matches
                                if (lowerQuery.Contains("email") && colName == "email") relevanceScore += 3;
                                if (lowerQuery.Contains("email") && colName == "emailaddress") relevanceScore += 2;
                            }
                        }

                        if (hasRelevantColumn)
                        {
                            candidateTables.Add((tableSchema.Key, relevanceScore));
                        }
                    }
                    
                    // Select the most relevant table (or top 2 if they're very relevant)
                    var bestTables = candidateTables
                        .OrderByDescending(t => t.relevanceScore)
                        .Take(1) // Limit to 1 table to avoid JOIN complexity for now
                        .Select(t => t.tableName);
                        
                    selectedTables.AddRange(bestTables);
                }

                // If still no specific tables found, use the first non-system table
                if (!selectedTables.Any())
                {
                    var nonSystemTable = tables.FirstOrDefault(t => !t.StartsWith("__") && !t.ToLower().Contains("migration"));
                    if (nonSystemTable != null)
                    {
                        selectedTables.Add(nonSystemTable);
                    }
                    else
                    {
                        selectedTables.Add(tables.First());
                    }
                }

                // Use pre-loaded schemas for selected tables
                var tableSchemas = new Dictionary<string, List<ColumnInfo>>();
                foreach (var table in selectedTables)
                {
                    if (allTableSchemas.ContainsKey(table))
                    {
                        tableSchemas[table] = allTableSchemas[table];
                    }
                }

                // Build WHERE conditions based on common patterns
                if (lowerQuery.Contains("where") || lowerQuery.Contains("with") || lowerQuery.Contains("in"))
                {
                    // Look for conditions like "salary > 50000", "department = IT", etc.
                    var patterns = new[]
                    {
                        @"(\w+)\s*(>|<|>=|<=|=)\s*(\d+)",
                        @"(\w+)\s*(=|is|equals?)\s*['""]?(\w+)['""]?",
                        @"(\w+)\s+(more than|greater than|above)\s+(\d+)",
                        @"(\w+)\s+(less than|below|under)\s+(\d+)"
                    };

                    foreach (var pattern in patterns)
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(lowerQuery, pattern);
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            var columnName = match.Groups[1].Value;
                            var operation = match.Groups[2].Value;
                            var value = match.Groups[3].Value;

                            // Map natural language operators to SQL
                            if (operation.Contains("more than") || operation.Contains("greater than") || operation.Contains("above"))
                                operation = ">";
                            else if (operation.Contains("less than") || operation.Contains("below") || operation.Contains("under"))
                                operation = "<";

                            // Find matching column in schema
                            foreach (var table in tableSchemas)
                            {
                                var column = table.Value.FirstOrDefault(c => c.Name.ToLower().Contains(columnName));
                                if (column != null)
                                {
                                    var fullColumnName = selectedTables.Count > 1 ? $"{table.Key}.{column.Name}" : column.Name;
                                    if (IsNumericType(column.Type))
                                    {
                                        whereConditions.Add($"{QuoteIdentifier(fullColumnName, dbType)} {operation} {value}");
                                    }
                                    else
                                    {
                                        whereConditions.Add($"{QuoteIdentifier(fullColumnName, dbType)} {operation} '{value}'");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                // Build SELECT clause - use word boundaries to avoid matching "count" within other words like "countries"
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerQuery, @"\bcount\b") || 
                    lowerQuery.Contains("how many") || 
                    (System.Text.RegularExpressions.Regex.IsMatch(lowerQuery, @"\bnumber\b") && !lowerQuery.Contains("name") && !lowerQuery.Contains("phone")))
                {
                    selectColumns.Add("COUNT(*)");
                }
                else
                {
                    // Select columns based on query intent
                    foreach (var table in tableSchemas)
                    {
                        var relevantColumns = new List<ColumnInfo>();

                        // First, try to find columns matching the query intent
                        foreach (var column in table.Value)
                        {
                            var colName = column.Name.ToLower();
                            if ((lowerQuery.Contains("email") && (colName.Contains("email") || colName.Contains("mail"))) ||
                                (lowerQuery.Contains("phone") && (colName.Contains("phone") || colName.Contains("mobile"))) ||
                                (lowerQuery.Contains("name") && (colName.Contains("name") || colName.Contains("firstname") || colName.Contains("lastname"))) ||
                                (lowerQuery.Contains("address") && colName.Contains("address")) ||
                                (lowerQuery.Contains("price") && (colName.Contains("price") || colName.Contains("cost") || colName.Contains("amount"))) ||
                                (lowerQuery.Contains("date") && (colName.Contains("date") || colName.Contains("time") || colName.Contains("created"))) ||
                                (lowerQuery.Contains("status") && colName.Contains("status")) ||
                                (lowerQuery.Contains("id") && colName.Contains("id")) ||
                                (lowerQuery.Contains("city") && colName.Contains("city")) ||
                                (lowerQuery.Contains("country") && colName.Contains("country")))
                            {
                                relevantColumns.Add(column);
                            }
                        }

                        // If we found specific columns, use them
                        if (relevantColumns.Any())
                        {
                            foreach (var col in relevantColumns.Take(5)) // Limit to 5 relevant columns
                            {
                                if (selectedTables.Count > 1)
                                {
                                    // Use table alias for multi-table queries  
                                    var tableAlias = GetTableAlias(table.Key);
                                    selectColumns.Add($"{tableAlias}.{QuoteIdentifier(col.Name, dbType)}");
                                }
                                else
                                {
                                    selectColumns.Add(QuoteIdentifier(col.Name, dbType));
                                }
                            }
                        }
                        else
                        {
                            // Fall back to key columns
                            var keyColumns = table.Value.Where(c => 
                                c.PrimaryKey || 
                                c.Name.ToLower().Contains("name") || 
                                c.Name.ToLower().Contains("title") ||
                                c.Name.ToLower().Contains("description")).Take(3);

                            foreach (var col in keyColumns)
                            {
                                if (selectedTables.Count > 1)
                                {
                                    var tableAlias = GetTableAlias(table.Key);
                                    selectColumns.Add($"{tableAlias}.{QuoteIdentifier(col.Name, dbType)}");
                                }
                                else
                                {
                                    selectColumns.Add(QuoteIdentifier(col.Name, dbType));
                                }
                            }

                            // If no key columns found, add first few columns
                            if (!selectColumns.Any())
                            {
                                var firstColumns = table.Value.Take(3);
                                foreach (var col in firstColumns)
                                {
                                    if (selectedTables.Count > 1)
                                    {
                                        var tableAlias = GetTableAlias(table.Key);
                                        selectColumns.Add($"{tableAlias}.{QuoteIdentifier(col.Name, dbType)}");
                                    }
                                    else
                                    {
                                        selectColumns.Add(QuoteIdentifier(col.Name, dbType));
                                    }
                                }
                            }
                        }
                    }
                }

                // Build SQL query
                var sql = $"SELECT {(selectColumns.Any() ? string.Join(", ", selectColumns) : "*")}";
                
                if (selectedTables.Count == 1)
                {
                    sql += $"\nFROM {QuoteIdentifier(selectedTables[0], dbType)}";
                }
                else
                {
                    // JOIN logic with table aliases
                    var firstTable = selectedTables[0];
                    var firstAlias = GetTableAlias(firstTable);
                    sql += $"\nFROM {QuoteIdentifier(firstTable, dbType)} {firstAlias}";
                    
                    for (int i = 1; i < selectedTables.Count; i++)
                    {
                        var joinTable = selectedTables[i];
                        var joinAlias = GetTableAlias(joinTable);
                        
                        var fks = await dbManager.GetForeignKeysAsync(firstTable);
                        var matchingFk = fks.FirstOrDefault(fk => fk.ReferencedTable == joinTable);
                        
                        if (matchingFk != null)
                        {
                            sql += $"\nJOIN {QuoteIdentifier(joinTable, dbType)} {joinAlias} ON {firstAlias}.{QuoteIdentifier(matchingFk.Column, dbType)} = {joinAlias}.{QuoteIdentifier(matchingFk.ReferencedColumn, dbType)}";
                        }
                        else
                        {
                            // Try reverse FK
                            var reverseFks = await dbManager.GetForeignKeysAsync(joinTable);
                            var reverseMatchingFk = reverseFks.FirstOrDefault(fk => fk.ReferencedTable == firstTable);
                            if (reverseMatchingFk != null)
                            {
                                sql += $"\nJOIN {QuoteIdentifier(joinTable, dbType)} {joinAlias} ON {joinAlias}.{QuoteIdentifier(reverseMatchingFk.Column, dbType)} = {firstAlias}.{QuoteIdentifier(reverseMatchingFk.ReferencedColumn, dbType)}";
                            }
                            else
                            {
                                // Smart JOIN detection for databases without formal FKs
                                string joinCondition = null;
                                
                                // Look for common patterns: City->Country via CountryId
                                if ((firstTable.ToLower() == "city" && joinTable.ToLower() == "country") ||
                                    (firstTable.ToLower() == "country" && joinTable.ToLower() == "city"))
                                {
                                    joinCondition = $"{firstAlias}.{QuoteIdentifier("CountryId", dbType)} = {joinAlias}.{QuoteIdentifier("CountryId", dbType)}";
                                }
                                // Look for ID column patterns
                                else
                                {
                                    var firstTableSchema = allTableSchemas[firstTable];
                                    var joinTableSchema = allTableSchemas[joinTable];
                                    
                                    // Find matching column names that could be foreign keys
                                    var possibleJoin = firstTableSchema
                                        .Where(c => c.Name.ToLower().EndsWith("id"))
                                        .FirstOrDefault(c => joinTableSchema.Any(jc => jc.Name.ToLower() == c.Name.ToLower()));
                                    
                                    if (possibleJoin != null)
                                    {
                                        joinCondition = $"{firstAlias}.{QuoteIdentifier(possibleJoin.Name, dbType)} = {joinAlias}.{QuoteIdentifier(possibleJoin.Name, dbType)}";
                                    }
                                }
                                
                                if (joinCondition != null)
                                {
                                    sql += $"\nJOIN {QuoteIdentifier(joinTable, dbType)} {joinAlias} ON {joinCondition}";
                                }
                            }
                        }
                    }
                }

                if (whereConditions.Any())
                {
                    sql += $"\nWHERE {string.Join(" AND ", whereConditions)}";
                }

                // Add LIMIT for safety (database-specific syntax)
                if (dbType == DatabaseType.MSSQL)
                {
                    // For MSSQL, we need to modify the SELECT clause to include TOP
                    if (sql.StartsWith("SELECT "))
                    {
                        sql = sql.Replace("SELECT ", "SELECT TOP 100 ");
                    }
                }
                else
                {
                    sql += "\nLIMIT 100";
                }

                // Execute the generated SQL
                var results = await dbManager.ExecuteQueryAsync(sql);

                return new QueryResponse
                {
                    Success = true,
                    Query = query,
                    GeneratedSql = sql,
                    Results = results,
                    Message = "Natural language query processed successfully",
                    RowCount = results.Count
                };
            }
            catch (Exception ex)
            {
                return new QueryResponse
                {
                    Success = false,
                    Query = query,
                    Message = "Failed to process natural language query",
                    Error = ex.Message
                };
            }
        }

        private bool IsNumericType(string type)
        {
            var upperType = type.ToUpper();
            return upperType.Contains("INT") || upperType.Contains("DECIMAL") || 
                   upperType.Contains("FLOAT") || upperType.Contains("DOUBLE") || 
                   upperType.Contains("NUMERIC") || upperType.Contains("REAL") || 
                   upperType.Contains("MONEY");
        }

        private string QuoteIdentifier(string identifier, DatabaseType dbType)
        {
            return dbType switch
            {
                DatabaseType.MySQL => $"`{identifier}`",
                DatabaseType.PostgreSQL => $"\"{identifier}\"",
                DatabaseType.SQLite => $"[{identifier}]",
                DatabaseType.MSSQL => $"[{identifier}]",
                _ => identifier
            };
        }

        [HttpDelete("{id}")]
        public ActionResult DeleteConnection(string id)
        {
            var connection = _connections.FirstOrDefault(c => c.Id == id);
            if (connection == null)
            {
                return NotFound("Connection not found");
            }

            _connections.Remove(connection);
            SaveConnections();

            return Ok(new { message = "Connection deleted successfully" });
        }

        [HttpPut("{id}/default")]
        public ActionResult SetDefaultConnection(string id)
        {
            var connection = _connections.FirstOrDefault(c => c.Id == id);
            if (connection == null)
            {
                return NotFound("Connection not found");
            }

            // Remove default from all connections
            foreach (var conn in _connections)
            {
                conn.IsDefault = false;
            }

            // Set new default
            connection.IsDefault = true;
            SaveConnections();

            return Ok(new { message = "Default connection updated" });
        }

        private static void SaveConnections()
        {
            try
            {
                var json = JsonSerializer.Serialize(_connections, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                System.IO.File.WriteAllText(_connectionsFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save connections: {ex.Message}");
            }
        }

        private static void LoadConnections()
        {
            try
            {
                if (System.IO.File.Exists(_connectionsFile))
                {
                    var json = System.IO.File.ReadAllText(_connectionsFile);
                    var connections = JsonSerializer.Deserialize<List<DatabaseConnection>>(json);
                    if (connections != null)
                    {
                        _connections.AddRange(connections);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load connections: {ex.Message}");
            }
        }

        private static string GetTableAlias(string tableName)
        {
            // Generate consistent table aliases
            switch (tableName.ToLower())
            {
                case "city": return "c";
                case "country": return "co";
                case "customer": return "cu";
                case "product": return "p";
                case "order": return "o";
                case "orderitem": return "oi";
                default: return tableName.Substring(0, Math.Min(2, tableName.Length)).ToLower();
            }
        }
    }
}