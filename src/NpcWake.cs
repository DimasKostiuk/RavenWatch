#if RAVEN_ITEM && RAVEN_NPC
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // Waking a sleeper (the user 2026-11-12: one asleep is not talked to — E opened its talk and it answered lying in its bed; «додати кнопку розбудити Q
    // чітко коли дивимось на нього. Тоді він прокидається злим і даємо мінус до настрою і відносин»). Pointed at, one asleep says «(спить)» and what Q does:
    // it wakes it, at once, with no ring (no talk and no orders to one asleep: woken first, then both as ever). It wakes cross, and that is all kept
    // by what the core already has:
    //   up        a whim of its duty (Whose, AwakeFor s): off its bed this step (NpcMind.Wake), it stands and looks at who woke it; over, its night goes on
    //             by itself — back to its bed (NpcResident's routine under the whim)
    //   words     the theme «woken» at once, «mem_woken» now and then after (NpcMoreWakePatterns.cs)
    //   to you    its memory «woken»: colder to the one who did it (NpcMemory: Toward, once a game day as every memory), a line against you on its card;
    //             its irritation (NpcPatience «irk»: once it is cross, twice in a row its talk is shut to you — «розізлився саме на тебе»)
    //   its mood  a share of it for MoodHours game hours after (NpcToil.MoodFrom): «розбудили серед сну» on its card, with every other reason
    // Who: one who lives at a base, asleep by its own night, whom this player may order (NpcMenu.MayOrder). Asleep under an order or a scene, or a
    // person of the story that lives nowhere: left as before — its talk may be what a stage waits for. Asleep on any game: NpcMind.AsleepOf.
    // Done where it is run (its act «wake <player>»), in the name of the one who pressed (NpcActor.SentBy).
    internal static class NpcWake
    {
        internal const string Whose = "woken";
        internal static readonly int KWokenAt = "RavenWatch_npcWokenAt".GetStableHashCode(); // string, world seconds: when it was last woken — in its locker (its mood's share: Mood)
        internal static float AwakeFor = 120f, Irk = 15f, Colder = 4f, MoodDown = 10f, MoodHours = 12f; // (Irk: NpcPatience.Gate is 20 — the second time in a row shuts its talk)

        private static float Hour { get { return (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f) / 24f; } }

        // one asleep that this player may wake (any game): its hover, E and Q ask the same
        internal static bool Can(NpcActor a)
        {
            return a != null && a.Def != null && NpcResident.Is(a) && NpcMenu.MayOrder(a) && NpcMind.AsleepOf(a) && NpcMind.LayerNow(a) <= NpcMind.Layer.Whim;
        }

        internal static void Wire()
        {
            NpcMemory.AddKind("woken", new NpcMemory.Kind { Weight = 3f, FadeDays = 3f, Yours = true, Toward = -Colder }, Lang.K("ти розбуди{в|ла} його|її серед сну"), true);
            NpcActor.Acts["wake"] = Act;
            NpcActor.HoverInstead.Add(a => Can(a) ? a.Name + " " + Lang.T("(спить)") + "\n[<color=yellow><b>" + RavenKeys.LabelOf(RavenKeys.GiftsId) + "</b></color>] " + Lang.T("Розбудити") : null);
            NpcActor.UseInstead.Add((a, p) => Can(a) ? (bool?)false : null); // (E: nothing — one asleep is not talked to)
            NpcMenu.KeyInstead.Add((a, p) =>
            {
                if (!Can(a)) return null;
                a.Act("wake " + p.GetPlayerID().ToString(CultureInfo.InvariantCulture));
                return true;
            });
            NpcToil.MoodFrom.Add(new NpcToil.Share { By = Mood, Down = NpcToil.Why.Woken });
        }

        // «wake <player>» where it is run
        private static void Act(NpcActor a, string[] w)
        {
            long by;
            NpcMind m = a.Mind;
            if (w.Length < 2 || m == null || !a.Owned || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out by) || !NpcActor.SentBy(by)) return;
            if (!m.Asleep) return; // (up already: a second press before the first one's news came)
            Player p = Player.GetPlayer(by);
            NpcMind.Plan up = new NpcMind.Plan { Kind = NpcMind.Task.Idle, To = a.transform.position, Looks = p != null, Look = p != null ? p.GetEyePoint() : Vector3.zero, Word = Lang.K("розбудили") };
            if (!m.Wake(up, AwakeFor, Whose)) { Npcs.Log(a.Id + ": asleep, and something over its night holds it — not woken"); return; }
            NpcLocker.Set(a, KWokenAt, NpcRescue.Now.ToString("0", CultureInfo.InvariantCulture));
            NpcMemory.Remember(a, "woken", by, "");
            a.Act("irk " + by.ToString(CultureInfo.InvariantCulture) + " " + Irk.ToString("0.#", CultureInfo.InvariantCulture));
            NpcPhrases.SayOr(a, "woken", a.Form(Lang.T("Га?.. Я ж сплю. Що сталося?")));
            Npcs.Log(a.Id + ": woken by " + by);
            StoryBus.Raise("npc_woken", a.Id, 0, a.transform.position);
        }

        // its mood's share: down a while after it was woken (asked once a game minute where it is run: one short string of its locker)
        private static float Mood(NpcActor a)
        {
            double at;
            string s = NpcLocker.Get(a, KWokenAt);
            if (string.IsNullOrEmpty(s) || !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out at)) return 0f;
            return NpcRescue.Now - at < MoodHours * Hour ? -MoodDown : 0f;
        }
    }
}
#endif
