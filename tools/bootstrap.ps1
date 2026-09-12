param([string]$CacheDirectory)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dependencyLock = Get-Content -LiteralPath (Join-Path $projectRoot 'dependencies.lock.json') -Raw | ConvertFrom-Json
if ($dependencyLock.schemaVersion -ne 1) { throw 'Unsupported dependency lock format.' }
if (-not [Environment]::Is64BitProcess) { throw 'Use 64-bit PowerShell on Windows x64.' }
if (-not $CacheDirectory) { $CacheDirectory = Join-Path $projectRoot 'work\downloads' }
$CacheDirectory = [IO.Path]::GetFullPath($CacheDirectory)
New-Item -ItemType Directory -Force -Path $CacheDirectory | Out-Null

function Test-Hash([string]$Path, [string]$Expected) {
    return (Test-Path -LiteralPath $Path -PathType Leaf) -and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.Equals($Expected,[StringComparison]::OrdinalIgnoreCase)
}
function Get-VerifiedFile([string]$Url, [string]$Hash, [string]$Destination) {
    if (Test-Hash $Destination $Hash) { return }
    if (Test-Path -LiteralPath $Destination) { throw ('Cached file checksum differs from dependencies.lock.json: ' + $Destination) }
    $temporary = $Destination + '.' + [Guid]::NewGuid().ToString('N') + '.download'
    try {
        Write-Output ('Downloading pinned dependency: ' + $Url)
        Invoke-WebRequest -Uri $Url -OutFile $temporary -UseBasicParsing
        if (-not (Test-Hash $temporary $Hash)) { throw ('Downloaded file checksum mismatch: ' + $Url) }
        Move-Item -LiteralPath $temporary -Destination $Destination
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
}

$archiveName = [IO.Path]::GetFileName(([Uri]$dependencyLock.zig.archiveUrl).AbsolutePath)
$archive = Join-Path $CacheDirectory $archiveName
Get-VerifiedFile $dependencyLock.zig.archiveUrl $dependencyLock.zig.archiveSha256 $archive
if ((Get-Item -LiteralPath $archive).Length -ne $dependencyLock.zig.archiveBytes) { throw 'Unexpected Zig archive size.' }
$toolchainDirectory = Join-Path $projectRoot 'work\toolchain'
$zig = Join-Path $toolchainDirectory ($dependencyLock.zig.folder + '\zig.exe')
if (-not (Test-Hash $zig $dependencyLock.zig.executableSha256)) {
    if (Test-Path -LiteralPath (Split-Path -Parent $zig)) { throw 'Existing Zig directory differs from the pinned build. Use a clean work directory.' }
    New-Item -ItemType Directory -Force -Path $toolchainDirectory | Out-Null
    Write-Output ('Extracting Zig ' + $dependencyLock.zig.version)
    Expand-Archive -LiteralPath $archive -DestinationPath $toolchainDirectory
    if (-not (Test-Hash $zig $dependencyLock.zig.executableSha256)) { throw 'Extracted Zig executable checksum mismatch.' }
}
$version = (& $zig version).Trim()
if ($LASTEXITCODE -ne 0 -or $version -ne $dependencyLock.zig.version) { throw 'Zig version check failed.' }
Write-Output ('Verified Zig ' + $version)

$sdkDirectory = Join-Path $projectRoot 'work\sdk\win32'
New-Item -ItemType Directory -Force -Path $sdkDirectory | Out-Null
foreach ($header in $dependencyLock.sdk.files) {
    Get-VerifiedFile $header.url $header.sha256 (Join-Path $sdkDirectory $header.name)
}
Write-Output ('Verified SDK headers at commit ' + $dependencyLock.sdk.commit)
Write-Output 'Build dependencies ready. Next: .\build.ps1 -Test and .\tests\native-tests.ps1'
