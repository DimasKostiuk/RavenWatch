#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A scene's frame kept for its own (the user 2026-11-15, ember_home: a villager walked up to the player, stood by him in every shot and spoke
    // a remark over the scene). While a scene plays on this game, the people not in it (StoryScenes.Holder: no scene holds them) step aside and
    // keep quiet near the player; when it is over, their day again:
    //   aside  one within Clear m of the player walks off to To m from him (a word on its Scene layer, owner «aside»: over its day, its
    //          whims and a story's «hold»; never over a scene's own word) — once a scene; «free» on that layer as the scene ends.
    //          A scene of «aside: away» (the user 2026-11-17, Eyvind met in the Hall: «нехай на момент сцени нпс уходять, але не завжди — в
    //          деяких сценах ок, що на фоні хтось ходить»): everyone within Quiet m leaves the frame, off to Gone m; one whose day brings it
    //          near while it plays goes too (each tick looks)
    //   quiet  one within Quiet m says nothing of its own (NpcActor.Hushed: asked as a line is made — a remark, an event's line, small talk)
    // Where it is run (NpcActor.Ticks): a person run by another game is moved by that game's scene only (a scene is one player's, his game
    // runs the people round him as a rule). Cost: one look at the scene's id a tick a person; the rest only while one plays.
    internal static class StoryAside
    {
        internal static float Clear = 7f, To = 11f, Quiet = 20f, Gone = 28f;
        private const string Whose = "aside";
        private static readonly HashSet<NpcActor> _aside = new HashSet<NpcActor>(); // session: the people this game sent off for the scene now

        internal static void Tick(NpcActor a)
        {
            bool on = StoryScenes.Playing.Length > 0;
            if (!on && _aside.Count == 0) return;
            bool sent = _aside.Contains(a);
            if (!on || InScene(a)) // (over, or the scene took this one itself or names it: its own again)
            {
                if (sent) { _aside.Remove(a); a.Order("free", NpcMind.Layer.Scene, Whose); }
                return;
            }
            if (sent) return;
            Player p = Player.m_localPlayer;
            if (p == null) return;
            StoryScenes.SceneDef s = StoryScenes.Current;
            bool far = s != null && s.Away; // («aside: away»: out of the frame, not only off the player)
            float near = far ? Quiet : Clear;
            Vector3 from = p.transform.position, at = a.transform.position, away = at - from;
            away.y = 0f;
            if (away.sqrMagnitude > near * near) return;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -p.transform.forward;
            Vector3 to = from + away * (far ? Gone : To);
            _aside.Add(a);
            a.Order("go " + V(to) + " ease", NpcMind.Layer.Scene, Whose);
        }

        // one gone from this game while sent off: nothing to free here (the game that runs it next has no word of ours on it)
        internal static void Forget(NpcActor a) { _aside.Remove(a); }

        // no line of its own now: a scene plays here, it is not in it, and it is near the player
        internal static bool Hushed(NpcActor a)
        {
            if (StoryScenes.Playing.Length == 0 || a == null || InScene(a)) return false;
            Player p = Player.m_localPlayer;
            return p != null && (a.transform.position - p.transform.position).sqrMagnitude < Quiet * Quiet;
        }

        // the scene's own: taken by it (its lock), or named in its steps (its cast: «npc lit say …» with no «take» — the dwarf fighting on in lit_fight).
        // Its words then are the scene's, never hushed (StoryStranger asks it too)
        internal static bool InScene(NpcActor a)
        {
            if (StoryScenes.Holder(a) != 0L) return true;
            StoryScenes.SceneDef s = StoryScenes.Current;
            return s != null && s.Cast.Contains(a.Id);
        }

        private static string V(Vector3 v) { return v.x.ToString("0.00", CultureInfo.InvariantCulture) + " " + v.y.ToString("0.00", CultureInfo.InvariantCulture) + " " + v.z.ToString("0.00", CultureInfo.InvariantCulture); }
    }
}
#endif
