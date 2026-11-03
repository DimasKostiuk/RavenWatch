#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // A ring of gold runes on the ground, seen only here (no network): the raven tree's arena (BindArena) and an errand
    // step's place (Want: the one ring of the running step, gone when no step asks for it).
    internal static class RuneRing
    {
        // a band of gold light round the ring at knee height (above the grass), and runes rising from it;
        // soft (an errand's place): a thinner, fainter band and fewer runes, a mark and not a wall
        internal static GameObject Build(string name, Vector3 at, float radius, bool soft = false)
        {
            GameObject ring = new GameObject(name);
            const int seg = 96;
            LineRenderer line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.loop = true; line.positionCount = seg;
            line.widthMultiplier = soft ? 0.08f : 0.14f;
            line.sharedMaterial = LineMat();
            line.startColor = line.endColor = new Color(1f, 0.8f, 0.35f, soft ? 0.4f : 0.9f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 pos = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                if (ZoneSystem.instance != null) pos.y = ZoneSystem.instance.GetGroundHeight(pos) + 0.6f;
                line.SetPosition(i, pos);
            }
            GameObject tpl = FxLib.Template(FxLib.OursPrefix + "runes");
            if (tpl == null) { RavenTree.Log("rune ring: no rune effect, the band of light only"); return ring; }
            int n = Mathf.Max(soft ? 5 : 8, Mathf.RoundToInt(radius * (soft ? 0.6f : 1.44f))); // (36 round the arena's 25 m)
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 pos = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                if (ZoneSystem.instance != null) pos.y = ZoneSystem.instance.GetGroundHeight(pos) + 0.6f;
                GameObject g = Object.Instantiate(tpl, pos, Quaternion.identity, ring.transform);
                g.hideFlags = HideFlags.None;
                g.transform.localScale = Vector3.one * 4f;
                foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule m = ps.main;
                    m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.35f), new Color(1f, 0.95f, 0.7f));
                    m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.16f);      // ×4: 0.4–0.65 m
                    m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
                    m.gravityModifier = -0.04f;                                   // rising up to ~2 m
                    ParticleSystem.EmissionModule e = ps.emission; e.rateOverTime = soft ? 2f : 4f;
                }
                g.SetActive(true);
            }
            return ring;
        }

        private static Material _lineMat;
        private static Material LineMat()
        {
            if (_lineMat != null) return _lineMat;
            _lineMat = new Material(Shader.Find("Sprites/Default"));
            _lineMat.hideFlags = HideFlags.HideAndDontSave;
            return _lineMat;
        }

        // ---------------------------------------------------------------- the running errand step's ring
        private static GameObject _step;
        private static Vector3 _at;
        private static float _radius;
        private static bool _wanted;

        // a step wants its ring here (every tick while it does; the ground is read once, when it is made)
        internal static void Want(Vector3 at, float radius)
        {
            _wanted = true;
            if (_step != null && (at - _at).sqrMagnitude < 0.01f && Mathf.Approximately(radius, _radius)) return;
            if (!Loaded(at)) return; // (its ground read once: not from the world as made, before the zone is here)
            if (_step != null) Object.Destroy(_step);
            _at = at; _radius = radius;
            _step = Build("RavenWatch_ErrandRing", at, radius, true);
        }

        private static bool Loaded(Vector3 at) { return ZoneSystem.instance != null && ZoneSystem.instance.IsZoneLoaded(at); }

        // after the steps' tick (Errands.Tick): a ring (and a stake) no step asked for is gone
        internal static void Tick()
        {
            if (!_wanted && _step != null) { Object.Destroy(_step); _step = null; }
            if (!_stakeWanted && _stake != null) { Object.Destroy(_stake); _stake = null; }
            if (!_propWanted && _prop != null) { Object.Destroy(_prop); _prop = null; }
            _wanted = _stakeWanted = _propWanted = false;
        }

        // ---------------------------------------------------------------- the rune stake in its middle (HoldStep)
        // a dark pole with runes over it; E on it is the step's deed (Errands.Act), as the J circle and the book
        private static GameObject _stake;
        private static Vector3 _stakeAt;
        private static bool _stakeWanted;

        internal static void WantStake(Vector3 at)
        {
            _stakeWanted = true;
            if (_stake != null && (at - _stakeAt).sqrMagnitude < 0.01f) return;
            if (!Loaded(at)) return;
            if (_stake != null) Object.Destroy(_stake);
            _stakeAt = at;
            _stake = Stake(at);
        }

        // ---------------------------------------------------------------- a step's thing at its place (ErrandTrail)
        // the look of a game thing (a cold fire pit, a chest…) made here only: built switched off, so nothing in the copy
        // wakes up (no network object), stripped to its looks, then a collider and E (the step's deed) on it, runes over it
        private static GameObject _prop;
        private static string _propKey;
        private static bool _propWanted;

        internal static void WantProp(Vector3 at, string key, string[] prefabs, string name, float scale)
        {
            _propWanted = true;
            if (_prop != null && _propKey == key) return;
            if (!Loaded(at)) return;
            if (_prop != null) Object.Destroy(_prop);
            _propKey = key;
            _prop = Prop(at, prefabs, name, scale);
        }

        private static GameObject Prop(Vector3 at, string[] prefabs, string name, float scale)
        {
            if (ZoneSystem.instance != null) at.y = ZoneSystem.instance.GetSolidHeight(at);
            GameObject src = null;
            foreach (string n in prefabs)
            {
                src = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(n) : null;
                if (src == null && ObjectDB.instance != null) src = ObjectDB.instance.GetItemPrefab(n);
                if (src != null) break;
            }
            if (src == null)
            {
                Errands.Log("errand prop: none of " + string.Join(",", prefabs) + " in this game — the rune stake instead");
                GameObject stake = Stake(at);
                stake.GetComponent<ErrandStake>().Name = name;
                return stake;
            }
            GameObject root = new GameObject("RavenWatch_ErrandProp");
            root.SetActive(false);
            root.transform.position = at;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject copy = Object.Instantiate(src, root.transform, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = src.transform.localScale * scale;
            foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Object.DestroyImmediate(mb);
            foreach (ParticleSystem x in copy.GetComponentsInChildren<ParticleSystem>(true)) if (x != null) Object.DestroyImmediate(x.gameObject);
            foreach (Light x in copy.GetComponentsInChildren<Light>(true)) if (x != null) Object.DestroyImmediate(x);
            foreach (AudioSource x in copy.GetComponentsInChildren<AudioSource>(true)) if (x != null) Object.DestroyImmediate(x);
            foreach (Collider x in copy.GetComponentsInChildren<Collider>(true)) if (x != null) Object.DestroyImmediate(x);
            foreach (Rigidbody x in copy.GetComponentsInChildren<Rigidbody>(true)) if (x != null) Object.DestroyImmediate(x);
            float top = 1f;
            Renderer[] rs = copy.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0) { Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds); top = Mathf.Clamp(b.max.y - at.y, 0.3f, 3f); }
            root.layer = LayerMask.NameToLayer("piece");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, top * 0.5f, 0f); col.size = new Vector3(1.4f, top, 1.4f);
            root.AddComponent<ErrandStake>().Name = name;
            GameObject tpl = FxLib.Template(FxLib.OursPrefix + "runes");
            if (tpl != null)
            {
                GameObject g = Object.Instantiate(tpl, at + Vector3.up * (top + 0.3f), Quaternion.identity, root.transform);
                g.hideFlags = HideFlags.None;
                g.transform.localScale = Vector3.one * 2f;
                foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule m = ps.main;
                    m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.35f), new Color(1f, 0.95f, 0.7f));
                }
                g.SetActive(true);
            }
            root.SetActive(true);
            return root;
        }

        private static GameObject Stake(Vector3 at)
        {
            if (ZoneSystem.instance != null) at.y = ZoneSystem.instance.GetGroundHeight(at);
            GameObject root = new GameObject("RavenWatch_ErrandStake");
            root.transform.position = at;
            root.layer = LayerMask.NameToLayer("piece");
            GameObject src = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("darkwood_pole") ?? ZNetScene.instance.GetPrefab("wood_pole2") : null;
            MeshRenderer mr = src != null ? src.GetComponentInChildren<MeshRenderer>(true) : null; // (its first look only: no other detail levels on top of it)
            MeshFilter mf = mr != null ? mr.GetComponent<MeshFilter>() : null;
            float top = 2f;
            if (mf != null && mf.sharedMesh != null)
            {
                GameObject g = new GameObject("pole");
                g.transform.SetParent(root.transform, false);
                Vector3 scale = mr.transform.lossyScale;
                Bounds b = mf.sharedMesh.bounds;
                g.transform.localRotation = Quaternion.Inverse(src.transform.rotation) * mr.transform.rotation;
                g.transform.localScale = scale;
                g.transform.localPosition = new Vector3(0f, -b.min.y * scale.y, 0f); // (its foot on the ground)
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                g.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
                top = b.size.y * scale.y;
            }
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, top * 0.5f, 0f); col.height = top + 0.2f; col.radius = 0.3f;
            root.AddComponent<ErrandStake>();
            GameObject tpl = FxLib.Template(FxLib.OursPrefix + "runes");
            if (tpl != null)
            {
                GameObject g = Object.Instantiate(tpl, at + Vector3.up * (top + 0.2f), Quaternion.identity, root.transform);
                g.hideFlags = HideFlags.None;
                g.transform.localScale = Vector3.one * 2f;
                foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule m = ps.main;
                    m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.35f), new Color(1f, 0.95f, 0.7f));
                }
                g.SetActive(true);
            }
            return root;
        }
    }

    internal sealed class ErrandStake : MonoBehaviour, Hoverable, Interactable
    {
        internal string Name; // (a prop of a step: its own name; null: the rune stake)
        public string GetHoverName() { return Name ?? Lang.T("Рунний кілок"); }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            Player p = Player.m_localPlayer;
            string why = null, act = p != null ? Errands.ActLabel(p, out why) : "";
            string s = GetHoverName() + (act.Length == 0 ? "" : why == null
                ? "\n[<color=yellow><b>$KEY_Use</b></color>] " + act
                : "\n<color=#9A9A9A>" + Lang.F("{0} — лише {1}", act, why) + "</color>");
            return Localization.instance != null ? Localization.instance.Localize(s) : s;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (hold || p == null || p != Player.m_localPlayer) return false;
            Errands.Act(p);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
#endif
