#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // «Адаптивні ворони»: now and then a raven leaves the shoulder by itself to stretch its wings. It flies along behind
    // you, circles above you (wider and higher over a ship under sail), or on the road darts ahead to scout and comes
    // back with word of what it saw; rarely both go up together and circle opposite ways. The animation director
    // (AnimDirector.Schedule) decides when and how; this starts and ends them. Never underground, under a roof or in
    // a fight: they are called back early when something happens. What it says on going off asks the dialogue
    // director first (it is its own idea, not your doing). The owner's game writes the outing on the player's ZDO;
    // every game flies it (ShoulderPin.AirStep), reading the mode.
    internal static class Outing
    {
        internal const int None = 0, Follow = 1, CircleRight = 2, CircleLeft = 3, WideRight = 4, WideLeft = 5, Scout = 6, Hunt = 7;
        // Hunt (the user, 2026-10-15): Munin chases one of the game's birds near (a gull, a crow) for a few seconds; the bird
        // on the player's ZDO (HuntH/M: "user:id") so every game chases the same one
        internal const string HuntH = "RavenWatch_OutTH", HuntM = "RavenWatch_OutTM";
        internal static float HuntMin = 9f, HuntMax = 14f;
        internal const string KeyH = "RavenWatch_OutH", KeyM = "RavenWatch_OutM";

        internal static bool On = true;
        internal static float FollowMin = 20f, FollowMax = 60f, CircleMin = 20f, CircleMax = 60f; // s (the user, 2026-10-10)
        internal static float RoadMin = 30f, RoadMax = 120f, SailMin = 60f, SailMax = 180f;       // longer on the road, longest at sea
        internal static float ScoutSeconds = 4.5f, ScoutRange = 40f;

        private static readonly float[] Until = { 0f, 0f };
        private static readonly int[] Mode = { None, None };
        internal static float LastAt = -1000f; // when the last outing began (the director counts from it)

        internal static bool IsWide(int m) { return m == WideRight || m == WideLeft; }
        internal static bool Clockwise(int m) { return m == CircleRight || m == WideRight; }

        internal static int Of(Player p, bool munin)
        {
            ZDO z = NetFlag.Zdo(p, false);
            return z != null ? z.GetInt(munin ? KeyM : KeyH, None) : None;
        }

        private static void Set(Player p, bool munin, int mode)
        {
            ZDO z = NetFlag.Zdo(p, true);
            string key = munin ? KeyM : KeyH;
            if (z != null && z.GetInt(key, None) != mode) z.Set(key, mode);
            Mode[munin ? 1 : 0] = mode;
        }

        internal static float RestStrength = 5f; // strength for an outing that ran its time (none when called back early)

        internal static bool Indoors() { return Senses.Underground || Senses.Indoors; }

        // From Shoulder.Tick (twice a second, after Senses): an outing ends on time, or sooner when something happens
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || (Of(p, false) == None && Of(p, true) == None)) return;
            string why = null;
            if (p.IsDead()) why = "you died";
            else if (Indoors()) why = "you went in";
            else if (Time.time - Senses.HurtAt < 0.6f) why = "you were hurt";
            else if (Senses.Health01 < 0.4f) why = "your health is low";
            else if (Senses.Jumped) why = "you went far at once";
            else if (Senses.Swimming) why = "you swim";
            else if (Senses.Sleeping) why = "you sleep";
            else if (Senses.Fight) why = "a fight";
            for (int i = 0; i < 2; i++)
            {
                bool munin = i == 1;
                if (Of(p, munin) == None) continue;
                if (why == null && Time.time < Until[i]) continue;
                int was = Mode[i];
                Set(p, munin, None);
                if (why != null) { if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Raven anim: " + (munin ? "Munin" : "Hugin") + " called back early: " + why); }
                else
                {
                    if (was == Scout) Report(p, munin); // back from scouting: what it saw ahead
                    else Quirks.OutingDone(p, munin);  // «Сорока»: something brought back
                    Power.Rested(munin, RestStrength * Norov.RestMul(RavenSeat.On(p, munin))); // an outing that ran its time rests the wings (knack «Легке крило»: more)
                }
            }
        }

        // could a raven go off now: you calm, out in the open, healthy enough
        internal static bool CanGo(Player p)
        {
            return p != null && !p.IsDead() && !Indoors() && !Senses.Fight && !Shoulder.Hovering(p) && !Senses.Swimming && !Senses.Sleeping
                && Senses.Health01 >= 0.5f;
        }

        // sitting quietly on the shoulder and awake
        internal static bool ReadyRaven(Player p, bool munin)
        {
            GameObject model = Shoulder.ModelOf(p, munin);
            if (model == null || Of(p, munin) != None) return false;
            if (!munin && Shoulder.HuginAway) return false;
            ShoulderPin pin = model.GetComponent<ShoulderPin>();
            ShoulderLife life = model.GetComponent<ShoulderLife>();
            if (pin == null || !pin.enabled || AnimDirector.Of(model) != RavenAct.Sit || life == null) return false;
            return life.Level > 0 && !life.Sleepy && !Satiety.StuffedWorn(munin) // stuffed: too lazy to fly about
                && !Quirks.KeepsHome(p, munin); // its quirk: sulking, asleep at night, freezing
        }

        // how long it stays out: by where you are, × its character (Norov: a homebody shorter, a wanderer longer)
        private static float Length(int mode, ItemDrop.ItemData item)
        {
            if (mode == Scout) return ScoutSeconds;
            if (mode == Hunt) return Random.Range(HuntMin, HuntMax);
            float k = Norov.OutLength(item);
            if (Senses.Sailing) return Random.Range(SailMin, SailMax) * k;
            if (Senses.Traveling) return Random.Range(RoadMin, RoadMax) * k;
            return (mode == Follow ? Random.Range(FollowMin, FollowMax) : Random.Range(CircleMin, CircleMax)) * k;
        }

        internal static bool Start(Player p, bool munin, int mode)
        {
            if (p == null || Shoulder.ModelOf(p, munin) == null || Indoors()) return false;
            LastAt = Time.time;
            Set(p, munin, mode);
            Until[munin ? 1 : 0] = Time.time + Length(mode, RavenSeat.On(p, munin));
            Nicks.OnOuting(p, munin);
            if (mode == Hunt) Say(munin, "out_hunt");
            else if (mode != Scout) Say(munin, mode == Follow ? "out_follow" : IsWide(mode) ? "out_sail" : "out_circle");
            return true;
        }

        // a hunt after this bird (its ZDO id), then the outing as any
        internal static bool StartHunt(Player p, bool munin, ZDOID bird)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return false;
            z.Set(munin ? HuntM : HuntH, bird.UserID + ":" + bird.ID);
            return Start(p, munin, Hunt);
        }

        // the bird it hunts (every game: from the player's ZDO), or null (gone, out of sight)
        internal static Transform Prey(Player p, bool munin)
        {
            ZDO z = NetFlag.Zdo(p, false);
            string raw = z != null ? z.GetString(munin ? HuntM : HuntH, "") : "";
            string[] w = raw.Split(':');
            long user; uint id;
            if (w.Length != 2 || !long.TryParse(w[0], out user) || !uint.TryParse(w[1], out id) || ZNetScene.instance == null) return null;
            GameObject g = ZNetScene.instance.FindInstance(new ZDOID(user, id));
            return g != null ? g.transform : null;
        }

        internal static bool StartBoth(Player p)
        {
            if (p == null || Shoulder.ModelOf(p, false) == null || Shoulder.ModelOf(p, true) == null || Indoors()) return false;
            LastAt = Time.time;
            bool hRight = Random.value < 0.5f, wide = Senses.Sailing;
            float len = Length(CircleRight, null); // together: one length, × each one's character
            Set(p, false, hRight ? (wide ? WideRight : CircleRight) : (wide ? WideLeft : CircleLeft));
            Set(p, true, hRight ? (wide ? WideLeft : CircleLeft) : (wide ? WideRight : CircleRight));
            Until[0] = Time.time + len * Norov.OutLength(RavenSeat.On(p, false));
            Until[1] = Time.time + len * Norov.OutLength(RavenSeat.On(p, true)) + Random.Range(0.5f, 2.5f);
            Say(Random.value < 0.5f, "out_both");
            Nicks.OnOuting(p, false); Nicks.OnOuting(p, true);
            return true;
        }

        // back from scouting: an enemy ahead (named), or the way is clear
        private static void Report(Player p, bool munin)
        {
            Character foe = Senses.Ahead(p, ScoutRange * Norov.ScoutMul(RavenSeat.On(p, munin))); // (knack «Пильне око»)
            Composer.Extra.Clear();
            if (foe != null) Composer.Extra["target"] = Localization.instance != null ? Lang.Game(foe.m_name) : foe.m_name;
            Say(munin, foe != null ? "out_scout_enemy" : "out_scout_clear", true);
            Composer.Extra.Clear();
        }

        // its own idea: the dialogue director decides whether it may speak now (else it goes quietly)
        private static void Say(bool munin, string tag) { Say(munin, tag, false); }
        private static void Say(bool munin, string tag, bool news)
        {
            Director.Voice v = news ? Director.Voice.Event : Director.Voice.Shoulder;
            if (!Director.May(v)) return;
            RavenSkills.StrikeSay(munin, tag);
            Director.Spoke(v, 4f);
        }

        // the director: a stronger act took the bird from its outing: called off (it would fly off again after)
        internal static void Interrupted(GameObject model)
        {
            ShoulderPin pin = model != null ? model.GetComponent<ShoulderPin>() : null;
            if (pin == null || !ReferenceEquals(pin.Player, Player.m_localPlayer)) return;
            Set(pin.Player, pin.Munin, None);
            Until[pin.Munin ? 1 : 0] = 0f;
        }

        // the panel's «Повернути»: both home now
        internal static void Recall(Player p) { if (p == null) return; Set(p, false, None); Set(p, true, None); }
    }
}
#endif
