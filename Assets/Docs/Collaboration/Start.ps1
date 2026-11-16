$ErrorActionPreference = 'Stop'
$boardUrl = 'http://127.0.0.1:4318'

function Test-BoardReady {
    try {
        $state = Invoke-RestMethod "$boardUrl/board" -TimeoutSec 2
        return $state.app -eq 'PuzzleApple-Collaboration'
    } catch { return $false }
}

if (!(Test-BoardReady)) {
    $nodeCommand = Get-Command node.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    $nodePath = $nodeCommand.Source
    if (!$nodePath) {
        $packages = Get-AppxPackage '*CodexPrimaryRuntime*' | Sort-Object Version -Descending
        foreach ($package in $packages) {
            $candidate = Join-Path $package.InstallLocation 'dependencies\node\bin\node.exe'
            if (Test-Path -LiteralPath $candidate) { $nodePath = $candidate; break }
        }
    }
    if (!$nodePath) { throw '找不到 Node.js。请安装 Node.js 20 或更高版本，再双击启动清单。' }
    $serverFile = Join-Path $PSScriptRoot 'server.mjs'
    $log = Join-Path $env:TEMP ('PuzzleApple-board-' + $PID + '.log')
    $process = Start-Process -FilePath $nodePath -ArgumentList ('"' + $serverFile + '"') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardError $log -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if (Test-BoardReady) { $ready = $true; break }
        if ($process.HasExited) { break }
        Start-Sleep -Milliseconds 200
    }
    if (!$ready) { throw "清单服务未能启动。请检查端口 4318 和日志 $log" }
}

try { Start-Process -FilePath $boardUrl }
catch { throw "请在浏览器中打开 $boardUrl" }
