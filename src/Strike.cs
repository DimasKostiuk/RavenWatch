#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Удар в очі» and «Кігті»: the raven leaves the shoulder, flies where you look, strikes and comes back.
    // The blow is the raven's own, never the weapon's: a base by the bosses you beat with it, raised by its training
    // (вишкіл, 30 levels earned by flights; techniques from 10 on, Mastery), by trust and by a full belly. Whatever other mods add to a player's hit
    // is cut back to that, and a boss loses at most a small share of its health to one flight.
    internal static class Strike
    {
        // ---------------------------------------------------------------- settings

        internal static float Range = 30f, RangeTop = 100f, RangeMaster = 120f, AimDegrees = 10f; // how far it flies: at training level 1, 20 and 30
        internal static float HuginCost = 25f, MuninCost = 35f;
        internal static float HuginCooldown = 40f, MuninCooldown = 60f;
        internal static float BossCapHugin = 0.03f, BossCapMunin = 0.05f;
        internal static float DebtBossCap = 0.1f; // «Злість» III: of a boss's health, the most a debt takes at once
        private static bool _volley;
        internal static float MuninMul = 1.2f;
        internal static float TrainPct = 3f, TrustPct = 5f, FedPct = 20f, FedMinutes = 10f;
        internal static float CritBonus = 20f, CritPerTrust = 5f; // points of crit chance on top of the training's (at the top: Hugin 85, Munin 90)
        // a raven's own pace (about 12-14 m/s for the real bird): far targets take their time, near ones are not a blink
        internal static float Speed = 12f, ReturnSpeed = 10f;
        internal static float SeriesGap = 0.45f;

        // the best blow a player can have in each biome, unlocked by the boss before it (seen with this raven)
        internal static readonly float[] Base = { 20f, 45f, 70f, 95f, 120f, 145f, 180f, 210f };
        internal static readonly string[] Bosses = { "Eikthyr", "gd_king", "Bonemass", "Dragon", "GoblinKing", "SeekerQueen", "Fader" };
        internal static string[] BossNames { get { return Lang.Arr(BossNamesSrc); } } // (translated on every read)
        private static readonly string[] BossNamesSrc = Lang.Keys("Ейктюр", "Прадавній", "Кістянка", "Модер", "Яглут", "Королева", "Фейдер");

        // flights from one training level to the next (1→2 … 29→30). Past TrainCap («Майстерність») the chances stay as at
        // TrainCap, the range grows on to RangeMaster, the blow by TrainPctHigh a level, and the techniques come (Mastery)
        internal static readonly int[] Need = { 5, 8, 10, 13, 15, 18, 20, 23, 25, 28, 30, 33, 35, 38, 40, 43, 45, 48, 50, 53, 55, 58, 60, 63, 65, 68, 70, 73, 75 };
        internal const int MaxLevel = 30, TrainCap = 20;
        internal static float TrainPctHigh = 1f;

        // chances at training level 1 → 20, in percent (Hugin has no triple: the series of three is Munin's)
        // (Hugin, one target and no bleeding: the precise one — more crits and doubles than Munin's per blow)
        private static readonly float[] HuginCrit = { 20f, 35f }, HuginDouble = { 20f, 40f }, HuginTriple = { 0f, 0f };
        private static readonly float[] MuninCrit = { 20f, 40f }, MuninDouble = { 25f, 35f }, MuninTriple = { 5f, 15f };
        internal static float MissRefund = 0.5f; // nothing struck: this share of the strength comes back and of the pause is let off

        private const string XpKey = "RavenWatch_strikes", BossKey = "RavenWatch_bossesSeen", FedAtKey = "RavenWatch_fedAt";
        private const string Rpc = "RavenWatch_Strike", RpcBoss = "RavenWatch_BossDown";
        private static readonly System.Random Rng = new System.Random();

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnStrike));
            ZRoutedRpc.instance.Register<ZPackage>(RpcBoss, new Action<long, ZPackage>(OnBossDown));
        }

        // ---------------------------------------------------------------- what the raven has in it

        private static string Get(ItemDrop.ItemData item, string key)
        {
            string v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(key, out v) ? v : null;
        }

        private static void Put(ItemDrop.ItemData item, string key, string v) { if (item != null) item.m_customData[key] = v; }

        // one more flight that reached someone (a raven on guard at home: Home)
        internal static void AddFlight(ItemDrop.ItemData item) { if (item != null) Put(item, XpKey, (Flights(item) + 1).ToString()); }

        internal static int Flights(ItemDrop.ItemData item) { int n; return int.TryParse(Get(item, XpKey), out n) ? Mathf.Max(0, n) : 0; }

        // the training level and how far into the next one
        internal static int Level(ItemDrop.ItemData item) { int have, need; return Level(item, out have, out need); }
        internal static int Level(ItemDrop.ItemData item, out int have, out int need)
        {
            int left = Flights(item), level = 1;
            while (level < MaxLevel && left >= Need[level - 1]) { left -= Need[level - 1]; level++; }
            have = level < MaxLevel ? left : 0;
            need = level < MaxLevel ? Need[level - 1] : 0;
            return level;
        }

        // the flights it takes from nothing to this level
        internal static int FlightsFor(int level)
        {
            int sum = 0;
            for (int i = 0; i < Mathf.Clamp(level, 1, MaxLevel) - 1; i++) sum += Need[i];
            return sum;
        }

        // (the console: raven train) as if it had flown just enough for this level
        internal static void SetLevel(ItemDrop.ItemData item, int level)
        {
            Put(item, XpKey, FlightsFor(level).ToString());
        }

        internal static List<string> BossesSeen(ItemDrop.ItemData item)
        {
            List<string> list = new List<string>();
            string raw = Get(item, BossKey);
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (string s in raw.Split(',')) if (s.Length > 0 && !list.Contains(s)) list.Add(s);
            return list;
        }

        // the base step: the bosses in the line fallen with this raven on the shoulder, one after another from the first
        // (steps: a later boss counts only once all before it are seen — Fader alone does not lift the strike to the top;
        // it is kept and counts when the gap is closed)
        internal static int Tier(ItemDrop.ItemData item)
        {
            List<string> seen = BossesSeen(item);
            int tier = 0;
            while (tier < Bosses.Length && seen.Contains(Bosses[tier])) tier++;
            return Mathf.Clamp(tier, 0, Base.Length - 1);
        }

        // how far this raven flies to a target: its training teaches it to fly further
        internal static float RangeOf(ItemDrop.ItemData item) { return Mathf.Round(TrainRange(item) * Norov.RangeMul(item) * GearFx.RangeMul(item) * RuneStars.RangeMul(item)); } // (knack «Далекий виліт», «Кігті комара», a starred rune)

        // the strength a strike takes (knack «Ощадний»: less)
        internal static float CostOf(bool munin, ItemDrop.ItemData item) { return (munin ? MuninCost : HuginCost) * Norov.CostMul(item); }

        // the blow as it would be with the next boss seen (the book's "what next")
        internal static float BlowWithTier(bool munin, ItemDrop.ItemData item, int tier)
        {
            return Base[Mathf.Clamp(tier, 0, Base.Length - 1)] * (munin ? MuninMul : 1f) * (1f + (TrainBonus(item) + TrustBonus(item) + FedBonus(item) + KnackBonus(item) + GearFx.DamagePct(item) + RuneStars.StrikePct(item) + BreedTraits.StrikePct(item) + Habits.StrikePct(item) + Bath.StrikePct(item)) / 100f) * StarveMul(item) * GiftUp.PowerOf(item, munin ? "mstrike" : "hstrike");
        }

        // why the blow is what it is, line by line (the book's tooltip)
        internal static string Why(bool munin, ItemDrop.ItemData item)
        {
            int tier = Tier(item);
            float b = BaseOf(munin, item), tr = TrainBonus(item), tu = TrustBonus(item), fe = FedBonus(item), kn = KnackBonus(item);
            float sum = tr + tu + fe + kn + GearFx.DamagePct(item) + RuneStars.StrikePct(item) + BreedTraits.StrikePct(item) + Habits.StrikePct(item) + Bath.StrikePct(item), blow = Blow(munin, item), starve = StarveMul(item);
            List<string> seen = BossesSeen(item);
            string from = tier == 0 ? Lang.T("ще жодного боса з ним на плечі") : Lang.F("найдальший бос, переможений з ним: {0}", BossNames[tier - 1]);
            string t = Lang.F("Основа: {0} ({1}{2})\n+ вишкіл {3}-го рівня: +{4}%\n+ довіра {5}-го рівня: +{6}%\n+ ситий: {7}\n{8}{9}{10}= {11} × {12}{13} = {14}\n\n{15}Удар не залежить від твоєї зброї. Боса один виліт ранить не більше ніж на {16}% здоров'я.", R(b), from, (munin ? Lang.F("; Мунін б'є на {0}% сильніше", R((MuninMul - 1f) * 100f)) : ""), Level(item), R(tr), Trust.Level(item), R(tu), (fe > 0f ? Lang.F("+{0}% (ще {1})", R(fe), RavenAbilities.Clock(FedLeft(item))) : Lang.F("0% (нагодуй — {0} хв по +{1}%)", R(FedMinutes), R(FedPct))), (kn > 0f ? Lang.F("+ хист «{0}»: +{1}%\n", Norov.KnackOf(item).Name(munin), R(kn)) : ""), GearFx.WhyLine(item) + RuneStars.WhyLine(item) + BreedTraits.WhyLine(item) + FreshLine(item), (starve < 1f ? (Trust.Starving(item)
                    ? Lang.F("<color=#FF6B6B>× голод: −{0}% (ситість 0 — нагодуй)</color>\n", R(Trust.StarveStrikePct))
                    : Lang.F("<color=#B48CE6>× об'ївся: −{0}% (лінується, поки ситість понад {1})</color>\n", R(Satiety.StuffedStrikePct), R(Satiety.StuffedAt))) : ""), R(b), (1f + sum / 100f).ToString("0.00"), (starve < 1f ? " × " + starve.ToString("0.00") : ""), R(blow), (GearFx.FrostShareOf(item) > 0f ? Lang.F("«Холодний медальйон»: {0}% удару — мороз, він сповільнює ціль.\n", R(GearFx.FrostShareOf(item) * 100f)) : ""), R((munin ? BossCapMunin : BossCapHugin) * 100f));
            return t;
        }

        // where the crit chance comes from, the very sum of CritChance (the strike's and the book's tooltips)
        internal static string CritLine(bool munin, ItemDrop.ItemData item)
        {
            return Lang.F("Шанс криту {0}%: основа {1}% (у кожного ворона) + вишкіл {2}% + довіра {3}%{4}{5}.", R(CritChance(munin, item)), R(CritBonus), R(Lerp(munin ? MuninCrit : HuginCrit, Level(item))), R(Mathf.Max(0, Trust.Level(item) - 1) * CritPerTrust), (Norov.CritPts(item) > 0f ? Lang.F(" + хист «Влучний» {0}% (межа {1}%)", R(Norov.CritPts(item)), R(Norov.CritCap(item))) : ""), (GearFx.CritPts(item) > 0f ? Lang.F(" + «Монета на удачу» {0}%", R(GearFx.CritPts(item))) : ""));
        }

        // where the range comes from, the very product of RangeOf: the training, then the knack and the gear (the book)
        internal static string RangeWhy(bool munin, ItemDrop.ItemData item)
        {
            float train = TrainRange(item);
            float knack = (Norov.RangeMul(item) - 1f) * 100f, gear = (GearFx.RangeMul(item) - 1f) * 100f, rune = (RuneStars.RangeMul(item) - 1f) * 100f;
            float all = Norov.RangeMul(item) * GearFx.RangeMul(item) * RuneStars.RangeMul(item);
            Norov.Knack k = Norov.KnackOf(item);
            return Lang.F("Від вишколу {0}-го рівня: {1} м ({2} м на 1-му, {3} м на {4}-му, {8} м на {9}-му)\n{5}{6}{7}", Level(item), R(train), R(Range), R(RangeTop), TrainCap,(knack > 0.5f ? Lang.F("+ хист «{0}»: +{1}%\n", (k != null ? k.Name(munin) : ""), R(knack)) : ""), (gear > 0.5f ? Lang.F("+ «Кігті комара»: +{0}%\n", R(gear)) : "") + (rune > 0.5f ? Lang.F("+ зіркова руна Фреї: +{0}%\n", R(rune)) : ""), (all > 1.005f ? Lang.F("= {0} × {1} = {2} м", R(train), all.ToString("0.00"), R(RangeOf(item))) : Lang.F("= {0} м", R(RangeOf(item)))), R(RangeMaster), MaxLevel);
        }

        private static string R(float v) { return Mathf.RoundToInt(v).ToString(); }

        internal static float FedLeft(ItemDrop.ItemData item) { return item != null ? WorldTime.Left(item.m_customData, FedAtKey) : 0f; }
        internal static void Fed(ItemDrop.ItemData item) { Fed(item, 1f); }
        internal static void Fed(ItemDrop.ItemData item, float mul) { if (item != null && FedMinutes > 0f) WorldTime.Set(item.m_customData, FedAtKey, FedMinutes * 60f * Mathf.Max(0.1f, mul)); }

        internal static float TrainBonus(ItemDrop.ItemData item) { int l = Level(item); return (Mathf.Min(l, TrainCap) - 1) * TrainPct + Mathf.Max(0, l - TrainCap) * TrainPctHigh; }

        // the range the training gives: Range → RangeTop up to TrainCap, then on to RangeMaster at MaxLevel
        private static float TrainRange(ItemDrop.ItemData item) { int l = Level(item); return l <= TrainCap ? Mathf.Lerp(Range, RangeTop, Grown(item)) : Mathf.Lerp(RangeTop, RangeMaster, (l - TrainCap) / (float)(MaxLevel - TrainCap)); }

        // how far the training has grown the range and the chances: 0 at level 1, 1 from TrainCap on
        private static float Grown(ItemDrop.ItemData item) { return (Mathf.Min(Level(item), TrainCap) - 1) / (float)(TrainCap - 1); }
        internal static float TrustBonus(ItemDrop.ItemData item) { return Mathf.Max(0, Trust.Level(item) - 1) * TrustPct; }
        internal static float FedBonus(ItemDrop.ItemData item) { return FedLeft(item) > 0f ? FedPct : 0f; }
        // «Свіже пір'я» a while after a bath (Bath): its own line in the tooltip
        private static string FreshLine(ItemDrop.ItemData item) { float l = Bath.FreshLeft(item); return l > 0f && Bath.StrikePct(item) > 0f ? Lang.F("+ свіже пір'я: +{0}% (ще {1})\n", R(Bath.StrikePct(item)), RavenAbilities.Clock(l)) : ""; }
        internal static float KnackBonus(ItemDrop.ItemData item) { return Norov.DamagePct(item); } // knack «Важкий дзьоб / Важкі кігті»

        // a starving raven (satiety 0) strikes weaker, and a stuffed one is too lazy to strike hard
        internal static float StarveMul(ItemDrop.ItemData item)
        {
            if (Trust.Starving(item) && !RuneStars.StarveFree(item)) return Mathf.Clamp01(1f - Trust.StarveStrikePct / 100f); // («Іній» ★★★: full strength)
            return Satiety.IsStuffed(item) ? Mathf.Clamp01(1f - Satiety.StuffedStrikePct / 100f) : 1f;
        }

        // the share of its blow that is fire: a rune of Freya (ignite) or the breed «Жаровий», the larger
        internal static float FireOf(ItemDrop.ItemData item) { return Mathf.Max(RuneStars.FireShare(item), BreedTraits.FireShareOf(item)); }

        internal static float BaseOf(bool munin, ItemDrop.ItemData item) { return Base[Tier(item)] * (munin ? MuninMul : 1f); }

        // one blow before any crit
        internal static float Blow(bool munin, ItemDrop.ItemData item)
        {
            return BaseOf(munin, item) * (1f + (TrainBonus(item) + TrustBonus(item) + FedBonus(item) + KnackBonus(item) + GearFx.DamagePct(item) + RuneStars.StrikePct(item) + BreedTraits.StrikePct(item) + Habits.StrikePct(item) + Bath.StrikePct(item)) / 100f) * StarveMul(item) * GiftUp.PowerOf(item, munin ? "mstrike" : "hstrike"); // (+ «Кігтясте намисто», golden runes)
        }

        private static float Lerp(float[] range, int level) { return Mathf.Lerp(range[0], range[1], (Mathf.Min(level, TrainCap) - 1) / (float)(TrainCap - 1)); }

        internal static float CritChance(bool munin, ItemDrop.ItemData item)
        {
            float c = Lerp(munin ? MuninCrit : HuginCrit, Level(item)) + CritBonus + Mathf.Max(0, Trust.Level(item) - 1) * CritPerTrust + Norov.CritPts(item) + GearFx.CritPts(item) + (!munin && Mastery.Has(item, "h_payback") ? Mastery.PaybackCrit : 0f); // (+ «Монета на удачу», «Розплата»)
            return Mathf.Clamp(c, 0f, Norov.CritCap(item)); // (knack «Влучний»: +5 and the cap 95)
        }
        internal static float DoubleChance(bool munin, ItemDrop.ItemData item) { return Lerp(munin ? MuninDouble : HuginDouble, Level(item)) + (!munin && Mastery.Has(item, "h_double") ? Mastery.DoublePts : 0f); } // («Подвійне око»)
        internal static float TripleChance(bool munin, ItemDrop.ItemData item) { return Lerp(munin ? MuninTriple : HuginTriple, Level(item)); }

        // ---------------------------------------------------------------- the flight

        internal sealed class Peck
        {
            public Character Target;
            public ZDOID Id;
            public bool Crit;
            public bool Sky; // «Каменем з неба»: the first blow at one that never saw it
            public float Debt; // «Злість» III: the blood debt Munin pays back with this blow
        }

        // the techniques (Mastery) of the raven that flies, as they act on this one flight
        internal sealed class Mods
        {
            public ItemDrop.ItemData Item;
            public float Paid, CritMul = 2f, Light, BlindMul = 1f, BlindBoss, MarkPct, MarkSecs, ResoPct, Feather, EdgePct, MercyPct, Slow;
            public bool Sky, Eitr, Silent, Odin, Thunder, Roar, Hot, Weak, Trail, Carrion, Rune, Thirst, BloodEitr, Whirl, Slaughter, Ricochet;
            public bool CarrionDone;
            public readonly HashSet<Character> Sneaky = new HashSet<Character>(); // unaware when the raven took off

            internal static Mods For(bool munin, ItemDrop.ItemData item, float paid)
            {
                Mods m = new Mods { Item = item, Paid = paid };
                Func<string, bool> has = id => Mastery.Has(item, id);
                if (!munin)
                {
                    if (has("h_mark")) { m.MarkPct = Mastery.MarkPct; m.MarkSecs = Mastery.MarkSeconds; }
                    if (has("h_spark")) m.Light = Mastery.SparkShare;
                    if (has("h_blind")) m.BlindMul = Mastery.BlindMul;
                    m.Sky = has("h_sky"); m.Eitr = has("h_eitr"); m.Ricochet = has("h_ricochet"); m.Silent = has("h_silent");
                    if (has("h_reso")) m.ResoPct = Mastery.ResoPct;
                    if (has("h_feather")) m.Feather = Mastery.FeatherShare;
                    if (has("h_edge")) m.EdgePct = Mastery.EdgePct;
                    if (has("h_odin") && Mastery.OdinReady()) { m.Odin = true; m.BlindBoss = Mastery.OdinBlindBoss; } // (used up in Launch, only on a flight with a target)
                    m.Thunder = has("h_thunder");
                    if (has("h_payback")) m.CritMul = Mastery.PaybackMul;
                }
                else
                {
                    if (has("m_cripple")) m.Slow = Mastery.CrippleSlow;
                    m.Hot = has("m_hot"); m.Roar = has("m_roar"); m.Weak = has("m_armor"); m.Trail = has("m_trail");
                    m.Carrion = has("m_carrion"); m.Rune = has("m_rune"); m.Thirst = has("m_thirst"); m.BloodEitr = has("m_bloodeitr");
                    if (has("m_mercy")) m.MercyPct = Mastery.MercyPct;
                    m.Whirl = has("m_whirl"); m.Slaughter = has("m_slaughter");
                }
                return m;
            }
        }

        // why not now, or null (the dome: nobody fights under it)
        internal static string Refused(Player p)
        {
            if (Dome.Inside(p.transform.position)) return Lang.T("Під куполом ніхто не б'ється — і ворон теж");
            return null;
        }

        // the raven is already out: one flight at a time per raven
        internal static bool Busy(Player p, bool munin) { return StrikeFlight.Out(p, munin) || (!munin && DecoyFlight.Out(p)); } // (Hugin out luring: busy too)

        // from the ability: pick the targets, roll the blows, fly. Training, the words and (on a miss) half the strength
        // back all come once it is back on the shoulder, by what its blows really did.
        internal static void Launch(Player p, bool munin, ItemDrop.ItemData item, float paid, List<Character> targets, float debt = 0f)
        {
            List<Peck> plan = new List<Peck>();
            Mods mods = Mods.For(munin, item, paid);
            if (mods.Odin) { if (targets.Count > 0) Mastery.OdinUsed(); else { mods.Odin = false; mods.BlindBoss = 0f; } } // «Око Одіна»: not spent on empty air
            float blow = Blow(munin, item), frost = GearFx.FrostShareOf(item), fire = FireOf(item), steal = RuneStars.Steal(item); // (this raven's, even if another is worn by the time it strikes)
            float pc = CritChance(munin, item), p2 = DoubleChance(munin, item), p3 = TripleChance(munin, item);
            // «Бойня»: the series takes in every one Munin has wounded near the first
            if (munin && mods.Slaughter && targets.Count > 0)
                foreach (Character c in Character.GetAllCharacters())
                    if (!targets.Contains(c) && Prey(p, c) && Marks.BleedingFrom(p, c) && Vector3.Distance(c.transform.position, targets[0].transform.position) <= Mastery.SlaughterRadius) targets.Add(c);
            foreach (Character t in targets)
            {
                int n = 1;
                double roll = Rng.NextDouble() * 100.0;
                // a series only on the first target: Munin rakes the other two in passing, once each
                if (t == targets[0]) { if (roll < p3) n = 3; else if (roll < p3 + p2) n = 2; }
                bool sneak = Unaware(t) && !Shadow.On(p); // struck unawares: a sure crit (not on top of Hugin's shadow, which has its own strike)
                if (sneak) mods.Sneaky.Add(t);
                for (int i = 0; i < n; i++)
                {
                    bool c = (i == 0 && sneak) || Rng.NextDouble() * 100.0 < pc;
                    if (i == 0 && Quirks.BraveCrit(p, munin, t)) c = true; // «Боязкий»: its first blow at a wounded boss
                    if (i == 0 && t == targets[0] && mods.Odin) c = true; // «Око Одіна»
                    plan.Add(new Peck { Target = t, Id = t.GetZDOID(), Crit = c, Sky = i == 0 && sneak && mods.Sky });
                }
            }
            if (debt > 0f && plan.Count > 0) plan[0].Debt = debt; // «Злість» III: the debt comes with the first blow
            // «Рикошет»: the blows on the first look like they finish it — the next one near it is taken too
            if (!munin && mods.Ricochet && targets.Count > 0)
            {
                Character first = targets[0];
                float sum = 0f;
                foreach (Peck k in plan) if (k.Target == first) sum += blow * (k.Crit ? mods.CritMul : 1f) * (k.Sky ? Mastery.SkyMul : 1f);
                Character next = !first.IsBoss() && sum >= first.GetHealth() ? Mastery.Nearest(p, first.transform.position, Mastery.RicochetReach, targets) : null;
                if (next != null) plan.Add(new Peck { Target = next, Id = next.GetZDOID(), Crit = Rng.NextDouble() * 100.0 < pc });
            }
            Vector3 miss = MissPoint(p, RangeOf(item));
            Dictionary<Character, float> capLeft = new Dictionary<Character, float>();
            Tally tally = new Tally();
            float range = RangeOf(item);
            float flown = Vector3.Distance(p.GetEyePoint(), miss); // with nobody to strike: how far it got before a wall or the ground
            // the raven has its say once it is back on the shoulder, with the corner's tally: not on the way out
            StrikeFlight.Fly(p, munin, plan, miss, (k, at) => Hit(p, munin, plan[k], blow, frost, fire, steal, capLeft, at, tally, mods), () => Home(p, munin, item, paid, tally, range, flown, mods),
                c => new Peck { Target = c, Id = c.GetZDOID(), Crit = Rng.NextDouble() * 100.0 < pc }); // (its first target fell to another: the next one)
            Send(p, munin, plan, miss);
        }

        // ---------------------------------------------------------------- «Вихор»: blow after blow, no landing between
        // One run of the raven's own blow for the whole whirl: its marks (blind, wound) once per target, the boss cap
        // anew every CapSpan seconds (else a boss would be done with after the first few blows).
        internal sealed class Volley
        {
            internal Mods Mods;
            internal float Blow, Frost, Fire, Steal, Crit;
            internal ItemDrop.ItemData Item;
            internal readonly Dictionary<Character, float> CapLeft = new Dictionary<Character, float>();
            internal float CapAt;
            internal int Hits;
            internal readonly Tally Tally = new Tally();
        }
        internal static float CapSpan = 10f;

        internal static Volley NewVolley(bool munin, ItemDrop.ItemData item)
        {
            Volley v = new Volley { Item = item, Mods = Mods.For(munin, item, 0f), CapAt = Time.time };
            v.Mods.Odin = false; v.Mods.BlindBoss = 0f; // («Око Одіна» is a strike's own, not spent on a whirl)
            v.Blow = Blow(munin, item); v.Frost = GearFx.FrostShareOf(item); v.Fire = FireOf(item); v.Steal = RuneStars.Steal(item);
            v.Crit = CritChance(munin, item);
            return v;
        }

        internal static Peck VolleyPeck(Volley v, Character c) { return new Peck { Target = c, Id = c.GetZDOID(), Crit = Rng.NextDouble() * 100.0 < v.Crit }; }

        // one blow of the whirl (the striker's game); bossMul: the share of the blow a boss gets
        internal static void VolleyHit(Player p, bool munin, Volley v, Peck k, Vector3 from, float mul, float bossMul, bool counts)
        {
            if (k == null || k.Target == null || k.Target.IsDead()) return;
            if (Time.time - v.CapAt >= CapSpan) { v.CapLeft.Clear(); v.CapAt = Time.time; }
            float blow = v.Blow * mul * (k.Target.IsBoss() ? bossMul : 1f);
            _volley = true; // (the whirl's blows: no spoils)
            try { Hit(p, munin, k, blow, v.Frost, v.Fire, v.Steal, v.CapLeft, from, v.Tally, v.Mods, !counts); }
            finally { _volley = false; }
            if (counts) v.Hits++;
        }

        // the whirl is over: training once for it if it struck anyone (as a strike's flight would)
        internal static void VolleyDone(Player p, bool munin, Volley v)
        {
            if (v == null || v.Hits <= 0 || v.Item == null) return;
            int before = Level(v.Item);
            Put(v.Item, XpKey, (Flights(v.Item) + 1).ToString());
            int after = Level(v.Item);
            if (after <= before || p == null) return;
            if (Array.IndexOf(Mastery.Milestones, after) >= 0) Mastery.Reached(p, munin, before, after);
            else RavenMsg.Center(p, Lang.F("{0}: вишкіл {1}-го рівня! Удар сильніший.", Plugin.RavenName(munin), after));
        }

        // back on the shoulder (or gone from it): what the flight earned, what it says, the line in the corner
        private static void Home(Player p, bool munin, ItemDrop.ItemData item, float paid, Tally tally, float range, float flown, Mods mods)
        {
            bool landed = tally.Order.Count > 0;
            int before = Level(item);
            // training comes from strikes that reached someone: a flight into empty air (or to a corpse) teaches nothing
            if (landed) Put(item, XpKey, (Flights(item) + 1).ToString());
            int after = Level(item);
            float back = 0f;
            if (!landed && MissRefund > 0f && paid > 0f) // (a free strike, «Відплата», took nothing: nothing back, no pause let off)
            {
                back = Power.Refund(item, paid * MissRefund);
                RavenSkills.LetOff(munin ? "mstrike" : "hstrike", MissRefund);
            }
            bool crit = false, combo = munin && tally.Order.Count > 1;
            foreach (Character c in tally.Order) { if (tally.Crits[c] > 0) crit = true; if (tally.Hits[c] > 1) combo = true; }
            ReportWhenBled(p, munin, tally, range, flown, back);
            if (p == null || p.IsDead()) return;
            if (landed && mods.Eitr) p.AddEitr(p.GetMaxEitr() * Mastery.EitrPct / 100f); // «Ейтир у пір'ї»
            if (after > before)
            {
                if (Array.IndexOf(Mastery.Milestones, after) >= 0) Mastery.Reached(p, munin, before, after);
                else RavenMsg.Center(p, Lang.F("{0}: вишкіл {1}-го рівня! Удар сильніший.", Plugin.RavenName(munin), after));
                RavenFx.Play("sfx_levelup", p.transform.position, Quaternion.identity);
            }
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(SayHome(p, munin, tally, landed, crit, combo, after > before, mods));
        }

        // The word after a strike, once it is known how it ended: a moment for the death to come over the network, and
        // for Munin until his wound has bled out (it may still kill). Killed (at least one of them): _kill, _crit_kill,
        // _combo_kill; a hit that killed nothing: _alive, something went wrong (Munin takes it hard); nothing reached: _miss. Not after every strike (SayChance); a new level of training always.
        internal static float SayChance = 0.7f;
        private static System.Collections.IEnumerator SayHome(Player p, bool munin, Tally tally, bool landed, bool crit, bool combo, bool levelUp, Mods mods)
        {
            float until = Time.time + Marks.WoundSeconds + 2f;
            yield return new WaitForSeconds(0.6f);
            while (munin && landed && Time.time < until && Marks.Bleeding(p, tally.Order)) yield return null;
            if (p == null || p.IsDead()) yield break;
            bool kill = false, silent = false;
            foreach (Character c in tally.Order) if (c == null || c.IsDead()) { kill = true; if (mods.Sneaky.Contains(c)) silent = true; }
            // «Тиха смерть»: one that never saw it is dead — half the strength and the pause back
            if (silent && mods.Silent && mods.Paid > 0f)
            {
                float back = Power.Refund(mods.Item, mods.Paid * Mastery.SilentBack);
                RavenSkills.LetOff("hstrike", Mastery.SilentBack);
                p.Message(MessageHud.MessageType.TopLeft, back >= 0.5f ? Lang.F("Тиха смерть: повернулось {0} сили, пауза коротша", R(back)) : Lang.T("Тиха смерть: пауза коротша"));
            }
            string pre = munin ? "mstrike" : "hstrike";
            string say = levelUp ? pre + "_level" : !landed ? pre + "_miss" : !kill ? (crit || combo) && UnityEngine.Random.value < 0.7f ? pre + (crit ? "_crit" : "_combo") : pre + "_alive" : crit ? pre + "_crit_kill" : combo ? pre + "_combo_kill" : pre + "_kill";
            if (levelUp || UnityEngine.Random.value < SayChance)
            {
                Composer.Extra.Clear();
                if (landed)
                {
                    Character first = tally.Order[0];
                    // the plain name always; its cases ({target_gen} {target_ins}) when creature_names.txt knows it
                    Composer.Extra["target"] = tally.Names[first];
                    if (Plugin.Instance != null && Plugin.Instance.PhraseDir != null) Composer.CreatureForms(Plugin.Instance.PhraseDir, Composer.Extra, "target", tally.Tokens[first]);
                }
                RavenSkills.StrikeSay(munin, say);
                Composer.Extra.Clear();
            }
            Quirks.StrikeHome(p, munin, crit, kill); // «Задавака»: shows off
        }

        private static Transform Aim()
        {
            Camera cam = Utils.GetMainCamera();
            return cam != null ? cam.transform : Player.m_localPlayer.transform;
        }

        // nobody to strike: it flies where the camera looks, as far as it reaches, but stops short of the ground, a wall or a tree in the
        // way (from the camera and from the player's eyes, so not through a wall behind a third-person camera)
        // and stays above the ground or water under that spot
        internal static float MissClear = 1.5f;

        private static Vector3 MissPoint(Player p, float range)
        {
            Transform aim = Aim();
            Vector3 eye = p.GetEyePoint();
            Vector3 dir = aim.forward;
            // as far as this raven reaches (its training), counted from the player, along the look
            float reach = range + Mathf.Max(0f, Vector3.Dot(eye - aim.position, dir));
            RaycastHit hit;
            Vector3 at = aim.position + dir * reach;
            if (Physics.Raycast(aim.position, dir, out hit, reach, Sight, QueryTriggerInteraction.Ignore)) at = hit.point - dir * MissClear;
            Vector3 way = at - eye;
            if (way.sqrMagnitude > 0.01f && Physics.SphereCast(eye, 0.3f, way.normalized, out hit, way.magnitude, Sight, QueryTriggerInteraction.Ignore))
                at = eye + way.normalized * Mathf.Max(1f, hit.distance - MissClear);
            float low = Floor(at) + MissClear;
            if (at.y < low) at.y = low;
            return at;
        }

        // what is under a spot: the terrain, a floor right under it (never a roof above it: indoors it stays indoors), the water
        internal static float Floor(Vector3 at)
        {
            RaycastHit hit;
            float ground = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(at) : float.MinValue;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out hit, 50f, Sight, QueryTriggerInteraction.Ignore)) ground = Mathf.Max(ground, hit.point.y);
            if (ZoneSystem.instance != null) ground = Mathf.Max(ground, ZoneSystem.instance.m_waterLevel);
            return ground;
        }

        private static int _sight;
        private static int Sight { get { if (_sight == 0) _sight = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small"); return _sight; } }

        internal static float MuninReach = 8f; // how far from the first target Munin goes on to the others

        internal static bool SeenFrom(Vector3 from, Character c) { return Clear(from, c, null, Body.Center(c)); }
        // its middle or its head (a raven on a perch: Home)
        internal static bool SeenFromAny(Vector3 from, Character c) { return Clear(from, c, null, Body.Center(c)) || Clear(from, c, null, Body.Head(c)); }

        // seen: its middle, or its head over a bush or a rise (a grazing deer's middle is often behind a tussock)
        internal static bool Seen(Player p, Character c) { Vector3 eye = p.GetEyePoint(); return Clear(eye, c, p, Body.Center(c)) || Clear(eye, c, p, Body.Head(c)); }
        internal static float ThreatNear = 10f; // m: a foe this close is the one to strike when none is in your sights
        internal static float AimBeyond = 60f; // m past the range a creature in your sights is still noticed (and said too far)

        // Nothing in the way but the creature itself (and the one looking): a modded creature's body is often on the
        // "Default" layer, not the game's "character" one, and the line to its middle hit its own hide ("not seen").
        // One cheap line first; only when it hits something is the whole line looked at.
        private static readonly RaycastHit[] _seenHits = new RaycastHit[16];
        private static bool Clear(Vector3 from, Character c, Character self, Vector3 to)
        {
            if (!Physics.Linecast(from, to, Sight, QueryTriggerInteraction.Ignore)) return true;
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(from, d / len, _seenHits, len, Sight, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = _seenHits[i].collider;
                if (col == null) continue;
                Transform t = col.transform;
                if (t.IsChildOf(c.transform) || (self != null && t.IsChildOf(self.transform))) continue;
                return false;
            }
            return true;
        }

        // A hunter: whatever lives and is not ours. The one in your sights, if it sees it and reaches it; else the
        // nearest one ahead; else the nearest one it can see at all, aside or behind you. Munin takes up to two more
        // next to the first. Nobody at all: it flies where you look all the same. 'note': why the one in your sights
        // was passed over (too far for this raven, hidden from it), for the corner.
        internal static List<Character> Targets(Player p, bool munin, float range, out string note, ItemDrop.ItemData item = null)
        {
            note = null;
            List<Character> found = new List<Character>();
            Transform aim = Aim();
            Vector3 from = aim.position, dir = aim.forward;
            Character inSight = null, ahead = null, any = null, sightOut = null, threat = null, foeAhead = null, foe = null;
            float sightScore = float.MaxValue, aheadScore = float.MaxValue, anyDist = float.MaxValue, outScore = float.MaxValue, outDist = 0f;
            float threatDist = float.MaxValue, foeAheadScore = float.MaxValue, foeDist = float.MaxValue;
            bool outFar = false;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Prey(p, c)) continue;
                float dist = Vector3.Distance(p.transform.position, c.transform.position);
                if (dist > range + AimBeyond) continue;
                Vector3 to = Body.Center(c) - from;
                float angle = Vector3.Angle(dir, to);
                float allow = Mathf.Max(AimDegrees, Mathf.Atan2(Body.Radius(c) + 0.6f, Mathf.Max(0.5f, to.magnitude)) * Mathf.Rad2Deg);
                float score = angle + dist * 0.15f;
                bool far = dist > range;
                if (far || !Seen(p, c))
                {
                    // in your sights, but not for this raven: remembered for the note in the corner, another one is taken
                    if (angle <= allow && score < outScore) { outScore = score; sightOut = c; outFar = far; outDist = dist; }
                    continue;
                }
                if (angle <= allow && score < sightScore) { sightScore = score; inSight = c; }
                if (angle <= 70f && score < aheadScore) { aheadScore = score; ahead = c; }
                if (dist < anyDist) { anyDist = dist; any = c; }
                if (!BaseAI.IsEnemy(p, c)) continue; // (a deer, a boar: only when no foe is about)
                MonsterAI ai = c.GetBaseAI() as MonsterAI;
                if ((dist <= ThreatNear || (ai != null && ai.GetTargetCreature() == p)) && dist < threatDist) { threatDist = dist; threat = c; }
                if (angle <= 70f && score < foeAheadScore) { foeAheadScore = score; foeAhead = c; }
                if (dist < foeDist) { foeDist = dist; foe = c; }
            }
            if (inSight == null && sightOut != null)
            {
                string who = Localization.instance != null ? Lang.Game(sightOut.m_name) : sightOut.m_name;
                note = outFar ? Lang.F("{0} задалеко — {1} м, долітає на {2}", who, R(outDist), R(range)) : Lang.F("{0} за перешкодою — не бачить", who);
                if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Strike aim: " + Utils.GetPrefabName(sightOut.gameObject) + " in the sights, " + R(outDist) + " m, " + (outFar ? "beyond the range " + R(range) : "hidden") + ": passed over");
            }
            // in your sights first; else a foe on you (close by or coming for you, wherever you look), then a foe ahead,
            // any foe, and only then the other game, ahead or near. A harmless one in the sights gives way to a foe on you.
            if (inSight != null && threat != null && !BaseAI.IsEnemy(p, inSight)) inSight = null;
            Character best = inSight ?? threat ?? foeAhead ?? foe ?? ahead ?? any;
            if (best == null) { WhyNone(p, range); return found; }
            if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Strike aim: " + Utils.GetPrefabName(best.gameObject) + " (" + (best == inSight ? "in the sights" : best == threat ? "foe on you" : best == foeAhead ? "foe ahead" : best == foe ? "nearest foe" : best == ahead ? "nearest ahead" : "nearest") + ")");
            found.Add(best);
            if (!munin) return found;
            // how many and how far: three within MuninReach, more with «Ще одна жертва» / «Зграя»
            int most = (Mastery.Has(item, "m_pack") ? Mastery.PackTargets : 3) + (Mastery.Has(item, "m_extra") ? Mastery.ExtraTargets : 0);
            float reach = Mastery.Has(item, "m_pack") ? Mathf.Max(MuninReach, Mastery.PackReach) : MuninReach;
            List<Character> near = new List<Character>();
            foreach (Character c in Character.GetAllCharacters())
                // the next ones: near the first (Munin flies on from there), and in sight from it, not from you
                if (c != best && Prey(p, c) && Vector3.Distance(c.transform.position, best.transform.position) < reach && SeenFrom(Body.Center(best), c)) near.Add(c);
            near.Sort((a, b) => Vector3.Distance(a.transform.position, best.transform.position).CompareTo(Vector3.Distance(b.transform.position, best.transform.position)));
            for (int i = 0; i < near.Count && found.Count < most; i++) found.Add(near[i]);
            return found;
        }

        // nobody to strike: what was near and why it was passed over, in the log (for creatures from other mods)
        private static void WhyNone(Player p, float range)
        {
            if (Plugin.Instance == null) return;
            List<string> l = new List<string>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == p || c.IsDead() || c is Player) continue;
                float dist = Vector3.Distance(p.transform.position, c.transform.position);
                if (dist > range + 10f) continue;
                string why = dist > range ? "far" : !Prey(p, c) ? "not prey (" + c.GetFaction() + (c.IsTamed() ? ", tamed" : "") + (c is Humanoid ? ", humanoid" : "") + ")" : !Seen(p, c) ? "not seen" : "?";
                l.Add(Utils.GetPrefabName(c.gameObject) + " " + Mathf.RoundToInt(dist) + " m: " + why);
                if (l.Count >= 8) break;
            }
            if (l.Count > 0) Plugin.Instance.Log("Strike found nobody within " + Mathf.RoundToInt(range) + " m. Near: " + string.Join("; ", l.ToArray()));
        }

        // anything alive that is not a player, not tamed and not of the friendly kinds (players' own folk and summons,
        // the dvergr): monsters asleep or unaware, deer and boars to hunt, the training dummy
        internal static bool Prey(Player p, Character c)
        {
            if (c == null || c == p || c.IsDead() || c is Player || c.IsTamed()) return false;
            if (BeingTamed(c)) return false; // fed and on its way to being tamed: not prey
            // someone, not something: a humanoid that is not hostile to you is a villager, a guard, a trader's folk (NPC mods)
            if (c is Humanoid && !BaseAI.IsEnemy(p, c)) return false;
            Character.Faction f = c.GetFaction();
            if (f == Character.Faction.Players || f == Character.Faction.PlayerSpawned || f == Character.Faction.Dverger) return false;
            ZNetView nv = c.GetComponent<ZNetView>();
            return nv != null && nv.IsValid();
        }

        // someone has started taming it: it was fed (and is not hungry yet), or its taming is under way
        internal static bool BeingTamed(Character c)
        {
            Tameable t = c.GetComponent<Tameable>();
            ZNetView nv = c.GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (t == null || zdo == null) return false;
            if (zdo.GetFloat(ZDOVars.s_tameTimeLeft, t.m_tamingTime) < t.m_tamingTime - 1f) return true;
            return zdo.GetLong(ZDOVars.s_tameLastFeeding, 0L) != 0L && !t.IsHungry();
        }

        // it did not see it coming: a monster not alerted, an animal grazing
        internal static bool Unaware(Character c)
        {
            BaseAI ai = c != null ? c.GetComponent<BaseAI>() : null;
            if (ai == null) return false;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid() && !nv.IsOwner()) return !nv.GetZDO().GetBool(ZDOVars.s_alert); // run in another game: its word, not our copy's
            return !ai.IsAlerted();
        }

        // ---------------------------------------------------------------- the blow (only the striker's game deals it)

        internal const float Marker = -1f; // m_skillRaiseAmount below this: a raven's blow, its size in it

        // what one flight did, for the line in the corner once the raven is back (its blows land far off, where the
        // game draws no numbers)
        internal sealed class Tally
        {
            public readonly List<Character> Order = new List<Character>();
            public readonly Dictionary<Character, float> Dealt = new Dictionary<Character, float>(), Bled = new Dictionary<Character, float>();
            public readonly Dictionary<Character, int> Hits = new Dictionary<Character, int>(), Crits = new Dictionary<Character, int>();
            public readonly Dictionary<Character, string> Names = new Dictionary<Character, string>();
            public readonly Dictionary<Character, string> Tokens = new Dictionary<Character, string>(); // the name key, for its cases in the words

            public void Add(Character c, float v, bool crit)
            {
                if (!Dealt.ContainsKey(c))
                {
                    Order.Add(c); Dealt[c] = 0f; Hits[c] = 0; Crits[c] = 0;
                    Names[c] = Localization.instance != null ? Lang.Game(c.m_name) : c.m_name;
                    Tokens[c] = c.m_name;
                }
                Dealt[c] += v; Hits[c]++;
                if (crit) Crits[c]++;
            }

            // Munin's wound, drop by drop (it bleeds on after the raven is back)
            public void AddBleed(Character c, float v)
            {
                if (c == null || !Dealt.ContainsKey(c)) return;
                float had;
                Bled.TryGetValue(c, out had);
                Bled[c] = had + v;
            }
        }

        // «Удар в очі: Сіролюдик −112 (2 удари, крит)» — one line, through the game's own corner messages: the game
        // shows it at the top left, a UI mod turns it into its own card with the gift's picture
        private static void Report(Player p, bool munin, Tally t, float range, float flown, float back)
        {
            if (p == null || p.IsDead()) return;
            string head = munin ? Lang.T("Кігті") : Lang.T("Удар в очі");
            string body;
            if (t.Order.Count == 0) body = Lang.F("нікого в межах {0} м{1}{2}", R(range), (flown < range - 3f ? Lang.F(" · пролетів {0} м, далі перешкода", R(flown)) : ""), (back >= 0.5f ? Lang.F(" · повернулось {0} сили", R(back)) : ""));
            else
            {
                List<string> parts = new List<string>();
                foreach (Character c in t.Order)
                {
                    int hits = t.Hits[c], crits = t.Crits[c];
                    List<string> notes = new List<string>();
                    if (hits > 1) notes.Add(Lang.N(hits, "удар|удари|ударів"));
                    if (crits > 0) notes.Add(crits > 1 ? Lang.F("крит ×{0}", crits) : Lang.T("крит"));
                    float bled;
                    if (munin && t.Bled.TryGetValue(c, out bled) && bled >= 0.5f) notes.Add(Lang.F("кровотеча −{0}", R(bled)));
                    if (c == null || c.IsDead()) notes.Add(Lang.T("вбито"));
                    else if (!munin) notes.Add(Lang.T("осліплено"));
                    parts.Add(t.Names[c] + " −" + R(t.Dealt[c]) + (notes.Count > 0 ? " (" + string.Join(", ", notes.ToArray()) + ")" : ""));
                }
                body = string.Join(" · ", parts.ToArray());
            }
            p.Message(MessageHud.MessageType.TopLeft, head + ": " + body, 0, AbilityIcons.For(munin ? "mstrike" : "hstrike", munin));
        }

        // Munin's wounds bleed on for a few seconds after he is back: the line waits for the last drop
        private static void ReportWhenBled(Player p, bool munin, Tally t, float range, float flown, float back)
        {
            if (!munin || t.Order.Count == 0 || Plugin.Instance == null || !Marks.Bleeding(p, t.Order)) { Report(p, munin, t, range, flown, back); return; }
            Plugin.Instance.StartCoroutine(ReportLater(p, munin, t, range, flown, back));
        }

        private static System.Collections.IEnumerator ReportLater(Player p, bool munin, Tally t, float range, float flown, float back)
        {
            float until = Time.time + Marks.WoundSeconds + 2f;
            while (Time.time < until && Marks.Bleeding(p, t.Order)) yield return null;
            Report(p, munin, t, range, flown, back);
        }

        // quiet: a blow that counts for nothing past the damage (a whirl's blows after its first ones: no «Зграя», no deeds)
        private static void Hit(Player p, bool munin, Peck peck, float blow, float frostShare, float fireShare, float steal, Dictionary<Character, float> capLeft, Vector3 from, Tally tally, Mods mods, bool quiet = false)
        {
            Character t = peck.Target;
            if (t == null || t.IsDead() || p == null) return;
            float v = blow * (peck.Crit ? mods.CritMul : 1f) * (peck.Sky ? Mastery.SkyMul : 1f);
            float hp = t.GetHealthPercentage();
            if (mods.EdgePct > 0f && hp < Mastery.EdgeHp) v *= 1f + mods.EdgePct / 100f; // «Ціль на межі»
            if (mods.MercyPct > 0f && (hp < Mastery.MercyHp || Marks.BleedingFrom(p, t))) v *= 1f + mods.MercyPct / 100f; // «Без жалю»
            if (t.m_faction == Character.Faction.Undead) v *= 1f + GearFx.UndeadPct(mods.Item) / 100f; // «Срібні кільця»
            if (t.IsBoss())
            {
                float left;
                if (!capLeft.TryGetValue(t, out left)) left = t.GetMaxHealth() * (munin ? BossCapMunin : BossCapHugin);
                v = Mathf.Min(v, left);
                capLeft[t] = left - v;
                if (v <= 0.5f && peck.Debt <= 0f) return;
            }
            if (peck.Debt > 0f) v += t.IsBoss() ? Mathf.Min(peck.Debt, t.GetMaxHealth() * DebtBossCap) : peck.Debt; // «Злість» III: the debt (a boss: its own cap)
            HitData hit = new HitData();
            float shares = Mathf.Max(1f, frostShare + fireShare + mods.Light); // (never more than the whole blow)
            float frost = v * frostShare / shares, fire = v * fireShare / shares, light = v * mods.Light / shares; // «Холодний медальйон»: part of it frost (the game's frost slows the target); «Сонце» ★★★: fire (it burns); «Іскра в дзьобі»: lightning
            if (munin) hit.m_damage.m_slash = v - frost - fire - light; else hit.m_damage.m_pierce = v - frost - fire - light;
            hit.m_damage.m_frost = frost;
            hit.m_damage.m_fire = fire;
            hit.m_damage.m_lightning = light;
            hit.m_point = Body.Center(t);
            Vector3 d = Body.Center(t) - from;
            hit.m_dir = d.sqrMagnitude > 0.001f ? d.normalized : t.transform.forward;
            hit.m_pushForce = munin ? 12f : 4f;
            hit.m_staggerMultiplier = munin ? 1.2f : 0.4f;
            hit.m_backstabBonus = 1f;
            hit.m_blockable = false;
            hit.m_dodgeable = false;
            hit.m_ranged = true;
            hit.m_skill = Skills.SkillType.None;
            hit.m_skillRaiseAmount = Marker - v; // carried to the target's game: there the blow is cut back to v
            hit.SetAttacker(p);
            t.Damage(hit);
            if (!_volley && !quiet) { if (munin) Twists.TrophyWatch(p, t); else Twists.Snatch(p, t); } // (II: «Кігті» a trophy off the fallen, «Удар в очі» a thing off the living)
            if (!quiet) Pack.OwnHit(p, munin, t, v, from); // («Зграя»: a friend's raven struck it a moment ago — a blow more)
            if (peck.Sky) t.Stagger(hit.m_dir); // «Каменем з неба»: knocked off its stride
            if (steal > 0f && !p.IsDead()) p.Heal(v * steal, true); // «Кров» ★★★
            if (mods.Feather > 0f) Mastery.AddShield(p, v * mods.Feather); // «Пір'яний щит»
            bool first = tally == null || !tally.Dealt.ContainsKey(t);
            bool firstTarget = tally == null || tally.Order.Count == 0;
            if (tally != null) tally.Add(t, v, peck.Crit);
            if (first)
            {
                // the raven's mark on it: Hugin blinds, Munin tears a bleeding wound
                Marks.Put(t, munin, mods, firstTarget);
                if (munin) Marks.StartBleed(p, t, blow, capLeft, tally != null ? new Action<Character, float>(tally.AddBleed) : null, mods);
                if (mods.MarkPct > 0f) Mastery.MarkAll(t, mods.MarkPct, mods.MarkSecs); // «Мітка мисливця»
                if (mods.Odin && firstTarget) Mastery.MarkAll(t, Mastery.OdinMarkPct, Mastery.OdinMarkSeconds); // «Око Одіна»
                if (mods.ResoPct > 0f) Mastery.MarkElements(t, mods.ResoPct, Mastery.ResoSeconds); // «Резонанс»
                if (mods.Thunder) // «Громовий дзьоб»
                {
                    Mastery.Splash(p, Body.Center(t), Mastery.ThunderRadius, blow * Mastery.ThunderShare, false, t, capLeft, munin);
                    BindFx.Nova(p, "chain", Body.Center(t), Mastery.ThunderRadius, 0.5f);
                }
                if (mods.Roar && firstTarget) // «Рев»
                {
                    Mastery.StaggerAround(p, t.transform.position, Mastery.RoarRadius);
                    BindFx.Nova(p, "gust", Body.Center(t), Mastery.RoarRadius, 0.5f);
                }
            }
            if (peck.Crit && DamageText.instance != null) DamageText.instance.ShowText(DamageText.TextType.Weak, hit.m_point + Vector3.up * 0.6f, Lang.F("КРИТ ×{0}", mods.CritMul.ToString("0.#")));
            if (peck.Crit && !quiet) Nicks.OnCrit(p, munin); // a deed for its nickname
        }

        // in the target's game, after armour and resistances: never more than the raven's blow
        internal static void Cut(HitData hit)
        {
            if (hit == null || hit.m_skillRaiseAmount > Marker) return;
            float want = Marker - hit.m_skillRaiseAmount;
            float total = hit.GetTotalDamage();
            if (total > want && total > 0f) hit.m_damage.Modify(want / total);
        }

        // ---------------------------------------------------------------- seen by the others

        private static void Send(Player p, bool munin, List<Peck> plan, Vector3 miss)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetZDOID());
            pkg.Write(munin);
            pkg.Write(miss);
            pkg.Write(plan.Count);
            foreach (Peck k in plan) { pkg.Write(k.Id); pkg.Write(k.Crit); }
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        private static void OnStrike(long sender, ZPackage pkg)
        {
            try
            {
                if (sender == ZDOMan.GetSessionID()) return;
                ZDOID who = pkg.ReadZDOID();
                bool munin = pkg.ReadBool();
                Vector3 miss = pkg.ReadVector3();
                int n = pkg.ReadInt();
                List<Peck> plan = new List<Peck>();
                for (int i = 0; i < n; i++)
                {
                    ZDOID id = pkg.ReadZDOID();
                    bool crit = pkg.ReadBool();
                    GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
                    Character c = go != null ? go.GetComponent<Character>() : null;
                    if (c != null) plan.Add(new Peck { Target = c, Id = id, Crit = crit });
                }
                Player me = Player.m_localPlayer;
                foreach (Player pl in Player.GetAllPlayers())
                {
                    if (pl == null || pl.GetZDOID() != who) continue;
                    if (me != null && Vector3.Distance(me.transform.position, pl.transform.position) > 120f) return;
                    StrikeFlight.Fly(pl, munin, plan, miss, null);
                    return;
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike message unreadable: " + e.Message); }
        }

        // ---------------------------------------------------------------- bosses that fell with the raven on the shoulder

        // A boss fight: the ravens on you when it began, known by the raven's own id (the same raven taken back from your
        // grave is a new copy of the item with the same id). They count if you or your raven struck that boss at least
        // once (Struck) and they are on you again when it falls: a death, a raven taken off or flown off in between
        // changes nothing. Only standing beside it counts for nothing: friends beat it, the ravens merely watched.
        private sealed class Fight
        {
            public string Prefab;
            public ItemDrop.ItemData Hugin, Munin; // on you at the start (null: none, or the boss was not fresh)
            public Vector3 Last;
            public float Seen; // when it was last near you, loaded and alive
        }

        // by the boss's own id in the world, not its loaded object: you die, wake far off at your bed (the boss unloads),
        // run back — the same fight goes on (a boss hit meanwhile would no longer be fresh)
        private static readonly Dictionary<ZDOID, Fight> Fights = new Dictionary<ZDOID, Fight>();
        internal static float WatchRange = 50f, FightKeepSeconds = 1200f;

        // the bosses you or your raven struck: told by your own hit (Diary's OutgoingHitPatch), so a change of who simulates
        // the boss loses nothing; only your hits on a boss come here, one entry a boss
        private static readonly HashSet<ZDOID> Struck = new HashSet<ZDOID>(); // session: a fight does not outlive the world's load (ResetFights)
        internal static void YouStruck(Character c) { if (Champions.TrueBoss(c) && Array.IndexOf(Bosses, Utils.GetPrefabName(c.gameObject)) >= 0) Struck.Add(c.GetZDOID()); } // (only the bosses a fight is kept for: another mod's boss would stay here for good)

        // the same raven: the same item, or one with its id (taken back from your grave)
        internal static bool Same(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            string ia = Backup.Id(a);
            return !string.IsNullOrEmpty(ia) && ia == Backup.Id(b);
        }

        // a boss fallen while the raven of its fight was not on you (in your grave): it counts once it is on you again
        private sealed class Owed { public ItemDrop.ItemData Raven; public bool Munin; public string Prefab; public float Until; }
        private static readonly List<Owed> Owing = new List<Owed>();
        internal static float OwedSeconds = 600f;

        // fresh: full of health, or risen just now (a mod that grows a boss's health as it appears, by its stars, leaves
        // it under full from the start; the game marks when each creature appeared)
        internal static float FreshSeconds = 45f;
        private static bool Fresh(Character c)
        {
            if (c.GetHealthPercentage() >= 0.98f) return true;
            ZNetView nv = c.GetComponent<ZNetView>();
            long born = nv != null && nv.IsValid() ? nv.GetZDO().GetLong(ZDOVars.s_spawnTime, 0L) : 0L;
            return born > 0L && ZNet.instance != null && (ZNet.instance.GetTime() - new DateTime(born)).TotalSeconds < FreshSeconds;
        }

        // twice a second: a boss near you, fresh, starts a fight with whichever ravens sit on you now
        internal static void Watch(Player p)
        {
            if (p == null) return;
            if (Later.Count > 0) TellLater(p);
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            if (h != null && (Trust.Away(h) || !Trust.Active(h))) h = null;
            if (m != null && (Trust.Away(m) || !Trust.Active(m))) m = null;
            if (Owing.Count > 0) PayOwed(p, h, m);
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || !c.IsBoss() || c.IsDead()) continue;
                string prefab = Utils.GetPrefabName(c.gameObject);
                if (Array.IndexOf(Bosses, prefab) < 0) continue;
                ZNetView nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                ZDOID id = nv.GetZDO().m_uid;
                float d = Vector3.Distance(p.transform.position, c.transform.position);
                Fight f;
                if (!Fights.TryGetValue(id, out f))
                {
                    if (p.IsDead() || d > WatchRange) continue;
                    bool fresh = Fresh(c);
                    Fights[id] = f = new Fight { Prefab = prefab, Hugin = fresh ? h : null, Munin = fresh ? m : null, Seen = Time.time };
                    Log("Boss fight " + prefab + ": hp " + Mathf.RoundToInt(c.GetHealthPercentage() * 100f) + "%" + (fresh ? "" : " (not fresh: it does not count)") + ", Hugin " + (f.Hugin != null) + ", Munin " + (f.Munin != null));
                }
                else if (!p.IsDead() && d <= WatchRange && Fresh(c)) // still untouched: whoever sits on you now is there from its start
                {
                    if (f.Hugin == null && h != null) f.Hugin = h;
                    if (f.Munin == null && m != null) f.Munin = m;
                }
                f.Last = c.transform.position;
                if (d <= WatchRange * 2f) f.Seen = Time.time;
            }
            if (Fights.Count > 0)
            {
                List<ZDOID> gone = null;
                foreach (KeyValuePair<ZDOID, Fight> kv in Fights) if (Time.time - kv.Value.Seen > FightKeepSeconds) { if (gone == null) gone = new List<ZDOID>(); gone.Add(kv.Key); } // (left it long ago, or its fall never reached you)
                if (gone != null) foreach (ZDOID g in gone) { Fights.Remove(g); Struck.Remove(g); }
            }
        }

        internal static void ResetFights() { Fights.Clear(); Struck.Clear(); Later.Clear(); Owing.Clear(); }

        // in the game where the boss dies: tell everyone which and where
        internal static void BossDied(Character c)
        {
            if (c == null || !c.IsBoss() || ZRoutedRpc.instance == null) return;
            string prefab = Utils.GetPrefabName(c.gameObject);
            if (Array.IndexOf(Bosses, prefab) < 0) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(prefab);
            pkg.Write(c.transform.position);
            // the last blow, if a raven's (its mark): whose and which (Munin tears, Hugin pierces) — added at the end, so
            // an older game reads the two fields above and stops
            HitData last = HitInfo.LastHit(c);
            Player by = last != null && last.m_skillRaiseAmount <= Marker ? last.GetAttacker() as Player : null;
            pkg.Write(by != null ? by.GetPlayerID() : 0L);
            pkg.Write(by != null && last.m_damage.m_slash > 0f);
            pkg.Write(c.GetZDOID()); // which of them (two of a kind may stand side by side)
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcBoss, pkg);
        }

        private static void OnBossDown(long sender, ZPackage pkg)
        {
            try
            {
                string prefab = pkg.ReadString();
                Vector3 at = pkg.ReadVector3();
                long felledBy = 0L;
                bool felledMunin = false;
                if (pkg.GetPos() < pkg.Size()) { felledBy = pkg.ReadLong(); felledMunin = pkg.ReadBool(); } // (an older game sends no more)
                ZDOID which = pkg.GetPos() < pkg.Size() ? pkg.ReadZDOID() : ZDOID.None;
                Player p = Player.m_localPlayer;
                if (p == null) return;
                ZDOID key = ZDOID.None;
                Fight fight = null;
                if (!which.IsNone()) { key = which; Fights.TryGetValue(which, out fight); }
                else foreach (KeyValuePair<ZDOID, Fight> kv in Fights)
                    if (kv.Value.Prefab == prefab && Vector3.Distance(kv.Value.Last, at) < 40f) { key = kv.Key; fight = kv.Value; break; }
                bool fought = Struck.Remove(key);
                if (fight == null) return;
                Fights.Remove(key);
                if (Vector3.Distance(p.transform.position, at) > WatchRange * 1.5f) return; // you were not there at the end
                Memory.OnBoss(p, prefab);
                PlayerReact.BossDown(p); // a fist to the sky, the ravens up circling
                if (!fought) { Log("Boss down " + prefab + ": you never struck it, it does not count"); return; }
                ItemDrop.ItemData h, m;
                RavenItem.Worn(p, out h, out m);
                if (Same(fight.Hugin, h)) Nicks.OnBoss(p, h, prefab); // each fall counts for the nicknames
                if (Same(fight.Munin, m)) Nicks.OnBoss(p, m, prefab);
                if (felledBy != 0L && felledBy == p.GetPlayerID()) Nicks.OnFelled(p, felledMunin ? m : h, prefab); // your raven struck the last blow
                Log("Boss down " + prefab + ": Hugin at start " + (fight.Hugin != null) + ", on you now " + Same(fight.Hugin, h) + "; Munin at start " + (fight.Munin != null) + ", on you now " + Same(fight.Munin, m));
                Settle(p, false, fight.Hugin, h, prefab);
                Settle(p, true, fight.Munin, m, prefab);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Boss message unreadable: " + e.Message); }
        }

        // the raven of the fight on you now: it counts; not on you (in your grave after a death): owed a while
        private static void Settle(Player p, bool munin, ItemDrop.ItemData atStart, ItemDrop.ItemData now, string prefab)
        {
            if (atStart == null) return;
            if (Same(atStart, now)) { Credit(p, munin, now, prefab); return; }
            Owing.Add(new Owed { Raven = atStart, Munin = munin, Prefab = prefab, Until = Time.time + OwedSeconds });
            Log("Boss down " + prefab + ": " + Plugin.RavenName(munin) + " not on you now, counted once it is back on you (" + Mathf.RoundToInt(OwedSeconds / 60f) + " min)");
        }

        private static void PayOwed(Player p, ItemDrop.ItemData h, ItemDrop.ItemData m)
        {
            for (int k = Owing.Count - 1; k >= 0; k--)
            {
                Owed o = Owing[k];
                ItemDrop.ItemData now = o.Munin ? m : h;
                if (Same(o.Raven, now)) { Owing.RemoveAt(k); Nicks.OnBoss(p, now, o.Prefab); Credit(p, o.Munin, now, o.Prefab); }
                else if (Time.time > o.Until) Owing.RemoveAt(k);
            }
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }

        private static void Credit(Player p, bool munin, ItemDrop.ItemData now, string prefab)
        {
            if (now == null) return;
            List<string> seen = BossesSeen(now);
            if (seen.Contains(prefab)) { Log("Boss " + prefab + ": " + Plugin.RavenName(munin) + " has it counted already"); return; }
            int tierBefore = Tier(now);
            seen.Add(prefab);
            Put(now, BossKey, string.Join(",", seen.ToArray()));
            int tierAfter = Tier(now);
            int i = Array.IndexOf(Bosses, prefab);
            bool stronger = tierAfter > tierBefore;
            float was = BlowWithTier(munin, now, tierBefore), becomes = BlowWithTier(munin, now, tierAfter);
            // not over the game's own banner of the boss's fall: a few seconds later, from the shoulder, and a line
            // in the corner that stays readable (Munin a moment after Hugin, so the two do not talk over each other)
            Later.Add(new News
            {
                At = Time.time + 6f + (munin ? 3f : 0f),
                Munin = munin,
                Tag = stronger ? "strike_stronger" : "strike_boss",
                Corner = Plugin.RavenName(munin) + (stronger ? Lang.F(": удар сильнішає — {0} → {1}", R(was), R(becomes))
                    : i > tierAfter && tierAfter < BossNames.Length ? Lang.F(" бачив, як упав {0}. Зарахується, коли переможете {1}", BossNames[i], BossNames[tierAfter]) // (out of turn: kept for later)
                    : Lang.F(" бачив, як упав {0}", BossNames[i])),
            });
        }

        private sealed class News { public float At; public bool Munin; public string Tag, Corner; }
        private static readonly List<News> Later = new List<News>();

        private static void TellLater(Player p)
        {
            for (int k = Later.Count - 1; k >= 0; k--)
            {
                News n = Later[k];
                if (Time.time < n.At) continue;
                Later.RemoveAt(k);
                if (p == null || p.IsDead()) continue;
                p.Message(MessageHud.MessageType.TopLeft, n.Corner);
                ScreamFx.CawAll(p, n.Munin, n.Munin ? 0.85f : 1.1f, 0.8f);
                RavenSkills.StrikeSay(n.Munin, n.Tag);
            }
        }
    }

    // ---------------------------------------------------------------- the raven in the air

    // The very raven from the shoulder lets go, flies an arc to its target, dives, strikes (once or in a series),
    // swings on to the next target, then comes back and lands. Every game flies its own copy; only the striker's
    // game deals the blows (onImpact).
    internal class StrikeFlight : MonoBehaviour, IRavenUser
    {
        // only leaving the shoulder is stronger: the raven goes (the leaving flies it on), the strike is over
        private bool _yielded;
        public void Yield()
        {
            _yielded = true;
            if (_onHome != null) { Action home = _onHome; _onHome = null; try { home(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike report failed: " + e.Message); } }
            DestroyImmediate(this);
        }

        private Player _owner;
        private bool _munin;
        private List<Strike.Peck> _plan;
        private Vector3 _miss;
        private Action<int, Vector3> _onImpact;
        private Action _onHome; // the striker's game: the flight's result, once the raven is back
        private ShoulderPin _pin;
        private ShoulderLife _life;
        private bool _clone; // no raven was drawn on the shoulder: a stand-in that goes away at the end
        private Unhooked _hook; // the shoulder's raven, let go of the body's bone while it flies

        private int _step;          // which peck now; _plan.Count: going home
        private int _phase;         // 0 out to the target, 1 bouncing back after the blow, 2 home
        private Vector3 _from;
        private float _t0;
        private float _born;
        private float _missDiveAt, _missDiveUntil;
        private bool _missTurnoutSet;
        private RavenMotor _motor;  // the raven's motor: flies it (and keeps its speed when it was taken in the air)
        private FlightBody _body;   // the motor's body for this flight (RavenFlight)
        private ParticleSystem _trail; // the feathers it sheds on the way
        private float _lastNear = float.MaxValue; // the new flight: closest it got to this target (passing it counts as there)
        private Vector3 _reOffset;  // after a blow, where the next dive starts, off the prey: up and back the way it came in
        private Func<Character, Strike.Peck> _repick;              // the striker's game: a new peck (its crit rolled) at another target
        private readonly HashSet<Character> _struck = new HashSet<Character>(); // the ones this flight has hit
        private int _retargets;
        // «Вихор»: no way home between blows — with nobody to strike it circles round you (phase 3) and takes the next
        // one the whirl gives; quicker by _pace. Ends by WhirlEnd (home, as after a strike).
        private Func<Character> _whirl;
        private float _pace = 1f, _orbitA, _nextLook;
        internal static float OrbitR = 2.6f, OrbitUp = 2.2f, OrbitTurn = 1.6f, WhirlLeash = 13f;

        // both ravens of a whirl, every game: the striker's game deals the blows (onImpact), the others only see it
        internal static bool Whirl(Player owner, bool munin, Func<Character> next, Func<Character, Strike.Peck> pick, Action<int, Vector3> onImpact, float pace)
        {
            if (owner == null) return false;
            Fly(owner, munin, new List<Strike.Peck>(), owner.GetEyePoint() + Vector3.up * OrbitUp, onImpact, null, pick);
            StrikeFlight f;
            if (!Flying[munin ? 1 : 0].TryGetValue(owner, out f) || f == null) return false;
            f._whirl = next; f._pace = pace; f._missDiveAt = 0f;
            f._orbitA = Mathf.Atan2(f.transform.position.z - owner.transform.position.z, f.transform.position.x - owner.transform.position.x);
            f.Begin(0, 3);
            return true;
        }

        // the peck a whirl's blow is for (the striker's game: onImpact gives its place in the plan)
        internal static Strike.Peck PeckOf(Player owner, bool munin, int k)
        {
            StrikeFlight f;
            return owner != null && Flying[munin ? 1 : 0].TryGetValue(owner, out f) && f != null && k >= 0 && k < f._plan.Count ? f._plan[k] : null;
        }

        internal static bool Whirling(Player owner, bool munin)
        {
            StrikeFlight f;
            return owner != null && Flying[munin ? 1 : 0].TryGetValue(owner, out f) && f != null && f._whirl != null;
        }

        // the whirl is over: home from wherever it is, the shoulder's landing as after a strike
        internal static void WhirlEnd(Player owner, bool munin)
        {
            StrikeFlight f;
            if (owner == null || !Flying[munin ? 1 : 0].TryGetValue(owner, out f) || f == null || f._whirl == null) return;
            f._whirl = null;
            if (f._phase != 2) { f._step = f._plan.Count; f.Begin(f._plan.Count, 2); }
        }

        // round you with nobody to strike: an eye open for the next one a few times a second
        private void Orbit(float dt)
        {
            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + 0.2f;
                Character c = _whirl();
                if (c != null && _repick != null) { _plan.Add(_repick(c)); Begin(_plan.Count - 1, 0); _motor.Finish(dt, 0f); return; }
            }
            _orbitA += dt * OrbitTurn * _pace * (_munin ? -1f : 1f); // (the two go round each other's way)
            Vector3 at = _owner.transform.position + Vector3.up * (OrbitUp + (_munin ? 0.5f : 0f)) + new Vector3(Mathf.Cos(_orbitA), 0f, Mathf.Sin(_orbitA)) * OrbitR;
            _motor.Steer(at, Strike.Speed * 0.8f * _pace, false, 0.3f, dt, Vector3.zero, 1f, true);
            _motor.Finish(dt, 0f);
        }

        private static readonly Dictionary<Player, StrikeFlight>[] Flying = { new Dictionary<Player, StrikeFlight>(), new Dictionary<Player, StrikeFlight>() };

        // the bird in the air for this player's strike (the shoulder model or its stand-in), or null
        internal static GameObject FlyerOf(Player p, bool munin)
        {
            StrikeFlight f;
            return p != null && Flying[munin ? 1 : 0].TryGetValue(p, out f) && f != null ? f.gameObject : null;
        }

        internal static bool Out(Player p, bool munin)
        {
            StrikeFlight f;
            return p != null && Flying[munin ? 1 : 0].TryGetValue(p, out f) && f != null;
        }

        internal static void Fly(Player owner, bool munin, List<Strike.Peck> plan, Vector3 miss, Action<int, Vector3> onImpact) { Fly(owner, munin, plan, miss, onImpact, null); }

        internal static void Fly(Player owner, bool munin, List<Strike.Peck> plan, Vector3 miss, Action<int, Vector3> onImpact, Action onHome, Func<Character, Strike.Peck> repick = null)
        {
            if (owner == null) return;
            GameObject model = Shoulder.ModelOf(owner, munin);
            if (model != null && !AnimDirector.CanTake(model, RavenAct.Strike)) model = null; // leaving for good: a stand-in strikes
            bool clone = false;
            if (model == null)
            {
                model = StandIn(owner, munin);
                clone = true;
                if (model == null) { if (onImpact != null) for (int i = 0; i < plan.Count; i++) onImpact(i, owner.GetEyePoint()); if (onHome != null) onHome(); return; }
            }
            StrikeFlight old = model.GetComponent<StrikeFlight>();
            if (old != null) DestroyImmediate(old); // at once: its clean-up (pin back on) must not come after this flight began
            StrikeFlight f = model.AddComponent<StrikeFlight>();
            f._owner = owner; f._munin = munin; f._plan = plan; f._miss = miss; f._onImpact = onImpact; f._onHome = onHome; f._clone = clone; f._repick = repick;
            f._pin = model.GetComponent<ShoulderPin>();
            f._life = model.GetComponent<ShoulderLife>();
            f._motor = RavenMotor.Of(model, munin, owner);
            AnimDirector.Take(model, RavenAct.Strike, f, clone ? "strike, a stand-in" : "strike"); // whoever had it lets go first
            if (f._pin != null) f._pin.enabled = false; // let go of the shoulder
            if (!clone) f._hook = Unhooked.From(model.transform);
            f._born = Time.time;
            f._missDiveAt = plan.Count == 0 ? f._born + 0.45f : 0f;
            f.Begin(0, 0);
            // a jump off the shoulder towards where it goes, up and out
            Vector3 to = f.Target() - model.transform.position;
            Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : owner.transform.forward;
            f._motor.Launch(dir * 3f + Vector3.up * 2.5f, "удар", 0.7f); // more inertia in a strike's flight
            f._body = f._motor.Body;
            Flying[munin ? 1 : 0][owner] = f;
            if (owner == Player.m_localPlayer || !Shadow.Flagged(owner)) f._trail = Feathers.Trail(model.transform, munin); // no trail to follow to someone in the shadow
            RavenFx.Play("sfx_raven_flap", model.transform.position, Quaternion.identity);
        }

        // the landing panel: the shoulder raven put this far off in the air and sent home, as after a strike
        internal static bool TestHome(Player owner, bool munin, Vector3 from)
        {
            if (owner == null) return false;
            GameObject model = Shoulder.ModelOf(owner, munin);
            if (model == null || model.GetComponent<StrikeFlight>() != null || !AnimDirector.CanTake(model, RavenAct.Strike)) return false;
            StrikeFlight f = model.AddComponent<StrikeFlight>();
            f._owner = owner; f._munin = munin; f._plan = new List<Strike.Peck>(); f._miss = from;
            f._pin = model.GetComponent<ShoulderPin>();
            f._life = model.GetComponent<ShoulderLife>();
            f._motor = RavenMotor.Of(model, munin, owner);
            bool flying = f._motor.Airborne; // already in the air (an outing): sent home from where it is, at its speed (the takeover test)
            AnimDirector.Take(model, RavenAct.Strike, f, "test landing");
            if (f._pin != null) f._pin.enabled = false;
            f._hook = Unhooked.From(model.transform);
            f._born = Time.time;
            if (!flying) model.transform.position = from;
            Vector3 to = owner.GetEyePoint() - from;
            if (f._motor.Launch((to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward) * 4f, "додому", 1f)) f._motor.Body.Airborne();
            f._body = f._motor.Body;
            f.Begin(0, 2);
            Flying[munin ? 1 : 0][owner] = f;
            return true;
        }

        internal static GameObject StandIn(Player owner, bool munin)
        {
            Raven source = Shoulder.Template(munin);
            if (source == null || source.m_visual == null) return null;
            GameObject go = Instantiate(source.m_visual, owner.GetEyePoint() + owner.transform.right * (munin ? -0.3f : 0.3f), owner.transform.rotation);
            go.SetActive(true);
            AppearMute.Quiet(go); // (the shoulder's raven off on its strike: no appearing out of nowhere)
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Shared.RemoveExclamation(source, go);
            go.transform.localScale = source.m_visual.transform.lossyScale * Shoulder.Scale * Shoulder.SizeFor(Mathf.Max(1, Trust.LevelWorn(munin))); // the shoulder raven's own size, not the game's big one
            Shoulder.Perch(go.GetComponentInChildren<Animator>());
            ShoulderLife life = go.AddComponent<ShoulderLife>();
            life.Init(owner, munin, Mathf.Max(1, Trust.LevelWorn(munin)));
            RavenWear.Dress(go, munin, RavenWear.OnPlayer(owner, munin));
            return go;
        }

        private bool _whistled;
        private void Begin(int step, int phase)
        {
            _step = step; _phase = phase;
            _whistled = false;
            _from = transform.position;
            _lastNear = float.MaxValue;
            _t0 = Time.time;
        }

        private Character Current() { return _step < _plan.Count ? _plan[_step].Target : null; }

        private Vector3 Home()
        {
            if (_pin != null && _pin.Bone != null) return _pin.Bone.position + Vector3.up * 0.1f;
            return _owner != null ? _owner.GetEyePoint() : transform.position;
        }

        private Vector3 Target()
        {
            if (_phase == 2) return Home();
            if (_plan.Count == 0) return _miss;
            Character c = Current();
            Vector3 aim = c != null ? Body.Center(c) : transform.position;
            if (_phase == 1) return aim + _reOffset; // set at the blow (it follows the prey, not the bird's own swing)
            return c != null ? aim + Approach(aim) : aim;
        }

        // The raven's own manner on the way in (its character): Hugin, a hunter, keeps high and swings out to his side,
        // then comes down onto the prey from above, steeply over the last metres; Munin, a brawler, goes straight in.
        // Gone by StoopNear m off the prey: the blow itself is exact.
        internal static float StoopNear = 2.5f, StoopSpan = 4f;
        private Vector3 Approach(Vector3 aim)
        {
            FlightCharacter ch = _motor.Char();
            if (ch.StoopUp <= 0.01f && ch.ArcSide <= 0.01f) return Vector3.zero;
            Vector3 to = aim - transform.position;
            float far = Mathf.Clamp01((to.magnitude - StoopNear) / Mathf.Max(0.5f, StoopSpan));
            if (far <= 0f) return Vector3.zero;
            float k = far * far * (3f - 2f * far);
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
            Vector3 side = flat.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, flat.normalized) * (_munin ? -1f : 1f) : Vector3.zero;
            return Vector3.up * (ch.StoopUp * k) + side * (ch.ArcSide * k);
        }

        // from LateUpdate, after the game's animation, right before the wings are sampled: a body with speed chasing the target; the blow when it gets there, bounced back and up; home, it brakes
        // to a hover over the shoulder, turns your way, and the game's own landing sets it down
        private void Step()
        {
            float dt = Time.deltaTime;
            if (_owner == null || _owner.IsDead() || (_whirl == null && Time.time - _born > 90f)) { Land(); return; }
            if (_phase == 3) { Orbit(dt); return; }
            // a whirl's prey left behind (you ran on): the next one near you
            if (_whirl != null && _phase == 0 && Current() != null && Vector3.Distance(_owner.transform.position, Current().transform.position) > WhirlLeash) { Next(); return; }
            // a whirl's dive: a whistle as it goes in (every game hears its own copy)
            if (_whirl != null && _phase == 0 && !_whistled && Current() != null && Vector3.Distance(transform.position, Body.Center(Current())) < 6f) { _whistled = true; PairFx.Whistle(transform.position); }
            if (_phase == 0 && _plan.Count == 0 && _missDiveUntil <= 0f && Time.time >= _missDiveAt)
            {
                _missDiveUntil = Time.time + 2f;
                _motor.SetPlan("пікірування");
            }
            else if (_phase == 0 && _plan.Count == 0 && _missDiveUntil > 0f && Time.time >= _missDiveUntil && _motor.Plan == "пікірування")
                _motor.SetPlan("удар");
            if (_phase == 0 && _plan.Count > 0 && (Current() == null || Current().IsDead())) { Next(); return; }
            Vector3 to = Target();
            float near = Vector3.Distance(transform.position, to);
            if (_phase == 2)
            {
                // home: the motor's way home, the same for every flight (waves on the long way, then the landing)
                Vector3 carry = _owner.GetVelocity();
                if (_motor.Home(to, _owner, _body.HomeSpeed(Strike.ReturnSpeed, carry, Time.time - _t0), dt, true, _born, false, Vector3.zero) || _motor.Stuck)
                {
                    if (ShoulderLife.LogLandings && ReferenceEquals(_owner, Player.m_localPlayer) && Plugin.Instance != null) Plugin.Instance.Log("Landing (strike): facing " + Mathf.RoundToInt(_body.YawOff(_owner.transform.forward)) + "° off the owner's way, " + Mathf.RoundToInt(Vector3.Distance(transform.position, to) * 100f) + " cm from the seat, speed " + _body.Speed.ToString("0.0"));
                    Land();
                }
                return;
            }
            if (_phase == 1)
            {
                if (Current() == null || Current().IsDead()) { Next(); return; } // it fell before the next dive
                float up = 6f * Mathf.Clamp(_motor.Char().TurnCap / 175f, 0.75f, 1.3f) * _pace; // Munin back up quicker
                float left = _motor.Steer(to, up, true, 2f, dt, Vector3.zero, 1f, true);
                // there, or out of time (Hugin climbs higher: he gets the longer while)
                if (left < 0.5f || Time.time - _t0 > Mathf.Max(0.6f, Strike.SeriesGap * 2.5f, _reOffset.magnitude / up + 0.4f)) Begin(_step, 0);
            }
            else
            {
                // at prey: fast, and the last metres a little slower so it can turn onto it; nobody there: to the
                // spot, then home. Close and getting farther: it went past, so it is there. It keeps clear of the
                // ground and trees on the way, not in the last metres of the dive (the prey is on the ground)
                float speed = Strike.Speed * 1.1f * _pace;
                if (_plan.Count > 0 && near < 4f) speed = Mathf.Lerp(6f * _pace, speed, near / 4f);
                bool missedStrike = _plan.Count == 0;
                bool diveComplete = missedStrike && _missDiveUntil > 0f && Time.time >= _missDiveUntil;
                if (diveComplete && !_missTurnoutSet)
                {
                    Vector3 forward = Vector3.ProjectOnPlane(_body.Vel, Vector3.up);
                    if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(_miss - transform.position, Vector3.up);
                    if (forward.sqrMagnitude > 0.01f)
                    {
                        // on and a little up out of the dive, never lower than over the ground or the water there
                        _miss = transform.position + forward.normalized * 4f + Vector3.up * 0.5f;
                        _miss.y = Mathf.Max(_miss.y, Strike.Floor(_miss) + Strike.MissClear);
                        _lastNear = float.MaxValue;
                    }
                    _missTurnoutSet = true;
                    to = Target();
                    near = Vector3.Distance(transform.position, to);
                }
                float sharp = missedStrike ? 0.3f : near < 6f ? _motor.Char().StrikeSharp : 0.3f; // the last dive: Munin snaps onto it, Hugin swoops
                float left = _motor.Steer(to + _body.Wave(near, _born), speed, false, sharp, dt, Vector3.zero, Mathf.Clamp01((near - 3f) / 4f), true);
                bool passed = left < 2f && left > _lastNear + 0.05f;
                _lastNear = Mathf.Min(_lastNear, left);
                if (missedStrike)
                {
                    if (diveComplete && (left < 1.5f || passed || Time.time - _t0 > 15f)) Begin(_plan.Count, 2);
                }
                else if (left < Contact() || passed) Impact();
                else if (Time.time - _t0 > ReachSeconds / Mathf.Max(0.5f, _body.Pace)) Skip(); // never got there (ran off, out of its way): no blow from afar (a slow raven gets its own longer while)
            }
            if (this == null) return;
            _motor.Finish(dt, 0f);
        }

        internal static float ReachSeconds = 12f;
        internal static bool LogPecks = false; // a line per blow in the log: who, when, how far (to look into the series in a test)
        private float _lastPeck;

        // how near the prey's middle counts as on it: a big one is struck on its hide (the motor may not let the bird
        // into a body that is not on the creatures' layers, a modded beast's)
        private float Contact() { Character c = Current(); return 0.8f + (c != null ? Mathf.Min(Body.Radius(c) * 0.7f, 2f) : 0f); } // (a dragon's radius would count a blow from afar)

        private void Impact()
        {
            Character c = Current();
            // the next dive's start, from the way it came in (before the bounce turns it round)
            FlightCharacter ch = _motor.Char();
            Vector3 cameIn = Vector3.ProjectOnPlane(_body.Vel, Vector3.up);
            _reOffset = Vector3.up * ch.ReUp - (cameIn.sqrMagnitude > 0.01f ? cameIn.normalized : Vector3.zero) * ch.ReBack;
            Vector3 at = c != null ? Body.Center(c) : transform.position;
            if (c != null) _struck.Add(c);
            Peck("blow", c, at);
            Raven template = Shoulder.Template(_munin);
            if (template != null && template.m_despawnEffect != null) RavenFx.Own(template.m_despawnEffect, at, Quaternion.identity, null, 0.35f, -1); // a puff of feathers
            RavenFx.Play("sfx_raven_kaw", at, Quaternion.identity);
            if (_life != null) _life.Flap(0.35f);
            if (_onImpact != null) { try { _onImpact(_step, _from); } catch (Exception ex) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike blow failed: " + ex.Message); } }
            // after the blow (it may have killed): the same one again — back and up for the next dive; another one —
            // on through, the blow takes some of its speed and lifts it a little, and the motor turns it to the next
            Character nx = NextTarget();
            if (nx == null || nx == c) _body.Bounce(ch.BounceBack, ch.BounceUp); // Hugin bounces up and off, Munin stays on it
            else _body.Kick(_body.Vel * 0.75f + Vector3.up * 1.5f);
            Next();
        }

        private void Skip()
        {
            Peck("skipped, not reached", Current(), transform.position);
            Next();
        }

        private void Peck(string what, Character c, Vector3 at)
        {
            if (!LogPecks || Plugin.Instance == null || !ReferenceEquals(_owner, Player.m_localPlayer)) return;
            float since = _lastPeck > 0f ? Time.time - _lastPeck : Time.time - _born;
            _lastPeck = Time.time;
            Plugin.Instance.Log("Strike " + (_munin ? "Munin" : "Hugin") + " peck " + (_step + 1) + "/" + _plan.Count + " " + what + ": "
                + (c != null ? Utils.GetPrefabName(c.gameObject) : "-") + (_step < _plan.Count && _plan[_step].Crit ? " crit" : "")
                + ", " + since.ToString("0.00") + " s after the last, " + Vector3.Distance(transform.position, at).ToString("0.0") + " m off, speed " + _body.Speed.ToString("0.0"));
        }

        // the next living one in the plan (dead ones are passed over), or null
        private int NextIndex()
        {
            int next = _step + 1;
            while (next < _plan.Count && (_plan[next].Target == null || _plan[next].Target.IsDead())) next++;
            return next;
        }

        private Character NextTarget() { int n = NextIndex(); return n < _plan.Count ? _plan[n].Target : null; }

        // Its target fell to someone else before it got there: rather than flying home with nothing (and the strike
        // pressed again), it takes the nearest living one it can see near it — up to Retargets times a flight. One
        // it struck and killed itself is a job done (that would be a free «Рикошет»).
        internal static int Retargets = 3;
        internal static float RetargetReach = 15f;

        private bool Retarget()
        {
            if (_retargets >= Retargets || _owner == null || _plan.Count == 0) return false;
            bool lost = false;
            foreach (Strike.Peck k in _plan) if ((k.Target == null || k.Target.IsDead()) && !_struck.Contains(k.Target)) { lost = true; break; }
            if (!lost) return false;
            Character best = null;
            float bd = RetargetReach;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Strike.Prey(_owner, c) || _struck.Contains(c)) continue;
                bool planned = false;
                foreach (Strike.Peck k in _plan) if (k.Target == c) { planned = true; break; }
                if (planned) continue;
                float d = Vector3.Distance(transform.position, Body.Center(c));
                if (d >= bd || Vector3.Distance(_owner.transform.position, c.transform.position) > Strike.RangeMaster || !Strike.SeenFrom(transform.position, c)) continue;
                bd = d; best = c;
            }
            if (best == null) return false;
            _retargets++;
            _plan.Add(_repick != null ? _repick(best) : new Strike.Peck { Target = best, Id = best.GetZDOID() }); // (another's game: the look only)
            Peck("retarget", best, Body.Center(best));
            return true;
        }

        // the same target again (a series): up and dive; another target: straight on; none left: home
        private void Next()
        {
            int next = NextIndex();
            if (_whirl != null && next >= _plan.Count)
            {
                // the whirl gives the next (maybe the same one again: a series); nobody — round you till someone comes
                Character c = _whirl();
                if (c == null || _repick == null) { _step = _plan.Count; Begin(_plan.Count, 3); return; }
                _plan.Add(_repick(c));
                next = _plan.Count - 1;
            }
            if (next >= _plan.Count && Retarget()) next = NextIndex();
            if (next >= _plan.Count) { _step = _plan.Count; Begin(_plan.Count, 2); return; }
            bool same = _plan[next].Target == Current() && Current() != null && !Current().IsDead();
            _step = next;
            Begin(next, same ? 1 : 0);
        }

        private void Land()
        {
            if (ReferenceEquals(_owner, Player.m_localPlayer)) RavenFlight.Note(_munin, -1f, 0f, false);
            Flying[_munin ? 1 : 0].Remove(_owner);
            if (_onHome != null) { Action home = _onHome; _onHome = null; try { home(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike report failed: " + e.Message); } }
            AnimDirector.Release(gameObject, this); // it sits (pose flags off); the pin takes it for the landing
            Feathers.EndTrail(_trail); _trail = null;
            if (_clone) { Destroy(gameObject); return; }
            if (_pin != null)
            {
                Vector3 from = transform.position;
                if (_hook != null) { _hook.Back(); _hook = null; }
                _pin.enabled = true;
                _pin.ComeBack(from, transform.rotation); // the game's own landing, onto the shoulder (from far: flown in again)
            }
            Destroy(this);
        }

        private void LateUpdate()
        {
            Step();
            if (this != null && _life != null && (_pin == null || !_pin.enabled)) _life.Apply();
        }

        private void OnDestroy()
        {
            StrikeFlight f;
            if (_owner != null && Flying[_munin ? 1 : 0].TryGetValue(_owner, out f) && f == this) Flying[_munin ? 1 : 0].Remove(_owner);
            if (_hook != null) { _hook.Back(); _hook = null; }
            if (_pin != null && !_clone && !_yielded) _pin.enabled = true; // yielded: the leaving holds the bird (the pin goes)
            Feathers.EndTrail(_trail); _trail = null;
            // gone before it was home (the bird itself destroyed: the shoulder rebuilt, a relog): the flight still counts
            if (_onHome != null) { Action home = _onHome; _onHome = null; try { home(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike report failed: " + e.Message); } }
            if (ReferenceEquals(_owner, Player.m_localPlayer)) RavenFlight.Note(_munin, -1f, 0f, false);
            if (!_yielded) AnimDirector.Release(gameObject, this);
        }
    }

    // ---------------------------------------------------------------- patches

    // the last word on a raven's blow: cut back to its own size, whatever else was added on the way
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class StrikeCutPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(HitData hit) {
            try
            {
                Strike.Cut(hit);
            }
            catch (System.Exception pgx) { PatchGuard.Fail("StrikeCutPatch", pgx); }
        }
    }

    [HarmonyPatch(typeof(Character), "OnDeath")]
    internal static class StrikeBossDeathPatch
    {
        private static void Prefix(Character __instance) {
            try
            {
                Strike.BossDied(__instance);
            }
            catch (System.Exception pgx) { PatchGuard.Fail("StrikeBossDeathPatch", pgx); }
        }
    }
}
#endif
