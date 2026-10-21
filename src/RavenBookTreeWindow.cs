#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The raven tree's own window (the roots' E, TreeRoot): not the book — one sheet of paper over the game, like the
    // nest's. Left: the runes that burn, by kind (any kinds together); a click lays one on the roots or takes it back.
    // Right: the tree's places (2/3/4, named by where each rune lies on the raven), ЩО / КОЛИ / ЯК lit by what lies
    // there, the sentence of the в'язь it would make, «Сплести» (the arena) — or your в'язь and «Розплести».
    // It closes far from the tree, by Esc or ×.
    // Drawn again only when what it shows changed (a stamp, checked a few times a second).
    internal static partial class RavenBook
    {
        private static GameObject _twRoot;
        private static RectTransform _twPanel;
        private static TreeView _twAt;
        private static string _twStamp = "", _twSaid = "";
        private static float _twNext;
        private static readonly List<string> _twMix = new List<string>(); // the runes laid on the roots
        private const float TwW = 1100f, TwH = 720f;

        internal static bool TreeWindowOpen { get { return _twRoot != null && _twRoot.activeSelf; } }

        internal static void OpenTree(TreeView v)
        {
            if (IsOpen) Close();
            CloseNestWindow();
            OrdersMenu.CloseNow();
            if (_gameFont == null && InventoryGui.instance != null)
            {
                TMP_Text sample = InventoryGui.instance.m_recipeName != null ? InventoryGui.instance.m_recipeName : InventoryGui.instance.m_playerName;
                _gameFont = sample != null ? sample.font : null;
                BookArt.Fonts(_gameFont);
            }
            if (_twRoot == null)
            {
                _twRoot = new GameObject("RavenWatch_TreeWindow", typeof(RectTransform));
                Canvas c = _twRoot.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 30002;
                CanvasScaler sc = _twRoot.AddComponent<CanvasScaler>();
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920f, 1080f);
                sc.matchWidthOrHeight = 1f;
                _twRoot.AddComponent<GraphicRaycaster>();
                UnityEngine.Object.DontDestroyOnLoad(_twRoot);
                Image dim = Rect("Dim", _twRoot.transform).gameObject.AddComponent<Image>();
                Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                dim.color = new Color(0f, 0f, 0f, 0.4f);
            }
            if (_twAt != v) _twMix.Clear();
            _twAt = v; _twStamp = ""; _twSaid = "";
            _twRoot.SetActive(true);
            TreeWindowDraw();
        }

        internal static void CloseTreeWindow()
        {
            if (_twRoot != null) _twRoot.SetActive(false);
            _twAt = null;
            _twMix.Clear();
        }

        // from the plugin's Update (RavenBook.Update)
        private static void TreeWindowTick()
        {
            if (!TreeWindowOpen) return;
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead() || InventoryGui.IsVisible() || Menu.IsVisible() || RavenKeys.Down(KeyCode.Escape)
                || _twAt == null || !_twAt.Near(p))
            { CloseTreeWindow(); return; }
            if (Time.time < _twNext) return;
            _twNext = Time.time + 0.3f;
            if (TreeStamp(p) != _twStamp) TreeWindowDraw();
        }

        private static string TreeStamp(Player p)
        {
            return RuneBook.Version + ":" + WearCatalog.Stamp + ":" + string.Join("+", _twMix.ToArray()) + ":" + _twSaid.Length
                + ":" + BindRun.Version + ":" + BindArena.Running + BindArena.Wave + ":" + SeatZone.Mine(p, _twAt.Perch).Count;
        }

        private static void TreeWindowDraw()
        {
            Player p = Player.m_localPlayer;
            if (_twRoot == null || p == null || _twAt == null) return;
            _twStamp = TreeStamp(p);
            if (_twPanel != null) { _twPanel.gameObject.SetActive(false); UnityEngine.Object.Destroy(_twPanel.gameObject); } // (hidden now: the new one is not drawn over it for a frame)
            _twPanel = Rect("Panel", _twRoot.transform);
            _twPanel.anchorMin = _twPanel.anchorMax = _twPanel.pivot = new Vector2(0.5f, 0.5f);
            _twPanel.sizeDelta = new Vector2(TwW, TwH);
            Image sh = Img(_twPanel, "Shadow", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.5f));
            Stretch(sh.rectTransform, Vector2.zero, Vector2.one, new Vector2(-30f, -36f), new Vector2(30f, 20f));
            RectTransform sheet = Rect("Sheet", _twPanel);
            Stretch(sheet, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Box(sheet, 10f, Paper, C("#a8843a"), 2f);
            sheet.gameObject.AddComponent<Button>(); // (clicks on the paper stay on it)

            TMP_Text title = T(_twPanel, _twAt.Name.ToUpperInvariant(), 28f, Ink, F.Head, TextAlignmentOptions.TopLeft);
            title.characterSpacing = 8f; title.textWrappingMode = TextWrappingModes.NoWrap;
            At(title.rectTransform, 36f, 24f, 800f, 40f);
            RectTransform x = Rect("Close", _twPanel);
            At(x, TwW - 52f, 20f, 30f, 30f);
            Image xi = Img(x, "Cross", BookArt.Cross, Muted);
            Stretch(xi.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Clickable(x.gameObject, CloseTreeWindow);

            RectTransform left = Rect("Left", _twPanel);
            At(left, 36f, 80f, 420f, TwH - 110f);
            VBox(left, 6f, TextAnchor.UpperLeft);
            TreeRunes(left, p);

            RectTransform right = Rect("Right", _twPanel);
            At(right, 500f, 80f, TwW - 500f - 36f, TwH - 110f);
            VBox(right, 10f, TextAnchor.UpperLeft);
            TreeRoots(right, p);
        }

        // ---- left: the runes that burn, by kind (any kinds go together)
        private static void TreeRunes(RectTransform col, Player p)
        {
            RuneBook.Data d = RuneBook.Of(p);
            T(col, Lang.T("Руни, що горять"), 21f, Ink, F.Head);
            int any = 0;
            for (int k = 0; k < WearCatalog.Kinds; k++)
            {
                List<RuneDef> open = new List<RuneDef>();
                foreach (RuneDef r in RuneBook.All) if ((int)r.Kind == k && d.Has(r.Id)) open.Add(r);
                if (open.Count == 0) continue;
                any += open.Count;
                T(col, Lang.T(RuneBook.KindTitles[k]) + "  <size=80%><color=#7a6a50>" + Lang.T(PartWord[k]) + "</color></size>", 16f, Brown, F.Head);
                RectTransform grid = Rect("Grid", col);
                GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(62f, 62f); g.spacing = new Vector2(6f, 6f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 6;
                foreach (RuneDef r in open) TreeRuneCell(grid, d, r);
            }
            if (any < 2) T(col, Lang.T("Для в'язі треба хоча б дві руни, що горять. Руни дає Одін за доручення."), 15f, InkSoft, F.Italic);
            else T(col, Lang.T("Клік по руні — покласти її на коріння або забрати. Наведи — що вона дає у в'язі."), 14f, InkSoft, F.Italic);
        }

        // what each kind is in the sentence of a в'язь
        private static readonly string[] PartWord = Lang.Keys("ЯК", "ЩО", "КОЛИ");

        private static void TreeRuneCell(Transform grid, RuneBook.Data d, RuneDef r)
        {
            bool laid = _twMix.Contains(r.Id), taken = d.InBind(r.Id);
            RectTransform cell = Rect("Rune_" + r.Id, grid);
            if (laid || taken || !RuneStone(cell, (int)r.Kind, r.Glyph, 1, 13f, 24f)) // (laid on the roots: a lit frame instead; its stone is in the roots)
            {
                Torn(cell, laid ? new Color(0.66f, 0.52f, 0.23f, 0.3f) : BookArt.Card, laid ? Gold : C("#a8843a"), laid ? 2.5f : 1.5f);
                TMP_Text g = T(cell, r.Glyph, 30f, laid ? GoldInk : taken ? A(Ink, 0.3f) : Ink, F.Rune, TextAlignmentOptions.Center);
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 12f), Vector2.zero);
                g.raycastTarget = false;
            }
            BindPart part = new BindPart { Kind = r.Kind, Wear = d.WearFor(r.Id) };
            Element el = part.El;
            if (el != Element.None)
            {
                Image dot = Img(cell, "El", BookArt.Circle, C("#" + Elements.Hex[(int)el]));
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = dot.rectTransform.pivot = new Vector2(1f, 1f);
                dot.rectTransform.sizeDelta = new Vector2(10f, 10f); dot.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
                dot.raycastTarget = false;
            }
            TMP_Text n = T(cell, Lang.T(r.Name), 11f, Brown, F.Body, TextAlignmentOptions.Bottom);
            Stretch(n.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 3f), new Vector2(-1f, 0f));
            n.textWrappingMode = TextWrappingModes.NoWrap; n.raycastTarget = false;
            string id = r.Id;
            Tip(cell.gameObject, () => RuneInBind(d, r));
            Clickable(cell.gameObject, () =>
            {
                Player me = Player.m_localPlayer;
                int slots = Bind.Slots(RavenTree.Kind(_twAt.transform.position));
                if (_twMix.Contains(id)) _twMix.Remove(id);
                else if (RuneBook.Of(me).InBind(id)) Tell(Lang.T("Ця руна вже у твоїй в'язі."));
                else if (_twMix.Count < slots) _twMix.Add(id);
                else Tell(Lang.F("На цьому дереві вміщається {0} рун(и).", slots));
                _twSaid = "";
                TreeWindowDraw();
            });
        }

        // a rune's tip: its effect, its element, what it is in a в'язь
        private static string RuneInBind(RuneBook.Data d, RuneDef r)
        {
            BindPart part = new BindPart { Kind = r.Kind, Wear = d.WearFor(r.Id) };
            WearDef w = WearCatalog.Find(r.Kind, part.Wear);
            string a = BindPower.ActionOf(part);
            string does;
            if (r.Kind == WearKind.Baldr) { BindPower bp = new BindPower(); string act = BindPower.ActiveOf(part); does = Lang.T("ЩО:") + " " + bp.WhatWords(a, 1f) + "\n" + Lang.F("Перша руна в'язі лише з рун Бальдра — кнопка «{0}»: {1}", Lang.T(BindPower.ActiveNames[act]), bp.ActiveWords(act, 1f)); }
            else if (r.Kind == WearKind.Heimdall) { string s; does = Lang.T("КОЛИ:") + " " + (BindPower.WhenWords.TryGetValue(a, out s) ? Lang.T(s) : a); }
            else { string s; does = Lang.T("ЯК:") + " " + (BindPower.HowWordsMap.TryGetValue(a, out s) ? Lang.T(s) : a); }
            return "<b>" + Lang.T(r.Name) + "</b> — " + (w != null ? w.Name : "?") + "\n" + Lang.T("Стихія:") + " " + Elements.Colored(part.El) + "\n\n" + does
                + (d.StarsOf(r.Id) > 0 ? "\n" + Lang.F("{0} — у в'язі +{1}% сили.", RuneStars.StarText(d.StarsOf(r.Id)), Mathf.RoundToInt(d.StarsOf(r.Id) * RuneStars.BindPerStar * 100f)) : "")
                + (d.InBind(r.Id) ? "\n\n" + Lang.T("Зараз у твоїй в'язі.") : "");
        }

        // ---- right: the roots, the sentence, the weaving (the arena), your в'язь
        private static void TreeRoots(RectTransform col, Player p)
        {
            RuneBook.Data d = RuneBook.Of(p);
            Vector3 at = _twAt.transform.position;
            int tree = RavenTree.Kind(at), slots = Bind.Slots(tree);
            T(col, Lang.F("Це дерево {0}: на його коріння лягає {1} рун(и). Воно {2}.", Lang.T(RavenTree.KindNames[tree]), slots, Lang.T(RavenTree.HowWeaves[tree])), 15f, InkSoft, F.Italic);
            string trust = BindArena.TrustProblem(tree);
            if (trust != null) T(col, trust, 15f, Bad, F.Italic);

            if (d.Bind.Length > 0) { YourBind(col, p, d); return; }

            // the places in the roots, named by where the rune lies on the raven: a click takes a laid one back
            T(col, Lang.T("Коріння"), 21f, Ink, F.Head);
            RectTransform roots = Rect("Roots", col);
            HBox(roots, 8f, TextAnchor.MiddleCenter, 0, 0, 6, 6);
            Spring(roots);
            for (int i = 0; i < slots; i++)
            {
                if (i > 0) { TMP_Text plus = T(roots, "+", 22f, A(Ink, 0.4f), F.Head); plus.textWrappingMode = TextWrappingModes.NoWrap; }
                RootPlace(roots, i < _twMix.Count ? RuneBook.Def(_twMix[i]) : null, Lang.T(Bind.PlaceNames[tree][i]), i >= 2);
            }
            Spring(roots);

            // ЩО / КОЛИ / ЯК: lit by what lies there
            Bind b = Bind.From(d, _twMix, tree);
            RectTransform marks = Rect("Marks", col);
            HBox(marks, 18f, TextAnchor.MiddleCenter, 0, 0, 2, 2);
            Spring(marks);
            Mark(marks, Lang.T("ЩО"), Lang.T("Бальдр"), b.Count(WearKind.Baldr) > 0);
            Mark(marks, Lang.T("КОЛИ"), Lang.T("Геймдаль"), b.Count(WearKind.Heimdall) > 0);
            Mark(marks, Lang.T("ЯК"), Lang.T("Фрея"), b.Count(WearKind.Freya) > 0);
            Spring(marks);

            string no = _twMix.Count == 0 ? Lang.T("Поклади руни на коріння — тут складеться речення в'язі.") : b.Problem();
            if (no != null) T(col, no, 16f, _twMix.Count == 0 ? InkSoft : Bad, F.Italic);
            else
            {
                BindPower w = BindPower.Of(b);
                TMP_Text s = T(col, w.Sentence(), 16f, Ink, F.Body);
                s.richText = true;
            }

            // the weaving: the arena
            bool seated = SeatZone.Mine(p, _twAt.Perch).Count > 0;
            bool can = no == null && trust == null && seated && !BindArena.Running;
            RectTransform row = Rect("Weave", col);
            HBox(row, 14f, TextAnchor.MiddleLeft, 0, 0, 4, 4);
            SealButton(row, Lang.T("Сплести"), "plain", 104f, 15f, () =>
            {
                string why = BindArena.Begin(Player.m_localPlayer, _twAt, new List<string>(_twMix));
                if (why != null) Tell(why);
                else { _twMix.Clear(); CloseTreeWindow(); }
            }, can, can ? 1f : 0.45f);
            string state = BindArena.Running ? Lang.F("Арена: хвиля {0} з {1}.", BindArena.Wave, BindArena.Waves)
                : !seated ? Lang.T("Коріння мовчить, поки на дереві нема твоїх воронів. Посади їх вище, на стовбур.")
                : Lang.F("Почнеться арена: {0} хвилі ворогів довкола дерева. Встоїш — в'язь сплететься; впадеш або покинеш коло довше ніж на 10 с — руни лишаться в книзі. Інші гравці можуть допомогти.", slots);
            TMP_Text st = T(row, state, 14f, !seated ? Bad : InkSoft, F.Italic);
            st.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (_twSaid.Length > 0) T(col, _twSaid, 16f, GoldInk, F.Head);
        }

        // your в'язь: its sentence, its runes; «Розплести» frees them (a new one only after that)
        private static void YourBind(RectTransform col, Player p, RuneBook.Data d)
        {
            T(col, Lang.T("Твоя в'язь"), 21f, Ink, F.Head);
            List<string> names = new List<string>();
            foreach (string r in d.BindRunes) { RuneDef rd = RuneBook.Def(r); if (rd != null) names.Add(rd.Glyph + " " + Lang.T(rd.Name)); }
            T(col, string.Join(" + ", names.ToArray()), 17f, GoldInk, F.Head);
            BindPower w = BindRun.PowerOf(d.Bind);
            if (w != null) { TMP_Text s = T(col, w.Sentence(), 16f, Ink, F.Body); s.richText = true; }
            RectTransform row = Rect("Unweave", col);
            HBox(row, 14f, TextAnchor.MiddleLeft, 0, 0, 4, 4);
            SealButton(row, Lang.T("Розплести"), "plain", 104f, 15f, () => { RuneBook.Unweave(Player.m_localPlayer); _twSaid = Lang.T("В'язь розплетено. Руни вільні."); TreeWindowDraw(); }, true, 1f);
            TMP_Text st = T(row, Lang.T("У тебе може бути лише одна в'язь. Щоб сплести нову — спершу розплети цю, тоді знову арена."), 14f, InkSoft, F.Italic);
            st.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (_twSaid.Length > 0) T(col, _twSaid, 16f, GoldInk, F.Head);
        }

        // one of ЩО / КОЛИ / ЯК: gold when a rune of its kind lies on the roots
        private static void Mark(RectTransform row, string word, string kind, bool lit)
        {
            RectTransform m = Rect("Mark", row);
            Fixed(m.gameObject, 92f, 46f);
            Torn(m, lit ? new Color(0.66f, 0.52f, 0.23f, 0.3f) : BookArt.Card, lit ? Gold : A(Ink, 0.3f), lit ? 2f : 1f);
            TMP_Text t = T(m, word + "\n<size=60%>" + kind + "</size>", 17f, lit ? GoldInk : A(Ink, 0.4f), F.Head, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.raycastTarget = false;
        }

        // one place in the roots: an empty ring with its name, or the laid rune (a click takes it back)
        private static void RootPlace(RectTransform row, RuneDef r, string place, bool later)
        {
            RectTransform box = Rect("Place", row);
            VBox(box, 2f, TextAnchor.UpperCenter);
            RectTransform c = Rect("Root", box);
            Fixed(c.gameObject, 72f, 72f);
            TMP_Text name = T(box, place, 12f, A(Ink, 0.6f), F.Italic, TextAlignmentOptions.Center);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            if (r != null && RuneStone(c, (int)r.Kind, r.Glyph, 2, 0f, 32f)) // its stone, when there is a picture
            {
                string rid = r.Id;
                Clickable(c.gameObject, () => { _twMix.Remove(rid); _twSaid = ""; TreeWindowDraw(); });
                return;
            }
            Image ring = Img(c, "Ring", BookArt.Circle, r != null ? Gold : A(Ink, later ? 0.18f : 0.35f));
            Stretch(ring.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fill = Img(c, "Fill", BookArt.Circle, r != null ? C("#3a2c1c") : C("#efe3c4"));
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fill.raycastTarget = false;
            TMP_Text g = T(c, r != null ? r.Glyph : later ? Lang.T("за бажанням") : "", r != null ? 34f : 11f, r != null ? Gold : A(Ink, 0.35f), r != null ? F.Rune : F.Italic, TextAlignmentOptions.Center);
            Stretch(g.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            g.raycastTarget = false;
            if (r == null) return;
            string id = r.Id;
            Clickable(ring.gameObject, () => { _twMix.Remove(id); _twSaid = ""; TreeWindowDraw(); });
        }
    }
}
#endif
