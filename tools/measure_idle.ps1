param([string]$Executable = (Join-Path $PSScriptRoot '../release/DeskTip-win-x64/DeskTip.exe'), [int]$Seconds = 45)
$ErrorActionPreference = 'Stop'
if (Get-Process DeskTip -ErrorAction SilentlyContinue) { throw '请先退出正在运行的 DeskTip，再执行后台测量。' }
$process = Start-Process -FilePath $Executable -ArgumentList '--background' -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 3
    $process.Refresh()
    $cpuStart = $process.TotalProcessorTime.TotalSeconds
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Seconds $Seconds
    $process.Refresh()
    $elapsed = $watch.Elapsed.TotalSeconds
    $result = [ordered]@{
        measuredAt = (Get-Date).ToString('o')
        scenario = 'fresh background start, no settings window, no reminder popup'
        elapsedSeconds = [Math]::Round($elapsed, 2)
        cpuSeconds = [Math]::Round($process.TotalProcessorTime.TotalSeconds - $cpuStart, 6)
        cpuPercentOfOneCore = [Math]::Round(100 * ($process.TotalProcessorTime.TotalSeconds - $cpuStart) / $elapsed, 4)
        workingSetMB = [Math]::Round($process.WorkingSet64 / 1MB, 2)
        privateMemoryMB = [Math]::Round($process.PrivateMemorySize64 / 1MB, 2)
        handles = $process.HandleCount
        runtime = 'Windows x64, WPF .NET 8 self-contained Release'
    }
    $folder = Join-Path $PSScriptRoot '../artifacts'
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'performance.json') -Encoding UTF8
    $result | ConvertTo-Json
} finally {
    Start-Process -FilePath $Executable -ArgumentList '--quit' -WindowStyle Hidden -Wait
    $process.WaitForExit(5000) | Out-Null
    $process.Dispose()
}
