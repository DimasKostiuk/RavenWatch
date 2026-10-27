#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The arena of a raven tree (binds-plan.md §10а): «Сплести» draws a ring of runes round the tree, and waves of
    // ordinary creatures of the tree's lands come (green tree 2 waves, icy 3, charred 4; see TierFor). You may step out of the ring, but
    // only for 10 s; dying or staying out longer — the в'язь does not hold, the runes stay in the book. Won — it is
    // woven. Other players may help: the creatures are counted, not the hands that killed them; more players near the
    // ring — more creatures. The creatures drop nothing (no farming by weaving again and again).
    // The game of the weaver runs it all (its creatures are its own); the ring is on the weaver's player (RingKey), so
    // every game near draws it.
    internal static class BindArena
    {
        internal static float Radius = 25f, OutsideMax = 10f, WaveGap = 4f, TimeLimit = 600f, StrayMax = 15f; // (TimeLimit: s for the whole arena; StrayMax: s a creature may stay far off the ring before it is swapped for a new one)
        internal const string Key = "RavenWatch_arena"; // on the creatures (their ZDO): an arena's — they drop nothing, in any game, ever
        internal const string RingKey = "RavenWatch_arenaRing"; // on the weaver's player: "x;y;z" of the tree, "" none

        private static bool _on;
        private static Player _weaver; // the arena belongs to this player object (destroyed: he died there)
        private static ZNet _net;      // ...in this session (another world or a relog: it ends without a word)
        private static Vector3 _at;
        private static int _wave, _waves;
        private static TreeView _view;
        private static List<string> _runes = new List<string>();
        private static Bind _b;
        private static readonly List<Character> _alive = new List<Character>();
        private static readonly Dictionary<Character, float> _straySince = new Dictionary<Character, float>(); // (far off the ring since)
        private static float _outside, _nextWave, _said, _endsAt;
        private static bool _warned; // the last minute told
        private static readonly Dictionary<ZDOID, GameObject> _rings = new Dictionary<ZDOID, GameObject>();
        private static readonly Dictionary<ZDOID, string> _ringAt = new Dictionary<ZDOID, string>();
        private static float _ringsLook;

        internal static bool Running { get { return _on; } }

        // one of an arena's creatures (its ZDO says so, in any game)
        internal static bool Of(Character c)
        {
            ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
            return nv != null && nv.IsValid() && nv.GetZDO().GetLong(Key, 0L) != 0L;
        }
        internal static int Wave { get { return _wave; } }
        internal static int Waves { get { return _waves; } }

        // null when it began; else why not
        internal static string Begin(Player p, TreeView v, List<string> runes)
        {
            if (p == null || v == null || Vector3.Distance(p.transform.position, v.transform.position) > Radius) return Lang.T("Підійди до Воронячого дерева.");
            if (SeatZone.Mine(p, v.Perch).Count == 0) return Lang.T("Коріння мовчить, поки на дереві нема твоїх воронів. Посади їх на жердину на верхівці.");
            if (_on) return Lang.T("Арена вже йде.");
            RuneBook.Data d = RuneBook.Of(p);
            int tree = RavenTree.Kind(v.transform.position);
            string trust = TrustProblem(tree);
            if (trust != null) return trust;
            string no = Still(d, runes, tree);
            if (no != null) return no;
            Bind b = Bind.From(d, runes, tree);
            _on = true; _weaver = p; _net = ZNet.instance; _view = v; _at = v.transform.position; _b = b; _runes = new List<string>(runes);
            _wave = 0; _waves = Bind.Slots(tree); _alive.Clear(); _outside = 0f; _nextWave = Time.time + 3f; _endsAt = Time.time + TimeLimit; _warned = false;
            Ring(p, true);
            Perches.AllHome(p); // the ravens drop what they do and come to your shoulders (the user: at once, when it begins)
            RavenTree.Log("arena for " + b.Id() + " (" + _waves + " waves)");
            RavenMsg.Center(p, Lang.F("Арена! Встоїш {0} хвилі за {2} хв — в'язь сплететься. Не виходь із кола довше ніж на {1} с.", _waves, Mathf.RoundToInt(OutsideMax), Mathf.RoundToInt(TimeLimit / 60f)));
            RavenFx.Play("sfx_raven_kaw", p.transform.position, Quaternion.identity);
            return null;
        }

        // what the в'язь needs of the book, at the start and again at the win: no в'язь yet, the runes yours, the mix right
        private static string Still(RuneBook.Data d, List<string> runes, int tree)
        {
            if (d.Bind.Length > 0) return Lang.T("Спершу розплети свою в'язь.");
            foreach (string r in runes) if (!d.Has(r)) return Lang.T("Сплітати можна лише руни, що горять.");
            return Bind.From(d, runes, tree).Problem();
        }

        // the trust a tree wants of one raven of yours at least
        internal static string TrustProblem(int tree)
        {
            int need = Bind.TrustNeed(tree), best = 0;
            Player p = Player.m_localPlayer;
            foreach (bool m in new bool[] { false, true }) { ItemDrop.ItemData it = Trust.Worn(m); if (it != null && Trust.Active(it) && !Trust.Stranger(it, p)) best = Mathf.Max(best, Trust.Level(it)); } // (a friend's raven does not open a tree)
            return best >= need ? null : Lang.F("Це дерево слухає лише ворона з довірою {0} і вище (у твоїх — {1}).", need, best);
        }

        internal static void Tick(Player p, float dt)
        {
            if (!_on) return;
            if (!ReferenceEquals(ZNet.instance, _net)) { End(null, false, null); return; } // (another world or session: its creatures went with it)
            bool fell = p == null || p.IsDead() || _weaver == null; // (the game destroys the dead player: a respawn is a new one)
            if (fell || !ReferenceEquals(p, _weaver)) { End(p, false, fell ? Lang.T("Ти впа{в|ла} на арені. В'язь не сплелась — руни лишились у книзі.") : Lang.T("Арену перервано. В'язь не сплелась — руни лишились у книзі.")); return; }
            for (int i = _alive.Count - 1; i >= 0; i--) if (_alive[i] == null || _alive[i].IsDead()) { if (!ReferenceEquals(_alive[i], null)) _straySince.Remove(_alive[i]); _alive.RemoveAt(i); }
            if (_wave >= _waves && _alive.Count == 0) { End(p, true, null); return; } // (the last wave down: won at once, never lost to the clock in the gap)
            if (Time.time >= _endsAt) { End(p, false, Lang.T("Час арени вийшов. В'язь не сплелась — руни лишились у книзі.")); return; }
            if (_endsAt - Time.time <= 60f && !_warned) { _warned = true; RavenMsg.Center(p, Lang.T("Арені лишилась хвилина!")); }
            Vector3 d = p.transform.position - _at; d.y = 0f;
            if (d.magnitude > Radius)
            {
                _outside += dt;
                if (Time.time >= _said) { _said = Time.time + 1f; RavenMsg.Center(p, Lang.F("Поза колом! Повернись за {0} с.", Mathf.CeilToInt(OutsideMax - _outside))); }
                if (_outside > OutsideMax) { End(p, false, Lang.T("Ти покину{в|ла} коло. В'язь не сплелась — руни лишились у книзі.")); return; }
            }
            else _outside = 0f;
            Hunt(p);
            Strays(p);
            if (_alive.Count > 0) { _nextWave = Time.time + WaveGap; return; }
            if (Time.time < _nextWave) return;
            _wave++;
            Spawn(p);
            RavenMsg.Center(p, Lang.F("Хвиля {0} з {1} · лишилось {2}", _wave, _waves, RavenAbilities.Clock(_endsAt - Time.time)));
        }

        // the creatures keep to the weaver (and to the ring)
        private static float _huntAt;
        private static void Hunt(Player p)
        {
            if (Time.time < _huntAt) return;
            _huntAt = Time.time + 1f;
            foreach (Character c in _alive)
            {
                MonsterAI ai = c.GetComponent<MonsterAI>();
                if (ai == null || !c.IsOwner() || Shadow.HiddenFrom(p, c.transform)) continue; // (you in the shadow or murk: they lose you, as anywhere)
                ai.SetHuntPlayer(true);
                if (AiTarget.Of(ai) == null) { AiTarget.Set(ai, p); ai.Alert(); }
            }
        }

        // a creature stuck far off the ring (fell off a cliff, wandered round a rock) is swapped for a new one by the ring
        private static void Strays(Player p)
        {
            float far = (Radius + 20f) * (Radius + 20f);
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                Character c = _alive[i];
                Vector3 d = c.transform.position - _at; d.y = 0f;
                if (d.sqrMagnitude <= far) { _straySince.Remove(c); continue; }
                float since;
                if (!_straySince.TryGetValue(c, out since)) { _straySince[c] = Time.time; continue; }
                if (Time.time - since < StrayMax) continue;
                _straySince.Remove(c); _alive.RemoveAt(i);
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Utils.GetPrefabName(c.gameObject)) : null;
                int level = c.GetLevel();
                Gone(c);
                if (prefab != null) SpawnOne(p, prefab, level);
            }
        }

        private static void Gone(Character c)
        {
            ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
            if (nv != null && nv.IsValid()) { if (!nv.IsOwner()) nv.ClaimOwnership(); ZNetScene.instance.Destroy(c.gameObject); }
        }

        // a spot in the ring on dry land, never water or lava; false when none found
        private static bool Spot(out Vector3 pos)
        {
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            for (int tries = 0; tries < 12; tries++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(Radius * 0.55f, Radius * 0.85f);
                pos = _at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (ZoneSystem.instance == null) return true;
                Vector3 normal; Heightmap.Biome biome; Heightmap.BiomeArea area; Heightmap hmap;
                ZoneSystem.instance.GetGroundData(ref pos, out normal, out biome, out area, out hmap);
                if (pos.y < sea + 0.5f) continue;
                if (hmap != null && WorldGenerator.IsAshlands(pos.x, pos.z) && hmap.GetLava(pos) > 0.05f) continue;
                pos.y += 0.5f;
                return true;
            }
            pos = Vector3.zero;
            return false;
        }


        // the lands a tree's arena takes its creatures from (Champions' tiers: Meadows 0 … Ashlands 6), by RavenTree.Kind
        // (0 icy, 1 green, 2 charred): its own [Lo, Hi], and now and then (NewerChance) a newer land up to Cap — only one
        // whose way is open (its boss before it killed): green the weakest, icy the Mountains and up, charred the Ashlands.
        private static readonly int[] TierLo = { 3, 0, 6 }, TierHi = { 3, 1, 6 }, TierCap = { 5, 2, 6 };
        internal static float NewerChance = 0.25f;

        private static int TierFor(int tree, int open)
        {
            tree = Mathf.Clamp(tree, 0, 2);
            int hi = Mathf.Max(TierLo[tree], Mathf.Min(TierHi[tree], open)); // (the green one's Black Forest only once Eikthyr is down)
            int top = Mathf.Min(TierCap[tree], open);
            if (top > hi && Random.value < NewerChance) return Random.Range(hi + 1, top + 1);
            return Random.Range(TierLo[tree], hi + 1);
        }

        // a wave: ordinary creatures of the tree's lands, more with each wave and with each player near the ring; the
        // further you are in the game past the tree's lands, the more of them come with a star
        private static void Spawn(Player p)
        {
            int open = Mathf.Min(Stage.Of(p), 6), tree = RavenTree.Kind(_at);
            int over = Mathf.Max(0, open - TierHi[Mathf.Clamp(tree, 0, 2)]);
            float star = Mathf.Min(0.6f, 0.12f * over + (_wave >= 3 ? 0.3f : 0f));
            int players = 0;
            foreach (Player o in Player.GetAllPlayers()) if (o != null && !o.IsDead() && Vector3.Distance(o.transform.position, _at) <= Radius + 15f) players++;
            int n = 2 + _wave + 2 * Mathf.Max(0, players - 1);
            Dictionary<int, List<string>> byTier = new Dictionary<int, List<string>>();
            for (int i = 0; i < n; i++)
            {
                int tier = TierFor(tree, open);
                List<string> can;
                if (!byTier.TryGetValue(tier, out can)) byTier[tier] = can = Champions.Plain(tier);
                if (can.Count == 0) continue;
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(can[Random.Range(0, can.Count)]) : null;
                if (prefab == null) continue;
                SpawnOne(p, prefab, Random.value < star ? 2 : 1);
            }
        }

        private static void SpawnOne(Player p, GameObject prefab, int level)
        {
            Vector3 pos;
            if (!Spot(out pos)) return; // (no dry spot in the ring: one creature fewer)
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            Character c = go.GetComponent<Character>();
            ZNetView nv = go.GetComponent<ZNetView>();
            if (c == null || nv == null || !nv.IsValid()) { Object.Destroy(go); return; }
            nv.GetZDO().Set(Key, p.GetPlayerID()); // (no loot: ChampionDropPatch empties its list, wherever it dies; no kill credit: CreatureDeathPatch)
            nv.GetZDO().Persistent = false; // never saved: a weaver who quits leaves none behind
            if (level > 1) c.SetLevel(level);
            _alive.Add(c);
        }

        private static void End(Player p, bool won, string why)
        {
            _on = false;
            Ring(p, false);
            foreach (Character c in _alive) Gone(c);
            _alive.Clear(); _straySince.Clear();
            TreeView view = _view;
            _weaver = null; _view = null; _net = null;
            if (p == null) return;
            if (won) { why = Still(RuneBook.Of(p), _runes, RavenTree.Kind(_at)); won = why == null; } // (the book may have changed while it went)
            if (!won) { RavenMsg.Center(p, why); RavenTree.Log("arena lost"); return; }
            RuneBook.SetBind(p, _b, _runes);
            BindPower w = BindPower.Of(_b);
            RavenMsg.Center(p, Lang.T("В'язь сплелась! Вона вже діє на тобі."));
            RavenFx.Play("vfx_offering", p.transform.position + Vector3.up, Quaternion.identity);
            RavenFx.Play("sfx_offering", p.transform.position, Quaternion.identity);
            if (view != null) view.Burst();
            RavenBook.ShowBindResult(p); // (what was woven, big on the screen)
            RavenTree.Log("arena won: " + _b.Id() + " · " + w.Mode);
        }

        // ---------------------------------------------------------------- the ring of runes (every game near draws it)
        private static void Ring(Player p, bool on)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null) return;
            string v = on ? _at.x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ";" + _at.y.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ";" + _at.z.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "";
            if (zdo.GetString(RingKey, "") != v) zdo.Set(RingKey, v);
        }

        // twice a second: the rings of the players near (yours too), made and taken away as they come and go
        internal static void RingsTick()
        {
            if (Time.time < _ringsLook) return;
            _ringsLook = Time.time + 0.5f;
            HashSet<ZDOID> seen = _seen; seen.Clear();
            foreach (Player o in Player.GetAllPlayers())
            {
                ZDO zdo = NetFlag.Zdo(o, false);
                string v = zdo != null ? zdo.GetString(RingKey, "") : "";
                if (v.Length == 0) continue;
                ZDOID id = o.GetZDOID();
                seen.Add(id);
                string had;
                if (_ringAt.TryGetValue(id, out had) && had == v && _rings.ContainsKey(id) && _rings[id] != null) continue;
                Vector3 at;
                if (!Parse(v, out at)) continue;
                GameObject old;
                if (_rings.TryGetValue(id, out old) && old != null) Object.Destroy(old);
                _rings[id] = Build(at);
                _ringAt[id] = v;
            }
            List<ZDOID> gone = _gone; gone.Clear();
            foreach (KeyValuePair<ZDOID, GameObject> kv in _rings) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (ZDOID id in gone) { if (_rings[id] != null) Object.Destroy(_rings[id]); _rings.Remove(id); _ringAt.Remove(id); }
        }

        private static readonly HashSet<ZDOID> _seen = new HashSet<ZDOID>();
        private static readonly List<ZDOID> _gone = new List<ZDOID>();

        private static bool Parse(string v, out Vector3 at)
        {
            at = Vector3.zero;
            string[] a = v.Split(';');
            float x, y, z;
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            if (a.Length != 3 || !float.TryParse(a[0], System.Globalization.NumberStyles.Float, ci, out x) || !float.TryParse(a[1], System.Globalization.NumberStyles.Float, ci, out y) || !float.TryParse(a[2], System.Globalization.NumberStyles.Float, ci, out z)) return false;
            at = new Vector3(x, y, z);
            return true;
        }

        private static GameObject Build(Vector3 at) { return RuneRing.Build("RavenWatch_ArenaRing", at, Radius); }
    }
}
#endif
