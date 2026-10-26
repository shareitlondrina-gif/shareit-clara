@echo off
setlocal
cd /d "%~dp0\..\.."
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Diagnosticar-Coexistencia-Clara-Visao.ps1"
set ERRO=%ERRORLEVEL%
echo.
echo Relatorio: logs\DIAGNOSTICO-COEXISTENCIA-ULTIMO.txt
pause
exit /b %ERRO%
