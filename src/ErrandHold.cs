#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // hold <slot> <seconds> [radius]: a watch kept at night in the rune ring round that place, a rune stake in its middle.
    // It begins with the step's deed (E on the stake, the J circle, the book) — only at night, inside the ring. While it
    // goes, the beasts of that land come (HoldWaves); out of the ring the count waits; died — the watch begins anew.
    // (mark hold<i>: begun)
    internal sealed class HoldStep : ErrandStep
    {
        private const int Unit = 10;
        private const float Seen = 80f; // (the ring and the stake: their ground read once, from a zone already loaded)
        private string _slot = "";
        private int _n = 180;
        private float _radius = 10f, _sat, _last;
        private bool _outTold;

        protected override void Read()
        {
            _slot = Arg(1, "");
            _n = Mathf.Max(Unit, ArgI(2, 180) / Unit * Unit);
            _radius = Mathf.Max(6f, Errands.F(Arg(3, "10"), 10f));
        }
        internal override int Need { get { return _n; } }
        internal override float Wait { get { return _n + Power.GameDaySeconds() * 0.5f; } } // (the night it waits for)

        private static string Key(int i) { return "hold" + i; }
        private static bool Begun(ErrandRun r, int i) { return r.Mark(Key(i)).Length > 0; }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            radius = _radius; at = Vector3.zero;
            string v;
            return r.Slot.TryGetValue(_slot, out v) && ErrandSteps.Point(v, out at);
        }

        // m beyond the ring (0 or less: in it)
        private float Off(ErrandRun r, int i, Player p)
        {
            Vector3 at; float rad;
            if (p == null || !Where(r, i, out at, out rad)) return float.MaxValue;
            Vector3 d = p.transform.position - at; d.y = 0f;
            return d.magnitude - rad;
        }

        internal override string ActLabel(ErrandRun r, int i, Player p, out string why)
        {
            why = null;
            if (r.Count[i] >= _n || Begun(r, i) || p == null) return "";
            if (Off(r, i, p) > 0f) why = Lang.T("у колі варти");
            else if (!EnvMan.IsNight()) why = Lang.T("уночі");
            return Lang.T("Почати варту");
        }

        internal override string Act(ErrandRun r, int i, Player p)
        {
            r.SetMark(Key(i), "1");
            _sat = 0f; _outTold = false;
            RavenMsg.Center(p, Lang.T("Варта почалася. Тримай коло до кінця — і не помри."));
            return null;
        }

        internal override bool Tick(ErrandRun r, int i, Player p)
        {
            float now = Time.time, dt = _last > 0f ? Mathf.Min(2f, now - _last) : 0f;
            _last = now;
            Vector3 at; float rad;
            if (p == null || r.Count[i] >= _n || !Where(r, i, out at, out rad)) return false;
            float off = Off(r, i, p);
            if (off < Seen) { RuneRing.Want(at, rad); RuneRing.WantStake(at); }
            if (!Begun(r, i)) return false;
            bool inside = off <= 0f;
            HoldWaves.Run(p, at, rad, inside);
            if (!inside)
            {
                if (!_outTold) { _outTold = true; RavenMsg.Center(p, Lang.T("Варта на паузі: повернись у коло.")); }
                return false;
            }
            _outTold = false;
            _sat += dt;
            if (_sat < Unit) return false;
            _sat -= Unit;
            r.Count[i] = Mathf.Min(_n, r.Count[i] + Unit);
            if (r.Count[i] >= _n) HoldWaves.End();
            return true;
        }

        // died on the watch: it begins anew (the next night will do)
        internal override bool OnEvent(ErrandRun r, int i, Ev type, string arg, int value)
        {
            if (type != Ev.Death || !Begun(r, i) || r.Count[i] >= _n) return false;
            r.SetMark(Key(i), "");
            r.Count[i] = 0;
            HoldWaves.End();
            Errands.Keep(Player.m_localPlayer, r);
            if (Player.m_localPlayer != null) RavenMsg.Center(Player.m_localPlayer, Lang.T("Варту зірвано. Почни її знову, уночі, біля рунного кілка."));
            return false;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            if (r.Count[i] >= _n) return Lang.T("Варту відстояно");
            int min = Mathf.Max(1, Mathf.RoundToInt(_n / 60f));
            if (!Begun(r, i)) return Lang.F("Вартуй уночі в рунному колі — {0} хв; почни біля кілка посередині", min);
            string line = Lang.F("Варта: {0} / {1} хв", r.Count[i] / 60, min);
            return Off(r, i, p) > 0f ? line + Lang.T(" (на паузі — повернись у коло)") : line;
        }
    }

    // The watch's beasts: ordinary creatures of the ring's land (as at your stage at most), a few at a time, more with
    // each wave, coming from outside the ring and hunting you. Marked as the raven tree arena's (BindArena.Key): no loot,
    // no kill counted — a watch begun again and again farms nothing. Gone when no watch keeps them (Late).
    internal static class HoldWaves
    {
        internal static float Gap = 30f;
        internal static int MaxAlive = 8;
        private static readonly List<Character> Alive = new List<Character>();
        private static bool _on, _wanted;
        private static float _next;
        private static int _wave;

        internal static void Run(Player p, Vector3 at, float radius, bool inside)
        {
            _wanted = true;
            if (!_on) { _on = true; _wave = 0; _next = Time.time + 5f; }
            Alive.RemoveAll(c => c == null || c.IsDead());
            if (!inside || Time.time < _next || Alive.Count >= MaxAlive || ZNetScene.instance == null || WorldGenerator.instance == null) return;
            _next = Time.time + Gap;
            _wave++;
            int tier = Mathf.Min(Champions.TierOf(WorldGenerator.instance.GetBiome(at)), Mathf.Min(Stage.Of(p), 7));
            List<string> can = Champions.Plain(tier);
            if (can.Count == 0) return;
            int n = Mathf.Min(MaxAlive - Alive.Count, 1 + _wave / 2);
            for (int k = 0; k < n; k++)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(can[Random.Range(0, can.Count)]);
                if (prefab != null) SpawnOne(p, prefab, at, radius, Random.value < 0.15f ? 2 : 1);
            }
        }

        private static void SpawnOne(Player p, GameObject prefab, Vector3 at, float radius, int level)
        {
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            for (int tries = 0; tries < 12; tries++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), d = radius + Random.Range(12f, 22f);
                Vector3 pos = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
                if (ZoneSystem.instance == null) return;
                Vector3 normal; Heightmap.Biome biome; Heightmap.BiomeArea area; Heightmap hmap;
                ZoneSystem.instance.GetGroundData(ref pos, out normal, out biome, out area, out hmap);
                if (pos.y < sea + 0.5f) continue; // (never in the water)
                if (hmap != null && WorldGenerator.IsAshlands(pos.x, pos.z) && hmap.GetLava(pos) > 0.05f) continue;
                pos.y += 0.5f;
                GameObject go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                Character c = go.GetComponent<Character>();
                ZNetView nv = go.GetComponent<ZNetView>();
                if (c == null || nv == null || !nv.IsValid()) { Object.Destroy(go); return; }
                nv.GetZDO().Set(BindArena.Key, p.GetPlayerID()); // (no loot, no kill credit — as the arena's)
                nv.GetZDO().Persistent = false; // never saved: one who quits leaves none behind
                if (level > 1) c.SetLevel(level);
                BaseAI ai = c.GetBaseAI();
                if (ai != null) ai.SetHuntPlayer(true);
                Alive.Add(c);
                return;
            }
        }

        // before the steps' tick (Errands.Tick): a watch no step kept going is over
        internal static void Late()
        {
            if (!_wanted && _on) End();
            _wanted = false;
        }

        internal static void End()
        {
            _on = false;
            foreach (Character c in Alive)
            {
                ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
                if (nv != null && nv.IsValid() && !c.IsDead()) { if (!nv.IsOwner()) nv.ClaimOwnership(); ZNetScene.instance.Destroy(c.gameObject); }
            }
            Alive.Clear();
        }
    }
}
#endif
