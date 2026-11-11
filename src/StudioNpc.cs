#if RAVEN_ITEM && RAVEN_NPC
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Студія» on an NPC (story tests: its body, its gait, its sitting); its cameras (PlaceBody) are every body's, the player's in a scene check too:
    //   nlook <name> <who> [views]                 — the NPC as it stands now (who: id, tag or near)
    //   nfilm <name> <who> [sec=4] [fps=2] [views] — frames in time, one sheet per view; the report says what it was doing
    //   nskin <name> <who>                          — its worn pieces' skins: the bones each is bound to, any outside its body or
    //                                                far from it (a strap stretched to the ground: what it is tied to)
    // Views: front (its face), back, side, left, top, three; close: hands (over the shoulder), handl, handr, chest, chestside, chestleft and chesttop
    // (its chest, head and both hands, all in the picture, from the front and from its right: folded arms, a letter held), contact (close on
    // both hands from its right and a little ahead: a palm on a door, what it touches), head, headside, headtop (the head close and what comes to it), fingerr, fingerl (the fingers close), holdr, holdl (a hand from before and below: what it holds a thing with). Framed on its
    // body (the collider, or the bone it is about), not on where you are.
    internal static partial class Studio
    {
        private static NpcActor NpcFor(string who)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return null;
            return who == "near" ? Npcs.Nearest(p.transform.position, 60f) : Npcs.Find(who);
        }

        // cameras round any body (a person, the player, the mannequin): one placing for every view, so a scene films the player as the stage films a person
        private static void PlaceBody(Rig r, Component a)
        {
            Transform v = a.transform;
            CapsuleCollider cap = a.GetComponent<CapsuleCollider>();
            float h = cap != null ? cap.height * v.lossyScale.y : 1.9f;
            float sizeWhole = Mathf.Clamp(h + 0.4f, 1.2f, 6f), size = sizeWhole;
            Vector3 whole = v.position + Vector3.up * h * 0.5f, mid = whole;
            float d = size * 2.1f;
            Vector3 fwd = Vector3.ProjectOnPlane(v.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            for (int i = 0; i < r.Cams.Count; i++)
            {
                Vector3 from;
                string view = r.Views[i];
                mid = whole; size = sizeWhole; d = size * 2.1f;
                if (view == "chest" || view == "chestside" || view == "chestleft" || view == "chesttop") // (close, the chest and both arms: what the hands do before the body)
                {
                    Humanoid body = a.GetComponent<Humanoid>();
                    Animator an = body != null ? body.GetComponentInChildren<Animator>() : null;
                    Transform chest = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, "Spine2") : null;
                    if (chest != null)
                    {
                        Bounds fit = new Bounds(chest.position, Vector3.zero); // (all of it in the picture: a hand reached forward was cut off at the edge of a frame set on the chest)
                        foreach (string bone in new[] { "Head", "LeftHand", "RightHand", "LeftHandMiddle3", "RightHandMiddle3" }) { Transform t = BodyClips.SkeletonBone(body, an.transform, bone); if (t != null) fit.Encapsulate(t.position); }
                        mid = fit.center; size = Mathf.Max(0.95f, Mathf.Max(fit.size.y, Vector3.ProjectOnPlane(fit.size, Vector3.up).magnitude) + 0.3f); d = size * 2.1f;
                    }
                }
                if (view == "head" || view == "headside" || view == "headtop") // (close on the head and whatever comes to it: a hand on the brow, an arm over it)
                {
                    Humanoid body = a.GetComponent<Humanoid>();
                    Animator an = body != null ? body.GetComponentInChildren<Animator>() : null;
                    Transform head = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, "Head") : null;
                    if (head != null) { mid = head.position + Vector3.up * 0.08f; size = 0.6f; d = size * 2.1f; }
                }
                if (view == "contact")
                {
                    Humanoid body = a.GetComponent<Humanoid>();
                    Animator an = body != null ? body.GetComponentInChildren<Animator>() : null;
                    Transform lh = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, "LeftHand") : null, rh = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, "RightHand") : null;
                    if (lh != null && rh != null) { mid = (lh.position + rh.position) * 0.5f; size = Mathf.Max(0.6f, Vector3.Distance(lh.position, rh.position) + 0.35f); d = size * 2.1f; }
                }
                if (view == "hands" || view == "handl" || view == "handr") // (close, on what the hands hold)
                {
                    Humanoid body = a.GetComponent<Humanoid>();
                    Animator an = body != null ? body.GetComponentInChildren<Animator>() : null;
                    Transform lh = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, "LeftHand") : null, rh = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, "RightHand") : null;
                    if (lh != null && rh != null)
                    {
                        mid = view == "handl" ? lh.position : view == "handr" ? rh.position : (lh.position + rh.position) * 0.5f;
                        size = view == "hands" ? 1.1f : 0.5f; d = size * 2.1f;
                    }
                }
                if (view == "feet") // (both feet close, from the side and a little ahead: a slide, a sole up, a foot off the game's stance)
                {
                    Humanoid body = a.GetComponent<Humanoid>();
                    Animator an = body != null ? body.GetComponentInChildren<Animator>() : null;
                    Transform lf = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, "LeftFoot") : null, rf = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, "RightFoot") : null;
                    if (lf != null && rf != null) { mid = (lf.position + rf.position) * 0.5f + Vector3.up * 0.12f; size = Mathf.Max(0.8f, Vector3.Distance(lf.position, rf.position) + 0.5f); d = size * 2.1f; }
                }
                if (view == "fingerr" || view == "fingerl" || view == "holdr" || view == "holdl") // (close on the fingers themselves, from before and aside: a hook or a lifted finger shows · hold: the same hand from before and below, its palm's side — what it holds the thing with)
                {
                    Humanoid body = a.GetComponent<Humanoid>();
                    Animator an = body != null ? body.GetComponentInChildren<Animator>() : null;
                    string side = view == "fingerl" || view == "holdl" ? "Left" : "Right";
                    Transform i2 = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, side + "HandIndex2") : null, m1 = body != null && an != null ? BodyClips.SkeletonBone(body, an.transform, side + "HandMiddle1") : null;
                    if (i2 != null && m1 != null) { mid = (i2.position + m1.position) * 0.5f; size = 0.22f; d = size * 2.1f; }
                }
                switch (view)
                {
                    case "fingerr": from = mid + right * d * 0.85f + fwd * d * 0.2f + Vector3.up * d * 0.35f; break; // (from the side, a little ahead: what the hand touches, a post or a door, stands in front of it)
                    case "fingerl": from = mid - right * d * 0.85f + fwd * d * 0.2f + Vector3.up * d * 0.35f; break;
                    case "holdr": from = mid + fwd * d * 0.85f + right * d * 0.35f - Vector3.up * d * 0.35f; break; // (before it and below: the palm and the fingers on the thing)
                    case "holdl": from = mid + fwd * d * 0.85f - right * d * 0.35f - Vector3.up * d * 0.35f; break;
                    case "hands": from = mid - fwd * d * 0.5f + Vector3.up * d * 0.85f; break; // (over the shoulder: as he sees them)
                    case "handl": from = mid - right * d * 0.8f - fwd * d * 0.25f + Vector3.up * d * 0.45f; break;
                    case "handr": from = mid + right * d * 0.8f - fwd * d * 0.25f + Vector3.up * d * 0.45f; break;
                    case "back": from = mid - fwd * d + Vector3.up * size * 0.15f; break;
                    case "left": from = mid - right * d + Vector3.up * size * 0.1f; break;
                    case "top": from = mid + Vector3.up * d * 1.05f + fwd * size * 0.05f; break;
                    case "three": from = mid + (fwd + right).normalized * d + Vector3.up * size * 0.35f; break;
                    case "side": from = mid + right * d + Vector3.up * size * 0.1f; break;
                    case "chestside": from = mid + right * d + fwd * d * 0.15f; break;
                    case "chestleft": from = mid - right * d + fwd * d * 0.15f; break; // (the other side: a hand on the far side of the body, its depth)
                    case "chesttop": from = mid + Vector3.up * d * 0.9f + fwd * d * 0.35f; break; // (from above and a little ahead, close: how far a hand is before the belly or the chest — 2026-11-09)
                    case "head": from = mid + fwd * d + Vector3.up * size * 0.1f; break;
                    case "headside": from = mid + right * d + Vector3.up * size * 0.1f; break;
                    case "headtop": from = mid + Vector3.up * d + fwd * size * 0.2f; break;
                    case "contact": from = mid + right * d * 0.95f + fwd * d * 0.3f + Vector3.up * size * 0.1f; break;
                    case "feet": from = mid + (right + fwd * 0.45f).normalized * d + Vector3.up * size * 0.3f; break;
                    default: from = mid + fwd * d + Vector3.up * size * 0.15f; break; // front
                }
                r.Cams[i].farClipPlane = 80f;
                int instanced = LayerMask.NameToLayer("InstanceRenderer"); // (the grass: drawn straight into every camera on this layer, so hiding its objects does nothing)
                if (instanced >= 0) r.Cams[i].cullingMask &= ~(1 << instanced);
                r.Cams[i].nearClipPlane = Mathf.Max(0.1f, Vector3.Distance(from, mid) - size); // (what stands between the camera and the body is not in the picture: a rock, a chest)
                r.Cams[i].transform.position = from;
                r.Cams[i].transform.rotation = Quaternion.LookRotation(mid - from, view == "top" || view == "headtop" || view == "chesttop" ? fwd : Vector3.up);
            }
            r.Mid = whole;
            r.Lamp.range = sizeWhole * 3.5f;
        }

        // the stage's check filmed in the same run as it is measured (AnimStage): from each view, one frame at each of the clip's
        // key moments (its parts' ends, the moments its hands and feet take hold), StageFrame px a frame, one sheet a view;
        // what each frame is: said, in the check's report
        internal const int StageFrame = 480;
        // (views «»: the words only — the sheets of the run are made on request, the key sheet after it says the main thing: StageKey)
        internal static IEnumerator StageFilm(NpcActor a, string name, string views, List<float> times, System.Func<float> clock, System.Func<bool> on, List<string> said, System.Func<string> pose, System.Func<List<Mark>> marks)
        {
            int n = Mathf.Min(times.Count, 16);
            if (n == 0 || a == null) yield break;
            Rig r = views.Length > 0 ? Make(views, StageFrame) : null;
            if (r != null) r.Marks = marks; // (the hands' ways and the targets drawn over the close views: BodyCheck.Marks)
            int cols = 0, rows = 0;
            Texture2D[] sheets = r != null ? HeroSheets(r, n, out cols, out rows) : null;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    while (on() && clock() < times[i]) yield return null;
                    if (!on() || a == null) break;
                    yield return new WaitForEndOfFrame();
                    if (r != null)
                    {
                        PlaceBody(r, a);
                        Shoot(r, sheets, i, cols, rows);
                        foreach (Texture2D sh in sheets) Label(sh, i, cols, rows, StageFrame, clock().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s");
                    }
                    said.Add((i + 1) + ": " + clock().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s — " + pose());
                }
                if (r != null) HeroSave(name, r, sheets);
            }
            finally { if (r != null) HeroFree(sheets, r); }
        }

        // the key sheet of a take (review 2 2026-11-10: ten sheets of 480 px and the main thing unseen in them): a row a moment — the VERDICT's
        // mistakes, the «want» moments, the marks (AnimStage.KeyMoments) — and in it the two views that show that kind of mistake, each cell
        // labelled in the picture (its time and view; the rule's line). hold(t) holds the clip at that moment (and the other body's, when there is
        // one). With another body playing another take beside it (the stage's «ab», or the take before this one): a second sheet, this take's
        // two views and then the other's, at the same moments — take N | take N−1
        internal static float ProofFirst = 1.2f, ProofNext = 0.35f; // s a held moment settles: the first the clip's way in too
        internal sealed class KeyMoment { public float T; public string Label = "", Views = "side,three"; }
        internal static IEnumerator StageKey(NpcActor a, NpcActor other, string otherSaid, string name, List<KeyMoment> moments, System.Action<float> hold, System.Func<string> pose, List<string> said, System.Action done, System.Func<List<Mark>> marks)
        {
            int n = moments.Count, px = StageFrame;
            if (n == 0 || a == null) { done(); yield break; }
            Texture2D key = new Texture2D(px * 2, px * n, TextureFormat.RGB24, false), diff = other != null ? new Texture2D(px * 4, px * n, TextureFormat.RGB24, false) : null;
            Fill(key, new Color32(20, 20, 20, 255)); if (diff != null) Fill(diff, new Color32(20, 20, 20, 255));
            try
            {
                for (int i = 0; i < n; i++)
                {
                    KeyMoment m = moments[i];
                    hold(m.T);
                    float until = Time.time + (i == 0 ? ProofFirst : ProofNext);
                    while (Time.time < until) yield return null;
                    if (a == null) break;
                    yield return new WaitForEndOfFrame();
                    Rig r = Make(m.Views, px);
                    try
                    {
                        r.Marks = marks;
                        PlaceBody(r, a);
                        NpcForeignCamera.Away(other, r.Cams); // (the other body 1.4 m aside is not in this one's pictures: it stood before the side camera, 2026-11-10)
                        ShootRow(r, key, 0, i, n);
                        string t = m.T.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s";
                        for (int v = 0; v < r.Views.Count && v < 2; v++) Label(key, i * 2 + v, 2, n, px, t + "  " + r.Views[v], m.Label);
                        if (diff != null && other != null)
                        {
                            ShootRow(r, diff, 0, i, n);
                            for (int v = 0; v < r.Views.Count && v < 2; v++) Label(diff, i * 4 + v, 4, n, px, t + "  " + r.Views[v] + "  this take", m.Label);
                            r.Marks = null; // (the marks are this body's)
                            PlaceBody(r, other);
                            NpcForeignCamera.Away(a, r.Cams);
                            ShootRow(r, diff, 2, i, n);
                            for (int v = 0; v < r.Views.Count && v < 2; v++) Label(diff, i * 4 + 2 + v, 4, n, px, t + "  " + r.Views[v] + "  " + otherSaid, m.Label);
                        }
                    }
                    finally { NpcForeignCamera.Away(null, null); Drop(r); }
                    said.Add((i + 1) + ": " + m.T.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s — " + m.Label + " (" + m.Views + ") — " + pose());
                }
                Save(name + "_key", key);
                if (diff != null) Save(name + "_diff", diff);
            }
            finally { UnityEngine.Object.Destroy(key); if (diff != null) UnityEngine.Object.Destroy(diff); done(); } // (whatever went wrong here, the stage is let go and the report written)
        }

        // each skin on its body (its own, each worn piece): what is drawn, how big, and the bones it is bound to — any missing,
        // outside the body, or farther than SkinFar from its hips (a strap tied to something away from it is drawn out to it)
        internal static float SkinFar = 1.2f;
        private static IEnumerator NpcSkin(string name, string who)
        {
            NpcActor a = NpcFor(who);
            if (a == null) { Note(name, "no NPC «" + who + "» (raven npc list)"); yield break; }
            Humanoid h = a.GetComponent<Humanoid>();
            Animator an = h != null ? h.GetComponentInChildren<Animator>() : null;
            Transform hips = h != null && an != null ? BodyClips.SkeletonBone(h, an.transform, "Hips") : null;
            Vector3 mid = hips != null ? hips.position : a.transform.position + Vector3.up;
            StringBuilder sb = new StringBuilder();
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (SkinnedMeshRenderer smr in a.GetComponentsInChildren<SkinnedMeshRenderer>(true)) // heavy: once, asked by the author
            {
                Transform[] bones = smr.bones;
                int missing = 0, outside = 0, far = 0, built = 0;
                StringBuilder odd = new StringBuilder();
                if (bones != null)
                    foreach (Transform b in bones)
                    {
                        if (b == null) { missing++; continue; }
                        if (b.name == BodyBuild.ProxyName) built++;
                        bool inBody = b.IsChildOf(a.transform);
                        float d = Vector3.Distance(b.position, mid);
                        if (!inBody) outside++;
                        if (d > SkinFar) far++;
                        if ((!inBody || d > SkinFar) && odd.Length < 600) odd.Append("\n      ").Append(BonePath(b)).Append(" — ").Append(d.ToString("0.00", ci)).Append(" m from the hips").Append(inBody ? "" : ", OUTSIDE the body");
                    }
                Bounds bb = smr.bounds;
                sb.Append('\n').Append(smr.name).Append(" [").Append(smr.sharedMesh != null ? smr.sharedMesh.name : "no mesh").Append("] ")
                  .Append(smr.enabled && smr.gameObject.activeInHierarchy ? "drawn" : "hidden")
                  .Append(", size ").Append(bb.size.x.ToString("0.00", ci)).Append('×').Append(bb.size.y.ToString("0.00", ci)).Append('×').Append(bb.size.z.ToString("0.00", ci)).Append(" m")
                  .Append(", root ").Append(smr.rootBone != null ? smr.rootBone.name : "none")
                  .Append(", bones ").Append(bones != null ? bones.Length : 0).Append(" (build children ").Append(built).Append(", missing ").Append(missing).Append(", outside ").Append(outside).Append(", far ").Append(far).Append(')')
                  .Append(odd);
            }
            Note(name, sb.Length > 0 ? sb.ToString() : "no skins");
        }

        private static string BonePath(Transform t)
        {
            string s = t.name;
            for (Transform p = t.parent; p != null && s.Length < 160; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        private static IEnumerator NpcLook(string name, string who, string views)
        {
            NpcActor a = NpcFor(who);
            if (a == null) { Note(name, "no NPC «" + who + "» (raven npc list)"); yield break; }
            Rig r = Make(views);
            Texture2D sheet = new Texture2D(Cell * r.Cams.Count, Cell, TextureFormat.RGB24, false);
            try
            {
                yield return new WaitForEndOfFrame();
                PlaceBody(r, a);
                Shoot(r, new[] { sheet }, 0, r.Cams.Count, 1);
                Save(name, sheet);
            }
            finally { Object.Destroy(sheet); Drop(r); }
            Note(name, a.Describe(Player.m_localPlayer) + "; columns: " + string.Join(", ", r.Views.ToArray()));
        }

        private static IEnumerator NpcFilm(string name, string who, float seconds, float fps, string views)
        {
            NpcActor a = NpcFor(who);
            if (a == null) { Note(name, "no NPC «" + who + "» (raven npc list)"); yield break; }
            int n = Mathf.Clamp(Mathf.RoundToInt(seconds * fps), 1, 24);
            Rig r = Make(views);
            int cols, rows;
            Texture2D[] sheets = HeroSheets(r, n, out cols, out rows);
            StringBuilder rep = new StringBuilder();
            try
            {
                for (int i = 0; i < n; i++)
                {
                    yield return new WaitForEndOfFrame();
                    if (a == null) { rep.Append("\n").Append(i).Append(": gone"); break; }
                    PlaceBody(r, a);
                    Shoot(r, sheets, i, cols, rows);
                    rep.Append("\n").Append(i).Append(": ").Append(a.Describe(Player.m_localPlayer));
                    yield return new WaitForSeconds(1f / Mathf.Max(0.2f, fps));
                }
                HeroSave(name, r, sheets);
            }
            finally { HeroFree(sheets, r); }
            Note(name, n + " frames, " + fps.ToString("0.#") + " a second; one sheet per view: " + string.Join(", ", r.Views.ToArray()) + rep);
        }

        // nprobe <name> <who> [sec=2]: what the game does to its body, every physics step and every drawn frame —
        // where it is against the first moment (cm), its turn, its speed, ground, «attached», the animator's state
        private static IEnumerator NpcProbe(string name, string who, float seconds)
        {
            NpcActor a = NpcFor(who);
            if (a == null) { Note(name, "no NPC «" + who + "» (raven npc list)"); yield break; }
            Character c = a.GetComponent<Character>();
            Rigidbody rb = a.GetComponent<Rigidbody>();
            Animator an = a.GetComponentInChildren<Animator>();
            StringBuilder sb = new StringBuilder();
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            CapsuleCollider cap = a.GetComponent<CapsuleCollider>();
            Player me = Player.m_localPlayer;
            CapsuleCollider mine = me != null ? me.GetComponent<CapsuleCollider>() : null;
            sb.Append("body: scale ").Append(a.transform.lossyScale.y.ToString("0.###", ci)).Append(" · ").Append(BodyBuild.Report(a));
            if (cap != null) sb.Append(", capsule h ").Append(cap.height.ToString("0.###", ci)).Append(" r ").Append(cap.radius.ToString("0.###", ci)).Append(" centre y ").Append(cap.center.y.ToString("0.###", ci));
            if (mine != null) sb.Append(" | yours: scale ").Append(me.transform.lossyScale.y.ToString("0.###", ci)).Append(", capsule h ").Append(mine.height.ToString("0.###", ci)).Append(" r ").Append(mine.radius.ToString("0.###", ci)).Append(" centre y ").Append(mine.center.y.ToString("0.###", ci));
            // who made it bigger: its own scale, a parent, the scale kept in its net data, its parts
            sb.Append("\nscale: own ").Append(a.transform.localScale.ToString("0.###")).Append(", parent ").Append(a.transform.parent != null ? a.transform.parent.name : "none");
            ZNetView znv = a.GetComponent<ZNetView>();
            if (znv != null && znv.IsValid())
                sb.Append(", net scale ").Append(znv.GetZDO().GetVec3(ZDOVars.s_scaleHash, Vector3.zero).ToString("0.###"))
                  .Append(" / ").Append(znv.GetZDO().GetFloat(ZDOVars.s_scaleScalarHash, 0f).ToString("0.###", ci))
                  .Append(", sync initial scale ").Append(znv.m_syncInitialScale);
            if (c != null) sb.Append(", level ").Append(c.GetLevel());
            sb.Append("\nparts:");
            foreach (Component part in a.GetComponents<Component>()) if (part != null) sb.Append(' ').Append(part.GetType().Name);
            if (rb != null) sb.Append("\nrigidbody: interpolation ").Append(rb.interpolation).Append(", kinematic ").Append(rb.isKinematic).Append(", gravity ").Append(rb.useGravity);
            sb.Append("\nhold: ").Append(NpcBody.HoldOn ? "on" : "off").Append(", attached bodies ").Append(NpcBody.Held.Count);
            if (a.Mind != null) sb.Append("\nmind: ").Append(a.Mind.Describe());
            sb.Append("\n\nkind  t      dx    dy    dz  (cm)  yaw     vel     ground attached  anim state (hash) trans");
            Vector3 p0 = a.transform.position;
            float t0 = Time.time, end = Time.time + Mathf.Clamp(seconds, 0.2f, 6f);
            HarmonyLib.Traverse touch = HarmonyLib.Traverse.Create(c).Field("m_lastGroundTouch");
            int rows = 0;
            while (Time.time < end && a != null && rows < 600)
            {
                yield return new WaitForFixedUpdate();
                if (a == null) break;
                Row(sb, "fixed", a, c, rb, an, p0, t0, touch, ci); rows++;
                yield return null; // (and the frame as drawn)
                if (a == null) break;
                Row(sb, "frame", a, c, rb, an, p0, t0, touch, ci); rows++;
            }
            Note(name, sb.ToString());
        }

        private static void Row(StringBuilder sb, string kind, NpcActor a, Character c, Rigidbody rb, Animator an, Vector3 p0, float t0, HarmonyLib.Traverse touch, System.Globalization.CultureInfo ci)
        {
            Vector3 d = (a.transform.position - p0) * 100f;
            sb.Append('\n').Append(kind).Append(' ').Append((Time.time - t0).ToString("0.000", ci))
              .Append(' ').Append(d.x.ToString("0.0", ci)).Append(' ').Append(d.y.ToString("0.0", ci)).Append(' ').Append(d.z.ToString("0.0", ci))
              .Append("  ").Append(a.transform.eulerAngles.y.ToString("0.00", ci))
              .Append("  ").Append(rb != null ? rb.linearVelocity.magnitude.ToString("0.000", ci) : "-")
              .Append("  ").Append(c != null && c.IsOnGround() ? "G" : "-").Append(touch.FieldExists() ? "(" + touch.GetValue<float>().ToString("0.00", ci) + ")" : "")
              .Append("  ").Append(c != null && c.IsAttached() ? "A" : "-");
            if (an != null)
            {
                AnimatorStateInfo st = an.GetCurrentAnimatorStateInfo(0);
                sb.Append("  ").Append(st.shortNameHash).Append(an.IsInTransition(0) ? " T>" + an.GetNextAnimatorStateInfo(0).shortNameHash : "");
            }
        }

        // hover: what the game counts as pointed at, and every thing along your look (what stops the pointing)
        internal static string Hover()
        {
            Player p = Player.m_localPlayer;
            if (p == null || GameCamera.instance == null) return "no player";
            StringBuilder sb = new StringBuilder();
            GameObject h = p.GetHoverObject();
            Hoverable hv = h != null ? h.GetComponentInParent<Hoverable>() : null;
            sb.Append("hover: ").Append(h != null ? h.name + " [" + LayerMask.LayerToName(h.layer) + "]" : "none");
            if (hv != null) { string t = hv.GetHoverText(); sb.Append(" | text: ").Append(t.Replace("\n", " / ")); }
            Transform cam = GameCamera.instance.transform;
            RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, 50f, LayerMask.GetMask("item", "piece", "piece_nonsolid", "Default", "static_solid", "Default_small", "character", "character_net", "terrain", "vehicle", "character_ghost"));
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (RaycastHit r in hits)
            {
                if (r.collider == null) continue;
                Rigidbody rb = r.collider.attachedRigidbody;
                sb.Append(" || ").Append(r.distance.ToString("0.0")).Append(" m ").Append(r.collider.name).Append(" [").Append(LayerMask.LayerToName(r.collider.gameObject.layer)).Append(r.collider.isTrigger ? ", trigger" : "").Append("]")
                  .Append(rb != null ? " rb " + rb.name : "").Append(r.collider.GetComponentInParent<NpcActor>() != null ? " NPC" : "");
            }
            return sb.ToString();
        }

        internal static string NpcAttach(string onOff)
        {
            if (onOff == "on" || onOff == "off") NpcBody.HoldOn = onOff == "on";
            return "body hold (an attached NPC is held on its point every physics step): " + (NpcBody.HoldOn ? "on" : "off");
        }
    }
}
#endif
