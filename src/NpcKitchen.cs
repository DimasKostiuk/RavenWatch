#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A cook's kitchen (the user 2026-11-12: «треба переробити систему кухаря»). What a cook does next and what its kitchen is, from the game's own lists:
    //   two chests   its pantry («Комора»: NpcWorker.Chest — its dishes go there, the base eats from it first: NpcMeals) and, if you show one, its ingredients
    //                («Інгредієнти»: NpcWorker.Supply, anywhere). With no second chest it takes from its pantry, as it always did. It cooks all there is:
    //                what you do not want cooked, you do not put there
    //   the kitchen  the cooking tools of its base up to Reach m (level ground) from its pantry: racks and ovens (CookingStation) and every crafting tool
    //                a food recipe names (a cauldron, a table to prepare food, a mod's own) — no tool is named here but by the recipes. Their fires
    //                (FireNear m round them) it keeps up itself, from where it takes
    //   the dishes   Book, once a world: what a rack or an oven turns into food, and every recipe at a tool whose thing is eaten — or is turned into food
    //                by an oven (a dough: Bakes; made, it stays in its hands and goes on to the oven). A recipe that wants one of several things (a fish
    //                cut up) gives by that thing's quality, as the game's own does
    //   what first   what is done comes off (it burns), a fire about to go out, an oven's fuel — then the best thing it can make now by what it was told
    //                (Tier: the dishes its tools can make, in thirds by how much they give — simple · middling · best; told one, that third first, then
    //                the nearest: nothing is left raw that makes anything). A dough already made: baked before anything new
    //   at a tool    a made dish takes CraftSeconds of its steps (its mood, craft and knack in them: NpcWorker), its move the tool's (a cauldron: «stir»)
    //   lacking      Lacks: what its kitchen wants, in the player's words — for its card and the heart's board
    // The limits: a recipe that wants a made dish for an ingredient finds it only if you put one among its ingredients (its own dishes go to the pantry).
    // Runs where the cook is run (Next); Tiers and Lacks on any game, only while a menu or a card shows them.
    internal static class NpcKitchen
    {
        internal static float Reach = 15f, FireNear = 3f, CraftSeconds = 8f;
        internal static readonly int KTier = "RavenWatch_npcCookTier".GetStableHashCode(); // int: 1 simple · 2 middling · 3 best (none kept: best) — in its locker
        private const string Cauldron = "$piece_cauldron"; // (only for its move: the one tool with a clip of its own)
        private const int Best = 2;

        // ---------------------------------------------------------------- the game's own lists, once a world
        internal sealed class Made { public Recipe R; public string Station = ""; public int Level = 1; public ItemDrop Food; public bool Bakes; }
        private sealed class Book
        {
            public readonly Dictionary<string, ItemDrop> Baked = new Dictionary<string, ItemDrop>(); // a thing's name → the food a rack or an oven makes of it
            public readonly List<Made> Recipes = new List<Made>();
            public readonly Dictionary<string, float> Worth = new Dictionary<string, float>();       // a dish's name → how much it gives
            public readonly HashSet<string> Tools = new HashSet<string>();                           // the crafting tools' names the recipes ask for
            public readonly HashSet<string> Doughs = new HashSet<string>();                          // what a recipe makes only to be baked
        }
        private static Book _book;
        private static object _bookWorld;

        private static float WorthOf(ItemDrop.ItemData it) { return it.m_shared.m_food + it.m_shared.m_foodStamina + it.m_shared.m_foodEitr; }
        private static string NameOf(ItemDrop d) { return d.m_itemData.m_shared.m_name; }

        private static Book B
        {
            get
            {
                if (_book != null && ReferenceEquals(_bookWorld, ZNet.instance)) return _book;
                Book b = new Book();
                if (ZNetScene.instance == null || ObjectDB.instance == null) return b; // (the game not ready yet: asked again)
                foreach (GameObject go in ZNetScene.instance.m_prefabs)
                {
                    CookingStation s = go != null ? go.GetComponent<CookingStation>() : null;
                    if (s == null || s.m_conversion == null) continue;
                    foreach (CookingStation.ItemConversion c in s.m_conversion)
                    {
                        if (c == null || c.m_from == null || c.m_to == null || c.m_to.m_itemData.m_shared.m_food <= 0f) continue;
                        if (!b.Baked.ContainsKey(NameOf(c.m_from))) b.Baked[NameOf(c.m_from)] = c.m_to;
                        b.Worth[NameOf(c.m_to)] = WorthOf(c.m_to.m_itemData);
                    }
                }
                foreach (Recipe r in ObjectDB.instance.m_recipes)
                {
                    if (r == null || !r.m_enabled || r.m_item == null || r.m_craftingStation == null || r.m_resources == null || r.m_resources.Length == 0) continue;
                    ItemDrop.ItemData.SharedData made = r.m_item.m_itemData.m_shared;
                    ItemDrop food = null, baked;
                    bool bakes = false;
                    if (made.m_food > 0f && made.m_itemType == ItemDrop.ItemData.ItemType.Consumable) food = r.m_item;
                    else if (b.Baked.TryGetValue(made.m_name, out baked)) { food = baked; bakes = true; }
                    if (food == null) continue;
                    b.Recipes.Add(new Made { R = r, Station = r.m_craftingStation.m_name, Level = Mathf.Max(1, r.m_minStationLevel), Food = food, Bakes = bakes });
                    b.Worth[NameOf(food)] = WorthOf(food.m_itemData);
                    b.Tools.Add(r.m_craftingStation.m_name);
                    if (bakes) b.Doughs.Add(made.m_name);
                }
                if (b.Worth.Count == 0) return b;
                _book = b; _bookWorld = ZNet.instance;
                Npcs.Log("kitchen: " + b.Worth.Count + " dishes in the game's lists (" + b.Baked.Count + " off a rack or an oven, " + b.Recipes.Count + " recipes at " + b.Tools.Count + " tools: " + string.Join(", ", new List<string>(b.Tools).ToArray()) + ")");
                return b;
            }
        }

        // a thing a kitchen makes to be eaten (what a base's people eat, what a cook carries to its pantry)
        internal static bool IsDish(string name) { return B.Worth.ContainsKey(name); }

        // ---------------------------------------------------------------- a kitchen: the tools round a pantry
        internal sealed class Kitchen { public Container Pantry; public readonly List<CookingStation> Racks = new List<CookingStation>(); public readonly List<CraftingStation> Tools = new List<CraftingStation>(); }

        internal static Kitchen Of(NpcWorker w, Container pantry)
        {
            if (w == null || pantry == null) return null;
            Kitchen k = new Kitchen { Pantry = pantry };
            Vector3 at = pantry.transform.position;
            Book b = B;
            foreach (CookingStation s in w.InBase<CookingStation>()) if (s != null && Utils.DistanceXZ(s.transform.position, at) <= Reach) k.Racks.Add(s);
            foreach (CraftingStation s in w.InBase<CraftingStation>()) if (s != null && b.Tools.Contains(s.m_name) && Utils.DistanceXZ(s.transform.position, at) <= Reach) k.Tools.Add(s);
            return k;
        }

        private static bool HasTool(Kitchen k, string station, int level)
        {
            foreach (CraftingStation s in k.Tools) if (s.m_name == station && s.GetLevel() >= level) return true;
            return false;
        }

        private static bool BakesHere(Kitchen k, string dough)
        {
            foreach (CookingStation s in k.Racks)
                foreach (CookingStation.ItemConversion c in s.m_conversion) if (c != null && c.m_from != null && c.m_to != null && NameOf(c.m_from) == dough) return true;
            return false;
        }

        // the dishes its tools can make (their fires, their roofs and what lies in its chest aside), each with its third: 0 simple · 1 middling · 2 best
        internal static Dictionary<string, int> Tiers(Kitchen k)
        {
            Dictionary<string, float> can = new Dictionary<string, float>();
            Book b = B;
            foreach (CookingStation s in k.Racks)
                foreach (CookingStation.ItemConversion c in s.m_conversion)
                    if (c != null && c.m_from != null && c.m_to != null && b.Worth.ContainsKey(NameOf(c.m_to)) && !b.Doughs.Contains(NameOf(c.m_from))) can[NameOf(c.m_to)] = b.Worth[NameOf(c.m_to)];
            foreach (Made m in b.Recipes)
                if (HasTool(k, m.Station, m.Level) && (!m.Bakes || BakesHere(k, NameOf(m.R.m_item)))) can[NameOf(m.Food)] = b.Worth[NameOf(m.Food)];
            List<KeyValuePair<string, float>> l = new List<KeyValuePair<string, float>>(can);
            l.Sort((x, y) => x.Value != y.Value ? x.Value.CompareTo(y.Value) : string.CompareOrdinal(x.Key, y.Key)); // (the same order on every game)
            Dictionary<string, int> tiers = new Dictionary<string, int>();
            for (int i = 0; i < l.Count; i++) tiers[l[i].Key] = i * 3 / l.Count;
            return tiers;
        }

        // ---------------------------------------------------------------- what it was told to cook
        internal static int Tier(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return Mathf.Clamp((z != null ? NpcLocker.Int(z, KTier, Best + 1) : Best + 1) - 1, 0, Best); }

        private static readonly string[] TierTitles = { Lang.K("Прості страви"), Lang.K("Середні страви"), Lang.K("Найкращі страви") };
        private static readonly string[] TierDescs =
        {
            Lang.K("Спершу найпростіше з того, що вміє кухня: інгредієнтів іде менше, а їжа скромніша."),
            Lang.K("Спершу страви середньої руки: ні найдешевші, ні найдорожчі."),
            Lang.K("Спершу найкраще, що можна зварити на цій кухні з того, що є."),
        };

        // its button in the cook's ring, and the ring under it: one of three, the one told lit
        internal static NpcMenu.Button Button()
        {
            return new NpcMenu.Button { Order = 99, Title = Lang.K("Що готувати"), Icon = "n-dishes", Shown = a => a.Worker != null && a.Worker.JobId == "cook",
                Desc = a => Lang.F("Зараз: {0}. Готує все, що лежить у його скрині; це лише каже, з чого починати. Решта піде на простіше.", Lang.T(TierTitles[Tier(a)]).ToLowerInvariant()),
                Sub = a => Ring() };
        }

        private static List<NpcMenu.Button> _ring;
        private static List<NpcMenu.Button> Ring()
        {
            if (_ring != null) return _ring;
            _ring = new List<NpcMenu.Button>();
            for (int i = 0; i <= Best; i++)
            {
                int t = i;
                _ring.Add(new NpcMenu.Button { Order = i, Title = TierTitles[t], Icon = "n-cook", Stay = true,
                    Desc = a => Lang.T(TierDescs[t]) + "\n" + Menu(a, t),
                    Now = a => Tier(a) == t, Do = (a, p) => NpcMenu.OnOwner(a, "cooktier", p.transform.position, t.ToString(System.Globalization.CultureInfo.InvariantCulture)) });
            }
            return _ring;
        }

        // the dishes of that third its kitchen can make, by name (under the pointer: only while its ring is open)
        private static string Menu(NpcActor a, int tier)
        {
            NpcWorker w = a.Worker;
            Kitchen k = w != null ? Of(w, w.Chest) : null;
            if (k == null) return Lang.T("Комори звідси не видно.");
            List<string> names = new List<string>();
            foreach (KeyValuePair<string, int> d in Tiers(k)) if (d.Value == tier) names.Add(Localization.instance != null ? Localization.instance.Localize(d.Key) : d.Key);
            if (names.Count == 0) return Lang.T("На цій кухні таких страв поки нема: бракує знаряддя.");
            names.Sort(StringComparer.CurrentCulture);
            int more = names.Count > 8 ? names.Count - 8 : 0;
            if (more > 0) names.RemoveRange(8, more);
            return Lang.F("На цій кухні це: {0}", string.Join(", ", names.ToArray())) + (more > 0 ? " " + Lang.F("і ще {0}", more) : "") + ".";
        }

        internal static void Deeds()
        {
            NpcMenu.Deeds["cooktier"] = (a, at, arg) =>
            {
                int t;
                string busy = NpcKit.Busy(a); if (busy != null) return busy;
                if (!int.TryParse(arg, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out t) || t < 0 || t > Best) return null;
                NpcLocker.Put(a.Zdo, KTier, t + 1);
                if (a.Worker != null) a.Worker.Reset();
                return Lang.F("Готуватиме: {0}.", Lang.T(TierTitles[t]).ToLowerInvariant());
            };
        }

        // ---------------------------------------------------------------- its next errand (where it is run)
        internal static NpcWorker.Errand Next(NpcWorker w)
        {
            Kitchen k = Of(w, w.ChestNow);
            if (k == null) return null;
            if (k.Racks.Count == 0 && k.Tools.Count == 0) { w.Why = Lang.K("біля комори нема кухонного знаряддя"); return null; }
            NpcWorker.Errand e = TakeOff(w, k); // (what is done first: it burns if left)
            if (e != null) return e;
            NpcWorker.Errand fire = Stoker.Fuel(w, Fires(w, k));
            if (fire != null && fire.Urgent) return fire; // (a fire of its kitchen about to go out)
            e = OvenFuel(w, k);
            if (e != null) return e;
            e = Cook(w, k);
            if (e != null) return e;
            if (fire == null) w.Why = Lang.K("нема з чого готувати");
            return fire; // (nothing to cook now: its fires kept up)
        }

        // the fires that burn under its tools (a rack over a hearth, a cauldron over a fire)
        private static List<Fireplace> Fires(NpcWorker w, Kitchen k)
        {
            List<Fireplace> l = new List<Fireplace>();
            foreach (Fireplace f in w.InBase<Fireplace>())
            {
                if (f == null) continue;
                bool by = false;
                foreach (CookingStation s in k.Racks) if (s.m_requireFire && Utils.DistanceXZ(s.transform.position, f.transform.position) <= FireNear) { by = true; break; }
                if (!by) foreach (CraftingStation s in k.Tools) if (s.m_craftRequireFire && Utils.DistanceXZ(s.transform.position, f.transform.position) <= FireNear) { by = true; break; }
                if (by) l.Add(f);
            }
            return l;
        }

        private static bool Done(CookingStation s, string item)
        {
            if (item.Length == 0) return false;
            if (s.m_overCookedItem != null && item == s.m_overCookedItem.name) return true;
            foreach (CookingStation.ItemConversion c in s.m_conversion) if (c != null && c.m_to != null && c.m_to.name == item) return true;
            return false;
        }

        private static bool FireLit(CookingStation s)
        {
            if (!s.m_requireFire) return true;
            try { return HarmonyLib.Traverse.Create(s).Method("IsFireLit").GetValue<bool>(); } catch { return false; } // quiet: the game's own check not found — as not lit, nothing put on it
        }

        // a rack or an oven that can cook now: its fire burns, or it has fuel of its own
        private static bool Hot(CookingStation s, ZDO z) { return s.m_useFuel ? z.GetFloat(ZDOVars.s_fuel) > 0f : FireLit(s); }

        // a crafting tool that can be worked at now: the game's own two wants (CraftingStation.CheckUsable asks them of a player: a roof over it, a fire under it)
        private static bool Usable(CraftingStation s)
        {
            if (s.m_craftRequireFire && !EffectArea.IsPointPlus025InsideBurningArea(s.transform.position)) return false;
            return !s.m_craftRequireRoof || Roofed(s);
        }
        private static bool Roofed(CraftingStation s)
        {
            if (s.m_roofCheckPoint == null) return true;
            float cover; bool under;
            Cover.GetCoverForPoint(s.m_roofCheckPoint.position, out cover, out under);
            return under && cover >= 0.7f;
        }

        private static NpcWorker.Errand TakeOff(NpcWorker w, Kitchen k)
        {
            foreach (CookingStation s in k.Racks)
            {
                ZNetView nv = s.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || s.m_slots == null) continue;
                ZDOID id = nv.GetZDO().m_uid;
                if (!Reserve.IsFree(id, w.Me)) continue;
                ZDO z = nv.GetZDO();
                int done = 0;
                for (int i = 0; i < s.m_slots.Length; i++) if (Done(s, z.GetString("slot" + i))) done++;
                if (done == 0) continue;
                HashSet<string> made = new HashSet<string>();
                foreach (CookingStation.ItemConversion c in s.m_conversion) if (c != null && c.m_to != null) made.Add(c.m_to.name);
                if (s.m_overCookedItem != null) made.Add(s.m_overCookedItem.name);
                int left = done;
                return new NpcWorker.Errand
                {
                    Target = id, At = s.transform.position, Near = 1.6f, Collect = made, Word = Lang.K("знімає готове"),
                    Use = (wk, e) => { if (left-- <= 0 || !nv.IsValid()) return true; nv.InvokeRPC("RPC_RemoveDoneItem", wk.Pos, 1); return false; }
                };
            }
            return null;
        }

        private static NpcWorker.Errand OvenFuel(NpcWorker w, Kitchen k)
        {
            foreach (CookingStation s in k.Racks)
            {
                if (!s.m_useFuel || s.m_fuelItem == null) continue;
                ZNetView nv = s.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                ZDO z = nv.GetZDO();
                ZDOID id = z.m_uid;
                if (!Reserve.IsFree(id, w.Me) || z.GetFloat(ZDOVars.s_fuel) > s.m_maxFuel - 2) continue;
                string fuel = NameOf(s.m_fuelItem);
                if (w.Carrying(fuel) + w.InChest(fuel) <= 0) continue;
                return new NpcWorker.Errand
                {
                    Target = id, At = s.transform.position, Near = 1.8f, Move = "feed_fire", Take = fuel, Count = s.m_maxFuel - Mathf.CeilToInt(z.GetFloat(ZDOVars.s_fuel)), Word = Lang.K("підкладає дрова в піч"),
                    Use = (wk, e) => { if (!nv.IsValid() || z.GetFloat(ZDOVars.s_fuel) > s.m_maxFuel - 1 || !wk.Spend(fuel)) return true; nv.InvokeRPC("RPC_AddFuel"); return false; }
                };
            }
            return null;
        }

        // how well a dish answers what it was told: the told third first, then the nearer one; in a third the better first (told «simple»: the plainer)
        private static float Rank(Dictionary<string, int> tiers, string dish, int told, bool begun)
        {
            int tier; float worth;
            if (!tiers.TryGetValue(dish, out tier)) tier = 0;
            if (!B.Worth.TryGetValue(dish, out worth)) worth = 0f;
            int off = told == Best ? Best - tier : told == 0 ? tier : tier == 1 ? 0 : tier == 0 ? 1 : 2;
            return (begun ? 100000f : 0f) + (Best - off) * 10000f + (told == 0 ? -worth : worth);
        }

        // what one making of a recipe takes, of what there is (null: not enough): each thing in full — or, for a recipe that wants one of several, the first there is
        private static List<KeyValuePair<string, int>> Needs(Recipe r, Func<string, int> have)
        {
            List<KeyValuePair<string, int>> l = new List<KeyValuePair<string, int>>();
            foreach (Piece.Requirement q in r.m_resources)
            {
                if (q == null || q.m_resItem == null || q.m_amount <= 0) continue;
                string name = NameOf(q.m_resItem);
                bool has = have(name) >= q.m_amount;
                if (r.m_requireOnlyOneIngredient) { if (has) { l.Add(new KeyValuePair<string, int>(name, q.m_amount)); return l; } continue; }
                if (!has) return null;
                l.Add(new KeyValuePair<string, int>(name, q.m_amount));
            }
            return !r.m_requireOnlyOneIngredient && l.Count > 0 ? l : null;
        }

        private static NpcWorker.Errand Cook(NpcWorker w, Kitchen k)
        {
            Book b = B;
            Dictionary<string, int> tiers = Tiers(k);
            int told = Tier(w.Actor);
            Func<string, int> have = n => w.Carrying(n) + w.InChest(n);
            float best = float.MinValue;
            Func<NpcWorker.Errand> make = null;
            // onto a rack or into an oven: a raw thing, or a dough it made
            foreach (CookingStation s in k.Racks)
            {
                ZNetView nv = s.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || s.m_slots == null) continue;
                ZDO z = nv.GetZDO();
                ZDOID id = z.m_uid;
                if (!Reserve.IsFree(id, w.Me) || !Hot(s, z)) continue;
                int free = 0;
                for (int i = 0; i < s.m_slots.Length; i++) if (z.GetString("slot" + i).Length == 0) free++;
                if (free == 0) continue;
                foreach (CookingStation.ItemConversion c in s.m_conversion)
                {
                    if (c == null || c.m_from == null || c.m_to == null || !b.Worth.ContainsKey(NameOf(c.m_to)) || have(NameOf(c.m_from)) <= 0) continue;
                    float rank = Rank(tiers, NameOf(c.m_to), told, b.Doughs.Contains(NameOf(c.m_from)));
                    if (rank <= best) continue;
                    best = rank;
                    CookingStation.ItemConversion cv = c; CookingStation st = s; int room = free;
                    make = () => Onto(st, nv, z, id, cv, room);
                }
            }
            // at a tool: a recipe whose things are all there
            foreach (Made m in b.Recipes)
            {
                if (m.Bakes && !BakesHere(k, NameOf(m.R.m_item))) continue;
                float rank = Rank(tiers, NameOf(m.Food), told, false);
                if (rank <= best) continue;
                CraftingStation tool = null;
                foreach (CraftingStation s in k.Tools)
                {
                    if (s.m_name != m.Station) continue;
                    ZNetView nv = s.GetComponent<ZNetView>();
                    if (nv == null || !nv.IsValid() || !Reserve.IsFree(nv.GetZDO().m_uid, w.Me) || s.GetLevel() < m.Level || !Usable(s)) continue;
                    tool = s; break;
                }
                if (tool == null) continue;
                List<KeyValuePair<string, int>> need = Needs(m.R, have);
                if (need == null) continue;
                best = rank;
                Made md = m; CraftingStation at = tool;
                make = () => Craft(md, at, need);
            }
            return make != null ? make() : null;
        }

        private static NpcWorker.Errand Onto(CookingStation s, ZNetView nv, ZDO z, ZDOID id, CookingStation.ItemConversion c, int free)
        {
            string raw = NameOf(c.m_from), prefab = c.m_from.name;
            return new NpcWorker.Errand
            {
                Target = id, At = s.transform.position, Near = 1.6f, Take = raw, Count = free, Word = B.Doughs.Contains(raw) ? Lang.K("ставить у піч") : Lang.K("кладе м'ясо"), Move = "feed_fire",
                Use = (wk, e) =>
                {
                    bool room = false;
                    for (int i = 0; i < s.m_slots.Length; i++) if (z.GetString("slot" + i).Length == 0) { room = true; break; }
                    if (!nv.IsValid() || !room || !wk.Spend(raw)) return true;
                    nv.InvokeRPC("RPC_AddItem", prefab, false);
                    return false;
                }
            };
        }

        // a dish made at a tool: its things in its hands, CraftSeconds of its steps at the tool, then they are gone and the dish is in its hands — to its pantry at once;
        // a dough stays in its hands for the oven. The tool's fire out meanwhile: left off, its things go back with its next walk to the chest
        private static NpcWorker.Errand Craft(Made m, CraftingStation tool, List<KeyValuePair<string, int>> need)
        {
            ZNetView nv = tool.GetComponent<ZNetView>();
            int steps = Mathf.Max(2, Mathf.CeilToInt(CraftSeconds / 0.7f));
            NpcWorker.Errand er = new NpcWorker.Errand
            {
                Target = nv.GetZDO().m_uid, At = tool.transform.position, Near = 1.8f, Also = need, Word = Lang.K("готує страву"), Move = tool.m_name == Cauldron ? "stir" : null,
            };
            er.Use = (wk, e) =>
            {
                if (tool == null || !Usable(tool)) return true;
                tool.PokeInUse(); // (the tool's own «at work» look, as under a player's hands)
                if (--steps > 0) return false;
                foreach (KeyValuePair<string, int> t in need) if (wk.Carrying(t.Key) < t.Value) return true; // (taken out of its hands meanwhile: nothing made of nothing)
                int amount = m.R.m_amount;
                if (m.R.m_requireOnlyOneIngredient) // (the game's own count: Recipe.GetAmount — by the quality of the one thing, and that thing's own extra)
                {
                    foreach (Piece.Requirement q in m.R.m_resources) if (q != null && q.m_resItem != null && NameOf(q.m_resItem) == need[0].Key) { amount += q.m_extraAmountOnlyOneIngredient; break; }
                    amount += Mathf.CeilToInt((wk.QualityOf(need[0].Key) - 1) * m.R.m_amount * m.R.m_qualityResultAmountMultiplier);
                }
                foreach (KeyValuePair<string, int> t in need) for (int i = 0; i < t.Value; i++) wk.Spend(t.Key);
                wk.Receive(m.R.m_item.gameObject, Mathf.Max(1, amount));
                tool.m_craftItemDoneEffects.Create(tool.transform.position, tool.transform.rotation);
                e.Made = !m.Bakes; // (a dish: to its pantry; a dough: on to the oven in its hands)
                return true;
            };
            return er;
        }

        // ---------------------------------------------------------------- what its kitchen lacks (its card, the board: any game, only while shown)
        internal static void Lacks(NpcWorker w, List<string> into)
        {
            Container pantry = w.Chest;
            if (pantry == null) return; // (not to be seen from this game: nothing said)
            Kitchen k = Of(w, pantry);
            if (k.Racks.Count == 0 && k.Tools.Count == 0) { into.Add(Lang.F("біля комори (до {0} м) нема кухонного знаряддя: решітки, печі чи казана", Mathf.RoundToInt(Reach))); return; }
            Book b = B;
            int cold = 0, warm = 0;
            foreach (CookingStation s in k.Racks) { ZNetView nv = s.GetComponent<ZNetView>(); if (nv != null && nv.IsValid() && Hot(s, nv.GetZDO())) warm++; else cold++; }
            foreach (CraftingStation s in k.Tools)
            {
                if (Usable(s)) { warm++; continue; }
                cold++;
                string name = Localization.instance != null ? Localization.instance.Localize(s.m_name) : s.m_name;
                into.Add(s.m_craftRequireRoof && !Roofed(s) ? Lang.F("{0}: потрібен дах над ним", name) : Lang.F("{0}: потрібен вогонь під ним", name));
            }
            if (warm == 0 && cold > 0 && into.Count == 0) into.Add(Lang.T("на кухні ніде не горить вогонь")); // (only racks, all cold: said once — a tool's own want is said by its name above)
            Container src = w.Source;
            Inventory inv = src != null ? src.GetInventory() : null;
            if (inv == null) return;
            Func<string, int> have = n => inv.CountItems(n);
            bool any = false;
            foreach (CookingStation s in k.Racks)
                foreach (CookingStation.ItemConversion c in s.m_conversion) if (c != null && c.m_from != null && c.m_to != null && b.Worth.ContainsKey(NameOf(c.m_to)) && have(NameOf(c.m_from)) > 0) any = true;
            List<string> want = new List<string>();
            foreach (Made m in b.Recipes)
            {
                if (Needs(m.R, have) == null) continue;
                if (HasTool(k, m.Station, m.Level) && (!m.Bakes || BakesHere(k, NameOf(m.R.m_item)))) { any = true; continue; }
                string tool = (Localization.instance != null ? Localization.instance.Localize(m.Station) : m.Station) + (m.Level > 1 ? " " + Lang.F("(рівень {0})", m.Level) : "");
                if (m.Bakes && HasTool(k, m.Station, m.Level)) tool = Lang.T("піч");
                if (!want.Contains(tool) && want.Count < 3) want.Add(tool);
            }
            if (want.Count > 0) into.Add(Lang.F("з цих інгредієнтів вийшло б більше, якби на кухні було: {0}", string.Join(", ", want.ToArray())));
            if (!any) into.Add(w.Supply != null ? Lang.T("у скрині інгредієнтів нема з чого готувати") : Lang.T("у коморі нема сирого: поклади туди або дай окрему скриню інгредієнтів"));
        }
    }
}
#endif
