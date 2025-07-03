namespace NlpToSqlApi.Models
{
    public class DatabaseSchema
    {
        public Dictionary<string, TableInfo> Tables { get; set; } = new();
        public Dictionary<string, List<ForeignKeyInfo>> Relationships { get; set; } = new();
    }

    public class TableInfo
    {
        public string Name { get; set; } = string.Empty;
        public List<ColumnInfo> Columns { get; set; } = new();
        public List<string> PrimaryKeys { get; set; } = new();
        public List<string> ColumnNames { get; set; } = new();
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool NotNull { get; set; }
        public object? DefaultValue { get; set; }
        public bool PrimaryKey { get; set; }
    }

    public class ForeignKeyInfo
    {
        public string Column { get; set; } = string.Empty;
        public string ReferencedTable { get; set; } = string.Empty;
        public string ReferencedColumn { get; set; } = string.Empty;
    }
}