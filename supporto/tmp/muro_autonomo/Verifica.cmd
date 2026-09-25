@echo off
cd /d "%~dp0"
if not exist Verifiche mkdir Verifiche
dotnet "App\ANTHEA.Muro.dll" --test "Verifiche\test.txt"
if errorlevel 1 goto errore
type "Verifiche\test.txt"
pause
exit /b 0
:errore
type "Verifiche\test.txt"
pause
exit /b 1
