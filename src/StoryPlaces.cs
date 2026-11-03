#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Місця»: named spots of the story in a world (zala, rana, kamin_torvalda, pagorb_odina…) — where scenes, cameras and
    // actors are put. For now the author's own, in a file per world (config/RavenWatch/blueprints/places/<world>.txt):
    // one a line "name x y z yaw". A blueprint keeps the places inside its circle and brings them back where it is placed
    // (<blueprint>.<name>). Later the story's places go into the world itself (shared, the server's).
    internal static class StoryPlaces
    {
        internal sealed class Place { internal string Name; internal Vector3 Pos; internal float Yaw; }

        private static readonly List<Place> _all = new List<Place>();
        private static long _world = -1L;

        internal static List<Place> All { get { Sync(); return _all; } }

        private static string FileOf(long world)
        {
            string name = ZNet.World != null ? ZNet.World.m_name : "world";
            foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            return Path.Combine(Path.Combine(Blueprint.Root, "places"), name + "_" + world.ToString(CultureInfo.InvariantCulture) + ".txt");
        }

        // the list of this world (another world loaded: its own list)
        private static void Sync()
        {
            long w = ZNet.instance != null && ZNet.World != null ? ZNet.instance.GetWorldUID() : 0L;
            if (w == _world) return;
            _world = w;
            _all.Clear();
            if (w == 0L || !Blueprint.On) return;
            string f = FileOf(w);
            if (!File.Exists(f)) return;
            foreach (string raw in File.ReadAllLines(f, Encoding.UTF8))
            {
                string[] a = raw.Trim().Split(' ');
                float x, y, z, yaw;
                if (a.Length < 5 || !TryF(a[1], out x) || !TryF(a[2], out y) || !TryF(a[3], out z) || !TryF(a[4], out yaw)) continue;
                _all.Add(new Place { Name = a[0], Pos = new Vector3(x, y, z), Yaw = yaw });
            }
        }

        private static void Write()
        {
            if (_world == 0L || !Blueprint.On) return;
            string f = FileOf(_world);
            Directory.CreateDirectory(Path.GetDirectoryName(f));
            StringBuilder sb = new StringBuilder();
            foreach (Place p in _all)
                sb.Append(p.Name).Append(' ').Append(F(p.Pos.x)).Append(' ').Append(F(p.Pos.y)).Append(' ').Append(F(p.Pos.z)).Append(' ').Append(F(p.Yaw)).Append('\n');
            File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false));
        }

        internal static Place Find(string name)
        {
            foreach (Place p in All) if (p.Name == name) return p;
            return null;
        }

        // a new place, or that one moved here
        internal static string Set(string name, Vector3 pos, float yaw)
        {
            if (!GoodName(name)) return "Назва місця: латиниця, цифри, _ - . (наприклад vognyshche).";
            Place p = Find(name);
            bool moved = p != null;
            if (p == null) { p = new Place { Name = name }; _all.Add(p); }
            p.Pos = pos; p.Yaw = yaw;
            Write();
            return (moved ? "Місце «" + name + "» перенесено сюди." : "Місце «" + name + "» поставлено.");
        }

        internal static string Remove(string name)
        {
            Place p = Find(name);
            if (p == null) return "Нема місця " + name;
            _all.Remove(p);
            Write();
            return "Місце «" + name + "» видалено.";
        }

        internal static bool GoodName(string n)
        {
            if (string.IsNullOrEmpty(n) || n.Length > 48) return false;
            foreach (char ch in n) if (!(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || ch == '_' || ch == '-' || ch == '.')) return false;
            return true;
        }

        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static bool TryF(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
    }
}
#endif
