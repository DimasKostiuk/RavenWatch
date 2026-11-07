using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A body's build, thin … heavy (−1 … +1, 0 as the game made it), for a body dressed by the game's VisEquipment (an
    // NPC now: NpcBody gives it NpcPersona.Build). The cloth follows: the game binds every worn piece's skin to the body's
    // own bone list (VisEquipment.AttachArmor), so what changes how a bone carries its skin changes the cloth over it too.
    // How: under each bone of the trunk and limbs one child «rw_build» (no children, never animated), scaled across the
    // bone, its length kept; the skins (the body's and each worn piece's) take that child in the bone's place. The skin
    // round the bone grows or narrows; the bone, all below it, the animation and the joints stay as they were (scaling
    // the bone itself would scale all below it and skew the arms). Nothing per frame: made once, its scale set when the
    // build changes (as it comes at once; changed on a living body eased over EaseFor, its Update on only then), skins
    // bound anew after each piece put on (SkinGuardPatch: after every other mod's own binding).
    //   Not followed: rigid pieces (helmet, beard, hair, weapons: on the head and hands, which keep their size).
    //   A piece with the game's cloth simulation (MagicaCloth: a cape) gets the build children as well: the game hands it
    //   the body's list before its cloth is built (its cloth's own bones by name stay the real ones).
    //   Not changed by the build: its capsule (how it walks, where it fits: a door, a seat, the way finder) — the look only.
    //   A body rebuilt (its bones gone) or another body: made again at the next Set. Its body fallen dead (the game's
    //   ragdoll, a body of its own): the same build (BodyBuildRagdollPatch).
    // Looking a bone up by name in a skin's list: BodyBuild.Real (the bone, not its build child).
    internal sealed class BodyBuild : MonoBehaviour
    {
        internal const string ProxyName = "rw_build";

        // across each bone at build 1: × (1 + gain), its length kept; the belly and the waist front-back apart from sideways
        // (a heavy one is deep in the belly more than wide). Tuned live: studio «set BodyBuild.Belly 0.6» — every built body
        // takes it at its next Set (NpcBody: within 2 s)
        internal static float Hips = 0.24f, Belly = 0.55f, BellySide = 0.35f, Waist = 0.40f, WaistSide = 0.28f, Chest = 0.18f,
            Neck = 0.25f, Shoulder = 0.12f, Arm = 0.28f, ForeArm = 0.18f, Thigh = 0.30f, Shin = 0.15f;

        // the bones (the left ones; the right mirrored) and the bone each one's length runs to (the axis kept)
        private static readonly string[,] Bones =
        {
            { "Hips", "Spine" }, { "Spine", "Spine1" }, { "Spine1", "Spine2" }, { "Spine2", "Neck" }, { "Neck", "Head" },
            { "LeftShoulder", "LeftArm" }, { "LeftArm", "LeftForeArm" }, { "LeftForeArm", "LeftHand" },
            { "LeftUpLeg", "LeftLeg" }, { "LeftLeg", "LeftFoot" },
        };

        // a bone's gain at build 1: x front-back, y sideways
        private static Vector2 Gain(string bone)
        {
            switch (bone)
            {
                case "Hips": return new Vector2(Hips, Hips * 0.9f);
                case "Spine": return new Vector2(Belly, BellySide);       // the belly
                case "Spine1": return new Vector2(Waist, WaistSide);
                case "Spine2": return new Vector2(Chest, Chest);          // the chest
                case "Neck": return new Vector2(Neck, Neck);
                case "LeftShoulder": return new Vector2(Shoulder, Shoulder);
                case "LeftArm": return new Vector2(Arm, Arm);
                case "LeftForeArm": return new Vector2(ForeArm, ForeArm);
                case "LeftUpLeg": return new Vector2(Thigh, Thigh);
                case "LeftLeg": return new Vector2(Shin, Shin);
            }
            return Vector2.zero;
        }
        private static float Stamp() { return Hips + Belly * 3f + BellySide * 5f + Waist * 7f + WaistSide * 11f + Chest * 13f + Neck * 17f + Shoulder * 19f + Arm * 23f + ForeArm * 29f + Thigh * 31f + Shin * 37f; } // (a gain changed: all set again)

        private sealed class Part { public Transform Proxy; public int Front, Side; public string Key; }
        private readonly List<Part> _parts = new List<Part>();
        private readonly Dictionary<Transform, Transform> _proxyOf = new Dictionary<Transform, Transform>();
        private float _build = float.NaN, _stamp;
        private SkinnedMeshRenderer _madeOn; // (the body it was made on: another, or its bones gone — a body rebuilt — made again)
        private bool _cannot;               // (this body cannot have it: said once, not tried again)

        // the build of this body (any game: the build is not sent, each game reads it from the person's own ways)
        internal static void Set(VisEquipment vis, float build)
        {
            if (vis == null || vis.m_bodyModel == null || vis.m_bodyModel.sharedMesh == null) return;
            build = Mathf.Clamp(build, -1f, 1f);
            BodyBuild b = vis.GetComponent<BodyBuild>();
            if (b == null)
            {
                if (Mathf.Abs(build) < 0.01f) return; // (as the game made it: nothing made)
                b = vis.gameObject.AddComponent<BodyBuild>();
                b.enabled = false; // (its Update: only while it eases)
            }
            if (b._cannot) return;
            if (b._madeOn != vis.m_bodyModel || (b._parts.Count > 0 && b._parts[0].Proxy == null))
            {
                foreach (Part p in b._parts) if (p.Proxy != null) Object.Destroy(p.Proxy.gameObject);
                b._parts.Clear(); b._proxyOf.Clear(); b._build = float.NaN;
                b._madeOn = vis.m_bodyModel;
                if (!b.Make(vis)) { b._cannot = true; return; }
            }
            float stamp = Stamp();
            if (Mathf.Abs(b._build - build) < 0.01f && b._stamp == stamp) return;
            b._stamp = stamp;
            if (float.IsNaN(b._build)) { b._build = b._shown = build; b.Apply(build); return; } // (as it comes: at once)
            if (Mathf.Abs(b._build - build) < 0.01f) { b.Apply(b._shown); return; } // (a gain tuned: at once)
            b._from = b._shown; b._build = build; b._since = Time.time; b.enabled = true; // (changed on a living body: eased over EaseFor)
        }

        internal static float EaseFor = 1.5f; // s
        private float _shown, _from, _since;

        private void Update()
        {
            float k = Mathf.Clamp01((Time.time - _since) / EaseFor);
            k = k * k * (3f - 2f * k);
            _shown = Mathf.Lerp(_from, _build, k);
            Apply(_shown);
            if (k >= 1f) enabled = false;
        }

        private void Apply(float build)
        {
            foreach (Part p in _parts)
            {
                if (p.Proxy == null) continue;
                Vector2 g = Gain(p.Key);
                Vector3 s = Vector3.one;
                s[p.Front] = Mathf.Max(0.5f, 1f + build * g.x);
                s[p.Side] = Mathf.Max(0.5f, 1f + build * g.y);
                p.Proxy.localScale = s;
            }
        }

        // its build children, once: each bone's axes from the body mesh's rest pose (its bind poses), not from the pose now
        private bool Make(VisEquipment vis)
        {
            SkinnedMeshRenderer body = vis.m_bodyModel;
            Transform[] bones = body.bones;
            Matrix4x4[] binds = body.sharedMesh.bindposes;
            if (bones == null || binds == null || binds.Length != bones.Length) { Plugin.Instance.Log("BodyBuild: " + vis.name + " has no bind poses to read (" + (binds != null ? binds.Length : 0) + " for " + (bones != null ? bones.Length : 0) + " bones): left as it is"); return false; }
            Dictionary<string, int> at = new Dictionary<string, int>();
            for (int i = 0; i < bones.Length; i++) { bones[i] = Real(bones[i]); if (bones[i] != null && !at.ContainsKey(bones[i].name)) at[bones[i].name] = i; } // (made again: the old build children, still in the list till the frame ends, as their bones)
            Vector3 front = body.transform.InverseTransformDirection(vis.transform.forward); // (the mesh's own frame: the renderer itself is not animated)
            for (int n = 0; n < Bones.GetLength(0); n++)
            {
                string key = Bones[n, 0], keyTo = Bones[n, 1];
                foreach (string name in new[] { key, BodyClips.Mirror(key) })
                {
                    if (name == null) continue;
                    string to = name == key ? keyTo : BodyClips.Mirror(keyTo) ?? keyTo;
                    int i, c;
                    if (!at.TryGetValue(name, out i) || !at.TryGetValue(to, out c)) continue;
                    Vector3 along = binds[i].MultiplyPoint3x4(binds[c].inverse.MultiplyPoint3x4(Vector3.zero)); // the child joint at rest, in this bone's own frame
                    Vector3 fw = binds[i].MultiplyVector(front);
                    int len = Biggest(along, -1), fr = Biggest(fw, len), side = 3 - len - fr;
                    GameObject o = new GameObject(ProxyName);
                    o.transform.SetParent(bones[i], false);
                    _parts.Add(new Part { Proxy = o.transform, Front = fr, Side = side, Key = key });
                    _proxyOf[bones[i]] = o.transform;
                }
            }
            if (_parts.Count == 0) { Plugin.Instance.Log("BodyBuild: " + vis.name + " has none of the trunk's and limbs' bones: left as it is"); return false; }
            Bind(vis);
            return true;
        }

        private static int Biggest(Vector3 v, int not)
        {
            int best = -1;
            for (int k = 0; k < 3; k++) if (k != not && (best < 0 || Mathf.Abs(v[k]) > Mathf.Abs(v[best]))) best = k;
            return best;
        }

        // every skin on the body takes the build children in its bones' place (the body's own list too: the game copies it
        // to each piece it puts on) — when made, and after each piece put on (SkinGuardPatch)
        internal static void Bind(VisEquipment vis)
        {
            BodyBuild b = vis != null ? vis.GetComponent<BodyBuild>() : null;
            if (b == null || b._proxyOf.Count == 0) return;
            foreach (SkinnedMeshRenderer r in vis.GetComponentsInChildren<SkinnedMeshRenderer>(true)) // heavy: once a piece is put on, and once when its build is first made
            {
                Transform[] bones = r != null ? r.bones : null;
                if (bones == null) continue;
                bool changed = false;
                for (int i = 0; i < bones.Length; i++)
                {
                    Transform p;
                    if (bones[i] != null && b._proxyOf.TryGetValue(Real(bones[i]), out p) && bones[i] != p) { bones[i] = p; changed = true; } // (an old build child too: onto the new one)
                }
                if (changed) r.bones = bones;
            }
        }

        // a bone of a skin's list as itself (its build child stands in its place there)
        internal static Transform Real(Transform t) { return t != null && t.parent != null && t.name == ProxyName ? t.parent : t; }

        // for the studio's report: its build, how many bones carry it, the belly as it is now
        internal static string Report(Component c)
        {
            BodyBuild b = c != null ? c.GetComponent<BodyBuild>() : null;
            if (b == null) return "build: none";
            if (b._cannot) return "build: cannot (see the log)";
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            string belly = "";
            foreach (Part p in b._parts) if (p.Key == "Spine" && p.Proxy != null) { belly = ", belly front ×" + p.Proxy.localScale[p.Front].ToString("0.##", ci) + " side ×" + p.Proxy.localScale[p.Side].ToString("0.##", ci); break; }
            return "build: " + b._build.ToString("0.##", ci) + " on " + b._parts.Count + " bones" + belly;
        }

        // its build now (0: none)
        internal static float Of(Component c) { BodyBuild b = c != null ? c.GetComponent<BodyBuild>() : null; return b != null && !float.IsNaN(b._build) ? b._build : 0f; }
    }

    // the body that falls dead (the game's ragdoll, dressed by its own VisEquipment) as it was: heavy or thin
    [HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
    internal static class BodyBuildRagdollPatch
    {
        private static void Postfix(Humanoid __instance, Ragdoll ragdoll)
        {
            try { float b = BodyBuild.Of(__instance); if (b != 0f && ragdoll != null) BodyBuild.Set(ragdoll.GetComponent<VisEquipment>(), b); }
            catch (System.Exception e) { PatchGuard.Fail("BodyBuildRagdollPatch", e); }
        }
    }
}
