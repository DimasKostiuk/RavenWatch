#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // The look tab's picture (StoryPanel «Вигляд»; the user 2026-11-01: «перегляд того, кого правиш» — not the NPC in the
    // world, where anything stands between): a mannequin of one template on a stage of its own far over the world —
    // the people's own body (the game's way for its character preview: no network object, VisEquipment dressing it from
    // its own fields), only its looks left on it, on a layer nobody else draws, its own camera and lamps.
    // Dressed from the template every frame it is drawn (the game's setters change nothing when nothing changed), turned
    // round (Yaw, a drag on the picture), framed from the whole body to the face (Shot). Filmed only while the tab asks
    // (Want); gone a few seconds after.
    internal sealed class NpcPreview : MonoBehaviour
    {
        internal const int W = 360, H = 480;
        internal static float Yaw = 0f;   // 0 — its face to the camera
        internal static int Shot;         // 0 the whole body · 1 to the waist · 2 the face
        internal static readonly string[] Shots = { "Усе тіло", "Пояс", "Обличчя" };
        internal static string Problem = "";
        private static readonly Vector3 Stage = new Vector3(200f, 9300f, 0f); // (away from the Workshop's stage at x 0)
        private static readonly Color Back = new Color(0.16f, 0.17f, 0.19f, 1f);

        private static NpcPreview _inst;
        private static NpcDef _def;
        private static float _wanted = -100f;
        private Camera _cam;
        private RenderTexture _rt;
        private GameObject _body;
        private VisEquipment _vis;
        private int _layer = -1;

        internal static Texture Picture { get { return _inst != null ? (Texture)_inst._rt : null; } }

        // the tab draws: film this template now
        internal static void Want(NpcDef d)
        {
            _def = d;
            _wanted = Time.unscaledTime;
            if (_inst == null) { GameObject g = new GameObject("RavenWatch_NpcPreview"); DontDestroyOnLoad(g); _inst = g.AddComponent<NpcPreview>(); }
        }

        private void Awake()
        {
            transform.position = Stage;
            _layer = RavenFx.FreeLayer();
            if (_layer < 0) { Problem = "Немає вільного шару для картинки."; return; }
            Problem = "";
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); _rt.antiAliasing = 2; _rt.Create();
            _cam = new GameObject("cam").AddComponent<Camera>();
            _cam.transform.SetParent(transform, false);
            _cam.enabled = false; // (filmed by hand, only when asked)
            _cam.targetTexture = _rt;
            _cam.cullingMask = 1 << _layer; _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = Back;
            _cam.fieldOfView = 30f; _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 50f;
            _cam.renderingPath = RenderingPath.Forward; _cam.allowHDR = false; _cam.allowMSAA = false;
            Lamp(1.1f, Color.white, Quaternion.Euler(35f, 160f, 0f));
            Lamp(0.55f, new Color(0.8f, 0.85f, 1f), Quaternion.Euler(20f, -30f, 0f));
            Lamp(0.45f, Color.white, Quaternion.Euler(-20f, 0f, 0f));
            MakeBody();
        }

        private void Lamp(float power, Color c, Quaternion turn)
        {
            Light l = new GameObject("lamp").AddComponent<Light>();
            l.transform.SetParent(transform, false); l.transform.rotation = turn;
            l.type = LightType.Directional; l.intensity = power; l.color = c; l.cullingMask = 1 << _layer; l.shadows = LightShadows.None;
        }

        // the people's body with only its looks left: made asleep (nothing of it wakes before it is stripped), woken with
        // no network object (the game's own switch for its character preview)
        private void MakeBody()
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Npcs.HumanPrefab) : null;
            if (prefab == null) { Problem = "Тіла NPC ще нема (світ не завантажений)."; return; }
            GameObject holder = new GameObject("body");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            _body = Instantiate(prefab, holder.transform, false);
            _body.transform.localPosition = Vector3.zero; _body.transform.localRotation = Quaternion.identity;
            for (int pass = 0; pass < 3; pass++) // (one needed by another goes on the next pass, once that one is off)
                foreach (MonoBehaviour mb in _body.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb is VisEquipment || mb is ZNetView) continue;
                    string ns = mb.GetType().Namespace ?? "";
                    if (ns.StartsWith("MagicaCloth")) continue; // (the capes' cloth: kept, it moves them)
                    try { DestroyImmediate(mb); } catch { }
                }
            foreach (Collider c in _body.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Rigidbody r in _body.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
            foreach (Animator a in _body.GetComponentsInChildren<Animator>(true)) a.fireEvents = false; // (its clips call the ones taken off)
            ZNetView.m_forceDisableInit = true;
            try { holder.SetActive(true); }
            finally { ZNetView.m_forceDisableInit = false; }
            _vis = _body.GetComponent<VisEquipment>();
            if (_vis == null) Problem = "У тіла NPC нема VisEquipment.";
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime - _wanted > 5f) { Destroy(gameObject); return; } // (the tab long closed: gone)
            if (Time.unscaledTime - _wanted > 0.3f || _def == null || _vis == null || _cam == null) return; // (not drawn just now: not filmed)
            Dress(_def);
            Layer(_body.transform); // (what it was dressed in just now is made on the game's layers: onto ours)
            _body.transform.localRotation = Quaternion.Euler(0f, 180f + Yaw, 0f); // (its face towards the camera at 0)
            Bounds b = BodyBounds();
            float h = Mathf.Max(1f, b.size.y);
            Vector3 aim = Shot == 0 ? b.center : Shot == 1 ? new Vector3(b.center.x, b.max.y - h * 0.3f, b.center.z) : new Vector3(b.center.x, b.max.y - h * 0.09f, b.center.z);
            float tall = Shot == 0 ? h * 1.15f : Shot == 1 ? h * 0.6f : h * 0.22f;
            float dist = tall * 0.5f / Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _cam.transform.position = aim + Vector3.back * dist + Vector3.up * (Shot == 2 ? 0.02f : h * 0.05f);
            _cam.transform.rotation = Quaternion.LookRotation(aim - _cam.transform.position, Vector3.up);
            _cam.Render();
        }

        // the template on the mannequin (the game's setters: a change only where something changed)
        private void Dress(NpcDef d)
        {
            if (d.Odin) return;
            _vis.SetModel(d.Model);
            _vis.SetSkinColor(d.Skin);
            _vis.SetHairColor(d.HairColor);
            _vis.SetHairItem(Hash(d.Hair));
            _vis.SetBeardItem(d.Model == 0 ? Hash(d.Beard) : 0);
            int chest = 0, legs = 0, helmet = 0, shoulder = 0, utility = 0, right = 0, left = 0;
            foreach (string g in d.Gear)
            {
                GameObject pf = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(g) : null;
                ItemDrop id = pf != null ? pf.GetComponent<ItemDrop>() : null;
                if (id == null) continue;
                int h = g.GetStableHashCode();
                switch (id.m_itemData.m_shared.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.Chest: chest = h; break;
                    case ItemDrop.ItemData.ItemType.Legs: legs = h; break;
                    case ItemDrop.ItemData.ItemType.Helmet: helmet = h; break;
                    case ItemDrop.ItemData.ItemType.Shoulder: shoulder = h; break;
                    case ItemDrop.ItemData.ItemType.Utility: utility = h; break;
                    case ItemDrop.ItemData.ItemType.Shield:
                    case ItemDrop.ItemData.ItemType.Bow: left = h; break;
                    case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                    case ItemDrop.ItemData.ItemType.Tool:
                    case ItemDrop.ItemData.ItemType.Torch: right = h; break;
                }
            }
            _vis.SetChestItem(chest); _vis.SetLegItem(legs); _vis.SetHelmetItem(helmet);
            _vis.SetShoulderItem(shoulder, 0, 0); _vis.SetUtilityItem(utility);
            _vis.SetRightItem(right, 0); _vis.SetLeftItem(left, 0, 0);
        }

        private static int Hash(string item) { return string.IsNullOrEmpty(item) || ObjectDB.instance == null || ObjectDB.instance.GetItemPrefab(item) == null ? 0 : item.GetStableHashCode(); }

        private void Layer(Transform t)
        {
            if (t.gameObject.layer != _layer) t.gameObject.layer = _layer;
            for (int i = 0; i < t.childCount; i++) Layer(t.GetChild(i));
        }

        private Bounds BodyBounds()
        {
            Vector3 feet = _body.transform.position;
            Bounds b = new Bounds(feet + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f));
            bool any = false;
            foreach (SkinnedMeshRenderer r in _body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r == null || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (b.size.y > 3f || b.size.y < 0.8f) b = new Bounds(feet + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f)); // (a loose cape, a long weapon: a person's own height)
            return b;
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_inst == this) _inst = null;
        }
    }
}
#endif
