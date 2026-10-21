#if RAVEN_ITEM
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Журнал воронів»: a raven of yours getting off a shoulder (or someone trying), getting on one, crumbling, or
    // vanishing from both shoulder and bag gets a line in a file of its own, which the game's log overwriting does not
    // touch: BepInEx/config/RavenWatch/rec/ravens_journal.txt. A line: when, whose, which raven (its id), what
    // happened, what the raven was doing (the director's act, the motor's plan, an order, an outing) and who called it
    // (the first methods outside RavenWatch). The same line within 10 s is counted, not written again (a mod calling
    // something every frame cannot flood it). Lines older than 14 days, and all but the last 2000, go once a game start.
    internal static class RavenJournal
    {
        internal const int KeepDays = 14, KeepLines = 2000;
        private const float Repeat = 10f; // s: the same event again within this is only counted

        private static string FilePath { get { return Path.Combine(Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "rec"), "ravens_journal.txt"); } }

        // ---------------------------------------------------------------- writing

        private sealed class Last { public float At; public int Skipped; }
        private static readonly Dictionary<string, Last> Recent = new Dictionary<string, Last>();
        private static bool _trimmed;

        internal static void Note(Player p, ItemDrop.ItemData item, string what)
        {
            if (p == null || p != Player.m_localPlayer || item == null) return; // only your own ravens
            bool munin;
            if (!RavenItem.IsRaven(item, out munin)) return;
            string id = IdOf(item);
            if (what.StartsWith("forgotten") || what.StartsWith("death")) Expected.Add(id); // the watch knows it goes
            int skipped;
            if (Throttled(id + what, out skipped)) return;
            StringBuilder sb = new StringBuilder(160);
            sb.Append(munin ? "Munin " : "Hugin ").Append(id).Append(" | ").Append(what);
            if (skipped > 0) sb.Append(" (and ").Append(skipped).Append(" more times in the last seconds)");
            State(sb, p, munin);
            sb.Append(" | by ").Append(Caller());
            Write(p, sb.ToString());
        }

        private static bool Throttled(string key, out int skipped)
        {
            skipped = 0;
            Last l;
            float now = Time.realtimeSinceStartup;
            if (Recent.TryGetValue(key, out l))
            {
                if (now - l.At < Repeat) { l.Skipped++; return true; }
                skipped = l.Skipped; l.At = now; l.Skipped = 0;
                return false;
            }
            if (Recent.Count > 200) Recent.Clear(); // never grows for ever
            Recent[key] = new Last { At = now };
            return false;
        }

        // what the raven was doing then
        private static void State(StringBuilder sb, Player p, bool munin)
        {
            GameObject m = Shoulder.ModelOf(p, munin);
            sb.Append(" | act ").Append(m != null ? AnimDirector.Of(m).ToString() : "-");
            ShoulderLife life = m != null ? m.GetComponent<ShoulderLife>() : null;
            if (life != null && life.Motor != null && life.Motor.Airborne) sb.Append(" «").Append(life.Motor.Plan).Append('»');
            int o = Orders.Of(p, munin), u = Outing.Of(p, munin);
            if (o != Orders.None) sb.Append(", order ").Append(Orders.Names[o]);
            if (u != Outing.None) sb.Append(", outing ").Append(u);
            if (p.IsDead()) sb.Append(", dead");
        }

        private static void Write(Player p, string text)
        {
            string line = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + p.GetPlayerName() + " | " + text;
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!_trimmed) { _trimmed = true; Trim(path); }
                File.AppendAllText(path, line + "\n", new UTF8Encoding(false));
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven journal failed: " + e.Message); }
            if (Plugin.Instance != null) Plugin.Instance.Log("Raven journal: " + line);
        }

        // once a game start, before the first line: the old lines go (older than KeepDays, beyond the last KeepLines)
        private static void Trim(string path)
        {
            if (!File.Exists(path)) return;
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            string from = System.DateTime.Now.AddDays(-KeepDays).ToString("yyyy-MM-dd HH:mm:ss");
            List<string> keep = new List<string>(lines.Length);
            foreach (string l in lines) if (l.Length >= 19 && string.CompareOrdinal(l, 0, from, 0, 19) >= 0) keep.Add(l); // the line starts with its date
            if (keep.Count > KeepLines) keep.RemoveRange(0, keep.Count - KeepLines);
            if (keep.Count != lines.Length) File.WriteAllLines(path, keep.ToArray(), new UTF8Encoding(false));
            string old = path + ".old"; // the earlier version kept a rotated copy: gone
            if (File.Exists(old)) File.Delete(old);
        }

        internal static string IdOf(ItemDrop.ItemData item)
        {
            string id;
            return item.m_customData != null && item.m_customData.TryGetValue("RavenWatch_id", out id) && !string.IsNullOrEmpty(id) ? id : "?";
        }

        // the methods that led here, outside our own classes (the game's or another mod's), most recent first
        private static string Caller()
        {
            StackFrame[] frames = new StackTrace(2, false).GetFrames();
            if (frames == null) return "?";
            StringBuilder sb = new StringBuilder();
            int n = 0;
            foreach (StackFrame f in frames)
            {
                System.Reflection.MethodBase mb = f.GetMethod();
                if (mb == null || mb.DeclaringType == null) continue;
                string ns = mb.DeclaringType.Namespace ?? "";
                if (sb.Length > 0) sb.Append(" < ");
                if (ns.Length > 0 && ns != "HuginWhispers") sb.Append(ns).Append('.');
                sb.Append(mb.DeclaringType.Name).Append('.').Append(mb.Name);
                if (++n >= 6) break;
            }
            return sb.ToString();
        }

        // is this call coming through any of these methods (by name, anywhere in the chain)?
        internal static bool CalledThrough(string[] names)
        {
            StackFrame[] frames = new StackTrace(1, false).GetFrames();
            if (frames == null) return false;
            foreach (StackFrame f in frames)
            {
                System.Reflection.MethodBase mb = f.GetMethod();
                if (mb == null) continue;
                string n = mb.Name, t = mb.DeclaringType != null ? mb.DeclaringType.FullName ?? "" : "";
                foreach (string want in names) if (n.Contains(want) || t.Contains(want)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- the watch
        // Every 2 s (from Shoulder.Tick): where each of your ravens is (by its id: a shoulder, or a bag cell). One that
        // was here and is now nowhere gets a line with where it was last (a raven you dropped or put in a chest shows
        // here too: the line says so). No garbage: the places are numbers, kept in two reused tables.
        private static Dictionary<string, int> _seen = new Dictionary<string, int>(), _now = new Dictionary<string, int>();
        private static float _watchAt;
        private static Player _watchFor;
        private const int RightShoulder = -1, LeftShoulder = -2; // else a bag cell: x * 100 + y

        internal static void Watch()
        {
            Player p = Player.m_localPlayer;
            if (p == null || Time.time < _watchAt) return;
            _watchAt = Time.time + 2f;
            if (p != _watchFor) { _watchFor = p; _seen.Clear(); } // another character (or born again): starts over
            if (p.IsDead() || p.IsTeleporting()) return;
            _now.Clear();
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            Put(h, RightShoulder); Put(m, LeftShoulder);
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) { bool mu; if (RavenItem.IsRaven(it, out mu)) Put(it, it.m_gridPos.x * 100 + it.m_gridPos.y); }
            foreach (KeyValuePair<string, int> kv in _seen)
                if (!_now.ContainsKey(kv.Key)) Gone(p, kv.Key, kv.Value);
            Dictionary<string, int> t = _seen; _seen = _now; _now = t; // swap: no copying
        }

        private static void Put(ItemDrop.ItemData it, int where)
        {
            if (it == null || it.m_customData == null) return;
            string id;
            if (it.m_customData.TryGetValue("RavenWatch_id", out id) && !string.IsNullOrEmpty(id)) _now[id] = where;
        }

        private static readonly HashSet<string> Expected = new HashSet<string>(); // gone on purpose (crumbled, the grave): no alarm

        private static void Gone(Player p, string id, int where)
        {
            if (Expected.Remove(id)) return;
            int skipped;
            if (Throttled(id + "gone", out skipped)) return;
            string was = where == RightShoulder ? "on the right shoulder" : where == LeftShoulder ? "on the left shoulder" : "in the bag " + (where / 100) + "," + (where % 100);
            Write(p, "raven " + id + " | VANISHED: neither on a shoulder nor in the bag any more (was " + was + "). Dropped or put in a chest by you? Then all is well; else this is the loss.");
        }
    }
}
#endif
