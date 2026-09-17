@echo off
cd /d "%~dp0"
if exist "app\ANTHEA.exe" (
  start "" "app\ANTHEA.exe"
  exit /b
)
dotnet run --project "X.Desktop\X.Desktop.csproj" -c Release
if errorlevel 1 pause
