param([string]$Zig)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Zig) { $Zig = Join-Path $projectRoot 'work\toolchain\zig-x86_64-windows-0.16.0\zig.exe' }
$sdk = Join-Path $projectRoot 'work\sdk\win32'
if (-not (Test-Path -LiteralPath $Zig) -or -not (Test-Path -LiteralPath (Join-Path $sdk 'audioenginebaseapo.h'))) { throw 'Run .\tools\bootstrap.ps1 before native tests.' }
$output = Join-Path $projectRoot 'work\native\ChannelFlip.NativeTests.exe'
& $Zig c++ -target x86_64-windows-gnu -std=c++17 -O2 -fno-exceptions -fno-rtti -static -municode -I $sdk (Join-Path $PSScriptRoot 'NativeTests.cpp') -lole32 -ladvapi32 -luuid -luser32 -o $output 2> (Join-Path $projectRoot 'work\native-test-build.log')
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $projectRoot 'work\native-test-build.log') -Tail 35; throw 'Native test compilation failed.' }
& $output (Join-Path $projectRoot 'work\native\ChannelFlipApo.dll') (Join-Path $projectRoot 'work\native\test-state.bin') | Tee-Object -FilePath (Join-Path $projectRoot 'work\native-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Native tests failed.' }
