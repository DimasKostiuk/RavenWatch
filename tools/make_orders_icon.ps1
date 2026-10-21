# The «Накази» icon for G → Ворони: the book's medallion (rim, runes, the two orbs) with its field painted anew
# (blue Hugin / red Munin) and the orders menu's gold circling arrow in the middle. Writes assets\icons\orders.png.
Add-Type -AssemblyName System.Drawing
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$src = [System.Drawing.Bitmap]::FromFile("$root\assets\icons\book.png")
$bmp = New-Object System.Drawing.Bitmap($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.DrawImage($src, 0, 0, $src.Width, $src.Height); $g.Dispose(); $src.Dispose()

$c = 128.0; $field = 86.0
function Lerp([double]$a, [double]$b, [double]$t) { return $a + ($b - $a) * $t }
for ($y = 0; $y -lt 256; $y++) {
  for ($x = 0; $x -lt 256; $x++) {
    $dx = $x + 0.5 - $c; $dy = $y + 0.5 - $c; $r = [Math]::Sqrt($dx * $dx + $dy * $dy)
    if ($r -gt $field + 1) { continue }
    $near = 1.0 - [Math]::Min(1.0, $r / $field)            # 1 at the centre, 0 at the edge
    $side = [Math]::Max(-1.0, [Math]::Min(1.0, $dx / 14.0)) # -1 blue .. 1 red, a soft seam
    $tb = (1.0 - $side) / 2.0
    $cR = (Lerp 34 62 $near) * $tb + (Lerp 100 150 $near) * (1 - $tb)
    $cG = (Lerp 70 104 $near) * $tb + (Lerp 22 44 $near) * (1 - $tb)
    $cB = (Lerp 104 146 $near) * $tb + (Lerp 22 40 $near) * (1 - $tb)
    $seam = 1.0 - [Math]::Abs($side); $cR -= 18 * $seam; $cG -= 14 * $seam; $cB -= 6 * $seam # darker where they meet
    $a = [Math]::Max(0.0, [Math]::Min(1.0, $field + 0.5 - $r))  # the edge blended into the old ring
    $o = $bmp.GetPixel($x, $y)
    $nr = [int](Lerp $o.R $cR $a); $ng = [int](Lerp $o.G $cG $a); $nb = [int](Lerp $o.B $cB $a)
    $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, [Math]::Max(0, $nr), [Math]::Max(0, $ng), [Math]::Max(0, $nb)))
  }
}

$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
# a soft warm light behind the arrow
$path = New-Object System.Drawing.Drawing2D.GraphicsPath; $path.AddEllipse(68, 68, 120, 120)
$glow = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
$glow.CenterColor = [System.Drawing.Color]::FromArgb(90, 255, 214, 140); $glow.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 255, 214, 140))
$g.FillEllipse($glow, 68, 68, 120, 120)
$arrow = [System.Drawing.Bitmap]::FromFile("$root\assets\menu\act-circle.png")
$size = 128; $g.DrawImage($arrow, [int]($c - $size / 2), [int]($c - $size / 2), $size, $size)
$arrow.Dispose(); $g.Dispose()
$bmp.Save("$root\assets\icons\orders.png", [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
"saved"
