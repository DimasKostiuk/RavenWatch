#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // siege <slot>: Odin's golden «Облога гнізда» at the nest the ravens flew to Odin from (slot «nest»). At night, in the
    // rune ring round it, «Почати облогу» (E on the stake, the J circle, the book): three waves of the nest's land come
    // from one side, half of each for the nest itself, the third with a lord. While it goes the nest has a siege's own
    // strength: the beasts' blows take that, not the building (SiegeNestHitPatch); it is gone — the errand is lost. Out of
    // the ring no new wave comes (the nest is still beaten); died — the siege begins anew another night. The waves'
    // beasts give nothing (as the watch's: BindArena.Key); the lord comes once per errand (its trophy, its gold).
    // The siege lives in this game only: left far behind, or a relog — it begins anew.
    internal sealed class SiegeStep : ErrandStep
    {
        internal static float Radius = 18f, Gap = 25f, Seen = 80f, Abandon = 150f;
        internal static int[] Waves = { 4, 6, 5 };
        internal static float RaiderShare = 0.5f;
        // the nest's siege strength by the land's tier (Meadows … the Deep North): some 25 blows of that land's beasts
        internal static float[] HpByTier = { 300f, 500f, 800f, 1200f, 1700f, 2300f, 3000f, 3600f };
        private const string LordMark = "siegelord"; // (its lord killed once: no other one for this errand)

        private string _slot = "";
        private ErrandRun _run;
        private int _index;
        private bool _on;
        private int _wave;          // the wave that came last (0: none yet)
        private float _next, _hp, _max, _hitTold, _outTold;
        private string _lord;       // the lord's stamp while it is up

        internal static SiegeStep Active; // (the nest's hits find it: SiegeNet)

        protected override void Read() { _slot = Arg(1, ""); }
        internal override int Need { get { return Waves.Length; } }
        internal override float Wait { get { return Power.GameDaySeconds() * 0.5f; } } // (the night it waits for)
        internal override float Margin { get { return 40f; } }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            radius = Radius; at = Vector3.zero;
            string v;
            return r.Slot.TryGetValue(_slot, out v) && ErrandSteps.Point(v, out at);
        }

        private float Off(ErrandRun r, int i, Player p)
        {
            Vector3 at; float rad;
            if (p == null || !Where(r, i, out at, out rad)) return float.MaxValue;
            Vector3 d = p.transform.position - at; d.y = 0f;
            return d.magnitude - rad;
        }

        // the stake a little off the nest (not inside its model)
        private static Vector3 StakeAt(Vector3 nest) { return nest + new Vector3(4f, 0f, 0f); }

        internal override string ActLabel(ErrandRun r, int i, Player p, out string why)
        {
            why = null;
            if (_on || r.Count[i] >= Need || p == null) return "";
            Vector3 at; float rad;
            if (!Where(r, i, out at, out rad)) return "";
            if (Off(r, i, p) > 0f) why = Lang.T("у колі біля гнізда");
            else if (SiegeNet.NestAt(at) == null) why = Lang.T("біля гнізда, з якого ворони летіли до Одіна");
            else if (!EnvMan.IsNight()) why = Lang.T("уночі");
            return Lang.T("Почати облогу");
        }

        internal override string Act(ErrandRun r, int i, Player p)
        {
            Vector3 at; float rad;
            if (!Where(r, i, out at, out rad)) return Lang.T("Гнізда не видно.");
            NestView nest = SiegeNet.NestAt(at);
            if (nest == null) return Lang.T("Гнізда не видно.");
            int tier = Tier(p, at);
            _run = r; _index = i; _on = true; _wave = 0; _lord = null;
            _max = _hp = HpByTier[Mathf.Clamp(tier, 0, HpByTier.Length - 1)];
            _next = Time.time + 8f; _hitTold = 0f; _outTold = 0f;
            r.Count[i] = 0;
            Active = this;
            SiegeNet.Flag(nest, true);
            RavenMsg.Center(p, Lang.T("Облога почалася. Тримайся біля гнізда до кінця — і не дай його розламати."));
            Say(p, Lang.Keys("Вони вже йдуть. Чую, як ламають кущі.", "Тримай коло. Гніздо — наше, хай навіть не думають.", "Ну все, тепер не спи. Звірі вже нюхають гніздо."),
                   Lang.Keys("Ідуть, падлюки! Я їх чую!", "Хто полізе в моє гніздо — тому дзьобом в око!", "Ну давайте, давайте, я вже зачекався!"));
            return null;
        }

        internal override bool Tick(ErrandRun r, int i, Player p)
        {
            Vector3 at; float rad;
            if (p == null || r.Count[i] >= Need || !Where(r, i, out at, out rad)) return false;
            float off = Off(r, i, p);
            if (off < Seen) { RuneRing.Want(at, rad); RuneRing.WantStake(StakeAt(at)); }
            if (!_on) return false;
            _run = r; _index = i; Active = this;
            NestView nest = SiegeNet.NestAt(at);
            if (nest == null)
            {
                if (off > Abandon) { Stop(p, true); RavenMsg.Center(p, Lang.T("Облогу покинуто. Почни її знову, уночі, біля гнізда.")); r.Count[i] = 0; Errands.Keep(p, r); }
                return false;
            }
            SiegeNet.Flag(nest, true); // (kept fresh: an old flag of a game gone lets the nest be)
            SiegeWaves.Keep();
            bool inside = off <= 0f;
            if (!inside && Time.time > _outTold) { _outTold = Time.time + 20f; RavenMsg.Center(p, Lang.T("Ти поза колом: нова хвиля не прийде, а гніздо б'ють далі.")); }
            if (SiegeWaves.Alive() > 0 || LordUp(p)) return false;
            if (_wave > 0 && r.Count[i] < _wave)
            {
                // a wave beaten
                r.Count[i] = _wave;
                if (r.Count[i] >= Need) { Stop(p, false); RavenMsg.Center(p, Lang.T("Облогу відбито! Гніздо встояло.")); return true; }
                _next = Time.time + Gap;
                RavenMsg.Center(p, Lang.F("Хвилю відбито ({0} з {1}). Наступна вже близько.", _wave, Need));
                return true;
            }
            if (!inside || Time.time < _next) return false;
            Wave(p, r, i, at);
            return false;
        }

        // the next wave: from one side, half of it for the nest; the last with its lord
        private void Wave(Player p, ErrandRun r, int i, Vector3 at)
        {
            _wave++;
            float from = UnityEngine.Random.Range(0f, 360f);
            int tier = Tier(p, at);
            List<string> can = Champions.Plain(tier);
            int n = Waves[Mathf.Clamp(_wave - 1, 0, Waves.Length - 1)];
            int raiders = Mathf.RoundToInt(n * RaiderShare);
            for (int k = 0; k < n && can.Count > 0; k++)
            {
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(can[UnityEngine.Random.Range(0, can.Count)]) : null;
                if (prefab != null) SiegeWaves.Spawn(p, prefab, at, from, k < raiders, UnityEngine.Random.value < 0.15f ? 2 : 1);
            }
            if (_wave == Waves.Length && r.Mark(LordMark).Length == 0) Lord(p, r, i, at, from);
            Errands.Log("siege of " + r.Id + ": wave " + _wave + " of " + Waves.Length + ", " + n + " from " + Mathf.RoundToInt(from) + "°" + (_lord != null ? ", with its lord" : ""));
            string side = Side(from);
            RavenMsg.Center(p, Lang.F("Хвиля {0} з {1} — {2}!", _wave, Waves.Length, side));
            ScreamFx.CawAll(p, r.Munin, 1f, 1f);
            Say(p, Lang.Keys("Ідуть {0}. Багато.", "Дивись {0} — ось вони.", "{0}. Не проґав."), Lang.Keys("{0}, бляха! Лізуть!", "Он вони, {0}! Бий!", "{0}! Я ж казав, що прийдуть!"), side);
        }

        private void Lord(Player p, ErrandRun r, int i, Vector3 at, float from)
        {
            Vector3 pos = SiegeWaves.Place(at, from, Radius + 16f);
            Champions.Mods m = Champions.Roll(p, pos, "", true);
            if (m == null) return;
            _lord = r.Id + "@" + r.Given.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ".siege" + i;
            Character c = Champions.Ensure(p, _lord, pos, m);
            if (c == null) { _lord = null; return; }
            BaseAI ai = c.GetBaseAI();
            if (ai != null) ai.SetHuntPlayer(true);
            FightMusic.Keep();
        }

        private bool LordUp(Player p)
        {
            if (_lord == null) return false;
            if (_run != null && _run.Mark(LordMark).Length > 0) { _lord = null; FightMusic.Stop(); return false; }
            if (Champions.Find(_lord, p.GetPlayerID()) != null) { FightMusic.Keep(); return true; }
            return false; // (not here: out of the loaded world, or fallen without you — the siege goes on without it)
        }

        // still wanted: its lord, while the siege goes (Errands.Wants: a champion not wanted goes)
        internal bool Wants(string stamp) { return _on && _lord != null && _lord == stamp; }

        internal bool Killed(ErrandRun r, string stamp)
        {
            if (_lord == null || _lord != stamp) return false;
            r.SetMark(LordMark, "1");
            _lord = null;
            FightMusic.Stop();
            Errands.Keep(Player.m_localPlayer, r);
            return false; // (the wave's own end moves the count)
        }

        // a blow at the nest (SiegeNet, from whichever game drives the beast)
        internal void Hit(float damage)
        {
            Player p = Player.m_localPlayer;
            if (!_on || p == null || _run == null || damage <= 0f) return;
            _hp -= damage;
            if (_hp <= 0f)
            {
                ErrandRun r = _run;
                Stop(p, true);
                Errands.Lose(p, r, Lang.T("гніздо розламали"));
                return;
            }
            if (Time.time > _hitTold)
            {
                _hitTold = Time.time + 6f;
                int pct = Mathf.RoundToInt(_hp / Mathf.Max(1f, _max) * 100f);
                p.Message(MessageHud.MessageType.TopLeft, Lang.F("Гніздо б'ють! Тримається: {0}%", pct));
                if (pct <= 30) Say(p, Lang.Keys("Гніздо ледве тримається. Відганяй їх від нього!", "Ще трохи — і нам нема де жити."), Lang.Keys("Вони ж його розламають! Бий тих, що біля гнізда!", "МОЄ ГНІЗДО! Відганяй їх, кажу!"));
            }
        }

        // died on the siege: it begins anew (another night)
        internal override bool OnEvent(ErrandRun r, int i, Ev type, string arg, int value)
        {
            if (type != Ev.Death || !_on) return false;
            Stop(Player.m_localPlayer, true);
            r.Count[i] = 0;
            Errands.Keep(Player.m_localPlayer, r);
            if (Player.m_localPlayer != null) RavenMsg.Center(Player.m_localPlayer, Lang.T("Облогу зірвано. Почни її знову іншої ночі, біля гнізда."));
            return false;
        }

        private void Stop(Player p, bool beasts)
        {
            _on = false; _lord = null;
            if (Active == this) Active = null;
            SiegeWaves.End(beasts);
            FightMusic.Stop();
            Vector3 at; float rad;
            NestView nest = _run != null && Where(_run, _index, out at, out rad) ? SiegeNet.NestAt(at) : null;
            if (nest != null) SiegeNet.Flag(nest, false);
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            if (r.Count[i] >= Need) return Lang.T("Облогу відбито");
            if (!_on) return Lang.F("Уночі стань у рунне коло біля гнізда й почни облогу — {0} хвилі звірів, з останньою їхній ватажок. Вони битимуть і гніздо: розламають — доручення пропаде", Need);
            int pct = Mathf.RoundToInt(_hp / Mathf.Max(1f, _max) * 100f);
            string line = Lang.F("Облога: хвиля {0} з {1} · гніздо тримається: {2}%", Mathf.Max(1, _wave), Need, pct);
            return Off(r, i, p) > 0f ? line + Lang.T(" (ти поза колом)") : line;
        }

        private static int Tier(Player p, Vector3 at)
        {
            int land = WorldGenerator.instance != null ? Champions.TierOf(WorldGenerator.instance.GetBiome(at)) : 0;
            return Mathf.Min(land, Mathf.Min(Stage.Of(p), 7));
        }

        // where they come from, in a word: «зі сходу», «з півночі»… (the world's +z is north)
        private static string Side(float deg)
        {
            int k = Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 45f) % 8;
            switch (k)
            {
                case 0: return Lang.T("з півночі");
                case 1: return Lang.T("з північного сходу");
                case 2: return Lang.T("зі сходу");
                case 3: return Lang.T("з південного сходу");
                case 4: return Lang.T("з півдня");
                case 5: return Lang.T("з південного заходу");
                case 6: return Lang.T("із заходу");
                default: return Lang.T("з північного заходу");
            }
        }

        // a word from whichever raven is on you ({0}: the side)
        private void Say(Player p, string[] hugin, string[] munin, string arg = null)
        {
            if (p == null) return;
            bool m = _run != null && _run.Munin;
            if (!Shoulder.Present(p, m)) m = !m;
            if (!Shoulder.Present(p, m)) return;
            string[] from = m ? munin : hugin;
            string line = Lang.T(from[UnityEngine.Random.Range(0, from.Length)]);
            if (arg != null) line = string.Format(line, arg);
            RavenSkills.SayFromShoulder(p, m, line.Length > 0 ? char.ToUpper(line[0]) + line.Substring(1) : line);
        }
    }

    // The siege's beasts: ordinary creatures of the nest's land, come from one side; the raiders (half) go for the nest
    // (SiegeTargetPatch), the rest for you. No loot, no kill counted (BindArena.Key), never saved.
    internal static class SiegeWaves
    {
        internal const string RaidKey = "RavenWatch_siegeNest"; // a raider's ZDO: the nest it goes for
        private static readonly List<Character> AliveList = new List<Character>();
        private static bool _wanted, _on;

        internal static int Alive() { AliveList.RemoveAll(c => c == null || c.IsDead()); return AliveList.Count; }
        internal static void Keep() { _wanted = true; _on = true; }

        // a place on dry land on that side of the nest, this far out
        internal static Vector3 Place(Vector3 at, float from, float dist)
        {
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            Vector3 best = at;
            for (int tries = 0; tries < 12; tries++)
            {
                float a = (from + UnityEngine.Random.Range(-35f, 35f)) * Mathf.Deg2Rad, d = dist + UnityEngine.Random.Range(-3f, 6f);
                Vector3 pos = at + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * d;
                if (ZoneSystem.instance == null) return pos;
                Vector3 normal; Heightmap.Biome biome; Heightmap.BiomeArea area; Heightmap hmap;
                ZoneSystem.instance.GetGroundData(ref pos, out normal, out biome, out area, out hmap);
                if (pos.y < sea + 0.5f) continue; // (never in the water)
                if (hmap != null && WorldGenerator.IsAshlands(pos.x, pos.z) && hmap.GetLava(pos) > 0.05f) continue;
                return pos + Vector3.up * 0.5f;
            }
            return best;
        }

        internal static void Spawn(Player p, GameObject prefab, Vector3 nest, float from, bool raider, int level)
        {
            Vector3 pos = Place(nest, from, SiegeStep.Radius + UnityEngine.Random.Range(10f, 18f));
            GameObject go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.Euler(0f, from + 180f, 0f));
            Character c = go.GetComponent<Character>();
            ZNetView nv = go.GetComponent<ZNetView>();
            if (c == null || nv == null || !nv.IsValid()) { UnityEngine.Object.Destroy(go); return; }
            nv.GetZDO().Set(BindArena.Key, p.GetPlayerID()); // (no loot, no kill credit — as the arena's)
            nv.GetZDO().Persistent = false;                    // never saved: one who quits leaves none behind
            if (raider) nv.GetZDO().Set(RaidKey, nest);
            if (level > 1) c.SetLevel(level);
            BaseAI ai = c.GetBaseAI();
            if (ai != null) { ai.SetHuntPlayer(!raider); ai.Alert(); }
            AliveList.Add(c);
        }

        // before the steps' tick (Errands.Tick): a siege no step kept going is over
        internal static void Late()
        {
            if (!_wanted && _on) End(true);
            _wanted = false;
        }

        internal static void End(bool beasts)
        {
            _on = false;
            if (beasts)
                foreach (Character c in AliveList)
                {
                    ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
                    if (nv != null && nv.IsValid() && !c.IsDead()) { if (!nv.IsOwner()) nv.ClaimOwnership(); ZNetScene.instance.Destroy(c.gameObject); }
                }
            AliveList.Clear();
        }
    }

    // The nest under siege, for every game: the siege's owner tells everyone where and till when (again every few
    // seconds while it goes, so one who comes later knows too), so a beast driven by any game strikes the siege's
    // strength instead of the building, and the owner is told. Not on the nest's own ZDO: Home keeps the ravens' data there.
    internal static class SiegeNet
    {
        private const string RpcHit = "RavenWatch_SiegeHit", RpcOn = "RavenWatch_SiegeOn";
        private const float Fresh = 30f;
        private struct Mark { public Vector3 At; public double Until; }
        private static readonly Dictionary<long, Mark> Sieges = new Dictionary<long, Mark>(); // owner (session) → its nest

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<float>(RpcHit, new Action<long, float>(OnHit));
            ZRoutedRpc.instance.Register<Vector3, bool>(RpcOn, new Action<long, Vector3, bool>(OnOn));
        }

        private static double Now { get { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time; } }

        private static void OnOn(long sender, Vector3 at, bool on)
        {
            if (!on) { Sieges.Remove(sender); return; }
            if (Sieges.Count > 16) Sieges.Clear();
            Sieges[sender] = new Mark { At = at, Until = Now + Fresh };
        }

        // the nest at this place, loaded here (null: none)
        internal static NestView NestAt(Vector3 at)
        {
            NestView best = null; float bd = 6f * 6f;
            foreach (NestView v in NestView.All)
            {
                if (v == null || !v.Valid) continue;
                Vector3 d = v.transform.position - at; d.y = 0f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = v; }
            }
            return best;
        }

        private static float _flagAt;
        internal static void Flag(NestView nest, bool on)
        {
            if (nest == null || ZRoutedRpc.instance == null) return;
            if (on && Time.time < _flagAt) return; // (every few seconds is fresh enough)
            _flagAt = on ? Time.time + 5f : 0f;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcOn, nest.transform.position, on);
        }

        // whose siege this building is under (0: none, or not a nest)
        internal static long By(WearNTear w)
        {
            if (w == null || Sieges.Count == 0 || w.GetComponent<NestView>() == null) return 0L;
            Vector3 at = w.transform.position;
            double now = Now;
            foreach (KeyValuePair<long, Mark> kv in Sieges)
                if (kv.Value.Until > now && (kv.Value.At - at).sqrMagnitude < 4f) return kv.Key;
            return 0L;
        }

        internal static void Hit(long owner, float damage)
        {
            if (owner == ZDOMan.GetSessionID()) { OnHit(owner, damage); return; }
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(owner, RpcHit, damage);
        }

        private static void OnHit(long sender, float damage)
        {
            if (SiegeStep.Active != null) SiegeStep.Active.Hit(Mathf.Clamp(damage, 0f, 5000f));
        }

        // ---- the raiders' aim: the nest, unless someone is at their throat
        private static readonly Dictionary<MonsterAI, Vector3> Raiders = new Dictionary<MonsterAI, Vector3>();
        private static readonly HashSet<MonsterAI> Plain = new HashSet<MonsterAI>();

        internal static bool RaiderOf(MonsterAI ai, out Vector3 nest)
        {
            nest = Vector3.zero;
            if (Plain.Contains(ai)) return false;
            if (Raiders.TryGetValue(ai, out nest)) return true;
            if (Raiders.Count + Plain.Count > 512) { Raiders.Clear(); Plain.Clear(); }
            ZNetView nv = ai.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return false; // (not yet: asked again)
            nest = nv.GetZDO().GetVec3(SiegeWaves.RaidKey, Vector3.zero);
            if (nest == Vector3.zero) { Plain.Add(ai); return false; }
            Raiders[ai] = nest;
            return true;
        }

        private static Vector3 _targetFor;
        private static StaticTarget _target;
        private static float _targetAt;
        internal static StaticTarget TargetAt(Vector3 at)
        {
            if (_target != null && Time.time - _targetAt < 1f && (at - _targetFor).sqrMagnitude < 1f) return _target;
            NestView v = NestAt(at);
            _target = v != null ? v.GetComponent<StaticTarget>() : null;
            _targetFor = at; _targetAt = Time.time;
            return _target;
        }
    }

    [HarmonyPatch(typeof(MonsterAI), "UpdateTarget")]
    internal static class SiegeTargetPatch
    {
        private static readonly FieldInfo Creature = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");
        private static readonly FieldInfo Static = AccessTools.Field(typeof(MonsterAI), "m_targetStatic");

        private static void Postfix(MonsterAI __instance)
        {
            Vector3 nest;
            if (Creature == null || Static == null || !SiegeNet.RaiderOf(__instance, out nest)) return;
            Character c = Creature.GetValue(__instance) as Character;
            if (c != null && Vector3.Distance(c.transform.position, __instance.transform.position) < 6f) return; // (someone at its throat: it fights back)
            StaticTarget t = SiegeNet.TargetAt(nest);
            if (t == null) return;
            Creature.SetValue(__instance, null);
            Static.SetValue(__instance, t);
        }
    }

    // a beast's blow at a nest under siege: the siege's strength takes it, not the building
    [HarmonyPatch(typeof(WearNTear), "Damage")]
    internal static class SiegeNestHitPatch
    {
        private static bool Prefix(WearNTear __instance, HitData hit)
        {
            if (hit == null) return true;
            Character a = hit.GetAttacker();
            if (a == null || a.IsPlayer()) return true;
            long by = SiegeNet.By(__instance);
            if (by == 0L) return true;
            if (__instance.m_hitEffect != null) __instance.m_hitEffect.Create(hit.m_point, Quaternion.identity, __instance.transform);
            SiegeNet.Hit(by, hit.GetTotalDamage());
            return false;
        }
    }
}
#endif
