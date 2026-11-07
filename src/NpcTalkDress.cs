#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What you give a person who is not on guard duty (the user 2026-11-06; who gets what: NpcGear.RoleOf). In its talk's own window, rows like any answers (its look: later, a window of its own):
    //   its clothes   «Перевдягнися» among its favours (NpcFavours «dress»: its patience, a roll, DressFor). Head, body, legs, cape: real things out of your bag into its cells (NpcGear keeps them), worn in place of
    //                 what it was given when it came, with no stats at all (NpcGear.Plain). Any player may, in that same way: taking one off only while it agrees to the favour, and it holds that against you (NpcMemory «clothes_taken»).
    //   its hand      its button «Спорядження» (Q): one cell. One with work: its tool, an axe or a pick. One with none who can stand up for itself: one melee weapon
    // Your cells' owner: as the game's own chest window, this game takes the person over first (Claim), so that nothing is written twice. Sent off or gone: all of it at its feet (NpcGear.DropAll).
    internal static partial class NpcTalk
    {
        internal const string DressPlace = "dress";
        private const int MostListed = 10; // (the scroll has room; more in your bag: the first ones)

        private static readonly KeyValuePair<ItemDrop.ItemData.ItemType, string>[] DressSlots =
        {
            new KeyValuePair<ItemDrop.ItemData.ItemType, string>(ItemDrop.ItemData.ItemType.Helmet, Lang.K("Голова")),
            new KeyValuePair<ItemDrop.ItemData.ItemType, string>(ItemDrop.ItemData.ItemType.Chest, Lang.K("Тулуб")),
            new KeyValuePair<ItemDrop.ItemData.ItemType, string>(ItemDrop.ItemData.ItemType.Legs, Lang.K("Ноги")),
            new KeyValuePair<ItemDrop.ItemData.ItemType, string>(ItemDrop.ItemData.ItemType.Shoulder, Lang.K("Плащ")),
        };

        private static string ItemWord(ItemDrop.ItemData it) { return Localization.instance != null ? Localization.instance.Localize(it.m_shared.m_name) : it.m_shared.m_name; }

        private static ItemDrop.ItemData InCells(NpcGear g, System.Func<ItemDrop.ItemData, bool> which)
        {
            if (g == null || g.Cells == null) return null;
            foreach (ItemDrop.ItemData it in g.Cells.GetAllItems()) if (which(it)) return it;
            return null;
        }

        private static List<ItemDrop.ItemData> InBag(Player p, System.Func<ItemDrop.ItemData, bool> which)
        {
            List<ItemDrop.ItemData> l = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) if (!it.m_equipped && which(it) && l.Count < MostListed) l.Add(it); // (what you wear: take it off first)
            return l;
        }

        // the person's cells are written here now, as when the game's own window opens them
        private static bool Claim(NpcActor a, NpcGear g)
        {
            ZNetView nv = a.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || g == null || g.Cells == null || !NpcLocker.Ready(a)) return false;
            if (!nv.IsOwner()) nv.ClaimOwnership();
            g.Read();
            return true;
        }

        private static void ToBag(Player p, NpcGear g, ItemDrop.ItemData it)
        {
            g.Cells.RemoveItem(it);
            if (!p.GetInventory().AddItem(it)) ItemDrop.DropItem(it, it.m_stack, p.transform.position + p.transform.forward * 0.8f + Vector3.up * 0.5f, Quaternion.identity);
        }

        private static bool ToCells(Player p, NpcGear g, ItemDrop.ItemData it)
        {
            if (!p.GetInventory().ContainsItem(it)) return false; // (the list stood open a while: it is not in your bag now — never two of it)
            p.GetInventory().RemoveItem(it);
            if (g.Cells.AddItem(it)) return true;
            p.GetInventory().AddItem(it);
            return false;
        }

        // a list of rows, each with what it does: its words, then the window
        private static void Rows(Player p, string says, List<KeyValuePair<string, System.Action>> rows)
        {
            List<string> words = new List<string>();
            foreach (KeyValuePair<string, System.Action> r in rows) words.Add(r.Key);
            NpcActor who = With;
            RavenBook.ShowTalk(who.Name, says, words, i =>
            {
                if (With != who || i < 0 || i >= rows.Count) { Close(); return; }
                IdleStir();
                rows[i].Value();
            }, Close);
        }

        // ---------------------------------------------------------------- its clothes
        private static void ShowDress(Player p, string says)
        {
            NpcGear g = With.Gear;
            if (g == null || NpcFavours.DressFor != With) { Show("start", p); return; }
            List<KeyValuePair<string, System.Action>> rows = new List<KeyValuePair<string, System.Action>>();
            foreach (KeyValuePair<ItemDrop.ItemData.ItemType, string> s in DressSlots)
            {
                ItemDrop.ItemData.ItemType type = s.Key;
                string label = Lang.T(s.Value);
                ItemDrop.ItemData on = InCells(g, it => it.m_shared.m_itemType == type);
                rows.Add(new KeyValuePair<string, System.Action>(on != null ? label + ": " + ItemWord(on) : label + ": " + Lang.T("своє"), () => ShowDressSlot(p, type, label)));
            }
            rows.Add(new KeyValuePair<string, System.Action>(Lang.T("Годі, досить."), () => Show("start", p)));
            Rows(p, says ?? NpcPhrases.Compose(With, "dress_ask", p) ?? With.Form(Lang.T("Ну, що там у тебе?")), rows);
        }

        private static void ShowDressSlot(Player p, ItemDrop.ItemData.ItemType type, string label)
        {
            NpcGear g = With.Gear;
            NpcActor a = With;
            List<KeyValuePair<string, System.Action>> rows = new List<KeyValuePair<string, System.Action>>();
            foreach (ItemDrop.ItemData it in InBag(p, x => x.m_shared.m_itemType == type))
            {
                ItemDrop.ItemData thing = it;
                rows.Add(new KeyValuePair<string, System.Action>(ItemWord(it), () =>
                {
                    if (!Claim(a, g)) { Show("start", p); return; }
                    ItemDrop.ItemData old = InCells(g, x => x.m_shared.m_itemType == type);
                    if (old != null) ToBag(p, g, old);
                    bool put = ToCells(p, g, thing);
                    ShowDress(p, put ? NpcPhrases.Compose(a, "dress_thanks", p) : null);
                }));
            }
            ItemDrop.ItemData on = InCells(g, x => x.m_shared.m_itemType == type);
            if (on != null) rows.Add(new KeyValuePair<string, System.Action>(Lang.F("Зняти: {0}", ItemWord(on)), () =>
            {
                if (!Claim(a, g)) { Show("start", p); return; }
                ItemDrop.ItemData now = InCells(g, x => x.m_shared.m_itemType == type);
                if (now != null) { ToBag(p, g, now); NpcMemory.Tell(a, "clothes_taken", p.GetPlayerID(), ""); }
                ShowDress(p, NpcPhrases.Compose(a, "dress_took", p));
            }));
            rows.Add(new KeyValuePair<string, System.Action>(Lang.T("Назад."), () => ShowDress(p, null)));
            Rows(p, rows.Count > 1 ? label : Lang.F("{0}: у твоїй торбі нема нічого такого.", label), rows);
        }

        // ---------------------------------------------------------------- its hand (Q: «Спорядження»)
        internal static void OpenHand(NpcActor a, Player p)
        {
            Open(a, p, ShowHand);
        }

        private static void ShowHand(Player p)
        {
            NpcActor a = With;
            NpcGear g = a.Gear;
            NpcGear.Role role = NpcGear.RoleOf(a);
            if (g == null || role == NpcGear.Role.None) { Close(); return; }
            List<KeyValuePair<string, System.Action>> rows = new List<KeyValuePair<string, System.Action>>();
            foreach (ItemDrop.ItemData it in InBag(p, x => NpcGear.HandFits(role, x)))
            {
                ItemDrop.ItemData thing = it;
                rows.Add(new KeyValuePair<string, System.Action>(ItemWord(it), () =>
                {
                    if (!Claim(a, g)) { Close(); return; }
                    ItemDrop.ItemData old = InCells(g, x => NpcGear.HandFits(role, x));
                    if (old != null) ToBag(p, g, old);
                    ToCells(p, g, thing);
                    ShowHand(p);
                }));
            }
            ItemDrop.ItemData on = InCells(g, x => NpcGear.HandFits(role, x));
            if (on != null) rows.Add(new KeyValuePair<string, System.Action>(Lang.F("Забрати: {0}", ItemWord(on)), () =>
            {
                if (!Claim(a, g)) { Close(); return; }
                ItemDrop.ItemData now = InCells(g, x => NpcGear.HandFits(role, x));
                if (now != null) ToBag(p, g, now);
                ShowHand(p);
            }));
            rows.Add(new KeyValuePair<string, System.Action>(Lang.T("Готово."), Close));
            string what = role == NpcGear.Role.Tool ? Lang.T("Сокира чи кайло для роботи") : Lang.T("Зброя ближнього бою");
            Rows(p, what + ": " + (on != null ? ItemWord(on) : Lang.T("нема")), rows);
        }
    }
}
#endif
