#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Whims (npc-plan.md): things a resident does because it feels like it, outside its duties — a rest in work time,
    // a stretch, a look round. Each is a small piece of its own (one entry below): when it can be, how likely, what it
    // does (a plan for the mind's Whim layer) and for how long. The Whim layer is over Job and Routine (it may leave
    // its work for a while) and under Order and Fight (never over them); when the time is up its work goes on.
    // Tried now and then in the day (NpcResident), one at a time. New whims: add an entry (later: wash in a tub or the
    // sea, chat with another resident, feed the animals…).
    internal static class NpcWhims
    {
        internal sealed class Whim
        {
            public string Id, Word;            // its name; what the window and the log say
            public float Weight = 1f;          // how likely among those that can be now
            public float Min = 30f, Max = 60f; // for how long (s)
            public Func<NpcResident, bool> Can = r => true;
            public Func<NpcResident, NpcMind.Plan> Make;
        }


        internal static readonly List<Whim> All = new List<Whim>
        {
            // a rest: sits down somewhere in its base (a free bench near it, else on the ground) for a minute or two
            new Whim { Id = "rest", Word = "перепочиває", Weight = 4f, Min = 60f, Max = 150f,
                Make = r => new NpcMind.Plan { Kind = NpcMind.Task.Sit, To = r.Base.Inside(r.transform.position) && !NpcPrivate.Off(r.Actor, r.Base, r.transform.position) ? r.transform.position : NpcPrivate.Spot(r.Actor, r.Base) } },
            // by the fire: a burning fire in its base, a bench near it when there is one (found by «sit»)
            new Whim { Id = "fire", Word = "гріється біля вогню", Weight = 3f, Min = 90f, Max = 180f,
                Can = r => NpcToil.FireSpot(r.Base, r.transform.position, r.Actor) != null,
                Make = r => new NpcMind.Plan { Kind = NpcMind.Task.Sit, To = NpcToil.FireSpot(r.Base, r.transform.position, r.Actor) ?? r.transform.position } },
            // a guest: once in a few game days it drops in to warm itself by the fire of a player's house — never while
            // anyone is home (NpcPrivate; at other times it keeps out)
            new Whim { Id = "guest", Word = "гріється в гостях", Weight = 1f, Min = 60f, Max = 120f, Can = r => NpcPrivate.CanVisit(r), Make = r => NpcPrivate.Visit(r) },
            // a stretch: the arms up (the game's flex), then it stands a little
            new Whim { Id = "stretch", Word = "потягується", Weight = 1f, Min = 6f, Max = 10f,
                Make = r => { if (r.Actor.Body != null) r.Actor.Body.Gesture("flex", false); return new NpcMind.Plan { Kind = NpcMind.Task.Idle, To = r.transform.position }; } },
            // a look round: stands and looks out somewhere for a few breaths
            new Whim { Id = "gaze", Word = "задивився", Weight = 2f, Min = 8f, Max = 16f,
                Make = r => { Vector2 d = UnityEngine.Random.insideUnitCircle.normalized * 20f; return new NpcMind.Plan { Kind = NpcMind.Task.Idle, To = r.transform.position, Looks = true, Look = r.transform.position + new Vector3(d.x, 2f, d.y) }; } },
            // a wash: to a tub in its base (sits in its water) or to the water at its shore (kneels at it)
            new Whim { Id = "wash", Word = "миється", Weight = 1.5f, Min = 50f, Max = 80f, Can = r => Wash(r) != null, Make = r => Wash(r) },
            // chores (NpcChores): mend what is damaged in its base; feed a hungry tamed beast, top up the ravens' feeder
            new Whim { Id = "mend", Word = "лагодить", Weight = 2.5f, Min = 120f, Max = 180f, Can = r => NpcChores.CanMend(r), Make = r => NpcChores.Plan(NpcChores.Mend) },
            new Whim { Id = "tend", Word = "годує звірів", Weight = 2.5f, Min = 90f, Max = 150f, Can = r => NpcChores.CanTend(r), Make = r => NpcChores.Plan(NpcChores.Tend) },
            // a meal (never by chance: hunger takes it there, NpcResident; here for the console)
            new Whim { Id = "eat", Word = "іде їсти", Weight = 0f, Min = 240f, Max = 300f, Can = r => false, Make = r => NpcMeals.Plan() },
        };

        // ---------------------------------------------------------------- where to wash (asked of a base once a minute)
        private static readonly Dictionary<string, KeyValuePair<float, Vector3[]>> WashAt = new Dictionary<string, KeyValuePair<float, Vector3[]>>(); // base → {where, looking at, 1 = a tub}
        private static readonly Dictionary<string, bool> Wet = new Dictionary<string, bool>(); // a kind of piece → has water of its own (a tub)

        private static NpcMind.Plan Wash(NpcResident r)
        {
            NpcBase b = r.Base;
            if (b == null || !b.Ready) return null;
            string key = Mathf.RoundToInt(b.Centre.x) + "," + Mathf.RoundToInt(b.Centre.z);
            KeyValuePair<float, Vector3[]> had;
            Vector3[] w;
            if (WashAt.TryGetValue(key, out had) && Time.time - had.Key < 60f) w = had.Value;
            else
            {
                w = Tub(b);
                Vector3 at, water;
                if (w == null && b.ShoreSpot(out at, out water)) w = new[] { at, water, Vector3.zero };
                if (WashAt.Count > 50) WashAt.Clear();
                WashAt[key] = new KeyValuePair<float, Vector3[]>(Time.time, w);
            }
            if (w == null || NpcPrivate.Off(r.Actor, b, w[0])) return null; // (a tub in a player's house: not for it)
            bool tub = w[2].x > 0.5f;
            if (!tub && NpcBackstory.Fact(r.Actor, "боїться_води")) return null; // (its story: afraid of deep water — a tub, or no wash)
            return new NpcMind.Plan { Kind = NpcMind.Task.Go, To = w[0], Near = tub ? 0.4f : 0.8f, Pose = tub ? "sit" : "kneel", Looks = true, Look = w[1] };
        }

        // a tub in the base: a built piece with water of its own (the game's hot tub, a mod's pool)
        private static Vector3[] Tub(NpcBase b)
        {
            if (b.Tubs != null) { foreach (Piece pc in b.Tubs) if (pc != null) { Vector3 c0 = pc.transform.position; return new[] { c0, c0 + pc.transform.forward * 2f, Vector3.right }; } return null; } // (the base's own look: NpcBase)
            List<Piece> pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(b.Centre, NpcBase.Reach, pieces);
            foreach (Piece pc in pieces)
            {
                if (pc == null) continue;
                bool wet;
                if (!Wet.TryGetValue(pc.m_name ?? "", out wet)) { wet = pc.GetComponentInChildren<WaterVolume>() != null; Wet[pc.m_name ?? ""] = wet; } // (asked once a kind: a big base has thousands)
                if (!wet || !b.Inside(pc.transform.position)) continue;
                Vector3 c = pc.transform.position;
                return new[] { c, c + pc.transform.forward * 2f, Vector3.right };
            }
            return null;
        }

        // maybe one now (a free mind: nothing over its duties)
        internal static void Maybe(NpcResident r)
        {
            NpcMind m = r.Mind;
            if (m == null || m.NowLayer > NpcMind.Layer.Job || !m.WhimFree(NpcMind.Leisure)) return;
            if (UnityEngine.Random.value > NpcPersona.Of(r.Actor).WhimChance) return; // (a lazy one gives in more often)
            Whim w = Pick(r);
            if (w == null) return;
            NpcMind.Plan p = w.Make(r);
            if (p == null) return;
            if (!m.TakeWhim(p, UnityEngine.Random.Range(w.Min, w.Max), NpcMind.Leisure)) return;
            Npcs.Log(r.Actor.Id + ": whim «" + w.Id + "» (" + Mathf.RoundToInt(p.Until - Time.time) + " s)");
        }

        // for the console: this one now
        internal static string Force(NpcResident r, string id)
        {
            foreach (Whim w in All)
            {
                if (w.Id != id) continue;
                NpcMind.Plan p = w.Make(r);
                if (!r.Mind.TakeWhim(p, UnityEngine.Random.Range(w.Min, w.Max), NpcMind.Leisure, true)) return "Ні.";
                return "Примха «" + w.Word + "» на " + Mathf.RoundToInt(p.Until - Time.time) + " с.";
            }
            List<string> ids = new List<string>(); foreach (Whim w in All) ids.Add(w.Id);
            return "Примхи: " + string.Join(", ", ids.ToArray());
        }

        private static Whim Pick(NpcResident r)
        {
            float sum = 0f;
            foreach (Whim w in All) if (w.Can(r)) sum += w.Weight;
            if (sum <= 0f) return null;
            float roll = UnityEngine.Random.value * sum;
            foreach (Whim w in All) { if (!w.Can(r)) continue; roll -= w.Weight; if (roll <= 0f) return w; }
            return null;
        }
    }
}
#endif
