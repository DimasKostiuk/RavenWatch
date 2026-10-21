#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The ravens' memory: what a raven saw from its owner's shoulder, kept in the raven itself, so each raven has
    // its own story. Not a log of everything: a few shelves of fixed size.
    //   firsts  - the first death; each boss, biome, dungeon, home, metal, tamed beast and friend (once each)
    //   records - the closest call, the biggest slaughter, the longest voyage, the highest fall, the longest wait in
    //             the grave, the worst day (a new record takes the old one's place)
    //   grudges - who killed you most (the 8 worst)
    //   deaths  - each with its place (12); a death on an old spot adds to that one; the weakest is forgotten first
    //   recent  - small lively moments (10): a scrape, a hard fight won, prey that got away, a run, a starred kill,
    //             a first kill of a kind; they fade within days, so fresh ones keep coming
    // Only an awake raven on its owner's shoulder remembers, and only its owner hears it remember.
    // It speaks of the past when nothing is happening, picking what fits the moment: the place, what you do, who is near.
    internal static class Memory
    {
        private const string Key = "RavenWatch_memory";

        internal sealed class Mem
        {
            public string Kind = "", Who = "", Biome = "", Extra = "";
            public int X, Z, Day, N, W = 1, Told, ToldDay = -9999;
        }

        private static readonly Dictionary<string, int> Caps = new Dictionary<string, int>
        {
            { "death", 12 }, { "recent", 10 }, { "grudge", 8 }, { "friend", 6 }, { "tame", 5 }, { "home", 4 }, { "metal", 8 }, { "boss", 8 }, { "biome", 10 }, { "dungeon", 10 },
        };

        // the metals worth remembering, as the raven names them (what you held: accusative)
        internal static readonly Dictionary<string, string> Metals = new Dictionary<string, string>
        {
            { "Copper", Lang.K("мідь") }, { "Tin", Lang.K("олово") }, { "Bronze", Lang.K("бронзу") }, { "Iron", Lang.K("залізо") }, { "Silver", Lang.K("срібло") },
            { "BlackMetal", Lang.K("чорний метал") }, { "Flametal", Lang.K("полум'яний метал") }, { "FlametalNew", Lang.K("полум'яний метал") },
        };

        // ---------------------------------------------------------------- kept in the raven

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace("|", "").Replace(";", "").Replace("\n", " "); }

        internal static List<Mem> Load(ItemDrop.ItemData item)
        {
            List<Mem> list = new List<Mem>();
            string raw;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(Key, out raw) || string.IsNullOrEmpty(raw)) return list;
            foreach (string row in raw.Split(';'))
            {
                string[] f = row.Split('|');
                if (f.Length < 11) continue;
                Mem m = new Mem();
                m.Kind = f[0]; m.Who = f[1]; m.Biome = f[2];
                int.TryParse(f[3], out m.X); int.TryParse(f[4], out m.Z); int.TryParse(f[5], out m.Day); int.TryParse(f[6], out m.N);
                int.TryParse(f[7], out m.W); int.TryParse(f[8], out m.Told); int.TryParse(f[9], out m.ToldDay);
                m.Extra = f[10];
                if (m.Kind.Length > 0) list.Add(m);
            }
            return list;
        }

        internal static void Save(ItemDrop.ItemData item, List<Mem> list)
        {
            if (item == null) return;
            Trim(list);
            List<string> rows = new List<string>();
            foreach (Mem m in list)
                rows.Add(string.Join("|", new string[] { Clean(m.Kind), Clean(m.Who), Clean(m.Biome), m.X.ToString(), m.Z.ToString(), m.Day.ToString(),
                    m.N.ToString(), m.W.ToString(), m.Told.ToString(), m.ToldDay.ToString(), Clean(m.Extra) }));
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[Key] = string.Join(";", rows.ToArray());
        }

        // a full shelf forgets: deaths by how much they meant and how long ago; grudges the mildest; the other shelves keep
        // their earliest (the first friends, beasts, homes), so a newcomer past the limit is not kept
        private static void Trim(List<Mem> list)
        {
            int today = Day();
            foreach (KeyValuePair<string, int> cap in Caps)
            {
                List<Mem> shelf = list.FindAll(m => m.Kind == cap.Key);
                while (shelf.Count > cap.Value)
                {
                    Mem weakest = shelf[0];
                    foreach (Mem m in shelf)
                        if (cap.Key == "grudge" ? m.N < weakest.N : Strength(m, today) < Strength(weakest, today)) weakest = m;
                    shelf.Remove(weakest);
                    list.Remove(weakest);
                }
            }
        }

        private static float Strength(Mem m, int today)
        {
            float age = Mathf.Max(0, today - m.Day);
            if (m.Kind == "recent") return m.W / (1f + age / 2f) / (1f + m.Told); // small things fade fast, the more so once told
            if (m.Kind != "death") return -m.Day; // the newest goes
            return m.W * m.N / (1f + age / 30f) / (1f + m.Told * 0.5f);
        }

        private static Mem Find(List<Mem> list, string kind) { return list.Find(m => m.Kind == kind); }
        private static Mem Find(List<Mem> list, string kind, string who) { return list.Find(m => m.Kind == kind && m.Who == who); }

        private static Mem Add(List<Mem> list, string kind, string who, Player p, int n, int w)
        {
            Mem m = new Mem();
            m.Kind = kind; m.Who = who ?? ""; m.N = n; m.W = w; m.Day = Day();
            Place(m, p);
            list.Add(m);
            return m;
        }

        private static void Place(Mem m, Player p)
        {
            if (p == null) return;
            m.X = Mathf.RoundToInt(p.transform.position.x);
            m.Z = Mathf.RoundToInt(p.transform.position.z);
            m.Biome = p.GetCurrentBiome().ToString();
        }

        internal static int Day() { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; }

        private static float Distance(Mem m, Player p)
        {
            if (p == null) return float.MaxValue;
            Vector3 at = p.transform.position;
            return new Vector2(at.x - m.X, at.z - m.Z).magnitude;
        }

        // ---------------------------------------------------------------- who saw it

        // awake, its owner's, on the shoulder now
        internal static bool Remembers(ItemDrop.ItemData item, Player p)
        {
            return item != null && p != null && Trust.Active(item) && !Trust.Stranger(item, p) && !Trust.Away(item);
        }

        // raven forget: everything a raven keeps about a land (the land itself, its dungeons, and what happened there)
        internal static int Forget(ItemDrop.ItemData item, string biome)
        {
            List<Mem> list = Load(item);
            int n = list.RemoveAll(m => string.Equals(m.Biome, biome, StringComparison.OrdinalIgnoreCase)
                || ((m.Kind == "biome" || m.Kind == "dungeon") && string.Equals(m.Who, biome, StringComparison.OrdinalIgnoreCase)));
            if (n > 0) Save(item, list);
            return n;
        }

        private static List<ItemDrop.ItemData> Witnesses(Player p)
        {
            List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>();
            if (p == null || p != Player.m_localPlayer || !RavenItem.Enabled) return list;
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            if (Remembers(h, p)) list.Add(h);
            if (Remembers(m, p)) list.Add(m);
            return list;
        }

        // ---------------------------------------------------------------- what happens

        private static float _lastDeathAt = -1f, _lowHp = float.MaxValue, _lowAt;
        private static string _newBiome = "";
        private static float _newBiomeAt = -999f;
        private static readonly List<KeyValuePair<float, string>> Kills = new List<KeyValuePair<float, string>>();
        private static float _fallHeight, _fallAt, _tombAt = -1f, _tombMinutes;
        private static float _voyage, _offShipAt;
        private static Vector3 _shipLast;
        private static bool _onShip;
        private static float _lastSample = -1f;
        private static readonly Dictionary<string, float> With = new Dictionary<string, float>();
        private static readonly HashSet<string> FriendsDone = new HashSet<string>();

        internal static void OnDeath(Player p, string who, bool creature, bool boss)
        {
            _lastDeathAt = Time.time;
            _lowHp = float.MaxValue;
            _fallHeight = 0f; // a fall that killed is a death, not a fall survived
            int day = Day();
            string biome = p != null ? p.GetCurrentBiome().ToString() : "";
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                string extra = creature ? (boss ? "b" : "c") : "x";
                if (Find(list, "fdeath") == null) Add(list, "fdeath", who, p, 1, 4).Extra = extra;
                Mem spot = null;
                foreach (Mem m in list) if (m.Kind == "death" && Distance(m, p) < 50f) { spot = m; break; }
                if (spot != null) { spot.N++; spot.Day = day; spot.Who = who; spot.Extra = extra; spot.W = Mathf.Min(9, spot.W + 1); spot.Told = 0; }
                else Add(list, "death", who, p, 1, boss ? 5 : creature ? 3 : 2).Extra = extra;
                if (creature)
                {
                    Mem g = Find(list, "grudge", who);
                    if (g == null) Add(list, "grudge", who, p, 1, 3); else { g.N++; g.Day = day; }
                }
                // the first day in a land, and dead there at once
                if (_newBiome == biome && Time.time - _newBiomeAt < 600f) { Mem b = Find(list, "biome", biome); if (b != null) { b.N = 1; b.W = 4; } }
                Mem t = Find(list, "today");
                if (t == null) t = Add(list, "today", "", p, 0, 0);
                if (t.Day != day) { t.Day = day; t.N = 0; }
                t.N++;
                Mem bad = Find(list, "badday");
                if (t.N >= 3 && (bad == null || t.N > bad.N || bad.Day == day))
                {
                    if (bad == null) bad = Add(list, "badday", "", p, t.N, 5);
                    bad.N = Mathf.Max(bad.N, t.N); bad.Day = day; Place(bad, p); bad.Told = 0;
                }
                Save(item, list);
            }
        }

        // health dipped: the lowest point of this scrape
        internal static void Low(float hp)
        {
            if (Time.time - _lowAt > 30f) _lowHp = float.MaxValue;
            _lowAt = Time.time;
            _lowHp = Mathf.Min(_lowHp, hp);
        }

        internal static void OnNearDeath(Player p, string by)
        {
            if (_lowHp == float.MaxValue) return;
            int hp = Mathf.Max(1, Mathf.CeilToInt(_lowHp));
            _lowHp = float.MaxValue;
            Recent(p, "near", by, hp, 3);
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                Mem r = Find(list, "near");
                if (r != null && hp >= r.N) continue;
                if (r == null) r = Add(list, "near", by, p, hp, 4);
                r.N = hp; r.Who = by ?? ""; r.Day = Day(); Place(r, p); r.Told = 0;
                Save(item, list);
            }
        }

        internal static void OnBiome(Player p, string biome)
        {
            if (string.IsNullOrEmpty(biome) || biome == "None") return;
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                if (Find(list, "biome", biome) != null) continue;
                Add(list, "biome", biome, p, 0, 2);
                Save(item, list);
                _newBiome = biome; _newBiomeAt = Time.time;
                RavenTalk.NewLand(biome); // the first time here: a talk about the new land
            }
        }

        internal static void OnDungeon(Player p)
        {
            if (p == null) return;
            string biome = p.GetCurrentBiome().ToString();
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                if (Find(list, "dungeon", biome) != null) continue;
                Add(list, "dungeon", biome, p, 0, 2);
                Save(item, list);
            }
        }

        // a small moment for the recent shelf; one of a kind about the same creature within a minute is enough
        private static string _lastRecent = "";
        private static float _lastRecentAt = -999f;

        internal static void Recent(Player p, string what, string who, int n, int w)
        {
            if (what + who == _lastRecent && Time.time - _lastRecentAt < 60f) return;
            _lastRecent = what + who; _lastRecentAt = Time.time;
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                Add(list, "recent", who, p, n, w).Extra = what;
                Save(item, list);
            }
        }

        // the damage you took lately: a fight that cost half your health and was won is worth a story
        private static readonly List<KeyValuePair<float, float>> Hurts = new List<KeyValuePair<float, float>>();
        private static float _hardAt = -999f;
        private static string _firstKill = "";
        private static float _firstKillAt;

        internal static void Hurt(float damage)
        {
            if (damage <= 0f) return;
            Hurts.Add(new KeyValuePair<float, float>(Time.time, damage));
            Hurts.RemoveAll(h => Time.time - h.Key > 45f);
        }

        // eight and more in a minute is a slaughter worth keeping
        internal static void OnKill(string token, int stars)
        {
            Player me = Player.m_localPlayer;
            if (me != null && !me.IsDead())
            {
                float taken = 0f;
                foreach (KeyValuePair<float, float> h in Hurts) if (Time.time - h.Key <= 45f) taken += h.Value;
                if (taken >= me.GetMaxHealth() * 0.5f && Time.time - _hardAt > 120f)
                {
                    _hardAt = Time.time;
                    Hurts.Clear();
                    Recent(me, "hard", token, Mathf.RoundToInt(taken), 3);
                }
                if (stars >= 2) Recent(me, "star", token, stars, 3);
                _firstKill = token ?? ""; _firstKillAt = Time.time; // the profile counts it a moment later
            }
            float now = Time.time;
            Kills.Add(new KeyValuePair<float, string>(now, token ?? ""));
            Kills.RemoveAll(k => now - k.Key > 60f);
            if (Kills.Count < 8) return;
            Dictionary<string, int> by = new Dictionary<string, int>();
            string top = ""; int best = 0;
            foreach (KeyValuePair<float, string> k in Kills)
            {
                int n; by.TryGetValue(k.Value, out n); by[k.Value] = ++n;
                if (n > best) { best = n; top = k.Value; }
            }
            Player p = Player.m_localPlayer;
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                Mem r = Find(list, "slaughter");
                if (r != null && Kills.Count <= r.N) continue;
                if (r == null) r = Add(list, "slaughter", top, p, Kills.Count, 4);
                r.N = Kills.Count; r.Who = top; r.Day = Day(); Place(r, p); r.Told = 0;
                Save(item, list);
            }
        }

        internal static void OnBoss(Player p, string prefab)
        {
            if (p == null) return;
            List<string> friends = new List<string>();
            foreach (Player other in Player.GetAllPlayers())
                if (other != null && other != p && friends.Count < 3 && Vector3.Distance(other.transform.position, p.transform.position) < 60f) friends.Add(other.GetPlayerName());
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                Mem b = Find(list, "boss", prefab);
                if (b != null) { b.N++; Save(item, list); continue; }
                Add(list, "boss", prefab, p, 1, 6).Extra = string.Join(", ", friends.ToArray());
                Save(item, list);
            }
        }

        internal static void OnFall(float height)
        {
            if (height < 8f) return;
            _fallHeight = Mathf.Max(_fallHeight, height);
            _fallAt = Time.time;
        }

        internal static void OnTame(string token)
        {
            Player p = Player.m_localPlayer;
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                if (Find(list, "tame", token) != null) continue;
                Add(list, "tame", token, p, 0, 3);
                Save(item, list);
            }
        }

        // a bed claimed: the first home, and a move far from every home before
        internal static void OnHome(Player p)
        {
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                bool known = false;
                foreach (Mem m in list) if (m.Kind == "home" && Distance(m, p) < 300f) { known = true; break; }
                if (known) continue;
                Add(list, "home", "", p, list.FindAll(m => m.Kind == "home").Count + 1, 3);
                Save(item, list);
            }
        }

        // the own grave opened: how long the raven lay in it (the items are back a moment later)
        internal static void OnTomb()
        {
            if (_lastDeathAt < 0f) return;
            _tombMinutes = (Time.time - _lastDeathAt) / 60f;
            _tombAt = Time.time + 2f;
        }

        internal static void OnMetal(Player p, string prefab)
        {
            if (!Metals.ContainsKey(prefab)) return;
            string name = Metals[prefab];
            foreach (ItemDrop.ItemData item in Witnesses(p))
            {
                List<Mem> list = Load(item);
                if (Find(list, "metal", name) != null) continue;
                Add(list, "metal", name, p, 0, 3);
                Save(item, list);
            }
        }

        // from the diary's sampling, every few seconds: friends beside you, the voyage, a fall survived, the grave
        internal static void Sample(Player p)
        {
            if (p == null || p != Player.m_localPlayer) return;
            float dt = _lastSample < 0f ? 0f : Mathf.Min(10f, Time.time - _lastSample);
            _lastSample = Time.time;
            if (p.IsDead()) return;

            if (_firstKill.Length > 0 && Time.time - _firstKillAt > 1f)
            {
                if (Stats.Kills(_firstKill) == 1) Recent(p, "newkill", _firstKill, 0, 2);
                _firstKill = "";
            }

            foreach (Player other in Player.GetAllPlayers())
            {
                if (other == null || other == p || Vector3.Distance(other.transform.position, p.transform.position) > 30f) continue;
                string name = other.GetPlayerName();
                if (string.IsNullOrEmpty(name) || FriendsDone.Contains(name)) continue;
                float t; With.TryGetValue(name, out t);
                With[name] = t += dt;
                if (t < 600f) continue;
                List<ItemDrop.ItemData> saw = Witnesses(p);
                if (saw.Count == 0) continue; // no raven on the shoulder now: it is noted when one is
                FriendsDone.Add(name);
                foreach (ItemDrop.ItemData item in saw)
                {
                    List<Mem> list = Load(item);
                    if (Find(list, "friend", name) != null) continue;
                    Add(list, "friend", name, p, 0, 3);
                    Save(item, list);
                }
            }

            bool onShip = ReferenceEquals(p, Player.m_localPlayer) && Senses.Ready ? Senses.OnShip : p.GetControlledShip() != null || p.GetStandingOnShip() != null;
            Vector3 pos = p.transform.position;
            if (onShip)
            {
                if (_onShip) { float d = Vector3.Distance(pos, _shipLast); if (d < 100f) _voyage += d; }
                _shipLast = pos; _onShip = true; _offShipAt = Time.time;
            }
            else if (_onShip && Time.time - _offShipAt > 30f)
            {
                _onShip = false;
                int meters = Mathf.RoundToInt(_voyage);
                _voyage = 0f;
                if (meters >= 1000)
                    foreach (ItemDrop.ItemData item in Witnesses(p))
                    {
                        List<Mem> list = Load(item);
                        Mem r = Find(list, "voyage");
                        if (r != null && meters <= r.N) continue;
                        if (r == null) r = Add(list, "voyage", "", p, meters, 3);
                        r.N = meters; r.Day = Day(); Place(r, p); r.Told = 0;
                        Save(item, list);
                    }
            }

            if (_fallHeight > 0f && Time.time - _fallAt > 2f)
            {
                int h = Mathf.RoundToInt(_fallHeight);
                _fallHeight = 0f;
                foreach (ItemDrop.ItemData item in Witnesses(p))
                {
                    List<Mem> list = Load(item);
                    Mem r = Find(list, "fall");
                    if (r != null && h <= r.N) continue;
                    if (r == null) r = Add(list, "fall", "", p, h, 3);
                    r.N = h; r.Day = Day(); Place(r, p); r.Told = 0;
                    Save(item, list);
                }
            }

            if (_tombAt > 0f && Time.time >= _tombAt)
            {
                _tombAt = -1f;
                int minutes = Mathf.RoundToInt(_tombMinutes);
                if (minutes >= 3)
                    // the raven lay in the grave, not on the shoulder: every own awake raven that came back with you
                    foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems())
                    {
                        bool munin;
                        if (!RavenItem.IsRaven(item, out munin) || Loyal.TakeStayed(item) || !Trust.Active(item) || Trust.Stranger(item, p)) continue; // stayed on the shoulder: it was not in there
                        List<Mem> list = Load(item);
                        Mem r = Find(list, "tomb");
                        if (r != null && minutes <= r.N) continue;
                        if (r == null) r = Add(list, "tomb", "", p, minutes, 3);
                        r.N = minutes; r.Day = Day(); r.Told = 0;
                        Save(item, list);
                    }
            }
        }

        // ---------------------------------------------------------------- remembering aloud

        internal sealed class Option { public Mem M; public string Tag; public float Score; }

        // what this raven could recall now, best fitting first; ctx: the theme of the moment (build, sail, here...)
        internal static List<Option> Options(ItemDrop.ItemData item, Player p, string ctx, string friendNear, out List<Mem> list)
        {
            list = Load(item);
            List<Option> result = new List<Option>();
            int today = Day();
            System.Random rng = new System.Random();
            string biomeNow = p != null ? p.GetCurrentBiome().ToString() : "";
            foreach (Mem m in list)
            {
                if (m.Kind == "today") continue;
                if (m.Told > 0 && (m.ToldDay == today || today - m.ToldDay < 2 * m.Told)) continue; // a story told rests, longer each time
                string tag = TagOf(m, p, rng);
                if (tag == null) continue;
                float score = m.W;
                bool sameLand = m.Biome == biomeNow && m.Biome.Length > 0;
                switch (m.Kind)
                {
                    case "death": case "fdeath":
                        if (tag.StartsWith("mem_death_") && tag.EndsWith("_here")) score += 8f;
                        else if (sameLand) score += 2f;
                        if (ctx == "chop" && m.Who == "Tree") score += 4f;
                        if ((ctx == "swim" || ctx == "sail") && m.Who == "Drowning") score += 4f;
                        break;
                    case "biome": case "dungeon": case "boss": case "near": if (sameLand) score += 3f; break;
                    case "home": if (ctx == "build" || ctx == "craft" || ctx == "sleep" || ctx == "rest") score += 4f; if (sameLand) score += 1f; break;
                    case "metal": if (ctx == "craft" || ctx == "mine" || ctx == "build") score += 3f; break;
                    case "voyage": if (ctx == "sail" || ctx == "swim") score += 5f; break;
                    case "tame": if (ctx == "pet") score += 5f; break;
                    case "fall": if (ctx == "fall") score += 4f; break;
                    case "friend": if (friendNear == m.Who) score += 5f; break;
                    case "recent":
                        int age = today - m.Day;
                        score += age <= 1 ? 4f : age <= 3 ? 2f : 0f; // yesterday's boar beats an old tale now and then
                        if (sameLand) score += 1f;
                        break;
                }
                if (m.Kind == "boss" && !string.IsNullOrEmpty(friendNear) && m.Extra.Contains(friendNear)) score += 4f;
                int ago = today - m.Day;
                if (ago > 0 && Array.IndexOf(Round, ago) >= 0) score += 3f; // "exactly ten days ago"
                score /= 1f + m.Told * 0.7f;
                score *= 0.7f + 0.6f * (float)rng.NextDouble();
                Option o = new Option(); o.M = m; o.Tag = tag; o.Score = score;
                result.Add(o);
            }
            Option stats = new Option(); stats.Tag = "mem_stats"; stats.Score = 1.2f * (0.7f + 0.6f * (float)rng.NextDouble());
            result.Add(stats);
            result.Sort((a, b) => b.Score.CompareTo(a.Score));
            return result;
        }

        internal static readonly int[] Round = { 7, 10, 20, 30, 50, 100, 200, 365 };

        private static string TagOf(Mem m, Player p, System.Random rng)
        {
            bool creature = m.Extra == "c" || m.Extra == "b";
            switch (m.Kind)
            {
                case "fdeath": return creature ? "mem_first_death_by" : "mem_first_death_cause";
                case "death":
                    if (Distance(m, p) < 60f) return m.N >= 2 ? "mem_death_again_here" : "mem_death_here";
                    return creature ? "mem_death_by" : "mem_death_cause";
                case "grudge": return m.N >= 2 ? "mem_grudge" : null;
                case "near": return "mem_near";
                case "boss": return m.Extra.Length > 0 && rng.Next(2) == 0 ? "mem_boss_friend" : "mem_boss";
                case "biome": return m.N == 1 ? "mem_biome_death" : "mem_biome";
                case "dungeon": return "mem_dungeon";
                case "slaughter": return "mem_slaughter";
                case "voyage": return "mem_voyage";
                case "fall": return "mem_fall";
                case "tame": return "mem_tame";
                case "home": return "mem_home";
                case "friend": return "mem_friend";
                case "tomb": return "mem_tomb";
                case "metal": return "mem_metal";
                case "badday": return "mem_badday";
                case "recent": return m.Extra.Length > 0 ? "mem_r_" + m.Extra : null;
            }
            return null;
        }

        // a raven brought back has lost some of it on the way: every recent moment and half of the deaths and grudges
        internal static void Forget(ItemDrop.ItemData item)
        {
            List<Mem> list = Load(item);
            list.RemoveAll(m => m.Kind == "recent" || m.Kind == "today");
            foreach (string kind in new string[] { "death", "grudge" })
            {
                List<Mem> shelf = list.FindAll(m => m.Kind == kind);
                for (int i = 1; i < shelf.Count; i += 2) list.Remove(shelf[i]);
            }
            Save(item, list);
        }

        internal static void MarkTold(ItemDrop.ItemData item, List<Mem> list, Mem m)
        {
            if (m == null) return;
            m.Told++;
            m.ToldDay = Day();
            Save(item, list);
        }

        // "учора", "5 днів тому", "рівно 10 днів тому", "давно"
        internal static string When(int day)
        {
            int ago = Day() - day;
            if (ago < 0) return Lang.T("колись");
            if (ago == 0) return Lang.T("сьогодні");
            if (ago == 1) return Lang.T("учора");
            if (ago == 2) return Lang.T("позавчора");
            string days = Lang.N(ago, "день|дні|днів");
            if (Array.IndexOf(Round, ago) >= 0) return Lang.F("рівно {0} тому", days);
            return ago > 60 ? Lang.F("давно, ще {0} тому", days) : Lang.F("{0} тому", days);
        }

        // what took a life when no creature did: "ти загинув через ..."
        internal static string Cause(string hitType)
        {
            switch (hitType)
            {
                case "Fall": case "Impact": return Lang.T("падіння");
                case "Drowning": case "Water": return Lang.T("воду");
                case "AshlandOcean": return Lang.T("окріп");
                case "Tree": return Lang.T("дерево");
                case "Burning": case "CinderFire": return Lang.T("вогонь");
                case "Freezing": return Lang.T("холод");
                case "Poisoned": return Lang.T("отруту");
                case "Smoke": return Lang.T("дим");
                case "Cart": return Lang.T("віз");
                case "Structural": return Lang.T("власну будівлю");
                case "Boat": return Lang.T("корабель");
                case "Stalagtite": return Lang.T("сталактит");
                case "EdgeOfWorld": return Lang.T("край світу");
                case "Self": return Lang.T("самого себе");
            }
            return Lang.T("щось геть безглузде");
        }

        // the bosses as the ravens name them: who, whom, with whom, of whom
        internal static string[] BossAcc { get { return Lang.Arr(BossAccSrc); } } // (translated on every read)
        private static readonly string[] BossAccSrc = Lang.Keys("Ейктюра", "Прадавнього", "Кістянку", "Модер", "Яглута", "Королеву", "Фейдера");
        internal static string[] BossIns { get { return Lang.Arr(BossInsSrc); } } // (translated on every read)
        private static readonly string[] BossInsSrc = Lang.Keys("Ейктюром", "Прадавнім", "Кістянкою", "Модер", "Яглутом", "Королевою", "Фейдером");
        internal static string[] BossGen { get { return Lang.Arr(BossGenSrc); } } // (translated on every read)
        private static readonly string[] BossGenSrc = Lang.Keys("Ейктюра", "Прадавнього", "Кістянки", "Модер", "Яглута", "Королеви", "Фейдера");
    }

    // ---------------------------------------------------------------- stories by the fire

    // Sit down (the emote, a chair, a bench, a throne, a ship's bench or helm), or stand a while in a fire's warmth, and a raven on the
    // shoulder recalls something: 5-20 s later, one raven, one story; staying on, another a few minutes later.
    // Nobody talks over anybody: a raven waits while any raven nearby speaks (yours or another player's).
    internal static class Cozy
    {
        internal static float DelayMin = 5f, DelayMax = 20f;
        internal static float FireSeconds = 40f;   // this long in a fire's warmth counts as sitting by it
        internal static float AgainSeconds = 180f; // staying on: the next story
        internal static float RestSeconds = 240f;  // a new sit-down soon after a story: nothing yet
        private static readonly FieldInfo EmoteState = AccessTools.Field(typeof(Player), "m_emoteState");
        private static bool _cozy;
        private static float _heatSince = -1f, _storyAt = -1f, _lastStory = -999f, _quietUntil;
        private static int _told;

        // someone's raven is talking near you: hold the story until it is done
        internal static void Quiet(float seconds) { _quietUntil = Mathf.Max(_quietUntil, Time.time + seconds); }
        internal static bool Quieted { get { return Time.time < _quietUntil; } } // a raven near is talking right now
        internal static bool Cosy { get { return _cozy; } } // sitting by a fire (or on a seat) a while: the talks with others' ravens ask it

        private static bool Sitting(Player p)
        {
            // anything the player sits or holds on: chairs, benches, thrones, a ship's bench or helm, from any mod (they all
            // attach the player the same way); not a saddle: riding is no rest
            if (p.IsAttached() && !p.IsRiding()) return true;
            if (!p.InEmote() || EmoteState == null) return false;
            string emote = EmoteState.GetValue(p) as string;
            return emote != null && emote.Contains("sit");
        }

        // from the twice-a-second sync
        internal static void Tick(Player p)
        {
            if (p == null || p.IsDead() || p.IsSleeping()) { _cozy = false; _storyAt = -1f; _heatSince = -1f; return; }
            bool heat = EffectArea.IsPointInsideArea(p.transform.position, EffectArea.Type.Heat, 1f) != null;
            if (!heat) _heatSince = -1f;
            else if (_heatSince < 0f) _heatSince = Time.time;
            bool now = Sitting(p) || (heat && Time.time - _heatSince >= FireSeconds && p.GetVelocity().magnitude < 1f);
            if (!now) { _cozy = false; _storyAt = -1f; return; }
            if (!_cozy)
            {
                _cozy = true;
                _told = 0;
                if (Time.time - _lastStory >= RestSeconds) Plan();
            }
            else if (_storyAt < 0f && _told > 0 && Time.time - _lastStory >= AgainSeconds) Plan();
            if (_storyAt < 0f || Time.time < _storyAt) return;
            if (!Director.May(Director.Voice.Fireside)) { _storyAt = Time.time + UnityEngine.Random.Range(15f, 30f); return; } // the director: a little later
            if (Time.time < _quietUntil) { _storyAt = _quietUntil + UnityEngine.Random.Range(2f, 6f); return; }
            _storyAt = -1f;
            Tell(p);
        }

        private static void Plan() { _storyAt = Time.time + UnityEngine.Random.Range(DelayMin, DelayMax); }

        // one raven only: Hugin or Munin by the usual odds, the other if the first has nothing to say
        private static void Tell(Player p)
        {
            if (Plugin.Instance == null) return;
            if (RavenTalk.Fireside(p)) { _lastStory = Time.time; _told++; return; } // sometimes the two of them talk instead
            bool first = UnityEngine.Random.Range(0, 100) < Plugin.Instance.MuninChance;
            foreach (bool munin in new bool[] { first, !first })
            {
                string line = Plugin.Instance.ComposeMemory(p, munin);
                if (string.IsNullOrEmpty(line)) continue;
                RavenSkills.SayFromShoulder(p, munin, line);
                Director.Spoke(Director.Voice.Fireside, RavenSpeech.ReadSeconds(line));
                _lastStory = Time.time;
                _told++;
                return;
            }
        }
    }

    // ---------------------------------------------------------------- hooks the diary does not have

    // a fall: how high it was, read before the game resets it
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class FallMemoryPatch
    {
        private static readonly FieldInfo MaxAir = AccessTools.Field(typeof(Character), "m_maxAirAltitude");
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (hit == null || hit.m_hitType != HitData.HitType.Fall || __instance != Player.m_localPlayer || MaxAir == null) return;
            Memory.OnFall((float)MaxAir.GetValue(__instance) - __instance.transform.position.y);
        } catch (System.Exception e) { PatchGuard.Fail("FallMemoryPatch", e); } }
    }

    [HarmonyPatch]
    internal static class TameMemoryPatch
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(Tameable), "Tame"); }
        private static bool Prepare() { return TargetMethod() != null; }
        private static void Postfix(Tameable __instance)
        {
            Player p = Player.m_localPlayer;
            if (p == null || __instance == null || Vector3.Distance(p.transform.position, __instance.transform.position) > 30f) return;
            Character c = __instance.GetComponent<Character>();
            if (c != null) Memory.OnTame(HitInfo.Token(c));
        }
    }

    [HarmonyPatch]
    internal static class HomeMemoryPatch
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(PlayerProfile), "SetCustomSpawnPoint"); }
        private static bool Prepare() { return TargetMethod() != null; }
        private static void Postfix() { Memory.OnHome(Player.m_localPlayer); }
    }

    [HarmonyPatch]
    internal static class TombMemoryPatch
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(TombStone), "GiveBoost"); }
        private static bool Prepare() { return TargetMethod() != null; }
        private static void Postfix() { Memory.OnTomb(); }
    }

    // a metal never held before: the game learns it here
    [HarmonyPatch]
    internal static class MetalMemoryPatch
    {
        private static readonly FieldInfo Known = AccessTools.Field(typeof(Player), "m_knownMaterial");
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(Player), "AddKnownItem"); }
        private static bool Prepare() { return TargetMethod() != null && Known != null; }
        private static void Prefix(Player __instance, ItemDrop.ItemData item)
        {
            if (__instance != Player.m_localPlayer || item == null || item.m_dropPrefab == null || item.m_shared == null) return;
            if (!Memory.Metals.ContainsKey(item.m_dropPrefab.name)) return;
            HashSet<string> known = Known.GetValue(__instance) as HashSet<string>;
            if (known != null && known.Contains(item.m_shared.m_name)) return;
            Memory.OnMetal(__instance, item.m_dropPrefab.name);
        }
    }
}
#endif
