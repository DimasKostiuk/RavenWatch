# RavenWatch_Voice's own plugin (voice_plugin/*.cs): it starts the voice program and connects it to RavenWatch (RavenVoiceApi)
# usage: tools/voice_plugin_build.ps1 -Main <RavenWatch dll to build against> -Out <dll> [-StewCopy]
#   -StewCopy: the copy for the Odins_Stew hand-out zip (its own GUID; stays off when the store package is installed)
param([Parameter(Mandatory=$true)][string]$Main, [Parameter(Mandatory=$true)][string]$Out, [switch]$StewCopy)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$M = 'E:\Steam\steamapps\common\Valheim\valheim_Data\Managed'; $C = 'D:\Thunderstore\Valheim\profiles\Valya\BepInEx\core'
if (-not (Test-Path $Main)) { throw "no RavenWatch dll to build the voice plugin against: $Main" }
$refs = @("$C\BepInEx.dll", "$M\UnityEngine.dll", "$M\UnityEngine.CoreModule.dll", "$M\netstandard.dll", $Main)
$a = @('/nologo', '/target:library', '/optimize+', '/warn:4', '/utf8output', '/codepage:65001', "/out:$Out") + ($refs | % { '/r:' + $_ }) + (Get-ChildItem "$root\voice_plugin\*.cs" | % { $_.FullName })
if ($StewCopy) { $a += '/define:STEW_COPY' }
$res = & C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe @a 2>&1
$res | Select-Object -First 20
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Out)) { throw "the voice plugin did not build" }
"== voice plugin$(if ($StewCopy) { ' (Odins_Stew copy)' }) -> $Out"
