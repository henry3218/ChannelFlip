param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('ChannelFlipIconNative' -as [type])) {
    Add-Type 'public static class ChannelFlipIconNative { [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool DestroyIcon(System.IntPtr icon); }'
}
$bitmap = New-Object System.Drawing.Bitmap 64,64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(20,35,32))
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(181,234,212)),5
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($pen,14,23,49,23)
$graphics.DrawLine($pen,41,15,49,23)
$graphics.DrawLine($pen,41,31,49,23)
$graphics.DrawLine($pen,50,42,15,42)
$graphics.DrawLine($pen,23,34,15,42)
$graphics.DrawLine($pen,23,50,15,42)
$iconHandle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconHandle)
$stream = [IO.File]::Create($OutputPath)
try { $icon.Save($stream) }
finally { $stream.Dispose(); $icon.Dispose(); [ChannelFlipIconNative]::DestroyIcon($iconHandle) | Out-Null; $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
