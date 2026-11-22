#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A person's raven (story-lore.md «Хто має воронів»: only the Watch's — Торвальд's dim one, Гейд's mute Hugin…):
    // written in its template, drawn on every game near it on the story bird's engine (StoryBird), never an item.
    //   raven: hugin|munin                              its kind (Hugin on the right shoulder, Munin on the left)
    //   raven_look: game|<file of assets/story/looks>   its colours (game: the game's own)
    //   raven_dim: 0..1                                 greyer and duller (the story may change it: «npcraven <who> dim»)
    //   raven_inside: perch 70, circle 10, shoulder 20  what it does while the person stays put under a roof
    //   raven_outside: perch 20, circle 50, shoulder 30 … and in the open
    //   raven_pet: 60                                   about every so many s it minds its raven, standing about: strokes it
    //                                                   on its shoulder (the clips pet_l/_r), looks at it on a perch nearby
    //   raven_talk: dim                                 your two may speak to it (RavenTalk, reason «npcr_<this>»): you came within
    //                                                   TalkNear m, a roll of TalkChance at most once in TalkEvery min; its scenes
    //                                                   have the step «N» — this raven, in the words of its own kind's book
    //   raven_name: Ворон Торвальда                     its name over its line in the talk (through Lang)
    //   raven_speaks: 0.1                               how often it says a word back (reason «npcr_<talk>_word»); else it keeps silent
    //   raven_answers: 0.08                             how often your raven asks it something and it answers to the point
    //                                                   (reason «npcr_<talk>_reply»): rolled first, then raven_speaks, then silence
    //   raven_hat: 5                                    a hat on its head from the pool (Outfits.HatPool), another one every so
    //                                                   many days of the world: the same on every game, from the person's seed
    //                                                   and the world's day — nothing sent (HatOf)
    // The person walking: shoulder or circle (the same weights, no perch); in a fight: circling high over it.
    // A perch: a free seat of any perch within PerchNear m (the Hall's «Жердина Варти» too) it can fly to from over its person (StoryBird.Open:
    // none behind a wall — 2026-11-17 Torvald's raven never got to one and went in a puff) — never one a player's raven
    // sits on or flies to, nor one another person's raven or a place's raven (LocRaven) took; a player's raven coming to it later sends this one off.
    // The person's owner decides and writes it into the person (KState «mode|a|b|c|d», KSeq); every game flies its own
    // picture from that, only within DrawFar m. Story (quests, scenes' «do»): npcraven <who> shoulder|circle|perch <place>|
    // leave|free|dim <0..1> — what the story says holds HoldSeconds (or till «free»).
    [DefaultExecutionOrder(20000)] // (its shoulder read after the person's pose is whole: NpcPose; a sitting bird rides its seat as a child)
    internal sealed class NpcRaven : MonoBehaviour
    {
        internal static bool Trace;                       // (the author: its flights in the log — raven npc raventrace)
        internal static float DrawFar = 45f, PerchNear = 15f, PerchLeave = 25f, HoldSeconds = 180f;
        internal static float ComeFrom = 35f;             // m: back from the sky after the story sent it off
        internal static float OpenFrom = 1.4f;            // m over its person's feet a perch must be open to (StoryBird.Open)
        internal static float StayMin = 12f, StayMax = 35f, CircleR = 4.5f, CircleUp = 3.5f, FightR = 7f, FightUp = 6f;
        internal static int Level = 4;                    // its size: as a raven of this trust level on a shoulder
        internal static float SeatLift = 0f;             // m over where a player's raven would sit there (Shoulder.SeatOff + ShoulderFit, what the person wears measured): none — the 7 cm set by eye before the measuring had Heid's raven in the air over her cloak (the user's shot 2026-11-19; at 0 it stands on it); kept as the one number to tune it by

        private static readonly int KState = "RavenWatch_npcRaven".GetStableHashCode();
        private static readonly int KSeq = "RavenWatch_npcRavenSeq".GetStableHashCode();
        private static readonly int KDim = "RavenWatch_npcRavenDim".GetStableHashCode();
        private static readonly int Perf = NpcPerf.Id("raven (ворон людини)");
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- its template's lines
        internal sealed class Spec
        {
            public bool Munin;
            public string Look = "game";
            public string Talk, Name;   // (raven_talk, raven_name: null — your two never speak to it)
            public float Dim, PetEvery, Speaks, Answers;
            public int HatDays;         // (raven_hat: 0 — no hat)
            public float[] Inside = { 70f, 10f, 20f }, Outside = { 20f, 50f, 30f }; // perch, circle, shoulder
        }

        private static readonly Dictionary<NpcDef, Spec> _specs = new Dictionary<NpcDef, Spec>();
        private static readonly Spec NoRaven = new Spec();

        internal static readonly string[] Words = { "raven", "raven_look", "raven_dim", "raven_pet", "raven_inside", "raven_outside", "raven_talk", "raven_name", "raven_speaks", "raven_answers", "raven_hat" }; // (its template lines: the switch below)

        internal static Spec Of(NpcDef d)
        {
            if (d == null) return null;
            Spec s;
            if (_specs.TryGetValue(d, out s)) return s == NoRaven ? null : s;
            if (_specs.Count > 64) _specs.Clear(); // (templates read again: the old ones gone)
            s = null;
            foreach (string line in d.Keep)
            {
                int c = line.IndexOf(':');
                if (c <= 0) continue;
                string k = line.Substring(0, c).Trim().ToLowerInvariant(), v = line.Substring(c + 1).Trim().ToLowerInvariant();
                if (!k.StartsWith("raven")) continue;
                if (s == null) s = new Spec();
                switch (k)
                {
                    case "raven": s.Munin = v.StartsWith("munin"); break;
                    case "raven_look": s.Look = v; break;
                    case "raven_dim": s.Dim = Mathf.Clamp01(Errands.F(v, 0f)); break;
                    case "raven_pet": s.PetEvery = Mathf.Max(0f, Errands.F(v, 0f)); break;
                    case "raven_inside": Weights(v, s.Inside); break;
                    case "raven_outside": Weights(v, s.Outside); break;
                    case "raven_talk": s.Talk = v.Length > 0 ? v : null; break;
                    case "raven_name": s.Name = line.Substring(c + 1).Trim(); break; // (its own case: shown)
                    case "raven_speaks": s.Speaks = Mathf.Clamp01(Errands.F(v, 0f)); break;
                    case "raven_answers": s.Answers = Mathf.Clamp01(Errands.F(v, 0f)); break;
                    case "raven_hat": s.HatDays = Mathf.Max(0, Mathf.RoundToInt(Errands.F(v, 0f))); break;
                }
            }
            if (s != null && !HasLine(d)) s = null; // (only «raven_…» lines, no «raven:» — no raven)
            _specs[d] = s ?? NoRaven;
            return s;
        }

        private static bool HasLine(NpcDef d) { foreach (string l in d.Keep) if (l.Trim().ToLowerInvariant().StartsWith("raven:")) return true; return false; }

        private static void Weights(string v, float[] w)
        {
            string[] p = v.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 1 < p.Length; i += 2)
            {
                int at = p[i] == "perch" ? 0 : p[i] == "circle" ? 1 : p[i] == "shoulder" ? 2 : -1;
                if (at >= 0) w[at] = Mathf.Max(0f, Errands.F(p[i + 1], w[at]));
            }
        }

        // its colours as a look's text (StoryLooks: the story's files), null: the game's own. Washed by dim, its look's own least dim among them
        private static string LookText(string look, float dim, out string id)
        {
            id = "npc_" + look + "_" + Mathf.RoundToInt(dim * 20f);
            StoryLooks.Look l = StoryLooks.Get(look);
            if (l == null && dim <= 0f) return null;
            if (l == null) l = StoryLooks.Get("plain");
            if (l == null) return null;
            dim = Mathf.Max(dim, l.Dim);
            if (dim <= 0f) { id = l.Id; return l.Text; }
            return StoryLooks.Dimmed(l, id, dim);
        }

        // ---------------------------------------------------------------- hung on the people (NpcModules)
        internal static void Wire()
        {
            NpcActor.Ticks.Add(OwnerTick);     // where it is run: what its raven does now
            NpcActor.Everywhere.Add(ShowTick); // every game: its raven's picture
            NpcActor.Unloaded.Add(Drop);
            NpcActor.Removed.Add(Drop);
            NpcActor.Acts["raven"] = Act;      // the story's word, sent to where it is run
        }

        internal static void Actions()
        {
            StoryActions.Register("npcraven", new StoryActions.Kind
            { Dry = "none",
                Min = 2,
                Usage = "npcraven <хто> shoulder|circle|perch <місце>|leave|free|dim <0..1>   (ворон людини; сюжет тримає це " + HoldSeconds.ToString("0") + " с або до free)",
                VerbAt = 2, Verbs = new Dictionary<string, StoryActions.Kind>
                {
                    { "shoulder", new StoryActions.Kind { Do = Word((who, w) => who.Act("raven shoulder")) } },
                    { "circle", new StoryActions.Kind { Do = Word((who, w) => who.Act("raven circle")) } },
                    { "leave", new StoryActions.Kind { Do = Word((who, w) => who.Act("raven leave")) } },
                    { "free", new StoryActions.Kind { Do = Word((who, w) => who.Act("raven free")) } },
                    { "dim", new StoryActions.Kind { Min = 1, Usage = "npcraven <хто> dim <0..1>", Do = Word((who, w) => who.Act("raven dim " + F(Mathf.Clamp01(Errands.F(w[3], 0f))))) } },
                    { "perch", new StoryActions.Kind { Min = 1, Usage = "npcraven <хто> perch <місце>",
                        Do = Word((who, w) =>
                        {
                            Vector3 at; float yaw;
                            if (!SeatNear(w[3], out at, out yaw)) { StoryQuests.Note("npcraven: нема місця «" + w[3] + "»"); return; }
                            who.Act("raven perch " + F(at.x) + " " + F(at.y) + " " + F(at.z) + " " + F(yaw));
                        }) } },
                }
            });
        }

        // a word to a person's raven: the person near, it has a raven, no other player's scene holds it
        private static System.Func<Player, StoryQuests.QuestDef, string[], string, string> Word(System.Action<NpcActor, string[]> say)
        {
            return (p, q, w, rest) =>
            {
                NpcActor who = StoryScenes.Actor(w[1], p.transform.position);
                if (who == null) { StoryQuests.Note("npcraven: нема «" + w[1] + "» поруч"); return null; }
                if (Of(who.Def) == null) { StoryQuests.Note("npcraven: у «" + w[1] + "» нема ворона (raven: у шаблоні)"); return null; }
                if (StoryScenes.HeldByOther(who)) { StoryQuests.Note("npcraven: «" + w[1] + "» зараз у сцені іншого гравця — не чіпаю"); return null; } // (one scene a person)
                say(who, w);
                return null;
            };
        }

        // a story place's seat for it: a free perch seat by it, else its top (a named piece) or the place
        private static bool SeatNear(string place, out Vector3 at, out float yaw)
        {
            yaw = 0f;
            Vector3 spot;
            if (!StoryWhere.Find(place, out spot)) { at = Vector3.zero; return false; }
            PerchView v; int seat;
            if (FreeSeat(spot, 4f, null, out v, out seat)) { Transform t = v.SeatT(seat); at = t.position; yaw = t.eulerAngles.y; return true; }
            return StoryWhere.Top(place, out at);
        }

        private static string F(float v) { return v.ToString("0.###", Inv); }

        // ---------------------------------------------------------------- the owner's mind for it
        private NpcActor _a;
        private Character _body;
        private Spec _spec;
        private float _nextLook, _nextDecide;
        // the story's word holds it till then (world seconds): in the person's locker (NpcLocker.Lives), so a new owner keeps the word
        internal static readonly int KHeldUntil = "RavenWatch_npcRavenHeld".GetStableHashCode();
        private int _situation = -1;
        private PerchView _perch; private int _perchSeat = -1;

        private static NpcRaven Get(NpcActor a)
        {
            NpcRaven r = a.GetComponent<NpcRaven>();
            if (r == null) { r = a.gameObject.AddComponent<NpcRaven>(); r._a = a; r._body = a.GetComponent<Character>(); }
            return r;
        }

        private static void OwnerTick(NpcActor a)
        {
            Spec s = Of(a.Def);
            if (s == null) return;
            long perf = NpcPerf.Now();
            Get(a).Decide(s);
            NpcPerf.Add(Perf, perf);
        }

        // 0 about its place (stays, works, strolls round), 1 on its way somewhere (follows, travels, flees, goes far), 2 fights
        private int Situation()
        {
            NpcMind m = _a.Mind;
            if (m == null) return 0;
            if (m.NowLayer == NpcMind.Layer.Fight) return 2;
            NpcMind.Task t = m.Now;
            if (t == NpcMind.Task.Follow || t == NpcMind.Task.Travel || t == NpcMind.Task.Flee) return 1;
            if (t == NpcMind.Task.Go && m.Current != null && Vector3.Distance(m.Current.To, _a.transform.position) > PerchLeave) return 1;
            return 0; // (a stroll round the Hall is not a way: its raven may keep its perch)
        }

        private void Decide(Spec s)
        {
            if (Time.time < _nextLook) return;
            _nextLook = Time.time + 1f;
            ZDO z = _a.Zdo;
            if (z == null || _body == null) return;
            if (_lookOff > 0f && Time.time >= _lookOff) { _lookOff = 0f; if (_a.Mind != null && _a.Mind.NowLayer != NpcMind.Layer.Scene) _a.Habit("look none"); } // (its look at its raven over)
            if (NpcRescue.Now < NpcLocker.Long(z, KHeldUntil, 0L)) return; // (the story holds it)
            int sit = Situation();
            string mode = Mode(z);
            MindItsRaven(s, mode, sit);
            bool wrong = mode == "perch" && (_perch == null || !_perch.Valid || _perch.At(_perchSeat) != null || _perch.Busy(_perchSeat)
                || Vector3.Distance(_perch.SeatT(_perchSeat).position, _a.transform.position) > PerchLeave || sit == 1); // (a player's raven came to it, or the person is off somewhere)
            bool fightTurn = (sit == 2) != (_situation == 2); // (into a fight or out of it: at once; a stroll's stops and starts — never)
            if (!wrong && !fightTurn && Time.time < _nextDecide && mode.Length > 0) return;
            _situation = sit;
            _perch = null; _perchSeat = -1;
            if (sit == 2) { Write(z, "circle " + F(FightR) + " " + F(FightUp)); _nextDecide = Time.time + StayMin; return; }
            bool roof; int comfort;
            Perches.Measure(_a.transform.position + Vector3.up * 1.5f, out roof, out comfort);
            float[] w = roof ? s.Inside : s.Outside;
            float wp = sit == 0 ? w[0] : 0f, wc = w[1], ws = w[2];
            float roll = Random.value * Mathf.Max(0.001f, wp + wc + ws);
            string pick = roll < wp ? "perch" : roll < wp + wc ? "circle" : "shoulder";
            if (pick == "perch")
            {
                PerchView v; int seat;
                if (FreeSeat(_a.transform.position, PerchNear, this, out v, out seat))
                {
                    Transform t = v.SeatT(seat);
                    _perch = v; _perchSeat = seat;
                    Write(z, "perch " + F(t.position.x) + " " + F(t.position.y) + " " + F(t.position.z) + " " + F(t.eulerAngles.y));
                }
                else pick = wc > ws ? "circle" : "shoulder"; // (no free seat near)
            }
            if (pick == "circle") Write(z, "circle " + F(CircleR) + " " + F(CircleUp));
            else if (pick == "shoulder") Write(z, "shoulder");
            _nextDecide = Time.time + Random.Range(StayMin, StayMax) * (pick == "perch" ? 1.5f : 1f);
        }

        // now and then it minds its raven (where it is run): only standing about at its own affairs, its raven settled
        internal static float SettleFor = 6f, LookFor = 3f, LookNear = 12f;
        private float _nextMind, _modeSince, _lookOff;
        private string _modeWas = "";
        private void MindItsRaven(Spec s, string mode, int sit)
        {
            float now = Time.time;
            if (mode != _modeWas) { _modeWas = mode; _modeSince = now; }
            NpcMind m = _a.Mind;
            if (s.PetEvery <= 0f || m == null) return;
            if (_nextMind <= 0f) _nextMind = now + NpcPersona.Gap(_a, Random.Range(0.3f, 1f) * s.PetEvery); // (not all at once as they come)
            if (now < _nextMind || now - _modeSince < SettleFor) return;
            NpcBody b = _a.Body;
            if (sit != 0 || b == null || !b.ClipFree || m.NowLayer > NpcMind.Layer.Whim || m.AtTable || _body.GetVelocity().magnitude > 0.3f) return; // (not on its way, not in a talk's gesture, not at a meal: asked again next second)
            Vector3 at; float yaw;
            if (mode == "shoulder") _a.Habit("clip pet_" + (s.Munin ? "l" : "r"));
            else if (ParsePerch(_a.Zdo.GetString(KState, ""), out at, out yaw) && Vector3.Distance(at, _a.transform.position) < LookNear && m.Now == NpcMind.Task.Idle)
            {
                _a.Habit("lookat " + F(at.x) + " " + F(at.y) + " " + F(at.z));
                _lookOff = now + LookFor;
            }
            else return; // (circling, or its perch out of sight: another moment)
            _nextMind = now + NpcPersona.Gap(_a, Random.Range(0.6f, 1.4f) * s.PetEvery);
        }

        private static string Mode(ZDO z) { string st = z.GetString(KState, ""); int sp = st.IndexOf(' '); return sp < 0 ? st : st.Substring(0, sp); }

        private static void Write(ZDO z, string state)
        {
            if (z.GetString(KState, "") == state) return;
            z.Set(KState, state);
            z.Set(KSeq, z.GetInt(KSeq, 0) + 1);
        }

        // a free seat of a perch within r m of «from»: none a player's raven sits on or flies to, none another
        // person's raven is on or bound for (as written in that person)
        private static bool FreeSeat(Vector3 from, float r, NpcRaven me, out PerchView best, out int seat)
        {
            best = null; seat = -1;
            List<KeyValuePair<PerchView, int>> free = new List<KeyValuePair<PerchView, int>>();
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || !v.Valid || v.IsSpot || v.IsNest || v.Gone) continue; // (Gone: a piece of a place its state has not now)
                if (Vector3.Distance(v.transform.position, from) > r) continue;
                for (int i = 0; i < v.Count; i++)
                    if (v.At(i) == null && !v.Busy(i) && !TakenByPeople(v.SeatT(i).position, me) && StoryBird.Open(from + Vector3.up * OpenFrom, v.SeatT(i).position)
                        && StoryBird.Room(v.SeatT(i).position, v.SeatT(i).eulerAngles.y, v.transform, Shoulder.SizeFor(Level))) free.Add(new KeyValuePair<PerchView, int>(v, i)); // (heavy: a ray and a capsule a free seat, as it chooses one)
            }
            if (free.Count == 0) return false;
            KeyValuePair<PerchView, int> pick = free[Random.Range(0, free.Count)];
            best = pick.Key; seat = pick.Value;
            return true;
        }

        // a seat a person's raven is on or bound for (a place's raven asks it: LocRaven)
        internal static bool Taken(Vector3 seat) { return TakenByPeople(seat, null); }

        private static bool TakenByPeople(Vector3 seat, NpcRaven me)
        {
            if (me != null && LocRaven.Taken(seat)) return true; // (a place's raven sits there: the Hall's)
            foreach (NpcRaven o in _all)
            {
                if (o == null || o == me || o._a == null) continue;
                ZDO z = o._a.Zdo;
                Vector3 at; float yaw;
                if (z != null && ParsePerch(z.GetString(KState, ""), out at, out yaw) && Vector3.Distance(at, seat) < 0.3f) return true;
            }
            return false;
        }

        private static bool ParsePerch(string st, out Vector3 at, out float yaw)
        {
            at = Vector3.zero; yaw = 0f;
            string[] w = st.Split(' ');
            if (w.Length < 5 || w[0] != "perch") return false;
            at = new Vector3(Errands.F(w[1], 0f), Errands.F(w[2], 0f), Errands.F(w[3], 0f));
            yaw = Errands.F(w[4], 0f);
            return true;
        }

        // the story's word (where it is run): raven shoulder|circle|leave|free · perch x y z yaw · dim v
        private static void Act(NpcActor a, string[] w)
        {
            if (w.Length < 2 || Of(a.Def) == null) return;
            ZDO z = a.Zdo;
            if (z == null) return;
            NpcRaven r = Get(a);
            switch (w[1])
            {
                case "free": NpcLocker.Put(z, KHeldUntil, 0L); r._nextDecide = 0f; r._nextLook = 0f; return;
                case "dim": z.Set(KDim, w.Length > 2 ? Mathf.Clamp01(Errands.F(w[2], 0f)) : 0f); return;
                case "shoulder": Write(z, "shoulder"); break;
                case "circle": Write(z, "circle " + F(CircleR) + " " + F(CircleUp)); break;
                case "leave": Write(z, "leave"); break;
                case "perch":
                    if (w.Length < 6) return;
                    Write(z, "perch " + w[2] + " " + w[3] + " " + w[4] + " " + w[5]);
                    r._perch = null; r._perchSeat = -1;
                    break;
                default: return;
            }
            NpcLocker.Put(z, KHeldUntil, (long)(NpcRescue.Now + HoldSeconds));
        }

        // ---------------------------------------------------------------- its picture (every game)
        private static readonly List<NpcRaven> _all = new List<NpcRaven>();
        private StoryBird _bird;
        private int _seqSeen = -1;
        private float _nextRead, _dimShown = -1f;
        private bool _onShoulder, _left;
        private Transform _bone;
        private Animator _anim;

        private void Awake() { _all.Add(this); }
        private void OnDestroy() { _all.Remove(this); Hide(); }

        private static void ShowTick(NpcActor a)
        {
            Spec s = Of(a.Def);
            if (s == null) return;
            long perf = NpcPerf.Now();
            Get(a).Show(s);
            NpcPerf.Add(Perf, perf);
        }

        private void Show(Spec s)
        {
            _spec = s;
            Player me = Player.m_localPlayer;
            ZDO z = _a.Zdo;
            float far = _bird != null ? DrawFar + 5f : DrawFar;
            bool see = me != null && z != null && _body != null && !_body.IsDead() && _a.Def.Echo <= 0f && !NpcVitals.IsDown(_a)
                && Vector3.Distance(me.transform.position, _a.transform.position) < far;
            if (!see) { Hide(); return; }
            if (Time.time < _nextRead) return;
            _nextRead = Time.time + 0.25f;
            float dim = z.GetFloat(KDim, -1f);
            if (dim < 0f) dim = s.Dim;
            int seq = z.GetInt(KSeq, 0);
            if (_bird != null && Mathf.Abs(dim - _dimShown) > 0.01f) { string id; _bird.Repaint(LookText(s.Look, dim, out id), id); _dimShown = dim; }
            if (_bird != null && s.HatDays > 0) WearHat(s, z);
            if (s.Talk != null) Approach(s, me); // (after its hat: a new one is a thing to say)
            if (_bird != null && seq == _seqSeen) return;
            if (_bird == null && _left && seq == _seqSeen) return; // (flown off by the story's word: not back till the next one)
            _seqSeen = seq;
            Apply(s, z.GetString(KState, ""), dim);
        }

        private void Apply(Spec s, string st, float dim)
        {
            string[] w = st.Split(' ');
            string mode = st.Length > 0 ? w[0] : "shoulder";
            if (mode == "leave")
            {
                _onShoulder = false; _left = true;
                if (_bird != null) { _bird.Leave(_bird.transform.position + _a.transform.forward * 10f); _bird = null; }
                return;
            }
            bool back = _left; // (it flew off: it comes back flying, never just there)
            _left = false;
            Vector3 at; float yaw;
            bool perch = ParsePerch(st, out at, out yaw);
            if (!perch) ShoulderPose(Quaternion.identity, out at, out yaw); // (where to fly to: the seat's own ride comes with LateUpdate)
            if (_bird == null)
            {
                string id;
                string look = LookText(s.Look, dim, out id);
                Vector3 from = back ? at + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * ComeFrom + Vector3.up * 12f : at;
                _bird = StoryBird.Sit(from, yaw, s.Munin, Shoulder.Scale * Shoulder.SizeFor(Level), look, id);
                _dimShown = dim;
                if (_bird == null) return;
                _bird.name = (s.Munin ? "Munin" : "Hugin") + " of " + _a.Id;
                if (back && mode != "circle") _bird.Land(at, yaw);
            }
            else if (mode != "circle") _bird.Land(at, yaw);
            _onShoulder = mode == "shoulder" || (!perch && mode != "circle");
            if (mode == "circle") _bird.Circle(_body, w.Length > 1 ? Errands.F(w[1], CircleR) : CircleR, w.Length > 2 ? Errands.F(w[2], CircleUp) : CircleUp);
        }

        // where it sits on the person: the shoulder's one place, as a player's raven has it (Shoulder.SeatOff from the bone, turned and grown with the
        // body, carried by the shoulder's own move: ride), SeatLift higher; upright, facing as the person does
        private void ShoulderPose(Quaternion ride, out Vector3 at, out float yaw)
        {
            if ((_bone == null || _anim == null || NpcGait.Stale(_bone, _anim)) && _spec != null) // (the body's own shoulder: what it wears carries bones of the same names; looked up again on a body rebuilt by new clothes)
            {
                _anim = _a.GetComponentInChildren<Animator>();
                _bone = _anim != null ? BodyClips.SkeletonBone(this, _anim.transform, _spec.Munin ? "LeftShoulder" : "RightShoulder") : null;
                _chest = _anim != null ? BodyClips.SkeletonBone(this, _anim.transform, "Spine2") : null;
                _ride.Reset();
            }
            Vector3 fwd = Vector3.ProjectOnPlane(_a.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up).eulerAngles.y;
            bool munin = _spec != null && _spec.Munin;
            Vector3 seat0 = (_bone != null ? _bone.position : _a.transform.position + Vector3.up * 1.5f) + ShoulderRide.Gain(ride, Shoulder.RideLift) * Shoulder.SeatOff(_body, munin);
            // on what the person wears, as a player's raven sits on what he wears (ShoulderFit: up or down to the surface under it, along the
            // shoulder off a hood or in from past its edge) — the user's shot 2026-11-19: Heid's raven sat beside her shoulder, not on it
            float lift = ShoulderFit.Lift(_body, munin, seat0);
            at = seat0 + ShoulderFit.Side(_body, munin) + Vector3.up * (lift + SeatLift * _a.transform.localScale.x);
        }

        // The shoulder seat rides with the person (the bird sits on it, or flies to it) the way a player's raven rides its shoulder (ShoulderPin.Place):
        // ON the bone this very frame, the seat carried and tipped by the collarbone's own move (ShoulderRide), its turn after the person's a moment
        // late (Shoulder.TurnFollow*). Till 2026-11-18 its place chased the bone with an ease of its own (a tenth of a second behind) and never
        // turned with it: at a run the shoulder went up and down under a bird that stayed behind — «ворон скаче по плечу», the user.
        private readonly ShoulderRide _ride = new ShoulderRide();
        private Transform _chest;
        private float _seatYaw; private bool _seatSet;

        private void LateUpdate()
        {
            if (_bird != null && (_a == null || Of(_a.Def) == null)) { Hide(); return; } // (its template changed: no raven now)
            if (!_onShoulder || _bird == null || _bird.Seat == null) { if (_seatSet) { _seatSet = false; _ride.Reset(); } return; }
            float dt = Time.deltaTime;
            Quaternion ride = _bone != null && _chest != null ? _ride.Step(_chest, _bone, dt) : Quaternion.identity;
            Vector3 at; float yaw;
            ShoulderPose(ride, out at, out yaw);
            if (!_seatSet) { _seatYaw = yaw; _seatSet = true; }
            else _seatYaw = Mathf.LerpAngle(_seatYaw, yaw, Mathf.Clamp01(dt * (_spec != null && _spec.Munin ? Shoulder.TurnFollowMunin : Shoulder.TurnFollowHugin)));
            _bird.Seat.SetPositionAndRotation(at, Quaternion.Slerp(Quaternion.identity, ride, Shoulder.RideTilt) * Quaternion.Euler(0f, _seatYaw, 0f));
        }

        private void Hide()
        {
            _onShoulder = false;
            if (_bird != null) Destroy(_bird.gameObject);
            _bird = null; _seqSeen = -1; _left = false;
        }

        private static void Drop(NpcActor a) { NpcRaven r = a != null ? a.GetComponent<NpcRaven>() : null; if (r != null) r.Hide(); }

        // ---------------------------------------------------------------- your two speak to it (RavenTalk)
        // You came near it (every game for its own player, on its picture's quarter-second read): at most once in TalkEvery
        // min a roll of TalkChance leaves RavenTalk a reason, which waits there for a quiet moment like any event (both on
        // your shoulders, nobody talking). Whether it answers is rolled here (raven_answers, raven_speaks), so the scene is chosen to fit.
        internal static float TalkNear = 10f, TalkFar = 16f, TalkEvery = 6f, TalkChance = 0.5f, TalkWait = 2f; // m, m, min, share, min
        private float _talkNext;
        // session: the hat your two last had before them, by person (not on this component: the person's coming back into
        // the area makes it anew, and every return would count as a new hat) — a new one, they speak to it at once
        private static readonly Dictionary<string, string> HatSeen = new Dictionary<string, string>();

        private void Approach(Spec s, Player me)
        {
            if (Time.time < _talkNext || _bird == null || _left) return;
            if (Vector3.Distance(me.transform.position, _a.transform.position) > TalkNear || StoryScenes.Holder(_a) != 0L) return; // (not while a scene has the person)
            _talkNext = Time.time + TalkEvery * 60f * Random.Range(0.7f, 1.3f);
            string seen; HatSeen.TryGetValue(_a.Id, out seen);
            bool newHat = _hatWorn != null && _hatWorn != seen;
            if (!newHat && Random.value > TalkChance) return;
            HatSeen[_a.Id] = _hatWorn;
            float roll = Random.value; // to the point (raven_answers), else a word (raven_speaks), else silence
            string end = roll < s.Answers ? RavenTalk.PersonReply : roll < s.Answers + s.Speaks ? RavenTalk.PersonWord : "";
            RavenTalk.Push(RavenTalk.PersonRaven + s.Talk + end, _a.Id, TalkWait);
        }

        // this person's raven sitting on that shoulder of it now (a clip that lays a hand on it: BodyClips.BirdOf), else null
        internal static GameObject BirdOn(Humanoid body, bool left)
        {
            NpcRaven r = body != null ? body.GetComponent<NpcRaven>() : null; // heavy: one component of one body, twice a second while such a clip plays (BodyAnimator.BirdLook)
            return r != null && r._bird != null && r._onShoulder && !r._left && r._spec != null && r._spec.Munin == left ? r._bird.gameObject : null;
        }

        // the picture of this person's raven here (another player's talk to it: its beak and voice on this game)
        internal static GameObject BirdOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (NpcRaven r in _all) if (r != null && r._a != null && r._bird != null && r._a.Id == id) return r._bird.gameObject;
            return null;
        }

        // the person's raven your talk speaks to, still here (within TalkFar m of you, seen, no scene holding the person);
        // hat: the name of what it wears on its head now, or null
        internal static bool Hearer(Player p, string id, out GameObject bird, out bool munin, out string name, out string hat)
        {
            bird = null; munin = false; name = null; hat = null;
            if (p == null || string.IsNullOrEmpty(id)) return false;
            foreach (NpcRaven r in _all)
            {
                if (r == null || r._a == null || r._a.Id != id || r._bird == null || r._left || r._spec == null) continue;
                if (StoryScenes.Holder(r._a) != 0L || Vector3.Distance(p.transform.position, r._bird.transform.position) > TalkFar) return false;
                bird = r._bird.gameObject; munin = r._spec.Munin;
                name = !string.IsNullOrEmpty(r._spec.Name) ? Lang.T(r._spec.Name) : Plugin.RavenName(munin);
                Outfits.Entry e = r._hatWorn != null ? Outfits.Find(r._hatWorn) : null;
                if (e != null) hat = Lang.LowerFirst(e.Name); // (mostly mid-sentence: «Шо це на тобі, {hat}?»; a line that starts with it is capitalised when composed)
                return true;
            }
            return false;
        }

        // the nearest person's raven with this talk, seen here (the console: raven talk npcr_<talk>)
        internal static string NearestWith(Player p, string talk)
        {
            string best = null; float d = TalkFar;
            foreach (NpcRaven r in _all)
            {
                if (r == null || r._a == null || r._bird == null || r._spec == null || r._spec.Talk != talk) continue;
                float x = Vector3.Distance(p.transform.position, r._bird.transform.position);
                if (x <= d) { d = x; best = r._a.Id; }
            }
            return best;
        }

        // ---------------------------------------------------------------- its hat (raven_hat)
        // Which hat now: the pool's piece for the person's seed and the world's day count in HatDays (another than the
        // last span's), the same on every game with nothing sent; asked on the picture's read only when the day turns
        // or the picture is new. Never swapped before your eyes: a new picture comes with it on, a day turned while you
        // look waits till the bird is out of the camera's view. Where it gets them is the story's riddle.
        private string _hatWorn; private int _hatDay = -1; private StoryBird _hatOn;

        private void WearHat(Spec s, ZDO z)
        {
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            if (day == _hatDay && _hatOn == _bird) return;
            if (_hatOn == _bird && InView(_bird.transform.position)) return; // (the day turned, but you see it: later)
            _hatDay = day; _hatOn = _bird;
            _hatWorn = HatOf(NpcKit.Seed(z), day / s.HatDays);
            Outfits.Dress(_bird.gameObject, _hatWorn != null ? _hatWorn + "|||" : "");
            if (_hatWorn == null) _hatDay = -1; // (no catalog yet: asked again on the next read)
        }

        private static bool InView(Vector3 at)
        {
            Camera cam = Utils.GetMainCamera();
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(at);
            return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f; // (a little past the frame's edge too)
        }

        internal static string HatOf(uint seed, int span)
        {
            List<string> pool = Outfits.HatPool();
            if (pool.Count == 0) return null;
            int at = Pick(seed, span, pool.Count);
            if (pool.Count > 1 && at == Pick(seed, span - 1, pool.Count)) at = (at + 1) % pool.Count; // (not the last span's own pick: seldom the same twice running)
            return pool[at];
        }

        private static int Pick(uint seed, int span, int n)
        {
            uint h = seed * 2654435761u ^ (uint)span * 40503u;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;
            return (int)(h % (uint)n);
        }

        // the author's look (raven npc ravens)
        internal static string Describe()
        {
            if (_all.Count == 0) return "Воронів у людей поруч нема.";
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Ворони людей (" + _all.Count + "):");
            foreach (NpcRaven r in _all)
            {
                if (r == null || r._a == null) continue;
                ZDO z = r._a.Zdo;
                sb.Append("\n  ").Append(r._a.Id).Append(": ").Append(z != null ? z.GetString(KState, "—") : "—")
                  .Append(NpcLocker.Long(z, KHeldUntil, 0L) > NpcRescue.Now ? " (сюжет тримає ще " + Mathf.RoundToInt((float)(NpcLocker.Long(z, KHeldUntil, 0L) - NpcRescue.Now)) + " с)" : "")
                  .Append(r._bird != null ? ", " + r._bird.Describe() : ", не видно тут")
                  .Append(r._hatWorn != null ? ", шапка " + r._hatWorn : "");
            }
            return sb.ToString();
        }
    }
}
#endif
