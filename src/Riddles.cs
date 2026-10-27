#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Odin's riddles (the «riddle» slot and step): the thing is never named in the book, only the riddle; it is laid in
    // the nest's bowl and given to Odin. One is picked by your stage (its boss beaten) when the errand is given.
    internal static class Riddles
    {
        internal sealed class Riddle
        {
            public string Id, Prefab, Needs, Text;
            public int Count;
            public Riddle(string id, string prefab, int count, string needs, string text) { Id = id; Prefab = prefab; Count = count; Needs = needs; Text = text; }
        }

        internal static readonly List<Riddle> All = new List<Riddle>
        {
            new Riddle("dandelion", "Dandelion", 5, "", Lang.K("Жовта, як сонце, а постаріє — біла, як сивина, і розлітається від подиху.")),
            new Riddle("resin", "Resin", 10, "", Lang.K("Сльоза дерева, що горить ясніше за саме дерево.")),
            new Riddle("feathers", "Feathers", 10, "", Lang.K("Легке, як подих, а без нього жодна стріла не знайде дороги.")),
            new Riddle("flint", "Flint", 10, "", Lang.K("Камінь, що пам'ятає першу сокиру і перший вогонь.")),
            new Riddle("raspberry", "Raspberry", 10, "", Lang.K("Червона й солодка, ховається в кущах на узліссі, де колись горіло.")),
            new Riddle("mushroom", "Mushroom", 5, "", Lang.K("Росте без насіння в тіні, носить червоний капелюх і нікому не кланяється.")),
            new Riddle("bones", "BoneFragments", 10, "defeated_eikthyr", Lang.K("Те, що лишається, коли м'ясо пішло, а гордість ще ні.")),
            new Riddle("thistle", "Thistle", 5, "defeated_eikthyr", Lang.K("Синій колючий цвіт, що світиться в темряві лісу, де ходять тролі.")),
            new Riddle("honey", "Honey", 5, "defeated_eikthyr", Lang.K("Солодке, яке стережуть тисячі маленьких жал.")),
            new Riddle("entrails", "Entrails", 5, "defeated_gdking", Lang.K("Те, що мерці носять у собі, хоч давно вже не їдять.")),
            new Riddle("guck", "Guck", 5, "defeated_gdking", Lang.K("Зелене й липке, звисає з болотних дерев і чвакає, коли його рвуть.")),
            new Riddle("ooze", "Ooze", 5, "defeated_gdking", Lang.K("Лишається від того, хто помер без кісток, і вибухає в чужих руках.")),
            new Riddle("wolffang", "WolfFang", 5, "defeated_bonemass", Lang.K("Біле й гостре: вило на місяць, поки не стало твоїм.")),
            new Riddle("obsidian", "Obsidian", 10, "defeated_bonemass", Lang.K("Чорне скло, народжене у вогні гори, гостріше за будь-який ніж.")),
            new Riddle("crystal", "Crystal", 3, "defeated_bonemass", Lang.K("Холодне, як гора, а в ньому видно те, чого не бачить око.")),
            new Riddle("barley", "Barley", 10, "defeated_dragon", Lang.K("Золоте зерно рівнин: з нього печуть хліб і варять те, від чого співають.")),
            new Riddle("tar", "Tar", 5, "defeated_dragon", Lang.K("Чорна смола, в якій грузне і вовк, і фулінг, а вогонь її любить.")),
            new Riddle("coins", "Coins", 50, "", Lang.K("За нього торговці віддадуть усе, а ворону воно потрібне хіба що блищати.")),
            new Riddle("wood", "Wood", 20, "", Lang.K("Його рубають, а воно не кричить; горить — а гріє не себе.")),
            new Riddle("stone", "Stone", 20, "", Lang.K("Лежить скрізь і нікуди не йде, а з нього стоять стіни.")),
            new Riddle("deerhide", "DeerHide", 5, "", Lang.K("Знято з того, хто в лузі тікав найшвидше, та не досить швидко.")),
            new Riddle("leather", "LeatherScraps", 10, "", Lang.K("Клапті чужої шкури: на сорочку замало, на ремінь — саме те.")),
            new Riddle("meat", "RawMeat", 5, "", Lang.K("Червоне й холодне, а було гарячим і рохкало.")),
            new Riddle("necktail", "NeckTail", 5, "", Lang.K("Хвіст того, хто сичить біля води і кусає за п'яти.")),
            new Riddle("greydwarfeye", "GreydwarfEye", 10, "", Lang.K("Око того, хто сам наполовину пень, а все одно все бачить.")),
            new Riddle("blueberries", "Blueberries", 10, "defeated_eikthyr", Lang.K("Сині, як сутінки, ростуть у темному лісі, де ходять лише сміливі.")),
            new Riddle("carrot", "Carrot", 5, "defeated_eikthyr", Lang.K("Ховає свій вогонь під землею, а зверху махає зеленим чубом.")),
            new Riddle("copper", "CopperOre", 10, "defeated_eikthyr", Lang.K("Руда кольору заходу сонця, що спить у каменях темного лісу.")),
            new Riddle("tin", "TinOre", 10, "defeated_eikthyr", Lang.K("Сіра руда з річкових берегів: сама нічого не варта, а в парі — бронза.")),
            new Riddle("bronze", "Bronze", 3, "defeated_eikthyr", Lang.K("Народжений із двох руд у печі, твердіший за обох батьків.")),
            new Riddle("coal", "Coal", 20, "defeated_eikthyr", Lang.K("Чорне, що лишилося від дерева після вогню, а саме палить ще жаркіше.")),
            new Riddle("trollhide", "TrollHide", 3, "defeated_eikthyr", Lang.K("Синя шкура велетня, що валить дерева голими руками.")),
            new Riddle("surtling", "SurtlingCore", 3, "defeated_gdking", Lang.K("Вогняне серце малого духа з болота: гасне в руках, а горить у печі.")),
            new Riddle("bloodbag", "Bloodbag", 5, "defeated_gdking", Lang.K("Мішечок, повний чужої крові: його носив той, хто присмоктується в воді.")),
            new Riddle("ironscrap", "IronScrap", 10, "defeated_gdking", Lang.K("Іржаве залізо, яке болото ховає в купах мулу, наче соромиться його.")),
            new Riddle("chain", "Chain", 2, "defeated_gdking", Lang.K("Кільце в кільце, і жодне не втече: ним тримали тих, хто не мав утекти.")),
            new Riddle("iron", "Iron", 5, "defeated_bonemass", Lang.K("Вийшло з болотного мулу, а тепер з нього кують те, чого боїться мул.")),
            new Riddle("silver", "SilverOre", 5, "defeated_bonemass", Lang.K("Біла руда з гірських надр: мерці її ненавидять, а вовки виють над нею.")),
            new Riddle("wolfpelt", "WolfPelt", 5, "defeated_bonemass", Lang.K("Сіра шуба, що ще пам'ятає, як вила на місяць.")),
            new Riddle("freezegland", "FreezeGland", 3, "defeated_bonemass", Lang.K("Холод у мішечку: його носила та, що плюється кригою з неба.")),
            new Riddle("onion", "Onion", 5, "defeated_bonemass", Lang.K("Сто сорочок, і всі без ґудзиків; хто роздягає — той плаче.")),
            new Riddle("cloudberry", "Cloudberry", 10, "defeated_dragon", Lang.K("Золота ягода рівнин, де трава по пояс і кожен кущ кусається.")),
            new Riddle("needle", "Needle", 5, "defeated_dragon", Lang.K("Жало, що летить швидше за стрілу і завжди цілить у шию.")),
            new Riddle("flax", "Flax", 10, "defeated_dragon", Lang.K("Синій цвіт рівнин: сьогодні гойдається на вітрі, а завтра стане сорочкою.")),
            new Riddle("loxpelt", "LoxPelt", 2, "defeated_dragon", Lang.K("Шкура того, хто ходить рівнинами, як гора на ногах.")),
            new Riddle("blackmetal", "BlackMetalScrap", 10, "defeated_dragon", Lang.K("Чорне залізо, яке малі злі люди носять, бо самі його не вміють кувати.")),
            new Riddle("sap", "Sap", 5, "defeated_goblinking", Lang.K("Сік, яким плаче найстаріше коріння в тумані.")),
            new Riddle("softtissue", "Softtissue", 5, "defeated_goblinking", Lang.K("М'яке, як подушка, з того, хто ходить у тумані й світиться зсередини.")),
            new Riddle("eitr", "Eitr", 3, "defeated_queen", Lang.K("Чиста сила в краплі: її п'ють чаклуни, а простий воїн обпікся б.")),
            new Riddle("charredbone", "CharredBone", 10, "defeated_queen", Lang.K("Кістки, що вже горіли один раз, і більше нічого не бояться.")),
            new Riddle("frostwood", "Frostwood", 20, "defeated_fader", Lang.K("Дерево, що росте там, де замерзає навіть подих, і не тріскає від морозу.")),
            new Riddle("frostcore", "FrostCore", 3, "defeated_fader", Lang.K("Серце холоду, що висить у кризі й не тане навіть біля вогню.")),
            new Riddle("lingonberry", "Lingonberry", 10, "defeated_fader", Lang.K("Кисла червона ягода, що ховається під снігом і чекає того, хто не полінується копати.")),
            new Riddle("moosehide", "MooseHide", 3, "defeated_fader", Lang.K("Шкура того, хто носить на голові гілля, а Північчю ходить, як конунг.")),
            new Riddle("moosesinew", "MooseSinew", 3, "defeated_fader", Lang.K("Жила північного велетня: з неї в'яжуть тятиву, що не рветься на морозі.")),
            new Riddle("moosemeat", "MooseMeat", 5, "defeated_fader", Lang.K("М'ясо того, хто їсть лише гілки, а важить, як три кабани.")),
            new Riddle("blubber", "SealBlubber", 5, "defeated_fader", Lang.K("Жир того, хто лежить на кризі, як мішок, і плаче, коли до нього підходиш.")),
            new Riddle("elakinghair", "ElakingHair", 3, "defeated_fader", Lang.K("Шерсть звіра, що риє сніг, як кріт, і вистрибує просто з-під ніг.")),
            new Riddle("jotunwitch", "TrophyJotunWitch", 1, "defeated_fader", Lang.K("Голова тієї, що каркає гірше за ворона, а гордиться більше.")),
        };

        internal static Riddle Find(string id) { foreach (Riddle r in All) if (r.Id == id) return r; return null; }

        // one that fits your stage, at random (null when none)
        internal static Riddle Pick(Player p)
        {
            List<Riddle> can = new List<Riddle>();
            foreach (Riddle r in All)
                if (r.Needs.Length == 0 || Stage.Has(p, r.Needs))
                    if (ObjectDB.instance == null || ObjectDB.instance.GetItemPrefab(r.Prefab) != null) can.Add(r);
            return can.Count > 0 ? can[UnityEngine.Random.Range(0, can.Count)] : null;
        }
    }
}
#endif
