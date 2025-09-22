using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using System.Text.Json;
using System.Transactions;

// ISSUE #9: Manual JSON Handling
// Headaches:
// - Malformed JSON from special characters
// - No compile-time type safety
// - Vulnerable to injection attacks
// - Error-prone manual string formatting
//
// BEST PRACTICE #9: Use Strongly-Typed Models
// Blessings:
// - Compile-time type safety
// - Automatic serialization handling
// - Protected against injection
// - Reliable data transformation

// ISSUE #10: No Idempotency Control
// Headaches:
// - Double-spending from network retries
// - Client timeout scenarios cause duplicates
// - Financial discrepancies and reconciliation issues
// - Regulatory compliance violations
//
// BEST PRACTICE #10: Implement Idempotency Pattern
// Blessings:
// - Safe retry handling
// - Prevents duplicate transactions
// - Simplified client implementation
// - Regulatory compliance assured

// TODO - remove tab spaces; format if possible (MS standards)

namespace PuzzlerBankApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BankAccountController : ControllerBase
    {
        // ISSUE #1: Hard-coded Dependencies and Missing Dependency Injection
        // Headaches: 
        // - Untestable code (cannot mock dependencies)
        // - Environment-specific configs hardcoded
        // - Violates SOLID principles
        // - Difficult to change implementations
        // - Security risk from exposed credentials in code
        private readonly string _connectionString = "Server=localhost;Database=BankDB;User Id=sa;Password=YourStrong!Password;TrustServerCertificate=true;";
        private readonly IAmazonSimpleNotificationService _snsClient = new AmazonSimpleNotificationServiceClient();

        // BEST PRACTICE #1: Implement Constructor Dependency Injection
        // Blessings:
        // - Configuration flexibility through appsettings.json
        // - Easy unit testing with mocked dependencies
        // - Loose coupling for better maintainability
        // - Secure credential management via configuration
        /*
        private readonly string _connectionString;
        private readonly IAmazonSimpleNotificationService _snsClient;

        public BankAccountController(IConfiguration configuration, IAmazonSimpleNotificationService snsClient)
        {
            _connectionString = configuration.GetConnectionString("PuzzlerPiggyBank");
            _snsClient = snsClient;
        }
        */

        [HttpPost("withdraw")]
        public string Withdraw(long accountId, decimal amount)
        {
            // ISSUE #7: Business Logic in Controller
            // Headaches:
            // - Violates single responsibility principle
            // - Business logic cannot be reused
            // - Testing requires HTTP context setup
            // - Tight coupling reduces maintainability
            //
            // BEST PRACTICE #7: Implement Service Layer Architecture
            // Blessings:
            // - Clear separation of concerns
            // - Reusable business logic
            // - Simplified testing without HTTP context
            // - Improved maintainability

            // ISSUE #8: Missing Input Validation
            // Headaches:
            // - Negative withdrawal amounts cause logic errors
            // - Invalid account IDs trigger database exceptions
            // - No protection against malicious input
            // - Runtime crashes from unexpected data
            //
            // BEST PRACTICE #8: Implement Model Validation
            // Blessings:
            // - Automatic request validation
            // - Clear error messages for clients
            // - Protection against invalid data
            // - Reduced error handling complexity

            // ISSUE #3: Race-Prone Two-Step Database Operation
            // Headaches:
            // - Account overdrafts from concurrent withdrawals
            // - Data inconsistency under load
            // - Financial losses and compliance issues
            // - Customer trust erosion
            // - Multiple database roundtrips impact performance
            //
            // BEST PRACTICE #3: Implement Single Atomic UPDATE
            // Blessings:
            // - Race conditions eliminated through atomic operation
            // - Guaranteed consistent account balance
            // - Improved performance with single DB operation
            // - Simplified error handling and recovery
            decimal currentBalance;
            var getBalanceSql = "SELECT balance FROM accounts WHERE id = @accountId";

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(getBalanceSql, connection))
                {
                    command.Parameters.AddWithValue("@accountId", accountId);
                    // Potential NullReferenceException if account doesn't exist
                    currentBalance = (decimal)command.ExecuteScalar();
                }
            }

            // ISSUE #2: Missing Transaction Scope
            // Headaches:
            // - Lost events if SNS publish fails
            // - Partial state changes (money deducted, no notification)
            // - Inconsistent system state across services
            // - Difficult error recovery and reconciliation
            //
            // BEST PRACTICE #2: Implement TransactionScope
            // Blessings:
            // - Guaranteed atomic operations across all resources
            // - Automatic rollback on any operation failure
            // - System remains consistent in failure scenarios
            // - Simplified error recovery and debugging
            if (currentBalance >= amount)
            {
                var updateSql = "UPDATE accounts SET balance = balance - @amount WHERE id = @accountId";
                int rowsAffected;

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(updateSql, connection))
                    {
                        command.Parameters.AddWithValue("@amount", amount);
                        command.Parameters.AddWithValue("@accountId", accountId);
                        rowsAffected = command.ExecuteNonQuery();
                    }
                }

                if (rowsAffected > 0)
                {
                    // ISSUE #4: Non-Atomic Event Publishing
                    // Headaches:
                    // - Event publishing failures lead to inconsistent system state
                    // - No retry mechanism for failed notifications
                    // - Missing audit trail for notification attempts
                    // - Difficult to track notification status
                    //
                    // BEST PRACTICE #4: Implement Outbox Pattern
                    // Blessings:
                    // - Guaranteed event delivery through persistence
                    // - Automatic retry mechanism for failed notifications
                    // - Complete audit trail of all notifications
                    // - System remains consistent during failures
                    var withdrawalEvent = new WithdrawalEvent
                    {
                        Amount = amount,
                        AccountId = accountId,
                        Status = "SUCCESSFUL"
                    };

                    // Hard-coded ARN is also a configuration issue
                    var topicArn = "arn:aws:sns:us-west-2:123456789012:WithdrawalEvents";
                    var publishRequest = new PublishRequest
                    {
                        TopicArn = topicArn,
                        Message = JsonSerializer.Serialize(withdrawalEvent)
                    };

                    // ISSUE #5: Fire-and-Forget Event Publishing
                    // Headaches:
                    // - Silent event loss during AWS outages
                    // - Downstream systems miss critical notifications
                    // - No visibility into messaging failures
                    // - Cascading system inconsistencies
                    //
                    // BEST PRACTICE #5: Implement Resilient Event Publishing
                    // Blessings:
                    // - Reliable message delivery with retries
                    // - Circuit breaker prevents cascading failures
                    // - Visibility into messaging health
                    // - Graceful handling of AWS outages
                    _snsClient.PublishAsync(publishRequest); // Fire and forget? Exception risk.

                    // ISSUE #6: Missing Observability
                    // Headaches:
                    // - Impossible to debug production issues
                    // - No performance monitoring or SLA tracking
                    // - Cannot trace requests across services
                    // - Blind to system health and bottlenecks
                    //
                    // BEST PRACTICE #6: Implement Structured Logging & Telemetry
                    // Blessings:
                    // - Comprehensive request tracing
                    // - Performance metrics and SLA monitoring
                    // - Quick problem identification
                    // - Data-driven optimization
                    return "Withdrawal successful";
                }
                else
                {
                    return "Withdrawal failed";
                }
            }
            else
            {
                return "Insufficient funds for withdrawal";
            }
        }
    }

    public class WithdrawalEvent
    {
        public decimal Amount { get; set; }
        public long AccountId { get; set; }
        public string Status { get; set; }
    }
}

/*
SUMMARY OF KEY BEST PRACTICE CORRECTIONS:

1.  **Dependency Injection:** Inject `IConfiguration` and `IAmazonSimpleNotificationService` via the constructor.
2.  **Service Layer:** Move business logic out of the controller and into a dedicated service class (e.g., `IAccountService.WithdrawAsync`).
3.  **Atomic Update:** Replace the SELECT/UPDATE with a single UPDATE statement: `UPDATE accounts SET balance = balance - @amount WHERE id = @accountId AND balance >= @amount`.
4.  **Transactions:** Wrap the entire unit of work (database operation + event persistence for the Outbox pattern) in a `TransactionScope`.
5.  **Outbox Pattern:** Instead of publishing directly to SNS, insert the event into an `Outbox` table in the same database transaction. A separate worker process then publishes them.
6.  **Async/Await:** Make the controller method `async Task<IActionResult>` and `await` all asynchronous calls.
7.  **Logging:** Inject `ILogger<BankAccountController>` and add informative log messages for successes, failures, and exceptions.
8.  **Validation:** Create a request model with Data Annotations or use FluentValidation to validate input before it reaches business logic.
*/