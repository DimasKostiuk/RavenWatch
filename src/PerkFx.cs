#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The blessings and burdens with mechanics of their own (Perks: Own, Golden), and the raven's part in them: when one
    // of them works, its raven shows it now and then — a word from the shoulder, a look, a caw. The numbers come from
    // Perks.Of (the worn, awake ravens, made again on every Sync); what a death brings is latched at the death itself,
    // since the ravens go into the grave with everything else.
    internal static class PerkFx
    {
        internal static float SayChance = 0.3f;       // a word when a blessing or burden works (not every time)
        internal static float BoredSeconds = 1800f;   // «Занудьгував»: this long without a kill
        internal static float RevengeSeconds = 600f;  // «Месник»
        internal static float GloatSeconds = 300f;    // «Зловтішний»
        internal static float GraveSeconds = 600f;    // «До могили»: at most this long, if the grave is never reached
        private const string KillersKey = "RavenWatch_killers"; // the player's customData: the last three kinds that killed them
        private const string LootKey = "RavenWatch_perkLoot";   // the player's ZDO: «Мисливець;Стерв'ятник», for the game that drops the loot

        // ---------------------------------------------------------------- a word from the shoulder

        private static readonly Dictionary<string, float> SaidAt = new Dictionary<string, float>();

        // a word about a blessing or burden: not every time (chance), not too often (each tag its pause, counted whether
        // a word came or not), never over another line
        internal static bool Say(bool munin, string tag, float chance, float pauseMinutes, params string[] values)
        {
            return SayAbout(munin, tag, chance, pauseMinutes, null, null, values);
        }

        private static bool SayAbout(bool munin, string tag, float chance, float pauseMinutes, string creatureKey, string creature, params string[] values)
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return false;
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null || !Trust.Active(item) || Trust.Away(item) || Trust.Stranger(item, p)) return false;
            float last;
            if (SaidAt.TryGetValue(tag, out last) && Time.time - last < pauseMinutes * 60f) return false;
            SaidAt[tag] = Time.time;
            if (UnityEngine.Random.value >= chance) return false;
            if (RavenSkills.Hushed(p) || !Director.May(Director.Voice.Event)) return false;
            string line = Compose(p, munin, tag, creatureKey, creature, values);
            if (string.IsNullOrEmpty(line)) return false;
            RavenSkills.SayFromShoulder(p, munin, line);
            Director.Spoke(Director.Voice.Event, RavenSkills.SaySeconds(line));
            return true;
        }

        private static string Compose(Player p, bool munin, string tag, string creatureKey, string creature, string[] values)
        {
            if (Plugin.Instance == null) return null;
            Composer.Extra.Clear();
            try
            {
                for (int i = 0; i + 1 < values.Length; i += 2) Composer.Extra[values[i]] = values[i + 1];
                if (creatureKey != null) Composer.ExtraCreature(creatureKey, creature);
                return Plugin.Instance.ComposeTagged(p, munin, tag);
            }
            finally { Composer.Extra.Clear(); }
        }

        // from the grave (the ravens are in it after a death): the word comes as a call in the middle of the screen;
        // a raven still on the shoulder (its «Вірність») says it from there
        private static void Call(Player p, bool munin, string tag)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item != null && Trust.Active(item) && !Trust.Away(item)) { Say(munin, tag, 1f, 0f); return; }
            string line = Compose(p, munin, tag, null, null, new string[0]);
            if (string.IsNullOrEmpty(line)) return;
            RavenMsg.Center(p, Lang.F("{0} з могили: «{1}»", Plugin.RavenName(munin), Grammar.Fix(line, p)));
        }

        // ---------------------------------------------------------------- a look (ShoulderLife.Search asks)

        private static readonly Transform[] LookAt = new Transform[2];
        private static readonly float[] LookUntil = new float[2];

        private static void Look(bool munin, Transform at, float seconds)
        {
            int i = munin ? 1 : 0;
            LookAt[i] = at;
            LookUntil[i] = Time.time + seconds;
        }

        internal static void LookAtFor(bool munin, Transform at, float seconds) { Look(munin, at, seconds); } // (others: «Кровна помста»)

        internal static Transform LookFor(bool munin)
        {
            int i = munin ? 1 : 0;
            if (LookAt[i] == null || Time.time > LookUntil[i]) { LookAt[i] = null; return null; }
            return LookAt[i];
        }

        // ---------------------------------------------------------------- a golden blessing

        private static int _goldFor;     // 1 Hugin, 2 Munin: his word about it is still to come
        private static float _goldUntil;

        internal static void GoldRolled(Player p, bool munin, ItemDrop.ItemData item)
        {
            RavenMsg.Center(p, Lang.F("<color=#E6C77C>Золоте благо!</color> {0}", Perks.BonusLine(item)));
            ScreamFx.CawAll(p, munin, munin ? 0.9f : 1.15f, 0.6f);
            _goldFor = munin ? 2 : 1;
            _goldUntil = Time.time + 10f; // a sleeping raven (just bought) says nothing: the gold line waits a little, then is gone
        }

        // ---------------------------------------------------------------- twice a second (Perks.Sync)

        internal static void Tick(Player p)
        {
            if (p == null) return;
            float now = Time.time;
            if (LastKillAt <= 0f) LastKillAt = now; // a fresh session: the clock of «Занудьгував» starts here
            if (_goldFor != 0)
            {
                if (now > _goldUntil) _goldFor = 0;
                else if (Say(_goldFor == 2, "perk_gold", 1f, 0f)) _goldFor = 0;
            }
            Respawned(p, now);
            GraveRun(p);
            Sniff(p, now);
            Whisper(p, now);
            Chatter(p, now);
            Sea(now);
            Bored(now);
            Publish(p);
        }

        // ---------------------------------------------------------------- «Нюх на скарби»

        private static readonly Collider[] Hits = new Collider[96];
        private static readonly HashSet<ZDOID> Sniffed = new HashSet<ZDOID>();
        private static int _sniffMask;
        private static float _sniffAt, _sniffSaid = -999f;
        internal static float SniffPause = 120f; // between two chests

        private static void Sniff(Player p, float now)
        {
            float reach = Perks.Of("sniff");
            if (reach <= 0f || now < _sniffAt || now - _sniffSaid < SniffPause) return;
            _sniffAt = now + 5f;
            ItemDrop.ItemData hugin = Trust.Worn(false);
            if (hugin == null || Trust.Away(hugin) || p.IsDead()) return;
            if (_sniffMask == 0) _sniffMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
            Vector3 me = p.transform.position;
            int n = Physics.OverlapSphereNonAlloc(me, reach, Hits, _sniffMask);
            Container best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Container c = Hits[i] != null ? Hits[i].GetComponentInParent<Container>() : null;
                if (c == null || !RavenSkills.WorldChest(c)) continue; // (someone's own, a grave, a ship's hold: not a find)
                ZNetView nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || Sniffed.Contains(nv.GetZDO().m_uid)) continue;
                Inventory inv = c.GetInventory();
                if (inv == null || inv.NrOfItems() == 0 || c.IsInUse()) continue;
                float d = (c.transform.position - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best == null) return;
            Sniffed.Add(best.GetComponent<ZNetView>().GetZDO().m_uid);
            _sniffSaid = now;
            Look(false, best.transform, 4f); // the head turns to it
            ScreamFx.CawAll(p, false, 1.3f, 0.45f);
            Say(false, "perk_sniff", 0.5f, 0f);
        }

        // ---------------------------------------------------------------- «Шепіт вітру», «Шепіт Одіна»

        private static readonly List<Minimap.PinData> RaidPins = new List<Minimap.PinData>();
        private static float _whisperAt;
        private static string _whisperRaid;

        private static void Whisper(Player p, float now)
        {
            float reach = Perks.Has("g_whisper") ? 150f : Perks.Of("whisper");
            RandomEvent raid = RandEventSystem.instance != null ? RandEventSystem.instance.GetActiveEvent() : null;
            if (raid == null) _whisperRaid = null;
            if (reach <= 0f || raid == null || p.IsDead() || Minimap.instance == null) { ClearRaidPins(); return; }
            if (now < _whisperAt) return;
            _whisperAt = now + 2f; // they move: the marks again every two seconds
            ClearRaidPins();
            Vector3 me = p.transform.position, sum = Vector3.zero;
            int count = 0;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed()) continue;
                if ((c.transform.position - me).sqrMagnitude > reach * reach) continue;
                MonsterAI ai = c.GetBaseAI() as MonsterAI;
                if (ai == null || !ai.IsEventCreature()) continue;
                string label = Localization.instance != null ? Lang.Game(c.m_name) : c.m_name;
                Minimap.PinData pin = Minimap.instance.AddPin(c.transform.position, Minimap.PinType.Shout, label, false, false, 0L, default(Splatform.PlatformUserID));
                RavenSkills.Icon(pin, RavenSkills.Trophy(c));
                RaidPins.Add(pin);
                sum += c.transform.position;
                count++;
            }
            if (count == 0 || _whisperRaid == raid.m_name) return;
            _whisperRaid = raid.m_name; // once per raid: where they come from
            SitEvents.Raise(SitEv.Boss); // they bristle
            ScreamFx.CawAll(p, false, 1.1f, 0.6f);
            string dir = Side(me, sum / count);
            if (!Say(false, "perk_whisper", 1f, 0f, "dir", dir, "count", count.ToString()))
                RavenMsg.Center(p, Lang.F("Хугін: нападники йдуть {0}. Вони на мапі.", dir));
        }

        private static void ClearRaidPins()
        {
            if (RaidPins.Count == 0) return;
            if (Minimap.instance != null) foreach (Minimap.PinData pin in RaidPins) if (pin != null) Minimap.instance.RemovePin(pin);
            RaidPins.Clear();
        }

        // where a point is from you, as the raven says it ("з півночі")
        private static readonly string[] Sides = { Lang.K("з півночі"), Lang.K("з північного сходу"), Lang.K("зі сходу"), Lang.K("з південного сходу"),
            Lang.K("з півдня"), Lang.K("з південного заходу"), Lang.K("з заходу"), Lang.K("з північного заходу") };

        private static string Side(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            if (new Vector2(d.x, d.z).sqrMagnitude < 25f) return Lang.T("звідусіль");
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; // 0 north, 90 east
            int i = Mathf.RoundToInt((a + 360f) % 360f / 45f) % 8;
            return Lang.T(Sides[i]);
        }

        // ---------------------------------------------------------------- «Балакун»

        private static float _chatterAt = -1f;
        private static readonly List<MonsterAI> Deaf = new List<MonsterAI>();

        private static void Chatter(Player p, float now)
        {
            float reach = Perks.Of("chatter");
            if (reach <= 0f) { _chatterAt = -1f; return; }
            if (_chatterAt < 0f) { _chatterAt = now + UnityEngine.Random.Range(20f, 30f) * 60f; return; }
            if (now < _chatterAt || p.IsDead()) return;
            ItemDrop.ItemData hugin = Trust.Worn(false);
            if (hugin == null || Trust.Away(hugin) || RavenSkills.Hushed(p) || Shadow.On(p) || Dome.Mine()) return; // (his own gifts hide you: he keeps quiet under them)
            // he waits until there is someone to hear it: a monster near that has not noticed you yet
            Deaf.Clear();
            Vector3 me = p.transform.position;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed() || c.IsBoss()) continue;
                if ((c.transform.position - me).sqrMagnitude > reach * reach || !BaseAI.IsEnemy(p, c)) continue;
                MonsterAI ai = c.GetBaseAI() as MonsterAI;
                if (ai != null && !ai.IsAlerted() && AiTarget.Of(ai) == null) Deaf.Add(ai);
            }
            if (Deaf.Count == 0) return;
            _chatterAt = now + UnityEngine.Random.Range(20f, 30f) * 60f;
            string line = Compose(p, false, "perk_chatter", null, null, new string[0]);
            if (string.IsNullOrEmpty(line)) line = Lang.T("КАР! Ой. Я вголос сказав?");
            RavenSkills.SayFromShoulder(p, false, line);
            Director.Spoke(Director.Voice.Event, RavenSkills.SaySeconds(line));
            ScreamFx.CawAll(p, false, 1.2f, 0.9f);
            foreach (MonsterAI ai in Deaf) { AiTarget.Alert(ai); AiTarget.Set(ai, p); }
            Deaf.Clear();
        }

        // ---------------------------------------------------------------- the sea: «Мореплавець», «Морська хвороба»

        private static float _onShipSince = -1f;

        private static void Sea(float now)
        {
            if (Perks.Of("sailor") > 0f && Senses.AtHelm && Senses.Sailing) Say(false, "perk_sailor", 0.5f, 20f);
            if (Perks.Of("seasick") <= 0f || !Senses.OnShip) { _onShipSince = -1f; return; }
            if (_onShipSince < 0f) _onShipSince = now;
            else if (now - _onShipSince > 60f) Say(false, "perk_seasick", 0.5f, 20f);
        }

        internal static float StaminaRegenMul() { float v = Perks.Of("seasick"); return v > 0f && Senses.OnShip ? 1f - v : 1f; }
        internal static float SailMul() { return 1f + Perks.Of("sailor"); }

        // ---------------------------------------------------------------- the trader: «Язикатий», «Хамло»

        private static readonly Dictionary<Trader.TradeItem, int[]> Prices = new Dictionary<Trader.TradeItem, int[]>(); // [its own price, what we set]

        internal static void Price(List<Trader.TradeItem> list)
        {
            float mul = 1f - Perks.Of("tongue") + Perks.Of("boor");
            if (Prices.Count > 400) Prices.Clear(); // the traders of zones long unloaded (each load makes its list anew)
            foreach (Trader.TradeItem t in list)
            {
                if (t == null) continue;
                int[] pr;
                if (!Prices.TryGetValue(t, out pr) || t.m_price != pr[1]) { pr = new int[] { t.m_price, t.m_price }; Prices[t] = pr; } // a price set anew (Haldor's ravens): that is its own now
                int want = Mathf.Max(1, Mathf.RoundToInt(pr[0] * mul));
                t.m_price = want;
                pr[1] = want;
            }
        }

        internal static void AtTrader()
        {
            if (Perks.Of("tongue") > 0f) Say(false, "perk_tongue", 0.5f, 10f);
            if (Perks.Of("boor") > 0f) Say(true, "perk_boor", 0.5f, 10f);
        }

        // ---------------------------------------------------------------- food, rest, the map, the pickup

        internal static float FoodMul() { return (1f + Perks.Of("glutton")) / (1f + Perks.Of("sweet")); } // × the time food burns down

        internal static void OnEat()
        {
            if (Perks.Of("sweet") > 0f) Say(false, "perk_sweet", SayChance, 15f);
            if (Perks.Of("glutton") > 0f) Say(true, "perk_glutton", SayChance, 15f);
        }

        internal static float RestedMul() { return Mathf.Max(0.5f, 1f + Perks.Of("wander") + (Perks.Has("g_trail") ? 0.3f : 0f) - Perks.Of("sleepy")); }

        internal static void OnRested()
        {
            if (Perks.Of("wander") > 0f || Perks.Has("g_trail")) Say(false, "perk_wander", SayChance, 20f);
            if (Perks.Of("sleepy") > 0f) Say(false, "perk_sleepy", SayChance, 20f);
        }

        internal static float ExploreMul() { return 1f + Perks.Of("farsight") + (Perks.Has("g_eye") ? 1f : 0f); }
        internal static float PickupMul() { return 1f + Perks.Of("picker") + (Perks.Has("g_trail") ? 1f : 0f); }

        // ---------------------------------------------------------------- your blows

        internal static float LastKillAt;
        private static bool _boredSaid;
        private static readonly Dictionary<string, float> GrudgeSaid = new Dictionary<string, float>();

        // × your own blow on a creature (not a raven's)
        internal static float DamageMul(Character target)
        {
            float mul = 1f, now = Time.time;
            float grudge = Perks.Of("grudge");
            bool memory = Perks.Has("g_memory");
            if (grudge > 0f || memory)
            {
                string tok = HitInfo.Token(target);
                int at = KillerIndex(tok);
                if (memory && at >= 0) { mul += 0.25f; Grudged(target, tok); }
                else if (grudge > 0f && at == 0) { mul += grudge; Grudged(target, tok); }
            }
            float finisher = Perks.Of("finisher");
            if (finisher > 0f && target.GetHealthPercentage() <= 0.3f) mul += finisher;
            if (now < _revengeUntil) mul += _revengeV;
            if (now < _gloatUntil) mul -= _gloatV;
            float bored = Perks.Of("bored");
            if (bored > 0f && now - LastKillAt > BoredSeconds) mul -= bored;
            return Mathf.Max(0.1f, mul);
        }

        // the one that killed you: Munin knows it at once (a caw, he bristles, a word; once in a while for each kind)
        private static void Grudged(Character target, string tok)
        {
            float last;
            if (GrudgeSaid.TryGetValue(tok, out last) && Time.time - last < 180f) return;
            GrudgeSaid[tok] = Time.time;
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData munin = Trust.Worn(true);
            if (p == null || munin == null || Trust.Away(munin)) return;
            SitEvents.Raise(SitEv.Boss);
            ScreamFx.CawAll(p, true, 0.85f, 0.8f);
            Look(true, target.transform, 3f);
            SayAbout(true, "perk_grudge", 0.6f, 0f, "grudge", tok);
        }

        // where this kind is among the last that killed you (0: the last one; −1: not among them)
        private static string _killersRaw;
        private static string[] _killers = new string[0];

        private static int KillerIndex(string tok)
        {
            Player p = Player.m_localPlayer;
            string raw;
            if (p == null || string.IsNullOrEmpty(tok) || !p.m_customData.TryGetValue(KillersKey, out raw)) return -1;
            if (raw != _killersRaw) { _killersRaw = raw; _killers = raw.Split('|'); }
            return Array.IndexOf(_killers, tok);
        }

        internal static void OnKill(Player p, bool boss, bool tamed)
        {
            if (p == null || tamed) return;
            LastKillAt = Time.time;
            _boredSaid = false;
            float heal = 0f, stamina = 0f;
            if (Perks.Has("g_feast")) { heal += 10f; stamina += 10f; }
            float thirst = Perks.Of("bloodthirst");
            if (thirst > 0f) heal += boss ? 25f : thirst;
            if (heal <= 0f || p.IsDead()) return;
            p.Heal(heal, true);
            if (stamina > 0f) p.AddStamina(stamina);
            Say(true, "perk_bloodthirst", SayChance, 5f);
        }

        private static void Bored(float now)
        {
            if (_boredSaid || Perks.Of("bored") <= 0f || now - LastKillAt <= BoredSeconds) return;
            _boredSaid = Say(true, "perk_bored", 1f, 0f) || now - LastKillAt > BoredSeconds + 300f; // (said, or let go after a while of trying)
        }

        // ---------------------------------------------------------------- the loot: «Мисливець», «Стерв'ятник»
        // The loot is made where the creature is simulated, maybe another player's game: the killer's numbers ride on
        // their ZDO (LootKey).

        // (checked against the ZDO itself: a respawned player is a new one, with a ZDO that knows nothing yet)
        private static ZDO _lootZdo;
        private static float _lootHunter = -1f, _lootScav = -1f;

        private static void Publish(Player p)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            float hunter = Perks.Of("hunter"), scav = Perks.Of("scav");
            if (z == _lootZdo && hunter == _lootHunter && scav == _lootScav) return; // (twice a second: nothing new, nothing made)
            _lootZdo = z; _lootHunter = hunter; _lootScav = scav;
            string want = hunter.ToString("0.##", CultureInfo.InvariantCulture) + ";" + scav.ToString("0.##", CultureInfo.InvariantCulture);
            if (z.GetString(LootKey, "") != want) z.Set(LootKey, want);
        }

        internal static void LootOf(Player killer, out float hunter, out float scav)
        {
            hunter = scav = 0f;
            if (killer == null) return;
            if (killer == Player.m_localPlayer) { hunter = Perks.Of("hunter"); scav = Perks.Of("scav"); return; }
            ZDO z = NetFlag.Zdo(killer, false);
            string raw = z != null ? z.GetString(LootKey, "") : "";
            string[] parts = raw.Split(';');
            if (parts.Length < 2) return;
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out hunter);
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out scav);
        }

        internal static void Looted(bool trophy)
        {
            Say(true, trophy ? "perk_hunter" : "perk_scav", 0.5f, 5f);
        }

        // ---------------------------------------------------------------- death and what it latches

        private static float _revengeV, _gloatV, _graveV, _bodyV;
        private static float _revengeUntil, _gloatUntil;
        private static bool _deathPending, _graveOn;
        private static Player _dead;
        private static float _aliveSince = -1f, _diedAt;
        internal static float RespawnWindow = 180f; // up again later than this (logged out dead, another character): nothing latched

        // the moment of your death (the ravens are still worn: their numbers hold)
        internal static void OnDeath(Player p, string killer)
        {
            _revengeV = Perks.Of("revenge");
            _gloatV = Perks.Of("gloat");
            _graveV = Perks.Of("grave");
            _bodyV = SkillKept();
            _deathPending = _revengeV > 0f || _gloatV > 0f || _graveV > 0f || _bodyV > 0f;
            _dead = p;
            _diedAt = Time.time;
            _aliveSince = -1f;
            _revengeUntil = _gloatUntil = 0f;
            if (string.IsNullOrEmpty(killer) || p == null) return;
            // the last three kinds that killed you (Злопам'ятний: the last; Пам'ять Одіна: all three)
            List<string> list = new List<string>();
            list.Add(killer);
            string raw;
            if (p.m_customData.TryGetValue(KillersKey, out raw))
                foreach (string k in raw.Split('|')) if (k.Length > 0 && k != killer && list.Count < 3) list.Add(k);
            p.m_customData[KillersKey] = string.Join("|", list.ToArray());
        }

        // the share of the skills a death leaves you (Skills.OnDeath, still at the moment of death)
        internal static float SkillKept() { return Mathf.Max(Perks.Of("bodymem"), Perks.Has("g_deathless") ? 0.75f : 0f); }

        // up again: a few seconds after, what the death latched comes into play, and Munin calls from the grave
        private static void Respawned(Player p, float now)
        {
            if (!_deathPending || p == _dead || p.IsDead()) return;
            if (now - _diedAt > RespawnWindow) { _deathPending = false; return; }
            if (_aliveSince < 0f) { _aliveSince = now; return; }
            if (now - _aliveSince < 3f) return;
            _deathPending = false;
            string bonus = null, minus = null;
            if (_revengeV > 0f)
            {
                _revengeUntil = now + RevengeSeconds;
                Timed(p, "RavenWatch_PerkRevenge", Lang.T("Помста"), Lang.F("Месник: +{0}% шкоди, поки не мине.", Mathf.RoundToInt(_revengeV * 100f)), RevengeSeconds, 0f);
                bonus = "perk_revenge";
            }
            if (_graveV > 0f)
            {
                Timed(p, GraveName, Lang.T("До могили"), Lang.F("До могили: бігаєш на {0}% швидше, поки не забереш речі.", Mathf.RoundToInt(_graveV * 100f)), GraveSeconds, _graveV);
                _graveOn = true;
                bonus = "perk_grave";
            }
            if (_bodyV > 0f) bonus = bonus ?? "perk_bodymem";
            if (_gloatV > 0f)
            {
                _gloatUntil = now + GloatSeconds;
                Timed(p, "RavenWatch_PerkGloat", Lang.T("Зловтіха"), Lang.F("Зловтішний: −{0}% шкоди, поки не мине.", Mathf.RoundToInt(_gloatV * 100f)), GloatSeconds, 0f);
                minus = "perk_gloat";
            }
            Call(p, true, bonus ?? minus); // one word, the blessing first
        }

        private const string GraveName = "RavenWatch_PerkGrave";
        private static readonly int GraveHash = GraveName.GetStableHashCode();
        private static readonly int CorpseRunHash = "CorpseRun".GetStableHashCode();

        // «До могили» ends with the grave's own run (the game gives it once the grave is emptied)
        private static void GraveRun(Player p)
        {
            if (!_graveOn) return;
            SEMan se = p.GetSEMan();
            if (!se.HaveStatusEffect(GraveHash)) { _graveOn = false; return; }
            if (se.HaveStatusEffect(CorpseRunHash)) { se.RemoveStatusEffect(GraveHash, true); _graveOn = false; }
        }

        // an effect with a time on it and the raven's own icon (one template per kind: the game copies it)
        private static readonly Dictionary<string, PerkTimed> Templates = new Dictionary<string, PerkTimed>();

        private static void Timed(Player p, string name, string title, string text, float seconds, float speed)
        {
            PerkTimed t;
            if (!Templates.TryGetValue(name, out t) || t == null)
            {
                t = ScriptableObject.CreateInstance<PerkTimed>();
                t.name = name;
                Templates[name] = t;
            }
            t.m_name = title;
            t.m_icon = RavenItem.Icon(true);
            t.m_ttl = seconds;
            t.m_speedModifier = speed;
            t.Text = text;
            SEMan se = p.GetSEMan();
            int hash = name.GetStableHashCode();
            if (se.HaveStatusEffect(hash)) se.RemoveStatusEffect(hash, true);
            se.AddStatusEffect(t, true);
        }
    }

    internal class PerkTimed : SE_Stats
    {
        public string Text = "";
        public override string GetTooltipString() { return Text; }
    }

    // ---------------------------------------------------------------- the hooks

    // your blow: «Злопам'ятний», «Добивайло», «Месник», «Зловтішний», «Занудьгував» (the attacker's side, before it goes out)
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class PerkHitPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            Player me = Player.m_localPlayer;
            if (me == null || hit == null || __instance == me || __instance is Player || hit.m_attacker != me.GetZDOID()) return;
            if (hit.m_skillRaiseAmount <= Strike.Marker) return; // a raven's own blow
            if (!Marks.RealBlow(hit)) return; // a burn or poison ticking: the blow that set it was already counted
            float mul = PerkFx.DamageMul(__instance);
            if (mul != 1f) hit.ApplyModifier(mul);
        } catch (Exception e) { PatchGuard.Fail("PerkHitPatch", e); } }
    }

    // «Пам'ять тіла», «Безсмертна пам'ять»: the skills a death takes, cut down
    [HarmonyPatch(typeof(Skills), "OnDeath")]
    internal static class PerkSkillDeathPatch
    {
        internal static bool Dying;
        private static void Prefix(Skills __instance) { Dying = Player.m_localPlayer != null && ReferenceEquals(__instance, Player.m_localPlayer.GetSkills()); }
        private static void Postfix() { Dying = false; }
    }

    [HarmonyPatch(typeof(Skills), "LowerAllSkills")]
    internal static class PerkSkillLowerPatch
    {
        private static void Prefix(ref float factor)
        {
            if (!PerkSkillDeathPatch.Dying) return;
            float kept = PerkFx.SkillKept();
            if (kept > 0f) factor *= 1f - kept;
        }
    }

    // «Ласун», «Ненажера»: food burns down slower or faster (only its own clock: the same dt also drives the healing)
    [HarmonyPatch(typeof(Player), "UpdateFood")]
    internal static class PerkFoodPatch
    {
        private static void Prefix(Player __instance, float dt, ref float ___m_foodUpdateTimer)
        {
            if (__instance != Player.m_localPlayer) return;
            float mul = PerkFx.FoodMul();
            if (mul != 1f) ___m_foodUpdateTimer += dt * Game.m_foodRate * (mul - 1f);
        }
    }

    // «Мандрівник», «Сонливий», «Слід Хугіна»: the rest lasts longer or shorter
    [HarmonyPatch(typeof(SE_Rested), "UpdateTTL")]
    internal static class PerkRestedPatch
    {
        private static void Prefix(SE_Rested __instance, Character ___m_character, out float[] __state)
        {
            __state = null;
            if (___m_character == null || ___m_character != Player.m_localPlayer) return;
            float mul = PerkFx.RestedMul();
            if (mul == 1f) return;
            __state = new float[] { __instance.m_baseTTL, __instance.m_TTLPerComfortLevel };
            __instance.m_baseTTL *= mul;
            __instance.m_TTLPerComfortLevel *= mul;
        }
        private static void Postfix(SE_Rested __instance, float[] __state)
        {
            if (__state == null) return;
            __instance.m_baseTTL = __state[0];
            __instance.m_TTLPerComfortLevel = __state[1];
        }
    }

    [HarmonyPatch(typeof(SE_Rested), "Setup")]
    internal static class PerkRestedWordPatch
    {
        private static void Postfix(Character character) { if (character != null && character == Player.m_localPlayer) PerkFx.OnRested(); }
    }

    // «Зіркий», «Око Одіна»: the map opens wider around you
    [HarmonyPatch(typeof(Minimap), "UpdateExplore")]
    internal static class PerkExplorePatch
    {
        private static void Prefix(Minimap __instance, out float __state)
        {
            __state = __instance.m_exploreRadius;
            __instance.m_exploreRadius *= PerkFx.ExploreMul();
        }
        private static void Postfix(Minimap __instance, float __state) { __instance.m_exploreRadius = __state; }
    }

    // «Підбирач», «Слід Хугіна»: things come to you from further
    [HarmonyPatch(typeof(Player), "AutoPickup")]
    internal static class PerkPickupPatch
    {
        private static void Prefix(Player __instance, out float __state)
        {
            __state = __instance.m_autoPickupRange;
            if (__instance == Player.m_localPlayer) __instance.m_autoPickupRange *= PerkFx.PickupMul();
        }
        private static void Postfix(Player __instance, float __state) { __instance.m_autoPickupRange = __state; }
    }

    // «Язикатий», «Хамло»: the trader's prices (after Haldor's ravens are on the list)
    [HarmonyPatch(typeof(Trader), "GetAvailableItems")]
    internal static class PerkPricePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(List<Trader.TradeItem> __result) { try { if (__result != null) PerkFx.Price(__result); } catch (Exception e) { PatchGuard.Fail("PerkPricePatch", e); } }
    }

    [HarmonyPatch(typeof(StoreGui), "Show")]
    internal static class PerkStorePatch
    {
        private static void Postfix() { try { PerkFx.AtTrader(); } catch (Exception e) { PatchGuard.Fail("PerkStorePatch", e); } }
    }

    // «Мореплавець»: the sail pulls harder for the one at the helm
    [HarmonyPatch(typeof(Ship), "GetSailForce")]
    internal static class PerkSailPatch
    {
        private static void Postfix(Ship __instance, ref Vector3 __result)
        {
            Player me = Player.m_localPlayer;
            if (me != null && me.GetControlledShip() == __instance) __result *= PerkFx.SailMul();
        }
    }

    // «Морська хвороба»: on a ship the stamina comes back slower
    [HarmonyPatch(typeof(SEMan), "ModifyStaminaRegen")]
    internal static class PerkSeasickPatch
    {
        private static void Postfix(Character ___m_character, ref float staminaMultiplier)
        {
            if (___m_character != null && ___m_character == Player.m_localPlayer) staminaMultiplier *= PerkFx.StaminaRegenMul();
        }
    }

    // «Мисливець», «Стерв'ятник»: more from your kill (made where the creature is simulated; champions keep their own)
    [HarmonyPatch(typeof(CharacterDrop), "GenerateDropList")]
    internal static class PerkDropPatch
    {
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
        { try {
            if (__result == null) return;
            Character c = __instance.GetComponent<Character>();
            if (c == null || c.IsTamed() || c.IsBoss() || BindArena.Of(c) || (Champions.Any && Champions.Fast(c) != null)) return;
            Player killer = KillCredit.KillerOf(c);
            float hunter, scav;
            PerkFx.LootOf(killer, out hunter, out scav);
            if (hunter <= 0f && scav <= 0f) return;
            bool mine = killer == Player.m_localPlayer;
            if (hunter > 0f)
                foreach (CharacterDrop.Drop d in __instance.m_drops)
                {
                    ItemDrop it = d.m_prefab != null ? d.m_prefab.GetComponent<ItemDrop>() : null;
                    if (it == null || it.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy || d.m_onePerPlayer) continue;
                    bool has = false;
                    foreach (KeyValuePair<GameObject, int> kv in __result) if (kv.Key == d.m_prefab) { has = true; break; }
                    // a second roll for a trophy that did not fall: together they fall that much more often
                    if (!has && d.m_chance < 1f && UnityEngine.Random.value < d.m_chance * hunter / Mathf.Max(0.01f, 1f - d.m_chance))
                    {
                        __result.Add(new KeyValuePair<GameObject, int>(d.m_prefab, 1));
                        if (mine) PerkFx.Looted(true);
                    }
                    break;
                }
            if (scav > 0f && UnityEngine.Random.value < scav)
            {
                List<GameObject> some = new List<GameObject>();
                foreach (KeyValuePair<GameObject, int> kv in __result)
                {
                    ItemDrop it = kv.Key != null ? kv.Key.GetComponent<ItemDrop>() : null;
                    if (it != null && it.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy && !some.Contains(kv.Key)) some.Add(kv.Key);
                }
                if (some.Count > 0)
                {
                    __result.Add(new KeyValuePair<GameObject, int>(some[UnityEngine.Random.Range(0, some.Count)], 1));
                    if (mine) PerkFx.Looted(false);
                }
            }
        } catch (Exception e) { PatchGuard.Fail("PerkDropPatch", e); } }
    }
}
#endif
