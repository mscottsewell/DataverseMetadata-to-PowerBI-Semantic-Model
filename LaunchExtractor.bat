@echo off
REM ===================================================================================
REM LaunchExtractor.bat - Quick Build and Launch for Standalone Configurator
REM ===================================================================================
REM
REM PURPOSE:
REM This script provides a one-click way to build and launch the standalone
REM DataverseToPowerBI.Configurator application for development and testing.
REM
REM WHAT IT DOES:
REM 1. Changes to the Configurator project directory
REM 2. Builds the project in Release configuration
REM 3. Launches the resulting executable
REM
REM REQUIREMENTS:
REM - .NET 8.0 SDK installed
REM - Run from the repository root directory
REM
REM NOTE:
REM For production use, prefer the XrmToolBox plugin which provides better
REM integration with Dataverse authentication and connection management.
REM
REM ===================================================================================

echo Building Dataverse Metadata Extractor...
cd /d "%~dp0DataverseToPowerBI.Configurator"
dotnet build --configuration Release
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Starting application...
start "" "bin\Release\net8.0-windows\DataverseToPowerBI.Configurator.exe"
