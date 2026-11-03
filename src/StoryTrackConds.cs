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
    //   calm                                 a good moment for something to happen: alive, outdoors, awake, not
    //                                        fighting, not on a ship, not in a cutscene
    //   hallgate open|shut                   the Hall's great door (its own world key: the one fact — never a story flag)
    //   hall · hearth N                      the world chose the Hall's spot (it is built when someone first comes near)
    //                                        · its hearth burns at least N (0 cold, 1 Hel, 2 the Watch)
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
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»", Test = (p, a) => StoryWorld.Flag(a[1]) });
            StoryCond.Register("wvar", new StoryCond.Kind { Usage = "wvar <назва> [>=|<|…] N   (світове число)", Min = 2,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryCond.CmpCheck(a, 2),
                Test = (p, a) => StoryCond.Compare(StoryWorld.Var(a[1]), a, 2) });
            StoryCond.Register("chose", new StoryCond.Kind { Usage = "chose <вибір> [шлях]   (яким шляхом гравець пішов; без шляху — чи вибрав узагалі)", Min = 1,
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Test = (p, a) => { string c = StoryQuests.Chosen(p, a[1]); return a.Length > 2 ? c == a[2] : c.Length > 0; } });
            StoryCond.Register("step", new StoryCond.Kind { Usage = "step <квест> <етап>   (гравець зараз на цьому етапі квесту)", Min = 2,
                Check = a => Story.GoodName(a[1]) && Story.GoodName(a[2]) ? null : "погана назва «" + a[1] + " " + a[2] + "»",
                Test = (p, a) => Story.Quest(p, a[1]) == Story.Active && Story.StageOf(p, a[1]) == a[2] });
            StoryCond.Register("played",new StoryCond.Kind { Usage = "played [>=|<|…] N   (хвилин живої гри цим персонажем)", Min = 1,
                Check = a => StoryCond.CmpCheck(a, 1), Test = (p, a) => StoryCond.Compare(Played(p) / 60, a, 1) });
            StoryCond.Register("near", new StoryCond.Kind { Usage = "near <місце> [метрів=15]   (hall, hall.<назва>, місце автора)", Min = 1,
                Check = a => a.Length > 2 && Errands.I(a[2], -1) < 1 ? "near: метри — число від 1" : null,
                Test = (p, a) =>
                {
                    Vector3 at;
                    float r = a.Length > 2 ? Errands.I(a[2], 15) : 15f;
                    return StoryWhere.Find(a[1], out at) && Utils.DistanceXZ(at, p.transform.position) <= r;
                } });
            StoryCond.Register("calm", new StoryCond.Kind { Usage = "calm   (жива, надворі, не спить, не б'ється, не на кораблі)", Test = (p, a) => Calm(p) });
            StoryCond.Register("hall", new StoryCond.Kind { Usage = "hall   (світ уже вибрав місце Зали Варти — стоїть вона чи ще ні)", Test = (p, a) => { Vector3 at; Quaternion r; return HallSpawn.Where(out at, out r); } });
            StoryCond.Register("hallgate", new StoryCond.Kind { Usage = "hallgate open|shut   (великі двері Зали — її власний ключ, один на світ)", Min = 1,
                Check = a => a[1] != "open" && a[1] != "shut" ? "hallgate open|shut" : null,
                Test = (p, a) => HallState.GateOpenOf(Locs.Hall) == (a[1] == "open") });
            StoryCond.Register("hearth", new StoryCond.Kind { Usage = "hearth [>=|<|…] N   (вогнище Зали: 0 холодне, 1 Хель, 2 Варти)", Min = 1,
                Check = a => StoryCond.CmpCheck(a, 1), Test = (p, a) => StoryCond.Compare(HallHearth.Level, a, 1) });
        }

        internal static bool Calm(Player p) { return WhyNotCalm(p) == null; }

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
            if (Senses.Fight)
            {
                Character c = Senses.Foe != null ? Senses.Foe : Senses.Boss;
                return "бій або ворог поруч" + (c != null ? ": " + Localization.instance.Localize(c.m_name) + " за " + Mathf.RoundToInt(Vector3.Distance(c.transform.position, p.transform.position)) + " м" : " (хто — не видно)");
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
