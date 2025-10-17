@echo off
title MainServer - Gomoku Game Server
echo ================================
echo  Gomoku Distributed Game Server
echo ================================
echo Starting MainServer...
echo - Game clients connect on port 5000
echo - Workers connect on port 5001
echo - Press Ctrl+C to stop
echo ================================
echo.

cd /d "%~dp0MainServer"
dotnet run

pause