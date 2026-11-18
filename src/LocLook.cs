#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A place's state laid on its loaded pieces, each second round you: shown or hidden, lit or out, strength, tint. The whole story: the head of LocState.cs.
    internal static partial class LocState
    {
        // every piece looked at anew (a layer changed by the author)
        // (the fires kept out are not forgotten here: Lights puts each in or out again as its piece is looked at — no
        // moment of a ruin's fires burning while another place's change makes every piece be looked at anew)
        internal static void Again() { _laid.Clear(); }

        // this place's only: its state changed — the others keep what they have (a fire of its own fading goes on: LitWhile sees to Lit)
        private static void AgainOf(Loc l) { _laid.Remove(l.Id); }

        // a piece of a place taken up as a prop (StoryProps): hidden while a hand holds it, whatever its state; let go, its place lays it
        // again by its state. The one owner of a piece's look: two hid and showed it each by itself, and the last one won — the ghost's
        // book back on the table after his house turned ruin for the player (2026-11-07)
        private static readonly HashSet<int> _held = new HashSet<int>();
        internal static void Held(ZNetView nv, bool held)
        {
            if (nv == null || !nv.IsValid()) return;
            int id = nv.GetInstanceID();
            if (held) { if (_held.Add(id)) Show(_hidden, nv, false); return; }
            if (!_held.Remove(id)) return;
            Show(_hidden, nv, WantsShown(nv)); // (at once, this piece only: its look goes this same moment, so nothing blinks — and no place of 2000 pieces laid anew for it)
        }

        // a piece of a place by its layer in that state — and a chest with things in it whatever its layer says (the user 2026-11-17: the Hall's
        // chest stood from 1.2 in 2.9 and now only in 1.4 «літ» — what a player put in it before is never hidden from him; emptied, it goes by its
        // layer from the next time its place is laid: a change of state, the world loaded). Only its look: for the story and the people it is
        // where its layer says (Absent — so «skrynia» of a later state is that state's chest, not the one kept for its things). Its things are
        // read from its ZDO, as the game's Container.Load reads them (every game the same; its own inventory fills a moment after it is laid):
        // «items» a byte array, Inventory.Save's version (int), then the count (ushort; an int before Version.Item.Smaller). Asked only of a
        // piece its layer hides — a handful a place
        internal static bool PieceShows(ZNetView nv, string layer, int state, int sub)
        {
            if (Shows(layer, state, sub)) return true;
            if (nv == null || !nv.IsValid() || nv.GetComponent<Container>() == null) return false;
            byte[] items = nv.GetZDO().GetByteArray(ZDOVars.s_items);
            if (items == null || items.Length < 6) return false; // (none, or an empty inventory: version and count alone)
            try { ZPackage p = new ZPackage(items); Version.Item v = (Version.Item)p.ReadInt(); return (v >= Version.Item.Smaller ? p.ReadUShort() : p.ReadInt()) > 0; }
            catch (Exception e) { Plugin.Instance.Log("LocState: a chest's things not read (" + e.Message + "): it goes by its layer"); return false; }
        }

        // this game's player sees this piece now: its layer in its place's state, and no «hidewhile» on its name. The author's hand-built one: always
        internal static bool WantsShown(ZNetView nv)
        {
            if (nv == null || !nv.IsValid() || !Blueprint.Copy(nv)) return true; // blueprint only: layers of its place's state (an inside has none)
            Loc l = Locs.Get(nv.GetZDO().GetString(LocParts.KOf, ""));
            if (l == null) return true;
            if (!PieceShows(nv, LocParts.LayerOf(l.Id, nv), StateOf(l), SubOf(l))) return false;
            Player me = Player.m_localPlayer;
            if (me == null || l.HideWhile.Count == 0) return true;
            string name = LocParts.NameOf(l.Id, nv);
            foreach (KeyValuePair<string, string> w in l.HideWhile) if (w.Key == name && StoryCond.Holds(me, w.Value, e => l.Log("hidewhile: " + e))) return false;
            return true;
        }

        // The game's smoke is loose puffs, not a piece's (they hang ~10 s and gather under a roof): on a change of state
        // the puffs within r m of its middle fade at once — a hidden pot or a fire put out leaves no smoke behind; what
        // still burns puffs anew. Once per change (all the world's smoke looked through)
        internal static void ClearSmoke(Vector3 at, float r)
        {
            foreach (Smoke s in UnityEngine.Object.FindObjectsByType<Smoke>(FindObjectsSortMode.None)) // heavy: once per change of a place's state (unsorted: the sort was all its cost)
            {
                if (s == null) continue;
                Vector3 d = s.transform.position - at; d.y = 0f;
                if (d.sqrMagnitude <= r * r) s.StartFadeOut();
            }
        }

        // each place once a second, one at a time, not all in one frame (each may look over every loaded piece: LocParts) — but in the first
        // frame you are in the world (come in, back from death, a new character) every place near at once, while the loading screen still
        // covers it: its pieces come in as the blueprint has them, and waiting for its turn they would hide and break before your eyes
        private static int _turn;
        private static Player _seen; // (the player the places were last laid for: a new one — all of them now)
        private static bool _wasAway; // (it was teleporting: out of a portal — all of them now too)
        internal static void Tick()
        {
            Player me = Player.m_localPlayer;
            List<Loc> all = Locs.All;
            bool away = me != null && me.IsTeleporting();
            if (me != null && !away && (me != _seen || _wasAway) && all.Count > 0)
            {
                _seen = me; _wasAway = false;
                foreach (Loc each in all) { TickOne(each, me); foreach (Action<Loc, Player> part in LocModules.Near) part(each, me); } // heavy: once as you come into the world or out of a portal — each place within Reach over its loaded pieces
                _next = Time.time + Every / all.Count;
                return;
            }
            if (away) _wasAway = true;
            if (Time.time < _next) return;
            if (all.Count == 0) return;
            _next = Time.time + Every / all.Count;
            if (me == null) return;
            if (++_turn >= all.Count) { _turn = 0; CloseDoors(); Prune(); }
            Loc l = all[_turn];
            TickOne(l, me);
            foreach (Action<Loc, Player> part in LocModules.Near) part(l, me); // (its parts look on the author's own building too)
        }

        // the pieces long gone: their numbers let go. What is hidden now stays remembered, or it would never come back
        private static void Prune()
        {
            foreach (Laid d in _laid.Values) if (d.Shown.Count > 50000) { d.Shown.Clear(); d.Healed.Clear(); }
            if (_hidden.Count <= 5000) return;
            List<int> dead = new List<int>();
            foreach (KeyValuePair<int, List<Component>> h in _hidden) if (h.Value.TrueForAll(c => c == null)) dead.Add(h.Key);
            foreach (int id in dead) _hidden.Remove(id);
        }

        private static void TickOne(Loc l, Player me)
        {
            if (!l.Built) return;
            Vector3 at; Quaternion rot;
            if (!l.Where(out at, out rot)) return;
            Vector3 d = me.transform.position - at; d.y = 0f;
            if (d.magnitude > Reach) return;
            int state = StateOf(l), sub = SubOf(l), was;
            if (!_states.TryGetValue(l.Id, out was) || was != state * 100 + sub)
            {
                if (_states.ContainsKey(l.Id)) { ClearSmoke(at, l.Reach() + 10f); LookVersion++; } // (a change: the smoke of what is gone does not hang on)
                _states[l.Id] = state * 100 + sub;
                AgainOf(l);
            }
            bool ruined = Ruined(l, state);
            Laid laid = LaidOf(l);
            bool mine = l.StateWhile.Count > 0; // (each player's own state: its strength drawn here only, never written — Loc.StateWhile)
            foreach (ZNetView nv in LocParts.All(l.Id))
            {
                int id = nv.GetInstanceID();
                bool on = true;
                if (!laid.Shown.Contains(id))
                {
                    on = PieceShows(nv, LocParts.LayerOf(l.Id, nv), state, sub) && !_held.Contains(id); // (in a hand: hidden whatever its state)
                    Show(_hidden, nv, on);
                    Guides(nv);
                    Lights(_dimmed, Unlit, nv, on, ruined, l, state, sub);
                    if (on) Tint(l, nv);
                    if (on && l.LockStations) DeadStations(nv);
                    laid.Shown.Add(id);
                    if (!on) laid.Healed.Add(id); // (hidden: its strength no matter)
                    if (on && nv.GetComponent<Door>() != null && !_doors.Contains(nv)) _doors.Add(nv);
                    if (l.LitWhile.Count > 0) { Fireplace fp = nv.GetComponent<Fireplace>(); List<Fireplace> fl; if (fp != null) { if (!_fires.TryGetValue(l.Id, out fl)) _fires[l.Id] = fl = new List<Fireplace>(); if (!fl.Contains(fp)) fl.Add(fp); } }
                    if (on && l.LockDoors.Length > 0 && nv.GetComponent<Door>() != null && nv.GetComponent<LocDoorView>() == null) nv.gameObject.AddComponent<LocDoorView>(); // (shut as his story has it)
                }
                if (!laid.Healed.Contains(id) && Heal(nv, Broken(l, nv, state, sub), mine)) laid.Healed.Add(id);
            }
            if (l.LitWhile.Count > 0) LitWhile(l, me);
            if (l.HideWhile.Count > 0) HideWhile(l, me, state, sub);
        }

        // a place's named pieces gone for you while the story says so (Loc.HideWhile: the Hall's old letter once you have read it);
        // after the pieces' own look above, in the same tick: a piece laid anew is hidden again before it is ever drawn. Only the
        // few named pieces, by their names (no look through all of them)
        private static readonly Dictionary<string, bool> _gone = new Dictionary<string, bool>(); // (one pass's: made once)
        private static void HideWhile(Loc l, Player me, int state, int sub)
        {
            _gone.Clear();
            foreach (KeyValuePair<string, string> w in l.HideWhile)
            {
                bool hide = StoryCond.Holds(me, w.Value, e => l.Log("hidewhile: " + e)); // (said once; a wrong line hides nothing)
                bool was;
                _gone[w.Key] = (_gone.TryGetValue(w.Key, out was) && was) || hide;
            }
            foreach (KeyValuePair<string, bool> g in _gone)
                foreach (ZNetView nv in LocParts.Named(l.Id, g.Key))
                {
                    bool want = !g.Value && PieceShows(nv, LocParts.LayerOf(l.Id, nv), state, sub) && !_held.Contains(nv.GetInstanceID());
                    if (want == !_hidden.ContainsKey(nv.GetInstanceID())) continue;
                    Show(_hidden, nv, want);
                    if (!want) LaidOf(l).Healed.Add(nv.GetInstanceID()); // (hidden: its strength no matter)
                }
        }

        // a place's fires that burn for you while the story says so (Loc.LitWhile: the ghost's hearth till the ghost is gone). Its fires only
        // (_fires: found once as each piece is laid, not a look through every piece each second)
        private static readonly Dictionary<string, List<Fireplace>> _fires = new Dictionary<string, List<Fireplace>>();
        private static void LitWhile(Loc l, Player me)
        {
            List<Fireplace> fires;
            if (!_fires.TryGetValue(l.Id, out fires)) return;
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                Fireplace f = fires[i];
                if (f == null) { fires.RemoveAt(i); continue; }
                string prefab = Utils.GetPrefabName(f.gameObject);
                bool burn = false, any = false;
                foreach (KeyValuePair<string, string> w in l.LitWhile)
                {
                    if (w.Key != prefab) continue;
                    any = true;
                    if (StoryCond.Holds(me, w.Value, e => l.Log("litwhile: " + e))) burn = true;
                }
                if (!any) continue;
                int id = f.GetInstanceID();
                if (burn) { Lit.Add(id); FireFade.Cancel(f); }
                else if (Lit.Contains(id) && !FireFade.Going(f)) FireFade.Out(f, Fades.Fire, () => Lit.Remove(id)); // (smooth by default: it burns down slowly, then it is out)
            }
        }

        // a piece of a place drawn broken at this step (the ruin's strength: the game's broken look) or whole. As its state has it (Ruined),
        // but for a step its «worn:» line names (Loc.Worn: «worn: 1.4 0.5 on wall roof»; the user 2026-11-17: the Hall's last abandoned step,
        // «літ» — Lit has already begun, «він все таки вже щось встиг зробити»: some walls and the roof still critical, the rest mended): there
        // only that part of the pieces whose prefab has one of its words and the ruin's own debris (layer «ruin»), all else whole.
        // Which: by patches — cubes of WornCell m of the blueprint's own frame, each drawn 0..1 by the place's name and its cell (the
        // same in every world and game), none marked by hand. Its lights, mist and all else as its state has them. Asked once a piece
        // is laid (Healed); the author's look (LocPreview) each second, his game only
        internal const float WornCell = 4f;
        internal static bool Broken(Loc l, ZNetView nv, int state, int sub)
        {
            float part = l != null && l.Worn.Count > 0 ? l.WornShare(state, sub) : -1f;
            if (part < 0f) return Ruined(l, state);
            string prefab = Utils.GetPrefabName(nv.gameObject), of; Vector3 at;
            if (!LocParts.Place(nv, out of, out at) || of != l.Id) return Ruined(l, state); // (not in this place's frame: as its state has it)
            if (MainOf(LocParts.LayerAt(of, prefab, at)) == "ruin") return true; // (the ruin's own debris — fallen beams, rubble — broken whatever is mended)
            if (part == 0f) return false;
            bool kind = false;
            foreach (string w in l.WornOn) if (prefab.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) { kind = true; break; }
            return kind && WornDraw(l.Id, at) < part;
        }
        // a patch's draw (FNV over the place's name and the cell, then mixed: no string.GetHashCode, not the same on every runtime)
        private static float WornDraw(string id, Vector3 at)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in id) h = (h ^ c) * 16777619u;
                h = (h ^ (uint)Mathf.FloorToInt(at.x / WornCell)) * 16777619u;
                h = (h ^ (uint)Mathf.FloorToInt(at.y / WornCell)) * 16777619u;
                h = (h ^ (uint)Mathf.FloorToInt(at.z / WornCell)) * 16777619u;
                h ^= h >> 16; h *= 2246822507u; h ^= h >> 13; h *= 3266489909u; h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }
        // a ruin still (strength, mist, fires out): its states 1..RuinTo (locations.list «ruin:»)
        internal static bool Ruined(Loc l, int state) { return l != null && state <= l.RuinTo; }

        // The Hall's lights: while it is a ruin its fire is out (the user 2026-11-01) — every fire of the game's in it
        // (candles, braziers, hearths, torches: Fireplace) does not burn (LocFirePatch), however much fuel; a lamp without
        // one (a dvergr lantern: a light and its glow) is put out — but those the place keeps lit in its ruin (Loc.RuinLit:
        // the Hall's candles, for the story; the user 2026-11-01). Hidden pieces burn neither. The Hearth of the Watch
        // keeps its own fires (LocHearth).
        internal static void Lights(Dictionary<int, List<Component>> dimmed, HashSet<int> unlit, ZNetView nv, bool on, bool ruined, Loc l, int state, int sub)
        {
            if (ruined && LitInRuin(l, nv, state, sub)) ruined = false; // (the story's few: candles burning in the dark Hall, its hearth once kindled)
            Fireplace f = nv.GetComponent<Fireplace>();
            if (f != null)
            {
                if (!on || ruined) unlit.Add(f.GetInstanceID()); else unlit.Remove(f.GetInstanceID());
                return;
            }
            if (nv.GetComponent<LocHearth>() != null) return;
            int id = nv.GetInstanceID();
            List<Component> off;
            if (!on) return; // (hidden: Show has it all off — what a ruin put out stays out with it, no light nor smoke from a hidden piece)
            if (!ruined)
            {
                if (!dimmed.TryGetValue(id, out off)) return;
                for (int i = off.Count - 1; i >= 0; i--) Enable(off[i], true);
                dimmed.Remove(id);
                return;
            }
            if (dimmed.ContainsKey(id) || nv.GetComponentInChildren<Light>(true) == null) return; // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            off = new List<Component>();
            Keepers(nv, off);
            foreach (Light lt in nv.GetComponentsInChildren<Light>(true)) if (lt.enabled) off.Add(lt); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            foreach (ParticleSystemRenderer r in nv.GetComponentsInChildren<ParticleSystemRenderer>(true)) if (r.enabled) off.Add(r); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            off.RemoveAll(WsItems.IsGlint); // (what must be found shines in a ruin too: the old letter was put out as a lamp, 2026-11-04)
            foreach (Component c in off) Enable(c, false);
            dimmed[id] = off;
        }

        // a piece the place recolours (Loc.Tints): its colour laid over its looks (here only, nothing written)
        private static MaterialPropertyBlock _tint;
        internal static bool Tint(Loc l, ZNetView nv)
        {
            Color c;
            if (l == null || l.Tints.Count == 0 || !l.Tints.TryGetValue(Utils.GetPrefabName(nv.gameObject), out c)) return false;
            if (_tint == null) _tint = new MaterialPropertyBlock();
            foreach (Renderer r in nv.GetComponentsInChildren<Renderer>(true)) // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            {
                if (r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(_tint);
                _tint.SetColor("_Color", c);
                r.SetPropertyBlock(_tint);
            }
            return true;
        }

        // its own colour back (the author's look ended)
        internal static void Untint(ZNetView nv)
        {
            if (nv == null) return;
            foreach (Renderer r in nv.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) r.SetPropertyBlock(null); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
        }

        internal static bool LitInRuin(Loc l, ZNetView nv, int state, int sub)
        {
            if (l == null || (l.RuinLit.Length == 0 && l.LitFrom.Count == 0)) return false;
            string prefab = Utils.GetPrefabName(nv.gameObject);
            return System.Array.IndexOf(l.RuinLit, prefab) >= 0 || l.LitAt(prefab, state, sub);
        }

        internal static void UndimAll(Dictionary<int, List<Component>> dimmed)
        {
            foreach (List<Component> off in dimmed.Values) for (int i = off.Count - 1; i >= 0; i--) Enable(off[i], true);
            dimmed.Clear();
        }

        // what turns a light back on by itself (the game's distance fade, its flicker) or keeps smoking: off first
        private static void Keepers(ZNetView nv, List<Component> into)
        {
            foreach (LightLod k in nv.GetComponentsInChildren<LightLod>(true)) if (k.enabled) into.Add(k); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            foreach (LightFlicker k in nv.GetComponentsInChildren<LightFlicker>(true)) if (k.enabled) into.Add(k); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            foreach (SmokeSpawner k in nv.GetComponentsInChildren<SmokeSpawner>(true)) if (k.enabled) into.Add(k); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
        }

        // hidden: what was on is put off and remembered; shown again: just that put back on (what the game keeps off
        // stays off)
        internal static void Show(Dictionary<int, List<Component>> hidden, ZNetView nv, bool on)
        {
            int id = nv.GetInstanceID();
            List<Component> off;
            if (on)
            {
                if (!hidden.TryGetValue(id, out off)) return; // (never hidden: left as the game made it)
                for (int i = off.Count - 1; i >= 0; i--) Enable(off[i], true); // (the lights before what fades them)
                hidden.Remove(id);
                return;
            }
            if (hidden.TryGetValue(id, out off))
            {
                // an item stand hidden: the thing on it is made later by the stand (when the world loads, or anew) — put off as it
                // comes, all of it as the stand itself (LocStandItemPatch: the moment the stand makes it). Its colliders too: with only
                // its looks off, a hidden stand of another state was still pointed at, and E reached it (the user 2026-11-08)
                if (nv.GetComponent<ItemStand>() == null) return;
                List<Component> come = new List<Component>();
                foreach (Renderer r in nv.GetComponentsInChildren<Renderer>(true)) if (r.enabled) come.Add(r); // heavy: once when a hidden stand makes its thing
                foreach (Collider c in nv.GetComponentsInChildren<Collider>(true)) if (c.enabled) come.Add(c); // heavy: once when a hidden stand makes its thing
                foreach (Light l in nv.GetComponentsInChildren<Light>(true)) if (l.enabled) come.Add(l); // heavy: once when a hidden stand makes its thing
                foreach (AudioSource a in nv.GetComponentsInChildren<AudioSource>(true)) if (a.enabled) come.Add(a); // heavy: once when a hidden stand makes its thing
                foreach (Component c in come) Enable(c, false);
                off.RemoveAll(c => c == null); // (the thing it made before, gone with the new one)
                off.AddRange(come);
                return;
            }
            off = new List<Component>();
            Keepers(nv, off);
            foreach (Renderer r in nv.GetComponentsInChildren<Renderer>(true)) if (r.enabled) off.Add(r); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            foreach (Collider c in nv.GetComponentsInChildren<Collider>(true)) if (c.enabled) off.Add(c); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            foreach (Light l in nv.GetComponentsInChildren<Light>(true)) if (l.enabled) off.Add(l); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            foreach (AudioSource a in nv.GetComponentsInChildren<AudioSource>(true)) if (a.enabled) off.Add(a); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only
            foreach (CraftingStation s in nv.GetComponentsInChildren<CraftingStation>(true)) off.Add(s);   // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only — (a hidden workbench is no station: not learnt by
            foreach (StationExtension x in nv.GetComponentsInChildren<StationExtension>(true)) off.Add(x); // heavy: once when a piece's look changes (laid, hidden, shown); the author's preview: each second, his machine only —  walking by, nothing built or made «near» it)
            foreach (Component c in off) Enable(c, false);
            hidden[id] = off;
        }

        // the game's own raven lands on a piece with a guide point (a workbench) and explains it: never on a piece of a place
        // of the story, whatever its state (taken out of the raven's list as each is laid; a copy that comes later: LocGuidePatch)
        private static void Guides(ZNetView nv)
        {
            foreach (GuidePoint g in nv.GetComponentsInChildren<GuidePoint>(true)) Raven.UnregisterStaticText(g.m_text); // heavy: once when a piece is laid
        }

        // a place that keeps no stations (Loc.LockStations): its workbench is out of the game's lists — nothing built,
        // made or learnt near it; E on it does nothing (LocStationPatch); its ring on the ground never shown
        internal static void DeadStations(ZNetView nv)
        {
            foreach (CraftingStation s in nv.GetComponentsInChildren<CraftingStation>(true)) // heavy: once when the piece is laid
            {
                Enable(s, false); // (out of the game's list too, so nothing is built by it)
                if (s.m_areaMarker != null) s.m_areaMarker.SetActive(false); // (its build ring: the station switched off before its own Start hid it would show it for good)
            }
            foreach (StationExtension x in nv.GetComponentsInChildren<StationExtension>(true)) Enable(x, false); // heavy: once when the piece is laid
        }

        // a piece of a place its world's state has not (by its layer, whatever this game has drawn yet): nothing for a person to use. Its Chair and
        // Container stay on it, only its looks and touch go off, so whoever looks for them asks this (NpcBase, NpcMind)
        internal static bool Absent(ZNetView nv)
        {
            if (!Blueprint.Copy(nv)) return false; // blueprint only: layers of its place's state (an inside has none)
            Loc l = Locs.Get(nv.GetZDO().GetString(LocParts.KOf, ""));
            return l != null && !Shows(LocParts.LayerOf(l.Id, nv), StateOf(l), SubOf(l)); // (by its layer alone: a chest kept in sight for a player's things is not the story's — PieceShows)
        }

        // goes up when any place changed its state here: what was gathered of its pieces is looked over again (NpcBase)
        internal static int LookVersion { get; private set; }

        // a piece of a place hidden now (its state has it not): no comfort from it either (LocComfortPatch)
        internal static bool AnyHidden { get { return _hidden.Count > 0; } }
        internal static bool IsHidden(Piece pc) { ZNetView nv = pc != null ? pc.GetComponent<ZNetView>() : null; return nv != null && _hidden.ContainsKey(nv.GetInstanceID()); }

        // a hidden stand has just made the thing it holds: that hidden too (in the world's places, or the author's look)
        internal static void StandMade(ZNetView nv)
        {
            if (nv == null) return;
            int id = nv.GetInstanceID();
            if (_hidden.ContainsKey(id)) Show(_hidden, nv, false);
            LocPreview.StandMade(nv);
        }

        // all hidden ones shown again
        internal static void ShowAll(Dictionary<int, List<Component>> hidden)
        {
            foreach (List<Component> off in hidden.Values) for (int i = off.Count - 1; i >= 0; i--) Enable(off[i], true);
            hidden.Clear();
        }

        // the game's own lists of stations and their extensions (what «near a workbench» and learning one look in)
        private static readonly FieldInfo AllStations = AccessTools.Field(typeof(CraftingStation), "m_allStations");
        private static readonly FieldInfo AllExtensions = AccessTools.Field(typeof(StationExtension), "m_allExtensions");

        private static void Listed(FieldInfo list, Component c, bool on)
        {
            System.Collections.IList l = list != null ? list.GetValue(null) as System.Collections.IList : null;
            if (l == null) return;
            if (!on) l.Remove(c);
            else if (!l.Contains(c)) l.Add(c);
        }

        private static void Enable(Component c, bool on)
        {
            if (c == null) return;
            if (c is CraftingStation) { Listed(AllStations, c, on); ((Behaviour)c).enabled = on; return; }
            if (c is StationExtension) { Listed(AllExtensions, c, on); ((Behaviour)c).enabled = on; return; }
            Renderer r = c as Renderer; if (r != null) { r.enabled = on; return; }
            Collider k = c as Collider; if (k != null) { k.enabled = on; return; }
            Behaviour b = c as Behaviour; if (b != null) b.enabled = on;
        }

        // this state's strength in its ZDO (by its owner), and its look redrawn here; false: not yet (another owns it
        // and has not written it yet)
        private static bool Heal(ZNetView nv, bool ruined, bool mine)
        {
            WearNTear w = nv.GetComponent<WearNTear>();
            if (w == null) return true;
            if (mine) { Draw(w, ruined ? Ruin : 1f); return true; } // (a state of his own: another player may see it whole)
            float want = (ruined ? Ruin : 1f) * w.m_health;
            ZDO z = nv.GetZDO();
            float now = z.GetFloat(ZDOVars.s_health, w.m_health);
            if (Mathf.Abs(now - want) > 0.5f)
            {
                if (!nv.IsOwner()) return false;
                z.Set(ZDOVars.s_health, want);
                now = want;
            }
            Draw(w, now / w.m_health);
            return true;
        }

        // a piece drawn at this part of its strength, here only and quietly (the game's own look for it)
        internal static void Draw(WearNTear w, float pct)
        {
            if (w == null || HealthShown == null || Redraw == null) return;
            pct = Mathf.Clamp01(pct);
            if (Mathf.Abs((float)HealthShown.GetValue(w) - pct) < 0.001f) return;
            HealthShown.SetValue(w, pct);
            Redraw.Invoke(w, new object[] { false });
        }

    }
}
#endif
