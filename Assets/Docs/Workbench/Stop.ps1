$ErrorActionPreference = 'Stop'
$workbenchUrl = 'http://127.0.0.1:4320'
try { $workbenchState = Invoke-RestMethod "$workbenchUrl/api/state" -TimeoutSec 2 }
catch { Write-Host '工作台未运行。'; exit 0 }
if ($workbenchState.app -ne 'PuzzleApple-V5-Workbench') { throw '该端口不是 V5 工作台，未停止。' }
$body = @{ baseToken = $workbenchState.token } | ConvertTo-Json
Invoke-RestMethod "$workbenchUrl/api/stop" -Method Post -ContentType 'application/json' -Headers @{ Origin = $workbenchUrl; 'X-Workbench-Token' = $workbenchState.csrf } -Body $body | Out-Null
Write-Host '工作台已停止。未保存的浏览器草稿仍保留，重新启动后可恢复。'
