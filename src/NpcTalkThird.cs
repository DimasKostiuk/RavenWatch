#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Another of its people butts into your talk (the user 2026-11-07, «третій встряє»; the way your raven does: NpcTalkRaven). You talk to A, B of the same home stands near
    // and is A's friend or A's foe: B says its word into the same window (RavenBook.SayOther: its name over its block; over its head too, so others near hear it), A answers it,
    // and that is not nothing between them:
    //   friend   «Правду каже» — both a little warmer to each other (NpcAttitude.TellPerson, as far as they may be: Fit)
    //   foe      «Тебе не питали» — A holds it against B (NpcAttitude.Quarrel «butted»: once a game day a pair, its memory quarrel_butted, a worry of its own next time)
    // One chance a talk (ThirdChance), only on its worry, its opening and small talk (CutPlaces), never for one in trouble, the same pair not again soon (ThirdEvery).
    // Asked on your game, where the talk runs: who is free to speak is what its owner wrote (NpcVoice.FreeAnywhere), how they feel about each other is their lockers.
    // What A says of B when you ask it (NpcTalkAbout) and B hears it is the same idea: there, B's own word for what it heard (third_overheard_*).
    // Words: third_<friend|foe> (B's, {other} = A), third_reply_<friend|foe> (A's, {other} = B).
    internal static partial class NpcTalk
    {
        internal static float ThirdChance = 0.3f, ThirdNear = 6f, ThirdHear = 10f, ThirdEvery = 600f, ThirdWarm = 3f, ThirdLookEvery = 5f;
        private static bool _thirdRolled;
        private static float _thirdLookAt = -100f; // (no one near: looked for again only after a while — every showing would go through its whole home)
        private static readonly Dictionary<string, float> ThirdAfter = new Dictionary<string, float>(); // session: a pair's seeds → when it may come again

        // a showing's third person: null, none (after your raven's word: one voice a showing)
        private static List<RavenBook.TalkSay> Third(string node, string worry, bool opening, Player p)
        {
            if (_thirdRolled || !Minded || With == null || p == null || NpcRescueTalk.State(With) != null || !(opening || worry != null || CutPlaces.Contains(node))) return null;
            if (Time.time - _thirdLookAt < ThirdLookEvery) return null;
            _thirdLookAt = Time.time;
            float to;
            NpcActor b = Neighbour(With, p, out to);
            if (b == null) return null;
            string key = Pair(With, b);
            float after;
            if (ThirdAfter.TryGetValue(key, out after) && Time.time < after) return null;
            _thirdRolled = true;
            if (UnityEngine.Random.value >= ThirdChance) return null;
            string kind = to >= 25f ? "friend" : "foe";
            string line = NpcPhrases.Compose(b, "third_" + kind, p, new Dictionary<string, string> { { "other", With.Name } });
            if (string.IsNullOrEmpty(line)) return null;
            if (ThirdAfter.Count > 128) ThirdAfter.Clear();
            ThirdAfter[key] = Time.time + ThirdEvery;
            List<RavenBook.TalkSay> said = Others(b, line);
            string reply = NpcPhrases.Compose(With, "third_reply_" + kind, p, new Dictionary<string, string> { { "other", b.Name } });
            if (!string.IsNullOrEmpty(reply)) said.Add(new RavenBook.TalkSay { Kind = RavenBook.SayPerson, Text = NpcBody.Spoken(With, reply) });
            if (kind == "foe") NpcAttitude.Quarrel(With, b, "butted");
            else if (WarmPair(With, b)) { NpcAttitude.TellPerson(With, b, ThirdWarm); NpcAttitude.TellPerson(b, With, ThirdWarm); } // (friends: warmer, once a game day)
            Npcs.Log(With.Id + ": " + b.Id + " butts into the talk (" + kind + ")");
            return said;
        }

        // another person's words in the window and over its head (its own gesture too)
        private static List<RavenBook.TalkSay> Others(NpcActor b, string line)
        {
            string plain = NpcBody.Spoken(b, line);
            b.Say(plain, Player.m_localPlayer, NpcActor.Voice.Over);
            return new List<RavenBook.TalkSay> { new RavenBook.TalkSay { Kind = RavenBook.SayOther, Name = b.Name, Text = plain } };
        }

        // one of its home near it and near you, free to speak, who is its friend or its foe — the strongest feeling first (to: how b feels about it)
        private static NpcActor Neighbour(NpcActor a, Player p, out float to)
        {
            to = 0f;
            ZDOID heart = a.Zdo != null ? NpcKit.GetRef(a.Zdo, NpcResident.KHeart) : ZDOID.None;
            if (heart.IsNone()) return null;
            NpcActor best = null;
            foreach (NpcActor b in NpcHeart.Residents(heart))
            {
                if (b == null || b == a || b.Def == null || !b.Def.Settler) continue;
                if (Utils.DistanceXZ(b.transform.position, a.transform.position) > ThirdNear || Utils.DistanceXZ(b.transform.position, p.transform.position) > ThirdHear) continue; // (near first: the lockers after)
                if (!NpcVoice.FreeAnywhere(b)) continue;
                float f = NpcAttitude.OfPerson(b, a);
                if ((f >= 25f || f <= -15f) && Mathf.Abs(f) > Mathf.Abs(to)) { to = f; best = b; }
            }
            return best;
        }

        // warmth between two people through a talk: once a game day a pair (a quarrel is once a day a pair too: NpcAttitude.Quarrel) — praise again and again is no way to make them dear
        private static readonly Dictionary<string, int> PairWarmed = new Dictionary<string, int>(); // session: a pair → the game day it was last warmed
        private static bool WarmPair(NpcActor a, NpcActor b)
        {
            int today = Today, day;
            string key = Pair(a, b);
            if (PairWarmed.TryGetValue(key, out day) && day == today) return false;
            if (PairWarmed.Count > 256) PairWarmed.Clear();
            PairWarmed[key] = today;
            return true;
        }

        private static string Pair(NpcActor a, NpcActor b) { uint x = SeedOf(a), y = SeedOf(b); return x < y ? x + ":" + y : y + ":" + x; }
    }
}
#endif
