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
            p.Add("found", StoryFound.ServerTick); // «pickloc»: a place the world finds among the game's locations (only the server knows them all)
            p.Add("waves", StoryWaves.ServerTick); // «waves»: foes at a place of the story, wave after wave while a player is there (StoryWaves)
            p.Add("quest resume", StoryQuests.ServerResume); // a quest of the server ended by «next: end», told on in a newer version: on again from there
            return p;
        }
        internal static void ServerTick() { Server.Run(null); }

        // the conditions (StoryTrackConds.Register: on the first load of the story)
        internal static void Conds()
        {
            StoryQuests.Places(); // the place «goal.mark»: known before a scene names it
            LocInside.Places();   // the places «in.<layout>[.<point>]»: an inside of the game's rooms, its way in and its named points
            StoryOwn.Conds();   // «ownraven»: a raven of that kind is yours
            StoryHeart.Actions();    // «heartlearn», «heart.near», «heart.first»: the hearts of a home (an action, registered with the conditions: before the quests are read)
            NpcRaven.Actions();      // «npcraven <who> …»: a person's raven told what to do (quests, scenes' do)
            StoryKeep.Actions();     // «keepbed», «unkeep», «kept», the event «keep_use»: a piece the story keeps whole till it is looked at (Ketil's bed)
            StoryChest.Actions();    // «chest <place>.<piece> <Prefab> <N>», «chesttake …»: a chest of a place the story fills (the Hall's, chapter 2)
            StoryFound.Actions();    // «pickloc», «stash», the place «found.<name>»: a location of the game the world picks, a thing put in its chest
            StoryWaves.Actions();    // «waves <name> <place> <m> …»: foes at a place, wave after wave; all down — the world fact <name>
            StoryPeople.Conds();     // «npcbed <who>»: a story person has a bed
            StorySpot.Actions();     // «spot <name> <min> <max>» and the place «me.<name>»: a spot of the player's own in the wild
            StoryTales.Register();   // «taleuntold», «{tale}», «!taletold»: what the people saw, told to one who missed it
            StoryQuests.Each.Add("tales", StoryTales.Look);
            StoryQuests.Each.Add("quiet rounds", StoryHush.Look);  // the quiet rounds round the scenes the stages lead to
            StoryQuests.Each.Add("stash", StoryFound.Look);
            WsItems.StoryOver = until => StoryCond.Holds(Player.m_localPlayer, until, Story.Log, true); // an item's «story: … until <the world's conditions>»: then an item as any
            StoryQuests.Each.Add("kept things", p => ItemMarks.Tick()); // the story's things that stay with you: one gone some other way (a bin) comes back       // «stash»: a thing put into a chest of a place loaded near you
            StoryQuests.Each.Add("chests", StoryChest.Look);       // «chest …»: what the stage still wants in a chest (its hover), «chesttake»: the things taken out by the game that holds it
            StoryQuests.Each.Add("seek", StorySeek.Look);          // «seek: <place> <words>»: the raven on the shoulder as the finder of that place (HotCold)
            StoryQuests.Each.Add("seat words", LocSitSay.Look);    // «sitsay: <place> <tag> [conditions]»: sat on a seat there — the raven on the shoulder has its word (the barrow's throne)
            StorySceneRavens.Register();                            // a scene that speaks with your ravens fetches them: back from a shy flight, out of the bag for the scene
            StoryQuests.Each.Add("scene ravens", StorySceneRavens.Look); // …and a raven lent out of the bag goes back into it after the scene
            Senses.SceneNow = () => StoryScenes.Playing.Length > 0; // a scene plays here: no raven leaves the shoulder by itself (Trust.FreeToLeave, Outing)
            StoryQuests.Each.Add("stock", StoryStock.Look);        // «stock: <place> <m> <Prefab> <N> …»: what the stage sends you to find there is there when you come
            StoryQuests.Each.Add("lost things", StoryLost.Look);   // «lost: <item> <place>»: a thing the stage waits for, gone from the ground, laid there again
            StoryQuests.Each.Add("call mark", StoryCallMark.Look); // «call: <who>»: a thread of gold over that person
            StoryQuests.Each.Add("been", StoryTrackConds.Been);    // «been <place>»: stood on its ground (written once)
            StoryQuests.Each.Add("heart known", StoryHeart.Look);  // the heart learnt in this world: its piece for this player
            StoryQuests.Each.Add("chapter title", StorySaga.Look); // a chapter begun: its title on the screen, once a character
            StoryQuests.Frame.Add("book given", p => RavenBook.Given(p)); // (the story's first look at this character, at once: a raven of his from before keeps the book)
            StoryQuests.Frame.Add("quiet rounds", StoryHush.Tick); // the rounds grow and fade softly
            StoryQuests.Frame.Add("props", StoryProps.Look);       // the things in hands (a few times a second, its own clock)
            StoryQuests.Frame.Add("stage music", StoryStageMusic.Tick); // a stage's «music:» while it stands (the rite's fight: silence)
            StoryQuests.Frame.Add("inside music", LocInsideMusic.Tick); // «insidemusic:»: a place's own music while the player is in its inside
            StoryQuests.Frame.Add("rising foes", StoryWaves.Rising); // «waves … rise <s>»: a wave that comes up stands, of the players' side, before it fights
            StoryQuests.Frame.Add("scene beasts", StoryBeasts.Tick); // «beast … go|hunt»: the scene's creatures walked, their foes held
            Senses.Seen = HelEyes.Look;                             // a creature with Hel's eyes (StoryBeasts «hel»), painted on every game that draws it
            StoryQuests.Frame.Add("watching", StoryWatch.Look);    // another's scene near me: what it holds of me, its director gone
        }

        // the scene steps (StoryScenes: when its kinds are first asked)
        internal static void Steps()
        {
            StoryOwn.Steps();   // «own hugin|munin»: your own raven comes back (and its landing)
            StoryHeart.Steps();    // «heart flare»: a flash in the nearest heart's lantern
            StoryAudio.Steps();    // «music», «bed», «mix», «mute», «ear», «sound»: what is heard
            StoryDome.Steps();     // «dome on|off»: Hugin's dome inside out over the player (the ambush)
            StoryBeasts.Steps();   // «beast <name> spawn|go|hunt|dead|stay|gone»: the game's own creatures in a scene (the skeleton and the boar)
            StoryScenes.Ends.Add(StoryAudio.SceneEnd); // (a scene over: all it held of what is heard let go)
            StoryScenes.Ends.Add(StoryOwn.SceneEnd);   // (… and your raven it left on a seat)
            StoryScenes.Ends.Add(StoryDome.SceneEnd);  // (… and its dome lowered)
            StoryScenes.OwnEnds.Add(StoryOwn.SceneEnd);
            RavenSkills.Silence.Add(() => StoryScenes.Playing.Length > 0); // a scene playing: the ravens say only its lines
        }

        // the story's words to people where they are run (NpcModules.Wire: once, with the NPC modules)
        internal static void People()
        {
            NpcStray.Told = z => StoryHold.Now(z).Length > 0; // (and is not brought home: a story's word, or its day's far line — StoryLife.Far)
            NpcActor.Ticks.Add(StoryHold.Tick);        // a word of the story to a person: it stands till «free» or the next word
            NpcActor.Unloaded.Add(StoryHold.Forget);
            NpcActor.Ticks.Add(StoryPeople.LeaveTick); // a story person the server sends out of the world (away, or for good) goes the moment nobody sees it
            NpcActor.Unloaded.Add(StoryPeople.Forget);
            NpcActor.Ticks.Add(StoryTales.Tick);       // who saw a moment of the world's story
            NpcActor.Unloaded.Add(StoryTales.Forget);
            NpcMind.KeepClip = StoryHold.KeepsClip;    // a scene over: the clip of its standing word stays on
            NpcMoves.Needs["carry"] = StoryProps.InHand; // «moves: clip@carry»: a move of its own only while its own thing is on its arm (Heid's herbs from her basket)
            NpcMoves.Needs["free"] = a => NpcTool.HandOf(a).Length == 0 && !StoryProps.InHand(a); // «moves: clip@free»: both hands free of its own things (Lit's hammer out of his hand: hands on his hips)
            // the story's own actions a talk may do, as «!<action> …» — the action itself (StoryActions), one way for each:
            //   «!world <name>»  a fact of this world (a server's story writes no character: chapter 1 — what you told Heid of the scroll, a fork later); the
            //                    talk runs on your game, the world key goes to everyone as any other
            //   «!spend <name> <hours>»  one more of a few at a time (condition «quota»: Torvald's tall tales, three, then a rest)
            NpcVitals.StoryOwn = StoryPeople.Wears;    // the story's people: no monster sees them unless their template says «unseen: no»
            NpcMap.Hidden = StorySeek.Hides;           // a search on: the one it is for has no mark on the map (StorySeek)
            NpcActor.Ticks.Add(StoryAside.Tick);       // a scene plays: the people not in it step out of its frame (StoryAside)…
            NpcActor.Unloaded.Add(StoryAside.Forget);
            NpcActor.Hushed = a => StoryAside.Hushed(a) || StoryStranger.Hushed(a); // …and keep quiet near it; one not met yet keeps quiet too
            StoryStranger.Wire();                      // «stranger:»: a person not met yet — no name, no talk, E and Q nothing; «strangersay:» your ravens' first words of it
            foreach (string deed in new[] { "world", "spend" })
                NpcTalk.Deeds[deed] = (a, p, w) => { if (w.Length > 1) StoryActions.Do(p, null, string.Join(" ", w)); };
            NpcPlaces.Authored = StoryLife.Places;     // its own life between the words: the places the author gave it
            NpcWhims.Held.Add(StoryLife.Planned);      // …and its day by the hour: no whim of its pleasure while its day says where it is
            NpcVoice.Sources.Add(StoryLife.Themes);    // …and what it does at a place of its day, for its own words unasked («at_<kind>», «say <theme>»)
            NpcActor.Ticks.Add(StoryLife.Tick);        // …an outing reached: what it came to say is due at its next remark
            NpcActor.Unloaded.Add(StoryLife.Forget);
            Npcs.Commands["day"] = (p, who, a, aim) => StoryLife.Tell(who);
            StoryMeets.Wire();                         // …and its gatherings: those whose days name the same one talk their own talks there
            NpcActor.Ticks.Add(StoryProps.Keep);       // a thing of its own on its arm: its loop kept on; what waits for a clip's moment (set down, taken up)
            NpcActor.Unloaded.Add(StoryProps.Forget);
            NpcPlaces.ClipBegins = (a, v) => StoryProps.SetDown(a, true, false) + (v.Tool ? NpcTool.Take(a, true) : 0f); // a visit with a clip of its own: the thing off its arm first, where it stands (its own doing: it goes back for it itself); its own tool into its hand where the place says «tool»
            NpcPlaces.ClipEnds = (a, v) => { if (v.Tool) NpcTool.Take(a, false); }; // …and back on its belt as it goes
            // E on a person a stage waits for («on: npc_use <who>», chapter 1: Ketil by the heart — his scene, no talk window): the event, nothing else.
            // Only while a stage waits for it: after that E opens its talk as ever
            NpcActor.UseInstead.Add((a, p) =>
            {
                if (a.Def == null || p == null || p != Player.m_localPlayer || !StoryQuests.Awaits(p, "npc_use", a.Def.Id)) return null;
                StoryBus.Raise("npc_use", a.Def.Id, 0, a.transform.position);
                return true;
            });
        }
    }
}
#endif
