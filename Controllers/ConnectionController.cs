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
        private readonly INaturalLanguageQueryService _nlQueryService;

        public ConnectionController(INaturalLanguageQueryService nlQueryService)
        {
            _nlQueryService = nlQueryService;
        }

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
                var trimmed = request.Query.Trim();
                if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|CREATE|EXEC|EXECUTE|MERGE|GRANT|REVOKE)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return BadRequest(new QueryResponse
                    {
                        Success = false,
                        Query = request.Query,
                        Message = "Only read-only queries are allowed",
                        Error = "Only SELECT queries (or plain English questions) can be run here; data-changing statements are blocked.",
                        DatabaseId = id
                    });
                }

                // Check if query is already SQL (SELECT or WITH ... SELECT), a single statement only
                if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(SELECT|WITH)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (trimmed.TrimEnd(';').Contains(';'))
                    {
                        return BadRequest(new QueryResponse
                        {
                            Success = false,
                            Query = request.Query,
                            Message = "Only one SQL statement is allowed",
                            Error = "Remove the extra statements after the semicolon.",
                            DatabaseId = id
                        });
                    }
                    request.Query = trimmed.TrimEnd(';');
                    var results = await dbManager.ExecuteQueryAsync(request.Query);
                    return Ok(new QueryResponse
                    {
                        Success = true,
                        Query = request.Query,
                        GeneratedSql = request.Query,
                        Results = results,
                        Message = "Query executed successfully",
                        RowCount = results.Count,
                        DatabaseId = id
                    });
                }
                else
                {
                    var nlpResult = await _nlQueryService.ProcessAsync(dbManager, connection.Type, request.Query);

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
    }
}