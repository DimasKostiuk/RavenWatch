#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace HuginWhispers
{
    // «Бібліотека звуків» (console: raven sounds, the test window's «Звуки» tab): in one list every sound of the game (the sfx_ objects, also the
    // ones still in its bundles, unloaded: a row like that is loaded on its first ▶ or click, RavenFx.Prefab), our own
    // files and the catalogue (assets/story/sounds), to listen to them in the game itself. A search, groups, ▶ to play (each press plays it anew,
    // one of its variants), ■ to stop, ★ to keep it among the chosen ones (config/RavenWatch/sounds_chosen.txt, the author's own).
    // A row clicked by its name opens below: what it plays, loudness, pitch, range, 2d, wet, heard from a distance through the audio core, as a scene
    // would hear it (Audio.Sound with SoundAsk.Try: the words on screen, not the line in the file), and «Зберегти» writes that line into the catalogue
    // (AudioBank.Save: only on the author's machine). A new file: put it in the folder, «Оновити список». The desk under it: a bed from the sound
    // chosen, a mute, the ear, the ravens' voice: all held by Audio.Library, let go by «Скинути все» and when the window closes.
    // The «Звуки» tab of the test window (DevPanel).
    internal class SoundPanel : MonoBehaviour
    {
        private sealed class Snd
        {
            public string Name, Lower, Group;
            public GameObject Prefab;
            public int Variants;
            public AudioClip[] Clips; // its variants, for playing one backwards
            public float Length;
            public string InBank; // (the catalogue's names that play it)
        }

        private const string BankGroup = "Каталог", FilesGroup = "Файли";

        private GUIStyle _row, _used, _star, _picked;
        private Vector2 _scroll;
        private string _search = "", _group = "Усі", _note = "";
        private bool _chosenOnly;
        private float _stretch = 1f;   // ▶ and ◀ alike: 2 = twice as long (and lower), 0.5 = half (and higher)
        private bool _skipQuiet;       // ◀ without the quiet lead-in (a fading tail turned round)
        private float _far;            // heard from this far ahead of you (m)
        private List<Snd> _game, _all, _shown;
        private string _shownKey;
        private readonly HashSet<string> _chosen = new HashSet<string>();

        private static readonly string[] Groups = { "Усі", BankGroup, FilesGroup, "Ворони", "Магія", "Боси", "Істоти", "Зброя", "Гравець", "Будова", "Довкілля", "Інше" };

        // what is being tuned
        private Snd _pick, _nextPick;   // (what opens or closes below the list changes only as a frame's layout begins: a control that appears halfway through an event is an error)
        private List<Snd> _list;
        private int _first, _last;
        private bool _showDesk;
        private string _editName = "", _editTokens = "", _mute = "sfx_footstep*";
        private float _vol = 1f, _pitch, _range, _voice = 1f;
        private float _spread; // (the line's «spread»: no slider of its own, kept so that «Зберегти» does not drop it)
        private bool _flat, _wet, _desk;
        private bool _any; // (the line's «any»: no box of its own on screen, kept as it was when the line is saved)

        private static string ChosenFile { get { return Path.Combine(Path.Combine(Paths.ConfigPath, "RavenWatch"), "sounds_chosen.txt"); } }

        private void Awake() { ReadChosen(); }

        // the test window closed: quiet
        internal void Closed() { Reset(); }

        // ---------------------------------------------------------------- the list
        // the game's own: heavy, when the tab first opens and on «Оновити список»
        private void Collect()
        {
            Dictionary<string, Snd> found = new Dictionary<string, Snd>();
            if (ZNetScene.instance != null)
                foreach (GameObject g in ZNetScene.instance.m_prefabs) Add(found, g);
            // many sounds are not in the game's list, only inside other objects
            foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>()) // heavy: as said above
                if (g != null && g.transform.parent == null && !g.scene.IsValid()) Add(found, g);
            // and the ones still in the game's bundles, not loaded yet: listed by name, loaded on their first ▶ or click (Fill)
            foreach (string n in RavenFx.SoundsInBundles())
                if (!found.ContainsKey(n)) found[n] = new Snd { Name = n, Lower = n.ToLowerInvariant(), Group = GroupOf(n.ToLowerInvariant()) };
            _game = new List<Snd>(found.Values);
            List<string> names = new List<string>();
            foreach (Snd s in _game) names.Add(s.Name);
            WriteList(names);
            if (Plugin.Instance != null) Plugin.Instance.Log("Sound library: " + _game.Count + " sounds");
            Rebuild();
        }

        private static void WriteList(List<string> names)
        {
            List<string> tracks = Audio.GameTracks();
            if (names.Count > 0 && tracks.Count > 0) AudioBank.WriteGameList(names, tracks); // (with no music yet the list would be a lie)
        }

        // the build holds the sound names to what the game has; none written yet: now, when the character first appears
        internal static void ListIfNone()
        {
            if (AudioBank.GameListThere) return;
            List<string> names = new List<string>();
            HashSet<string> had = new HashSet<string>();
            foreach (GameObject g in RavenFx.AllLoaded()) if (IsSound(g) && had.Add(g.name)) names.Add(g.name);
            foreach (string n in RavenFx.SoundsInBundles()) if (had.Add(n)) names.Add(n);
            WriteList(names);
        }

        private static bool IsSound(GameObject g)
        {
            if (g == null) return false;
            string lower = g.name.ToLowerInvariant();
            return lower.StartsWith("sfx_") && !lower.Contains("(clone)") && g.GetComponentInChildren<AudioSource>(true) != null; // heavy: the list of the game's sounds, on its making only (a networked sfx_ is a sound too: AudioBank.OfPrefab)
        }

        // the game's sounds with the files and the catalogue beside them (no search through the scene: after every save)
        private void Rebuild()
        {
            Dictionary<string, Snd> byName = new Dictionary<string, Snd>();
            _all = new List<Snd>();
            if (_game != null) foreach (Snd s in _game) { s.InBank = null; _all.Add(s); byName[s.Name] = s; }
            foreach (string file in AudioBank.FilesThere())
            {
                Snd s = new Snd { Name = file, Lower = file.ToLowerInvariant(), Group = FilesGroup };
                _all.Add(s);
                byName[file] = s;
            }
            List<Snd> bank = new List<Snd>();
            foreach (string name in AudioBank.Names())
            {
                Snd row = new Snd { Name = name, Lower = name.ToLowerInvariant(), Group = BankGroup };
                foreach (string token in AudioBank.Find(name).Tokens)
                {
                    Snd of;
                    if (!byName.TryGetValue(token, out of)) continue;
                    of.InBank = of.InBank == null ? name : of.InBank + ", " + name;
                    if (row.Clips == null && of.Clips != null) { row.Clips = of.Clips; row.Prefab = of.Prefab; row.Variants = of.Variants; row.Length = of.Length; }
                }
                bank.Add(row);
            }
            _all.AddRange(bank);
            _all.Sort((a, b) => string.CompareOrdinal(a.Lower, b.Lower));
            _shownKey = null;
            if (_nextPick != null) _nextPick = _all.Find(s => s.Name == _nextPick.Name);
        }

        private static void Add(Dictionary<string, Snd> found, GameObject g)
        {
            if (g == null || found.ContainsKey(g.name) || !IsSound(g)) return;
            string lower = g.name.ToLowerInvariant();
            Snd s = new Snd { Name = g.name, Lower = lower, Group = GroupOf(lower) };
            Fill(s, g);
            found[g.name] = s;
        }

        // what a row knows of its sound (variants, length, the clips for ◀); a row still in the game's bundles: loaded now
        private static void Fill(Snd s, GameObject g)
        {
            if (g == null) return;
            s.Prefab = g;
            AudioSource src = g.GetComponentInChildren<AudioSource>(true); // heavy: the list of the game's sounds, on its making, or one row on its first click
            ZSFX z = g.GetComponentInChildren<ZSFX>(true); // heavy: the same
            if (z != null && z.m_audioClips != null && z.m_audioClips.Length > 0)
            {
                s.Variants = z.m_audioClips.Length;
                s.Clips = z.m_audioClips;
                foreach (AudioClip c in z.m_audioClips) if (c != null) s.Length = Mathf.Max(s.Length, c.length);
            }
            else if (src != null && src.clip != null) { s.Variants = 1; s.Length = src.clip.length; s.Clips = new AudioClip[] { src.clip }; }
        }

        private static void Loaded(Snd s)
        {
            if (s.Prefab == null && s.Group != BankGroup && s.Group != FilesGroup) Fill(s, RavenFx.Prefab(s.Name));
        }

        private static bool Any(string s, params string[] words) { foreach (string w in words) if (s.Contains(w)) return true; return false; }

        private static string GroupOf(string n)
        {
            if (Any(n, "raven", "crow", "bird", "gull", "seagal", "odin")) return "Ворони";
            if (Any(n, "eikthyr", "gd_king", "elder", "bonemass", "dragon", "moder", "goblinking", "yagluth", "seekerqueen", "queen", "fader", "boss")) return "Боси";
            if (Any(n, "staff", "magic", "spell", "eitr", "lightning", "frost", "fireball", "demister", "wisp", "guardian", "gp_", "shield_", "summon", "rune", "portal", "teleport", "mead", "potion")) return "Магія";
            if (Any(n, "sword", "axe", "bow", "arrow", "club", "knife", "spear", "mace", "atgeir", "sledge", "crossbow", "hit", "swing", "blocked", "parry", "weapon", "pickaxe", "throw", "bomb")) return "Зброя";
            if (Any(n, "greydwarf", "troll", "skeleton", "draugr", "wolf", "deer", "boar", "neck", "blob", "leech", "serpent", "lox", "deathsquito", "fenring", "drake", "hatchling", "goblin", "fuling", "seeker", "gjall", "tick", "dverger", "charred", "morgen", "asksvin", "volture", "bat", "ghost", "wraith", "abomination", "surtling", "golem", "ulv", "cultist", "hare", "chicken", "hen", "fish", "bjorn", "bear", "unbjorn", "creature", "monster", "growl", "alert", "death", "idle")) return "Істоти";
            if (Any(n, "player", "eat", "drink", "jump", "land", "footstep", "step", "swim", "dodge", "equip", "unequip", "pickup", "dropped", "sleep", "rested", "hurt", "die", "emote", "level", "skill", "discover")) return "Гравець";
            if (Any(n, "build", "place", "wood", "stone", "door", "chest", "workbench", "forge", "smelter", "kiln", "cauldron", "repair", "destroy", "remove", "piece", "craft", "cart", "ship", "boat", "sail", "lock")) return "Будова";
            if (Any(n, "wind", "rain", "water", "wave", "thunder", "tree", "bush", "rock", "fire", "torch", "lava", "ice", "snow", "mist", "storm", "ambient", "ocean", "river", "mine", "ore", "dig", "geyser")) return "Довкілля";
            return "Інше";
        }

        private List<Snd> Shown()
        {
            string key = _search + "|" + _group + "|" + _chosenOnly + "|" + _chosen.Count;
            if (_shown != null && key == _shownKey) return _shown;
            _shownKey = key;
            _shown = new List<Snd>();
            if (_all == null) return _shown;
            string q = _search.Trim().ToLowerInvariant();
            foreach (Snd s in _all)
            {
                if (_group != "Усі" && s.Group != _group) continue;
                if (_chosenOnly && !_chosen.Contains(s.Name)) continue;
                if (q.Length > 0 && !s.Lower.Contains(q)) continue;
                _shown.Add(s);
            }
            return _shown;
        }

        // ---------------------------------------------------------------- playing
        // where it is heard from: ahead of you by _far
        private Vector3 Where()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return Vector3.zero;
            Vector3 head = p.transform.position + Vector3.up * 1.6f;
            Camera cam = Utils.GetMainCamera();
            Vector3 ahead = cam != null ? cam.transform.forward : p.transform.forward;
            return head + ahead * _far;
        }

        // through the core, as a scene would hear it (a track is no one-shot: heard as music)
        private bool Hear(string name, AudioBank.Entry words)
        {
            if (Player.m_localPlayer == null) return false;
            if (Audio.Sound(name, Where(), new SoundAsk { Owner = Audio.Library, Stretch = _stretch, Try = words })) return true;
            AudioBank.Entry e = words ?? AudioBank.Find(name);
            if (!AudioBank.IsTrack(e)) return false;
            if (words != null) Audio.MusicOff(Audio.Library, 0.2f); // (the same name with other words: heard anew)
            return Audio.Music(Audio.Library, AudioLayer.Scene, name, 1f, 0f, 1f, true, 0f, words);
        }

        private void Play(Snd s)
        {
            Loaded(s);
            if (!Hear(s.Name, null)) { _note = "Не грає: у цій грі такого нема."; return; }
            _note = "▶ " + s.Name + Len() + (s.Variants > 1 ? " (варіантів: " + s.Variants + ", щоразу інший)" : "") + (_far > 0f ? ", за " + Mathf.RoundToInt(_far) + " м" : "");
            // its backwards copies made now, silently, so ◀ answers at once
            if (s.Clips != null) foreach (AudioClip c in s.Clips) SoundBack.Prepare(c);
        }

        private string Len() { return Mathf.Approximately(_stretch, 1f) ? "" : " ×" + _stretch.ToString("0.0#"); }

        private void StopAll()
        {
            Audio.StopSounds(Audio.Library, null, 0.1f);
            Audio.BedOff(Audio.Library, null, 0.3f);
            Audio.MusicOff(Audio.Library, 0.5f);
            _backWaiting.Clear();
        }

        private void Reset()
        {
            _backWaiting.Clear();
            Audio.End(Audio.Library, 0.3f, true);
            _voice = 1f;
        }

        // ◀: one of its variants at random, backwards. The first time a variant is caught silently (as long as it
        // sounds), then it plays by itself; after that at once (kept for the session).
        private readonly List<KeyValuePair<Snd, AudioClip>> _backWaiting = new List<KeyValuePair<Snd, AudioClip>>();

        private void PlayBack(Snd s)
        {
            Loaded(s);
            if (s.Clips == null || s.Clips.Length == 0) { _note = "У цього звуку нема кліпу."; return; }
            AudioClip clip = null;
            for (int tries = 0; tries < 8 && clip == null; tries++) clip = s.Clips[UnityEngine.Random.Range(0, s.Clips.Length)];
            if (clip == null) { _note = "У цього звуку нема кліпу."; return; }
            string which = s.Variants > 1 ? " (варіант " + (Array.IndexOf(s.Clips, clip) + 1) + " із " + s.Variants + ")" : "";
            if (SoundBack.Made(clip))
            {
                if (!Back(s, clip)) _note = "◀ " + s.Name + which + ": не вдалося записати.";
                else _note = "◀ " + s.Name + which + Len();
                return;
            }
            SoundBack.Prepare(clip);
            _backWaiting.Add(new KeyValuePair<Snd, AudioClip>(s, clip));
            _note = "◀ " + s.Name + which + ": записую (" + clip.length.ToString("0.0") + " с тиші — так довго, як звучить сам звук), тоді зіграю навпаки…";
        }

        // the backwards clip through a player like the sound's own (its mixer: the game's sound volume counts)
        private bool Back(Snd s, AudioClip clip)
        {
            AudioClip back = SoundBack.Get(clip);
            Player p = Player.m_localPlayer;
            if (back == null || p == null) return false;
            AudioSource like = s.Prefab != null ? s.Prefab.GetComponentInChildren<AudioSource>(true) : null; // heavy: on a press of ◀
            return SoundBack.PlayClip(back, like, Where(), null, _stretch, _skipQuiet ? SoundBack.QuietStart(back) : 0f, 0f, 0f, Audio.Library);
        }

        // ---------------------------------------------------------------- the chosen ones
        private void ReadChosen()
        {
            _chosen.Clear();
            try
            {
                if (!File.Exists(ChosenFile)) return;
                foreach (string l in File.ReadAllLines(ChosenFile, Encoding.UTF8))
                {
                    string t = l.Trim();
                    if (t.Length > 0 && !t.StartsWith("#")) _chosen.Add(t);
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Chosen sounds could not be read: " + e.Message); }
        }

        private void SaveChosen()
        {
            try
            {
                List<string> lines = new List<string>(_chosen);
                lines.Sort(StringComparer.Ordinal);
                lines.Insert(0, "# Обрані звуки з бібліотеки (raven sounds). Одна назва на рядок.");
                Directory.CreateDirectory(Path.GetDirectoryName(ChosenFile));
                File.WriteAllLines(ChosenFile, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception e) { _note = "Не зберіг: " + e.Message; }
        }

        // ---------------------------------------------------------------- tuning one
        private void Pick(Snd s)
        {
            Loaded(s);
            _nextPick = s;
            bool bank = s.Group == BankGroup;
            AudioBank.Entry e = bank ? AudioBank.Find(s.Name) : new AudioBank.Entry { Tokens = new[] { s.Name } };
            _editName = bank ? s.Name : Suggest(s.Name);
            _editTokens = string.Join(" ", e.Tokens);
            _vol = e.Vol; _pitch = e.Pitch; _range = e.Range; _flat = e.Flat; _wet = e.Wet; _any = e.Any; _spread = e.Spread;
        }

        private static string Suggest(string name)
        {
            string n = name.StartsWith("sfx_") ? name.Substring(4) : name.StartsWith("file:") ? Path.GetFileNameWithoutExtension(name.Substring(5)) : name;
            StringBuilder b = new StringBuilder();
            foreach (char c in n) b.Append((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' ? c : '_');
            return b.ToString();
        }

        private AudioBank.Entry Edited()
        {
            return new AudioBank.Entry { Tokens = _editTokens.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), Vol = _vol, Pitch = _pitch, Range = _range, Spread = _spread, Flat = _flat, Wet = _wet, Any = _any };
        }

        private static float Row(string title, float v, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, GUILayout.Width(150f));
            v = Mathf.Round(GUILayout.HorizontalSlider(v, min, max) / step) * step;
            GUILayout.EndHorizontal();
            return v;
        }

        private const float EditorH = 212f, DeskH = 142f;

        private void Editor()
        {
            bool bank = _pick.Group == BankGroup;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Назва:", GUILayout.Width(52f));
            _editName = GUILayout.TextField(_editName ?? "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Грає:", GUILayout.Width(52f));
            _editTokens = GUILayout.TextField(_editTokens ?? "");
            GUILayout.EndHorizontal();
            _vol = Row("Гучність " + _vol.ToString("0.00"), _vol, 0f, 2f, 0.05f);
            _pitch = Row("Тон " + (_pitch > 0f ? _pitch.ToString("0.00") : "свій"), _pitch, 0f, 2f, 0.05f);
            _range = Row("Дальність " + (_range > 0f ? Mathf.RoundToInt(_range) + " м" : "своя"), _range, 0f, 200f, 5f);
            _far = Row("Слухати за " + Mathf.RoundToInt(_far) + " м", _far, 0f, 100f, 1f);
            GUILayout.BeginHorizontal();
            _flat = GUILayout.Toggle(_flat, " 2d (без місця, у вухах)", GUILayout.Width(190f));
            _wet = GUILayout.Toggle(_wet, " wet (як звук світу)", GUILayout.Width(170f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Слухати")) _note = Hear(_editName.Length > 0 ? _editName : _pick.Name, Edited()) ? "▶ з цими значеннями (ще не збережено)" : "Не грає: такого звуку нема.";
            GUI.enabled = AudioBank.CanWrite;
            if (GUILayout.Button(bank ? "Зберегти" : "Новий звук")) Save();
            GUI.enabled = AudioBank.CanWrite && bank;
            if (GUILayout.Button("Видалити", GUILayout.Width(90f))) Remove();
            GUI.enabled = true;
            if (GUILayout.Button("Закрити", GUILayout.Width(80f))) _nextPick = null;
            GUILayout.EndHorizontal();
            string line = (AudioBank.IsTrack(Edited()) && !_editTokens.Contains("sfx_") ? "music " + _editName + " 3" : "sound " + _editName + " player");
            GUILayout.BeginHorizontal();
            GUILayout.TextField(line);
            if (GUILayout.Button("Копіювати", GUILayout.Width(100f))) { GUIUtility.systemCopyBuffer = line; _note = "Скопійовано: " + line; }
            GUILayout.EndHorizontal();
        }

        private void Save()
        {
            string name = (_editName ?? "").Trim();
            string why = AudioBank.Save(name, Edited());
            if (why != null) { _note = why; return; }
            _note = "Записано в каталог: " + name;
            Rebuild();
            Snd row = _all.Find(s => s.Name == name && s.Group == BankGroup);
            if (row != null) _nextPick = row;
        }

        private void Remove()
        {
            string name = _pick.Name, why = AudioBank.Remove(name);
            if (why != null) { _note = why; return; }
            _note = "Прибрано з каталогу: " + name;
            _nextPick = null;
            Rebuild();
        }

        // the desk: what stays on until it is let go
        private void Desk()
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = _pick != null;
            if (GUILayout.Button("▶ Фон із вибраного") && _pick != null)
            {
                AudioBank.Entry words = Edited();
                bool on = Audio.Bed(Audio.Library, _pick.Name, 1f, 1.5f, new SoundAsk { Owner = Audio.Library, Flat = _far <= 0f, Try = words }, Where());
                _note = on ? "Фон: " + _pick.Name + (_far > 0f ? ", за " + Mathf.RoundToInt(_far) + " м" : "") : "Фоном не грає: такого нема.";
            }
            GUI.enabled = true;
            if (GUILayout.Button("■ Фони", GUILayout.Width(80f))) Audio.BedOff(Audio.Library, null, 1f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Заглушити:", GUILayout.Width(86f));
            _mute = GUILayout.TextField(_mute ?? "");
            if (GUILayout.Button("Глушити", GUILayout.Width(80f)) && _mute.Trim().Length > 0) Audio.Mute(Audio.Library, _mute.Trim());
            if (GUILayout.Button("Повернути", GUILayout.Width(90f))) Audio.Unmute(Audio.Library, null);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Чути від:", GUILayout.Width(86f));
            if (GUILayout.Button("гравця")) Audio.Ear(Audio.Library, false, 0.5f);
            if (GUILayout.Button("камери")) Audio.Ear(Audio.Library, true, 0.5f);
            GUILayout.EndHorizontal();
            float was = _voice;
            _voice = Row("Голос воронів " + _voice.ToString("0.00"), _voice, 0f, 1f, 0.05f);
            if (_voice != was) Audio.Mix(Audio.Library, AudioMix.Voice, _voice, 0.2f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Скинути все")) { Reset(); _note = "Усе відпущено."; }
            if (GUILayout.Button("Що звучить")) _note = Audio.Report();
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- the tab
        private void Update()
        {
            for (int i = _backWaiting.Count - 1; i >= 0; i--)
            {
                KeyValuePair<Snd, AudioClip> w = _backWaiting[i];
                if (!SoundBack.Made(w.Value)) continue;
                _backWaiting.RemoveAt(i);
                _note = Back(w.Key, w.Value) ? "◀ " + w.Key.Name + ": навпаки (записано, далі грає одразу)." : "◀ " + w.Key.Name + ": не вдалося записати.";
            }
        }

        private const float RowH = 26f;

        internal void Body(float height)
        {
            if (_row == null)
            {
                _row = new GUIStyle(GUI.skin.label); _row.fontSize = 13; _row.wordWrap = false; _row.clipping = TextClipping.Clip;
                _used = new GUIStyle(_row); _used.normal.textColor = new Color(0.55f, 0.85f, 1f); _used.fontSize = 11;
                _star = new GUIStyle(GUI.skin.button); _star.fontSize = 14;
                _picked = new GUIStyle(_row); _picked.normal.textColor = new Color(1f, 0.83f, 0.3f);
            }
            if (_game == null) Collect();
            bool layout = Event.current.type == EventType.Layout;
            if (layout) { _pick = _nextPick; _showDesk = _desk; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Пошук:", GUILayout.Width(52f));
            _search = GUILayout.TextField(_search ?? "");
            if (GUILayout.Button("×", GUILayout.Width(26f))) _search = "";
            GUILayout.EndHorizontal();

            // the groups, two rows
            for (int r = 0; r < 2; r++)
            {
                GUILayout.BeginHorizontal();
                for (int i = r * 6; i < Mathf.Min(Groups.Length, r * 6 + 6); i++)
                    if (GUILayout.Toggle(_group == Groups[i], Groups[i], "Button")) _group = Groups[i];
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            _chosenOnly = GUILayout.Toggle(_chosenOnly, " лише обрані ★ (" + _chosen.Count + ")", "Button");
            if (GUILayout.Button("■ Тиша", GUILayout.Width(80f))) { StopAll(); _note = "Тиша."; }
            if (GUILayout.Button("Оновити список", GUILayout.Width(120f))) Collect();
            _desk = GUILayout.Toggle(_desk, "Пульт", "Button", GUILayout.Width(64f));
            GUI.enabled = AudioBank.CanWrite;
            if (GUILayout.Button("Тека", GUILayout.Width(52f))) Application.OpenURL(new Uri(AudioBank.WhereFiles).AbsoluteUri);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            // how long ▶ and ◀ play: the game's own speed control (longer is lower)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Тривалість ×" + _stretch.ToString("0.00"), GUILayout.Width(120f));
            _stretch = Mathf.Round(GUILayout.HorizontalSlider(_stretch, 0.5f, 3f) * 20f) / 20f;
            if (GUILayout.Button("1×", GUILayout.Width(34f))) _stretch = 1f;
            _skipQuiet = GUILayout.Toggle(_skipQuiet, " ◀ без тихого початку", GUILayout.Width(170f));
            GUILayout.EndHorizontal();

            if (layout || _list == null) _list = Shown();
            List<Snd> list = _list;
            int heard = Audio.Heard(Audio.Library);
            GUILayout.Label("Звуків: " + list.Count + (_all != null && list.Count != _all.Count ? " із " + _all.Count : "") + (heard > 0 ? " · грає: " + heard : "") + "   (клік по назві: налаштувати)");

            // only the rows in sight are drawn: the list is long
            float viewH = Mathf.Max(104f, height - 300f - (_pick != null ? EditorH : 0f) - (_showDesk ? DeskH : 0f)); // minus the rows above (search, groups, buttons, length) and what is open below
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(viewH));
            if (layout) { _first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / RowH) - 2); _last = Mathf.Min(list.Count, _first + Mathf.CeilToInt(viewH / RowH) + 4); }
            int first = Mathf.Min(_first, list.Count), last = Mathf.Min(_last, list.Count);
            GUILayout.Space(first * RowH);
            for (int i = first; i < last; i++)
            {
                Snd s = list[i];
                // one exact strip per row (layout margins would shift the rows the scroll counts on)
                Rect r = GUILayoutUtility.GetRect(10f, RowH, GUILayout.ExpandWidth(true));
                float x = r.x, y = r.y + 2f, h = RowH - 4f;
                if (GUI.Button(new Rect(x, y, 30f, h), "▶")) Play(s);
                x += 32f;
                if (GUI.Button(new Rect(x, y, 30f, h), "◀")) PlayBack(s);
                x += 34f;
                bool star = _chosen.Contains(s.Name);
                GUI.color = star ? new Color(1f, 0.83f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
                if (GUI.Button(new Rect(x, y, 30f, h), star ? "★" : "☆", _star))
                {
                    if (star) _chosen.Remove(s.Name); else _chosen.Add(s.Name);
                    SaveChosen();
                }
                GUI.color = Color.white;
                x += 36f;
                string info = s.Length > 0f ? s.Length.ToString("0.0") + " с" : "";
                if (s.Variants > 1) info += " ×" + s.Variants;
                if (GUI.Button(new Rect(x, r.y + 3f, 210f, h), s.Name, s == _pick ? _picked : _row)) Pick(s);
                x += 214f;
                GUI.Label(new Rect(x, r.y + 3f, 62f, h), info, _row);
                x += 64f;
                bool mine = s.InBank != null;
                GUI.Label(new Rect(x, r.y + 3f, Mathf.Max(20f, r.xMax - x), h), mine ? "у каталозі: " + s.InBank : s.Group, mine ? _used : _row);
            }
            GUILayout.Space(Mathf.Max(0, list.Count - last) * RowH);
            GUILayout.EndScrollView();

            if (_pick != null) Editor();
            if (_showDesk) Desk();
            GUILayout.Label(_note);
        }
    }
}
#endif
