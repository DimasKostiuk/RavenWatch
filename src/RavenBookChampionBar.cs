#if RAVEN_ITEM
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // An errand's champion's own bar at the top of the screen, as a boss's (every champion, not only a lord; the game's
    // own bar over it is off, ChampionHudPatch): its own name in gold («Гнилозуб, ватажок ★★★»), under it what it is and
    // its traits, and its health. Shown for the nearest one alive within reach of you; its own canvas, so no UI mod
    // that redraws the game's bars takes it away.
    internal static partial class RavenBook
    {
        internal static float ChampionBarReach = 45f;
        private static GameObject _cbRoot;
        private static RectTransform _cbFill;
        private static TMP_Text _cbName, _cbSub, _cbHp;
        private static int _cbHpWas = -1, _cbMaxWas = -1;
        private static Character _cbFor;
        private static float _cbLook, _cbShown;

        // every frame (yours): who it is for, twice a second; its health smoothly
        internal static void ChampionBarTick()
        {
            Player p = Player.m_localPlayer;
            if (Time.time >= _cbLook)
            {
                _cbLook = Time.time + 0.5f;
                Character c = p != null && !p.IsDead() && Champions.Any ? Champions.Nearest(p.transform.position, ChampionBarReach) : null;
                if (c != _cbFor) { _cbFor = c; if (c != null) ChampionBarFor(c); }
            }
            if (_cbFor == null || _cbFor.IsDead()) { if (_cbRoot != null && _cbRoot.activeSelf) _cbRoot.SetActive(false); _cbFor = null; return; }
            float max = _cbFor.GetMaxHealth(), hp = _cbFor.GetHealth(), k = max > 0f ? Mathf.Clamp01(hp / max) : 0f;
            _cbShown = Mathf.MoveTowards(_cbShown, k, Time.deltaTime * 0.8f);
            Vector2 fill = new Vector2(Mathf.Max(0.001f, _cbShown), 1f);
            if (_cbFill.anchorMax != fill) _cbFill.anchorMax = fill; // (only while it moves: a still bar leaves its canvas alone)
            int shownHp = Mathf.CeilToInt(hp), shownMax = Mathf.CeilToInt(max);
            if (shownHp != _cbHpWas || shownMax != _cbMaxWas) { _cbHpWas = shownHp; _cbMaxWas = shownMax; _cbHp.text = shownHp + " / " + shownMax; } // (the text only when the numbers change)
        }

        private static void ChampionBarFor(Character c)
        {
            Champions.Mods m = Champions.Fast(c);
            if (m == null) { _cbFor = null; return; }
            if (_cbRoot == null) ChampionBarMake();
            _cbRoot.SetActive(true);
            _cbName.text = Champions.Name(m);
            string w = Champions.Words(m);
            _cbSub.text = ErrandSteps.Creature(m.Who) + (w.Length > 0 ? " · " + w : ""); // (ватажок / охоронець: already in its name)
            float max = c.GetMaxHealth();
            _cbShown = max > 0f ? Mathf.Clamp01(c.GetHealth() / max) : 0f;
            _cbHpWas = _cbMaxWas = -1;
        }

        private static void ChampionBarMake()
        {
            EnsureGameFont();
            _cbRoot = OverlayCanvas("RavenWatch_ChampionBar", 50, false); // (under the book and the windows)

            RectTransform box = Rect("Bar", _cbRoot.transform);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 1f);
            box.sizeDelta = new Vector2(640f, 96f);
            box.anchoredPosition = new Vector2(0f, -36f);
            _cbName = T(box, "", 26f, C("#FFC860"), F.Head, TextAlignmentOptions.Top);
            At(_cbName.rectTransform, 0f, 0f, 640f, 34f);
            _cbName.characterSpacing = 3f; _cbName.textWrappingMode = TextWrappingModes.NoWrap; _cbName.raycastTarget = false;
            _cbName.outlineWidth = 0.15f; _cbName.outlineColor = new Color32(0, 0, 0, 200);
            _cbSub = T(box, "", 15f, C("#E8DCC0"), F.Italic, TextAlignmentOptions.Top);
            At(_cbSub.rectTransform, 0f, 34f, 640f, 22f);
            _cbSub.textWrappingMode = TextWrappingModes.NoWrap; _cbSub.raycastTarget = false;

            RectTransform frame = Rect("Frame", box);
            At(frame, 20f, 60f, 600f, 22f);
            Box(frame, 5f, new Color(0.08f, 0.04f, 0.03f, 0.85f), C("#B8903C"), 1.5f);
            RectTransform track = Rect("Track", frame);
            Stretch(track, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            _cbFill = Rect("Fill", track);
            _cbFill.anchorMin = Vector2.zero; _cbFill.anchorMax = Vector2.one; _cbFill.offsetMin = Vector2.zero; _cbFill.offsetMax = Vector2.zero;
            Box(_cbFill, 3f, C("#A8281C"), new Color(0f, 0f, 0f, 0f), 0f);
            _cbHp = T(frame, "", 13f, C("#F4E8D0"), F.Body, TextAlignmentOptions.Center);
            Stretch(_cbHp.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _cbHp.raycastTarget = false;
            foreach (Graphic g in _cbRoot.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false; // (never in the way of a click)
        }
    }
}
#endif
