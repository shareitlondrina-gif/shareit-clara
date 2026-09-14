[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ConfigDir = 'C:\ConfigSys\Clara'
$Arquivo = Join-Path $ConfigDir 'SegredosClara.local.xml'
$Raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Modelo = Join-Path $Raiz 'ConfigSys\SegredosClara.local.exemplo.xml'

function Converter-SecureStringParaTexto([Security.SecureString]$Secure) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

function Validar-Url([string]$Valor) {
    if ([string]::IsNullOrWhiteSpace($Valor)) { throw 'A URL nao pode ficar vazia.' }
    try { $uri = [Uri]$Valor.Trim() } catch { throw 'URL invalida.' }
    if ($uri.Scheme -ne 'https') { throw 'O Deploy Hook deve usar HTTPS.' }
    if ($uri.Host -notmatch '(^|\.)render\.com$') { throw 'A URL informada nao pertence ao dominio render.com.' }
    return $Valor.Trim()
}

New-Item -ItemType Directory -Force -Path $ConfigDir | Out-Null
if (-not (Test-Path $Arquivo)) {
    if (-not (Test-Path $Modelo)) { throw "Modelo nao encontrado: $Modelo" }
    Copy-Item $Modelo $Arquivo
}

Write-Host 'SHARE IT .CLARA - CONFIGURAR DEPLOY HOOK DO RENDER' -ForegroundColor Cyan
Write-Host 'A URL sera armazenada somente em C:\ConfigSys\Clara\SegredosClara.local.xml.'
Write-Host 'Ela nao sera exibida de volta na tela.'
Write-Host ''
$seguro = Read-Host 'Cole a URL secreta do Deploy Hook e pressione ENTER' -AsSecureString
$url = Validar-Url (Converter-SecureStringParaTexto $seguro)

[xml]$xml = Get-Content $Arquivo
if ($null -eq $xml.SegredosClara) { throw 'XML de segredos invalido: elemento SegredosClara ausente.' }
$no = $xml.SegredosClara.SelectSingleNode('RenderDeployHook')
if ($null -eq $no) {
    $no = $xml.CreateElement('RenderDeployHook')
    $null = $xml.SegredosClara.AppendChild($no)
}
$no.InnerText = $url
$xml.Save($Arquivo)

Write-Host ''
Write-Host '[OK] Deploy Hook persistido com seguranca.' -ForegroundColor Green
Write-Host '[INFO] Agora execute o PUBLICAR-CLARA.cmd normalmente.' -ForegroundColor Cyan
