#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // What the things from the raven tables give (RavenGear, Outfits): one effect each, on the raven that wears it.
    // Each thing fits both ravens; where an effect touches a gift, it is that raven's own gift (the spiral on Hugin
    // makes his dome last, on Munin her healing quicker). Here are the numbers and the small parts of their own
    // (the bell's warning, the words when a thing is put on); the rest is asked from where it is used.
    internal static class GearFx
    {
        internal const string Collar = "zakhysnyi_osheinyk", Bell = "dzvinkyi_oshyinyk",
            Vanguard = "kaptur_avanhardu", ClawNecklace = "kihtiaste_namysto", Coin = "moneta_na_udachu", Frost = "kholodnyi_medalion",
            Mosquito = "kihti_komara", Spiral = "spiral_zakhystu",
            Amulet = "koloskovyi_oberih", Harness = "zbruia_hintsia", Anklet = "vitriana_pidkova", Saddle = "sidlo_vishchuna",
            SilverRings = "sribni_kiltsia", BoneBracelets = "kistiani_braslety", Wraps = "shkiriani_obmotky",
            Shell = "pantsyrnyi_shchytok", Forager = "torba_zbyracha", Pendant = "eitrova_pidviska";

        // the things of the second (Mistlands) table: the raven lets itself be given them from this trust level on
        internal const int HighLevel = 5;
        private static readonly string[] High = { Vanguard, Mosquito, Frost, Spiral, Anklet, Saddle, Shell, Forager, Pendant };
        internal static int WearLevel(string key)
        {
            string id = IdOf(key);
            return id != null && System.Array.IndexOf(High, id) >= 0 ? HighLevel : Outfits.WardrobeLevel;
        }

        // the numbers
        internal static float CollarDeathMul = 0.5f, CollarLoyalPts = 20f;
        internal static float BellRadius = 30f, BellRest = 20f, BellScreamMul = 1.25f;
        internal static float ClawDamagePct = 10f, ClawLowHpPct = 15f, ClawLowHp = 0.3f, ClawFuryMul = 1.2f;
        internal static float CoinCritPts = 5f, CoinStealMul = 1.25f; // steals 20% less often: the waits ×1.25
        internal static float FrostShare = 0.2f;                       // of each blow, frost (the game's frost slows)
        internal static float MosquitoCooldownMul = 0.85f, MosquitoRangePct = 10f;
        internal static float SpiralMul = 1.25f, SpiralCooldownMul = 0.85f;
        internal static float AmuletHungerMul = 0.75f, HarnessStrengthMul = 1.2f, AnkletFlyMul = 1.1f, SaddleTrustMul = 1.25f;
        internal static float CoverPct = 20f; // the vanguard hood: less damage from enemies while it is off doing something
        internal static float SilverUndeadPct = 50f, BoneDamagePct = 5f, WrapsNoiseMul = 0.7f, WrapsSeenMul = 0.75f;
        internal static float ShellChance = 0.1f, ForagerChance = 0.15f, PendantCostMul = 0.85f;

        private static string IdOf(string key) { return key != null && key.StartsWith(RavenGear.KeyPrefix, System.StringComparison.Ordinal) ? key.Substring(RavenGear.KeyPrefix.Length) : null; }

        // this raven wears that thing (and may: Outfits.WornOpen), given from the bag: an admin's try-on in the fitting
        // (not paid for) only shows it, it gives nothing
        internal static bool Has(ItemDrop.ItemData raven, string id)
        {
            if (raven == null) return false;
            string key = RavenGear.KeyPrefix + id;
            for (int s = Outfits.Neck; s < Outfits.Slots; s++) if (Outfits.WornOpen(raven, s) == key && Outfits.Paid(raven, s)) return true;
            return false;
        }

        // the worn raven of yours, at home and yours, wears it
        internal static bool On(bool munin, string id)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            Player p = Player.m_localPlayer;
            return item != null && !Trust.Away(item) && (p == null || !Trust.Stranger(item, p)) && Has(item, id);
        }

        // ---------------------------------------------------------------- the knobs asked from elsewhere

        // Захисний ошейник: your death costs less trust; «Вірність» is likelier
        internal static float DeathMul(ItemDrop.ItemData raven) { return Has(raven, Collar) ? CollarDeathMul : 1f; }
        internal static float LoyalChance(ItemDrop.ItemData raven, float chance) { return Has(raven, Collar) ? Mathf.Clamp01(chance + CollarLoyalPts / 100f) : chance; }

        // Дзвінкий ошийник on Munin: the scream reaches further
        internal static float ScreamMul() { return On(true, Bell) ? BellScreamMul : 1f; }

        // the strike's blow: the claw necklace (more, and more again when you are nearly dead) and the bone bracelets
        internal static float DamagePct(ItemDrop.ItemData raven) { return ClawPct(raven) + BonePct(raven); }
        private static float BonePct(ItemDrop.ItemData raven) { return Has(raven, BoneBracelets) ? BoneDamagePct : 0f; }
        private static float ClawPct(ItemDrop.ItemData raven)
        {
            if (!Has(raven, ClawNecklace)) return 0f;
            Player p = Player.m_localPlayer;
            bool low = p != null && p.GetMaxHealth() > 0f && p.GetHealth() / p.GetMaxHealth() < ClawLowHp;
            return ClawDamagePct + (low ? ClawLowHpPct : 0f);
        }
        // Munin's fury and blood fury last longer with it
        internal static float FuryMul() { return On(true, ClawNecklace) ? ClawFuryMul : 1f; }

        internal static float CritPts(ItemDrop.ItemData raven) { return Has(raven, Coin) ? CoinCritPts : 0f; }
        // the coin: every kind of stealing comes less often
        internal static float StealWaitMul(ItemDrop.ItemData raven) { return Has(raven, Coin) ? CoinStealMul : 1f; }

        // the frost medallion: its blows are partly frost, and the cold and the rain do not bother it
        internal static float FrostShareOf(ItemDrop.ItemData raven) { return Has(raven, Frost) ? FrostShare : 0f; }
        internal static bool ColdProof(ItemDrop.ItemData raven) { return Has(raven, Frost); }

        internal static float StrikeCooldownMul(ItemDrop.ItemData raven) { return Has(raven, Mosquito) ? MosquitoCooldownMul : 1f; }
        internal static float RangeMul(ItemDrop.ItemData raven) { return Has(raven, Mosquito) ? 1f + MosquitoRangePct / 100f : 1f; }

        // the spiral: the guarding gifts of this raven (Hugin: dome, last chance; Munin: healing)
        internal static float GuardMul(bool munin) { return On(munin, Spiral) ? SpiralMul : 1f; }
        internal static float GuardCooldownMul(bool munin) { return On(munin, Spiral) ? SpiralCooldownMul : 1f; }

        // Колосковий оберіг: it gets hungry slower; Збруя гінця: strength comes faster; Сідло віщуна: trust grows faster
        internal static float HungerMul(ItemDrop.ItemData raven) { return Has(raven, Amulet) ? AmuletHungerMul : 1f; }
        internal static float StrengthMul(ItemDrop.ItemData raven) { return Has(raven, Harness) ? HarnessStrengthMul : 1f; }
        internal static float TrustMul(ItemDrop.ItemData raven) { return Has(raven, Saddle) ? SaddleTrustMul : 1f; }
        // Вітряна підкова: it flies faster — a part of its pace (Wing.Now), so every game and the joint tag fly it so
        internal static float WingMul(ItemDrop.ItemData raven) { return Has(raven, Anklet) ? AnkletFlyMul : 1f; }

        // Срібні кільця: its blow on the undead (draugr, skeletons, ghosts…) is stronger
        internal static float UndeadPct(ItemDrop.ItemData raven) { return Has(raven, SilverRings) ? SilverUndeadPct : 0f; }

        // Шкіряні обмотки: you are quieter and seen later, while a raven of yours wearing them sits on your shoulder
        // (set by Tick, asked often by the game)
        internal static bool Quiet;
        // Панцирний щиток: which raven takes a blow for you now (-1 none, 0 Hugin, 1 Munin); Торба збирача: one picks with you
        internal static int Shielder = -1;
        internal static bool Picking;

        // Ейтрова підвіска: this raven's gifts cost less strength (Power.Price)
        internal static float CostMul(bool munin) { return On(munin, Pendant) ? PendantCostMul : 1f; }

        // the plants the forager's bag picks one more of: food grown or found, and the farm's and the meadow's stalks
        private static readonly HashSet<string> Stalks = new HashSet<string> { "Flax", "Barley", "Thistle", "Dandelion", "Fiddleheadfern", "SmokePuff", "CarrotSeeds", "TurnipSeeds", "OnionSeeds" };
        internal static bool Plant(GameObject prefab)
        {
            ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (d == null) return false;
            return d.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable || Stalks.Contains(prefab.name);
        }

        // what it gives, for the item's tooltip (null: only for looks)
        internal static string Text(string id)
        {
            switch (id)
            {
                case Collar: return Lang.F("Коли ти гинеш, ворон утрачає вдвічі менше довіри, а шанс, що він лишиться на плечі («Вірність»), на {0}% більший.", Mathf.RoundToInt(CollarLoyalPts));
                case Bell: return Lang.F("Ворон дзенькає й каркає, коли ворог ближче {0} м починає на тебе полювати. На Муніні його крик дістає на {1}% далі.", Mathf.RoundToInt(BellRadius), Mathf.RoundToInt((BellScreamMul - 1f) * 100f));
                case ClawNecklace: return Lang.F("Удар ворона +{0}%, а коли в тебе менше {1}% здоров'я — ще +{2}%. На Муніні Лють і Кривава лють тривають на {3}% довше.", Mathf.RoundToInt(ClawDamagePct), Mathf.RoundToInt(ClawLowHp * 100f), Mathf.RoundToInt(ClawLowHpPct), Mathf.RoundToInt((ClawFuryMul - 1f) * 100f));
                case Coin: return Lang.F("Шанс криту удару ворона +{0}%. Ворон краде на {1}% рідше.", Mathf.RoundToInt(CoinCritPts), Mathf.RoundToInt((1f - 1f / CoinStealMul) * 100f));
                case Frost: return Lang.F("Ворон не мерзне й не мокне (примхи «Мерзляк» і «Мокрий» не діють). {0}% його удару — мороз, що сповільнює ціль.", Mathf.RoundToInt(FrostShare * 100f));
                case Mosquito: return Lang.F("Удар ворона перезаряджається на {0}% швидше і дістає на {1}% далі.", Mathf.RoundToInt((1f - MosquitoCooldownMul) * 100f), Mathf.RoundToInt(MosquitoRangePct));
                case Vanguard: return Lang.F("«Прикриття»: поки ворон у короткому вильоті на твоє діло (удар, «Облетіти», розвідка Хугіна, купол), удари ворогів по тобі на {0}% слабші. «Поруч», «Кружляй», трюки й догонялки не рахуються.", Mathf.RoundToInt(CoverPct));
                case Amulet: return Lang.F("Ворон голодніє на {0}% повільніше.", Mathf.RoundToInt((1f - AmuletHungerMul) * 100f));
                case Harness: return Lang.F("Сила ворона прибуває на {0}% швидше.", Mathf.RoundToInt((HarnessStrengthMul - 1f) * 100f));
                case Anklet: return Lang.F("Ворон літає на {0}% швидше.", Mathf.RoundToInt((AnkletFlyMul - 1f) * 100f));
                case Saddle: return Lang.F("Довіра ворона росте на {0}% швидше.", Mathf.RoundToInt((SaddleTrustMul - 1f) * 100f));
                case SilverRings: return Lang.F("Удар ворона по нежиті (драугри, скелети, привиди та інша мертвеччина) на {0}% сильніший.", Mathf.RoundToInt(SilverUndeadPct));
                case BoneBracelets: return Lang.F("Удар ворона +{0}%.", Mathf.RoundToInt(BoneDamagePct));
                case Shell: return Lang.F("Поки ворон на плечі, з шансом {0}% він підставляє щиток під удар ворога: удар не завдає тобі шкоди.", Mathf.RoundToInt(ShellChance * 100f));
                case Forager: return Lang.F("Поки ворон на плечі, коли ти зриваєш рослину (ягоди, гриби, овочі, льон, ячмінь, будяк…), з шансом {0}% він зриває ще одну таку саму.", Mathf.RoundToInt(ForagerChance * 100f));
                case Pendant: return Lang.F("Дари цього ворона коштують на {0}% менше сили.", Mathf.RoundToInt((1f - PendantCostMul) * 100f));
                case Wraps: return Lang.F("Поки ворон на плечі, ти ступаєш тихо: шумиш на {0}% менше, а скрадаючись, тебе помічають на {1}% ближче.", Mathf.RoundToInt((1f - WrapsNoiseMul) * 100f), Mathf.RoundToInt((1f - WrapsSeenMul) * 100f));
                case Spiral: return Lang.F("Захисні дари цього ворона сильніші: у Хугіна Купол і Останній шанс тривають на {0}% довше, у Муніна Зцілення лікує на {1}% швидше; їхня перезарядка на {2}% коротша.", Mathf.RoundToInt((SpiralMul - 1f) * 100f), Mathf.RoundToInt((SpiralMul - 1f) * 100f), Mathf.RoundToInt((1f - SpiralCooldownMul) * 100f));
            }
            return null;
        }

        // «Прикриття» (Каптур авангарду): while a raven wearing it is out on an errand of yours that has its price and
        // its wait (a strike, the order «Облетіти», Hugin's scouting or dome), the enemies' blows on you are weaker.
        // Not «Поруч» / «Кружляй» (5 minutes for 1 strength) nor the tricks and the chase (1 strength, each trick its
        // own 5 s wait: taken in turn they would cover a whole fight). Your own game only.
        internal static float CoverMul(Player p)
        {
            if (p == null) return 1f;
            foreach (bool munin in new bool[] { false, true })
                if (On(munin, Vanguard) && Covering(p, munin)) return 1f - CoverPct / 100f;
            return 1f;
        }

        private static bool Covering(Player p, bool munin)
        {
            if (Strike.Busy(p, munin)) return true;
            if (Orders.Of(p, munin) == Orders.Scout) return true;
            return !munin && (RavenSkills.Scouting() || Dome.Mine());
        }

        // the strike book's lines for the things that add to the blow
        internal static string WhyLine(ItemDrop.ItemData raven)
        {
            float d = ClawPct(raven), b = BonePct(raven), u = UndeadPct(raven);
            string s = d > 0f ? Lang.F("+ «Кігтясте намисто»: +{0}%{1}\n", Mathf.RoundToInt(d), (d > ClawDamagePct ? Lang.T(" (ти ледь жив{ий|а})") : "")) : "";
            if (b > 0f) s += Lang.F("+ «Кістяні браслети»: +{0}%\n", Mathf.RoundToInt(b));
            return u > 0f ? s + Lang.F("+ «Срібні кільця»: +{0}% по нежиті\n", Mathf.RoundToInt(u)) : s;
        }

        // ---------------------------------------------------------------- the bell: a warning when something starts hunting you

        private static readonly HashSet<ZDOID> Hunting = new HashSet<ZDOID>();
        private static float _warnedAt = -100f, _next;

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.5f;
            Player p = Player.m_localPlayer;
            Quiet = Perched(p, Wraps);
            Shielder = Perched(p, Shell, false) ? 0 : Perched(p, Shell, true) ? 1 : -1;
            Picking = Perched(p, Forager);
            // the raven that rings: one with the bell, on the shoulder now (Hugin first)
            int ringer = p == null || p.IsDead() ? -1 : Ringer(p, false) ? 0 : Ringer(p, true) ? 1 : -1;
            if (ringer < 0) { Hunting.Clear(); return; }
            bool munin = ringer == 1;
            HashSet<ZDOID> now = new HashSet<ZDOID>();
            Character first = null;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == p || c.IsDead() || c.IsPlayer() || c.IsTamed()) continue;
                if ((c.transform.position - p.transform.position).sqrMagnitude > BellRadius * BellRadius) continue;
                MonsterAI ai = c.GetBaseAI() as MonsterAI;
                if (ai == null || ai.GetTargetCreature() != p) continue;
                ZDOID id = c.GetZDOID();
                now.Add(id);
                if (!Hunting.Contains(id) && first == null) first = c;
            }
            Hunting.Clear();
            foreach (ZDOID id in now) Hunting.Add(id);
            if (first == null || Time.time - _warnedAt < BellRest) return;
            _warnedAt = Time.time;
            ScreamFx.CawAll(p, munin, munin ? 0.85f : 1.3f, 1f);
            string name = Localization.instance != null ? Lang.Game(first.m_name) : first.m_name;
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} дзенькнув: {1} полює на тебе", Plugin.RavenName(munin), name), 0, RavenItem.Icon(munin));
        }

        private static bool Ringer(Player p, bool munin) { string off; return On(munin, Bell) && !RavenSkills.OffShoulder(p, munin, out off); }
        private static bool Perched(Player p, string id) { return Perched(p, id, false) || Perched(p, id, true); }
        private static bool Perched(Player p, string id, bool munin)
        {
            string off;
            return p != null && !p.IsDead() && On(munin, id) && !RavenSkills.OffShoulder(p, munin, out off); // (an outing of its own counts too: OffShoulder)
        }

        // the shell took a blow: a word over you, a caw and a line (those two not more often than every few seconds)
        private static float _shellSaidAt = -100f;
        internal static void Shelled(Player p, bool munin, Vector3 at)
        {
            if (DamageText.instance != null) DamageText.instance.ShowText(DamageText.TextType.Immune, at + Vector3.up * 0.4f, Lang.T("щиток!"));
            if (Time.time - _shellSaidAt < 4f) return;
            _shellSaidAt = Time.time;
            ScreamFx.CawAll(p, munin, munin ? 0.85f : 1.3f, 0.8f);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} прийняв удар на щиток", Plugin.RavenName(munin)), 0, RavenItem.Icon(munin));
        }

        // ---------------------------------------------------------------- the words when a thing is put on (half the time)

        internal static float SayChance = 0.5f;

        internal static void PutOnSay(Player p, bool munin, string key)
        {
            string id = IdOf(key);
            if (p == null || id == null || Random.value >= SayChance) return;
            string line = Satiety.Say(p, munin, "gear_on_" + id);
            if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(p, munin, line);
        }
    }

    // Шкіряні обмотки: your noise is smaller, and sneaking you are seen closer
    [HarmonyPatch(typeof(Character), "AddNoise")]
    internal static class GearNoisePatch
    {
        private static void Prefix(Character __instance, ref float __0) {
            try
            {
                if (GearFx.Quiet && ReferenceEquals(__instance, Player.m_localPlayer)) __0 *= GearFx.WrapsNoiseMul;
            }
            catch (System.Exception pgx) { PatchGuard.Fail("GearNoisePatch", pgx); }
        }
    }

    [HarmonyPatch(typeof(Player), "GetStealthFactor")]
    internal static class GearStealthPatch
    {
        private static void Postfix(Player __instance, ref float __result) { if (GearFx.Quiet && ReferenceEquals(__instance, Player.m_localPlayer) && __instance.IsCrouching()) __result *= GearFx.WrapsSeenMul; } // (standing the game keeps it 1: only a sneak is helped)
    }

    // Панцирний щиток: now and then a creature's blow on you lands on the raven's shell instead (your own game)
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class GearShellPatch
    {
        [HarmonyPriority(Priority.High)]
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (GearFx.Shielder < 0 || hit == null || hit.m_hitType != HitData.HitType.EnemyHit || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
            Character from = hit.GetAttacker();
            if (from == null || from.IsPlayer() || hit.GetTotalDamage() <= 0f || Random.value >= GearFx.ShellChance) return;
            hit.m_damage.Modify(0f);
            GearFx.Shelled((Player)__instance, GearFx.Shielder == 1, __instance.GetCenterPoint());
        } catch (System.Exception e) { PatchGuard.Fail("GearShellPatch", e); } }
    }

    // Торба збирача: a plant you pick, now and then one more of it (dropped the game's own way, as the first)
    [HarmonyPatch(typeof(Pickable), "Interact")]
    internal static class GearForagerPatch
    {
        private static readonly System.Reflection.MethodInfo Drop = AccessTools.Method(typeof(Pickable), "Drop");
        private static void Prefix(Pickable __instance, Humanoid character, bool ___m_picked, out bool __state)
        {
            __state = GearFx.Picking && Drop != null && ReferenceEquals(character, Player.m_localPlayer) && !___m_picked && __instance.GetEnabled != 0 && GearFx.Plant(__instance.m_itemPrefab) && FirstTry(__instance);
        }

        // one roll a plant: until the pick comes back from its owner it still looks unpicked, and E pressed again would roll again
        private static readonly Dictionary<int, float> Rolled = new Dictionary<int, float>();
        private static bool FirstTry(Pickable plant)
        {
            float now = Time.time, at;
            if (Rolled.Count > 64) Rolled.Clear();
            int id = plant.GetInstanceID();
            if (Rolled.TryGetValue(id, out at) && now - at < 5f) return false;
            Rolled[id] = now;
            return true;
        }
        private static void Postfix(Pickable __instance, bool __state)
        { try {
            if (!__state || Random.value >= GearFx.ForagerChance) return;
            Drop.Invoke(__instance, new object[] { __instance.m_itemPrefab, 1, Mathf.Max(1, __instance.m_amount) });
        } catch (System.Exception e) { PatchGuard.Fail("GearForagerPatch", e); } }
    }

    // «Прикриття»: a creature's blow on you (after armour), in your own game, while the hooded raven is out
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class GearCoverPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (hit == null || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
            Character from = hit.GetAttacker();
            if (from == null || from == __instance || from.IsPlayer()) return;
            float m = GearFx.CoverMul((Player)__instance);
            if (m < 1f) hit.m_damage.Modify(m);
        } catch (System.Exception e) { PatchGuard.Fail("GearCoverPatch", e); } }
    }
}
#endif
