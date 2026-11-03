#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // A person in need (NpcRescue) is not hurt as the game hurts: it bears a number of blows (Max), whatever land the foe
    // is from — every blow of a creature takes one; the game's health bar over it shows how many are left. None left: it
    // falls senseless (NpcVitals) — a healing mead brings it to with half of them; nobody in time (its lying time) and it
    // dies: gone for good, its trouble over (the server takes it away with its foes: NpcRescue.End). A player's blow
    // counts for nothing. Saved, it is past harm until it lives somewhere (NpcActor.Immortal). Saved but not wanted
    // («Іди своєю дорогою»): it walks off and is gone. A story person gets its own way of this later.
    // Runs where the person is owned (the game's damage comes there); what every game needs is in its ZDO.
    internal static class NpcRescueHits
    {
        internal const int Max = 10;
        internal const int Dead = 3, Expired = 4; // (NpcRescue.KStage: 0 waiting · 1 set · 2 saved · 3 died · 4 its time ran out)
        internal static readonly int KHits = "RavenWatch_rescueHits".GetStableHashCode(); // int: blows it can still bear
        internal static readonly int KGone = "RavenWatch_rescueGone".GetStableHashCode(); // bool: saved, sent its way — gone at its «until»

        // in need now: its trouble set, not saved, not dead, nobody's yet
        internal static bool InNeed(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            return z != null && z.GetString(NpcRescue.KKind, "").Length > 0 && z.GetInt(NpcRescue.KStage, 0) == 1 && !NpcTalk.Hired(a);
        }

        internal static bool Died(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null && z.GetString(NpcRescue.KKind, "").Length > 0 && z.GetInt(NpcRescue.KStage, 0) == Dead; }

        internal static int Left(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? Mathf.Clamp(z.GetInt(KHits, Max), 0, Max) : Max; }

        // its scene set up: all of them
        internal static void Fill(NpcActor a)
        {
            if (a == null || a.Zdo == null) return;
            a.Zdo.Set(KHits, Max);
            Bar(a, Max);
        }

        // a blow (its owner's game, the game's RPC_Damage): true — taken here, the game's own damage skipped
        internal static bool Take(Character body, HitData hit)
        {
            NpcActor a = NpcActor.Of(body);
            if (!InNeed(a)) return false;
            if (!a.Owned || NpcVitals.IsDown(a)) return true;
            Character from = hit != null ? hit.GetAttacker() : null;
            if (from == null || from.IsPlayer() || NpcActor.Of(from) != null) return true; // (only a creature's blow counts)
            int left = Left(a) - 1;
            a.Zdo.Set(KHits, Mathf.Max(0, left));
            if (left <= 0) { if (a.Vitals != null) a.Vitals.KnockOut(); a.Say(a.Form(Lang.T("Я… більше не можу…"))); return true; }
            Bar(a, left);
            if (left == 3) a.Say(a.Form(Lang.T("Ще трохи — і я впаду! Швидше!")));
            body.m_hitEffects.Create(hit.m_point, Quaternion.identity, body.transform); // (the blow seen and heard, as the game's)
            return true;
        }

        // the game's own health bar as the blows left (it is sent to every game)
        private static void Bar(NpcActor a, int left)
        {
            Character c = a.GetComponent<Character>();
            if (c != null) c.SetHealth(Mathf.Max(1f, c.GetMaxHealth() * left / Max));
        }

        // brought to with a healing mead: half of them
        internal static void Revived(NpcActor a)
        {
            if (!InNeed(a)) return;
            a.Zdo.Set(KHits, Max / 2);
            Bar(a, Max / 2);
        }

        // lying too long, nobody came: dead — the server takes it away (NpcRescue: «until» long past)
        internal static void Perish(NpcActor a)
        {
            ZDO z = a.Zdo;
            if (z == null) return;
            z.Set(NpcRescue.KStage, Dead);
            z.Set(NpcRescue.KUntil, 1L);
            Npcs.Log(a.Id + ": nobody brought it to — it died");
        }

        // saved, not wanted: off its own way; gone half a minute later
        internal static void GoAway(NpcActor a, Player from)
        {
            ZDO z = a.Zdo;
            if (z == null || NpcTalk.Hired(a)) return;
            Vector3 away = from != null ? a.transform.position - from.transform.position : a.transform.forward; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            a.Order("go " + Npcs.V(a.transform.position + away.normalized * 60f));
            z.Set(KGone, true);
            z.Set(NpcRescue.KUntil, (long)NpcRescue.Now + 30L);
            string bye = Lang.T("Що ж… бувай. Дякую, що не кину{в|ла} мене там.");
            a.Say(from != null ? Grammar.Fix(bye, from) : Lang.T("Що ж… бувай. Дякую тобі."));
            Npcs.Log(a.Id + ": saved, goes its own way");
        }

        // under its name: how many blows it still bears
        internal static string Line(NpcActor a)
        {
            if (!InNeed(a) || NpcVitals.IsDown(a)) return "";
            int left = Left(a);
            string col = left <= 3 ? "#e8705a" : left <= 6 ? "#e3c060" : "#9fd27a";
            return "\n<size=85%><color=" + col + ">" + Lang.F("Витримає ще {0}", Blows(left)) + "</color></size>";
        }

        // «1 удар», «3 удари», «7 ударів»
        private static string Blows(int n)
        {
            int d = n % 10, h = n % 100;
            string w = d == 1 && h != 11 ? Lang.T("удар") : d >= 2 && d <= 4 && (h < 12 || h > 14) ? Lang.T("удари") : Lang.T("ударів");
            return n + " " + w;
        }
    }
}
#endif
