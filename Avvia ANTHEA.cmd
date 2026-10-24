@echo off
setlocal
cd /d "%~dp0"
echo Ricompilazione di ANTHEA in corso...
dotnet build "src\ANTHEA.Desktop\ANTHEA.Desktop.csproj" -c Release -t:Rebuild
if errorlevel 1 goto errore
dotnet publish "src\ANTHEA.Desktop\ANTHEA.Desktop.csproj" -c Release --no-build --no-restore -o app
if errorlevel 1 goto errore
echo Ricompilazione completata.

start "" "app\ANTHEA.exe"
if errorlevel 1 goto errore
exit /b 0

:errore
echo.
echo Operazione non riuscita. Il programma non e' stato avviato.
echo Controlla gli errori sopra e verifica che ANTHEA sia chiuso.
pause
exit /b 1
