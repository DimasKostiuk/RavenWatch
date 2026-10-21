#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The arena of a raven tree (binds-plan.md §10а): «Сплести» draws a ring of runes round the tree, and waves of
    // ordinary creatures of your stage come (green tree 2 waves, icy 3, charred 4). You may step out of the ring, but
    // only for 10 s; dying or staying out longer — the в'язь does not hold, the runes stay in the book. Won — it is
    // woven. Other players may help: the creatures are counted, not the hands that killed them; more players near the
    // ring — more creatures. The creatures drop nothing (no farming by weaving again and again).
    // The game of the weaver runs it all (its creatures are its own); the ring is on the weaver's player (RingKey), so
    // every game near draws it.
    internal static class BindArena
    {
        internal static float Radius = 25f, OutsideMax = 10f, WaveGap = 4f;
        internal const string Key = "RavenWatch_arena"; // on the creatures (their ZDO): an arena's — they drop nothing, in any game, ever
        internal const string RingKey = "RavenWatch_arenaRing"; // on the weaver's player: "x;y;z" of the tree, "" none

        private static bool _on;
        private static Player _weaver; // the arena belongs to this player object (a new session or character: it is gone)
        private static Vector3 _at;
        private static int _wave, _waves;
        private static TreeView _view;
        private static List<string> _runes = new List<string>();
        private static Bind _b;
        private static readonly List<Character> _alive = new List<Character>();
        private static float _outside, _nextWave, _said;
        private static readonly Dictionary<ZDOID, GameObject> _rings = new Dictionary<ZDOID, GameObject>();
        private static readonly Dictionary<ZDOID, string> _ringAt = new Dictionary<ZDOID, string>();
        private static float _ringsLook;

        internal static bool Running { get { return _on; } }
        internal static int Wave { get { return _wave; } }
        internal static int Waves { get { return _waves; } }

        // null when it began; else why not
        internal static string Begin(Player p, TreeView v, List<string> runes)
        {
            if (p == null || v == null) return Lang.T("Підійди до Воронячого дерева.");
            if (_on) return Lang.T("Арена вже йде.");
            RuneBook.Data d = RuneBook.Of(p);
            if (d.Bind.Length > 0) return Lang.T("Спершу розплети свою в'язь.");
            int tree = RavenTree.Kind(v.transform.position);
            string trust = TrustProblem(tree);
            if (trust != null) return trust;
            foreach (string r in runes) if (!d.Has(r)) return Lang.T("Змішувати можна лише руни, що горять.");
            Bind b = Bind.From(d, runes, tree);
            string no = b.Problem();
            if (no != null) return no;
            _on = true; _weaver = p; _view = v; _at = v.transform.position; _b = b; _runes = new List<string>(runes);
            _wave = 0; _waves = Bind.Slots(tree); _alive.Clear(); _outside = 0f; _nextWave = Time.time + 3f;
            Ring(p, true);
            RavenTree.Log("arena for " + b.Id() + " (" + _waves + " waves)");
            RavenMsg.Center(p, Lang.F("Арена! Встоїш {0} хвилі — в'язь сплететься. Не виходь із кола довше ніж на {1} с.", _waves, Mathf.RoundToInt(OutsideMax)));
            RavenFx.Play("sfx_raven_kaw", p.transform.position, Quaternion.identity);
            return null;
        }

        // the trust a tree wants of one raven of yours at least
        internal static string TrustProblem(int tree)
        {
            int need = Bind.TrustNeed(tree), best = 0;
            foreach (bool m in new bool[] { false, true }) { ItemDrop.ItemData it = Trust.Worn(m); if (it != null) best = Mathf.Max(best, Trust.Level(it)); }
            return best >= need ? null : Lang.F("Це дерево слухає лише ворона з довірою {0} і вище (у твоїх — {1}).", need, best);
        }

        internal static void Tick(Player p, float dt)
        {
            if (!_on) return;
            if (!ReferenceEquals(p, _weaver)) { End(p, false, Lang.T("Арену перервано. В'язь не сплелась — руни лишились у книзі.")); return; } // (a respawn after a death there, another session)
            if (p == null || p.IsDead()) { End(p, false, Lang.T("Ти впав на арені. В'язь не сплелась — руни лишились у книзі.")); return; }
            Vector3 d = p.transform.position - _at; d.y = 0f;
            if (d.magnitude > Radius)
            {
                _outside += dt;
                if (Time.time >= _said) { _said = Time.time + 1f; RavenMsg.Center(p, Lang.F("Поза колом! Повернись за {0} с.", Mathf.CeilToInt(OutsideMax - _outside))); }
                if (_outside > OutsideMax) { End(p, false, Lang.T("Ти покинув коло. В'язь не сплелась — руни лишились у книзі.")); return; }
            }
            else _outside = 0f;
            for (int i = _alive.Count - 1; i >= 0; i--) if (_alive[i] == null || _alive[i].IsDead()) _alive.RemoveAt(i);
            Hunt(p);
            if (_alive.Count > 0) { _nextWave = Time.time + WaveGap; return; }
            if (Time.time < _nextWave) return;
            if (_wave >= _waves) { End(p, true, null); return; }
            _wave++;
            Spawn(p);
            RavenMsg.Center(p, Lang.F("Хвиля {0} з {1}", _wave, _waves));
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
                if (ai == null || !c.IsOwner()) continue;
                ai.SetHuntPlayer(true);
                if (TargetOf.GetValue(ai) == null) { TargetOf.SetValue(ai, p); ai.Alert(); }
            }
        }

        private static readonly System.Reflection.FieldInfo TargetOf = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");

        // a wave: ordinary creatures of your stage's land, more with each wave and with each player near the ring
        private static void Spawn(Player p)
        {
            int stage = Stage.Of(p);
            List<string> can = Champions.Plain(stage);
            if (can.Count == 0) return;
            int players = 0;
            foreach (Player o in Player.GetAllPlayers()) if (o != null && !o.IsDead() && Vector3.Distance(o.transform.position, _at) <= Radius + 15f) players++;
            int n = 2 + _wave + 2 * Mathf.Max(0, players - 1);
            for (int i = 0; i < n; i++)
            {
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(can[Random.Range(0, can.Count)]) : null;
                if (prefab == null) continue;
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(Radius * 0.55f, Radius * 0.85f);
                Vector3 pos = _at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (ZoneSystem.instance != null) pos.y = ZoneSystem.instance.GetGroundHeight(pos) + 0.5f;
                GameObject go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                Character c = go.GetComponent<Character>();
                ZNetView nv = go.GetComponent<ZNetView>();
                if (c == null || nv == null || !nv.IsValid()) { Object.Destroy(go); continue; }
                nv.GetZDO().Set(Key, p.GetPlayerID()); // (no loot: ArenaNoDropPatch, wherever it dies)
                if (_wave >= 3 && Random.value < 0.3f) c.SetLevel(2);
                _alive.Add(c);
            }
        }

        private static void End(Player p, bool won, string why)
        {
            _on = false;
            Ring(p, false);
            foreach (Character c in _alive)
            {
                if (c == null) continue;
                ZNetView nv = c.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid()) { if (!nv.IsOwner()) nv.ClaimOwnership(); ZNetScene.instance.Destroy(c.gameObject); }
            }
            _alive.Clear();
            _weaver = null; _view = null;
            if (p == null) return;
            if (!won) { RavenMsg.Center(p, why); RavenTree.Log("arena lost"); return; }
            RuneBook.SetBind(p, _b, _runes);
            bool worn = false;
            foreach (bool m in new bool[] { false, true }) if (!worn && Trust.Worn(m) != null && RuneBook.PutBind(p, m) == null) worn = true;
            BindPower w = BindPower.Of(_b);
            RavenMsg.Center(p, worn ? Lang.T("В'язь сплелась!") : Lang.T("В'язь сплелась! Вдягни її на ворона в книзі («Шлях Одіна» → В'язь), коли він сяде на плече."));
            RavenFx.Play("vfx_offering", p.transform.position + Vector3.up, Quaternion.identity);
            RavenFx.Play("sfx_offering", p.transform.position, Quaternion.identity);
            if (_view != null) _view.Burst();
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

        private static GameObject Build(Vector3 at)
        {
            GameObject tpl = FxLib.Template(FxLib.OursPrefix + "runes");
            if (tpl == null) return null;
            GameObject ring = new GameObject("RavenWatch_ArenaRing");
            const int n = 36;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 pos = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Radius;
                if (ZoneSystem.instance != null) pos.y = ZoneSystem.instance.GetGroundHeight(pos) + 0.3f;
                GameObject g = Object.Instantiate(tpl, pos, Quaternion.identity, ring.transform);
                g.hideFlags = HideFlags.None;
                g.transform.localScale = Vector3.one * 4f;
                foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule m = ps.main;
                    m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.35f), new Color(1f, 0.95f, 0.7f));
                    ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = 3f;
                }
                g.SetActive(true);
            }
            return ring;
        }
    }

    // an arena's creature drops nothing — in the game that holds it when it dies (the weaver's or a helper's), after a reload too
    [HarmonyPatch(typeof(CharacterDrop), "OnDeath")]
    internal static class ArenaNoDropPatch
    {
        private static bool Prefix(CharacterDrop __instance)
        {
            try
            {
                ZNetView nv = __instance.GetComponent<ZNetView>();
                return nv == null || !nv.IsValid() || nv.GetZDO().GetLong(BindArena.Key, 0L) == 0L;
            }
            catch (System.Exception e) { PatchGuard.Fail("ArenaNoDropPatch", e); return true; }
        }
    }
}
#endif
