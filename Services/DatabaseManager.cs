using Microsoft.Data.Sqlite;
using NlpToSqlApi.Models;
using System.Data;

namespace NlpToSqlApi.Services
{
    public class DatabaseManager : IDisposable
    {
        private readonly string _connectionString;
        private SqliteConnection? _connection;

        public DatabaseManager(string databasePath)
        {
            _connectionString = $"Data Source={databasePath}";
        }

        private async Task EnsureConnectionAsync()
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                _connection?.Dispose();
                _connection = new SqliteConnection(_connectionString);
                await _connection.OpenAsync();
            }
        }

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query)
        {
            await EnsureConnectionAsync();
            
            using var command = new SqliteCommand(query, _connection);
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
            
            const string query = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            using var command = new SqliteCommand(query, _connection);
            using var reader = await command.ExecuteReaderAsync();
            
            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString("name"));
            }
            
            return tables;
        }

        public async Task<List<ColumnInfo>> GetTableSchemaAsync(string tableName)
        {
            await EnsureConnectionAsync();
            
            var query = $"PRAGMA table_info({tableName})";
            using var command = new SqliteCommand(query, _connection);
            using var reader = await command.ExecuteReaderAsync();
            
            var columns = new List<ColumnInfo>();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString("name"),
                    Type = reader.GetString("type"),
                    NotNull = reader.GetBoolean("notnull"),
                    DefaultValue = reader.IsDBNull("dflt_value") ? null : reader.GetValue("dflt_value"),
                    PrimaryKey = reader.GetBoolean("pk")
                });
            }
            
            return columns;
        }

        public async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(string tableName)
        {
            await EnsureConnectionAsync();
            
            var query = $"PRAGMA foreign_key_list({tableName})";
            using var command = new SqliteCommand(query, _connection);
            using var reader = await command.ExecuteReaderAsync();
            
            var foreignKeys = new List<ForeignKeyInfo>();
            while (await reader.ReadAsync())
            {
                foreignKeys.Add(new ForeignKeyInfo
                {
                    Column = reader.GetString("from"),
                    ReferencedTable = reader.GetString("table"),
                    ReferencedColumn = reader.GetString("to")
                });
            }
            
            return foreignKeys;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await EnsureConnectionAsync();
                using var command = new SqliteCommand("SELECT 1", _connection);
                await command.ExecuteScalarAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {       
            _connection?.Dispose();
        }
    }
}