#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The game's own creatures in a scene (chapter 2, the user 2026-11-18: on the meadow Eyvind marked «a skeleton with Hel's blue-green eyes comes at
    // us from afar, we stand wary — and an ordinary boar kills it»; «треба додати якось тварин»). Real creatures of the game, made by the game that
    // plays the scene (its network objects: every game near sees them, the game draws and runs them), each by its name in the scene:
    //   beast <name> spawn <Prefab> <place> [face <place>] [hel] [hp <n>] [keep]
    //                         made on the ground there, facing that (none: you). hel — Hel's eyes (blue-green: HelEyes, below, on every game
    //                         that draws it: the world key KHel on it); hp — its health now (1: one blow kills it); keep — it stays in the world
    //                         after the scene as a creature like any (the boar); without — gone with the scene. It stands, its own mind off,
    //                         till «go» or «hunt» (its own mind would go for you at once)
    //   beast <name> go <place|player|beast.x> [near <m>=2] [run] [nowait]   walked there in a straight line by the scene, its own mind off (it fights
    //                         nobody on the way); stops near it; waits till it is there unless «nowait»
    //   beast <name> hunt player|beast.<x>   its own mind on, that one its foe (AiTarget: the game's own fight — the boar goes for the skeleton:
    //                         BaseAI.IsEnemy, a beast of the meadows and the dead are foes). Kept so while the scene holds it
    //   beast <name> dead [s=8]  waits till it is dead; not dead by then — it falls by the blow of the one hunting it (no scene hangs on a miss)
    //   beast <name> stay [face <place>]   its own mind off, it stands (the boar after the kill: it does not go for you mid-scene)
    //   beast <name> gone        taken out of the world
    //   beast.<name>             a place word: where it is (its middle; the camera: «camera on beast.skel full low»)
    // While the scene plays they are its own: no fight of yours (StoryScenes.Ours: it does not stop for them, as for the rising waves), the quiet
    // round leaves them (StoryHush.KFoe). Not kept by the world while the scene plays (ZDO.Persistent off: its game gone mid-scene — they go with it).
    // Its end: those «keep» are the world's from then on (kept, their own mind, no target; KFoe stays, so the fading quiet round does not run them off);
    // the rest go. A scene stopped: all of them go (it plays again from the start: no second boar). A creature whose foe fell stands (its mind off).
    internal static class StoryBeasts
    {
        internal static float GoNear = 2f, DeadIn = 8f, StuckFor = 3f, StuckMoved = 0.4f;
        internal static readonly int KHel = "RavenWatch_helEyes".GetStableHashCode(); // (on a creature: its eyes are Hel's — every game that draws it paints them)
        private static readonly int KKept = "RavenWatch_beastKept".GetStableHashCode(); // (on a «keep» one: the world's time it became the world's)

        private sealed class Beast
        {
            public string Name = "", Prefab = "", GoWord = "";
            public Character C;
            public bool Keep, Going, Run, MindOffNext;
            public int MadeFrame; // (the frame it was made in: its own Start — the weapon in its hand — runs in a later one)
            public float Near, CheckAt, DeadBy; // (CheckAt: the next «stuck?» look of its walk · DeadBy: «dead»'s own time)
            public Vector3 CheckFrom;
            public Character Hunts;
        }
        private static readonly Dictionary<string, Beast> _beasts = new Dictionary<string, Beast>(); // this scene's, by name
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void Steps()
        {
            StoryScenes.Register("beast", new StoryScenes.StepKind(StoryScenes.StepSeen.World) { Min = 2, VerbAt = 2,
                Usage = "beast <назва> spawn <Prefab> <місце> [face <місце>] [hel] [hp <n>] [keep] · go <місце> [near м] [run] [nowait] · hunt player|beast.<x> · dead [с] · stay [face <місце>] · gone",
                Verbs = new Dictionary<string, StoryScenes.StepVerb>
            {
                { "spawn", new StoryScenes.StepVerb(StoryScenes.StepSeen.World) { Min = 2, PlaceAt = 4,
                    Check = st => Arg(st.W, "hp") != null && F(Arg(st.W, "hp"), -1f) <= 0f ? "beast … hp <n>: число" : Arg(st.W, "face") != null && Arg(st.W, "face") != "player" ? StoryWhere.Why(Arg(st.W, "face")) : null,
                    Begin = Spawn, Setup = Spawn, Done = Said } },
                { "go", new StoryScenes.StepVerb(StoryScenes.StepSeen.World) { Min = 1, PlaceAt = 3,
                    Begin = Go, Busy = (p, st) => Array.IndexOf(st.W, "nowait") < 0 && Walking(st.W[1]), Done = Said } },
                { "hunt", new StoryScenes.StepVerb(StoryScenes.StepSeen.World) { Min = 1, PlaceAt = 3, Begin = Hunt, Done = Said } },
                { "dead", new StoryScenes.StepVerb(StoryScenes.StepSeen.World) {
                    Begin = (p, st) => { Beast b = Of(st.W[1], null); if (b != null) b.DeadBy = Time.time + (st.W.Length > 3 ? F(st.W[3], DeadIn) : DeadIn); }, // (dead already — the boar was quick: nothing to wait for, nothing amiss)
                    Setup = (p, st) => { Beast b = Of(st.W[1], null); if (b != null) { b.DeadBy = 0f; Dying(p, st); } }, // (played from a later step: it is down)
                    Busy = Dying, Done = Said } },
                { "stay", new StoryScenes.StepVerb(StoryScenes.StepSeen.World) { PlaceAfter = "face", Begin = Stay, Setup = Stay, Done = Said } },
                { "gone", new StoryScenes.StepVerb(StoryScenes.StepSeen.World) { Begin = (p, st) => { Beast b = Of(st.W[1], "gone"); if (b != null) { Remove(b); _beasts.Remove(b.Name); } } } },
            } });
            StoryScenes.Ends.Add(SceneEnd);
            StoryScenes.Ours.Add(Ours);
        }

        // ---------------------------------------------------------------- the steps
        private static void Spawn(Player p, StoryScenes.Step st)
        {
            string[] w = st.W;
            string name = w[1];
            Beast old;
            if (_beasts.TryGetValue(name, out old)) { Remove(old); _beasts.Remove(name); } // (one name, one creature)
            GameObject pf = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(w[3]) : null;
            if (pf == null || pf.GetComponent<Character>() == null) { StoryScenes.Note("beast spawn: у грі нема істоти «" + w[3] + "» — крок пропущено"); return; }
            Vector3 at;
            if (!StoryWhere.Find(w[4], out at)) { StoryScenes.Note("beast spawn: нема місця «" + w[4] + "» — крок пропущено"); return; }
            float ground;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out ground)) at.y = ground;
            Vector3 face = p.transform.position;
            string fw = Arg(w, "face");
            if (fw != null && fw != "player" && !StoryWhere.Find(fw, out face)) face = p.transform.position;
            Vector3 look = face - at; look.y = 0f;
            GameObject go = UnityEngine.Object.Instantiate(pf, at + Vector3.up * 0.1f, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity); // heavy: once a step — the creature itself
            Character c = go.GetComponent<Character>();
            ZNetView nv = go.GetComponent<ZNetView>();
            ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (z != null) { z.Set(StoryHush.KFoe, true); z.Persistent = false; } // (the scene's own: no quiet round runs it off; not kept by the world till the scene ends — the game
            // takes it out when its game leaves: a relog or a crash mid-scene leaves no skeleton on the meadow for ever, no second boar when it plays again; «keep» — kept at the end)
            bool hel = Array.IndexOf(w, "hel") > 3;
            if (hel) { if (z != null) z.Set(KHel, true); HelEyes.Paint(c); }
            string hp = Arg(w, "hp");
            if (hp != null && c != null && nv != null && nv.IsOwner()) c.SetHealth(Mathf.Max(1f, F(hp, 1f)));
            _beasts[name] = new Beast { Name = name, Prefab = w[3], C = c, Keep = Array.IndexOf(w, "keep") > 3, MindOffNext = true, MadeFrame = Time.frameCount }; // (it stands till the scene says go or hunt — its own
            // mind would go for you at once —, but off only the next frame: its MonsterAI.Start gives it its weapon first; off now, that never runs — the review 2026-11-18)
            StoryScenes.Track("beast " + name + ": " + w[3] + " made at " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z) + (hel ? ", Hel's eyes" : "") + (hp != null ? ", health " + hp : ""));
        }

        private static void Go(Player p, StoryScenes.Step st)
        {
            Beast b = Of(st.W[1], "go");
            if (b == null) return;
            b.MindOffNext = false;
            Mind(b, false);
            b.GoWord = st.W[3]; b.Near = Arg(st.W, "near") != null ? F(Arg(st.W, "near"), GoNear) : GoNear; b.Run = Array.IndexOf(st.W, "run") > 3;
            b.Going = true; b.Hunts = null; b.CheckAt = Time.time + StuckFor; b.CheckFrom = b.C.transform.position;
        }

        private static void Hunt(Player p, StoryScenes.Step st)
        {
            Beast b = Of(st.W[1], "hunt");
            if (b == null) return;
            Character foe = st.W[3] == "player" ? p : st.W[3].StartsWith("beast.", StringComparison.Ordinal) ? CharOf(st.W[3].Substring(6)) : null;
            if (foe == null) { StoryScenes.Note("beast hunt: нема «" + st.W[3] + "» (player чи beast.<назва>) — крок пропущено"); return; }
            if (!(b.C.GetBaseAI() is MonsterAI)) { StoryScenes.Note("beast hunt: «" + b.Prefab + "» не б'ється сам (у нього нема MonsterAI) — крок пропущено"); return; }
            b.Going = false; b.MindOffNext = false;
            Mind(b, true);
            b.Hunts = foe;
            Aim(b);
        }

        // waits till it is dead; its time over — it falls by the blow of the one that hunts it (or nobody's)
        private static bool Dying(Player p, StoryScenes.Step st)
        {
            Beast b = Of(st.W[1], null);
            if (b == null || b.C == null || b.C.IsDead()) return false;
            if (Time.time < b.DeadBy) return true;
            Beast by = null;
            foreach (Beast o in _beasts.Values) if (o.Hunts == b.C) { by = o; break; }
            HitData h = new HitData();
            h.m_damage.m_damage = b.C.GetMaxHealth() * 10f + 1000f;
            h.m_point = b.C.GetCenterPoint();
            if (by != null && by.C != null) h.SetAttacker(by.C);
            b.C.Damage(h);
            StoryScenes.Note("beast " + b.Name + ": не впав за свій час — ударено" + (by != null ? " (від імені «" + by.Name + "»)" : ""));
            return false;
        }

        private static void Stay(Player p, StoryScenes.Step st)
        {
            Beast b = Of(st.W[1], "stay");
            if (b == null) return;
            b.Going = false; b.Hunts = null; b.MindOffNext = false;
            Mind(b, false);
            string fw = Arg(st.W, "face");
            Vector3 to;
            if (fw != null && StoryWhere.Find(fw, out to)) { Vector3 d = to - b.C.transform.position; d.y = 0f; if (d.sqrMagnitude > 0.01f) b.C.SetLookDir(d.normalized); }
        }

        private static string Said(Player p, StoryScenes.Step st)
        {
            Beast b;
            if (!_beasts.TryGetValue(st.W[1], out b) || b.C == null) return "no beast «" + st.W[1] + "»";
            return b.Prefab + (b.C.IsDead() ? " dead" : ", health " + Mathf.RoundToInt(b.C.GetHealth())) + ", " + Mathf.RoundToInt(Utils.DistanceXZ(b.C.transform.position, p.transform.position)) + " m from him" + (b.Going ? " — still walking" : ""); // flat: a creature and you on the land
        }

        // ---------------------------------------------------------------- every frame while a scene holds beasts (StoryQuests.Frame)
        internal static void Tick(Player p)
        {
            if (_beasts.Count == 0) return;
            foreach (Beast b in _beasts.Values)
            {
                if (b.C == null || b.C.IsDead()) { b.Going = false; continue; }
                if (b.MindOffNext && Time.frameCount > b.MadeFrame + 1) { b.MindOffNext = false; if (!b.Going && (object)b.Hunts == null) Mind(b, false); } // (the frame after its Start: the review 2026-11-18 — off in the frame it was made, Start never ran and the skeleton walked with no sword)
                if (b.Going) Walk(b);
                else if ((object)b.Hunts != null) { if (b.Hunts == null || b.Hunts.IsDead()) { b.Hunts = null; Mind(b, false); } else Aim(b); } // (Unity's null: a foe taken out of the world) // (its foe down: it stands — its own mind would find the next foe: you)
            }
        }

        private static void Walk(Beast b)
        {
            Vector3 to;
            if (!StoryWhere.Find(b.GoWord, out to)) { b.Going = false; b.C.SetMoveDir(Vector3.zero); StoryScenes.Note("beast go: «" + b.GoWord + "» зникло — " + b.Name + " стоїть"); return; }
            Vector3 d = to - b.C.transform.position; d.y = 0f;
            if (d.magnitude <= b.Near) { b.Going = false; b.C.SetMoveDir(Vector3.zero); return; }
            d.Normalize();
            b.C.SetRun(b.Run);
            b.C.SetMoveDir(d);
            b.C.SetLookDir(d);
            if (Time.time < b.CheckAt) return;
            if (Utils.DistanceXZ(b.C.transform.position, b.CheckFrom) < StuckMoved) { b.Going = false; b.C.SetMoveDir(Vector3.zero); StoryScenes.Note("beast go: " + b.Name + " застряг за " + Mathf.RoundToInt(d.magnitude) + " м — стоїть"); return; } // flat: a creature walks the land
            b.CheckAt = Time.time + StuckFor; b.CheckFrom = b.C.transform.position;
        }

        // its foe kept: the game drops a target it loses sight of; the scene's one is held
        private static void Aim(Beast b)
        {
            MonsterAI ai = b.C.GetBaseAI() as MonsterAI;
            if (ai == null || AiTarget.Of(ai) == b.Hunts) return;
            AiTarget.Set(ai, b.Hunts);
            AiTarget.Alert(ai);
        }

        // its own mind on or off (off: the game does not run it at all — BaseAI.OnDisable takes it off the game's list; it stands where the scene puts it)
        private static void Mind(Beast b, bool on)
        {
            BaseAI ai = b.C != null ? b.C.GetBaseAI() : null;
            if (ai == null) return;
            if (!on) { b.C.SetMoveDir(Vector3.zero); MonsterAI m = ai as MonsterAI; if (m != null) AiTarget.Drop(m); }
            ai.enabled = on;
        }

        // ---------------------------------------------------------------- the end
        internal static void SceneEnd(bool broken)
        {
            foreach (Beast b in _beasts.Values)
            {
                if (b.C == null) continue;
                if (broken || !b.Keep) { Remove(b); continue; }
                Mind(b, true); // (the world's now: its own mind, no foe of the scene's)
                MonsterAI m = b.C.GetBaseAI() as MonsterAI;
                if (m != null) AiTarget.Drop(m);
                ZNetView nv = b.C.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid()) { ZDO z = nv.GetZDO(); z.Persistent = true; z.Set(KKept, (long)(ZNet.instance.GetTimeSeconds() * 1000.0)); } // (kept by the world from now — a key written
                // with it, or the flag alone raises no revision and a boar standing still would be lost with its owner: the review 2026-11-18; its mark KFoe stays: the quiet round, fading, would run it off)
            }
            _beasts.Clear();
        }

        private static void Remove(Beast b)
        {
            if (b.C == null) return;
            ZNetView nv = b.C.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid()) { if (!nv.IsOwner()) nv.ClaimOwnership(); ZNetScene.instance.Destroy(b.C.gameObject); }
            b.C = null;
        }

        // the scene's own: a fight with it is no fight of yours (StoryScenes.FoeNear, FightWait)
        private static bool Ours(Character c)
        {
            if (c == null) return false;
            foreach (Beast b in _beasts.Values) if (b.C == c) return true;
            return false;
        }

        // the place word «beast.<name>»: its middle (StoryWhere)
        internal static bool At(string name, out Vector3 at)
        {
            Character c = CharOf(name);
            at = c != null ? c.GetCenterPoint() : Vector3.zero;
            return c != null;
        }

        private static bool Walking(string name) { Beast b; return _beasts.TryGetValue(name, out b) && b.C != null && !b.C.IsDead() && b.Going; }

        internal static Character CharOf(string name) { Beast b; return _beasts.TryGetValue(name, out b) && b.C != null ? b.C : null; }

        private static Beast Of(string name, string verb)
        {
            Beast b;
            if (_beasts.TryGetValue(name, out b) && b.C != null && !b.C.IsDead()) return b;
            if (verb != null) StoryScenes.Note("beast " + verb + ": нема живої «" + name + "» (спершу beast " + name + " spawn …) — крок пропущено");
            return null;
        }

        private static string Arg(string[] w, string word) { int i = Array.IndexOf(w, word); return i > 2 && i + 1 < w.Length ? w[i + 1] : null; }
        private static float F(string s, float or) { float v; return s != null && float.TryParse(s, NumberStyles.Float, Inv, out v) ? v : or; }
    }

    // Hel's eyes on a creature of the game (the user 2026-11-18: «скелет з синьо-зеленими очима Хель» — the first sign that Hel's servant is near;
    // the trolls of his second letter later): blue-green as Hel's flame in the Hall. Its own glow taken over — what of its look already glows
    // (an emissive material: the eyes of the game's monsters are the brightest of it, as a raven's — EyeGlow; a light, a glow of its own named
    // «eye»/«glow») — and a faint light of that colour at its eyes (Character.m_eye). Once a creature (a mark on it); said in the log, what it found.
    // Every game that draws it: the key KHel in its ZDO, looked at in the one pass over the creatures there is (Senses.Seen)
    internal static class HelEyes
    {
        internal static readonly Color Hel = new Color(0.37f, 0.86f, 0.78f);
        internal static float Light = 1.2f, Range = 1.1f, Glow = 1.6f;
        private sealed class Mark : MonoBehaviour { }

        internal static void Look(Character c)
        {
            if (c == null || c.GetComponent<Mark>() != null) return;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid() && nv.GetZDO().GetBool(StoryBeasts.KHel)) Paint(c);
        }

        internal static void Paint(Character c)
        {
            if (c == null || c.GetComponent<Mark>() != null) return;
            c.gameObject.AddComponent<Mark>();
            int lights = 0, glows = 0, fx = 0;
            foreach (Light l in c.GetComponentsInChildren<Light>(true)) { l.color = Hel; lights++; } // heavy: once a creature
            foreach (Renderer r in c.GetComponentsInChildren<Renderer>(true)) // heavy: once a creature
            {
                bool named = r.name.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0;
                if (r is ParticleSystemRenderer) { ParticleSystem ps = r.GetComponent<ParticleSystem>(); if (named && ps != null) { ParticleSystem.MainModule m = ps.main; m.startColor = Hel; fx++; } continue; }
                float most = -1f;
                foreach (Material m in r.sharedMaterials) // (looked at, not copied: the tint goes on as a property block — no copies of the game's looks left behind)
                {
                    if (m == null || !m.HasProperty("_EmissionColor") || !m.IsKeywordEnabled("_EMISSION")) continue;
                    float was = m.GetColor("_EmissionColor").maxColorComponent;
                    if (was > 0.05f || named) most = Mathf.Max(most, was);
                }
                if (most < 0f) continue;
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", Hel * Mathf.Max(most, Glow));
                r.SetPropertyBlock(mpb);
                glows++;
            }
            Transform eye = c.m_eye != null ? c.m_eye : c.transform;
            GameObject g = new GameObject("RavenWatch_helEyes"); // heavy: once a creature
            g.transform.SetParent(eye, false);
            Light own = g.AddComponent<Light>();
            own.type = LightType.Point; own.color = Hel; own.range = Range; own.intensity = Light; own.shadows = LightShadows.None;
            Story.Log("hel eyes on " + Utils.GetPrefabName(c.gameObject) + ": " + lights + " light(s) of its own, " + glows + " glowing look(s), " + fx + " glow fx" + (lights + glows + fx == 0 ? " — NONE of its own glows: only our light at its eyes" : "") + ", our light at " + (c.m_eye != null ? "its eyes" : "its root (no m_eye)"));
        }
    }
}
#endif
