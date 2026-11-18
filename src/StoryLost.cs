#if RAVEN_ITEM && RAVEN_STORY
using System;
using UnityEngine;

namespace HuginWhispers
{
    // A thing of the story a stage waits for, lying on the ground, is never lost for good (the user 2026-11-16: «запобіжники на випадок, якщо сервер
    // видалить предмети з землі, щоб не було софт локу»). The game's own clock does not take it (WsItems: m_autoDestroy off), but a server's cleaning,
    // another mod that clears what lies about, the sea may. A stage's line:
    //   lost: <item prefab> <place>     while this stage stands and you do not carry the thing, you are within Near m of the place and its ground is
    //                                   loaded, and none of it lies within Reach m of it nor is in a chest there: it is laid there again (StoryFound.Leave,
    //                                   the one way the story lays a thing of the world) and said in the log. The one that was there may be in another
    //                                   player's bag (a stage of the world's, he away): a second one then — better two than a story that cannot go on.
    // storycheck asks it of every stage that waits for a thing the story puts on the ground or at the feet (a scene's «drop», «do give», a quest's «lay»).
    // Cost: twice a second the stages' few words (most have none); the look round (a sphere, the loaded chests: ItemMarks.Around) only near the place
    // without the thing, once in Every s
    internal static class StoryLost
    {
        internal static float Near = 30f, Reach = 15f, Every = 6f;
        private static float _next;

        internal static void Look(Player p)
        {
            if (p == null || Time.time < _next || ZNetScene.instance == null || StoryScenes.Playing.Length > 0) return; // (a scene lays its own things: not in the middle of it)
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                StoryQuests.StageDef s = StoryQuests.StageNow(p, q);
                if (s != null) foreach (string line in s.Lost) Check(p, q, s, line);
            }
        }

        private static void Check(Player p, StoryQuests.QuestDef q, StoryQuests.StageDef s, string line)
        {
            string[] w = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Vector3 at;
            if (w.Length < 2 || ItemMarks.InBag(p.GetInventory(), w[0]) > 0) return;
            if (!StoryWhere.Find(w[1], out at) || StoryWhere.Apart(at, p.transform.position) > Near || !ZNetScene.instance.IsAreaReady(at)) return;
            _next = Time.time + Every; // (one look round in a while, whatever it finds: a thing laid needs its frames to come)
            if (ItemMarks.Around(at, Reach, w[0]) > 0) return;
            if (StoryFound.Leave(w[0], at)) Story.Log("lost: " + q.Id + "." + s.Id + ": " + w[0] + " was gone from " + w[1] + " (not on the ground, in no chest near, not in your bag) — laid there again");
            else if (Told.Add(w[0])) Story.Log("lost: " + q.Id + "." + s.Id + ": no item «" + w[0] + "» to lay again"); // (once: not every Every s)
        }
        private static readonly System.Collections.Generic.HashSet<string> Told = new System.Collections.Generic.HashSet<string>(); // session: an item said missing
    }
}
#endif
