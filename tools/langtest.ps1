param([string]$Version = "2.1.0")
# the language core checked outside the game (tools/LangTest.cs on build/RavenWatch-<version>.dll)
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\kosti\Desktop\RavensWatch'
$M = 'E:\Steam\steamapps\common\Valheim\valheim_Data\Managed'; $C = 'D:\Thunderstore\Valheim\profiles\Valya\BepInEx\core'
$exe = "$root\build\LangTest.exe"
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /out:$exe "$root\tools\LangTest.cs" | Select-Object -First 20
if ($LASTEXITCODE -ne 0) { throw "LangTest build failed" }
& $exe "$root\build\RavenWatch-$Version.dll" $M $C
