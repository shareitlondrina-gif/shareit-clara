[CmdletBinding()]
param()
$ErrorActionPreference='SilentlyContinue'
$Raiz=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Logs=Join-Path $Raiz 'logs';New-Item -ItemType Directory -Force -Path $Logs|Out-Null
$Out=Join-Path $Logs 'DIAGNOSTICO-COEXISTENCIA-ULTIMO.txt'
$l=New-Object System.Collections.Generic.List[string]
function A([string]$x){$null=$l.Add($x);Write-Host $x}
A 'SHARE IT - DIAGNOSTICO DE COEXISTENCIA .CLARA + .VISAO'
A ("Data: "+(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
A ''
$servicos=@('ShareIT.Clara.Agente','ShareIT.Visao.Agente','ShareIT.Visao.Manutencao')
foreach($s in $servicos){$svc=Get-Service -Name $s -ErrorAction SilentlyContinue;if($svc){A ("Servico {0}: {1} / inicio={2}" -f $s,$svc.Status,$svc.StartType)}else{A ("Servico {0}: nao instalado" -f $s)}}
A ''
$pastas=@('C:\Program Files\Share IT\Clara','C:\ProgramData\ShareIT\Clara','C:\Program Files\ShareIT\Visao','C:\ProgramData\ShareIT\Visao')
foreach($p in $pastas){A ("Pasta {0}: {1}" -f $p,(Test-Path $p))}
A ''
$proc=Get-Process | Where-Object {$_.ProcessName -match 'ShareIT\.(Clara|Visao)'} | Select-Object ProcessName,Id,CPU,WorkingSet64
if($proc){A 'Processos:';foreach($p in $proc){A ("  {0} PID={1} CPU={2} RAM={3:N0} MB" -f $p.ProcessName,$p.Id,$p.CPU,($p.WorkingSet64/1MB))}}else{A 'Processos Share IT nao identificados neste momento.'}
A ''
A 'Interpretacao: os produtos usam nomes de servico e pastas diferentes. Este diagnostico observa coexistencia; nao altera configuracoes.'
$l | Set-Content -Path $Out -Encoding UTF8
