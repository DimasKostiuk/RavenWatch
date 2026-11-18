# Renders the pieces of the talk scroll's top (tools\talk_top_art.html: the roll on its hooks, the ravens' heads, what hangs from their rings) at 2x into
# assets\book\talk_<part>.png, and the whole top put together into docs\plans\mockups\talk-top-a.png (for the eye only).
# Each piece's size is read from the page itself (its "name: { w: N, h: M" and "shade: true": SHADE more on every side), so the two never differ.
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$prof = Join-Path $env:TEMP 'rw_chrome_profile'
$page = [IO.File]::ReadAllText("$root\tools\talk_top_art.html")
$shade = [regex]::Match($page, 'const SHADE = (\d+);')
if (-not $shade.Success) { throw 'talk_top_art.html: no "const SHADE = N;"' }
$pad = [int]$shade.Groups[1].Value
$parts = [ordered]@{}
foreach ($m in [regex]::Matches($page, '(?m)^\s*(\w+): \{ w: (\d+), h: (\d+),( shade: true,)?')) {
    $s = if ($m.Groups[4].Success) { 2 * $pad } else { 0 }
    $parts[$m.Groups[1].Value] = ([int]$m.Groups[2].Value + $s).ToString() + ',' + ([int]$m.Groups[3].Value + $s).ToString()
}
if ($parts.Count -eq 0) { throw 'talk_top_art.html: no pieces found' }
function Shot($part, $size, $out) {
    if (Test-Path $out) { Remove-Item $out }
    $url = 'file:///' + ($root -replace '\\', '/') + "/tools/talk_top_art.html?part=$part"
    Start-Process $chrome -ArgumentList @('--headless=new', '--disable-gpu', '--hide-scrollbars', "--user-data-dir=$prof", '--force-device-scale-factor=2', '--default-background-color=00000000', '--virtual-time-budget=2000', "--window-size=$size", "--screenshot=$out", $url) -Wait | Out-Null
    (Split-Path $out -Leaf) + ' ' + $size + ' -> ' + (Get-Item $out -ErrorAction SilentlyContinue).Length
}
foreach ($k in $parts.Keys) { Shot $k $parts[$k] "$root\assets\book\talk_$k.png" }
Shot 'all' '640,170' "$root\docs\plans\mockups\talk-top-a.png"
