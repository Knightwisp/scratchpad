using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using System.Transactions;

namespace PuzzlerBankApp.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class BankAccountController : ControllerBase
{
    // Fix: Removed hardcoded dependencies in favor of Dependency Injection 
    // Properties can be passed in by startup configuration;
    // Configuration can be changed without code changes;
    // Simpler to mock dependencies for testing
    private readonly string _connectionString;
    private readonly ILogger<BankAccountController> _logger;
    private readonly string _topicArn;

    public BankAccountController(
        IConfiguration configuration,
        ILogger<BankAccountController> logger)
    {
        // Fix: Configuration from appsettings instead of hardcoded values
        _connectionString = configuration.GetConnectionString("PuzzlerPiggyBank") 
            ?? throw new ArgumentException("Missing connection string", nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topicArn = configuration["AWS:SNS:WithdrawalEventsTopic"] 
            ?? "puzzler-topic-arn";
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> WithdrawAsync([FromBody] WithdrawalRequest request)
    {
        // Fix: Added input validation
        ArgumentNullException.ThrowIfNull(request);

        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
        {
            _logger.LogWarning("Invalid withdrawal request: {Errors}", validationResults);
            return BadRequest(validationResults);
        }

        // Fix: Added transaction scope for consistency
        // The database update and event publication should be atomic 
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            // Fix: Replaced two-step operation with stored procedure
            var resultCode = await ProcessWithdrawalAsync(request);
            if (resultCode != 0)
            {
                return HandleWithdrawalError(resultCode, request.AccountId);
            }

            // Fix: Added outbox pattern for reliable event publishing
            // Async methods since the operations can happen in parallel but must fail/succeed together
            await PersistWithdrawalEventAsync(request);

            scope.Complete();
            // Fix: Added structured logging with context
            _logger.LogInformation(
                "Withdrawal successful: Amount {Amount} from account {AccountId}",
                request.Amount,
                request.AccountId);

            return Ok(new { message = "Withdrawal successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing withdrawal for account {AccountId}", request.AccountId);
            // Fix: Better error handling (http status codes with messages)
            return StatusCode(500, new { message = "An error occurred processing the withdrawal" });
        }
    }

    // Fix: Extracted method for better readability and maintenance
    // This should be in a "service class"
    private async Task<int> ProcessWithdrawalAsync(WithdrawalRequest request)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // use a stored procedure to encapsulate the data logic, 
        // take advantage of cached plans 
        // and send less I/O over the network
        await using var command = new SqlCommand("ProcessWithdrawal", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@AccountId", request.AccountId);
        command.Parameters.AddWithValue("@Amount", request.Amount);
        command.Parameters.AddWithValue("@IdempotencyKey", Guid.Parse(request.IdempotencyKey));

        var resultCodeParameter = command.Parameters.Add("@ResultCode", SqlDbType.Int);
        resultCodeParameter.Direction = ParameterDirection.Output;
        
        var errorMessageParameter = command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 255);
        errorMessageParameter.Direction = ParameterDirection.Output;

        try
        {
            await command.ExecuteNonQueryAsync();
            var resultCode = (int)resultCodeParameter.Value;
            
            if (resultCode != 0)
            {
                var errorMessage = errorMessageParameter.Value?.ToString() ?? "Unknown error";
                _logger.LogWarning("Stored procedure returned error code {Code}: {Message}", resultCode, errorMessage);
            }
            
            return resultCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ProcessWithdrawal stored procedure");
            return -1; // Database error
        }
    }

    // Fix: Implemented "outbox" pattern for async event delivery
    private async Task PersistWithdrawalEventAsync(WithdrawalRequest request)
    {
        var withdrawalEvent = new WithdrawalEvent
        {
            AccountId = request.AccountId,
            Amount = request.Amount,
            TransactionId = request.IdempotencyKey,
            Status = "SUCCESSFUL"
        };

        // This could publish to SNS et al.
        // The "outbox" pattern: notification processed by a background service that reads from a database table
        // which decouples operation and publishing
        _logger.LogInformation("Event would be published to SNS topic {TopicArn}: {@Event}", 
            _topicArn, withdrawalEvent);

        // could drop a queue or stream in here instead
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("InsertEventOutbox", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@EventType", "WITHDRAWAL");
        command.Parameters.AddWithValue("@Payload", JsonSerializer.Serialize(withdrawalEvent));
        command.Parameters.AddWithValue("@Status", "PENDING");

        var eventIdParameter = command.Parameters.Add("@EventId", SqlDbType.BigInt);
        eventIdParameter.Direction = ParameterDirection.Output;
        
        var resultCodeParameter = command.Parameters.Add("@ResultCode", SqlDbType.Int);
        resultCodeParameter.Direction = ParameterDirection.Output;
        
        var errorMessageParameter = command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 255);
        errorMessageParameter.Direction = ParameterDirection.Output;

        try
        {
            await command.ExecuteNonQueryAsync();
            var resultCode = (int)resultCodeParameter.Value;
            var eventId = (long)eventIdParameter.Value;
            
            if (resultCode == 0)
            {
                _logger.LogInformation("Event persisted to outbox with ID {EventId}", eventId);
            }
            else
            {
                var errorMessage = errorMessageParameter.Value?.ToString() ?? "Unknown error";
                _logger.LogWarning("Failed to persist event: {Message}", errorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting withdrawal event to outbox");
            throw; // Re-throw to trigger transaction rollback
        }
    }

    // Fix: Error handling with specific error messages
    private IActionResult HandleWithdrawalError(int resultCode, long accountId)
    {
        var errorMessage = resultCode switch
        {
            1 => "Insufficient funds for withdrawal",
            2 => "Account not found",
            3 => "Duplicate transaction",
            _ => "Unknown error occurred"
        };

        _logger.LogWarning("Withdrawal failed: {Error} for account {AccountId}", errorMessage, accountId);
        return BadRequest(new { message = errorMessage });
    }
}

// Fix: Using records type for typed, validated objects to pass data
public sealed record WithdrawalRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Account ID must be positive")]
    public required long AccountId { get; init; }
    
    [Range(typeof(decimal), "0.01", "1000000.00", ErrorMessage = "Amount must be between 0.01 and 1,000,000")]
    public required decimal Amount { get; init; }
    
    [Required(ErrorMessage = "Idempotency key is required")]
    [StringLength(36, MinimumLength = 36, ErrorMessage = "Idempotency key must be a valid GUID")]
    public required string IdempotencyKey { get; init; }
}

public sealed record WithdrawalEvent
{
    public required long AccountId { get; init; }
    public required decimal Amount { get; init; }
    public required string TransactionId { get; init; }
    public required string Status { get; init; }
}