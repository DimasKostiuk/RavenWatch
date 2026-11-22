#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Hugin's «Віщування» (passive): some mornings (as the morning comes, whether you slept or not) Hugin tells what the
    // day holds — the raid fated for it (OmenFate: the server's roll made ahead, so it is true) and the weather where you
    // are (the game's weather is fixed ahead by the world's clock, so that is true too). A raid foretold comes with a
    // card: turn it aside a day (Hugin's strength, once in a few days; it comes back grown) or call it now, here, when
    // you are ready for its trophies. Now and then the morning brings a bad omen: die before the next morning and your
    // ravens lose less trust. At sea Hugin also warns of a wind about to turn and of a serpent in these waters.
    // A morning he speaks may bring the day's gift too (BoonChance): +BoonAmount health, stamina or eitr till the next
    // morning (kept in the character, so a relog keeps it; added to what the food gives: OmenBoonPatch).
    internal static class Omen
    {
        internal static bool Enabled = true;
        internal static int UnlockLevel = 5;
        internal static float ChanceFirst = 30f, ChanceTop = 40f;   // % of mornings he speaks: at the gift's level, at the top (2.8.1: 40→80 was nearly every day)
        internal static float BoonChance = 50f, BoonAmount = 20f;  // the day's gift on a morning he speaks: how often, how much
        internal static float BadChance = 20f, BadDeathMul = 0.5f;  // «Недобрий знак»: how often, the trust a death takes
        internal static float DeflectCost = 40f, DeflectDays = 3f;  // turning a raid aside: Hugin's strength, once in so many days
        internal static float CardSeconds = 45f;
        internal static bool AtSea = true;                          // the wind and the serpent
        internal static float SerpentRange = 150f;

        private const string DayKey = "RavenWatch_omenDay";     // the character: "<world>:<day>" of the last morning looked at
        private const string BadKey = "RavenWatch_badOmen";     // the character: the bad omen lasts till then (world time)
        private const string TurnKey = "RavenWatch_omenTurned"; // the character: the next raid may be turned aside then
        internal const string BadName = "RavenWatch_BadOmen";
        private const string BoonKey = "RavenWatch_omenBoon";      // the character: the day's gift lasts till then (world time)
        private const string BoonKindKey = "RavenWatch_omenBoonKind"; // the character: 0 health, 1 stamina, 2 eitr
        internal const string BoonName = "RavenWatch_OmenBoon";
        private static SE_Stats _boon;
        private static float _boonSayAt = -1f, _boonLookAt;
        private static int _boonSay = -1;
        private static SE_Stats _bad;
        private static float _nextLook, _awakeSince = -1f, _badSayAt = -1f, _nextSea, _windSaidFor = -1f, _seaSaidAt = -100f;
        private static readonly HashSet<ZDOID> Serpents = new HashSet<ZDOID>();

        internal static bool Is(RavenAbility a) { return a != null && a.Id == "homen"; }

        internal static float ChanceNow() { return Trust.LerpFrom(false, UnlockLevel, ChanceFirst, ChanceTop); }

        internal static string ReadyText()
        {
            if (!Enabled) return Lang.T("<color=#9a9a9a>вимкнено в налаштуваннях</color>");
            Player p = Player.m_localPlayer;
            string s = Lang.F("<color=#FFD54F><b>уранці з шансом {0}%</b></color>", RavenAbilities.R(ChanceNow()));
            float turn = p != null ? WorldTime.Left(p.m_customData, TurnKey) : 0f;
            if (turn > 0f) s += Lang.F(" · <color=#FF5A5A>відвести набіг — за {0}</color>", RavenAbilities.Clock(turn));
            return s;
        }

        // Hugin awake on the shoulder, trusting enough
        private static bool Ready(Player p)
        {
            if (!Enabled || p == null || p != Player.m_localPlayer || p.IsDead()) return false;
            ItemDrop.ItemData h = Trust.Worn(false);
            if (RavenSpeech.Hushed()) return false; // (a scene plays: the morning's word waits for its end — said into the quiet it would be lost, the morning used up)
            return h != null && Trust.Active(h) && !Trust.Away(h) && !Trust.Stranger(h, p) && Trust.SettledLevel(h) >= UnlockLevel && Shoulder.Arrived(p, false);
        }

        // ---------------------------------------------------------------- the morning

        internal static void Tick(Player p)
        {
            OmenFate.Tick();
            OmenFate.TickGrown();
            if (p == null || p != Player.m_localPlayer) return;
            if (_badSayAt > 0f && Time.time >= _badSayAt) { _badSayAt = -1f; Say(p, "omen_bad"); }
            if (_boonSayAt > 0f && Time.time >= _boonSayAt) { _boonSayAt = -1f; Say(p, BoonTag(_boonSay)); }
            if (Time.time >= _boonLookAt) { _boonLookAt = Time.time + 5f; KeepBoonIcon(p); }
            if (Time.time < _nextLook) return;
            _nextLook = Time.time + 1f;
            Sea(p);
            EnvMan em = EnvMan.instance;
            if (em == null || ZNet.instance == null || p.IsDead() || p.InBed() || em.IsTimeSkipping()) { _awakeSince = -1f; return; }
            if (_awakeSince < 0f) { _awakeSince = Time.time; return; }
            if (Time.time - _awakeSince < 6f) return; // (just up: the morning first)
            double now = ZNet.instance.GetTimeSeconds();
            float frac = (float)(now % em.m_dayLengthSec) / em.m_dayLengthSec;
            if (frac < 0.15f || frac > 0.4f) return; // the morning: from sunrise until a while after
            string today = ZNet.instance.GetWorldUID() + ":" + em.GetDay();
            string seen;
            if (p.m_customData.TryGetValue(DayKey, out seen) && seen == today) return;
            if (!Ready(p)) return; // (Hugin not here yet: the morning waits for him)
            p.m_customData[DayKey] = today; // (once a morning, spoken or not)
            if (UnityEngine.Random.Range(0f, 100f) >= ChanceNow()) { Keep(p, TodayQuiet); Log("morning: Hugin keeps quiet"); return; }
            bool bad = UnityEngine.Random.Range(0f, 100f) < BadChance;
            OmenFate.Ask(a => Speak(p, a, bad));
        }

        // the morning's word: the raid (with the card), else the weather, else a calm day; the bad omen a moment later
        private static void Speak(Player p, OmenFate.Answer a, bool bad)
        {
            if (p == null || p != Player.m_localPlayer || !Ready(p)) return;
            string land, weather = Forecast(p, out land);
            string sky = weather != null ? Lang.F("{0} {1}", land, weather) : null; // «на Луках зранку ясно, вдень дощ»
            bool raid = a != null && a.State == OmenFate.Raid, calm = a != null && a.State == OmenFate.Calm;
            if (!raid && !calm && sky == null && !bad) { Keep(p, TodayNothing); Log("morning: nothing to foretell here"); return; }
            string shownRaid = null;
            if (raid) RaidToken(a.Event, out shownRaid);
            Keep(p, TodaySaid, shownRaid, raid ? ZNet.instance.GetTimeSeconds() + a.Left : 0.0, sky, calm, bad);
            ScreamFx.CawAll(p, false, 1.25f, 0.6f);
            Nicks.OnOmen(p); // a deed for Hugin's nickname
            if (bad) Bad(p);
            if (UnityEngine.Random.Range(0f, 100f) < BoonChance) Boon(p, bad ? 18f : 9f);
            if (raid)
            {
                string when = When(a.Left), shown;
                string token = RaidToken(a.Event, out shown);
                string line = Line(p, a.Grown ? "omen_grown" : "omen_raid", "when", when, "raid", token);
                if (line != null) RavenSkills.SayFromShoulder(p, false, line);
                RavenBook.ShowOmen(a, line, shown, when, sky, bad);
                Log("morning: raid " + a.Event + (a.Grown ? " (grown)" : "") + " in " + Mathf.RoundToInt(a.Left) + " s");
                return;
            }
            string l = weather != null ? Line(p, "omen_weather", "land", land, "weather", weather) : null;
            if (l == null && calm) l = Line(p, "omen_calm");
            if (l != null) RavenSkills.SayFromShoulder(p, false, l);
            StringBuilder sb = new StringBuilder(Lang.T("Віщування Хугіна"));
            if (sky != null) sb.Append('\n').Append(Lang.F("Небо: {0}.", sky));
            if (calm) sb.Append('\n').Append(Lang.T("Набігу до наступного ранку не буде."));
            if (bad) sb.Append('\n').Append(Lang.T("Недобрий знак: загинеш до ранку — ворони втратять менше довіри."));
            RavenMsg.Center(p, sb.ToString());
            Log("morning: " + (a == null ? "no word from the server" : a.State == OmenFate.Calm ? "calm" : a.State == OmenFate.Now ? "a raid is on" : "raids off") + (sky != null ? ", weather " + sky : ""));
        }

        // ---------------------------------------------------------------- the day's word, kept for the book

        // what Hugin foretold this morning (or that he kept quiet), in the character, for the gift's tooltip: a message on
        // the screen is soon gone, often under another one (the day's number)
        private const string TodayKey = "RavenWatch_omenToday"; // "<world>:<day>" US state US raid US raid's world time US sky US calm US bad
        private const string TodayQuiet = "quiet", TodayNothing = "nothing", TodaySaid = "said";
        private const char Sep = '\u001f';

        private static string Today()
        {
            EnvMan em = EnvMan.instance;
            return em != null && ZNet.instance != null ? ZNet.instance.GetWorldUID() + ":" + em.GetDay() : null;
        }

        private static void Keep(Player p, string state, string raid = null, double raidAt = 0.0, string sky = null, bool calm = false, bool bad = false)
        {
            string today = Today();
            if (p == null || today == null) return;
            p.m_customData[TodayKey] = string.Join(Sep.ToString(), new string[] { today, state, raid ?? "", raidAt.ToString("R", CultureInfo.InvariantCulture), sky ?? "", calm ? "1" : "0", bad ? "1" : "0" });
        }

        internal static string TodayText(Player p)
        {
            string today = Today(), v;
            if (p == null || today == null) return "";
            string head = Lang.F("<color=#FFD54F><b>{0}</b></color>", Lang.T("Сьогоднішнє віщування"));
            string[] f = p.m_customData.TryGetValue(TodayKey, out v) ? v.Split(Sep) : null;
            if (f == null || f.Length < 7 || f[0] != today)
            {
                string seen;
                bool looked = p.m_customData.TryGetValue(DayKey, out seen) && seen == today;
                EnvMan em = EnvMan.instance;
                float frac = (float)(ZNet.instance.GetTimeSeconds() % em.m_dayLengthSec) / em.m_dayLengthSec;
                return head + "\n" + (!looked && frac <= 0.4f ? Lang.T("Ще попереду: Хугін віщує вранці.") : Lang.T("Сьогодні його не було."));
            }
            if (f[1] == TodayQuiet) return head + "\n" + Lang.T("Хугін сьогодні змовчав.");
            if (f[1] == TodayNothing) return head + "\n" + Lang.T("Хугіну сьогодні нічого віщувати.");
            StringBuilder sb = new StringBuilder(head);
            double at;
            if (f[2].Length > 0 && double.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out at))
            {
                float left = (float)(at - ZNet.instance.GetTimeSeconds());
                sb.Append('\n').Append(left > 0f ? Lang.F("Набіг: {0} — {1}.", f[2], When(left)) : Lang.F("Набіг: {0} — його час уже настав.", f[2]));
            }
            if (f[4].Length > 0) sb.Append('\n').Append(Lang.F("Небо: {0}.", f[4]));
            if (f[5] == "1") sb.Append('\n').Append(Lang.T("Набігу до наступного ранку не буде."));
            if (f[6] == "1" && WorldTime.LeftHere(p.m_customData, BadKey) > 0f) sb.Append('\n').Append(Lang.T("Недобрий знак: загинеш до ранку — ворони втратять менше довіри."));
            if (WorldTime.LeftHere(p.m_customData, BoonKey) > 0f) sb.Append('\n').Append(Lang.F("Дар Хугіна до ранку: +{0} {1}", RavenAbilities.R(BoonAmount), BoonWhat(BoonKind(p))));
            return sb.ToString();
        }

        // ---------------------------------------------------------------- the bad omen

        private static void Bad(Player p)
        {
            EnvMan em = EnvMan.instance;
            if (em == null) return;
            double next = em.GetMorningStartSec(em.GetDay() + 1);
            float secs = Mathf.Max(60f, (float)(next - ZNet.instance.GetTimeSeconds()));
            WorldTime.SetHere(p.m_customData, BadKey, secs); // (kept with its world: carried to a world whose clock is behind, it does not last for hours)
            ShowBad(p, secs);
            _badSayAt = Time.time + 9f;
        }

        private static void ShowBad(Player p, float secs)
        {
            SE_Stats se = RavenFx.Template(ref _bad, BadName);
            se.m_name = Lang.W("Недобрий знак");
            se.m_tooltip = Lang.T("Хугін бачив недобрий знак. Загинеш до наступного ранку — ворони втратять менше довіри.");
            se.m_icon = AbilityIcons.ForEffect("homen", false);
            se.m_ttl = secs;
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        // ---------------------------------------------------------------- the day's gift

        private static readonly string[] BoonTags = { "omen_boon_hp", "omen_boon_stamina", "omen_boon_eitr" };
        private static string BoonTag(int kind) { return BoonTags[Mathf.Clamp(kind, 0, 2)]; }
        private static string BoonWhat(int kind) { return kind == 1 ? Lang.T("витривалості") : kind == 2 ? Lang.T("ейтиру") : Lang.T("здоров'я"); }

        // one of the three, till the next morning; Hugin says it a moment after the morning's word
        private static void Boon(Player p, float sayAfter)
        {
            EnvMan em = EnvMan.instance;
            if (em == null || ZNet.instance == null) return;
            double next = em.GetMorningStartSec(em.GetDay() + 1);
            float secs = Mathf.Max(60f, (float)(next - ZNet.instance.GetTimeSeconds()));
            int kind = UnityEngine.Random.Range(0, 3);
            WorldTime.SetHere(p.m_customData, BoonKey, secs); // (likewise)
            p.m_customData[BoonKindKey] = kind.ToString();
            ShowBoon(p, kind, secs);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Дар Хугіна до ранку: +{0} {1}", RavenAbilities.R(BoonAmount), BoonWhat(kind)));
            _boonSay = kind; _boonSayAt = Time.time + sayAfter;
            Log("morning: the day's gift, +" + BoonAmount + " " + BoonTag(kind));
        }

        private static void ShowBoon(Player p, int kind, float secs)
        {
            SE_Stats se = RavenFx.Template(ref _boon, BoonName);
            se.m_name = Lang.W("Дар віщування");
            se.m_tooltip = Lang.F("Хугін вивіщував тобі дар: +{0} {1} до наступного ранку.", RavenAbilities.R(BoonAmount), BoonWhat(kind));
            se.m_icon = AbilityIcons.ForEffect("homen", false);
            se.m_ttl = secs;
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        // the gift and the bad omen outlive a relog (they are in the character): their icons are put back, with the time left
        private static void KeepBoonIcon(Player p)
        {
            if (p.IsDead()) return; // (back with the new body)
            float left = WorldTime.LeftHere(p.m_customData, BoonKey);
            if (left > 0f && !p.GetSEMan().HaveStatusEffect(BoonName.GetStableHashCode())) ShowBoon(p, BoonKind(p), left);
            left = WorldTime.LeftHere(p.m_customData, BadKey);
            if (left > 0f && !p.GetSEMan().HaveStatusEffect(BadName.GetStableHashCode())) ShowBad(p, left);
        }

        private static int BoonKind(Player p)
        {
            string v; int k;
            return p.m_customData.TryGetValue(BoonKindKey, out v) && int.TryParse(v, out k) ? Mathf.Clamp(k, 0, 2) : 0;
        }

        // From the food's total (OmenBoonPatch): what the day's gift adds now, for this player only
        internal static void AddBoon(Player p, ref float hp, ref float stamina, ref float eitr)
        {
            if (p == null || p != Player.m_localPlayer || p.m_customData == null || WorldTime.LeftHere(p.m_customData, BoonKey) <= 0f) return;
            int kind = BoonKind(p);
            if (kind == 0) hp += BoonAmount; else if (kind == 1) stamina += BoonAmount; else eitr += BoonAmount;
        }

        // From Trust.OnDeath: the share of the trust a death takes (the bad omen: less)
        internal static float DeathMul()
        {
            Player p = Player.m_localPlayer;
            return p != null && WorldTime.LeftHere(p.m_customData, BadKey) > 0f ? BadDeathMul : 1f;
        }

        // ---------------------------------------------------------------- the card's buttons

        // "" — may turn it aside now; else why not (shown on the card)
        internal static string WhyNotTurn(OmenFate.Answer a)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return Lang.T("нема кому");
            if (a.Grown) return Lang.T("двічі долю не відведеш");
            float left = WorldTime.Left(p.m_customData, TurnKey);
            if (left > 0f) return Lang.F("знову зможе за {0}", RavenAbilities.Clock(left));
            float cost = TurnCost();
            if (Power.Current(false) < Mathf.Round(cost)) return Lang.F("бракує сили Хугіна: треба {0}", Mathf.RoundToInt(cost));
            return "";
        }

        internal static float TurnCost() { return DeflectCost * Satiety.CostMul(false); }

        internal static void Turn()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (WorldTime.Left(p.m_customData, TurnKey) > 0f || Power.Current(false) < Mathf.Round(TurnCost())) { RavenMsg.Center(p, Lang.F("Хугін не зміг: {0}", Lang.T("бракує сили або ще рано"))); return; }
            OmenFate.Do(1, (ok, why) =>
            {
                if (!ok) { RavenMsg.Center(p, Lang.F("Хугін не зміг: {0}", Why(why))); return; }
                Power.Spend(p, false, DeflectCost);
                WorldTime.Set(p.m_customData, TurnKey, DeflectDays * (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f));
                ScreamFx.CawAll(p, false, 1.1f, 0.8f);
                Say(p, "omen_deflect");
                RavenMsg.Center(p, Lang.T("Набіг відведено на добу.\nВін повернеться — і буде злішим."));
                Log("raid turned aside");
            });
        }

        internal static void Summon()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            OmenFate.Do(2, (ok, why) =>
            {
                if (!ok) { RavenMsg.Center(p, Lang.F("Не вийшло накликати: {0}", Why(why))); return; }
                ScreamFx.CawAll(p, false, 0.95f, 1f);
                Say(p, "omen_summon");
                _munSummonAt = Time.time + 5f;
                Log("raid called now");
            });
        }

        private static float _munSummonAt = -1f;
        internal static void TickLate(Player p)
        {
            if (_munSummonAt < 0f || Time.time < _munSummonAt || p == null) return;
            _munSummonAt = -1f;
            ItemDrop.ItemData m = Trust.Worn(true);
            if (m == null || !Trust.Active(m) || Trust.Away(m)) return;
            string line = Line(p, true, "omen_summon");
            if (line != null) RavenSkills.SayFromShoulder(p, true, line);
        }

        // the server's "why not", said
        internal static string Why(string code)
        {
            switch (code)
            {
                case "nobase": return Lang.T("накликати можна лише біля своєї бази");
                case "land": return Lang.T("цей набіг сюди не дійде — не та земля");
                case "keys": return Lang.T("цей набіг тобі ще не судився");
                case "inside": return Lang.T("під землею його не накличеш");
                case "busy": return Lang.T("напад уже йде");
                case "twice": return Lang.T("двічі долю не відведеш");
                case "none": return Lang.T("віщування вже не діє");
                case "noplayer": return Lang.T("сервер тебе ще не бачить");
                default: return Lang.T("без RavenWatch на сервері Хугін не впливає на набіги");
            }
        }

        // ---------------------------------------------------------------- what is said

        // when the raid comes, said from now: «сьогодні вдень», «цієї ночі», «завтра вранці»
        internal static string When(float left)
        {
            EnvMan em = EnvMan.instance;
            if (em == null || ZNet.instance == null) return Lang.T("скоро");
            double now = ZNet.instance.GetTimeSeconds(), at = now + left;
            float frac = (float)(at % em.m_dayLengthSec) / em.m_dayLengthSec;
            bool tomorrow = em.GetDay(at) > em.GetDay(now);
            if (left < 120f) return Lang.T("от-от");
            if (frac < 0.15f) return Lang.T("цієї ночі");
            if (tomorrow) return frac < 0.3f ? Lang.T("завтра вранці") : Lang.T("завтра");
            if (frac < 0.3f) return Lang.T("цього ранку");
            if (frac < 0.6f) return Lang.T("сьогодні вдень");
            if (frac < 0.85f) return Lang.T("надвечір");
            return Lang.T("цієї ночі");
        }

        // the raid's main creature (its token, for the ravens' names) and the raid as the card shows it
        private static string RaidToken(string name, out string shown)
        {
            shown = name;
            RandEventSystem rs = RandEventSystem.instance;
            if (rs == null) return "";
            foreach (RandomEvent e in rs.m_events)
            {
                if (e == null || e.m_name != name) continue;
                string token = "";
                foreach (SpawnSystem.SpawnData sd in e.m_spawn)
                {
                    Character c = sd != null && sd.m_prefab != null ? sd.m_prefab.GetComponent<Character>() : null;
                    if (c == null || c.IsBoss()) continue;
                    token = HitInfo.Token(c);
                    shown = Lang.Game(c.m_name);
                    break;
                }
                string sign = !string.IsNullOrEmpty(e.m_startMessage) ? Lang.Game(e.m_startMessage) : "";
                if (!string.IsNullOrEmpty(sign) && !sign.StartsWith("[")) shown = string.IsNullOrEmpty(shown) || shown == name ? sign : shown + " — «" + sign + "»";
                return token;
            }
            return "";
        }

        private static string Line(Player p, string tag, params string[] values) { return Line(p, false, tag, values); }

        private static string Line(Player p, bool munin, string tag, params string[] values)
        {
            if (Plugin.Instance == null) return null;
            Composer.Extra.Clear();
            try
            {
                for (int i = 0; i + 1 < values.Length; i += 2)
                {
                    if (values[i] == "raid") { if (!string.IsNullOrEmpty(values[i + 1])) Composer.ExtraCreature("raid", values[i + 1]); }
                    else if (!string.IsNullOrEmpty(values[i + 1])) Composer.Extra[values[i]] = values[i + 1];
                }
                string line = Plugin.Instance.ComposeTagged(p, munin, tag);
                return string.IsNullOrEmpty(line) ? null : line;
            }
            finally { Composer.Extra.Clear(); }
        }

        private static void Say(Player p, string tag)
        {
            if (!Ready(p)) return;
            string line = Line(p, tag);
            if (line != null) RavenSkills.SayFromShoulder(p, false, line);
        }

        // ---------------------------------------------------------------- the weather ahead

        // the day ahead where you stand, by parts: «зранку ясно, вдень і надвечір дощ, уночі гроза» (null: nothing to say);
        // land: where, said before it («на Луках»)
        internal static string Forecast(Player p, out string land)
        {
            land = "";
            EnvMan em = EnvMan.instance;
            if (em == null || ZNet.instance == null || WorldGenerator.instance == null || p == null || p.transform.position.y > 3000f) return null;
            Vector3 at = p.transform.position;
            long dur = Math.Max(1L, em.m_environmentDuration), day = Math.Max(1L, em.m_dayLengthSec);
            double now = ZNet.instance.GetTimeSeconds();
            long first = (long)(now / dur), last = (long)((now + day) / dur);
            string[] parts = new string[4]; int[] rank = new int[4];
            List<int> order = new List<int>();
            for (long k = first; k <= last && k - first < 200; k++)
            {
                double t = Math.Max(now, k * (double)dur);
                int part = Part((float)(t % day) / day);
                string w; int r;
                if (!Weather(EnvAt(em, k, at), out w, out r)) continue;
                if (!order.Contains(part)) { order.Add(part); parts[part] = w; rank[part] = r; }
                else if (r > rank[part]) { parts[part] = w; rank[part] = r; } // (the most telling weather of that part)
            }
            if (order.Count == 0) return null;
            // the same weather in parts one after another is said once: «вдень і надвечір дощ»
            List<string> said = new List<string>();
            int i = 0;
            bool all = true;
            for (int j = 1; j < order.Count; j++) if (parts[order[j]] != parts[order[0]]) all = false;
            land = Composer.BiomeName(Biome(at), true);
            if (all) return Lang.F("увесь день {0}", parts[order[0]]);
            while (i < order.Count)
            {
                int j = i;
                while (j + 1 < order.Count && parts[order[j + 1]] == parts[order[i]]) j++;
                string when = PartName(order[i]);
                if (j > i) when = Lang.F("{0} і {1}", when, PartName(order[j]));
                said.Add(when + " " + parts[order[i]]);
                i = j + 1;
            }
            return string.Join(", ", said.ToArray());
        }

        private static Heightmap.Biome Biome(Vector3 at) { return WorldGenerator.instance.GetBiome(at); }

        // 0 morning, 1 day, 2 evening, 3 night (the day's raw share: the sun is up from 0.15 to 0.85)
        private static int Part(float frac) { return frac < 0.15f || frac >= 0.85f ? 3 : frac < 0.3f ? 0 : frac < 0.6f ? 1 : 2; }
        private static string PartName(int part) { return part == 0 ? Lang.T("зранку") : part == 1 ? Lang.T("вдень") : part == 2 ? Lang.T("надвечір") : Lang.T("уночі"); }

        // the weather the game will pick for that stretch of time at that spot (its own way: the stretch's number seeds it)
        private static EnvSetup EnvAt(EnvMan em, long period, Vector3 at)
        {
            List<EnvEntry> list;
            try
            {
                List<EnvEntry> got = em.GetAvailableEnvironments(WorldGenerator.instance.GetBiomeSector(at));
                if (got == null || got.Count == 0) return null;
                list = new List<EnvEntry>(got); // (the game's list is one it fills anew on every call)
            }
            catch (Exception) { return null; }
            bool ash = WorldGenerator.IsAshlands(at.x, at.z), north = WorldGenerator.IsDeepnorth(at.x, at.y); // (as the game asks it)
            UnityEngine.Random.State state = UnityEngine.Random.state;
            EnvSetup pick = null;
            try
            {
                UnityEngine.Random.InitState((int)period);
                float sum = 0f;
                foreach (EnvEntry e in list) if (!e.m_ashlandsOverride && !e.m_deepnorthOverride) sum += e.m_weight;
                float roll = UnityEngine.Random.Range(0f, sum), run = 0f;
                foreach (EnvEntry e in list)
                {
                    if (e.m_ashlandsOverride || e.m_deepnorthOverride) continue;
                    run += e.m_weight;
                    if (run >= roll) { pick = e.m_env; break; }
                }
                if (pick == null) { EnvEntry lastE = list[list.Count - 1]; if (!lastE.m_ashlandsOverride && !lastE.m_deepnorthOverride) pick = lastE.m_env; }
                foreach (EnvEntry e in list) { if (e.m_ashlandsOverride && ash) pick = e.m_env; if (e.m_deepnorthOverride && north) pick = e.m_env; }
            }
            finally { UnityEngine.Random.state = state; }
            return pick;
        }

        // the weather in a word, and how telling it is (a storm says more than a clear sky)
        private static bool Weather(EnvSetup e, out string word, out int rank)
        {
            word = null; rank = 0;
            if (e == null || string.IsNullOrEmpty(e.m_name)) return false;
            string n = e.m_name.ToLowerInvariant();
            if (n.Contains("snowstorm") || n.Contains("blizzard")) { word = Lang.T("завірюха"); rank = 6; }
            else if (n.Contains("thunder") || n == "nofogts" || n.Contains("storm")) { word = Lang.T("гроза"); rank = 6; }
            else if (n.Contains("ash") && (n.Contains("rain") || n.Contains("cinder"))) { word = Lang.T("попіл з неба"); rank = 5; }
            else if (n.Contains("snow")) { word = Lang.T("сніг"); rank = 4; }
            else if (n.Contains("lightrain")) { word = Lang.T("мряка"); rank = 3; }
            else if (n.Contains("rain") || e.m_isWet) { word = Lang.T("дощ"); rank = 4; }
            else if (n.Contains("mist") || n.Contains("fog")) { word = Lang.T("туман"); rank = 2; }
            else if (n.Contains("dark")) { word = Lang.T("морок"); rank = 2; }
            else { word = Lang.T("ясно"); rank = 1; }
            return true;
        }

        // ---------------------------------------------------------------- at sea

        private static void Sea(Player p)
        {
            if (!AtSea || Time.time < _nextSea) return;
            _nextSea = Time.time + 3f;
            Ship ship = ShipPlay.ShipOf(p);
            if (ship == null || !Ready(p) || Time.time - _seaSaidAt < 120f) return;
            if (Serpent(p)) return;
            Wind(p, ship);
        }

        // a serpent in these waters (one not yet warned of): before it finds you
        private static bool Serpent(Player p)
        {
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || Utils.GetPrefabName(c.gameObject) != "Serpent") continue;
                if (Vector3.Distance(c.transform.position, p.transform.position) > SerpentRange) continue;
                ZNetView nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || !Serpents.Add(nv.GetZDO().m_uid)) continue;
                if (Serpents.Count > 64) Serpents.Clear();
                _seaSaidAt = Time.time;
                PerkFx.LookAtFor(false, c.transform, 3f);
                ScreamFx.CawAll(p, false, 1.3f, 0.8f);
                string line = Line(p, "omen_serpent");
                RavenSkills.SayFromShoulder(p, false, line ?? Lang.T("У цих водах змій. Я його чую."));
                Log("a serpent near the ship");
                return true;
            }
            return false;
        }

        // the wind about to turn a lot (the game's wind is fixed ahead by the clock): a word some seconds before
        private static void Wind(Player p, Ship ship)
        {
            EnvMan em = EnvMan.instance;
            if (em == null || ZNet.instance == null || ship.IsWindControllActive()) return;
            long w = Math.Max(1L, WindPeriod(em));
            double now = ZNet.instance.GetTimeSeconds();
            long next = ((long)(now / w) + 1L) * w;
            double ahead = next - now;
            if (ahead < 15.0 || ahead > 90.0 || next == _windSaidFor) return;
            _windSaidFor = next;
            EnvSetup env = em.GetCurrentEnvironment();
            if (env == null) return;
            float pNow, pThen;
            Vector3 dNow = WindAt(em, (long)now, env, out pNow), dThen = WindAt(em, next + 1L, env, out pThen);
            if (Vector3.Angle(dNow, dThen) < 60f || UnityEngine.Random.value > 0.6f) return;
            string said = Lang.F("{0}, {1}", Toward(dThen), WindWord(pThen));
            _seaSaidAt = Time.time;
            string line = Line(p, "omen_wind", "wind", said, "soon", RavenAbilities.SpokenSpan((float)ahead));
            RavenSkills.SayFromShoulder(p, false, line ?? Lang.F("Скоро вітер поверне: дутиме {0}.", said));
            Log("the wind turns in " + Mathf.RoundToInt((float)ahead) + " s: " + said);
        }

        private static long WindPeriod(EnvMan em) { return em.m_windPeriodDuration; }

        // the game's wind at that second (its octaves, seeded by the clock), for that weather
        private static Vector3 WindAt(EnvMan em, long t, EnvSetup env, out float power)
        {
            long w = Math.Max(1L, WindPeriod(em));
            UnityEngine.Random.State state = UnityEngine.Random.state;
            float angle = 0f, strength = 0.5f;
            try
            {
                foreach (int octave in new int[] { 1, 2, 4, 8 })
                {
                    UnityEngine.Random.InitState((int)(t / Math.Max(1L, w / octave)));
                    angle += UnityEngine.Random.value * (Mathf.PI * 2f / octave);
                    strength += -0.5f / octave + UnityEngine.Random.value / octave;
                }
            }
            finally { UnityEngine.Random.state = state; }
            power = Mathf.Clamp(Mathf.Lerp(env.m_windMin, env.m_windMax, strength), 0.05f, 1f);
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        private static readonly string[] TowardSrc = Lang.Keys("на північ", "на північний схід", "на схід", "на південний схід", "на південь", "на південний захід", "на захід", "на північний захід");
        private static string Toward(Vector3 d)
        {
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return Lang.T(TowardSrc[Mathf.RoundToInt((a + 360f) % 360f / 45f) % 8]);
        }

        private static string WindWord(float v) { return v < 0.3f ? Lang.T("слабкий") : v < 0.6f ? Lang.T("рівний") : v < 0.85f ? Lang.T("сильний") : Lang.T("шквальний"); }

        // ---------------------------------------------------------------- the rest

        internal static void Reset() { _awakeSince = -1f; _badSayAt = -1f; _boonSayAt = -1f; _munSummonAt = -1f; _windSaidFor = -1f; Serpents.Clear(); OmenFate.Reset(); RavenBook.HideOmen(); }

        // the test panel: this morning's word now (the morning's roll and the day's mark left as they are)
        internal static string Test(Player p)
        {
            if (p == null || p.IsDead()) return Lang.T("Персонаж мертвий.");
            if (!Ready(p)) return Lang.T("Хугін не віщує: його нема на плечі, він відлетів або довіра замала.");
            OmenFate.Ask(a => Speak(p, a, UnityEngine.Random.Range(0f, 100f) < BadChance));
            return Lang.T("Хугін віщує (відповідь сервера — за мить).");
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Omen: " + s); }
    }

    // the day's gift: on top of what the food gives (the game sets the maximum from this total)
    [HarmonyLib.HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
    internal static class OmenBoonPatch
    {
        private static void Postfix(Player __instance, ref float __0, ref float __1, ref float __2) // (by place: health, stamina, eitr, whatever the game names them)
        { try { Omen.AddBoon(__instance, ref __0, ref __1, ref __2); } catch (Exception e) { PatchGuard.Fail("OmenBoonPatch", e); } }
    }
}
#endif
