#if RAVEN_ITEM && RAVEN_NPC
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Студія» on an NPC (story tests: its body, its gait, its sitting):
    //   nlook <name> <who> [views]                 — the NPC as it stands now (who: id, tag or near)
    //   nfilm <name> <who> [sec=4] [fps=2] [views] — frames in time, one sheet per view; the report says what it was doing
    // Views: front (its face), back, side, left, top, three. Framed on its body (the collider), not on where you are.
    internal static partial class Studio
    {
        private static NpcActor NpcFor(string who)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return null;
            return who == "near" ? Npcs.Nearest(p.transform.position, 60f) : Npcs.Find(who);
        }

        private static void PlaceNpc(Rig r, NpcActor a)
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
                switch (view)
                {
                    case "hands": from = mid - fwd * d * 0.5f + Vector3.up * d * 0.85f; break; // (over the shoulder: as he sees them)
                    case "handl": from = mid - right * d * 0.8f - fwd * d * 0.25f + Vector3.up * d * 0.45f; break;
                    case "handr": from = mid + right * d * 0.8f - fwd * d * 0.25f + Vector3.up * d * 0.45f; break;
                    case "back": from = mid - fwd * d + Vector3.up * size * 0.15f; break;
                    case "left": from = mid - right * d + Vector3.up * size * 0.1f; break;
                    case "top": from = mid + Vector3.up * d * 1.05f + fwd * size * 0.05f; break;
                    case "three": from = mid + (fwd + right).normalized * d + Vector3.up * size * 0.35f; break;
                    case "side": from = mid + right * d + Vector3.up * size * 0.1f; break;
                    default: from = mid + fwd * d + Vector3.up * size * 0.15f; break; // front
                }
                r.Cams[i].farClipPlane = 80f;
                int instanced = LayerMask.NameToLayer("InstanceRenderer"); // (the grass: drawn straight into every camera on this layer, so hiding its objects does nothing)
                if (instanced >= 0) r.Cams[i].cullingMask &= ~(1 << instanced);
                r.Cams[i].nearClipPlane = Mathf.Max(0.1f, Vector3.Distance(from, mid) - size); // (what stands between the camera and the body is not in the picture: a rock, a chest)
                r.Cams[i].transform.position = from;
                r.Cams[i].transform.rotation = Quaternion.LookRotation(mid - from, view == "top" ? fwd : Vector3.up);
            }
            r.Mid = whole;
            r.Lamp.range = sizeWhole * 3.5f;
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
                PlaceNpc(r, a);
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
                    PlaceNpc(r, a);
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
            sb.Append("body: scale ").Append(a.transform.lossyScale.y.ToString("0.###", ci));
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
