#if RAVEN_ITEM
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // What a raven is doing, from the weakest to the strongest: a stronger act may take the bird from a weaker one.
    internal enum RavenAct { Sit = 0, Land = 1, Arrive = 2, Outing = 3, Hover = 4, Command = 5, Dome = 6, Strike = 7, FlyOff = 8 }

    // whoever drives a raven and can be asked to let it go (a stronger act takes it)
    internal interface IRavenUser { void Yield(); }

    // «Режисер анімацій»: one place that knows what each raven is doing and who drives it. Every flight asks it for
    // the bird (Take) and gives it back (Release); a stronger act takes it from a weaker one, which is told to let go
    // (Yield). The pose flags on the bird (flying clip, soaring, held still, hovering) are set here only, on each
    // change. An outing that is cut short is called off on the network too. Every change can go into the log, and
    // «raven anim» shows the state now.
    internal static class AnimDirector
    {
        internal static bool LogOn = false; // the developer's log (raven anim log): who drives a bird, take-offs, landings, strikes, sounds

        private sealed class Entry
        {
            public GameObject Model;
            public RavenAct Act;
            public object Owner;
            public float Since;
            public string Why = "";
        }

        private static readonly Dictionary<int, Entry> All = new Dictionary<int, Entry>();
        private static float _prune;

        private static Entry Get(GameObject m)
        {
            if (Time.time >= _prune) { _prune = Time.time + 30f; Prune(); }
            Entry e;
            int id = m.GetInstanceID();
            if (!All.TryGetValue(id, out e)) { e = new Entry { Model = m, Since = Time.time }; All[id] = e; }
            return e;
        }

        private static void Prune()
        {
            List<int> gone = null;
            foreach (KeyValuePair<int, Entry> kv in All) if (kv.Value.Model == null) { if (gone == null) gone = new List<int>(); gone.Add(kv.Key); }
            if (gone != null) foreach (int k in gone) All.Remove(k);
        }

        internal static RavenAct Of(GameObject m)
        {
            Entry e;
            return m != null && All.TryGetValue(m.GetInstanceID(), out e) ? e.Act : RavenAct.Sit;
        }

        private static bool Allowed(Entry e, RavenAct act, object owner)
        {
            if (e.Act == RavenAct.Sit || (owner != null && ReferenceEquals(e.Owner, owner))) return true;
            if (e.Owner == null || (e.Owner is Object && (Object)e.Owner == null)) return true; // its driver is gone
            if (act == e.Act && act == RavenAct.Strike) return true; // a new strike over the old one
            return (int)act > (int)e.Act;
        }

        internal static bool CanTake(GameObject m, RavenAct act) { return m != null && Allowed(Get(m), act, null); }

        // ask for the bird: true when it is yours now (the one who had it was told to let go)
        internal static bool Take(GameObject m, RavenAct act, object owner, string why)
        {
            if (m == null) return false;
            Entry e = Get(m);
            if (!Allowed(e, act, owner)) { Log(m, "refused " + act + " (" + why + "): " + e.Act + " (" + e.Why + ") holds it"); return false; }
            RavenAct was = e.Act;
            object prev = e.Owner;
            if (prev != null && !ReferenceEquals(prev, owner))
            {
                IRavenUser u = prev as IRavenUser;
                if (u != null && !(prev is Object && (Object)prev == null))
                {
                    try { u.Yield(); }
                    catch (System.Exception ex)
                    {
                        Log(m, "could not transfer " + act + " (" + why + "): " + ex);
                        return false;
                    }
                }
                if (m == null) return false;
                e = Get(m);
                if (!ReferenceEquals(e.Owner, prev) || e.Act != was || !Allowed(e, act, owner))
                {
                    Log(m, "refused " + act + " (" + why + "): ownership changed while yielding");
                    return false;
                }
            }
            e.Act = act; e.Owner = owner; e.Since = Time.time; e.Why = why ?? "";
            if (was == RavenAct.Outing && act != RavenAct.Outing) Outing.Interrupted(m);
            if (was == RavenAct.Command && act != RavenAct.Command && act != RavenAct.Land) Orders.Interrupted(m, act); // (Land: it came home, the order was over)
            Flags(m, act);
            if (was != act) Log(m, was + " → " + act + " (" + e.Why + ")");
            return true;
        }

        // give the bird back (only its driver can): it sits, the pose flags off
        internal static void Release(GameObject m, object owner)
        {
            Entry e;
            if (m == null || !All.TryGetValue(m.GetInstanceID(), out e) || !ReferenceEquals(e.Owner, owner)) return;
            RavenAct was = e.Act;
            e.Act = RavenAct.Sit; e.Owner = null; e.Since = Time.time; e.Why = "";
            Flags(m, RavenAct.Sit);
            if (was != RavenAct.Sit) Log(m, was + " → Sit");
        }

        // the pose flags of each act (the fly-off sets its own as it goes; Land: the game's fly-in clip, then the landing)
        private static void Flags(GameObject m, RavenAct act)
        {
            ShoulderLife l = m.GetComponent<ShoulderLife>();
            if (l == null || act == RavenAct.FlyOff) return;
            switch (act)
            {
                case RavenAct.Strike:
                case RavenAct.Dome:
                    l.Hovering = false; l.Soaring = true; l.Still = true; l.VanillaFly = true; break;
                case RavenAct.Outing:
                case RavenAct.Arrive: // the fly-in, once the motor has it
                case RavenAct.Command: // an order (Orders)
                    l.Hovering = false; l.Soaring = false; l.Still = false; l.VanillaFly = true; break;
                case RavenAct.Hover:
                    l.Soaring = false; l.Still = false; l.Hovering = true; l.VanillaFly = ShoulderLife.HoverVanilla; break;
                default: // Sit, Land
                    l.Hovering = false; l.Soaring = false; l.Still = false; l.VanillaFly = false; break;
            }
        }

        private static string Name(GameObject m)
        {
            ShoulderPin pin = m.GetComponent<ShoulderPin>();
            if (pin != null && pin.Player != null) return (pin.Munin ? "Munin" : "Hugin") + " of " + pin.Player.GetPlayerName();
            ShoulderLife l = m.GetComponent<ShoulderLife>();
            return (l != null ? (l.Munin ? "Munin" : "Hugin") : "raven") + " (" + m.name + ")";
        }

        // a line about this bird in the log (the landings' and take-offs' styles)
        internal static void Note(GameObject m, string what) { Log(m, what); }

        // only your own ravens (other players' would fill the log in a crowd)
        private static void Log(GameObject m, string what)
        {
            if (!LogOn || Plugin.Instance == null || m == null) return;
            ShoulderPin pin = m.GetComponent<ShoulderPin>();
            ShoulderLife life = m.GetComponent<ShoulderLife>();
            Player who = pin != null ? pin.Player : life != null ? life.Player : null;
            if (who != null && !ReferenceEquals(who, Player.m_localPlayer)) return;
            Plugin.Instance.Log("Raven anim: " + Name(m) + ": " + what);
        }

        // ---------------------------------------------------------------- the schedule of the ravens' own doings
        // Once in a while (a random 20-30 min) the director looks: if a raven may go (you calm and outside, it sitting
        // awake), then on a coin toss one goes to stretch its wings (which and how: random), or both together circling
        // opposite ways. Not possible right then: it looks again every minute until it is. It counts from the last
        // outing, whoever began it.
        internal static float OutingMinMinutes = 20f, OutingMaxMinutes = 30f, OutingChance = 0.5f, BothChance = 0.5f;
        private static float _nextLook = -1f;
        private static bool _pending;

        internal static void Schedule()
        {
            Player p = Player.m_localPlayer;
            WorldVisit.Schedule(p); // its own look: the visits to a sign, a berry bush (not the outings' rhythm)
            if (p == null || !Outing.On) return;
            float since = Outing.LastAt;
            if (_nextLook < 0f || since > _nextLook - Gap().x * 60f) NextLook(Mathf.Max(Time.time, since));
            // taking to the road or the sea: the next look comes sooner (the ravens get more freedom on a journey)
            if (!_pending && _nextLook - Time.time > Gap().y * 60f) NextLook(Mathf.Max(Time.time, since));
            if (Time.time < _nextLook && !_pending) return;
            if (!_pending)
            {
                _pending = true;
                if (Random.value > Mathf.Clamp(OutingChance * WornOutChance(p), 0f, 0.95f)) { _pending = false; NextLook(Time.time); if (LogOn && Plugin.Instance != null) Plugin.Instance.Log("Raven anim: the director looked — the ravens stay put this time"); return; }
            }
            if (Time.time < _retry) return;
            _retry = Time.time + 60f;
            if (!Outing.CanGo(p)) return; // not now: again in a minute
            bool h = Outing.ReadyRaven(p, false), m = Outing.ReadyRaven(p, true);
            if (!h && !m) return;
            _pending = false;
            NextLook(Time.time);
            if (h && m && Random.value < BothChance && !Senses.Traveling) { Outing.StartBoth(p); return; }
            // which one: the more eager by its character (Norov) goes more often
            float wh = Norov.OutChance(RavenSeat.On(p, false)), wm = Norov.OutChance(RavenSeat.On(p, true));
            bool munin = h && m ? Random.value * (wh + wm) < wm : m;
            ZDOID bird;
            if (munin && Random.value < HuntChance && WorldBirds.Near(p, out bird)) { Outing.StartHunt(p, true, bird); return; } // a gull, a crow near: after it
            Outing.Start(p, munin, Pick(RavenSeat.On(p, munin)));
        }

        // the director's chance × your ravens' characters (their middle): a homebody pair stays put more often
        private static float WornOutChance(Player p)
        {
            float sum = 0f; int n = 0;
            for (int i = 0; i < 2; i++)
            {
                ItemDrop.ItemData it = RavenSeat.On(p, i == 1);
                if (it == null || !Trust.Active(it)) continue;
                sum += Norov.OutChance(it); n++;
            }
            return n > 0 ? sum / n : 1f;
        }

        // what kind of outing, by where you are: at sea wide circles over the mast or along; on the road also a
        // quick scouting ahead; otherwise along or round. The raven's character weighs each kind (Norov).
        private static int Pick(ItemDrop.ItemData item)
        {
            bool right = Random.value < 0.5f;
            float f = Norov.FollowW(item), c = Norov.CircleW(item), s = Norov.ScoutW(item);
            float wf, wc, ws;
            if (Senses.Sailing) { wf = 0.4f * f; wc = 0.6f * c; ws = 0f; }
            else if (Senses.Traveling) { wf = 0.3f * f; wc = 0.3f * c; ws = 0.4f * s; }
            else { wf = 0.5f * f; wc = 0.5f * c; ws = 0f; }
            float r = Random.value * (wf + wc + ws);
            if (r < ws) return Outing.Scout;
            if (r < ws + wf) return Outing.Follow;
            return Senses.Sailing ? (right ? Outing.WideRight : Outing.WideLeft) : (right ? Outing.CircleRight : Outing.CircleLeft);
        }

        // minutes between looks (from, to): at sea 5-8, on the road 10-15, otherwise as set (20-30)
        internal static float SailMin = 5f, SailMax = 8f, RoadMin = 10f, RoadMax = 15f;
        private static Vector2 Gap()
        {
            if (Senses.Sailing) return new Vector2(SailMin, SailMax);
            if (Senses.Traveling) return new Vector2(RoadMin, RoadMax);
            return new Vector2(OutingMinMinutes, OutingMaxMinutes);
        }

        internal static float HuntChance = 0.6f; // Munin's outing with a bird of the game near: a hunt
        private static float _retry;
        private static void NextLook(float from) { Vector2 g = Gap(); _nextLook = from + Random.Range(g.x, g.y) * 60f; }
        internal static string NextLookText { get { return _nextLook < 0f ? "—" : _pending ? "чекає нагоди" : Mathf.Max(0f, (_nextLook - Time.time) / 60f).ToString("0.0") + " хв"; } }

        // ---------------------------------------------------------------- the busy idles on the shoulder
        // Its own doings (the sitting director's templates: preening, a shake, a step aside...) ask first: only while the
        // bird just sits (not flying, landing, out), one at a time, with a rest between them (Hugin longer, Munin
        // shorter, both busier at low trust), not in a fight and not while a raven is talking; each template keeps its
        // own rest besides (SitDirector). A reaction is only noted (the doings wait for it). A "no" is not lost: the asker
        // tries a little later. Quick reactions (Munin's twitch, the beak clacks) only keep out of the way.
        internal enum Idle { Settle, React, Doing } // Settle: just down; React: a reaction to you; Doing: a template of its own
        internal static float IdleRestHugin = 14f, IdleRestMunin = 6f;   // s between any two busy idles of one raven

        internal static bool MayIdle(ShoulderLife life, Idle kind)
        {
            if (life == null || Of(life.gameObject) != RavenAct.Sit) return false;
            float now = Time.time;
            if (now < life.IdleBusyUntil) return false;
            if (kind == Idle.Settle) return true; // just down: its little settle (a hop, a beat) has no rest to wait for, only nothing else going on
            if (Senses.Fight || Cozy.Quieted) return false;
            float rest = (life.Munin ? IdleRestMunin : IdleRestHugin) / life.Nerves;
            if (now - life.IdleLastAt < rest) return false;
            return true; // (each sitting template keeps its own rest: SitDirector)
        }

        internal static void DidIdle(ShoulderLife life, Idle kind, float seconds)
        {
            if (life == null) return;
            life.IdleBusyUntil = Time.time + Mathf.Max(0.1f, seconds);
            life.IdleLastAt = Time.time;
            life.IdleLastKind = kind.ToString();
        }

        // a quick reaction (twitch, beak clack) does not cut into a busy idle
        internal static bool Busy(ShoulderLife life) { return life != null && Time.time < life.IdleBusyUntil; }

        // «raven anim»: what each of your ravens is doing now, who drives it, since when
        internal static string Report(Player p)
        {
            StringBuilder sb = new StringBuilder("Режисер анімацій" + (LogOn ? " (лог увімкнено)" : "") + ". Наступна перевірка прогулянки: " + NextLookText + "\n" + Senses.Describe() + ":");
            foreach (bool munin in new[] { false, true })
            {
                GameObject m = Shoulder.ModelOf(p, munin);
                sb.Append("\n  ").Append(munin ? "Мунін: " : "Хугін: ");
                if (m == null) { sb.Append("не на плечі"); continue; }
                Entry e = Get(m);
                sb.Append(e.Act).Append(e.Why.Length > 0 ? " (" + e.Why + ")" : "").Append(", ").Append((Time.time - e.Since).ToString("0.0")).Append(" с");
                if (e.Owner != null) sb.Append(", веде ").Append(e.Owner.GetType().Name);
                ShoulderLife life = m.GetComponent<ShoulderLife>();
                if (life != null && life.Motor != null && life.Motor.Airborne) sb.Append("; мотор: «").Append(life.Motor.Plan).Append("» ").Append(life.Motor.Body.Speed.ToString("0.0")).Append(" м/с");
                if (life != null && life.IdleLastKind.Length > 0) sb.Append("; остання дія: ").Append(life.IdleLastKind).Append(" ").Append((Time.time - life.IdleLastAt).ToString("0")).Append(" с тому");
            }
            return sb.ToString();
        }
    }
}
#endif
