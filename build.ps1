param([switch]$Test, [switch]$SkipNative, [string]$Zig)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x (64-bit) is required.' }
New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'app'),(Join-Path $projectRoot 'work') | Out-Null
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Xml.dll','System.Xml.Linq.dll','System.ServiceProcess.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','UIAutomationProvider.dll','UIAutomationTypes.dll') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF\' + $_)) }
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$target = Join-Path $projectRoot 'app\ChannelFlip.exe'
if (-not $SkipNative) { & (Join-Path $projectRoot 'native\build.ps1') -Zig $Zig }
& (Join-Path $projectRoot 'tools\create-icon.ps1') -OutputPath (Join-Path $projectRoot 'app\ChannelFlip.ico')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 ('/out:' + $target) ('/win32icon:' + (Join-Path $projectRoot 'app\ChannelFlip.ico')) ('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')) ('/resource:' + (Join-Path $projectRoot 'src\MainWindow.xaml') + ',ChannelFlip.MainWindow.xaml') ('/resource:' + (Join-Path $projectRoot 'work\native\ChannelFlipApo.dll') + ',ChannelFlip.Native.dll') ('/resource:' + (Join-Path $projectRoot 'app\ChannelFlip.ico') + ',ChannelFlip.Icon') ('/resource:' + (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.txt') + ',ChannelFlip.Notices') ('/resource:' + (Join-Path $projectRoot 'LICENSE') + ',ChannelFlip.License') @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Write-Output ('Built: ' + $target)
if ($Test) {
    $testTarget = Join-Path $projectRoot 'work\ChannelFlip.Tests.exe'
    & $compiler /nologo /target:exe /platform:x64 /optimize+ ('/out:' + $testTarget) ('/r:' + $target) @references (Join-Path $projectRoot 'tests\Tests.cs') (Join-Path $projectRoot 'tests\UiTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    Copy-Item -LiteralPath $target -Destination (Join-Path $projectRoot 'work\ChannelFlip.exe')
    & $testTarget (Join-Path $projectRoot 'work\tests')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
