#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person asks about you and remembers what you said (the user 2026-11-12: a talk is two-way — «ти ж казав, що не любиш дощ»).
    //   it asks     a worry of its own, «ask_you_<topic>» (the group «ask»), only while a talk asks (NpcVoice.Asked: never over its head — it wants an
    //               answer): one question a game day a person, a topic you have not answered it, in an order of its own (its seed); a settler living at a
    //               heart, not cold to you; a curious one sooner (AskWeight: at the top it opens its talk, below it is among «ще на думці»)
    //   you answer  the place «c_ask_you_<topic>»: «!youare <topic> <value>» keeps it («none»: you would not say — it does not ask that again)
    //   it recalls  «said_<topic>_<value>» (the group «said»), in the talk and over its head, when the moment fits (Topics: Fits — rain now, you just
    //               fought, the night) or, for a topic marked Idle, on a day of its own now and then; never on the day you said it
    // Kept in your character, one row a person (NpcTalkMemo «q»): what you told THIS person — another one asks for itself.
    // A new topic: a row of Topics, its place c_ask_you_<topic> in the talk, its words ask_you_<topic> and said_<topic>_<value> (NpcMoreSayPatterns, Ask).
    internal static partial class NpcTalk
    {
        private sealed class AskTopic { public string Id; public Func<Player, string, bool> Fits; public bool Idle; }

        private static readonly AskTopic[] Topics =
        {
            new AskTopic { Id = "rain", Fits = (p, v) => { string w = NpcVoice.Weather(); return w == "rain" || w == "storm"; } },
            new AskTopic { Id = "fight", Fits = (p, v) => Time.time - Senses.HitMonsterAt < NpcVoice.YouFoughtFor },
            new AskTopic { Id = "sleep", Fits = (p, v) => v == "owl" ? EnvMan.IsNight() : NpcVoice.DayPart() == "morning" },
            new AskTopic { Id = "food", Fits = (p, v) => NpcVoice.DayPart() == "evening", Idle = true },
            new AskTopic { Id = "land", Fits = (p, v) => false, Idle = true },
        };

        internal static float AskWeight = 34f, SaidFits = 46f, SaidIdle = 24f, SaidIdleChance = 0.2f, AskNotBelow = -15f;

        // what you told this person of yourself: topic → value
        private static Dictionary<string, string> YouAre(Row r)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (r == null) return d;
            foreach (string pair in r.Words.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { int eq = pair.IndexOf('='); if (eq > 0) d[pair.Substring(0, eq)] = pair.Substring(eq + 1); }
            return d;
        }

        // «!youare <topic> <value>»
        private static void YouAreDeed(NpcActor a, Player p, string[] w)
        {
            if (a == null || p == null || w.Length < 3) return;
            Row r = PutRow(p, "q", a, "");
            Dictionary<string, string> d = YouAre(r);
            d[w[1]] = w[2];
            List<string> l = new List<string>();
            foreach (KeyValuePair<string, string> e in d) l.Add(e.Key + "=" + e.Value);
            r.Words = string.Join(",", l.ToArray()); r.Count = Today; r.At = NowSec;
            SaveRows(p);
            Npcs.Log(a.Id + ": knows of you — " + w[1] + " " + w[2]);
        }

        // a source of NpcVoice (NpcModules): its question to you, and what it recalls of your answers
        internal static void AskThemes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            if (a == null || p == null || p != Player.m_localPlayer || a.Zdo == null || a.Def == null || !a.Def.Settler) return; // (your answers live in your character: this game's player only)
            Row r = FindRow(p, "q", a, "");
            Dictionary<string, string> d = YouAre(r);
            int today = Today;
            bool saidToday = r != null && !Elsewhere(r) && r.Count == today;
            uint seed = NpcKit.Seed(a.Zdo);
            if (!saidToday) foreach (AskTopic t in Topics) // what it recalls
            {
                string v;
                if (!d.TryGetValue(t.Id, out v) || v == "none") continue;
                if (t.Fits(p, v)) themes.Add(new KeyValuePair<string, float>("said_" + t.Id + "_" + v, SaidFits));
                else if (t.Idle && NpcNature.H(seed, "said" + t.Id + today) < SaidIdleChance) themes.Add(new KeyValuePair<string, float>("said_" + t.Id + "_" + v, SaidIdle));
            }
            if (!NpcVoice.Asked || saidToday || !NpcResident.Is(a) || NpcAttitude.Of(a, p.GetPlayerID()) < AskNotBelow) return; // its question
            AskTopic next = null; float first = 2f;
            foreach (AskTopic t in Topics) { if (d.ContainsKey(t.Id)) continue; float o = NpcNature.H(seed, "ask" + t.Id); if (o < first) { first = o; next = t; } }
            if (next != null) themes.Add(new KeyValuePair<string, float>("ask_you_" + next.Id, AskWeight * Mathf.Lerp(0.7f, 1.3f, NpcPersona.Of(a).Curiosity)));
        }

        private static void WireAsk() { Deeds["youare"] = YouAreDeed; }
    }
}
#endif
