@echo off
title WorkerServer - Gomoku AI Worker
echo ================================
echo   Gomoku AI Worker Server
echo ================================
echo Starting WorkerServer...
echo - Connecting to MainServer at localhost:5001
echo - Handles AI calculations and move validation
echo - Press Ctrl+C to stop
echo ================================
echo.

cd /d "%~dp0WorkerServer"
dotnet run

pause