#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Примхи»: what each quirk of the norov (Norov.QuirkList) does. One per raven; trust does not change them.
    // The owner's game runs them (Tick from Shoulder.Tick, twice a second, and hooks in the gifts, orders, feeding,
    // strikes and outings). A gloomy raven (sulking, freezing, afraid) droops on every game: a flag on the player.
    // A quirk never touches «Повернись», and the passive gifts keep working.
    internal static class Quirks
    {
        internal const string SulkKey = "RavenWatch_sulk", DislikeKey = "RavenWatch_dislike", DislikeKnownKey = "RavenWatch_dislikeKnown";
        internal const string GloomKeyH = "RavenWatch_GloomH", GloomKeyM = "RavenWatch_GloomM";

        internal static float ThiefDaysMin = 1f, ThiefDaysMax = 2f;   // game days between two thefts of a full raven
        internal static float ThiefCoinChance = 0.5f, ThiefCoins = 10f; // coins or the best food: half and half; 10 coins
        internal static float GrumbleMin = 15f, GrumbleMax = 25f;     // minutes between grumbles
        internal static float WetRainSeconds = 20f, WetRest = 300f;   // in the rain this long, then under a roof: a shake; not twice in 5 min
        internal static float WetSeconds = 30f;
        internal static float LoudMin = 40f, LoudMax = 90f, LoudNoise = 30f; // s of sneaking between caws; m the enemies hear it
        internal static float ShowoffDelay = 8f;
        internal static float SulkSeconds = 300f;
        internal static float WillfulChance = 0.1f;
        internal static float MagpieChance = 0.3f;
        internal static readonly string[] Junk = { "Stone", "Wood", "Resin", "BoneFragments", "Flint", "Feathers" };
        internal static float TimidHealth = 0.75f; // a boss above this share of its health scares it
        internal static float ChillyRest = 600f;   // s between two complaints about the cold

        private static ItemDrop.ItemData Item(Player p, bool munin) { return p != null ? RavenSeat.On(p, munin) : null; }

        // the worn raven of yours with this quirk, awake, yours, here (null: none)
        private static ItemDrop.ItemData With(Player p, bool munin, string id)
        {
            ItemDrop.ItemData item = Item(p, munin);
            if (item == null || !Norov.HasQuirk(item, id) || !Trust.Active(item) || Trust.Stranger(item, p) || Trust.Away(item)) return null;
            if ((id == "chilly" || id == "wet") && GearFx.ColdProof(item)) return null; // «Холодний медальйон»: no cold, no rain
            return item;
        }

        private static string Name(bool munin) { return Plugin.RavenName(munin); }
        private static void Corner(Player p, bool munin, string text) { if (p != null) p.Message(MessageHud.MessageType.TopLeft, text, 0, RavenItem.Icon(munin)); }

        // a line from the phrase files, said from the shoulder (values: "food", "...", ...)
        private static bool Say(Player p, bool munin, string tag, params string[] values)
        {
            string line = Satiety.Say(p, munin, tag, values);
            if (string.IsNullOrEmpty(line)) return false;
            RavenSkills.SayFromShoulder(p, munin, line);
            return true;
        }

        // its own idea (not an answer to you): only when the dialogue director lets it
        private static bool SayOwn(Player p, bool munin, string tag, Director.Voice v)
        {
            if (!Director.May(v)) return false;
            if (!Say(p, munin, tag)) return false;
            Director.Spoke(v, 4f);
            return true;
        }

        // ---------------------------------------------------------------- rolled (Norov.Ensure)

        // «Перебірливий»: in every class of its food about a third it will not touch (PickyShare), but at least
        // PickyKeep of each class stay, so every trust level still has food; never its favourite
        internal static float PickyShare = 0.34f;
        internal static int PickyKeep = 2;

        internal static void Rolled(ItemDrop.ItemData item, bool munin)
        {
            if (!Norov.HasQuirk(item, "picky")) { item.m_customData.Remove(DislikeKey); item.m_customData.Remove(DislikeKnownKey); return; }
            string fav = Satiety.FavOf(item);
            List<string> cut = new List<string>();
            for (int cls = 1; cls <= 5; cls++)
            {
                List<Trust.Food> pool = new List<Trust.Food>();
                int all = 0;
                foreach (Trust.Food f in Trust.Foods(munin)) if (f.Class == cls) { all++; if (f.Prefab != fav) pool.Add(f); }
                int n = Mathf.Min(Mathf.RoundToInt(all * PickyShare), all - PickyKeep, pool.Count);
                for (int k = 0; k < n; k++) { int j = Random.Range(0, pool.Count); cut.Add(pool[j].Prefab); pool.RemoveAt(j); }
            }
            item.m_customData[DislikeKey] = string.Join(",", cut.ToArray());
            item.m_customData.Remove(DislikeKnownKey);
        }

        // for the book's refresh: what may change on the card
        internal static string Code(ItemDrop.ItemData item)
        {
            return Mathf.CeilToInt(SulkLeft(item) / 60f) + ":" + (Read(item, DislikeKnownKey) ?? "");
        }

        private static List<string> ListOf(ItemDrop.ItemData item, string key)
        {
            string v = Read(item, key);
            List<string> r = new List<string>();
            if (!string.IsNullOrEmpty(v)) foreach (string s in v.Split(',')) if (s.Length > 0) r.Add(s);
            return r;
        }

        private static string Read(ItemDrop.ItemData item, string key) { return Norov.Read(item, key); }

        // the disliked list, parsed once per value (EdibleFoods asks it for every food, the book and the radial often)
        private static string _dislikeRaw;
        private static HashSet<string> _dislikeSet = new HashSet<string>();
        private static HashSet<string> DislikeSet(ItemDrop.ItemData item)
        {
            string raw = Read(item, DislikeKey) ?? "";
            if (raw != _dislikeRaw) { _dislikeRaw = raw; _dislikeSet = new HashSet<string>(ListOf(item, DislikeKey)); }
            return _dislikeSet;
        }

        // more for the book's tooltip: the disliked food once known, the sulk's time left
        internal static string Note(ItemDrop.ItemData item, bool munin)
        {
            string s = "";
            if (Norov.HasQuirk(item, "picky"))
            {
                List<string> all = ListOf(item, DislikeKey), known = ListOf(item, DislikeKnownKey);
                string names = "";
                foreach (string k in known) if (all.Contains(k)) names += (names.Length > 0 ? ", " : "") + FoodName(munin, k);
                int unknown = 0;
                foreach (string d in all) if (!known.Contains(d)) unknown++;
                s += Lang.F("\n\nНе їсть: {0}{1}.", (names.Length > 0 ? "<color=#FF6B6B>" + names + "</color>" : Lang.T("ще не знаєш, що саме")), (unknown > 0 ? Lang.F(" (і ще {0}, про які ти не знаєш)", Lang.N(unknown, "страву|страви|страв")) : ""));
            }
            float left = SulkLeft(item);
            if (left > 0f) s += Lang.F("\n\n<color=#FF6B6B>Зараз ображений: ще {0}.</color>", RavenAbilities.Span(left));
            return s;
        }

        private static string FoodName(bool munin, string prefab)
        {
            foreach (Trust.Food f in Trust.Foods(munin)) if (f.Prefab == prefab) return f.Name;
            return prefab;
        }

        // ---------------------------------------------------------------- «Ревнивий»: sulking after the other one was fed

        // (asked for every gift in the radial and the book every frame: read once a frame per raven)
        private static int _sulkFrame = -1;
        private static ItemDrop.ItemData _sulkItem;
        private static float _sulkLeft;
        internal static float SulkLeft(ItemDrop.ItemData item)
        {
            if (item == null || item.m_customData == null) return 0f;
            if (_sulkFrame == Time.frameCount && ReferenceEquals(_sulkItem, item)) return _sulkLeft;
            _sulkFrame = Time.frameCount; _sulkItem = item;
            _sulkLeft = item.m_customData.ContainsKey(SulkKey) ? WorldTime.Left(item.m_customData, SulkKey) : 0f;
            return _sulkLeft;
        }

        // why this raven of yours does nothing now (null: it is not sulking)
        internal static string Sulk(Player p, bool munin)
        {
            float left = SulkLeft(Item(p, munin));
            return left > 0f ? Lang.F("{0} ображений — ще {1} нічого не робитиме", Name(munin), RavenAbilities.Span(left)) : null;
        }

        // you fed this raven: the other one, if jealous, takes offence
        internal static void Fed(Player p, bool munin)
        {
            ItemDrop.ItemData other = With(p, !munin, "jealous");
            if (other == null || SulkLeft(other) > 0f) return;
            WorldTime.Set(other.m_customData, SulkKey, SulkSeconds);
            _sulkFrame = -1; // read anew
            _sulking[!munin ? 1 : 0] = true;
            Corner(p, !munin, Lang.F("{0} ревнує: {1} хв ні до чого себе не підпустить", Name(!munin), Mathf.RoundToInt(SulkSeconds / 60f)));
            Say(p, !munin, "quirk_jealous");
        }

        // ---------------------------------------------------------------- «Перебірливий»: the disliked food

        // it will not eat this (its favourite it always eats)
        internal static bool Dislikes(ItemDrop.ItemData item, string prefab)
        {
            return prefab != null && Norov.HasQuirk(item, "picky") && DislikeSet(item).Contains(prefab) && !Satiety.IsFav(item, prefab);
        }

        // offered what it will not eat: refused (the food stays with you), now you know, and it grumbles
        internal static string PickyRefuses(Player p, bool munin, ItemDrop.ItemData item, Trust.Food food)
        {
            if (food == null || !Dislikes(item, food.Prefab)) return null;
            List<string> known = ListOf(item, DislikeKnownKey);
            if (!known.Contains(food.Prefab)) { known.Add(food.Prefab); item.m_customData[DislikeKnownKey] = string.Join(",", known.ToArray()); }
            Say(p, munin, "quirk_picky", "food", food.Name);
            return Lang.F("{0} не їсть: {1}. Перебірливий — дай щось інше.", Name(munin), food.Name);
        }

        // ---------------------------------------------------------------- the gates (gifts, orders, outings)

        private static bool NightNow() { return Senses.Night; } // (Senses: EnvMan.IsNight, twice a second)

        // a gift of this raven cannot be used now because of its quirk (null: it can)
        internal static string Blocks(Player p, bool munin, string ability)
        {
            string s = Sulk(p, munin);
            if (s != null) return s;
            if (ability == "scout" && With(p, munin, "sleepy") != null && NightNow()) return Lang.F("{0} спить: уночі на розвідку не полетить", Name(munin));
            return null;
        }

        // an order (Orders.Why): the same, short
        // asleep at night (sleepy), so no scouting: the reason OrderBlock gives (and not sulking first)
        internal static bool AsleepForScout(Player p, bool munin) { return SulkLeft(Item(p, munin)) <= 0f && With(p, munin, "sleepy") != null && NightNow(); }

        internal static string OrderBlock(Player p, bool munin, int order)
        {
            float left = SulkLeft(Item(p, munin));
            if (left > 0f) return Lang.F("ображений, ще {0}", RavenAbilities.Span(left));
            if (order == Orders.Scout && With(p, munin, "sleepy") != null && NightNow()) return Lang.T("ніч — спить, на розвідку не полетить");
            return null;
        }

        // the ravens' own outings: not when sulking, asleep at night, freezing
        internal static bool KeepsHome(Player p, bool munin)
        {
            if (SulkLeft(Item(p, munin)) > 0f) return true;
            if (With(p, munin, "sleepy") != null && NightNow()) return true;
            if (With(p, munin, "chilly") != null && Cold(p)) return true;
            return false;
        }

        // a refusal before it is said (the gift's message, the order's corner line): its own words now and then
        private static float _gateSaidAt = -100f;
        internal static void SaySleepy(Player p, bool munin) { if (Time.time - _gateSaidAt > 20f) { _gateSaidAt = Time.time; Say(p, munin, "quirk_sleepy"); } }

        // ---------------------------------------------------------------- «Свавільний»: the order not done (strength spent)

        internal static bool Willful(Player p, bool munin, int order)
        {
            if (With(p, munin, "willful") == null || Random.value >= WillfulChance) return false;
            Corner(p, munin, Lang.F("{0}: «{1}» — не захотів (сила витрачена)", Name(munin), Orders.Names[order]));
            Say(p, munin, "quirk_willful");
            return true;
        }

        // ---------------------------------------------------------------- «Боязкий»: bosses

        private static readonly HashSet<ZDOID> Braved = new HashSet<ZDOID>(); // bosses it has already struck bravely

        private static bool Scary(Character c) { return c != null && c.IsBoss() && !c.IsDead() && c.GetHealthPercentage() > TimidHealth; }

        // a strike at a boss still fresh: it will not go (null: it will)
        internal static string TimidRefuses(Player p, bool munin, List<Character> targets)
        {
            if (With(p, munin, "timid") == null || targets == null || targets.Count == 0 || !Scary(targets[0])) return null;
            if (Time.time - _gateSaidAt > 6f) { _gateSaidAt = Time.time; Say(p, munin, "quirk_timid"); }
            return Lang.F("{0} боїться: спершу підрань боса (ще {1}% здоров'я до відваги)", Name(munin), Mathf.RoundToInt((targets[0].GetHealthPercentage() - TimidHealth) * 100f));
        }

        // its first blow at a wounded boss is a sure crit (marked once per boss)
        internal static bool BraveCrit(Player p, bool munin, Character t)
        {
            if (With(p, munin, "timid") == null || t == null || !t.IsBoss() || Scary(t)) return false;
            ZDOID id = t.GetZDOID();
            if (Braved.Contains(id)) return false;
            Braved.Add(id);
            Say(p, munin, "quirk_timid_brave");
            return true;
        }

        // ---------------------------------------------------------------- «Задавака»: after a crit or a kill

        internal static void StrikeHome(Player p, bool munin, bool crit, bool kill)
        {
            if ((!crit && !kill) || With(p, munin, "showoff") == null) return;
            RavenSkills.Delay(munin ? "mstrike" : "hstrike", ShowoffDelay);
            Corner(p, munin, Lang.F("{0} хизується — наступний удар на {1} с пізніше", Name(munin), Mathf.RoundToInt(ShowoffDelay)));
            _showoffSay[munin ? 1 : 0] = Time.time + 2.5f; // after its strike line has been read
        }
        private static readonly float[] _showoffSay = { -1f, -1f };

        // the raven on the shoulder plays its quirk's act (the sitting system), if it sits there
        internal static void Act(Player p, bool munin, string template)
        {
            GameObject model = Shoulder.ModelOf(p, munin);
            ShoulderLife life = model != null ? model.GetComponent<ShoulderLife>() : null;
            if (life != null) life.Act(template);
        }

        // ---------------------------------------------------------------- «Сорока»: back from an outing with junk

        internal static void OutingDone(Player p, bool munin)
        {
            if (With(p, munin, "magpie") == null || Random.value >= MagpieChance || ObjectDB.instance == null) return;
            GameObject prefab = ObjectDB.instance.GetItemPrefab(Junk[Random.Range(0, Junk.Length)]);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null || !p.GetInventory().AddItem(prefab, 1)) return; // no room: it drops it on the way
            string name = Localization.instance != null ? Lang.Game(drop.m_itemData.m_shared.m_name) : prefab.name;
            Corner(p, munin, Lang.F("{0} приніс тобі: {1}", Name(munin), name));
            Say(p, munin, "quirk_magpie", "item", name);
        }

        // ---------------------------------------------------------------- the weather («Мерзляк», «Мокрий»)

        private static string Env()
        {
            EnvSetup e = EnvMan.instance != null ? EnvMan.instance.GetCurrentEnvironment() : null;
            return e != null && e.m_name != null ? e.m_name.ToLowerInvariant() : "";
        }
        internal static bool Raining() { string n = Env(); return n.Contains("rain") || n.Contains("thunder") || n.Contains("storm"); }
        internal static bool Snowing() { string n = Env(); return n.Contains("snow") || n.Contains("blizzard"); }
        private static bool Outside() { return !Senses.Indoors && !Senses.Underground; }

        // cold for a chilly raven: rain or snow outside, or out in the Mountains or the Deep North
        internal static bool Cold(Player p)
        {
            if (!Outside()) return false;
            return Raining() || Snowing() || Senses.Biome == Heightmap.Biome.Mountain || Senses.Biome == Heightmap.Biome.DeepNorth;
        }

        // ---------------------------------------------------------------- the tick

        private static readonly float[] _thiefAt = { -1f, -1f }, _grumbleAt = { -1f, -1f }, _loudAt = { -1f, -1f };
        private static readonly float[] _wetRain = { 0f, 0f }, _wetShakeAt = { -1f, -1f }, _wetLast = { -1000f, -1000f };
        private static readonly bool[] _swam = new bool[2], _sulking = new bool[2], _cold = new bool[2];
        private static readonly float[] _chillySaid = { -1000f, -1000f };

        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            for (int i = 0; i < 2; i++) TickRaven(p, i == 1);
        }

        private static void TickRaven(Player p, bool munin)
        {
            int i = munin ? 1 : 0;
            ItemDrop.ItemData item = Item(p, munin);
            Norov.Quirk q = item != null ? Norov.QuirkOf(item) : null;
            bool mine = q != null && Trust.Active(item) && !Trust.Stranger(item, p) && !Trust.Away(item);
            string off;
            bool onShoulder = mine && Shoulder.Arrived(p, munin) && !RavenSkills.OffShoulder(p, munin, out off); // (not while it is still flying in)
            float now = Time.time;

            // the sulk: ends by itself (a word when it is over)
            float sulk = mine ? SulkLeft(item) : 0f;
            if (_sulking[i] && sulk <= 0f) { _sulking[i] = false; if (mine && !RavenSkills.Hushed(p)) Say(p, munin, "quirk_jealous_end"); }
            if (sulk > 0f) _sulking[i] = true;

            bool gloom = sulk > 0f;
            if (!mine) { NetFlag.Set(p, munin ? GloomKeyM : GloomKeyH, false); return; }

            switch (q.Id)
            {
                case "thief": Thief(p, munin, item, onShoulder, now); break;
                case "grumbler":
                    if (_grumbleAt[i] < 0f) _grumbleAt[i] = now + Random.Range(GrumbleMin, GrumbleMax) * 60f;
                    else if (now >= _grumbleAt[i] && onShoulder && !Senses.Fight && sulk <= 0f)
                    {
                        if (SayOwn(p, munin, "quirk_grumble", Director.Voice.Shoulder)) { _grumbleAt[i] = now + Random.Range(GrumbleMin, GrumbleMax) * 60f; Act(p, munin, Lang.T("Бурчить")); }
                        else _grumbleAt[i] = now + 60f;
                    }
                    break;
                case "wet": if (!GearFx.ColdProof(item)) Wet(p, munin, onShoulder, now); break; // (Холодний медальйон: not)
                case "loud":
                    if (!p.IsCrouching() || !onShoulder) { _loudAt[i] = -1f; break; }
                    if (_loudAt[i] < 0f) _loudAt[i] = now + Random.Range(LoudMin, LoudMax);
                    else if (now >= _loudAt[i])
                    {
                        _loudAt[i] = now + Random.Range(LoudMin, LoudMax);
                        ScreamFx.Caw(p, munin, munin ? 0.85f : 1.3f, 1f);
                        p.AddNoise(LoudNoise);
                        if (!RavenSkills.Hushed(p)) Say(p, munin, "quirk_loud"); // its own noise: not over a talk
                    }
                    break;
                case "chilly":
                    bool cold = Cold(p) && !GearFx.ColdProof(item); // (Холодний медальйон: never cold)
                    if (cold && onShoulder) gloom = true;
                    if (cold && !_cold[i] && onShoulder && now - _chillySaid[i] > ChillyRest && SayOwn(p, munin, "quirk_chilly", Director.Voice.Event)) _chillySaid[i] = now;
                    _cold[i] = cold;
                    break;
                case "timid":
                    if (Scary(Senses.Boss)) gloom = true;
                    break;
                case "showoff":
                    if (_showoffSay[i] > 0f && now >= _showoffSay[i]) { _showoffSay[i] = -1f; if (!RavenSkills.Hushed(p)) Say(p, munin, "quirk_showoff"); Act(p, munin, Lang.T("Хизується")); }
                    break;
            }
            NetFlag.Set(p, munin ? GloomKeyM : GloomKeyH, gloom);
        }

        // «Злодюжка»: full and still it helps itself, now and then
        private static void Thief(Player p, bool munin, ItemDrop.ItemData item, bool onShoulder, float now)
        {
            int i = munin ? 1 : 0;
            if (_thiefAt[i] < 0f) { _thiefAt[i] = now + Random.Range(ThiefDaysMin, ThiefDaysMax) * Power.GameDaySeconds() * GearFx.StealWaitMul(item); return; }
            if (now < _thiefAt[i] || !onShoulder || SulkLeft(item) > 0f) return;
            if (Satiety.Zone(item) <= Satiety.Hungry) return; // hungry: it steals the usual way (Satiety)
            _thiefAt[i] = now + Random.Range(ThiefDaysMin, ThiefDaysMax) * Power.GameDaySeconds() * GearFx.StealWaitMul(item); // (Монета на удачу: less often)
            // the best food in the bag (highest class, then the most nourishing), or coins: a coin toss; none of the one: the other
            Trust.Food pick = null;
            foreach (Trust.Food f in Trust.Foods(munin))
                if (Trust.Have(p, f.Prefab) > 0 && (pick == null || f.Class > pick.Class || (f.Class == pick.Class && f.Points > pick.Points))) pick = f;
            bool coinsHere = Trust.Have(p, "Coins") > 0;
            if (coinsHere && (pick == null || Random.value < ThiefCoinChance))
            {
                int n = Mathf.Min(Mathf.RoundToInt(ThiefCoins), Trust.Have(p, "Coins"));
                p.GetInventory().RemoveItem(Trust.FindName(p, "Coins"), n, -1, false);
                string coins = Lang.N(n, "монету|монети|монет");
                Corner(p, munin, Lang.F("{0} поцупив {1}", Name(munin), coins));
                Say(p, munin, "quirk_thief_coins", "coins", coins);
                Nicks.OnTheft(p, munin);
                return;
            }
            if (pick == null) return; // nothing worth taking
            p.GetInventory().RemoveItem(Trust.FindName(p, pick.Prefab), 1, -1, false);
            Corner(p, munin, Lang.F("{0} поцупив із торби: {1}", Name(munin), pick.Name));
            Say(p, munin, "quirk_thief", "food", pick.Name);
            Nicks.OnTheft(p, munin);
        }

        // «Мокрий»: out of the water, or in from the rain: a shake, on you
        private static void Wet(Player p, bool munin, bool onShoulder, float now)
        {
            int i = munin ? 1 : 0;
            if (Senses.Swimming) _swam[i] = true;
            if (Raining() && Outside()) _wetRain[i] += 0.5f;
            bool due = (_swam[i] && !Senses.Swimming) || (_wetRain[i] >= WetRainSeconds && (!Outside() || !Raining()));
            if (due && _wetShakeAt[i] < 0f) _wetShakeAt[i] = now + 1.5f;
            if (!Raining() && Outside()) _wetRain[i] = 0f;
            if (_wetShakeAt[i] < 0f || now < _wetShakeAt[i]) return;
            _wetShakeAt[i] = -1f; _swam[i] = false; _wetRain[i] = 0f;
            if (!onShoulder || now - _wetLast[i] < WetRest) return;
            _wetLast[i] = now;
            GameObject model = Shoulder.ModelOf(p, munin);
            ShoulderLife life = model != null ? model.GetComponent<ShoulderLife>() : null;
            if (life != null) life.ShakeNow();
            StatusEffect se = p.GetSEMan().AddStatusEffect("Wet".GetStableHashCode(), true);
            if (se != null) se.m_ttl = WetSeconds;
            if (!RavenSkills.Hushed(p)) Say(p, munin, "quirk_wet");
        }
    }
}
#endif
