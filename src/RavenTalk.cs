#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Hugin and Munin talking with each other. A scene is a fixed run of steps (who speaks, which tag), each step put
    // together from parts in the speaker's own tone (TalkPatterns), so the talk holds together and still comes out new.
    //
    // How long: the steps without "?" are always said; the "?" ones each go on by a chance that suits the moment (by the
    // fire they talk more, after a fight less), and the first one skipped ends the scene, so a talk never jumps a step.
    // Every "?" step is written as a fitting last word, so a talk of 2 lines and one of 4 both end well. 4 at most.
    //
    // When (no polling of conditions): a timer wakes the talk every 10-20 min, and events (a boss, a death, a close call,
    // a new land, one raven fed and not the other) leave a reason waiting for the next quiet moment. Only then is it
    // checked once whether both ravens sit on you, awake and yours, with no fight near and nobody else talking; if not,
    // it tries again a little later. By the fire a talk sometimes comes instead of a story. And when you call a raven's
    // thought, the other one now and then answers it.
    //
    // Shown in a talk bubble over your head (TalkBubble); players within 30 m see it too, sent once per talk.
    internal static class RavenTalk
    {
        internal static float EveryMin = 10f, EveryMax = 20f; // minutes between talks out of nothing
        internal static float ReactChance = 0.35f;              // the other raven answers a thought
        internal static float FiresideChance = 0.4f;            // by the fire: a talk instead of a story
        internal const float HearRange = 30f;
        internal const float SettleSeconds = 4f;              // back on the shoulder this long before a talk
        internal const int MaxLines = 4;
        private const string RpcTalk = "RavenWatch_Talk";
        private const string RecentKey = "RavenWatch_talkRecent";
        private const string FileName = "dialogues.txt";
        private const int Revision = 6; // 6: the everyday moments of 2.1.0 (raid, trade, ride, smelter, fermenter, repair, mead, chair, home, trophy, trophy hung, cart, garden); 5: talks on a perch (when=perch); 2: a long walk, a long voyage; 3: crossing into another land; 4: the moments (dungeon, fall, load, portal, fish, wood, ore, snow, storm, dawn, dusk, a friend)

        private sealed class Step { public bool Munin; public string Tag; public bool Optional; }
        private sealed class Scene { public string Id; public string When; public int Weight = 1; public readonly List<Step> Steps = new List<Step>(); }

        private static List<Scene> _scenes;
        private static DateTime _stamp;
        private static string _dir;
        private static readonly Dictionary<string, float> Reasons = new Dictionary<string, float>(); // reason -> until (time)
        private static readonly Dictionary<string, string> ReasonArgs = new Dictionary<string, string>();
        private static float _nextIdle = -1f, _retryAt;
        private static float _replyAt = -1f;
        private static bool _replyMunin;

        // ---------------------------------------------------------------- the scenes (a file you can edit)

        // [id] when=reason weight=N, then the steps: H or M, a tag, a ? for a step that may be left out
        private static readonly string[] Defaults =
        {
            "[idle_a] when=idle", "M dlg_idle_m_open", "H dlg_idle_h_reply", "M? dlg_m_close", "",
            "[idle_b] when=idle", "H dlg_idle_h_open", "M dlg_idle_m_reply", "H? dlg_h_close", "M? dlg_m_close", "",
            "[idle_c] when=idle", "M dlg_player_m", "H dlg_player_h", "M? dlg_m_close", "",
            "[rest_a] when=rest", "H dlg_rest_h", "M dlg_rest_m", "H? dlg_h_soft", "",
            "[rest_b] when=rest", "M dlg_rest_m_open", "H dlg_rest_h_reply", "M? dlg_m_close", "",
            "[rain] when=rain", "M dlg_rain_m", "H dlg_rain_h", "M? dlg_rain_m2", "",
            "[fog] when=fog", "H dlg_fog_h", "M dlg_fog_m", "H? dlg_h_close", "",
            "[night] when=night", "M dlg_night_m", "H dlg_night_h", "M? dlg_m_close", "",
            "[boss] when=boss", "H dlg_boss_h", "M dlg_boss_m", "H? dlg_boss_h2", "",
            "[near_death] when=near_death", "M dlg_near_m", "H dlg_near_h", "M? dlg_m_close", "",
            "[death] when=death", "H dlg_death_h", "M dlg_death_m", "H? dlg_h_close", "",
            "[biome] when=biome", "H dlg_biome_h", "M dlg_biome_m", "H? dlg_h_close", "",
            "[fed_hugin] when=fed_hugin", "M dlg_jeal_m", "H dlg_jeal_h", "M? dlg_jeal_m2", "",
            "[fed_munin] when=fed_munin", "H dlg_jealM_h", "M dlg_jealM_m", "H? dlg_h_close", "",
            "[gap_h] when=gap_h", "H dlg_gap_h_brag", "M dlg_gap_m_reply", "H? dlg_h_close", "",
            "[gap_m] when=gap_m", "M dlg_gap_m_brag", "H dlg_gap_h_reply", "M? dlg_m_close", "",
            "[walk] when=walk", "M dlg_walk_m", "H dlg_walk_h", "M? dlg_m_close", "",
            "[sail] when=sail", "H dlg_sail_h", "M dlg_sail_m", "H? dlg_h_close", "",
            "[cross_h] when=cross", "H dlg_cross_h", "M dlg_cross_m_reply", "H? dlg_h_close", "",
            "[cross_m] when=cross", "M dlg_cross_m", "H dlg_cross_h_reply", "M? dlg_m_close", "",
            "[dungeon] when=dungeon", "H dlg_dun_h", "M dlg_dun_m", "H? dlg_dun_h2", "",
            "[fall] when=fall", "M dlg_fall_m", "H dlg_fall_h", "M? dlg_fall_m2", "",
            "[heavy] when=heavy", "M dlg_heavy_m", "H dlg_heavy_h", "M? dlg_heavy_m2", "",
            "[portal] when=portal", "M dlg_portal_m", "H dlg_portal_h", "M? dlg_portal_m2", "",
            "[fish] when=fish", "M dlg_fish_m", "H dlg_fish_h", "M? dlg_fish_m2", "",
            "[chop] when=chop", "H dlg_chop_h", "M dlg_chop_m", "H? dlg_chop_h2", "",
            "[mine] when=mine", "M dlg_mine_m", "H dlg_mine_h", "M? dlg_mine_m2", "",
            "[snow] when=snow", "M dlg_snow_m", "H dlg_snow_h", "M? dlg_snow_m2", "",
            "[storm] when=storm", "H dlg_storm_h", "M dlg_storm_m", "H? dlg_storm_h2", "",
            "[dawn] when=dawn", "H dlg_dawn_h", "M dlg_dawn_m", "H? dlg_dawn_h2", "",
            "[dusk] when=dusk", "M dlg_dusk_m", "H dlg_dusk_m_h", "M? dlg_dusk_m2", "",
            "[friend] when=friend", "H dlg_friend_h", "M dlg_friend_m", "H? dlg_friend_h2", "",
            "[raid] when=raid", "H dlg_raid_h", "M dlg_raid_m", "H? dlg_raid_h2", "",
            "[trade] when=trade", "M dlg_trade_m", "H dlg_trade_h", "M? dlg_trade_m2", "",
            "[ride] when=ride", "H dlg_ride_h", "M dlg_ride_m", "H? dlg_ride_h2", "",
            "[smelt] when=smelt", "M dlg_smelt_m", "H dlg_smelt_h", "M? dlg_smelt_m2", "",
            "[ferment] when=ferment", "M dlg_ferment_m", "H dlg_ferment_h", "M? dlg_ferment_m2", "",
            "[repair] when=repair", "H dlg_repair_h", "M dlg_repair_m", "H? dlg_repair_h2", "",
            "[potion] when=potion", "M dlg_potion_m", "H dlg_potion_h", "M? dlg_potion_m2", "",
            "[sit] when=sit", "H dlg_sit_h", "M dlg_sit_m", "H? dlg_sit_h2", "",
            "[cozy] when=cozy", "H dlg_cozy_h", "M dlg_cozy_m", "H? dlg_cozy_h2", "",
            "[trophy] when=trophy", "M dlg_trophy_m", "H dlg_trophy_h", "M? dlg_trophy_m2", "",
            "[trophy_hang] when=trophy_hang", "H dlg_hang_h", "M dlg_hang_m", "H? dlg_hang_h2", "",
            "[cart] when=cart", "M dlg_cart_m", "H dlg_cart_h", "M? dlg_cart_m2", "",
            "[plant] when=plant", "H dlg_plant_h", "M dlg_plant_m", "H? dlg_plant_h2", "",
            "[perch_a] when=perch", "H dlg_perch_h_open", "M dlg_perch_m_reply", "H? dlg_h_close", "",
            "[perch_b] when=perch", "M dlg_perch_m_open", "H dlg_perch_h_reply", "M? dlg_m_close", "",
        };

        internal static void Init(string dir)
        {
            _dir = dir;
            try
            {
                string path = Path.Combine(dir, FileName);
                string mark = "# talk rev " + Revision;
                if (!File.Exists(path))
                {
                    List<string> all = new List<string>();
                    all.Add(Lang.T("# Розмови Хугіна й Муніна між собою. Сцена: [назва] when=привід weight=вага, далі кроки по черзі:"));
                    all.Add(Lang.T("#   H або M — хто говорить, далі тег (його шаблони й частини — у hugin_patterns.txt / munin_patterns.txt)."));
                    all.Add(Lang.T("#   H? / M? — крок, якого може й не бути: перший пропущений закінчує розмову, тож пиши його як гарне останнє слово."));
                    all.Add(Lang.T("# Приводи: idle (просто так), rest (біля вогню), walk (довго йдемо), sail (довго пливемо), cross (перейшли в інший край), biome (уперше в новому краї),"));
                    all.Add(Lang.T("#   dungeon (підземелля), fall (падіння), heavy (перевантаження), portal, fish (риба), chop (рубка), mine (руда), snow, storm, dawn, dusk, friend (інший гравець поруч), rain, fog, night, boss, near_death, death, fed_hugin, fed_munin, gap_h, gap_m,"));
                    all.Add(Lang.T("#   perch (обидва на жердині; там же й idle, rest, rain, fog, snow, storm, night, dawn, dusk, friend, gap_h, gap_m)."));
                    all.Add(Lang.T("# Не більше 4 реплік. Файл можна правити під час гри."));
                    all.Add(mark);
                    all.AddRange(Lang.Lines(FileName, Defaults, true));
                    File.WriteAllLines(path, all.ToArray(), new UTF8Encoding(false));
                }
                else
                {
                    // a newer mod brings new scenes: added once to the end, your own stay as they are
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    if (!text.Contains(mark))
                    {
                        List<string> add = new List<string> { "", mark };
                        bool take = false;
                        foreach (string l in Lang.Lines(FileName, Defaults, true))
                        {
                            if (l.StartsWith("[")) take = !text.Contains(l.Substring(0, l.IndexOf(']') + 1));
                            if (take) add.Add(l);
                        }
                        File.AppendAllText(path, "\n" + string.Join("\n", add.ToArray()) + "\n", new UTF8Encoding(false));
                    }
                }
            }
            catch (Exception e) { Log("Talks file: " + e.Message); }
        }

        private static List<Scene> Scenes()
        {
            string path = _dir != null ? Path.Combine(_dir, FileName) : null;
            DateTime stamp = path != null && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (_scenes != null && stamp == _stamp) return _scenes;
            _stamp = stamp;
            string[] lines = Lang.Lines(FileName, Defaults, true);
            try { if (stamp != DateTime.MinValue) lines = File.ReadAllLines(path, Encoding.UTF8); } catch { }
            List<Scene> list = new List<Scene>();
            Scene cur = null;
            foreach (string raw in lines)
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                if (l.StartsWith("["))
                {
                    int end = l.IndexOf(']');
                    if (end < 0) { cur = null; continue; }
                    cur = new Scene { Id = l.Substring(1, end - 1).Trim(), When = "idle" };
                    foreach (string part in l.Substring(end + 1).Split(new[] { ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int eq = part.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = part.Substring(0, eq).Trim().ToLowerInvariant(), v = part.Substring(eq + 1).Trim();
                        if (k == "when") cur.When = v.ToLowerInvariant();
                        else if (k == "weight") { int w; if (int.TryParse(v, out w)) cur.Weight = Mathf.Clamp(w, 1, 20); }
                    }
                    list.Add(cur);
                    continue;
                }
                if (cur == null || cur.Steps.Count >= MaxLines) continue;
                string[] bits = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (bits.Length < 2) continue;
                string who = bits[0].ToUpperInvariant();
                bool opt = who.EndsWith("?");
                who = who.TrimEnd('?');
                if (who != "H" && who != "M" && who != "Х" && who != "М") continue;
                cur.Steps.Add(new Step { Munin = who == "M" || who == "М", Tag = bits[1], Optional = opt });
            }
            list.RemoveAll(s => s.Steps.Count == 0);
            _scenes = list;
            return list;
        }

        // ---------------------------------------------------------------- reasons

        // something happened that the ravens might talk about at the next quiet moment
        internal static void Push(string reason, string arg, float minutes)
        {
            Reasons[reason] = Time.time + minutes * 60f;
            if (arg != null) ReasonArgs[reason] = arg; else ReasonArgs.Remove(reason);
            if (_retryAt > Time.time + 20f) _retryAt = Time.time + 20f; // a fresh reason: try soon
        }

        // talked of already (with another player's ravens): no talk of the two about it later
        internal static void Forget(string reason) { Reasons.Remove(reason); ReasonArgs.Remove(reason); }

        // from the diary: the events worth a talk
        internal static void OnEvent(Ev type, string arg, int value)
        {
            switch (type)
            {
                case Ev.Kill: if (value > 0) { Push("boss", arg, 5f); CrossTalk.Boss(arg); } break; // with someone else near: their ravens may talk of it instead
                case Ev.Death: Push("death", null, 30f); break; // they are in the grave for now: the talk waits until they are back
                case Ev.NearDeath: Push("near_death", null, 4f); break;
                case Ev.Biome: Crossed(arg); break;
                case Ev.Travel: Walked(value); break;
                case Ev.Dungeon: Fork("dungeon", 10f); break;
                case Ev.Fall: Fork("fall", 5f); break;
                case Ev.Portal: _portalAt = Time.time; break; // a word once through (Through)
                case Ev.Fish: Fork("fish", 15f); break;
                case Ev.Chop: Work(true); break;
                case Ev.Mine: Work(false); break;
                case Ev.Encumbered: Heavy(); break;
                case Ev.Afk: _walked = 0f; break;
                case Ev.Build: _builtAt = Time.time; break; // a word about the building only while there is some
                // 2.1.0: the everyday moments; the ones that come every few seconds (a ride, a cart, a warm home, sowing) only after a while of it
                case Ev.Raid: Fork("raid", 30f); break;
                case Ev.Trade: Fork("trade", 20f); break;
                case Ev.Smelt: Fork("smelt", 20f); break;
                case Ev.Ferment: Fork("ferment", 20f); break;
                case Ev.Repair: Fork("repair", 15f); break;
                case Ev.Potion: Fork("potion", 15f); break;
                case Ev.Sit: Fork("sit", 20f); break;
                case Ev.Trophy: Fork("trophy", 15f); break;
                case Ev.TrophyHang: Fork("trophy_hang", 15f); break;
                case Ev.Plant: Ongoing("plant", 5, 180f, 20f); break;
                case Ev.Ride: Ongoing("ride", 12, 15f, 20f); break;
                case Ev.Cart: Ongoing("cart", 12, 15f, 20f); break;
                case Ev.Cozy: Ongoing("cozy", 36, 15f, 30f); break;
                // 2.4.0: a portal refusing the ore, a thing broken, a beast tamed, the body freezing, poisoned or on fire
                case Ev.PortalBlocked: Fork("portal_ore", 10f); break;
                case Ev.Broke: Fork("broke", 5f); break;
                case Ev.Tame: Fork("tame", 5f); break;
                case Ev.Freezing: Fork("freezing", 15f); break;
                case Ev.Poison: Fork("poisoned", 10f); break;
                case Ev.Burning: Fork("burning", 10f); break;
            }
        }

        // from one land into another: a talk now and then (not at every step along a border); the sea is the voyage's
        internal static float CrossMinutes = 10f;
        private static string _land;
        private static float _crossAt = -999f;
        private static void Crossed(string biome)
        {
            string from = _land;
            _land = biome;
            if (from == null || from == biome || biome == "Ocean" || from == "Ocean") return;
            if (Time.time - _crossAt < CrossMinutes * 60f) return;
            _crossAt = Time.time;
            Push("cross", from, 2f);
        }

        // the first time in a land (the ravens' memory says so): that is the new-land talk, not a crossing
        internal static void NewLand(string biome)
        {
            Reasons.Remove("cross"); ReasonArgs.Remove("cross");
            Push("biome", biome, 10f); // the first time in a land is rare: it waits up to 10 min for a quiet moment
        }

        // a moment worth a word, not too often (each kind keeps its own pause, in minutes)
        private static readonly Dictionary<string, float> MomentAt = new Dictionary<string, float>();
        private static void Moment(string reason, float waitMinutes, float pauseMinutes)
        {
            float last;
            if (MomentAt.TryGetValue(reason, out last) && Time.time - last < pauseMinutes * 60f) return;
            MomentAt[reason] = Time.time;
            Push(reason, null, waitMinutes);
        }

        // A moment (a fall, a load, a dungeon, a portal, a fish, wood, ore) decides at once, and only once:
        //   MomentChance — is there a word at all; then TalkChance — a talk of both (only with both on the shoulders) or one line.
        // Each kind keeps its pause (counted from the moment, whether a word came or not). The director's "not now" means
        // nothing this time: a word about a moment comes then or never.
        internal static float MomentChance = 0.5f, TalkChance = 0.5f;
        private static readonly string[] ForkedTags = { "dungeon", "fall", "heavy", "encumbered", "portal", "fish", "chop", "mine",
            "raid", "trade", "smelt", "ferment", "repair", "potion", "sit", "trophy", "trophy_hang", "plant", "ride", "cart", "cozy",
            "portal_ore", "broke", "tame", "freezing", "poisoned", "burning" };

        // something that goes on (a ride, a cart behind you, a warm home, sowing): a moment once it has gone on for `count`
        // events with no gap longer than `gapSeconds`, then the count starts again
        private static readonly Dictionary<string, int> OngoingCount = new Dictionary<string, int>();
        private static readonly Dictionary<string, float> OngoingAt = new Dictionary<string, float>();
        private static void Ongoing(string reason, int count, float gapSeconds, float pauseMinutes)
        {
            float now = Time.time, last;
            int n;
            if (!OngoingAt.TryGetValue(reason, out last) || now - last > gapSeconds) n = 0; else OngoingCount.TryGetValue(reason, out n);
            OngoingAt[reason] = now;
            if (++n >= count) { n = 0; Fork(reason, pauseMinutes); }
            OngoingCount[reason] = n;
        }
        internal static bool Forked(string tag) { return Array.IndexOf(ForkedTags, tag) >= 0; }

        private static void Fork(string reason, float pauseMinutes)
        {
            float last, now = Time.time;
            Player p = Player.m_localPlayer;
            if (p == null || p != _for || now < _readyAt) return; // just came into the world (spawned in a dungeon...): not a word
            if (MomentAt.TryGetValue(reason, out last) && now - last < pauseMinutes * 60f) return;
            MomentAt[reason] = now;
            if (UnityEngine.Random.value >= MomentChance) return; // not a word this time
            if (!Director.May(Director.Voice.Event)) return;
            if (UnityEngine.Random.value < TalkChance && !TalkBubble.Playing(p) && CanTalk(p) && Play(p, reason, Chattiness(reason))) return;
            Plugin.MomentLine(reason == "heavy" ? "encumbered" : reason); // one raven, one line
        }

        // a long go at the trees or the rocks: so many blows within a few minutes
        internal static int WorkBlows = 15;
        private static int _chop, _mine;
        private static float _chopAt, _mineAt;
        private static void Work(bool wood)
        {
            float now = Time.time;
            if (wood) { if (now - _chopAt > 180f) _chop = 0; _chopAt = now; if (++_chop >= WorkBlows) { _chop = 0; Fork("chop", 15f); } }
            else { if (now - _mineAt > 180f) _mine = 0; _mineAt = now; if (++_mine >= WorkBlows) { _mine = 0; Fork("mine", 15f); } }
        }

        // overloaded: said right then (the diary writes it every moment you are; a new load after half a minute free of it)
        private static float _heavyAt = -999f;
        private static void Heavy()
        {
            bool fresh = Time.time - _heavyAt > 30f;
            _heavyAt = Time.time;
            if (fresh) Fork("heavy", 10f);
        }

        // through a portal: the word comes when you stand on the other side a moment (not on the way, behind the loading)
        private static float _portalAt = -1f;
        private static void Through(Player p)
        {
            if (_portalAt < 0f) return;
            if (p.IsTeleporting()) { _portalAt = Time.time; return; }
            if (Time.time - _portalAt < 3f) return;
            _portalAt = -1f;
            Fork("portal", 10f);
        }

        // dawn and dusk, from the hour (looked at in the twice-a-second tick: only a number compared)
        private static float _dayWas = -1f;
        private static Player _for;      // the player the talks were last looked at for
        private static float _readyAt;   // no talk before this (just came into the world)
        private static void Hours()
        {
            if (EnvMan.instance == null) return;
            float f = EnvMan.instance.GetDayFraction();
            // only the hour going by counts: a jump (the world loading, the clock set from the server) is no dawn
            if (_dayWas >= 0f && f > _dayWas && f - _dayWas < 0.01f)
            {
                if (_dayWas < 0.25f && f >= 0.25f) Moment("dawn", 4f, 20f);
                if (_dayWas < 0.75f && f >= 0.75f) Moment("dusk", 4f, 20f);
            }
            _dayWas = f;
        }

        // A reason waits for a quiet moment (in real time; a sleep skips hours of the game's in a moment): when its turn
        // comes it is looked at again, and a talk about what is no longer so is left out.
        private static bool StillTrue(Player p, string reason)
        {
            string arg;
            ReasonArgs.TryGetValue(reason, out arg);
            switch (reason)
            {
                case "dawn": case "dusk": return HourFits(reason) && !Senses.Underground; // no sky down there
                case "biome": return arg == null || Here(p) == arg;                       // still in the new land
                case "cross": return arg == null || (Here(p) != arg && Here(p) != "Ocean"); // not gone back where it came from
                case "sail": return p.GetStandingOnShip() != null;                         // still aboard
                case "walk": return Time.time - _walkedAt < 60f && p.GetStandingOnShip() == null; // still on the road
                default: return true;
            }
        }

        private static string Here(Player p) { return p.GetCurrentBiome().ToString(); }

        // a dawn or dusk talk only while it still is that hour (a sleep skips the night in a moment, the wait is in real time)
        private static bool HourFits(string reason)
        {
            if (EnvMan.instance == null) return false;
            float f = EnvMan.instance.GetDayFraction();
            return reason == "dawn" ? f >= 0.22f && f < 0.34f : f >= 0.72f && f < 0.84f;
        }

        // a long walk: the metres add up while you keep going (a pause of half a minute starts it over)
        internal static float WalkMeters = 800f, SailMinutes = 4f;
        private static float _walked, _walkedAt, _sailed;
        private static void Walked(int meters)
        {
            if (Time.time - _walkedAt > 30f) _walked = 0f;
            _walkedAt = Time.time;
            _walked += meters;
            if (_walked < WalkMeters) return;
            _walked = 0f;
            Push("walk", null, 3f);
        }

        // a long voyage: on a moving ship, at the helm or not (counted in the twice-a-second tick)
        private static void Sailing(Player p)
        {
            if (!Senses.Sailing) { _sailed = Mathf.Max(0f, _sailed - 1f); return; } // the shared situation: on a moving ship
            _sailed += 0.5f;
            if (_sailed < SailMinutes * 60f) return;
            _sailed = 0f;
            Push("sail", null, 3f);
        }

        // one raven was fed: the other may be jealous (only if it was not fed today itself)
        internal static void Fed(bool munin, string food) { Push(munin ? "fed_munin" : "fed_hugin", food, 3f); }

        // ---------------------------------------------------------------- the twice-a-second sync: only time compared

        internal static void Tick(Player p)
        {
            if (p == null) return;
            float now = Time.time;
            if (p != _for) // just came into the world: a minute for the ravens to be there, and nothing left from before
            {
                _for = p;
                _readyAt = now + 60f;
                _dayWas = -1f;
                Reasons.Clear();
                ReasonArgs.Clear();
            }
            if (_replyAt > 0f && now >= _replyAt) { _replyAt = -1f; Reply(p, _replyMunin); }
            Sailing(p);
            Hours();
            Through(p);
            if (TalkBubble.Playing(p)) return;
            if (_nextIdle < 0f) _nextIdle = now + UnityEngine.Random.Range(EveryMin, EveryMax) * 60f;
            bool due = now >= _nextIdle || Reasons.Count > 0;
            if (!due || now < _retryAt) return;
            // now, and only now, is it looked at whether they can talk
            Drop(now);
            if (!Director.May(EventWaiting() ? Director.Voice.Event : Director.Voice.Talk)) { _retryAt = now + 20f; return; } // the director: not now
            if (!CanTalk(p)) { _retryAt = now + (Reasons.ContainsKey("biome") ? 10f : UnityEngine.Random.Range(30f, 60f)); return; } // a new land: looked at again soon
            bool idle = now >= _nextIdle;
            if (Start(p, idle)) _nextIdle = now + UnityEngine.Random.Range(EveryMin, EveryMax) * 60f;
            else if (idle) _nextIdle = now + 60f; // nothing fit: a minute later
            _retryAt = now + 20f;
        }

        private static void Drop(float now)
        {
            List<string> old = null;
            foreach (KeyValuePair<string, float> kv in Reasons) if (now > kv.Value) { if (old == null) old = new List<string>(); old.Add(kv.Key); }
            if (old != null) foreach (string k in old) { Reasons.Remove(k); ReasonArgs.Remove(k); }
        }

        // an event (a boss, a death, a new land...) is waiting: the director lets it come sooner than mere chatter
        private static readonly string[] EventReasons = { "boss", "death", "near_death", "biome" };
        private static readonly string[] WaitingOrder = { "boss", "death", "near_death", "biome", "fed_hugin", "fed_munin", "dawn", "dusk", "sail", "walk", "cross" };
        private static bool EventWaiting()
        {
            foreach (string r in EventReasons) if (Reasons.ContainsKey(r)) return true;
            return false;
        }

        // both on your shoulders, awake, yours, here; no fight near, nobody else talking
        private static bool CanTalk(Player p)
        {
            if (p != _for || Time.time < _readyAt) return false; // just came into the world: the ravens are not there yet
            if (p.IsDead() || p.IsSleeping()) return false;
            // the cheap flags first, the ravens' models last
            if (Cozy.Quieted || RavenSpeech.Has(p.gameObject)) return false;
            if (Senses.Ready ? Senses.Fight || Senses.Attacking || Senses.Swimming || Senses.Health01 < 0.35f // busy with their skin: a foe near (looked for once a second), an archer afar, a swing, the water, little life left
                : BaseAI.HaveEnemyInRange(p, p.transform.position, 25f)) return false;
            if (!Yours(p, RavenSeat.On(p, false)) || !Yours(p, RavenSeat.On(p, true))) return false;
            if (!Shoulder.Settled(p, false, SettleSeconds) || !Shoulder.Settled(p, true, SettleSeconds)) return false; // both really sitting, a moment already
            return true;
        }

        // worn, awake, here and yours (a stranger's raven, awake, is the hostile one)
        private static bool Yours(Player p, ItemDrop.ItemData r) { return r != null && Trust.Active(r) && !Trust.Away(r) && !Trust.Stranger(r, p); }

        // which reason now: a waiting event first, then the weather and the hour, then the two of them, then just talk
        private static bool Start(Player p, bool idle)
        {
            List<string> order = new List<string>();
            foreach (string r in WaitingOrder) if (Reasons.ContainsKey(r)) order.Add(r);
            if (idle)
            {
                bool rain, snow, storm, fog;
                Sky(out rain, out snow, out storm, out fog);
                if (storm) order.Add("storm");
                if (snow) order.Add("snow");
                if (rain) order.Add("rain");
                if (Friend(p) != null) order.Add("friend");
                if (fog) order.Add("fog");
                if (EnvMan.IsNight() && !Senses.Underground) order.Add("night"); // in a crypt it is dark anyway: no talk of the night sky
                int lh = Trust.Level(RavenSeat.On(p, false)), lm = Trust.Level(RavenSeat.On(p, true));
                if (lh - lm >= 2) order.Add("gap_h"); else if (lm - lh >= 2) order.Add("gap_m");
                order.Add("idle");
            }
            foreach (string reason in order)
            {
                if (reason == "fed_hugin" && Trust.FedToday(RavenSeat.On(p, true))) { Reasons.Remove(reason); continue; } // both fed: nothing to envy
                if (reason == "fed_munin" && Trust.FedToday(RavenSeat.On(p, false))) { Reasons.Remove(reason); continue; }
                if (!StillTrue(p, reason)) { Reasons.Remove(reason); ReasonArgs.Remove(reason); continue; } // it waited, and it is no longer so
                if (UnityEngine.Random.value > 0.6f && reason != order[order.Count - 1] && (reason == "rain" || reason == "fog" || reason == "night" || reason == "snow" || reason == "storm" || reason == "friend" || reason == "gap_h" || reason == "gap_m")) continue; // the weather is not always the talk
                if (Play(p, reason, Chattiness(reason))) { Reasons.Remove(reason); ReasonArgs.Remove(reason); return true; }
            }
            return false;
        }

        // ---------------------------------------------------------------- what is around (so the words fit it)

        // The weather as it falls on you: rain only out under the sky (under a roof they are not wet), not the Ashlands' ash
        // or cinders; a storm only with thunder (a snowstorm is snow); in a crypt or a cave no weather at all.
        private static void Sky(out bool rain, out bool snow, out bool storm, out bool fog)
        {
            Sky(Senses.Underground, Senses.Indoors, Senses.Biome, out rain, out snow, out storm, out fog);
        }

        // the same for any place (a perch: its own roof and land)
        internal static void Sky(bool underground, bool roofed, Heightmap.Biome biome, out bool rain, out bool snow, out bool storm, out bool fog)
        {
            rain = snow = storm = fog = false;
            if (EnvMan.instance == null || underground) return;
            EnvSetup env = EnvMan.instance.GetCurrentEnvironment();
            string n = env != null && env.m_name != null ? env.m_name : ""; // looked into as it is (Has: any case), no copy
            bool ash = biome == Heightmap.Biome.AshLands || Has(n, "ash") || Has(n, "cinder");
            snow = Has(n, "snow");
            storm = Has(n, "thunder") || (Has(n, "storm") && !snow);
            rain = !ash && !snow && !roofed && Has(n, "rain");
            fog = !ash && (Has(n, "mist") || Has(n, "fog"));
        }

        private static float _builtAt = -999f;

        // What is around, looked at once per talk (Look), not for every line tried: Patterns.Fits then only searches words.
        private static bool _sky, _night, _day, _sword, _built, _woods, _wet;
        private static readonly Func<string, bool> FitsFn = Fits; // one delegate, not a new one per talk

        private static void Look()
        {
            _sky = !Senses.Underground && !Senses.Indoors;
            _night = EnvMan.IsNight();
            float f = EnvMan.instance != null ? EnvMan.instance.GetDayFraction() : 0.5f;
            _day = f >= 0.2f && f <= 0.82f; // dawn and dusk still have their sun
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData w = p != null ? p.GetCurrentWeapon() : null;
            _sword = w != null && w.m_shared != null && w.m_shared.m_skillType == Skills.SkillType.Swords;
            _built = Time.time - _builtAt <= 600f;
            _woods = Woods();
            bool rain, snow, storm, fog;
            Sky(out rain, out snow, out storm, out fog);
            _wet = rain || storm;
        }

        // Patterns.Fits while a talk is put together: a line that speaks of what is not here is left for another time
        // (stars under a roof or by day, a sword with an axe in hand, the building with nothing built, trees at sea...)
        private static bool Fits(string line)
        {
            if (Has(line, Lang.TP("зорі")) || Has(line, Lang.TP("зірк"))) { if (!(_sky && _night)) return false; }
            if (Has(line, Lang.TP("сонц")) && !(_sky && _day)) return false;
            if (Has(line, Lang.TP("мечем")) && !_sword) return false;
            if (Has(line, Lang.TP("будує")) && !_built) return false;
            if ((Has(line, Lang.TP("дерев")) || Word(line, Lang.TP("ліс"))) && !_woods) return false;
            if (Has(line, Lang.TP("дощ пішов")) && _wet) return false; // wishing for rain in the rain
            return true;
        }

        private static bool Has(string line, string word) { return line.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0; }

        // trees around: a land that has them, out in the open, not aboard
        private static bool Woods()
        {
            if (Senses.Underground || Senses.OnShip) return false;
            switch (Senses.Biome)
            {
                case Heightmap.Biome.Meadows: case Heightmap.Biome.BlackForest: case Heightmap.Biome.Swamp:
                case Heightmap.Biome.Mountain: case Heightmap.Biome.Plains: case Heightmap.Biome.Mistlands: return true;
                default: return false;
            }
        }

        // the word starts here (ліс, лісі, Лісом — not «наліс»)
        private static bool Word(string l, string stem)
        {
            for (int i = l.IndexOf(stem, StringComparison.OrdinalIgnoreCase); i >= 0; i = l.IndexOf(stem, i + 1, StringComparison.OrdinalIgnoreCase))
                if (i == 0 || !char.IsLetter(l[i - 1])) return true;
            return false;
        }

        // another player within 20 m (only looked for when the timer wakes)
        private static Player Friend(Player p)
        {
            Player best = null;
            float bd = 20f;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == p || o.IsDead() || Shadow.Flagged(o)) continue; // one hidden in the shadow is not named
                float d = Vector3.Distance(o.transform.position, p.transform.position);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        // how likely each "?" step goes on: they talk more by the fire, less after a fight
        private static float Chattiness(string reason)
        {
            switch (reason)
            {
                case "rest": return 0.75f;
                case "idle": case "night": case "rain": case "fog": return 0.55f;
                case "gap_h": case "gap_m": case "fed_hugin": case "fed_munin": return 0.5f;
                case "walk": case "sail": return 0.6f;
                case "cross": return 0.5f;
                case "heavy": case "fall": case "portal": return 0.4f;
                case "dungeon": case "fish": case "chop": case "mine": case "snow": case "storm": case "dawn": case "dusk": case "friend": return 0.5f;
                case "raid": case "trade": case "ride": case "smelt": case "ferment": case "repair": case "potion": case "sit": case "cozy":
                case "trophy": case "trophy_hang": case "cart": case "plant": return 0.5f;
                default: return 0.35f;
            }
        }

        // the fire: sometimes a talk instead of a story (Cozy asks)
        internal static bool Fireside(Player p)
        {
            if (UnityEngine.Random.value > FiresideChance || TalkBubble.Playing(p) || !CanTalk(p)) return false;
            return Play(p, "rest", Chattiness("rest"));
        }

        // a raven's thought: the other may answer it a moment later
        internal static void AfterThought(Player p, bool munin, string line)
        {
            if (p == null || UnityEngine.Random.value > ReactChance) return;
            ItemDrop.ItemData other = RavenSeat.On(p, !munin);
            if (other == null || !Trust.Active(other) || Trust.Away(other) || Trust.Hostile(other)) return;
            if (!Shoulder.Settled(p, !munin, 1f)) return; // the other one is out (a strike, an outing...): it does not answer from the air
            _replyMunin = !munin;
            _replyAt = Time.time + Mathf.Clamp(RavenSpeech.ReadSeconds(line) * 0.6f, 2.5f, 7f);
        }

        private static void Reply(Player p, bool munin)
        {
            if (Plugin.Instance == null || RavenSeat.On(p, munin) == null || !Shoulder.Settled(p, munin, 0f)) return; // flown off since
            string line;
            Look();
            Patterns.Fits = FitsFn;
            try { line = Plugin.Instance.ComposeTagged(p, munin, munin ? "dlg_react_m" : "dlg_react_h"); }
            finally { Patterns.Fits = null; }
            if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(p, munin, line);
        }

        // the console (raven talk [reason]): a talk now, whatever the timer says; both ravens must sit on you
        internal static string Force(Player p, string reason)
        {
            if (p == null) return Lang.T("Нема гравця.");
            if (reason == "react") { _replyMunin = UnityEngine.Random.value < 0.5f; _replyAt = Time.time + 0.5f; return Lang.F("Відповідь на думку від {0}", Plugin.RavenName(_replyMunin)); }
            if (RavenSeat.On(p, false) == null || RavenSeat.On(p, true) == null) return Lang.T("Для розмови потрібні обидва ворони на плечах.");
            if (TalkBubble.Playing(p)) return Lang.T("Вони вже розмовляють.");
            if (reason == "any") // any reason at all, in a random order, till one has a scene that fits now
            {
                List<string> whens = new List<string>();
                foreach (Scene s in Scenes()) if (!whens.Contains(s.When)) whens.Add(s.When);
                while (whens.Count > 0)
                {
                    string w = whens[UnityEngine.Random.Range(0, whens.Count)];
                    whens.Remove(w);
                    if (Play(p, w, Chattiness(w))) return Lang.F("Розмова: {0}", w);
                }
                return Lang.T("Жодна сцена зараз не підійшла.");
            }
            if (Play(p, reason, Chattiness(reason))) return Lang.F("Розмова: {0}", reason);
            return Lang.F("Нема сцени для «{0}». Приводи: idle, rest, walk, sail, cross, dungeon, fall, heavy, portal, fish, chop, mine, snow, storm, dawn, dusk, friend, rain, fog, night, boss, near_death, death, biome, fed_hugin, fed_munin, gap_h, gap_m, а також react (відповідь на думку) і any (будь-яка).", reason);
        }

        // ---------------------------------------------------------------- a scene

        private static bool Play(Player p, string reason, float chatty)
        {
            if (Plugin.Instance == null) return false;
            Look(); // what is around, once for all the scenes tried
            List<Scene> fit = new List<Scene>();
            List<Scene> all = Scenes();
            List<string> recent = Recent(p);
            foreach (Scene s in all) if (s.When == reason && !recent.Contains(s.Id)) fit.Add(s);
            if (fit.Count == 0) foreach (Scene s in all) if (s.When == reason) fit.Add(s); // all told lately: any of them
            while (fit.Count > 0)
            {
                Scene s = Weighted(fit);
                fit.Remove(s);
                List<TalkBubble.Line> lines = null;
                for (int tries = 0; tries < 3 && lines == null; tries++) lines = Make(p, s, reason, chatty); // an answer that does not fit what was said is never taken: put together again
                if (lines == null) continue;
                Remember(p, s.Id);
                TalkBubble.Play(p, lines, true);
                Send(p, lines);
                float total = 0f;
                foreach (TalkBubble.Line l in lines) total += TalkBubble.Pace(l.Text);
                Director.Spoke(reason == "boss" || reason == "death" || reason == "near_death" || reason == "biome" || reason == "heavy" || reason == "fall" || reason == "portal" || reason == "dungeon" ? Director.Voice.Event
                    : reason == "rest" ? Director.Voice.Fireside : Director.Voice.Talk, total + 2f); // counted; the others wait while it is said
                return true;
            }
            return false;
        }

        private static Scene Weighted(List<Scene> list)
        {
            int sum = 0;
            foreach (Scene s in list) sum += s.Weight;
            int r = UnityEngine.Random.Range(0, sum);
            foreach (Scene s in list) { r -= s.Weight; if (r < 0) return s; }
            return list[0];
        }

        private static List<TalkBubble.Line> Make(Player p, Scene s, string reason, float chatty)
        {
            List<TalkBubble.Line> lines = new List<TalkBubble.Line>();
            string arg;
            ReasonArgs.TryGetValue(reason, out arg);
            Composer.Extra.Clear();
            Patterns.Fits = FitsFn; // (Play has looked round)
            try
            {
                if (reason == "boss" && !string.IsNullOrEmpty(arg) && _dir != null) Composer.CreatureForms(_dir, Composer.Extra, "boss", arg); // {boss} {boss_gen} {boss_ins}
                if (reason == "fed_hugin" || reason == "fed_munin") Composer.Extra["food"] = string.IsNullOrEmpty(arg) ? Lang.T("Їжа") : arg;
                if (reason == "cross") Composer.BiomeForms(Composer.Extra, "from", arg); // {from} {from_in}: where you came from
                if (reason == "friend") { Player f = Friend(p); if (f == null) return null; Composer.Extra["friend"] = f.GetPlayerName(); }
                string thread = null; // what the talk is about: set by the first line with a ^mark, followed by the rest
                foreach (Step st in s.Steps)
                {
                    if (lines.Count >= MaxLines) break;
                    if (st.Optional && UnityEngine.Random.value > chatty && !Asked(lines)) break; // the first one left out ends the talk (a question is always answered)
                    Patterns.ThreadWant = thread;
                    string line = null;
                    for (int tries = 0; tries < 4; tries++) // not the same words again in one talk ("я сам тебе доб'ю" twice)
                    {
                        line = Plugin.Instance.ComposeTagged(p, st.Munin, st.Tag);
                        if (string.IsNullOrEmpty(line) || !Echoes(line, lines)) break;
                    }
                    if (string.IsNullOrEmpty(line)) { if (st.Optional) break; return null; }
                    thread = Follow(thread);
                    lines.Add(new TalkBubble.Line { Munin = st.Munin, Text = Grammar.Fix(line, p) });
                }
            }
            finally { Composer.Extra.Clear(); Patterns.ThreadWant = null; Patterns.Fits = null; }
            return lines.Count >= 2 ? lines : null;
        }

        // ---------------------------------------------------------------- on a perch (PerchSpeech)

        // the reasons that fit two ravens sitting on a perch (no falls, portals, fights, roads...)
        internal static readonly string[] PerchReasons = { "perch", "idle", "rest", "rain", "fog", "snow", "storm", "night", "dawn", "dusk", "friend", "gap_h", "gap_m" };

        // A talk of the two on a perch for this reason, or null: each raven in the tone of its own trust, the owner's
        // name and forms from the perch's record ({friend}: someone else near).
        internal static List<TalkBubble.Line> PerchTalk(string reason, int levelH, int levelM, string name, bool female, string friend, List<string> recent, int voiceH = -1, int voiceM = -1)
        {
            if (Plugin.Instance == null || Array.IndexOf(PerchReasons, reason) < 0) return null;
            if (reason == "friend" && string.IsNullOrEmpty(friend)) return null;
            string dir = Plugin.Instance.PhraseDir;
            List<Scene> fit = new List<Scene>();
            foreach (Scene s in Scenes()) if (s.When == reason && (recent == null || !recent.Contains(s.Id))) fit.Add(s);
            if (fit.Count == 0) foreach (Scene s in Scenes()) if (s.When == reason) fit.Add(s);
            Dictionary<string, string> extra = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(friend)) extra["friend"] = friend;
            float chatty = Chattiness(reason == "perch" ? "idle" : reason);
            while (fit.Count > 0)
            {
                Scene s = Weighted(fit);
                fit.Remove(s);
                List<TalkBubble.Line> lines = new List<TalkBubble.Line>();
                string thread = null;
                bool ok = true;
                try
                {
                    foreach (Step st in s.Steps)
                    {
                        if (lines.Count >= MaxLines) break;
                        if (st.Optional && UnityEngine.Random.value > chatty && !Asked(lines)) break;
                        Patterns.ThreadWant = thread;
                        string line = null;
                        for (int tries = 0; tries < 4; tries++)
                        {
                            line = Composer.ComposePerch(dir, st.Munin, st.Tag, st.Munin ? levelM : levelH, name, female, extra, st.Munin ? voiceM : voiceH);
                            if (string.IsNullOrEmpty(line) || !Echoes(line, lines)) break;
                        }
                        if (string.IsNullOrEmpty(line)) { if (st.Optional) break; ok = false; break; }
                        thread = Follow(thread);
                        lines.Add(new TalkBubble.Line { Munin = st.Munin, Text = line });
                    }
                }
                finally { Patterns.ThreadWant = null; }
                if (!ok || lines.Count < 2) continue;
                if (recent != null) { recent.Remove(s.Id); recent.Add(s.Id); while (recent.Count > 6) recent.RemoveAt(0); }
                return lines;
            }
            return null;
        }

        // what the next line answers: the first marked line sets the topic, a chain (^this>next) moves it on to what was
        // just said, so the last word answers the answer, not only the first topic
        private static string Follow(string thread)
        {
            if (Patterns.ThreadNext != null) return Patterns.ThreadNext;
            return thread ?? Patterns.ThreadGot;
        }

        // the last line asked something: the next one is not left out
        internal static bool Asked(List<TalkBubble.Line> lines)
        {
            return lines.Count > 0 && Asks(lines[lines.Count - 1].Text);
        }

        // a line that ends on a question (its last sentence asks); an echo inside it («Сила? Ну, сила — то нормас.») asks nothing
        internal static bool Asks(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            int end = line.LastIndexOfAny(new[] { '.', '!', '?', '…' });
            return end >= 0 && line[end] == '?';
        }

        // three words in a row that an earlier line of the talk already said
        private static bool Echoes(string line, List<TalkBubble.Line> said)
        {
            if (said.Count == 0) return false;
            string[] w = Words(line);
            for (int i = 0; i + 2 < w.Length; i++)
            {
                string three = " " + w[i] + " " + w[i + 1] + " " + w[i + 2] + " ";
                foreach (TalkBubble.Line l in said) if ((" " + string.Join(" ", Words(l.Text)) + " ").Contains(three)) return true;
            }
            return false;
        }

        private static string[] Words(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant()) sb.Append(char.IsLetter(c) || c == '\'' || c == '\u2019' ? c : ' ');
            return sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // the talks told lately (the last 8), kept in the character so they do not come again soon
        private static List<string> Recent(Player p)
        {
            string raw;
            List<string> list = new List<string>();
            if (p.m_customData.TryGetValue(RecentKey, out raw)) list.AddRange(raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            return list;
        }

        private static void Remember(Player p, string id)
        {
            List<string> list = Recent(p);
            list.Remove(id);
            list.Add(id);
            while (list.Count > 8) list.RemoveAt(0);
            p.m_customData[RecentKey] = string.Join(",", list.ToArray());
        }

        // ---------------------------------------------------------------- the others nearby see it too (sent once per talk)

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcTalk, new Action<long, ZPackage>(OnTalk));
        }

        private static void Send(Player p, List<TalkBubble.Line> lines)
        {
            if (ZRoutedRpc.instance == null || ZNet.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetZDOID());
            pkg.Write(p.transform.position);
            pkg.Write(lines.Count);
            foreach (TalkBubble.Line l in lines) { pkg.Write(l.Munin); pkg.Write(l.Text); }
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcTalk, pkg);
        }

        private static void OnTalk(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            if (me == null || sender == ZDOMan.GetSessionID()) return;
            ZDOID id = pkg.ReadZDOID();
            Vector3 pos = pkg.ReadVector3();
            int n = Mathf.Min(pkg.ReadInt(), MaxLines);
            List<TalkBubble.Line> lines = new List<TalkBubble.Line>();
            for (int i = 0; i < n; i++) lines.Add(new TalkBubble.Line { Munin = pkg.ReadBool(), Text = pkg.ReadString() });
            if (Vector3.Distance(me.transform.position, pos) > 100f) return; // far off: nothing; nearer, it runs and shows as you come within 30 m
            foreach (Player other in Player.GetAllPlayers())
                if (other != null && other != me && other.GetZDOID() == id) { if (!Shadow.Flagged(other)) TalkBubble.Play(other, lines, false); break; }
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }
    }
}
#endif
