#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HuginWhispers
{
    // A person's feet: links in the way finder's map where a body walks but the map has a gap (2026-11-06, measured: a raised stone pier, an
    // upper floor and its stairs were islands — a way ended 6 m short, 0.5 m below, for the game's walker and for ours; people dithered there
    // and were put across in the end). Where a walk's way ends short, the way back from its goal ends on the far side of the gap (Bridge
    // finds both ends); when a body could walk the straight line between them — ground under every LinkStep m, no rise or drop over StepRise
    // from one to the next (stairs are such a line), room for a body all along it — that line becomes a link of the map (NavMesh.AddLink, as
    // the game stitches its own tiles), both ways, for our walker only. Every way asked after it (walks, Have, Way: a pantry, a seat upstairs)
    // goes over it as over a floor. A failed line is not looked at again for FailKeep s. Links are kept LinkKeep s, then dropped (a base
    // changes: the next walk that needs it makes it again); a new way finder (a world loaded) drops them all. At most MostLinks.
    internal sealed partial class NpcWalk
    {
        internal static float LinkFar = 7f, LinkStep = 0.3f, StepRise = 0.6f, LinkKeep = 900f, FailKeep = 60f;
        // A ledge a body steps down from and not up onto (measured 2026-11-11: a dais 0.8 m over the floor round it, a chair on it — whoever
        // got there had no way off, «a step of -0.80 m», four walks ended in a puff): a line that goes down by one step of up to StepFall m is a
        // link too, but one way only, down. Up it stays a wall: the body does not climb it (NpcWalk.StepUp: 0.75 m at most). The body is taken
        // down it smoothly (NpcWalk.StepDown), no fall
        internal static float StepFall = 1f;
        private const int MostLinks = 200;
        private sealed class Link { public Vector3 A, B; public NavMeshLinkInstance It; public float At; public bool Down; } // (Down: from A to B only)
        private static readonly List<Link> _links = new List<Link>();
        private static readonly Dictionary<Pair, float> _linkFailed = new Dictionary<Pair, float>();
        private static Pathfinding _linksOf;
        private static readonly int PerfLink = NpcPerf.Id("path links (містки карти)");

        // the two ends of a gap a walk met: a link made when a body walks it (true: there is one now)
        internal static bool LinkAcross(Vector3 a, Vector3 b)
        {
            Pathfinding pf = Pathfinding.instance;
            if (pf == null || !NpcKit.OwnWalker) return false; // (links only for our own walker: the game's creatures keep their map as it is)
            if (!ReferenceEquals(pf, _linksOf)) { DropLinks(); _linksOf = pf; }
            float now = Time.time;
            for (int i = _links.Count - 1; i >= 0; i--)
            {
                Link l = _links[i];
                if (now - l.At > LinkKeep) { Unlink(l); _links.RemoveAt(i); continue; }
                if (((l.A - a).sqrMagnitude < 1f && (l.B - b).sqrMagnitude < 1f) || (!l.Down && (l.A - b).sqrMagnitude < 1f && (l.B - a).sqrMagnitude < 1f)) return true; // (there already — a way down is no way up)
            }
            Pair key = Key(a, b);
            float failed;
            if (_linkFailed.TryGetValue(key, out failed) && now - failed < FailKeep) return false;
            if (Utils.DistanceXZ(a, b) > LinkFar) return false;
            long perf = NpcPerf.Now();
            bool down;
            string why = Walkable(a, b, out down);
            NpcPerf.Add(PerfLink, perf);
            if (why != null)
            {
                if (_linkFailed.Count > 512) _linkFailed.Clear();
                _linkFailed[key] = now;
                if (Watch) Write("halts.txt", System.DateTime.Now.ToString("HH:mm:ss") + "\tLINK no\t" + Npcs.V(a) + " → " + Npcs.V(b) + "\t" + why);
                return false;
            }
            if (_links.Count >= MostLinks) { Unlink(_links[0]); _links.RemoveAt(0); }
            NavMeshLinkData d = new NavMeshLinkData
            {
                startPosition = a, endPosition = b, width = 0f, costModifier = -1f, bidirectional = !down, area = 0,
                agentTypeID = NpcKit.Filter(pf, NpcKit.Walker).agentTypeID
            };
            NavMeshLinkInstance it = NavMesh.AddLink(d);
            if (!NavMesh.IsLinkValid(it)) { _linkFailed[key] = now; Npcs.Log("way finder: a link not taken by the map " + Npcs.V(a) + " → " + Npcs.V(b)); return false; }
            _links.Add(new Link { A = a, B = b, It = it, At = now, Down = down });
            Npcs.Log("way finder: linked " + Npcs.V(a) + " → " + Npcs.V(b) + " (" + Utils.DistanceXZ(a, b).ToString("0.0") + " m, rise " + (b.y - a.y).ToString("0.0") + " m" + (down ? ", down only" : "") + ")");
            if (Watch) Write("halts.txt", System.DateTime.Now.ToString("HH:mm:ss") + "\tLINK made\t" + Npcs.V(a) + " → " + Npcs.V(b));
            _kept.Clear(); // (answers asked before it: asked afresh)
            return true;
        }

        private static void Unlink(Link l) { if (NavMesh.IsLinkValid(l.It)) NavMesh.RemoveLink(l.It); } // (a world unloaded may have taken it already)

        // the people's kind of walker made anew (its slope tried live: NpcKit.Remake): what was linked and answered for the old one is not the new one's
        internal static void WalkerChanged() { DropLinks(); _kept.Clear(); }

        private static void DropLinks()
        {
            foreach (Link l in _links) Unlink(l);
            _links.Clear(); _linkFailed.Clear();
        }

        // does a body walk the straight line a → b: null if so, else why not
        private static readonly int LinkGround = LayerMask.GetMask("terrain", "static_solid", "piece", "Default");
        private static string Walkable(Vector3 a, Vector3 b, out bool down)
        {
            down = false;
            Vector3 flat = b - a; flat.y = 0f;
            float len = flat.magnitude;
            if (len < 0.05f) return Mathf.Abs(b.y - a.y) <= StepRise ? null : "straight up";
            int n = Mathf.Max(2, Mathf.CeilToInt(len / LinkStep));
            float last = a.y;
            for (int i = 1; i <= n; i++)
            {
                float k = (float)i / n;
                Vector3 p = a + flat * k; p.y = Mathf.Lerp(a.y, b.y, k);
                float top = Mathf.Max(p.y, last) + StepRise + 0.4f; // (looked for from just over a step: not the roof above)
                RaycastHit h;
                if (!Physics.Raycast(new Vector3(p.x, top, p.z), Vector3.down, out h, top - (Mathf.Min(p.y, last) - StepFall - 0.4f), LinkGround, QueryTriggerInteraction.Ignore)) return "no ground at " + k.ToString("0.00");
                if (Vector3.Angle(h.normal, Vector3.up) > 60f) return "a wall at " + k.ToString("0.00");
                float step = h.point.y - last;
                if (step > StepRise || step < -StepFall || (step < -StepRise && down)) return "a step of " + step.ToString("0.00") + " m at " + k.ToString("0.00"); // (one step down a line at most: two of them are a fall)
                if (step < -StepRise) down = true;
                last = h.point.y;
                string held = Blocker(h.point, (down ? StepFall : StepRise) + 0.4f); if (held != null) return "no room for a body at " + k.ToString("0.00") + ": " + held; // (room over a step's height: the edge of the higher floor itself is what it steps onto — measured 2026-11-06: 8 of 9 links refused at the edge)
            }
            return Mathf.Abs(last - b.y) <= StepRise ? null : "ends off its far end";
        }

        // what stands where a body would be over that ground (null: room). A door people may use is no blocker: the walk opens it on its way
        // (NpcWalkDoors) — a shut door is a wall to the game's map, and a room behind one was an island (2026-11-06, the links refused there)
        private static readonly Collider[] _hold = new Collider[8];
        private static string Blocker(Vector3 ground, float from) // (from: m over the ground the look begins — over the ledge it has just come down from, too)
        {
            int n = Physics.OverlapCapsuleNonAlloc(ground + Vector3.up * Mathf.Min(from, 1.5f), ground + Vector3.up * 1.6f, 0.25f, _hold, LinkGround, QueryTriggerInteraction.Ignore); // heavy: one small capsule a step of a line looked at, only when a walk's way ends short of a gap, a failed line not again for FailKeep s
            string held = null;
            for (int i = 0; i < n; i++)
            {
                Collider c = _hold[i];
                if (c == null) continue;
                Door d = c.GetComponentInParent<Door>();
                if (d != null && NpcDoors.Usable(d, null)) continue;
                Piece p = c.GetComponentInParent<Piece>();
                held = (held == null ? "" : held + ", ") + (p != null ? Utils.GetPrefabName(p.gameObject) : c.name) + (d != null ? " (door, not for people)" : "");
            }
            return held;
        }

        internal static string Links() { return "Містків карти: " + _links.Count + " (не вдалось: " + _linkFailed.Count + ")"; }
    }
}
#endif
