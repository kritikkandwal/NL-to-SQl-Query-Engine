using Microsoft.AspNetCore.Mvc;
using NlpToSqlApi.Services;
using System.Text.Json;

namespace NlpToSqlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseController : ControllerBase
    {
        private readonly ILogger<DatabaseController> _logger;
        private readonly string _databaseDirectory = "databases";

        public DatabaseController(ILogger<DatabaseController> logger)
        {
            _logger = logger;
            
            // Ensure database directory exists
            if (!Directory.Exists(_databaseDirectory))
            {
                Directory.CreateDirectory(_databaseDirectory);
            }
        }

        /// <summary>
        /// Upload a SQLite database file
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult<object>> UploadDatabase(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file uploaded" });
            }

            if (!file.FileName.EndsWith(".db") && !file.FileName.EndsWith(".sqlite") && !file.FileName.EndsWith(".sqlite3"))
            {
                return BadRequest(new { success = false, message = "Only SQLite database files (.db, .sqlite, .sqlite3) are allowed" });
            }

            try
            {
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(_databaseDirectory, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Test the database connection
                using var dbManager = new DatabaseManager(filePath);
                var isValid = await dbManager.TestConnectionAsync();
                
                if (!isValid)
                {
                    System.IO.File.Delete(filePath);
                    return BadRequest(new { success = false, message = "Invalid SQLite database file" });
                }

                // Get basic info about the database
                var tables = await dbManager.GetTablesAsync();
                
                var result = new
                {
                    success = true,
                    message = "Database uploaded successfully",
                    databaseId = fileName,
                    databasePath = filePath,
                    tableCount = tables.Count,
                    tables = tables
                };

                _logger.LogInformation("Database uploaded: {FileName}, Tables: {TableCount}", fileName, tables.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading database: {FileName}", file.FileName);
                return StatusCode(500, new { success = false, message = "Error uploading database file" });
            }
        }

        /// <summary>
        /// List all uploaded databases
        /// </summary>
        [HttpGet("list")]
        public async Task<ActionResult<object>> ListDatabases()
        {
            try
            {
                var databases = new List<object>();
                
                // Add default sample database
                databases.Add(new
                {
                    id = "sample",
                    name = "Sample Company Database",
                    path = "sample_company.db",
                    isDefault = true,
                    description = "Contains employees, departments, and salaries data"
                });

                // Add uploaded databases
                if (Directory.Exists(_databaseDirectory))
                {
                    var files = Directory.GetFiles(_databaseDirectory, "*.db")
                        .Concat(Directory.GetFiles(_databaseDirectory, "*.sqlite"))
                        .Concat(Directory.GetFiles(_databaseDirectory, "*.sqlite3"));

                    foreach (var file in files)
                    {
                        try
                        {
                            var fileName = Path.GetFileName(file);
                            using var dbManager = new DatabaseManager(file);
                            
                            if (await dbManager.TestConnectionAsync())
                            {
                                var tables = await dbManager.GetTablesAsync();
                                
                                databases.Add(new
                                {
                                    id = fileName,
                                    name = fileName,
                                    path = file,
                                    isDefault = false,
                                    tableCount = tables.Count,
                                    tables = tables,
                                    uploadDate = System.IO.File.GetCreationTime(file)
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reading database file: {File}", file);
                        }
                    }
                }

                return Ok(new { databases });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing databases");
                return StatusCode(500, new { success = false, message = "Error listing databases" });
            }
        }

        /// <summary>
        /// Get detailed schema information for a specific database
        /// </summary>
        [HttpGet("{databaseId}/schema")]
        public async Task<ActionResult<object>> GetDatabaseSchema(string databaseId)
        {
            try
            {
                string databasePath;
                
                if (databaseId == "sample")
                {
                    databasePath = "sample_company.db";
                    // Ensure sample database exists
                    if (!System.IO.File.Exists(databasePath))
                    {
                        await CreateSampleDatabaseAsync(databasePath);
                    }
                }
                else
                {
                    databasePath = Path.Combine(_databaseDirectory, databaseId);
                    if (!System.IO.File.Exists(databasePath))
                    {
                        return NotFound(new { success = false, message = "Database not found" });
                    }
                }

                using var nlpService = new NlpToSqlService(databasePath);
                var schema = await nlpService.GetDatabaseSchemaAsync();
                
                // Get additional details
                using var dbManager = new DatabaseManager(databasePath);
                var tables = await dbManager.GetTablesAsync();
                
                var detailedSchema = new Dictionary<string, object>();
                
                foreach (var tableName in tables)
                {
                    var columns = await dbManager.GetTableSchemaAsync(tableName);
                    var foreignKeys = await dbManager.GetForeignKeysAsync(tableName);
                    
                    // Get sample data
                    var sampleData = new List<Dictionary<string, object>>();
                    try
                    {
                        var sampleQuery = $"SELECT * FROM {tableName} LIMIT 3";
                        sampleData = await dbManager.ExecuteQueryAsync(sampleQuery);
                    }
                    catch { /* Ignore errors for sample data */ }
                    
                    detailedSchema[tableName] = new
                    {
                        columns = columns.Select(c => new
                        {
                            name = c.Name,
                            type = c.Type,
                            notNull = c.NotNull,
                            primaryKey = c.PrimaryKey,
                            defaultValue = c.DefaultValue
                        }),
                        foreignKeys = foreignKeys.Select(fk => new
                        {
                            column = fk.Column,
                            referencedTable = fk.ReferencedTable,
                            referencedColumn = fk.ReferencedColumn
                        }),
                        sampleData = sampleData,
                        rowCount = await GetTableRowCount(dbManager, tableName)
                    };
                }

                return Ok(new 
                { 
                    success = true, 
                    databaseId = databaseId,
                    schema = detailedSchema,
                    summary = schema 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database schema: {DatabaseId}", databaseId);
                return StatusCode(500, new { success = false, message = "Error retrieving database schema" });
            }
        }

        /// <summary>
        /// Delete an uploaded database
        /// </summary>
        [HttpDelete("{databaseId}")]
        public ActionResult<object> DeleteDatabase(string databaseId)
        {
            if (databaseId == "sample")
            {
                return BadRequest(new { success = false, message = "Cannot delete the sample database" });
            }

            try
            {
                var databasePath = Path.Combine(_databaseDirectory, databaseId);
                
                if (!System.IO.File.Exists(databasePath))
                {
                    return NotFound(new { success = false, message = "Database not found" });
                }

                System.IO.File.Delete(databasePath);
                
                _logger.LogInformation("Database deleted: {DatabaseId}", databaseId);
                return Ok(new { success = true, message = "Database deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting database: {DatabaseId}", databaseId);
                return StatusCode(500, new { success = false, message = "Error deleting database" });
            }
        }

        /// <summary>
        /// Create a new sample database with custom data
        /// </summary>
        [HttpPost("create-sample")]
        public async Task<ActionResult<object>> CreateSampleDatabase([FromBody] CreateSampleDatabaseRequest request)
        {
            try
            {
                var fileName = $"sample_{Guid.NewGuid().ToString()[..8]}.db";
                var filePath = Path.Combine(_databaseDirectory, fileName);

                using var dbManager = new DatabaseManager(filePath);
                
                // Create tables based on request
                foreach (var table in request.Tables)
                {
                    var createTableSql = GenerateCreateTableSql(table);
                    await dbManager.ExecuteQueryAsync(createTableSql);
                    
                    // Insert sample data if provided
                    if (table.SampleData != null && table.SampleData.Any())
                    {
                        var insertSql = GenerateInsertSql(table.Name, table.SampleData);
                        await dbManager.ExecuteQueryAsync(insertSql);
                    }
                }

                var tables = await dbManager.GetTablesAsync();
                
                var result = new
                {
                    success = true,
                    message = "Sample database created successfully",
                    databaseId = fileName,
                    databasePath = filePath,
                    tableCount = tables.Count,
                    tables = tables
                };

                _logger.LogInformation("Sample database created: {FileName}, Tables: {TableCount}", fileName, tables.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sample database");
                return StatusCode(500, new { success = false, message = "Error creating sample database" });
            }
        }

        private async Task<int> GetTableRowCount(DatabaseManager dbManager, string tableName)
        {
            try
            {
                var countQuery = $"SELECT COUNT(*) as count FROM {tableName}";
                var result = await dbManager.ExecuteQueryAsync(countQuery);
                return result.FirstOrDefault()?["count"] as int? ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private string GenerateCreateTableSql(TableDefinition table)
        {
            var columns = string.Join(", ", table.Columns.Select(c => 
                $"{c.Name} {c.Type}" + 
                (c.PrimaryKey ? " PRIMARY KEY" : "") +
                (c.NotNull ? " NOT NULL" : "") +
                (!string.IsNullOrEmpty(c.DefaultValue) ? $" DEFAULT {c.DefaultValue}" : "")
            ));
            
            return $"CREATE TABLE {table.Name} ({columns})";
        }

        private string GenerateInsertSql(string tableName, List<Dictionary<string, object>> data)
        {
            if (!data.Any()) return string.Empty;
            
            var columns = string.Join(", ", data[0].Keys);
            var values = string.Join(", ", data.Select(row => 
                "(" + string.Join(", ", row.Values.Select(v => 
                    v is string ? $"'{v}'" : v?.ToString() ?? "NULL"
                )) + ")"
            ));
            
            return $"INSERT INTO {tableName} ({columns}) VALUES {values}";
        }

        private async Task CreateSampleDatabaseAsync(string databasePath)
        {
            using var dbManager = new DatabaseManager(databasePath);
            
            // Create tables and insert sample data (reuse logic from QueryController)
            var createQueries = new[]
            {
                @"CREATE TABLE departments (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE,
                    manager_id INTEGER,
                    location TEXT,
                    budget REAL
                )",
                @"CREATE TABLE employees (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    email TEXT UNIQUE,
                    department_id INTEGER,
                    hire_date DATE,
                    position TEXT,
                    manager_id INTEGER,
                    FOREIGN KEY (department_id) REFERENCES departments (id),
                    FOREIGN KEY (manager_id) REFERENCES employees (id)
                )",
                @"CREATE TABLE salaries (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    employee_id INTEGER,
                    salary REAL NOT NULL,
                    bonus REAL DEFAULT 0,
                    effective_date DATE,
                    FOREIGN KEY (employee_id) REFERENCES employees (id)
                )"
            };

            foreach (var query in createQueries)
            {
                await dbManager.ExecuteQueryAsync(query);
            }

            // Insert sample data (abbreviated for brevity)
            var sampleInserts = new[]
            {
                "INSERT INTO departments VALUES (1, 'IT', NULL, 'Building A', 500000)",
                "INSERT INTO departments VALUES (2, 'HR', NULL, 'Building B', 200000)",
                "INSERT INTO employees VALUES (1, 'John', 'Smith', 'john@company.com', 1, '2020-01-15', 'Engineer', NULL)",
                "INSERT INTO employees VALUES (2, 'Sarah', 'Johnson', 'sarah@company.com', 1, '2019-03-20', 'Senior Dev', NULL)",
                "INSERT INTO salaries VALUES (1, 1, 75000, 5000, '2020-01-15')",
                "INSERT INTO salaries VALUES (2, 2, 95000, 8000, '2019-03-20')"
            };

            foreach (var insert in sampleInserts)
            {
                await dbManager.ExecuteQueryAsync(insert);
            }
        }
    }

    public class CreateSampleDatabaseRequest
    {
        public List<TableDefinition> Tables { get; set; } = new();
    }

    public class TableDefinition
    {
        public string Name { get; set; } = string.Empty;
        public List<ColumnDefinition> Columns { get; set; } = new();
        public List<Dictionary<string, object>>? SampleData { get; set; }
    }

    public class ColumnDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool PrimaryKey { get; set; }
        public bool NotNull { get; set; }
        public string? DefaultValue { get; set; }
    }
}