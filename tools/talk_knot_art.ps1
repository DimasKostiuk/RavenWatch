# Renders the talk scroll's thread rope pieces of tools\talk_knot_art.html (the user's design 2026-11-08, Downloads\suviy-stosunky.html 5a) at 2x into
# assets\book\talk_<part>.png.
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$prof = Join-Path $env:TEMP 'rw_chrome_profile'
$parts = @{ knot1 = '30,28'; knot2 = '30,28'; frayed = '30,30'; end = '30,30'; cord = '11,16' }
foreach ($k in $parts.Keys) {
    $out = "$root\assets\book\talk_$k.png"
    if (Test-Path $out) { Remove-Item $out }
    $url = 'file:///' + ($root -replace '\\', '/') + "/tools/talk_knot_art.html?part=$k"
    Start-Process $chrome -ArgumentList @('--headless=new', '--disable-gpu', '--hide-scrollbars', "--user-data-dir=$prof", '--force-device-scale-factor=2', '--default-background-color=00000000', '--virtual-time-budget=2000', "--window-size=$($parts[$k])", "--screenshot=$out", $url) -Wait | Out-Null
    "talk_$k.png -> " + (Get-Item $out -ErrorAction SilentlyContinue).Length
}
