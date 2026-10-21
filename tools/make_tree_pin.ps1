# The map mark of the Raven Tree: a bare tree with a raven on a branch, white on clear (coloured in the game).
#   powershell -File tools/make_tree_pin.ps1 [out.png]
param([string]$Out = "$PSScriptRoot\..\assets\icons\map_tree.png")
Add-Type -AssemblyName System.Drawing
$n = 128
$bmp = New-Object System.Drawing.Bitmap $n, $n, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$w = [System.Drawing.Brushes]::White
function P([float]$x, [float]$y) { New-Object System.Drawing.PointF $x, $y }
function Line([float]$w0, [float]$x1, [float]$y1, [float]$x2, [float]$y2) {
  $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $w0
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round; $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawLine($pen, $x1, $y1, $x2, $y2); $pen.Dispose()
}
# trunk, widening into roots
$g.FillPolygon($w, [System.Drawing.PointF[]]@((P 56 50), (P 72 50), (P 74 100), (P 88 116), (P 40 116), (P 54 100)))
# branches
Line 9 64 60 34 30
Line 9 64 56 96 26
Line 6 46 42 30 44
Line 6 86 38 108 40
Line 6 40 34 34 14
Line 6 90 30 98 12
Line 5 64 56 64 14
Line 4 34 30 18 26
Line 4 96 26 112 20
# the raven on the right branch: body, head, beak, tail
$g.FillEllipse($w, 80, 2, 32, 20)
$g.FillEllipse($w, 104, 0, 16, 15)
$g.FillPolygon($w, [System.Drawing.PointF[]]@((P 117 4), (P 128 8), (P 117 12)))
$g.FillPolygon($w, [System.Drawing.PointF[]]@((P 84 12), (P 64 24), (P 70 8)))
$g.Dispose()
$bmp.Save((Resolve-Path (Split-Path $Out)).Path + "\" + (Split-Path $Out -Leaf), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"map mark -> $Out"
