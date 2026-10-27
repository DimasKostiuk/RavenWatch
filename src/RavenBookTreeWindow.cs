#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The raven tree's own window (the roots' E, TreeRoot), after the user's mockups 3a / 3b / 3c: a dark sheet in the
    // tree's own colour (green, icy, charred). On top its name and places, ЩО / КОЛИ / ЯК lit by what lies on the roots;
    // under it the branch with one place per root (Серце, Середина, Оболонка, Кора), the rune laid there or a dashed
    // ring; then a parchment strip with the в'язь's sentence, short, the runes' signs in it (the whole sentence with its
    // numbers when hovered); three chests of wood with each god's eight sockets (a rune that burns sits in its own, one
    // laid on the roots leaves it empty); and the «Сплести» stone of this tree (a pebble, a chipped block, a coal shard).
    // A click on a rune in a chest lays it on the first free place, a click on a place takes it back. With a в'язь
    // woven the branch and strip show it, the stone unweaves. It closes far from the tree, by Esc or ×.
    // Drawn again only when what it shows changed (a stamp, checked a few times a second).
    internal static partial class RavenBook
    {
        private static GameObject _twRoot;
        private static RectTransform _twPanel;
        private static TreeView _twAt;
        private static string _twStamp = "", _twSaid = "";
        private static float _twNext;
        private static readonly List<string> _twMix = new List<string>(); // the runes laid on the roots
        private const float TwW = 1100f, TwH = 840f;

        // by the tree's kind (0 icy, 1 green, 2 charred): its light, its sheet, the stone's picture and rune, the stone's ink
        private static readonly Color[] TreeLight = { C("#a8d8f0"), C("#a8d878"), C("#f0a060") };
        private static readonly Color[] TreeSheet = { C("#0d1620"), C("#0f1a10"), C("#1b1009") };
        private static readonly string[] TreeStoneArt = { "tree_btn_icy", "tree_btn_green", "tree_btn_charred" };
        private static readonly string[] TreeStoneRune = { "ᛁ", "ᛒ", "ᚲ" };
        private static readonly Color[] TreeStoneInk = { C("#eef6ff"), C("#eef2d8"), C("#f0c890") };
        private static readonly Color[] GodInk = { C("#e7a0d8"), C("#e8c07a"), C("#8cc0f0") }; // Фрея, Бальдр, Геймдаль
        private static readonly string[] GodSocket = { "tree_socket_f", "tree_socket_b", "tree_socket_h" };
        private static readonly Color SignInk = C("#8a4a1a");

        internal static bool TreeWindowOpen { get { return _twRoot != null && _twRoot.activeSelf; } }

        internal static void OpenTree(TreeView v)
        {
            if (IsOpen) Close();
            CloseNestWindow();
            OrdersMenu.CloseNow();
            EnsureGameFont();
            if (_twRoot == null)
            {
                _twRoot = OverlayCanvas("RavenWatch_TreeWindow", 30002, true);
                Image dim = Rect("Dim", _twRoot.transform).gameObject.AddComponent<Image>();
                Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                dim.color = new Color(0f, 0f, 0f, 0.45f);
            }
            _twAt = v; _twStamp = ""; _twSaid = "";
            _twRoot.SetActive(true);
            TreeWindowDraw();
        }

        internal static void CloseTreeWindow()
        {
            TreeTip.Hide();
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
            TreeTip.Tick(_twRoot.transform, false);
            if (Time.time < _twNext) return;
            _twNext = Time.time + 0.3f;
            if (TreeStamp(p) != _twStamp) TreeWindowDraw();
        }

        // ---- its own tips (the book's show only inside the open book)
        private static readonly WinTip TreeTip = new WinTip(420f);

        private static string TreeStamp(Player p)
        {
            return RuneBook.Version + ":" + WearCatalog.Stamp + ":" + string.Join("+", _twMix.ToArray())
                + ":" + BindRun.Version + ":" + BindArena.Running + BindArena.Wave + ":" + SeatZone.Mine(p, _twAt.Perch).Count;
        }

        private static void TreeWindowDraw()
        {
            Player p = Player.m_localPlayer;
            if (_twRoot == null || p == null || _twAt == null) return;
            _twStamp = TreeStamp(p);
            if (_twPanel != null) { _twPanel.gameObject.SetActive(false); UnityEngine.Object.Destroy(_twPanel.gameObject); } // (hidden now: the new one is not drawn over it for a frame)
            RuneBook.Data d = RuneBook.Of(p);
            Bind mine = d.Bind.Length > 0 ? Bind.Parse(d.Bind) : null;
            int tree = RavenTree.Kind(_twAt.transform.position);
            Color light = TreeLight[tree];

            _twPanel = Rect("Panel", _twRoot.transform);
            _twPanel.anchorMin = _twPanel.anchorMax = _twPanel.pivot = new Vector2(0.5f, 0.5f);
            _twPanel.sizeDelta = new Vector2(TwW, TwH);
            Image sh = Img(_twPanel, "Shadow", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.55f));
            Stretch(sh.rectTransform, Vector2.zero, Vector2.one, new Vector2(-30f, -36f), new Vector2(30f, 20f));
            RectTransform sheet = Rect("Sheet", _twPanel);
            Stretch(sheet, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Box(sheet, 14f, TreeSheet[tree], A(light, 0.35f), 2f);

            // the name, the places; ЩО / КОЛИ / ЯК
            int slots = Bind.Slots(tree);
            TMP_Text title = T(_twPanel, Lang.F("{0} дерево", Lang.T(RavenTree.KindNames[tree])).ToUpperInvariant() + "  ·  " + Lang.N(Bind.Slots(tree), "місце|місця|місць").ToUpperInvariant(), 26f, light, F.Head, TextAlignmentOptions.TopLeft);
            title.characterSpacing = 6f; title.textWrappingMode = TextWrappingModes.NoWrap;
            At(title.rectTransform, 40f, 30f, 640f, 40f);
            List<string> laid = mine != null ? d.BindRunes : _twMix;
            Bind b = mine ?? Bind.From(d, _twMix, tree);
            Mark(TwW - 300f, Lang.T("ЩО"), b.Count(WearKind.Baldr) > 0, light);
            Mark(TwW - 216f, Lang.T("КОЛИ"), b.Count(WearKind.Heimdall) > 0, light);
            Mark(TwW - 132f, Lang.T("ЯК"), b.Count(WearKind.Freya) > 0, light);
            RectTransform x = Rect("Close", _twPanel);
            At(x, TwW - 46f, 16f, 28f, 28f);
            Image xi = Img(x, "Cross", BookArt.Cross, A(light, 0.7f));
            Stretch(xi.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Clickable(x.gameObject, CloseTreeWindow);

            // the branch and its places
            Image branch = Img(_twPanel, "Branch", BookArt.Picture("tree_branch"), A(light, 0.8f));
            At(branch.rectTransform, 40f, 130f, TwW - 80f, 28f);
            branch.raycastTarget = false;
            if (mine != null)
            {
                // your в'язь: its heap of stones tied by its tree's knot, on the branch
                RectTransform holder = Rect("Holder", _twPanel);
                At(holder, TwW * 0.5f - 80f, 64f, 160f, 160f);
                RectTransform em = BindEmblem(holder, mine, d.BindRunes, 160f);
                if (em != null) TreeTip.Attach(em.gameObject, () => BindPower.Of(mine).Sentence());
            }
            else
                for (int i = 0; i < slots; i++)
                {
                    float cx = 40f + (TwW - 80f) * (i + 0.5f) / slots;
                    Place(cx, 144f, i < laid.Count ? laid[i] : null, Lang.T(Bind.PlaceNames[tree][i]), d, light);
                }

            // the strip: the sentence, short (the whole one when hovered)
            string no = mine != null ? null : _twMix.Count == 0 ? Lang.T("Поклади руни на коріння — тут складеться речення в'язі.") : b.Problem();
            BindPower w = no == null ? BindPower.Of(b) : null;
            RectTransform strip = Rect("Strip", _twPanel);
            At(strip, 40f, 236f, TwW - 80f, 96f);
            Box(strip, 8f, C("#ecdcb4"), C("#b89a62"), 1.5f);
            TMP_Text line = T(strip, no ?? Short(d, laid, w), 22f, no != null ? A(C("#3a2a1a"), 0.6f) : C("#3a2a1a"), no != null ? F.Italic : F.Body, TextAlignmentOptions.Left);
            Stretch(line.rectTransform, Vector2.zero, Vector2.one, new Vector2(26f, 6f), new Vector2(-26f, -6f));
            line.richText = true; line.raycastTarget = false;
            if (w != null) TreeTip.Attach(strip.gameObject, () => w.Sentence());

            // the state: trust, ravens on the tree, the arena, what was just said
            bool seated = SeatZone.Mine(p, _twAt.Perch).Count > 0;
            string trust = BindArena.TrustProblem(tree);
            string state = _twSaid.Length > 0 ? _twSaid
                : mine != null ? Lang.T("Це твоя в'язь. Вона в тебе одна: щоб сплести нову, спершу розплети цю.")
                : BindArena.Running ? Lang.F("Арена: хвиля {0} з {1}.", BindArena.Wave, BindArena.Waves)
                : trust ?? (!seated ? Lang.T("Коріння мовчить, поки на дереві нема твоїх воронів. Посади їх на жердину на верхівці.")
                : Lang.F("Почнеться арена: {0} хвилі ворогів довкола дерева, на все — {1} хв. Встоїш — в'язь сплететься; впадеш, не встигнеш або покинеш коло довше ніж на {2} с — руни лишаться в книзі. Інші гравці можуть допомогти.", slots, Mathf.RoundToInt(BindArena.TimeLimit / 60f), Mathf.RoundToInt(BindArena.OutsideMax)));
            bool bad = mine == null && _twSaid.Length == 0 && (trust != null || !seated);
            TMP_Text st = T(_twPanel, state, 14f, bad ? C("#e08070") : A(light, 0.75f), F.Italic, TextAlignmentOptions.TopLeft);
            At(st.rectTransform, 44f, 340f, TwW - 88f, 44f);

            // the chests
            float cw = (TwW - 80f - 2f * 16f) / 3f;
            WearKind[] order = { WearKind.Freya, WearKind.Baldr, WearKind.Heimdall };
            string[] role = { Lang.T("як"), Lang.T("що"), Lang.T("коли") };
            for (int c = 0; c < 3; c++) Chest(40f + c * (cw + 16f), 390f, cw, 250f, order[c], role[c], d, slots, mine != null);

            // the stone: weave (the arena), or unweave
            bool can = mine != null || (no == null && trust == null && seated && !BindArena.Running);
            WeaveStone(tree, mine != null ? Lang.T("Розплести") : Lang.T("Сплести"), can, () =>
            {
                Player me = Player.m_localPlayer;
                if (mine != null) { RuneBook.Unweave(me); _twSaid = Lang.T("В'язь розплетено. Руни вільні."); TreeWindowDraw(); return; }
                string why = BindArena.Begin(me, _twAt, new List<string>(_twMix));
                if (why != null) Tell(why);
                else { _twMix.Clear(); CloseTreeWindow(); }
            });
        }

        // one of ЩО / КОЛИ / ЯК at the top right: lit by a rune of its kind on the roots
        private static void Mark(float x, string word, bool lit, Color light)
        {
            RectTransform m = Rect("Mark", _twPanel);
            At(m, x, 26f, 76f, 40f);
            Box(m, 5f, new Color(0f, 0f, 0f, 0.25f), lit ? light : A(light, 0.2f), lit ? 2f : 1f);
            TMP_Text t = T(m, word, 17f, lit ? light : A(light, 0.3f), F.Head, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.characterSpacing = 4f; t.raycastTarget = false;
        }

        // one place on the branch: the rune laid there (a click takes it back) or a dashed ring; its name under it
        private static void Place(float cx, float cy, string rune, string name, RuneBook.Data d, Color light)
        {
            RuneDef r = rune != null ? RuneBook.Def(rune) : null;
            RectTransform c = Rect("Place", _twPanel);
            At(c, cx - 40f, cy - 40f, 80f, 80f);
            if (r == null)
            {
                Image ring = Img(c, "Ring", BookArt.Picture("tree_place"), A(light, 0.45f));
                Stretch(ring.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
                ring.raycastTarget = false;
            }
            else
            {
                Image glow = Img(c, "Glow", BookArt.SoftDisc, A(light, 0.35f));
                Stretch(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-14f, -14f), new Vector2(14f, 14f));
                glow.raycastTarget = false;
                if (!RuneStone(c, (int)r.Kind, r.Glyph, 2, 0f, 30f))
                {
                    TMP_Text g = T(c, r.Glyph, 32f, light, F.Rune, TextAlignmentOptions.Center);
                    Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                }
                Image hit = Img(c, "Hit", null, new Color(0f, 0f, 0f, 0f));
                Stretch(hit.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                TreeTip.Attach(hit.gameObject, () => RuneInBind(d, r));
                { string id = rune; Clickable(hit.gameObject, () => { _twMix.Remove(id); _twSaid = ""; TreeWindowDraw(); }); }
            }
            TMP_Text n = T(_twPanel, name, 17f, r != null ? light : A(light, 0.4f), F.Head, TextAlignmentOptions.Top);
            At(n.rectTransform, cx - 90f, cy + 44f, 180f, 26f);
            n.characterSpacing = 3f; n.raycastTarget = false;
        }

        // a god's chest: its eight runes in their sockets (closed ones and laid ones leave the socket empty)
        private static void Chest(float x, float y, float w, float h, WearKind k, string role, RuneBook.Data d, int slots, bool woven)
        {
            RectTransform chest = Rect("Chest", _twPanel);
            At(chest, x, y, w, h);
            Image wood = chest.gameObject.AddComponent<Image>();
            wood.sprite = BookArt.Picture("tree_wood"); wood.type = Image.Type.Tiled; wood.color = Color.white;
            RectTransform edge = Rect("Edge", chest);
            Stretch(edge, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Box(edge, 6f, new Color(0f, 0f, 0f, 0f), C("#5a3e24"), 2f);
            int open = 0;
            List<RuneDef> runes = new List<RuneDef>();
            foreach (RuneDef r in RuneBook.All) if (r.Kind == k) { runes.Add(r); if (d.Has(r.Id)) open++; }
            Color ink = GodInk[(int)k];
            TMP_Text t = T(chest, Lang.T(WearCatalog.KindNames[(int)k]) + "  <size=75%><color=#" + ColorUtility.ToHtmlStringRGB(A(ink, 0.6f)) + ">" + role + "</color></size>", 24f, ink, F.Head, TextAlignmentOptions.TopLeft);
            At(t.rectTransform, 18f, 12f, w - 100f, 34f); t.raycastTarget = false;
            TMP_Text count = T(chest, open + " / " + runes.Count, 17f, A(ink, 0.7f), F.Head, TextAlignmentOptions.TopRight);
            At(count.rectTransform, w - 100f, 16f, 82f, 28f); count.raycastTarget = false;
            float cell = Mathf.Min(76f, (w - 36f - 3f * 8f) / 4f), gap = (w - 36f - 4f * cell) / 3f; // (four in a row with 8 px at least between)
            for (int i = 0; i < runes.Count; i++)
            {
                float cx = 18f + (i % 4) * (cell + gap), cy = 58f + (i / 4) * (cell + 10f);
                RuneCell(chest, cx, cy, cell, runes[i], d, slots, woven);
            }
        }

        private static void RuneCell(RectTransform chest, float x, float y, float size, RuneDef r, RuneBook.Data d, int slots, bool woven)
        {
            RectTransform c = Rect("Rune_" + r.Id, chest);
            At(c, x, y, size, size);
            bool open = d.Has(r.Id), laid = _twMix.Contains(r.Id), taken = d.InBind(r.Id);
            Image socket = Img(c, "Socket", BookArt.Picture(GodSocket[(int)r.Kind]), Color.white);
            Stretch(socket.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            socket.raycastTarget = false;
            if (!open) return; // (a socket waiting for its rune)
            if (laid)
            {
                TMP_Text ghost = T(c, r.Glyph, 26f, A(GodInk[(int)r.Kind], 0.25f), F.Rune, TextAlignmentOptions.Center); // (it lies on the roots)
                Stretch(ghost.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                ghost.raycastTarget = false;
            }
            else
            {
                RectTransform stone = Rect("Stone", c);
                Stretch(stone, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
                if (!RuneStone(stone, (int)r.Kind, r.Glyph, 1, 0f, 24f))
                {
                    TMP_Text g = T(stone, r.Glyph, 26f, GodInk[(int)r.Kind], F.Rune, TextAlignmentOptions.Center);
                    Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                }
                if (taken || woven) { Image dimmer = Img(c, "Dim", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.55f)); Stretch(dimmer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); dimmer.raycastTarget = false; }
            }
            int stars = d.StarsOf(r.Id);
            if (stars > 0)
            {
                SparkRow(c, stars, false);
            }
            Element el = new BindPart { Kind = r.Kind, Wear = d.WearFor(r.Id) }.El;
            if (el != Element.None)
            {
                Image dot = Img(c, "El", BookArt.Circle, C("#" + Elements.Hex[(int)el]));
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = dot.rectTransform.pivot = new Vector2(1f, 1f);
                dot.rectTransform.sizeDelta = new Vector2(11f, 11f); dot.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
                dot.raycastTarget = false;
            }
            Image hit = Img(c, "Hit", null, new Color(0f, 0f, 0f, 0f));
            Stretch(hit.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TreeTip.Attach(hit.gameObject, () => RuneInBind(d, r));
            string id = r.Id;
            Clickable(hit.gameObject, () =>
            {
                if (_twMix.Contains(id)) _twMix.Remove(id);
                else if (RuneBook.Of(Player.m_localPlayer).Bind.Length > 0) Tell(Lang.T("Спершу розплети свою в'язь."));
                else if (_twMix.Count < slots) _twMix.Add(id);
                else Tell(Lang.F("На цьому дереві вміщається {0}.", Lang.N(slots, "руна|руни|рун")));
                _twSaid = "";
                TreeWindowDraw();
            });
        }

        // the tree's own stone with «Сплести» (or «Розплести») cut in it between two of its runes
        private static void WeaveStone(int tree, string word, bool can, Action act)
        {
            RectTransform s = Rect("Weave", _twPanel);
            At(s, (TwW - 500f) * 0.5f, 664f, 500f, 125f);
            Image im = Img(s, "Stone", BookArt.Picture(TreeStoneArt[tree]), can ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.9f));
            Stretch(im.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            im.preserveAspect = true;
            string r = TreeStoneRune[tree];
            TMP_Text t = T(s, r + "   " + word.ToUpperInvariant() + "   " + r, 32f, can ? TreeStoneInk[tree] : A(TreeStoneInk[tree], 0.5f), F.Head, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero);
            t.characterSpacing = 6f; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
            if (can) Clickable(im.gameObject, act);
        }

        // ---- the sentence, short: «ᛗ Коли робиш перекат, ᚺ блискавка б'є 3 найближчих ворогів, ᚠ ширше.»
        internal static readonly Dictionary<string, string> ShortWhat = new Dictionary<string, string>
        {
            { "flash", Lang.K("спалах вогню обпікає ворогів довкола") }, { "frost", Lang.K("морозна хвиля сковує ворогів") }, { "chain", Lang.K("блискавка б'є {0} найближчих ворогів") },
            { "heal", Lang.K("тебе лікує") }, { "poison", Lang.K("хмара отрути труїть ворогів") }, { "vanish", Lang.K("вороги (крім босів) тебе гублять") },
            { "leech", Lang.K("твої удари лікують тебе") }, { "shield", Lang.K("тебе закриває щит") }, { "gust", Lang.K("вихор відкидає ворогів") },
        };
        internal static readonly Dictionary<string, string> ShortHow = new Dictionary<string, string>
        {
            { "radius", Lang.K("ширше") }, { "duration", Lang.K("довше") }, { "double", Lang.K("часом двічі") }, { "allyheal", Lang.K("і лікує друзів") },
            { "recharge", Lang.K("швидше заряджається") }, { "night", Lang.K("уночі сильніше") }, { "blood", Lang.K("за кров — удвічі сильніше") }, { "power", Lang.K("сильніше") },
        };

        private static string Sign(RuneDef r) { return "<color=#" + ColorUtility.ToHtmlStringRGB(SignInk) + ">" + r.Glyph + "</color> "; }

        // the в'язь in one short sentence, each part after its rune's sign: «ᛖ Коли біжиш, ᚲ спалах вогню, ᚱ ширше.»
        private static string Short(RuneBook.Data d, List<string> runes, BindPower w)
        {
            List<KeyValuePair<string, string>> when = new List<KeyValuePair<string, string>>(), what = new List<KeyValuePair<string, string>>(), how = new List<KeyValuePair<string, string>>();
            bool firstBaldr = true;
            foreach (string id in runes)
            {
                RuneDef r = RuneBook.Def(id);
                if (r == null) continue;
                string a = BindPower.ActionOf(new BindPart { Kind = r.Kind, Wear = d.WearFor(id) }), s;
                if (r.Kind == WearKind.Heimdall) { if (BindPower.WhenWords.TryGetValue(a, out s)) when.Add(Part(r, (when.Count == 0 ? Lang.T("Коли") + " " : "") + Lang.T(s))); }
                else if (r.Kind == WearKind.Freya) { if (ShortHow.TryGetValue(a, out s)) how.Add(Part(r, Lang.T(s))); }
                else
                {
                    if (firstBaldr && w.Mode == BindMode.Button) what.Add(Part(r, Lang.F("кнопка в колі дарів — «{0}»", w.ActiveTitle())));
                    else if (ShortWhat.TryGetValue(a, out s)) what.Add(Part(r, Lang.F(s, w.Targets)));
                    firstBaldr = false;
                }
            }
            if (w.Mode == BindMode.Motion) what.Add(new KeyValuePair<string, string>("", Lang.T("ти мчиш швидше")));
            // a sentence begins with a capital: its first part's words (after the sign)
            List<KeyValuePair<string, string>> first = when.Count > 0 ? when : what.Count > 0 ? what : how;
            if (first.Count > 0 && first[0].Value.Length > 0) first[0] = new KeyValuePair<string, string>(first[0].Key, char.ToUpper(first[0].Value[0]) + first[0].Value.Substring(1));
            string line = when.Count > 0 ? Join(when, " " + Lang.T("або") + " ") + ", " + Join(what, " " + Lang.T("і") + " ") : Join(what, " " + Lang.T("і") + " ");
            if (how.Count > 0) line += ", " + Join(how, ", ");
            return line + ".";
        }

        private static KeyValuePair<string, string> Part(RuneDef r, string text) { return new KeyValuePair<string, string>(Sign(r), text); }

        private static string Join(List<KeyValuePair<string, string>> parts, string sep)
        {
            string[] a = new string[parts.Count];
            for (int i = 0; i < parts.Count; i++) a[i] = parts[i].Key + parts[i].Value;
            return string.Join(sep, a);
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
                + (d.StarsOf(r.Id) > 0 ? "\n" + Lang.F("{0} — у в'язі +{1}% сили.", RuneStars.SparkWord(d.StarsOf(r.Id)), Mathf.RoundToInt(d.StarsOf(r.Id) * RuneStars.BindPerStar * 100f)) : "")
                + (d.InBind(r.Id) ? "\n\n" + Lang.T("Зараз у твоїй в'язі.") : "")
                + "\n\n" + Lang.T("Клік — покласти на коріння або забрати.");
        }
    }
}
#endif
