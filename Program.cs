using PuzzlerBankApp.Data;
using PuzzlerBankApp.Controllers;
using Microsoft.Data.Sqlite;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddLogging();

// SQLite connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=piggybank.db";
builder.Services.AddScoped<IDbConnection>(_ => new SqliteConnection(connectionString));

// Register services
builder.Services.AddScoped<SqliteStoredProcedures>();

// Add API Explorer and Swagger for demo purposes
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Piggy Bank Withdrawal API Demo", 
        Version = "v1",
        Description = "Demonstrates improved bank withdrawal implementation with best practices"
    });
});

var app = builder.Build();

// Initialize database using the same connection configuration as the services
using (var scope = app.Services.CreateScope())
{
    var connection = scope.ServiceProvider.GetRequiredService<IDbConnection>();
    var dbInitializer = new DatabaseInitializer(connection);
    await dbInitializer.InitializeAsync();
}

// Configure the HTTP request pipeline - Enable Swagger for demo purposes
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Piggy Bank Withdrawal API Demo V1");
    c.RoutePrefix = ""; // Set Swagger UI at the root
});

app.UseRouting();
app.MapControllers();

// Add a simple health check endpoint
app.MapGet("/health", () => new HealthCheckResponse { 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Environment = app.Environment.EnvironmentName 
});

app.Run();