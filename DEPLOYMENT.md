# Local Deployment Guide

## Prerequisites

1. **.NET 8 SDK** - Download from [Microsoft .NET Downloads](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Git** (optional) - For cloning the repository

## Quick Start

### Option 1: Download and Run

1. **Download the project files** to your local machine
2. **Open a terminal/command prompt** and navigate to the `NlpToSqlApi` folder
3. **Restore dependencies**:
   ```bash
   dotnet restore
   ```
4. **Build the application**:
   ```bash
   dotnet build
   ```
5. **Run the application**:
   ```bash
   dotnet run
   ```
6. **Open your browser** and go to: `http://localhost:5000`

### Option 2: Using Visual Studio

1. **Open Visual Studio 2022** (Community, Professional, or Enterprise)
2. **Open the solution** by selecting `NlpToSqlApi.csproj`
3. **Press F5** or click the "Run" button
4. The application will start automatically and open in your default browser

### Option 3: Using Visual Studio Code

1. **Open Visual Studio Code**
2. **Install the C# extension** if not already installed
3. **Open the folder** containing `NlpToSqlApi.csproj`
4. **Open the terminal** in VS Code (Ctrl+` or View > Terminal)
5. **Run the commands**:
   ```bash
   dotnet restore
   dotnet run
   ```
6. **Open browser** to `http://localhost:5000`

## Project Structure

```
NlpToSqlApi/
├── Controllers/          # API endpoints
├── Services/            # Business logic
├── Models/              # Data models
├── wwwroot/            # Web interface
├── databases/          # Uploaded databases (created automatically)
├── Program.cs          # Application entry point
├── NlpToSqlApi.csproj  # Project configuration
└── README.md           # API documentation
```

## Configuration Options

### Changing the Port

By default, the application runs on port 5000. To change this:

1. **Edit `Program.cs`** and modify the line:
   ```csharp
   app.Urls.Add("http://0.0.0.0:5000");
   ```
   Change `5000` to your desired port.

2. **Or use command line**:
   ```bash
   dotnet run --urls="http://localhost:8080"
   ```

### Database Storage Location

Uploaded databases are stored in the `databases/` folder by default. To change this, modify the `_databaseDirectory` variable in `Controllers/DatabaseController.cs`.

## Testing the Application

### 1. Web Interface
- Navigate to `http://localhost:5000`
- Use the interactive interface to upload databases and run queries

### 2. API Endpoints
- **Swagger Documentation**: `http://localhost:5000/swagger`
- **Health Check**: `http://localhost:5000/health`
- **API Root**: `http://localhost:5000/api`

### 3. Example API Calls

Using curl (if available):
```bash
# Health check
curl http://localhost:5000/health

# Process a natural language query
curl -X POST "http://localhost:5000/api/query/process" \
  -H "Content-Type: application/json" \
  -d '{"query": "Show all employees in IT department"}'

# List available databases
curl http://localhost:5000/api/database/list

# Get database schema
curl http://localhost:5000/api/query/schema
```

## Sample Databases Included

The application includes:

1. **Sample Company Database** - Employee, department, and salary data
2. **Example Store Database** - Products, suppliers, and orders data

Both are created automatically when first accessed.

## Uploading Custom Databases

1. **Prepare your SQLite database** (`.db`, `.sqlite`, or `.sqlite3` file)
2. **Use the web interface** to upload via the "Upload Database" section
3. **Or use the API**:
   ```bash
   curl -X POST "http://localhost:5000/api/database/upload" \
     -F "file=@your_database.db"
   ```

## Troubleshooting

### Common Issues

1. **Port already in use**:
   - Change the port in `Program.cs` or use `--urls` parameter
   - Or stop other applications using port 5000

2. **Missing .NET SDK**:
   - Download and install .NET 8 SDK from Microsoft
   - Verify installation: `dotnet --version`

3. **Build errors**:
   - Ensure all files are properly downloaded
   - Run `dotnet clean` then `dotnet restore` and `dotnet build`

4. **Database upload issues**:
   - Ensure the SQLite file is valid
   - Check file permissions
   - Verify the `databases/` directory exists and is writable

### Logs and Debugging

- **Console logs** show in the terminal where you ran `dotnet run`
- **Enable detailed logging** by setting environment variable:
  ```bash
  export ASPNETCORE_ENVIRONMENT=Development
  dotnet run
  ```

## Production Deployment

For production deployment:

1. **Publish the application**:
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Run the published version**:
   ```bash
   cd publish
   dotnet NlpToSqlApi.dll
   ```

3. **Configure for production**:
   - Set `ASPNETCORE_ENVIRONMENT=Production`
   - Configure proper logging
   - Set up reverse proxy (nginx/IIS) if needed
   - Configure HTTPS certificates

## Integration Examples

### JavaScript/Frontend Integration
```javascript
const response = await fetch('http://localhost:5000/api/query/process', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ query: 'Show all products with price > 100' })
});
const result = await response.json();
```

### Python Integration
```python
import requests

response = requests.post('http://localhost:5000/api/query/process', 
  json={'query': 'Show all products with price > 100'})
result = response.json()
```

### C# Integration
```csharp
using var client = new HttpClient();
var request = new { query = "Show all products with price > 100" };
var json = JsonSerializer.Serialize(request);
var content = new StringContent(json, Encoding.UTF8, "application/json");
var response = await client.PostAsync("http://localhost:5000/api/query/process", content);
var result = await response.Content.ReadAsStringAsync();
```

## Support

For issues or questions:
1. Check the troubleshooting section above
2. Review the API documentation at `/swagger`
3. Check the console logs for error messages
4. Ensure your SQLite database is properly formatted

The application is designed to be self-contained and should work on Windows, macOS, and Linux systems with .NET 8 installed.