$ErrorActionPreference = 'Stop'
$workbenchPort = if ($env:PUZZLEAPPLE_WORKBENCH_PORT) { [int]$env:PUZZLEAPPLE_WORKBENCH_PORT } else { 4317 }
$workbenchUrl = "http://127.0.0.1:$workbenchPort"
$workbenchReady = $false
try {
    $workbenchState = Invoke-RestMethod "$workbenchUrl/api/state" -TimeoutSec 2
    $workbenchReady = $workbenchState.snapshot.catalogPath -eq 'Assets/GameData/V3/Opening/OpeningCatalog.asset'
} catch { }
if (-not $workbenchReady) {
    $workbenchNode = (Get-Command node -ErrorAction Stop).Source
    $workbenchScript = Join-Path $PSScriptRoot 'server.mjs'
    $workbenchLog = Join-Path $env:TEMP "PuzzleApple-workbench-$workbenchPort.log"
    $workbenchErrors = Join-Path $env:TEMP "PuzzleApple-workbench-$workbenchPort.errors.log"
    $workbenchProcess = Start-Process -FilePath $workbenchNode -ArgumentList ('"' + $workbenchScript + '"') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput $workbenchLog -RedirectStandardError $workbenchErrors -PassThru
    for ($workbenchAttempt = 0; $workbenchAttempt -lt 30; $workbenchAttempt++) {
        Start-Sleep -Milliseconds 200
        try {
            $workbenchState = Invoke-RestMethod "$workbenchUrl/api/state" -TimeoutSec 2
            if ($workbenchState.snapshot.catalogPath -eq 'Assets/GameData/V3/Opening/OpeningCatalog.asset') { $workbenchReady = $true; break }
        } catch { }
        if ($workbenchProcess.HasExited) { break }
    }
}
if (-not $workbenchReady) { throw "Cannot start the workbench. Check port $workbenchPort and the log in TEMP. Node.js 20+ is required." }
Start-Process $workbenchUrl
