#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The raven sits on what you wear, not on a fixed spot by the bone: naked it would float, in heavy armour it
    // would sink in. When your gear changes, the body and everything worn are read once (their vertices as they are
    // posed now) and the top of the surface over each shoulder is found; the raven is lifted or lowered to it,
    // within limits (any mod's armour may be odd). Meshes that cannot be read fall back on the kind of gear:
    // naked, tunic, light or heavy armour, a cape.
    internal static class ShoulderFit
    {
        internal static bool On = true;
        // The surface right under the raven: the highest point in a narrow column under its seat (4 cm across, wider
        // only if the mesh is too sparse there). Taking the highest point in a wide circle picked a raised collar or
        // pauldron edge beside it and set the bird up to 13 cm too high.
        internal static readonly float[] Columns = { 0.05f, 0.08f, 0.11f };
        internal static float NeckGap = 0.03f; // points this much nearer the body's middle than the seat are the neck, not the shoulder
        internal static float Margin = 0.01f;   // feet this far above the top found
        internal static float MinLift = -0.2f, MaxLift = 0.08f; // the ray finds the true surface: room to sit down onto a bare shoulder (~13 cm under the seat)
        internal static float Below = 0.25f, Above = 0.15f; // the height band around the seat that is searched

        private sealed class Fit
        {
            public string Sig;
            public float Next;
            public bool[] Dirty = { true, true };
            public float[] Lift = { 0f, 0f };
            public string[] How = { "", "" };
        }

        private static readonly Dictionary<Player, Fit> Fits = new Dictionary<Player, Fit>();
        private static Mesh _baked;
        private static Vector3[] _world = new Vector3[0];
        private static readonly List<int> Tris = new List<int>();

        // straight down through the seat: does this triangle lie under it, and at what height (in the ground plane)
        private static bool UnderSeat(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out float y)
        {
            y = 0f;
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 1e-10f) return false;
            float w1 = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / d;
            float w2 = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / d;
            float w3 = 1f - w1 - w2;
            if (w1 < 0f || w2 < 0f || w3 < 0f) return false;
            y = w1 * a.y + w2 * b.y + w3 * c.y;
            return true;
        }
        private static readonly List<Vector3> Verts = new List<Vector3>();

        private static readonly System.Reflection.FieldInfo ChestField = AccessTools.Field(typeof(Humanoid), "m_chestItem");
        private static readonly System.Reflection.FieldInfo CapeField = AccessTools.Field(typeof(Humanoid), "m_shoulderItem");

        // how far up (or down) from its seat by the bone the raven sits on this player's shoulder now
        internal static float Lift(Player p, bool munin, Vector3 seat)
        {
            if (!On || p == null) return 0f;
            Fit f;
            if (!Fits.TryGetValue(p, out f))
            {
                if (Fits.Count > 40) Prune();
                f = new Fit(); Fits[p] = f;
            }
            if (Time.time >= f.Next)
            {
                f.Next = Time.time + (ReferenceEquals(p, Player.m_localPlayer) ? 0.25f : 1.5f);
                string sig = Signature(p);
                if (sig != f.Sig) { f.Sig = sig; f.Dirty[0] = f.Dirty[1] = true; }
            }
            int i = munin ? 1 : 0;
            if (f.Dirty[i])
            {
                f.Dirty[i] = false;
                string how;
                long t0 = Slow.Start();
                f.Lift[i] = Measure(p, seat, out how);
                Slow.End(t0, "shoulder fit (armour measured)");
                f.How[i] = how;
                if (ShoulderLife.LogLandings && Plugin.Instance != null) Plugin.Instance.Log("Shoulder fit: " + (munin ? "Munin" : "Hugin") + " on " + p.GetPlayerName() + " " + (f.Lift[i] >= 0f ? "+" : "") + Mathf.RoundToInt(f.Lift[i] * 100f) + " cm (" + how + ")");
            }
            return f.Lift[i];
        }

        internal static string Info(Player p)
        {
            Fit f;
            if (p == null || !Fits.TryGetValue(p, out f)) return Lang.T("ще не міряно");
            return Lang.F("Хугін {0} ({1}), Мунін {2} ({3})", Cm(f.Lift[0]), f.How[0], Cm(f.Lift[1]), f.How[1]);
        }

        // this player again (a raven just landed: their pose now)
        internal static void Again(Player p) { Fit f; if (p != null && Fits.TryGetValue(p, out f)) { f.Dirty[0] = f.Dirty[1] = true; } }

        internal static void Remeasure() { foreach (Fit f in Fits.Values) { f.Sig = null; f.Next = 0f; } }

        private static string Cm(float v) { return Lang.F("{0} см", ((v >= 0f ? "+" : "") + Mathf.RoundToInt(v * 100f))); }

        private static void Prune()
        {
            List<Player> gone = new List<Player>();
            foreach (Player k in Fits.Keys) if (k == null) gone.Add(k);
            foreach (Player k in gone) Fits.Remove(k);
        }

        // what is worn and drawn now (a change means: measure again)
        private static string Signature(Player p)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (Renderer r in Worn(p)) sb.Append(r.GetInstanceID()).Append(',');
            return sb.ToString();
        }

        // the body and what is worn on it: not the ravens, not weapons or tools in the hands or on the back, not the head
        private static List<Renderer> Worn(Player p)
        {
            List<Renderer> list = new List<Renderer>();
            HashSet<Transform> skip = new HashSet<Transform>();
            VisEquipment vis = p.GetComponent<VisEquipment>();
            if (vis != null)
                foreach (System.Reflection.FieldInfo fi in Attach())
                {
                    Transform t = fi.GetValue(vis) as Transform;
                    if (t != null) skip.Add(t);
                }
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                string rn = r.name.ToLowerInvariant();
                if (rn.Contains("hair") || rn.Contains("beard")) continue; // long hair lifted Munin onto it
                if (r.GetComponentInParent<ShoulderPin>() != null || r.GetComponentInParent<WispLife>() != null) continue; // the ravens and Hugin's wisp are not armour
                bool out_ = false;
                for (Transform t = r.transform; t != null && t != p.transform; t = t.parent)
                    if (skip.Contains(t) || t.name == "Head") { out_ = true; break; }
                if (!out_) list.Add(r);
            }
            return list;
        }

        // the hands, the back and the helmet points (found once: this runs four times a second)
        private static List<System.Reflection.FieldInfo> _attach;
        private static List<System.Reflection.FieldInfo> Attach()
        {
            if (_attach != null) return _attach;
            _attach = new List<System.Reflection.FieldInfo>();
            foreach (string n in new[] { "m_leftHand", "m_rightHand", "m_helmet", "m_backShield", "m_backMelee", "m_backTwohandedMelee", "m_backBow", "m_backTool", "m_backAtgeir" })
            {
                System.Reflection.FieldInfo fi = AccessTools.Field(typeof(VisEquipment), n);
                if (fi != null) _attach.Add(fi);
            }
            return _attach;
        }

        private static float Measure(Player p, Vector3 seat, out string how)
        {
            float top = float.MinValue;
            // the surface right under the seat, found on the mesh's own triangles (exact even on the coarse body, where
            // the nearest points lie far off and the highest one near the neck stands well above the shoulder)
            float hitTop = float.MinValue; string hitOn = "";
            Vector3 mid = p.transform.position;
            float seatR = new Vector2(seat.x - mid.x, seat.z - mid.z).magnitude;
            int[] colN = new int[Columns.Length];
            float[] colTop = new float[Columns.Length];
            string[] colOn = new string[Columns.Length];
            for (int c = 0; c < Columns.Length; c++) colTop[c] = float.MinValue;
            float widest = Columns[Columns.Length - 1];
            int found = 0, unread = 0, read = 0;
            float bestFlat = float.MaxValue, bestDy = 0f; string bestOn = "";
            System.Text.StringBuilder diag = new System.Text.StringBuilder();
            if (_baked == null) { _baked = new Mesh(); _baked.hideFlags = HideFlags.HideAndDontSave; }
            foreach (Renderer r in Worn(p))
            {
                try
                {
                    SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
                    MeshFilter mf = smr == null ? r.GetComponent<MeshFilter>() : null;
                    if (smr != null ? smr.sharedMesh == null : (mf == null || mf.sharedMesh == null)) continue;
                    if (smr == null && !mf.sharedMesh.isReadable) { unread++; continue; }
                    // a skinned mesh is baked as posed now; Unity's bake may or may not carry the scale, so both
                    // readings are tried and the one that lands around the body is kept
                    for (int pass = 0; pass < (smr != null ? 3 : 1); pass++)
                    {
                        Matrix4x4 m;
                        Mesh mesh;
                        if (smr != null)
                        {
                            _baked.Clear();
                            smr.BakeMesh(_baked, pass == 1); // works for a mesh we may not read ourselves (the bake is ours)
                            if (_baked.vertexCount == 0) { unread++; break; }
                            mesh = _baked;
                            m = pass < 2 ? Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one) : smr.transform.localToWorldMatrix;
                        }
                        else { mesh = mf.sharedMesh; m = r.transform.localToWorldMatrix; }
                        Verts.Clear();
                        mesh.GetVertices(Verts);
                        Vector3 lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo;
                        for (int i = 0; i < Verts.Count; i++) { Vector3 v = m.MultiplyPoint3x4(Verts[i]); lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v); }
                        float tall = hi.y - lo.y;
                        bool around = tall > 0.03f && tall < 3f && seat.y > lo.y - 0.3f && seat.y < hi.y + 0.5f && seat.x > lo.x - 0.5f && seat.x < hi.x + 0.5f && seat.z > lo.z - 0.5f && seat.z < hi.z + 0.5f;
                        diag.Append(' ').Append(r.name).Append(pass == 0 ? "" : pass == 1 ? "(2)" : "(3)").Append(" [").Append((lo - seat).ToString("F2")).Append("..").Append((hi - seat).ToString("F2")).Append(" scale ").Append(r.transform.lossyScale.x.ToString("0.###")).Append(']');
                        if (!around && smr != null && pass < 2) continue; // not where the body is (or not body-sized): the next reading
                        read++;
                        int here = 0;
                        for (int i = 0; i < Verts.Count; i++)
                        {
                            Vector3 v = m.MultiplyPoint3x4(Verts[i]);
                            float dy = v.y - seat.y;
                            if (dy < -Below || dy > Above) continue;
                            float dx = v.x - seat.x, dz = v.z - seat.z;
                            float flat = dx * dx + dz * dz;
                            float ax = v.x - mid.x, az = v.z - mid.z;
                            if (ax * ax + az * az < (seatR - NeckGap) * (seatR - NeckGap)) continue; // the neck side
                            if (flat < bestFlat) { bestFlat = flat; bestDy = dy; bestOn = r.name; }
                            if (flat > widest * widest) continue;
                            found++; here++;
                            for (int c = 0; c < Columns.Length; c++)
                                if (flat <= Columns[c] * Columns[c]) { colN[c]++; if (v.y > colTop[c]) { colTop[c] = v.y; colOn[c] = r.name; } }
                        }
                        diag.Append('=').Append(here).Append('/').Append(Verts.Count);
                        if (_world.Length < Verts.Count) _world = new Vector3[Verts.Count * 2];
                        for (int i = 0; i < Verts.Count; i++) _world[i] = m.MultiplyPoint3x4(Verts[i]);
                        for (int sm = 0; sm < mesh.subMeshCount; sm++)
                        {
                            Tris.Clear();
                            mesh.GetTriangles(Tris, sm);
                            for (int k = 0; k + 2 < Tris.Count; k += 3)
                            {
                                int ia = Tris[k], ib = Tris[k + 1], ic = Tris[k + 2];
                                if (ia >= Verts.Count || ib >= Verts.Count || ic >= Verts.Count) continue;
                                float y;
                                if (!UnderSeat(seat, _world[ia], _world[ib], _world[ic], out y)) continue;
                                float dy = y - seat.y;
                                if (dy >= -Below && dy <= Above && y > hitTop) { hitTop = y; hitOn = r.name; }
                            }
                        }
                        break;
                    }
                }
                catch { unread++; }
            }
            Verts.Clear();
            if (ShoulderLife.LogLandings && Plugin.Instance != null && ReferenceEquals(p, Player.m_localPlayer)) Plugin.Instance.Log("Shoulder fit detail: read " // (others' gear changes would flood the log) + read + ", unreadable " + unread + "; points near the seat:" + diag
                + (bestFlat < float.MaxValue ? "; closest surface " + Mathf.RoundToInt(Mathf.Sqrt(bestFlat) * 100f) + " cm aside, " + Mathf.RoundToInt(bestDy * 100f) + " cm up/down, on " + bestOn : "; no surface in the band"));
            if (hitTop > float.MinValue)
            {
                how = Lang.F("поверхня під вороном: {0} {1} см (промінь)", hitOn, Mathf.RoundToInt((hitTop - seat.y) * 100f));
                return Mathf.Clamp(hitTop + Margin - seat.y, MinLift, MaxLift);
            }
            for (int c = 0; c < Columns.Length; c++)
            {
                if (colN[c] < 3) continue;
                top = colTop[c];
                how = Lang.F("поверхня під вороном: {0} {1} см, стовпчик {2} см, {3} точок", colOn[c], Mathf.RoundToInt((top - seat.y) * 100f), Mathf.RoundToInt(Columns[c] * 200f), colN[c]);
                return Mathf.Clamp(top + Margin - seat.y, MinLift, MaxLift);
            }
            how = Lang.F("за типом одягу{0}", (unread > 0 ? Lang.F(", моделі закриті: {0}", unread) : ""));
            return ByGear(p);
        }

        // no surface could be read: by what is worn
        internal static float NakedLift = 0f, TunicLift = 0.015f, LightLift = 0.025f, HeavyLift = 0.04f, CapeLift = 0.01f;
        internal static float HeavyArmor = 20f; // chest armour from this on counts as heavy
        private static float ByGear(Player p)
        {
            ItemDrop.ItemData chest = null, cape = null;
            try { if (ChestField != null) chest = ChestField.GetValue(p) as ItemDrop.ItemData; if (CapeField != null) cape = CapeField.GetValue(p) as ItemDrop.ItemData; } catch { }
            float lift = chest == null ? NakedLift : chest.m_shared.m_armor < 8f ? TunicLift : chest.m_shared.m_armor < HeavyArmor ? LightLift : HeavyLift;
            if (cape != null) lift += CapeLift;
            return Mathf.Clamp(lift, MinLift, MaxLift);
        }
    }
}
#endif
