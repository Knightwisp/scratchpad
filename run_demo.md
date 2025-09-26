# Piggy Bank Withdrawal Demo - Quick Start

## Prerequisites
- Docker Desktop installed and running
- No other services using ports 5000 or 1433

## Running the Demo

### Automated Script
```cmd
run-demo.bat
```

### Manual Setup
```cmd
# Start containers
docker-compose up -d

# Wait 30 seconds for database startup

# Setup database
docker-compose exec -T db /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "PiggyBank2024!" -i /docker-entrypoint-initdb.d/setupDemo_piggyBankDB.sql
```

## Testing the API

### Via Swagger UI
1. Open http://localhost:5000 in a browser
2. Generate a new GUID for each test (idempotency requirement)

### Via PowerShell
```powershell
$guid = [System.Guid]::NewGuid().ToString()
Invoke-RestMethod -Uri "http://localhost:5000/piggybank/withdraw" -Method POST -ContentType "application/json" -Body "{`"accountId`":1,`"amount`":100.00,`"idempotencyKey`":"`$guid`"}"
```

## Test Scenarios

1. **Successful withdrawal** (account 1, amount < 1000)
2. **Insufficient funds** (account 1, amount > remaining balance)
3. **Account not found** (account 999)
4. **Duplicate transaction** (use same GUID twice)
5. **Invalid input** (negative amount, invalid account ID)

## Monitoring

### View API Logs
```bash
docker-compose logs -f api
```

### Check Database
```cmd
# Connect to SQL Server
docker-compose exec db /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "PiggyBank2024!"

# View account balances
SELECT account_id, balance FROM PiggyBankDemo.dbo.accounts;

# View transactions
SELECT * FROM PiggyBankDemo.dbo.processed_transactions;

# View events
SELECT * FROM PiggyBankDemo.dbo.event_outbox;
```

## Cleanup
```cmd
docker-compose down
docker volume rm scratchpad_db_data  # Optional: removes database data
```

## Architecture Highlights Demonstrated

- ✅ **Dependency Injection**: Configuration-based setup
- ✅ **Input Validation**: DataAnnotations with custom messages  
- ✅ **Atomic Operations**: Stored procedures with proper error handling
- ✅ **Idempotency**: GUID-based duplicate prevention
- ✅ **Outbox Pattern**: Reliable event publishing simulation
- ✅ **Structured Logging**: Request correlation and error tracking
- ✅ **Transaction Management**: TransactionScope for consistency
- ✅ **Error Handling**: Proper HTTP status codes and messages