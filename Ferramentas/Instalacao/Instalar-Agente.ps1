[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$TokenInstalacao,
    [Parameter(Mandatory=$true)][string]$UrlServidor
)
$ErrorActionPreference='Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute o instalador como Administrador.'
}
$Origem = Join-Path $PSScriptRoot 'Pacote'
$Destino = 'C:\Program Files\Share IT\Clara'
$Dados = 'C:\ProgramData\ShareIT\Clara'
$Servico = 'ShareIT.Clara.Agente'
$Tarefa = 'Share IT .Clara - Sessao'
$HostNativoNome = 'br.com.shareit.clara'
$ExtensaoId = 'diidcakejmmckhhbggepbblogonddjml'
if (-not (Test-Path (Join-Path $Origem 'Agente\ShareIT.Clara.Agente.exe'))) { throw 'Pacote do agente nao encontrado.' }
if (-not (Test-Path (Join-Path $Origem 'NavegadorHost\ShareIT.Clara.NavegadorHost.exe'))) { throw 'Native Messaging Host do navegador nao encontrado.' }

New-Item -ItemType Directory -Force -Path $Destino,$Dados | Out-Null
$Segredos = Join-Path $Dados 'segredos'
New-Item -ItemType Directory -Force -Path $Segredos | Out-Null
& icacls.exe $Segredos /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' | Out-Null
Copy-Item (Join-Path $Origem '*') $Destino -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'MANUTENCAO-CLARA.cmd') $Destino -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot 'Manutencao-Clara.ps1') $Destino -Force -ErrorAction SilentlyContinue
$config = [ordered]@{
    urlServidor=$UrlServidor.TrimEnd('/')
    intervaloHeartbeatSegundos=60
    intervaloSincronizacaoSegundos=30
    intervaloAtualizacaoMinutos=15
    limiarOciosidadeMinutos=5
    monitorarUrlsDominios=$false
}
$config | ConvertTo-Json | Set-Content (Join-Path $Dados 'configuracao-agente.json') -Encoding UTF8
Set-Content -Path (Join-Path $Segredos 'token-instalacao.txt') -Value $TokenInstalacao -Encoding ASCII -NoNewline

& sc.exe stop $Servico 2>$null | Out-Null
& sc.exe delete $Servico 2>$null | Out-Null
Start-Sleep -Seconds 1
$AgenteExe = Join-Path $Destino 'Agente\ShareIT.Clara.Agente.exe'
& sc.exe create $Servico binPath= ('"{0}"' -f $AgenteExe) start= auto DisplayName= 'Share IT .Clara - Agente' | Out-Null
& sc.exe description $Servico 'Agente Windows do Share IT .Clara para telemetria autorizada de performance.' | Out-Null
& sc.exe failure $Servico reset= 86400 actions= restart/60000/restart/60000/restart/120000 | Out-Null
& sc.exe failureflag $Servico 1 | Out-Null

$SessaoExe = Join-Path $Destino 'Sessao\ShareIT.Clara.Sessao.exe'
Unregister-ScheduledTask -TaskName $Tarefa -Confirm:$false -ErrorAction SilentlyContinue
$acao = New-ScheduledTaskAction -Execute $SessaoExe
$gatilho = New-ScheduledTaskTrigger -AtLogOn
$principal = New-ScheduledTaskPrincipal -GroupId 'BUILTIN\Users' -RunLevel Limited
Register-ScheduledTask -TaskName $Tarefa -Action $acao -Trigger $gatilho -Principal $principal -Description 'Coletor de sessao do Share IT .Clara' | Out-Null

# Native Messaging Host: o executavel recebe SOMENTE dominio/navegador da extensao e encaminha ao agente por Named Pipe.
$PastaManifesto = Join-Path $Dados 'navegador'
New-Item -ItemType Directory -Force -Path $PastaManifesto | Out-Null
$HostExe = Join-Path $Destino 'NavegadorHost\ShareIT.Clara.NavegadorHost.exe'
$ManifestoHost = Join-Path $PastaManifesto ($HostNativoNome + '.json')
$Manifesto = [ordered]@{
    name = $HostNativoNome
    description = 'Share IT .Clara - Contexto web autorizado'
    path = $HostExe
    type = 'stdio'
    allowed_origins = @("chrome-extension://$ExtensaoId/")
}
$Manifesto | ConvertTo-Json -Depth 4 | Set-Content $ManifestoHost -Encoding UTF8
foreach ($Chave in @(
    "HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\$HostNativoNome",
    "HKLM:\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\$HostNativoNome"
)) {
    New-Item -Path $Chave -Force | Out-Null
    Set-Item -Path $Chave -Value $ManifestoHost
}

& sc.exe start $Servico | Out-Null
try { Start-ScheduledTask -TaskName $Tarefa } catch { }
Write-Host 'Agente instalado e iniciado. O token sera consumido automaticamente no primeiro registro.' -ForegroundColor Green
Write-Host 'Host nativo do Chrome/Edge registrado. A coleta web somente sera processada se a politica do tenant habilitar Monitorar URLs/dominios.' -ForegroundColor Cyan
Write-Host ("Extensao para homologacao: {0}" -f (Join-Path $Destino 'NavegadorExtensao')) -ForegroundColor Cyan
