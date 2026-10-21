#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // «Принести»: a small light thing lying on the ground, the one you look at («Туди», SitHere.Look), is fetched by
    // the raven: one piece of it, never more (the user, 2026-10-15), MaxWeight kg at most. It is an order
    // (Orders.Fetch: costs as one, its own flight FetchPlan), the spot it flies to on the player's ZDO for every game.
    // The owner's game takes the piece when the raven is down at it (Grab: the thing's own owner gives it over first,
    // as for a pickup; ItemDrop.RemoveOne), keeps it, and puts it in your bag once the raven sits on you again (Tick);
    // the bag full: at your feet. Meanwhile the thing is seen in its beak on every game (Visuals: its prefab's name on
    // the player's ZDO, a copy of the item's meshes on the bone «Jaw»).
    internal static class RavenFetch
    {
        internal static float MaxWeight = 2f;   // kg a piece
        internal static float Range = 40f;      // m from you
        internal static float Bring = 25f;      // s: home by then or not, what it took is given
        internal const string AtH = "RavenWatch_FetchH", AtM = "RavenWatch_FetchM";       // where it flies to
        internal const string CarryH = "RavenWatch_CarryH", CarryM = "RavenWatch_CarryM"; // what is in its beak (a prefab)

        // the owner's game: the thing it was sent for, and what it holds
        private static readonly ZDOID[] Want = { ZDOID.None, ZDOID.None };
        private static readonly ItemDrop.ItemData[] Held = new ItemDrop.ItemData[2];
        private static readonly float[] HeldAt = new float[2];
        private static readonly float[] Dist = new float[2];
        private static ZNet _heldIn; // the world it was taken in (another world: it is not brought there)

        private static void Say(Player p, bool munin, string text) { if (p != null) p.Message(MessageHud.MessageType.TopLeft, Grammar.Fix(text, p), 0, RavenItem.Icon(munin)); }

        internal static string NameOf(ItemDrop d)
        {
            if (d == null || d.m_itemData == null || d.m_itemData.m_shared == null) return Lang.T("річ");
            string n = d.m_itemData.m_shared.m_name ?? "";
            return Localization.instance != null ? Lang.Game(n) : n;
        }

        // why this one cannot be fetched (null: it can)
        internal static string WhyNot(ItemDrop d)
        {
            ZNetView nv = d != null ? d.GetComponent<ZNetView>() : null;
            if (d == null || nv == null || !nv.IsValid()) return Lang.T("Цього вже нема.");
            d.Load();
            if (d.m_itemData == null || d.m_itemData.m_shared == null || d.IsPiece()) return Lang.T("Це не підняти.");
            bool m;
            if (RavenItem.IsRaven(d.m_itemData, out m)) return Lang.T("Ворона ворон не носить.");
            float w = d.m_itemData.m_shared.m_weight;
            if (w > MaxWeight) return Lang.F("Заважке для ворона: {0} кг (до {1}).", Satiety.Num(Mathf.Round(w * 10f) / 10f), Satiety.Num(MaxWeight));
            return null;
        }

        internal static bool Carrying(bool munin) { return Held[munin ? 1 : 0] != null; }

        // the order (Perches.GoHere for a thing under the crosshair); Orders.Give says itself why not
        internal static string Go(Player p, bool munin, ItemDrop d, float dist)
        {
            string why = WhyNot(d);
            if (why != null) return why;
            int k = munin ? 1 : 0;
            if (Held[k] != null) return Lang.F("{0} ще несе попереднє.", Plugin.RavenName(munin));
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return null;
            Want[k] = d.GetComponent<ZNetView>().GetZDO().m_uid;
            Dist[k] = dist;
            z.Set(munin ? AtM : AtH, d.transform.position);
            if (!Orders.Give(p, munin, Orders.Fetch)) { Want[k] = ZDOID.None; return null; }
            d.RequestOwn(); // the thing's own game hands it to this one meanwhile (a pickup asks the same)
            return null;
        }

        // where it flies (every game)
        internal static Vector3 Target(Player p, bool munin)
        {
            ZDO z = p != null ? NetFlag.Zdo(p, false) : null;
            Vector3 me = p != null ? p.transform.position : Vector3.zero;
            return z != null ? z.GetVec3(munin ? AtM : AtH, me) : me;
        }

        // how long the order may take: there, a moment, and the start of the way back
        internal static float Seconds(Player p, bool munin) { return 10f + Dist[munin ? 1 : 0] / 5f; }

        // FetchPlan, down at the thing (every game asks; the owner's game takes it): true when the order is over
        private static float _askedAt = -9f;
        internal static bool Grab(Player p, bool munin)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer)) return false;
            int k = munin ? 1 : 0;
            if (Want[k] == ZDOID.None) return false;
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(Want[k]) : null;
            ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
            ZNetView nv = d != null ? d.GetComponent<ZNetView>() : null;
            if (d == null || nv == null || !nv.IsValid())
            {
                Want[k] = ZDOID.None;
                Orders.Fetched(p, munin);
                Say(p, munin, Lang.F("{0}: не знайшов — хтось уже підібрав.", Plugin.RavenName(munin)));
                return true;
            }
            if (!nv.IsOwner()) { if (Time.time - _askedAt > 0.5f) { _askedAt = Time.time; d.RequestOwn(); } return false; } // not handed over yet
            d.Load();
            ItemDrop.ItemData one = d.m_itemData.Clone();
            one.m_stack = 1;
            string prefab = one.m_dropPrefab != null ? one.m_dropPrefab.name : Utils.GetPrefabName(d.gameObject);
            if (!d.RemoveOne()) return false;
            Held[k] = one; HeldAt[k] = Time.time; Want[k] = ZDOID.None; _heldIn = ZNet.instance;
            ZDO z = NetFlag.Zdo(p, true);
            if (z != null) z.Set(munin ? CarryM : CarryH, prefab);
            Orders.Fetched(p, munin);
            return true;
        }

        // the order ran out before it got there (Orders.Tick)
        internal static void Missed(bool munin) { Want[munin ? 1 : 0] = ZDOID.None; }

        // From Orders.Tick (the owner's game, twice a second): what it holds goes into your bag once it sits on you again
        internal static void Tick(Player p)
        {
            for (int k = 0; k < 2; k++)
            {
                if (Held[k] == null) continue;
                bool munin = k == 1;
                if (ZNet.instance != _heldIn) { Held[k] = null; continue; } // (another world now: the piece stayed in that one)
                if (p == null) continue;                                       // (between lives: it waits)
                GameObject model = Shoulder.ModelOf(p, munin);
                ShoulderPin pin = model != null ? model.GetComponent<ShoulderPin>() : null;
                bool home = pin != null && pin.enabled && !pin.InAir && !pin.Landing;
                if (!home && !p.IsDead() && model != null && Time.time - HeldAt[k] < Bring) continue;
                Give(p, munin, Held[k]);
                Held[k] = null;
                ZDO z = NetFlag.Zdo(p, true);
                if (z != null) z.Set(munin ? CarryM : CarryH, "");
            }
        }

        // the character is saved (leaving the world, or the game's own save): what a beak holds goes into the bag now,
        // else it would be lost with the session (it lives only here)
        internal static void BeforeSave(Player p)
        {
            if (p == null || p != Player.m_localPlayer || ZNet.instance != _heldIn) return;
            for (int k = 0; k < 2; k++)
            {
                if (Held[k] == null) continue;
                Give(p, k == 1, Held[k]);
                Held[k] = null;
                ZDO z = NetFlag.Zdo(p, true);
                if (z != null) z.Set(k == 1 ? CarryM : CarryH, "");
            }
        }

        private static void Give(Player p, bool munin, ItemDrop.ItemData one)
        {
            string name = Localization.instance != null ? Lang.Game(one.m_shared.m_name) : one.m_shared.m_name;
            if (!p.IsDead() && p.GetInventory().AddItem(one)) { Say(p, munin, Lang.F("{0} приніс: {1}.", Plugin.RavenName(munin), name)); return; }
            Vector3 at = p.transform.position + p.transform.forward * 0.7f + Vector3.up * 0.5f;
            ItemDrop.DropItem(one, 1, at, Quaternion.identity);
            if (!p.IsDead()) Say(p, munin, Lang.F("{0} приніс: {1} — у сумці нема місця, кинув під ноги.", Plugin.RavenName(munin), name));
        }

        // ---------------------------------------------------------------- in its beak (every game)

        private const string Tag = "RavenWatch_Carry:";
        // marks the copy in a beak: the eyes' glow (EyeGlow) leaves its materials alone
        internal class Carried : MonoBehaviour { }
        // what is shown in a beak now, by the player's ZDO and the raven (a model gone takes its copy with it: then null)
        private static readonly System.Collections.Generic.Dictionary<ZDOID, Transform>[] Shown = { new System.Collections.Generic.Dictionary<ZDOID, Transform>(), new System.Collections.Generic.Dictionary<ZDOID, Transform>() };

        // From Shoulder.Tick (twice a second): the players near, both ravens: what their ZDO says it holds, on its beak
        // (a raven that holds nothing and shows nothing costs one ZDO read)
        internal static void Visuals()
        {
            Player me = Player.m_localPlayer;
            if (me == null || (ZNet.instance != null && ZNet.instance.IsDedicated())) return;
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null || (pl.transform.position - me.transform.position).sqrMagnitude > 80f * 80f) continue;
                ZDO z = NetFlag.Zdo(pl, false);
                ZDOID id = pl.GetZDOID();
                for (int k = 0; k < 2; k++)
                {
                    bool munin = k == 1;
                    string want = z != null ? z.GetString(munin ? CarryM : CarryH, "") : "";
                    Transform have;
                    bool had = Shown[k].TryGetValue(id, out have);
                    if (!had && want.Length == 0) continue; // (nothing held, nothing shown: the usual case, nothing more looked up)
                    if (have != null && have.name == Tag + want) continue;
                    if (have != null) Object.Destroy(have.gameObject);
                    Shown[k].Remove(id);
                    if (want.Length == 0) continue;
                    GameObject model = Shoulder.ModelOf(pl, munin);
                    Transform jaw = model != null ? Bone(model, "Jaw") : null;
                    Transform made = jaw != null ? Beak(model, jaw, want) : null;
                    if (made != null) Shown[k][id] = made;
                }
            }
        }

        private static Transform Bone(GameObject r, string name)
        {
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // a copy of the item's look (its first level of detail, meshes only: nothing of the game's object), held at the
        // beak's tip, about a fifth of the raven in size
        private static Transform Beak(GameObject model, Transform jaw, string prefab)
        {
            GameObject src = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (src == null) return null;
            GameObject root = new GameObject(Tag + prefab);
            root.AddComponent<Carried>();
            root.layer = model.layer;
            root.transform.SetParent(jaw, false);
            Renderer[] parts;
            Transform look = src.transform.Find("RavenWatch_Look"); // an item in a model's look (WsItems): that, not its hidden own shape
            LODGroup lod = look == null ? src.GetComponentInChildren<LODGroup>(true) : null;
            LOD[] lods = lod != null ? lod.GetLODs() : null;
            parts = look != null ? look.GetComponentsInChildren<Renderer>(true) : lods != null && lods.Length > 0 ? lods[0].renderers : src.GetComponentsInChildren<Renderer>(true);
            Transform sr = src.transform;
            foreach (Renderer r in parts)
            {
                MeshRenderer mr = r as MeshRenderer;
                MeshFilter mf = mr != null ? mr.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject g = new GameObject("m");
                g.layer = model.layer;
                g.transform.SetParent(root.transform, false);
                g.transform.localPosition = sr.InverseTransformPoint(mr.transform.position);
                g.transform.localRotation = Quaternion.Inverse(sr.rotation) * mr.transform.rotation;
                g.transform.localScale = Div(mr.transform.lossyScale, sr.lossyScale);
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                MeshRenderer nr = g.AddComponent<MeshRenderer>();
                nr.sharedMaterials = mr.sharedMaterials;
                nr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Bounds b, body;
            if (!Measure(root, out b) || !Measure(model, out body, root.transform)) { Object.Destroy(root); return null; }
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float want = Mathf.Max(body.size.x, Mathf.Max(body.size.y, body.size.z)) * 0.22f;
            if (size > 0.0001f) root.transform.localScale = root.transform.localScale * (want / size);
            // its middle just under the beak's tip
            Transform tip = Bone(model, "Jaw1_end");
            Vector3 aim = tip != null ? tip.position : jaw.position;
            Measure(root, out b);
            root.transform.position += aim - b.center + Vector3.down * b.extents.y * 0.3f;
            return root.transform;
        }

        // The post (PostBird): a letter in the beak, as here; a parcel in its claws — its top held between the feet every
        // frame (ClawGrip: the toes' middle, wherever the tucked legs are), upright on the bird's heading. (Placed once by
        // the bird's bounds it hung a body below: in flight the wings reach far under the body.) null: cannot be shown.
        internal static Transform Hold(GameObject model, string prefab, bool claws)
        {
            if (model == null || string.IsNullOrEmpty(prefab)) return null;
            if (!claws)
            {
                Transform jaw = Bone(model, "Jaw");
                return jaw != null ? Beak(model, jaw, prefab) : null;
            }
            Transform made = Beak(model, model.transform, prefab); // (sized as in a beak, a little bigger below)
            if (made == null) return null;
            made.localScale *= 1.3f;
            System.Collections.Generic.List<Transform> toes = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "index1.l" || n == "index1.r" || n == "mid1.l" || n == "mid1.r" || n == "backtalon1.l" || n == "backtalon1.r") toes.Add(t);
            }
            Bounds b, body;
            if (!Measure(made.gameObject, out b)) return made;
            if (toes.Count > 0)
            {
                ClawGrip g = made.gameObject.AddComponent<ClawGrip>();
                g.Bird = model.transform; g.Toes = toes.ToArray();
                g.TopLocal = made.InverseTransformPoint(new Vector3(b.center.x, b.max.y - b.extents.y * 0.2f, b.center.z)); // (a little of its top in the grip)
                g.Follow();
            }
            else if (Measure(model, out body, made)) // (no toes found: under the body, as near as the bounds tell)
                made.position += new Vector3(body.center.x, body.min.y + body.extents.y * 0.25f, body.center.z) - b.center - Vector3.up * b.extents.y;
            return made;
        }

        private static Vector3 Div(Vector3 a, Vector3 b) { return new Vector3(a.x / Mathf.Max(0.0001f, b.x), a.y / Mathf.Max(0.0001f, b.y), a.z / Mathf.Max(0.0001f, b.z)); }

        // the world bounds of what is drawn under r (skip: a part not to count)
        private static bool Measure(GameObject r, out Bounds b, Transform skip = null)
        {
            b = new Bounds();
            bool any = false;
            foreach (Renderer x in r.GetComponentsInChildren<Renderer>(false))
            {
                if (x is ParticleSystemRenderer || (skip != null && x.transform.IsChildOf(skip))) continue;
                if (!any) { b = x.bounds; any = true; } else b.Encapsulate(x.bounds);
            }
            return any;
        }
    }

    // a parcel in the claws: its top between the feet (the toes' middle), upright, turned the way the bird flies
    internal sealed class ClawGrip : MonoBehaviour
    {
        internal Transform Bird;
        internal Transform[] Toes;
        internal Vector3 TopLocal;

        internal void Follow()
        {
            if (Bird == null || Toes == null) return;
            Vector3 feet = Vector3.zero;
            int n = 0;
            foreach (Transform t in Toes) if (t != null) { feet += t.position; n++; }
            if (n == 0) return;
            feet /= n;
            Vector3 f = Bird.forward; f.y = 0f;
            if (f.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            transform.position += feet - transform.TransformPoint(TopLocal);
        }

        private void LateUpdate() { Follow(); } // (PostBird calls it too, right after the bird's pose: never a frame behind)
    }
}
#endif
