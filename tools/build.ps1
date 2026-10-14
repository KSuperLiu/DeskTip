param([switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
try {
    dotnet run --project tests/DeskTip.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw '规则测试失败。' }
    if ($FrameworkDependent) {
        dotnet publish src/DeskTip -c Release -r win-x64 --self-contained false -o release/DeskTip-lite -p:DebugType=None -p:DebugSymbols=false
    } else {
        dotnet publish src/DeskTip -c Release -r win-x64 --self-contained true -o release/DeskTip-win-x64 -p:DebugType=None -p:DebugSymbols=false
    }
    if ($LASTEXITCODE -ne 0) { throw '发布失败。' }
    $folder = if ($FrameworkDependent) { 'release/DeskTip-lite' } else { 'release/DeskTip-win-x64' }
    Copy-Item -LiteralPath 'docs/快速开始.txt' -Destination $folder
} finally { Pop-Location }
