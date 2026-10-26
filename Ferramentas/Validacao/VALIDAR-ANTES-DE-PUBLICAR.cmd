@echo off
setlocal
cd /d "%~dp0\..\.."
echo ============================================================
echo SHARE IT .CLARA - VALIDACAO ANTES DA PUBLICACAO
echo ============================================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Validar-Clara.ps1"
set ERRO=%ERRORLEVEL%
echo.
if not "%ERRO%"=="0" (
  echo [ERRO] Validacao falhou. Codigo %ERRO%.
) else (
  echo [OK] Validacao concluida. Pode executar PUBLICAR-CLARA.cmd.
)
echo Log: logs\VALIDACAO-PRE-PUBLICACAO-ULTIMA.txt
echo ============================================================
pause
exit /b %ERRO%
