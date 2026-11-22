#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Прохання ворона» (the user, 2026-10-22): a small wish of the raven itself, apart from Odin's errands — 5 to 30
    // minutes, after its own heart. One at a time for both ravens: every EveryMin–EveryMax game days one of them, at
    // random, asks in a calm moment (the director's gate): its words in a speech card, «Мунін просить: …» in the corner.
    // Nothing to accept: it simply waits, and quietly passes after Lasts game days (Munin may grumble). A reminder at most
    // once a game day. The book shows a wax seal beside the raven's name (RavenBookRequest).
    //   want    — feed it this (now and then its favourite: a hint)
    //   show    — take it somewhere: a land you know, a mountain top, the open sea, a dungeon
    //   revenge — (Munin) kill the kind that lately hurt or killed you
    //   hunt    — (Munin) N of a kind while he is on you (or by his own blow); now and then by night, or only by his blow
    //   fight   — (Munin) a beast with stars killed by you while he watches from the shoulder
    //   ride    — (Munin) a few minutes riding a tamed beast with him on the shoulder (once you have ridden in this world)
    //   sit     — (Hugin) a few minutes by a fire, or on a perch
    //   sail    — (Hugin) a few minutes on a ship under way with him on the shoulder
    //   dress   — put on it a look it has open and is not wearing, and keep it on a few minutes
    //   feathers — give it feathers for the nest (the card's «Дати»)
    //   shiny   — give it amber, a ruby, a necklace, later lands' treasures or a heap of coins (the card's «Дати»): its hoard grows
    //   cache   — its cache 150–300 m away, never near a base: a ring on the map, it caws faster the nearer you are (a
    //             finder), on the spot it flies down onto it; 3 blows of a pickaxe there and its small chest comes up
    //   meet    — come up to a player whose raven is its friend or its enemy
    //   tub     — bathe it in the hot tub with you: only a raven dirty a long while (Bath.DirtyMinutes), once you have a tub
    // Paid in trust (mostly), a little satiety, and sparklets («іскринки»: 1, the harder ones 2): SparksForRune of them
    // light a rune of your own choosing (the book: «Запалити руну») — Odin's errands give a spark to a random one.
    // NickAt done: the nickname «Повірник» (Nicks). Checked only on what the mod hears anyway (ate, killed, a
    // diary event) and, for what is about a place, every 2 s while such a wish waits; the cache's distance twice a second
    // and only while there is one. Everything lives in the raven (customData); only the next day and who last hurt you
    // (for the revenge) are the character's. A raven that is not yours (Trust.Stranger) neither asks nor moves its owner's
    // wish on: only the owner can do it.
    internal static class Requests
    {
        internal static int EveryMin = 4, EveryMax = 5;     // game days between two wishes (both ravens together)
        internal static int Lasts = 3;                      // game days a wish waits
        internal const int SparksForRune = 5;
        internal static int NickAt = 25;
        internal static float CacheMin = 150f, CacheMax = 300f; // m from you its cache is laid
        internal static float CacheRing = 30f;              // m: the ring on the map (the cache somewhere in it)
        internal static float CacheNear = 60f;              // m: from here it says warmer / colder
        internal static float CawFrom = 100f;               // m: from here it caws (the whole ring and a bit round it)
        internal static int DigHits = 3;                    // blows of a pickaxe on the spot
        internal static float PeakY = 160f;                 // m: «a mountain top»
        internal static float TrustPerSpark = 12f;          // trust for a wish done, per sparklet it gives
        internal static float SatietyGift = 8f;
        internal static float TubDirtyMin = 30f;            // min dirty (from 50) on end before it may ask for the hot tub
        internal static float CoinsChance = 0.3f;           // «Дай блискуче» from the Mountains on: a heap of coins instead of a thing
        internal const string FeatherPrefab = "Feathers";
        // «Дай блискуче»: its things (their lands: Satiety.Lands); the ancient ones of the latest lands are rarer by Fresh
        private static readonly string[] Shinies = { "Amber", "Amber", "AmberPearl", "Ruby", "SilverNecklace", "AncientCoin", "AncientGemstoneBlack", "AncientGemstoneGreen", "AncientGemstoneOrange", "AncientGemstonePurple" };

        internal const string Key = "RavenWatch_req", SaidKey = "RavenWatch_reqSaid", SparksKey = "RavenWatch_reqSparks", HoardKey = "RavenWatch_reqHoard";
        private const string NextKey = "RavenWatch_reqNext", FoeKey = "RavenWatch_reqFoe", RodeKey = "RavenWatch_reqRode"; // (rode: the world you rode a beast in)
        internal const string CachePrefab = "RavenWatch_Cache";

        // ---------------------------------------------------------------- one wish

        internal sealed class Req
        {
            public string Type = "", Arg = "";
            public int Need = 1, Have, Day, Reward = 1, Dug, Reminded = -1;
            public bool Done, Verified, Shown;     // Done: kept (the seal gold) until its card is opened
            public string World = "";
            public float X, Z, Cx, Cz;             // the place (cache, show: where), the ring's middle
            public bool HasPlace;
            public string Mode = "";               // how («Полюємо»: "" any, "night", "raven" — by his own blow)

            public string Code()
            {
                return string.Join("|", new string[] { Type, Arg.Replace("|", "/"), Need.ToString(CultureInfo.InvariantCulture), Have.ToString(CultureInfo.InvariantCulture),
                    Day.ToString(CultureInfo.InvariantCulture), Reward.ToString(CultureInfo.InvariantCulture), Dug.ToString(CultureInfo.InvariantCulture),
                    Reminded.ToString(CultureInfo.InvariantCulture), Done ? "1" : "0", Verified ? "1" : "0", Shown ? "1" : "0", World,
                    HasPlace ? "1" : "0", N(X), N(Z), N(Cx), N(Cz), Mode.Replace("|", "/") });
            }

            public static Req Parse(string raw)
            {
                if (string.IsNullOrEmpty(raw)) return null;
                string[] a = raw.Split('|');
                if (a.Length < 17) return null;
                Req r = new Req();
                r.Type = a[0]; r.Arg = a[1];
                r.Need = Errands.I(a[2], 1); r.Have = Errands.I(a[3], 0); r.Day = Errands.I(a[4], 0); r.Reward = Errands.I(a[5], 1);
                r.Dug = Errands.I(a[6], 0); r.Reminded = Errands.I(a[7], -1);
                r.Done = a[8] == "1"; r.Verified = a[9] == "1"; r.Shown = a[10] == "1"; r.World = a[11];
                r.HasPlace = a[12] == "1"; r.X = Errands.F(a[13], 0f); r.Z = Errands.F(a[14], 0f); r.Cx = Errands.F(a[15], 0f); r.Cz = Errands.F(a[16], 0f);
                r.Mode = a.Length > 17 ? a[17] : ""; // (a wish saved before there were ways)
                return r;
            }

            private static string N(float v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }
            public Vector3 Place { get { return new Vector3(X, 0f, Z); } }
        }

        internal static Req Of(ItemDrop.ItemData item)
        {
            string raw;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(Key, out raw) ? Req.Parse(raw) : null;
        }

        private static void Save(ItemDrop.ItemData item, Req r)
        {
            if (item == null) return;
            if (r == null) { item.m_customData.Remove(Key); item.m_customData.Remove(SaidKey); } else item.m_customData[Key] = r.Code();
        }

        internal static int Sparks(ItemDrop.ItemData item) { string v; return item != null && item.m_customData.TryGetValue(SparksKey, out v) ? Errands.I(v, 0) : 0; }
        private static void SetSparks(ItemDrop.ItemData item, int n) { if (item != null) { item.m_customData[SparksKey] = Mathf.Max(0, n).ToString(CultureInfo.InvariantCulture); } }
        // spend n sparklets (a word's price: Words); false when there are not enough (a negative n adds: the console)
        internal static bool SpendSparks(ItemDrop.ItemData item, int n)
        {
            if (item == null) return false;
            int have = Sparks(item);
            if (n > have) return false;
            if (n != 0) SetSparks(item, have - n);
            return true;
        }
        internal static int Hoard(ItemDrop.ItemData item) { string v; return item != null && item.m_customData.TryGetValue(HoardKey, out v) ? Errands.I(v, 0) : 0; }
        internal static int DoneCount(ItemDrop.ItemData item) { return Nicks.Count(item, "reqs"); }

        // your own raven of that side, awake (a stranger's raven: its wish is its owner's, not yours to do)
        private static ItemDrop.ItemData Mine(Player p, bool munin) { ItemDrop.ItemData it = Trust.Worn(munin); return it != null && p != null && Trust.Active(it) && !Trust.Stranger(it, p) ? it : null; }

        // a wish of this world, still waiting (not yet done)
        internal static bool Waiting(Req r) { return r != null && !r.Done && r.World == World; }

        private static string World { get { return ZNet.instance != null && ZNet.World != null ? ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture) : ""; } }
        private static int Day { get { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; } }
        internal static int DaysLeft(Req r) { return r == null ? 0 : Mathf.Max(0, r.Day + Lasts - Day); }

        // ---------------------------------------------------------------- the words

        // what it wants, short: the corner, the card's row
        internal static string Goal(Req r)
        {
            if (r == null) return "";
            switch (r.Type)
            {
                case "want": return Lang.F("Хочу: {0}", ItemName(r.Arg));
                case "show": return Lang.F("Покажи мені: {0}", ShowName(r.Arg, false));
                case "revenge": return Lang.F("Помстися: {0}", Foe(r.Arg));
                case "hunt": return Lang.F("Полюємо: {0}", Foe(r.Arg)) + HowWord(r);
                case "fight": return Lang.F("Хочу бачити бій: звір із {0} при мені", Stars(r.Need));
                case "ride": return Lang.T("Покатай мене верхи на приборканому звірі");
                case "sit": return Lang.T("Посидь зі мною біля вогню чи на жердині");
                case "sail": return Lang.T("Поплаваймо: корабель під вітрилом, я на плечі");
                case "dress": return Lang.F("Прикрась мене: {0}", LookName(r.Arg));
                case "feathers": return Lang.F("Принеси пір'я для гнізда: {0}", ItemName(r.Arg));
                case "shiny": return Lang.F("Дай блискуче: {0}", ShinyName(r));
                case "cache": return Lang.T("Знайди мою схованку");
                case "meet": return Lang.T("Познайом мене з вороном-другом чи ворогом");
                case "teach": return Lang.T("Навчи мене слова");
                case "tub": return Lang.T("Скупай мене в гарячій купелі");
            }
            return r.Type;
        }

        // what it asks, for «Мунін просить: …» in the corner
        internal static string Asks(Req r)
        {
            if (r == null) return "";
            switch (r.Type)
            {
                case "want": return Lang.F("дати поїсти — {0}", ItemName(r.Arg));
                case "show": return Lang.F("показати {0}", ShowName(r.Arg, true));
                case "revenge": return Lang.F("помститися — {0}", Foe(r.Arg));
                case "hunt": return Lang.F("полювання — {0} ×{1}", Foe(r.Arg), r.Need) + HowWord(r);
                case "fight": return Lang.F("бій при ньому — звір із {0}", Stars(r.Need));
                case "ride": return Lang.T("покатати його верхи");
                case "sit": return Lang.T("посидіти з ним біля вогню чи на жердині");
                case "sail": return Lang.T("поплавати з ним під вітрилом");
                case "dress": return Lang.F("прикрасити його — {0}", LookName(r.Arg));
                case "feathers": return Lang.F("пір'я для гнізда — {0} ×{1}", ItemName(r.Arg), r.Need);
                case "shiny": return Lang.F("дати блискуче — {0}", ShinyName(r));
                case "cache": return Lang.T("знайти його схованку");
                case "meet": return Lang.T("познайомити його з вороном-другом чи ворогом");
                case "teach": return Lang.T("навчити його слова (книга воронів)");
                case "tub": return Lang.T("скупати його в гарячій купелі");
            }
            return r.Type;
        }

        // how far along, for the card («0 / 1», «2 хв / 4 хв»); "" when there is nothing to count
        internal static string Progress(Req r)
        {
            if (r == null) return "";
            if (r.Type == "sit" || r.Type == "sail" || r.Type == "ride" || r.Type == "dress") return Lang.F("{0} / {1} хв", r.Have / 60, r.Need / 60);
            if (r.Type == "cache") return r.Dug > 0 ? Lang.F("копаєш: {0} / {1}", r.Dug, DigHits) : "";
            if (r.Type == "show" || r.Type == "meet" || r.Type == "fight" || r.Type == "teach" || r.Type == "tub") return "";
            return r.Have + " / " + r.Need;
        }

        // «Полюємо»'s way, after the goal ("" when any)
        private static string HowWord(Req r)
        {
            if (r.Mode == "night") return Lang.T(" (уночі)");
            if (r.Mode == "raven") return Lang.T(" (його ударом)");
            return "";
        }

        private static string Stars(int n) { return n >= 2 ? Lang.T("двома зірками") : Lang.T("зіркою"); }

        // a look of the book's wardrobe by its prefab
        private static string LookName(string prefab)
        {
            Outfits.Entry e = Outfits.Find(prefab);
            return e != null ? e.Name : prefab;
        }

        // «Дай блискуче»: a thing, or a heap of coins
        private static string ShinyName(Req r) { return r.Need > 1 ? r.Need + " × " + ItemName(r.Arg) : ItemName(r.Arg); }

        // acc: as in «показати …» (only the mountain top changes)
        private static string ShowName(string arg, bool acc)
        {
            if (arg == "peak") return acc ? Lang.T("гірську вершину") : Lang.T("гірська вершина");
            if (arg == "sea") return Lang.T("відкрите море");
            if (arg == "dungeon") return Lang.T("підземелля");
            Heightmap.Biome b;
            return TryBiome(arg, out b) ? Composer.BiomeName(b, false) : arg;
        }

        private static bool TryBiome(string arg, out Heightmap.Biome b)
        {
            b = Heightmap.Biome.None;
            if (arg == null || !arg.StartsWith("biome:", StringComparison.Ordinal)) return false;
            try { b = (Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), arg.Substring(6)); return true; } catch { return false; }
        }

        internal static string ItemName(string prefab)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            return d != null && Localization.instance != null ? Localization.instance.Localize(d.m_itemData.m_shared.m_name) : prefab;
        }

        private static string Foe(string token) { return Localization.instance != null && !string.IsNullOrEmpty(token) ? Localization.instance.Localize(token.StartsWith("$", StringComparison.Ordinal) ? token : "$" + token) : token; } // (the diary's tokens come without the $)

        // a line from the phrase files with these values, else one of the mod's own
        private static string Say(Player p, bool munin, string tag, string fallback, params string[] values) { return SayFoe(p, munin, tag, fallback, null, values); }

        // the same with a creature in its cases ({foe} {foe_gen} {foe_ins} {foe_genpl})
        private static string SayFoe(Player p, bool munin, string tag, string fallback, string foe, params string[] values)
        {
            if (Plugin.Instance == null) return fallback;
            Composer.Extra.Clear();
            try
            {
                for (int i = 0; i + 1 < values.Length; i += 2) Composer.Extra[values[i]] = values[i + 1];
                if (foe != null) Composer.ExtraCreature("foe", foe);
                string made = Plugin.Instance.ComposeTagged(p, munin, tag);
                return !string.IsNullOrEmpty(made) ? made : fallback;
            }
            finally { Composer.Extra.Clear(); }
        }

        private static void Speak(Player p, bool munin, string line, bool director)
        {
            if (p == null || string.IsNullOrEmpty(line) || !Shoulder.Present(p, munin)) return;
            RavenSkills.SayFromShoulder(p, munin, line);
            if (director) Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line));
        }

        // ---------------------------------------------------------------- the clock (Errands.Tick, twice a second)

        private static float _slow;

        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || EnvMan.instance == null || ZNet.instance == null) return;
            Pins(p);
            if (p.IsDead() || !RavenBook.Given(p)) return; // (no book yet: the raven only sits, it asks for nothing — RavenBook.Given)
            if (_spawn != null) SpawnCache(p);
            bool slow = Time.time >= _slow;
            if (slow) _slow = Time.time + 2f;
            bool any = false;
            foreach (bool munin in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Mine(p, munin);
                Req r = Of(item);
                if (!Waiting(r)) continue;
                any = true;
                if (Day >= r.Day + Lasts) { Expire(p, munin, item, r); continue; }
                if (slow && Beyond(p, r, munin)) { Save(item, null); Log(Plugin.RavenName(munin) + "'s wish dropped (two lands beyond the world): " + r.Arg); RavenBook.Refresh(); continue; }
                if (r.Type == "cache") CacheTick(p, munin, item, r);
                else if (slow) PlaceTick(p, munin, item, r);
                if (slow) Remind(p, munin, item, r);
            }
            if (!any && slow && !WaitsInBag(p)) MaybeGive(p);
        }

        // a raven in the bag (not worn) still waiting with its wish: one at a time for both
        private static bool WaitsInBag(Player p)
        {
            bool m;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
                if (RavenItem.IsRaven(it, out m) && !it.m_equipped && !Trust.Stranger(it, p) && Waiting(Of(it))) return true;
            return false;
        }

        // a wish for a thing two lands or more beyond the one the world opens (one asked before this rule)
        private static bool Beyond(Player p, Req r, bool munin)
        {
            return LandOf(r, munin) > Stage.Land(p) + 1;
        }

        // the land a wish is about (0: none — a place, a fire, a player)
        private static int LandOf(Req r, bool munin)
        {
            if (r == null) return 0;
            if (r.Type == "want")
            {
                Trust.Food f = Trust.Foods(munin).Find(x => x.Prefab == r.Arg); // (a food added in the config: by its class)
                return Satiety.LandOf(r.Arg, f != null ? f.Class : 0);
            }
            if (r.Type == "shiny") return Satiety.LandOf(r.Arg, 0);
            Heightmap.Biome b;
            if (r.Type == "show" && TryBiome(r.Arg, out b)) return Stage.LandOf(b);
            if (r.Type == "hunt") foreach (KeyValuePair<Heightmap.Biome, string[]> kv in HuntKinds) if (Array.IndexOf(kv.Value, r.Arg) >= 0) return Stage.LandOf(kv.Key);
            return 0;
        }

        // one of the land beyond the one the world opens: this rarely, for a spark more, and the raven says it knows
        internal static float AheadChance = 0.15f;
        private static bool FromAfar(Player p, Req r, bool munin) { return LandOf(r, munin) > Stage.Land(p); }

        private static string AfarLine(bool munin)
        {
            string[] l = munin
                ? new[] { Lang.T("Так, це далеко. Що, страшно?"), Lang.T("Знаю, не з наших країв. Тим цікавіше."), Lang.T("Далеко? То й ліпше — дорогою буде кого дзьобнути.") }
                : new[] { Lang.T("Знаю, це далеко звідси. Але ж ти дійдеш."), Lang.T("Це з дальшої землі. Тому й прошу саме тебе."), Lang.T("Далеко, так. Зате буде що згадати.") };
            return l[UnityEngine.Random.Range(0, l.Length)];
        }

        // by the raven's trust: the land you open and the one before it more, the old ones less (Stage.Fresh)
        private static T Fresh<T>(Player p, ItemDrop.ItemData item, List<T> from, Func<T, int> land)
        {
            float sum = 0f;
            int now = Stage.Land(p), level = Trust.Level(item);
            foreach (T x in from) sum += Stage.Fresh(now - land(x), level, false);
            float at = UnityEngine.Random.value * sum;
            foreach (T x in from) { at -= Stage.Fresh(now - land(x), level, false); if (at <= 0f) return x; }
            return from[from.Count - 1];
        }

        private static bool Calm(Player p)
        {
            return !p.IsSleeping() && !Senses.Fight && !p.InInterior() && !RavenSpeech.Has(p.gameObject) && Director.May(Director.Voice.Shoulder);
        }

        // a raven of yours, awake, on you now, that may ask
        private static bool CanAsk(Player p, bool munin, out ItemDrop.ItemData item)
        {
            item = Mine(p, munin);
            return item != null && !Trust.Away(item) && Shoulder.Present(p, munin);
        }

        private static void MaybeGive(Player p)
        {
            string raw;
            int next;
            string key = World;
            if (!p.m_customData.TryGetValue(NextKey, out raw) || !raw.StartsWith(key + ":", StringComparison.Ordinal))
            {
                p.m_customData[NextKey] = key + ":" + (Day + UnityEngine.Random.Range(1, 3)); // (the first one soon)
                return;
            }
            next = Errands.I(raw.Substring(key.Length + 1), 0);
            if (Day < next || !Calm(p)) return;
            List<bool> who = new List<bool>();
            ItemDrop.ItemData it;
            foreach (bool m in new bool[] { false, true }) if (CanAsk(p, m, out it)) who.Add(m);
            if (who.Count == 0) return;
            bool munin = who[UnityEngine.Random.Range(0, who.Count)];
            bool given = Give(p, munin, null) == null;
            int sooner = Trust.Level(Mine(p, munin)) >= 5 ? 1 : 0; // (a raven that trusts you asks a day sooner)
            int days = given ? Mathf.Max(2, UnityEngine.Random.Range(EveryMin, EveryMax + 1) - sooner + DaysOf(Mine(p, munin))) : 1; // (its character: DaysOf; nothing fitted: tomorrow again)
            p.m_customData[NextKey] = key + ":" + (Day + days);
        }

        // ---------------------------------------------------------------- a new wish

        // null when given, else why not (type: the test command's choice, else by the raven's heart)
        internal static string Give(Player p, bool munin, string type)
        {
            ItemDrop.ItemData item;
            if (!CanAsk(p, munin, out item)) return Lang.F("{0} зараз не може просити (не на плечі, спить чи чужий).", Plugin.RavenName(munin));
            Req r = null;
            if (type != null) r = Make(p, munin, item, type);
            else
            {
                List<KeyValuePair<string, float>> w = Weights(p, munin, item);
                for (int tries = 0; tries < 6 && r == null && w.Count > 0; tries++)
                {
                    float sum = 0f;
                    foreach (KeyValuePair<string, float> kv in w) sum += kv.Value;
                    float roll = UnityEngine.Random.value * sum;
                    int i = 0;
                    for (; i < w.Count - 1; i++) { roll -= w[i].Value; if (roll <= 0f) break; }
                    r = Make(p, munin, item, w[i].Key);
                    if (r == null) w.RemoveAt(i);
                }
            }
            if (r == null) return Lang.T("Не знайшлося прохання, яке можна виконати тут і зараз.");
            r.Day = Day; r.World = World;
            Save(item, r);
            Remember(item, r.Type);
            string line = Asked(p, munin, r);
            if (!string.IsNullOrEmpty(line) && FromAfar(p, r, munin)) line += " " + AfarLine(munin);
            if (!string.IsNullOrEmpty(line)) item.m_customData[SaidKey] = line; // (the card shows it)
            Speak(p, munin, line, true);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} просить: {1}", Plugin.RavenName(munin), Asks(r)));
            Log(Plugin.RavenName(munin) + " asks: " + r.Code());
            return null;
        }

        // what may come, how often: by who it is, a little by its heart (its quirk and knack)
        private static List<KeyValuePair<string, float>> Weights(Player p, bool munin, ItemDrop.ItemData item)
        {
            List<KeyValuePair<string, float>> w = new List<KeyValuePair<string, float>>();
            Norov.Character c = Norov.CharOf(item);
            string ch = c != null ? c.Id : "";
            Norov.Quirk q = Norov.QuirkOf(item);
            string qu = q != null ? q.Id : "";
            float shows = Norov.Shows(Trust.Level(item)); // (its character shows more as it trusts you; a quirk always)
            string last;
            string[] recent = item.m_customData.TryGetValue(LastKey, out last) ? last.Split(',') : new string[0];
            Action<string, float> add = (type, at) => w.Add(new KeyValuePair<string, float>(type, at * Norov.Mul(Lean(ch, type), shows) * Habit(qu, type) * Again(recent, type)));
            add("want", 1.2f);
            add("show", 1f);
            add("shiny", 0.9f);
            add("cache", 1f);
            add("meet", 0.6f);
            add("dress", 0.8f);
            add("feathers", 0.7f);
            add("teach", 0.9f); // (only from trust 3, with a free place for a word: Words)
            add("tub", 2.5f);   // (only dirty a long while, a tub known: seldom there, wanted then)
            if (munin) { add("revenge", 1.4f); add("hunt", 1.1f); add("fight", 1f); add("ride", 0.8f); }
            else { add("sit", 1.1f); add("sail", 0.9f); }
            return w;
        }

        // what a character asks for more, or less (Norov.Characters). Each one leans on wishes both ravens have, so it
        // shows in Hugin and in Munin alike (the user, 2026-10-27: stronger than before, its favourite ~a quarter of all)
        private static float Lean(string character, string type)
        {
            switch (character + "/" + type)
            {
                case "homebody/sit": return 2f;
                case "homebody/tub": case "calm/tub": return 1.5f;
                case "bold/tub": return 0.5f;
                case "homebody/dress": case "homebody/feathers": return 1.6f;
                case "homebody/want": return 1.3f;
                case "homebody/show": case "homebody/sail": return 0.5f;
                case "homebody/ride": return 0.6f;
                case "wanderer/show": return 2.2f;
                case "wanderer/sail": return 2f;
                case "wanderer/cache": return 1.6f;
                case "wanderer/sit": return 0.5f;
                case "fidget/ride": return 2f;
                case "fidget/show": case "fidget/cache": return 1.6f;
                case "fidget/hunt": case "fidget/fight": case "fidget/sail": return 1.4f;
                case "fidget/sit": return 0.4f;
                case "calm/sit": return 1.6f;
                case "calm/want": case "calm/feathers": return 1.4f;
                case "calm/teach": return 1.3f;
                case "calm/fight": return 0.6f;
                case "calm/ride": return 0.7f;
                case "watchful/cache": return 1.6f;
                case "watchful/show": case "watchful/revenge": return 1.4f;
                case "bold/fight": return 2.2f;
                case "bold/revenge": return 1.8f;
                case "bold/sail": return 1.6f;
                case "bold/show": case "bold/hunt": case "bold/dress": return 1.4f;
                case "devoted/dress": return 1.8f;
                case "devoted/meet": case "devoted/teach": return 1.6f;
                case "devoted/want": case "devoted/feathers": return 1.4f;
                case "curious/show": return 2f;
                case "curious/cache": return 1.8f;
                case "curious/shiny": return 1.4f;
                case "curious/sit": return 0.6f;
                case "proud/dress": return 2.2f;
                case "proud/tub": return 2f;
                case "proud/feathers": return 1.4f;
                case "proud/fight": return 0.6f;
                case "proud/hunt": return 0.7f;
                case "tender/meet": return 1.8f;
                case "tender/sit": return 1.6f;
                case "tender/teach": return 1.4f;
                case "tender/want": return 1.3f;
                case "tender/fight": case "tender/revenge": return 0.6f;
                case "grumpy/revenge": return 1.6f;
                case "grumpy/sit": case "grumpy/want": return 1.3f;
                case "grumpy/meet": return 0.5f;
                case "grumpy/dress": return 0.6f;
                case "grumpy/show": return 0.7f;
                case "huntsman/hunt": return 2.2f;
                case "huntsman/fight": return 1.6f;
                case "huntsman/revenge": return 1.4f;
                case "huntsman/ride": return 1.3f;
                case "huntsman/sit": case "huntsman/dress": return 0.6f;
                case "sweettooth/want": return 2f;
                case "sweettooth/sit": return 1.2f;
                case "sweettooth/shiny": return 0.7f;
                case "sweettooth/fight": return 0.8f;
            }
            return 1f;
        }

        // what a quirk asks for more, or less (Norov.QuirkList)
        private static float Habit(string quirk, string type)
        {
            switch (quirk + "/" + type)
            {
                case "picky/want": return 1.33f;
                case "sleepy/sit": return 1.36f;
                case "magpie/shiny": return 1.8f;
                case "thief/shiny": return 1.4f;
                case "thief/cache": return 1.3f;
                case "showoff/fight": case "showoff/dress": return 1.4f;
                case "timid/fight": return 0.4f;
                case "timid/revenge": return 0.7f;
                case "chilly/sail": return 0.6f;
                case "chilly/sit": return 1.4f; // (by the fire)
                case "wet/sail": return 1.3f;
                case "jealous/meet": return 1.3f;
                case "loud/teach": return 1.8f;
                case "grumbler/want": return 1.3f;
                case "willful/cache": return 1.3f;
                case "willful/dress": return 0.6f;
            }
            return 1f;
        }

        // what it asked lately (newest first): the last one rarely again, the one before less often
        private const string LastKey = "RavenWatch_reqLast";
        private static float Again(string[] recent, string type)
        {
            if (recent.Length > 0 && recent[0] == type) return 0.2f;
            if (recent.Length > 1 && recent[1] == type) return 0.5f;
            return 1f;
        }

        private static void Remember(ItemDrop.ItemData item, string type)
        {
            string last;
            string[] was = item.m_customData.TryGetValue(LastKey, out last) ? last.Split(',') : new string[0];
            if (was.Length > 0 && was[0] == type) return; // (the same again: the one before it stays remembered)
            item.m_customData[LastKey] = was.Length > 0 && was[0].Length > 0 ? type + "," + was[0] : type;
        }

        // «Відданий» and «Лагідний» ask a day sooner, «Непосида» a day later (both ravens' wishes share the days)
        private static int DaysOf(ItemDrop.ItemData item)
        {
            Norov.Character c = Norov.CharOf(item);
            return c == null ? 0 : c.Id == "devoted" || c.Id == "tender" ? -1 : c.Id == "fidget" ? 1 : 0;
        }

        // «Непосида»: fewer wishes, but longer ones (and paid as the harder ones)
        private static void Longer(ItemDrop.ItemData item, Req r)
        {
            Norov.Character c = Norov.CharOf(item);
            if (c == null || c.Id != "fidget") return;
            if (r.Type == "sit" || r.Type == "sail" || r.Type == "ride" || r.Type == "dress") r.Need = Mathf.CeilToInt(r.Need * 1.5f / 60f) * 60;
            else if (r.Type == "hunt") r.Need += 2;
            else if (r.Type == "feathers") r.Need += 5;
            else return;
            r.Reward = Mathf.Max(r.Reward, 2);
        }

        // a new wish of this type; one two lands beyond the world never, one from the next land for 2 sparks
        private static Req Make(Player p, bool munin, ItemDrop.ItemData item, string type)
        {
            Req r = MakeAny(p, munin, item, type);
            if (r == null) return null;
            Longer(item, r);
            if (Beyond(p, r, munin)) { Log(type + " " + r.Arg + ": too far beyond the world, not asked"); return null; }
            if (FromAfar(p, r, munin)) r.Reward = Mathf.Max(r.Reward, 2);
            return r;
        }

        private static Req MakeAny(Player p, bool munin, ItemDrop.ItemData item, string type)
        {
            Req r = new Req { Type = type };
            switch (type)
            {
                case "want":
                {
                    List<Trust.Food> foods = Trust.EdibleFoods(munin, item);
                    if (foods == null) return null;
                    int land = Stage.Land(p);
                    List<Trust.Food> afar = foods.FindAll(x => Satiety.LandOf(x.Prefab, x.Class) == land + 1);
                    if (afar.Count > 0 && UnityEngine.Random.value < AheadChance) { r.Arg = afar[UnityEngine.Random.Range(0, afar.Count)].Prefab; return r; }
                    foods = foods.FindAll(x => Satiety.LandOf(x.Prefab, x.Class) <= land); // (nothing from a land the world has not reached yet)
                    if (foods.Count == 0) return null;
                    Trust.Food fav = Satiety.FavFood(item, munin);
                    bool favOk = fav != null && foods.Exists(x => x.Prefab == fav.Prefab); // (its favourite only when it may eat it now)
                    Trust.Food f = favOk && UnityEngine.Random.value < 0.35f ? fav : Fresh(p, item, foods, x => Satiety.LandOf(x.Prefab, x.Class));
                    r.Arg = f.Prefab;
                    return r;
                }
                case "show":
                {
                    List<string> can = new List<string>();
                    Heightmap.Biome here = p.GetCurrentBiome();
                    string lands;
                    p.m_customData.TryGetValue(Diary.LandsKey, out lands);
                    int now = Stage.Land(p);
                    Heightmap.Biome next = Stage.NextLand(p); // (the land beyond the one the world opens: rarely, seen or not)
                    if (next != Heightmap.Biome.None && next != here && UnityEngine.Random.value < AheadChance) { r.Arg = "biome:" + next; return r; }
                    if (lands != null)
                        foreach (string b in lands.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            Heightmap.Biome lb;
                            if (b != here.ToString() && b != "Ocean" && b != "None" && TryBiome("biome:" + b, out lb) && Stage.LandOf(lb) <= now) can.Add("biome:" + b); // (lands seen ahead of the world: only by the roll above)
                        }
                    if (lands != null && lands.Contains("|Mountain|") && here != Heightmap.Biome.Mountain) can.Add("peak"); // (never where you already are: done at once)
                    if (here != Heightmap.Biome.Ocean) can.Add("sea");
                    if (!p.InInterior()) can.Add("dungeon");
                    if (can.Count == 0) return null;
                    r.Arg = Fresh(p, item, can, x => { Heightmap.Biome b; return TryBiome(x, out b) ? Stage.LandOf(b) : now; }); // (peak, sea, dungeon: never old)
                    r.Reward = r.Arg == "peak" || r.Arg == "dungeon" ? 2 : 1;
                    return r;
                }
                case "revenge":
                {
                    if (!munin) return null;
                    string foe = RecentFoe(p);
                    if (foe == null) return null;
                    r.Arg = foe; r.Reward = 2;
                    return r;
                }
                case "hunt":
                {
                    if (!munin) return null;
                    string[] afar = null;
                    bool far = UnityEngine.Random.value < AheadChance && HuntKinds.TryGetValue(Stage.NextLand(p), out afar);
                    r.Arg = far ? afar[UnityEngine.Random.Range(0, afar.Length)] : HuntKind(p);
                    if (r.Arg == null) return null;
                    r.Need = far ? UnityEngine.Random.Range(2, 4) : UnityEngine.Random.Range(3, 7);
                    float way = UnityEngine.Random.value; // (half the time a way: by night, or only by his own blow — fewer then)
                    if (!far && way < 0.25f) { r.Mode = "night"; r.Need = Mathf.Max(2, r.Need - 1); }
                    else if (!far && way < 0.5f) { r.Mode = "raven"; r.Need = UnityEngine.Random.Range(2, 4); }
                    r.Reward = r.Need >= 5 || r.Mode == "raven" ? 2 : 1;
                    return r;
                }
                case "fight":
                    if (!munin) return null;
                    r.Need = UnityEngine.Random.value < 0.3f ? 2 : 1; // stars at least
                    r.Reward = r.Need;
                    return r;
                case "ride":
                {
                    if (!munin) return null;
                    string rode;
                    if (!p.m_customData.TryGetValue(RodeKey, out rode) || rode != World) return null; // (never ridden here: nothing to ride)
                    r.Need = UnityEngine.Random.Range(2, 5) * 60;
                    return r;
                }
                case "tub":
                    if (Bath.DirtyMinutes(item) < TubDirtyMin || !Bath.TubKnown(p)) return null;
                    return r;
                case "sit":
                    if (munin) return null;
                    r.Need = UnityEngine.Random.Range(3, 6) * 60;
                    return r;
                case "sail":
                    if (munin || Stage.Of(p) < 1) return null;
                    r.Need = UnityEngine.Random.Range(3, 6) * 60;
                    return r;
                case "dress":
                {
                    if (Trust.SettledLevel(item) < Outfits.WardrobeLevel) return null; // (it lets itself be dressed only from the wardrobe's level on)
                    List<string> looks = new List<string>();
                    foreach (Outfits.Entry e in Outfits.Catalog())
                        if (Outfits.Listed(e) && (e.Gear == null || Outfits.OwnHat(e)) && Outfits.IsOpen(item, e, true) && Outfits.Worn(item, e.Slot) != e.Prefab) looks.Add(e.Prefab);
                    if (looks.Count == 0) return null; // (nothing open to put on yet: the wardrobe opens with trust)
                    r.Arg = looks[UnityEngine.Random.Range(0, looks.Count)];
                    r.Need = UnityEngine.Random.Range(3, 6) * 60;
                    return r;
                }
                case "feathers":
                    if (ObjectDB.instance == null || ObjectDB.instance.GetItemPrefab(FeatherPrefab) == null) return null;
                    r.Arg = FeatherPrefab;
                    r.Need = UnityEngine.Random.Range(1, 3) * 5;
                    return r;
                case "shiny":
                {
                    int land = Stage.Land(p) + (UnityEngine.Random.value < AheadChance ? 1 : 0);
                    if (Stage.Land(p) >= 4 && UnityEngine.Random.value < CoinsChance) // (a heap of coins: more the further the world)
                    {
                        r.Arg = "Coins"; r.Need = Stage.Land(p) * 25;
                        return r;
                    }
                    List<string> pool = new List<string>(Shinies);
                    pool.RemoveAll(x => Satiety.LandOf(x, 0) > land || ObjectDB.instance == null || ObjectDB.instance.GetItemPrefab(x) == null);
                    if (pool.Count == 0) return null;
                    r.Arg = Fresh(p, item, pool, x => Satiety.LandOf(x, 0));
                    r.Reward = Satiety.LandOf(r.Arg, 0) >= 3 || r.Arg == "Ruby" ? 2 : 1;
                    return r;
                }
                case "cache":
                {
                    Vector3 at;
                    if (!PickCache(p, p.transform.position, CacheMin, CacheMax, false, out at)) return null;
                    PlaceCache(r, at);
                    r.Reward = 2;
                    return r;
                }
                case "teach":
                    if (!Words.HasRoom(item)) return null;
                    return r;
                case "meet":
                    if (ZNet.instance == null || ZNet.instance.GetPlayerList().Count < 2 || !Bonds.HasFriendOrEnemy(item)) return null; // (someone to meet, and on the server now)
                    return r;
            }
            return null;
        }

        private static string Asked(Player p, bool munin, Req r)
        {
            switch (r.Type)
            {
                case "want":
                    return Say(p, munin, "req_want", munin ? Lang.F("Слухай. Хочу {0}. Не завтра — зараз би.", ItemName(r.Arg)) : Lang.F("У мене маленьке прохання: {0}. Дуже хочеться.", ItemName(r.Arg)), "food", ItemName(r.Arg));
                case "show":
                    if (r.Arg == "peak") return Say(p, munin, "req_show_peak", munin ? Lang.T("Хочу на саму верхівку гори. Щоб усе внизу — дрібне.") : Lang.T("Візьми мене на гірську вершину. Звідти видно все."));
                    if (r.Arg == "sea") return Say(p, munin, "req_show_sea", munin ? Lang.T("Хочу в море. Далеко, щоб берега не видно.") : Lang.T("Покажи мені відкрите море. Давно не бачив хвиль."));
                    if (r.Arg == "dungeon") return Say(p, munin, "req_show_dungeon", munin ? Lang.T("Своди мене під землю. Скучив за темрявою.") : Lang.T("Хочу в підземелля. Подивитись, що там ховають."));
                    Heightmap.Biome b; TryBiome(r.Arg, out b);
                    return Say(p, munin, "req_show_biome", munin ? Lang.F("Давно не був {0}. Неси мене туди.", Composer.BiomeName(b, true)) : Lang.F("Скучив за краєм. Покажи мені, як там {0}.", Composer.BiomeName(b, true)), "place", Composer.BiomeName(b, false), "place_in", Composer.BiomeName(b, true));
                case "revenge":
                    return SayFoe(p, munin, "req_revenge", Lang.F("Той {0} тебе ледь не зжер. Знайди такого й розмаж.", Foe(r.Arg)), r.Arg);
                case "hunt":
                    if (r.Mode == "night") return SayFoe(p, munin, "req_hunt_night", Lang.F("Уночі. {1} — штук {0}. Вдень не рахується.", r.Need, Foe(r.Arg)), r.Arg, "n", RavenAbilities.SpokenCount(r.Need));
                    if (r.Mode == "raven") return SayFoe(p, munin, "req_hunt_raven", Lang.F("Цього разу б'ю я. {1} — штук {0}. Ти тільки підставляй.", r.Need, Foe(r.Arg)), r.Arg, "n", RavenAbilities.SpokenCount(r.Need));
                    return SayFoe(p, munin, "req_hunt", Lang.F("Руки сверблять. Покладемо штук {0} — {1}. Я дивлюсь.", r.Need, Foe(r.Arg)), r.Arg, "n", RavenAbilities.SpokenCount(r.Need));
                case "fight":
                    return Say(p, munin, r.Need >= 2 ? "req_fight2" : "req_fight", r.Need >= 2 ? Lang.T("Хочу бачити справжній бій. Звір із двома зірками, і я на плечі.") : Lang.T("Хочу бачити бій. Знайди звіра з зіркою, а я подивлюсь з плеча."));
                case "ride":
                    return Say(p, munin, "req_ride", Lang.F("Покатай мене верхи. Хвилин {0}. Хочу відчути себе вершником.", r.Need / 60), "n", RavenAbilities.SpokenCount(r.Need / 60));
                case "sit":
                    return Say(p, munin, "req_sit", Lang.F("Посидь зі мною біля вогню хвилин {0}. Просто посидь.", r.Need / 60), "n", RavenAbilities.SpokenCount(r.Need / 60));
                case "sail":
                    return Say(p, munin, "req_sail", Lang.F("Поплаваймо. Хвилин {0} під вітрилом, я на плечі.", r.Need / 60), "n", RavenAbilities.SpokenCount(r.Need / 60));
                case "dress":
                    return Say(p, munin, "req_dress", munin ? Lang.F("Оце хочу: {0}. Вдягни й не знімай хвилин {1}.", LookName(r.Arg), r.Need / 60) : Lang.F("Мені б оце: {0}. Хвилин {1} поносити — і досить.", LookName(r.Arg), r.Need / 60), "look", LookName(r.Arg), "n", RavenAbilities.SpokenCount(r.Need / 60));
                case "feathers":
                    return Say(p, munin, "req_feathers", munin ? Lang.F("Гніздо лисіє. Принеси пір'я — штук {0}.", r.Need) : Lang.F("Гнізду б пір'я. Штук {0}, м'якого.", r.Need), "n", RavenAbilities.SpokenCount(r.Need));
                case "shiny":
                    if (r.Need > 1) return Say(p, munin, "req_coins", munin ? Lang.F("Монети. {0}. Дзенькають — значить, мої.", r.Need) : Lang.F("Скарбничці б монет. {0}, не менше.", r.Need), "n", RavenAbilities.SpokenCount(r.Need));
                    return Say(p, munin, "req_shiny", munin ? Lang.F("Дай мені {0}. Моє буде. Не питай навіщо.", ItemName(r.Arg)) : Lang.F("Мені б {0}. Для скарбнички. Ти ж розумієш.", ItemName(r.Arg)), "thing", ItemName(r.Arg));
                case "cache":
                    return Say(p, munin, "req_cache", munin ? Lang.T("Я дещо заховав. Знайдеш — твоє. Не знайдеш — ну й лох.") : Lang.T("Я сховав скарб. Знайди його — дивись на мапу і слухай мене."));
                case "teach":
                    return Say(p, munin, "req_teach", munin
                        ? Pick(Lang.T("Навчи мене слова. Якогось смачного. Повторю так, що всі охриніють."), Lang.T("Хочу нове слово. Скажи щось — я запам'ятаю і буду дражнитись."), Lang.T("Мені нудно. Навчи якогось слова, бо вигадаю своє."))
                        : Pick(Lang.T("Навчи мене якогось слова. Я повторюватиму — не завжди вчасно, обіцяю."), Lang.T("У мене прохання: скажи мені нове слово. Ворони вміють повторювати."), Lang.T("Хочу вивчити слово. Твоє, людське. Напиши його в книзі.")));
                case "tub":
                    return Say(p, munin, "req_tub", munin ? Lang.T("Я смерджу вже сам від себе. Гаряча купіль, ти і я. Сьогодні.") : Lang.T("Я брудний уже надто довго. Гаряча купіль — от чого я хочу."));
                case "meet":
                    return Say(p, munin, "req_meet", munin ? Lang.T("Хочу побачити тих, з ким у мене свої рахунки. Підведи мене до них.") : Lang.T("Познайом мене ближче з моїми знайомими воронами. Підійди до когось."));
            }
            return null;
        }

        // ---------------------------------------------------------------- done, gone, reminded

        private static void Complete(Player p, bool munin, ItemDrop.ItemData item, Req r)
        {
            r.Done = true;
            Save(item, r);
            int sparks = Sparks(item) + r.Reward;
            SetSparks(item, sparks);
            float trust = Trust.Gift(item, munin, TrustPerSpark * r.Reward, Lang.T("виконане прохання")); // (its own words: «прохання» alone is a heading of the heart's board)
            Satiety.Change(item, SatietyGift);
            Nicks.Add(item, "reqs", 1);
            Nest.Deed(p, Nest.RequestMinutes, Lang.T("виконане прохання ворона"));
            if (r.Type != "teach") Speak(p, munin, Say(p, munin, "req_done", munin ? Lang.T("Ну. Можеш, коли хочеш.") : Lang.T("Дякую. Я це запам'ятаю.")), false); // (a word taught: it tries the word instead)
            string spark = Lang.N(r.Reward, "іскринка|іскринки|іскринок");
            RavenMsg.Center(p, Lang.F("{0}: прохання виконано! +{1}{2}", Plugin.RavenName(munin), spark, sparks >= SparksForRune ? Lang.T("\nУ книзі можна запалити руну.") : ""));
            Log("done " + r.Type + ": trust +" + trust.ToString("0.#", CultureInfo.InvariantCulture) + ", sparklets " + sparks);
            RavenBook.Refresh();
        }

        private static void Expire(Player p, bool munin, ItemDrop.ItemData item, Req r)
        {
            Save(item, null);
            Log(Plugin.RavenName(munin) + "'s wish passed: " + r.Type);
            if (munin && Shoulder.Present(p, true)) Speak(p, true, Say(p, true, "req_expired", Lang.T("Ну і хрін з ним. Сам якось.")), false);
            RavenBook.Refresh();
        }

        private static void Remind(Player p, bool munin, ItemDrop.ItemData item, Req r)
        {
            if (r.Reminded >= Day || Day == r.Day || !Shoulder.Present(p, munin) || !Calm(p)) return;
            r.Reminded = Day;
            Save(item, r);
            Speak(p, munin, Say(p, munin, "req_remind", munin ? Lang.T("Не забу{в|ла}?") + " " + Goal(r) + "." : Lang.F("Нагадую, без тиску. {0}.", Goal(r)), "goal", Goal(r)), true);
        }

        // «Навчи мене»: the word written in the wish's card. null: taught, else why not
        internal static string Teach(Player p, bool munin, string typed)
        {
            ItemDrop.ItemData item = Mine(p, munin);
            Req r = Of(item);
            if (!Waiting(r) || r.Type != "teach") return Lang.T("Він уже не просить слова.");
            string why = Words.Teach(item, typed);
            if (why != null) return why;
            string word = Words.Clean(typed);
            Complete(p, munin, item, r);
            Words.Tried(p, munin, word);
            return null;
        }

        private static string Pick(params string[] l) { return l[UnityEngine.Random.Range(0, l.Length)]; }

        // its card was opened: a wish done is put away (the seal goes)
        internal static void Seen(ItemDrop.ItemData item)
        {
            Req r = Of(item);
            if (r != null && r.Done) Save(item, null);
        }

        // ---------------------------------------------------------------- what moves it on

        // a bath in the hot tub with you (Bath.Done)
        internal static void OnTubBath(Player p, bool munin, ItemDrop.ItemData item)
        {
            Req r = Of(item);
            if (!Waiting(r) || r.Type != "tub" || Trust.Stranger(item, p)) return;
            r.Have = r.Need;
            Complete(p, munin, item, r);
        }

        internal static void OnFed(Player p, bool munin, ItemDrop.ItemData item, string prefab)
        {
            Req r = Of(item);
            if (!Waiting(r) || r.Type != "want" || r.Arg != prefab) return;
            r.Have = r.Need;
            Complete(p, munin, item, r);
        }

        private static readonly List<string> Kills = new List<string>(); // the kinds you killed lately (for «Полюємо»)
        private static string _killsWorld;

        internal static void OnKill(string prefab, string token, int stars, bool boss, bool raven, bool own)
        {
            Player p = Player.m_localPlayer;
            if (p == null || string.IsNullOrEmpty(token)) return;
            Words.OnKill(stars, boss); // a taught word for the win
            if (_killsWorld != World) { Kills.Clear(); _killsWorld = World; } // (another world: its own kinds)
            if (!boss) { Kills.Add(token); if (Kills.Count > 40) Kills.RemoveAt(0); }
            ItemDrop.ItemData item = Mine(p, true);
            Req r = Of(item);
            if (!Waiting(r)) return;
            if (r.Type == "fight") // (your own blow — not a pet's or his; him watching from the shoulder or out in that fight with his own blows; a boss is not «a beast with stars»)
            {
                bool watched = Shoulder.Present(p, true) || Strike.Busy(p, true); // (the user 2026-11-05: Munin off striking at it saw the fight too)
                if (!boss && own && !raven && stars >= r.Need && watched) { r.Have = r.Need; Complete(p, true, item, r); }
                else if (!boss && Plugin.Instance != null) Plugin.Instance.Log("Request fight: not counted — " + (!own ? "not your own blow" : raven ? "a raven's blow" : stars < r.Need ? stars + " star(s) of " + r.Need : "Munin neither on your shoulder nor in the fight"));
                return;
            }
            if (r.Arg != token) return;
            if (r.Type == "revenge") { r.Have = r.Need; Complete(p, true, item, r); return; }
            if (r.Type != "hunt") return;
            bool counts = r.Mode == "raven" ? raven
                : r.Mode == "night" ? (raven || Shoulder.Present(p, true)) && EnvMan.IsNight()
                : raven || Shoulder.Present(p, true);
            if (!counts) return;
            r.Have++;
            if (r.Have >= r.Need) Complete(p, true, item, r);
            else Save(item, r);
        }

        // the diary: who hurt or killed you (Munin's revenge remembers it two game days)
        internal static void OnEvent(Ev type, string arg, int value)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (type == Ev.Ride) { string w; if (!p.m_customData.TryGetValue(RodeKey, out w) || w != World) p.m_customData[RodeKey] = World; return; } // (now «Покатай мене» may come; any mount)
            if (string.IsNullOrEmpty(arg) || arg.IndexOf('_') < 0) return; // (a creature's token, «enemy_troll»; a hit type has none)
            if ((type == Ev.Death && value == 1) || type == Ev.NearDeath) p.m_customData[FoeKey] = arg + "|" + Day + "|" + World;
        }

        private static string RecentFoe(Player p)
        {
            string raw;
            if (!p.m_customData.TryGetValue(FoeKey, out raw) || raw == null) return null;
            string[] a = raw.Split('|');
            if (a.Length < 3 || a[2] != World || Day - Errands.I(a[1], -99) > 2) return null;
            return a[0];
        }

        private static readonly Dictionary<Heightmap.Biome, string[]> HuntKinds = new Dictionary<Heightmap.Biome, string[]>
        {
            { Heightmap.Biome.Meadows, new[] { "enemy_greyling", "enemy_boar", "enemy_neck" } },
            { Heightmap.Biome.BlackForest, new[] { "enemy_greydwarf", "enemy_skeleton", "enemy_greydwarfbrute" } },
            { Heightmap.Biome.Swamp, new[] { "enemy_draugr", "enemy_blob", "enemy_leech" } },
            { Heightmap.Biome.Mountain, new[] { "enemy_wolf", "enemy_drake" } },
            { Heightmap.Biome.Plains, new[] { "enemy_goblin", "enemy_deathsquito" } },
            { Heightmap.Biome.Mistlands, new[] { "enemy_seeker", "enemy_tick" } },
            { Heightmap.Biome.AshLands, new[] { "enemy_charred_melee", "enemy_charred_archer", "enemy_volture" } },
            { Heightmap.Biome.DeepNorth, new[] { "enemy_jotun_warrior", "enemy_frysling", "enemy_blobfrost" } },
        };

        // a kind of the land you are in: one you killed lately, else one of that land
        private static string HuntKind(Player p)
        {
            if (Kills.Count > 0) return Kills[UnityEngine.Random.Range(Mathf.Max(0, Kills.Count - 10), Kills.Count)];
            string[] kinds;
            return HuntKinds.TryGetValue(p.GetCurrentBiome(), out kinds) ? kinds[UnityEngine.Random.Range(0, kinds.Length)] : null;
        }

        // every 2 s: the wishes about where you are and what you do (show, sit, sail, ride, dress, meet)
        private static void PlaceTick(Player p, bool munin, ItemDrop.ItemData item, Req r)
        {
            bool on = Shoulder.Present(p, munin);
            if (r.Type == "show")
            {
                if (!on) return;
                bool there;
                Heightmap.Biome b;
                if (r.Arg == "peak") there = !p.InInterior() && p.GetCurrentBiome() == Heightmap.Biome.Mountain && p.transform.position.y >= PeakY;
                else if (r.Arg == "sea") there = p.GetCurrentBiome() == Heightmap.Biome.Ocean && (p.GetStandingOnShip() != null || p.IsSwimming() || Senses.AtHelm) && Deep(p.transform.position);
                else if (r.Arg == "dungeon") there = p.InInterior();
                else there = TryBiome(r.Arg, out b) && !p.InInterior() && p.GetCurrentBiome() == b;
                if (there) Complete(p, munin, item, r);
                return;
            }
            if (r.Type == "sit")
            {
                bool fire = on && EffectArea.IsPointInsideArea(p.transform.position, EffectArea.Type.Heat, 1f) != null;
                if (!fire && !NearOwnPerch(p, munin)) return;
                Tick2(p, munin, item, r);
                return;
            }
            if (r.Type == "sail") // (under sail, going ahead: at the helm or aboard; rowing and drifting do not count)
            {
                Ship s = p.GetControlledShip();
                if (s == null) s = p.GetStandingOnShip();
                if (on && s != null && s.GetSpeedSetting() >= Ship.Speed.Half && s.GetSpeed() > 1.5f) Tick2(p, munin, item, r);
                return;
            }
            if (r.Type == "ride")
            {
                if (on && p.IsRiding()) Tick2(p, munin, item, r);
                return;
            }
            if (r.Type == "dress") // (worn on the shoulder: a look changes only there)
            {
                Outfits.Entry e = Outfits.Find(r.Arg);
                if (on && e != null && Outfits.Worn(item, e.Slot) == r.Arg) Tick2(p, munin, item, r);
                return;
            }
            if (r.Type == "meet")
            {
                if (!on) return;
                foreach (Player o in Player.GetAllPlayers())
                {
                    if (o == null || o == p || Vector3.Distance(o.transform.position, p.transform.position) > 10f) continue;
                    foreach (bool theirs in new bool[] { false, true })
                    {
                        float s = Bonds.Score(p, munin, o, theirs);
                        if (s >= Bonds.FriendAt || s <= Bonds.EnemyAt) { Complete(p, munin, item, r); return; }
                    }
                }
            }
        }

        // two seconds more of a timed wish (sit, sail, ride, dress)
        private static void Tick2(Player p, bool munin, ItemDrop.ItemData item, Req r)
        {
            r.Have += 2;
            if (r.Have >= r.Need) Complete(p, munin, item, r);
            else Save(item, r); // (the wish is read anew each tick: every count kept)
        }

        // it sits on a perch and you are by it (not left on one at home while you are away)
        private static bool NearOwnPerch(Player p, bool munin)
        {
            long pid = p.GetPlayerID();
            foreach (PerchView v in PerchView.All)
                if (v != null && v.Valid && v.SeatOf(pid, munin) >= 0 && Vector3.Distance(v.transform.position, p.transform.position) <= 15f) return true;
            return false;
        }

        private static bool Deep(Vector3 at)
        {
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            return WorldGenerator.instance == null || WorldGenerator.instance.GetHeight(at.x, at.z) < water - 10f;
        }

        // a wish given from the bag (the card's «Дати»): shiny things, feathers
        internal static bool Gives(Req r) { return r != null && (r.Type == "shiny" || r.Type == "feathers"); }

        // «Дай блискуче», «Принеси пір'я»: the card's button; null when given, else why not
        internal static string GiveShiny(Player p, bool munin)
        {
            ItemDrop.ItemData item = Mine(p, munin);
            Req r = Of(item);
            if (!Waiting(r) || !Gives(r)) return Lang.T("Зараз він нічого не просить дати.");
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(r.Arg) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            int need = Mathf.Max(1, r.Need);
            if (drop == null || p.GetInventory().CountItems(drop.m_itemData.m_shared.m_name, -1, false) < need) return Lang.F("У тебе нема: {0}.", ShinyName(r));
            p.GetInventory().RemoveItem(drop.m_itemData.m_shared.m_name, need, -1, false); // (any world level)
            if (r.Type == "shiny") item.m_customData[HoardKey] = (Hoard(item) + 1).ToString(CultureInfo.InvariantCulture);
            r.Have = r.Need;
            Complete(p, munin, item, r);
            return null;
        }

        // «Запалити руну»: this raven's sparklets light the rune you chose (RavenBookRunes' lighting); null when lit
        internal static string Light(Player p, bool munin, string rune)
        {
            ItemDrop.ItemData item = Mine(p, munin);
            if (Sparks(item) < SparksForRune) return Lang.F("Треба {0} іскринок.", SparksForRune);
            if (RuneStars.SparkOn(p, rune, Lang.F("Іскринки {0}", Plugin.RavenNameGen(munin))) == null) return Lang.T("Ця руна вже не візьме іскру.");
            SetSparks(item, Sparks(item) - SparksForRune);
            Log(Plugin.RavenName(munin) + "'s sparklets lit " + rune);
            return null;
        }

        // ---------------------------------------------------------------- «Покажи мені»: where

        // the nearest place of the wish (searched once, on the card's «Показати на мапі»)
        private static bool ShowWhere(Player p, ItemDrop.ItemData item, Req r, out Vector3 at)
        {
            at = Vector3.zero;
            if (r == null || p == null || WorldGenerator.instance == null) return false;
            if (r.HasPlace) { at = new Vector3(r.Cx, 0f, r.Cz); return true; }
            if (r.Type != "show" || r.Arg == "dungeon") return false;
            Heightmap.Biome want = r.Arg == "peak" ? Heightmap.Biome.Mountain : r.Arg == "sea" ? Heightmap.Biome.Ocean : Heightmap.Biome.None;
            if (want == Heightmap.Biome.None && !TryBiome(r.Arg, out want)) return false;
            Vector3 me = p.transform.position;
            for (float d = 100f; d <= 4000f; d += 100f)
            {
                float best = -1f;
                for (int i = 0; i < 24; i++)
                {
                    float ang = i * 15f * Mathf.Deg2Rad;
                    Vector3 q = me + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * d;
                    if (WorldGenerator.instance.GetBiome(q) != want) continue;
                    if (r.Arg == "sea" && !Deep(q)) continue;
                    float h = WorldGenerator.instance.GetHeight(q.x, q.z);
                    if (r.Arg == "peak" ? h > best : best < 0f) { best = h; at = q; }
                }
                if (best >= 0f || (r.Arg == "peak" && best > PeakY)) break;
            }
            if (at == Vector3.zero) return false;
            r.HasPlace = true; r.X = r.Cx = at.x; r.Z = r.Cz = at.z;
            Save(item, r);
            return true;
        }

        // ---------------------------------------------------------------- the cache

        private static void PlaceCache(Req r, Vector3 at)
        {
            r.HasPlace = true; r.X = at.x; r.Z = at.z;
            Vector2 off = UnityEngine.Random.insideUnitCircle * (CacheRing * 0.6f); // the ring is not centred on it
            r.Cx = at.x + off.x; r.Cz = at.z + off.y;
            r.Verified = false; r.Shown = false; r.Dug = 0;
        }

        // where its cache goes: the one finder of quiet spots in the wild (WorldSpot), its own chest not counted as built
        private static bool PickCache(Player p, Vector3 from, float min, float max, bool loaded, out Vector3 at) { return WorldSpot.Pick(p, from, min, max, loaded, CachePrefab, out at); }
        private static bool NearBase(Player p, Vector3 q) { return WorldSpot.NearBase(p, q, CachePrefab); }
        private static bool GroundClear(Vector3 q, out Vector3 spot) { return WorldSpot.GroundClear(q, out spot); }
        private static float Flat(Vector3 a, Vector3 b) { return WorldSpot.Flat(a, b); }

        private static readonly HotCold _finder = new HotCold(); // (the raven as a finder: the one way of it, a story's search too — StorySeek)
        private static bool _quietTold, _cawTold; // (once a game each, in the log)

        // twice a second while a cache waits: the ground checked once it is loaded, the finder's caws, warmer/colder,
        // the raven onto the spot
        private static void CacheTick(Player p, bool munin, ItemDrop.ItemData item, Req r)
        {
            float d = Flat(p.transform.position, r.Place);
            if (d > CawFrom || HotCold.Held) { _finder.Reset(); return; } // (the story's search holds the raven: one finder at a time — HotCold)
            // with you (on the shoulder in any pose, or out flying near): the finder's caw comes from you anyway
            if (Shoulder.ModelOf(p, munin) == null) { if (!_quietTold) { _quietTold = true; Log("cache finder quiet: " + Plugin.RavenName(munin) + " is not with you (" + Mathf.RoundToInt(d) + " m off)"); } return; }
            // the finder: caws faster the nearer (HotCold)
            if (_finder.Caw(p, munin, d, CawFrom) && !_cawTold) { _cawTold = true; Log("cache finder: " + Plugin.RavenName(munin) + " caws, " + Mathf.RoundToInt(d) + " m off"); }
            if (d > CacheNear) { _finder.Reset(); return; }
            if (!r.Verified && ZoneSystem.instance != null && ZoneSystem.instance.IsZoneLoaded(r.Place))
            {
                Vector3 spot;
                if (NearBase(p, new Vector3(r.X, 0f, r.Z)) || !GroundClear(new Vector3(r.X, 0f, r.Z), out spot))
                {
                    Vector3 moved;
                    if (PickCache(p, r.Place, 25f, 70f, true, out moved)) { PlaceCache(r, moved); Log("cache moved off a bad spot: " + moved); }
                    else Log("cache: no better spot near, kept where it is");
                    r.Verified = true; // (looked at once: never a search twice a second)
                    Save(item, r);
                    return;
                }
                r.Verified = true;
                Save(item, r);
            }
            if (!Shoulder.Present(p, munin)) return;
            if (d <= 4f)
            {
                if (!r.Shown)
                {
                    r.Shown = true;
                    Save(item, r);
                    Speak(p, munin, Say(p, munin, "req_here", munin ? Lang.T("Тут! Копай, чого стоїш.") : Lang.T("Тут! Під ногами. Кайло в руки.")), false);
                    Vector2 side = UnityEngine.Random.insideUnitCircle.normalized * 0.7f; // (beside it: the pickaxe goes where it points)
                    Vector3 beside = new Vector3(r.X + side.x, 0f, r.Z + side.y);
                    Vector3 spot = new Vector3(beside.x, ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(beside) : p.transform.position.y, beside.z);
                    PerchView v;
                    if (SitHere.Place(p, munin, spot, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), null, false, out v) == null) { _digSpot = v; _digMunin = munin; }
                }
                return;
            }
            int word = _finder.Word(d);
            if (word > 0) Speak(p, munin, Say(p, munin, "req_warmer", munin ? Lang.T("Тепліше.") : Lang.T("Тепліше…")), false);
            else if (word < 0) Speak(p, munin, Say(p, munin, "req_colder", munin ? Lang.T("Холодно, бовдуре.") : Lang.T("Холодніше…")), false);
        }

        private static PerchView _digSpot;
        private static bool _digMunin;
        private static Req _spawn;
        private static ItemDrop.ItemData _spawnItem;
        private static bool _spawnMunin;
        private static float _spawnAt;

        // a pickaxe's blow on the ground (TerrainOp): on the cache's spot it counts
        internal static void OnDig(Vector3 at)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            ItemDrop.ItemData weapon = p.GetCurrentWeapon();
            if (weapon == null || weapon.m_shared.m_skillType != Skills.SkillType.Pickaxes || !p.InAttack()) return;
            if (Flat(p.transform.position, at) > 6f) return; // (someone else's blow)
            foreach (bool munin in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Mine(p, munin);
                Req r = Of(item);
                if (!Waiting(r) || r.Type != "cache" || Flat(at, r.Place) > 2.5f) continue;
                r.Dug++;
                Save(item, r);
                if (r.Dug >= DigHits && _spawn == null) { _spawn = r; _spawnItem = item; _spawnMunin = munin; _spawnAt = Time.time + 0.4f; }
                return;
            }
        }

        private static void SpawnCache(Player p)
        {
            if (Time.time < _spawnAt) return;
            Req r = _spawn; ItemDrop.ItemData item = _spawnItem; bool munin = _spawnMunin;
            _spawn = null; _spawnItem = null;
            // still that wish, and you still by it (not after a death, a portal or another world: the next blow tries again)
            Req now = Of(Mine(p, munin));
            if (r == null || !Waiting(now) || now.Type != "cache" || now.X != r.X || now.Z != r.Z || Flat(p.transform.position, r.Place) > 20f) return;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(CachePrefab) : null;
            if (prefab == null) { Log("the cache chest is not in the game"); return; }
            float ground = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(r.Place) : p.transform.position.y;
            GameObject g = UnityEngine.Object.Instantiate(prefab, new Vector3(r.X, ground - 0.12f, r.Z), Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            Piece pc = g.GetComponent<Piece>();
            try { if (pc != null) pc.SetCreator(p.GetPlayerID(), Splatform.PlatformManager.DistributionPlatform.LocalUser.PlatformUserID); } catch (Exception e) { Log("cache creator: " + e.Message); }
            Container c = g.GetComponent<Container>();
            if (c != null) Fill(c.GetInventory(), r, Trust.Level(item));
            RavenFx.Play("vfx_offering", g.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            Log("cache chest dug up at " + g.transform.position + " by " + p.GetPlayerID() + ", items " + (c != null ? c.GetInventory().NrOfItems() : -1));
            if (_digSpot != null && _digMunin == munin) { Perches.CallBack(p, _digSpot, munin, item); }
            _digSpot = null;
            r.Have = r.Need;
            Complete(p, munin, item, r);
        }

        // coins by trust, what it «gathered» (amber, the land's berries or meat), now and then something rare
        private static void Fill(Inventory inv, Req r, int level)
        {
            if (inv == null) return;
            Add(inv, "Coins", UnityEngine.Random.Range(15, 35) + 8 * Mathf.Max(0, level));
            Add(inv, "Amber", UnityEngine.Random.Range(1, 3)); // (nothing of a later land's metal: a cache in the meadows must not skip ahead)
            Heightmap.Biome b = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(r.Place) : Heightmap.Biome.Meadows;
            string food;
            switch (b)
            {
                case Heightmap.Biome.BlackForest: food = UnityEngine.Random.value < 0.5f ? "Blueberries" : "DeerMeat"; break;
                case Heightmap.Biome.Swamp: food = "Turnip"; break;
                case Heightmap.Biome.Mountain: food = "WolfMeat"; break;
                case Heightmap.Biome.Plains: food = UnityEngine.Random.value < 0.5f ? "Cloudberry" : "LoxMeat"; break;
                case Heightmap.Biome.Mistlands: food = "HareMeat"; break;
                default: food = UnityEngine.Random.value < 0.5f ? "Raspberry" : "RawMeat"; break;
            }
            Add(inv, food, UnityEngine.Random.Range(2, 5));
            float rare = UnityEngine.Random.value;
            if (rare < 0.08f) Add(inv, "SilverNecklace", 1);
            else if (rare < 0.18f) Add(inv, "Ruby", 1);
            else if (rare < 0.35f) Add(inv, "AmberPearl", 1);
        }

        private static void Add(Inventory inv, string prefab, int n)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (go == null || n <= 0) return;
            inv.AddItem(go, n);
        }

        // ---------------------------------------------------------------- the chest (ZNetScene.Awake)

        private static GameObject _holder, _chest;

        // a small personal chest (the game's own, made small): only you open it; no hammer, nothing to take apart;
        // gone by itself once empty (a pit stays)
        internal static void Register(ZNetScene scene)
        {
            if (scene == null) return;
            if (_chest == null)
            {
                GameObject src = scene.GetPrefab("piece_chest_private");
                if (src == null) { Log("piece_chest_private not found: no cache chest"); return; }
                if (_holder == null) { _holder = new GameObject("RavenWatch_Requests"); _holder.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(_holder); }
                GameObject g = UnityEngine.Object.Instantiate(src, _holder.transform, false);
                g.name = CachePrefab;
                g.transform.localScale = Vector3.one * 0.6f;
                WearNTear wnt = g.GetComponent<WearNTear>();
                if (wnt != null) UnityEngine.Object.DestroyImmediate(wnt);
                StaticTarget st = g.GetComponent<StaticTarget>();
                if (st != null) UnityEngine.Object.DestroyImmediate(st); // (nothing to break: monsters must not stand hitting it)
                Piece pc = g.GetComponent<Piece>();
                if (pc != null) { pc.m_name = Lang.T("Схованка ворона"); pc.m_canBeRemoved = false; pc.m_resources = new Piece.Requirement[0]; }
                Container c = g.GetComponent<Container>();
                if (c != null)
                {
                    c.m_name = Lang.T("Схованка ворона");
                    c.m_privacy = Container.PrivacySetting.Public; // only yours all the same: RequestChestAccessPatch
                    c.m_autoDestroyEmpty = true;
                    c.m_checkGuardStone = false;
                    c.m_defaultItems = new DropTable();
                    c.m_width = 4; c.m_height = 2;
                }
                _chest = g;
            }
            if (!scene.m_prefabs.Contains(_chest)) scene.m_prefabs.Add(_chest);
            Dictionary<int, GameObject> named = Traverse.Create(scene).Field("m_namedPrefabs").GetValue<Dictionary<int, GameObject>>();
            if (named != null) named[CachePrefab.GetStableHashCode()] = _chest;
        }

        // ---------------------------------------------------------------- the map

        private static readonly List<Minimap.PinData> _pins = new List<Minimap.PinData>();
        internal static readonly List<Minimap.PinData> Ours = new List<Minimap.PinData>();
        private static bool _pinMunin;
        private static string _pinsFor;
        private static Minimap _map;

        private static void Pins(Player p)
        {
            if (Minimap.instance == null) return;
            if (_map != Minimap.instance) { _map = Minimap.instance; _pins.Clear(); Ours.Clear(); _pinsFor = null; }
            string want = null; Req at = null; bool munin = false;
            foreach (bool m in new bool[] { false, true })
            {
                Req r = Of(Mine(p, m));
                if (Waiting(r) && r.HasPlace) { at = r; munin = m; want = m + at.Type + at.Cx + "," + at.Cz; break; } // (not every progress tick)
            }
            if (want == _pinsFor) return;
            ClearPins();
            _pinsFor = want;
            if (at == null) return;
            Vector3 c = new Vector3(at.Cx, 0f, at.Cz);
            string label = at.Type == "cache" ? Lang.F("Схованка {0}", Plugin.RavenNameGen(munin)) : Lang.F("{0}: {1}", Plugin.RavenName(munin), Goal(at));
            if (at.Type == "cache")
                _pins.Add(ErrandMap.Zone(c, CacheRing));
            Minimap.PinData mark = Minimap.instance.AddPin(c, Minimap.PinType.RandomEvent, label, false, false, 0L, default(Splatform.PlatformUserID));
            mark.m_icon = ErrandMap.GreyOf(mark.m_icon);
            _pins.Add(mark);
            _pinMunin = munin;
            Ours.Clear(); Ours.AddRange(_pins);
        }

        private static void ClearPins()
        {
            if (Minimap.instance != null) foreach (Minimap.PinData pin in _pins) if (pin != null) Minimap.instance.RemovePin(pin);
            _pins.Clear(); Ours.Clear(); _pinsFor = null;
        }

        // after the map's redraw: ours in the raven's colour
        internal static void Tint()
        {
            Color c = _pinMunin ? new Color(0.95f, 0.4f, 0.35f, 1f) : new Color(0.45f, 0.65f, 1f, 1f);
            for (int i = Ours.Count - 1; i >= 0; i--)
            {
                Minimap.PinData pin = Ours[i];
                if (pin == null) { Ours.RemoveAt(i); continue; }
                Color k = ErrandMap.IsZone(pin) ? new Color(c.r, c.g, c.b, 0.8f) : c;
                if (pin.m_iconElement != null && pin.m_iconElement.color != k) pin.m_iconElement.color = k;
                if (pin.m_NamePinData != null && pin.m_NamePinData.PinNameText != null && pin.m_NamePinData.PinNameText.color != k) pin.m_NamePinData.PinNameText.color = k;
            }
        }

        // the card's «Показати на мапі»
        internal static string ShowOnMap(Player p, bool munin)
        {
            ItemDrop.ItemData item = Mine(p, munin);
            Req r = Of(item);
            Vector3 at;
            if (!Waiting(r) || !ShowWhere(p, item, r, out at)) return Lang.T("Тут мапа не допоможе.");
            _pinsFor = null; // (a place just found: its mark now)
            Pins(p);
            if (Minimap.instance != null) { RavenBook.Close(); Minimap.instance.ShowPointOnMap(at); }
            return null;
        }

        // ---------------------------------------------------------------- the test command: raven request [hugin|munin] [type|done|clear|sparks N]

        internal static string Cmd(Player p, string[] a)
        {
            bool munin = a.Length > 1 && a[1].ToLowerInvariant().StartsWith("m");
            string what = a.Length > 2 ? a[2].ToLowerInvariant() : "";
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return Plugin.RavenName(munin) + " не на тобі.";
            if (what == "clear") { Save(item, null); return "Прохання знято."; }
            if (what == "done") { Req r = Of(item); if (!Waiting(r)) return "Нема активного прохання."; Complete(p, munin, item, r); return "Виконано."; }
            if (what == "sparks") { SetSparks(item, a.Length > 3 ? Errands.I(a[3], 0) : SparksForRune); return "Іскринок: " + Sparks(item); }
            if (what == "next") { p.m_customData[NextKey] = World + ":" + Day; return "Наступне прохання — щойно буде спокійна хвилина."; }
            if (what.Length > 0)
            {
                Save(item, null);
                string why = Give(p, munin, what);
                return why ?? Goal(Of(item));
            }
            Req cur = Of(item);
            return cur == null ? "Прохання нема. Типи: want show revenge hunt fight ride sit sail dress feathers shiny cache meet tub; також done, clear, sparks N, next." : cur.Code() + "\n" + Goal(cur) + (cur.HasPlace ? " · до місця " + Mathf.RoundToInt(Flat(p.transform.position, cur.Place)) + " м" : "");
        }

        internal static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Requests: " + s); }
    }

    // a pickaxe's dig on the ground: the cache's spot counts it
    [HarmonyPatch(typeof(TerrainOp), "Awake")]
    internal static class RequestDigPatch
    {
        private static void Postfix(TerrainOp __instance)
        {
            try { if (__instance != null && !TerrainOp.m_forceDisableTerrainOps) Requests.OnDig(__instance.transform.position); }
            catch (Exception e) { PatchGuard.Fail("RequestDigPatch", e); }
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class RequestChestPatch
    {
        private static void Postfix(ZNetScene __instance) { try { Requests.Register(__instance); } catch (Exception e) { Requests.Log("chest: " + e); } }
    }

    // the cache is only for whoever dug it up, on every way in (a click, another mod taking from nearby chests): the
    // game's own access check, whatever privacy the chest says
    [HarmonyPatch(typeof(Container), "CheckAccess")]
    internal static class RequestChestAccessPatch
    {
        private static void Postfix(Container __instance, long playerID, ref bool __result)
        {
            try
            {
                if (__instance == null || Utils.GetPrefabName(__instance.gameObject) != Requests.CachePrefab) return;
                Piece pc = __instance.GetComponent<Piece>();
                long creator = pc != null ? pc.GetCreator() : 0L;
                __result = creator == 0L || creator == playerID;
            }
            catch (Exception e) { PatchGuard.Fail("RequestChestAccessPatch", e); }
        }
    }

    // each try to open the cache, in the log (until the chest is seen to open in a game)
    [HarmonyPatch(typeof(Container), "Interact")]
    internal static class RequestChestOpenLog
    {
        private static void Prefix(Container __instance, bool hold)
        {
            try
            {
                if (hold || __instance == null || Utils.GetPrefabName(__instance.gameObject) != Requests.CachePrefab) return;
                Piece pc = __instance.GetComponent<Piece>();
                ZNetView nv = __instance.GetComponent<ZNetView>();
                long me = Game.instance != null ? Game.instance.GetPlayerProfile().GetPlayerID() : 0L;
                Requests.Log("cache chest opened: creator " + (pc != null ? pc.GetCreator() : 0L) + ", you " + me + ", net " + (nv != null && nv.IsValid()) + ", owner " + (nv != null && nv.IsValid() && nv.IsOwner()) + ", items " + (__instance.GetInventory() != null ? __instance.GetInventory().NrOfItems() : -1));
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Minimap), "UpdatePins")]
    internal static class RequestMapTintPatch
    {
        private static void Postfix() { try { if (Requests.Ours.Count > 0) Requests.Tint(); } catch { } }
    }
}
#endif
