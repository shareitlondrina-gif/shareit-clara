@echo off
REM Clara
REM Programador: Paulo Andrade
REM Objetivo: executar em um único comando o gate completo e a publicação da Clara.
REM Origem: código-fonte local e configurações autorizadas em ConfigSys/ambiente.
REM Destino: build validado, pacote Windows, Git/Render e gate online.
REM Boas práticas: nenhuma publicação ocorre antes de restore/build/test e das validações estruturais.
setlocal
cd /d "%~dp0"
title Clara - Validar e Publicar

if not exist "%~dp0logs" mkdir "%~dp0logs" >nul 2>&1
set "LAUNCHER_LOG=%~dp0logs\Publicador-Launcher.log"
>>"%LAUNCHER_LOG%" echo ====================================================
>>"%LAUNCHER_LOG%" echo [%date% %time%] Iniciando PUBLICAR-CLARA.cmd em "%~dp0"

echo ====================================================
echo  CLARA - VALIDAR + PUBLICAR
echo ====================================================
echo [INFO] Um unico comando executara validacao, testes e publicacao.
echo [INFO] Nenhuma alteracao online ocorre antes do gate local ficar 100%% OK.
echo.

where powershell.exe >nul 2>&1
if errorlevel 1 (
  echo [ERRO] Windows PowerShell nao encontrado.
  >>"%LAUNCHER_LOG%" echo [%date% %time%] ERRO: Windows PowerShell nao encontrado.
  pause
  exit /b 1
)

echo [GATE 1] Validacao estrutural + restore + build Release + testes...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Ferramentas\Validacao\Validar-Clara.ps1"
if errorlevel 1 (
  echo.
  echo [ERRO] PUBLICACAO BLOQUEADA. O gate local falhou.
  >>"%LAUNCHER_LOG%" echo [%date% %time%] ERRO: gate local falhou.
  echo Log: %~dp0logs\VALIDACAO-PRE-PUBLICACAO-ULTIMA.txt
  pause
  exit /b 1
)
set "CLARA_GATE_VALIDADO=1"
echo [OK] Gate local aprovado. Publicacao liberada.
echo.

echo [GATE 2] Empacotamento, Git/Render e validacao online...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Ferramentas\Automacao\Publicar-Clara.ps1" %*
set ERRO=%ERRORLEVEL%
>>"%LAUNCHER_LOG%" echo [%date% %time%] Publicador terminou com codigo %ERRO%.

echo.
if not "%ERRO%"=="0" (
  echo ====================================================
  echo [ERRO] Publicacao interrompida. Codigo %ERRO%.
  echo Log principal: %~dp0logs\PUBLICACAO-ULTIMA.log
  echo Diagnostico:   %~dp0logs\DIAGNOSTICO-PUBLICACAO-ULTIMO.txt
  echo ====================================================
  pause
  exit /b %ERRO%
)

echo ====================================================
echo [OK] CLARA VALIDADA E PUBLICADA.
echo Log principal: %~dp0logs\PUBLICACAO-ULTIMA.log
echo ====================================================
pause
exit /b 0
