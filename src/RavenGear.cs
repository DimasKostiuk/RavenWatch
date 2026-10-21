#if RAVEN_ITEM
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Річ для ворона»: a Workshop model marked "use: gear" — something a raven wears (a saddle on its back, a charm on
    // its neck, rings on its legs, a hat of our own: only for looks, in the pool of the game's hats), made at the raven table (Perches.TableStation). How it is given to a raven and what
    // it gives come later; here is how such a model sits on a raven, the same in the Workshop's picture, in the book's
    // and in the game.
    //
    // The model is made round the raven as it sits (the Workshop shows a sitting raven of the top size there): point 0
    // is the slot's bone (the back's Spine, the neck's Spine1, the left leg's Leg3.L), the axes are the raven's own (its
    // front +Z). In the game the model hangs on that bone and so goes with it (breath, legs, flight). On the legs it is
    // worn on both: the right one gets the model mirrored. The book's fitting (Outfits.Fit, in metres and degrees in
    // that same frame) moves it a little from where the Workshop put it.
    // A thing on the neck goes part way with the head: between where the neck's root would carry it and where the head
    // would, as the skin of the neck between them. After the bones have their pose for the frame.
    [DefaultExecutionOrder(30000)]
    internal sealed class NeckFollow : MonoBehaviour
    {
        internal static float Share = 0.6f; // how much of the head's motion it takes
        internal Transform Root, Head;
        internal Vector3 PosRoot, PosHead;
        internal Quaternion RotRoot, RotHead;

        private void LateUpdate()
        {
            if (Root == null || Head == null) return;
            Vector3 a = Root.TransformPoint(PosRoot), b = Head.TransformPoint(PosHead);
            Quaternion qa = Root.rotation * RotRoot, qb = Head.rotation * RotHead;
            transform.position = Vector3.Lerp(a, b, Share);
            transform.rotation = Quaternion.Slerp(qa, qb, Share);
        }
    }

    internal static class RavenGear
    {
        internal const string Use = "gear";
        internal const string Back = "back", Neck = "neck", Legs = "legs", Head = "head";
        internal static readonly string[] SlotIds = { Back, Neck, Legs, Head };
        internal static string[] SlotNames { get { return Lang.Arr(SlotNamesSrc); } } // (translated on every read)
        private static readonly string[] SlotNamesSrc = Lang.Keys("Спина", "Шия", "Лапи", "Шапка");
        internal const string HolderName = "RavenWatch_Gear";
        internal const string KeyPrefix = "gear_"; // what a raven wears is kept as this + the model's id (Outfits)

        // the thing as an item of the game (WsItems makes it): RavenWatch_gear_<id>
        internal const string PrefabStart = "RavenWatch_";
        internal static string PrefabOf(string key) { return PrefabStart + key; }
        internal static string KeyOfPrefab(string prefab) { return prefab != null && prefab.StartsWith(PrefabStart + KeyPrefix, System.StringComparison.Ordinal) ? prefab.Substring(PrefabStart.Length) : null; }

        // The feeding pouch (торба): a thing on the back with room for a little food the raven eats by itself (RavenPouch).
        internal static readonly string[] PouchIds = { "sumka_dlia_hoduvannia" };
        internal static bool IsPouch(string key) { return key != null && key.StartsWith(KeyPrefix, System.StringComparison.Ordinal) && System.Array.IndexOf(PouchIds, key.Substring(KeyPrefix.Length)) >= 0; }

        // the words on the item: the model's note, and how it is worn
        internal static string Description(WsModel m)
        {
            string note = m != null && !string.IsNullOrEmpty(m.Note) && m.Note.Trim().ToLowerInvariant() != (m.Name ?? "").Trim().ToLowerInvariant() ? m.Note.Trim() + "\n\n" : "";
            string id = m != null ? (string.IsNullOrEmpty(m.Id) || m.Id == Perches.IdFor("") ? Perches.IdFor(m.Name) : m.Id) : "";
            if (IsPouch(KeyPrefix + id))
                return Lang.F("{0}Торба на спину ворона. ПКМ — надіти Хугіну, Shift+ПКМ — Мунінові (або в Книзі воронів, вкладка «Образ»). Поклади туди їжу — до {1} шматків. Коли ворон зголодніє, поїсть із неї сам.", note, RavenPouch.Max);
            string fx = GearFx.Text(id);
            int lv = GearFx.WearLevel(KeyPrefix + id);
            return Lang.F("{0}Річ для ворона ({1}). Надівається з {2}-го рівня довіри: ПКМ — Хугіну, Shift+ПКМ — Мунінові (або в Книзі воронів, вкладка «Образ»).", (note + (fx != null ? fx + "\n\n" : "")), Lang.Lower(SlotNames[System.Array.IndexOf(SlotIds, SlotOf(m))]), lv);
        }

        // the raven table it is made at: the one its model names, else the first there is (null: none yet)
        internal static string TableOf(WsModel m)
        {
            string want = m != null ? m.Station : null;
            foreach (Perches.Def d in Perches.Tables) if (d.Built != null && d.Prefab == want) return d.Prefab;
            foreach (Perches.Def d in Perches.Tables) if (d.Built != null) return d.Prefab;
            return null;
        }

        internal static string SlotOf(WsModel m) { return m != null && (m.Slot == Legs || m.Slot == Neck || m.Slot == Head) ? m.Slot : Back; }
        // a hat rides the head itself (Jaw: the skull with the upper beak), as the game's head pieces do (Outfits)
        internal static string BoneName(string slot, bool right) { return slot == Legs ? (right ? "Leg3.R" : "Leg3.L") : slot == Neck ? "Spine1" : slot == Head ? "Jaw" : "Spine"; }
        // On the neck it moves with the neck as it is (its root, Spine1); "follow: body" (a pendant on the chest, the
        // Workshop's «Рухається з шиєю» off): made at the same place, but carried by the body (Spine), not the neck.
        internal const string FollowBody = "body";
        internal static string FollowOf(WsModel m, string slot) { return slot == Neck && m != null && m.Follow == FollowBody ? "Spine" : null; }

        internal static Transform Bone(GameObject raven, string name)
        {
            if (raven == null) return null;
            foreach (Transform t in raven.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        // ---------------------------------------------------------------- the things there are
        // In the mod: the models built into it (assets/gear/*.txt). While tuning (an admin, config "Outfits all for
        // tuning"): also every Workshop file marked as a thing for a raven, as soon as it is saved there.
        internal sealed class Item
        {
            public string Key, Id, Slot;
            public string NameSrc, NoteSrc; // the source texts (Ukrainian)
            public string Name { get { return Lang.T(NameSrc); } set { NameSrc = value; } }
            public string Note { get { return Lang.T(NoteSrc); } set { NoteSrc = value; } }
            public WsModel Model;
            public bool BuiltIn;  // in the mod itself (not only a Workshop file)
            public Sprite Icon;   // its picture for the book, taken once
            public bool IconDone;
        }

        internal static int Version; // bumped whenever the list (or a model in it) changes
        private static List<Item> _items;
        private static readonly Dictionary<string, Item> ByKey = new Dictionary<string, Item>();
        private static int _savedSeen = -1;
        private static bool _tuningSeen;

        internal static List<Item> Items()
        {
            bool tuning = Outfits.TuningAllowed; // (not Tuning: the book's player view switches that, and the files would be read again each time)
            if (_items != null && _savedSeen == WsFiles.Saved && _tuningSeen == tuning) return _items;
            _savedSeen = WsFiles.Saved; _tuningSeen = tuning;
            _items = new List<Item>();
            ByKey.Clear();
            try
            {
                System.Reflection.Assembly asm = typeof(RavenGear).Assembly;
                foreach (string res in asm.GetManifestResourceNames())
                {
                    if (!res.StartsWith("RavenWatch.gear.", System.StringComparison.Ordinal)) continue;
                    using (Stream st = asm.GetManifestResourceStream(res))
                    using (StreamReader rd = new StreamReader(st, Encoding.UTF8)) Add(WsFiles.FromText(rd.ReadToEnd()), res, true);
                }
            }
            catch (System.Exception e) { Perches.Log("built-in gear: " + e.Message); }
            if (tuning || Perches.FromFiles) foreach (string f in WsFiles.List()) { WsModel m = WsFiles.Load(f); if (m != null) Add(m, f, false); }
            _items.Sort((a, b) => a.Slot != b.Slot ? System.Array.IndexOf(SlotIds, a.Slot).CompareTo(System.Array.IndexOf(SlotIds, b.Slot)) : string.Compare(a.Name, b.Name, System.StringComparison.CurrentCulture));
            Version++;
            return _items;
        }

        private static void Add(WsModel m, string file, bool builtIn)
        {
            if (m == null || m.Use != Use) return;
            if (string.IsNullOrEmpty(m.Name)) m.Name = Path.GetFileNameWithoutExtension(file);
            m.File = file;
            // no id, or the Workshop's stand-in for a model marked before it had a name ("perch"): its own from the name
            string id = string.IsNullOrEmpty(m.Id) || m.Id == Perches.IdFor("") ? Perches.IdFor(m.Name) : m.Id;
            Item old;
            // two Workshop files with one id: both kept (the second gets a number), neither hides the other
            if (!builtIn && ByKey.TryGetValue(KeyPrefix + id, out old) && !old.BuiltIn)
            {
                string first = id;
                for (int n = 2; ByKey.ContainsKey(KeyPrefix + id); n++) id = first + "_" + n;
                Perches.Log("gear " + m.Name + ": id «" + first + "» is taken by " + old.Name + ", shown as " + id + " (give it its own id)");
            }
            Item it = new Item { Id = id, Key = KeyPrefix + id, Name = m.Name, Note = m.Note, Slot = SlotOf(m), Model = m, BuiltIn = builtIn };
            if (ByKey.TryGetValue(it.Key, out old)) { it.BuiltIn = old.BuiltIn || builtIn; _items.Remove(old); } // a Workshop file wins over the built-in one
            ByKey[it.Key] = it;
            _items.Add(it);
        }

        internal static Item Find(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(KeyPrefix, System.StringComparison.Ordinal)) return null;
            Items();
            Item it;
            return ByKey.TryGetValue(key, out it) ? it : null;
        }

        // Has this raven got the thing? Made at the raven table and given to it in the book: it wears it (Outfits.PutOn).
        internal static bool Owned(ItemDrop.ItemData raven, Item it) { return it != null && Outfits.Worn(raven, Outfits.SlotOfGear(it.Slot)) == it.Key; }

        // the book's picture of the thing: its own photo (the tab «Фото»), or photographed by itself once
        internal static Sprite IconOf(Item it)
        {
            if (it == null) return null;
            if (it.IconDone) return it.Icon;
            it.IconDone = true;
            it.Icon = IconPhotos.Get(it.Id);
            if (it.Icon != null) return it.Icon;
            GameObject holder = new GameObject("RavenWatch_GearIcon");
            holder.transform.position = new Vector3(0f, 9650f, 0f);
            try
            {
                foreach (WsPart p in it.Model.Parts) if (!WsFiles.Special(p.Src)) Perches.Part(holder.transform, p, false);
                it.Icon = Perches.Photo(holder.transform);
            }
            catch (System.Exception e) { Perches.Log("gear icon " + it.Name + ": " + e.Message); }
            finally { Object.DestroyImmediate(holder); }
            return it.Icon;
        }

        // a new photo taken (or the own one thrown away): the thing's picture again
        internal static void IconAgain(string id)
        {
            if (_items == null) return;
            foreach (Item it in _items) if (it.Id == id) it.IconDone = false; // the book asks IconOf when it draws
        }

        // ---------------------------------------------------------------- the sitting raven, measured once
        // where each bone is and how it is turned and sized, in the frame the Workshop draws in (the raven's holder)
        private struct Ref { public Quaternion Rot; public float Scale; public Vector3 Pos; }
        private static readonly Dictionary<string, Ref> Refs = new Dictionary<string, Ref>();

        // the Workshop's frame on a bone, for what follows it without being put under it (FxLib): the turn and the size
        internal static bool FrameOf(bool munin, string bone, out Quaternion rot, out float scale)
        {
            Ref r;
            bool ok = RefOf(munin, bone, out r);
            rot = ok ? r.Rot : Quaternion.identity; scale = ok ? r.Scale : 1f;
            return ok;
        }

        private static bool RefOf(bool munin, string bone, out Ref r)
        {
            string key = (munin ? "m|" : "h|") + bone;
            if (Refs.TryGetValue(key, out r)) return true;
            GameObject holder = new GameObject("RavenWatch_GearRef");
            holder.transform.position = new Vector3(0f, 9200f, 0f);
            try
            {
                GameObject bird = Perches.Sitter(holder.transform, munin, Shoulder.Scale, -1, null, null);
                Transform b = Bone(bird, bone);
                if (b == null) return false;
                r.Rot = Quaternion.Inverse(holder.transform.rotation) * b.rotation;
                r.Scale = Mathf.Max(0.0001f, Mathf.Abs(b.lossyScale.x) / Mathf.Max(0.0001f, Mathf.Abs(holder.transform.lossyScale.x)));
                r.Pos = Quaternion.Inverse(holder.transform.rotation) * (b.position - holder.transform.position) / Mathf.Max(0.0001f, Mathf.Abs(holder.transform.lossyScale.x));
                Refs[key] = r;
                return true;
            }
            finally { Object.DestroyImmediate(holder); }
        }

        // Hugin or Munin, by the model's body mesh (a raven model without our components: the book's picture)
        internal static bool IsMunin(GameObject raven)
        {
            ShoulderLife life = raven != null ? raven.GetComponent<ShoulderLife>() : null;
            if (life != null) return life.Munin;
            string mine = MeshOf(raven);
            Raven m = Shoulder.Template(true);
            return mine.Length > 0 && m != null && m.m_visual != null && MeshOf(m.m_visual) == mine;
        }

        private static string MeshOf(GameObject g)
        {
            SkinnedMeshRenderer s = g != null ? g.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            return s != null && s.sharedMesh != null ? s.sharedMesh.name : "";
        }

        // ---------------------------------------------------------------- on a raven

        // the model put on a raven (its visual): on the slot's bone, or both legs; what it wore before goes.
        // left = false: only the mirrored right leg (the Workshop draws the left one itself, to be edited)
        internal static void Attach(GameObject raven, WsModel m, bool munin, int layer, bool left = true)
        {
            Detach(raven);
            if (raven == null || m == null) return;
            string slot = SlotOf(m);
            if (slot == Legs)
            {
                if (left) One(raven, m, munin, layer, BoneName(slot, false), false, null, null);
                One(raven, m, munin, layer, BoneName(slot, true), true, null, null);
            }
            else One(raven, m, munin, layer, BoneName(slot, false), false, null, null, FollowOf(m, slot));
        }

        internal static void Detach(GameObject raven)
        {
            if (raven == null) return;
            List<GameObject> gone = new List<GameObject>();
            foreach (Transform t in raven.GetComponentsInChildren<Transform>(true)) if (t.name == HolderName) gone.Add(t.gameObject);
            foreach (GameObject g in gone) Object.Destroy(g);
        }

        // Worn (Outfits.Dress): on its bone (both legs), moved by the book's fit, marked as an outfit piece so the
        // next dressing takes it off. Other slots' things stay.
        internal static void PutOn(GameObject raven, Item it, Outfits.Fit fit)
        {
            if (raven == null || it == null) return;
            bool munin = IsMunin(raven);
            if (it.Slot == Legs)
            {
                One(raven, it.Model, munin, raven.layer, BoneName(Legs, false), false, fit, it.Key);
                One(raven, it.Model, munin, raven.layer, BoneName(Legs, true), true, fit, it.Key);
            }
            else One(raven, it.Model, munin, raven.layer, BoneName(it.Slot, false), false, fit, it.Key, FollowOf(it.Model, it.Slot));
        }

        // an empty holder on a bone of this raven, in the Workshop's frame (the raven's own axes as it sits, front +Z;
        // sizes of a shoulder raven, growing with the raven); null when the raven has no such bone
        internal static Transform Holder(GameObject raven, bool munin, string boneName, string name)
        {
            Transform bone = Bone(raven, boneName);
            Ref r;
            if (bone == null || !RefOf(munin, boneName, out r)) return null;
            GameObject h = new GameObject(name);
            h.transform.SetParent(bone, false);
            h.transform.localPosition = Vector3.zero;
            h.transform.localRotation = Quaternion.Inverse(r.Rot);
            h.transform.localScale = Vector3.one / r.Scale;
            return h.transform;
        }

        // boneName: where point 0 is (as made in the Workshop); follow: the bone that carries it, when another (null: the same)
        private static void One(GameObject raven, WsModel m, bool munin, int layer, string boneName, bool mirror, Outfits.Fit fit, string outfit, string follow = null)
        {
            string carrier = string.IsNullOrEmpty(follow) ? boneName : follow;
            Transform bone = Bone(raven, carrier);
            Ref r, at;
            if (bone == null || !RefOf(munin, carrier, out r) || !RefOf(munin, boneName, out at)) { Perches.Log("gear " + m.Name + ": no bone " + carrier); return; }
            GameObject holder = new GameObject(outfit != null ? "RavenWatch_Outfit_" + outfit : HolderName);
            if (outfit != null) holder.AddComponent<Outfits.Piece>();
            holder.transform.SetParent(bone, false);
            // its point 0 where the slot's bone sits (the carrier's own place when it is the same bone)
            holder.transform.localPosition = carrier == boneName ? Vector3.zero : Quaternion.Inverse(r.Rot) * (at.Pos - r.Pos) / r.Scale;
            holder.transform.localRotation = Quaternion.Inverse(r.Rot);
            holder.transform.localScale = Vector3.one / r.Scale;
            // on the neck, moving with it: the neck's root (Spine1) hardly moves; the head (Neck) does all the looking
            // about. The thing is carried part way with the head, as the neck's own skin is (NeckFollow)
            if (carrier == "Spine1" && boneName == "Spine1")
            {
                Transform head = Bone(raven, "Neck");
                Ref h;
                if (head != null && RefOf(munin, "Neck", out h))
                {
                    NeckFollow nf = holder.AddComponent<NeckFollow>();
                    nf.Root = bone; nf.Head = head;
                    nf.PosRoot = holder.transform.localPosition; nf.RotRoot = holder.transform.localRotation;
                    nf.PosHead = Quaternion.Inverse(h.Rot) * (at.Pos - h.Pos) / h.Scale; nf.RotHead = Quaternion.Inverse(h.Rot);
                }
            }
            Transform inner = holder.transform;
            if (mirror)
            {
                GameObject flip = new GameObject("mirror");
                flip.transform.SetParent(holder.transform, false);
                flip.transform.localScale = new Vector3(-1f, 1f, 1f);
                inner = flip.transform;
            }
            if (fit != null)
            {
                // the book's fit: in the Workshop's frame (metres, degrees), so a mirrored leg is moved mirrored
                GameObject f = new GameObject("fit");
                f.transform.SetParent(inner, false);
                f.transform.localPosition = new Vector3(fit.X, fit.Y, fit.Z);
                f.transform.localRotation = Quaternion.Euler(fit.RX, fit.RY, fit.RZ);
                f.transform.localScale = new Vector3(fit.S * fit.SX, fit.S * fit.SY, fit.S * fit.SZ);
                inner = f.transform;
            }
            foreach (WsPart p in m.Parts) if (!WsFiles.Special(p.Src)) Perches.Part(inner, p, false);
            foreach (Transform t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer >= 0 ? layer : raven.layer;
            foreach (Renderer rd in holder.GetComponentsInChildren<Renderer>(true)) { rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rd.receiveShadows = false; }
        }
    }
}
#endif
