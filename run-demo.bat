@echo off
echo Setting up Piggy Bank Withdrawal API Demo...
echo.

REM Build the project
echo Building the API...
dotnet build

if errorlevel 1 (
    echo Build failed. Please check the error messages above.
    pause
    exit /b 1
)

echo.
echo Build complete.
echo.
echo Starting the API...
echo.
echo The API will be available at: http://localhost:5000
echo.
echo Press Ctrl+C to stop the server
echo.

dotnet run --urls "http://localhost:5000"