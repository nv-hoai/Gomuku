@echo off
title Build All - Gomoku Projects
echo ================================
echo   Building Gomoku Projects
echo ================================
echo Building all projects...
echo.

echo Building SharedLib...
cd /d "%~dp0SharedLib"
dotnet build
if errorlevel 1 (
    echo ERROR: SharedLib build failed!
    pause
    exit /b 1
)

echo.
echo Building MainServer...
cd /d "%~dp0MainServer"
dotnet build
if errorlevel 1 (
    echo ERROR: MainServer build failed!
    pause
    exit /b 1
)

echo.
echo Building WorkerServer...
cd /d "%~dp0WorkerServer"
dotnet build
if errorlevel 1 (
    echo ERROR: WorkerServer build failed!
    pause
    exit /b 1
)

echo.
echo ================================
echo    BUILD SUCCESSFUL!
echo ================================
echo All projects built successfully.
echo You can now run the servers.
echo.
pause