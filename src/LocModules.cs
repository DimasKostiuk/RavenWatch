#if RAVEN_ITEM && RAVEN_NPC
using System;
using UnityEngine;

namespace HuginWhispers
{
    // The parts of a place of the story in one list (as NpcModules for the people, StoryModules for the story): the game's clock calls only this,
    // and the core names no part. A new part of a place = its own file + one line here; what it asks of a place = a line of the list (LocDef, Locs.Lines).
    // A part that throws is said in the log and rests (Parts); the others go on.
    internal static class LocModules
    {
        private static readonly Parts Server = MakeServer(), Everyone = MakeEveryone();

        private static Parts MakeServer()
        {
            Parts p = new Parts("LocModules");
            p.Add("place spots", Locs.ServerTick);     // each place's spot, once a world, and its building ahead of the players (LocBuild)
            p.Add("place steps", LocState.ServerTick); // a change waiting till nobody sees it; «becomes:», «follows:»
            return p;
        }

        private static Parts MakeEveryone()
        {
            Parts p = new Parts("LocModules");
            p.Add("place marks", Locs.Tick);     // its golden mark on the map («mapmark:»)
            p.Add("place looks", LocState.Tick); // its state laid on the pieces loaded round you
            p.Add("place fog", LocFog.Tick);     // Hel's mist («fog:»)
            p.Add("place cold", LocCold.Tick);   // the cold inside its building («cold:»)
            return p;
        }

        // each second, for each built place you are near (LocState.Tick): a part that looks at its loaded pieces
        internal static readonly Action<Loc, Player>[] Near = { LocTouch.Tick }; // («touch:»: what glows under E)

        internal static void ServerTick() { Server.Run(null); }
        internal static void Tick() { Everyone.Run(null); }
    }
}
#endif
