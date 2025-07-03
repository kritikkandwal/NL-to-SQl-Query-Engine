using NlpToSqlApi.Models;

namespace NlpToSqlApi.Services
{
    public class SchemaAnalyzer
    {
        private readonly DatabaseManager _databaseManager;
        private DatabaseSchema? _schema;

        public SchemaAnalyzer(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public async Task<DatabaseSchema> AnalyzeSchemaAsync()
        {
            if (_schema != null) return _schema;

            _schema = new DatabaseSchema();
            var tableNames = await _databaseManager.GetTablesAsync();

            foreach (var tableName in tableNames)
            {
                var columns = await _databaseManager.GetTableSchemaAsync(tableName);
                var foreignKeys = await _databaseManager.GetForeignKeysAsync(tableName);

                var tableInfo = new TableInfo
                {
                    Name = tableName,
                    Columns = columns,
                    PrimaryKeys = columns.Where(c => c.PrimaryKey).Select(c => c.Name).ToList(),
                    ColumnNames = columns.Select(c => c.Name).ToList()
                };

                _schema.Tables[tableName] = tableInfo;

                if (foreignKeys.Any())
                {
                    _schema.Relationships[tableName] = foreignKeys;
                }
            }

            return _schema;
        }

        public List<string> FindTablesByKeyword(string keyword)
        {
            if (_schema == null) return new List<string>();

            return _schema.Tables.Keys
                .Where(tableName => tableName.ToLower().Contains(keyword.ToLower()))
                .ToList();
        }

        public List<(string Table, string Column)> FindColumnsByKeyword(string keyword)
        {
            if (_schema == null) return new List<(string, string)>();

            var matches = new List<(string, string)>();
            
            foreach (var (tableName, tableInfo) in _schema.Tables)
            {
                foreach (var columnName in tableInfo.ColumnNames)
                {
                    if (columnName.ToLower().Contains(keyword.ToLower()))
                    {
                        matches.Add((tableName, columnName));
                    }
                }
            }

            return matches;
        }

        public List<string> SuggestJoinConditions(List<string> tables)
        {
            if (_schema == null) return new List<string>();

            var joinConditions = new List<string>();

            for (int i = 0; i < tables.Count; i++)
            {
                for (int j = i + 1; j < tables.Count; j++)
                {
                    var table1 = tables[i];
                    var table2 = tables[j];

                    // Check for direct foreign key relationships
                    if (_schema.Relationships.ContainsKey(table1))
                    {
                        foreach (var fk in _schema.Relationships[table1])
                        {
                            if (fk.ReferencedTable == table2)
                            {
                                joinConditions.Add($"{table1}.{fk.Column} = {table2}.{fk.ReferencedColumn}");
                            }
                        }
                    }

                    if (_schema.Relationships.ContainsKey(table2))
                    {
                        foreach (var fk in _schema.Relationships[table2])
                        {
                            if (fk.ReferencedTable == table1)
                            {
                                joinConditions.Add($"{table2}.{fk.Column} = {table1}.{fk.ReferencedColumn}");
                            }
                        }
                    }

                    // If no direct relationship, try common column names
                    if (!joinConditions.Any())
                    {
                        var table1Info = _schema.Tables[table1];
                        var table2Info = _schema.Tables[table2];
                        
                        var commonColumns = table1Info.ColumnNames
                            .Intersect(table2Info.ColumnNames, StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        if (commonColumns.Any())
                        {
                            var commonColumn = commonColumns.First();
                            joinConditions.Add($"{table1}.{commonColumn} = {table2}.{commonColumn}");
                        }
                    }
                }
            }

            return joinConditions;
        }

        public bool IsNumericColumn(string tableName, string columnName)
        {
            if (_schema?.Tables.ContainsKey(tableName) != true) return false;

            var column = _schema.Tables[tableName].Columns
                .FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

            if (column == null) return false;

            var numericTypes = new[] { "INTEGER", "REAL", "NUMERIC", "DECIMAL", "FLOAT", "DOUBLE" };
            return numericTypes.Contains(column.Type.ToUpper());
        }

        public Dictionary<string, List<string>> GetSchemaSummary()
        {
            if (_schema == null) return new Dictionary<string, List<string>>();

            var summary = new Dictionary<string, List<string>>();
            
            foreach (var (tableName, tableInfo) in _schema.Tables)
            {
                var columnDescriptions = new List<string>();
                
                foreach (var column in tableInfo.Columns)
                {
                    var desc = $"{column.Name} ({column.Type}";
                    if (column.PrimaryKey) desc += ", PK";
                    if (column.NotNull) desc += ", NOT NULL";
                    desc += ")";
                    columnDescriptions.Add(desc);
                }
                
                summary[tableName] = columnDescriptions;
            }

            return summary;
        }
    }
}