param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'work\ui-validation' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$exe = Join-Path $projectRoot 'app\ChannelFlip.exe'
$scenarios = @('setup','waiting','processing','confirmed-idle','off','offline','empty','core-error','read-error','enhancements-off','unsupported','test-failed','busy','long-name')
$cases = @()
foreach ($scenario in $scenarios) {
    foreach ($size in @(@(584,600),@(664,860))) {
        $cases += [pscustomobject]@{Scenario=$scenario;Width=$size[0];Height=$size[1];Dpi=96;Theme='normal'}
    }
}
foreach ($scenario in @('setup','core-error','long-name')) {
    foreach ($dpi in @(120,144,192)) { $cases += [pscustomobject]@{Scenario=$scenario;Width=584;Height=600;Dpi=$dpi;Theme='normal'} }
}
foreach ($scenario in @('setup','processing','core-error','offline','empty')) {
    $cases += [pscustomobject]@{Scenario=$scenario;Width=584;Height=600;Dpi=144;Theme='contrast'}
}
foreach ($scenario in @('setup','confirmed-idle','off','core-error','long-name')) {
    $cases += [pscustomobject]@{Scenario=$scenario;Width=584;Height=600;Dpi=96;Theme='normal';TextPercent=225}
}
foreach ($case in $cases) {
    $textPercent = if ($case.PSObject.Properties['TextPercent']) { $case.TextPercent } else { 100 }
    $name = '{0}-{1}x{2}-{3}dpi-{4}-{5}text.png' -f $case.Scenario,$case.Width,$case.Height,$case.Dpi,$case.Theme,$textPercent
    $output = Join-Path $OutputDirectory $name
    $p = Start-Process -FilePath $exe -ArgumentList @('--render-preview',('"{0}"' -f $output),$case.Scenario,$case.Width,$case.Height,$case.Dpi,$case.Theme,$textPercent) -WindowStyle Hidden -PassThru -Wait
    if ($p.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw ('Scenario failed: ' + $name) }
}
$cases | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'cases.json') -Encoding UTF8
Write-Output ('PASS {0} isolated scenario renders. DPI is applied to WPF layout; physical monitor transitions remain a separate check.' -f $cases.Count)
Write-Output ('Output: ' + $OutputDirectory)
