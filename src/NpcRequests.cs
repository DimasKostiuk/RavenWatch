#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A resident's request (npc-plan.md, «прохання»): now and then (once in one and a half to three game days) it asks
    // for a thing, only of the lands already reached, and only what is true:
    //   for its work   what its chest really lacks and has room for (a stoker wood, a smelter ore or coal, a cook meat…): brought, it goes into its carry and from there to
    //                  its chest as all it carries does (NpcWorker.Receive); no chest, a chest with enough in it, or no room: not asked
    //   for its gear   a guard with a bow: arrows, into its cells
    //   for itself     gloves, a pillow, something sweet: it is used up. Nothing true for its work: it asks one of these (ForJob[""])
    // Its reason is one of a few, chosen as it asks (Ask.Whys). Said aloud with the right form of the number (Said: «30 полін»); the board's plain list keeps the game's own name.
    // Passed unbrought it lets go, says so if you are near and remembers it a little (request_lapsed).
    // On the heart's board and in its mouth when you are near. Brought (its talk: «Ось те, що ти просив»), it pays at once, by what was asked (Worth × PayOver,
    // never the very thing it asked for, nothing worth much more than that, nothing of later lands than what it asked for):
    //   work        what its work makes (bars, a cooked meal, mead, rare mushrooms, arrows…)
    //   coins       the game's coins
    //   valuable    amber, an amber pearl, a silver necklace, a ruby, when what was asked is worth that much
    // It waits three game days; two at most at once at a base. Kept as one short string in its locker (NpcLocker: not in
    // the walking person's ZDO, which is sent whole many times a second), written by its owner when it asks and when it
    // is done; what is taken and given happens on the game of the one who brings it.
    internal static class NpcRequests
    {
        internal static readonly int KRequest = "RavenWatch_npcRequest".GetStableHashCode(); // string "item|count|reward|rcount|until|why|goes" ("" none) — in its locker
        internal static readonly int KAskNext = "RavenWatch_npcRequestNext".GetStableHashCode(); // long: world seconds of the next ask — in its locker (NpcLocker.Lives)
        private const float DaysMin = 1.5f, DaysMax = 3f, Lasts = 3f;
        private const int PerBase = 2;
        internal static float PayOver = 1.3f, PayLeast = 5f, PayMostOver = 1.4f; // × what was asked is worth · never less, in coins · a thing worth more than this × what it pays is not paid with
        // what you brought for its work, not used yet: «name|left|until;…» ("" none) — in its locker. Taken back out of its chest, it would lack it again and ask for the
        // same and pay again (a farm: the same thirty logs, paid every few days). So the same thing is not asked for while what you brought is unused (its own takes
        // from its chest count it down: Used) or until BroughtDays pass; arrows in a guard's cells: only the days
        internal static readonly int KBrought = "RavenWatch_npcRequestBrought".GetStableHashCode();
        internal static float BroughtDays = 8f;

        // where what it asked for goes: used up by itself · its chest, by its carry · its gear's cells
        internal enum Goes { Self, Chest, Cells }

        // ---------------------------------------------------------------- what may be asked: [item, count, the lands it needs (0 Meadows … 6 Ashlands), what it is for, where it goes]
        private sealed class Ask
        {
            public string Item; public int Count, Tier; public string[] Whys; public Goes To;
            public Ask(string i, int c, int t, string[] whys, Goes to = Goes.Self) { Item = i; Count = c; Tier = t; Whys = whys; To = to; }
            public Ask(string i, int t) { Item = i; Tier = t; Whys = new string[0]; } // (what its work makes: pay)
        }
        private static string[] W(params string[] whys) { return whys; } // (its reasons; each marked for the table: Lang.K)
        private static readonly Dictionary<string, Ask[]> ForJob = new Dictionary<string, Ask[]>
        {
            { "stoker", new[] { new Ask("Wood", 30, 0, W(Lang.K("дрова кінчаються"), Lang.K("вогонь не чекає"), Lang.K("у ящику вже видно дно"), Lang.K("на ніч не вистачить")), Goes.Chest), new Ask("Resin", 15, 0, W(Lang.K("смолоскипам живиці бракує"), Lang.K("смолоскипи вже ледь горять"), Lang.K("без неї тут стане темно")), Goes.Chest), new Ask("Coal", 10, 1, W(Lang.K("жаровні гаснуть"), Lang.K("жаровні ледь тліють"), Lang.K("на вугіллі жар тримається довше")), Goes.Chest) } },
            { "smelter", new[] { new Ask("Coal", 20, 1, W(Lang.K("піч голодна"), Lang.K("піч вистигає"), Lang.K("без вугілля я тут лише дивлюсь"), Lang.K("жару не вистачає")), Goes.Chest), new Ask("CopperOre", 10, 1, W(Lang.K("без руди піч стоїть"), Lang.K("мідь скінчилась"), Lang.K("піч є, а плавити нічого")), Goes.Chest), new Ask("TinOre", 10, 1, W(Lang.K("на бронзу олова треба"), Lang.K("без олова бронзи не буде"), Lang.K("міді вистачає, а олова ні")), Goes.Chest), new Ask("IronScrap", 10, 2, W(Lang.K("залізо скінчилось"), Lang.K("залізо тут усім потрібне"), Lang.K("брухт перетоплю, ніхто й не впізнає")), Goes.Chest), new Ask("SilverOre", 10, 3, W(Lang.K("хочу спробувати срібло"), Lang.K("срібла ще не плавив ніхто з нас"), Lang.K("піч готова до срібла")), Goes.Chest) } },
            { "cook", new[] { new Ask("RawMeat", 6, 0, W(Lang.K("м'яса на всіх не вистачає"), Lang.K("котел порожній"), Lang.K("люди вже питають про обід")), Goes.Chest), new Ask("DeerMeat", 5, 0, W(Lang.K("на юшку"), Lang.K("оленина вийде на славу"), Lang.K("хочу зварити щось путнє")), Goes.Chest), new Ask("NeckTail", 6, 0, W(Lang.K("на смаженину"), Lang.K("їх добре смажити до хрускоту"), Lang.K("усі їх люблять, хоч і не зізнаються")), Goes.Chest), new Ask("FishRaw", 4, 1, W(Lang.K("на суп"), Lang.K("давно не було риби"), Lang.K("м'ясо вже всім набридло")), Goes.Chest), new Ask("LoxMeat", 4, 4, W(Lang.K("на свято"), Lang.K("такого ми ще не куштували"), Lang.K("хочу справжнього м'яса на стіл")), Goes.Chest) } },
            { "keeper", new[] { new Ask("Honey", 5, 0, W(Lang.K("на медове сусло"), Lang.K("без меду медовухи не буде"), Lang.K("бджоли не встигають")), Goes.Chest), new Ask("Raspberry", 15, 0, W(Lang.K("на медовуху"), Lang.K("бочки стоять порожні"), Lang.K("малина дасть гарний смак")), Goes.Chest), new Ask("Blueberries", 15, 1, W(Lang.K("на медовуху"), Lang.K("бочки стоять порожні"), Lang.K("з чорниці медовуха вийде міцніша")), Goes.Chest), new Ask("Barley", 10, 4, W(Lang.K("на пиво"), Lang.K("час спробувати щось міцніше"), Lang.K("люди заслужили пива")), Goes.Chest) } },
            { "gatherer", new[] { new Ask("Feathers", 10, 0, W(Lang.K("на м'яку підстилку"), Lang.K("кошики вистелити"), Lang.K("ягоди в кошику давляться"))), new Ask("LeatherScraps", 10, 0, W(Lang.K("на торби"), Lang.K("торба геть протерлась"), Lang.K("усе висипається дорогою"))), new Ask("FineWood", 10, 1, W(Lang.K("на міцні кошики"), Lang.K("старі кошики розсипаються"), Lang.K("хочу зробити щось на роки"))) } },
            { "cleaner", new[] { new Ask("Resin", 10, 0, W(Lang.K("мітлу підклеїти"), Lang.K("мітла розсипається в руках"), Lang.K("рукоять уже хитається"))), new Ask("DeerHide", 4, 0, W(Lang.K("на ганчірки"), Lang.K("старі ганчірки вже нічого не витирають"), Lang.K("бруду більше, ніж ганчірок"))) } },
            { "guard", new[] { new Ask("ArrowFlint", 30, 0, W(Lang.K("на варті без них ніяк"), Lang.K("сагайдак майже порожній"), Lang.K("вартовий без стріл просто глядач")), Goes.Cells), new Ask("ArrowBronze", 30, 1, W(Lang.K("старі стріли скінчились"), Lang.K("кремінь звірів уже не бере"), Lang.K("хочу бити сильніше")), Goes.Cells), new Ask("ArrowIron", 30, 2, W(Lang.K("проти тих, що в броні"), Lang.K("бронзові стріли від броні відскакують"), Lang.K("вороги стали міцнішими")), Goes.Cells), new Ask("TrollHide", 3, 1, W(Lang.K("на обладунок"), Lang.K("обладунок тріщить на плечах"), Lang.K("наступного удару мій обладунок не витримає"))) } },
            { "lumber", new[] { new Ask("LeatherScraps", 10, 0, W(Lang.K("рукавиці стерлись"), Lang.K("долоні вже в мозолях"), Lang.K("сокира вислизає з рук"))), new Ask("Resin", 10, 0, W(Lang.K("топорище підклеїти"), Lang.K("сокира хитається на топорищі"), Lang.K("топорище ось-ось вилетить"))), new Ask("TrollHide", 2, 1, WarmCoat) } },
            { "miner", new[] { new Ask("LeatherScraps", 10, 0, W(Lang.K("на налокітники"), Lang.K("у шахті лікті стираються першими"), Lang.K("камінь не жаліє рук"))), new Ask("Resin", 15, 0, W(Lang.K("смолоскипи в шахту"), Lang.K("у шахті нічого не видно"), Lang.K("наосліп кайлом не помахаєш"))), new Ask("TrollHide", 2, 1, WarmCoat) } },
            { "servant", new[] { new Ask("Resin", 20, 0, W(Lang.K("світильникам живиці бракує"), Lang.K("у домі вже темніє"), Lang.K("світильники ледь горять")), Goes.Chest), new Ask("Wood", 20, 0, W(Lang.K("дров у дім треба"), Lang.K("у домі холоднішає"), Lang.K("хочу, щоб дім стояв теплий")), Goes.Chest), new Ask("Coal", 10, 1, W(Lang.K("у домі жаровні холонуть"), Lang.K("жаровні в домі гаснуть"), Lang.K("у домі має бути тепло")), Goes.Chest) } },
            { "", new[] { new Ask("Raspberry", 10, 0, W(Lang.K("давно не їв|їла солодкого"), Lang.K("смак дитинства, мабуть"), Lang.K("хочеться чогось не з котла"))), new Ask("Honey", 3, 0, W(Lang.K("на свято"), Lang.K("хочу собі трохи радості"), Lang.K("від кашлю добре"))), new Ask("Feathers", 5, 0, W(Lang.K("на подушку"), Lang.K("голові твердо"), Lang.K("сплю на кулаку"))), new Ask("Blueberries", 10, 1, W(Lang.K("люблю чорницю"), Lang.K("хочу, щоб губи були сині"), Lang.K("у лісі її повно, а мені не можна туди"))), new Ask("Cloudberry", 6, 4, W(Lang.K("сниться ночами"), Lang.K("раз скуштував|скуштувала й не можу забути"), Lang.K("кажуть, вона гріє зсередини"))) } },
        };

        private static string[] WarmCoat { get { return W(Lang.K("на теплу куртку"), Lang.K("вітер продуває наскрізь"), Lang.K("без теплого одягу зиму не витримаю")); } } // (a property, not a field: ForJob above is made first and reads it)

        // how it says a number of a thing: its three forms (Lang.N); a thing not here: the board's plain words
        private static readonly Dictionary<string, string> Forms = new Dictionary<string, string>
        {
            { "Wood", Lang.K("поліно|поліна|полін") }, { "FineWood", Lang.K("поліно доброго дерева|поліна доброго дерева|полін доброго дерева") }, { "Resin", Lang.K("шматок живиці|шматки живиці|шматків живиці") }, { "Coal", Lang.K("шматок вугілля|шматки вугілля|шматків вугілля") },
            { "CopperOre", Lang.K("шматок мідної руди|шматки мідної руди|шматків мідної руди") }, { "TinOre", Lang.K("шматок олов'яної руди|шматки олов'яної руди|шматків олов'яної руди") }, { "IronScrap", Lang.K("шматок залізного брухту|шматки залізного брухту|шматків залізного брухту") }, { "SilverOre", Lang.K("шматок срібної руди|шматки срібної руди|шматків срібної руди") },
            { "RawMeat", Lang.K("шматок сирого м'яса|шматки сирого м'яса|шматків сирого м'яса") }, { "DeerMeat", Lang.K("шматок оленини|шматки оленини|шматків оленини") }, { "NeckTail", Lang.K("хвіст ящірки|хвости ящірок|хвостів ящірок") }, { "FishRaw", Lang.K("сира риба|сирі риби|сирих риб") }, { "LoxMeat", Lang.K("шматок м'яса локса|шматки м'яса локса|шматків м'яса локса") },
            { "Honey", Lang.K("міра меду|міри меду|мір меду") }, { "Raspberry", Lang.K("ягода малини|ягоди малини|ягід малини") }, { "Blueberries", Lang.K("ягода чорниці|ягоди чорниці|ягід чорниці") }, { "Cloudberry", Lang.K("ягода морошки|ягоди морошки|ягід морошки") }, { "Barley", Lang.K("міра ячменю|міри ячменю|мір ячменю") },
            { "Feathers", Lang.K("пір'їна|пір'їни|пір'їн") }, { "LeatherScraps", Lang.K("клапоть шкіри|клапті шкіри|клаптів шкіри") }, { "DeerHide", Lang.K("оленяча шкура|оленячі шкури|оленячих шкур") }, { "TrollHide", Lang.K("шкура троля|шкури троля|шкур троля") },
            { "ArrowFlint", Lang.K("кременева стріла|кременеві стріли|кременевих стріл") }, { "ArrowBronze", Lang.K("бронзова стріла|бронзові стріли|бронзових стріл") }, { "ArrowIron", Lang.K("залізна стріла|залізні стріли|залізних стріл") },
        };
        private static string Said(string item, int count) { string f; return Forms.TryGetValue(item, out f) ? Lang.N(count, f) : Words(item, count); }

        // what its work makes, as pay: [item, the lands it needs]; how many: by what was asked (Pay)
        private static readonly Dictionary<string, Ask[]> Makes = new Dictionary<string, Ask[]>
        {
            { "stoker", new[] { new Ask("Coal", 1) } },
            { "smelter", new[] { new Ask("Bronze", 1), new Ask("Iron", 2), new Ask("Silver", 3), new Ask("BlackMetal", 4) } },
            { "cook", new[] { new Ask("CookedMeat", 0), new Ask("QueensJam", 1), new Ask("TurnipStew", 2), new Ask("Bread", 4) } },
            { "keeper", new[] { new Ask("MeadHealthMinor", 1), new Ask("MeadStaminaMinor", 1), new Ask("MeadHealthMedium", 3) } },
            { "gatherer", new[] { new Ask("MushroomYellow", 1), new Ask("Cloudberry", 4), new Ask("Thistle", 1) } },
            { "lumber", new[] { new Ask("Wood", 0), new Ask("FineWood", 1), new Ask("RoundLog", 2) } },
            { "miner", new[] { new Ask("Stone", 0), new Ask("CopperOre", 1), new Ask("IronScrap", 2), new Ask("SilverOre", 3) } },
        };

        // what a thing is worth, in coins: the labour of it, so that a request's pay is by what was asked. A valuable (amber, a ruby): the game's own price, not here
        private static readonly Dictionary<string, float> Worth = new Dictionary<string, float>
        {
            { "Wood", 0.5f }, { "Stone", 0.5f }, { "Coal", 1f }, { "Resin", 1f }, { "Feathers", 1f }, { "LeatherScraps", 1f }, { "DeerHide", 2f }, { "TrollHide", 5f }, { "FineWood", 1f }, { "RoundLog", 1.5f },
            { "Raspberry", 0.5f }, { "Blueberries", 0.5f }, { "Cloudberry", 2f }, { "Honey", 2f }, { "Barley", 2f }, { "Thistle", 1.5f }, { "MushroomYellow", 2f },
            { "RawMeat", 1.5f }, { "DeerMeat", 2f }, { "NeckTail", 1f }, { "FishRaw", 2f }, { "LoxMeat", 5f }, { "CookedMeat", 2f }, { "QueensJam", 4f }, { "TurnipStew", 4f }, { "Bread", 6f },
            { "CopperOre", 2f }, { "TinOre", 2f }, { "IronScrap", 3f }, { "SilverOre", 5f }, { "Bronze", 7f }, { "Iron", 9f }, { "Silver", 12f }, { "BlackMetal", 16f },
            { "ArrowFlint", 0.5f }, { "ArrowBronze", 1f }, { "ArrowIron", 1.5f },
            { "MeadHealthMinor", 6f }, { "MeadStaminaMinor", 6f }, { "MeadHealthMedium", 12f },
        };
        internal static float WorthOf(string item)
        {
            float w;
            if (Worth.TryGetValue(item, out w)) return w;
            ItemDrop d = DropOf(item);
            return d != null && d.m_itemData.m_shared.m_value > 0 ? d.m_itemData.m_shared.m_value : 2f;
        }

        // ---------------------------------------------------------------- the lands reached (the server's keys, on every game)
        internal static int Tier()
        {
            ZoneSystem z = ZoneSystem.instance;
            if (z == null) return 0;
            string[] keys = { "defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon", "defeated_goblinking", "defeated_queen" };
            int t = 0;
            foreach (string k in keys) if (z.GetGlobalKey(k)) t++; else break;
            return t;
        }

        // ---------------------------------------------------------------- asking (where it is owned, now and then: NpcResident)
        private static readonly Dictionary<NpcActor, float> NextLook = new Dictionary<NpcActor, float>(); // session: its next look
        internal static void Forget(NpcActor a) { NextLook.Remove(a); }

        internal static void Tick(NpcActor a)
        {
            if (a == null || !a.Owned || a.Zdo == null || !NpcResident.Is(a) || !a.Def.Settler) return; // (a person of the story has no way to take what it asked for)
            float look;
            if (NextLook.TryGetValue(a, out look) && Time.time < look) return;
            if (NextLook.Count > 64) NextLook.Clear();
            NextLook[a] = Time.time + 30f;
            ZDO z = a.Zdo;
            long now = (long)NpcRescue.Now;
            string req = Kept(a);
            if (req == null) return; // (its locker not here yet: later)
            if (req.Length > 0)
            {
                Request r = Parse(req);
                if (r == null || now > r.Until) { NpcLocker.Set(a, KRequest, ""); NpcLocker.Put(z, KAskNext, now + (long)(Day * DaysMin)); Lapsed(a, r); Npcs.Log(a.Id + ": its request ran out"); }
                else if (Player.GetClosestPlayer(a.transform.position, 8f) != null && UnityEngine.Random.value < 0.15f && NpcVoice.Free(a)) NpcPhrases.SayOr(a, "request_nag", Plain(a, r), Values(r));
                return;
            }
            long next = NpcLocker.Long(z, KAskNext, 0L);
            if (next == 0L) { NpcLocker.Put(z, KAskNext, now + (long)(Day * UnityEngine.Random.Range(0.5f, DaysMax))); return; } // (new here: the first ask a little later)
            if (now < next) return;
            NpcLocker.Put(z, KAskNext, now + (long)(Day * UnityEngine.Random.Range(DaysMin, DaysMax)));
            if (Asking(a) >= PerBase) return;
            Request made = Make(a, now);
            if (made == null) return;
            if (!NpcLocker.Set(a, KRequest, made.Code())) return;
            Npcs.Log(a.Id + ": asks for " + made.Count + " " + made.Item + " (pays " + made.Reward + " " + made.RewardCount + ")");
            if (Player.GetClosestPlayer(a.transform.position, 12f) != null) NpcPhrases.SayOr(a, "request_new", Plain(a, made), Values(made));
        }

        // it stopped waiting: remembered a little, and said if someone is near
        private static void Lapsed(NpcActor a, Request r)
        {
            if (r == null) return;
            NpcMemory.Remember(a, "request_lapsed", 0L, Words(r.Item, r.Count));
            if (Player.GetClosestPlayer(a.transform.position, 12f) != null) NpcPhrases.SayOr(a, "request_lapsed", a.Form(Lang.T("Гаразд. Обійдуся без цього.")), null, NpcActor.Voice.Unasked);
        }

        // its ask in plain words (a job's goods have no «why»: none said)
        private static string Plain(NpcActor a, Request r)
        {
            return a.Form(r.Why.Length > 0 ? Lang.F("Мені б {0} — {1}.", Said(r.Item, r.Count), Lang.T(r.Why)) : Lang.F("Мені б {0}.", Said(r.Item, r.Count)));
        }

        // its ask as values for its made words: {item}, and {why} when it has one
        private static Dictionary<string, string> Values(Request r)
        {
            Dictionary<string, string> v = new Dictionary<string, string> { { "item", Said(r.Item, r.Count) } };
            if (r.Why.Length > 0) v["why"] = Lang.T(r.Why);
            return v;
        }

        // for testing (the console): ask now
        internal static string Force(NpcActor a)
        {
            if (a == null || !a.Owned || a.Zdo == null) return "Його веде інша гра.";
            if (!NpcResident.Is(a)) return "Просять лише мешканці.";
            if (Kept(a) == null) return "Його шафка ще не тут — за мить.";
            NpcLocker.Set(a, KRequest, ""); NpcLocker.Put(a.Zdo, KAskNext, 1L); NextLook.Remove(a);
            Tick(a);
            return (Kept(a) ?? "").Length > 0 ? "Просить: " + Text(a) : "Не вийшло (на базі вже двоє просять?).";
        }

        private static float Day { get { return EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f; } }

        // how many at its base ask now (two at most)
        private static int Asking(NpcActor a)
        {
            int n = 0;
            Vector3 home = NpcRules.Home(a);
            foreach (NpcActor o in NpcActor.Live) if (o != null && o != a && (Kept(o) ?? "").Length > 0 && Utils.DistanceXZ(NpcRules.Home(o), home) < NpcBase.Reach) n++;
            return n;
        }

        private static Request Make(NpcActor a, long now)
        {
            int tier = Tier();
            string job = a.Worker != null && a.Worker.Works ? a.Worker.JobId : "";
            Ask[] pool;
            if (!ForJob.TryGetValue(job, out pool)) pool = ForJob[""];
            List<Ask> fit = new List<Ask>();
            foreach (Ask x in pool) if (x.Tier <= tier && Exists(x.Item) && Lacks(a, x)) fit.Add(x);
            if (fit.Count == 0) foreach (Ask x in ForJob[""]) if (x.Tier <= tier && Exists(x.Item)) fit.Add(x); // (nothing true for its work: something of its own)
            if (fit.Count == 0) return null;
            Ask ask = fit[UnityEngine.Random.Range(0, fit.Count)];
            Request r = new Request { Item = ask.Item, Count = ask.Count, Why = ask.Whys.Length > 0 ? ask.Whys[UnityEngine.Random.Range(0, ask.Whys.Length)] : "", To = ask.To, Until = now + (long)(Day * Lasts) };
            Pay(r, ask.Tier, job, tier);
            return r;
        }

        // is this really wanted now: its chest has less of it than it asks for (no chest: none to put it in) · a guard with a bow for these arrows, short of them · for itself: always
        private static bool Lacks(NpcActor a, Ask x)
        {
            ItemDrop d = DropOf(x.Item);
            if (d == null) return false;
            string name = d.m_itemData.m_shared.m_name;
            if (x.To != Goes.Self && Unused(a, name)) return false; // (what you brought last time is still there, unused)
            switch (x.To)
            {
                case Goes.Chest: { Container c = a.Worker != null ? a.Worker.Chest : null; return c != null && a.Worker.InChest(name) < x.Count && NpcWorker.Space(c.GetInventory(), d.m_itemData) >= x.Count; } // (a link read, its chest's list counted: once an ask)
                case Goes.Cells: return a.Gear != null && a.Gear.Cells != null && a.Gear.Far != null && a.Gear.Far.m_shared.m_ammoType == d.m_itemData.m_shared.m_ammoType && a.Gear.Cells.CountItems(name) < x.Count;
                default: return true;
            }
        }

        // the valuable that fits this pay, the dearest first, of the lands reached (null: all worth too much)
        private static string Valuable(float value, int tier)
        {
            if (tier >= 5 && Exists("Ruby") && WorthOf("Ruby") <= value * PayMostOver) return "Ruby";
            if (tier >= 3 && Exists("SilverNecklace") && WorthOf("SilverNecklace") <= value * PayMostOver) return "SilverNecklace";
            if (tier >= 1 && Exists("AmberPearl") && WorthOf("AmberPearl") <= value * PayMostOver) return "AmberPearl";
            return Exists("Amber") && WorthOf("Amber") <= value * PayMostOver ? "Amber" : null;
        }

        // how many of it make this pay
        private static int HowMany(float value, string item) { return Mathf.Max(1, Mathf.RoundToInt(value / WorthOf(item))); }

        // what it pays, by what it asked for: its work's make (when it has one), coins, or a valuable; never the very thing asked for, nor anything worth much more than the pay
        private static void Pay(Request r, int askTier, string job, int tier)
        {
            float value = Mathf.Max(PayLeast, r.Count * WorthOf(r.Item) * PayOver);
            List<KeyValuePair<string, float>> kinds = new List<KeyValuePair<string, float>>();
            Ask[] makes;
            List<Ask> made = new List<Ask>();
            if (Makes.TryGetValue(job, out makes)) foreach (Ask m in makes) if (m.Tier <= tier && m.Tier <= askTier && m.Item != r.Item && Exists(m.Item) && WorthOf(m.Item) <= value * PayMostOver) made.Add(m);
            if (made.Count > 0) kinds.Add(new KeyValuePair<string, float>("work", 2f));
            kinds.Add(new KeyValuePair<string, float>("coins", 1.5f));
            string valuable = Valuable(value, tier);
            if (valuable != null) kinds.Add(new KeyValuePair<string, float>("valuable", 1f));
            float sum = 0f; foreach (KeyValuePair<string, float> k in kinds) sum += k.Value;
            float roll = UnityEngine.Random.value * sum;
            string kind = "coins";
            foreach (KeyValuePair<string, float> k in kinds) { roll -= k.Value; if (roll <= 0f) { kind = k.Key; break; } }
            switch (kind)
            {
                case "work": { Ask m = made[made.Count - 1 - UnityEngine.Random.Range(0, Mathf.Min(2, made.Count))]; r.Reward = m.Item; r.RewardCount = HowMany(value, m.Item); break; } // (one of the best two it can pay with)
                case "valuable": r.Reward = valuable; r.RewardCount = HowMany(value, valuable); break;
                default: r.Reward = "Coins"; r.RewardCount = Mathf.RoundToInt(value * UnityEngine.Random.Range(0.9f, 1.15f)); break;
            }
        }

        private static bool Exists(string item) { return ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(item) != null; }
        private static ItemDrop DropOf(string item) { GameObject p = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item) : null; return p != null ? p.GetComponent<ItemDrop>() : null; }

        // ---------------------------------------------------------------- a request
        internal sealed class Request
        {
            public string Item = "", Reward = "Coins", Why = ""; public int Count, RewardCount; public long Until;
            internal Goes To;
            public string Code() { return Item + "|" + Count + "|" + Reward + "|" + RewardCount + "|" + Until + "|" + Why.Replace("|", "¦") + "|" + (int)To; }
        }

        internal static Request Parse(string s)
        {
            string[] f = (s ?? "").Split('|');
            if (f.Length < 5) return null;
            Request r = new Request { Item = f[0], Count = Errands.I(f[1], 1), Reward = f[2], RewardCount = Errands.I(f[3], 1), Why = f.Length > 5 ? f[5].Replace("¦", "|") : "" };
            long u; long.TryParse(f[4], out u); r.Until = u;
            if (f.Length > 6) r.To = (Goes)Errands.I(f[6], 0);
            return r;
        }

        // its request as kept (null: its locker not here yet; "" none)
        private static string Kept(NpcActor a) { return a != null ? NpcLocker.Get(a, KRequest) : null; }

        internal static Request Of(NpcActor a)
        {
            Request r = Parse(Kept(a));
            return r != null && r.Until >= (long)NpcRescue.Now && !Given.Contains(GivenKey(a, r)) ? r : null;
        }
        private static readonly HashSet<string> Given = new HashSet<string>(); // session: «seed@until» brought here, its owner not yet heard from
        private static string GivenKey(NpcActor a, Request r) { return NpcKit.Seed(a.Zdo).ToString(CultureInfo.InvariantCulture) + "@" + r.Until.ToString(CultureInfo.InvariantCulture); }

        // «20 смоли» (the game's own name of a thing, in your language)
        internal static string Words(string item, int count)
        {
            ItemDrop d = DropOf(item);
            string name = d != null && Localization.instance != null ? Localization.instance.Localize(d.m_itemData.m_shared.m_name).ToLowerInvariant() : item;
            return count > 1 ? count + " × " + name : name;
        }

        internal static string PayWords(Request r) { return Words(r.Reward, r.RewardCount); }

        // for the board, its talk: what it asks, for what, how long left
        internal static string Text(NpcActor a)
        {
            Request r = Of(a);
            if (r == null) return "";
            string left = NpcRescue.Left(r.Until - (long)NpcRescue.Now);
            return Plain(a, r) + " " + a.Form(Lang.F("Віддячу: {0}. Чекатиму ще {1}", PayWords(r), left)) + (left.EndsWith(".") ? "" : "."); // (an abbreviation's stop ends it: no «год..»)
        }

        internal static string Note(NpcActor a)
        {
            Request r = Of(a);
            return r == null ? null : Lang.F("{0} просить: {1} (віддячить: {2})", a.Name, Words(r.Item, r.Count), PayWords(r));
        }

        // ---------------------------------------------------------------- brought (on the game of the one who brings it)
        // how many of what it asks for you carry now (its scrap: «3 / 3»; -1: it asks for nothing)
        internal static int Carried(Player p, NpcActor a)
        {
            Request r = Of(a);
            if (r == null || p == null) return -1;
            ItemDrop d = DropOf(r.Item);
            return d != null ? p.GetInventory().CountItems(d.m_itemData.m_shared.m_name) : 0;
        }

        internal static bool CanGive(Player p, NpcActor a)
        {
            Request r = Of(a);
            if (r == null || p == null) return false;
            ItemDrop d = DropOf(r.Item);
            return d != null && p.GetInventory().CountItems(d.m_itemData.m_shared.m_name) >= r.Count;
        }

        internal static string LastPaid = ""; // (its words of thanks: the talk's {reward})

        internal static void Give(Player p, NpcActor a)
        {
            LastPaid = a != null ? a.Form(Lang.T("Ой… а де ж воно? Нема.")) : "";
            Request r = Of(a);
            if (r == null || !CanGive(p, a)) return;
            if (Given.Count > 200) Given.Clear();
            Given.Add(GivenKey(a, r));
            p.GetInventory().RemoveItem(DropOf(r.Item).m_itemData.m_shared.m_name, r.Count);
            Errands.Give(p, ObjectDB.instance.GetItemPrefab(r.Reward), r.RewardCount); // (stack by stack; what does not fit at your feet)
            string paid = Lang.F("Тримай: {0}.", Words(r.Reward, r.RewardCount));
            LastPaid = (NpcPhrases.Compose(a, "request_thanks", p) ?? Lang.T("Ой, дякую! Саме те, що треба.")) + " " + a.Form(paid);
            NpcMemory.Tell(a, "request_done", p.GetPlayerID(), Words(r.Item, r.Count)); // (remembered first: a promise of it looks for this as soon as the request is gone — NpcPromises)
            a.Act("requestdone " + r.Until.ToString(CultureInfo.InvariantCulture)); // (its owner takes it off — that very request)
            Npcs.Log(a.Id + ": got " + r.Count + " " + r.Item + ", paid " + r.RewardCount + " " + r.Reward);
            StoryBus.Raise("npc_request_done", a.Id, r.Count, a.transform.position);
        }

        // when its request now runs out (world s; 0: none)
        internal static long UntilOf(NpcActor a) { Request r = Parse(Kept(a)); return r != null ? r.Until : 0L; }

        // where it is owned: that request off (not another one asked since), and what you brought where it was for
        internal static void Done(NpcActor a, string until)
        {
            Request r = Parse(Kept(a));
            if (r == null || r.Until.ToString(CultureInfo.InvariantCulture) != until || !NpcLocker.Set(a, KRequest, "")) return;
            Deliver(a, r);
        }

        // for its work: to its chest as all it carries goes (NpcWorker.Receive) · for its gear: into its cells · for itself: used up. Its work or its bow gone meanwhile: at its feet, nothing vanishes
        private static void Deliver(NpcActor a, Request r)
        {
            if (r.To == Goes.Self) return;
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(r.Item) : null;
            ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (d == null) return;
            List<Brought> was = BroughtOf(a);
            was.RemoveAll(b => b.Name == d.m_itemData.m_shared.m_name);
            was.Add(new Brought { Name = d.m_itemData.m_shared.m_name, Left = r.Count, Until = (long)NpcRescue.Now + (long)(Day * BroughtDays) });
            KeepBrought(a, was);
            if (r.To == Goes.Chest && a.Worker != null && a.Worker.Works) { a.Worker.Receive(prefab, r.Count); return; }
            int left = r.Count;
            ItemDrop.ItemData it = d.m_itemData.Clone();
            it.m_dropPrefab = prefab;
            if (r.To == Goes.Cells && a.Gear != null && a.Gear.Cells != null)
            {
                int k = Mathf.Min(left, NpcWorker.Space(a.Gear.Cells, it));
                if (k > 0) { ItemDrop.ItemData part = it.Clone(); part.m_stack = k; if (a.Gear.Cells.AddItem(part)) left -= k; }
            }
            if (left > 0) ItemDrop.DropItem(it, left, a.transform.position + a.transform.forward * 0.8f + Vector3.up * 0.5f, Quaternion.identity);
        }

        // what you brought (KBrought «name|left|until;…»): each thing on its own, so two things taken back by turns are not asked for by turns; past its days a row goes
        private sealed class Brought { public string Name = ""; public int Left; public long Until; }
        private const int BroughtMost = 8;
        private static List<Brought> BroughtOf(NpcActor a)
        {
            List<Brought> l = new List<Brought>();
            long now = (long)NpcRescue.Now;
            foreach (string row in (NpcLocker.Get(a, KBrought) ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] w = row.Split('|');
                Brought b = new Brought();
                if (w.Length != 3 || !int.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out b.Left) || !long.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out b.Until)) continue;
                b.Name = w[0];
                if (b.Left > 0 && now < b.Until) l.Add(b);
            }
            return l;
        }
        private static void KeepBrought(NpcActor a, List<Brought> l)
        {
            while (l.Count > BroughtMost) l.RemoveAt(0); // (the oldest first)
            List<string> rows = new List<string>();
            foreach (Brought b in l) rows.Add(b.Name + "|" + b.Left.ToString(CultureInfo.InvariantCulture) + "|" + b.Until.ToString(CultureInfo.InvariantCulture));
            NpcLocker.Set(a, KBrought, string.Join(";", rows.ToArray()));
        }
        private static bool Unused(NpcActor a, string name) { foreach (Brought b in BroughtOf(a)) if (b.Name == name) return true; return false; }

        // it took this much of a thing out of its own chest for its work (NpcWorker, where it is run): what you brought counts down (nothing written unless it was one of those)
        internal static void Used(NpcActor a, string name, int took)
        {
            string s = a != null ? NpcLocker.Get(a, KBrought) : null;
            if (string.IsNullOrEmpty(s) || s.IndexOf(name, StringComparison.Ordinal) < 0) return;
            List<Brought> l = BroughtOf(a);
            Brought b = l.Find(x => x.Name == name);
            if (b == null) return;
            b.Left -= took;
            if (b.Left <= 0) l.Remove(b);
            KeepBrought(a, l);
        }
    }
}
#endif
