#if RAVEN_ITEM && RAVEN_NPC
// (the Hall of the Watch's states: abandoned from 2.9, the others with the story in 3.0 — the user 2026-11-01)
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The Hall's states. The blueprint is the whole Hall at its best; a state is laid on the same pieces, never built
    // anew, and is one for the world (world keys «ravenwatch_hall_state <n>» and «…_sub <k>»: the story runs on the
    // server, as the game's own). 1 — abandoned (2.9: the only one players see), 2 — repaired, 3 — lived in (the story,
    // 3.0; the user 2026-11-01). A state has sub-states, its own steps in order (1.0 — its start; the Hall's 1.1 «гейд»:
    // its hearth first lit, Heid clears away what points to her; 1.2 «розпалена»: the Watch's fire, the chest for the
    // wood): the author adds them in his window (Loc.Subs; the user 2026-11-01: states and sub-states clearly apart).
    //   · strength: abandoned — every piece at Ruin of it, so the game shows its own broken look (≤ 25%: cracked beams,
    //     holes; in its sub-states too); from repaired — whole. The owner of each piece writes it into its ZDO (kept, seen by all); every game
    //     redraws the look quietly (no crack or splinters for two thousand pieces at once). It never falls: Blueprint.
    //   · layers (HallParts: <building>.layers.txt in the mod, never in the world): a piece's states — «ruin» only while
    //     abandoned (rubble, fallen beams, bushes inside), «repaired» from 2, «fixed» only while repaired, «lived» from 3
    //     (food on the stands, banners) — and its sub-states' marks after commas: «+1.2» — there only in sub-state 1.2
    //     (the chest; its state none), «-1.1» — gone in 1.1 (as «ruin,-1.1»), «>2.2» — in its state from 2.2 on (not at
    //     its start nor in 2.1). Not theirs:
    //     hidden — their looks, colliders, lights and sounds off — so a later state needs no building.
    // The story never switches it in sight: it asks, and the change waits behind the scenes (below).
    // Every place of the story (Loc) has states the same way — its own count, names and sub-states, its own world keys;
    // the layers and their numbers are the Hall's (a place with fewer states uses the first of them). «Ruined» is the
    // place's own (Loc.RuinTo: the Hall 1).
    internal static class HallState
    {
        internal const int Abandoned = 1, Repaired = 2, Lived = 3;
        internal const int HeidSub = 1, LitSub = 2; // the Hall's 1.1 «гейд», 1.2 «розпалена»
        internal const float Ruin = 0.1f; // of a piece's strength while abandoned (the game's broken look: ≤ 0.25)
        internal static readonly string[] Layers = { "ruin", "repaired", "lived", "fixed" };
        private const string OldLit = "lit"; // (the Hall's layer before sub-states: now «+1.2», there only while kindled)
        private const float Every = 1f, Reach = 200f; // (its pieces are loaded only round a player)

        private static readonly FieldInfo HealthShown = AccessTools.Field(typeof(WearNTear), "m_healthPercentage");
        private static readonly MethodInfo Redraw = AccessTools.Method(typeof(WearNTear), "UpdateVisual");
        private static readonly HashSet<int> _shown = new HashSet<int>();   // pieces laid for this state (by their layer)
        private static readonly HashSet<int> _healed = new HashSet<int>();  // pieces whose strength is this state's
        private static readonly Dictionary<int, List<Component>> _hidden = new Dictionary<int, List<Component>>(); // pieces hidden now: what was put off
        private static readonly Dictionary<int, List<Component>> _dimmed = new Dictionary<int, List<Component>>(); // lamps put out while a ruin
        internal static readonly HashSet<int> Unlit = new HashSet<int>(); // the game's fires (Fireplace) kept out: hidden, or a ruin
        internal static readonly HashSet<int> Lit = new HashSet<int>();   // …and those kept burning for you, fuel or not (Loc.LitWhile)
        private static readonly Dictionary<string, int> _states = new Dictionary<string, int>(); // each place's state (×100 + sub-state) its pieces were laid for
        private static float _next;

        // the Hall's (most of the story asks for it)
        internal static int State { get { return StateOf(Locs.Hall); } }
        internal static int Sub { get { return SubOf(Locs.Hall); } }

        internal static int StateOf(Loc l)
        {
            string v; int s;
            if (l == null || ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(l.KeyState, out v) || !int.TryParse(v, out s)) return Abandoned;
            return Mathf.Clamp(s, Abandoned, l.Last);
        }

        // its state's sub-state (0 — the state itself)
        internal static int SubOf(Loc l)
        {
            string v; int k;
            if (l == null || ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(l.KeySub, out v) || !int.TryParse(v, out k)) return 0;
            return Mathf.Clamp(k, 0, l.SubsOf(StateOf(l)));
        }

        // ---------------------------------------------------------------- layers: a state's part and sub-states' marks
        // «ruin,-1.1» → «ruin» and [«-1.1»]; «+1.2» → «» and [«+1.2»]; the old «lit» → «» and [«+1.2»]. Read once per
        // text (a few dozen kinds in a whole place; looked at for every piece every second while the author looks)
        private sealed class Parsed
        {
            internal string Main = "";
            internal string[] Marks = new string[0];
            internal bool Only; // any «+»: only in sub-states, its state's part means nothing then
        }
        private static readonly Dictionary<string, Parsed> _parsed = new Dictionary<string, Parsed>();

        private static Parsed Parse(string layer)
        {
            layer = layer ?? "";
            Parsed p;
            if (_parsed.TryGetValue(layer, out p)) return p;
            p = new Parsed();
            List<string> marks = new List<string>();
            if (layer == OldLit) marks.Add(Mark(true, Abandoned, LitSub));
            else
                foreach (string one in layer.Split(','))
                {
                    if (one.Length == 0) continue;
                    if (one[0] == '+' || one[0] == '-' || one[0] == '>') { if (!marks.Contains(one)) marks.Add(one); }
                    else if (p.Main.Length == 0) p.Main = one;
                }
            p.Marks = marks.ToArray();
            foreach (string m in p.Marks) if (m[0] == '+') p.Only = true;
            if (_parsed.Count > 1000) _parsed.Clear(); // (the author's tries: never many)
            _parsed[layer] = p;
            return p;
        }

        internal static string MainOf(string layer) { return Parse(layer).Main; }
        internal static bool HasMark(string layer, string mark) { return System.Array.IndexOf(Parse(layer).Marks, mark) >= 0; }
        internal static bool SubOnly(string layer) { return Parse(layer).Only; }
        internal static bool HasAnyMark(string layer) { return Parse(layer).Marks.Length > 0; }

        internal static string Mark(bool there, int state, int sub) { return (there ? "+" : "-") + state + "." + sub; }

        private static string Compose(string main, IEnumerable<string> marks)
        {
            string t = main == "-" ? "" : main ?? "";
            foreach (string m in marks) t += (t.Length > 0 ? "," : "") + m;
            return t;
        }

        // the layer with its state's part changed (its sub-states' marks kept)
        internal static string WithMain(string layer, string main) { return Compose(main, Parse(layer).Marks); }

        // the layer with its mark for this sub-state changed: «+» there only, «-» gone, null — no mark for it
        internal static string WithMark(string layer, int state, int sub, bool? there)
        {
            Parsed p = Parse(layer);
            List<string> marks = new List<string>(p.Marks);
            marks.Remove(Mark(true, state, sub));
            marks.Remove(Mark(false, state, sub));
            if (there.HasValue) marks.Add(Mark(there.Value, state, sub));
            return Compose(p.Main, marks);
        }

        // a piece of one state made one of its steps' only: its state's part and that state's «-» and «>» marks gone, its «+»
        // marks kept (there in those steps too), this step's «+» added
        internal static string OnlyIn(string layer, int state, int sub)
        {
            string mine = "-" + state + ".", plus = Mark(true, state, sub);
            List<string> marks = new List<string>();
            foreach (string m in Parse(layer).Marks) if (!m.StartsWith(mine) && !m.StartsWith(">" + state + ".") && m != plus) marks.Add(m);
            marks.Add(plus);
            return Compose("", marks);
        }

        // its mark for this sub-state: true «+», false «-», null none
        internal static bool? MarkFor(string layer, int state, int sub)
        {
            if (HasMark(layer, Mark(true, state, sub))) return true;
            if (HasMark(layer, Mark(false, state, sub))) return false;
            return null;
        }

        internal static bool Shows(string layer, int state, int sub)
        {
            Parsed p = Parse(layer);
            if (p.Marks.Length == 0) return MainShows(p.Main, state);
            if (p.Only) return sub > 0 && HasMark(layer, Mark(true, state, sub));
            if (!MainShows(p.Main, state) || (sub > 0 && HasMark(layer, Mark(false, state, sub)))) return false;
            int from = FromOf(p, state);
            return sub >= from; // («>2.2»: not in that state's earlier steps)
        }

        // «>2.2» — there from that step of its state on (not at its start nor in the steps before); 0 — none
        internal static string From(int state, int sub) { return ">" + state + "." + sub; }
        private static int FromOf(Parsed p, int state)
        {
            string mine = ">" + state + ".";
            foreach (string m in p.Marks) { int s, k; if (m.StartsWith(mine) && SubNumber(m.Substring(1), out s, out k)) return k; }
            return 0;
        }
        internal static int FromOf(string layer, int state) { return FromOf(Parse(layer), state); }

        // the layer «from this step on» in its state (on), or without it (off)
        internal static string WithFrom(string layer, int state, int sub, bool on)
        {
            Parsed p = Parse(layer);
            string mine = ">" + state + ".";
            List<string> marks = new List<string>();
            foreach (string m in p.Marks) if (!m.StartsWith(mine)) marks.Add(m);
            if (on) marks.Add(From(state, sub));
            return Compose(p.Main, marks);
        }

        private static bool MainShows(string main, int state)
        {
            switch (main)
            {
                case "ruin": return state < Repaired;
                case "repaired": return state >= Repaired;
                case "lived": return state >= Lived;
                case "fixed": return state == Repaired; // (only while repaired: scaffolds, planks left over)
            }
            int n;
            if (Numbered(main, "only", out n)) return state == n;
            if (Numbered(main, "from", out n)) return state >= n;
            return true;
        }

        // the other places' layers, by their states' numbers: «only2» — in state 2 only, «from2» — from state 2 on
        internal static bool Numbered(string layer, string kind, out int n)
        {
            n = 0;
            return layer != null && layer.StartsWith(kind) && int.TryParse(layer.Substring(kind.Length), out n) && n >= 1;
        }

        internal static bool GoodLayer(string layer)
        {
            if (layer == OldLit) return true;
            int n, s, k;
            Parsed p = Parse(layer);
            if (p.Main.Length > 0 && System.Array.IndexOf(Layers, p.Main) < 0 && !Numbered(p.Main, "only", out n) && !Numbered(p.Main, "from", out n)) return false;
            foreach (string m in p.Marks) if (!SubNumber(m.Substring(1), out s, out k) || s < 1 || k < 1) return false;
            return p.Main.Length > 0 || p.Marks.Length > 0;
        }

        // «1.1» → 1, 1; «2» → 2, 0
        internal static bool SubNumber(string v, out int state, out int sub)
        {
            state = sub = 0;
            if (string.IsNullOrEmpty(v)) return false;
            int dot = v.IndexOf('.');
            if (dot < 0) return int.TryParse(v, out state);
            return int.TryParse(v.Substring(0, dot), out state) && int.TryParse(v.Substring(dot + 1), out sub) && sub >= 0;
        }

        internal static string MainWhen(string main)
        {
            switch (main)
            {
                case "": return "завжди";
                case "ruin": return "лише в закинутій";
                case "repaired": return "з відремонтованої";
                case "lived": return "з обжитої";
                case "fixed": return "лише у відремонтованій";
            }
            int n;
            if (Numbered(main, "only", out n)) return "лише в стані " + n;
            if (Numbered(main, "from", out n)) return "зі стану " + n;
            return main;
        }

        // in words, with the place's sub-states' names when it is known
        internal static string LayerWhen(string layer, Loc l = null)
        {
            Parsed p = Parse(layer);
            List<string> there = new List<string>(), gone = new List<string>(), from = new List<string>();
            foreach (string m in p.Marks)
            {
                int s, k;
                if (!SubNumber(m.Substring(1), out s, out k)) continue;
                (m[0] == '+' ? there : m[0] == '>' ? from : gone).Add(l != null ? "«" + l.WordOf(s, k) + "»" : s + "." + k);
            }
            if (there.Count > 0) return "лише в субстані " + string.Join(", ", there.ToArray());
            return MainWhen(p.Main) + (from.Count > 0 ? ", у своєму стані — з " + string.Join(", ", from.ToArray()) : "") + (gone.Count > 0 ? ", крім " + string.Join(", ", gone.ToArray()) : "");
        }

        // every piece looked at anew (a layer changed by the author)
        // (the fires kept out are not forgotten here: Lights puts each in or out again as its piece is looked at — no
        // moment of a ruin's fires burning while another place's change makes every piece be looked at anew)
        internal static void Again() { _shown.Clear(); _healed.Clear(); Lit.Clear(); } // (Lit: worked out again on the next look)

        // The game's smoke is loose puffs, not a piece's (they hang ~10 s and gather under a roof): on a change of state
        // the puffs within r m of its middle fade at once — a hidden pot or a fire put out leaves no smoke behind; what
        // still burns puffs anew. Once per change (all the world's smoke looked through)
        internal static void ClearSmoke(Vector3 at, float r)
        {
            foreach (Smoke s in Object.FindObjectsOfType<Smoke>())
            {
                if (s == null) continue;
                Vector3 d = s.transform.position - at; d.y = 0f;
                if (d.sqrMagnitude <= r * r) s.StartFadeOut();
            }
        }

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + Every;
            Player me = Player.m_localPlayer;
            if (me == null) return;
            if (_shown.Count > 50000) { Again(); _hidden.Clear(); } // (the pieces long gone)
            foreach (Loc l in Locs.All) { TickOne(l, me); if (l.Touches.Count > 0) LocTouch.Tick(l, me); } // (a touch: on the author's own building too)
            CloseDoors();
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
                if (_states.ContainsKey(l.Id)) ClearSmoke(at, l.Reach() + 10f); // (a change: the smoke of what is gone does not hang on)
                _states[l.Id] = state * 100 + sub;
                Again();
            }
            bool ruined = Ruined(l, state);
            foreach (ZNetView nv in HallParts.All(l.Id))
            {
                int id = nv.GetInstanceID();
                bool on = true;
                if (!_shown.Contains(id))
                {
                    on = Shows(HallParts.LayerOf(l.Id, nv), state, sub);
                    Show(_hidden, nv, on);
                    Lights(_dimmed, Unlit, nv, on, ruined, l, state, sub);
                    if (on) Tint(l, nv);
                    _shown.Add(id);
                    if (!on) _healed.Add(id); // (hidden: its strength no matter)
                    if (on && nv.GetComponent<Door>() != null && !_doors.Contains(nv)) _doors.Add(nv);
                }
                if (!_healed.Contains(id) && Heal(nv, ruined)) _healed.Add(id);
            }
            if (l.LitWhile.Count > 0) LitWhile(l, me);
        }

        // a place's fires that burn for you while the story says so (Loc.LitWhile: the ghost's hearth till the ghost is gone)
        private static readonly Dictionary<string, List<Cond>> _litConds = new Dictionary<string, List<Cond>>();
        private static void LitWhile(Loc l, Player me)
        {
            foreach (ZNetView nv in HallParts.All(l.Id))
            {
                Fireplace f = nv.GetComponent<Fireplace>();
                if (f == null) continue;
                string prefab = Utils.GetPrefabName(nv.gameObject);
                bool burn = false, any = false;
                foreach (KeyValuePair<string, string> w in l.LitWhile)
                {
                    if (w.Key != prefab) continue;
                    any = true;
                    List<Cond> c;
                    if (!_litConds.TryGetValue(w.Value, out c)) { List<string> errs = new List<string>(); c = StoryCond.Parse(w.Value, errs); if (errs.Count == 0) _litConds[w.Value] = c; else foreach (string e in errs) l.Log("litwhile: " + e); }
                    if (StoryCond.All(me, c)) burn = true;
                }
                if (!any) continue;
                int id = f.GetInstanceID();
                if (burn) { Lit.Add(id); FireFade.Cancel(f); }
                else if (Lit.Contains(id) && !FireFade.Going(f)) FireFade.Out(f, Fades.Fire, () => Lit.Remove(id)); // (smooth by default: it burns down slowly, then it is out)
            }
        }

        // a ruin still (strength, mist, fires out): the Hall abandoned or kindled; another place by its own list
        internal static bool Ruined(int state) { return Ruined(Locs.Hall, state); }
        internal static bool Ruined(Loc l, int state) { return l != null && state <= l.RuinTo; }

        // The Hall's lights: while it is a ruin its fire is out (the user 2026-11-01) — every fire of the game's in it
        // (candles, braziers, hearths, torches: Fireplace) does not burn (HallFirePatch), however much fuel; a lamp without
        // one (a dvergr lantern: a light and its glow) is put out — but those the place keeps lit in its ruin (Loc.RuinLit:
        // the Hall's candles, for the story; the user 2026-11-01). Hidden pieces burn neither. The Hearth of the Watch
        // keeps its own fires (HallHearth).
        internal static void Lights(Dictionary<int, List<Component>> dimmed, HashSet<int> unlit, ZNetView nv, bool on, bool ruined, Loc l, int state, int sub)
        {
            if (ruined && LitInRuin(l, nv, state, sub)) ruined = false; // (the story's few: candles burning in the dark Hall, its hearth once kindled)
            Fireplace f = nv.GetComponent<Fireplace>();
            if (f != null)
            {
                if (!on || ruined) unlit.Add(f.GetInstanceID()); else unlit.Remove(f.GetInstanceID());
                return;
            }
            if (nv.GetComponent<HallHearth>() != null) return;
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
            if (dimmed.ContainsKey(id) || nv.GetComponentInChildren<Light>(true) == null) return;
            off = new List<Component>();
            Keepers(nv, off);
            foreach (Light lt in nv.GetComponentsInChildren<Light>(true)) if (lt.enabled) off.Add(lt);
            foreach (ParticleSystemRenderer r in nv.GetComponentsInChildren<ParticleSystemRenderer>(true)) if (r.enabled) off.Add(r);
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
            foreach (Renderer r in nv.GetComponentsInChildren<Renderer>(true))
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
            foreach (Renderer r in nv.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) r.SetPropertyBlock(null);
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
            foreach (LightLod k in nv.GetComponentsInChildren<LightLod>(true)) if (k.enabled) into.Add(k);
            foreach (LightFlicker k in nv.GetComponentsInChildren<LightFlicker>(true)) if (k.enabled) into.Add(k);
            foreach (SmokeSpawner k in nv.GetComponentsInChildren<SmokeSpawner>(true)) if (k.enabled) into.Add(k);
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
                // an item stand hidden: the thing on it is made later by the stand (when the world loads, or anew) — its
                // looks put off as they come (HallStandItemPatch: the moment the stand makes it)
                if (nv.GetComponent<ItemStand>() == null) return;
                foreach (Renderer r in nv.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; off.Add(r); }
                return;
            }
            off = new List<Component>();
            Keepers(nv, off);
            foreach (Renderer r in nv.GetComponentsInChildren<Renderer>(true)) if (r.enabled) off.Add(r);
            foreach (Collider c in nv.GetComponentsInChildren<Collider>(true)) if (c.enabled) off.Add(c);
            foreach (Light l in nv.GetComponentsInChildren<Light>(true)) if (l.enabled) off.Add(l);
            foreach (AudioSource a in nv.GetComponentsInChildren<AudioSource>(true)) if (a.enabled) off.Add(a);
            foreach (CraftingStation s in nv.GetComponentsInChildren<CraftingStation>(true)) off.Add(s);   // (a hidden workbench is no station: not learnt by
            foreach (StationExtension x in nv.GetComponentsInChildren<StationExtension>(true)) off.Add(x); //  walking by, nothing built or made «near» it)
            foreach (Component c in off) Enable(c, false);
            hidden[id] = off;
        }

        // a hidden stand has just made the thing it holds: that hidden too (in the world's places, or the author's look)
        internal static void StandMade(ZNetView nv)
        {
            if (nv == null) return;
            int id = nv.GetInstanceID();
            if (_hidden.ContainsKey(id)) Show(_hidden, nv, false);
            HallPreview.StandMade(nv);
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
        private static bool Heal(ZNetView nv, bool ruined)
        {
            WearNTear w = nv.GetComponent<WearNTear>();
            if (w == null) return true;
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

        // ---------------------------------------------------------------- a change behind the scenes
        // The story asks for a state or a sub-state (Request) and it waits, the Hall shut (its doors closed and locked), until nobody
        // can see it come (the user 2026-11-01: no telling anyone out, it waits as long as it takes):
        //   inside (food, people, things in the hall) — nobody in it nor within NearIn m of its walls;
        //   outside (the whole building, e.g. mended) — nobody within NearOut m (it is not loaded for anyone), or all
        //   asleep (the night skipped).
        // The server looks (it knows where everyone is); the waiting change is a world key «<place>_next <n>[.<k>] in|out».
        internal const float NearIn = 40f, NearOut = 150f;
        private static readonly FieldInfo Sleeping = AccessTools.Field(typeof(Game), "m_sleeping");
        private static float _nextServer;

        internal static bool Pending(Loc l, out int next, out int nextSub, out bool outside)
        {
            next = nextSub = 0; outside = false;
            string v;
            if (l == null || ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(l.KeyNext, out v)) return false;
            string[] w = v.Split(' ');
            if (w.Length < 1 || !SubNumber(w[0], out next, out nextSub)) return false;
            outside = w.Length > 1 && w[1] == "out";
            next = Mathf.Clamp(next, Abandoned, l.Last);
            nextSub = Mathf.Clamp(nextSub, 0, l.SubsOf(next));
            return true;
        }

        internal static bool ShutOf(Loc l) { int n, k; bool o; return Pending(l, out n, out k, out o); }

        internal static void Request(Loc l, int next, int sub, bool outside)
        {
            if (l == null || ZoneSystem.instance == null) return;
            next = Mathf.Clamp(next, Abandoned, l.Last);
            sub = Mathf.Clamp(sub, 0, l.SubsOf(next));
            ZoneSystem.instance.SetGlobalKey(l.KeyNext + " " + next + (sub > 0 ? "." + sub : "") + " " + (outside ? "out" : "in"));
        }

        // the world's state and sub-state at once (a new state starts at its own 1.0)
        internal static void Put(Loc l, int state, int sub)
        {
            if (l == null || ZoneSystem.instance == null) return;
            state = Mathf.Clamp(state, Abandoned, l.Last);
            sub = Mathf.Clamp(sub, 0, l.SubsOf(state));
            ZoneSystem.instance.SetGlobalKey(l.KeyState + " " + state);
            if (sub > 0) ZoneSystem.instance.SetGlobalKey(l.KeySub + " " + sub); else ZoneSystem.instance.RemoveGlobalKey(l.KeySub);
        }

        internal static void ServerTick()
        {
            if (Time.time < _nextServer) return;
            _nextServer = Time.time + 2f;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            Renumber();
            foreach (Loc l in Locs.All) ServerOne(l);
        }

        // a world from before the sub-states (the author's tests; players had only 1): the Hall's old 2 «розпалена» → 1.2,
        // 3 → 2, 4 → 3 — once, when its keys are surely read (the Hall built: its own key)
        private const string Renumbered = "ravenwatch_hall_renumbered";
        private static void Renumber()
        {
            Loc l = Locs.Hall;
            if (l == null || !l.Built || ZoneSystem.instance.GetGlobalKey(Renumbered)) return;
            string v; int s;
            if (ZoneSystem.instance.GetGlobalKey(l.KeyState, out v) && int.TryParse(v, out s) && s >= 2)
            {
                int now = s == 2 ? Abandoned : s - 1, sub = s == 2 ? LitSub : 0;
                Put(l, now, sub);
                l.Log("old state " + s + " is now " + l.WordOf(now, sub));
            }
            ZoneSystem.instance.RemoveGlobalKey(l.KeyNext);
            ZoneSystem.instance.SetGlobalKey(Renumbered);
        }

        private static void ServerOne(Loc l)
        {
            if (!l.Built) return;
            int next, nextSub; bool outside;
            if (!Pending(l, out next, out nextSub, out outside)) { if (l.IsHall) Kindle(); else if (l.Follows == HallSpawn.Name) SyncFollower(l); return; }
            float near = outside ? NearOut : NearIn, nearest = float.MaxValue;
            foreach (ZDO z in ZNet.instance.GetAllCharacterZDOS()) if (z != null) nearest = Mathf.Min(nearest, l.From(z.GetPosition()));
            bool asleep = outside && Game.instance != null && Sleeping != null && (bool)Sleeping.GetValue(Game.instance);
            if (nearest < near && !asleep) return;
            Put(l, next, nextSub);
            ZoneSystem.instance.RemoveGlobalKey(l.KeyNext);
            l.Log("became " + l.WordOf(next, nextSub) + " behind the scenes (" + (asleep ? "everyone asleep" : "nearest " + (nearest == float.MaxValue ? "nobody" : Mathf.RoundToInt(nearest) + " m")) + ")");
        }

        // ---------------------------------------------------------------- the abandoned Hall's steps (the user 2026-11-01)
        // 1.1 «гейд»: its hearth first lit with wood (HallHearth.Hel, Hel's cold blue fire) — Heid, who led us here, clears
        // away all that points to her. 1.2 «розпалена»: lit with the Feather of the Watch (3.0; HallHearth.Watch) and a
        // heart of a home built anywhere in the world — the chest for the wood appears. Each behind the scenes (nobody
        // within NearIn m); in order, never back
        internal const string FireKey = "ravenwatch_hall_fire";
        private static float _nextKindle;

        internal static bool FireLit { get { return HallHearth.Level == HallHearth.Watch; } }

        private static void Kindle()
        {
            if (Time.time < _nextKindle || State != Abandoned || Sub >= LitSub || HallHearth.Level < HallHearth.Hel) return;
            if (Sub < HeidSub)
            {
                Request(Locs.Hall, Abandoned, HeidSub, false);
                Locs.Hall.Log("its hearth lit for the first time: Heid's step as soon as nobody is near");
                return;
            }
            if (!FireLit) return;
            _nextKindle = Time.time + 30f; // (all the world's hearts looked for: not often)
            if (!NpcHeart.AnyInWorld()) return;
            Request(Locs.Hall, Abandoned, LitSub, false);
            Locs.Hall.Log("its fire lit with the Watch's and a heart built: kindled as soon as nobody is near");
        }

        // ---------------------------------------------------------------- the places that follow the Hall
        // One truth for the Hall, the places that follow it and the story's people (the user 2026-11-01: «синхронізувати стан
        // зали і табір гейд з самою гейд»; 2026-11-02: as data, no place id in the code): the Hall's own state. A place
        // with «follows: hall» (locations.list — Heid's camp by the stones of the fallen) is 1 till the Hall is kindled
        // (1.2 «розпалена», the Watch's fire and a heart), 2 from then on — either way: a Hall set back (the author's tests)
        // brings it back. StoryPeople moves the people by the same word (Kindled). Behind the scenes: nobody within NearOut
        // m, or all asleep
        // the Hall kindled: «розпалена» (1.2) or on — the one word the places that follow it and the story's people read
        internal static bool Kindled { get { return State > Abandoned || Sub >= LitSub; } }

        // a place that follows the Hall (locations.list «follows: hall»): 2 once it is kindled, else 1
        private static void SyncFollower(Loc l)
        {
            int want = Kindled ? 2 : Abandoned;
            if (l.Last < 2 || StateOf(l) == want) return;
            Request(l, want, 0, true);
            l.Log(want == 2 ? "the Hall is kindled: Heid's camp goes as soon as nobody sees it" : "the Hall is not kindled: Heid's camp comes back as soon as nobody sees it");
        }

        // ---------------------------------------------------------------- the doors
        // All of them while a change waits; the great door in front (named MainDoor by the author: <place>.names.txt) also
        // till the story opens it (the world key <place>_gate; 2.9: never — in through the holes, if one wants)
        internal const string MainDoor = "dveri_holovni";
        internal static bool GateOpenOf(Loc l) { return l != null && ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(l.KeyGate); }

        private static readonly List<ZNetView> _doors = new List<ZNetView>();
        private static void CloseDoors()
        {
            for (int i = _doors.Count - 1; i >= 0; i--)
            {
                ZNetView nv = _doors[i];
                if (nv == null || !nv.IsValid()) { _doors.RemoveAt(i); continue; }
                Loc l = Locs.Get(nv.GetZDO().GetString(HallParts.KOf, ""));
                if (l == null || (!ShutOf(l) && (GateOpenOf(l) || !IsMain(l, nv)))) continue;
                if (nv.IsOwner() && nv.GetZDO().GetInt(ZDOVars.s_state) != 0) nv.GetZDO().Set(ZDOVars.s_state, 0);
            }
        }

        // the place a door is of (a blueprint put it there), null — none
        private static Loc DoorOf(Door d)
        {
            ZNetView nv = d != null ? d.GetComponent<ZNetView>() : null;
            return nv != null && nv.IsValid() ? Locs.Get(nv.GetZDO().GetString(HallParts.KOf, "")) : null;
        }

        private static bool IsMain(Loc l, ZNetView nv) { return HallParts.NameOf(l.Id, nv) == MainDoor; }

        // why this door does not open now (null — it opens)
        internal static string Locked(Door d)
        {
            Loc l = DoorOf(d);
            if (l == null) return null;
            if (ShutOf(l)) return l.IsHall ? "Двері Зали зачинені зсередини." : "Двері зачинені зсередини.";
            if (!GateOpenOf(l) && IsMain(l, d.GetComponent<ZNetView>())) return l.IsHall ? "Великі двері Зали замкнені." : "Великі двері замкнені.";
            return null;
        }

        // the author's look and switch: `raven blueprint state [place] [1|1.1|2|…] [in|out] | cancel | fire | gate` (the
        // world keys; everyone's place follows) — with in/out the change waits behind the scenes as the story's would. No
        // place named: the Hall
        internal static string Cmd(string[] a)
        {
            Loc named = a.Length > 2 ? Locs.Get(a[2]) : null;
            if (named == null) return Cmd(Locs.Hall, a);
            List<string> rest = new List<string>(a);
            rest.RemoveAt(2);
            return Cmd(named, rest.ToArray());
        }

        internal static string Cmd(Loc l, string[] a)
        {
            if (!l.Built) return l.Title + ": у цьому світі ще нема.";
            string arg = a.Length > 2 ? a[2].ToLowerInvariant() : "", how = a.Length > 3 ? a[3].ToLowerInvariant() : "";
            int next, nextSub; bool outside;
            if (arg == "cancel")
            {
                if (!Pending(l, out next, out nextSub, out outside)) return "Зміни, що чекає, нема.";
                ZoneSystem.instance.RemoveGlobalKey(l.KeyNext);
                return l.Title + ": зміну на «" + l.WordOf(next, nextSub) + "» скасовано, двері знову відчиняються.";
            }
            if (arg == "fire" && l.IsHall)
            {
                int f = (HallHearth.Level + 1) % 3; // (cold → Hel's → the Watch's → cold)
                HallHearth.SetLevel(f);
                return "Вогнище Варти: " + HallHearth.WordOf(f) + (f == HallHearth.Watch ? ". Зала стане «" + l.WordOf(Abandoned, LitSub) + "» за кадром, щойно у світі буде серце оселі й нікого ближче " + NearIn + " м; тоді ж Гейд переїде в Залу, а її табір зникне."
                     : f == HallHearth.Hel ? ". Зала стане «" + l.WordOf(Abandoned, HeidSub) + "» за кадром, щойно нікого ближче " + NearIn + " м (якщо ще не була)."
                     : " (стану Зали це не міняє).");
            }
            if (arg == "gate")
            {
                if (GateOpenOf(l)) { ZoneSystem.instance.RemoveGlobalKey(l.KeyGate); return l.Title + ": великі двері знову замкнені."; }
                ZoneSystem.instance.SetGlobalKey(l.KeyGate);
                return l.Title + ": великі двері відімкнено.";
            }
            int s, k;
            if (!SubNumber(arg, out s, out k))
                return l.Title + " зараз: " + l.WordOf(StateOf(l), SubOf(l)) + (Pending(l, out next, out nextSub, out outside) ? "; чекає за кадром: " + l.WordOf(next, nextSub) + " (" + (outside ? "ззовні: нікого ближче " + NearOut + " м або всі сплять" : "всередині: нікого ближче " + NearIn + " м") + "), двері зачинені" : "")
                     + (l.IsHall ? "; вогнище — " + HallHearth.WordOf(HallHearth.Level) : "") + ", великі двері " + (GateOpenOf(l) ? "відімкнені" : "замкнені")
                     + ".\nraven blueprint state [місце] 1…" + l.Last + " чи 1.1 (субстан) — одразу; … in|out — за кадром, як сюжет; state cancel; state gate" + (l.IsHall ? "; state fire" : "") + ".";
            s = Mathf.Clamp(s, Abandoned, l.Last);
            if (k > l.SubsOf(s)) return "У стані «" + l.WordOf(s) + "» нема субстану " + s + "." + k + ".";
            if (how == "in" || how == "out")
            {
                Request(l, s, k, how == "out");
                return l.Title + " стане «" + l.WordOf(s, k) + "», щойно ніхто не бачитиме (" + (how == "out" ? "нікого ближче " + NearOut + " м або всі сплять" : "нікого ближче " + NearIn + " м") + "). Двері поки зачинені.";
            }
            Put(l, s, k);
            return l.Title + " тепер: " + l.WordOf(s, k) + " (за секунду-дві).";
        }
    }

    // The author's look at the states on the Hall he builds by hand (its frame in this world: BlueprintFrame): only
    // drawn, only in his game — no strength written, nothing kept; off: everything as it was. A Hall a blueprint put
    // into the world is not touched (it shows the world's own state).
    internal static class HallPreview
    {
        internal static int State { get; private set; } // 0 — off
        internal static int Sub { get; private set; }   // its sub-state looked at (0 — the state itself)
        internal static Loc Of { get; private set; }    // the place looked at (the author's own, by its frame)
        private static readonly Dictionary<int, List<Component>> _hidden = new Dictionary<int, List<Component>>();
        private static readonly Dictionary<int, List<Component>> _dimmed = new Dictionary<int, List<Component>>();
        internal static readonly HashSet<int> Unlit = new HashSet<int>();
        private static readonly HashSet<ZNetView> _tinted = new HashSet<ZNetView>();
        private static readonly HashSet<WearNTear> _drawn = new HashSet<WearNTear>();
        private static readonly Dictionary<int, string> _layer = new Dictionary<int, string>(); // a piece's layer, till the author's files change
        private static int _layerVersion = -1;
        private static readonly string NotHere = new string('-', 2); // (its own object: compared by reference)
        private static float _next;

        internal static string Set(int s) { return Set(Of ?? Locs.Hall, s, 0); }
        internal static string Again() { return Set(Of ?? Locs.Hall, State, Sub); } // (the look again at once)

        internal static string Set(Loc l, int s, int sub = 0)
        {
            Vector3 c; Quaternion rot;
            if (l != Of && Of != null) { State = 0; Sub = 0; Off(); } // (another place: the one looked at before as it is)
            Of = l;
            if (s != 0 && !BlueprintFrame.Get(l.Id, out c, out rot)) return "Спершу рамка «" + l.Title + "» у цьому світі (збережи або «знайти рамку»).";
            int was = State * 100 + Sub;
            State = Mathf.Clamp(s, 0, l.Last);
            Sub = State == 0 ? 0 : Mathf.Clamp(sub, 0, l.SubsOf(State));
            if (was != State * 100 + Sub && BlueprintFrame.Get(l.Id, out c, out rot)) HallState.ClearSmoke(c, HallParts.AuthorReach); // (the look changed: no smoke of what it hid)
            if (State == 0) Off(); else _next = 0f;
            return State == 0 ? "Перегляд вимкнено: «" + l.Title + "» така, як є." : "Перегляд: «" + l.Title + "» як «" + l.WordOf(State, Sub) + "» (лише в тебе, у світі нічого не змінено).";
        }

        internal static void Tick()
        {
            if (State == 0 || Time.time < _next) return;
            _next = Time.time + 1f;
            Vector3 c; Quaternion rot;
            if (Of == null || !BlueprintFrame.Get(Of.Id, out c, out rot)) { Set(0); return; }
            bool ruined = HallState.Ruined(Of, State);
            if (_layerVersion != HallParts.Version || _layer.Count > 50000) { _layerVersion = HallParts.Version; _layer.Clear(); }
            foreach (Piece pc in Blueprint.Around(c, HallParts.AuthorReach))
            {
                ZNetView nv = pc.GetComponent<ZNetView>();
                if (nv.GetZDO().GetString(HallParts.KOf, "").Length > 0) continue; // (a placed copy: the world's state)
                int id = nv.GetInstanceID();
                string layer;
                if (!_layer.TryGetValue(id, out layer))
                {
                    string of; Vector3 at;
                    _layer[id] = layer = HallParts.Place(nv, out of, out at) && of == Of.Id ? HallParts.LayerAt(of, Utils.GetPrefabName(pc.gameObject), at) : NotHere;
                }
                if (ReferenceEquals(layer, NotHere)) continue; // (another place's, its frame nearer: not this look)
                bool on = HallState.Shows(layer, State, Sub);
                HallState.Show(_hidden, nv, on);
                HallState.Lights(_dimmed, Unlit, nv, on, ruined, Of, State, Sub);
                if (on && HallState.Tint(Of, nv)) _tinted.Add(nv);
                WearNTear w = on ? pc.GetComponent<WearNTear>() : null;
                if (w == null) continue;
                _drawn.Add(w);
                HallState.Draw(w, ruined ? HallState.Ruin : 1f);
            }
        }

        internal static void StandMade(ZNetView nv) { if (_hidden.ContainsKey(nv.GetInstanceID())) HallState.Show(_hidden, nv, false); }

        private static void Off()
        {
            HallState.ShowAll(_hidden);
            HallState.UndimAll(_dimmed);
            Unlit.Clear();
            foreach (ZNetView nv in _tinted) HallState.Untint(nv);
            _tinted.Clear();
            foreach (WearNTear w in _drawn)
            {
                if (w == null) continue;
                ZNetView nv = w.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid()) HallState.Draw(w, nv.GetZDO().GetFloat(ZDOVars.s_health, w.m_health) / w.m_health);
            }
            _drawn.Clear();
        }
    }

    // a chest of a place that keeps them shut (Loc.LockChests): nobody opens it, the author neither (his own is open)
    [HarmonyPatch(typeof(Container), "Interact")]
    internal static class LocChestPatch
    {
        // (quietly, and no «[E] open» on it either: just a chest standing there — the user 2026-11-01)
        internal static bool Shut(Container c)
        {
            ZNetView nv = c != null ? c.GetComponentInParent<ZNetView>() : null;
            if (!Blueprint.Copy(nv)) return false;
            Loc l = Locs.Get(nv.GetZDO().GetString(HallParts.KOf, ""));
            return l != null && l.LockChests;
        }

        private static bool Prefix(Container __instance, ref bool __result)
        {
            if (!Shut(__instance)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Container), "GetHoverText")]
    internal static class LocChestHoverPatch
    {
        private static void Postfix(Container __instance, ref string __result) { if (LocChestPatch.Shut(__instance)) __result = ""; }
    }

    [HarmonyPatch(typeof(Container), "GetHoverName")]
    internal static class LocChestNamePatch
    {
        private static void Postfix(Container __instance, ref string __result) { if (LocChestPatch.Shut(__instance)) __result = ""; }
    }

    // a bed of a place that keeps them empty (Loc.LockBeds): no sleep, no spawn point — for anyone
    [HarmonyPatch(typeof(Bed), "Interact")]
    internal static class LocBedPatch
    {
        private static bool Prefix(Bed __instance, Humanoid human, ref bool __result)
        {
            ZNetView nv = __instance.GetComponentInParent<ZNetView>();
            if (!Blueprint.Copy(nv)) return true;
            Loc l = Locs.Get(nv.GetZDO().GetString(HallParts.KOf, ""));
            if (l == null || !l.LockBeds) return true;
            if (human == Player.m_localPlayer) human.Message(MessageHud.MessageType.Center, Lang.T("Це ліжко не для тебе: тут спав хтось інший."));
            __result = false;
            return false;
        }
    }

    // the thing an item stand holds, made by the stand after the stand was hidden: hidden at once (no waiting, no looking)
    [HarmonyPatch(typeof(ItemStand), "SetVisualItem")]
    internal static class HallStandItemPatch
    {
        private static void Postfix(ItemStand __instance) { HallState.StandMade(__instance.GetComponent<ZNetView>()); }
    }

    // a fire of the Hall kept out (HallState.Lights): not burning, whatever its fuel
    [HarmonyPatch(typeof(Fireplace), "IsBurning")]
    internal static class HallFirePatch
    {
        private static void Postfix(Fireplace __instance, ref bool __result)
        {
            int id = __instance.GetInstanceID();
            if (HallState.Lit.Contains(id)) { __result = true; return; } // (burning for you as the story says: fuel or not)
            if (!__result) return;
            if (HallState.Unlit.Contains(id) || HallPreview.Unlit.Contains(id)) __result = false;
        }
    }

    // while a change waits behind the scenes, the Hall's doors do not open; nor its great door till the story opens it
    [HarmonyPatch(typeof(Door), "Interact")]
    internal static class HallDoorPatch
    {
        private static bool Prefix(Door __instance, Humanoid character, ref bool __result)
        {
            string why = HallState.Locked(__instance);
            if (why == null) return true;
            if (character != null) character.Message(MessageHud.MessageType.Center, Lang.T(why));
            __result = false;
            return false;
        }
    }

    // the Hall is not mended by a hammer, piece by piece: the story mends it whole
    [HarmonyPatch(typeof(WearNTear), "Repair")]
    internal static class HallRepairPatch
    {
        private static float _told;
        private static bool Prefix(WearNTear __instance, ref bool __result)
        {
            if (!BlueprintWearPatch.IsMarked(__instance) || Perches.StoryAuthor) return true; // (the author mends his own Hall as he builds it)
            if (Player.m_localPlayer != null && Time.time - _told > 2f) { _told = Time.time; Player.m_localPlayer.Message(MessageHud.MessageType.Center, Lang.T("Цього так не полагодиш: воно належить цьому місцю.")); }
            __result = false;
            return false;
        }
    }
}
#endif
