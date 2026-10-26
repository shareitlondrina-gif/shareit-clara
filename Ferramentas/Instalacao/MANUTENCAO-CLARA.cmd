@echo off
setlocal
if "%~1"=="" (
  echo Uso: MANUTENCAO-CLARA.cmd ^<Parar^|Reparar^|Desinstalar^> ^<codigo-administrador^>
  exit /b 2
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manutencao-Clara.ps1" -Operacao "%~1" -Codigo "%~2"
exit /b %errorlevel%
