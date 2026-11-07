# Every Harmony patch of the built dll finds its method in the game, checked at the build (the user 2026-11-04: «so we do not come
# back to such mistakes»): a patch whose method is not declared on the type it names does nothing, and the game only writes one
# Error line in its log and runs on (LocTouchAnimPatch named Player, the method is Humanoid's: the gesture stayed). Harmony looks the
# method up on that type only (AccessTools.DeclaredMethod), so it must be declared right there.
# Run: powershell -File tools/patchcheck.ps1 <dll> <game Managed dir> <BepInEx core dir>   (exit code 1 on a problem)
param([string]$Dll, [string]$Managed, [string]$Core)
Add-Type -Path "$Core\Mono.Cecil.dll"
$res = New-Object Mono.Cecil.DefaultAssemblyResolver
$res.AddSearchDirectory($Managed); $res.AddSearchDirectory($Core); $res.AddSearchDirectory((Split-Path $Dll))
$p = New-Object Mono.Cecil.ReaderParameters; $p.AssemblyResolver = $res
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Dll, $p)
$problems = New-Object System.Collections.Generic.List[string]
$checked = 0
# what one HarmonyPatch attribute says: a type, a name, a kind (MethodType: 0 normal, 1 getter, 2 setter, 3 constructor, 4 static ctor)
function Read-Patch($attr) {
    $o = @{ Type = $null; Name = $null; Kind = -1 }
    foreach ($a in $attr.ConstructorArguments) {
        $v = $a.Value
        if ($v -is [Mono.Cecil.TypeReference]) { $o.Type = $v }
        elseif ($v -is [string]) { $o.Name = $v }
        elseif ($a.Type.Name -eq 'MethodType') { $o.Kind = [int]$v }
    }
    return $o
}
foreach ($t in $asm.MainModule.GetTypes()) {
    $attrs = @($t.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'HarmonyPatch' })
    if ($attrs.Count -eq 0) { continue }
    if ($t.Methods | Where-Object { $_.Name -in 'TargetMethod', 'TargetMethods' }) { continue } # (it finds its method itself)
    $type = $null; $name = $null; $kind = 0
    foreach ($a in $attrs) { $o = Read-Patch $a; if ($o.Type) { $type = $o.Type }; if ($o.Name) { $name = $o.Name }; if ($o.Kind -ge 0) { $kind = $o.Kind } }
    foreach ($m in $t.Methods) { foreach ($a in @($m.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'HarmonyPatch' })) { $o = Read-Patch $a; if ($o.Type) { $type = $o.Type }; if ($o.Name) { $name = $o.Name }; if ($o.Kind -ge 0) { $kind = $o.Kind } } }
    if ($type -eq $null) { continue }
    $checked++
    $def = $null; try { $def = $type.Resolve() } catch { }
    if ($def -eq $null) { $problems.Add("$($t.Name): its type $($type.FullName) is not in the game"); continue }
    $ok = switch ($kind) {
        1 { [bool]($def.Properties | Where-Object { $_.Name -eq $name -and $_.GetMethod }) }
        2 { [bool]($def.Properties | Where-Object { $_.Name -eq $name -and $_.SetMethod }) }
        3 { [bool]($def.Methods | Where-Object { $_.Name -eq '.ctor' }) }
        4 { [bool]($def.Methods | Where-Object { $_.Name -eq '.cctor' }) }
        default { if ($name) { [bool]($def.Methods | Where-Object { $_.Name -eq $name }) } else { $true } }
    }
    if (-not $ok) {
        $where = ''; $b = $def.BaseType
        while ($b -ne $null -and $where -eq '') { $bd = $null; try { $bd = $b.Resolve() } catch { }; if ($bd -eq $null) { break }; if ($bd.Methods | Where-Object { $_.Name -eq $name }) { $where = " (it is declared on $($bd.Name): name that type)" }; $b = $bd.BaseType }
        $problems.Add("$($t.Name): $($def.Name) declares no '$name'$where; the patch would do nothing, with one line in the log")
    }
}
if ($problems.Count -gt 0) { $problems | ForEach-Object { "PATCH $_" }; "patchcheck: $($problems.Count) patch(es) find no method"; exit 1 }
"patchcheck: ok ($checked patch classes find their methods)"
