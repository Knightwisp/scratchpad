-- Database setup for Piggy Bank Withdrawal Demo

-- IGNORED FOR DEMO --
-- Script should be idempotent but since demo in container will run fresh each time
-- Object names should have naming scheme, probably not plural table names, for example
-- better error handling, for example, not xact abort on but expert transaction plus error handling
-- Conscious indexing and other constraints over just "INDEX"
-- IGNORED FOR DEMO --


CREATE DATABASE PiggyBankDemo;
GO
USE PiggyBankDemo;
GO

-- Accounts table with improved naming convention
CREATE TABLE accounts (
    account_id BIGINT IDENTITY(1,1) PRIMARY KEY,
    balance DECIMAL(18,2) NOT NULL CHECK (balance >= 0),
    created_date DATETIME2 DEFAULT SYSUTCDATETIME(),
    updated_date DATETIME2 DEFAULT SYSUTCDATETIME()
);

-- Processed transactions with GUID idempotency keys
CREATE TABLE processed_transactions (
    processed_transaction_id BIGINT IDENTITY(1,1) PRIMARY KEY,
    idempotency_key UNIQUEIDENTIFIER NOT NULL UNIQUE,
    account_id BIGINT NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    transaction_date DATETIME2 DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (account_id) REFERENCES accounts(account_id)
);

-- Event outbox table with improved naming
CREATE TABLE event_outbox (
    event_outbox_id BIGINT IDENTITY(1,1) PRIMARY KEY,
    event_type NVARCHAR(50) NOT NULL,
    payload NVARCHAR(MAX) NOT NULL,
    status NVARCHAR(20) NOT NULL DEFAULT 'PENDING',
    created_date DATETIME2 DEFAULT SYSUTCDATETIME(),
    processed_date DATETIME2 NULL
);

GO

-- Enhanced ProcessWithdrawal stored procedure with error handling
CREATE PROCEDURE ProcessWithdrawal
    @AccountId BIGINT,
    @Amount DECIMAL(18,2),
    @IdempotencyKey UNIQUEIDENTIFIER,
    @ResultCode INT OUTPUT,
    @ErrorMessage NVARCHAR(255) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    
    BEGIN TRY
        -- Initialize output parameters
        SET @ResultCode = 0;
        SET @ErrorMessage = '';
        
        -- Check for duplicate transaction (idempotency)
        IF EXISTS (SELECT 1 FROM processed_transactions WHERE idempotency_key = @IdempotencyKey)
        BEGIN
            SET @ResultCode = 3;
            SET @ErrorMessage = 'Duplicate transaction detected';
            RETURN;
        END
        
        -- Validate account exists
        IF NOT EXISTS (SELECT 1 FROM accounts WHERE account_id = @AccountId)
        BEGIN
            SET @ResultCode = 2;
            SET @ErrorMessage = 'Account not found';
            RETURN;
        END
        
        -- Atomic withdrawal operation with optimistic concurrency
        DECLARE @PreviousBalance DECIMAL(18,2);
        
        UPDATE accounts 
        SET balance = balance - @Amount,
            updated_date = SYSUTCDATETIME(),
            @PreviousBalance = balance
        WHERE account_id = @AccountId 
        AND balance >= @Amount;
        
        -- Check if withdrawal was successful
        IF @@ROWCOUNT = 0
        BEGIN
            SET @ResultCode = 1;
            SET @ErrorMessage = 'Insufficient funds for withdrawal';
            RETURN;
        END
        
        -- Record successful transaction for idempotency tracking
        INSERT INTO processed_transactions (idempotency_key, account_id, amount)
        VALUES (@IdempotencyKey, @AccountId, @Amount);
        
        SET @ResultCode = 0;
        SET @ErrorMessage = 'Withdrawal processed successfully';
        
    END TRY
    BEGIN CATCH
        SET @ResultCode = -1;
        SET @ErrorMessage = 'Database error: ' + ERROR_MESSAGE();
        
        -- Log the error for debugging
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

-- Enhanced InsertEventOutbox stored procedure with error handling
CREATE PROCEDURE InsertEventOutbox
    @EventType NVARCHAR(50),
    @Payload NVARCHAR(MAX),
    @Status NVARCHAR(20) = 'PENDING',
    @EventId BIGINT OUTPUT,
    @ResultCode INT OUTPUT,
    @ErrorMessage NVARCHAR(255) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Initialize output parameters
        SET @ResultCode = 0;
        SET @ErrorMessage = '';
        SET @EventId = 0;
        
        -- Validate inputs
        IF @EventType IS NULL OR LEN(@EventType) = 0
        BEGIN
            SET @ResultCode = 1;
            SET @ErrorMessage = 'EventType cannot be null or empty';
            RETURN;
        END
        
        IF @Payload IS NULL OR LEN(@Payload) = 0
        BEGIN
            SET @ResultCode = 2;
            SET @ErrorMessage = 'Payload cannot be null or empty';
            RETURN;
        END
        
        -- Insert event into outbox
        INSERT INTO event_outbox (event_type, payload, status)
        VALUES (@EventType, @Payload, @Status);
        
        SET @EventId = SCOPE_IDENTITY();
        SET @ErrorMessage = 'Event inserted successfully';
        
    END TRY
    BEGIN CATCH
        SET @ResultCode = -1;
        SET @ErrorMessage = 'Database error: ' + ERROR_MESSAGE();
        
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

-- Insert test data with proper account_id references
INSERT INTO accounts (balance) VALUES 
    (1000.00),  -- account_id will be 1
    (500.00),   -- account_id will be 2
    (2500.00);  -- account_id will be 3

GO

-- Create indexes for better performance
CREATE INDEX IX_processed_transactions_idempotency_key ON processed_transactions(idempotency_key);
CREATE INDEX IX_processed_transactions_account_id ON processed_transactions(account_id);
CREATE INDEX IX_event_outbox_status ON event_outbox(status);
CREATE INDEX IX_event_outbox_created_date ON event_outbox(created_date);

GO

-- Verification queries
SELECT 'Database setup completed successfully' AS Status;
SELECT 'Accounts created:', COUNT(*) AS AccountCount FROM accounts;
SELECT account_id, balance FROM accounts;