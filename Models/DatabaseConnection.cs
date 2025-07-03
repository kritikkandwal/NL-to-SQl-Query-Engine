namespace NlpToSqlApi.Models;

public class DatabaseConnection
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DatabaseType Type { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public int TableCount { get; set; }
    public List<string> Tables { get; set; } = new();
}

public enum DatabaseType
{
    SQLite,
    MySQL,
    PostgreSQL,
    MSSQL
}

public class DatabaseConnectionRequest
{
    public string Name { get; set; } = string.Empty;
    public DatabaseType Type { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Tables { get; set; } = new();
    public string? ErrorDetails { get; set; }
}