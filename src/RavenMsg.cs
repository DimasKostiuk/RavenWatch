using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace HuginWhispers
{
    // The mod's messages in the middle of the screen stay longer than the game's own. The game shows a centre message
    // at full strength and starts fading it out over ~4 s right away; showing it again later made it jump back (a blink).
    // Instead the text is held at full strength, then faded out once, smoothly, unless something else took its place.
    internal static class RavenMsg
    {
        private const float GameSeconds = 4f;  // how long the game's own centre message lasts
        private const float FadeSeconds = 2f;
        private static readonly FieldInfo CenterText = AccessTools.Field(typeof(MessageHud), "m_messageCenterText");
        private static string _text;
        private static float _holdUntil;

        // extra: how many times longer than the game's own (each ~4 s more)
        internal static void Center(Character who, string text, int extra = 1)
        {
            if (who == null || string.IsNullOrEmpty(text)) return;
            text = Grammar.Fix(text, who as Player); // ({male|female} forms: this player's)
            who.Message(MessageHud.MessageType.Center, text);
            if (who != Player.m_localPlayer || extra <= 0) { _text = null; return; }
            _text = text;
            _holdUntil = Time.unscaledTime + GameSeconds * (extra + 1) - FadeSeconds;
        }

        // from the plugin's Update
        internal static void Tick()
        {
            if (_text == null || MessageHud.instance == null) return;
            TMP_Text shown = CenterText != null ? CenterText.GetValue(MessageHud.instance) as TMP_Text : null;
            if (shown == null) { _text = null; return; }
            if (shown.text != _text && (Localization.instance == null || shown.text != Localization.instance.Localize(_text))) { _text = null; return; } // another message is up now: leave it
            if (Time.unscaledTime < _holdUntil) { shown.CrossFadeAlpha(1f, 0f, true); return; } // over the game's own fade: still at full strength
            shown.CrossFadeAlpha(0f, FadeSeconds, true);
            _text = null;
        }
    }
}
