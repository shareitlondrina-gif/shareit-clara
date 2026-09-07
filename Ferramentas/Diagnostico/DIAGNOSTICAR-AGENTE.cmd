@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Diagnosticar-Agente.ps1"
exit /b %ERRORLEVEL%
