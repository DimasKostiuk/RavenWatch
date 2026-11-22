#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A place's own raven (the user 2026-11-17: the Hall's postman, «а можливо у нього буде серйозніша роль»): from when its conditions hold
    // it lives in the world, at home in its place. Nobody's, no item, never a word: it caws now and then. Its place's lines (locations.list):
    //   raven: hall vohnyshche reached ch2 lit_day   its look (assets/story/looks: the Hall's brown, with brown sparks behind it in the
    //                                                air: StoryLooks «sparks:»), the named piece whose ZDO keeps its word, the conditions
    //   ravenroof: -3 10.5 3                         the point under its roof (the blueprint's frame) it goes out and comes in through: from
    //                                                there straight up through the roof (StoryBird.Through: a couple of seconds the motor
    //                                                neither swerves nor stops — the user 2026-11-17: «з низу це не буде видно»), over it off
    //                                                into the sky; the same way back. The roof over it is measured each time (one ray down): the
    //                                                Hall's roof grows whole as Lit mends it, the way stays the same
    //   ravensits: piece_bench01 piece_table …       the pieces it sits on besides the perches: their tops, level, room over them, within
    //                                                the walls, open to the middle under the roof (Reachable: no top inside a wall, no beam
    //                                                under the eaves outside); none its place's state has not now (LocState.Absent; a perch: PerchView.Gone)
    // What it does: sits a while (SitMin..SitMax s), then flies to another seat, now and then by way of the middle under the roof (a turn
    // round the Hall): a free perch first while there is one (none a player's raven sits on or flies to — one coming later sends it off —
    // nor a person's: NpcRaven), else a piece; now and then (HeadShare) onto the head of someone standing in the place, a player or a story
    // person, for HeadMin..HeadMax s (never while a scene plays near, nor on one a scene holds). Never on a piece someone sits or stands
    // on (a person on the bench: Occupied); one sitting down where it sits sends it off. No caw while a scene plays near.
    // A seat it never gets to (StoryBird's Stuck): once more from the middle, then a puff and it sits there — and that seat is not chosen again
    // on this game (Bad, till its place's state changes); a point in the air it never gets to: on to its next leg. Lent to a scene and back:
    // StoryBird.ForScene / ForPlace.
    // One mind: the game that owns the home piece's ZDO (a scene that takes it out or gives it back takes the mind too: MarkOut, Back) decides and writes the word there — KState «seat x y z yaw [hub]» · «head p <player
    // id>» · «head n <person>» · «in» · «out <world time>», KSeq — and every game within DrawFar m of the home piece draws its own picture
    // from it (StoryBird), a new picture sitting where the word says (it was there before you came). Nobody near: nothing runs or is drawn.
    // Out on the story's errand (a scene's «bird come … from <place>», the letter: StoryScenesBird): where it is drawn, the scene takes its
    // very picture (TakeOut) — to one outside the walls up through the roof first (StoryBird.Via); where it is not, the scene's own bird of
    // its look comes from the place's side, and a picture far from the scene goes out through its roof (Away); the mind writes «out» for
    // OutMost s at most. The scene's «bird home <place>» — or its end, or its stop — gives it back (Back): its picture near, it comes in
    // through the roof; else it comes in once the mind writes «in».
    // Cost: nothing with no such place near; near, its word read ReadEvery s, the mind once a second, its seats looked over only when the
    // place's state or its loaded pieces change (heavy: its kinds of pieces only, a few rays each).
    internal static class LocRaven
    {
        internal static float DrawFar = 60f, ReadEvery = 0.25f, SitMin = 25f, SitMax = 70f, HeadShare = 0.1f, HeadMin = 10f, HeadMax = 22f;
        internal static float HubShare = 0.3f, InSeconds = 14f, OutMost = 480f, SkyFrom = 35f, SkyUp = 18f, AboveRoof = 1.6f, OpenUp = 5f;
        // (OutMost: longer than a letter may lie open, StoryScenes.LetterMost 300 s, and the flights: never «in» while a scene still has it)
        internal static float LegMost = 40f; // s a hover or a pass may take before the next leg goes anyway (a bird that hangs short of its point)
        internal static float SeatsAgain = 5f; // s: its seats looked over again at most this often while only the loaded pieces' count changes
        internal static float CawMin = 40f, CawMax = 130f, HeadUp = 0.17f, SeatRoom = 0.7f, SeatLevel = 25f;
        internal static int Level = 4; // its size: as a raven of this trust level on a shoulder
        internal static bool Trace;    // (the author: its words and flights in the log — raven story placeraven; where it is: raven story npcravens)
        // (the author's run for the flight's numbers — raven story placeraven hurry: it sits only a few seconds between its flights, so the log
        // fills with «Flight to a seat …» lines in minutes; this game's mind only, nothing kept)
        internal static bool Hurry;
        internal static float HurryMin = 3f, HurryMax = 6f;

        private static readonly int KState = "RavenWatch_placeRaven".GetStableHashCode();
        private static readonly int KSeq = "RavenWatch_placeRavenSeq".GetStableHashCode();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static void Log(string s) { if (Trace) Story.Log("place raven: " + s); }
        private static string F(float v) { return v.ToString("0.###", Inv); }

        private sealed class Seat { internal Vector3 At; internal float Yaw; internal PerchView Perch; internal int I = -1; internal ZNetView Piece; }
        private enum LegKind { Hover, Through, Land, Head, Leave }
        private struct Leg { internal LegKind Kind; internal Vector3 At; internal float Yaw; internal Character Body; }

        // one a place that has a raven, on this game
        private sealed class R
        {
            internal Loc L;
            internal ZNetView Home;
            internal StoryBird Bird;
            internal int SeqSeen = -1;
            internal bool Drawn, WasOwner;                          // (its picture drawn on this game now · this game owned its home piece at the last read)
            internal string Word = "";
            internal float NextRead, NextMind, NextDecide, InSince = -1f, NextCaw, HeadUntil;
            internal readonly Queue<Leg> Plan = new Queue<Leg>();
            internal bool LegGoing; internal LegKind LegNow; internal Leg LegLast; internal float LegAt;
            internal int Stuck;
            internal Seat Now;                                     // (the mind's: the seat it wrote, session — a new owner finds it from the word)
            internal List<Seat> Seats = new List<Seat>();
            internal int SeatsVersion = -1, SeatsPieces = -1; internal float SeatsAt = -100f;
            internal readonly List<Vector3> Bad = new List<Vector3>(); // (session: seats it never got to twice — not chosen again here, till its place's state changes)
        }
        private static bool IsBad(R r, Vector3 seat) { foreach (Vector3 b in r.Bad) if (Vector3.Distance(b, seat) < 0.3f) return true; return false; } // (as Nearest: the word's numbers are rounded)
        private static readonly Dictionary<string, R> _of = new Dictionary<string, R>();

        // ---------------------------------------------------------------- the clock (LocModules: each frame; its own pace inside)
        internal static void Tick()
        {
            Player me = Player.m_localPlayer;
            int have = 0;
            foreach (Loc l in Locs.All)
            {
                if (l.RavenLook.Length == 0) continue;
                have++;
                R r;
                if (!_of.TryGetValue(l.Id, out r)) _of[l.Id] = r = new R { L = l };
                r.L = l; // (the list read again: its fresh copy)
                One(r, me);
            }
            if (_of.Count > have) Drop(); // (a place's «raven:» line gone — the author's list read again: its bird with it)
        }

        private static void Drop()
        {
            List<string> gone = new List<string>();
            foreach (KeyValuePair<string, R> e in _of) { Loc l = Locs.Get(e.Key); if (l == null || l.RavenLook.Length == 0) gone.Add(e.Key); }
            foreach (string id in gone) { Hide(_of[id]); _of.Remove(id); }
        }

        private static void One(R r, Player me)
        {
            if (me == null || ZNet.instance == null) { Hide(r); return; }
            if (!ReferenceEquals(r.Bird, null) && r.Bird == null) { r.Bird = null; r.Plan.Clear(); r.LegGoing = false; r.SeqSeen = -1; } // (its picture gone by itself — a puff, a seat never reached: drawn again from its word)
            if (Time.time >= r.NextRead)
            {
                r.NextRead = Time.time + ReadEvery;
                ZNetView home = HomeOf(r.L);
                if (home == null || !StoryCond.Holds(me, r.L.RavenWhen, Story.Log)) { Hide(r); return; }
                ZDO z = home.GetZDO();
                // the mind: on the game that owns the home piece, while anyone is near it — its owner may stand farther off than any
                // picture is drawn (the game keeps a piece's owner while its zones are that player's), and it must decide for those near
                bool owner = home.IsOwner();
                if (owner && !r.WasOwner) { r.Now = null; r.NextDecide = Time.time + Random.Range(SitMin * 0.5f, SitMin); r.HeadUntil = Time.time + HeadMin; } // (owner now: what it sits on stays a while, on a head too — no hop for all at once; its seat read from the word, not one kept from an owning before)
                r.WasOwner = owner;
                if (owner && Time.time >= r.NextMind && AnyoneNear(home.transform.position)) { r.NextMind = Time.time + 1f; r.Home = home; Mind(r, z); }
                if (Vector3.Distance(me.transform.position, home.transform.position) > DrawFar + (r.Bird != null ? 10f : 0f)) { HidePicture(r); r.Home = home; return; } // (its picture only near you)
                r.Home = home; r.Drawn = true;
                int seq = z.GetInt(KSeq, 0);
                if (seq != r.SeqSeen) { r.SeqSeen = seq; r.Word = z.GetString(KState, ""); Apply(r); }
            }
            if (r.Home == null || !r.Drawn) return;
            Fly(r);
            Caw(r);
        }

        private static ZNetView HomeOf(Loc l)
        {
            List<ZNetView> named = LocParts.Named(l.Id, l.RavenHome);
            foreach (ZNetView nv in named) if (nv != null && nv.IsValid()) return nv;
            return null;
        }

        private static void Hide(R r) { HidePicture(r); r.Home = null; r.WasOwner = false; }

        // its picture off here (you went off; the mind may still run on this game)
        private static void HidePicture(R r)
        {
            if (r.Bird != null) Object.Destroy(r.Bird.gameObject);
            r.Bird = null; r.Plan.Clear(); r.LegGoing = false; r.SeqSeen = -1; r.Word = ""; r.Stuck = 0; r.Drawn = false;
        }

        // a player within DrawFar of the home piece: its mind has someone to decide for
        private static bool AnyoneNear(Vector3 at)
        {
            foreach (Player p in Player.GetAllPlayers()) if (p != null && Vector3.Distance(p.transform.position, at) <= DrawFar) return true;
            return false;
        }

        // ---------------------------------------------------------------- the way out and in: under the roof, over it, the sky
        private static Vector3 Hub(R r)
        {
            Vector3 at;
            if (r.L.HasRavenRoof && r.L.ToWorld(r.L.RavenRoof, out at)) return at;
            return r.Home.transform.position + Vector3.up * 4f; // (no «ravenroof:»: over its home piece — storycheck asks for the line)
        }

        // over the roof right above the hub: the roof's top measured now (one ray down), open sky — a few metres up
        private static Vector3 Above(R r, Vector3 hub)
        {
            RaycastHit hit;
            if (Physics.Raycast(hub + Vector3.up * 30f, Vector3.down, out hit, 30f - 0.3f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) return hit.point + Vector3.up * AboveRoof; // heavy: one ray, on its way out or in
            return hub + Vector3.up * OpenUp;
        }

        private static Vector3 Sky(Vector3 above)
        {
            Vector3 d = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            return above + d * SkyFrom + Vector3.up * SkyUp;
        }

        private static void PlanOut(R r)
        {
            Vector3 hub = Hub(r), above = Above(r, hub), at = r.Bird.transform.position;
            r.Plan.Clear(); r.LegGoing = false;
            if (r.L.Holds(at) && at.y < above.y - AboveRoof) // (under its roof: up through it; over it or out in the sky already — straight off)
            {
                r.Plan.Enqueue(new Leg { Kind = LegKind.Hover, At = hub });
                r.Plan.Enqueue(new Leg { Kind = LegKind.Through, At = above });
            }
            r.Plan.Enqueue(new Leg { Kind = LegKind.Leave, At = Sky(above) });
            Log(r.L.Id + ": out through the roof (" + F(above.y - hub.y) + " m up from under it)");
        }

        // in from where it is: over the roof first unless it is under it already
        private static void PlanIn(R r)
        {
            Vector3 hub = Hub(r), above = Above(r, hub);
            r.Plan.Clear(); r.LegGoing = false;
            Vector3 at = r.Bird.transform.position;
            if (!(r.L.Holds(at) && at.y < above.y - AboveRoof))
            {
                r.Plan.Enqueue(new Leg { Kind = LegKind.Hover, At = above });
                r.Plan.Enqueue(new Leg { Kind = LegKind.Through, At = hub });
            }
            else r.Plan.Enqueue(new Leg { Kind = LegKind.Hover, At = hub });
            Log(r.L.Id + ": in through the roof");
        }

        private static bool Entering(R r) { foreach (Leg g in r.Plan) if (g.Kind == LegKind.Through) return true; return r.LegGoing && r.LegNow == LegKind.Through; }

        // ---------------------------------------------------------------- its picture (every game near): from the word
        private static void Apply(R r)
        {
            string[] w = r.Word.Split(' ');
            string mode = r.Word.Length > 0 ? w[0] : "";
            Log(r.L.Id + ": «" + r.Word + "»" + (r.Bird != null ? " (" + StoryBird.StateName(r.Bird.State) + ")" : " (not drawn)"));
            if (mode == "out") { if (r.Bird != null && !Leaving(r)) PlanOut(r); return; }
            if (mode == "in")
            {
                if (r.Bird == null) { if (!Make(r, Sky(Above(r, Hub(r))), 0f)) return; PlanIn(r); }
                else if (Leaving(r) || !Entering(r) && !r.L.Holds(r.Bird.transform.position)) PlanIn(r);
                return;
            }
            Seat s; Character body;
            if (mode == "seat" && ParseSeat(w, out s))
            {
                if (r.Bird == null) { Make(r, s.At, s.Yaw); return; } // (it was there before you came)
                if (Leaving(r)) PlanIn(r);
                DropLands(r);
                if (!Entering(r) && w.Length > 5 && w[5] == "hub") r.Plan.Enqueue(new Leg { Kind = LegKind.Hover, At = Hub(r) }); // (a turn round the Hall)
                r.Plan.Enqueue(new Leg { Kind = LegKind.Land, At = s.At, Yaw = s.Yaw });
                return;
            }
            if (mode == "head")
            {
                if ((body = HeadOf(w)) == null) { r.SeqSeen = -1; return; } // (that one not here yet — a person still coming in as you walk up: the word read again in ReadEvery s)
                Transform hb = Body.HeadBone(body);
                if (r.Bird == null) { if (Make(r, HeadSeat(body, hb), body.transform.eulerAngles.y)) Ride(r.Bird, body, hb); return; }
                if (Leaving(r)) PlanIn(r);
                DropLands(r);
                r.Plan.Enqueue(new Leg { Kind = LegKind.Head, Body = body });
            }
        }

        private static bool Leaving(R r) { foreach (Leg g in r.Plan) if (g.Kind == LegKind.Leave) return true; return false; } // (a Leave leg gives the bird away as it goes: never «going»)

        // what it was going to do after its way in (a seat, a head, a turn round the place): no more, a new word says it — its way in stays
        private static void DropLands(R r)
        {
            if (r.Plan.Count == 0) return;
            Leg[] all = r.Plan.ToArray();
            int keep = -1;
            for (int i = 0; i < all.Length; i++) if (all[i].Kind == LegKind.Through) keep = i;
            r.Plan.Clear();
            for (int i = 0; i <= keep; i++) r.Plan.Enqueue(all[i]);
        }

        private static bool Make(R r, Vector3 at, float yaw)
        {
            StoryLooks.Look look = StoryLooks.Get(r.L.RavenLook);
            if (look == null) { Story.Log("place raven: " + r.L.Id + ": no look «" + r.L.RavenLook + "» in assets/story/looks"); return false; }
            r.Bird = StoryBird.Sit(at, yaw, false, Shoulder.Scale * Shoulder.SizeFor(Level), look.Text, look.Id);
            if (r.Bird == null) return false;
            r.Bird.name = "Raven of " + r.L.Id;
            r.Bird.TakeLook(look); // (its sparks, its hat)
            R mine = r;
            r.Bird.Stuck = () => OnStuck(mine);
            r.Bird.ViaHint(Hub(r)); // (its way through the air round something tries the middle under the roof first: every seat is open to it — Reachable)
            r.NextCaw = Time.time + Random.Range(CawMin, CawMax);
            return true;
        }

        // a seat it never got to: once more from the middle under the roof, then a puff of its feathers and it sits there (the word stands), and
        // that seat is not chosen again here; a point to hang at it never got to (over the roof from outside): on to its next leg from where it is
        private static void OnStuck(R r)
        {
            if (r.Bird == null) return;
            if (r.LegLast.Kind == LegKind.Hover) { Log(r.L.Id + ": its point in the air out of reach — on from where it is"); r.LegGoing = false; return; }
            if (++r.Stuck >= 2)
            {
                if (r.LegLast.Kind == LegKind.Land) r.Bad.Add(r.LegLast.At);
                Log(r.L.Id + ": its seat out of reach twice — a puff, there now; not chosen again here"); r.Bird.Gone(); r.Bird = null; r.SeqSeen = -1; r.Stuck = 0; r.Plan.Clear(); r.LegGoing = false;
                if (r.Home != null && r.Home.IsOwner()) r.NextDecide = 0f; // (the mind here: another seat now)
                return;
            } // (drawn again sitting where its word says)
            Log(r.L.Id + ": its seat out of reach — once more, from the middle under the roof");
            Leg[] rest = r.Plan.ToArray();
            r.Plan.Clear();
            r.Plan.Enqueue(new Leg { Kind = LegKind.Hover, At = Hub(r) });
            if (r.LegLast.Kind == LegKind.Land || r.LegLast.Kind == LegKind.Head) r.Plan.Enqueue(r.LegLast); // (the same seat, from the open middle)
            foreach (Leg g in rest) r.Plan.Enqueue(g);
            r.LegGoing = false;
        }

        // its legs one after another: a hover or a pass waits till it is there, a landing goes and is done
        private static void Fly(R r)
        {
            StoryBird b = r.Bird;
            if (b == null) return;
            if (b.Sitting) r.Stuck = 0; // (it got to a seat: the next one out of reach is a first try again)
            if (r.LegGoing)
            {
                if ((r.LegNow == LegKind.Hover || r.LegNow == LegKind.Through) && !b.Settled && Time.time - r.LegAt < LegMost) return;
                r.LegGoing = false;
            }
            if (r.Plan.Count == 0) return;
            Leg g = r.Plan.Dequeue();
            r.LegGoing = true; r.LegNow = g.Kind; r.LegLast = g; r.LegAt = Time.time;
            switch (g.Kind)
            {
                case LegKind.Hover: b.HoverAt(g.At); break;
                case LegKind.Through: b.Through(g.At); break;
                case LegKind.Land: b.Land(g.At, g.Yaw); break;
                case LegKind.Head:
                    if (g.Body == null || g.Body.IsDead()) { r.LegGoing = false; break; }
                    Transform hb = Body.HeadBone(g.Body);
                    b.Land(HeadSeat(g.Body, hb), g.Body.transform.eulerAngles.y);
                    Ride(b, g.Body, hb);
                    if (Random.value < 0.5f) r.NextCaw = Time.time + Random.Range(2f, 4f); // (a caw from up there now and then)
                    break;
                case LegKind.Leave: b.Leave(g.At); r.Bird = null; r.Plan.Clear(); r.LegGoing = false; break;
            }
        }

        // on a head: its bone (Body.HeadBone — a dvergr's too: the game's own head call threw for Lit), found once a landing by the caller,
        // followed each frame
        private static Vector3 HeadSeat(Character c, Transform h) { return (h != null ? h.position : Body.Head(c)) + Vector3.up * HeadUp; }
        private static void Ride(StoryBird b, Character c, Transform h) { Transform t = c.transform; b.FollowSeat(() => c == null ? t.position : HeadSeat(c, h), t); }

        private static void Caw(R r)
        {
            StoryBird b = r.Bird;
            if (b == null || Time.time < r.NextCaw) return;
            r.NextCaw = Time.time + Random.Range(CawMin, CawMax);
            if (!b.Sitting || SceneNear()) return; // (not into a scene's words)
            ScreamFx.CawAt(b.transform, b.transform.position + Vector3.up * 0.2f, Random.Range(1.05f, 1.2f), 0.55f, false);
        }

        // ---------------------------------------------------------------- the words
        private static bool ParseSeat(string[] w, out Seat s)
        {
            s = null;
            if (w.Length < 5) return false;
            float x, y, z, yaw;
            if (!float.TryParse(w[1], NumberStyles.Float, Inv, out x) || !float.TryParse(w[2], NumberStyles.Float, Inv, out y) || !float.TryParse(w[3], NumberStyles.Float, Inv, out z) || !float.TryParse(w[4], NumberStyles.Float, Inv, out yaw)) return false;
            s = new Seat { At = new Vector3(x, y, z), Yaw = yaw };
            return true;
        }

        // «head p <player id>» · «head n <person>»: that one, here and alive
        private static Character HeadOf(string[] w)
        {
            if (w.Length < 3) return null;
            if (w[1] == "p") { long id; Player p = long.TryParse(w[2], NumberStyles.Integer, Inv, out id) ? PerchView.PlayerOf(id) : null; return p != null && !p.IsDead() ? p : null; }
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || a.Id != w[2]) continue;
                Character c = a.GetComponent<Character>();
                if (c != null && !c.IsDead()) return c;
            }
            return null;
        }

        private static void Write(ZDO z, string word)
        {
            if (z.GetString(KState, "") == word) return;
            z.Set(KState, word);
            z.Set(KSeq, z.GetInt(KSeq, 0) + 1);
        }

        // ---------------------------------------------------------------- the mind (the home piece's owner), once a second
        private static void Mind(R r, ZDO z)
        {
            string word = z.GetString(KState, "");
            string[] w = word.Split(' ');
            if (word.Length == 0) { Write(z, "in"); r.InSince = Time.time; r.NextDecide = 0f; return; } // (its first day here: it comes in from the sky)
            if (w[0] == "out")
            {
                double until;
                if (w.Length < 2 || !double.TryParse(w[1], NumberStyles.Float, Inv, out until) || WorldTime.Now >= until) { Write(z, "in"); r.InSince = Time.time; }
                return;
            }
            if (w[0] == "in")
            {
                if (r.InSince < 0f) r.InSince = Time.time; // (a new owner: from now)
                if (Time.time - r.InSince >= InSeconds && Time.time >= r.NextDecide) Decide(r, z, false); // (nothing to sit on: asked again in 10 s, not each second)
                return;
            }
            r.InSince = -1f;
            if (Time.time >= r.NextDecide || Wrong(r, w)) Decide(r, z, true);
        }

        // what it sits on can't be kept: a player's raven came, the piece is gone in the place's state, the head went off or a scene began
        private static bool Wrong(R r, string[] w)
        {
            if (w[0] == "head")
            {
                Character c = HeadOf(w);
                if (c == null || !r.L.Holds(c.transform.position) || Time.time >= r.HeadUntil || SceneNear()) return true;
                NpcActor a = NpcActor.Live.Find(x => x != null && x.GetComponent<Character>() == c);
                return a != null && StoryScenes.Holder(a) != 0L;
            }
            Seat s = r.Now;
            if (s == null) { Seat said; if (!ParseSeat(w, out said)) return true; s = r.Now = Nearest(Seats(r), said.At); if (s == null) return true; } // (a new owner: the seat its word names — none of its seats now: off it)
            if (s.Perch == null && s.Piece == null) return true; // (the perch or piece of its seat is gone: off it now)
            if (s.Perch != null) return !s.Perch.Valid || s.Perch.Gone || s.Perch.At(s.I) != null || s.Perch.Busy(s.I);
            if (s.Piece != null) return !s.Piece.IsValid() || LocState.Absent(s.Piece) || Occupied(s.At); // (someone sat down where it sits)
            return false;
        }

        // someone on it or at it (a person on the bench, a player standing on the chest): not a seat now
        internal static float OccupiedNear = 0.8f;
        private static bool Occupied(Vector3 seat)
        {
            foreach (Player p in Player.GetAllPlayers()) if (p != null && Mathf.Abs(p.transform.position.y - seat.y) < 1.5f && Utils.DistanceXZ(p.transform.position, seat) < OccupiedNear) return true; // flat: a body on the same floor as the seat (the height asked beside it)
            foreach (NpcActor a in NpcActor.Live) if (a != null && Mathf.Abs(a.transform.position.y - seat.y) < 1.5f && Utils.DistanceXZ(a.transform.position, seat) < OccupiedNear) return true; // flat: as above
            return false;
        }

        private static Seat Nearest(List<Seat> all, Vector3 at)
        {
            foreach (Seat s in all) if (Vector3.Distance(s.At, at) < 0.3f) return s;
            return null;
        }

        private static bool SceneNear() { return StoryScenes.Playing.Length > 0 || StoryWatch.Watching; }

        private static void Decide(R r, ZDO z, bool fromSeat)
        {
            List<Seat> seats = Seats(r), perches = new List<Seat>(), pieces = new List<Seat>();
            Vector3 cur = r.Bird != null ? r.Bird.transform.position : Hub(r);
            foreach (Seat s in seats)
            {
                if (s == r.Now || Vector3.Distance(s.At, cur) < 2f || NpcRaven.Taken(s.At) || IsBad(r, s.At)) continue; // (another seat, not right by this one, no person's raven on it, not one it never got to)
                if (s.Perch != null) { if (s.Perch.Valid && !s.Perch.Gone && s.Perch.At(s.I) == null && !s.Perch.Busy(s.I)) perches.Add(s); }
                else if (s.Piece != null && s.Piece.IsValid() && !LocState.Absent(s.Piece) && !Occupied(s.At)) pieces.Add(s);
            }
            Character head = Random.value < HeadShare && !SceneNear() ? PickHead(r) : null;
            r.NextDecide = Time.time + (Hurry ? Random.Range(HurryMin, HurryMax) : Random.Range(SitMin, SitMax));
            if (head != null)
            {
                Player p = head as Player;
                NpcActor a = p == null ? NpcActor.Live.Find(x => x != null && x.GetComponent<Character>() == head) : null;
                if (p != null || a != null)
                {
                    r.Now = null; r.HeadUntil = Time.time + Random.Range(HeadMin, HeadMax); r.NextDecide = r.HeadUntil;
                    Write(z, p != null ? "head p " + p.GetPlayerID().ToString(Inv) : "head n " + a.Id);
                    return;
                }
            }
            List<Seat> from = perches.Count > 0 && (pieces.Count == 0 || Random.value < 0.65f) ? perches : pieces;
            if (from.Count == 0) { r.NextDecide = Time.time + 10f; if (!fromSeat) Log(r.L.Id + ": nothing to sit on — it waits under the roof"); return; } // (asked again soon; «in»: it hangs in the middle)
            Seat pick = from[Random.Range(0, from.Count)];
            r.Now = pick;
            Write(z, "seat " + F(pick.At.x) + " " + F(pick.At.y) + " " + F(pick.At.z) + " " + F(pick.Yaw) + (fromSeat && Random.value < HubShare ? " hub" : ""));
        }

        // someone standing in the place: a player, or a story person no scene holds, not running
        private static Character PickHead(R r)
        {
            List<Character> can = new List<Character>();
            foreach (Player p in Player.GetAllPlayers())
                if (p != null && !p.IsDead() && r.L.Holds(p.transform.position) && p.GetVelocity().magnitude < 1.5f && !p.InWater()) can.Add(p);
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || a.Def == null || a.Def.Settler || StoryScenes.Holder(a) != 0L) continue;
                Character c = a.GetComponent<Character>();
                if (c != null && !c.IsDead() && r.L.Holds(c.transform.position) && c.GetVelocity().magnitude < 1.5f) can.Add(c);
            }
            return can.Count > 0 ? can[Random.Range(0, can.Count)] : null;
        }

        // its seats in the place: the perches' seats and the tops of its pieces of the kinds it sits on — looked over again only when the
        // place's state changed or pieces came or went (LocState.LookVersion; the loaded list's count)
        private static List<Seat> Seats(R r)
        {
            List<ZNetView> all = LocParts.All(r.L.Id);
            if (r.SeatsVersion == LocState.LookVersion && (r.SeatsPieces == all.Count || Time.time < r.SeatsAt + SeatsAgain) && !Dead(r.Seats)) return r.Seats; // (pieces still coming in as you walk up: once in SeatsAgain s)
            if (r.SeatsVersion != LocState.LookVersion) r.Bad.Clear(); // (the place itself changed: worth a try again — not only more of its pieces loaded)
            r.SeatsVersion = LocState.LookVersion; r.SeatsPieces = all.Count; r.SeatsAt = Time.time;
            List<Seat> seats = new List<Seat>();
            Vector3 hub = Hub(r);
            int looked = 0, closed = 0;
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || !v.Valid || v.IsSpot || v.IsNest || !r.L.Holds(v.transform.position)) continue;
                for (int i = 0; i < v.Count; i++) { Transform t = v.SeatT(i); if (!Reachable(hub, t.position, t.eulerAngles.y, v.transform)) { closed++; continue; } seats.Add(new Seat { At = t.position, Yaw = t.eulerAngles.y, Perch = v, I = i }); }
            }
            if (r.L.RavenSits.Length > 0)
                foreach (ZNetView nv in all)
                {
                    if (nv == null || !nv.IsValid() || System.Array.IndexOf(r.L.RavenSits, Utils.GetPrefabName(nv.gameObject)) < 0) continue;
                    looked++;
                    Vector3 top;
                    if (!TopOf(nv, out top) || !r.L.Holds(top)) continue;
                    Vector3 face = hub - top; face.y = 0f;
                    float yaw = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face.normalized).eulerAngles.y : Random.Range(0f, 360f); // (facing the middle of the place)
                    if (!Reachable(hub, top, yaw, nv.transform)) { closed++; continue; }
                    seats.Add(new Seat { At = top, Yaw = yaw, Piece = nv });
                }
            r.Seats = seats;
            Story.Log("place raven: " + r.L.Id + ": " + seats.Count + " seats (" + looked + " pieces of its kinds looked at; " + closed + " out of its reach)");
            return seats;
        }

        // a seat of a piece or a perch gone (its zone unloaded and loaded again — new objects, the same count; another world): all looked over anew.
        // Cost: the list (a few dozen), whenever the seats are asked — the mind's choice, a new owner's check
        private static bool Dead(List<Seat> seats)
        {
            foreach (Seat s in seats) if (s.Piece == null && s.Perch == null) return true; // (each has one of them: Unity's null once it is gone)
            return false;
        }

        // a seat it can fly to from inside (StoryBird.Open): from the open middle under the roof (the hub), or from the air over the place's
        // middle at its own height (a bench under a gallery). A top inside a wall, a beam under the eaves outside it, is none (2026-11-17, the
        // user's shot: it took off, flew at such a seat and hung in the wall) — nor a seat with no room for its body as it sits there facing the
        // middle (StoryBird.Room: 2026-11-18, it sat half inside a wall, on a beam laid into the wall's plane). Heavy: a capsule and two rays a
        // seat at most, only when its seats are looked over
        internal static float ReachSide = 1.2f;
        private static bool Reachable(Vector3 hub, Vector3 seat, float yaw, Transform own)
        {
            return StoryBird.Room(seat, yaw, own, Shoulder.SizeFor(Level))
                && (StoryBird.Open(hub, seat) || StoryBird.Open(new Vector3(hub.x, seat.y + StoryBird.OpenOver + ReachSide, hub.z), seat));
        }

        // the top of a piece it may sit on: the middle of its body from above, level enough, room over it (a ray down onto it, one up)
        private static bool TopOf(ZNetView nv, out Vector3 top)
        {
            top = Vector3.zero;
            Bounds b = new Bounds(); bool any = false;
            foreach (Collider c in nv.GetComponentsInChildren<Collider>()) // heavy: only when its seats are looked over (a state's change, pieces loaded)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            if (!any) return false;
            RaycastHit hit;
            Vector3 over = new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
            if (!Physics.Raycast(over, Vector3.down, out hit, b.size.y + 0.6f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) return false;
            if (!hit.collider.transform.IsChildOf(nv.transform) || Vector3.Angle(hit.normal, Vector3.up) > SeatLevel) return false; // (its own top, level)
            if (Physics.Raycast(hit.point + Vector3.up * 0.05f, Vector3.up, SeatRoom, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) return false; // (no room over it)
            top = hit.point;
            return true;
        }

        // ---------------------------------------------------------------- the story's errand (StoryScenesBird)
        // the place's raven known on this game (its home piece loaded: its mind may be here, near or not); drawn — its picture here too
        private static R Known(string place)
        {
            Loc l = Locs.Get(place);
            R r;
            return l != null && _of.TryGetValue(l.Id, out r) && r.Home != null && r.Home.IsValid() ? r : null;
        }

        internal static bool Has(string place) { Loc l = Locs.Get(place); return l != null && l.RavenLook.Length > 0; }
        // its look (assets/story/looks), "" none: a scene's bird of its look that comes from the place's side wears it (StoryScenes «bird come … from»)
        internal static string LookOf(string place) { Loc l = Locs.Get(place); return l != null ? l.RavenLook : ""; }

        // a scene's bird comes from the place to «to»: its very picture where it is drawn (the scene drives it now) — one under its roof to
        // one outside goes up through the roof first, one over the roof to one inside comes down through it (StoryBird.Via), never through
        // a wall; not drawn here, or on its way off: null (the scene's own bird of its look comes from the place's side). «Out» for all
        // mine: the scene is this game's own (its director here; a mirror of another player's scene — not: it takes no mind)
        internal static StoryBird TakeOut(string place, Vector3 to, bool mine)
        {
            R r = Known(place);
            if (r == null) return null;
            MarkOut(r, mine);
            StoryBird b = r.Drawn ? r.Bird : null;
            if (b == null || Leaving(r)) return null; // (not drawn here, or on its way off: «out» written above)
            Vector3 hub = Hub(r), above = Above(r, hub), at = b.transform.position;
            bool under = r.L.Holds(at) && at.y < above.y - AboveRoof, inside = r.L.Holds(to) && to.y < above.y - AboveRoof;
            r.Bird = null; r.Plan.Clear(); r.LegGoing = false;
            b.FollowSeat(null, null);
            b.Adopt();
            b.ForScene(); // (a seat it never gets to in the scene: it hangs there, not a puff — the scene goes on with it)
            if (under && !inside) b.Via(hub, above);
            else if (!under && inside) b.Via(above, hub);
            Log(r.L.Id + ": taken out by a scene" + (under != inside ? " (through its roof first)" : ""));
            return b;
        }

        // …on a game far from that scene: out through its roof, where it is drawn here; «out» where its mind is
        internal static void Away(string place)
        {
            R r = Known(place);
            if (r == null) return;
            MarkOut(r, false);
            if (r.Drawn && r.Bird != null && !Leaving(r)) PlanOut(r);
        }

        // the scene's game takes the mind for its errand (the review 2026-11-18: a letter played on a game that did not own the home piece wrote
        // nothing — the owner's mind kept its raven on a seat in the Hall while the scene's flew off with the letter: two ravens). The home piece's
        // ZDO changes hands as the game's own do when players come and go; a mirror of his scene on another game takes nothing
        private static bool TakeMind(R r, bool mine) { if (mine && !r.Home.IsOwner()) { r.Home.ClaimOwnership(); Log(r.L.Id + ": its mind taken by the scene's game"); } return r.Home.IsOwner(); }
        private static void MarkOut(R r, bool mine)
        {
            if (!TakeMind(r, mine)) return;
            Write(r.Home.GetZDO(), "out " + (WorldTime.Now + OutMost).ToString("0", Inv));
            r.Now = null;
        }

        // the scene's bird home again: «in» where its mind is; drawn here — its picture takes this very bird in through the roof (true).
        // False: not drawn here (the scene's bird flies off towards the place)
        internal static bool Back(string place, StoryBird b, bool mine)
        {
            R r = Known(place);
            if (r == null) return false;
            if (TakeMind(r, mine)) { Write(r.Home.GetZDO(), "in"); r.InSince = Time.time; r.NextDecide = 0f; }
            if (!r.Drawn) return false;
            if (b == null) return true;
            if (r.Bird != null) { Vector3 to; if (!StoryWhere.Find(place, out to)) to = r.Home.transform.position; b.Leave(to); return true; } // (it is drawn here already)
            r.Bird = b; r.Stuck = 0;
            b.Adopt(); // (a mirror of another player's scene: his leaving the game no longer sends it off — it is the place's now)
            b.ForPlace("Raven of " + r.L.Id);
            r.NextCaw = Time.time + Random.Range(CawMin, CawMax);
            R it = r;
            b.Stuck = () => OnStuck(it);
            b.ViaHint(Hub(r));
            PlanIn(r); // (its next word — «in», then a seat — goes on from here: the «out» it went with was read while the scene had it)
            Log(r.L.Id + ": home again from a scene");
            return true;
        }

        // a seat taken by a place's raven (NpcRaven: not for a person's raven then)
        internal static bool Taken(Vector3 seat)
        {
            foreach (R r in _of.Values)
            {
                if (r.Home == null || !r.Word.StartsWith("seat ")) continue;
                Seat s;
                if (ParseSeat(r.Word.Split(' '), out s) && Vector3.Distance(s.At, seat) < 0.3f) return true;
            }
            return false;
        }

        // the author's look (raven story npcravens)
        internal static string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (R r in _of.Values)
                sb.Append("\nВорон місця «").Append(r.L.Id).Append("»: ").Append(r.Home == null ? "не тут (далеко, ще не час чи нема деталі «" + r.L.RavenHome + "»)" :
                    "«" + r.Word + "»" + (r.Home.IsOwner() ? " (вирішує ця гра)" : "") + ", місць " + r.Seats.Count + (r.Bird != null ? ", " + r.Bird.Describe() : ", не видно"));
            return sb.Length > 0 ? sb.ToString() : "\nВоронів місць поруч нема.";
        }
    }
}
#endif
