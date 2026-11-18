#if RAVEN_ITEM && RAVEN_STORY
// (the hearts of a home in the story: from 2.9 — the user 2026-11-02; no story names in it since 2026-11-06)
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Серце оселі» and the story (story-lore.md «Пролог»): till the first heart stands in a world nobody may build one, till it is
    // learnt there (the action «heartlearn»: the prologue gives it after the Hall's old letter; locations.list «elsedo:» to whoever reads the
    // letter outside that moment). All of it is the server's, nothing in the character (the user 2026-11-07): the letter opens it for everyone in this
    // world, and the first heart built opens it the game's own way, by its materials (Eikthyr's antler among
    // them). Only the mechanics live here; what the story makes of it is in its data, which asks the module's own words:
    //   world heart_first     a heart stands in this world (set by the first one built, whoever built it; a world from before with a heart
    //                         gets it from the server once) — the main prologue's end for everyone
    //   world heart_found     the heart learnt in this world (heartlearn): every game here gets its piece (Look). The game writes a piece it knows into the
    //                         character, so on another server that piece is still kept off by Open, which asks only this world
    //   event heart_built     a heart was just built by you (a quest's «on:»)
    //   event heart_use       E on any heart, taken from its board only while a stage waits for it and would pass (its «wait:» holds:
    //                         chapter 1 reads the scroll by a heart, «wait: item …»); the hover's E then says that stage's goal
    //   a heart's lantern     dark till an ember of the Hall's fire is put in it (the user 2026-11-15: built is not enough, a heart is always lit):
    //                         E on a dark heart with an ember in the bag (the item a place's hearth gives: «hearthgives:», Embers) lights it for good
    //                         (NpcHeart.Light, its own KLit) and takes the ember — unless a stage takes that E first (chapter 1: its scene, «heart light»);
    //                         event heart_lit. A heart works as ever unlit: only its lantern is dark
    //   place heart.near      the heart nearest to you (a scene's camera, the bird)
    //   place heart.lamp      its lantern (NpcHeart.LampAt): a close shot of the lantern itself, a look at it
    //   place heart.first     the world's first heart, where it was built (world text «heart_first_at»: written as it is built; a world from
    //                         before gets one of its hearts from the server). Known on every game and on the server with no player: a story
    //                         person may live there («live: heart.first», chapter 1: Ketil comes to it)
    internal static class StoryHeart
    {
        internal const string First = "heart_first", Found = "heart_found", Built = "heart_built", FirstAt = "heart_first_at", Use = "heart_use";
        // the author took the first heart back for a test (StoryQuests.Rebuild → Taken): the census below must not set it again from the hearts that stand — till one is built
        internal const string HeldOff = "heart_first_off";
        private static readonly MethodInfo AddKnown = AccessTools.Method(typeof(Player), "AddKnownPiece");

        internal static bool Open(Player p) { return StoryWorld.Flag(First) || StoryWorld.Flag(Found); }

        // twice a second (StoryQuests.Each): learnt in this world, and this game's player has not its piece yet
        internal static void Look(Player p) { if (StoryWorld.Flag(Found)) GivePiece(p); }

        // the module's words: the action «heartlearn», the place «heart.near», the hearts' lantern
        internal static void Actions()
        {
            StoryActions.Register("heartlearn", new StoryActions.Kind { Dry = "set world world:heart_found", Min = 0, Usage = "heartlearn   (серце оселі відкривається всім у цьому світі)",
                Do = (p, q, w, rest) => { Learn(p); return null; } });
            StoryQuests.Taken[First] = on => StoryWorld.SetFlag(HeldOff, !on); // (the author's jump to before the first heart, in a world where a heart stands)
            NpcHeart.LampLit = h => h.Lit;
            // E on a heart a stage waits for («on: heart_use», chapter 1: the scroll read by a heart): the event, not the board; its hover says the
            // stage's goal. Only while that stage would pass on it (StoryQuests.Awaiting: its «wait:» too) — else E opens the board as ever
            NpcHeart.UseInstead.Add((h, p) =>
            {
                if (p == null || p != Player.m_localPlayer || !StoryQuests.Awaits(p, Use, "")) return null;
                StoryBus.Raise("heart_use", "", 0, h.transform.position); // (= Use, written out: tools/quest_dry.js finds who raises an event by this literal)
                return true;
            });
            NpcHeart.HoverInstead.Add((h, p) =>
            {
                StoryQuests.StageDef s = p != null ? StoryQuests.Awaiting(p, Use, "") : null;
                return s != null ? StoryQuests.GoalText(s) : null;
            });
            // E on a dark heart with an ember in the bag: lit for good (after the stage's own E above: chapter 1 plays its scene instead)
            NpcHeart.UseInstead.Add((h, p) =>
            {
                string ember = h.Lit ? null : EmberIn(p); // (its prefab)
                if (ember == null) return null;
                Light(h, p, ember);
                return true;
            });
            NpcHeart.HoverInstead.Add((h, p) => !h.Lit && EmberIn(p) != null ? Lang.T("Розпалити Жариною") : null);
            StoryWhere.WorldKept.Add("heart.first"); // (the world's: a person may live there; «heart.near» is the player's)
            StoryWhere.Prefixed["heart."] = (string rest, out Vector3 at) =>
            {
                if (rest == "first") return FirstHeart(out at); // (the world's: no player needed)
                NpcHeart h = Nearest(Player.m_localPlayer);
                at = h == null ? Vector3.zero : rest == "lamp" ? h.LampAt : h.transform.position + Vector3.up * 1.5f;
                return h != null;
            };
        }

        // the ember in this player's bag: its prefab — any of those a place's hearth gives («hearthgives:»); null: none
        internal static string EmberIn(Player p)
        {
            if (p == null) return null;
            foreach (Loc l in Locs.All)
            {
                string name = l.HearthGives.Length > 0 ? LocHearth.ItemName(l.HearthGives) : null;
                if (name != null && p.GetInventory().CountItems(name) > 0) return l.HearthGives;
            }
            return null;
        }

        // the ember put in a heart's lantern: taken from the bag (ItemMarks.Take: a story thing given on purpose, not put back), the heart lit
        // for good, the story told (event heart_lit). A heart lit already takes none
        internal static void Light(NpcHeart h, Player p, string ember)
        {
            if (h == null || p == null || h.Lit) return;
            if (!ItemMarks.Take(p, ember, 1)) return;
            h.Light();
            p.Message(MessageHud.MessageType.Center, Lang.T("Серце оселі розпалене."));
            StoryBus.Raise("heart_lit", "", 0, h.transform.position);
        }

        // where the world's first heart was built (false: none yet)
        internal static bool FirstHeart(out Vector3 at) { return StoryWorld.Pos(FirstAt, out at); }
        // its place really kept: not the «0 0 0» a build before 2026-11-16 wrote (the server takes one of the world's hearts again: ServerTick)
        private static bool FirstKept() { Vector3 at; return StoryWorld.Text(FirstAt).Length > 0 && StoryWorld.Pos(FirstAt, out at) && at.sqrMagnitude > 0.01f; }
        private static void SetFirstAt(Vector3 at) { StoryWorld.SetPos(FirstAt, at); }

        internal static void Steps()
        {
            StoryScenes.Register("heart", new StoryScenes.StepKind(StoryScenes.StepSeen.World) { Min = 1, Usage = "heart flare|light [секунд]   (flare: у ліхтарі найближчого серця спалахує вогонь і одразу гасне; light: Жарина з торби — в ліхтар, горить відтепер завжди)",
                Check = st => st.W[1] != "flare" && st.W[1] != "light" ? "heart flare|light [секунд]" : null,
                Begin = (p, st) =>
                {
                    NpcHeart h = Nearest(p);
                    float s;
                    if (h == null) { StoryScenes.Note("heart " + st.W[1] + ": серця поруч нема"); return; }
                    if (st.W[1] == "light") { if (h.Lit) { StoryScenes.Note("heart light: це серце вже розпалене — Жарина лишається"); h.Flare(2.5f); return; } string ember = EmberIn(p); if (ember == null) { StoryScenes.Note("heart light: Жарини в торбі нема"); h.Flare(2.5f); return; } Light(h, p, ember); return; }
                    h.Flare(st.W.Length > 2 && float.TryParse(st.W[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s) ? s : 2.5f);
                } });
        }

        // the loaded heart nearest to the player (40 m at most)
        private static NpcHeart Nearest(Player p)
        {
            if (p == null) return null;
            NpcHeart best = null;
            float bd = 40f * 40f;
            foreach (NpcHeart h in NpcHeart.Loaded)
            {
                if (h == null) continue;
                float d = (h.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }

        // a heart placed by you (Diary.Placed, the one hook on building): yours, the world's first if none stood, and the story hears it.
        // from: what it was made from — the game's own piece of that kind, standing nowhere; made: the heart put down (null: the game made none — where you stand).
        // (Till 2026-11-16 the place was read off «from»: «heart_first_at = 0 0 0» — Ketil came to the middle of the world, his mark on the bosses' stones)
        internal static void Placed(Player p, Piece from, Piece made)
        {
            if (!IsHeart(from)) return;
            if (!StoryWorld.Flag(First)) SetFirstAt(made != null ? made.transform.position : p.transform.position); // (the world's first: where it stands)
            StoryWorld.SetFlag(First, true);
            StoryWorld.SetFlag(HeldOff, false); // (a heart really built: the author's hold is over)
            StoryBus.Raise(Built, "", 0, p.transform.position);
        }

        // the server, now and then: a world whose hearts stood before this word (an older build) gets it — once, then never asked again
        private static float _nextLook;
        internal static void ServerTick()
        {
            if (Time.time < _nextLook || ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            _nextLook = Time.time + 30f; // (the census of the world's hearts: not often, and only till the words are there)
            Vector3 at;
            if (StoryWorld.Flag(HeldOff)) return; // (the author holds the story before the first heart: the hearts that stand are not counted)
            if (StoryWorld.Flag(First) && FirstKept()) return;
            if (!NpcHeart.AnyInWorld(out at)) return;
            if (!FirstKept()) { SetFirstAt(at); Story.Log("heart: the world's first heart taken as the one at " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z)); } // (built before its place was kept)
            if (StoryWorld.Flag(First)) return;
            StoryWorld.SetFlag(First, true);
            Story.Log("heart: this world had a heart already — «" + First + "» set");
        }

        internal static bool IsHeart(Piece piece)
        {
            if (piece == null) return false;
            string name = Utils.GetPrefabName(piece.gameObject);
            foreach (Perches.Def d in Perches.Hearts) if (d.Prefab == name) return true;
            return false;
        }

        // the heart learnt: everyone in this world may build one
        internal static void Learn(Player p)
        {
            StoryWorld.SetFlag(Found, true);
            GivePiece(p);
        }

        // its piece, for this game's player (the game's own «new piece» message, as if learnt)
        private static void GivePiece(Player p)
        {
            if (p == null || ZNetScene.instance == null || AddKnown == null) return;
            foreach (Perches.Def d in Perches.Hearts)
            {
                GameObject g = ZNetScene.instance.GetPrefab(d.Prefab);
                Piece piece = g != null ? g.GetComponent<Piece>() : null;
                if (piece != null && !p.IsRecipeKnown(piece.m_name)) AddKnown.Invoke(p, new object[] { piece });
            }
        }
    }

    // before the world's first heart, or its learning in this world: not in the hammer, not buildable
    [HarmonyPatch(typeof(Player), "HaveRequirements", new System.Type[] { typeof(Piece), typeof(Player.RequirementMode) })]
    internal static class StoryHeartGatePatch
    {
        private static void Postfix(Player __instance, Piece piece, ref bool __result)
        {
            try { if (__result && StoryHeart.IsHeart(piece) && !StoryHeart.Open(__instance)) __result = false; }
            catch (System.Exception e) { PatchGuard.Fail("StoryHeartGatePatch", e); }
        }
    }
}
#endif
