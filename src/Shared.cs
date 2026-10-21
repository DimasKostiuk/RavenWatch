using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Shared ravens: other players nearby see a copy of the raven that landed next to someone,
    // with its words above it. The real raven stays local to its player, exactly as before;
    // when the setting is off, nothing here runs and nothing is sent.
    internal static class Shared
    {
        internal const string RpcShow = "RavenWatch_ShareShow";
        internal const string RpcSay = "RavenWatch_ShareSay";
        internal const string RpcHide = "RavenWatch_ShareHide";
        internal const string RpcAlive = "RavenWatch_ShareAlive";
        private const float AliveTimeout = 15f; // no "still here" for this long: the owner's raven is gone

        private const float ShowRange = 100f;       // only players this close create the copy
        private const float ChatterSeconds = 9f;    // how often the copy repeats its title
        private const float MaxLifeSeconds = 900f;  // a copy never lingers longer than this
        private const int MaxGhosts = 8;

        internal static bool Enabled;
        internal static bool MapPin = true;

        private sealed class Ghost
        {
            public long Owner;
            public string OwnerName;
            public bool Munin;
            public string Title;
            public Vector3 Pos;
            public GameObject Go;
            public Animator Anim;
            public float Born;
            public float NextChatter;
            public bool Talked;
            public float DieAt = -1f;
            public Minimap.PinData Pin;
            public int LandTries;
            public float NextLandCheck;
            public float LastAlive;
            public bool Heartbeat; // the owner sends "still here"; owners on older versions never do
        }

        private static readonly List<Ghost> Ghosts = new List<Ghost>();
        private static float _nextOwnerCheck;

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcShow, new Action<long, ZPackage>(OnShow));
            ZRoutedRpc.instance.Register<ZPackage>(RpcSay, new Action<long, ZPackage>(OnSay));
            ZRoutedRpc.instance.Register<ZPackage>(RpcHide, new Action<long, ZPackage>(OnHide));
            ZRoutedRpc.instance.Register<ZPackage>(RpcAlive, new Action<long, ZPackage>(OnAlive));
        }

        internal static void Reset()
        {
            foreach (Ghost g in Ghosts) Destroy(g);
            Ghosts.Clear();
        }

        // ---------------------------------------------------------------- the owner tells the others

        // the game tries to land a raven again every second while it cannot (seen: 87 tries in a row); one landing is
        // told once, not every try, or each try made the others a fresh copy and a fresh map mark
        private static string _shownKey, _said;
        private static float _saidAt = -999f;
        private static float _shownAt = -999f;
        internal static bool Shown { get { return _shownKey != null; } }

        internal static void BroadcastShow(Raven raven, Raven.RavenText text)
        {
            if (!Enabled || ZRoutedRpc.instance == null || Player.m_localPlayer == null) return;
            string key = text.m_key + (raven.m_isMunin ? "|m" : "|h");
            if (key == _shownKey && Time.time - _shownAt < 60f) return;
            _shownKey = key; _shownAt = Time.time;
            ZPackage p = new ZPackage();
            p.Write(Player.m_localPlayer.GetPlayerName());
            p.Write(raven.m_isMunin);
            p.Write(raven.transform.position);
            p.Write(raven.transform.rotation);
            p.Write(text.m_topic ?? "");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcShow, p);
        }

        internal static void BroadcastSay(Raven.RavenText text)
        {
            if (!Enabled || ZRoutedRpc.instance == null) return;
            // talking to the raven again and again (E held, E spammed) repeats its line: the others get it once, not a
            // chat line per press
            string said = (text.m_topic ?? "") + "\n" + (text.m_text ?? "");
            if (said == _said && Time.time - _saidAt < 20f) return;
            _said = said; _saidAt = Time.time;
            ZPackage p = new ZPackage();
            p.Write(text.m_topic ?? "");
            p.Write(text.m_text ?? "");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcSay, p);
        }

        internal static void BroadcastHide()
        {
            _shownKey = null;
            if (!Enabled || ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcHide, new ZPackage());
        }

        // Every few seconds while the owner's raven really sits there. A lost or reordered "hide" (a raven sent off
        // and called back again and again) once left copies standing for everyone but their owner; now they go
        // as soon as the "still here" stops.
        internal static void BroadcastAlive()
        {
            if (!Enabled || ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcAlive, new ZPackage());
        }

        private static void OnAlive(long sender, ZPackage p)
        {
            if (!Enabled) return;
            Ghost g = Find(sender);
            if (g == null) return;
            g.LastAlive = Time.time;
            g.Heartbeat = true;
        }

        // ---------------------------------------------------------------- the others draw a copy

        private static void OnShow(long sender, ZPackage p)
        {
            string ownerName = p.ReadString();
            bool munin = p.ReadBool();
            Vector3 pos = p.ReadVector3();
            Quaternion rot = p.ReadQuaternion();
            string title = p.ReadString();
            if (!Enabled || sender == ZDOMan.GetSessionID() || Player.m_localPlayer == null) return;
            // the same raven told again (an older owner re-tells every landing try): the copy and its mark stay as they
            // are, and what was learned about its "still here" is kept
            Ghost same = Find(sender);
            if (same != null && same.Munin == munin && same.DieAt < 0f && Vector3.Distance(same.Pos, pos) < 30f) { same.LastAlive = Time.time; same.Title = title; return; }

            Remove(sender, false);
            if (Ghosts.Count >= MaxGhosts) Remove(Ghosts[0].Owner, false);

            Ghost g = new Ghost();
            g.Owner = sender;
            g.OwnerName = ownerName;
            g.Munin = munin;
            g.Title = title;
            g.Pos = pos;
            g.Born = Time.time;
            g.LastAlive = Time.time;
            g.NextChatter = Time.time + 2f;
            if (MapPin && Minimap.instance != null)
                g.Pin = Minimap.instance.AddPin(pos, Minimap.PinType.Icon3, Label(g), false, false);
            Ghosts.Add(g);

            if (Vector3.Distance(Player.m_localPlayer.transform.position, pos) > ShowRange) return; // far away: just the pin

            Raven source = FindRaven(munin);
            if (source == null || source.m_visual == null) return;

            GameObject go = UnityEngine.Object.Instantiate(source.m_visual, pos, rot);
            go.name = "RavenWatch_SharedRaven";
            go.SetActive(true);
            AppearMute.Quiet(go);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            RemoveExclamation(source, go);
            DumpOnce(source, go);

            g.Go = go;
            g.Anim = go.GetComponentInChildren<Animator>();
            if (g.Anim != null)
            {
                g.Anim.enabled = true;
                g.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                g.Anim.Rebind();
                g.Anim.Update(0f);
                AppearMute.Allow(go, 5f); // it flies in as the game's ravens do: with its appearing
                g.Anim.SetTrigger("flyin");
            }
            g.NextLandCheck = Time.time + 1.5f;
        }

        private static void OnSay(long sender, ZPackage p)
        {
            string topic = p.ReadString();
            string text = p.ReadString();
            if (!Enabled) return;
            Ghost g = Find(sender);
            if (g == null || Chat.instance == null) return;
            g.Talked = true;
            Player me = Player.m_localPlayer;
            if (me != null && Vector3.Distance(me.transform.position, g.Pos) <= ShowRange) ToChat(g, topic, text);
            if (g.Go == null) return;
            string body = string.IsNullOrEmpty(topic) ? text : string.IsNullOrEmpty(text) ? topic : "<color=orange>" + topic + "</color>\n" + text;
            Chat.instance.SetNpcText(g.Go, Vector3.up * TextOffset(g), 20f, 20f, Label(g), body, true);
            if (g.Anim != null) g.Anim.SetTrigger("talk");
        }

        private static void OnHide(long sender, ZPackage p)
        {
            if (!Enabled) return;
            Remove(sender, true);
        }

        // called from the plugin's Update while copies exist
        internal static void Update()
        {
            if (Ghosts.Count == 0) return;
            Player me = Player.m_localPlayer;
            if (Time.time >= _nextOwnerCheck) { _nextOwnerCheck = Time.time + 5f; DropLeftOwners(); }
            for (int i = Ghosts.Count - 1; i >= 0; i--)
            {
                Ghost g = Ghosts[i];
                if (Time.time - g.Born > MaxLifeSeconds || (g.DieAt > 0f && Time.time >= g.DieAt) || (g.Heartbeat && Time.time - g.LastAlive > AliveTimeout))
                {
                    Destroy(g);
                    Ghosts.RemoveAt(i);
                    continue;
                }
                if (g.Go == null) continue; // a pin only, or the copy is already gone
                if (me != null && Vector3.Distance(me.transform.position, g.Go.transform.position) > ShowRange * 1.5f)
                {
                    DestroyCopy(g);
                    continue;
                }
                if (g.DieAt < 0f && g.Anim != null && g.LandTries < 3 && Time.time >= g.NextLandCheck)
                {
                    g.NextLandCheck = Time.time + 1.5f;
                    if (InTag(g.Anim, "away"))
                    {
                        g.LandTries++;
                        g.Anim.SetTrigger(g.LandTries >= 2 ? "teleportin" : "flyin");
                        Plugin.Instance.Log("Shared raven copy still away, landing try " + g.LandTries);
                    }
                    else g.LandTries = 3; // visible or on its way: done
                }
                if (g.DieAt < 0f && !g.Talked && Time.time >= g.NextChatter && Chat.instance != null && !string.IsNullOrEmpty(g.Title))
                {
                    g.NextChatter = Time.time + ChatterSeconds;
                    Chat.instance.SetNpcText(g.Go, Vector3.up * TextOffset(g), 20f, 6f, Label(g), g.Title, false);
                }
            }
        }

        // the owner left the game while the raven sat there: take the copy and the pin away
        private static void DropLeftOwners()
        {
            if (ZNet.instance == null) return;
            HashSet<long> online = new HashSet<long>();
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList()) online.Add(info.m_characterID.UserID);
            for (int i = Ghosts.Count - 1; i >= 0; i--)
            {
                if (online.Contains(Ghosts[i].Owner)) continue;
                Destroy(Ghosts[i]);
                Ghosts.RemoveAt(i);
            }
        }

        // ---------------------------------------------------------------- helpers

        // the words of a raven nearby also land in the chat, so nobody misses them
        private static void ToChat(Ghost g, string topic, string text)
        {
            string line = string.IsNullOrEmpty(text) ? topic : (string.IsNullOrEmpty(topic) ? "" : topic + " — ") + text;
            line = line.Replace("\r", "").Replace("\n", " ");
            string color = g.Munin ? Style.MuninTitle : Style.HuginTitle;
            Chat.instance.AddString("<color=" + color + ">" + Label(g) + "</color>", line, Talker.Type.Normal, false);
            Traverse.Create(Chat.instance).Field("m_hideTimer").SetValue(0f); // show the chat window for a moment
        }

        internal static bool IsCopy(GameObject go, out bool munin)
        {
            munin = false;
            if (go == null) return false;
            foreach (Ghost g in Ghosts)
            {
                if (g.Go != go) continue;
                munin = g.Munin;
                return true;
            }
            return false;
        }

        private static Ghost Find(long owner)
        {
            foreach (Ghost g in Ghosts) if (g.Owner == owner) return g;
            return null;
        }

        private static void Remove(long owner, bool flyAway)
        {
            Ghost g = Find(owner);
            if (g == null) return;
            if (flyAway && g.Go != null && g.Anim != null)
            {
                g.Anim.SetTrigger("flyaway");
                if (Chat.instance != null) Chat.instance.ClearNpcText(g.Go);
                RemovePin(g);
                g.DieAt = Time.time + 3f; // let the take-off play, then it goes
                return;
            }
            Destroy(g);
            Ghosts.Remove(g);
        }

        private static void Destroy(Ghost g)
        {
            DestroyCopy(g);
            RemovePin(g);
        }

        private static void DestroyCopy(Ghost g)
        {
            if (g.Go == null) return;
            if (Chat.instance != null) Chat.instance.ClearNpcText(g.Go);
            UnityEngine.Object.Destroy(g.Go);
            g.Go = null;
            g.Anim = null;
        }

        private static void RemovePin(Ghost g)
        {
            if (g.Pin != null && Minimap.instance != null) Minimap.instance.RemovePin(g.Pin);
            g.Pin = null;
        }

        // nobody can talk to the copy, so it must never show the "talk to me" mark.
        // Destroyed, not hidden: the raven's animator switches it back on otherwise.
        internal static void RemoveExclamation(Raven source, GameObject copy)
        {
            string exName = source.m_exclamation != null ? source.m_exclamation.name : null;
            List<GameObject> doomed = new List<GameObject>();
            if (exName != null && source.m_exclamation.transform.IsChildOf(source.m_visual.transform))
            {
                Transform t = copy.transform.Find(RelativePath(source.m_visual.transform, source.m_exclamation.transform));
                if (t != null) doomed.Add(t.gameObject);
            }
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
            {
                if (t == copy.transform) continue;
                if ((exName != null && t.name == exName) || t.name.IndexOf("exclam", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("exclem", StringComparison.OrdinalIgnoreCase) >= 0)
                    doomed.Add(t.gameObject);
            }
            foreach (GameObject d in doomed) { d.SetActive(false); UnityEngine.Object.Destroy(d); }
        }

        private static bool InTag(Animator a, string tag)
        {
            if (a == null || !a.isInitialized) return false;
            return a.GetCurrentAnimatorStateInfo(0).IsTag(tag) || a.GetNextAnimatorStateInfo(0).IsTag(tag);
        }

        // one look at how the raven model is built, written to the log the first time a copy is made
        private static bool _dumped;
        private static void DumpOnce(Raven source, GameObject copy)
        {
            if (_dumped || Plugin.Instance == null) return;
            _dumped = true;
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("Shared raven copy layout. exclamation=");
                sb.Append(source.m_exclamation != null ? RelativePath(source.transform, source.m_exclamation.transform) : "none");
                sb.Append(" visual=").Append(RelativePath(source.transform, source.m_visual.transform));
                foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
                {
                    Renderer r = t.GetComponent<Renderer>();
                    sb.Append(" | ").Append(RelativePath(copy.transform, t)).Append(t.gameObject.activeSelf ? "" : "(off)");
                    if (r != null) sb.Append(r.enabled ? "[R]" : "[r-off]");
                }
                Plugin.Instance.Log(sb.ToString());
            }
            catch (Exception e) { Plugin.Instance.Log("layout dump failed: " + e.Message); }
        }

        private static string RelativePath(Transform root, Transform child)
        {
            if (child == root) return ".";
            string path = child.name;
            for (Transform p = child.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private static Raven _hugin, _munin;

        private static Raven FindRaven(bool munin)
        {
            Raven cached = munin ? _munin : _hugin;
            if (cached != null) return cached;
            foreach (Raven r in UnityEngine.Object.FindObjectsByType<Raven>(FindObjectsSortMode.None))
            {
                if (r.m_isMunin) _munin = r; else _hugin = r;
            }
            return munin ? _munin : _hugin;
        }

        private static string Label(Ghost g)
        {
            string raven = g.Munin ? Lang.T("Мунін") : Lang.T("Хугін");
            return raven + " → " + g.OwnerName;
        }

        private static float TextOffset(Ghost g)
        {
            Raven r = FindRaven(g.Munin);
            return r != null ? r.m_textOffset : 1.5f;
        }
    }

    // ---------------------------------------------------------------- hooks on the owner's real raven

    [HarmonyPatch(typeof(Raven), "Spawn")]
    internal static class SharedSpawnPatch
    {
        private static void Postfix(Raven __instance, Raven.RavenText text)
        {
            if (!Shared.Enabled || text == null || text.m_key == null || !text.m_key.StartsWith(Plugin.KeyPrefix) || Plugin.IsPrivate(text.m_key)) return;
            if (__instance.transform.position.y > 5000f) return; // found no place to land
            Shared.BroadcastShow(__instance, text);
        }
    }

    [HarmonyPatch(typeof(Raven), "Talk")]
    internal static class SharedTalkPatch
    {
        private static void Prefix(Raven __instance)
        {
            if (!Shared.Enabled) return;
            Raven.RavenText cur = Plugin.CurrentText(__instance);
            if (cur != null && cur.m_key != null && cur.m_key.StartsWith(Plugin.KeyPrefix) && !Plugin.IsQuip(cur.m_key) && !Plugin.IsPrivate(cur.m_key)) Shared.BroadcastSay(cur);
        }
    }

    [HarmonyPatch(typeof(Raven), "FlyAway")]
    internal static class SharedFlyAwayPatch
    {
        private static void Prefix(Raven __instance)
        {
            if (!Shared.Enabled || Plugin.HoldsQuip(__instance) || Plugin.HoldsVisit(__instance)) return;
            Raven.RavenText cur = Plugin.CurrentText(__instance);
            // also when its text is already gone (a quick raven that has said its piece): the others were told it came
            if (Shared.Shown || (cur != null && cur.m_key != null && cur.m_key.StartsWith(Plugin.KeyPrefix) && !Plugin.IsPrivate(cur.m_key))) Shared.BroadcastHide();
        }
    }
}
