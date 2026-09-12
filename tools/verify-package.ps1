param([switch]$SkipDeviceDiagnostics)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$isolated = Join-Path $projectRoot ('work\standalone-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $isolated | Out-Null
$exe = Join-Path $isolated 'ChannelFlip.exe'
Copy-Item -LiteralPath (Join-Path $projectRoot 'app\ChannelFlip.exe') -Destination $exe
$operations = @(@('--render-preview','preview-v2.png'),@('--licenses','notices-from-exe.txt'))
if (-not $SkipDeviceDiagnostics) { $operations += ,@('--diagnose','diagnostic-v2.txt') }
foreach ($operation in $operations) {
    $report = Join-Path $projectRoot ('work\' + $operation[1])
    $process = Start-Process -FilePath $exe -WorkingDirectory $isolated -ArgumentList @($operation[0],('"' + $report + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw ('Standalone execution failed: ' + $operation[0]) }
    Write-Output ('PASS single EXE ' + $operation[0])
}
if (@(Get-ChildItem -LiteralPath $isolated).Count -ne 1) { throw 'Standalone directory should contain only the EXE.' }
$assembly = [Reflection.Assembly]::LoadFile($exe)
$coreStream = $assembly.GetManifestResourceStream('ChannelFlip.Native.dll')
$memory = [IO.MemoryStream]::new()
$coreStream.CopyTo($memory)
$nativeData = $memory.ToArray()
$coreStream.Dispose()
$memory.Dispose()
$builtData = [IO.File]::ReadAllBytes((Join-Path $projectRoot 'work\native\ChannelFlipApo.dll'))
if ([Convert]::ToBase64String($nativeData) -ne [Convert]::ToBase64String($builtData)) { throw 'Embedded DLL differs from tested build.' }
Write-Output 'PASS embedded native DLL is byte-identical to the tested build'
function U16([int]$offset) { [BitConverter]::ToUInt16($nativeData,$offset) }
function U32([int]$offset) { [BitConverter]::ToUInt32($nativeData,$offset) }
$pe = U32 0x3c
$sectionCount = U16 ($pe + 6)
$optional = $pe + 24
$sectionStart = $optional + (U16 ($pe + 20))
if ((U16 ($pe + 4)) -ne 0x8664 -or (U16 $optional) -ne 0x20b -or ((U16 ($pe + 22)) -band 0x2000) -eq 0) { throw 'Expected x64 PE DLL.' }
function Offset([uint32]$rva) {
    for ($index = 0; $index -lt $sectionCount; $index++) {
        $section = $sectionStart + $index * 40
        $start = U32 ($section + 12)
        $size = [Math]::Max((U32 ($section + 8)),(U32 ($section + 16)))
        if ($rva -ge $start -and $rva -lt ($start + $size)) { return [int]((U32 ($section + 20)) + $rva - $start) }
    }
    throw 'Invalid PE address.'
}
$descriptor = Offset (U32 ($optional + 120))
$imports = @()
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class SystemDllCheck {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr LoadLibraryEx(string name,IntPtr file,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern uint GetModuleFileName(IntPtr module,StringBuilder path,uint size);
    [DllImport("kernel32.dll")] public static extern bool FreeLibrary(IntPtr module);
}
'@
while ((U32 ($descriptor + 12)) -ne 0) {
    $nameOffset = Offset (U32 ($descriptor + 12))
    $end = $nameOffset
    while ($nativeData[$end] -ne 0) { $end++ }
    $name = [Text.Encoding]::ASCII.GetString($nativeData,$nameOffset,$end-$nameOffset)
    $module = [SystemDllCheck]::LoadLibraryEx($name,[IntPtr]::Zero,0x800)
    if ($module -eq [IntPtr]::Zero) { throw ('Windows cannot resolve dependency: ' + $name) }
    try {
        $resolved = [Text.StringBuilder]::new(1024)
        [void][SystemDllCheck]::GetModuleFileName($module,$resolved,1024)
        if (-not $resolved.ToString().StartsWith((Join-Path $env:WINDIR 'System32\'),[StringComparison]::OrdinalIgnoreCase)) { throw ('Non-system DLL dependency: ' + $resolved) }
    } finally { [void][SystemDllCheck]::FreeLibrary($module) }
    $imports += $name
    $descriptor += 20
}
Write-Output ('PASS native dependencies exist in Windows System32: ' + ($imports -join ', '))
Get-FileHash -LiteralPath (Join-Path $projectRoot 'app\ChannelFlip.exe'),(Join-Path $projectRoot 'work\native\ChannelFlipApo.dll') -Algorithm SHA256 | Format-Table -AutoSize
