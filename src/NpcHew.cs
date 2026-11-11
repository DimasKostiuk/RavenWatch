#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A lumberjack and a miner (the user, 2026-10-30): each works a plot («Ділянка», 25 m round a spot pointed at, up to
    // 150 m from its base — out there it is out of its base's shelter) with a tool of its own from its gear cells:
    //   lumberjack   an axe: the logs lying first, then the stumps, then a standing tree — what its axe's tier can fell
    //   miner        a pickaxe: deposits (copper, silver, tin…), boulders, whatever stone a pickaxe breaks
    // Every blow is the game's own (the thing's owner takes it as from a player: the same drops, a tree falls as ever) and
    // wears its tool as a player's is worn — at nothing it stops («сокира зламана»: on the heart's board) until you mend
    // it or give it another. What falls (wood, cones and seeds, resin; ore, stone) it picks up and carries to its chest
    // when its hands are full. A blow is slower than a player's. Falling trees and logs do not hurt our people.
    internal static class NpcHew
    {
        internal static readonly int KArea = "RavenWatch_npcArea".GetStableHashCode(); // vec3: its plot (none: zero)
        internal const float Radius = 25f, FarFromBase = 150f, BlowEvery = 1.6f;

        // ---------------------------------------------------------------- its plot
        internal static bool Area(NpcActor a, out Vector3 at) { ZDO z = a != null ? a.Zdo : null; at = z != null ? NpcLocker.Vec(z, KArea, Vector3.zero) : Vector3.zero; return at != Vector3.zero; }

        internal static string SetArea(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a)) return "Спершу оселі його: працюють від своєї бази.";
            if (Utils.DistanceXZ(aim, NpcRules.Home(a)) > FarFromBase) return Lang.F("Задалеко від його бази (до {0} м).", Mathf.RoundToInt(FarFromBase));
            NpcLocker.Put(a.Zdo, KArea, aim);
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            return Lang.F("Тут його ділянка ({0} м довкола).", Mathf.RoundToInt(Radius));
        }

        // ---------------------------------------------------------------- its tool (from its gear cells)
        internal static ItemDrop.ItemData Tool(NpcActor a, bool chop, bool broken = false)
        {
            Inventory cells = a != null && a.Gear != null ? a.Gear.Cells : null;
            if (cells == null) return null;
            ItemDrop.ItemData best = null; float score = 0f;
            foreach (ItemDrop.ItemData it in cells.GetAllItems())
            {
                HitData.DamageTypes d = it.GetDamage();
                float v = chop ? d.m_chop : d.m_pickaxe;
                if (v <= 0f) continue;
                bool dead = it.m_shared.m_useDurability && it.m_durability <= 0f;
                if (dead != broken) continue;
                float s = it.m_shared.m_toolTier * 1000f + v;
                if (s > score) { score = s; best = it; }
            }
            return best;
        }

        internal static string ToolWord(bool chop) { return chop ? Lang.T("сокира") : Lang.T("кайло"); }

        // why it does not work now (no tool, a broken one, no plot): null when it can
        internal static string Lacks(NpcActor a, bool chop)
        {
            Vector3 at;
            if (!Area(a, out at)) return Lang.T("чекає, де працювати (кнопка «Ділянка»)");
            if (Tool(a, chop) != null) return null;
            return Tool(a, chop, true) != null ? Lang.F("{0} зламана — полагодь або дай іншу", ToolWord(chop)) : Lang.F("потрібна {0} у спорядженні", ToolWord(chop));
        }

        // ---------------------------------------------------------------- what is in its plot (looked over now and then)
        private sealed class Plot { public float At; public readonly List<Component> Things = new List<Component>(); }
        private static readonly Dictionary<string, Plot> Plots = new Dictionary<string, Plot>();
        private static readonly int Solid = LayerMask.GetMask("Default", "static_solid", "Default_small");

        private static List<Component> Things(Vector3 area)
        {
            string key = Mathf.RoundToInt(area.x) + "," + Mathf.RoundToInt(area.z);
            Plot p;
            if (Plots.TryGetValue(key, out p) && Time.time - p.At < 8f) return p.Things;
            p = new Plot { At = Time.time };
            HashSet<Component> seen = new HashSet<Component>();
            foreach (Collider c in Physics.OverlapSphere(area, Radius, Solid))
            {
                Component t = c.GetComponentInParent<IDestructible>() as Component; // (one look-up; what is not a tree, a log, a stone is left out by Rank)
                if (t != null && (t is TreeLog || t is TreeBase || t is MineRock5 || t is MineRock || t is Destructible) && seen.Add(t)) p.Things.Add(t);
            }
            if (Plots.Count > 50) Plots.Clear();
            Plots[key] = p;
            return p.Things;
        }

        // ---------------------------------------------------------------- the work
        internal static NpcWorker.Errand Find(NpcWorker w, bool chop)
        {
            NpcActor a = w.Actor;
            if (Worn.Remove(a)) Done(a); // (the last one was cut short: its tool away, its wear kept)
            string lacks = Lacks(a, chop);
            if (lacks != null) { w.Why = lacks; return null; }
            Vector3 area; Area(a, out area);
            ItemDrop.ItemData tool = Tool(a, chop);
            // 1) what fell: picked up (its hands not full)
            if (w.Room)
            {
                foreach (ItemDrop d in NpcWorker.Drops(area, Radius))
                {
                    if (d == null || !Its(d, chop)) continue;
                    ZDOID id = NpcKit.IdOf(d);
                    if (id.IsNone() || !Reserve.IsFree(id, w.Me)) continue;
                    return new NpcWorker.Errand { Target = id, At = d.transform.position, Near = 1.2f, Collect = new HashSet<string> { d.gameObject.name.Replace("(Clone)", "").Trim() }, Word = Lang.T("підбирає"), Use = (wk, e) => true };
                }
            }
            else return null; // (its hands full: to its chest first — NpcWorker stores what it carries)
            // 2) the nearest thing its tool can take, by what comes first
            Component best = null; int rank = int.MaxValue; float bd = float.MaxValue;
            foreach (Component t in Things(area))
            {
                if (t == null || (t.transform.position - area).sqrMagnitude > Radius * Radius) continue;
                int r = Rank(t, chop, tool.m_shared.m_toolTier);
                if (r < 0) continue;
                ZDOID id = NpcKit.IdOf(t);
                if (id.IsNone() || !Reserve.IsFree(id, w.Me)) continue;
                float d = (t.transform.position - w.Pos).sqrMagnitude;
                if (r < rank || (r == rank && d < bd)) { best = t; rank = r; bd = d; }
            }
            if (best == null) { w.Why = chop ? Lang.T("на ділянці нема що рубати") : Lang.T("на ділянці нема що копати"); return null; }
            return Hew(w, best, chop);
        }

        // what drops it picks up: a lumberjack wood, cones and seeds, resin; a miner ore, stone, a gem
        private static bool Its(ItemDrop d, bool chop)
        {
            string k = NpcTidy.Kinds[NpcTidy.KindOf(d)].Id;
            return chop ? (k == "wood" || k == "plant" || k == "fuel") : (k == "ore" || k == "stone" || k == "value");
        }

        // which first (−1: not for it): a lumberjack the logs, the stumps, the trees; a miner the deposits, the rest
        private static int Rank(Component t, bool chop, int tier)
        {
            if (chop)
            {
                TreeLog log = t as TreeLog; if (log != null) return log.m_minToolTier <= tier ? 0 : -1;
                Destructible st = t as Destructible; if (st != null) return st.m_destructibleType == DestructibleType.Tree && st.m_minToolTier <= tier ? 1 : -1;
                TreeBase tree = t as TreeBase; if (tree != null) return tree.m_minToolTier <= tier ? 2 : -1;
                return -1;
            }
            MineRock5 m5 = t as MineRock5; if (m5 != null) return m5.m_minToolTier <= tier ? 0 : -1;
            MineRock m = t as MineRock; if (m != null) return m.m_minToolTier <= tier ? 0 : -1;
            Destructible ds = t as Destructible;
            if (ds == null || ds.m_destructibleType == DestructibleType.Tree || ds.m_destructibleType == DestructibleType.Character || ds.m_minToolTier > tier) return -1;
            if (ds.m_damages.m_pickaxe == HitData.DamageModifier.Immune || ds.GetComponent<DropOnDestroyed>() == null) return -1; // (a stone that gives something)
            return 1;
        }

        // the nearest part of it to strike (a deposit: one of its pieces still there)
        private static Collider Part(Component t, Vector3 from)
        {
            Collider best = null; float bd = float.MaxValue;
            foreach (Collider c in t.GetComponentsInChildren<Collider>())
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger) continue;
                float d = (c.bounds.ClosestPoint(from) - from).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        private static NpcWorker.Errand Hew(NpcWorker w, Component t, bool chop)
        {
            NpcActor a = w.Actor;
            Collider part = Part(t, w.Pos);
            Vector3 at = part != null ? part.bounds.ClosestPoint(w.Pos) : t.transform.position;
            float next = 0f; int blows = 0;
            return new NpcWorker.Errand
            {
                Target = NpcKit.IdOf(t), At = at, Near = chop ? 1.8f : 2.4f,
                Word = chop ? (t is TreeBase ? Lang.T("рубає дерево") : Lang.T("розрубує колоду")) : Lang.T("копає"),
                Use = (wk, e) =>
                {
                    ItemDrop.ItemData tool = Tool(a, chop);
                    if (t == null || tool == null || blows >= 20) { Worn.Remove(a); Done(a); return true; } // (it fell / broke, or the tool did: done)
                    if (Time.time < next) return false;
                    next = Time.time + BlowEvery * NpcHunger.Slow(a) / (NpcKnack.Of(a, chop ? "lumber" : "miner") * NpcToil.Pace(a)); // (as its other work: hunger, knack, mood, craft)
                    NpcChores.Hold(a, tool.m_dropPrefab != null ? tool.m_dropPrefab.name : (chop ? "AxeStone" : "PickaxeAntler")); // (in hand at every blow: a redress or a fight between them took it)
                    if (blows++ == 0) Worn.Add(a);
                    Collider c = chop ? null : Part(t, wk.Pos);
                    if (!chop && c == null) { Worn.Remove(a); Done(a); return true; }
                    Blow(wk, tool, chop, t as IDestructible, c != null ? c.bounds.ClosestPoint(wk.Pos) : e.At + Vector3.up, c);
                    return false;
                }
            };
        }

        // the game's own blow, its tool worn as a player's
        private static void Blow(NpcWorker w, ItemDrop.ItemData tool, bool chop, IDestructible target, Vector3 point, Collider part)
        {
            if (target == null) return;
            HitData hit = new HitData();
            HitData.DamageTypes d = tool.GetDamage();
            if (chop) hit.m_damage.m_chop = d.m_chop; else hit.m_damage.m_pickaxe = d.m_pickaxe;
            hit.m_toolTier = (short)tool.m_shared.m_toolTier;
            hit.m_point = point;
            Vector3 dir = point - w.Pos; dir.y = 0f;
            hit.m_dir = dir.sqrMagnitude > 0.01f ? dir.normalized : w.transform.forward;
            hit.m_hitCollider = part;
            Character me = w.GetComponent<Character>();
            if (me != null) hit.SetAttacker(me);
            target.Damage(hit);
            if (tool.m_shared.m_useDurability) tool.m_durability = Mathf.Max(0f, tool.m_durability - Mathf.Max(0.5f, tool.m_shared.m_useDurabilityDrain));
            Humanoid body = w.GetComponent<Humanoid>();
            ItemDrop.ItemData inHand = body != null ? body.GetCurrentWeapon() : null;
            ZSyncAnimation za = w.GetComponent<ZSyncAnimation>();
            if (za != null && inHand != null && !string.IsNullOrEmpty(inHand.m_shared.m_attack.m_attackAnimation)) za.SetTrigger(inHand.m_shared.m_attack.m_attackAnimation);
        }

        private static readonly HashSet<NpcActor> Worn = new HashSet<NpcActor>(); // (a tool in its hand, its wear not kept yet)

        // its work set anew (another job, a reset): a tool still in its hand put away, its wear kept
        internal static void PutAway(NpcActor a) { if (a == null) return; if (Worn.Remove(a)) Done(a); }

        // an errand over: the tool put away, its wear kept (once a thing, not every blow: its locker is sent when written)
        private static void Done(NpcActor a)
        {
            NpcChores.Hold(a, null);
            if (a.Gear != null) a.Gear.Keep();
        }
    }

    internal sealed class Lumber : Job
    {
        internal Lumber() { Move = ""; Id = "lumber"; Word = Lang.K("лісоруб"); Icon = "n-lumber"; Place = Lang.K("на ділянку лісу"); StoreAfterEach = false;
            Desc = Lang.K("Рубає дерева на своїй ділянці (25 м довкола, до 150 м від бази): спершу колоди, тоді пні, тоді дерева. Потрібна сокира в його спорядженні — вона зношується, як у тебе. Дерево, шишки, смолу несе в ящик."); }
        internal override string Assign(NpcActor a, Vector3 at) { string s = NpcWorker.SetJob(a, Id, NpcMenu.By); return a.Worker == null || a.Worker.JobId != Id ? s : NpcHew.SetArea(a, at); }
        internal override NpcWorker.Errand Find(NpcWorker w) { return NpcHew.Find(w, true); }
    }

    internal sealed class Miner : Job
    {
        internal Miner() { Move = ""; Id = "miner"; Word = Lang.K("рудокоп"); Icon = "n-miner"; Place = Lang.K("на ділянку з рудою чи камінням"); StoreAfterEach = false;
            Desc = Lang.K("Копає на своїй ділянці (25 м довкола, до 150 м від бази) руду й каміння, яке бере його кайло. Потрібне кайло в його спорядженні — воно зношується, як у тебе. Руду, камінь несе в ящик."); }
        internal override string Assign(NpcActor a, Vector3 at) { string s = NpcWorker.SetJob(a, Id, NpcMenu.By); return a.Worker == null || a.Worker.JobId != Id ? s : NpcHew.SetArea(a, at); }
        internal override NpcWorker.Errand Find(NpcWorker w) { return NpcHew.Find(w, false); }
    }

    // a falling tree, a rolling log, a piece coming down: not on our people (they stand by them all day)
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class NpcNoTreeHitPatch
    {
        private static bool Prefix(Character __instance, HitData hit)
        {
            if (NpcActor.Live.Count == 0 || hit == null) return true;
            if (hit.m_hitType != HitData.HitType.Tree && hit.m_hitType != HitData.HitType.Impact && hit.m_hitType != HitData.HitType.Structural) return true;
            try { return NpcActor.Of(__instance) == null; }
            catch (System.Exception e) { PatchGuard.Fail("NpcNoTreeHitPatch", e); return true; }
        }
    }
}
#endif
