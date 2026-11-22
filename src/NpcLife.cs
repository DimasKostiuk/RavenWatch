#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Оживлення», the first set (the user 2026-11-03): a resident's day answers what happens round it. Each is a duty of
    // the moment (NpcMind.TakeWhim with an owner of its own: over a leisure whim, never over another duty — a meal —, under
    // orders, a fight, a scene), always with a term:
    //   rain     it rains on it out in its base: to the nearest thing of its base under a roof (a fire, a chest, a bench,
    //            a bed…; not in a player's own house: NpcPrivate) — on a free seat there now and then, a lazy one more often
    //            (the user 2026-11-03) —, a grumble once. A lazy or an old one nearly always; a
    //            diligent one at its work now and then works on wet (decided once a shower). Back to its day when it stops
    //            (or after RainMost). One it sent from the rain shakes the water off its face and hand once it stands there (Shake: the
    //            clip «rain_shake» through its gate; not seated — the user 2026-11-18, the player's own move in the rain)
    //   raid     the game's raid round its base: a peaceful one (not a guard, not a fighter: Guard.Is) runs under a roof the
    //            same way — the doors it opens on its way it shuts behind it (NpcDoors). After it a sigh; whoever mends,
    //            mends (NpcWhims «mend»); a bold or grumpy fighter may hold it against one who hid (Blamed: NpcAttitude.Quarrel,
    //            and a small line begins: NpcArcs); one in such a line who swore not to hide stays out (NpcArcs.Stands)
    //   back     you come back after long (NpcVoice «back»): a free one near turns to you — a chatty one waves, a quiet one
    //            nods (its words are NpcVoice's)
    //   morning  once a morning, in its first hour, the residents of a base with a heart gather round it for a minute,
    //            looking at it (a lazy one may oversleep it); a chatty one says a word for the day
    // Looked at every LookEvery s a person, where it is run (NpcActor.Ticks); what it knows of a shower, a raid, a morning
    // lives a session only (nothing of it outlives the moment).
    internal static class NpcLife
    {
        internal const string Rain = "rain", Raid = "raid", Back = "back", Morning = "morning";
        internal static float LookEvery = 3f, RainMost = 240f, RaidMost = 300f, GatherFor = 70f;

        private sealed class State { public float Next; public NpcResident R; public int Shower = -1, Told = -1; public bool Hardy, InRaid, NoRoof, Wet; public long Gathered = -1; }
        private static readonly Dictionary<NpcActor, State> States = new Dictionary<NpcActor, State>(); // session: what it does now, off when it goes (Forget)
        private static int _shower; private static bool _wet; // (a shower counted: each one decided anew)

        internal static void Forget(NpcActor a) { States.Remove(a); }

        // in the rain the day of one at its ease is NpcLife's (to a roof, or waits there): nobody else sends it out (NpcPlaces asks). A guard
        // at its post: not its
        internal static bool RainHolds(NpcActor a)
        {
            if (EnvMan.instance == null || !EnvMan.IsWet() || Guard.OnWatch(a)) return false;
            State s; return !(States.TryGetValue(a, out s) && s.NoRoof && s.Shower == _shower); // (no roof found for it this shower: its own day goes on, not stood in the rain)
        }

        // where it is run, every frame: its own look every LookEvery s
        internal static void Tick(NpcActor a)
        {
            State s;
            if (!States.TryGetValue(a, out s)) { s = new State { Next = Time.time + Random.Range(0f, LookEvery) }; States[a] = s; }
            if (Time.time < s.Next) return;
            s.Next = Time.time + LookEvery;
            if (a.Mind == null || !NpcResident.Is(a)) return;
            if (s.R == null) { s.R = a.GetComponent<NpcResident>(); if (s.R == null) return; }
            NpcBase b = s.R.Base;
            if (b == null || !b.Ready) return;
            bool wet = EnvMan.instance != null && EnvMan.IsWet();
            if (wet && !_wet) _shower++;
            _wet = wet;
            if (RaidNow(s.R.Home)) { Hide(a, s, b); return; }
            if (s.InRaid) After(a, s);
            if (RainHolds(a)) Shelter(a, s, b); else { a.Mind.EndWhim(Rain); s.Wet = false; } // (the rain over before it got there: nothing to shake off)
            Gather(a, s);
        }

        // under a roof out of the rain: the water off its face and hand (its own life's move: the gate — its body, «shuns:»). true: done with —
        // shaken off, or it sat down there (no standing move on a seat); false: still on its feet coming, the next look
        internal static string ShakeClip = "rain_shake";
        internal static float ShakeNear = 1.5f;
        private static bool Shake(NpcActor a)
        {
            NpcMind m = a.Mind;
            if (m == null || a.Body == null || m.SeatedNow || m.NowLayer >= NpcMind.Layer.Fight) return true;
            Character c = a.GetComponent<Character>();
            if ((c != null && c.GetVelocity().magnitude > 0.3f) || !a.Body.ClipFree) return false;
            string clip = NpcMoves.Pick(a, ShakeClip);
            if (clip.Length > 0 && a.Body.Clip(clip)) Npcs.Log(a.Who + ": rain — shakes the water off");
            return true;
        }

        // ---------------------------------------------------------------- a raid
        private static bool RaidNow(Vector3 home)
        {
            RandomEvent ev = RandEventSystem.instance != null ? RandEventSystem.instance.GetActiveEvent() : null;
            return ev != null && Utils.DistanceXZ(ev.m_pos, home) < ev.m_eventRange + 30f; // (as its words hear it: NpcNews)
        }

        private static void Hide(NpcActor a, State s, NpcBase b)
        {
            if (!a.Def.Settler || Guard.Is(a) || NpcNature.Fighter(a) || NpcArcs.Stands(a)) return; // (a person of the story is the story's; a fighter goes out to them: its fight; one who swore not to hide again stays: NpcArcs)
            if (!s.InRaid) { s.InRaid = true; NpcPhrases.SayOr(a, "raid_hide", a.Form(Lang.T("Ховаймося! Швидше в дім!")), null, NpcActor.Voice.Unasked); NpcFeel.Feel(a, NpcFeel.Fright, 60f, a.transform.position, false); }
            NpcMind m = a.Mind;
            NpcMind.Plan w = m.Of(NpcMind.Layer.Whim);
            if (w != null && w.Whose == Raid) return; // (on its way in, or there already)
            Vector3 at; bool seat;
            if (!NpcPlaces.Roofed(a, b, out at, out seat)) return;
            if (m.TakeWhim(new NpcMind.Plan { Kind = NpcMind.Task.Go, To = at, Near = 0.8f, Run = true }, RaidMost, Raid, true)) Npcs.Log(a.Who + ": a raid — hides under a roof");
        }

        private static void After(NpcActor a, State s)
        {
            s.InRaid = false;
            a.Mind.EndWhim(Raid);
            if (Random.value < 0.6f) NpcPhrases.SayOr(a, "raid_over", a.Form(Lang.T("Ух… обійшлося.")), null, NpcActor.Voice.Unasked);
            Blamed(a);
        }

        // one who hid, now and then held against it by a fighter of its base who fought it off (a bold or a grumpy one: NpcAttitude.Quarrel
        // «raid»); who fought: its memory of a fight at home just now (NpcMemory, read on any game)
        internal static float BlameChance = 0.35f, FoughtWithin = 600f;
        private static void Blamed(NpcActor a)
        {
            if (Random.value > BlameChance) return;
            foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart)))
            {
                if (o == null || o == a || (!Guard.Is(o) && !NpcNature.Fighter(o))) continue;
                NpcMemory.Entry e = NpcMemory.Find(o, "fight_won", 0L) ?? NpcMemory.Find(o, "fight_lost", 0L);
                if (e == null || NpcRescue.Now - e.At > FoughtWithin) continue;
                HashSet<string> t = NpcPhrases.Tones(o);
                if (!t.Contains("смілив") && !t.Contains("бурч")) continue;
                NpcAttitude.Quarrel(o, a, "raid");
                NpcArcs.Begin(a, o, "hidden", "hidden_ashamed"); // (it may become a small line of its own)
                return;
            }
        }

        // ---------------------------------------------------------------- rain
        private static void Shelter(NpcActor a, State s, NpcBase b)
        {
            NpcMind m = a.Mind;
            NpcMind.Plan w = m.Of(NpcMind.Layer.Whim);
            if (w != null && w.Whose == Rain) { if (s.Wet && Utils.DistanceXZ(a.transform.position, w.To) < ShakeNear && Shake(a)) s.Wet = false; return; } // (sheltering already; come out of the rain and standing there: the water shaken off, once)
            if (m.NowLayer > NpcMind.Layer.Whim || m.Asleep || !m.WhimFree(Rain)) return; // (an order, a fight, a scene; in bed it stays)
            if (NpcToil.Roof(a.transform.position + Vector3.up * 1.6f)) { Why(a, s, "dry where it is"); return; } // (dry where it is; on a seat there too it sits on) // heavy: the game's cover rays, every LookEvery s a person — only in rain, only one out in it with its whim free
            if (s.Shower != _shower)
            {
                s.Shower = _shower; s.NoRoof = false; // (once a shower: does it mind?)
                NpcPersona p = NpcPersona.Of(a);
                NpcSettlers.Look look = NpcSettlers.Of(a);
                bool old = look != null && look.Age == 2;
                s.Hardy = !old && m.NowLayer == NpcMind.Layer.Job && Random.value < Mathf.Clamp01(p.Diligence - p.Laziness);
            }
            if (s.Hardy && m.NowLayer == NpcMind.Layer.Job) { Why(a, s, "hardy: works on wet"); return; } // (works on wet)
            Vector3 at; bool seat; string why;
            if (!NpcPlaces.Roofed(a, b, out at, out seat, out why)) { s.NoRoof = true; Why(a, s, why); return; }
            bool sit = seat && Random.value < 0.35f + NpcPersona.Of(a).Laziness * 0.5f; // (a seat under the roof: sits it out — the seat itself found free by «sit», else the ground by it)
            if (!m.TakeWhim(new NpcMind.Plan { Kind = sit ? NpcMind.Task.Sit : NpcMind.Task.Go, To = at, Near = 0.8f }, RainMost, Rain)) return;
            Npcs.Log(a.Who + ": rain — goes under a roof");
            s.Wet = true; // (it walks there through the rain: wet)
            if (Random.value < 0.3f + NpcPersona.Of(a).Chatty * 0.5f) NpcPhrases.SayOr(a, "rain", a.Form(Lang.T("Знову ллє…")), null, NpcActor.Voice.Unasked); // (its words: the theme in NpcPhrases)
        }

        // why it does not go under a roof, once a shower a person (the log: «it stood in the rain», 2026-11-04)
        private static void Why(NpcActor a, State s, string why)
        {
            if (s.Told == _shower) return;
            s.Told = _shower;
            Npcs.Log(a.Who + ": rain — stays (" + why + ")");
        }

        // ---------------------------------------------------------------- you, back after long (NpcVoice «back»)
        internal static void Welcome(NpcActor a, Player p)
        {
            NpcMind m = a != null ? a.Mind : null;
            if (m == null || p == null || m.NowLayer > NpcMind.Layer.Whim || m.SeatedNow || m.Asleep || !m.WhimFree(Back)) return;
            if (!m.TakeWhim(new NpcMind.Plan { Kind = NpcMind.Task.Idle, To = a.transform.position, Looks = true, Look = p.transform.position + Vector3.up * 1.6f }, 4f, Back)) return;
            if (NpcAttitude.Of(a, p.GetPlayerID()) >= 25f) NpcFeel.Feel(a, NpcFeel.Joy, 45f, p.transform.position, false); // (glad you are back: one who is warm to you)
            Npcs.Log(a.Who + ": welcomes you back");
            if (a.Body == null) return;
            if (NpcPersona.Of(a).Chatty >= 0.5f) a.Body.Gesture("wave", false); // (the game's wave: every game sees it)
            else a.Body.Move("fire_nod_l|fire_nod_r");                         // (a nod: ours)
        }

        // ---------------------------------------------------------------- the morning by its heart
        private static void Gather(NpcActor a, State s)
        {
            if (EnvMan.instance == null || Guard.OnWatch(a)) return; // (one on watch stays at its post)
            float hour = EnvMan.instance.GetDayFraction() * 24f;
            if (hour < 6f || hour >= 7f) return;
            long day = EnvMan.instance.GetDay();
            if (s.Gathered == day) return;
            NpcMind m = a.Mind;
            if (m.NowLayer > NpcMind.Layer.Job || m.Asleep || !m.WhimFree(Morning)) return; // (still asleep: it comes when it is up — or misses it)
            s.Gathered = day;
            NpcPersona p = NpcPersona.Of(a);
            if (Random.value < p.Laziness * 0.5f) return; // (overslept it)
            ZDOID hid = NpcKit.GetRef(a.Zdo, NpcResident.KHeart);
            ZDO heart = !hid.IsNone() && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(hid) : null;
            if (heart == null) return;
            Vector3 c = heart.GetPosition();
            // its own place in an even ring: the residents of this heart in the order of their seeds, ~1.4 m apart (the user
            // 2026-11-03: they stood too close), 3.5 m round at least
            List<uint> ring = new List<uint>();
            foreach (NpcActor o in NpcActor.Live) if (o != null && o.Zdo != null && NpcResident.Is(o) && NpcKit.GetRef(o.Zdo, NpcResident.KHeart) == hid) ring.Add(NpcKit.Seed(o.Zdo)); // (the people loaded here: a handful)
            ring.Sort();
            int slot = Mathf.Max(0, ring.IndexOf(NpcKit.Seed(a.Zdo))), n = Mathf.Max(1, ring.Count);
            float ang = slot * Mathf.PI * 2f / n, radius = Mathf.Max(3.5f, n * 1.4f / (Mathf.PI * 2f));
            Vector3 by = c + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * radius, v;
            Vector3 at = NpcKit.Valid(out v, by, 1.5f, NpcKit.Walker) ? v : by;
            if (!m.TakeWhim(new NpcMind.Plan { Kind = NpcMind.Task.Go, To = at, Near = 0.8f, Looks = true, Look = c + Vector3.up * 1f }, GatherFor, Morning)) return;
            Npcs.Log(a.Who + ": the morning — to its heart");
            if (Random.value < p.Chatty * 0.6f) NpcPhrases.SayOr(a, "morning", a.Form(Lang.T("Ну що, з чого почнемо день?")), null, NpcActor.Voice.Unasked); // (its words: the theme in NpcPhrases)
        }
    }
}
#endif
