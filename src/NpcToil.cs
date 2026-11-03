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
    //               not wet in the rain, paid — its work ×0.7 at the worst to ×1.2 at the best; at the very bottom three
    //               game days on end it leaves (to the ravens' home, as a starved one does)
    //   pay         every five game days it walks up to its heart and takes its pay from the heart's coin box («Скарбничка»)
    //               itself — a few coins by its work and how far the world has gone; none there: unpaid, its mood down
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
        private const float Tick = 5f, KeepEvery = 120f, PayDays = 5f, Newcomer = 0.6f, Learn = 0.1f, LeaveDays = 3f, MoodLow = 15f;

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

        // × its work's pace (NpcWorker, NpcHew): its mood and its craft
        internal static float Pace(NpcActor a)
        {
            if (a == null) return 1f;
            float m = Mood(a);
            float byMood = m < 50f ? 0.7f + 0.3f * m / 50f : 1f + 0.2f * (m - 50f) / 50f;
            return byMood * Craft(a, a.Worker != null ? a.Worker.JobId : "");
        }

        // ---------------------------------------------------------------- read on every game (its ZDO)
        internal static float Tired(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? z.GetFloat(KTired, 0f) : 0f; }
        internal static float Mood(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? z.GetFloat(KMood, 60f) : 60f; }
        internal static int Unpaid(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? z.GetInt(KUnpaid, -1) : -1; }

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

        private static void Learned(NpcActor a, string job)
        {
            float now = Mathf.Min(1f, Craft(a, job) + Learn);
            string kept = NpcLocker.Get(a, KCraft) ?? "";
            StringBuilder sb = new StringBuilder();
            foreach (string part in kept.Split(';')) { if (part.Length == 0 || part.StartsWith(job + "=", System.StringComparison.Ordinal)) continue; sb.Append(part).Append(';'); }
            sb.Append(job).Append('=').Append(now.ToString("0.##", CultureInfo.InvariantCulture));
            NpcLocker.Set(a, KCraft, sb.ToString());
        }

        // ---------------------------------------------------------------- what weighs on it (its mood's worst)
        internal enum Why { None, NoBed, BedOpen, Hungry, Starving, SameFood, Tired, Wet, Unpaid, NoFire }
        internal static string WhyWord(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            switch ((Why)(z != null ? z.GetInt(KWhy, 0) : 0))
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
                default: return "";
            }
        }

        // a meal (NpcHunger.Eat): the last few, for its mood
        internal void Ate(string food)
        {
            if (string.IsNullOrEmpty(food)) return;
            _ate.Add(food);
            if (_ate.Count > 5) _ate.RemoveAt(0);
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
            if (!_read) { _read = true; _tired = _keptTired = z.GetFloat(KTired, 0f); _mood = _keptMood = z.GetFloat(KMood, 60f); _low = _keptLow = z.GetFloat(KLow, 0f); _target = _mood; return; }
            if (!NpcResident.Is(_a) || (_a.Vitals != null && _a.Vitals.Down)) return;
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
            z.Set(KTired, _tired); z.Set(KMood, _mood); z.Set(KLow, _low);
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
            if (_rest == null && _tired >= 100f && m.WhimFree("rest") && (m.NowLayer == NpcMind.Layer.Job || (m.NowLayer == NpcMind.Layer.Whim && m.Of(NpcMind.Layer.Job) != null)) && !m.Asleep) // (worn out at its work — not up from its bed to sit)
            {
                NpcBase b = NpcBase.For(NpcRules.Home(_a));
                Vector3 spot = FireSpot(b, transform.position, _a) ?? (b.Ready ? NpcPrivate.Spot(_a, b) : transform.position);
                _rest = new NpcMind.Plan { Kind = NpcMind.Task.Sit, To = spot };
                m.TakeWhim(_rest, 1200f, "rest");
                NpcPhrases.SayOr(_a, "rest", _a.Form(Lang.T("Ух… втомився|втомилась. Посиджу трохи.")));
                Npcs.Log(_a.Id + ": worn out — sits down to rest");
            }
        }

        // sitting on a seat or by a burning fire: rested soonest
        private bool Comfy()
        {
            Vector3 p = transform.position;
            return NpcMind.SeatNear(p, 1.3f) || NpcKit.Nearest<Fireplace>(p, 4f, f => f.IsBurning()) != null;
        }

        // two steps from the nearest burning fire of its base (its fires as its workers see them: looked over now and then)
        internal static Vector3? FireSpot(NpcBase b, Vector3 me, NpcActor who)
        {
            if (b == null || !b.Ready) return null;
            List<Fireplace> fires = new List<Fireplace>();
            foreach (Fireplace f in NpcWorker.InBaseOf<Fireplace>(b, me)) if (f != null && f.IsBurning() && !NpcPrivate.Off(who, b, f.transform.position)) fires.Add(f); // (not a fire in a player's house)
            fires.Sort((x, y) => (x.transform.position - me).sqrMagnitude.CompareTo((y.transform.position - me).sqrMagnitude));
            for (int i = 0; i < fires.Count && i < 4; i++) // (the nearest few: one up a tower out of reach gives way to the next)
            {
                Vector3? s = NpcKit.FireSide(fires[i], me);
                if (s != null) return s;
            }
            return null;
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

        private void Weigh()
        {
            float t = 50f; Why worst = Why.None; float worstV = 0f;
            System.Action<float, Why> add = (v, w) => { t += v; if (v < worstV) { worstV = v; worst = w; } };
            ZDOID bedId = NpcKit.GetRef(_a.Zdo, NpcResident.KBed);
            if (bedId.IsNone()) add(-15f, Why.NoBed);
            else
            {
                add(10f, Why.None);
                GameObject bed = NpcKit.Find(bedId);
                if (bed != null) add(Roof(bed.transform.position + Vector3.up * 0.5f) ? 5f : -10f, Why.BedOpen);
            }
            NpcBase b = NpcBase.For(NpcRules.Home(_a));
            add(FireSpot(b, transform.position, _a) != null ? 5f : -5f, Why.NoFire);
            float full = NpcHunger.Full(_a);
            add(NpcHunger.IsStarving(_a) ? -25f : NpcHunger.IsHungry(_a) ? -10f : full >= 60f ? 10f : 0f, NpcHunger.IsStarving(_a) ? Why.Starving : Why.Hungry);
            if (_ate.Count >= 3)
            {
                HashSet<string> kinds = new HashSet<string>(_ate);
                add(kinds.Count <= 1 ? -8f : kinds.Count == 2 ? 0f : kinds.Count == 3 ? 5f : 10f, Why.SameFood);
            }
            if (_tired >= 90f) add(-10f, Why.Tired);
            if (EnvMan.instance != null && EnvMan.IsWet() && !Roof(transform.position + Vector3.up * 1.6f)) add(-10f, Why.Wet);
            if (_a.Worker != null && _a.Worker.Works)
            {
                int unpaid = _a.Zdo.GetInt(KUnpaid, -1);
                if (unpaid == 0) add(15f, Why.Unpaid); else if (unpaid == 1) add(-15f, Why.Unpaid); else if (unpaid >= 2) add(-25f, Why.Unpaid);
            }
            _target = Mathf.Clamp(t, 0f, 100f);
            if (_a.Zdo.GetInt(KWhy, 0) != (int)worst) _a.Zdo.Set(KWhy, (int)worst);
        }

        private static bool Roof(Vector3 p) { float cover; bool under; Cover.GetCoverForPoint(p, out cover, out under); return under; }

        // at the bottom three days on end: it goes (as a starved one: to the ravens' home)
        private void Leave()
        {
            NpcPhrases.SayOr(_a, "leave_unhappy", _a.Form(Lang.T("Досить з мене. Піду до Дому воронів.")));
            Npcs.Log(_a.Id + ": unhappy too long — leaves the base");
            StoryBus.Raise("npc_left_unhappy", _a.Id, 0, transform.position);
            _mood = _target = 50f; _low = 0f; Keep();
            _a.Act("unhire");
            _a.Order("travel " + Npcs.V(NpcTravel.Hall) + " " + NpcTravel.ToHall);
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
            long next = z.GetLong(KPayNext, 0L);
            if (next == 0L) { z.Set(KPayNext, now + (long)(Day * PayDays * Random.Range(0.6f, 1f))); return; } // (the first one a little sooner)
            if (_pay != null)
            {
                NpcHeart h = Heart();
                if (h != null && (_pay.Kind == NpcMind.Task.Idle || Utils.DistanceXZ(transform.position, h.transform.position) <= PayReach)) // (there: its walk to the spot before the board ended — NpcMind.Arrived — or it stands by the heart; one distance of its own no longer: a bigger heart left it standing «not there», 2026-11-03)
                {
                    if (h.Box != null && !m.Reach(h.Box, ref _payHand)) return; // (its hand into the coin box first, as into any chest: NpcMind.Reach)
                    m.EndWhim("pay");
                    _pay = null;
                    TakePay(h);
                    if (h.Box != null) NpcLid.Close(h.Box);
                    return;
                }
                if (m.Of(NpcMind.Layer.Whim) != _pay || h == null || Time.time - _paySince > 180f) { _pay = null; z.Set(KPayNext, now + (long)(Hour * 3f)); } // (did not get there: later today)
                return;
            }
            if (now < next) { _payWait = "ще не час"; return; }
            if (!m.WhimFree("pay")) { _payWait = "зайнятий: " + m.Describe(); return; }
            float hour = EnvMan.instance != null ? EnvMan.instance.GetDayFraction() * 24f : 12f;
            if (!_payNow && (hour < 7f || hour > 19f)) { _payWait = "не день (" + hour.ToString("0.0", CultureInfo.InvariantCulture) + " год)"; return; } // (in the day)
            NpcHeart heart = Heart();
            if (heart == null) { _payWait = "серця нема поруч"; z.Set(KPayNext, now + (long)(Hour * 3f)); return; } // (its heart not here: later)
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
            if (got < 0) { z.Set(KPayNext, now + (long)(Hour)); return; } // (a little later)
            z.Set(KPayNext, now + (long)(Day * PayDays));
            if (got >= wage)
            {
                z.Set(KUnpaid, 0);
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
                z.Set(KUnpaid, Mathf.Max(0, z.GetInt(KUnpaid, -1)) + 1);
                NpcMemory.Remember(_a, "unpaid", 0L, "");
                NpcPhrases.SayOr(_a, "unpaid", _a.Form(Lang.T("Скарбничка порожня… Знову без платні.")));
                Npcs.Log(_a.Id + ": no pay in the box");
            }
            _nextMood = 0f; // (weighed again at once)
        }

        // for testing (the console): its pay now
        internal string ForcePay()
        {
            if (!_a.Owned) return "Його веде інша гра.";
            _a.Zdo.Set(KPayNext, 1L); _pay = null; _payNow = true; _payWait = "";
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
