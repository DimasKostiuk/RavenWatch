#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Ravens on perches near each other talk together: up to four ravens on up to three perches within Range of each
    // other, of one owner or of different players (their owners may be away). The perch with the lowest id among the
    // taken ones decides (taken: the perch's data, the same in every game; a bird's calm is each game's own look, so it
    // cannot choose who decides): its holder puts the talk together and sends it, everyone near sees the same talk.
    //   - one owner's Hugin and Munin (on one perch or on two): their own perch talk;
    //   - ravens of different players: a talk of the cross kinds (the first in this session a greeting, then old pals;
    //     a warm place: a fireside talk), and now and then one owner's two among them still talk alone.
    internal static class PerchCrowd
    {
        internal static float Range = 12f, PairAlone = 0.35f, Warm = 0.5f;
        internal const int MaxPerches = 3, MaxRavens = 4;
        private const string Rpc = "RavenWatch_PerchCrowd";

        private sealed class Bird { public PerchView View; public int Seat; public Perches.Occupant O; public GameObject Go; }

        private static readonly HashSet<string> Greeted = new HashSet<string>(); // these ravens already greeted each other this session

        internal static bool Try(PerchView v)
        {
            if (v == null || !v.Owner || v.IsSpot || Plugin.Instance == null) return false;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return false;
            List<PerchView> perches = Cluster(v);
            if (perches.Count == 0 || perches[0] != v) return false; // another perch near decides
            bool heard = false;
            foreach (PerchView p in perches)
            {
                if (TalkBubble.PlayingAt(p)) return false; // a talk on one of them already (a guest's...): not cut short
                if (p.SomeoneNear()) heard = true;
            }
            if (!heard) return false; // nobody to hear
            List<Bird> birds = new List<Bird>();
            foreach (PerchView p in perches)
                for (int i = 0; i < p.Count; i++)
                    if (p.CalmAt(i)) birds.Add(new Bird { View = p, Seat = i, O = p.At(i), Go = p.BirdAt(i) });
            if (birds.Count < 2) return false;

            List<long> owners = new List<long>();
            foreach (Bird b in birds) if (!owners.Contains(b.O.Pid)) owners.Add(b.O.Pid);
            Bird h, m;
            if (owners.Count == 1) return FindPair(birds, out h, out m) && PairTalk(v, perches, h, m);
            if (UnityEngine.Random.value < PairAlone && FindPair(birds, out h, out m) && PairTalk(v, perches, h, m)) return true;
            return Mixed(v, perches, birds);
        }

        // the taken perches within Range of v (v among them), the deciding one first, at most MaxPerches
        private static List<PerchView> Cluster(PerchView v)
        {
            List<PerchView> all = new List<PerchView>();
            foreach (PerchView p in PerchView.All)
            {
                if (p == null || !p.Valid || p.IsSpot || Vector3.Distance(p.transform.position, v.transform.position) > Range) continue;
                if (p.Taken()) all.Add(p);
            }
            all.Sort((a, b) => Order(a.Id, b.Id));
            if (!all.Contains(v)) return new List<PerchView>();
            List<PerchView> pick = new List<PerchView> { all[0] };
            List<PerchView> rest = all.FindAll(p => p != all[0]);
            rest.Sort((a, b) => Vector3.Distance(a.transform.position, all[0].transform.position).CompareTo(Vector3.Distance(b.transform.position, all[0].transform.position)));
            for (int i = 0; i < rest.Count && pick.Count < MaxPerches; i++) pick.Add(rest[i]);
            if (!pick.Contains(v)) return new List<PerchView>(); // too far down the list: not in this talk
            return pick;
        }

        private static int Order(ZDOID a, ZDOID b)
        {
            int c = a.UserID.CompareTo(b.UserID);
            return c != 0 ? c : a.ID.CompareTo(b.ID);
        }

        // one owner's Hugin and Munin among them (the first such pair)
        private static bool FindPair(List<Bird> birds, out Bird h, out Bird m)
        {
            h = null; m = null;
            foreach (Bird a in birds)
            {
                if (a.O.Munin) continue;
                Bird b = birds.Find(x => x.O.Munin && x.O.Pid == a.O.Pid);
                if (b != null) { h = a; m = b; return true; }
            }
            return false;
        }

        // the two on the deciding perch: the talk over it, as ever; else each from its own perch
        private static bool PairTalk(PerchView v, List<PerchView> perches, Bird h, Bird m)
        {
            return h.View == v && m.View == v ? v.PairTalk(h.O, m.O) : Pair(v, perches, h, m);
        }

        // one owner's Hugin and Munin on two perches: their perch talk, each from its own seat
        private static bool Pair(PerchView v, List<PerchView> perches, Bird h, Bird m)
        {
            List<TalkBubble.Line> lines = v.PairLines(h.O, m.O);
            if (lines == null) return false;
            Player owner = PerchView.PlayerOf(h.O.Pid);
            List<int> by = new List<int>(), to = new List<int>(); // 0 Hugin, 1 Munin
            for (int i = 0; i < lines.Count; i++)
            {
                TalkBubble.Line l = lines[i];
                Bird s = l.Munin ? m : h, t = l.Munin ? h : m;
                l.Bird = s.Go; l.ToBird = t.Go; l.Owner = owner;
                lines[i] = l;
                by.Add(l.Munin ? 1 : 0); to.Add(l.Munin ? 0 : 1);
            }
            Play(perches, new List<Bird> { h, m }, lines, by, to);
            return true;
        }

        // ravens of different players: a cross talk; the first two always of two owners
        private static bool Mixed(PerchView v, List<PerchView> perches, List<Bird> birds)
        {
            for (int i = birds.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); Bird t = birds[i]; birds[i] = birds[j]; birds[j] = t; }
            Bird a = birds[0], b = birds.Find(x => x.O.Pid != a.O.Pid);
            if (b == null) return false;
            List<Bird> who = new List<Bird> { a, b };
            foreach (Bird x in birds) if (x != a && x != b && who.Count < MaxRavens) who.Add(x);

            List<CrossTalk.Who> whos = new List<CrossTalk.Who>();
            foreach (Bird x in who) whos.Add(WhoOf(x));
            string key = Key(who);
            bool roof; int comfort;
            v.Env(out roof, out comfort);
            string kind = roof && comfort >= 5 && UnityEngine.Random.value < Warm ? "fire" : Greeted.Contains(key) ? "pals" : "greet";
            List<CrossTalk.Line> made = CrossTalk.PerchMake(kind, whos);
            if (made == null && kind != "greet") made = CrossTalk.PerchMake("greet", whos);
            if (made == null) return false;
            if (Greeted.Count > 500) Greeted.Clear();
            Greeted.Add(key);

            List<TalkBubble.Line> lines = new List<TalkBubble.Line>();
            List<int> by = new List<int>(), to = new List<int>();
            foreach (CrossTalk.Line l in made)
            {
                Bird s = who[l.Who], t = who[l.To];
                lines.Add(new TalkBubble.Line { Munin = s.O.Munin, Text = l.Text, Name = whos[l.Who].NickName, To = whos[l.To].NickName, Bird = s.Go, ToBird = t.Go, Owner = whos[l.Who].P });
                by.Add(l.Who); to.Add(l.To);
            }
            Play(perches, who, lines, by, to);
            return true;
        }

        private static CrossTalk.Who WhoOf(Bird x)
        {
            Player p = PerchView.PlayerOf(x.O.Pid);
            CrossTalk.Who w = new CrossTalk.Who { P = p, Munin = x.O.Munin, Level = Mathf.Max(1, x.O.Level), Owner = x.O.Name, Female = x.O.Female, Voice = x.O.Voice };
            if (p == null)
            {
                ItemDrop.ItemData data = Home.Cache(x.View, x.Seat); // left at home: its own data (the nickname)
                Nicks.Nick nick = data != null ? Nicks.Current(data) : null;
                if (nick != null) { w.Nick = nick.Name; w.Call = nick.Call; }
            }
            return w;
        }

        private static string Key(List<Bird> who)
        {
            List<string> k = new List<string>();
            foreach (Bird b in who) k.Add(b.O.Pid + (b.O.Munin ? "m" : "h"));
            k.Sort(StringComparer.Ordinal);
            return string.Join(",", k.ToArray());
        }

        // ---------------------------------------------------------------- shown here, sent to everybody

        // by/to: each line's speaker and whom it speaks to, as indexes into who
        private static void Play(List<PerchView> perches, List<Bird> who, List<TalkBubble.Line> lines, List<int> by, List<int> to)
        {
            foreach (PerchView p in perches) p.Talked(lines);
            Player me = Player.m_localPlayer;
            bool mine = false;
            foreach (Bird b in who) if (me != null && b.O.Pid == me.GetPlayerID()) mine = true;
            TalkBubble.PlayCrowd(perches, lines, mine);
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(perches.Count);
            foreach (PerchView p in perches) pkg.Write(p.Id);
            pkg.Write(who.Count);
            foreach (Bird b in who) { pkg.Write(perches.IndexOf(b.View)); pkg.Write(b.Seat); pkg.Write(b.O.Munin); }
            pkg.Write(lines.Count);
            for (int i = 0; i < lines.Count; i++) { pkg.Write(by[i]); pkg.Write(to[i]); pkg.Write(lines[i].Text); pkg.Write(lines[i].Name ?? ""); pkg.Write(lines[i].To ?? ""); }
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        internal static void Register()
        {
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnTalk);
        }

        private static void OnTalk(long sender, ZPackage pkg)
        {
            if (sender == ZDOMan.GetSessionID()) return; // shown here already
            Player me = Player.m_localPlayer;
            if (me == null) return;
            try
            {
                int k = Mathf.Min(pkg.ReadInt(), MaxPerches);
                List<PerchView> perches = new List<PerchView>();
                for (int i = 0; i < k; i++)
                {
                    ZDOID id = pkg.ReadZDOID();
                    GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
                    PerchView p = g != null ? g.GetComponent<PerchView>() : null;
                    if (p == null || Vector3.Distance(me.transform.position, p.transform.position) > 60f) return; // not here, or far: nothing to see
                    perches.Add(p);
                }
                if (perches.Count == 0 || !perches[0].HeldBy(sender)) return; // only the deciding perch's holder speaks for them
                int n = Mathf.Min(pkg.ReadInt(), MaxRavens);
                List<GameObject> birds = new List<GameObject>();
                bool mine = false;
                for (int i = 0; i < n; i++)
                {
                    int pi = pkg.ReadInt(), seat = pkg.ReadInt(); bool munin = pkg.ReadBool();
                    if (pi < 0 || pi >= perches.Count) return;
                    Perches.Occupant o = perches[pi].At(seat);
                    GameObject go = perches[pi].BirdAt(seat);
                    if (o == null || o.Munin != munin || go == null) return; // its bird is not drawn here
                    if (o.Pid == me.GetPlayerID()) mine = true;
                    birds.Add(go);
                }
                int m = Mathf.Min(pkg.ReadInt(), CrossTalk.MaxLines);
                List<TalkBubble.Line> lines = new List<TalkBubble.Line>();
                for (int i = 0; i < m; i++)
                {
                    int by = pkg.ReadInt(), to = pkg.ReadInt();
                    string text = CrossTalk.Clean(pkg.ReadString()), name = CrossTalk.Clean(pkg.ReadString()), toName = CrossTalk.Clean(pkg.ReadString());
                    if (by < 0 || by >= birds.Count || to < 0 || to >= birds.Count || text.Length == 0) return;
                    Perches.Occupant so = null;
                    for (int pi = 0; pi < perches.Count && so == null; pi++) for (int s = 0; s < perches[pi].Count; s++) if (perches[pi].BirdAt(s) == birds[by]) { so = perches[pi].At(s); break; }
                    lines.Add(new TalkBubble.Line { Munin = so != null && so.Munin, Text = text, Name = name.Length > 0 ? name : null, To = toName.Length > 0 ? toName : null, Bird = birds[by], ToBird = birds[to], Owner = so != null ? PerchView.PlayerOf(so.Pid) : null });
                }
                if (lines.Count < 2) return;
                foreach (PerchView p in perches) p.Talked(lines);
                TalkBubble.PlayCrowd(perches, lines, mine);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Perch talk could not be read: " + e.Message); }
        }
    }
}
#endif
