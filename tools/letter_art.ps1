# Renders the post's letter layers of tools\letter_art.html (cut from Desktop\export\letter-2c) into
# assets\book\letter_<part>.png with headless Chrome: envelope, seal, seal_l, seal_r, paper.
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$prof = Join-Path $env:TEMP 'rw_chrome_profile'
New-Item -ItemType Directory -Force "$root\assets\book" | Out-Null
$parts = @{ envelope = '600,840'; seal = '220,240'; seal_l = '220,240'; seal_r = '220,240'; paper = '840,1120' }
foreach ($k in $parts.Keys) {
    $out = "$root\assets\book\letter_$k.png"
    if (Test-Path $out) { Remove-Item $out }
    $url = 'file:///' + ($root -replace '\\', '/') + "/tools/letter_art.html?part=$k"
    Start-Process $chrome -ArgumentList @('--headless=new', '--disable-gpu', '--hide-scrollbars', "--user-data-dir=$prof", '--force-device-scale-factor=1', '--default-background-color=00000000', '--virtual-time-budget=3000', "--window-size=$($parts[$k])", "--screenshot=$out", $url) -Wait | Out-Null
    "letter_$k.png -> " + (Get-Item $out -ErrorAction SilentlyContinue).Length
}
