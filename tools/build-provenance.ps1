# Shared by build, verification and packaging. These records describe provenance;
# they are integrity checks, not a replacement for code signing.
function Get-ArtifactHash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Get-TextHash([string]$Value) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))).Replace('-','').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}
function Write-BuildJson([string]$Path, $Value) {
    $temporary = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 12))
        $stream = [IO.FileStream]::new($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
        if ([IO.File]::Exists($Path)) { [IO.File]::Replace($temporary,$Path,[NullString]::Value) } else { [IO.File]::Move($temporary,$Path) }
    } finally { if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) } }
}
function Get-InputSnapshot([string]$Root, [string[]]$Paths) {
    $inputs = @($Paths | Sort-Object -Unique | ForEach-Object {
        [ordered]@{path=$_;sha256=(Get-ArtifactHash (Join-Path $Root $_))}
    })
    [ordered]@{sha256=(Get-TextHash (($inputs | ForEach-Object { $_.sha256 + '  ' + $_.path }) -join "`n"));files=$inputs}
}
function Get-NativeSnapshot([string]$Root) {
    $paths = @(Get-ChildItem -LiteralPath (Join-Path $Root 'native') -File | ForEach-Object { 'native/' + $_.Name })
    $paths += @('dependencies.lock.json','tools/build-provenance.ps1')
    Get-InputSnapshot $Root $paths
}
function Get-SourceSnapshot([string]$Root) {
    $commit = & git -C $Root rev-parse --verify HEAD
    if ($LASTEXITCODE -ne 0) { throw 'A Git checkout is required to record the build source.' }
    $tree = & git -C $Root rev-parse 'HEAD^{tree}'
    $status = @(& git -C $Root status --porcelain)
    $paths = @(& git -C $Root -c core.quotepath=false ls-files --cached --others --exclude-standard | Where-Object { Test-Path -LiteralPath (Join-Path $Root $_) -PathType Leaf })
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate source inputs.' }
    $snapshot = Get-InputSnapshot $Root $paths
    [ordered]@{commit=[string]$commit;tree=[string]$tree;dirty=($status.Count -gt 0);sha256=$snapshot.sha256;files=$snapshot.files}
}
function Read-VerifiedBuild([string]$Directory) {
    $exe = Join-Path $Directory 'ChannelFlip.exe'
    $record = Get-Content -LiteralPath (Join-Path $Directory 'BUILD_RECORD.json') -Raw | ConvertFrom-Json
    if ($record.schema -ne 1 -or (Get-ArtifactHash $exe) -ne $record.applicationSha256) { throw 'EXE hash does not match its build record.' }
    $assembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($exe))
    $stream = $assembly.GetManifestResourceStream('ChannelFlip.Build.json')
    if (-not $stream) { throw 'EXE has no embedded build provenance.' }
    $reader = [IO.StreamReader]::new($stream)
    try { $contextText = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ((Get-TextHash $contextText) -ne $record.contextSha256) { throw 'Embedded provenance differs from the build record.' }
    $context = $contextText | ConvertFrom-Json
    if ($context.version -ne $assembly.GetName().Version.ToString(3) -or $record.version -ne $context.version) { throw 'EXE version differs from the build record.' }
    $core = $assembly.GetManifestResourceStream('ChannelFlip.Native.dll')
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $coreHash = [BitConverter]::ToString($hash.ComputeHash($core)).Replace('-','').ToLowerInvariant() }
    finally { $hash.Dispose(); $core.Dispose() }
    if ($coreHash -ne $record.nativeSha256 -or $coreHash -ne $context.nativeSha256) { throw 'Embedded core differs from the build record.' }
    [pscustomobject]@{Record=$record;Context=$context;Exe=$exe}
}
