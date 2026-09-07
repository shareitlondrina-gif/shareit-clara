@echo off
setlocal
cd /d "%~dp0"
title Share IT .Clara - Instalacao do Agente
if "%~1"=="" (
  set /p TOKEN=Informe o token temporario de instalacao: 
) else (
  set TOKEN=%~1
)
if "%~2"=="" (
  set /p URL=Informe a URL do servidor .Clara: 
) else (
  set URL=%~2
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar-Agente.ps1" -TokenInstalacao "%TOKEN%" -UrlServidor "%URL%"
exit /b %ERRORLEVEL%
