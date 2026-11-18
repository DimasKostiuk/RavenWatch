#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The story's people together (the user 2026-11-16: «бесіди між нпс… сісти біля каміну поспілкуватись»). A gathering is not a thing of
    // its own in the world: it is a name. The people whose days (StoryLife: «at: … meet <name>», «spot: … meet <name>») name the same one in
    // the same hours come to its place each by its own day — nothing leads them — and while two or more of them are there they talk the
    // talks written for it (assets/story/meets/*.txt, «[meet <name>]», each line by the one named: NpcChatter, its head). Not a second
    // small talk: the talk itself, its pauses, the floor and the voices are NpcChatter's; here only who is there now.
    //   Who is there   its day has the gathering now (StoryLife.MeetNow), it is within Near m of the gathering's place, and it may speak
    //                  (NpcChatter.Ready: not in another talk, not with a player, not in a scene, not just out of a talk)
    //   Who starts     the one of them with the lowest id, on the game that runs it (never two games at once); a look every LookMin…LookMax s
    //                  a person, Chance of them a try
    //   A guest      a player sits within Near m of the gathering's place (on a seat of the game, not a ship's, or on the ground: Seated) — the
    //                  condition «guest» of a talk (`? guest`; «he»: the one who starts). He sat down: they notice him — the next look of the one who
    //                  starts tries at once (no Chance), their pause after the last talk is not waited (NpcChatter.Ready, rested), and of the talks
    //                  that fit, one with «guest» is said first (NpcChatter.Meet, first). Once a sitting-down; again only NoticeAgain s after the
    //                  last (up and down by the fire: no nagging). Another game's player on a seat of the game is not seen here (the game keeps
    //                  that only on his own): his sitting on the ground is
    //   A pair         two of them near each other (PairNear m) outside a gathering (the user 2026-11-16: Lit and Torvald — «короткі репліки, коли вони
    //                  поруч поза зборами»): a short exchange of their pair's talks («[pair <a> <b>]», assets/story/meets; NpcChatter.Pair) — at random
    //                  intervals, PairMin…PairMax s a pair (the user: nothing on a schedule; the first one of a session not at once), only where a player
    //                  hears it (NpcChatter: earshot, the floor). Their talks' «? …» say which step of the story they are at (their arc: the conditions)
    // Nothing kept, nothing sent: after a reload they are simply there again by their days. A talk now, for the author: «raven npc meet <who>».
    internal static class StoryMeets
    {
        internal const string Kind = "meets";
        internal static float LookMin = 6f, LookMax = 11f, Near = 7f, Chance = 0.5f, NoticeAgain = 120f;
        internal static float PairNear = 5f, PairMin = 240f, PairMax = 720f; // m · s between two exchanges of a pair, at random
        private static readonly Dictionary<string, float> PairNext = new Dictionary<string, float>(); // session: a pair's key → its next exchange (Time.time)
        internal const string GuestWord = "guest"; // (a talk's condition: a player sits in the circle — registered under this name in Wire: storycheck reads the name there)
        private static readonly Dictionary<NpcActor, float> Next = new Dictionary<NpcActor, float>(); // session: its next look
        private static readonly HashSet<string> Sat = new HashSet<string>(), Waits = new HashSet<string>(); // session: gatherings with a player sitting at the last look · with him not noticed yet
        private static readonly Dictionary<string, float> NoticedAt = new Dictionary<string, float>(); // session: a gathering → when it last noticed a guest
        private static readonly List<NpcActor> _there = new List<NpcActor>();

        internal static void Wire()
        {
            NpcChatter.More.Add(() => StoryFiles.All(Kind, null, false).Values);
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(a => Next.Remove(a));
            Npcs.Commands["meet"] = (p, who, a, aim) => Force(who);
            StoryCond.Register("guest", new StoryCond.Kind { Dry = "moment", Usage = "guest   (у колі зборів того, хто починає розмову, сидить гравець: assets/story/meets)",
                Test = (p, w) => { NpcActor him = NpcTalk.Him; Vector3 at; return him != null && StoryLife.MeetNow(him, out at).Length > 0 && Seated(at); } });
            // a true tale by the fire (the user 2026-11-16: heard there, it is in the book's «Сага» as if asked, and no longer to be lost)
            StoryCond.Register("untold", new StoryCond.Kind { Dry = "moment", Min = 2, Usage = "untold <розмова> <початок місця>   (гравцеві біля того, хто починає розмову зборів, ця людина ще може розповісти таке місце своєї розмови: assets/story/meets, рядок {told …})",
                Test = (p, w) => Untold(NpcTalk.Him, w[1], w[2]) });
            NpcChatter.Tells = Tell;
            NpcActor.Told = Heard;
        }

        // «{told <talk> <place prefix>}»: one place of that talk, starting so, that the one who hears may still be told and has not heard (its time:
        // NpcTalk.Askable — Torvald's tales are his chapter 1's, and gone with it), its words as the person says them there. The same one as «untold» asked
        // «untold» as asked for a talk being picked: its answer kept UntoldFor s (every look of the one who starts asks each talk that has it — a walk over a
        // talk's places each), dropped the moment a tale is heard here (Heard)
        internal static float UntoldFor = 5f;
        private sealed class Kept { public float Until; public bool Yes; public Player For; }
        private static readonly Dictionary<string, Kept> _untold = new Dictionary<string, Kept>(); // session: «<talk> <prefix>» → the answer for this game's player
        private static bool Untold(NpcActor near, string talk, string prefix)
        {
            Player me = Listener(near);
            if (me == null) return false;
            string key = talk + " " + prefix;
            Kept k;
            if (_untold.TryGetValue(key, out k) && k.For == me && Time.time < k.Until) return k.Yes;
            if (k == null) _untold[key] = k = new Kept();
            k.For = me; k.Until = Time.time + UntoldFor; k.Yes = NpcTalk.Untold(me, talk, prefix) != null;
            return k.Yes;
        }

        internal static float HearNear = 12f; // m from the one who tells it (or the gathering's place: Listener): heard
        private static string[] Tell(NpcActor who, string what)
        {
            string[] w = what.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            string place = w.Length == 2 ? NpcTalk.Untold(Listener(who), w[0], w[1]) : null;
            NpcTalk.Talk t = place != null ? NpcTalk.Find(w[0]) : null;
            NpcTalk.Node n;
            if (t == null || !t.Nodes.TryGetValue(place, out n) || n.Says.Length == 0) return null;
            return new[] { n.Says, w[0] + " " + place };
        }

        // whom a tale is judged for: this game's own player, within HearNear m of the gathering's place of that person — the same one for «untold»
        // and the line (the review of 2026-11-16: the nearest player of the game that runs the talk may be another's, whose story this game does not
        // keep — he was told tales he knew, every time; and the nearest to the one who starts and to the teller were two players). None: no tale here
        private static Player Listener(NpcActor near)
        {
            Player me = Player.m_localPlayer;
            Vector3 at;
            if (near == null || me == null || me.IsDead() || StoryLife.MeetNow(near, out at).Length == 0) return null;
            return (me.transform.position - at).sqrMagnitude <= HearNear * HearNear ? me : null;
        }

        // a place of a talk said over a head reached this game: its own player near the one who said it has heard it (his book: NpcTalk.Hear; what it
        // opened is said at once — no talk window is open to say it under)
        private static void Heard(NpcActor a, string told)
        {
            Player me = Player.m_localPlayer;
            int sp = told.IndexOf(' ');
            if (me == null || me.IsDead() || sp <= 0 || (me.transform.position - a.transform.position).sqrMagnitude > HearNear * HearNear) return;
            NpcTalk.Hear(me, told.Substring(0, sp), told.Substring(sp + 1));
            _untold.Clear(); // (what is left untold changed: asked anew)
            RavenBook.FlushTalkNotes(me);
        }

        // a player sits within Near m of the place (this game's players: a seat of the game, not a ship's — or on the ground)
        private static bool Seated(Vector3 at)
        {
            foreach (Player p in Player.GetAllPlayers())
                if (p != null && !p.IsDead() && !p.InBed() && (p.IsSitting() || (p.IsAttached() && !p.IsAttachedToShip())) && (p.transform.position - at).sqrMagnitude <= Near * Near) return true; // (a bed holds him too: not a guest — the review of 2026-11-16)
            return false;
        }

        // a guest at this gathering not noticed yet: he sat down since a look of any of them, and the last notice is NoticeAgain s ago. Kept till a
        // talk of «guest» is said (the one who starts may look later than the one who saw him sit) or he stands up
        private static bool NewGuest(string name, Vector3 at)
        {
            if (!Seated(at)) { Sat.Remove(name); Waits.Remove(name); return false; }
            float last;
            if (Sat.Add(name) && (!NoticedAt.TryGetValue(name, out last) || Time.time - last >= NoticeAgain)) Waits.Add(name);
            return Waits.Contains(name);
        }

        private static void Tick(NpcActor a)
        {
            float next;
            if (Next.TryGetValue(a, out next) && Time.time < next) return;
            if (!StoryPeople.Wears(a.Id)) { Next[a] = Time.time + 600f; return; } // (not of the story's people: no day of its own)
            Next[a] = Time.time + Random.Range(LookMin, LookMax);
            Vector3 at;
            string name = StoryLife.MeetNow(a, out at);
            if (name.Length == 0) { PairTick(a); return; }
            bool guest = NewGuest(name, at); // (he sat down: noticed — no chance, no pause waited)
            if ((!guest && Random.value > Chance) || !Gather(a, false, guest)) return;
            if (!NpcChatter.Meet(_gathering, _there, false, guest ? GuestWord : null) || !guest) return;
            Waits.Remove(name); NoticedAt[name] = Time.time;
        }

        // outside a gathering: another of the story's people near with a pair's talks for the two, it the one who starts, their time come, both free
        private static void PairTick(NpcActor a)
        {
            Vector3 at;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o == a || !StoryPeople.Wears(o.Id) || (o.transform.position - a.transform.position).sqrMagnitude > PairNear * PairNear) continue;
                if (!NpcChatter.HasPair(a.Id, o.Id) || !NpcChatter.First(a, o) || StoryLife.MeetNow(o, out at).Length > 0) continue; // (that one at its gathering: the gathering's talks)
                string key = NpcChatter.PairKey(a.Id, o.Id);
                float next;
                if (!PairNext.TryGetValue(key, out next)) { PairNext[key] = Time.time + Random.Range(PairMin * 0.25f, PairMin); continue; } // (first seen together this session: not at once)
                if (Time.time < next || !NpcChatter.Ready(a) || !NpcChatter.Ready(o)) continue;
                if (NpcChatter.Pair(a, o)) PairNext[key] = Time.time + Random.Range(PairMin, PairMax);
                return;
            }
        }

        // those at its gathering now, itself first; false: none now, too few, or another of them is the one who starts
        private static string _gathering = "";
        private static bool Gather(NpcActor a, bool force, bool rested = false)
        {
            Vector3 at, at2;
            _gathering = StoryLife.MeetNow(a, out at);
            _there.Clear();
            if (_gathering.Length == 0 || (a.transform.position - at).sqrMagnitude > Near * Near || !NpcChatter.Ready(a, force || rested)) return false;
            _there.Add(a);
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o == a || !StoryPeople.Wears(o.Id) || StoryLife.MeetNow(o, out at2) != _gathering) continue;
                if ((o.transform.position - at).sqrMagnitude > Near * Near || !NpcChatter.Ready(o, force || rested)) continue;
                if (!force && !NpcChatter.First(a, o)) return false; // (that one starts it, where it is run)
                _there.Add(o);
            }
            return _there.Count >= 2;
        }

        private static string Force(NpcActor a)
        {
            if (a == null || !a.Owned) return "Його веде інша гра.";
            Vector3 at;
            string name = StoryLife.MeetNow(a, out at);
            if (name.Length == 0) return a.Name + ": за розкладом зараз не на зборах (raven npc day " + a.Id + ").";
            if (!Gather(a, true)) return a.Name + ": збори «" + name + "», але поруч (до " + Mathf.RoundToInt(Near) + " м від їхнього місця) нема ще когось із них, хто може говорити.";
            return NpcChatter.Meet(name, _there, true, Seated(at) ? GuestWord : null) ? "Заговорили: збори «" + name + "», тут " + _there.Count + "." : "Збори «" + name + "»: жодна розмова зараз не пасує тим, хто тут (assets/story/meets).";
        }
    }
}
#endif
