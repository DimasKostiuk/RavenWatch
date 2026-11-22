#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Вигляд», a person's clothes and things (the user 2026-11-13: «всі речі які є у грі і які можна повісити на
    // людину … всі шапочки які ми робили для воронів … не нахаращено»): a row of its slots, each saying what it wears, and one grid of pictures —
    // the chosen slot's things only, weakest first (as the game opens them, biome by biome), a search over their names; the right hand sorted by
    // kind too. The last slot is a raven's hat of ours (RavenGear, slot head: NpcHat), with its fit on four sliders.
    // IMGUI asks for the same controls in every event of a frame: what changes how many there are (a slot, a kind, the search, a hat put on) takes
    // effect in the next frame — the search on its Layout event, a click by ending this event (GUIUtility.ExitGUI). What is shown is worked out
    // once per change (Shown: the list, the names, the pictures) and only its visible rows are drawn.
    internal partial class StoryPanel
    {
        private const int HatSlot = 8; // (after the game's slots: SlotNames)
        private static readonly int[] WardrobeOrder = { 0, 1, 2, 3, 4, 7, 5, 6, HatSlot }; // (how they stand in the row: the body first, then the hands)
        private int _wearSlot;
        private string _wearFind = "", _wearFindOn = "", _wearKind = "";
        private Vector2 _wearScroll;
        private float _gridTop; // (the scroll as the frame began: which rows are drawn stays the same in all its events)
        private const float Cell = 54f, RowH = Cell + 4f, GridRows = 4.2f;
        private const int PerRow = 8; // (8 × 58: as wide as the column right of the picture)
        private static int _iconFrame, _iconsMade; // (a raven's hat's picture is a photo: a few a frame)
        private GUIStyle _btnRich, _selRich, _hintRich; // (the slot buttons: two lines, the slot's name bold)

        // what the grid shows, worked out once per change of these
        private string _shownKey = "";
        private readonly List<string> _shown = new List<string>();
        private readonly List<string> _kinds = new List<string>();
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(); // prefab or hat id → its name (localized once)

        private void WardrobeStyles()
        {
            if (_btnRich != null) return;
            _btnRich = new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _selRich = new GUIStyle(_sel) { richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _hintRich = new GUIStyle(_hint) { richText = true };
        }

        private void Wardrobe(NpcDef d)
        {
            WardrobeStyles();
            if (Event.current.type == EventType.Layout) _wearFindOn = _wearFind; // (the search takes effect on the frame's first event)
            GUILayout.Label("Одяг і речі", _head);
            // the slots: what each has on now
            for (int r = 0; r < WardrobeOrder.Length; r += 3)
            {
                GUILayout.BeginHorizontal();
                for (int k = r; k < r + 3 && k < WardrobeOrder.Length; k++)
                {
                    int s = WardrobeOrder[k];
                    string worn = s == HatSlot ? d.Hat : InSlot(d, s);
                    if (GUILayout.Button("<b>" + SlotTitle(s) + "</b>\n" + (worn.Length > 0 ? Short(NameOf(s, worn)) : "<color=#8a8a8a>— нема —</color>"), _wearSlot == s ? _selRich : _btnRich, GUILayout.Width(160f), GUILayout.Height(40f)) && _wearSlot != s)
                    { _wearSlot = s; _wearKind = ""; _wearScroll = Vector2.zero; GUIUtility.ExitGUI(); }
                }
                GUILayout.EndHorizontal();
            }

            // the chosen slot: a search, and the way to take its thing off
            string cur = _wearSlot == HatSlot ? d.Hat : InSlot(d, _wearSlot);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Пошук:", GUILayout.Width(52f));
            _wearFind = GUILayout.TextField(_wearFind, 40, GUILayout.Width(220f));
            if (GUILayout.Button("✕", GUILayout.Width(28f)) && _wearFind.Length > 0) { _wearFind = ""; GUIUtility.ExitGUI(); }
            // (right by the search, never pushed to the row's far end: the column is wider than the window shows, and there the button stood
            // out of sight — the user 2026-11-20: «щоб я міг знімати кнопкою якоюсь плащі … зараз наче як не можна»)
            GUILayout.Space(10f);
            GUI.enabled = cur.Length > 0;
            if (GUILayout.Button("Зняти", GUILayout.Width(90f))) { Put(d, _wearSlot, cur, ""); GUIUtility.ExitGUI(); }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            Shown();
            if (_wearSlot == 5) KindChips();

            // the grid: the chosen slot's things, a picture each, only the rows in sight drawn; a click puts it on
            string hover = "";
            int rows = Mathf.CeilToInt(_shown.Count / (float)PerRow);
            float viewH = Mathf.Min(GridRows, Mathf.Max(1f, rows)) * RowH + 8f;
            if (Event.current.type == EventType.Layout) _gridTop = _wearScroll.y;
            _wearScroll = GUILayout.BeginScrollView(_wearScroll, GUILayout.Height(viewH));
            int first = Mathf.Clamp(Mathf.FloorToInt(_gridTop / RowH), 0, Mathf.Max(0, rows - 1)), last = Mathf.Min(rows, first + Mathf.CeilToInt(viewH / RowH) + 1);
            if (first > 0) GUILayout.Space(first * RowH);
            for (int row = first; row < last; row++)
            {
                GUILayout.BeginHorizontal();
                for (int k = row * PerRow; k < (row + 1) * PerRow && k < _shown.Count; k++)
                {
                    string id = _shown[k];
                    Rect at = GUILayoutUtility.GetRect(Cell, Cell, GUILayout.Width(Cell), GUILayout.Height(Cell));
                    bool on = id == cur;
                    if (GUI.Button(at, GUIContent.none, on ? _sel : GUI.skin.button)) { Put(d, _wearSlot, cur, on ? "" : id); GUIUtility.ExitGUI(); } // (a click on what is worn takes it off)
                    Sprite sp = _wearSlot == HatSlot ? HatIcon(id) : IconOf(id);
                    if (sp != null) { if (Event.current.type == EventType.Repaint) DrawSprite(new Rect(at.x + 4f, at.y + 4f, at.width - 8f, at.height - 8f), sp); }
                    else GUI.Label(at, Short(NameOf(_wearSlot, id)), _hint);
                    if (at.Contains(Event.current.mousePosition)) hover = id;
                }
                GUILayout.EndHorizontal();
            }
            if (rows - last > 0) GUILayout.Space((rows - last) * RowH);
            GUILayout.EndScrollView();
            string say = hover.Length > 0 ? hover : cur;
            GUILayout.Label(_shown.Count == 0 ? "Нічого не знайдено." : say.Length > 0 ? (say == cur ? "Вдягнуто: " : "") + NameOf(_wearSlot, say) + (_wearSlot == HatSlot ? "" : "  <color=#8a8a8a>" + say + "</color>") : _shown.Count + " речей. Наведи — побачиш назву, клікни на вдягнуте — зніме, клікни — вдягне.", _hintRich, GUILayout.Height(36f)); // (a fixed height: the hover changes the text between events, not the layout)

            if (_wearSlot == HatSlot && d.Hat.Length > 0) HatFit(d);
        }

        // the list for the chosen slot, its kind and the search — again only when one of them (or the hats) changed
        private void Shown()
        {
            string key = _wearSlot + "|" + _wearKind + "|" + _wearFindOn + "|" + RavenGear.Version;
            if (key == _shownKey) return;
            _shownKey = key;
            _shown.Clear(); _kinds.Clear();
            List<string> all = _wearSlot == HatSlot ? HatIds() : _bySlot[_wearSlot];
            string find = _wearFindOn.Trim().ToLowerInvariant();
            foreach (string id in all)
            {
                string kind = _wearSlot == 5 ? KindOf(id) : "";
                if (kind.Length > 0 && !_kinds.Contains(kind)) _kinds.Add(kind);
                if (_wearKind.Length > 0 && kind != _wearKind) continue;
                if (find.Length > 0 && NameOf(_wearSlot, id).ToLowerInvariant().IndexOf(find, System.StringComparison.Ordinal) < 0 && id.ToLowerInvariant().IndexOf(find, System.StringComparison.Ordinal) < 0) continue;
                _shown.Add(id);
            }
            _kinds.Sort((a, b) => KindRank(a).CompareTo(KindRank(b)));
        }

        // the right hand's kinds, as chips (only the kinds there are)
        private void KindChips()
        {
            AuthorRow chips = AuthorRow.Begin(false);
            if (chips.Button("усе", _wearKind.Length == 0 ? _sel : null, 62f) && _wearKind.Length > 0) { _wearKind = ""; _wearScroll = Vector2.zero; GUIUtility.ExitGUI(); }
            for (int i = 0; i < _kinds.Count; i++)
            {
                string k = _kinds[i];
                if (chips.Button(k, _wearKind == k ? _sel : null, 80f)) { _wearKind = _wearKind == k ? "" : k; _wearScroll = Vector2.zero; GUIUtility.ExitGUI(); }
            }
            chips.End();
        }

        private static readonly string[] KindNames = { "мечі", "сокири", "булави", "списи", "древка", "ножі", "кулаки", "луки", "арбалети", "магія", "кирки", "інструменти", "смолоскипи", "інше" };
        private static int KindRank(string k) { int i = System.Array.IndexOf(KindNames, k); return i < 0 ? 99 : i; }
        private static string KindOf(string prefab)
        {
            ItemDrop.ItemData.SharedData s = SharedOf(prefab);
            if (s == null) return "інше";
            if (s.m_itemType == ItemDrop.ItemData.ItemType.Torch) return "смолоскипи";
            if (s.m_itemType == ItemDrop.ItemData.ItemType.Tool) return s.m_skillType == Skills.SkillType.Pickaxes ? "кирки" : "інструменти"; // (a hammer, a hoe keep the game's default skill: never swords)
            switch (s.m_skillType)
            {
                case Skills.SkillType.Swords: return "мечі";
                case Skills.SkillType.Axes: return "сокири";
                case Skills.SkillType.Clubs: return "булави";
                case Skills.SkillType.Spears: return "списи";
                case Skills.SkillType.Polearms: return "древка";
                case Skills.SkillType.Knives: return "ножі";
                case Skills.SkillType.Unarmed: return "кулаки";
                case Skills.SkillType.Bows: return "луки";
                case Skills.SkillType.Crossbows: return "арбалети";
                case Skills.SkillType.ElementalMagic: case Skills.SkillType.BloodMagic: return "магія";
                case Skills.SkillType.Pickaxes: return "кирки";
            }
            return "інше";
        }

        // a thing into a slot (or out of it: «» ); a raven's hat into the template's «hat:»
        private void Put(NpcDef d, int slot, string cur, string now)
        {
            if (slot == HatSlot) { if (d.Hat != now) { d.Hat = now; if (now.Length == 0) d.HatFit = new Vector4(0f, 0f, 1f, 0f); Changed(); } return; }
            if (cur.Length > 0) d.Gear.Remove(cur);
            if (now.Length > 0) d.Gear.Add(now);
            Changed();
        }

        // the hat's fit: up, forward, size, tilt (from where it sits by itself: NpcHat)
        private void HatFit(NpcDef d)
        {
            GUILayout.Label("Як сидить шапка", _head);
            Vector4 f = d.HatFit, was = f;
            f.x = FitRow("Вище / нижче", f.x, -0.15f, 0.15f, (f.x * 100f).ToString("0") + " см");
            f.y = FitRow("Вперед / назад", f.y, -0.12f, 0.12f, (f.y * 100f).ToString("0") + " см");
            f.z = FitRow("Розмір", f.z, 0.4f, 2.5f, "×" + f.z.ToString("0.00"));
            f.w = FitRow("Нахил", f.w, -40f, 40f, f.w.ToString("0") + "°");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Як сама сідає", GUILayout.Width(140f))) f = new Vector4(0f, 0f, 1f, 0f);
            GUILayout.Label("Шапку зроблено на ворона: на людині вона сама стає завширшки з голову й сідає на маківку.", _hint);
            GUILayout.EndHorizontal();
            if (f != was) { d.HatFit = f; Changed(); }
        }
        private float FitRow(string label, float v, float lo, float hi, string shown)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(120f));
            float now = GUILayout.HorizontalSlider(v, lo, hi, GUILayout.Width(260f));
            GUILayout.Label(shown, _hint, GUILayout.Width(70f));
            GUILayout.EndHorizontal();
            return Mathf.Abs(now - v) > 0.0005f ? now : v;
        }

        // ---- names and pictures
        private static string SlotTitle(int s) { return s == HatSlot ? "Шапка ворона" : SlotNames[s]; }
        private static string Short(string s) { return s.Length > 21 ? s.Substring(0, 20) + "…" : s; }
        private static string NameOf(int slot, string id)
        {
            string key = (slot == HatSlot ? "hat:" : "") + id, n;
            if (Names.TryGetValue(key, out n)) return n;
            n = slot == HatSlot ? HatName(id) : ItemName(id);
            Names[key] = n;
            return n;
        }
        private static ItemDrop.ItemData.SharedData SharedOf(string prefab)
        {
            GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop id = g != null ? g.GetComponent<ItemDrop>() : null;
            return id != null ? id.m_itemData.m_shared : null;
        }
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        private static Sprite IconOf(string prefab)
        {
            Sprite sp;
            if (Icons.TryGetValue(prefab, out sp)) return sp;
            ItemDrop.ItemData.SharedData s = SharedOf(prefab);
            sp = s != null && s.m_icons != null && s.m_icons.Length > 0 ? s.m_icons[0] : null;
            Icons[prefab] = sp;
            return sp;
        }
        private static List<string> _hatIds;
        private static int _hatsOf = -1;
        private static List<string> HatIds()
        {
            if (_hatIds != null && _hatsOf == RavenGear.Version) return _hatIds; // (a hat saved in the Workshop: the list again)
            _hatsOf = RavenGear.Version;
            _hatIds = new List<string>();
            foreach (RavenGear.Item it in RavenGear.Items()) if (it.Slot == RavenGear.Head) _hatIds.Add(it.Id);
            foreach (string k in new List<string>(Names.Keys)) if (k.StartsWith("hat:", System.StringComparison.Ordinal)) Names.Remove(k);
            return _hatIds;
        }
        private static RavenGear.Item Hat(string id) { foreach (RavenGear.Item it in RavenGear.Items()) if (it.Id == id) return it; return null; }
        private static string HatName(string id) { RavenGear.Item it = Hat(id); return it != null ? it.Name : id; }
        private static Sprite HatIcon(string id)
        {
            RavenGear.Item it = Hat(id);
            if (it == null) return null;
            if (it.IconDone) return it.Icon;
            if (_iconFrame != Time.frameCount) { _iconFrame = Time.frameCount; _iconsMade = 0; }
            if (_iconsMade >= 2) return null; // (a photo each: two a frame, the rest next frames)
            _iconsMade++;
            return RavenGear.IconOf(it); // heavy: a photo once per hat, two a frame at most
        }
        // a sprite of an atlas: its own corner of the picture (the game packs its icons; DataUtility knows how, textureRect throws for tight ones)
        private static void DrawSprite(Rect at, Sprite sp)
        {
            Texture t = sp.texture;
            if (t == null) return;
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sp);
            GUI.DrawTextureWithTexCoords(at, t, new Rect(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y), true);
        }
    }
}
#endif
