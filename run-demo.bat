@echo off
REM run-demo.bat - Demo startup script

echo Starting Piggy Bank Withdrawal Demo...

REM Check if Docker is running
docker info >nul 2>&1
if errorlevel 1 (
    echo Docker is not running. Please start Docker Desktop and try again.
    pause
    exit /b 1
)

echo Starting containers...
docker-compose up -d

echo Waiting for database to be ready...
timeout /t 30 /nobreak >nul

REM Run database setup
echo Setting up database...
docker-compose exec -T db /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "PiggyBank2024!" -i /docker-entrypoint-initdb.d/setupDemo_piggyBankDB.sql

echo Try the API: http://localhost:5000
echo Database: localhost:1433 (sa/PiggyBank2024!)
echo.
echo Logs: docker-compose logs -f api
echo Stop: docker-compose down
pause