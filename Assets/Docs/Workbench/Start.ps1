param([switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$workbenchUrl = 'http://127.0.0.1:4320'
function Test-WorkbenchReady {
    try {
        $state = Invoke-RestMethod "$workbenchUrl/api/state" -TimeoutSec 2
        if ($state.app -ne 'PuzzleApple-V5-Workbench') { return $false }
        if ($state.doc.schemaVersion -eq 3) { return $true }
        if ($state.doc.schemaVersion -in @(1,2)) { & (Join-Path $PSScriptRoot 'Stop.ps1') }
        return $false
    }
    catch { return $false }
}
if (!(Test-WorkbenchReady)) {
    $nodePath = (Get-Command node.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    if (!$nodePath) {
        foreach ($package in (Get-AppxPackage '*CodexPrimaryRuntime*' | Sort-Object Version -Descending)) {
            $candidate = Join-Path $package.InstallLocation 'dependencies\node\bin\node.exe'
            if (Test-Path -LiteralPath $candidate) { $nodePath = $candidate; break }
        }
    }
    if (!$nodePath) { throw '找不到 Node.js，请安装 Node.js 20 或更新版本。' }
    $serverFile = Join-Path $PSScriptRoot 'server.mjs'
    $log = Join-Path $env:TEMP ('PuzzleApple-workbench-' + $PID + '.log')
    $process = Start-Process -FilePath $nodePath -ArgumentList ('"' + $serverFile + '"') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardError $log -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if (Test-WorkbenchReady) { $ready = $true; break }
        if ($process.HasExited) { break }
        Start-Sleep -Milliseconds 200
    }
    if (!$ready) { throw "工作台未能启动。请检查端口 4320 和日志 $log" }
}
if (!$NoBrowser) { Start-Process -FilePath $workbenchUrl }
