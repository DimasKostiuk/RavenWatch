#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // A resident's hunger (npc-plan.md, «Голод і їдальня»): nobody works at a base for nothing. Its fullness (0–100) goes
    // down while it is about (loaded here — a base nobody is near sleeps, and so does its hunger), slower asleep; a
    // meal from the base's pantry (NpcMeals) fills it up again. By how full it is:
    //   35+      as ever
    //   15–35    hungry: works slower, grumbles now and then, goes to eat in the day when there is food
    //   0–15     starving: drops its work, grumbles more
    //   0, long  (nine game days where it is about — the user: it wanders the base hungry first) — leaves the base:
    //            unhired, unsettled, its gear at its feet
    // It never dies of it. Kept in its ZDO only every two minutes and at a meal (one float: no traffic for a ticking
    // bar); the owner counts it, every other game reads what is kept. Only residents get hungry.
    internal sealed class NpcHunger : MonoBehaviour
    {
        internal static readonly int KFull = "RavenWatch_npcFull".GetStableHashCode();       // float 0–100
        internal static readonly int KStarved = "RavenWatch_npcStarved".GetStableHashCode(); // float: seconds at nothing
        private const float PerDay = 60f, Hungry = 35f, Starving = 15f, LeaveDays = 9f, KeepEvery = 120f, AsleepSlower = 0.5f;
        private static float DayLength { get { return EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f; } }

        private NpcActor _a;
        private bool _read;
        private float _full = 100f, _kept = 100f, _starved, _keptStarved, _last, _nextKeep, _nextMutter;
        internal float LastMeal = -1000f; // (Time.time: a second helping only when still hungry)

        private void Awake() { _a = GetComponent<NpcActor>(); _nextMutter = Time.time + Random.Range(120f, 300f); }

        // ---------------------------------------------------------------- what the others read
        internal static float Full(NpcActor a)
        {
            NpcHunger h = a != null ? a.Hunger : null;
            if (h != null && h._read && a.Owned) return h._full;
            ZDO z = a != null ? a.Zdo : null;
            return z != null ? NpcLocker.Num(z, KFull, 100f) : 100f;
        }
        internal static bool IsHungry(NpcActor a) { return NpcResident.Is(a) && a.Def.Settler && Full(a) < Hungry; }
        internal static bool IsStarving(NpcActor a) { return NpcResident.Is(a) && a.Def.Settler && Full(a) < Starving; }
        internal static float Slow(NpcActor a) { return IsHungry(a) ? 1.4f : 1f; } // × its work step

        // how much of its fullness a game day takes, as its day usually goes (the board's forecast, NpcMeals.Supply): twelve hours at its work's load, four about, eight asleep
        internal static float NeedPerDay(NpcActor a) { return PerDay * (12f * NpcToil.WorkLoad(a) + 4f + 8f * AsleepSlower) / 24f; }

        // its hunger in a word and how bad (its card, the board): tone 0 fine · 2 hungry · 3 starving
        internal static int Tone(NpcActor a) { float f = Full(a); return f < Starving ? 3 : f < Hungry ? 2 : 0; }
        internal static string Word(NpcActor a)
        {
            float f = Full(a);
            return a.Form(f < Starving ? Lang.T("дуже голодний|голодна") : f < Hungry ? Lang.T("голодний|голодна") : f < 70f ? Lang.T("не голодний|голодна") : Lang.T("ситий|сита"));
        }

        // ---------------------------------------------------------------- where it is owned
        private void Update()
        {
            if (_a == null || !_a.Owned) { _read = false; return; }
            ZDO z = _a.Zdo;
            if (z == null) return;
            if (!_read) { if (!NpcLocker.Ready(_a)) return; _read = true; _full = _kept = NpcLocker.Num(z, KFull, 100f); _starved = _keptStarved = NpcLocker.Num(z, KStarved, 0f); _last = Time.time; return; } // (taken over: from what was kept)
            if (Time.time - _last < 1f) return;
            float dt = Time.time - _last; _last = Time.time;
            if (!NpcResident.Is(_a) || !_a.Def.Settler) return; // (a person of the story has no pantry to go to: it does not hunger)
            bool asleep = _a.Mind != null && _a.Mind.Asleep;
            bool fed = _full >= Starving;
            _full = Mathf.Max(0f, _full - PerDay / DayLength * dt * (asleep ? AsleepSlower : 1f) * NpcToil.HungerLoad(_a)); // (work makes it hungry: NpcToil)
            if (fed && _full < Starving) NpcMemory.Remember(_a, "starved", 0L, ""); // (the day it went without: remembered once each time)
            _starved = _full <= 0f ? _starved + dt : 0f;
            if (_starved > LeaveDays * DayLength && !NpcLevers.IsProtected(_a)) { Leave(); return; } // (the story holds it: hungry, but it stays)
            if (Time.time >= _nextKeep && (Mathf.Abs(_full - _kept) > 0.5f || _starved - _keptStarved > 60f)) Keep(); // (at nothing the days without food are kept too)
            if (_full < Hungry && !asleep && Time.time >= _nextMutter)
            {
                _nextMutter = Time.time + Random.Range(240f, 540f) * (1.5f - NpcPersona.Of(_a).Chatty); // (a chatty one grumbles more often)
                NpcChatter.Mutter(_a, _full < Starving ? "starving" : "hungry");
            }
        }

        private void Keep()
        {
            ZDO z = _a.Zdo;
            if (z == null) return;
            _nextKeep = Time.time + KeepEvery; _kept = _full; _keptStarved = _starved;
            NpcLocker.Put(z, KFull, _full);
            NpcLocker.Put(z, KStarved, _starved);
        }

        // a meal: filled by what it ate (NpcMeals)
        internal string LastFood = ""; // (what it ate last, its name as shown: its words)

        internal void Eat(float fill, string food = "")
        {
            if (!_read) return;
            LastFood = food ?? "";
            NpcToil toil = GetComponent<NpcToil>(); if (toil != null) toil.Ate(LastFood); // (its mood minds the same food every time)
            _full = Mathf.Min(100f, _full + fill); _starved = 0f; LastMeal = Time.time;
            if (_a != null && _a.Vitals != null) _a.Vitals.Fed(); // (a meal heals a little)
            Keep();
        }

        // for testing (the console): how full it is now
        internal void SetFull(float f) { if (!_read) return; _full = Mathf.Clamp(f, 0f, 100f); _starved = 0f; Keep(); }

        // starved too long: it goes (it was not fed — the user: no deaths but the story's)
        private void Leave()
        {
            NpcChatter.Mutter(_a, "leave", NpcActor.Voice.Event);
            Npcs.Log(_a.Id + ": starved too long — leaves the base");
            StoryBus.Raise("npc_left_hungry", _a.Id, 0, transform.position);
            _full = 60f; _starved = 0f; Keep(); // (taken in again some day: not at once at its end)
            _a.Act("unhire"); // (unsettled too)
            Vector3 hall = NpcTravel.Refuge;
            _a.Order("travel " + Npcs.V(hall) + " " + NpcTravel.ToRefuge); // (to the ravens' home: hired again from there)
        }
    }
}
#endif
