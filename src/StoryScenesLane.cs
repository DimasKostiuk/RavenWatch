#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (StoryScenes), a lane: a straight line over open ground that several things of one scene go along — one walks out
    // along it, others fly in down it, the camera rides beside it (chapter 1, the ambush: the user 2026-11-14 — Ketil walks away from you, the
    // sisters' falcons come low from where he walks to, the camera goes with them from the side).
    //   lane <name> <from> <toward> [clear <m>=45] [wide <m>=1.3]   laid from one place toward another, level: the way itself when nothing stands in it for
    //                                       «clear» m, «wide» m to either side (a tree, a rock, a wall, water, a steep bank), else the nearest way to either side that is
    //                                       clear (LaneTurn° apart, up to LaneTurns of them); none clear: the way itself, and a note
    //   lane.<name>@<m>[@<side>[@<up>]]     a place word: so many m along it from its start (below zero: behind it), so many m to its right (below zero: left),
    //                                       on the ground or so many m over it (a cry out of the sky, a look up at it)
    //   camera rail <lane> <who> [side <m>=6] [up <m>=1] [lead <m>=0] [left|right] [cut|blend N] [fov N]
    //                                       the lens rides a rail beside the lane, that far from it, that high over the ground under it, abreast of
    //                                       «who» (lead: m ahead of it, the way it moves along the lane), looking at it; the side with less in the
    //                                       lens's way, unless one is said (left|right of the lane as it runs from its start). «who» a falcon: when it
    //                                       turns into a person the shot goes on with her (StoryScenes.FalconAt)
    // A lane is the scene's: gone with it (Release).
    internal static partial class StoryScenes
    {
        private sealed class Lane { public Vector3 From, Dir; }
        private static readonly Dictionary<string, Lane> _lanes = new Dictionary<string, Lane>(); // session: this scene's lanes by name
        internal static float LaneClear = 45f, LaneTurn = 15f, LaneStep = 3f, LaneRoom = 1.3f, LaneBank = 1.6f; // m it must be clear · ° between the ways tried · m between its samples · m clear round each, over the ground · m the ground may rise or fall from one sample to the next
        internal static int LaneTurns = 4; // (to either side)

        // load time (its entry: StoryScenesKinds): its name a plain word, its second place a word of the story (the first: the entry's PlaceAt)
        private static string LaneCheck(Step st)
        {
            return Story.GoodName(st.W[1]) && st.W[1].IndexOf('@') < 0 && st.W[1].IndexOf('.') < 0 ? StoryWhere.Why(st.W[3]) : "lane: погана назва «" + st.W[1] + "»";
        }

        private static void LaneLay(Player p, Step st)
        {
            string[] w = st.W;
            Vector3 from, toward;
            if (!StoryWhere.Find(w[2], out from) || !StoryWhere.Find(w[3], out toward)) { Note("lane: нема місця «" + w[2] + "» чи «" + w[3] + "» — крок пропущено"); return; }
            int ci = Array.IndexOf(w, "clear"), wi = Array.IndexOf(w, "wide");
            float clear = ci > 3 && w.Length > ci + 1 ? F(w[ci + 1], LaneClear) : LaneClear, wide = wi > 3 && w.Length > wi + 1 ? F(w[wi + 1], LaneRoom) : LaneRoom;
            Vector3 way = Flat(toward - from), best = way; string what = null; float turned = 0f; bool found = false;
            for (int k = 0; k <= 2 * LaneTurns && !found; k++)
            {
                float deg = LaneTurn * ((k + 1) / 2) * (k % 2 == 1 ? 1f : -1f);
                Vector3 d = Quaternion.AngleAxis(deg, Vector3.up) * way;
                string blocked = LaneBlocked(from, d, clear, wide);
                if (blocked == null) { best = d; turned = deg; found = true; }
                else if (k == 0) what = blocked;
            }
            from.y = LaneGround(from);
            _lanes[w[1]] = new Lane { From = from, Dir = best };
            if (!found) Note("lane " + w[1] + ": на " + F(clear) + " м нема чистої смуги в жодний бік (" + what + ") — лишилась пряма");
            else if (turned != 0f) Track("lane " + w[1] + ": пряма зайнята (" + what + ") — смуга повернута на " + F(turned) + "°");
            Story.Log("lane " + w[1] + ": from " + V(from) + ", way " + F(Mathf.Atan2(best.x, best.z) * Mathf.Rad2Deg) + "°" + (found ? turned != 0f ? " (turned " + F(turned) + "°: " + what + ")" : "" : " (NOT clear: " + what + ")"));
        }

        private static float LaneGround(Vector3 at) { float g; return ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out g) ? g : at.y; }

        // what stands in a lane's way within so many m (null: nothing): water, a steep bank, a trunk, a rock, a wall. Its first metres are left
        // out: those who begin there stand there
        private static string LaneBlocked(Vector3 from, Vector3 dir, float clear, float wide)
        {
            Quaternion turn = Quaternion.LookRotation(dir);
            Vector3 half = new Vector3(Mathf.Max(0.3f, wide), LaneRoom, LaneStep * 0.5f); // (that far to either side of it — a camera's rail beside it wants it wide —, from knee height up to a tall person's reach)
            if (ZoneSystem.instance == null) return null;
            int mask = LayerMask.GetMask("Default", "static_solid", "piece"); // (trunks, rocks, walls — not the ground itself, not a twig or a stone to pick up)
            float was = LaneGround(from);
            for (float m = LaneStep; m <= clear; m += LaneStep)
            {
                Vector3 at = from + dir * m;
                float g;
                if (!ZoneSystem.instance.GetGroundHeight(at, out g)) return "the land " + F(m) + " m off is not made yet";
                if (g < ZoneSystem.instance.m_waterLevel + 0.2f) return "water " + F(m) + " m off";
                if (Mathf.Abs(g - was) > LaneBank) return "a bank " + F(m) + " m off";
                was = g;
                Collider[] hit = Physics.OverlapBox(new Vector3(at.x, g + LaneRoom + 0.5f, at.z), half, turn, mask, QueryTriggerInteraction.Ignore); // heavy: clear / LaneStep boxes a way tried, up to 2·LaneTurns + 1 ways, once as the lane is laid
                if (hit.Length > 0) return hit[0].name + " " + F(m) + " m off";
            }
            return null;
        }

        // «lane.<name>@<m>[@<side>]» (StoryWhere.Prefixed): the rest of the word → where
        internal static bool LaneAt(string rest, out Vector3 at)
        {
            at = Vector3.zero;
            string[] part = rest.Split('@');
            Lane l;
            if (!_lanes.TryGetValue(part[0], out l)) return false;
            float along = part.Length > 1 ? F(part[1], 0f) : 0f, side = part.Length > 2 ? F(part[2], 0f) : 0f;
            at = l.From + l.Dir * along + Vector3.Cross(Vector3.up, l.Dir) * side;
            at.y = LaneGround(at) + (part.Length > 3 ? F(part[3], 0f) : 0f);
            return true;
        }

        // «camera rail <lane> <who> …»: the lens beside the lane, abreast of the one filmed, low over the ground (its header says the words)
        internal static float RailSide = 6f, RailUp = 1f, RailFov = 50f, RailEase = 8f, RailReach = 24f;
        private static void RailShot(Player p, string[] w, float blend, float fov)
        {
            Lane l;
            if (w.Length < 4 || !_lanes.TryGetValue(w[2], out l)) { Note("camera rail: нема смуги «" + (w.Length > 2 ? w[2] : "") + "» (спершу lane <назва> …) — крок пропущено"); return; }
            string who = w[3];
            Vector3 first;
            if (!StoryWhere.Find(who, out first)) { Note("camera rail: нема «" + who + "» — крок пропущено"); return; }
            float side = RailWord(w, "side", RailSide), up = RailWord(w, "up", RailUp), lead = RailWord(w, "lead", 0f);
            Vector3 right = Vector3.Cross(Vector3.up, l.Dir);
            float sign = Array.IndexOf(w, "left") > 3 ? -1f : Array.IndexOf(w, "right") > 3 ? 1f : 0f;
            float from = Vector3.Dot(first - l.From, l.Dir);
            if (sign == 0f)
            {
                // (the side with less in the lens's way along what it will ride: RailReach m toward the lane's start from where the one filmed is now)
                int mask = LensMask, onRight = 0, onLeft = 0;
                for (float m = from; m >= Mathf.Max(0f, from - RailReach); m -= 3f) // heavy: up to 2 × RailReach / 3 spheres, once as the shot begins
                {
                    Vector3 a = l.From + l.Dir * m + right * side, b = l.From + l.Dir * m - right * side;
                    if (Physics.CheckSphere(new Vector3(a.x, LaneGround(a) + up, a.z), LensRoom * 2f, mask, QueryTriggerInteraction.Ignore)) onRight++;
                    if (Physics.CheckSphere(new Vector3(b.x, LaneGround(b) + up, b.z), LensRoom * 2f, mask, QueryTriggerInteraction.Ignore)) onLeft++;
                }
                sign = onLeft < onRight ? -1f : 1f;
                if (onLeft + onRight > 0) Track("camera rail: на шляху лінзи праворуч " + onRight + ", ліворуч " + onLeft + " — їде " + (sign > 0f ? "праворуч" : "ліворуч"));
            }
            bool thing = StoryWhere.IsLow(who);
            float aimUp = thing ? 0f : PlaceUp;
            Vector3 seen = first, lens = Vector3.zero; bool begun = false; float wasAlong = from, moves = -1f; // (moves: which way along the lane the one filmed goes — toward its start till it is seen to go the other way)
            Func<Vector3> aim = () => { Vector3 now; if (StoryWhere.Find(who, out now)) seen = now; return seen + Vector3.up * aimUp; };
            float f = fov > 1f ? fov : RailFov, near = side; int lensMask = LensMask;
            // (its own pose, not the shots' «nothing between the lens and its aim»: a trunk passing before a riding lens is a picture; the lens
            //  jumping in onto the bird to get round that trunk is not — the first run, 2026-11-14. What it does keep: never inside a trunk or a rock —
            //  there it rides nearer the lane, as far in as it must, and out again after)
            StoryCamera.Shot(() =>
            {
                Vector3 at = aim();
                float along = Vector3.Dot(at - l.From, l.Dir);
                if (Mathf.Abs(along - wasAlong) > 0.05f) { moves = along > wasAlong ? 1f : -1f; wasAlong = along; }
                Vector3 on = l.From + l.Dir * (along + lead * moves);
                float want = side;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 c = on + right * (want * sign);
                    if (!Physics.CheckSphere(new Vector3(c.x, LaneGround(c) + up, c.z), RailTrunk, lensMask, QueryTriggerInteraction.Ignore)) break; // heavy: up to three spheres a frame, only while this shot is on
                    want -= RailIn;
                }
                near = Mathf.Lerp(near, Mathf.Max(1.5f, want), 1f - Mathf.Exp(-RailEase * Time.deltaTime));
                Vector3 to = on + right * (near * sign);
                to.y = LaneGround(to) + up;
                lens = begun ? Vector3.Lerp(lens, to, 1f - Mathf.Exp(-RailEase * Time.deltaTime)) : to;
                begun = true;
                Vector3 d = at - lens;
                return new StoryCamera.Pose { At = lens, Rot = d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity, Fov = f };
            }, blend);
        }
        internal static float RailTrunk = 0.7f, RailIn = 1.4f; // m clear round the riding lens · m nearer the lane it rides where something stands on its rail

        private static float RailWord(string[] w, string word, float def)
        {
            int i = Array.IndexOf(w, word);
            return i > 3 && w.Length > i + 1 ? F(w[i + 1], def) : def;
        }
    }
}
#endif
