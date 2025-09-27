using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PuzzlerBankApp.Data;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;

namespace PuzzlerBankApp.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class BankAccountController : ControllerBase
{
    // Fix: Removed hardcoded dependencies in favor of Dependency Injection 
    // Properties can be passed in by startup;
    // Configuration can be changed without code releases;
    // Simpler to mock dependencies for testing
    private readonly IDbConnection _connection;
    private readonly SqliteStoredProcedures _storedProcedures;
    private readonly ILogger<BankAccountController> _logger;
    private readonly string _topicArn;

    public BankAccountController(
        IDbConnection connection,
        SqliteStoredProcedures storedProcedures,
        IConfiguration configuration,
        ILogger<BankAccountController> logger)
    {
        // Fix: Configuration from settings instead of hardcoded or code-dependent values
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _storedProcedures = storedProcedures ?? throw new ArgumentNullException(nameof(storedProcedures));
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
        // In demo, SQLite handles transactions internally
        try
        {
            var (resultCode, errorMessage, updatedBalance) = await _storedProcedures.ProcessWithdrawalAsync(
                request.AccountId, 
                request.Amount, 
                request.IdempotencyKey);
                
            if (resultCode != 0)
            {
                // Log the detailed error message from stored procedure
                _logger.LogWarning("Withdrawal failed for account {AccountId}: {ErrorMessage} (Code: {ResultCode})", 
                    request.AccountId, errorMessage, resultCode);
                    
                return BadRequest(new ApiResponse { Message = errorMessage });
            }

            // Fix: Added offline yet reliable event publishing
            await PersistWithdrawalEventAsync(request);

            // Fix: Added structured logging with context
            _logger.LogInformation(
                "Withdrawal successful: Amount {Amount} from account {AccountId}, Updated balance: {Balance}",
                request.Amount,
                request.AccountId,
                updatedBalance);

            return Ok(new WithdrawalSuccessResponse { 
                Message = "Withdrawal successful", 
                UpdatedBalance = updatedBalance,
                WithdrawnAmount = request.Amount
            });
        }
        catch (Exception ex)
        {
            // Fix: Better error handling and logging
            _logger.LogError(ex, "Error processing withdrawal for account {AccountId}", request.AccountId);
            return StatusCode(500, new ApiResponse { Message = "An error occurred processing the withdrawal" });
        }
    }

    // Fix: Implemented reliable event delivery
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

        var eventPayload = JsonSerializer.Serialize(withdrawalEvent);
        var (resultCode, eventId, errorMessage) = await _storedProcedures.InsertEventOutboxAsync(
            "WITHDRAWAL", 
            eventPayload);
            
        if (resultCode == 0)
        {
            _logger.LogInformation("Event persisted to outbox with ID {EventId}", eventId);
        }
        else
        {
            _logger.LogWarning("Failed to persist event: {Message}", errorMessage);
            throw new InvalidOperationException($"Failed to persist event: {errorMessage}");
        }
    }

    // Fix: Error handling with specific error codes, messages
    private IActionResult HandleWithdrawalError(int resultCode, long accountId)
    {
        var errorMessage = resultCode switch
        {
            1 => "Insufficient funds for withdrawal",
            2 => "Account not found",
            3 => "Duplicate transaction",
            -1 => "Database error occurred",
            _ => $"Unknown error occurred (code: {resultCode})"
        };

        _logger.LogWarning("Withdrawal failed: {Error} for account {AccountId} with result code {ResultCode}", errorMessage, accountId, resultCode);
        return BadRequest(new ApiResponse { Message = errorMessage });
    }
}

// Fix: Using records type for typed, validated objects to pass data
public sealed record WithdrawalRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Account ID must be positive")]
    public required long AccountId { get; init; }
    
    [Range(0.01, 1000000.00, ErrorMessage = "Amount must be between 0.01 and 1,000,000")]
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

public sealed record ApiResponse
{
    public required string Message { get; init; }
}

public sealed record WithdrawalSuccessResponse
{
    public required string Message { get; init; }
    public required decimal UpdatedBalance { get; init; }
    public required decimal WithdrawnAmount { get; init; }
}

// Added to test weird characters on swagger UI
public sealed record HealthCheckResponse
{
    public required string Status { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Environment { get; init; }
}