#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Світ», what the ravens on your shoulder notice and have a word about (the user, 2026-10-15):
    //   - the game's own raven (the tutorial's Hugin or Munin, with its «!»), not one of ours: your raven is jealous of it
    //     — «that one from Asgard is no kin of mine, don't listen to it»;
    //   - Odin himself (the old wanderer by the fire, who vanishes as you come): they fall silent and bow, and then one
    //     of them says a word.
    // Once each (a raven: this session; Odin: each time he shows up), through the dialogue director with an event's
    // right. The game's ravens and Odin are kept as they come and go (Harmony on their Awake); looked over once a second.
    internal static class WorldNotice
    {
        internal static float RivalNear = 20f, OdinNear = 30f;
        internal static readonly List<Raven> Ravens = new List<Raven>();
        internal static readonly List<Odin> Odins = new List<Odin>();
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static float _next;

        // From WorldVisit.Schedule (the owner's game, twice a second)
        internal static void Tick(Player p)
        {
            if (p == null || Time.time < _next) return;
            _next = Time.time + 1f;
            Ravens.RemoveAll(r => r == null);
            Odins.RemoveAll(o => o == null);
            Vector3 me = p.transform.position;
            foreach (Odin o in Odins)
            {
                if (!o.gameObject.activeInHierarchy || (o.transform.position - me).sqrMagnitude > OdinNear * OdinNear || !Seen.Add(o.GetInstanceID())) continue;
                Say(p, "world_odin", true);
                return;
            }
            foreach (Raven r in Ravens)
            {
                if (!r.gameObject.activeInHierarchy || (r.transform.position - me).sqrMagnitude > RivalNear * RivalNear) continue;
                Raven.RavenText cur = Plugin.CurrentText(r);
                if (cur == null || (cur.m_key != null && cur.m_key.StartsWith(Plugin.KeyPrefix)) || !Seen.Add(r.GetInstanceID())) continue; // (out with a word of the game's own, not one of ours)
                Say(p, "world_rival", false);
                return;
            }
        }

        // a word from your shoulder: Hugin first (Odin's: either), if he sits there
        private static void Say(Player p, string tag, bool either)
        {
            bool h = Shoulder.ModelOf(p, false) != null, m = Shoulder.ModelOf(p, true) != null;
            if (!h && !m) return;
            bool munin = h && m ? (either && Random.value < 0.5f) : m;
            if (!Director.May(Director.Voice.Event)) return;
            string line = Satiety.Say(p, munin, tag);
            if (string.IsNullOrEmpty(line)) return;
            RavenSkills.SayFromShoulder(p, munin, line);
            Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line));
        }
    }

    [HarmonyPatch(typeof(Raven), "Awake")]
    internal static class WorldNoticeRavenPatch
    {
        private static void Postfix(Raven __instance) { if (__instance != null && !WorldNotice.Ravens.Contains(__instance)) WorldNotice.Ravens.Add(__instance); }
    }

    [HarmonyPatch(typeof(Odin), "Awake")]
    internal static class WorldNoticeOdinPatch
    {
        private static void Postfix(Odin __instance) { if (__instance != null && !WorldNotice.Odins.Contains(__instance)) WorldNotice.Odins.Add(__instance); }
    }
}
#endif
