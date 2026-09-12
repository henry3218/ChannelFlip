param([string]$Version)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $projectRoot 'app\ChannelFlip.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the application before packaging.' }
if (-not $Version) { $Version = [Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString(3) + '-preview.1' }
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$') { throw 'Invalid release version.' }
$dist = Join-Path $projectRoot 'dist'
$stage = Join-Path $projectRoot ('work\release-stage\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $dist,$stage | Out-Null
$package = Join-Path $dist ('ChannelFlip-' + $Version + '-windows-x64.zip')
if (Test-Path -LiteralPath $package) { throw ('Package already exists; select a new version or move it before rebuilding: ' + $package) }
Copy-Item -LiteralPath $exe -Destination (Join-Path $stage 'ChannelFlip.exe')
Copy-Item -LiteralPath (Join-Path $projectRoot 'app\使用說明.txt') -Destination (Join-Path $stage 'README.txt')
foreach ($name in @('LICENSE','THIRD_PARTY_NOTICES.txt','FIRST_RUN.md','VALIDATION.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination (Join-Path $stage $name)
}
$gitCommit = $null
if (Get-Command git -ErrorAction SilentlyContinue) {
    $value = & git -C $projectRoot rev-parse --verify HEAD 2>$null
    if ($LASTEXITCODE -eq 0) { $gitCommit = [string]$value }
}
$info = [ordered]@{
    version=$Version
    status='preview; see VALIDATION.md in the source repository for hardware-test evidence'
    sourceCommit=$gitCommit
    applicationSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    dependenciesLockSha256=(Get-FileHash -LiteralPath (Join-Path $projectRoot 'dependencies.lock.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    license='MIT'
}
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
