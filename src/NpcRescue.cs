#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // People in need (npc-plan.md, «порятунок»): how settlers come — not the story's. Each named «Серце оселі» hears, once
    // in five to seven game days and only when its last one is over, of someone in trouble out in the lands already
    // reached; its board tells what and where and how long is left (two days), and whoever reads it gets a mark of its
    // own colour on the map. Out there a settler (NpcSettlers):
    //   surrounded  foes round it (a few of its land's); slain, it is safe
    //   captive     a camp of them round it, it sits bound; slain, and untied by a word (its talk)
    //   hungry      sits worn out; a cooked meal from you (its talk)
    //   wounded     lies hurt, a couple of beasts near; driven off, and a healing mead from you (its talk)
    //   lost        its knife or axe left some way off (it says where), guarded; brought back to it (its talk)
    //   freezing    (the Mountains) sits shivering, frost on it; a burning fire beside it
    //   chased      runs to you with foes at its heels (it follows you); slain, it is safe
    //   giant       one great foe of its land guards it (not in the Meadows); slain, it is safe
    //   astray      lost somewhere about the mark (the mark is off by a few dozen metres); it calls out, seen from far — found
    //               and spoken to (its talk)
    //   ambush      (seldom) looks like a plain call for help — come close and foes jump out round you; slain, both safe
    // Saved, it thanks you and may be hired (as any person: «Іди сам до оселі»…). Not saved in time, it is gone (and its
    // foes and its lost thing with it), and a new one is heard of after a while. At most Cap settlers on the server.
    // The server keeps the times and makes the person (a ZDO where it waits); what it changes on a person another game runs it
    // asks of that game (Tell: a write from here over a ZDO that game owns is lost at that game's next write); the game where it is loaded sets the scene
    // when someone comes near (NpcRescueSite) — its foes, its pose, its lost thing.
    internal static class NpcRescue
    {
        internal const string Whose = "rescue"; // (its plans on a person's Scene layer: the story's own take it over — NpcMind.May)
        internal const int Cap = 20;
        private const float DaysMin = 5f, DaysMax = 7f, LastsDays = 3f, Tick = 20f, NearMin = 250f, NearMax = 900f;
        internal static readonly int KNext = "RavenWatch_rescueNext".GetStableHashCode();   // on a heart: long, world seconds of the next one
        internal static readonly int KNpc = "RavenWatch_rescueNpc".GetStableHashCode();    // on a heart: string ref of its person in need
        internal static readonly int KInfo = "RavenWatch_rescueInfo".GetStableHashCode();  // on a heart: "kind|x|z|until" (its board)
        internal static readonly int KKind = "RavenWatch_rescue".GetStableHashCode();      // on the person: its trouble ("" none)
        internal static readonly int KLonger = "RavenWatch_rescueLonger".GetStableHashCode(); // on the person: bool, its time stretched once someone reached it
        internal static readonly int KUntil = "RavenWatch_rescueUntil".GetStableHashCode(); // on the person: long, world seconds
        internal static readonly int KStage = "RavenWatch_rescueStage".GetStableHashCode(); // on the person: 0 waiting · 1 set · 2 saved
        internal static readonly int KFoes = "RavenWatch_rescueFoes".GetStableHashCode();  // on the person: its foes' refs "a,b,c"
        internal static readonly int KItem = "RavenWatch_rescueItem".GetStableHashCode();  // on the person: vec3 where its lost thing lies
        internal static readonly string[] Kinds = { "surrounded", "captive", "hungry", "wounded", "lost", "freezing", "chased", "giant", "astray", "ambush" };
        internal static readonly int KLost = "RavenWatch_rescueLost".GetStableHashCode(); // on the person: string ref of its lost thing (not a foe)

        // a trouble for that land, by weight (the Mountains' cold only there; no giant in the Meadows; an ambush seldom)
        private static string PickKind(Heightmap.Biome land)
        {
            List<KeyValuePair<string, float>> w = new List<KeyValuePair<string, float>>();
            foreach (string k in new[] { "surrounded", "captive", "hungry", "wounded", "lost", "chased", "astray" }) w.Add(new KeyValuePair<string, float>(k, 1f));
            if (land != Heightmap.Biome.Meadows) w.Add(new KeyValuePair<string, float>("giant", 1f));
            if (land == Heightmap.Biome.Mountain) w.Add(new KeyValuePair<string, float>("freezing", 1.5f));
            w.Add(new KeyValuePair<string, float>("ambush", 0.4f));
            float sum = 0f; foreach (KeyValuePair<string, float> e in w) sum += e.Value;
            float roll = UnityEngine.Random.value * sum;
            foreach (KeyValuePair<string, float> e in w) { roll -= e.Value; if (roll <= 0f) return e.Key; }
            return "surrounded";
        }

        internal static double Now { get { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0; } }
        private static float Day { get { return EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f; } }

        // what befell «the person» (людина: the words agree with it — whether a man or a woman is not known yet out there)
        internal static string KindWord(string k)
        {
            switch (k)
            {
                case "surrounded": return Lang.T("у кільці ворогів");
                case "captive": return Lang.T("у полоні в таборі");
                case "hungry": return Lang.T("без сил від голоду");
                case "wounded": return Lang.T("поранена, довкола звірі");
                case "lost": return Lang.T("загубила свою річ");
                case "freezing": return Lang.T("мерзне в горах");
                case "chased": return Lang.T("тікає від погоні");
                case "giant": return Lang.T("її стереже велетень");
                case "astray": return Lang.T("заблукала — шукай за голосом");
                case "ambush": return Lang.T("поранена, кличе на допомогу"); // (it does not say it is a trap)
            }
            return k;
        }

        // ---------------------------------------------------------------- the server: times, the person made, the end
        private static float _next;
        private static string _forced; // (the console's test: this kind next)

        // for testing (the console, on the server's game — alone or the host): the nearest named heart hears of one now
        internal static string Test(Vector3 at, string kind)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return "Лише на сервері (або коли граєш сам).";
            NpcHeart h = NpcHeart.Near(at, 200f);
            if (h == null || h.Name.Length == 0) return "Поруч нема названого «Серця оселі».";
            ZDO z = h.GetComponent<ZNetView>().GetZDO();
            ZDOID old = NpcKit.GetRef(z, KNpc);
            if (!old.IsNone()) End(z, ZDOMan.instance.GetZDO(old), true, (long)Now);
            Own(z); z.Set(KNext, 0L);
            _forced = Array.IndexOf(Kinds, kind) >= 0 ? kind : null;
            _next = 0f;
            ServerTick();
            string info = z.GetString(KInfo, "");
            return info.Length > 0 ? "Подія: " + info + ". Відкрий дошку серця — з'явиться позначка." : "Не вийшло (ліміт " + Cap + " поселенців або нема місця) — див. журнал.";
        }

        // a heart just built (NpcHeart.Start): the first of its builder in this world hears of one as soon as it is named,
        // any later one waits as usual
        internal static void Born(ZDO heart, bool first)
        {
            heart.Set(KNext, first ? 1L : (long)Now + (long)(Day * UnityEngine.Random.Range(DaysMin, DaysMax)));
        }

        internal static void ServerTick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || Time.time < _next || Perches.Hearts.Count == 0) return;
            _next = Time.time + Tick;
            try
            {
                long now = (long)Now;
                int settlers = -1;
                foreach (Perches.Def d in Perches.Hearts)
                {
                    List<ZDO> hearts = NpcCensus.Of(d.Prefab); // (the shared census: NpcCensus)
                    foreach (ZDO h in hearts)
                    {
                        if (h.GetString(NpcHeart.KName, "").Length == 0) continue; // (an unnamed heart hears nothing)
                        ZDOID npc = NpcKit.GetRef(h, KNpc);
                        if (!npc.IsNone())
                        {
                            ZDO n = ZDOMan.instance.GetZDO(npc);
                            if (n == null || n.GetLong(NpcTalk.KHired, 0L) != 0L) { End(h, n, false, now); continue; } // (gone, or hired: over)
                            if (n.GetBool(NpcRescueHits.KGone, false) && now > n.GetLong(KUntil, 0L)) { End(h, n, true, now); continue; } // (saved, sent its way: gone)
                            int st = n.GetInt(KStage, 0); long until = n.GetLong(KUntil, 0L);
                            if (st >= NpcRescueHits.Dead && now > until) { End(h, n, true, now); continue; } // (died, or its time ran out while you were there — told: gone)
                            if (st == 1 && !n.GetBool(KLonger, false)) // (someone reached it: at least half a day left from now — not lost to a clock you never saw)
                            {
                                long need = now + (long)(Day * 0.5f);
                                Tell(n, 1, System.Math.Max(until, need));
                                if (until < need) { until = need; string[] info = h.GetString(KInfo, "").Split('|'); if (info.Length >= 4) { info[3] = need.ToString(); Own(h); h.Set(KInfo, string.Join("|", info)); } }
                            }
                            if (now > until && st == 1) { Tell(n, NpcRescueHits.Expired, now + 20L); continue; } // (time ran out with someone there: told on their games, gone a moment later)
                            if (now > until && st < 2) { End(h, n, true, now); continue; } // (nobody came in time: gone)
                            if (now > n.GetLong(KUntil, 0L) + (long)Day) { End(h, n, true, now); continue; } // (saved but never hired: it goes its way a day after)
                            continue;
                        }
                        if (now < h.GetLong(KNext, 0L)) continue;
                        if (settlers < 0) settlers = Settlers();
                        if (settlers >= Cap) continue;
                        if (Start(h, now)) settlers++;
                    }
                }
            }
            catch (Exception e) { Npcs.Log("rescue: " + e.Message); }
        }

        // its stage and its time, on the game that owns it (none owns it, or this one: written here)
        private static void Tell(ZDO n, int stage, long until)
        {
            long owner = n.GetOwner();
            if (owner == 0L || owner == ZDOMan.GetSessionID()) { Keep(n, stage, until); return; }
            ZRoutedRpc.instance.InvokeRoutedRPC(owner, n.m_uid, NpcActor.RpcAct, "rescuetime " + stage + " " + until); // (not there yet: asked again at the next pass)
        }

        internal static void Keep(ZDO n, int stage, long until)
        {
            if (n == null) return;
            n.Set(KLonger, true);
            n.Set(KStage, stage);
            n.Set(KUntil, until);
        }

        // the settlers of the whole server (the template marked generated)
        internal static int Settlers()
        {
            int n = 0;
            List<ZDO> all = NpcCensus.Of(Npcs.HumanPrefab); // (the shared census: NpcCensus)
            foreach (ZDO z in all) { NpcDef d = NpcDefs.Find(z.GetString(Npcs.KId, "")); if (d != null && d.Generated) n++; }
            return n;
        }

        private static void Own(ZDO z) { if (z != null && z.GetOwner() != ZDOMan.GetSessionID()) z.SetOwner(ZDOMan.GetSessionID()); }

        private static bool Start(ZDO heart, long now)
        {
            Vector3 home = heart.GetPosition();
            Vector3 at; Heightmap.Biome land;
            if (!Spot(home, out at, out land)) { _forced = null; Own(heart); heart.Set(KNext, now + (long)(Day * 0.5f)); return false; } // (nowhere fit: half a day later)
            string kind = _forced != null ? _forced : PickKind(land);
            _forced = null;
            int hash = Npcs.HumanPrefab.GetStableHashCode();
            ZDO n = ZDOMan.instance.CreateNewZDO(at, hash);
            n.Persistent = true;
            n.SetPrefab(hash);
            n.Set(Npcs.KId, "settler");
            n.Set(NpcRules.KHome, at);
            n.Set(KKind, kind);
            long until = now + (long)(Day * LastsDays);
            n.Set(KUntil, until);
            NpcLocker.Put(n, NpcLevers.KVars, "inneed=1;" + kind + "=1");
            Own(heart);
            NpcKit.Mark(n); // (its own number: a person moves — its heart finds it again after a restart)
            NpcCensus.Added(n); // (counted at once: no second one past the cap before the next pass)
            NpcKit.SetRef(heart, KNpc, n.m_uid);
            Vector3 mark = at;
            if (kind == "astray") { Vector2 off = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(25f, 60f); mark += new Vector3(off.x, 0f, off.y); } // (only about there: found by its calls)
            heart.Set(KInfo, kind + "|" + Mathf.RoundToInt(mark.x) + "|" + Mathf.RoundToInt(mark.z) + "|" + until);
            Npcs.Log("rescue: «" + heart.GetString(NpcHeart.KName, "") + "» hears of someone " + kind + " at " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z));
            return true;
        }

        // over: the next one after five to seven days; gone (not saved): the person, its foes, its lost thing taken away
        private static void End(ZDO heart, ZDO npc, bool remove, long now)
        {
            Own(heart);
            NpcKit.SetRef(heart, KNpc, ZDOID.None);
            heart.Set(KInfo, "");
            heart.Set(KNext, now + (long)(Day * UnityEngine.Random.Range(DaysMin, DaysMax)));
            if (npc == null) return;
            if (!remove) return; // (hired: its own game takes its trouble off it — NpcRescueSite; the server never takes over a person)
            foreach (string r in (npc.GetString(KFoes, "") + "," + npc.GetString(KLost, "")).Split(',')) { ZDO f = r.Length > 0 ? ZDOMan.instance.GetZDO(NpcKit.Resolve(r)) : null; if (f != null) { Own(f); ZDOMan.instance.DestroyZDO(f); } }
            Own(npc);
            ZDOMan.instance.DestroyZDO(npc);
            Npcs.Log("rescue: the one in need is gone");
        }

        // a spot in the lands already reached (its land: the newest one now and then), on dry land, some way off
        private static bool Spot(Vector3 home, out Vector3 at, out Heightmap.Biome land)
        {
            at = home; land = Heightmap.Biome.Meadows;
            WorldGenerator wg = WorldGenerator.instance;
            if (wg == null || ZoneSystem.instance == null) return false;
            List<Heightmap.Biome> lands = Lands();
            Heightmap.Biome want = UnityEngine.Random.value < 0.5f ? lands[lands.Count - 1] : lands[UnityEngine.Random.Range(0, lands.Count)];
            float water = ZoneSystem.instance.m_waterLevel;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < 80; i++)
                {
                    Vector2 d = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(NearMin, NearMax * (pass + 1));
                    Vector3 p = home + new Vector3(d.x, 0f, d.y);
                    Heightmap.Biome b = wg.GetBiome(p);
                    if (pass == 0 ? b != want : !lands.Contains(b)) continue; // (its land; else any reached one, further out)
                    float h = wg.GetHeight(p.x, p.z);
                    if (h < water + 2f || h > water + 600f) continue; // (dry land; the Mountains are high)
                    if (Mathf.Abs(wg.GetHeight(p.x + 3f, p.z) - h) > 2f || Mathf.Abs(wg.GetHeight(p.x, p.z + 3f) - h) > 2f) continue; // (no cliff)
                    p.y = h + 0.5f;
                    at = p; land = b;
                    return true;
                }
            return false;
        }

        // the lands reached: by the bosses beaten
        private static List<Heightmap.Biome> Lands()
        {
            List<Heightmap.Biome> l = new List<Heightmap.Biome> { Heightmap.Biome.Meadows };
            ZoneSystem z = ZoneSystem.instance;
            if (z.GetGlobalKey("defeated_eikthyr")) l.Add(Heightmap.Biome.BlackForest);
            if (z.GetGlobalKey("defeated_gdking")) l.Add(Heightmap.Biome.Swamp);
            if (z.GetGlobalKey("defeated_bonemass")) l.Add(Heightmap.Biome.Mountain);
            if (z.GetGlobalKey("defeated_dragon")) l.Add(Heightmap.Biome.Plains);
            if (z.GetGlobalKey("defeated_goblinking")) l.Add(Heightmap.Biome.Mistlands);
            if (z.GetGlobalKey("defeated_queen")) l.Add(Heightmap.Biome.AshLands);
            return l;
        }

        // ---------------------------------------------------------------- a heart's board (any game near it)
        internal static string BoardLine(ZDO heart, Vector3 at)
        {
            if (heart == null) return null;
            string info = heart.GetString(KInfo, "");
            if (info.Length > 0)
            {
                string[] f = info.Split('|');
                long until; float x, z;
                if (f.Length >= 4 && long.TryParse(f[3], out until) && float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                {
                    Vector3 where = new Vector3(x, 0f, z);
                    Know(NpcKit.GetRef(heart, KNpc), f[0], where, until);
                    return Lang.F("Людина в біді: {0}. {1}, {2}. Лишилось: {3}. Позначено на мапі.", KindWord(f[0]), Direction(at, where), Distance(at, where), Left(until - (long)Now));
                }
            }
            long next = heart.GetLong(KNext, 0L) - (long)Now;
            if (heart.GetString(NpcHeart.KName, "").Length == 0) return Lang.T("Безіменне серце чуток не чує — дай йому назву.");
            return next > 0 ? Lang.F("Чуток поки нема. Наступні — десь за {0}.", Left(next)) : Lang.T("Чуток поки нема.");
        }

        // one going on, in short (a heart's board, «Треба зробити»); null: none. Marks nothing: that is BoardLine, when the board is opened
        internal sealed class Short { public string Text = "", Left = ""; public Vector3 At; }
        internal static Short BoardShort(ZDO heart, Vector3 at)
        {
            string info = heart != null ? heart.GetString(KInfo, "") : "";
            if (info.Length == 0) return null;
            string[] f = info.Split('|');
            long until; float x, z;
            if (f.Length < 4 || !long.TryParse(f[3], out until) || !float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return null;
            Vector3 where = new Vector3(x, WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(x, z) : at.y, z); // (kept without its height: its ring on the land, not under it)
            return new Short { Text = Lang.F("Людина в біді: {0}, {1}, {2}", KindWord(f[0]), Direction(at, where), Distance(at, where)), Left = Left(until - (long)Now), At = where };
        }

        internal static string Left(long seconds)
        {
            float days = Mathf.Max(0f, seconds) / Day;
            if (days >= 1f) { int d = Mathf.FloorToInt(days); int h = Mathf.FloorToInt((days - d) * 24f); return Lang.F("{0} д. {1} год.", d, h); }
            return Lang.F("{0} год.", Mathf.Max(1, Mathf.CeilToInt(days * 24f)));
        }

        private static string Distance(Vector3 a, Vector3 b) { float m = Utils.DistanceXZ(a, b); return m >= 1000f ? Lang.F("{0} км", (m / 1000f).ToString("0.#")) : Lang.F("{0} м", Mathf.RoundToInt(m)); }

        internal static string Direction(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float ang = Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);
            string[] w = { "на північ", "на північний схід", "на схід", "на південний схід", "на південь", "на південний захід", "на захід", "на північний захід" };
            return Lang.T(w[Mathf.RoundToInt(ang / 45f) % 8]);
        }

        // ---------------------------------------------------------------- the marks you know of (read on a board): kept in
        // the character for this world, drawn on the map until over (NpcMap)
        internal sealed class Known { public ZDOID Npc; public string Kind; public Vector3 At; public long Until; }
        private const string SeenKey = "RavenWatch_rescues";

        internal static void Know(ZDOID npc, string kind, Vector3 at, long until)
        {
            Player p = Player.m_localPlayer;
            if (p == null || npc.IsNone()) return;
            List<Known> l = All();
            foreach (Known k in l) if (k.Npc == npc) return;
            l.Add(new Known { Npc = npc, Kind = kind, At = at, Until = until });
            Save(l);
        }

        internal static void Forget(ZDOID npc) { List<Known> l = All(); if (l.RemoveAll(k => k.Npc == npc) > 0) Save(l); }

        internal static List<Known> All()
        {
            List<Known> l = new List<Known>();
            Player p = Player.m_localPlayer;
            string s;
            if (p == null || ZNet.instance == null || !p.m_customData.TryGetValue(SeenKey, out s)) return l;
            string world = ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture);
            long now = (long)Now;
            foreach (string e in s.Split(';'))
            {
                string[] f = e.Split('|');
                long until; float x, z;
                if (f.Length < 6 || f[0] != world || !long.TryParse(f[5], out until) || until + (long)Day < now) continue; // (another world's, or long over)
                if (!float.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) continue;
                l.Add(new Known { Npc = NpcKit.Resolve(f[1]), Kind = f[2], At = new Vector3(x, 0f, z), Until = until });
            }
            return l;
        }

        private static void Save(List<Known> l)
        {
            Player p = Player.m_localPlayer;
            if (p == null || ZNet.instance == null) return;
            string world = ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture);
            StringBuilder sb = new StringBuilder();
            foreach (Known k in l) { if (sb.Length > 0) sb.Append(';'); sb.Append(world).Append('|').Append(NpcKit.Save(k.Npc)).Append('|').Append(k.Kind).Append('|').Append(Mathf.RoundToInt(k.At.x)).Append('|').Append(Mathf.RoundToInt(k.At.z)).Append('|').Append(k.Until); }
            p.m_customData[SeenKey] = sb.ToString();
        }
    }

    // ---------------------------------------------------------------- the scene where the person waits (where it is owned)
    internal sealed class NpcRescueSite : MonoBehaviour
    {
        private const string Whose = NpcRescue.Whose;
        private NpcActor _a;
        private float _next;
        private void Awake() { _a = GetComponent<NpcActor>(); }

        private int _seenStage = -1; // (this game's: the stage it last saw — its turn to «saved» told to whoever is near)

        private void Update()
        {
            if (_a == null || Time.time < _next) return;
            _next = Time.time + 1f;
            ZDO z = _a.Zdo;
            string kind = z != null ? z.GetString(NpcRescue.KKind, "") : "";
            if (kind.Length == 0) { _seenStage = -1; return; }
            Told(z);
            if (!_a.Owned) return;
            if (NpcTalk.Hired(_a)) { Done(z); return; } // (hired: a person like any other now)
            int stage = z.GetInt(NpcRescue.KStage, 0);
            if (stage >= NpcRescueHits.Dead) return; // (died: the server takes it away)
            if (stage == 0) { if (Player.GetClosestPlayer(transform.position, 80f) != null) SetUp(z, kind); return; }
            if (stage == 1)
            {
                if (kind == "ambush" && NpcLevers.Flag(_a, "ambush")) { Player near = Player.GetClosestPlayer(transform.position, 14f); if (near != null) Spring(z, near); } // (the trap shuts)
                if (kind == "freezing") Freezing();
                if (kind == "astray" && Time.time >= _nextCall && Player.GetClosestPlayer(transform.position, 110f) != null) { _nextCall = Time.time + UnityEngine.Random.Range(8f, 14f); NpcChatter.Mutter(_a, "need_astray"); } // (calls out: seen from far)
                if (NpcLevers.Flag(_a, "foes") && !FoesLeft(z))
                {
                    NpcLevers.Do(_a, new[] { "npcunflag", "foes" });
                    if (kind == "surrounded" || kind == "chased" || kind == "giant" || kind == "ambush") NpcLevers.Do(_a, new[] { "npcflag", "rescued" });
                    else if (kind == "captive") _a.Say(_a.Form(Lang.T("Вони мертві! Розв'яжи мене, прошу.")));
                    else if (kind == "wounded") _a.Say(_a.Form(Lang.T("Їх нема… дякую. Меду б мені…")));
                }
                if (NpcLevers.Flag(_a, "rescued")) { Saved(z); return; }
                // (after a reload the scene's pose is gone — the plan of a scene is not kept: put back while it waits)
                if (NpcLevers.Flag(_a, "inneed") && _a.Mind != null && _a.Mind.Of(NpcMind.Layer.Scene) == null) _a.Mind.Put(NpcMind.Layer.Scene, ScenePlan(_a, kind), Whose);
                if (UnityEngine.Random.value < 0.03f && Player.GetClosestPlayer(transform.position, 25f) != null) NpcChatter.Mutter(_a, "need_" + kind); // (a call for help now and then)
            }
        }

        // the scene once someone comes near: its foes, its pose, its lost thing
        private void SetUp(ZDO z, string kind)
        {
            Heightmap.Biome land = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(transform.position) : Heightmap.Biome.Meadows;
            List<string> foes = new List<string>();
            if (kind == "surrounded") Spawn(land, transform.position, 3 + UnityEngine.Random.Range(0, 2), 7f, foes);
            else if (kind == "captive") Spawn(land, transform.position, 4 + UnityEngine.Random.Range(0, 2), 9f, foes);
            else if (kind == "wounded") Spawn(land, transform.position, 2, 10f, foes, true);
            else if (kind == "giant") Spawn(land, transform.position, 1, 6f, foes, false, true);
            else if (kind == "chased")
            {
                Player p = Player.GetClosestPlayer(transform.position, 100f);
                Vector3 away = p != null ? transform.position - p.transform.position : transform.forward; away.y = 0f;
                Spawn(land, transform.position + away.normalized * 14f, 3 + UnityEngine.Random.Range(0, 2), 3f, foes); // (at its heels, on the side away from you)
            }
            else if (kind == "lost")
            {
                Vector2 d = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(40f, 70f);
                Vector3 at = transform.position + new Vector3(d.x, 0f, d.y);
                float h; at.y = ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out h) ? h : at.y;
                _lostRef = null;
                Lost(land, at);
                if (_lostRef != null) z.Set(NpcRescue.KLost, _lostRef); // (not a foe: taken away with them when the time is up)
                z.Set(NpcRescue.KItem, at);
                Spawn(land, at, 2 + UnityEngine.Random.Range(0, 2), 5f, foes);
                _a.Say(Lang.F("Моя річ лишилась {0} звідси, кроків за {1}. Там вони…", NpcRescue.Direction(transform.position, at), Mathf.RoundToInt(Utils.DistanceXZ(transform.position, at))));
            }
            z.Set(NpcRescue.KFoes, string.Join(",", foes.ToArray()));
            if (foes.Count > 0) NpcLevers.Do(_a, new[] { "npcflag", "foes" });
            if (_a.Mind != null) { _a.Mind.Put(NpcMind.Layer.Scene, ScenePlan(_a, kind), Whose); _a.Mind.Do("fight off", NpcMind.Layer.Scene, Whose); } // (no fight while in need: on its scene's layer — gone with it)
            z.Set(NpcRescue.KStage, 1);
            NpcRescueHits.Fill(_a); // (the blows it can bear: all)
            NpcChatter.Mutter(_a, "need_" + kind);
            Npcs.Log(_a.Id + ": in need («" + kind + "»), " + foes.Count + " foes");
        }

        private static string Pose(string kind) { return kind == "surrounded" || kind == "giant" ? "cower" : kind == "captive" || kind == "hungry" || kind == "freezing" || kind == "ambush" ? "sit" : kind == "wounded" ? "rest" : "despair"; }

        // what it does while it waits: a pose — or, chased, runs to whoever is near; or what you told it (KMode): hide where
        // you showed it, or walk with you under your guard
        internal static readonly int KMode = "RavenWatch_rescueMode".GetStableHashCode(); // string: "" wait · "follow <user:id>" · "hide x y z"
        private static NpcMind.Plan ScenePlan(NpcActor a, string kind) { NpcMind.Plan p = PlanOf(a, kind); p.Speaks = true; return p; } // (one in need calls for help: its scene keeps it talking)

        private static NpcMind.Plan PlanOf(NpcActor a, string kind)
        {
            Vector3 at = a.transform.position;
            string[] m = (a.Zdo != null ? a.Zdo.GetString(KMode, "") : "").Split(' ');
            if (m[0] == "follow" && m.Length > 1) { ZDOID who = NpcKit.Resolve(m[1]); if (!who.IsNone() && NpcKit.Find(who) != null) return new NpcMind.Plan { Kind = NpcMind.Task.Follow, Who = who, Keep = 3f }; } // (whom it walked with gone — a player is numbered anew each time: it waits where it is)
            float x, y, z;
            if (m[0] == "hide" && m.Length > 3 && float.TryParse(m[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) && float.TryParse(m[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y) && float.TryParse(m[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                return new NpcMind.Plan { Kind = NpcMind.Task.Go, To = new Vector3(x, y, z), Near = 0.8f, Pose = "sit" }; // (there: sits low — NpcMind.Arrived)
            Player p = kind == "chased" ? Player.GetClosestPlayer(at, 100f) : null;
            if (p != null) return new NpcMind.Plan { Kind = NpcMind.Task.Follow, Who = p.GetZDOID(), Keep = 2.5f };
            return new NpcMind.Plan { Kind = NpcMind.Task.Pose, Pose = Pose(kind), To = at };
        }

        // told what to do while you are away (its owner's game: NpcActor «rescuemode»)
        internal static void Mode(NpcActor a, string mode)
        {
            if (a == null || a.Zdo == null || !NpcRescueHits.InNeed(a)) return;
            a.Zdo.Set(KMode, mode);
            string kind = a.Zdo.GetString(NpcRescue.KKind, "");
            if (a.Mind != null) a.Mind.Put(NpcMind.Layer.Scene, ScenePlan(a, kind), Whose);
            a.Say(a.Form(Lang.T(mode.StartsWith("follow") ? "Іду за тобою. Не відходь далеко." : mode.StartsWith("hide") ? "Добре, сховаюсь там. Тільки повертайся." : "Чекатиму тут. Повертайся швидше.")));
        }

        // the trap shuts: foes round the one who came
        private void Spring(ZDO z, Player near)
        {
            Heightmap.Biome land = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(transform.position) : Heightmap.Biome.Meadows;
            List<string> foes = new List<string>();
            Spawn(land, near.transform.position, 4, 11f, foes);
            z.Set(NpcRescue.KFoes, string.Join(",", foes.ToArray()));
            NpcLevers.Do(_a, new[] { "npcunflag", "ambush" });
            if (foes.Count > 0) NpcLevers.Do(_a, new[] { "npcflag", "foes" });
            _a.Say(_a.Form(Lang.T("Пастка! Вибач… вони змусили мене кликати.")));
            Npcs.Log(_a.Id + ": the ambush sprang, " + foes.Count + " foes");
        }

        // cold: frost on it now and then; a burning fire beside it — warm, saved
        private float _nextFrost, _nextCall;
        private void Freezing()
        {
            if (NpcKit.Nearest<Fireplace>(transform.position, 4f, f => f.IsBurning() && NpcKit.Warms(f)) != null) { NpcLevers.Do(_a, new[] { "npcunflag", "freezing" }); NpcLevers.Do(_a, new[] { "npcflag", "rescued" }); return; }
            if (Time.time < _nextFrost) return;
            _nextFrost = Time.time + 4f;
            Character c = _a.GetComponent<Character>();
            if (c != null && c.GetSEMan() != null) c.GetSEMan().AddStatusEffect("Frost".GetStableHashCode(), true, 0, 0f, (short)0); // (the game's frost on it: seen by everyone)
        }

        // hired: its trouble taken off it for good (its flags of need, its scene)
        private void Done(ZDO z)
        {
            z.Set(NpcRescue.KKind, "");
            z.Set(KMode, "");
            foreach (string f in new[] { "inneed", "foes", "captive", "hungry", "wounded", "lost", "surrounded", "freezing", "chased", "giant", "astray", "ambush" }) if (NpcLevers.Flag(_a, f)) NpcLevers.Do(_a, new[] { "npcunflag", f });
            if (_a.Mind != null && _a.Mind.Of(NpcMind.Layer.Scene) != null) _a.Mind.Put(NpcMind.Layer.Scene, null, Whose);
        }

        // on every game: the moment it is saved, whoever is near is told so in the middle of the screen (and talks to it)
        private void Told(ZDO z)
        {
            int stage = z.GetInt(NpcRescue.KStage, 0);
            if (_seenStage == 1 && stage == NpcRescueHits.Expired)
            {
                Player me1 = Player.m_localPlayer;
                if (me1 != null && Vector3.Distance(me1.transform.position, transform.position) < 80f && MessageHud.instance != null)
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, _a.Form(Lang.F("{0} не дочекався|дочекалася тебе й пішов|пішла…", _a.Name)));
            }
            else if (_seenStage == 1 && stage == NpcRescueHits.Dead)
            {
                Player me0 = Player.m_localPlayer;
                if (me0 != null && Vector3.Distance(me0.transform.position, transform.position) < 60f && MessageHud.instance != null)
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, _a.Form(Lang.F("{0} не дочекався|дочекалася допомоги…", _a.Name)));
            }
            else if (_seenStage == 1 && stage == 2 && !NpcTalk.Hired(_a))
            {
                Player me = Player.m_localPlayer;
                if (me != null && NpcTalk.With != _a && Vector3.Distance(me.transform.position, transform.position) < 60f && MessageHud.instance != null) // (talking to it already: its thanks are in the talk)
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, _a.Form(Lang.F("{0} врятовано! Поговори з ним|нею.", _a.Name)));
            }
            _seenStage = stage;
        }

        // saved: up, thankful, its own again
        private void Saved(ZDO z)
        {
            z.Set(NpcRescue.KStage, 2);
            z.Set(KMode, ""); // (no more hiding or walking under guard: saved)
            NpcLevers.Do(_a, new[] { "npcunflag", "inneed" });
            if (_a.Mind != null) { _a.Mind.Put(NpcMind.Layer.Scene, null, Whose); _a.Mind.Put(NpcMind.Layer.Routine, new NpcMind.Plan { Kind = NpcMind.Task.Wander, Home = transform.position, Radius = 6f }, Whose); }
            NpcChatter.Mutter(_a, "saved");
            string how = z.GetString(NpcRescue.KKind, "");
            Player by = Player.GetClosestPlayer(transform.position, 40f); // (saved by the sword or a fire: the one by it now — a talk's answer remembers its own saver: !remember rescued)
            if (by != null && (how == "surrounded" || how == "chased" || how == "giant" || how == "ambush" || how == "freezing")) NpcMemory.Remember(_a, "rescued", by.GetPlayerID(), how);
            Npcs.Log(_a.Id + ": saved");
            StoryBus.Raise("npc_saved", _a.Id, 0, transform.position);
        }

        private bool FoesLeft(ZDO z)
        {
            foreach (string r in z.GetString(NpcRescue.KFoes, "").Split(','))
            {
                if (r.Length == 0) continue;
                ZDO f = ZDOMan.instance.GetZDO(NpcKit.Resolve(r));
                if (f == null) continue;
                ZNetView fv = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(f) : null; GameObject go = fv != null ? fv.gameObject : null;
                Character c = go != null ? go.GetComponent<Character>() : null;
                if (c == null || !c.IsDead()) return true; // (alive — or not here to see: counted alive)
            }
            return false;
        }

        // its land's foes (beasts for the wounded), a ring round the spot
        private static readonly Dictionary<Heightmap.Biome, string[]> Foes = new Dictionary<Heightmap.Biome, string[]>
        {
            { Heightmap.Biome.Meadows, new[] { "Greyling", "Greyling", "Greydwarf", "Neck" } },
            { Heightmap.Biome.BlackForest, new[] { "Greydwarf", "Greydwarf", "Greydwarf_Elite", "Skeleton" } },
            { Heightmap.Biome.Swamp, new[] { "Draugr", "Draugr", "Skeleton", "Blob" } },
            { Heightmap.Biome.Mountain, new[] { "Wolf", "Wolf", "Ulv", "Fenring" } },
            { Heightmap.Biome.Plains, new[] { "Goblin", "Goblin", "GoblinArcher", "GoblinBrute" } },
            { Heightmap.Biome.Mistlands, new[] { "Seeker", "Seeker", "SeekerBrute" } },
            { Heightmap.Biome.AshLands, new[] { "Charred_Melee", "Charred_Melee", "Charred_Archer" } },
        };
        private static readonly Dictionary<Heightmap.Biome, string[]> Beasts = new Dictionary<Heightmap.Biome, string[]>
        {
            { Heightmap.Biome.Meadows, new[] { "Boar", "Neck" } }, { Heightmap.Biome.BlackForest, new[] { "Greydwarf" } }, { Heightmap.Biome.Swamp, new[] { "Leech", "Draugr" } },
            { Heightmap.Biome.Mountain, new[] { "Wolf" } }, { Heightmap.Biome.Plains, new[] { "Deathsquito", "Goblin" } }, { Heightmap.Biome.Mistlands, new[] { "Seeker" } }, { Heightmap.Biome.AshLands, new[] { "Asksvin" } },
        };

        private static readonly Dictionary<Heightmap.Biome, string[]> Giants = new Dictionary<Heightmap.Biome, string[]>
        {
            { Heightmap.Biome.BlackForest, new[] { "Troll" } }, { Heightmap.Biome.Swamp, new[] { "Abomination" } }, { Heightmap.Biome.Mountain, new[] { "StoneGolem" } },
            { Heightmap.Biome.Plains, new[] { "GoblinBrute" } }, { Heightmap.Biome.Mistlands, new[] { "SeekerBrute" } }, { Heightmap.Biome.AshLands, new[] { "Morgen" } },
        };

        private static void Spawn(Heightmap.Biome land, Vector3 round, int count, float r, List<string> into, bool beasts = false, bool giant = false)
        {
            string[] pool;
            if (!(giant ? Giants : beasts ? Beasts : Foes).TryGetValue(land, out pool)) pool = giant ? Giants[Heightmap.Biome.BlackForest] : Foes[Heightmap.Biome.Meadows];
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(pool[UnityEngine.Random.Range(0, pool.Length)]) : null;
                if (prefab == null) prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Greydwarf") : null;
                if (prefab == null) continue;
                float ang = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                Vector3 at = round + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * r;
                float h; if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out h)) at.y = h + 0.2f;
                GameObject go = UnityEngine.Object.Instantiate(prefab, at, Quaternion.LookRotation(round - at));
                ZNetView nv = go.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid()) into.Add(NpcKit.Save(nv.GetZDO(), true));
            }
        }

        // its lost thing: a tool of its land, marked as its own (its name as the maker's)
        internal const string LostOf = "RavenWatch_lostOf";
        private string _lostRef;

        private void Lost(Heightmap.Biome land, Vector3 at)
        {
            GameObject prefab = NpcKeepsake.Any(land); // (its own: a necklace, a charm, a pearl — or its land's weapon; one to a cell)
            if (prefab == null && ObjectDB.instance != null) prefab = ObjectDB.instance.GetItemPrefab("SilverNecklace");
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null) return;
            ItemDrop.ItemData it = drop.m_itemData.Clone();
            it.m_dropPrefab = prefab;
            it.m_crafterName = _a.Name;
            it.m_customData[LostOf] = NpcKit.Save(_a.Zdo, true);
            ItemDrop d = ItemDrop.DropItem(it, 1, at + Vector3.up * 0.5f, Quaternion.identity);
            ZNetView nv = d != null ? d.GetComponent<ZNetView>() : null;
            if (nv != null && nv.IsValid()) _lostRef = NpcKit.Save(nv.GetZDO(), true); // (taken away with its foes when the time is up)
        }

        // the thing of this person in a bag (its talk: «Я знайшов твою річ»)
        internal static ItemDrop.ItemData LostThing(Player p, NpcActor a)
        {
            if (p == null || a == null || a.Zdo == null) return null;
            ZDOID me = a.Zdo.m_uid;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) { string v; if (it.m_customData != null && it.m_customData.TryGetValue(LostOf, out v) && NpcKit.Resolve(v) == me) return it; }
            return null;
        }
    }
}
#endif
