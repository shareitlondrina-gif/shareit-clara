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
$Logs = Join-Path $Raiz 'logs'
$CarimboExecucao = Get-Date -Format 'yyyyMMdd-HHmmss'
$LogAtual = Join-Path $Logs ("Publicacao-Clara-v{0}-{1}.log" -f $Versao, $CarimboExecucao)
$LogEspelho = Join-Path $Logs 'PUBLICACAO-ULTIMA.log'
$DiagnosticoAtual = Join-Path $Logs ("DIAGNOSTICO-PUBLICACAO-v{0}-{1}.txt" -f $Versao, $CarimboExecucao)
$DiagnosticoEspelho = Join-Path $Logs 'DIAGNOSTICO-PUBLICACAO-ULTIMO.txt'
$BuildArquivo = Join-Path $Logs 'BUILD.txt'
$BuildId = ''
$CommitEsperado = ''
$RepositorioGit = ''
$Branch = 'main'
$OnlineValidado = $false
$PublicacaoOnlineSolicitada = $false
$HookFallbackAcionado = $false
$GitRemotoConfirmado = $false

New-Item -ItemType Directory -Force -Path $Logs | Out-Null
Set-Content -Path $LogEspelho -Value ("Share IT .Clara v{0} - publicacao iniciada em {1}" -f $Versao, (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')) -Encoding UTF8
Start-Transcript -Path $LogAtual -Force | Out-Null

function Registrar-Linha([string]$Texto, [ConsoleColor]$Cor = [ConsoleColor]::Gray) {
    Write-Host $Texto -ForegroundColor $Cor
    try { Add-Content -Path $LogEspelho -Value ((Get-Date -Format 'HH:mm:ss') + ' ' + $Texto) -Encoding UTF8 } catch { }
}
function Etapa([string]$Texto) { Registrar-Linha ''; Registrar-Linha "==> $Texto" ([ConsoleColor]::Cyan) }
function Info([string]$Texto) { Registrar-Linha "[INFO] $Texto" ([ConsoleColor]::DarkCyan) }
function Ok([string]$Texto) { Registrar-Linha "[OK] $Texto" ([ConsoleColor]::Green) }
function Aviso([string]$Texto) { Registrar-Linha "[ATENCAO] $Texto" ([ConsoleColor]::Yellow) }
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
    $configurado = XmlTexto $Publicacao.ConfiguracaoPublicacao 'RepositorioGitHub' 'https://github.com/shareitlondrina-gif/shareit-clara.git'
    if (-not [string]::IsNullOrWhiteSpace($configurado)) { return $configurado }
    if (Test-Path (Join-Path $Raiz '.git')) {
        Push-Location $Raiz
        try {
            $origem = (& git.exe remote get-url origin 2>$null | Select-Object -First 1)
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($origem)) { return $origem.Trim() }
        } finally { Pop-Location }
    }
    return 'https://github.com/shareitlondrina-gif/shareit-clara.git'
}
function Proteger-Url([string]$Url) {
    if ([string]::IsNullOrWhiteSpace($Url)) { return '' }
    try {
        return ($Url -replace '(https?://)([^/@]+)@', '$1***@')
    } catch { return '[url indisponivel]' }
}
function Tentar-Json([string]$Url) {
    try {
        $resposta = Invoke-RestMethod -Uri $Url -TimeoutSec 10 -Headers @{ 'Cache-Control' = 'no-cache' }
        return ($resposta | ConvertTo-Json -Depth 8 -Compress)
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__ 2>$null
        return ("ERRO HTTP={0} mensagem={1}" -f $status, $_.Exception.Message)
    }
}
function Obter-GitTexto([string[]]$Argumentos) {
    if (-not (Tem-Comando 'git.exe')) { return 'git.exe indisponivel' }
    Push-Location $Raiz
    try {
        $saida = & git.exe @Argumentos 2>&1 | Out-String
        return $saida.Trim()
    }
    catch { return $_.Exception.Message }
    finally { Pop-Location }
}

function Consultar-CommitRemoto([string]$Remote, [string]$BranchName) {
    $resultado = [ordered]@{
        Sucesso = $false
        Commit = ''
        Codigo = -1
        Mensagem = ''
    }
    try {
        $saida = @(& git.exe ls-remote --heads $Remote ("refs/heads/{0}" -f $BranchName) 2>&1)
        $codigo = $LASTEXITCODE
        $resultado.Codigo = $codigo
        $linha = $saida | Where-Object { ([string]$_) -match '^[0-9a-fA-F]{40}\s+refs/heads/' } | Select-Object -First 1
        if ($codigo -eq 0 -and -not [string]::IsNullOrWhiteSpace([string]$linha)) {
            $commit = (([string]$linha -split '\s+')[0]).Trim().ToLowerInvariant()
            if ($commit -match '^[0-9a-f]{40}$') {
                $resultado.Sucesso = $true
                $resultado.Commit = $commit
                return [pscustomobject]$resultado
            }
        }
        $resultado.Mensagem = (($saida | ForEach-Object { [string]$_ }) -join ' ').Trim()
    }
    catch {
        $resultado.Mensagem = $_.Exception.Message
    }
    return [pscustomobject]$resultado
}

function Confirmar-CommitRemoto([string]$Remote, [string]$BranchName, [string]$CommitLocal, [int]$Tentativas = 6, [int]$IntervaloSegundos = 5) {
    $ultimoCommitValido = ''
    $houveConsultaValida = $false
    for ($i = 1; $i -le [Math]::Max(1, $Tentativas); $i++) {
        $consulta = Consultar-CommitRemoto -Remote $Remote -BranchName $BranchName
        if ($consulta.Sucesso) {
            $houveConsultaValida = $true
            $ultimoCommitValido = [string]$consulta.Commit
            if ($ultimoCommitValido -eq $CommitLocal.ToLowerInvariant()) {
                return $true
            }
            Aviso "GitHub respondeu com SHA '$ultimoCommitValido', ainda diferente do local '$CommitLocal'. Tentativa $i/$Tentativas."
        }
        else {
            $mensagem = [string]$consulta.Mensagem
            if ([string]::IsNullOrWhiteSpace($mensagem)) { $mensagem = "codigo git $($consulta.Codigo)" }
            Aviso "Confirmacao do SHA remoto temporariamente indisponivel ($mensagem). Tentativa $i/$Tentativas."
        }
        if ($i -lt $Tentativas) { Start-Sleep -Seconds ([Math]::Max(1, $IntervaloSegundos)) }
    }

    if ($houveConsultaValida -and -not [string]::IsNullOrWhiteSpace($ultimoCommitValido)) {
        Falhar "O push retornou sem erro, mas apos $Tentativas tentativas o commit remoto '$ultimoCommitValido' continua diferente do commit local '$CommitLocal'."
    }

    Aviso 'O git push retornou sucesso, mas o GitHub nao respondeu de forma confiavel a verificacao ls-remote. A confirmacao do SHA sera deferida ao gate online do Render; isto nao sera tratado como falha de push.'
    return $false
}
function Gerar-DiagnosticoPublicacao([string]$Motivo, [string]$UrlSistema, [string]$UrlLive, [string]$UrlHealth) {
    try {
        $linhas = New-Object System.Collections.Generic.List[string]
        $null = $linhas.Add('SHARE IT .CLARA - DIAGNOSTICO AUTOMATICO DE PUBLICACAO')
        $null = $linhas.Add(('Gerado em: {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')))
        $null = $linhas.Add(('Motivo: {0}' -f $Motivo))
        $null = $linhas.Add(('Versao esperada: {0}' -f $Versao))
        $null = $linhas.Add(('Build esperado: {0}' -f $BuildId))
        $null = $linhas.Add(('Commit esperado: {0}' -f $CommitEsperado))
        $null = $linhas.Add(('Repositorio: {0}' -f (Proteger-Url $RepositorioGit)))
        $null = $linhas.Add(('Branch: {0}' -f $Branch))
        $null = $linhas.Add(('SHA remoto confirmado imediatamente apos push: {0}' -f $GitRemotoConfirmado))
        $null = $linhas.Add(('URL sistema: {0}' -f $UrlSistema))
        $null = $linhas.Add(('Deploy hook configurado: {0}' -f (-not [string]::IsNullOrWhiteSpace($env:CLARA_RENDER_DEPLOY_HOOK))))
        $null = $linhas.Add(('Deploy hook fallback acionado: {0}' -f $HookFallbackAcionado))
        $null = $linhas.Add('')
        $null = $linhas.Add('--- RESPOSTAS ONLINE ---')
        if (-not [string]::IsNullOrWhiteSpace($UrlLive)) { $null = $linhas.Add(('Liveness: {0}' -f (Tentar-Json $UrlLive))) }
        if (-not [string]::IsNullOrWhiteSpace($UrlHealth)) { $null = $linhas.Add(('Health: {0}' -f (Tentar-Json $UrlHealth))) }
        if (-not [string]::IsNullOrWhiteSpace($UrlSistema)) { $null = $linhas.Add(('Versao: {0}' -f (Tentar-Json ($UrlSistema.TrimEnd('/') + '/api/versao')))) }
        $null = $linhas.Add('')
        $null = $linhas.Add('--- ESTADO GIT LOCAL/REMOTO ---')
        if (Test-Path (Join-Path $Raiz '.git')) {
            $null = $linhas.Add(('status: {0}' -f (Obter-GitTexto @('status','--short','--branch'))))
            $null = $linhas.Add(('HEAD: {0}' -f (Obter-GitTexto @('rev-parse','HEAD'))))
            $null = $linhas.Add(('ultimo commit: {0}' -f (Obter-GitTexto @('log','-1','--oneline'))))
            if (-not [string]::IsNullOrWhiteSpace($Branch)) {
                $null = $linhas.Add(('origin/{0}: {1}' -f $Branch, (Obter-GitTexto @('ls-remote','origin',("refs/heads/{0}" -f $Branch)))))
            }
        } else {
            $null = $linhas.Add('Pasta .git nao existe nesta copia.')
        }
        $null = $linhas.Add('')
        $null = $linhas.Add('--- INTERPRETACAO AUTOMATICA ---')
        $liveTexto = if (-not [string]::IsNullOrWhiteSpace($UrlLive)) { Tentar-Json $UrlLive } else { '' }
        if (-not [string]::IsNullOrWhiteSpace($CommitEsperado) -and $liveTexto -notmatch [regex]::Escape($Versao)) {
            $null = $linhas.Add('O Git pode ter recebido o commit, mas o Render continua servindo outra versao. Verifique no Render se o Web Service shareit-clara esta conectado ao repositorio/branch corretos e se o deploy mais recente falhou.')
        } elseif ($liveTexto -match [regex]::Escape($Versao) -and $liveTexto -notmatch [regex]::Escape($BuildId)) {
            $null = $linhas.Add('A versao coincide, mas o buildId nao coincide. O endpoint esta respondendo uma compilacao diferente da que este publicador acabou de enviar.')
        } else {
            $null = $linhas.Add('Use as secoes acima para distinguir falha de Git, disparo do Render, build do Render ou readiness do banco.')
        }
        $null = $linhas.Add('')
        $null = $linhas.Add('Nenhum segredo, token ou connection string e gravado por este diagnostico.')
        $linhas | Set-Content -Path $DiagnosticoAtual -Encoding UTF8
        Copy-Item $DiagnosticoAtual $DiagnosticoEspelho -Force
        Aviso "Diagnostico salvo em: $DiagnosticoAtual"
        Aviso "Copia facil de localizar: $DiagnosticoEspelho"
    }
    catch {
        Aviso "Nao foi possivel gerar o diagnostico automatico: $($_.Exception.Message)"
    }
}
function Acionar-DeployHookFallback {
    if ($HookFallbackAcionado) { return }
    $hook = $env:CLARA_RENDER_DEPLOY_HOOK
    if ([string]::IsNullOrWhiteSpace($hook)) { return }
    try {
        Aviso 'A versao ainda nao mudou. Acionando uma vez o deploy hook de fallback do Render (URL protegida e nao gravada em log).'
        Invoke-WebRequest -Method Post -Uri $hook -UseBasicParsing -TimeoutSec 20 | Out-Null
        $script:HookFallbackAcionado = $true
        Ok 'Deploy hook de fallback acionado.'
    }
    catch {
        Aviso "Falha ao acionar deploy hook de fallback: $($_.Exception.Message)"
    }
}

function Testar-PublicacaoOnline([string]$UrlLive, [string]$UrlHealth, [int]$TempoMaximoMinutos, [bool]$UsarHookFallback, [int]$SegundosHookFallback) {
    $inicio = Get-Date
    $limite = $inicio.AddMinutes([Math]::Max(2, $TempoMaximoMinutos))
    $tentativa = 0
    $livenessConfirmado = $false

    while ((Get-Date) -lt $limite) {
        $tentativa++
        try {
            $live = Invoke-RestMethod -Uri $UrlLive -TimeoutSec 8 -Headers @{ 'Cache-Control' = 'no-cache' }
            $versaoLive = [string]$live.versao
            $buildLive = [string]$live.buildId
            $commitLive = [string]$live.commit
            $aplicacaoLive = [string]$live.aplicacao
            $estadoBanco = [string]$live.inicializacaoBanco
            $buildConfere = (-not [string]::IsNullOrWhiteSpace($BuildId)) -and ($buildLive -eq $BuildId)

            if ($versaoLive -eq $Versao -and $aplicacaoLive -eq 'ok' -and $buildConfere) {
                if (-not $livenessConfirmado) {
                    Ok "Web/API v$versaoLive respondeu ao liveness com o build esperado '$buildLive'. Estado do banco: '$estadoBanco'. Commit Render='$commitLive'."
                    $livenessConfirmado = $true
                }

                try {
                    $resposta = Invoke-RestMethod -Uri $UrlHealth -TimeoutSec 8 -Headers @{ 'Cache-Control' = 'no-cache' }
                    $versaoRemota = [string]$resposta.versao
                    $buildRemoto = [string]$resposta.buildId
                    $banco = [string]$resposta.banco
                    $nomeBanco = [string]$resposta.nomeBanco
                    $migracoesRemotas = [int]$resposta.migracoes
                    if ($banco -eq 'ok' -and $nomeBanco -eq 'shareit_clara' -and $versaoRemota -eq $Versao -and $buildRemoto -eq $BuildId -and $migracoesRemotas -eq $MigracoesEsperadas) {
                        Ok "Health completo confirmado na versao $versaoRemota, build $buildRemoto, banco $nomeBanco, $migracoesRemotas migrations."
                        return $true
                    }
                    Aviso "Web/API esta online, mas o health completo ainda nao confirmou a baseline. Versao='$versaoRemota', build='$buildRemoto/$BuildId', banco='$banco', migrations='$migracoesRemotas/$MigracoesEsperadas'."
                }
                catch {
                    $statusHealth = $_.Exception.Response.StatusCode.value__ 2>$null
                    if ($statusHealth -eq 503) {
                        Aviso "Web/API v$Versao build $BuildId esta online, mas o banco ainda esta inicializando ou apresentou falha. Consulte /api/health e os Logs do Render."
                    } else {
                        Aviso "Web/API v$Versao build $BuildId esta online, mas o health completo ainda esta indisponivel: $($_.Exception.Message)"
                    }
                }
            } elseif ($versaoLive -eq $Versao -and $aplicacaoLive -eq 'ok') {
                Aviso "A versao remota ja e '$versaoLive', mas o build ainda nao e o desta execucao. Remoto='$buildLive', esperado='$BuildId'. Tentativa $tentativa."
            } else {
                Aviso "Liveness respondeu, mas ainda nao esta na versao esperada. Remota='$versaoLive', esperada='$Versao', buildRemoto='$buildLive'. Tentativa $tentativa."
            }
        }
        catch {
            $status = $_.Exception.Response.StatusCode.value__ 2>$null
            if ($status -eq 404) {
                Aviso "Liveness respondeu HTTP 404; o deploy novo ainda nao assumiu o servico. Tentativa $tentativa."
            } elseif ($status -eq 503) {
                Aviso "Render respondeu HTTP 503; o servico pode estar reiniciando, iniciando o container ou aguardando o banco. Aguardando sem classificar como falha definitiva. Tentativa $tentativa."
            } else {
                Aviso "Liveness ainda indisponivel: $($_.Exception.Message). Tentativa $tentativa."
            }
        }

        if ($UsarHookFallback -and -not $HookFallbackAcionado -and ((Get-Date) - $inicio).TotalSeconds -ge [Math]::Max(30, $SegundosHookFallback)) {
            Acionar-DeployHookFallback
        }

        if ((Get-Date) -lt $limite) { Start-Sleep -Seconds 6 }
    }

    return $false
}

try {
    Info "Log detalhado desta execucao: $LogAtual"
    Info "Log-resumo facil de localizar: $LogEspelho"
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
    $repoPreConfigurado = XmlTexto $pub.ConfiguracaoPublicacao 'RepositorioGitHub' 'https://github.com/shareitlondrina-gif/shareit-clara.git'
    $possuiRepositorioLocal = Test-Path (Join-Path $Raiz '.git')
    # Compatibilidade com ConfigSys criado pela v0.1.0/v0.1.1: se ja existe repositorio local
    # ou URL configurada, a publicacao automatica passa a fazer push mesmo que o XML antigo tenha false.
    $executarGit = (-not $SemGit) -and $publicacaoAutomatica -and ($gitExplicitamenteHabilitado -or $possuiRepositorioLocal -or -not [string]::IsNullOrWhiteSpace($repoPreConfigurado))
    $executarHook = (-not $SemDeploy) -and (XmlBool $pub.ConfiguracaoPublicacao 'ExecutarDeployHook' $false)
    $usarHookFallback = (-not $SemDeploy) -and (XmlBool $pub.ConfiguracaoPublicacao 'UsarDeployHookComoFallback' $true)
    $segundosHookFallback = [Math]::Max(30, (XmlInt $pub.ConfiguracaoPublicacao 'SegundosAntesDeployHookFallback' 120))
    $PublicacaoOnlineSolicitada = $executarGit -or $executarHook
    $Branch = XmlTexto $pub.ConfiguracaoPublicacao 'Branch' 'main'
    $RepositorioGit = $repoPreConfigurado

    Etapa 'Gerando identificador unico do build'
    $BuildId = ("{0}-{1}-{2}" -f $Versao, ([DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss')), ([Guid]::NewGuid().ToString('N').Substring(0,8)))
    Set-Content -Path $BuildArquivo -Value $BuildId -Encoding ASCII
    Ok "Build ID desta publicacao: $BuildId"
    Info 'Esse identificador sera devolvido por /api/versao e /api/health/live para impedir falso positivo com build antigo.'

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
    $NavegadorHostOut = Join-Path $PacoteRoot 'NavegadorHost'
    $NavegadorExtensaoOut = Join-Path $PacoteRoot 'NavegadorExtensao'
    $WebOut = Join-Path $Release 'Web'
    New-Item -ItemType Directory -Force -Path $AgenteOut,$SessaoOut,$AtualizadorOut,$NavegadorHostOut,$NavegadorExtensaoOut,$WebOut | Out-Null

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
        & dotnet.exe publish .\src\ShareIT.Clara.NavegadorHost\ShareIT.Clara.NavegadorHost.csproj -c Release -r win-x64 --self-contained true -o $NavegadorHostOut -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Versao
        if ($LASTEXITCODE -ne 0) { Falhar 'Publish do Native Messaging Host falhou.' }
        Copy-Item (Join-Path $Raiz 'src\ShareIT.Clara.NavegadorExtensao\*') $NavegadorExtensaoOut -Recurse -Force

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
        $ZipInstalador = Join-Path $Release ("Clara-Instalador-v{0}.zip" -f $Versao)
        Compress-Archive -Path (Join-Path $Instalador '*') -DestinationPath $ZipInstalador -CompressionLevel Optimal -Force
        $PastaInstaladorWeb = Join-Path $WebOut 'instaladores\agente'
        New-Item -ItemType Directory -Force -Path $PastaInstaladorWeb | Out-Null
        Copy-Item $ZipInstalador (Join-Path $PastaInstaladorWeb (Split-Path $ZipInstalador -Leaf)) -Force

        $Resumo = [ordered]@{
            produto='Share IT .Clara'; versao=$Versao; geradoEmUtc=[DateTimeOffset]::UtcNow.ToString('O');
            agenteSha256=$Sha; dotnet=$dotnetVersao; health=$health; buildId=$BuildId
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
        $RepositorioGit = Obter-RepositorioGit $pub
        if ([string]::IsNullOrWhiteSpace($RepositorioGit)) {
            Falhar "Publicacao online esta habilitada, mas o repositorio GitHub nao foi configurado. Preencha RepositorioGitHub em '$ConfigPub' ou configure o remote origin do Git."
        }
        $Branch = XmlTexto $pub.ConfiguracaoPublicacao 'Branch' 'main'
        Info ("Repositorio alvo: {0}" -f (Proteger-Url $RepositorioGit))
        Info "Branch alvo: $Branch"
        Push-Location $Raiz
        try {
            if (-not (Test-Path '.git')) {
                & git.exe init
                if ($LASTEXITCODE -ne 0) { Falhar 'git init falhou.' }
            }
            $gitNome = (& git.exe config user.name 2>$null | Select-Object -First 1)
            $gitEmail = (& git.exe config user.email 2>$null | Select-Object -First 1)
            if ([string]::IsNullOrWhiteSpace($gitNome)) { & git.exe config user.name 'Share IT Release'; if ($LASTEXITCODE -ne 0) { Falhar 'Nao foi possivel configurar user.name local do Git.' } }
            if ([string]::IsNullOrWhiteSpace($gitEmail)) { & git.exe config user.email 'release@shareit.local'; if ($LASTEXITCODE -ne 0) { Falhar 'Nao foi possivel configurar user.email local do Git.' } }

            # O pacote de release nao leva a pasta .git. Portanto, cada nova versao pode
            # ser executada em uma pasta limpa. Antes de criar o commit local, vinculamos
            # esta copia ao historico remoto para evitar non-fast-forward/fetch first.
            $remotes = @(& git.exe remote 2>$null)
            if ($remotes -contains 'origin') {
                & git.exe remote set-url origin $RepositorioGit
                if ($LASTEXITCODE -ne 0) { Falhar 'Nao foi possivel atualizar o remote origin.' }
            } else {
                & git.exe remote add origin $RepositorioGit
                if ($LASTEXITCODE -ne 0) { Falhar 'Nao foi possivel configurar o remote origin.' }
            }

            Etapa 'Sincronizando historico Git remoto'
            & git.exe ls-remote --exit-code --heads origin $Branch *> $null
            $resultadoRemote = $LASTEXITCODE
            if ($resultadoRemote -eq 0) {
                & git.exe fetch origin $Branch --prune
                if ($LASTEXITCODE -ne 0) { Falhar 'git fetch falhou. O historico remoto nao foi alterado.' }

                # Mantem os arquivos da release atual no disco, mas posiciona o HEAD no
                # ultimo commit remoto. Assim o novo commit e criado por cima do historico
                # existente sem merge artificial e sem usar push --force.
                & git.exe reset --mixed ("origin/{0}" -f $Branch)
                if ($LASTEXITCODE -ne 0) { Falhar 'Nao foi possivel sincronizar a base local com o historico remoto.' }
                Ok "Historico remoto sincronizado com origin/$Branch; arquivos da release atual preservados."
            }
            elseif ($resultadoRemote -eq 2) {
                Info "A branch remota '$Branch' ainda nao existe; sera criada no primeiro push."
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
            & git.exe branch -M $Branch
            if ($LASTEXITCODE -ne 0) { Falhar 'Falha ao ajustar branch Git.' }
            & git.exe push -u origin $Branch
            if ($LASTEXITCODE -ne 0) { Falhar 'Git push falhou mesmo apos sincronizacao segura do historico remoto.' }

            $CommitEsperado = (& git.exe rev-parse HEAD).Trim().ToLowerInvariant()
            if ([string]::IsNullOrWhiteSpace($CommitEsperado)) { Falhar 'Nao foi possivel obter o commit local apos o push.' }
            $GitRemotoConfirmado = Confirmar-CommitRemoto -Remote 'origin' -BranchName $Branch -CommitLocal $CommitEsperado -Tentativas 6 -IntervaloSegundos 5
            if ($GitRemotoConfirmado) {
                Ok "Codigo enviado e confirmado no GitHub: $(Proteger-Url $RepositorioGit) ($Branch)."
                Ok "Commit local/remoto confirmado: $CommitEsperado"
            }
            else {
                Ok "git push concluido para $(Proteger-Url $RepositorioGit) ($Branch)."
                Aviso "SHA remoto nao foi confirmado imediatamente; a publicacao seguira para a validacao do Render usando versao e Build ID como gate final."
            }
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
        if ($usarHookFallback -and -not [string]::IsNullOrWhiteSpace($env:CLARA_RENDER_DEPLOY_HOOK)) {
            Info "Deploy hook de fallback esta disponivel e podera ser acionado automaticamente apos $segundosHookFallback segundo(s) se a versao nao mudar."
        } elseif ($usarHookFallback) {
            Info 'Deploy hook de fallback nao esta configurado; a validacao seguira somente pelo push/Auto Deploy.'
        }
        $OnlineValidado = Testar-PublicacaoOnline -UrlLive $healthLive -UrlHealth $health -TempoMaximoMinutos $tempoDeploy -UsarHookFallback $usarHookFallback -SegundosHookFallback $segundosHookFallback
        if (-not $OnlineValidado) {
            Gerar-DiagnosticoPublicacao -Motivo "Render nao confirmou v$Versao / build $BuildId dentro do tempo configurado." -UrlSistema $urlSistema -UrlLive $healthLive -UrlHealth $health
            Falhar "A compilacao e o envio terminaram e o ZIP local foi preservado em '$ZipFinal', mas o Render nao confirmou a baseline v$Versao build $BuildId dentro do tempo configurado. Consulte '$DiagnosticoAtual' e '$LogAtual'."
        }
    } elseif (-not [string]::IsNullOrWhiteSpace($health)) {
        Etapa 'Verificando endereco configurado sem exigir nova publicacao'
        $OnlineValidado = Testar-PublicacaoOnline -UrlLive $healthLive -UrlHealth $health -TempoMaximoMinutos 2 -UsarHookFallback $false -SegundosHookFallback 120
        if (-not $OnlineValidado) {
            Aviso 'A release local foi concluida, mas a versao online nao foi confirmada nesta execucao.'
        }
    }

    Write-Host "`n====================================================" -ForegroundColor Green
    Write-Host " SHARE IT .CLARA - RESULTADO DA PUBLICACAO" -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Green
    Write-Host ("Versao:       v{0}" -f $Versao)
    Write-Host ("Build ID:     {0}" -f $BuildId)
    if (-not [string]::IsNullOrWhiteSpace($CommitEsperado)) { Write-Host ("Commit Git:    {0}" -f $CommitEsperado) }
    if ($executarGit) {
        Write-Host ("Git remoto:    {0}" -f ($(if ($GitRemotoConfirmado) { 'CONFIRMADO' } else { 'PUSH OK / SHA DIFERIDO AO GATE ONLINE' })))
    }
    Write-Host "Compilacao:   OK"
    Write-Host "Testes:       OK"
    Write-Host "Banco alvo:   shareit_clara"
    Write-Host ("Migrations:    {0}" -f $MigracoesEsperadas)
    Write-Host ("ZIP:          {0}" -f $ZipFinal)
    Write-Host ("Log:          {0}" -f $LogAtual)
    Write-Host ("Log facil:    {0}" -f $LogEspelho)
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
catch {
    $mensagemErro = $_.Exception.Message
    Registrar-Linha "[ERRO] $mensagemErro" ([ConsoleColor]::Red)
    try { Add-Content -Path $LogEspelho -Value ($_ | Out-String) -Encoding UTF8 } catch { }
    if (-not (Test-Path $DiagnosticoAtual)) {
        Gerar-DiagnosticoPublicacao -Motivo $mensagemErro -UrlSistema $urlSistema -UrlLive $healthLive -UrlHealth $health
    }
    throw
}
finally {
    try { Stop-Transcript | Out-Null } catch { }
    try {
        if (Test-Path $LogAtual) { Copy-Item $LogAtual $LogEspelho -Force }
    } catch { }
}
