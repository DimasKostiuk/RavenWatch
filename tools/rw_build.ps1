param([string]$Version, [switch]$RavenItem, [switch]$DevKit, [switch]$NoVoice, [switch]$WithVoice, [switch]$NoTalkCheck)
$tag = if ($DevKit) { "-devkit" } else { "" }; if ($DevKit) { $RavenItem = [switch]$true }
$ErrorActionPreference = 'Stop'
$M='E:\Steam\steamapps\common\Valheim\valheim_Data\Managed'; $C='D:\Thunderstore\Valheim\profiles\Valya\BepInEx\core'; $root='C:\Users\kosti\Desktop\RavensWatch'
$list=@("$C\BepInEx.dll","$C\0Harmony.dll","$M\assembly_valheim.dll","$M\assembly_utils.dll","$M\assembly_guiutils.dll","$M\Splatform.dll","$M\UnityEngine.dll","$M\UnityEngine.CoreModule.dll","$M\UnityEngine.IMGUIModule.dll","$M\UnityEngine.InputLegacyModule.dll","$M\UnityEngine.AnimationModule.dll","$M\UnityEngine.PhysicsModule.dll","$M\UnityEngine.ParticleSystemModule.dll","$M\UnityEngine.AudioModule.dll","$M\UnityEngine.UI.dll","$M\UnityEngine.UIModule.dll","$M\UnityEngine.TextRenderingModule.dll","$M\UnityEngine.ImageConversionModule.dll","$M\Unity.TextMeshPro.dll","$M\UnityEngine.TextCoreFontEngineModule.dll","$M\SoftReferenceableAssets.dll","$M\netstandard.dll")
$out="$root\build\RavenWatch-$Version$tag.dll"
$a=@('/nologo','/target:library','/optimize+','/warn:4','/utf8output','/codepage:65001',"/out:$out") + ($list | % { '/r:' + $_ })
if ($DevKit) { $a += '/define:RAVEN_DEVKIT' }
if ($RavenItem -and [version]$Version -ge [version]"2.4.0") { $a += '/define:RAVEN_STORY' } # the story (story-plan.md): from 2.4 only
if ($RavenItem) { $a += '/define:RAVEN_ITEM'; $a += "/resource:$root\assets\raven-fallback.png,RavenWatch.icon.png"; $a += "/resource:$root\assets\runes.png,RavenWatch.runes.png"; $a += "/resource:$root\assets\feathers.png,RavenWatch.feathers.png"; $a += "/resource:$root\assets\smoke.png,RavenWatch.smoke.png"; Get-ChildItem "$root\assets\icons\*.png" | % { $a += "/resource:$($_.FullName),RavenWatch.icons.$($_.Name)" }; if (Test-Path "$root\assets\perches") { Get-ChildItem "$root\assets\perches\*.txt" | % { $a += "/resource:$($_.FullName),RavenWatch.perches.$($_.Name)" } }; if (Test-Path "$root\assets\gear") { Get-ChildItem "$root\assets\gear\*.txt" | % { $a += "/resource:$($_.FullName),RavenWatch.gear.$($_.Name)" } }; if (Test-Path "$root\assets\items") { Get-ChildItem "$root\assets\items\*.txt" | % { $a += "/resource:$($_.FullName),RavenWatch.items.$($_.Name)" } }; if (Test-Path "$root\assets\fbx") { Get-ChildItem "$root\assets\fbx\*.fbx" | % { $a += "/resource:$($_.FullName),RavenWatch.fbx.$($_.Name)" }; Get-ChildItem "$root\assets\fbx\*_glow.png" | % { $a += "/resource:$($_.FullName),RavenWatch.fbx.$($_.Name)" } }; if (Test-Path "$root\assets\menu") { Get-ChildItem "$root\assets\menu\*.png" | % { $a += "/resource:$($_.FullName),RavenWatch.menu.$($_.Name)" } }; if (Test-Path "$root\assets\photos") { Get-ChildItem "$root\assets\photos\*.png" | % { $a += "/resource:$($_.FullName),RavenWatch.photos.$($_.Name)" } }; if (Test-Path "$root\assets\book") { Get-ChildItem "$root\assets\book\*.png" | % { $a += "/resource:$($_.FullName),RavenWatch.book.$($_.Name)" } }; if (Test-Path "$root\assets\fonts") { Get-ChildItem "$root\assets\fonts\*.ttf" | % { $a += "/resource:$($_.FullName),RavenWatch.fonts.$($_.Name)" } } }
# the languages: assets/lang/<Language>/*.txt as RavenWatch.lang.<Language>.<file>
if (Test-Path "$root\assets\lang") { Get-ChildItem "$root\assets\lang" -Directory | % { $ln = $_.Name; Get-ChildItem "$($_.FullName)\*.txt" | % { $a += "/resource:$($_.FullName),RavenWatch.lang.$ln.$($_.Name)" } } }
# the ravens' talks must hold together (every answer on the thread of what was said): tools/talkcheck.js
if ($RavenItem -and -not $NoTalkCheck) { & node "$root\tools\talkcheck.js" | Select-Object -Last 15; if ($LASTEXITCODE -ne 0) { throw "TALK CHECK FAILED (node tools/talkcheck.js; -NoTalkCheck to skip)" } }
$a += "$root\src\*.cs"
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe @a 2>&1 | Select-Object -First 30
if ($LASTEXITCODE -ne 0) { throw "BUILD FAILED" }

# staging copy of the package with this version's texts
$stage="$env:TEMP\rw_stage_$Version$tag"; if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force "$stage\plugins" | Out-Null
Copy-Item "$root\package\icon.png" "$stage\icon.png"
Copy-Item $out "$stage\plugins\RavenWatch.dll"
$utf8 = New-Object System.Text.UTF8Encoding($false)
$manifest = [IO.File]::ReadAllText("$root\package\manifest.json", $utf8) -replace '"version_number": "[^"]+"', ('"version_number": "' + $Version + '"')
if (-not $RavenItem) { $manifest = $manifest -replace '"description": "[^"]*"', '"description": "Hugin and Munin visit players now and then. Hugin teases you about what you just did, Munin swears at you (NSFW). Admin panel to send ravens. Ukrainian."' }
[IO.File]::WriteAllText("$stage\manifest.json", $manifest, $utf8)
$changelog = [IO.File]::ReadAllText("$root\package\CHANGELOG.md", $utf8)
$readme = [IO.File]::ReadAllText("$root\package\README.md", $utf8)
# the build for model makers: its own readme (how to make, mark and send models) and words in the manifest
if ($DevKit) {
    $readme = [IO.File]::ReadAllText("$root\package\DEVKIT.md", $utf8)
    $manifest = $manifest -replace '"description": "[^"]*"', '"description": "RavenWatch for model makers: Workshop on F7, Ready mark, pack to desktop. Not for servers."'
    [IO.File]::WriteAllText("$stage\manifest.json", $manifest, $utf8)
}
if (-not $RavenItem) {
    $changelog = [regex]::Replace($changelog, '(?s)## 1\.[2-9]\.\d+\r?\n.*?(?=## 1\.1\.)', '')
    $readme = [regex]::Replace($readme, '(?s)## Ворон на плечі.*?(?=## Адмін-панель)', '')
    # console rows that only exist with the raven items
    $readme = [regex]::Replace($readme, '(?m)^\| `raven (give|power|reroll|perk|perks|ready|info|shoulder|tilt|trust|gift|guide|wings|icons|perf)\b.*\r?\n', '')
    $readme = [regex]::Replace($readme, '(?s)\*\*New in 1\.2:\*\*.*?\r?\n\r?\n', '')
}
if ($RavenItem) {
    # 1.2 has no planned visits to talk to: only the quick ravens (and Hugin the guide, in the raven section)
    $readme = $readme.Replace('сідають поруч, кажуть щось над головою, а після розмови (E) видають своє і відлітають — так само, як ванільні підказки Хугіна.', 'сідають поруч, кажуть щось над головою й відлітають.')
    $readme = [regex]::Replace($readme, '(?m)^- \*\*За розкладом:\*\*.*\r?\n', '')
    $readme = [regex]::Replace($readme, '(?m)^Над вороном, який прилетів до тебе.*\r?\n\r?\n', '')
    $readme = $readme.Replace('плановий візит — щонайменше через 15 хвилин після попереднього ворона; швидкий ворон', 'швидкий ворон')
    # sections of later versions (work in progress) stay out of this package
    $own = [regex]::Match($changelog, '(?m)^## ' + [regex]::Escape($Version) + '\s*$')
    $first = [regex]::Match($changelog, '(?m)^## ')
    if ($own.Success -and $first.Success -and $own.Index -gt $first.Index) { $changelog = $changelog.Substring(0, $first.Index) + $changelog.Substring($own.Index) }
    # Thunderstore takes a changelog of at most 100000 characters: the store gets 2.x only, the full one stays in package/
    $cut = [regex]::Match($changelog, '(?m)^## 1\.')
    if ($cut.Success) { $changelog = $changelog.Substring(0, $cut.Index) + "## 1.x`r`n`r`nEarlier versions (1.0–1.6) are left out here: the store limits the changelog's length.`r`n`r`nРанні версії (1.0–1.6) тут не наведено: магазин обмежує довжину списку змін.`r`n" }
}
if ($changelog.Length -gt 100000) { throw "CHANGELOG too long for Thunderstore: $($changelog.Length) characters (max 100000)" }
[IO.File]::WriteAllText("$stage\CHANGELOG.md", $changelog, $utf8)
[IO.File]::WriteAllText("$stage\README.md", $readme, $utf8)

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip="$root\RavenWatch-$Version$tag.zip"; if (Test-Path $zip) { [IO.File]::Delete($zip) }
$z=[IO.Compression.ZipFile]::Open($zip,'Create'); foreach($f in 'manifest.json','README.md','CHANGELOG.md','icon.png','plugins/RavenWatch.dll'){ [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,(Join-Path $stage $f),$f,'Optimal') }
# the Book of ravens' fonts are built into the dll: their licences go with it
if ($RavenItem -and (Test-Path "$root\assets\fonts")) { Get-ChildItem "$root\assets\fonts\OFL-*.txt" | % { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,$_.FullName,"plugins/fonts/$($_.Name)",'Optimal') } }
# the ravens' voice (voicehost: RavenVoice.exe + ONNX Runtime, data: HolosTTS model with our knobs, vocab, stress dictionary, voices)
if ($WithVoice -and -not $NoVoice -and (Test-Path "$root\voicehost\out\RavenVoice.exe")) {
    foreach ($f in 'RavenVoice.exe','RavenVoice.exe.config','Microsoft.ML.OnnxRuntime.dll','System.Buffers.dll','System.Memory.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','onnxruntime.dll','onnxruntime_providers_shared.dll') { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,"$root\voicehost\out\$f","plugins/voice/$f",'Optimal') }
    foreach ($f in 'model.onnx','vocab.txt','stress_dict.tsv','voices.bin','NOTICE.txt','language.txt') { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,"$root\voicehost\data\$f","plugins/voice/$f",'Optimal') }
}
$z.Dispose()
if ($RavenItem -and -not $DevKit) { Copy-Item $out "$root\package\plugins\RavenWatch.dll" -Force }

Add-Type -Path "$C\Mono.Cecil.dll"
$ad=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($out)
$dllVer = (($ad.MainModule.Types | ? Name -eq 'Plugin').Fields | ? Name -eq 'Version' | % Constant)
"== $Version  dll version: " + $dllVer + "  raven item: " + [bool]($ad.MainModule.Types | ? Name -eq 'RavenItem')
$z=[IO.Compression.ZipFile]::OpenRead($zip); $z.Entries | % { "   $($_.FullName) $($_.Length)" }; $exes = @($z.Entries | ? { $_.FullName -match '\.exe$' }); $z.Dispose()
# the store rejects packages with executables: the voice goes in its own package (tools/voice_pack.ps1)
if ($exes.Count -gt 0 -and -not $WithVoice) { throw "the package has an executable: $($exes[0].FullName)" }
if ($exes.Count -gt 0) { "   !! TEST ONLY: this zip carries the voice program (.exe) — never upload it, the store rejects it" }
# Plugin.Version in src must be the version being built (another session may have moved it on)
if ($RavenItem -and $dllVer -ne $Version) { [IO.File]::Delete($zip); throw "Plugin.Version in src is ${dllVer}, not ${Version}: zip deleted" }
"   changelog head: " + (($changelog -split "`n" | ? { $_ -match '^## ' } | select -First 2) -join ' / ')
"   readme has raven section: " + ($readme -match 'Ворон на плечі')

