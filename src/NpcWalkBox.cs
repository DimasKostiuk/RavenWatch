#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A person's feet: what it did, measured and written; the console (NpcWalk).
    internal sealed partial class NpcWalk
    {
        // ---------------------------------------------------------------- the measures (raven npc walk report)
        // A micro-stop: it means to walk, it was walking, and its feet all but halt for a moment (under MicroBelow m/s for
        // MicroFor s) — the game's turn-on-the-spot made hundreds of them and none was a «halt» over half a second
        internal static float MicroBelow = 0.45f, MicroAbove = 0.9f, MicroFor = 0.08f;
        private Character _body;
        private float _dipAt = -1f, _goSince = -1f, _goLast;
        private bool _wasFast;
        private void Dips()
        {
            float now = Time.time;
            if (now - _goLast > 0.3f) { _goSince = now; _wasFast = false; _dipAt = -1f; } // (a fresh start: its speeding up is no dip)
            _goLast = now;
            _trip.Going += Time.deltaTime;
            if (_body == null) _body = _m.GetComponent<Character>();
            if (_body == null) return;
            Vector3 v = _body.GetVelocity(); v.y = 0f;
            float s = v.magnitude;
            if (s >= MicroAbove) { if (_dipAt >= 0f && now - _dipAt >= MicroFor) { _trip.Micro++; Totals.Micro++; } _dipAt = -1f; _wasFast = true; return; }
            if (s < MicroBelow && _wasFast && now - _goSince > 0.8f && _dipAt < 0f) _dipAt = now;
        }

        // every put-somewhere of a body (a jump, a slide, a snap onto a seat or off it, a step on unseen): one place counts
        // them, with why and how far — the drive towards none a player sees
        internal static void Jumped(Component who, Vector3 from, Vector3 to, string why)
        {
            float far = Vector3.Distance(from, to);
            if (far < 0.25f) return; // (a body already there: nothing seen)
            int n; Totals.Jumps.TryGetValue(why, out n); Totals.Jumps[why] = n + 1;
            NpcMind m = who != null ? who.GetComponent<NpcMind>() : null;
            if (m != null && m.Feet != null && m.Feet._trip != null) m.Feet._trip.Jumps++;
            if (Watch) Write("halts.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}	{1}	JUMP {2}	{3:0.00} m	{4} → {5}	seen {6}", DateTime.Now, m != null ? m.Name() : (who != null ? who.name : "?"), why, far, Npcs.V(from), Npcs.V(to), Watched(from) || Watched(to)));
        }

        // the counts since the test began or the last mark
        private static class Totals
        {
            public static int Walks, Halts, Micro, Slides, Pops; public static float Metres, Took, Stood, Going;
            public static readonly Dictionary<string, int> Jumps = new Dictionary<string, int>();
            public static readonly Dictionary<string, int> PopsBy = new Dictionary<string, int>();
            public static readonly Dictionary<string, int> DoorUses = new Dictionary<string, int>();
            public static DateTime From = DateTime.Now;
            public static void Clear() { Walks = Halts = Micro = Slides = Pops = 0; Metres = Took = Stood = Going = 0f; Jumps.Clear(); PopsBy.Clear(); DoorUses.Clear(); From = DateTime.Now; }
        }

        internal static void ClearTotals() { Totals.Clear(); }

        // a door told open or shut (NpcDoors): written, counted per door (a door worked over and over is a fault)
        internal static void DoorUsed(Door d, string what, Vector3 by)
        {
            string key = Npcs.V(d.transform.position);
            int n; Totals.DoorUses.TryGetValue(key, out n); Totals.DoorUses[key] = n + 1;
            if (Watch) Write("halts.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}\tDOOR {1}\t{2}\tby {3}", DateTime.Now, what, key, Npcs.V(by)));
        }
        internal static void Popped(string who, string what) { Totals.Pops++; string k = who + ": " + what; int n; Totals.PopsBy.TryGetValue(k, out n); Totals.PopsBy[k] = n + 1; }

        internal static string Report()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "Хода з {0:HH:mm:ss}: прогулянок {1}, {2:0} м за {3:0} с.", Totals.From, Totals.Walks, Totals.Metres, Totals.Took);
            sb.AppendFormat(CultureInfo.InvariantCulture, "\n  швидкість у русі {0:0.00} м/с · мікрозупинок {1} ({2:0.0} на 10 м) · зупинок > 0.5 с {3}, стояли {4:0} с · протискань {5}",
                Totals.Going > 0f ? Totals.Metres / Totals.Going : 0f, Totals.Micro, Totals.Metres > 0f ? Totals.Micro * 10f / Totals.Metres : 0f, Totals.Halts, Totals.Stood, Totals.Slides);
            int jumps = 0; foreach (int v in Totals.Jumps.Values) jumps += v;
            sb.Append("\n  ривки (перенесення тіла): ").Append(jumps);
            foreach (KeyValuePair<string, int> kv in Totals.Jumps) sb.Append(" · ").Append(kv.Key).Append(' ').Append(kv.Value);
            sb.Append("\n  стрибки пози: ").Append(Totals.Pops);
            foreach (KeyValuePair<string, int> kv in Totals.PopsBy) sb.Append(" · ").Append(kv.Key).Append(' ').Append(kv.Value);
            int uses = 0, most = 0; string mostAt = "";
            foreach (KeyValuePair<string, int> kv in Totals.DoorUses) { uses += kv.Value; if (kv.Value > most) { most = kv.Value; mostAt = kv.Key; } }
            sb.Append("\n  двері: дій ").Append(uses).Append(", дверей ").Append(Totals.DoorUses.Count).Append(most > 0 ? ", найбільше в одних " + most + " (" + mostAt + ")" : "");
            string s = sb.ToString();
            Write("summary.txt", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "\n" + s + "\n");
            return s;
        }

        // ---------------------------------------------------------------- the black box
        internal static bool Watch = Directory.Exists(Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "studio")); // (on where the studio is: the author's own game)
        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "npcwalk"); } }

        private sealed class Trip
        {
            public string What = ""; public Vector3 From, To; public float At;
            public int Asks, Fails, Partial, Rounds, Halts, Bridges, NoBridge, Widened, Slides, Turns, Micro, Jumps, Doors, Shaped; public float Stood, Moved, Going;
            public bool Pushed;
            public string Why = ""; public float HaltSince = -1f; public string HaltWhy = ""; public Vector3 HaltAt;
            private Vector3 _last; private float _lastAt;
            public void Moving(NpcMind m) { Vector3 p = m.transform.position; if (_lastAt > 0f) Moved += Utils.DistanceXZ(p, _last); _last = p; _lastAt = Time.time; }
        }
        private Trip _trip;
        private float _filmAt;
        private static int _films;

        // it stands while it means to go: how long and why (one line per halt over half a second)
        private void Halt(string why, Vector3 me, Vector3 fwd)
        {
            Doing = why;
            if (_trip == null) return;
            float now = Time.time;
            if (_trip.HaltSince < 0f || _trip.HaltWhy != why) { Flush(); _trip.HaltSince = now; _trip.HaltWhy = why; _trip.HaltAt = me; }
            _trip.Stood += Time.deltaTime;
            if (now - _trip.HaltSince > 3f && now - _filmAt > 60f && _films < 20 && Watch) // (a long one: a top view of it, once a minute at most)
            {
                _filmAt = now; _films++;
                Film("walk" + _films);
            }
        }

        // the halt that was going on, written once it ends (or the walk does)
        private void Flush()
        {
            Trip t = _trip;
            if (t == null || t.HaltSince < 0f) return;
            float len = Time.time - t.HaltSince;
            t.HaltSince = -1f;
            if (len < 0.5f) return;
            t.Halts++;
            if (t.Why.Length == 0 || len > 1f) t.Why = t.HaltWhy;
            Vector3 fwd = t.To - t.HaltAt; fwd.y = 0f;
            Write("halts.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}\t{1}\t{2}\t{3:0.0}s\t{4}\tat {5}\tgoal {6} ({7:0.0} m, up {8:0.0})\tby: {9}\tnear: {10}",
                DateTime.Now, _m.Name(), t.What, len, t.HaltWhy, Npcs.V(t.HaltAt), Npcs.V(t.To), Utils.DistanceXZ(t.HaltAt, t.To), t.To.y - t.HaltAt.y, Things(t.HaltAt, fwd), People(t.HaltAt)));
        }

        private void End(string how)
        {
            Trip t = _trip;
            _trip = null;
            if (t == null) return;
            if (t.HaltSince >= 0f) { _trip = t; Flush(); _trip = null; }
            float took = Time.time - t.At, straight = Utils.DistanceXZ(t.From, t.To);
            Totals.Walks++; Totals.Metres += t.Moved; Totals.Took += took; Totals.Stood += t.Stood; Totals.Going += t.Going; Totals.Halts += t.Halts; Totals.Slides += t.Slides;
            Write("walks.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}\t{1}\t{2}\t{3}\t{4:0.0} m\ttook {5:0.0}s\tstood {6:0.0}s ({7} halts)\twalked {8:0.0} m\tasks {9} fails {10} partial {11} rounds {12} bridges {16}/{17} widened {18} slides {19} turns {20} micro {21} jumps {22} speed {23:0.00} doors {24} shaped {25}\t{13}\t{14} → {15}",
                DateTime.Now, _m.Name(), t.What, how, straight, took, t.Stood, t.Halts, t.Moved, t.Asks, t.Fails, t.Partial, t.Rounds, t.Why, Npcs.V(t.From), Npcs.V(t.To), t.Bridges, t.NoBridge, t.Widened, t.Slides, t.Turns, t.Micro, t.Jumps, t.Going > 0f ? t.Moved / t.Going : 0f, t.Doors, t.Shaped));
            if (t.Rounds >= 3 || took > 20f + straight || t.Moved > straight * 3f + 8f) Write("walks.txt", "\tits last way (" + _path.Count + " points, at " + _next + "): " + WayText()); // (a long one, or one far round: its way with heights — what it followed)
        }

        private string WayText()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _path.Count && i < 40; i++) sb.Append(i == _next ? "[" : "").Append(Npcs.V(_path[i])).Append(i == _next ? "]" : "").Append(" · ");
            return sb.ToString();
        }

        // its «stuck» rule's last word (NpcMind.Unstick): how it ended
        internal void Stuck(string how) { if (_trip != null) End(how); Forget(); }

        // put somewhere else (a jump, through a door): its old way and its count of gain forgotten — the next step begins afresh
        internal void Forget() { _goal = new Vector3(float.MaxValue, 0f, 0f); _hasPath = false; _path.Clear(); _slideUntil = 0f; _stepUntil = 0f; }

        // what stands by it, in front (the pieces' names: a door, stairs, a table, a fence)
        private static readonly int Pieces = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid");
        private static string Things(Vector3 at, Vector3 fwd)
        {
            if (fwd.sqrMagnitude > 0.0001f) fwd.Normalize();
            HashSet<string> names = new HashSet<string>();
            foreach (Collider c in Physics.OverlapSphere(at + fwd * 0.7f + Vector3.up * 0.9f, 0.9f, Pieces, QueryTriggerInteraction.Ignore))
            {
                Piece p = c.GetComponentInParent<Piece>();
                string n = p != null ? p.gameObject.name : c.transform.root.name;
                names.Add(n.Replace("(Clone)", "").Trim());
                if (names.Count >= 6) break;
            }
            return names.Count > 0 ? string.Join(", ", new List<string>(names).ToArray()) : "-";
        }

        private string People(Vector3 at)
        {
            List<string> l = new List<string>();
            foreach (NpcActor o in NpcActor.Live) if (o != null && o.gameObject != _m.gameObject && Utils.DistanceXZ(o.transform.position, at) < 1.6f) l.Add(o.Name);
            foreach (Player p in Player.GetAllPlayers()) if (p != null && Utils.DistanceXZ(p.transform.position, at) < 1.6f) l.Add(p.GetPlayerName());
            return l.Count > 0 ? string.Join(", ", l.ToArray()) : "-";
        }

        // a top view of it from the studio (it reads its request file: our own line in it)
        private void Film(string name)
        {
            try
            {
                NpcActor a = _m.Actor;
                if (a == null) return;
                string who = a.Tag.Length > 0 ? a.Tag : a.Id;
                string req = Path.Combine(Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "studio"), "request.txt");
                File.AppendAllText(req, "nfilm " + name + " " + who + " 3 4 top,three\n"); // (top over open ground; three under a roof)
                Write("halts.txt", "\t\t(studio: " + name + " — a top view of " + who + ")");
            }
            catch (Exception e) { Npcs.Log("walk film: " + e.Message); }
        }

        internal static void Write(string file, string line)
        {
            try { Directory.CreateDirectory(Dir); File.AppendAllText(Path.Combine(Dir, file), line + "\n", Encoding.UTF8); }
            catch (Exception e) { Npcs.Log("walk log: " + e.Message); }
        }

        // ---------------------------------------------------------------- the console: raven npc walk …
        internal static string Command(Player p, string[] a)
        {
            string w = a.Length > 2 ? a[2] : "";
            switch (w)
            {
                case "set": return a.Length > 4 ? Set(a[3], a[4]) : Get();
                case "watch": Watch = a.Length < 4 || a[3] != "off"; return "чорна скринька: " + (Watch ? "пише в " + Dir : "вимкнено");
                case "test": return NpcWalkTest.Start(p, a.Length > 3 ? a[3] : "");
                case "stop": return NpcWalkTest.Stop();
                case "report": return Report();
                case "go": // go <who> x y z — sent there on its Order layer (a walk to measure); free: raven npc free <who>
                    {
                        NpcActor who = a.Length > 6 ? Npcs.Find(a[3]) : null;
                        float x, y, z;
                        if (who == null || !float.TryParse(a[4], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(a[5], NumberStyles.Float, CultureInfo.InvariantCulture, out y) || !float.TryParse(a[6], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return "raven npc walk go <хто> x y z";
                        who.Order("go " + Npcs.V(new Vector3(x, y, z)));
                        return who.Name + " іде до " + Npcs.V(new Vector3(x, y, z)) + " (відпустити: raven npc free)";
                    }
                case "mark": Totals.Clear(); Write("walks.txt", "---- " + (a.Length > 3 ? string.Join(" ", a, 3, a.Length - 3) : "mark") + " ----"); Write("halts.txt", "---- " + (a.Length > 3 ? string.Join(" ", a, 3, a.Length - 3) : "mark") + " ----"); return "Позначено.";
                default: return "raven npc walk set <ключ> <число> | watch [off] | test [хв] | stop | mark <слова> (лічильники з нуля) | report\n" + Get();
            }
        }
    }
}
#endif
