@echo off
cd /d "%~dp0"
start "" /wait "app-composizione\Materiali.exe" --check
type "app-composizione\verifica.txt"
pause





