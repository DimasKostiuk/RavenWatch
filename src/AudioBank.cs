#if RAVEN_ITEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace HuginWhispers
{
    // What a name means to the audio core (Audio): assets/story/sounds/*.txt, a line each «name a b … [options]» — the first of
    // a, b … the game (or the folder) has is used. Each of them:
    //   sfx_…            a game sound (the object in the «Звуки» tab)
    //   Creature/kind    a creature's or item's own sound: Ghost/idle, ShieldBanded/block (its effect list's sounds, no sparks;
    //                    they are loaded with their owner, a lone sfx_ name often is not)
    //   game:<name>      a track of the game's music (in «music» and «bed»; a bare name of a track works too)
    //   file:<x.ogg>     a file of our own (.ogg .wav .mp3): assets/story/sounds on the author's machine (StoryFiles.Source),
    //                    plugins/sounds in the package, or config/RavenWatch/story/sounds
    // Options: vol=0.8  pitch=0.9  range=60  2d  wet. A name not in the files stands for itself.
    // A file is read when a scene that names it begins (Prepare), or when first asked: never twice, a failure not retried.
    // Long ones (music, beds) stay compressed in memory; all are thrown away when a new world loads (RavenFx.Forget) and when
    // the author reads the files anew (Again: a file replaced on disk is heard at once).
    internal static class AudioBank
    {
        internal const string Kind = "sounds";

        internal sealed class Entry { public string[] Tokens = new string[0]; public float Vol = 1f, Pitch, Range; public bool Flat, Wet; }

        // one sound of a game's effect: what its ZSFX would pick from, and the source to copy its 3D settings from
        internal sealed class Shot { public AudioClip[] Clips; public AudioSource Like; public float MinVol, MaxVol, MinPitch, MaxPitch; }

        private sealed class FileClip { public AudioClip Clip; public bool Done; }

        private static Dictionary<string, Entry> _all;
        private static readonly Dictionary<string, List<Shot>> Shots = new Dictionary<string, List<Shot>>();
        private static readonly Dictionary<string, FileClip> Files = new Dictionary<string, FileClip>();
        private static readonly HashSet<string> Told = new HashSet<string>();

        internal static void Again() { _all = null; Told.Clear(); Unload(); } // (the author's files read anew: a scene reload)

        internal static void LogOnce(string key, string text) { if (Told.Add(key) && Plugin.Instance != null) Plugin.Instance.Log("Audio: " + text); }

        private static Dictionary<string, Entry> All()
        {
            if (_all != null) return _all;
            _all = new Dictionary<string, Entry>();
#if RAVEN_NPC
            foreach (KeyValuePair<string, string> f in StoryFiles.All(Kind, null, false))
                foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(f.Value))
                {
                    string[] w = ln.Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (w.Length < 2) continue;
                    Entry e = new Entry();
                    List<string> tokens = new List<string>();
                    for (int i = 1; i < w.Length; i++)
                    {
                        string t = w[i];
                        if (t == "2d") e.Flat = true;
                        else if (t == "wet") e.Wet = true;
                        else if (t.StartsWith("vol=", StringComparison.Ordinal)) e.Vol = Errands.F(t.Substring(4), 1f);
                        else if (t.StartsWith("pitch=", StringComparison.Ordinal)) e.Pitch = Errands.F(t.Substring(6), 0f);
                        else if (t.StartsWith("range=", StringComparison.Ordinal)) e.Range = Errands.F(t.Substring(6), 0f);
                        else tokens.Add(t);
                    }
                    e.Tokens = tokens.ToArray();
                    _all[w[0]] = e;
                }
#endif
            return _all;
        }

        internal static IEnumerable<string> Names() { return All().Keys; }

        internal static Entry Find(string name)
        {
            Entry e;
            if (All().TryGetValue(name, out e)) return e;
            return new Entry { Tokens = new[] { name } };
        }

        // ---------------------------------------------------------------- what can be played

        internal static bool IsTrack(string name)
        {
            foreach (string t in Find(name).Tokens) if (t.StartsWith("file:", StringComparison.Ordinal) || Audio.GameTrack(GameName(t)) != null) return true;
            return false;
        }

        internal static bool IsLoop(string name)
        {
            foreach (string t in Find(name).Tokens) if (t.StartsWith("file:", StringComparison.Ordinal) || Audio.GameTrack(GameName(t)) != null || TokenShots(t) != null) return true;
            return false;
        }

        private static string GameName(string token) { return token.StartsWith("game:", StringComparison.Ordinal) ? token.Substring(5) : token; }

        // a track or a bed's clip: 1 here it is, 0 still loading, -1 nothing
        internal static int Clip(string name, out AudioClip clip, out float vol)
        {
            clip = null; vol = 1f;
            Entry e = Find(name);
            vol = e.Vol;
            foreach (string t in e.Tokens)
            {
                if (t.StartsWith("file:", StringComparison.Ordinal))
                {
                    FileClip f = Load(t.Substring(5), true);
                    if (!f.Done) return 0;
                    if (f.Clip != null) { clip = f.Clip; return 1; }
                    continue;
                }
                MusicMan.NamedMusic m = Audio.GameTrack(GameName(t));
                if (m != null) { clip = m.m_clips[UnityEngine.Random.Range(0, m.m_clips.Length)]; vol *= m.m_volume; return 1; }
                List<Shot> shots = t.StartsWith("game:", StringComparison.Ordinal) ? null : TokenShots(t);
                if (shots != null) foreach (Shot s in shots) { AudioClip c = s.Clips != null && s.Clips.Length > 0 ? s.Clips[0] : s.Like != null ? s.Like.clip : null; if (c != null) { clip = c; return 1; } }
            }
            return -1;
        }

        // a one-shot: the first of its tokens there is. A file is one Shot with no source to copy from.
        internal static List<Shot> ShotsOf(string name)
        {
            foreach (string t in Find(name).Tokens)
            {
                if (t.StartsWith("file:", StringComparison.Ordinal))
                {
                    FileClip f = Load(t.Substring(5), false);
                    if (!f.Done) { LogOnce("late " + name, "sound «" + name + "»: its file was not ready when asked (read on; the scene's first look at it is Prepare)"); continue; }
                    if (f.Clip != null) return new List<Shot> { new Shot { Clips = new[] { f.Clip } } };
                    continue;
                }
                if (t.StartsWith("game:", StringComparison.Ordinal)) continue;
                List<Shot> s = TokenShots(t);
                if (s != null) return s;
            }
            return null;
        }

        private static List<Shot> TokenShots(string token)
        {
            List<Shot> have;
            if (Shots.TryGetValue(token, out have))
            {
                if (have == null) return null;
                bool whole = true; // (a clip the game has let go since: looked for anew)
                foreach (Shot s in have) if (s.Like == null) whole = false;
                if (whole) return have;
            }
            List<Shot> r = token.IndexOf('/') > 0 ? OfOwner(token) : OfPrefab(RavenFx.Prefab(token));
            Shots[token] = r != null && r.Count > 0 ? r : null;
            return Shots[token];
        }

        private static List<Shot> OfPrefab(GameObject prefab)
        {
            if (prefab == null || prefab.GetComponent<ZNetView>() != null) return null;
            List<Shot> r = new List<Shot>();
            foreach (ZSFX z in prefab.GetComponentsInChildren<ZSFX>(true)) // heavy: once per named sound, its result kept in Shots
                r.Add(new Shot { Clips = z.m_audioClips, Like = z.GetComponent<AudioSource>(), MinVol = z.m_minVol, MaxVol = z.m_maxVol, MinPitch = z.m_minPitch, MaxPitch = z.m_maxPitch });
            if (r.Count == 0)
                foreach (AudioSource a in prefab.GetComponentsInChildren<AudioSource>(true)) // heavy: once per named sound, its result kept in Shots
                    if (a.clip != null) r.Add(new Shot { Like = a, MinVol = a.volume, MaxVol = a.volume, MinPitch = a.pitch, MaxPitch = a.pitch });
            return r;
        }

        // «Ghost/idle», «ShieldBanded/block»: the sounds of that owner's effect list of that kind
        private static List<Shot> OfOwner(string what)
        {
            int cut = what.IndexOf('/');
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(what.Substring(0, cut)) : null;
            if (prefab == null) return null;
            string kind = what.Substring(cut + 1);
            foreach (Component c in prefab.GetComponents<Component>())
            {
                if (c == null) continue;
                ItemDrop item = c as ItemDrop;
                List<Shot> r = OfList(item != null ? (object)item.m_itemData.m_shared : c, kind);
                if (r != null) return r;
            }
            return null;
        }

        private static List<Shot> OfList(object owner, string kind)
        {
            foreach (string f in new[] { "m_" + kind + "Sound", "m_" + kind + "Effects", "m_" + kind + "edEffects", "m_" + kind + "Effect" })
            {
                System.Reflection.FieldInfo fi = owner.GetType().GetField(f, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.FlattenHierarchy);
                EffectList el = fi != null ? fi.GetValue(owner) as EffectList : null;
                if (el == null || el.m_effectPrefabs == null) continue;
                List<Shot> r = new List<Shot>();
                foreach (EffectList.EffectData d in el.m_effectPrefabs)
                    if (d.m_enabled && d.m_prefab != null) { List<Shot> one = OfPrefab(d.m_prefab); if (one != null) r.AddRange(one); }
                if (r.Count > 0) return r;
            }
            return null;
        }

        // ---------------------------------------------------------------- files

        // the files a scene names, read now: when its step comes the clip is there
        internal static void Prepare(string name, bool longOne)
        {
            foreach (string t in Find(name).Tokens) if (t.StartsWith("file:", StringComparison.Ordinal)) Load(t.Substring(5), longOne);
        }

        private static FileClip Load(string file, bool longOne)
        {
            FileClip f;
            if (Files.TryGetValue(file, out f)) return f;
            f = new FileClip();
            Files[file] = f;
            string path = Locate(file);
            if (path == null) { f.Done = true; LogOnce("file " + file, "no file «" + file + "» (assets/story/sounds, plugins/sounds, config/RavenWatch/story/sounds)"); return f; }
            string uri;
            try { uri = new Uri(path).AbsoluteUri; }
            catch (Exception e) { f.Done = true; LogOnce("file " + file, "«" + path + "»: " + e.Message); return f; }
            Audio.Host(Read(f, path, uri, longOne));
            return f;
        }

        private static string Locate(string file)
        {
            if (file.Length == 0 || file != Path.GetFileName(file)) return null; // (a bare name only: it may come from another game, and never walks out of these folders)
            List<string> where = new List<string>();
#if RAVEN_NPC
            if (StoryFiles.Source != null) where.Add(Path.Combine(StoryFiles.Source, Kind));
#endif
            string dll = Path.GetDirectoryName(typeof(AudioBank).Assembly.Location);
            if (!string.IsNullOrEmpty(dll)) where.Add(Path.Combine(dll, Kind));
            where.Add(Path.Combine(Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "story"), Kind));
            foreach (string w in where) { string p = Path.Combine(w, file); if (File.Exists(p)) return p; }
            return null;
        }

        private static AudioType TypeOf(string path)
        {
            string x = Path.GetExtension(path).ToLowerInvariant();
            return x == ".wav" ? AudioType.WAV : x == ".mp3" ? AudioType.MPEG : AudioType.OGGVORBIS;
        }

        private static IEnumerator Read(FileClip f, string path, string uri, bool longOne)
        {
            using (UnityWebRequest q = UnityWebRequestMultimedia.GetAudioClip(uri, TypeOf(path)))
            {
                DownloadHandlerAudioClip h = (DownloadHandlerAudioClip)q.downloadHandler;
                h.compressed = longOne; // (a long one decoded as it plays, a short one ready at once)
                yield return q.SendWebRequest();
                if (q.result != UnityWebRequest.Result.Success) LogOnce("file " + path, "«" + path + "» could not be read: " + q.error);
                else
                {
                    f.Clip = h.audioClip;
                    if (f.Clip != null) f.Clip.name = Path.GetFileNameWithoutExtension(path);
                }
                f.Done = true;
                if (f.Clip != null && !Files.ContainsValue(f)) { UnityEngine.Object.Destroy(f.Clip); f.Clip = null; } // (thrown away while it was on its way)
            }
        }

        // the files go (the game's own clips are the game's), and what was looked up in that world
        internal static void Unload()
        {
            foreach (FileClip f in Files.Values) if (f.Clip != null) UnityEngine.Object.Destroy(f.Clip);
            Files.Clear();
            Shots.Clear();
        }
    }
}
#endif
