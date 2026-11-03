#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // An errand's modifiers: a rule over the whole errand, not a step. Set in the template:
    //   mods: nodeath            always
    //   maymods: nodeath 30      with this chance (%) when it is given
    // and by themselves on any errand (not Odin's trial): one with RandomOne %, two with RandomTwo %; «Пожертва» only on a
    // golden errand, rarely. The run keeps the ones it got (ErrandRun.Mods) and the broken ones (its mark "mlost").
    //   «Без смерті»   — a death fails the whole errand;
    //   «Без подряпин» — health under 30% once: that modifier's reward is gone, the errand goes on;
    //   «Поспіх»       — not done within a game day: the same;
    //   «Пішки»        — through a portal once: the same; only on an errand that sends you far (Far), and the farther,
    //                    the more it gives (FarStep);
    //   «Без відступу» — run from a foe that was hitting you (Ev.Flee): the same;
    //   «Тихо»         — a blow given or taken at night or in a storm: the same; never on an errand that wants the night;
    //   «Пожертва»     — up to 300 coins laid at the nest (mark "stake"): done, Odin gives three times back; else lost.
    // The reward of these is a secret until the errand is done: notches, or (RuneStars) a spark of Odin by each one's
    // chance. And the two of Odin's own (rare, only when a rune can take a spark; never both): a spark FOR SURE, said
    // openly in gold —
    //   «Обітниця Одіна»    — a death or a portal fails the whole errand (so no «Без смерті», no «Пішки» with it);
    //   «Випробування іскри» — done within its time (TrialSeconds: the way there and back on foot, what each step asks,
    //                          a little slack) and without a death; late or dead: only the spark is gone.
    internal static class ErrandMods
    {
        internal sealed class Mod
        {
            public string Id = "", Name = "", Text = "";
            public int Bonus;        // its notches (or, by chance, a spark in their place)
            public float Spark = -1f; // its chance (%) of a spark in their place (-1: RuneStars.SparkChance); golden: as much more as RuneStars gives a golden one
            public bool Soft;        // broken: only its own reward is gone, the errand goes on (else the errand fails)
            public bool Death;       // a death breaks it
            public bool GoldenOnly;  // comes only on a golden errand
            public bool Coins;       // its reward is the stake back (Пожертва), never a spark
            public bool Sure;        // Odin's own: a spark for sure, rolled apart, shown openly
            public int On = -1;      // the deed (Ev) that breaks it, besides a death
            public string Why = "";  // what is said when that deed breaks it
        }

        internal static float RandomOne = 35f, RandomTwo = 10f, OfferingChance = 3f, HurryDays = 1f, ScratchAt = 0.3f;
        internal static float SureChance = 5f, SureGolden = 10f; // Odin's own (a sure spark), % of errands (golden ones)
        internal static int SureFallback = 3;                     // their notches if no rune could take the spark after all
        internal static float Far = 400f, FarStep = 1500f;        // m: «Пішки» only this far; +1 notch (and +5% spark) each FarStep more, up to +2
        internal static float TrialBase = 300f, TrialPace = 4f, TrialRoute = 1.35f, TrialSlack = 1.25f, TrialMin = 600f, TrialMax = 2700f; // s, m/s
        internal static float FleeGrace = 45f; // s after it is given a flight from a fight does not count
        internal static int OfferingMax = 300, OfferingMul = 3;
        internal const string LostKey = "mlost", StakeKey = "stake", FarKey = "far", LimitKey = "limit";

        internal static readonly Dictionary<string, Mod> All = new Dictionary<string, Mod>
        {
            { "nodeath", new Mod { Id = "nodeath", Name = Lang.K("Без смерті"), Text = Lang.K("помреш — Одін не зарахує, і доручення пропаде"), Bonus = 1, Death = true } },
            { "noscratch", new Mod { Id = "noscratch", Name = Lang.K("Без подряпин"), Text = Lang.K("здоров'я хоч раз нижче 30% чи смерть — нагорода за це пропаде"), Bonus = 1, Soft = true, Death = true } },
            { "hurry", new Mod { Id = "hurry", Name = Lang.K("Поспіх"), Text = Lang.K("не встигнеш за ігровий день — нагорода за це пропаде"), Bonus = 1, Soft = true } },
            { "noportal", new Mod { Id = "noportal", Name = Lang.K("Пішки"), Text = Lang.K("пройдеш крізь портал — нагорода за це пропаде; що далі шлях, то щедріша"), Bonus = 1, Soft = true, On = (int)Ev.Portal, Why = Lang.K("ти пройш{ов|ла} крізь портал") } },
            { "noflee", new Mod { Id = "noflee", Name = Lang.K("Без відступу"), Text = Lang.K("утечеш від ворога, що тебе бив, — нагорода за це пропаде"), Bonus = 1, Spark = 35f, Soft = true, On = (int)Ev.Flee, Why = Lang.K("ти втік{|ла} з бою") } },
            { "quiet", new Mod { Id = "quiet", Name = Lang.K("Тихо"), Text = Lang.K("вдариш чи дістанеш удару вночі або в бурю — нагорода за це пропаде"), Bonus = 1, Spark = 25f, Soft = true } },
            { "vow", new Mod { Id = "vow", Name = Lang.K("Обітниця Одіна"), Text = Lang.K("ні смерті, ні порталів до кінця: виконаєш — іскра Одіна напевно; порушиш — доручення пропаде"), Death = true, Sure = true, On = (int)Ev.Portal, Why = Lang.K("ти пройш{ов|ла} крізь портал") } },
            { "sparktest", new Mod { Id = "sparktest", Name = Lang.K("Випробування іскри"), Text = Lang.K("встигни до кінця часу й не помри: іскра Одіна напевно; ні — іскри не буде, доручення триває"), Soft = true, Death = true, Sure = true } },
            { "offering", new Mod { Id = "offering", Name = Lang.K("Пожертва"), Text = Lang.K("на самому початку, поки нічого не зроблено, поклади в гнізді до 300 монет: виконаєш — Одін поверне втричі, ні — вони пропадуть"), Soft = true, GoldenOnly = true, Coins = true } },
        };

        internal static bool Known(string id) { return All.ContainsKey(id); }

        // the ones this errand gets now: the template's, then by themselves; far: how far it sends you (m)
        internal static List<string> Roll(ErrandDef d, Player p, float far)
        {
            List<string> l = new List<string>();
            foreach (string id in d.Mods) if (Known(id) && !l.Contains(id)) l.Add(id);
            foreach (KeyValuePair<string, float> kv in d.MayMods)
                if (Known(kv.Key) && !l.Contains(kv.Key) && UnityEngine.Random.value * 100f < kv.Value) l.Add(kv.Key);
            if (d.Trial || FromStock(d)) return l;
            float roll = UnityEngine.Random.value * 100f;
            int want = roll < RandomTwo ? 2 : roll < RandomTwo + RandomOne ? 1 : 0;
            List<string> pool = new List<string>();
            foreach (Mod m in All.Values)
            {
                if (m.GoldenOnly || m.Sure || l.Contains(m.Id)) continue;
                if (m.Id == "noportal" && far < Far) continue; // (near: no portal to want)
                if (m.Id == "quiet" && WantsNight(d)) continue; // (a night hunt can't be quiet at night)
                if (m.Id == "noportal" && Carries(d)) continue; // (Odin's feather goes through no portal anyway, a guide is followed on foot)
                if (m.Id == "hurry" && Slow(d)) continue; // (taming, a day of peace: most of a day by themselves)
                pool.Add(m.Id);
            }
            for (int i = 0; i < want && pool.Count > 0 && Plain(l) < 2; i++) { int k = UnityEngine.Random.Range(0, pool.Count); l.Add(pool[k]); pool.RemoveAt(k); }
            if (d.Golden && !l.Contains("offering") && UnityEngine.Random.value * 100f < OfferingChance) l.Add("offering");
            if (!l.Contains("vow") && !l.Contains("sparktest") && UnityEngine.Random.value * 100f < (d.Golden ? SureGolden : SureChance) && RuneStars.CanSpark(p))
                l.Add(far >= Far && UnityEngine.Random.value < 0.5f ? "vow" : "sparktest"); // (a vow of no portals means something only far off)
            Tidy(l);
            return l;
        }

        // Odin's own hold some rules already: the same rule twice is gone
        internal static void Tidy(List<string> l)
        {
            if (l.Contains("vow")) { l.Remove("sparktest"); l.Remove("nodeath"); l.Remove("noportal"); } // (one of Odin's own at a time)
            if (l.Contains("sparktest")) { l.Remove("hurry"); l.Remove("nodeath"); }
        }

        // a step that may be done at once from what you keep (one of its ways only a thing to bring)
        private static bool FromStock(ErrandDef d) { foreach (string[] s in d.Steps) if (s.Length > 1 && s[0] == "any" && System.Array.IndexOf(s, "bring") > 0) return true; return false; }

        private static bool Carries(ErrandDef d) { foreach (string[] s in d.Steps) if (s.Length > 0 && (s[0] == "carry" || s[0] == "follow")) return true; return false; } // (on foot by itself)

        private static bool Slow(ErrandDef d)
        {
            foreach (string[] s in d.Steps)
            {
                if (s.Length > 0 && s[0] == "spare") return true; // (a day of peace from the next dawn)
                for (int k = 0; k + 1 < s.Length; k++) if (s[k] == "do" && string.Equals(s[k + 1], "Tame", System.StringComparison.OrdinalIgnoreCase)) return true; // (also as a way of «any»)
            }
            return false;
        }

        private static bool WantsNight(ErrandDef d)
        {
            foreach (string[] s in d.Steps) { if (s.Length > 0 && s[0] == "hold") return true; foreach (string w in s) if (w == "night") return true; } // (a watch is kept at night)
            return false;
        }

        private static int Plain(List<string> l) { int n = 0; Mod m; foreach (string id in l) if (All.TryGetValue(id, out m) && !m.GoldenOnly && !m.Sure) n++; return n; }

        // the errand keeps a spark for sure (Odin's own, not broken)
        internal static bool SureSpark(ErrandRun r) { Mod m; foreach (string id in r.Mods) if (All.TryGetValue(id, out m) && m.Sure && !Lost(r, id)) return true; return false; }

        // how far the errand sends you (kept when given)
        internal static float FarOf(ErrandRun r) { float f; return float.TryParse(r.Mark(FarKey), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : 0f; }
        private static int FarExtra(ErrandRun r) { return Mathf.Clamp(Mathf.FloorToInt((FarOf(r) - Far) / FarStep), 0, 2); }

        // a modifier's notches and spark chance on this run
        private static int NotchesOf(ErrandRun r, Mod m) { return m.Bonus + (m.Id == "noportal" ? FarExtra(r) : 0); }
        private static float SparkOf(ErrandRun r, Mod m, bool golden)
        {
            float c = m.Spark >= 0f ? m.Spark : RuneStars.SparkChance;
            if (m.Id == "noportal") c += 5f * FarExtra(r);
            return c + (golden ? RuneStars.SparkGolden - RuneStars.SparkChance : 0f);
        }

        // «Випробування іскри»: its time, from what the errand asks (when given): to each place of a step and back on
        // foot, every step's own work, a little slack
        // (not clamped above: Errands gives no trial that would need more than TrialMax — a vow, or none, in its place)
        internal static double TrialSeconds(ErrandRun r, Vector3 from)
        {
            float way = 0f, work = 0f;
            Vector3 at = from;
            for (int i = 0; i < r.Steps.Length; i++)
            {
                ErrandStep s = r.Steps[i];
                Vector3 to; float radius;
                bool placed = s.Where(r, i, out to, out radius);
                if (placed) { way += Vector3.Distance(Flat(at), Flat(to)); at = to; }
                work += WorkOf(s, placed, radius);
            }
            way += Vector3.Distance(Flat(at), Flat(from)); // (and back to hand it in)
            return Mathf.Max(TrialMin, (TrialBase + way * TrialRoute / TrialPace + work) * TrialSlack);
        }

        // what a step itself takes: a rest its seconds (till dawn: half a day at most); a deed with a wait of its own, that
        // wait; at a place, a look round its ring; elsewhere, the finding; and more for every one it wants (a champion's count is a mask, not a number)
        private static float WorkOf(ErrandStep s, bool placed, float radius)
        {
            if (s is RestStep) return s.Need <= 1 ? Power.GameDaySeconds() * 0.5f : s.Need + 60f;
            if (s.Wait > 0f) return 150f + s.Wait; // (a beast tamed, a voyage, a day of peace: their own time)
            float w = placed ? 120f + radius * 1.5f / TrialPace : 150f;
            if (!(s is ChampionStep)) w += 30f * Mathf.Min(10, Mathf.Max(0, s.Need - 1));
            return w;
        }
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        internal static float TrialLeft(ErrandRun r) { double lim; return double.TryParse(r.Mark(LimitKey), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lim) ? (float)(r.Given + lim - WorldTime.Now) : float.MaxValue; } // (no time kept: never late)

        // ---------------------------------------------------------------- the run's marks
        internal static bool Lost(ErrandRun r, string id) { return ("+" + r.Mark(LostKey) + "+").Contains("+" + id + "+"); }
        private static void Lose(Player p, ErrandRun r, string id, string why)
        {
            if (Lost(r, id)) return;
            string had = r.Mark(LostKey);
            r.SetMark(LostKey, had.Length > 0 ? had + "+" + id : id);
            Mod m;
            if (p != null && All.TryGetValue(id, out m)) RavenMsg.Center(p, Lang.F("«{0}» зламано: {1}. Доручення триває, але нагороди за це не буде.", Lang.T(m.Name), why));
        }

        internal static int Stake(ErrandRun r) { int n; return int.TryParse(r.Mark(StakeKey), out n) ? Mathf.Max(0, n) : 0; }
        internal static float HurryLeft(ErrandRun r) { return (float)(r.Given + HurryDays * Power.GameDaySeconds() - WorldTime.Now); }

        // a deed of the game against the modifiers: a hard one broken ends the errand (fails: why, said to the player),
        // a soft one only loses its reward (changed: the run wants saving)
        internal static bool Breaks(Player p, ErrandRun r, Ev type, out string fails)
        {
            fails = null;
            if (r.Mods.Count == 0) return false;
            Mod m;
            if (type != Ev.Death)
            {
                bool broke = false; // (through a portal, run from a fight: a soft one's reward gone, a hard one the errand)
                foreach (string id in r.Mods)
                {
                    if (!All.TryGetValue(id, out m) || m.On != (int)type || Lost(r, id)) continue;
                    if (type == Ev.Flee && WorldTime.Now - r.Given < FleeGrace) continue; // (a fight left before the errand came is not this errand's)
                    if (!m.Soft) { fails = Lang.T(m.Why); return false; }
                    Lose(p, r, id, Lang.T(m.Why)); broke = true;
                }
                return broke;
            }
            string why = Lang.T("ти загину{в|ла}");
            foreach (string id in r.Mods) if (All.TryGetValue(id, out m) && m.Death && !m.Soft) { fails = why; return false; }
            bool changed = false;
            foreach (string id in r.Mods) if (All.TryGetValue(id, out m) && m.Death && !Lost(r, id)) { Lose(p, r, id, why); changed = true; } // (a death is the worst scratch)
            return changed;
        }

        private static float _lastHp; // the health at the last tick — of this player on this errand (another: only noted)
        private static Player _hpOf;
        private static string _hpRun;

        // twice a second (Errands.Tick): health and time; true when a mark changed (the run is saved)
        internal static bool Tick(Player p, ErrandRun r)
        {
            if (p == null || r.Mods.Count == 0) return false;
            bool changed = false;
            float hp = p.GetHealth(), was = hp;
            if (ReferenceEquals(p, _hpOf) && _hpRun == r.Id) was = _lastHp;
            _lastHp = hp; _hpOf = p; _hpRun = r.Id;
            if (r.Mods.Contains("noscratch") && !Lost(r, "noscratch") && !p.IsDead() && p.GetMaxHealth() > 0f && hp < was - 0.5f && hp / p.GetMaxHealth() < ScratchAt) // (only a fall: not low health climbing back after food)
            { Lose(p, r, "noscratch", Lang.T("здоров'я впало нижче 30%")); changed = true; }
            if (r.Mods.Contains("hurry") && !Lost(r, "hurry") && HurryLeft(r) <= 0f)
            { Lose(p, r, "hurry", Lang.T("ігровий день минув")); changed = true; }
            if (r.Mods.Contains("sparktest") && !Lost(r, "sparktest") && TrialLeft(r) <= 0f)
            { Lose(p, r, "sparktest", Lang.T("час вийшов")); changed = true; }
            if (r.Mods.Contains("quiet") && !Lost(r, "quiet") && (Time.time - Senses.HitMonsterAt < 1f || Time.time - Senses.HurtByFoeAt < 1f) && Dark()) // (a fight: a monster hit or hitting you, not a deer hunted; the weather looked at only then)
            { Lose(p, r, "quiet", EnvMan.IsNight() ? Lang.T("ти би{вся|лася} вночі") : Lang.T("ти би{вся|лася} у бурю")); changed = true; }
            return changed;
        }

        // night, or a storm (thunder in the weather's name: plain rain is no storm)
        private static bool Dark()
        {
            if (EnvMan.IsNight()) return true;
            EnvSetup e = EnvMan.instance != null ? EnvMan.instance.GetCurrentEnvironment() : null;
            string n = e != null ? e.m_name ?? "" : "";
            return n.IndexOf("storm", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("thunder", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // the notches its modifiers would give (the ones not broken), before any spark
        internal static int Bonus(ErrandRun r)
        {
            int n = 0;
            Mod m;
            foreach (string id in r.Mods) if (All.TryGetValue(id, out m) && !m.Coins && !m.Sure && !Lost(r, id)) n += NotchesOf(r, m);
            return n;
        }

        // an errand done: each modifier kept may bring a spark of Odin (a star on a random rune, RuneStars) in place of
        // its notches; no spark (or no rune to take it) — its notches. «Пожертва»: the stake three times back.
        // Returns the notches (doubled on a golden errand).
        internal static int Pay(Player p, ErrandRun r, bool golden)
        {
            int n = 0;
            Mod m;
            foreach (string id in r.Mods)
            {
                if (!All.TryGetValue(id, out m) || Lost(r, id)) continue;
                if (m.Coins) { int stake = Stake(r); if (stake > 0) GiveCoins(p, stake * OfferingMul, Lang.F("Одін прийняв пожертву й повернув утричі: {0} монет.", stake * OfferingMul)); continue; }
                if (m.Sure) { if (RuneStars.Spark(p) == null) n += SureFallback * (golden ? 2 : 1); continue; } // (Odin's own: a spark, no dice)
                if (UnityEngine.Random.value * 100f < SparkOf(r, m, golden) && RuneStars.Spark(p) != null) continue;
                n += NotchesOf(r, m) * (golden ? 2 : 1);
            }
            return n;
        }

        private static void GiveCoins(Player p, int n, string said)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Coins") : null;
            if (p == null || go == null || n <= 0) return;
            Errands.Give(p, go, n);
            RavenMsg.Center(p, said);
            p.Message(MessageHud.MessageType.TopLeft, said);
        }

        // «Пожертва» is a stake, so only before anything of the errand is done (not laid at the hand-in, a sure ×3)
        internal static bool OfferOpen(ErrandRun r)
        {
            if (r == null || r.Def == null || r.Step != 0) return false;
            foreach (int n in r.Count) if (n > 0) return false;
            return true;
        }

        // «Пожертва»: coins laid at the nest (once, up to OfferingMax); null when done, else why not
        internal static string Offer(Player p, ErrandRun r, int coins)
        {
            if (p == null || r == null || !r.Mods.Contains("offering")) return Lang.T("Це доручення пожертви не просить.");
            if (Stake(r) > 0) return Lang.T("Пожертву вже покладено.");
            if (!OfferOpen(r)) return Lang.T("Пожертву кладуть на початку доручення, поки нічого не зроблено.");
            coins = Mathf.Clamp(coins, 1, OfferingMax);
            Inventory inv = p.GetInventory();
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Coins") : null;
            string name = go != null ? go.GetComponent<ItemDrop>().m_itemData.m_shared.m_name : "$item_coins";
            if (inv.CountItems(name) < coins) return Lang.F("Треба {0} монет у сумці.", coins);
            inv.RemoveItem(name, coins);
            r.SetMark(StakeKey, coins.ToString());
            return null;
        }

        // the lines for the book: «☠ Без смерті — помреш…» (a broken one says so; «Поспіх» its time; «Пожертва» its stake)
        internal static List<string> Lines(ErrandRun r)
        {
            List<string> l = new List<string>();
            Mod m;
            foreach (string id in r.Mods)
            {
                if (!All.TryGetValue(id, out m)) continue;
                string s = m.Sure ? "<color=#8a5f12><b>" + Lang.T(m.Name) + "</b> — " + Lang.T(m.Text) + "</color>" : "☠ " + Lang.T(m.Name) + " — " + Lang.T(m.Text); // (Odin's own in gold: its spark is no secret)
                if (Lost(r, id)) s = "<s>" + s + "</s> " + Lang.T("(зламано)");
                else if (id == "hurry") s += " " + Lang.F("(лишилось {0})", RavenAbilities.Clock(Mathf.Max(0f, HurryLeft(r))));
                else if (id == "sparktest") s += " " + Lang.F("(лишилось {0})", RavenAbilities.Clock(Mathf.Max(0f, TrialLeft(r))));
                else if (id == "offering") s += " " + (Stake(r) > 0 ? Lang.F("(покладено {0})", Stake(r)) : OfferOpen(r) ? Lang.T("(ще нічого)") : Lang.T("(не покладено)"));
                l.Add(s);
            }
            return l;
        }
    }
}
#endif
