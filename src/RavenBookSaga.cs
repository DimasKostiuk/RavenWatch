#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace HuginWhispers
{
    // «Сага»: the story's progress (the user 2026-11-05; the look is to be tuned later). The left page — the world: what happened
    // here for everyone, chapter by chapter, with the names of the players who saw it; what this player neither saw nor was told
    // stays a rumour (a person telling it lifts it), and a row of notches says how much of the chapter has happened. The right
    // page — his own: what to do now (the goal line, «Показати на мапі») and what he has passed, in the order lived. Everything
    // comes from StorySaga; nothing is put together here. Seen once drawn (the tab's dot, StorySaga.Seen).
    internal static partial class RavenBook
    {
        private static void PageSaga()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            List<StorySaga.Page> pages = StorySaga.Pages(p);
            SagaWorld(pages);
            SagaOwn(p, pages);
            StorySaga.Seen(p);
        }

        // ---- left: the world
        private static void SagaWorld(List<StorySaga.Page> pages)
        {
            RectTransform left = Column("World", ColumnX(true), ColumnTop, ColumnW, 8f);
            for (int i = 0; i < pages.Count; i++)
            {
                StorySaga.Page pg = pages[i];
                if (i > 0) { RectTransform gap = Rect("Gap", left); Fixed(gap.gameObject, -1f, 14f); }
                T(left, Lang.T(pg.Chapter.Name), 26f, Ink, F.Head);
                Divider(left);
                if (pg.World.Count == 0) T(left, Lang.T("У світі ще нічого не сталося."), 16f, InkSoft, F.Italic);
                foreach (StoryTales.Told t in pg.World)
                {
                    if (t.Known)
                    {
                        T(left, Lang.T(t.Saga), 17f, Ink, F.Body);
                        string who = Witnesses(t);
                        if (who.Length > 0) T(left, Lang.F("Свідки: {0}", who), 14f, Caption, F.Italic);
                    }
                    else
                    {
                        T(left, Lang.T(t.Rumor.Length > 0 ? t.Rumor : "Кажуть, у світі щось сталося."), 16f, InkSoft, F.Italic);
                        TMP_Text hint = T(left, Lang.T("Чутка"), 14f, GoldInk, F.Body);
                        Tip(hint.gameObject, () => Lang.T("Ти цього не бачи{в|ла}. Поговори з людьми: хтось, може, бачив сам і розповість."));
                    }
                    RectTransform after = Rect("Gap", left); Fixed(after.gameObject, -1f, 4f);
                }
                Notches(left, pg);
            }
        }

        // the chapter's moments of the world, as «Золотий шлях» draws its steps (◆ happened, ◇ not yet: only these two are in
        // the book's fonts — ▮▯ came out as squares); how many, under the cursor (a figure beside «з» reads as «3 3»)
        private static void Notches(RectTransform col, StorySaga.Page pg)
        {
            if (pg.Of == 0) return;
            RectTransform row = Rect("Notches", col);
            HBox(row, 6f, TextAnchor.MiddleLeft);
            for (int i = 0; i < pg.Of; i++)
            {
                TMP_Text n = T(row, i < pg.Happened ? "◆" : "◇", 22f, i < pg.Happened ? GoldInk : A(Ink, 0.35f), F.Body);
                n.textWrappingMode = TextWrappingModes.NoWrap;
                Tip(n.gameObject, () => Lang.F("<b>{0}</b>\nУ світі сталося {1} з {2} подій цього розділу.", Lang.T(pg.Chapter.Name), pg.Happened, pg.Of));
            }
            if (pg.Closed) { TMP_Text c = T(row, Lang.T("розділ закінчено"), 14f, Caption, F.Italic); c.textWrappingMode = TextWrappingModes.NoWrap; }
            Spring(row);
        }

        // «ти, Оля, Торвін»: you first; the world keeps names in lower case (its keys)
        private static string Witnesses(StoryTales.Told t)
        {
            List<string> l = new List<string>();
            if (t.You) l.Add(Lang.T("ти"));
            foreach (string w in t.Witnesses) l.Add(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(w));
            return l.Count > 0 ? Few(l) : "";
        }

        // ---- right: your own way
        private static void SagaOwn(Player p, List<StorySaga.Page> pages)
        {
            RectTransform right = Column("Own", ColumnX(false), ColumnTop, ColumnW, 8f);
            T(right, Lang.T("Зараз"), 26f, Ink, F.Head);
            Divider(right);
            StoryQuests.QuestDef q; StoryQuests.StageDef s;
            if (StoryQuests.Now(p, out q, out s))
            {
                T(right, Lang.T(q.Name), 18f, Brown, F.Head);
                T(right, StoryQuests.GoalText(s), 17f, Ink, F.Body);
                Vector3 at;
                if (StoryQuests.MarkAt(q, s, out at))
                {
                    TMP_Text map = T(right, "<u>" + Lang.T("Показати на мапі") + "</u>", 15f, GoldInk, F.Body);
                    Clickable(map.gameObject, () => { Close(); if (Minimap.instance != null) Minimap.instance.ShowPointOnMap(at); });
                    Tip(map.gameObject, () => Lang.T("Закриє книгу й відкриє мапу там, де золота мітка."));
                }
            }
            else T(right, Lang.T("Зараз сюжет нічого від тебе не чекає."), 16f, InkSoft, F.Italic);

            RectTransform gap = Rect("Gap", right); Fixed(gap.gameObject, -1f, 12f);
            T(right, Lang.T("Мій шлях"), 22f, Ink, F.Head);
            Divider(right);
            bool any = false;
            foreach (StorySaga.Page pg in pages)
            {
                if (pg.Own.Count == 0) continue;
                if (pages.Count > 1) T(right, Lang.T(pg.Chapter.Name), 15f, Caption, F.Head);
                foreach (string e in pg.Own) T(right, Lang.T(e), 17f, Ink, F.Body);
                any = true;
            }
            if (!any) T(right, Lang.T("Ще нічого не записано."), 16f, InkSoft, F.Italic);
        }
    }
}
#endif
