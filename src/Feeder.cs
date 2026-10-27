#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Годівниця» (a Workshop model marked "use: feeder"): a box of the game's own (E opens it, as a chest) by the
    // perches. A raven left at home near it (Home.FeederReach) eats from it when it asks for food, and puts its finds in
    // it. Whoever holds a raven's perch takes from the box (the box's holder for a moment, as the game does with chests).
    internal static class Feeder
    {
        internal static int Width = 4, Height = 2;

        internal static void Make(Perches.Def d, GameObject root, ZNetScene scene)
        {
            Container c = root.AddComponent<Container>();
            c.m_name = Lang.W(d.Name);
            c.m_width = Width; c.m_height = Height;
            c.m_privacy = Container.PrivacySetting.Public;
            c.m_checkGuardStone = true;
            GameObject chest = scene.GetPrefab("piece_chest_wood");
            Container like = chest != null ? chest.GetComponent<Container>() : null;
            if (like != null) { c.m_bkg = like.m_bkg; c.m_openEffects = like.m_openEffects; c.m_closeEffects = like.m_closeEffects; }
            root.AddComponent<FeederView>().Feed = d.Feed;
        }

        // the feeders near a perch: the thriftiest first, then the nearest
        private static readonly List<FeederView> _near = new List<FeederView>(4);
        internal static List<FeederView> Near(Vector3 at)
        {
            _near.Clear();
            float r = Home.FeederReach * Home.FeederReach;
            foreach (FeederView f in FeederView.All) if (f != null && f.Box != null && (f.transform.position - at).sqrMagnitude <= r) _near.Add(f);
            _near.Sort((a, b) => a.Feed != b.Feed ? b.Feed.CompareTo(a.Feed) : (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));
            return _near;
        }

        // the box to change now: held here (taken over if nobody has it open)
        private static Inventory Held(FeederView f)
        {
            ZNetView nv = f.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return null;
            if (!nv.IsOwner()) { if (f.Box.IsInUse()) return null; nv.ClaimOwnership(); }
            return f.Box.GetInventory();
        }

        // a meal from the feeder: what, how much it fills, its favourite or not
        internal sealed class Meal { internal string Prefab; internal float Fill; internal bool Fav; internal FeederView From; }

        // Not only the hand-fed list: whatever of its kind lies there — Munin any meat (raw, cooked, a meat dish), Hugin
        // fish, berries, mushrooms, vegetables and the dishes made of them. A plain thing fills little, a rich dish much:
        // a dish by the game's own food values, a raw thing by where it grows (its class).
        private static readonly string[] MeatWords = { "Meat", "NeckTail", "Sausage", "Jerky" };
        private static readonly string[] PlantStarts = { "Fish", "Mushroom" };
        private static readonly Dictionary<string, int> RawClass = new Dictionary<string, int>
        {
            { "Raspberry", 1 }, { "Honey", 1 }, { "Blueberries", 2 }, { "Carrot", 2 }, { "Turnip", 3 }, { "Cloudberry", 4 }, { "Onion", 4 },
            { "RoyalJelly", 4 }, { "Vineberry", 5 }, { "Fiddleheadfern", 5 },
        };
        internal static float DishMin = 12f, DishMax = 45f, DishPer = 6f; // a dish: 10 + (health + stamina + eitr) / DishPer, within these

        private static bool Meaty(string name) { foreach (string w in MeatWords) if (name.IndexOf(w, System.StringComparison.Ordinal) >= 0) return true; return false; }

        // how much this fills that raven (0: not its food)
        internal static float FillOf(bool munin, ItemDrop.ItemData it)
        {
            if (it == null || it.m_dropPrefab == null) return 0f;
            string name = it.m_dropPrefab.name;
            if (name.StartsWith("Fishing", System.StringComparison.Ordinal) || name.IndexOf("Entrails", System.StringComparison.Ordinal) >= 0 || name.IndexOf("Bone", System.StringComparison.Ordinal) >= 0) return 0f; // (rods, bait; never bones or guts)
            bool meat = Meaty(name);
            if (meat != munin) return 0f;
            ItemDrop.ItemData.SharedData sh = it.m_shared;
            float food = sh.m_food + sh.m_foodStamina + sh.m_foodEitr;
            if (sh.m_itemType == ItemDrop.ItemData.ItemType.Consumable && food > 0f) return Mathf.Clamp(10f + food / DishPer, DishMin, DishMax); // a dish, or something eaten as it is
            int cls;
            if (RawClass.TryGetValue(name, out cls)) return Satiety.FillOfClass(cls);
            if (meat) return Satiety.FillOfClass(Satiety.ClassOf(name, 0, 0)); // raw meat: by its biome
            foreach (string p in PlantStarts) if (name.StartsWith(p, System.StringComparison.Ordinal)) return Satiety.FillOfClass(Satiety.ClassOf(name, 0, 0)); // raw fish, a mushroom
            return 0f;
        }

        // one piece of its food out of a feeder near: its favourite first, else the most filling; null: nothing there
        internal static Meal Take(Vector3 at, bool munin, ItemDrop.ItemData raven)
        {
            foreach (FeederView f in Near(at))
            {
                Inventory inv = Held(f);
                if (inv == null) continue;
                ItemDrop.ItemData pick = null;
                float best = 0f; bool fav = false;
                foreach (ItemDrop.ItemData it in inv.GetAllItems())
                {
                    float fill = FillOf(munin, it);
                    if (fill <= 0f || Quirks.Dislikes(raven, it.m_dropPrefab.name)) continue; // («Перебірливий»: not what it will not eat)
                    bool isFav = Satiety.IsFav(raven, it.m_dropPrefab.name);
                    if (fav && !isFav) continue;
                    if (isFav && !fav) { pick = it; best = fill; fav = true; continue; }
                    if (fill > best) { pick = it; best = fill; }
                }
                if (pick == null) continue;
                string prefab = pick.m_dropPrefab.name;
                inv.RemoveItem(pick, 1);
                return new Meal { Prefab = prefab, Fill = best * (fav ? Satiety.FavFillMul : 1f) * f.Feed, Fav = fav, From = f }; // (a better feeder: the same food goes further)
            }
            return null;
        }

        // What is food for neither raven has no place in a feeder: a raven at home throws one heap of it out onto the ground
        // beside it now and then (the feeder is no free chest). The feeder it came from and the thing's name; null: nothing
        // to throw out (or not yet: once a while per feeder).
        internal static float TossMin = 40f, TossMax = 90f, TossOut = 0.9f;
        internal static FeederView TossJunk(Vector3 at, out string what)
        {
            what = null;
            foreach (FeederView f in Near(at))
            {
                if (Time.time < f.TossAt || f.Box.IsInUse()) continue;
                ZNetView nv = f.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                // only by the box's own holder, never taken over for it: a box just taken may be a stale copy (a thing
                // thrown out of it would be there twice), and two perch holders by one feeder would snatch it to and fro
                if (!nv.IsOwner()) continue;
                Inventory inv = f.Box.GetInventory();
                ItemDrop.ItemData junk = null;
                foreach (ItemDrop.ItemData it in inv.GetAllItems())
                    if (it.m_dropPrefab != null && FillOf(false, it) <= 0f && FillOf(true, it) <= 0f) { junk = it; break; }
                f.TossAt = Time.time + Random.Range(TossMin, TossMax);
                if (junk == null) continue;
                what = Localization.instance != null ? Lang.Game(junk.m_shared.m_name) : junk.m_shared.m_name;
                if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(TossLater(f, junk));
                return f;
            }
            return null;
        }

        // out it goes once its raven is there and pecking (its flight to the feeder: a couple of seconds); still in the
        // box till then — taken out only by the box's holder, out of the box first, then on the ground (never in both)
        internal static float TossDelay = 2.5f;
        private static System.Collections.IEnumerator TossLater(FeederView f, ItemDrop.ItemData junk)
        {
            yield return new WaitForSeconds(TossDelay);
            if (f == null || f.Box == null || f.Box.IsInUse()) yield break;
            ZNetView nv = f.GetComponent<ZNetView>();
            Inventory inv = f.Box.GetInventory();
            if (nv == null || !nv.IsValid() || !nv.IsOwner() || inv == null || !inv.ContainsItem(junk)) yield break; // (taken meanwhile)
            int n = junk.m_stack;
            Vector3 side = f.transform.right * (Random.value < 0.5f ? -1f : 1f) + f.transform.forward * Random.Range(-0.5f, 0.5f);
            Vector3 to = f.transform.position + side.normalized * TossOut + Vector3.up * 0.6f;
            if (!inv.RemoveItem(junk, n)) yield break;
            ItemDrop.DropItem(junk, n, to, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        }

        // a find ("Prefab:count") onto the finds shelf of the nearest feeder with room; its name, or null (no feeder, no room)
        internal static string PutFind(Vector3 at, string spec)
        {
            string[] kv = spec.Split(':');
            int n = 1;
            if (kv.Length > 1) int.TryParse(kv[1], out n);
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(kv[0]) : null;
            ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (d == null) return null;
            foreach (FeederView f in Near(at))
            {
                if (f.Box.IsInUse()) continue; // (someone has it open: later)
                ItemDrop.ItemData it = d.m_itemData.Clone();
                it.m_dropPrefab = prefab; it.m_stack = Mathf.Max(1, n);
                if (f.PutOnShelf(it, spec)) return spec;
            }
            return null;
        }
    }

    internal class FeederView : MonoBehaviour
    {
        internal static readonly List<FeederView> All = new List<FeederView>();
        internal Container Box;
        internal const string FindsKey = "RavenWatch_finds", LastKey = "RavenWatch_lastFind";
        internal static int FindsWidth = 4;
        // the finds shelf: what the ravens brought, kept apart from the food (the window shows it; you only take from it)
        internal Inventory Finds;
        private string _findsRaw = "";
        private bool _loading;
        private float _syncAt;
        private ZNetView _nv;
        internal static bool IsFinds(Inventory inv) { foreach (FeederView f in All) if (f != null && f.Finds == inv) return true; return false; }
        internal string LastFind { get { return _nv != null && _nv.IsValid() ? _nv.GetZDO().GetString(LastKey, "") : ""; } }

        // this game may change the shelf now (holds it, or takes it over while nobody has the feeder open but us)
        // (you have it open yourself: yours, whoever held it — a change by your hand is never lost; someone else has it
        // open: theirs to change now)
        internal bool ClaimFinds()
        {
            if (_nv == null || !_nv.IsValid()) return false;
            if (_nv.IsOwner()) return true;
            if (Box.IsInUse() && !FeederUi.Showing(this)) return false;
            _nv.ClaimOwnership();
            return true;
        }

        // the shelf as its holder last wrote it (every second at most)
        // now: read at once, whatever the second (before changing it: never write over a newer shelf)
        internal void Sync(bool now = false)
        {
            if (_nv == null || !_nv.IsValid() || (!now && Time.time < _syncAt)) return;
            _syncAt = Time.time + 1f;
            string raw = _nv.GetZDO().GetString(FindsKey, "");
            if (raw == _findsRaw) return;
            _findsRaw = raw;
            _loading = true;
            try { if (raw.Length > 0) Finds.Load(new ZPackage(raw)); else Finds.RemoveAll(); }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Feeder finds: " + e.Message); }
            finally { _loading = false; }
        }

        private void SaveFinds()
        {
            if (_loading || _nv == null || !_nv.IsValid() || !ClaimFinds()) return;
            ZPackage pkg = new ZPackage();
            Finds.Save(pkg);
            _findsRaw = pkg.GetBase64();
            _nv.GetZDO().Set(FindsKey, _findsRaw);
        }

        // a find onto the shelf (its holder only); false: the shelf is full
        internal bool PutOnShelf(ItemDrop.ItemData it, string spec)
        {
            Sync(true);
            if (!ClaimFinds() || !Finds.AddItem(it)) return false;
            _nv.GetZDO().Set(LastKey, spec);
            return true;
        }
        private Transform[] _spots = new Transform[0];
        private float[] _taken = new float[0];

        // where a raven eats on it (the model's @seat), or null
        internal Transform Spot(int i) { return i >= 0 && i < _spots.Length ? _spots[i] : null; }
        internal int Spots { get { return _spots.Length; } }

        // a spot nobody eats at now (the holder of the perch picks it; everyone flies there), kept for this long; -1: none
        internal int TakeSpot(float seconds)
        {
            for (int i = 0; i < _spots.Length; i++) if (Time.time >= _taken[i]) { _taken[i] = Time.time + seconds; return i; }
            return -1;
        }
        internal float TossAt; // (Feeder.TossJunk: not before this)
        public float Feed = 1f; // its thrift (Perches.Def.Feed): public, so every copy made from the prefab keeps it

        private void Awake()
        {
            Box = GetComponent<Container>();
            _nv = GetComponent<ZNetView>();
            Finds = new Inventory(Lang.T("Знахідки"), null, FindsWidth, 1);
            Finds.m_onChanged = (System.Action)System.Delegate.Combine(Finds.m_onChanged, new System.Action(SaveFinds));
            List<Transform> spots = new List<Transform>();
            for (int i = 0; ; i++) { Transform t = transform.Find("eat_" + i); if (t == null) break; spots.Add(t); }
            _spots = spots.ToArray(); _taken = new float[_spots.Length];
            ZNetView nv = GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return; // the hammer's ghost
            All.Add(this);
            _syncAt = 0f; Sync();
        }

        private void OnDestroy() { All.Remove(this); }
    }
}
#endif
