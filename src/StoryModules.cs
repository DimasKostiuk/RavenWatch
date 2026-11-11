#if RAVEN_ITEM && RAVEN_STORY
namespace HuginWhispers
{
    // The story's modules in one list (as NpcModules for the people): each brings its own conditions, scene steps and
    // hooks through the registries (StoryCond.Register, StoryScenes.Register, Trust.BackFrom …) — the core never names
    // a module. A new part of the story = its own file + one line here.
    internal static class StoryModules
    {
        // the server's own look (the game's clock): a part that throws rests, the rest of the game's Update goes on (Parts)
        private static readonly Parts Server = MakeServer();
        private static Parts MakeServer()
        {
            Parts p = new Parts("StoryModules");
            p.Add("people", StoryPeople.ServerTick); // the story's people kept once in the world, where they live
            p.Add("first heart", StoryHeart.ServerTick); // a world with a heart from before gets «heart_first», once
            return p;
        }
        internal static void ServerTick() { Server.Run(null); }

        // the conditions (StoryTrackConds.Register: on the first load of the story)
        internal static void Conds()
        {
            StoryQuests.Places(); // the place «goal.mark»: known before a scene names it
            StoryOwn.Conds();   // «ownraven»: a raven of that kind is yours
            StoryHeart.Actions();    // «heartlearn», «heart.near»: the hearts of a home (an action, registered with the conditions: before the quests are read)
            NpcRaven.Actions();      // «npcraven <who> …»: a person's raven told what to do (quests, scenes' do)
            StorySpot.Actions();     // «spot <name> <min> <max>» and the place «me.<name>»: a spot of the player's own in the wild
            StoryTales.Register();   // «taleuntold», «{tale}», «!taletold»: what the people saw, told to one who missed it
            StoryQuests.Each.Add("tales", StoryTales.Look);
            StoryQuests.Each.Add("quiet rounds", StoryHush.Look);  // the quiet rounds round the scenes the stages lead to
            StoryQuests.Each.Add("call mark", StoryCallMark.Look); // «call: <who>»: a thread of gold over that person
            StoryQuests.Each.Add("been", StoryTrackConds.Been);    // «been <place>»: stood on its ground (written once)
            StoryQuests.Each.Add("heart known", StoryHeart.Look);  // the heart learnt in this world: its piece for this player
            StoryQuests.Frame.Add("book given", p => RavenBook.Given(p)); // (the story's first look at this character, at once: a raven of his from before keeps the book)
            StoryQuests.Frame.Add("quiet rounds", StoryHush.Tick); // the rounds grow and fade softly
            StoryQuests.Frame.Add("props", StoryProps.Look);       // the things in hands (a few times a second, its own clock)
            StoryQuests.Frame.Add("watching", StoryWatch.Look);    // another's scene near me: what it holds of me, its director gone
        }

        // the scene steps (StoryScenes: when its kinds are first asked)
        internal static void Steps()
        {
            StoryOwn.Steps();   // «own hugin|munin»: your own raven comes back (and its landing)
            StoryHeart.Steps();    // «heart flare»: a flash in the nearest heart's lantern
            StoryAudio.Steps();    // «music», «bed», «mix», «mute», «ear», «sound»: what is heard
            StoryScenes.Ends.Add(StoryAudio.SceneEnd); // (a scene over: all it held of what is heard let go)
            StoryScenes.Ends.Add(StoryOwn.SceneEnd);   // (… and your raven it left on a seat)
            StoryScenes.OwnEnds.Add(StoryOwn.SceneEnd);
            RavenSkills.Silence.Add(() => StoryScenes.Playing.Length > 0); // a scene playing: the ravens say only its lines
        }

        // the story's words to people where they are run (NpcModules.Wire: once, with the NPC modules)
        internal static void People()
        {
            NpcStray.Told = z => StoryHold.Of(z.GetString(Npcs.KId, "")).Length > 0; // (and is not brought home)
            NpcActor.Ticks.Add(StoryHold.Tick);        // a word of the story to a person: it stands till «free» or the next word
            NpcActor.Unloaded.Add(StoryHold.Forget);
            NpcActor.Ticks.Add(StoryTales.Tick);       // who saw a moment of the world's story
            NpcActor.Unloaded.Add(StoryTales.Forget);
            NpcMind.KeepClip = StoryHold.KeepsClip;    // a scene over: the clip of its standing word stays on
            NpcPlaces.Authored = StoryLife.Places;     // its own life between the words: the places the author gave it
            NpcActor.Ticks.Add(StoryProps.Keep);       // a thing of its own on its arm: its loop kept on; what waits for a clip's moment (set down, taken up)
            NpcActor.Unloaded.Add(StoryProps.Forget);
            NpcPlaces.ClipBegins = a => StoryProps.SetDown(a, true, false); // a visit with a clip of its own: the thing off its arm first, where it stands (its own doing: it goes back for it itself)
        }
    }
}
#endif
