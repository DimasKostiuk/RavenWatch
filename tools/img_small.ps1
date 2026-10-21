# A model's picture made small for the mod (DLL size): resized to <size> px square and saved as JPG.
#   powershell -File tools/img_small.ps1 <in.png> <out.jpg> <size> [quality]
param([string]$In, [string]$Out, [int]$Size = 1024, [int]$Quality = 88)
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Image]::FromFile((Resolve-Path $In))
$bmp = New-Object System.Drawing.Bitmap $Size, $Size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.DrawImage($src, 0, 0, $Size, $Size)
$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$ep = New-Object System.Drawing.Imaging.EncoderParameters 1
$ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]$Quality)
$bmp.Save($Out, $codec, $ep)
$g.Dispose(); $bmp.Dispose(); $src.Dispose()
"{0} -> {1} ({2} px, {3} KB)" -f (Split-Path $In -Leaf), (Split-Path $Out -Leaf), $Size, [math]::Round((Get-Item $Out).Length / 1KB)
