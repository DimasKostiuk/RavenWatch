#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The track's own conditions (registered into StoryCond on the first load):
    //   world <x> · wvar <x> [>=|<…] N      the world's story (StoryWorld)
    //   chose <choice> [way]                 the way the player went at a quest's choice (StoryQuests ways); no way — chose at all
    //   step <quest> <stage>                 standing at that stage of the quest now (a place's touch glows only then)
    //   played N                             minutes of real play in this character (idle minutes do not count)
    //   near <place> [metres=15]             standing that near (StoryWhere)
    //   before <thing> [metres=6] [half=2]   straight out before it, on its outer side, within half m of its middle line (a scene that walks you
    //                                        to a gate: from there nothing stands in the way; from the side, the stairs did)
    //   calm                                 a good moment for something to happen: alive, outdoors, awake, not
    //                                        fighting, not on a ship, not in a cutscene
    //   sky                                  open sky overhead (no roof, not underground: a raven may circle over you)
    //   since <stamp> N                      N minutes of real play since the action «stamp <stamp>» (a pause in a story:
    //                                        «10 minutes after the first raven»; an idle game does not count)
    //   gate <place> open|shut               a place's great door (locations.list «gate:»; its own world key: the one fact — never a story flag)
    //   placed <place> · hearth <place> N    the world chose its spot (it is built when someone first comes near) · its hearth burns at least N
    //   state <place> [>=…] N[.K]            its state and sub-state (1.2: abandoned · kindled)
    //   been <place>                         the player has stood on its ground (in its no-build ring) — written once (Been)
    //   (world, wvar, placed, gate, hearth, state ask only the world: a place's server rules may use them — StoryCond.Kind.World)
    internal static class StoryTrackConds
    {
        internal const string PlayedVar = "played_s";
        private static bool _done;
        private static float _lastInput = -999f, _unsaved, _saveAt;

        internal static void Register()
        {
            if (_done) return;
            _done = true;
            StoryModules.Conds(); // (the modules' own conditions)
            StoryCond.Register("world", new StoryCond.Kind { Usage = "world <назва>   (світовий прапорець сюжету)", Min = 1,
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»", Test = (p, a) => StoryWorld.Flag(a[1]), World = true });
            StoryCond.Register("wvar", new StoryCond.Kind { Usage = "wvar <назва> [>=|<|…] N   (світове число)", Min = 2,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryCond.CmpCheck(a, 2),
                Test = (p, a) => StoryCond.Compare(StoryWorld.Var(a[1]), a, 2), World = true });
            StoryCond.Register("chose", new StoryCond.Kind { Usage = "chose <вибір> [шлях]   (яким шляхом гравець пішов; без шляху — чи вибрав узагалі)", Min = 1,
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Test = (p, a) => { string c = StoryQuests.Chosen(p, a[1]); return a.Length > 2 ? c == a[2] : c.Length > 0; } });
            StoryCond.Register("step", new StoryCond.Kind { Usage = "step <квест> <етап>   (гравець зараз на цьому етапі квесту)", Min = 2,
                Check = a => Story.GoodName(a[1]) && Story.GoodName(a[2]) ? null : "погана назва «" + a[1] + " " + a[2] + "»",
                Test = (p, a) => StoryQuests.IsAt(p, a[1], a[2]) });
            StoryCond.Register("quest", new StoryCond.Kind { Usage = "quest <id> [done|active|failed|started]   (без стану: done; квест сервера читає світ)", Min = 1,
                Check = a => a.Length > 2 && System.Array.IndexOf(new[] { Story.Done, Story.Active, Story.Failed, "started" }, a[2]) < 0 ? "стан квесту: done | active | failed | started" : null,
                Test = (p, a) =>
                {
                    string want = a.Length > 2 ? a[2] : Story.Done, has = StoryQuests.StateOf(p, a[1]);
                    return want == "started" ? has.Length > 0 : has == want;
                } });
            StoryCond.Register("played",new StoryCond.Kind { Usage = "played [>=|<|…] N   (хвилин живої гри цим персонажем)", Min = 1,
                Check = a => StoryCond.CmpCheck(a, 1), Test = (p, a) => StoryCond.Compare(Played(p) / 60, a, 1) });
            StoryCond.Register("before", new StoryCond.Kind { Usage = "before <річ> [метрів=6] [пів-ширини=2]   (прямо перед нею, із зовнішнього боку)", Min = 2,
                Check = a => (a.Length > 2 && Errands.I(a[2], -1) < 1) || (a.Length > 3 && Errands.I(a[3], -1) < 1) ? "before: метри — числа від 1" : null,
                Test = (p, a) =>
                {
                    Vector3 at, mid, way;
                    float m = a.Length > 2 ? Errands.I(a[2], 6) : 6f, half = a.Length > 3 ? Errands.I(a[3], 2) : 2f;
                    if (!StoryWhere.Find(a[1], out at) || Utils.DistanceXZ(at, p.transform.position) > m + half + 2f || !BeforeOf(a[1], out mid, out way)) return false; // (far off: only the distance; its colliders only within a few m of it)
                    Vector3 d = p.transform.position - mid; d.y = 0f;
                    float ahead = Vector3.Dot(d, way);
                    return ahead >= 0f && ahead <= m && Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, way))) <= half;
                } });
            StoryCond.Register("near", new StoryCond.Kind { Usage = "near <місце> [метрів=15]   (hall, hall.<назва>, місце автора)", Min = 1,
                Check = a => a.Length > 2 && Errands.I(a[2], -1) < 1 ? "near: метри — число від 1" : null,
                Test = (p, a) =>
                {
                    Vector3 at;
                    float r = a.Length > 2 ? Errands.I(a[2], 15) : 15f;
                    return StoryWhere.Find(a[1], out at) && Utils.DistanceXZ(at, p.transform.position) <= r;
                } });
            StoryCond.Register("npcclip", new StoryCond.Kind { Usage = "npcclip <хто> <кліп>   (цей кліп чи його частина зараз на людині)", Min = 2, Test = (p, a) => StoryHold.InClip(Npcs.Find(a[1]), a[2]) });
            StoryCond.Register("npcprop", new StoryCond.Kind { Usage = "npcprop <хто>   (людина зараз тримає річ у руці чи на руці: своя річ або річ сцени; та, що лежить на землі, — ні)", Min = 1, Test = (p, a) => StoryProps.InHand(Npcs.Find(a[1])) });
            StoryCond.Register("npcat", new StoryCond.Kind { Usage = "npcat <місце> [метрів]   (людина, з якою говориш, зараз у цьому місці: без метрів — на його землі за кресленням; річ чи мітка без метрів — 15 м)", Min = 1,
                Check = a => a.Length > 2 && Errands.I(a[2], -1) < 1 ? "npcat: метри — число від 1" : null,
                Test = (p, a) =>
                {
                    Vector3 at;
                    NpcActor him = NpcTalk.Him;
                    if (him == null) return false;
                    string piece;
                    Loc loc = StoryWhere.Place(a[1], out piece);
                    if (loc != null && piece.Length == 0 && a.Length <= 2) return loc.Holds(him.transform.position); // (the Hall's walls are no circle: a radius took in the yard outside its gate)
                    return StoryWhere.Find(a[1], out at) && Utils.DistanceXZ(at, him.transform.position) <= (a.Length > 2 ? Errands.I(a[2], 15) : 15f);
                } });
            StoryCond.Register("calm", new StoryCond.Kind { Usage = "calm   (жива, не в підземеллі, не спить, не б'ється, не на кораблі, не будує, не біля станка, не сидить, вікна закриті)", Test = (p, a) => Calm(p), Why = (p, a) => WhyNotCalm(p) });
            StoryCond.Register("inside", new StoryCond.Kind { Usage = "inside <місце>   (гравець у будівлі цього місця: стоїть на її підлозі й під її дахом; поруч під виступом даху — ні)", Min = 1,
                Test = (p, a) => Inside(p, a[1]) });
            StoryCond.Register("touched", new StoryCond.Kind { Usage = "touched <місце.назва>   (гравець щойно натиснув E на цій сюжетній речі: сцена, що чекає дотику, «until touched …»)", Min = 1,
                Test = (p, a) => LocTouch.UsedKey == a[1] && Time.time - LocTouch.UsedAt < 2f });
            StoryCond.Register("sky", new StoryCond.Kind { Usage = "sky   (над головою небо: не під дахом, не в підземеллі)", Test = (p, a) => !Senses.Indoors && !Senses.Underground, Why = (p, a) => Senses.Underground ? "у підземеллі" : "під дахом" });
            StoryCond.Register("since", new StoryCond.Kind { Usage = "since <мітка> [>=|<|…] N   (хвилин живої гри від дії «stamp <мітка>»; без мітки — ні)", Min = 2,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryCond.CmpCheck(a, 2),
                Test = (p, a) => Story.Var(p, StampVar(a[1])) > 0 && StoryCond.Compare((Played(p) - Story.Var(p, StampVar(a[1]))) / 60, a, 2),
                Why = (p, a) => Story.Var(p, StampVar(a[1])) > 0 ? "минуло " + (Played(p) - Story.Var(p, StampVar(a[1]))) / 60 + " хв" : "мітки «" + a[1] + "» нема" });
            StoryActions.Register("stamp", new StoryActions.Kind { Min = 1, Usage = "stamp <мітка>   (запам'ятати хвилину живої гри: умова «since <мітка> N»)",
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Do = (p, q, w, rest) => { Story.SetVar(p, StampVar(w[1]), Mathf.Max(1, Played(p))); return null; } });
            // a place of the story, by its id or its alias (locations.list): what the world says of it — the same for everyone
            StoryCond.Register("placed", new StoryCond.Kind { Usage = "placed <місце>   (світ уже вибрав, де воно стоїть — збудоване чи ще ні)", Min = 1, World = true,
                Check = a => PlaceCheck(a[1]), Test = (p, a) => { Vector3 at; Quaternion r; Loc l = Locs.Get(a[1]); return l != null && l.Where(out at, out r); } });
            StoryCond.Register("gate", new StoryCond.Kind { Usage = "gate <місце> open|shut   (головна брама місця, рядок gate: — її власний ключ світу)", Min = 2, World = true,
                Check = a => PlaceCheck(a[1]) ?? (a[2] != "open" && a[2] != "shut" ? "gate <місце> open|shut" : null),
                Test = (p, a) => LocState.GateOpenOf(Locs.Get(a[1])) == (a[2] == "open") });
            StoryCond.Register("hearth", new StoryCond.Kind { Usage = "hearth <місце> [>=|<|…] N   (вогнище місця: 0 холодне, 1 Хель, 2 Варти)", Min = 2, World = true,
                Check = a => PlaceCheck(a[1]) ?? StoryCond.CmpCheck(a, 2), Test = (p, a) => StoryCond.Compare(LocHearth.LevelOf(Locs.Get(a[1])), a, 2) });
            StoryCond.Register("state", new StoryCond.Kind { Usage = "state <місце> [>=|<|…] N[.K]   (стан місця й субстан: 1.2 — «закинута · розпалена»)", Min = 2, World = true,
                Check = a => PlaceCheck(a[1]) ?? StateCheck(a), Test = (p, a) => StateTest(Locs.Get(a[1]), a) });
            StoryCond.Register("been", new StoryCond.Kind { Usage = "been <місце>   (гравець уже стояв на землі цього місця)", Min = 1,
                Check = a => PlaceCheck(a[1]), Test = (p, a) => { Loc l = Locs.Get(a[1]); return l != null && Story.Flag(p, BeenFlag(l)); } });
        }

        internal static bool Calm(Player p) { return WhyNotCalm(p) == null; }

        private static string PlaceCheck(string id) { return Locs.Get(id) == null ? "нема місця «" + id + "» (locations.list)" : null; }

        // «state zala >= 1.2»: its state ×100 + sub-state, compared as one number (1.2 → 102)
        private static string StateCheck(string[] a)
        {
            string[] b = Hundreds(a);
            return b != null && StoryCond.CmpCheck(b, 2) == null ? null : "state <місце> [>=|<|…] N[.K]";
        }

        private static bool StateTest(Loc l, string[] a)
        {
            string[] b = Hundreds(a);
            return l != null && b != null && StoryCond.Compare(LocState.StateOf(l) * 100 + LocState.SubOf(l), b, 2);
        }

        private static string[] Hundreds(string[] a)
        {
            string[] b = (string[])a.Clone();
            int i = b.Length - 1, s, k;
            string num = b[i], sign = "";
            while (num.Length > 0 && (num[0] == '<' || num[0] == '>' || num[0] == '=')) { sign += num[0]; num = num.Substring(1); }
            if (!LocState.SubNumber(num, out s, out k)) return null;
            b[i] = sign + (s * 100 + k);
            return b;
        }

        // been on a place's ground (the personal flag, written once: Been — the places' look twice a second)
        private static string BeenFlag(Loc l) { return "been." + l.Id + "." + (ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L); } // (a character's, in this world: a place stands elsewhere in another)
        internal static void Been(Player p)
        {
            foreach (Loc l in Locs.All)
            {
                if (!l.Built || Story.Flag(p, BeenFlag(l)) || !l.InNoBuild(p.transform.position)) continue;
                Story.SetFlag(p, BeenFlag(l), true);
            }
        }

        // on a floor of that place and under a roof of it, by the pieces themselves (the game's shelter is true under the eaves outside too)
        internal static bool Inside(Player p, string place)
        {
            return LocState.InsideOf(p, place);
        }

        // a thing's front, kept BeforeEvery s (StoryWhere.Before reads its colliders; a gate swinging moves it, so not for good)
        internal static float BeforeEvery = 2f;
        private static string _beforeOf;
        private static float _beforeAt = -99f;
        private static bool _beforeOk;
        private static Vector3 _beforeMid, _beforeWay;
        private static bool BeforeOf(string thing, out Vector3 mid, out Vector3 way)
        {
            if (thing != _beforeOf || Time.time - _beforeAt > BeforeEvery) { _beforeOf = thing; _beforeAt = Time.time; _beforeOk = StoryWhere.Before(thing, out _beforeMid, out _beforeWay); }
            mid = _beforeMid; way = _beforeWay;
            return _beforeOk;
        }

        private static string StampVar(string stamp) { return "at_" + stamp; } // (the second of real play «stamp» was put at: the character's var)

        // what keeps the moment from being calm (null: calm) — the panel says it
        internal static string WhyNotCalm(Player p)
        {
            if (p == null) return "нема гравця";
            if (p.IsDead()) return "мертвий";
            if (p.InInterior()) return "у підземеллі";
            if (p.IsSleeping()) return "спить";
            if (p.IsTeleporting()) return "телепорт";
            if (p.GetStandingOnShip() != null || p.IsAttachedToShip()) return "на кораблі";
            if (p.InCutscene()) return "сцена гри";
            if (p.InPlaceMode()) return "будує";
            if (p.GetCurrentCraftingStation() != null) return "біля станка";
            if (p.IsAttached() || p.IsSitting()) return "сидить";
            if (InventoryGui.IsVisible() || StoreGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || TextInput.IsVisible() || Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus())) return "відкрите вікно";
            if (Senses.Pressed(p))
            {
                Character c = Senses.NearestFoe != null ? Senses.NearestFoe : Senses.Boss;
                return "бій або ворог поруч" + (c != null ? ": " + c.name.Replace("(Clone)", "") + " " + Localization.instance.Localize(c.m_name) + " за " + Mathf.RoundToInt(Vector3.Distance(c.transform.position, p.transform.position)) + " м" : " (хто — не видно)");
            }
            return null;
        }

        // seconds of real play (kept in the character: StoryState n.played_s)
        internal static int Played(Player p) { return Story.Var(p, PlayedVar) + Mathf.FloorToInt(_unsaved); }

        // every frame: a minute counts while someone touches the keys or the mouse within it (an idle game does not)
        internal static void Count(Player p)
        {
            if (Input.anyKey || Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.01f) _lastInput = Time.time;
            if (Time.time - _lastInput < 60f && !Game.IsPaused()) _unsaved += Time.deltaTime;
            if (Time.time < _saveAt || _unsaved < 1f) return;
            _saveAt = Time.time + 30f;
            int whole = Mathf.FloorToInt(_unsaved);
            _unsaved -= whole;
            Story.SetVar(p, PlayedVar, Story.Var(p, PlayedVar) + whole, true); // (every 30 s: not into the log)
        }

        // the author's «skip the wait»
        internal static void AddMinutes(Player p, int min) { Story.SetVar(p, PlayedVar, Story.Var(p, PlayedVar) + min * 60); }
    }
}
#endif
