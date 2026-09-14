@echo off
setlocal
cd /d "%~dp0"
title Share IT .Clara - Publicacao

if not exist "%~dp0logs" mkdir "%~dp0logs" >nul 2>&1
set "LAUNCHER_LOG=%~dp0logs\Publicador-Launcher.log"
>>"%LAUNCHER_LOG%" echo ====================================================
>>"%LAUNCHER_LOG%" echo [%date% %time%] Iniciando PUBLICAR-CLARA.cmd em "%~dp0"

echo ====================================================
echo  SHARE IT .CLARA - PUBLICADOR
echo ====================================================
echo [INFO] Pasta: %~dp0
echo [INFO] Todos os logs serao salvos em logs\
echo [INFO] Ultimo log: logs\PUBLICACAO-ULTIMA.log
echo [INFO] Diagnostico: logs\DIAGNOSTICO-PUBLICACAO-ULTIMO.txt
echo ====================================================
echo.

where powershell.exe >nul 2>&1
if errorlevel 1 (
  echo [ERRO] Windows PowerShell nao encontrado.
  >>"%LAUNCHER_LOG%" echo [%date% %time%] ERRO: Windows PowerShell nao encontrado.
  echo.
  pause
  exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Ferramentas\Automacao\Publicar-Clara.ps1" %*
set ERRO=%ERRORLEVEL%
>>"%LAUNCHER_LOG%" echo [%date% %time%] PowerShell terminou com codigo %ERRO%.

echo.
if not "%ERRO%"=="0" (
  echo ====================================================
  echo [ERRO] Publicacao interrompida. Codigo %ERRO%.
  echo Log principal: %~dp0logs\PUBLICACAO-ULTIMA.log
  echo Diagnostico:   %~dp0logs\DIAGNOSTICO-PUBLICACAO-ULTIMO.txt
  echo Logs completos: %~dp0logs
  echo ====================================================
  echo.
  pause
  exit /b %ERRO%
)

echo ====================================================
echo [OK] Processo do Share IT .Clara concluido.
echo Log principal: %~dp0logs\PUBLICACAO-ULTIMA.log
echo A janela permanecera aberta para voce conferir o resumo.
echo ====================================================
echo.
pause
exit /b 0
