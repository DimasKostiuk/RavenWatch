#if RAVEN_ITEM
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The Ravens page's margin (the user's design, 2026-10-28): down the page's outer edge, between two runes ᛟ, three
    // medallions that always stand in their place — the bath (how dirty, the fresh feathers' time; red past the line
    // where gains fall), the acquaintances (friends · foes, RavenBookKin) and the taught words (RavenBookWords). One
    // with nothing in it is faded; none for a stranger's raven.
    internal static partial class RavenBook
    {
        private const float RailTop = 168f, RailW = 44f;
        private static float RailX(bool munin) { return munin ? 32f : BookW - 32f - RailW; }

        private static readonly Color MedalFace = C("#E6D5AE"), MedalRim = C("#A07A2C"), MedalBright = C("#C9A24A"), MedalBad = C("#9A3A2A"),
            RailInk = C("#8A6A2E"), RailDot = C("#A08868");

        private sealed class Medal
        {
            public RectTransform Disc;
            public Image Ring;
            public TMP_Text Num;
            public CanvasGroup Fade;
        }

        private static void Rail(bool m, ItemDrop.ItemData item)
        {
            RectTransform rail = Rect(m ? "RailMunin" : "RailHugin", _pages);
            At(rail, RailX(m), RailTop, RailW, 0f);
            VBox(rail, 6f, TextAnchor.UpperCenter).childForceExpandWidth = false;
            rail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RailLine(rail, true);
            RailLine(rail, false);
            RailRune(rail, "ᛟ", 13f);
            BathMedal(rail, m, item);
            RailRune(rail, "᛬", 12f);
            KinMedal(rail, m, item);
            RailRune(rail, "᛬", 12f);
            WordsMedal(rail, m, item);
            RailRune(rail, "ᛟ", 13f);
        }

        // a thin gold line fading out over the rail (top) or under it
        private static void RailLine(RectTransform rail, bool top)
        {
            Image l = Img(rail, "Line", BookArt.Fade(top), A(MedalRim, 0.6f));
            l.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RectTransform r = l.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, top ? 1f : 0f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(30f, 2f);
            r.anchoredPosition = new Vector2(0f, top ? 17f : -17f);
            r.localRotation = Quaternion.Euler(0f, 0f, -90f); // (the faded end away from the rail)
        }

        private static void RailRune(RectTransform rail, string rune, float size)
        {
            TMP_Text t = T(rail, rune, size, RailInk, F.Rune, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Fixed(t.gameObject, RailW, size + 2f);
        }

        // a medallion: the gold ring, the parchment face with its thin inner ring, the picture; the number under it
        private static Medal Medallion(RectTransform rail, string name, string picture)
        {
            Medal md = new Medal();
            RectTransform box = Rect(name, rail);
            VBox(box, 2f, TextAnchor.UpperCenter).childForceExpandWidth = false;
            Fixed(box.gameObject, RailW, -1f);
            md.Fade = box.gameObject.AddComponent<CanvasGroup>();
            RectTransform disc = Rect("Disc", box);
            Fixed(disc.gameObject, 40f, 40f);
            md.Disc = disc;
            md.Ring = Img(disc, "Ring", BookArt.Circle, MedalRim);
            Image face = Img(disc, "Face", BookArt.Circle, MedalFace);
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            Image inner = Img(disc, "Inner", BookArt.Circle, A(MedalRim, 0.7f));
            Stretch(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image face2 = Img(disc, "Face2", BookArt.Circle, MedalFace);
            Stretch(face2.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            Sprite pic = BookArt.Picture(picture);
            if (pic != null)
            {
                Image p = Img(disc, "Picture", pic, Color.white);
                p.preserveAspect = true;
                Stretch(p.rectTransform, Vector2.zero, Vector2.one, new Vector2(7f, 7f), new Vector2(-7f, -7f));
            }
            md.Num = T(box, "", 15f, Ink, F.Head, TextAlignmentOptions.Center);
            md.Num.textWrappingMode = TextWrappingModes.NoWrap;
            Fixed(md.Num.gameObject, RailW, 16f);
            return md;
        }

        // a click on the medallion (its ring lights up under the pointer)
        private static void MedalClick(Medal md, System.Action click)
        {
            Clickable(md.Disc.gameObject, click);
            Button b = md.Disc.GetComponent<Button>();
            b.targetGraphic = md.Ring;
            md.Ring.CrossFadeColor(b.colors.normalColor, 0f, true, true);
        }

        // something new in it (a bond changed, a word caught): a gold glow breathing behind the medallion
        private static void MedalBreath(Medal md)
        {
            Image glow = Img(md.Disc, "Glow", BookArt.Glow, A(Gold, 0.7f));
            glow.transform.SetAsFirstSibling();
            Stretch(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-10f, -10f), new Vector2(10f, 10f));
            SparkBreath b = glow.gameObject.AddComponent<SparkBreath>();
            b.Low = 0.15f; b.High = 0.8f;
        }

        // the bath: how dirty (0–100), red past the line where its gains fall; under it ᛊ and the fresh feathers' time
        private static void BathMedal(RectTransform rail, bool m, ItemDrop.ItemData item)
        {
            Medal md = Medallion(rail, "Bath", "rail_tub");
            RectTransform fresh = Rect("Fresh", md.Num.transform.parent);
            HBox(fresh, 1f, TextAnchor.MiddleCenter);
            Fixed(fresh.gameObject, RailW, 13f);
            TMP_Text rune = T(fresh, "", 11f, GoldInk, F.Rune, TextAlignmentOptions.Center);
            TMP_Text time = T(fresh, "", 11f, GoldInk, F.Body, TextAlignmentOptions.Center);
            rune.textWrappingMode = time.textWrappingMode = TextWrappingModes.NoWrap;
            ItemDrop.ItemData it = item;
            System.Action live = () =>
            {
                if (md.Num == null) return;
                float dirt = Bath.DirtOf(it), left = Bath.FreshLeft(it);
                bool filthy = Filthy(Player.m_localPlayer, it);
                md.Ring.color = filthy ? MedalBad : left > 0f ? MedalBright : MedalRim;
                md.Num.color = filthy ? MedalBad : Ink;
                md.Num.text = "<b>" + Mathf.RoundToInt(dirt) + "</b>";
                rune.text = left > 0f ? "ᛊ" : "";
                time.text = left > 0f ? "<b>" + RavenAbilities.Clock(left) + "</b>" : "";
            };
            live();
            Live.Add(live);
            Tip(md.Disc.gameObject, () => "<b>" + Bath.BookNumbers(it) + "</b>\n\n" + Bath.BookTip(it, m)); // (its stage and the fresh feathers' time in words first)
        }
    }
}
#endif
