using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using Npgsql;
using NlpToSqlApi.Models;
using System.Data;
using System.Data.Common;

namespace NlpToSqlApi.Services
{
    public class UniversalDatabaseManager : IDisposable
    {
        private readonly DatabaseType _databaseType;
        private readonly string _connectionString;
        private DbConnection? _connection;

        public string CacheKey => $"{_databaseType}|{_connectionString}";

        public UniversalDatabaseManager(DatabaseType type, string connectionString)
        {
            _databaseType = type;
            _connectionString = connectionString;
        }

        private async Task EnsureConnectionAsync()
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                _connection?.Dispose();
                
                var connectionString = _connectionString;
                
                // Add timeout for MSSQL to prevent hanging
                if (_databaseType == DatabaseType.MSSQL && !_connectionString.Contains("Connection Timeout"))
                {
                    connectionString = _connectionString.TrimEnd(';') + ";Connection Timeout=10;";
                }
                
                _connection = _databaseType switch
                {
                    DatabaseType.SQLite => new SqliteConnection(connectionString),
                    DatabaseType.MySQL => new MySqlConnection(connectionString),
                    DatabaseType.PostgreSQL => new NpgsqlConnection(connectionString),
                    DatabaseType.MSSQL => new SqlConnection(connectionString),
                    _ => throw new NotSupportedException($"Database type {_databaseType} is not supported")
                };
                
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await _connection.OpenAsync(cancellationTokenSource.Token);
            }
        }

        public async Task<ConnectionTestResult> TestConnectionAsync()
        {
            try
            {
                await EnsureConnectionAsync();
                var tables = await GetTablesAsync();
                
                return new ConnectionTestResult
                {
                    Success = true,
                    Message = "Connection successful",
                    Tables = tables
                };
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Connection failed",
                    ErrorDetails = ex.Message
                };
            }
        }

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query)
        {
            await EnsureConnectionAsync();
            
            using var command = _connection!.CreateCommand();
            command.CommandText = query;
            using var reader = await command.ExecuteReaderAsync();
            
            var results = new List<Dictionary<string, object>>();
            
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[reader.GetName(i)] = value == DBNull.Value ? null : value;
                }
                results.Add(row);
            }
            
            return results;
        }

        public async Task<List<string>> GetTablesAsync()
        {
            await EnsureConnectionAsync();
            
            string query = _databaseType switch
            {
                DatabaseType.SQLite => "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'",
                DatabaseType.MySQL => "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'",
                DatabaseType.PostgreSQL => "SELECT tablename FROM pg_tables WHERE schemaname = 'public'",
                DatabaseType.MSSQL => "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'",
                _ => throw new NotSupportedException($"Database type {_databaseType} is not supported")
            };

            var results = await ExecuteQueryAsync(query);
            return results.Select(r => r.Values.First()?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        }

        public async Task<List<ColumnInfo>> GetTableSchemaAsync(string tableName)
        {
            await EnsureConnectionAsync();
            
            string query = _databaseType switch
            {
                DatabaseType.SQLite => $"PRAGMA table_info({tableName})",
                DatabaseType.MySQL => $@"
                    SELECT 
                        COLUMN_NAME as name, 
                        DATA_TYPE as type, 
                        IS_NULLABLE = 'NO' as not_null,
                        COLUMN_KEY = 'PRI' as primary_key,
                        COLUMN_DEFAULT as default_value
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}'
                    ORDER BY ORDINAL_POSITION",
                DatabaseType.PostgreSQL => $@"
                    SELECT 
                        column_name as name,
                        data_type as type,
                        is_nullable = 'NO' as not_null,
                        column_default as default_value
                    FROM information_schema.columns 
                    WHERE table_schema = 'public' AND table_name = '{tableName}'
                    ORDER BY ordinal_position",
                DatabaseType.MSSQL => $@"
                    SELECT 
                        COLUMN_NAME as name,
                        DATA_TYPE as type,
                        CASE WHEN IS_NULLABLE = 'NO' THEN 1 ELSE 0 END as not_null,
                        0 as primary_key,
                        COLUMN_DEFAULT as default_value
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = '{tableName}'
                    ORDER BY ORDINAL_POSITION",
                _ => throw new NotSupportedException($"Database type {_databaseType} is not supported")
            };

            var results = await ExecuteQueryAsync(query);
            var columns = new List<ColumnInfo>();

            foreach (var row in results)
            {
                if (_databaseType == DatabaseType.SQLite)
                {
                    columns.Add(new ColumnInfo
                    {
                        Name = row["name"]?.ToString() ?? "",
                        Type = row["type"]?.ToString() ?? "",
                        NotNull = Convert.ToBoolean(row["notnull"]),
                        PrimaryKey = Convert.ToBoolean(row["pk"]),
                        DefaultValue = row["dflt_value"]?.ToString()
                    });
                }
                else
                {
                    columns.Add(new ColumnInfo
                    {
                        Name = row["name"]?.ToString() ?? "",
                        Type = row["type"]?.ToString() ?? "",
                        NotNull = Convert.ToBoolean(row["not_null"]),
                        PrimaryKey = row.ContainsKey("primary_key") ? Convert.ToBoolean(row["primary_key"]) : false,
                        DefaultValue = row["default_value"]?.ToString()
                    });
                }
            }

            // For PostgreSQL, get primary key info separately
            if (_databaseType == DatabaseType.PostgreSQL)
            {
                var pkQuery = $@"
                    SELECT a.attname
                    FROM pg_index i
                    JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey)
                    WHERE i.indrelid = '{tableName}'::regclass AND i.indisprimary";
                
                try
                {
                    var pkResults = await ExecuteQueryAsync(pkQuery);
                    var pkColumns = pkResults.Select(r => r["attname"]?.ToString()).ToHashSet();
                    
                    foreach (var column in columns)
                    {
                        if (pkColumns.Contains(column.Name))
                        {
                            column.PrimaryKey = true;
                        }
                    }
                }
                catch
                {
                    // If PK query fails, continue without PK info
                }
            }

            // SQL Server: INFORMATION_SCHEMA.COLUMNS carries no key info
            if (_databaseType == DatabaseType.MSSQL)
            {
                var pkQuery = $@"
                    SELECT kcu.COLUMN_NAME AS attname
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                      ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND kcu.TABLE_NAME = tc.TABLE_NAME AND kcu.TABLE_SCHEMA = tc.TABLE_SCHEMA
                    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.TABLE_NAME = '{tableName.Replace("'", "''")}'";
                try
                {
                    var pkColumns = (await ExecuteQueryAsync(pkQuery)).Select(r => r["attname"]?.ToString()).ToHashSet();
                    foreach (var column in columns.Where(c => pkColumns.Contains(c.Name))) column.PrimaryKey = true;
                }
                catch
                {
                    // continue without PK info
                }
            }

            return columns;
        }

        public async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(string tableName)
        {
            await EnsureConnectionAsync();
            
            string query = _databaseType switch
            {
                DatabaseType.SQLite => $"PRAGMA foreign_key_list({tableName})",
                DatabaseType.MySQL => $@"
                    SELECT 
                        COLUMN_NAME as column_name,
                        REFERENCED_TABLE_NAME as referenced_table,
                        REFERENCED_COLUMN_NAME as referenced_column
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = '{tableName}' 
                    AND REFERENCED_TABLE_NAME IS NOT NULL",
                DatabaseType.PostgreSQL => $@"
                    SELECT 
                        kcu.column_name,
                        ccu.table_name AS referenced_table,
                        ccu.column_name AS referenced_column
                    FROM information_schema.table_constraints AS tc 
                    JOIN information_schema.key_column_usage AS kcu
                        ON tc.constraint_name = kcu.constraint_name
                        AND tc.table_schema = kcu.table_schema
                    JOIN information_schema.constraint_column_usage AS ccu
                        ON ccu.constraint_name = tc.constraint_name
                        AND ccu.table_schema = tc.table_schema
                    WHERE tc.constraint_type = 'FOREIGN KEY' 
                    AND tc.table_name = '{tableName}'",
                DatabaseType.MSSQL => $@"
                    SELECT 
                        COL_NAME(fc.parent_object_id, fc.parent_column_id) AS column_name,
                        OBJECT_NAME(fc.referenced_object_id) AS referenced_table,
                        COL_NAME(fc.referenced_object_id, fc.referenced_column_id) AS referenced_column
                    FROM sys.foreign_keys AS f
                    INNER JOIN sys.foreign_key_columns AS fc ON f.object_id = fc.constraint_object_id
                    WHERE OBJECT_NAME(fc.parent_object_id) = '{tableName}'",
                _ => throw new NotSupportedException($"Database type {_databaseType} is not supported")
            };

            var results = await ExecuteQueryAsync(query);
            var foreignKeys = new List<ForeignKeyInfo>();

            foreach (var row in results)
            {
                if (_databaseType == DatabaseType.SQLite)
                {
                    foreignKeys.Add(new ForeignKeyInfo
                    {
                        Column = row["from"]?.ToString() ?? "",
                        ReferencedTable = row["table"]?.ToString() ?? "",
                        ReferencedColumn = row["to"]?.ToString() ?? ""
                    });
                }
                else
                {
                    foreignKeys.Add(new ForeignKeyInfo
                    {
                        Column = row["column_name"]?.ToString() ?? "",
                        ReferencedTable = row["referenced_table"]?.ToString() ?? "",
                        ReferencedColumn = row["referenced_column"]?.ToString() ?? ""
                    });
                }
            }

            return foreignKeys;
        }

        public async Task<List<Dictionary<string, object>>> GetSampleDataAsync(string tableName, int limit = 3)
        {
            await EnsureConnectionAsync();
            
            string query = _databaseType switch
            {
                DatabaseType.SQLite => $"SELECT * FROM {tableName} LIMIT {limit}",
                DatabaseType.MySQL => $"SELECT * FROM {tableName} LIMIT {limit}",
                DatabaseType.PostgreSQL => $"SELECT * FROM {tableName} LIMIT {limit}",
                DatabaseType.MSSQL => $"SELECT TOP {limit} * FROM {tableName}",
                _ => throw new NotSupportedException($"Database type {_databaseType} is not supported")
            };

            return await ExecuteQueryAsync(query);
        }

        public string GetSqlDialect()
        {
            return _databaseType switch
            {
                DatabaseType.SQLite => "SQLite",
                DatabaseType.MySQL => "MySQL",
                DatabaseType.PostgreSQL => "PostgreSQL",
                DatabaseType.MSSQL => "MSSQL",
                _ => "Unknown"
            };
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}