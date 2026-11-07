#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A resident's request (npc-plan.md, «прохання»): now and then (once in one and a half to three game days) it asks
    // for a thing — for its work (a stoker wood or resin, a smelter ore or coal, a cook meat, a guard arrows, a servant
    // resin for the lights) or, with no work, for itself (berries, honey, feathers) — only of the lands already reached.
    // On the heart's board and in its mouth when you are near. Brought (its talk: «Ось те, що ти просив»), it pays at once:
    //   work        what its work makes (bars, a cooked meal, mead, rare mushrooms, arrows…)
    //   coins       the game's coins
    //   valuable    amber, an amber pearl, a silver necklace, a ruby — better the further the world has gone
    //   rumour      (to come: a place worth going to — the hook is here, it pays nothing yet)
    // It waits three game days; two at most at once at a base. Kept as one short string in its locker (NpcLocker: not in
    // the walking person's ZDO, which is sent whole many times a second), written by its owner when it asks and when it
    // is done; what is taken and given happens on the game of the one who brings it.
    internal static class NpcRequests
    {
        internal static readonly int KRequest = "RavenWatch_npcRequest".GetStableHashCode(); // string "item|count|reward|rcount|until|why" ("" none) — in its locker
        internal static readonly int KAskNext = "RavenWatch_npcRequestNext".GetStableHashCode(); // long: world seconds of the next ask — in its locker (NpcLocker.Lives)
        private const float DaysMin = 1.5f, DaysMax = 3f, Lasts = 3f;
        private const int PerBase = 2;

        // ---------------------------------------------------------------- what may be asked: [item, count, the lands it needs (0 Meadows … 6 Ashlands), what it is for]
        private sealed class Ask { public string Item; public int Count, Tier; public string Why; public Ask(string i, int c, int t, string w) { Item = i; Count = c; Tier = t; Why = w; } }
        private static readonly Dictionary<string, Ask[]> ForJob = new Dictionary<string, Ask[]>
        {
            { "stoker", new[] { new Ask("Wood", 30, 0, "дрова кінчаються"), new Ask("Resin", 15, 0, "смолоскипам живиці бракує"), new Ask("Coal", 10, 1, "жаровням вугілля треба") } },
            { "smelter", new[] { new Ask("Coal", 20, 1, "піч голодна"), new Ask("CopperOre", 10, 1, "без руди піч стоїть"), new Ask("TinOre", 10, 1, "на бронзу олова треба"), new Ask("IronScrap", 10, 2, "залізо скінчилось"), new Ask("SilverOre", 10, 3, "хочу спробувати срібло") } },
            { "cook", new[] { new Ask("RawMeat", 6, 0, "м'яса на всіх не вистачає"), new Ask("DeerMeat", 5, 0, "на юшку"), new Ask("NeckTail", 6, 0, "на смаженину"), new Ask("FishRaw", 4, 1, "на суп"), new Ask("LoxMeat", 4, 4, "на свято") } },
            { "keeper", new[] { new Ask("Honey", 5, 0, "на закваску"), new Ask("Raspberry", 15, 0, "на медовуху"), new Ask("Blueberries", 15, 1, "на медовуху"), new Ask("Barley", 10, 4, "на пиво") } },
            { "gatherer", new[] { new Ask("Feathers", 10, 0, "на кошики"), new Ask("LeatherScraps", 10, 0, "на торби"), new Ask("FineWood", 10, 1, "на міцні кошики") } },
            { "cleaner", new[] { new Ask("Resin", 10, 0, "мітлу підклеїти"), new Ask("DeerHide", 4, 0, "на ганчірки") } },
            { "guard", new[] { new Ask("ArrowFlint", 30, 0, "на варті без них ніяк"), new Ask("ArrowBronze", 30, 1, "старі стріли скінчились"), new Ask("ArrowIron", 30, 2, "проти тих, що в броні"), new Ask("TrollHide", 3, 1, "на обладунок") } },
            { "lumber", new[] { new Ask("LeatherScraps", 10, 0, "рукавиці стерлись"), new Ask("Resin", 10, 0, "топорище підклеїти"), new Ask("TrollHide", 2, 1, "на теплу куртку") } },
            { "miner", new[] { new Ask("LeatherScraps", 10, 0, "на налокітники"), new Ask("Resin", 15, 0, "смолоскипи в шахту"), new Ask("TrollHide", 2, 1, "на теплу куртку") } },
            { "servant", new[] { new Ask("Resin", 20, 0, "світильникам живиці бракує"), new Ask("Wood", 20, 0, "дров у дім треба"), new Ask("Coal", 10, 1, "жаровням у домі вугілля") } },
            { "", new[] { new Ask("Raspberry", 10, 0, "давно не їв|їла солодкого"), new Ask("Honey", 3, 0, "на свято"), new Ask("Feathers", 5, 0, "на подушку"), new Ask("Blueberries", 10, 1, "люблю чорницю"), new Ask("Cloudberry", 6, 4, "сниться ночами") } },
        };

        // what its work makes, as pay: [item, count, the lands it needs]
        private static readonly Dictionary<string, Ask[]> Makes = new Dictionary<string, Ask[]>
        {
            { "stoker", new[] { new Ask("Coal", 40, 1, "") } },
            { "smelter", new[] { new Ask("Bronze", 10, 1, ""), new Ask("Iron", 10, 2, ""), new Ask("Silver", 8, 3, ""), new Ask("BlackMetal", 8, 4, "") } },
            { "cook", new[] { new Ask("CookedMeat", 10, 0, ""), new Ask("QueensJam", 6, 1, ""), new Ask("TurnipStew", 6, 2, ""), new Ask("Bread", 6, 4, "") } },
            { "keeper", new[] { new Ask("MeadHealthMinor", 4, 1, ""), new Ask("MeadStaminaMinor", 4, 1, ""), new Ask("MeadHealthMedium", 4, 3, "") } },
            { "gatherer", new[] { new Ask("MushroomYellow", 12, 1, ""), new Ask("Cloudberry", 20, 4, ""), new Ask("Thistle", 15, 1, "") } },
            { "guard", new[] { new Ask("ArrowFire", 50, 1, ""), new Ask("ArrowIron", 50, 2, ""), new Ask("ArrowSilver", 50, 3, "") } },
            { "servant", new[] { new Ask("Coal", 40, 1, "") } },
            { "lumber", new[] { new Ask("Wood", 50, 0, ""), new Ask("FineWood", 30, 1, ""), new Ask("RoundLog", 30, 2, "") } },
            { "miner", new[] { new Ask("Stone", 50, 0, ""), new Ask("CopperOre", 15, 1, ""), new Ask("IronScrap", 15, 2, ""), new Ask("SilverOre", 15, 3, "") } },
        };

        // reward kinds by weight; a hook for each (the rumour's: to come)
        internal static Func<NpcActor, Player, string> RumourHook; // (set it to give a place worth going to; null: no rumours yet)

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
        private static readonly Dictionary<NpcActor, float> NextLook = new Dictionary<NpcActor, float>();

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
                if (r == null || now > r.Until) { NpcLocker.Set(a, KRequest, ""); NpcLocker.Put(z, KAskNext, now + (long)(Day * DaysMin)); Npcs.Log(a.Id + ": its request ran out"); }
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

        // its ask in plain words (a job's goods have no «why»: none said)
        private static string Plain(NpcActor a, Request r)
        {
            return a.Form(r.Why.Length > 0 ? Lang.F("Мені б {0} — {1}.", Words(r.Item, r.Count), Lang.T(r.Why)) : Lang.F("Мені б {0}.", Words(r.Item, r.Count)));
        }

        // its ask as values for its made words: {item}, and {why} when it has one
        private static Dictionary<string, string> Values(Request r)
        {
            Dictionary<string, string> v = new Dictionary<string, string> { { "item", Words(r.Item, r.Count) } };
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
            foreach (Ask x in pool) if (x.Tier <= tier && Exists(x.Item)) fit.Add(x);
            if (fit.Count == 0) return null;
            Ask ask = fit[UnityEngine.Random.Range(0, fit.Count)];
            Request r = new Request { Item = ask.Item, Count = ask.Count, Why = ask.Why, Until = now + (long)(Day * Lasts) };
            Pay(r, job, tier);
            return r;
        }

        // what it pays: its work's make (when it has one), coins, or a valuable (the rumour: when it comes)
        private static void Pay(Request r, string job, int tier)
        {
            List<KeyValuePair<string, float>> kinds = new List<KeyValuePair<string, float>>();
            Ask[] makes;
            List<Ask> made = new List<Ask>();
            if (Makes.TryGetValue(job, out makes)) foreach (Ask m in makes) if (m.Tier <= tier && Exists(m.Item)) made.Add(m);
            if (made.Count > 0) kinds.Add(new KeyValuePair<string, float>("work", 2f));
            kinds.Add(new KeyValuePair<string, float>("coins", 1.5f));
            kinds.Add(new KeyValuePair<string, float>("valuable", 1f));
            if (RumourHook != null) kinds.Add(new KeyValuePair<string, float>("rumour", 0.6f));
            float sum = 0f; foreach (KeyValuePair<string, float> k in kinds) sum += k.Value;
            float roll = UnityEngine.Random.value * sum;
            string kind = "coins";
            foreach (KeyValuePair<string, float> k in kinds) { roll -= k.Value; if (roll <= 0f) { kind = k.Key; break; } }
            switch (kind)
            {
                case "work": { Ask m = made[made.Count - 1 - UnityEngine.Random.Range(0, Mathf.Min(2, made.Count))]; r.Reward = m.Item; r.RewardCount = Mathf.RoundToInt(m.Count * UnityEngine.Random.Range(1f, 1.5f)); break; } // (one of its best two, and a little over)
                case "valuable":
                {
                    string v = tier >= 5 && Exists("Ruby") ? "Ruby" : tier >= 3 && Exists("SilverNecklace") ? "SilverNecklace" : tier >= 1 && Exists("AmberPearl") ? "AmberPearl" : "Amber";
                    r.Reward = v; r.RewardCount = v == "Amber" ? 2 : 1;
                    break;
                }
                case "rumour": r.Reward = "rumour"; r.RewardCount = 1; break;
                default: r.Reward = "Coins"; r.RewardCount = 15 + tier * 12 + UnityEngine.Random.Range(0, 10); break;
            }
        }

        private static bool Exists(string item) { return ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(item) != null; }

        // ---------------------------------------------------------------- a request
        internal sealed class Request
        {
            public string Item = "", Reward = "Coins", Why = ""; public int Count, RewardCount; public long Until;
            public string Code() { return Item + "|" + Count + "|" + Reward + "|" + RewardCount + "|" + Until + "|" + Why.Replace("|", "¦"); }
        }

        internal static Request Parse(string s)
        {
            string[] f = (s ?? "").Split('|');
            if (f.Length < 5) return null;
            Request r = new Request { Item = f[0], Count = Errands.I(f[1], 1), Reward = f[2], RewardCount = Errands.I(f[3], 1), Why = f.Length > 5 ? f[5].Replace("¦", "|") : "" };
            long u; long.TryParse(f[4], out u); r.Until = u;
            return r;
        }

        // its request as kept (null: its locker not here yet; "" none)
        private static string Kept(NpcActor a) { return a != null ? NpcLocker.Get(a, KRequest) : null; }

        internal static Request Of(NpcActor a)
        {
            Request r = Parse(Kept(a));
            return r != null && r.Until >= (long)NpcRescue.Now && !Given.Contains(a.Id + "@" + r.Until) ? r : null;
        }
        private static readonly HashSet<string> Given = new HashSet<string>(); // (this game's: «id@until» brought)

        // «20 смоли» (the game's own name of a thing, in your language)
        internal static string Words(string item, int count)
        {
            GameObject p = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item) : null;
            ItemDrop d = p != null ? p.GetComponent<ItemDrop>() : null;
            string name = d != null && Localization.instance != null ? Localization.instance.Localize(d.m_itemData.m_shared.m_name).ToLowerInvariant() : item;
            return count > 1 ? count + " × " + name : name;
        }

        internal static string PayWords(Request r) { return r.Reward == "rumour" ? Lang.T("одна чутка") : Words(r.Reward, r.RewardCount); }

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
            GameObject pf = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(r.Item) : null;
            ItemDrop d = pf != null ? pf.GetComponent<ItemDrop>() : null;
            return d != null ? p.GetInventory().CountItems(d.m_itemData.m_shared.m_name) : 0;
        }

        internal static bool CanGive(Player p, NpcActor a)
        {
            Request r = Of(a);
            if (r == null || p == null) return false;
            GameObject pf = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(r.Item) : null;
            ItemDrop d = pf != null ? pf.GetComponent<ItemDrop>() : null;
            return d != null && p.GetInventory().CountItems(d.m_itemData.m_shared.m_name) >= r.Count;
        }

        internal static string LastPaid = ""; // (its words of thanks: the talk's {reward})

        internal static void Give(Player p, NpcActor a)
        {
            LastPaid = a != null ? a.Form(Lang.T("Ой… а де ж воно? Нема.")) : "";
            Request r = Of(a);
            if (r == null || !CanGive(p, a)) return;
            if (Given.Count > 200) Given.Clear();
            Given.Add(a.Id + "@" + r.Until);
            GameObject pf = ObjectDB.instance.GetItemPrefab(r.Item);
            p.GetInventory().RemoveItem(pf.GetComponent<ItemDrop>().m_itemData.m_shared.m_name, r.Count);
            string paid;
            if (r.Reward == "rumour") paid = RumourHook != null ? (RumourHook(a, p) ?? Lang.T("…ні, забув|забула. Згадаю — скажу.")) : Lang.T("…ні, забув|забула. Згадаю — скажу.");
            else
            {
                Errands.Give(p, ObjectDB.instance.GetItemPrefab(r.Reward), r.RewardCount); // (stack by stack; what does not fit at your feet)
                paid = Lang.F("Тримай: {0}.", Words(r.Reward, r.RewardCount));
            }
            LastPaid = (NpcPhrases.Compose(a, "request_thanks", p) ?? Lang.T("Ой, дякую! Саме те, що треба.")) + " " + a.Form(paid);
            NpcMemory.Tell(a, "request_done", p.GetPlayerID(), Words(r.Item, r.Count)); // (remembered first: a promise of it looks for this as soon as the request is gone — NpcPromises)
            a.Act("requestdone " + r.Until.ToString(CultureInfo.InvariantCulture)); // (its owner takes it off — that very request)
            Npcs.Log(a.Id + ": got " + r.Count + " " + r.Item + ", paid " + r.RewardCount + " " + r.Reward);
            StoryBus.Raise("npc_request_done", a.Id, r.Count, a.transform.position);
        }

        // when its request now runs out (world s; 0: none)
        internal static long UntilOf(NpcActor a) { Request r = Parse(Kept(a)); return r != null ? r.Until : 0L; }

        // where it is owned: that request off (not another one asked since)
        internal static void Done(NpcActor a, string until)
        {
            Request r = Parse(Kept(a));
            if (r != null && r.Until.ToString(CultureInfo.InvariantCulture) == until) NpcLocker.Set(a, KRequest, "");
        }
    }
}
#endif
