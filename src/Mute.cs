using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace HuginWhispers
{
    // Players the admin has silenced: no Hugin or Munin of this mod ever visits them.
    // The server keeps the list in a file (by platform id, so it survives restarts and new characters)
    // and tells each player whether they are on it.
    internal static class Mute
    {
        internal const string RpcSet = "HuginWhispers_MuteSet";
        internal const string RpcList = "HuginWhispers_MuteList";
        internal const string RpcListReply = "HuginWhispers_MuteListReply";
        internal const string RpcHello = "HuginWhispers_Hello";
        internal const string RpcState = "HuginWhispers_MuteState";
        private const string HostKey = "host";

        // client side
        internal static bool SelfMuted;
        internal static bool AdminByServer; // the server's own adminlist check, sent with the hello answer
        internal static bool HelloSent;
        internal static readonly HashSet<long> MutedOnline = new HashSet<long>(); // for the admin window

        // server side
        private static HashSet<string> _ids;
        private static Dictionary<string, string> _names;

        private static string FilePath { get { return Path.Combine(Path.Combine(Paths.ConfigPath, "RavenWatch"), "muted_players.txt"); } }

        internal static void ResetClient()
        {
            SelfMuted = false;
            AdminByServer = false;
            HelloSent = false;
            MutedOnline.Clear();
        }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcSet, new Action<long, ZPackage>(OnSet));
            ZRoutedRpc.instance.Register<ZPackage>(RpcList, new Action<long, ZPackage>(OnList));
            ZRoutedRpc.instance.Register<ZPackage>(RpcListReply, new Action<long, ZPackage>(OnListReply));
            ZRoutedRpc.instance.Register<ZPackage>(RpcHello, new Action<long, ZPackage>(OnHello));
            ZRoutedRpc.instance.Register<ZPackage>(RpcState, new Action<long, ZPackage>(OnState));
        }

        // ---------------------------------------------------------------- client calls

        internal static void SendHello()
        {
            HelloSent = true;
            ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcHello, new ZPackage());
        }

        internal static void RequestList()
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcList, new ZPackage());
        }

        internal static void Set(long target, bool muted)
        {
            if (muted) MutedOnline.Add(target); else MutedOnline.Remove(target); // the server's answer confirms it
            ZPackage p = new ZPackage();
            p.Write(target);
            p.Write(muted);
            ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcSet, p);
        }

        // ---------------------------------------------------------------- server

        private static void Load()
        {
            if (_ids != null) return;
            _ids = new HashSet<string>();
            _names = new Dictionary<string, string>();
            if (!File.Exists(FilePath)) return;
            foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string name = "";
                int hash = line.IndexOf('#');
                if (hash > 0) { name = line.Substring(hash + 1).Trim(); line = line.Substring(0, hash).Trim(); }
                _ids.Add(line);
                _names[line] = name;
            }
        }

        private static void Save()
        {
            List<string> lines = new List<string>();
            lines.Add("# RavenWatch: players who never get a raven. One id per line; the name after # is only a reminder.");
            foreach (string id in _ids)
            {
                string name;
                _names.TryGetValue(id, out name);
                lines.Add(string.IsNullOrEmpty(name) ? id : id + "  # " + name);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllLines(FilePath, lines.ToArray(), new UTF8Encoding(false));
        }

        private static string KeyOf(long uid, out string name)
        {
            name = "";
            if (uid == ZDOMan.GetSessionID())
            {
                if (Player.m_localPlayer != null) name = Player.m_localPlayer.GetPlayerName();
                return HostKey;
            }
            ZNetPeer peer = ZNet.instance.GetPeer(uid);
            if (peer == null || peer.m_socket == null) return null;
            name = peer.m_playerName;
            return peer.m_socket.GetHostName();
        }

        internal static bool IsMutedUid(long uid)
        {
            Load();
            string name;
            string key = KeyOf(uid, out name);
            return key != null && _ids.Contains(key);
        }

        private static void OnSet(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            long target = pkg.ReadLong();
            bool muted = pkg.ReadBool();
            if (!Plugin.IsAdminSender(sender)) { Plugin.ReplyTo(sender, Lang.T("Сервер відмовив: тебе немає в adminlist.txt.")); return; }
            Load();
            string name;
            string key = KeyOf(target, out name);
            if (key == null) { Plugin.ReplyTo(sender, Lang.T("Гравця не знайдено на сервері.")); return; }
            if (muted) { _ids.Add(key); _names[key] = name; }
            else { _ids.Remove(key); _names.Remove(key); }
            Save();

            ZPackage state = new ZPackage();
            state.Write(muted);
            ZRoutedRpc.instance.InvokeRoutedRPC(target, RpcState, state);
            Plugin.ReplyTo(sender, muted ? Lang.F("{0}: ворони вимкнені.", name) : Lang.F("{0}: ворони знову прилітають.", name));
            SendList(sender);
        }

        private static void OnList(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (!Plugin.IsAdminSender(sender)) return;
            SendList(sender);
        }

        private static void SendList(long to)
        {
            Load();
            List<long> online = new List<long>();
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                long uid = info.m_characterID.UserID;
                if (uid != 0 && IsMutedUid(uid)) online.Add(uid);
            }
            ZPackage p = new ZPackage();
            p.Write(online.Count);
            foreach (long uid in online) p.Write(uid);
            ZRoutedRpc.instance.InvokeRoutedRPC(to, RpcListReply, p);
        }

        private static void OnHello(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            ZPackage state = new ZPackage();
            state.Write(IsMutedUid(sender));
            state.Write(Plugin.IsAdminSender(sender));
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcState, state);
        }

        // ---------------------------------------------------------------- client receives

        private static void OnListReply(long sender, ZPackage pkg)
        {
            if (sender != Plugin.ServerPeerId() && sender != ZDOMan.GetSessionID()) return;
            MutedOnline.Clear();
            int n = pkg.ReadInt();
            for (int i = 0; i < n; i++) MutedOnline.Add(pkg.ReadLong());
        }

        private static void OnState(long sender, ZPackage pkg)
        {
            if (sender != Plugin.ServerPeerId() && sender != ZDOMan.GetSessionID()) return;
            SelfMuted = pkg.ReadBool();
            try { if (pkg.GetPos() < pkg.Size()) AdminByServer = pkg.ReadBool(); } catch { } // older servers send only the first flag
            if (SelfMuted && Plugin.Instance != null) Plugin.Instance.RemoveOurTexts();
        }
    }
}
