#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The quiet round a story event (the user 2026-11-02): another player must not spoil a scene by bringing monsters to
    // it — and nobody should notice a wall. Round the event's place (a scene's «place:», or the mark of the stage that
    // leads to a scene), never round the player — unless the scene has no place: then round him while it plays.
    // Every distance of it is StoryWhere.Apart: a round on the land never reaches an inside of ours hung over it (the user 2026-11-15: a
    // round on the grass over the barrow left the whole crypt without foes), nor one inside the land under it.
    //   grows:  it is there once the player is on such a stage and within Approach m of the place; small (Min m) far
    //           off, wider as he comes, full (Max) when he is at the place or the scene plays; it never shrinks while he
    //           steps about — it fades slowly (Fade m/s) once nobody wants it
    //   Max:    the scene's «guard: <m>» (off — none), else by the place's size (Loc.Reach), MaxLo–MaxHi m; never less
    //           than the scene's «calm» looks (Senses.FoeRange) and CalmMargin m more — a monster the scene would wait for
    //           is run off, not waited for (the user 2026-11-03: a boar 15 m off held the shield's scene)
    //   soft:   the whole round — no monster is born within it (a free spawn of the world: BirthMargin m more); one not chasing
    //           anyone runs out of it; one in a fight may follow its foe in
    //   hard:   the core (HardShare of it, HardLo–HardHi m) — nobody stays in it; a monster whose target stands in it
    //           lets the target go and runs off (archers shooting in from outside too, within Shoot m)
    //   each monster has its own edge (±Jitter): no line where all of them stop
    // Monsters run off (the user: «нехай біжать») — the game's own way near a trader. A monster a scene wants (its ZDO
    // KFoe) is left alone; bosses and tamed ones too.
    // Every game that wants a round tells it to all games every Renew s (a monster is run where its owner is); a round
    // not told again for Life s is gone — a crash leaves nothing behind.
    internal static class StoryHush
    {
        internal static float Approach = 150f, Min = 8f, MaxLo = 16f, MaxHi = 40f, Default = 30f, Grow = 8f, Fade = 1.5f;
        internal static float CalmMargin = 8f;
        internal static float HardShare = 0.35f, HardLo = 8f, HardHi = 14f, BirthMargin = 30f, Shoot = 60f, Jitter = 0.2f;
        internal static float Life = 8f, Renew = 3f;
        internal static readonly int KFoe = "RavenWatch_sceneFoe".GetStableHashCode();
        private const string Rpc = "RavenWatch_StoryHush";

        // ---------------------------------------------------------------- the rounds this game wants (Look, from the stages)
        private sealed class Mine { public Vector3 At; public float Max, R; public bool Full, Wanted; }
        private static readonly Dictionary<string, Mine> _mine = new Dictionary<string, Mine>();
        private static float _sendAt;
        private static bool _changed;

        // a pass of the story begins: what is not wanted again in it fades
        private static void Pass() { foreach (Mine m in _mine.Values) m.Wanted = false; }

        // twice a second (StoryQuests.Each): the rounds this player's story wants — the scene playing (round its place, else round
        // him); a stage that plays a scene with a place; a stage whose mark leads to a scene (its next stage's, or its ways')
        internal static void Look(Player p)
        {
            Pass();
            StoryScenes.SceneDef now = StoryScenes.Current;
            if (now != null) WantAt("scene", now, now.Place.Length > 0 ? now.Place : null, p, true);
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                StoryQuests.StageDef s = StoryQuests.StageNow(p, q);
                if (s == null) continue;
                if (s.Scene.Length > 0) { StoryScenes.SceneDef sc = StoryScenes.Find(s.Scene); if (sc != null && sc.Place.Length > 0 && sc != now) WantAt(q.Id + "." + s.Id, sc, sc.Place, p, false); }
                if (s.Ways.Count == 0) { if (s.MarkAt.Length > 0) WantAt(q.Id + "." + s.Id + ".m", StoryQuests.Leads(q, s.Next), s.MarkAt, p, false); }
                else foreach (StoryQuests.StageDef w in s.Ways) if (w.MarkAt.Length > 0) WantAt(q.Id + "." + s.Id + "." + w.Id, StoryQuests.Leads(q, w.Next), w.MarkAt, p, false);
            }
        }

        // (a mark that leads to no scene wants no quiet)
        private static void WantAt(string key, StoryScenes.SceneDef sc, string place, Player p, bool full)
        {
            if (sc == null) return;
            Vector3 at;
            if (place == null) at = p.transform.position; // (a scene with no place: round the player)
            else if (!StoryWhere.Find(place, out at)) return;
            Want(key, at, MaxFor(sc, place), full);
        }

        // a round wanted this pass: its place, its full size, full now (the scene plays) or by the player's distance
        private static void Want(string key, Vector3 at, float max, bool full)
        {
            if (max <= 0f) return;
            Mine m;
            if (!_mine.TryGetValue(key, out m)) { m = new Mine(); _mine[key] = m; }
            m.At = at; m.Max = Mathf.Clamp(max, 1f, 80f); m.Full = full; m.Wanted = true;
        }

        // the full size of a round at a place for a scene: the scene's word, else the place's size, else Default
        internal static float MaxFor(StoryScenes.SceneDef sc, string place)
        {
            if (sc != null && sc.Guard == 0f) return 0f;
            return Mathf.Max(MaxOwn(sc, place), Senses.FoeRange + CalmMargin);
        }

        private static float MaxOwn(StoryScenes.SceneDef sc, string place)
        {
            if (sc != null && sc.Guard > 0f) return sc.Guard;
            string piece;
            Loc loc = string.IsNullOrEmpty(place) ? null : StoryWhere.Place(place, out piece);
            if (loc != null && loc.Shape()) return Mathf.Clamp(loc.Reach() + 4f, MaxLo, MaxHi);
            return Default;
        }

        // every frame: the rounds grow and fade softly; told to everyone now and then
        internal static void Tick(Player p)
        {
            if (p == null) return;
            float dt = Time.deltaTime;
            List<string> gone = null;
            foreach (KeyValuePair<string, Mine> kv in _mine)
            {
                Mine m = kv.Value;
                float want = 0f;
                if (m.Wanted)
                {
                    if (m.Full) want = m.Max;
                    else
                    {
                        float d = StoryWhere.Apart(m.At, p.transform.position) - m.Max;
                        if (d <= 0f) want = m.Max;
                        else if (d < Approach) want = Mathf.Lerp(Mathf.Min(Min, m.Max), m.Max, 1f - d / Approach);
                    }
                }
                float was = m.R;
                if (want > m.R) m.R = Mathf.MoveTowards(m.R, want, Grow * dt); // (it grows as he comes …)
                else if (!m.Wanted || want <= 0f) m.R = Mathf.MoveTowards(m.R, 0f, Fade * dt); // (… and only fades when nobody wants it: steps back do not shrink it)
                if (Mathf.Abs(m.R - was) > 0.0001f && Mathf.Abs(m.R - _sentR(kv.Key)) > 2f) _changed = true;
                if (!m.Wanted && m.R <= 0.01f) (gone ?? (gone = new List<string>())).Add(kv.Key);
            }
            if (gone != null) { foreach (string k in gone) _mine.Remove(k); _changed = true; }
            if (!_changed && Time.time < _sendAt) return;
            if (_mine.Count == 0 && !_changed) return;
            bool any = _changed || _sent.Count > 0; // (still far from every place it wants: nothing to tell anyone, the whole time he is on that stage)
            if (!any) foreach (Mine m in _mine.Values) if (m.R > 0.5f) { any = true; break; }
            _sendAt = Time.time + Renew;
            if (!any) return;
            _changed = false;
            Send();
        }

        private static readonly Dictionary<string, float> _sent = new Dictionary<string, float>();
        private static float _sentR(string key) { float r; return _sent.TryGetValue(key, out r) ? r : 0f; }

        private static void Send()
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            _sent.Clear();
            int n = 0;
            foreach (Mine m in _mine.Values) if (m.R > 0.5f) n++;
            pkg.Write(n);
            foreach (KeyValuePair<string, Mine> kv in _mine)
            {
                if (kv.Value.R <= 0.5f) continue;
                pkg.Write(kv.Value.At); pkg.Write(kv.Value.R); pkg.Write(HardOf(kv.Value.R, kv.Value.Max));
                _sent[kv.Key] = kv.Value.R;
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg); // (this game too)
        }

        // the core: a share of the full size, grown with the round
        private static float HardOf(float r, float max) { return Mathf.Clamp(max * HardShare, HardLo, HardHi) * Mathf.Clamp01(r / Mathf.Max(1f, max)); }

        // ---------------------------------------------------------------- the rounds of every game (here)
        private struct Ring { public Vector3 At; public float Soft, Hard, Until; }
        private static readonly Dictionary<long, List<Ring>> _rings = new Dictionary<long, List<Ring>>(); // (by the game that wants them)
        private static int _count;

        internal static void RegisterRpcs() { ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnRings); }

        private static void OnRings(long from, ZPackage pkg)
        {
            int n = pkg.ReadInt();
            if (n <= 0) { _rings.Remove(from); Count(); return; }
            List<Ring> l = new List<Ring>(Mathf.Min(n, 16));
            float until = Time.time + Life;
            for (int i = 0; i < n; i++)
            {
                Vector3 at = pkg.ReadVector3(); float soft = pkg.ReadSingle(), hard = pkg.ReadSingle();
                if (i < 16) l.Add(new Ring { At = at, Soft = Mathf.Min(soft, 80f), Hard = Mathf.Min(hard, soft), Until = until });
            }
            _rings[from] = l;
            Count();
        }

        private static void Count() { _count = 0; foreach (List<Ring> l in _rings.Values) _count += l.Count; }

        // rounds not told again: gone (asked by the checks below, at most once a second)
        private static float _pruneAt;
        private static void Prune()
        {
            if (Time.time < _pruneAt) return;
            _pruneAt = Time.time + 1f;
            List<long> old = null;
            foreach (KeyValuePair<long, List<Ring>> kv in _rings) if (kv.Value.Count == 0 || Time.time > kv.Value[0].Until) (old ?? (old = new List<long>())).Add(kv.Key);
            if (old == null) return;
            foreach (long k in old) _rings.Remove(k);
            Count();
        }

        // no birth here: within a round (StoryWhere.Apart — never across the line between the land and an inside: a round on the grass over a
        // crypt of ours left the whole crypt without foes, the user 2026-11-15). The world's free spawns keep BirthMargin m more (they come from
        // out of sight and walk in); a fixed spawner of a place (a dungeon's guard, a nest) only the round itself — it is the place's own foe
        internal static bool NoBirth(Vector3 point, bool fixedPoint)
        {
            if (_count == 0) return false;
            Prune();
            float margin = fixedPoint ? 0f : BirthMargin;
            foreach (List<Ring> l in _rings.Values)
                foreach (Ring r in l) if (StoryWhere.Apart(r.At, point) < r.Soft + margin) return true;
            return false;
        }

        // ---------------------------------------------------------------- the monsters near
        private static readonly FieldInfo FTarget = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");
        private static readonly FieldInfo FStatic = AccessTools.Field(typeof(MonsterAI), "m_targetStatic");
        private static readonly MethodInfo MFlee = AccessTools.Method(typeof(BaseAI), "Flee");
        private static readonly MethodInfo MAlert = AccessTools.Method(typeof(BaseAI), "SetAlerted");
        private static readonly object[] NoAlert = { false };

        // runs before the monster's own thinking (its owner's game): true — it ran off this frame, its own thinking skipped
        internal static bool KeepOff(MonsterAI ai, float dt)
        {
            if (_count == 0 || ai == null) return false;
            Prune();
            if (_count == 0) return false;
            Vector3 pos = ai.transform.position;
            if (!Near(pos)) return false; // (most monsters of the world: one distance each, nothing more asked)
            Character target = FTarget != null ? FTarget.GetValue(ai) as Character : null;
            float edge = 1f + Jitter * (Frac(ai.GetInstanceID() * 0.6180339f) * 2f - 1f); // (its own edge)
            Vector3 from = Vector3.zero; bool off = false;
            foreach (List<Ring> l in _rings.Values)
            {
                foreach (Ring r in l)
                {
                    float d = StoryWhere.Apart(r.At, pos);
                    if (d > r.Soft * edge + Shoot) continue;
                    bool inHard = d < r.Hard * edge;
                    bool inSoft = d < r.Soft * edge;
                    bool targetHard = target != null && StoryWhere.Apart(r.At, target.transform.position) < r.Hard; // (its prey in the core: shooting in from outside too)
                    if (inHard || targetHard || (inSoft && target == null)) { from = r.At; off = true; break; }
                }
                if (off) break;
            }
            if (!off) return false;
            Character me = ai.GetComponent<Character>();
            if (me == null || me.IsTamed() || me.IsBoss() || me.IsDead()) return false;
            ZNetView nv = me.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return false; // (only where it is run — elsewhere the game's own picture)
            if (nv.GetZDO().GetBool(KFoe)) return false; // (the scene's own)
            try
            {
                if (FTarget != null) FTarget.SetValue(ai, null);
                if (FStatic != null) FStatic.SetValue(ai, null);
                if (MAlert != null && ai.IsAlerted()) MAlert.Invoke(ai, NoAlert);
                if (MFlee != null) MFlee.Invoke(ai, new object[] { dt, from });
            }
            catch (Exception e) { Story.Log("hush: " + e.Message); return false; }
            return true;
        }

        private static bool Near(Vector3 pos)
        {
            foreach (List<Ring> l in _rings.Values)
                foreach (Ring r in l) if (StoryWhere.Apart(r.At, pos) < r.Soft * (1f + Jitter) + Shoot) return true;
            return false;
        }

        private static float Frac(float v) { return v - Mathf.Floor(v); }
    }

    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    internal static class StoryHushMonster
    {
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        {
            try
            {
                if (!StoryHush.KeepOff(__instance, dt)) return true;
                __result = true;
                return false;
            }
            catch (Exception e) { PatchGuard.Fail("StoryHushMonster", e); return true; }
        }
    }

    [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
    internal static class StoryHushSpawn
    {
        private static bool Prefix(Vector3 spawnPoint)
        {
            try { return !StoryHush.NoBirth(spawnPoint, false); }
            catch (Exception e) { PatchGuard.Fail("StoryHushSpawn", e); return true; }
        }
    }

    [HarmonyPatch(typeof(CreatureSpawner), "Spawn")]
    internal static class StoryHushPoint
    {
        private static bool Prefix(CreatureSpawner __instance, ref ZNetView __result)
        {
            try { if (!StoryHush.NoBirth(__instance.transform.position, true)) return true; }
            catch (Exception e) { PatchGuard.Fail("StoryHushPoint", e); return true; }
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(SpawnArea), "SpawnOne")]
    internal static class StoryHushNest
    {
        private static bool Prefix(SpawnArea __instance, ref bool __result)
        {
            try { if (!StoryHush.NoBirth(__instance.transform.position, true)) return true; }
            catch (Exception e) { PatchGuard.Fail("StoryHushNest", e); return true; }
            __result = false;
            return false;
        }
    }
}
#endif
