@echo off
REM Clara
REM Programador: Paulo Andrade
REM Objetivo: launcher operacional da Clara.
REM Origem: execução administrativa/local.
REM Destino: script PowerShell ou rotina validada correspondente.
REM Boas práticas: código de saída obrigatório e interrupção imediata em falha.
setlocal
if "%~1"=="" (
  echo Uso: MANUTENCAO-CLARA.cmd ^<Parar^|Reparar^|Desinstalar^> ^<codigo-administrador^>
  exit /b 2
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manutencao-Clara.ps1" -Operacao "%~1" -Codigo "%~2"
exit /b %errorlevel%
