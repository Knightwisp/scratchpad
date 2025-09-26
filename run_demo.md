# Piggy Bank Withdrawal Demo
A simple way to exercise [`bankWithdrawelApi.cs`](./bankWithdrawelApi.cs).

### PowerShell Script
```cmd
run-demo.bat
```

This will:
1. Build the .NET 7.0 project
2. Initialize SQLite database with demo schemas and seed data
3. Start the API server on http://localhost:5000

## Browse the API 
1. Open http://localhost:5000
2. Browse Swagger documentation
3. Health check: http://localhost:5000/health

### Via PowerShell
- Execute or emulate script: [`testWithdraw.ps1`](./testWithdraw.ps1).

## Stop the Demo
`Ctrl+C` to stop the server.  
Note `piggybank.db` persists for subsequent runs and can be trashed.
