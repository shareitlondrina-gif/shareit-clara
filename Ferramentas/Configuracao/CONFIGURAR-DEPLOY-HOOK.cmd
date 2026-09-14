@echo off
setlocal
cd /d "%~dp0"
title Share IT .Clara - Configurar Deploy Hook
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Configurar-DeployHook.ps1"
set ERRO=%ERRORLEVEL%
echo.
if not "%ERRO%"=="0" (
  echo [ERRO] Nao foi possivel salvar o Deploy Hook. Codigo %ERRO%.
) else (
  echo [OK] Configuracao concluida. A URL nao foi exibida nem gravada em logs.
)
echo.
pause
exit /b %ERRO%
