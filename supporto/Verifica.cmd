@echo off
cd /d "%~dp0.."
if not exist "supporto\artefatti" mkdir "supporto\artefatti"
dotnet run --project "supporto\test\X.Verifiche\X.Verifiche.csproj" -c Release -- "supporto\test\casi_confronto.json" "supporto\artefatti\confronto_numerico.json"
if errorlevel 1 goto errore
echo Verifiche completate.
pause
exit /b 0
:errore
pause
exit /b 1
