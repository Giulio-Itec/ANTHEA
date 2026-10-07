@echo off
rem Verifiche di ANTHEA senza prove WPF (profilo standard di build\ci.ps1).
rem Uso: Verifica.cmd [/nopause]. Pausa finale solo con il doppio clic, mai con /nopause o con CI definita.
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\build\ci.ps1" -Profile standard -Tag verifica
set "ESITO=%ERRORLEVEL%"
if /i "%~1"=="/nopause" goto fine
if defined CI goto fine
echo %cmdcmdline% | find /i "/c" >nul && pause
:fine
exit /b %ESITO%
