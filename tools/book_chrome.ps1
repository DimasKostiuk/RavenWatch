# Renders the empty book of tools\book_chrome.html (cut from the design) into assets\book\chrome.png at 1.5x:
# 1300x780 design pixels, the book (1200x680) at 50,50, two equal pages. Needs Chrome and the internet (the runes'
# web font).
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$prof = Join-Path $env:TEMP 'rw_chrome_profile'
New-Item -ItemType Directory -Force "$root\assets\book" | Out-Null
$out = "$root\assets\book\chrome.png"
if (Test-Path $out) { Remove-Item $out }
$url = 'file:///' + ($root -replace '\\', '/') + '/tools/book_chrome.html'
$p = Start-Process $chrome -ArgumentList @('--headless=new', '--disable-gpu', '--hide-scrollbars', "--user-data-dir=$prof", '--force-device-scale-factor=1.5', '--default-background-color=00000000', '--virtual-time-budget=8000', '--window-size=1300,780', "--screenshot=$out", $url) -Wait -PassThru
"chrome.png -> " + (Get-Item $out -ErrorAction SilentlyContinue).Length
