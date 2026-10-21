# The orders menu's «Сісти тут» / context icon: a gold sight (ring, four ticks, a dot) in the style of the other
# act-*.png (light gold fill, dark brown outline); and its greyed -dis copy. Writes assets\menu\act-here(.dis).png.
Add-Type -AssemblyName System.Drawing
$out = 'C:\Users\kosti\Desktop\RavensWatch\assets\menu'

function Draw([string]$file, $fillTop, $fillBottom, $ink, [int]$alpha) {
    $bmp = New-Object System.Drawing.Bitmap(96, 96, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $c = 48.0
    $dark = New-Object System.Drawing.Pen($ink, 11); $dark.StartCap = 'Round'; $dark.EndCap = 'Round'
    $rect = New-Object System.Drawing.RectangleF(0, 10, 96, 76)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $fillTop, $fillBottom, 90.0)
    $gold = New-Object System.Drawing.Pen($brush, 6); $gold.StartCap = 'Round'; $gold.EndCap = 'Round'
    $r = 22.0
    $ticks = @(@(0, -1), @(0, 1), @(-1, 0), @(1, 0))
    foreach ($pen in @($dark, $gold)) {
        $g.DrawEllipse($pen, [float]($c - $r), [float]($c - $r), [float](2 * $r), [float](2 * $r))
        foreach ($t in $ticks) { $g.DrawLine($pen, [float]($c + $t[0] * 30), [float]($c + $t[1] * 30), [float]($c + $t[0] * 40), [float]($c + $t[1] * 40)) }
    }
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($ink)), [float]($c - 8.5), [float]($c - 8.5), 17.0, 17.0)
    $g.FillEllipse($brush, [float]($c - 5.5), [float]($c - 5.5), 11.0, 11.0)
    $g.Dispose()
    if ($alpha -lt 255) { # the greyed copy is see-through, as the other -dis icons
        for ($y = 0; $y -lt 96; $y++) { for ($x = 0; $x -lt 96; $x++) { $p = $bmp.GetPixel($x, $y); if ($p.A -gt 0) { $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([int]($p.A * $alpha / 255), $p.R, $p.G, $p.B)) } } }
    }
    $bmp.Save("$out\$file", [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

Draw 'act-here.png' ([System.Drawing.Color]::FromArgb(255, 244, 226, 164)) ([System.Drawing.Color]::FromArgb(255, 226, 190, 112)) ([System.Drawing.Color]::FromArgb(240, 31, 24, 14)) 255
Draw 'act-here-dis.png' ([System.Drawing.Color]::FromArgb(255, 125, 125, 125)) ([System.Drawing.Color]::FromArgb(255, 108, 108, 108)) ([System.Drawing.Color]::FromArgb(240, 13, 13, 13)) 150
"done"
