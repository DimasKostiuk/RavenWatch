#if RAVEN_ITEM
using System;
using System.Collections.Generic;
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
                () => Lang.T("Хугін бачить кожен твій крок і нічого не пропускає: бої, знахідки, рани, ворогів за кущами. Спитай — і почуєш, що про все це записано в його літописі. А коли довкола тихо, він згадає щось із того, що ви пройшли разом. Наведи приціл на іншого гравця — і думка буде про нього: хто він, що в нього в руках, що ви пройшли разом; а його ворон може й відповісти."),
                () => Power.ThoughtCost, () => RavenSkills.CooldownSeconds("thought"), () => 0f),
            new RavenAbility("scout", false, 2, Lang.K("Розвідка"), Lang.K("Мапа відкривається довкола, вороги позначаються на мапі."),
                () => Lang.F("Хугін відлітає на розвідку, і мапа відкривається на {0} м довкола (з довірою до {1} м). Через {2} с він вертається на плече й розповідає, що бачив, і лише тоді на мапі з'являються позначки: вороги ближче {3} м — своїми трофеями на {4} с (боси блимають), родовища руди ближче {5} м — іконкою руди на {6} хв. Руду краю Хугін показує лише тоді, коли його бос уже переможений: мідь і олово після Прадавнього, залізо після Кістянки, срібло після Модер і так далі. Ще він помічає входи під землю — склепи, поховальні камери, печери, шахти — і позначає їх на мапі на той самий час.", R(RavenSkills.RevealRadius()), R(RavenSkills.ScoutRevealRadiusTop), R(RavenSkills.ScoutAwaySeconds), R(RavenSkills.ScoutEnemyRadius), R(RavenSkills.ScoutSeconds), R(RavenSkills.ScoutOreRadius), R(RavenSkills.ScoutOreMinutes)),
                () => Power.ScoutCost, () => RavenSkills.CooldownSeconds("scout"), () => RavenSkills.ScoutSeconds),
            new RavenAbility("light", false, 3, Lang.K("Світло Хугіна"), Lang.K("Над головою світляк, світить ширше за смолоскип."),
                () => Lang.F("Над головою літає світляк і світить на {0}% ширше за смолоскип, а від нього тихо розлітаються руни. Туман Туманних земель він не розганяє. Натисни дар ще раз — і світляк згасне; перезарядка однаково рахується від запуску.", R(RavenLight.RangeMultiplier * 100f - 100f)),
                () => Power.LightCost, () => RavenSkills.CooldownSeconds("light"), () => RavenLight.Duration()),
            new RavenAbility("leap", false, 4, Lang.K("Злет Хугіна"), Lang.K("Підкидає високо вгору; поки діє, кожен стрибок такий, а падіння не шкодить."),
                () => Lang.F("Підкидає на {0} м угору, а на бігу — дугою вперед приблизно на {1} м; у повітрі кермуєш клавішами руху. Поки діє, кожен стрибок із землі такий самий, а падіння не шкодить. Скінчиться в повітрі — приземлення болітиме, як завжди.", R(Leap.HeightNow()), R(Leap.ForwardNow())),
                () => Power.LeapCost, () => RavenSkills.CooldownSeconds("leap"), () => Leap.Duration()),
            new RavenAbility("shadow", false, 5, Lang.K("Тінь"), Lang.K("Монстри тебе не бачать і не чують; підкрадись і вдар зі спини."),
                () => Lang.F("Хугін ховає тебе в тінь: монстри не бачать і не чують тебе{0}, а ті, що гналися, втрачають слід. Удар, постріл чи закляття знімають тінь, а перший удар — з тіні: ×{1} шкоди{2}; бонуси PoV до скритної атаки додаються зверху. Біг, стрибки, двері, їжа та інше тінь не знімають.", (Shadow.FoolsBosses ? "" : Lang.T(" (боси бачать)")), Shadow.StrikeMultiplier.ToString("0.#"), (Shadow.Stun ? Lang.T(" і ворог приголомшений") : "")),
                () => Power.ShadowCost, () => RavenSkills.CooldownSeconds("shadow"), () => Shadow.Duration()),
            new RavenAbility("dome", false, 6, Lang.K("Під крилом"), Lang.K("Хугін накриває тебе куполом: монстри кружляють біля краю, а всередині ніхто нікого не ранить."),
                () => Lang.F("Хугін розправляє крила, і довкола тебе на {0} м здіймається купол з рун. Монстри, що за тобою гналися, підбігають до краю й кружляють довкола, чекаючи, — увійти не можуть і не атакують. Під куполом ніхто нікого не ранить: ні ти звідти, ні тебе ззовні, тож стріляти з-під крила не вийде. Купол — як дах над головою: дощ і вітер лишаються зовні, але холод — ні; розпали всередині вогнище, і грітимешся, як у хаті. Захищає всіх, хто стоїть усередині. Боси на купол не зважають і заходять під нього, але проти боса купол крихкий: він приймає на себе лише {1} по тих, хто всередині, і на останньому розлітається. Під землею не розкривається. Коли купол зникає, монстри кидаються на тебе, тож будь напоготові.", R(Dome.Radius), Lang.N(Dome.BossBlows, "удар|удари|ударів")),
                () => Power.DomeCost, () => RavenSkills.CooldownSeconds("dome"), () => Dome.Duration()),
            new RavenAbility("hstrike", false, 7, Lang.K("Удар в очі"), Lang.K("Хугін злітає туди, куди ти дивишся, і влучно дзьобає ворога."),
                () => Lang.F("Хугін зривається з плеча, летить до істоти, на яку ти дивишся (від {0} до {1} м — що вищий вишкіл, то далі; у прямій видимості; звір у прицілі задалеко чи не видно — бере найближчого попереду, а як нема, то збоку чи позаду; нікого — летить, куди дивишся), пірнає й дзьобає. Б'є і монстрів, і дичину — оленів, кабанів, — але не приручених, не тих, кого ти саме приручаєш, і не своїх. Хто не чекав удару (не помітив тебе), отримує перший удар критом (крім як у Тіні). Шкода колюча; після удару вертається на плече. Ціль осліплена на {2} с (бос — на {3} с): нікого не бачить і не чує, губить того, за ким гналася, — час утекти чи зайти зі спини; перший справжній удар по ній (твій чи друга; підпал чи отрута не рахуються) знімає осліплення. Сила удару своя, не від зброї: росте з вишколом, з довірою, за кожного боса, якого ти переміг з Хугіном на плечі, і коли він ситий. Хугін може дзьобнути ту саму ціль двічі; крит б'є вдвічі сильніше. Боса один виліт ранить не більше ніж на {4}% його здоров'я. Не знайшов нікого — половина сили вертається, а пауза вдвічі коротша. Усе це — у книзі воронів, вкладка «Вишкіл».", R(Strike.Range), R(Strike.RangeTop), R(Marks.BlindSeconds), Marks.BlindBossSeconds.ToString("0.#"), R(Strike.BossCapHugin * 100f)),
                () => Strike.CostOf(false, Trust.Worn(false)), () => RavenSkills.CooldownSeconds("hstrike"), () => 0f),
            new RavenAbility("mstrike", true, 7, Lang.K("Кігті"), Lang.K("Мунін летить туди, куди ти дивишся, і рве кігтями до трьох ворогів."),
                () => Lang.F("Мунін зривається з плеча, летить до істоти, на яку ти дивишся (від {0} до {1} м — що вищий вишкіл, то далі; звір у прицілі задалеко чи не видно — бере найближчого попереду, а як нема, то збоку чи позаду; нікого — летить, куди дивишся), і рве кігтями — і монстрів, і дичину, але не приручених, не тих, кого ти саме приручаєш, і не своїх; хто не чекав, отримує перший удар критом (крім як у Тіні) — рубляча шкода, сильніша за Хугінову; ще двох ворогів поруч він зачепить по дорозі. Першу ціль може роздерти двічі чи тричі, інших зачіпає раз; хитає їх, а тоді вертається. Мунін частіше за Хугіна б'є критом (×2). Кожному лишає рвану рану на {2} с (бос — {3} с): стікає кров'ю — ще {4}% від удару — і кульгає на {5}% повільніше. Сила удару своя, не від зброї: росте з вишколом, з довірою, за кожного боса, якого ти переміг з Муніном на плечі, і коли він ситий. Боса один виліт ранить не більше ніж на {6}% його здоров'я. Не знайшов нікого — половина сили вертається, а пауза вдвічі коротша. Усе це — у книзі воронів, вкладка «Вишкіл».", R(Strike.Range), R(Strike.RangeTop), R(Marks.WoundSeconds), Marks.WoundBossSeconds.ToString("0.#"), R(Marks.BleedShare * 100f), R(Marks.Slow * 100f), R(Strike.BossCapMunin * 100f)),
                () => Strike.CostOf(true, Trust.Worn(true)), () => RavenSkills.CooldownSeconds("mstrike"), () => 0f),
            new RavenAbility("anger", true, 0, Lang.K("Злість (пасивний ефект)"), Lang.K("коли тебе вдарили, Мунін злиться, і ти якийсь час б'єш сильніше."),
                () => Lang.F("Коли тебе вдарили, Мунін злиться: {0} ти завдаєш на {1}% більше шкоди. Бере силу Муніна; спрацьовує не частіше ніж раз на {2}.", Span(WeakPassive.SecondsOf(true)), R(WeakPassive.PercentOf(true)), Span(WeakPassive.CooldownOf(true))),
                () => WeakPassive.Cost, () => WeakPassive.CooldownOf(true), () => WeakPassive.SecondsOf(true)),
            new RavenAbility("bloodfury", true, 0, Lang.K("Кривава лють (пасивний ефект)"), Lang.K("коли здоров'я падає нижче 30%, Мунін шаленіє, і ти б'єш сильніше й швидше."),
                () => Lang.F("Коли здоров'я падає нижче {0}%, Мунін шаленіє — {1} с ти завдаєш на {2}% більше шкоди й б'єш на {3}% швидше (разом із Люттю ще швидше). Бере силу Муніна; якщо сили бракує або ще не минула перезарядка — не спрацює.", R(BloodFury.Threshold * 100f), R(BloodFury.Duration()), R(BloodFury.DamagePercent), R(BloodFury.SpeedPercent)),
                () => BloodFury.Cost, () => BloodFury.Cooldown(), () => BloodFury.Duration()),
            new RavenAbility("mthought", true, 6, Lang.K("Думка Муніна"), Lang.K("Мунін усе бачить і все пам'ятає."),
                () => Lang.T("Мунін усе бачить і все пам'ятає: кожну твою дурницю, кожну рану, кожного ворога поруч. Спитай, якщо не боїшся відповіді. А в тиші він нагадає щось із вашого спільного минулого — і не завжди те, що хотілося б. Наведи приціл на іншого гравця — і Мунін пройдеться по ньому; його ворон може й огризнутися."),
                () => Power.MuninThoughtCost, () => RavenSkills.CooldownSeconds("mthought"), () => 0f),
            new RavenAbility("lightstep", false, 0, Lang.K("Легкий крок (пасивний ефект)"), Lang.K("коли біжиш і поруч нема ворогів, витривалість якийсь час відновлюється швидше."),
                () => Lang.F("Коли біжиш і поруч нема ворогів, Хугін підказує дорогу: {0} витривалість відновлюється на {1}% швидше. Бере силу Хугіна; спрацьовує не частіше ніж раз на {2}.", Span(WeakPassive.SecondsOf(false)), R(WeakPassive.PercentOf(false)), Span(WeakPassive.CooldownOf(false))),
                () => WeakPassive.Cost, () => WeakPassive.CooldownOf(false), () => WeakPassive.SecondsOf(false)),
            new RavenAbility("lastchance", false, 0, Lang.K("Останній шанс (пасивний ефект)"), Lang.K("Хугін рятує від смертельного удару — лишається 1 здоров'я і кілька секунд невразливості, щоб утекти."),
                () => Lang.F("Коли удар мав би тебе вбити, Хугін рятує — лишається 1 здоров'я і {0} с ніщо тебе не ранить, щоб утекти. Діє на все: удари, падіння, отруту, вогонь. Бере силу Хугіна; якщо сили бракує або ще не минула перезарядка — не врятує.", R(LastChance.SafeSeconds)),
                () => LastChance.Cost, () => LastChance.Cooldown(), () => 0f),
            new RavenAbility("hloyal", false, 0, Lang.K("Вірність (пасивний ефект)"), Lang.K("коли ти гинеш, Хугін може не піти в могилу й лишитися на плечі."),
                () => Lang.F("Коли ти гинеш, Хугін із шансом {0}% не йде в могилу разом з рештою речей: лишається на плечі й зустрічає тебе після відродження — бігти по нього до надгробка не треба. Сили не бере й перезарядки не має; не діє, поки Хугін відлетів.", R(Loyal.Chance * 100f)),
                () => 0f, () => 0f, () => 0f),
            new RavenAbility("mloyal", true, 0, Lang.K("Впертість (пасивний ефект)"), Lang.K("коли ти гинеш, Мунін може вчепитися в плече й не піти в могилу."),
                () => Lang.F("Коли ти гинеш, Мунін із шансом {0}% вчіплюється кігтями в плече й нікуди не йде — ні в могилу, ні до валькірій. Після відродження він уже сидить на своєму місці й, звісно, усе тобі пригадає. Сили не бере й перезарядки не має; не діє, поки Мунін відлетів.", R(Loyal.Chance * 100f)),
                () => 0f, () => 0f, () => 0f),
            new RavenAbility("scream", true, 2, Lang.K("Крик"), Lang.K("Вороги поруч втрачають рівновагу й тікають."),
                () => Lang.F("Мунін кричить так, що вороги ближче {0} м втрачають рівновагу й {1} с тікають від тебе. Боси лише втрачають рівновагу.", R(RavenSkills.ScreamRadius), R(Fear.Seconds)),
                () => Power.ScreamCost, () => RavenSkills.CooldownSeconds("scream"), () => 0f),
            new RavenAbility("fury", true, 3, Lang.K("Лють"), Lang.K("Удари, натяг лука, перезарядка арбалета й каст посохів швидші на якийсь час."),
                () => Lang.F("Удари, натяг лука, перезарядка арбалета й каст посохів на {0}% швидші. Силу боса не витрачає.", R(Fury.Percent())),
                () => Power.FuryCost, () => RavenSkills.CooldownSeconds("fury"), () => Fury.Duration()),
            new RavenAbility("wings", true, 4, Lang.K("Попутний вітер"), Lang.K("Бігаєш швидше на якийсь час."),
                () => Lang.F("+{0}% до швидкості руху, а біг коштує на {1}% менше витривалості.", R(Wings.SpeedNow()), R(Wings.RunStaminaPercent)),
                () => Power.WingsCost, () => RavenSkills.CooldownSeconds("wings"), () => Wings.Duration()),
            new RavenAbility("heal", true, 5, Lang.K("Зцілення Муніна"), Lang.K("Як випита медовуха: здоров'я відновлюється до повного, і ані краплі понад."),
                () => Lang.T("Як випита медовуха: здоров'я поступово відновлюється до повного й ні краплі понад максимум. Якщо тебе поранять, поки діє, все одно закінчиш із повним здоров'ям. Із медовухами перезарядку не ділить."),
                () => Power.HealCost, () => RavenSkills.CooldownSeconds("heal"), () => RavenHeal.Seconds),
            new RavenAbility("ward", true, 9, Lang.K("Заслона Муніна"), Lang.K("Перед тобою стіна з рун: вона приймає на себе удар спереду, хоч який сильний."),
                () => Lang.F("Мунін кидає руни, і перед тобою на {0} стає півколо червонуватих рун. Перший удар спереду (з усієї передньої половини) заслона бере на себе цілком, хоч би який він був: ні шкоди, ні відкидання, ні отрути, ні оглушення. Скільки той удар мав би зняти, видно червоним числом над тобою. Удар, що мав би тебе оглушити, оглушує того, хто бив, а близького ворога ще й відкидає назад. Тримає удари монстрів і босів, стріли, закляття й удари по площі, а з увімкненим PvP і удари гравців; отрута, вогонь, падіння й удар у спину проходять, як завжди. З {1}-го рівня довіри заслона витримує два удари, на {2}-му — три; скільки ще витримає, показують знаки-руни над нею, а після кожного удару вона тріскається й тьмяніє. Вдале парирування заряду не забирає: удар бере твій щит, а заслона чекає далі. Та якщо парирування мало б тебе оглушити (чи забракне витривалості), удар бере заслона: мінус заряд, і оглушується той, хто бив. Діє й під куполом.", Span(Ward.Duration()), Ward.TwoBlowsAt, Ward.ThreeBlowsAt),
                () => Power.WardCost, () => RavenSkills.CooldownSeconds("ward"), () => Ward.Duration()),
            // the great gifts (trust 6)
            new RavenAbility("hdecoy", false, 10, Lang.K("Обманка"), Lang.K("Хугін кружляє над ворогом і дражнить його: той б'ється лише з вороном."),
                () => Lang.F("Хугін летить до ворога, на якого ти дивишся (або до найближчого, до {0} м), кружляє над його головою й дражнить. {1} той не зважає ні на кого, крім ворона: ходить за ним і махає в повітря, і його удари нікого не ранять (Хугін ухиляється, летить пір'я; ворону це не шкодить). Та якщо гравці вдарять його {2} рази, він розкусить обманку й кинеться на того, хто бив, а Хугін повернеться. З {3}-го рівня довіри Хугін заодно водить за носа ще одного ворога поруч, з {4}-го — двох. Боси на це не купуються. Натисни дар ще раз — Хугін одразу повернеться.", R(Decoy.Range), Span(Decoy.Duration()), Decoy.HitsToBreak, Decoy.GangOneAt, Decoy.GangTwoAt),
                () => Power.DecoyCost, () => RavenSkills.CooldownSeconds("hdecoy"), () => Decoy.Duration()),
            new RavenAbility("hflight", false, 8, Lang.K("Крила Одіна"), Lang.K("Великий дар Хугіна: ти летиш, а обидва ворони — за твоєю спиною."),
                () => Lang.F("Хмара пір'я, Хугін злітає першим, а слідом підкидає й тебе. {0} ти летиш туди, куди дивиться камера (вбік — A/D), Пробіл — угору, присісти — униз, завжди з однаковою швидкістю, але з розгоном і гальмуванням; без клавіш висиш у повітрі. Хугін і Мунін летять за спиною, за Хугіном тягнеться слід пір'я. Не вище {1} м над землею; у польоті не можна атакувати зброєю, але ворони б'ють («Удар в очі», «Кігті»); не діють «Під крилом», «Розвідка» й «Злет» (Хугін тримає для тебе крила), а в повітрі ще «Крик», «Лють» і «Попутний вітер». Поки дар діє, можна сісти й піти пішки: опустись до самої землі з «присісти»; а пробіл на землі замість звичайного стрибка знову підкидає в небо. Натисни дар ще раз або дочекайся кінця — плавно опустишся, як на плащі левітації, без шкоди від падіння; на землі дар просто закінчиться. Не злітає під землею, під дахом, з води, верхи й з перевантаженням.", Span(OdinWings.Duration()), R(OdinWings.MaxHeight)),
                () => Power.FlightCost, () => RavenSkills.CooldownSeconds("hflight"), () => OdinWings.Duration()),
            new RavenAbility("mberserk", true, 8, Lang.K("Звір Муніна"), Lang.K("Мунін будить у тобі звіра: удвічі більший, сильніший і міцніший. Потім — втома."),
                () => Lang.F("Мунін будить у тобі звіра, як у берсерка: ти плавно виростаєш до {0}× себе — справжнім тілом, тож тебе й б'ють як велетня, а в тісному можна застрягти. {1} ти завдаєш на {2}% більше шкоди, броня на {3}% міцніша, тебе не оглушити й не відкинути, рука сягає далі, а удар ногою (без зброї, колесо миші) відкидає на {5}% далі. Бонуси додаються до «Злості» й «Кривавої люті»; «Лють» із звіром не працює, і «Крила Одіна» теж. Не в підземеллях (і зайдеш туди — звір вийде). Мунін на плечі лишається свого розміру. <color=#FF8A7A>Розплата: потім ти плавно меншаєш назад, і {4} витривалість відновлюється вдвічі повільніше.</color>", Beast.Size.ToString("0.#"), Span(Beast.Duration()), R(Beast.DamagePercent), R(Beast.ArmorPercent), Span(Beast.TiredSeconds), R((Beast.KickPush - 1f) * 100f)),
                () => Power.BeastCost, () => RavenSkills.CooldownSeconds("mberserk"), () => Beast.Duration()),
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

        internal static bool Stub(RavenAbility a) { return false; } // (every gift made; kept for the gifts to come)

        // the trust levels the small passives (anger, light step) and the strong ones (last chance, blood fury) open on
        internal static int WeakUnlock = 2, StrongUnlock = 4;

        // The trust level each gift opens on. The small and great passives and the dome keep their own settings;
        // "Gift unlock levels" in the config may set any of them ("light:3, wings:2").
        private static readonly Dictionary<string, int> Levels = new Dictionary<string, int>
        {
            { "thought", 1 }, { "scout", 1 }, { "hstrike", 1 }, { "light", 2 }, { "hdecoy", 2 }, { "leap", 4 }, { "shadow", 5 }, { "hflight", 6 },
            { "mthought", 1 }, { "mstrike", 1 }, { "scream", 2 }, { "wings", 3 }, { "heal", 4 }, { "fury", 5 }, { "ward", 4 }, { "mberserk", 6 },
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
            if (a.Id == "dome") return Dome.UnlockLevel;
            if (a.Id == "ward") return Ward.UnlockLevel;
            if (Loyal.Is(a)) return Loyal.UnlockLevel;
            if (a.Id == "anger" || a.Id == "lightstep") return WeakUnlock;
            int lv;
            return Levels.TryGetValue(a.Id, out lv) ? lv : 1;
        }

        // shown in the menu and told in full in the book: open now (an unwoken raven shows what it will have first)
        internal static bool Shown(RavenAbility a) { return a != null && UnlockLevel(a) <= Mathf.Max(1, Trust.LevelWorn(a.Munin)); }
        internal static bool Unlocked(RavenAbility a) { return Trust.LevelWorn(a.Munin) >= UnlockLevel(a); }
        internal static string LockedText(RavenAbility a) { return Lang.F("<color=#9a9a9a>відкриється на {0}-му рівні довіри</color>", UnlockLevel(a)); }

        // a passive's own state (ready or when again)
        internal static string PassiveReady(RavenAbility a)
        {
            if (!Unlocked(a)) return LockedText(a);
            switch (a.Id)
            {
                case "lastchance": return LastChance.ReadyText();
                case "bloodfury": return BloodFury.ReadyText();
                case "hloyal": case "mloyal": return Loyal.ReadyText(a.Id == "mloyal");
                default: return WeakPassive.ReadyText(a.Munin);
            }
        }

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
