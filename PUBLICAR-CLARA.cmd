@echo off
setlocal
cd /d "%~dp0"
title Share IT .Clara - Publicacao
where powershell.exe >nul 2>&1
if errorlevel 1 (
  echo [ERRO] Windows PowerShell nao encontrado.
  echo.
  pause
  exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Ferramentas\Automacao\Publicar-Clara.ps1" %*
set ERRO=%ERRORLEVEL%

echo.
if not "%ERRO%"=="0" (
  echo ====================================================
  echo [ERRO] Publicacao interrompida. Codigo %ERRO%.
  echo Consulte o log em: artifacts\logs
  echo ====================================================
  echo.
  pause
  exit /b %ERRO%
)

echo ====================================================
echo [OK] Processo do Share IT .Clara concluido.
echo A janela permanecera aberta para voce conferir o resumo.
echo ====================================================
echo.
pause
exit /b 0
