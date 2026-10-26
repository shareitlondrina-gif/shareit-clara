@echo off
REM Clara
REM Programador: Paulo Andrade
REM Objetivo: launcher operacional da Clara.
REM Origem: execução administrativa/local.
REM Destino: script PowerShell ou rotina validada correspondente.
REM Boas práticas: código de saída obrigatório e interrupção imediata em falha.
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Diagnosticar-Agente.ps1"
exit /b %ERRORLEVEL%
