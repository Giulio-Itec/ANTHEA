@echo off
cd /d "%~dp0"
start "" /wait "app-schermo\Materiali.exe" --check
type "app-schermo\verifica.txt"
pause







