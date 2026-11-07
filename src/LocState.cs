#if RAVEN_ITEM && RAVEN_NPC
// (the Hall of the Watch's states: abandoned from 2.9, the others with the story in 3.0 — the user 2026-11-01)
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A place's states (the Hall as the example). The blueprint is the whole place at its best; a state is laid on the same pieces, never built
    // anew, and is one for the world (world keys «ravenwatch_hall_state <n>» and «…_sub <k>»: the story runs on the
    // server, as the game's own). 1 — abandoned (2.9: the only one players see), 2 — repaired, 3 — lived in (the story,
    // 3.0; the user 2026-11-01). A state has sub-states, its own steps in order (1.0 — its start; the Hall's 1.1 «гейд»:
    // the first heart built, Heid clears away what points to her; 1.2 «розпалена»: the Watch's fire, the chest for the
    // wood): the author adds them in his window (Loc.Subs; the user 2026-11-01: states and sub-states clearly apart).
    //   · strength: abandoned — every piece at Ruin of it, so the game shows its own broken look (≤ 25%: cracked beams,
    //     holes; in its sub-states too); from repaired — whole. The owner of each piece writes it into its ZDO (kept, seen by all); every game
    //     redraws the look quietly (no crack or splinters for two thousand pieces at once). It never falls: Blueprint.
    //   · layers (LocParts: <building>.layers.txt in the mod, never in the world): a piece's states — «ruin» only while
    //     abandoned (rubble, fallen beams, bushes inside), «repaired» from 2, «fixed» only while repaired, «lived» from 3
    //     (food on the stands, banners) — and its sub-states' marks after commas: «+1.2» — there only in sub-state 1.2
    //     (the chest; its state none), «-1.1» — gone in 1.1 (as «ruin,-1.1»), «>2.2» — in its state from 2.2 on (not at
    //     its start nor in 2.1). Not theirs:
    //     hidden — their looks, colliders (nobody stands on them, no raven sits there), lights, sounds, stations and
    //     comfort off — so a later state needs no building. The game's own raven never explains a piece of a place, in any
    //     state (Guides).
    // Its steps and followers are its own lines (locations.list «becomes:», «follows:»: the server, LocState below). Here: states, changes behind the scenes,
    // the author's console; LocLayers: a piece's layer; LocLook: laid on the pieces; LocDoors: its doors; LocPreview: the author's look; LocPatches: the game's hooks.
    // The story never switches it in sight: it asks, and the change waits behind the scenes (below).
    // Every place of the story (Loc) has states the same way — its own count, names and sub-states, its own world keys;
    // the named layers (ruin, repaired, lived, fixed) mean states 1, 2, 3; any place may use them or «onlyN», «fromN». «Ruined» is the
    // place's own (Loc.RuinTo: the Hall 1).
    internal static partial class LocState
    {
        internal const int Abandoned = 1, Repaired = 2, Lived = 3;
        internal const float Ruin = 0.1f; // of a piece's strength while abandoned (the game's broken look: ≤ 0.25)
        internal static readonly string[] Layers = { "ruin", "repaired", "lived", "fixed" };
        private const float Every = 1f, Reach = 340f; // (its pieces are loaded only round a player; its landmarks from the game's far ring: Loc)

        private static readonly FieldInfo HealthShown = AccessTools.Field(typeof(WearNTear), "m_healthPercentage");
        private static readonly MethodInfo Redraw = AccessTools.Method(typeof(WearNTear), "UpdateVisual");
        private sealed class Laid { internal readonly HashSet<int> Shown = new HashSet<int>(), Healed = new HashSet<int>(); } // a place's pieces laid for its state (by their layer) · whose strength is its state's
        private static readonly Dictionary<string, Laid> _laid = new Dictionary<string, Laid>();
        private static Laid LaidOf(Loc l) { Laid d; if (!_laid.TryGetValue(l.Id, out d)) _laid[l.Id] = d = new Laid(); return d; }
        private static readonly Dictionary<int, List<Component>> _hidden = new Dictionary<int, List<Component>>(); // pieces hidden now: what was put off
        private static readonly Dictionary<int, List<Component>> _dimmed = new Dictionary<int, List<Component>>(); // lamps put out while a ruin
        internal static readonly HashSet<int> Unlit = new HashSet<int>(); // the game's fires (Fireplace) kept out: hidden, or a ruin
        internal static readonly HashSet<int> Lit = new HashSet<int>();   // …and those kept burning for you, fuel or not (Loc.LitWhile)
        private static readonly Dictionary<string, int> _states = new Dictionary<string, int>(); // each place's state (×100 + sub-state) its pieces were laid for
        private static float _next;

        internal static int StateOf(Loc l)
        {
            if (l != null && l.StateWhile.Count > 0) return Mine(l);
            string v; int s;
            if (l == null || ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(l.KeyState, out v) || !int.TryParse(v, out s)) return Abandoned;
            return Mathf.Clamp(s, Abandoned, l.Last);
        }

        // a place whose state is each player's own (Loc.StateWhile: the ghost's house lived in till you have the book): the
        // first state whose conditions hold for you, else its first; asked often, so worked out at most twice a second
        private static readonly Dictionary<string, KeyValuePair<float, int>> _mineAt = new Dictionary<string, KeyValuePair<float, int>>();
        private static int Mine(Loc l)
        {
            KeyValuePair<float, int> was;
            if (_mineAt.TryGetValue(l.Id, out was) && Time.time < was.Key) return was.Value;
            int s = Abandoned;
            Player me = Player.m_localPlayer;
            foreach (KeyValuePair<string, string> w in l.StateWhile)
            {
                int n;
                if (!int.TryParse(w.Key, out n)) continue;
                if (me != null && StoryCond.Holds(me, w.Value, e => l.Log("statewhile: " + e))) { s = n; break; }
            }
            s = Mathf.Clamp(s, Abandoned, l.Last);
            _mineAt[l.Id] = new KeyValuePair<float, int>(Time.time + 0.5f, s);
            return s;
        }

        // its state's sub-state (0 — the state itself)
        internal static int SubOf(Loc l)
        {
            string v; int k;
            if (l == null || ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(l.KeySub, out v) || !int.TryParse(v, out k)) return 0;
            return Mathf.Clamp(k, 0, l.SubsOf(StateOf(l)));
        }

        // ---------------------------------------------------------------- a change behind the scenes
        // The story asks for a state or a sub-state (Request) and it waits, its doors as they are (a door opened by the story stays open;
        // shutting them locked in the player who lit the first fire, the user 2026-11-04), until nobody
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
            foreach (Loc l in Locs.All) ServerOne(l);
        }

        private static void ServerOne(Loc l)
        {
            if (!l.Built) return;
            int next, nextSub; bool outside;
            if (!Pending(l, out next, out nextSub, out outside)) { Becomes(l); Follow(l); return; }
            float near = outside ? NearOut : NearIn, nearest = float.MaxValue;
            foreach (ZDO z in ZNet.instance.GetAllCharacterZDOS()) if (z != null) nearest = Mathf.Min(nearest, l.From(z.GetPosition()));
            bool asleep = outside && Game.instance != null && Sleeping != null && (bool)Sleeping.GetValue(Game.instance);
            if (nearest < near && !asleep) return;
            Put(l, next, nextSub);
            ZoneSystem.instance.RemoveGlobalKey(l.KeyNext);
            l.Log("became " + l.WordOf(next, nextSub) + " behind the scenes (" + (asleep ? "everyone asleep" : "nearest " + (nearest == float.MaxValue ? "nobody" : Mathf.RoundToInt(nearest) + " m")) + ")");
        }

        // ---------------------------------------------------------------- a place's own steps, by its rules (locations.list; the server)
        // «becomes: 1.1 hearth zala >= 1» — once these world conditions hold it moves on to that step: in the file's order, forward only
        // (the Hall: 1.1 «гейд» — its hearth first lit, Hel's blue fire, Heid clears away what points to her; 1.2 «розпалена» — the Watch's
        // fire and the world's first heart: the chest for the wood appears). «follows: 2 state zala >= 1.2» — that state while they hold,
        // else its first, either way (Heid's camp goes once the Hall is kindled; a Hall set back by the author's tests brings it back).
        // Behind the scenes as any change (Request): nobody near. Every 2 s on the server, a few world keys read a rule
        private static void Becomes(Loc l)
        {
            if (l.Becomes.Count == 0) return;
            int now = StateOf(l) * 100 + SubOf(l);
            foreach (Loc.Rule st in l.Becomes)
            {
                int at = Mathf.Clamp(st.State, Abandoned, l.Last);
                if (at * 100 + Mathf.Clamp(st.Sub, 0, l.SubsOf(at)) <= now) continue; // (clamped as its state reads: never asked for ever)
                if (!StoryCond.Holds(null, st.When, e => l.Log("becomes: " + e), true)) return; // (in order: the next step only after this one)
                Request(l, st.State, st.Sub, false);
                l.Log("becomes " + l.WordOf(st.State, st.Sub) + " (" + st.When + ") as soon as nobody is near");
                return;
            }
        }

        private static void Follow(Loc l)
        {
            if (l.Follow == null) return;
            int want = StoryCond.Holds(null, l.Follow.When, e => l.Log("follows: " + e), true) ? Mathf.Clamp(l.Follow.State, Abandoned, l.Last) : Abandoned; // (clamped as its state reads: never asked for ever)
            if (StateOf(l) == want) return;
            Request(l, want, 0, true);
            l.Log("follows: " + l.WordOf(want) + " (" + l.Follow.When + (want == Abandoned ? " — no longer" : "") + ") as soon as nobody sees it");
        }

        // the author's look and switch: `raven blueprint state [place] [1|1.1|2|…] [in|out] | cancel | fire | gate` (the
        // world keys; everyone's place follows) — with in/out the change waits behind the scenes as the story's would. No
        // place named: the first of the list (the Hall)
        internal static string Cmd(string[] a)
        {
            Loc named = a.Length > 2 ? Locs.Get(a[2]) : null;
            if (named == null) return Locs.All.Count > 0 ? Cmd(Locs.All[0], a) : "Місць сюжету нема.";
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
                return l.Title + ": зміну на «" + l.WordOf(next, nextSub) + "» скасовано.";
            }
            if (arg == "fire")
            {
                int f = (LocHearth.LevelOf(l) + 1) % 3; // (cold → Hel's → the Watch's → cold)
                LocHearth.SetLevel(l, f);
                return l.Title + ", вогнище: " + LocHearth.WordOf(f) + (l.Becomes.Count > 0 ? ". Його кроки (becomes:) ідуть за кадром, щойно справдяться їхні умови й нікого ближче " + NearIn + " м." : ".");
            }
            if (arg == "gate")
            {
                bool open = !GateOpenOf(l);
                SetGate(l, open);
                return l.Title + (open ? ": великі двері відімкнено." : ": великі двері знову замкнені.");
            }
            int s, k;
            if (!SubNumber(arg, out s, out k))
                return l.Title + " зараз: " + l.WordOf(StateOf(l), SubOf(l)) + (Pending(l, out next, out nextSub, out outside) ? "; чекає за кадром: " + l.WordOf(next, nextSub) + " (" + (outside ? "ззовні: нікого ближче " + NearOut + " м або всі сплять" : "всередині: нікого ближче " + NearIn + " м") + ")" : "")
                     + "; вогнище — " + LocHearth.WordOf(LocHearth.LevelOf(l)) + (l.Gate.Length > 0 ? ", великі двері " + (GateOpenOf(l) ? "відімкнені" : "замкнені") : "")
                     + ".\nraven blueprint state [місце] 1…" + l.Last + " чи 1.1 (субстан) — одразу; … in|out — за кадром, як сюжет; state cancel; state gate; state fire.";
            s = Mathf.Clamp(s, Abandoned, l.Last);
            if (k > l.SubsOf(s)) return "У стані «" + l.WordOf(s) + "» нема субстану " + s + "." + k + ".";
            if (how == "in" || how == "out")
            {
                Request(l, s, k, how == "out");
                return l.Title + " стане «" + l.WordOf(s, k) + "», щойно ніхто не бачитиме (" + (how == "out" ? "нікого ближче " + NearOut + " м або всі сплять" : "нікого ближче " + NearIn + " м") + ").";
            }
            Put(l, s, k);
            return l.Title + " тепер: " + l.WordOf(s, k) + " (за секунду-дві).";
        }
    }
}
#endif
