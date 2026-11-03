#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Вигляд»: our people's looks on a mannequin (NpcPreview).
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
            _peopleScroll = GUILayout.BeginScrollView(_peopleScroll, GUILayout.Height(520f));
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

                GUILayout.Label("Тіло", _head);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Чоловік", d.Model == 0 ? _sel : GUI.skin.button, GUILayout.Width(120f))) { d.Model = 0; Changed(); }
                if (GUILayout.Button("Жінка", d.Model == 1 ? _sel : GUI.skin.button, GUILayout.Width(120f))) { d.Model = 1; Changed(); }
                GUILayout.EndHorizontal();

                GUILayout.Label("Шкіра", _head);
                d.Skin = ColourRow(d.Skin, SkinPresets, SkinNames);
                GUILayout.Label("Волосся й борода", _head);
                d.Hair = Pick("Зачіска", d.Hair, _hairs, true);
                d.Beard = Pick("Борода", d.Beard, _beards, true);
                d.HairColor = ColourRow(d.HairColor, HairPresets, HairNames);

                GUILayout.Label("Одяг і речі в руках", _head);
                for (int s = 0; s < SlotNames.Length; s++)
                {
                    string cur = InSlot(d, s);
                    string now = Pick(SlotNames[s], cur, _bySlot[s], true);
                    if (now != cur) { if (cur.Length > 0) d.Gear.Remove(cur); if (now.Length > 0) d.Gear.Add(now); }
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
                "• Колір одягу — перефарбувати плащ і тканину, щоб однакова броня не робила близнюків.\n" +
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
            GUILayout.BeginHorizontal(GUILayout.Width(NpcPreview.W));
            for (int i = 0; i < NpcPreview.Shots.Length; i++)
                if (GUILayout.Button(NpcPreview.Shots[i], NpcPreview.Shot == i ? _sel : GUI.skin.button)) NpcPreview.Shot = i;
            GUILayout.EndHorizontal();
            GUILayout.Label("Тягни мишкою по картинці, щоб повернути.", _hint);
            GUILayout.EndVertical();
            GUILayout.Space(8);
        }

        private void Changed() { _redressAt = Time.unscaledTime + 0.25f; }

        // a colour: presets + three sliders (as the game keeps it: 0..1 each)
        private Vector3 ColourRow(Vector3 c, Vector3[] presets, string[] names)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < presets.Length; i++)
            {
                if (i > 0 && i % 3 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); } // (three a row: the column is narrow)
                if (GUILayout.Button(names[i])) { c = presets[i]; Changed(); }
            }
            GUILayout.EndHorizontal();
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
            _hairs = new List<string>(); _beards = new List<string>();
            _bySlot = new Dictionary<int, List<string>>();
            for (int s = 0; s < SlotNames.Length; s++) _bySlot[s] = new List<string>();
            foreach (GameObject g in ObjectDB.instance.m_items)
            {
                if (g == null) continue;
                ItemDrop id = g.GetComponent<ItemDrop>();
                if (id == null) continue;
                ItemDrop.ItemData.SharedData s = id.m_itemData.m_shared;
                if (s.m_itemType == ItemDrop.ItemData.ItemType.Customization)
                {
                    if (g.name.StartsWith("Hair")) _hairs.Add(g.name);
                    else if (g.name.StartsWith("Beard")) _beards.Add(g.name);
                    continue;
                }
                int slot = SlotOf(s);
                if (slot >= 0) _bySlot[slot].Add(g.name);
            }
            _hairs.Sort(System.StringComparer.Ordinal); _beards.Sort(System.StringComparer.Ordinal);
            foreach (List<string> l in _bySlot.Values) l.Sort(System.StringComparer.Ordinal);
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
