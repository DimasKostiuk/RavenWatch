#if RAVEN_ITEM && RAVEN_STORY
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A scene of one's own (a scene's «alone: yes» — the personal prologue, the user 2026-11-03): while it plays, the
    // other players are not drawn in this game at all — their bodies, gear, their ravens (on the shoulder or flown off),
    // their names and their chat over their heads — so nobody can walk into the shot and spoil it. Only the personal
    // prologue's scenes say it (other scenes keep their own rules). Only drawn: they are there for the world, the scene still
    // stops on a blow as any scene does. When it ends or stops, everything put out is put back.
    // Twice a second while such a scene plays (new players, new gear), the other players' renderers only.
    internal static class StoryAlone
    {
        private static readonly List<Renderer> _off = new List<Renderer>();
        private static bool _on;
        private static float _next;


        internal static void Begin() { _on = true; _next = 0f; Tick(); }

        internal static void End()
        {
            if (!_on) return;
            _on = false;
            foreach (Renderer r in _off) if (r != null) r.enabled = true;
            _off.Clear();
        }

        // from StoryScenes.Tick, every frame while it plays (the look itself: twice a second)
        internal static void Tick()
        {
            if (!_on || Time.time < _next) return;
            _next = Time.time + 0.5f;
            Player me = Player.m_localPlayer;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me) continue;
                Hide(o.gameObject);
                Hide(Shoulder.DrawnOf(o, false)); Hide(Shoulder.DrawnOf(o, true)); // (their ravens, flown off the shoulder too)
            }
        }

        private static void Hide(GameObject g)
        {
            if (g == null) return;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) // heavy: twice a second, only while a scene of one's own plays, the other players and their ravens only
                if (r.enabled) { r.enabled = false; _off.Add(r); }
        }

        // what another player writes or shouts, over his head (the game's Chat world texts): put out too, every frame
        private static readonly FieldInfo FTexts = AccessTools.Field(typeof(Chat), "m_worldTexts");
        private static FieldInfo _talker, _textGui;
        internal static void Words(Chat chat)
        {
            if (!_on || FTexts == null || ZNet.instance == null) return;
            IList texts = FTexts.GetValue(chat) as IList;
            if (texts == null) return;
            long me = ZNet.GetUID();
            foreach (object t in texts)
            {
                if (t == null) continue;
                if (_talker == null) { _talker = AccessTools.Field(t.GetType(), "m_talkerID"); _textGui = AccessTools.Field(t.GetType(), "m_gui"); }
                if (_talker == null || _textGui == null || (long)_talker.GetValue(t) == me) continue;
                GameObject g = _textGui.GetValue(t) as GameObject;
                if (g != null && g.activeSelf) g.SetActive(false);
            }
        }

        // the name over another player's head (the game's EnemyHud): put out too, every frame while it plays
        private static readonly FieldInfo FHuds = AccessTools.Field(typeof(EnemyHud), "m_huds");
        private static FieldInfo _gui;
        internal static void Names(EnemyHud hud)
        {
            if (!_on || FHuds == null) return;
            IDictionary huds = FHuds.GetValue(hud) as IDictionary;
            if (huds == null) return;
            Player me = Player.m_localPlayer;
            foreach (DictionaryEntry e in huds)
            {
                Player o = e.Key as Player;
                if (o == null || o == me || e.Value == null) continue;
                if (_gui == null) _gui = AccessTools.Field(e.Value.GetType(), "m_gui");
                GameObject g = _gui != null ? _gui.GetValue(e.Value) as GameObject : null;
                if (g != null && g.activeSelf) g.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(Chat), "LateUpdate")]
    internal static class StoryAloneChatPatch
    {
        private static void Postfix(Chat __instance)
        {
            try { StoryAlone.Words(__instance); }
            catch (System.Exception e) { PatchGuard.Fail("StoryAloneChatPatch", e); }
        }
    }

    [HarmonyPatch(typeof(EnemyHud), "LateUpdate")]
    internal static class StoryAloneHudPatch
    {
        private static void Postfix(EnemyHud __instance)
        {
            try { StoryAlone.Names(__instance); }
            catch (System.Exception e) { PatchGuard.Fail("StoryAloneHudPatch", e); }
        }
    }
}
#endif
