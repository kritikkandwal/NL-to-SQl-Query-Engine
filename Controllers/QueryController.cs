using Azure;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Mysqlx.Crud;
using NlpToSqlApi.Models;
using NlpToSqlApi.Services;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Utilities.Collections;
using System;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Numerics;

namespace NlpToSqlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueryController : ControllerBase
    {
        private readonly ILogger<QueryController> _logger;

        public QueryController(ILogger<QueryController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Process a natural language query and return SQL results
        /// </summary>
        [HttpPost("process")]
        public async Task<ActionResult<QueryResponse>> ProcessQuery([FromBody] QueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new QueryResponse 
                { 
                    Success = false, 
                    ErrorMessage = "Query cannot be empty" 
                });
            }

            try
            {
                // Use provided database path or default sample database
                var databasePath = request.DatabasePath ?? "sample_company.db";
                
                // Ensure sample database exists if using default
                if (request.DatabasePath == null)
                {
                    await EnsureSampleDatabaseAsync(databasePath);
                }

                using var nlpService = new NlpToSqlService(databasePath);
                
                // Test connection first
                var connectionTest = await nlpService.TestConnectionAsync();
                if (!connectionTest)
                {
                    return BadRequest(new QueryResponse 
                    { 
                        Success = false, 
                        ErrorMessage = "Could not connect to the database" 
                    });
                }

                var response = await nlpService.ProcessQueryAsync(request.Query, request.Language ?? "en");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing query: {Query}", request.Query);
                return StatusCode(500, new QueryResponse 
                { 
                    Success = false, 
                    ErrorMessage = "Internal server error occurred while processing the query" 
                });
            }
        }

        /// <summary>
        /// Get database schema information
        /// </summary>
        [HttpGet("schema")]
        public async Task<ActionResult<Dictionary<string, List<string>>>> GetSchema([FromQuery] string? databasePath = null)
        {
            try
            {
                var dbPath = databasePath ?? "sample_company.db";
                
                // Ensure sample database exists if using default
                if (databasePath == null)
                {
                    await EnsureSampleDatabaseAsync(dbPath);
                }

                using var nlpService = new NlpToSqlService(dbPath);
                var schema = await nlpService.GetDatabaseSchemaAsync();
                return Ok(schema);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schema for database: {DatabasePath}", databasePath);
                return StatusCode(500, "Error retrieving database schema");
            }
        }

        /// <summary>
        /// Test database connection
        /// </summary>
        [HttpGet("test-connection")]
        public async Task<ActionResult<object>> TestConnection([FromQuery] string? databasePath = null)
        {
            try
            {
                var dbPath = databasePath ?? "sample_company.db";
                
                // Ensure sample database exists if using default
                if (databasePath == null)
                {
                    await EnsureSampleDatabaseAsync(dbPath);
                }

                using var nlpService = new NlpToSqlService(dbPath);
                var isConnected = await nlpService.TestConnectionAsync();
                
                return Ok(new { connected = isConnected, databasePath = dbPath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for database: {DatabasePath}", databasePath);
                return Ok(new { connected = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get example queries
        /// </summary>
        [HttpGet("examples")]
        public ActionResult<object> GetExampleQueries()
        {
            var examples = new[]
            {
                "Show all employees in the IT department",
                "Find employees with salary greater than 50000",
                "List IT department employees with salary more than 50000",
                "Show all departments and their employee counts",
                "Find the highest paid employee in each department",
                "Count employees by department",
                "Show average salary by department"
            };

            return Ok(new { examples });
        }

        private async Task EnsureSampleDatabaseAsync(string databasePath)
        {
            if (!System.IO.File.Exists(databasePath))
            {
                _logger.LogInformation("Creating sample database at {DatabasePath}", databasePath);
                await CreateSampleDatabaseAsync(databasePath);
            }
        }

        private async Task CreateSampleDatabaseAsync(string databasePath)
        {
            using var dbManager = new DatabaseManager(databasePath);
            
            // Create tables and insert sample data
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

            // Insert sample data
            await InsertSampleDataAsync(dbManager);
        }

        private async Task InsertSampleDataAsync(DatabaseManager dbManager)
        {
            // Insert departments
            var deptInserts = new[]
            {
                "INSERT INTO departments (id, name, location, budget) VALUES (1, 'IT', 'Building A, Floor 3', 500000)",
                "INSERT INTO departments (id, name, location, budget) VALUES (2, 'HR', 'Building A, Floor 1', 200000)",
                "INSERT INTO departments (id, name, location, budget) VALUES (3, 'Finance', 'Building B, Floor 2', 300000)",
                "INSERT INTO departments (id, name, location, budget) VALUES (4, 'Marketing', 'Building A, Floor 2', 250000)",
                "INSERT INTO departments (id, name, location, budget) VALUES (5, 'Sales', 'Building C, Floor 1', 400000)"
            };

            foreach (var insert in deptInserts)
            {
                await dbManager.ExecuteQueryAsync(insert);
            }

            // Insert employees
            var empInserts = new[]
            {
                "INSERT INTO employees VALUES (1, 'John', 'Smith', 'john.smith@company.com', 1, '2020-01-15', 'Software Engineer', NULL)",
                "INSERT INTO employees VALUES (2, 'Sarah', 'Johnson', 'sarah.johnson@company.com', 1, '2019-03-20', 'Senior Developer', NULL)",
                "INSERT INTO employees VALUES (3, 'Mike', 'Davis', 'mike.davis@company.com', 1, '2021-06-10', 'DevOps Engineer', 2)",
                "INSERT INTO employees VALUES (4, 'Emily', 'Brown', 'emily.brown@company.com', 2, '2018-09-05', 'HR Manager', NULL)",
                "INSERT INTO employees VALUES (5, 'David', 'Wilson', 'david.wilson@company.com', 2, '2020-11-12', 'HR Specialist', 4)",
                "INSERT INTO employees VALUES (6, 'Lisa', 'Anderson', 'lisa.anderson@company.com', 3, '2017-05-08', 'Financial Analyst', NULL)",
                "INSERT INTO employees VALUES (7, 'Tom', 'Taylor', 'tom.taylor@company.com', 3, '2019-12-03', 'Accountant', 6)",
                "INSERT INTO employees VALUES (8, 'Jennifer', 'White', 'jennifer.white@company.com', 4, '2020-02-18', 'Marketing Manager', NULL)",
                "INSERT INTO employees VALUES (9, 'Robert', 'Garcia', 'robert.garcia@company.com', 4, '2021-04-25', 'Marketing Specialist', 8)",
                "INSERT INTO employees VALUES (10, 'Amanda', 'Martinez', 'amanda.martinez@company.com', 5, '2018-08-14', 'Sales Manager', NULL)"
            };

            foreach (var insert in empInserts)
            {
                await dbManager.ExecuteQueryAsync(insert);
            }

            // Insert salaries
            var salInserts = new[]
            {
                "INSERT INTO salaries VALUES (1, 1, 75000, 5000, '2020-01-15')",
                "INSERT INTO salaries VALUES (2, 2, 95000, 8000, '2019-03-20')",
                "INSERT INTO salaries VALUES (3, 3, 80000, 6000, '2021-06-10')",
                "INSERT INTO salaries VALUES (4, 4, 85000, 7000, '2018-09-05')",
                "INSERT INTO salaries VALUES (5, 5, 55000, 3000, '2020-11-12')",
                "INSERT INTO salaries VALUES (6, 6, 90000, 7500, '2017-05-08')",
                "INSERT INTO salaries VALUES (7, 7, 65000, 4000, '2019-12-03')",
                "INSERT INTO salaries VALUES (8, 8, 82000, 6500, '2020-02-18')",
                "INSERT INTO salaries VALUES (9, 9, 58000, 3500, '2021-04-25')",
                "INSERT INTO salaries VALUES (10, 10, 92000, 8500, '2018-08-14')"
            };

            foreach (var insert in salInserts)
            {
                await dbManager.ExecuteQueryAsync(insert);
            }
        }
    }
}


