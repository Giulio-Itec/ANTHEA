@echo off
cd /d "%~dp0"
dotnet publish src\Materiali.csproj -c Release -o app-composizione
pause





