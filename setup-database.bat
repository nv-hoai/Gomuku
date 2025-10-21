@echo off
title Database Setup - Gomoku Game
echo ================================
echo  Database Migration Setup
echo ================================
echo Setting up Entity Framework migrations...
echo.

cd /d "%~dp0MainServer"

echo Step 1: Installing EF Core CLI tools...
dotnet tool install --global dotnet-ef --skip-existing
echo.

echo Step 2: Creating initial migration...
dotnet ef migrations add InitialCreate --project ../SharedLib --context GomokuDbContext
if errorlevel 1 (
    echo ERROR: Migration creation failed!
    pause
    exit /b 1
)

echo.
echo Step 3: Creating/updating database...
dotnet ef database update --project ../SharedLib --context GomokuDbContext
if errorlevel 1 (
    echo ERROR: Database update failed!
    echo Please check your connection string in appsettings.json
    pause
    exit /b 1
)

echo.
echo ================================
echo    DATABASE SETUP COMPLETE!
echo ================================
echo Database created successfully with:
echo - Users table (with BCrypt + Salt authentication)
echo - PlayerProfiles table (game stats and ELO)
echo - GameHistory table (match records)
echo - Default admin user: admin/admin123
echo.
echo You can now start the servers!
pause