@echo off
cd /d "%~dp0"
dotnet build "src\ANTHEA.Desktop\ANTHEA.Desktop.csproj" -c Release
if errorlevel 1 goto errore
dotnet publish "src\ANTHEA.Desktop\ANTHEA.Desktop.csproj" -c Release --no-restore -o app
if errorlevel 1 goto errore
echo Compilazione completata. Avviare app\ANTHEA.exe
exit /b 0
:errore
pause
exit /b 1
