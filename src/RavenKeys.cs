#if RAVEN_ITEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Keys for the Book of ravens (B), the gifts and orders circles (Q, J) and, if wished, each gift, order, trick and perch.
    // They only listen: nothing is taken away from the game or other mods. A key used elsewhere too
    // does both things; the book shows who else uses it.
    internal static class RavenKeys
    {
        internal const string BookId = "book";
        internal const string OrdersId = "orders"; // the orders menu (OrdersMenu), J unless changed
        internal const string GiftsId = "gifts";   // the gifts circle (OrdersMenu, its other face), Q unless changed
        // an order on its own key (set in the orders circle: right click it); "orderH1" = Hugin's «Поруч»
        internal static readonly int[] KeyedOrders = { Orders.Follow, Orders.Circle, Orders.Scout };
        internal static string OrderId(bool munin, int order) { return (munin ? "orderM" : "orderH") + order; }
        private static readonly string[] OrderEnglish = { "", "beside", "circle", "fly around" };
        internal const string PerchHId = "perchH", PerchMId = "perchM", PerchBothId = "perchBoth"; // to the nearest perch and back (Perches.Order)
        internal const string HereHId = "hereH", HereMId = "hereM"; // «Туди»: what fits where you look (Perches.OrderHere, SitHere)
        // a trick on its own key (set in the orders menu: right click the trick); "trickH4" = Hugin's barrel roll
        internal static string TrickId(bool munin, int order) { return (munin ? "trickM" : "trickH") + order; }
        private static readonly string[] TrickEnglish = { "", "", "", "", "barrel roll", "loop", "dive", "figure eight", "candle", "falling leaf", "Immelmann", "hammerhead", "low pass", "waves" };
        private static readonly Dictionary<string, ConfigEntry<KeyboardShortcut>> Entries = new Dictionary<string, ConfigEntry<KeyboardShortcut>>();
        private static readonly KeyCode[] Modifiers = { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.AltGr };

        internal static void Bind(ConfigFile cfg)
        {
            const string sec = "6. Keys";
            const string note = " Easiest to set in game: right click the piece in the gifts circle (Q) or the orders circle (J). Never takes a key away from the game or other mods: a key used elsewhere too does both.";
            foreach (RavenAbility a in RavenAbilities.All)
                if (!a.Passive && !RavenAbilities.Stub(a)) Entries[a.Id] = Plugin.Instance.Hide(sec, (a.Munin ? "Munin " : "Hugin ") + (a.Id == "mthought" ? "thought" : a.Id), KeyboardShortcut.Empty, "Key for " + a.Name + "." + note);
            Entries[BookId] = cfg.Bind(sec, "Book of ravens", new KeyboardShortcut(KeyCode.B), "Key that opens the Book of ravens directly (B unless changed)." + note);
            // configs from before B was the book's key keep their empty value: given B once, a later choice is kept
            ConfigEntry<int> rev = Plugin.Instance.Hide(sec, "Keys revision", 0, "Internal: which default keys this file already got. Do not change.");
            if (rev.Value < 1)
            {
                if (Entries[BookId].Value.MainKey == KeyCode.None) Entries[BookId].Value = new KeyboardShortcut(KeyCode.B);
                rev.Value = 1;
            }
            Entries[OrdersId] = cfg.Bind(sec, "Raven orders", new KeyboardShortcut(KeyCode.J), "Key for the orders ring (fly beside, circle, scout, tricks, come back): hold it, point, let go; a short tap leaves it open. Also in G → Ravens → Orders." + note);
            Entries[GiftsId] = cfg.Bind(sec, "Raven gifts", new KeyboardShortcut(KeyCode.Q), "Key for the gifts circle (strikes, scouting, light, scream...; its centre opens the orders): hold it, point, let go; a short tap leaves it open. Gives way while you build (the game and build mods use Q there) and while you look at something that has its own use on this key (renaming a tamed animal with Zen mods' Q)." + note);
            foreach (bool munin in new bool[] { false, true })
                foreach (int o in KeyedOrders)
                    Entries[OrderId(munin, o)] = Plugin.Instance.Hide(sec, (munin ? "Munin" : "Hugin") + " order " + OrderEnglish[o], KeyboardShortcut.Empty, "Key for the order «" + Lang.En(Orders.NamesSrc[o]) + "». Easiest to set in game: J, right click the order." + note);
            Entries[PerchHId] = Plugin.Instance.Hide(sec, "Hugin perch", KeyboardShortcut.Empty, "Key that sends Hugin to the nearest perch (20 m, seen) or calls him back from it." + note);
            Entries[PerchMId] = Plugin.Instance.Hide(sec, "Munin perch", KeyboardShortcut.Empty, "Key that sends Munin to the nearest perch (20 m, seen) or calls him back from it." + note);
            Entries[HereHId] = Plugin.Instance.Hide(sec, "Hugin there", KeyboardShortcut.Empty, "Key for the context order (There): what fits where you look - Hugin sits on the player or tamed animal you look at, fetches the small thing, sits on that perch or on top of that building; or comes back from where he sits." + note);
            Entries[HereMId] = Plugin.Instance.Hide(sec, "Munin there", KeyboardShortcut.Empty, "Key for the context order (There): what fits where you look - Munin sits on the player or tamed animal you look at, fetches the small thing, sits on that perch or on top of that building; or comes back from where he sits." + note);
            Entries[PerchBothId] = Plugin.Instance.Hide(sec, "Both ravens perch", KeyboardShortcut.Empty, "Key that sends both ravens to the nearest perches, or calls back those sitting there." + note);
            foreach (bool munin in new bool[] { false, true })
                foreach (int o in Orders.Tricks)
                    Entries[TrickId(munin, o)] = Plugin.Instance.Hide(sec, (munin ? "Munin" : "Hugin") + " trick " + TrickEnglish[o], KeyboardShortcut.Empty, "Key for the trick «" + Lang.En(Orders.NamesSrc[o]) + "». Easiest to set in game: J, right click the trick." + note);
        }

        internal static KeyboardShortcut Get(string id)
        {
            ConfigEntry<KeyboardShortcut> e;
            return Entries.TryGetValue(id, out e) ? e.Value : KeyboardShortcut.Empty;
        }

        internal static void Set(string id, KeyboardShortcut k)
        {
            ConfigEntry<KeyboardShortcut> e;
            if (Entries.TryGetValue(id, out e)) e.Value = k;
            ClashCache.Clear();
        }

        // the book's «Прибрати всі клавіші»: every key of the gifts, orders, tricks and perches; the book's, the gifts
        // circle's and the orders circle's own keys stay
        internal static void ClearRaven()
        {
            foreach (KeyValuePair<string, ConfigEntry<KeyboardShortcut>> e in Entries)
                if (e.Key != BookId && e.Key != OrdersId && e.Key != GiftsId) e.Value.Value = KeyboardShortcut.Empty;
            ClashCache.Clear();
        }

        internal static string Name(string id)
        {
            if (id == BookId) return Lang.T("Книга воронів");
            if (id == OrdersId) return Lang.T("Накази воронам");
            if (id == GiftsId) return Lang.T("Дари воронів");
            foreach (bool munin in new bool[] { false, true }) foreach (int o in KeyedOrders) if (id == OrderId(munin, o)) return (munin ? Lang.T("Мунін: ") : Lang.T("Хугін: ")) + Orders.Names[o];
            if (id == PerchHId) return Lang.T("Хугін: жердина");
            if (id == PerchMId) return Lang.T("Мунін: жердина");
            if (id == PerchBothId) return Lang.T("Обидва: жердина");
            if (id == HereHId) return Lang.T("Хугін: туди");
            if (id == HereMId) return Lang.T("Мунін: туди");
            foreach (bool munin in new bool[] { false, true }) foreach (int o in Orders.Tricks) if (id == TrickId(munin, o)) return (munin ? Lang.T("Мунін: ") : Lang.T("Хугін: ")) + Orders.Names[o];
            foreach (RavenAbility a in RavenAbilities.All) if (a.Id == id) return a.Name;
            return id;
        }

        // ---------------------------------------------------------------- pressing

        private static readonly List<Action> Hit = new List<Action>();

        // every key with what it does: the gifts, the keyed orders and the tricks of both ravens, made once (looked at
        // every frame: nothing new is made for that)
        private sealed class KeyAct { internal string Id; internal Action Act; }
        private static List<KeyAct> _acts;
        private static List<KeyAct> Acts()
        {
            if (_acts != null) return _acts;
            _acts = new List<KeyAct>();
            foreach (RavenAbility a in RavenAbilities.All)
            {
                if (a.Passive) continue;
                RavenAbility ab = a;
                _acts.Add(new KeyAct { Id = a.Id, Act = () => RavenSkills.Use(ab) });
            }
            for (int m = 0; m < 2; m++)
            {
                bool mm = m == 1;
                foreach (int o in KeyedOrders) { int order = o; _acts.Add(new KeyAct { Id = OrderId(mm, o), Act = () => GiveOrder(mm, order) }); }
                foreach (int o in Orders.Tricks) { int order = o; _acts.Add(new KeyAct { Id = TrickId(mm, o), Act = () => GiveOrder(mm, order) }); }
            }
            return _acts;
        }

        private static void GiveOrder(bool munin, int order) { Player pl = Player.m_localPlayer; if (pl != null) Orders.Give(pl, munin, order); }

        internal static void Update()
        {
            if (Entries.Count == 0 || RavenBook.Capturing || RavenBook.JustBound || OrdersMenu.Capturing || OrdersMenu.JustBound || Typing()) return;
            // no raven of its own yet: nobody to send, the keys stay quiet (G → Ворони still opens everything); what is open closes on its key
            Player own = Player.m_localPlayer;
            if (!Trust.Bonded(own))
            {
                if (RavenBook.IsOpen && Pressed(Get(BookId))) RavenBook.Close();
                else if (Pressed(Get(BookId)) && RavenBook.Given(own) && Free() && !own.InPlaceMode() && !GivesWay(Get(BookId).MainKey)) RavenBook.Open(); // the book Odin gave with his trial
                else if (OrdersMenu.IsOpen && (Pressed(Get(OrdersId)) || Pressed(Get(GiftsId)))) OrdersMenu.CloseNow();
                return;
            }
            // a building tool in hand: B, J and Q are the game's and other mods' (Path of Valheiman's Hugin flight is on B there)
            bool building = own != null && own.InPlaceMode() && !RavenBook.IsOpen && !OrdersMenu.IsOpen;
            // with the gifts or orders circle open (it poses as the trader's window, so not Free): it closes, the book opens
            if (Pressed(Get(BookId)) && OrdersMenu.IsOpen && !RavenBook.IsOpen) { OrdersMenu.CloseNow(); RavenBook.Open(); return; }
            if (Pressed(Get(BookId)) && (RavenBook.IsOpen || (Free() && !building && !GivesWay(Get(BookId).MainKey)))) { RavenBook.Toggle(); return; }
            if (Pressed(Get(OrdersId)) && (OrdersMenu.IsOpen || (Free() && !building && !GivesWay(Get(OrdersId).MainKey)))) { OrdersMenu.OpenByKey(Get(OrdersId).MainKey, false); return; } // again: closes it
            if (Pressed(Get(GiftsId)) && (OrdersMenu.IsOpen || (Free() && !GivesWay(Get(GiftsId).MainKey)))) { OrdersMenu.OpenByKey(Get(GiftsId).MainKey, true); return; }
            Player me = Player.m_localPlayer;
            if (me != null && Free())
            {
                if (Pressed(Get(PerchBothId))) { Perches.OrderBoth(me); return; }
                if (Pressed(Get(PerchHId))) { Perches.Order(me, false); return; }
                if (Pressed(Get(PerchMId))) { Perches.Order(me, true); return; }
                if (Pressed(Get(HereHId))) { Perches.OrderHere(me, false); return; }
                if (Pressed(Get(HereMId))) { Perches.OrderHere(me, true); return; }
            }
            // Shift+V and V both match when Shift+V is pressed: only the one with more modifiers goes
            List<Action> hit = Hit;
            hit.Clear();
            int best = -1;
            List<KeyAct> acts = Acts();
            for (int i = 0; i < acts.Count; i++) Consider(Get(acts[i].Id), acts[i].Act, ref best);
            if (hit.Count == 0 || !Free()) return; // the game busy (chat, a menu, the map...): not now
            foreach (Action a in hit) a();
        }

        // Shift+V and V both match when Shift+V is pressed: only those with the most modifiers go
        private static void Consider(KeyboardShortcut k, Action act, ref int best)
        {
            if (!Pressed(k)) return;
            int mods = Count(k);
            if (mods > best) { best = mods; Hit.Clear(); }
            if (mods == best) Hit.Add(act);
        }

        // The game's input answers with an error for key codes it has no key for; those are simply never pressed
        private static readonly HashSet<KeyCode> Unknown = new HashSet<KeyCode>(); // asked once, failed: never asked again

        internal static bool Down(KeyCode k)
        {
            if (k == KeyCode.None || Unknown.Contains(k)) return false;
            try { return ZInput.GetKeyDown(k, false); } catch { Unknown.Add(k); return false; }
        }

        internal static bool Held(KeyCode k)
        {
            if (k == KeyCode.None || Unknown.Contains(k)) return false;
            try { return ZInput.GetKey(k, false); } catch { Unknown.Add(k); return false; }
        }

        // a key the game's input knows (asking about it does not fail)
        internal static bool Known(KeyCode k)
        {
            try { ZInput.GetKey(k, false); return true; } catch { return false; }
        }

        // the main key went down and the shortcut's modifiers are held; other held keys (running with Shift) do not matter
        private static bool Pressed(KeyboardShortcut k)
        {
            if (!Down(k.MainKey)) return false;
            foreach (KeyCode m in k.Modifiers) if (!Held(m)) return false;
            return true;
        }

        private static int Count(KeyboardShortcut k) { int n = 0; foreach (KeyCode m in k.Modifiers) n++; return n; }

        // the player is playing: not typing, not in a menu, map or inventory
        // a text field has the keys (the book's number fields, the Workshop's names): a letter is a letter there
        private static bool Typing()
        {
            if (WsStage.Ready && GUIUtility.keyboardControl != 0) return true; // an IMGUI field (the Workshop)
            UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
            GameObject sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return false;
            TMPro.TMP_InputField f = sel.GetComponent<TMPro.TMP_InputField>();
            return f != null && f.isFocused;
        }

        // The gifts' key gives way (the other use of it goes alone): while building (the game's own Q flips the build
        // tabs, build mods switch their modes on it) and while looking at something that offers its own action on this
        // very key (Zen mods rename a tamed animal with Q) or can be renamed at all
        private static readonly HashSet<string> Logged = new HashSet<string>(); // hints looked at without our key, once each (to see other mods' formats)
        private static bool GivesWay(KeyCode key)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return false;
            if (p.InPlaceMode()) return true;
            GameObject h = p.GetHoverObject();
            if (h == null) return false;
            Tameable tame = h.GetComponentInParent<Tameable>();
            if (tame != null && tame.IsTamed()) return true; // a tame one can be renamed
            if (SeneaLHolds(key, h)) return true; // SeneaL UI: its key held with E on a smelter, a fire, a torch opens its window
            Hoverable hv = h.GetComponentInParent<Hoverable>();
            string text = null;
            try { text = hv != null ? hv.GetHoverText() : null; } catch { }
            // and what is really shown under the crosshair: other mods add their lines there after the object's own text
            string shown = Hud.instance != null && Hud.instance.m_hoverName != null && Hud.instance.m_hoverName.gameObject.activeInHierarchy ? Hud.instance.m_hoverName.text : null;
            if (!string.IsNullOrEmpty(shown) && shown != text) text = (text ?? "") + "\n" + shown;
            if (string.IsNullOrEmpty(text)) return false;
            string name = KeyName(key);
            if (text.IndexOf("<b>" + name + "</b>", StringComparison.OrdinalIgnoreCase) >= 0) return true; // its hint names this key
            // a keycap drawn as a picture: the key's name inside the tag (<sprite name="Q">)
            if (name.Length > 0 && System.Text.RegularExpressions.Regex.IsMatch(text, @"<[^>]*name\s*=\s*[""']?" + System.Text.RegularExpressions.Regex.Escape(name) + @"[""'\s>]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;
            // other mods draw their hints their own way (SeneaL UI's keycaps: no <b>): the key's name standing alone in the plain text
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", " ");
            if (name.Length > 0 && System.Text.RegularExpressions.Regex.IsMatch(plain, @"(?<![\p{L}\d])" + System.Text.RegularExpressions.Regex.Escape(name) + @"(?![\p{L}\d])")) return true;
            if (Plugin.Instance != null && Logged.Add(h.name)) Plugin.Instance.Log("Raven keys: " + name + " not found in the hint of " + h.name + ": " + plain.Replace("\n", " | "));
            string rename = Localization.instance != null ? Lang.Game("$hud_rename") : null;
            return !string.IsNullOrEmpty(rename) && !rename.StartsWith("[") && text.IndexOf(rename, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // SeneaL UI's [Keys] SmelterWindow (held with the use key on a furnace, a kiln, a fire, a torch, a cooking station):
        // read from its live config, so a key changed in its settings counts at once
        private static ConfigEntryBase _seneaLSmelter;
        private static bool _seneaLLooked;
        private static bool SeneaLHolds(KeyCode key, GameObject h)
        {
            if (!_seneaLLooked)
            {
                _seneaLLooked = true;
                try
                {
                    PluginInfo pi;
                    if (Chainloader.PluginInfos.TryGetValue("seneaL.valheim.ui", out pi) && pi.Instance != null)
                        foreach (ConfigDefinition d in pi.Instance.Config.Keys)
                            if (d.Section == "Keys" && d.Key == "SmelterWindow") { _seneaLSmelter = pi.Instance.Config[d]; break; }
                }
                catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven keys: SeneaL UI keys not read: " + e.Message); }
            }
            if (_seneaLSmelter == null || h == null) return false;
            object v = _seneaLSmelter.BoxedValue;
            KeyCode k = v is KeyboardShortcut ? ((KeyboardShortcut)v).MainKey : v is KeyCode ? (KeyCode)v : KeyCode.None;
            if (k != key) return false;
            return h.GetComponentInParent<Smelter>() != null || h.GetComponentInParent<Fireplace>() != null || h.GetComponentInParent<CookingStation>() != null;
        }

        private static bool Free()
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead() || RavenBook.IsOpen) return false;
            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            if (global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible()) return false;
            if (TextViewer.instance != null && TextViewer.instance.IsVisible()) return false;
            if (Hud.InRadial() || GameCamera.InFreeFly()) return false;
            return true;
        }

        // ---------------------------------------------------------------- labels

        // a key's label by its id, made again only when the key changed (the orders menu asks four times a second)
        private static readonly Dictionary<string, KeyValuePair<KeyboardShortcut, string>> Labels = new Dictionary<string, KeyValuePair<KeyboardShortcut, string>>();
        internal static string LabelOf(string id)
        {
            KeyboardShortcut k = Get(id);
            KeyValuePair<KeyboardShortcut, string> was;
            if (Labels.TryGetValue(id, out was) && was.Key.Equals(k)) return was.Value;
            string l = Label(k);
            Labels[id] = new KeyValuePair<KeyboardShortcut, string>(k, l);
            return l;
        }

        internal static string Label(KeyboardShortcut k)
        {
            if (k.MainKey == KeyCode.None) return "—";
            List<string> parts = new List<string>();
            foreach (KeyCode m in k.Modifiers) parts.Add(KeyName(m));
            parts.Add(KeyName(k.MainKey));
            return string.Join("+", parts.ToArray());
        }

        internal static string KeyName(KeyCode k)
        {
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString();
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "Num " + (int)(k - KeyCode.Keypad0);
            if (k >= KeyCode.Mouse2 && k <= KeyCode.Mouse6) return Lang.F("Миша {0}", ((int)(k - KeyCode.Mouse0) + 1));
            switch (k)
            {
                case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt: case KeyCode.RightAlt: case KeyCode.AltGr: return "Alt";
                case KeyCode.Space: return Lang.T("Пробіл");
                case KeyCode.Return: return "Enter";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
            }
            return k.ToString();
        }

        internal static bool IsModifier(KeyCode k) { return Array.IndexOf(Modifiers, k) >= 0 || k == KeyCode.LeftCommand || k == KeyCode.RightCommand || k == KeyCode.LeftWindows || k == KeyCode.RightWindows; }

        internal static KeyCode[] HeldModifiers(KeyCode except)
        {
            List<KeyCode> held = new List<KeyCode>();
            foreach (KeyCode m in new KeyCode[] { KeyCode.LeftShift, KeyCode.LeftControl, KeyCode.LeftAlt })
            {
                KeyCode right = m == KeyCode.LeftShift ? KeyCode.RightShift : m == KeyCode.LeftControl ? KeyCode.RightControl : KeyCode.RightAlt;
                if (m == except || right == except) continue;
                if (Held(m) || Held(right)) held.Add(m);
            }
            return held.ToArray();
        }

        // ---------------------------------------------------------------- who else uses a key

        private static readonly FieldInfo ButtonsField = AccessTools.Field(typeof(ZInput), "m_buttons");
        private static readonly MethodInfo KeyPath = AccessTools.Method(typeof(ZInput), "KeyCodeToPath");

        // Looking for clashes walks every game button and every config entry of every mod: the answer is kept a little
        // (the book asks for each bound key whenever a page is drawn); a key changed here clears it at once.
        private static readonly Dictionary<string, KeyValuePair<float, List<string>>> ClashCache = new Dictionary<string, KeyValuePair<float, List<string>>>();
        private const float ClashSeconds = 10f;

        internal static List<string> Clashes(string id)
        {
            string key = id + "|" + Get(id).ToString();
            KeyValuePair<float, List<string>> hit;
            if (ClashCache.TryGetValue(key, out hit) && Time.unscaledTime - hit.Key < ClashSeconds && Time.unscaledTime >= hit.Key) return new List<string>(hit.Value);
            List<string> found = FindClashes(id);
            ClashCache[key] = new KeyValuePair<float, List<string>>(Time.unscaledTime, found);
            return new List<string>(found);
        }

        // told once a game, with the real cause (not the wrapper "thrown by the target of an invocation")
        private static bool _toldUnreadable;
        private static void Unreadable(string button, Exception ex)
        {
            if (_toldUnreadable) return;
            _toldUnreadable = true;
            Exception real = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            if (Plugin.Instance != null) Plugin.Instance.Log("Raven keys: game key " + (button ?? "list") + " unreadable (" + real.GetType().Name + ": " + real.Message + "), told once");
        }

        private static List<string> FindClashes(string id)
        {
            List<string> found = new List<string>();
            KeyboardShortcut k = Get(id);
            if (k.MainKey == KeyCode.None) return found;
            KeyCode key = k.MainKey;

            foreach (KeyValuePair<string, ConfigEntry<KeyboardShortcut>> e in Entries)
                if (e.Key != id && e.Value.Value.MainKey == key) found.Add(Lang.F("ця ж книга: {0}", Name(e.Key)));

            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha8) found.Add(Lang.F("гра: панель швидкого доступу {0}", (int)(key - KeyCode.Alpha0)));
            try
            {
                string path = KeyPath != null ? KeyPath.Invoke(null, new object[] { key, false }) as string : null;
                IDictionary buttons = ButtonsField != null && ZInput.instance != null ? ButtonsField.GetValue(ZInput.instance) as IDictionary : null;
                if (!string.IsNullOrEmpty(path) && buttons != null)
                    foreach (DictionaryEntry b in buttons)
                    {
                        string name = b.Key as string;
                        if (name == null || name.StartsWith("Joy", StringComparison.OrdinalIgnoreCase) || b.Value == null) continue;
                        string ap;
                        try
                        {
                            MethodInfo gap = AccessTools.Method(b.Value.GetType(), "GetActionPath");
                            ap = gap != null ? gap.Invoke(b.Value, new object[] { true }) as string : null;
                        }
                        catch (Exception ex) { Unreadable(name, ex); continue; } // a button with no key of its own: skipped, the rest still checked
                        if (string.Equals(ap, path, StringComparison.OrdinalIgnoreCase)) found.Add(Lang.F("гра: {0}", GameName(name)));
                    }
            }
            catch (Exception ex) { Unreadable(null, ex); }

            try
            {
                foreach (PluginInfo pi in Chainloader.PluginInfos.Values)
                {
                    if (pi == null || pi.Instance == null || pi.Instance is Plugin) continue;
                    foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> e in pi.Instance.Config)
                    {
                        object v = e.Value != null ? e.Value.BoxedValue : null;
                        KeyCode other = v is KeyboardShortcut ? ((KeyboardShortcut)v).MainKey : v is KeyCode ? (KeyCode)v : KeyCode.None;
                        if (other != KeyCode.None && other == key) found.Add(pi.Metadata.Name + ": " + e.Key.Key);
                    }
                }
            }
            catch (Exception ex) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven keys: other mods' keys unreadable: " + ex.Message); }
            return found;
        }

        // the game's own name for its action, as in its key settings
        private static string GameName(string button)
        {
            if (Localization.instance != null)
            {
                string loc = Lang.Game("$settings_" + button.ToLowerInvariant());
                if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[") && !loc.StartsWith("$")) return loc;
            }
            return button;
        }
    }
}
#endif
