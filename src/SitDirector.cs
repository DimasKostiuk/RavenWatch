#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Система сидіння», the director: what a sitting raven does and how it feels, decided in one place and the same
    // on every game. The owner's game decides (only it knows that you eat, craft, were hit, killed something); the
    // others play what it decided. Its mood (calm, alert, curious, drowsy) colours everything; what it does are sitting
    // templates (SitTemplates): its own doings now and then, and its reactions to what you do. Each one is played from a
    // seed, so every game plays it alike.
    internal enum SitMood { Calm = 0, Alert = 1, Curious = 2, Drowsy = 3 }
    internal enum SitEv { Hurt, Boss, Kill, Work, Eat, Pick, Pet, Fall }

    // ---------------------------------------------------------------- what happens to you (the owner's game)
    internal static class SitEvents
    {
        internal sealed class E { public int Id; public SitEv Kind; public float At; public int Taken; public bool Forced; } // Taken: 1 Hugin, 2 Munin (bits)
        internal static readonly List<E> Recent = new List<E>();
        private static int _next = 1;
        internal static float Keep = 3f; // s an event stays to be reacted to

        // forced: the test window's «as if» (both react, no chance, no rest)
        internal static void Raise(SitEv kind, bool forced = false)
        {
            for (int i = Recent.Count - 1; i >= 0; i--) if (Time.time - Recent[i].At > Keep) Recent.RemoveAt(i);
            if (!forced && Recent.Count > 0 && Recent[Recent.Count - 1].Kind == kind && Time.time - Recent[Recent.Count - 1].At < 0.2f) return; // (the same blow told by both ravens)
            Recent.Add(new E { Id = _next++, Kind = kind, At = Time.time, Forced = forced });
        }

        // what your diary notes, as the ravens see it
        internal static void FromDiary(Ev type)
        {
            switch (type)
            {
                case Ev.Kill: Raise(SitEv.Kill); break;
                case Ev.Build: case Ev.Craft: case Ev.Cook: case Ev.Chop: case Ev.Mine: case Ev.Fish:
                case Ev.Plant: case Ev.Smelt: case Ev.Ferment: case Ev.Repair: Raise(SitEv.Work); break;
                case Ev.Eat: Raise(SitEv.Eat); break;
                case Ev.Pick: case Ev.Loot: case Ev.Trophy: Raise(SitEv.Pick); break;
                case Ev.Pet: Raise(SitEv.Pet); break;
                case Ev.Fall: Raise(SitEv.Fall); break;
            }
        }

        // the situation (SitTemplates) an event is
        internal static int SitOf(SitEv e)
        {
            switch (e)
            {
                case SitEv.Hurt: return SitTemplates.Hurt; case SitEv.Fall: return SitTemplates.Fall; case SitEv.Boss: return SitTemplates.Boss;
                case SitEv.Kill: return SitTemplates.Kill; case SitEv.Work: return SitTemplates.Work; case SitEv.Eat: return SitTemplates.Eat;
                case SitEv.Pick: return SitTemplates.Loot; default: return SitTemplates.Pet;
            }
        }
    }

    // ---------------------------------------------------------------- the network: one number per raven
    // In the owner's player data (sent with it anyway; nothing sent on its own), 63 bits: the template (15: its name's
    // hash + 1, 0 nothing), its seed (8), when it began on the network clock (22 bits of tenths of a second), the mood
    // (2), the situation (4: a game lacking that template plays one of its own for it), where it looks (on 1, yaw 6,
    // pitch 5).
    internal static class SitNet
    {
        internal static readonly int KeyH = "RavenWatch_SitH".GetStableHashCode(), KeyM = "RavenWatch_SitM".GetStableHashCode();
        private const long Mask22 = 0x3FFFFF;
        internal const float LookYaw = 75f, LookPitch = 35f;

        internal static double Seconds { get { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time; } }
        internal static long Now { get { return (long)(Seconds * 10.0) & Mask22; } }
        internal static float Age(long start) { return ((Now - start) & Mask22) / 10f; }

        internal static long Pack(int tpl, int seed, long start, int mood, int sit, bool look, float yaw, float pitch)
        {
            return ((long)tpl & 0x7FFF) | ((long)(seed & 255) << 15) | ((start & Mask22) << 23) | ((long)(mood & 3) << 45) | ((long)(sit & 15) << 47)
                | ((look ? 1L : 0L) << 51) | (Q(yaw / LookYaw, 31) << 52) | (Q(pitch / LookPitch, 15) << 58);
        }
        internal static int Tpl(long v) { return (int)(v & 0x7FFF); }
        internal static int Seed(long v) { return (int)((v >> 15) & 255); }
        internal static long Start(long v) { return (v >> 23) & Mask22; }
        internal static int Mood(long v) { return (int)((v >> 45) & 3); }
        internal static int Sit(long v) { return (int)((v >> 47) & 15); }
        internal static bool Look(long v) { return ((v >> 51) & 1) != 0; }
        internal static float Yaw(long v) { return U((v >> 52) & 63, 31) * LookYaw; }
        internal static float Pitch(long v) { return U((v >> 58) & 31, 15) * LookPitch; }
        private static long Q(float x, int half) { return Mathf.RoundToInt(Mathf.Clamp(x, -1f, 1f) * half) + half + 1; }
        private static float U(long q, int half) { return (q - half - 1) / (float)half; }
    }

    // ---------------------------------------------------------------- one raven's sitting director
    internal sealed class SitDirector
    {
        // how often it does something of its own (s, before its nerves and mood), the chances it reacts (by situation)
        internal static float DoingEveryHugin = 20f, DoingEveryMunin = 7f;
        internal static readonly float[] HuginChance = { 1f, 1f, 0.8f, 1f, 0.45f, 0.35f, 0.3f, 0.3f, 0.9f, 1f };  // by situation (Doing, Pair: unused)
        internal static readonly float[] MuninChance = { 1f, 1f, 0.8f, 1f, 0.75f, 0.5f, 0.7f, 0.5f, 0.9f, 1f };
        internal static readonly float[] Cooldown = { 0f, 1.5f, 3f, 6f, 8f, 15f, 25f, 10f, 8f, 0f };
        internal static float PairShare = 0.3f; // of its own doings, this share is something with the other raven (when it is there and free)
        internal static float WorkCuriousSeconds = 25f;

        // what plays now (both sides)
        internal SitRun Run;
        private float _actAt, _shownAt;
        internal int Mood = (int)SitMood.Calm;
        internal readonly float[] MoodW = { 1f, 0f, 0f, 0f };
        internal bool LookOn; internal float LookYaw, LookPitch;
        internal float HoldU = -1f;            // the test window: held at this share of its time (-1: plays)
        internal int ForceMood = -1;           // the test window: this mood (-1: as it is)

        // the owner's own book
        private int _seen;
        private float _moodAt, _curiousUntil = -1f, _doingNext = -1f;
        private readonly float[] _sitLast = new float[SitTemplates.SitCount];
        private readonly Dictionary<SitTemplate, float> _played = new Dictionary<SitTemplate, float>();
        private SitTemplate _pendingT;
        private int _pendingSit;
        private float _pendingAt;
        internal bool ShakeWanted;             // a quirk asked for a shake now (ShoulderLife.ShakeNow)
        internal string Wanted; internal float WantedAt, WantedUntil; // a quirk's moment asks this act by name (ShoulderLife.Act): from WantedAt, as soon as it may, else forgotten
        internal void Want(string template, float after, float wait) { Wanted = template; WantedAt = Time.time + after; WantedUntil = WantedAt + wait; }
        private int _pubTpl, _pubSeed, _pubSit;
        private long _pubStart;
        private long _written = long.MinValue, _read = long.MinValue;
        // who it is (the owner's game: its character on every game, its quirk in its item), looked at once a second
        private string _charId = "", _quirkId = "";
        private float _shows = 1f, _whoAt = -1f;
        private Norov.Character _char;

        internal SitDirector() { for (int i = 0; i < _sitLast.Length; i++) _sitLast[i] = -100f; }

        internal float ActT { get { return Run == null ? 0f : HoldU >= 0f ? HoldU * Run.Seconds : Time.time - _actAt; } }
        internal float ActW { get { return Run == null ? 0f : HoldU >= 0f ? 1f : Run.Weight(ActT, Time.time - _shownAt); } }
        internal bool Playing { get { return Run != null && ActW > 0f; } }

        // the mood's colour on the sitting
        internal float RateMul { get { return 1f + 0.1f * MoodW[1] - 0.4f * MoodW[3] - 0.45f * Doze; } }
        internal float MoodHeadPitch { get { return -6f * MoodW[1] + 14f * MoodW[3] + DozeHead * Doze; } }
        internal float MoodHunch { get { return -4f * MoodW[1] + 2f * MoodW[2] + 7f * MoodW[3] + DozeHunch * Doze; } }

        // drowsy a while, it nods off: 0 awake … 1 asleep on its seat (the head tucked low, still, the breath slow, no
        // doings, no looking about); a strong act (a blow, a boss…) or another mood wakes it at once. From the mood and
        // the act, so every game alike
        internal float Doze;
        internal static float DozeAfter = 12f, DozeIn = 5f, DozeOut = 0.6f, DozeHead = 22f, DozeHunch = 5f;
        internal const int WakePrio = 4;
        private float _drowsySince = -1f;
        internal float GlanceMul { get { return Mathf.Max(0.3f, 1f - 0.5f * MoodW[1] - 0.4f * MoodW[2] + 1.5f * MoodW[3]); } }

        internal string Describe() { return "настрій " + SitTemplates.MoodNames[Mathf.Clamp(Mood, 0, 3)].ToLowerInvariant() + (Doze > 0.01f ? ", спить " + Mathf.RoundToInt(Doze * 100f) + "%" : "") + (Playing ? ", «" + Run.T.Name + "»" : ""); }

        // One frame (only while it sits under the sitting system). The game that decides for it decides and writes; the
        // others read and play.
        internal void Tick(ISitBird b)
        {
            bool decides = b.BirdDecides;
            if (decides) Decide(b); else Follow(b);
            if (Run != null && HoldU < 0f && ActT >= Run.Seconds) Run = null;
            for (int i = 0; i < MoodW.Length; i++) MoodW[i] = Mathf.MoveTowards(MoodW[i], i == Mood ? 1f : 0f, Time.deltaTime);
            bool drowsy = Mood == (int)SitMood.Drowsy && !(Playing && (Run.T.Prio >= WakePrio || Run.Sit != SitTemplates.Doing || !Run.T.Moods[(int)SitMood.Drowsy])); // (a reaction to you, or any lively act, wakes it)
            if (!drowsy) _drowsySince = -1f; else if (_drowsySince < 0f) _drowsySince = Time.time;
            float doze = drowsy && Time.time - _drowsySince >= DozeAfter ? 1f : 0f;
            Doze = Mathf.MoveTowards(Doze, doze, Time.deltaTime / (doze > Doze ? DozeIn : DozeOut));
            if (decides) Publish(b);
        }

        // ---- the deciding game
        private void Decide(ISitBird b)
        {
            bool munin = b.BirdMunin;
            Who(b);
            if (Time.time >= _moodAt)
            {
                _moodAt = Time.time + 0.5f;
                bool alert, drowsy;
                b.BirdMood(out alert, out drowsy);
                bool night = EnvMan.instance != null && EnvMan.IsNight();
                SitMood m;
                if (alert) m = SitMood.Alert;
                else if (drowsy || (_quirkId == "sleepy" && night)) m = SitMood.Drowsy; // (a sleepyhead nods off at night whatever you do)
                else if (Time.time < _curiousUntil) m = SitMood.Curious;
                else m = SitMood.Calm;
                Mood = ForceMood >= 0 ? ForceMood : (int)m;
            }
            if (HoldU >= 0f) return; // the test window holds a pose
            // what happened to you (a raven on your shoulder): each event once; its chance by the raven; the other raven
            // less often and a moment later
            if (b.BirdHears)
                foreach (SitEvents.E e in SitEvents.Recent)
                {
                    if (e.Id <= _seen) continue;
                    _seen = e.Id;
                    if (Time.time - e.At > 1f) continue;
                    int sit = SitEvents.SitOf(e.Kind);
                    if (e.Kind == SitEv.Work) _curiousUntil = Time.time + WorkCuriousSeconds;
                    int mine = munin ? 2 : 1, other = munin ? 1 : 2;
                    bool after = (e.Taken & other) != 0;
                    if (!e.Forced)
                    {
                        if (Time.time - _sitLast[sit] < Cooldown[sit]) continue;
                        float chance = (munin ? MuninChance[sit] : HuginChance[sit]) * Mathf.Clamp(b.BirdNerves, 0.8f, 1.3f) * (after ? 0.55f : 1f) * (_char != null ? Norov.Mul(_char.SitReact, _shows) : 1f);
                        if (e.Kind == SitEv.Hurt || e.Kind == SitEv.Boss) chance = 1f; // a blow and a boss: both, always
                        if (Random.value > chance) continue;
                    }
                    SitEvents.E ev = e;
                    SitTemplate t = SitTemplates.Pick(sit, Mood, munin, x => Mine(x) && (ev.Forced || Rested(x, b)), Random.value, Weight);
                    if (t == null || (_pendingT != null && _pendingT.Prio >= t.Prio)) continue; // (a stronger one waits already: this event is not its)
                    _sitLast[sit] = Time.time;
                    e.Taken |= mine;
                    _pendingT = t; _pendingSit = sit;
                    _pendingAt = Time.time + (after ? Random.Range(0.15f, 0.5f) : Random.Range(0f, munin ? 0.12f : 0.2f));
                }
            if (_pendingT != null && Time.time >= _pendingAt)
            {
                SitTemplate t = _pendingT; _pendingT = null;
                bool busyHigher = Playing && Run.T.Prio >= t.Prio;
                bool answer = _answering; _answering = false;
                if (!busyHigher && !b.BirdAsleep) { Start(b, t, _pendingSit, Random.Range(0, 256), !answer); b.BirdDid(Run.Seconds, true); }
            }
            if (Playing) return;
            // a quirk's moment (the grumbler grumbles, the show-off shows off, the picky one turns its beak away): its own act
            if (Wanted != null && (Time.time >= WantedUntil || Doze > 0.01f)) Wanted = null;
            if (Wanted != null && Time.time >= WantedAt && !b.BirdDroopy && b.BirdMayDo())
            {
                SitTemplate wt = SitTemplates.Find(Wanted); Wanted = null;
                if (wt != null && wt.For(munin) && ClipOk(wt, b)) { Start(b, wt, SitTemplates.Doing, Random.Range(0, 256), false); b.BirdDid(Run.Seconds, true); return; }
            }
            // its own doings, now and then, when its busy-idle budget lets it
            float every = (munin ? DoingEveryMunin : DoingEveryHugin) / b.BirdNerves * (MoodW[3] > 0.5f ? 1.6f : 1f) * (MoodW[1] > 0.5f ? 2f : 1f) / (_char != null ? Norov.Mul(_char.SitPace, _shows) : 1f);
            if (_doingNext < 0f) _doingNext = Time.time + Random.Range(0.3f, 1.2f) * every;
            if (b.BirdDroopy || (Doze > 0.01f && !ShakeWanted) || (!ShakeWanted && Time.time < _doingNext)) return; // (asleep on its seat: no doings; wet, it shakes awake)
            if (!b.BirdMayDo()) { _doingNext = Time.time + Random.Range(2f, 5f); return; }
            bool shake = ShakeWanted;
            System.Func<SitTemplate, bool> ok = x => Mine(x) && Rested(x, b) && (!shake || x.Shake) && ClipOk(x, b) && (!x.Step || b.BirdSpeed < 1f) && (x.Reach <= 0f || b.BirdOnPerch);
            int dsit = SitTemplates.Doing;
            SitTemplate d = null;
            if (!shake && Random.value < PairShare && PartnerFree(b)) { d = SitTemplates.Pick(SitTemplates.Pair, Mood, munin, ok, Random.value, Weight); if (d != null) dsit = SitTemplates.Pair; }
            if (d == null) d = SitTemplates.Pick(SitTemplates.Doing, shake ? (int)SitMood.Calm : Mood, munin, ok, Random.value, Weight); // (a shake: whatever its mood)
            ShakeWanted = false;
            _doingNext = Time.time + Random.Range(0.5f, 1.5f) * every;
            if (d == null) return;
            Start(b, d, dsit, Random.Range(0, 256));
            b.BirdDid(Run.Seconds, false);
        }

        // who this raven is: its character (as far as its trust shows it) and its quirk (where this game knows it)
        private void Who(ISitBird b)
        {
            if (Time.time < _whoAt) return;
            _whoAt = Time.time + 1f;
            _munin = b.BirdMunin;
            _char = Norov.OnPlayer(b.BirdOwner, b.BirdMunin);
            _charId = _char != null ? _char.Id : "";
            _quirkId = b.BirdQuirk ?? "";
            _shows = Norov.Shows(b.BirdLevel);
        }

        // a template only for some (its need) is this raven's when one of them is it
        private bool Mine(SitTemplate t)
        {
            if (!t.Need) return true;
            foreach (string id in t.Suits) if ((id == _charId && _charId.Length > 0) || (id == _quirkId && _quirkId.Length > 0)) return true;
            return false;
        }

        // how often this raven takes it: the template's weight, ×2 for its character (as far as it shows), ×3 for its quirk
        private float Weight(SitTemplate t)
        {
            float w = t.WeightFor(_munin);
            foreach (string id in t.Suits)
            {
                if (id == _charId && _charId.Length > 0) w *= Norov.Mul(2f, _shows);
                else if (id == _quirkId && _quirkId.Length > 0) w *= 3f;
            }
            return w;
        }
        private bool _munin;

        private static bool PartnerFree(ISitBird b) { ISitBird p = b.BirdPartner; return p != null && !p.BirdDir.Playing && p.BirdDir.HoldU < 0f && p.BirdDir.Mood != (int)SitMood.Drowsy; }

        // the other raven asks an answer of this one (its template, a moment later); a stronger one waiting stays
        internal void Answer(SitTemplate t, float delay)
        {
            if (t == null || HoldU >= 0f || (_pendingT != null && _pendingT.Prio >= t.Prio)) return;
            _pendingT = t; _pendingSit = SitTemplates.Pair; _pendingAt = Time.time + delay; _answering = true;
        }
        private bool _answering;

        private static bool ClipOk(SitTemplate t, ISitBird b) { return t.Clip < 0 || b.BirdHasClip(t.Clip == 7 ? SitClip.Idle2 : (SitClip)t.Clip); }

        // its rest since it last played this one (Hugin longer, Munin shorter, a young raven less)
        private bool Rested(SitTemplate t, ISitBird b)
        {
            float at;
            if (!_played.TryGetValue(t, out at)) return true;
            return Time.time - at >= t.Rest * (b.BirdMunin ? 0.6f : 1.3f) / b.BirdNerves;
        }

        // the test window: play this one now on your raven (sent as any other)
        internal void PlayNow(ISitBird b, SitTemplate t)
        {
            int sit = 0;
            for (int i = 0; i < SitTemplates.SitCount; i++) if (t.Sits[i]) { sit = i; break; }
            Start(b, t, sit, Random.Range(0, 256));
        }

        // an act begins here now (the deciding game); it goes out with the next publish. 'ask': the other raven may answer it
        private void Start(ISitBird b, SitTemplate t, int sit, int seed, bool ask = true)
        {
            Begin(b, t, sit, seed, 0f);
            _played[t] = Time.time;
            _pubTpl = t.Id + 1; _pubSeed = seed; _pubSit = sit; _pubStart = SitNet.Now;
            if (!ask || t.Answer.Length == 0 || Random.value > t.AnswerChance) return;
            ISitBird p = b.BirdPartner;
            if (p != null) p.BirdDir.Answer(SitTemplates.Find(t.Answer), Random.Range(t.AnswerMin, Mathf.Max(t.AnswerMin, t.AnswerMax)));
        }

        private void Publish(ISitBird b)
        {
            long v = SitNet.Pack(Playing ? _pubTpl : 0, _pubSeed, _pubStart, Mood, _pubSit, LookOn, LookYaw, LookPitch);
            if (v == _written) return;
            ZDO zdo = b.BirdZdo(true);
            if (zdo == null) return;
            zdo.Set(b.BirdKey, v);
            _written = v;
        }

        // ---- the others: what the deciding game decided
        private void Follow(ISitBird b)
        {
            ZDO zdo = b.BirdZdo(false);
            if (zdo == null) return;
            long v = zdo.GetLong(b.BirdKey, 0L);
            if (v == _read) return;
            long was = _read;
            _read = v;
            Mood = SitNet.Mood(v);
            LookOn = SitNet.Look(v); LookYaw = SitNet.Yaw(v); LookPitch = SitNet.Pitch(v);
            int tpl = SitNet.Tpl(v);
            bool fresh = was == long.MinValue || SitNet.Tpl(was) != tpl || SitNet.Seed(was) != SitNet.Seed(v) || SitNet.Start(was) != SitNet.Start(v);
            if (!fresh || tpl == 0) return;
            int sit = Mathf.Clamp(SitNet.Sit(v), 0, SitTemplates.SitCount - 1), seed = SitNet.Seed(v);
            // the decider's template, or (another set of templates here) one of ours for the same situation, by its seed
            SitTemplate t = SitTemplates.FindId(tpl - 1);
            if (t == null) t = SitTemplates.Pick(sit, Mood, b.BirdMunin, null, (seed + 0.5f) / 256f);
            if (t == null) return;
            float age = SitNet.Age(SitNet.Start(v));
            SitRun run = SitRun.Make(t, seed, sit, b.BirdMunin, b);
            if (age < run.Seconds) Begin(b, t, sit, seed, age); // (late: picked up where it is now; over: not played)
        }

        // a template played on this bird, 'age' s in (the same on every game)
        private void Begin(ISitBird b, SitTemplate t, int sit, int seed, float age)
        {
            Run = SitRun.Make(t, seed, sit, b.BirdMunin, b);
            _actAt = Time.time - age; _shownAt = Time.time;
            b.BirdPlay(Run, age);
        }

        // the deciding game's look this frame (ShoulderLife.Turn): sent with the rest
        internal void SetLook(bool on, float yaw, float pitch) { LookOn = on; LookYaw = yaw; LookPitch = pitch; }
    }

    // a sitting raven, wherever it sits (on a shoulder: ShoulderLife; on a perch: PerchLife), as the sitting director sees it
    internal interface ISitBird
    {
        bool BirdMunin { get; }
        int BirdLevel { get; }
        float BirdNerves { get; }
        bool BirdAsleep { get; }      // no acts at all
        bool BirdDroopy { get; }      // no doings of its own (starving, sulking; on a perch: an enemy near)
        float BirdSpeed { get; }      // its seat's speed (a step aside only on a still one)
        bool BirdOnPerch { get; }
        Player BirdOwner { get; }     // its owner, when in the world (its character); may be null
        bool BirdDecides { get; }     // this game decides for it and writes it: yours on your shoulder, a perch's holder on it
        bool BirdHears { get; }       // it reacts to what you do (SitEvents): on your shoulder
        string BirdQuirk { get; }     // its quirk, where this game knows it ("" else)
        ZDO BirdZdo(bool write);      // where its decisions travel
        int BirdKey { get; }
        ISitBird BirdPartner { get; } // the other raven by it, sitting under the sitting system, awake
        SitDirector BirdDir { get; }
        bool BirdHasClip(SitClip k);
        float BirdClipLength(int clip);
        void BirdPlay(SitRun r, float age); // a template begins on it, 'age' s in
        bool BirdMayDo();             // its busy-idle budget lets it do something of its own now
        void BirdDid(float seconds, bool reaction);
        void BirdMood(out bool alert, out bool drowsy); // what its senses say (the deciding game)
    }
}
#endif
