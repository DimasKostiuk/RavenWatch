#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A raven of the story (StoryScenes' «bird»): nobody's, no item, no network object — only a picture, as the raven in a
    // grave (GraveBird), on the same engine: the motor flies it, the shoulder's life moves its wings, the perch's look
    // seats it. Eirik's raven first (story-lore.md «Пролог»): the memory of the first Watch, a Munin with ash-grey wings.
    // The scene's owner drives it; the players near get the same orders (StoryScenes' RPC) and fly a picture of their own.
    //   Come(from, seat, yaw)   appears far off and flies in, lands on the seat (or hangs in the air there: hover);
    //                           own: not Eirik's but your own raven's picture (its kind, plain black, its size asleep) —
    //                           the one that becomes yours on your shoulder (StoryOwn: «own … here»)
    //   Land(seat, yaw)         off from where it is, to another seat
    //   FollowSeat(at, body)    its seat goes with that point (a shoulder's seat: Shoulder.SeatPoint), asked a frame, from now till the next seat
    //   Face(point)             turns where it sits
    //   Say(text)               a line over it
    //   Circle(around, r, up)   round over someone (until told otherwise)
    //   Leave(toward)           off that way and gone in the sky
    //   Carry(prefab) · Drop()  a thing in its beak, as the post's bird (RavenFetch.Hold): the letter it brings — off as he takes it
    // A white-blue streak follows Eirik's raven in the air, so it is seen against trees and sky (Trail); your own has none.
    // A person's raven (NpcRaven) is one too: made sitting (Sit), its own kind, size and colours (Repaint), no streak,
    // circling its person (Circle takes any body), its flights in the log only when traced.
    //   Gone()                  a puff of feathers · Vanish(): no puff (your own raven, the item on your shoulder now)
    //   Wear(look, seconds)     its colours go over into another look, slowly (chapter 1: Eirik's raven gives its feather and one of its two colours dies)
    //   HoverAt(point)          off its seat (or on from where it flies) to hang in the air there (Settled once there)
    //   Via(first, through)     a way before whatever it is told next: hang at «first», straight through to «through» (a roof), then
    //                           the next Land or HoverAt (a place's raven out to one outside its walls, or in: LocRaven.TakeOut)
    //   Through(point)          from the air straight to that point through whatever is between, slowly, a couple of seconds
    //                           (RavenMotor.Through: a place's raven out and in through its roof — LocRaven), then it hangs there
    // A place's raven (LocRaven) is made sitting too (Sit) and its place tells it what to do; a seat it never gets to calls its Stuck
    // instead of the puff. Whether a seat can be flown to at all is asked as it is chosen: Open(from, seat). A scene's bird that never gets to hang before him (GiveUp s: trees over him) goes the rest straight (Through). The sparks of its look («sparks:», StoryLooks) trail it while it flies (Sparks), none while it sits.
    internal class StoryBird : MonoBehaviour, IRavenUser
    {
        private const string Name = "у сюжеті";
        internal static int Level = 4;
        internal static bool PaintOn = true; // (the author's test: off = the game's own colours)
        internal static readonly List<StoryBird> Live = new List<StoryBird>();
        private int _wasState = -1;
        private static string Where(Vector3 v) { return Mathf.RoundToInt(v.x) + "," + Mathf.RoundToInt(v.y) + "," + Mathf.RoundToInt(v.z); }
        private static void Log(string s) { Story.Log("bird: " + s); }
        private void Note(string s) { if (!_npc || NpcRaven.Trace || (LocRaven.Trace && name.StartsWith("Raven of "))) Log((_npc ? "(" + name + ") " : "") + s); } // (a person's raven, a place's: in the log only when traced)
        internal static float GiveUp = 25f, LeaveGone = 50f, LeaveSeconds = 14f;
        // a seat a bird of the story can fly to from there: nothing between that point and the air just over it (a top inside a wall, a perch
        // behind one — 2026-11-17 the Hall's raven took off at such a seat and hung in the wall GiveUp s, twice). The kind's one check, asked
        // as a seat is chosen: LocRaven (its seats, from the middle under its roof), NpcRaven (a perch, from its person's head). Heavy: one ray
        internal static float OpenOver = 0.35f;
        internal static bool Open(Vector3 from, Vector3 seat) { return !Physics.Linecast(from, seat + Vector3.up * OpenOver, RavenMotor.Mask, QueryTriggerInteraction.Ignore); }
        internal static float RingNear = 10f, RingFar = 25f; // (circling: the ring's slow glide within RingNear m of it, full cruise from RingFar)
        private const int Sits = 0, Flies = 2, Final = 3, Landing = 4, Away = 5, Hover = 6, Circles = 7, Passes = 8;
        internal System.Action Stuck; // (a place's raven: its seat out of reach — its place picks again; none: the puff, or a scene's hover)
        // the streak (the user 2026-11-02): thin, 30% fainter than it was, from its tail (not from under its feet), and
        // faint right at the bird — it grows out of nothing a little behind it, then fades away
        internal static float TrailSeconds = 1.6f, TrailWidth = 0.1f, TrailPeak = 0.6f, TrailRise = 0.14f;
        internal static Color TrailHead = new Color(0.92f, 0.97f, 1f, 1f), TrailTail = new Color(0.35f, 0.6f, 1f, 0f);
        private Transform _trail;
        private Character _around;
        private float _ringR = 4.5f, _ringUp = 3.5f, _ringPhase;

        private static readonly Dictionary<string, Look> _looks = new Dictionary<string, Look>();
        internal static void ForgetLooks() { _looks.Clear(); } // (the files read again: their colours too)
        // a person's raven (NpcRaven): its kind, size and colours; no streak, not among the scene's birds
        private bool _npc, _munin = true;
        internal bool Munin { get { return _munin; } }
        private float _size = -1f;
        private string _lookText, _lookId = ""; // (its colours: the look it was told, StoryLooks; none — the game's)

        internal Transform Seat;
        internal int State = Sits;
        internal bool Sitting { get { return State == Sits; } }
        // there and still: sitting, or hanging in the air where it was sent (the scene goes on)
        internal bool Settled { get { return _via == 0 && (State == Sits || (State == Hover && Seat != null && Vector3.Distance(transform.position, Seat.position) < 1.5f)); } }
        private bool _hover;
        private Vector3 _faceAt;
        private ShoulderLife _life;
        private RavenMotor _motor;
        private RavenLooks.Painted _paint;
        private float _since, _settle;
        private Vector3 _landOff, _away;
        private Quaternion _landFromRot, _faceTo;
        private bool _clip, _yielded, _turning;

        public void Yield() { _yielded = true; Destroy(this); }

        // nobody's any more — its scene is over (Release), or the one whose scene it is left the game (Owner: another
        // player's, a picture of their scene here): where it sits, hangs or circles, it flies off a little later, or at
        // once when nobody is near
        internal long Owner;
        internal static float FreeSeconds = 60f, FreeNear = 60f;
        private bool _free;
        private float _freeAt, _nextFree;

        internal void Release() { if (!_free) { _free = true; _freeAt = Time.time; } }
        // nobody's scene's any more, nor free to fly off: a place's raven takes it (LocRaven.Back, TakeOut) — another player's scene's mirror too
        internal void Adopt() { Owner = 0L; _free = false; }
        // a place's raven lent to a scene (LocRaven.TakeOut): a scene's bird now — a seat it never gets to leaves it hanging there, not a puff
        // (the scene goes on with it), its legs in the log, among the scene's birds
        internal void ForScene() { _npc = false; Stuck = null; if (!Live.Contains(this)) Live.Add(this); }
        // …and back (LocRaven.Back): the place's again — its name, nobody's eyes to face, not among the scene's birds
        internal void ForPlace(string named) { _npc = true; _faceAt = Vector3.zero; name = named; Live.Remove(this); }

        private void Unowned()
        {
            if (Time.time < _nextFree) return;
            _nextFree = Time.time + 1f;
            if (!_free && Owner != 0L && PerchView.PlayerOf(Owner) == null) Release();
            if (!_free || (State != Sits && State != Hover && State != Circles)) return;
            Player me = Player.m_localPlayer;
            bool alone = me == null || Vector3.Distance(me.transform.position, transform.position) > FreeNear;
            if (alone || Time.time - _freeAt > FreeSeconds) Leave(transform.position + transform.forward * 10f);
        }
        private static float Size { get { return Shoulder.Scale * Shoulder.SizeFor(Level); } }
        private float MySize { get { return _size > 0f ? _size : Size; } }

        // a bird far off (from), flying in to a seat (yaw: where it faces sitting)
        internal static StoryBird Come(Vector3 from, Vector3 seat, float yaw, bool hover, int own = -1, string look = "") // (own: -1 a bird of the story's — its look, else StoryLooks.Default; 0 Hugin, 1 Munin)
        {
            Transform start = NewSeat(from, yaw);
            StoryLooks.Look told = own >= 0 ? null : StoryLooks.Get(look) ?? StoryLooks.Default;
            float size = own >= 0 ? Shoulder.Scale * Shoulder.SizeFor(0) : told != null && told.Level > 0 ? Shoulder.Scale * Shoulder.SizeFor(told.Level) : Size; // (your own: as it will sit on you; a look with «level:» — that trust's size: Bruni's raven)
            GameObject r = Perches.Sitter(start, own != 0, size, -1, null, null);
            if (r == null) { Destroy(start.gameObject); Log("no picture made (Perches.Sitter)"); return null; }
            Log("comes from " + Where(from) + " to " + Where(seat) + (hover ? " (in the air)" : " (to sit)"));
            StoryBird b = r.AddComponent<StoryBird>();
            b.Seat = start;
            b._size = size;
            if (own >= 0) b._munin = own == 1; // (the game's colours: your own is a plain black one)
            if (told != null) { b._lookText = told.Text; b._lookId = told.Id; }
            b.Paint();
            b.Land(seat, yaw);
            b._hover = hover; // (after Land: a landing is not a hover, a coming to hang is — «bird come … hover», the letter's raven)
            if (told != null && told.Streak) b.Streak(); // (the white-blue streak: its look says — your own comes as a raven)
            if (told != null) b.TakeLook(told);
            b._ringPhase = Random.Range(0f, 10f);
            Live.Add(b);
            return b;
        }

        // a person's raven (NpcRaven), sitting there already (it was there before you came near); lookText null: the game's colours
        internal static StoryBird Sit(Vector3 seat, float yaw, bool munin, float size, string lookText, string lookId)
        {
            Transform at = NewSeat(seat, yaw);
            GameObject r = Perches.Sitter(at, munin, size, -1, null, null);
            if (r == null) { Destroy(at.gameObject); return null; }
            StoryBird b = r.AddComponent<StoryBird>();
            b._npc = true; b._munin = munin; b._size = size; b._lookText = lookText; b._lookId = lookId ?? "";
            b.Seat = at;
            b._ringPhase = Random.Range(0f, 10f);
            b.Paint();
            return b;
        }

        // its colours anew (a person's raven grows dim or bright: NpcRaven)
        internal void Repaint(string lookText, string lookId)
        {
            if (_lookId == (lookId ?? "") && _lookText == lookText) return;
            foreach (Renderer x in GetComponentsInChildren<Renderer>(true)) // (back to the game's materials first: painted anew from them)
            {
                if (x == null || x is ParticleSystemRenderer || x.GetComponentInParent<Outfits.Piece>() != null) continue; // (not what it wears: a person's raven's hat)
                Material[] cur = x.sharedMaterials;
                for (int i = 0; i < cur.Length; i++) cur[i] = OwnOf(EyeGlow.Original(cur[i]));
                x.sharedMaterials = cur;
            }
            RavenLooks.Drop(_paint); _paint = null;
            _lookText = lookText; _lookId = lookId ?? "";
            Paint();
            ShoulderLife life = GetComponent<ShoulderLife>();
            if (life != null) life.EyesAgain();
        }

        private static Transform NewSeat(Vector3 at, float yaw)
        {
            GameObject g = new GameObject("RavenWatch_StorySeat");
            g.transform.position = at;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return g.transform;
        }

        // ---- the orders

        private System.Func<Vector3> _seatAt; private Transform _seatBody;
        internal void FollowSeat(System.Func<Vector3> at, Transform body)
        {
            if (_via > 0 && _thenSet) { _thenFollow = at; _thenBody = body; return; } // (on its way: the seat it goes on to moves with that one only then)
            _seatAt = at; _seatBody = body;
        }
        private System.Func<Vector3> _thenFollow; private Transform _thenBody;

        // a way first, whatever it is told meanwhile (a place's raven to one outside its walls: up under its roof, through it, then on to
        // him — LocRaven.TakeOut; or the other way in): hang at «first», pass to «through», then the Land or HoverAt it was given meanwhile.
        // Settled only after that; Leave, Circle, Gone cut the way
        private int _via; private Vector3 _viaThrough;
        private bool _thenSet, _thenLand; private Vector3 _thenAt; private float _thenYaw;
        internal void Via(Vector3 first, Vector3 through)
        {
            _via = 1; _viaThrough = through; _thenSet = false; _thenFollow = null; _thenBody = null;
            HoverNow(first);
        }
        private void ViaTick()
        {
            if (_via == 1 && State == Hover && Seat != null && Vector3.Distance(transform.position, Seat.position) < 1.5f) { _via = 2; Through(_viaThrough); return; }
            if (_via != 2 || State != Hover) return;
            _via = 0;
            if (!_thenSet) return;
            _thenSet = false;
            if (_thenLand) LandNow(_thenAt, _thenYaw); else HoverNow(_thenAt);
            if (_thenFollow != null) { _seatAt = _thenFollow; _seatBody = _thenBody; _thenFollow = null; _thenBody = null; }
        }

        internal void Land(Vector3 seat, float yaw)
        {
            if (_via > 0) { _thenSet = true; _thenLand = true; _thenAt = seat; _thenYaw = yaw; _thenFollow = null; _thenBody = null; return; } // (after its way; a seat to follow, if any, is told after this)
            LandNow(seat, yaw);
        }

        private void LandNow(Vector3 seat, float yaw)
        {
            _seatAt = null;
            Transform old = Seat, now = NewSeat(seat, yaw);
            if (State == Landing) SitDown(); // (touching down this moment: on the old seat first, then off it)
            if (State == Hover) State = Flies; // (from the air to a seat: it lands now)
            _hover = false; // (told to land while still on its way to hang somewhere: it lands, not hangs over the seat)
            if (_motor != null) _motor.Through = false; // (told to land in the middle of a pass: walls count again)
            _from = ComeFrom(seat, Flat(now.forward)); // (a side of the seat as it faces: it may turn with a body — a shoulder, a head)
            Seat = now;
            if (State == Sits) TakeOff(seat + Vector3.up * 2f); // (off the old seat: no longer its child)
            else { if (_motor != null && _motor.Body != null) _motor.Body.NewLeg(); State = Flies; _since = Time.time; }
            if (_motor == null) SitDown(); // (no engine: straight onto the new seat)
            if (old != null) Destroy(old.gameObject);
        }

        // to hang in the air at a point (off its seat, or on from where it flies)
        internal void HoverAt(Vector3 at)
        {
            if (_via > 0) { _thenSet = true; _thenLand = false; _thenAt = at; _thenFollow = null; _thenBody = null; return; } // (after its way)
            HoverNow(at);
        }

        private void HoverNow(Vector3 at)
        {
            _seatAt = null;
            Transform old = Seat;
            Seat = NewSeat(at, transform.eulerAngles.y);
            if (State == Landing) SitDown();
            _hover = true;
            if (State == Sits) TakeOff(at);
            else { if (_motor != null && _motor.Body != null) _motor.Body.NewLeg(); State = Flies; _since = Time.time; }
            if (_motor != null) _motor.Through = false;
            if (old != null) Destroy(old.gameObject);
        }

        // what its look puts on it besides its colours: the sparks in flight, a hat and its gear (StoryLooks «hat:», «wear:»: Outfits.Dress, as a
        // person's raven's hat; several of one slot joined by «+»). A look it changes to (Wear) takes the old one's off: none — none; others — the new ones
        internal void TakeLook(StoryLooks.Look l)
        {
            if (l == null) return;
            if (l.Sparks) Sparks(l.SparkA, l.SparkB);
            else if (_sparks != null) { Destroy(_sparks.gameObject); _sparks = null; }
            string[] slots = { "", "", "", "" }; // (Outfits: head, neck, back, legs)
            List<string> pieces = new List<string>(l.Wear);
            if (l.Hat.Length > 0) pieces.Insert(0, l.Hat);
            foreach (string id in pieces)
            {
                Outfits.Entry e = Outfits.Find(RavenGear.KeyPrefix + id);
                if (e == null || e.Slot < 0 || e.Slot >= slots.Length) { Log("look " + l.Id + ": no gear «" + id + "» (assets/gear)"); continue; }
                slots[e.Slot] += (slots[e.Slot].Length > 0 ? "+" : "") + RavenGear.KeyPrefix + id;
            }
            Outfits.Dress(gameObject, string.Join("|", slots).Trim('|').Length > 0 ? string.Join("|", slots) : ""); // (none: an old hat off; nothing worn — nothing done)
        }

        // the side it comes in to a seat from: from behind it (it lands facing ahead), unless something is there — a seat facing the middle
        // of a hall has the wall behind it (2026-11-17: the Hall's raven, its seats facing the middle, flew into the wall to get behind them) —,
        // then from before it, then from straight over it, and if that is shut too, from behind as ever. Kept as the side of the seat as it
        // faces (1 behind, -1 before, 0 over: Side), so a seat that turns with a body is come to as it faces now. Heavy: up to three rays, a landing
        private int _from = 1;
        internal static float ComeBack = 1.6f, ComeUp = 1.2f;
        private static int ComeFrom(Vector3 seat, Vector3 front)
        {
            Vector3 over = seat + Vector3.up * OpenOver, up = Vector3.up * ComeUp;
            if (!Physics.Linecast(over, seat - front * ComeBack + up, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) return 1;
            if (!Physics.Linecast(over, seat + front * ComeBack + up, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) return -1;
            if (!Physics.Linecast(over, seat + up, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) return 0;
            return 1;
        }
        private Vector3 Side(Vector3 front) { return _from == 1 ? -front : _from == -1 ? front : Vector3.zero; }

        // from the air straight to a point through whatever is in the way (a roof), slowly: the motor neither swerves nor stops for this
        // leg (RavenMotor.Through); there it hangs. Off a seat it first flies up the usual way (HoverAt)
        internal static float ThroughSpeed = 4f, ThroughMost = 6f;
        internal bool OnItsWay { get { return _via > 0 || State == Passes; } } // (out through a roof, or a straight pass: StoryScenes.BirdWaits lets it get there)
        private float _passMost = 6f;
        private bool _passAgain; // (this pass was given its time once more already)
        internal void Through(Vector3 to)
        {
            _passAgain = false;
            if (State == Sits || State == Landing || _motor == null) { HoverNow(to); return; }
            _seatAt = null;
            if (Seat != null) Seat.position = to;
            _hover = true;
            if (_motor.Body != null) _motor.Body.NewLeg();
            _motor.Through = true;
            _passMost = Mathf.Max(ThroughMost, Vector3.Distance(transform.position, to) / ThroughSpeed + 1f); // (a roof: a couple of seconds; the rest of a way: as long as it takes)
            State = Passes; _since = Time.time;
        }

        internal void Face(Vector3 point)
        {
            if (State == Hover || _hover) { _faceAt = point; return; } // (in the air: the flight turns it)
            _faceAt = point;
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f || Seat == null) return;
            _faceTo = Quaternion.LookRotation(d.normalized, Vector3.up);
            _turning = true;
        }

        internal void Say(string text)
        {
            if (Chat.instance == null || string.IsNullOrEmpty(text)) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * 0.55f, 40f, RavenSpeech.Seconds(text, RavenSpeech.Pace.Bird), "", text, false);
            ScreamFx.CawAt(transform, transform.position, 0.55f, 0.8f, false);
        }

        internal void Leave(Vector3 toward)
        {
            Vector3 d = toward - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 1f) d = transform.forward;
            _away = transform.position + d.normalized * 80f + Vector3.up * 30f;
            _via = 0;
            if (State == Landing) SitDown();
            if (State == Sits) TakeOff(_away);
            _hover = false;
            if (_motor == null) { Gone(); return; }
            if (_motor.Body != null) _motor.Body.NewLeg();
            State = Away; _since = Time.time;
        }

        internal void Gone() { Perches.Puff(gameObject, _munin, "a story bird gone"); }

        // its colours into those of «to» over seconds: painted anew WearSteps times on the way (StoryLooks.Between), not each frame —
        // a repaint makes its materials again. A bird in the game's own colours (none told) takes the look at once
        internal static int WearSteps = 3; // (each repaint goes over the whole picture and sends it to the card: ~20 MB, measured in the log — the review 2026-11-14: 10 would stutter)
        private string _wearFrom, _wearTo, _wearId;
        private float _wearAt, _wearFor;
        private string _wearTarget;
        private StoryLooks.Look _wearLook; // (its sparks and hat, put on when the colours are through)
        private long _wearMs;
        private int _wearDone;
        internal void Wear(StoryLooks.Look to, float seconds)
        {
            if (to == null) return;
            if (_lookText == null || seconds <= 0.05f) { _wearTo = null; Repaint(to.Text, to.Id); TakeLook(to); return; }
            _wearLook = to; _wearFrom = _lookText; _wearTo = to.Text; _wearId = to.Id + "_from_" + _lookId; _wearAt = Time.time; _wearFor = seconds; _wearDone = 0; _wearTarget = to.Id; _wearMs = 0L;
            Feathers.Shed(transform.position + Vector3.up * 0.15f, _munin, 14); // (a few of its own feathers loose: the change begins under them)
        }
        private void WearTick()
        {
            if (_wearTo == null) return;
            float t = Mathf.Clamp01((Time.time - _wearAt) / _wearFor);
            int step = Mathf.Min(WearSteps, Mathf.FloorToInt(t * WearSteps));
            if (step <= _wearDone && t < 1f) return;
            _wearDone = step;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            if (t >= 1f)
            {
                string to = _wearTo; _wearTo = null;
                TakeLook(_wearLook);
                Repaint(to, _wearTarget); // heavy: WearSteps times in one change of colours (a scene's step) — a whole repaint each (RavenLooks.Paint), its renderers looked up
                Note("wear «" + _wearTarget + "»: " + WearSteps + " repaints, " + (_wearMs + sw.ElapsedMilliseconds) + " ms in all");
                return;
            }
            string mid = _wearId + "_" + step; // (named by where it came from too: another start, other colours on the way)
            Repaint(StoryLooks.Between(_wearFrom, _wearTo, mid, (float)step / WearSteps), mid); // heavy: as above
            _wearMs += sw.ElapsedMilliseconds;
        }
        internal void Vanish() { Destroy(gameObject); }

        // a thing in its beak (the scene: a letter it brings — StoryScenes.LetterAhead); a picture of the item, gone with the bird (on its jaw)
        private Transform _carry;
        internal void Carry(string prefab)
        {
            Drop();
            _carry = RavenFetch.Hold(gameObject, prefab, false);
            if (_carry == null) Log("nothing in its beak: «" + prefab + "» cannot be shown (no jaw, or no such item)");
        }
        internal void Drop() { if (_carry != null) Destroy(_carry.gameObject); _carry = null; }

        internal void Circle(Character around, float r, float up)
        {
            if (around == null) return;
            _around = around; _ringR = r; _ringUp = up; _hover = false; _via = 0;
            if (State == Landing) SitDown();
            if (State == Sits) TakeOff(Body.Head(around) + Vector3.up * up);
            if (_motor == null) return;
            if (_motor.Body != null) _motor.Body.NewLeg();
            State = Circles; _since = Time.time;
        }

        // the streak: a trail on a follower, never a child of the bird (its life turns every transform under it as a bone)
        private void Streak()
        {
            try
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh == null) return;
                GameObject g = new GameObject("RavenWatch_StoryBirdTrail");
                g.transform.position = transform.position;
                TrailFollow fol = g.AddComponent<TrailFollow>(); fol.Target = transform; fol.At = TailPoint();
                TrailRenderer tr = g.AddComponent<TrailRenderer>();
                Material m = new Material(sh); m.name = "RavenWatch story trail"; m.hideFlags = HideFlags.HideAndDontSave;
                tr.sharedMaterial = m;
                tr.time = TrailSeconds;
                tr.minVertexDistance = 0.15f;
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(TrailRise, 1f), new Keyframe(1f, 0f)); // (narrow where it leaves the bird)
                tr.widthMultiplier = TrailWidth;
                Gradient gr = new Gradient();
                gr.SetKeys(new[] { new GradientColorKey(TrailHead, 0f), new GradientColorKey(new Color(0.6f, 0.8f, 1f), 0.4f), new GradientColorKey(TrailTail, 1f) },
                           new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(TrailPeak, TrailRise), new GradientAlphaKey(TrailPeak * 0.6f, 0.4f), new GradientAlphaKey(0f, 1f) }); // (no hard start at the bird)
                tr.colorGradient = gr;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
                _trail = g.transform;
            }
            catch (System.Exception e) { Log("trail: " + e.Message); }
        }

        // the sparks of its look behind it in the air (the Hall's raven: brown): a few, left where it flew, off while it sits (SparksTick); on
        // a follower as the streak is, never its child. A dot of light each (RavenAura.Dot), as the nest's glow
        internal static float SparksPerMetre = 2.2f, SparksLife = 1.1f;
        private ParticleSystem _sparks;
        private bool _sparksOn = true;
        internal void Sparks(Color a, Color b)
        {
            try
            {
                if (_sparks != null) { ParticleSystem.MainModule now = _sparks.main; now.startColor = new ParticleSystem.MinMaxGradient(a, b); return; } // (another look's: its colours)
                GameObject g = new GameObject("RavenWatch_StoryBirdSparks");
                g.transform.position = transform.position;
                TrailFollow fol = g.AddComponent<TrailFollow>(); fol.Target = transform; fol.At = TailPoint();
                ParticleSystem ps = g.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World; // (left behind where it flew)
                main.startLifetime = new ParticleSystem.MinMaxCurve(SparksLife * 0.6f, SparksLife);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                main.gravityModifier = 0.05f; // (they sink a little)
                main.maxParticles = 40;
                main.startColor = new ParticleSystem.MinMaxGradient(a, b);
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = 0f;
                em.rateOverDistance = SparksPerMetre;
                ParticleSystem.ShapeModule sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.08f;
                Gradient fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(fade);
                ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
                Material m = RavenAura.Dot();
                if (m != null && r != null) r.sharedMaterial = m;
                ps.Play();
                _sparks = ps; _sparksOn = true;
            }
            catch (System.Exception e) { Log("sparks: " + e.Message); }
        }

        private void SparksTick()
        {
            if (_sparks == null) return;
            bool flying = State != Sits && State != Landing;
            if (flying == _sparksOn) return;
            _sparksOn = flying;
            ParticleSystem.EmissionModule em = _sparks.emission;
            em.enabled = flying;
        }

        // where its tail ends, in its own frame: from its body's size (the model's pivot is between its feet)
        private Vector3 TailPoint()
        {
            Bounds b = new Bounds(); bool any = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return new Vector3(0f, 0.2f, -0.25f); // (none seen yet: about a raven's)
            Vector3 c = transform.InverseTransformPoint(b.center);
            Vector3 e = transform.InverseTransformVector(b.extents); e = new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z));
            return new Vector3(0f, c.y, c.z - e.z * 0.85f); // (its middle height, at the back end)
        }

        // the streak's follower: at a point of the bird (its tail), never its child
        internal sealed class TrailFollow : MonoBehaviour
        {
            internal Transform Target;
            internal Vector3 At;
            private void LateUpdate()
            {
                if (Target != null) { transform.position = Target.TransformPoint(At); return; }
                if (!ReferenceEquals(Target, null)) { Target = null; Destroy(gameObject, 2f); }
            }
        }

        // ---- the engine (GraveBird's way)

        private void TakeOff(Vector3 aim)
        {
            Player life = Player.m_localPlayer;
            if (life == null) return;
            foreach (PerchPose pp in GetComponents<PerchPose>()) DestroyImmediate(pp);
            Raven src = Shoulder.Template(_munin);
            if (src != null && src.m_visual != null) { Perches.ToTemplate(gameObject, src.m_visual); Perches.Reveal(gameObject, false); }
            transform.SetParent(null, true);
            _life = gameObject.AddComponent<ShoulderLife>();
            _life.Init(life, _munin, Level);
            _life.Still = true;
            AnimDirector.Take(gameObject, RavenAct.Outing, this, Name);
            _motor = RavenMotor.Of(gameObject, _munin, null);
            Paint();
            if (_motor == null) return;
            _motor.Launch(_motor.TakeoffVelocity(aim, Vector3.zero, Name), Name, 1f);
            State = Flies; _since = Time.time;
        }

        // what keeps it off its seat, as numbers for the log: a roof over the seat, something between it and the seat
        private string Blocked()
        {
            if (Seat == null) return "";
            Vector3 s = Seat.position;
            RaycastHit hit;
            string roof = Physics.Raycast(s + Vector3.up * 0.3f, Vector3.up, out hit, 6f, RavenMotor.Mask) ? "a roof " + (hit.distance + 0.3f).ToString("0.0") + " m over the seat (" + hit.collider.name + ")" : "open sky over the seat";
            Vector3 d = s - transform.position;
            string way = Physics.Raycast(transform.position, d.normalized, out hit, d.magnitude - 0.3f, RavenMotor.Mask) ? hit.collider.name + " between, " + hit.distance.ToString("0.0") + " m from the bird" : "a clear line to it";
            return " — " + roof + ", " + way + ", " + d.magnitude.ToString("0.0") + " m off";
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

        private void LateUpdate()
        {
            if (_seatAt != null && _seatBody == null) _seatAt = null; // (the one it sat on is gone: its seat stays where it was)
            if (_seatAt != null && Seat != null) { Seat.position = _seatAt(); Seat.rotation = Quaternion.Euler(0f, _seatBody.eulerAngles.y, 0f); }
            if (Seat == null) { Destroy(gameObject); return; }
            Unowned();
            WearTick();
            SparksTick();
            ViaTick();
            if (State != _wasState) { _wasState = State; Note(StateName(State) + " at " + Where(transform.position) + ", seat " + Where(Seat.position) + (Player.m_localPlayer != null ? ", " + Mathf.RoundToInt(Vector3.Distance(transform.position, Player.m_localPlayer.transform.position)) + " m from you" : "")); }
            float dt = Time.deltaTime;
            if (State == Sits)
            {
                if (_turning) { Seat.rotation = Quaternion.RotateTowards(Seat.rotation, _faceTo, 120f * dt); _turning = Quaternion.Angle(Seat.rotation, _faceTo) > 1f; }
                return;
            }
            if (_motor == null || _life == null) return;
            if (_motor.Through && State != Passes) _motor.Through = false; // (another order cut its way through the roof: walls are walls again)
            if (State == Away)
            {
                _motor.Steer(_away, _motor.Body != null ? _motor.Body.Style.Cruise : 5f, false, 0f, dt, Vector3.zero, 1f);
                _motor.Finish(dt, 0f);
                if (Vector3.Distance(transform.position, Seat.position) > LeaveGone || Time.time - _since > LeaveSeconds) { Destroy(gameObject); return; }
            }
            else if (State == Circles)
            {
                if (_around == null || _motor.Body == null) { Leave(transform.position + transform.forward * 10f); return; }
                float t = Time.time + _ringPhase;
                Vector3 mid = Body.Head(_around) + Vector3.up * (_ringUp + Mathf.Sin(t * 0.5f) * 0.5f);
                Vector3 aim = CirclePlan.Ring(transform.position, mid, Mathf.Lerp(_ringR * 0.85f, _ringR * 1.15f, Mathf.PerlinNoise(t * 0.12f, _ringPhase)), 34f, 1f);
                // far from its ring (come from 60 m behind you): its full cruise, easing to the ring's slow glide as it nears (the user
                // 2026-11-06: Eirik's raven took ages to come in over you at the glide)
                float off = Vector3.Distance(transform.position, mid);
                _motor.Steer(aim, _motor.Body.Style.Cruise * Mathf.Lerp(0.65f, 1f, Mathf.InverseLerp(RingNear, RingFar, off)), false, 0f, dt, Vector3.zero, 1f);
                _motor.Finish(dt, 0f);
            }
            else if (State == Passes)
            {
                // through the roof: straight at it, slowly, nothing in the way counts (the motor's Through); there — it hangs in the air
                if (_motor.Body == null) { _motor.Through = false; Perches.Puff(gameObject, _munin, "a story bird lost its wings"); return; }
                _motor.Steer(Seat.position, ThroughSpeed, true, 1f, dt, Vector3.zero, 0f);
                _motor.Finish(dt, 0f);
                bool late = Time.time - _since > _passMost;
                if (late || Vector3.Distance(transform.position, Seat.position) < 0.8f)
                {
                    if (late && !_passAgain) { _passAgain = true; _since = Time.time; _passMost = ThroughMost; Note("through: not there yet — on, " + ThroughMost.ToString("0") + " s more (not left in a roof)"); return; } // (smoothly on, no jump: the motor's Through still on)
                    if (late) { Note("through: not there in " + _passMost.ToString("0") + " s, " + Vector3.Distance(transform.position, Seat.position).ToString("0.0") + " m off — it hangs where it got to"); Seat.position = transform.position; } // (there now: Settled, its next leg goes on)
                    _motor.Through = false;
                    State = Hover; _since = Time.time;
                }
            }
            else if (State == Hover || (_hover && (State == Flies || State == Final)))
            {
                // in the air before you (the post raven's way): it never lands, it keeps there facing you
                if (_motor.Body == null) { Perches.Puff(gameObject, _munin, "a story bird lost its wings"); return; }
                Vector3 aim = Seat.position, look = (_faceAt != Vector3.zero ? _faceAt : aim + Seat.forward) - transform.position; look.y = 0f;
                float d = Vector3.Distance(transform.position, aim);
                if (d < 8f && look.sqrMagnitude > 0.01f) _motor.Body.FaceHint(look.normalized, 1f - d / 8f * (State == Hover ? 0f : 1f));
                float left = _motor.Steer(aim, d > 15f ? _motor.Body.Style.Cruise : _motor.Body.Style.Cruise * 0.7f, true, d < 4f ? 1f : 0f, dt, Vector3.zero, d < 6f ? 0.3f : 1f);
                _motor.Finish(dt, d < 3f ? Mathf.Clamp01(1f - d / 3f) * 0.5f : 0f);
                if (State != Hover && left < 1.2f) State = Hover;
                if (State != Hover && Time.time - _since > GiveUp)
                {
                    if (Stuck != null) { _since = Time.time; Stuck(); return; }
                    // (a scene's bird that never got to hang before him — the trees over him, 2026-11-17: the Hall's letter raven made 40 m in 25 s
                    // in the Black Forest and went in a puff: the rest of the way straight, through what is between, as through a roof)
                    Note("never got before him: the rest straight through" + Blocked());
                    Through(Seat.position);
                    return;
                }
            }
            else if (State == Flies || State == Final)
            {
                if (_motor.Body == null) { Perches.Puff(gameObject, _munin, "a story bird lost its wings"); return; }
                if (Time.time - _since > GiveUp)
                {
                    // its seat out of reach (under a roof, behind a wall from where it came): a scene's bird hangs in the air
                    // where it got to — the scene goes on with it there; a person's raven goes (NpcRaven brings it again)
                    if (Stuck != null) { Note("its seat out of reach" + Blocked()); _since = Time.time; Stuck(); return; } // (a place's raven: its place picks again)
                    if (_npc) { Perches.Puff(gameObject, _munin, "a person's raven never got to its seat"); return; }
                    Note("its seat out of reach: it hangs in the air where it got to" + Blocked());
                    Seat.position = transform.position; _hover = true; State = Hover; _since = Time.time;
                    return;
                }
                Vector3 seat = Seat.position, front = Flat(Seat.forward);
                if (State == Flies)
                {
                    Vector3 pre = seat + Side(front) * ComeBack + Vector3.up * ComeUp; // (behind it as a rule; the open side: ComeFrom)
                    _motor.Steer(pre, _motor.Body.Style.Cruise, true, 0f, dt, Vector3.zero, 1f);
                    _motor.Finish(dt, 0f);
                    if (Vector3.Distance(transform.position, pre) < 1.2f || Vector3.Distance(transform.position, seat) < 2f) { _motor.Body.MarkFinal(); State = Final; }
                }
                else
                {
                    Vector3 aim = seat + Vector3.up * FlightBody.LandAbove + Side(front) * FlightBody.LandBehind;
                    float near = Vector3.Distance(transform.position, aim);
                    _motor.Body.FaceHint(front, 1f - Mathf.Min(near, RavenFlight.TurnFrom) / RavenFlight.TurnFrom);
                    float left = _motor.Steer(aim, _motor.Body.HomeSpeed(5f, Vector3.zero, 0f), true, near < 4f ? 2f : 0f, dt, Vector3.zero, 1f);
                    _motor.Finish(dt, Mathf.Clamp01(1f - near / Mathf.Max(0.5f, _motor.Landing().Flare)));
                    if (_motor.Body.CanLand(left, Vector3.zero)) { _life.NextLand = _motor.Landing(); BeginLand(); }
                }
            }
            else if (State == Landing) { LandStep(); return; }
            if (this != null && _life != null && State != Landing) _life.Apply();
        }

        private void BeginLand()
        {
            State = Landing;
            _motor.Grounded();
            _life.VanillaFly = false; _life.Still = false;
            Quaternion seatRot = Seat.rotation * Quaternion.Euler(Shoulder.Rotation);
            _landOff = Quaternion.Inverse(seatRot) * (transform.position - Seat.position);
            _landFromRot = Quaternion.Inverse(seatRot) * transform.rotation;
            _clip = _life.BeginLand();
            _settle = Time.time;
        }

        private void LandStep()
        {
            Quaternion seatRot = Seat.rotation * Quaternion.Euler(Shoulder.Rotation);
            float way;
            bool still;
            if (_clip) still = _life.StepLand(out way);
            else { way = Mathf.Clamp01((Time.time - _settle) / 0.35f); still = way < 1f; way = way * way * (3f - 2f * way); }
            transform.position = Seat.position + seatRot * (_landOff * (1f - way));
            transform.rotation = Quaternion.Slerp(seatRot * _landFromRot, seatRot, Mathf.Clamp01(way * ShoulderPin.LandTurnRate));
            if (!still || Time.time - _settle > 4f) SitDown();
        }

        private void SitDown()
        {
            AnimDirector.Release(gameObject, this);
            Perches.Seat(gameObject, Seat, _munin, MySize, -1, null, null);
            _life = null; _motor = null; State = Sits;
            Paint();
        }

        // its colours on whatever body it wears now (the perch's look and the template's are other renderers) — the
        // shoulder's way (RavenWear.Paint): from the GAME's materials under any eye-glow copy, body renderers only
        private void Paint()
        {
            try
            {
                if (!PaintOn) return;
                if (_lookText == null) return; // (a person's raven in the game's own colours)
                Look look;
                if (!_looks.TryGetValue(_lookId, out look)) { look = Look.Parse(_lookText, _lookId); _looks[_lookId] = look; }
                if (_paint == null) _paint = RavenLooks.Use(look, _munin);
                if (_paint == null) { Note("look not painted: " + RavenLooks.Problem); return; }
                int n = 0;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r is ParticleSystemRenderer || r.GetComponentInParent<FxOn>() != null || r.GetComponentInParent<Outfits.Piece>() != null) continue; // (not what it wears)
                    Material[] cur = r.sharedMaterials;
                    Material[] game = new Material[cur.Length];
                    bool body = false;
                    for (int i = 0; i < cur.Length; i++) { game[i] = OwnOf(EyeGlow.Original(cur[i])); body |= RavenLooks.IsBody(game[i]); } // (under the eyes' copy first, then back from our paint)
                    if (!body) continue;
                    r.sharedMaterials = RavenLooks.Dress(_paint, game);
                    n++;
                }
                ShoulderLife life = GetComponent<ShoulderLife>();
                if (life != null) life.EyesAgain(); // (the eyes' glow made again on the painted body)
                if (n == 0) Note("look: no body renderer found");
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Story bird look: " + e.Message); }
        }

        // a material already painted by us: back to the game's one it was copied from (painted again, never a copy of a copy)
        private Material OwnOf(Material m)
        {
            if (m == null || _paint == null) return m;
            foreach (KeyValuePair<Material, Material> kv in _paint.Mats) if (kv.Value == m) return kv.Key;
            return m;
        }

        internal static string StateName(int s) { return s == Circles ? "circles" : s == Sits ? "sits" : s == Flies ? "flies in" : s == Final ? "final way in" : s == Landing ? "landing" : s == Away ? "away" : s == Hover ? "hangs in the air" : s == Passes ? "through the roof" : s.ToString(); }

        // the author's look at it (raven story bird)
        internal string Describe()
        {
            Player p = Player.m_localPlayer;
            int on = 0, all = 0;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) { all++; if (r.enabled && r.gameObject.activeInHierarchy) on++; }
            string mats = "";
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r != null && !(r is ParticleSystemRenderer)) foreach (Material m in r.sharedMaterials) if (m != null) mats += " [" + r.name + ": " + m.name + " / " + (m.shader != null ? m.shader.name : "-") + (m.HasProperty("_Color") ? " a=" + m.color.a.ToString("0.00") : "") + "]";
            return StateName(State) + " at " + Where(transform.position) + (p != null ? ", " + Mathf.RoundToInt(Vector3.Distance(transform.position, p.transform.position)) + " m from you" : "")
                + ", seat " + (Seat != null ? Where(Seat.position) : "-") + ", renderers " + on + "/" + all + ", painted " + (_paint != null) + mats;
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_trail != null) { TrailFollow f = _trail.GetComponent<TrailFollow>(); if (f != null) f.Target = null; Destroy(_trail.gameObject, TrailSeconds + 0.2f); } // (the streak fades where it was)
            if (_sparks != null) { TrailFollow f = _sparks.GetComponent<TrailFollow>(); if (f != null) f.Target = null; ParticleSystem.EmissionModule em = _sparks.emission; em.enabled = false; Destroy(_sparks.gameObject, SparksLife + 0.2f); } // (the last ones fade where they are)
            Note("gone at " + Where(transform.position) + " (" + StateName(State) + ")");
            if (!_yielded) AnimDirector.Release(gameObject, this);
            RavenLooks.Drop(_paint); _paint = null;
            if (Seat != null) Destroy(Seat.gameObject);
        }
    }
}
#endif
