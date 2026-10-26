@echo off
REM Clara
REM Programador: Paulo Andrade
REM Objetivo: launcher operacional da Clara.
REM Origem: execução administrativa/local.
REM Destino: script PowerShell ou rotina validada correspondente.
REM Boas práticas: código de saída obrigatório e interrupção imediata em falha.
setlocal
cd /d "%~dp0\..\.."
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Diagnosticar-Coexistencia-Clara-Visao.ps1"
set ERRO=%ERRORLEVEL%
echo.
echo Relatorio: logs\DIAGNOSTICO-COEXISTENCIA-ULTIMO.txt
pause
exit /b %ERRO%
