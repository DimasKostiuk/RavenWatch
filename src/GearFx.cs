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
            Mosquito = "kihti_komara", Spiral = "spiral_zakhystu";

        // the things of the second (Mistlands) table: the raven lets itself be given them from this trust level on
        internal const int HighLevel = 5;
        private static readonly string[] High = { Vanguard, Mosquito, Frost, Spiral };
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
        internal static float CoverPct = 20f; // the vanguard hood: less damage from enemies while it is off doing something

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

        // the strike's blow: the claw necklace (more, and more again when you are nearly dead)
        internal static float DamagePct(ItemDrop.ItemData raven)
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

        // the strike book's line for the thing that adds to the blow
        internal static string WhyLine(ItemDrop.ItemData raven)
        {
            float d = DamagePct(raven);
            return d > 0f ? Lang.F("+ «Кігтясте намисто»: +{0}%{1}\n", Mathf.RoundToInt(d), (d > ClawDamagePct ? Lang.T(" (ти ледь живий)") : "")) : "";
        }

        // ---------------------------------------------------------------- the bell: a warning when something starts hunting you

        private static readonly HashSet<ZDOID> Hunting = new HashSet<ZDOID>();
        private static float _warnedAt = -100f, _next;

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.5f;
            Player p = Player.m_localPlayer;
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
            ScreamFx.Caw(p, munin, munin ? 0.85f : 1.3f, 1f);
            string name = Localization.instance != null ? Lang.Game(first.m_name) : first.m_name;
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} дзенькнув: {1} полює на тебе", Plugin.RavenName(munin), name), 0, RavenItem.Icon(munin));
        }

        private static bool Ringer(Player p, bool munin) { string off; return On(munin, Bell) && !RavenSkills.OffShoulder(p, munin, out off); }

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
