#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // What living together does to people, by how they get on (NpcAttitude: how one feels about another), the user 2026-11-15: «усе що пропонуєш — реалізувати,
    // і прозоро: завжди можна відслідкувати на вдачі». Each part is said where it is seen: its card's «Свої» (NpcProfile: who, what it does now, how long), its
    // mood's «чому» (NpcToil.Why: Friend, Foe, Feast), the heart's board (NpcBoard: an ultimatum in «Треба зробити», «Свято» under its tabs, «сталося»).
    //   mood       one of its home it is cold to: down (FoeMood; hostile EnemyMood) · a friend there: up (FriendMood; close DearMood) · a feast lately: up (FeastMood)
    //   work       a friend with work of its own within WorkNear m: its work ×WorkWith (NpcToil.Pace; looked at once in WorkLook s a person)
    //   table      its seat at a meal leans to a friend's side and away from one it is cold to or kept apart from (NpcMind.SeatLean)
    //   raid       hit by a beast, a friend near who fights comes to its side (NpcMind.Hurt → act «standby» where that one is run; stood_by); after a fight
    //              beaten off, those of its home who fought it too (not ran) warmer (fought_together) — each once a game day a pair
    //   feud       hostile (≤ NpcAttitude.HostileAt) to one of its home: «або я, або …» (a settler only; Ultimatum, kept in its locker). Over when it is no longer
    //              hostile (peace, «Помиріться вже») or that one lives here no more; UltimatumDays on, still so — it leaves (NpcToil.Leaves)
    //   a scuffle  a spat between two meeting on foot, one hostile: the colder one's fist in its palm, the other steps back (fist_palm, step_back: clips of
    //              our own; a real shove wants a clip of its own, the user's to approve) — a quarrel «brawl» (NpcChatter.Spats)
    //   apart      «Тримайтеся одне від одного подалі» (its talk, !keepapart: the one it holds most against — NpcAttitude.Grudge, its ultimatum's one first;
    //              out of patience with you: no, as with peace): for ApartDays it neither chats with that one (NpcChatter.Shuns), nor walks up to it
    //              (NpcPlaces.PersonWorth), nor sits by it; from the one of its ultimatum: that many days more on it, once (Pushed). An ultimatum waits
    //              while it is not known here whether that one still lives at its home (a client's census); its {foe} is that one
    //   a grudge   cold to one of its home: a worry said to anyone (quarrel_grudge) — its talk's answers to a quarrel, «Помиріться вже» always there
    //   feast      the board's «Свято» (Feast): dishes from your bag, one a person; all of the home warmer to each other, a feast's day of good mood (Why.Feast);
    //              once in FeastEvery game days a heart (kept on its people: no write to the heart). Seen (the user 2026-11-16: «видиме Свято»): for
    //              FeastHours game hours after it they keep it at their dining table — its drinks and toasts (NpcPlaces.Feasting)
    //   a circle   an evening its home gathers by the fire (the user 2026-11-16: no day by the hour for settlers — an evening circle, not every
    //              evening): the evenings of a heart at random intervals (NpcKit.Days, its heart's key), its people drawn to the fire (NpcPlaces.Circle)
    //   snoring    a settler snores by its seed (Snores: SnoreShare of them; its card says so): asleep at night with its bed within SnoreNear m of one
    //              who does, a light sleeper holds it against that one now and then (quarrel «snore», once a night a person)
    //   nosy       a nosy one (NpcNature «nosy») at its ease now and then walks to a neighbour's work chest; that one near and awake sees it — quarrel «chest»
    // Where a person is run (its locker is written there); read on every game (its card, the board). Light: a look every LookEvery s a person, the
    // rest at an event (a hit, a fight's end, a spat, a feast, a seat picked).
    internal static class NpcNeighbours
    {
        internal static readonly int KNeighbours = "RavenWatch_npcNeighbours".GetStableHashCode(); // string «ult=<key>@<since>;apart=<key>@<until>;feast=<when>» (world seconds) — in its locker

        internal static float FoeMood = -6f, EnemyMood = -12f, FriendMood = 4f, DearMood = 7f, FeastMood = 8f, FeastDays = 1f, FeastEvery = 3f, FeastWarm = 6f, FeastHours = 5f;
        internal static float WorkNear = 15f, WorkWith = 1.1f, WorkLook = 10f;
        internal static float SeatNear = 1.8f, SeatFriend = -3f, SeatFoe = 6f;
        internal static float StandByNear = 25f, StandByWarm = 4f, FoughtWithin = 120f, FoughtWarm = 5f;
        internal static float UltimatumDays = 3f, ApartDays = 3f;
        internal static float SnoreNear = 5f, SnoreShare = 0.25f, SnoreChance = 0.35f;
        internal static float NosyChance = 0.04f, NosySeen = 15f, NosyNear = 2f, ScuffleChance = 0.5f;
        private const float LookEvery = 30f, NosyFor = 60f;
        private const string NosyWhim = "nosy";

        internal static void Wire()
        {
            NpcToil.MoodFrom.Add(new NpcToil.Share { By = FoeShare, Down = NpcToil.Why.Foe });
            NpcToil.MoodFrom.Add(new NpcToil.Share { By = FriendShare, Up = NpcToil.Why.Friend });
            NpcToil.MoodFrom.Add(new NpcToil.Share { By = FeastShare, Up = NpcToil.Why.Feast });
            NpcMind.SeatLean = SeatLean;
            NpcMind.Hurt.Add(Hurt);
            NpcMind.FightOver.Add(FightOver);
            NpcChatter.Shuns.Add(Apart);
            NpcChatter.Spats.Add(Scuffle);
            NpcPlaces.PersonWorth.Add((a, o) => Apart(a, o) ? -10f : 0f); // (one it keeps away from: never walked up to)
            NpcPlaces.Feasting = Feasting;
            NpcPlaces.Circle = Circle;
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
            NpcActor.Acts["standby"] = StandBy;
            NpcActor.Acts["feast"] = FeastAct;
            NpcActor.Acts["apart"] = ApartAct;
            NpcTalk.Deeds["keepapart"] = KeepApart;
            NpcTalk.Words["{apart}"] = (a, p) => ApartSaid;
            NpcVoice.Sources.Add(Themes);
            NpcAttitude.GrudgeFirst = a => { Kept k = Of(a); return k != null ? k.Ult : 0L; }; // (make peace, keep apart: with the one of its ultimatum first)
            NpcPhrases.ValueSources.Add((a, p, v) => { Kept k = Of(a); string n = k != null && k.Ult != 0L ? NameOf(a, k.Ult) : ""; if (n.Length > 0) v["foe"] = n; }); // (its ultimatum's one is its {foe}: after NpcAttitude.Values, which names the coldest)
            Npcs.Commands["neighbours"] = (p, who, w, aim) => Describe(who);
        }

        // ---------------------------------------------------------------- kept in its locker
        internal sealed class Kept { public long Ult, UltAt, Apart, ApartUntil, Feast; public bool Pushed; } // (Pushed: its ultimatum put off once by keeping apart — once only)

        // null: its locker is not here yet
        internal static Kept Of(NpcActor a)
        {
            string s = a != null ? NpcLocker.Get(a, KNeighbours) : null;
            if (s == null) return null;
            Kept k = new Kept();
            foreach (string e in s.Split(';'))
            {
                int eq = e.IndexOf('=');
                if (eq <= 0) continue;
                string name = e.Substring(0, eq), v = e.Substring(eq + 1);
                if (name == "feast") { long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out k.Feast); continue; }
                if (name == "pushed") { k.Pushed = v == "1"; continue; }
                int at = v.IndexOf('@'); long x, y;
                if (at <= 0 || !long.TryParse(v.Substring(0, at), NumberStyles.Integer, CultureInfo.InvariantCulture, out x) || !long.TryParse(v.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out y)) continue;
                if (name == "ult") { k.Ult = x; k.UltAt = y; } else if (name == "apart") { k.Apart = x; k.ApartUntil = y; }
            }
            return k;
        }

        private static void Write(NpcActor a, Kept k)
        {
            List<string> parts = new List<string>();
            if (k.Ult != 0L) parts.Add("ult=" + k.Ult.ToString(CultureInfo.InvariantCulture) + "@" + k.UltAt.ToString(CultureInfo.InvariantCulture));
            if (k.Apart != 0L) parts.Add("apart=" + k.Apart.ToString(CultureInfo.InvariantCulture) + "@" + k.ApartUntil.ToString(CultureInfo.InvariantCulture));
            if (k.Feast != 0L) parts.Add("feast=" + k.Feast.ToString(CultureInfo.InvariantCulture));
            if (k.Ult != 0L && k.Pushed) parts.Add("pushed=1");
            NpcLocker.Set(a, KNeighbours, string.Join(";", parts.ToArray()));
        }

        private static long Now { get { return (long)NpcRescue.Now; } }
        private static float Day { get { return EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f; } }
        private static long Today() { return (long)(NpcRescue.Now / Day); }

        // the others of its home loaded here
        private static IEnumerable<NpcActor> Folk(NpcActor a)
        {
            if (a == null || a.Zdo == null) yield break;
            foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart))) if (o != null && o != a && o.Zdo != null) yield return o;
        }

        // the name of one by its key (NpcAttitude.PersonKey): loaded, else the census's (the server: all; a client: what it has); "" none
        internal static string NameOf(NpcActor a, long key)
        {
            foreach (NpcActor o in Folk(a)) if (NpcAttitude.PersonKey(o) == key) return o.Name;
            ZDO z = Census(key);
            return z != null ? NpcActor.Shown(z.GetString(NpcSettlers.KName, "")) : "";
        }

        private static ZDO Census(long key)
        {
            foreach (ZDO z in NpcCensus.People()) if (z != null && -1L - NpcKit.Seed(z) == key) return z;
            return null;
        }

        // that one lives at its home still: 1 yes (loaded here, or the census has it at the same heart) · 0 no (the census has it elsewhere) · −1 not
        // known here (a client's census holds only what it has loaded: one away following a player is not «gone»)
        private static int LivesWith(NpcActor a, long key)
        {
            foreach (NpcActor o in Folk(a)) if (NpcAttitude.PersonKey(o) == key) return 1;
            ZDO z = Census(key);
            if (z == null) return -1;
            return NpcKit.GetRef(z, NpcResident.KHeart) == NpcKit.GetRef(a.Zdo, NpcResident.KHeart) ? 1 : 0;
        }

        // ---------------------------------------------------------------- mood (NpcToil.MoodFrom: asked once a game minute where it is run)
        private static float FoeShare(NpcActor a)
        {
            float worst = 0f;
            foreach (NpcActor o in Folk(a)) worst = Mathf.Min(worst, NpcAttitude.OfPerson(a, o));
            return worst <= NpcAttitude.HostileAt ? EnemyMood : NpcAttitude.Cold(worst) ? FoeMood : 0f;
        }

        private static float FriendShare(NpcActor a)
        {
            float best = 0f;
            foreach (NpcActor o in Folk(a)) best = Mathf.Max(best, NpcAttitude.OfPerson(a, o));
            return best >= NpcAttitude.CloseAt ? DearMood : best >= NpcAttitude.WarmAt ? FriendMood : 0f;
        }

        // its home's feast on now: FeastHours game hours from it (kept on it: Kept.Feast)
        private static bool Feasting(NpcActor a) { Kept k = Of(a); return k != null && k.Feast > 0L && Now - k.Feast < FeastHours / 24f * Day; }

        // its home's evening circle tonight: the evenings of its heart at random intervals (NpcKit.Days); a settler with no heart — none
        private static bool Circle(NpcActor a)
        {
            if (a == null || a.Zdo == null || EnvMan.instance == null) return false;
            string heart = a.Zdo.GetString(NpcResident.KHeart, ""); // (its lasting link as written: the same on every game, nothing resolved)
            return heart.Length > 0 && NpcKit.OnDay(NpcKit.Days("circle|" + heart), EnvMan.instance.GetDay());
        }

        private static float FeastShare(NpcActor a) { Kept k = Of(a); return k != null && k.Feast > 0L && Now - k.Feast < FeastDays * Day ? FeastMood : 0f; }

        // ---------------------------------------------------------------- work (NpcToil.Pace: a step of its work)
        private static readonly Dictionary<NpcActor, KeyValuePair<float, bool>> WorkKept = new Dictionary<NpcActor, KeyValuePair<float, bool>>(); // session: looked at a while ago
        internal static float WorkPace(NpcActor a)
        {
            if (a == null || !NpcResident.Is(a)) return 1f;
            KeyValuePair<float, bool> kept;
            if (WorkKept.TryGetValue(a, out kept) && Time.time < kept.Key) return kept.Value ? WorkWith : 1f;
            bool with = WorkFriend(a) != null;
            if (WorkKept.Count > 256) WorkKept.Clear();
            WorkKept[a] = new KeyValuePair<float, bool>(Time.time + WorkLook, with);
            return with ? WorkWith : 1f;
        }

        // a friend with work of its own near it now (null: none) — its card says the same
        internal static NpcActor WorkFriend(NpcActor a)
        {
            if (a == null || a.Worker == null || !a.Worker.Works || NpcMind.LayerNow(a) != NpcMind.Layer.Job) return null;
            foreach (NpcActor o in Folk(a)) // (at its work now, on any game: NpcMind.LayerNow — one with a job asleep or at a meal is no help)
                if (o.Worker != null && o.Worker.Works && NpcMind.LayerNow(o) == NpcMind.Layer.Job && Utils.DistanceXZ(o.transform.position, a.transform.position) <= WorkNear && NpcAttitude.OfPerson(a, o) >= NpcAttitude.WarmAt) return o;
            return null;
        }

        // ---------------------------------------------------------------- the table (NpcMind.SeatLean: metres added to a seat's way, its meal's seat picked)
        private static float SeatLean(NpcActor a, Vector3 seat)
        {
            float lean = 0f;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o == a || o.Zdo == null || (o.transform.position - seat).sqrMagnitude > SeatNear * SeatNear) continue;
                float v = NpcAttitude.OfPerson(a, o);
                if (NpcAttitude.Cold(v) || Apart(a, o)) lean += SeatFoe; else if (v >= NpcAttitude.WarmAt) lean += SeatFriend;
            }
            return lean;
        }

        // ---------------------------------------------------------------- a fight
        private static readonly Dictionary<NpcActor, float> CalledAt = new Dictionary<NpcActor, float>(); // session: its friends last called to its side
        private static readonly Dictionary<string, long> PairDay = new Dictionary<string, long>();      // session: «what|a|b» → the game day it last counted

        private static bool OncePair(string what, NpcActor a, NpcActor o)
        {
            string key = what + "|" + NpcAttitude.PersonKey(a).ToString(CultureInfo.InvariantCulture) + "|" + NpcAttitude.PersonKey(o).ToString(CultureInfo.InvariantCulture);
            long day = Today(), was;
            if (PairDay.TryGetValue(key, out was) && was == day) return false;
            if (PairDay.Count > 512) PairDay.Clear();
            PairDay[key] = day;
            return true;
        }

        // it is hit by a beast (where it is run): a friend near who fights comes to its side, where that one is run
        private static void Hurt(NpcActor a, Character by)
        {
            if (a == null || by == null || by.IsPlayer() || a.Zdo == null || !NpcResident.Is(a)) return;
            float last;
            if (CalledAt.TryGetValue(a, out last) && Time.time - last < 10f) return;
            if (CalledAt.Count > 256) CalledAt.Clear();
            CalledAt[a] = Time.time;
            ZNetView nv = by.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            string foe = NpcKit.Ref(nv.GetZDO().m_uid);
            foreach (NpcActor o in Folk(a))
            {
                if (!NpcNature.Fighter(o) || NpcVitals.IsDown(o) || Utils.DistanceXZ(o.transform.position, a.transform.position) > StandByNear || NpcAttitude.OfPerson(o, a) < NpcAttitude.WarmAt) continue;
                o.Act("standby " + foe + " " + NpcKit.Ref(a.Zdo.m_uid));
            }
        }

        // «standby <foe> <friend>» (where it is run): to its friend's side, a word as it comes; the friend keeps it in mind
        private static void StandBy(NpcActor o, string[] w)
        {
            if (w.Length < 3 || o.Mind == null || !o.Owned || o.Mind.NowLayer == NpcMind.Layer.Fight || o.Mind.Asleep) return;
            GameObject f = NpcKit.Find(NpcKit.Parse(w[1]));
            Character c = f != null ? f.GetComponent<Character>() : null;
            if (c == null || c.IsDead()) return;
            GameObject fr = NpcKit.Find(NpcKit.Parse(w[2]));
            NpcActor friend = fr != null ? fr.GetComponent<NpcActor>() : null;
            CalledAt[o] = Time.time; // (its own HurtBy below runs the Hurt list again: it is not the one hit — no calling its own friends to its side)
            o.Mind.HurtBy(c);
            NpcMind.Plan fight = o.Mind.Of(NpcMind.Layer.Fight);
            if (fight == null || fight.Kind != NpcMind.Task.Attack) return; // (its fight set to run or off now — a scene, an order: it did not come, nothing said or owed)
            Npcs.Log(o.Id + ": stands by " + (friend != null ? friend.Id : "?") + " against " + c.m_name);
            if (friend == null) return;
            NpcPhrases.SayOr(o, "stand_by", "", new Dictionary<string, string> { { "other", friend.Name } }, NpcActor.Voice.Unasked);
            if (OncePair("stood", friend, o)) { NpcMemory.Tell(friend, "stood_by", 0L, o.Key); NpcAttitude.TellPerson(friend, o, StandByWarm); }
        }

        // a fight at home beaten off (where it is run): those of its home who fought it too, not running, warmer
        private static void FightOver(NpcActor a, string kind)
        {
            if (kind != "fight_won" || a == null || !a.Owned || !NpcResident.Is(a)) return;
            bool ran, won;
            NpcMind.FoughtOf(a, out ran, out won);
            if (ran) return;
            foreach (NpcActor o in Folk(a))
            {
                bool oRan, oWon;
                if (NpcMind.FoughtOf(o, out oRan, out oWon) > FoughtWithin || oRan || !OncePair("fought", a, o)) continue;
                NpcAttitude.Add(a, NpcAttitude.PersonKey(o), FoughtWarm, NpcAttitude.Fit(a, o));
                NpcMemory.Remember(a, "fought_together", 0L, o.Key);
            }
        }

        // ---------------------------------------------------------------- a spat on foot (NpcChatter.Spats, where both are run): a scuffle, now and then —
        // its quarrel «brawl» instead of the table's (true; the pair's one quarrel a day: NpcAttitude.QuarrelAct)
        private static bool Scuffle(NpcActor a, NpcActor o, string where)
        {
            if (where != "meet" || a == null || o == null || !a.Owned || !o.Owned) return false;
            float ao = NpcAttitude.OfPerson(a, o), oa = NpcAttitude.OfPerson(o, a);
            if ((ao > NpcAttitude.HostileAt && oa > NpcAttitude.HostileAt) || UnityEngine.Random.value > ScuffleChance) return false;
            NpcActor angry = ao <= oa ? a : o, other = angry == a ? o : a;
            if (angry.Mind != null) angry.Mind.Face(other.transform.position);
            if (other.Mind != null) other.Mind.Face(angry.transform.position);
            if (angry.Body != null && angry.Body.ClipFree) angry.Body.Move("fist_palm");
            if (other.Body != null && other.Body.ClipFree) other.Body.Move("step_back");
            NpcAttitude.Quarrel(other, angry, "brawl"); NpcAttitude.Quarrel(angry, other, "brawl");
            Npcs.Log(angry.Id + ": a scuffle with " + other.Id);
            return true;
        }

        // ---------------------------------------------------------------- its look (where it is run): the feud, snoring, a nosy one
        private static readonly Dictionary<NpcActor, float> Next = new Dictionary<NpcActor, float>(); // session: its next look
        private static void Tick(NpcActor a)
        {
            float at;
            if (Next.TryGetValue(a, out at) && Time.time < at) return;
            if (Next.Count > 256) Next.Clear();
            Next[a] = Time.time + LookEvery * UnityEngine.Random.Range(0.8f, 1.2f);
            if (!a.Owned || a.Mind == null) return;
            if (Snooping.ContainsKey(a)) { Nosy(a); return; } // (on its way to a chest: only that, looked at every 2 s till it is there)
            Kept k = Of(a);
            if (k == null) return; // (its locker not here yet)
            if (!NpcResident.Is(a)) { if (k.Ult != 0L || k.Apart != 0L) { k.Ult = 0L; k.Apart = 0L; Write(a, k); } return; } // (gone from its home some other way — let go, unhappy: its ultimatum and its keeping apart go with it)
            if (Feud(a, k)) return;
            Snore(a);
            Nosy(a);
        }

        private static void Forget(NpcActor a) { Next.Remove(a); WorkKept.Remove(a); CalledAt.Remove(a); Snooping.Remove(a); SnoreNight.Remove(a); }

        // its ultimatum set, eased or carried out; its keeping apart over. True: it left
        private static bool Feud(NpcActor a, Kept k)
        {
            long now = Now;
            bool changed = false;
            if (k.Apart != 0L && k.ApartUntil < now) { k.Apart = 0L; changed = true; }
            if (k.Ult != 0L)
            {
                int lives = LivesWith(a, k.Ult);
                if (lives == 0 || NpcAttitude.Of(a, k.Ult) > NpcAttitude.HostileAt)
                {
                    Npcs.Log(a.Id + ": its ultimatum is over (" + (lives != 0 ? "no longer hostile" : "that one lives here no more") + ")");
                    k.Ult = 0L; changed = true;
                }
                else if (now - k.UltAt > UltimatumDays * Day && lives == 1 && !NpcLevers.IsProtected(a) && !NpcVitals.IsDown(a) && !a.Mind.Asleep) // (carried out only for sure and awake: that one known to live here, nothing of the story holding it — else it waits)
                {
                    string other = NameOf(a, k.Ult);
                    k.Ult = 0L; Write(a, k);
                    NpcToil.Leaves(a, "leave_feud", Lang.T("Я казав|казала: або я, або ця людина. Бувай."), "npc_left_feud", "its ultimatum ran out (" + other + ")");
                    return true;
                }
            }
            else if (a.Def != null && a.Def.Generated && !NpcLevers.IsProtected(a) && !a.Mind.Asleep && !NpcVitals.IsDown(a))
            {
                NpcActor worst = null; float lo = NpcAttitude.HostileAt;
                foreach (NpcActor o in Folk(a)) { float v = NpcAttitude.OfPerson(a, o); if (v <= lo) { lo = v; worst = o; } }
                if (worst != null)
                {
                    k.Ult = NpcAttitude.PersonKey(worst); k.UltAt = now; k.Pushed = false; changed = true;
                    NpcPhrases.SayOr(a, "quarrel_ultimatum", "", null, NpcActor.Voice.Unasked);
                    Npcs.Log(a.Id + ": ultimatum — it or " + worst.Id);
                }
            }
            if (changed) Write(a, k);
            return false;
        }

        // days left of its ultimatum (−1: none)
        internal static float UltimatumLeft(Kept k) { return k == null || k.Ult == 0L ? -1f : Mathf.Max(0f, UltimatumDays * Day - (Now - k.UltAt)); }

        // a settler snores by its seed, the same on every game and after a restart
        internal static bool Snores(NpcActor a) { return a != null && a.Zdo != null && a.Def != null && a.Def.Generated && NpcKit.Seed(a.Zdo) % 100u < (uint)(SnoreShare * 100f); }

        private static readonly Dictionary<NpcActor, long> SnoreNight = new Dictionary<NpcActor, long>(); // session: the night it last minded
        private static void Snore(NpcActor a)
        {
            if (!a.Mind.Asleep || Snores(a) || EnvMan.instance == null || !EnvMan.IsNight()) return;
            long night = (long)(NpcRescue.Now / Day + 0.5), was; // (a night counted noon to noon: the game's day turns at midnight, in the middle of it)
            if (SnoreNight.TryGetValue(a, out was) && was == night) return;
            Vector3 bed = BedAt(a);
            if (bed == Vector3.zero) return;
            foreach (NpcActor o in Folk(a))
            {
                if (!Snores(o) || !NpcMind.AsleepOf(o)) continue; // (it snores only asleep in its bed: on any game, NpcMind.AsleepOf)
                Vector3 other = BedAt(o);
                if (other == Vector3.zero || Vector3.Distance(other, bed) > SnoreNear) continue;
                SnoreNight[a] = night;
                if (UnityEngine.Random.value < SnoreChance) NpcAttitude.Quarrel(a, o, "snore");
                return;
            }
        }

        private static Vector3 BedAt(NpcActor a) { GameObject b = NpcKit.Find(NpcLocker.Ref(a.Zdo, NpcResident.KBed)); return b != null ? b.transform.position : Vector3.zero; }

        private static readonly Dictionary<NpcActor, NpcActor> Snooping = new Dictionary<NpcActor, NpcActor>(); // session: whose chest it is on its way to
        private static void Nosy(NpcActor a)
        {
            NpcActor o;
            if (Snooping.TryGetValue(a, out o))
            {
                Container c = o != null && o.Worker != null ? o.Worker.Chest : null;
                NpcMind.Plan w = a.Mind.Of(NpcMind.Layer.Whim);
                if (c == null || w == null || w.Whose != NosyWhim) { Snooping.Remove(a); return; }
                Next[a] = Time.time + 2f; // (on its way: looked at often)
                if (Utils.DistanceXZ(a.transform.position, c.transform.position) > NosyNear) return;
                Snooping.Remove(a);
                a.Mind.EndWhim(NosyWhim);
                if (!NpcMind.AsleepOf(o) && Utils.DistanceXZ(o.transform.position, a.transform.position) <= NosySeen) NpcAttitude.Quarrel(o, a, "chest"); // (awake on any game: NpcMind.AsleepOf)
                return;
            }
            if (!NpcNature.Is(a, "nosy") || !a.Mind.AtLeisure || !a.Mind.WhimFree(NosyWhim) || UnityEngine.Random.value > NosyChance) return;
            List<NpcActor> chests = new List<NpcActor>();
            foreach (NpcActor x in Folk(a)) if (x.Worker != null && x.Worker.Chest != null) chests.Add(x);
            if (chests.Count == 0) return;
            o = chests[UnityEngine.Random.Range(0, chests.Count)];
            if (a.Mind.TakeWhim(new NpcMind.Plan { Kind = NpcMind.Task.Go, To = o.Worker.Chest.transform.position, Near = 1.2f }, NosyFor, NosyWhim))
            {
                Snooping[a] = o;
                Next[a] = Time.time + 2f;
                Npcs.Log(a.Id + ": nosy — goes to look into " + o.Id + "'s chest");
            }
        }

        // ---------------------------------------------------------------- keeping apart (its talk)
        internal static string ApartSaid = "";
        private static void KeepApart(NpcActor a, Player p, string[] w)
        {
            ApartSaid = "…";
            if (a == null || p == null) return;
            string kind;
            NpcActor o = NpcAttitude.Grudge(a, out kind);
            if (o == null) { ApartSaid = NpcPhrases.Compose(a, "peace_none", p) ?? a.Form(Lang.T("Та ми й не сварилися.")); return; }
            if (NpcPatience.Of(a, p) < NpcPatience.Dry) // (out of patience with you: no, as with peace — and it costs a little)
            {
                ApartSaid = NpcPhrases.Compose(a, "peace_no", p, new Dictionary<string, string> { { "other", o.Name } }) ?? a.Form(Lang.T("Ні. Хай спершу перше слово скаже."));
                NpcPatience.Spend(a, p, 4f);
                return;
            }
            a.Act("apart " + NpcAttitude.PersonKey(o).ToString(CultureInfo.InvariantCulture) + " " + ((long)(NpcRescue.Now + ApartDays * Day)).ToString(CultureInfo.InvariantCulture));
            ApartSaid = NpcPhrases.Compose(a, "apart_yes", p, new Dictionary<string, string> { { "other", o.Name } }) ?? a.Form(Lang.T("Гаразд. Обходитиму стороною."));
        }

        // «apart <key> <until>» where it is run
        private static void ApartAct(NpcActor a, string[] w)
        {
            long key, until;
            if (w.Length < 3 || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out key) || !long.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out until)) return;
            Kept k = Of(a);
            if (k == null) return;
            k.Apart = key; k.ApartUntil = until;
            if (k.Ult == key && !k.Pushed) { k.UltAt += (long)(ApartDays * Day); k.Pushed = true; } // (kept apart from the very one of its ultimatum: that many days more before it goes, once — time to cool down or make peace, not a way to put it off for good)
            Write(a, k);
            Npcs.Log(a.Id + ": keeps apart from " + key + " till " + until);
        }

        // either of them keeps away from the other now (any game: their lockers)
        internal static bool Apart(NpcActor a, NpcActor o)
        {
            if (a == null || o == null) return false;
            long now = Now;
            Kept ka = Of(a), ko = Of(o);
            return (ka != null && ka.Apart == NpcAttitude.PersonKey(o) && ka.ApartUntil > now) || (ko != null && ko.Apart == NpcAttitude.PersonKey(a) && ko.ApartUntil > now);
        }

        // ---------------------------------------------------------------- a feast (the board's «Свято», your game)
        // why it cannot be now (null: it can); need: dishes it wants
        internal static string FeastWhyNot(NpcHeart h, Player p, out int need)
        {
            List<NpcActor> folk = h != null ? NpcHeart.Residents(h.Id) : new List<NpcActor>();
            need = Mathf.Max(2, folk.Count);
            if (folk.Count < 2) return Lang.T("Свято — коли в оселі живе хоча б двоє.");
            long last = LastFeast(folk);
            if (last > 0L && Now - last < FeastEvery * Day) return Lang.F("Наступне свято — за {0}.", NpcRescue.Left((long)(FeastEvery * Day) - (Now - last)));
            if (p == null || Dishes(p.GetInventory()) < need) return Lang.F("Для свята потрібно {0} у торбі: по одній на кожного.", Lang.N(need, "готова страва|готові страви|готових страв"));
            return null;
        }

        private static long LastFeast(List<NpcActor> folk) { long last = 0L; foreach (NpcActor o in folk) { Kept k = Of(o); if (k != null && k.Feast > last) last = k.Feast; } return last; }

        private static int Dishes(Inventory inv)
        {
            int n = 0;
            if (inv != null) foreach (ItemDrop.ItemData it in inv.GetAllItems()) if (it.m_shared.m_food > 0f && NpcKitchen.IsDish(it.m_shared.m_name)) n += it.m_stack;
            return n;
        }

        // each of its people given its own dish (NpcActor.Pay: a feast that does nothing where one is run — its locker not there yet — gives that dish back);
        // a second click before their games answer is held off here a while (FeastWait), not taken twice
        private const float FeastWait = 30f;
        private static readonly Dictionary<ZDOID, float> FeastAsked = new Dictionary<ZDOID, float>(); // session: a feast just given at this heart, on this game
        internal static void Feast(NpcHeart h, Player p)
        {
            int need;
            float at;
            string no = h != null && FeastAsked.TryGetValue(h.Id, out at) && Time.time - at < FeastWait ? Lang.T("Свято вже триває.") : FeastWhyNot(h, p, out need);
            if (no != null) { if (p != null) p.Message(MessageHud.MessageType.Center, no); return; }
            if (FeastAsked.Count > 64) FeastAsked.Clear();
            FeastAsked[h.Id] = Time.time;
            Inventory inv = p.GetInventory();
            string when = Now.ToString(CultureInfo.InvariantCulture);
            int given = 0;
            foreach (NpcActor o in NpcHeart.Residents(h.Id))
            {
                if (o == null) continue;
                ItemDrop.ItemData dish = null;
                foreach (ItemDrop.ItemData it in inv.GetAllItems()) if (it.m_shared.m_food > 0f && NpcKitchen.IsDish(it.m_shared.m_name)) { dish = it; break; }
                if (dish == null) break;
                o.Pay(p, dish, "feast " + when);
                given++;
            }
            p.Message(MessageHud.MessageType.Center, Lang.T("Свято! Усі в оселі трохи потеплішали одне до одного."));
            Npcs.Log("a feast at «" + h.Title + "» (" + given + " dishes)");
        }

        // «feast <when>» where it is run (paid with its dish): glad, warmer to each of its home, a day of good mood
        private static void FeastAct(NpcActor a, string[] w)
        {
            long when;
            if (w.Length < 2 || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out when)) { NpcActor.NotDone(); return; }
            Kept k = Of(a);
            if (k == null) { NpcActor.NotDone(); return; } // (its locker not here yet: its dish goes back)
            k.Feast = when;
            Write(a, k);
            NpcFeel.Tell(a, NpcFeel.Joy, 300f);
            foreach (NpcActor o in Folk(a)) NpcAttitude.Add(a, NpcAttitude.PersonKey(o), FeastWarm, NpcAttitude.Fit(a, o));
            NpcMemory.Remember(a, "feast", 0L, "");
            if (UnityEngine.Random.value < 0.7f) NpcPhrases.SayOr(a, "feast", "", null, NpcActor.Voice.Unasked);
        }

        // ---------------------------------------------------------------- its words: its ultimatum, or a grudge it holds, weighs on it. Both said to anyone
        // (NpcPhrases group «ultimatum», not one only for one it trusts), both open its talk's answers to a quarrel — «Помиріться вже», «подалі»: the
        // card and the board send you there, and a grudge outlives the memory of its quarrel (that fades in days, the cold does not)
        private static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            Kept k = Of(a);
            if (k != null && k.Ult != 0L) { themes.Add(new KeyValuePair<string, float>("quarrel_ultimatum", 45f)); return; }
            string kind;
            if (NpcResident.Is(a) && NpcAttitude.Grudge(a, out kind) != null) themes.Add(new KeyValuePair<string, float>("quarrel_grudge", 25f));
        }

        // the console: raven npc neighbours <who>
        private static string Describe(NpcActor a)
        {
            Kept k = Of(a);
            if (k == null) return Lang.T("Шафка ще не тут.");
            StringBuilder sb = new StringBuilder(a.Name + ":");
            if (k.Ult != 0L) sb.Append(" ultimatum ").Append(NameOf(a, k.Ult)).Append(" (").Append(NpcRescue.Left((long)UltimatumLeft(k))).Append(")");
            if (k.Apart != 0L && k.ApartUntil > Now) sb.Append(" apart ").Append(NameOf(a, k.Apart)).Append(" (").Append(NpcRescue.Left(k.ApartUntil - Now)).Append(")");
            if (k.Feast > 0L) sb.Append(" feast ").Append(NpcRescue.Left(Now - k.Feast)).Append(" ago");
            sb.Append(Snores(a) ? " · snores" : "");
            NpcActor wf = WorkFriend(a);
            if (wf != null) sb.Append(" · works by ").Append(wf.Name);
            sb.Append(" · mood: foe ").Append(FoeShare(a).ToString("0", CultureInfo.InvariantCulture)).Append(", friend ").Append(FriendShare(a).ToString("0", CultureInfo.InvariantCulture)).Append(", feast ").Append(FeastShare(a).ToString("0", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
#endif
