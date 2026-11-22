#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Вигляд»: our people's looks on a mannequin (NpcPreview). A dvergr (body: dvergr) has controls of its own: DvergrLook.
    internal partial class StoryPanel
    {
        // ---------------------------------------------------------------- «Вигляд»: our people's looks, on a mannequin
        // (the user 2026-11-01: no NPC to be put before you to be dressed) — a list of the templates we write (not the
        // settlers the mod makes), the picked one on a mannequin of its own (NpcPreview), its template changed in place:
        // the picture at once, the living ones of it near dressed again a moment later; «Зберегти» writes it as
        // config/RavenWatch/npc/<id>.txt (read while tuning; then built into the mod — assets/story/npc).
        private string _lookId = "";
        private Vector2 _peopleScroll;
        private bool _drag;

        private void Look()
        {
            NpcDef d = NpcDefs.Find(_lookId);
            if (_redressAt > 0f && Time.unscaledTime >= _redressAt) { _redressAt = -1f; if (d != null) foreach (NpcActor n in NpcActor.Live) if (n != null && n.Def != null && n.Def.Id == d.Id) n.Redress(); } // (the living ones of it near: as on the picture)
            Lists();

            GUILayout.BeginHorizontal();
            // left: our people
            GUILayout.BeginVertical(GUILayout.Width(180f));
            GUILayout.Label("Наші люди", _head);
            _peopleScroll = GUILayout.BeginScrollView(_peopleScroll, GUILayout.Height(Fill(40f)));
            foreach (NpcDef x in NpcDefs.All())
            {
                if (x.Generated) continue; // (settlers: the mod makes each its own)
                if (GUILayout.Button(x.Name + "\n(" + x.Id + ")", x.Id == _lookId ? _sel : GUI.skin.button)) { _lookId = x.Id; _saveId = x.Id; _saveNameNpc = x.Name; d = x; }
            }
            GUILayout.EndScrollView();
            GUILayout.Label("Поселенців мод одягає сам — тут лише ті, кого пишемо ми.", _hint);
            GUILayout.EndVertical();
            GUILayout.Space(8);

            LookPicture(d);
            _lookScroll = GUILayout.BeginScrollView(_lookScroll);
            if (d == null) GUILayout.Label("Обери людину зліва.", _hint);
            else if (d.Odin) GUILayout.Label("Це тіло Одіна — його вигляд не міняється (лише ім'я в шаблоні).", _hint);
            else
            {
                GUILayout.Label("Зміни видно на картинці одразу, а на живих «" + d.Id + "» поруч — за мить. Щоб лишилось — «Зберегти» внизу.", _hint);

                if (d.Dvergr) DvergrLook(d);
                else
                {
                GUILayout.Label("Тіло", _head);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Чоловік", d.Model == 0 ? _sel : GUI.skin.button, GUILayout.Width(120f))) { d.Model = 0; Changed(); }
                if (GUILayout.Button("Жінка", d.Model == 1 ? _sel : GUI.skin.button, GUILayout.Width(120f))) { d.Model = 1; Changed(); }
                GUILayout.EndHorizontal();

                GUILayout.Label("Зріст і статура", _head);
                NpcPersona w = NpcPersona.OfTemplate(d); // (as in the world: a person written by hand is seeded by its template)
                float h = Trait("Зріст", w.Height, 0.8f, 1.25f, "×" + w.Height.ToString("0.00"));
                float b = Trait("Статура", w.Build, -1f, 1f, (w.Build < -0.15f ? "худий " : w.Build > 0.15f ? "огрядний " : "звичайний ") + w.Build.ToString("0.00"));
                if (Mathf.Abs(h - w.Height) > 0.001f) { d.SetTrait("height", h); Changed(); }
                if (Mathf.Abs(b - w.Build) > 0.001f) { d.SetTrait("build", b); Changed(); }

                GUILayout.Label("Шкіра", _head);
                d.Skin = ColourRow(d.Skin, SkinPresets, SkinNames);
                GUILayout.Label("Волосся й борода", _head);
                d.Hair = Pick("Зачіска", d.Hair, _hairs, true);
                d.Beard = Pick("Борода", d.Beard, _beards, true);
                d.HairColor = ColourRow(d.HairColor, HairPresets, HairNames);
                GUILayout.Label("Очі й колір одягу", _head);
                d.TintEyes = TintRow("Очі", d.TintEyes, EyePresets, EyeNames);
                d.TintCape = TintRow("Плащ", d.TintCape, CapePresets, CapeNames);
                d.TintHead = TintRow("На голові", d.TintHead, CapePresets, CapeNames);
                d.TintChest = TintRow("Груди", d.TintChest, CapePresets, CapeNames);
                d.TintLegs = TintRow("Ноги", d.TintLegs, CapePresets, CapeNames);
                GUILayout.Label("Колір, яким виходить уся річ у цьому слоті, хоч яка вона (з ременями й металом); світло, тінь і малюнок лишаються свої. Нічого не вдягнуто — нічого не фарбується. Очі світяться лише у двергра.", _hint);

                Wardrobe(d); // (the slots and one grid of pictures: StoryPanelWardrobe.cs)
                }

                GUILayout.Space(10);
                GUILayout.Label("Зберегти як шаблон", _head);
                GUILayout.BeginHorizontal();
                GUILayout.Label("id:", GUILayout.Width(40f));
                _saveId = GUILayout.TextField(_saveId, 40, GUILayout.Width(200f));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("ім'я:", GUILayout.Width(40f));
                _saveNameNpc = GUILayout.TextField(_saveNameNpc, 60, GUILayout.Width(200f));
                GUI.enabled = Story.GoodName(_saveId) && _saveNameNpc.Trim().Length > 0;
                if (GUILayout.Button("Зберегти", GUILayout.Width(110f))) Note = SaveDef(d);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Label("Інший id — новий шаблон (цей лишається як був у файлі). Звички й репліки шаблону зберігаються як були.", _hint);

                GUILayout.Space(8);
                NpcActor live = null;
                foreach (NpcActor n in NpcActor.Live) if (n != null && n.Def != null && n.Def.Id == d.Id) { live = n; break; }
                GUI.enabled = live != null;
                if (GUILayout.Button(live != null ? "Діагностика тіла (для розписів і очей)" : "Діагностика тіла — потрібен живий «" + d.Id + "» поруч")) Note = Diagnose(live);
                GUI.enabled = true;
            }

            GUILayout.Space(14);
            GUILayout.Label("Далі буде (ще не зроблено)", _head);
            GUILayout.Label(
                "• Розписи на тілі — бойова фарба, татуювання, шрами, бруд, попіл: своя копія текстури тіла з візерунками по зонах (голова, тулуб, руки, ноги), як «Фарби» воронів.\n" +
                "• Очі — колір і світіння (відлуння, Гейд з подихом Хель, Бальдр).\n" +
                "• Розмір і пропорції — 0.9–1.1, ширші плечі, згорблений старий (перевірити, щоб не ламало анімацій).\n" +
                "• Постава й хода — наші власні анімації на NPC (кульгає, вальяжно, втомлено).\n" +
                "• Наші речі з Meshy — сумка з травами, амулет, шапка — як спорядження воронів.\n" +
                "• Ефекти на тілі — туман, іскри, пір'я.\n" +
                "• Відлуння — бліде, знебарвлене, зі світінням; чи вийде напівпрозорим — перевірити.\n" +
                "Перший крок до розписів і очей — кнопка «Діагностика тіла» вище: я з її звіту побачу, що дозволяє шейдер тіла.", _hint);
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
        }

        // the template on its mannequin (NpcPreview), turned by a drag on it or the buttons, framed from the body to the face
        private void LookPicture(NpcDef d)
        {
            GUILayout.BeginVertical(GUILayout.Width(NpcPreview.W + 8f));
            if (d == null || d.Odin) { GUILayout.Label("Тут буде видно того, кого правиш.", _hint, GUILayout.Width(NpcPreview.W)); GUILayout.EndVertical(); GUILayout.Space(8); return; }
            NpcPreview.Want(d);
            Texture t = NpcPreview.Picture;
            Rect at = GUILayoutUtility.GetRect(NpcPreview.W, NpcPreview.H, GUILayout.Width(NpcPreview.W), GUILayout.Height(NpcPreview.H));
            if (t != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(at, t, ScaleMode.ScaleToFit, false);
            if (t == null && NpcPreview.Problem.Length > 0) GUI.Label(at, NpcPreview.Problem, _warn);
            Event e = Event.current; // (a drag on the picture turns it)
            if (e.type == EventType.MouseDown && at.Contains(e.mousePosition)) { _drag = true; e.Use(); }
            else if (e.type == EventType.MouseUp) _drag = false;
            else if (e.type == EventType.MouseDrag && _drag) { NpcPreview.Yaw -= e.delta.x * 0.6f; e.Use(); }
            GUILayout.BeginHorizontal(GUILayout.Width(NpcPreview.W));
            if (GUILayout.RepeatButton("<", GUILayout.Width(40f))) NpcPreview.Yaw += 90f * Time.unscaledDeltaTime;
            if (GUILayout.Button("Обличчям")) NpcPreview.Yaw = 0f;
            if (GUILayout.Button("Боком")) NpcPreview.Yaw = 90f;
            if (GUILayout.Button("Спиною")) NpcPreview.Yaw = 180f;
            if (GUILayout.RepeatButton(">", GUILayout.Width(40f))) NpcPreview.Yaw -= 90f * Time.unscaledDeltaTime;
            GUILayout.EndHorizontal();
            AuthorRow shots = AuthorRow.Begin();
            for (int i = 0; i < NpcPreview.Shots.Length; i++)
                if (shots.Button(NpcPreview.Shots[i], NpcPreview.Shot == i ? _sel : null)) NpcPreview.Shot = i;
            shots.End();
            GUILayout.Label("Тягни мишкою по картинці, щоб повернути.", _hint);
            GUILayout.EndVertical();
            GUILayout.Space(8);
        }

        private void Changed() { _redressAt = Time.unscaledTime + 0.25f; }

        // a dvergr (NpcDef.Dvergr): its look is what it wears — one of the game's own suits, one of its hairs, a thing in its hand; its height
        private static List<string> _dvSuits, _dvHairs;
        private static readonly Dictionary<string, string> DvergrNames = new Dictionary<string, string>
        {
            { "DvergerSuitArbalest", "Розвідник" }, { "DvergerSuitArbalest_Ashlands", "Розвідник із Попелищ" }, { "DvergerSuitFire", "Маг вогню" }, { "DvergerSuitIce", "Маг криги" }, { "DvergerSuitSupport", "Маг підтримки" },
            { "DvergerHairMale", "Чоловіче з бородою" }, { "DvergerHairMale_Redbeard", "Чоловіче руде з бородою" }, { "DvergerHairFemale", "Жіноче" }, { "DvergerHairFemale_Redhair", "Жіноче руде" },
        };
        private static string DvergrHas(NpcDef d, string start) { foreach (string g in d.Gear) if (g.StartsWith(start, System.StringComparison.Ordinal)) return g; return ""; }
        private void DvergrLook(NpcDef d)
        {
            GUILayout.Label("Зріст", _head);
            NpcPersona w = NpcPersona.OfTemplate(d);
            float h = Trait("Зріст", w.Height, 0.8f, 1.25f, "×" + w.Height.ToString("0.00"));
            if (Mathf.Abs(h - w.Height) > 0.001f) { d.SetTrait("height", h); Changed(); }
            GUILayout.Label("Вбрання й волосся двергра", _head);
            foreach (string start in new[] { "DvergerSuit", "DvergerHair" })
            {
                string cur = DvergrHas(d, start);
                string now = Pick(start == "DvergerSuit" ? "Вбрання" : "Волосся", cur, start == "DvergerSuit" ? _dvSuits : _dvHairs, true);
                if (now != cur) { if (cur.Length > 0) d.Gear.Remove(cur); if (now.Length > 0) d.Gear.Add(now); }
            }
            GUILayout.Label("Капюшон належить вбранню: без вбрання двергр голий. Шкіри, статі й статури в нього нема.", _hint);
            GUILayout.Label("Кольори", _head);
            d.TintSkin = TintRow("Шкіра", d.TintSkin, DvSkin, DvSkinNames);
            d.TintHair = TintRow("Волосся й борода", d.TintHair, HairPresets, HairNames);
            d.TintSuit = TintRow("Вбрання", d.TintSuit, DvSuit, DvSuitNames);
            d.TintEyes = TintRow("Очі", d.TintEyes, DvEyes, DvEyesNames);
            GUILayout.Label("Колір — яким виходить уся частина; світло, тінь і малюнок лишаються свої. «Як у грі» знімає колір.", _hint);
            GUILayout.Label("Річ у руках", _head);
            for (int s = 5; s <= 6; s++)
            {
                string cur = InSlot(d, s);
                string now = Pick(SlotNames[s], cur, _bySlot[s], true);
                if (now != cur) { if (cur.Length > 0) d.Gear.Remove(cur); if (now.Length > 0) d.Gear.Add(now); }
            }
        }

        // a person's eyes, cape and what is on its head (BodyTint: tint_eyes, tint_cape, tint_head — the head takes the cape's colours)
        private static readonly Vector3[] EyePresets = { new Vector3(0.92f, 0.64f, 0.16f), new Vector3(0.36f, 0.22f, 0.1f), new Vector3(0.3f, 0.5f, 0.25f), new Vector3(0.55f, 0.6f, 0.65f), new Vector3(0.44f, 0.73f, 1f), new Vector3(0.08f, 0.06f, 0.05f) };
        private static readonly string[] EyeNames = { "бурштин", "карі", "зелені", "сірі", "блакитні", "темні" };
        private static readonly Vector3[] CapePresets = { new Vector3(0.5f, 0.37f, 0.26f), new Vector3(0.62f, 0.47f, 0.25f), new Vector3(0.78f, 0.7f, 0.56f), new Vector3(0.33f, 0.22f, 0.15f), new Vector3(0.45f, 0.45f, 0.45f), new Vector3(0.12f, 0.12f, 0.13f) };
        private static readonly string[] CapeNames = { "кавовий", "вохра", "кремовий", "бурий", "сірий", "темний" };

        // a dvergr's colour (BodyTint): as the game made it, or its own — the people's colour row
        private static readonly Vector3[] DvSkin = { new Vector3(0.36f, 0.47f, 0.48f), new Vector3(0.45f, 0.45f, 0.45f), new Vector3(0.62f, 0.52f, 0.44f), new Vector3(0.3f, 0.36f, 0.3f), new Vector3(0.5f, 0.58f, 0.66f), new Vector3(0.24f, 0.22f, 0.26f) };
        private static readonly string[] DvSkinNames = { "сіро-синя", "сіра", "тілесна", "болотна", "бліда", "темна" };
        private static readonly Vector3[] DvSuit = { new Vector3(0.19f, 0.26f, 0.14f), new Vector3(0.13f, 0.2f, 0.26f), new Vector3(0.3f, 0.1f, 0.09f), new Vector3(0.12f, 0.12f, 0.13f), new Vector3(0.36f, 0.28f, 0.14f), new Vector3(0.24f, 0.14f, 0.28f) };
        private static readonly string[] DvSuitNames = { "зелене", "синє", "червоне", "чорне", "вохра", "пурпур" };
        private static readonly Vector3[] DvEyes = { new Vector3(1f, 0.74f, 0.2f), new Vector3(0f, 0.38f, 1f), new Vector3(0.3f, 1f, 0.4f), new Vector3(1f, 0.25f, 0.1f), new Vector3(0.9f, 0.9f, 1f), new Vector3(0.05f, 0.05f, 0.05f) };
        private static readonly string[] DvEyesNames = { "золоті", "сині", "зелені", "червоні", "білі", "згаслі" };
        private Vector3 TintRow(string label, Vector3 c, Vector3[] presets, string[] names)
        {
            bool own = NpcDef.HasTint(c);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150f));
            if (GUILayout.Button("як у грі", !own ? _sel : GUI.skin.button, GUILayout.Width(100f)) && own) { c = NpcDef.NoTint; own = false; Changed(); }
            if (GUILayout.Button("свій колір", own ? _sel : GUI.skin.button, GUILayout.Width(100f)) && !own) { c = presets[0]; own = true; Changed(); }
            GUILayout.EndHorizontal();
            return own ? ColourRow(c, presets, names) : c;
        }

        // a trait on a slider, in steps of 0.05 (written only when moved)
        private float Trait(string label, float v, float lo, float hi, string shown)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            float now = GUILayout.HorizontalSlider(v, lo, hi, GUILayout.Width(260f));
            GUILayout.Label(shown, _hint);
            GUILayout.EndHorizontal();
            return Mathf.Abs(now - v) > 0.001f ? Mathf.Clamp(Mathf.Round(now * 20f) / 20f, lo, hi) : v;
        }

        // a colour: presets + three sliders (as the game keeps it: 0..1 each)
        private Vector3 ColourRow(Vector3 c, Vector3[] presets, string[] names)
        {
            AuthorRow row = AuthorRow.Begin(); // (the column is narrow: as many a row as fit)
            for (int i = 0; i < presets.Length; i++)
                if (row.Button(names[i])) { c = presets[i]; Changed(); }
            row.End();
            Vector3 was = c;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ч", GUILayout.Width(14f)); c.x = GUILayout.HorizontalSlider(c.x, 0f, 1f, GUILayout.Width(110f));
            GUILayout.Label("З", GUILayout.Width(14f)); c.y = GUILayout.HorizontalSlider(c.y, 0f, 1f, GUILayout.Width(110f));
            GUILayout.Label("С", GUILayout.Width(14f)); c.z = GUILayout.HorizontalSlider(c.z, 0f, 1f, GUILayout.Width(110f));
            GUILayout.Label(c.x.ToString("0.00") + " " + c.y.ToString("0.00") + " " + c.z.ToString("0.00"), _hint);
            GUILayout.EndHorizontal();
            if (c != was) Changed();
            return c;
        }

        // ◀ name ▶ through a list ("" = none)
        private string Pick(string label, string cur, List<string> all, bool none)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            int i = all.IndexOf(cur);
            string now = cur;
            if (GUILayout.Button("◀", GUILayout.Width(34f))) now = all.Count == 0 ? "" : all[i <= 0 ? all.Count - 1 : i - 1];
            GUILayout.Label(cur.Length > 0 ? ItemName(cur) + "  (" + cur + ")" : "— нема —", GUILayout.Width(220f));
            if (GUILayout.Button("▶", GUILayout.Width(34f))) now = all.Count == 0 ? "" : all[i < 0 || i >= all.Count - 1 ? 0 : i + 1];
            if (none && GUILayout.Button("зняти", GUILayout.Width(70f))) now = "";
            GUILayout.EndHorizontal();
            if (now != cur) Changed();
            return now;
        }

        private static string ItemName(string prefab)
        {
            string dv; if (DvergrNames.TryGetValue(prefab, out dv)) return dv; // (the game names every one of them «Iron plate armor»)
            GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop id = g != null ? g.GetComponent<ItemDrop>() : null;
            return id != null && Localization.instance != null ? Localization.instance.Localize(id.m_itemData.m_shared.m_name) : prefab;
        }

        private static int SlotOf(ItemDrop.ItemData.SharedData s)
        {
            switch (s.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Helmet: return 0;
                case ItemDrop.ItemData.ItemType.Chest: return 1;
                case ItemDrop.ItemData.ItemType.Legs: return 2;
                case ItemDrop.ItemData.ItemType.Shoulder: return 3;
                case ItemDrop.ItemData.ItemType.Utility: return 4;
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch: return 5;
                case ItemDrop.ItemData.ItemType.Shield: return 6;
                case ItemDrop.ItemData.ItemType.Trinket: return 7;
            }
            return -1;
        }

        private static string InSlot(NpcDef d, int slot)
        {
            foreach (string g in d.Gear)
            {
                GameObject pf = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(g) : null;
                ItemDrop id = pf != null ? pf.GetComponent<ItemDrop>() : null;
                if (id != null && SlotOf(id.m_itemData.m_shared) == slot) return g;
            }
            return "";
        }

        // the game's own hair, beards and gear by slot (once)
        private static void Lists()
        {
            if (_bySlot != null || ObjectDB.instance == null) return;
            _hairs = new List<string>(); _beards = new List<string>(); _dvSuits = new List<string>(); _dvHairs = new List<string>();
            _bySlot = new Dictionary<int, List<string>>();
            for (int s = 0; s < SlotNames.Length; s++) _bySlot[s] = new List<string>();
            foreach (GameObject g in ObjectDB.instance.m_items)
            {
                if (g == null) continue;
                ItemDrop id = g.GetComponent<ItemDrop>();
                if (id == null) continue;
                ItemDrop.ItemData.SharedData s = id.m_itemData.m_shared;
                if (g.name.StartsWith("DvergerSuit", System.StringComparison.Ordinal)) { _dvSuits.Add(g.name); continue; } // (a dvergr's own: DvergrLook)
                if (g.name.StartsWith("DvergerHair", System.StringComparison.Ordinal)) { _dvHairs.Add(g.name); continue; }
                if (s.m_itemType == ItemDrop.ItemData.ItemType.Customization)
                {
                    if (g.name.StartsWith("Hair")) _hairs.Add(g.name);
                    else if (g.name.StartsWith("Beard")) _beards.Add(g.name);
                    continue;
                }
                int slot = SlotOf(s);
                if (slot >= 0 && NpcGear.Wearable(id)) _bySlot[slot].Add(g.name); // (not a creature's attack: those are items too)
            }
            _hairs.Sort(System.StringComparer.Ordinal); _beards.Sort(System.StringComparer.Ordinal); _dvSuits.Sort(System.StringComparer.Ordinal); _dvHairs.Sort(System.StringComparer.Ordinal);
            foreach (List<string> l in _bySlot.Values) l.Sort((a, b) => { int c = Strength(a).CompareTo(Strength(b)); return c != 0 ? c : string.CompareOrdinal(a, b); }); // (weakest first: as the game opens them, biome by biome)
        }

        // how strong a thing is, to put them in the order the game opens them: armour, a weapon's damage, a shield's block
        private static float Strength(string prefab)
        {
            ItemDrop.ItemData.SharedData s = SharedOf(prefab);
            if (s == null) return 0f;
            return s.m_armor + s.m_damages.GetTotalDamage() + s.m_blockPower;
        }

        private string SaveDef(NpcDef d)
        {
            string oldId = d.Id, oldName = d.Name;
            d.Id = _saveId; d.Name = _saveNameNpc.Trim();
            string text = d.Write();
            d.Id = oldId; d.Name = oldName;
            string why = StoryFiles.Save(StoryFiles.Npc, NpcDefs.Dir, _saveId + ".txt", text); // (where the story's files are read: the mod's own folder on the author's machine — StoryFiles)
            if (why != null) return "Не вдалося зберегти: " + why;
            NpcDefs.Reload();
            return "Шаблон «" + _saveId + "» збережено → " + System.IO.Path.Combine(StoryFiles.Target(StoryFiles.Npc, NpcDefs.Dir), _saveId + ".txt") + (StoryFiles.IsSource || RavenLooks.FromFiles ? "" : " · файли NPC читаються лише в режимі налаштування (адмін)");
        }

        // what the body's shader allows (for paint, eyes, glow): a report into config/RavenWatch/npc/_diag/
        private static string Diagnose(NpcActor a)
        {
            if (a == null) return "Нема кого.";
            System.Text.StringBuilder sb = new System.Text.StringBuilder("NPC body report: " + a.Id + "\n");
            foreach (Renderer r in a.GetComponentsInChildren<Renderer>(true))
            {
                sb.Append("\n[").Append(r.GetType().Name).Append("] ").Append(r.name).Append(r.enabled ? "" : " (off)");
                SkinnedMeshRenderer sm = r as SkinnedMeshRenderer;
                if (sm != null && sm.sharedMesh != null) sb.Append(" · mesh ").Append(sm.sharedMesh.name).Append(' ').Append(sm.sharedMesh.vertexCount).Append(" v, readable ").Append(sm.sharedMesh.isReadable).Append(", bones ").Append(sm.bones.Length);
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    Shader sh = m.shader;
                    sb.Append("\n  material ").Append(m.name).Append(" · shader ").Append(sh != null ? sh.name : "?");
                    if (sh == null) continue;
                    for (int i = 0; i < sh.GetPropertyCount(); i++)
                    {
                        string pn = sh.GetPropertyName(i);
                        sb.Append("\n    ").Append(pn).Append(" (").Append(sh.GetPropertyType(i)).Append(')');
                        if (sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                        {
                            Texture t = m.GetTexture(pn);
                            if (t != null) { sb.Append(" = ").Append(t.name).Append(' ').Append(t.width).Append('×').Append(t.height); Texture2D t2 = t as Texture2D; if (t2 != null) sb.Append(' ').Append(t2.format).Append(", readable ").Append(t2.isReadable); }
                        }
                        else if (sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color) sb.Append(" = ").Append(m.GetColor(pn));
                        else if (sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Float || sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Range) sb.Append(" = ").Append(m.GetFloat(pn));
                    }
                }
            }
            try
            {
                string dir = System.IO.Path.Combine(NpcDefs.Dir, "_diag");
                System.IO.Directory.CreateDirectory(dir);
                string f = System.IO.Path.Combine(dir, a.Id + ".txt");
                System.IO.File.WriteAllText(f, sb.ToString(), new System.Text.UTF8Encoding(false));
                return "Звіт про тіло записано → " + f;
            }
            catch (System.Exception e) { return "Не вдалося записати звіт: " + e.Message; }
        }
    }
}
#endif
