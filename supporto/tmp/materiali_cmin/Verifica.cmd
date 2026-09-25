@echo off
cd /d "%~dp0"
start "" /wait "app-cmin\Materiali.exe" --check
type "app-cmin\verifica.txt"
pause






