# NLP to SQL Query System

Convert natural language questions into SQL queries and get results from your databases.

## Quick Start (Local Setup)

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed on your machine

### Windows
1. Download all files to a folder on your computer
2. Double-click `run.bat` or open Command Prompt in the folder and run:
   ```cmd
   run.bat
   ```

### macOS/Linux
1. Download all files to a folder on your computer
2. Open Terminal in the folder and run:
   ```bash
   ./run.sh
   ```

### Manual Start
```bash
dotnet restore
dotnet build
dotnet run
```

### Access the Application
Open your browser and go to: **http://localhost:5000**

## Features

- **Upload SQLite databases** - Drag and drop your .db files
- **Natural language queries** - Ask questions in plain English
- **Multiple databases** - Switch between different databases
- **Schema visualization** - See table structures and relationships
- **Real-time results** - Get SQL and data instantly

## Example Queries

Try these with the sample databases:

**Company Database:**
- "Show all employees in IT department"
- "Who are the highest paid employees?"
- "List employees hired after 2020"

**Store Database:**
- "Show all electronics products"
- "Which products cost more than 100?"
- "List orders with customer names"

## API Endpoints

The system provides REST API endpoints for integration:

- **POST** `/api/query/process` - Process natural language queries
- **GET** `/api/database/list` - List available databases
- **POST** `/api/database/upload` - Upload new database
- **GET** `/api/database/{id}/schema` - Get database schema

Full API documentation available at: **http://localhost:5000/swagger**

## Supported Database Types

- **SQLite** (.db, .sqlite, .sqlite3) - File upload or connection string
- **MySQL** - Connection via connection string
- **PostgreSQL** - Connection via connection string
- Tables with foreign key relationships
- Various data types (text, numbers, dates)

## Database Connection Examples

**MySQL:** `Server=localhost;Database=mydb;User=user;Password=pass;`
**PostgreSQL:** `Host=localhost;Database=mydb;Username=user;Password=pass;`
**SQLite:** `Data Source=path/to/database.db`

## Troubleshooting

**Port already in use?**
- Change the port in `Program.cs` or stop other applications using port 5000

**Missing .NET?**
- Install .NET 8 SDK from Microsoft website

**Database upload issues?**
- Ensure your SQLite file is valid and not corrupted

For detailed setup instructions, see `DEPLOYMENT.md`.