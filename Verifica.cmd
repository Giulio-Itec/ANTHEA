@echo off
cd /d "%~dp0"
dotnet run --project "X.Verifiche\X.Verifiche.csproj" -c Release -- "casi_confronto.json" "confronto_numerico.json"
if errorlevel 1 goto errore
echo Verifiche completate.
pause
exit /b 0
:errore
pause
exit /b 1
