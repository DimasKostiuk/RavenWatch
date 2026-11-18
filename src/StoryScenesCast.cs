#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A person made «whole» (behind a black screen, out of a falcon's feathers) is made bare: the game dresses it over its next frames, and a
    // camera on it showed a naked body for a moment (the first run of the low change, 2026-11-14). Its body is not drawn till its clothes are
    // on (the game's own lists of what it wears), or VeilMost s at most — one with no clothes at all is shown then
    internal sealed class NpcVeil : MonoBehaviour
    {
        internal static float VeilMost = 0.6f;
        private static readonly System.Reflection.FieldInfo Chest = HarmonyLib.AccessTools.Field(typeof(VisEquipment), "m_chestItemInstances"), Legs = HarmonyLib.AccessTools.Field(typeof(VisEquipment), "m_legItemInstances");
        private VisEquipment _vis; private float _till;
        // Held: made beforehand for a scene and not to be seen yet («npc <who> ready»: making three people in the frame a falcon turns into them froze the
        // picture — the user 2026-11-14) — nothing of it is drawn, clothes the game puts on meanwhile too, till Release shows it dressed
        internal bool Held;
        private readonly HashSet<Renderer> _off = new HashSet<Renderer>(); private float _look;
        internal void Release()
        {
            Held = false;
            foreach (Renderer r in _off) if (r != null) r.enabled = true;
            _off.Clear();
            if (_vis != null && _vis.m_bodyModel != null) _vis.m_bodyModel.enabled = true;
            Destroy(this);
        }
        private void Awake()
        {
            _vis = GetComponent<VisEquipment>();
            _till = Time.time + VeilMost;
            if (_vis == null || _vis.m_bodyModel == null) { Destroy(this); return; }
            _vis.m_bodyModel.enabled = false;
        }
        private void LateUpdate()
        {
            if (Held)
            {
                if (Time.time < _look) return;
                _look = Time.time + 0.15f;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r != null && r.enabled) { r.enabled = false; _off.Add(r); } // heavy: seven times a second, only while a person made beforehand waits unseen (a few seconds of a scene)
                return;
            }
            if (_vis == null || _vis.m_bodyModel == null) { Destroy(this); return; }
            List<GameObject> chest = Chest != null ? Chest.GetValue(_vis) as List<GameObject> : null, legs = Legs != null ? Legs.GetValue(_vis) as List<GameObject> : null; // (a few frames only: it takes itself off)
            if (Time.time < _till && (chest == null || chest.Count == 0) && (legs == null || legs.Count == 0)) return;
            _vis.m_bodyModel.enabled = true;
            Destroy(this);
        }
    }

    // The scene director (StoryScenes), its people: taken, locked for the scene (one scene a person), ordered, let go.
    internal static partial class StoryScenes
    {
        // ---------------------------------------------------------------- the people of the scene
        internal static IEnumerable<KeyValuePair<string, NpcActor>> CastNow { get { return _cast; } } // (the studio's scene check reads who is in it)
        private static readonly Dictionary<string, NpcActor> _cast = new Dictionary<string, NpcActor>(); // (its word → the one taken)
        private static readonly Dictionary<NpcActor, long> _ledWhenTaken = new Dictionary<NpcActor, long>(); // session: whom each person of the scene followed by the story's word when the scene took it
        private static readonly List<NpcActor> _made = new List<NpcActor>(); // (made for this scene: «npc <who> spawn» — taken away after it)
        internal static float RunFrom = 12f; // (m: a person of a scene farther off than this runs)
        private static NpcActor _walker;
        private static float _limit; // (this step's own time limit; 0 — the general one)
        private static Vector3 _walkTo, _walkFrom;
        private static float _walkNear, _walkBegan, _walkWalkFrom; // (where and when its walk began; m left when it fell from a run to a walk, below zero none)
        private static bool _walkRanAny; private static string _walkSaid; // (what the walk came to: the step's Done)

        // ---------------------------------------------------------------- one scene a person (the user 2026-11-02)
        // A scene that takes a person locks it: «<player>@<until>» in its ZDO (KScene), written where it is run (the act
        // «scenelock»), renewed every LockRenew s while the scene holds it, taken off when the scene lets it go. Another
        // player's scene meeting a person under a fresh lock stops (and tries again later, as after any stop); a lock not
        // renewed (its player left) is free after LockFor s.
        internal static readonly int KScene = "RavenWatch_npcScene".GetStableHashCode();
        internal static float LockFor = 15f, LockRenew = 5f;
        private static float _renewAt;
        private static bool _wired;

        private static void Wire()
        {
            if (_wired) return;
            _wired = true;
            StoryProps.Wire();   // («prop»: a thing in a person's hand)
            NpcSpeech.SceneOf = Holder; // (a scene's people hear every line said in it, however far: NpcSpeech)
            NpcActor.Acts["scenelock"] = (a, w) => { ZDO z = a.Zdo; if (z != null) z.Set(KScene, w.Length > 1 ? w[1] : ""); }; // (where it is run: its owner writes)
            NpcActor.Acts["mate"] = (a, w) => { NpcBody body = a.GetComponent<NpcBody>(); if (body != null) body.MateSet(w.Length > 1 ? w[1] : null, w.Length > 2 ? F(w[2], 0.8f) : 0.8f); }; // (a clip of two: whom it is played on — NpcBody.MateLook, every game)
            NpcMind.SceneWatch = a =>
            {
                ZDO z = a != null ? a.Zdo : null;
                if (z == null || Holder(a) != 0L) return;
                // one made for a scene whose lock ran out: its scene ended without it (its zone let go before the scene's end — a jump, a portal: FreeCast found no
                // body to fade). It has no day of its own to be let go into: it fades away, as its scene would have had it (the review 2026-11-14)
                if (_now == null && z.GetString(KScene, "").Length > 0 && a.Tag == MadeTag) { /* no scene playing here: one still playing takes its body back when it loads again — Actor */ Npcs.Log(a.Id + ": made for a scene that is over — fades away"); Lock(a, false); NpcFade.Out(a, Fades.Person, "-"); return; }
                if (z.GetString(KScene, "").Length > 0) { Npcs.Log(a.Id + ": its scene's lock ran out, nobody renews it — let go"); LetGo(a, LockOf(a)); return; } // (its player left, the game closed, its owner changed: nobody else would)
                StoryActions.LeadWatch(a);
            };
        }

        private static double Now { get { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0; } }
        private static long Me { get { return Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L; } }

        // whose scene locked it, the lock run out or not (0 — none)
        private static long LockOf(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            string v = z != null ? z.GetString(KScene, "") : "";
            int at = v.IndexOf('@');
            long who;
            return at > 0 && long.TryParse(v.Substring(0, at), out who) ? who : 0L;
        }

        // who holds a person for a scene now (0 — nobody)
        internal static long Holder(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            string v = z != null ? z.GetString(KScene, "") : "";
            int at = v.IndexOf('@');
            long who; double until;
            if (at <= 0 || !long.TryParse(v.Substring(0, at), out who) || !double.TryParse(v.Substring(at + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out until)) return 0L;
            return until > Now ? who : 0L;
        }

        // held by another player's scene?
        internal static bool HeldByOther(NpcActor a) { long h = Holder(a); return h != 0L && h != Me; }

        private static void Lock(NpcActor a, bool on)
        {
            if (a == null) return;
            a.Act("scenelock " + (on ? Me + "@" + (Now + LockFor).ToString("0", System.Globalization.CultureInfo.InvariantCulture) : ""));
        }

        internal static bool InCast(NpcActor a) { return a != null && _cast.ContainsValue(a); }

        // a person of the scene by its word (its template id or its tag): the one taken already, else the nearest loaded here (never the
        // animation stage's mannequin in its body: «stage up heid» stood nearer than Heid and the scene took it)
        internal static NpcActor Actor(string who, Vector3 near)
        {
            NpcActor a;
            if (_cast.TryGetValue(who, out a) && a != null) return a;
            NpcActor best = null; float bd = Seen;
            foreach (NpcActor n in NpcActor.Live)
            {
                if (n == null || n.Def == null || (n.Def.Id != who && n.Tag != who) || n.Tag == AnimStage.TagName) continue;
                float d = Vector3.Distance(n.transform.position, near);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        // a person of the scene on its way (npc go) is watched in Walking: run here — its mind says (its walk turns into standing there when it arrives:
        // NpcMind.Arrived); run on another game — where it stands, by the same rule and a step more (what we see lags)
        // where a person walks to: you, or a place — a piece of a building to its edge, from where the person comes
        internal static float BeforeNear = 0.15f; // m off its spot a person «before» a thing counts as there
        private static bool Target(Player p, string what, Vector3 from, out Vector3 at)
        {
            if (what == "player") { at = p.transform.position; return true; }
            return StoryWhere.Approach(what, from, out at);
        }

        // where a thing is thrown («prop throw <place|player|npc.x>»): a word of place; body — at someone, not at a spot: it lands short of them
        // (StoryProps.ThrowShort: nobody catches it)
        private static bool ThrowAt(string what, out Vector3 to, out bool body)
        {
            body = what == "player" || what == "bird" || (what.StartsWith("npc.") && !StoryWhere.IsThing(what));
            return StoryWhere.Find(what, out to);
        }

        // a door of the story's places opened or shut — its state in the world (the door plays it on every game)
        private static void Door(string place, bool open)
        {
            int done = 0;
            foreach (ZNetView nv in StoryWhere.Pieces(place)) // (every piece of that name: a gate's two halves)
            {
                Door d = nv.GetComponentInChildren<Door>();
                if (d == null || !nv.IsValid()) continue;
                if (open && LocState.Locked(d) != null) Note("door: «" + place + "» ще замкнені (do gate <місце> open перед цим) — відчиняться на мить і зачиняться знову");
                if (!nv.IsOwner()) nv.ClaimOwnership();
                nv.GetZDO().Set(ZDOVars.s_state, open ? LocState.Inward(nv) : 0); // (open: into its building)
                done++;
            }
            if (done == 0) Note("door: нема дверей «" + place + "» поруч — крок пропущено");
        }

        // the person a verb speaks of: taken for the scene (one scene a person), said in the log and the scene stopped when it is not there
        // (never half a scene without its person: again when it is near — TryPlay waits for the cast)
        private delegate void PersonVerb(Player p, Step st, string[] w, NpcActor a);
        private static Action<Player, Step> OnPerson(PersonVerb verb, bool goneIsFine = false)
        {
            return (p, st) =>
            {
                string[] w = st.W;
                string who = w[1];
                NpcActor a = Actor(who, p.transform.position);
                if (a == null)
                {
                    if (!goneIsFine) Stop("«" + who + "» зник зі сцени");
                    return;
                }
                if (!_cast.ContainsKey(who))
                {
                    if (HeldByOther(a)) { Stop("«" + who + "» зараз у сцені іншого гравця"); return; } // (one scene a person: this one again later)
                    Lock(a, true);
                    _ledWhenTaken[a] = StoryActions.LeadNow(a); // (the «follow» this scene cuts across: its end lets that one go, not one given meanwhile)
                }
                _cast[who] = a;
                verb(p, st, w, a);
            };
        }

        private static StepKind NpcKind()
        {
            PersonVerb stay = (p, st, w, a) => a.Order("stay", NpcMind.Layer.Scene);
            return new StepKind(StepSeen.World) { Min = 2, VerbAt = 2, Usage = "npc <хто> take|go|face|say|emote|sit|stay|free|spawn|fade|prop|clip|mark …", Verbs = new Dictionary<string, StepVerb>
            {
                { "take", new StepVerb(StepSeen.World) { Begin = OnPerson(stay) } },
                { "stay", new StepVerb(StepSeen.World) { Begin = OnPerson(stay) } },
                { "free", new StepVerb(StepSeen.World) { NotCast = true, Begin = OnPerson((p, st, w, a) => { StoryHold.Clear(a.Id); Free(w[1], a); }, true) } }, // («вільно»: its standing word too; gone already: nothing to let go)
                { "spawn", new StepVerb(StepSeen.World) { Min = 1, NotCast = true, PlaceAt = 3,
                    Begin = (p, st) =>
                    {
                        string[] w = st.W;
                        int ai = Array.IndexOf(w, "at"), bi = Array.IndexOf(w, "behind");
                        bool whole = Array.IndexOf(w, "whole") > 3; // («whole»: there at once, never see-through — made behind a black screen, or out of sight)
                        if (ai > 3 && w.Length > ai + 3) SpawnAt(p, w[1], w[3], new Vector3(F(w[ai + 1], 0f), F(w[ai + 2], 0f), F(w[ai + 3], 0f)), whole);
                        else if (bi > 3 && w.Length > bi + 1) SpawnBehind(p, w[1], w[3], F(w[bi + 1], 24f), whole);
                        else Spawn(p, w[1], w[3], whole);
                    } } },
                { "ready", new StepVerb(StepSeen.World) { Min = 1, NotCast = true, PlaceAt = 3, Usage = "npc <хто> ready <місце> [as <шаблон>]   (зроблена заздалегідь і невидима: сокіл, що стає нею, лише ставить і показує її — без завмирання кадру)",
                    Begin = (p, st) =>
                    {
                        string[] w = st.W;
                        Vector3 at;
                        if (_ready.ContainsKey(w[1]) || _cast.ContainsKey(w[1])) return; // (made already)
                        if (!StoryWhere.Find(w[3], out at)) { Note("npc ready: нема місця «" + w[3] + "» — крок пропущено (сокіл зробить її в мить підміни)"); return; }
                        int asAt = Array.IndexOf(w, "as");
                        NpcActor a = Made(p, w[1], at, -1f, asAt > 3 && w.Length > asAt + 1 ? w[asAt + 1] : null, true, float.NaN);
                        NpcVeil veil = a != null ? a.GetComponent<NpcVeil>() : null;
                        if (veil == null) return;
                        veil.Held = true;
                        _cast.Remove(w[1]); _ready[w[1]] = a; // (not in the scene yet: no step speaks to it, no shot finds it, till a falcon turns into it)
                    } } },
                { "prop", new StepVerb(StepSeen.World) { Min = 1, PlaceAfter = "from", PlaceOf = w => StoryProps.PlaceOf(w, 3), // (the piece after «from»: the word storycheck reads here; the game asks PlaceOf — where a thing is thrown as well)
                    Check = st => { string why = st.W[3] == "fetch" || st.W[3] == "carry" ? null : StoryProps.Check(st.W, 3); return why != null ? "npc <хто> " + why + (why == StoryProps.Usage ? " · prop fetch · prop carry" : "") : null; },
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        if (w[3] == "put") { StoryProps.PutDown(a); _waitFor = Time.time + 0.6f; return; } // (a scene's thing back; its own thing set down where it stands)
                        if (w[3] == "drop") { _waitFor = Time.time + Mathf.Max(0.6f, StoryProps.Drop(a)); return; } // (what it holds — its own thing, or one the scene gave it — falls from where it is and lies there)
                        if (w[3] == "throw")
                        {
                            Vector3 to; bool body;
                            if (!ThrowAt(w[4], out to, out body)) { Note("npc prop throw: нема місця «" + w[4] + "» — крок пропущено"); return; }
                            _waitFor = Time.time + StoryProps.Throw(a, to, body); // (the step waits its flight)
                            return;
                        }
                        if (w[3] == "fetch") { StoryProps.Fetch(a.Id); return; } // (its own thing the story had it set down: it may go back for it)
                        if (w[3] == "carry") // (its own thing, lying where it set it down, onto its arm now: it glides up from the ground — the scene plays the «up» clip and waits its mark before this)
                        {
                            StoryProps.Ask mine = StoryProps.CarryOf(a);
                            if (mine == null) Note("npc prop carry: у «" + a.Id + "» нема своєї речі (рядок carry: її файлу) — крок пропущено");
                            else if (!StoryProps.InHand(a)) StoryProps.PickUp(a, mine, false);
                            _waitFor = Time.time + 0.6f;
                            return;
                        }
                        string why;
                        StoryProps.Ask ask = w[3] == "take" ? StoryProps.Read(w, 4, out why) : null;
                        if (ask == null) { Note("npc prop: " + StoryProps.Usage); return; }
                        why = StoryProps.Take(a, ask);
                        if (why != null) Note("npc prop: " + why);
                        _waitFor = Time.time + 0.6f;
                    }) } },
                { "go", new StepVerb(StepSeen.World) { Min = 1, PlaceAt = 3, Begin = OnPerson(NpcGo), Busy = (p, st) => Walking(),
                    Late = st => { if (_walker != null) { WalkerShort(); _walker.Order("stay", NpcMind.Layer.Scene); _walker = null; } },
                    Done = (p, st) => _walkSaid } },
                { "arrive", new StepVerb(StepSeen.World) { Usage = "npc <хто> arrive [секунд=12]   (чекає, поки людина дійде: хода, почата без чекання — falcon … become … stride go <місце>)",
                    Begin = OnPerson((p, st, w, a) => { _limit = Time.time + (w.Length > 3 ? F(w[3], 12f) : 12f); }),
                    Busy = (p, st) => { NpcActor a = Actor(st.W[1], p.transform.position); NpcMind.Plan plan = a != null && a.Owned && a.Mind != null ? a.Mind.Of(NpcMind.Layer.Scene) : null; return plan != null && plan.Kind == NpcMind.Task.Go; } } },
                { "face", new StepVerb(StepSeen.World) { Min = 1, PlaceAt = 3,
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        Vector3 at;
                        if (w[3] == "player") a.Order("look " + NpcKit.Ref(p.GetZDOID()) + " keep", NpcMind.Layer.Scene);
                        else if (StoryWhere.Find(w[3], out at)) a.Order("lookat " + V(at + Vector3.up * (StoryWhere.IsLow(w[3]) ? 0f : 1.2f)), NpcMind.Layer.Scene); // (a thing's word is the thing itself, where it is this moment, held or lying low: no height over it)
                        else Note("npc face: нема місця «" + w[3] + "»");
                    }) } },
                { "say", new StepVerb(StepSeen.World) { Min = 1,
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        string line = Lang.T(Rest(st.Text, 3));
                        _waitFor = Time.time + RavenSpeech.Seconds(line, RavenSpeech.Pace.Scene);
                        a.Say(line, p);
                        Spoken(a.ShownName, line); // (in a shot: as a subtitle too — the bubble is small in a wide picture)
                    }) } },
                // «says»: the same line, and the scene goes on while it is said (the others turn to it mid-line: Eyvind's «Сорок вісім…», the user
                // 2026-11-17); «wait said» waits till it is over
                { "says", new StepVerb(StepSeen.World) { Min = 1,
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        string line = Lang.T(Rest(st.Text, 3));
                        _saidUntil = Time.time + RavenSpeech.Seconds(line, RavenSpeech.Pace.Scene);
                        a.Say(line, p);
                        Spoken(a.ShownName, line);
                    }) } },
                { "emote", new StepVerb(StepSeen.World) { Min = 1, Begin = OnPerson((p, st, w, a) => a.Order("emote " + w[3] + (Array.IndexOf(w, "loop") > 3 ? " loop" : ""), NpcMind.Layer.Scene)) } },
                { "mark", new StepVerb(StepSeen.World) { Min = 1, Usage = "npc <хто> mark <мітка> [секунд]",
                    Begin = OnPerson((p, st, w, a) => { _markFrom = Time.time; _waitFor = Time.time + (w.Length > 4 ? F(w[4], 30f) : 30f); }),
                    Busy = (p, st) => NpcMarkBusy(p, st.W),
                    Done = (p, st) => MarkSaid(CharOf(st.W[1], p), st.W[3]) } },
                { "clip", new StepVerb(StepSeen.World) { Min = 1, Usage = "npc <хто> clip <кліп>|none [on npc.<x>|player [м]]", // (one of our clips: BodyClips — npc <who> clip <name>|none [seconds])
                    Check = st => st.W[3] != "none" && BodyClips.Find(st.W[3]) == null ? "npc <хто> clip: нема кліпу «" + st.W[3] + "»" : null,
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        // («… on npc.<x> [m]»: a clip of two — its hands go to where the other really stands: BodyAnimator.Mate; any other clip takes the mate off)
                        int on = Array.IndexOf(w, "on");
                        Character other = on > 3 && w.Length > on + 1 && w[on + 1].StartsWith("npc.") ? CharOf(w[on + 1].Substring(4), p) : on > 3 && w.Length > on + 1 && w[on + 1] == "player" ? p : null;
                        if (on > 3 && other == null) Note("npc clip … on: нема «" + (w.Length > on + 1 ? w[on + 1] : "") + "» — кліп грає без прив'язки до другого тіла");
                        a.Order("clip " + w[3] + (on < 0 && w.Length > 4 ? " " + w[4] : ""), NpcMind.Layer.Scene);
                        // (after the clip's own order — letting the last clip go takes its mate off —, written on the person where it is run: every game near sets its own picture by it)
                        ZNetView onNv = other != null ? other.GetComponent<ZNetView>() : null;
                        string mate = onNv != null && onNv.IsValid() ? NpcKit.Ref(onNv.GetZDO().m_uid) : "";
                        if (mate.Length > 0 || (a.Zdo != null && a.Zdo.GetString(NpcBody.KMate, "").Length > 0)) a.Act("mate " + (mate.Length > 0 ? mate + " " + F(on > 3 && w.Length > on + 2 ? F(w[on + 2], 0.8f) : 0.8f) : ""));
                    }),
                    Done = (p, st) => st.W[3] == "none" ? null : ClipOn(CharOf(st.W[1], p), st.W[3]) } },
                { "tool", new StepVerb(StepSeen.World) { Min = 1, Usage = "npc <хто> tool hand|belt|auto [now]   (своя річ людини — в руку, на пояс чи як сама хоче: NpcTool; now — без кліпу, одразу)",
                    Check = st => st.W[3] != "hand" && st.W[3] != "belt" && st.W[3] != "auto" ? "npc <хто> tool hand|belt|auto [now]" : null,
                    Begin = OnPerson((p, st, w, a) => a.Act("tool " + w[3] + (Array.IndexOf(w, "now") > 3 ? " now" : ""))) } }, // (the user 2026-11-15: Lit shows his hammer — «а це Лот», a close shot of it: «camera on npc.lit.thing»)
                { "sparks", new StepVerb(StepSeen.World) { Usage = "npc <хто> sparks [секунд=3.5]   (стоїть цілий, з нього здіймаються темно-жовті іскри, наприкінці зникає за ними: NpcSparks)",
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        float s = w.Length > 3 ? F(w[3], 3.5f) : 3.5f;
                        NpcSparks.Begin(a.gameObject, s, () => { if (a != null) NpcFade.Out(a, 0.05f, "-"); }); // (gone at once at their end, behind the last burst)
                        if (a.Zdo != null) SendAct(ZRoutedRpc.Everybody, "n:sparks", NpcKit.Ref(a.Zdo.m_uid) + " " + F(s));
                        _waitFor = Time.time + s;
                    }) } },
                { "fade", new StepVerb(StepSeen.World) {
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        float s = w.Length > 3 ? F(w[3], Fades.Person) : Fades.Person;
                        NpcFade.Out(a, s, w.Length > 4 ? w[4] : "-");
                        _waitFor = Time.time + s;
                    }) } },
                { "sit", new StepVerb(StepSeen.World) { Min = 1, PlaceAt = 3,
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        Vector3 at;
                        if (!Target(p, w[3], a.transform.position, out at)) { Note("npc sit: нема місця «" + w[3] + "» — крок пропущено"); return; }
                        a.Order("sit " + V(at), NpcMind.Layer.Scene);
                    }) } },
            } };
        }

        private static Character CharOf(string who, Player p) { NpcActor a = Actor(who, p.transform.position); return a != null ? a.GetComponent<Character>() : null; }

        private static void NpcGo(Player p, Step st, string[] w, NpcActor a)
        {
            Vector3 to;
            _walkSaid = null;
            int bi = Array.IndexOf(w, "before");
            Vector3 mid = Vector3.zero, outOf = Vector3.zero;
            bool square = bi > 0 && w.Length > bi + 1 && StoryWhere.Before(w[3], out mid, out outOf);
            if (square) to = mid + outOf * F(w[bi + 1], 0.6f); // (asked once: it looks through all the thing's parts)
            else if (!Target(p, w[3], a.transform.position, out to)) { Note("npc go: нема місця «" + w[3] + "» — крок пропущено"); return; }
            int ni = Array.IndexOf(w, "near");
            float near = square ? BeforeNear : ni >= 0 && w.Length > ni + 1 ? F(w[ni + 1], 1.6f) : 1.6f;
            bool run = Array.IndexOf(w, "walk") < 0 && (Array.IndexOf(w, "run") > 0 || Utils.DistanceXZ(a.transform.position, to) > RunFrom); // (far off: it hurries — the user 2026-11-01; «walk»: never — one who has nowhere to hurry, Ketil 2026-11-14)
            a.Order("go " + V(to) + (run ? " run" : "") + " near " + F(near) + (square ? " exact" : ""), NpcMind.Layer.Scene); // (it stops that far off: its own Near; exact: no slack, a clip follows)
            if (Array.IndexOf(w, "nowait") > 3) return; // (the scene goes on as it walks — it talks, the camera turns away; «npc <who> arrive» waits for it later)
            _walker = a; _walkTo = to; _walkNear = square ? near : NpcMind.Reach(near);
            _walkFrom = a.transform.position; _walkBegan = Time.time; _walkRanAny = false; _walkWalkFrom = -1f;
            float way = Utils.DistanceXZ(a.transform.position, to);
            _limit = Time.time + way / (run ? 3.5f : 1.3f) + 8f; // (as long as the walk takes, and some: stuck — the scene goes on)
        }

        // a person made for this scene at a place, facing you (not kept in the world: gone with the scene)
        // always the same spot of a place, wherever the player is: its blueprint's frame, set on the floor there
        private static void SpawnAt(Player p, string who, string place, Vector3 local, bool whole = false)
        {
            Loc l = Locs.Get(place);
            Vector3 at;
            if (l == null || !l.ToWorld(local, out at)) { Note("npc spawn … at: нема місця «" + place + "» — крок пропущено"); return; }
            RaycastHit hit;
            if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out hit, 4f, LayerMask.GetMask("piece", "Default", "static_solid", "terrain"), QueryTriggerInteraction.Ignore)) at.y = hit.point.y;
            Made(p, who, at, -1f, null, whole);
        }

        // «spawn <place> behind <m>»: that far behind the place as the camera looks — out of the picture, at your back — on dry ground and not inside a
        // trunk, a rock or a wall: the first clear of a few ways round behind (BehindArc° apart), then nearer. None clear: skipped, a note
        internal static float BehindArc = 25f;
        private static void SpawnBehind(Player p, string who, string place, float m, bool whole)
        {
            Vector3 from;
            if (!StoryWhere.Find(place, out from) || ZoneSystem.instance == null) { Note("npc spawn … behind: нема місця «" + place + "» — крок пропущено"); return; }
            Camera cam = Utils.GetMainCamera();
            Vector3 back = -(cam != null ? cam.transform.forward : p.transform.forward); back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = -p.transform.forward;
            back.Normalize();
            int mask = LayerMask.GetMask("Default", "static_solid", "piece");
            foreach (float k in new[] { 1f, 0.7f, 0.45f })
                foreach (float deg in new[] { 0f, BehindArc, -BehindArc, 2f * BehindArc, -2f * BehindArc })
                {
                    Vector3 at = from + Quaternion.Euler(0f, deg, 0f) * back * (m * k);
                    float ground;
                    if (!ZoneSystem.instance.GetGroundHeight(at, out ground) || ground < ZoneSystem.instance.m_waterLevel + 0.3f) continue; // (its land not made here, or water)
                    at.y = ground;
                    if (Physics.CheckCapsule(at + Vector3.up * 0.5f, at + Vector3.up * 1.7f, 0.35f, mask, QueryTriggerInteraction.Ignore)) continue; // heavy: at most 15 checks, once the step (a trunk, a rock, a wall where it would stand)
                    Made(p, who, at, -1f, null, whole);
                    return;
                }
            Note("npc spawn … behind: позаду «" + place + "» за " + m.ToString("0") + " м ніде стати — крок пропущено");
        }

        private static void Spawn(Player p, string who, string place, bool whole = false)
        {
            Vector3 at;
            if (!StoryWhere.Approach(place, p.transform.position, out at)) { Note("npc spawn: нема місця «" + place + "» — крок пропущено"); return; } // (beside a named piece, on your side: never in a bed or a chair)
            Made(p, who, at, -1f, null, whole);
        }

        // template: its template when the scene calls it by another name (two sisters of one template: «falcon c become sestra_3 as sestra_2»);
        // whole: there at once, never see-through (a falcon's change: its feathers hide the moment); yaw: the way it faces (none: you)
        private static readonly Dictionary<string, NpcActor> _ready = new Dictionary<string, NpcActor>(); // session: this scene's people made beforehand, unseen («npc <who> ready»)
        private static NpcActor Made(Player p, string who, Vector3 at, float comeIn = -1f, string template = null, bool whole = false, float yaw = float.NaN)
        {
            Vector3 look = p.transform.position - at; look.y = 0f;
            Quaternion turn = !float.IsNaN(yaw) ? Quaternion.Euler(0f, yaw, 0f) : look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
            NpcActor waiting;
            if (_ready.TryGetValue(who, out waiting))
            {
                // (made beforehand: set there, turned, shown — nothing is made in this frame)
                _ready.Remove(who);
                if (waiting != null)
                {
                    waiting.Order("tp " + V(at), NpcMind.Layer.Scene);
                    waiting.transform.position = at; waiting.transform.rotation = turn;
                    Rigidbody rb = waiting.GetComponent<Rigidbody>();
                    if (rb != null) { rb.position = at; rb.rotation = turn; rb.velocity = Vector3.zero; }
                    NpcVeil veil = waiting.GetComponent<NpcVeil>();
                    if (veil != null) veil.Release();
                    _cast[who] = waiting;
                    Lock(waiting, true); // (its lock fresh the moment it is in the scene)
                    waiting.Order("stay", NpcMind.Layer.Scene);
                    return waiting;
                }
            }
            NpcActor a = Npcs.Spawn(template ?? who, at, turn, p.GetPlayerID(), MadeTag);
            if (a == null) { Note("npc spawn: «" + (template ?? who) + "» не вийшло зробити (нема шаблону?) — крок пропущено"); return null; }
            ZDO z = a.Zdo;
            if (z != null) z.Persistent = false; // (left behind by a crash: gone with the owner's session)
            _made.Add(a);
            _cast[who] = a;
            Lock(a, true);
            a.Order("stay", NpcMind.Layer.Scene);
            if (!whole) NpcFade.In(a, comeIn > 0f ? comeIn : SpawnIn); // (it comes in slowly, never just stands there; out of a falcon: whole at once, behind its feathers)
            else a.gameObject.AddComponent<NpcVeil>(); // (whole means dressed: its bare body is not drawn for the frames before the game has put its clothes on)
            return a;
        }
        internal static float SpawnIn = 2.5f;
        internal const string MadeTag = "scene"; // (the tag of one made for a scene: never one of the world's people — StoryPeople.Of)

        // a thing left on the ground at a place (the echo's charm): a real item of the world, anyone may pick it up
        private static void Drop(Player p, string prefabName, string place)
        {
            Vector3 at;
            if (place == "player") at = p.transform.position + p.transform.forward * 1.2f;
            else if (!StoryWhere.Find(place, out at)) { Note("drop: нема місця «" + place + "» — крок пропущено"); return; }
            if (!StoryFound.Leave(prefabName, at)) Note("drop: нема предмета «" + prefabName + "» — крок пропущено"); // (the one way a thing of the world is laid: the quest's «lay» too)
        }

        private static void Free(string who, NpcActor a)
        {
            long led;
            if (a != null) LetGo(a, _ledWhenTaken.TryGetValue(a, out led) ? led : 0L);
            if (a != null) _ledWhenTaken.Remove(a);
            _cast.Remove(who);
            if (_walker == a) _walker = null;
        }

        // all a scene holds a person by, off: «free» takes its clip too
        // its scene over: what it held let go — and the story's «follow» it cut across (whom it followed when the scene took it; its lock run out
        // with nobody to say: the lock's player). A «follow» given meanwhile, by another player's stage, is his: the scene does not end it
        private static void LetGo(NpcActor a, long scenePlayer)
        {
            StoryProps.Put(a); // (its prop back too)
            a.Order("look none", NpcMind.Layer.Scene);
            a.Order("free", NpcMind.Layer.Scene);
            StoryActions.LeadOff(a, scenePlayer);
            Lock(a, false);
        }

        // the scene over or stopped: everyone taken back to its own day, where it is
        private static void FreeCast()
        {
            foreach (KeyValuePair<string, NpcActor> kv in new List<KeyValuePair<string, NpcActor>>(_cast)) Free(kv.Key, kv.Value);
            foreach (NpcActor a in _made) if (a != null) NpcFade.Out(a, Fades.Person, "-"); // (made for the scene: fades away with it — smooth by default; one faded already is gone already)
            _made.Clear(); _ready.Clear();
            _cast.Clear(); _ledWhenTaken.Clear(); _walker = null;
        }
    }
}
#endif
