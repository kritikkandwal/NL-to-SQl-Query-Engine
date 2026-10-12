using NlpToSqlApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "NLP to SQL API", 
        Version = "v1",
        Description = "Convert natural language queries to SQL and execute them against databases"
    });
});

// Add CORS for frontend applications
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddSingleton<INaturalLanguageSqlBuilder, NaturalLanguageSqlBuilder>();
builder.Services.AddScoped<INaturalLanguageQueryService, NaturalLanguageQueryService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseDefaultFiles(); // Serve wwwroot/index.html at "/"
app.UseStaticFiles(); // Enable static file serving

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NLP to SQL API v1");
        c.RoutePrefix = "swagger"; // Move Swagger UI to /swagger
    });
}

app.UseCors("AllowAll");
app.UseRouting();
app.MapControllers();

// Add a health check endpoint
app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });

// API information endpoint (the root "/" now serves the UI from wwwroot/index.html)
app.MapGet("/api", () => new
{ 
    name = "NLP to SQL API",
    version = "1.0.0",
    description = "Convert natural language queries to SQL and execute them against databases",
    endpoints = new
    {
        query = "/api/query/process",
        schema = "/api/query/schema",
        examples = "/api/query/examples",
        swagger = "/swagger"
    }
});

// Configure to listen on all addresses and port 5000
app.Urls.Add("http://0.0.0.0:5000");

app.Run();