#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What a person calls out in a fight (the user 2026-11-15, of the dwarf Lit beset at the forest's edge: «під час бою він вигукує щось у своїй манері»).
    // Its template's lines, a line a variant (translated as its greetings are: tools/l10n.js):
    //   cry: Тримай лінію! Рівно!
    // While it fights (its mind's Fight layer, attacking), where it is run, one of them now and then: the first CryFirstMin…CryFirstMax s into the fight,
    // then every CryMin…CryMax s — over its head as any line of its (NpcActor.SayLine: kept quiet in another's scene, StoryAside). Nothing kept: a fight
    // that ends forgets its clock.
    internal static class NpcCry
    {
        internal static float CryFirstMin = 1f, CryFirstMax = 3f, CryMin = 6f, CryMax = 11f;
        private static readonly Dictionary<NpcActor, float> _next = new Dictionary<NpcActor, float>(); // session: when it calls out next, while it fights

        internal static void Tick(NpcActor a)
        {
            NpcDef d = a.Def;
            if (d == null || d.Cry.Count == 0) return;
            NpcMind m = a.Mind;
            bool fights = m != null && m.NowLayer == NpcMind.Layer.Fight && m.Now == NpcMind.Task.Attack;
            float at;
            if (!fights) { _next.Remove(a); return; }
            if (!_next.TryGetValue(a, out at)) { _next[a] = Time.time + Random.Range(CryFirstMin, CryFirstMax); return; }
            if (Time.time < at) return;
            _next[a] = Time.time + Random.Range(CryMin, CryMax);
            a.Say(d.Cry[Random.Range(0, d.Cry.Count)], null, NpcActor.Voice.Over);
        }

        internal static void Forget(NpcActor a) { _next.Remove(a); }
    }
}
#endif
