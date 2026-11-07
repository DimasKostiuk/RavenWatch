#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // You stand before a person with its talk open and say nothing (the user 2026-11-05: it is a living person, it will not just wait).
    //   AlertAt s with no click   over the window, its words (RavenBook.ShowTalkAlert), the parchment under them out of focus and its answers not to be clicked — a few seconds, hard
    //                             limited there, so no click meant to wave them away picks «Бувай»; Esc and the ✕ work as ever. It is cross (Irritate: AlertIrk, shared as any anger,
    //                             NpcPatience) and it will remember it (NpcMemory «zoned_out»: its next talk opens with that, answers in «c_mem_zoned_out», any of them lets it go)
    //   every AlertEvery s        other, harder words (idle_alert_more), a little crosser (MoreIrk)
    //   a click on an answer      once its words are gone: all gone, and its first words are about you coming round (idle_back). A click while they are up does nothing, but counts: you are here (IdleStir)
    //   IdleFor s                 it has had enough: a word over its head (idle_gone), crosser still (GoneIrk), the window closes
    // Silence means nothing at all: the mouse moved more than StirMoved, a wheel, a key, a click anywhere count as you being here (a player reading is no player gone; its words already up stay their few seconds).
    // Its anger is summed while it lasts and said once when the silence ends (_silenceIrk: anger wears off with time, a little at a time it would never grow). One long silence stays under NpcPatience.Gate (it does not
    // shut anyone's talk with it), a second soon after does not.
    // Words: idle_alert, idle_alert_more (each «_close» first to one dear), idle_gone, idle_back, mem_zoned_out, zoned_*. A person with no words for it (the story's, own words only:
    // add [id:idle_alert]) shows nothing, is not cross and only ends the talk at IdleFor; a talk with no patience of its own (not Minded): no anger, no memory. One in trouble (NpcRescueTalk) begs,
    // it does not take offence: nothing either, only the end at IdleFor.
    internal static partial class NpcTalk
    {
        internal static float AlertAt = 180f, IdleFor = 300f, AlertEvery = 35f, AlertIrk = 4f, MoreIrk = 2f, GoneIrk = 6f;
        internal const string ZonedOut = "zoned_out";
        internal static float StirMoved = 4f; // px
        private static bool _alerted;
        private static float _nextAlert, _silenceIrk;
        private static Vector3 _mouseAt;

        // from Tick: true when it ended the talk
        private static bool IdleTick(Player p)
        {
            Vector3 m = Input.mousePosition;
            if ((m - _mouseAt).sqrMagnitude > StirMoved * StirMoved || Input.anyKeyDown || Input.mouseScrollDelta.y != 0f) { _mouseAt = m; _touched = Time.time; } // (one float compare a frame, only while a talk is open)
            float idle = Time.time - _touched;
            if (idle > IdleFor) { Gone(p); return true; }
            if (idle < AlertAt || Time.time < _nextAlert) return false;
            _nextAlert = Time.time + AlertEvery;
            string line = NpcRescueTalk.State(With) != null ? null : IdleLine(_alerted ? "idle_alert_more" : "idle_alert", p);
            if (line == null) { _nextAlert = float.MaxValue; return false; } // (no words for it: it only ends the talk)
            bool first = !_alerted;
            _alerted = true;
            RavenBook.ShowTalkAlert(With.Name, NpcBody.Spoken(With, (first ? "{~shrug} " : "") + line).Trim());
            if (!Minded) return false;
            _silenceIrk += first ? AlertIrk : MoreIrk;
            if (first) NpcMemory.Tell(With, ZonedOut, p.GetPlayerID(), "");
            return false;
        }

        private static void Gone(Player p)
        {
            NpcActor a = With;
            if (Minded && NpcRescueTalk.State(a) == null)
            {
                _silenceIrk += GoneIrk;
                string line = NpcPhrases.Compose(a, "idle_gone", p);
                if (!string.IsNullOrEmpty(line)) a.Say(line, p); // (asked for, by you: not its unasked words — NpcVoice.Free)
            }
            Close();
        }

        // to one dear, its softer words first
        private static string IdleLine(string theme, Player p)
        {
            string s = NpcAttitude.Level(With, p) == "close" ? NpcPhrases.Compose(With, theme + "_close", p) : null;
            if (string.IsNullOrEmpty(s)) s = NpcPhrases.Compose(With, theme, p);
            return string.IsNullOrEmpty(s) ? null : s;
        }

        // a click while its words are up: nothing happens, but you are here: no more of them for now, and it does not walk off
        internal static void IdleStir() { _touched = Time.time; }

        // any click: you are back. Its words about it, to go before the place's own (null: you were not away)
        private static string IdleBack(Player p)
        {
            _touched = Time.time;
            if (!_alerted) return null;
            IdleOff();
            string s = NpcPhrases.Compose(With, "idle_back", p);
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static void IdleOff()
        {
            if (_silenceIrk > 0f && With != null && Minded) NpcPatience.Irritate(With, Player.m_localPlayer, _silenceIrk); // (the silence is over: its anger for all of it at once)
            _silenceIrk = 0f;
            _alerted = false; _nextAlert = 0f;
            RavenBook.HideTalkAlert();
        }
    }
}
#endif
