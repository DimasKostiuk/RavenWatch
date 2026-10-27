#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The raids' fate for Hugin's «Віщування», kept by the server (the game rolls raids there). The game rolls only when
    // its timer runs out, and only then — so Hugin could not know. Here the roll of the next raid is made ahead, the
    // first time a player's Hugin asks within a day of it (the very roll the game would make: its chance, one of the
    // raids possible now), and when the timer runs out that fate is what happens, not a new roll. Nobody asks: the
    // game rolls as ever.
    //   - «відвести»: the timer goes back a day; the fated raid stays fated and comes then for sure, grown (its
    //     creatures may come with more stars: every game with the mod makes them so, told by RpcGrown);
    //   - «накликати»: the fated raid starts now at the asker, if it could happen there (a base, its land, the keys).
    // Lost on a server restart (the game's own roll then). A world with raids off (world modifiers) has no fate.
    internal static class OmenFate
    {
        internal const int Off = 0, Calm = 1, Raid = 2, Now = 3;
        internal static float GrownLevelChance = 35f; // % a grown raid's creature rises a star (each step)

        private const string RpcAsk = "RavenWatch_OmenAsk", RpcTell = "RavenWatch_OmenTell", RpcDo = "RavenWatch_OmenDo", RpcDone = "RavenWatch_OmenDone", RpcGrown = "RavenWatch_OmenGrown";
        private static readonly FieldInfo TimerField = AccessTools.Field(typeof(RandEventSystem), "m_eventTimer");
        private static readonly MethodInfo Possible = AccessTools.Method(typeof(RandEventSystem), "GetPossibleRandomEvents");

        // the server's fate for the next roll
        private static bool _decided, _grown;
        private static string _event; // null: the roll comes to nothing

        // ---------------------------------------------------------------- what an answer is

        internal sealed class Answer
        {
            public int State;           // Off, Calm, Raid, Now
            public string Event = "";   // the raid's name (the game's: army_eikthyr…)
            public bool Grown;          // turned aside once: comes back stronger, can't be turned aside again
            public float Left;          // seconds till the roll (the game's own clock: while the server runs)
            public string SummonWhy = ""; // "" — may be called now, here; else why not (a code)
            public int Players;
        }

        internal sealed class Pending { public Action<Answer> Done; public Action<bool, string> DoneDo; public float Until; }
        private static readonly Dictionary<int, Pending> Waiting = new Dictionary<int, Pending>();
        private static int _next = 1;
        internal static float Wait = 5f;

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcAsk, new Action<long, ZPackage>(OnAsk));
            ZRoutedRpc.instance.Register<ZPackage>(RpcTell, new Action<long, ZPackage>(OnTell));
            ZRoutedRpc.instance.Register<ZPackage>(RpcDo, new Action<long, ZPackage>(OnDo));
            ZRoutedRpc.instance.Register<ZPackage>(RpcDone, new Action<long, ZPackage>(OnDone));
            ZRoutedRpc.instance.Register<ZPackage>(RpcGrown, new Action<long, ZPackage>(OnGrown));
        }

        // ---------------------------------------------------------------- the asker's side

        // what the next raid is (done(null): no RavenWatch on the server, or no answer)
        internal static void Ask(Action<Answer> done)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer()) { done(Tell(ZNet.GetUID())); return; }
            ZNetPeer server = ZNet.instance != null ? ZNet.instance.GetServerPeer() : null;
            if (ZRoutedRpc.instance == null || server == null) { done(null); return; }
            int id = _next++;
            Waiting[id] = new Pending { Done = done, Until = Time.time + Wait };
            ZPackage pkg = new ZPackage();
            pkg.Write(id);
            ZRoutedRpc.instance.InvokeRoutedRPC(server.m_uid, RpcAsk, pkg);
        }

        // turn the raid aside (1) or call it now (2); done(ok, why)
        internal static void Do(int action, Action<bool, string> done)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer()) { string why = Act(ZNet.GetUID(), action); done(why == "", why); return; }
            ZNetPeer server = ZNet.instance != null ? ZNet.instance.GetServerPeer() : null;
            if (ZRoutedRpc.instance == null || server == null) { done(false, "noserver"); return; }
            int id = _next++;
            Waiting[id] = new Pending { DoneDo = done, Until = Time.time + Wait };
            ZPackage pkg = new ZPackage();
            pkg.Write(id);
            pkg.Write(action);
            ZRoutedRpc.instance.InvokeRoutedRPC(server.m_uid, RpcDo, pkg);
        }

        internal static void Tick()
        {
            if (Waiting.Count == 0) return;
            List<int> late = null;
            foreach (KeyValuePair<int, Pending> kv in Waiting) if (Time.time > kv.Value.Until) { if (late == null) late = new List<int>(); late.Add(kv.Key); }
            if (late == null) return;
            foreach (int id in late)
            {
                Pending p = Waiting[id];
                Waiting.Remove(id);
                if (Plugin.Instance != null) Plugin.Instance.Log("Omen: the server did not answer (no RavenWatch on it?)");
                try { if (p.Done != null) p.Done(null); else if (p.DoneDo != null) p.DoneDo(false, "noserver"); } catch (Exception e) { PatchGuard.Fail("OmenFate.Tick", e); }
            }
        }

        private static void OnTell(long sender, ZPackage pkg)
        {
            ZNetPeer server = ZNet.instance != null ? ZNet.instance.GetServerPeer() : null;
            if (server == null || sender != server.m_uid) return;
            int id = pkg.ReadInt();
            Answer a = new Answer { State = pkg.ReadInt(), Event = pkg.ReadString(), Grown = pkg.ReadBool(), Left = pkg.ReadSingle(), SummonWhy = pkg.ReadString(), Players = pkg.ReadInt() };
            Pending p;
            if (!Waiting.TryGetValue(id, out p) || p.Done == null) return;
            Waiting.Remove(id);
            p.Done(a);
        }

        private static void OnDone(long sender, ZPackage pkg)
        {
            ZNetPeer server = ZNet.instance != null ? ZNet.instance.GetServerPeer() : null;
            if (server == null || sender != server.m_uid) return;
            int id = pkg.ReadInt();
            string why = pkg.ReadString();
            Pending p;
            if (!Waiting.TryGetValue(id, out p) || p.DoneDo == null) return;
            Waiting.Remove(id);
            p.DoneDo(why == "", why);
        }

        // ---------------------------------------------------------------- the server's side

        private static void OnAsk(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            int id = pkg.ReadInt();
            Answer a = Tell(sender);
            ZPackage back = new ZPackage();
            back.Write(id);
            back.Write(a.State); back.Write(a.Event ?? ""); back.Write(a.Grown); back.Write(a.Left); back.Write(a.SummonWhy ?? ""); back.Write(a.Players);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcTell, back);
        }

        private static void OnDo(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            int id = pkg.ReadInt(), action = pkg.ReadInt();
            ZPackage back = new ZPackage();
            back.Write(id);
            back.Write(Act(sender, action));
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcDone, back);
        }

        private static float Threshold(RandEventSystem rs) { return rs.m_eventIntervalMin * 60f * Game.m_eventRate; }
        private static float Timer(RandEventSystem rs) { return TimerField != null ? (float)TimerField.GetValue(rs) : 0f; }
        private static float DayLength { get { return EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f; } }

        // the next raid as the server sees it (fated now, if it comes within a day)
        private static Answer Tell(long asker)
        {
            Answer a = new Answer();
            a.Players = ZNet.instance != null ? Mathf.Max(1, ZNet.instance.GetNrOfPlayers()) : 1;
            RandEventSystem rs = RandEventSystem.instance;
            if (rs == null || TimerField == null || Game.m_eventRate <= 0f) { a.State = Off; return a; }
            RandomEvent on = rs.GetCurrentRandomEvent();
            if (on != null) { a.State = Now; a.Event = on.m_name; return a; }
            a.Left = Mathf.Max(0f, Threshold(rs) - Timer(rs));
            if (!_decided && a.Left > DayLength) { a.State = Calm; return a; } // no roll before the next morning
            if (!_decided) Decide(rs);
            if (_event == null) { a.State = Calm; return a; }
            a.State = Raid; a.Event = _event; a.Grown = _grown;
            RandomEvent ev = EventNamed(rs, _event);
            a.SummonWhy = ev == null ? "none" : WhyNot(ev, asker);
            return a;
        }

        // the game's own roll, made ahead: its chance, then one of the raids possible now
        private static void Decide(RandEventSystem rs)
        {
            _decided = true; _grown = false; _event = null;
            if (UnityEngine.Random.Range(0f, 100f) > rs.m_eventChance / Game.m_eventRate) { Log("fate: the next roll comes to nothing"); return; }
            List<KeyValuePair<RandomEvent, Vector3>> list = PossibleNow(rs);
            if (list == null || list.Count == 0) { Log("fate: nothing possible now"); return; }
            _event = list[UnityEngine.Random.Range(0, list.Count)].Key.m_name;
            Log("fate: " + _event);
        }

        private static List<KeyValuePair<RandomEvent, Vector3>> PossibleNow(RandEventSystem rs)
        {
            try { return Possible != null ? Possible.Invoke(rs, null) as List<KeyValuePair<RandomEvent, Vector3>> : null; }
            catch (Exception e) { Log("possible raids: " + e.Message); return null; }
        }

        private static RandomEvent EventNamed(RandEventSystem rs, string name)
        {
            foreach (RandomEvent e in rs.m_events) if (e != null && e.m_enabled && e.m_name == name) return e;
            return null;
        }

        // "" — that raid could start at the asker now; else why not: "nobase", "land", "keys", "inside", "noplayer"
        private static string WhyNot(RandomEvent ev, long asker)
        {
            Dictionary<string, string> data; Vector3 at;
            if (!PlayerData(asker, out data, out at)) return "noplayer";
            if (at.y > 3000f) return "inside";
            string possible, baseRaw;
            int baseValue;
            if (!data.TryGetValue("possibleEvents", out possible) || !data.TryGetValue("baseValue", out baseRaw) || !int.TryParse(baseRaw, out baseValue)) return "noplayer";
            bool playerKeys = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.PlayerEvents);
            if (playerKeys && Array.IndexOf(possible.Split(','), ev.m_name) < 0) return "keys";
            if (!playerKeys)
            {
                foreach (string k in ev.m_requiredGlobalKeys) if (!ZoneSystem.instance.GetGlobalKey(k)) return "keys";
                foreach (string k in ev.m_notRequiredGlobalKeys) if (ZoneSystem.instance.GetGlobalKey(k)) return "keys";
            }
            if (ev.m_biome != Heightmap.Biome.None && WorldGenerator.instance != null && (WorldGenerator.instance.GetBiome(at) & ev.m_biome) == 0) return "land";
            if (ev.m_nearBaseOnly && baseValue < 3) return "nobase";
            return "";
        }

        private static bool PlayerData(long peer, out Dictionary<string, string> data, out Vector3 at)
        {
            data = null; at = Vector3.zero;
            if (ZNet.instance == null) return false;
            if (peer == ZNet.GetUID()) { data = ZNet.instance.m_serverSyncedPlayerData; at = ZNet.instance.GetReferencePosition(); return data != null; }
            ZNetPeer p = ZNet.instance.GetPeer(peer);
            if (p == null || !p.IsReady()) return false;
            data = p.m_serverSyncedPlayerData; at = p.m_refPos;
            return data != null;
        }

        // 1: turn it aside a day; 2: call it now at the asker. "" — done; else why not
        private static string Act(long asker, int action)
        {
            RandEventSystem rs = RandEventSystem.instance;
            if (rs == null || TimerField == null || Game.m_eventRate <= 0f) return "off";
            if (rs.GetCurrentRandomEvent() != null) return "busy";
            if (!_decided || _event == null) return "none";
            RandomEvent ev = EventNamed(rs, _event);
            if (ev == null) return "none";
            if (action == 1)
            {
                if (_grown) return "twice";
                TimerField.SetValue(rs, Timer(rs) - DayLength);
                _grown = true;
                Log("fate: " + _event + " turned aside a day, comes back grown");
                return "";
            }
            if (action == 2)
            {
                string why = WhyNot(ev, asker);
                if (why != "") return why;
                Dictionary<string, string> data; Vector3 at;
                PlayerData(asker, out data, out at);
                bool grown = _grown;
                string name = _event;
                _decided = false; _grown = false; _event = null;
                TimerField.SetValue(rs, 0f); // (the game's next roll counts from now)
                if (grown) SendGrown(name);
                rs.SetRandomEventByName(name, at);
                Log("fate: " + name + " called now" + (grown ? " (grown)" : ""));
                return "";
            }
            return "off";
        }

        // From the patch on the game's roll (server): the timer is out — the fate happens instead of a new roll
        internal static bool Roll(RandEventSystem rs, float dt)
        {
            if (!_decided || TimerField == null || Game.m_eventRate <= 0f) return false;
            if (Timer(rs) + dt <= Threshold(rs)) return false;
            TimerField.SetValue(rs, 0f); // (the game adds dt and finds nothing to roll)
            string name = _event;
            bool grown = _grown;
            _decided = false; _grown = false; _event = null;
            if (name == null) { Log("fate kept: no raid"); return true; }
            foreach (KeyValuePair<RandomEvent, Vector3> kv in PossibleNow(rs) ?? new List<KeyValuePair<RandomEvent, Vector3>>())
            {
                if (kv.Key.m_name != name) continue;
                if (grown) SendGrown(name);
                rs.SetRandomEventByName(name, kv.Value);
                Log("fate kept: " + name + (grown ? " (grown)" : ""));
                return true;
            }
            Log("fate: " + name + " cannot happen now (nobody where it could) — no raid");
            return true;
        }

        // ---------------------------------------------------------------- a grown raid (every game)

        private static string _grownName;
        private static float _grownUntil;
        private static RandomEvent _bumped;

        private static void SendGrown(string name)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(name);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcGrown, pkg);
        }

        private static void OnGrown(long sender, ZPackage pkg)
        {
            _grownName = pkg.ReadString();
            _grownUntil = Time.time + 1800f; // (a raid lasts minutes; long enough for it, never for the next one)
        }

        // the raid running here is the grown one: its creatures come with more stars (spawned by whichever game runs
        // the zone, so every game makes its own copy of the raid so)
        internal static void TickGrown()
        {
            if (_grownName == null) return;
            if (Time.time > _grownUntil) { _grownName = null; _bumped = null; return; }
            RandomEvent re = RandEventSystem.instance != null ? RandEventSystem.instance.GetCurrentRandomEvent() : null;
            if (re == null || re.m_name != _grownName || ReferenceEquals(re, _bumped)) return;
            _bumped = re;
            foreach (SpawnSystem.SpawnData sd in re.m_spawn)
            {
                if (sd == null) continue;
                sd.m_maxLevel = Mathf.Min(3, Mathf.Max(sd.m_maxLevel, sd.m_minLevel) + 1);
                sd.m_overrideLevelupChance = Mathf.Max(sd.m_overrideLevelupChance, GrownLevelChance);
                sd.m_levelUpMinCenterDistance = 0f;
            }
            Log("the grown raid " + re.m_name + " is here: its creatures come stronger");
        }

        internal static void Reset() { _decided = false; _grown = false; _event = null; _grownName = null; _bumped = null; Waiting.Clear(); }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Omen: " + s); }
    }

    // the game's raid roll (server): a fated one happens instead
    [HarmonyPatch(typeof(RandEventSystem), "UpdateRandomEvent")]
    internal static class OmenRollPatch
    {
        private static void Prefix(RandEventSystem __instance, float dt)
        { try {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            OmenFate.Roll(__instance, dt);
        } catch (Exception e) { PatchGuard.Fail("OmenRollPatch", e); } }
    }
}
#endif
