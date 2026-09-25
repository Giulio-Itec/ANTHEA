@echo off
cd /d "%~dp0"
start "" /wait "app-esempi\Materiali.exe" --check
type "app-esempi\verifica.txt"
pause









