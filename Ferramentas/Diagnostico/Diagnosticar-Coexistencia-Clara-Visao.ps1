# Clara
# Programador: Paulo Andrade
# Objetivo: diagnosticar coexistência entre Clara e VISÃO sem alterar nenhum produto.
# Origem: serviços, processos e diretórios instalados na estação Windows.
# Destino: logs\DIAGNOSTICO-COEXISTENCIA-ULTIMO.txt.
# Boas práticas: somente leitura, descoberta dinâmica e nenhuma dependência do nome interno usado pelo VISÃO.

[CmdletBinding()]
param()
$ErrorActionPreference='SilentlyContinue'
$Raiz=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Logs=Join-Path $Raiz 'logs';New-Item -ItemType Directory -Force -Path $Logs|Out-Null
$Out=Join-Path $Logs 'DIAGNOSTICO-COEXISTENCIA-ULTIMO.txt'
$l=New-Object System.Collections.Generic.List[string]
function A([string]$x){$null=$l.Add($x);Write-Host $x}
A 'Clara - DIAGNOSTICO DE COEXISTENCIA CLARA + VISAO'
A ("Data: "+(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
A ''
$servicos=Get-CimInstance Win32_Service | Where-Object { $_.Name -match 'Clara|Visao' -or $_.PathName -match 'Clara|Visao' }
if($servicos){A 'Servicos relacionados:';foreach($s in $servicos){A ("  {0}: estado={1}; inicio={2}; caminho={3}" -f $s.Name,$s.State,$s.StartMode,$s.PathName)}}else{A 'Nenhum servica Clara/VISAO identificado.'}
A ''
$pastas=@('C:\Program Files\Clara','C:\ProgramData\Clara')
foreach($pasta in $pastas){A ("Pasta Clara {0}: {1}" -f $pasta,(Test-Path $pasta))}
A ''
$proc=Get-Process | Where-Object {$_.ProcessName -match 'Clara|Visao'} | Select-Object ProcessName,Id,CPU,WorkingSet64
if($proc){A 'Processos relacionados:';foreach($item in $proc){A ("  {0} PID={1} CPU={2} RAM={3:N0} MB" -f $item.ProcessName,$item.Id,$item.CPU,($item.WorkingSet64/1MB))}}else{A 'Processos Clara/VISAO nao identificados neste momento.'}
A ''
A 'Interpretacao: o diagnostico apenas observa possiveis conflitos de servico, processo e pasta; nenhuma configuracao e alterada.'
$l | Set-Content -Path $Out -Encoding UTF8
