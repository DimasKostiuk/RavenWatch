#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Gossip: the ravens of different players speak of what really happened to their people, not only of made-up tales.
    // Each game puts a few fresh facts on its player (one short string, looked at every 30 s, written only when it
    // changes): the last death (today or yesterday: by whom or of what, how many times today), a boss beaten in the last
    // three days, how many days each raven has been with its owner, and how many foes the owner has killed.
    //   "d,day,c|b|x,who,n;b,day,prefab;h,days;m,days;k,kills"
    // A raven's line then takes them as placeholders — the speaker's own {g_my_…} and the other one's {g_their_…}:
    //   death_when · killer, killer_gen, killer_ins (a creature) · cause (anything else) · deaths («2 рази»: two or more)
    //   boss, boss_acc, boss_gen, boss_ins, boss_when · days («120 днів») · kills («3041 ворога»)
    // A line with a fact that is not there is simply not chosen (Composer's AllKnown), so gossip comes only when there is
    // something to tell; and only in some talks (CrossTalk rolls Chance once a talk).
    internal static class Gossip
    {
        internal const string NetKey = "RavenWatch_Gossip";
        internal static float Chance = 0.35f; // of the pals' talks, those where the facts may be spoken of
        internal static bool ForceNext;       // the console: the next talk speaks of them for sure

        // the console (raven gossip): what your ravens tell about you now, and the next pals' talk speaks of it
        internal static string Cmd(Player p)
        {
            if (p == null) return Lang.T("Нема гравця.");
            _next = 0f;
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            Publish(p, h, m);
            Dictionary<string, string> v = new Dictionary<string, string>();
            Fill(v, p, false, "", Plugin.Instance != null ? Plugin.Instance.PhraseDir : null);
            ForceNext = true;
            if (v.Count == 0) return Lang.T("Свіжих фактів поки нема (смерть за сьогодні-вчора, бос за три дні, від 3 днів разом, від 50 убитих).");
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string> kv in v) parts.Add(kv.Key + ": " + kv.Value);
            return Lang.F("Ворони знають про тебе: {0}. Наступна розмова приятелів — з плітками (raven cross pals).", string.Join(" · ", parts.ToArray()));
        }
        private static float _next;

        // the wearer's game, in the twice-a-second sync: the facts gathered every 30 s, written only when they change
        internal static void Publish(Player p, ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            if (Time.time < _next) return;
            _next = Time.time + 30f;
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            string s = Build(hugin, munin);
            if (z.GetString(NetKey, "") != s) z.Set(NetKey, s);
        }

        private static string Build(ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            int today = Memory.Day();
            Memory.Mem death = null, boss = null;
            int todayDeaths = 0;
            foreach (ItemDrop.ItemData item in new[] { hugin, munin })
            {
                if (item == null) continue;
                foreach (Memory.Mem m in Memory.Load(item))
                {
                    if (m.Kind == "death" && today - m.Day <= 1 && (death == null || m.Day > death.Day)) death = m;
                    else if (m.Kind == "today" && m.Day == today) todayDeaths = Mathf.Max(todayDeaths, m.N);
                    else if (m.Kind == "boss" && today - m.Day <= 2 && (boss == null || m.Day > boss.Day)) boss = m;
                }
            }
            StringBuilder sb = new StringBuilder(64);
            if (death != null) Part(sb, "d", death.Day.ToString(), Clean(death.Extra), Clean(death.Who), (death.Day == today ? Mathf.Max(1, todayDeaths) : 1).ToString());
            if (boss != null) Part(sb, "b", boss.Day.ToString(), Clean(boss.Who));
            if (hugin != null) Part(sb, "h", Nicks.Count(hugin, "days").ToString());
            if (munin != null) Part(sb, "m", Nicks.Count(munin, "days").ToString());
            PlayerProfile prof = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (prof != null) Part(sb, "k", Stats.Get(prof, PlayerStatType.EnemyKills).ToString());
            return sb.ToString();
        }

        private static void Part(StringBuilder sb, params string[] f)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(string.Join(",", f));
        }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(",", "").Replace(";", ""); }

        // the facts of this player (and this raven of theirs) as placeholders with this prefix (g_my_ / g_their_)
        internal static void Fill(Dictionary<string, string> v, Player pl, bool munin, string pre, string dir) { using (Lang.Phrase()) FillIn(v, pl, munin, pre, dir); } // (the line's values in its own language)
        private static void FillIn(Dictionary<string, string> v, Player pl, bool munin, string pre, string dir)
        {
            ZDO z = pl != null ? NetFlag.Zdo(pl, false) : null;
            string raw = z != null ? z.GetString(NetKey, "") : "";
            if (raw.Length == 0) return;
            int today = Memory.Day(), n, day;
            foreach (string part in raw.Split(';'))
            {
                string[] f = part.Split(',');
                if (f.Length < 2) continue;
                switch (f[0])
                {
                    case "d":
                        if (f.Length < 5 || !int.TryParse(f[1], out day) || today - day > 1 || today < day) break;
                        v[pre + "death_when"] = Memory.When(day);
                        if (f[2] == "x") { if (f[3].Length > 0) v[pre + "cause"] = Memory.Cause(f[3]); }
                        else if (!string.IsNullOrEmpty(dir)) Composer.CreatureForms(dir, v, pre + "killer", f[3]);
                        if (int.TryParse(f[4], out n) && n >= 2) v[pre + "deaths"] = Composer.Times(n);
                        break;
                    case "b":
                        if (f.Length < 3 || !int.TryParse(f[1], out day) || today - day > 2 || today < day) break;
                        int i = Array.IndexOf(Strike.Bosses, f[2]);
                        if (i < 0) break;
                        v[pre + "boss"] = Strike.BossNames[i]; v[pre + "boss_acc"] = Memory.BossAcc[i];
                        v[pre + "boss_gen"] = Memory.BossGen[i]; v[pre + "boss_ins"] = Memory.BossIns[i];
                        v[pre + "boss_when"] = Memory.When(day);
                        break;
                    case "h": case "m":
                        if ((f[0] == "m") != munin || !int.TryParse(f[1], out n) || n < 3) break;
                        v[pre + "days"] = Lang.N(n, "день|дні|днів");
                        break;
                    case "k":
                        if (!int.TryParse(f[1], out n) || n < 50) break;
                        v[pre + "kills"] = Lang.N(n, "ворога|ворогів|ворогів");
                        break;
                }
            }
        }
    }
}
#endif
