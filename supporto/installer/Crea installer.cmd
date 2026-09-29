@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Crea-Installer.ps1" %*
if errorlevel 1 goto errore
exit /b 0
:errore
echo.
echo Creazione dell'installer non riuscita. Controlla gli errori sopra.
pause
exit /b 1
