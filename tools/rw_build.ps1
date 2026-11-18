param([string]$Version, [switch]$RavenItem, [switch]$DevKit, [switch]$NoVoice, [switch]$WithVoice, [switch]$NoTalkCheck, [switch]$NoStory)
$tag = if ($DevKit) { "-devkit" } else { "" }; if ($DevKit) { $RavenItem = [switch]$true }
$ErrorActionPreference = 'Stop'
$M='E:\Steam\steamapps\common\Valheim\valheim_Data\Managed'; $C='D:\Thunderstore\Valheim\profiles\Valya\BepInEx\core'; $root='C:\Users\kosti\Desktop\RavensWatch'
$list=@("$C\BepInEx.dll","$C\0Harmony.dll","$M\assembly_valheim.dll","$M\assembly_utils.dll","$M\assembly_guiutils.dll","$M\Splatform.dll","$M\UnityEngine.dll","$M\UnityEngine.CoreModule.dll","$M\UnityEngine.IMGUIModule.dll","$M\UnityEngine.InputLegacyModule.dll","$M\UnityEngine.AnimationModule.dll","$M\UnityEngine.PhysicsModule.dll","$M\UnityEngine.AIModule.dll","$M\UnityEngine.ClothModule.dll","$M\UnityEngine.ParticleSystemModule.dll","$M\UnityEngine.AudioModule.dll","$M\UnityEngine.UnityWebRequestModule.dll","$M\UnityEngine.UnityWebRequestAudioModule.dll","$M\UnityEngine.UI.dll","$M\UnityEngine.UIModule.dll","$M\UnityEngine.TextRenderingModule.dll","$M\UnityEngine.ImageConversionModule.dll","$M\UnityEngine.ScreenCaptureModule.dll","$M\Unity.TextMeshPro.dll","$M\UnityEngine.TextCoreFontEngineModule.dll","$M\SoftReferenceableAssets.dll","$M\netstandard.dll")
$out="$root\build\RavenWatch-$Version$tag.dll"
# the dll's own (assembly) name is FROZEN as «RavenWatch-2.5.1» in every version: RavenWatch_Voice is built against that
# name, so the voice package keeps working across main updates without a re-upload. Never change it (that breaks
# every voice package out there: «Could not load file or assembly RavenWatch-2.5.1» — 2.6.0 had «RavenWatch-2.6.0»).
$asm="$root\build\name\RavenWatch-2.5.1.dll"; [void][IO.Directory]::CreateDirectory("$root\build\name")
$a=@('/nologo','/target:library','/optimize+','/warn:4','/utf8output','/codepage:65001',"/out:$asm") + ($list | % { '/r:' + $_ })
if ($DevKit) { $a += '/define:RAVEN_DEVKIT' }
if ($RavenItem -and [version]$Version -ge [version]"2.9.0") { $a += '/define:RAVEN_NPC' } # the people (npc-plan.md): from 2.9 (the user 2026-10-30: NPCs in 2.9, the story in 3.0) — with the story's groundwork they stand on (its bus, conditions, a character's memory)
if ($RavenItem -and -not $NoStory -and [version]$Version -ge [version]"2.9.0") { $a += '/define:RAVEN_STORY' } # the story (story-plan.md): from 2.9 = the prologue + all the story tools (the user 2026-11-01; the story itself, or a part of it, in 3.0); -NoStory: a store build while the story is unfinished (2.6.0 went out without it)
if ($RavenItem) { $a += '/define:RAVEN_ITEM'; $a += "/resource:$root\assets\raven-fallback.png,RavenWatch.icon.png"; $a += "/resource:$root\assets\runes.png,RavenWatch.runes.png"; $a += "/resource:$root\assets\feathers.png,RavenWatch.feathers.png"; $a += "/resource:$root\assets\gaits.txt,RavenWatch.gaits.txt"; $a += "/resource:$root\assets\smoke.png,RavenWatch.smoke.png"; Get-ChildItem "$root\assets\icons\*.png" | % { $a += "/resource:$($_.FullName),RavenWatch.icons.$($_.Name)" }; if (Test-Path "$root\assets\perches") { Get-ChildItem "$root\assets\perches\*.txt" | % { $a += "/resource:$($_.FullName),RavenWatch.perches.$($_.Name)" } }; if (Test-Path "$root\assets\gear") { Get-ChildItem "$root\assets\gear\*.txt" | % { $a += "/resource:$($_.FullName),RavenWatch.gear.$($_.Name)" } }; if (Test-Path "$root\assets\items") { Get-ChildItem "$root\assets\items\*.txt" | % { $a += "/resource:$($_.FullName),RavenWatch.items.$($_.Name)" } }; if (Test-Path "$root\assets\fbx") { Get-ChildItem "$root\assets\fbx\*.fbx" | % { $a += "/resource:$($_.FullName),RavenWatch.fbx.$($_.Name)" }; Get-ChildItem "$root\assets\fbx\*_glow.png" | % { $a += "/resource:$($_.FullName),RavenWatch.fbx.$($_.Name)" } }; if (Test-Path "$root\assets\menu") { Get-ChildItem "$root\assets\menu\*.png" | % { $a += "/resource:$($_.FullName),RavenWatch.menu.$($_.Name)" } }; if (Test-Path "$root\assets\photos") { Get-ChildItem "$root\assets\photos\*.png" | % { $a += "/resource:$($_.FullName),RavenWatch.photos.$($_.Name)" } }; if (Test-Path "$root\assets\book") { Get-ChildItem "$root\assets\book\*.png" | % { $a += "/resource:$($_.FullName),RavenWatch.book.$($_.Name)" } }; if (Test-Path "$root\assets\fonts") { Get-ChildItem "$root\assets\fonts\*.ttf" | % { $a += "/resource:$($_.FullName),RavenWatch.fonts.$($_.Name)" } } }
# the story's own models (assets/story: the Hall of the Watch, the Watch's feather — the user 2026-10-31): from 2.9 (the user 2026-10-31: «ці моделі будуть у версії 2.9»)
if ($RavenItem -and [version]$Version -ge [version]"2.9.0" -and (Test-Path "$root\assets\story")) { Get-ChildItem "$root\assets\story\perches\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.perches.$($_.Name)" }; Get-ChildItem "$root\assets\story\clips\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.clips.$($_.Name)" }; Get-ChildItem "$root\assets\story\items\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.items.$($_.Name)" }; Get-ChildItem "$root\assets\story\fbx\*.*" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.fbx.$($_.Name)" }; Get-ChildItem "$root\assets\story\memories\*.*" -ErrorAction SilentlyContinue | ? { $_.Extension -in ".png",".jpg" } | % { $a += "/resource:$($_.FullName),RavenWatch.memories.$($_.Name)" }; Get-ChildItem "$root\assets\story\blueprints\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.blueprints.$($_.Name)" }; Get-ChildItem "$root\assets\story\quests\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.quests.$($_.Name)" }; Get-ChildItem "$root\assets\story\scenes\*.txt" -ErrorAction SilentlyContinue | ? { -not $_.Name.StartsWith("_") } | % { $a += "/resource:$($_.FullName),RavenWatch.scenes.$($_.Name)" }; Get-ChildItem "$root\assets\story\npc\*.txt" -ErrorAction SilentlyContinue | ? { -not $_.Name.StartsWith("_") } | % { $a += "/resource:$($_.FullName),RavenWatch.npc.$($_.Name)" }; Get-ChildItem "$root\assets\story\people\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.people.$($_.Name)" }; Get-ChildItem "$root\assets\story\sounds\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.sounds.$($_.Name)" } }
# the story people's talks (NpcTalk): only with the story, their conditions (npcat, step) are its own
if ($RavenItem -and -not $NoStory -and [version]$Version -ge [version]"2.9.0") { Get-ChildItem "$root\assets\story\talk\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.talk.$($_.Name)" }; Get-ChildItem "$root\assets\story\tales\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.tales.$($_.Name)" }; Get-ChildItem "$root\assets\story\saga\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.saga.$($_.Name)" }; Get-ChildItem "$root\assets\story\looks\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.looks.$($_.Name)" }; Get-ChildItem "$root\assets\story\words\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.words.$($_.Name)" }; Get-ChildItem "$root\assets\story\meets\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.meets.$($_.Name)" }; Get-ChildItem "$root\assets\story\rooms\*.txt" -ErrorAction SilentlyContinue | % { $a += "/resource:$($_.FullName),RavenWatch.rooms.$($_.Name)" } }
# the languages: assets/lang/<Language>/*.txt as RavenWatch.lang.<Language>.<file>
if (Test-Path "$root\assets\lang") { Get-ChildItem "$root\assets\lang" -Directory | % { $ln = $_.Name; Get-ChildItem "$($_.FullName)\*.txt" | % { $a += "/resource:$($_.FullName),RavenWatch.lang.$ln.$($_.Name)" } } }
# the ravens' talks must hold together (every answer on the thread of what was said): tools/talkcheck.js
if ($RavenItem -and -not $NoTalkCheck) { & node "$root\tools\talkcheck.js" | Select-Object -Last 15; if ($LASTEXITCODE -ne 0) { throw "TALK CHECK FAILED (node tools/talkcheck.js; -NoTalkCheck to skip)" } }
# the phrase books hold together (every list named exists and is used, every tag speaks at every trust tier, English mirrors Ukrainian): tools/bookcheck.js
if ($RavenItem -and -not $NoTalkCheck) { & node "$root\tools\bookcheck.js" | Select-Object -Last 1; if ($LASTEXITCODE -ne 0) { throw "BOOK CHECK FAILED (node tools/bookcheck.js; -NoTalkCheck to skip)" } }
# the core's rules (docs/ARCHITECTURE.md): a patch beside the core stops the build
& node "$root\tools\arch_check.js"; if ($LASTEXITCODE -ne 0) { throw "ARCH CHECK FAILED: docs/ARCHITECTURE.md" }
# people's words hold together and stay varied (a new theme, a new retelling: enough variants, its forms right): tools/npcwordcheck.js
if ($RavenItem -and -not $NoTalkCheck) { & node "$root\tools\npcwordcheck.js" | Select-Object -Last 15; if ($LASTEXITCODE -ne 0) { throw "NPC WORD CHECK FAILED (node tools/npcwordcheck.js)" } }
# a person stays light on the net (a new value of a person: on it only if every game must see it at once, else its locker): tools/npcnetcheck.js
if ($RavenItem) { & node "$root\tools\npcnetcheck.js" | Select-Object -Last 15; if ($LASTEXITCODE -ne 0) { throw "NPC NET CHECK FAILED (node tools/npcnetcheck.js)" } }
# the story's data holds together (stages, scenes, places, people): tools/storycheck.js
& node "$root\tools\storycheck.js" --ledger; if ($LASTEXITCODE -ne 0) { throw "STORY CHECK FAILED: assets/story" }
# the clips hold together (names, limits, weights, parts; every clip a scene names is in the mod): tools/clipcheck.js
& node "$root\tools\clipcheck.js"; if ($LASTEXITCODE -ne 0) { throw "CLIP CHECK FAILED: assets/story/clips" }
# every text in English as soon as it is written (the user 2026-11-11): the menus' table has no blank, the people's themes and lists are all there and as long: tools/l10n.js
if ($RavenItem) { & node "$root\tools\l10n.js" check English --strict | Select-Object -Last 20; if ($LASTEXITCODE -ne 0) { throw "ENGLISH CHECK FAILED (node tools/l10n.js sync English, translate, then check English --strict)" } }
$a += "$root\src\*.cs"
# the arguments go in a response file: with every model, photo and text built in, the command line outgrew Windows' limit
$rsp = "$root\build\csc.rsp"
$a | % { '"' + $_ + '"' } | Set-Content -Path $rsp -Encoding UTF8
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe "@$rsp" 2>&1 | Select-Object -First 30
$built = $LASTEXITCODE; Remove-Item $rsp -ErrorAction SilentlyContinue
if ($built -ne 0) { throw "BUILD FAILED" }
# every Harmony patch finds its method on the type it names (one that does not does nothing, with one log line): tools/patchcheck.ps1
& powershell -NoProfile -ExecutionPolicy Bypass -File "$root\tools\patchcheck.ps1" $asm $M $C; if ($LASTEXITCODE -ne 0) { throw "PATCH CHECK FAILED" }
Copy-Item $asm $out -Force; Remove-Item "$root\build\name" -Recurse
# the ravens' phrase files in English, every theme, list and creature of the dll just made (the user 2026-11-11): its originals out of it, then tools/phrasecheck_lang.js
if ($RavenItem -and -not $DevKit) { & powershell -NoProfile -ExecutionPolicy Bypass -File "$root\tools\langdump.ps1" -Version $Version | Select-Object -Last 3; if ($LASTEXITCODE -ne 0) { throw "LANG DUMP FAILED (tools/langdump.ps1)" }; & node "$root\tools\phrasecheck_lang.js" English --strict | Select-Object -Last 15; if ($LASTEXITCODE -ne 0) { throw "RAVENS' ENGLISH CHECK FAILED (node tools/phrasecheck_lang.js English --strict)" } }

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
    # Thunderstore takes a changelog of at most 100000 characters: the store gets 2.1 and later only, the full one stays in package/
    $cut = [regex]::Match($changelog, '(?m)^## (1\.|2\.0\.)')
    if ($cut.Success) { $changelog = $changelog.Substring(0, $cut.Index) + "## 1.x – 2.0`r`n`r`nEarlier versions (1.0–2.0) are left out here: the store limits the changelog's length.`r`n`r`nРанні версії (1.0–2.0) тут не наведено: магазин обмежує довжину списку змін.`r`n" }
}
if ($changelog.Length -gt 100000) { throw "CHANGELOG too long for Thunderstore: $($changelog.Length) characters (max 100000)" }
[IO.File]::WriteAllText("$stage\CHANGELOG.md", $changelog, $utf8)
[IO.File]::WriteAllText("$stage\README.md", $readme, $utf8)

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
[void][IO.Directory]::CreateDirectory("$root\releases"); $zip="$root\releases\RavenWatch-$Version$tag.zip"; if (Test-Path $zip) { [IO.File]::Delete($zip) }
$z=[IO.Compression.ZipFile]::Open($zip,'Create'); foreach($f in 'manifest.json','README.md','CHANGELOG.md','icon.png','plugins/RavenWatch.dll'){ [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,(Join-Path $stage $f),$f,'Optimal') }
# the Book of ravens' fonts are built into the dll: their licences go with it
if ($RavenItem -and (Test-Path "$root\assets\fonts")) { Get-ChildItem "$root\assets\fonts\OFL-*.txt" | % { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,$_.FullName,"plugins/fonts/$($_.Name)",'Optimal') } }
# the story's own music and sounds (AudioBank): files beside the dll, read only when a scene names them
if ($RavenItem -and (Test-Path "$root\assets\story\sounds")) { Get-ChildItem "$root\assets\story\sounds\*" -Include *.ogg,*.wav,*.mp3 | % { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,$_.FullName,"plugins/sounds/$($_.Name)",'Optimal') } }
# the ravens' voice (voicehost: RavenVoice.exe + ONNX Runtime, data: HolosTTS model with our knobs, vocab, stress dictionary, voices)
if ($WithVoice -and -not $NoVoice -and (Test-Path "$root\voicehost\out\RavenVoice.exe")) {
    # (test only: the voice's own plugin, built against this dll, starts the program — RavenWatch never does)
    & "$root\tools\voice_plugin_build.ps1" -Main $out -Out "$root\build\RavenWatch_Voice.dll"
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,"$root\build\RavenWatch_Voice.dll","plugins/RavenWatch_Voice.dll",'Optimal')
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

