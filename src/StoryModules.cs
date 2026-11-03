#if RAVEN_ITEM && RAVEN_STORY
namespace HuginWhispers
{
    // The story's modules in one list (as NpcModules for the people): each brings its own conditions, scene steps and
    // hooks through the registries (StoryCond.Register, StoryScenes.Register, Trust.BackFrom …) — the core never names
    // a module. A new part of the story = its own file + one line here.
    internal static class StoryModules
    {
        // the conditions (StoryTrackConds.Register: on the first load of the story)
        internal static void Conds()
        {
            StoryOwn.Conds();   // «ownraven»: a raven of that kind is yours
            PrologueHeart.Actions(); // «heartlearn»: the heart opens for this character (an action, registered with the conditions: before the quests are read)
            NpcRaven.Actions();      // «npcraven <who> …»: a person's raven told what to do (quests, scenes' do)
        }

        // the scene steps (StoryScenes: when its kinds are first asked)
        internal static void Steps()
        {
            StoryOwn.Steps();   // «own hugin|munin»: your own raven comes back (and its landing)
            RavenSkills.Silence.Add(() => StoryScenes.Playing.Length > 0); // a scene playing: the ravens say only its lines
        }
    }
}
#endif
