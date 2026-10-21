#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The test window's tab «Руни» (console: raven wear ui): every rune effect there is, by layer, each with a short
    // code (Ф3, Б5, Г2, В1, М4, Н7) to name it by. Left — the list: a click puts it on the raven on the stage (again —
    // takes it off), «+» adds it to the mix. Middle — the raven, dressed by the game's own RavenWear.Dress (what is seen
    // here is what a rune puts on a raven in the world). Right — what the clicked one is made of, and the mix: two or
    // three of one layer laid on together by FxMix (mode, weights, one colour), seen on the raven at once, saved to
    // config/RavenWatch/mixes/<id>.txt.
    internal class RunePanel : MonoBehaviour
    {
        private sealed class Entry
        {
            public string Code = "", Id = "", Name = "", Desc = "", WearId = "", SourceId = "";
            public int Cat;            // index into CatNames
            public WearKind Kind;
            public RavenFxDef Fx; public Look Look; public MixDef Mix;
        }

        private static readonly string[] CatNames = { "Фрея", "Бальдр", "Геймдаль", "В'язі", "Мої мікси", "Не руни" };
        private static readonly string[] CatLetters = { "Ф", "Б", "Г", "В", "М", "Н" };
        private static readonly string[] CatTips =
        {
            "колір пір'я, очі, полиск",
            "сяйво навколо ворона — завжди на ньому",
            "слід у повітрі — коли летить",
            "сплетені тобою на Воронячому дереві",
            "збережені тут мікси",
            "ефекти й вигляди бібліотеки, яких руни не дають (лише показати й міксувати)",
        };
        private static readonly string[] CatColors = { "FF9AC8", "FFC860", "8EC9FF", "C8A0FF", "9AE6A0", "A0A0A0" };
        private static readonly string[] LayerTips = { "Фрея — колір пір'я", "Бальдр — сяйво (аура)", "Геймдаль — слід у польоті" };

        private GUIStyle _btn, _on, _row, _rowOn, _small, _head, _code;
        private List<Entry> _entries = new List<Entry>();
        private int _entriesAt = -1;
        private bool _entriesOther;
        private readonly string[] _worn = { "", "", "" }; // per layer: the wear id on the stage raven
        private Entry _sel;
        private MixDef _mix = new MixDef();
        private bool _mixDirty;
        private float _mixAt;
        private bool _showOther, _confirmDelete;
        private Vector2 _leftScroll, _rightScroll;
        private string _note = "";

        // ---------------------------------------------------------------- the tab shown / left
        internal void Opened() { enabled = true; _entriesAt = -1; }

        internal void Closed()
        {
            enabled = false;
            if (EdChanged) { FxLib.Live = null; RavenLooks.Live = null; _liveDirty = false; _edFx = null; _edLook = null; _sel = null; _pendingSel = null; }
            LookStage.Close(); // (the stage's raven goes, and with it all it wears)
        }

        private void OnDestroy() { LookStage.Close(); }

        // ---------------------------------------------------------------- every frame: the stage raven dressed, filmed
        private void Update()
        {
            if (!LookStage.Ready(LookStage.Munin)) return;
            if (_mixDirty && Time.unscaledTime - _mixAt > 0.15f) PushMix();
            if (_liveDirty && Time.unscaledTime - _liveAt > 0.25f) PushLive();
            GameObject model = LookStage.Model;
            WearState st = model.GetComponent<WearState>();
            if (st == null) { st = model.AddComponent<WearState>(); st.Stage = true; }
            RavenWear.Dress(model, LookStage.Munin, StageCode());
            LookStage.Render();
        }

        private string StageCode()
        {
            Wear w = new Wear();
            for (int k = 0; k < WearCatalog.Kinds; k++) w[(WearKind)k] = _worn[k];
            return w.Code();
        }

        // the mix as it is now goes on the raven (two sources or more); one alone or none: it comes off
        private void PushMix()
        {
            _mixDirty = false;
            int k = (int)_mix.Kind;
            if (_mix.Of.Count >= 2) { FxMix.Draft = _mix.Copy(); _worn[k] = FxMix.DraftId; if (_mix.Kind == WearKind.Heimdall && LookStage.Pose == 0) LookStage.Pose = 2; }
            else
            {
                FxMix.Draft = null;
                for (int i = 0; i < _worn.Length; i++) if (_worn[i] == FxMix.DraftId) _worn[i] = "";
            }
        }

        private void MixChanged() { _mixDirty = true; _mixAt = Time.unscaledTime; }

        // ---------------------------------------------------------------- the list
        private void Refresh()
        {
            Player pl = Player.m_localPlayer;
            string mine = pl != null ? RuneBook.Of(pl).Bind : "";
            int stamp = WearCatalog.Stamp * 101 + mine.Length;
            if (_entriesAt == stamp && _entriesOther == _showOther) return;
            _entriesAt = stamp; _entriesOther = _showOther;
            string sel = _sel != null ? _sel.Cat + "|" + _sel.Id : "";
            _entries = new List<Entry>();
            for (int k = 0; k < WearCatalog.Kinds; k++)
                foreach (WearDef d in WearCatalog.Of((WearKind)k)) Add(k, d.Kind, d.Id, d.Name, d.Desc, d.Id, d.Id, d.Fx, d.Look, null);
            foreach (WearDef d in WearCatalog.Mixes()) if (!FxMix.IsTemp(d.Id)) Add(4, d.Kind, d.Id, d.Name, d.Desc, d.Id, "", null, d.Look, d.Mix);
            if (_showOther)
            {
                foreach (Look l in RavenLooks.AllLooks()) if (l.For != "rune") Add(5, WearKind.Freya, l.Id, l.Name, l.Desc, "mx_solo_f", l.Id, null, l, null);
                foreach (RavenFxDef d in FxLib.All()) if (!d.Rune) { WearKind k = d.Trail ? WearKind.Heimdall : WearKind.Baldr; Add(5, k, d.Id, d.Name, d.Desc, "mx_solo_" + WearCatalog.KindIds[(int)k], d.Id, d, null, null); }
            }
            _sel = null;
            foreach (Entry e in _entries) if (e.Cat + "|" + e.Id == sel) _sel = e;
            if (_sel == null && sel.Length > 0) { string id = sel.Substring(sel.IndexOf('|') + 1); foreach (Entry e in _entries) if (e.Id == id && e.Mix == null) { _sel = e; break; } }
        }

        private void Add(int cat, WearKind kind, string id, string name, string desc, string wearId, string sourceId, RavenFxDef fx, Look look, MixDef mix)
        {
            int n = 1;
            foreach (Entry x in _entries) if (x.Cat == cat) n++;
            _entries.Add(new Entry { Code = CatLetters[cat] + n, Cat = cat, Kind = kind, Id = id, Name = name, Desc = desc ?? "", WearId = wearId, SourceId = sourceId, Fx = fx, Look = look, Mix = mix });
        }

        private bool IsOn(Entry e)
        {
            if (_worn[(int)e.Kind] != e.WearId) return false;
            if (e.Cat != 5) return true;
            MixDef solo = FxMix.GetTemp(e.WearId);
            return solo != null && solo.Of.Count == 1 && solo.Of[0] == e.SourceId;
        }

        private void Toggle(Entry e)
        {
            if (_sel != e)
            {
                if (EdChanged && _pendingSel == null) { _pendingSel = e; return; }
                _sel = e; BeginEdit(e);
            }
            _confirmDelete = false;
            int k = (int)e.Kind;
            if (IsOn(e)) { _worn[k] = ""; _note = e.Code + " знято."; return; }
            if (e.Cat == 5)
            {
                MixDef solo = new MixDef { Name = e.Name, Kind = e.Kind };
                solo.Of.Add(e.SourceId); solo.Weight.Add(1f);
                FxMix.SetTemp(e.WearId, solo);
            }
            _worn[k] = e.WearId;
            if (e.Kind == WearKind.Heimdall && LookStage.Pose == 0) LookStage.Pose = 2; // a trail is seen only going round
            _note = e.Code + " «" + e.Name + "» на вороні (" + LayerTips[k] + ").";
        }

        private Entry BySource(string id)
        {
            foreach (Entry e in _entries) if (e.SourceId == id && e.Cat != 5) return e;
            foreach (Entry e in _entries) if (e.SourceId == id) return e;
            return null;
        }

        private Entry ByWear(int k)
        {
            string id = _worn[k];
            if (id.Length == 0) return null;
            if (id == FxMix.DraftId) return null;
            if (id.StartsWith("mx_solo_", System.StringComparison.Ordinal)) { MixDef s = FxMix.GetTemp(id); return s != null && s.Of.Count > 0 ? BySource(s.Of[0]) : null; }
            foreach (Entry e in _entries) if (e.WearId == id && (int)e.Kind == k) return e;
            return null;
        }

        // ---------------------------------------------------------------- the mix
        private void AddToMix(Entry e)
        {
            if (e.SourceId.Length == 0) return;
            bool look = e.Kind == WearKind.Freya, wasLook = _mix.Kind == WearKind.Freya;
            if (_mix.Of.Count == 0 || look != wasLook)
            {
                if (_mix.Of.Count > 0) _note = "Мікс почато наново: колір не міксується з ефектами.";
                MixDef n = new MixDef { Kind = e.Kind, Mode = _mix.Mode, Harmony = _mix.Harmony, Name = _mix.Name };
                ClearDraftWear();
                _mix = n;
            }
            else if (_mix.Kind != e.Kind) _note = e.Code + " — " + (e.Kind == WearKind.Baldr ? "аура" : "слід") + ", у міксі стане " + (_mix.Kind == WearKind.Baldr ? "аурою" : "слідом") + ".";
            if (_mix.Of.Contains(e.SourceId)) { _note = e.Code + " уже в міксі."; return; }
            if (_mix.Of.Count >= FxMix.MaxSources) { _note = "У міксі вже " + FxMix.MaxSources + " — прибери один (×)."; return; }
            _mix.Of.Add(e.SourceId); _mix.Weight.Add(1f);
            if (_mix.Of.Count < 2) _note = e.Code + " у міксі. Додай ще один (+) — і мікс одразу буде на вороні.";
            MixChanged();
        }

        // «+» puts it in, «✓» takes it out
        private void MixTick(Entry e)
        {
            int i = _mix.Of.IndexOf(e.SourceId);
            if (i < 0) { AddToMix(e); return; }
            _mix.Of.RemoveAt(i); if (i < _mix.Weight.Count) _mix.Weight.RemoveAt(i);
            _note = e.Code + " прибрано з міксу.";
            MixChanged();
        }

        private void ClearDraftWear() { for (int i = 0; i < _worn.Length; i++) if (_worn[i] == FxMix.DraftId) _worn[i] = ""; }

        // ---------------------------------------------------------------- the window
        internal void Body(float height)
        {
            if (_btn == null)
            {
                _btn = new GUIStyle(GUI.skin.button); _btn.richText = true;
                _on = new GUIStyle(_btn); _on.fontStyle = FontStyle.Bold; _on.normal.textColor = new Color(1f, 0.83f, 0.3f);
                _row = new GUIStyle(GUI.skin.button); _row.alignment = TextAnchor.MiddleLeft; _row.richText = true; _row.fontSize = 12; _row.wordWrap = false; _row.clipping = TextClipping.Clip;
                _rowOn = new GUIStyle(_row); _rowOn.fontStyle = FontStyle.Bold; _rowOn.normal.textColor = new Color(1f, 0.83f, 0.3f);
                _small = new GUIStyle(GUI.skin.label); _small.fontSize = 12; _small.richText = true; _small.wordWrap = true;
                _head = new GUIStyle(GUI.skin.label); _head.fontStyle = FontStyle.Bold; _head.richText = true; _head.normal.textColor = new Color(0.95f, 0.82f, 0.55f);
                _code = new GUIStyle(GUI.skin.textArea); _code.fontSize = 11; _code.wordWrap = true;
            }
            if (Player.m_localPlayer == null) { GUILayout.Label("Зайди у світ."); return; }
            Refresh();
            GUILayout.BeginHorizontal(GUILayout.Height(height));
            Left(height);
            Middle();
            Right(height);
            GUILayout.EndHorizontal();
        }

        private void Left(float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(330f));
            GUILayout.Label("<color=#9A9A9A>Клік — вдягти на ворона й правити праворуч (ще клік — зняти). «+» / «✓» — у мікс / з міксу. Називай мені ефект кодом: Г3, Б5…</color>", _small);
            _leftScroll = GUILayout.BeginScrollView(_leftScroll, GUILayout.Height(height - 70f));
            int cat = -1;
            foreach (Entry e in _entries)
            {
                if (e.Cat != cat)
                {
                    cat = e.Cat;
                    GUILayout.Space(cat == 0 ? 0f : 8f);
                    GUILayout.Label("<color=#" + CatColors[cat] + ">" + CatNames[cat] + "</color>  <color=#9A9A9A>— " + CatTips[cat] + "</color>", _head);
                }
                bool on = IsOn(e);
                GUILayout.BeginHorizontal();
                string kind = e.Cat >= 3 ? " <color=#9A9A9A>[" + WearCatalog.KindNames[(int)e.Kind] + "]</color>" : "";
                Element el = e.Cat == 4 ? Element.None : Elements.Of(e.Kind, e.SourceId.Length > 0 ? e.SourceId : e.Id);
                string label = "<b><color=#" + CatColors[e.Cat] + ">" + e.Code + "</color></b>  " + e.Name + kind + (el != Element.None ? "  " + Elements.Colored(el) : "") + "  <color=#7A7A7A>" + e.Id + "</color>";
                if (GUILayout.Button((on ? "● " : "") + label, on ? _rowOn : (_sel == e ? _rowOn : _row), GUILayout.Width(e.SourceId.Length > 0 ? 270f : 300f))) Toggle(e);
                if (e.SourceId.Length > 0 && GUILayout.Button(_mix.Of.Contains(e.SourceId) ? "✓" : "+", _mix.Of.Contains(e.SourceId) ? _on : _btn, GUILayout.Width(26f))) MixTick(e);
                GUILayout.EndHorizontal();
            }
            if (_entries.Count == 0) GUILayout.Label("Каталог порожній.", _small);
            GUILayout.EndScrollView();
            if (GUILayout.Button(_showOther ? "Сховати «Не руни»" : "Показати й «Не руни» (уся бібліотека)", _showOther ? _on : _btn)) _showOther = !_showOther;
            if (!RavenLooks.FromFiles) GUILayout.Label("<color=#FF9A7A>Файли (мікси, свої ефекти) читаються лише з «Outfits all for tuning» = true.</color>", _small);
            GUILayout.EndVertical();
        }

        private void Middle()
        {
            GUILayout.BeginVertical(GUILayout.Width(470f));
            Rect pic = GUILayoutUtility.GetRect(470f, 470f, GUILayout.Width(470f), GUILayout.Height(470f));
            if (LookStage.Texture != null) GUI.DrawTexture(pic, LookStage.Texture, ScaleMode.ScaleToFit, false);
            else GUI.Label(pic, "Сцена не готова: " + LookStage.Problem);
            Event ev = Event.current;
            if (ev.type == EventType.MouseDrag && pic.Contains(ev.mousePosition))
            {
                LookStage.Yaw += ev.delta.x * 0.6f;
                LookStage.Pitch = Mathf.Clamp(LookStage.Pitch + ev.delta.y * 0.4f, -85f, 85f);
                LookStage.Spin = false;
                ev.Use();
            }
            if (ev.type == EventType.ScrollWheel && pic.Contains(ev.mousePosition)) { LookStage.Zoom = Mathf.Clamp(LookStage.Zoom - ev.delta.y * 0.05f, 0.5f, 6f); ev.Use(); }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Хугін", !LookStage.Munin ? _on : _btn)) LookStage.Munin = false;
            if (GUILayout.Button("Мунін", LookStage.Munin ? _on : _btn)) LookStage.Munin = true;
            GUILayout.Space(8f);
            if (GUILayout.Button("Сидить", LookStage.Pose == 0 ? _on : _btn)) LookStage.Pose = 0;
            if (GUILayout.Button("Летить", LookStage.Pose == 1 ? _on : _btn)) LookStage.Pose = 1;
            if (GUILayout.Button("Кружляє", LookStage.Pose == 2 ? _on : _btn)) LookStage.Pose = 2;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Спереду")) { LookStage.Yaw = 0f; LookStage.Pitch = 5f; LookStage.Spin = false; }
            if (GUILayout.Button("Збоку")) { LookStage.Yaw = 90f; LookStage.Pitch = 5f; LookStage.Spin = false; }
            if (GUILayout.Button("Ззаду")) { LookStage.Yaw = 180f; LookStage.Pitch = 5f; LookStage.Spin = false; }
            if (GUILayout.Button("Згори")) { LookStage.Pitch = 80f; LookStage.Spin = false; }
            if (GUILayout.Button(LookStage.Spin ? "Обертати ✓" : "Обертати", LookStage.Spin ? _on : _btn)) LookStage.Spin = !LookStage.Spin;
            if (GUILayout.Button(LookStage.Studio ? "Світло: студія" : "Світло: як у світі")) LookStage.Studio = !LookStage.Studio;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Зараз на вороні", _head);
            for (int k = 0; k < WearCatalog.Kinds; k++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<color=#" + CatColors[k] + ">" + WearCatalog.KindNames[k] + "</color>", _small, GUILayout.Width(70f));
                string id = _worn[k];
                string what;
                if (id.Length == 0) what = "<color=#7A7A7A>нічого</color>";
                else if (id == FxMix.DraftId) what = "<b>мікс</b> «" + FxMix.ModeName(_mix) + "»: " + MixSources();
                else { Entry e = ByWear(k); what = e != null ? "<b>" + e.Code + "</b> " + e.Name + "  <color=#7A7A7A>" + e.Id + "</color>" : id + " <color=#FF9A7A>(нема в каталозі)</color>"; }
                GUILayout.Label(what, _small);
                if (id.Length > 0 && GUILayout.Button("×", GUILayout.Width(24f))) _worn[k] = "";
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зняти все")) for (int k = 0; k < _worn.Length; k++) _worn[k] = "";
            if (GUILayout.Button("На мого Хугіна")) _note = ToShoulder(false);
            if (GUILayout.Button("На мого Муніна")) _note = ToShoulder(true);
            GUILayout.EndHorizontal();
            if (_note.Length > 0) GUILayout.Label(_note, _small);
            GUILayout.EndVertical();
        }

        private string MixSources()
        {
            List<string> l = new List<string>();
            for (int i = 0; i < _mix.Of.Count; i++) { Entry e = BySource(_mix.Of[i]); l.Add(e != null ? e.Code : _mix.Of[i]); }
            return string.Join(" + ", l.ToArray());
        }

        // what the stage raven wears onto that raven on your shoulder (all three layers), as the console's raven wear
        private string ToShoulder(bool munin)
        {
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (p == null || item == null) return Plugin.RavenName(munin) + " не на плечі.";
            if (_mixDirty) PushMix();
            for (int k = 0; k < WearCatalog.Kinds; k++) RavenWear.Set(item, (WearKind)k, _worn[k]);
            ItemDrop.ItemData hugin, mun;
            RavenItem.Worn(p, out hugin, out mun);
            RavenWear.Publish(p, hugin, mun);
            RavenWear.Dress(Shoulder.LocalModel(munin), munin, RavenWear.Code(item));
            bool temp = false;
            foreach (string id in _worn) temp |= FxMix.IsTemp(id);
            return Plugin.RavenName(munin) + " на плечі вдягнув те саме." + (temp ? " (Незбережений мікс / «Не руна» — лише до перезапуску й лише в тебе.)" : "")
                + (Outfits.Tuning ? "" : " Без «Outfits all for tuning» ворон носить лише руни, які в тебе є.");
        }

        // ---------------------------------------------------------------- right: the clicked one, the mix
        private void Right(float height)
        {
            GUILayout.BeginVertical();
            _rightScroll = GUILayout.BeginScrollView(_rightScroll, GUILayout.Height(height - 10f));
            Details();
            GUILayout.Space(12);
            MixEditor();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void Details()
        {
            if (_pendingSel != null)
            {
                GUILayout.Label("<color=#FFB060>Не збережено зміни в «" + EdName() + "».</color>", _small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Зберегти")) { SaveEdit(); Entry p = _pendingSel; _pendingSel = null; Toggle(p); }
                if (GUILayout.Button("Кинути")) { DropEdit(); Entry p = _pendingSel; _pendingSel = null; Toggle(p); }
                if (GUILayout.Button("Лишитись")) _pendingSel = null;
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
            }
            if (_sel == null && _edFx == null && _edLook == null) { GUILayout.Label("Клікни ефект ліворуч — тут він відкриється для правки.", _small); return; }
            Entry e = _sel;
            if (e != null)
            {
                GUILayout.Label("<color=#" + CatColors[e.Cat] + ">" + e.Code + "</color>  " + e.Name, _head);
                GUILayout.Label("<color=#9A9A9A>" + CatNames[e.Cat] + (e.Cat >= 3 ? " · шар " + WearCatalog.KindNames[(int)e.Kind] : "") + " · " + LayerTips[(int)e.Kind] + " · id</color> " + e.Id, _small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(IsOn(e) ? "Зняти з ворона" : "Вдягти на ворона", IsOn(e) ? _on : _btn)) Toggle(e);
                if (e.SourceId.Length > 0 && GUILayout.Button(_mix.Of.Contains(e.SourceId) ? "Прибрати з міксу" : "+ у мікс", _mix.Of.Contains(e.SourceId) ? _on : _btn)) MixTick(e);
                if (e.Mix != null && GUILayout.Button("Відкрити в міксі")) { ClearDraftWear(); _mix = e.Mix.Copy(); MixChanged(); _note = "Мікс " + e.Code + " у редакторі: «Зберегти» перепише його файл."; }
                GUILayout.EndHorizontal();
            }
            if (e != null && e.Mix != null)
            {
                GUILayout.Label("Мікс «" + FxMix.ModeName(e.Mix) + "»" + (e.Mix.Harmony ? ", одна барва" : ""), _small);
                for (int i = 0; i < e.Mix.Of.Count; i++) { Entry s = BySource(e.Mix.Of[i]); GUILayout.Label("  " + (s != null ? "<b>" + s.Code + "</b> " + s.Name : e.Mix.Of[i] + " <color=#FF9A7A>(нема)</color>") + "  вага " + e.Mix.W(i).ToString("0.00", CultureInfo.InvariantCulture), _small); }
                if (e.Look != null) LookLines(e.Look);
                GUI.enabled = e.Mix.SavedAt != default(System.DateTime);
                if (GUILayout.Button(_confirmDelete ? "Точно видалити файл міксу?" : "Видалити мікс", _confirmDelete ? _on : _btn))
                {
                    if (_confirmDelete) { for (int k = 0; k < _worn.Length; k++) if (_worn[k] == e.Id) _worn[k] = ""; FxMix.Delete(e.Mix); _sel = null; _confirmDelete = false; _note = "Мікс видалено."; }
                    else _confirmDelete = true;
                }
                GUI.enabled = true;
                GUILayout.Label("<color=#9A9A9A>Як записано:</color>", _small); GUILayout.TextArea(e.Mix.Code().TrimEnd(), _code);
                return;
            }
            GUILayout.Space(6);
            if (_edFx != null) FxEditor();
            else if (_edLook != null) LookEditor();
        }

        // ---------------------------------------------------------------- the editor: the clicked one, changed live
        private RavenFxDef _edFx;
        private Look _edLook;
        private string _edSaved = "";
        private Entry _pendingSel;
        private bool _liveDirty;
        private float _liveAt;
        private int _zone = 1;
        private bool _basePick;
        private string _baseSearch = "", _baseSearchWas = null;
        private Vector2 _baseScroll;
        private readonly List<FxLib.Base> _bases = new List<FxLib.Base>();
        private readonly Dictionary<string, Vector3> _hsv = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, string> _hex = new Dictionary<string, string>();

        private string EdCode() { return _edFx != null ? _edFx.Code() : _edLook != null ? _edLook.Code() : ""; }
        private bool EdChanged { get { return (_edFx != null || _edLook != null) && EdCode() != _edSaved; } }
        private string EdName() { return _edFx != null ? _edFx.Name : _edLook != null ? _edLook.Name : ""; }
        private string EdId() { return _edFx != null ? _edFx.Id : _edLook != null ? _edLook.Id : ""; }

        private void BeginEdit(Entry e)
        {
            _edFx = null; _edLook = null; _basePick = false; _hsv.Clear(); _hex.Clear();
            if (e != null && e.Mix == null && e.Fx != null) _edFx = e.Fx.Copy();
            else if (e != null && e.Mix == null && e.Look != null) _edLook = e.Look.Copy();
            _edSaved = EdCode();
        }

        private void EdChangedNow() { _liveDirty = true; _liveAt = Time.unscaledTime; }

        // the edit on everything wearing its id (the stage raven, your ravens), a little after the last change
        private void PushLive()
        {
            _liveDirty = false;
            if (_edFx != null) FxLib.Live = EdChanged ? _edFx.Copy() : null;
            else if (_edLook != null) RavenLooks.Live = EdChanged ? _edLook.Copy() : null;
        }

        private void SaveEdit()
        {
            string id = EdId();
            if (_edFx != null) { FxLib.Live = null; FxLib.Save(_edFx.Copy()); _note = "Збережено: fx/" + id + ".txt"; }
            else if (_edLook != null) { RavenLooks.Live = null; RavenLooks.Save(_edLook.Copy()); _note = "Збережено: looks/" + id + ".txt"; }
            _edSaved = EdCode(); _liveDirty = false;
            if (!RavenLooks.FromFiles) _note += " <color=#FF9A7A>Але без «Outfits all for tuning» = true гра файли не читає!</color>";
        }

        private void DropEdit()
        {
            FxLib.Live = null; RavenLooks.Live = null; _liveDirty = false;
            string id = EdId();
            if (_edFx != null) { RavenFxDef d = FxLib.Find(id); if (d != null) _edFx = d.Copy(); }
            else if (_edLook != null) { Look l = FxMix.FindLook(id); if (l != null) _edLook = l.Copy(); }
            _edSaved = EdCode(); _hsv.Clear(); _hex.Clear();
        }

        private void ResetToMod()
        {
            string id = EdId();
            FxLib.Live = null; RavenLooks.Live = null; _liveDirty = false;
            if (_edFx != null) { FxLib.Delete(_edFx); RavenFxDef d = FxLib.Find(id); if (d != null) _edFx = d.Copy(); }
            else if (_edLook != null) { RavenLooks.Delete(_edLook); Look l = FxMix.FindLook(id); if (l != null) _edLook = l.Copy(); }
            _edSaved = EdCode(); _hsv.Clear(); _hex.Clear();
            _note = "Файл прибрано: «" + EdName() + "» знову як у моді.";
        }

        private void SaveRow(bool file, bool builtin)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = EdChanged;
            if (GUILayout.Button(EdChanged ? "<b>Зберегти</b> •" : "Збережено", _btn, GUILayout.Height(28f))) SaveEdit();
            if (GUILayout.Button("Скасувати", GUILayout.Height(28f))) DropEdit();
            GUI.enabled = file && builtin;
            if (GUILayout.Button("Як у моді", GUILayout.Height(28f))) ResetToMod();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#9A9A9A>Ворон міняється одразу, поки крутиш. «Зберегти» пише файл" + (file ? " (є свій файл)" : "") + "; «Як у моді» прибирає твій файл.</color>", _small);
        }

        private void FxEditor()
        {
            RavenFxDef d = _edFx;
            SaveRow(FxLib.HasFile(d.Id), FxLib.IsBuiltin(d.Id));
            string was = d.Code();
            Field("Назва", ref d.Name);
            Field("Опис", ref d.Desc);
            ElementRow(ref d.Element, d.Trail ? WearKind.Heimdall : WearKind.Baldr, d.Id);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Основа: <b>" + FxLib.LabelOf(d.Base) + "</b>" + (FxLib.HasLight(d.Base) ? " <color=#9A9A9A>(зі світлом)</color>" : ""), _small);
            if (GUILayout.Button(_basePick ? "Сховати ▴" : "Змінити ▾", _basePick ? _on : _btn, GUILayout.Width(90f))) _basePick = !_basePick;
            GUILayout.EndHorizontal();
            if (_basePick) BasePicker(d);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Аура (Бальдр)", !d.Trail ? _on : _btn) && d.Trail) SwitchKind(d, "aura");
            if (GUILayout.Button("Слід (Геймдаль)", d.Trail ? _on : _btn) && !d.Trail) SwitchKind(d, "trail");
            GUILayout.EndHorizontal();

            GUILayout.Label("Де на вороні", _small);
            GUILayout.BeginHorizontal();
            for (int b = 0; b < LookThings.BoneIds.Length; b++)
            {
                if (b > 0 && b % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (GUILayout.Button(LookThings.BoneNames[b], d.Bone == LookThings.BoneIds[b] ? _on : _btn)) d.Bone = LookThings.BoneIds[b];
            }
            GUILayout.EndHorizontal();
            d.At.z = Axis("Вперед", d.At.z);
            d.At.y = Axis("Вгору", d.At.y);
            d.At.x = Axis("Вбік", d.At.x);

            GUILayout.Space(4);
            if (GUILayout.Button(d.Tinted ? "Свій колір ✓" : "Колір як у грі", d.Tinted ? _on : _btn)) d.Tinted = !d.Tinted;
            if (d.Tinted) ColorEdit("fx", ref d.Tint);
            d.Size = Slider("Розмір", d.Size, 0.1f, 5f, 1f);
            d.Amount = Slider("Кількість", d.Amount, 0f, 5f, 1f);
            d.Speed = Slider("Швидкість", d.Speed, 0.1f, 3f, 1f);
            d.Life = Slider("Довше живе", d.Life, 0.2f, 3f, 1f);
            if (d.Code() != was) EdChangedNow();
            GUILayout.Label("<color=#9A9A9A>Як записано:</color>", _small);
            GUILayout.TextArea(d.Code().TrimEnd(), _code);
        }

        // an aura made a trail (or back): it changes layer, so what the stage wears moves with it
        private void SwitchKind(RavenFxDef d, string kind)
        {
            int from = d.Trail ? (int)WearKind.Heimdall : (int)WearKind.Baldr;
            d.Kind = kind;
            int to = d.Trail ? (int)WearKind.Heimdall : (int)WearKind.Baldr;
            if (_worn[from] == d.Id) { _worn[from] = ""; _worn[to] = d.Id; }
            if (d.Trail && LookStage.Pose == 0) LookStage.Pose = 2;
            _note = "Тепер це " + (d.Trail ? "слід (Геймдаль)" : "аура (Бальдр)") + " — після збереження воно в тому списку.";
        }

        private void BasePicker(RavenFxDef d)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Пошук", _small, GUILayout.Width(44f));
            _baseSearch = GUILayout.TextField(_baseSearch ?? "");
            if (GUILayout.Button("×", GUILayout.Width(24f))) _baseSearch = "";
            GUILayout.EndHorizontal();
            if (_baseSearch != _baseSearchWas)
            {
                _baseSearchWas = _baseSearch;
                _bases.Clear();
                string[] words = (_baseSearch ?? "").ToLowerInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (FxLib.Base b in FxLib.Bases())
                {
                    string l = b.Label.ToLowerInvariant();
                    bool ok = true;
                    foreach (string w in words) if (!l.Contains(w)) { ok = false; break; }
                    if (ok) _bases.Add(b);
                }
                _baseScroll = Vector2.zero;
            }
            const float row = 22f, h = 200f;
            _baseScroll = GUILayout.BeginScrollView(_baseScroll, GUILayout.Height(h));
            Rect all = GUILayoutUtility.GetRect(200f, row * _bases.Count);
            int first = Mathf.Max(0, Mathf.FloorToInt(_baseScroll.y / row) - 1), last = Mathf.Min(_bases.Count - 1, first + Mathf.CeilToInt(h / row) + 2);
            for (int i = first; i <= last; i++)
            {
                FxLib.Base b = _bases[i];
                string tag = b.Src.StartsWith(FxLib.OursPrefix, System.StringComparison.Ordinal) ? "наше" : (b.Loop ? "живий" : "спалах") + (b.Lights > 0 ? " · світло" : "");
                if (GUI.Button(new Rect(all.x, all.y + i * row, all.width, row - 2f), b.Label + "  <color=#9A9A9A>" + tag + "</color>", d.Base == b.Src ? _rowOn : _row)) d.Base = b.Src;
            }
            GUILayout.EndScrollView();
        }

        private void LookEditor()
        {
            Look l = _edLook;
            SaveRow(RavenLooks.HasFile(l.Id), RavenLooks.IsBuiltin(l.Id));
            string was = l.Code();
            Field("Назва", ref l.Name);
            Field("Опис", ref l.Desc);
            ElementRow(ref l.Element, WearKind.Freya, l.Id);
            GUILayout.Label("Зони <color=#9A9A9A>(клік по назві — правити її)</color>", _head);
            for (int z = 1; z < l.Zones.Length; z++)
            {
                Look.Part p = l.Zones[z];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.On ? "✓" : "–", p.On ? _on : _btn, GUILayout.Width(26f)))
                {
                    p.On = !p.On;
                    if (p.On && p.Dark == Color.black && p.Light == Color.white) { p.Dark = new Color(0.05f, 0.05f, 0.06f); p.Light = new Color(0.5f, 0.5f, 0.55f); }
                }
                if (GUILayout.Button(LookDiag.ZoneNames[z], _zone == z ? _on : _btn, GUILayout.Width(110f))) _zone = z;
                if (p.On) { Swatch(p.Dark); Swatch(p.Light); GUILayout.Label("<color=#9A9A9A>сила " + Mathf.RoundToInt(p.Mix * 100f) + "%</color>", _small); }
                else GUILayout.Label("<color=#7A7A7A>як у грі</color>", _small);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            Look.Part cur = l.Zones[_zone];
            GUILayout.Label("«" + LookDiag.ZoneNames[_zone] + "»", _head);
            if (cur.On)
            {
                GUILayout.Label("Тінь (темні пір'їни)", _small); ColorEdit("z" + _zone + "d", ref cur.Dark);
                GUILayout.Label("Світло (світлі пір'їни)", _small); ColorEdit("z" + _zone + "l", ref cur.Light);
                cur.Mix = Slider("Сила", cur.Mix, 0f, 1f, 1f);
                cur.Relief = Slider("Рельєф", cur.Relief, 0f, 1f, 0.8f);
            }
            else GUILayout.Label("<color=#9A9A9A>Ця зона як у грі. Постав ✓, щоб фарбувати.</color>", _small);

            GUILayout.Space(6);
            Part("Очі (світіння)", ref l.EyesOn);
            if (l.EyesOn) { ColorEdit("eyes", ref l.Eyes); l.EyePower = Slider("Сила", l.EyePower, 0f, 12f, 4f); }
            Part("Світіння тіла", ref l.GlowOn);
            if (l.GlowOn) { ColorEdit("glow", ref l.Glow); l.GlowPower = Slider("Сила", l.GlowPower, 0f, 4f, 0.5f); }
            Part("Полиск пір'я", ref l.SheenOn);
            if (l.SheenOn) { ColorEdit("sheen", ref l.Sheen); l.Metal = Slider("Метал", l.Metal, 0f, 1f, 0.5f); l.MetalGloss = Slider("Блиск металу", l.MetalGloss, 0f, 1f, 0.7f); l.Gloss = Slider("Гладкість", l.Gloss, 0f, 1f, 0.2f); }
            Part("Чарівні іскри", ref l.SparksOn);
            if (l.SparksOn) { ColorEdit("sparks", ref l.Sparks); l.SparkPower = Slider("Сила", l.SparkPower, 0f, 8f, 2f); l.SparkSize = Slider("Розмір", l.SparkSize, 0.3f, 12f, 3f); l.SparkEdge = Slider("Край", l.SparkEdge, 0f, 1f, 0.2f); }
            if (l.Code() != was) EdChangedNow();
            GUILayout.Label("<color=#9A9A9A>Як записано:</color>", _small);
            GUILayout.TextArea(l.Code(false).TrimEnd(), _code);
        }

        // the element (the в'язь counts by it): the catalog's own unless chosen here
        private void ElementRow(ref string el, WearKind k, string id)
        {
            Element now = Elements.Parse(el);
            Element shown = now != Element.None ? now : Elements.Of(k, id);
            GUILayout.Label("Стихія: " + Elements.Colored(shown) + (now == Element.None ? " <color=#9A9A9A>(з таблиці)</color>" : ""), _small);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Elements.Count; i++)
            {
                if (i == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (GUILayout.Button("<color=#" + Elements.Hex[i] + ">" + Elements.NameOf((Element)i) + "</color>", (int)shown == i ? _on : _btn)) el = Elements.Ids[i];
            }
            if (GUILayout.Button("з таблиці", now == Element.None ? _on : _btn)) el = "";
            GUILayout.EndHorizontal();
        }

        private void Part(string name, ref bool on)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(on ? "✓" : "–", on ? _on : _btn, GUILayout.Width(26f))) on = !on;
            GUILayout.Label(name + (on ? "" : "  <color=#7A7A7A>як у грі</color>"), _head);
            GUILayout.EndHorizontal();
        }

        private void Field(string label, ref string v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _small, GUILayout.Width(44f));
            string n = GUILayout.TextField(v ?? "");
            if (n != v) v = n;
            GUILayout.EndHorizontal();
        }

        private float Slider(string label, float v, float min, float max, float def)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _small, GUILayout.Width(96f));
            float n = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(150f));
            GUILayout.Label(N(n), _small, GUILayout.Width(40f));
            if (GUILayout.Button(N(def), GUILayout.Width(34f))) n = def;
            GUILayout.EndHorizontal();
            return n;
        }

        private float Axis(string label, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _small, GUILayout.Width(60f));
            if (GUILayout.Button("−−", GUILayout.Width(30f))) v -= 0.02f;
            if (GUILayout.Button("−", GUILayout.Width(26f))) v -= 0.005f;
            GUILayout.Label(v.ToString("0.000", CultureInfo.InvariantCulture), _small, GUILayout.Width(50f));
            if (GUILayout.Button("+", GUILayout.Width(26f))) v += 0.005f;
            if (GUILayout.Button("++", GUILayout.Width(30f))) v += 0.02f;
            if (GUILayout.Button("0", GUILayout.Width(26f))) v = 0f;
            GUILayout.EndHorizontal();
            return Mathf.Round(v * 1000f) / 1000f;
        }

        // feathers: black, soot, ash, silver, snow, bone, gold, copper, rust, crimson, blood, violet, night blue, sky, teal, moss
        private static readonly Color[] Swatches =
        {
            new Color(0.03f, 0.03f, 0.035f), new Color(0.12f, 0.12f, 0.13f), new Color(0.35f, 0.35f, 0.37f), new Color(0.72f, 0.74f, 0.78f),
            new Color(0.96f, 0.96f, 0.94f), new Color(0.85f, 0.78f, 0.62f), new Color(0.95f, 0.75f, 0.25f), new Color(0.72f, 0.42f, 0.2f),
            new Color(0.5f, 0.22f, 0.1f), new Color(0.7f, 0.08f, 0.12f), new Color(0.35f, 0.02f, 0.04f), new Color(0.35f, 0.15f, 0.55f),
            new Color(0.08f, 0.1f, 0.3f), new Color(0.35f, 0.6f, 0.95f), new Color(0.15f, 0.5f, 0.5f), new Color(0.25f, 0.4f, 0.15f),
        };

        // a colour: ready swatches, hex, hue / saturation / brightness (the hue kept while the colour is grey or black)
        private void ColorEdit(string key, ref Color c)
        {
            Vector3 hsv;
            if (!_hsv.TryGetValue(key, out hsv) || !Near(Color.HSVToRGB(hsv.x, hsv.y, hsv.z), c)) { float h0, s0, v0; Color.RGBToHSV(c, out h0, out s0, out v0); hsv = new Vector3(h0, s0, v0); }
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Swatches.Length; i++)
            {
                if (GUILayout.Button("", GUILayout.Width(16f), GUILayout.Height(16f))) { c = Swatches[i]; float h1, s1, v1; Color.RGBToHSV(c, out h1, out s1, out v1); hsv = new Vector3(h1, s1, v1); _hex.Remove(key); }
                Rect r = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.Repaint) { Color was = GUI.color; GUI.color = Swatches[i]; GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), Texture2D.whiteTexture); GUI.color = was; }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Swatch(c);
            string hexNow = Look.Hex(c), shown;
            if (!_hex.TryGetValue(key, out shown)) shown = hexNow;
            string typed = GUILayout.TextField(shown, 6, GUILayout.Width(70f));
            Color parsed;
            if (typed != shown)
            {
                _hex[key] = typed;
                if (typed.Length == 6 && ColorUtility.TryParseHtmlString("#" + typed, out parsed)) { c = parsed; float h2, s2, v2; Color.RGBToHSV(c, out h2, out s2, out v2); hsv = new Vector3(h2, s2, v2); }
            }
            GUILayout.Label("<color=#9A9A9A>hex</color>", _small);
            GUILayout.EndHorizontal();
            float h = HsvRow("Відтінок", hsv.x), s = HsvRow("Насиченість", hsv.y), v = HsvRow("Яскравість", hsv.z);
            if (h != hsv.x || s != hsv.y || v != hsv.z) { hsv = new Vector3(h, s, v); c = Color.HSVToRGB(h, s, v); _hex.Remove(key); }
            c.a = 1f;
            _hsv[key] = hsv;
        }

        private static bool Near(Color a, Color b) { return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f; }

        private float HsvRow(string label, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _small, GUILayout.Width(96f));
            float n = GUILayout.HorizontalSlider(v, 0f, 1f, GUILayout.Width(190f));
            GUILayout.Label(Mathf.RoundToInt(n * 100f) + "%", _small, GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return n;
        }

        private void LookLines(Look l)
        {
            for (int z = 1; z < l.Zones.Length; z++)
            {
                Look.Part p = l.Zones[z];
                GUILayout.BeginHorizontal();
                GUILayout.Label(LookDiag.ZoneNames[z], _small, GUILayout.Width(96f));
                if (p.On) { Swatch(p.Dark); Swatch(p.Light); GUILayout.Label("<color=#9A9A9A>" + Look.Hex(p.Dark) + " → " + Look.Hex(p.Light) + " · сила " + Mathf.RoundToInt(p.Mix * 100f) + "% · рельєф " + Mathf.RoundToInt(p.Relief * 100f) + "%</color>", _small); }
                else GUILayout.Label("<color=#7A7A7A>як у грі</color>", _small);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            Extra("Очі", l.EyesOn, l.Eyes, "сила " + N(l.EyePower));
            Extra("Світіння", l.GlowOn, l.Glow, "сила " + N(l.GlowPower));
            Extra("Полиск", l.SheenOn, l.Sheen, "метал " + N(l.Metal) + " · блиск " + N(l.MetalGloss) + " · гладкість " + N(l.Gloss));
            Extra("Іскри", l.SparksOn, l.Sparks, "сила " + N(l.SparkPower) + " · розмір " + N(l.SparkSize));
        }

        private void Extra(string name, bool on, Color c, string more)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _small, GUILayout.Width(96f));
            if (on) { Swatch(c); GUILayout.Label("<color=#9A9A9A>" + Look.Hex(c) + " · " + more + "</color>", _small); }
            else GUILayout.Label("<color=#7A7A7A>як у грі</color>", _small);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void MixEditor()
        {
            GUILayout.Label("Мікс" + (_mix.Of.Count > 0 ? "  <color=#9A9A9A>· шар " + WearCatalog.KindNames[(int)_mix.Kind] + " (" + LayerTips[(int)_mix.Kind] + ")</color>" : ""), _head);
            if (_mix.Of.Count == 0) { GUILayout.Label("<color=#9A9A9A>Натисни «+» біля 2–3 ефектів одного шару (два сліди, дві аури або два кольори) — система сама гарно накладе їх на ворона.</color>", _small); return; }
            int drop = -1;
            for (int i = 0; i < _mix.Of.Count; i++)
            {
                Entry e = BySource(_mix.Of[i]);
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>" + (char)('A' + i) + "</b>", _small, GUILayout.Width(16f));
                GUILayout.Label(e != null ? "<b>" + e.Code + "</b> " + e.Name : _mix.Of[i], _small, GUILayout.Width(170f));
                GUILayout.Label("вага", _small, GUILayout.Width(32f));
                float w = GUILayout.HorizontalSlider(_mix.W(i), 0f, 1f, GUILayout.Width(90f));
                GUILayout.Label(w.ToString("0.00", CultureInfo.InvariantCulture), _small, GUILayout.Width(34f));
                if (Mathf.Abs(w - _mix.W(i)) > 0.001f) { _mix.Weight[i] = w; MixChanged(); }
                if (GUILayout.Button("×", GUILayout.Width(24f))) drop = i;
                GUILayout.EndHorizontal();
            }
            if (drop >= 0) { _mix.Of.RemoveAt(drop); if (drop < _mix.Weight.Count) _mix.Weight.RemoveAt(drop); MixChanged(); }
            if (_mix.Of.Count < 2) GUILayout.Label("<color=#FFB060>Додай ще один (+) — тоді мікс з'явиться на вороні.</color>", _small);

            bool look = _mix.Kind == WearKind.Freya;
            string[] modes = look ? FxMix.LookModes : FxMix.FxModes, tips = look ? FxMix.LookModeTips : FxMix.FxModeTips;
            GUILayout.Label("Як накласти", _small);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < modes.Length; i++) if (GUILayout.Button(modes[i], _mix.Mode == i ? _on : _btn)) { _mix.Mode = i; MixChanged(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#9A9A9A>" + tips[Mathf.Clamp(_mix.Mode, 0, tips.Length - 1)] + "</color>", _small);
            if (!look && GUILayout.Button(_mix.Harmony ? "Одна барва ✓ (кольори зближені)" : "Одна барва: ні (кожен свій колір)", _mix.Harmony ? _on : _btn)) { _mix.Harmony = !_mix.Harmony; MixChanged(); }
            if (!look) GUILayout.Label("<color=#9A9A9A>Кількість ділиться на всіх (разом ≈1.4 одного), світло лишається одне.</color>", _small);
            GUILayout.Label("<color=#9A9A9A>Дерево плете так само: зелене — «" + (look ? FxMix.LookModes[0] : FxMix.FxModes[0]) + "», крижане — «" + (look ? FxMix.LookModes[1] : FxMix.FxModes[1]) + "», горіле — «" + (look ? FxMix.LookModes[2] : FxMix.FxModes[2]) + "»" + (look ? "" : ", завжди з «Одна барва»") + ", ваги всі 1.</color>", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_worn[(int)_mix.Kind] == FxMix.DraftId ? "Мікс на вороні ✓" : "Вдягти мікс", _worn[(int)_mix.Kind] == FxMix.DraftId ? _on : _btn) && _mix.Of.Count >= 2) { PushMix(); }
            if (GUILayout.Button("Очистити")) { ClearDraftWear(); FxMix.Draft = null; _mix = new MixDef { Mode = _mix.Mode }; _note = "Мікс очищено."; }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Назва", _small, GUILayout.Width(44f));
            string n = GUILayout.TextField(_mix.Name);
            if (n != _mix.Name) _mix.Name = n;
            GUILayout.EndHorizontal();
            GUI.enabled = _mix.Of.Count >= 2 && _mix.Name.Trim().Length > 0;
            bool has = _mix.Id.Length > 0 && !FxMix.IsTemp(_mix.Id);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(has ? "<b>Зберегти</b> (переписати " + _mix.Id + ")" : "<b>Зберегти мікс</b>", _btn, GUILayout.Height(26f))) SaveMix(false);
            if (has && GUILayout.Button("Як новий", GUILayout.Height(26f))) SaveMix(true);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Label("<color=#9A9A9A>Код міксу:</color>", _small);
            GUILayout.TextArea(_mix.Code().TrimEnd(), _code);
        }

        private void SaveMix(bool asNew)
        {
            MixDef m = _mix.Copy();
            if (asNew || FxMix.IsTemp(m.Id)) m.Id = "";
            FxMix.Save(m);
            _mix.Id = m.Id;
            // what wore the draft now wears the saved one (the same thing, by its own id)
            for (int k = 0; k < _worn.Length; k++) if (_worn[k] == FxMix.DraftId) _worn[k] = m.Id;
            FxMix.Draft = null;
            _note = "Збережено: mixes/" + m.Id + ".txt — у списку «Мої мікси».";
        }

        private void Swatch(Color c)
        {
            Rect r = GUILayoutUtility.GetRect(18f, 16f, GUILayout.Width(18f), GUILayout.Height(16f));
            Color was = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, 1f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = was;
        }

        private static string N(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    }
}
#endif
