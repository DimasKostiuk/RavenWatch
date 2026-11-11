#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
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
            NpcActor.Acts["scenelock"] = (a, w) => { ZDO z = a.Zdo; if (z != null) z.Set(KScene, w.Length > 1 ? w[1] : ""); }; // (where it is run: its owner writes)
            NpcMind.SceneWatch = a =>
            {
                ZDO z = a != null ? a.Zdo : null;
                if (z == null || Holder(a) != 0L) return;
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
                        int ai = Array.IndexOf(w, "at");
                        if (ai > 3 && w.Length > ai + 3) SpawnAt(p, w[1], w[3], new Vector3(F(w[ai + 1], 0f), F(w[ai + 2], 0f), F(w[ai + 3], 0f))); else Spawn(p, w[1], w[3]);
                    } } },
                { "prop", new StepVerb(StepSeen.World) { Min = 1, PlaceAfter = "from",
                    Check = st => { string pw; return st.W[3] != "put" && st.W[3] != "fetch" && (st.W[3] != "take" || StoryProps.Read(st.W, 4, out pw) == null) ? "npc <хто> " + StoryProps.Usage + " · prop fetch" : null; },
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        if (w[3] == "put") { StoryProps.PutDown(a); _waitFor = Time.time + 0.6f; return; } // (a scene's thing back; its own thing set down where it stands)
                        if (w[3] == "fetch") { StoryProps.Fetch(a.Id); return; } // (its own thing the story had it set down: it may go back for it)
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
                { "face", new StepVerb(StepSeen.World) { Min = 1, PlaceAt = 3,
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        Vector3 at;
                        if (w[3] == "player") a.Order("look " + NpcKit.Ref(p.GetZDOID()) + " keep", NpcMind.Layer.Scene);
                        else if (StoryWhere.Find(w[3], out at)) a.Order("lookat " + V(at + Vector3.up * 1.2f), NpcMind.Layer.Scene);
                        else Note("npc face: нема місця «" + w[3] + "»");
                    }) } },
                { "say", new StepVerb(StepSeen.World) { Min = 1,
                    Begin = OnPerson((p, st, w, a) =>
                    {
                        string line = Lang.T(Rest(st.Text, 3));
                        _waitFor = Time.time + RavenSpeech.Seconds(line, RavenSpeech.Pace.Scene);
                        a.Say(line, p);
                        Spoken(a.Name, line); // (in a shot: as a subtitle too — the bubble is small in a wide picture)
                    }) } },
                { "emote", new StepVerb(StepSeen.World) { Min = 1, Begin = OnPerson((p, st, w, a) => a.Order("emote " + w[3] + (Array.IndexOf(w, "loop") > 3 ? " loop" : ""), NpcMind.Layer.Scene)) } },
                { "mark", new StepVerb(StepSeen.World) { Min = 1, Usage = "npc <хто> mark <мітка> [секунд]",
                    Begin = OnPerson((p, st, w, a) => { _markFrom = Time.time; _waitFor = Time.time + (w.Length > 4 ? F(w[4], 30f) : 30f); }),
                    Busy = (p, st) => NpcMarkBusy(p, st.W),
                    Done = (p, st) => MarkSaid(CharOf(st.W[1], p), st.W[3]) } },
                { "clip", new StepVerb(StepSeen.World) { Min = 1, Usage = "npc <хто> clip <кліп>|none", // (one of our clips: BodyClips — npc <who> clip <name>|none [seconds])
                    Check = st => st.W[3] != "none" && BodyClips.Find(st.W[3]) == null ? "npc <хто> clip: нема кліпу «" + st.W[3] + "»" : null,
                    Begin = OnPerson((p, st, w, a) => a.Order("clip " + w[3] + (w.Length > 4 ? " " + w[4] : ""), NpcMind.Layer.Scene)),
                    Done = (p, st) => st.W[3] == "none" ? null : ClipOn(CharOf(st.W[1], p), st.W[3]) } },
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
            bool run = Array.IndexOf(w, "run") > 0 || Utils.DistanceXZ(a.transform.position, to) > RunFrom; // (far off: it hurries — the user 2026-11-01)
            a.Order("go " + V(to) + (run ? " run" : "") + " near " + F(near) + (square ? " exact" : ""), NpcMind.Layer.Scene); // (it stops that far off: its own Near; exact: no slack, a clip follows)
            _walker = a; _walkTo = to; _walkNear = square ? near : NpcMind.Reach(near);
            _walkFrom = a.transform.position; _walkBegan = Time.time; _walkRanAny = false; _walkWalkFrom = -1f;
            float way = Utils.DistanceXZ(a.transform.position, to);
            _limit = Time.time + way / (run ? 3.5f : 1.3f) + 8f; // (as long as the walk takes, and some: stuck — the scene goes on)
        }

        // a person made for this scene at a place, facing you (not kept in the world: gone with the scene)
        // always the same spot of a place, wherever the player is: its blueprint's frame, set on the floor there
        private static void SpawnAt(Player p, string who, string place, Vector3 local)
        {
            Loc l = Locs.Get(place);
            Vector3 at;
            if (l == null || !l.ToWorld(local, out at)) { Note("npc spawn … at: нема місця «" + place + "» — крок пропущено"); return; }
            RaycastHit hit;
            if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out hit, 4f, LayerMask.GetMask("piece", "Default", "static_solid", "terrain"), QueryTriggerInteraction.Ignore)) at.y = hit.point.y;
            Made(p, who, at);
        }

        private static void Spawn(Player p, string who, string place)
        {
            Vector3 at;
            if (!StoryWhere.Approach(place, p.transform.position, out at)) { Note("npc spawn: нема місця «" + place + "» — крок пропущено"); return; } // (beside a named piece, on your side: never in a bed or a chair)
            Made(p, who, at);
        }

        private static void Made(Player p, string who, Vector3 at)
        {
            Vector3 look = p.transform.position - at; look.y = 0f;
            NpcActor a = Npcs.Spawn(who, at, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity, p.GetPlayerID(), "scene");
            if (a == null) { Note("npc spawn: «" + who + "» не вийшло зробити (нема шаблону?) — крок пропущено"); return; }
            ZDO z = a.Zdo;
            if (z != null) z.Persistent = false; // (left behind by a crash: gone with the owner's session)
            _made.Add(a);
            _cast[who] = a;
            Lock(a, true);
            a.Order("stay", NpcMind.Layer.Scene);
            NpcFade.In(a, SpawnIn); // (it comes in slowly, never just stands there)
        }
        internal static float SpawnIn = 2.5f;

        // a thing left on the ground at a place (the echo's charm): a real item of the world, anyone may pick it up
        private static void Drop(Player p, string prefabName, string place)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (d == null) { Note("drop: нема предмета «" + prefabName + "» — крок пропущено"); return; }
            Vector3 at;
            if (place == "player") at = p.transform.position + p.transform.forward * 1.2f;
            else if (!StoryWhere.Find(place, out at)) { Note("drop: нема місця «" + place + "» — крок пропущено"); return; }
            ItemDrop.ItemData item = d.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = 1;
            ItemDrop.DropItem(item, 1, at + Vector3.up * 0.4f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
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
            _made.Clear();
            _cast.Clear(); _ledWhenTaken.Clear(); _walker = null;
        }
    }
}
#endif
