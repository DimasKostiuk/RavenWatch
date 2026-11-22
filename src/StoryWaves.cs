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
    // wave — the fight begins anew when one comes back. No foe is made within Clear m of a player: its spot goes farther out on its own line
    // (at most ClearMost past the round). A wave that stood WaveMost s (its count begins anew with the fight) counts as beaten: its foes left
    // are taken out and the next comes — one stuck where nobody reaches it holds no quest (the user 2026-11-20). A foe is a ZDO the server makes (as a stash's chest): every game draws it, the nearest
    // player's game runs it. The people of the place fight as their templates say (Lit: «fight: defend»).
    //   … <m> rise <s> …   the first wave comes up and stands that long before it fights (the user 2026-11-15, the rite: «a wide shot as the
    //                      ghosts come» — a scene starts the waves with «do waves …» and films them rising). While it rises a foe is of the
    //                      players' side on every game that draws it (Character.m_faction: the game's own «enemy?», BaseAI.IsEnemy — so it
    //                      strikes nobody, and «calm», a scene's stop for a foe at you, Senses do not count it); then its own side again.
    //   … rise <s> walk …  (chapter 2, the user 2026-11-18: «ми бачимо, як на нас іде 5 скелетів») while it rises it walks at the nearest
    //                      player and stops WalkNear m off him: the game that runs it turns its own mind off for that time and walks it in a
    //                      straight line (as a scene's beast: StoryBeasts), then its mind on — it fights
    //   … away <place> …   every wave comes out of one side: an arc of Arc° on the round's edge, the side away from that place (the dead come
    //                      from the forest's depth, not from the Hall they go for)
    //   … hel …            Hel's blue-green eyes on every foe of it (the key StoryBeasts.KHel: HelEyes paints it on every game)
    // The words after <m> in any order, before the creatures.
    //   wavesgo <name>     its rise is over now, whatever its seconds (the review 2026-11-18: a scene's length is not known beforehand — with
    //                      ravens or without, a walk that takes long —, and foes that fight under the bars stop it): the scene that films the rise
    //                      says it as its last step, and «rise» is only the most it may last. The world's «wavesgo_<name>»; a fresh first wave of
    //                      the same waves (a player died and came back) fights at once — no scene films it again
    //   wavesoff <name>    the fight called off and forgotten (the user 2026-11-18: the night on the tower broken — all went off, slept through —
    //                      begins again the next night): its foes out of the world (as the jump's Undo), its fact gone, so «waves <name> …»
    //                      stands anew from its first wave
    // The author's jump past or back before its fact (StoryRebuild: the fact «world <name>» set either way) — the fight that stands is taken
    // away, its foes too (Undo, through StoryQuests.Taken; the server takes the foes out on its next pass)
    internal static class StoryWaves
    {
        internal static float Near = 30f, Gone = 70f, Lift = 0.5f, Every = 1f, Arc = 110f, WalkNear = 7f, WalkFrom = 0.3f;
        internal static float Clear = 20f, ClearMost = 40f; // m: no foe made nearer a player than this — its spot on the round goes farther out, at most this past the round (the user 2026-11-20: «спавн далі… надто близько зявляются до мене»)
        internal static float WaveMost = 300f; // s a wave may stand with a player near: then it counts as beaten and its foes left go — one stuck where nobody reaches it holds no quest (the user 2026-11-20: «таймер на 5 хв»)
        private const string Def = "waves_", At = "wavesat_", Go = "wavesgo_", Since = "wavessince_", UndoMark = "- ";
        private static readonly int KWave = "RavenWatch_wave".GetStableHashCode(); // (on a foe: the waves it is of)
        private static readonly int KRise = "RavenWatch_waveRise".GetStableHashCode(); // (on a foe of a rising wave: the world's time, ms, it fights from)
        private static readonly int KWalk = "RavenWatch_waveWalk".GetStableHashCode(); // (on a foe of a rising wave with «walk»: it walks at you while it rises)
        private static readonly List<ZDO> _near = new List<ZDO>(), _mine = new List<ZDO>();
        private static Dictionary<string, GameObject> _prefabs; // (the creatures by their names in small letters: a world text is kept so)
        private static float _next;

        internal const string Usage = "waves <назва> <місце> <м> [rise <с> [walk]] [away <місце>] [hel] <істота>:<скільки>[,…] [/ <наступна хвиля> …]   (хвилі ворогів біля місця; останній упав — факт світу <назва>; нікого поруч — усе спочатку)";

        internal static void Actions()
        {
            StoryActions.Register("waves", new StoryActions.Kind { Dry = "later world world:{1}", Min = 4, Usage = Usage, Fight = true,
                Check = a =>
                {
                    if (!Story.GoodName(a[1])) return "погана назва «" + a[1] + "»";
                    string why = StoryWhere.Why(a[2]);
                    if (why != null) return why;
                    if (Errands.F(a[3], 0f) <= 0f) return "waves: м — число";
                    Opts o = OptsOf(a, 4);
                    if (o.Bad.Length > 0) return "waves: rise <с> — число, а не «" + o.Bad + "»";
                    if (o.Walk && o.Rise <= 0f) return "waves: walk — лише з rise (іде, поки встає)";
                    if (o.Away.Length > 0 && (why = StoryWhere.Why(o.Away)) != null) return "waves away: " + why;
                    if (Spec(string.Join(" ", a, o.At, a.Length - o.At)) == null) return Usage;
                    string name = a[1];
                    StoryQuests.Taken[name] = on => Undo(name); // (read once a file is read: the jump's answer for this fight's fact)
                    return null;
                },
                Do = (p, q, w, rest) =>
                {
                    if (StoryWorld.Flag(w[1])) return null; // (beaten already: kept)
                    StoryWorld.SetText(Def + w[1], string.Join(" ", w, 2, w.Length - 2));
                    return null;
                } });
            StoryActions.Register("wavesgo", new StoryActions.Kind { Dry = "none", Min = 1, Usage = "wavesgo <назва>   (хвиля, що встає, — уже в бій: сцена, що її знімає, каже це своїм останнім кроком)",
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Do = (p, q, w, rest) => { StoryWorld.SetVar(Go + w[1], 1); return null; } });
            StoryActions.Register("wavesoff", new StoryActions.Kind { Dry = "clear world world:{1}", Min = 1, Usage = "wavesoff <назва>   (бій скасовано й забуто: вороги геть, факту нема — waves <назва> стане знову з першої хвилі)",
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Do = (p, q, w, rest) => { Undo(w[1]); StoryWorld.SetFlag(w[1], false); return null; } });
        }

        // the author's jump took its fact either way: the fight that stands is gone — the server takes its foes out on its next pass (Run: the undo mark)
        private static void Undo(string name)
        {
            string text = StoryWorld.Text(Def + name);
            StoryWorld.SetText(Def + name, text.Length > 0 && !text.StartsWith(UndoMark) ? UndoMark + text : text);
            StoryWorld.SetVar(At + name, 0);
            StoryWorld.SetVar(Go + name, 0);
            StoryWorld.SetVar(Since + name, 0);
        }

        // the words between <m> and the creatures: rise <s>, walk, away <place>, hel — in any order; At: where the creatures begin
        private sealed class Opts { public float Rise; public bool Walk, Hel; public string Away = "", Bad = ""; public int At; } // (Bad: «rise» with no number after it)
        private static Opts OptsOf(string[] w, int at)
        {
            Opts o = new Opts();
            int i = at;
            while (i < w.Length)
            {
                if (w[i] == "rise" && i + 1 < w.Length) { float s; if (!float.TryParse(w[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out s)) o.Bad = w[i + 1]; o.Rise = Mathf.Clamp(s, 0f, 60f); i += 2; }
                else if (w[i] == "away" && i + 1 < w.Length) { o.Away = w[i + 1]; i += 2; }
                else if (w[i] == "walk") { o.Walk = true; i++; }
                else if (w[i] == "hel") { o.Hel = true; i++; }
                else break;
            }
            o.At = i;
            return o;
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
            bool undo = text.StartsWith(UndoMark); // (the author's jump took it: its foes out, its words gone — Undo)
            string[] w = (undo ? text.Substring(UndoMark.Length) : text).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            Vector3 at;
            if (w.Length < 3 || !StoryWhere.Find(w[0], out at)) { if (undo) StoryWorld.SetText(Def + name, ""); return; }
            if (undo)
            {
                _near.Clear();
                BlueprintGround.ObjectsAround(at, Errands.F(w[1], 14f) + Gone + 30f, _near); // heavy: once, the author's jump
                int took = 0;
                foreach (ZDO z in _near) if (z.GetString(KWave, "") == name) { Take(z); took++; }
                StoryWorld.SetText(Def + name, "");
                Story.Log("waves «" + name + "»: taken back by the author's jump — " + took + " foe(s) out");
                return;
            }
            float r = Errands.F(w[1], 14f);
            Opts o = OptsOf(w, 2);
            int from = o.At;
            List<List<KeyValuePair<string, int>>> waves = w.Length > from ? Spec(string.Join(" ", w, from, w.Length - from)) : null;
            if (waves == null) { Story.Log("waves «" + name + "»: not read: " + text); StoryWorld.SetText(Def + name, ""); return; }
            bool near = false, gone = true;
            _players.Clear();
            foreach (ZDO c in ZNet.instance.GetAllCharacterZDOS()) // (the players' characters, every game's)
            {
                if (c == null) continue;
                float d = Vector3.Distance(c.GetPosition(), at);
                if (d < r + Near) near = true;
                if (d < r + Gone) { gone = false; _players.Add(c.GetPosition()); } // (those a new wave must not be made beside: Spawn)
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
                StoryWorld.SetVar(Since + name, 0); // (its five minutes begin anew with its first wave)
                Story.Log("waves «" + name + "»: nobody near — " + _mine.Count + " foe(s) taken out, the fight waits for one to come back");
                return;
            }
            int now = (int)ZNet.instance.GetTimeSeconds(), since = StoryWorld.Var(Since + name);
            if (_mine.Count > 0 && since > 0 && now - (since - 1) >= WaveMost) // (a wave stood its most with a player there: beaten — one stuck in a rock or behind a cliff holds nothing)
            {
                foreach (ZDO z in _mine) Take(z);
                Story.Log("waves «" + name + "»: wave " + wave + " stood " + Mathf.RoundToInt(WaveMost) + " s — counted beaten, " + _mine.Count + " foe(s) left taken out (stuck where nobody reached them?)");
                _mine.Clear();
            }
            if (!near || _mine.Count > 0) return;
            if (wave >= waves.Count)
            {
                StoryWorld.SetFlag(name, true);
                StoryWorld.SetText(Def + name, "");
                StoryWorld.SetVar(At + name, 0);
                StoryWorld.SetVar(Go + name, 0);
                StoryWorld.SetVar(Since + name, 0);
                Story.Log("waves «" + name + "»: all " + waves.Count + " beaten");
                return;
            }
            Spawn(name, at, r, waves[wave], wave + 1, o, wave == 0);
            StoryWorld.SetVar(At + name, wave + 1);
            StoryWorld.SetVar(Since + name, now + 1); // (when this wave came: WaveMost; 0 is «none»)
        }

        private static readonly List<Vector3> _players = new List<Vector3>(); // (the players about the fight this pass: Run fills it)
        private static bool BesidePlayer(Vector3 p)
        {
            foreach (Vector3 q in _players) { float dx = q.x - p.x, dz = q.z - p.z; if (dx * dx + dz * dz < Clear * Clear) return true; }
            return false;
        }

        private static void Spawn(string name, Vector3 at, float r, List<KeyValuePair<string, int>> wave, int no, Opts o, bool first)
        {
            float rise = first ? o.Rise : 0f; // (only the first wave rises: the scene films it)
            long fights = rise > 0f ? (long)((ZNet.instance.GetTimeSeconds() + rise) * 1000.0) : 0L;
            int all = 0, i = 0, made = 0, pushed = 0;
            foreach (KeyValuePair<string, int> f in wave) if (PrefabOf(f.Key) != null) all += f.Value; // (one the game lacks leaves no gap in the round)
            float start = Random.value * 360f, span = 360f; // (all round; «away»: an arc on the far side)
            Vector3 from;
            if (o.Away.Length > 0)
            {
                Vector3 off = at - (StoryWhere.Find(o.Away, out from) ? from : at); off.y = 0f;
                if (off.sqrMagnitude > 1f) { span = Arc; start = Mathf.Atan2(off.z, off.x) * Mathf.Rad2Deg - Arc * 0.5f + Arc * 0.5f / Mathf.Max(1, all); } // (the arc's middle right away from that place; its foes evenly over it)
                else Story.Log("waves «" + name + "»: away «" + o.Away + "» not found or at the place itself — all round");
            }
            foreach (KeyValuePair<string, int> f in wave)
            {
                GameObject pf = PrefabOf(f.Key);
                if (pf == null) { Story.Log("waves «" + name + "»: no creature «" + f.Key + "» in the game"); continue; }
                int hash = pf.name.GetStableHashCode();
                for (int k = 0; k < f.Value; k++, i++)
                {
                    float a = (start + i * span / Mathf.Max(1, all)) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float rr = r;
                    while (rr < r + ClearMost && BesidePlayer(at + dir * rr)) rr += 4f; // (a player stands by the round: its spot farther out on its own line — the player came from the forest's side)
                    if (rr > r) pushed++;
                    Vector3 p = at + dir * rr;
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
                    if (fights > 0L && o.Walk) z.Set(KWalk, true);
                    if (o.Hel) z.Set(StoryBeasts.KHel, true);
                    made++;
                }
            }
            Story.Log("waves «" + name + "»: wave " + no + ", " + made + " foe(s)" + (rise > 0f ? ", rising " + rise.ToString("0.#", CultureInfo.InvariantCulture) + " s" + (o.Walk ? " walking" : "") : "") + (span < 360f ? ", from the side away from " + o.Away : "") + (o.Hel ? ", Hel's eyes" : "") + " round " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z) + " at " + r.ToString("0.#", CultureInfo.InvariantCulture) + " m" + (pushed > 0 ? ", " + pushed + " of them farther out (a player within " + Mathf.RoundToInt(Clear) + " m of its spot)" : ""));
        }

        // every frame (StoryQuests.Frame), on every game: a rising foe drawn here is of the players' side till its time, then its own again.
        // Looked at only while the first wave of waves with «rise» stands in the world (only it rises: its words and its wave's number, read once a
        // second) or one is held here — not through the later waves, nor while a fight waits for a player to come back
        internal static float RiseEvery = 0.25f;
        private static float _riseNext, _wordsNext;
        private static bool _anyRise;
        private static readonly Dictionary<Character, Character.Faction> _rising = new Dictionary<Character, Character.Faction>(); // session: held here, and its own side
        private static readonly List<Character> _gone = new List<Character>();
        private static readonly HashSet<string> _released = new HashSet<string>(); // (the waves whose rise «wavesgo» said over: read once a second)
        internal static void Rising(Player p)
        {
            if (ZNet.instance == null || Time.time < _riseNext) return;
            _riseNext = Time.time + RiseEvery;
            if (Time.time >= _wordsNext)
            {
                _wordsNext = Time.time + 1f;
                _anyRise = false;
                _released.Clear();
                foreach (KeyValuePair<string, string> kv in StoryWorld.Texts(Def)) // heavy: a pass over the world's keys, once a second
                {
                    string name = kv.Key.Substring(Def.Length);
                    if (StoryWorld.Var(Go + name) == 1) { _released.Add(name); continue; } // (its rise said over: «wavesgo»)
                    if (kv.Value.Contains(" rise ") && StoryWorld.Var(At + name) == 1) _anyRise = true;
                }
            }
            if (!_anyRise && _rising.Count == 0) return;
            long now = (long)(ZNet.instance.GetTimeSeconds() * 1000.0);
            foreach (Character c in Character.GetAllCharacters()) // heavy: the drawn creatures, 4 times a second — only while a wave rises
            {
                if (c == null || c.IsPlayer()) continue;
                ZNetView nv = c.GetComponent<ZNetView>();
                ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
                if (z == null) continue;
                long until = z.GetLong(KRise, 0L);
                bool held = _rising.ContainsKey(c), rises = until > now && (_released.Count == 0 || !_released.Contains(z.GetString(KWave, "")));
                if (rises) { if (!held) { _rising[c] = c.m_faction; c.m_faction = Character.Faction.Players; } }
                else if (held) { c.m_faction = _rising[c]; _rising.Remove(c); }
                if (rises && z.GetBool(KWalk) && nv.IsOwner()) Walk(c);
                else if (_walking.ContainsKey(c)) Stop(c);
            }
            _gone.Clear();
            foreach (Character c in _rising.Keys) if (c == null) _gone.Add(c);
            foreach (Character c in _gone) _rising.Remove(c);
            _gone.Clear();
            foreach (Character c in _walking.Keys) if (c == null) _gone.Add(c);
            foreach (Character c in _gone) _walking.Remove(c);
        }

        // «rise … walk»: the game that runs it walks it at the nearest player while it rises, its own mind off (it would stand or wander: of the
        // players' side it has no foe); WalkFrom s after this game first drew it (its own Start first — a skeleton's sword: StoryBeasts), WalkNear m off him.
        // A straight line, as a scene's beast (StoryBeasts.Walk): moved less than StuckMoved m in StuckFor s (a wall, a rock, water) — it stands there
        internal static float StuckFor = 3f, StuckMoved = 0.4f;
        private sealed class Walker { public float Since, CheckAt; public Vector3 From; public bool Stuck; }
        private static readonly Dictionary<Character, Walker> _walking = new Dictionary<Character, Walker>(); // session: driven here
        private static void Walk(Character c)
        {
            Walker w;
            if (!_walking.TryGetValue(c, out w)) _walking[c] = w = new Walker { Since = Time.time, CheckAt = Time.time + WalkFrom + StuckFor, From = c.transform.position };
            if (Time.time < w.Since + WalkFrom) return;
            BaseAI ai = c.GetBaseAI();
            if (ai != null && ai.enabled) ai.enabled = false;
            Player to = w.Stuck ? null : Player.GetClosestPlayer(c.transform.position, Gone);
            Vector3 d = to != null ? to.transform.position - c.transform.position : Vector3.zero;
            d.y = 0f;
            if (d.magnitude <= WalkNear) { c.SetMoveDir(Vector3.zero); if (d.sqrMagnitude > 0.01f) c.SetLookDir(d.normalized); w.CheckAt = Time.time + StuckFor; w.From = c.transform.position; return; } // (standing by him is no being stuck)
            d.Normalize();
            c.SetRun(false);
            c.SetMoveDir(d);
            c.SetLookDir(d);
            if (Time.time < w.CheckAt) return;
            if (Utils.DistanceXZ(c.transform.position, w.From) < StuckMoved) { w.Stuck = true; c.SetMoveDir(Vector3.zero); Story.Log("waves: a walking foe stuck " + Mathf.RoundToInt(Utils.DistanceXZ(c.transform.position, to.transform.position)) + " m off the player — it stands till it fights"); return; } // flat: a creature walks the land
            w.CheckAt = Time.time + StuckFor; w.From = c.transform.position;
        }
        private static void Stop(Character c)
        {
            _walking.Remove(c);
            c.SetMoveDir(Vector3.zero);
            BaseAI ai = c.GetBaseAI();
            if (ai != null && !ai.enabled) ai.enabled = true; // (its own mind again: it fights — or, run by another game now, that one runs it)
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
