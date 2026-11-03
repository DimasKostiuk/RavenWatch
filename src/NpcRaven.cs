#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A person's raven (story-lore.md «Хто має воронів»: only the Watch's — Торвальд's dim one, Гейд's mute Hugin…):
    // written in its template, drawn on every game near it on the story bird's engine (StoryBird), never an item.
    //   raven: hugin|munin                              its kind (Hugin on the right shoulder, Munin on the left)
    //   raven_look: game|hel|dim|eirik                  its colours (game: the game's own)
    //   raven_dim: 0..1                                 greyer and duller (the story may change it: «npcraven <who> dim»)
    //   raven_inside: perch 70, circle 10, shoulder 20  what it does while the person stays put under a roof
    //   raven_outside: perch 20, circle 50, shoulder 30 … and in the open
    //   raven_pet: 60                                   about every so many s it minds its raven, standing about: strokes it
    //                                                   on its shoulder (the clips pet_l/_r), looks at it on a perch nearby
    // The person walking: shoulder or circle (the same weights, no perch); in a fight: circling high over it.
    // A perch: a free seat of any perch within PerchNear m (the Hall's «Жердина Варти» too) — never one a player's raven
    // sits on or flies to, nor one another person's raven took; a player's raven coming to it later sends this one off.
    // The person's owner decides and writes it into the person (KState «mode|a|b|c|d», KSeq); every game flies its own
    // picture from that, only within DrawFar m. Story (quests, scenes' «do»): npcraven <who> shoulder|circle|perch <place>|
    // leave|free|dim <0..1> — what the story says holds HoldSeconds (or till «free»).
    [DefaultExecutionOrder(20000)] // (its shoulder read after the person's pose is whole: NpcPose; a sitting bird rides its seat as a child)
    internal sealed class NpcRaven : MonoBehaviour
    {
        internal static bool Trace;                       // (the author: its flights in the log — raven npc raventrace)
        internal static float DrawFar = 45f, PerchNear = 15f, PerchLeave = 25f, HoldSeconds = 180f;
        internal static float ComeFrom = 35f;             // m: back from the sky after the story sent it off
        internal static float StayMin = 12f, StayMax = 35f, CircleR = 4.5f, CircleUp = 3.5f, FightR = 7f, FightUp = 6f;
        internal static int Level = 4;                    // its size: as a raven of this trust level on a shoulder
        internal static Vector3 ShoulderOff = new Vector3(0.14f, 0.12f, 0f); // m from the shoulder bone (x mirrored for Munin)

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
            public float Dim, PetEvery;
            public float[] Inside = { 70f, 10f, 20f }, Outside = { 20f, 50f, 30f }; // perch, circle, shoulder
        }

        private static readonly Dictionary<NpcDef, Spec> _specs = new Dictionary<NpcDef, Spec>();
        private static readonly Spec NoRaven = new Spec();

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

        // its colours as a look's text (RavenLooks), null: the game's own
        private static string LookText(string look, float dim, out string id)
        {
            id = "npc_" + look + "_" + Mathf.RoundToInt(dim * 20f);
            if (look == "eirik" && dim <= 0f) { id = "story_eirik"; return StoryBird.LookText; }
            if (look != "hel" && look != "dim" && look != "eirik" && dim <= 0f) return null;
            if (look == "dim") dim = Mathf.Max(dim, 0.45f);
            // body, head, tail · wings · tips: dark and light; the eyes
            string[] dark = look == "hel" ? new[] { "07090B", "0A0E11", "1A2A30" } : look == "eirik" ? new[] { "08080A", "34322F", "5E5A55" } : new[] { "0A0A0C", "0C0C0E", "1C1C20" };
            string[] light = look == "hel" ? new[] { "1C272B", "2A3D44", "4C6C75" } : look == "eirik" ? new[] { "26262C", "B4B0A9", "D6D2CB" } : new[] { "26262C", "2C2C32", "48484E" };
            string eyes = look == "hel" ? "A6E4EC" : look == "eirik" ? "BFD2DC" : "D8B060";
            float power = look == "hel" ? 3f : 2.5f;
            System.Text.StringBuilder sb = new System.Text.StringBuilder("id: " + id + "\nname: " + id + "\nfor: any\n");
            sb.Append("zone body ").Append(Grey(dark[0], dim)).Append(' ').Append(Grey(light[0], dim)).Append(" 100 80\n");
            sb.Append("zone head ").Append(Grey(dark[0], dim)).Append(' ').Append(Grey(light[0], dim)).Append(" 100 80\n");
            sb.Append("zone tail ").Append(Grey(dark[0], dim)).Append(' ').Append(Grey(light[0], dim)).Append(" 100 80\n");
            sb.Append("zone wings ").Append(Grey(dark[1], dim)).Append(' ').Append(Grey(light[1], dim)).Append(" 100 85\n");
            sb.Append("zone tips ").Append(Grey(dark[2], dim)).Append(' ').Append(Grey(light[2], dim)).Append(" 100 85\n");
            sb.Append("eyes ").Append(Grey(eyes, dim * 0.8f)).Append(' ').Append((power * (1f - dim * 0.85f)).ToString("0.##", Inv)).Append('\n');
            return sb.ToString();
        }

        // a colour washed towards a dull grey by 0..1
        private static string Grey(string hex, float by)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString("#" + hex, out c)) return hex;
            c = Color.Lerp(c, new Color(0.42f, 0.42f, 0.43f), Mathf.Clamp01(by) * 0.7f);
            return ColorUtility.ToHtmlStringRGB(c);
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
            {
                Min = 2,
                Usage = "npcraven <хто> shoulder|circle|perch <місце>|leave|free|dim <0..1>   (ворон людини; сюжет тримає це " + HoldSeconds.ToString("0") + " с або до free)",
                Check = w =>
                {
                    string m = w[2];
                    if (m != "shoulder" && m != "circle" && m != "perch" && m != "leave" && m != "free" && m != "dim") return "npcraven <хто> shoulder|circle|perch <місце>|leave|free|dim <0..1>";
                    if ((m == "perch" || m == "dim") && w.Length < 4) return "npcraven <хто> " + m + (m == "perch" ? " <місце>" : " <0..1>");
                    return null;
                },
                Do = (p, q, w, rest) =>
                {
                    NpcActor who = StoryScenes.Actor(w[1], p.transform.position);
                    if (who == null) { StoryQuests.Note("npcraven: нема «" + w[1] + "» поруч"); return null; }
                    if (Of(who.Def) == null) { StoryQuests.Note("npcraven: у «" + w[1] + "» нема ворона (raven: у шаблоні)"); return null; }
                    if (StoryScenes.HeldByOther(who)) { StoryQuests.Note("npcraven: «" + w[1] + "» зараз у сцені іншого гравця — не чіпаю"); return null; } // (one scene a person, its raven too)
                    string m = w[2];
                    if (m == "perch")
                    {
                        Vector3 at; float yaw;
                        if (!SeatNear(w[3], out at, out yaw)) { StoryQuests.Note("npcraven: нема місця «" + w[3] + "»"); return null; }
                        who.Act("raven perch " + F(at.x) + " " + F(at.y) + " " + F(at.z) + " " + F(yaw));
                    }
                    else if (m == "dim") who.Act("raven dim " + F(Mathf.Clamp01(Errands.F(w[3], 0f))));
                    else who.Act("raven " + m);
                    return null;
                }
            });
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
        private float _nextLook, _nextDecide, _heldUntil;
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
            if (Time.time < _heldUntil) return; // (the story holds it)
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
            if (_nextMind <= 0f) _nextMind = now + Random.Range(0.3f, 1f) * s.PetEvery; // (not all at once as they come)
            if (now < _nextMind || now - _modeSince < SettleFor) return;
            NpcBody b = _a.Body;
            if (sit != 0 || b == null || b.ClipBusy || m.NowLayer > NpcMind.Layer.Whim || m.AtTable || _body.GetVelocity().magnitude > 0.3f) return; // (not on its way, not in a talk's gesture, not at a meal: asked again next second)
            Vector3 at; float yaw;
            if (mode == "shoulder") _a.Habit("clip pet_" + (s.Munin ? "l" : "r"));
            else if (ParsePerch(_a.Zdo.GetString(KState, ""), out at, out yaw) && Vector3.Distance(at, _a.transform.position) < LookNear && m.Now == NpcMind.Task.Idle)
            {
                _a.Habit("lookat " + F(at.x) + " " + F(at.y) + " " + F(at.z));
                _lookOff = now + LookFor;
            }
            else return; // (circling, or its perch out of sight: another moment)
            _nextMind = now + Random.Range(0.6f, 1.4f) * s.PetEvery;
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
                if (v == null || !v.Valid || v.IsSpot || v.IsNest) continue;
                if (Vector3.Distance(v.transform.position, from) > r) continue;
                for (int i = 0; i < v.Count; i++)
                    if (v.At(i) == null && !v.Busy(i) && !TakenByPeople(v.SeatT(i).position, me)) free.Add(new KeyValuePair<PerchView, int>(v, i));
            }
            if (free.Count == 0) return false;
            KeyValuePair<PerchView, int> pick = free[Random.Range(0, free.Count)];
            best = pick.Key; seat = pick.Value;
            return true;
        }

        private static bool TakenByPeople(Vector3 seat, NpcRaven me)
        {
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
                case "free": r._heldUntil = 0f; r._nextDecide = 0f; r._nextLook = 0f; return;
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
            r._heldUntil = Time.time + HoldSeconds;
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
            if (!perch) ShoulderPose(out at, out yaw);
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

        // where it sits on the person: over its shoulder bone, upright, facing as the person does
        private void ShoulderPose(out Vector3 at, out float yaw)
        {
            if ((_bone == null || _anim == null || NpcGait.Stale(_bone, _anim)) && _spec != null) { _anim = _a.GetComponentInChildren<Animator>(); _bone = _anim != null ? BodyClips.SkeletonBone(this, _anim.transform, _spec.Munin ? "LeftShoulder" : "RightShoulder") : null; } // (the body's own shoulder: what it wears carries bones of the same names; looked up again on a body rebuilt by new clothes)
            Vector3 fwd = Vector3.ProjectOnPlane(_a.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            Quaternion facing = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            yaw = facing.eulerAngles.y;
            Vector3 off = _spec != null && _spec.Munin ? new Vector3(-ShoulderOff.x, ShoulderOff.y, ShoulderOff.z) : ShoulderOff;
            at = (_bone != null ? _bone.position : _a.transform.position + Vector3.up * 1.5f) + facing * (off * _a.transform.localScale.x);
        }

        // the shoulder seat rides with the person (the bird sits on it, or flies to it) — the shoulder player ravens' way:
        // its place eased in the person's own frame (the walk's shake of the bone smoothed away, the body's move kept
        // whole) and its turn after the person's a moment late (Shoulder.TurnFollow*)
        internal static float SeatEase = 10f;
        private Vector3 _seatLocal; private float _seatYaw; private bool _seatSet;

        private void LateUpdate()
        {
            if (_bird != null && (_a == null || Of(_a.Def) == null)) { Hide(); return; } // (its template changed: no raven now)
            if (!_onShoulder || _bird == null || _bird.Seat == null) { _seatSet = false; return; }
            Vector3 at; float yaw;
            ShoulderPose(out at, out yaw);
            Transform root = _a.transform;
            Vector3 local = root.InverseTransformPoint(at);
            float dt = Time.deltaTime;
            if (!_seatSet) { _seatLocal = local; _seatYaw = yaw; _seatSet = true; }
            else
            {
                _seatLocal = Vector3.Lerp(_seatLocal, local, 1f - Mathf.Exp(-SeatEase * dt));
                _seatYaw = Mathf.LerpAngle(_seatYaw, yaw, Mathf.Clamp01(dt * (_spec != null && _spec.Munin ? Shoulder.TurnFollowMunin : Shoulder.TurnFollowHugin)));
            }
            _bird.Seat.SetPositionAndRotation(root.TransformPoint(_seatLocal), Quaternion.Euler(0f, _seatYaw, 0f));
        }

        private void Hide()
        {
            _onShoulder = false;
            if (_bird != null) Destroy(_bird.gameObject);
            _bird = null; _seqSeen = -1; _left = false;
        }

        private static void Drop(NpcActor a) { NpcRaven r = a != null ? a.GetComponent<NpcRaven>() : null; if (r != null) r.Hide(); }

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
                  .Append(r._heldUntil > Time.time ? " (сюжет тримає ще " + Mathf.RoundToInt(r._heldUntil - Time.time) + " с)" : "")
                  .Append(r._bird != null ? ", " + r._bird.Describe() : ", не видно тут");
            }
            return sb.ToString();
        }
    }
}
#endif
