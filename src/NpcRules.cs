#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // An NPC's own habits (NpcDef «on …:» lines): when something happens → what it does. The first bit of a life of its
    // own, and its day; a scene can still order it about over these.
    //   on spawn            once, when it first appears in the world
    //   on near <m>         a player comes within m (once per coming: again after they went 4 m further off)
    //   on talk             E on it (with no talk rule: one of its greet lines)
    //   on hit              something hit it (every 3 s at most)
    //   on fight_over       a fight of its own ended
    //   on day / on night   the time of day turned (and once when it appears, for the time it is now)
    // What: orders split by «;», each one of
    //   say <words> · say @greet (one of its greet lines) · raise <event> (on the story's bus, with its id)
    //   any mind order (NpcMind.Do): go · follow · stay · look · lookat · wander · sit · stand · emote · fight · attack · tp
    // Words that stand for a place or someone in those orders:
    //   home    where it first appeared · here    where it is now · fire    by the nearest fire (40 m), else home
    //   player  the player of this moment (who came near, who talks), else the nearest one
    // Runs where the NPC is owned.
    internal sealed class NpcRule
    {
        public string When = "";
        public float Arg;
        public string Do = "";

        internal static NpcRule Parse(string when, string what)
        {
            string[] w = when.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0 || what.Length == 0) return null;
            NpcRule r = new NpcRule { When = w[0], Do = what };
            if (Array.IndexOf(NpcRules.Kinds, r.When) < 0) return null;
            if (r.When == "near") r.Arg = w.Length > 1 ? Mathf.Clamp(Errands.F(w[1], 5f), 1f, 40f) : 5f;
            return r;
        }
    }

    internal static class NpcRules
    {
        internal static readonly string[] Kinds = { "spawn", "near", "talk", "hit", "fight_over", "day", "night" };
        internal static readonly int KHome = "RavenWatch_npcHome".GetStableHashCode(); // Vector3: where it first appeared
        internal static readonly int KBorn = "RavenWatch_npcBorn".GetStableHashCode(); // bool: «on spawn» done

        internal static bool Has(NpcDef d, string when) { if (d == null) return false; foreach (NpcRule r in d.Rules) if (r.When == when) return true; return false; }

        // the rules of this kind, in order; true when there was one
        internal static bool Fire(NpcActor a, string when, Player subject)
        {
            NpcDef d = a.Def;
            if (d == null || !a.Owned) return false;
            bool any = false;
            foreach (NpcRule r in d.Rules)
            {
                if (r.When != when) continue;
                any = true;
                Run(a, r.Do, subject);
            }
            return any;
        }

        internal static void Run(NpcActor a, string what, Player subject)
        {
            foreach (string raw in what.Split(';'))
            {
                string s = raw.Trim();
                if (s.Length == 0) continue;
                try
                {
                    if (s.StartsWith("say "))
                    {
                        string text = s.Substring(4).Trim();
                        if (text == "@greet") text = a.Def.Greet.Count > 0 ? a.Def.Greet[UnityEngine.Random.Range(0, a.Def.Greet.Count)] : "";
                        a.Say(text);
                    }
                    else if (s.StartsWith("raise ")) StoryBus.Raise(s.Substring(6).Trim(), a.Id, 0, a.transform.position);
                    else a.OrderNow(Expand(a, s, subject));
                }
                catch (Exception e) { Npcs.Log(a.Id + " rule «" + s + "»: " + e.Message); }
            }
        }

        // home / here / fire / player → numbers the mind understands
        private static string Expand(NpcActor a, string order, Player subject)
        {
            string[] w = order.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < w.Length; i++)
            {
                switch (w[i])
                {
                    case "home": w[i] = Npcs.V(Home(a)); break;
                    case "here": w[i] = Npcs.V(a.transform.position); break;
                    case "fire": w[i] = Npcs.V(ByFire(a)); break;
                    case "player":
                        Player p = subject != null ? subject : Player.GetClosestPlayer(a.transform.position, 60f);
                        w[i] = p != null ? NpcMind.IdOf(p.GetZDOID()) : "none";
                        break;
                }
            }
            return string.Join(" ", w);
        }

        internal static Vector3 Home(NpcActor a)
        {
            ZDO z = a.Zdo;
            return z != null ? z.GetVec3(KHome, a.transform.position) : a.transform.position;
        }

        // a spot two steps from the nearest fire, on the NPC's side of it
        private static Vector3 ByFire(NpcActor a)
        {
            Fireplace best = null;
            float bd = 40f;
            foreach (Collider c in Physics.OverlapSphere(a.transform.position, 40f, LayerMask.GetMask("piece", "piece_nonsolid", "Default", "Default_small")))
            {
                Fireplace f = c.GetComponentInParent<Fireplace>();
                if (f == null || !f.IsBurning()) continue;
                float d = Vector3.Distance(f.transform.position, a.transform.position);
                if (d < bd) { bd = d; best = f; }
            }
            if (best == null) return Home(a);
            Vector3 dir = a.transform.position - best.transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            return best.transform.position + dir.normalized * 2f;
        }
    }
}
#endif
