param([string]$Version = "2.1.0")
# the built-in Ukrainian phrase files out of the built dll (build/l10n_source/), the text for translations
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$M = 'E:\Steam\steamapps\common\Valheim\valheim_Data\Managed'; $C = 'D:\Thunderstore\Valheim\profiles\Valya\BepInEx\core'
$exe = "$root\build\LangDump.exe"
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /out:$exe "$root\tools\LangDump.cs" | Select-Object -First 20
if ($LASTEXITCODE -ne 0) { throw "LangDump build failed" }
& $exe "$root\build\RavenWatch-$Version.dll" "$root\build\l10n_source" $M $C
