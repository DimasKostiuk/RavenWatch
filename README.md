# RavenWatch

A Valheim mod (BepInEx): Odin's ravens Hugin and Munin live on your shoulders — they fly, fight, fetch, carry mail and roast you for every death. Trust rituals, gifts, orders, Odin's errands and runes. English & Ukrainian. Works on vanilla.

> 🔞 Contains strong language (Munin swears).

- **Download:** [Thunderstore — Ko4erga/RavenWatch](https://thunderstore.io/c/valheim/p/Ko4erga/RavenWatch/)
- **Player guide:** [package/README.md](package/README.md) (the same text as on Thunderstore)
- **Changes:** [package/CHANGELOG.md](package/CHANGELOG.md)
- **Bugs, ideas:** [Discord](https://discord.gg/XM5kuwpAUU)

## Repository layout

| Folder | What's in it |
|---|---|
| `src/` | C# source of the plugin (namespace `HuginWhispers`, GUID `ko4erga.ravenwatch`) |
| `assets/` | Embedded resources: icons, book art, fonts (OFL), models (`fbx/`), gear and perch definitions, language files (`lang/<Language>/`) |
| `tools/` | Build script, phrase and talk checks, art generators, phrasebook |
| `package/` | Thunderstore package texts: README, CHANGELOG, manifest, icon |
| `voice_package/` | Package texts for the optional voice addon RavenWatch_Voice |

## Building

Windows, .NET Framework 4 `csc.exe` (C# 5), Node.js for the phrase checks.

```powershell
powershell -File tools/rw_build.ps1 -Version 2.4.0 -RavenItem
```

The script references Valheim's `Managed` folder and a BepInEx `core` folder by absolute paths at its top — change them to yours. Output: `build/RavenWatch-<version>.dll` and a Thunderstore zip in the project root.
