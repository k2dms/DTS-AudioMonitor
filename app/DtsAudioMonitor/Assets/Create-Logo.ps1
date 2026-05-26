Add-Type -AssemblyName System.Drawing
$path = Join-Path $PSScriptRoot 'logo.png'
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(255, 30, 25, 55))
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.Point]::new(0, 0),
    [System.Drawing.Point]::new(256, 256),
    [System.Drawing.Color]::FromArgb(255, 100, 80, 220),
    [System.Drawing.Color]::FromArgb(255, 160, 140, 255))
$g.FillEllipse($brush, 28, 28, 200, 200)
$white = [System.Drawing.Brushes]::White
$g.FillEllipse($white, 70, 95, 116, 116)
$g.FillRectangle($white, 118, 70, 20, 55)
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
$icon = [System.Drawing.Icon]::FromHandle(([System.Drawing.Bitmap]::FromFile($path)).GetHicon())
$fs = [IO.File]::Create((Join-Path $PSScriptRoot 'app.ico'))
$icon.Save($fs); $fs.Close()
Write-Host "OK $path"
