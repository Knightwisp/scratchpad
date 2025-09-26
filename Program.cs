var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddLogging();

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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Piggy Bank Withdrawal API Demo V1");
        c.RoutePrefix = ""; // Set Swagger UI at the root
    });
}

app.UseRouting();
app.MapControllers();

// Add a simple health check endpoint
app.MapGet("/health", () => new { 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Environment = app.Environment.EnvironmentName 
});

app.Run();