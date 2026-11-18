#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // People on the map (npc-plan.md): a mark with its name for each person loaded near you, following it as it moves.
    // Not saved (made again each time), not sent anywhere: every game draws the ones it has. Odin has none (the story
    // keeps him a secret). Looked at twice a second, from the people's own Update (no extra loop when there are none).
    // Hidden: a module's word that a person is not to be shown now (the story: one a search is for — StorySeek.Hides, the user 2026-11-15).
    internal static class NpcMap
    {
        private const float Every = 0.5f;
        private static readonly Dictionary<NpcActor, Minimap.PinData> Pins = new Dictionary<NpcActor, Minimap.PinData>();
        private static readonly Dictionary<NpcActor, bool> PinGuard = new Dictionary<NpcActor, bool>(); // (which mark it has: a guard's or a person's)
        private static Minimap _map;
        private static float _next;
        private static Sprite _icon;
        internal static System.Func<Vector3, bool> Hidden; // (a module's: no mark at this spot now)

        internal static void Tick()
        {
            if (Time.time < _next || Minimap.instance == null) return;
            _next = Time.time + Every;
            if (_map != Minimap.instance) { Pins.Clear(); PinGuard.Clear(); Rescues.Clear(); _map = Minimap.instance; } // (a new map: the old marks went with the old one)
            if (Time.time >= _nextRescue) { _nextRescue = Time.time + 2f; RescuePins(); }
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || a.Mind == null || (a.Def != null && a.Def.Echo > 0f)) continue; // (Odin, an echo: no mark)
                Minimap.PinData pin;
                if (Hidden != null && Hidden(a.transform.position)) { if (Pins.TryGetValue(a, out pin)) Forget(a); continue; } // (not to be shown now: its mark off till then)
                bool guard = Guard.Is(a), was;
                string shown = a.ShownName; // (its name as the game shows it: one not met yet is «Чужинець» here too — StoryStranger)
                if (Pins.TryGetValue(a, out pin) && pin != null && (!PinGuard.TryGetValue(a, out was) || was != guard || pin.m_name != shown)) { _map.RemovePin(pin); pin = null; } // (became a guard, or stopped, or its shown name changed: its mark anew)
                if (pin == null)
                {
                    pin = _map.AddPin(a.transform.position, Minimap.PinType.None, shown, false, false, 0L, default(Splatform.PlatformUserID));
                    PinGuard[a] = guard;
                    Sprite s = guard ? GuardIcon() : Icon();
                    if (s != null) pin.m_icon = s;
                    Pins[a] = pin;
                }
                pin.m_pos = a.transform.position;
            }
        }

        // a person gone from here (destroyed, out of range): its mark too
        internal static void Forget(NpcActor a)
        {
            Minimap.PinData pin;
            if (a == null || !Pins.TryGetValue(a, out pin)) return;
            Pins.Remove(a); PinGuard.Remove(a);
            if (pin != null && Minimap.instance != null && _map == Minimap.instance) Minimap.instance.RemovePin(pin);
        }

        // ---------------------------------------------------------------- people in need you marked on a board (NpcRescue): a teal mark
        // at each, while it lasts and till you find it; a lost thing's own mark once its owner has said where (its person loaded here), found or not
        private static readonly Dictionary<string, Minimap.PinData> Rescues = new Dictionary<string, Minimap.PinData>();
        private static float _nextRescue;
        private static Sprite _rescueIcon;

        internal static void RescueNow() { _nextRescue = 0f; } // (one marked just now: drawn at the next look, not in up to 2 s)

        private static void RescuePins()
        {
            long now = (long)NpcRescue.Now;
            Dictionary<string, KeyValuePair<Vector3, string>> want = new Dictionary<string, KeyValuePair<Vector3, string>>();
            foreach (NpcRescue.Known k in NpcRescue.All())
            {
                ZDOID id = k.Npc;
                ZDO nz = ZDOMan.instance != null && !id.IsNone() ? ZDOMan.instance.GetZDO(id) : null; // (its own «until» when this game has it: stretched once you reached it)
                if (System.Math.Max(k.Until, nz != null ? nz.GetLong(NpcRescue.KUntil, 0L) : 0L) < now) continue;
                if (nz != null && (nz.GetInt(NpcRescue.KStage, 0) >= NpcRescueHits.Dead || nz.GetString(NpcRescue.KKind, "").Length == 0)) continue; // (died, its time ran out, or it is in need no more — hired by anyone: NpcRescueSite.Done)
                if (!k.Found) want[k.Link] = new KeyValuePair<Vector3, string>(k.At, Lang.T("Людина в біді") + ": " + NpcRescue.KindWord(k.Kind));
                if (nz == null) continue; // (not here: no lost thing of it to show)
                foreach (NpcActor a in NpcActor.Live)
                {
                    if (a == null || a.Zdo == null || a.Zdo.m_uid != id) continue;
                    Vector3 item = a.Zdo.GetVec3(NpcRescue.KItem, Vector3.zero);
                    if (item != Vector3.zero && NpcLevers.Flag(a, "lost")) { want[k.Link + "/item"] = new KeyValuePair<Vector3, string>(item, a.Form(Lang.F("Річ, яку загубив|загубила {0}", a.Name))); Glow(item, id); }
                }
            }
            foreach (KeyValuePair<string, KeyValuePair<Vector3, string>> w in want)
            {
                Minimap.PinData pin;
                if (Rescues.TryGetValue(w.Key, out pin) && pin != null) { pin.m_pos = w.Value.Key; continue; }
                pin = _map.AddPin(w.Value.Key, Minimap.PinType.None, w.Value.Value, false, false, 0L, default(Splatform.PlatformUserID));
                Sprite s = RescueIcon();
                if (s != null) pin.m_icon = s;
                Rescues[w.Key] = pin;
            }
            foreach (string key in new List<string>(Rescues.Keys))
            {
                if (want.ContainsKey(key)) continue;
                Minimap.PinData pin = Rescues[key];
                Rescues.Remove(key);
                if (pin != null) _map.RemovePin(pin);
            }
        }

        // the lost thing on the ground: a warm glow on it, seen from afar (a small axe in the snow is easily missed) — on
        // every game that has it loaded, nothing sent; it goes with the thing when it is picked up
        private static void Glow(Vector3 at, ZDOID owner)
        {
            ItemDrop d = null;
            foreach (Collider c in Physics.OverlapSphere(at, 8f, LayerMask.GetMask("item")))
            {
                ItemDrop x = c != null ? c.GetComponentInParent<ItemDrop>() : null; string v;
                if (x != null && x.m_itemData != null && x.m_itemData.m_customData != null && x.m_itemData.m_customData.TryGetValue(NpcRescueSite.LostOf, out v) && NpcKit.Resolve(v) == owner) { d = x; break; } // (its owner by a lasting link: NpcKit)
            }
            if (d == null || d.transform.Find("RavenWatch_lostGlow") != null) return;
            GameObject g = new GameObject("RavenWatch_lostGlow");
            g.transform.SetParent(d.transform, false);
            g.transform.localPosition = Vector3.up * 0.6f;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.78f, 0.35f); l.range = 6f; l.intensity = 2.2f; l.shadows = LightShadows.None;
        }

        private static Sprite RescueIcon()
        {
            if (_rescueIcon != null) return _rescueIcon;
            Texture2D t = OrdersMenu.PicOrNone("n-rescue");
            if (t == null) return null;
            _rescueIcon = Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), new Vector2(0.5f, 0.5f));
            return _rescueIcon;
        }

        // a guard's mark: a steel-blue medallion with a shield and a sword (tools/npc_icons.html)
        private static Sprite _guardIcon;
        private static Sprite GuardIcon()
        {
            if (_guardIcon != null) return _guardIcon;
            Texture2D t = OrdersMenu.PicOrNone("n-pin-guard");
            if (t == null) return Icon();
            _guardIcon = Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), new Vector2(0.5f, 0.5f));
            return _guardIcon;
        }

        // the mark: a small medallion with a person (tools/npc_icons.html)
        private static Sprite Icon()
        {
            if (_icon != null) return _icon;
            Texture2D t = OrdersMenu.PicOrNone("n-pin");
            if (t == null) return null;
            _icon = Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), new Vector2(0.5f, 0.5f));
            return _icon;
        }
    }
}
#endif
