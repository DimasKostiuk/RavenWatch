#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A raven's thought about the player you look at. When you call Hugin's or Munin's thought while aiming at another
    // player (near the middle of the screen, within Range, nothing solid between), the thought is most often (Chance)
    // about that player: their ravens and their names, how your ravens know each other, what you went through together
    // (the bosses in the ravens' memory, the day you met), how they are now (hurt, asleep), what is on their head and in
    // their hands (an axe, a pickaxe, a weapon), how close they stand, the ship you share, the night or the storm, envy
    // of their legend, a blow between the two of you just now.
    // Never over real danger (an enemy close, you nearly dead): then the raven says what it would anyway.
    //
    // The words are heard over your raven by everyone near, the one you look at too. And now and then (Retort, never
    // twice in a row at the same player) one of their ravens snaps back: a two-line talk in the bridge bubble
    // (CrossTalk "about"), so both sides see the same thing.
    internal static class AboutTalk
    {
        internal static float Range = 25f;          // m: no further than this
        internal static float Chance = 0.85f;       // the thought is about them
        internal static float RetortChance = 0.35f; // one of their ravens answers (more with old pals, less with strangers)
        internal static float RetortPause = 180f;   // s: not again at the same player sooner
        private const float NearAngle = 15f, FarAngle = 3.5f; // ° off the middle of the screen allowed: near / at Range

        internal sealed class Shot
        {
            public Player Target; public string Theme, Line; public int Raven = -1; // Raven: their raven named in it (0 Hugin, 1 Munin)
        }

        private static readonly Dictionary<long, float> RetortAt = new Dictionary<long, float>();
        private static readonly Dictionary<long, string> LastTheme = new Dictionary<long, string>(); // what was said about each player last
        private static readonly Dictionary<long, float> HitYouAt = new Dictionary<long, float>(), YouHitAt = new Dictionary<long, float>();
        internal static float HitMemory = 60f; // s a blow between you is still fresh

        // a blow between you and another player (Diary's hit patches)
        internal static void HitYou(Player by) { if (by != null && by != Player.m_localPlayer) HitYouAt[by.GetPlayerID()] = Time.time; }
        internal static void YouHit(Player whom) { if (whom != null && whom != Player.m_localPlayer) YouHitAt[whom.GetPlayerID()] = Time.time; }
        private static bool Fresh(Dictionary<long, float> d, long id) { float at; return d.TryGetValue(id, out at) && Time.time - at < HitMemory; }
        private static int _blockMask = -1;

        // ---------------------------------------------------------------- whom you look at

        internal static Player Target(Player p)
        {
            Camera cam = Utils.GetMainCamera();
            if (p == null || cam == null) return null;
            if (_blockMask < 0) _blockMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small");
            Vector3 eye = cam.transform.position, fwd = cam.transform.forward;
            Player best = null;
            float bestOff = float.MaxValue;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == p || o.IsDead() || Shadow.Flagged(o)) continue;
                if (Vector3.Distance(p.transform.position, o.transform.position) > Range) continue;
                Vector3 at = o.GetCenterPoint(), dir = at - eye;
                float dist = dir.magnitude;
                if (dist < 0.5f) continue;
                // about a body's width either side, but never stricter than FarAngle nor looser than NearAngle
                float allow = Mathf.Clamp(Mathf.Atan2(1.2f, dist) * Mathf.Rad2Deg, FarAngle, NearAngle);
                float off = Vector3.Angle(fwd, dir);
                if (off > allow || off >= bestOff) continue;
                if (Physics.Linecast(eye, at, _blockMask)) continue; // a wall, a hill, a house between
                best = o; bestOff = off;
            }
            return best;
        }

        // ---------------------------------------------------------------- the thought

        private sealed class Theme { public string Tag; public float Score; public Theme(string t, float s) { Tag = t; Score = s; } }

        // a thought about the one you look at, or null (nobody there, danger, the dice, nothing fits)
        internal static Shot Try(Player p, bool munin)
        {
            if (p == null || Plugin.Instance == null || Trust.Hostile(Trust.Worn(munin))) return null;
            if (p.GetHealthPercentage() < 0.35f || BaseAI.HaveEnemyInRange(p, p.transform.position, 15f)) return null; // danger first
            Player t = Target(p);
            if (t == null || UnityEngine.Random.value > Chance) return null;

            string dir = Plugin.Instance.PhraseDir;
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["name"] = p.GetPlayerName();
            v["friend"] = t.GetPlayerName();
            List<Theme> themes = new List<Theme>();
            Func<float> jitter = () => UnityEngine.Random.Range(0f, 15f);

            // how they are now
            if (t.GetHealthPercentage() < 0.35f) themes.Add(new Theme("about_hurt", 100f));
            if (t.InBed()) themes.Add(new Theme("about_sleep", 95f));
            long tid = t.GetPlayerID();
            if (Fresh(HitYouAt, tid)) themes.Add(new Theme("about_hityou", 92f));
            if (Fresh(YouHitAt, tid)) themes.Add(new Theme("about_youhit", 90f));
            float dist = Vector3.Distance(p.transform.position, t.transform.position);
            if (dist < 3f) themes.Add(new Theme("about_close", 50f + jitter()));
            if (p.GetStandingOnShip() != null && dist < 12f) themes.Add(new Theme("about_ship", 58f + jitter()));
            string env = EnvMan.instance != null && EnvMan.instance.GetCurrentEnvironment() != null ? EnvMan.instance.GetCurrentEnvironment().m_name : "";
            if (env.IndexOf("storm", StringComparison.OrdinalIgnoreCase) >= 0 || env.IndexOf("thunder", StringComparison.OrdinalIgnoreCase) >= 0)
                themes.Add(new Theme("about_storm", 42f + jitter())); // (its lines speak of thunder: plain rain is no storm)
            else if (EnvMan.IsNight()) themes.Add(new Theme("about_night", 32f + jitter()));

            // what is in their hands
            Skills.SkillType skill;
            string held = Held(t, out skill);
            if (held != null)
            {
                if (skill == Skills.SkillType.Axes) themes.Add(new Theme("about_axe", 40f + jitter()));
                else if (skill == Skills.SkillType.Pickaxes) themes.Add(new Theme("about_pick", 40f + jitter()));
                else { v["weapon"] = held; themes.Add(new Theme("about_weapon", 30f + jitter())); }
            }

            // their ravens: one of them (the more famous name more often)
            int raven = -1;
            string rName = null, rCall = null; Nicks.Kind rKind = Nicks.Kind.Simple;
            List<int> theirs = new List<int>();
            foreach (bool m in new[] { false, true }) if (Shoulder.ModelOf(t, m) != null) theirs.Add(m ? 1 : 0);
            if (theirs.Count > 0)
            {
                raven = theirs[UnityEngine.Random.Range(0, theirs.Count)];
                string n, c; Nicks.Kind k;
                foreach (int r in theirs) if (Nicks.Of(t, r == 1, out n, out c, out k) && k > rKind) raven = r; // a gold or a legend is the one to speak of
                if (!Nicks.Of(t, raven == 1, out rName, out rCall, out rKind)) { rName = Plugin.RavenName(raven == 1); rKind = Nicks.Kind.Simple; } // an older mod: no name published
                v["fraven"] = rName;
                v["fraven_kin"] = Plugin.RavenName(raven == 1);
                themes.Add(new Theme(rKind == Nicks.Kind.Legend ? "about_raven_legend" : rKind == Nicks.Kind.Deed ? "about_raven_gold" : "about_raven",
                    (rKind == Nicks.Kind.Legend ? 75f : rKind == Nicks.Kind.Deed ? 65f : 50f) + jitter()));
                string mn, mc; Nicks.Kind mk;
                if (rKind == Nicks.Kind.Legend && (!Nicks.Of(p, munin, out mn, out mc, out mk) || mk < Nicks.Kind.Legend))
                    themes.Add(new Theme("about_envy", 70f + jitter())); // a legend on their shoulder, none on yours
            }
            else themes.Add(new Theme("about_noraven", 55f + jitter()));

            // how your ravens know each other
            int talks = CrossTalk.TalksWith(p, t);
            ItemDrop.ItemData mine = RavenSeat.On(p, munin);
            Memory.Mem met = null, boss = null;
            if (mine != null)
                foreach (Memory.Mem m in Memory.Load(mine))
                {
                    if (m.Kind == "friend" && m.Who == t.GetPlayerName()) met = m;
                    if (m.Kind == "boss" && m.Extra.Contains(t.GetPlayerName()) && (boss == null || m.Day > boss.Day)) boss = m;
                }
            if (talks == 0 && met == null) themes.Add(new Theme("about_new", 60f + jitter()));
            else if (talks >= CrossTalk.PalsAt) themes.Add(new Theme("about_pals", 55f + jitter()));
            else themes.Add(new Theme("about_known", 40f + jitter()));
            if (met != null) { v["since"] = Memory.When(met.Day); themes.Add(new Theme("about_since", 45f + jitter())); }
            if (boss != null)
            {
                int bi = Array.IndexOf(Strike.Bosses, boss.Who);
                if (bi >= 0 && bi < Memory.BossAcc.Length)
                {
                    v["mboss"] = Memory.BossAcc[bi];
                    v["m_when"] = Memory.When(boss.Day);
                    themes.Add(new Theme("about_boss", 70f + jitter()));
                }
            }

            // what is on their head
            string helmet = Helmet(t);
            if (helmet != null) { v["helmet"] = helmet; themes.Add(new Theme("about_helmet", 35f + jitter())); }
            else themes.Add(new Theme("about_nohelmet", 40f + jitter()));
            themes.Add(new Theme("about_any", 20f + jitter()));

            // the theme said to this player last time steps back, so looking again brings something new
            string was;
            if (LastTheme.TryGetValue(tid, out was)) foreach (Theme th in themes) if (th.Tag == was && th.Score < 90f) th.Score -= 35f;

            // one of the best three, the first most often (as the thoughts do); a theme with nothing to say drops out
            themes.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (themes.Count > 3) themes.RemoveRange(3, themes.Count - 3);
            int level = mine != null ? Mathf.Max(1, Trust.Level(mine)) : 1;
            bool female = Grammar.Female(p), theyFemale = Grammar.Female(t);
            while (themes.Count > 0)
            {
                int roll = UnityEngine.Random.Range(0, 100);
                int i = roll < 60 || themes.Count == 1 ? 0 : (roll < 85 || themes.Count == 2 ? 1 : 2);
                string line = Composer.ComposeCross(dir, munin, themes[i].Tag, level, female, theyFemale, v, Voice.Code(mine), true);
                if (!string.IsNullOrEmpty(line))
                {
                    LastTheme[tid] = themes[i].Tag;
                    return new Shot { Target = t, Theme = themes[i].Tag, Line = line, Raven = themes[i].Tag.StartsWith("about_raven") || themes[i].Tag == "about_envy" ? raven : -1 };
                }
                themes.RemoveAt(i);
            }
            return null;
        }

        // what they hold in the right hand (a weapon or a tool, as every game draws it) and its skill; null: empty-handed
        internal static string Held(Player t, out Skills.SkillType skill)
        {
            skill = Skills.SkillType.None;
            try
            {
                ZNetView nv = t.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || ObjectDB.instance == null) return null;
                int hash = nv.GetZDO().GetInt(ZDOVars.s_rightItem, 0);
                if (hash == 0) return null;
                GameObject prefab = ObjectDB.instance.GetItemPrefab(hash);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return null;
                ItemDrop.ItemData.SharedData sh = drop.m_itemData.m_shared;
                skill = sh.m_skillType;
                bool work = skill == Skills.SkillType.Axes || skill == Skills.SkillType.Pickaxes;
                if (sh.m_itemType == ItemDrop.ItemData.ItemType.Torch || skill == Skills.SkillType.Fishing || (sh.m_itemType == ItemDrop.ItemData.ItemType.Tool && !work))
                    return null; // a torch, a hammer, a hoe, a fishing rod: nothing to say
                string name = Localization.instance != null ? Lang.Game(sh.m_name) : sh.m_name;
                return string.IsNullOrEmpty(name) || name.StartsWith("[") ? null : name;
            }
            catch { return null; }
        }

        // the piece on their head as the game names it, or null (bare-headed); every game draws it from the player's ZDO
        private static string Helmet(Player t)
        {
            try
            {
                ZNetView nv = t.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || ObjectDB.instance == null) return null;
                int hash = nv.GetZDO().GetInt(ZDOVars.s_helmetItem, 0);
                if (hash == 0) return null;
                GameObject prefab = ObjectDB.instance.GetItemPrefab(hash);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return null;
                string name = Localization.instance != null ? Lang.Game(drop.m_itemData.m_shared.m_name) : drop.m_itemData.m_shared.m_name;
                return string.IsNullOrEmpty(name) || name.StartsWith("[") ? null : name;
            }
            catch { return null; }
        }

        // ---------------------------------------------------------------- said: alone, or with one of theirs snapping back

        // true: it went as a talk with their raven (the caller says nothing more); false: say the line as usual
        internal static bool Said(Player p, bool munin, Shot shot)
        {
            if (shot == null || shot.Target == null || Plugin.Instance == null) return false;
            Player t = shot.Target;
            long id = t.GetPlayerID();
            float last;
            if (RetortAt.TryGetValue(id, out last) && Time.time - last < RetortPause) return false;
            List<int> can = new List<int>();
            foreach (bool m in new[] { false, true }) if (CrossTalk.CanTalk(t, m)) can.Add(m ? 1 : 0);
            if (can.Count == 0) return false;
            int talks = CrossTalk.TalksWith(p, t);
            float chance = RetortChance * (talks >= CrossTalk.PalsAt ? 1.4f : talks == 0 ? 0.8f : 1f); // old pals bicker more
            if (shot.Raven >= 0 && can.Contains(shot.Raven)) chance += 0.15f; // spoken of by name: it has a word to say
            if (UnityEngine.Random.value > chance) return false;

            // which of theirs answers: the one spoken of, mostly; else either (Munin is quicker to snap)
            int who = shot.Raven >= 0 && can.Contains(shot.Raven) && UnityEngine.Random.value < 0.75f ? shot.Raven
                : can.Count == 1 ? can[0] : (UnityEngine.Random.value < 0.55f ? 1 : 0);
            bool theirMunin = who == 1;
            ItemDrop.ItemData mine = RavenSeat.On(p, munin);
            CrossTalk.Who a = new CrossTalk.Who { P = p, Munin = munin, Level = mine != null ? Mathf.Max(1, Trust.Level(mine)) : 1 };
            CrossTalk.Who b = new CrossTalk.Who { P = t, Munin = theirMunin, Level = Mathf.Max(1, Shoulder.LevelOn(t, theirMunin)) };
            string back = Back(b, a, shot.Theme);
            if (string.IsNullOrEmpty(back)) return false;
            RetortAt[id] = Time.time;
            List<CrossTalk.Who> whos = new List<CrossTalk.Who> { a, b };
            List<CrossTalk.Line> lines = new List<CrossTalk.Line>
            {
                new CrossTalk.Line { Who = 0, To = 1, Text = shot.Line },
                new CrossTalk.Line { Who = 1, To = 0, Text = back },
            };
            CrossTalk.Script("about", whos, lines);
            return true;
        }

        // their raven's answer to what yours said: by the theme's family, else anything sharp
        private static string Back(CrossTalk.Who s, CrossTalk.Who to, string theme)
        {
            string fam;
            switch (theme)
            {
                case "about_hurt": fam = "hurt"; break;
                case "about_sleep": fam = "sleep"; break;
                case "about_boss": fam = "boss"; break;
                case "about_raven": case "about_raven_gold": case "about_raven_legend": fam = "raven"; break;
                case "about_new": fam = "new"; break;
                case "about_helmet": case "about_nohelmet": fam = "helmet"; break;
                case "about_pals": fam = "friend"; break;                       // warm: "my own people"
                case "about_known": case "about_since": fam = "known"; break;   // plain: "I know you"
                case "about_axe": case "about_pick": fam = "work"; break;
                case "about_weapon": fam = "weapon"; break;
                case "about_close": fam = "close"; break;
                case "about_ship": fam = "ship"; break;
                case "about_night": case "about_storm": fam = "night"; break;
                case "about_envy": fam = "envy"; break;
                case "about_hityou": case "about_youhit": fam = "hit"; break;
                default: fam = "any"; break;
            }
            string dir = Plugin.Instance.PhraseDir;
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["name"] = s.P.GetPlayerName();
            v["friend"] = to.P.GetPlayerName();
            v["raven"] = Nicks.Name(to.P, to.Munin);
            v["addr"] = Nicks.Call(to.P, to.Munin);
            v["me"] = Nicks.Name(s.P, s.Munin);
            foreach (string tag in new[] { "about_back_" + fam, "about_back_any" })
            {
                string line = Composer.ComposeCross(dir, s.Munin, tag, s.Level, Grammar.Female(s.P), Grammar.Female(to.P), v, Voice.CodeOn(s.P, s.Munin));
                if (!string.IsNullOrEmpty(line)) return line;
            }
            return null;
        }
    }
}
#endif
