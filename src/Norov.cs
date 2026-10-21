#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Норов»: who this one raven is, beyond being Hugin or Munin. Three parts:
    //   Характер — how it behaves: how often it flies off by itself, for how long and where to, how restless it is on
    //              the shoulder, a touch of its manner in the air (±8% of the flight character). It shows more with
    //              trust: a trust-1 raven has about a third of it, at trust 7 all of it.
    //   Хист     — one strength, always a plus, a fixed number the book shows as it is.
    //   Примха   — one small habit of its own, a minus with life in it (what each does: Quirks.cs); trust leaves it be.
    // Rolled once for a bought raven (Perks.Sync), kept in the item (m_customData, Backup copies it with the rest).
    // The character goes onto the player's ZDO as well: every game flies the bird, so every game must know it.
    // Ids are stable: never rename them (they are saved in the ravens, and phrases will hang on Tag).
    internal static class Norov
    {
        internal const string CharKey = "RavenWatch_char", KnackKey = "RavenWatch_knack", QuirksKey = "RavenWatch_quirks";
        internal const string NetKey = "RavenWatch_Norov"; // on the player's ZDO: Hugin's character index + 1, Munin's ×16

        internal sealed class Character
        {
            public string Id;
            public string NameSrc, TextSrc; // the source texts (Ukrainian)
            public string Name { get { return Lang.T(NameSrc); } set { NameSrc = value; } }
            public string Text { get { return Lang.T(TextSrc); } set { TextSrc = value; } }
            public float WeightH = 1f, WeightM = 1f;          // how likely each raven is rolled this one
            public float OutChance = 1f, OutLength = 1f;      // its own outings: × the director's chance, × their length
            public float Follow = 1f, Circle = 1f, Scout = 1f; // × the weight of each kind of outing
            public float Nerves = 1f;                         // × restlessness on the shoulder (preening, shakes, steps, clacks, head turns)
            public float Turn = 1f, Curve = 1f, Switch = 1f, Ease = 1f, Wobble = 1f; // flight: turn rate/accel, curve radius, template patience, bank/nose ease, aim wobble
            public float SitPace = 1f, SitReact = 1f, SitAmp = 1f;  // sitting: × how often it does things of its own, × how readily it reacts to you, × how big its poses are
            public string[] Knacks = new string[0];           // the knacks that suit it (rolled twice as often with it)
            public string[] Quirks = new string[0];           // the quirks that suit it (likewise)
            public string Tag { get { return "char_" + Id; } } // its phrases (later)
        }

        internal sealed class Knack
        {
            public string Id;
            public string NameHSrc, NameMSrc, TextSrc; // the source texts (Ukrainian)
            public string NameH { get { return Lang.T(NameHSrc); } set { NameHSrc = value; } }
            public string NameM { get { return Lang.T(NameMSrc); } set { NameMSrc = value; } }
            public string Text { get { return Lang.T(TextSrc); } set { TextSrc = value; } }
            public float Damage, Range, Cooldown, Cost, Crit, Scout, Satiety, Rest; // % (Crit: points)
            public string Name(bool munin) { return munin && NameM != null ? NameM : NameH; }
            public string Tag { get { return "knack_" + Id; } }
        }

        internal static readonly Character[] Characters =
        {
            new Character { Id = "homebody", Name = Lang.K("Домосід"), WeightH = 1.3f, WeightM = 0.7f,
                Text = Lang.K("Тримається плеча: сам відлітає рідше і ненадовго, здебільшого летить слідом за тобою. На плечі спокійний, у повітрі плавний."),
                OutChance = 0.6f, OutLength = 0.8f, Follow = 2f, Circle = 0.6f, Scout = 0.5f, Nerves = 0.85f, SitPace = 0.9f, SitReact = 0.9f, SitAmp = 0.95f,
                Turn = 0.95f, Curve = 1.05f, Switch = 1.08f, Ease = 0.95f, Knacks = new[] { "frugal", "thrifty" }, Quirks = new[] { "sleepy", "chilly" } },
            new Character { Id = "wanderer", Name = Lang.K("Мандрівник"), WeightH = 1.3f, WeightM = 0.8f,
                Text = Lang.K("Любить небо: сам відлітає частіше й літає довше, охочіше кружляє й розвідує. Широкі дуги, довгі ковзання."),
                OutChance = 1.5f, OutLength = 1.3f, Follow = 0.6f, Circle = 1.3f, Scout = 1.6f, SitAmp = 1.05f,
                Curve = 1.08f, Switch = 1.05f, Knacks = new[] { "far", "eye" }, Quirks = new[] { "magpie", "wet" } },
            new Character { Id = "fidget", Name = Lang.K("Непосида"), WeightH = 0.7f, WeightM = 1.3f,
                Text = Lang.K("Не всидить: сам зривається частіше, але ненадовго. На плечі весь час вовтузиться, у повітрі смикається й міняє манеру."),
                OutChance = 1.3f, OutLength = 0.7f, Nerves = 1.3f, SitPace = 1.4f, SitReact = 1.2f, SitAmp = 1.1f,
                Turn = 1.08f, Curve = 0.94f, Switch = 0.92f, Ease = 1.08f, Knacks = new[] { "quick", "light" }, Quirks = new[] { "loud", "willful" } },
            new Character { Id = "calm", Name = Lang.K("Спокійний"), WeightH = 1.3f, WeightM = 0.6f,
                Text = Lang.K("Нічим не збентежиш: сам відлітає трохи рідше, на плечі сидить тихо, у повітрі найрівніший."),
                OutChance = 0.8f, Nerves = 0.75f, SitPace = 0.7f, SitReact = 0.7f, SitAmp = 0.85f,
                Turn = 0.94f, Curve = 1.04f, Switch = 1.08f, Ease = 0.92f, Wobble = 0.5f, Knacks = new[] { "keen", "thrifty" }, Quirks = new[] { "picky", "grumbler" } },
            new Character { Id = "watchful", Name = Lang.K("Сторожкий"), WeightH = 1f, WeightM = 1f,
                Text = Lang.K("Усе мусить бачити: у дорозі сам частіше летить розвідати шлях, на плечі пильно озирається."),
                Follow = 0.8f, Scout = 2f, Nerves = 1.1f, Ease = 1.04f, SitPace = 1.1f, SitReact = 1.15f, Knacks = new[] { "eye", "keen" }, Quirks = new[] { "timid", "chilly" } },
            new Character { Id = "bold", Name = Lang.K("Зухвалий"), WeightH = 0.6f, WeightM = 1.4f,
                Text = Lang.K("Задирака: сам відлітає трохи частіше й любить кружляти. У повітрі різкий, з крутими поворотами."),
                OutChance = 1.1f, Circle = 1.3f, Nerves = 1.15f, SitPace = 1.1f, SitReact = 1.2f, SitAmp = 1.2f,
                Turn = 1.08f, Curve = 0.92f, Switch = 0.95f, Ease = 1.06f, Knacks = new[] { "heavy", "quick" }, Quirks = new[] { "showoff", "grumbler" } },
            new Character { Id = "devoted", Name = Lang.K("Відданий"), WeightH = 1f, WeightM = 1f,
                Text = Lang.K("Твоя тінь: сам відлітає трохи рідше і майже завжди летить слідом за тобою, а не кудись."),
                OutChance = 0.9f, Follow = 2.5f, Circle = 0.7f, Scout = 0.5f, Nerves = 0.9f, Knacks = new[] { "light", "frugal" }, Quirks = new[] { "jealous", "thief" } },
        };

        internal static readonly Knack[] Knacks =
        {
            new Knack { Id = "heavy", NameH = Lang.K("Важкий дзьоб"), NameM = Lang.K("Важкі кігті"), Damage = 10f, Text = Lang.K("Шкода удару +10%.") },
            new Knack { Id = "far", NameH = Lang.K("Далекий виліт"), Range = 10f, Text = Lang.K("Дальність удару +10%.") },
            new Knack { Id = "quick", NameH = Lang.K("Швидке крило"), Cooldown = 15f, Text = Lang.K("Пауза між ударами −15%.") },
            new Knack { Id = "thrifty", NameH = Lang.K("Ощадний"), Cost = 10f, Text = Lang.K("Удар коштує на 10% менше сили.") },
            new Knack { Id = "keen", NameH = Lang.K("Влучний"), Crit = 5f, Text = Lang.K("Шанс криту +5%, і межа криту 95% замість 90%.") },
            new Knack { Id = "eye", NameH = Lang.K("Пильне око"), Scout = 15f, Text = Lang.K("Розвідка бачить на 15% далі: ворогів і руду в розвідці Хугіна, ворогів у наказі «Облетіти» і з прогулянки.") },
            new Knack { Id = "frugal", NameH = Lang.K("Невибагливий"), Satiety = 20f, Text = Lang.K("Ситість спадає на 20% повільніше.") },
            new Knack { Id = "light", NameH = Lang.K("Легке крило"), Rest = 50f, Text = Lang.K("Прогулянка, що добігла кінця, дає на 50% більше сили.") },
        };

        // «Примхи»: one per raven, a small habit of its own (a minus with life in it, never one that breaks the game:
        // «Повернись» always works). Trust does not change them (for now). What each does: Quirks.cs.
        internal sealed class Quirk
        {
            public string Id;
            public string NameSrc, TextSrc; // the source texts (Ukrainian)
            public string Name { get { return Lang.T(NameSrc); } set { NameSrc = value; } }
            public string Text { get { return Lang.T(TextSrc); } set { TextSrc = value; } }
            public string Tag { get { return "quirk_" + Id; } }
        }

        internal static readonly Quirk[] QuirkList =
        {
            new Quirk { Id = "thief", Name = Lang.K("Злодюжка"), Text = Lang.K("Краде з торби навіть ситий: раз на одну-дві ігрові доби поцупить або найкращу їжу, яка в тебе є, або 10 монет (пів на пів).") },
            new Quirk { Id = "picky", Name = Lang.K("Перебірливий"), Text = Lang.K("Частину своєї їжі не їсть зовсім: приблизно третину кожного класу (але щонайменше дві страви класу лишаються). Що саме — дізнаєшся, коли запропонуєш: він відмовиться, і їжа лишиться в тебе.") },
            new Quirk { Id = "grumbler", Name = Lang.K("Бурчун"), Text = Lang.K("Час від часу бурчить сам по собі — на дорогу, погоду, твоє плече.") },
            new Quirk { Id = "wet", Name = Lang.K("Мокрий"), Text = Lang.K("Після купання або дощу обтрушується на тебе — і ти теж мокрий.") },
            new Quirk { Id = "loud", Name = Lang.K("Галасливий"), Text = Lang.K("Коли ти скрадаєшся, часом каркає — і вороги поруч можуть почути.") },
            new Quirk { Id = "showoff", Name = Lang.K("Задавака"), Text = Lang.K("Після криту чи вбивства хизується, і наступного удару доведеться чекати на 8 с довше.") },
            new Quirk { Id = "jealous", Name = Lang.K("Ревнивий"), Text = Lang.K("Коли годуєш другого ворона, ображається на 5 хв: сидить понурий на плечі й нічого не дозволяє — ні дарів, ні наказів, ні годування, ні жердини, ні зняти з плеча.") },
            new Quirk { Id = "sleepy", Name = Lang.K("Сонько"), Text = Lang.K("Уночі спить: сам не гуляє і на розвідку не літає — ні дар, ні наказ.") },
            new Quirk { Id = "chilly", Name = Lang.K("Мерзляк"), Text = Lang.K("Мерзне в дощ, сніг і в холодних краях (Гори, Глибока Північ): понуро сидить на плечі, сам не гуляє й скаржиться.") },
            new Quirk { Id = "magpie", Name = Lang.K("Сорока"), Text = Lang.K("Іноді приносить із прогулянки мотлох — камінь, гілку, кістки — і кладе тобі в торбу.") },
            new Quirk { Id = "willful", Name = Lang.K("Свавільний"), Text = Lang.K("Часом (10%) не виконує наказ — а сила однаково витрачена. «Повернись» він слухає завжди.") },
            new Quirk { Id = "timid", Name = Lang.K("Боязкий"), Text = Lang.K("Боїться босів: поруч із босом сидить понурий і не летить на нього, поки бос не втратить чверть здоров'я. Зате перший його удар по такому босу — завжди крит.") },
        };

        internal static float TrustShows = 0.35f; // share of its character a trust-1 raven already shows (all of it by trust 7)

        // ---------------------------------------------------------------- reading

        internal static string Read(ItemDrop.ItemData item, string key)
        {
            string v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(key, out v) ? v : null;
        }

        internal static int CharIndex(string id) { for (int i = 0; i < Characters.Length; i++) if (Characters[i].Id == id) return i; return -1; }
        internal static Character CharById(string id) { int i = CharIndex(id); return i >= 0 ? Characters[i] : null; }
        internal static Knack KnackById(string id) { foreach (Knack k in Knacks) if (k.Id == id) return k; return null; }
        internal static Quirk QuirkById(string id) { foreach (Quirk q in QuirkList) if (q.Id == id) return q; return null; }
        internal static Quirk QuirkOf(ItemDrop.ItemData item) { return QuirkById(Read(item, QuirksKey)); }
        internal static bool HasQuirk(ItemDrop.ItemData item, string id) { return item != null && Read(item, QuirksKey) == id; }

        internal static Character CharOf(ItemDrop.ItemData item) { return CharById(Read(item, CharKey)); }
        internal static Knack KnackOf(ItemDrop.ItemData item) { return KnackById(Read(item, KnackKey)); }
        internal static bool Rolled(ItemDrop.ItemData item) { return CharOf(item) != null && KnackOf(item) != null && QuirkOf(item) != null; }
        internal static string Code(ItemDrop.ItemData item) { return (Read(item, CharKey) ?? "-") + "/" + (Read(item, KnackKey) ?? "-") + "/" + (Read(item, QuirksKey) ?? "-") + "/" + Quirks.Code(item); }

        // how much of its character shows at this trust (0 asleep: nothing yet)
        internal static float Shows(int level) { return level <= 0 ? 0f : Mathf.Lerp(TrustShows, 1f, Mathf.Clamp01((level - 1) / 6f)); }
        internal static float Shows(ItemDrop.ItemData item) { return item != null ? Shows(Trust.Level(item)) : 0f; }

        // a character's multiplier as far as it shows (1 when there is none)
        internal static float Mul(float full, float shows) { return 1f + (full - 1f) * shows; }

        // ---------------------------------------------------------------- rolling

        // a bought raven of yours without its norov yet gets one (old ravens too, once); never a stranger's
        internal static void Ensure(Player p, ItemDrop.ItemData item, bool munin)
        {
            if (item == null || Rolled(item) || Trust.Stranger(item, p)) return;
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            Character c = CharOf(item);
            if (c == null)
            {
                float sum = 0f;
                foreach (Character x in Characters) sum += munin ? x.WeightM : x.WeightH;
                float r = Random.value * sum;
                c = Characters[Characters.Length - 1];
                foreach (Character x in Characters) { r -= munin ? x.WeightM : x.WeightH; if (r <= 0f) { c = x; break; } }
                item.m_customData[CharKey] = c.Id;
            }
            if (KnackOf(item) == null)
            {
                float sum = 0f;
                foreach (Knack k in Knacks) sum += KnackWeight(c, k);
                float r = Random.value * sum;
                Knack pick = Knacks[Knacks.Length - 1];
                foreach (Knack k in Knacks) { r -= KnackWeight(c, k); if (r <= 0f) { pick = k; break; } }
                item.m_customData[KnackKey] = pick.Id;
            }
            if (QuirkOf(item) == null)
            {
                float sum = 0f;
                bool noisy = Perks.NoiseBurden(item); // its burden is already the noise: not «Галасливий» on top
                foreach (Quirk q in QuirkList) sum += QuirkWeight(c, q, noisy);
                float r = Random.value * sum;
                Quirk pick = QuirkList[0];
                foreach (Quirk q in QuirkList) { float w = QuirkWeight(c, q, noisy); if (w <= 0f) continue; pick = q; r -= w; if (r <= 0f) break; }
                item.m_customData[QuirksKey] = pick.Id;
                Quirks.Rolled(item, munin);
            }
            Backup.Save(p, item); // the copy keeps its norov too
        }

        private static float KnackWeight(Character c, Knack k) { return System.Array.IndexOf(c.Knacks, k.Id) >= 0 ? 2f : 1f; }
        private static float QuirkWeight(Character c, Quirk q, bool noisy) { return noisy && q.Id == "loud" ? 0f : System.Array.IndexOf(c.Quirks, q.Id) >= 0 ? 2f : 1f; }

        // the console's «raven norov»: rolled anew (for tests)
        internal static void Reroll(Player p, ItemDrop.ItemData item, bool munin)
        {
            if (item == null || item.m_customData == null) return;
            item.m_customData.Remove(CharKey); item.m_customData.Remove(KnackKey); item.m_customData.Remove(QuirksKey);
            Ensure(p, item, munin);
        }

        // ---------------------------------------------------------------- the network: the character of each worn raven

        internal static void Publish(Player p, ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            int v = Mathf.Clamp(CharIndex(Read(hugin, CharKey)) + 1, 0, 15) | (Mathf.Clamp(CharIndex(Read(munin, CharKey)) + 1, 0, 15) << 4);
            if (z.GetInt(NetKey, 0) != v) z.Set(NetKey, v);
        }

        // the character of this player's raven, as every game knows it (null: none)
        internal static Character OnPlayer(Player p, bool munin) { int i = IndexOnPlayer(p, munin); return i >= 0 ? Characters[i] : null; }
        internal static int IndexOnPlayer(Player p, bool munin)
        {
            ZDO z = p != null ? NetFlag.Zdo(p, false) : null;
            if (z == null) return -1;
            int i = ((z.GetInt(NetKey, 0) >> (munin ? 4 : 0)) & 15) - 1;
            return i >= 0 && i < Characters.Length ? i : -1;
        }

        // ---------------------------------------------------------------- the character's effects

        // restlessness on the shoulder (× ShoulderLife.Nerves)
        internal static float NervesMul(Player p, bool munin, int level)
        {
            Character c = OnPlayer(p, munin);
            return c != null ? Mul(c.Nerves, Shows(level)) : 1f;
        }

        // its touch on the flight character (RavenFlight.CharFor): turns, curves, template patience, eases, wobble
        internal static void Shape(FlightCharacter f, Character c, int level)
        {
            if (f == null || c == null) return;
            float s = Shows(level);
            float turn = Mul(c.Turn, s), curve = Mul(c.Curve, s), ease = Mul(c.Ease, s);
            f.TurnCap *= turn; f.SharpCap *= turn; f.TurnAccel *= turn;
            f.CurveMin *= curve; f.CurveMax *= curve;
            f.SwitchMul *= Mul(c.Switch, s);
            f.BankEase *= ease; f.PitchEase *= ease;
            f.Wobble *= Mul(c.Wobble, s);
        }

        // your raven's own outings: × the director's chance, × the length, × each kind's weight
        internal static float OutChance(ItemDrop.ItemData item) { Character c = CharOf(item); return c != null ? Mul(c.OutChance, Shows(item)) : 1f; }
        internal static float OutLength(ItemDrop.ItemData item) { Character c = CharOf(item); return c != null ? Mul(c.OutLength, Shows(item)) : 1f; }
        internal static float FollowW(ItemDrop.ItemData item) { Character c = CharOf(item); return c != null ? Mul(c.Follow, Shows(item)) : 1f; }
        internal static float CircleW(ItemDrop.ItemData item) { Character c = CharOf(item); return c != null ? Mul(c.Circle, Shows(item)) : 1f; }
        internal static float ScoutW(ItemDrop.ItemData item) { Character c = CharOf(item); return c != null ? Mul(c.Scout, Shows(item)) : 1f; }

        // ---------------------------------------------------------------- the knack's effects (fixed: trust does not change them)

        private static float K(ItemDrop.ItemData item, System.Func<Knack, float> f) { Knack k = KnackOf(item); return k != null ? f(k) : 0f; }
        internal static float DamagePct(ItemDrop.ItemData item) { return K(item, k => k.Damage); }
        internal static float RangeMul(ItemDrop.ItemData item) { return 1f + K(item, k => k.Range) / 100f; }
        internal static float CooldownMul(ItemDrop.ItemData item) { return 1f - K(item, k => k.Cooldown) / 100f; }
        internal static float CostMul(ItemDrop.ItemData item) { return 1f - K(item, k => k.Cost) / 100f; }
        internal static float CritPts(ItemDrop.ItemData item) { return K(item, k => k.Crit); }
        internal static float CritCap(ItemDrop.ItemData item) { return 90f + K(item, k => k.Crit); }
        internal static float ScoutMul(ItemDrop.ItemData item) { return (1f + K(item, k => k.Scout) / 100f) * RuneStars.RangeMul(item); } // (and «Золото Одіна»)
        internal static float SatietyMul(ItemDrop.ItemData item) { return 1f - K(item, k => k.Satiety) / 100f; }
        internal static float RestMul(ItemDrop.ItemData item) { return 1f + K(item, k => k.Rest) / 100f; }

        // ---------------------------------------------------------------- the book

        // icons of the traits: assets/norov/<id>.png when they are drawn; none yet (the book draws a stub)
        internal static Sprite Icon(string id) { return null; }

        internal static string CharTip(ItemDrop.ItemData item)
        {
            Character c = CharOf(item);
            if (c == null) return Lang.T("<b>Характер</b>\n\nЩе не відомий.");
            int pct = Mathf.RoundToInt(Shows(item) * 100f);
            return Lang.F("<b>Характер: {0}</b>\n\n{1}\n\n<color=#8C93A1>{2}</color>", c.Name, c.Text, (pct >= 100 ? Lang.T("Розкрито повністю.") : Lang.F("Розкрито на {0}%: з довірою характер проявляється сильніше (повністю — на 7-му рівні).", pct)));
        }

        internal static string KnackTip(ItemDrop.ItemData item, bool munin)
        {
            Knack k = KnackOf(item);
            return k == null ? Lang.T("<b>Хист</b>\n\nЩе не відомий.") : Lang.F("<b>Хист: {0}</b>\n\n{1}\n\n<color=#8C93A1>Хист сталий: довіра його не змінює.</color>", k.Name(munin), k.Text);
        }

        internal static string QuirkTip(ItemDrop.ItemData item, bool munin)
        {
            Quirk q = QuirkOf(item);
            if (q == null) return Lang.T("<b>Примха</b>\n\nЩе не відома.");
            return Lang.F("<b>Примха: {0}</b>\n\n{1}{2}\n\n<color=#8C93A1>Примха — його звичка, з ним назавжди.</color>", q.Name, q.Text, Quirks.Note(item, munin));
        }
    }
}
#endif
