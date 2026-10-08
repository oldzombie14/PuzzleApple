$ErrorActionPreference = 'Stop'
$workbenchScript = (Resolve-Path (Join-Path $PSScriptRoot 'server.mjs')).Path
Get-CimInstance Win32_Process -Filter "name = 'node.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Contains('"' + $workbenchScript + '"')
} | ForEach-Object { Stop-Process -Id $_.ProcessId }
