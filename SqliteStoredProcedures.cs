using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Data;

namespace PuzzlerBankApp.Data;

/// <summary>
/// SQLite implementations of stored procedure equivalents
/// Preserves SQL Server logic and patterns
/// </summary>
public class SqliteStoredProcedures
{
    private readonly IDbConnection _connection;

    public SqliteStoredProcedures(IDbConnection connection)
    {
        _connection = connection;
    }
    /// <summary>
    /// Process withdrawal with atomic transaction and business logic validation
    /// Equivalent to SQL Server ProcessWithdrawal stored procedure
    /// </summary>
    /// <param name="accountId">Account to withdraw from</param>
    /// <param name="amount">Amount to withdraw</param>
    /// <param name="idempotencyKey">Unique key to prevent duplicates</param>
    /// <returns>Result code, error message, and updated balance</returns>
    public async Task<(int ResultCode, string ErrorMessage, decimal UpdatedBalance)> ProcessWithdrawalAsync(
        long accountId, 
        decimal amount, 
        string idempotencyKey)
    {
        var sqliteConnection = (SqliteConnection)_connection;
        if (sqliteConnection.State != ConnectionState.Open)
            await sqliteConnection.OpenAsync();

        using var transaction = sqliteConnection.BeginTransaction();
        try
        {
            // Check for duplicate transaction (idempotency)
            var checkDuplicateQuery = "SELECT COUNT(*) FROM processed_transactions WHERE idempotency_key = @IdempotencyKey";
            using var checkCmd = new SqliteCommand(checkDuplicateQuery, sqliteConnection, transaction);
            checkCmd.Parameters.AddWithValue("@IdempotencyKey", idempotencyKey);
            
            var duplicateCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
            if (duplicateCount > 0)
            {
                return (3, "Duplicate transaction detected", 0);
            }

            // Validate account exists
            var checkAccountQuery = "SELECT COUNT(*) FROM accounts WHERE account_id = @AccountId";
            using var accountCmd = new SqliteCommand(checkAccountQuery, sqliteConnection, transaction);
            accountCmd.Parameters.AddWithValue("@AccountId", accountId);
            
            var accountExists = Convert.ToInt64(await accountCmd.ExecuteScalarAsync()) > 0;
            if (!accountExists)
            {
                return (2, "Account not found", 0);
            }

            // Atomic withdrawal operation - equivalent to SQL Server UPDATE with WHERE conditions
            var withdrawalQuery = @"
                UPDATE accounts 
                SET balance = balance - @Amount,
                    updated_date = datetime('now')
                WHERE account_id = @AccountId 
                AND balance >= @Amount";
            
            using var withdrawCmd = new SqliteCommand(withdrawalQuery, sqliteConnection, transaction);
            withdrawCmd.Parameters.AddWithValue("@Amount", amount);
            withdrawCmd.Parameters.AddWithValue("@AccountId", accountId);
            
            var rowsAffected = await withdrawCmd.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
            {
                return (1, "Insufficient funds for withdrawal", 0);
            }

            // Record successful transaction for idempotency tracking
            var recordTransactionQuery = @"
                INSERT INTO processed_transactions (idempotency_key, account_id, amount)
                VALUES (@IdempotencyKey, @AccountId, @Amount)";
            
            using var recordCmd = new SqliteCommand(recordTransactionQuery, sqliteConnection, transaction);
            recordCmd.Parameters.AddWithValue("@IdempotencyKey", idempotencyKey);
            recordCmd.Parameters.AddWithValue("@AccountId", accountId);
            recordCmd.Parameters.AddWithValue("@Amount", (double)amount);
            
            await recordCmd.ExecuteNonQueryAsync();

            // Get the updated balance
            var balanceQuery = "SELECT balance FROM accounts WHERE account_id = @AccountId";
            using var balanceCmd = new SqliteCommand(balanceQuery, sqliteConnection, transaction);
            balanceCmd.Parameters.AddWithValue("@AccountId", accountId);
            var updatedBalance = Convert.ToDecimal(await balanceCmd.ExecuteScalarAsync());

            await transaction.CommitAsync();
            return (0, "Withdrawal processed successfully", updatedBalance);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (-1, $"Database error: {ex.Message}", 0);
        }
    }

    /// <summary>
    /// Insert event into outbox table for reliable event publishing
    /// Equivalent to SQL Server InsertEventOutbox stored procedure
    /// </summary>
    /// <param name="eventType">Type of event (e.g., WITHDRAWAL)</param>
    /// <param name="payload">JSON payload of the event</param>
    /// <param name="status">Event status (default: PENDING)</param>
    /// <returns>Result code, event ID, and error message</returns>
    public async Task<(int ResultCode, long EventId, string ErrorMessage)> InsertEventOutboxAsync(
        string eventType,
        string payload,
        string status = "PENDING")
    {
        try
        {
            var sqliteConnection = (SqliteConnection)_connection;
            if (sqliteConnection.State != ConnectionState.Open)
                await sqliteConnection.OpenAsync();

            // Validate inputs (preserving SQL Server validation logic)
            if (string.IsNullOrEmpty(eventType))
                return (1, 0, "EventType cannot be null or empty");
            
            if (string.IsNullOrEmpty(payload))
                return (2, 0, "Payload cannot be null or empty");

            var insertQuery = @"
                INSERT INTO event_outbox (event_type, payload, status)
                VALUES (@EventType, @Payload, @Status)";
            
            using var command = new SqliteCommand(insertQuery, sqliteConnection);
            command.Parameters.AddWithValue("@EventType", eventType);
            command.Parameters.AddWithValue("@Payload", payload);
            command.Parameters.AddWithValue("@Status", status);
            
            await command.ExecuteNonQueryAsync();
            
            // Get the last inserted row ID (equivalent to SQL Server SCOPE_IDENTITY())
            var getIdQuery = "SELECT last_insert_rowid()";
            using var idCommand = new SqliteCommand(getIdQuery, sqliteConnection);
            var eventId = Convert.ToInt64(await idCommand.ExecuteScalarAsync());
            
            return (0, eventId, "Event inserted successfully");
        }
        catch (Exception ex)
        {
            return (-1, 0, $"Database error: {ex.Message}");
        }
    }
}