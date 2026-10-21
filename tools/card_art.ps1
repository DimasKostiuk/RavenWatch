# Renders the book's popup card pieces of tools\card_art.html (cut from Downloads\mail-1a.png) at 2x into
# assets\book\card_<part>.png.
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$prof = Join-Path $env:TEMP 'rw_chrome_profile'
$parts = @{ frame = '128,128'; paper = '256,256'; plaque = '160,56'; close = '64,64'; gold = '160,52'; seal = '56,56'; ribbon = '20,56' }
foreach ($k in $parts.Keys) {
    $out = "$root\assets\book\card_$k.png"
    if (Test-Path $out) { Remove-Item $out }
    $url = 'file:///' + ($root -replace '\\', '/') + "/tools/card_art.html?part=$k"
    Start-Process $chrome -ArgumentList @('--headless=new', '--disable-gpu', '--hide-scrollbars', "--user-data-dir=$prof", '--force-device-scale-factor=2', '--default-background-color=00000000', '--virtual-time-budget=2000', "--window-size=$($parts[$k])", "--screenshot=$out", $url) -Wait | Out-Null
    "card_$k.png -> " + (Get-Item $out -ErrorAction SilentlyContinue).Length
}
