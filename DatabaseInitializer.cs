using Microsoft.Data.Sqlite;
using System.Data;

namespace PuzzlerBankApp.Data;

/// <summary>
/// Handles SQLite database initialization using separate SQL files
/// </summary>
public class DatabaseInitializer
{
    private readonly IDbConnection _connection;

    public DatabaseInitializer(IDbConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Initialize the SQLite database with schema, indexes, and seed data
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var sqliteConnection = (SqliteConnection)_connection;
            if (sqliteConnection.State != ConnectionState.Open)
                await sqliteConnection.OpenAsync();

            Console.WriteLine($"Database connection string: {sqliteConnection.ConnectionString}");
            Console.WriteLine("Executing SQL files...");

            // Execute SQL files in order
            await ExecuteSqlFileAsync(sqliteConnection, "sql/schema.sql");
            Console.WriteLine("✅ Schema created");
            
            await ExecuteSqlFileAsync(sqliteConnection, "sql/indexes.sql");
            Console.WriteLine("✅ Indexes created");
            
            await ExecuteSqlFileAsync(sqliteConnection, "sql/seed_data.sql");
            Console.WriteLine("✅ Seed data inserted");
            
            // Verify tables were created
            var tablesQuery = "SELECT name FROM sqlite_master WHERE type='table'";
            using var cmd = new SqliteCommand(tablesQuery, sqliteConnection);
            using var reader = await cmd.ExecuteReaderAsync();
            Console.WriteLine("Tables created:");
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"  - {reader.GetString(0)}");
            }
            
            Console.WriteLine("SQLite database initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database initialization failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Execute SQL commands from a file
    /// </summary>
    /// <param name="connection">Open SQLite connection</param>
    /// <param name="filePath">Path to SQL file</param>
    private static async Task ExecuteSqlFileAsync(SqliteConnection connection, string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"SQL file not found: {filePath}");
        }

        var sql = await File.ReadAllTextAsync(filePath);
        Console.WriteLine($"Executing SQL file: {filePath}");
        
        // Clean up the SQL by removing comment-only lines first
        var lines = sql.Split('\n');
        var cleanedLines = lines
            .Where(line => !line.Trim().StartsWith("--") && !string.IsNullOrWhiteSpace(line.Trim()))
            .ToArray();
        var cleanedSql = string.Join('\n', cleanedLines);
        
        // Split by semicolons and execute each statement
        var commands = cleanedSql.Split(';', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var commandText in commands)
        {
            var trimmedCommand = commandText.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedCommand))
            {
                Console.WriteLine($"Executing command: {trimmedCommand.Substring(0, Math.Min(50, trimmedCommand.Length))}...");
                try 
                {
                    using var command = new SqliteCommand(trimmedCommand, connection);
                    var result = await command.ExecuteNonQueryAsync();
                    Console.WriteLine($"Command executed successfully, rows affected: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing SQL command: {ex.Message}");
                    Console.WriteLine($"Command was: {trimmedCommand}");
                    throw;
                }
            }
        }
    }
}