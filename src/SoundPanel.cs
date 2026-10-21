#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace HuginWhispers
{
    // «Бібліотека звуків» (console: raven sounds, the test window's «Звуки» tab): every sound of the game (the sfx_ objects) in one list, to listen
    // to them in the game itself and pick some for the gifts and moments. A search, groups, ▶ to play (each press
    // plays it anew, the game picking one of its variants), ■ to stop, ★ to keep it among the chosen ones
    // (config/RavenWatch/sounds_chosen.txt, one name a line, read back when we decide what goes where).
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
        }

        private GUIStyle _row, _used, _star;
        private Vector2 _scroll;
        private string _search = "", _group = "Усі", _note = "";
        private bool _chosenOnly;
        private float _stretch = 1f;   // ▶ and ◀ alike: 2 = twice as long (and lower), 0.5 = half (and higher)
        private bool _skipQuiet;       // ◀ without the quiet lead-in (a fading tail turned round)
        private List<Snd> _all, _shown;
        private string _shownKey;
        private readonly HashSet<string> _chosen = new HashSet<string>();
        private readonly List<GameObject> _playing = new List<GameObject>();

        private static readonly string[] Groups = { "Усі", "Ворони", "Магія", "Боси", "Істоти", "Зброя", "Гравець", "Будова", "Довкілля", "Інше" };

        // what the mod plays now, shown beside the name
        private static readonly Dictionary<string, string> Used = new Dictionary<string, string>
        {
            { "sfx_raven_kaw", "обряд, удар ворона, Вірність" },
            { "sfx_raven_flap", "купол, злет, удар ворона" },
            { "sfx_offering", "обряд, купол" },
            { "sfx_levelup", "обряд, вишкіл" },
            { "sfx_bossstone_attach_done", "обряд" },
        };

        private static string ChosenFile { get { return Path.Combine(Path.Combine(Paths.ConfigPath, "RavenWatch"), "sounds_chosen.txt"); } }

        private void Awake() { ReadChosen(); }

        // the test window closed: quiet
        internal void Closed() { StopAll(); }

        // ---------------------------------------------------------------- the list
        private void Collect()
        {
            Dictionary<string, Snd> found = new Dictionary<string, Snd>();
            if (ZNetScene.instance != null)
                foreach (GameObject g in ZNetScene.instance.m_prefabs) Add(found, g);
            // many sounds are not in the game's list, only inside other objects
            foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>())
                if (g != null && g.transform.parent == null && !g.scene.IsValid()) Add(found, g);
            _all = new List<Snd>(found.Values);
            _all.Sort((a, b) => string.CompareOrdinal(a.Lower, b.Lower));
            _shownKey = null;
            if (Plugin.Instance != null) Plugin.Instance.Log("Sound library: " + _all.Count + " sounds");
        }

        private static void Add(Dictionary<string, Snd> found, GameObject g)
        {
            if (g == null || found.ContainsKey(g.name)) return;
            string lower = g.name.ToLowerInvariant();
            if (!lower.StartsWith("sfx_") || lower.Contains("(clone)") || g.GetComponent<ZNetView>() != null) return;
            AudioSource src = g.GetComponentInChildren<AudioSource>(true);
            if (src == null) return;
            Snd s = new Snd { Name = g.name, Lower = lower, Prefab = g };
            ZSFX z = g.GetComponentInChildren<ZSFX>(true);
            if (z != null && z.m_audioClips != null && z.m_audioClips.Length > 0)
            {
                s.Variants = z.m_audioClips.Length;
                s.Clips = z.m_audioClips;
                foreach (AudioClip c in z.m_audioClips) if (c != null) s.Length = Mathf.Max(s.Length, c.length);
            }
            else if (src.clip != null) { s.Variants = 1; s.Length = src.clip.length; s.Clips = new AudioClip[] { src.clip }; }
            s.Group = GroupOf(lower);
            found[g.name] = s;
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
        private void Play(Snd s)
        {
            Player p = Player.m_localPlayer;
            if (p == null || s.Prefab == null) return;
            try
            {
                GameObject made = RavenFx.PlayStretched(s.Prefab, p.transform.position + Vector3.up * 1.6f, _stretch);
                if (made == null) { _note = "Не грає."; return; }
                _playing.Add(made);
                _note = "▶ " + s.Name + Len() + (s.Variants > 1 ? " (варіантів: " + s.Variants + ", щоразу інший)" : "");
                // its backwards copies made now, silently, so ◀ answers at once
                if (s.Clips != null) foreach (AudioClip c in s.Clips) SoundBack.Prepare(c);
            }
            catch (Exception e) { _note = "Не грає: " + e.Message; }
        }

        private string Len() { return Mathf.Approximately(_stretch, 1f) ? "" : " ×" + _stretch.ToString("0.0#"); }

        private void StopAll()
        {
            foreach (GameObject g in _playing) if (g != null) Destroy(g);
            _playing.Clear();
            _backWaiting.Clear();
        }

        // ◀: one of its variants at random, backwards. The first time a variant is caught silently (as long as it
        // sounds), then it plays by itself; after that at once (kept for the session).
        private readonly List<KeyValuePair<Snd, AudioClip>> _backWaiting = new List<KeyValuePair<Snd, AudioClip>>();

        private void PlayBack(Snd s)
        {
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
            AudioSource like = s.Prefab != null ? s.Prefab.GetComponentInChildren<AudioSource>(true) : null;
            GameObject go = SoundBack.PlayClip(back, like, p.transform.position + Vector3.up * 1.6f, null, _stretch, _skipQuiet ? SoundBack.QuietStart(back) : 0f);
            if (go != null) _playing.Add(go);
            return go != null;
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

        // ---------------------------------------------------------------- the tab
        private void Update()
        {
            for (int i = _playing.Count - 1; i >= 0; i--) if (_playing[i] == null) _playing.RemoveAt(i);
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
            }
            if (_all == null) Collect();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Пошук:", GUILayout.Width(52f));
            _search = GUILayout.TextField(_search ?? "");
            if (GUILayout.Button("×", GUILayout.Width(26f))) _search = "";
            GUILayout.EndHorizontal();

            // the groups, two rows
            for (int r = 0; r < 2; r++)
            {
                GUILayout.BeginHorizontal();
                for (int i = r * 5; i < Mathf.Min(Groups.Length, r * 5 + 5); i++)
                    if (GUILayout.Toggle(_group == Groups[i], Groups[i], "Button")) _group = Groups[i];
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            _chosenOnly = GUILayout.Toggle(_chosenOnly, " лише обрані ★ (" + _chosen.Count + ")", "Button");
            if (GUILayout.Button("■ Тиша", GUILayout.Width(80f))) { StopAll(); _note = "Тиша."; }
            if (GUILayout.Button("Оновити список", GUILayout.Width(120f))) Collect();
            GUILayout.EndHorizontal();
            // how long ▶ and ◀ play: the game's own speed control (longer is lower)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Тривалість ×" + _stretch.ToString("0.00"), GUILayout.Width(120f));
            _stretch = Mathf.Round(GUILayout.HorizontalSlider(_stretch, 0.5f, 3f) * 20f) / 20f;
            if (GUILayout.Button("1×", GUILayout.Width(34f))) _stretch = 1f;
            _skipQuiet = GUILayout.Toggle(_skipQuiet, " ◀ без тихого початку", GUILayout.Width(170f));
            GUILayout.EndHorizontal();

            List<Snd> list = Shown();
            GUILayout.Label("Звуків: " + list.Count + (_all != null && list.Count != _all.Count ? " із " + _all.Count : "") + (_playing.Count > 0 ? " · грає: " + _playing.Count : ""));

            // only the rows in sight are drawn: the list is long
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            float viewH = Mathf.Max(100f, height - 280f); // minus the rows above (search, groups, buttons, length)
            int first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / RowH) - 2);
            int last = Mathf.Min(list.Count, first + Mathf.CeilToInt(viewH / RowH) + 4);
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
                GUI.Label(new Rect(x, r.y + 3f, 210f, h), s.Name, _row);
                x += 214f;
                GUI.Label(new Rect(x, r.y + 3f, 62f, h), info, _row);
                x += 64f;
                string used;
                bool mine = Used.TryGetValue(s.Name, out used);
                GUI.Label(new Rect(x, r.y + 3f, Mathf.Max(20f, r.xMax - x), h), mine ? "у моді: " + used : s.Group, mine ? _used : _row);
            }
            GUILayout.Space(Mathf.Max(0, list.Count - last) * RowH);
            GUILayout.EndScrollView();

            GUILayout.Label(_note);
        }
    }
}
#endif
