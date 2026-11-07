#if RAVEN_ITEM && RAVEN_STORY
using UnityEngine;

namespace HuginWhispers
{
    // The person the story wants you to talk to now (the stage's word «call: heid»; the user 2026-11-05: no «!» as in other games, the gold of
    // the things to be found, of Odin and the Watch): a small thread of golden specks rising about half a metre over its head. Only on this
    // player's own game, only while his stage says so and no scene plays. Nothing goes over the network; a person out of reach has none.
    // The thread never cuts off: the specks in the air finish their way (WsItems.CallLine lives on StopAndGo).
    [DefaultExecutionOrder(31000)] // (after the pose: on the head as it is shown)
    internal sealed class StoryCallMark : MonoBehaviour
    {
        internal static float OverHead = 0.3f, GoesIn = 2.5f;
        private static StoryCallMark _on;

        private NpcActor _who;
        private Transform _head, _visual;
        private ParticleSystem _ps;

        // twice a second (StoryQuests.Each): whom this player's stage calls now — its «call:» (a scene playing: nobody, its people are at work)
        internal static void Look(Player p)
        {
            NpcActor who = null;
            if (StoryScenes.Playing.Length == 0)
                foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
                {
                    StoryQuests.StageDef s = StoryQuests.StageNow(p, q);
                    if (s != null && s.Call.Length > 0) { who = StoryScenes.Actor(s.Call, p.transform.position); break; }
                }
            Want(who);
        }

        private static void Want(NpcActor who)
        {
            if (_on != null && _on._who == who) return;
            if (_on != null) { _on.Leave(); _on = null; }
            if (who == null) return;
            GameObject g = WsItems.CallLine();
            _on = g.AddComponent<StoryCallMark>();
            _on._who = who;
            _on._ps = g.GetComponent<ParticleSystem>();
        }

        private void Leave()
        {
            _who = null;
            if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, GoesIn);
        }

        private void LateUpdate()
        {
            if (_who == null) { if (_on == this) { Leave(); _on = null; } return; } // (its body gone: out of reach)
            if (_head == null)
            {
                Animator a = _who.GetComponentInChildren<Animator>(); // heavy: only till its head is found, one person, and only while called
                if (a != null) { _visual = a.transform; _head = BodyClips.SkeletonBone(_who, _visual, "Head"); }
            }
            transform.position = (_head != null ? _head.position : _who.transform.position + Vector3.up * 1.7f) + Vector3.up * OverHead;
        }
    }
}
#endif
