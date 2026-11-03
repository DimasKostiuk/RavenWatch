#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // Every raven ability in one place: the radial menu, the keys and the Book of ravens all read it.
    internal sealed class RavenAbility
    {
        public string Id, NameSrc, ShortSrc; // the source texts (Ukrainian): shown through Lang
        public string Name { get { return Lang.T(NameSrc); } }
        public string Short { get { return Lang.T(ShortSrc); } }
        // the name without «(пасивний ефект)»; Bare: without the raven's name too («Світло Хугіна» → «Світло»)
        public string Plain { get { return Lang.T(NameSrc.Replace(" (пасивний ефект)", "")); } }
        public string Bare { get { return Lang.T(NameSrc.Replace(" (пасивний ефект)", "").Replace(" Хугіна", "").Replace(" Муніна", "")); } }
        public bool Munin;
        public bool Pair; // both ravens' (each pays its share): kept on Hugin's side of the lists, on the circle between the wings
        public int N; // 0: a passive (no button, no key)
        public Func<string> Long;
        public Func<float> Cost, Cooldown, Lasts; // Lasts 0: happens at once
        public bool Passive { get { return N == 0; } } // works by itself: no button, no key

        public RavenAbility(string id, bool munin, int n, string name, string shortText, Func<string> longText, Func<float> cost, Func<float> cooldown, Func<float> lasts)
        {
            Id = id; Munin = munin; N = n; NameSrc = name; ShortSrc = shortText; Long = longText; Cost = cost; Cooldown = cooldown; Lasts = lasts;
        }
    }

    internal static class RavenAbilities
    {
        internal static readonly RavenAbility[] All =
        {
            new RavenAbility("thought", false, 1, Lang.K("Думка Хугіна"), Lang.K("Хугін усе бачить і має що сказати."),
                () => Lang.T("Спитай — і Хугін розкаже, що бачив: бої, знахідки, рани, ворогів поруч. У тиші згадає щось із вашої дороги. Наведи приціл на гравця — думка буде про нього, а його ворон може відповісти."),
                () => Power.ThoughtCost, () => RavenSkills.CooldownSeconds("thought"), () => 0f),
            new RavenAbility("scout", false, 2, Lang.K("Розвідка"), Lang.K("Мапа відкривається довкола, вороги позначаються на мапі."),
                () => Lang.F("Хугін облітає край: мапа відкривається на {0} м. Через {1} с він вертається й позначає ворогів ближче {2} м (на {3} с, боси блимають), руду ближче {4} м (на {5} хв, лише там, де бос краю вже переможений) і входи під землю.", R(RavenSkills.RevealRadius()), R(RavenSkills.ScoutAwaySeconds), R(RavenSkills.ScoutEnemyRadius), R(RavenSkills.ScoutSeconds), R(RavenSkills.ScoutOreRadius), R(RavenSkills.ScoutOreMinutes)),
                () => Power.ScoutCost, () => RavenSkills.CooldownSeconds("scout"), () => RavenSkills.ScoutSeconds),
            new RavenAbility("light", false, 3, Lang.K("Світло Хугіна"), Lang.K("Над головою світляк, світить ширше за смолоскип."),
                () => Lang.F("Над головою світляк: світить на {0}% ширше за смолоскип. Туман Туманних земель не розганяє. Натисни ще раз — згасне.", R(RavenLight.RangeMultiplier * 100f - 100f)),
                () => Power.LightCost, () => RavenSkills.CooldownSeconds("light"), () => RavenLight.Duration()),
            new RavenAbility("leap", false, 4, Lang.K("Злет Хугіна"), Lang.K("Підкидає високо вгору; поки діє, кожен стрибок такий, а падіння не шкодить."),
                () => Lang.F("Підкидає на {0} м угору, а з розбігу — дугою на ~{1} м уперед; у повітрі кермуєш. Поки діє, кожен стрибок такий самий, а падіння не шкодить. Скінчиться в повітрі — приземлення болітиме.", R(Leap.HeightNow()), R(Leap.ForwardNow())),
                () => Power.LeapCost, () => RavenSkills.CooldownSeconds("leap"), () => Leap.Duration()),
            new RavenAbility("shadow", false, 5, Lang.K("Тінь"), Lang.K("Монстри тебе не бачать і не чують; підкрадись і вдар зі спини."),
                () => Lang.F("Монстри не бачать і не чують тебе{0}, погоня губить слід. Тінь знімає лише удар, постріл чи закляття, і перший удар із тіні — ×{1} шкоди{2}.{3}", (Shadow.FoolsBosses ? "" : Lang.T(" (боси бачать)")), Shadow.StrikeNow().ToString("0.#"), (Shadow.Stun ? Lang.T(" і ворог приголомшений") : ""), (Power.Pov ? Lang.T(" Бонуси PoV до скритної атаки — зверху.") : "")),
                () => Power.ShadowCost, () => RavenSkills.CooldownSeconds("shadow"), () => Shadow.Duration()),
            new RavenAbility("dome", false, 6, Lang.K("Під крилом"), Lang.K("Хугін накриває тебе куполом: монстри кружляють біля краю, а всередині ніхто нікого не ранить."),
                () => Lang.F("Довкола тебе на {0} м купол із рун. Монстри кружляють біля краю й не заходять; усередині ніхто нікого не ранить — ні ззовні, ні звідти. Дощ і вітер лишаються зовні (холод — ні), вогнище всередині гріє, як у хаті. Бос заходить і ламає купол за {1}. Під землею не розкривається. Зникне — монстри кинуться, будь напоготові.", R(Dome.Radius), Lang.N(Dome.BossBlows, "удар|удари|ударів")),
                () => Power.DomeCost, () => RavenSkills.CooldownSeconds("dome"), () => Dome.Duration()),
            new RavenAbility("hstrike", false, 7, Lang.K("Удар в очі"), Lang.K("Хугін злітає туди, куди ти дивишся, і влучно дзьобає ворога."),
                () => Lang.F("Хугін летить до цілі в прицілі (до {0} м; нема — до найближчої) і дзьобає: {1} колючої шкоди, крит ×2 — {2}% (хто тебе не помітив — завжди); може дзьобнути двічі. Ціль сліпне на {3} с (бос — {4} с) і губить погоню, доки її не вдарять. Б'є монстрів і дичину, не приручених. Боса — не більше {5}% здоров'я за виліт. Не знайшов цілі — пів сили назад і пауза вдвічі коротша. Сила росте з вишколом, довірою, перемогами над босами й ситістю; прийоми — у вкладці «Розвиток».", R(StrikeRange(false)), R(StrikeBlow(false)), R(Strike.CritChance(false, Trust.Worn(false))), R(Marks.BlindSeconds), Marks.BlindBossSeconds.ToString("0.#"), R(Strike.BossCapHugin * 100f)),
                () => Strike.CostOf(false, Trust.Worn(false)), () => RavenSkills.CooldownSeconds("hstrike"), () => 0f),
            new RavenAbility("mstrike", true, 7, Lang.K("Кігті"), Lang.K("Мунін летить туди, куди ти дивишся, і рве кігтями до трьох ворогів."),
                () => Lang.F("Мунін летить до цілі в прицілі (до {0} м; нема — до найближчої) і рве кігтями: {1} рублячої шкоди, першу ціль — до трьох разів, ще двох поруч зачепить по дорозі. Крит ×2 — {2}% (хто тебе не помітив — завжди). Рвана рана на {3} с (бос — {4} с): кровить ще на {5}% від удару й кульгає на {6}% повільніше. Не б'є приручених. Боса — не більше {7}% здоров'я за виліт. Не знайшов цілі — пів сили назад і пауза вдвічі коротша. Сила росте з вишколом, довірою, перемогами над босами й ситістю; прийоми — у вкладці «Розвиток».", R(StrikeRange(true)), R(StrikeBlow(true)), R(Strike.CritChance(true, Trust.Worn(true))), R(Marks.WoundSeconds), Marks.WoundBossSeconds.ToString("0.#"), R(Marks.BleedShare * 100f), R(Marks.Slow * 100f), R(Strike.BossCapMunin * 100f)),
                () => Strike.CostOf(true, Trust.Worn(true)), () => RavenSkills.CooldownSeconds("mstrike"), () => 0f),
            new RavenAbility("anger", true, 0, Lang.K("Злість (пасивний ефект)"), Lang.K("коли тебе вдарили, Мунін злиться, і ти якийсь час б'єш сильніше."),
                () => Lang.F("Коли тебе вдарили, Мунін злиться: {0} ти б'єш на {1}% сильніше. Не частіше ніж раз на {2}.", Span(WeakPassive.SecondsOf(true)), R(WeakPassive.PercentOf(true)), Span(WeakPassive.CooldownOf(true))),
                () => WeakPassive.Cost, () => WeakPassive.CooldownOf(true), () => WeakPassive.SecondsOf(true)),
            new RavenAbility("bloodfury", true, 0, Lang.K("Кривава лють (пасивний ефект)"), Lang.K("коли здоров'я падає нижче 30%, Мунін шаленіє, і ти б'єш сильніше й швидше."),
                () => Lang.F("Коли здоров'я нижче {0}%, Мунін шаленіє: {1} с ти б'єш на {2}% сильніше й на {3}% швидше (з Люттю — ще швидше).", R(BloodFury.Threshold * 100f), R(BloodFury.Duration()), R(BloodFury.DamageNow()), R(BloodFury.SpeedNow())),
                () => BloodFury.Cost, () => BloodFury.Cooldown(), () => BloodFury.Duration()),
            new RavenAbility("mthought", true, 6, Lang.K("Думка Муніна"), Lang.K("Мунін усе бачить і все пам'ятає."),
                () => Lang.T("Спитай, якщо не боїшся відповіді: Мунін пам'ятає кожну твою дурницю, рану й ворога поруч. У тиші нагадає щось зі спільного минулого — не завжди приємне. Наведи приціл на гравця — Мунін пройдеться по ньому, а його ворон може огризнутися."),
                () => Power.MuninThoughtCost, () => RavenSkills.CooldownSeconds("mthought"), () => 0f),
            new RavenAbility("lightstep", false, 0, Lang.K("Легкий крок (пасивний ефект)"), Lang.K("коли біжиш і поруч нема ворогів, витривалість якийсь час відновлюється швидше."),
                () => Lang.F("Коли біжиш і ворогів поруч нема, Хугін веде ритм: {0} витривалість відновлюється на {1}% швидше. Не частіше ніж раз на {2}.", Span(WeakPassive.SecondsOf(false)), R(WeakPassive.PercentOf(false)), Span(WeakPassive.CooldownOf(false))),
                () => WeakPassive.Cost, () => WeakPassive.CooldownOf(false), () => WeakPassive.SecondsOf(false)),
            new RavenAbility("lastchance", false, 0, Lang.K("Останній шанс (пасивний ефект)"), Lang.K("Хугін рятує від смертельного удару — лишається 1 здоров'я і кілька секунд невразливості, щоб утекти."),
                () => Lang.F("Удар, що мав би вбити, — зброя, падіння, отрута чи вогонь, — лишає тобі 1 здоров'я, і {0} с ніщо не ранить: час утекти.", R(LastChance.Safe())),
                () => LastChance.Cost, () => LastChance.Cooldown(), () => 0f),
            new RavenAbility("hloyal", false, 0, Lang.K("Вірність (пасивний ефект)"), Lang.K("коли ти гинеш, Хугін може не піти в могилу й лишитися на плечі."),
                () => Lang.F("Коли ти гинеш, Хугін із шансом {0}% не йде в могилу: лишається на плечі й зустрічає тебе після відродження. Не діє, поки він відлетів.", R(Loyal.ChanceOf(Trust.Worn(false)) * 100f)),
                () => 0f, () => 0f, () => 0f),
            new RavenAbility("mloyal", true, 0, Lang.K("Впертість (пасивний ефект)"), Lang.K("коли ти гинеш, Мунін може вчепитися в плече й не піти в могилу."),
                () => Lang.F("Коли ти гинеш, Мунін із шансом {0}% вчіплюється в плече й не йде в могилу — а після відродження все тобі пригадає. Не діє, поки він відлетів.", R(Loyal.ChanceOf(Trust.Worn(true)) * 100f)),
                () => 0f, () => 0f, () => 0f),
            new RavenAbility("mrevenge", true, 0, Lang.K("Кровна помста (пасивний ефект)"), Lang.K("Мунін позначає того, хто тебе вбив: помстися вчасно — і він поверне навички, втрачені через смерть."),
                () => Lang.F("Коли тебе вбивають, Мунін позначає вбивцю — звіра, боса чи гравця. Убий його за {0}, будучи ближче {1} м (добити може й друг), — і він поверне навички й довіру, забрані смертю. По міченому б'єш на {2}% сильніше, а руни над ним і мапа показують, де він. Зник — мітку бере такий самий звір поруч. Мунін має бути на плечі, коли ти гинеш.", RavenAbilities.Span(BloodRevenge.WindowMinutes(Trust.Worn(true)) * 60f), R(BloodRevenge.NearRange), R(BloodRevenge.DamageFor(BloodRevenge.WindowMinutes(Trust.Worn(true))))),
                () => 0f, () => 0f, () => 0f),
            new RavenAbility("homen", false, 0, Lang.K("Віщування (пасивний ефект)"), Lang.K("Деколи вранці Хугін каже, що несе день: набіг, погоду, недобрий знак; набіг можна відвести або накликати."),
                () => Lang.F("Деякими ранками ({0}%) Хугін віщує день: чи буде набіг до наступного ранку і яка погода. Набіг можна відвести на добу (−{1} сили, раз на {2}; тоді прийде напевно й злішим) або накликати зараз біля бази. Недобрий знак: загинеш до ранку — ворони втратять менше довіри. Інколи ({3}%) ще й дар до ранку: +{4} здоров'я, витривалості чи ейтиру. У морі попереджає про зміну вітру й змія. Набіги — лише якщо RavenWatch стоїть і на сервері.", R(Omen.ChanceNow()), R(Omen.TurnCost()), RavenAbilities.Span(Omen.DeflectDays * (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f)), R(Omen.BoonChance), R(Omen.BoonAmount)),
                () => 0f, () => 0f, () => 0f),
            new RavenAbility("mtame", true, 14, Lang.K("Приборкання"), Lang.K("Мунін сідає на голову дикій тварині й ламає її норов: приручення йде набагато швидше."),
                () => Lang.F("Наведи приціл на дику тварину (до {0} м) — Мунін сідає їй на голову й ламає норов: {1} приручення йде в {2} швидше, і вона ні на кого не кидається. Годувати однаково треба. Поки він там, інші його дари недоступні; натисни ще раз — повернеться.", R(Tame.Range), RavenAbilities.Span(Tame.Duration()), Tame.Times()),
                () => Tame.Cost, () => RavenSkills.CooldownSeconds("mtame"), () => Tame.Duration()),
            new RavenAbility("scream", true, 2, Lang.K("Крик"), Lang.K("Вороги поруч втрачають рівновагу й тікають."),
                () => Lang.F("Мунін кричить так, що вороги ближче {0} м втрачають рівновагу й {1} с тікають від тебе. Боси лише втрачають рівновагу.", R(RavenSkills.ScreamRadius), R(Fear.ScreamSeconds())),
                () => Power.ScreamCost, () => RavenSkills.CooldownSeconds("scream"), () => 0f),
            new RavenAbility("fury", true, 3, Lang.K("Лють"), Lang.K("Удари, натяг лука, перезарядка арбалета й каст посохів швидші на якийсь час."),
                () => Lang.F("Удари, натяг лука, перезарядка арбалета й каст посохів на {0}% швидші. Силу боса не витрачає.", R(Fury.Percent())),
                () => Power.FuryCost, () => RavenSkills.CooldownSeconds("fury"), () => Fury.Duration()),
            new RavenAbility("wings", true, 4, Lang.K("Попутний вітер"), Lang.K("Бігаєш швидше на якийсь час."),
                () => Lang.F("+{0}% до швидкості руху, а біг коштує на {1}% менше витривалості.", R(Wings.SpeedNow()), R(Wings.RunStaminaPercent)),
                () => Power.WingsCost, () => RavenSkills.CooldownSeconds("wings"), () => Wings.Duration()),
            new RavenAbility("heal", true, 5, Lang.K("Зцілення Муніна"), Lang.K("Як випита медовуха: здоров'я відновлюється до повного, і ані краплі понад."),
                () => Lang.T("Як медовуха: здоров'я поступово відновлюється до повного, навіть якщо тебе ранять. Перезарядку з медовухами не ділить."),
                () => Power.HealCost, () => RavenSkills.CooldownSeconds("heal"), () => RavenHeal.Seconds),
            new RavenAbility("ward", true, 9, Lang.K("Заслона Муніна"), Lang.K("Перед тобою стіна з рун: вона приймає на себе удар спереду, хоч який сильний."),
                () => Lang.F("Перед тобою на {0} півколо рун. Удар спереду воно бере цілком — ні шкоди, ні відкидання, ні оглушення, а хто бив, сам оглушується. Тримає удари монстрів і босів, стріли й закляття (гравців — у PvP); отрута, вогонь, падіння й удар у спину проходять. Витримує {1} (знаки над заслоною); слабкі удари (до {2}% твого здоров'я) бере, не витрачаючи заряду. Вдале парирування заряду не забирає. Діє й під куполом.", Span(Ward.Duration()), Lang.N(Ward.BlowsAt(Trust.LevelWorn(true)), "удар|удари|ударів"), R(Ward.FreeShare * 100f)),
                () => Power.WardCost, () => RavenSkills.CooldownSeconds("ward"), () => Ward.Duration()),
            // the great gifts (trust 6)
            new RavenAbility("hdecoy", false, 10, Lang.K("Обманка"), Lang.K("Хугін кружляє над ворогом і дражнить його: той б'ється лише з вороном."),
                () => Lang.F("Хугін кружляє над ворогом у прицілі (до {0} м) і дражнить: {1} той б'ється лише з вороном і нікого не ранить. Після {2} ударів гравців розкусить обманку.{3} Наприкінці обдурені {4} с стоять приголомшені. Боси не купуються. Натисни ще раз — Хугін повернеться.", R(Decoy.Range), Span(Decoy.Duration()), Decoy.HitsToBreak, Trust.LevelWorn(false) >= Decoy.GangTwoAt ? Lang.T(" Заодно дурить ще двох ворогів поруч.") : Trust.LevelWorn(false) >= Decoy.GangOneAt ? Lang.T(" Заодно дурить ще одного ворога поруч.") : "", R(Decoy.StunSeconds)),
                () => Power.DecoyCost, () => RavenSkills.CooldownSeconds("hdecoy"), () => Decoy.Duration()),
            new RavenAbility("hflight", false, 8, Lang.K("Крила Одіна"), Lang.K("Великий дар Хугіна: ти летиш, а обидва ворони — за твоєю спиною."),
                () => Lang.F("Ти летиш {0}: W — куди дивиться камера, A/D — убік, Пробіл — угору, присісти — униз; без клавіш висиш. Не вище {1} м над землею. Ворони летять за спиною й б'ють, а зброєю атакувати не можна; купол, розвідка й злет недоступні. Присядеш біля землі — станеш на ноги, Пробіл — знову в небо. Кінець чи ще раз дар — плавно опустишся без шкоди. Не злітає під землею, під дахом, з води, верхи й з перевантаженням.", Span(OdinWings.Duration()), R(OdinWings.MaxHeight)),
                () => Power.FlightCost, () => RavenSkills.CooldownSeconds("hflight"), () => OdinWings.Duration()),
            new RavenAbility("mberserk", true, 8, Lang.K("Звір Муніна"), Lang.K("Мунін будить у тобі звіра: удвічі більш{ий|а}, сильніш{ий|а} і міцніш{ий|а}. Потім — втома."),
                () => Lang.F("Ти виростаєш у {0}× — справжнім тілом, тож у тісному можна застрягти. {1}: +{2}% шкоди, +{3}% броні, тебе не оглушити й не відкинути, удар ногою відкидає на {5}% далі. Додається до Злості й Кривавої люті; з Люттю й Крилами Одіна не працює. Не в підземеллях. <color=#FF8A7A>Потім {4} витривалість відновлюється вдвічі повільніше.</color>", Beast.Size.ToString("0.#"), Span(Beast.Duration()), R(Beast.DamagePercent), R(Beast.ArmorPercent), Span(Beast.TiredSeconds), R((Beast.KickPush - 1f) * 100f)),
                () => Power.BeastCost, () => RavenSkills.CooldownSeconds("mberserk"), () => Beast.Duration()),
            // the paired gifts: both ravens on the shoulders, both pay (PairGifts)
            new RavenAbility("pload", false, 11, Lang.K("Подвійна ноша"), Lang.K("Обидва ворони підхоплюють частину вантажу й летять поруч."),
                () => Lang.F("{0} ворони несуть частину ноші: +{1} ваги, а падіння з висоти ранить на {2} менше. Поки тягнуть, їхні дари не працюють. Натисни ще раз — кинуть раніше. Платять обидва.", Span(Load.Duration()), R(Load.BonusNow()), Mathf.RoundToInt(Load.FallNow() * 100f) + "%"),
                () => Load.Cost, () => RavenSkills.CooldownSeconds("pload"), () => Load.Duration()) { Pair = true },
            new RavenAbility("pclear", false, 12, Lang.K("Розігнати хмари"), Lang.K("Обидва ворони кружляють угорі — і довкола тебе ясно."),
                () => Lang.F("{0} довкола тебе на {1} м ясно: ні дощу, ні снігу, у горах тепліше, туман Туманних земель відступає. Ясне коло йде з тобою. Не діє під землею, під дахом і там, де погоду тримає край. Платять обидва.", Span(ClearSky.Duration()), R(ClearSky.RadiusNow())),
                () => ClearSky.Cost, () => RavenSkills.CooldownSeconds("pclear"), () => ClearSky.Duration()) { Pair = true },
            new RavenAbility("pwhirl", false, 13, Lang.K("Вихор"), Lang.K("Обидва ворони кружляють довкола й без упину б'ють усіх поруч, доки стане сили."),
                () => Lang.F("Ворони кружляють довкола й без упину б'ють кожного ворога ближче {0} м — як «Удар в очі» й «Кігті», з критами й мітками. Кожен удар — {1} сили того, хто б'є (почати — по {3} у кожного); скінчиться в одного — обидва вертаються. Натисни ще раз — повернуться раніше; нікого бити — стихає. Боси — пів шкоди. У залік ідуть лише перші {2} ударів. Під куполом не діє.", R(Whirl.Radius), Whirl.PerBlowNow().ToString("0.#"), Whirl.Counted, Mathf.RoundToInt(PairGifts.CostOf(RavenAbilities.Find("pwhirl")))),
                () => Whirl.PerBlowNow(), () => RavenSkills.CooldownSeconds("pwhirl"), () => 0f) { Pair = true },
        };

        // every cost as it is now: a hungry raven's gifts cost more (Satiety); Power.Spend takes the plain cost and does the same
        static RavenAbilities()
        {
            foreach (RavenAbility a in All)
            {
                Func<float> plain = a.Cost, plainWait = a.Cooldown;
                bool m = a.Munin;
                string id = a.Id;
                a.Cost = () => plain() * Satiety.CostMul(m) * GiftUp.CostMul(id); // (golden runes: level II)
                if (a.Passive) a.Cooldown = () => plainWait() * GiftUp.CooldownMul(id); // (the active gifts: CooldownSeconds)
            }
        }

        // the strike of the worn raven for its text; no raven on: the plain numbers
        private static float StrikeRange(bool munin) { ItemDrop.ItemData r = Trust.Worn(munin); return r != null ? Strike.RangeOf(r) : Strike.Range; }
        private static float StrikeBlow(bool munin) { ItemDrop.ItemData r = Trust.Worn(munin); return r != null ? Strike.Blow(munin, r) : Strike.BaseOf(munin, null); }

        internal static bool Stub(RavenAbility a) { return false; } // (every gift made; kept for the gifts to come)

        // the trust levels the small passives (anger, light step) and the strong ones (last chance, blood fury) open on
        internal static int WeakUnlock = 2, StrongUnlock = 4;

        // The trust level each gift opens on. The small and great passives and the dome keep their own settings;
        // "Gift unlock levels" in the config may set any of them ("light:3, wings:2").
        private static readonly Dictionary<string, int> Levels = new Dictionary<string, int>
        {
            { "thought", 1 }, { "scout", 1 }, { "hstrike", 1 }, { "light", 2 }, { "hdecoy", 3 }, { "leap", 4 }, { "shadow", 5 }, { "hflight", 6 },
            { "mthought", 1 }, { "mstrike", 1 }, { "scream", 2 }, { "wings", 3 }, { "heal", 4 }, { "fury", 5 }, { "ward", 4 }, { "mberserk", 6 },
            { "mtame", 3 }, { "pload", 2 }, { "pclear", 3 }, { "pwhirl", 6 }, // (the paired ones: both ravens at this level)
        };

        private static readonly Dictionary<string, int> BuiltinLevels = new Dictionary<string, int>(Levels);

        // from the built-in ones each time: the server's list must not stay after leaving it
        internal static void SetLevels(string list)
        {
            Levels.Clear();
            foreach (KeyValuePair<string, int> kv in BuiltinLevels) Levels[kv.Key] = kv.Value;
            if (string.IsNullOrEmpty(list)) return;
            foreach (string part in list.Split(','))
            {
                string[] kv = part.Split(':');
                int lv;
                if (kv.Length == 2 && int.TryParse(kv[1].Trim(), out lv)) Levels[kv[0].Trim()] = Mathf.Clamp(lv, 1, 7);
            }
        }

        internal static int UnlockLevel(RavenAbility a)
        {
            if (a == null) return 1;
            if (a.Id == "lastchance" || a.Id == "bloodfury") return StrongUnlock;
            if (BloodRevenge.Is(a)) return BloodRevenge.UnlockLevel;
            if (Omen.Is(a)) return Omen.UnlockLevel;
            if (a.Id == "dome") return Dome.UnlockLevel;
            if (a.Id == "ward") return Ward.UnlockLevel;
            if (Loyal.Is(a)) return Loyal.UnlockLevel;
            if (a.Id == "anger" || a.Id == "lightstep") return WeakUnlock;
            int lv;
            return Levels.TryGetValue(a.Id, out lv) ? lv : 1;
        }

        // shown in the menu and told in full in the book: open now (an unwoken raven shows what it will have first)
        internal static bool Shown(RavenAbility a) { return a != null && UnlockLevel(a) <= Mathf.Max(1, LevelOf(a)); }
        internal static bool Unlocked(RavenAbility a) { return LevelOf(a) >= UnlockLevel(a); }
        // the trust it opens by: a paired gift waits for the less trusting of the two
        private static int LevelOf(RavenAbility a) { return a.Pair ? Mathf.Min(Trust.LevelWorn(false), Trust.LevelWorn(true)) : Trust.LevelWorn(a.Munin); }
        internal static string LockedText(RavenAbility a) { return a.Pair ? Lang.F("<color=#9a9a9a>відкриється, коли обидва ворони будуть на {0}-му рівні довіри</color>", UnlockLevel(a)) : Lang.F("<color=#9a9a9a>відкриється на {0}-му рівні довіри</color>", UnlockLevel(a)); }

        // a passive's own state (ready or when again)
        internal static string PassiveReady(RavenAbility a)
        {
            if (!Unlocked(a)) return LockedText(a);
            switch (a.Id)
            {
                case "lastchance": return LastChance.ReadyText();
                case "bloodfury": return BloodFury.ReadyText();
                case "hloyal": case "mloyal": return Loyal.ReadyText(a.Id == "mloyal");
                case "mrevenge": return BloodRevenge.ReadyText();
                case "homen": return Omen.ReadyText();
                default: return WeakPassive.ReadyText(a.Munin);
            }
        }

        // a passive that takes no strength and has no wait (the loyalty at death, the revenge, the omen)
        internal static bool Free(RavenAbility a) { return a != null && (Loyal.Is(a) || BloodRevenge.Is(a) || Omen.Is(a)); }

        internal static RavenAbility Find(string id) { foreach (RavenAbility a in All) if (a.Id == id) return a; return null; }

        // ready in gold, waiting in red: everywhere a state is shown (tooltips, the book, the radial menu)
        internal static string ReadyMark(float secondsLeft, string ready)
        {
            return secondsLeft <= 0f
                ? "<color=#FFD54F><b>" + ready + "</b></color>"
                : Lang.F("<color=#FF5A5A><b>відпочиває {0}</b></color>", Clock(secondsLeft));
        }

        internal static string R(float v) { return Mathf.RoundToInt(v).ToString(); }

        // a short clock for the wait: 39:47, or 1:05:00 from an hour on
        internal static string Clock(float seconds)
        {
            int s = Mathf.Max(1, Mathf.CeilToInt(seconds));
            return s >= 3600 ? (s / 3600) + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00") : (s / 60) + ":" + (s % 60).ToString("00");
        }

        // A time as a raven says it, in its words (no «хв», «с»): «хвилину й двадцять вісім секунд», «дві хвилини»,
        // «сорок п'ять секунд». The numbers in words too, in their right gender (the voice reads digits as «один», «два»:
        // «один хвилину»). Ten minutes and more: whole minutes, no seconds. In English: digits and the words.
        internal static string SpokenSpan(float seconds)
        {
            int s = Mathf.Max(1, Mathf.CeilToInt(seconds - 0.01f));
            if (s >= 600) s = Mathf.CeilToInt(s / 60f) * 60;
            int min = s / 60, sec = s % 60;
            bool uk = Lang.PhraseLanguage == Lang.Source;
            string m = min > 0 ? (uk ? Counted(min, "хвилину|хвилини|хвилин") : Lang.N(min, "хвилину|хвилини|хвилин")) : null;
            string c = sec > 0 ? (uk ? Counted(sec, "секунду|секунди|секунд") : Lang.N(sec, "секунду|секунди|секунд")) : null;
            if (m == null) return c;
            if (c == null) return m;
            return uk ? m + (m.EndsWith("н") ? " і " : " й ") + c : Lang.F("{0} і {1}", m, c);
        }

        // a feminine count in Ukrainian words, as after «за»: 1 → «хвилину», 2 → «дві хвилини», 21 → «двадцять одну хвилину»
        private static readonly string[] UkOnes = { "", "одну", "дві", "три", "чотири", "п'ять", "шість", "сім", "вісім", "дев'ять", "десять", "одинадцять", "дванадцять", "тринадцять", "чотирнадцять", "п'ятнадцять", "шістнадцять", "сімнадцять", "вісімнадцять", "дев'ятнадцять" };
        private static readonly string[] UkTens = { "", "", "двадцять", "тридцять", "сорок", "п'ятдесят", "шістдесят", "сімдесят", "вісімдесят", "дев'яносто" };
        private static readonly string[] UkHundreds = { "", "сто", "двісті", "триста", "чотириста", "п'ятсот", "шістсот", "сімсот", "вісімсот", "дев'ятсот" };
        private static string Counted(int n, string forms)
        {
            string word = Lang.P(n, forms);
            return n == 1 ? word : UkWords(n) + " " + word;
        }
        private static string UkWords(int n)
        {
            if (n <= 0 || n >= 1000) return n.ToString(CultureInfo.InvariantCulture);
            List<string> w = new List<string>();
            if (n >= 100) { w.Add(UkHundreds[n / 100]); n %= 100; }
            if (n >= 20) { w.Add(UkTens[n / 10]); n %= 10; }
            if (n > 0) w.Add(UkOnes[n]);
            return string.Join(" ", w.ToArray());
        }

        // a count a raven says of feminine things (хвилин, штук, монет, as «хвилин {n}»): in Ukrainian words, «дві», «двадцять одну»
        // (the voice reads digits as «два», «один»); in another language the digits
        internal static string SpokenCount(int n) { return Lang.PhraseLanguage == Lang.Source ? UkWords(n) : n.ToString(CultureInfo.InvariantCulture); }

        // "5 хв", "1 хв 30 с", "45 с"
        internal static string Span(float seconds)
        {
            // a wait still left is at least "1 с", never "миттєво"
            if (seconds <= 0f) return Lang.T("миттєво");
            int s = Mathf.Max(1, Mathf.CeilToInt(seconds - 0.01f));
            if (s < 60) return Lang.F("{0} с", s);
            return (s % 60 == 0) ? Lang.F("{0} хв", (s / 60)) : Lang.F("{0} хв {1} с", (s / 60), (s % 60));
        }
    }
}
#endif
