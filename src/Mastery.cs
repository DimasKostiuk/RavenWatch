#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Майстерність»: the strike's training goes on past 20 to 30, and at training 10, 20, 25 and 30 the raven learns
    // one of four techniques, picked for good (it stays with this raven). Each milestone has one technique for each way
    // of fighting — archer, mage, tank, close fighter — so ravens of different players strike differently. The pick is
    // kept on the raven item; the effects run in the striker's game (Strike, Marks) and, for the ones that guard you,
    // in your own game (the patches below).
    internal static class Mastery
    {
        internal enum Style { Archer, Mage, Tank, Melee }
        internal static readonly int[] Milestones = { 10, 20, 25, 30 };

        internal sealed class Tech
        {
            public string Id, Name; public bool Munin; public int At; public Style Way;
            public Func<string> Text; // its words with the numbers in
        }

        // ---------------------------------------------------------------- the numbers
        // Hugin
        internal static float MarkPct = 10f, MarkSeconds = 8f;                 // «Мітка мисливця»
        internal static float SparkShare = 0.3f;                               // «Іскра в дзьобі»
        internal static float BlindMul = 1.6f;                                 // «Глибока сліпота»
        internal static float DoublePts = 10f;                                 // «Подвійне око»
        internal static float SkyMul = 1.5f;                                   // «Каменем з неба»
        internal static float EitrPct = 8f;                                    // «Ейтир у пір'ї»
        internal static float RiposteGap = 30f;                                // «Відплата»
        internal static float RicochetReach = 8f;                              // «Рикошет»
        internal static float SilentBack = 0.5f;                               // «Тиха смерть»
        internal static float ResoPct = 15f, ResoSeconds = 8f;                 // «Резонанс»
        internal static float FeatherShare = 0.3f, FeatherSeconds = 10f, FeatherCap = 0.5f; // «Пір'яний щит» (cap: of your health)
        internal static float EdgeHp = 0.4f, EdgePct = 25f;                    // «Ціль на межі»
        internal static float OdinGap = 120f, OdinBlindBoss = 3f, OdinMarkPct = 15f, OdinMarkSeconds = 15f; // «Око Одіна»
        internal static float ThunderShare = 0.5f, ThunderRadius = 4f;         // «Громовий дзьоб»
        internal static float InterceptGap = 60f, InterceptOver = 0.3f, InterceptCut = 0.5f; // «Хугін перехоплює»
        internal static float PaybackMul = 2.5f, PaybackCrit = 10f;            // «Розплата»
        // Munin
        internal static float CrippleSlow = 0.5f;                              // «Рвані крила»
        internal static float RoarRadius = 4f;                                 // «Рев»
        internal static int ExtraTargets = 1;                                  // «Ще одна жертва»
        internal static float PreyPct = 10f;                                   // «Здобич»
        internal static float BloodEitr = 0.1f;                                // «Кров в ейтир»
        internal static float WeakCut = 0.15f;                                 // «Рвана броня»
        internal static float TrailRadius = 6f;                                // «Кривавий слід»
        internal static float CarrionBack = 0.3f;                              // «Падальник»
        internal static float RuneShare = 0.5f, RuneRadius = 4f;               // «Кривава руна»
        internal static float ThirstShare = 0.15f;                             // «Спрага»
        internal static float MercyPct = 20f, MercyHp = 0.4f;                  // «Без жалю»
        internal static int PackTargets = 6; internal static float PackReach = 12f; // «Зграя»
        internal static float WhirlShare = 0.5f, WhirlRadius = 3f;             // «Кривавий вир»
        internal static float IronCut = 0.15f;                                 // «Залізна кров»
        internal static float SlaughterRadius = 8f;                            // «Бойня»

        private static string R(float v) { return Mathf.RoundToInt(v).ToString(); }

        internal static readonly Tech[] All =
        {
            // ---- Hugin, the hunter
            new Tech { Id = "h_mark", Munin = false, At = 10, Way = Style.Archer, Name = Lang.K("Мітка мисливця"), Text = () => Lang.F("Ціль, яку вразив Хугін, {0} с отримує на {1}% більше шкоди від твоїх ударів — і стріл, і заклять, і клинка.", R(MarkSeconds), R(MarkPct)) },
            new Tech { Id = "h_spark", Munin = false, At = 10, Way = Style.Mage, Name = Lang.K("Іскра в дзьобі"), Text = () => Lang.F("{0}% удару Хугіна стає блискавицею: її не спиняє броня проти колючого, і вона б'є тих, кого дзьоб не бере.", R(SparkShare * 100f)) },
            new Tech { Id = "h_blind", Munin = false, At = 10, Way = Style.Tank, Name = Lang.K("Глибока сліпота"), Text = () => Lang.F("Осліплення триває {0} с замість {1} (бос — {2} с замість {3}): довше, ніж ворог тебе не бачить і не б'є.", R(Marks.BlindSeconds * BlindMul), R(Marks.BlindSeconds), (Marks.BlindBossSeconds * BlindMul).ToString("0.#"), Marks.BlindBossSeconds.ToString("0.#")) },
            new Tech { Id = "h_double", Munin = false, At = 10, Way = Style.Melee, Name = Lang.K("Подвійне око"), Text = () => Lang.F("Шанс подвійного удару +{0}%.", R(DoublePts)) },

            new Tech { Id = "h_sky", Munin = false, At = 20, Way = Style.Archer, Name = Lang.K("Каменем з неба"), Text = () => Lang.F("Перший удар по цілі, що не бачила ворона, — ×{0} і хитає її, збиваючи з ритму.", SkyMul.ToString("0.#")) },
            new Tech { Id = "h_eitr", Munin = false, At = 20, Way = Style.Mage, Name = Lang.K("Ейтир у пір'ї"), Text = () => Lang.F("Виліт, що когось вразив, повертає тобі {0}% ейтиру.", R(EitrPct)) },
            new Tech { Id = "h_riposte", Munin = false, At = 20, Way = Style.Tank, Name = Lang.K("Відплата"), Text = () => Lang.F("Коли ти відбиваєш удар у останню мить (парирування), Хугін сам зривається на нападника — без сили й без паузи дару. Раз на {0} с.", R(RiposteGap)) },
            new Tech { Id = "h_ricochet", Munin = false, At = 20, Way = Style.Melee, Name = Lang.K("Рикошет"), Text = () => Lang.F("Коли удар добиває ціль, Хугін одразу летить на наступну поруч (до {0} м від неї).", R(RicochetReach)) },

            new Tech { Id = "h_silent", Munin = false, At = 25, Way = Style.Archer, Name = Lang.K("Тиха смерть"), Text = () => Lang.F("Убив ціль, що не бачила його, — повертається {0}% сили й {0}% паузи.", R(SilentBack * 100f)) },
            new Tech { Id = "h_reso", Munin = false, At = 25, Way = Style.Mage, Name = Lang.K("Резонанс"), Text = () => Lang.F("Ціль, яку вразив Хугін, {0} с отримує на {1}% більше стихійної шкоди від тебе: вогню, морозу, блискавки, отрути, духу.", R(ResoSeconds), R(ResoPct)) },
            new Tech { Id = "h_feather", Munin = false, At = 25, Way = Style.Tank, Name = Lang.K("Пір'яний щит"), Text = () => Lang.F("Кожне влучання Хугіна дає тобі щит на {0}% від удару на {1} с (разом не більше {2}% твого здоров'я): він бере на себе шкоду першим.", R(FeatherShare * 100f), R(FeatherSeconds), R(FeatherCap * 100f)) },
            new Tech { Id = "h_edge", Munin = false, At = 25, Way = Style.Melee, Name = Lang.K("Ціль на межі"), Text = () => Lang.F("+{0}% удару по ворогах, у яких менше {1}% здоров'я.", R(EdgePct), R(EdgeHp * 100f)) },

            new Tech { Id = "h_odin", Munin = false, At = 30, Way = Style.Archer, Name = Lang.K("Око Одіна"), Text = () => Lang.F("Раз на {0} хв перший удар — неодмінно крит, сліпить навіть боса на {1} с і мітить ціль: {2} с вона отримує на {3}% більше шкоди від тебе.", R(OdinGap / 60f), R(OdinBlindBoss), R(OdinMarkSeconds), R(OdinMarkPct)) },
            new Tech { Id = "h_thunder", Munin = false, At = 30, Way = Style.Mage, Name = Lang.K("Громовий дзьоб"), Text = () => Lang.F("Кожна вражена ціль спалахує блискавицею: {0}% удару всім ворогам за {1} м від неї.", R(ThunderShare * 100f), R(ThunderRadius)) },
            new Tech { Id = "h_intercept", Munin = false, At = 30, Way = Style.Tank, Name = Lang.K("Хугін перехоплює"), Text = () => Lang.F("Поки Хугін на плечі: удар, що зняв би понад {0}% твого здоров'я, слабшає вдвічі. Раз на {1} с.", R(InterceptOver * 100f), R(InterceptGap)) },
            new Tech { Id = "h_payback", Munin = false, At = 30, Way = Style.Melee, Name = Lang.K("Розплата"), Text = () => Lang.F("Крит б'є ×{0} замість ×2, а шанс криту +{1}%.", PaybackMul.ToString("0.#"), R(PaybackCrit)) },

            // ---- Munin, the brawler
            new Tech { Id = "m_cripple", Munin = true, At = 10, Way = Style.Archer, Name = Lang.K("Рвані крила"), Text = () => Lang.F("Рана сповільнює на {0}% замість {1}%: ворог не дожене тебе, поки ти стріляєш.", R(CrippleSlow * 100f), R(Marks.Slow * 100f)) },
            new Tech { Id = "m_hot", Munin = true, At = 10, Way = Style.Mage, Name = Lang.K("Гаряча кров"), Text = () => Lang.T("Кровотеча стає горінням: шкода вогнем, і ворог займається.") },
            new Tech { Id = "m_roar", Munin = true, At = 10, Way = Style.Tank, Name = Lang.K("Рев"), Text = () => Lang.F("Перший удар хитає всіх ворогів за {0} м від цілі.", R(RoarRadius)) },
            new Tech { Id = "m_extra", Munin = true, At = 10, Way = Style.Melee, Name = Lang.K("Ще одна жертва"), Text = () => Lang.F("Мунін рве на {0} ворога більше за раз.", ExtraTargets) },

            new Tech { Id = "m_prey", Munin = true, At = 20, Way = Style.Archer, Name = Lang.K("Здобич"), Text = () => Lang.F("Поки ціль кровить від Муніна, вона отримує на {0}% більше шкоди від тебе.", R(PreyPct)) },
            new Tech { Id = "m_bloodeitr", Munin = true, At = 20, Way = Style.Mage, Name = Lang.K("Кров в ейтир"), Text = () => Lang.F("{0}% кровотечі вертається тобі ейтиром.", R(BloodEitr * 100f)) },
            new Tech { Id = "m_armor", Munin = true, At = 20, Way = Style.Tank, Name = Lang.K("Рвана броня"), Text = () => Lang.F("Поранений Муніном ворог б'є на {0}% слабше, поки рана свіжа (і тебе, і друзів).", R(WeakCut * 100f)) },
            new Tech { Id = "m_trail", Munin = true, At = 20, Way = Style.Melee, Name = Lang.K("Кривавий слід"), Text = () => Lang.F("Ворог гине з раною — рана переходить на найближчого сусіда (до {0} м).", R(TrailRadius)) },

            new Tech { Id = "m_carrion", Munin = true, At = 25, Way = Style.Archer, Name = Lang.K("Падальник"), Text = () => Lang.F("Добив кровотечею — повертається {0}% сили удару.", R(CarrionBack * 100f)) },
            new Tech { Id = "m_rune", Munin = true, At = 25, Way = Style.Mage, Name = Lang.K("Кривава руна"), Text = () => Lang.F("Поранений ворог, гинучи, вибухає вогнем: {0}% удару всім за {1} м.", R(RuneShare * 100f), R(RuneRadius)) },
            new Tech { Id = "m_thirst", Munin = true, At = 25, Way = Style.Tank, Name = Lang.K("Спрага"), Text = () => Lang.F("Кровотеча лікує тебе на {0}% від завданого.", R(ThirstShare * 100f)) },
            new Tech { Id = "m_mercy", Munin = true, At = 25, Way = Style.Melee, Name = Lang.K("Без жалю"), Text = () => Lang.F("+{0}% удару по поранених і по тих, у кого менше {1}% здоров'я.", R(MercyPct), R(MercyHp * 100f)) },

            new Tech { Id = "m_pack", Munin = true, At = 30, Way = Style.Archer, Name = Lang.K("Зграя"), Text = () => Lang.F("Мунін рве до {0} ворогів за раз, і шукає їх далі — до {1} м від першого.", PackTargets, R(PackReach)) },
            new Tech { Id = "m_whirl", Munin = true, At = 30, Way = Style.Mage, Name = Lang.K("Кривавий вир"), Text = () => Lang.F("Рана палить і довкола: кожна крапля — ще {0}% усім ворогам за {1} м від пораненого.", R(WhirlShare * 100f), R(WhirlRadius)) },
            new Tech { Id = "m_iron", Munin = true, At = 30, Way = Style.Tank, Name = Lang.K("Залізна кров"), Text = () => Lang.F("Поки на будь-кому кровить рана Муніна, ти отримуєш на {0}% менше шкоди.", R(IronCut * 100f)) },
            new Tech { Id = "m_slaughter", Munin = true, At = 30, Way = Style.Melee, Name = Lang.K("Бойня"), Text = () => Lang.F("Серія Муніна б'є ще й усіх поранених ним ворогів за {0} м від першої цілі.", R(SlaughterRadius)) },
        };

        internal static Tech Find(string id) { foreach (Tech t in All) if (t.Id == id) return t; return null; }

        internal static string WayName(Style s)
        {
            switch (s)
            {
                case Style.Archer: return Lang.T("лучник");
                case Style.Mage: return Lang.T("маг");
                case Style.Tank: return Lang.T("танк");
            }
            return Lang.T("ближній бій");
        }

        // ---------------------------------------------------------------- the picks, on the raven

        private const string Key = "RavenWatch_mastery"; // "h_mark,h_eitr,..."

        internal static List<string> Picks(ItemDrop.ItemData item)
        {
            List<string> l = new List<string>();
            string raw;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(Key, out raw) || string.IsNullOrEmpty(raw)) return l;
            foreach (string s in raw.Split(',')) if (s.Length > 0 && Find(s) != null && !l.Contains(s)) l.Add(s);
            return l;
        }

        internal static bool Has(ItemDrop.ItemData item, string id)
        {
            string raw;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(Key, out raw) && !string.IsNullOrEmpty(raw) && ("," + raw + ",").Contains("," + id + ",");
        }

        // the technique picked at this milestone, or null
        internal static Tech PickedAt(ItemDrop.ItemData item, int at)
        {
            foreach (string id in Picks(item)) { Tech t = Find(id); if (t != null && t.At == at) return t; }
            return null;
        }

        // milestones reached and still without a pick
        internal static List<int> Pending(ItemDrop.ItemData item)
        {
            List<int> l = new List<int>();
            if (item == null) return l;
            int level = Strike.Level(item);
            foreach (int at in Milestones) if (level >= at && PickedAt(item, at) == null) l.Add(at);
            return l;
        }

        // for good: only a reached milestone with no pick yet, and only the owner's own raven
        internal static string Pick(Player p, ItemDrop.ItemData item, string id)
        {
            Tech t = Find(id);
            bool munin;
            if (p == null || item == null || t == null || !RavenItem.IsRaven(item, out munin) || munin != t.Munin) return Lang.T("Немає такого прийому.");
            if (Trust.Stranger(item, p)) return Lang.T("Це не твій ворон.");
            if (Strike.Level(item) < t.At) return Lang.F("Відкриється на {0}-му рівні вишколу.", t.At);
            if (PickedAt(item, t.At) != null) return Lang.T("На цьому рівні прийом уже обрано.");
            List<string> l = Picks(item);
            l.Add(id);
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[Key] = string.Join(",", l.ToArray());
            _refreshAt = 0f;
            return null;
        }

        // (the console: raven mastery reset) the picks gone, to pick again
        internal static void Forget(ItemDrop.ItemData item)
        {
            if (item != null && item.m_customData != null) item.m_customData.Remove(Key);
            _refreshAt = 0f;
        }

        // ---------------------------------------------------------------- what the worn ravens know (refreshed twice a second)

        private static readonly HashSet<string> Mine = new HashSet<string>();
        private static float _refreshAt;
        private static bool _told;

        internal static bool On(string id) { return Mine.Contains(id); }

        // from the skills' Update
        internal static void Tick(Player p)
        {
            if (p == null || Time.time < _refreshAt) return;
            _refreshAt = Time.time + 0.5f;
            if (Hunt.Count > 0) // marks run out (and the marked die): not kept for ever
            {
                List<Character> over = null;
                foreach (KeyValuePair<Character, Mark> kv in Hunt)
                    if (kv.Key == null || (Time.time >= kv.Value.Until && Time.time >= kv.Value.ElemUntil)) { if (over == null) over = new List<Character>(); over.Add(kv.Key); }
                if (over != null) foreach (Character c in over) Hunt.Remove(c);
            }
            Mine.Clear();
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            foreach (ItemDrop.ItemData it in new[] { h, m })
            {
                if (it == null || !Trust.Active(it) || Trust.Stranger(it, p)) continue;
                foreach (string id in Picks(it)) Mine.Add(id);
            }
            // once a session: a raven past a milestone with no pick yet (an older raven after this update, too)
            if (!_told && ((h != null && Pending(h).Count > 0 && !Trust.Stranger(h, p)) || (m != null && Pending(m).Count > 0 && !Trust.Stranger(m, p))))
            {
                _told = true;
                p.Message(MessageHud.MessageType.TopLeft, Lang.T("Вишкіл: ворон може вивчити прийом — книга воронів, «Розвиток», печатка вишколу."));
            }
        }

        internal static void Reset() { Mine.Clear(); _refreshAt = 0f; _told = false; Hunt.Clear(); _shield = 0f; _riposteAt = 0f; _odinAt = 0f; _interceptAt = 0f; }

        // a milestone reached on the way back from a strike: say so
        internal static void Reached(Player p, bool munin, int before, int after)
        {
            if (p == null) return;
            foreach (int at in Milestones)
                if (before < at && after >= at)
                    RavenMsg.Center(p, Lang.F("{0}: вишкіл {1}-го рівня — новий прийом!\nКлікни печатку вишколу в книзі воронів («Розвиток»).", Plugin.RavenName(munin), at));
        }

        // ---------------------------------------------------------------- marks on targets (your own blows count more)

        private sealed class Mark { public float Until, Pct, ElemUntil, ElemPct; }
        private static readonly Dictionary<Character, Mark> Hunt = new Dictionary<Character, Mark>();

        internal static void MarkAll(Character c, float pct, float secs)
        {
            if (c == null || pct <= 0f) return;
            Mark k; if (!Hunt.TryGetValue(c, out k)) Hunt[c] = k = new Mark();
            k.Pct = Time.time < k.Until ? Mathf.Max(k.Pct, pct) : pct; // (the stronger of two marks while both last)
            k.Until = Mathf.Max(k.Until, Time.time + secs);
        }

        internal static void MarkElements(Character c, float pct, float secs)
        {
            if (c == null || pct <= 0f) return;
            Mark k; if (!Hunt.TryGetValue(c, out k)) Hunt[c] = k = new Mark();
            k.ElemUntil = Mathf.Max(k.ElemUntil, Time.time + secs); k.ElemPct = Mathf.Max(k.ElemPct, pct);
        }

        // one of your blows (not a raven's) on a creature: the marks and the bleeding prey count it up
        internal static void Outgoing(Player me, Character c, HitData hit)
        {
            float all = 0f;
            Mark k;
            if (Hunt.Count > 0 && Hunt.TryGetValue(c, out k))
            {
                if (Time.time < k.Until) all = k.Pct;
                if (Time.time < k.ElemUntil && k.ElemPct > 0f)
                {
                    float e = 1f + k.ElemPct / 100f;
                    hit.m_damage.m_fire *= e; hit.m_damage.m_frost *= e; hit.m_damage.m_lightning *= e; hit.m_damage.m_poison *= e; hit.m_damage.m_spirit *= e;
                }
                if (Time.time >= k.Until && Time.time >= k.ElemUntil) Hunt.Remove(c);
            }
            if (On("m_prey") && Marks.BleedingFrom(me, c)) all += PreyPct;
            if (all > 0f) hit.m_damage.Modify(1f + all / 100f);
        }

        // ---------------------------------------------------------------- what guards you (in your own game)

        private static float _shield, _shieldUntil, _interceptAt;

        internal static void AddShield(Player p, float v)
        {
            if (p == null || v <= 0f) return;
            if (Time.time > _shieldUntil) _shield = 0f;
            _shield = Mathf.Min(_shield + v, p.GetMaxHealth() * FeatherCap);
            _shieldUntil = Time.time + FeatherSeconds;
        }

        internal static void Incoming(Player p, HitData hit)
        {
            float total = hit.GetTotalDamage();
            if (total <= 0f) return;
            Character a = hit.GetAttacker();
            float mul = 1f;
            if (a != null && Marks.Weakened(a.transform)) mul *= 1f - WeakCut; // «Рвана броня»
            if (On("m_iron") && Marks.AnyBleedFrom(p)) mul *= 1f - IronCut;    // «Залізна кров»
            if (mul < 1f) { hit.m_damage.Modify(mul); total *= mul; }
            // «Хугін перехоплює»: a heavy blow halved while he sits on you
            if (On("h_intercept") && a != null && Time.time >= _interceptAt && total > p.GetMaxHealth() * InterceptOver && HuginHome(p)) // (a blow: not a fall or drowning)
            {
                _interceptAt = Time.time + InterceptGap;
                hit.m_damage.Modify(InterceptCut); total *= InterceptCut;
                BindFx.Nova(p, "shield", Body.Center(p), 1.6f, 0.5f);
                p.Message(MessageHud.MessageType.TopLeft, Lang.T("Хугін перехопив удар"));
            }
            // «Пір'яний щит» takes what it can first
            if (_shield > 0f && Time.time <= _shieldUntil)
            {
                float take = Mathf.Min(_shield, total);
                _shield -= take;
                hit.m_damage.Modify((total - take) / total);
            }
        }

        private static bool HuginHome(Player p)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            return h != null && Trust.Active(h) && !Trust.Away(h) && !Strike.Busy(p, false) && !Perches.On(h);
        }

        // ---------------------------------------------------------------- parry: «Відплата»

        private static float _riposteAt;

        internal static void Parried(Player p, Character attacker)
        {
            if (!On("h_riposte") || attacker == null || Time.time < _riposteAt) return;
            if (RavenSkills.FreeStrike(p, false, attacker)) _riposteAt = Time.time + RiposteGap;
        }

        // ---------------------------------------------------------------- «Око Одіна»: once in a while

        private static float _odinAt;
        internal static bool OdinReady() { return On("h_odin") && Time.time >= _odinAt; }
        internal static void OdinUsed() { _odinAt = Time.time + OdinGap; }

        // ---------------------------------------------------------------- area blows (the striker's game deals them)

        // a share of a blow to every creature near a point (not the one it came from): marked as a raven's, so the
        // boss cap of the flight holds and it is cut like any raven blow
        internal static void Splash(Player p, Vector3 at, float radius, float v, bool fire, Character skip, Dictionary<Character, float> capLeft, bool munin)
        {
            if (p == null || v <= 0.5f) return;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == skip || !Strike.Prey(p, c) || Vector3.Distance(Body.Center(c), at) > radius + Body.Radius(c)) continue;
                float d = v;
                if (c.IsBoss() && capLeft != null)
                {
                    float left;
                    if (!capLeft.TryGetValue(c, out left)) left = c.GetMaxHealth() * (munin ? Strike.BossCapMunin : Strike.BossCapHugin);
                    d = Mathf.Min(d, left);
                    capLeft[c] = left - d;
                }
                if (d <= 0.5f) continue;
                HitData hit = new HitData();
                if (fire) hit.m_damage.m_fire = d; else hit.m_damage.m_lightning = d;
                hit.m_point = Body.Center(c);
                Vector3 dir = Body.Center(c) - at;
                hit.m_dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up;
                hit.m_pushForce = 2f;
                hit.m_staggerMultiplier = 0.2f;
                hit.m_backstabBonus = 1f;
                hit.m_blockable = false;
                hit.m_dodgeable = false;
                hit.m_ranged = true;
                hit.m_skill = Skills.SkillType.None;
                hit.m_skillRaiseAmount = Strike.Marker - d;
                hit.SetAttacker(p);
                c.Damage(hit);
            }
        }

        // every creature near a point staggers (the roar)
        internal static void StaggerAround(Player p, Vector3 at, float radius)
        {
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Strike.Prey(p, c) || Vector3.Distance(c.transform.position, at) > radius + Body.Radius(c)) continue;
                Vector3 d = c.transform.position - at; d.y = 0f;
                c.Stagger(d.sqrMagnitude > 0.001f ? d.normalized : -c.transform.forward);
            }
        }

        // the nearest prey to a point, not one of these, within a radius
        internal static Character Nearest(Player p, Vector3 at, float radius, ICollection<Character> not)
        {
            Character best = null; float bd = radius;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Strike.Prey(p, c) || (not != null && not.Contains(c))) continue;
                float d = Vector3.Distance(c.transform.position, at);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }
    }

    // ---------------------------------------------------------------- the patches

    // your blows (any but a raven's) on a creature marked by your raven
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class MasteryOutgoingPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            Player me = Player.m_localPlayer;
            if (me == null || hit == null || ReferenceEquals(__instance, me) || hit.m_attacker != me.GetZDOID() || hit.m_skillRaiseAmount <= Strike.Marker) return;
            if (!Marks.RealBlow(hit)) return; // a burn or poison ticking: the blow that set it was already counted
            Mastery.Outgoing(me, __instance, hit);
        } catch (Exception e) { PatchGuard.Fail("MasteryOutgoingPatch", e); } }
    }

    // blows on you: the guarding techniques (before Path of Valheiman's rune shield, which is late)
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class MasteryIncomingPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (hit == null || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
            Mastery.Incoming((Player)__instance, hit);
        } catch (Exception e) { PatchGuard.Fail("MasteryIncomingPatch", e); } }
    }

    // a parry: the game's own test (a timed block within a quarter second); the attacker is the blow's
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class MasteryParryPatch
    {
        private static readonly System.Reflection.FieldInfo BlockTimer = AccessTools.Field(typeof(Humanoid), "m_blockTimer");
        private static readonly System.Reflection.MethodInfo Blocker = AccessTools.Method(typeof(Humanoid), "GetCurrentBlocker");

        private static void Prefix(Humanoid __instance, ref bool __state)
        {
            __state = false;
            try {
                if (!Mastery.On("h_riposte") || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
                ItemDrop.ItemData b = (ItemDrop.ItemData)Blocker.Invoke(__instance, null);
                float t = (float)BlockTimer.GetValue(__instance);
                __state = b != null && b.m_shared.m_timedBlockBonus > 1f && t != -1f && t < 0.25f;
            } catch (Exception e) { PatchGuard.Fail("MasteryParryPatch", e); }
        }

        private static void Postfix(Humanoid __instance, Character attacker, bool __result, bool __state)
        { try {
            if (__state && __result) Mastery.Parried((Player)__instance, attacker);
        } catch (Exception e) { PatchGuard.Fail("MasteryParryPatch", e); } }
    }
}
#endif
