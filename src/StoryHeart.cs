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
    //   world heart_spark     the hearts' lanterns burn (act 1: the Hall's spark brought to one — the user 2026-11-03); till then the first
    //                         heart only flashes once (the scene step «heart flare»)
    //   place heart.near      the heart nearest to you (a scene's camera, the bird)
    internal static class StoryHeart
    {
        internal const string First = "heart_first", Found = "heart_found", Built = "heart_built", Spark = "heart_spark";
        private static readonly MethodInfo AddKnown = AccessTools.Method(typeof(Player), "AddKnownPiece");

        internal static bool Open(Player p) { return StoryWorld.Flag(First) || StoryWorld.Flag(Found); }

        // twice a second (StoryQuests.Each): learnt in this world, and this game's player has not its piece yet
        internal static void Look(Player p) { if (StoryWorld.Flag(Found)) GivePiece(p); }

        // the module's words: the action «heartlearn», the place «heart.near», the hearts' lantern
        internal static void Actions()
        {
            StoryActions.Register("heartlearn", new StoryActions.Kind { Min = 0, Usage = "heartlearn   (серце оселі відкривається всім у цьому світі)",
                Do = (p, q, w, rest) => { Learn(p); return null; } });
            NpcHeart.LampLit = () => StoryWorld.Flag(Spark);
            StoryWhere.Prefixed["heart."] = (string rest, out Vector3 at) => { NpcHeart h = Nearest(Player.m_localPlayer); at = h != null ? h.transform.position + Vector3.up * 1.5f : Vector3.zero; return h != null; };
        }

        internal static void Steps()
        {
            StoryScenes.Register("heart", new StoryScenes.StepKind(StoryScenes.StepSeen.World) { Min = 1, Usage = "heart flare [секунд]   (у ліхтарі найближчого серця спалахує вогонь і одразу гасне)",
                Check = st => st.W[1] != "flare" ? "heart flare [секунд]" : null,
                Begin = (p, st) =>
                {
                    NpcHeart h = Nearest(p);
                    float s;
                    if (h == null) { StoryScenes.Note("heart flare: серця поруч нема"); return; }
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

        // a heart placed by you (Diary.Placed, the one hook on building): yours, the world's first if none stood, and the story hears it
        internal static void Placed(Player p, Piece piece)
        {
            if (!IsHeart(piece)) return;
            StoryWorld.SetFlag(First, true);
            StoryBus.Raise(Built, "", 0, p.transform.position);
        }

        // the server, now and then: a world whose hearts stood before this word (an older build) gets it — once, then never asked again
        private static float _nextLook;
        internal static void ServerTick()
        {
            if (Time.time < _nextLook || ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            _nextLook = Time.time + 30f; // (the census of the world's hearts: not often, and only till the word is there)
            if (StoryWorld.Flag(First) || !NpcHeart.AnyInWorld()) return;
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
