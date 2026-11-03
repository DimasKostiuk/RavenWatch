#if RAVEN_ITEM && RAVEN_STORY
// (the prologue's heart: from 2.9 — the user 2026-11-02)
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Серце оселі» in the main prologue (story-lore.md «Пролог»): till the first heart stands in this world nobody may
    // build one — but the one who found its old drawing in the Hall (HeartBoard). The first heart built ends the main
    // prologue for the whole server (world flag WorldDone, set by its quest); from then on the heart is learnt the game's
    // own way, by its materials (Eikthyr's antler among them).
    internal static class PrologueHeart
    {
        internal const string WorldDone = "prologue_done", Found = "heart_found", Built = "heart_built", Stage = "heart"; // (Stage: the main prologue's own, which waits for Built)
        private static readonly MethodInfo AddKnown = AccessTools.Method(typeof(Player), "AddKnownPiece");

        internal static bool Open(Player p) { return StoryWorld.Flag(WorldDone) || Story.Flag(p, Found); }

        // the quest's own word: «heartlearn» — the heart opens for this character (after the letter's scene)
        internal static void Actions()
        {
            StoryActions.Register("heartlearn", new StoryActions.Kind { Min = 0, Usage = "heartlearn   (серце оселі відкривається цьому гравцеві)",
                Do = (p, q, w, rest) => { Learn(p); return null; } });
        }

        internal static bool IsHeart(Piece piece)
        {
            if (piece == null) return false;
            string name = Utils.GetPrefabName(piece.gameObject);
            foreach (Perches.Def d in Perches.Hearts) if (d.Prefab == name) return true;
            return false;
        }

        // the drawing found: this character may build one (the game's own «new piece» message, as if learnt)
        internal static void Learn(Player p)
        {
            if (p == null) return;
            Story.SetFlag(p, Found, true);
            if (ZNetScene.instance == null || AddKnown == null) return;
            foreach (Perches.Def d in Perches.Hearts)
            {
                GameObject g = ZNetScene.instance.GetPrefab(d.Prefab);
                Piece piece = g != null ? g.GetComponent<Piece>() : null;
                if (piece != null && !p.IsRecipeKnown(piece.m_name)) AddKnown.Invoke(p, new object[] { piece });
            }
        }
    }

    // before the world's first heart: not in the hammer, not buildable — but for the one who found the drawing
    [HarmonyPatch(typeof(Player), "HaveRequirements", new System.Type[] { typeof(Piece), typeof(Player.RequirementMode) })]
    internal static class PrologueHeartGatePatch
    {
        private static void Postfix(Player __instance, Piece piece, ref bool __result)
        {
            try { if (__result && PrologueHeart.IsHeart(piece) && !PrologueHeart.Open(__instance)) __result = false; }
            catch (System.Exception e) { PatchGuard.Fail("PrologueHeartGatePatch", e); }
        }
    }

    // a heart placed by you: the story hears it (the main prologue's last stage — that stage marks the world itself, with
    // its storm). Built by one not on that stage (the letter read outside the prologue): the world's first heart all the
    // same, the main prologue ends for everyone without the storm
    [HarmonyPatch(typeof(Player), "PlacePiece")]
    internal static class PrologueHeartBuiltPatch
    {
        private static void Postfix(Player __instance, Piece piece)
        {
            try
            {
                if (__instance != Player.m_localPlayer || !PrologueHeart.IsHeart(piece)) return;
                StoryBus.Raise(PrologueHeart.Built, "", 0, __instance.transform.position);
                bool stage = Story.Quest(__instance, HeartBoard.Quest) == Story.Active && Story.StageOf(__instance, HeartBoard.Quest) == PrologueHeart.Stage;
                if (!stage) StoryWorld.SetFlag(PrologueHeart.WorldDone, true);
            }
            catch (System.Exception e) { PatchGuard.Fail("PrologueHeartBuiltPatch", e); }
        }
    }

    // the old letter on the Hall's upper floor (story piece kreslennia_sertsia): it glows while this character has not
    // read it (and the world has no heart yet). E — the letter's words; in the main prologue the story then plays its
    // scene (a raven's line) and only after it opens the heart (quest action «heartlearn»); anyone else reading it
    // outside that stage learns the heart at once
    internal sealed class HeartBoard : MonoBehaviour, Hoverable, Interactable
    {
        internal const string Event = "kreslennia_sertsia", Quest = "prologue_main", Stage = "board";
        private GameObject _glint;
        private float _next;

        private void Start() { _glint = WsItems.Glint(gameObject, 3f); }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 1f;
            Player p = Player.m_localPlayer;
            bool on = p != null && !PrologueHeart.Open(p);
            if (_glint != null && _glint.activeSelf != on) _glint.SetActive(on);
        }

        public string GetHoverName() { return Lang.T("Старий лист"); }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            string t = Lang.T("Старий лист") + "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Прочитати");
            return Localization.instance != null ? Localization.instance.Localize(t) : t;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (hold || p == null || p != Player.m_localPlayer) return false;
            if (TextViewer.instance != null)
                TextViewer.instance.ShowText(TextViewer.Style.Rune, Lang.T("Старий лист"), Lang.T(
                    "Стовп із вороном на верхівці, дошка під дашком, ліхтар. Такі ставили там, де жили люди: біля них збирались.\nПід кресленням дряпнуто: «дерево, пір'я, ріг того, хто перший упав»."), true);
            bool story = Story.Quest(p, Quest) == Story.Active && Story.StageOf(p, Quest) == Stage;
            if (!story) PrologueHeart.Learn(p); // (not the prologue's moment: no scene to wait for)
            StoryBus.Raise("use", Event, 1, transform.position);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
#endif
