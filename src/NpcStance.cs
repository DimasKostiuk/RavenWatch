#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // How a person stands or sits when nothing asks anything of its hands (the user 2026-11-05: «щоб нпс стояли в різних позах і сиділи
    // теж»): over the one stance of the game, a pose of its own now and then — its way in, a while, its way out, all our clips on its body
    // (NpcBody.Clip), so every game sees it from its ZDO. Which one (Pick):
    //   who it is   its own liking of each pose (its seed: the same person likes the same poses after a restart), its age, its nerve, its talk, its nature
    //   the moment  the morning's gathering (arms folded, on its belt, behind its back), cold out of doors (holds itself), tired or by the
    //               fire on a seat (elbows on its knees)
    //   or none     now and then its hands just hang: Free is a choice too
    // A seat's and the ground's are other clips: the hips half a metre lower, the knees elsewhere (measured: raven npc gait).
    // A new pose is a line of Poses, its three clips a clip's parts («<clip>.in|loop|out», assets/story/clips). A clip not there is passed over.
    // Only a resident, at its ease (a whim's stay, its day: never an order, a fight, a scene, a job — save a guard standing its watch), standing still or seated,
    // never on its guard (NpcAlert: a fight, a fright, just fought — then the game's own ready stance). What is in its hands picks the poses (Pose.Hands,
    // NpcGear.HandsOf: free, or the game's stance for its weapon — onehanded, onehanded+shield, knives, greatsword, atgeir, bow…): a guard on duty keeps its weapon out (NpcGear.InHand) and stands at its ease with it — a pose made
    // for a weapon in hand (the user 2026-11-08: «зброя завжди напоготові, просто поза спокійна»); none made yet for what it holds: the game's stance.
    // Its hands change under a pose (a weapon drawn or put away): that pose is over. What takes its place: anything — a gesture, a scene, a clip of a
    // place's; it walks off: the core lets the loop go (NpcBody.LoopOff). The motor goes on over a pose (NpcGait: its top half only, NpcBody.InPose).
    // Only while a player sees it (NpcClock.Seen): a pose is the dearest of what its bones do (hand targets each frame), and nobody far sees it.
    // Where it is run (NpcActor.Ticks): a look every LookEvery s a person, a few compares; a choice once in HoldMin…HoldMax s; a clip's own word
    // to the ZDO only as it changes. Session only: a reload or a new owner picks anew (the clip's term ends what was on).
    internal static class NpcStance
    {
        internal static float LookEvery = 1f, StillFor = 3f, HoldMin = 20f, HoldMax = 60f, GapMin = 5f, GapMax = 25f, Free = 0.8f;

        internal enum Where { Stand, Chair, Ground }

        internal sealed class Pose
        {
            public string Id = "";
            public bool Stand, Chair, Ground;
            public string Hands = NpcGear.HandsFree; // what its hands hold in it (NpcGear.HandsOf): a pose with a weapon says which
            public string InPart = "in", OutPart = "out"; // (an older clip's own names of its parts)
            public string In { get { return Id + "." + InPart; } }
            public string Loop { get { return Id + ".loop"; } }
            public string Out { get { return Id + "." + OutPart; } }
            public float Old, Brave, Quiet, Morning, Cold, Tired, Fire; // what adds to its worth
            public bool OnlyCold;
        }

        internal static readonly List<Pose> Poses = new List<Pose>
        {
            new Pose { Id = "arms_cross", InPart = "folds", OutPart = "unfolds", Stand = true, Quiet = 0.4f, Morning = 1f },
            new Pose { Id = "hands_back", Stand = true, Old = 0.8f, Morning = 0.8f },
            new Pose { Id = "hands_belt", Stand = true, Brave = 0.6f, Morning = 0.8f },
            new Pose { Id = "hand_belt", Stand = true, Brave = 0.3f },
            new Pose { Id = "hug_self", Stand = true, Cold = 3f, OnlyCold = true },
            new Pose { Id = "sit_elbows", Chair = true, Tired = 1.2f, Fire = 0.6f },
            new Pose { Id = "sit_hands", Chair = true, Old = 0.4f },
            new Pose { Id = "ground_back", Ground = true, Fire = 0.3f },
            new Pose { Id = "ground_knee", Ground = true, Tired = 1.2f, Fire = 0.3f },
            // a weapon in hand (a guard on duty, the user 2026-11-08): the ones carried on the shoulder, over the game's stance for them. The others stand as with a sword
            // at their ease (NpcGear.Stance: the game's own calm stance), the game's stance of a sword, a sword and shield, a bow, a spear is calm already: none needed; the atgeir is carried on the shoulder (NpcGear.Carry)
            new Pose { Id = "guard_greatsword_rest", Stand = true, Hands = "greatsword" },
            new Pose { Id = "guard_club2h_rest", Stand = true, Hands = "twohandedclub" },

        };
        internal static float ArmedStill = 0.5f, ArmedHoldMin = 120f, ArmedHoldMax = 300f; // (a weapon in hand: its calm pose at once, held long — the game's ready stance only on its guard)

        private sealed class State { public float Next, Still, StageAt, Until; public Pose On; public int Stage; }
        private static readonly Dictionary<NpcActor, State> States = new Dictionary<NpcActor, State>(); // session: off when it goes (Forget)
        internal static void Forget(NpcActor a) { States.Remove(a); }

        private static float H(uint seed, string id) { uint h = seed ^ (uint)id.GetStableHashCode(); h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; return (h & 0xFFFFu) / 65535f; }

        private static bool May(NpcActor a, out Where where)
        {
            where = Where.Stand;
            NpcMind m = a.Mind;
            NpcBody b = a.Body;
            if (m == null || b == null || !NpcClock.Seen(a) || !NpcResident.Is(a) || m.Asleep || m.NowLayer > NpcMind.Layer.Whim || !b.ClipFree || NpcAlert.On(a)) return false;
            bool watch = m.NowLayer == NpcMind.Layer.Job && Guard.Hired(a); // (a guard at its post or a stop of its rounds: its watch is standing at its ease)
            if (m.NowLayer == NpcMind.Layer.Job && !watch) return false;
            if (m.SeatedNow && m.Now == NpcMind.Task.Sit) { where = b.HeldGesture == "sit" ? Where.Ground : Where.Chair; return b.SeatsNow; }
            if ((m.Now != NpcMind.Task.Idle && !(watch && m.Now == NpcMind.Task.Work)) || b.HeldGesture.Length > 0) return false;
            Character c = a.GetComponent<Character>();
            return c != null && c.GetVelocity().magnitude < 0.2f && c.IsOnGround();
        }

        internal static void Tick(NpcActor a)
        {
            State s;
            if (!States.TryGetValue(a, out s)) States[a] = s = new State { Next = Time.time + Random.Range(0f, GapMax) };
            if (Time.time < s.StageAt) return;
            s.StageAt = Time.time + LookEvery;
            if (s.On != null) { Going(a, s); return; }
            if (a.Body != null && a.Body.PoseSaid) a.Body.PoseWord(false); // (come to be run here, or a reload: the word of a pose nobody here took)
            Where where;
            if (!May(a, out where)) { s.Still = 0f; return; }
            if (s.Still <= 0f) s.Still = Time.time;
            bool armed = NpcGear.HandsOf(a) != NpcGear.HandsFree;
            if ((!armed && Time.time < s.Next) || Time.time - s.Still < (armed ? ArmedStill : StillFor)) return;
            s.Next = Time.time + Random.Range(GapMin, GapMax);
            Pose p = Pick(a, where);
            if (p == null || BodyClips.Find(p.In) == null || BodyClips.Find(p.Loop) == null || BodyClips.Find(p.Out) == null) return; // (hands just hang a while; a clip not made: passed over)
            PAnimClip c = BodyClips.Find(p.In);
            if (!a.Body.Clip(p.In, c.Length + 2f)) return;
            a.Body.PoseWord(true);
            s.On = p; s.Stage = 1; s.StageAt = Time.time + c.Length;
            s.Until = Time.time + c.Length + (armed ? Random.Range(ArmedHoldMin, ArmedHoldMax) : Random.Range(HoldMin, HoldMax));
            Npcs.Log(a.Who + ": stands «" + p.Id + "»");
        }

        // its way in, the while, its way out; something else on its body: this one is over, whatever it was. It may not go on: at once, no way
        // out (a seated one's way out reaches for knees that have risen), and what a scene or a fight put on is left alone
        private static void Going(NpcActor a, State s)
        {
            Pose p = s.On;
            BodyAnimator b = BodyAnimator.Of(a.GetComponent<Character>());
            string now = b != null ? b.ClipIn(NpcBody.ClipSlot) : null;
            bool ours = now == p.In || now == p.Loop || now == p.Out;
            if (!ours || !a.Body.ClipBusy) { Over(a, s); return; }
            Where where;
            if (s.Stage < 3 && (!May(a, out where) || NpcGear.HandsOf(a) != p.Hands)) // (its hands changed under it: a weapon drawn or put away)
            {
                if (a.Mind == null || a.Mind.NowLayer <= NpcMind.Layer.Whim || NpcAlert.On(a)) a.Body.Clip(""); // (on its guard: its own pose off at once — the fight's stance)
                Over(a, s);
                return;
            }
            if (s.Stage == 1) { float hold = s.Until - Time.time; a.Body.Clip(p.Loop, hold + 5f); s.Stage = 2; return; }
            if (s.Stage == 2 && Time.time >= s.Until)
            {
                PAnimClip o = BodyClips.Find(p.Out);
                a.Body.Clip(p.Out, o != null ? o.Length + 1f : 2f);
                s.Stage = 3; s.StageAt = Time.time + (o != null ? o.Length : 1f);
                return;
            }
            if (s.Stage == 3) Over(a, s);
        }

        private static void Over(NpcActor a, State s) { if (a.Body != null) a.Body.PoseWord(false); s.On = null; s.Stage = 0; s.Still = 0f; s.Next = Mathf.Max(s.Next, Time.time + Random.Range(GapMin, GapMax)); }

        // by its liking and by the moment; Free: nothing
        private static Pose Pick(NpcActor a, Where where)
        {
            uint seed = NpcKit.Seed(a.Zdo);
            NpcPersona w = NpcPersona.Of(a);
            NpcSettlers.Look look = NpcSettlers.Of(a);
            bool old = look != null && look.Age == 2;
            NpcMind.Plan whim = a.Mind.Of(NpcMind.Layer.Whim);
            bool morning = whim != null && whim.Whose == NpcLife.Morning;
            NpcGait g = a.GetComponent<NpcGait>();
            float cold = g != null ? g.Wished("cold") : 0f, tired = NpcToil.Tired(a) / 100f;
            bool fire = NpcPlaces.VisitKind(a) == "fire";
            string hands = NpcGear.HandsOf(a);
            float free = hands == NpcGear.HandsFree ? Free : 0f, sum = free, roll; // (a weapon in hand: never the game's ready stance for a while — its own calm one)
            List<KeyValuePair<Pose, float>> can = new List<KeyValuePair<Pose, float>>();
            foreach (Pose p in Poses)
            {
                if (!(where == Where.Stand ? p.Stand : where == Where.Chair ? p.Chair : p.Ground) || p.Hands != hands) continue;
                if (p.OnlyCold && cold < 0.5f) continue;
                float v = 0.2f + H(seed, p.Id) * 1.2f + (old ? p.Old : 0f) + w.Bravery * p.Brave + (1f - w.Chatty) * p.Quiet + (morning ? p.Morning : 0f) + cold * p.Cold + (tired > 0.6f ? p.Tired : 0f) + (fire ? p.Fire : 0f);
                v *= NpcNature.Pose(w.Nature, p.Id); // (its nature: NpcNature)
                can.Add(new KeyValuePair<Pose, float>(p, v));
                sum += v;
            }
            roll = Random.value * sum - free;
            foreach (KeyValuePair<Pose, float> c in can) { if (roll < 0f) return null; roll -= c.Value; if (roll < 0f) return c.Key; }
            return null;
        }

        internal static void Wire()
        {
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
        }
    }
}
#endif
