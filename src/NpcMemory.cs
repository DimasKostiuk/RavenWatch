#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // What a person remembers (npc-plan.md, «пам'ять подій»): the few things that happened to it that matter — you saved it,
    // brought what it asked for, it went hungry, a neighbour left — kept in its locker, the strong ones for good, the small
    // ones fading. Its words use them (NpcPhrases: {m_<kind>} what it was, {ago_<kind>} when, {n_<kind>} how many times —
    // a template wanting one it does not remember is passed over; themes mem_<kind> said now and then — and while a talk asks
    // (NpcVoice.Asked) its two strongest, the fresher the heavier; «you_remember» in «Що думаєш про мене?»), and so can the story:
    //   in a talk     !remember <kind> [what]   (about you)   ·   !forget <kind>   ·   ?npcremembers <kind> [me] [npc]
    //   in code       NpcMemory.Tell(a, kind, playerId, what)  ·  NpcMemory.Find(a, kind, playerId)  ·  NpcMemory.Kinds
    //   on the bus    «npc_remember_<kind>» (arg: the person) when it takes a thing in
    // A kind not in Kinds is kept for good (the story's own) — but news_* fades in five days and you_land_* too (NpcNews,
    // KindOf). One entry per kind and player: the same again counts up.
    // Written only where the person is owned (Tell goes there); read anywhere its locker is.
    internal static class NpcMemory
    {
        internal static readonly int KMemory = "RavenWatch_npcMemory".GetStableHashCode(); // string "kind,player,time,count,what;…" — in its locker

        internal sealed class Kind { public float Weight; public float FadeDays; public bool Yours; public float Toward; } // (FadeDays 0: kept for good; Yours: about one player — told to that one only; Toward: how much warmer to that player — NpcAttitude)
        internal static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            { "rescued",       new Kind { Weight = 10f, Yours = true, Toward = 40f } }, // you saved it from its trouble
            { "revived",       new Kind { Weight = 8f, Yours = true, Toward = 25f } }, // you brought it round with a mead
            { "hired",         new Kind { Weight = 6f, Yours = true, Toward = 15f } }, // you took it in
            { "fired",         new Kind { Weight = 6f, Yours = true, Toward = -50f } }, // you sent it off (what is left of the warmth: not much)
            { "settled",       new Kind { Weight = 3f } },                // it came to live at a heart
            { "request_done",  new Kind { Weight = 5f, FadeDays = 20f, Yours = true, Toward = 8f } }, // you brought what it asked for (what: the thing)
            { "promise_kept",  new Kind { Weight = 6f, FadeDays = 25f, Yours = true, Toward = 10f } }, // you did what you promised (NpcPromises)
            { "promise_broken", new Kind { Weight = 6f, FadeDays = 15f, Yours = true, Toward = -8f } }, // you did not
            { "heard_kept",    new Kind { Weight = 2f, FadeDays = 8f, Yours = true, Toward = 3f } },  // a neighbour told it you keep your word (NpcPromises)
            { "heard_broken",  new Kind { Weight = 2f, FadeDays = 8f, Yours = true, Toward = -3f } }, // …or do not
            { "neighbour_left", new Kind { Weight = 5f, FadeDays = 15f } }, // one who lived with it went away (what: the name)
            { "fight_won",     new Kind { Weight = 3f, FadeDays = 6f } },  // a fight at home, beaten off
            { "fight_lost",    new Kind { Weight = 4f, FadeDays = 8f } },  // …or not
            { "starved",       new Kind { Weight = 4f, FadeDays = 10f } }, // it went without food
            { "unpaid",        new Kind { Weight = 4f, FadeDays = 10f } }, // the coin box was empty on its pay day
            { "missed",        new Kind { Weight = 2f, FadeDays = 4f, Yours = true } }, // you were away long
            { "goal_done",     new Kind { Weight = 6f, FadeDays = 30f, Toward = 15f } }, // its long wish came true (what: what it was; a player who gave it: warmer — NpcGoals)
            // a quarrel with a neighbour (what: its name — NpcAttitude.Quarrel): over the last food, its own place, one idling while it works,
            // words at a table, one who hid while it fought, one paid when it was not
            { "quarrel_food",  new Kind { Weight = 5f, FadeDays = 4f } },
            { "quarrel_place", new Kind { Weight = 4f, FadeDays = 3f } },
            { "quarrel_work",  new Kind { Weight = 4f, FadeDays = 3f } },
            { "quarrel_table", new Kind { Weight = 5f, FadeDays = 4f } },
            { "quarrel_raid",  new Kind { Weight = 6f, FadeDays = 5f } },
            { "quarrel_pay",   new Kind { Weight = 5f, FadeDays = 4f } },
            { "quarrel_teased", new Kind { Weight = 4f, FadeDays = 3f } }, // a bully picked on it before everyone (NpcAttitude.Tease)
            { "brushed_off",   new Kind { Weight = 2f, FadeDays = 2f, Yours = true, Toward = -3f } }, // you had no time for what weighs on it (NpcTalk: «Мені зараз не до того»)
            { "insulted",      new Kind { Weight = 6f, FadeDays = 4f, Yours = true, Toward = -5f } }, // you were rude on purpose (NpcTalkTone): its next talk opens with it
            { "clothes_taken", new Kind { Weight = 3f, FadeDays = 5f, Yours = true, Toward = -4f } }, // you took back what you dressed it in (NpcTalkDress)
            { "raven_mocked",  new Kind { Weight = 2f, FadeDays = 5f, Yours = true } }, // your raven said something to its face (NpcTalkRaven): now and then a word about it, not enough to open a talk
            { "zoned_out",     new Kind { Weight = 6f, FadeDays = 3f, Yours = true } }, // you stood before it saying nothing, its talk open (NpcTalkIdle): it asks about it next time
            { "secret_told",   new Kind { Weight = 4f, Yours = true, Toward = 5f } },                 // it told you what it never told anyone (NpcFavours)
        };
        private static readonly Kind News = new Kind { Weight = 4f, FadeDays = 5f }, Land = new Kind { Weight = 3f, FadeDays = 5f, Yours = true };

        // a kind's rules: the table's, else by its name (news_* fades in five days; you_land_* too, about you only — NpcNews);
        // null: the story's own, kept for good
        internal static Kind KindOf(string kind)
        {
            Kind k;
            if (Kinds.TryGetValue(kind, out k)) return k;
            if (kind.StartsWith("news_", StringComparison.Ordinal)) return News;
            if (kind.StartsWith("you_land_", StringComparison.Ordinal)) return Land;
            return null;
        }

        // what may be pushed out for room: the fading kinds (news, a meal missed); kept-for-good ones and the story's never
        private static bool Pushable(string kind) { Kind k = KindOf(kind); return k != null && k.FadeDays > 0f; }

        private const int Most = 10; // (entries of the kinds above kept: the weakest, oldest goes first; the story's own are never pushed out)

        internal sealed class Entry { public string Kind = "", What = ""; public long Player; public long At; public int Count = 1; }

        // ---------------------------------------------------------------- writing (where the person is owned)
        // from any game: sent to the one that runs it (NpcActor.Acts «remember»)
        internal static void Tell(NpcActor a, string kind, long player, string what)
        {
            if (a == null || !GoodKind(kind)) return;
            a.Act("remember " + kind + " " + player.ToString(CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(what) ? "" : " " + Clean(what)));
        }

        internal static void Remember(NpcActor a, string kind, long player, string what)
        {
            if (a == null || !a.Owned || !GoodKind(kind)) return;
            List<Entry> l = Read(a);
            if (l == null) return; // (its locker not here yet)
            long now = (long)NpcRescue.Now;
            Entry e = l.Find(x => x.Kind == kind && x.Player == player);
            bool fresh = e == null || now - e.At > (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f); // (the same again within a day: counted, its feeling not moved again — let go and hired back is no trick)
            if (e != null) { e.Count++; e.At = now; if (!string.IsNullOrEmpty(what)) e.What = Clean(what); }
            else l.Add(new Entry { Kind = kind, Player = player, At = now, What = Clean(what ?? "") });
            Write(a, l);
            Npcs.Log(a.Id + ": remembers " + kind + (player != 0 ? " (" + player + ")" : "") + (string.IsNullOrEmpty(what) ? "" : " — " + what));
            StoryBus.Raise("npc_remember_" + kind, a.Id, e != null ? e.Count : 1, a.transform.position);
            Kind k = KindOf(kind);
            if (fresh && player != 0 && k != null && k.Toward != 0f) NpcAttitude.Add(a, player, k.Toward); // (what you did: how it feels about you)
        }

        internal static void Forget(NpcActor a, string kind, long player)
        {
            if (a == null || !a.Owned) return;
            List<Entry> l = Read(a);
            if (l == null || l.RemoveAll(x => x.Kind == kind && (player == 0 || x.Player == player)) == 0) return;
            Write(a, l);
            Npcs.Log(a.Id + ": forgets " + kind);
        }

        // everyone else who lives at its heart takes it in too (a neighbour gone: told to all who stay)
        internal static void TellHome(NpcActor a, string kind, long player, string what)
        {
            ZDOID heart = a != null ? NpcKit.GetRef(a.Zdo, NpcResident.KHeart) : ZDOID.None;
            foreach (NpcActor o in NpcHeart.Residents(heart)) if (o != a) Tell(o, kind, player, what);
        }

        // «remember <kind> <player> [what…]» / «forget <kind> [player]» (NpcActor.Acts)
        internal static void Act(NpcActor a, string[] w)
        {
            long player = 0L;
            if (w.Length > 2) long.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out player);
            if (w[0] == "forget") { if (w.Length > 1) Forget(a, w[1], player); return; }
            if (w.Length > 1) Remember(a, w[1], player, w.Length > 3 ? string.Join(" ", w, 3, w.Length - 3) : "");
        }

        // ---------------------------------------------------------------- reading (any game)
        // its memory of that about this player (player 0: about anyone — the latest)
        internal static Entry Find(NpcActor a, string kind, long player)
        {
            List<Entry> l = Read(a);
            if (l == null) return null;
            Entry best = null;
            foreach (Entry e in l) if (e.Kind == kind && (player == 0 || e.Player == player) && (best == null || e.At > best.At)) best = e;
            return best;
        }

        // what it may speak of to this player: what is about that player, and what is about no one (never another's own)
        internal static List<Entry> About(NpcActor a, long player)
        {
            List<Entry> got = new List<Entry>();
            List<Entry> l = Read(a);
            if (l != null) foreach (Entry e in l) if ((player != 0 && e.Player == player) || (e.Player == 0 && !Yours(e.Kind))) got.Add(e);
            return got;
        }

        private static bool Yours(string kind) { Kind k = KindOf(kind); return k != null && k.Yours; }

        // when, in words (game days: today, yesterday, a few days ago, long ago)
        // ---- for the eyes, not for its words: what its card and a heart's board say of a memory
        //   Lines     what it remembers of you (its card «До тебе»): the text, its forms {твої} and його|її, and whether it counts for you or against
        //   Board     what happened at its home (a heart's board «Життя»): {0} what it was / who
        private sealed class Shown { public string Text; public bool Against; public Shown(string t, bool against = false) { Text = t; Against = against; } }
        private static readonly Dictionary<string, Shown> Lines = new Dictionary<string, Shown>
        {
            { "rescued", new Shown(Lang.K("ти врятува{в|ла} його|її")) },
            { "revived", new Shown(Lang.K("ти постави{в|ла} його|її на ноги")) },
            { "hired", new Shown(Lang.K("ти взя{в|ла} його|її до себе")) },
            { "fired", new Shown(Lang.K("ти прогна{в|ла} його|її"), true) },
            { "request_done", new Shown(Lang.K("ти прин{іс|есла} те, що просив|просила")) },
            { "promise_kept", new Shown(Lang.K("ти дотрима{в|ла} слова")) },
            { "promise_broken", new Shown(Lang.K("ти не дотрима{в|ла} слова"), true) },
            { "heard_kept", new Shown(Lang.K("чув|чула, що ти тримаєш слово")) },
            { "heard_broken", new Shown(Lang.K("чув|чула, що ти не тримаєш слова"), true) },
            { "goal_done", new Shown(Lang.K("ти допом{іг|огла} здійснити мрію")) },
            { "secret_told", new Shown(Lang.K("розповів|розповіла тобі те, чого не казав|казала нікому")) },
            { "missed", new Shown(Lang.K("тебе довго не було"), true) },
            { "brushed_off", new Shown(Lang.K("ти відмахну{вся|лась} від його|її турботи"), true) },
            { "insulted", new Shown(Lang.K("ти нагруби{в|ла} йому|їй"), true) },
            { "clothes_taken", new Shown(Lang.K("ти забра{в|ла} одяг, який да{в|ла}"), true) },
            { "zoned_out", new Shown(Lang.K("ти стоя{в|ла} перед ним|нею й мовча{в|ла}"), true) },
            { "raven_mocked", new Shown(Lang.K("твій ворон щось сказав йому|їй просто в очі"), true) },
        };

        internal sealed class Line { public string Text = ""; public bool Against; public long At; }

        // what it remembers of this player, the latest first
        internal static List<Line> OfYou(NpcActor a, Player p)
        {
            List<Line> l = new List<Line>();
            if (a == null || p == null) return l;
            foreach (Entry e in About(a, p.GetPlayerID()))
            {
                Shown s;
                if (e.Player != p.GetPlayerID() || !Lines.TryGetValue(e.Kind, out s)) continue;
                l.Add(new Line { Text = a.Form(Grammar.Fix(Lang.T(s.Text), p)), Against = s.Against, At = e.At });
            }
            l.Sort((x, y) => y.At.CompareTo(x.At));
            return l;
        }

        private static string BoardWords(string kind, string what, string who)
        {
            switch (kind)
            {
                case "neighbour_left": return Lang.F("Пішли з оселі: {0}", what);
                case "news_newcomer": return Lang.F("Нова людина в оселі: {0}", what);
                case "fight_won": return Lang.T("Напад на оселю відбито");
                case "fight_lost": return Lang.T("Напад на оселю не вдалося відбити");
                case "starved": return Lang.F("Голодували: {0}", who);
                case "unpaid": return Lang.F("Лишились без платні: {0}", who);
                case "goal_done": return what.Length > 0 ? Lang.F("Збулась мрія: {0} ({1})", who, what) : Lang.F("Збулась мрія: {0}", who);
            }
            if (kind.StartsWith("news_boss_", StringComparison.Ordinal)) return Lang.T("Прийшла звістка: повалено володаря цих земель");
            return null;
        }

        // what happened at home, one line a thing however many remember it (those who went through it together named together), the latest first
        internal sealed class Event { public string Kind = "", What = ""; public long At; public readonly List<string> Who = new List<string>(); }
        internal static List<KeyValuePair<string, long>> HomeEvents(List<NpcActor> folk, int most)
        {
            Dictionary<string, Event> seen = new Dictionary<string, Event>();
            foreach (NpcActor a in folk)
            {
                if (a == null) continue;
                foreach (Entry e in About(a, 0L))
                {
                    if (e.Player != 0L || BoardWords(e.Kind, e.What, "") == null) continue;
                    Event ev;
                    string key = e.Kind + "|" + e.What;
                    if (!seen.TryGetValue(key, out ev)) seen[key] = ev = new Event { Kind = e.Kind, What = e.What, At = e.At };
                    if (e.At > ev.At) ev.At = e.At;
                    if (!ev.Who.Contains(a.Name)) ev.Who.Add(a.Name);
                }
            }
            List<Event> all = new List<Event>(seen.Values);
            all.Sort((x, y) => y.At.CompareTo(x.At));
            List<KeyValuePair<string, long>> l = new List<KeyValuePair<string, long>>();
            for (int i = 0; i < all.Count && i < most; i++) l.Add(new KeyValuePair<string, long>(BoardWords(all[i].Kind, all[i].What, string.Join(", ", all[i].Who.ToArray())), all[i].At));
            return l;
        }

        internal static string Ago(long at)
        {
            float day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f;
            long days = (long)(NpcRescue.Now / day) - (long)(at / day);
            return days <= 0 ? Lang.T("сьогодні") : days == 1 ? Lang.T("учора") : days < 5 ? Lang.T("кілька днів тому") : Lang.T("давно");
        }

        // ---------------------------------------------------------------- its words: values and themes (NpcModules wires them)
        internal static void Values(NpcActor a, Player to, Dictionary<string, string> v)
        {
            foreach (Entry e in About(a, to != null ? to.GetPlayerID() : 0L))
            {
                string k = e.Kind;
                if (v.ContainsKey("ago_" + k) && e.Player == 0) continue; // (about you first)
                v["m_" + k] = e.What;
                v["ago_" + k] = Ago(e.At);
                v["n_" + k] = e.Count.ToString(CultureInfo.InvariantCulture);
            }
        }

        // now and then (not more than once in a while) a word of something it remembers
        private static readonly Dictionary<NpcActor, float> NextRecall = new Dictionary<NpcActor, float>();
        internal static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            float at;
            if (!NpcVoice.Asked && NextRecall.TryGetValue(a, out at) && Time.time < at) return;
            List<Entry> l = About(a, p.GetPlayerID());
            l.RemoveAll(x => !NpcPhrases.Has(a, "mem_" + x.Kind)); // (only what it has words for: a story's own kind with none is not «recalled» in silence)
            if (l.Count == 0) return;
            if (NpcVoice.Asked) // (a talk asks what weighs on it: its strongest memories now, the fresher the heavier — nothing held back for later)
            {
                List<KeyValuePair<string, float>> all = new List<KeyValuePair<string, float>>();
                float day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f;
                foreach (Entry x in l)
                {
                    Kind k = KindOf(x.Kind);
                    float days = (float)((NpcRescue.Now - x.At) / day), weight = k != null ? k.Weight : 5f;
                    all.Add(new KeyValuePair<string, float>("mem_" + x.Kind, (days < 1f ? 55f : days < 2f ? 35f : days < 5f ? 22f : 12f) * Mathf.Clamp(weight / 5f, 0.4f, 1.6f)));
                }
                all.Sort((x, y) => y.Value.CompareTo(x.Value));
                for (int i = 0; i < all.Count && i < 2; i++) themes.Add(all[i]);
                return;
            }
            if (NextRecall.Count > 128) NextRecall.Clear();
            NextRecall[a] = Time.time + UnityEngine.Random.Range(300f, 600f); // (a chance, not a word said: the theme may lose to another — so not too long)
            List<Entry> today = l.FindAll(x => (x.Kind.StartsWith("news_", StringComparison.Ordinal) || x.Kind.StartsWith("you_land_", StringComparison.Ordinal)) && Ago(x.At) == Ago((long)NpcRescue.Now)); // (fresh news: told sooner)
            Entry e = today.Count > 0 ? today[UnityEngine.Random.Range(0, today.Count)] : l[UnityEngine.Random.Range(0, l.Count)];
            themes.Add(new KeyValuePair<string, float>("mem_" + e.Kind, today.Count > 0 ? 55f : 22f)); // (today's news: said at the first chance — an old memory only now and then)
        }

        // ---------------------------------------------------------------- the story's condition
        // a fight at home remembered, once in NpcMind.FightMemo s: a fight's back and forth is one fight (NpcMind.FightOver)
        internal static void FightEnded(NpcActor me, string kind)
        {
            if (!NpcResident.Is(me) || Utils.DistanceXZ(me.transform.position, NpcRules.Home(me)) >= 40f) return;
            Entry had = Find(me, kind, 0L);
            if (had == null || NpcRescue.Now - had.At > NpcMind.FightMemo) Remember(me, kind, 0L, "");
        }

        internal static void Register()
        {
            StoryCond.Register("npcremembers", new StoryCond.Kind { Usage = "npcremembers <подія> [me] [npc]   (пам'ятає це — про будь-кого; me — саме про тебе)", Min = 1,
                Test = (p, w) =>
                {
                    bool me = w.Length > 2 && w[2] == "me";
                    NpcActor n = NpcLevers.Who(w, me ? 3 : 2); // (named and not here: no one — never the one you talk to instead)
                    return n != null && (me ? p != null && Find(n, w[1], p.GetPlayerID()) != null : Find(n, w[1], 0L) != null);
                } });
        }

        // the console: what it remembers
        internal static string Describe(NpcActor a)
        {
            List<Entry> l = Read(a);
            if (l == null) return Lang.T("Шафка ще не тут.");
            if (l.Count == 0) return a.Name + ": " + Lang.T("нічого не пам'ятає.");
            StringBuilder sb = new StringBuilder(a.Name + ":");
            foreach (Entry e in l) sb.Append("\n  ").Append(e.Kind).Append(e.Player != 0 ? " (" + e.Player + ")" : "").Append(e.Count > 1 ? " ×" + e.Count : "").Append(" — ").Append(Ago(e.At)).Append(e.What.Length > 0 ? ": " + e.What : "");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- kept
        // null: its locker cannot be read here now; faded ones left out
        private static List<Entry> Read(NpcActor a)
        {
            string s = NpcLocker.Get(a, KMemory);
            if (s == null) return null;
            List<Entry> l = new List<Entry>();
            float day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f;
            double now = NpcRescue.Now;
            foreach (string raw in s.Split(';'))
            {
                string[] f = raw.Split(',');
                if (f.Length < 4) continue;
                Entry e = new Entry { Kind = f[0], What = f.Length > 4 ? f[4] : "" };
                long.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out e.Player);
                long.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out e.At);
                int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out e.Count);
                Kind k = KindOf(e.Kind);
                if (k != null && k.FadeDays > 0f && now - e.At > k.FadeDays * day) continue; // (faded)
                l.Add(e);
            }
            return l;
        }

        private static void Write(NpcActor a, List<Entry> l)
        {
            float day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f;
            double now = NpcRescue.Now;
            while (l.FindAll(x => Pushable(x.Kind)).Count > Most) // (the weakest of the everyday kinds goes: its weight less a little for each day since)
            {
                Entry worst = null; float low = float.MaxValue;
                foreach (Entry e in l) { if (!Pushable(e.Kind)) continue; Kind k = KindOf(e.Kind); float w = k.Weight - (float)((now - e.At) / day) * 0.2f; if (w < low) { low = w; worst = e; } }
                l.Remove(worst);
            }
            List<string> parts = new List<string>();
            foreach (Entry e in l) parts.Add(e.Kind + "," + e.Player.ToString(CultureInfo.InvariantCulture) + "," + e.At.ToString(CultureInfo.InvariantCulture) + "," + e.Count.ToString(CultureInfo.InvariantCulture) + "," + e.What);
            NpcLocker.Set(a, KMemory, string.Join(";", parts.ToArray()));
        }

        private static bool GoodKind(string kind) { if (string.IsNullOrEmpty(kind)) return false; foreach (char c in kind) if (!char.IsLetterOrDigit(c) && c != '_') return false; return true; }
        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(';', ' ').Replace(',', ' ').Replace('|', '¦').Trim(); }
    }
}
#endif
