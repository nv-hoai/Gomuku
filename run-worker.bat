@echo off
cls
title WorkerServer - Gomoku AI Worker

echo ================================
echo       Gomoku AI Worker Server
echo ================================
echo 1. Connect to localhost:5001
echo 2. Enter custom IP and port
echo ================================
set /p choice=Select an option [1-2]: 

if "%choice%"=="1" (
    set IP=localhost
    set PORT=5001
) else if "%choice%"=="2" (
    set /p IP=Enter IP address: 
    set /p PORT=Enter port: 
) else (
    echo.
    echo  Invalid choice!
    pause
    exit /b
)

cls
echo ================================
echo       Gomoku AI Worker Server
echo ================================
echo - Connecting to MainServer at %IP%:%PORT%
echo - Handles AI calculations and move validation
echo - Press Ctrl+C to stop
echo ================================
echo.

cd /d "%~dp0WorkerServer"
dotnet run -- "%IP%" "%PORT%"

pause
