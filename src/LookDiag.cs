#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuginWhispers
{
    // The raven's zones for the looks (Zones: one zone byte per pixel of its colour picture, used by RavenLooks), and
    // the diagnostics behind them (console: raven looks diag): what the raven's look is made of, written to config/RavenWatch/looks/_diag/
    //   <raven>_report.txt     — the materials (every shader property), the mesh, the bones, the zone and overlap counts
    //   <raven>_main.png       — its colour picture as the game keeps it; _emission.png the glow picture
    //   <raven>_zones.png      — the picture split into zones (each zone one flat colour), _zones_over.png over the colours
    //   <raven>_sides.png      — left (red) / right (blue) halves; purple = both halves share those pixels
    //   <raven>_photo_*.png    — the raven filmed with the zone picture on it: as the game stores it and sitting
    // Zones come from the bones: each triangle goes to the bone its corners lean on most (the mesh's own weights when
    // it can be read, else the nearest bone of a baked copy). The eyes come from the glow picture's brightest spots.
    internal static class LookDiag
    {
        internal enum Zone { None, Body, Head, Beak, Eyes, Wings, Tips, Tail, Legs }
        internal static readonly string[] ZoneNames = { "нема", "Тулуб", "Голова", "Дзьоб", "Очі", "Крила", "Кінчики крил", "Хвіст", "Лапи" };
        private static readonly Color32[] ZoneColors =
        {
            new Color32(0, 0, 0, 0), new Color32(60, 180, 75, 255), new Color32(255, 225, 25, 255), new Color32(245, 130, 48, 255),
            new Color32(255, 255, 255, 255), new Color32(0, 130, 200, 255), new Color32(240, 50, 230, 255), new Color32(230, 25, 75, 255),
            new Color32(150, 90, 40, 255),
        };
        private const int MaxSide = 1024, PhotoSize = 768;

        internal static string Dir { get { return Path.Combine(Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "looks"), "_diag"); } }

        internal static string Cmd()
        {
            Directory.CreateDirectory(Dir);
            List<string> said = new List<string>();
            foreach (bool munin in new[] { false, true })
            {
                try { said.Add(Run(munin)); }
                catch (System.Exception e)
                {
                    said.Add((munin ? "Мунін" : "Хугін") + ": помилка " + e.Message);
                    if (Plugin.Instance != null) Plugin.Instance.LogError("Looks diag " + (munin ? "Munin" : "Hugin") + ": " + e);
                }
            }
            return string.Join(" | ", said.ToArray()) + ". Файли: " + Dir;
        }

        // ---------------------------------------------------------------- the zone map, for the looks
        // One byte per pixel of the raven's colour picture: its zone (Zone as a number). Made once per raven model from a
        // copy (weights → zones, beak split, eyes from the glow picture), grown a few pixels over the islands' edges so the
        // picture's smoothing never pulls an unpainted pixel in. Pixels no triangle uses stay 0 (= body).
        private class ZoneMap { public byte[] Z; public int W, H; }
        private static readonly Dictionary<bool, ZoneMap> Maps = new Dictionary<bool, ZoneMap>();

        internal static byte[] Zones(bool munin, out int W, out int H, out string why)
        {
            ZoneMap zm;
            W = H = 0; why = "";
            if (Maps.TryGetValue(munin, out zm)) { W = zm.W; H = zm.H; return zm.Z; }
            Raven src = Shoulder.Template(munin);
            if (src == null || src.m_visual == null) { why = "ворона ще нема (зайди у світ)"; return null; }
            SkinnedMeshRenderer smr = null;
            foreach (SkinnedMeshRenderer s in src.m_visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh != null && (smr == null || s.sharedMesh.vertexCount > smr.sharedMesh.vertexCount)) smr = s;
            Material main = smr != null ? smr.sharedMaterial : null;
            Texture mainTex = main != null ? main.mainTexture : null;
            if (mainTex == null) { why = "у ворона нема картинки кольору"; return null; }
            float scale = Mathf.Min(1f, (float)MaxSide / Mathf.Max(mainTex.width, mainTex.height));
            W = Mathf.Max(4, Mathf.RoundToInt(mainTex.width * scale)); H = Mathf.Max(4, Mathf.RoundToInt(mainTex.height * scale));
            Texture2D emi = Read(main.HasProperty("_EmissionMap") ? main.GetTexture("_EmissionMap") : null, MaxSide);
            GameObject stage = new GameObject("RavenWatch_LookZones");
            stage.transform.position = new Vector3(0f, 9450f, 0f);
            GameObject model = Object.Instantiate(src.m_visual, stage.transform, false);
            try
            {
                AppearMute.Quiet(model);
                model.SetActive(true);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                    if (t != model.transform && t.localScale.sqrMagnitude < 1e-6f) t.localScale = Vector3.one;
                foreach (Animator a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                SkinnedMeshRenderer skin = null;
                foreach (SkinnedMeshRenderer s in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.sharedMesh == smr.sharedMesh) skin = s;
                if (skin == null) { why = "копія без скіну"; return null; }
                Transform[] bones = skin.bones;
                Vector2[] uv; int[] tris; int[] vBone; Vector3[] vPos, bonePos; string how;
                Data(skin, new StringBuilder(), false, out uv, out tris, out vBone, out vPos, out bonePos, out how);
                if (uv == null || uv.Length == 0) { why = "UV не прочитано"; return null; }
                Zone[] vZone = new Zone[vBone.Length];
                for (int i = 0; i < vZone.Length; i++) vZone[i] = ZoneOfIndex(bones, vBone[i]);
                SplitBeak(bones, bonePos, vPos, vZone, new StringBuilder());
                byte[] zone = new byte[W * H], side = new byte[W * H], clash = new byte[W * H];
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    Fill(uv[a], uv[b], uv[c], W, H, (byte)Majority(vZone[a], vZone[b], vZone[c]), 0, zone, side, clash);
                }
                if (emi != null)
                {
                    Color32[] ep = emi.GetPixels32();
                    float max = 0f;
                    foreach (Color32 p in ep) max = Mathf.Max(max, Lum(p));
                    if (max > 0.02f)
                        for (int y = 0; y < H; y++)
                            for (int x = 0; x < W; x++)
                                if (Lum(ep[(y * emi.height / H) * emi.width + x * emi.width / W]) >= max * 0.6f) zone[y * W + x] = (byte)Zone.Eyes;
                }
                // grown over the edges
                for (int pass = 0; pass < 4; pass++)
                {
                    byte[] was = (byte[])zone.Clone();
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            int i = y * W + x;
                            if (was[i] != 0) continue;
                            byte n = x > 0 ? was[i - 1] : (byte)0;
                            if (n == 0 && x < W - 1) n = was[i + 1];
                            if (n == 0 && y > 0) n = was[i - W];
                            if (n == 0 && y < H - 1) n = was[i + W];
                            zone[i] = n;
                        }
                }
                zm = new ZoneMap { Z = zone, W = W, H = H };
                Maps[munin] = zm;
                if (Plugin.Instance != null) Plugin.Instance.Log("Raven looks: zone map of " + (munin ? "Munin" : "Hugin") + " " + W + "x" + H + " (" + how + ")");
                return zone;
            }
            finally
            {
                Object.Destroy(stage);
                if (emi != null) Object.Destroy(emi);
            }
        }

        internal static Color32 ZoneColor(int z) { return z >= 0 && z < ZoneColors.Length ? ZoneColors[z] : new Color32(20, 20, 20, 255); }

        private static string Run(bool munin)
        {
            string who = munin ? "munin" : "hugin";
            Raven src = Shoulder.Template(munin);
            if (src == null || src.m_visual == null) return who + ": ворона не знайдено (зайди у світ)";
            StringBuilder rep = new StringBuilder();
            rep.Append("Raven look diag: ").Append(src.name).Append(", visual ").Append(src.m_visual.name).Append(", game colour space ").Append(QualitySettings.activeColorSpace).Append('\n');

            SkinnedMeshRenderer smr = null;
            foreach (Renderer r in src.m_visual.GetComponentsInChildren<Renderer>(true))
            {
                rep.Append("\nRenderer ").Append(r.name).Append(" (").Append(r.GetType().Name).Append(")");
                foreach (Material m in r.sharedMaterials) DumpMaterial(rep, m);
                SkinnedMeshRenderer s = r as SkinnedMeshRenderer;
                if (s != null && s.sharedMesh != null && (smr == null || s.sharedMesh.vertexCount > smr.sharedMesh.vertexCount)) smr = s;
            }
            if (smr == null) { File.WriteAllText(Path.Combine(Dir, who + "_report.txt"), rep.ToString()); return who + ": немає скіну"; }
            Material main = smr.sharedMaterial;
            Texture mainTex = main != null ? main.mainTexture : null;
            Texture emiTex = main != null && main.HasProperty("_EmissionMap") ? main.GetTexture("_EmissionMap") : null;

            // the pictures
            Texture2D col = Read(mainTex, MaxSide);
            Texture2D emi = Read(emiTex, MaxSide);
            if (col != null) Save(who + "_main.png", col);
            if (emi != null) Save(who + "_emission.png", emi);
            int W = col != null ? col.width : 512, H = col != null ? col.height : 512;

            // the work is done on a copy standing in our own empty place
            int layer = RavenFx.FreeLayer();
            GameObject stage = new GameObject("RavenWatch_LookDiag");
            stage.transform.position = new Vector3(0f, 9400f, 0f);
            GameObject model = Object.Instantiate(src.m_visual, stage.transform, false);
            List<Texture2D> made = new List<Texture2D>();
            try
            {
                AppearMute.Quiet(model);
                Shared.RemoveExclamation(src, model);
                model.SetActive(true);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                    if (t != model.transform && t.localScale.sqrMagnitude < 1e-6f) t.localScale = Vector3.one;
                Animator anim = model.GetComponentInChildren<Animator>(true);
                if (anim != null) anim.enabled = false;
                SkinnedMeshRenderer skin = null;
                foreach (SkinnedMeshRenderer s in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.sharedMesh == smr.sharedMesh) skin = s;
                if (skin == null) return who + ": копія без скіну";
                skin.updateWhenOffscreen = true;

                // the mesh
                Mesh mesh = skin.sharedMesh;
                Transform[] bones = skin.bones;
                rep.Append("\n\nMesh ").Append(mesh.name).Append(": vertices ").Append(mesh.vertexCount).Append(", submeshes ").Append(mesh.subMeshCount)
                    .Append(", readable ").Append(mesh.isReadable).Append(", bones ").Append(bones.Length).Append(", root ").Append(skin.rootBone != null ? skin.rootBone.name : "none");
                rep.Append("\nBones:");
                for (int i = 0; i < bones.Length; i++) rep.Append("\n  ").Append(i).Append(' ').Append(bones[i] != null ? bones[i].name : "null").Append(" → ").Append(ZoneNames[(int)ZoneOf(bones[i])]);

                Vector2[] uv; int[] tris; int[] vBone; Vector3[] vPos, bonePos; string how;
                Data(skin, rep, true, out uv, out tris, out vBone, out vPos, out bonePos, out how);
                rep.Append("\nZones taken from: ").Append(how);
                if (uv == null || uv.Length == 0) { File.WriteAllText(Path.Combine(Dir, who + "_report.txt"), rep.ToString()); return who + ": UV не прочитано (" + how + ")"; }
                int outside = 0;
                foreach (Vector2 u in uv) if (u.x < -0.001f || u.x > 1.001f || u.y < -0.001f || u.y > 1.001f) outside++;
                rep.Append("\nUV outside 0..1: ").Append(outside).Append(" of ").Append(uv.Length);
                int[] perBone = new int[bones.Length];
                foreach (int vb in vBone) if (vb >= 0 && vb < perBone.Length) perBone[vb]++;
                rep.Append("\nCorners per main bone:");
                for (int i = 0; i < bones.Length; i++) if (perBone[i] > 0) rep.Append("\n  ").Append(bones[i] != null ? bones[i].name : "null").Append(": ").Append(perBone[i]).Append("  colour #").Append(Hex(BoneColor(i)));

                // the zone of every corner; the head is split into head and beak along the line neck → beak tip
                Zone[] vZone = new Zone[vBone.Length];
                for (int i = 0; i < vZone.Length; i++) vZone[i] = ZoneOfIndex(bones, vBone[i]);
                SplitBeak(bones, bonePos, vPos, vZone, rep);

                // zone and side of every triangle, drawn into the picture's space
                byte[] zone = new byte[W * H];
                byte[] side = new byte[W * H]; // bit 1 left, bit 2 right
                byte[] clash = new byte[W * H]; // two different zones on one pixel
                byte[] boneAt = new byte[W * H]; // main bone + 1
                byte[] dummy = new byte[W * H];
                int[] triZone = new int[ZoneNames.Length];
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    Zone z = Majority(vZone[a], vZone[b], vZone[c]);
                    int sd = SideOf(bones, vBone[a]) | SideOf(bones, vBone[b]) | SideOf(bones, vBone[c]);
                    if (sd == 3) sd = SideOf(bones, vBone[a]);
                    triZone[(int)z]++;
                    Fill(uv[a], uv[b], uv[c], W, H, (byte)z, (byte)sd, zone, side, clash);
                    int mb = vBone[b] == vBone[c] ? vBone[b] : vBone[a];
                    Fill(uv[a], uv[b], uv[c], W, H, (byte)(mb + 1), 0, boneAt, dummy, dummy);
                }
                // the eyes: the brightest spots of the glow picture
                int eyes = 0;
                if (emi != null)
                {
                    Color32[] ep = emi.GetPixels32();
                    float max = 0f;
                    foreach (Color32 p in ep) max = Mathf.Max(max, Lum(p));
                    if (max > 0.02f)
                        for (int y = 0; y < H; y++)
                            for (int x = 0; x < W; x++)
                            {
                                int ex = x * emi.width / W, ey = y * emi.height / H;
                                if (Lum(ep[ey * emi.width + ex]) >= max * 0.6f) { zone[y * W + x] = (byte)Zone.Eyes; eyes++; }
                            }
                }
                int[] px = new int[ZoneNames.Length];
                int both = 0, clashes = 0, used = 0;
                for (int i = 0; i < zone.Length; i++)
                {
                    px[zone[i]]++;
                    if (zone[i] != 0) used++;
                    if (side[i] == 3) both++;
                    if (clash[i] != 0) clashes++;
                }
                rep.Append("\n\nZones (triangles / pixels of ").Append(W).Append('x').Append(H).Append("):");
                for (int i = 0; i < ZoneNames.Length; i++) rep.Append("\n  ").Append(ZoneNames[i]).Append(": ").Append(triZone[i]).Append(" / ").Append(px[i]);
                rep.Append("\nPixels covered: ").Append(used).Append(", shared by left and right: ").Append(both)
                    .Append(" (").Append(Pct(both, used)).Append("), two zones on one pixel: ").Append(clashes).Append(" (").Append(Pct(clashes, used)).Append(')');

                Texture2D zt = Pic(W, H), over = Pic(W, H), sides = Pic(W, H);
                made.Add(zt); made.Add(over); made.Add(sides);
                Color32[] zp = new Color32[W * H], op = new Color32[W * H], sp = new Color32[W * H];
                Color32[] cp = col != null ? col.GetPixels32() : null;
                for (int i = 0; i < zp.Length; i++)
                {
                    Color32 zc = ZoneColors[zone[i]];
                    zp[i] = zone[i] == 0 ? new Color32(20, 20, 20, 255) : zc;
                    Color32 basec = cp != null ? cp[i] : new Color32(40, 40, 40, 255);
                    Color32 bright = new Color32(B(basec.r * 3f), B(basec.g * 3f), B(basec.b * 3f), 255);
                    op[i] = zone[i] == 0 ? bright : Color32.Lerp(bright, zc, 0.5f);
                    sp[i] = side[i] == 3 ? new Color32(170, 60, 220, 255) : side[i] == 1 ? new Color32(220, 50, 50, 255) : side[i] == 2 ? new Color32(50, 90, 230, 255)
                        : zone[i] != 0 ? new Color32(120, 120, 120, 255) : new Color32(20, 20, 20, 255);
                }
                zt.SetPixels32(zp); zt.Apply(); over.SetPixels32(op); over.Apply(); sides.SetPixels32(sp); sides.Apply();
                Save(who + "_zones.png", zt); Save(who + "_zones_over.png", over); Save(who + "_sides.png", sides);
                Texture2D bt = Pic(W, H);
                made.Add(bt);
                Color32[] bp = new Color32[W * H];
                for (int i = 0; i < bp.Length; i++) bp[i] = boneAt[i] == 0 ? new Color32(20, 20, 20, 255) : BoneColor(boneAt[i] - 1);
                bt.SetPixels32(bp); bt.Apply();
                Save(who + "_bones.png", bt);
                Transform headBone = null;
                foreach (Transform t in bones) if (t != null && t.name == "Jaw") headBone = t;
                if (headBone == null) foreach (Transform t in bones) if (t != null && t.name == "Neck") headBone = t;

                // photos: the zone picture worn by the raven
                if (layer >= 0)
                {
                    foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                    foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) if (r != skin) r.enabled = false;
                    Material[] mats = skin.sharedMaterials;
                    Material[] painted = new Material[mats.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        Material m = new Material(mats[i]); m.hideFlags = HideFlags.HideAndDontSave;
                        m.mainTexture = zt;
                        if (m.HasProperty("_Color")) m.color = Color.white;
                        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                        painted[i] = m;
                    }
                    skin.sharedMaterials = painted;
                    Photos(stage, model, skin, layer, who + "_photo_asis", null);
                    if (anim != null)
                    {
                        anim.enabled = true; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        anim.Rebind(); anim.Update(0f); anim.SetTrigger("teleportin");
                        for (int i = 0; i < 60; i++) anim.Update(0.1f);
                        anim.enabled = false;
                        Photos(stage, model, skin, layer, who + "_photo_sit", null);
                    }
                    Photos(stage, model, skin, layer, who + "_head", headBone);
                    foreach (Material m in painted) if (m != null) m.mainTexture = bt;
                    Photos(stage, model, skin, layer, who + "_bones", null);
                    Photos(stage, model, skin, layer, who + "_bones_head", headBone);
                    foreach (Material m in painted) if (m != null) Object.Destroy(m);
                }
                File.WriteAllText(Path.Combine(Dir, who + "_report.txt"), rep.ToString());
                if (Plugin.Instance != null) Plugin.Instance.Log(rep.ToString());
                return (munin ? "Мунін" : "Хугін") + ": " + how + ", ліво/право спільні " + Pct(both, used) + ", очі " + eyes + " точок";
            }
            finally
            {
                Object.Destroy(stage);
                if (col != null) Object.Destroy(col);
                if (emi != null) Object.Destroy(emi);
                foreach (Texture2D t in made) Object.Destroy(t);
            }
        }

        // UVs, triangles, each corner's main bone and where it stands (in the baked copy's space), plus where the bones
        // stand in that same space. The bones' real weights are found even when the mesh may not be read: each bone in
        // turn is moved a little (its children held where they were) and the copy baked again; skinning moves every
        // corner by its weight for that bone times the move, so what moved and how far IS the weight.
        private static void Data(SkinnedMeshRenderer skin, StringBuilder rep, bool check, out Vector2[] uv, out int[] tris, out int[] vBone, out Vector3[] pos, out Vector3[] bonePos, out string how)
        {
            Mesh mesh = skin.sharedMesh;
            bool ownWeights = mesh.isReadable && mesh.boneWeights != null && mesh.boneWeights.Length == mesh.vertexCount;
            Transform[] bones = skin.bones;
            Mesh baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, true);
                uv = baked.uv; tris = baked.triangles;
                pos = baked.vertices;
                int n = pos.Length;

                // where the bones stand in the baked space: of two readings, the one nearer the corners
                Matrix4x4[] tries = { Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one), skin.transform.localToWorldMatrix };
                float bestErr = float.MaxValue; int bestTry = 0;
                for (int k = 0; k < tries.Length; k++)
                {
                    float err = 0f;
                    for (int i = 0; i < n; i += 7) { float d; Nearest(bones, tries[k].MultiplyPoint3x4(pos[i]), out d); err += d; }
                    if (err < bestErr) { bestErr = err; bestTry = k; }
                }
                Matrix4x4 inv = tries[bestTry].inverse;
                bonePos = new Vector3[bones.Length];
                for (int i = 0; i < bones.Length; i++) bonePos[i] = bones[i] != null ? inv.MultiplyPoint3x4(bones[i].position) : Vector3.zero;

                if (ownWeights && !check)
                {
                    BoneWeight[] bw = mesh.boneWeights;
                    vBone = new int[n];
                    for (int i = 0; i < n; i++) vBone[i] = bw[i].boneIndex0;
                    how = "ваги кісток самої моделі";
                    return;
                }
                // the weights, bone by bone
                float[][] w = new float[bones.Length][];
                float step = Mathf.Max(0.001f, skin.bounds.size.magnitude * 0.05f);
                Vector3 delta = new Vector3(step, step * 0.7f, step * 0.4f);
                Mesh moved = new Mesh();
                try
                {
                    for (int b = 0; b < bones.Length; b++)
                    {
                        Transform bone = bones[b];
                        if (bone == null) continue;
                        int cc = bone.childCount;
                        Vector3[] cp = new Vector3[cc]; Quaternion[] cr = new Quaternion[cc];
                        for (int c = 0; c < cc; c++) { cp[c] = bone.GetChild(c).position; cr[c] = bone.GetChild(c).rotation; }
                        Vector3 was = bone.position;
                        bone.position = was + delta;
                        for (int c = 0; c < cc; c++) { bone.GetChild(c).position = cp[c]; bone.GetChild(c).rotation = cr[c]; }
                        skin.BakeMesh(moved, true);
                        Vector3[] v = moved.vertices;
                        bone.position = was;
                        for (int c = 0; c < cc; c++) { bone.GetChild(c).position = cp[c]; bone.GetChild(c).rotation = cr[c]; }
                        float[] wb = new float[n];
                        for (int i = 0; i < n && i < v.Length; i++) wb[i] = (v[i] - pos[i]).magnitude;
                        w[b] = wb;
                    }
                }
                finally { Object.Destroy(moved); }
                int[] probed = new int[n];
                int none = 0;
                for (int i = 0; i < n; i++)
                {
                    float sum = 0f, top = 0f; int at = -1;
                    for (int b = 0; b < bones.Length; b++)
                    {
                        if (w[b] == null) continue;
                        sum += w[b][i];
                        if (w[b][i] > top) { top = w[b][i]; at = b; }
                    }
                    if (sum < 1e-7f) { at = -1; none++; }
                    probed[i] = at;
                }
                vBone = probed;
                how = "ваги, знайдені рухом кісток (" + none + " вершин без кістки)";

                // the mesh's own weights when it may be read: the found ones are checked against them
                if (mesh.isReadable)
                {
                    BoneWeight[] real = mesh.boneWeights;
                    if (real != null && real.Length == n)
                    {
                        int same = 0;
                        for (int i = 0; i < n; i++) if (real[i].boneIndex0 == probed[i]) same++;
                        rep.Append("\nProbed weights vs the mesh's own: same main bone on ").Append(same).Append(" of ").Append(n).Append(" (").Append(Pct(same, n)).Append(')');
                        int[] own = new int[n];
                        for (int i = 0; i < n; i++) own[i] = real[i].boneIndex0;
                        vBone = own;
                        how = "ваги кісток самої моделі (знайдені рухом збігаються на " + Pct(same, n) + ")";
                    }
                }
            }
            finally { Object.Destroy(baked); }
        }

        private static int Nearest(Transform[] bones, Vector3 p, out float best)
        {
            best = float.MaxValue; int at = -1;
            for (int i = 0; i < bones.Length; i++)
            {
                Transform b = bones[i];
                if (b == null) continue;
                Vector3 a = b.position, e = a;
                for (int c = 0; c < b.childCount; c++) { Transform ch = b.GetChild(c); if (System.Array.IndexOf(bones, ch) >= 0 || ch.name.EndsWith("_end")) { e = ch.position; break; } }
                Vector3 ab = e - a;
                float t = ab.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                float d = (a + ab * t - p).magnitude;
                if (d < best) { best = d; at = i; }
            }
            return at;
        }

        internal static Zone ZoneOf(Transform b)
        {
            if (b == null) return Zone.None;
            string n = b.name.ToLowerInvariant();
            if (n.Contains("jaw") || n.Contains("beak")) return Zone.Beak;
            if (n.Contains("neck") || n.Contains("head")) return Zone.Head;
            if (n.Contains("wing3")) return Zone.Tips;
            if (n.Contains("wing")) return Zone.Wings;
            if (n.Contains("tail")) return Zone.Tail;
            if (n.Contains("leg") || n.Contains("toe") || n.Contains("foot") || n.Contains("talon") || n.StartsWith("index") || n.StartsWith("mid") || n.StartsWith("ring")) return Zone.Legs;
            return Zone.Body;
        }

        private static Zone ZoneOfIndex(Transform[] bones, int i) { return i >= 0 && i < bones.Length ? ZoneOf(bones[i]) : Zone.None; }

        private static int SideOf(Transform[] bones, int i)
        {
            if (i < 0 || i >= bones.Length || bones[i] == null) return 0;
            string n = bones[i].name.ToLowerInvariant();
            return n.EndsWith(".l") || n.EndsWith("_l") ? 1 : n.EndsWith(".r") || n.EndsWith("_r") ? 2 : 0;
        }

        private static Zone Majority(Zone a, Zone b, Zone c) { return b == c ? b : a; }

        private static void Fill(Vector2 a, Vector2 b, Vector2 c, int W, int H, byte z, byte sd, byte[] zone, byte[] side, byte[] clash)
        {
            Vector2 A = new Vector2(a.x * W, a.y * H), B2 = new Vector2(b.x * W, b.y * H), C = new Vector2(c.x * W, c.y * H);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B2.x, C.x)))), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B2.x, C.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B2.y, C.y)))), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B2.y, C.y))));
            float area = (B2.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B2.y - A.y);
            if (Mathf.Abs(area) < 1e-6f) return;
            const float pad = 0.6f; // a little over the edges, so seams do not stay unpainted
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Edge(B2, C, p) / area, w1 = Edge(C, A, p) / area, w2 = Edge(A, B2, p) / area;
                    float e = pad / Mathf.Sqrt(Mathf.Abs(area));
                    if (w0 < -e || w1 < -e || w2 < -e) continue;
                    int i = y * W + x;
                    if (zone[i] != 0 && zone[i] != z) clash[i] = 1;
                    zone[i] = z;
                    side[i] |= sd;
                }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p) { return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x); }

        // the head's corners (by bone) are split at this share of the way from the neck bone to the beak tip
        internal const float BeakFrom = 0.62f;

        private static void SplitBeak(Transform[] bones, Vector3[] bonePos, Vector3[] pos, Zone[] vZone, StringBuilder rep)
        {
            int neck = -1;
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null && bones[i].name == "Neck") neck = i;
            if (neck < 0) { rep.Append("\nBeak split: no Neck bone"); return; }
            Vector3 n0 = bonePos[neck], tip = n0;
            float far = 0f;
            for (int i = 0; i < pos.Length; i++)
                if ((vZone[i] == Zone.Head || vZone[i] == Zone.Beak) && (pos[i] - n0).sqrMagnitude > far) { far = (pos[i] - n0).sqrMagnitude; tip = pos[i]; }
            Vector3 dir = tip - n0;
            float len = dir.magnitude;
            if (len < 1e-6f) return;
            dir /= len;
            int[] bins = new int[10];
            for (int i = 0; i < pos.Length; i++)
            {
                if (vZone[i] != Zone.Head && vZone[i] != Zone.Beak) continue;
                float t = Vector3.Dot(pos[i] - n0, dir) / len;
                bins[Mathf.Clamp((int)(t * 10f), 0, 9)]++;
                vZone[i] = t >= BeakFrom ? Zone.Beak : Zone.Head;
            }
            rep.Append("\nHead corners along neck → beak tip (tenths): ");
            for (int i = 0; i < 10; i++) rep.Append(i == 0 ? "" : " ").Append(bins[i]);
            rep.Append(", beak from ").Append(BeakFrom.ToString(CultureInfo.InvariantCulture));
        }

        private static Color32 BoneColor(int i)
        {
            Color c = Color.HSVToRGB((i * 0.618034f) % 1f, i % 2 == 0 ? 0.85f : 0.55f, i % 3 == 0 ? 1f : 0.8f);
            return c;
        }

        private static string Hex(Color32 c) { return c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2"); }

        private static void Photos(GameObject stage, GameObject model, SkinnedMeshRenderer skin, int layer, string name, Transform focus)
        {
            Camera cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.SetParent(stage.transform, false);
            cam.enabled = false; cam.cullingMask = 1 << layer; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.25f, 0.25f, 0.27f, 1f); cam.fieldOfView = 25f; cam.renderingPath = RenderingPath.Forward;
            cam.allowHDR = false; cam.allowMSAA = false;
            Light key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(stage.transform, false); key.type = LightType.Directional; key.intensity = 1.1f; key.cullingMask = 1 << layer;
            key.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            Light fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(stage.transform, false); fill.type = LightType.Directional; fill.intensity = 0.7f; fill.cullingMask = 1 << layer;
            fill.transform.rotation = Quaternion.Euler(30f, -40f, 0f);
            RenderTexture rt = new RenderTexture(PhotoSize, PhotoSize, 24, RenderTextureFormat.ARGB32);
            rt.Create(); cam.targetTexture = rt;
            Texture2D shot = new Texture2D(PhotoSize, PhotoSize, TextureFormat.RGBA32, false);
            RenderTexture prev = RenderTexture.active;
            try
            {
                Bounds bb = skin.bounds;
                float dist = bb.extents.magnitude / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.75f;
                if (focus != null) { bb.center = focus.position; dist *= 0.3f; }
                cam.nearClipPlane = Mathf.Max(0.01f, dist * 0.05f); cam.farClipPlane = dist * 4f;
                Vector3[] dirs = { model.transform.right, -model.transform.right, model.transform.forward, -model.transform.forward, Vector3.up, -Vector3.up };
                string[] names = { "side1", "side2", "front", "back", "top", "under" };
                for (int i = 0; i < dirs.Length; i++)
                {
                    Vector3 d = dirs[i];
                    cam.transform.position = bb.center + d * dist;
                    cam.transform.rotation = Quaternion.LookRotation(-d, Mathf.Abs(d.y) > 0.9f ? model.transform.forward : Vector3.up);
                    cam.Render();
                    RenderTexture.active = rt;
                    shot.ReadPixels(new Rect(0, 0, PhotoSize, PhotoSize), 0, 0);
                    shot.Apply();
                    Save(name + "_" + names[i] + ".png", shot);
                }
            }
            finally
            {
                RenderTexture.active = prev;
                cam.targetTexture = null;
                Object.Destroy(rt); Object.Destroy(shot);
                Object.Destroy(cam.gameObject); Object.Destroy(key.gameObject); Object.Destroy(fill.gameObject);
            }
        }

        // a game picture read off the card, as it is stored (no colour conversion on the way)
        internal static Texture2D Read(Texture src, int maxSide)
        {
            if (src == null) return null;
            float scale = Mathf.Min(1f, (float)maxSide / Mathf.Max(src.width, src.height));
            int w = Mathf.Max(4, Mathf.RoundToInt(src.width * scale)), h = Mathf.Max(4, Mathf.RoundToInt(src.height * scale));
            bool srgb = Srgb(src);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            RenderTexture prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
                t.hideFlags = HideFlags.HideAndDontSave;
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                t.Apply();
                return t;
            }
            finally { RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); }
        }

        internal static bool Srgb(Texture t)
        {
            try { return UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat); }
            catch (System.Exception) { return true; }
        }

        private static void DumpMaterial(StringBuilder log, Material m)
        {
            if (m == null) return;
            Shader sh = m.shader;
            log.Append("\n  material ").Append(m.name).Append(" / shader ").Append(sh != null ? sh.name : "none");
            log.Append("\n    keywords: ").Append(string.Join(" ", m.shaderKeywords));
            if (sh == null) return;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                string n = sh.GetPropertyName(i);
                ShaderPropertyType type = sh.GetPropertyType(i);
                log.Append("\n    ").Append(n).Append(" (").Append(type).Append(")");
                if (type == ShaderPropertyType.Color) log.Append(" = ").Append(m.GetColor(n));
                else if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range) log.Append(" = ").Append(m.GetFloat(n).ToString(CultureInfo.InvariantCulture));
                else if (type == ShaderPropertyType.Vector) log.Append(" = ").Append(m.GetVector(n));
                else if (type == ShaderPropertyType.Texture)
                {
                    Texture tx = m.GetTexture(n);
                    log.Append(" = ").Append(tx != null ? tx.name + " " + tx.width + "x" + tx.height + " " + tx.graphicsFormat + (Srgb(tx) ? " sRGB" : " linear") : "none");
                }
            }
        }

        private static Texture2D Pic(int w, int h) { Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.hideFlags = HideFlags.HideAndDontSave; t.filterMode = FilterMode.Point; return t; }
        private static void Save(string file, Texture2D t) { File.WriteAllBytes(Path.Combine(Dir, file), ImageConversion.EncodeToPNG(t)); }
        private static float Lum(Color32 c) { return (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f; }
        private static byte B(float v) { return (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255); }
        private static string Pct(int a, int b) { return b > 0 ? (100f * a / b).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "0%"; }
    }
}
#endif
