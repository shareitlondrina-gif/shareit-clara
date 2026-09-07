$ErrorActionPreference='Continue'
$Dados='C:\ProgramData\ShareIT\Clara'
Write-Host '=== Share IT .Clara - Diagnostico do Agente ===' -ForegroundColor Cyan
Get-Service 'ShareIT.Clara.Agente' -ErrorAction SilentlyContinue | Format-List Name,DisplayName,Status,StartType
Write-Host "`nTarefa da sessao:"
Get-ScheduledTask -TaskName 'Share IT .Clara - Sessao' -ErrorAction SilentlyContinue | Format-List TaskName,State
Write-Host "`nConfiguracao:"
if (Test-Path (Join-Path $Dados 'configuracao-agente.json')) { Get-Content (Join-Path $Dados 'configuracao-agente.json') | ForEach-Object { $_ -replace '"tokenInstalacao"\s*:\s*"[^"]*"','"tokenInstalacao":"***"' } }
Write-Host "`nIdentidade protegida: $([bool](Test-Path (Join-Path $Dados 'segredos\identidade.dat')))"
Write-Host "Fila offline: $([bool](Test-Path (Join-Path $Dados 'fila\periodos.jsonl')))"
Write-Host "Atualizacoes: $([bool](Test-Path (Join-Path $Dados 'atualizacoes')))"
