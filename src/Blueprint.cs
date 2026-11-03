#if RAVEN_ITEM && RAVEN_NPC
// (the author's tool for the Hall: from 2.9 — asleep without its folder)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Креслення»: the author's tool for buildings the mod puts into the world (the Hall of the Watch first). It sleeps
    // unless the folder config/RavenWatch/blueprints exists (players never have it), like the Studio. The window
    // raven story (StoryPanel, tab «Креслення») drives it; the console words do the same:
    //   raven blueprint save <name> [radius=20]  — every piece within the radius round you, kept as a shape only: which piece,
    //                                              where, how turned (no chest contents, no signs' words, no state). You stand
    //                                              at the centre on the floor and look at the front: that is the blueprint's
    //                                              origin and its «forward». The story places (StoryPlaces) inside go in too.
    //   raven blueprint list · preview <name>|off · place <name> · undo
    // A file: "# RavenWatch blueprint 1", "name …", "pieces N", then one piece a line: prefab x y z qx qy qz qw (relative),
    // and the places: "@mark <name> x y z yaw". Placed pieces carry RavenWatch_bp in their ZDO: they neither wear nor fall
    // without support (the ground under a building put somewhere else is never the ground it was built on).
    internal static class Blueprint
    {
        internal const string Mark = "RavenWatch_bp";
        private const int PerFrame = 40;

        internal static string Root { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "blueprints"); } }
        internal static bool On { get { return Directory.Exists(Root); } }

        // Item: what hangs on it (an item stand: a head, a weapon…), with its look — «item:<prefab>:<variant>:<quality>:<turn>»
        // after the rotation on its line (the user 2026-11-01: only the item stands; no chests, no armour stands, no signs)
        internal struct Part { internal string Prefab; internal Vector3 Pos; internal Quaternion Rot; internal string Item; internal int Variant, Quality, Turn; }
        internal struct Spot { internal string Name; internal Vector3 Pos; internal float Yaw; }

        internal sealed class Info
        {
            internal string Name;
            internal int Pieces, Kinds;
            internal Vector3 Size;
            internal List<Spot> Marks = new List<Spot>();
        }

        // the ghost: one root, its pieces under it in the blueprint's own frame (moving the root moves the building)
        internal static GameObject Ghost;
        internal static string GhostName = "";
        private static readonly List<ZDOID> _last = new List<ZDOID>();
        private static string _lastName = "";
        private static readonly List<Piece> _near = new List<Piece>();
        private static readonly int GhostLayer = LayerMask.NameToLayer("ghost");
        internal static bool Placing { get; private set; }
        internal static int LastCount { get { return _last.Count; } }
        internal static string LastName { get { return _lastName; } }

        internal static string Cmd(Player p, string[] a)
        {
            if (!On) return "Креслення сплять: немає теки " + Root;
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            string name = a.Length > 2 ? a[2] : "";
            switch (what)
            {
                case "save": return Save(p, name, a.Length > 3 ? Num(a[3], 20f) : 20f, a.Length > 4 && a[4].ToLowerInvariant() == "force");
                case "list": return List();
                case "preview": return name.ToLowerInvariant() == "off" ? GhostOff() : Preview(name, p.transform.position, Facing(p));
                case "place": return Place(name, p.transform.position, Facing(p), a.Length > 3 && a[3].ToLowerInvariant() == "force");
                case "undo": return Undo();
                case "name": return NamePiece(p, name);
                case "hall": return HallSpawn.Tell(p);
                case "layer": return HallParts.Layer(HallParts.Aimed(p), name.ToLowerInvariant());
                case "state": return HallState.Cmd(a);
                case "frame": return Frame(p, name, a.Length > 3 ? a[3].ToLowerInvariant() : "");
                case "looks": return Looks(p);
            }
            return "raven blueprint save <name> [radius] · list · preview <name>|off · place <name> [force] · undo · name <piece name>|- · layer ruin|repaired|lived|fixed[,+1.1|,-1.1]|- · state [1|1.1|2|3] [in|out]|cancel · frame <name> [find|here] · hall   (or the window: raven story)";
        }

        // the author names the piece he looks at, in a building a blueprint put there (for the story's scripts:
        // HallParts.Named). The name goes into the mod's names file, never into the world: added any time, even after a
        // release, it finds the piece in every world where that building stands
        private static string NamePiece(Player p, string name)
        {
            return HallParts.Name(HallParts.Aimed(p), name);
        }

        // ---------------------------------------------------------------- what stands round you now (the window counts live)
        internal static List<Piece> Around(Vector3 c, float radius)
        {
            List<Piece> list = new List<Piece>();
            _near.Clear();
            Piece.GetAllPiecesInRadius(c, radius + 60f, _near); // (the game's own list of pieces; its circle is round in height too: a wide one, then ours)
            foreach (Piece pc in _near)
            {
                if (pc == null) continue;
                ZNetView nv = pc.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue; // (the ghost's pieces have none)
                Vector3 at = pc.transform.position;
                if (new Vector2(at.x - c.x, at.z - c.z).magnitude <= radius) list.Add(pc);
            }
            return list;
        }

        // the game's own prefabs come from its own asset files; a mod's are made while the game runs (a copy hung in the
        // scene, or a prefab of another bundle) — a hint only, the list is checked by eye
        internal static bool LooksVanilla(string prefab)
        {
            if (prefab.StartsWith("RavenWatch_")) return true; // (ours: the Workshop's pieces)
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            return g != null && !g.scene.IsValid();
        }

        // which kinds of piece round you have a look of their own when ruined (the game shows its broken model at ≤ 25%
        // strength — HallState's abandoned Hall) and which look new whatever their strength: one model for all, or no
        // strength at all (our Meshy pieces). Into the log too, the whole list
        internal static string Looks(Player p)
        {
            Dictionary<string, int> ruins = new Dictionary<string, int>(), same = new Dictionary<string, int>();
            foreach (Piece pc in Around(p.transform.position, 40f))
            {
                string k = Utils.GetPrefabName(pc.gameObject);
                WearNTear w = pc.GetComponent<WearNTear>();
                bool own = w != null && w.m_broken != null && w.m_broken != w.m_new;
                Dictionary<string, int> d = own ? ruins : same;
                int n; d.TryGetValue(k, out n); d[k] = n + 1;
            }
            if (ruins.Count + same.Count == 0) return "У колі 40 м нема деталей.";
            StringBuilder sb = new StringBuilder("Мають зруйнований вигляд (" + ruins.Count + " видів): ");
            foreach (KeyValuePair<string, int> kv in ruins) sb.Append(kv.Key).Append('×').Append(kv.Value).Append(", ");
            sb.Append("\nВиглядають однаково завжди (" + same.Count + " видів): ");
            foreach (KeyValuePair<string, int> kv in same) sb.Append(kv.Key).Append('×').Append(kv.Value).Append(", ");
            if (Plugin.Instance != null) Plugin.Instance.Log("Blueprint looks: " + sb.ToString().Replace('\n', ' '));
            return sb.ToString().TrimEnd(',', ' ') + "\n(повний список — і в лозі)";
        }

        // a blueprint's frame (BlueprintFrame): where it is fixed; «find» — from the building round you (one saved before
        // frames); «here» — anew where you stand, looking at the front (only for a new building: the old names, layers
        // and the Halls in players' worlds keep the old frame)
        internal static string Frame(Player p, string name, string how)
        {
            if (!GoodName(name)) return "raven blueprint frame <назва> [find|here]";
            Vector3 c; Quaternion rot;
            if (how == "find")
            {
                string why = BlueprintFrame.Find(name, p.transform.position, 80f);
                return why == null ? "Рамку «" + name + "» знайдено в будівлі й закріплено." : "Рамку не знайдено: " + why + ".";
            }
            if (how == "here")
            {
                BlueprintFrame.Set(name, p.transform.position, Facing(p).eulerAngles.y);
                return "Рамку «" + name + "» закріплено тут." + (File.Exists(FileOf(name)) ? " Увага: старе креслення збережене з іншою рамкою — його імена й шари більше не збіжуться." : "");
            }
            if (BlueprintFrame.Get(name, out c, out rot)) return "Рамка «" + name + "»: " + Mathf.RoundToInt(c.x) + " " + Mathf.RoundToInt(c.z) + ", перед " + Mathf.RoundToInt(rot.eulerAngles.y) + "°, " + Mathf.RoundToInt(Vector3.Distance(p.transform.position, c)) + " м від тебе.";
            string w = BlueprintFrame.WorldOf(name);
            return w.Length > 0 ? "Рамка «" + name + "» закріплена в іншому світі: " + w + "." : "Рамки «" + name + "» ще нема: перше збереження закріпить її (або find — знайти в будівлі за старим кресленням).";
        }

        // ---------------------------------------------------------------- save
        internal static string Save(Player p, string name, float radius, bool force = false)
        {
            if (!GoodName(name)) return "Назва креслення: латиниця, цифри, _ чи - (наприклад zala).";
            // its frame: the one fixed in this world (BlueprintFrame) — found from the building for one saved before
            // frames; a first save fixes it where you stand, looking at the front
            Vector3 c; Quaternion facing;
            string fresh = "";
            if (!BlueprintFrame.Get(name, out c, out facing))
            {
                if (File.Exists(FileOf(name)))
                {
                    string why = BlueprintFrame.Find(name, p.transform.position, radius + 20f);
                    if (why != null || !BlueprintFrame.Get(name, out c, out facing))
                        return "НЕ ЗБЕРЕЖЕНО: не знайшов рамку старого «" + name + "» у цій будівлі (" + why + "). Імена й шари прив'язані до неї — тому не перезаписую. Якщо це справді нова будівля: raven blueprint frame " + name + " here (стань у центр, дивись на вхід).";
                    fresh = "\nРамку старого креслення знайдено в будівлі: деталі лишилися на своїх місцях.";
                }
                else
                {
                    c = p.transform.position; facing = Facing(p);
                    BlueprintFrame.Set(name, c, facing.eulerAngles.y);
                    fresh = "\nРамку закріплено тут: далі «" + name + "» зберігатиметься відносно неї, хоч би де ти стояв.";
                }
            }
            // from far away its pieces are not loaded: the save would lose them
            float far = new Vector2(p.transform.position.x - c.x, p.transform.position.z - c.z).magnitude;
            if (far > radius + 20f) return "НЕ ЗБЕРЕЖЕНО: ти за " + Mathf.RoundToInt(far) + " м від середини «" + name + "» — здалеку її деталі не завантажені. Підійди ближче.";
            Quaternion inv = Quaternion.Inverse(facing);
            List<Part> parts = new List<Part>();
            Dictionary<string, int> kinds = new Dictionary<string, int>();
            foreach (Piece pc in Around(c, radius))
            {
                string prefab = Utils.GetPrefabName(pc.gameObject);
                Part part = new Part { Prefab = prefab, Pos = inv * (pc.transform.position - c), Rot = inv * pc.transform.rotation };
                Hung(pc, ref part);
                parts.Add(part);
                int n; kinds.TryGetValue(prefab, out n); kinds[prefab] = n + 1;
            }
            // far fewer pieces than the last save: not loaded, a smaller circle, or a building gone — asked first
            Info old = File.Exists(FileOf(name)) ? Describe(name) : null;
            if (old != null && !force && parts.Count < old.Pieces * 0.8f)
                return "НЕ ЗБЕРЕЖЕНО: у колі " + parts.Count + " деталей, а в останньому збереженні " + old.Pieces + ". Чи все завантажено й чи радіус той самий? Якщо так і задумано: raven blueprint save " + name + " " + Mathf.RoundToInt(radius) + " force.";
            if (parts.Count == 0) return "У колі " + radius + " м немає жодного будівельного елемента.";
            List<Spot> marks = new List<Spot>();
            float baseYaw = facing.eulerAngles.y;
            foreach (StoryPlaces.Place pl in StoryPlaces.All)
                if (new Vector2(pl.Pos.x - c.x, pl.Pos.z - c.z).magnitude <= radius)
                    marks.Add(new Spot { Name = Short(pl.Name), Pos = inv * (pl.Pos - c), Yaw = Mathf.Repeat(pl.Yaw - baseYaw, 360f) });
            float ground = BlueprintGround.Measure(parts, c, facing); // (how high the ground stood under the floor here: placed elsewhere, it is made so again)
            StringBuilder sb = new StringBuilder();
            sb.Append("# RavenWatch blueprint 1\nname ").Append(name).Append("\npieces ").Append(parts.Count).Append("\nground ").Append(F(ground)).Append('\n');
            foreach (Part pt in parts)
                sb.Append(pt.Prefab).Append(' ').Append(F(pt.Pos.x)).Append(' ').Append(F(pt.Pos.y)).Append(' ').Append(F(pt.Pos.z)).Append(' ')
                  .Append(Q(pt.Rot.x)).Append(' ').Append(Q(pt.Rot.y)).Append(' ').Append(Q(pt.Rot.z)).Append(' ').Append(Q(pt.Rot.w))
                  .Append(string.IsNullOrEmpty(pt.Item) ? "" : " item:" + pt.Item + ":" + pt.Variant + ":" + pt.Quality + ":" + pt.Turn).Append('\n');
            foreach (Spot s in marks)
                sb.Append("@mark ").Append(s.Name).Append(' ').Append(F(s.Pos.x)).Append(' ').Append(F(s.Pos.y)).Append(' ').Append(F(s.Pos.z)).Append(' ').Append(F(s.Yaw)).Append('\n');
            bool kept = File.Exists(FileOf(name));
            if (kept) File.Copy(FileOf(name), FileOf(name) + ".bak", true); // (the one before: one save back, by hand)
            File.WriteAllText(FileOf(name), sb.ToString(), new UTF8Encoding(false));
            List<string> names = new List<string>(kinds.Keys);
            names.Sort((x, y) => kinds[y].CompareTo(kinds[x]));
            int hung = 0;
            foreach (Part pt in parts) if (!string.IsNullOrEmpty(pt.Item)) hung++;
            StringBuilder r = new StringBuilder("Креслення «" + name + "» збережено: " + parts.Count + " елементів, " + names.Count + " видів, міток " + marks.Count + ", речей на кріпленнях " + hung + ", земля " + F(-ground) + " м під підлогою" + (kept ? " (попереднє — у " + name + ".txt.bak)" : ""));
            List<string> odd = new List<string>();
            foreach (string k in names) if (!LooksVanilla(k)) odd.Add(k);
            if (odd.Count > 0) r.Append("\nСХОЖЕ НЕ НА ВАНІЛЬНІ (перевір): ").Append(string.Join(", ", odd.ToArray()));
            r.Append(fresh);
            if (Plugin.Instance != null)
            {
                StringBuilder all = new StringBuilder("Blueprint " + name + ":");
                foreach (string k in names) all.Append(' ').Append(k).Append('×').Append(kinds[k]);
                Plugin.Instance.Log(all.ToString());
            }
            return r.ToString();
        }

        // ---------------------------------------------------------------- read
        internal static List<Part> Load(string name, out List<Spot> marks, out string error)
        {
            error = null;
            marks = new List<Spot>();
            if (!GoodName(name)) { error = "Нема такої назви креслення: " + name; return null; }
            string f = FileOf(name);
            if (!File.Exists(f)) { error = "Нема креслення " + f; return null; }
            List<Part> parts = Parse(File.ReadAllLines(f, Encoding.UTF8), marks);
            if (parts.Count == 0) error = "Креслення порожнє: " + f;
            return parts;
        }

        // the lines of a blueprint (a file of the author's, or one built into the mod: HallSpawn)
        internal static List<Part> Parse(string[] lines, List<Spot> marks)
        {
            List<Part> parts = new List<Part>();
            foreach (string raw in lines)
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#") || l.StartsWith("name ") || l.StartsWith("pieces ") || l.StartsWith("ground ")) continue;
                string[] w = l.Split(' ');
                if (w[0] == "@mark")
                {
                    float mx, my, mz, yaw;
                    if (w.Length >= 6 && TryF(w[2], out mx) && TryF(w[3], out my) && TryF(w[4], out mz) && TryF(w[5], out yaw))
                        marks.Add(new Spot { Name = w[1], Pos = new Vector3(mx, my, mz), Yaw = yaw });
                    continue;
                }
                if (w.Length < 8) continue;
                float x, y, z, qx, qy, qz, qw;
                if (!TryF(w[1], out x) || !TryF(w[2], out y) || !TryF(w[3], out z) || !TryF(w[4], out qx) || !TryF(w[5], out qy) || !TryF(w[6], out qz) || !TryF(w[7], out qw)) continue;
                Part part = new Part { Prefab = w[0], Pos = new Vector3(x, y, z), Rot = new Quaternion(qx, qy, qz, qw) };
                for (int k = 8; k < w.Length; k++)
                {
                    if (!w[k].StartsWith("item:")) continue;
                    string[] it = w[k].Split(':');
                    int v, q, t;
                    if (it.Length < 5 || it[1].Length == 0 || !int.TryParse(it[2], out v) || !int.TryParse(it[3], out q) || !int.TryParse(it[4], out t)) continue;
                    part.Item = it[1]; part.Variant = v; part.Quality = q; part.Turn = t;
                }
                parts.Add(part);
            }
            return parts;
        }

        // how high the ground stood under the floor where it was saved (a blueprint from before «ground»: just under it)
        internal static float GroundOf(string name)
        {
            string f = FileOf(name);
            return GoodName(name) && File.Exists(f) ? GroundIn(File.ReadAllLines(f, Encoding.UTF8)) : BlueprintGround.NoGround;
        }

        internal static float GroundIn(string[] lines)
        {
            foreach (string raw in lines)
            {
                float g;
                if (raw.StartsWith("ground ") && TryF(raw.Substring(7).Trim(), out g)) return g;
            }
            return BlueprintGround.NoGround;
        }

        internal static List<string> Names()
        {
            List<string> list = new List<string>();
            if (!On) return list;
            foreach (string f in Directory.GetFiles(Root, "*.txt")) { string n = Path.GetFileNameWithoutExtension(f); if (n.IndexOf('.') < 0) list.Add(n); } // (zala.names.txt, zala.layers.txt: a blueprint's files, not blueprints)
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        internal static Info Describe(string name)
        {
            string err;
            List<Spot> marks;
            List<Part> parts = Load(name, out marks, out err);
            if (parts == null || err != null) return null;
            Info i = new Info { Name = name, Pieces = parts.Count, Marks = marks };
            HashSet<string> kinds = new HashSet<string>();
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            foreach (Part pt in parts) { kinds.Add(pt.Prefab); lo = Vector3.Min(lo, pt.Pos); hi = Vector3.Max(hi, pt.Pos); }
            i.Kinds = kinds.Count;
            i.Size = hi - lo;
            return i;
        }

        internal static string Delete(string name)
        {
            if (!GoodName(name) || !File.Exists(FileOf(name))) return "Нема креслення " + name;
            if (GhostName == name) GhostOff();
            File.Copy(FileOf(name), FileOf(name) + ".deleted", true); // (by hand back: rename it .txt again)
            File.Delete(FileOf(name));
            return "Креслення «" + name + "» видалено (копія — " + name + ".txt.deleted).";
        }

        private static string List()
        {
            List<string> names = Names();
            if (names.Count == 0) return "Креслень ще нема (" + Root + ").";
            StringBuilder sb = new StringBuilder("Креслення (" + names.Count + "):");
            foreach (string n in names) { Info i = Describe(n); sb.Append("\n  ").Append(n).Append(" — ").Append(i != null ? i.Pieces : 0).Append(" елементів"); }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- preview: a ghost, nothing networked
        internal static string Preview(string name, Vector3 at, Quaternion rot)
        {
            string err;
            List<Spot> marks;
            List<Part> parts = Load(name, out marks, out err);
            if (parts == null || err != null) return err;
            BlueprintGround.Fit fit = BlueprintGround.Check(parts, at, rot);
            if (fit.Seen) at.y = fit.Ground - GroundOf(name); // (shown where it would truly stand)
            GhostOff();
            Ghost = new GameObject("RavenWatch_blueprint_ghost");
            Ghost.transform.SetPositionAndRotation(at, rot);
            GhostName = name;
            int made = 0, missing = 0;
            foreach (Part pt in parts)
            {
                GameObject pf = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(pt.Prefab) : null;
                if (pf == null) { missing++; continue; }
                GameObject g;
                ZNetView.m_forceDisableInit = true;
                try { g = UnityEngine.Object.Instantiate(pf, Ghost.transform, false); }
                finally { ZNetView.m_forceDisableInit = false; }
                g.transform.localPosition = pt.Pos;
                g.transform.localRotation = pt.Rot;
                if (GhostLayer >= 0) foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = GhostLayer; // (the game's own placement ghost layer: no piece list, comfort or ward counts it)
                foreach (Collider col in g.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                foreach (MonoBehaviour mb in g.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                made++;
            }
            return "Привид «" + name + "»: " + made + " елементів" + (missing > 0 ? ", не знайдено в грі " + missing : "") + ". "
                 + (fit.Ok ? "Місце годиться: земля відходить до " + fit.Dev.ToString("0.0") + " м, її вирівняє." : fit.Why);
        }

        internal static string GhostOff()
        {
            if (Ghost == null) return "Привида нема.";
            UnityEngine.Object.Destroy(Ghost);
            Ghost = null;
            GhostName = "";
            return "Привида прибрано.";
        }

        // ---------------------------------------------------------------- place: real pieces, a few each frame
        internal static string Place(string name, Vector3 at, Quaternion rot, bool force = false)
        {
            if (Placing) return "Ще ставлю попереднє.";
            string err;
            List<Spot> marks;
            List<Part> parts = Load(name, out marks, out err);
            if (parts == null || err != null) return err;
            if (Plugin.Instance == null) return "Мод ще не готовий.";
            // the ground first: the place checked, the building set at its ground's height, the land made flat under it
            BlueprintGround.Fit fit = BlueprintGround.Check(parts, at, rot);
            if (!fit.Ok && !force) return fit.Why + " (поставити все одно: raven blueprint place " + name + " force)";
            string ground = "";
            if (fit.Seen)
            {
                at.y = fit.Ground - GroundOf(name);
                ground = BlueprintGround.Level(parts, at, rot, fit.Ground);
            }
            GhostOff();
            _last.Clear();
            _lastName = name;
            Placing = true;
            // the blueprint's places come with it, named <blueprint>.<mark> (one of that name already: moved here)
            foreach (Spot s in marks)
                StoryPlaces.Set(name + "." + s.Name, at + rot * s.Pos, Mathf.Repeat(rot.eulerAngles.y + s.Yaw, 360f));
            Plugin.Instance.StartCoroutine(Build(parts, at, rot, name, null));
            return "Ставлю «" + name + "»: " + parts.Count + " елементів" + (marks.Count > 0 ? ", міток " + marks.Count : "") + (ground.Length > 0 ? "; " + ground : "") + "…";
        }

        // a building the mod puts into the world itself (HallSpawn): its places, then its pieces; done(made, missing) at the end
        internal static void Spawn(List<Part> parts, List<Spot> marks, Vector3 at, Quaternion rot, string name, Action<int, int> done)
        {
            Placing = true;
            _last.Clear();
            _lastName = name;
            foreach (Spot s in marks)
                StoryPlaces.Set(name + "." + s.Name, at + rot * s.Pos, Mathf.Repeat(rot.eulerAngles.y + s.Yaw, 360f));
            Plugin.Instance.StartCoroutine(Build(parts, at, rot, name, done, done != null ? Quick : PerFrame));
        }

        // the world's own building (HallSpawn) goes up fast: it comes into sight as the player does, all at once is best
        private const int Quick = 200;

        private static IEnumerator Build(List<Part> parts, Vector3 c, Quaternion baseRot, string name, Action<int, int> done, int perFrame = PerFrame)
        {
            int made = 0, missing = 0;
            try
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    Part pt = parts[i];
                    GameObject pf = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(pt.Prefab) : null;
                    if (pf == null) missing++;
                    else
                    {
                        GameObject g = UnityEngine.Object.Instantiate(pf, c + baseRot * pt.Pos, baseRot * pt.Rot);
                        ZNetView nv = g.GetComponent<ZNetView>();
                        if (nv != null && nv.IsValid())
                        {
                            nv.GetZDO().Set(Mark, 1);
                            HallParts.Stamp(nv.GetZDO(), name, pt.Pos); // (which building, and where in it: the story's scripts find it by these — HallParts)
                            _last.Add(nv.GetZDO().m_uid);
                            BlueprintWearPatch.Forget(g);
                        }
                        Fireplace fire = g.GetComponent<Fireplace>();
                        if (fire != null) fire.m_infiniteFuel = true; // (its Awake ran before the mark: HallLightPatch catches it on every later load)
                        if (!string.IsNullOrEmpty(pt.Item)) Hang(g, nv, pt);
                        made++;
                    }
                    if (i % perFrame == perFrame - 1) yield return null;
                }
            }
            finally { Placing = false; }
            if (done != null) { done(made, missing); yield break; }
            Say("Креслення «" + name + "» поставлено: " + made + " елементів" + (missing > 0 ? ", не знайдено в грі " + missing : ""));
        }

        internal static string Undo()
        {
            if (Placing) return "Ще ставлю — зачекай.";
            if (_last.Count == 0) { string land = BlueprintGround.Undo(); return land.Length > 0 ? land.Trim() : "Нема що прибирати (лише останнє поставлене в цій грі)."; }
            int gone = 0;
            foreach (ZDOID id in _last)
            {
                ZDO z = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
                if (z == null) continue;
                ZNetView nv = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(z) : null;
                if (nv != null) { nv.ClaimOwnership(); ZNetScene.instance.Destroy(nv.gameObject); }
                else { z.SetOwner(ZDOMan.GetSessionID()); ZDOMan.instance.DestroyZDO(z); }
                gone++;
            }
            _last.Clear();
            return "Прибрано " + gone + " елементів «" + _lastName + "» (мітки лишились — їх видаляють у «Місцях»)." + BlueprintGround.Undo();
        }

        // ---------------------------------------------------------------- what hangs on an item stand
        // read: the stand's own ZDO (the game keeps the item there by its prefab's hash)
        private static void Hung(Piece pc, ref Part part)
        {
            ItemStand stand = pc.GetComponent<ItemStand>();
            ZNetView nv = pc.GetComponent<ZNetView>();
            if (stand == null || nv == null || !nv.IsValid() || !stand.HaveAttachment() || ObjectDB.instance == null) return;
            GameObject item = ObjectDB.instance.GetItemPrefab(stand.GetAttachedItem());
            if (item == null) return;
            ZDO z = nv.GetZDO();
            part.Item = item.name;
            part.Variant = z.GetInt(ZDOVars.s_variant);
            part.Quality = z.GetInt(ZDOVars.s_quality, 1);
            part.Turn = z.GetInt(ZDOVars.s_type);
        }

        // write: as the game does when one hangs a thing (the item's data in the stand's ZDO, then its look to everyone)
        private static void Hang(GameObject g, ZNetView nv, Part pt)
        {
            ItemStand stand = g.GetComponent<ItemStand>();
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(pt.Item) : null;
            ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
            if (stand == null || drop == null || nv == null || !nv.IsValid()) { if (Plugin.Instance != null) Plugin.Instance.Log("Blueprint: cannot hang " + pt.Item + " on " + pt.Prefab); return; }
            ItemDrop.ItemData data = drop.m_itemData.Clone();
            data.m_stack = 1;
            data.m_variant = pt.Variant;
            data.m_quality = Mathf.Max(1, pt.Quality);
            data.m_equipped = false;
            data.m_dropPrefab = item;
            int hash = item.name.GetStableHashCode();
            ZDO z = nv.GetZDO();
            z.Set(ZDOVars.s_item, hash);
            ItemDrop.SaveToZDO(data, z);
            z.Set(ZDOVars.s_type, pt.Turn);
            nv.InvokeRPC(ZNetView.Everybody, "SetVisualItem", hash, data.m_variant, data.m_quality, pt.Turn);
        }

        // ---------------------------------------------------------------- helpers
        // a piece a blueprint put into the world (its building stamped on it: HallParts), not the author's own by hand
        internal static bool Copy(ZNetView nv)
        {
            return nv != null && nv.IsValid() && nv.GetZDO().GetString(HallParts.KOf, "").Length > 0;
        }

        internal static bool Marked(ZNetView nv)
        {
            return nv != null && nv.IsValid() && nv.GetZDO().GetInt(Mark) == 1;
        }

        internal static Quaternion Facing(Player p)
        {
            Vector3 f = GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward;
            f.y = 0f;
            return f.sqrMagnitude < 0.001f ? Quaternion.identity : Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        internal static bool GoodName(string n)
        {
            if (string.IsNullOrEmpty(n) || n.Length > 40) return false;
            foreach (char ch in n) if (!(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || ch == '_' || ch == '-')) return false;
            return true;
        }

        // a place «zala.vognyshche» kept inside a blueprint by its own short name
        private static string Short(string n) { int d = n.LastIndexOf('.'); return d >= 0 ? n.Substring(d + 1) : n; }

        internal static string FileOf(string name) { return Path.Combine(Root, name + ".txt"); }
        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static string Q(float v) { return v.ToString("0.#####", CultureInfo.InvariantCulture); }
        private static bool TryF(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
        private static float Num(string s, float d) { float v; return TryF(s, out v) && v > 0f ? Mathf.Min(v, 200f) : d; }

        internal static void Say(string s)
        {
            if (Plugin.Instance != null) Plugin.Instance.Log(s);
#if RAVEN_STORY
            StoryPanel.Note = s; // (the story window: 3.0)
#endif
            if (global::Console.instance != null) global::Console.instance.Print(s);
            if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, s);
        }
    }

    // a building of a blueprint neither wears nor falls without support. The game asks every piece near anyone, often:
    // the answer is kept per piece (a piece's mark never changes once placed; one just placed asks again). So does the
    // author's own Hall, the one he builds by hand (within its frame, BlueprintFrame): taking a beam away, or a layer
    // hidden while he looks at a state (its colliders off), must not bring the rest down
    [HarmonyPatch(typeof(WearNTear), "UpdateWear")]
    internal static class BlueprintWearPatch
    {
        private static readonly Dictionary<int, bool> Known = new Dictionary<int, bool>();

        internal static void Forget(GameObject g)
        {
            WearNTear w = g != null ? g.GetComponent<WearNTear>() : null;
            if (w != null) Known.Remove(w.GetInstanceID());
        }

        internal static void ForgetAll() { Known.Clear(); } // (a frame fixed or moved: the author's Hall is elsewhere)

        private static bool Prefix(WearNTear __instance) { return !IsMarked(__instance); }

        internal static bool IsMarked(WearNTear w)
        {
            int id = w.GetInstanceID();
            bool marked;
            if (!Known.TryGetValue(id, out marked))
            {
                ZNetView nv = w.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) return false; // (not ready: ask again next time)
                marked = Blueprint.Marked(nv) || Reference(w, nv);
                if (Known.Count > 50000) Known.Clear(); // (the pieces long gone)
                Known[id] = marked;
            }
            return marked;
        }

        private static bool Reference(WearNTear w, ZNetView nv)
        {
            Vector3 c; Quaternion rot;
            string of;
            return nv.GetZDO().GetString(HallParts.KOf, "").Length == 0 // (not a copy the blueprint placed)
                && HallParts.AuthorFrame(w.transform.position, out of, out c, out rot);
        }
    }

    // nor takes any damage: no blow, no fire, no monster (the Hall stands as the world's own)
    [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
    internal static class BlueprintDamagePatch
    {
        private static bool Prefix(WearNTear __instance) { return !BlueprintWearPatch.IsMarked(__instance); }
    }

    [HarmonyPatch(typeof(WearNTear), "ApplyDamage")]
    internal static class BlueprintApplyDamagePatch
    {
        private static bool Prefix(WearNTear __instance, ref bool __result)
        {
            if (!BlueprintWearPatch.IsMarked(__instance)) return true;
            __result = false;
            return false;
        }
    }

    // round the standing Hall of the Watch nothing is built, taken down, dug or levelled — as round the start's stones
    // (the game's own question, so its own «$msg_nobuildzone»)
    [HarmonyPatch(typeof(Location), "IsInsideNoBuildLocation")]
    internal static class HallNoBuildPatch
    {
        private static void Postfix(Vector3 point, ref bool __result)
        {
            if (!__result && HallSpawn.InNoBuild(point)) __result = true;
        }
    }

    // what hangs on the Hall's item stands stays there: nobody but the author takes it down, turns it, or hangs another.
    // One the story wants touched now (LocTouch: Munin's shield) answers E with the story's event — the author's own too
    [HarmonyPatch(typeof(ItemStand), "Interact")]
    internal static class BlueprintStandPatch
    {
        private static bool Prefix(ItemStand __instance, Humanoid user, bool hold, bool alt, ref bool __result)
        {
            LocTouch t = __instance.GetComponent<LocTouch>();
            if (t != null && t.On) { __result = t.Interact(user, hold, alt); return false; }
            if (!Blueprint.Copy(__instance.GetComponent<ZNetView>())) return true; // (a copy: nobody, the author too — his own Hall he dresses as he likes)
            __result = false; // (quietly: it is simply not a thing to touch — the user 2026-11-01)
            return false;
        }
    }

    // nor says anything when looked at: no «[E] take», no name — the thing just hangs there (but a touch of the story's)
    [HarmonyPatch(typeof(ItemStand), "GetHoverText")]
    internal static class BlueprintStandHoverPatch
    {
        private static void Postfix(ItemStand __instance, ref string __result)
        {
            LocTouch t = __instance.GetComponent<LocTouch>();
            if (t != null && t.On) __result = t.GetHoverText();
            else if (Blueprint.Copy(__instance.GetComponent<ZNetView>())) __result = "";
        }
    }

    [HarmonyPatch(typeof(ItemStand), "GetHoverName")]
    internal static class BlueprintStandNamePatch
    {
        private static void Postfix(ItemStand __instance, ref string __result)
        {
            LocTouch t = __instance.GetComponent<LocTouch>();
            if (t != null && t.On) __result = t.GetHoverName();
            else if (Blueprint.Copy(__instance.GetComponent<ZNetView>())) __result = "";
        }
    }

    [HarmonyPatch(typeof(ItemStand), "UseItem")]
    internal static class BlueprintStandUsePatch
    {
        private static bool Prefix(ItemStand __instance, Humanoid user, ref bool __result)
        {
            if (!Blueprint.Copy(__instance.GetComponent<ZNetView>())) return true; // (a copy: nobody, the author too — his own Hall he dresses as he likes)
            __result = true; // (handled, quietly: the item stays in the bag)
            return false;
        }
    }

    // nor comes down under a player's hammer: only the author's (config/RavenWatch/blueprints)
    [HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
    internal static class BlueprintHammerPatch
    {
        private static bool Prefix(Player __instance, Piece piece, ref bool __result)
        {
            if (piece == null || Perches.StoryAuthor || !Blueprint.Marked(piece.GetComponent<ZNetView>())) return true;
            __instance.Message(MessageHud.MessageType.Center, Lang.T("Цього не розібрати: воно належить цьому місцю."));
            __result = false;
            return false;
        }
    }
}
#endif
