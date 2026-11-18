#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Маю дещо для тебе» (the user 2026-11-07): a gift to a settler, once a game day a person. Its talk's answer (?giftable) opens a made place (Places «gift»): a few
    // things of your bag it could take (a kind of gift each: Kind), «Ось, тримай: …». It takes what you pick as it takes food (a bite: it eats it, its hunger and its meals
    // on the game that runs it), anything else is simply its now.
    //   its taste   steady for a person (its seed, nothing kept): a liking for each kind × its nature's (NpcNature.Like: Likes) × what its hands know from its story (a cook
    //               bread, a gatherer mushrooms and flowers, a fighter trophies) — the two it likes best are liked, one it cannot stand is the lowest; a food it often
    //               eats is liked too (NpcToil.AteOf: where it is run only)
    //   liked       its memory gift_liked (warmer to you; three of them tie the thread «рука»: NpcThreads), its patience up · plain: a little warmer
    //   not liked   words only — but a grumbler or a bully takes it ill: gift_insult (colder), a little angry
    // Words gift_ask, gift_liked, gift_plain, gift_disliked, gift_insult ({item}). Runs on your game: your bag is here.
    internal static partial class NpcTalk
    {
        internal const string GiftPlace = "gift";
        private const string GiftRow = "~gift";
        private const int GiftsListed = 6;
        internal static float GiftPlainWarm = 1.5f, GiftLikedPatience = 6f, GiftIrk = 6f;
        private static readonly string[] GiftKinds = { "drink", "meat", "fish", "bread", "sweet", "mushroom", "flower", "shiny", "trophy" };

        // what kind of gift a thing is (null: not one to give)
        internal static string GiftKind(ItemDrop.ItemData it)
        {
            if (it == null || it.m_shared == null || it.m_equipped) return null;
            string n = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
            if (it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy) return "trophy";
            if (n.StartsWith("Mead", StringComparison.Ordinal)) return "drink";
            if (n.StartsWith("Mushroom", StringComparison.Ordinal)) return "mushroom";
            if (n == "Dandelion" || n == "Thistle") return "flower";
            if (n == "Amber" || n == "AmberPearl" || n == "Ruby" || n.EndsWith("Necklace", StringComparison.Ordinal)) return "shiny";
            if (n == "Honey" || n == "Raspberry" || n == "Blueberries" || n == "Cloudberry" || n.Contains("Jam")) return "sweet";
            if (it.m_shared.m_food <= 0f) return null;
            if (n.Contains("Fish")) return "fish";
            if (n.Contains("Meat") || n.Contains("Sausage") || n.Contains("Pudding") || n.Contains("Ham")) return "meat";
            return "bread"; // (any other cooked food: a hearty bite)
        }

        private static float Liking(NpcActor a, string kind)
        {
            float v = Mathf.Lerp(0.4f, 1.6f, NpcNature.H(SeedOf(a), "taste_" + kind)) * NpcNature.Like(NpcNature.Of(a), kind);
            if (kind == "bread" && NpcBackstory.Knack(a, "cook") > 0f) v *= 1.5f;
            if ((kind == "mushroom" || kind == "flower") && NpcBackstory.Knack(a, "gatherer") > 0f) v *= 1.5f;
            if (kind == "trophy" && (NpcBackstory.Knack(a, "melee") > 0f || NpcBackstory.Knack(a, "guard") > 0f)) v *= 1.5f;
            return v;
        }

        // 1 liked, −1 it cannot stand it, 0 plain
        private static int Taste(NpcActor a, string kind, string name)
        {
            if (AteOf(a, name) >= 2) return 1;
            float mine = Liking(a, kind);
            int above = 0; float least = float.MaxValue;
            foreach (string k in GiftKinds) { float v = Liking(a, k); if (k != kind && v > mine) above++; if (v < least) least = v; }
            if (above < 2 && mine >= 1.1f) return 1;
            return mine <= least + 0.001f && mine < 0.8f ? -1 : 0;
        }

        private static int AteOf(NpcActor a, string name) { return NpcToil.AteOf(a, name); }

        // the things of your bag it could take, a kind each name, at most «most» (1: the condition only asks whether there is one)
        private static List<ItemDrop.ItemData> Giftable(Player p, int most = GiftsListed)
        {
            List<ItemDrop.ItemData> l = new List<ItemDrop.ItemData>();
            if (p == null) return l;
            HashSet<string> seen = new HashSet<string>();
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
            {
                if (l.Count >= most) break;
                if (GiftKind(it) != null && seen.Add(it.m_shared.m_name)) l.Add(it);
            }
            return l;
        }

        private static void ShowGift(Player p)
        {
            NpcActor a = With;
            List<KeyValuePair<string, Action>> rows = new List<KeyValuePair<string, Action>>();
            foreach (ItemDrop.ItemData it in Giftable(p)) { ItemDrop.ItemData x = it; string you = Lang.F("Ось, тримай: {0}.", ItemWord(it)); rows.Add(new KeyValuePair<string, Action>(you, () => Give(p, x, you))); }
            rows.Add(new KeyValuePair<string, Action>(Lang.T("Ні, потім."), () => Show("start", p)));
            Rows(p, NpcPhrases.Compose(a, "gift_ask", p) ?? a.Form(Lang.T("Мені? А що там?")), rows);
        }

        private static void Give(Player p, ItemDrop.ItemData it, string you)
        {
            NpcActor a = With;
            string kind = GiftKind(it);
            if (a == null || kind == null || !p.GetInventory().ContainsItem(it) || !FirstToday(a, p, GiftRow)) { Show("start", p); return; } // (the list stood open: not in your bag now, or given since — the day's row written here)
            string name = ItemWord(it);
            int taste = Taste(a, kind, name); // (before it eats it: this very bite is not «what it eats often»)
            if (it.m_shared.m_food > 0f) Feed(a, p, it); else p.GetInventory().RemoveItem(it, 1); // (a bite: it eats it, where it is run)
            bool touchy = NpcPhrases.Tones(a).Contains("бурч") || NpcNature.Is(a, "bully");
            string theme = taste > 0 ? "gift_liked" : taste < 0 ? (touchy ? "gift_insult" : "gift_disliked") : "gift_plain";
            long me = p.GetPlayerID();
            if (taste > 0) { NpcMemory.Tell(a, "gift_liked", me, NpcMemory.Thing(NpcKit.PrefabOf(it), 1)); NpcPatience.Spend(a, p, -GiftLikedPatience); Warmer(a); }
            else if (taste == 0) NpcAttitude.Tell(a, me, GiftPlainWarm);
            else if (touchy) { NpcMemory.Tell(a, "gift_insult", me, NpcMemory.Thing(NpcKit.PrefabOf(it), 1)); NpcPatience.Irritate(a, p, GiftIrk); Colder(a, false); }
            string said = NpcPhrases.Compose(a, theme, p, new Dictionary<string, string> { { "item", name } }) ?? "…";
            Npcs.Log(a.Id + ": given " + name + " (" + kind + ", " + (taste > 0 ? "liked" : taste < 0 ? "not liked" : "plain") + ")");
            List<KeyValuePair<string, Action>> rows = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>(Welcome(a, p), () => Show("start", p)),
                new KeyValuePair<string, Action>(Bye(a, p), Close),
            };
            Rows(p, RavenBook.Says(NpcBody.Spoken(a, said)), rows, you);
        }

        private static void WireGift()
        {
            Places[GiftPlace] = ShowGift;
            StoryCond.Register("giftable", new StoryCond.Kind { Usage = "giftable   (у тебе є що йому подарувати, і сьогодні ти ще не дарував)", Test = (p, w) => Him != null && p != null && !DoneToday(Him, p, GiftRow) && Giftable(p, 1).Count > 0 });
        }
    }
}
#endif
