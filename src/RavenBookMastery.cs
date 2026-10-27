#if RAVEN_ITEM
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuginWhispers
{
    internal static partial class RavenBook
    {
        // ---------------------------------------------------------------- «Майстерність»
        // The training's seal is the way in: a milestone reached and not yet used — it breathes and sends out gold
        // rings, and a click opens the card with that milestone's four techniques (pick one, then «Вивчити»).
        // Nothing waiting: the click shows what this raven has learned and when the next comes. Under the seal one line
        // names the techniques learned so far.

        // the seal's tooltip: short — a technique waits, or when the next one comes
        private static string SealTip(bool m, ItemDrop.ItemData item)
        {
            int level = Strike.Level(item);
            List<int> wait = Mastery.Pending(item);
            if (wait.Count > 0 && !Trust.Stranger(item, Player.m_localPlayer))
                return Lang.F("<b>Вишкіл {0} · новий прийом</b>\nКлікни — обери один із чотирьох.", wait[0]);
            int next = NextMilestone(level);
            string head = Lang.F("<b>Вишкіл {0} із {1}</b>", level, Strike.MaxLevel);
            if (next < 0) return head + "\n" + Lang.T("Усі прийоми вивчено. Клік — переглянути.");
            int left = Mathf.Max(0, Strike.FlightsFor(next) - Strike.Flights(item));
            return head + "\n" + Lang.F("Наступний прийом — на {0}-му: ще {1}.", next, Lang.N(left, "влучний виліт|влучні вильоти|влучних вильотів")) + "\n" + Lang.T("Клік — прийоми майстерності.");
        }

        private static int NextMilestone(int level) { foreach (int at in Mastery.Milestones) if (at > level) return at; return -1; }

        // under the seal: the techniques learned (a click opens the card too)
        private static void MasteryLine(Transform col, RectTransform seal, bool m, ItemDrop.ItemData item)
        {
            List<string> names = new List<string>();
            foreach (int at in Mastery.Milestones) { Mastery.Tech t = Mastery.PickedAt(item, at); if (t != null) names.Add(Lang.T(t.Name)); }
            if (names.Count == 0) return;
            TMP_Text line = T(col, Lang.F("<color={0}><b>Прийоми</b></color>: {1}", GoldInkHex, string.Join(" · ", names.ToArray())), 14f, InkSoft, F.Body);
            Clickable(line.gameObject, () => MasteryAsk(seal, m, item, null));
            Tip(line.gameObject, () => Lang.T("Клік — прийоми майстерності."));
        }

        // the seal breathes while a pick waits
        private static void PulseSeal(RectTransform seal)
        {
            // its own canvas: the breath changes colours and sizes every frame, and on the book's canvas that would have
            // the whole page rebuilt each frame; on its own only these few pieces are. Only the picture goes there: a
            // nested canvas loses every click to the page under it (Unity sorts hits of one root canvas by depth, and
            // the nested one counts its depth from zero), so the click and the tip stay on the book's canvas, on «Hit».
            RectTransform pulse = Rect("Pulse", seal);
            pulse.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(pulse, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pulse.gameObject.AddComponent<Canvas>();
            Image halo = Img(pulse, "Halo", BookArt.SoftDisc, A(Gold, 0f));
            Stretch(halo.rectTransform, Vector2.zero, Vector2.one, new Vector2(-18f, -18f), new Vector2(18f, 18f));
            Image ring2 = Img(pulse, "Ripple2", BookArt.Ring(0.1f), A(Gold, 0f));
            Stretch(ring2.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image ring = Img(pulse, "Ripple", BookArt.Ring(0.14f), A(Gold, 0f));
            Stretch(ring.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Transform face = seal.Find("Face");
            if (face != null) face.SetParent(pulse, false); // (over the rings)

            Image hit = Img(seal, "Hit", null, new Color(0f, 0f, 0f, 0f));
            hit.raycastTarget = true;
            hit.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(hit.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Button was = face != null ? face.GetComponent<Button>() : null;
            if (was != null)
            {
                Button b = hit.gameObject.AddComponent<Button>();
                b.transition = was.transition; b.colors = was.colors; b.targetGraphic = was.targetGraphic; // (the face still lights up under the pointer)
                b.interactable = was.interactable;
                Navigation n = b.navigation; n.mode = Navigation.Mode.None; b.navigation = n;
                // the face's button stays (off): its listeners hold it — Tint's «unselect me» was the first of them, and
                // with the button destroyed it threw, so the card never opened
                was.enabled = false;
                GameObject hg = hit.gameObject;
                b.onClick.AddListener(() => { Unselect(hg); was.onClick.Invoke(); });
            }

            SealPulse p = seal.gameObject.AddComponent<SealPulse>();
            p.Ring = ring; p.Ring2 = ring2; p.Halo = halo; p.Face = face;
            p.FaceImg = face != null ? face.GetComponent<Image>() : null;
            if (p.FaceImg != null) p.FaceColor = p.FaceImg.color;
        }

        // where the seal takes the pointer: «Hit» while it breathes, else its face
        private static GameObject SealHit(RectTransform seal)
        {
            Transform hit = seal.Find("Hit");
            return hit != null ? hit.gameObject : seal.Find("Face").gameObject;
        }

        // The card. pick: the technique chosen on it so far (null: none yet). Nothing waiting: the raven's list.
        private static void MasteryAsk(RectTransform by, bool m, ItemDrop.ItemData item, string pick)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            if (_root == null || p == null || item == null) return;
            List<int> wait = Mastery.Pending(item);
            bool own = !Trust.Stranger(item, p);
            int at = own && wait.Count > 0 ? wait[0] : -1;
            Card card = OpenCard("MasteryAsk", Lang.T("Майстерність"), 520f, at > 0);
            RectTransform panel = card.Paper;
            panel.GetComponent<VerticalLayoutGroup>().spacing = 8f;

            if (at > 0)
            {
                T(panel, Lang.F("Вишкіл {0}: {1} вивчає прийом", at, Plugin.RavenName(m)), 21f, PInk, F.Head);
                T(panel, Lang.T("Один із чотирьох — назавжди. Кожен під свій стиль бою."), 15f, PSoft, F.Italic);
                foreach (Mastery.Tech t in Mastery.All)
                {
                    if (t.Munin != m || t.At != at) continue;
                    string id = t.Id;
                    RectTransform r = TechChoice(panel, t, pick == id, true);
                    Clickable(r.gameObject, () => MasteryAsk(by, m, item, id));
                    if (pick != id) ChoiceGlow.On(r, PGoldLine); // (under the pointer: lit, its edge in gold)
                }
                T(card.FootWords, pick == null ? Lang.T("Обери прийом.") : Lang.T("Решта три на цьому рівні закриються."), 15f, FootSoft, F.Italic);
                AskButton(card.Foot, Lang.T("Пізніше"), false, ClosePopup);
                Mastery.Tech chosen = pick != null ? Mastery.Find(pick) : null;
                System.Action yes = () =>
                {
                    ClosePopup();
                    string why = Mastery.Pick(Player.m_localPlayer, item, pick);
                    Tell(why ?? Lang.F("{0} вивчив прийом «{1}».", Plugin.RavenName(m), Lang.T(chosen.Name)));
                    if (why == null) { RavenFx.Play("sfx_levelup", p.transform.position, Quaternion.identity); }
                    Show(_tab, true);
                };
                CardButton(card.Foot, Lang.T("Вивчити"), true, () => { if (chosen != null) yes(); }, "ᚦ", chosen != null);
                if (chosen != null) _nickYes = yes;
            }
            else
            {
                // what this raven knows, milestone by milestone
                T(panel, Lang.F("{0}: прийоми майстерності", Plugin.RavenName(m)), 21f, PInk, F.Head);
                int level = Strike.Level(item);
                foreach (int a in Mastery.Milestones)
                {
                    Mastery.Tech t = Mastery.PickedAt(item, a);
                    if (t != null) { TechChoice(panel, t, true, true, a); continue; }
                    RectTransform r = PaperChoice(panel, false, false);
                    HBox(r, 10f, TextAnchor.MiddleLeft, 12, 12, 8, 8);
                    T(r, a.ToString(), 20f, PSoft, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
                    T(r, level >= a ? Lang.T("ще не обрано") : Lang.F("відкриється на {0}-му рівні вишколу (зараз {1})", a, level), 15f, PSoft, F.Italic).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                }
                T(panel, Lang.T("На вишколі 10, 20, 25 і 30 — по прийому на вибір, назавжди."), 14f, PSoft, F.Italic);
            }
            PlaceCard(card, by);
        }

        // one technique on the card: its name and style on top, what it does under
        private static RectTransform TechChoice(Transform panel, Mastery.Tech t, bool here, bool can, int at = 0)
        {
            RectTransform r = PaperChoice(panel, here, can);
            VBox(r, 2f, TextAnchor.UpperLeft, 12, 12, 8, 9);
            RectTransform head = Rect("Head", r);
            HBox(head, 8f, TextAnchor.MiddleLeft);
            if (at > 0) T(head, at.ToString(), 17f, here ? PlaqueInk : PSoft, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            TMP_Text n = T(head, Lang.T(t.Name), 18f, here ? PlaqueInk : PInk, F.Head);
            n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            T(head, Mastery.WayName(t.Way), 14f, here ? FootSoft : PLabel, F.Italic, TextAlignmentOptions.MidlineRight).textWrappingMode = TextWrappingModes.NoWrap;
            T(r, t.Text(), 14f, here ? FootInk : PSoft, F.Body);
            return r;
        }
    }

    // a choice card under the pointer: its paper brighter and warmer, its edge in gold, eased in and out
    internal sealed class ChoiceGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image _fill, _line;
        private Color _fill0, _line0, _gold;
        private float _w, _want;
        internal static void On(RectTransform r, Color gold)
        {
            ChoiceGlow g = r.gameObject.AddComponent<ChoiceGlow>();
            g._fill = r.GetComponent<Image>();
            Transform l = r.Find("Line");
            g._line = l != null ? l.GetComponent<Image>() : null;
            g._fill0 = g._fill != null ? g._fill.color : Color.clear;
            g._line0 = g._line != null ? g._line.color : Color.clear;
            g._gold = gold;
        }
        public void OnPointerEnter(PointerEventData e) { _want = 1f; }
        public void OnPointerExit(PointerEventData e) { _want = 0f; }
        private void OnDisable() { _want = 0f; _w = 0f; Paint(); }
        private void Update()
        {
            if (Mathf.Approximately(_w, _want)) return;
            _w = Mathf.MoveTowards(_w, _want, Time.unscaledDeltaTime * 7f);
            Paint();
        }
        private void Paint()
        {
            float k = _w * _w * (3f - 2f * _w);
            if (_fill != null) _fill.color = Color.Lerp(_fill0, new Color(1f, 0.93f, 0.74f, 0.75f), k);
            if (_line != null) _line.color = Color.Lerp(_line0, new Color(_gold.r * 1.25f, _gold.g * 1.2f, _gold.b, 1f), k);
        }
    }

    // the waiting seal: it swells a little and back, a soft gold halo comes and goes, a gold ring rolls out of it
    internal sealed class SealPulse : MonoBehaviour
    {
        internal Image Ring, Ring2, Halo, FaceImg;
        internal Transform Face;
        internal Color FaceColor = Color.white;
        private static readonly Color Gold = BookArt.Gold;
        private static readonly Color Bright = new Color(1f, 0.86f, 0.42f); // a warm, lit gold: the pulse is meant to be seen

        private void Update()
        {
            float t = Time.unscaledTime;
            float breath = 0.5f + 0.5f * Mathf.Sin(t * 3.6f);
            if (Face != null) Face.localScale = Vector3.one * (1f + 0.08f * breath);
            if (FaceImg != null) FaceImg.color = Color.Lerp(FaceColor, Bright, 0.35f * breath); // (the seal itself lights up)
            if (Halo != null) Halo.color = new Color(Bright.r, Bright.g, Bright.b, 0.35f + 0.45f * breath);
            Wave(Ring, t / 1.4f);
            Wave(Ring2, t / 1.4f + 0.5f); // (a second ring half a beat behind)
        }
        private static void Wave(Image ring, float x)
        {
            if (ring == null) return;
            float k = Mathf.Repeat(x, 1f); // from the seal's edge outwards
            ring.rectTransform.localScale = Vector3.one * (1f + 0.45f * k);
            ring.color = new Color(Bright.r, Bright.g, Bright.b, 1f * (1f - k) * (1f - k));
        }
    }
}
#endif
