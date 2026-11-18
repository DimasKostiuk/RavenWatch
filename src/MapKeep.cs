#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A lasting mark on your map (the user 2026-11-16: «постійна мітка про серця оселі та Залу Варти, щоб не загубитись — з можливістю її стерти вручну»).
    // It is the game's own saved mark, as one you put yourself: kept with your map of this world, and a right click takes it off. The character keeps
    // «put» for it (a story flag «mapkeep_<kind>_<x>_<z>_<world>»): taken off by hand, it is never put again; put once, it is not put twice.
    // Its picture is one of the game's (Minimap.PinType: a saved mark keeps only its type — a picture of our own would be gone after a reload).
    //   Put    a mark there now, if this character never had one there (the place's rule «mappin:» — Loc; a heart you stand by — below)
    //   Drop   the thing is gone: its mark off, and «put» forgotten (a new one there gets a mark again)
    // Hearts (NpcHeart): the one you stand by gets a house with its name (renamed — the mark's words follow, while the mark is there); one taken down is
    // seen gone once you are by its spot again (its ground loaded, no heart there): its mark off. Cost: every Every s — the hearts loaded here (a few) and
    // this world's «put» of hearts within Gone m (the character's flags: one pass)
    internal static class MapKeep
    {
        internal const float Every = 2f, Near = 20f, Gone = 30f;
        internal const Minimap.PinType HeartPin = Minimap.PinType.Icon1, PlacePin = Minimap.PinType.Icon0; // (the game's house · fire)
        private const string Pre = "mapkeep_";
        private static float _next;

        private static string Key(string kind, Vector3 at) { return Pre + kind + "_" + Mathf.RoundToInt(at.x).ToString(CultureInfo.InvariantCulture) + "_" + Mathf.RoundToInt(at.z).ToString(CultureInfo.InvariantCulture) + "_" + Story.WorldId; }

        // the game has read your saved map of this world (before that a mark put would be lost with its loading, and «put» kept for nothing).
        // The game's own fields, found by name: gone in a newer game — said once in the log, and no marks (never one put to be lost)
        private static readonly System.Reflection.FieldInfo Generated = HarmonyLib.AccessTools.Field(typeof(Minimap), "m_hasGenerated");
        private static bool _toldMissing;
        private static bool Ready
        {
            get
            {
                if (Generated == null || MapPins == null) { if (!_toldMissing) { _toldMissing = true; Debug.Log("[RavenWatch] map: the game's Minimap has no m_hasGenerated / m_pins — lasting marks are off"); } return false; }
                return Minimap.instance != null && (bool)Generated.GetValue(Minimap.instance);
            }
        }

        internal static void Put(Player p, string kind, Vector3 at, Minimap.PinType type, string label)
        {
            if (p == null || p != Player.m_localPlayer || !Ready) return;
            string key = Key(kind, at);
            Minimap.PinData had = Find(at, type);
            if (Story.Flag(p, key))
            {
                if (had == null || had.m_name == label) return; // (taken off by hand: never again · as it is)
                Minimap.instance.RemovePin(had); // (renamed: its words anew — the game draws a mark's name once)
                Minimap.instance.AddPin(at, type, label, true, false);
                return;
            }
            if (had == null) Minimap.instance.AddPin(at, type, label, true, false); // (one of yours there already, of that picture: it is that one)
            Story.SetFlag(p, key, true);
        }

        internal static void Drop(Player p, string kind, Vector3 at, Minimap.PinType type)
        {
            if (p == null || !Ready) return;
            Minimap.PinData had = Find(at, type);
            if (had != null) Minimap.instance.RemovePin(had);
            Story.SetFlag(p, Key(kind, at), false);
        }

        // a saved mark of that picture on that spot (within 2 m: a heart never moves)
        private static Minimap.PinData Find(Vector3 at, Minimap.PinType type)
        {
            foreach (Minimap.PinData pin in Pins())
                if (pin != null && pin.m_save && pin.m_type == type && Utils.DistanceXZ(pin.m_pos, at) < 2f) return pin;
            return null;
        }

        private static readonly System.Reflection.FieldInfo MapPins = HarmonyLib.AccessTools.Field(typeof(Minimap), "m_pins");
        private static readonly List<Minimap.PinData> NoPins = new List<Minimap.PinData>();
        private static List<Minimap.PinData> Pins() { return MapPins.GetValue(Minimap.instance) as List<Minimap.PinData> ?? NoPins; } // (asked only when Ready: the field is there)

        // ---------------------------------------------------------------- the hearts (the game's clock: HuginWhispers.Update)
        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + Every;
            Player p = Player.m_localPlayer;
            if (p == null || !Ready || ZNetScene.instance == null) return;
            Vector3 me = p.transform.position;
            foreach (NpcHeart h in NpcHeart.Loaded)
                if (h != null && Utils.DistanceXZ(h.transform.position, me) < Near)
                    Put(p, "heart", h.transform.position, HeartPin, h.Name.Length > 0 ? h.Name : Lang.T("Серце оселі"));
            string pre = Pre + "heart_", world = "_" + Story.WorldId;
            List<Vector3> gone = null;
            foreach (string f in Story.Of(p).Flags)
            {
                Vector3 at;
                if (!f.StartsWith(pre, StringComparison.Ordinal) || !f.EndsWith(world, StringComparison.Ordinal) || !Spot(f.Substring(pre.Length, f.Length - pre.Length - world.Length), out at)) continue;
                at.y = me.y;
                if (Utils.DistanceXZ(at, me) > Gone || !ZNetScene.instance.IsAreaReady(at)) continue; // (its ground not here yet: nothing known of it)
                if (!Stands(at)) (gone = gone ?? new List<Vector3>()).Add(at);
            }
            if (gone != null) foreach (Vector3 at in gone) { Drop(p, "heart", at, HeartPin); Npcs.Log("map: the heart at " + Npcs.V(at) + " is gone — its mark off"); } // (after the look: Drop changes the flags looked over)
        }

        // a heart on that spot: its object here, or its ZDO (the census of what is loaded: it comes before the object does — a mark is never taken off a heart still being made)
        private static bool Stands(Vector3 at)
        {
            foreach (NpcHeart h in NpcHeart.Loaded) if (h != null && Utils.DistanceXZ(h.transform.position, at) < 2f) return true;
            foreach (Perches.Def d in Perches.Hearts)
                foreach (ZDO z in NpcCensus.Of(d.Prefab)) if (z != null && Utils.DistanceXZ(z.GetPosition(), at) < 2f) return true;
            return false;
        }

        // «<x>_<z>» of a key
        private static bool Spot(string xz, out Vector3 at)
        {
            at = Vector3.zero;
            int u = xz.LastIndexOf('_'), x, z;
            if (u <= 0 || !int.TryParse(xz.Substring(0, u), NumberStyles.Integer, CultureInfo.InvariantCulture, out x) || !int.TryParse(xz.Substring(u + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out z)) return false;
            at = new Vector3(x, 0f, z);
            return true;
        }
    }
}
#endif
