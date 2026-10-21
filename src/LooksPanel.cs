#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The test window's tab «Фарби»: make and tune raven looks (RavenLooks). Left — the looks (files, built-in);
    // middle — the raven on its own stage (drag to turn, wheel to zoom; sitting or flying; the look / its zones / the
    // game's own); right — every zone's colours, the eyes, the body's glow, the sheen, the magic sparks. «Зберегти»
    // writes config/RavenWatch/looks/<id>.txt; the Guide takes a saved look with «для: Провідник» at once.
    internal class LooksPanel : MonoBehaviour
    {
        private GUIStyle _on, _btn, _small, _head;
        private List<Look> _files = new List<Look>();
        private Look _cur;
        private string _savedCode = "";
        private bool _builtinPicked;
        private int _zone = (int)LookDiag.Zone.Body;
        private int _show; // 0 the look, 1 the zones, 2 the game's own
        private readonly RavenLooks.Painted _live = new RavenLooks.Painted();
        private bool _dirty = true, _paintedOk;
        private float _paintAt;
        private int _paints, _shown = -1;
        private Material[] _shownFor;
        private Material[] _zoneMats;
        private bool _zoneMatsMunin;
        private GameObject _world;
        private SkinnedMeshRenderer _worldSkin;
        private Material[] _worldOrig;
        private Vector2 _leftScroll, _rightScroll;
        private string _note = "";
        private Look.Part _clip;
        private readonly Dictionary<string, Vector3> _hsv = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, string> _fields = new Dictionary<string, string>();

        // «Речі»: the catalog on the left (as the Workshop's), the things hung on the raven and their places on the right
        private int _mode; // 0 «Вигляди» (colours, which effects), 1 «Речі», 2 «Ефекти» (our library)
        private bool _more; // the look's rarer settings (sheen, sparks, the code) shown
        private int _thing = -1;

        // «Ефекти»: our library on the left with the game's VFX to start from; the effect on the raven; its tuning on the right
        private RavenFxDef _fx;
        private string _fxSaved = "", _fxTuned = "", _lookFxKind = "", _fxFailed = "";
        private FxOn _fxOn;
        private string _fxSearch = "", _fxSearchWas = null;
        private int _fxCat, _fxCatWas = -1;
        private Vector2 _fxScroll, _fxLibScroll;
        private readonly List<FxLib.Base> _fxShown = new List<FxLib.Base>();
        private static readonly string[] FxCatNames = { "Наші листи", "Живі", "Спалахи vfx", "Усі" };
        private string _search = "", _searchWas = null;
        private int _cat, _catWas = -1;
        private Vector2 _catScroll;
        private readonly List<string> _shownSrc = new List<string>();
        private readonly List<string> _shownLabel = new List<string>();
        private readonly List<Sprite> _shownIcon = new List<Sprite>();
        private string _stageKind = "", _worldKind = "";
        private GUIStyle _rowL;
        private static readonly string[] CatNames = { "Речі", "Будівлі", "Світ", "Істоти", "Ассети", "Моделі" }; // (effects: the «Ефекти» mode, our library)
        private static readonly int[] CatIds = { WsCatalog.CatItem, WsCatalog.CatBuild, WsCatalog.CatWorld, WsCatalog.CatCreature, WsCatalog.CatAsset, -1 };

        private static readonly string[] ForIds = { "guide", "hugin", "munin", "any", "rune" };
        private static readonly string[] ForNames = { "Провідник", "Хугін", "Мунін", "будь-який", "Руна Фреї" };
        // feathers: black, soot, ash, silver, snow, bone, sand, gold, copper, rust, crimson, blood, wine, violet, night blue, sky, teal, moss
        private static readonly Color[] Swatches =
        {
            new Color(0.03f, 0.03f, 0.035f), new Color(0.12f, 0.12f, 0.13f), new Color(0.35f, 0.35f, 0.37f), new Color(0.72f, 0.74f, 0.78f),
            new Color(0.96f, 0.96f, 0.94f), new Color(0.85f, 0.78f, 0.62f), new Color(0.75f, 0.62f, 0.4f), new Color(0.95f, 0.75f, 0.25f),
            new Color(0.72f, 0.42f, 0.2f), new Color(0.5f, 0.22f, 0.1f), new Color(0.7f, 0.08f, 0.12f), new Color(0.35f, 0.02f, 0.04f),
            new Color(0.3f, 0.05f, 0.18f), new Color(0.35f, 0.15f, 0.55f), new Color(0.08f, 0.1f, 0.3f), new Color(0.35f, 0.6f, 0.95f),
            new Color(0.15f, 0.5f, 0.5f), new Color(0.25f, 0.4f, 0.15f),
        };

        // ---------------------------------------------------------------- the tab shown / left
        internal void Opened()
        {
            enabled = true;
            Reload();
            if (_cur == null) Pick(RavenLooks.ForGuide() ?? new Look { Name = "Новий вигляд" }, true);
            _dirty = true;
        }

        internal void Closed()
        {
            enabled = false;
            if (LookStage.Model != null) FxLib.Unhang(LookStage.Model);
            LookStage.Close();
            RemoveWorld();
            _stageKind = ""; _lookFxKind = ""; _fxOn = null;
        }

        private void Reload() { _files = RavenLooks.FileLooks(); }

        private void Pick(Look l, bool builtin)
        {
            _cur = l.Copy();
            _cur.Id = l.Id;
            _savedCode = _cur.Code();
            _builtinPicked = builtin;
            _dirty = true;
            _thing = _cur.Things.Count > 0 ? 0 : -1;
        }

        private bool Changed { get { return _cur != null && _cur.Code() != _savedCode; } }

        // ---------------------------------------------------------------- every frame: paint when changed, film
        private void Update()
        {
            if (_cur == null) return;
            if (!LookStage.Ready(LookStage.Munin)) return;
            if (_dirty && Time.unscaledTime - _paintAt > 0.12f)
            {
                _dirty = false;
                _paintAt = Time.unscaledTime;
                _paintedOk = RavenLooks.Paint(_live, _cur, LookStage.Munin);
                if (!_paintedOk) _note = "Не вдалося пофарбувати: " + RavenLooks.Problem;
                _paints++;
            }
            // the materials are put on only when something changed (the copies are repainted in place)
            int want = _paints * 8 + _show * 2 + (LookStage.Munin ? 1 : 0);
            if (want != _shown || LookStage.Originals != _shownFor)
            {
                _shown = want; _shownFor = LookStage.Originals;
                Material[] mats = null;
                if (_show == 0 && _paintedOk) mats = RavenLooks.Dress(_live, LookStage.Originals);
                else if (_show == 1) mats = ZoneMats();
                LookStage.Show(mats);
                if (_world != null && _worldSkin != null) _worldSkin.sharedMaterials = _paintedOk && _live.Munin == WorldMunin ? RavenLooks.Dress(_live, _worldOrig) : _worldOrig;
            }
            SyncThings(LookStage.Model, LookStage.Munin, LookStage.Layer, ref _stageKind);
            if (_world != null) SyncThings(_world, WorldMunin, _world.layer, ref _worldKind);
            SyncFx(LookStage.Model, LookStage.Munin, LookStage.Layer);
            LookStage.Render();
        }

        // On the stage: in «Ефекти» only the effect being made (a trail on a raven going round); else what the look wears
        private void SyncFx(GameObject raven, bool munin, int layer)
        {
            if (raven == null) return;
            if (_mode == 2)
            {
                if (_lookFxKind.Length > 0) { FxLib.Unhang(raven); _lookFxKind = ""; _fxOn = null; }
                if (_fx == null || string.IsNullOrEmpty(_fx.Base)) { if (_fxOn != null) { _fxOn.Off(); _fxOn = null; } return; }
                string want = raven.GetInstanceID() + "|" + _fx.Shape();
                if (_fxOn == null || _fxOn.Raven != raven || _fxOn.Shape != _fx.Shape())
                {
                    if (want == _fxFailed) return; // (tried: not again every frame)
                    if (_fxOn != null) _fxOn.Off();
                    _fxOn = FxLib.Put(raven, _fx, munin, layer);
                    _fxTuned = _fx.Code();
                    if (_fxOn == null) { _fxFailed = want; _note = "Цей ефект не вдалося зібрати: у нього нема частинок чи світла, або це шматок логіки гри."; }
                    else _fxFailed = "";
                    return;
                }
                string code = _fx.Code();
                if (code != _fxTuned) { _fxTuned = code; _fxOn.Tune(_fx, false); }
                return;
            }
            if (_fxOn != null) { _fxOn.Off(); _fxOn = null; }
            string kind = raven.GetInstanceID() + "|" + FxLib.Version + "|" + string.Join(",", _cur.Fx.ToArray());
            if (kind == _lookFxKind) return;
            _lookFxKind = kind;
            try { FxLib.Hang(raven, _cur.Fx, munin, layer); }
            catch (System.Exception e) { _note = "Ефекти не вдалося показати: " + e.Message; Perches.Log("look fx: " + e); }
        }

        // the look's things on a raven: built anew when what they are changes, else only moved (each frame, cheap)
        private void SyncThings(GameObject raven, bool munin, int layer, ref string was)
        {
            if (raven == null) return;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(raven.GetInstanceID()).Append(munin ? "m" : "h");
            foreach (Look.Thing t in _cur.Things) sb.Append('#').Append(t.Kind());
            string kind = sb.ToString();
            if (kind != was)
            {
                was = kind;
                try { LookThings.Hang(raven, _cur, munin, layer); }
                catch (System.Exception e) { _note = "Не вдалося повісити: " + e.Message; Perches.Log("look things: " + e); }
                return;
            }
            foreach (LookThings.Tag tag in raven.GetComponentsInChildren<LookThings.Tag>(true))
                if (tag.Index < _cur.Things.Count) LookThings.Place(tag.transform, _cur.Things[tag.Index]);
        }

        private Material[] ZoneMats()
        {
            if (_zoneMats != null && _zoneMatsMunin == LookStage.Munin) return _zoneMats;
            FreeZoneMats();
            Texture2D t = RavenLooks.ZonePic(LookStage.Munin);
            if (t == null) return null;
            Material[] o = LookStage.Originals;
            _zoneMats = new Material[o.Length];
            for (int i = 0; i < o.Length; i++)
            {
                if (!RavenLooks.IsBody(o[i])) { _zoneMats[i] = o[i]; continue; }
                Material m = new Material(o[i]); m.hideFlags = HideFlags.HideAndDontSave;
                m.mainTexture = t;
                if (m.HasProperty("_Color")) m.color = Color.white;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                _zoneMats[i] = m;
            }
            _zoneMatsMunin = LookStage.Munin;
            return _zoneMats;
        }

        private void FreeZoneMats()
        {
            if (_zoneMats != null && LookStage.Originals != null)
                for (int i = 0; i < _zoneMats.Length; i++) if (_zoneMats[i] != null && System.Array.IndexOf(LookStage.Originals, _zoneMats[i]) < 0) Destroy(_zoneMats[i]);
            _zoneMats = null;
        }

        // ---------------------------------------------------------------- «Показати у світі»
        private bool WorldMunin;
        private void ToggleWorld()
        {
            if (_world != null) { RemoveWorld(); _note = "Ворона зі світу прибрано."; return; }
            Player p = Player.m_localPlayer;
            Raven src = Shoulder.Template(LookStage.Munin);
            if (p == null || src == null || src.m_visual == null) return;
            WorldMunin = LookStage.Munin;
            _world = Instantiate(src.m_visual);
            _world.name = "RavenWatch_LookInWorld";
            AppearMute.Quiet(_world);
            Shared.RemoveExclamation(src, _world);
            _world.SetActive(true);
            foreach (Collider c in _world.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Transform t in _world.GetComponentsInChildren<Transform>(true)) if (t != _world.transform && t.localScale.sqrMagnitude < 1e-6f) t.localScale = Vector3.one;
            LookStage.Settle(_world);
            Vector3 fwd = p.transform.forward; fwd.y = 0f; fwd.Normalize();
            _world.transform.position = p.transform.position + fwd * 1.8f + Vector3.up * 0.9f;
            _world.transform.rotation = Quaternion.LookRotation(-fwd);
            _world.transform.localScale = src.m_visual.transform.lossyScale;
            _worldSkin = null;
            foreach (SkinnedMeshRenderer s in _world.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.sharedMesh != null && (_worldSkin == null || s.sharedMesh.vertexCount > _worldSkin.sharedMesh.vertexCount)) _worldSkin = s;
            foreach (Renderer r in _world.GetComponentsInChildren<Renderer>(true)) if (r is ParticleSystemRenderer) r.enabled = false;
            _worldOrig = _worldSkin != null ? _worldSkin.sharedMaterials : null;
            _shown = -1; // dressed on the next frame
            _note = "Ворон стоїть перед тобою (на висоті грудей), у світлі світу. Ще раз — прибрати.";
        }

        private void RemoveWorld()
        {
            if (_world != null) Destroy(_world);
            _world = null; _worldSkin = null; _worldOrig = null; _worldKind = "";
        }

        // ---------------------------------------------------------------- the window
        internal void Body(float height)
        {
            if (_on == null)
            {
                _btn = new GUIStyle(GUI.skin.button); _btn.richText = true;
                _on = new GUIStyle(_btn); _on.fontStyle = FontStyle.Bold; _on.normal.textColor = new Color(1f, 0.83f, 0.3f);
                _small = new GUIStyle(GUI.skin.label); _small.fontSize = 12; _small.richText = true; _small.wordWrap = true;
                _head = new GUIStyle(GUI.skin.label); _head.fontStyle = FontStyle.Bold; _head.richText = true; _head.normal.textColor = new Color(0.95f, 0.82f, 0.55f);
                _rowL = new GUIStyle(GUI.skin.button); _rowL.alignment = TextAnchor.MiddleLeft; _rowL.richText = true; _rowL.fontSize = 12; _rowL.wordWrap = false; _rowL.clipping = TextClipping.Clip;
            }
            if (_cur == null) { GUILayout.Label("Зайди у світ."); return; }
            GUILayout.BeginHorizontal(GUILayout.Height(height));
            Left(height);
            Middle();
            Right(height);
            GUILayout.EndHorizontal();
        }

        private void Left(float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(_mode == 0 ? 220f : 290f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Кольори", _mode == 0 ? _on : _btn)) Mode(0);
            if (GUILayout.Button("Речі" + (_cur.Things.Count > 0 ? " " + _cur.Things.Count : ""), _mode == 1 ? _on : _btn)) Mode(1);
            if (GUILayout.Button("Ефекти", _mode == 2 ? _on : _btn)) { Mode(2); if (_fx == null) NewFx(); }
            GUILayout.EndHorizontal();
            if (_mode == 2) { FxLeft(height); GUILayout.EndVertical(); return; }
            if (_mode == 1) Catalog(height * 0.55f);
            else
            {
                _leftScroll = GUILayout.BeginScrollView(_leftScroll, GUILayout.Height(height * 0.55f));
                if (RavenLooks.FromFiles)
                    foreach (Look l in _files)
                        if (GUILayout.Button(l.Name + "  <color=#9A9A9A>· " + ForName(l.For) + "</color>", !_builtinPicked && _cur.Id == l.Id ? _on : _btn)) Ask(() => Pick(l, false));
                GUILayout.Label("<color=#9A9A9A>вбудовані в мод:</color>", _small);
                foreach (Look l in RavenLooks.BuiltinLooks())
                    if (GUILayout.Button(l.Name + "  <color=#9A9A9A>· " + ForName(l.For) + "</color>", _builtinPicked && _cur.Id == l.Id ? _on : _btn)) Ask(() => Pick(l, true));
                GUILayout.EndScrollView();
            }
            if (!RavenLooks.FromFiles) GUILayout.Label("<color=#FF9A7A>Файли виглядів читаються лише з «Outfits all for tuning» = true.</color>", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Новий")) Ask(() => { Look n = new Look { Name = "Новий вигляд", For = _cur.For }; Pick(n, false); _savedCode = ""; });
            if (GUILayout.Button("Копія")) { Look c = _cur.Copy(); c.Id = ""; c.Name = _cur.Name + " (копія)"; _cur = c; _builtinPicked = false; _savedCode = ""; _note = "Копія: збережеться окремим файлом."; }
            GUILayout.EndHorizontal();
            bool isFile = !_builtinPicked && !string.IsNullOrEmpty(_cur.Id) && _files.Exists(x => x.Id == _cur.Id);
            GUI.enabled = isFile;
            if (GUILayout.Button(_confirmDelete ? "Точно видалити файл?" : "Видалити файл", _confirmDelete ? _on : _btn))
            {
                if (_confirmDelete) { RavenLooks.Delete(_cur); Reload(); _note = "Файл видалено."; _confirmDelete = false; Look g = RavenLooks.ForGuide(); if (g != null) Pick(g, RavenLooks.BuiltinLooks().Contains(g)); }
                else _confirmDelete = true;
            }
            GUI.enabled = true;

            GUILayout.Space(8);
            GUILayout.Label("Назва", _small);
            string n2 = GUILayout.TextField(_cur.Name);
            if (n2 != _cur.Name) _cur.Name = n2;
            GUILayout.Label("Для кого", _small);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < ForIds.Length; i++)
            {
                if (i == 2) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (GUILayout.Button(ForNames[i], _cur.For == ForIds[i] ? _on : _btn)) _cur.For = ForIds[i];
            }
            GUILayout.EndHorizontal();
            if (_cur.For == "rune")
            {
                GUILayout.Label("Опис для книги (коротко, що дає руна)", _small);
                string d2 = GUILayout.TextField(_cur.Desc);
                if (d2 != _cur.Desc) _cur.Desc = d2;
                GUILayout.Label("<color=#9A9A9A>Колір, очі й полиск — це руна Фреї. Річ і ефекти з бібліотеки руна не дає. Одягнути: raven wear f " + (string.IsNullOrEmpty(_cur.Id) ? "<id після збереження>" : _cur.Id) + "</color>", _small);
            }
            else if (_cur.For != "guide") GUILayout.Label("<color=#9A9A9A>Ворони гравців вдягають вигляди лише через руни («Руна Фреї»).</color>", _small);

            GUILayout.Space(8);
            GUI.enabled = Changed || string.IsNullOrEmpty(_savedCode) || _builtinPicked;
            if (GUILayout.Button(Changed ? "<b>Зберегти</b> •" : "Зберегти", GUILayout.Height(30f)))
            {
                if (string.IsNullOrEmpty(_cur.Name.Trim())) _note = "Дай вигляду назву.";
                else
                {
                    RavenLooks.Save(_cur);
                    _savedCode = _cur.Code(); _builtinPicked = false; Reload();
                    _note = "Збережено: looks/" + _cur.Id + ".txt" + (_cur.For == "guide" ? ". Провідник уже в ньому (raven guide — покликати)." : ".");
                }
            }
            GUI.enabled = Changed;
            if (GUILayout.Button("Скасувати зміни"))
            {
                Look back = string.IsNullOrEmpty(_savedCode) ? new Look { Name = "Новий вигляд", For = _cur.For } : Look.Parse(_savedCode, _cur.Id);
                back.Id = _cur.Id;
                _cur = back; _dirty = true;
            }
            GUI.enabled = true;
            if (_pendingAsk != null)
            {
                GUILayout.Label("<color=#FFB060>Є незбережені зміни.</color>", _small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Кинути їх")) { System.Action a = _pendingAsk; _pendingAsk = null; a(); }
                if (GUILayout.Button("Лишитись")) _pendingAsk = null;
                GUILayout.EndHorizontal();
            }
            if (_note.Length > 0) GUILayout.Label(_note, _small);
            GUILayout.EndVertical();
        }

        private bool _confirmDelete;
        private System.Action _pendingAsk;
        private void Ask(System.Action a) { _confirmDelete = false; if (Changed) _pendingAsk = a; else { _pendingAsk = null; a(); } }

        private static string ForName(string id) { int i = System.Array.IndexOf(ForIds, id); return i >= 0 ? ForNames[i] : id; }

        private void Middle()
        {
            GUILayout.BeginVertical(GUILayout.Width(500f));
            Rect pic = GUILayoutUtility.GetRect(500f, 500f, GUILayout.Width(500f), GUILayout.Height(500f));
            if (LookStage.Texture != null) GUI.DrawTexture(pic, LookStage.Texture, ScaleMode.ScaleToFit, false);
            else GUI.Label(pic, "Сцена не готова: " + LookStage.Problem);
            Event e = Event.current;
            if (e.type == EventType.MouseDrag && pic.Contains(e.mousePosition))
            {
                LookStage.Yaw += e.delta.x * 0.6f;
                LookStage.Pitch = Mathf.Clamp(LookStage.Pitch + e.delta.y * 0.4f, -85f, 85f);
                LookStage.Spin = false;
                e.Use();
            }
            if (e.type == EventType.ScrollWheel && pic.Contains(e.mousePosition)) { LookStage.Zoom = Mathf.Clamp(LookStage.Zoom - e.delta.y * 0.05f, 0.5f, 6f); e.Use(); }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Спереду")) { LookStage.Yaw = 0f; LookStage.Pitch = 5f; LookStage.Spin = false; }
            if (GUILayout.Button("Збоку")) { LookStage.Yaw = 90f; LookStage.Pitch = 5f; LookStage.Spin = false; }
            if (GUILayout.Button("Ззаду")) { LookStage.Yaw = 180f; LookStage.Pitch = 5f; LookStage.Spin = false; }
            if (GUILayout.Button("Згори")) { LookStage.Pitch = 80f; LookStage.Spin = false; }
            if (GUILayout.Button("Знизу")) { LookStage.Pitch = -80f; LookStage.Spin = false; }
            if (GUILayout.Button(LookStage.Spin ? "Обертати: так" : "Обертати", LookStage.Spin ? _on : _btn)) LookStage.Spin = !LookStage.Spin;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Модель", GUILayout.Width(55f));
            if (GUILayout.Button("Хугін", !LookStage.Munin ? _on : _btn) && LookStage.Munin) { LookStage.Munin = false; FreeZoneMats(); _dirty = true; }
            if (GUILayout.Button("Мунін", LookStage.Munin ? _on : _btn) && !LookStage.Munin) { LookStage.Munin = true; FreeZoneMats(); _dirty = true; }
            GUILayout.Space(10f);
            if (GUILayout.Button("Сидить", LookStage.Pose == 0 ? _on : _btn)) LookStage.Pose = 0;
            if (GUILayout.Button("Летить", LookStage.Pose == 1 ? _on : _btn)) LookStage.Pose = 1;
            if (GUILayout.Button("Кружляє", LookStage.Pose == 2 ? _on : _btn)) LookStage.Pose = 2;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Показ", GUILayout.Width(55f));
            if (GUILayout.Button("Вигляд", _show == 0 ? _on : _btn)) _show = 0;
            if (GUILayout.Button("Зони", _show == 1 ? _on : _btn)) _show = 1;
            if (GUILayout.Button("Як у грі", _show == 2 ? _on : _btn)) _show = 2;
            GUILayout.Space(10f);
            if (GUILayout.Button(_world != null ? "Прибрати зі світу" : "Показати у світі", _world != null ? _on : _btn)) ToggleWorld();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Світло", GUILayout.Width(55f));
            if (GUILayout.Button("Студія", LookStage.Studio ? _on : _btn)) LookStage.Studio = true;
            if (GUILayout.Button("Як зараз у світі", !LookStage.Studio ? _on : _btn)) LookStage.Studio = false;
            if (GUILayout.Button("Чому біле? (знімки)")) LightDiag();
            GUILayout.EndHorizontal();
            if (_show == 1)
            {
                GUILayout.BeginHorizontal();
                for (int z = 1; z < LookDiag.ZoneNames.Length; z++)
                {
                    Color32 c = LookDiag.ZoneColor(z);
                    GUILayout.Label("<color=#" + Look.Hex(c) + ">■</color> " + LookDiag.ZoneNames[z], _small);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("<color=#9A9A9A>Тягни мишкою — крутити, коліщатко — ближче. Ліве й праве крило мають одну спільну картинку: фарбуються однаково.</color>", _small);
            GUILayout.EndVertical();
        }

        private void Right(float height)
        {
            GUILayout.BeginVertical();
            _rightScroll = GUILayout.BeginScrollView(_rightScroll, GUILayout.Height(height - 10f));
            if (_mode == 1) { Things(); GUILayout.EndScrollView(); GUILayout.EndVertical(); return; }
            if (_mode == 2) { FxEditor(); GUILayout.EndScrollView(); GUILayout.EndVertical(); return; }
            LookFxList();
            GUILayout.Space(8);
            GUILayout.Label("Зони", _head);
            for (int z = 1; z < LookDiag.ZoneNames.Length; z++)
            {
                Look.Part p = _cur.Zones[z];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.On ? "✓" : "–", p.On ? _on : _btn, GUILayout.Width(26f)))
                {
                    p.On = !p.On; _dirty = true;
                    if (p.On && p.Dark == Color.black && p.Light == Color.white) { p.Dark = new Color(0.05f, 0.05f, 0.06f); p.Light = new Color(0.5f, 0.5f, 0.55f); }
                }
                if (GUILayout.Button(LookDiag.ZoneNames[z], _zone == z ? _on : _btn, GUILayout.Width(120f))) _zone = z;
                if (p.On)
                {
                    Box(p.Dark, 22f); Box(p.Light, 22f);
                    GUILayout.Label("<color=#9A9A9A>" + Mathf.RoundToInt(p.Mix * 100f) + "%</color>", _small, GUILayout.Width(40f));
                }
                else GUILayout.Label("<color=#9A9A9A>як у грі</color>", _small);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(6);
            ZoneEditor();

            GUILayout.Space(10);
            GUILayout.Label("Очі (світіння)", _head);
            Toggle(ref _cur.EyesOn, "Свої очі", "Очі як у грі");
            if (_cur.EyesOn) { ColorEdit("eyes", ref _cur.Eyes); _cur.EyePower = Slider("Сила", _cur.EyePower, 0f, 12f); }

            GUILayout.Space(8);
            GUILayout.Label("Світіння тіла", _head);
            Toggle(ref _cur.GlowOn, "Світиться", "Не світиться");
            if (_cur.GlowOn) { ColorEdit("glow", ref _cur.Glow); _cur.GlowPower = Slider("Сила", _cur.GlowPower, 0f, 4f); GUILayout.Label("<color=#9A9A9A>Світліше пір'я світиться більше.</color>", _small); }

            GUILayout.Space(8);
            if (GUILayout.Button(_more ? "Менше ▴" : "Більше: блиск пір'я, іскри, код ▾", _btn)) _more = !_more;
            if (!_more) { GUILayout.EndScrollView(); GUILayout.EndVertical(); return; }
            GUILayout.Label("Блиск пір'я", _head);
            Toggle(ref _cur.SheenOn, "Свій блиск", "Блиск як у грі");
            if (_cur.SheenOn)
            {
                ColorEdit("sheen", ref _cur.Sheen);
                _cur.Metal = Slider("Метал", _cur.Metal, 0f, 1f);
                _cur.MetalGloss = Slider("Блиск металу", _cur.MetalGloss, 0f, 1f);
                _cur.Gloss = Slider("Гладкість", _cur.Gloss, 0f, 1f);
            }

            GUILayout.Space(8);
            GUILayout.Label("Чарівні іскри (по пір'ю)", _head);
            if (RavenLooks.SparkTemplate() == null) GUILayout.Label("<color=#FF9A7A>У грі не знайшлося картинки іскор — не працюватиме.</color>", _small);
            Toggle(ref _cur.SparksOn, "Є іскри", "Нема іскор");
            if (_cur.SparksOn)
            {
                ColorEdit("sparks", ref _cur.Sparks);
                _cur.SparkPower = Slider("Сила", _cur.SparkPower, 0f, 8f);
                _cur.SparkSize = Slider("Розмір", _cur.SparkSize, 0.3f, 12f);
                _cur.SparkEdge = Slider("Край", _cur.SparkEdge, 0f, 1f);
            }

            GUILayout.Space(10);
            GUILayout.Label("<color=#9A9A9A>Код вигляду (так він лежить у файлі):</color>", _small);
            GUILayout.TextArea(_cur.Code(), _small);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void ZoneEditor()
        {
            Look.Part p = _cur.Zones[_zone];
            GUILayout.Label("«" + LookDiag.ZoneNames[_zone] + "»", _head);
            if (!p.On)
            {
                GUILayout.Label("<color=#9A9A9A>Ця зона як у грі. Увімкни ✓, щоб фарбувати.</color>", _small);
                if (GUILayout.Button("Фарбувати цю зону")) { p.On = true; p.Dark = new Color(0.05f, 0.05f, 0.06f); p.Light = new Color(0.5f, 0.5f, 0.55f); _dirty = true; }
                return;
            }
            GUILayout.Label("Тінь (найтемніше пір'я)", _small);
            ColorEdit("z" + _zone + "d", ref p.Dark);
            GUILayout.Label("Світло (найсвітліше пір'я)", _small);
            ColorEdit("z" + _zone + "l", ref p.Light);
            p.Mix = Slider("Сила", p.Mix, 0f, 1f);
            p.Relief = Slider("Рельєф", p.Relief, 0f, 1f);
            GUILayout.Label("<color=#9A9A9A>Сила — скільки нового кольору (0 — як у грі). Рельєф — скільки малюнка пір'я лишити (0 — рівний колір).</color>", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Поміняти місцями")) { Color t = p.Dark; p.Dark = p.Light; p.Light = t; _dirty = true; }
            if (GUILayout.Button("Копіювати")) { _clip = new Look.Part { On = true, Dark = p.Dark, Light = p.Light, Mix = p.Mix, Relief = p.Relief }; _note = "Зону скопійовано."; }
            GUI.enabled = _clip != null;
            if (GUILayout.Button("Вставити")) { p.Dark = _clip.Dark; p.Light = _clip.Light; p.Mix = _clip.Mix; p.Relief = _clip.Relief; _dirty = true; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Так само на всі зони"))
            {
                for (int z = 1; z < _cur.Zones.Length; z++) { Look.Part q = _cur.Zones[z]; q.On = true; q.Dark = p.Dark; q.Light = p.Light; q.Mix = p.Mix; q.Relief = p.Relief; }
                _dirty = true; _note = "Усі зони — як «" + LookDiag.ZoneNames[_zone] + "».";
            }
            if (GUILayout.Button("Як у грі")) { p.On = false; _dirty = true; }
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- «Речі»: the catalog (left)
        private void Catalog(float h)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Пошук", GUILayout.Width(48f));
            _search = GUILayout.TextField(_search ?? "");
            if (GUILayout.Button("×", GUILayout.Width(24f))) _search = "";
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < CatNames.Length; i++)
            {
                if (i == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (GUILayout.Button(CatNames[i], i == _cat ? _on : _btn)) _cat = i;
            }
            GUILayout.EndHorizontal();
            if (CatIds[_cat] >= 0 && !WsCatalog.Ready)
            {
                GUILayout.Label("Список ще не зібрано.", _small);
                if (GUILayout.Button("Зібрати")) WsCatalog.Build();
                GUILayout.Space(Mathf.Max(0f, h - 30f));
                return;
            }
            if (_search != _searchWas || _cat != _catWas) Filter();
            GUILayout.Label("<color=#9A9A9A>" + _shownSrc.Count + ". Клік — повісити; ↔ — замінити вибрану річ.</color>", _small);

            const float row = 34f;
            _catScroll = GUILayout.BeginScrollView(_catScroll, GUILayout.Height(Mathf.Max(160f, h - 70f)));
            Rect all = GUILayoutUtility.GetRect(240f, row * _shownSrc.Count);
            int first = Mathf.Max(0, Mathf.FloorToInt(_catScroll.y / row) - 1);
            int last = Mathf.Min(_shownSrc.Count - 1, first + Mathf.CeilToInt(h / row) + 2);
            for (int i = first; i <= last; i++)
            {
                Rect r = new Rect(all.x, all.y + i * row, all.width, row - 2f);
                Rect icon = new Rect(r.x + 2f, r.y + 1f, row - 4f, row - 4f);
                Rect main = new Rect(icon.xMax + 4f, r.y, r.width - icon.width - 40f, r.height);
                Rect swap = new Rect(main.xMax + 2f, r.y, 30f, r.height);
                DrawIcon(icon, _shownIcon[i]);
                if (GUI.Button(main, _shownLabel[i], _rowL)) AddThing(_shownSrc[i]);
                if (GUI.Button(swap, "↔"))
                {
                    if (_thing < 0 || _thing >= _cur.Things.Count) _note = "Спершу вибери праворуч річ, яку замінити.";
                    else { _cur.Things[_thing].Src = _shownSrc[i]; _note = "Замінено."; }
                }
            }
            GUILayout.EndScrollView();
        }

        private void Filter()
        {
            _searchWas = _search; _catWas = _cat;
            _shownSrc.Clear(); _shownLabel.Clear(); _shownIcon.Clear();
            string[] words = (_search ?? "").ToLowerInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (CatIds[_cat] < 0)
            {
                // the Workshop's saved models (things for a raven first: they were made round it)
                List<string> files = WsFiles.List();
                files.Sort((a, b) => (WsFiles.UseOf(a) == "gear" ? 0 : 1).CompareTo(WsFiles.UseOf(b) == "gear" ? 0 : 1));
                foreach (string f in files)
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(f);
                    if (!Has(name.ToLowerInvariant(), words)) continue;
                    string use = WsFiles.UseOf(f);
                    _shownSrc.Add(LookThings.ModelPrefix + name);
                    _shownLabel.Add(name + "\n<size=10><color=#9A9A9A>модель майстерні" + (use.Length > 0 ? " · " + use : "") + "</color></size>");
                    _shownIcon.Add(null);
                }
            }
            else
                foreach (WsCatalog.Entry e in WsCatalog.All)
                {
                    if (e.Cat != CatIds[_cat] || !Has(e.Search, words)) continue;
                    _shownSrc.Add(e.Name);
                    _shownLabel.Add((e.Label.Length > 0 ? e.Label : e.Name) + "\n<size=10><color=#9A9A9A>" + e.Name + (e.Mod != WsCatalog.Game ? " · " + e.Mod : "") + "</color></size>");
                    _shownIcon.Add(e.Icon);
                }
            _catScroll = Vector2.zero;
        }

        private static bool Has(string text, string[] words)
        {
            foreach (string w in words) if (text.IndexOf(w, System.StringComparison.Ordinal) < 0) return false;
            return true;
        }

        private static void DrawIcon(Rect r, Sprite s)
        {
            if (s == null || s.texture == null) { GUI.Box(r, ""); return; }
            Rect tr = s.textureRect;
            Texture t = s.texture;
            GUI.DrawTextureWithTexCoords(r, t, new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height));
        }

        // a new thing: on the head, a little above it, about the head's size (a Workshop model where it was made)
        private void AddThing(string src)
        {
            Look.Thing t = new Look.Thing { Src = src, Bone = "head", Pos = new Vector3(0f, 0.04f, 0f) };
            if (src.StartsWith(LookThings.ModelPrefix, System.StringComparison.Ordinal))
            {
                WsModel m = LookThings.Model(src.Substring(LookThings.ModelPrefix.Length));
                t.Pos = Vector3.zero;
                if (m != null && m.Use == "gear")
                {
                    string slot = RavenGear.SlotOf(m);
                    t.Bone = slot == RavenGear.Legs ? "legl" : slot == RavenGear.Neck ? "neck" : slot == RavenGear.Head ? "beak" : "back";
                    t.Pair = slot == RavenGear.Legs;
                }
            }
            t.Scale = Vector3.one * LookThings.FirstScale(src);
            t.Fx = WsCatalog.FxOnly(src);
            _cur.Things.Add(t);
            _thing = _cur.Things.Count - 1;
            _note = "Повішено: " + ThingName(t) + ". Праворуч — куди і як.";
        }

        private static string ThingName(Look.Thing t)
        {
            if (t.Src.StartsWith(LookThings.ModelPrefix, System.StringComparison.Ordinal)) return t.Src.Substring(LookThings.ModelPrefix.Length);
            string l = WsCatalog.Ready ? WsCatalog.Label(t.Src) : "";
            return l.Length > 0 ? l : t.Src;
        }

        // ---------------------------------------------------------------- «Речі»: what hangs on it and where (right)
        private void Things()
        {
            GUILayout.Label("Речі на вороні", _head);
            if (_cur.Things.Count == 0) { GUILayout.Label("<color=#9A9A9A>Ще нічого. Клацни річ ліворуч — вона з'явиться на голові ворона.</color>", _small); return; }
            for (int i = 0; i < _cur.Things.Count; i++)
            {
                Look.Thing t = _cur.Things[i];
                if (GUILayout.Button((i + 1) + ". " + ThingName(t) + "  <color=#9A9A9A>· " + LookThings.NameOf(t.Bone) + (t.Pair && LookThings.PairOf(t.Bone) != null ? " + пара" : "") + "</color>", i == _thing ? _on : _rowL)) _thing = i;
            }
            if (_thing < 0 || _thing >= _cur.Things.Count) return;
            Look.Thing s = _cur.Things[_thing];

            GUILayout.Space(8);
            GUILayout.Label("«" + ThingName(s) + "»", _head);
            GUILayout.Label("Де висить", _small);
            GUILayout.BeginHorizontal();
            for (int b = 0; b < LookThings.BoneIds.Length; b++)
            {
                if (b > 0 && b % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (GUILayout.Button(LookThings.BoneNames[b], s.Bone == LookThings.BoneIds[b] ? _on : _btn, GUILayout.Width(118f))) s.Bone = LookThings.BoneIds[b];
            }
            GUILayout.EndHorizontal();
            if (LookThings.PairOf(s.Bone) != null)
                if (GUILayout.Button(s.Pair ? "Пара: так — дзеркально і на другому боці" : "Пара: ні — лише на цьому боці", s.Pair ? _on : _btn)) s.Pair = !s.Pair;

            GUILayout.Space(6);
            GUILayout.Label("Місце <color=#9A9A9A>(у розмірах ворона на плечі)</color>", _small);
            s.Pos.z = Axis("Вперед", s.Pos.z, 0.002f, 0.01f, "0.000");
            s.Pos.y = Axis("Вгору", s.Pos.y, 0.002f, 0.01f, "0.000");
            s.Pos.x = Axis("Вбік", s.Pos.x, 0.002f, 0.01f, "0.000");
            GUILayout.Label("Поворот, градуси", _small);
            s.Rot.x = Axis("Нахил", s.Rot.x, 5f, 15f, "0");
            s.Rot.y = Axis("Поворот", s.Rot.y, 5f, 15f, "0");
            s.Rot.z = Axis("Крен", s.Rot.z, 5f, 15f, "0");
            GUILayout.Label("Розмір", _small);
            GUILayout.BeginHorizontal();
            foreach (float k in new float[] { 0.5f, 0.9f, 0.98f, 1.02f, 1.1f, 2f })
                if (GUILayout.Button(k < 1f ? (k == 0.5f ? "×½" : "−" + Mathf.RoundToInt((1f - k) * 100f) + "%") : (k == 2f ? "×2" : "+" + Mathf.RoundToInt((k - 1f) * 100f) + "%"))) s.Scale *= k;
            GUILayout.EndHorizontal();
            s.Scale.x = Axis("Ширина", s.Scale.x, s.Scale.x * 0.02f, s.Scale.x * 0.1f, "0.000", LookThings.FirstScale(s.Src));
            s.Scale.y = Axis("Висота", s.Scale.y, s.Scale.y * 0.02f, s.Scale.y * 0.1f, "0.000", LookThings.FirstScale(s.Src));
            s.Scale.z = Axis("Довжина", s.Scale.z, s.Scale.z * 0.02f, s.Scale.z * 0.1f, "0.000", LookThings.FirstScale(s.Src));

            GUILayout.Space(6);
            if (GUILayout.Button(s.Tinted ? "Свій колір" : "Колір як у грі", s.Tinted ? _on : _btn, GUILayout.Width(160f))) s.Tinted = !s.Tinted;
            if (s.Tinted) ColorEditRaw("thing" + _thing, ref s.Tint);
            bool model = s.Src.StartsWith(LookThings.ModelPrefix, System.StringComparison.Ordinal);
            if (!model && !WsCatalog.FxOnly(s.Src) && WsCatalog.HasFx(s.Src))
                if (GUILayout.Button(s.Fx ? "Живий ефект: є (вогник, іскри, світло)" : "Живий ефект: нема", s.Fx ? _on : _btn)) s.Fx = !s.Fx;

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Копія")) { _cur.Things.Insert(_thing + 1, s.Clone()); _thing++; }
            if (GUILayout.Button("Скинути місце")) { s.Pos = model ? Vector3.zero : new Vector3(0f, 0.04f, 0f); s.Rot = Vector3.zero; s.Scale = Vector3.one * LookThings.FirstScale(s.Src); }
            if (GUILayout.Button("Зняти")) { _cur.Things.RemoveAt(_thing); _thing = Mathf.Min(_thing, _cur.Things.Count - 1); }
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#9A9A9A>Вперед — куди дивиться ворон. Річ рухається разом зі своєю кісткою: голова крутиться, крила махають. «Зберегти» ліворуч — і Провідник у ній (речі з майстерні та ассети — лише поки файли читаються).</color>", _small);
        }

        private float Axis(string label, float v, float step, float big, string fmt, float reset = 0f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80f));
            if (GUILayout.Button("−−", GUILayout.Width(32f))) v -= big;
            if (GUILayout.Button("−", GUILayout.Width(28f))) v -= step;
            GUILayout.Label(v.ToString(fmt, CultureInfo.InvariantCulture), GUILayout.Width(60f));
            if (GUILayout.Button("+", GUILayout.Width(28f))) v += step;
            if (GUILayout.Button("++", GUILayout.Width(32f))) v += big;
            if (GUILayout.Button(reset == 0f ? "0" : "1", GUILayout.Width(26f))) v = reset;
            GUILayout.EndHorizontal();
            return v;
        }

        // ---------------------------------------------------------------- «Ефекти»: our library (left)
        // another mode: a question or a «delete?» asked in the one left is dropped (one click there must not delete here)
        private void Mode(int m)
        {
            if (m == _mode) return;
            _mode = m; _confirmDelete = false; _pendingAsk = null;
        }

        private bool FxChanged { get { return _fx != null && _fx.Code() != _fxSaved; } }

        private void AskFx(System.Action a) { if (FxChanged) _pendingAsk = a; else { _pendingAsk = null; a(); } }

        private void NewFx() { _fx = new RavenFxDef(); _fxSaved = ""; }

        private void PickFx(RavenFxDef d) { _fx = d.Copy(); _fx.Id = d.Id; _fxSaved = _fx.Code(); if (_fx.Trail) LookStage.Pose = 2; }

        private void FxLeft(float height)
        {
            GUILayout.Label("Наші ефекти", _head);
            List<RavenFxDef> all = FxLib.All();
            _fxLibScroll = GUILayout.BeginScrollView(_fxLibScroll, GUILayout.Height(height * 0.24f));
            if (all.Count == 0) GUILayout.Label("<color=#9A9A9A>Ще жодного. Вибери нижче основу з гри — і збережи.</color>", _small);
            foreach (RavenFxDef d in all)
                if (GUILayout.Button(d.Name + "  <color=#9A9A9A>· " + (d.Trail ? "слід" : "аура") + "</color>", _fx != null && _fx.Id == d.Id && d.Id.Length > 0 ? _on : _rowL)) { RavenFxDef pick = d; AskFx(() => PickFx(pick)); }
            GUILayout.EndScrollView();
            if (!RavenLooks.FromFiles) GUILayout.Label("<color=#FF9A7A>Файли ефектів читаються лише з «Outfits all for tuning» = true.</color>", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Новий")) AskFx(NewFx);
            if (GUILayout.Button("Копія") && _fx != null) { RavenFxDef c = _fx.Copy(); c.Id = ""; c.Name = _fx.Name + " (копія)"; _fx = c; _fxSaved = ""; }
            GUI.enabled = _fx != null && _fx.Id.Length > 0;
            if (GUILayout.Button(_confirmDelete ? "Точно?" : "Видалити", _confirmDelete ? _on : _btn))
            {
                if (_confirmDelete) { FxLib.Delete(_fx); _cur.Fx.Remove(_fx.Id); NewFx(); _confirmDelete = false; _note = "Ефект видалено."; }
                else _confirmDelete = true;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (_fx != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Назва", GUILayout.Width(48f));
                _fx.Name = GUILayout.TextField(_fx.Name);
                GUILayout.EndHorizontal();
                GUI.enabled = FxChanged && !string.IsNullOrEmpty(_fx.Base);
                if (GUILayout.Button(FxChanged ? "<b>Зберегти ефект</b> •" : "Зберегти ефект", _btn, GUILayout.Height(28f)))
                {
                    FxLib.Save(_fx); _fxSaved = _fx.Code(); _lookFxKind = "";
                    _note = "Збережено: fx/" + _fx.Id + ".txt. Щоб ворон його носив — «Кольори» → «Ефекти з бібліотеки».";
                }
                GUI.enabled = true;
            }
            if (_pendingAsk != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<color=#FFB060>Не збережено.</color>", _small);
                if (GUILayout.Button("Кинути")) { System.Action a = _pendingAsk; _pendingAsk = null; a(); }
                if (GUILayout.Button("Лишитись")) _pendingAsk = null;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("Основа з гри", _head);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Пошук", GUILayout.Width(48f));
            _fxSearch = GUILayout.TextField(_fxSearch ?? "");
            if (GUILayout.Button("×", GUILayout.Width(24f))) _fxSearch = "";
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < FxCatNames.Length; i++) if (GUILayout.Button(FxCatNames[i], i == _fxCat ? _on : _btn)) _fxCat = i;
            GUILayout.EndHorizontal();
            if (_fxSearch != _fxSearchWas || _fxCat != _fxCatWas) FxFilter();
            GUILayout.Label("<color=#9A9A9A>" + _fxShown.Count + ". Клік — взяти за основу цього ефекту.</color>", _small);
            const float row = 24f;
            float h = Mathf.Max(120f, height * 0.42f);
            _fxScroll = GUILayout.BeginScrollView(_fxScroll, GUILayout.Height(h));
            Rect all2 = GUILayoutUtility.GetRect(240f, row * _fxShown.Count);
            int first = Mathf.Max(0, Mathf.FloorToInt(_fxScroll.y / row) - 1);
            int last = Mathf.Min(_fxShown.Count - 1, first + Mathf.CeilToInt(h / row) + 2);
            for (int i = first; i <= last; i++)
            {
                FxLib.Base b = _fxShown[i];
                Rect r = new Rect(all2.x, all2.y + i * row, all2.width, row - 2f);
                string tag = b.Src.StartsWith(FxLib.OursPrefix, System.StringComparison.Ordinal) ? "наше" : (b.Loop ? "живий" : "спалах") + (b.Lights > 0 ? " · світло" : "");
                bool cur = _fx != null && _fx.Base == b.Src;
                if (GUI.Button(r, b.Label + "  <color=#9A9A9A>" + tag + "</color>", cur ? _on : _rowL) && _fx != null)
                {
                    bool named = _fx.Name != "Новий ефект" && _fx.Name.Length > 0;
                    _fx.Base = b.Src;
                    if (!named) _fx.Name = b.Label;
                }
            }
            GUILayout.EndScrollView();
            if (_note.Length > 0) GUILayout.Label(_note, _small);
        }

        private void FxFilter()
        {
            _fxSearchWas = _fxSearch; _fxCatWas = _fxCat;
            _fxShown.Clear();
            string[] words = (_fxSearch ?? "").ToLowerInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (FxLib.Base b in FxLib.Bases())
            {
                bool ours = b.Src.StartsWith(FxLib.OursPrefix, System.StringComparison.Ordinal);
                if (_fxCat == 0 && !ours) continue;
                if (_fxCat == 1 && (ours || !b.Loop)) continue;
                if (_fxCat == 2 && (ours || !b.Flash)) continue;
                if (!Has(b.Label.ToLowerInvariant(), words)) continue;
                _fxShown.Add(b);
            }
            _fxScroll = Vector2.zero;
        }

        // ---------------------------------------------------------------- «Ефекти»: the one being made (right)
        private void FxEditor()
        {
            if (_fx == null) return;
            GUILayout.Label("«" + _fx.Name + "»", _head);
            GUILayout.Label(string.IsNullOrEmpty(_fx.Base) ? "<color=#FFB060>Основа ще не вибрана: клацни щось ліворуч у «Основа з гри».</color>" : "Основа: <b>" + FxLib.LabelOf(_fx.Base) + "</b>", _small);

            GUILayout.Space(6);
            GUILayout.Label("Для чого", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Аура — завжди на вороні", !_fx.Trail ? _on : _btn)) { _fx.Kind = "aura"; if (LookStage.Pose == 2) LookStage.Pose = 0; }
            if (GUILayout.Button("Слід — коли летить", _fx.Trail ? _on : _btn)) { _fx.Kind = "trail"; LookStage.Pose = 2; }
            GUILayout.EndHorizontal();
            if (_fx.Trail) GUILayout.Label("<color=#9A9A9A>На сцені ворон кружляє, щоб було видно слід.</color>", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_fx.Rune ? (_fx.Trail ? "Руна Геймдаля ✓" : "Руна Бальдра ✓") : "Не руна", _fx.Rune ? _on : _btn)) _fx.Rune = !_fx.Rune;
            GUILayout.EndHorizontal();
            if (_fx.Rune)
            {
                GUILayout.Label("Опис для книги (коротко, що дає руна)", _small);
                string d2 = GUILayout.TextField(_fx.Desc);
                if (d2 != _fx.Desc) _fx.Desc = d2;
                GUILayout.Label("<color=#9A9A9A>Одягнути: raven wear " + (_fx.Trail ? "h " : "b ") + (string.IsNullOrEmpty(_fx.Id) ? "<id після збереження>" : _fx.Id) + "</color>", _small);
            }

            GUILayout.Label("Де на вороні", _small);
            GUILayout.BeginHorizontal();
            for (int b = 0; b < LookThings.BoneIds.Length; b++)
            {
                if (b > 0 && b % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (GUILayout.Button(LookThings.BoneNames[b], _fx.Bone == LookThings.BoneIds[b] ? _on : _btn, GUILayout.Width(118f))) _fx.Bone = LookThings.BoneIds[b];
            }
            GUILayout.EndHorizontal();
            _fx.At.z = Axis("Вперед", _fx.At.z, 0.005f, 0.02f, "0.000");
            _fx.At.y = Axis("Вгору", _fx.At.y, 0.005f, 0.02f, "0.000");
            _fx.At.x = Axis("Вбік", _fx.At.x, 0.005f, 0.02f, "0.000");

            GUILayout.Space(6);
            if (GUILayout.Button(_fx.Tinted ? "Свій колір" : "Колір як у грі", _fx.Tinted ? _on : _btn, GUILayout.Width(160f))) _fx.Tinted = !_fx.Tinted;
            if (_fx.Tinted) ColorEditRaw("fx", ref _fx.Tint);

            GUILayout.Space(6);
            _fx.Size = FxSlider("Розмір", _fx.Size, 0.1f, 5f);
            _fx.Amount = FxSlider("Кількість", _fx.Amount, 0f, 5f);
            _fx.Speed = FxSlider("Швидкість", _fx.Speed, 0.1f, 3f);
            _fx.Life = FxSlider("Довше живе", _fx.Life, 0.2f, 3f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Ще раз") && _fxOn != null) _fxOn.Again();
            if (GUILayout.Button("Усе як у грі")) { _fx.Size = _fx.Amount = _fx.Speed = _fx.Life = 1f; _fx.Tinted = false; _fx.At = Vector3.zero; }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.Label("<color=#9A9A9A>Так: 1) основа ліворуч, 2) для чого і де, 3) колір і повзунки, 4) «Зберегти ефект». Потім у «Кольори» постав йому галочку — і вигляд (Провідник) його носить.</color>", _small);
        }

        private float FxSlider(string label, float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            float n = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(200f));
            GUILayout.Label("×" + n.ToString("0.00", CultureInfo.InvariantCulture), GUILayout.Width(50f));
            if (GUILayout.Button("1", GUILayout.Width(24f))) n = 1f;
            GUILayout.EndHorizontal();
            return n;
        }

        // the look's effects from the library: ticked ones it wears
        private void LookFxList()
        {
            GUILayout.Label("Ефекти з бібліотеки", _head);
            List<RavenFxDef> all = FxLib.All();
            if (all.Count == 0) { GUILayout.Label("<color=#9A9A9A>Бібліотека порожня — зроби ефект у вкладці «Ефекти».</color>", _small); return; }
            foreach (RavenFxDef d in all)
            {
                bool on = _cur.Fx.Contains(d.Id);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(on ? "✓" : "–", on ? _on : _btn, GUILayout.Width(26f))) { if (on) _cur.Fx.Remove(d.Id); else _cur.Fx.Add(d.Id); }
                GUILayout.Label(d.Name + "  <color=#9A9A9A>· " + (d.Trail ? "слід" : "аура") + " · " + LookThings.NameOf(d.Bone) + "</color>", _small);
                GUILayout.EndHorizontal();
            }
            foreach (string id in _cur.Fx) if (FxLib.Find(id) == null) GUILayout.Label("<color=#FF9A7A>«" + id + "» — нема в бібліотеці.</color>", _small);
        }

        // ---------------------------------------------------------------- «Чому біле?»: the same raven shot many ways
        private void LightDiag()
        {
            if (LookStage.Model == null || LookStage.Originals == null) { _note = "Сцена не готова."; return; }
            string dir = System.IO.Path.Combine(RavenLooks.Dir, "_diag");
            System.IO.Directory.CreateDirectory(dir);
            Material[] orig = LookStage.Originals;
            Material[] look = _paintedOk ? RavenLooks.Dress(_live, orig) : orig;
            bool studio = LookStage.Studio;
            List<Material> made = new List<Material>();
            System.Text.StringBuilder rep = new System.Text.StringBuilder();
            try
            {
                Shot(dir, "01_game_studio", orig, true, true);
                Shot(dir, "02_look_studio", look, true, true);
                Shot(dir, "03_game_world", orig, false, true);
                Shot(dir, "04_look_world", look, false, true);
                Shot(dir, "05_look_no_sheen", Variant(look, orig, made, (m, o) => { foreach (string k in new[] { "_Metallic", "_MetalGloss", "_Glossiness" }) if (m.HasProperty(k)) m.SetFloat(k, o.GetFloat(k)); if (m.HasProperty("_MetalColor")) m.SetColor("_MetalColor", o.GetColor("_MetalColor")); }), true, true);
                Shot(dir, "06_look_no_metal", Variant(look, orig, made, (m, o) => { foreach (string k in new[] { "_Metallic", "_MetalGloss", "_Glossiness" }) if (m.HasProperty(k)) m.SetFloat(k, 0f); }), true, true);
                Shot(dir, "07_look_no_rain", Variant(look, orig, made, (m, o) => { m.DisableKeyword("_ADDRAIN_ON"); if (m.HasProperty("_AddRain")) m.SetFloat("_AddRain", 0f); }), true, true);
                Shot(dir, "08_look_no_glow", Variant(look, orig, made, (m, o) => { if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black); }), true, true);
                Shot(dir, "09_look_no_normal", Variant(look, orig, made, (m, o) => { m.DisableKeyword("_NORMALMAP"); if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null); }), true, true);
                Shot(dir, "10_look_game_texture", Variant(look, orig, made, (m, o) => { m.mainTexture = o.mainTexture; }), true, true);
                Shot(dir, "11_look_no_lamps", look, true, false);
                Shot(dir, "12_game_no_lamps", orig, true, false);

                rep.Append("Light diag ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("\nglobals: _Wet ").Append(Shader.GetGlobalFloat("_Wet"))
                   .Append(" _AmbientColor ").Append(Shader.GetGlobalColor("_AmbientColor")).Append(" _SunColor ").Append(Shader.GetGlobalColor("_SunColor"))
                   .Append(" _SunDir ").Append(Shader.GetGlobalVector("_SunDir")).Append("\nRenderSettings: ambient ").Append(RenderSettings.ambientMode).Append(' ').Append(RenderSettings.ambientLight)
                   .Append(" fog ").Append(RenderSettings.fog).Append(' ').Append(RenderSettings.fogColor).Append(' ').Append(RenderSettings.fogDensity)
                   .Append(" reflection ").Append(RenderSettings.defaultReflectionMode).Append(' ').Append(RenderSettings.reflectionIntensity).Append('\n');
                for (int i = 0; i < orig.Length; i++)
                {
                    if (!RavenLooks.IsBody(orig[i])) continue;
                    Props(rep, "GAME", orig[i]);
                    if (look[i] != orig[i]) Props(rep, "LOOK", look[i]);
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "light_report.txt"), rep.ToString());
                _note = "Знімки готові: looks/_diag/light_*.png і light_report.txt. Скажи мені — я подивлюся сам.";
            }
            catch (System.Exception e) { _note = "Знімки не вдалися: " + e.Message; Perches.Log("light diag: " + e); }
            finally
            {
                LookStage.Studio = studio; LookStage.NoLamps = false;
                foreach (Material m in made) if (m != null) Destroy(m);
                _shown = -1;
            }
        }

        private static Material[] Variant(Material[] look, Material[] orig, List<Material> made, System.Action<Material, Material> change)
        {
            Material[] r = new Material[look.Length];
            for (int i = 0; i < look.Length; i++)
            {
                if (!RavenLooks.IsBody(orig[i])) { r[i] = look[i]; continue; }
                Material m = new Material(look[i]); m.hideFlags = HideFlags.HideAndDontSave;
                change(m, orig[i]);
                made.Add(m); r[i] = m;
            }
            return r;
        }

        private static void Shot(string dir, string name, Material[] mats, bool studio, bool lamps)
        {
            LookStage.Show(mats);
            LookStage.Studio = studio; LookStage.NoLamps = !lamps;
            LookStage.Render();
            LookStage.Save(System.IO.Path.Combine(dir, "light_" + name + ".png"));
        }

        private static void Props(System.Text.StringBuilder sb, string what, Material m)
        {
            sb.Append('\n').Append(what).Append(' ').Append(m.name).Append(" / ").Append(m.shader.name).Append("\n  keywords: ").Append(string.Join(" ", m.shaderKeywords)).Append('\n');
            Shader sh = m.shader;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                string n = sh.GetPropertyName(i);
                UnityEngine.Rendering.ShaderPropertyType t = sh.GetPropertyType(i);
                sb.Append("  ").Append(n).Append(" = ");
                if (t == UnityEngine.Rendering.ShaderPropertyType.Color) sb.Append(m.GetColor(n));
                else if (t == UnityEngine.Rendering.ShaderPropertyType.Vector) sb.Append(m.GetVector(n));
                else if (t == UnityEngine.Rendering.ShaderPropertyType.Texture) { Texture x = m.GetTexture(n); sb.Append(x == null ? "none" : x.name + " " + x.width + "x" + x.height + " " + x.graphicsFormat); }
                else sb.Append(m.GetFloat(n).ToString(CultureInfo.InvariantCulture));
                sb.Append('\n');
            }
        }

        // ---------------------------------------------------------------- little controls
        private void Toggle(ref bool v, string yes, string no)
        {
            if (GUILayout.Button(v ? yes : no, v ? _on : _btn, GUILayout.Width(160f))) { v = !v; _dirty = true; }
        }

        private float Slider(string label, float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            float n = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(170f));
            GUILayout.Label(n.ToString(max > 2f ? "0.0" : "0.00", CultureInfo.InvariantCulture), GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(n - v) > 1e-5f) _dirty = true;
            return n;
        }

        private void Box(Color c, float w)
        {
            Rect r = GUILayoutUtility.GetRect(w, 18f, GUILayout.Width(w), GUILayout.Height(18f));
            Color was = GUI.color; GUI.color = c; GUI.DrawTexture(new Rect(r.x + 1f, r.y + 2f, r.width - 2f, r.height - 4f), Texture2D.whiteTexture); GUI.color = was;
        }

        private bool Swatch(Color c, bool picked)
        {
            Rect r = GUILayoutUtility.GetRect(22f, 20f, GUILayout.Width(22f), GUILayout.Height(20f));
            bool hit = GUI.Button(r, "");
            Color was = GUI.color; GUI.color = c; GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), Texture2D.whiteTexture); GUI.color = was;
            if (picked) GUI.Label(new Rect(r.x + 7f, r.y + 1f, r.width, r.height), "<b>•</b>", _small);
            return hit;
        }

        // a colour: ready feather colours, the code, hue / saturation / brightness
        // a colour that is no paint of the feathers (a thing's, an effect's): the raven is not painted again for it
        private void ColorEditRaw(string key, ref Color c) { bool was = _dirty; ColorEdit(key, ref c); _dirty = was; }

        private void ColorEdit(string key, ref Color c)
        {
            GUILayout.BeginHorizontal();
            for (int k = 0; k < Swatches.Length; k++)
                if (Swatch(Swatches[k], c == Swatches[k])) { c = Swatches[k]; _hsv.Remove(key); _dirty = true; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Код #", GUILayout.Width(44f));
            string hex = ColorUtility.ToHtmlStringRGB(c);
            string name = "look_hex_" + key, text;
            if (GUI.GetNameOfFocusedControl() != name || !_fields.TryGetValue(name, out text)) text = hex;
            GUI.SetNextControlName(name);
            string typed = GUILayout.TextField(text, GUILayout.Width(80f));
            _fields[name] = typed;
            Color parsed;
            if (typed != text && typed.Length == 6 && ColorUtility.TryParseHtmlString("#" + typed, out parsed) && ColorUtility.ToHtmlStringRGB(parsed) != hex) { c = parsed; _hsv.Remove(key); _dirty = true; }
            Box(c, 60f);
            GUILayout.EndHorizontal();
            // hue kept while it means nothing (grey, black), so dragging brightness back up does not lose it
            Vector3 hsv;
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            if (_hsv.TryGetValue(key, out hsv) && Near(Color.HSVToRGB(hsv.x, hsv.y, hsv.z), c)) { h = hsv.x; s = hsv.y; v = hsv.z; }
            float nh = SliderRaw("Відтінок", h), ns = SliderRaw("Насиченість", s), nv = SliderRaw("Яскравість", v);
            if (nh != h || ns != s || nv != v)
            {
                c = Color.HSVToRGB(nh, ns, nv);
                _hsv[key] = new Vector3(nh, ns, nv);
                _dirty = true;
            }
        }

        private static bool Near(Color a, Color b) { return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f; }

        private float SliderRaw(string label, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            float n = GUILayout.HorizontalSlider(v, 0f, 1f, GUILayout.Width(170f));
            GUILayout.Label(Mathf.RoundToInt(n * 100f) + "%", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return n;
        }

        private void OnDestroy() { _live.Free(); FreeZoneMats(); RemoveWorld(); }
    }

    // the raven on its own stage far above the world, filmed into a picture for the tab
    internal static class LookStage
    {
        private const int Size = 512;
        private static GameObject _stage, _model;
        private static Camera _cam;
        private static Light[] _lamps;
        private static RenderTexture _rt;
        private static SkinnedMeshRenderer _skin;
        private static Material[] _orig;
        private static Animator _anim;
        private static AnimationClip _fly;
        private static Transform[] _bones;
        private static Vector3[] _bp, _bs;
        private static Quaternion[] _br;
        private static bool _built, _builtMunin;
        private static float _failedAt = -100f;
        private static float _flyT, _circleT;
        private static float _rest = 0.5f; // the sitting raven's size (half its box's diagonal), measured once
        internal static float CircleRadius = 2.2f, CircleSpeed = 1.1f;
        internal static bool NoLamps; // (the light diagnosis: only the ambient light)

        // the picture as it is now into a PNG file
        internal static void Save(string path)
        {
            if (_rt == null) return;
            RenderTexture prev = RenderTexture.active;
            Texture2D t = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = _rt;
                t.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                t.Apply();
                System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            }
            finally { RenderTexture.active = prev; Object.Destroy(t); }
        }

        private static ReflectionProbe _probe;
        private static Cubemap _cube;

        internal static bool Munin;
        internal static bool Studio = true; // its own light, not the world's weather (see Render)
        internal static int Layer = -1;
        internal static GameObject Model { get { return _model; } }
        internal static float Yaw = 60f, Pitch = 10f, Zoom = 1f;
        internal static bool Spin;
        internal static int Pose;
        internal static string Problem = "";
        internal static RenderTexture Texture { get { return _rt; } }
        internal static Material[] Originals { get { return _orig; } }

        internal static bool Ready(bool munin)
        {
            if (_built && _builtMunin == munin && _model != null) return true;
            if (_builtMunin == munin && Time.unscaledTime - _failedAt < 3f) return false;
            Build(munin);
            if (_model == null) _failedAt = Time.unscaledTime;
            return _model != null;
        }

        // the game's raven, landed and held in its sitting pose (as the book's picture)
        internal static void Settle(GameObject model)
        {
            foreach (Animator a in model.GetComponentsInChildren<Animator>(true))
            {
                a.enabled = true;
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                a.Rebind();
                a.Update(0f);
                a.SetTrigger("teleportin");
                for (int i = 0; i < 60; i++) a.Update(0.1f);
                a.enabled = false;
            }
        }

        private static void Build(bool munin)
        {
            Close();
            _built = true; _builtMunin = munin;
            Raven src = Shoulder.Template(munin);
            if (src == null || src.m_visual == null) { Problem = "ворона ще нема"; return; }
            int layer = RavenFx.FreeLayer();
            if (layer < 0) { Problem = "нема вільного шару"; return; }
            Layer = layer;
            _stage = new GameObject("RavenWatch_LookStage");
            _stage.transform.position = new Vector3(0f, 9500f, 0f);
            _model = Object.Instantiate(src.m_visual, _stage.transform, false);
            AppearMute.Quiet(_model);
            Shared.RemoveExclamation(src, _model);
            _model.SetActive(true);
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;
            _model.transform.localScale = Vector3.one;
            foreach (Collider c in _model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            Settle(_model);
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
            {
                if (t != _model.transform && t.localScale.sqrMagnitude < 1e-6f) t.localScale = Vector3.one;
                t.gameObject.layer = layer;
            }
            _skin = null;
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) { r.enabled = false; continue; }
                SkinnedMeshRenderer s = r as SkinnedMeshRenderer;
                if (s != null && s.sharedMesh != null && (_skin == null || s.sharedMesh.vertexCount > _skin.sharedMesh.vertexCount)) _skin = s;
            }
            if (_skin == null) { Problem = "нема скіну"; Close(); _built = true; return; }
            _skin.updateWhenOffscreen = true;
            _rest = Mathf.Max(0.05f, _skin.bounds.extents.magnitude);
            _orig = _skin.sharedMaterials;
            _anim = _model.GetComponentInChildren<Animator>(true);
            _fly = _anim != null ? ShoulderLife.FlyClipOf(_anim) : null;
            List<Transform> bones = new List<Transform>();
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true)) if (t != _model.transform) bones.Add(t);
            _bones = bones.ToArray();
            _bp = new Vector3[_bones.Length]; _bs = new Vector3[_bones.Length]; _br = new Quaternion[_bones.Length];
            for (int i = 0; i < _bones.Length; i++) { _bp[i] = _bones[i].localPosition; _br[i] = _bones[i].localRotation; _bs[i] = _bones[i].localScale; }

            _cam = new GameObject("Cam").AddComponent<Camera>();
            _cam.transform.SetParent(_stage.transform, false);
            _cam.enabled = false;
            _cam.cullingMask = 1 << layer;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.16f, 0.16f, 0.18f, 1f);
            _cam.fieldOfView = 25f;
            _cam.renderingPath = RenderingPath.Forward;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            Light key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(_stage.transform, false);
            key.type = LightType.Directional; key.intensity = 1.15f; key.color = new Color(1f, 0.96f, 0.9f); key.cullingMask = 1 << layer;
            key.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            Light fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(_stage.transform, false);
            fill.type = LightType.Directional; fill.intensity = 0.55f; fill.color = new Color(0.8f, 0.85f, 1f); fill.cullingMask = 1 << layer;
            fill.transform.rotation = Quaternion.Euler(20f, -60f, 0f);
            key.enabled = fill.enabled = false;
            _lamps = new Light[] { key, fill };
            // the sky it reflects: a soft studio grey of its own (far from the world's probes, it took the world's sky)
            _probe = new GameObject("Probe").AddComponent<ReflectionProbe>();
            _probe.transform.SetParent(_stage.transform, false);
            _probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            _probe.customBakedTexture = StudioSky();
            _probe.size = Vector3.one * 40f;
            _probe.importance = 100;
            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            _rt.antiAliasing = 2;
            _rt.Create();
            _cam.targetTexture = _rt;
            Problem = "";
        }

        internal static void Show(Material[] mats)
        {
            if (_skin == null || _orig == null) return;
            Material[] want = mats ?? _orig;
            Material[] now = _skin.sharedMaterials;
            bool same = now.Length == want.Length;
            for (int i = 0; same && i < now.Length; i++) same = now[i] == want[i];
            if (!same) _skin.sharedMaterials = want;
        }

        internal static void Render()
        {
            if (_cam == null || _model == null) return;
            // the pose
            for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) { _bones[i].localPosition = _bp[i]; _bones[i].localRotation = _br[i]; _bones[i].localScale = _bs[i]; }
            // «Кружляє»: round the stage's middle, flying (a trail is seen left behind); else in the middle
            Vector3 mid = _stage.transform.position;
            if (Pose == 2)
            {
                _circleT += Time.unscaledDeltaTime;
                float r = Mathf.Max(0.3f, _rest * CircleRadius), a = _circleT * CircleSpeed;
                Vector3 at = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 2f) * r * 0.08f, Mathf.Sin(a) * r);
                Vector3 way = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                _model.transform.position = mid + at;
                _model.transform.rotation = Quaternion.LookRotation(-way, Vector3.up) * Quaternion.Euler(0f, 0f, 18f); // (the game's raven faces its own -Z), banked into the turn
            }
            else { _model.transform.localPosition = Vector3.zero; _model.transform.localRotation = Quaternion.identity; }
            if (Pose >= 1 && _fly != null && _anim != null)
            {
                _flyT += Time.unscaledDeltaTime;
                Transform root = _anim.transform;
                Vector3 lp = root.localPosition; Quaternion lr = root.localRotation; Vector3 ls = root.localScale;
                _fly.SampleAnimation(_anim.gameObject, Mathf.Repeat(_flyT, Mathf.Max(0.1f, _fly.length)));
                root.localPosition = lp; root.localRotation = lr; root.localScale = ls;
                foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer) && !r.gameObject.activeSelf && r.GetComponentInParent<LookThings.Tag>() == null) r.gameObject.SetActive(true);
            }
            if (Spin) Yaw += Time.unscaledDeltaTime * 25f;
            // the camera round the raven's middle (measured on the sitting pose, so flying does not swing the view)
            Bounds b = _skin.bounds;
            if (Pose == 2) b = new Bounds(mid, Vector3.one * (_rest * (CircleRadius + 1f) * 1.2f)); // the whole ring in the picture
            float dist = b.extents.magnitude / Mathf.Sin(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.85f / Zoom;
            Quaternion q = Quaternion.Euler(Pitch, Yaw + 90f, 0f);
            _cam.transform.position = b.center - q * Vector3.forward * dist;
            _cam.transform.rotation = q;
            _cam.nearClipPlane = Mathf.Max(0.01f, dist * 0.05f);
            _cam.farClipPlane = dist * 4f;
            RenderTexture prev = RenderTexture.active;
            foreach (Light l in _lamps) l.enabled = !NoLamps;
            if (_probe != null) _probe.enabled = Studio;
            // The game's creature shader takes the world's weather from globals (EnvMan): wetness after rain (a pale
            // glossy coat), the sun's colour and way, the ambient colour, and the fog. In the studio the raven gets ours
            // for this one shot; everything goes back right after, ambient probe included (setting the ambient light
            // rewrites it, and the world's grass would come back pale).
            bool fog = RenderSettings.fog;
            UnityEngine.Rendering.AmbientMode mode = RenderSettings.ambientMode;
            Color amb = RenderSettings.ambientLight;
            UnityEngine.Rendering.SphericalHarmonicsL2 sh = RenderSettings.ambientProbe;
            float wet = Shader.GetGlobalFloat(WetId);
            Color ambCol = Shader.GetGlobalColor(AmbId), sunCol = Shader.GetGlobalColor(SunColId);
            Vector4 sunDir = Shader.GetGlobalVector(SunDirId);
            try
            {
                if (Studio)
                {
                    Color a = new Color(0.32f, 0.32f, 0.34f);
                    RenderSettings.fog = false;
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = a;
                    Shader.SetGlobalFloat(WetId, 0f);
                    Shader.SetGlobalColor(AmbId, a);
                    Light key = _lamps[0];
                    Shader.SetGlobalColor(SunColId, key.color * key.intensity);
                    Shader.SetGlobalVector(SunDirId, -key.transform.forward);
                }
                _cam.Render();
            }
            finally
            {
                foreach (Light l in _lamps) l.enabled = false;
                RenderTexture.active = prev;
                if (Studio)
                {
                    RenderSettings.fog = fog;
                    RenderSettings.ambientMode = mode;
                    RenderSettings.ambientLight = amb;
                    RenderSettings.ambientProbe = sh;
                    Shader.SetGlobalFloat(WetId, wet);
                    Shader.SetGlobalColor(AmbId, ambCol);
                    Shader.SetGlobalColor(SunColId, sunCol);
                    Shader.SetGlobalVector(SunDirId, sunDir);
                }
            }
        }

        private static readonly int WetId = Shader.PropertyToID("_Wet"), AmbId = Shader.PropertyToID("_AmbientColor"),
            SunColId = Shader.PropertyToID("_SunColor"), SunDirId = Shader.PropertyToID("_SunDir");

        // a little sky for the studio: light grey above, darker below, made once
        private static Cubemap StudioSky()
        {
            if (_cube != null) return _cube;
            const int n = 16;
            _cube = new Cubemap(n, TextureFormat.RGBA32, false);
            _cube.hideFlags = HideFlags.HideAndDontSave;
            foreach (CubemapFace face in new CubemapFace[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                Color[] px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float up = face == CubemapFace.PositiveY ? 1f : face == CubemapFace.NegativeY ? 0f : 1f - (y + 0.5f) / n; // (side faces run top to bottom)
                        float v = Mathf.Lerp(0.08f, 0.42f, up);
                        px[y * n + x] = new Color(v, v, v * 1.04f, 1f);
                    }
                _cube.SetPixels(px, face);
            }
            _cube.Apply();
            return _cube;
        }

        internal static void Close()
        {
            if (_stage != null) Object.Destroy(_stage);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            _stage = null; _model = null; _cam = null; _lamps = null; _rt = null; _skin = null; _orig = null; _anim = null; _fly = null; _probe = null;
            _built = false;
        }
    }
}
#endif
