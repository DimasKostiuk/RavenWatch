#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Valheim.UI;

namespace HuginWhispers
{
    // A "Ravens" group in the game's radial menu (G): the worn ravens' abilities with their cost and cooldown.
    // Other mods (SeneaL UI too) add their groups the same way, by appending to the main circle's list,
    // so they all fit together; when the circle is full the game moves the rest to another layer.
    internal class RavenRadial : ScriptableObject, IRadialConfig
    {
        internal static bool Enabled = true;
        private static RavenRadial _instance;

        private static readonly FieldInfo NameField = AccessTools.Field(typeof(RadialMenuElement), "<Name>k__BackingField");
        private static readonly FieldInfo SubField = AccessTools.Field(typeof(RadialMenuElement), "<SubTitle>k__BackingField");
        private static readonly FieldInfo DescField = AccessTools.Field(typeof(RadialMenuElement), "<Description>k__BackingField");

        public string LocalizedName { get { return Lang.T("Ворони"); } }
        public Sprite Sprite { get { return AbilityIcons.For("main", false); } }

        internal static RavenRadial Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<RavenRadial>();
                    _instance.name = "RavenWatch_Radial";
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return _instance;
            }
        }

        public void InitRadialConfig(RadialBase radial)
        {
            List<RadialMenuElement> list = new List<RadialMenuElement>();
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData hugin = null, munin = null;
            if (p != null) RavenItem.Worn(p, out hugin, out munin);
            bool any = hugin != null || munin != null;
            list.Add(Book()); // first, always in the same place; also with no raven (a lost one comes back from the book)
            if (any) // the ravens' own circle, either face (also on their keys: Q the gifts, J the orders)
            {
                list.Add(Circle(true, Lang.T("Дари"), Lang.T("удари · розвідка · світло · крик…"), Lang.F("Коло дарів воронів. Своя клавіша — {0}.", RavenKeys.Label(RavenKeys.Get(RavenKeys.GiftsId))), AbilityIcons.For("hstrike", false)));
                list.Add(Circle(false, Lang.T("Накази"), Lang.T("поруч · кружляй · облетіти · жердина · трюки"), Lang.F("Коло наказів воронам. Своя клавіша — {0}.", RavenKeys.Label(RavenKeys.Get(RavenKeys.OrdersId))), AbilityIcons.For("orders", false)));
            }
            foreach (RavenAbility a in RavenAbilities.All) // the gifts here too, one by one (not yet open: only in the book)
                if (!a.Passive && (a.Munin ? munin : hugin) != null && RavenAbilities.Shown(a)) list.Add(Ability(a));
            radial.ConstructRadial(list);
        }

        private const string AbilityPrefix = "RavenWatch_Ability_";
        private static string AbilityName(bool munin) { return AbilityPrefix + (munin ? "munin" : "hugin"); }

        // the menu keeps the last used button for a quick repeat; one of ours is useless once its raven is off
        internal static bool Stale(RadialMenuElement e)
        {
            if (e == null || !e.gameObject.name.StartsWith(AbilityPrefix)) return false;
            ItemDrop.ItemData h, m;
            RavenItem.Worn(Player.m_localPlayer, out h, out m);
            return e.gameObject.name.StartsWith(AbilityName(true)) ? m == null : h == null;
        }

        // A gift's line under its name is written when the circle is built, and the game's quick-repeat copy of the last
        // used gift on the main circle is never rebuilt: the line is read anew every time the game shows a gift.
        internal static void Fresh(RadialMenuElement e)
        {
            if (e == null || !e.gameObject.name.StartsWith(AbilityPrefix)) return;
            string n = e.gameObject.name;
            RavenAbility a = RavenAbilities.Find(n.Substring(n.LastIndexOf('_') + 1));
            if (a == null) return;
            if (SubField != null) SubField.SetValue(e, RavenSkills.Status(a));
            if (e.Icon != null) e.Icon.color = RavenSkills.UsableNow(a) ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.55f);
        }

        // the Book of ravens: abilities, your ravens, keys
        private static RadialMenuElement Book()
        {
            EmoteElement e = UnityEngine.Object.Instantiate(RadialData.SO.EmoteElement);
            e.gameObject.name = "RavenWatch_Book";
            EmoteDataMapping map = new EmoteDataMapping();
            map.Emote = (Emotes)0;
            map.Sprite = AbilityIcons.For("book", false);
            map.LocaString = "";
            e.Init(map);
            if (NameField != null) NameField.SetValue(e, Lang.T("Книга воронів"));
            if (SubField != null) SubField.SetValue(e, Lang.T("дари · ворони · довіра · клавіші"));
            if (DescField != null) DescField.SetValue(e, Lang.T("Що вміють ворони, що вони тобі дають, довіра (годування й ритуал) і клавіші для дарів."));
            e.Interact = () => { RavenBook.Open(); return true; };
            e.CloseOnInteract = () => true;
            return e;
        }

        // the ravens' own circle (OrdersMenu) on one of its faces, opened once the game's radial has gone
        private static RadialMenuElement Circle(bool gifts, string name, string sub, string desc, Sprite icon)
        {
            EmoteElement e = UnityEngine.Object.Instantiate(RadialData.SO.EmoteElement);
            e.gameObject.name = gifts ? "RavenWatch_Gifts" : "RavenWatch_Orders";
            EmoteDataMapping map = new EmoteDataMapping();
            map.Emote = (Emotes)0;
            map.Sprite = icon;
            map.LocaString = "";
            e.Init(map);
            if (NameField != null) NameField.SetValue(e, name);
            if (SubField != null) SubField.SetValue(e, sub);
            if (DescField != null) DescField.SetValue(e, desc);
            e.Interact = () => { OrdersMenu.OpenFromRadial(gifts); return true; };
            e.CloseOnInteract = () => true;
            return e;
        }

        // a vanilla emote element, given a raven's icon, name and an ability instead of an emote
        private static RadialMenuElement Ability(RavenAbility a)
        {
            EmoteElement e = UnityEngine.Object.Instantiate(RadialData.SO.EmoteElement);
            e.gameObject.name = AbilityName(a.Munin) + "_" + a.Id; // the gift's id: its state is read again whenever it is shown
            EmoteDataMapping map = new EmoteDataMapping();
            map.Emote = (Emotes)0;
            map.Sprite = AbilityIcons.For(a.Id, a.Munin);
            map.LocaString = "";
            e.Init(map);
            if (NameField != null) NameField.SetValue(e, a.Name);
            if (SubField != null) SubField.SetValue(e, RavenSkills.Status(a));
            if (DescField != null) DescField.SetValue(e, a.Short);
            // one that cannot be used right now (asleep, flown off, resting, too weak) is drawn dim
            if (e.Icon != null) e.Icon.color = RavenSkills.UsableNow(a) ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.55f);
            e.Interact = () => { RavenSkills.Use(a); return true; };
            e.CloseOnInteract = () => true;
            return e;
        }
    }

    // The game lays the circle out clockwise, but where it starts depends on how many pieces there are: with one raven
    // the book (first in the list) landed low on the left. Once laid out, the book swaps places (in the game's own list,
    // so the cursor still picks the right one) with the piece right after the top one, clockwise, and the circle is
    // laid out again: the book is always in the same spot, whatever the gifts.
    [HarmonyPatch(typeof(RadialBase), "SetRadialLayout")]
    internal static class RavenRadialBookFirstPatch
    {
        private static readonly FieldInfo Elements = AccessTools.Field(typeof(RadialBase), "m_elements");
        private static readonly MethodInfo Layout = AccessTools.Method(typeof(RadialBase), "SetRadialLayout");
        private static bool _again;

        // clockwise from straight up, 0..360
        private static float Clock(Vector3 p) { float a = Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg; return a < 0f ? a + 360f : a; }

        private static void Postfix(RadialBase __instance)
        {
            if (_again || !(__instance.CurrentConfig is RavenRadial) || Elements == null || Layout == null) return;
            try
            {
                RadialArray<RadialMenuElement> list = Elements.GetValue(__instance) as RadialArray<RadialMenuElement>;
                RadialMenuElement[] arr = list != null ? list.GetArray : null;
                if (arr == null) return;
                int count = Mathf.Min(list.Count, arr.Length);
                int book = -1;
                for (int i = 0; i < count; i++) if (arr[i] != null && arr[i].gameObject.name == "RavenWatch_Book") { book = i; break; }
                if (book < 0) return;
                int per = Mathf.Max(1, __instance.MaxElementsPerLayer);
                int first = -1;
                float best = 999f;
                for (int i = 0; i < count; i++)
                {
                    if (arr[i] == null || i / per != book / per) continue; // the same page of the circle
                    float a = Clock(arr[i].LocalPosition);
                    if (a < 5f || a > 355f) continue; // the top piece is the way back
                    if (a < best) { best = a; first = i; }
                }
                if (first < 0 || first == book) return;
                RadialMenuElement t = arr[book]; arr[book] = arr[first]; arr[first] = t;
                _again = true;
                try { Layout.Invoke(__instance, null); } finally { _again = false; }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Ravens radial: book first failed: " + e.Message); }
        }
    }

    // before the main circle is built: drop a "last used" ability whose raven is no longer worn, and draw the one that
    // stays as it is now (it was coloured when it was made: a gift used a moment ago looked ready until hovered)
    [HarmonyPatch(typeof(ValheimRadialConfig), "InitRadialConfig")]
    internal static class RavenRadialLastUsedPatch
    {
        private static readonly FieldInfo LastUsed = AccessTools.Field(typeof(RadialBase), "m_lastUsed");
        private static void Prefix(RadialBase radial)
        {
            if (LastUsed == null || radial == null) return;
            try
            {
                RadialMenuElement last = LastUsed.GetValue(radial) as RadialMenuElement;
                if (RavenRadial.Stale(last)) LastUsed.SetValue(radial, null);
                else RavenRadial.Fresh(last);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Ravens radial cleanup failed: " + e.Message); }
        }
    }

    // the main circle: our group is always there, with no raven only the book in it (a lost raven comes back from the book)
    [HarmonyPatch(typeof(ValheimRadialConfig), "AddEmoteGroup")]
    internal static class RavenRadialPatch
    {
        private static void Postfix(ValheimRadialConfig __instance, RadialBase radial, List<RadialMenuElement> elements)
        {
            if (!RavenItem.Enabled || !RavenRadial.Enabled || Player.m_localPlayer == null || elements == null) return;
            try
            {
                GroupElement g = UnityEngine.Object.Instantiate(RadialData.SO.GroupElement);
                g.Init(RavenRadial.Instance, __instance, radial);
                elements.Add(g);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Ravens radial group failed: " + e.Message); }
        }
    }
}
#endif

#if RAVEN_ITEM
namespace HuginWhispers
{
    // the game shows a circle piece's name and line: ours are read anew right before
    [HarmonyLib.HarmonyPatch]
    internal static class RavenRadialFreshPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            // Set has two overloads (element + animator, and a radial config); ours is the element one
            System.Type t = HarmonyLib.AccessTools.TypeByName("Valheim.UI.ElementInfo");
            if (t == null) return null;
            foreach (System.Reflection.MethodInfo m in HarmonyLib.AccessTools.GetDeclaredMethods(t))
            {
                if (m.Name != "Set") continue;
                System.Reflection.ParameterInfo[] p = m.GetParameters();
                if (p.Length > 0 && p[0].ParameterType == typeof(Valheim.UI.RadialMenuElement)) return m;
            }
            return null;
        }

        private static bool Prepare() { return TargetMethod() != null; }

        private static void Prefix(Valheim.UI.RadialMenuElement element) { try { RavenRadial.Fresh(element); } catch { } }
    }
}
#endif
