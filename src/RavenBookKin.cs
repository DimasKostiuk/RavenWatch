#if RAVEN_ITEM
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // «Знайомі» in the book (BondLog): a seal with the rune ᛗ (people, kin) and the number of ravens met, beside the
    // raven's name on «Ворони» — only once it has met a raven of another player (alone, there is nothing to show). A
    // click opens the post-style card: each raven it knows (its picture, name, whose it is), how they get along — in
    // words, once it has shown itself — and when they last met; on hover the story of the two and why it is so.
    internal static partial class RavenBook
    {
        private static readonly Color KinWax = C("#6b4a22");
        internal static int KinShown = 40; // ravens (and people) listed in the card (the closest bonds and the latest met first)
        private const float KinViewMax = 480f; // the lists' height before they scroll: about seven rows with both headers

        private static void KinSeal(RectTransform top, bool m, ItemDrop.ItemData item)
        {
            List<BondLog.Rec> all = BondLog.Of(item);
            List<People.Rec> folk = People.Of(item);
            if (all.Count == 0 && folk.Count == 0) return;
            int friends, foes;
            Tally(item, all, out friends, out foes);
            int liked, hated;
            TallyFolk(folk, out liked, out hated);
            RectTransform box = Rect("Kin", top);
            Fixed(box.gameObject, 54f, 66f);
            RectTransform seal = Rect("Seal", box);
            seal.anchorMin = seal.anchorMax = seal.pivot = new Vector2(0.5f, 1f);
            seal.sizeDelta = new Vector2(46f, 46f);
            seal.anchoredPosition = new Vector2(0f, -2f);
            bool fresh = BondLog.AnyFresh(item) || People.AnyFresh(item);
            if (fresh) // a bond changed since you looked: it breathes, as a wish waiting
            {
                Image glow = Img(seal, "Glow", BookArt.Glow, A(Gold, 0.7f));
                Stretch(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-10f, -10f), new Vector2(10f, 10f));
                SparkBreath b = glow.gameObject.AddComponent<SparkBreath>();
                b.Low = 0.15f; b.High = 0.8f;
            }
            Image ring = Img(seal, "Ring", BookArt.Circle, Color.Lerp(KinWax, Color.black, 0.35f));
            Image face = Img(seal, "Face", BookArt.Circle, KinWax);
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            TMP_Text g = T(seal, "ᛗ", 22f, C("#f6e2c0"), F.Rune, TextAlignmentOptions.Center);
            Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text n = T(box, (all.Count + folk.Count).ToString(), 13f, InkSoft, F.Body, TextAlignmentOptions.Bottom);
            Stretch(n.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            foreach (Image i in seal.GetComponentsInChildren<Image>(true)) i.raycastTarget = i == ring;
            ring.raycastTarget = true;
            int f = friends, e = foes, c = all.Count + folk.Count, lk = liked, ht = hated, nf = folk.Count;
            Tip(ring.gameObject, () => Lang.F("<b>Знайомі {0}: {1}</b>\n{2}\n\nКлік — хто з ким дружить і чому.", Plugin.RavenNameGen(m), c,
                Lang.F("Друзів: {0} · ворогів: {1}", f, e) + (nf > 0 ? "\n" + Lang.F("Людей: {0} · поважає: {1} · не терпить: {2}", nf, lk, ht) : ""))
                + (fresh ? "\n<color=#FFD54F>" + Lang.T("Є зміни у стосунках.") + "</color>" : ""));
            ItemDrop.ItemData mine = item;
            Clickable(ring.gameObject, () => KinCard(seal, m, mine)); // (this raven: the one whose seal it is)
        }

        // friends and foes among the ones that have shown how they get along
        private static void Tally(ItemDrop.ItemData item, List<BondLog.Rec> all, out int friends, out int foes)
        {
            friends = foes = 0;
            foreach (BondLog.Rec r in all)
            {
                float s, st;
                if (!r.Shown || !Bonds.Stored(item, r.Rid, out s, out st)) continue;
                Bonds.State b = Bonds.StateOf(s);
                if (b >= Bonds.State.Pals) friends++;
                else if (b == Bonds.State.Enemy) foes++;
            }
        }

        private static void KinCard(RectTransform by, bool m, ItemDrop.ItemData item)
        {
            ClosePopup();
            if (item == null) return;
            BondLog.Rename(item); // a raven renamed since they met: its new name
            List<BondLog.Rec> all = new List<BondLog.Rec>(BondLog.Of(item));
            // the strongest bonds that have shown themselves first, then the latest met
            all.Sort((a, b) =>
            {
                float sa = Strength(item, a), sb = Strength(item, b);
                return sa != sb ? sb.CompareTo(sa) : b.Last.CompareTo(a.Last);
            });
            List<People.Rec> folk = new List<People.Rec>(People.Of(item));
            folk.Sort((a, b) =>
            {
                float sa = a.Shown ? Mathf.Abs(a.Score) : -1f, sb = b.Shown ? Mathf.Abs(b.Score) : -1f;
                return sa != sb ? sb.CompareTo(sa) : b.Last.CompareTo(a.Last);
            });
            Card c = OpenCard("KinCard", Lang.F("Знайомі {0}", Plugin.RavenNameGen(m)), 520f, true);
            T(c.Paper, Lang.F("Кого {0} знає серед інших гравців і їхніх воронів. Наведи на рядок — історія й чому так.", Plugin.RavenName(m)), 15f, PSoft, F.Italic);
            // the lists scroll under the words above once they are taller than KinViewMax (the card stays inside the book)
            RectTransform view = Rect("View", c.Paper);
            Fixed(view.gameObject, -1f, KinViewMax);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            RectTransform list = Rect("List", view);
            list.anchorMin = new Vector2(0f, 1f); list.anchorMax = new Vector2(1f, 1f); list.pivot = new Vector2(0.5f, 1f);
            list.sizeDelta = Vector2.zero;
            VBox(list, 10f, TextAnchor.UpperLeft, 0, 10);
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view; sr.content = list; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 120f; // a notch of the wheel: about two rows
            ScrollArrow(view, sr, 26f);
            if (all.Count > 0)
            {
                if (folk.Count > 0) T(list, "<b>" + Lang.T("Ворони") + "</b>", 16f, PLabel, F.Body);
                for (int i = 0; i < all.Count && i < KinShown; i++) KinRow(list, item, m, all[i]);
                if (all.Count > KinShown) T(list, Lang.F("…і ще {0}", Lang.N(all.Count - KinShown, "ворон|ворони|воронів")), 14f, PSoft, F.Italic);
            }
            if (folk.Count > 0)
            {
                T(list, "<b>" + Lang.T("Люди") + "</b>", 16f, PLabel, F.Body);
                for (int i = 0; i < folk.Count && i < KinShown; i++) FolkRow(list, item, m, folk[i]);
                if (folk.Count > KinShown) T(list, Lang.F("…і ще {0}", Lang.N(folk.Count - KinShown, "гравець|гравці|гравців")), 14f, PSoft, F.Italic);
            }
            int friends, foes, liked, hated;
            Tally(item, all, out friends, out foes);
            TallyFolk(folk, out liked, out hated);
            T(c.FootWords, Lang.F("Друзів: {0} · ворогів: {1}", friends, foes) + (folk.Count > 0 ? " · " + Lang.F("поважає: {0} · не терпить: {1}", liked, hated) : ""), 15f, FootSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
            bool fresh = BondLog.AnyFresh(item) || People.AnyFresh(item);
            CardButton(c.Foot, Lang.T("Добре"), true, () => { ClosePopup(); if (fresh) Refresh(); });
            BondLog.Seen(item); // (opened: the seal stops breathing with the next draw)
            People.Seen(item);
            // the card first takes its width, then the lists their height (the words wrap only once the width is known)
            PlaceCard(c, by, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            Fixed(view.gameObject, -1f, Mathf.Min(KinViewMax, LayoutUtility.GetPreferredHeight(list)));
            PlaceCard(c, by, true);
        }

        private static void TallyFolk(List<People.Rec> folk, out int liked, out int hated)
        {
            liked = hated = 0;
            foreach (People.Rec r in folk)
            {
                if (!r.Shown) continue;
                People.State s = People.StateOf(r.Score);
                if (s >= People.State.Respect) liked++;
                else if (s <= People.State.Dislike) hated++;
            }
        }

        // a player it knows: a seal with their first letter, their name and manner, how it takes to them, when last near
        private static void FolkRow(RectTransform paper, ItemDrop.ItemData item, bool m, People.Rec r)
        {
            RectTransform row = Rect("Folk " + r.Name, paper);
            HBox(row, 12f, TextAnchor.MiddleLeft);
            RectTransform badge = Rect("Badge", row);
            Fixed(badge.gameObject, 46f, 46f);
            Img(badge, "Ring", BookArt.Circle, Ink);
            Image face = Img(badge, "Face", BookArt.Circle, C("#d8c497"));
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            TMP_Text letter = T(badge, r.Name.Length > 0 ? r.Name.Substring(0, 1).ToUpperInvariant() : "?", 20f, Ink, F.Head, TextAlignmentOptions.Center);
            Stretch(letter.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            foreach (Image i in badge.GetComponentsInChildren<Image>(true)) i.raycastTarget = false;
            RectTransform words = Rect("Words", row);
            VBox(words, 1f, TextAnchor.MiddleLeft);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            string style = People.StyleName(r.Style);
            T(words, "<b>" + r.Name + "</b>" + (style.Length > 0 ? " · " + style : "") + (r.Fresh ? " <color=" + GoldInkHex + "><b>" + Lang.T("нове") + "</b></color>" : ""), 16f, PInk, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
            string hex = "#6b5636", state;
            if (!r.Shown) state = Lang.T("ще придивляється");
            else
            {
                People.State s = People.StateOf(r.Score);
                state = People.StateWord(s);
                hex = s >= People.State.Respect ? PGoodHex : s == People.State.Dislike ? "#9a4a16" : s == People.State.Hate ? "#8E1E1E" : hex;
            }
            T(words, Lang.F("<color={0}>{1}</color> · поруч {2}", hex, state, BondLog.When(r.Last)), 14f, PSoft, F.Italic).textWrappingMode = TextWrappingModes.NoWrap;
            ItemDrop.ItemData it = item;
            People.Rec rec = r;
            Image back = row.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0f);
            Tip(row.gameObject, () => People.Story(it, m, rec));
        }

        // how strong a bond is to sort by (one not shown yet: last)
        private static float Strength(ItemDrop.ItemData item, BondLog.Rec r)
        {
            float s, st;
            return r.Shown && Bonds.Stored(item, r.Rid, out s, out st) ? Mathf.Abs(s) : -1f;
        }

        private static void KinRow(RectTransform paper, ItemDrop.ItemData item, bool m, BondLog.Rec r)
        {
            RectTransform row = Rect("Kin " + r.Nick, paper);
            HBox(row, 12f, TextAnchor.MiddleLeft);
            Image pic = Portrait(row, r.Munin, 34f);
            RectTransform words = Rect("Words", row);
            VBox(words, 1f, TextAnchor.MiddleLeft);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            T(words, Lang.F("<b>{0}</b> · {1} гравця {2}", r.Nick, Plugin.RavenName(r.Munin), r.Owner) + (r.Fresh ? " <color=" + GoldInkHex + "><b>" + Lang.T("нове") + "</b></color>" : ""), 16f, PInk, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
            string hex; string state = KinState(item, r, out hex);
            T(words, Lang.F("<color={0}>{1}</color> · бачились {2}", hex, state, BondLog.When(r.Last)), 14f, PSoft, F.Italic).textWrappingMode = TextWrappingModes.NoWrap;
            ItemDrop.ItemData it = item;
            BondLog.Rec rec = r;
            // the whole row answers the cursor (a see-through back): the story of the two
            Image back = row.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0f);
            pic.raycastTarget = false;
            Tip(row.gameObject, () => BondLog.Story(it, m, rec));
        }

        // the bond in words (and its ink), or that they are still looking each other over
        private static string KinState(ItemDrop.ItemData item, BondLog.Rec r, out string hex)
        {
            float s, st;
            hex = "#6b5636";
            if (!r.Shown || !Bonds.Stored(item, r.Rid, out s, out st)) return Lang.T("ще придивляються одне до одного");
            switch (Bonds.StateOf(s))
            {
                case Bonds.State.Close: hex = PGoodHex; return Lang.T("нерозлийвода");
                case Bonds.State.Pals: hex = PGoodHex; return Lang.T("друзі");
                case Bonds.State.Wary: hex = "#9a4a16"; return Lang.T("насторожі");
                case Bonds.State.Enemy: hex = "#8E1E1E"; return Lang.T("вороги");
                default: return Lang.T("знайомі");
            }
        }
    }
}
#endif
