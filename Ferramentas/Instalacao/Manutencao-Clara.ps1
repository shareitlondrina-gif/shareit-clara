[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Parar','Reparar','Desinstalar')][string]$Operacao,
    [Parameter(Mandatory=$true)][string]$Codigo
)
$ErrorActionPreference='Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute a manutenção como Administrador.'
}
$Dados='C:\ProgramData\ShareIT\Clara'
$Destino='C:\Program Files\Share IT\Clara'
$Servico='ShareIT.Clara.Agente'
$Tarefa='Share IT .Clara - Sessao'
$Identidade=Join-Path $Dados 'segredos\identidade.dat'
$Config=Join-Path $Dados 'configuracao-agente.json'
if(-not(Test-Path $Identidade)){throw 'Identidade do dispositivo não encontrada. Use o portal da Clara para suporte.'}
if(-not(Test-Path $Config)){throw 'Configuração local da Clara não encontrada.'}
Add-Type -AssemblyName System.Security
$protegido=[IO.File]::ReadAllBytes($Identidade)
$aberto=[Security.Cryptography.ProtectedData]::Unprotect($protegido,$null,[Security.Cryptography.DataProtectionScope]::LocalMachine)
try{$idJson=[Text.Encoding]::UTF8.GetString($aberto)|ConvertFrom-Json}finally{[Array]::Clear($aberto,0,$aberto.Length)}
$cfg=Get-Content $Config -Raw | ConvertFrom-Json
$base=([string]$cfg.urlServidor).TrimEnd('/')
$headers=@{'X-Clara-Agente'=[string]$idJson.identificadorAgente;'X-Clara-Token'=[string]$idJson.tokenDispositivo}
$body=@{codigo=$Codigo;operacao=$Operacao}|ConvertTo-Json -Compress
try{
    $resp=Invoke-RestMethod -Method Post -Uri "$base/api/v1/agentes/manutencao/autorizar" -Headers $headers -ContentType 'application/json' -Body $body -TimeoutSec 30
}catch{throw 'Código recusado, expirado ou já utilizado. Solicite um novo código ao Administrador.'}
if(-not $resp.autorizado){throw 'Operação não autorizada pelo servidor.'}
switch($Operacao){
 'Parar' {
   Stop-Service -Name $Servico -Force -ErrorAction Stop
   Write-Host 'Serviço Clara parado com autorização administrativa.' -ForegroundColor Yellow
 }
 'Reparar' {
   Set-Service -Name $Servico -StartupType Automatic
   Restart-Service -Name $Servico -Force
   try{Start-ScheduledTask -TaskName $Tarefa}catch{}
   Write-Host 'Serviço Clara reparado/reiniciado com autorização administrativa.' -ForegroundColor Green
 }
 'Desinstalar' {
   Stop-Service -Name $Servico -Force -ErrorAction SilentlyContinue
   & sc.exe delete $Servico | Out-Null
   Unregister-ScheduledTask -TaskName $Tarefa -Confirm:$false -ErrorAction SilentlyContinue
   foreach($k in @('HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\br.com.shareit.clara','HKLM:\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\br.com.shareit.clara')){Remove-Item $k -Recurse -Force -ErrorAction SilentlyContinue}
   $cleanup=Join-Path $env:TEMP ('clara-cleanup-'+[guid]::NewGuid().ToString('N')+'.cmd')
   @"
@echo off
ping 127.0.0.1 -n 4 >nul
rmdir /s /q "$Destino"
rmdir /s /q "$Dados"
del /q "%~f0"
"@ | Set-Content $cleanup -Encoding ASCII
   Start-Process cmd.exe -ArgumentList '/c',('"'+$cleanup+'"') -WindowStyle Hidden
   Write-Host 'Desinstalação autorizada. A limpeza será concluída em segundos.' -ForegroundColor Green
 }
}
