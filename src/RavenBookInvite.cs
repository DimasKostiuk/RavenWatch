#if RAVEN_ITEM
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The invitation to a game of the ravens (JointPlay): the post's card (RavenBookCard's pieces) on a canvas of its own,
    // over the game near the top — the book need not be open. Who calls, with which raven; a button for each raven of
    // yours (picture, name, and why not if it cannot) — a click picks it and accepts; «Відхилити», or Esc, declines; the
    // time left runs down at the bottom. While it is up the game treats it as a window (the cursor, no swing):
    // RavenBookAsWindowPatch / InviteNoSwingPatch.
    internal static partial class RavenBook
    {
        private static GameObject _inviteRoot;
        private static TMP_Text _inviteTime;
        internal static bool InviteOpen { get { return _inviteRoot != null && _inviteRoot.activeSelf; } }

        internal static void ShowInvite(string fromName, bool fromMunin, float seconds)
        {
            HideInvite();
            Player me = Player.m_localPlayer;
            if (me == null) return;
            _inviteRoot = new GameObject("RavenWatch_Invite", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(_inviteRoot);
            Canvas canvas = _inviteRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30002;
            CanvasScaler sc = _inviteRoot.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            _inviteRoot.AddComponent<GraphicRaycaster>();
            _inviteRoot.AddComponent<InviteKeys>();
            RectTransform root = (RectTransform)_inviteRoot.transform;

            RectTransform frame = Rect("Frame", root);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 1f);
            frame.pivot = new Vector2(0.5f, 1f);
            frame.anchoredPosition = new Vector2(0f, -150f);
            CardPiece(frame.gameObject.AddComponent<Image>(), "card_frame", 32f, 12f, C("#2a1b10"));
            VBox(frame, 0f, TextAnchor.UpperCenter, 16, 16, 36, 0);
            frame.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            frame.sizeDelta = new Vector2(560f, 0f);

            // the parchment: who calls, which raven; under it the ravens of yours to pick
            RectTransform paper = Rect("Paper", frame);
            CardPiece(paper.gameObject.AddComponent<Image>(), "card_paper", 24f, 10f, Paper);
            VBox(paper, 12f, TextAnchor.UpperCenter, 24, 24, 20, 18);
            RectTransform head = Rect("Head", paper);
            HBox(head, 14f, TextAnchor.MiddleLeft);
            RectTransform pic = Rect("Pic", head);
            Fixed(pic.gameObject, 64f, 64f);
            Img(pic, "Ring", BookArt.Circle, fromMunin ? C("#8E1E1E") : C("#2f4f7a"));
            Image face = Img(pic, "Face", AbilityIcons.For(fromMunin ? "munin" : "hugin", fromMunin), Color.white);
            face.preserveAspect = true;
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            TMP_Text say = T(head, Lang.F("<b>{0}</b> кличе твого ворона в догонялки — з {1}.", fromName, (fromMunin ? Lang.T("Муніном") : Lang.T("Хугіном"))), 19f, PInk, F.Body, TextAlignmentOptions.MidlineLeft);
            say.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            T(paper, Lang.T("Обери, хто з твоїх полетить грати:"), 16f, PSoft, F.Italic, TextAlignmentOptions.Center);
            RectTransform row = Rect("Ravens", paper);
            HBox(row, 14f, TextAnchor.MiddleCenter);
            foreach (bool m in new[] { false, true })
            {
                if (RavenSeat.On(me, m) == null) continue;
                bool munin = m;
                string why = JointPlay.WhyNot(me, munin);
                RectTransform b = PaperChoice(row, false, why == null);
                VBox(b, 4f, TextAnchor.MiddleCenter, 14, 14, 10, 10);
                Fixed(b.gameObject, 200f, -1f);
                RectTransform rp = Rect("Pic", b);
                Fixed(rp.gameObject, 52f, 52f);
                Image ri = Img(rp, "Face", AbilityIcons.For(munin ? "munin" : "hugin", munin), why == null ? Color.white : new Color(1f, 1f, 1f, 0.45f));
                ri.preserveAspect = true;
                Stretch(ri.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                T(b, "<b>" + Nicks.Name(me, munin) + "</b>", 18f, munin ? C("#8E1E1E") : C("#2f4f7a"), F.Body, TextAlignmentOptions.Center);
                T(b, why ?? Lang.T("грати ним"), 14f, why == null ? PSoft : C("#8E1E1E"), F.Italic, TextAlignmentOptions.Center);
                if (why == null)
                {
                    Button btn = b.gameObject.AddComponent<Button>();
                    btn.targetGraphic = b.GetComponent<Image>();
                    Tint(btn);
                    btn.onClick.AddListener(() => { string msg = JointPlay.Accept(munin); if (msg != null && Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, msg); });
                }
            }

            // the dark strip: the time left, and «Відхилити»
            RectTransform foot = Rect("Foot", frame);
            HBox(foot, 12f, TextAnchor.MiddleRight, 6, 0, 12, 14);
            RectTransform words = Rect("Words", foot);
            VBox(words, 0f, TextAnchor.MiddleLeft);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _inviteTime = T(words, "", 16f, FootSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
            CardButton(foot, Lang.T("Відхилити"), false, () => JointPlay.Decline("no"));

            Plaque(frame, Lang.T("Виклик"), 38f, false);
        }

        internal static void HideInvite()
        {
            if (_inviteRoot != null) UnityEngine.Object.Destroy(_inviteRoot);
            _inviteRoot = null; _inviteTime = null;
        }

        // the time left (and Esc: no), each frame while the card is up
        internal sealed class InviteKeys : MonoBehaviour
        {
            private int _shownSec = -1;
            private void Update()
            {
                string name; bool munin; float left;
                if (!JointPlay.Invited(out name, out munin, out left)) { HideInvite(); return; }
                int sec = Mathf.CeilToInt(left);
                if (_inviteTime != null && sec != _shownSec) { _shownSec = sec; _inviteTime.text = Lang.F("ще {0} с · Esc — відхилити", sec); }
                if (RavenKeys.Down(KeyCode.Escape)) JointPlay.Decline("no");
            }
        }
    }

    // While the invitation (or a stone's raven pick) is up a mouse click is ours: no swing, no block (as the orders menu)
    [HarmonyLib.HarmonyPatch(typeof(Player), "SetControls")]
    internal static class InviteNoSwingPatch
    {
        private static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold)
        {
            if (!(RavenBook.InviteOpen || RavenBook.PickOpen || RavenBook.OmenOpen) || __instance != Player.m_localPlayer) return;
            attack = attackHold = secondaryAttack = secondaryAttackHold = block = blockHold = false;
        }
    }
}
#endif
