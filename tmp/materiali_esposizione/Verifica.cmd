@echo off
cd /d "%~dp0"
start "" /wait "app-esposizione-menu\Materiali.exe" --check
type "app-esposizione-menu\verifica.txt"
pause








