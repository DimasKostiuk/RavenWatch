#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // For someone making models for the mod (the «для авторів» build, rw_build.ps1 -DevKit): the test window on F7, the
    // Workshop's files on, the outfit fitting open. What they mark «✓ Готово для мода» in the Workshop goes into one
    // archive on their desktop (Pack), sent to the mod's author, who puts it in the mod (tools/import_models.js).
    internal static class DevKit
    {
#if RAVEN_DEVKIT
        internal const bool On = true;
#else
        internal const bool On = false;
#endif
        internal const KeyCode WindowKey = KeyCode.F7;

        // ---------------------------------------------------------------- the archive

        // who marks a model ready: the character's name (or the computer's user)
        internal static string Author
        {
            get
            {
                string n = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "";
                if (string.IsNullOrEmpty(n)) n = Environment.UserName;
                return string.IsNullOrEmpty(n) ? "автор" : n.Trim();
            }
        }

        // Everything marked ready, the fits from the book (outfits.txt) and a list of what is in it, as one zip on the
        // desktop. Returns its path (null: nothing to pack) and what to warn about.
        internal static string Pack(out int count, out List<string> warnings)
        {
            count = 0;
            warnings = new List<string>();
            List<KeyValuePair<string, byte[]>> files = new List<KeyValuePair<string, byte[]>>();
            StringBuilder list = new StringBuilder();
            string author = Author, date = DateTime.Now.ToString("yyyy-MM-dd");
            list.Append("# RavenWatch: моделі від ").Append(author).Append(", ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
            foreach (string f in WsFiles.List())
            {
                WsModel m = WsFiles.Load(f);
                if (m == null || string.IsNullOrEmpty(m.Ready)) continue;
                count++;
                files.Add(new KeyValuePair<string, byte[]>("models/" + Path.GetFileName(f), File.ReadAllBytes(f)));
                // its photo (the tab «Фото»), when one was taken
                string key = IconPhotos.KeyOf(m);
                if (IconPhotos.HasOwn(key)) files.Add(new KeyValuePair<string, byte[]>("photos/" + key + ".png", File.ReadAllBytes(IconPhotos.FileOf(key))));
                list.Append(Path.GetFileName(f)).Append(" · ").Append(string.IsNullOrEmpty(m.Use) ? "просто модель" : m.Use).Append(" · id ").Append(m.Id).Append(" · ").Append(m.Ready).Append('\n');
                // parts the mod cannot have for everyone: from other mods, or the Workshop's own asset files
                foreach (WsPart p in m.Parts)
                {
                    if (WsFiles.Special(p.Src)) continue;
                    WsCatalog.Entry e = WsCatalog.Find(p.Src);
                    string why = WsAssets.Is(p.Src) ? "файл з «Ассетів»" : e == null ? "деталі нема в грі" : e.Mod != WsCatalog.Game ? "деталь з мода «" + e.Mod + "»" : null;
                    if (why != null) { string w = m.Name + ": " + p.Src + " — " + why; if (!warnings.Contains(w)) warnings.Add(w); }
                }
            }
            if (count == 0) return null;
            string fits = Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "outfits.txt");
            if (File.Exists(fits)) files.Add(new KeyValuePair<string, byte[]>("outfits.txt", File.ReadAllBytes(fits)));
            if (warnings.Count > 0) { list.Append("\n# Увага:\n"); foreach (string w in warnings) list.Append(w).Append('\n'); }
            files.Add(new KeyValuePair<string, byte[]>("список.txt", Encoding.UTF8.GetBytes(list.ToString())));
            string safe = author;
            foreach (char ch in Path.GetInvalidFileNameChars()) safe = safe.Replace(ch, '_');
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desk) || !Directory.Exists(desk)) desk = BepInEx.Paths.ConfigPath;
            string path = Path.Combine(desk, "RavenWatch-моделі-" + safe + "-" + date + ".zip");
            WriteZip(path, files);
            return path;
        }

        // ---------------------------------------------------------------- a plain zip (stored, UTF-8 names)

        private static uint[] _crc;
        private static uint Crc(byte[] d)
        {
            if (_crc == null)
            {
                _crc = new uint[256];
                for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; _crc[i] = c; }
            }
            uint x = 0xFFFFFFFFu;
            foreach (byte b in d) x = _crc[(x ^ b) & 0xFF] ^ (x >> 8);
            return x ^ 0xFFFFFFFFu;
        }

        private static void WriteZip(string path, List<KeyValuePair<string, byte[]>> files)
        {
            DateTime now = DateTime.Now;
            ushort time = (ushort)((now.Hour << 11) | (now.Minute << 5) | (now.Second / 2));
            ushort day = (ushort)(((now.Year - 1980) << 9) | (now.Month << 5) | now.Day);
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                List<long> offsets = new List<long>();
                List<uint> crcs = new List<uint>();
                foreach (KeyValuePair<string, byte[]> f in files)
                {
                    byte[] name = Encoding.UTF8.GetBytes(f.Key);
                    uint crc = Crc(f.Value);
                    offsets.Add(fs.Position); crcs.Add(crc);
                    w.Write(0x04034b50u); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0); w.Write(time); w.Write(day);
                    w.Write(crc); w.Write((uint)f.Value.Length); w.Write((uint)f.Value.Length); w.Write((ushort)name.Length); w.Write((ushort)0);
                    w.Write(name); w.Write(f.Value);
                }
                long dir = fs.Position;
                for (int i = 0; i < files.Count; i++)
                {
                    byte[] name = Encoding.UTF8.GetBytes(files[i].Key);
                    w.Write(0x02014b50u); w.Write((ushort)20); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0); w.Write(time); w.Write(day);
                    w.Write(crcs[i]); w.Write((uint)files[i].Value.Length); w.Write((uint)files[i].Value.Length); w.Write((ushort)name.Length);
                    w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0); w.Write(0u); w.Write((uint)offsets[i]);
                    w.Write(name);
                }
                long end = fs.Position;
                w.Write(0x06054b50u); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)files.Count); w.Write((ushort)files.Count);
                w.Write((uint)(end - dir)); w.Write((uint)dir); w.Write((ushort)0);
            }
        }
    }

    // the «для авторів» build: a word on entering the world, how to get to the Workshop
    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class DevKitSpawnPatch
    {
        private static void Postfix(Player __instance)
        {
            if (!DevKit.On || __instance != Player.m_localPlayer) return;
            __instance.Message(MessageHud.MessageType.TopLeft, "RavenWatch для авторів моделей: F7 — тестове вікно (вкладка «Майстерня»), F8 — мишка вікну чи грі.");
            // ravens to fit things on (no console needed): each one this character has nowhere yet
            try
            {
                ItemDrop.ItemData wh, wm;
                RavenItem.Worn(__instance, out wh, out wm);
                bool h = wh != null, m = wm != null;
                foreach (ItemDrop.ItemData it in __instance.GetInventory().GetAllItems())
                {
                    bool mm;
                    if (RavenItem.IsRaven(it, out mm)) { if (mm) m = true; else h = true; }
                }
                if (!h && RavenItem.Hugin != null) __instance.GetInventory().AddItem(RavenItem.Hugin, 1);
                if (!m && RavenItem.Munin != null) __instance.GetInventory().AddItem(RavenItem.Munin, 1);
                if (!h || !m) __instance.Message(MessageHud.MessageType.Center, "У сумці — ворони для примірок: правий клік, щоб посадити на плече");
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("DevKit ravens: " + e.Message); }
        }
    }
}
#endif
