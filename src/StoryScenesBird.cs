#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (StoryScenes), its bird: the steps, the orders here and for those near (RPC), a seat it is seen on.
    internal static partial class StoryScenes
    {
        private static void BirdStep(Player p, Step st, string[] w)
        {
            switch (w[1])
            {
                case "come":
                    {
                        // bird come [from=60] [up=18] land [metres=5]
                        float from = w.Length > 2 ? F(w[2], 60f) : 60f, up = w.Length > 3 ? F(w[3], 18f) : 18f;
                        int li = Array.IndexOf(w, "land");
                        float before = li >= 0 && w.Length > li + 1 ? F(w[li + 1], 5f) : 5f;
                        Vector3 fwd = Flat(GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward);
                        Vector3 start = p.transform.position - fwd * from + Vector3.up * up;
                        int ai = Array.IndexOf(w, "at");
                        Vector3 seat;
                        bool hover = false;
                        if (ai >= 0 && w.Length > ai + 1) { if (!StoryWhere.Top(w[ai + 1], out seat)) { Note("bird come at: нема місця «" + w[ai + 1] + "» — крок пропущено"); break; } }
                        else if (!Perch(p, fwd, before, out seat)) hover = true; // (nothing to sit on in sight — grass would hide it: in the air before you)
                        if (hover) seat = p.transform.position + fwd * HoverAhead + Vector3.up * 2.2f;
                        float yaw = Quaternion.LookRotation(Flat(p.transform.position - seat), Vector3.up).eulerAngles.y;
                        Act("come", V(start) + " " + V(seat) + " " + F(yaw) + (hover ? " 1" : " 0"));
                        if (hover) Act("look", V(p.GetEyePoint()));
                        break;
                    }
                case "land":
                    {
                        Vector3 at;
                        if (!StoryWhere.Top(w[2], out at)) { Note("bird land: нема місця «" + w[2] + "» — крок пропущено"); break; }
                        float yaw = Quaternion.LookRotation(Flat(p.transform.position - at), Vector3.up).eulerAngles.y;
                        Act("land", V(at) + " " + F(yaw));
                        break;
                    }
                case "look": if (_bird != null) Act("look", V(w[2] == "player" ? p.transform.position : _bird.transform.position + _bird.transform.forward)); break;
                case "say":
                    {
                        string line = Lang.T(Rest(st.Text, 2));
                        _waitFor = Time.time + ReadTime(line);
                        Act("say", line);
                        break;
                    }
                case "leave":
                    {
                        Vector3 to;
                        if (w[2] == "up" || !StoryWhere.Find(w[2], out to)) to = _bird != null ? _bird.transform.position + _bird.transform.forward * 10f : p.transform.position;
                        Act("leave", V(to));
                        _bird = null; // (on its own now: gone in the sky)
                        break;
                    }
                case "gone": Act("gone", ""); _bird = null; break;
                case "circle":
                    {
                        float sec = F(w[2], 30f), r = w.Length > 3 ? F(w[3], 4.5f) : 4.5f, up = w.Length > 4 ? F(w[4], 3.5f) : 3.5f;
                        _waitFor = Time.time + sec;
                        Act("circle", F(r) + " " + F(up));
                        break;
                    }
            }
        }

        // ---------------------------------------------------------------- the bird's orders: here and for those near
        private static void Act(string what, string args)
        {
            _bird = Apply(_bird, what, args, Player.m_localPlayer);
            if (ZRoutedRpc.instance == null) return;
            Player p = Player.m_localPlayer;
            ZPackage pkg = new ZPackage();
            pkg.Write(p != null ? p.GetPlayerID() : 0L);
            pkg.Write(p != null ? p.transform.position : Vector3.zero);
            pkg.Write(what);
            pkg.Write(args);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        private static StoryBird Apply(StoryBird b, string what, string args, Player owner)
        {
            string[] a = args.Split(' ');
            switch (what)
            {
                case "come":
                    if (b != null) b.Gone();
                    return a.Length >= 7 ? StoryBird.Come(PV(a, 0), PV(a, 3), F(a[6], 0f), a.Length > 7 && a[7] == "1") : null;
                case "land": if (b != null && a.Length >= 4) b.Land(PV(a, 0), F(a[3], 0f)); return b;
                case "look": if (b != null && a.Length >= 3) b.Face(PV(a, 0)); return b;
                case "say": if (b != null) b.Say(args); return b;
                case "circle": if (b != null && a.Length >= 2) b.Circle(owner, F(a[0], 4.5f), F(a[1], 3.5f)); return b;
                case "leave": if (b != null && a.Length >= 3) b.Leave(PV(a, 0)); return null;
                case "gone": if (b != null) b.Gone(); return null;
                case "free": if (b != null) b.Release(); return null;
            }
            return b;
        }

        internal static void RegisterRpcs() { ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnAct); }

        private static void OnAct(long sender, ZPackage pkg)
        {
            try
            {
                long who = pkg.ReadLong();
                Vector3 at = pkg.ReadVector3();
                string what = pkg.ReadString(), args = pkg.ReadString();
                Player me = Player.m_localPlayer;
                if (me == null || who == me.GetPlayerID()) return; // (our own: done already)
                StoryBird b;
                _mirrors.TryGetValue(who, out b);
                if (b == null && (what != "come" || Vector3.Distance(at, me.transform.position) > Seen)) return; // (too far to see it come)
                b = Apply(b, what, args, PerchView.PlayerOf(who));
                if (b != null) { b.Owner = who; _mirrors[who] = b; } else _mirrors.Remove(who);
            }
            catch (Exception e) { Story.Log("scene act: " + e.Message); }
        }

        // a seat it is seen on, before you (3–12 m, the way you look, ±25°): the top of something standing out of the
        // ground half a metre to three and a half (a rock, a stump, a log, a fence, a wall, a cart), level enough, sky
        // over it, in your sight; the nearest to «before» metres. On the bare ground the meadow's grass hides a raven.
        internal static float PerchMin = 0.45f, PerchMax = 3.5f, HoverAhead = 4.5f;
        private static bool Perch(Player p, Vector3 fwd, float before, out Vector3 seat)
        {
            seat = Vector3.zero;
            if (ZoneSystem.instance == null) return false;
            Vector3 eye = p.GetEyePoint(), right = Vector3.Cross(Vector3.up, fwd);
            float best = float.MaxValue;
            RaycastHit hit, block;
            for (float dist = 3f; dist <= 12f; dist += 1f)
                for (int s = -4; s <= 4; s++)
                {
                    Vector3 at = p.transform.position + fwd * dist + right * (s * dist * 0.11f);
                    if (!Physics.Raycast(at + Vector3.up * 8f, Vector3.down, out hit, 14f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) continue;
                    float raise = hit.point.y - ZoneSystem.instance.GetGroundHeight(hit.point);
                    if (raise < PerchMin || raise > PerchMax || Vector3.Angle(hit.normal, Vector3.up) > 35f) continue;
                    if (hit.collider.GetComponentInParent<Character>() != null) continue; // (not on a beast's back)
                    Rigidbody rb = hit.collider.attachedRigidbody;
                    if (rb != null && !rb.isKinematic) continue;                           // (nor on a thing that rolls)
                    if (Physics.Raycast(hit.point + Vector3.up * 0.3f, Vector3.up, 3f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) continue; // (a roof over it)
                    Vector3 top = hit.point + Vector3.up * 0.25f;
                    if (Physics.Linecast(eye, top, out block, RavenMotor.Mask, QueryTriggerInteraction.Ignore) && (block.point - top).magnitude > 0.5f) continue; // (out of sight)
                    float score = Mathf.Abs(dist - before) + Mathf.Abs(s) * 0.3f;
                    if (score < best) { best = score; seat = hit.point; }
                }
            return best < float.MaxValue;
        }
    }
}
#endif
