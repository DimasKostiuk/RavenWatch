#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Putting a thing from the raven table on a raven, as the game puts on any gear: the hero's own "equipping" move
    // and bar (the game's action queue), then the thing goes onto the raven and the raven settles it — wings up, a
    // shiver, a few feathers shaken loose, a soft caw — seen by everyone nearby. Pressed again meanwhile: called off.
    // From the bag: right click (or its hotbar key) gives it to Hugin, with Shift to Munin; from the book: a click.
    internal static class GearDress
    {
        internal static float PoseSeconds = 1.2f;
        private const string RpcH = "RavenWatch_GearDressedH", RpcM = "RavenWatch_GearDressedM";
        private static readonly System.Reflection.MethodInfo QueueOn = AccessTools.Method(typeof(Player), "QueueEquipAction");

        // the things waiting for the hero's move to end, and the raven each is for
        private static readonly Dictionary<ItemDrop.ItemData, bool> Pending = new Dictionary<ItemDrop.ItemData, bool>();

        internal static void Register()
        {
            RavenFx.Register(RpcH, p => Settle(p, false));
            RavenFx.Register(RpcM, p => Settle(p, true));
        }

        internal static bool IsThing(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null && RavenGear.KeyOfPrefab(item.m_dropPrefab.name) != null;
        }

        private static Outfits.Entry EntryOf(ItemDrop.ItemData thing) { return IsThing(thing) ? Outfits.Find(RavenGear.KeyOfPrefab(thing.m_dropPrefab.name)) : null; }

        // start giving it to that raven; null when started (or done), else why not (said by the caller)
        internal static string Start(Player p, bool munin, ItemDrop.ItemData thing)
        {
            if (p == null || thing == null) return null;
            string why = Outfits.CanPutOn(p, munin, EntryOf(thing));
            if (why != null) return why;
            List<ItemDrop.ItemData> gone = new List<ItemDrop.ItemData>(); // moves called off long ago: forgotten
            foreach (ItemDrop.ItemData t in Pending.Keys) if (!p.GetInventory().ContainsItem(t)) gone.Add(t);
            foreach (ItemDrop.ItemData t in gone) Pending.Remove(t);
            Pending[thing] = munin;
            if (RavenSeat.EquipSeconds > 0f && QueueOn != null && !p.InAttack()) // (the ravens' own "Shoulder on/off seconds"; 0: at once)
            {
                thing.m_shared.m_equipDuration = RavenSeat.EquipSeconds; // (one setting for every thing of this kind)
                try { QueueOn.Invoke(p, new object[] { thing }); return null; }
                catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Gear equip move: " + e.Message); }
            }
            Done(p, thing);
            return null;
        }

        // the move is over (the game "equips" it: the patch below); true when it was ours
        internal static bool Done(Player p, ItemDrop.ItemData thing)
        {
            bool munin;
            if (thing == null || !Pending.TryGetValue(thing, out munin)) return false;
            Pending.Remove(thing);
            string why = Outfits.PutOn(p, munin, EntryOf(thing), thing); // (asked again: the raven may have gone meanwhile)
            if (why != null) { RavenMsg.Center(p, why); return true; }
            RavenFx.Broadcast(munin ? RpcM : RpcH, p, x => Settle(x, munin)); // (its words: Outfits.PutOn)
            RavenBook.Again();
            return true;
        }

        // the raven settles its new thing (on any player's shoulder)
        private static void Settle(Player p, bool munin)
        {
            ShoulderLife life = Shoulder.LifeOf(p, munin);
            if (life == null) return; // (an asleep raven wears nothing: below the wardrobe level)
            ShoulderLife.Moment(p, munin, "dress", PoseSeconds);
            life.ShakeNow();
            Vector3 at = life.transform.position + Vector3.up * 0.1f;
            Feathers.Shed(at, munin, 6);
            ScreamFx.CawAt(life.transform, at, munin ? 0.9f : 1.35f, 0.45f, true, Voice.CodeOn(p, munin));
        }
    }

    // right click in the bag, or the hotbar key: a thing for a raven goes to Hugin (Shift: Munin) after the move
    [HarmonyPatch(typeof(Humanoid), "UseItem")]
    internal static class GearDressUsePatch
    {
        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData __1)
        { try {
            if (!GearDress.IsThing(__1)) return true;
            Player p = __instance as Player;
            if (p == null || p != Player.m_localPlayer) return false;
            bool munin = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            string why = GearDress.Start(p, munin, __1);
            if (why != null) RavenMsg.Center(p, why);
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("GearDressUsePatch", e); return true; } }
    }

    // the game's move done: it "equips" the thing — here it goes onto the raven instead (it is no gear of the hero's)
    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    internal static class GearDressEquipPatch
    {
        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
        { try {
            if (!GearDress.IsThing(item)) return true;
            Player p = __instance as Player;
            __result = false;
            if (p != null && p == Player.m_localPlayer) GearDress.Done(p, item);
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("GearDressEquipPatch", e); return true; } }
    }
}
#endif
