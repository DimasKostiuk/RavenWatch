#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The story's own people who live in the world (story-lore.md «Пролог»; the user 2026-11-01). The server keeps each
    // of them once in a world — never two, never none while their place stands — and says where they live; their day
    // is a resident's like anyone's (NpcResident: its duties about its home, the fire in the evening, its bed at night),
    // the story holding them (NpcLevers «protect»: nobody sends them off, hunger does not take them away).
    // Who they are and where they live is data (the user 2026-11-02: no names of the story in the code), StoryFiles kind
    // «people» (assets/story/people/*.txt):
    //   [person heid]
    //   npc: heid                                               its NPC template (NpcDefs)
    //   live: torvald.vohnyshche spawn torvald.lava bed torvald.lizhko   home, where it appears, its bed — while the Hall
    //                                                                    is not kindled
    //   kindled: hall.vohnyshche                                 the same, once the Hall is kindled (HallState.Kindled)
    // A spot is «<place>.<named piece>» (the place's names: HallParts — loaded or not, Loc.NamedAt) or «<place>».
    // Made with its home (nobody within Born m; after BornWait s all the same), moved only where nobody sees it (no player
    // within Unseen m of it or of where it goes, its body not loaded here). Made straight as an object of the world (the
    // server's own, as NpcRescue does): its body comes when someone comes near.
    internal static class StoryPeople
    {
        internal const string Kind = "people";
        internal static float Every = 15f, Unseen = 150f, Born = 40f, BornWait = 30f, Moved = 6f;
        private static float _next;

        internal sealed class Home { public string At = "", Spawn = "", Bed = ""; }
        internal sealed class Person
        {
            public string Id = "", Npc = "", File = "";
            public Home Before, Kindled;
            public float BornWait = -1f;
            public string Why = "";
            internal Home Now { get { return HallState.Kindled && Kindled != null ? Kindled : Before; } }
        }

        private static List<Person> _all;
        internal static List<Person> All { get { if (_all == null) Load(); return _all; } }
        internal static Person Get(string id) { foreach (Person x in All) if (x.Id == id) return x; return null; }

        internal static void Reload() { _all = null; }

        private static void Load()
        {
            _all = new List<Person>();
            foreach (KeyValuePair<string, string> f in StoryFiles.All(Kind, null, false)) Parse(f.Key, f.Value);
            Story.Log("people: " + _all.Count + " read");
        }

        private static void Parse(string file, string text)
        {
            Person cur = null;
            foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(text))
            {
                string line = ln.Value; int n = ln.Key;
                if (line.StartsWith("[person ") && line.EndsWith("]")) { cur = new Person { Id = line.Substring(8, line.Length - 9).Trim(), File = file }; cur.Npc = cur.Id; _all.Add(cur); continue; }
                int c = line.IndexOf(':');
                if (cur == null || c <= 0) { Story.Log("people: " + file + ":" + n + ": не розумію «" + line + "»"); continue; }
                string k = line.Substring(0, c).Trim(), v = line.Substring(c + 1).Trim();
                switch (k)
                {
                    case "npc": cur.Npc = v; break;
                    case "live": cur.Before = HomeOf(v); break;
                    case "kindled": cur.Kindled = HomeOf(v); break;
                    default: Story.Log("people: " + file + ":" + n + ": невідоме поле «" + k + "»"); break;
                }
            }
        }

        // «<spot> [spawn <spot>] [bed <spot>]»
        private static Home HomeOf(string v)
        {
            string[] w = v.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Home h = new Home { At = w.Length > 0 ? w[0] : "" };
            for (int i = 1; i + 1 < w.Length; i += 2)
            {
                if (w[i] == "spawn") h.Spawn = w[i + 1];
                else if (w[i] == "bed") h.Bed = w[i + 1];
            }
            return h;
        }

        // a spot «<place>[.<name>]»: where it is, its object (null — none, or a whole place), its place; why not (null — found)
        private static string Spot(string spot, out Vector3 at, out ZDO zdo, out Loc loc)
        {
            at = Vector3.zero; zdo = null;
            string piece;
            loc = StoryWhere.Place(spot, out piece); // (the same words as everywhere in the story: «hall» is the Hall)
            if (loc == null) return "no place «" + spot + "»";
            if (!loc.Stands) return loc.Id + " does not stand in this world yet";
            if (piece.Length == 0) { Quaternion r; return loc.Anchor(out at, out r) ? null : loc.Id + " has no place"; }
            return loc.NamedAt(piece, out at, out zdo) ? null : "no piece named «" + piece + "» in " + loc.Id + " (its names file)";
        }

        // ---------------------------------------------------------------- the server keeps them
        internal static void ServerTick()
        {
            if (Time.time < _next || ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZoneSystem.instance == null) return;
            _next = Time.time + Every;
            foreach (Person x in All)
            {
                try { Keep(x); }
                catch (Exception e) { Story.Log("people: " + x.Id + ": " + e.Message); }
            }
        }

        // why it is not (yet) where it should be — said in the log once, when it changes
        private static void Why(Person x, string why)
        {
            if (why == x.Why) return;
            x.Why = why;
            if (why.Length > 0) Story.Log("people: " + x.Id + " waits — " + why);
        }

        private static void Keep(Person x)
        {
            Home h = x.Now;
            if (h == null || h.At.Length == 0) { Why(x, "no «live:» in " + x.File); return; }
            Vector3 home, spawn; ZDO dummy, bedZ = null; Loc loc, other;
            string why = Spot(h.At, out home, out dummy, out loc);
            if (why != null) { Why(x, why); return; }
            if (h.Spawn.Length == 0 || Spot(h.Spawn, out spawn, out dummy, out other) != null) spawn = home + Aside(loc, home);
            else { Vector3 to = home - spawn; to.y = 0f; if (to.sqrMagnitude > 1f) spawn += to.normalized * 1.2f; } // (a step off it, towards home: not inside it)
            Vector3 bedAt;
            if (h.Bed.Length > 0 && Spot(h.Bed, out bedAt, out bedZ, out other) != null) bedZ = null;

            ZDO npc = null;
            foreach (ZDO z in NpcCensus.Of(Npcs.HumanPrefab)) // (the shared census: NpcCensus)
            {
                if (z == null || z.GetString(Npcs.KId, "") != x.Npc) continue;
                if (npc == null) { npc = z; continue; }
                ZDO gone = z;
                if (!npc.GetBool(NpcResident.KResident, false) && z.GetBool(NpcResident.KResident, false)) { gone = npc; npc = z; } // (the one living there stays: a second one made by hand goes)
                Own(gone); ZDOMan.instance.DestroyZDO(gone); // (never two of one)
                Story.Log("people: a second " + x.Id + " taken away");
            }

            if (npc == null)
            {
                // it comes as its home does, before anyone is close — and if someone stays there (came, or was put there),
                // after a little while all the same: never a home with nobody in it for good
                if (!Unseen0(spawn, Born))
                {
                    if (x.BornWait < 0f) x.BornWait = Time.time;
                    if (Time.time - x.BornWait < BornWait) { Why(x, "someone within " + Born + " m of its home"); return; }
                }
                x.BornWait = -1f;
                int hash = Npcs.HumanPrefab.GetStableHashCode();
                npc = ZDOMan.instance.CreateNewZDO(spawn + Vector3.up * 0.5f, hash);
                npc.Persistent = true;
                npc.SetPrefab(hash);
                npc.Set(Npcs.KId, x.Npc);
                npc.Set(NpcLevers.KVars, NpcLevers.Protected + "=1");
                NpcKit.Mark(npc);
                Live(npc, home, bedZ);
                NpcCensus.Added(npc);
                Why(x, "");
                Story.Log("people: " + x.Id + " made at " + h.At + " (" + Mathf.RoundToInt(spawn.x) + "," + Mathf.RoundToInt(spawn.z) + ")" + (h.Bed.Length > 0 ? (bedZ != null ? ", its bed found" : ", its bed not found") : ""));
                return;
            }

            if (Utils.DistanceXZ(npc.GetVec3(NpcRules.KHome, npc.GetPosition()), home) < Moved) { Why(x, ""); return; } // (lives there already)
            if (!Unseen0(npc.GetPosition(), Unseen) || !Unseen0(spawn, Unseen)) { Why(x, "someone within " + Unseen + " m of it or of its new home"); return; } // (moves when nobody sees it go)
            // its body still loaded on this game (the host's own land reaches farther than Unseen): its owner writes where the
            // body stands into it each frame — a move now would be undone at once. Once its land is let go
            if (ZNetScene.instance != null && ZNetScene.instance.FindInstance(npc.m_uid) != null) { Why(x, "its body is still loaded here — it moves once its land is let go"); return; }
            Own(npc);
            npc.SetPosition(spawn + Vector3.up * 0.5f);
            Live(npc, home, bedZ);
            Why(x, "");
            Story.Log("people: " + x.Id + " moved to " + h.At);
        }

        // a resident there (the server's write: nobody runs it now — nobody is near)
        private static void Live(ZDO z, Vector3 home, ZDO bed)
        {
            z.Set(NpcResident.KResident, true);
            z.Set(NpcRules.KHome, home);
            z.Set(NpcResident.KSettledAt, (long)NpcRescue.Now);
            NpcKit.SetRef(z, NpcResident.KBed, bed != null ? bed.m_uid : ZDOID.None);
            NpcKit.SetRef(z, NpcResident.KHeart, ZDOID.None);
        }

        // a step off its home towards the middle of the place (not in a fire)
        private static Vector3 Aside(Loc l, Vector3 home)
        {
            Vector3 at; Quaternion rot;
            Vector3 d = l != null && l.Anchor(out at, out rot) ? at - home : Vector3.zero;
            d.y = 0f;
            return (d.sqrMagnitude > 1f ? d.normalized : Vector3.forward) * 2.5f;
        }

        private static bool Unseen0(Vector3 p, float far)
        {
            foreach (ZDO c in ZNet.instance.GetAllCharacterZDOS()) if (c != null && Utils.DistanceXZ(c.GetPosition(), p) < far) return false;
            return true;
        }

        // the author's look: `raven story people [id]` (and `raven story heid`) — where each is, where it lives, what it waits for
        internal static string Describe(Player p, string only)
        {
            if (ZDOMan.instance == null) return "Нема світу.";
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Люди сюжету (" + All.Count + ")" + (HallState.Kindled ? " · Зала розпалена" : " · Зала ще не розпалена") + ":");
            foreach (Person x in All)
            {
                if (only.Length > 0 && x.Id != only) continue;
                Home h = x.Now;
                sb.Append("\n").Append(x.Id).Append(" (").Append(x.File).Append("): живе ").Append(h != null ? h.At : "—");
                int n = 0;
                foreach (ZDO z in NpcCensus.Of(Npcs.HumanPrefab))
                {
                    if (z == null || z.GetString(Npcs.KId, "") != x.Npc) continue;
                    n++;
                    Vector3 at = z.GetPosition(), home = z.GetVec3(NpcRules.KHome, at);
                    sb.Append("\n  стоїть ").Append(Mathf.RoundToInt(at.x)).Append(",").Append(Mathf.RoundToInt(at.y)).Append(",").Append(Mathf.RoundToInt(at.z))
                      .Append(" — ").Append(Mathf.RoundToInt(Vector3.Distance(at, p.transform.position))).Append(" м від тебе; дім ")
                      .Append(Mathf.RoundToInt(home.x)).Append(",").Append(Mathf.RoundToInt(home.z)).Append(" (").Append(Mathf.RoundToInt(Utils.DistanceXZ(at, home))).Append(" м від нього)")
                      .Append("; тіло ").Append(ZNetScene.instance != null && ZNetScene.instance.FindInstance(z.m_uid) != null ? "завантажене тут" : "не завантажене тут")
                      .Append("; мешканець ").Append(z.GetBool(NpcResident.KResident, false) ? "так" : "ні");
                }
                if (n == 0) sb.Append("\n  у світі нема").Append(ZNet.instance != null && !ZNet.instance.IsServer() ? " (ти не сервер — видно лише завантажене)" : "");
                sb.Append("\n  чекає: ").Append(x.Why.Length > 0 ? x.Why : "нічого");
            }
            foreach (Loc l in Locs.All) if (l.Follows.Length > 0 && l.Built) sb.Append("\n").Append(l.Title).Append(": ").Append(l.WordOf(HallState.StateOf(l))).Append(HallState.ShutOf(l) ? " (зміна чекає за кадром)" : "");
            return sb.ToString();
        }

        private static void Own(ZDO z) { if (z.GetOwner() != ZDOMan.GetSessionID()) z.SetOwner(ZDOMan.GetSessionID()); }
    }
}
#endif
