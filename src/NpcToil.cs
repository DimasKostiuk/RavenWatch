#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A worker's day has its price (the user, 2026-10-30: no work for free):
    //   hunger      it works it off — heavy work (lumberjack, miner, guard) three times as fast, the rest a little faster (NpcHunger)
    //   tiredness   0–100: it grows while it works (heavy work quicker); at the top it puts its work down and sits — on a
    //               bench or by a fire it is rested soonest, on the ground slowly, asleep in its bed well
    //   mood        0–100: its bed under a roof, a fire burning in its base, fed, not always the same food, not worn out,
    //               not wet in the rain, paid, a module's share (MoodFrom: its wish come true, or wanted long — NpcGoals; woken from its sleep — NpcWake; one of its home it cannot stand, a friend there,
    //               a feast — NpcNeighbours) — its work ×0.7 at the worst to ×1.2 at the best (a friend at work near it: ×NpcNeighbours.WorkWith); at the very bottom three
    //               game days on end it leaves (to the ravens' home, as a starved one does)
    //   pay         every five game days it walks up to its heart and takes its pay from the heart's coin box («Скарбничка»)
    //               itself — a few coins by its work and how far the world has gone; none there: unpaid, its mood down (and one
    //               of its base who was paid may be envied: NpcAttitude.Quarrel «pay»)
    //   its craft   a newcomer works at ×0.6 of a hand who knows it; every pay it takes up +0.1, to ×1 (kept per job, in its
    //               locker); a story person knows its work already
    // Where it is owned; tiredness, mood and its pay's state in its ZDO (written now and then, when changed), its craft
    // in its locker (written on a payday).
    internal sealed class NpcToil : MonoBehaviour
    {
        internal static readonly int KTired = "RavenWatch_npcTired".GetStableHashCode();   // float 0–100
        internal static readonly int KMood = "RavenWatch_npcMood".GetStableHashCode();     // float 0–100
        internal static readonly int KLow = "RavenWatch_npcMoodLow".GetStableHashCode();   // float: seconds at the bottom
        internal static readonly int KWhy = "RavenWatch_npcMoodWhy".GetStableHashCode();   // int: what weighs on it most (Why)
        internal static readonly int KPayNext = "RavenWatch_npcPayNext".GetStableHashCode(); // long: world seconds of its next pay
        internal static readonly int KUnpaid = "RavenWatch_npcUnpaid".GetStableHashCode(); // int: pays missed in a row (−1 never paid yet)
        internal static readonly int KCraft = "RavenWatch_npcCraft".GetStableHashCode();   // string «job=0.6;…» — in its locker
        internal static readonly int KMoodParts = "RavenWatch_npcMoodParts".GetStableHashCode(); // string «why=points;…»: what its mood is made of now, as Weigh last counted it (its card's tip) — in its locker, only when changed
        internal const float PayDays = 5f, MoodLow = 15f, MoodPoor = 30f;
        private const float Tick = 5f, KeepEvery = 120f, Newcomer = 0.6f, Learn = 0.1f, LeaveDays = 3f;

        private NpcActor _a;
        private bool _read;
        private float _tired, _mood = 60f, _low, _keptTired, _keptMood, _keptLow, _next, _nextKeep, _nextMood, _target = 60f;
        private NpcMind.Plan _rest, _pay;
        private float _paySince;
        private readonly List<string> _ate = new List<string>(); // (its last meals: not kept — at a reload the mood is not hurt by it)

        private void Awake() { _a = GetComponent<NpcActor>(); }

        // ---------------------------------------------------------------- by the work
        // heavy 3, middling 1.5, light 1.3 — how fast its hunger and its tiredness go while it works
        private static float Load(string job)
        {
            switch (job)
            {
                case "lumber": case "miner": case "guard": return 3f;
                case "cleaner": case "servant": return 1.3f;
                case "": return 1f;
                default: return 1.5f;
            }
        }

        internal static bool Working(NpcActor a)
        {
            return a != null && a.Worker != null && a.Worker.Works && !a.Worker.Idle && a.Mind != null && a.Mind.NowLayer == NpcMind.Layer.Job;
        }

        // × its hunger's pace (NpcHunger)
        internal static float HungerLoad(NpcActor a) { return Working(a) ? Load(a.Worker.JobId) : 1f; }

        // its work's load whether or not it is at it now (NpcHunger.NeedPerDay): its job alone, so any game reads the same
        internal static float WorkLoad(NpcActor a) { return a != null && a.Worker != null && a.Worker.Works ? Load(a.Worker.JobId) : 1f; }

        // × its work's pace (NpcWorker, NpcHew): its mood, its craft, a friend at work near it (NpcNeighbours)
        internal static float Pace(NpcActor a)
        {
            if (a == null) return 1f;
            float m = Mood(a);
            float byMood = m < 50f ? 0.7f + 0.3f * m / 50f : 1f + 0.2f * (m - 50f) / 50f;
            return byMood * Craft(a, a.Worker != null ? a.Worker.JobId : "") * NpcNeighbours.WorkPace(a);
        }

        // ---------------------------------------------------------------- read on every game (its ZDO)
        internal static float Tired(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? NpcLocker.Num(z, KTired, 0f) : 0f; }
        internal static float Mood(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? NpcLocker.Num(z, KMood, 60f) : 60f; }
        internal static int Unpaid(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? NpcLocker.Int(z, KUnpaid, -1) : -1; }

        // its craft at a job (a newcomer ×0.6 … ×1)
        internal static float Craft(NpcActor a, string job)
        {
            if (a == null || string.IsNullOrEmpty(job)) return 1f;
            if (a.Def != null && !a.Def.Generated) return 1f; // (a story person knows its work)
            string kept = NpcLocker.Get(a, KCraft);
            if (kept == null) return Newcomer; // (its locker not here yet)
            foreach (string part in kept.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0 && part.Substring(0, eq) == job) return Mathf.Clamp(Errands.F(part.Substring(eq + 1), Newcomer), Newcomer, 1f);
            }
            return Newcomer;
        }

        internal static void Learned(NpcActor a, string job)
        {
            float now = Mathf.Min(1f, Craft(a, job) + Learn);
            string kept = NpcLocker.Get(a, KCraft) ?? "";
            StringBuilder sb = new StringBuilder();
            foreach (string part in kept.Split(';')) { if (part.Length == 0 || part.StartsWith(job + "=", System.StringComparison.Ordinal)) continue; sb.Append(part).Append(';'); }
            sb.Append(job).Append('=').Append(now.ToString("0.##", CultureInfo.InvariantCulture));
            NpcLocker.Set(a, KCraft, sb.ToString());
        }

        // ---------------------------------------------------------------- what weighs on it (its mood's worst)
        // (kept as a number in its locker: new ones only at the end; those from Bed on lift it, Pining weighs a little)
        internal enum Why { None, NoBed, BedOpen, Hungry, Starving, SameFood, Tired, Wet, Unpaid, NoFire, Bed, BedRoof, Fire, Fed, Varied, Paid, Wish, Pining, Woken, Foe, Friend, Feast } // (kept by number in its locker: a new one at the end only)
        internal static string WhyWord(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            return WhyWord((Why)(z != null ? NpcLocker.Int(z, KWhy, 0) : 0));
        }

        private static string WhyWord(Why w)
        {
            switch (w)
            {
                case Why.NoBed: return Lang.T("нема свого ліжка");
                case Why.BedOpen: return Lang.T("ліжко не під дахом");
                case Why.Hungry: return Lang.T("голодно");
                case Why.Starving: return Lang.T("дуже голодно");
                case Why.SameFood: return Lang.T("все та сама їжа");
                case Why.Tired: return Lang.T("втома");
                case Why.Wet: return Lang.T("мокне під дощем");
                case Why.Unpaid: return Lang.T("не платять");
                case Why.NoFire: return Lang.T("на базі не горить вогонь");
                case Why.Bed: return Lang.T("має своє ліжко");
                case Why.BedRoof: return Lang.T("ліжко під дахом");
                case Why.Fire: return Lang.T("тепло біля вогню");
                case Why.Fed: return Lang.T("ситно");
                case Why.Varied: return Lang.T("їжа різна");
                case Why.Paid: return Lang.T("платять вчасно");
                case Why.Wish: return Lang.T("збулося те, про що мріяв|мріяла");
                case Why.Pining: return Lang.T("довго чекає на те, про що мріє");
                case Why.Woken: return Lang.T("розбудили серед сну");
                case Why.Foe: return Lang.T("живе поруч із тим, кого не терпить");
                case Why.Friend: return Lang.T("поруч друг");
                case Why.Feast: return Lang.T("недавно було свято");
                default: return "";
            }
        }

        // its mood in a word, no number (its card under «настрій», the board)
        internal static string MoodWord(NpcActor a)
        {
            float m = Mood(a);
            return a.Form(m < MoodLow ? Lang.T("на межі") : m < MoodPoor ? Lang.T("пригнічений|пригнічена") : m < 45f ? Lang.T("похмурий|похмура") : m < 70f ? Lang.T("спокійний|спокійна") : Lang.T("веселий|весела"));
        }

        // what it is made of (the tip under the pointer): «+ …» what lifts it, «− …» what weighs on it, a line each, the heavy first; as its owner last counted it
        internal static string MoodTip(NpcActor a)
        {
            List<KeyValuePair<float, Why>> parts = new List<KeyValuePair<float, Why>>();
            foreach (string e in (NpcLocker.Get(a, KMoodParts) ?? "").Split(';'))
            {
                int eq = e.IndexOf('='), w; float v;
                if (eq > 0 && int.TryParse(e.Substring(0, eq), NumberStyles.Integer, CultureInfo.InvariantCulture, out w) && float.TryParse(e.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) parts.Add(new KeyValuePair<float, Why>(v, (Why)w));
            }
            parts.Sort((x, y) => Mathf.Abs(y.Key).CompareTo(Mathf.Abs(x.Key)));
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<float, Why> p in parts) { string word = WhyWord(p.Value); if (word.Length > 0) sb.Append(sb.Length > 0 ? "\n" : "").Append(p.Key > 0f ? "+ " : "− ").Append(word); }
            return a.Form(sb.ToString());
        }

        // how rested, in words (its card): tone 0 fine · 2 tired · 3 worn out
        internal static string RestWord(NpcActor a, out int tone)
        {
            float t = Tired(a);
            tone = t >= 90f ? 3 : t >= 60f ? 2 : 0;
            return a.Form(t >= 90f ? Lang.T("знесилений|знесилена") : t >= 60f ? Lang.T("втомлений|втомлена") : Lang.T("бадьорий|бадьора"));
        }

        // its craft in a word (its card): a newcomer ×0.6 … ×1
        internal static string CraftWord(NpcActor a)
        {
            float c = Craft(a, a.Worker != null ? a.Worker.JobId : "");
            return c >= 0.99f ? Lang.T("майстер|майстриня") : c >= 0.8f ? Lang.T("вправний|вправна") : Lang.T("ще вчиться");
        }

        // what that word means (its card's tip): the same steps as CraftWord
        internal static string CraftTip(NpcActor a)
        {
            float c = Craft(a, a.Worker != null ? a.Worker.JobId : "");
            string now = c >= 0.99f ? Lang.T("Знає цю роботу досконало й робить її на повну швидкість.") : c >= 0.8f ? Lang.T("Робить цю роботу майже як майстер, ще трохи навчиться.") : Lang.T("Поки робить цю роботу помітно повільніше за майстра.");
            return Lang.T("Наскільки добре знає свою роботу. Новачок працює повільніше й вчиться з кожною отриманою платнею, аж доки стане майстром. Нова робота — вчитися знову.") + "\n" + now;
        }

        // the coin box against the pays to come (the board): one round of pays (each worker's own day), who is first, who would go without. Read off each one's locker when asked
        internal sealed class PayLook { public int Sum, Short, Workers; public long NextIn = -1L; public string Next = "", FirstShort = ""; public long ShortIn = -1L; }
        internal static PayLook PayAhead(List<NpcActor> who, int coins)
        {
            PayLook l = new PayLook();
            long now = (long)NpcRescue.Now;
            List<KeyValuePair<long, NpcActor>> line = new List<KeyValuePair<long, NpcActor>>();
            foreach (NpcActor a in who)
            {
                if (a == null || a.Zdo == null || a.Worker == null || !a.Worker.Works) continue;
                long next = NpcLocker.Long(a.Zdo, KPayNext, 0L);
                line.Add(new KeyValuePair<long, NpcActor>(next > 0L ? next : now + (long)(Day * PayDays), a));
                l.Sum += Wage(a); l.Workers++;
            }
            line.Sort((x, y) => x.Key.CompareTo(y.Key));
            int left = coins;
            foreach (KeyValuePair<long, NpcActor> e in line)
            {
                if (l.NextIn < 0L) { l.NextIn = System.Math.Max(0L, e.Key - now); l.Next = e.Value.Name; }
                int wage = Wage(e.Value);
                if (left >= wage) { left -= wage; continue; }
                l.Short += wage - left; left = 0;
                if (l.ShortIn < 0L) { l.ShortIn = System.Math.Max(0L, e.Key - now); l.FirstShort = e.Value.Name; }
            }
            return l;
        }

        // a meal (NpcHunger.Eat): the last few, for its mood
        internal void Ate(string food)
        {
            if (string.IsNullOrEmpty(food)) return;
            _ate.Add(food);
            if (_ate.Count > 5) _ate.RemoveAt(0);
        }

        // how many of its last meals were this food (its name as shown) — where it is run only: elsewhere, and after a reload, 0 (NpcTalkGift: what it eats often it likes)
        internal static int AteOf(NpcActor a, string food)
        {
            NpcToil t = a != null && a.Owned ? a.GetComponent<NpcToil>() : null;
            if (t == null || string.IsNullOrEmpty(food)) return 0;
            int n = 0;
            foreach (string f in t._ate) if (string.Equals(f, food, System.StringComparison.OrdinalIgnoreCase)) n++;
            return n;
        }

        // ---------------------------------------------------------------- where it is owned
        private static float DayLength { get { return EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f; } }
        private static float Hour { get { return DayLength / 24f; } }

        private void Update()
        {
            if (_a == null || !_a.Owned) { _read = false; return; }
            ZDO z = _a.Zdo;
            if (z == null || Time.time < _next) return;
            float dt = _read ? Time.time - _lastTick : 0f; _lastTick = Time.time; // (its own last step: the step is shorter while it goes for its pay)
            _next = Time.time + (_pay != null ? PayStep : Tick); // (on its way for its pay: looked at often — its hand into the box must not wait a whole step)
            if (!_read) { if (!NpcLocker.Ready(_a)) return; _read = true; _tired = _keptTired = NpcLocker.Num(z, KTired, 0f); _mood = _keptMood = NpcLocker.Num(z, KMood, 60f); _low = _keptLow = NpcLocker.Num(z, KLow, 0f); _target = _mood; return; }
            if (!NpcResident.Settled(_a) || (_a.Vitals != null && _a.Vitals.Down)) return; // (a person of the story: no pay, no mood of the home)
            dt = Mathf.Clamp(dt, 0f, 30f);
            Tiredness(dt);
            Moods(dt);
            Payday();
            if (Time.time >= _nextKeep && (Mathf.Abs(_tired - _keptTired) > 2f || Mathf.Abs(_mood - _keptMood) > 2f || Mathf.Abs(_low - _keptLow) > 60f)) Keep();
        }

        private void Keep()
        {
            ZDO z = _a.Zdo;
            _nextKeep = Time.time + KeepEvery; _keptTired = _tired; _keptMood = _mood; _keptLow = _low;
            NpcLocker.Put(z, KTired, _tired); NpcLocker.Put(z, KMood, _mood); NpcLocker.Put(z, KLow, _low);
        }

        // ---- tiredness
        private void Tiredness(float dt)
        {
            NpcMind m = _a.Mind;
            if (Working(_a)) _tired = Mathf.Min(100f, _tired + 100f / (12f / Load(_a.Worker.JobId)) * dt / Hour); // (heavy: full in four hours of work; middling eight; light some nine)
            else
            {
                float hours = m != null && m.Asleep ? 2f : m != null && m.SeatedNow ? (Comfy() ? 1.5f : 3f) : 5f; // (rested in so many hours from the top)
                _tired = Mathf.Max(0f, _tired - 100f / hours * dt / Hour);
            }
            if (m == null) return;
            if (_rest != null && m.Of(NpcMind.Layer.Whim) != _rest) _rest = null; // (something else came over it)
            if (_rest != null && _tired < 15f) { m.EndWhim("rest"); _rest = null; return; } // (rested: back to work)
            if (_tired >= 100f && SitToRest(true)) Npcs.Log(_a.Id + ": worn out — sits down to rest");
        }

        // it sits to rest till it is rested (worn out: its own; let off by a player in its talk: LetRest) — from its work, never up from its bed.
        // At its ease too when a player told it to (asked: 2026-11-08, «Іди відпочинь» to a tired one off work thanked you and stood on), not on its way to bed
        private bool SitToRest(bool says, bool asked = false)
        {
            NpcMind m = _a.Mind;
            if (m == null || _rest != null || m.Asleep || !m.WhimFree("rest")) return false;
            NpcMind.Plan day = m.Of(NpcMind.Layer.Routine);
            bool atWork = m.NowLayer == NpcMind.Layer.Job || (m.NowLayer == NpcMind.Layer.Whim && m.Of(NpcMind.Layer.Job) != null);
            if (!atWork && !(asked && m.AtLeisure && (day == null || day.Kind != NpcMind.Task.Sleep))) return false;
            _rest = NpcPlaces.Best(_a, "fire") ?? NpcPlaces.Best(_a, "seat") ?? NpcPlaces.RestHere(_a);
            _rest.Kind = NpcMind.Task.Sit; // (it sits, by the fire too)
            m.TakeWhim(_rest, 1200f, "rest");
            if (says) NpcPhrases.SayOr(_a, "rest", _a.Form(Lang.T("Ух… втомився|втомилась. Посиджу трохи.")), null, NpcActor.Voice.Unasked);
            return true;
        }

        // «Іди відпочинь, я прикрию» in its talk (act «letrest», where it is run): it puts its work down now — or sits down at its ease — if it is tired enough for it to mean something
        internal static float LetRestFrom = 40f;
        internal static void LetRest(NpcActor a)
        {
            NpcToil t = a != null ? a.GetComponent<NpcToil>() : null;
            if (t == null || !t._read || t._tired < LetRestFrom) return;
            if (t.SitToRest(false, true)) Npcs.Log(a.Id + ": let off to rest (" + Mathf.RoundToInt(t._tired) + ")");
            else Npcs.Log(a.Id + ": told to rest, but something it must do comes first");
        }

        // sitting on a seat or by a burning fire: rested soonest
        private bool Comfy()
        {
            Vector3 p = transform.position;
            if (NpcMind.SeatNear(p, 1.3f)) return true;
            NpcBase b = NpcBase.Covering(p); // (a base's own list of its fires: no look over every collider near, each step of every seated person)
            if (b == null) return NpcKit.Nearest<Fireplace>(p, 4f, f => f.IsBurning() && NpcKit.Warms(f)) != null; // (a camp, the wild: no base looks round)
            foreach (Component c in b.Round(typeof(Fireplace))) { Fireplace f = c as Fireplace; if (f != null && Vector3.Distance(f.transform.position, p) < 4f && f.IsBurning() && NpcKit.Warms(f)) return true; }
            return false;
        }

        // ---- mood
        private void Moods(float dt)
        {
            if (Time.time >= _nextMood) { _nextMood = Time.time + 60f; Weigh(); }
            float step = 10f * dt / Hour; // (ten a game hour towards what it feels)
            _mood = Mathf.MoveTowards(_mood, _target, step);
            if (_mood < MoodLow) _low += dt; else _low = Mathf.Max(0f, _low - dt);
            if (_low > LeaveDays * DayLength && !NpcLevers.IsProtected(_a)) Leave();
        }

        // a module's share of its mood (NpcGoals: its wish; NpcWake: woken from its sleep), points ± on the 0–100 scale, asked once a game minute;
        // its word on the card by its sign (Up when it lifts, Down when it weighs: a share said nothing of itself, and every one was named a wish)
        internal sealed class Share { public System.Func<NpcActor, float> By; public Why Up = Why.None, Down = Why.None; }
        internal static readonly List<Share> MoodFrom = new List<Share>();

        private void Weigh()
        {
            float t = 50f; Why worst = Why.None; float worstV = 0f;
            StringBuilder parts = new StringBuilder();
            System.Action<float, Why> add = (v, w) => { if (v == 0f) return; t += v; if (v < worstV) { worstV = v; worst = w; } parts.Append((int)w).Append('=').Append(v.ToString("0", CultureInfo.InvariantCulture)).Append(';'); };
            ZDOID bedId = NpcLocker.Ref(_a.Zdo, NpcResident.KBed);
            if (bedId.IsNone()) add(-15f, Why.NoBed);
            else
            {
                add(10f, Why.Bed);
                GameObject bed = NpcKit.Find(bedId);
                if (bed != null) { if (Roof(bed.transform.position + Vector3.up * 0.5f)) add(5f, Why.BedRoof); else add(-10f, Why.BedOpen); }
            }
            if (NpcPlaces.AnyFire(_a)) add(5f, Why.Fire); else add(-5f, Why.NoFire);
            float full = NpcHunger.Full(_a);
            if (NpcHunger.IsStarving(_a)) add(-25f, Why.Starving); else if (NpcHunger.IsHungry(_a)) add(-10f, Why.Hungry); else if (full >= 60f) add(10f, Why.Fed);
            if (_ate.Count >= 3)
            {
                HashSet<string> kinds = new HashSet<string>(_ate);
                if (kinds.Count <= 1) add(-8f, Why.SameFood); else if (kinds.Count >= 3) add(kinds.Count == 3 ? 5f : 10f, Why.Varied);
            }
            if (_tired >= 90f) add(-10f, Why.Tired);
            if (EnvMan.instance != null && EnvMan.IsWet() && !Roof(transform.position + Vector3.up * 1.6f)) add(-10f, Why.Wet);
            if (_a.Worker != null && _a.Worker.Works)
            {
                int unpaid = NpcLocker.Int(_a.Zdo, KUnpaid, -1);
                if (unpaid == 0) add(15f, Why.Paid); else if (unpaid == 1) add(-15f, Why.Unpaid); else if (unpaid >= 2) add(-25f, Why.Unpaid);
            }
            foreach (Share m in MoodFrom) { float by = m.By(_a); add(by, by > 0f ? m.Up : m.Down); } // (a module's own share: NpcGoals — proud, or long wanting; NpcWake — woken)
            _target = Mathf.Clamp(t, 0f, 100f);
            if (NpcLocker.Int(_a.Zdo, KWhy, 0) != (int)worst) NpcLocker.Put(_a.Zdo, KWhy, (int)worst);
            NpcLocker.Put(_a.Zdo, KMoodParts, parts.ToString()); // (written only when it changed: NpcLocker.Put; a game minute at the most often)
        }

        internal static bool Roof(Vector3 p) { float cover; bool under; Cover.GetCoverForPoint(p, out cover, out under); return under; }

        // it means to go (at the bottom a day of the three already, its locker: any game): its words say so, and a word of yours may stay it (NpcTalkSway)
        internal static float LeaveWarnDays = 1f;
        internal static bool Leaving(NpcActor a) { return a != null && a.Zdo != null && NpcResident.Is(a) && NpcLocker.Num(a.Zdo, KLow, 0f) > LeaveWarnDays * DayLength; }

        // «stay» (talked round, where it is run): its days at the bottom start again — it stays, and its mood is still what its life makes it
        internal static void Stay(NpcActor a)
        {
            NpcToil t = a != null ? a.GetComponent<NpcToil>() : null;
            if (t == null || !t._read) return;
            t._low = 0f; t.Keep();
            Npcs.Log(a.Id + ": talked round — stays");
        }

        // at the bottom three days on end: it goes (as a starved one: to the ravens' home)
        private void Leave() { Leaves(_a, "leave_unhappy", Lang.T("Досить з мене. Піду до Дому воронів."), "npc_left_unhappy", "unhappy too long"); }

        // it leaves its base, where it is run (unhappy too long; its ultimatum ran out — NpcNeighbours): its words, the bus, unhired, on its way to the ravens' home
        internal static void Leaves(NpcActor a, string theme, string written, string busEvent, string why)
        {
            if (a == null) return;
            NpcPhrases.SayOr(a, theme, a.Form(written));
            Npcs.Log(a.Id + ": " + why + " — leaves the base");
            StoryBus.Raise(busEvent, a.Id, 0, a.transform.position);
            NpcToil t = a.GetComponent<NpcToil>();
            if (t != null) { t._mood = t._target = 50f; t._low = 0f; t.Keep(); }
            a.Act("unhire");
            a.Order("travel " + Npcs.V(NpcTravel.Refuge) + " " + NpcTravel.ToRefuge);
        }

        // ---- pay
        internal static int Wage(NpcActor a)
        {
            string job = a.Worker != null ? a.Worker.JobId : "";
            float l = Load(job);
            int coins = l >= 3f ? 7 : l >= 1.5f ? 5 : 3;
            return Mathf.RoundToInt(coins * (1f + NpcRequests.Tier() * 0.5f));
        }

        private static float Day { get { return DayLength; } }
        private string _payWait = ""; // (why its pay waits now: the console, raven npc toil)
        private bool _payNow;          // (asked from the console: not held back by the hour)
        private float _payHand, _lastTick;
        private const float PayStep = 0.25f;
        private const float PayReach = 6f; // m from the heart's middle: by it (a heart is built up to 4.5 m)

        private void Payday()
        {
            ZDO z = _a.Zdo;
            NpcMind m = _a.Mind;
            if (_a.Worker == null || !_a.Worker.Works || m == null) { _payWait = "не працює"; return; }
            long now = (long)NpcRescue.Now;
            long next = NpcLocker.Long(z, KPayNext, 0L);
            if (next == 0L) { NpcLocker.Put(z, KPayNext, now + (long)(Day * PayDays * Random.Range(0.6f, 1f))); return; } // (the first one a little sooner)
            if (_pay != null)
            {
                NpcHeart h = Heart();
                if (m.Of(NpcMind.Layer.Whim) != _pay || h == null || Time.time - _paySince > 180f) { _pay = null; NpcLocker.Put(z, KPayNext, now + (long)(Hour * 3f)); return; } // (did not get there, or stands by a box someone keeps open: later today)
                if (m.NowLayer != NpcMind.Layer.Whim) return; // (a fight, an order came over it: no hand into a box meanwhile)
                if (_pay.Kind == NpcMind.Task.Idle || Utils.DistanceXZ(transform.position, h.transform.position) <= PayReach) // (there: its walk to the spot before the board ended — NpcMind.Arrived — or it stands by the heart; one distance of its own no longer: a bigger heart left it standing «not there», 2026-11-03)
                {
                    if (h.Box != null && !m.Reach(h.Box, ref _payHand)) return; // (its hand into the coin box first, as into any chest: NpcMind.Reach)
                    m.EndWhim("pay");
                    _pay = null;
                    TakePay(h);
                    if (h.Box != null) NpcLid.Close(h.Box);
                    return;
                }
                return;
            }
            if (now < next) { _payWait = "ще не час"; return; }
            if (!m.WhimFree("pay")) { _payWait = "зайнятий: " + m.Describe(); return; }
            float hour = EnvMan.instance != null ? EnvMan.instance.GetDayFraction() * 24f : 12f;
            if (!_payNow && (hour < 7f || hour > 19f)) { _payWait = "не день (" + hour.ToString("0.0", CultureInfo.InvariantCulture) + " год)"; return; } // (in the day)
            NpcHeart heart = Heart();
            if (heart == null) { _payWait = "серця нема поруч"; NpcLocker.Put(z, KPayNext, now + (long)(Hour * 3f)); return; } // (its heart not here: later)
            _payWait = "іде"; _payNow = false;
            Vector3 to = heart.Box != null ? heart.Box.transform.position : heart.transform.position + heart.transform.forward * -3f; // (up to its coin box; none: before its board)
            _pay = new NpcMind.Plan { Kind = NpcMind.Task.Go, To = to, Near = 1f }; _payHand = 0f;
            _paySince = Time.time;
            m.TakeWhim(_pay, 200f, "pay");
            Npcs.Log(_a.Id + ": goes for its pay");
        }

        private NpcHeart Heart()
        {
            GameObject go = NpcKit.Find(NpcKit.GetRef(_a.Zdo, NpcResident.KHeart));
            return go != null ? go.GetComponent<NpcHeart>() : null;
        }

        private void TakePay(NpcHeart h)
        {
            ZDO z = _a.Zdo;
            int wage = Wage(_a);
            long now = (long)NpcRescue.Now;
            int got = h.TakeCoins(wage); // (−1: someone has the box open; 0: not enough there — it takes none)
            if (got < 0) { NpcLocker.Put(z, KPayNext, now + (long)(Hour)); return; } // (a little later)
            NpcLocker.Put(z, KPayNext, now + (long)(Day * PayDays));
            if (got >= wage)
            {
                NpcLocker.Put(z, KUnpaid, 0);
                string job = _a.Worker.JobId;
                bool learned = Craft(_a, job) < 1f;
                if (learned) Learned(_a, job);
                Player near = Player.GetClosestPlayer(transform.position, 20f);
                string coins = Lang.N(wage, "монета|монети|монет");
                string paid = NpcPhrases.Compose(_a, "paid", near, new Dictionary<string, string> { { "wage", coins } }) ?? _a.Form(Lang.F("Платня — {0}. Дякую.", coins));
                if (learned) paid += " " + (NpcPhrases.Compose(_a, "learned", near) ?? _a.Form(Lang.T("Руки вже звикають до роботи.")));
                _a.Say(paid);
                Npcs.Log(_a.Id + ": took its pay (" + wage + ")" + (learned ? ", its craft now " + Craft(_a, job).ToString("0.0", CultureInfo.InvariantCulture) : ""));
                StoryBus.Raise("npc_paid", _a.Id, wage, transform.position);
            }
            else
            {
                NpcLocker.Put(z, KUnpaid, Mathf.Max(0, NpcLocker.Int(z, KUnpaid, -1)) + 1);
                NpcMemory.Remember(_a, "unpaid", 0L, "");
                NpcPhrases.SayOr(_a, "unpaid", _a.Form(Lang.T("Скарбничка порожня… Знову без платні.")));
                Npcs.Log(_a.Id + ": no pay in the box");
                foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(z, NpcResident.KHeart))) // (one of its base paid last time while it goes without: envy — NpcAttitude.Quarrel «pay»)
                    if (o != null && o != _a && o.Worker != null && o.Worker.Works && Unpaid(o) == 0 && Random.value < 0.5f) { NpcAttitude.Quarrel(_a, o, "pay"); break; }
            }
            _nextMood = 0f; // (weighed again at once)
        }

        // for testing (the console): its pay now
        internal string ForcePay()
        {
            if (!_a.Owned) return "Його веде інша гра.";
            NpcLocker.Put(_a.Zdo, KPayNext, 1L); _pay = null; _payNow = true; _payWait = "";
            return "Іде по платню (" + Wage(_a) + ").";
        }

        internal string Describe()
        {
            string job = _a.Worker != null ? _a.Worker.JobId : "";
            return (_payWait.Length > 0 ? "платня: " + _payWait + " · " : "") + "втома " + Mathf.RoundToInt(_read ? _tired : Tired(_a)) + ", настрій " + Mathf.RoundToInt(_read ? _mood : Mood(_a)) + " (до " + Mathf.RoundToInt(_target) + ", " + WhyWord(_a) + ")"
                + (job.Length > 0 ? ", ремесло " + Mathf.RoundToInt(Craft(_a, job) * 100f) + "%, платня " + Wage(_a) + " (пропущено: " + Unpaid(_a) + ")" : "");
        }
    }
}
#endif
