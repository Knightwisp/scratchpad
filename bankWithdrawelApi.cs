using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using System.Text.Json;
using System.Transactions; // For TransactionScope

namespace PoorBankApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BankAccountController : ControllerBase
    {
        // ISSUE 1: Hard-coded dependencies and no Dependency Injection (DI)
        // - Tightly coupled, impossible to mock for unit tests, difficult to change implementations.
        // BEST PRACTICE: Use Constructor Injection. This makes the class testable and configurable.
        private readonly string _connectionString = "Server=localhost;Database=BankDB;User Id=sa;Password=YourStrong!Password;TrustServerCertificate=true;";
        private readonly IAmazonSimpleNotificationService _snsClient = new AmazonSimpleNotificationServiceClient();

        /*
        CORRECTED APPROACH FOR ISSUE 1:
        private readonly string _connectionString;
        private readonly IAmazonSimpleNotificationService _snsClient;

        public BankAccountController(IConfiguration configuration, IAmazonSimpleNotificationService snsClient)
        {
            _connectionString = configuration.GetConnectionString("BankDB");
            _snsClient = snsClient;
        }
        */

        [HttpPost("withdraw")]
        public string Withdraw(long accountId, decimal amount)
        {
            // ISSUE 8: No input validation or model binding.
            // - Negative amounts or invalid account IDs will cause runtime errors or logical bugs.
            // BEST PRACTICE: Use Data Annotations for automatic model validation or FluentValidation.
            // The [ApiController] attribute automatically returns a 400 Bad Request if validation fails.

            // ISSUE 7: Business logic in Controller.
            // - Controllers should be thin coordinators. This makes logic hard to reuse and test.
            // BEST PRACTICE: Use the Mediator pattern (e.g., MediatR) or a dedicated Service Layer.
            // The controller's job is to delegate to a command handler or service.

            // ISSUE 3: Inefficient two-step database operation prone to race conditions.
            // - The balance is checked and then updated in two separate commands. The balance could change between them.
            // BEST PRACTICE: Use a single, atomic UPDATE statement that checks the condition.
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

            // ISSUE 2: No transaction scope; operations are not atomic.
            // - A failure after the UPDATE but before the SNS publish leaves the system inconsistent.
            // BEST PRACTICE: Use a TransactionScope to wrap the entire unit of work (DB ops + event publish).
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
                    // ISSUE 4: Non-atomic operation; event published after DB commit.
                    // - If publish fails, the withdrawal is not recorded in other systems.
                    // BEST PRACTICE: Use the Outbox Pattern or a resilient workflow (e.g., a durable queue).
                    // Publish the event WITHIN the same database transaction before committing.
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

                    // ISSUE 5: No async/await. Potential fire-and-forget and unobserved exceptions.
                    // BEST PRACTICE: Always await asynchronous calls and make the action method async.
                    // Use cancellation tokens for robustness.
                    _snsClient.PublishAsync(publishRequest); // Fire and forget? Exception risk.

                    // ISSUE 6: No structured logging or observability.
                    // - Impossible to debug, trace, or monitor in production.
                    // BEST PRACTICE: Inject an ILogger and log key events, errors, and warnings.
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