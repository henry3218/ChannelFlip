param([string]$Version, [string]$BuildDirectory, [string]$DestinationDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $projectRoot 'tools\build-provenance.ps1')
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $projectRoot 'app' }
$build = Read-VerifiedBuild $BuildDirectory
$exe = $build.Exe
if (-not $Version) { $Version = $build.Context.version + '-preview.1' }
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$') { throw 'Invalid release version.' }
if (($Version -split '-',2)[0] -ne $build.Context.version) { throw 'Release version differs from the built EXE.' }
$tests = Get-Content -LiteralPath (Join-Path $BuildDirectory 'TEST_RESULTS.json') -Raw | ConvertFrom-Json
if ($tests.schema -ne 1 -or $tests.applicationSha256 -ne $build.Record.applicationSha256 -or $tests.nativeSha256 -ne $build.Record.nativeSha256 -or
    $tests.contextSha256 -ne $build.Record.contextSha256 -or $tests.managed -ne 'passed' -or $tests.native -ne 'passed' -or $tests.faultInjection -ne 'passed') { throw 'This exact EXE and core have not passed build -Test.' }
$dist = if ($DestinationDirectory) { [IO.Path]::GetFullPath($DestinationDirectory) } else { Join-Path $projectRoot 'dist' }
$stage = Join-Path $projectRoot ('work\release-stage\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $dist,$stage | Out-Null
$package = Join-Path $dist ('ChannelFlip-' + $Version + '-windows-x64.zip')
if (Test-Path -LiteralPath $package) { throw ('Package already exists; select a new version or move it before rebuilding: ' + $package) }
Copy-Item -LiteralPath $exe -Destination (Join-Path $stage 'ChannelFlip.exe')
if ((Get-ArtifactHash (Join-Path $stage 'ChannelFlip.exe')) -ne $tests.applicationSha256) { throw 'EXE changed while staging the package.' }
$documents = [ordered]@{'app/使用說明.txt'='README.txt';'app/README.en.txt'='README.en.txt';'LICENSE'='LICENSE';'THIRD_PARTY_NOTICES.txt'='THIRD_PARTY_NOTICES.txt';'FIRST_RUN.md'='FIRST_RUN.md';'FIRST_RUN.en.md'='FIRST_RUN.en.md';'VALIDATION.md'='VALIDATION.md';'VALIDATION.en.md'='VALIDATION.en.md'}
foreach ($sourcePath in $documents.Keys) {
    $inputRecord = @($build.Context.source.files | Where-Object { $_.path -eq $sourcePath })
    $destination = Join-Path $stage $documents[$sourcePath]
    Copy-Item -LiteralPath (Join-Path $projectRoot $sourcePath) -Destination $destination
    if ($inputRecord.Count -ne 1 -or (Get-ArtifactHash $destination) -ne $inputRecord[0].sha256) { throw ('Package document differs from the build source: ' + $sourcePath) }
}
$info = [ordered]@{
    schema=1
    version=$Version
    status='preview; see VALIDATION.md in the source repository for hardware-test evidence'
    sourceCommit=$build.Context.source.commit
    sourceDirty=$build.Context.source.dirty
    sourceSha256=$build.Context.source.sha256
    buildId=$build.Context.buildId
    builtUtc=$build.Context.builtUtc
    applicationSha256=$build.Record.applicationSha256
    nativeSha256=$build.Record.nativeSha256
    license='MIT'
    interfaceLanguages=@('zh-TW','en')
}
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'BUILD_RECORD.json'),(Join-Path $BuildDirectory 'TEST_RESULTS.json') -Destination $stage
Write-BuildJson (Join-Path $stage 'BUILD_CONTEXT.json') $build.Context
[IO.File]::WriteAllText((Join-Path $stage 'BUILD_INFO.json'),($info | ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
$checksums = Get-ChildItem -LiteralPath $stage -File | Sort-Object Name | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
}
[IO.File]::WriteAllLines((Join-Path $stage 'SHA256SUMS.txt'),[string[]]$checksums,[Text.UTF8Encoding]::new($false))
Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $stage -File | ForEach-Object FullName) -DestinationPath $package
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($package + '.sha256.txt',$hash + '  ' + [IO.Path]::GetFileName($package) + "`n",[Text.UTF8Encoding]::new($false))
Write-Output ('Created local preview package: ' + $package)
Write-Output ('SHA256: ' + $hash)
