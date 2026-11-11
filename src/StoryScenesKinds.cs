#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (StoryScenes), the kinds of its steps: a step's first word picks its entry; player, bird, npc have verbs of their own (StoryScenesPlayer, Bird, Cast).
    internal static partial class StoryScenes
    {
        // ---------------------------------------------------------------- the kinds of steps
        // A registry, as StoryCond's conditions and StoryActions' actions (the user 2026-11-02: predictable, nothing patched
        // on): a step's first word picks its kind, a new kind is one entry — the director itself never changes for it.
        //   Min / Usage   words it needs after its own; how it is written (the errors)
        //   Check(st)     load time: null — fine, else what is wrong (after Min)
        //   Begin(p, st)  it starts; it may set its clock: Hold(s) — busy till then; _limit — its own time limit
        //   Busy(p, st)   still going? (none: its clock — Hold — says)
        //   Late(st)      it ran past its limit: what it began is put right (a walker stands where it is)
        //   Done(p, st)   as it ends: what came of it, in words with numbers (the walk's distance and time, where the bird sits, the clip on the body);
        //                 the director adds how long it took and whether it was cut off (EndStep → Outcomes, the log) — the scene's check puts it by the step
        //   Setup(p, st)  played from a later step: what it leaves behind, at once
        //   Lasting       it changes the world for good: once it ran the scene is never played again
        //   PlaceAt / PlaceAfter / PlaceNot / PlaceOf   the word that names a place (load check here and in storycheck, from this one source)
        //   Verbs, VerbAt a step with verbs («player go», «npc heid go», «bird land»): the word at VerbAt picks its own entry (StepVerb), with all of the above
        // The clock (_waitFor, _until, _limit) is the step's own: cleared as each step starts.
        // who sees a step (StoryWatch), a word every step must give: its constructor has no other way
        internal enum StepSeen { World, Shared, Own }

        internal class StepKind
        {
            internal readonly StepSeen Seen; // World: the network shows it · Shared: it reaches every game near · Own: the director's own (his camera, his body), left out when he has given them back
            internal StepKind(StepSeen seen) { Seen = seen; }
            public int Min;
            public string Usage = "";
            public Func<Step, string> Check;
            public Action<Player, Step> Begin;
            public Func<Player, Step, bool> Busy;
            public Action<Step> Late;
            public Func<Player, Step, string> Done; // (what it came to, read as it ends — «ran 14 m in 4.1 s, stopped 0.3 m off»; null: nothing to say. Said by the director with how long it took: StoryScenes.EndStep)
            public bool Lasting; // (it changes the world or the bag for good: once it ran, the scene is never played again — a stop finishes it quietly)
            public Action<Player, Step> Setup; // (played from a later step: what this one leaves behind, put at once — where he stands, the bars, the shot, the thing in his hand, the bird on its seat; null: nothing)
            public int PlaceAt;        // the word naming a place (0: none)
            public string PlaceAfter;  // …or the word after this one («at», «from», «look»)
            public string[] PlaceNot;  // …unless it is one of these («near», «forward»)
            public Func<string[], string> PlaceOf; // …or found by the kind itself (a module's)
            public bool NotCast;       // (npc: this verb takes no person from the world: not waited for)
            public Dictionary<string, StepVerb> Verbs; // (a step with verbs: the word at VerbAt picks one)
            public int VerbAt;

            internal string PlaceWord(string[] w)
            {
                if (PlaceOf != null) return PlaceOf(w);
                int i = PlaceAt;
                if (PlaceAfter != null) { i = Array.IndexOf(w, PlaceAfter); i = i > 0 ? i + 1 : 0; }
                if (i <= 0 || i >= w.Length) return null;
                return PlaceNot != null && Array.IndexOf(PlaceNot, w[i]) >= 0 ? null : w[i];
            }
        }

        internal sealed class StepVerb : StepKind { internal StepVerb(StepSeen seen) : base(seen) { } }

        // what a step's words pick: its kind, its verb in it (null: neither known)
        private static StepKind KindOf(string[] w)
        {
            StepKind k;
            if (w.Length == 0 || !Kinds.TryGetValue(w[0], out k)) return null;
            if (k.Verbs == null) return k;
            StepVerb v;
            return w.Length > k.VerbAt && k.Verbs.TryGetValue(w[k.VerbAt], out v) ? v : null;
        }

        private static Dictionary<string, StepKind> _kinds;
        private static Dictionary<string, StepKind> Kinds { get { if (_kinds == null) { _kinds = Builtin(); StoryModules.Steps(); } return _kinds; } } // (and the modules' own)

        // a module's own step (the same way as the ones below)
        internal static void Register(string word, StepKind k) { Kinds[word] = k; }

        private static void Hold(float seconds) { _waitFor = Time.time + seconds; }

        private static Dictionary<string, StepKind> Builtin()
        {
            StepKind air = new StepKind(StepSeen.Own) // (each game's own air, or everyone's with «all»: StoryAir)
            {
                Usage = "weather <env> [s] [all] · weather off · thor [all] · shake <сила> [all]",
                Check = st => st.W[0] != "thor" && st.W.Length < 2 ? st.W[0] + ": мало слів" : null,
                Begin = (p, st) =>
                {
                    bool all = st.W[st.W.Length - 1] == "all";
                    if (st.W[0] == "weather") { _weather = st.W[1] != "off"; _weatherAll = _weather && (all || _weatherAll); } // (the scene's weather ends with it: Release)
                    StoryAir.Play(all ? st.Text.Substring(0, st.Text.LastIndexOf("all")).Trim() : st.Text, all);
                },
                Setup = (p, st) => { if (st.W[0] == "weather") { _weather = st.W[1] != "off"; StoryAir.Play(st.W[st.W.Length - 1] == "all" ? st.Text.Substring(0, st.Text.LastIndexOf("all")).Trim() : st.Text, false); } } // (played from a later step: the weather it would have)
            };
            return new Dictionary<string, StepKind>
            {
                { "wait", new StepKind(StepSeen.World) { Min = 1, Usage = "wait <секунд>", Begin = (p, st) => Hold(F(st.W[1], 1f)) } },
                { "until", new StepKind(StepSeen.World) { Usage = "until <умови> | <секунд>",
                    Check = st =>
                    {
                        int bar = st.Text.LastIndexOf('|');
                        if (bar < 0) return "until <умови> | <секунд>";
                        List<string> errs = new List<string>();
                        StoryCond.Parse(st.Text.Substring(5, bar - 5), errs);
                        return errs.Count > 0 ? string.Join("; ", errs.ToArray()) : null;
                    },
                    Begin = (p, st) => { _until = Time.time + st.UntilSec; _limit = _until + 1f; },
                    Busy = (p, st) => Time.time < _until && !StoryCond.All(p, st.Until),
                    Done = (p, st) => StoryCond.All(p, st.Until) ? "its condition holds" : "its condition NEVER held in its " + F(st.UntilSec) + " s" } },
                { "msg", new StepKind(StepSeen.Shared) { Min = 1, Usage = "msg <текст>",
                    Begin = (p, st) => { string line = Grammar.Fix(Lang.T(Rest(st.Text, 1)), p); if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, line); StoryWatch.Line(line); } } },
                { "watchers", new StepKind(StepSeen.Shared) { Min = 1, Usage = "watchers take [camera] · watchers free   (забрати клавіші в тих, хто поруч, і дати їм камеру сцени; кінець сцени все повертає)",
                    Check = st => st.W[1] != "free" && (st.W[1] != "take" || (st.W.Length > 2 && st.W[2] != "camera")) ? "watchers take [camera] · watchers free" : null,
                    Begin = WatchersStep, Setup = WatchersStep } },
                { "say", new StepKind(StepSeen.Own) { Min = 2, Usage = "say hugin|munin <текст>",
                    Check = st => st.W[1] != "hugin" && st.W[1] != "munin" ? "say hugin|munin <текст>" : null,
                    Begin = (p, st) =>
                    {
                        string line = Lang.T(Rest(st.Text, 2));
                        bool munin = st.W[1] == "munin";
                        if (!Shoulder.Present(p, munin)) return; // (not on the shoulder: nothing said, nothing waited)
                        Hold(RavenSpeech.Seconds(line, RavenSpeech.Pace.Scene));
                        RavenSkills.StoryLine(munin, line);
                        Spoken(Plugin.RavenName(munin), line); // (in a shot the game's screen is hidden: its words as a subtitle)
                    } } },
                { "sub", new StepKind(StepSeen.Shared) { Min = 1, Usage = "sub <текст>",
                    Begin = (p, st) => { string line = Grammar.Fix(Lang.T(Rest(st.Text, 1)), p); float t = RavenSpeech.Seconds(line, RavenSpeech.Pace.Scene); Hold(t); if (!_ownOff) StoryScreen.Sub(line, t + 0.4f); StoryWatch.Line(line); } } },
                { "bars", new StepKind(StepSeen.Own) { Min = 1, Usage = "bars on|off",
                    Check = st => st.W[1] != "on" && st.W[1] != "off" ? "bars on|off" : null,
                    Begin = (p, st) => StoryScreen.Bars(st.W[1] == "on"), Setup = (p, st) => StoryScreen.Bars(st.W[1] == "on") } },
                { "camera", new StepKind(StepSeen.Own) { Min = 1, PlaceAfter = "look", Usage = "camera <місце|behind|over|face [кут] [м]|follow [вбік] [вгору] [назад]|off> [look …] [blend N|cut] [fov N] [live N] · camera shot <кадр> · camera move <кадр> <кадр> <с> [ease …] [wait] · camera orbit <що> <радіус> <висота> <градусів> <с> [wait] · camera on <хто> [close|medium|full|wide] [front|left|right|back|<градусів>] [low|high] [track] [push <м> <с>] [look …] [wait] · camera shoulder|two <хто> <на кого> [left|right] [push <м> <с>] [wait]",
                    Check = CameraCheck, // (on, shoulder, two: who they are of, no unknown word)
                    Busy = (p, st) => Time.time < _waitFor, // (a move, an orbit or a push with «wait»)
                    Begin = (p, st) => CameraStep(p, st.W), Setup = (p, st) => CameraStep(p, st.W) } },
                { "fade", new StepKind(StepSeen.Own) { Min = 1, Usage = "fade out|in [секунд]   (увесь кадр у чорне й назад; крок чекає, поки скінчиться)",
                    Check = st => st.W[1] != "out" && st.W[1] != "in" ? "fade out|in [секунд]" : null,
                    Begin = (p, st) => { float s = st.W.Length > 2 ? F(st.W[2], 1.5f) : 1.5f; StoryScreen.Fade(st.W[1] == "out", s); Hold(s); } } },
                { "player", PlayerKind() },
                { "prop", new StepKind(StepSeen.Own) { Min = 1, Usage = StoryProps.Usage, PlaceAfter = "from", PlaceOf = w => StoryProps.PlaceOf(w, 1), // (the piece after «from»: the word storycheck reads here; the game asks PlaceOf — where a thing is thrown as well)
                    Check = st => StoryProps.Check(st.W, 1),
                    Begin = PropStep, Setup = PropStep } }, // (played from a later step: the thing in his hand already — or lying where it fell)
                { "weather", air }, { "thor", air }, { "shake", air },
                { "bolt", new StepKind(StepSeen.Own) { Min = 1, PlaceAt = 1, PlaceNot = new[] { "near", "ahead" }, Usage = "bolt near|ahead [n] [all] · bolt <місце> [n] [розкид] [all]",
                    Begin = (p, st) =>
                    {
                        string[] w = st.W;
                        bool all = w[w.Length - 1] == "all";
                        int n = all ? w.Length - 1 : w.Length;
                        string line;
                        if (w[1] == "near" || w[1] == "ahead") line = "bolt " + w[1] + " " + (n > 2 ? w[2] : "1");
                        else
                        {
                            Vector3 at;
                            if (!StoryWhere.Find(w[1], out at)) { Note("bolt: нема місця «" + w[1] + "» — крок пропущено"); return; }
                            line = "bolt " + V(at) + " " + (n > 2 ? w[2] : "1") + " " + (n > 3 ? w[3] : "0");
                        }
                        StoryAir.Play(line, all);
                    } } },
                { "do", new StepKind(StepSeen.World) { Min = 1, Lasting = true, Usage = "do <дія квесту>",
                    Check = st => { string why = StoryActions.Check(st.Text.Substring(2).Trim(), true); return why != null ? "do: " + why : null; },
                    Begin = (p, st) => StoryActions.Do(p, null, st.Text.Substring(2).Trim()) } },
                { "drop", new StepKind(StepSeen.World) { Min = 2, PlaceAt = 2, Lasting = true, Usage = "drop <предмет> <місце|player|npc.x>   (річ лишається на землі там)",
                    Begin = (p, st) => Drop(p, st.W[1], st.W[2]) } },
                { "door", new StepKind(StepSeen.World) { Min = 2, PlaceAt = 1, Lasting = true, Usage = "door <місце> open|close",
                    Check = st => st.W[2] != "open" && st.W[2] != "close" ? "door <місце> open|close" : null,
                    Begin = (p, st) => Door(st.W[1], st.W[2] == "open") } },
                { "bird", BirdKind() },
                { "npc", NpcKind() },
            };
        }

        private static void WatchersStep(Player p, Step st) { StoryWatch.Take(st.W[1] == "take", st.W.Length > 2 && st.W[2] == "camera"); }

        private static string Check(Step st)
        {
            string[] w = st.W;
            StepKind k;
            if (w.Length == 0) return "порожній крок";
            if (!Kinds.TryGetValue(w[0], out k)) return "невідомий крок «" + w[0] + "» (є: " + string.Join(", ", new List<string>(Kinds.Keys).ToArray()) + ")";
            if (w.Length - 1 < k.Min) return k.Usage.Length > 0 ? k.Usage : w[0] + ": мало слів";
            if (k.Verbs != null)
            {
                StepVerb v;
                if (!k.Verbs.TryGetValue(w[k.VerbAt], out v)) return w[0] + ": невідоме «" + w[k.VerbAt] + "» (є: " + string.Join(", ", new List<string>(k.Verbs.Keys).ToArray()) + ")";
                if (w.Length - 1 - k.VerbAt < v.Min) return (v.Usage.Length > 0 ? v.Usage : k.Usage);
                return v.Check != null ? v.Check(st) : null;
            }
            return k.Check != null ? k.Check(st) : null;
        }
    }
}
#endif
