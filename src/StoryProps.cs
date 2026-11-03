#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The story's props (the user 2026-11-02): a thing taken up in a scene — the shield of Munin's place lifted off the
    // ground in two hands, later a charm, a feather, a key in Heid's hand. Never the thing itself: its look only (HandLook),
    // and the piece it came from hidden meanwhile. The look comes exactly where the piece lies, the same moment the piece
    // goes, and goes back there before the piece returns: nothing pops.
    //   state:  on the holder's own ZDO (a player's, or a person's of the story) — Key = «item|hand|piece|pose|until|size|rest|free»;
    //           written only by the owner (a person's through its act «prop»). Nothing else is sent: every game near
    //           reads it and draws it (a hand's look, the piece hidden, the pose), so one who comes in mid-scene sees it
    //           too, and a player who leaves takes it with him (his ZDO is gone).
    //   until:  world seconds (a person's: a crashed scene's prop does not stay in its hand for good); 0 — no end
    //   hand:   l | r (none — a shield on the left, as the game holds it; anything else on the right)
    //   piece:  the place's name of what it came from (prologue_munin.shchyt): each game finds it by that name, or «-»
    //   pose:   a player's clip while held (through Gestures: prop_look_<hand> — looks at it; «-» none)
    //   rest:   x,y,z,rx,ry,rz in the world: where it lay; «-» nowhere (it grows in the hand)
    //   free:   1 — the clip moves it (the channels @Prop, @LeftHand.on, @RightHand.on: BodyClips), both hands left empty;
    //           0 — on the hand's joint, as the game holds it
    // The scene puts every prop back when it ends or stops (StoryScenes), whatever its file said.
    internal static class StoryProps
    {
        internal static readonly int Key = "RavenWatch_prop".GetStableHashCode();
        internal static float PoseFade = 0.5f, Tick = 0.2f, NpcFor = 600f, Glide = 0.5f;

        // ---------------------------------------------------------------- the words of a step
        // take <item> [from <place.piece>] [left|right] [pose <clip>|still] [size <m>] [free]   (w from the word after «take»)
        internal sealed class Ask
        {
            public string Item = "", From = "", Hand = "", Pose = "";
            public float Size;
            public bool Free, Lasting, HasRest; // (Lasting, HasRest: the animation stage's own mannequin — no term; lies where it says)
            public Vector3 RestAt;
            public Quaternion RestTurn = Quaternion.identity;
        }

        internal static string Usage = "prop take <предмет> [from <місце.частина>] [left|right] [pose <кліп>|still] [size <м>] [free] · prop put";

        internal static Ask Read(string[] w, int at, out string why)
        {
            why = null;
            Ask a = new Ask();
            if (w.Length <= at) { why = Usage; return null; }
            a.Item = w[at];
            for (int i = at + 1; i < w.Length; i++)
            {
                string x = w[i];
                if (x == "left" || x == "right") a.Hand = x == "left" ? "l" : "r";
                else if (x == "still") a.Pose = "-";
                else if (x == "free") a.Free = true;
                else if ((x == "from" || x == "pose" || x == "size") && i + 1 < w.Length)
                {
                    string v = w[++i];
                    if (x == "from") a.From = v;
                    else if (x == "pose") a.Pose = v;
                    else if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out a.Size) || a.Size < 0f) { why = "size <метрів>"; return null; }
                }
                else { why = "prop: не розумію «" + x + "» (" + Usage + ")"; return null; }
            }
            return a;
        }

        // ---------------------------------------------------------------- taking, putting back
        // the code for a holder: null and why when the item is not in the game
        private static string Code(Ask a, bool person, out string why)
        {
            why = null;
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(a.Item) : null;
            ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (d == null) { why = "нема предмета «" + a.Item + "»"; return null; }
            string hand = a.Hand.Length > 0 ? a.Hand : d.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield ? "l" : "r";
            string piece = "-", rest = a.HasRest ? Rest(a.RestAt, a.RestTurn) : "-";
            if (a.From.Length > 0)
            {
                ZNetView nv = StoryWhere.Piece(a.From);
                if (nv != null && nv.IsValid()) { piece = a.From; rest = RestOf(nv); }
                else why = "«" + a.From + "» нема поруч — на землі лишиться"; // (still taken: only the ground keeps its own)
            }
            string pose = person || a.Free ? "-" : a.Pose.Length > 0 ? a.Pose : "prop_look_" + hand;
            if (pose != "-" && BodyClips.Find(pose) == null) { why = "нема пози «" + pose + "» — тримає без неї"; pose = "-"; }
            double until = person && !a.Lasting && ZNet.instance != null ? ZNet.instance.GetTimeSeconds() + NpcFor : 0.0;
            return a.Item + "|" + hand + "|" + piece + "|" + pose + "|" + until.ToString("0", CultureInfo.InvariantCulture) + "|" + a.Size.ToString("0.###", CultureInfo.InvariantCulture) + "|" + rest + "|" + (a.Free ? "1" : "0");
        }

        // where a piece's look lies: its meshes' middle, its turn (HandLook's copy is made centred, so it lands on it)
        private static string RestOf(ZNetView nv) { return Rest(Middle(nv), nv.transform.rotation); }

        // what the eye sees as its middle, not its object's root («player go» walks to this too, so a clip finds it where it expects)
        internal static Vector3 Middle(ZNetView nv)
        {
            Bounds b = new Bounds(); bool any = false;
            foreach (MeshRenderer r in nv.GetComponentsInChildren<MeshRenderer>()) // heavy: once per prop taken from a piece or walk to it
            {
                if (!r.enabled) continue;
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            }
            return any ? b.center : nv.transform.position;
        }

        private static string Rest(Vector3 at, Quaternion turn)
        {
            Vector3 e = turn.eulerAngles;
            float[] v = { at.x, at.y, at.z, e.x, e.y, e.z };
            string[] t = new string[6];
            for (int i = 0; i < 6; i++) t[i] = v[i].ToString("0.###", CultureInfo.InvariantCulture);
            return string.Join(",", t);
        }

        // the local player takes it (a scene step); a note when something was left out, else null
        internal static string Take(Player p, Ask a)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return "гравця нема";
            string why;
            string code = Code(a, false, out why);
            if (code == null) return why;
            Put(p); // (one thing a hand: the one before goes back first)
            z.Set(Key, code);
            Held h = Held.Of(code);
            if (h.Pose != "-") Gestures.Scene(p, h.Pose, PoseFade);
            if (h.Free && h.HasRest)
            {
                Vector3 before = p.transform.InverseTransformPoint(h.RestAt);
                Story.Log("prop " + h.Item + " lies at " + before.x.ToString("0.00") + " " + before.y.ToString("0.00") + " " + before.z.ToString("0.00") + " before him (x right, y up, z ahead), turned " + Quaternion.Angle(p.transform.rotation, h.RestTurn).ToString("0") + " deg from him");
            }
            return why;
        }

        internal static void Put(Player p)
        {
            ZDO z = NetFlag.Zdo(p, true);
            string code = z != null ? z.GetString(Key, "") : "";
            if (code.Length == 0) return;
            Held h = Held.Of(code);
            if (h.Pose != "-") Gestures.SceneEnd(p, PoseFade);
            z.Set(Key, "");
        }

        // a person of the story (its owner writes: its act «prop»)
        internal static string Take(NpcActor a, Ask ask)
        {
            string why;
            string code = Code(ask, true, out why);
            if (code == null) return why;
            a.Act("prop " + code);
            return why;
        }

        internal static void Put(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z != null && z.GetString(Key, "").Length > 0) a.Act("prop -");
        }

        internal static void Wire() { NpcActor.Acts["prop"] = (n, w) => { ZDO z = n.Zdo; if (z != null) z.Set(Key, w.Length > 1 && w[1] != "-" ? w[1] : ""); }; }

        // ---------------------------------------------------------------- what a code says
        internal sealed class Held
        {
            public string Item = "", Hand = "r", Piece = "-", Pose = "-"; public double Until; public float Size;
            public bool HasRest, Free; public Vector3 RestAt; public Quaternion RestTurn = Quaternion.identity;
            internal static Held Of(string code)
            {
                Held h = new Held();
                string[] f = (code ?? "").Split('|');
                if (f.Length > 0) h.Item = f[0];
                if (f.Length > 1) h.Hand = f[1];
                if (f.Length > 2) h.Piece = f[2];
                if (f.Length > 3) h.Pose = f[3];
                if (f.Length > 4) double.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out h.Until);
                if (f.Length > 5) float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out h.Size);
                if (f.Length > 6) h.ReadRest(f[6]);
                if (f.Length > 7) h.Free = f[7] == "1";
                return h;
            }
            private void ReadRest(string s)
            {
                string[] g = s.Split(',');
                if (g.Length < 6) return;
                float[] v = new float[6];
                for (int i = 0; i < 6; i++) if (!float.TryParse(g[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return;
                RestAt = new Vector3(v[0], v[1], v[2]);
                RestTurn = Quaternion.Euler(v[3], v[4], v[5]);
                HasRest = true;
            }
            internal bool Live { get { return Item.Length > 0 && (Until <= 0.0 || ZNet.instance == null || ZNet.instance.GetTimeSeconds() < Until); } }
        }

        // ---------------------------------------------------------------- every game: what it draws
        private sealed class Hidden { public GameObject Piece; public readonly List<Renderer> Looks = new List<Renderer>(); public readonly List<Collider> Touch = new List<Collider>(); }

        private static float _next;
        private static ZNet _world;
        private static readonly Dictionary<string, Hidden> _hidden = new Dictionary<string, Hidden>();
        private static readonly HashSet<string> _want = new HashSet<string>();

        // a few times a second (StoryQuests.Tick): the holders near, their hands, the pieces they took
        internal static void Look()
        {
            if (Time.time < _next || ZNet.instance == null) return;
            _next = Time.time + Tick;
            if (_world != ZNet.instance) { _world = ZNet.instance; Forget(); } // (another world: nothing of the last one kept)
            _want.Clear();
            foreach (Player o in Player.GetAllPlayers()) See(o);
            foreach (NpcActor a in NpcActor.Live) if (a != null) See(a.GetComponent<Character>());
            Pieces();
        }

        private static void See(Character c)
        {
            if (c == null) return;
            ZNetView nv = c.GetComponent<ZNetView>();
            string code = nv != null && nv.IsValid() ? nv.GetZDO().GetString(Key, "") : "";
            StoryPropView v = c.GetComponent<StoryPropView>();
            Held h = code.Length > 0 ? Held.Of(code) : null;
            if (h != null && !h.Live) h = null;
            if (h == null)
            {
                if (v == null) return;
                v.Set("");
                if (v.Back) v.Clear(); // (back where it lay: it goes the moment the piece returns, below)
                else if (v.Piece != null) _want.Add(v.Piece); // (still on its way back: the piece waits for it)
                return;
            }
            if (v == null) v = c.gameObject.AddComponent<StoryPropView>();
            v.Set(code);
            if (h.Piece != "-") _want.Add(h.Piece);
        }

        // the pieces held now: hidden (their look and their touch); the ones let go: back as they were
        private static void Pieces()
        {
            List<string> back = null;
            foreach (KeyValuePair<string, Hidden> kv in _hidden)
                if (!_want.Contains(kv.Key) || kv.Value.Piece == null) (back ?? (back = new List<string>())).Add(kv.Key); // (one unloaded meanwhile: found anew when it is back)
            if (back != null) foreach (string name in back) Show(name);
            foreach (string name in _want) if (!_hidden.ContainsKey(name)) Hide(name);
        }

        private static void Hide(string name)
        {
            ZNetView nv = StoryWhere.Piece(name);
            if (nv == null) return; // (not loaded here: asked again next look)
            Hidden h = new Hidden { Piece = nv.gameObject };
            foreach (Renderer r in nv.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; h.Looks.Add(r); } // heavy: once per prop taken from a piece
            foreach (Collider c in nv.GetComponentsInChildren<Collider>()) if (c.enabled) { c.enabled = false; h.Touch.Add(c); } // heavy: once per prop taken from a piece
            _hidden[name] = h;
        }

        private static void Show(string name)
        {
            Hidden h;
            if (!_hidden.TryGetValue(name, out h)) return;
            foreach (Renderer r in h.Looks) if (r != null) r.enabled = true;
            foreach (Collider c in h.Touch) if (c != null) c.enabled = true;
            _hidden.Remove(name);
        }

        private static void Forget() { _hidden.Clear(); _want.Clear(); }
    }

    // A holder's thing on this game: its look on the hand's joint or where the clip says, what the hands held before hidden
    // meanwhile (the game may put its weapon back there any moment: hidden again each frame while held).
    [DefaultExecutionOrder(11000)] // (after every pose, a person's too: NpcPose)
    internal sealed class StoryPropView : MonoBehaviour
    {
        private string _code = "";
        private StoryProps.Held _h;
        private GameObject _look;
        private Transform _joint;
        private BodyAnimator _anim;
        private Vector3 _inHand;
        private float _g; // (0: where it lay, or nothing yet · 1: taken)
        private Quaternion _asLay = Quaternion.identity; // (how it lay against the body as it took it: a clip reckons the thing as lying square before the body)
        private bool _leaving;
        private readonly List<GameObject> _hid = new List<GameObject>();

        // the piece it came from, while its look is out
        internal string Piece { get { return _look != null && _h != null && _h.Piece != "-" ? _h.Piece : null; } }
        // let go and arrived
        internal bool Back { get { return _leaving && _g <= 0f; } }
        // the size of its look in its own frame (the stage says it: the clip's points on it are reckoned from this)
        internal Vector3 Size { get { return _box.size; } }
        private Bounds _box;

        // how far a point is from the thing, m; below zero: inside it. Round when its two larger sides are alike (a shield),
        // else a box. For the stage's measure only: no one in play asks
        internal float Distance(Vector3 world)
        {
            if (_look == null) return 99f;
            Vector3 q = _look.transform.InverseTransformPoint(world) - _box.center, e = _box.extents;
            int thin = e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;
            float a = thin == 0 ? e.y : e.x, b = thin == 2 ? e.y : e.z, qa = thin == 0 ? q.y : q.x, qb = thin == 2 ? q.y : q.z;
            float out0 = Mathf.Abs(q[thin]) - e[thin];
            if (Mathf.Abs(a - b) < 0.1f * Mathf.Max(a, b))
            {
                float radial = Mathf.Sqrt(qa * qa + qb * qb) - Mathf.Max(a, b);
                return radial > 0f || out0 > 0f ? new Vector2(Mathf.Max(radial, 0f), Mathf.Max(out0, 0f)).magnitude : Mathf.Max(radial, out0);
            }
            Vector3 d = new Vector3(Mathf.Abs(q.x) - e.x, Mathf.Abs(q.y) - e.y, Mathf.Abs(q.z) - e.z);
            return d.x > 0f || d.y > 0f || d.z > 0f ? Vector3.Max(d, Vector3.zero).magnitude : Mathf.Max(d.x, Mathf.Max(d.y, d.z));
        }

        internal void Set(string code)
        {
            if (code == _code) return;
            if (code.Length == 0) { _code = ""; _leaving = _look != null; if (!_leaving) Clear(); return; }
            Clear();
            _code = code;
            _h = StoryProps.Held.Of(code);
            VisEquipment ve = GetComponent<VisEquipment>();
            if (ve == null) return;
            _joint = _h.Hand == "l" ? ve.m_leftHand : ve.m_rightHand;
            if (_joint == null && !_h.Free) return;
            _look = _h.Free || _h.Size > 0f ? HandLook.Copy(_h.Item, _h.Size, _h.Free) : HandLook.Make(_h.Item, _h.Size);
            if (_look == null) { Story.Log("prop: no look for " + _h.Item); return; }
            _box = Measure();
            if (_h.Free)
            {
                _look.transform.SetPositionAndRotation(_h.RestAt, _h.RestTurn);
                Humanoid body = GetComponent<Humanoid>();
                _anim = body != null ? BodyAnimator.For(body) : null;
                // from whatever side he came, the clip's frame of the thing is square to his body (the hands' points hold
                // on any side); its look keeps how it really lay, so it does not spin round in the hands
                Vector3 ahead = transform.forward; ahead.y = 0f;
                Quaternion square = ahead.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(ahead) : Quaternion.identity;
                _asLay = Quaternion.Inverse(square) * _h.RestTurn;
                if (_anim != null) _anim.PropRest(true, _h.RestAt, square);
            }
            else
            {
                _look.transform.SetParent(_joint, false);
                bool copy = _look.transform.childCount > 0 && _look.transform.GetChild(0).name == "look"; // (a copy, not the game's grip: a little into the palm)
                _inHand = copy ? new Vector3(0f, 0.02f, 0.03f) : Vector3.zero;
                _look.transform.localPosition = _inHand;
                _look.transform.localRotation = Quaternion.identity;
            }
            Place(0f);
            HideOthers();
        }

        // its meshes' corners in the look's own frame (a mesh's own axes may be turned: its thin side is not always its z)
        private Bounds Measure()
        {
            Bounds b = new Bounds(); bool any = false;
            foreach (MeshFilter f in _look.GetComponentsInChildren<MeshFilter>()) // heavy: once per look made
            {
                if (f.sharedMesh == null) continue;
                Bounds m = f.sharedMesh.bounds;
                Vector3[] points = f.sharedMesh.isReadable ? f.sharedMesh.vertices : new Vector3[8]; // (its own points: the corners of its box overstate a turned mesh, a shield by half)
                if (!f.sharedMesh.isReadable) for (int i = 0; i < 8; i++) points[i] = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                foreach (Vector3 c in points)
                {
                    Vector3 p = _look.transform.InverseTransformPoint(f.transform.TransformPoint(c));
                    if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
                }
            }
            return b;
        }

        private void LateUpdate()
        {
            if (_look == null) return;
            _g = Mathf.MoveTowards(_g, _leaving ? 0f : 1f, Time.deltaTime / StoryProps.Glide);
            Place(_g * _g * (3f - 2f * _g));
            HideOthers();
        }

        // from where it lay to where it is meant to be (nothing lay anywhere: it grows there)
        private void Place(float k)
        {
            Transform t = _look.transform;
            if (_h.Free)
            {
                Vector3 at = _h.RestAt; Quaternion turn = _h.RestTurn;
                if (_anim != null) { _anim.PropAt(out at, out turn); turn = turn * _asLay; }
                t.SetPositionAndRotation(Vector3.Lerp(_h.RestAt, at, k), Quaternion.Slerp(_h.RestTurn, turn, k));
            }
            else if (_h.HasRest) t.SetPositionAndRotation(Vector3.Lerp(_h.RestAt, _joint.TransformPoint(_inHand), k), Quaternion.Slerp(_h.RestTurn, _joint.rotation, k));
            else t.localScale = Vector3.one * Mathf.Max(0.01f, k);
        }

        private void HideOthers()
        {
            VisEquipment ve = _h.Free ? GetComponent<VisEquipment>() : null;
            if (ve != null) { Empty(ve.m_leftHand); Empty(ve.m_rightHand); } // (a clip takes it in both hands)
            else Empty(_joint);
        }

        private void Empty(Transform joint)
        {
            if (joint == null) return;
            for (int i = 0; i < joint.childCount; i++)
            {
                GameObject g = joint.GetChild(i).gameObject;
                if (g == _look || !g.activeSelf) continue;
                g.SetActive(false);
                _hid.Add(g);
            }
        }

        internal void Clear()
        {
            if (_look != null) Destroy(_look);
            _look = null;
            foreach (GameObject g in _hid) if (g != null) g.SetActive(true);
            _hid.Clear();
            _joint = null;
            if (_anim != null) _anim.PropRest(false, Vector3.zero, Quaternion.identity);
            _anim = null;
            _h = null; _g = 0f; _leaving = false; _code = "";
        }

        private void OnDestroy() { Clear(); }
    }
}
#endif
