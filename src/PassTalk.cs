#if RAVEN_ITEM
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Ravens thrown a word in passing: two players with ravens come within a few metres of each other while one of them
    // runs, and now and then a raven of one side tosses the other's raven a line (and sometimes gets one back), over
    // their heads and in the chat. Nothing new is looked for: CrossTalk's look round (once a second, the players with
    // ravens near) hands each of them here (Seen), and only the moment of coming close is weighed.
    //   - a greeting owed (you meet after a while) goes first: that is the meeting talk's, not a word in passing;
    //   - the same pair at most once in PairMinutes, and not that soon after any talk of theirs (a shared doing);
    //   - the lower player id decides and puts it together, and sends the finished words to everybody.
    // What about: who runs (they rush by / you rush by / towards each other / a race the same way), a weapon in their
    // hand, the night, old pals or strangers. The answer follows the thread (^theme).
    internal static class PassTalk
    {
        internal static bool Enabled = true;
        internal static float Chance = 0.3f;
        internal static float PairMinutes = 10f;
        private const float Range = 8f, Fast = 3.5f, AnyPause = 60f, BackChance = 0.3f;
        private const string Rpc = "RavenWatch_Pass";

        private struct Seen1 { public Vector3 Pos; public float At; }
        private static readonly Dictionary<long, Seen1> Last = new Dictionary<long, Seen1>(); // where each was a moment ago
        private static readonly HashSet<long> Inside = new HashSet<long>();                 // within Range now: one pass, one chance
        private static readonly Dictionary<long, float> PairAt = new Dictionary<long, float>(); // the last word (or shared talk) with each
        private static float _lastAny = -999f;

        // from CrossTalk's look round, for a player with ravens within its range (d: the distance now)
        internal static void Seen(Player p, Player o, long id, float d, float now, bool owed)
        {
            if (!Enabled || !CrossTalk.Decides(p, o)) return;              // the lower id decides (a test dummy: you): the other game only shows it
            Seen1 was;
            bool had = Last.TryGetValue(id, out was) && now - was.At < 1.6f; // seen the second before: its pace is known
            Vector3 pos = o.transform.position;
            Last[id] = new Seen1 { Pos = pos, At = now };
            if (!had) Inside.Remove(id);                                     // gone a while (a portal, out of range): a new pass
            if (d > Range) { Inside.Remove(id); return; }
            if (!Inside.Add(id)) return;                                     // still the same pass
            if (owed || Senses.OnShip) return;                               // a greeting comes first; aboard, the ship's speed is no running
            if (now - _lastAny < AnyPause) return;
            float last;
            if (PairAt.TryGetValue(id, out last) && now - last < PairMinutes * 60f) return;
            Vector3 mine = p.GetVelocity(); mine.y = 0f;
            Vector3 theirs = had ? (pos - was.Pos) / Mathf.Max(0.5f, now - was.At) : Vector3.zero; theirs.y = 0f;
            bool iRun = mine.magnitude >= Fast, theyRun = theirs.magnitude >= Fast;
            if (!iRun && !theyRun) return;                                   // only in passing, not standing about
            if (UnityEngine.Random.value >= Chance) return;
            if (Senses.Ready && Senses.Fight) return;
            Throw(p, o, id, iRun, theyRun, iRun && theyRun && Vector3.Dot(mine.normalized, theirs.normalized) > 0.5f);
        }

        // a talk of theirs just now (a meeting, a boss, the fire): no word in passing soon after
        internal static void Shared(long id) { PairAt[id] = Time.time; }

        private static void Throw(Player p, Player o, long id, bool iRun, bool theyRun, bool sameWay)
        {
            if (Plugin.Instance == null || Cozy.Quieted) return;
            List<CrossTalk.Who> mine = Ravens(p), theirs = Ravens(o);
            if (mine.Count == 0 || theirs.Count == 0) return;
            bool iStart = UnityEngine.Random.value < 0.5f;
            CrossTalk.Who a = Pick(iStart ? mine : theirs), b = Pick(iStart ? theirs : mine);
            // the moment as the one who speaks sees it: whose player runs
            bool aRuns = iStart ? iRun : theyRun, bRuns = iStart ? theyRun : iRun;
            string theme = Theme(p, o, a, b, aRuns, bRuns, sameWay);
            List<CrossTalk.Line> lines = new List<CrossTalk.Line>();
            List<CrossTalk.Who> whos = new List<CrossTalk.Who> { a, b };
            string first = CrossTalk.Say(a, b, "x_pass_" + theme, null);
            if (string.IsNullOrEmpty(first)) return;
            lines.Add(new CrossTalk.Line { Who = 0, To = 1, Text = first });
            if (UnityEngine.Random.value < BackChance)
            {
                string back = CrossTalk.Say(b, a, "x_pass_back", theme);
                if (!string.IsNullOrEmpty(back)) lines.Add(new CrossTalk.Line { Who = 1, To = 0, Text = back });
            }
            PairAt[id] = Time.time;
            _lastAny = Time.time;
            Send(whos, lines);
            Show(whos, lines);
        }

        // What the word is about. The plainest is who runs; something more telling about the one spoken to (b) comes
        // first when there is one, each by its own chance: hurt, a famous nickname, alone, a weapon in hand, wet, a raven
        // of the same kind, the night, old pals or strangers.
        private static string Theme(Player p, Player o, CrossTalk.Who a, CrossTalk.Who b, bool aRuns, bool bRuns, bool sameWay)
        {
            Player them = b.P;
            if (them.GetHealthPercentage() < 0.35f && Roll(0.5f)) return "hurt";
            string nn, nc; Nicks.Kind nk;
            if (Nicks.Of(them, b.Munin, out nn, out nc, out nk) && nk >= Nicks.Kind.Deed && Roll(0.45f)) return "nick";
            if (Shoulder.ModelOf(them, !b.Munin) == null && Roll(0.35f)) return "alone"; // the other raven is not drawn on them: not worn, or away
            Skills.SkillType skill;
            if (Roll(0.3f) && AboutTalk.Held(them, out skill) != null && skill != Skills.SkillType.Pickaxes) return "weapon";
            if (Roll(0.25f)) { bool rain, snow, storm, fog; RavenTalk.Sky(Senses.Underground, Senses.Indoors, Senses.Biome, out rain, out snow, out storm, out fog); if (rain || storm) return "wet"; }
            if (a.Munin == b.Munin && Roll(0.25f)) return "kin";
            if (EnvMan.IsNight() && Roll(0.2f)) return "night";
            if (Roll(0.3f))
            {
                int talks = CrossTalk.TalksWith(p, o);
                if (talks >= CrossTalk.PalsAt) return "pals";
                if (talks == 0) return "stranger";
            }
            if (aRuns && bRuns) return sameWay ? "race" : "toward";
            return bRuns ? "rush" : "lead";
        }

        private static bool Roll(float chance) { return UnityEngine.Random.value < chance; }

        private static List<CrossTalk.Who> Ravens(Player pl)
        {
            List<CrossTalk.Who> list = new List<CrossTalk.Who>(2);
            for (int i = 0; i < 2; i++)
            {
                bool munin = i == 1;
                if (CrossTalk.CanTalk(pl, munin)) list.Add(new CrossTalk.Who { P = pl, Munin = munin, Level = Mathf.Max(1, Shoulder.LevelOn(pl, munin)) });
            }
            return list;
        }

        private static CrossTalk.Who Pick(List<CrossTalk.Who> list) { return list[UnityEngine.Random.Range(0, list.Count)]; }

        // the console (raven pass [theme]): a word now, no dice, no pause; nobody near: your own two play both sides
        internal static string Force(Player p, string theme)
        {
            if (p == null || Plugin.Instance == null) return Lang.T("Нема гравця.");
            string[] themes = { "rush", "lead", "toward", "race", "weapon", "night", "pals", "stranger", "nick", "kin", "alone", "hurt", "wet" };
            if (string.IsNullOrEmpty(theme) || Array.IndexOf(themes, theme) < 0) theme = themes[UnityEngine.Random.Range(0, themes.Length)];
            Player o = null; float best = 30f;
            foreach (Player x in Player.GetAllPlayers())
                if (x != null && x != p && Ravens(x).Count > 0) { float d = Vector3.Distance(x.transform.position, p.transform.position); if (d < best) { best = d; o = x; } }
            List<CrossTalk.Who> mine = Ravens(p), theirs = o != null ? Ravens(o) : new List<CrossTalk.Who>();
            CrossTalk.Who a, b;
            if (theirs.Count > 0 && mine.Count > 0) { a = Pick(mine); b = Pick(theirs); }
            else if (mine.Count == 2) { a = mine[0]; b = mine[1]; }
            else return Lang.T("Потрібен гравець із воронами поруч або обидва твої ворони на плечах.");
            List<CrossTalk.Who> whos = new List<CrossTalk.Who> { a, b };
            List<CrossTalk.Line> lines = new List<CrossTalk.Line>();
            string first = CrossTalk.Say(a, b, "x_pass_" + theme, null);
            if (string.IsNullOrEmpty(first)) return Lang.F("Нема фраз для «{0}».", theme);
            lines.Add(new CrossTalk.Line { Who = 0, To = 1, Text = first });
            string back = CrossTalk.Say(b, a, "x_pass_back", theme);
            if (!string.IsNullOrEmpty(back)) lines.Add(new CrossTalk.Line { Who = 1, To = 0, Text = back });
            Send(whos, lines);
            Show(whos, lines);
            return Lang.F("Слово мимохідь: {0}. Теми: rush, lead, toward, race, weapon, night, pals, stranger, nick, kin, alone, hurt, wet.", theme);
        }

        // ---------------------------------------------------------------- everybody sees it

        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnPass)); }

        private static void Send(List<CrossTalk.Who> whos, List<CrossTalk.Line> lines)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(whos.Count);
            foreach (CrossTalk.Who w in whos) { pkg.Write(w.P.GetZDOID()); pkg.Write(w.Munin); }
            pkg.Write(lines.Count);
            foreach (CrossTalk.Line l in lines) { pkg.Write(l.Who); pkg.Write(l.To); pkg.Write(l.Text); }
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        private static void OnPass(long sender, ZPackage pkg)
        {
            if (Player.m_localPlayer == null || sender == ZDOMan.GetSessionID()) return;
            try
            {
                int n = Mathf.Min(pkg.ReadInt(), 2);
                List<CrossTalk.Who> whos = new List<CrossTalk.Who>(n);
                for (int i = 0; i < n; i++)
                {
                    ZDOID zid = pkg.ReadZDOID();
                    bool munin = pkg.ReadBool();
                    Player found = null;
                    foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetZDOID() == zid) { found = o; break; }
                    if (found == null || Shadow.Flagged(found)) return; // not around here, or unseen: nothing to show
                    whos.Add(new CrossTalk.Who { P = found, Munin = munin });
                }
                int m = Mathf.Min(pkg.ReadInt(), 2);
                List<CrossTalk.Line> lines = new List<CrossTalk.Line>(m);
                for (int i = 0; i < m; i++)
                {
                    CrossTalk.Line l = new CrossTalk.Line { Who = pkg.ReadInt(), To = pkg.ReadInt(), Text = CrossTalk.Clean(pkg.ReadString()) };
                    if (l.Who < 0 || l.Who >= whos.Count || l.To < 0 || l.To >= whos.Count || l.Text.Length == 0) return;
                    lines.Add(l);
                }
                Show(whos, lines);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("A word in passing could not be read: " + e.Message); }
        }

        // over the ravens (the answer a moment later) and in the chat for those near
        private static void Show(List<CrossTalk.Who> whos, List<CrossTalk.Line> lines)
        {
            Player me = Player.m_localPlayer;
            if (me == null || Plugin.Instance == null || lines.Count == 0) return;
            float d = Vector3.Distance(me.transform.position, whos[0].P.transform.position);
            if (d > CrossTalk.HearRange) return;
            float total = 0f;
            foreach (CrossTalk.Line l in lines) total += RavenSpeech.ReadSeconds(l.Text);
            Cozy.Quiet(Mathf.Min(total, 8f)); // the others hold their words a moment
            foreach (CrossTalk.Who w in whos) if (w.P == me) { Director.Spoke(Director.Voice.Event, Mathf.Min(total, 8f)); break; }
            Plugin.Instance.StartCoroutine(Play(whos, lines));
        }

        private static IEnumerator Play(List<CrossTalk.Who> whos, List<CrossTalk.Line> lines)
        {
            Dictionary<int, float> free = new Dictionary<int, float>(); // a raven's next line replaces its last: not before that one is read
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) yield return new WaitForSeconds(1.4f);
                CrossTalk.Line l = lines[i];
                float at;
                if (free.TryGetValue(l.Who, out at) && at > Time.time) yield return new WaitForSeconds(at - Time.time);
                free[l.Who] = Time.time + TalkBubble.Pace(l.Text);
                CrossTalk.Who w = whos[l.Who], to = whos[l.To];
                if (w.P == null || to.P == null) yield break;
                RavenSpeech.ShowShoulder(w.P, w.Munin, RavenSpeech.ReadSeconds(l.Text), l.Text);
                GameObject model = Shoulder.ModelOf(w.P, w.Munin);
                ShoulderLife.TalkOn(model != null ? model.GetComponentInChildren<Animator>() : null);
                CrossBubble.ChatLine(w, to, l.Text);
            }
        }
    }
}
#endif
