#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    internal static class TakeoffKeys
    {
        internal const int Keys = 5;
        internal static readonly string[] BoneNames =
        {
            "Wing1.l", "Wing2.l", "Wing3.l",
            "Spine", "Spine1", "Neck", "Tail",
            "Leg1.l", "Leg2.l", "Leg3.l", "Index1.l", "Mid1.l", "Ring1.l", "BackTalon1.l", "BackTalon2.l",
            "Leg1.r", "Leg2.r", "Leg3.r", "Index1.r", "Mid1.r", "Ring1.r", "BackTalon1.r", "BackTalon2.r",
            "Wing1.r", "Wing2.r", "Wing3.r"
        };
        internal static readonly string[] BoneLabels =
        {
            "Крило — плече (ліве)", "Крило — лікоть (ліве)", "Крило — кисть (ліве)",
            "Тулуб", "Шия — основа", "Голова", "Хвіст",
            "Лапа ліва — стегно", "Лапа ліва — гомілка", "Лапа ліва — щиколотка",
            "Лапа ліва — палець 1", "Лапа ліва — палець 2", "Лапа ліва — палець 3",
            "Лапа ліва — задній кіготь 1", "Лапа ліва — задній кіготь 2",
            "Лапа права — стегно", "Лапа права — гомілка", "Лапа права — щиколотка",
            "Лапа права — палець 1", "Лапа права — палець 2", "Лапа права — палець 3",
            "Лапа права — задній кіготь 1", "Лапа права — задній кіготь 2",
            "Крило — плече (праве)", "Крило — лікоть (праве)", "Крило — кисть (праве)"
        };
        internal static readonly Vector3[,] Pose = new Vector3[Keys, BoneNames.Length];
        internal static bool Active;
        internal static float Duration = 0.9f;
        internal static string FilePath
        {
            get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "takeoffkeys.txt"); }
        }

        internal static Vector3 At(float x, int bone)
        {
            // one take-off, not a loop: key 1 is not bent by key 5 (the ends take themselves as their neighbours)
            x = Mathf.Clamp(x, 0f, Keys - 1);
            int a = Mathf.Min((int)x, Keys - 2), b = a + 1;
            float f = Mathf.Clamp01(x - a);
            int before = Mathf.Max(a - 1, 0), after = Mathf.Min(b + 1, Keys - 1);
            Vector3 p0 = Pose[before, bone], p1 = Pose[a, bone], p2 = Pose[b, bone], p3 = Pose[after, bone];
            return new Vector3(Curve(p0.x, p1.x, p2.x, p3.x, f), Curve(p0.y, p1.y, p2.y, p3.y, f), Curve(p0.z, p1.z, p2.z, p3.z, f));
        }

        private static float Curve(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            float value = 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
            return Mathf.Clamp(value, Mathf.Min(p1, p2), Mathf.Max(p1, p2));
        }

        internal static void Reset()
        {
            Active = false;
            for (int k = 0; k < Keys; k++)
                for (int b = 0; b < BoneNames.Length; b++)
                    Pose[k, b] = Vector3.zero;
        }

        internal static void Save()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder("# RavenWatch shoulder take-off pose: 5 keys × bones × local pitch yaw roll in degrees\n");
            sb.Append(Active ? "1" : "0").Append(' ').Append(Duration.ToString("0.##", c)).Append('\n');
            for (int k = 0; k < Keys; k++)
            {
                for (int b = 0; b < BoneNames.Length; b++)
                {
                    Vector3 v = Pose[k, b];
                    sb.Append(v.x.ToString("0.#", c)).Append(' ').Append(v.y.ToString("0.#", c)).Append(' ').Append(v.z.ToString("0.#", c));
                    if (b + 1 < BoneNames.Length) sb.Append(' ');
                }
                sb.Append('\n');
            }
            string directory = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(directory);
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
            else File.Move(temporary, FilePath);
            if (Plugin.Instance != null) Plugin.Instance.Log("Take-off keys saved: " + FilePath);
        }

        internal static void Load()
        {
            if (!File.Exists(FilePath)) return;
            try
            {
                CultureInfo c = CultureInfo.InvariantCulture;
                List<string> rows = new List<string>();
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string s = line.Trim();
                    if (s.Length > 0 && !s.StartsWith("#")) rows.Add(s);
                }
                if (rows.Count != Keys + 1) throw new InvalidDataException("Expected a header and exactly five pose rows.");
                string[] header = rows[0].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (header.Length != 2 || (header[0] != "0" && header[0] != "1"))
                    throw new InvalidDataException("Invalid take-off header.");
                float duration;
                if (!float.TryParse(header[1], NumberStyles.Float, c, out duration) || float.IsNaN(duration) || float.IsInfinity(duration))
                    throw new InvalidDataException("Invalid take-off duration.");
                string[] firstRow = rows[1].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                int bonesPerRow = firstRow.Length / 3;
                if (firstRow.Length % 3 != 0 || (bonesPerRow != 23 && bonesPerRow != BoneNames.Length))
                    throw new InvalidDataException("Expected either 23 or " + BoneNames.Length + " bones in each pose row.");
                Vector3[,] loaded = new Vector3[Keys, BoneNames.Length];
                for (int k = 0; k < Keys; k++)
                {
                    string[] values = rows[k + 1].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length != bonesPerRow * 3) throw new InvalidDataException("Pose row " + (k + 1) + " has an unexpected number of values.");
                    for (int b = 0; b < bonesPerRow; b++)
                    {
                        float x, y, z;
                        if (!float.TryParse(values[b * 3], NumberStyles.Float, c, out x) ||
                            !float.TryParse(values[b * 3 + 1], NumberStyles.Float, c, out y) ||
                            !float.TryParse(values[b * 3 + 2], NumberStyles.Float, c, out z) ||
                            float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) || float.IsNaN(z) || float.IsInfinity(z))
                            throw new InvalidDataException("Invalid pose value at key " + (k + 1) + ", bone " + BoneNames[b] + ".");
                        loaded[k, b] = new Vector3(Mathf.Clamp(x, -90f, 90f), Mathf.Clamp(y, -90f, 90f), Mathf.Clamp(z, -90f, 90f));
                    }
                }
                for (int k = 0; k < Keys; k++)
                    for (int b = 0; b < BoneNames.Length; b++)
                        Pose[k, b] = loaded[k, b];
                Active = header[0] == "1";
                Duration = Mathf.Clamp(duration, 0.3f, 2f);
            }
            catch (InvalidDataException e) { LogLoadError(e); }
            catch (IOException e) { LogLoadError(e); }
            catch (UnauthorizedAccessException e) { LogLoadError(e); }
            catch (ArgumentException e) { LogLoadError(e); }
            catch (SecurityException e) { LogLoadError(e); }
        }

        private static void LogLoadError(System.Exception e)
        {
            string message = "Take-off keys unreadable: " + e.Message;
            if (Plugin.Instance != null) Plugin.Instance.Log(message);
            else Debug.LogError(message);
        }

        internal sealed class Rig
        {
            private readonly Transform[] _bones = new Transform[BoneNames.Length];
            private readonly Quaternion[] _base = new Quaternion[BoneNames.Length];
            private bool _captured;

            internal Rig(Transform root)
            {
                Dictionary<string, Transform> found = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (!found.ContainsKey(t.name)) found.Add(t.name, t);
                for (int i = 0; i < BoneNames.Length; i++) found.TryGetValue(BoneNames[i], out _bones[i]);
            }

            internal bool HasBone(int index) { return index >= 0 && index < _bones.Length && _bones[index] != null; }
            internal void RecaptureBase() { _captured = false; }

            internal void Apply(float x, float weight, bool resetToBase)
            {
                if (!_captured)
                {
                    for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) _base[i] = _bones[i].localRotation;
                    _captured = true;
                }
                weight = Mathf.Clamp01(weight);
                for (int i = 0; i < _bones.Length; i++)
                {
                    Transform bone = _bones[i];
                    if (bone == null) continue;
                    Quaternion basis = resetToBase ? _base[i] : bone.localRotation;
                    if (resetToBase) bone.localRotation = _base[i];
                    Vector3 angles = At(x, i) * weight;
                    bone.localRotation = basis * Quaternion.Euler(angles.x, angles.y, angles.z);
                }
            }
        }
    }
}
#endif
