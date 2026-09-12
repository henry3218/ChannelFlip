param([switch]$Test, [switch]$SkipNative, [string]$Zig, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
. (Join-Path $projectRoot 'tools\build-provenance.ps1')
$source = Get-SourceSnapshot $projectRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'app' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x (64-bit) is required.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory,(Join-Path $projectRoot 'work') | Out-Null
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Xml.dll','System.Xml.Linq.dll','System.ServiceProcess.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','UIAutomationProvider.dll','UIAutomationTypes.dll') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF\' + $_)) }
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$target = Join-Path $OutputDirectory 'ChannelFlip.exe'
$resources = @('/resource:' + (Join-Path $projectRoot 'src\Translations.xml') + ',ChannelFlip.Translations.xml')
if (-not $SkipNative) { & (Join-Path $projectRoot 'native\build.ps1') -Zig $Zig }
$nativeRecord = Get-Content -LiteralPath (Join-Path $projectRoot 'work\native\BUILD_RECORD.json') -Raw | ConvertFrom-Json
$nativeHash = Get-ArtifactHash (Join-Path $projectRoot 'work\native\ChannelFlipApo.dll')
if ($nativeRecord.schema -ne 1 -or $nativeRecord.sha256 -ne $nativeHash -or $nativeRecord.inputs.sha256 -ne (Get-NativeSnapshot $projectRoot).sha256) { throw 'Native build is stale or has been replaced. Rebuild without -SkipNative.' }
$versionMatch = [regex]::Match([IO.File]::ReadAllText((Join-Path $projectRoot 'src\Program.cs')), 'AssemblyVersion\("(\d+\.\d+\.\d+)\.\d+"\)')
if (-not $versionMatch.Success) { throw 'Assembly version is missing.' }
$context = [ordered]@{schema=1;buildId=[Guid]::NewGuid().ToString('N');builtUtc=[DateTime]::UtcNow.ToString('o');version=$versionMatch.Groups[1].Value;source=$source;nativeSha256=$nativeHash;compilerSha256=(Get-ArtifactHash $compiler);nativeCompilerSha256=$nativeRecord.compilerSha256}
$contextPath = Join-Path $OutputDirectory 'BUILD_CONTEXT.json'
Write-BuildJson $contextPath $context
$resources += '/resource:' + $contextPath + ',ChannelFlip.Build.json'
& (Join-Path $projectRoot 'tools\create-icon.ps1') -OutputPath (Join-Path $projectRoot 'app\ChannelFlip.ico')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 ('/out:' + $target) ('/win32icon:' + (Join-Path $projectRoot 'app\ChannelFlip.ico')) ('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')) ('/resource:' + (Join-Path $projectRoot 'src\MainWindow.xaml') + ',ChannelFlip.MainWindow.xaml') ('/resource:' + (Join-Path $projectRoot 'work\native\ChannelFlipApo.dll') + ',ChannelFlip.Native.dll') ('/resource:' + (Join-Path $projectRoot 'app\ChannelFlip.ico') + ',ChannelFlip.Icon') ('/resource:' + (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.txt') + ',ChannelFlip.Notices') ('/resource:' + (Join-Path $projectRoot 'LICENSE') + ',ChannelFlip.License') @references @resources @sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
if ((Get-SourceSnapshot $projectRoot).sha256 -ne $source.sha256) { throw 'Sources changed during compilation. Build again.' }
$applicationHash = Get-ArtifactHash $target
Write-BuildJson (Join-Path $OutputDirectory 'BUILD_RECORD.json') ([ordered]@{schema=1;version=$context.version;applicationSha256=$applicationHash;nativeSha256=$nativeHash;contextSha256=(Get-TextHash ([IO.File]::ReadAllText($contextPath)))})
Write-Output ('Built: ' + $target)
if ($Test) {
    $testTarget = Join-Path $projectRoot 'work\ChannelFlip.Tests.exe'
    $testSources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -Filter '*Tests.cs' | ForEach-Object FullName
    & $compiler /nologo /target:exe /platform:x64 /optimize+ ('/out:' + $testTarget) ('/r:' + $target) @references @testSources
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    Copy-Item -LiteralPath $target -Destination (Join-Path $projectRoot 'work\ChannelFlip.exe')
    & $testTarget (Join-Path $projectRoot 'work\tests')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & (Join-Path $projectRoot 'tests\native-tests.ps1') -Zig $Zig
    if ((Get-ArtifactHash $target) -ne $applicationHash -or (Get-ArtifactHash (Join-Path $projectRoot 'work\native\ChannelFlipApo.dll')) -ne $nativeHash -or (Get-SourceSnapshot $projectRoot).sha256 -ne $source.sha256) { throw 'Source or tested artifacts changed during validation.' }
    Write-BuildJson (Join-Path $OutputDirectory 'TEST_RESULTS.json') ([ordered]@{schema=1;applicationSha256=$applicationHash;nativeSha256=$nativeHash;contextSha256=(Get-TextHash ([IO.File]::ReadAllText($contextPath)));managed='passed';native='passed';faultInjection='passed';completedUtc=[DateTime]::UtcNow.ToString('o')})
}
