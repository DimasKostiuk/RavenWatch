# RavenWatch_Voice: the ravens' voice as its own Thunderstore package (the main package carries no executable)
# usage: tools/voice_pack.ps1  -> RavenWatch_Voice-<manifest version>.zip in the project root
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$pkg = "$root\voice_package"
$out = "$root\voicehost\out"; $data = "$root\voicehost\data"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$manifest = Get-Content "$pkg\manifest.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$ver = $manifest.version_number
if ($manifest.description.Length -gt 250) { throw "manifest description too long: $($manifest.description.Length)" }
$zip = "$root\RavenWatch_Voice-$ver.zip"
if (Test-Path $zip) { Remove-Item $zip }
$z = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($f in 'manifest.json','README.md','CHANGELOG.md','icon.png') { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, "$pkg\$f", $f, 'Optimal') }
    foreach ($f in 'RavenVoice.exe','RavenVoice.exe.config','Microsoft.ML.OnnxRuntime.dll','System.Buffers.dll','System.Memory.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','onnxruntime.dll','onnxruntime_providers_shared.dll') { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, "$out\$f", "plugins/voice/$f", 'Optimal') }
    foreach ($f in 'model.onnx','vocab.txt','stress_dict.tsv','voices.bin','NOTICE.txt','language.txt') { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, "$data\$f", "plugins/voice/$f", 'Optimal') }
} finally { $z.Dispose() }
"== RavenWatch_Voice $ver -> $zip  (" + [math]::Round((Get-Item $zip).Length / 1MB, 1) + " MB)"
