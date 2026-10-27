#if RAVEN_ITEM
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // Which raven? — the post's card on a canvas of its own, over the game in the middle (the book need not be open),
    // for a stone used from the bag: «Поклик Одіна» (OdinCall), «Камінь долі» (FateStone). A card for each raven: its
    // picture, name and a word on it (or why it cannot), lit under the pointer; a click does the stone's deed with that
    // one. «Скасувати» or Esc close it, the stone kept.
    internal sealed class RavenPick
    {
        public string Title, Say, Sub;
        public Func<bool, string> WhyNot; // (munin) null: this raven can
        public Func<bool, string> Note;   // (munin) the word under its name when it can
        public Func<bool, string> Act;    // (munin) the deed; null, or what to tell
    }

    internal static partial class RavenBook
    {
        private static GameObject _pickRoot;
        internal static bool PickOpen { get { return _pickRoot != null && _pickRoot.activeSelf; } }

        internal static void ShowRavenPick(RavenPick pick)
        {
            HidePick();
            Player me = Player.m_localPlayer;
            if (me == null || pick == null) return;
            EnsureGameFont();
            _pickRoot = OverlayCanvas("RavenWatch_RavenPick", 30002, true);
            _pickRoot.AddComponent<PickKeys>();
            RectTransform root = (RectTransform)_pickRoot.transform;

            RectTransform frame = Rect("Frame", root);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = new Vector2(0f, 60f);
            CardPiece(frame.gameObject.AddComponent<Image>(), "card_frame", 32f, 12f, C("#2a1b10"));
            VBox(frame, 0f, TextAnchor.UpperCenter, 16, 16, 36, 0);
            frame.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            frame.sizeDelta = new Vector2(560f, 0f);

            RectTransform paper = Rect("Paper", frame);
            CardPiece(paper.gameObject.AddComponent<Image>(), "card_paper", 24f, 10f, Paper);
            VBox(paper, 12f, TextAnchor.UpperCenter, 24, 24, 20, 18);
            T(paper, pick.Say, 18f, PInk, F.Body, TextAlignmentOptions.Center);
            if (!string.IsNullOrEmpty(pick.Sub)) T(paper, pick.Sub, 15f, PSoft, F.Italic, TextAlignmentOptions.Center);
            RectTransform row = Rect("Ravens", paper);
            HBox(row, 14f, TextAnchor.MiddleCenter);
            foreach (bool m in new[] { false, true })
            {
                bool munin = m;
                string why = pick.WhyNot(munin);
                RectTransform b = PaperChoice(row, false, why == null);
                VBox(b, 4f, TextAnchor.MiddleCenter, 14, 14, 10, 10);
                Fixed(b.gameObject, 220f, -1f);
                RectTransform rp = Rect("Pic", b);
                Fixed(rp.gameObject, 52f, 52f);
                Image ri = Img(rp, "Face", AbilityIcons.For(munin ? "munin" : "hugin", munin), why == null ? Color.white : new Color(1f, 1f, 1f, 0.45f));
                ri.preserveAspect = true;
                Stretch(ri.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Color ink = munin ? C("#8E1E1E") : C("#2f4f7a");
                TMP_Text name = T(b, "<b>" + Nicks.Name(me, munin) + "</b>", 18f, ink, F.Body, TextAlignmentOptions.Center);
                TMP_Text note = T(b, why ?? pick.Note(munin), 14f, why == null ? PSoft : C("#8E1E1E"), F.Italic, TextAlignmentOptions.Center);
                if (why != null) continue;
                Button btn = b.gameObject.AddComponent<Button>();
                btn.targetGraphic = b.GetComponent<Image>();
                btn.transition = Selectable.Transition.None; // (the hover below lights it instead)
                // under the pointer the card lifts a little, its name and words turn gold, its edge and fill too
                Hover(b.gameObject, new Graphic[] { name }, ink, b.GetComponent<Image>());
                Hover(note.gameObject, new Graphic[] { note }, PSoft, null, b.gameObject);
                b.GetComponent<InkHover>().Grow = 0.04f;
                Transform line = b.Find("Line");
                if (line != null) Hover(line.gameObject, new Graphic[] { line.GetComponent<Image>() }, line.GetComponent<Image>().color, null, b.gameObject);
                btn.onClick.AddListener(() =>
                {
                    Player p = Player.m_localPlayer;
                    HidePick();
                    string msg = p != null ? pick.Act(munin) : null;
                    if (msg != null && p != null) RavenMsg.Center(p, msg);
                });
            }

            RectTransform foot = Rect("Foot", frame);
            HBox(foot, 12f, TextAnchor.MiddleRight, 6, 0, 12, 14);
            RectTransform words = Rect("Words", foot);
            VBox(words, 0f, TextAnchor.MiddleLeft);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            T(words, Lang.T("Esc — скасувати"), 16f, FootSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
            CardButton(foot, Lang.T("Скасувати"), false, HidePick);

            Plaque(frame, pick.Title, 38f, false);
        }

        internal static void HidePick()
        {
            if (_pickRoot != null) UnityEngine.Object.Destroy(_pickRoot);
            _pickRoot = null;
        }

        internal sealed class PickKeys : MonoBehaviour
        {
            private void Update()
            {
                if (Player.m_localPlayer == null || RavenKeys.Down(KeyCode.Escape)) HidePick();
            }
        }
    }
}
#endif
