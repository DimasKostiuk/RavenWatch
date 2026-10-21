#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // An errand's modifiers: a rule over the whole errand, not a step. Set in the template:
    //   mods: nodeath            always
    //   maymods: nodeath 30      with this chance (%) when it is given
    // The run keeps the ones it got (ErrandRun.Mods). Each one may end the errand (Breaks: a reason, the errand is
    // lost) and adds notches when it is done anyway (Bonus; doubled on a golden errand, over its golden rune).
    internal static class ErrandMods
    {
        internal sealed class Mod
        {
            public string Id = "", Name = "", Text = "";
            public int Bonus;
        }

        internal static readonly Dictionary<string, Mod> All = new Dictionary<string, Mod>
        {
            { "nodeath", new Mod { Id = "nodeath", Name = Lang.K("Без смерті"), Text = Lang.K("помреш — Одін не зарахує, і доручення пропаде"), Bonus = 1 } },
        };

        internal static bool Known(string id) { return All.ContainsKey(id); }

        // the ones this errand gets now
        internal static List<string> Roll(ErrandDef d)
        {
            List<string> l = new List<string>();
            foreach (string id in d.Mods) if (Known(id) && !l.Contains(id)) l.Add(id);
            foreach (KeyValuePair<string, float> kv in d.MayMods)
                if (Known(kv.Key) && !l.Contains(kv.Key) && UnityEngine.Random.value * 100f < kv.Value) l.Add(kv.Key);
            return l;
        }

        // a deed of the game that ends the errand: why (said to the player), or null
        internal static string Breaks(ErrandRun r, Ev type)
        {
            if (r.Mods.Count == 0) return null;
            if (type == Ev.Death && r.Mods.Contains("nodeath")) return Lang.T("ти загину{в|ла}");
            return null;
        }

        internal static int Bonus(ErrandRun r)
        {
            int n = 0;
            Mod m;
            foreach (string id in r.Mods) if (All.TryGetValue(id, out m)) n += m.Bonus;
            return n;
        }

        // an errand done: each of its modifiers may bring a spark of Odin (a star on a random rune, RuneStars) in place of
        // its notches; no spark (or no rune to take it) — its notches. Returns the notches (doubled on a golden errand).
        internal static int Pay(Player p, ErrandRun r, bool golden)
        {
            int n = 0;
            Mod m;
            float chance = golden ? RuneStars.SparkGolden : RuneStars.SparkChance;
            foreach (string id in r.Mods)
            {
                if (!All.TryGetValue(id, out m)) continue;
                if (UnityEngine.Random.value * 100f < chance && RuneStars.Spark(p) != null) continue;
                n += m.Bonus * (golden ? 2 : 1);
            }
            return n;
        }

        // the lines for the book: «☠ Без смерті — помреш…»
        internal static List<string> Lines(ErrandRun r)
        {
            List<string> l = new List<string>();
            Mod m;
            foreach (string id in r.Mods) if (All.TryGetValue(id, out m)) l.Add("☠ " + Lang.T(m.Name) + " — " + Lang.T(m.Text));
            return l;
        }
    }

    // Odin's riddles (the «riddle» slot and step): the thing is never named in the book, only the riddle; it is laid in
    // the nest's bowl and given to Odin. One is picked by your stage (its boss beaten) when the errand is given.
    internal static class Riddles
    {
        internal sealed class Riddle
        {
            public string Id, Prefab, Needs, Text;
            public int Count;
            public Riddle(string id, string prefab, int count, string needs, string text) { Id = id; Prefab = prefab; Count = count; Needs = needs; Text = text; }
        }

        internal static readonly List<Riddle> All = new List<Riddle>
        {
            new Riddle("dandelion", "Dandelion", 5, "", Lang.K("Жовта, як сонце, а постаріє — біла, як сивина, і розлітається від подиху.")),
            new Riddle("resin", "Resin", 10, "", Lang.K("Сльоза дерева, що горить ясніше за саме дерево.")),
            new Riddle("feathers", "Feathers", 10, "", Lang.K("Легке, як подих, а без нього жодна стріла не знайде дороги.")),
            new Riddle("flint", "Flint", 10, "", Lang.K("Камінь, що пам'ятає першу сокиру і перший вогонь.")),
            new Riddle("raspberry", "Raspberry", 10, "", Lang.K("Червона й солодка, ховається в кущах на узліссі, де колись горіло.")),
            new Riddle("mushroom", "Mushroom", 5, "", Lang.K("Росте без насіння в тіні, носить червоний капелюх і нікому не кланяється.")),
            new Riddle("bones", "BoneFragments", 10, "defeated_eikthyr", Lang.K("Те, що лишається, коли м'ясо пішло, а гордість ще ні.")),
            new Riddle("thistle", "Thistle", 5, "defeated_eikthyr", Lang.K("Синій колючий цвіт, що світиться в темряві лісу, де ходять тролі.")),
            new Riddle("honey", "Honey", 5, "defeated_eikthyr", Lang.K("Солодке, яке стережуть тисячі маленьких жал.")),
            new Riddle("entrails", "Entrails", 5, "defeated_gdking", Lang.K("Те, що мерці носять у собі, хоч давно вже не їдять.")),
            new Riddle("guck", "Guck", 5, "defeated_gdking", Lang.K("Зелене й липке, звисає з болотних дерев і чвакає, коли його рвуть.")),
            new Riddle("ooze", "Ooze", 5, "defeated_gdking", Lang.K("Лишається від того, хто помер без кісток, і вибухає в чужих руках.")),
            new Riddle("wolffang", "WolfFang", 5, "defeated_bonemass", Lang.K("Біле й гостре: вило на місяць, поки не стало твоїм.")),
            new Riddle("obsidian", "Obsidian", 10, "defeated_bonemass", Lang.K("Чорне скло, народжене у вогні гори, гостріше за будь-який ніж.")),
            new Riddle("crystal", "Crystal", 3, "defeated_bonemass", Lang.K("Холодне, як гора, а в ньому видно те, чого не бачить око.")),
            new Riddle("barley", "Barley", 10, "defeated_dragon", Lang.K("Золоте зерно рівнин: з нього печуть хліб і варять те, від чого співають.")),
            new Riddle("tar", "Tar", 5, "defeated_dragon", Lang.K("Чорна смола, в якій грузне і вовк, і фулінг, а вогонь її любить.")),
            new Riddle("coins", "Coins", 50, "", Lang.K("За нього торговці віддадуть усе, а ворону воно потрібне хіба що блищати.")),
        };

        internal static Riddle Find(string id) { foreach (Riddle r in All) if (r.Id == id) return r; return null; }

        // one that fits your stage, at random (null when none)
        internal static Riddle Pick(Player p)
        {
            List<Riddle> can = new List<Riddle>();
            foreach (Riddle r in All)
                if (r.Needs.Length == 0 || Stage.Has(p, r.Needs))
                    if (ObjectDB.instance == null || ObjectDB.instance.GetItemPrefab(r.Prefab) != null) can.Add(r);
            return can.Count > 0 ? can[UnityEngine.Random.Range(0, can.Count)] : null;
        }
    }

    // A fight's music while a champion lord stands (the game's own boss music, else its battle music); stopped when it
    // falls or you go.
    internal static class FightMusic
    {
        private static string _name;
        private static bool _on;
        private static readonly FieldInfo Triggered = AccessTools.Field(typeof(MusicMan), "m_triggeredMusic");
        private static readonly MethodInfo StopIt = AccessTools.Method(typeof(MusicMan), "StopMusic");

        private static string Name()
        {
            if (_name != null) return _name;
            _name = "combat";
            if (MusicMan.instance == null) return _name;
            List<string> names = new List<string>();
            foreach (MusicMan.NamedMusic m in MusicMan.instance.m_music)
            {
                if (m == null || string.IsNullOrEmpty(m.m_name)) continue;
                names.Add(m.m_name);
                if (_name == "combat" && m.m_enabled && m.m_clips != null && m.m_clips.Length > 0 && m.m_name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0) _name = m.m_name;
            }
            Errands.Log("fight music: «" + _name + "» (the game has: " + string.Join(", ", names.ToArray()) + ")");
            return _name;
        }

        // every couple of seconds while the fight goes on
        internal static void Keep()
        {
            if (MusicMan.instance == null) return;
            MusicMan.instance.TriggerMusic(Name());
            _on = true;
        }

        internal static void Stop()
        {
            if (!_on) return;
            _on = false;
            try
            {
                if (MusicMan.instance == null) return;
                if (Triggered != null) Triggered.SetValue(MusicMan.instance, null);
                if (StopIt != null) StopIt.Invoke(MusicMan.instance, null);
            }
            catch (Exception e) { Errands.Log("fight music stop: " + e.Message); }
        }
    }
}
#endif
