# Clara
# Programador: Paulo Andrade
# Objetivo: automação operacional controlada da Clara.
# Origem: parâmetros/configuração local e ambiente autorizado.
# Destino: processo local, publicação ou manutenção correspondente.
# Boas práticas: falhar cedo, não registrar segredos e validar pré-condições antes de alterar o ambiente.

[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$Raiz=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Logs=Join-Path $Raiz 'logs'
New-Item -ItemType Directory -Force -Path $Logs | Out-Null
$BuildArquivo=Join-Path $Logs 'BUILD.txt'
$Log=Join-Path $Logs 'VALIDACAO-PRE-PUBLICACAO-ULTIMA.txt'
$linhas=New-Object System.Collections.Generic.List[string]
function Add([string]$x){$null=$linhas.Add($x);Write-Host $x}
function Fail([string]$x){Add ("[ERRO] "+$x);$linhas | Set-Content -Path $Log -Encoding UTF8;throw $x}
Add 'CLARA - VALIDACAO PRE-PUBLICACAO'
Add ("Data: "+(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$versao=(Get-Content (Join-Path $Raiz 'VERSAO.txt') -Raw).Trim()
Add ("Versao: "+$versao)
$revisao=(Get-Content (Join-Path $Raiz 'RELEASE.txt') -Raw).Trim()
Add ("Revisao: "+$revisao)
if($revisao -ne '0.6.0-RC1-R6'){Fail "RELEASE.txt inesperada: $revisao"}
if($versao -ne '0.6.0'){Fail "VERSAO.txt inesperada: $versao"}
# O Build ID precisa existir ANTES do dotnet build porque Clara.Web o incorpora no artefato publicado.
# Origem: versão atual + timestamp UTC + identificador aleatório.
# Destino: logs\BUILD.txt -> Clara.Web/BUILD.txt -> endpoints técnicos -> gate online.
$buildId=("{0}-{1}-{2}" -f $versao, ([DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss')), ([Guid]::NewGuid().ToString('N').Substring(0,8)))
Set-Content -Path $BuildArquivo -Value $buildId -Encoding ASCII
Add ("Build ID preparado antes da compilacao: "+$buildId)
if(-not (Get-Command dotnet.exe -ErrorAction SilentlyContinue)){Fail '.NET SDK nao encontrado.'}
$dotnet=& dotnet.exe --version
Add ("dotnet: "+$dotnet)
if($dotnet -notmatch '^10\.'){Fail "SDK .NET 10 esperado; encontrado $dotnet"}
$migs=@(Get-ChildItem (Join-Path $Raiz 'database\migrations') -Filter '*.sql' -File | Sort-Object Name)
Add ("Migrations: "+$migs.Count)
if($migs.Count -ne 12){Fail "Esperadas 12 migrations; encontradas $($migs.Count)."}
$obrigatorios=@('PUBLICAR-CLARA.cmd','RELEASE.txt','Clara.sln','Dockerfile','render.yaml','database\migrations\0011_perfil_usuario_identidade_visual.sql','database\migrations\0012_identidade_clara.sql','Ferramentas\Instalacao\INSTALAR-AGENTE.cmd','Ferramentas\Instalacao\Instalar-Agente.ps1','Ferramentas\Instalacao\MANUTENCAO-CLARA.cmd','Ferramentas\Instalacao\Manutencao-Clara.ps1','Ferramentas\Diagnostico\DIAGNOSTICAR-AGENTE.cmd','Ferramentas\Diagnostico\Diagnosticar-Agente.ps1')
foreach($r in $obrigatorios){if(-not (Test-Path (Join-Path $Raiz $r) -PathType Leaf)){Fail "Arquivo obrigatorio ausente: $r"}}
$segredos=@('SegredosClara.local.xml','.env','appsettings.Production.json')
foreach($n in $segredos){$achado=Get-ChildItem $Raiz -Recurse -Force -File -ErrorAction SilentlyContinue | Where-Object {$_.Name -eq $n};if($achado){Fail "Arquivo de segredo/local nao deve integrar release: $n"}}
# Identidade ativa: migrations antigas são imutáveis e ficam fora desta varredura de branding.
$fontesAtivas=Get-ChildItem $Raiz -Recurse -Force -File -ErrorAction SilentlyContinue | Where-Object {
  $_.FullName -notlike (Join-Path $Raiz 'database\migrations\*') -and
  $_.FullName -notlike (Join-Path $Raiz 'logs\*') -and
  $_.Extension -in @('.cs','.cshtml','.csproj','.sln','.ps1','.cmd','.js','.css','.json','.xml','.yaml','.yml','.md','.txt','.props')
}
$padraoMarcaLegada=('Sha'+'re\s*IT|Sha'+'reIT|sha'+'reit')
$marcaLegada=$fontesAtivas | Select-String -Pattern $padraoMarcaLegada -CaseSensitive:$false -ErrorAction SilentlyContinue
if($marcaLegada){Fail 'Identidade legada encontrada em código/configuração ativa. A Clara deve usar somente a marca Clara.'}
Add '[OK] Identidade ativa somente Clara; migrations históricas permanecem imutáveis.'

$layout=Get-Content (Join-Path $Raiz 'src\Clara.Web\Views\Shared\_Layout.cshtml') -Raw
if($layout -notmatch 'icone-clara-64\.png' -or $layout -notmatch '>Clara</span>'){Fail 'Layout principal não está usando a identidade Clara esperada.'}
if($layout -match 'logo-clara\.png'){Fail 'Layout ainda referencia logo horizontal legado.'}
$padraoAssetLegado=('^logo-clara|sha'+'re|sha'+'reit')
$assetsLegados=@(Get-ChildItem (Join-Path $Raiz 'src\Clara.Web\wwwroot\img') -File | Where-Object {$_.Name -match $padraoAssetLegado})
if($assetsLegados.Count -gt 0){Fail ('Assets visuais legados encontrados: '+(($assetsLegados.Name)-join ', '))}
Add '[OK] Interface usa somente ícone + nome Clara; sem logo horizontal legado.'

$cs=@(Get-ChildItem (Join-Path $Raiz 'src') -Recurse -Filter '*.cs' -File)
$semCabecalho=@($cs | Where-Object { -not (Select-String -Path $_.FullName -Pattern 'Programador: Paulo Andrade' -SimpleMatch -Quiet) })
if($semCabecalho.Count -gt 0){Fail ("Arquivos C# sem cabeçalho padrão de autoria: " + (($semCabecalho | Select-Object -ExpandProperty FullName) -join ', '))}
Add ("[OK] Cabeçalho Paulo Andrade em {0}/{0} arquivos C#." -f $cs.Count)

if(Test-Path (Join-Path $Raiz 'VALIDAR-ANTES-DE-PUBLICAR.cmd')){Fail 'O launcher separado de validação não deve existir na raiz; use somente PUBLICAR-CLARA.cmd.'}
Add '[OK] Fluxo oficial em comando único: PUBLICAR-CLARA.cmd.'

Push-Location $Raiz
try{
  Add 'Executando dotnet restore...'
  & dotnet.exe restore .\Clara.sln
  if($LASTEXITCODE -ne 0){Fail 'dotnet restore falhou.'}
  Add 'Executando dotnet build Release...'
  & dotnet.exe build .\Clara.sln -c Release --no-restore -p:Version=$versao
  if($LASTEXITCODE -ne 0){Fail 'dotnet build falhou.'}
  Add 'Executando dotnet test...'
  & dotnet.exe test .\Clara.sln -c Release --no-build --no-restore
  if($LASTEXITCODE -ne 0){Fail 'dotnet test falhou.'}
} finally {Pop-Location}
Add '[OK] Restore/build/test concluido.'
Add '[OK] Release pronta para o publicador. Esta validacao nao substitui o gate online de versao/Build ID/banco/migrations.'
$linhas | Set-Content -Path $Log -Encoding UTF8
