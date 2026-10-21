using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The server's balance is everyone's on it: prices, gift costs and pauses, strength and trust gains, losses,
    // satiety, ritual heads and foods. A player joining says hello, the server answers with its own values of these
    // settings, and the game uses them instead of the player's file, so nobody makes a raven cheap or tireless by
    // editing their own config. The player's file is never written: the server's values only lie over it while
    // connected (a change in the file or the mod menu meanwhile changes nothing), and after leaving everything is the
    // player's own again. A change on the server reaches everyone on it a moment later. Personal things (keys,
    // chattiness, look, flight, the interface) stay each player's own. The host of a world (and single player) is the
    // server itself: its own file counts.
    internal static class ServerConfig
    {
        private const string RpcHello = "RavenWatch_ConfigHello", RpcValues = "RavenWatch_ConfigValues";
        private const int Format = 1;

        private static readonly HashSet<string> SyncedSections = new HashSet<string> { "5. Raven items", "7. Trust" };

        // in those sections, what stays each player's own: how the ravens look, fly and talk, the interface
        private static readonly HashSet<string> Local = new HashSet<string>
        {
            "Enabled", "Ability gestures", "Munin ward shows damage", "Radial menu group", "Show gains in corner", "Thought assembled percent",
            "Munin fury effect color", "Quiet seconds between own lines", "Moment word chance", "Emote words chance",
            "Emote words pause minutes", "Moment talk chance", "Talks with other players ravens", "Ravens touch the world",
            "Other players ravens may sit on me", "Words in passing chance", "Words in passing pause minutes",
            "Talks with other players ravens pause minutes", "Own lines per 10 minutes",
            "SeneaL UI shoulder cells", "SeneaL UI shoulder cell size", "SeneaL UI shoulder cell out", "SeneaL UI shoulder cell down",
            "Hugin light talk chance", "Hugin light game sounds", "Hugin light sound volume", "Hugin light follow lag seconds",
            "Hugin light runes per second", "Hugin light wisp color", "Hugin light wisp opacity", "Hugin shadow self opacity percent",
            "Flight wing beat speed", "Flight clip", "Hover uses game flight", "Flight level percent", "Flight loop auto",
            "Flight clip from percent", "Flight clip to percent",
            "Hugin bone", "Munin bone", "Shoulder scale", "Shoulder offset X", "Shoulder offset Y", "Shoulder offset Z",
            "Shoulder rotation X", "Shoulder rotation Y", "Shoulder rotation Z", "Hugin tilt", "Munin tilt",
            "Landing tuning v2", "Flight black box key", "Raven outings", "Raven outings check from minutes",
            "Raven outings check to minutes", "Raven outings chance percent", "Shoulder arrival delay seconds",
            "Hover during emotes", "Eye glow by trust level", "Outfits only from the game", "Book of ravens size percent",
            "Outfits all for tuning",
            "Raven size at level 1 percent", "Guide raven", "Guide repeat minutes",
        };

        private sealed class Item { public ConfigEntryBase Entry; public Action Apply; }

        private static readonly Dictionary<string, Item> Items = new Dictionary<string, Item>();
        private static readonly HashSet<ConfigFile> Watched = new HashSet<ConfigFile>();
        private static Dictionary<string, string> _server; // the server's values while on a server; null: our own file
        private static bool _helloSent, _dirty;
        private static float _sendAt;
        private static readonly Dictionary<long, float> Answered = new Dictionary<long, float>(); // the server: who got them when

        private static string Key(ConfigDefinition d) { return d.Section + "\n" + d.Key; }

        internal static bool Synced(ConfigEntryBase e)
        {
            return e != null && SyncedSections.Contains(e.Definition.Section) && !Local.Contains(e.Definition.Key);
        }

        // every setting the mod applies goes through here: a synced one is remembered with the way it is applied
        internal static void Add(ConfigEntryBase e, Action apply)
        {
            if (!Synced(e)) return;
            Items[Key(e.Definition)] = new Item { Entry = e, Apply = apply };
            if (Watched.Add(e.ConfigFile)) e.ConfigFile.SettingChanged += OnChanged;
        }

        // the value to use: the server's while on a server, else the file's
        internal static T Get<T>(ConfigEntry<T> e)
        {
            string s;
            if (_server != null && e != null)
            {
                string k = Key(e.Definition);
                if (Items.ContainsKey(k) && _server.TryGetValue(k, out s))
                {
                    try { return (T)TomlTypeConverter.ConvertToValue(s, typeof(T)); }
                    catch (Exception) { }
                }
            }
            return e.Value;
        }

        // on the server: a synced setting changed (the file, the mod menu) — everyone gets it a moment later
        private static void OnChanged(object sender, SettingChangedEventArgs ev)
        {
            if (ev == null || !Synced(ev.ChangedSetting)) return;
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            _dirty = true;
            _sendAt = Time.time + 2f;
        }

        internal static void Register()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZPackage>(RpcHello, new Action<long, ZPackage>(OnHello));
            ZRoutedRpc.instance.Register<ZPackage>(RpcValues, new Action<long, ZPackage>(OnValues));
        }

        // the mod's Update, on every game (a dedicated server too)
        internal static void Update()
        {
            if (!_dirty || Time.time < _sendAt) return;
            _dirty = false;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcValues, Pack());
            Log("Server settings: a change sent to everyone (" + Items.Count + " values)");
        }

        // the server: a player has just joined (before their character loads, so nothing is bought at their own price)
        internal static void SendTo(long peer)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZRoutedRpc.instance == null || peer == 0) return;
            float now = Time.realtimeSinceStartup, at;
            if (Answered.TryGetValue(peer, out at) && now - at < 2f) return; // asked again and again: not a flood of answers
            Answered[peer] = now;
            if (Answered.Count > 200) Answered.Clear();
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, RpcValues, Pack());
        }

        // a client in the world (the mod's Tick): ask the server once, in case its first word was missed
        internal static void Hello()
        {
            if (_helloSent || ZRoutedRpc.instance == null || ZNet.instance == null || ZNet.instance.IsServer()) return;
            _helloSent = true;
            ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcHello, new ZPackage());
        }

        // left the world: our own file again
        internal static void ResetClient()
        {
            _helloSent = false;
            if (_server == null) return;
            _server = null;
            ApplyAll();
            Log("Server settings: back to your own file");
        }

        private static void OnHello(long sender, ZPackage p) { SendTo(sender); }

        private static void OnValues(long sender, ZPackage p)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer()) return; // the host keeps its own
            if (sender != Plugin.ServerPeerId()) return;
            try
            {
                if (p.ReadInt() != Format) { Log("Server settings: unknown format, kept your own file"); return; }
                int n = p.ReadInt();
                Dictionary<string, string> got = new Dictionary<string, string>();
                for (int i = 0; i < n; i++)
                {
                    string k = p.ReadString();
                    got[k] = p.ReadString();
                }
                _server = got;
                int differ = 0;
                foreach (KeyValuePair<string, Item> kv in Items)
                {
                    string s;
                    if (got.TryGetValue(kv.Key, out s) && s != kv.Value.Entry.GetSerializedValue()) differ++;
                }
                ApplyAll();
                Log("Server settings: " + got.Count + " values from the server, " + differ + " differ from your file");
            }
            catch (Exception e) { Log("Server settings: could not read them (" + e.Message + "), kept your own file"); }
        }

        private static ZPackage Pack()
        {
            ZPackage p = new ZPackage();
            p.Write(Format);
            p.Write(Items.Count);
            foreach (KeyValuePair<string, Item> kv in Items)
            {
                p.Write(kv.Key);
                p.Write(kv.Value.Entry.GetSerializedValue());
            }
            return p;
        }

        private static void ApplyAll()
        {
            foreach (Item it in Items.Values)
            {
                try { it.Apply(); }
                catch (Exception e) { Log("Server settings: " + it.Entry.Definition.Key + " failed: " + e.Message); }
            }
        }

        private static void Log(string s)
        {
            if (Plugin.Instance != null) Plugin.Instance.Log(s);
        }
    }

    [HarmonyPatch(typeof(ZRoutedRpc), "AddPeer")]
    internal static class ServerConfigJoinPatch
    {
        private static void Postfix(ZNetPeer peer)
        {
            if (peer != null) ServerConfig.SendTo(peer.m_uid);
        }
    }
}
