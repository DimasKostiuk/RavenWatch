#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What a place of the story is: the lines of its block in the list (Locs.Lines reads them; their words: the head of locations.list.txt)
    // and nothing else: no state of a game, so a fresh read of the list goes over a place in one copy of these fields (Loc.Take).
    // A new rule of a place = a field here, a line in Locs.Lines, and what asks it.
    internal class LocDef
    {
        // ---------------------------------------------------------------- what it is (Locs reads it)
        internal string Id, Title;
        internal Heightmap.Biome Biome = Heightmap.Biome.Meadows;
        internal string Near = "";                   // round another place of the story («» — round the start)
        internal Dictionary<string, float> Apart = new Dictionary<string, float>(); // «apart: prologue_munin 100»: its middle at least that far from that place's (both ways)
        internal bool Edge;                          // Min..Max counted between the walls of the two (not from the other's middle)
        internal float Min = 300f, Max = 1500f, Far = 3000f; // m from the start / from that place (the last ring only if the first has no place)
        internal float Clear = 40f;                  // m round the footprint free of any player's building
        internal float RoomAlong, RoomAcross;        // «room: 5 8»: its own ground beyond the blueprint's walls, m in front and behind / on each side (the user 2026-11-06: room for its states to come — a caravan's road); sought, levelled and cleared with it
        internal float NoBuild = 30f;                // m from its middle: no building there (as round the start's stones)
        internal string[] Words = { "закинута" };    // its states' names (1, 2, …)
        internal string[][] Subs = { new string[0] }; // each state's sub-states' names (1.1, 1.2, …; 1.0 — the state itself)
        internal int RuinTo = 1;                     // states 1..RuinTo: a ruin (broken look, fires out)
        internal string[] Aliases = new string[0];   // «alias: hall»: other words the story's data may call it by (Locs.Get)
        internal string Gate = "";                   // «gate: dveri_holovni»: its great door (a named piece) — shut and locked till the story opens it (LocState)
        internal bool Fog;                           // Hel's mist over it while a ruin (LocFog)…
        internal string FogWhile = "";               // …and while these hold («fog: hearth zala < 2»: the Watch's fire breaks Hel's hold) — world conditions
        internal string ColdWhile = "";              // «cold: state zala < 1.2»: inside its building you are cold while these hold (LocCold) — world conditions
        internal bool Refuge;                        // «refuge: yes»: the ravens' home — the unhired go there to be hired again (NpcTravel; the Hall)
        internal string HearthShut = "Попіл не займається. Тут ще замкнено.";  // «hearthshut: <слова>»: its hearth when its great door is still locked
        internal string MapMark = "";                // «mapmark: world heart_first, !been zala»: its golden mark on the map for whoever these hold for (no story: always)
        internal List<Rule> Becomes = new List<Rule>(); // «becomes: 1.1 hearth zala >= 1»: the server moves it on to that step once these hold — forward only, behind the scenes
        internal Rule Follow;                        // «follows: 2 state zala >= 1.2»: that state while these hold, else its first — both ways, behind the scenes (Heid's camp)
        internal Dictionary<string, List<KeyValuePair<string, string>>> Reads = new Dictionary<string, List<KeyValuePair<string, string>>>(); // «read: rizba hearth zala 2 :: <words>»: what E on a touched piece shows, the first whose conditions hold
        internal Dictionary<string, string> ElseDo = new Dictionary<string, string>(); // «elsedo: lyst heartlearn»: the story's actions on a touch no stage of yours waits for (LocTouch)
        internal HashSet<string> TouchQuiet = new HashSet<string>(); // («touch: rizba quiet …»: no glow — E on it all the same)
        internal string[] RuinLit = new string[0];   // pieces (prefabs) still burning in its ruin — the rest of its fires are out
        internal List<LitStep> LitFrom = new List<LitStep>(); // pieces (prefabs) burning in its ruin from a step on (Hall: hearth from 1.1)
        internal List<KeyValuePair<string, string>> LitWhile = new List<KeyValuePair<string, string>>(); // «litwhile: fire_pit !flag echo_gone»: that fire burns (no fuel needed) for you while the story's conditions hold for you, else it is out (LocState.LitWhile)
        internal List<KeyValuePair<string, string>> HideWhile = new List<KeyValuePair<string, string>>(); // «hidewhile: lyst flag letter_read»: that named piece is gone for you while the story's conditions hold for you (any of its lines); a world's condition: for everyone (LocState.HideWhile: the Hall's old letter, once read)
        internal List<KeyValuePair<string, string>> StateWhile = new List<KeyValuePair<string, string>>(); // «statewhile: 2 !flag echo_gone»: the state each player sees is his own — that one while the story's conditions hold for him, else the first (LocState.StateOf: the ghost's house lived in till he has the book)
        internal HashSet<string> TouchAnim = new HashSet<string>(); // («touch: knyha anim …»: the game's reach-and-touch plays on E; else none)
        internal List<KeyValuePair<string, string>> Touches = new List<KeyValuePair<string, string>>(); // «touch: shchyt step prologue munin_shield»: that named piece glows for you while the conditions hold, E on it — the story's event «use <place>.<name>» (LocTouch)
        internal float Sink;                         // m it stands lower in the ground than it was saved (its foundation half in the earth)
        internal bool LockChests;                    // its chests do not open (nobody's to take from)
        internal bool LockStations;                  // its workbenches, forges… are no stations: nothing made, built or learnt by them
        internal string LockDoors = "";              // its doors locked for each player while these conditions hold for him (StoryCond: till the story leads him in)
        internal string ShutDoors = "";              // …and these stand shut for him till he opens them himself, whatever the world says (LocDoorView)
        internal string[] OpenDoors = new string[0]; // of those, the pieces never locked, by prefab or by name (a wicket in a fence, the door of an unbuilt hut)
        internal bool LockBeds;                      // nobody sleeps in its beds (nor makes them his spawn)
        internal bool Grass;                         // its ground keeps all its grass (else bare earth under its floors, grass round them)
        internal float BareRound;                    // >0: bare earth only within that many m of its middle, grass round it («grass: 5»)
        internal Dictionary<string, Color> Tints = new Dictionary<string, Color>(); // pieces (prefabs) recoloured in it
        internal string KeyBase;                     // its world keys' stem

        // a step of its states and the world conditions that bring it (becomes, follows)
        internal sealed class Rule { internal int State, Sub; internal string When = ""; }

        internal int Last { get { return Words.Length; } }
        internal bool Is(string word) { return Id == word || Array.IndexOf(Aliases, word) >= 0; }
        internal string KeySpot { get { return KeyBase; } }
        internal string KeyBuilt { get { return KeyBase + "_built"; } }
        internal string KeyNo { get { return KeyBase + "_no"; } }
        internal string KeyAgain { get { return KeyBase + "_again"; } } // (built again where it stood: no player-building check, it stood there already)
        internal string KeyWild { get { return KeyBase + "_wild"; } } // (its ground cleared by Wild's answer: a place built before it kept the bushes — LocBuild.WildOnce)
        internal string KeyTight { get { return KeyBase + "_tight"; } } // (no place with its room in this world: it stands by its walls alone — Loc.Room)
        internal string KeyState { get { return KeyBase + "_state"; } }
        internal string KeyNext { get { return KeyBase + "_next"; } }
        internal string KeySub { get { return KeyBase + "_sub"; } }
        internal string KeyGate { get { return KeyBase + "_gate"; } }
        internal string KeyFire { get { return KeyBase + "_fire"; } } // (its hearth's fire, if it has one: LocHearth)
        // the keys the story changes as it goes (its state, its gate, its fire) — not where it stands nor that it is built: what a
        // studio check gives back after its run (StudioScene.TestState); a place built during a run stays built
        internal string[] StoryKeys { get { return new[] { KeyState, KeyNext, KeySub, KeyGate, KeyFire }; } }
        internal string MarkKey { get { return "loc_" + Id; } }

        protected string Rpc(string what) { return "RavenWatch_Loc_" + Id + "_" + what; }

        internal string WordOf(int s) { return s >= 1 && s <= Words.Length ? Words[s - 1] : s.ToString(); }
        internal int SubsOf(int s) { return s >= 1 && s <= Subs.Length ? Subs[s - 1].Length : 0; }
        internal string SubWord(int s, int sub) { return sub >= 1 && sub <= SubsOf(s) ? Subs[s - 1][sub - 1] : sub.ToString(); }
        internal string WordOf(int s, int sub) { return sub <= 0 ? WordOf(s) : WordOf(s) + " · " + SubWord(s, sub); }

        // «litfrom: 1.1 hearth»: these burn in its ruin from that step on (that state's later steps, and later states)
        internal struct LitStep { internal int State, Sub; internal string[] Prefabs; }
        internal bool LitAt(string prefab, int state, int sub)
        {
            foreach (LitStep f in LitFrom)
                if ((state > f.State || (state == f.State && sub >= f.Sub)) && Array.IndexOf(f.Prefabs, prefab) >= 0) return true;
            return false;
        }
    }
}
#endif
