param([string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $projectRoot 'app' }
. (Join-Path $projectRoot 'tools\build-provenance.ps1')
$verified = Read-VerifiedBuild $BuildDirectory
$fixture = Join-Path $projectRoot ('work\package-tests\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $fixture | Out-Null
$packager = Join-Path $projectRoot 'tools\package-release.ps1'
$script:packageChecks = 0
function Copy-Fixture([string]$Name) {
    $directory = Join-Path $fixture $Name
    New-Item -ItemType Directory -Path $directory | Out-Null
    Copy-Item -LiteralPath (Join-Path $BuildDirectory 'ChannelFlip.exe'),(Join-Path $BuildDirectory 'BUILD_RECORD.json'),(Join-Path $BuildDirectory 'BUILD_CONTEXT.json'),(Join-Path $BuildDirectory 'TEST_RESULTS.json') -Destination $directory
    $directory
}
function Reject([string]$Name,[string]$Directory,[string]$Version,[string]$Message) {
    $rejected = $false
    try { & $packager -BuildDirectory $Directory -Version $Version -DestinationDirectory (Join-Path $fixture 'rejected') }
    catch { if ($_.Exception.Message -notlike ('*' + $Message + '*')) { throw }; $rejected = $true }
    if (-not $rejected) { throw ('Packaging unexpectedly accepted ' + $Name) }
    $script:packageChecks++; Write-Output ('PASS ' + $Name)
}
$version = $verified.Context.version + '-preview.1'
Reject 'release version mismatch' $BuildDirectory '999.0.0-preview.1' 'Release version differs'
$changed = Copy-Fixture 'changed-exe'
$stream = [IO.File]::OpenWrite((Join-Path $changed 'ChannelFlip.exe'))
try { $stream.Seek(0,[IO.SeekOrigin]::End) | Out-Null; $stream.WriteByte(42) } finally { $stream.Dispose() }
Reject 'replaced EXE' $changed $version 'EXE hash does not match'
$recordPath = Join-Path $changed 'BUILD_RECORD.json'
$record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
$record.applicationSha256 = Get-ArtifactHash (Join-Path $changed 'ChannelFlip.exe')
Write-BuildJson $recordPath $record
Reject 'changed EXE with a stale test receipt' $changed $version 'have not passed'
$wrong = Copy-Fixture 'wrong-record-version'
$recordPath = Join-Path $wrong 'BUILD_RECORD.json'
$record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
$record.version = '999.0.0'; Write-BuildJson $recordPath $record
Reject 'build record version mismatch' $wrong $version 'EXE version differs'
$incomplete = Copy-Fixture 'incomplete-tests'
$receiptPath = Join-Path $incomplete 'TEST_RESULTS.json'
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
$receipt.native = 'failed'; Write-BuildJson $receiptPath $receipt
Reject 'failed native validation' $incomplete $version 'have not passed'

# A different checkout/HEAD at packaging time cannot supply build provenance.
& {
    function git { throw 'Packaging must not query the current Git HEAD.' }
    & $packager -BuildDirectory $BuildDirectory -Version $version -DestinationDirectory (Join-Path $fixture 'valid')
}
$zip = Join-Path $fixture ('valid\ChannelFlip-' + $version + '-windows-x64.zip')
$extracted = Join-Path $fixture 'extracted'
Expand-Archive -LiteralPath $zip -DestinationPath $extracted
$info = Get-Content -LiteralPath (Join-Path $extracted 'BUILD_INFO.json') -Raw | ConvertFrom-Json
if ($info.sourceCommit -ne $verified.Context.source.commit -or $info.sourceSha256 -ne $verified.Context.source.sha256 -or (Get-ArtifactHash (Join-Path $extracted 'ChannelFlip.exe')) -ne $verified.Record.applicationSha256) { throw 'Package lost the build origin or tested EXE.' }
$script:packageChecks++; Write-Output 'PASS packaging uses embedded build source without querying HEAD and retains the tested EXE'
foreach ($line in Get-Content -LiteralPath (Join-Path $extracted 'SHA256SUMS.txt')) {
    $parts = $line -split '  ',2
    if ($parts.Count -ne 2 -or (Get-ArtifactHash (Join-Path $extracted $parts[1])) -ne $parts[0]) { throw 'Packaged checksum mismatch.' }
}
$script:packageChecks++; Write-Output 'PASS all packaged file checksums match'
Write-Output ('RESULT: ' + $script:packageChecks + ' packaging checks passed; 0 failed')
