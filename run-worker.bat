@echo off
title WorkerServer - Gomoku Worker Server
echo ================================
echo       Gomoku Worker Server
echo ================================
echo - Connecting to MainServer at 192.168.195.69 / 192.168.195.126:5001
echo - Handles AI calculations and move validation
echo - Press Ctrl+C to stop
echo ================================
echo.

cd /d "%~dp0WorkerServer"
dotnet run

pause
