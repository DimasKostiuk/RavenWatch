#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A scene counts on your ravens (the user 2026-11-16, Eirik's hut: his raven talked to nobody — Munin had flown off on his own while the scene
    // waited for quiet, and every line of both was skipped; «якщо вдруг я зняв ворона або він у сумці, то у сцені всеодно він нехай буде»). As a
    // scene begins (the bus's «scene_start»), each kind of raven it speaks with («say <kind>», a condition «raven <kind>») and does not bring in itself
    // («own <kind>», «bird shoulder»: the scene where it first comes) is fetched:
    //   · worn, off on a shy flight of its own (Trust.Shy): that flight cut short — it flies in onto the shoulder at once, nothing said;
    //   · none worn of that kind, an awake raven of yours of it in the bag: onto its shoulder for the scene, quietly (a wait after taking it off does not
    //     count), and back into the bag Linger s after no scene plays any more (its end, its stop) — no offence either. A relog in the middle: the
    //     game saved it on the shoulder, and there it stays (this game's run only knows what it lent).
    // Sent off by you — to Odin, the nest, the post, a perch: it stays where it was sent, and the scene's lines without it hold (docs/jobs/scene.md).
    // Cost: on a scene's start one pass over its steps and over the bag; twice a second (StoryQuests.Each) nothing while none is lent.
    internal static class StorySceneRavens
    {
        internal static float Linger = 3f; // s with no scene before a lent raven goes back: a scene that follows another keeps it
        private static readonly bool[] Kinds = { false, true };
        private static readonly List<ItemDrop.ItemData> Lent = new List<ItemDrop.ItemData>(2); // ravens put on the shoulder out of the bag for a scene
        private static float _sceneAt;

        internal static void Register() { StoryBus.Listen("scene ravens", e => { if (e.Name == "scene_start") Fetch(Player.m_localPlayer, StoryScenes.Current); }); }

        private static void Fetch(Player p, StoryScenes.SceneDef s)
        {
            if (p == null || s == null || p.IsDead()) return;
            foreach (bool munin in Kinds) if (Speaks(s, munin)) Bring(p, munin);
        }

        // the scene has words of that raven and does not bring it in itself
        private static bool Speaks(StoryScenes.SceneDef s, bool munin)
        {
            string kind = munin ? "munin" : "hugin";
            bool speaks = false;
            foreach (StoryScenes.Step st in s.Steps)
            {
                string[] w = st.W;
                if (w.Length > 1 && ((w[0] == "own" && w[1] == kind) || (w[0] == "bird" && w[1] == "shoulder" && (w.Length < 3 || w[2] == kind)))) return false; // («bird shoulder» with no kind: its bird's — one of the two, so neither is fetched)
                if (w.Length > 1 && w[0] == "say" && w[1] == kind) speaks = true;
                if (st.If != null) foreach (Cond c in st.If) if (c.A.Length > 1 && c.A[0] == "raven" && (c.A[1] == kind || c.A[1] == "any" || c.A[1] == "both")) speaks = true;
            }
            return speaks;
        }

        private static void Bring(Player p, bool munin)
        {
            ItemDrop.ItemData worn = RavenSeat.On(p, munin);
            if (worn != null)
            {
                if (Trust.AwayWhy(worn) != Trust.Shy || Perches.On(worn)) return; // (on the shoulder, or sent off: as it is)
                Trust.CutShy(worn);
                RavenJournal.Note(p, worn, "back on the shoulder for a scene (its shy flight cut short)");
                return;
            }
            ItemDrop.ItemData best = null;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
            {
                bool m;
                if (!RavenItem.IsRaven(it, out m) || m != munin || RavenSeat.IsSeated(it) || !Trust.Active(it) || Trust.Stranger(it, p)) continue;
                if (best == null || Trust.Level(it) > Trust.Level(best)) best = it;
            }
            if (best == null || !RavenSeat.Sit(p, best, true)) return;
            Lent.Add(best);
            RavenJournal.Note(p, best, "on the shoulder for a scene (out of the bag)");
        }

        // twice a second: a raven lent to a scene goes back into the bag once no scene plays
        internal static void Look(Player p)
        {
            if (Lent.Count == 0 || p == null) return;
            if (StoryScenes.Playing.Length > 0) { _sceneAt = Time.time; return; }
            if (Time.time - _sceneAt < Linger || p.IsDead()) return;
            foreach (ItemDrop.ItemData it in Lent)
                if (RavenSeat.IsSeated(it) && RavenSeat.TakeOff(p, it, false)) RavenJournal.Note(p, it, "back into the bag after the scene");
            Lent.Clear(); // (taken off meanwhile, died, no room in the bag: it stays as it is now)
        }
    }
}
#endif
