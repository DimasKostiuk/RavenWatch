# Renders the Ravens page's margin medallion pictures of tools\rail_icons.html at 2x into assets\book\rail_<part>.png.
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$prof = Join-Path $env:TEMP 'rw_chrome_profile'
foreach ($k in @('tub', 'kin', 'words')) {
    $out = "$root\assets\book\rail_$k.png"
    if (Test-Path $out) { Remove-Item $out }
    $url = 'file:///' + ($root -replace '\\', '/') + "/tools/rail_icons.html?part=$k"
    Start-Process $chrome -ArgumentList @('--headless=new', '--disable-gpu', '--hide-scrollbars', "--user-data-dir=$prof", '--force-device-scale-factor=2', '--default-background-color=00000000', '--virtual-time-budget=2000', '--window-size=64,64', "--screenshot=$out", $url) -Wait | Out-Null
    "rail_$k.png -> " + (Get-Item $out -ErrorAction SilentlyContinue).Length
}
