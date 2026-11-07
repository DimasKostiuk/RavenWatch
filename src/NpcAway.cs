#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Що тут сталося, поки мене не було?» (the user 2026-11-05: on a server where the story has not moved, a person still tells what
    // happened while you were away). One question in its talk (NpcTalk common «away»), what it answers from what it remembers:
    //   the story's own first   a module's Source (StoryTales: a tale of the world it knows and you do not — kept as before, its own rules)
    //   then its own memories   NpcMemory entries made while you were away (Window: from the last time you were round its base to when
    //                           you came back — Tick, where it is run: a look every LookEvery s for players within Near m, written into its
    //                           locker at most once in Mark s a player, so a reload or a night off keeps it), not told you yet (your character keeps when you were told: NpcTalk.AwayToldAt), worth telling
    //                           (Skip: what is about you, or its story's own), with words for it (away_<kind>, else its mem_<kind>), its
    //                           own only to one it trusts (NpcVoice.Groups Personal, NpcVoice.Trusted), not what it told you lately (a
    //                           worry it opened with: NpcTalk.ToldLately) — the weightiest Most of them, its weight first, then the newest
    // The question is there only when it has something (?awaynews); the answer «Зрозумів» marks it told (!awaytold): the tale known, the
    // memories told up to now, their worries weighed less a while (NpcTalk.MarkTold). Closed with Esc: asked again next time.
    internal static class NpcAway
    {
        internal static readonly int KAway = "RavenWatch_npcAway".GetStableHashCode(); // string "player=near,from,to;…" — in its locker: when you were last round it, your last absence
        internal static int Most = 3;
        private const int MostPlayers = 8;

        internal sealed class Source { public Func<NpcActor, Player, string> Line; public Action<NpcActor, Player> Told; }
        internal static readonly List<Source> Sources = new List<Source>();

        // what is not news to tell (about you, its own story, its words kept elsewhere)
        private static readonly HashSet<string> Skip = new HashSet<string> { "missed", "hired", "fired", "brushed_off", "insulted", "zoned_out", "raven_mocked", "clothes_taken", "secret_told", "request_done", "settled", "rescued", "revived", "promise_kept", "promise_broken", "heard_kept", "heard_broken" };
        private static bool Skipped(string kind) { return Skip.Contains(kind) || kind.StartsWith("tale_", StringComparison.Ordinal) || kind.StartsWith("you_land_", StringComparison.Ordinal) || kind.StartsWith("arc_", StringComparison.Ordinal); }

        // how long not round its base counts as away: a third of a game day, at least 15 min (a walk to the forest and back is no absence)
        internal static float AwayLeast = 900f; // (the test: raven npc tune NpcAway.AwayLeast 30)
        internal static float AwayMin { get { return Mathf.Max(AwayLeast, (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f) / 3f); } }
        private static bool Away(long since, long now) { return now - since - NpcClock.Skipped(since, now) >= (long)AwayMin; } // (a night slept is no absence: NpcClock)
        internal static float Near = 60f, LookEvery = 30f, Mark = 600f; // m round it that is «here» · s between its looks · s between its writes of you here

        private sealed class Seen { public long Near, From, To; }

        private static Dictionary<long, Seen> Read(NpcActor a)
        {
            Dictionary<long, Seen> d = new Dictionary<long, Seen>();
            string kept = NpcLocker.Get(a, KAway);
            if (string.IsNullOrEmpty(kept)) return kept == null ? null : d;
            foreach (string e in kept.Split(';'))
            {
                int eq = e.IndexOf('=');
                string[] w = eq > 0 ? e.Substring(eq + 1).Split(',') : null;
                long who; Seen x = new Seen();
                if (w == null || w.Length < 3 || !long.TryParse(e.Substring(0, eq), NumberStyles.Integer, CultureInfo.InvariantCulture, out who)
                    || !long.TryParse(w[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Near) || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.From)
                    || !long.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.To)) continue;
                d[who] = x;
            }
            return d;
        }

        // ---------------------------------------------------------------- you round its base (where it is run, NpcActor.Ticks)
        private static readonly Dictionary<NpcActor, float> NextLook = new Dictionary<NpcActor, float>(); // session: when it looks next
        internal static void Tick(NpcActor a)
        {
            float at;
            if (NextLook.TryGetValue(a, out at) && Time.time < at) return;
            if (NextLook.Count > 256) NextLook.Clear();
            NextLook[a] = Time.time + LookEvery * UnityEngine.Random.Range(0.8f, 1.2f);
            if (!a.Owned || (!NpcResident.Is(a) && !NpcTalk.Hired(a))) return;
            Dictionary<long, Seen> d = null;
            long now = (long)NpcRescue.Now;
            bool changed = false;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || Utils.DistanceXZ(p.transform.position, a.transform.position) > Near) continue;
                if (d == null) { d = Read(a); if (d == null) return; } // (its locker not here yet)
                long me = p.GetPlayerID();
                Seen x;
                if (!d.TryGetValue(me, out x)) { d[me] = new Seen { Near = now }; changed = true; continue; }
                if (now - x.Near < Mark) continue;
                if (Away(x.Near, now)) { x.From = x.Near; x.To = now; } // (back after a while: that was your absence)
                x.Near = now;
                changed = true;
            }
            if (!changed) return;
            List<KeyValuePair<long, Seen>> l = new List<KeyValuePair<long, Seen>>(d);
            l.Sort((x, y) => y.Value.Near.CompareTo(x.Value.Near));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < l.Count && i < MostPlayers; i++) // (one long unseen goes first)
                sb.Append(l[i].Key.ToString(CultureInfo.InvariantCulture)).Append('=').Append(l[i].Value.Near.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(l[i].Value.From.ToString(CultureInfo.InvariantCulture)).Append(',').Append(l[i].Value.To.ToString(CultureInfo.InvariantCulture)).Append(';');
            NpcLocker.Set(a, KAway, sb.ToString());
        }

        internal static void Forget(NpcActor a) { NextLook.Remove(a); }

        // your last absence as it saw it (false: none yet). Back just now, not yet written down by its look: from the last you were round it till now
        private static bool Window(NpcActor a, long player, out long from, out long to)
        {
            from = to = 0L;
            Dictionary<long, Seen> d = Read(a);
            Seen x;
            if (d == null || !d.TryGetValue(player, out x)) return false;
            long now = (long)NpcRescue.Now;
            if (Away(x.Near, now)) { from = x.Near; to = now; return true; }
            from = x.From; to = x.To;
            return to > 0L;
        }

        // ---------------------------------------------------------------- what it would tell (the talker's game: its locker read)
        private static List<NpcMemory.Entry> News(NpcActor a, Player p)
        {
            List<NpcMemory.Entry> got = new List<NpcMemory.Entry>();
            long me = p.GetPlayerID(), from, to;
            if (!Window(a, me, out from, out to)) return got;
            long told = NpcTalk.AwayToldAt(a, p);
            bool trusted = NpcAttitude.Of(a, me) >= NpcVoice.Trusted && !NpcPatience.Angry(a); // (its own, as in its worries: NpcVoice.Concerns)
            foreach (NpcMemory.Entry e in NpcMemory.About(a, me))
            {
                if (e.At <= from || e.At > to || e.At <= told || Skipped(e.Kind)) continue;
                string theme = Theme(a, e.Kind);
                if (theme == null) continue;
                NpcVoice.Group g = NpcVoice.GroupOf("mem_" + e.Kind);
                if ((g != null && g.Personal && !trusted) || NpcTalk.ToldLately(a, p, "mem_" + e.Kind)) continue;
                got.Add(e);
            }
            got.Sort((x, y) => { int w = Weight(y.Kind).CompareTo(Weight(x.Kind)); return w != 0 ? w : y.At.CompareTo(x.At); });
            if (got.Count > Most) got.RemoveRange(Most, got.Count - Most);
            return got;
        }

        private static float Weight(string kind) { NpcMemory.Kind k = NpcMemory.KindOf(kind); return k != null ? k.Weight : 5f; }
        private static string Theme(NpcActor a, string kind) { return NpcPhrases.Has(a, "away_" + kind) ? "away_" + kind : NpcPhrases.Has(a, "mem_" + kind) ? "mem_" + kind : null; }

        internal static bool Has(NpcActor a, Player p)
        {
            if (a == null || p == null) return false;
            foreach (Source s in Sources) if (s.Line != null && !string.IsNullOrEmpty(s.Line(a, p))) return true;
            return News(a, p).Count > 0;
        }

        // its answer: a few words to begin, the story's own, then its memories, each in its own words
        internal static string Text(NpcActor a, Player p)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Source s in Sources) { string line = s.Line != null ? s.Line(a, p) : null; if (!string.IsNullOrEmpty(line)) sb.Append(line).Append(' '); }
            foreach (NpcMemory.Entry e in News(a, p)) { string line = NpcPhrases.Compose(a, Theme(a, e.Kind), p); if (!string.IsNullOrEmpty(line)) sb.Append(line).Append(' '); }
            if (sb.Length == 0) return a.Form(Lang.T("Та нічого особливого."));
            string lead = NpcPhrases.Compose(a, "away_intro", p);
            return (string.IsNullOrEmpty(lead) ? "" : lead + " ") + sb.ToString().Trim();
        }

        // told: the story's own known, its memories told up to now (your character), their worries weighed less a while
        private static void Told(NpcActor a, Player p)
        {
            foreach (NpcMemory.Entry e in News(a, p)) NpcTalk.MarkTold(p, a, "mem_" + e.Kind);
            foreach (Source s in Sources) if (s.Told != null) s.Told(a, p);
            NpcTalk.AwayTold(a, p);
        }

        // ---------------------------------------------------------------- wired once (NpcModules)
        internal static void Wire()
        {
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
            NpcTalk.Words["{away}"] = Text;
            NpcTalk.Deeds["awaytold"] = (a, p, w) => Told(a, p);
            Register();
        }

        private static bool _registered;
        internal static void Register()
        {
            if (_registered) return;
            _registered = true;
            StoryCond.Register("awaynews", new StoryCond.Kind { Usage = "awaynews   (йому є що розповісти про час, поки тебе не було)", Test = (p, w) => Has(NpcTalk.Him, p) });
        }
    }
}
#endif
