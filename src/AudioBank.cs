#if RAVEN_ITEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HuginWhispers
{
    // What a name means to the audio core (Audio): assets/story/sounds/*.txt, a line each «name a b … [options]» — the first of
    // a, b … the game (or the folder) has is used. Each of them:
    //   sfx_…            a game sound (the object in the «Звуки» tab)
    //   Creature/kind    a creature's or item's own sound: Ghost/idle, ShieldBanded/block (its effect list's sounds, no sparks;
    //                    they are loaded with their owner; a lone sfx_ name still in the game's bundles is loaded by RavenFx.Prefab)
    //   game:<name>      a track of the game's music (in «music» and «bed»; a bare name of a track works too)
    //   file:<x.ogg>     a file of our own (.ogg .wav .mp3): assets/story/sounds on the author's machine (StoryFiles.Source),
    //                    plugins/sounds in the package
    // Several files in one line are its variants: one of them each time, by chance, never the one before («hm file:hm_1.ogg file:hm_2.ogg
    // file:hm_3.ogg»; a track or a bed: one of them when it starts). A game sound has its own variants already.
    // A line on «@» is no sound: a word of another reader of these files («@timbre <name> …»: a person's voice, NpcTimbre).
    // Options: vol=0.8  pitch=0.9  spread=0.05  range=60  2d  wet. A name not in the files stands for itself. pitch counts for music and
    // beds too; spread for one-shots (each time its pitch a little off, here ±5%: a sound heard often is not the same each time; on top of
    // any pitch, a person's own too); range, 2d, wet for sounds and beds that have a place (Audio.Bed).
    // A file is read when a scene that names it begins (Prepare), or when first asked: never twice, a failure not retried. A
    // one-shot asked while its file is on the way waits for it (Audio.Later), as music and beds do.
    // Long ones (music, beds) stay compressed in memory; all are thrown away when a new world loads (RavenFx.Forget) and when
    // the author reads the files anew (Again: a file replaced on disk is heard at once).
    // A game sound keeps what its ZSFX would do: variants, pitch, loudness, fade in and out, delay, random pan, and how much
    // room it gets by distance (Shot, Audio.Room). What it may not keep on purpose: the game's limit on sounds at once.
    // The «Звуки» tab (SoundPanel) writes the files through Save and Remove: only on the author's machine, a line at a time, and
    // what stands round it (notes, other lines) stays as it was. A name the game has not loaded yet is looked for again later
    // (RetryEvery), not remembered as missing for the whole world.
    internal static class AudioBank
    {
        internal const string Kind = "sounds";

        internal sealed class Entry
        {
            public string[] Tokens = new string[0];
            public float Vol = 1f, Pitch, Range, Spread;
            public bool Flat, Wet;
            internal int LastClip = -1; // (of its own files, the one played last: Shot.Pick)

            // its line as the files write it, after the name
            internal string Words()
            {
                StringBuilder b = new StringBuilder(string.Join(" ", Tokens));
                if (Mathf.Abs(Vol - 1f) > 0.001f) b.Append(" vol=").Append(N(Vol));
                if (Pitch > 0f) b.Append(" pitch=").Append(N(Pitch));
                if (Spread > 0f) b.Append(" spread=").Append(N(Spread));
                if (Range > 0f) b.Append(" range=").Append(N(Range));
                if (Flat) b.Append(" 2d");
                if (Wet) b.Append(" wet");
                return b.ToString();
            }
        }

        private static string N(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        internal const int RoomOwn = 0, RoomByDistance = 1, RoomNone = 2; // (a plain source: as it is set · ZSFX with distance reverb, a file · ZSFX without it)

        // one sound of a game's effect: what its ZSFX would pick from and do, and the source to copy its 3D settings from
        internal sealed class Shot
        {
            public AudioClip[] Clips; public AudioSource Like;
            public float MinVol, MaxVol, MinPitch, MaxPitch;
            public float FadeIn, FadeOut, FadeAfter = -1f, MinDelay, MaxDelay, MinPan, MaxPan, ReverbAt;
            public bool RandomPan;
            public int Room = RoomOwn;
            public Entry Turns; // (our own files: the line they are of remembers which one played last)
            internal const int OwnTakes = 3;

            // the clip to play now: one of its variants; of our own files never the one before
            // own: a person's number — of a sound with more than OwnTakes takes it has its own three, the same three every time (its own
            // «hm»: two people of one timbre do not hum the very same takes)
            internal AudioClip Pick(uint own = 0u)
            {
                if (Clips == null || Clips.Length == 0) return Like != null ? Like.clip : null;
                int n = Clips.Length;
                if (own != 0u && n > OwnTakes)
                {
                    int a = (int)(own % (uint)n), b = (a + 1 + (int)((own >> 8) % (uint)(n - 1))) % n, c = (b + 1 + (int)((own >> 16) % (uint)(n - 1))) % n;
                    if (c == a) c = (c + 1) % n;
                    if (c == b) c = (c + 1) % n; // (a, b, c: three different takes for every n over three)
                    int turn = UnityEngine.Random.Range(0, OwnTakes), pick = turn == 0 ? a : turn == 1 ? b : c;
                    if (Turns != null && pick == Turns.LastClip) pick = pick == a ? b : pick == b ? c : a; // (not the one before)
                    if (Turns != null) Turns.LastClip = pick;
                    return Clips[pick];
                }
                if (Turns == null || Clips.Length < 2 || Turns.LastClip < 0 || Turns.LastClip >= Clips.Length)
                {
                    int any = UnityEngine.Random.Range(0, Clips.Length);
                    if (Turns != null) Turns.LastClip = any;
                    return Clips[any];
                }
                int i = UnityEngine.Random.Range(0, Clips.Length - 1);
                if (i >= Turns.LastClip) i++;
                Turns.LastClip = i;
                return Clips[i];
            }
        }

        private sealed class FileClip { public AudioClip Clip; public bool Done; }

        private static Dictionary<string, Entry> _all;
        private static readonly Dictionary<string, List<Shot>> Shots = new Dictionary<string, List<Shot>>();
        private static readonly Dictionary<string, float> Missing = new Dictionary<string, float>(); // (a sound not found: when to look again)
        internal static float RetryEvery = 30f;
        private const long LongFile = 1048576;
        private static readonly Dictionary<string, FileClip> Files = new Dictionary<string, FileClip>();
        private static readonly HashSet<string> Told = new HashSet<string>();

        internal static void Again() // (the author's files read anew: a scene reload)
        {
            Reread(); Unload();
#if RAVEN_NPC
            NpcBabble.Forget(); // (the manner and the hums are lines of the same files; the clips it copied from are let go)
#endif
        }
        private static void Reread()
        {
            _all = null; Told.Clear();
#if RAVEN_NPC
            NpcTimbre.Forget(); // (the timbres are lines of the same files)
#endif
        }

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
                    if (w.Length < 2 || w[0][0] == '@') continue;
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
                        else if (t.StartsWith("spread=", StringComparison.Ordinal)) e.Spread = Mathf.Clamp(Errands.F(t.Substring(7), 0f), 0f, 0.5f);
                        else if (t.IndexOf('=') >= 0) LogOnce("option " + w[0] + t, "«" + w[0] + "»: no option «" + t + "» (vol= pitch= spread= range= 2d wet): left out");
                        else tokens.Add(t);
                    }
                    e.Tokens = tokens.ToArray();
                    if (_all.ContainsKey(w[0])) LogOnce("twice " + w[0], "«" + w[0] + "» is written twice in the sound files: the last one counts");
                    _all[w[0]] = e;
                }
#endif
            return _all;
        }

        internal static IEnumerable<string> Names() { return All().Keys; }
        internal static bool Has(string name) { return !string.IsNullOrEmpty(name) && All().ContainsKey(name); } // (a line of the files: one who plays a sound only if it is written — NpcSpeech)

        internal static Entry Find(string name)
        {
            Entry e;
            if (All().TryGetValue(name, out e)) return e;
            return new Entry { Tokens = new[] { name } };
        }

        // ---------------------------------------------------------------- what can be played

        internal static bool IsTrack(string name) { return IsTrack(Find(name)); }

        internal static bool IsTrack(Entry e)
        {
            foreach (string t in e.Tokens) if (t.StartsWith("file:", StringComparison.Ordinal) || Audio.GameTrack(GameName(t)) != null) return true;
            return false;
        }

        internal static bool IsLoop(Entry e)
        {
            foreach (string t in e.Tokens) if (t.StartsWith("file:", StringComparison.Ordinal) || Audio.GameTrack(GameName(t)) != null || (!t.StartsWith("game:", StringComparison.Ordinal) && TokenShots(t) != null)) return true;
            return false;
        }

        private static string GameName(string token) { return token.StartsWith("game:", StringComparison.Ordinal) ? token.Substring(5) : token; }

        // a track or a bed's clip: 1 here it is, 0 still loading, -1 nothing
        internal static int Clip(Entry e, out AudioClip clip, out float vol)
        {
            clip = null;
            vol = e.Vol;
            bool files = false;
            foreach (string t in e.Tokens)
            {
                if (t.StartsWith("file:", StringComparison.Ordinal))
                {
                    if (files) continue; // (all the line's files were looked at with its first)
                    files = true;
                    List<AudioClip> own;
                    if (!OwnFiles(e, true, out own)) return 0;
                    if (own.Count > 0) { clip = own[UnityEngine.Random.Range(0, own.Count)]; return 1; }
                    continue;
                }
                MusicMan.NamedMusic m = Audio.GameTrack(GameName(t));
                if (m != null) { clip = m.m_clips[UnityEngine.Random.Range(0, m.m_clips.Length)]; vol *= m.m_volume; return 1; }
                List<Shot> shots = t.StartsWith("game:", StringComparison.Ordinal) ? null : TokenShots(t);
                if (shots != null) foreach (Shot s in shots) { AudioClip c = s.Clips != null && s.Clips.Length > 0 ? s.Clips[0] : s.Like != null ? s.Like.clip : null; if (c != null) { clip = c; if (s.MaxVol > 0f) vol *= s.MaxVol; return 1; } }
            }
            return -1;
        }

        // a line's own files, all of them: its variants (those that could be read). false: one is still on its way
        private static bool OwnFiles(Entry e, bool longOne, out List<AudioClip> clips)
        {
            clips = new List<AudioClip>();
            bool all = true;
            foreach (string t in e.Tokens)
            {
                if (!t.StartsWith("file:", StringComparison.Ordinal)) continue;
                FileClip f = Load(t.Substring(5), longOne);
                if (!f.Done) all = false;
                else if (f.Clip != null) clips.Add(f.Clip);
            }
            return all;
        }

        // a line's own files by name, each with its clip (NpcBabble: the samples of a voice's sounds). false: one is still on its way
        internal static bool FilesOf(Entry e, List<KeyValuePair<string, AudioClip>> into)
        {
            bool all = true;
            if (e == null) return true;
            foreach (string t in e.Tokens)
            {
                if (!t.StartsWith("file:", StringComparison.Ordinal)) continue;
                FileClip f = Load(t.Substring(5), false);
                if (!f.Done) all = false;
                else if (f.Clip != null) into.Add(new KeyValuePair<string, AudioClip>(t.Substring(5), f.Clip));
            }
            return all;
        }

        // a one-shot: the first of its tokens there is. Its files are one Shot with no source to copy from, each file a variant of it.
        // loading: a file is on the way (the sounds after it do not stand in: it will be there in a moment)
        internal static List<Shot> ShotsOf(Entry e, out bool loading)
        {
            loading = false;
            bool files = false;
            foreach (string t in e.Tokens)
            {
                if (t.StartsWith("file:", StringComparison.Ordinal))
                {
                    if (files) continue; // (all the line's files were looked at with its first)
                    files = true;
                    List<AudioClip> own;
                    if (!OwnFiles(e, false, out own)) { loading = true; return null; }
                    if (own.Count > 0) return new List<Shot> { new Shot { Clips = own.ToArray(), Room = RoomByDistance, Turns = e } };
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
                bool whole = true; // (a clip the game has let go since: looked for anew)
                foreach (Shot s in have) if (s.Like == null) whole = false;
                if (whole) return have;
                Shots.Remove(token);
            }
            float again;
            if (Missing.TryGetValue(token, out again) && Time.unscaledTime < again) return null; // (not here a moment ago: not looked for on every ask)
            List<Shot> r = token.IndexOf('/') > 0 ? OfOwner(token) : OfPrefab(RavenFx.Prefab(token));
            if (r == null || r.Count == 0) { Missing[token] = Time.unscaledTime + RetryEvery; return null; }
            Missing.Remove(token);
            Shots[token] = r;
            return r;
        }

        private static Shot Of(ZSFX z)
        {
            return new Shot
            {
                Clips = z.m_audioClips, Like = z.GetComponent<AudioSource>(), MinVol = z.m_minVol, MaxVol = z.m_maxVol, MinPitch = z.m_minPitch, MaxPitch = z.m_maxPitch,
                FadeIn = z.m_fadeInDuration, FadeOut = z.m_fadeOutDuration, FadeAfter = z.m_fadeOutOnAwake ? z.m_fadeOutDelay : -1f,
                MinDelay = z.m_minDelay, MaxDelay = z.m_maxDelay, RandomPan = z.m_randomPan, MinPan = z.m_minPan, MaxPan = z.m_maxPan,
                Room = z.m_distanceReverb ? RoomByDistance : RoomNone, ReverbAt = z.m_useCustomReverbDistance ? z.m_customReverbDistance : 0f
            };
        }

        private static List<Shot> OfPrefab(GameObject prefab)
        {
            if (prefab == null) return null;
            // a networked object (a creature, a piece) is not a sound: its name gives none. A sound object of the game may be
            // networked too (sfx_build_hammer_*, 2026-11-17: «none of it in this game» though loaded) — its clips are only read
            // here, the object itself is never made, so its ZNetView does not count
            if (!prefab.name.StartsWith("sfx_", StringComparison.OrdinalIgnoreCase) && prefab.GetComponent<ZNetView>() != null)
            {
                LogOnce(prefab.name, "«" + prefab.name + "» is a networked object, not a sound: name its sfx_ or Owner/kind");
                return null;
            }
            List<Shot> r = new List<Shot>();
            foreach (ZSFX z in prefab.GetComponentsInChildren<ZSFX>(true)) // heavy: once per named sound, its result kept in Shots
                r.Add(Of(z));
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
            if (path == null) { f.Done = true; LogOnce("file " + file, "no file «" + file + "» (assets/story/sounds, plugins/sounds)"); return f; }
            string uri;
            try { uri = new Uri(path).AbsoluteUri; longOne |= new FileInfo(path).Length > LongFile; } // (a big file stays compressed, whoever asked first)
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
            Missing.Clear();
        }

        // ---------------------------------------------------------------- the author's window

        private static readonly string[] Reserved = { "off", "game", "stop", "all" };

        private static string Folder()
        {
#if RAVEN_NPC
            if (StoryFiles.Source != null) return Path.Combine(StoryFiles.Source, Kind);
#endif
            return null;
        }

        internal static bool CanWrite { get { return Folder() != null; } }
        internal static string WhereFiles { get { return Folder(); } }

        // the audio files there are, as «file:x.ogg». Asked when the list is made: it looks at the disk
        internal static List<string> FilesThere()
        {
            List<string> r = new List<string>();
            List<string> where = new List<string>();
            if (Folder() != null) where.Add(Folder());
            string dll = Path.GetDirectoryName(typeof(AudioBank).Assembly.Location);
            if (!string.IsNullOrEmpty(dll)) where.Add(Path.Combine(dll, Kind));
            foreach (string w in where)
            {
                if (!Directory.Exists(w)) continue;
                foreach (string p in Directory.GetFiles(w))
                {
                    string x = Path.GetExtension(p).ToLowerInvariant(), t = "file:" + Path.GetFileName(p);
                    if ((x == ".ogg" || x == ".wav" || x == ".mp3") && !r.Contains(t)) r.Add(t);
                }
            }
            return r;
        }

        // null: written. Otherwise why not
        internal static string Save(string name, Entry e)
        {
            string why = NameWhy(name);
            if (why != null) return why;
            if (e.Tokens.Length == 0) return "Нема що грати: напиши хоч один звук.";
            foreach (string t in e.Tokens) if (t.IndexOf('=') >= 0 || t.IndexOf('#') >= 0) return "«" + t + "» не може бути звуком.";
            return Rewrite(name, name + " " + e.Words());
        }

        internal static string Remove(string name) { return Rewrite(name, null); }

        private static string NameWhy(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Дай звуку назву.";
            foreach (char c in name) if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')) return "У назві лише латинські літери, цифри й «_».";
            if (name.StartsWith("sfx_", StringComparison.OrdinalIgnoreCase)) return "Назва на «sfx_» затулила б звук гри: дай іншу.";
            if (Array.IndexOf(Reserved, name) >= 0) return "«" + name + "» — слово сцени, дай іншу назву.";
            return null;
        }

        // a line changed where it stands, its note kept; a new one at the end of story.txt; line null: taken out
        private static string Rewrite(string name, string line)
        {
            string dir = Folder();
            if (dir == null || !Directory.Exists(dir)) return "Писати можна лише там, де лежить тека assets/story/sounds.";
            try
            {
                string[] files = Directory.GetFiles(dir, "*.txt");
                Array.Sort(files, StringComparer.Ordinal);
                foreach (string f in files)
                {
                    string text = File.ReadAllText(f, Encoding.UTF8);
                    string nl = text.Contains("\r\n") ? "\r\n" : "\n";
                    List<string> ls = new List<string>(text.Replace("\r", "").Split('\n'));
                    for (int i = 0; i < ls.Count; i++)
                    {
                        string bare = StoryWords(ls[i]);
                        if (bare.Length == 0 || bare[0] == '#' || bare.Split(new[] { ' ', '\t' }, 2)[0] != name) continue;
                        if (line == null) ls.RemoveAt(i);
                        else { int note = ls[i].IndexOf(" #", StringComparison.Ordinal); ls[i] = note >= 0 ? line + "  " + ls[i].Substring(note).TrimStart() : line; }
                        File.WriteAllText(f, string.Join(nl, ls.ToArray()), new UTF8Encoding(false));
                        Reread();
                        return null;
                    }
                }
                if (line == null) return "У файлах нема «" + name + "».";
                string target = Path.Combine(dir, "story.txt");
                string had = File.Exists(target) ? File.ReadAllText(target, Encoding.UTF8) : "";
                string eol = had.Contains("\r\n") ? "\r\n" : "\n";
                File.WriteAllText(target, had + (had.Length > 0 && !had.EndsWith("\n", StringComparison.Ordinal) ? eol : "") + line + eol, new UTF8Encoding(false));
                Reread();
                return null;
            }
            catch (Exception e) { LogOnce("write " + e.Message, "the sound files could not be written: " + e.Message); return "Не записав: " + e.Message; }
        }

        private static string StoryWords(string raw)
        {
#if RAVEN_NPC
            return StoryFiles.Bare(raw);
#else
            return raw.Trim();
#endif
        }

        private static string GameListFile()
        {
            string dir = Folder();
            return dir != null ? Path.GetFullPath(Path.Combine(dir, "../../../tools/game_sounds.txt")) : null; // (assets/story/sounds → tools)
        }

        internal static bool GameListThere { get { string f = GameListFile(); return f == null || File.Exists(f); } }

        // what the game has, for the build's check of the names (tools/game_sounds.txt, written on the author's machine: by the sound
        // tab's list, or when the character first appears if there is none yet)
        internal static void WriteGameList(IEnumerable<string> sounds, IEnumerable<string> tracks)
        {
            string file = GameListFile();
            if (file == null || !Directory.Exists(Path.GetDirectoryName(file))) return;
            try
            {
                List<string> ls = new List<string>();
                foreach (string s in sounds) ls.Add("sfx " + s);
                foreach (string s in tracks) ls.Add("track " + s);
                ls.Sort(StringComparer.Ordinal);
                File.WriteAllText(file, "# written by the game (the sound tab): what storycheck holds the sound names to\n" + string.Join("\n", ls.ToArray()) + "\n", new UTF8Encoding(false));
            }
            catch (Exception e) { LogOnce("list " + e.Message, "tools/game_sounds.txt could not be written: " + e.Message); }
        }
    }
}
#endif
