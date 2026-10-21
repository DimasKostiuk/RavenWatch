# The post's two pictures: assets\icons\mail.png (a white envelope for the book's gold corner button, tinted there)
# and assets\icons\letter.png (the «Пергамент» item: a folded sheet with a red wax seal). Run once; a picture of your
# own in config\RavenWatch\icons\ with the same name wins in the game.
Add-Type -AssemblyName System.Drawing
$root = 'C:\Users\kosti\Desktop\RavensWatch'

# ---- mail.png: 64x64, white lines on clear
$bmp = New-Object System.Drawing.Bitmap(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 5)
$pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
$g.DrawRectangle($pen, 6, 14, 52, 36)
$g.DrawLines($pen, [System.Drawing.Point[]]@((New-Object System.Drawing.Point(8, 17)), (New-Object System.Drawing.Point(32, 36)), (New-Object System.Drawing.Point(56, 17))))
$g.Dispose()
$bmp.Save("$root\assets\icons\mail.png", [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()

# ---- letter.png: 128x128, a parchment folded in three, a little turned, with a red seal
$bmp = New-Object System.Drawing.Bitmap(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.Clear([System.Drawing.Color]::Transparent)
$g.TranslateTransform(64, 64); $g.RotateTransform(-12); $g.TranslateTransform(-64, -64)
# soft shadow
$sh = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, 0, 0, 0))
$g.FillRectangle($sh, 22, 34, 90, 66)
# the sheet: warm paper, darker towards the edges
$rect = New-Object System.Drawing.Rectangle(16, 28, 92, 68)
$paper = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 238, 222, 182), [System.Drawing.Color]::FromArgb(255, 205, 180, 128), 70)
$g.FillRectangle($paper, $rect)
$edge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 120, 88, 44), 2)
$g.DrawRectangle($edge, $rect)
# the folds
$fold = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 140, 104, 56), 2)
$g.DrawLine($fold, 16, 51, 108, 51); $g.DrawLine($fold, 16, 74, 108, 74)
# a few lines of writing
$ink = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, 70, 48, 26), 2)
foreach ($y in 36, 42, 58, 64, 82, 88) { $g.DrawLine($ink, 26, $y, (Get-Random -Minimum 70 -Maximum 98), $y) }
# the red wax seal with a raven rune (ᚺ drawn as two bars and a cross bar)
$seal = New-Object System.Drawing.Drawing2D.GraphicsPath; $seal.AddEllipse(70, 58, 34, 34)
$wax = New-Object System.Drawing.Drawing2D.PathGradientBrush($seal)
$wax.CenterColor = [System.Drawing.Color]::FromArgb(255, 200, 40, 40); $wax.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 120, 14, 18))
$g.FillPath($wax, $seal)
$rim = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 96, 10, 14), 2); $g.DrawPath($rim, $seal)
$rune = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(230, 250, 200, 170), 3)
$g.DrawLine($rune, 81, 66, 81, 84); $g.DrawLine($rune, 93, 66, 93, 84); $g.DrawLine($rune, 81, 72, 93, 78)
$g.Dispose()
$bmp.Save("$root\assets\icons\letter.png", [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
"saved"
