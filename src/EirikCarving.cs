#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // Eirik's last carving in the Hall (story-lore.md «Пролог», step 8): E on it — read on the game's rune-stone page when
    // it can be read, else a line why not; either way the story hears «use eirik_carving» (the prologue's stage waits on
    // it). Readable only by the Watch's own fire (act 1; the user 2026-11-02: «текст пливе перед очима» till then).
    internal sealed class EirikCarving : MonoBehaviour, Hoverable, Interactable
    {
        internal const string Event = "eirik_carving";
        private ZNetView _nv;

        private void Awake() { _nv = GetComponent<ZNetView>(); }

        public string GetHoverName() { return Lang.T("Остання різьба Ейріка"); }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            string t = Lang.T("Остання різьба Ейріка") + "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Роздивитися");
            return Localization.instance != null ? Localization.instance.Localize(t) : t;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (hold || p == null || _nv == null || !_nv.IsValid()) return false;
            string why = WhyNot(p);
            if (why != null) p.Message(MessageHud.MessageType.Center, Lang.T(why));
            else if (TextViewer.instance != null) TextViewer.instance.ShowText(TextViewer.Style.Rune, Lang.T("Остання різьба Ейріка"), Lang.T(Words()), true);
            StoryBus.Raise("use", Event, why == null ? 1 : 0, transform.position); // (the story hears it read — value 1 — or only looked at)
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        // why it cannot be read now (null — it can)
        private static string WhyNot(Player p)
        {
            if (HallHearth.Level < HallHearth.Watch) return "Текст пливе перед очима — нічого не розібрати."; // (only by the Watch's own fire: act 1)
            return null;
        }

        // the words: a draft (story-lore.md); the chopped line is Heid's, its words come later
        private static string Words()
        {
            return "Я Ейрік. Я лишився сам. Я ще пам'ятаю всіх.\nУ відрізаній гілці воля не вертається. Коли повернеться сік, встануть ті, чиї імена тут.\nВони не згадають нас. Не кажіть їм, ким вони були.\n\n(рядок свіжо зрубано)\n\nБережіть для них вогонь. Несіть його туди, де вони житимуть.";
        }
    }
}
#endif
