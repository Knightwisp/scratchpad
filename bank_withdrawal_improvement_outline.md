# Bank Account Withdrawal Code Improvement Analysis

## Issues and the Enhancements

| Issue | Problem | Suggested Enhancement | Design Aspect Category | Rationale |
|-------|---------|-------------------|----------------------|-----------|
| Hard-coded Dependencies | Direct instantiation of `AmazonSimpleNotificationServiceClient()` | Constructor injection with `IConfiguration` and `IAmazonSimpleNotificationService` | Dependency Management | • Untestable code (cannot mock dependencies)<br>• Environment-specific configs hardcoded<br>• Violates SOLID principles<br>• Difficult to change implementations |
| Race Condition in Balance Check | Separate SELECT then UPDATE operations allow concurrent modifications | Single atomic UPDATE with optimistic concurrency control using RowVersion | Concurrency Control | • Account overdrafts from concurrent withdrawals<br>• Data inconsistency under load<br>• Financial losses and compliance issues<br>• Customer trust erosion |
| No Transaction Management | Database update and event publishing occur separately without atomicity | Implement TransactionScope with Outbox Pattern for eventual consistency | Data Consistency | • Lost events if SNS publish fails<br>• Partial state changes (money deducted, no notification)<br>• Inconsistent system state across services<br>• Difficult error recovery and reconciliation |
| Basic Error Handling | Generic string returns provide no structured error information | Global exception handler with typed exceptions and structured error responses | Error Management | • Poor user experience with unclear errors<br>• No structured logging for debugging<br>• HTTP status codes always 200<br>• Difficult troubleshooting in production |
| No Input Validation | Method accepts parameters without validation constraints | Data Annotations with automatic model validation via `[ApiController]` | Data Validation | • Negative withdrawal amounts cause logic errors<br>• Invalid account IDs trigger database exceptions<br>• No protection against malicious input<br>• Runtime crashes from unexpected data |
| Fire-and-Forget Event Publishing | SNS publishing ignores failures and provides no retry mechanism | Polly retry policies with circuit breaker pattern for resilient messaging | Reliability | • Silent event loss during AWS outages<br>• Downstream systems miss critical notifications<br>• No visibility into messaging failures<br>• Cascading system inconsistencies |
| No Observability | Missing logging, metrics, and distributed tracing capabilities | Structured logging with OpenTelemetry Activity tracing for monitoring | Observability | • Impossible to debug production issues<br>• No performance monitoring or SLA tracking<br>• Cannot trace requests across services<br>• Blind to system health and bottlenecks |
| Business Logic in Controller | Controller handles HTTP concerns and business rules simultaneously | Service layer architecture with dependency injection separation | Architecture | • Violates single responsibility principle<br>• Business logic cannot be reused<br>• Testing requires HTTP context setup<br>• Tight coupling reduces maintainability |
| Manual JSON Handling | String concatenation for JSON serialization without error handling | Strongly-typed models with System.Text.Json automatic serialization | Data Serialization | • Malformed JSON from special characters<br>• No compile-time type safety<br>• Vulnerable to injection attacks<br>• Error-prone manual string formatting |
| No Idempotency | Duplicate requests could cause multiple withdrawals from same account | Custom action filter implementing idempotency key validation | Request Handling | • Double-spending from network retries<br>• Client timeout scenarios cause duplicates<br>• Financial discrepancies and reconciliation issues<br>• Regulatory compliance violations |

---

## Code Examples

<details>
<summary>Dependency Injection Implementation</summary>

```csharp
// Program.cs
services.AddScoped<IAmazonSimpleNotificationService>(provider => 
    new AmazonSimpleNotificationServiceClient());
services.Configure<AwsSettings>(configuration.GetSection("Aws"));

// Controller
public BankAccountController(IAmazonSimpleNotificationService snsClient, IConfiguration config)
{
    _snsClient = snsClient;
    _config = config;
}
```
</details>

<details>
<summary>Atomic Database Operations</summary>

```csharp
// Single atomic operation
var sql = @"UPDATE accounts 
           SET balance = balance - @amount, RowVersion = RowVersion + 1
           WHERE id = @accountId 
           AND balance >= @amount 
           AND RowVersion = @currentVersion";

var rowsAffected = await command.ExecuteNonQueryAsync();
if (rowsAffected == 0) throw new OptimisticConcurrencyException();
```
</details>

<details>
<summary>Transaction Management with Outbox Pattern</summary>

```csharp
using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

// Update account balance
await _accountRepository.WithdrawAsync(accountId, amount);

// Store event for later processing
await _outboxRepository.SaveAsync(new OutboxEvent 
{
    EventType = "WithdrawalCompleted",
    Data = JsonSerializer.Serialize(new WithdrawalEvent(accountId, amount))
});

scope.Complete();
```
</details>

<details>
<summary>Resilient Event Publishing</summary>

```csharp
private readonly IAsyncPolicy _retryPolicy = Policy
    .Handle<AmazonServiceException>()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

public async Task PublishWithRetryAsync(PublishRequest request)
{
    await _retryPolicy.ExecuteAsync(() => _snsClient.PublishAsync(request));
}
```
</details>

<details>
<summary>Structured Logging & Observability</summary>

```csharp
[HttpPost("withdraw")]
public async Task<IActionResult> Withdraw([FromBody] WithdrawalRequest request)
{
    using var activity = _activitySource.StartActivity("BankWithdrawal");
    activity?.SetTag("account.id", request.AccountId);
    
    _logger.LogInformation("Processing withdrawal for account {AccountId} amount {Amount}", 
        request.AccountId, request.Amount);
        
    // Business logic...
}
```
</details>

---
