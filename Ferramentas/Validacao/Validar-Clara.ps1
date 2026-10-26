[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$Raiz=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Logs=Join-Path $Raiz 'logs'
New-Item -ItemType Directory -Force -Path $Logs | Out-Null
$Log=Join-Path $Logs 'VALIDACAO-PRE-PUBLICACAO-ULTIMA.txt'
$linhas=New-Object System.Collections.Generic.List[string]
function Add([string]$x){$null=$linhas.Add($x);Write-Host $x}
function Fail([string]$x){Add ("[ERRO] "+$x);$linhas | Set-Content -Path $Log -Encoding UTF8;throw $x}
Add 'SHARE IT .CLARA - VALIDACAO PRE-PUBLICACAO'
Add ("Data: "+(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$versao=(Get-Content (Join-Path $Raiz 'VERSAO.txt') -Raw).Trim()
Add ("Versao: "+$versao)
if($versao -ne '0.6.0'){Fail "VERSAO.txt inesperada: $versao"}
if(-not (Get-Command dotnet.exe -ErrorAction SilentlyContinue)){Fail '.NET SDK nao encontrado.'}
$dotnet=& dotnet.exe --version
Add ("dotnet: "+$dotnet)
if($dotnet -notmatch '^10\.'){Fail "SDK .NET 10 esperado; encontrado $dotnet"}
$migs=@(Get-ChildItem (Join-Path $Raiz 'database\migrations') -Filter '*.sql' -File | Sort-Object Name)
Add ("Migrations: "+$migs.Count)
if($migs.Count -ne 11){Fail "Esperadas 11 migrations; encontradas $($migs.Count)."}
$obrigatorios=@('PUBLICAR-CLARA.cmd','VALIDAR-ANTES-DE-PUBLICAR.cmd','Dockerfile','render.yaml','database\migrations\0011_perfil_usuario_identidade_visual.sql','Ferramentas\Instalacao\INSTALAR-AGENTE.cmd','Ferramentas\Instalacao\Instalar-Agente.ps1','Ferramentas\Instalacao\MANUTENCAO-CLARA.cmd','Ferramentas\Instalacao\Manutencao-Clara.ps1','Ferramentas\Diagnostico\DIAGNOSTICAR-AGENTE.cmd','Ferramentas\Diagnostico\Diagnosticar-Agente.ps1')
foreach($r in $obrigatorios){if(-not (Test-Path (Join-Path $Raiz $r) -PathType Leaf)){Fail "Arquivo obrigatorio ausente: $r"}}
$segredos=@('SegredosClara.local.xml','.env','appsettings.Production.json')
foreach($n in $segredos){$achado=Get-ChildItem $Raiz -Recurse -Force -File -ErrorAction SilentlyContinue | Where-Object {$_.Name -eq $n};if($achado){Fail "Arquivo de segredo/local nao deve integrar release: $n"}}
Push-Location $Raiz
try{
  Add 'Executando dotnet restore...'
  & dotnet.exe restore .\ShareIT.Clara.sln
  if($LASTEXITCODE -ne 0){Fail 'dotnet restore falhou.'}
  Add 'Executando dotnet build Release...'
  & dotnet.exe build .\ShareIT.Clara.sln -c Release --no-restore -p:Version=$versao
  if($LASTEXITCODE -ne 0){Fail 'dotnet build falhou.'}
  Add 'Executando dotnet test...'
  & dotnet.exe test .\ShareIT.Clara.sln -c Release --no-build --no-restore
  if($LASTEXITCODE -ne 0){Fail 'dotnet test falhou.'}
} finally {Pop-Location}
Add '[OK] Restore/build/test concluido.'
Add '[OK] Release pronta para o publicador. Esta validacao nao substitui o gate online de versao/Build ID/banco/migrations.'
$linhas | Set-Content -Path $Log -Encoding UTF8
