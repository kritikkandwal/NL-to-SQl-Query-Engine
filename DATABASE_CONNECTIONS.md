# Database Connection Guide

Your NLP to SQL system now supports multiple database types through connection strings. You can connect to MySQL, PostgreSQL, and SQLite databases.

## Supported Database Types

- **SQLite** - File-based databases (.db, .sqlite, .sqlite3)
- **MySQL** - MySQL Server databases  
- **PostgreSQL** - PostgreSQL databases
- **Microsoft SQL Server (MSSQL)** - SQL Server and Azure SQL Database

## Adding Database Connections

### Through Web Interface

1. **Open the application** at `http://localhost:5000`
2. **Navigate to "Add Database Connection"** section
3. **Select database type** from the dropdown
4. **Enter connection name** (e.g., "My MySQL Database")
5. **Enter connection string** (see examples below)
6. **Click "Add Connection"** - the system will test the connection
7. **Switch to your new connection** using the database selector

### Connection String Examples

#### MySQL
```
Server=localhost;Database=your_database;User=your_username;Password=your_password;Port=3306;
```

**Real-world examples:**
```bash
# Local MySQL
Server=localhost;Database=company_db;User=root;Password=mypassword;

# Remote MySQL
Server=mysql.example.com;Database=sales_data;User=analyst;Password=secure123;Port=3306;

# MySQL with SSL
Server=localhost;Database=inventory;User=app_user;Password=app_pass;SslMode=Required;
```

#### PostgreSQL
```
Host=localhost;Database=your_database;Username=your_username;Password=your_password;Port=5432;
```

**Real-world examples:**
```bash
# Local PostgreSQL
Host=localhost;Database=company_db;Username=postgres;Password=mypassword;

# Remote PostgreSQL
Host=postgres.example.com;Database=analytics;Username=analyst;Password=secure123;Port=5432;

# PostgreSQL with SSL
Host=localhost;Database=orders;Username=app_user;Password=app_pass;SSL Mode=Require;
```

#### Microsoft SQL Server (MSSQL)
```
Server=localhost;Database=your_database;User Id=your_username;Password=your_password;
```

**Real-world examples:**
```bash
# Local SQL Server with Windows Authentication (Windows only)
Server=localhost;Database=company_db;Integrated Security=true;

# Local SQL Server with SQL Authentication
Server=localhost;Database=sales_data;User Id=sa;Password=mypassword;

# Remote SQL Server
Server=sqlserver.example.com;Database=inventory;User Id=app_user;Password=secure123;

# Azure SQL Database
Server=myserver.database.windows.net;Database=analytics;User Id=myuser@myserver;Password=mypassword;Encrypt=true;

# SQL Server with custom port
Server=localhost,1433;Database=orders;User Id=app_user;Password=app_pass;

# SQL Server Express instance
Server=localhost\SQLEXPRESS;Database=testdb;User Id=test;Password=test123;
```

**Important Notes:**
- MSSQL connections require an actual SQL Server instance running
- LocalDB is not supported on Linux environments
- Connection timeout is set to 10 seconds to prevent hanging
- Proper error messages are displayed when connection fails

#### SQLite (Alternative to file upload)
```
Data Source=path/to/your/database.db
```

## Using API Endpoints

### Add Connection
```bash
curl -X POST "http://localhost:5000/api/connection/add" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My MySQL Database",
    "type": 1,
    "connectionString": "Server=localhost;Database=mydb;User=user;Password=pass;"
  }'
```

**Database Types:**
- `0` = SQLite
- `1` = MySQL
- `2` = PostgreSQL
- `3` = MSSQL

### List Connections
```bash
curl -X GET "http://localhost:5000/api/connection/list"
```

### Test Connection
```bash
curl -X POST "http://localhost:5000/api/connection/{connection-id}/test"
```

### Get Schema
```bash
curl -X GET "http://localhost:5000/api/connection/{connection-id}/schema"
```

### Execute Query
```bash
curl -X POST "http://localhost:5000/api/connection/{connection-id}/query" \
  -H "Content-Type: application/json" \
  -d '{"query": "SELECT * FROM users LIMIT 5"}'
```

## Setting Up Your Database

### MySQL Setup

1. **Install MySQL Server** on your machine or use a cloud service
2. **Create a database:**
   ```sql
   CREATE DATABASE your_database_name;
   ```
3. **Create a user and grant permissions:**
   ```sql
   CREATE USER 'your_username'@'localhost' IDENTIFIED BY 'your_password';
   GRANT ALL PRIVILEGES ON your_database_name.* TO 'your_username'@'localhost';
   FLUSH PRIVILEGES;
   ```
4. **Import your data** or create tables as needed

### PostgreSQL Setup

1. **Install PostgreSQL** on your machine or use a cloud service
2. **Create a database:**
   ```sql
   CREATE DATABASE your_database_name;
   ```
3. **Create a user and grant permissions:**
   ```sql
   CREATE USER your_username WITH PASSWORD 'your_password';
   GRANT ALL PRIVILEGES ON DATABASE your_database_name TO your_username;
   ```
4. **Import your data** or create tables as needed

### Microsoft SQL Server (MSSQL) Setup

1. **Install SQL Server** or use Azure SQL Database
2. **Create a database:**
   ```sql
   CREATE DATABASE your_database_name;
   ```
3. **Create a login and user:**
   ```sql
   CREATE LOGIN your_username WITH PASSWORD = 'your_password';
   USE your_database_name;
   CREATE USER your_username FOR LOGIN your_username;
   ALTER ROLE db_datareader ADD MEMBER your_username;
   ALTER ROLE db_datawriter ADD MEMBER your_username;
   ```
4. **Import your data** or create tables as needed

**MSSQL Connection Examples:**
- **Local with Windows Auth:** `Server=localhost;Database=MyDB;Integrated Security=true;`
- **Local with SQL Auth:** `Server=localhost;Database=MyDB;User Id=sa;Password=mypassword;`
- **Azure SQL Database:** `Server=myserver.database.windows.net;Database=MyDB;User Id=myuser@myserver;Password=mypassword;Encrypt=true;`

## Security Considerations

### Connection String Security
- **Never share connection strings** containing passwords
- **Use environment variables** for production deployments
- **Create dedicated database users** with minimal required permissions
- **Use SSL/TLS connections** when connecting over networks

### Recommended Database Permissions
For the NLP to SQL system, the database user needs:
- **SELECT** permissions on all tables you want to query
- **No INSERT/UPDATE/DELETE** permissions (read-only access recommended)
- **Access to schema information tables** to analyze table structure

### Example Secure User Setup

**MySQL:**
```sql
CREATE USER 'nlp_readonly'@'localhost' IDENTIFIED BY 'secure_password';
GRANT SELECT ON your_database.* TO 'nlp_readonly'@'localhost';
GRANT SHOW VIEW ON your_database.* TO 'nlp_readonly'@'localhost';
```

**PostgreSQL:**
```sql
CREATE USER nlp_readonly WITH PASSWORD 'secure_password';
GRANT CONNECT ON DATABASE your_database TO nlp_readonly;
GRANT USAGE ON SCHEMA public TO nlp_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO nlp_readonly;
```

## Troubleshooting

### Common Connection Issues

**"Connection failed" error:**
- Verify database server is running
- Check host/port are correct
- Confirm username/password are valid
- Ensure user has required permissions

**"Access denied" error:**
- Check user permissions on the database
- Verify the user can connect from your application's host
- For MySQL: ensure user is allowed from the correct host (`'user'@'localhost'` vs `'user'@'%'`)

**"Database not found" error:**
- Confirm the database name exists
- Check for typos in database name
- Verify user has access to the specific database

### Testing Your Connection

You can test database connectivity before adding to the NLP system:

**MySQL:**
```bash
mysql -h localhost -u your_username -p your_database
```

**PostgreSQL:**
```bash
psql -h localhost -U your_username -d your_database
```

## Supported SQL Features

The system supports:
- **SELECT statements** with WHERE, JOIN, GROUP BY, ORDER BY
- **Aggregate functions** (COUNT, SUM, AVG, MAX, MIN)
- **Multiple table joins** based on foreign key relationships
- **Complex WHERE conditions** with AND, OR, comparison operators
- **Subqueries** and common table expressions (depending on database)

### Database-Specific SQL Differences

The system automatically adapts to different SQL dialects:
- **MySQL**: Uses backticks for identifiers, specific date functions
- **PostgreSQL**: Uses double quotes for identifiers, advanced features
- **SQLite**: Simplified SQL syntax, limited data types

## Migration from SQLite Files

If you have existing SQLite databases and want to move to MySQL/PostgreSQL:

1. **Export your SQLite data** to SQL format
2. **Import into your target database** (with schema adjustments if needed)
3. **Add the new database connection** to the NLP system
4. **Test with your existing queries** - they should work the same way

The natural language processing works identically across all database types, so your queries like "Show all customers from California" will work the same way regardless of the underlying database.

## Natural Language Examples

Once connected, you can use natural language queries with any database:

**Product/Inventory Database:**
- "Show me all electronics products"
- "Find products with price more than 100"
- "How many items are in stock"
- "List products by supplier"

**Customer Database:**
- "Show customers from California"
- "Find customers with orders over 1000"
- "How many active customers do we have"
- "List customers by registration date"

**Sales/Order Database:**
- "Show orders from last month"
- "Find high-value orders above 500"
- "How many orders were placed today"
- "List orders with customer details"

The system automatically:
- Identifies relevant tables based on your question
- Generates appropriate JOIN queries when needed
- Handles different SQL dialects (MySQL vs PostgreSQL vs SQLite)
- Maps natural language operators to SQL (e.g., "more than" → ">")
- Selects meaningful columns to display