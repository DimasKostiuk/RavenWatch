using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Builds Hugin's remark about the last minutes: picks the most notable event, a title and a text for it,
    // fills in names in the right grammatical case and sometimes adds a short tail.
    internal static class Composer
    {
        private static readonly System.Random Rng = new System.Random();

        internal const string TitlesFile = "hugin_event_titles.txt";
        internal const string TextsFile = "hugin_event_texts.txt";
        internal const string TailsFile = "hugin_tails.txt";
        internal const string NamesFile = "creature_names.txt";
        internal const string HuginQuipsFile = "hugin_quips.txt";
        internal const string MuninQuipsFile = "munin_quips.txt";

        internal static void EnsureDefaults(string dir)
        {
            WriteIfMissing(Path.Combine(dir, TitlesFile), Lang.T("Хугін: заголовки над головою для подій. Формат: [подія] текст."), DefaultsEvents.Titles);
            WriteIfMissing(Path.Combine(dir, TextsFile),
                Lang.T("Хугін: що він каже про події останніх хвилин. Формат: [подія] текст. Підстановки: {name} {killer} {killer_gen} {killer_ins} {attacker} {attacker_gen} {attacker_ins} {boss} {boss_gen} {boss_ins} {top} {top_gen} {top_ins} {top_genpl} {count} {times} {level} {biome} {biome_in} {km}."),
                DefaultsEvents.Texts);
            WriteIfMissing(Path.Combine(dir, TailsFile), Lang.T("Хугін: короткі хвости, які інколи додаються після фрази про подію."), DefaultsEvents.Tails);
            WriteIfMissing(Path.Combine(dir, HuginQuipsFile), Lang.T("Хугін: одна фраза, яку він каже одразу, коли прилітає після події. Формат: [подія] текст. {name} — нік гравця, інші підстановки як у hugin_event_texts.txt, а також статистика персонажа: {deaths_all} {cause_times} {tombs_times} {bosses_all} {kills_all} {killer_kills} {top_kills} {streak_max} {km_all} {air_km} {sail_km} {built_all} {base_hours} {trees_all} {mines_all} {fish_all} {fish_lost} {portals_times} {sleep_all} {food_all} {berries_all} {jumps_all}."), Quips.Hugin, QuipsRevision);
            WriteIfMissing(Path.Combine(dir, MuninQuipsFile), Lang.T("Мунін: одна фраза, яку він каже одразу, коли прилітає після події. Формат: [подія] текст. {name} — нік гравця, інші підстановки як у hugin_event_texts.txt, а також статистика персонажа: {deaths_all} {cause_times} {tombs_times} {bosses_all} {kills_all} {killer_kills} {top_kills} {streak_max} {km_all} {air_km} {sail_km} {built_all} {base_hours} {trees_all} {mines_all} {fish_all} {fish_lost} {portals_times} {sleep_all} {food_all} {berries_all} {jumps_all}."), Quips.Munin, QuipsRevision);
#if RAVEN_ITEM
            Patterns.EnsureFiles(dir);
#if RAVEN_ITEM
            RavenTalk.Init(dir); // the ravens' talks with each other
#endif
#endif
            WriteIfMissing(Path.Combine(dir, NamesFile), Lang.T("Назви істот у відмінках: ключ = називний | родовий | орудний | родовий множини. Ключ — назва з гри без $."), DefaultsEvents.CreatureNames);
        }

        // bump when the quick ravens get new lines for events they already had
        private const int QuipsRevision = 7;

        private static void WriteIfMissing(string path, string header, string[] lines) { WriteIfMissing(path, header, lines, 0); }

        private static void WriteIfMissing(string path, string header, string[] lines, int revision)
        {
            lines = Lang.Lines(Path.GetFileName(path), lines); // the current language's own lines
            if (File.Exists(path))
            {
                if (revision > 0) AppendRevision(path, lines, revision);
                AppendNewTags(path, lines);
                return;
            }
            List<string> all = new List<string>();
            all.Add("# " + header);
            all.Add(Lang.T("# Рядки з # на початку пропускаються. Файл можна правити під час гри."));
            if (revision > 0) all.Add(RevisionMark(revision));
            all.AddRange(lines);
            File.WriteAllLines(path, all.ToArray(), new UTF8Encoding(false));
        }

        private static string RevisionMark(int revision) { return "# rev " + revision + " (" + Lang.T("не видаляй: так мод знає, які фрази вже дописав") + ")"; } // (the mark itself is never translated: it is read back)

        // a revision brings new lines for old events: add the ones the file lacks, once
        private static void AppendRevision(string path, string[] lines, int revision)
        {
            string[] raw = File.ReadAllLines(path, Encoding.UTF8);
            string mark = "# rev " + revision + " ";
            HashSet<string> have = new HashSet<string>();
            foreach (string l in raw)
            {
                if (l.StartsWith(mark)) return;
                have.Add(l.Trim());
            }
            List<string> add = new List<string>();
            add.Add(RevisionMark(revision));
            foreach (string l in lines) if (!have.Contains(l)) add.Add(l);
            File.AppendAllText(path, "\n" + string.Join("\n", add.ToArray()) + "\n", new UTF8Encoding(false));
        }

        // a mod update brings new events: add their lines to the player's file without touching the rest
        private static void AppendNewTags(string path, string[] lines)
        {
            List<string> existing = ReadPlain(path, null);
            HashSet<string> tags = new HashSet<string>();
            HashSet<string> keys = new HashSet<string>();
            foreach (string l in existing)
            {
                string t = TagOf(l);
                if (t != null) tags.Add(t);
                int eq = l.IndexOf('=');
                if (eq > 0) keys.Add(l.Substring(0, eq).Trim());
            }
            List<string> add = new List<string>();
            foreach (string l in lines)
            {
                string t = TagOf(l);
                if (t != null) { if (!tags.Contains(t)) add.Add(l); continue; }
                int eq = l.IndexOf('=');
                if (eq > 0 && !keys.Contains(l.Substring(0, eq).Trim())) add.Add(l);
            }
            if (add.Count > 0) File.AppendAllText(path, "\n" + string.Join("\n", add.ToArray()) + "\n", new UTF8Encoding(false));
        }

        private static string TagOf(string line)
        {
            if (!line.StartsWith("[")) return null;
            int end = line.IndexOf(']');
            return end > 1 ? line.Substring(1, end - 1) : null;
        }

        // ---------------------------------------------------------------- choosing the event

        private struct Pick { public string Tag; public int Score; public float Age; public Pick(string t, int s, float a) { Tag = t; Score = s; Age = a; } }

        // fresh things matter more: an old death fades behind a boar killed a minute ago
        private static float Fresh(float age)
        {
            if (age < 60f) return 1f;
            if (age < 120f) return 0.85f;
            if (age < 240f) return 0.7f;
            if (age < 420f) return 0.5f;
            return 0.35f;
        }

        private static void Add(List<Pick> list, string tag, int score, float age)
        {
            list.Add(new Pick(tag, Mathf.RoundToInt(score * Fresh(age)), age));
        }

        private static List<Pick> Rank(Diary.Summary s)
        {
            List<Pick> list = new List<Pick>();
            if (!string.IsNullOrEmpty(s.Boss)) Add(list, "boss", 100, Time.time - s.BossAt);
            if (s.N(Ev.Raid) > 0) Add(list, "raid", 86, s.Age(Ev.Raid));
            int deaths = s.N(Ev.Death);
            if (deaths >= 2) Add(list, "death_many", 92, s.Age(Ev.Death));
            else if (deaths == 1) Add(list, DeathTag(s.LastDeathCause), 88, s.Age(Ev.Death));
            if (s.N(Ev.NearDeath) > 0) Add(list, "near_death", 80, s.Age(Ev.NearDeath));
            if (s.Level > 0) Add(list, "level", 75, s.Age(Ev.Level));
            if (s.Ticks >= 20 && s.N(Ev.Afk) >= s.Ticks * 0.7f && Time.time - Diary.LastActive >= 20f) Add(list, "afk", 70, s.Age(Ev.Afk));
            if (s.N(Ev.Friendly) > 0) Add(list, "friendly", 68, s.Age(Ev.Friendly));
            int kills = s.N(Ev.Kill);
            if (kills >= 8) Add(list, "kill_many", 65, s.Age(Ev.Kill));
            if (s.N(Ev.Escaped) > 0) Add(list, "escaped", 62, s.Age(Ev.Escaped));
            if (s.N(Ev.Flee) > 0) Add(list, "flee", 60, s.Age(Ev.Flee));
            if (!string.IsNullOrEmpty(s.NewBiome)) Add(list, "biome", 60, s.Age(Ev.Biome));
            if (s.N(Ev.Dungeon) > 0) Add(list, "dungeon", 58, s.Age(Ev.Dungeon));
            if (s.N(Ev.Fall) > 0) Add(list, "fall", 55, s.Age(Ev.Fall));
            if (s.N(Ev.Build) >= 15) Add(list, "build", kills == 0 ? 57 : 52, s.Age(Ev.Build));
            if (s.N(Ev.Fish) > 0) Add(list, "fish", 52, s.Age(Ev.Fish));
            if (s.N(Ev.Hungry) >= 48 && Player.m_localPlayer != null && Player.m_localPlayer.GetFoods().Count == 0) Add(list, "hungry", 22, s.Age(Ev.Hungry));
            if (s.N(Ev.Encumbered) >= 12) Add(list, "encumbered", 50, s.Age(Ev.Encumbered));
            if (s.N(Ev.Swim) >= 8 && s.Age(Ev.Swim) < 60f) Add(list, "swim", 48, s.Age(Ev.Swim));
            if (kills >= 1 && kills < 8) Add(list, "kill", 47, s.Age(Ev.Kill));
            if (s.N(Ev.Sail) >= 12 && s.Age(Ev.Sail) < 60f) Add(list, "sail", 45, s.Age(Ev.Sail));
            if (s.N(Ev.Chop) >= 25) Add(list, "chop", 42, s.Age(Ev.Chop));
            if (s.N(Ev.Mine) >= 25) Add(list, "mine", 42, s.Age(Ev.Mine));
            if (s.N(Ev.Portal) > 0) Add(list, "portal", 40, s.Age(Ev.Portal));
            if (s.N(Ev.Cook) >= 3) Add(list, "cook", 40, s.Age(Ev.Cook));
            if (s.N(Ev.Craft) >= 3) Add(list, "craft", 38, s.Age(Ev.Craft));
            if (s.N(Ev.Sleep) > 0) Add(list, "sleep", 36, s.Age(Ev.Sleep));
            if (s.N(Ev.Pick) >= 15) Add(list, "pick", 35, s.Age(Ev.Pick));
            if (s.Meters >= 2000) Add(list, "travel", 35, s.Age(Ev.Travel));
            if (s.N(Ev.Loot) >= 3) Add(list, "loot", 33, s.Age(Ev.Loot));
            if (s.N(Ev.Pet) > 0) Add(list, "pet", 30, s.Age(Ev.Pet));
            // the everyday things: a fight's finer moments, the road, the workshop, the home
            if (s.N(Ev.Parry) > 0) Add(list, "parry", 50, s.Age(Ev.Parry));
            if (s.N(Ev.Bow) >= 4) Add(list, "bow", 46, s.Age(Ev.Bow));
            if (s.N(Ev.Magic) >= 4) Add(list, "magic", 46, s.Age(Ev.Magic));
            if (s.N(Ev.Ride) >= 6 && !string.IsNullOrEmpty(s.Mount)) Add(list, "ride", 46, s.Age(Ev.Ride));
            if (s.N(Ev.Dodge) > 0) Add(list, "dodge", 45, s.Age(Ev.Dodge));
            if (s.N(Ev.Trade) > 0) Add(list, "trade", 44, s.Age(Ev.Trade));
            if (s.N(Ev.TrophyHang) > 0) Add(list, "trophy_hang", 44, s.Age(Ev.TrophyHang));
            if (s.N(Ev.Trophy) > 0) Add(list, "trophy", 43, s.Age(Ev.Trophy));
            if (s.N(Ev.Potion) > 0) Add(list, "potion", 42, s.Age(Ev.Potion));
            if (s.N(Ev.Cart) >= 6) Add(list, "cart", 40, s.Age(Ev.Cart));
            if (s.N(Ev.Plant) >= 5) Add(list, "plant", 40, s.Age(Ev.Plant));
            if (s.N(Ev.Smelt) >= 3) Add(list, "smelt", 38, s.Age(Ev.Smelt));
            if (s.N(Ev.Repair) > 0) Add(list, "repair", 37, s.Age(Ev.Repair));
            if (s.N(Ev.Ferment) > 0) Add(list, "ferment", 36, s.Age(Ev.Ferment));
            if (s.N(Ev.Sit) > 0) Add(list, "sit", 28, s.Age(Ev.Sit));
            // 2.4.0: ore at a portal, a broken thing, a tamed beast, the body freezing, poisoned or on fire
            if (s.N(Ev.Tame) > 0 && !string.IsNullOrEmpty(s.Tamed)) Add(list, "tame", 58, s.Age(Ev.Tame));
            if (s.N(Ev.Broke) > 0) Add(list, "broke", 52, s.Age(Ev.Broke));
            if (s.N(Ev.Burning) > 0) Add(list, "burning", 50, s.Age(Ev.Burning));
            if (s.N(Ev.Poison) > 0) Add(list, "poisoned", 48, s.Age(Ev.Poison));
            if (s.N(Ev.PortalBlocked) > 0) Add(list, "portal_ore", 47, s.Age(Ev.PortalBlocked));
            if (s.N(Ev.Freezing) > 0) Add(list, "freezing", 46, s.Age(Ev.Freezing));
            if (s.N(Ev.Cozy) >= 24 && s.Age(Ev.Cozy) < 60f) Add(list, "cozy", 16, s.Age(Ev.Cozy)); // at home for hours: now and then, not every thought
            if (s.N(Ev.Night) >= 24 && EnvMan.IsNight()) Add(list, "night", 30, s.Age(Ev.Night));
            string weather = WeatherTag();
            if (weather != null) list.Add(new Pick(weather, weather == "weather_clear" ? 10 : 24, 0f)); // a clear day is most days: below the land's own words
            Player me = Player.m_localPlayer;
            if (me != null) list.Add(new Pick("here_" + me.GetCurrentBiome().ToString().ToLowerInvariant(), 14, 0f));
            list.Add(new Pick("here", 12, 0f));
            list.Add(new Pick("idle", 0, 9999f));
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            return list;
        }

        // the weather right now, from the environment's name (Rain, ThunderStorm, Misty, Snow, Ashrain...)
        private static string WeatherTag()
        {
            if (EnvMan.instance == null) return null;
            EnvSetup env = EnvMan.instance.GetCurrentEnvironment();
            string n = env != null && env.m_name != null ? env.m_name.ToLowerInvariant() : "";
            if (n.Contains("thunder") || n.Contains("storm")) return "weather_storm";
            if (n.Contains("snow")) return "weather_snow";
            if (n.Contains("rain")) return "weather_rain";
            if (n.Contains("mist") || n.Contains("fog")) return "weather_fog";
            // a clear sky by day, out in the open (the Mistlands' "clear" is still murk)
            Player me = Player.m_localPlayer;
            if (n.Contains("clear") && !n.Contains("mistlands") && EnvMan.IsDay() && me != null && !me.InInterior()) return "weather_clear";
            return null;
        }

        private static string DeathTag(string cause)
        {
            switch (cause)
            {
                case "creature": return "death_creature";
                case "Fall": return "death_fall";
                case "Drowning": case "Water": return "death_drown";
                case "Burning": case "CinderFire": case "AshlandsLava": case "AshlandsOcean": return "death_fire";
                case "Poisoned": return "death_poison";
                case "Freezing": return "death_cold";
                case "Tree": return "death_tree";
                default: return "death";
            }
        }

        // ---------------------------------------------------------------- composing

        internal sealed class Remark { public string Tag; public string Title; public string Text; }

        // a quick remark straight after an event: one line with the player's name, Hugin or Munin
        // words only a raven sitting on you may say: a raven that flies in to someone (maybe without a raven at all) leaves them out
        private static string[] ShoulderWords { get { return Lang.ArrP(ShoulderWordsSrc); } } // (translated on every read)
        private static readonly string[] ShoulderWordsSrc = Lang.Keys("плеч", "сиджу на тобі", "на тобі сиджу", "сидимо", "на тобі як на троні", "я на тобі");

        private static bool ShoulderOnly(string text)
        {
            string low = text.ToLowerInvariant();
            foreach (string w in ShoulderWords) if (low.Contains(w)) return true;
            return false;
        }

        internal static Remark ComposeQuip(string dir, Player p, bool munin, string preferTag, string lastTag, float windowSeconds) { using (Lang.Phrase()) return ComposeQuipIn(dir, p, munin, preferTag, lastTag, windowSeconds); } // (the line's values in its own language)

        private static Remark ComposeQuipIn(string dir, Player p, bool munin, string preferTag, string lastTag, float windowSeconds)
        {
            Tone.Use(munin);
            Diary.Summary s = Diary.Summarize(windowSeconds);
            Dictionary<string, string> values = Values(dir, p, s);
            List<string> lines = ReadTagged(Path.Combine(dir, munin ? MuninQuipsFile : HuginQuipsFile), munin ? Quips.Munin : Quips.Hugin);
            List<string> order = new List<string>();
            if (!string.IsNullOrEmpty(preferTag)) order.Add(preferTag);
            foreach (Pick pick in Rank(s))
            {
                if (pick.Tag == lastTag && pick.Score < 80 && pick.Tag != "idle") continue;
                if (!order.Contains(pick.Tag)) order.Add(pick.Tag);
            }
            foreach (string tag in order)
            {
                List<string> candidates = null; // the whole lines: looked through only when they may be needed (they are a spare now)
                string text = null;
#if RAVEN_ITEM
                // like the thought: a sentence put together from parts, so it is new nearly every time
                bool has = Patterns.Has(dir, munin, tag);
                if (!has || Patterns.Share < 1f) { candidates = Usable(lines, tag, values); candidates.RemoveAll(ShoulderOnly); } // it flew in: it sits on nobody's shoulder
                if (has && (candidates == null || candidates.Count == 0 || Rng.NextDouble() < Patterns.Share))
                    text = Patterns.Compose(dir, munin, tag, values, t => AllKnown(t, values) && !ShoulderOnly(t));
#endif
                if (text == null && candidates == null) { candidates = Usable(lines, tag, values); candidates.RemoveAll(ShoulderOnly); }
                if (text == null && candidates.Count > 0) text = candidates[Rng.Next(candidates.Count)];
                if (text == null) continue;
                text = Again(dir, munin, tag, text, values, t => AllKnown(t, values) && !ShoulderOnly(t));
                Remark r = new Remark();
                r.Tag = tag;
                r.Title = Fill(text, values);
                return r;
            }
            return null;
        }

#if RAVEN_ITEM
        // ---------------------------------------------------------------- a raven's thought (the ability)
        // What happened lately (the diary) and what the raven sees right now: enemies close, you barely standing,
        // a friend near, the place, the night. Asked for every few seconds, so not always the top thing: one of
        // the three best, and not what it just said. A theme with templates (Patterns) is often said as a sentence
        // assembled from parts, so it is new nearly every time; the whole lines stay for the rest.

        private const float SeeRange = 30f;

        // The thought comes from the raven on your shoulder: lines written for a raven that flies in
        // ("пролітав", "прилетів сказати") are left to the visiting ravens. Lines tagged sh_<theme>
        // are the shoulder's own and only the thought uses them.
        private static string[] VisitWords { get { return Lang.ArrP(VisitWordsSrc); } } // (translated on every read)
        private static readonly string[] VisitWordsSrc = Lang.Keys("прилет", "пролітав", "залет", "привітат", "навідат", "перевіряю, чи ти живий");

        // a timer's raven that already sits on your shoulder: the same choice of event as a quick raven,
        // with the shoulder's lines (no flying in)
        internal static Remark ComposeShoulderQuip(string dir, Player p, bool munin, string preferTag, float windowSeconds) { using (Lang.Phrase()) return ComposeShoulderQuipIn(dir, p, munin, preferTag, windowSeconds); } // (the line's values in its own language)
        private static Remark ComposeShoulderQuipIn(string dir, Player p, bool munin, string preferTag, float windowSeconds)
        {
            Tone.Use(munin);
            Diary.Summary s = Diary.Summarize(windowSeconds);
            Dictionary<string, string> values = Values(dir, p, s);
            if (Trust.Hostile(Trust.Worn(munin))) return Hostile(dir, p, munin, values, Rank(s));
            if (string.IsNullOrEmpty(preferTag))
            {
                Remark past = Remember(dir, p, munin, Rank(s), values);
                if (past != null) return past;
            }
            List<string> lines = ReadTagged(Path.Combine(dir, munin ? MuninQuipsFile : HuginQuipsFile), munin ? Quips.Munin : Quips.Hugin);
            List<string> order = new List<string>();
            if (!string.IsNullOrEmpty(preferTag)) order.Add(preferTag);
            foreach (Pick pick in Rank(s)) if (!order.Contains(pick.Tag)) order.Add(pick.Tag);
            Func<string, bool> known = t => AllKnown(t, values) && !Visiting(t); // it sits on you: no words of a raven that flew in
            foreach (string tag in order)
            {
                List<string> candidates = null; // the whole lines: looked through only when they may be needed
                string text = null;
                if (Patterns.Has(dir, munin, "sh_" + tag) && Rng.NextDouble() < ShoulderShare) text = Patterns.Compose(dir, munin, "sh_" + tag, values, known);
                bool has = Patterns.Has(dir, munin, tag);
                if (text == null && (!has || Patterns.Share < 1f)) candidates = ForShoulder(lines, tag, values);
                if (text == null && has && (candidates == null || candidates.Count == 0 || Rng.NextDouble() < Patterns.Share)) text = Patterns.Compose(dir, munin, tag, values, known);
                if (text == null && candidates == null) candidates = ForShoulder(lines, tag, values);
                if (text == null && candidates.Count > 0) text = candidates[Rng.Next(candidates.Count)];
                if (text == null) continue;
                text = Again(dir, munin, tag, text, values, known);
                Remark r = new Remark();
                r.Tag = tag;
                r.Title = Fill(text, values);
                return r;
            }
            return null;
        }

        // ---------------------------------------------------------------- the ravens' memory

        // when nothing much is happening, now and then a raven recalls something from its own memory instead
        internal static double MemoryShare = 0.35;
        private static readonly string[] Loud = { "raid", "boss", "near_death", "kill", "kill_many", "level", "friendly", "escaped", "flee", "biome", "dungeon", "fall", "see_enemy", "see_hurt", "encumbered", "hungry" };

        private static Remark Remember(string dir, Player p, bool munin, List<Pick> picks, Dictionary<string, string> values)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (!Memory.Remembers(item, p) || Rng.NextDouble() >= MemoryShare) return null;
            string ctx = "idle";
            foreach (Pick pick in picks)
            {
                if (pick.Tag.StartsWith("death") || Array.IndexOf(Loud, pick.Tag) >= 0) return null; // something happened: that first
                if (ctx == "idle" && pick.Tag != "idle" && !pick.Tag.StartsWith("see_") && !pick.Tag.StartsWith("weather")) ctx = pick.Tag;
            }
            return Recollect(dir, p, munin, item, ctx, values, Rng.NextDouble() < 0.35); // the lifetime numbers only now and then, they have no story to rest
        }

        // a story by the fire: asked for, so no chance roll; the moment counts as rest
        internal static string ComposeMemory(string dir, Player p, bool munin) { using (Lang.Phrase()) return ComposeMemoryIn(dir, p, munin); } // (the line's values in its own language)
        private static string ComposeMemoryIn(string dir, Player p, bool munin)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (!Memory.Remembers(item, p)) return null;
            Tone.Use(munin);
            Dictionary<string, string> values = Values(dir, p, Diary.Summarize(Diary.WindowSeconds));
            Remark r = Recollect(dir, p, munin, item, "rest", values, true);
            return r != null ? r.Title : null;
        }

        private static Remark Recollect(string dir, Player p, bool munin, ItemDrop.ItemData item, string ctx, Dictionary<string, string> values, bool numbers)
        {
            string friend;
            values.TryGetValue("friend", out friend);
            if (string.IsNullOrEmpty(friend) && p != null)
                foreach (Player other in Player.GetAllPlayers())
                    if (other != null && other != p && Vector3.Distance(other.transform.position, p.transform.position) < 15f) { friend = other.GetPlayerName(); break; }
            List<Memory.Mem> list;
            List<Memory.Option> options = Memory.Options(item, p, ctx, friend, out list);
            List<string> lines = ReadTagged(Path.Combine(dir, munin ? MuninQuipsFile : HuginQuipsFile), munin ? Quips.Munin : Quips.Hugin);
            Dictionary<string, string[]> names = ReadNames(Path.Combine(dir, NamesFile));
            for (int i = 0; i < options.Count && i < 4; i++)
            {
                Memory.Option o = options[i];
                if (o.M == null && !numbers) continue;
                Dictionary<string, string> v = new Dictionary<string, string>(values);
                if (o.M != null) Recall(v, names, o.M);
                Func<string, bool> known = t => AllKnown(t, v);
                string tag = o.M != null && o.M.Told > 0 && Patterns.Has(dir, munin, o.Tag + "_again") && Rng.NextDouble() < 0.6 ? o.Tag + "_again" : o.Tag;
                string text = Patterns.Has(dir, munin, tag) ? Patterns.Compose(dir, munin, tag, v, known) : null;
                if (text == null)
                {
                    List<string> whole = Usable(lines, o.Tag, v);
                    if (whole.Count > 0) text = whole[Rng.Next(whole.Count)];
                }
                if (text == null) continue;
                Memory.MarkTold(item, list, o.M);
                Remark r = new Remark();
                r.Tag = "memory";
                r.Title = Fill(text, v);
                return r;
            }
            return null;
        }

        // a memory's details as placeholders: {m_when} {m_where} {m_where_in} {m_who...} {m_cause} {m_hp} {m_count} {m_km} {m_m} {m_min} {m_times} {m_friend} {m_item}
        private static void Recall(Dictionary<string, string> v, Dictionary<string, string[]> names, Memory.Mem m)
        {
            v["m_when"] = Memory.When(m.Day);
            string[] land;
            if (Biomes.TryGetValue(m.Biome, out land)) { v["m_where"] = land[0]; v["m_where_in"] = land[1]; }
            if (m.Kind == "biome" || m.Kind == "dungeon")
            {
                if (Biomes.TryGetValue(m.Who, out land)) { v["m_where"] = land[0]; v["m_where_in"] = land[1]; }
            }
            else if (m.Kind == "boss")
            {
                int i = Array.IndexOf(Strike.Bosses, m.Who);
                if (i >= 0) { v["m_who"] = Strike.BossNames[i]; v["m_who_acc"] = Memory.BossAcc[i]; v["m_who_ins"] = Memory.BossIns[i]; v["m_who_gen"] = Memory.BossGen[i]; }
                if (m.Extra.Length > 0) v["m_friend"] = m.Extra;
                if (m.N >= 2) v["m_times"] = Times(m.N);
            }
            else if (m.Kind == "friend") v["m_friend"] = m.Who;
            else if (m.Kind == "metal") v["m_item"] = Lang.T(m.Who); // (kept in the source language, said in yours)
            else if ((m.Kind == "death" || m.Kind == "fdeath") && m.Extra == "x") v["m_cause"] = Memory.Cause(m.Who);
            else if (!string.IsNullOrEmpty(m.Who)) AddCreature(v, names, "m_who", m.Who);
            if ((m.Kind == "death" || m.Kind == "fdeath") && m.Extra != "x") AddCreature(v, names, "m_who", m.Who);
            switch (m.Kind)
            {
                case "near": v["m_hp"] = m.N.ToString(); break;
                case "recent":
                    if (m.Extra == "near") v["m_hp"] = m.N.ToString();
                    if (m.Extra == "hard") v["m_lost"] = m.N.ToString();
                    if (m.Extra == "star") v["m_stars"] = Lang.N(m.N, "зірка|зірки|зірок");
                    break;
                case "slaughter": v["m_count"] = Lang.N(m.N, "ворога|ворогів|ворогів"); break;
                case "voyage": v["m_km"] = Lang.F("{0} км", Lang.Num(m.N / 1000f, "0.0")); break;
                case "fall": v["m_m"] = Lang.F("{0} м", m.N); break;
                case "tomb": v["m_min"] = Lang.N(m.N, "хвилину|хвилини|хвилин"); break;
                case "grudge": case "badday": case "death": v["m_times"] = Times(m.N); break;
            }
        }

        // ---------------------------------------------------------------- someone else's raven

        // venom fitting the moment: a death, a scrape, a kill, or just being worn by the wrong one
        private static Remark Hostile(string dir, Player p, bool munin, Dictionary<string, string> values, List<Pick> picks)
        {
            Tone.Tier = 1;
            string owner = Trust.OwnerName(Trust.Worn(munin));
            if (!string.IsNullOrEmpty(owner)) values["owner"] = owner;
            string tag = "stranger_talk";
            foreach (Pick pick in picks)
            {
                if (pick.Tag.StartsWith("death")) { tag = "stranger_death"; break; }
                if (pick.Tag == "near_death" || pick.Tag == "see_hurt") { tag = "stranger_near_death"; break; }
                if (pick.Tag == "kill" || pick.Tag == "kill_many" || pick.Tag == "boss") { tag = "stranger_kill"; break; }
            }
            Func<string, bool> known = t => AllKnown(t, values);
            List<string> lines = ReadTagged(Path.Combine(dir, munin ? MuninQuipsFile : HuginQuipsFile), munin ? Quips.Munin : Quips.Hugin);
            foreach (string t in new string[] { tag, "stranger_talk", "stranger_peck" })
            {
                string text = Patterns.Has(dir, munin, t) ? Patterns.Compose(dir, munin, t, values, known) : null;
                if (text == null) { List<string> whole = Usable(lines, t, values); if (whole.Count > 0) text = whole[Rng.Next(whole.Count)]; }
                if (text == null) continue;
                Remark r = new Remark();
                r.Tag = t;
                r.Title = Fill(text, values);
                return r;
            }
            return null;
        }

        // how often a raven on the shoulder speaks of being there (the sh_ templates) rather than of the theme alone
        private const double ShoulderShare = 0.35;

        // a line written for a raven that flies in ("прилетів сказати"), not for one already on your shoulder
        private static bool Visiting(string text)
        {
            string low = text.ToLowerInvariant();
            foreach (string w in VisitWords) if (low.Contains(w)) return true;
            return false;
        }

        private static List<string> ForShoulder(List<string> lines, string tag, Dictionary<string, string> values)
        {
            List<string> result = new List<string>();
            foreach (string l in Usable(lines, tag, values))
            {
                string low = l.ToLowerInvariant();
                bool visit = false;
                foreach (string w in VisitWords) if (low.Contains(w)) { visit = true; break; }
                if (!visit) result.Add(l);
            }
            result.AddRange(Usable(lines, "sh_" + tag, values));
            return result;
        }

        internal static Remark ComposeThought(string dir, Player p, bool munin, List<string> recent, float windowSeconds) { using (Lang.Phrase()) return ComposeThoughtIn(dir, p, munin, recent, windowSeconds); } // (the line's values in its own language)

        private static Remark ComposeThoughtIn(string dir, Player p, bool munin, List<string> recent, float windowSeconds)
        {
            Tone.Use(munin);
            Diary.Summary s = Diary.Summarize(windowSeconds);
            Dictionary<string, string> values = Values(dir, p, s);
            List<Pick> picks = Rank(s);
            Sees(dir, p, picks, values);
            picks.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (Trust.Hostile(Trust.Worn(munin))) return Hostile(dir, p, munin, values, picks);
            Remark past = Remember(dir, p, munin, picks, values);
            if (past != null) return past;
            List<string> lines = ReadTagged(Path.Combine(dir, munin ? MuninQuipsFile : HuginQuipsFile), munin ? Quips.Munin : Quips.Hugin);
            Func<string, bool> known = t => AllKnown(t, values) && !Visiting(t); // it sits on you: no words of a raven that flew in

            List<string> tags = new List<string>();
            List<List<string>> whole = new List<List<string>>();
            foreach (Pick pick in picks)
            {
                if (recent != null && recent.Contains(pick.Tag) && pick.Score < 85) continue; // danger is worth repeating
                if (tags.Contains(pick.Tag)) continue;
                List<string> candidates = ForShoulder(lines, pick.Tag, values);
                if (candidates.Count == 0 && !Patterns.Has(dir, munin, pick.Tag)) continue;
                tags.Add(pick.Tag); whole.Add(candidates);
                if (tags.Count >= 3) break;
            }
            // one of the best three, the first most often; a theme that gives nothing right now drops out
            while (tags.Count > 0)
            {
                int roll = Rng.Next(100);
                int i = roll < 60 || tags.Count == 1 ? 0 : (roll < 85 || tags.Count == 2 ? 1 : 2);
                string text = null;
                bool assemble = Patterns.Has(dir, munin, tags[i]) && (whole[i].Count == 0 || Rng.NextDouble() < Patterns.Share);
                if (assemble) text = Patterns.Compose(dir, munin, tags[i], values, known);
                if (text == null && whole[i].Count > 0) text = whole[i][Rng.Next(whole[i].Count)];
                if (text == null) { tags.RemoveAt(i); whole.RemoveAt(i); continue; }
                text = Again(dir, munin, tags[i], text, values, known);
                Remark r = new Remark();
                r.Tag = tags[i];
                r.Title = Fill(text, values);
                return r;
            }
            return null;
        }

        private static void Sees(string dir, Player p, List<Pick> picks, Dictionary<string, string> values)
        {
            if (p == null) return;
            if (p.GetHealthPercentage() < 0.35f) picks.Add(new Pick("see_hurt", 95, 0f));
            Character nearest = null;
            float best = float.MaxValue;
            int count = 0;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed() || !BaseAI.IsEnemy(p, c)) continue;
                float d = Vector3.Distance(p.transform.position, c.transform.position);
                if (d > SeeRange) continue;
                count++;
                if (d < best) { best = d; nearest = c; }
            }
            if (nearest != null)
            {
                string token = (nearest.m_name ?? "").TrimStart('$');
                AddCreature(values, ReadNames(Path.Combine(dir, NamesFile)), "seen", token);
                if (!values.ContainsKey("seen")) values["seen"] = Localization.instance != null ? Lang.Game(nearest.m_name) : nearest.m_name;
                values["seen_enemy"] = values["seen"];
                values["seen_count"] = count.ToString();
                picks.Add(new Pick("see_enemy", 90, 0f));
            }
            Player friend = null;
            float fd = float.MaxValue;
            foreach (Player other in Player.GetAllPlayers())
            {
                if (other == null || other == p || other.IsDead()) continue;
                float d = Vector3.Distance(p.transform.position, other.transform.position);
                if (d <= SeeRange && d < fd) { fd = d; friend = other; }
            }
            if (friend != null) { values["friend"] = friend.GetPlayerName(); picks.Add(new Pick("see_friend", 40, 0f)); }
            if (EnvMan.instance != null && EnvMan.IsNight()) picks.Add(new Pick("see_night", 32, 0f));
            if (values.ContainsKey("biome_here_in")) picks.Add(new Pick("see_place", 20, 0f));
        }
#endif

        // how eventful the last minutes were: the more notable things, the sooner a quick raven may come again
        internal static int Intensity(float windowSeconds)
        {
            Diary.Summary s = Diary.Summarize(windowSeconds);
            int n = 0;
            if (!string.IsNullOrEmpty(s.Boss)) n += 2;
            n += Mathf.Min(s.N(Ev.Death), 3);
            n += Mathf.Min(s.N(Ev.NearDeath), 2);
            if (s.Level > 0) n++;
            if (s.N(Ev.Friendly) > 0) n++;
            if (s.N(Ev.Kill) >= 8) n++;
            if (s.N(Ev.Escaped) > 0) n++;
            if (s.N(Ev.Flee) > 0) n++;
            if (!string.IsNullOrEmpty(s.NewBiome)) n++;
            if (s.N(Ev.Dungeon) > 0) n++;
            if (s.N(Ev.Raid) > 0) n += 2;
            return n;
        }

        // values for the next tagged line only (a strike's target...), cleared by whoever sets them
        internal static readonly Dictionary<string, string> Extra = new Dictionary<string, string>();

        // a line of one exact kind for the worn ravens; the numbers come from the character's statistics
        internal static string ComposeTag(string dir, Player p, bool munin, string tag) { using (Lang.Phrase()) return ComposeTagIn(dir, p, munin, tag); } // (the line's values in its own language)
        private static string ComposeTagIn(string dir, Player p, bool munin, string tag)
        {
            Diary.Summary s = Diary.Summarize(Diary.WindowSeconds);
            Dictionary<string, string> values = Values(dir, p, s);
            foreach (KeyValuePair<string, string> kv in Extra) values[kv.Key] = kv.Value;
            List<string> lines = ReadTagged(Path.Combine(dir, munin ? MuninQuipsFile : HuginQuipsFile), munin ? Quips.Munin : Quips.Hugin);
            Tone.Use(munin);
#if RAVEN_ITEM
            // someone else's raven gives the gift all the same, but grudgingly: its own lines, {gift} and {owner} in them
            if (tag != "stranger_peck" && tag != "asleep_talk" && Trust.Hostile(Trust.Worn(munin)))
            {
                string owner = Trust.OwnerName(Trust.Worn(munin));
                if (!string.IsNullOrEmpty(owner)) values["owner"] = owner;
                foreach (RavenAbility a in RavenAbilities.All)
                    if (tag == a.Id || tag.StartsWith(a.Id + "_")) { values["gift"] = a.Plain; break; }
                tag = "stranger_gift";
            }
#endif
            List<string> candidates = Usable(lines, tag, values);
#if RAVEN_ITEM
            if (Patterns.Has(dir, munin, tag) && (candidates.Count == 0 || Rng.NextDouble() < Patterns.Share))
            {
                string made = Patterns.Compose(dir, munin, tag, values, t => AllKnown(t, values));
                if (made != null) return Fill(Again(dir, munin, tag, made, values, t => AllKnown(t, values)), values);
            }
#endif
            // not the same line twice in a row
            string last;
            if (candidates.Count > 1 && LastTagged.TryGetValue(tag, out last)) candidates.Remove(last);
            if (candidates.Count == 0) return null;
            string pick = candidates[Rng.Next(candidates.Count)];
            LastTagged[tag] = pick;
            return Fill(Again(dir, munin, tag, munin ? Gutter.Dirty(pick, Tone.Tier, Beast.On() && Tone.Own) : pick, values, t => AllKnown(t, values)), values); // (a whole line: Munin's street mouth too)
        }

        private static readonly Dictionary<string, string> LastTagged = new Dictionary<string, string>();

        // ---------------------------------------------------------------- the raven notices it says the same again
        // Each theme said is remembered for AgainMinutes. Said again within that: now and then a line of its own that
        // knows it ("<theme>_again", {again_n}: вдруге, втретє…); for a theme without such lines, from the third time
        // on, now and then a lead-in ("again_lead": «Ну от, знову.») before the usual line.
        internal static float AgainMinutes = 15f;
        internal static double AgainChance = 0.6, AgainLeadChance = 0.3;
        private static readonly Dictionary<string, List<float>> SaidAt = new Dictionary<string, List<float>>();
        private static readonly string[] AgainLeadTags =
        {
            "chop", "mine", "build", "kill", "kill_many", "fish", "pick", "loot", "craft", "cook", "portal", "fall", "swim", "sail",
            "encumbered", "hungry", "hungry_ask", "fed", "plant", "smelt", "ferment", "repair", "sit", "trade", "potion", "trophy",
            "trophy_hang", "cart", "ride", "bow", "magic", "parry", "dodge", "raid", "death",
            "portal_ore", "broke", "freezing", "poisoned", "burning",
        };

        // all the deaths are one theme here, and a shoulder's own words count as their theme
        private static string AgainKey(string tag)
        {
            if (tag.StartsWith("death")) return "death";
            if (tag.StartsWith("sh_")) return tag.Substring(3);
            return tag;
        }

        // how many times this theme was said lately; it is said once more now
        private static int SaidLately(string key)
        {
            List<float> l;
            if (!SaidAt.TryGetValue(key, out l)) { l = new List<float>(); SaidAt[key] = l; }
            float from = Time.time - AgainMinutes * 60f;
            l.RemoveAll(t => t < from);
            int n = l.Count;
            l.Add(Time.time);
            return n;
        }

        private static string AgainOrdinal(int n)
        {
            switch (n)
            {
                case 2: return Lang.T("вдруге");
                case 3: return Lang.T("втретє");
                case 4: return Lang.T("вчетверте");
                case 5: return Lang.T("вп'яте");
                case 6: return Lang.T("вшосте");
                case 7: return Lang.T("всьоме");
                default: return Lang.T("вкотре");
            }
        }

        private static string Capital(string s) { return s.Length > 0 && char.IsLower(s[0]) ? Lang.UpperFirst(s) : s; }

        // the line chosen for a theme, or one that knows the theme came up again
        private static string Again(string dir, bool munin, string tag, string text, Dictionary<string, string> values, Func<string, bool> known)
        {
#if RAVEN_ITEM
            if (text == null || string.IsNullOrEmpty(tag)) return text;
            string key = AgainKey(tag);
            int n = SaidLately(key);
            if (n == 0) return text;
            values["again_n"] = AgainOrdinal(n + 1);
            string again = key + "_again";
            if (Patterns.Has(dir, munin, again) && Rng.NextDouble() < AgainChance)
            {
                string made = Patterns.Compose(dir, munin, again, values, known);
                if (made != null) return made;
            }
            if (n >= 2 && Array.IndexOf(AgainLeadTags, key) >= 0 && Patterns.Has(dir, munin, "again_lead") && Rng.NextDouble() < AgainLeadChance)
            {
                string lead = Patterns.Compose(dir, munin, "again_lead", values, known);
                if (lead != null) return lead + " " + Capital(text);
            }
#endif
            return text;
        }

#if RAVEN_ITEM
        // A raven on a perch (PerchSpeech): its words put together without its owner being here — the tone from its
        // trust level, the owner's name and {m|f} forms from the perch's own record, the rest from 'extra'.
        internal static string ComposePerch(string dir, bool munin, string tag, int level, string name, bool female, Dictionary<string, string> extra, int voice = -1) { using (Lang.Phrase()) return ComposePerchIn(dir, munin, tag, level, name, female, extra, voice); } // (the line's values in its own language)
        private static string ComposePerchIn(string dir, bool munin, string tag, int level, string name, bool female, Dictionary<string, string> extra, int voice)
        {
            if (string.IsNullOrEmpty(dir) || !Patterns.Has(dir, munin, tag)) return null;
            Dictionary<string, string> values = new Dictionary<string, string>();
            values["name"] = name ?? "";
            if (extra != null) foreach (KeyValuePair<string, string> kv in extra) values[kv.Key] = kv.Value;
            int was = Tone.Tier;
            bool wasOwn = Tone.Own;
            Tone.Tier = Tone.TierOf(level);
            Tone.Own = false;
            Manner.Use(voice, munin, true); // the perch's raven in its own manner
            try
            {
                string made = Patterns.Compose(dir, munin, tag, values, t => AllKnown(t, values));
                return made != null ? Fill(Grammar.Fix(made, female), values) : null; // the owner's forms first (Fill would take the holder's)
            }
            finally { Tone.Tier = was; Tone.Own = wasOwn; Manner.Clear(); }
        }

        // a raven speaking to another player's raven (CrossTalk): {a|b} are its own owner's forms, {>a|b} the forms of the
        // owner of the raven it speaks to; the tone of its own trust
        private static readonly System.Text.RegularExpressions.Regex OtherForms = new System.Text.RegularExpressions.Regex(@"\{>([^{}|]*)\|([^{}|]*)\}");
        internal static string ComposeCross(string dir, bool munin, string tag, int level, bool female, bool otherFemale, Dictionary<string, string> values, int voice = -1, bool address = false) { using (Lang.Phrase()) return ComposeCrossIn(dir, munin, tag, level, female, otherFemale, values, voice, address); } // (the line's values in its own language)
        private static string ComposeCrossIn(string dir, bool munin, string tag, int level, bool female, bool otherFemale, Dictionary<string, string> values, int voice, bool address)
        {
            if (string.IsNullOrEmpty(dir) || !Patterns.Has(dir, munin, tag)) return null;
            int was = Tone.Tier;
            bool wasOwn = Tone.Own;
            Tone.Tier = Tone.TierOf(level);
            Tone.Own = false;
            Manner.Use(voice, munin, address); // the speaking raven's own manner (address: it may call its own owner by its word)
            try
            {
                string made = Patterns.Compose(dir, munin, tag, values, t => AllKnown(t, values));
                if (made == null) return null;
                made = OtherForms.Replace(made, m => otherFemale ? m.Groups[2].Value : m.Groups[1].Value);
                return Fill(Grammar.Fix(made, female), values);
            }
            finally { Tone.Tier = was; Tone.Own = wasOwn; Manner.Clear(); }
        }
#endif

        // the reactive mode: did something worth a visit happen, and is it over (not mid-fight)?
        internal static bool ReactiveCandidate(float windowSeconds, int minScore, float minAgeSeconds, out string tag)
        {
            tag = null;
            foreach (Pick pick in Rank(Diary.Summarize(windowSeconds)))
            {
                if (pick.Tag == "idle" || pick.Tag == "afk") continue;
#if RAVEN_ITEM
                if (RavenTalk.Forked(pick.Tag)) continue; // a moment: it got (or did not get) its word when it happened
#endif
                if (pick.Score < minScore) return false;
                if (pick.Age < minAgeSeconds || pick.Age > windowSeconds) return false;
                tag = pick.Tag;
                return true;
            }
            return false;
        }

        // how often a visit's letter is assembled rather than one of the old whole texts, when the theme has both
        internal static double VisitAssembled = 0.65;

        internal static Remark Compose(string dir, Player p, string lastTag) { return Compose(dir, p, lastTag, Diary.WindowSeconds); }

        internal static Remark Compose(string dir, Player p, string lastTag, float windowSeconds)
        {
            Tone.Use(false);
            Diary.Summary s = Diary.Summarize(windowSeconds);
            Dictionary<string, string> values = Values(dir, p, s);
            List<string> titles = ReadTagged(Path.Combine(dir, TitlesFile), DefaultsEvents.Titles);
            List<string> texts = ReadTagged(Path.Combine(dir, TextsFile), DefaultsEvents.Texts);

            foreach (Pick pick in Rank(s))
            {
                // a fresh topic is better than the same joke twice, unless something big happened
                if (pick.Tag == lastTag && pick.Score < 80 && pick.Tag != "idle") continue;
                List<string> candidates = Usable(texts, pick.Tag, values);
                string made = null;
#if RAVEN_ITEM
                // the letter too is put together from parts most of the time (no words of a raven sitting on you: he flew in)
                if (Patterns.Has(dir, false, pick.Tag) && (candidates.Count == 0 || Rng.NextDouble() < VisitAssembled))
                    made = Patterns.Compose(dir, false, pick.Tag, values, x => AllKnown(x, values) && !ShoulderOnly(x));
#endif
                if (made == null && candidates.Count == 0) continue;

                Remark r = new Remark();
                r.Tag = pick.Tag;
                r.Text = Fill(made ?? candidates[Rng.Next(candidates.Count)], values);
                List<string> t = Usable(titles, pick.Tag, values);
                r.Title = t.Count > 0 ? Fill(t[Rng.Next(t.Count)], values) : null;
                if (made == null && pick.Tag != "idle" && Rng.Next(100) < 55) // an assembled line has its own ending
                {
                    List<string> tails = ReadPlain(Path.Combine(dir, TailsFile), DefaultsEvents.Tails);
                    if (tails.Count > 0) r.Text += " " + tails[Rng.Next(tails.Count)];
                }
                return r;
            }
            return null;
        }

        private static Dictionary<string, string> Values(string dir, Player p, Diary.Summary s)
        {
            Dictionary<string, string> v = new Dictionary<string, string>();
            Dictionary<string, string[]> names = ReadNames(Path.Combine(dir, NamesFile));
            v["name"] = p != null ? p.GetPlayerName() : "";
            AddCreature(v, names, "killer", s.LastKiller);
            AddCreature(v, names, "attacker", s.NearDeathBy);
            AddCreature(v, names, "boss", s.Boss);
            AddCreature(v, names, "top", s.TopKill);
            AddCreature(v, names, "prey", s.Escaped);
            AddCreature(v, names, "foe", s.Fled);
            int kills = s.N(Ev.Kill), builds = s.N(Ev.Build), fish = s.N(Ev.Fish);
            if (kills > 0) v["count"] = kills.ToString();
            if (builds > 0 && kills == 0) v["count"] = builds.ToString();
            if (fish > 0) v["fish"] = fish.ToString();
            if (s.N(Ev.Death) > 0) v["times"] = Times(s.N(Ev.Death));
            if (s.Level > 0) v["level"] = s.Level.ToString();
            if (s.Meters > 0) v["km"] = Lang.F("{0} км", Lang.Num(s.Meters / 1000f, "0.0"));
            string[] here;
            if (p != null && Biomes.TryGetValue(p.GetCurrentBiome().ToString(), out here))
            {
                v["biome_here"] = here[0];
                v["biome_here_in"] = here[1];
            }
            string[] biome;
            if (!string.IsNullOrEmpty(s.NewBiome) && Biomes.TryGetValue(s.NewBiome, out biome))
            {
                v["biome"] = biome[0];
                v["biome_in"] = biome[1];
            }
            AddCreature(v, names, "mount", s.Mount);
            AddCreature(v, names, "tamed", s.Tamed);
            if (!string.IsNullOrEmpty(s.Broke) && !s.Broke.StartsWith("[")) v["broke"] = s.Broke;
            string on;
            if (!string.IsNullOrEmpty(s.Mount) && MountOn.TryGetValue(s.Mount, out on)) v["mount_on"] = on;
            Trader(v, s.Trader);
            try { Stats.Fill(v, s); }
            catch { } // the profile is not ready: lines without numbers still work
            return v;
        }

        // "on whom" for a ride ({mount_on}): the locative the creature names file does not hold; a modded mount
        // without it is simply not named that way (the lines that need it give way to others)
        private static Dictionary<string, string> MountOn { get { return Lang.Map(MountOnSrc); } } // (translated per language)
        private static readonly Dictionary<string, string> MountOnSrc = new Dictionary<string, string>
        {
            { "enemy_lox", Lang.K("на бикоящурі") }, { "enemy_asksvin", Lang.K("на попелозаврі") }, { "enemy_wolf", Lang.K("на вовкові") },
            { "enemy_boar", Lang.K("на кабані") }, { "enemy_deer", Lang.K("на олені") }, { "enemy_hen", Lang.K("на курці") },
        };

        // the trader you spoke with: {trader} {trader_gen} {trader_ins}; an unknown one only by its name
        private static void Trader(Dictionary<string, string> v, string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            string t = token.ToLowerInvariant();
            string[] f = null;
            if (t.Contains("haldor")) f = new[] { Lang.T("Хальдор"), Lang.T("Хальдора"), Lang.T("Хальдором") };
            else if (t.Contains("hildir")) f = new[] { Lang.T("Хільдір"), Lang.T("Хільдір"), Lang.T("Хільдір") };
            else if (t.Contains("bogwitch")) f = new[] { Lang.T("Болотяна відьма"), Lang.T("Болотяної відьми"), Lang.T("Болотяною відьмою") };
            if (f != null) { v["trader"] = f[0]; v["trader_gen"] = f[1]; v["trader_ins"] = f[2]; return; }
            string local = Localization.instance != null ? Lang.Game(token) : token;
            if (!string.IsNullOrEmpty(local) && !local.StartsWith("[")) v["trader"] = local;
        }

        // a land's name and "in it" ({key} {key_in}): the talks name where you came from
        internal static void BiomeForms(Dictionary<string, string> v, string key, string token)
        {
            string[] f;
            if (token != null && Biomes.TryGetValue(token, out f)) { v[key] = f[0]; v[key + "_in"] = f[1]; }
        }

        // a creature's name in its cases from creature_names.txt (the talks put the boss in them)
        internal static void CreatureForms(string dir, Dictionary<string, string> v, string key, string token)
        {
            AddCreature(v, ReadNames(Path.Combine(dir, NamesFile)), key, token != null && token.StartsWith("$") ? token.Substring(1) : token);
        }

        // only placeholders we really know get a value; phrases needing missing ones are skipped
        private static void AddCreature(Dictionary<string, string> v, Dictionary<string, string[]> names, string key, string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            string[] forms;
            if (names.TryGetValue(token, out forms) && forms.Length >= 4)
            {
                v[key] = forms[0];
                v[key + "_gen"] = forms[1];
                v[key + "_ins"] = forms[2];
                v[key + "_genpl"] = forms[3];
                return;
            }
            string local = Localization.instance != null ? Lang.Game("$" + token) : "";
            if (string.IsNullOrEmpty(local) || local.StartsWith("[")) return;
            v[key] = local;
            if (!Lang.Cases) { v[key + "_gen"] = local; v[key + "_ins"] = local; } // (no cases: the name as it is; the plural stays unknown)
        }

        internal static string Times(int n) { return Lang.N(n, "раз|рази|разів"); }

        // a land's name as the ravens say it ("Чорнолісся"; at: "у Чорноліссі")
        internal static string BiomeName(Heightmap.Biome b, bool at)
        {
            string[] n;
            return Biomes.TryGetValue(b.ToString(), out n) ? n[at ? 1 : 0] : b.ToString();
        }

        private static Dictionary<string, string[]> Biomes { get { return Lang.Map(BiomesSrc); } } // (translated per language)
        private static readonly Dictionary<string, string[]> BiomesSrc = new Dictionary<string, string[]>
        {
            { "Meadows", Lang.Keys("Левади", "на Левадах") },
            { "BlackForest", Lang.Keys("Чорнолісся", "у Чорноліссі") },
            { "Swamp", Lang.Keys("Болото", "на Болоті") },
            { "Mountain", Lang.Keys("Гора", "у Горах") },
            { "Plains", Lang.Keys("Рівнини", "на Рівнинах") },
            { "Mistlands", Lang.Keys("Імлисті землі", "в Імлистих землях") },
            { "AshLands", Lang.Keys("Попелисті землі", "у Попелистих землях") },
            { "DeepNorth", Lang.Keys("Далека північ", "на Далекій півночі") },
            { "Ocean", Lang.Keys("Океан", "в Океані") },
        };

        // ---------------------------------------------------------------- files

        private static List<string> Usable(List<string> lines, string tag, Dictionary<string, string> values)
        {
            List<string> result = new List<string>();
            string prefix = "[" + tag + "]";
            foreach (string line in lines)
            {
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string body = line.Substring(prefix.Length).Trim();
                if (!Tone.Take(ref body)) continue; // a line for another trust tier
                if (body.Length > 0 && AllKnown(body, values)) result.Add(body);
            }
            return result;
        }

        private static bool AllKnown(string text, Dictionary<string, string> values)
        {
            int i = 0;
            while ((i = text.IndexOf('{', i)) >= 0)
            {
                int j = text.IndexOf('}', i);
                if (j < 0) break;
                string token = text.Substring(i + 1, j - i - 1);
                if (!Grammar.IsForms(token) && !values.ContainsKey(token)) return false;
                i = j + 1;
            }
            return true;
        }

        private static string Fill(string text, Dictionary<string, string> values)
        {
            foreach (KeyValuePair<string, string> kv in values) text = text.Replace("{" + kv.Key + "}", kv.Value);
            text = Grammar.Fix(text); // {male|female}: the player's own gender
            if (text.Length > 0) text = Lang.UpperFirst(text);
            return text.Replace("\\n", "\n");
        }

        private static List<string> ReadTagged(string path, string[] fallback)
        {
            List<string> lines = ReadPlain(path, null);
            if (lines.Count == 0 && fallback != null) lines.AddRange(fallback);
            return lines;
        }

        // a file is read again only when it was saved since (the player may edit it during the game)
        private static readonly Dictionary<string, KeyValuePair<DateTime, List<string>>> Files = new Dictionary<string, KeyValuePair<DateTime, List<string>>>();

        // the file's date is looked at no more than once in CheckSeconds (many lines are put together in a moment)
        private const float CheckSeconds = 2f;
        private static readonly Dictionary<string, float> CheckedAt = new Dictionary<string, float>();

        // the file's lines as kept (not a copy: do not change it), or null when there is no file
        private static List<string> Kept(string path)
        {
            KeyValuePair<DateTime, List<string>> kept;
            float now = Time.realtimeSinceStartup, at;
            if (Files.TryGetValue(path, out kept) && CheckedAt.TryGetValue(path, out at) && now - at < CheckSeconds) return kept.Value;
            CheckedAt[path] = now;
            if (!File.Exists(path)) { Files.Remove(path); return null; }
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            if (!Files.TryGetValue(path, out kept) || kept.Key != stamp)
            {
                List<string> lines = new List<string>();
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    lines.Add(line);
                }
                kept = new KeyValuePair<DateTime, List<string>>(stamp, lines);
                Files[path] = kept;
            }
            return kept.Value;
        }

        private static List<string> ReadPlain(string path, string[] fallback)
        {
            List<string> result = new List<string>();
            List<string> kept = Kept(path);
            if (kept != null) result.AddRange(kept); // a copy: callers add to it
            if (result.Count == 0 && fallback != null) result.AddRange(Lang.Lines(Path.GetFileName(path), fallback));
            return result;
        }

        // made again only when the file changed (the kept lines are a new list then); read only by the callers
        private static object _namesFrom;
        private static Dictionary<string, string[]> _names;

        private static Dictionary<string, string[]> ReadNames(string path)
        {
            List<string> kept = Kept(path);
            object from = kept != null && kept.Count > 0 ? (object)kept : DefaultsEvents.CreatureNames;
            if (_names != null && ReferenceEquals(from, _namesFrom)) return _names;
            Dictionary<string, string[]> map = new Dictionary<string, string[]>();
            List<string> lines = ReadPlain(path, DefaultsEvents.CreatureNames);
            foreach (string line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().TrimStart('$');
                string[] forms = line.Substring(eq + 1).Split('|');
                for (int i = 0; i < forms.Length; i++) forms[i] = forms[i].Trim();
                map[key] = forms;
            }
            _namesFrom = from;
            _names = map;
            return map;
        }
    }
}
