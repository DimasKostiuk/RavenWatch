#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A long way by itself (npc-plan.md, «Дорога»): to a named «Серце оселі» after a hire, to the Ravens' home (the
    // Зала; for now the world's start) when it left a base hungry — and later the story's own journeys. One plan on the
    // Order layer (NpcMind Task.Travel, kept: it goes on after a reload):
    //   watched    (a player near it) it walks there — jogs, round fences and moats by the game's paths, straight on
    //              where there is none (into the water: it swims)
    //   unwatched  (nobody near: its zone is about to sleep) it steps out of the world and on — it is never seen to appear at its goal:
    //              someone there (within Watch m of the goal): to a spot out of their sight in land they hold loaded (ComeFrom; none: the edge
    //              round its goal, Edge m off, on the side it came from), and it walks in before their eyes;
    //              nobody there: right next to its goal, and it is there when they come (the user 2026-11-16: «вона рухається в сторону серця
    //              лише якщо я біля неї» — till then it waited at that edge as a ZDO, 110 m from home, for someone to come near it)
    //   15 minutes on its way, or stuck for long — the same step on (stuck close by: right next to its goal)
    //   stuck before a player's eyes (2026-11-12: it stood at a fence for as long as you were within Watch m — its step on waited for you to
    //              leave, and the feet's own last safety takes home only one that has a home): far from its goal it steps on to that edge when
    //              nobody is near where it comes; else the mind's own safety puts it on a safe spot a leg ahead, or by its goal when that is
    //              near, in a puff (NpcMind.StepTo: the one way a stuck body is put before a player)
    //   on its way no blow reaches it and no monster sees it (KTravel in its ZDO: every game knows)
    //   told       the story's word sends it far (StoryHold, over its FarWalk m: Ketil to his glade 336 m off — a plain «go» finds no way that far, he
    //              stood a minute and was taken home in a puff, 2026-11-16): the same way, a jog; there, the word itself takes over
    //   there      to a heart: it settles at it (NpcResident); to the hall: it lives there, waiting to be hired
    //   arrive     one made while a player stands by where it lives (StoryPeople: the user 2026-11-15, Ketil popped up by the heart
    //              before his eyes) is made out of sight (ComeFrom: WalkIn m off, as far from every player as it can be, in a land some
    //              player holds loaded — so it sets off at once) and walks in, at a walk; the way given to it before any game runs it (Give) —
    //              none when a story's word already leads it (StoryHold: that walks it); a way in taken up after its home moved is over at once (ArriveStale)
    internal static class NpcTravel
    {
        internal static readonly int KTravel = "RavenWatch_npcTravel".GetStableHashCode(); // bool: on its way
        internal const string ToHeart = "heart", ToRefuge = "hall", ToHome = "home", ToBack = "back", ToArrive = "arrive", ToWord = "told"; // (home: back to its base after it lay senseless — slowly, NpcVitals; back: a resident led far off and left there walks home — NpcResident; arrive: a story person made out of sight walks in to its home — StoryPeople)
        private const float WalkIn = 75f, WalkInLeast = 55f, ArriveStale = 20f, Watch = 130f, Edge = 110f, Arrive = 4f, ArriveRefuge = 10f, Longest = 900f, Stuck = 45f, Ahead = 30f;

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
            if (p.Pose == ToArrive && a.Zdo != null && Utils.DistanceXZ(a.Zdo.GetVec3(NpcRules.KHome, p.To), p.To) > ArriveStale) // (a way in left behind a story's word, taken up after its home moved: Ketil's glade — not back to the old one)
            { Mark(a, false); Npcs.Log(a.Id + ": its way in is old — it lives elsewhere now"); return true; }
            if (!p.Started) { p.Started = true; p.Since = Time.time; p.Best = float.MaxValue; p.BestAt = Time.time; Mark(a, true); Npcs.Log(a.Id + ": on its way to the " + p.Pose + " (" + Mathf.RoundToInt(Utils.DistanceXZ(m.transform.position, p.To)) + " m)"); }
            if (Time.time - p.SteppedAt > 2f) p.BestAt = Time.time; // (held up meanwhile by something else: its «no gain» counted from now)
            p.SteppedAt = Time.time;
            float d = Utils.DistanceXZ(m.transform.position, p.To);
            if (d <= (p.Pose == ToRefuge ? ArriveRefuge : Arrive)) { There(a, p); return true; }
            if (d < p.Best - 1f) { p.Best = d; p.BestAt = Time.time; }
            bool far = d > Edge + 15f, forced = Time.time - p.Since > Longest || Time.time - p.BestAt > Stuck;
            bool seen = Watched(m.transform.position); // (no step on out of the world before a player's eyes for being long on its way: it walks on; stuck there — below)
            if (far && !seen) { Jump(a, p, true); return false; }
            if (!far && !seen && d > 20f && !Watched(p.To)) { Jump(a, p, false); return false; } // (left at the edge with nobody at its goal — one that waited there from before: on to its goal)
            if (!far && forced && d > 20f && !seen) { Jump(a, p, false); return false; } // (stuck close by: right next to it)
            if (seen && Time.time - p.BestAt > Stuck) // (stuck before a player's eyes: once a Stuck s)
            {
                p.BestAt = Time.time;
                if (far && !Watched(Spot(a, p, true))) { Jump(a, p, true); return false; } // (it goes in a puff; nobody sees it come)
                Vector3 on = p.To - m.transform.position; on.y = 0f;
                Vector3 by = d > Ahead + Arrive ? m.transform.position + on.normalized * Ahead : p.To;
                float gh;
                if (d > Ahead + Arrive && ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(by, out gh)) by.y = gh;
                m.StepTo(by, "stuck on its way to the " + p.Pose + " (" + Mathf.RoundToInt(d) + " m off)");
                return false;
            }
            m.Head(dt, p.To, Ahead, p.Pose == ToHome || p.Pose == ToArrive); // (a walk: it limps; it comes in — WalkIn is under the jog's 80 m anyway)
            m.Status = p.Pose == ToRefuge ? Lang.K("іде до Дому воронів") : p.Pose == ToHome ? Lang.K("шкутильгає додому") : p.Pose == ToBack ? Lang.K("вертається додому") : p.Pose == ToWord ? Lang.K("іде") : Lang.K("іде до оселі");
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
            Vector3 at = Spot(a, p, edge);
            a.Appear(); // (a puff where it was: it goes, nobody sees it come)
            Npcs.Log(a.Id + ": stepped on to its " + p.Pose + " (" + Mathf.RoundToInt(Utils.DistanceXZ(a.transform.position, at)) + " m)");
            if (a.Body == null) return;
            a.Body.Put(at, "подорож (крок поза очима)", false); // (the body's one way: NpcBody.Put; its own puff above, none where it comes)
            ZDO z = a.Zdo;
            if (z != null) z.SetPosition(at);
            p.Since = Time.time; p.Best = float.MaxValue; p.BestAt = Time.time;
        }

        // where a step on brings it
        private static Vector3 Spot(NpcActor a, NpcMind.Plan p, bool edge)
        {
            Vector3 at;
            if (edge && !Watched(p.To)) edge = false; // (nobody at its goal to see it come: right next to it)
            if (edge && ComeFrom(p.To, out at)) return Ground(at, p.To.y); // (someone there: out of their sight, in land they hold loaded — it walks in at once, not waits as a ZDO)
            if (edge) { Vector3 back = a.transform.position - p.To; back.y = 0f; at = p.To + (back.sqrMagnitude > 1f ? back.normalized : Vector3.forward) * Edge; }
            else { Vector2 r = Random.insideUnitCircle.normalized * 12f; at = p.To + new Vector3(r.x, 0f, r.y); }
            return Ground(at, p.To.y);
        }

        // on the land's own height there (the world's shape: it need not be loaded here), at least on the water
        private static Vector3 Ground(Vector3 at, float orY)
        {
            float h;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out h)) at.y = h + 0.3f; // (loaded here: the real ground, a dug moat too)
            else if (WorldGenerator.instance != null) at.y = Mathf.Max(WorldGenerator.instance.GetHeight(at.x, at.z), ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f) + 0.5f;
            else at.y = orY + 1f;
            return at;
        }

        // where one that must be at «to» while a player is near it comes in from (the server's: its players' ZDOs; once, as it is made — 16 sides ×
        // 3 lengths, the world's height at each and along its line home: heavy, ~500 heights, once a birth): WalkIn m off it or a little nearer, on
        // land, in a land some player holds loaded (there a game runs it at once: it walks in, not waits as a ZDO), no player within WalkInLeast m —
        // the one farthest from them all, by land all the way home where there is one (a river, the sea between: it would swim). The server has no
        // way of the game's (no land loaded): a wall or a cliff between is met by the walk's own safety. False: none (a small island, players all round)
        internal static bool ComeFrom(Vector3 to, out Vector3 at)
        {
            at = to;
            if (ZNet.instance == null || WorldGenerator.instance == null) return false;
            List<ZDO> players = ZNet.instance.GetAllCharacterZDOS();
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f, best = -1f, bestDry = -1f;
            Vector3 dryAt = to;
            for (int i = 0; i < 16; i++)
            {
                float r = i * Mathf.PI / 8f;
                for (float len = WalkIn; len >= WalkInLeast; len -= 10f)
                {
                    Vector3 p = to + new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r)) * len;
                    if (WorldGenerator.instance.GetHeight(p.x, p.z) < water + 0.3f) continue; // (not in the water)
                    float least = float.MaxValue; bool held = false;
                    foreach (ZDO c in players)
                    {
                        if (c == null) continue;
                        least = Mathf.Min(least, Utils.DistanceXZ(c.GetPosition(), p));
                        held |= ZNetScene.InActiveArea(p, c.GetPosition());
                    }
                    if (!held || least < WalkInLeast) continue;
                    if (least > best) { best = least; at = p; }
                    if (least > bestDry && WorldSpot.DryWay(p, to, 0f)) { bestDry = least; dryAt = p; }
                }
            }
            if (best < 0f) return false;
            at = Ground(bestDry >= 0f ? dryAt : at, to.y);
            return true;
        }

        // its way given before any game runs it (the server's, on one it has just made — StoryPeople): kept in its locker as its Order plan, so the
        // game that takes it over goes on its way at once (NpcMind.Load); nothing else of its mind is written
        internal static void Give(ZDO z, Vector3 to, string pose)
        {
            NpcLocker.Put(z, NpcMind.KPlans, "order=" + new NpcMind.Plan { Kind = NpcMind.Task.Travel, To = to, Pose = pose }.Code());
        }

        private static void There(NpcActor a, NpcMind.Plan p)
        {
            Mark(a, false);
            if (p.Pose == ToHome) Npcs.Log(a.Id + ": limped home");
            else if (p.Pose == ToBack) Npcs.Log(a.Id + ": back home");
            else if (p.Pose == ToArrive) Npcs.Log(a.Id + ": walked in to its home");
            else if (p.Pose == ToWord) Npcs.Log(a.Id + ": came where the story's word sends it"); // (StoryHold walks it the last metres and stands it there)
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
