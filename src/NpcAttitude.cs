#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // How a person feels about each player (npc-plan.md, «ставлення»): −100…100 per player, kept in its locker. Words only
    // (the user): its greeting, «Що думаєш про мене?», «Як справи?», «Поговорімо» — from cold to dear. What moves it: what
    // it remembers you did (NpcMemory.Kinds Toward: you saved it, brought it round, took it in, brought what it asked),
    // your blows, a little each day it sees you; left alone it drifts back to even. Hunger and pay do not touch it (the
    // user: those weigh on its mood, not on you).
    //   levels   hostile ≤ −50 · cold < −15 · even · warm ≥ 25 · close ≥ 60
    //   its words   a theme's own for the level is tried first: see_player_cold, about_you_close, how_hostile… (Variant)
    //   the story   ?npcattitude >= 50 [npc]  ·  !attitude +10  ·  NpcAttitude.Add / Of / Level
    // Written only where the person is owned (Add goes there); read anywhere its locker is.
    internal static class NpcAttitude
    {
        internal static readonly int KAttitude = "RavenWatch_npcAttitude".GetStableHashCode(); // string "player=value@day;…" — in its locker

        private const int Most = 8;                     // players kept (the one nearest even goes first)
        private const float Seen = 2f, SeenUpTo = 30f;  // a day it sees you: a little warmer, up to this
        private const float Blow = 5f;                  // your blow
        private const int DriftAfter = 5;               // days unseen before it drifts back (a point a day)

        private static object _touched;
        private sealed class Feel { public long Player; public float Value; public long Day; }

        // ---------------------------------------------------------------- reading (any game)
        internal static float Of(NpcActor a, long player)
        {
            List<Feel> l = Read(a);
            if (l == null) return 0f;
            Feel f = l.Find(x => x.Player == player);
            return f != null ? Drifted(f) : 0f;
        }

        internal static string Level(float v) { return v <= -50f ? "hostile" : v < -15f ? "cold" : v >= 60f ? "close" : v >= 25f ? "warm" : "even"; }
        internal static string Level(NpcActor a, Player p) { return p != null ? Level(Of(a, p.GetPlayerID())) : "even"; }

        // its card: how it is with you
        internal static string Word(NpcActor a, Player p)
        {
            switch (Level(a, p))
            {
                case "hostile": return Lang.T("до тебе: ворожо");
                case "cold": return Lang.T("до тебе: холодно");
                case "warm": return Lang.T("до тебе: тепло");
                case "close": return Lang.T("до тебе: як до рідних");
                default: return Lang.T("до тебе: рівно");
            }
        }

        // ---------------------------------------------------------------- writing (where the person is owned)
        // from any game: sent to the one that runs it (NpcActor.Acts «attitude»)
        internal static void Tell(NpcActor a, long player, float by)
        {
            if (a == null || player == 0L || by == 0f) return;
            a.Act("attitude " + player.ToString(CultureInfo.InvariantCulture) + " " + by.ToString("0.#", CultureInfo.InvariantCulture));
        }

        internal static void Add(NpcActor a, long player, float by, float upTo = 100f)
        {
            if (a == null || !a.Owned || player == 0L) return;
            List<Feel> l = Read(a);
            if (l == null) return;
            long day = Today();
            Feel f = l.Find(x => x.Player == player);
            if (f == null) { f = new Feel { Player = player }; l.Add(f); }
            float was = Drifted(f);
            f.Value = by > 0f ? Mathf.Max(was, Mathf.Min(was + by, upTo)) : Mathf.Max(-100f, was + by); // (a cap stops a rise only: never pulls it down)
            f.Value = Mathf.Clamp(f.Value, -100f, 100f);
            f.Day = day;
            _touched = f;
            Write(a, l);
            _touched = null;
            if (Level(was) != Level(f.Value))
            {
                Npcs.Log(a.Id + ": feels " + Level(f.Value) + " to " + player + " (" + Mathf.RoundToInt(f.Value) + ")");
                StoryBus.Raise("npc_attitude_" + Level(f.Value), a.Id, Mathf.RoundToInt(f.Value), a.transform.position);
            }
        }

        // «attitude <player> <by>» (NpcActor.Acts)
        internal static void Act(NpcActor a, string[] w)
        {
            long player; float by;
            if (w.Length < 3 || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out player) || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out by)) return;
            Add(a, player, by);
        }

        // a blow from a player (NpcActor.HitBy, where it is owned): not more than once in a few seconds
        private static readonly Dictionary<NpcActor, float> NextBlow = new Dictionary<NpcActor, float>();
        internal static void Hit(NpcActor a, Character by)
        {
            Player p = by as Player;
            float at;
            if (p == null || (NextBlow.TryGetValue(a, out at) && Time.time < at)) return;
            if (NextBlow.Count > 128) NextBlow.Clear();
            NextBlow[a] = Time.time + 5f;
            Add(a, p.GetPlayerID(), -Blow);
        }

        // it sees you (NpcVoice, where it is owned): once a game day a little warmer
        internal static void SawYou(NpcActor a, Player p)
        {
            if (a == null || p == null || !a.Owned) return;
            List<Feel> l = Read(a);
            if (l == null) return;
            Feel f = l.Find(x => x.Player == p.GetPlayerID());
            if (f != null && f.Day == Today()) return;
            Add(a, p.GetPlayerID(), Seen, SeenUpTo);
        }

        // ---------------------------------------------------------------- its words (NpcPhrases.Variants)
        // the theme for how it feels about you: tried before the plain one (none fits: the plain one)
        internal static string Variant(NpcActor a, Player to, string theme)
        {
            if (to == null) return null;
            string level = Level(a, to);
            if (level == "even") return null;
            float chance = level == "hostile" ? 0.9f : level == "cold" ? 0.75f : level == "close" ? 0.7f : 0.5f;
            if (UnityEngine.Random.value > chance) return null;
            if (theme.StartsWith("how_", StringComparison.Ordinal)) return level == "hostile" || level == "cold" ? "how_" + level : null; // (warm or dear: its plain how is warm enough)
            switch (theme)
            {
                case "see_player": case "back": case "about_you": case "talk_invite": return theme + "_" + level;
                default: return null;
            }
        }

        // ---------------------------------------------------------------- the story's condition
        internal static void Register()
        {
            StoryCond.Register("npcattitude", new StoryCond.Kind { Usage = "npcattitude [>=|<|…] N [npc]   (як він до тебе: −100…100; тепло ≥ 25, рідні ≥ 60, холодно < −15, ворожо ≤ −50)", Min = 1,
                Check = w => StoryCond.CmpCheck(w, 1),
                Test = (p, w) =>
                {
                    string last = w[w.Length - 1];
                    int ignored;
                    NpcActor n = w.Length > 2 && !int.TryParse(last, out ignored) && !last.StartsWith(">") && !last.StartsWith("<") && !last.StartsWith("=") ? NpcLevers.Who(w, w.Length - 1) : NpcTalk.Him; // (a person named: that one — not here, no one)
                    return n != null && p != null && StoryCond.Compare(Mathf.RoundToInt(Of(n, p.GetPlayerID())), w, 1);
                } });
        }

        // the console
        internal static string Describe(NpcActor a)
        {
            List<Feel> l = Read(a);
            if (l == null) return Lang.T("Шафка ще не тут.");
            if (l.Count == 0) return a.Name + ": " + Lang.T("до всіх рівно.");
            List<string> s = new List<string>();
            foreach (Feel f in l) s.Add(f.Player + " " + Mathf.RoundToInt(Drifted(f)) + " (" + Level(Drifted(f)) + ")");
            return a.Name + ": " + string.Join(", ", s.ToArray());
        }

        // ---------------------------------------------------------------- kept
        private static long Today() { float day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f; return (long)(NpcRescue.Now / day); }

        // unseen long: a point a day back towards even
        private static float Drifted(Feel f)
        {
            long off = Today() - f.Day - DriftAfter;
            if (off <= 0) return f.Value;
            return f.Value > 0f ? Mathf.Max(0f, f.Value - off) : Mathf.Min(0f, f.Value + off);
        }

        private static List<Feel> Read(NpcActor a)
        {
            string s = NpcLocker.Get(a, KAttitude);
            if (s == null) return null;
            List<Feel> l = new List<Feel>();
            foreach (string e in s.Split(';'))
            {
                int eq = e.IndexOf('='), at = e.IndexOf('@');
                if (eq < 0 || at < eq) continue;
                Feel f = new Feel();
                if (!long.TryParse(e.Substring(0, eq), NumberStyles.Integer, CultureInfo.InvariantCulture, out f.Player)) continue;
                float.TryParse(e.Substring(eq + 1, at - eq - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out f.Value);
                long.TryParse(e.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out f.Day);
                l.Add(f);
            }
            return l;
        }

        private static void Write(NpcActor a, List<Feel> l)
        {
            while (l.Count > Most) { Feel calm = null; foreach (Feel f in l) if (f != _touched && (calm == null || Mathf.Abs(Drifted(f)) < Mathf.Abs(Drifted(calm)))) calm = f; if (calm == null) break; l.Remove(calm); } // (a new face is not let go at once)
            List<string> parts = new List<string>();
            foreach (Feel f in l) parts.Add(f.Player.ToString(CultureInfo.InvariantCulture) + "=" + Mathf.RoundToInt(f.Value).ToString(CultureInfo.InvariantCulture) + "@" + f.Day.ToString(CultureInfo.InvariantCulture));
            NpcLocker.Set(a, KAttitude, string.Join(";", parts.ToArray()));
        }
    }
}
#endif
