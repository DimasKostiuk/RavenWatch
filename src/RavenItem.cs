#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Hugin and Munin as items: bought from Haldor, worn on their own shoulders (RavenSeat), sitting small there.
    // Each raven rolls one bonus and one drawback when bought; a Fate stone rolls them again.
    // Built at runtime from vanilla prefabs, so the mod needs no other library.
    internal static class RavenItem
    {
        internal const string HuginPrefab = "RavenWatch_Hugin";
        internal const string MuninPrefab = "RavenWatch_Munin";
        internal const string RerollPrefab = "RavenWatch_FateStone";
        internal const string LetterPrefab = "RavenWatch_Letter"; // the post's blank sheet (Post)
        internal const string FeatherPrefab = "RavenWatch_OdinFeather"; // carried for an errand (CarryStep)
        internal const string FeatherStampKey = "RavenWatch_feather";     // on each one: which errand, step and taking (CarryStep.Stamp)

        internal static bool Enabled = true;
        internal static int HuginPrice = 1500;
        internal static int MuninPrice = 1500;
        internal static int RerollPrice = 1000;
        internal static int LetterPrice = 50;
        // no numbers here: costs and pauses come from the server's settings and grow with trust — the book shows them
        private static readonly string HuginDescription = Lang.K("Ворон думки. Правий клік — сяде на праве плече. Усе про нього — у книзі воронів (<color=orange>G</color>).");
        private static readonly string MuninDescription = Lang.K("Ворон пам'яті. Правий клік — сяде на ліве плече. Усе про нього — у книзі воронів (<color=orange>G</color>).");

        internal static GameObject Hugin;
        internal static GameObject Munin;
        internal static GameObject Reroll;
        internal static GameObject Letter;
        internal static GameObject Feather;
        private static GameObject _holder;
        private static Sprite _huginIcon;
        private static Sprite _muninIcon;
        private static Sprite _huginModel, _muninModel; // photographed from the raven models, once the game has them
        private static float _nextModelTry;
        private static int _modelTries; // a few tries, then the mod's picture stays
        // your own pictures: config/RavenWatch/icons/hugin.png and munin.png win over everything
        private static Sprite _huginFile, _muninFile;
        private static bool _filesRead;

        internal static string Name(bool munin) { return munin ? Lang.T("Ворон Мунін") : Lang.T("Ворон Хугін"); }
        internal static readonly string RerollName = Lang.K("Камінь долі ворона");

        private static readonly MethodInfo UpdateRegisters = AccessTools.Method(typeof(ObjectDB), "UpdateRegisters");
        // the byte[] overload, named outright: newer Unity also has a Span one
        private static readonly MethodInfo LoadImage = AccessTools.Method(typeof(ImageConversion), "LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]) });

        // ---------------------------------------------------------------- the items

        private static GameObject Clone(GameObject source, string name)
        {
            if (source == null) return null;
            if (_holder == null)
            {
                _holder = new GameObject("RavenWatch_Prefabs");
                _holder.SetActive(false); // keeps the templates out of the world: nothing in them wakes up
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            GameObject go = UnityEngine.Object.Instantiate(source, _holder.transform, false);
            go.name = name;
            ItemDrop drop = go.GetComponent<ItemDrop>();
            if (drop == null) { UnityEngine.Object.Destroy(go); return null; }
            drop.m_itemData.m_dropPrefab = go;
            return go;
        }

        private static GameObject BuildRaven(GameObject feathers, bool munin)
        {
            GameObject go = Clone(feathers, munin ? MuninPrefab : HuginPrefab);
            if (go == null) return null;
            ItemDrop.ItemData.SharedData sh = go.GetComponent<ItemDrop>().m_itemData.m_shared;
            sh.m_name = munin ? Lang.Word("rw_item_munin", "Ворон Мунін") : Lang.Word("rw_item_hugin", "Ворон Хугін");
            sh.m_description = Lang.Word(munin ? "rw_item_munin_desc" : "rw_item_hugin_desc", munin ? MuninDescription : HuginDescription);
            sh.m_itemType = ItemDrop.ItemData.ItemType.Misc; // not a slot of the game: the ravens have their own shoulders (RavenSeat)
            sh.m_maxStackSize = 1;
            sh.m_weight = 0.5f;
            sh.m_value = 0;
            sh.m_teleportable = true;
            sh.m_questItem = false;
            sh.m_equipStatusEffect = null; // the passive belongs to each raven, see Perks
            Sprite icon = Icon(munin);
            if (icon != null) sh.m_icons = new Sprite[] { icon };
            return go;
        }

        private static GameObject BuildReroll(GameObject stone)
        {
            GameObject go = Clone(stone, RerollPrefab);
            if (go == null) return null;
            ItemDrop.ItemData.SharedData sh = go.GetComponent<ItemDrop>().m_itemData.m_shared;
            sh.m_name = Lang.Word("rw_item_reroll", RerollName);
            sh.m_description = Lang.Word("rw_item_reroll_desc", "Використай (правий клік), і ворон, що сидить у тебе на плечі, отримає нове випадкове благо й тягар. Камінь при цьому зникає.");
            sh.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            sh.m_maxStackSize = 10;
            sh.m_weight = 0.5f;
            sh.m_value = 0;
            sh.m_teleportable = true;
            sh.m_questItem = false;
            sh.m_food = 0f; sh.m_foodStamina = 0f; sh.m_foodEitr = 0f;
            sh.m_consumeStatusEffect = null;
            FateStoneLook.Dress(go); // violet glow, light and sparks
            Sprite icon = AbilityIcons.Get("stone"); // the user's picture (assets/icons/stone.png, or config/RavenWatch/icons/stone.png)
            if (icon != null) sh.m_icons = new Sprite[] { icon };
            return go;
        }

        // the post's blank sheet: bought from Haldor one at a time, one goes with every letter (Post)
        private static GameObject BuildLetter(GameObject scraps)
        {
            GameObject go = Clone(scraps, LetterPrefab);
            if (go == null) return null;
            ItemDrop.ItemData.SharedData sh = go.GetComponent<ItemDrop>().m_itemData.m_shared;
            sh.m_name = Lang.Word("rw_item_letter", "Пергамент");
            sh.m_description = Lang.Word("rw_item_letter_desc", "Чистий аркуш для листа. Ворон понесе його будь-кому на сервері: книга воронів (G → «Ворони») → конверт у її кутку. Один лист — один пергамент.");
            sh.m_itemType = ItemDrop.ItemData.ItemType.Material;
            sh.m_maxStackSize = 50;
            sh.m_weight = 0.1f;
            sh.m_value = 0;
            sh.m_teleportable = true;
            sh.m_questItem = false;
            Sprite icon = AbilityIcons.Get("letter"); // assets/icons/letter.png (or config/RavenWatch/icons/letter.png)
            if (icon != null) sh.m_icons = new Sprite[] { icon };
            return go;
        }

        // Odin's feather: an errand's (CarryStep) — used from the bag, it is laid down; no portal, no post, no price
        private static GameObject BuildFeather(GameObject feathers)
        {
            GameObject go = Clone(feathers, FeatherPrefab);
            if (go == null) return null;
            ItemDrop.ItemData.SharedData sh = go.GetComponent<ItemDrop>().m_itemData.m_shared;
            sh.m_name = Lang.Word("rw_item_feather", "Перо Одіна");
            sh.m_description = Lang.Word("rw_item_feather_desc", "Перо з крила Одінового ворона. Донеси його туди, куди кличе доручення, і там використай (правий клік). Крізь портал не пройде.");
            sh.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            sh.m_maxStackSize = 1;
            sh.m_weight = 0.1f;
            sh.m_value = 0;
            sh.m_teleportable = false;
            sh.m_toolTier = 1000; // (the game's portal check: such a thing never goes through, even where all things may)
            sh.m_questItem = false;
            sh.m_food = 0f; sh.m_foodStamina = 0f; sh.m_foodEitr = 0f;
            sh.m_consumeStatusEffect = null;
            FateStoneLook.Dress(go, FateStoneLook.Odin); // black, a faint gold glow, a gold light and sparks
            Sprite icon = AbilityIcons.Get("feather"); // assets/icons/feather.png (tools/FeatherIcon.cs), or config/RavenWatch/icons/feather.png
            if (icon != null) sh.m_icons = new Sprite[] { icon };
            return go;
        }

        internal static bool IsFeather(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == FeatherPrefab;
        }

        private static bool Stamped(ItemDrop.ItemData it, string stamp)
        {
            string s;
            return it.m_customData != null && it.m_customData.TryGetValue(FeatherStampKey, out s) && s == stamp;
        }

        // the feather just put in the bag (the one without a stamp yet) gets this one
        internal static void StampNew(Player p, string stamp)
        {
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
                if (IsFeather(it) && (it.m_customData == null || !it.m_customData.ContainsKey(FeatherStampKey))) { it.m_customData[FeatherStampKey] = stamp; return; }
        }

        // how many of Odin's feathers with this stamp the bag holds
        internal static int Feathers(Player p, string stamp)
        {
            int n = 0;
            if (p != null) foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) if (IsFeather(it) && Stamped(it, stamp)) n += it.m_stack;
            return n;
        }

        // out of the bag: all of them, or (keep) all but the one that counts now; true when there were any
        internal static bool DropFeathers(Player p) { return DropFeathers(p, null); }
        internal static bool DropFeathers(Player p, string keep)
        {
            if (p == null) return false;
            List<ItemDrop.ItemData> all = p.GetInventory().GetAllItems().FindAll(it => IsFeather(it) && (keep == null || !Stamped(it, keep)));
            foreach (ItemDrop.ItemData it in all) p.GetInventory().RemoveItem(it);
            return all.Count > 0;
        }

        private static void BuildAll(Func<string, GameObject> find)
        {
            GameObject feathers = find("Feathers");
            if (Hugin == null) Hugin = BuildRaven(feathers, false);
            if (Munin == null) Munin = BuildRaven(feathers, true);
            if (Reroll == null) Reroll = BuildReroll(find("Thunderstone") ?? feathers);
            if (Letter == null) Letter = BuildLetter(find("LeatherScraps") ?? feathers);
            if (Feather == null) Feather = BuildFeather(feathers);
#if RAVEN_NPC
            NpcKeepsake.Build(find, Clone); // (what people in need lose: NpcRescue)
#endif
        }

        private static IEnumerable<GameObject> Ours()
        {
            foreach (GameObject g in new GameObject[] { Hugin, Munin, Reroll, Letter, Feather }) yield return g;
#if RAVEN_NPC
            foreach (GameObject g in NpcKeepsake.All()) yield return g;
#endif
        }

        // the game's network prefab list: dropped ravens must exist for everyone
        internal static void RegisterNet(ZNetScene scene)
        {
            if (!Enabled || scene == null || scene.m_prefabs == null) return;
            BuildAll(n => scene.m_prefabs.Find(g => g != null && g.name == n));
            foreach (GameObject g in Ours())
                if (g != null && !scene.m_prefabs.Contains(g)) scene.m_prefabs.Add(g);
        }

        // the item database: in the main menu too, or the character screen would lose the ravens
        internal static void RegisterItems(ObjectDB db)
        {
            if (!Enabled || db == null || db.m_items == null || db.m_items.Count == 0) return;
            Power.NoticePov();
            BuildAll(n => db.GetItemPrefab(n));
            bool added = false;
            foreach (GameObject g in Ours())
                if (g != null && !db.m_items.Contains(g)) { db.m_items.Add(g); added = true; }
            if (added && UpdateRegisters != null) UpdateRegisters.Invoke(db, null);
        }

        // an object's name is a new string every time it is read: the answer is kept per prefab instead
        private static readonly Dictionary<GameObject, int> Kinds = new Dictionary<GameObject, int>(); // 0 no raven, 1 Hugin, 2 Munin

        internal static bool IsRaven(ItemDrop.ItemData item, out bool munin)
        {
            munin = false;
            GameObject prefab = item != null ? item.m_dropPrefab : null;
            if (prefab == null) return false;
            int kind;
            if (!Kinds.TryGetValue(prefab, out kind))
            {
                string n = prefab.name;
                kind = n == HuginPrefab ? 1 : n == MuninPrefab ? 2 : 0;
                Kinds[prefab] = kind;
            }
            munin = kind == 2;
            return kind != 0;
        }

        internal static bool IsReroll(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == RerollPrefab;
        }

        // the worn ravens (Hugin on the right shoulder, Munin on the left, RavenSeat): looked up once a frame (and again after anything is put on or taken off)
        private static int _wornFrame = -1;
        private static Player _wornBy;
        private static ItemDrop.ItemData _wornHugin, _wornMunin;

        internal static void ForgetWorn() { _wornFrame = -1; }

        // a fresh raven item of that kind (its own data, never the prefab's) — Odin's gift, your own raven; null: none made yet
        internal static ItemDrop.ItemData NewRaven(bool munin)
        {
            GameObject prefab = munin ? Munin : Hugin;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null) return null;
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = 1;
            item.m_customData = new Dictionary<string, string>(item.m_customData); // (never the prefab's own)
            return item;
        }

        internal static void Worn(Player p, out ItemDrop.ItemData hugin, out ItemDrop.ItemData munin)
        {
            hugin = null; munin = null;
            if (p == null) return;
            if (_wornFrame == Time.frameCount && ReferenceEquals(_wornBy, p)) { hugin = _wornHugin; munin = _wornMunin; return; }
            RavenSeat.SeatedOf(p, out hugin, out munin); // in the bag with the mark, or off the bag on the shoulders
            _wornFrame = Time.frameCount; _wornBy = p; _wornHugin = hugin; _wornMunin = munin;
        }

        // ---------------------------------------------------------------- one raven, one cell
        // Each raven is its own: its bonus, drawback and strength live in the item. Something in the game set
        // (another mod) may make them stack, and then a second raven melts into the first and its own data is lost.
        // So the stack size is held at 1, and a stack that formed anyway is split again; what melted cannot be
        // brought back, so each raven split off rolls anew and starts with the starting strength.
        private static bool _stackLogged;

        internal static void KeepSingle(Player p)
        {
            foreach (GameObject g in new GameObject[] { Hugin, Munin })
            {
                ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
                if (d == null || d.m_itemData.m_shared.m_maxStackSize == 1) continue;
                if (!_stackLogged && Plugin.Instance != null) { _stackLogged = true; Plugin.Instance.Log("Ravens: something set their stack size to " + d.m_itemData.m_shared.m_maxStackSize + "; held at 1."); }
                d.m_itemData.m_shared.m_maxStackSize = 1;
            }
            if (p == null) return;
            Inventory inv = p.GetInventory();
            List<ItemDrop.ItemData> split = null;
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                bool munin;
                if (!IsRaven(item, out munin)) continue;
                if (item.m_shared.m_maxStackSize != 1) item.m_shared.m_maxStackSize = 1;
                if (item.m_stack <= 1) continue;
                if (split == null) split = new List<ItemDrop.ItemData>();
                for (int i = 1; i < item.m_stack; i++)
                {
                    ItemDrop.ItemData one = item.Clone();
                    one.m_stack = 1;
                    one.m_equipped = false;
                    one.m_customData = new Dictionary<string, string>(); // a fresh raven: rolled again, starting strength
                    split.Add(one);
                }
                item.m_stack = 1;
            }
            if (split == null) return;
            foreach (ItemDrop.ItemData one in split)
            {
                if (inv.AddItem(one)) continue;
                ItemDrop.DropItem(one, 1, p.transform.position + p.transform.forward + Vector3.up, p.transform.rotation);
            }
            if (Plugin.Instance != null) Plugin.Instance.Log("Ravens: a stack of " + (split.Count + 1) + " ravens split into single ones.");
            RavenMsg.Center(p, Lang.T("Ворони не терплять тісноти —\nрозсадив їх по клітинках."));
        }

        // The raven's own model as its icon, as soon as the game has the ravens loaded; the mod's picture until then.
        // The item's look is shared by every copy of it, so the inventory, the trader and the tooltips all change at once.
        internal static void TickIcons()
        {
            if (!_filesRead) ReadFiles();
            if ((_huginModel != null && _muninModel != null) || _modelTries >= 6 || Time.realtimeSinceStartup < _nextModelTry) return;
            if (Player.m_localPlayer == null) return; // the ravens' models are there once in the world
            if (_nextModelTry <= 0f) { _nextModelTry = Time.realtimeSinceStartup + 5f; return; } // let the worn ravens land on the shoulder first
            _nextModelTry = Time.realtimeSinceStartup + 5f;
            _modelTries++;
            RenderIcons();
        }

        // the console's "raven icons": try again now, and say how it went
        internal static string RetryIcons()
        {
            _huginModel = null; _muninModel = null; _modelTries = 0;
            string files = ReadFiles();
            return files + RenderIcons();
        }

        private static string RenderIcons()
        {
            string report = "";
            if (_huginFile != null) _huginModel = _huginFile; // your picture: nothing to photograph
            if (_muninFile != null) _muninModel = _muninFile;
            if (_huginModel == null) { _huginModel = IconRender.Render(false); report += "Hugin: " + IconRender.Why; if (_huginModel != null) UseIcon(Hugin, _huginModel); }
            if (_muninModel == null) { _muninModel = IconRender.Render(true); report += (report.Length > 0 ? "; " : "") + "Munin: " + IconRender.Why; if (_muninModel != null) UseIcon(Munin, _muninModel); }
            if (report.Length > 0 && Plugin.Instance != null) Plugin.Instance.Log("Raven icons from the models: " + report);
            return report;
        }

        internal static void UseIcon(GameObject prefab, Sprite icon) // (also the Workshop items: WsItems)
        {
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop != null) drop.m_itemData.m_shared.m_icons = new Sprite[] { icon };
            // items already in inventories may hold their own copy of the shared look
            Player p = Player.m_localPlayer;
            if (p != null && prefab != null)
            {
                foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems())
                    if (item.m_dropPrefab != null && item.m_dropPrefab.name == prefab.name) item.m_shared.m_icons = new Sprite[] { icon };
                foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p))
                    if (item.m_dropPrefab != null && item.m_dropPrefab.name == prefab.name) item.m_shared.m_icons = new Sprite[] { icon };
            }
            if (p != null) InvRedraw.Changed(p.GetInventory()); // the open inventory redraws
        }

        internal static string IconDir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "icons"); } }

        // hugin.png / munin.png from the icons folder; the folder is made with a note on first start
        private static string ReadFiles()
        {
            _filesRead = true;
            string report = "";
            try
            {
                string dir = IconDir;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                // what the folder is for: README.txt (an English name, as every file of the mod; the words in your
                // language), written while missing; the old Ukrainian-named one (before 2.4.0) goes
                string old = Path.Combine(dir, "ПРОЧИТАЙ.txt"), readme = Path.Combine(dir, "README.txt");
                if (File.Exists(old)) File.Delete(old);
                if (!File.Exists(readme))
                    File.WriteAllText(readme,
                        Lang.T("Свої іконки воронів: поклади сюди hugin.png і/або munin.png.\nКвадратна PNG із прозорим фоном, найкраще 128x128 (можна 256x256).\nПідхоплюється при вході у світ.\nНема файлу — мод бере свої вбудовані іконки.\n"), new System.Text.UTF8Encoding(false));
                // your file in the folder, else the mod's own picture (assets/icons/hugin.png, munin.png)
                bool ownHugin = File.Exists(Path.Combine(dir, "hugin.png")), ownMunin = File.Exists(Path.Combine(dir, "munin.png"));
                _huginFile = AbilityIcons.Get("hugin");
                _muninFile = AbilityIcons.Get("munin");
                if (_huginFile != null) { _huginModel = _huginFile; UseIcon(Hugin, _huginFile); report += "Hugin: " + (ownHugin ? "your" : "built-in") + " hugin.png; "; }
                if (_muninFile != null) { _muninModel = _muninFile; UseIcon(Munin, _muninFile); report += "Munin: " + (ownMunin ? "your" : "built-in") + " munin.png; "; }
            }
            catch (Exception e) { report += "icons folder: " + e.Message + "; "; }
            if (report.Length > 0 && Plugin.Instance != null) Plugin.Instance.Log("Raven icons: " + report);
            return report;
        }


        internal static Sprite Icon(bool munin)
        {
            Sprite own = munin ? _muninFile : _huginFile;
            if (own != null) return own;
            Sprite model = munin ? _muninModel : _huginModel;
            if (model != null) return model;
            if (munin ? _muninIcon != null : _huginIcon != null) return munin ? _muninIcon : _huginIcon;
            try
            {
                using (Stream s = typeof(RavenItem).Assembly.GetManifestResourceStream("RavenWatch.icon.png"))
                {
                    if (s == null || LoadImage == null) return null;
                    byte[] data = new byte[s.Length];
                    s.Read(data, 0, data.Length);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.hideFlags = HideFlags.HideAndDontSave;
                    if (!(bool)LoadImage.Invoke(null, new object[] { tex, data })) return null;
                    if (munin)
                    {
                        // Munin's icon: the same raven, gone blood red
                        Color32[] px = tex.GetPixels32();
                        for (int i = 0; i < px.Length; i++)
                        {
                            int grey = (px[i].r + px[i].g + px[i].b) / 3;
                            px[i] = new Color32((byte)Mathf.Min(255, grey + 90), (byte)(grey / 3), (byte)(grey / 3), px[i].a);
                        }
                        tex.SetPixels32(px);
                        tex.Apply();
                    }
                    Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    sp.name = munin ? "RavenWatch_MuninIcon" : "RavenWatch_HuginIcon";
                    sp.hideFlags = HideFlags.HideAndDontSave;
                    if (munin) _muninIcon = sp; else _huginIcon = sp;
                    return sp;
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven icon could not load: " + e.Message); }
            return null;
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class RavenItemNetPatch
    {
        private static void Prefix(ZNetScene __instance) { RavenItem.RegisterNet(__instance); }
    }

    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class RavenItemDbPatch
    {
        private static void Postfix(ObjectDB __instance) { RavenItem.RegisterItems(__instance); }
    }

    [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
    internal static class RavenItemMenuDbPatch
    {
        private static void Postfix(ObjectDB __instance) { RavenItem.RegisterItems(__instance); }
    }

    // Haldor sells both ravens, the Fate stone, the parchment and our goods for the raven things (WsItems.ForSale)
    [HarmonyPatch(typeof(Trader), "GetAvailableItems")]
    internal static class RavenTraderPatch
    {
        private static void Postfix(Trader __instance, List<Trader.TradeItem> __result)
        {
            if (!RavenItem.Enabled || __result == null) return;
            bool haldor = __instance.gameObject.name.IndexOf("Haldor", StringComparison.OrdinalIgnoreCase) >= 0
                || (__instance.m_name != null && __instance.m_name.IndexOf("haldor", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!haldor) return;
            Offer(__result, RavenItem.Hugin, RavenItem.HuginPrice);
            Offer(__result, RavenItem.Munin, RavenItem.MuninPrice);
            Offer(__result, RavenItem.Reroll, RavenItem.RerollPrice);
            Offer(__result, RavenItem.Letter, RavenItem.LetterPrice); // (one at a time)
            foreach (KeyValuePair<GameObject, int> g in WsItems.ForSale(Player.m_localPlayer)) Offer(__result, g.Key, g.Value); // our goods, each after its boss
        }

        // StoreGui finds the selected entry by reference (GetSelectedItemIndex), so each offer must be the same
        // object every time: a fresh one per call lost the selection and its highlight
        private static readonly Dictionary<string, Trader.TradeItem> Made = new Dictionary<string, Trader.TradeItem>();

        private static void Offer(List<Trader.TradeItem> list, GameObject prefab, int price)
        {
            if (prefab == null || price <= 0) return;
            foreach (Trader.TradeItem t in list) if (t.m_prefab != null && t.m_prefab.name == prefab.name) return;
            ItemDrop drop = prefab.GetComponent<ItemDrop>();
            Trader.TradeItem item;
            if (Made.TryGetValue(prefab.name, out item) && item.m_prefab == drop)
            {
                item.m_price = price;
                list.Add(item);
                return;
            }
            item = new Trader.TradeItem();
            Made[prefab.name] = item;
            item.m_prefab = drop;
            item.m_stack = 1;
            item.m_price = price;
            item.m_requiredGlobalKey = "";
            item.m_buyKey = "";
            item.m_incrementKey = "";
            item.m_name = "";
            item.m_tooltip = "";
            item.m_buyPlayerEffects = new EffectList();
            list.Add(item);
        }
    }

    // a raven's own bonus and drawback under its description; at the trader they are still a secret. Short (the user
    // 2026-11-02): who it is, trust, strength, blessing and burden — the gifts, how strength grows and the passives live in
    // the book (its «Сила» tip) and in the shoulder effect's tooltip
    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip", new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    internal static class RavenTooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            bool munin;
            if (!RavenItem.IsRaven(item, out munin)) return;
            string rank = Perks.Rolled(item) ? "\n\n" + Trust.RankLine(munin, item) : ""; // a bought raven: how far it trusts you
            string owner = Trust.OwnerName(item);
            if (!string.IsNullOrEmpty(owner)) rank += Lang.F("\nВірний: {0}", owner); // whose raven it is
            Nicks.Nick nick = Perks.Rolled(item) ? Nicks.Current(item) : null;
            if (nick != null) rank += Lang.F("\nПрізвисько: <color={0}>{1}</color>", Nicks.Hex(nick.K), nick.Name);
            if (Perks.Rolled(item)) rank += Lang.F("\nСила: <color=orange>{0}/{1}</color>", Mathf.FloorToInt(Power.Get(item)), Mathf.RoundToInt(Trust.Strength(item)));
            __result += rank + Sleeping(item) + Perks.Describe(item);
        }

        // asleep or not, at a glance: with many ravens in the chest it is easy to lose track (one line, no "name: value")
        private static string Sleeping(ItemDrop.ItemData item)
        {
            Trust.Step wake = Trust.Steps().Count > 0 ? Trust.Steps()[0] : null;
            string how = wake != null ? Lang.F(" ({0} ×{1} біля Жертовних каменів)", wake.Name, wake.Count) : "";
            if (!Perks.Rolled(item)) return Lang.F("\n\n<color=#9a9a9a>Спить — розбуди ритуалом{0}, тоді відкриються благо й тягар.</color>", how) // at the trader
                + "\n\n<color=#b8a27a><i>" + Lang.T("Уламок чиєїсь пам'яті: ворони тих, хто продав себе Хель, блукають без господаря, а Хальдор їх ловить.") + "</i></color>"; // (story-lore.md: why Haldor sells ravens)
            if (Trust.Active(item)) return "";
            return Lang.F("\n\n<color=#FF5A5A><b>Спить</b></color> — розбуди ритуалом{0}.", how);
        }
    }

    // using the Fate stone: roll a worn raven again. Taken at the right click itself (UseItem, before every other
    // mod: some stop a consumable there, and then the game's ConsumeItem never comes); ConsumeItem stays as a fallback.
    // The click asks which raven (RavenBook.ShowRavenPick, as Odin's call): both may sit on you; the stone goes only
    // when one is picked.
    internal static class FateStone
    {
        internal static bool Use(Player p, Inventory inventory, ItemDrop.ItemData item)
        {
            if (p == null || p != Player.m_localPlayer || FateRite.Busy) return false; // (the last stone still at work: this one stays in the bag)
            if (Trust.Worn(false) == null && Trust.Worn(true) == null)
            {
                RavenMsg.Center(p, Lang.T("Посади на плече ворона,\nякому хочеш змінити долю."));
                return false;
            }
            if (InventoryGui.instance != null && InventoryGui.IsVisible()) InventoryGui.instance.Hide();
            RavenBook.ShowRavenPick(new RavenPick
            {
                Title = Lang.T("Камінь долі"),
                Say = Lang.T("Підніми камінь до ворона — і доля його перекинеться: нове благо й новий тягар."),
                Sub = Lang.T("Старої долі вже не повернути. Обери, кому змінити:"),
                WhyNot = munin => WhyNot(p, munin),
                Note = munin => Perks.Rolled(Trust.Worn(munin)) ? Perks.Short(Trust.Worn(munin)) : Lang.T("доля ще прихована"),
                Act = munin => Reroll(p, munin),
            });
            return false; // (spent only when a raven is picked)
        }

        // why this raven's fate cannot be changed now; null: it can
        private static string WhyNot(Player p, bool munin)
        {
            ItemDrop.ItemData raven = Trust.Worn(munin);
            if (raven == null) return Lang.T("не на плечі");
            if (Trust.Stranger(raven, p)) return Lang.F("не твій: долю йому міняє лише {0}", Trust.OwnerName(raven));
            return null;
        }

        // the raven picked: its fate rolled again, the stone spent, the rite shown; null, or why not
        private static string Reroll(Player p, bool m)
        {
            if (FateRite.Busy) return null;
            string why = WhyNot(p, m);
            if (why != null) return RavenItem.Name(m) + ": " + why + ".";
            ItemDrop.ItemData stone = null;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) if (RavenItem.IsReroll(it)) { stone = it; break; }
            if (stone == null) return Lang.T("Каменя долі в сумці нема.");
            ItemDrop.ItemData raven = Trust.Worn(m);
            string was = Perks.Short(raven);
            Perks.Reroll(raven, m);
            Backup.Save(p, raven); // the copy follows the new fate at once: no going back to the old one
            p.GetInventory().RemoveOneItem(stone);
            if (Plugin.Instance != null) Plugin.Instance.Log("Fate stone: " + RavenItem.Name(m) + " " + was + " -> " + Perks.Short(raven));
            FateRite.Play(p, m, raven, RavenItem.Name(m) + ": " + Perks.Short(raven)); // shown: the stone raised, the raven turned (the new fate told at its end; a golden blessing its own way)
            return null;
        }
    }

    [HarmonyPatch(typeof(Humanoid), "UseItem")]
    internal static class FateStoneUsePatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Humanoid __instance, Inventory __0, ItemDrop.ItemData __1)
        { try {
            if (!RavenItem.IsReroll(__1)) return true;
            FateStone.Use(__instance as Player, __0, __1);
            return false;
        } catch (Exception e) { PatchGuard.Fail("FateStoneUsePatch", e); return true; } }
    }

    [HarmonyPatch(typeof(Player), "ConsumeItem")]
    internal static class RavenRerollPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Player __instance, Inventory inventory, ItemDrop.ItemData item, ref bool __result)
        {
            if (RavenItem.IsFeather(item)) { __result = FeatherUsePatch.Use(__instance as Player); return false; }
            if (!RavenItem.IsReroll(item)) return true;
            __result = FateStone.Use(__instance, inventory, item);
            return false;
        }
    }

    // Odin's feather used from the bag: laid down at its errand's place (Errands.Act, as the J circle and the book)
    [HarmonyPatch(typeof(Humanoid), "UseItem")]
    internal static class FeatherUsePatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData __1)
        { try {
            if (!RavenItem.IsFeather(__1)) return true;
            Use(__instance as Player);
            return false;
        } catch (Exception e) { PatchGuard.Fail("FeatherUsePatch", e); return true; } }

        internal static bool Use(Player p)
        {
            if (p == null || p != Player.m_localPlayer) return false;
            ErrandRun r = Errands.Of(p);
            if (r != null && r.Def != null && r.Current is CarryStep) return Errands.Act(p); // (not another step's deed: a watch, a guide)
            RavenMsg.Center(p, Lang.T("Зараз перо Одіна ні до чого."));
            return false;
        }
    }

    // The open inventory redrawn (the game's private Inventory.Changed): Valheim 1.0 has Changed(bool success, bool
    // cheatedStateChanged), older games Changed(); the one with all-bool parameters and the most of them is the game's
    // own (a compatibility mod may add the old one beside it), called with its defaults (false).
    internal static class InvRedraw
    {
        private static MethodInfo _m;
        private static object[] _args;
        private static bool _looked;

        internal static void Changed(Inventory inv)
        {
            if (inv == null) return;
            if (!_looked)
            {
                _looked = true;
                foreach (MethodInfo m in typeof(Inventory).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name != "Changed") continue;
                    ParameterInfo[] ps = m.GetParameters();
                    bool bools = true;
                    foreach (ParameterInfo p in ps) if (p.ParameterType != typeof(bool)) bools = false;
                    if (bools && (_m == null || ps.Length > _m.GetParameters().Length)) _m = m;
                }
                if (_m != null) { _args = new object[_m.GetParameters().Length]; for (int i = 0; i < _args.Length; i++) _args[i] = false; }
                else if (Plugin.Instance != null) Plugin.Instance.Log("Inventory redraw: the game's Inventory.Changed not found");
            }
            if (_m == null) return;
            try { _m.Invoke(inv, _args); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Inventory redraw failed: " + e.Message); }
        }
    }
}
#endif
