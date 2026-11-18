#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // Waves of foes at a place of the story (the user 2026-11-15, chapter 1: the dwarf Lit beset at the forest's edge — «2 хвилі ворогів простих
    // вистачить», the player helps him beat them off; «якщо ми помремо в процесі завдання, то все заморожується, і ми просто можемо повернутись
    // на це місце»). A quest's start:/done: (the world's: the server runs it):
    //   waves <name> <place> <m> <creature>:<n>[,<creature>:<n>…] [/ <the next wave> …]
    // While it stands (world text «waves_<name>»), the server once a second: a player within m + Near of the place and none of its foes left —
    // the next wave, each foe at the round's edge (m off the place, spread round it), on the ground, hunting the player (the game's own «hunt
    // player»: it comes at once), marked the scene's own foe (StoryHush.KFoe: no quiet round runs it off); the last of the last wave down — the
    // world fact <name>, the waves gone. Nobody within m + Gone (the player died, went off): its foes taken out of the world, back to the first
    // wave — the fight begins anew when one comes back. A foe is a ZDO the server makes (as a stash's chest): every game draws it, the nearest
    // player's game runs it. The people of the place fight as their templates say (Lit: «fight: defend»).
    //   … <m> rise <s> …   the first wave comes up and stands that long before it fights (the user 2026-11-15, the rite: «a wide shot as the
    //                      ghosts come» — a scene starts the waves with «do waves …» and films them rising). While it rises a foe is of the
    //                      players' side on every game that draws it (Character.m_faction: the game's own «enemy?», BaseAI.IsEnemy — so it
    //                      strikes nobody, and «calm», a scene's stop for a foe at you, Senses do not count it); then its own side again.
    internal static class StoryWaves
    {
        internal static float Near = 30f, Gone = 70f, Lift = 0.5f, Every = 1f;
        private const string Def = "waves_", At = "wavesat_";
        private static readonly int KWave = "RavenWatch_wave".GetStableHashCode(); // (on a foe: the waves it is of)
        private static readonly int KRise = "RavenWatch_waveRise".GetStableHashCode(); // (on a foe of a rising wave: the world's time, ms, it fights from)
        private static readonly List<ZDO> _near = new List<ZDO>(), _mine = new List<ZDO>();
        private static Dictionary<string, GameObject> _prefabs; // (the creatures by their names in small letters: a world text is kept so)
        private static float _next;

        internal const string Usage = "waves <назва> <місце> <м> [rise <с>] <істота>:<скільки>[,…] [/ <наступна хвиля> …]   (хвилі ворогів біля місця; останній упав — факт світу <назва>; нікого поруч — усе спочатку)";

        internal static void Actions()
        {
            StoryActions.Register("waves", new StoryActions.Kind { Dry = "set world world:{1}", Min = 4, Usage = Usage,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryWhere.Why(a[2]) ?? (Errands.F(a[3], 0f) <= 0f ? "waves: м — число" : Spec(string.Join(" ", a, SpecAt(a, 4), a.Length - SpecAt(a, 4))) == null ? Usage : null),
                Do = (p, q, w, rest) =>
                {
                    if (StoryWorld.Flag(w[1])) return null; // (beaten already: kept)
                    StoryWorld.SetText(Def + w[1], string.Join(" ", w, 2, w.Length - 2));
                    return null;
                } });
        }

        // where the creatures begin in the words («rise <s>» before them — its seconds), and those seconds (0: none)
        private static int SpecAt(string[] w, int at) { float r; return SpecAt(w, at, out r); }
        private static int SpecAt(string[] w, int at, out float rise)
        {
            rise = 0f;
            if (w.Length <= at + 2 || w[at] != "rise") return at;
            rise = Mathf.Clamp(Errands.F(w[at + 1], 0f), 0f, 60f);
            return at + 2;
        }

        // the waves as written: each a list of creature → how many; null — not as Usage says
        private static List<List<KeyValuePair<string, int>>> Spec(string text)
        {
            List<List<KeyValuePair<string, int>>> all = new List<List<KeyValuePair<string, int>>>();
            foreach (string wave in text.Split('/'))
            {
                List<KeyValuePair<string, int>> one = new List<KeyValuePair<string, int>>();
                foreach (string f in wave.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    int c = f.IndexOf(':'), n;
                    if (c <= 0 || !int.TryParse(f.Substring(c + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 1 || n > 20) return null;
                    one.Add(new KeyValuePair<string, int>(f.Substring(0, c), n));
                }
                if (one.Count == 0) return null;
                all.Add(one);
            }
            return all.Count > 0 ? all : null;
        }

        internal static void ServerTick()
        {
            if (Time.time < _next || ZNet.instance == null || !ZNet.instance.IsServer() || ZNetScene.instance == null || ZDOMan.instance == null) return;
            _next = Time.time + Every;
            foreach (KeyValuePair<string, string> kv in StoryWorld.Texts(Def)) Run(kv.Key.Substring(Def.Length), kv.Value);
        }

        private static void Run(string name, string text)
        {
            string[] w = text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            Vector3 at;
            if (w.Length < 3 || !StoryWhere.Find(w[0], out at)) return;
            float r = Errands.F(w[1], 14f), rise;
            int from = SpecAt(w, 2, out rise);
            List<List<KeyValuePair<string, int>>> waves = w.Length > from ? Spec(string.Join(" ", w, from, w.Length - from)) : null;
            if (waves == null) { Story.Log("waves «" + name + "»: not read: " + text); StoryWorld.SetText(Def + name, ""); return; }
            bool near = false, gone = true;
            foreach (ZDO c in ZNet.instance.GetAllCharacterZDOS()) // (the players' characters, every game's)
            {
                if (c == null) continue;
                float d = Vector3.Distance(c.GetPosition(), at);
                if (d < r + Near) near = true;
                if (d < r + Gone) gone = false;
            }
            _near.Clear(); _mine.Clear();
            BlueprintGround.ObjectsAround(at, r + Gone + 30f, _near); // heavy: once a second while a fight of the story stands (the server; the place's sectors)
            foreach (ZDO z in _near) if (z.GetString(KWave, "") == name) _mine.Add(z);
            int wave = StoryWorld.Var(At + name);
            if (gone)
            {
                if (_mine.Count == 0 && wave == 0) return;
                foreach (ZDO z in _mine) Take(z);
                StoryWorld.SetVar(At + name, 0);
                Story.Log("waves «" + name + "»: nobody near — " + _mine.Count + " foe(s) taken out, the fight waits for one to come back");
                return;
            }
            if (!near || _mine.Count > 0) return;
            if (wave >= waves.Count)
            {
                StoryWorld.SetFlag(name, true);
                StoryWorld.SetText(Def + name, "");
                StoryWorld.SetVar(At + name, 0);
                Story.Log("waves «" + name + "»: all " + waves.Count + " beaten");
                return;
            }
            Spawn(name, at, r, waves[wave], wave + 1, wave == 0 ? rise : 0f);
            StoryWorld.SetVar(At + name, wave + 1);
        }

        private static void Spawn(string name, Vector3 at, float r, List<KeyValuePair<string, int>> wave, int no, float rise)
        {
            long fights = rise > 0f ? (long)((ZNet.instance.GetTimeSeconds() + rise) * 1000.0) : 0L;
            int all = 0, i = 0, made = 0;
            foreach (KeyValuePair<string, int> f in wave) all += f.Value;
            float start = Random.value * 360f;
            foreach (KeyValuePair<string, int> f in wave)
            {
                GameObject pf = PrefabOf(f.Key);
                if (pf == null) { Story.Log("waves «" + name + "»: no creature «" + f.Key + "» in the game"); continue; }
                int hash = pf.name.GetStableHashCode();
                for (int k = 0; k < f.Value; k++, i++)
                {
                    float a = (start + i * 360f / Mathf.Max(1, all)) * Mathf.Deg2Rad;
                    Vector3 p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    p.y = Mathf.Max(WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(p.x, p.z) : at.y, at.y) + Lift; // (the land as the world made it — the server has no ground of its own to ask —, never under the place itself: a hall's floor stands over the land)
                    Vector3 look = at - p; look.y = 0f;
                    ZDO z = ZDOMan.instance.CreateNewZDO(p, hash);
                    z.Persistent = true;
                    z.SetPrefab(hash);
                    z.SetRotation(look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
                    z.Set(KWave, name);
                    z.Set(StoryHush.KFoe, true);
                    z.Set(ZDOVars.s_huntPlayer, true);
                    if (fights > 0L) z.Set(KRise, fights);
                    made++;
                }
            }
            Story.Log("waves «" + name + "»: wave " + no + ", " + made + " foe(s)" + (rise > 0f ? ", rising " + rise.ToString("0.#", CultureInfo.InvariantCulture) + " s" : "") + " round " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z) + " at " + r.ToString("0.#", CultureInfo.InvariantCulture) + " m");
        }

        // every frame (StoryQuests.Frame), on every game: a rising foe drawn here is of the players' side till its time, then its own again.
        // Looked at only while a rising wave stands in the world (its words, read once a second) or one is held here
        internal static float RiseEvery = 0.25f;
        private static float _riseNext, _wordsNext;
        private static bool _anyRise;
        private static readonly Dictionary<Character, Character.Faction> _rising = new Dictionary<Character, Character.Faction>(); // session: held here, and its own side
        private static readonly List<Character> _gone = new List<Character>();
        internal static void Rising(Player p)
        {
            if (ZNet.instance == null || Time.time < _riseNext) return;
            _riseNext = Time.time + RiseEvery;
            if (Time.time >= _wordsNext)
            {
                _wordsNext = Time.time + 1f;
                _anyRise = false;
                foreach (KeyValuePair<string, string> kv in StoryWorld.Texts(Def)) if (kv.Value.Contains(" rise ")) { _anyRise = true; break; } // heavy: a pass over the world's keys, once a second
            }
            if (!_anyRise && _rising.Count == 0) return;
            long now = (long)(ZNet.instance.GetTimeSeconds() * 1000.0);
            foreach (Character c in Character.GetAllCharacters()) // heavy: the drawn creatures, 4 times a second — only while a wave rises
            {
                if (c == null || c.IsPlayer()) continue;
                ZNetView nv = c.GetComponent<ZNetView>();
                ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
                if (z == null) continue;
                bool held = _rising.ContainsKey(c);
                if (z.GetLong(KRise, 0L) > now) { if (!held) { _rising[c] = c.m_faction; c.m_faction = Character.Faction.Players; } }
                else if (held) { c.m_faction = _rising[c]; _rising.Remove(c); }
            }
            _gone.Clear();
            foreach (Character c in _rising.Keys) if (c == null) _gone.Add(c);
            foreach (Character c in _gone) _rising.Remove(c);
        }

        // out of the world, whoever runs it now (the server takes it over first: nobody near runs it as a rule)
        private static void Take(ZDO z)
        {
            if (!z.IsOwner()) z.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(z);
        }

        private static GameObject PrefabOf(string name)
        {
            if (_prefabs == null)
            {
                _prefabs = new Dictionary<string, GameObject>();
                foreach (GameObject g in ZNetScene.instance.m_prefabs) if (g != null && g.GetComponent<Character>() != null) _prefabs[g.name.ToLowerInvariant()] = g;
            }
            GameObject pf;
            return _prefabs.TryGetValue((name ?? "").ToLowerInvariant(), out pf) ? pf : null;
        }
    }
}
#endif
