using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // Our ravens' words look their own: Hugin gets a golden title and warm text,
    // Munin a crimson title on a darker bubble. Vanilla raven hints stay untouched.
    [HarmonyPatch(typeof(Chat), "SetNpcText")]
    internal static class Style
    {
        internal const string HuginTitle = "#FFC94A";
        internal const string HuginText = "#FFE6BF";
        internal const string MuninTitle = "#C8102E";
        private const float MuninShade = 0.55f;

        private static readonly MethodInfo FindNpcText = AccessTools.Method(typeof(Chat), "FindNpcText");

        internal static bool HasNpcText(GameObject talker)
        {
            if (RavenSpeech.Enabled) return RavenSpeech.Has(talker); // our own cards (RavenSpeech)
            if (FindNpcText == null || Chat.instance == null) return true; // cannot tell: leave it alone
            return FindNpcText.Invoke(Chat.instance, new object[] { talker }) != null;
        }

        private static bool Prefix(GameObject talker, Vector3 offset, float cullDistance, float ttl, ref string topic, ref string text, out int __state)
        {
            __state = 0;
            bool munin;
            if (!Ours(talker, out munin)) return true;
            if (RavenSpeech.Enabled) // a card of our own instead of the game's bubble; another player's raven only over it
            {
#if RAVEN_ITEM
                // the Guide speaks through a raven of the game, which gives no title: the card names him, not «Хугін»
                Raven guide = talker.GetComponent<Raven>();
                Raven.RavenText cur = guide != null ? Plugin.CurrentText(guide) : null;
                if (cur != null && Guide.IsGuide(cur.m_key)) topic = Lang.T(Guide.Name);
#endif
                RavenSpeech.Show(talker, offset, cullDistance, ttl, munin, topic, text, talker.GetComponent<Raven>() != null);
                return false;
            }
            __state = munin ? 2 : 1;
            string title = munin ? MuninTitle : HuginTitle;
            if (!string.IsNullOrEmpty(topic)) topic = "<color=" + title + ">" + topic + "</color>";
            if (string.IsNullOrEmpty(text)) return true;
            text = text.Replace("<color=orange>", "<color=" + title + ">");
            if (!munin) text = "<color=" + HuginText + ">" + text + "</color>";
            return true;
        }

        private static void Postfix(Chat __instance, GameObject talker, int __state)
        {
            if (__state != 2 || FindNpcText == null) return;
            try
            {
                Chat.NpcText npc = FindNpcText.Invoke(__instance, new object[] { talker }) as Chat.NpcText;
                if (npc == null || npc.m_gui == null) return;
                foreach (Image img in npc.m_gui.GetComponentsInChildren<Image>(true))
                {
                    Color c = img.color;
                    img.color = new Color(c.r * MuninShade, c.g * MuninShade, c.b * MuninShade, Mathf.Min(1f, c.a + 0.1f));
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Munin bubble style failed: " + e.Message); }
        }

        private static bool Ours(GameObject talker, out bool munin)
        {
            munin = false;
            if (talker == null) return false;
            Raven raven = talker.GetComponent<Raven>();
            if (raven != null)
            {
                Raven.RavenText cur = Plugin.CurrentText(raven);
                if (cur == null || cur.m_key == null || !cur.m_key.StartsWith(Plugin.KeyPrefix)) return false;
                munin = raven.m_isMunin;
                return true;
            }
            return Shared.IsCopy(talker, out munin);
        }
    }
}
