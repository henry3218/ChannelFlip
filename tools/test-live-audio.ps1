param([Parameter(Mandatory = $true)][string]$EndpointId, [switch]$VerifyInstalled)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$app = Join-Path $projectRoot 'app\ChannelFlip.exe'
$output = Join-Path $projectRoot 'work\ChannelFlip.LiveAudio.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /optimize+ ('/out:' + $output) ('/r:' + $app) ('/r:' + (Join-Path $framework 'System.Core.dll')) (Join-Path $projectRoot 'tests\LiveAudio.cs')
if ($LASTEXITCODE -ne 0) { throw 'Live test compilation failed.' }
Copy-Item -LiteralPath $app -Destination (Join-Path $projectRoot 'work\ChannelFlip.exe')
$testArguments = @($EndpointId)
if ($VerifyInstalled) { $testArguments += '--verify-installed' }
& $output @testArguments
if ($LASTEXITCODE -ne 0) { throw 'Live audio verification failed.' }
