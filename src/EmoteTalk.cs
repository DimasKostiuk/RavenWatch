#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The ravens about your emotes: you start a dance (a wave, a bow, a cry...) and now and then one of them has a word
    // about it a moment later — from the air, if the emote sent them up off the shoulder (a dance, lying down). Each kind
    // keeps its own pause once a word was said, and nothing comes while another raven speaks, in a fight, or for the
    // hero's own gestures on a gift (RavenSkills.Animate). The game counts every emote started (s_emoteID), so a wave
    // after a wave is seen too.
    internal static class EmoteTalk
    {
        internal static bool Enabled = true;
        internal static float Chance = 0.35f;
        internal static float KindPauseMinutes = 4f;  // the same kind of emote: a word at most this often
        private const float AnyPause = 45f;           // any emote word: at most this often
        internal static float OursUntil;              // a gift's gesture started by the mod: not your doing

        // the game's emotes (and the ones of mods that use the same names) -> the words for them; sit: the fireside
        // stories (Cozy) are about that already; anything else: the words for any odd doing
        private static readonly Dictionary<string, string> Kinds = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "dance", "dance" }, { "vibe", "dance" }, { "headbang", "headbang" }, { "wave", "wave" }, { "comehere", "comehere" },
            { "point", "point" }, { "challenge", "roar" }, { "roar", "roar" }, { "cheer", "cheer" }, { "thumbsup", "cheer" },
            { "nonono", "nonono" }, { "blowkiss", "kiss" }, { "loveyou", "kiss" }, { "bow", "bow" }, { "kneel", "bow" },
            { "cower", "cower" }, { "cry", "cry" }, { "despair", "cry" }, { "flex", "flex" }, { "laugh", "laugh" },
            { "shrug", "shrug" }, { "relax", "rest" }, { "rest", "rest" }, { "toast", "toast" }, { "count", "count" },
            { "sit", null },
        };

        private static Player _for;
        private static ZNetView _nv; // the player's, found once
        private static int _id;
        private static float _sayAt = -1f, _lastAny = -999f;
        private static string _kind;
        private static readonly Dictionary<string, float> KindAt = new Dictionary<string, float>();

        // from the twice-a-second sync (RavenSkills)
        internal static void Tick(Player p)
        {
            if (p == null) return;
            if (p != _for || _nv == null) { _for = p; _nv = p.GetComponent<ZNetView>(); _id = -1; _sayAt = -1f; }
            ZDO zdo = _nv != null && _nv.IsValid() ? _nv.GetZDO() : null;
            if (zdo == null) return;
            int id = zdo.GetInt(ZDOVars.s_emoteID, 0);
            if (_id < 0) { _id = id; return; } // just came into the world: what was before is not news
            if (id != _id)
            {
                _id = id;
                string e = zdo.GetString(ZDOVars.s_emote, "");
                if (e.Length > 0 && Time.time >= OursUntil) Started(e);
            }
            if (_sayAt > 0f && Time.time >= _sayAt) { _sayAt = -1f; Say(p, _kind); }
        }

        private static void Started(string emote)
        {
            if (!Enabled || _sayAt > 0f) return;
            string kind;
            if (!Kinds.TryGetValue(emote, out kind)) kind = "any";
            if (kind == null) return;
            float now = Time.time, last;
            if (now - _lastAny < AnyPause) return;
            if (KindAt.TryGetValue(kind, out last) && now - last < KindPauseMinutes * 60f) return;
            if (UnityEngine.Random.value >= Chance) return;
            _kind = kind;
            _sayAt = now + UnityEngine.Random.Range(1.2f, 2.5f); // a moment to see it (and to fly up, for a dance)
        }

        private static void Say(Player p, string kind)
        {
            if (Plugin.Instance == null || p.IsDead() || p.IsSleeping()) return;
            if (Senses.Ready && Senses.Fight) return;
            if (Cozy.Quieted || RavenSpeech.Has(p.gameObject)) return; // someone is talking: the moment has passed
            bool h = Free(p, false), m = Free(p, true);
            if (!h && !m) return;
            bool munin = h && m ? UnityEngine.Random.value < 0.55f : m;
            string tag = "emote_" + kind;
            if (!Patterns.Has(Plugin.Instance.PhraseDir, munin, tag)) tag = "emote_any";
            string line = Plugin.Instance.ComposeTagged(p, munin, tag);
            if (string.IsNullOrEmpty(line)) return;
            RavenSkills.SayFromShoulder(p, munin, line);
            Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line)); // the other chatter waits for it
            _lastAny = Time.time;
            KindAt[kind] = Time.time;
        }

        // the console (raven emote [kind]): a word about that kind now, no dice, no pause
        internal static string Force(Player p, string kind)
        {
            if (p == null) return Lang.T("Нема гравця.");
            if (string.IsNullOrEmpty(kind)) kind = "dance";
            string k; if (Kinds.TryGetValue(kind, out k) && k != null) kind = k;
            if (!Free(p, false) && !Free(p, true)) return Lang.T("Немає вільного ворона на плечі.");
            _kind = kind; _sayAt = Time.time + 0.3f;
            return Lang.F("Слово про емоцію: {0}. Види: dance, headbang, wave, comehere, point, roar, cheer, nonono, kiss, bow, cower, cry, flex, laugh, shrug, rest, toast, count, any.", kind);
        }

        // a raven of yours that is here, awake, and yours (another's raven keeps its venom for other things)
        private static bool Free(Player p, bool munin)
        {
            ItemDrop.ItemData r = RavenSeat.On(p, munin);
            return r != null && Trust.Active(r) && !Trust.Stranger(r, p) && !Trust.Away(r);
        }
    }
}
#endif
