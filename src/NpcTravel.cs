#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // A long way by itself (npc-plan.md, «Дорога»): to a named «Серце оселі» after a hire, to the Ravens' home (the
    // Зала; for now the world's start) when it left a base hungry — and later the story's own journeys. One plan on the
    // Order layer (NpcMind Task.Travel, kept: it goes on after a reload):
    //   watched    (a player near it) it walks there — jogs, round fences and moats by the game's paths, straight on
    //              where there is none (into the water: it swims)
    //   unwatched  (nobody near: its zone is about to sleep) it steps out of the world and on to the edge of the land
    //              round its goal (Edge m off, on the side it came from) — it is never seen to appear at its goal: whoever
    //              is there sees it come from afar and walk in (the user); nobody there, it waits at that edge as a ZDO and
    //              walks in once someone comes
    //   15 minutes on its way, or stuck for long — the same step on (stuck close by: right next to its goal)
    //   on its way no blow reaches it and no monster sees it (KTravel in its ZDO: every game knows)
    //   there      to a heart: it settles at it (NpcResident); to the hall: it lives there, waiting to be hired
    internal static class NpcTravel
    {
        internal static readonly int KTravel = "RavenWatch_npcTravel".GetStableHashCode(); // bool: on its way
        internal const string ToHeart = "heart", ToRefuge = "hall", ToHome = "home", ToBack = "back"; // (home: back to its base after it lay senseless — slowly, NpcVitals; back: a resident led far off and left there walks home — NpcResident)
        private const float Watch = 130f, Edge = 110f, Arrive = 4f, ArriveRefuge = 10f, Longest = 900f, Stuck = 45f, Ahead = 30f;

        // the ravens' home: the story's place that takes in the unhired (locations.list «refuge: yes» — the Hall) once it stands; the world's start until then
        internal static Vector3 Refuge
        {
            get
            {
                Vector3 p;
                Quaternion r;
                Loc home = Locs.Refuge;
                if (home != null && home.Built && home.Where(out p, out r)) return home.Shape() && home.Gate.Length > 0 && !LocState.GateOpenOf(home) ? home.BeforeFront(p, r) : p; // (its great door still locked: before it, not pushing at its wall)
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetLocationIcon("StartTemple", out p)) return p;
                if (ZoneSystem.instance != null && ZNet.instance != null && ZNet.instance.IsServer())
                {
                    ZoneSystem.LocationInstance li;
                    if (ZoneSystem.instance.FindClosestLocation("StartTemple", Vector3.zero, out li)) return li.m_position;
                }
                if (Game.instance != null && Game.instance.GetPlayerProfile() != null) return Game.instance.GetPlayerProfile().GetHomePoint();
                return Vector3.zero;
            }
        }

        // on its way (every game: from its ZDO)
        internal static bool Away(Character c)
        {
            NpcActor a = NpcActor.Of(c);
            ZDO z = a != null ? a.Zdo : null;
            return z != null && z.GetBool(KTravel, false);
        }

        internal static void Mark(NpcActor a, bool on)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || !a.Owned || z.GetBool(KTravel, false) == on) return;
            z.Set(KTravel, on);
        }

        // one step of the way (true: there, the plan is over)
        internal static bool Step(NpcMind m, NpcMind.Plan p, float dt)
        {
            NpcActor a = m.Actor;
            if (a == null) return true;
            if (!p.Started) { p.Started = true; p.Since = Time.time; p.Best = float.MaxValue; p.BestAt = Time.time; Mark(a, true); Npcs.Log(a.Id + ": on its way to the " + p.Pose + " (" + Mathf.RoundToInt(Utils.DistanceXZ(m.transform.position, p.To)) + " m)"); }
            float d = Utils.DistanceXZ(m.transform.position, p.To);
            if (d <= (p.Pose == ToRefuge ? ArriveRefuge : Arrive)) { There(a, p); return true; }
            if (d < p.Best - 1f) { p.Best = d; p.BestAt = Time.time; }
            bool far = d > Edge + 15f, forced = Time.time - p.Since > Longest || Time.time - p.BestAt > Stuck;
            bool seen = Watched(m.transform.position); // (never a step on before a player's eyes, forced or not: it walks on)
            if (far && !seen) { Jump(a, p, true); return false; }
            if (!far && forced && d > 20f && !seen) { Jump(a, p, false); return false; } // (stuck close by: right next to it)
            m.Head(dt, p.To, Ahead, p.Pose == ToHome);
            m.Status = p.Pose == ToRefuge ? Lang.K("іде до Дому воронів") : p.Pose == ToHome ? Lang.K("шкутильгає додому") : p.Pose == ToBack ? Lang.K("вертається додому") : Lang.K("іде до оселі");
            return false;
        }

        private static bool Watched(Vector3 at)
        {
            foreach (Player pl in Player.GetAllPlayers()) if (pl != null && Utils.DistanceXZ(pl.transform.position, at) < Watch) return true;
            return false;
        }

        // out of the world and on: to the edge round its goal on the side it came from (edge), or right next to it; on the
        // land's own height there (the world's shape: it need not be loaded here), at least on the water
        private static void Jump(NpcActor a, NpcMind.Plan p, bool edge)
        {
            Vector3 at;
            if (edge) { Vector3 back = a.transform.position - p.To; back.y = 0f; at = p.To + (back.sqrMagnitude > 1f ? back.normalized : Vector3.forward) * Edge; }
            else { Vector2 r = Random.insideUnitCircle.normalized * 12f; at = p.To + new Vector3(r.x, 0f, r.y); }
            float h;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out h)) at.y = h + 0.3f; // (loaded here: the real ground, a dug moat too)
            else if (WorldGenerator.instance != null) at.y = Mathf.Max(WorldGenerator.instance.GetHeight(at.x, at.z), ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f) + 0.5f;
            else at.y = p.To.y + 1f;
            a.Appear(); // (a puff where it was: it goes, nobody sees it come)
            Npcs.Log(a.Id + ": stepped on to its " + p.Pose + " (" + Mathf.RoundToInt(Utils.DistanceXZ(a.transform.position, at)) + " m)");
            if (a.Body == null) return;
            a.Body.Put(at, "подорож (крок поза очима)", false); // (the body's one way: NpcBody.Put; its own puff above, none where it comes)
            ZDO z = a.Zdo;
            if (z != null) z.SetPosition(at);
            p.Since = Time.time; p.Best = float.MaxValue; p.BestAt = Time.time;
        }

        private static void There(NpcActor a, NpcMind.Plan p)
        {
            Mark(a, false);
            if (p.Pose == ToHome) Npcs.Log(a.Id + ": limped home");
            else if (p.Pose == ToBack) Npcs.Log(a.Id + ": back home");
            else if (p.Pose == ToHeart)
            {
                string said = NpcResident.Settle(a, p.To);
                Npcs.Log(a.Id + ": came to its heart — " + said);
                if (NpcResident.Is(a)) { NpcPhrases.SayOr(a, "arrived", Lang.T("Ось я й тут. Тут і житиму.")); NpcMemory.Remember(a, "settled", 0L, ""); }
                else if (NpcLevers.IsProtected(a)) Npcs.Log(a.Id + ": no room at its heart, but the story holds it — it stays here");
                else // (no room here — or the heart is gone: back to the ravens' home, hired again from there)
                {
                    NpcPhrases.SayOr(a, "no_room", Lang.T("Тут нема для мене місця. Піду до Дому воронів."));
                    a.Act("unhire");
                    a.Order("travel " + Npcs.V(Refuge) + " " + ToRefuge);
                }
            }
            else
            {
                ZDO z = a.Zdo;
                if (z != null) z.Set(NpcRules.KHome, p.To);
                if (a.Mind != null) a.Mind.Put(NpcMind.Layer.Routine, new NpcMind.Plan { Kind = NpcMind.Task.Wander, Home = p.To, Radius = 15f }, "travel");
                Npcs.Log(a.Id + ": came to the ravens' home — waits to be hired");
            }
            StoryBus.Raise("npc_arrived", a.Id, 0, a.transform.position);
        }
    }

    // on its way nobody's foe: no monster goes for it, it goes for none
    [HarmonyLib.HarmonyPatch(typeof(BaseAI), "IsEnemy", new[] { typeof(Character), typeof(Character) })]
    internal static class NpcTravelUnseenPatch
    {
        private static void Postfix(Character a, Character b, ref bool __result)
        {
            if (!__result || NpcActor.Live.Count == 0) return;
            try { if (NpcVitals.Unseen(a) || NpcVitals.Unseen(b)) __result = false; } // (senseless too: nobody minds it)
            catch (System.Exception e) { PatchGuard.Fail("NpcTravelUnseenPatch", e); }
        }
    }
}
#endif
