@echo off
setlocal
cd /d "%~dp0"
title Share IT .Clara - Validacao antes de publicar

echo ============================================================
echo  SHARE IT .CLARA - VALIDACAO ANTES DA PUBLICACAO
echo ============================================================
echo [INFO] Pasta: %~dp0
echo.

if not exist "%~dp0Ferramentas\Validacao\VALIDAR-ANTES-DE-PUBLICAR.cmd" (
  echo [ERRO] Validador interno nao encontrado.
  echo Esperado: Ferramentas\Validacao\VALIDAR-ANTES-DE-PUBLICAR.cmd
  pause
  exit /b 1
)

call "%~dp0Ferramentas\Validacao\VALIDAR-ANTES-DE-PUBLICAR.cmd"
exit /b %ERRORLEVEL%
