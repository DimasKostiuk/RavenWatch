#if RAVEN_ITEM && RAVEN_NPC
// (the pieces of a place of the story, for its parts and the story's scripts: from 2.9, with Blueprint)
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Every piece a blueprint puts into the world carries, besides Blueprint's mark, which building it is of («zala»)
    // and where in it it stands (its place in the blueprint's own frame — the same in every world). Names live in the
    // mod, never in the world (the user 2026-11-01: names given later must never break the worlds already built):
    //   assets/story/blueprints/<building>.names.txt (built in; the author's machine reads and writes that same file: Blueprint.Data,
    //   `raven blueprint name <name>`), a line each: «name prefab x y z». One name may have several
    //   lines (the same piece where an older and a newer blueprint put it): any of them found will do.
    // The story's scripts:
    //   LocParts.All("zala")            — the loaded pieces of the Hall (only round players: the rest is not loaded)
    //   LocParts.Named("zala", "vkhid") — the piece(s) of that name
    // and change them through their ZDO (seen by everyone, kept with the world).
    // Layers (LocState) are kept the same way, in <building>.layers.txt: «layer prefab x y z», one layer a piece
    // (`raven blueprint layer <layer>`).
    internal static class LocParts
    {
        internal const string KOf = "RavenWatch_bpOf", KAt = "RavenWatch_bpAt";
        private const float Near = 0.05f; // m: the same place (the blueprint keeps millimetres)

        internal static void Stamp(ZDO z, string of, Vector3 local)
        {
            z.Set(KOf, of);
            z.Set(KAt, local);
        }

        // the world's own pieces of it (a blueprint stamped them): the world's state is laid on these only — never on the
        // author's hand-built one, whose look is his own (LocPreview) and whose strength must not be written to (Heal)
        // (the same list for every caller of one look — LocState once a second, its fires, the fog: none of them keeps or changes it)
        internal static List<ZNetView> All(string of)
        {
            Look k = Loaded(of);
            if (k.All == null) k.All = Find(of, null, true);
            else k.All.RemoveAll(nv => nv == null || !nv.IsValid()); // (gone since the look)
            return k.All;
        }

        internal static List<ZNetView> Named(string of, string name)
        {
            Look k = Loaded(of);
            List<ZNetView> l;
            if (!k.Named.TryGetValue(name, out l)) // (found once per look: the camera asks each frame)
            {
                List<Entry> want = new List<Entry>();
                foreach (Entry e in Table(of, Names)) if (e.Name == name) want.Add(e);
                l = want.Count == 0 ? new List<ZNetView>() : Find(of, want, false); // (the story's names: the author's own building too)
                k.Named[name] = l;
            }
            l.RemoveAll(nv => nv == null || !nv.IsValid()); // (gone since the look: the same list kept, nothing new made per ask)
            return l;
        }

        // where a name points in the building's own frame (its first line: a prefab and its place), false — no such name
        internal static bool NameSpot(string of, string name, out string prefab, out Vector3 local)
        {
            foreach (Entry e in Table(of, Names)) if (e.Name == name) { prefab = e.Prefab; local = e.At; return true; }
            prefab = ""; local = Vector3.zero;
            return false;
        }

        // the name the author gave a piece of the building («» — none)
        internal static string NameOf(string of, ZNetView nv)
        {
            string b; Vector3 at;
            if (nv == null || !nv.IsValid() || !Place(nv, out b, out at) || b != of) return "";
            string prefab = Utils.GetPrefabName(nv.gameObject);
            return At(of, Names, prefab, at);
        }

        // the layer of a piece of the building («» — none: there in every state)
        internal static string LayerOf(string of, ZNetView nv)
        {
            string b; Vector3 at;
            if (nv == null || !nv.IsValid() || !Place(nv, out b, out at) || b != of) return "";
            return LayerAt(of, Utils.GetPrefabName(nv.gameObject), at);
        }

        internal static string LayerAt(string of, string prefab, Vector3 at)
        {
            return At(of, Layers, prefab, at);
        }

        // a line of a table by its prefab and place (asked for each piece laid: a place has thousands, its table hundreds)
        private static readonly Dictionary<List<Entry>, Dictionary<string, List<Entry>>> _byPrefab = new Dictionary<List<Entry>, Dictionary<string, List<Entry>>>();
        private static string At(string of, string kind, string prefab, Vector3 at)
        {
            List<Entry> t = Table(of, kind);
            Dictionary<string, List<Entry>> by;
            if (!_byPrefab.TryGetValue(t, out by))
            {
                if (_byPrefab.Count > 64) _byPrefab.Clear(); // (tables read anew by the author: the old ones let go)
                _byPrefab[t] = by = new Dictionary<string, List<Entry>>();
                foreach (Entry e in t) { List<Entry> l; if (!by.TryGetValue(e.Prefab, out l)) by[e.Prefab] = l = new List<Entry>(); l.Add(e); }
            }
            List<Entry> same;
            if (by.TryGetValue(prefab, out same)) foreach (Entry e in same) if ((e.At - at).sqrMagnitude < Near * Near) return e.Name;
            return "";
        }

        // the files read anew, by whoever looks next (StoryFiles.ReloadAll)
        internal static void Reread() { _tables.Clear(); _byPrefab.Clear(); Version++; }

        internal static int CountIf(string of, System.Func<string, bool> which)
        {
            int n = 0;
            foreach (Entry e in Table(of, Layers)) if (which(e.Name)) n++;
            return n;
        }

        // a piece's building and its place in it: its stamp (a blueprint put it there), else — in the author's world —
        // by the frame of the Hall he builds by hand (BlueprintFrame), within AuthorReach m of its middle
        internal const float AuthorReach = 60f;
        internal static bool Place(ZNetView nv, out string of, out Vector3 at)
        {
            ZDO z = nv.GetZDO();
            of = z.GetString(KOf, ""); at = z.GetVec3(KAt, Vector3.zero);
            if (of.Length > 0) return true;
            Vector3 c; Quaternion rot;
            if (!AuthorFrame(nv.transform.position, out of, out c, out rot)) return false;
            at = BlueprintFrame.Local(c, rot, nv.transform.position);
            return true;
        }

        // the author's own place a point is in: the nearest frame of the story's places in this world, within AuthorReach
        internal static bool AuthorFrame(Vector3 p, out string of, out Vector3 c, out Quaternion rot)
        {
            of = ""; c = Vector3.zero; rot = Quaternion.identity;
            float best = AuthorReach;
            foreach (Loc l in Locs.All)
            {
                Vector3 lc; Quaternion lr;
                if (!BlueprintFrame.Get(l.Id, out lc, out lr)) continue;
                float d = (p - lc).magnitude;
                if (d > best) continue;
                best = d; of = l.Id; c = lc; rot = lr;
            }
            return of.Length > 0;
        }

        // ---------------------------------------------------------------- the names
        private struct Entry { internal string Name, Prefab; internal Vector3 At; }
        private const string Names = "names", Layers = "layers";
        private static readonly Dictionary<string, List<Entry>> _tables = new Dictionary<string, List<Entry>>();

        private static string AuthorFile(string of, string kind) { return Path.Combine(Blueprint.Data, of + "." + kind + ".txt"); }

        // the author's own file when he has one (the whole truth: the mod's copy is made from it — a line he took off is
        // gone, not found again in the copy); else the mod's
        private static List<Entry> Table(string of, string kind)
        {
            List<Entry> t;
            if (_tables.TryGetValue(of + "." + kind, out t)) return t;
            t = new List<Entry>();
            if (Blueprint.On && File.Exists(AuthorFile(of, kind))) Read(File.ReadAllLines(AuthorFile(of, kind), Encoding.UTF8), t);
            else
                using (Stream st = typeof(LocParts).Assembly.GetManifestResourceStream("RavenWatch.blueprints." + of + "." + kind + ".txt"))
                    if (st != null) using (StreamReader r = new StreamReader(st, Encoding.UTF8)) Read(r.ReadToEnd().Replace("\r", "").Split('\n'), t);
            _tables[of + "." + kind] = t;
            return t;
        }

        internal static int Version { get; private set; } // goes up on every change of the author's files (caches look again)

        // the building piece under the crosshair, far too (the game's own hover reaches only a few metres: a roof beam
        // is out of it)
        internal static ZNetView Aimed(Player p)
        {
            if (p == null || GameCamera.instance == null) return null;
            Transform cam = GameCamera.instance.transform;
            RaycastHit hit;
            if (!Physics.Raycast(cam.position, cam.forward, out hit, 60f, AimMask)) return null;
            Piece pc = hit.collider != null ? hit.collider.GetComponentInParent<Piece>() : null;
            ZNetView nv = pc != null ? pc.GetComponent<ZNetView>() : null;
            return nv != null && nv.IsValid() ? nv : null;
        }
        private static readonly int AimMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle");

        private static void Read(string[] lines, List<Entry> into)
        {
            foreach (string raw in lines)
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                string[] w = l.Split(' ');
                float x, y, z;
                if (w.Length < 5 || !TryF(w[2], out x) || !TryF(w[3], out y) || !TryF(w[4], out z)) continue;
                into.Add(new Entry { Name = w[0], Prefab = w[1], At = new Vector3(x, y, z) });
            }
        }

        // the author names the piece he looks at («-»: the names of this piece taken off). Into his names file
        internal static string Name(ZNetView nv, string name)
        {
            if (name != "-" && !Blueprint.GoodName(name)) return "Ім'я елемента: латиниця, цифри, _ чи - (наприклад vkhid_left).";
            string done = Write(nv, name, Names);
            if (done == null) return Bad(nv);
            return name == "-" ? "Ім'я з «" + done + "» знято." : "«" + done + "» тепер зветься «" + name + "».";
        }

        // the author puts the piece he looks at into a layer of the Hall's states («-»: into none); a layer is its
        // states' part and its sub-states' marks after commas (LocState: «ruin,-1.1», «+1.1»)
        internal static string Layer(ZNetView nv, string layer)
        {
            if (string.IsNullOrEmpty(layer)) layer = "-";
            if (layer != "-" && !LocState.GoodLayer(layer)) return "Шар: " + string.Join(", ", LocState.Layers) + ", only<N>, from<N>, gone (прибрана з креслення: у вже збудованих світах ховається), позначки субстанів +1.1 / -1.1 через кому, чи - (без шару).";
            string done = Write(nv, layer, Layers);
            if (done == null) return Bad(nv);
            LocState.Again();
            string of; Vector3 at;
            Loc l = Place(nv, out of, out at) ? Locs.Get(of) : null;
            if (l != null) l.Log("layer of " + done + " at " + F(at.x) + " " + F(at.y) + " " + F(at.z) + " → " + (layer == "-" ? "none" : layer)); // (the author's every change: what went wrong found later)
            return layer == "-" ? "«" + done + "» тепер без шару: є в усіх станах." : "«" + done + "» тепер: " + LocState.LayerWhen(layer, l) + ".";
        }

        private static string Bad(ZNetView nv)
        {
            if (nv == null || !nv.IsValid()) return "Подивись на будівельний елемент (щоб його було підсвічено) і повтори.";
            return "Це не частина Зали: ні поставленої кресленням, ні твоєї (у її рамці, до " + Mathf.RoundToInt(AuthorReach) + " м від середини; рамка — raven blueprint frame zala).";
        }

        // this piece's one line in the author's file of that kind («-»: its line taken off); the piece's prefab, or null
        private static string Write(ZNetView nv, string name, string kind)
        {
            string of; Vector3 at;
            if (nv == null || !nv.IsValid() || !Place(nv, out of, out at)) return null;
            string prefab = Utils.GetPrefabName(nv.gameObject);
            string file = AuthorFile(of, kind);
            List<string> keep = new List<string>();
            if (!File.Exists(file)) // (no file of his yet: the built-in one first, or one line would become the whole truth — Table — and the rest be lost)
                using (Stream st = typeof(LocParts).Assembly.GetManifestResourceStream("RavenWatch.blueprints." + of + "." + kind + ".txt"))
                    if (st != null) { Directory.CreateDirectory(Blueprint.Data); using (StreamReader r = new StreamReader(st, Encoding.UTF8)) File.WriteAllText(file, r.ReadToEnd(), new UTF8Encoding(false)); }
            if (File.Exists(file))
                foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
                {
                    List<Entry> one = new List<Entry>();
                    Read(new[] { raw }, one);
                    if (one.Count == 1 && one[0].Prefab == prefab && (one[0].At - at).sqrMagnitude < Near * Near) continue; // (this piece's old line)
                    keep.Add(raw);
                }
            if (name != "-") keep.Add(name + " " + prefab + " " + F(at.x) + " " + F(at.y) + " " + F(at.z));
            File.WriteAllLines(file, keep.ToArray(), new UTF8Encoding(false));
            _tables.Remove(of + "." + kind);
            Version++;
            return prefab;
        }

        // ---------------------------------------------------------------- the author's own turn and lift
        // What the game's hammer cannot: a piece of the author's Hall tipped on any side (a table knocked over), lifted
        // or lowered, or set level again. Its transform and its ZDO (kept with the world, so in the next save — the
        // blueprint keeps a whole turn); its lines in the names and layers files follow it when it moves. A placed
        // copy's pieces are not turned: they come from the blueprint. null — done; else why not
        internal static string Nudge(ZNetView nv, Quaternion turn, Vector3 shift, bool level)
        {
            if (nv == null || !nv.IsValid()) return "Приціл не на деталі.";
            if (nv.GetZDO().GetString(KOf, "").Length > 0) return "Це деталь поставленої кресленням копії — крути деталі своєї Зали (еталону).";
            if (!nv.IsOwner()) nv.ClaimOwnership();
            Transform t = nv.transform;
            Vector3 was = t.position, now = was + shift;
            Quaternion r = level ? Quaternion.Euler(0f, t.rotation.eulerAngles.y, 0f) : turn * t.rotation;
            t.SetPositionAndRotation(now, r);
            ZDO z = nv.GetZDO();
            z.SetPosition(now);
            z.SetRotation(r);
            if (z.GetInt(Blueprint.Mark) != 1) { z.Set(Blueprint.Mark, 1); BlueprintWearPatch.Forget(nv.gameObject); } // (tipped, it may stand on nothing: kept from falling like a blueprint's piece)
            if (shift != Vector3.zero) Moved(nv, was, now);
            return null;
        }

        private static void Moved(ZNetView nv, Vector3 fromWorld, Vector3 toWorld)
        {
            Vector3 c; Quaternion rot; string of;
            if (!AuthorFrame(fromWorld, out of, out c, out rot)) return;
            Vector3 from = BlueprintFrame.Local(c, rot, fromWorld), to = BlueprintFrame.Local(c, rot, toWorld);
            string prefab = Utils.GetPrefabName(nv.gameObject);
            foreach (string kind in new[] { Names, Layers })
            {
                string file = AuthorFile(of, kind);
                if (!File.Exists(file)) continue;
                string[] lines = File.ReadAllLines(file, Encoding.UTF8);
                bool changed = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    List<Entry> one = new List<Entry>();
                    Read(new[] { lines[i] }, one);
                    if (one.Count != 1 || one[0].Prefab != prefab || (one[0].At - from).sqrMagnitude >= Near * Near) continue;
                    lines[i] = one[0].Name + " " + prefab + " " + F(to.x) + " " + F(to.y) + " " + F(to.z);
                    changed = true;
                }
                if (!changed) continue;
                File.WriteAllLines(file, lines, new UTF8Encoding(false));
                _tables.Remove(of + "." + kind);
                Version++;
            }
        }

        // ---------------------------------------------------------------- the loaded pieces of a building
        // Looked over once every LookAgain s per building (the user 2026-11-02: one look shared — the Hall's states, its
        // mist, the story's scenes and marks all ask; the game's piece list holds every loaded piece of the world), sooner
        // (MinAgain) when the game's pieces came or went (its own list of every loaded piece: not every object of the world — drops,
        // effects and people change that every frame), and at once when the author's files change (Version). Its pieces by its own stamp or — the author's world —
        // by his building's frame (Place: one rule with Loc.Anchor); round where it stands (Loc.Anchor). A piece gone
        // since is left out when asked. Named ones found from that same look.
        // Measured 2026-11-11 (raven npc perf, 1115 s by a base): 1.2 looks a second, 2.1 ms each — 2.5 ms of every second, the second dearest thing
        // the people's side did, though nothing had changed. A place whose spot is known (Fixed) is now looked over only when a piece came or went
        // within its reach (PieceChanged: the game's Piece.Awake / OnDestroy, the patches NpcBase already has), MinAgain s apart at most, and once
        // in SafeAgain s all the same. A place with no spot yet (looked for round the player), and the author's game (his frame may move, his
        // files change), keep the look by the clock as before.
        internal static float LookAgain = 2f, MinAgain = 0.25f, SafeAgain = 30f; // (the longest a look by the clock is kept; the soonest again when pieces came or went; the longest a look by pieces is kept)
        private sealed class Look { public bool Fixed, Dirty; public Vector3 Centre; public float Reach; public float At = -100f; public int Ver = -1, Loaded = -1; public object World; public List<ZNetView> All; public readonly List<ZNetView> Pieces = new List<ZNetView>(); public readonly List<Vector3> Spots = new List<Vector3>(); public readonly List<bool> Stamped = new List<bool>(); public readonly Dictionary<string, List<ZNetView>> Named = new Dictionary<string, List<ZNetView>>(); }
        private static readonly System.Reflection.FieldInfo AllPieces = AccessTools.Field(typeof(Piece), "s_allPieces");
        private static System.Collections.ICollection _allPieces;
        // how many pieces the game has loaded now (its own list); the game changed: every object it has instead, as before
        private static int PiecesLoaded()
        {
            if (_allPieces == null && AllPieces != null) _allPieces = AllPieces.GetValue(null) as System.Collections.ICollection;
            if (_allPieces != null) return _allPieces.Count;
            return ZNetScene.instance != null ? ZNetScene.instance.NrOfInstances() : 0;
        }
        private static readonly Dictionary<string, Look> _looks = new Dictionary<string, Look>();
        private static readonly List<Piece> _near = new List<Piece>();
        private static readonly int PerfLook = NpcPerf.Id("place pieces (деталі місця, огляд)");

        private static Look Loaded(string of)
        {
            Look k;
            if (!_looks.TryGetValue(of, out k)) _looks[of] = k = new Look();
            int loaded = PiecesLoaded(); // (a piece came or went: one of this place, maybe — hidden layers must not flash)
            float age = Time.time - k.At;
            bool byPieces = k.Fixed && !Blueprint.On;
            if (k.Ver == Version && ReferenceEquals(k.World, ZNet.instance)
                && (byPieces ? age < SafeAgain && (!k.Dirty || age < MinAgain) : age < LookAgain && (loaded == k.Loaded || age < MinAgain))) return k;
            long perf = NpcPerf.Now();
            k.At = Time.time; k.Ver = Version; k.World = ZNet.instance; k.Loaded = loaded; k.Dirty = false; k.Fixed = false;
            k.Pieces.Clear(); k.Spots.Clear(); k.Stamped.Clear(); k.Named.Clear(); k.All = null;
            Vector3 at; Quaternion rot;
            Loc l = Locs.Get(of);
            float reach = l != null && l.Shape() ? l.Reach() + 10f : 60f; // (no blueprint in this build: a ring as wide as the Hall's)
            if (l == null || !l.Anchor(out at, out rot)) { if (Player.m_localPlayer == null) return k; at = Player.m_localPlayer.transform.position; }
            else
            {
                k.Fixed = true; k.Centre = at; k.Reach = reach; // (it stands there: its pieces come and go round that spot only)
                if (BeyondLoaded(at, reach)) { NpcPerf.Add(PerfLook, perf); return k; }
            }
            _near.Clear();
            Piece.GetAllPiecesInRadius(at, reach, _near); // heavy: walks every loaded piece of the world; one look a place, every LookAgain s, every MinAgain s at most while pieces come and go, and only while the place can be loaded
            foreach (Piece pc in _near)
            {
                ZNetView nv = pc != null ? pc.GetComponent<ZNetView>() : null;
                string b; Vector3 local;
                if (nv == null || !nv.IsValid() || !Place(nv, out b, out local) || b != of) continue;
                k.Pieces.Add(nv); k.Spots.Add(local); k.Stamped.Add(nv.GetZDO().GetString(KOf, "").Length > 0);
            }
            NpcPerf.Add(PerfLook, perf);
            return k;
        }

        // a piece came into the world or left it here (every game: NpcBasePieceIn / NpcBasePieceOut): the places it may be of are looked over again
        // when next asked. Called in bursts as a zone loads: a look at the few places there are, nothing else
        internal static void PieceChanged(Vector3 at)
        {
            if (_looks.Count == 0) return;
            foreach (Look k in _looks.Values)
            {
                if (!k.Fixed || k.Dirty) continue;
                float dx = at.x - k.Centre.x, dz = at.z - k.Centre.z, far = k.Reach + 2f;
                if (dx * dx + dz * dz <= far * far) k.Dirty = true;
            }
        }

        // nothing of a place this far from this game's view can be loaded (the game's own ring, near + far, one zone to spare), so no piece to look for: measured 2026-11-06, 5.5 ms a look with every place 400-600 m away. A dedicated server asks always: it has no view of its own
        private static bool BeyondLoaded(Vector3 at, float reach)
        {
            if (ZNet.instance == null || ZoneSystem.instance == null || ZNet.instance.IsDedicated()) return false;
            Vector3 me = ZNet.instance.GetReferencePosition();
            float loaded = (ZNet.instance.GetSyncedSimulationDistance().TotalSimulationDistance + 1) * ZoneSystem.instance.m_zoneSize + reach;
            return Mathf.Abs(at.x - me.x) > loaded || Mathf.Abs(at.z - me.z) > loaded;
        }

        private static List<ZNetView> Find(string of, List<Entry> want, bool stampedOnly)
        {
            Look k = Loaded(of);
            List<ZNetView> list = new List<ZNetView>();
            for (int i = 0; i < k.Pieces.Count; i++)
            {
                ZNetView nv = k.Pieces[i];
                if (nv == null || !nv.IsValid()) continue; // (gone since the look)
                if (stampedOnly && !k.Stamped[i]) continue; // (the author's own building: not the world's)
                if (want != null && !Wanted(nv, k.Spots[i], want)) continue;
                list.Add(nv);
            }
            return list;
        }

        private static bool Wanted(ZNetView nv, Vector3 at, List<Entry> want)
        {
            string prefab = Utils.GetPrefabName(nv.gameObject);
            foreach (Entry e in want) if (e.Prefab == prefab && (e.At - at).sqrMagnitude < Near * Near) return true;
            return false;
        }

        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static bool TryF(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
    }

    // the Hall's lights burn without fuel (the user 2026-10-31): the game's own «infinite fuel» on each fire of a blueprint
    // (a piece just placed by Blueprint gets it there: its Awake ran before the mark), and on the author's own Hall too
    // (the user 2026-11-01: its candles went out while he built)
    [HarmonyPatch(typeof(Fireplace), "Awake")]
    internal static class LocLightPatch
    {
        private static void Postfix(Fireplace __instance)
        {
            try { Lay(__instance); }
            catch (System.Exception e) { PatchGuard.Fail("LocLightPatch", e); }
        }

        private static void Lay(Fireplace __instance)
        {
            WearNTear w = __instance.GetComponent<WearNTear>();
            ZNetView nv = __instance.GetComponent<ZNetView>();
            if (Blueprint.Marked(nv) || (w != null && BlueprintWearPatch.IsMarked(w))) __instance.m_infiniteFuel = true;
            // a fire of the Hall a blueprint put into the world: out from its first moment while the Hall is a ruin or it
            // is hidden (LocState.Tick says the same a moment later — no flash of light on the way in)
            Loc l = nv != null && nv.IsValid() ? Locs.Get(nv.GetZDO().GetString(LocParts.KOf, "")) : null;
            if (l == null) return;
            int state = LocState.StateOf(l);
            bool on = LocState.Shows(LocParts.LayerOf(l.Id, nv), state, LocState.SubOf(l));
            if (!on || (LocState.Ruined(l, state) && !LocState.LitInRuin(l, nv, state, LocState.SubOf(l)))) LocState.Unlit.Add(__instance.GetInstanceID());
        }
    }
}
#endif
