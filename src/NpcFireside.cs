#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // Two people talking as they sit by a fire (NpcChatter, a talk «fire»): when the talk has run its course each takes it in
    // its own way — a laugh, a nod, rarer a proud chin or an awkward one (the same clips as a player's by the fire: BodyClips
    // fire_*_sit_*), on its body's layer for clips (NpcBody.Clip). Who speaks and who listens turn their heads to each other
    // in every talk alike (NpcSpeech: it took the place of the listening clip story_listen_*, which only turned the head
    // a set way aside; the player's own by the fire keeps it, Fireside). Orders go through the person (it may be run on another game).
    internal static class NpcFireside
    {
        internal static float EndChance = 0.75f; // that one takes the talk's end with a gesture at all

        // the talk over: to its end (finished), or broken off (nothing more)
        internal static void End(NpcActor a, NpcActor b, bool finished)
        {
            Close(a, b, finished);
            Close(b, a, finished);
        }

        private static void Close(NpcActor who, NpcActor other, bool finished)
        {
            if (who == null) return;
            if (!finished || other == null || who.Mind == null || !who.Mind.SeatedNow || Random.value > EndChance) return;
            who.Habit("clip fire_" + Mood(who) + "_sit_" + Side(who, other));
        }

        // which side of it the other sits on (the clips are made for both: _l, _r)
        private static string Side(NpcActor who, NpcActor other)
        {
            return Vector3.Dot(other.transform.position - who.transform.position, who.transform.right) >= 0f ? "r" : "l";
        }

        // how it takes a talk: the talkative laugh more
        private static string Mood(NpcActor who)
        {
            float laugh = 0.15f + NpcPersona.Of(who).Chatty * 0.4f, roll = Random.value;
            if (roll < laugh) return "laugh";
            if (roll < laugh + 0.1f) return "proud";
            if (roll < laugh + 0.2f) return "sheepish";
            return "nod";
        }
    }
}
#endif
