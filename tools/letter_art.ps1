# Renders the post's letter layers of tools\letter_art.html (cut from Desktop\export\letter-2c) into
# assets\book\letter_<part>.png with headless Chrome: envelope, seal, seal_l, seal_r, paper, bits0..bits4 — the post's own;
# and each sender's seal (SEALS in the html: a story person's npc id) as letter_seal_<who>.png, letter_seal_l_<who>.png,
# letter_seal_r_<who>.png, letter_bits<v>_<who>.png (RavenBookLetter picks them by the letter's sender; none: the post's own).
# -Only bits0,bits1,…: those parts alone (the rest left as they are).
param([string[]]$Only)
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$prof = Join-Path $env:TEMP 'rw_chrome_profile'
New-Item -ItemType Directory -Force "$root\assets\book" | Out-Null
$html = Get-Content "$root\tools\letter_art.html" -Raw -Encoding UTF8
$nBits = [int]([regex]::Match($html, 'const BITS = (\d+)').Groups[1].Value)   # (the kinds of bits, read from the html)
if ($nBits -le 0) { throw 'letter_art.ps1: no BITS read from letter_art.html' }
$parts = @{ envelope = '600,840'; seal = '220,240'; seal_l = '220,240'; seal_r = '220,240'; paper = '840,1120' }
$bitParts = @(0..($nBits - 1) | ForEach-Object { "bits$_" })
foreach ($b in $bitParts) { $parts[$b] = '180,120' }
$senders = @([regex]::Matches($html, '(?m)^\s+(\w+):\s+\{ wax:') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -ne 'post' })   # (the senders of SEALS in the html, read from it: a seal added there is drawn here)
if ($senders.Count -eq 0) { throw 'letter_art.ps1: no SEALS read from letter_art.html' }
"senders: $($senders -join ', ')"
$own = @('seal', 'seal_l', 'seal_r') + $bitParts     # (the parts a sender has his own of)
$jobs = @()
foreach ($k in $parts.Keys) { $jobs += ,@($k, '', "letter_$k.png") }
foreach ($w in $senders) { foreach ($k in $own) { $jobs += ,@($k, $w, "letter_${k}_$w.png") } }
if ($Only) { $jobs = @($jobs | Where-Object { $Only -contains $_[0] }) }
foreach ($j in $jobs) {
    $k = $j[0]; $w = $j[1]; $out = "$root\assets\book\$($j[2])"
    if (Test-Path $out) { Remove-Item $out }
    $url = 'file:///' + ($root -replace '\\', '/') + "/tools/letter_art.html?part=$k" + $(if ($w) { "&who=$w" } else { '' })
    Start-Process $chrome -ArgumentList @('--headless=new', '--disable-gpu', '--hide-scrollbars', "--user-data-dir=$prof", '--force-device-scale-factor=1', '--default-background-color=00000000', '--virtual-time-budget=3000', "--window-size=$($parts[$k])", "--screenshot=$out", $url) -Wait | Out-Null
    "$($j[2]) -> " + (Get-Item $out -ErrorAction SilentlyContinue).Length
}
