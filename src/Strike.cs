#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Удар в очі» and «Кігті»: the raven leaves the shoulder, flies where you look, strikes and comes back.
    // The blow is the raven's own, never the weapon's: a base by the bosses it saw fall, raised by its training
    // (вишкіл, 20 levels earned by flights), by trust and by a full belly. Whatever other mods add to a player's hit
    // is cut back to that, and a boss loses at most a small share of its health to one flight.
    internal static class Strike
    {
        // ---------------------------------------------------------------- settings

        internal static float Range = 30f, RangeTop = 100f, AimDegrees = 10f; // how far it flies: at training level 1 and 20
        internal static float HuginCost = 25f, MuninCost = 35f;
        internal static float HuginCooldown = 45f, MuninCooldown = 60f;
        internal static float BossCapHugin = 0.03f, BossCapMunin = 0.05f;
        internal static float MuninMul = 1.2f;
        internal static float TrainPct = 3f, TrustPct = 5f, FedPct = 20f, FedMinutes = 10f;
        internal static float CritBonus = 20f, CritPerTrust = 5f; // points of crit chance on top of the training's (at the top: Hugin 70, Munin 90)
        // a raven's own pace (about 12-14 m/s for the real bird): far targets take their time, near ones are not a blink
        internal static float Speed = 12f, ReturnSpeed = 10f;
        internal static float SeriesGap = 0.45f;

        // the best blow a player can have in each biome, unlocked by the boss before it (seen with this raven)
        internal static readonly float[] Base = { 20f, 45f, 70f, 95f, 120f, 145f, 180f, 210f };
        internal static readonly string[] Bosses = { "Eikthyr", "gd_king", "Bonemass", "Dragon", "GoblinKing", "SeekerQueen", "Fader" };
        internal static string[] BossNames { get { return Lang.Arr(BossNamesSrc); } } // (translated on every read)
        private static readonly string[] BossNamesSrc = Lang.Keys("Ейктюр", "Прадавній", "Кістянка", "Модер", "Яглут", "Королева", "Фейдер");

        // flights from one training level to the next (1→2 … 19→20)
        internal static readonly int[] Need = { 5, 8, 10, 13, 15, 18, 20, 23, 25, 28, 30, 33, 35, 38, 40, 43, 45, 48, 50 };
        internal const int MaxLevel = 20;

        // chances at training level 1 → 20, in percent (Hugin has no triple: the series of three is Munin's)
        private static readonly float[] HuginCrit = { 10f, 20f }, HuginDouble = { 10f, 25f }, HuginTriple = { 0f, 0f };
        private static readonly float[] MuninCrit = { 20f, 40f }, MuninDouble = { 25f, 35f }, MuninTriple = { 5f, 15f };
        internal static float MissRefund = 0.5f; // nothing struck: this share of the strength comes back and of the pause is let off

        private const string XpKey = "RavenWatch_strikes", BossKey = "RavenWatch_bossesSeen", FedAtKey = "RavenWatch_fedAt";
        private const string Rpc = "RavenWatch_Strike", RpcBoss = "RavenWatch_BossDown";
        private static readonly System.Random Rng = new System.Random();

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnStrike));
            ZRoutedRpc.instance.Register<ZPackage>(RpcBoss, new Action<long, ZPackage>(OnBossDown));
        }

        // ---------------------------------------------------------------- what the raven has in it

        private static string Get(ItemDrop.ItemData item, string key)
        {
            string v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(key, out v) ? v : null;
        }

        private static void Put(ItemDrop.ItemData item, string key, string v) { if (item != null) item.m_customData[key] = v; }

        internal static int Flights(ItemDrop.ItemData item) { int n; return int.TryParse(Get(item, XpKey), out n) ? Mathf.Max(0, n) : 0; }

        // the training level and how far into the next one
        internal static int Level(ItemDrop.ItemData item) { int have, need; return Level(item, out have, out need); }
        internal static int Level(ItemDrop.ItemData item, out int have, out int need)
        {
            int left = Flights(item), level = 1;
            while (level < MaxLevel && left >= Need[level - 1]) { left -= Need[level - 1]; level++; }
            have = level < MaxLevel ? left : 0;
            need = level < MaxLevel ? Need[level - 1] : 0;
            return level;
        }

        internal static List<string> BossesSeen(ItemDrop.ItemData item)
        {
            List<string> list = new List<string>();
            string raw = Get(item, BossKey);
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (string s in raw.Split(',')) if (s.Length > 0 && !list.Contains(s)) list.Add(s);
            return list;
        }

        // the base step: the furthest boss in the line that fell with this raven on the shoulder
        internal static int Tier(ItemDrop.ItemData item)
        {
            int tier = 0;
            foreach (string b in BossesSeen(item))
            {
                int i = Array.IndexOf(Bosses, b);
                if (i + 1 > tier) tier = i + 1;
            }
            return Mathf.Clamp(tier, 0, Base.Length - 1);
        }

        // how far this raven flies to a target: its training teaches it to fly further
        internal static float RangeOf(ItemDrop.ItemData item) { return Mathf.Round(Mathf.Lerp(Range, RangeTop, (Level(item) - 1) / (float)(MaxLevel - 1)) * Norov.RangeMul(item) * GearFx.RangeMul(item) * RuneStars.RangeMul(item)); } // (knack «Далекий виліт», «Кігті комара», a starred rune)

        // the strength a strike takes (knack «Ощадний»: less)
        internal static float CostOf(bool munin, ItemDrop.ItemData item) { return (munin ? MuninCost : HuginCost) * Norov.CostMul(item); }

        // the blow as it would be with the next boss seen (the book's "what next")
        internal static float BlowWithTier(bool munin, ItemDrop.ItemData item, int tier)
        {
            return Base[Mathf.Clamp(tier, 0, Base.Length - 1)] * (munin ? MuninMul : 1f) * (1f + (TrainBonus(item) + TrustBonus(item) + FedBonus(item) + KnackBonus(item) + GearFx.DamagePct(item) + RuneStars.StrikePct(item)) / 100f) * StarveMul(item) * GiftUp.PowerOf(item, munin ? "mstrike" : "hstrike");
        }

        // why the blow is what it is, line by line (the book's tooltip)
        internal static string Why(bool munin, ItemDrop.ItemData item)
        {
            int tier = Tier(item);
            float b = BaseOf(munin, item), tr = TrainBonus(item), tu = TrustBonus(item), fe = FedBonus(item), kn = KnackBonus(item);
            float sum = tr + tu + fe + kn + GearFx.DamagePct(item) + RuneStars.StrikePct(item), blow = Blow(munin, item), starve = StarveMul(item);
            List<string> seen = BossesSeen(item);
            string from = tier == 0 ? Lang.T("ще жодного боса з ним на плечі") : Lang.F("найдальший бос, переможений з ним: {0}", BossNames[tier - 1]);
            string t = Lang.F("Основа: {0} ({1}{2})\n+ вишкіл {3}-го рівня: +{4}%\n+ довіра {5}-го рівня: +{6}%\n+ ситий: {7}\n{8}{9}{10}= {11} × {12}{13} = {14}\n\n{15}Удар не залежить від твоєї зброї. Боса один виліт ранить не більше ніж на {16}% здоров'я.", R(b), from, (munin ? Lang.F("; Мунін б'є на {0}% сильніше", R((MuninMul - 1f) * 100f)) : ""), Level(item), R(tr), Trust.Level(item), R(tu), (fe > 0f ? Lang.F("+{0}% (ще {1})", R(fe), RavenAbilities.Clock(FedLeft(item))) : Lang.F("0% (нагодуй — {0} хв по +{1}%)", R(FedMinutes), R(FedPct))), (kn > 0f ? Lang.F("+ хист «{0}»: +{1}%\n", Norov.KnackOf(item).Name(munin), R(kn)) : ""), GearFx.WhyLine(item) + RuneStars.WhyLine(item), (starve < 1f ? (Trust.Starving(item)
                    ? Lang.F("<color=#FF6B6B>× голод: −{0}% (ситість 0 — нагодуй)</color>\n", R(Trust.StarveStrikePct))
                    : Lang.F("<color=#B48CE6>× об'ївся: −{0}% (лінується, поки ситість понад {1})</color>\n", R(Satiety.StuffedStrikePct), R(Satiety.StuffedAt))) : ""), R(b), (1f + sum / 100f).ToString("0.00"), (starve < 1f ? " × " + starve.ToString("0.00") : ""), R(blow), (GearFx.FrostShareOf(item) > 0f ? Lang.F("«Холодний медальйон»: {0}% удару — мороз, він сповільнює ціль.\n", R(GearFx.FrostShareOf(item) * 100f)) : ""), R((munin ? BossCapMunin : BossCapHugin) * 100f));
            return t;
        }

        // where the crit chance comes from, the very sum of CritChance (the strike's and the book's tooltips)
        internal static string CritLine(bool munin, ItemDrop.ItemData item)
        {
            return Lang.F("Шанс криту {0}%: основа {1}% (у кожного ворона) + вишкіл {2}% + довіра {3}%{4}{5}.", R(CritChance(munin, item)), R(CritBonus), R(Lerp(munin ? MuninCrit : HuginCrit, Level(item))), R(Mathf.Max(0, Trust.Level(item) - 1) * CritPerTrust), (Norov.CritPts(item) > 0f ? Lang.F(" + хист «Влучний» {0}% (межа {1}%)", R(Norov.CritPts(item)), R(Norov.CritCap(item))) : ""), (GearFx.CritPts(item) > 0f ? Lang.F(" + «Монета на удачу» {0}%", R(GearFx.CritPts(item))) : ""));
        }

        // where the range comes from, the very product of RangeOf: the training, then the knack and the gear (the book)
        internal static string RangeWhy(bool munin, ItemDrop.ItemData item)
        {
            float train = Mathf.Lerp(Range, RangeTop, (Level(item) - 1) / (float)(MaxLevel - 1));
            float knack = (Norov.RangeMul(item) - 1f) * 100f, gear = (GearFx.RangeMul(item) - 1f) * 100f;
            Norov.Knack k = Norov.KnackOf(item);
            return Lang.F("Від вишколу {0}-го рівня: {1} м (від {2} м на 1-му до {3} м на {4}-му)\n{5}{6}{7}", Level(item), R(train), R(Range), R(RangeTop), MaxLevel, (knack > 0.5f ? Lang.F("+ хист «{0}»: +{1}%\n", (k != null ? k.Name(munin) : ""), R(knack)) : ""), (gear > 0.5f ? Lang.F("+ «Кігті комара»: +{0}%\n", R(gear)) : ""), (knack > 0.5f || gear > 0.5f ? Lang.F("= {0} × {1} = {2} м", R(train), (Norov.RangeMul(item) * GearFx.RangeMul(item)).ToString("0.00"), R(RangeOf(item))) : Lang.F("= {0} м", R(RangeOf(item)))));
        }

        private static string R(float v) { return Mathf.RoundToInt(v).ToString(); }

        internal static float FedLeft(ItemDrop.ItemData item) { return item != null ? WorldTime.Left(item.m_customData, FedAtKey) : 0f; }
        internal static void Fed(ItemDrop.ItemData item) { Fed(item, 1f); }
        internal static void Fed(ItemDrop.ItemData item, float mul) { if (item != null && FedMinutes > 0f) WorldTime.Set(item.m_customData, FedAtKey, FedMinutes * 60f * Mathf.Max(0.1f, mul)); }

        internal static float TrainBonus(ItemDrop.ItemData item) { return (Level(item) - 1) * TrainPct; }
        internal static float TrustBonus(ItemDrop.ItemData item) { return Mathf.Max(0, Trust.Level(item) - 1) * TrustPct; }
        internal static float FedBonus(ItemDrop.ItemData item) { return FedLeft(item) > 0f ? FedPct : 0f; }
        internal static float KnackBonus(ItemDrop.ItemData item) { return Norov.DamagePct(item); } // knack «Важкий дзьоб / Важкі кігті»

        // a starving raven (satiety 0) strikes weaker, and a stuffed one is too lazy to strike hard
        internal static float StarveMul(ItemDrop.ItemData item)
        {
            if (Trust.Starving(item) && !RuneStars.StarveFree(item)) return Mathf.Clamp01(1f - Trust.StarveStrikePct / 100f); // («Іній» ★★★: full strength)
            return Satiety.IsStuffed(item) ? Mathf.Clamp01(1f - Satiety.StuffedStrikePct / 100f) : 1f;
        }

        internal static float BaseOf(bool munin, ItemDrop.ItemData item) { return Base[Tier(item)] * (munin ? MuninMul : 1f); }

        // one blow before any crit
        internal static float Blow(bool munin, ItemDrop.ItemData item)
        {
            return BaseOf(munin, item) * (1f + (TrainBonus(item) + TrustBonus(item) + FedBonus(item) + KnackBonus(item) + GearFx.DamagePct(item) + RuneStars.StrikePct(item)) / 100f) * StarveMul(item) * GiftUp.PowerOf(item, munin ? "mstrike" : "hstrike"); // (+ «Кігтясте намисто», golden runes)
        }

        private static float Lerp(float[] range, int level) { return Mathf.Lerp(range[0], range[1], (level - 1) / (float)(MaxLevel - 1)); }

        internal static float CritChance(bool munin, ItemDrop.ItemData item)
        {
            float c = Lerp(munin ? MuninCrit : HuginCrit, Level(item)) + CritBonus + Mathf.Max(0, Trust.Level(item) - 1) * CritPerTrust + Norov.CritPts(item) + GearFx.CritPts(item); // (+ «Монета на удачу»)
            return Mathf.Clamp(c, 0f, Norov.CritCap(item)); // (knack «Влучний»: +5 and the cap 95)
        }
        internal static float DoubleChance(bool munin, ItemDrop.ItemData item) { return Lerp(munin ? MuninDouble : HuginDouble, Level(item)); }
        internal static float TripleChance(bool munin, ItemDrop.ItemData item) { return Lerp(munin ? MuninTriple : HuginTriple, Level(item)); }

        // ---------------------------------------------------------------- the flight

        internal sealed class Peck
        {
            public Character Target;
            public ZDOID Id;
            public bool Crit;
        }

        // why not now, or null (the dome: nobody fights under it)
        internal static string Refused(Player p)
        {
            if (Dome.Inside(p.transform.position)) return Lang.T("Під куполом ніхто не б'ється — і ворон теж");
            return null;
        }

        // the raven is already out: one flight at a time per raven
        internal static bool Busy(Player p, bool munin) { return StrikeFlight.Out(p, munin) || (!munin && DecoyFlight.Out(p)); } // (Hugin out luring: busy too)

        // from the ability: pick the targets, roll the blows, fly. Training, the words and (on a miss) half the strength
        // back all come once it is back on the shoulder, by what its blows really did.
        internal static void Launch(Player p, bool munin, ItemDrop.ItemData item, float paid, List<Character> targets)
        {
            List<Peck> plan = new List<Peck>();
            float pc = CritChance(munin, item), p2 = DoubleChance(munin, item), p3 = TripleChance(munin, item);
            foreach (Character t in targets)
            {
                int n = 1;
                double roll = Rng.NextDouble() * 100.0;
                // a series only on the first target: Munin rakes the other two in passing, once each
                if (t == targets[0]) { if (roll < p3) n = 3; else if (roll < p3 + p2) n = 2; }
                bool sneak = Unaware(t) && !Shadow.On(p); // struck unawares: a sure crit (not on top of Hugin's shadow, which has its own strike)
                for (int i = 0; i < n; i++)
                {
                    bool c = (i == 0 && sneak) || Rng.NextDouble() * 100.0 < pc;
                    if (i == 0 && Quirks.BraveCrit(p, munin, t)) c = true; // «Боязкий»: its first blow at a wounded boss
                    plan.Add(new Peck { Target = t, Id = t.GetZDOID(), Crit = c });
                }
            }
            Vector3 miss = MissPoint(p, RangeOf(item));
            float blow = Blow(munin, item), frost = GearFx.FrostShareOf(item), fire = RuneStars.FireShare(item), steal = RuneStars.Steal(item); // (this raven's, even if another is worn by the time it strikes)
            Dictionary<Character, float> capLeft = new Dictionary<Character, float>();
            Tally tally = new Tally();
            float range = RangeOf(item);
            float flown = Vector3.Distance(p.GetEyePoint(), miss); // with nobody to strike: how far it got before a wall or the ground
            // the raven has its say once it is back on the shoulder, with the corner's tally: not on the way out
            StrikeFlight.Fly(p, munin, plan, miss, (k, at) => Hit(p, munin, plan[k], blow, frost, fire, steal, capLeft, at, tally), () => Home(p, munin, item, paid, tally, range, flown));
            Send(p, munin, plan, miss);
        }

        // back on the shoulder (or gone from it): what the flight earned, what it says, the line in the corner
        private static void Home(Player p, bool munin, ItemDrop.ItemData item, float paid, Tally tally, float range, float flown)
        {
            bool landed = tally.Order.Count > 0;
            int before = Level(item);
            // training comes from strikes that reached someone: a flight into empty air (or to a corpse) teaches nothing
            if (landed) Put(item, XpKey, (Flights(item) + 1).ToString());
            int after = Level(item);
            float back = 0f;
            if (!landed && MissRefund > 0f)
            {
                back = Power.Refund(item, paid * MissRefund);
                RavenSkills.LetOff(munin ? "mstrike" : "hstrike", MissRefund);
            }
            bool crit = false, combo = munin && tally.Order.Count > 1;
            foreach (Character c in tally.Order) { if (tally.Crits[c] > 0) crit = true; if (tally.Hits[c] > 1) combo = true; }
            ReportWhenBled(p, munin, tally, range, flown, back);
            if (p == null || p.IsDead()) return;
            if (after > before)
            {
                RavenMsg.Center(p, Lang.F("{0}: вишкіл {1}-го рівня! Удар сильніший.", Plugin.RavenName(munin), after));
                RavenFx.Play("sfx_levelup", p.transform.position, Quaternion.identity);
            }
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(SayHome(p, munin, tally, landed, crit, combo, after > before));
        }

        // The word after a strike, once it is known how it ended: a moment for the death to come over the network, and
        // for Munin until his wound has bled out (it may still kill). Killed (at least one of them): _kill, _crit_kill,
        // _combo_kill; a hit that killed nothing: _alive, something went wrong (Munin takes it hard); nothing reached: _miss. Not after every strike (SayChance); a new level of training always.
        internal static float SayChance = 0.7f;
        private static System.Collections.IEnumerator SayHome(Player p, bool munin, Tally tally, bool landed, bool crit, bool combo, bool levelUp)
        {
            float until = Time.time + Marks.WoundSeconds + 2f;
            yield return new WaitForSeconds(0.6f);
            while (munin && landed && Time.time < until && Marks.Bleeding(p, tally.Order)) yield return null;
            if (p == null || p.IsDead()) yield break;
            bool kill = false;
            foreach (Character c in tally.Order) if (c == null || c.IsDead()) kill = true;
            string pre = munin ? "mstrike" : "hstrike";
            string say = levelUp ? pre + "_level" : !landed ? pre + "_miss" : !kill ? (crit || combo) && UnityEngine.Random.value < 0.7f ? pre + (crit ? "_crit" : "_combo") : pre + "_alive" : crit ? pre + "_crit_kill" : combo ? pre + "_combo_kill" : pre + "_kill";
            if (levelUp || UnityEngine.Random.value < SayChance)
            {
                Composer.Extra.Clear();
                if (landed)
                {
                    Character first = tally.Order[0];
                    // the plain name always; its cases ({target_gen} {target_ins}) when creature_names.txt knows it
                    Composer.Extra["target"] = tally.Names[first];
                    if (Plugin.Instance != null && Plugin.Instance.PhraseDir != null) Composer.CreatureForms(Plugin.Instance.PhraseDir, Composer.Extra, "target", tally.Tokens[first]);
                }
                RavenSkills.StrikeSay(munin, say);
                Composer.Extra.Clear();
            }
            Quirks.StrikeHome(p, munin, crit, kill); // «Задавака»: shows off
        }

        private static Transform Aim()
        {
            Camera cam = Utils.GetMainCamera();
            return cam != null ? cam.transform : Player.m_localPlayer.transform;
        }

        // nobody to strike: it flies where the camera looks, as far as it reaches, but stops short of the ground, a wall or a tree in the
        // way (from the camera and from the player's eyes, so not through a wall behind a third-person camera)
        // and stays above the ground or water under that spot
        internal static float MissClear = 1.5f;

        private static Vector3 MissPoint(Player p, float range)
        {
            Transform aim = Aim();
            Vector3 eye = p.GetEyePoint();
            Vector3 dir = aim.forward;
            // as far as this raven reaches (its training), counted from the player, along the look
            float reach = range + Mathf.Max(0f, Vector3.Dot(eye - aim.position, dir));
            RaycastHit hit;
            Vector3 at = aim.position + dir * reach;
            if (Physics.Raycast(aim.position, dir, out hit, reach, Sight, QueryTriggerInteraction.Ignore)) at = hit.point - dir * MissClear;
            Vector3 way = at - eye;
            if (way.sqrMagnitude > 0.01f && Physics.SphereCast(eye, 0.3f, way.normalized, out hit, way.magnitude, Sight, QueryTriggerInteraction.Ignore))
                at = eye + way.normalized * Mathf.Max(1f, hit.distance - MissClear);
            float low = Floor(at) + MissClear;
            if (at.y < low) at.y = low;
            return at;
        }

        // what is under a spot: the terrain, a floor right under it (never a roof above it: indoors it stays indoors), the water
        internal static float Floor(Vector3 at)
        {
            RaycastHit hit;
            float ground = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(at) : float.MinValue;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out hit, 50f, Sight, QueryTriggerInteraction.Ignore)) ground = Mathf.Max(ground, hit.point.y);
            if (ZoneSystem.instance != null) ground = Mathf.Max(ground, ZoneSystem.instance.m_waterLevel);
            return ground;
        }

        private static int _sight;
        private static int Sight { get { if (_sight == 0) _sight = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small"); return _sight; } }

        internal static float MuninReach = 8f; // how far from the first target Munin goes on to the others

        internal static bool SeenFrom(Vector3 from, Character c) { return Clear(from, c, null, Body.Center(c)); }

        // seen: its middle, or its head over a bush or a rise (a grazing deer's middle is often behind a tussock)
        internal static bool Seen(Player p, Character c) { Vector3 eye = p.GetEyePoint(); return Clear(eye, c, p, Body.Center(c)) || Clear(eye, c, p, Body.Head(c)); }
        internal static float AimBeyond = 60f; // m past the range a creature in your sights is still noticed (and said too far)

        // Nothing in the way but the creature itself (and the one looking): a modded creature's body is often on the
        // "Default" layer, not the game's "character" one, and the line to its middle hit its own hide ("not seen").
        // One cheap line first; only when it hits something is the whole line looked at.
        private static readonly RaycastHit[] _seenHits = new RaycastHit[16];
        private static bool Clear(Vector3 from, Character c, Character self, Vector3 to)
        {
            if (!Physics.Linecast(from, to, Sight, QueryTriggerInteraction.Ignore)) return true;
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(from, d / len, _seenHits, len, Sight, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = _seenHits[i].collider;
                if (col == null) continue;
                Transform t = col.transform;
                if (t.IsChildOf(c.transform) || (self != null && t.IsChildOf(self.transform))) continue;
                return false;
            }
            return true;
        }

        // A hunter: whatever lives and is not ours. The one in your sights, if it sees it and reaches it; else the
        // nearest one ahead; else the nearest one it can see at all, aside or behind you. Munin takes up to two more
        // next to the first. Nobody at all: it flies where you look all the same. 'note': why the one in your sights
        // was passed over (too far for this raven, hidden from it), for the corner.
        internal static List<Character> Targets(Player p, bool munin, float range, out string note)
        {
            note = null;
            List<Character> found = new List<Character>();
            Transform aim = Aim();
            Vector3 from = aim.position, dir = aim.forward;
            Character inSight = null, ahead = null, any = null, sightOut = null;
            float sightScore = float.MaxValue, aheadScore = float.MaxValue, anyDist = float.MaxValue, outScore = float.MaxValue, outDist = 0f;
            bool outFar = false;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Prey(p, c)) continue;
                float dist = Vector3.Distance(p.transform.position, c.transform.position);
                if (dist > range + AimBeyond) continue;
                Vector3 to = Body.Center(c) - from;
                float angle = Vector3.Angle(dir, to);
                float allow = Mathf.Max(AimDegrees, Mathf.Atan2(Body.Radius(c) + 0.6f, Mathf.Max(0.5f, to.magnitude)) * Mathf.Rad2Deg);
                float score = angle + dist * 0.15f;
                bool far = dist > range;
                if (far || !Seen(p, c))
                {
                    // in your sights, but not for this raven: remembered for the note in the corner, another one is taken
                    if (angle <= allow && score < outScore) { outScore = score; sightOut = c; outFar = far; outDist = dist; }
                    continue;
                }
                if (angle <= allow && score < sightScore) { sightScore = score; inSight = c; }
                if (angle <= 70f && score < aheadScore) { aheadScore = score; ahead = c; }
                if (dist < anyDist) { anyDist = dist; any = c; }
            }
            if (inSight == null && sightOut != null)
            {
                string who = Localization.instance != null ? Lang.Game(sightOut.m_name) : sightOut.m_name;
                note = outFar ? Lang.F("{0} задалеко — {1} м, долітає на {2}", who, R(outDist), R(range)) : Lang.F("{0} за перешкодою — не бачить", who);
                if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Strike aim: " + Utils.GetPrefabName(sightOut.gameObject) + " in the sights, " + R(outDist) + " m, " + (outFar ? "beyond the range " + R(range) : "hidden") + ": passed over");
            }
            Character best = inSight != null ? inSight : ahead != null ? ahead : any;
            if (best == null) { WhyNone(p, range); return found; }
            if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Strike aim: " + Utils.GetPrefabName(best.gameObject) + " (" + (best == inSight ? "in the sights" : best == ahead ? "nearest ahead" : "nearest") + ")");
            found.Add(best);
            if (!munin) return found;
            List<Character> near = new List<Character>();
            foreach (Character c in Character.GetAllCharacters())
                // the next ones: near the first (Munin flies on from there), and in sight from it, not from you
                if (c != best && Prey(p, c) && Vector3.Distance(c.transform.position, best.transform.position) < MuninReach && SeenFrom(Body.Center(best), c)) near.Add(c);
            near.Sort((a, b) => Vector3.Distance(a.transform.position, best.transform.position).CompareTo(Vector3.Distance(b.transform.position, best.transform.position)));
            for (int i = 0; i < near.Count && found.Count < 3; i++) found.Add(near[i]);
            return found;
        }

        // nobody to strike: what was near and why it was passed over, in the log (for creatures from other mods)
        private static void WhyNone(Player p, float range)
        {
            if (Plugin.Instance == null) return;
            List<string> l = new List<string>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == p || c.IsDead() || c is Player) continue;
                float dist = Vector3.Distance(p.transform.position, c.transform.position);
                if (dist > range + 10f) continue;
                string why = dist > range ? "far" : !Prey(p, c) ? "not prey (" + c.GetFaction() + (c.IsTamed() ? ", tamed" : "") + (c is Humanoid ? ", humanoid" : "") + ")" : !Seen(p, c) ? "not seen" : "?";
                l.Add(Utils.GetPrefabName(c.gameObject) + " " + Mathf.RoundToInt(dist) + " m: " + why);
                if (l.Count >= 8) break;
            }
            if (l.Count > 0) Plugin.Instance.Log("Strike found nobody within " + Mathf.RoundToInt(range) + " m. Near: " + string.Join("; ", l.ToArray()));
        }

        // anything alive that is not a player, not tamed and not of the friendly kinds (players' own folk and summons,
        // the dvergr): monsters asleep or unaware, deer and boars to hunt, the training dummy
        internal static bool Prey(Player p, Character c)
        {
            if (c == null || c == p || c.IsDead() || c is Player || c.IsTamed()) return false;
            if (BeingTamed(c)) return false; // fed and on its way to being tamed: not prey
            // someone, not something: a humanoid that is not hostile to you is a villager, a guard, a trader's folk (NPC mods)
            if (c is Humanoid && !BaseAI.IsEnemy(p, c)) return false;
            Character.Faction f = c.GetFaction();
            if (f == Character.Faction.Players || f == Character.Faction.PlayerSpawned || f == Character.Faction.Dverger) return false;
            ZNetView nv = c.GetComponent<ZNetView>();
            return nv != null && nv.IsValid();
        }

        // someone has started taming it: it was fed (and is not hungry yet), or its taming is under way
        internal static bool BeingTamed(Character c)
        {
            Tameable t = c.GetComponent<Tameable>();
            ZNetView nv = c.GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (t == null || zdo == null) return false;
            if (zdo.GetFloat(ZDOVars.s_tameTimeLeft, t.m_tamingTime) < t.m_tamingTime - 1f) return true;
            return zdo.GetLong(ZDOVars.s_tameLastFeeding, 0L) != 0L && !t.IsHungry();
        }

        // it did not see it coming: a monster not alerted, an animal grazing
        internal static bool Unaware(Character c)
        {
            BaseAI ai = c != null ? c.GetComponent<BaseAI>() : null;
            if (ai == null) return false;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid() && !nv.IsOwner()) return !nv.GetZDO().GetBool(ZDOVars.s_alert); // run in another game: its word, not our copy's
            return !ai.IsAlerted();
        }

        // ---------------------------------------------------------------- the blow (only the striker's game deals it)

        internal const float Marker = -1f; // m_skillRaiseAmount below this: a raven's blow, its size in it

        // what one flight did, for the line in the corner once the raven is back (its blows land far off, where the
        // game draws no numbers)
        private sealed class Tally
        {
            public readonly List<Character> Order = new List<Character>();
            public readonly Dictionary<Character, float> Dealt = new Dictionary<Character, float>(), Bled = new Dictionary<Character, float>();
            public readonly Dictionary<Character, int> Hits = new Dictionary<Character, int>(), Crits = new Dictionary<Character, int>();
            public readonly Dictionary<Character, string> Names = new Dictionary<Character, string>();
            public readonly Dictionary<Character, string> Tokens = new Dictionary<Character, string>(); // the name key, for its cases in the words

            public void Add(Character c, float v, bool crit)
            {
                if (!Dealt.ContainsKey(c))
                {
                    Order.Add(c); Dealt[c] = 0f; Hits[c] = 0; Crits[c] = 0;
                    Names[c] = Localization.instance != null ? Lang.Game(c.m_name) : c.m_name;
                    Tokens[c] = c.m_name;
                }
                Dealt[c] += v; Hits[c]++;
                if (crit) Crits[c]++;
            }

            // Munin's wound, drop by drop (it bleeds on after the raven is back)
            public void AddBleed(Character c, float v)
            {
                if (c == null || !Dealt.ContainsKey(c)) return;
                float had;
                Bled.TryGetValue(c, out had);
                Bled[c] = had + v;
            }
        }

        // «Удар в очі: Сіролюдик −112 (2 удари, крит)» — one line, through the game's own corner messages: the game
        // shows it at the top left, SeneaL UI turns it into its own card with the gift's picture
        private static void Report(Player p, bool munin, Tally t, float range, float flown, float back)
        {
            if (p == null || p.IsDead()) return;
            string head = munin ? Lang.T("Кігті") : Lang.T("Удар в очі");
            string body;
            if (t.Order.Count == 0) body = Lang.F("нікого в межах {0} м{1}{2}", R(range), (flown < range - 3f ? Lang.F(" · пролетів {0} м, далі перешкода", R(flown)) : ""), (back >= 0.5f ? Lang.F(" · повернулось {0} сили", R(back)) : ""));
            else
            {
                List<string> parts = new List<string>();
                foreach (Character c in t.Order)
                {
                    int hits = t.Hits[c], crits = t.Crits[c];
                    List<string> notes = new List<string>();
                    if (hits > 1) notes.Add(Lang.N(hits, "удар|удари|ударів"));
                    if (crits > 0) notes.Add(crits > 1 ? Lang.F("крит ×{0}", crits) : Lang.T("крит"));
                    float bled;
                    if (munin && t.Bled.TryGetValue(c, out bled) && bled >= 0.5f) notes.Add(Lang.F("кровотеча −{0}", R(bled)));
                    if (c == null || c.IsDead()) notes.Add(Lang.T("вбито"));
                    else if (!munin) notes.Add(Lang.T("осліплено"));
                    parts.Add(t.Names[c] + " −" + R(t.Dealt[c]) + (notes.Count > 0 ? " (" + string.Join(", ", notes.ToArray()) + ")" : ""));
                }
                body = string.Join(" · ", parts.ToArray());
            }
            p.Message(MessageHud.MessageType.TopLeft, head + ": " + body, 0, AbilityIcons.For(munin ? "mstrike" : "hstrike", munin));
        }

        // Munin's wounds bleed on for a few seconds after he is back: the line waits for the last drop
        private static void ReportWhenBled(Player p, bool munin, Tally t, float range, float flown, float back)
        {
            if (!munin || t.Order.Count == 0 || Plugin.Instance == null || !Marks.Bleeding(p, t.Order)) { Report(p, munin, t, range, flown, back); return; }
            Plugin.Instance.StartCoroutine(ReportLater(p, munin, t, range, flown, back));
        }

        private static System.Collections.IEnumerator ReportLater(Player p, bool munin, Tally t, float range, float flown, float back)
        {
            float until = Time.time + Marks.WoundSeconds + 2f;
            while (Time.time < until && Marks.Bleeding(p, t.Order)) yield return null;
            Report(p, munin, t, range, flown, back);
        }

        private static void Hit(Player p, bool munin, Peck peck, float blow, float frostShare, float fireShare, float steal, Dictionary<Character, float> capLeft, Vector3 from, Tally tally)
        {
            Character t = peck.Target;
            if (t == null || t.IsDead() || p == null) return;
            float v = blow * (peck.Crit ? 2f : 1f);
            if (t.IsBoss())
            {
                float left;
                if (!capLeft.TryGetValue(t, out left)) left = t.GetMaxHealth() * (munin ? BossCapMunin : BossCapHugin);
                v = Mathf.Min(v, left);
                capLeft[t] = left - v;
                if (v <= 0.5f) return;
            }
            HitData hit = new HitData();
            float frost = v * frostShare, fire = v * fireShare; // «Холодний медальйон»: part of it frost (the game's frost slows the target); «Сонце» ★★★: fire (it burns)
            if (munin) hit.m_damage.m_slash = v - frost - fire; else hit.m_damage.m_pierce = v - frost - fire;
            hit.m_damage.m_frost = frost;
            hit.m_damage.m_fire = fire;
            hit.m_point = Body.Center(t);
            Vector3 d = Body.Center(t) - from;
            hit.m_dir = d.sqrMagnitude > 0.001f ? d.normalized : t.transform.forward;
            hit.m_pushForce = munin ? 12f : 4f;
            hit.m_staggerMultiplier = munin ? 1.2f : 0.4f;
            hit.m_backstabBonus = 1f;
            hit.m_blockable = false;
            hit.m_dodgeable = false;
            hit.m_ranged = true;
            hit.m_skill = Skills.SkillType.None;
            hit.m_skillRaiseAmount = Marker - v; // carried to the target's game: there the blow is cut back to v
            hit.SetAttacker(p);
            t.Damage(hit);
            if (steal > 0f && !p.IsDead()) p.Heal(v * steal, true); // «Кров» ★★★
            bool first = tally == null || !tally.Dealt.ContainsKey(t);
            if (tally != null) tally.Add(t, v, peck.Crit);
            if (first)
            {
                // the raven's mark on it: Hugin blinds, Munin tears a bleeding wound
                Marks.Put(t, munin);
                if (munin) Marks.StartBleed(p, t, blow, capLeft, tally != null ? new Action<Character, float>(tally.AddBleed) : null);
            }
            if (peck.Crit && DamageText.instance != null) DamageText.instance.ShowText(DamageText.TextType.Weak, hit.m_point + Vector3.up * 0.6f, Lang.T("КРИТ ×2"));
            if (peck.Crit) Nicks.OnCrit(p, munin); // a deed for its nickname
        }

        // in the target's game, after armour and resistances: never more than the raven's blow
        internal static void Cut(HitData hit)
        {
            if (hit == null || hit.m_skillRaiseAmount > Marker) return;
            float want = Marker - hit.m_skillRaiseAmount;
            float total = hit.GetTotalDamage();
            if (total > want && total > 0f) hit.m_damage.Modify(want / total);
        }

        // ---------------------------------------------------------------- seen by the others

        private static void Send(Player p, bool munin, List<Peck> plan, Vector3 miss)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetZDOID());
            pkg.Write(munin);
            pkg.Write(miss);
            pkg.Write(plan.Count);
            foreach (Peck k in plan) { pkg.Write(k.Id); pkg.Write(k.Crit); }
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        private static void OnStrike(long sender, ZPackage pkg)
        {
            try
            {
                if (sender == ZDOMan.GetSessionID()) return;
                ZDOID who = pkg.ReadZDOID();
                bool munin = pkg.ReadBool();
                Vector3 miss = pkg.ReadVector3();
                int n = pkg.ReadInt();
                List<Peck> plan = new List<Peck>();
                for (int i = 0; i < n; i++)
                {
                    ZDOID id = pkg.ReadZDOID();
                    bool crit = pkg.ReadBool();
                    GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
                    Character c = go != null ? go.GetComponent<Character>() : null;
                    if (c != null) plan.Add(new Peck { Target = c, Id = id, Crit = crit });
                }
                Player me = Player.m_localPlayer;
                foreach (Player pl in Player.GetAllPlayers())
                {
                    if (pl == null || pl.GetZDOID() != who) continue;
                    if (me != null && Vector3.Distance(me.transform.position, pl.transform.position) > 120f) return;
                    StrikeFlight.Fly(pl, munin, plan, miss, null);
                    return;
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike message unreadable: " + e.Message); }
        }

        // ---------------------------------------------------------------- bosses that fell with the raven on the shoulder

        private sealed class Fight
        {
            public string Prefab;
            public ItemDrop.ItemData Hugin, Munin; // null: not here at the start, or left during the fight
            public Vector3 Last;
            public float GoneAt; // the boss vanished from this game (died, or left the loaded area): kept a little for the news of its death
        }

        private static readonly Dictionary<Character, Fight> Fights = new Dictionary<Character, Fight>();
        internal static float WatchRange = 50f;

        // twice a second: a boss near you, full of health, starts a fight with whichever ravens sit on you now;
        // a raven taken off or flown away during it no longer counts for this boss
        internal static void Watch(Player p)
        {
            if (p == null) return;
            if (Later.Count > 0) TellLater(p);
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            if (h != null && (Trust.Away(h) || !Trust.Active(h) || Dome.Mine())) h = null;
            if (m != null && (Trust.Away(m) || !Trust.Active(m))) m = null;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || !c.IsBoss() || c.IsDead()) continue;
                string prefab = Utils.GetPrefabName(c.gameObject);
                if (Array.IndexOf(Bosses, prefab) < 0) continue;
                Fight f;
                if (!Fights.TryGetValue(c, out f))
                {
                    if (Vector3.Distance(p.transform.position, c.transform.position) > WatchRange) continue;
                    bool fresh = c.GetHealthPercentage() >= 0.98f; // from the very start, or it does not count
                    Fights[c] = f = new Fight { Prefab = prefab, Hugin = fresh ? h : null, Munin = fresh ? m : null };
                }
                f.Last = c.transform.position;
                if (f.Hugin != null && !ReferenceEquals(f.Hugin, h)) f.Hugin = null;
                if (f.Munin != null && !ReferenceEquals(f.Munin, m)) f.Munin = null;
            }
            List<Character> gone = null;
            foreach (KeyValuePair<Character, Fight> kv in Fights)
            {
                bool drop;
                if (kv.Key == null)
                {
                    if (kv.Value.GoneAt <= 0f) kv.Value.GoneAt = Time.time;
                    drop = Time.time - kv.Value.GoneAt > 15f;
                }
                else drop = !kv.Key.IsDead() && Vector3.Distance(p.transform.position, kv.Key.transform.position) > WatchRange * 2f; // walked away from it
                if (drop) { if (gone == null) gone = new List<Character>(); gone.Add(kv.Key); }
            }
            if (gone != null) foreach (Character c in gone) Fights.Remove(c);
        }

        internal static void ResetFights() { Fights.Clear(); Later.Clear(); }

        // in the game where the boss dies: tell everyone which and where
        internal static void BossDied(Character c)
        {
            if (c == null || !c.IsBoss() || ZRoutedRpc.instance == null) return;
            string prefab = Utils.GetPrefabName(c.gameObject);
            if (Array.IndexOf(Bosses, prefab) < 0) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(prefab);
            pkg.Write(c.transform.position);
            // the last blow, if a raven's (its mark): whose and which (Munin tears, Hugin pierces) — added at the end, so
            // an older game reads the two fields above and stops
            HitData last = HitInfo.LastHit(c);
            Player by = last != null && last.m_skillRaiseAmount <= Marker ? last.GetAttacker() as Player : null;
            pkg.Write(by != null ? by.GetPlayerID() : 0L);
            pkg.Write(by != null && last.m_damage.m_slash > 0f);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcBoss, pkg);
        }

        private static void OnBossDown(long sender, ZPackage pkg)
        {
            try
            {
                string prefab = pkg.ReadString();
                Vector3 at = pkg.ReadVector3();
                long felledBy = 0L;
                bool felledMunin = false;
                if (pkg.GetPos() < pkg.Size()) { felledBy = pkg.ReadLong(); felledMunin = pkg.ReadBool(); } // (an older game sends no more)
                Player p = Player.m_localPlayer;
                if (p == null) return;
                Character key = null;
                Fight fight = null;
                foreach (KeyValuePair<Character, Fight> kv in Fights)
                    if (kv.Value.Prefab == prefab && Vector3.Distance(kv.Value.Last, at) < 40f) { key = kv.Key; fight = kv.Value; break; }
                if (fight == null) return;
                Fights.Remove(key);
                if (Vector3.Distance(p.transform.position, at) > WatchRange * 1.5f) return; // you were not there at the end
                Memory.OnBoss(p, prefab);
                ItemDrop.ItemData h, m;
                RavenItem.Worn(p, out h, out m);
                if (fight.Hugin != null && ReferenceEquals(fight.Hugin, h)) Nicks.OnBoss(p, h, prefab); // each fall counts for the nicknames
                if (fight.Munin != null && ReferenceEquals(fight.Munin, m)) Nicks.OnBoss(p, m, prefab);
                if (felledBy != 0L && felledBy == p.GetPlayerID()) Nicks.OnFelled(p, felledMunin ? m : h, prefab); // your raven struck the last blow
                Credit(p, false, fight.Hugin, h, prefab);
                Credit(p, true, fight.Munin, m, prefab);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Boss message unreadable: " + e.Message); }
        }

        private static void Credit(Player p, bool munin, ItemDrop.ItemData atStart, ItemDrop.ItemData now, string prefab)
        {
            if (atStart == null || !ReferenceEquals(atStart, now)) return;
            List<string> seen = BossesSeen(now);
            if (seen.Contains(prefab)) return;
            int tierBefore = Tier(now);
            seen.Add(prefab);
            Put(now, BossKey, string.Join(",", seen.ToArray()));
            int tierAfter = Tier(now);
            int i = Array.IndexOf(Bosses, prefab);
            bool stronger = tierAfter > tierBefore;
            float was = BlowWithTier(munin, now, tierBefore), becomes = BlowWithTier(munin, now, tierAfter);
            // not over the game's own banner of the boss's fall: a few seconds later, from the shoulder, and a line
            // in the corner that stays readable (Munin a moment after Hugin, so the two do not talk over each other)
            Later.Add(new News
            {
                At = Time.time + 6f + (munin ? 3f : 0f),
                Munin = munin,
                Tag = stronger ? "strike_stronger" : "strike_boss",
                Corner = Plugin.RavenName(munin) + (stronger ? Lang.F(": удар сильнішає — {0} → {1}", R(was), R(becomes)) : Lang.F(" бачив, як упав {0}", BossNames[i])),
            });
        }

        private sealed class News { public float At; public bool Munin; public string Tag, Corner; }
        private static readonly List<News> Later = new List<News>();

        private static void TellLater(Player p)
        {
            for (int k = Later.Count - 1; k >= 0; k--)
            {
                News n = Later[k];
                if (Time.time < n.At) continue;
                Later.RemoveAt(k);
                if (p == null || p.IsDead()) continue;
                p.Message(MessageHud.MessageType.TopLeft, n.Corner);
                ScreamFx.Caw(p, n.Munin, n.Munin ? 0.85f : 1.1f, 0.8f);
                RavenSkills.StrikeSay(n.Munin, n.Tag);
            }
        }
    }

    // ---------------------------------------------------------------- the raven in the air

    // The very raven from the shoulder lets go, flies an arc to its target, dives, strikes (once or in a series),
    // swings on to the next target, then comes back and lands. Every game flies its own copy; only the striker's
    // game deals the blows (onImpact).
    internal class StrikeFlight : MonoBehaviour, IRavenUser
    {
        // only leaving the shoulder is stronger: the raven goes (the leaving flies it on), the strike is over
        private bool _yielded;
        public void Yield()
        {
            _yielded = true;
            if (_onHome != null) { Action home = _onHome; _onHome = null; try { home(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike report failed: " + e.Message); } }
            DestroyImmediate(this);
        }

        private Player _owner;
        private bool _munin;
        private List<Strike.Peck> _plan;
        private Vector3 _miss;
        private Action<int, Vector3> _onImpact;
        private Action _onHome; // the striker's game: the flight's result, once the raven is back
        private ShoulderPin _pin;
        private ShoulderLife _life;
        private bool _clone; // no raven was drawn on the shoulder: a stand-in that goes away at the end
        private Unhooked _hook; // the shoulder's raven, let go of the body's bone while it flies

        private int _step;          // which peck now; _plan.Count: going home
        private int _phase;         // 0 out to the target, 1 bouncing back after the blow, 2 home
        private Vector3 _from;
        private float _t0;
        private float _born;
        private float _missDiveAt, _missDiveUntil;
        private bool _missTurnoutSet;
        private RavenMotor _motor;  // the raven's motor: flies it (and keeps its speed when it was taken in the air)
        private FlightBody _body;   // the motor's body for this flight (RavenFlight)
        private ParticleSystem _trail; // the feathers it sheds on the way
        private float _lastNear = float.MaxValue; // the new flight: closest it got to this target (passing it counts as there)
        private Vector3 _reOffset;  // after a blow, where the next dive starts, off the prey: up and back the way it came in

        private static readonly Dictionary<Player, StrikeFlight>[] Flying = { new Dictionary<Player, StrikeFlight>(), new Dictionary<Player, StrikeFlight>() };

        // the bird in the air for this player's strike (the shoulder model or its stand-in), or null
        internal static GameObject FlyerOf(Player p, bool munin)
        {
            StrikeFlight f;
            return p != null && Flying[munin ? 1 : 0].TryGetValue(p, out f) && f != null ? f.gameObject : null;
        }

        internal static bool Out(Player p, bool munin)
        {
            StrikeFlight f;
            return p != null && Flying[munin ? 1 : 0].TryGetValue(p, out f) && f != null;
        }

        internal static void Fly(Player owner, bool munin, List<Strike.Peck> plan, Vector3 miss, Action<int, Vector3> onImpact) { Fly(owner, munin, plan, miss, onImpact, null); }

        internal static void Fly(Player owner, bool munin, List<Strike.Peck> plan, Vector3 miss, Action<int, Vector3> onImpact, Action onHome)
        {
            if (owner == null) return;
            GameObject model = Shoulder.ModelOf(owner, munin);
            if (model != null && !AnimDirector.CanTake(model, RavenAct.Strike)) model = null; // leaving for good: a stand-in strikes
            bool clone = false;
            if (model == null)
            {
                model = StandIn(owner, munin);
                clone = true;
                if (model == null) { if (onImpact != null) for (int i = 0; i < plan.Count; i++) onImpact(i, owner.GetEyePoint()); if (onHome != null) onHome(); return; }
            }
            StrikeFlight old = model.GetComponent<StrikeFlight>();
            if (old != null) DestroyImmediate(old); // at once: its clean-up (pin back on) must not come after this flight began
            StrikeFlight f = model.AddComponent<StrikeFlight>();
            f._owner = owner; f._munin = munin; f._plan = plan; f._miss = miss; f._onImpact = onImpact; f._onHome = onHome; f._clone = clone;
            f._pin = model.GetComponent<ShoulderPin>();
            f._life = model.GetComponent<ShoulderLife>();
            f._motor = RavenMotor.Of(model, munin, owner);
            AnimDirector.Take(model, RavenAct.Strike, f, clone ? "strike, a stand-in" : "strike"); // whoever had it lets go first
            if (f._pin != null) f._pin.enabled = false; // let go of the shoulder
            if (!clone) f._hook = Unhooked.From(model.transform);
            f._born = Time.time;
            f._missDiveAt = plan.Count == 0 ? f._born + 0.45f : 0f;
            f.Begin(0, 0);
            // a jump off the shoulder towards where it goes, up and out
            Vector3 to = f.Target() - model.transform.position;
            Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : owner.transform.forward;
            f._motor.Launch(dir * 3f + Vector3.up * 2.5f, "удар", 0.7f); // more inertia in a strike's flight
            f._body = f._motor.Body;
            Flying[munin ? 1 : 0][owner] = f;
            if (owner == Player.m_localPlayer || !Shadow.Flagged(owner)) f._trail = Feathers.Trail(model.transform, munin); // no trail to follow to someone in the shadow
            RavenFx.Play("sfx_raven_flap", model.transform.position, Quaternion.identity);
        }

        // the landing panel: the shoulder raven put this far off in the air and sent home, as after a strike
        internal static bool TestHome(Player owner, bool munin, Vector3 from)
        {
            if (owner == null) return false;
            GameObject model = Shoulder.ModelOf(owner, munin);
            if (model == null || model.GetComponent<StrikeFlight>() != null || !AnimDirector.CanTake(model, RavenAct.Strike)) return false;
            StrikeFlight f = model.AddComponent<StrikeFlight>();
            f._owner = owner; f._munin = munin; f._plan = new List<Strike.Peck>(); f._miss = from;
            f._pin = model.GetComponent<ShoulderPin>();
            f._life = model.GetComponent<ShoulderLife>();
            f._motor = RavenMotor.Of(model, munin, owner);
            bool flying = f._motor.Airborne; // already in the air (an outing): sent home from where it is, at its speed (the takeover test)
            AnimDirector.Take(model, RavenAct.Strike, f, "test landing");
            if (f._pin != null) f._pin.enabled = false;
            f._hook = Unhooked.From(model.transform);
            f._born = Time.time;
            if (!flying) model.transform.position = from;
            Vector3 to = owner.GetEyePoint() - from;
            if (f._motor.Launch((to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward) * 4f, "додому", 1f)) f._motor.Body.Airborne();
            f._body = f._motor.Body;
            f.Begin(0, 2);
            Flying[munin ? 1 : 0][owner] = f;
            return true;
        }

        internal static GameObject StandIn(Player owner, bool munin)
        {
            Raven source = Shoulder.Template(munin);
            if (source == null || source.m_visual == null) return null;
            GameObject go = Instantiate(source.m_visual, owner.GetEyePoint() + owner.transform.right * (munin ? -0.3f : 0.3f), owner.transform.rotation);
            go.SetActive(true);
            AppearMute.Quiet(go); // (the shoulder's raven off on its strike: no appearing out of nowhere)
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Shared.RemoveExclamation(source, go);
            go.transform.localScale = source.m_visual.transform.lossyScale * Shoulder.Scale * Shoulder.SizeFor(Mathf.Max(1, Trust.LevelWorn(munin))); // the shoulder raven's own size, not the game's big one
            Shoulder.Perch(go.GetComponentInChildren<Animator>());
            ShoulderLife life = go.AddComponent<ShoulderLife>();
            life.Init(owner, munin, Mathf.Max(1, Trust.LevelWorn(munin)));
            RavenWear.Dress(go, munin, RavenWear.OnPlayer(owner, munin));
            return go;
        }

        private void Begin(int step, int phase)
        {
            _step = step; _phase = phase;
            _from = transform.position;
            _lastNear = float.MaxValue;
            _t0 = Time.time;
        }

        private Character Current() { return _step < _plan.Count ? _plan[_step].Target : null; }

        private Vector3 Home()
        {
            if (_pin != null && _pin.Bone != null) return _pin.Bone.position + Vector3.up * 0.1f;
            return _owner != null ? _owner.GetEyePoint() : transform.position;
        }

        private Vector3 Target()
        {
            if (_phase == 2) return Home();
            if (_plan.Count == 0) return _miss;
            Character c = Current();
            Vector3 aim = c != null ? Body.Center(c) : transform.position;
            if (_phase == 1) return aim + _reOffset; // set at the blow (it follows the prey, not the bird's own swing)
            return c != null ? aim + Approach(aim) : aim;
        }

        // The raven's own manner on the way in (its character): Hugin, a hunter, keeps high and swings out to his side,
        // then comes down onto the prey from above, steeply over the last metres; Munin, a brawler, goes straight in.
        // Gone by StoopNear m off the prey: the blow itself is exact.
        internal static float StoopNear = 2.5f, StoopSpan = 4f;
        private Vector3 Approach(Vector3 aim)
        {
            FlightCharacter ch = _motor.Char();
            if (ch.StoopUp <= 0.01f && ch.ArcSide <= 0.01f) return Vector3.zero;
            Vector3 to = aim - transform.position;
            float far = Mathf.Clamp01((to.magnitude - StoopNear) / Mathf.Max(0.5f, StoopSpan));
            if (far <= 0f) return Vector3.zero;
            float k = far * far * (3f - 2f * far);
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
            Vector3 side = flat.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, flat.normalized) * (_munin ? -1f : 1f) : Vector3.zero;
            return Vector3.up * (ch.StoopUp * k) + side * (ch.ArcSide * k);
        }

        // from LateUpdate, after the game's animation, right before the wings are sampled: a body with speed chasing the target; the blow when it gets there, bounced back and up; home, it brakes
        // to a hover over the shoulder, turns your way, and the game's own landing sets it down
        private void Step()
        {
            float dt = Time.deltaTime;
            if (_owner == null || _owner.IsDead() || Time.time - _born > 90f) { Land(); return; }
            if (_phase == 0 && _plan.Count == 0 && _missDiveUntil <= 0f && Time.time >= _missDiveAt)
            {
                _missDiveUntil = Time.time + 2f;
                _motor.SetPlan("пікірування");
            }
            else if (_phase == 0 && _plan.Count == 0 && _missDiveUntil > 0f && Time.time >= _missDiveUntil && _motor.Plan == "пікірування")
                _motor.SetPlan("удар");
            if (_phase == 0 && _plan.Count > 0 && (Current() == null || Current().IsDead())) { Next(); return; }
            Vector3 to = Target();
            float near = Vector3.Distance(transform.position, to);
            if (_phase == 2)
            {
                // home: the motor's way home, the same for every flight (waves on the long way, then the landing)
                Vector3 carry = _owner.GetVelocity();
                if (_motor.Home(to, _owner, _body.HomeSpeed(Strike.ReturnSpeed, carry, Time.time - _t0), dt, true, _born, false, Vector3.zero) || _motor.Stuck)
                {
                    if (ShoulderLife.LogLandings && ReferenceEquals(_owner, Player.m_localPlayer) && Plugin.Instance != null) Plugin.Instance.Log("Landing (strike): facing " + Mathf.RoundToInt(_body.YawOff(_owner.transform.forward)) + "° off the owner's way, " + Mathf.RoundToInt(Vector3.Distance(transform.position, to) * 100f) + " cm from the seat, speed " + _body.Speed.ToString("0.0"));
                    Land();
                }
                return;
            }
            if (_phase == 1)
            {
                if (Current() == null || Current().IsDead()) { Next(); return; } // it fell before the next dive
                float up = 6f * Mathf.Clamp(_motor.Char().TurnCap / 175f, 0.75f, 1.3f); // Munin back up quicker
                float left = _motor.Steer(to, up, true, 2f, dt, Vector3.zero, 1f, true);
                // there, or out of time (Hugin climbs higher: he gets the longer while)
                if (left < 0.5f || Time.time - _t0 > Mathf.Max(0.6f, Strike.SeriesGap * 2.5f, _reOffset.magnitude / up + 0.4f)) Begin(_step, 0);
            }
            else
            {
                // at prey: fast, and the last metres a little slower so it can turn onto it; nobody there: to the
                // spot, then home. Close and getting farther: it went past, so it is there. It keeps clear of the
                // ground and trees on the way, not in the last metres of the dive (the prey is on the ground)
                float speed = Strike.Speed * 1.1f;
                if (_plan.Count > 0 && near < 4f) speed = Mathf.Lerp(6f, speed, near / 4f);
                bool missedStrike = _plan.Count == 0;
                bool diveComplete = missedStrike && _missDiveUntil > 0f && Time.time >= _missDiveUntil;
                if (diveComplete && !_missTurnoutSet)
                {
                    Vector3 forward = Vector3.ProjectOnPlane(_body.Vel, Vector3.up);
                    if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(_miss - transform.position, Vector3.up);
                    if (forward.sqrMagnitude > 0.01f)
                    {
                        // on and a little up out of the dive, never lower than over the ground or the water there
                        _miss = transform.position + forward.normalized * 4f + Vector3.up * 0.5f;
                        _miss.y = Mathf.Max(_miss.y, Strike.Floor(_miss) + Strike.MissClear);
                        _lastNear = float.MaxValue;
                    }
                    _missTurnoutSet = true;
                    to = Target();
                    near = Vector3.Distance(transform.position, to);
                }
                float sharp = missedStrike ? 0.3f : near < 6f ? _motor.Char().StrikeSharp : 0.3f; // the last dive: Munin snaps onto it, Hugin swoops
                float left = _motor.Steer(to + _body.Wave(near, _born), speed, false, sharp, dt, Vector3.zero, Mathf.Clamp01((near - 3f) / 4f), true);
                bool passed = left < 2f && left > _lastNear + 0.05f;
                _lastNear = Mathf.Min(_lastNear, left);
                if (missedStrike)
                {
                    if (diveComplete && (left < 1.5f || passed || Time.time - _t0 > 15f)) Begin(_plan.Count, 2);
                }
                else if (left < Contact() || passed) Impact();
                else if (Time.time - _t0 > ReachSeconds) Skip(); // never got there (ran off, out of its way): no blow from afar
            }
            if (this == null) return;
            _motor.Finish(dt, 0f);
        }

        internal static float ReachSeconds = 12f;
        internal static bool LogPecks = false; // a line per blow in the log: who, when, how far (to look into the series in a test)
        private float _lastPeck;

        // how near the prey's middle counts as on it: a big one is struck on its hide (the motor may not let the bird
        // into a body that is not on the creatures' layers, a modded beast's)
        private float Contact() { Character c = Current(); return 0.8f + (c != null ? Mathf.Min(Body.Radius(c) * 0.7f, 2f) : 0f); } // (a dragon's radius would count a blow from afar)

        private void Impact()
        {
            Character c = Current();
            // the next dive's start, from the way it came in (before the bounce turns it round)
            FlightCharacter ch = _motor.Char();
            Vector3 cameIn = Vector3.ProjectOnPlane(_body.Vel, Vector3.up);
            _reOffset = Vector3.up * ch.ReUp - (cameIn.sqrMagnitude > 0.01f ? cameIn.normalized : Vector3.zero) * ch.ReBack;
            Vector3 at = c != null ? Body.Center(c) : transform.position;
            Peck("blow", c, at);
            Raven template = Shoulder.Template(_munin);
            if (template != null && template.m_despawnEffect != null) RavenFx.Own(template.m_despawnEffect, at, Quaternion.identity, null, 0.35f, -1); // a puff of feathers
            RavenFx.Play("sfx_raven_kaw", at, Quaternion.identity);
            if (_life != null) _life.Flap(0.35f);
            if (_onImpact != null) { try { _onImpact(_step, _from); } catch (Exception ex) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike blow failed: " + ex.Message); } }
            // after the blow (it may have killed): the same one again — back and up for the next dive; another one —
            // on through, the blow takes some of its speed and lifts it a little, and the motor turns it to the next
            Character nx = NextTarget();
            if (nx == null || nx == c) _body.Bounce(ch.BounceBack, ch.BounceUp); // Hugin bounces up and off, Munin stays on it
            else _body.Kick(_body.Vel * 0.75f + Vector3.up * 1.5f);
            Next();
        }

        private void Skip()
        {
            Peck("skipped, not reached", Current(), transform.position);
            Next();
        }

        private void Peck(string what, Character c, Vector3 at)
        {
            if (!LogPecks || Plugin.Instance == null || !ReferenceEquals(_owner, Player.m_localPlayer)) return;
            float since = _lastPeck > 0f ? Time.time - _lastPeck : Time.time - _born;
            _lastPeck = Time.time;
            Plugin.Instance.Log("Strike " + (_munin ? "Munin" : "Hugin") + " peck " + (_step + 1) + "/" + _plan.Count + " " + what + ": "
                + (c != null ? Utils.GetPrefabName(c.gameObject) : "-") + (_step < _plan.Count && _plan[_step].Crit ? " crit" : "")
                + ", " + since.ToString("0.00") + " s after the last, " + Vector3.Distance(transform.position, at).ToString("0.0") + " m off, speed " + _body.Speed.ToString("0.0"));
        }

        // the next living one in the plan (dead ones are passed over), or null
        private int NextIndex()
        {
            int next = _step + 1;
            while (next < _plan.Count && (_plan[next].Target == null || _plan[next].Target.IsDead())) next++;
            return next;
        }

        private Character NextTarget() { int n = NextIndex(); return n < _plan.Count ? _plan[n].Target : null; }

        // the same target again (a series): up and dive; another target: straight on; none left: home
        private void Next()
        {
            int next = NextIndex();
            if (next >= _plan.Count) { _step = _plan.Count; Begin(_plan.Count, 2); return; }
            bool same = _plan[next].Target == Current() && Current() != null && !Current().IsDead();
            _step = next;
            Begin(next, same ? 1 : 0);
        }

        private void Land()
        {
            if (ReferenceEquals(_owner, Player.m_localPlayer)) RavenFlight.Note(_munin, -1f, 0f, false);
            Flying[_munin ? 1 : 0].Remove(_owner);
            if (_onHome != null) { Action home = _onHome; _onHome = null; try { home(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike report failed: " + e.Message); } }
            AnimDirector.Release(gameObject, this); // it sits (pose flags off); the pin takes it for the landing
            Feathers.EndTrail(_trail); _trail = null;
            if (_clone) { Destroy(gameObject); return; }
            if (_pin != null)
            {
                Vector3 from = transform.position;
                if (_hook != null) { _hook.Back(); _hook = null; }
                _pin.enabled = true;
                _pin.ComeBack(from, transform.rotation); // the game's own landing, onto the shoulder (from far: flown in again)
            }
            Destroy(this);
        }

        private void LateUpdate()
        {
            Step();
            if (this != null && _life != null && (_pin == null || !_pin.enabled)) _life.Apply();
        }

        private void OnDestroy()
        {
            StrikeFlight f;
            if (_owner != null && Flying[_munin ? 1 : 0].TryGetValue(_owner, out f) && f == this) Flying[_munin ? 1 : 0].Remove(_owner);
            if (_hook != null) { _hook.Back(); _hook = null; }
            if (_pin != null && !_clone && !_yielded) _pin.enabled = true; // yielded: the leaving holds the bird (the pin goes)
            Feathers.EndTrail(_trail); _trail = null;
            // gone before it was home (the bird itself destroyed: the shoulder rebuilt, a relog): the flight still counts
            if (_onHome != null) { Action home = _onHome; _onHome = null; try { home(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Strike report failed: " + e.Message); } }
            if (ReferenceEquals(_owner, Player.m_localPlayer)) RavenFlight.Note(_munin, -1f, 0f, false);
            if (!_yielded) AnimDirector.Release(gameObject, this);
        }
    }

    // ---------------------------------------------------------------- patches

    // the last word on a raven's blow: cut back to its own size, whatever else was added on the way
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class StrikeCutPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(HitData hit) { try { Strike.Cut(hit); } catch { } }
    }

    [HarmonyPatch(typeof(Character), "OnDeath")]
    internal static class StrikeBossDeathPatch
    {
        private static void Prefix(Character __instance) { try { Strike.BossDied(__instance); } catch { } }
    }
}
#endif
