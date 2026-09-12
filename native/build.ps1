param([string]$Zig)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $projectRoot 'tools\build-provenance.ps1')
$inputs = Get-NativeSnapshot $projectRoot
if (-not $Zig) { $Zig = Join-Path $projectRoot 'work\toolchain\zig-x86_64-windows-0.16.0\zig.exe' }
if (-not (Test-Path -LiteralPath $Zig)) { throw 'Build tool missing. Run .\tools\bootstrap.ps1 from the repository root, or supply -Zig.' }
$outputDirectory = Join-Path $projectRoot 'work\native'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
& $Zig c++ -target x86_64-windows-gnu -std=c++17 -shared -O2 -fno-exceptions -fno-rtti -nostdlib++ -s (Join-Path $PSScriptRoot 'ChannelFlipApo.cpp') (Join-Path $PSScriptRoot 'ChannelFlipApo.def') -lole32 -ladvapi32 -luuid -o (Join-Path $outputDirectory 'ChannelFlipApo.dll') 2> (Join-Path $outputDirectory 'build.log')
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $outputDirectory 'build.log') -Tail 45; throw 'Native audio core build failed.' }
$nativeBytes = [IO.File]::ReadAllBytes((Join-Path $outputDirectory 'ChannelFlipApo.dll'))
if ($nativeBytes[0] -ne 0x4d -or $nativeBytes[1] -ne 0x5a) { throw 'Native core must be a Windows PE DLL.' }
if ((Get-NativeSnapshot $projectRoot).sha256 -ne $inputs.sha256) { throw 'Native sources changed during compilation.' }
Write-BuildJson (Join-Path $outputDirectory 'BUILD_RECORD.json') ([ordered]@{
    schema=1;inputs=$inputs;compilerSha256=(Get-ArtifactHash $Zig);sha256=(Get-ArtifactHash (Join-Path $outputDirectory 'ChannelFlipApo.dll'))
})
Write-Output ('Built native audio core: ' + (Join-Path $outputDirectory 'ChannelFlipApo.dll'))
