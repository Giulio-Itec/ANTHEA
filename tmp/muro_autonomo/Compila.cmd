@echo off
cd /d "%~dp0"
dotnet publish "Sorgenti\Muro.csproj" -c Release --self-contained false -o App
if errorlevel 1 goto errore
echo Compilazione completata.
exit /b 0
:errore
pause
exit /b 1
