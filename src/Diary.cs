using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    internal enum Ev
    {
        Death, Kill, NearDeath, Fall, Swim, Travel, Biome, Dungeon,
        Chop, Mine, Build, Craft, Cook, Eat, Hungry, Night, Level, Afk,
        Escaped, Flee, Friendly, Portal, Sail, Sleep, Pick, Loot, Encumbered, Pet, Fish,
        Raid, Plant, Trade, Ride, Parry, Dodge, Bow, Magic, Smelt, Ferment, Repair, Potion, Sit, Cozy, Trophy, TrophyHang, Cart, Fuel,
        PortalBlocked, Broke, Tame, Freezing, Poison, Burning // ore at a portal, a broken thing, a tamed beast, the body in trouble
    }

    internal struct Entry
    {
        public float Time;
        public Ev Type;
        public string Arg;   // creature token, hit type or biome
        public int Value;    // meters, level, boss flag...
    }

    // What Hugin knows about the player's last minutes. Lives only in memory on this PC.
    internal static class Diary
    {
        private const int MaxEntries = 4000;
        private const int MaxTracked = 32;
        private static readonly List<Entry> Entries = new List<Entry>();

        private static Vector3 _lastPos;
        private static Quaternion _lastLook;
        private static bool _havePos;
        private static Heightmap.Biome _lastBiome;
        internal const string LandsKey = "RavenWatch_lands"; // (also read by the mast's lookout: WorldVisit.NewLand)
        private const string PlacesKey = "RavenWatch_places";

        // the lands this character has stood in (a handful of names, in the character); a character from before this
        // takes them from the first-visit list of the ravens' strength, so lands already walked are not new again
        private static bool FirstTimeIn(Player p, Heightmap.Biome biome)
        {
            if (p == null || p.m_customData == null) return false;
            string raw;
            if (!p.m_customData.TryGetValue(LandsKey, out raw))
            {
                raw = "|";
                string places;
                if (p.m_customData.TryGetValue(PlacesKey, out places) && places != null)
                    foreach (string part in places.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                        if (part.StartsWith("biome:", StringComparison.Ordinal)) raw += part.Substring(6) + "|";
            }
            string name = biome.ToString();
            bool fresh = !raw.Contains("|" + name + "|");
            if (fresh) raw += name + "|";
            p.m_customData[LandsKey] = raw;
            return fresh && biome != Heightmap.Biome.Meadows && biome != Heightmap.Biome.Ocean;
        }
        private static bool _wasInterior;
        private static int _lastLevel = -1;
        private static float _nearDeathAt = -999f;
        private static string _nearDeathBy;
        private static bool _nearDeathPending;

        // animals we wounded, and creatures that wounded us: watched for a short while
        private sealed class Watch { public Character Who; public string Token; public float Since; public float LastHit; }
        private static readonly List<Watch> Prey = new List<Watch>();
        private static readonly List<Watch> Foes = new List<Watch>();

        internal static float WindowSeconds = 600f;
        internal static float LastActive;
        private const float AfkAfterSeconds = 60f;

        // any key, an open window, moving or turning: the player is here
        internal static void MarkActive() { LastActive = Time.time; }
        internal static float KeepSeconds = 1260f; // the longest look-back any mode needs, plus a little

        internal static void Reset()
        {
            Entries.Clear();
            Prey.Clear();
            Foes.Clear();
            _havePos = false;
            LastActive = Time.time;
            _lastBiome = Heightmap.Biome.None;
            _wasInterior = false;
            _lastLevel = -1;
            _nearDeathPending = false;
        }

        // after Hugin has spoken: forget what he already commented on, keep only the position trackers
        internal static void ClearEvents()
        {
            Entries.Clear();
            Prey.Clear();
            Foes.Clear();
            _nearDeathPending = false;
        }

        // a piece built by the player here (BuildPatch): from — what it was made from, made — the piece itself (null: the game made none)
        internal static void Placed(Player p, Piece from, Piece made, bool cheated)
        {
            bool plant = from.GetComponent<Plant>() != null;
            Add(plant ? Ev.Plant : Ev.Build, "", 0); // (a seed in the ground is gardening, not building)
#if RAVEN_ITEM
            if (plant && !cheated) HuginChores.OnPlanted();
#if RAVEN_STORY
            StoryHeart.Placed(p, from);
#endif
#if RAVEN_NPC
            if (made != null) StoryBus.Raise("piece_placed", Utils.GetPrefabName(made.gameObject), 0, made.transform.position);
            NpcPromises.Built(p, made); // (what you built may be what you promised someone)
#endif
#endif
        }

        internal static void Add(Ev type, string arg, int value)
        {
#if RAVEN_ITEM
            FightJudge.On(type); // (the fight being judged: parries, dodges, potions, running, a close call)
            Habits.Happened(type); // («Звички»: deaths, a night's sleep)
#endif
            Entry e;
            e.Time = Time.time;
            e.Type = type;
            e.Arg = arg ?? "";
            e.Value = value;
            Entries.Add(e);
            if (IsAction(type)) LastActive = Time.time;
#if RAVEN_ITEM
            RavenTalk.OnEvent(type, e.Arg, value); // something the ravens may talk about later
            Nicks.OnEvent(type, value);            // and a deed for their nicknames
            SitEvents.FromDiary(type);             // and how they react on the shoulder (the sitting system)
            Errands.OnEvent(type, e.Arg, value);   // and Odin's errand, if it waits for this
            Requests.OnEvent(type, e.Arg, value);  // and who hurt you (Munin's wish of revenge)
            Words.OnEvent(type);                   // and a taught word for the moment (you ate, sat by a fire)
#if RAVEN_STORY
            StoryBus.FromDiary(type, e.Arg, value); // and the story
#endif
#endif
            Prune();
        }

        private static bool IsAction(Ev t)
        {
            switch (t)
            {
                case Ev.Travel: case Ev.Afk: case Ev.Swim: case Ev.Night: case Ev.Hungry: case Ev.Encumbered:
                case Ev.Sail: case Ev.Biome: case Ev.Dungeon: case Ev.Level: case Ev.Escaped: case Ev.Flee:
                case Ev.Raid: case Ev.Cozy: case Ev.Freezing: case Ev.Poison: case Ev.Burning:
                    return false;
                default: return true;
            }
        }

        private static void Prune()
        {
            float cutoff = Time.time - Math.Max(KeepSeconds, 120f);
            int drop = 0;
            while (drop < Entries.Count && Entries[drop].Time < cutoff) drop++;
            if (Entries.Count - drop > MaxEntries) drop = Entries.Count - MaxEntries;
            if (drop > 0) Entries.RemoveRange(0, drop);
        }

        // ---------------------------------------------------------------- watched creatures

        internal static void WoundedAnimal(Character c)
        {
            if (c == null) return;
            foreach (Watch w in Prey) if (w.Who == c) { w.LastHit = Time.time; return; }
            if (Prey.Count >= MaxTracked) Prey.RemoveAt(0);
            Watch n = new Watch();
            n.Who = c; n.Token = HitInfo.Token(c); n.Since = Time.time; n.LastHit = Time.time;
            Prey.Add(n);
        }

        internal static void HurtBy(Character c)
        {
            if (c == null) return;
            foreach (Watch w in Foes) if (w.Who == c) { w.LastHit = Time.time; return; }
            if (Foes.Count >= MaxTracked) Foes.RemoveAt(0);
            Watch n = new Watch();
            n.Who = c; n.Token = HitInfo.Token(c); n.Since = Time.time; n.LastHit = Time.time;
            Foes.Add(n);
        }

        // a kill of something we were watching: it neither escaped nor chased us off
        internal static void Killed(Character c, string token)
        {
            for (int i = Prey.Count - 1; i >= 0; i--)
                if ((c != null && Prey[i].Who == c) || (c == null && Prey[i].Token == token)) { Prey.RemoveAt(i); if (c == null) break; }
            for (int i = Foes.Count - 1; i >= 0; i--)
                if ((c != null && Foes[i].Who == c) || (c == null && Foes[i].Token == token)) { Foes.RemoveAt(i); if (c == null) break; }
        }

        private static void CheckWatched(Vector3 pos)
        {
            for (int i = Prey.Count - 1; i >= 0; i--)
            {
                Watch w = Prey[i];
                float age = Time.time - w.LastHit;
                if (w.Who == null) { if (age > 3f) Prey.RemoveAt(i); continue; } // unloaded or died elsewhere
                if (w.Who.IsDead()) { Prey.RemoveAt(i); continue; }
                if (age > 15f)
                {
                    if (Vector3.Distance(pos, w.Who.transform.position) > 25f)
                    {
                        Add(Ev.Escaped, w.Token, 0);
#if RAVEN_ITEM
                        Memory.Recent(Player.m_localPlayer, "escaped", w.Token, 0, 2);
#endif
                    }
                    Prey.RemoveAt(i);
                }
            }
            for (int i = Foes.Count - 1; i >= 0; i--)
            {
                Watch w = Foes[i];
                float age = Time.time - w.LastHit;
                if (w.Who == null || w.Who.IsDead() || age > 40f) { Foes.RemoveAt(i); continue; }
                if (Vector3.Distance(pos, w.Who.transform.position) > 45f)
                {
                    Add(Ev.Flee, w.Token, 0);
#if RAVEN_ITEM
                    Memory.Recent(Player.m_localPlayer, "fled", w.Token, 0, 2);
#endif
                    Foes.RemoveAt(i);
                }
            }
        }

        // ---------------------------------------------------------------- sampling, every few seconds

        internal static void Sample(Player p)
        {
            if (p == null || p.IsDead()) { _havePos = false; return; }
            Vector3 pos = p.transform.position;
            Quaternion look = p.GetLookYaw();

            if (_havePos)
            {
                float moved = Vector3.Distance(pos, _lastPos);
                if (moved > 0.5f || Quaternion.Angle(look, _lastLook) >= 2f || p.InPlaceMode()) LastActive = Time.time;
                if (moved < 100f) // a portal or respawn is not travel
                {
                    if (moved > 0.5f) Add(Ev.Travel, "", Mathf.RoundToInt(moved));
                    else if (Time.time - LastActive >= AfkAfterSeconds && !p.IsSleeping()) Add(Ev.Afk, "", 0);
                }
            }
            _lastPos = pos;
            _lastLook = look;
            _havePos = true;

#if RAVEN_ITEM
            bool shared = Senses.Ready; // the situation already gathered for everyone (Senses): the same checks, made once
#else
            bool shared = false;
#endif
            if (shared ? Senses.Swimming : p.IsSwimming()) Add(Ev.Swim, "", 0);
            if (shared ? Senses.AtHelm : p.GetControlledShip() != null) Add(Ev.Sail, "", 0);
            if (shared ? Senses.Encumbered : p.IsEncumbered()) Add(Ev.Encumbered, "", 0);
            SampleLife(p);
            SampleBody(p);

            Heightmap.Biome biome = shared ? Senses.Biome : p.GetCurrentBiome();
            if (biome != _lastBiome && biome != Heightmap.Biome.None)
            {
                if (_lastBiome == Heightmap.Biome.None) FirstTimeIn(p, biome); // on entering the world: the list starts with where you stand
                else
                {
                    // the lines for it all speak of a new, harsher land: only the first time here, never home or the sea
                    if (FirstTimeIn(p, biome)) Add(Ev.Biome, biome.ToString(), 0);
#if RAVEN_ITEM
                    else RavenTalk.OnEvent(Ev.Biome, biome.ToString(), 0); // a familiar land: no new-land line, but the ravens may still talk of the crossing
                    Memory.OnBiome(p, biome.ToString());
#endif
                }
                _lastBiome = biome;
            }

            bool interior = shared ? Senses.Underground : p.InInterior();
            if (interior && !_wasInterior)
            {
                Add(Ev.Dungeon, "", 0);
#if RAVEN_ITEM
                Memory.OnDungeon(p);
                Power.OnPlace(p, "dungeon:" + Power.Spot(p.transform.position), Lang.T("нове підземелля"));
#endif
            }
            _wasInterior = interior;

            if ((shared ? Senses.Night && !Senses.Indoors && !Senses.Sleeping : EnvMan.IsNight() && !p.InShelter() && !p.IsSleeping()) && !interior) Add(Ev.Night, "", 0);
            if (p.GetFoods().Count == 0) Add(Ev.Hungry, "", 0);

            int level = PovLevel();
            if (level > 0)
            {
                if (_lastLevel > 0 && level > _lastLevel) Add(Ev.Level, "", level);
                _lastLevel = level;
#if RAVEN_ITEM
                Power.OnPovLevel(p, level);
#endif
            }

            if (_nearDeathPending && Time.time - _nearDeathAt > 8f)
            {
                _nearDeathPending = false;
                if (!p.IsDead())
                {
                    Add(Ev.NearDeath, _nearDeathBy, 0);
#if RAVEN_ITEM
                    Memory.OnNearDeath(p, _nearDeathBy);
                    Power.OnNearDeath();
#endif
                }
            }

            CheckWatched(pos);
#if RAVEN_ITEM
            Memory.Sample(p);
#endif
        }

        // the body in trouble (freezing, poisoned, on fire: when it starts), and a worn thing that just broke (durability to
        // zero: a weapon, a tool, a piece of armour; a torch burning out is no news)
        private static bool _freezing, _poison, _burning;
        private static readonly Dictionary<ItemDrop.ItemData, float> Wear = new Dictionary<ItemDrop.ItemData, float>();
        private static readonly List<ItemDrop.ItemData> WearGone = new List<ItemDrop.ItemData>();

        private static void SampleBody(Player p)
        {
            SEMan se = p.GetSEMan();
            if (se != null)
            {
                bool f = se.HaveStatusEffect(SEMan.s_statusEffectFreezing), po = se.HaveStatusEffect(SEMan.s_statusEffectPoison), b = se.HaveStatusEffect(SEMan.s_statusEffectBurning);
                if (f && !_freezing) Add(Ev.Freezing, "", 0);
                if (po && !_poison) Add(Ev.Poison, "", 0);
                if (b && !_burning) Add(Ev.Burning, "", 0);
                _freezing = f; _poison = po; _burning = b;
            }
            WearGone.Clear();
            foreach (ItemDrop.ItemData item in Wear.Keys) WearGone.Add(item);
            foreach (ItemDrop.ItemData item in p.GetInventory().GetEquippedItems())
            {
                if (item == null || item.m_shared == null || !item.m_shared.m_useDurability || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch) continue;
                float was;
                if (Wear.TryGetValue(item, out was) && was > 0f && item.m_durability <= 0f) Add(Ev.Broke, Lang.Game(item.m_shared.m_name), 0);
                Wear[item] = item.m_durability;
                WearGone.Remove(item);
            }
            foreach (ItemDrop.ItemData item in WearGone) Wear.Remove(item); // taken off or gone: forgotten
        }

        // a raid where you stand (once per raid), a ride on someone's back, a cart behind you, a warm home around you
        private static string _raid;
        private static readonly FieldInfo VagonsField = AccessTools.Field(typeof(Vagon), "m_instances");

        // a cart is hitched to you
        internal static bool Carting(Player p)
        {
            List<Vagon> carts = VagonsField != null ? VagonsField.GetValue(null) as List<Vagon> : null;
            if (carts != null) foreach (Vagon c in carts) if (c != null && c.IsAttached(p)) return true;
            return false;
        }

        private static void SampleLife(Player p)
        {
            RandomEvent raid = RandEventSystem.instance != null ? RandEventSystem.instance.GetActiveEvent() : null;
            string raidName = raid != null ? raid.m_name : null;
            if (raidName != null && raidName != _raid) Add(Ev.Raid, raidName, 0);
            _raid = raidName;

            if (p.IsRiding())
            {
                Sadle saddle = p.GetDoodadController() as Sadle;
                Character mount = saddle != null ? saddle.GetCharacter() : null;
                if (mount != null) Add(Ev.Ride, HitInfo.Token(mount), 0);
            }

            if (Carting(p)) Add(Ev.Cart, "", 0);

            // at home: under a roof with some comfort around, and no raid at the door
            if (raidName == null && !p.InInterior() && p.InShelter() && RawComfort.Of(p) >= 5) Add(Ev.Cozy, "", 0);
        }

        internal static void OnLowHealth(string attackerToken)
        {
            if (Time.time - _nearDeathAt < 30f) return;
            _nearDeathAt = Time.time;
            _nearDeathBy = attackerToken;
            _nearDeathPending = true;
        }

        internal static void OnOwnDeath()
        {
            _nearDeathPending = false;
            Foes.Clear(); // dying is not running away
        }

        // Path of Valheiman character level, if that mod is installed
        private static PropertyInfo _povCurrent;
        private static FieldInfo _povLevel;
        private static bool _povLooked;

        private static int PovLevel()
        {
            try
            {
                if (!_povLooked)
                {
                    _povLooked = true;
                    Type t = AccessTools.TypeByName("PassiveTree.Progress");
                    if (t != null)
                    {
                        _povCurrent = AccessTools.Property(t, "Current");
                        _povLevel = AccessTools.Field(t, "Level");
                    }
                }
                if (_povCurrent == null || _povLevel == null) return 0;
                object cur = _povCurrent.GetValue(null, null);
                return cur != null ? (int)_povLevel.GetValue(cur) : 0;
            }
            catch { return 0; }
        }

        // ---------------------------------------------------------------- summary

        internal sealed class Summary
        {
            public readonly Dictionary<Ev, int> Count = new Dictionary<Ev, int>();
            public readonly Dictionary<Ev, float> Last = new Dictionary<Ev, float>();
            public string LastDeathCause = "";   // hit type name, or "creature"
            public string LastKiller = "";       // creature token
            public string TopKill = "";
            public string LastKill = "";
            public string Boss = "";
            public float BossAt;
            public string NearDeathBy = "";
            public string Escaped = "";
            public string Fled = "";
            public string NewBiome = "";
            public string Trader = "";           // the trader's name token ($npc_haldor...)
            public string Mount = "";            // the creature token of the last ride
            public string Tamed = "";            // the creature token of the last beast tamed
            public string Broke = "";            // the name of the last thing that broke (as the game shows it)
            public int Meters;
            public int Level;
            public int Ticks;

            public int N(Ev e) { int n; return Count.TryGetValue(e, out n) ? n : 0; }
            public float Age(Ev e) { float t; return Last.TryGetValue(e, out t) ? Time.time - t : 9999f; }
        }

        internal static Summary Summarize() { return Summarize(WindowSeconds); }

        internal static Summary Summarize(float windowSeconds)
        {
            Summary s = new Summary();
            float from = Time.time - windowSeconds;
            Dictionary<string, int> kills = new Dictionary<string, int>();
            foreach (Entry e in Entries)
            {
                if (e.Time < from) continue;
                int n;
                s.Count.TryGetValue(e.Type, out n);
                s.Count[e.Type] = n + 1;
                s.Last[e.Type] = e.Time;
                switch (e.Type)
                {
                    case Ev.Death:
                        s.LastDeathCause = e.Value == 1 ? "creature" : e.Arg;
                        s.LastKiller = e.Value == 1 ? e.Arg : "";
                        break;
                    case Ev.Kill:
                        if (e.Value == 1) { s.Boss = e.Arg; s.BossAt = e.Time; }
                        s.LastKill = e.Arg;
                        int k;
                        kills.TryGetValue(e.Arg, out k);
                        kills[e.Arg] = k + 1;
                        break;
                    case Ev.NearDeath: s.NearDeathBy = e.Arg; break;
                    case Ev.Travel: s.Meters += e.Value; s.Ticks++; break;
                    case Ev.Afk: s.Ticks++; break;
                    case Ev.Biome: s.NewBiome = e.Arg; break;
                    case Ev.Level: s.Level = Math.Max(s.Level, e.Value); break;
                    case Ev.Escaped: s.Escaped = e.Arg; break;
                    case Ev.Flee: s.Fled = e.Arg; break;
                    case Ev.Trade: s.Trader = e.Arg; break;
                    case Ev.Ride: s.Mount = e.Arg; break;
                    case Ev.Tame: s.Tamed = e.Arg; break;
                    case Ev.Broke: s.Broke = e.Arg; break;
                }
            }
            int best = 0;
            foreach (KeyValuePair<string, int> kv in kills)
            {
                if (kv.Value > best || (kv.Value == best && kv.Key == s.LastKill)) { best = kv.Value; s.TopKill = kv.Key; }
            }
            return s;
        }
    }

    // ---------------------------------------------------------------- event hooks

    internal static class HitInfo
    {
        private static readonly FieldInfo LastHitField = AccessTools.Field(typeof(Character), "m_lastHit");

        internal static HitData LastHit(Character c)
        {
            return LastHitField != null ? LastHitField.GetValue(c) as HitData : null;
        }

        internal static string Token(Character c)
        {
            if (c == null || string.IsNullOrEmpty(c.m_name)) return "";
            return c.m_name.StartsWith("$") ? c.m_name.Substring(1) : c.m_name;
        }
    }

    [HarmonyPatch(typeof(Player), "OnDeath")]
    internal static class PlayerDeathPatch
    {
        private static void Prefix(Player __instance)
        {
#if RAVEN_ITEM
            BloodRevenge.Died(__instance); // (a player under someone's vendetta: the avenger's game judges)
#endif
            if (__instance != Player.m_localPlayer) return;
            Diary.OnOwnDeath();
#if RAVEN_ITEM
            Power.OnDeath(); // before the tombstone takes the worn raven
#endif
            HitData hit = HitInfo.LastHit(__instance);
            Character attacker = hit != null ? hit.GetAttacker() : null;
            bool creature = attacker != null && attacker != __instance && !(attacker is Player);
            string who = creature ? HitInfo.Token(attacker) : (hit != null ? hit.m_hitType.ToString() : "Undefined");
            Diary.Add(Ev.Death, who, creature ? 1 : 0);
#if RAVEN_ITEM
            Memory.OnDeath(__instance, who, creature, creature && Champions.TrueBoss(attacker)); // still worn: the tombstone has not taken the ravens yet
            PerkFx.OnDeath(__instance, creature ? who : null); // what the blessings and burdens latch at a death
            Nicks.OnDeath(__instance, hit, attacker); // the ravens saw it (a deed for their nicknames)
            BloodRevenge.OnDeath(__instance, attacker); // Munin marks the one that killed you (the skills still whole)
            Guide.Died(__instance);    // the Guide tells what a death costs (once)
#endif
        }
    }

    // whose kill it is when the killing blow names nobody: a modded weapon's bleed, poison or blast often hits with no
    // attacker in it (and a summon or a pet finishing it off names itself) — the last player who hit it within the
    // window gets it. Kept where the creature is simulated (RPC_Damage runs there, as OnDeath does)
    internal static class KillCredit
    {
        private const float Window = 20f;
        private static readonly Dictionary<ZDOID, KeyValuePair<Player, float>> Last = new Dictionary<ZDOID, KeyValuePair<Player, float>>();

        internal static void Hit(Character c, Player p)
        {
            if (Last.Count > 400)
            {
                List<ZDOID> old = new List<ZDOID>();
                foreach (KeyValuePair<ZDOID, KeyValuePair<Player, float>> e in Last) if (Time.time - e.Value.Value > Window) old.Add(e.Key);
                foreach (ZDOID id in old) Last.Remove(id);
            }
            Last[c.GetZDOID()] = new KeyValuePair<Player, float>(p, Time.time);
        }

        // the killer of the creature that just died here (CreatureDeathPatch): its loot is made right after (PerkDropPatch)
        private static Character _lastDead;
        private static Player _lastKiller;
        internal static void Died(Character c, Player killer) { _lastDead = c; _lastKiller = killer; }
        internal static Player KillerOf(Character c) { return c != null && ReferenceEquals(c, _lastDead) ? _lastKiller : null; }

        // a kill of yours, whichever game held the creature: this one (CreatureDeathPatch, with the creature) or another
        // (Plugin.OnKill, told over the net). One list, so a kill counts the same on a server as alone
        internal static void Mine(Character c, string token, int boss, int stars, string prefab, bool tamed, bool raven, bool own, ZDOID id)
        {
            Diary.Killed(c, token);
            Diary.Add(Ev.Kill, token, boss);
#if RAVEN_ITEM
            if (raven && Whirl.Quiet()) return; // («Вихор» past its first blows: the kill gives nothing)
            Player me = Player.m_localPlayer;
            bool animal = c != null ? c.m_faction == Character.Faction.AnimalsVeg : !MuninDeeds.Monster(prefab);
            Memory.OnKill(token, stars);
            if (!tamed && !animal) FightJudge.Kill(stars, boss == 1);
            if (!tamed) Habits.Killed(animal); // («Вояка», «Звіролов»)
            Bath.Killed(); // (Munin: the blood on him)
            Power.OnKill(boss, stars, prefab, tamed, token);
            if (!tamed) HuginChores.OnKilled(id, boss == 1); // (Hugin's gifts helped)
            PerkFx.OnKill(me, boss == 1, tamed); // «Кровожер», «Кривава учта», «Занудьгував»
            ErrandHow.Dying = id; // (the way it was killed: by this one's own blows)
            try { if (!tamed) Errands.OnKill(prefab, token, stars, boss == 1, raven); }
            finally { ErrandHow.Dying = ZDOID.None; }
            if (tamed) return;
            Requests.OnKill(prefab, token, stars, boss == 1, raven, own && !raven); // a raven's own wish
#if RAVEN_STORY
            StoryBus.Kill(prefab, stars, boss == 1);
#endif
            Nicks.OnKill(me, stars, raven); // a deed for Munin's nickname
            BindRun.OnKill(me); // a в'язь that fires on a kill
#endif
        }

        internal static Player Take(Character c)
        {
            ZDOID id = c.GetZDOID();
            KeyValuePair<Player, float> e;
            if (!Last.TryGetValue(id, out e)) return null;
            Last.Remove(id);
            return e.Key != null && Time.time - e.Value <= Window ? e.Key : null;
        }

        // ---- a kill shared with a helper (the user, 2026-10-27): Odin's kill errands count it for whoever has one and
        // took at least AssistShare of the creature's health with their own blows (their ravens' too), not only for the
        // one who finished it. The errands, and half the kill's strength for Munin: blessings, wishes and the rest stay the finisher's.
        internal static float AssistShare = 0.3f;
        private const float ShareKeep = 60f; // s after the last blow that its record is still kept
        private sealed class Share { public float At; public readonly Dictionary<Player, float> By = new Dictionary<Player, float>(2); }
        private static readonly Dictionary<ZDOID, Share> Shares = new Dictionary<ZDOID, Share>();
        private static readonly List<ZDOID> OldShares = new List<ZDOID>();
        private static readonly List<Player> HelperList = new List<Player>(2);

        // the health this player's blow took (RPC_Damage, where the creature is simulated)
        internal static void Dealt(Character c, Player p, float lost)
        {
            if (Shares.Count > 200)
            {
                OldShares.Clear();
                foreach (KeyValuePair<ZDOID, Share> e in Shares) if (Time.time - e.Value.At > ShareKeep) OldShares.Add(e.Key);
                foreach (ZDOID id in OldShares) Shares.Remove(id);
            }
            ZDOID key = c.GetZDOID();
            Share s;
            if (!Shares.TryGetValue(key, out s)) Shares[key] = s = new Share();
            float had;
            s.By.TryGetValue(p, out had);
            s.By[p] = had + lost;
            s.At = Time.time;
        }

        // at its death: the players (not the finisher) who took enough of it; the record goes
        internal static List<Player> Helpers(Character c, Player killer)
        {
            HelperList.Clear();
            ZDOID key = c.GetZDOID();
            Share s;
            if (!Shares.TryGetValue(key, out s)) return HelperList;
            Shares.Remove(key);
            if (Time.time - s.At > ShareKeep) return HelperList;
            float need = c.GetMaxHealth() * AssistShare;
            foreach (KeyValuePair<Player, float> e in s.By) if (e.Key != null && e.Key != killer && e.Value >= need) HelperList.Add(e.Key);
            return HelperList;
        }

#if RAVEN_ITEM
        private const string RpcAssist = "RavenWatch_KillAssist";
        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(RpcAssist, new Action<long, ZPackage>(OnAssist)); }

        // tell a helper: its errands may count this kill
        internal static void SendAssist(Player helper, Character c, string prefab, string token, int stars, bool boss)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(c.GetZDOID());
            pkg.Write(prefab ?? "");
            pkg.Write(token ?? "");
            pkg.Write(stars);
            pkg.Write(boss);
            if (helper == Player.m_localPlayer) { pkg.SetPos(0); Assisted(pkg); return; }
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(helper.GetZDOID().UserID, RpcAssist, pkg);
        }

        private static void OnAssist(long sender, ZPackage pkg) { try { Assisted(pkg); } catch (Exception e) { Debug.LogWarning("[RavenWatch] kill assist unreadable: " + e.Message); } }

        // only the errands count it, as a kill of your own (the way of killing: by your own blows on that very one)
        private static void Assisted(ZPackage pkg)
        {
            if (Player.m_localPlayer == null) return;
            ZDOID id = pkg.ReadZDOID();
            string prefab = pkg.ReadString(), token = pkg.ReadString();
            int stars = pkg.ReadInt();
            bool boss = pkg.ReadBool();
            Power.OnAssist(boss ? 1 : 0, stars, prefab, token);
            ErrandHow.Dying = id;
            try { Errands.OnKill(prefab, token, stars, boss, false); }
            finally { ErrandHow.Dying = ZDOID.None; }
        }
#endif
    }

    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class KillCreditPatch
    {
        // __state: its health before a player's blow (-1: not a player's), the share taken counted after it
        private static void Prefix(Character __instance, HitData hit, out float __state)
        { __state = -1f; try {
            if (hit == null || __instance is Player) return;
            Player p = hit.GetAttacker() as Player;
            if (p == null) return;
            KillCredit.Hit(__instance, p);
#if RAVEN_ITEM
            Champions.Struck(__instance, p); // (its owner's own blow: what its errand and its gold ask)
#endif
            if (!__instance.IsTamed()) __state = __instance.GetHealth();
        } catch (System.Exception e) { PatchGuard.Fail("KillCreditPatch", e); } }

        private static void Postfix(Character __instance, HitData hit, float __state)
        { try {
            if (__state < 0f || hit == null || __instance.IsDead()) return; // (the killing blow: its finisher has the kill anyway)
            float lost = __state - __instance.GetHealth();
            Player p = hit.GetAttacker() as Player;
            if (p != null && lost > 0f) KillCredit.Dealt(__instance, p, lost);
        } catch (System.Exception e) { PatchGuard.Fail("KillCreditPatch", e); } }
    }

    // runs where the creature is simulated; tells the killer (possibly another PC) about the kill
    [HarmonyPatch(typeof(Character), "OnDeath")]
    internal static class CreatureDeathPatch
    {
        private static void Prefix(Character __instance)
        { try {
            if (__instance is Player) return;
#if RAVEN_ITEM
            BloodRevenge.Died(__instance); // someone's vendetta on it: where it died, to everybody
            Champions.Died(__instance); // an errand's champion: its owner is told, whoever killed it
            if (BindArena.Of(__instance)) return; // an arena's creature: no strength, errands, nicknames (weaving again and again would farm them)
#endif
            HitData hit = HitInfo.LastHit(__instance);
            Character by = hit != null ? hit.GetAttacker() : null;
            Player killer = by as Player;
            Player credited = KillCredit.Take(__instance);
            if (killer == null && (by == null || by.IsTamed())) killer = credited; // a blow that names nobody: the last player who hit it
            // his own blow: his hit, or one that names nobody and is his by the credit (his sword's lightning, a burn, a bleed: the user
            // 2026-11-05 killed a starred wolf with a lightning proc and Munin's «fight» wish did not count). A pet's or a raven's — not
            bool own = killer != null && (by == killer || (by == null && credited == killer));
            KillCredit.Died(__instance, killer); // (for its loot, made right after: the killer's blessings)
#if RAVEN_ITEM
            if (!__instance.IsTamed()) // a helper's share: its errands count it too (whoever finished it, even a beast)
                foreach (Player h in KillCredit.Helpers(__instance, killer))
                    KillCredit.SendAssist(h, __instance, Utils.GetPrefabName(__instance.gameObject), HitInfo.Token(__instance), Mathf.Max(0, __instance.GetLevel() - 1), Champions.TrueBoss(__instance));
#endif
            if (killer == null) return;
            string token = HitInfo.Token(__instance);
#if RAVEN_ITEM
            int boss = Champions.TrueBoss(__instance) ? 1 : 0;
#else
            int boss = __instance.IsBoss() ? 1 : 0;
#endif
            int stars = Mathf.Max(0, __instance.GetLevel() - 1);
            string prefab = Utils.GetPrefabName(__instance.gameObject); // what Munin's strength goes by
            bool tamed = __instance.IsTamed();
#if RAVEN_ITEM
            bool raven = hit != null && hit.m_skillRaiseAmount <= Strike.Marker; // the last blow a raven's (its mark: Strike)
#else
            bool raven = false;
#endif
            if (killer == Player.m_localPlayer) { KillCredit.Mine(__instance, token, boss, stars, prefab, tamed, raven, own, __instance.GetZDOID()); return; }
            Plugin.SendKill(killer.GetZDOID().UserID, token, boss, stars, prefab, tamed, raven, own, __instance.GetZDOID());
        } catch (System.Exception e) { PatchGuard.Fail("CreatureDeathPatch", e); } }
    }

    // the attacker's side of a hit: wounded animals, friendly fire
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class OutgoingHitPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            Player me = Player.m_localPlayer;
            if (me == null || hit == null || __instance == me || hit.m_attacker != me.GetZDOID() || hit.GetAttacker() != me) return; // by id first: every hit in the area passes here
            if (__instance is Player)
            {
                Diary.Add(Ev.Friendly, "", 0);
#if RAVEN_ITEM
                AboutTalk.YouHit((Player)__instance); // your raven noticed whom you hit
#endif
                return;
            }
#if RAVEN_ITEM
            Senses.HitFoeAt = Time.time;
            if (__instance.m_faction != Character.Faction.AnimalsVeg && !__instance.IsTamed()) Senses.HitMonsterAt = Time.time;
            if (!__instance.IsTamed()) ErrandHow.OnHit(__instance, hit); // (an errand's way of killing: a bow, a sneak attack…)
            Strike.YouStruck(__instance); // (a boss's fall counts only for those who fought it)
#endif
            switch (hit.m_skill) // what the blow came from: arrows and bolts, or the staffs
            {
                case Skills.SkillType.Bows: case Skills.SkillType.Crossbows: Diary.Add(Ev.Bow, "", 0); break;
                case Skills.SkillType.ElementalMagic: case Skills.SkillType.BloodMagic: Diary.Add(Ev.Magic, "", 0); break;
            }
            if (__instance.m_faction == Character.Faction.AnimalsVeg && !__instance.IsTamed()) Diary.WoundedAnimal(__instance);
        } catch (System.Exception e) { PatchGuard.Fail("OutgoingHitPatch", e); } }
    }

    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class PlayerHurtPatch
    {
        private static void Postfix(Character __instance, HitData hit)
        { try {
            if (__instance != Player.m_localPlayer || hit == null || __instance.IsDead()) return;
            if (hit.m_hitType == HitData.HitType.Fall) Diary.Add(Ev.Fall, "", 0);
#if RAVEN_ITEM
            if (hit.m_hitType == HitData.HitType.Tree) Nicks.OnTreeHit(Player.m_localPlayer); // a log on the head
            Memory.Hurt(hit.GetTotalDamage());
#endif
            Character attacker = hit.GetAttacker();
            bool creature = attacker != null && !(attacker is Player);
#if RAVEN_ITEM
            if (attacker is Player && attacker != __instance) { AboutTalk.HitYou((Player)attacker); People.OnHit((Player)attacker); } // and who hit you (your ravens remember it)
#endif
            if (creature && attacker.m_faction != Character.Faction.AnimalsVeg) Diary.HurtBy(attacker);
#if RAVEN_ITEM
            if (creature) Senses.HurtByFoeAt = Time.time;
            if (creature && attacker.m_faction != Character.Faction.AnimalsVeg) FightJudge.Hurt(Player.m_localPlayer, attacker, hit.GetTotalDamage());
#endif
#if RAVEN_ITEM
            if (creature) HuginChores.OnHurt(hit.GetTotalDamage());
#endif
            if (__instance.GetHealthPercentage() < 0.15f)
            {
                Diary.OnLowHealth(creature ? HitInfo.Token(attacker) : "");
#if RAVEN_ITEM
                Memory.Low(__instance.GetHealth());
#endif
            }
        } catch (System.Exception e) { PatchGuard.Fail("PlayerHurtPatch", e); } }
    }

    internal static class Mine
    {
        internal static bool Me(Humanoid h) { return h != null && h == Player.m_localPlayer; }

        internal static void WorkHit(HitData hit, Ev type)
        {
            if (hit != null && Player.m_localPlayer != null && hit.GetAttacker() == Player.m_localPlayer) Diary.Add(type, "", 0);
        }
    }

    [HarmonyPatch(typeof(TreeBase), "Damage")]
    internal static class TreeHitPatch { private static void Prefix(HitData hit) { Mine.WorkHit(hit, Ev.Chop); } }

    [HarmonyPatch(typeof(TreeLog), "Damage")]
    internal static class LogHitPatch { private static void Prefix(HitData hit) { Mine.WorkHit(hit, Ev.Chop); } }

    [HarmonyPatch(typeof(MineRock5), "Damage")]
    internal static class Rock5HitPatch { private static void Prefix(HitData hit) { Mine.WorkHit(hit, Ev.Mine); } }

    [HarmonyPatch(typeof(MineRock), "Damage")]
    internal static class RockHitPatch { private static void Prefix(HitData hit) { Mine.WorkHit(hit, Ev.Mine); } }

    // A piece built by the player here: the ONE hook on building (the user 2026-11-04: «not patches, the core»), told to everyone who
    // cares from here — the diary (building, gardening), Hugin's chores (a seed sown), the prologue's heart, the people (a new thing to
    // look at: the bus «piece_placed», where it stands). PlacePiece hands over the piece it was made from; the very piece made comes
    // from SetCreator, which the game calls inside it on the piece it has just put down
    [HarmonyPatch(typeof(Player), "PlacePiece")]
    internal static class BuildPatch
    {
        internal static Piece Made; // (the piece put down in this PlacePiece: SetCreator)
        private static void Prefix() { Made = null; }
        private static void Postfix(Player __instance, Piece piece, bool cheated)
        {
            try { if (__instance == Player.m_localPlayer && piece != null) Diary.Placed(__instance, piece, Made, cheated); }
            catch (Exception e) { PatchGuard.Fail("BuildPatch", e); }
            finally { Made = null; }
        }
    }

    [HarmonyPatch(typeof(Piece), "SetCreator")]
    internal static class BuildMadePatch
    {
        private static void Postfix(Piece __instance) { BuildPatch.Made = __instance; }
    }

    // A thing made (only when it really came into the bag: a click without the materials makes nothing). Food made
    // this way — the cauldron's dishes — is cooking too, once a craft (several when crafted many at once).
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class CraftPatch
    {
        private static readonly FieldInfo RecipeF = AccessTools.Field(typeof(InventoryGui), "m_craftRecipe");
        private static void Prefix(InventoryGui __instance, Player player, out KeyValuePair<Recipe, int> __state)
        {
            __state = new KeyValuePair<Recipe, int>(null, 0);
            if (player != Player.m_localPlayer || RecipeF == null) return;
            Recipe r = RecipeF.GetValue(__instance) as Recipe;
            if (r == null || r.m_item == null) return;
            __state = new KeyValuePair<Recipe, int>(r, player.GetInventory().CountItems(r.m_item.m_itemData.m_shared.m_name));
        }
        private static void Postfix(Player player, KeyValuePair<Recipe, int> __state)
        {
            if (player != Player.m_localPlayer) return;
            Recipe r = __state.Key;
            if (r == null) { Diary.Add(Ev.Craft, "", 0); return; } // (unknown: as before)
            ItemDrop.ItemData.SharedData s = r.m_item.m_itemData.m_shared;
            int got = player.GetInventory().CountItems(s.m_name) - __state.Value;
            if (got <= 0 && s.m_maxStackSize <= 1) got = 1; // an upgrade: the same one thing, better
            if (got <= 0) return;
            Diary.Add(Ev.Craft, "", 0);
            if (s.m_food <= 0f && s.m_foodStamina <= 0f && s.m_foodEitr <= 0f) return;
            int times = Mathf.Max(1, got / Mathf.Max(1, r.m_amount));
            for (int i = 0; i < times; i++) Diary.Add(Ev.Cook, "", 0);
        }
    }

    // The spit, the iron cooking station, the oven: cooked when you take a finished dish off it (E), not a burnt one.
    // (Putting meat on with E goes Interact → OnInteract, not UseItem: counting there missed all of it.)
    [HarmonyPatch(typeof(CookingStation), "OnInteract")]
    internal static class CookPatch
    {
        private static readonly MethodInfo HaveDoneM = AccessTools.Method(typeof(CookingStation), "HaveDoneItem");
        private static readonly MethodInfo NextM = AccessTools.Method(typeof(CookingStation), "NextItemStatus");
        private static void Prefix(CookingStation __instance, Humanoid user, out bool __state)
        {
            __state = false;
            try
            {
                if (!Mine.Me(user) || HaveDoneM == null || NextM == null || !(bool)HaveDoneM.Invoke(__instance, null)) return;
                __state = NextM.Invoke(__instance, null).ToString() == "Done";
            }
            catch (Exception) { }
        }
        private static void Postfix(bool __result, bool __state) { if (__result && __state) Diary.Add(Ev.Cook, "", 0); }
    }

    [HarmonyPatch(typeof(Player), "EatFood")]
    internal static class EatPatch
    {
        private static void Postfix(Player __instance, bool __result)
        {
            if (!__result || __instance != Player.m_localPlayer) return;
            Diary.Add(Ev.Eat, "", 0);
#if RAVEN_ITEM
            PerkFx.OnEat(); // «Ласун», «Ненажера»
#endif
        }
    }

    [HarmonyPatch(typeof(TeleportWorld), "Teleport")]
    internal static class PortalPatch
    {
        // only a real crossing: the game calls this on every step into the portal, also when it refuses (ore in the bag, no
        // other end) — then nothing is teleporting; and once for one crossing
        private static float _at = -100f;
        private static void Postfix(Player player)
        {
            if (player != Player.m_localPlayer || !player.IsTeleporting() || Time.time - _at < 5f) return;
            _at = Time.time;
            Diary.Add(Ev.Portal, "", 0);
        }
    }

    [HarmonyPatch(typeof(Player), "SetSleeping")]
    internal static class SleepPatch
    {
        private static void Postfix(Player __instance, bool sleep) { if (sleep && __instance == Player.m_localPlayer) Diary.Add(Ev.Sleep, "", 0); }
    }

    [HarmonyPatch(typeof(Pickable), "Interact")]
    internal static class PickPatch
    {
        // (Interact returns whether to play the hand animation, not whether anything was picked: the state before tells)
        private static void Prefix(Pickable __instance, Humanoid character, bool ___m_picked, out string __state)
        {
            __state = Mine.Me(character) && !___m_picked && __instance.GetEnabled != 0 ? (__instance.m_itemPrefab != null ? __instance.m_itemPrefab.name : "") : null;
        }
        private static void Postfix(string __state) { if (__state != null) Diary.Add(Ev.Pick, __state, 0); }
    }

    [HarmonyPatch(typeof(Container), "Interact")]
    internal static class LootPatch
    {
        private static void Postfix(Humanoid character, bool __result) { if (__result && Mine.Me(character)) Diary.Add(Ev.Loot, "", 0); }
    }

    [HarmonyPatch(typeof(Tameable), "Interact")]
    internal static class PetPatch
    {
        private static void Postfix(Humanoid user, bool __result) { try { if (__result && Mine.Me(user)) Diary.Add(Ev.Pet, "", 0); } catch (System.Exception e) { PatchGuard.Fail("PetPatch", e); } }
    }

    [HarmonyPatch(typeof(FishingFloat), "Catch")]
    internal static class FishPatch
    {
        private static void Postfix(Character owner) { if (owner != null && owner == Player.m_localPlayer) Diary.Add(Ev.Fish, "", 0); }
    }

    // the store opens on any word with a trader (Interact itself always says false)
    [HarmonyPatch(typeof(Trader), "Interact")]
    internal static class TradePatch
    {
        private static void Postfix(Trader __instance, Humanoid character, bool hold) { if (!hold && Mine.Me(character)) Diary.Add(Ev.Trade, __instance.m_name, 0); }
    }

    [HarmonyPatch(typeof(Chair), "Interact")]
    internal static class SitPatch
    {
        private static void Postfix(Humanoid human, bool __result) { if (__result && Mine.Me(human)) Diary.Add(Ev.Sit, "", 0); }
    }

    [HarmonyPatch(typeof(Fermenter), "AddItem")]
    internal static class FermentPatch
    {
        private static void Postfix(Humanoid user, bool __result) { if (__result && Mine.Me(user)) Diary.Add(Ev.Ferment, "", 0); }
    }

    // ore into a smelter or a blast furnace (the same class runs kilns, windmills and spinning wheels: not those)
    [HarmonyPatch(typeof(Smelter), "OnAddOre")]
    internal static class SmeltPatch
    {
        private static void Postfix(Smelter __instance, Humanoid user, bool __result)
        {
            if (!__result || !Mine.Me(user)) return;
            string n = (__instance.m_name ?? "").ToLowerInvariant();
            if (n.Contains("smelter") || n.Contains("blastfurnace")) Diary.Add(Ev.Smelt, "", 0);
        }
    }

    // one worn thing mended per click: fewer worn things after it means it worked
    [HarmonyPatch(typeof(InventoryGui), "RepairOneItem")]
    internal static class RepairPatch
    {
        private static readonly List<ItemDrop.ItemData> Worn = new List<ItemDrop.ItemData>();
        private static int WornCount()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return 0;
            Worn.Clear();
            p.GetInventory().GetWornItems(Worn);
            return Worn.Count;
        }
        private static void Prefix(out int __state) { __state = WornCount(); }
        private static void Postfix(int __state) { if (WornCount() < __state) Diary.Add(Ev.Repair, "", 0); }
    }

    // a mead or a potion: something drunk for its effect, not food
    [HarmonyPatch(typeof(Player), "ConsumeItem")]
    internal static class PotionPatch
    {
        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || __instance != Player.m_localPlayer || item == null) return;
            if (item.m_shared.m_food <= 0f && item.m_shared.m_consumeStatusEffect != null) Diary.Add(Ev.Potion, item.m_dropPrefab != null ? item.m_dropPrefab.name : "", 0);
            string n = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            if (n.IndexOf("Mead", System.StringComparison.OrdinalIgnoreCase) >= 0) Combat.Drank(); // (the ravens on you: a twist of the head)
#if RAVEN_ITEM
            if (n.StartsWith("Mead")) Mead.Drank(__instance); // a raven sips it now and then: tipsy for a minute
#endif
        }
    }

    // the game's own words on the screen, before they are translated: a portal that will not take your load (ore, metal), and
    // a beast tamed near you («$enemy_wolf $hud_tamedone», sent to the nearest player, whoever owns the beast)
    [HarmonyPatch(typeof(MessageHud), "ShowMessage")]
    internal static class ScreenWordsPatch
    {
        private const string Tamed = " $hud_tamedone";
        private static void Prefix(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text) || Player.m_localPlayer == null) return;
                if (text == "$msg_noteleport") Diary.Add(Ev.PortalBlocked, "", 0);
#if RAVEN_ITEM
                else if (text == "$msg_noroom" || text == "$inventory_full") Nicks.OnNoRoom(Player.m_localPlayer);
#endif
                else if (text.EndsWith(Tamed, StringComparison.Ordinal))
                {
                    string name = text.Substring(0, text.Length - Tamed.Length).Trim();
                    Diary.Add(Ev.Tame, name.StartsWith("$") ? name.Substring(1) : name, 0);
                }
            }
            catch (Exception e) { PatchGuard.Fail("ScreenWordsPatch", e); }
        }
    }

    // a trophy hung on a stand
    [HarmonyPatch(typeof(ItemStand), "UseItem")]
    internal static class TrophyHangPatch
    {
        private static readonly MethodInfo CanAttach = AccessTools.Method(typeof(ItemStand), "CanAttach");
        private static void Prefix(ItemStand __instance, Humanoid user, ItemDrop.ItemData item, out bool __state)
        {
            __state = false;
            if (!Mine.Me(user) || item == null || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy || __instance.HaveAttachment()) return;
            try { __state = CanAttach == null || (bool)CanAttach.Invoke(__instance, new object[] { item }); } catch { __state = false; }
            _hung = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
        }
        private static string _hung = ""; // (the kind of trophy going up now)
        private static void Postfix(ItemStand __instance, Humanoid user, bool __state, bool __result)
        {
            if (!__state || !__result) return;
#if RAVEN_ITEM
            // Odin's errand counts each stand once while it lasts, also one that held a trophy before (your old trophy
            // wall); the ravens' word below only for a stand never hung on before
            try { Errands.OnEvent(Ev.TrophyHang, StandId(__instance) + "|" + _hung, 0); } catch (System.Exception e) { PatchGuard.Fail("TrophyHangPatch errand", e); }
#endif
            try { if (!FirstOnStand(user as Player, __instance)) return; } catch (System.Exception e) { PatchGuard.Fail("TrophyHangPatch", e); }
            Diary.Add(Ev.TrophyHang, "", 0); // (no stand: the errand above has counted it already)
        }

        private static string StandId(ItemStand stand)
        {
            ZNetView nv = stand != null ? stand.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid()) return "";
            ZDOID id = nv.GetZDO().m_uid;
            return id.UserID + ":" + id.ID;
        }

        // the stands this character has hung a trophy on (kept in the character: the last StandsKept of them)
        private const string StandsKey = "RavenWatch_trophyStands";
        private const int StandsKept = 200;
        private static bool FirstOnStand(Player p, ItemStand stand)
        {
            ZNetView nv = stand != null ? stand.GetComponent<ZNetView>() : null;
            if (p == null || nv == null || !nv.IsValid()) return true;
            ZDOID id = nv.GetZDO().m_uid;
            string me = id.UserID + ":" + id.ID, raw;
            if (!p.m_customData.TryGetValue(StandsKey, out raw)) raw = "";
            List<string> l = new List<string>(raw.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            if (l.Contains(me)) return false;
            l.Add(me);
            if (l.Count > StandsKept) l.RemoveRange(0, l.Count - StandsKept);
            p.m_customData[StandsKey] = string.Join(",", l.ToArray());
            return true;
        }
    }

    // a creature's blow that passed through you mid-roll
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class DodgePatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (__instance != Player.m_localPlayer || hit == null || !hit.m_dodgeable || __instance.IsDead() || __instance.GetHealth() <= 0f) return;
            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker is Player) return;
            if (__instance.IsDodgeInvincible()) Diary.Add(Ev.Dodge, HitInfo.Token(attacker), 0);
        } catch (System.Exception e) { PatchGuard.Fail("DodgePatch", e); } }
    }
}
