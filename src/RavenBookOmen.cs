#if RAVEN_ITEM
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // Hugin's «Віщування» with a raid in it: the post's card (as the invitation) near the top of the screen — what
    // Hugin said, the raid and when, the day's weather; «Відвести» (a day later, Hugin's strength, it comes back
    // grown), «Накликати» (now, here) or «Хай буде» (Esc too). The time left runs down at the bottom. While it is up the
    // game treats it as a window (RavenBookAsWindowPatch / InviteNoSwingPatch).
    internal static partial class RavenBook
    {
        private static GameObject _omenRoot;
        private static TMP_Text _omenTime;
        private static float _omenUntil;
        internal static bool OmenOpen { get { return _omenRoot != null && _omenRoot.activeSelf; } }

        internal static void ShowOmen(OmenFate.Answer a, string said, string raid, string when, string weather, bool bad)
        {
            HideOmen();
            Player me = Player.m_localPlayer;
            if (me == null || a == null) return;
            _omenUntil = Time.time + Omen.CardSeconds;
            _omenRoot = new GameObject("RavenWatch_Omen", typeof(RectTransform));
            Object.DontDestroyOnLoad(_omenRoot);
            Canvas canvas = _omenRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30002;
            CanvasScaler sc = _omenRoot.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            _omenRoot.AddComponent<GraphicRaycaster>();
            _omenRoot.AddComponent<OmenKeys>();
            RectTransform root = (RectTransform)_omenRoot.transform;

            RectTransform frame = Rect("Frame", root);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 1f);
            frame.pivot = new Vector2(0.5f, 1f);
            frame.anchoredPosition = new Vector2(0f, -150f);
            CardPiece(frame.gameObject.AddComponent<Image>(), "card_frame", 32f, 12f, C("#2a1b10"));
            VBox(frame, 0f, TextAnchor.UpperCenter, 16, 16, 36, 0);
            frame.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            frame.sizeDelta = new Vector2(600f, 0f);

            // the parchment: Hugin's word, then the raid, the weather, the omen
            RectTransform paper = Rect("Paper", frame);
            CardPiece(paper.gameObject.AddComponent<Image>(), "card_paper", 24f, 10f, Paper);
            VBox(paper, 10f, TextAnchor.UpperCenter, 24, 24, 20, 18);
            RectTransform head = Rect("Head", paper);
            HBox(head, 14f, TextAnchor.MiddleLeft);
            RectTransform pic = Rect("Pic", head);
            Fixed(pic.gameObject, 64f, 64f);
            Img(pic, "Ring", BookArt.Circle, C("#2f4f7a"));
            Image face = Img(pic, "Face", AbilityIcons.For("homen", false), Color.white);
            face.preserveAspect = true;
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            TMP_Text say = T(head, string.IsNullOrEmpty(said) ? Lang.T("Хугін бачив набіг.") : "«" + Grammar.Fix(said, me) + "»", 19f, PInk, F.Italic, TextAlignmentOptions.MidlineLeft);
            say.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            T(paper, Lang.F("<b>Набіг:</b> {0} · {1}", raid, when), 17f, PInk, F.Body, TextAlignmentOptions.Center);
            if (a.Grown) T(paper, Lang.T("Ти вже відводи{в|ла} його раз — тепер він прийде злішим."), 15f, C("#8E1E1E"), F.Italic, TextAlignmentOptions.Center);
            if (!string.IsNullOrEmpty(weather)) T(paper, Lang.F("<b>Небо:</b> {0}.", weather), 15f, PSoft, F.Body, TextAlignmentOptions.Center);
            if (bad) T(paper, Lang.T("<b>Недобрий знак:</b> загинеш до ранку — ворони втратять менше довіри."), 15f, C("#8E1E1E"), F.Body, TextAlignmentOptions.Center);
            if (a.Players > 1) T(paper, Lang.T("Набіги в цьому світі спільні: твій вибір — для всіх."), 14f, PSoft, F.Italic, TextAlignmentOptions.Center);

            string turnWhy = Omen.WhyNotTurn(a), callWhy = a.SummonWhy ?? "";
            T(paper, turnWhy == "" ? Lang.F("<b>Відвести</b> — на добу пізніше, −{0} сили Хугіна; прийде злішим.", Mathf.RoundToInt(Omen.TurnCost()))
                : Lang.F("<b>Відвести</b> — не можна: {0}.", turnWhy), 14f, turnWhy == "" ? PSoft : C("#8E1E1E"), F.Body, TextAlignmentOptions.Center);
            T(paper, callWhy == "" ? Lang.T("<b>Накликати</b> — зараз, тут, поки ти готов{ий|а} і хочеш трофеїв.")
                : Lang.F("<b>Накликати</b> — не можна: {0}.", Omen.Why(callWhy)), 14f, callWhy == "" ? PSoft : C("#8E1E1E"), F.Body, TextAlignmentOptions.Center);

            // the dark strip: the time left and the three choices
            RectTransform foot = Rect("Foot", frame);
            HBox(foot, 12f, TextAnchor.MiddleRight, 6, 0, 12, 14);
            RectTransform words = Rect("Words", foot);
            VBox(words, 0f, TextAnchor.MiddleLeft);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _omenTime = T(words, "", 15f, FootSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
            CardButton(foot, Lang.T("Відвести"), false, () => { HideOmen(); Omen.Turn(); }, "ᚱ", turnWhy == "");
            CardButton(foot, Lang.T("Накликати"), true, () => { HideOmen(); Omen.Summon(); }, "ᛏ", callWhy == "");
            CardButton(foot, Lang.T("Хай буде"), false, HideOmen);

            Plaque(frame, Lang.T("Віщування"), 38f, false);
        }

        internal static void HideOmen()
        {
            if (_omenRoot != null) Object.Destroy(_omenRoot);
            _omenRoot = null; _omenTime = null;
        }

        // the time left (and Esc: let it be), each frame while the card is up
        internal sealed class OmenKeys : MonoBehaviour
        {
            private int _shownSec = -1;
            private void Update()
            {
                Player p = Player.m_localPlayer;
                float left = _omenUntil - Time.time;
                if (p == null || p.IsDead() || left <= 0f) { HideOmen(); return; }
                int sec = Mathf.CeilToInt(left);
                if (_omenTime != null && sec != _shownSec) { _shownSec = sec; _omenTime.text = Lang.F("ще {0} с · Esc — хай буде", sec); }
                if (RavenKeys.Down(KeyCode.Escape)) HideOmen();
            }
        }
    }
}
#endif
