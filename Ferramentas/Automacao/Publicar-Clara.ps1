[CmdletBinding()]
param(
    [switch]$SemGit,
    [switch]$SemDeploy,
    [switch]$NaoAbrirNavegador
)

$ErrorActionPreference = 'Stop'
$Raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Versao = (Get-Content (Join-Path $Raiz 'VERSAO.txt') -Raw).Trim()
$MigracoesEsperadas = @(Get-ChildItem (Join-Path $Raiz 'database\migrations') -Filter '*.sql' -File).Count
$ConfigDir = 'C:\ConfigSys\Clara'
$ConfigClara = Join-Path $ConfigDir 'ConfiguracaoClara.xml'
$ConfigPub = Join-Path $ConfigDir 'Publicacao\ConfiguracaoPublicacao.xml'
$Artefatos = Join-Path $Raiz 'artifacts'
$Release = Join-Path $Artefatos ("Clara-v{0}" -f $Versao)
$Logs = Join-Path $Artefatos 'logs'
$LogAtual = Join-Path $Logs ("Publicacao-Clara-v{0}-{1}.log" -f $Versao, (Get-Date -Format 'yyyyMMdd-HHmmss'))
$OnlineValidado = $false
$PublicacaoOnlineSolicitada = $false

New-Item -ItemType Directory -Force -Path $Logs | Out-Null
Start-Transcript -Path $LogAtual -Force | Out-Null

function Etapa([string]$Texto) { Write-Host "`n==> $Texto" -ForegroundColor Cyan }
function Info([string]$Texto) { Write-Host "[INFO] $Texto" -ForegroundColor DarkCyan }
function Ok([string]$Texto) { Write-Host "[OK] $Texto" -ForegroundColor Green }
function Aviso([string]$Texto) { Write-Host "[ATENCAO] $Texto" -ForegroundColor Yellow }
function Falhar([string]$Texto) { throw $Texto }
function Tem-Comando([string]$Nome) { return [bool](Get-Command $Nome -ErrorAction SilentlyContinue) }
function Atualizar-Path {
    $machine = [Environment]::GetEnvironmentVariable('Path','Machine')
    $user = [Environment]::GetEnvironmentVariable('Path','User')
    $env:Path = "$machine;$user"
}
function Garantir-Comando([string]$Comando,[string]$WingetId,[string]$Descricao) {
    if (Tem-Comando $Comando) { return }
    if (-not (Tem-Comando 'winget.exe')) { Falhar "$Descricao nao encontrado e winget indisponivel para instalacao automatica." }
    Write-Host "Instalando $Descricao..."
    & winget.exe install --id $WingetId --exact --silent --accept-source-agreements --accept-package-agreements
    Atualizar-Path
    if (-not (Tem-Comando $Comando)) { Falhar "$Descricao nao ficou disponivel apos a instalacao." }
}
function Copiar-SeAusente([string]$Origem,[string]$Destino) {
    if (-not (Test-Path $Destino)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $Destino) | Out-Null
        Copy-Item $Origem $Destino
    }
}
function XmlTexto($No, [string]$Nome, [string]$Padrao = '') {
    if ($null -eq $No) { return $Padrao }
    $propriedade = $No.PSObject.Properties[$Nome]
    if ($null -eq $propriedade -or $null -eq $propriedade.Value) { return $Padrao }
    $valor = [string]$propriedade.Value
    if ([string]::IsNullOrWhiteSpace($valor)) { return $Padrao }
    return $valor.Trim()
}
function XmlBool($No, [string]$Nome, [bool]$Padrao) {
    $texto = XmlTexto $No $Nome ''
    if ([string]::IsNullOrWhiteSpace($texto)) { return $Padrao }
    $resultado = $false
    if ([bool]::TryParse($texto, [ref]$resultado)) { return $resultado }
    return $Padrao
}
function XmlInt($No, [string]$Nome, [int]$Padrao) {
    $texto = XmlTexto $No $Nome ''
    $resultado = 0
    if ([int]::TryParse($texto, [ref]$resultado)) { return $resultado }
    return $Padrao
}
function Obter-RepositorioGit([xml]$Publicacao) {
    $configurado = XmlTexto $Publicacao.ConfiguracaoPublicacao 'RepositorioGitHub' ''
    if (-not [string]::IsNullOrWhiteSpace($configurado)) { return $configurado }
    if (Test-Path (Join-Path $Raiz '.git')) {
        Push-Location $Raiz
        try {
            $origem = (& git.exe remote get-url origin 2>$null | Select-Object -First 1)
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($origem)) { return $origem.Trim() }
        } finally { Pop-Location }
    }
    return ''
}
function Testar-PublicacaoOnline([string]$UrlLive, [string]$UrlHealth, [int]$TempoMaximoMinutos) {
    $limite = (Get-Date).AddMinutes([Math]::Max(2, $TempoMaximoMinutos))
    $tentativa = 0
    $livenessConfirmado = $false

    while ((Get-Date) -lt $limite) {
        $tentativa++
        try {
            $live = Invoke-RestMethod -Uri $UrlLive -TimeoutSec 8 -Headers @{ 'Cache-Control' = 'no-cache' }
            $versaoLive = [string]$live.versao
            $aplicacaoLive = [string]$live.aplicacao
            $estadoBanco = [string]$live.inicializacaoBanco

            if ($versaoLive -eq $Versao -and $aplicacaoLive -eq 'ok') {
                if (-not $livenessConfirmado) {
                    Ok "Web/API v$versaoLive respondeu ao liveness. Estado do banco: '$estadoBanco'."
                    $livenessConfirmado = $true
                }

                try {
                    $resposta = Invoke-RestMethod -Uri $UrlHealth -TimeoutSec 8 -Headers @{ 'Cache-Control' = 'no-cache' }
                    $versaoRemota = [string]$resposta.versao
                    $banco = [string]$resposta.banco
                    $nomeBanco = [string]$resposta.nomeBanco
                    $migracoesRemotas = [int]$resposta.migracoes
                    if ($banco -eq 'ok' -and $nomeBanco -eq 'shareit_clara' -and $versaoRemota -eq $Versao -and $migracoesRemotas -eq $MigracoesEsperadas) {
                        Ok "Health completo confirmado na versao $versaoRemota, banco $nomeBanco, $migracoesRemotas migrations."
                        return $true
                    }
                    Aviso "Web/API esta online, mas o health completo ainda nao confirmou a baseline. Versao='$versaoRemota', banco='$banco', migrations='$migracoesRemotas/$MigracoesEsperadas'."
                }
                catch {
                    $statusHealth = $_.Exception.Response.StatusCode.value__ 2>$null
                    if ($statusHealth -eq 503) {
                        Aviso "Web/API v$Versao esta online, mas o banco ainda esta inicializando ou apresentou falha. Consulte /api/health e os Logs do Render."
                    } else {
                        Aviso "Web/API v$Versao esta online, mas o health completo ainda esta indisponivel: $($_.Exception.Message)"
                    }
                }
            } else {
                Aviso "Liveness respondeu, mas ainda nao esta na versao esperada. Remota='$versaoLive', esperada='$Versao'. Tentativa $tentativa."
            }
        }
        catch {
            $status = $_.Exception.Response.StatusCode.value__ 2>$null
            if ($status -eq 404) {
                Aviso "Liveness respondeu HTTP 404; o deploy novo ainda nao assumiu o servico. Tentativa $tentativa."
            } else {
                Aviso "Liveness ainda indisponivel: $($_.Exception.Message). Tentativa $tentativa."
            }
        }

        if ((Get-Date) -lt $limite) { Start-Sleep -Seconds 6 }
    }

    return $false
}

try {
    Etapa 'Preparando ConfigSys'
    New-Item -ItemType Directory -Force -Path $ConfigDir | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $ConfigDir 'Publicacao') | Out-Null
    Copiar-SeAusente (Join-Path $Raiz 'ConfigSys\ConfiguracaoClara.exemplo.xml') $ConfigClara
    Copiar-SeAusente (Join-Path $Raiz 'ConfigSys\ConfiguracaoPublicacao.exemplo.xml') $ConfigPub
    if (-not (Test-Path $ConfigPub)) { Falhar 'ConfiguracaoPublicacao.xml nao encontrado.' }
    if (-not (Test-Path $ConfigClara)) { Falhar 'ConfiguracaoClara.xml nao encontrado.' }
    [xml]$cfg = Get-Content $ConfigClara
    [xml]$pub = Get-Content $ConfigPub

    $urlSistema = XmlTexto $pub.ConfiguracaoPublicacao 'UrlSistema' (XmlTexto $cfg.ConfiguracaoClara.Servidor 'UrlPublica' 'https://shareit-clara.onrender.com')
    if ([string]::IsNullOrWhiteSpace($urlSistema) -or $urlSistema.Contains('exemplo.com.br')) {
        $urlSistema = 'https://shareit-clara.onrender.com'
    }
    $health = XmlTexto $pub.ConfiguracaoPublicacao 'UrlHealth' ($urlSistema.TrimEnd('/') + '/api/health')
    if ([string]::IsNullOrWhiteSpace($health) -or $health.Contains('exemplo.com.br')) {
        $health = $urlSistema.TrimEnd('/') + '/api/health'
    }
    $healthLive = $urlSistema.TrimEnd('/') + '/api/health/live'
    $abrirNavegador = XmlBool $pub.ConfiguracaoPublicacao 'AbrirNavegadorAposPublicacao' $true
    $tempoDeploy = [Math]::Max(2, (XmlInt $pub.ConfiguracaoPublicacao 'TempoMaximoDeployMinutos' 12))
    $publicacaoAutomatica = XmlBool $pub.ConfiguracaoPublicacao 'PublicacaoAutomatica' $true
    $gitExplicitamenteHabilitado = XmlBool $pub.ConfiguracaoPublicacao 'ExecutarGitPush' $true
    $repoPreConfigurado = XmlTexto $pub.ConfiguracaoPublicacao 'RepositorioGitHub' ''
    $possuiRepositorioLocal = Test-Path (Join-Path $Raiz '.git')
    # Compatibilidade com ConfigSys criado pela v0.1.0/v0.1.1: se ja existe repositorio local
    # ou URL configurada, a publicacao automatica passa a fazer push mesmo que o XML antigo tenha false.
    $executarGit = (-not $SemGit) -and $publicacaoAutomatica -and ($gitExplicitamenteHabilitado -or $possuiRepositorioLocal -or -not [string]::IsNullOrWhiteSpace($repoPreConfigurado))
    $executarHook = (-not $SemDeploy) -and (XmlBool $pub.ConfiguracaoPublicacao 'ExecutarDeployHook' $false)
    $PublicacaoOnlineSolicitada = $executarGit -or $executarHook

    Etapa 'Validando protecao do banco'
    $nomeBanco = [string]$cfg.ConfiguracaoClara.Banco.NomeBanco
    if ([string]::IsNullOrWhiteSpace($nomeBanco)) { $nomeBanco = 'shareit_clara' }
    if ($nomeBanco -cne 'shareit_clara') { Falhar "Banco configurado '$nomeBanco'. Nesta baseline o destino obrigatorio e shareit_clara." }
    $criarBanco = [string]$cfg.ConfiguracaoClara.Banco.CriarAutomaticamente
    if ([string]::IsNullOrWhiteSpace($criarBanco)) { $criarBanco = 'true' }
    Ok 'Banco de destino protegido: shareit_clara'
    Info "Criacao automatica do banco: $criarBanco"
    Info 'A conexao pode apontar para o PostgreSQL compartilhado do .VISÃO; migrations do .Clara so executam em shareit_clara.'
    Info "Migrations versionadas esperadas nesta release: $MigracoesEsperadas"

    Etapa 'Validando prerequisitos'
    Garantir-Comando 'dotnet.exe' 'Microsoft.DotNet.SDK.10' '.NET SDK 10'
    $dotnetVersao = (& dotnet.exe --version).Trim()
    if (-not $dotnetVersao.StartsWith('10.')) { Falhar ".NET SDK 10 obrigatorio. Encontrado: $dotnetVersao" }
    if ($executarGit) { Garantir-Comando 'git.exe' 'Git.Git' 'Git' }

    Etapa 'Limpando artefatos anteriores'
    if (Test-Path $Release) { Remove-Item $Release -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $Release | Out-Null
    $PacoteRoot = Join-Path $Release 'PacoteAgente'
    $AgenteOut = Join-Path $PacoteRoot 'Agente'
    $SessaoOut = Join-Path $PacoteRoot 'Sessao'
    $AtualizadorOut = Join-Path $PacoteRoot 'Atualizador'
    $WebOut = Join-Path $Release 'Web'
    New-Item -ItemType Directory -Force -Path $AgenteOut,$SessaoOut,$AtualizadorOut,$WebOut | Out-Null

    Etapa 'Restore, compilacao e testes'
    Push-Location $Raiz
    try {
        & dotnet.exe restore .\ShareIT.Clara.sln
        if ($LASTEXITCODE -ne 0) { Falhar 'dotnet restore falhou.' }
        & dotnet.exe build .\ShareIT.Clara.sln -c Release --no-restore -p:Version=$Versao
        if ($LASTEXITCODE -ne 0) { Falhar 'dotnet build falhou.' }
        & dotnet.exe test .\ShareIT.Clara.sln -c Release --no-build --no-restore
        if ($LASTEXITCODE -ne 0) { Falhar 'dotnet test falhou.' }

        Etapa 'Publicando componentes Windows'
        & dotnet.exe publish .\src\ShareIT.Clara.Agente\ShareIT.Clara.Agente.csproj -c Release -r win-x64 --self-contained true -o $AgenteOut -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Versao
        if ($LASTEXITCODE -ne 0) { Falhar 'Publish do agente falhou.' }
        & dotnet.exe publish .\src\ShareIT.Clara.Sessao\ShareIT.Clara.Sessao.csproj -c Release -r win-x64 --self-contained true -o $SessaoOut -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Versao
        if ($LASTEXITCODE -ne 0) { Falhar 'Publish da sessao falhou.' }
        & dotnet.exe publish .\src\ShareIT.Clara.Atualizador\ShareIT.Clara.Atualizador.csproj -c Release -r win-x64 --self-contained true -o $AtualizadorOut -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Versao
        if ($LASTEXITCODE -ne 0) { Falhar 'Publish do atualizador falhou.' }

        Etapa 'Gerando pacote automatico do agente'
        $ZipAgente = Join-Path $Release ("Clara-Agent-v{0}.zip" -f $Versao)
        Compress-Archive -Path (Join-Path $PacoteRoot '*') -DestinationPath $ZipAgente -CompressionLevel Optimal -Force
        $Sha = (Get-FileHash $ZipAgente -Algorithm SHA256).Hash.ToLowerInvariant()

        Etapa 'Publicando Web/API'
        & dotnet.exe publish .\src\ShareIT.Clara.Web\ShareIT.Clara.Web.csproj -c Release -o $WebOut -p:Version=$Versao
        if ($LASTEXITCODE -ne 0) { Falhar 'Publish Web/API falhou.' }
        $PastaUpdateWeb = Join-Path $WebOut ("atualizacoes\agente\{0}" -f $Versao)
        New-Item -ItemType Directory -Force -Path $PastaUpdateWeb | Out-Null
        Copy-Item $ZipAgente (Join-Path $PastaUpdateWeb (Split-Path $ZipAgente -Leaf)) -Force
        $ManifestoDir = Join-Path $WebOut 'atualizacoes\agente'
        $Obrigatoria = XmlBool $pub.ConfiguracaoPublicacao 'ObrigarAtualizacaoAgente' $false
        $Manifesto = [ordered]@{
            versao = $Versao
            urlPacote = ("/api/v1/agentes/atualizacao/pacote/{0}" -f $Versao)
            sha256 = $Sha
            obrigatoria = $Obrigatoria
            publicadaEmUtc = [DateTimeOffset]::UtcNow.ToString('O')
        }
        $Manifesto | ConvertTo-Json | Set-Content (Join-Path $ManifestoDir 'manifesto.json') -Encoding UTF8

        Etapa 'Preparando instalador e diagnostico'
        $Instalador = Join-Path $Release 'InstaladorAgente'
        New-Item -ItemType Directory -Force -Path $Instalador | Out-Null
        Copy-Item $PacoteRoot (Join-Path $Instalador 'Pacote') -Recurse -Force
        Copy-Item (Join-Path $Raiz 'Ferramentas\Instalacao\INSTALAR-AGENTE.cmd') $Instalador
        Copy-Item (Join-Path $Raiz 'Ferramentas\Instalacao\Instalar-Agente.ps1') $Instalador
        Copy-Item (Join-Path $Raiz 'Ferramentas\Diagnostico\DIAGNOSTICAR-AGENTE.cmd') $Instalador
        Copy-Item (Join-Path $Raiz 'Ferramentas\Diagnostico\Diagnosticar-Agente.ps1') $Instalador

        $Resumo = [ordered]@{
            produto='Share IT .Clara'; versao=$Versao; geradoEmUtc=[DateTimeOffset]::UtcNow.ToString('O');
            agenteSha256=$Sha; dotnet=$dotnetVersao; health=$health
        }
        $Resumo | ConvertTo-Json | Set-Content (Join-Path $Release 'release.json') -Encoding UTF8
    }
    finally { Pop-Location }

    Etapa 'Verificando inclusao acidental de segredos'
    $arquivosSuspeitos = Get-ChildItem $Release -Recurse -File | Where-Object { $_.Name -match 'Segredos.*local|\.env$|credentials|secret' }
    if ($arquivosSuspeitos) { Falhar ('Arquivo potencialmente secreto incluido no release: ' + (($arquivosSuspeitos.FullName) -join ', ')) }
    Ok 'Release sem arquivos locais de segredos conhecidos.'

    Etapa 'Gerando ZIP local antes da publicacao online'
    $ZipFinal = Join-Path $Artefatos ("Clara-v{0}.zip" -f $Versao)
    if (Test-Path $ZipFinal) { Remove-Item $ZipFinal -Force }
    Compress-Archive -Path (Join-Path $Release '*') -DestinationPath $ZipFinal -CompressionLevel Optimal
    Ok "ZIP local gerado: $ZipFinal"

    if ($executarGit) {
        Etapa 'Publicando codigo no GitHub'
        $repo = Obter-RepositorioGit $pub
        if ([string]::IsNullOrWhiteSpace($repo)) {
            Falhar "Publicacao online esta habilitada, mas o repositorio GitHub nao foi configurado. Preencha RepositorioGitHub em '$ConfigPub' ou configure o remote origin do Git."
        }
        $branch = XmlTexto $pub.ConfiguracaoPublicacao 'Branch' 'main'
        Push-Location $Raiz
        try {
            if (-not (Test-Path '.git')) {
                & git.exe init
                if ($LASTEXITCODE -ne 0) { Falhar 'git init falhou.' }
            }

            # O pacote de release nao leva a pasta .git. Portanto, cada nova versao pode
            # ser executada em uma pasta limpa. Antes de criar o commit local, vinculamos
            # esta copia ao historico remoto para evitar non-fast-forward/fetch first.
            $remotes = @(& git.exe remote 2>$null)
            if ($remotes -contains 'origin') {
                & git.exe remote set-url origin $repo
                if ($LASTEXITCODE -ne 0) { Falhar 'Nao foi possivel atualizar o remote origin.' }
            } else {
                & git.exe remote add origin $repo
                if ($LASTEXITCODE -ne 0) { Falhar 'Nao foi possivel configurar o remote origin.' }
            }

            Etapa 'Sincronizando historico Git remoto'
            & git.exe ls-remote --exit-code --heads origin $branch *> $null
            $resultadoRemote = $LASTEXITCODE
            if ($resultadoRemote -eq 0) {
                & git.exe fetch origin $branch --prune
                if ($LASTEXITCODE -ne 0) { Falhar 'git fetch falhou. O historico remoto nao foi alterado.' }

                # Mantem os arquivos da release atual no disco, mas posiciona o HEAD no
                # ultimo commit remoto. Assim o novo commit e criado por cima do historico
                # existente sem merge artificial e sem usar push --force.
                & git.exe reset --mixed ("origin/{0}" -f $branch)
                if ($LASTEXITCODE -ne 0) { Falhar 'Nao foi possivel sincronizar a base local com o historico remoto.' }
                Ok "Historico remoto sincronizado com origin/$branch; arquivos da release atual preservados."
            }
            elseif ($resultadoRemote -eq 2) {
                Info "A branch remota '$branch' ainda nao existe; sera criada no primeiro push."
            }
            else {
                Falhar 'Nao foi possivel consultar a branch remota no GitHub. Verifique autenticacao e permissao do repositorio.'
            }

            & git.exe add -A
            & git.exe diff --cached --quiet
            if ($LASTEXITCODE -ne 0) {
                & git.exe commit -m ("Share IT .Clara v{0}" -f $Versao)
                if ($LASTEXITCODE -ne 0) { Falhar 'git commit falhou.' }
            } else {
                Info 'Nenhuma alteracao nova para commit; sera validado o estado da branch remota.'
            }
            & git.exe branch -M $branch
            if ($LASTEXITCODE -ne 0) { Falhar 'Falha ao ajustar branch Git.' }
            & git.exe push -u origin $branch
            if ($LASTEXITCODE -ne 0) { Falhar 'Git push falhou mesmo apos sincronizacao segura do historico remoto.' }
            Ok "Codigo enviado ao GitHub: $repo ($branch)."
        } finally { Pop-Location }
    } else {
        Aviso 'Git push desabilitado. O pacote foi gerado localmente, mas esta execucao nao publicou codigo novo no Render.'
    }

    if ($executarHook) {
        Etapa 'Acionando deploy hook do Render'
        $hook = $env:CLARA_RENDER_DEPLOY_HOOK
        if ([string]::IsNullOrWhiteSpace($hook)) { Falhar 'CLARA_RENDER_DEPLOY_HOOK nao definido.' }
        Invoke-WebRequest -Method Post -Uri $hook -UseBasicParsing | Out-Null
        Ok 'Deploy hook acionado.'
    } elseif ($executarGit) {
        Info 'Deploy hook desabilitado. O render.yaml usa autoDeploy=true; o push ao GitHub deve disparar o deploy do Web Service conectado ao repositorio.'
    }

    if ($PublicacaoOnlineSolicitada -and -not $SemDeploy) {
        Etapa "Aguardando o Render publicar v$Versao"
        Info "Liveness: $healthLive"
        Info "Readiness: $health"
        Info "Tempo maximo total de validacao: $tempoDeploy minuto(s)."
        $OnlineValidado = Testar-PublicacaoOnline -UrlLive $healthLive -UrlHealth $health -TempoMaximoMinutos $tempoDeploy
        if (-not $OnlineValidado) {
            Falhar "A compilacao e o envio terminaram e o ZIP local foi preservado em '$ZipFinal', mas o Render nao confirmou a baseline v$Versao dentro do tempo configurado. Verifique /api/health/live, /api/health e os Logs do servico shareit-clara."
        }
    } elseif (-not [string]::IsNullOrWhiteSpace($health)) {
        Etapa 'Verificando endereco configurado sem exigir nova publicacao'
        $OnlineValidado = Testar-PublicacaoOnline -UrlLive $healthLive -UrlHealth $health -TempoMaximoMinutos 2
        if (-not $OnlineValidado) {
            Aviso 'A release local foi concluida, mas a versao online nao foi confirmada nesta execucao.'
        }
    }

    Write-Host "`n====================================================" -ForegroundColor Green
    Write-Host " SHARE IT .CLARA - RESULTADO DA PUBLICACAO" -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Green
    Write-Host ("Versao:       v{0}" -f $Versao)
    Write-Host "Compilacao:   OK"
    Write-Host "Testes:       OK"
    Write-Host "Banco alvo:   shareit_clara"
    Write-Host ("Migrations:    {0}" -f $MigracoesEsperadas)
    Write-Host ("ZIP:          {0}" -f $ZipFinal)
    Write-Host ("Log:          {0}" -f $LogAtual)
    if ($OnlineValidado) {
        Write-Host "Render:       ONLINE E VALIDADO" -ForegroundColor Green
        Write-Host ("Sistema:      {0}" -f $urlSistema) -ForegroundColor Green
        Write-Host ("Health:       {0}" -f $health) -ForegroundColor Green
    } else {
        Write-Host "Render:       NAO CONFIRMADO NESTA EXECUCAO" -ForegroundColor Yellow
        Write-Host ("Endereco:     {0}" -f $urlSistema)
    }
    Write-Host "====================================================`n" -ForegroundColor Green

    if ($OnlineValidado -and $abrirNavegador -and -not $NaoAbrirNavegador) {
        Start-Process $urlSistema
    }
}
finally {
    try { Stop-Transcript | Out-Null } catch { }
}
