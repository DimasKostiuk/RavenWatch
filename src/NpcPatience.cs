#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // How much a person is ready to talk to you now (the user 2026-11-05, docs/plans/talk-plan.md): its patience, 0–100, a bar by its
    // talk window with its reasons under the pointer. Not a state of its own to keep — reckoned when asked, of three parts:
    //   its state      shared, the same for every player: its mood (NpcToil: its bed, fire, food, rain, pay, wish), its tiredness, what it
    //                  feels now (NpcFeel), what it is busy with (work, a meal, a raid round its base), its ways (chatty, grumpy)
    //   its irritation shared too, and who caused it: one who angered it bears all of it, everyone else half (KIrk, in its locker)
    //   yours          its feeling to you (NpcAttitude) and what you spent of it in your talks (KSpent, in its locker, a player each)
    // Irritation and what you spent wear off with the world's time (a game hour each: IrkFade — twice as fast for a cheerful one, half for a
    // grumpy one — and SpentFade), so a reload or a night away keeps them as they were (in one's own world the clock stops with the game).
    //   levels   «Відчепись» < Out ≤ «Сухо» < Dry ≤ «Звично» < Easy ≤ «Охоче» < Warm ≤ «Від душі»
    //   Gate     irritation from which its own talk, its favours and the rest of its story are shut for everyone, whoever angered it
    // What moves it: NpcTalk's repeats and brush-offs, a talk's answers (!patience −N, !irritate N), its favours (NpcFavours). It never shuts
    // what a person must do (hire, let go, its request, its rescue, the story): those stay, only their tone changes.
    //   the story   ?patience >= 75 [npc] · ?angry · !patience ±N · !irritate N · NpcPatience.Of / Angry / Spend / Irritate
    //   console     raven npc patience <who> [irk N | spend N | clear]
    // Written only where the person is run (its Acts «patience», «irk»); read on any game from its locker.
    internal static class NpcPatience
    {
        internal static readonly int KIrk = "RavenWatch_npcIrk".GetStableHashCode();     // string "value|worldsec|player" — in its locker
        internal static readonly int KSpent = "RavenWatch_npcSpent".GetStableHashCode(); // string "player=value@worldsec;…" — in its locker
        internal static float Out = 20f, Dry = 45f, Easy = 75f, Warm = 90f, Gate = 20f;
        internal static float IrkFade = 10f, SpentFade = 8f, Heard = -15f; // points a game hour · what listening may give back at most (spent below zero)
        private const int MostSpent = 8;

        internal sealed class Part { public string Label; public float Value; }

        private static float Hour { get { return (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f) / 24f; } }

        // ---------------------------------------------------------------- reading (any game)
        // its irritation now and the player who caused it (0: nobody known)
        internal static float Irk(NpcActor a, out long by)
        {
            by = 0L;
            string s = NpcLocker.Get(a, KIrk);
            if (string.IsNullOrEmpty(s)) return 0f;
            string[] w = s.Split('|');
            float v; double at;
            if (w.Length < 3 || !float.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v) || !double.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out at)) return 0f;
            long.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out by);
            return Mathf.Max(0f, v - IrkRate(a) * (float)((NpcRescue.Now - at) / Hour));
        }

        internal static bool Angry(NpcActor a) { long by; return Irk(a, out by) >= Gate; }

        private static float IrkRate(NpcActor a)
        {
            HashSet<string> t = NpcPhrases.Tones(a);
            return IrkFade * (t.Contains("весел") ? 2f : 1f) * (t.Contains("бурч") ? 0.5f : 1f);
        }

        // what you spent of its patience, worn off by now (below zero: you listened to it)
        internal static float Spent(NpcActor a, long player)
        {
            foreach (KeyValuePair<long, float> e in SpentAll(a)) if (e.Key == player) return e.Value;
            return 0f;
        }

        private static List<KeyValuePair<long, float>> SpentAll(NpcActor a)
        {
            List<KeyValuePair<long, float>> l = new List<KeyValuePair<long, float>>();
            string s = NpcLocker.Get(a, KSpent);
            if (string.IsNullOrEmpty(s)) return l;
            foreach (string e in s.Split(';'))
            {
                int eq = e.IndexOf('='), at = e.IndexOf('@');
                long who; float v; double when;
                if (eq <= 0 || at < eq || !long.TryParse(e.Substring(0, eq), NumberStyles.Integer, CultureInfo.InvariantCulture, out who)
                    || !float.TryParse(e.Substring(eq + 1, at - eq - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                    || !double.TryParse(e.Substring(at + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out when)) continue;
                float worn = SpentFade * (float)((NpcRescue.Now - when) / Hour);
                v = v > 0f ? Mathf.Max(0f, v - worn) : Mathf.Min(0f, v + worn);
                if (Mathf.Abs(v) >= 0.5f) l.Add(new KeyValuePair<long, float>(who, v));
            }
            return l;
        }

        internal static float Of(NpcActor a, Player p) { return Of(a, p, null); }

        // its patience to this player now; parts: its reasons, each with its share (the bar's tip)
        internal static float Of(NpcActor a, Player p, List<Part> parts)
        {
            if (a == null || p == null) return 50f;
            float v = 50f;
            Action<string, float> add = (label, d) => { if (Mathf.Abs(d) < 0.5f) return; v += d; if (parts != null) parts.Add(new Part { Label = a.Form(label), Value = d }); };
            if (NpcResident.Is(a))
            {
                float mood = NpcToil.Mood(a);
                string why = mood < 45f ? NpcToil.WhyWord(a) : "";
                add(why.Length > 0 ? Lang.F("Настрій ({0})", why) : Lang.T("Настрій"), (mood - 60f) * 0.4f);
            }
            float tired = NpcToil.Tired(a);
            if (tired >= 60f) add(Lang.T("Втомлений|Втомлена"), -(tired - 50f) * 0.35f);
            string feel; float strength;
            if (NpcFeel.Of(a, out feel, out strength))
            {
                if (feel == NpcFeel.Fright) add(Lang.T("Наляканий|Налякана"), -15f * strength);
                else if (feel == NpcFeel.Joy) add(Lang.T("Радіє"), 10f * strength);
                else if (feel == NpcFeel.Grief) add(Lang.T("Сумує"), -12f * strength);
                else if (feel == NpcFeel.Awe) add(Lang.T("Вражений|Вражена"), 4f * strength);
            }
            NpcPersona pe = NpcPersona.Of(a);
            if (a.Mind != null && a.Mind.NowLayer == NpcMind.Layer.Job) add(Lang.T("Ти відриваєш від роботи"), -8f * (0.5f + pe.Diligence));
            else if (a.Mind != null && a.Mind.Now == NpcMind.Task.Eat) add(Lang.T("Ти заважаєш їсти"), -6f);
            if (NpcNews.RaidNear(a)) add(Lang.T("Набіг поруч"), -10f);
            add(pe.Chatty >= 0.5f ? Lang.T("Балакучий|Балакуча") : Lang.T("Небалакучий|Небалакуча"), (pe.Chatty - 0.5f) * 20f);
            if (NpcPhrases.Tones(a).Contains("бурч")) add(Lang.T("Бурчун|Бурчунка"), -6f);
            long me = p.GetPlayerID();
            float att = NpcAttitude.Of(a, me);
            add(att >= 0f ? Lang.T("Ставлення до тебе") : Lang.T("Тобі не довіряє"), att * 0.35f);
            long by;
            float irk = Irk(a, out by);
            if (irk >= 0.5f) add(by == me ? Lang.T("Злий|Зла загалом (через тебе)") : by != 0L ? Lang.F("Злий|Зла загалом (через {0})", Who(by)) : Lang.T("Злий|Зла загалом"), by == me || by == 0L ? -irk : -irk * 0.5f);
            float spent = Spent(a, me);
            if (spent > 0f) add(Lang.T("Втомився|Втомилася від розмови з тобою"), -spent);
            else if (spent < 0f) add(Lang.T("Ти слухаєш його|її"), -spent);
            return Mathf.Clamp(v, 0f, 100f);
        }

        // a player's name by its id (one on this game; else «інший гравець»)
        private static string Who(long id)
        {
            foreach (Player p in Player.GetAllPlayers()) if (p != null && p.GetPlayerID() == id) return p.GetPlayerName();
            return Lang.T("іншого гравця");
        }

        internal static string Level(float v)
        {
            return v < Out ? Lang.T("Відчепись") : v < Dry ? Lang.T("Сухо") : v < Easy ? Lang.T("Звично") : v < Warm ? Lang.T("Охоче") : Lang.T("Від душі");
        }

        // the bar's tip: its level, what it is, its reasons, what is shut now and why
        internal static string Tip(NpcActor a, Player p) { float v; return Tip(a, p, out v); }

        // …and its patience with it (one reckoning for the window: its bar and its box)
        internal static string Tip(NpcActor a, Player p, out float v)
        {
            List<Part> parts = new List<Part>();
            v = Of(a, p, parts);
            StringBuilder sb = new StringBuilder();
            sb.Append("<b>").Append(Lang.F("Терпіння до тебе: {0} — «{1}»", Mathf.RoundToInt(v), Level(v))).Append("</b>\n");
            sb.Append("<i>").Append(Lang.T("Скільки він|вона готовий|готова з тобою говорити.")).Append("</i>\n");
            foreach (Part x in parts) sb.Append(x.Value > 0f ? "  + " : "  − ").Append(x.Label).Append("  ").Append(Mathf.RoundToInt(Mathf.Abs(x.Value))).Append('\n');
            if (Angry(a)) sb.Append(Lang.T("▲ Особисте й прохання закриті: він|вона надто злий|зла.")).Append('\n');
            else if (!NpcResident.Is(a)) { } // (favours: only of one living at a base — its talk's «Попросити про щось…»)
            else if (v < Easy) sb.Append(Lang.F("Від {0} охоче розповідає й може погодитись на прохання.", Mathf.RoundToInt(Easy))).Append('\n');
            else sb.Append(Lang.T("★ Можна про щось попросити.")).Append('\n');
            return a.Form(sb.ToString().TrimEnd('\n'));
        }

        // ---------------------------------------------------------------- writing (sent to the game that runs it)
        // what you spent of its patience (below zero: given back — it was listened to)
        internal static void Spend(NpcActor a, Player p, float by)
        {
            if (a == null || p == null || by == 0f) return;
            a.Act("patience " + p.GetPlayerID().ToString(CultureInfo.InvariantCulture) + " " + by.ToString("0.#", CultureInfo.InvariantCulture));
        }

        // you angered it (shared: everyone sees it angry, you most)
        internal static void Irritate(NpcActor a, Player p, float by)
        {
            if (a == null || p == null || by <= 0f) return;
            a.Act("irk " + p.GetPlayerID().ToString(CultureInfo.InvariantCulture) + " " + by.ToString("0.#", CultureInfo.InvariantCulture));
        }

        // «patience <player> <by>» where it is run
        private static void SpendAct(NpcActor a, string[] w)
        {
            long who; float by;
            if (w.Length < 3 || !a.Owned || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out who) || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out by) || !NpcActor.SentBy(who)) return;
            if (NpcLocker.Get(a, KSpent) == null) return; // (its locker not here yet)
            List<KeyValuePair<long, float>> l = SpentAll(a);
            float was = 0f;
            l.RemoveAll(e => { if (e.Key == who) { was = e.Value; return true; } return false; });
            l.Add(new KeyValuePair<long, float>(who, Mathf.Clamp(was + by, Heard, 100f)));
            while (l.Count > MostSpent) l.RemoveAt(0); // (the oldest written first: one long unseen goes)
            StringBuilder sb = new StringBuilder();
            string now = NpcRescue.Now.ToString("0", CultureInfo.InvariantCulture);
            foreach (KeyValuePair<long, float> e in l) sb.Append(e.Key.ToString(CultureInfo.InvariantCulture)).Append('=').Append(e.Value.ToString("0.#", CultureInfo.InvariantCulture)).Append('@').Append(now).Append(';');
            NpcLocker.Set(a, KSpent, sb.ToString());
        }

        // «irk <player> <by>» where it is run: the one who angered it last is the one named
        private static void IrkAct(NpcActor a, string[] w)
        {
            long who; float by;
            if (w.Length < 3 || !a.Owned || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out who) || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out by) || !NpcActor.SentBy(who)) return;
            if (NpcLocker.Get(a, KIrk) == null) return;
            long was;
            float v = Mathf.Clamp(Irk(a, out was) + by, 0f, 80f);
            NpcLocker.Set(a, KIrk, v.ToString("0.#", CultureInfo.InvariantCulture) + "|" + NpcRescue.Now.ToString("0", CultureInfo.InvariantCulture) + "|" + (by > 0f ? who : was).ToString(CultureInfo.InvariantCulture));
            Npcs.Log(a.Id + ": irritated " + v.ToString("0", CultureInfo.InvariantCulture) + " (by " + who + ")");
        }

        // ---------------------------------------------------------------- its words
        // angry, and you near: now and then it grumbles to itself (NpcVoice: the complaints' group)
        private static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            long by;
            float irk = Irk(a, out by);
            if (irk >= 10f) themes.Add(new KeyValuePair<string, float>("irritated", Mathf.Min(60f, 45f * irk / 40f)));
        }

        // its greeting while it is angry with someone else: that it is not you (NpcPhrases.Variants)
        private static string Variant(NpcActor a, Player to, string theme)
        {
            if (to == null || (theme != "see_player" && theme != "talk_again")) return null;
            long by;
            if (Irk(a, out by) < Gate || by == 0L || by == to.GetPlayerID()) return null;
            string close = NpcAttitude.Level(a, to) == "close" ? "irritated_greet_close" : null;
            return close != null && NpcPhrases.Has(a, close) ? close : "irritated_greet";
        }

        // ---------------------------------------------------------------- wired once (NpcModules)
        internal static void Wire()
        {
            NpcActor.Acts["patience"] = SpendAct;
            NpcActor.Acts["irk"] = IrkAct;
            NpcVoice.Sources.Add(Themes);
            NpcPhrases.Variants.Add(Variant);
            NpcPhrases.ValueSources.Add((a, to, v) => { long by; if (Irk(a, out by) >= 0.5f && by != 0L) v["irked_by"] = Who(by); }); // ({irked_by}: whom it is angry with)
            NpcTalk.Deeds["patience"] = (a, p, w) => { float by; if (w.Length > 1 && float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out by)) Spend(a, p, -by); }; // (!patience −10: it is ten less)
            NpcTalk.Deeds["irritate"] = (a, p, w) => { float by; if (w.Length > 1 && float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out by)) Irritate(a, p, by); };
            Register();
            Npcs.Commands["patience"] = Command;
        }

        // its conditions, known before any talk is read (NpcTalk.Register)
        private static bool _registered;
        internal static void Register()
        {
            if (_registered) return;
            _registered = true;
            StoryCond.Register("patience", new StoryCond.Kind { Usage = "patience [>=|<|…] N   (його терпіння до тебе: 0…100; охоче ≥ 75)", Min = 1,
                Check = w => StoryCond.CmpCheck(w, 1),
                Test = (p, w) => NpcTalk.Him != null && p != null && StoryCond.Compare(Mathf.RoundToInt(Of(NpcTalk.Him, p)), w, 1) });
            StoryCond.Register("angry", new StoryCond.Kind { Usage = "angry   (злий загалом: особисте й прохання закриті для всіх)", Test = (p, w) => NpcTalk.Him != null && Angry(NpcTalk.Him) });
        }

        // raven npc patience <who> [irk N | spend N | clear]: its patience to you and why (the log too)
        private static string Command(Player p, NpcActor who, string[] a, Vector3 aim)
        {
            {
                float n;
                if (a.Length > 4 && float.TryParse(a[4], NumberStyles.Float, CultureInfo.InvariantCulture, out n))
                {
                    if (a[3] == "irk") { who.Act("irk " + p.GetPlayerID().ToString(CultureInfo.InvariantCulture) + " " + n.ToString("0.#", CultureInfo.InvariantCulture)); return "Сказано (у журналі — що вийшло)."; }
                    if (a[3] == "spend") { Spend(who, p, n); return "Сказано."; }
                }
                if (a.Length > 3 && a[3] == "clear") { long by; who.Act("irk " + p.GetPlayerID() + " " + (-Irk(who, out by) - 1f).ToString("0.#", CultureInfo.InvariantCulture)); Spend(who, p, -Spent(who, p.GetPlayerID())); return "Злість і витрату знято."; }
                string tip = Tip(who, p).Replace("<b>", "").Replace("</b>", "").Replace("<i>", "").Replace("</i>", "");
                Npcs.Log("[patience] " + who.Name + "\n" + tip);
                return tip.Replace('\n', ' ');
            }
        }
    }
}
#endif
