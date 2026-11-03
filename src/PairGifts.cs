#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The paired gifts (the user, 2026-10-24): both ravens on the shoulders, each pays its own share of strength.
    //  «Подвійна ноша» (Load)     — they take part of the load and fly beside you: more carry weight; meanwhile none of
    //                              their gifts works, as when they are off the shoulder.
    //  «Розігнати хмари» (ClearSky) — they circle high over you: a clear sky for everyone near you, the mist parted.
    //  «Вихор» (Whirl)            — they whirl round you striking every foe near, no landing between blows; every second
    //                              costs both, and once one runs dry both come back and the pause begins.
    // Kept on Hugin's side of the gift lists (RavenAbility.Pair), between the wings at the top of the gifts circle.
    internal static class PairGifts
    {
        internal static void Reset() { Whirl.Reset(); Load.Reset(); ClearSky.Reset(); PairFx.Reset(); }

        // the gift's own gesture of yours, both arms (BodyClips: the others see it by your data); over whatever small
        // gesture was playing, never in Odin's wings or a swing
        internal static void Gesture(Player p, string clip)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer) || !RavenSkills.Animations || OdinWings.Flying() || p.InAttack()) return;
            BodyClips.Play(p, 2, clip, 0.2f, UnityEngine.Random.Range(0.95f, 1.05f));
        }

        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            Whirl.Tick(p);
            Load.Tick(p);
            ClearSky.Tick(p);
            PairFx.Tick();
        }

        // a gift of the ravens' that waits while both are busy with a paired one (null: it may go)
        internal static string Blocks(RavenAbility a)
        {
            if (a == null) return null;
            if (Load.On() && a.Id != "pload") return Lang.T("Ворони тягнуть ношу — кігті зайняті");
            if (Whirl.On() && a.Id != "pwhirl") return Lang.T("Ворони у вихорі");
            // the clear sky: a strike comes back up to it, but none of them leaves the sky for long (the same ones keep
            // the clear sky from starting: Why)
            if (ClearSky.Mine() && (a.Id == "dome" || a.Id == "scout" || a.Id == "hdecoy" || a.Id == "mtame")) return Lang.T("Ворони розганяють хмари");
            return null;
        }

        // why the two cannot go now (null: they can)
        internal static string Why(Player p, RavenAbility a)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            if (h == null || m == null) return Lang.T("потрібні обидва ворони на плечах");
            for (int i = 0; i < 2; i++)
            {
                bool mu = i == 1;
                ItemDrop.ItemData it = mu ? m : h;
                if (!Shoulder.Arrived(p, mu)) return Lang.F("{0} ще летить до тебе", Plugin.RavenName(mu));
                if (Trust.Away(it)) return Trust.AwayText(mu, it);
                if (!Trust.Active(it)) return Lang.F("{0} спить — спершу обряд", Plugin.RavenName(mu));
                if (Trust.Hostile(it)) return Lang.F("{0} не твій", Plugin.RavenName(mu));
                if (Strike.Busy(p, mu)) return Lang.F("{0} в атаці", Plugin.RavenName(mu));
                string q = Quirks.Blocks(p, mu, a.Id);
                if (q != null) return q;
            }
            if (RavenSkills.Scouting()) return Lang.T("Хугін на розвідці");
            if (Dome.Mine()) return Lang.T("Хугін тримає купол");
            if (Decoy.Mine()) return Lang.T("Хугін дражнить ворога");
            if (Tame.Mine()) return Lang.T("Мунін приборкує звіра");
            return null;
        }

        // which of the two is short of strength for this (null: both have it); the same reckoning as Spend, so a gift is
        // never paid by one and refused by the other
        internal static string Short(float cost)
        {
            bool h = !Power.Afford(false, cost), m = !Power.Afford(true, cost);
            if (!h && !m) return null;
            return h && m ? Lang.T("обом бракує сили") : h ? Lang.T("Хугіну бракує сили") : Lang.T("Муніну бракує сили");
        }

        // both pay (or neither)
        internal static bool Pay(Player p, float cost)
        {
            string no = Short(cost);
            if (no != null) { RavenMsg.Center(p, no); return false; }
            return Power.Spend(p, false, cost) && Power.Spend(p, true, cost);
        }

        internal static float CostOf(RavenAbility a)
        {
            float c = a.Id == "pwhirl" ? Whirl.StartMin : a.Id == "pload" ? Load.Cost : ClearSky.Cost;
            return c * GiftUp.CostMul(a.Id);
        }

        // ---------------------------------------------------------------- from the circle, the radial menu, the keys

        internal static void Use(Player p, RavenAbility a)
        {
            if (a.Id == "pwhirl" && Whirl.On()) { Whirl.End(p, null); return; }       // pressed again: home now, the rest kept
            if (a.Id == "pload" && Load.On()) { Load.End(p, true); return; }          // pressed again: the load dropped
            if (a.Id == "pclear" && ClearSky.Mine()) { ClearSky.End(p, true); return; } // pressed again: the clouds come back
            string no = RavenSkills.GiftBlock(a) ?? Why(p, a);
            if (no != null) { RavenMsg.Center(p, a.Name + ": " + no); return; }
            if (!RavenAbilities.Unlocked(a)) { RavenMsg.Center(p, a.Name + ": " + Lang.F("відкриється, коли обидва ворони будуть на {0}-му рівні довіри", RavenAbilities.UnlockLevel(a))); return; }
            float left = RavenSkills.LeftOf(a.Id);
            if (left > 0f) { RavenMsg.Center(p, Lang.F("{0}: ворони ще відпочивають. Зачекай {1}", a.Name, RavenAbilities.Span(left))); return; }
            string refused = a.Id == "pwhirl" ? Whirl.Refused(p) : a.Id == "pload" ? Load.Refused(p) : ClearSky.Refused(p);
            if (refused != null) { RavenMsg.Center(p, refused); return; }
            if (a.Id == "pwhirl")
            {
                string shortOf = Short(CostOf(a)); // (enough to start; each blow is paid as it goes)
                if (shortOf != null) { RavenMsg.Center(p, a.Name + ": " + shortOf); return; }
                Whirl.Start(p);
                return;
            }
            if (!Pay(p, CostOf(a))) return;
            RavenSkills.StartCooldown(a.Id);
            if (a.Id == "pload") Load.Start(p); else ClearSky.Start(p);
        }

        // ---------------------------------------------------------------- what the circle and the book show

        internal static string Status(RavenAbility a)
        {
            Player p = Player.m_localPlayer;
            if (a.Id == "pwhirl" && Whirl.On()) return Lang.T("<color=#FFD54F><b>вихор · натисни, щоб повернути воронів</b></color>");
            if (a.Id == "pload" && Load.On()) return Lang.F("<color=#FFD54F><b>тягнуть ношу · ще {0} · натисни, щоб кинули</b></color>", RavenAbilities.Span(Load.Left()));
            if (a.Id == "pclear" && ClearSky.Mine()) return Lang.F("<color=#FFD54F><b>ясно · ще {0} · натисни, щоб хмари вернулися</b></color>", RavenAbilities.Span(ClearSky.Left()));
            string no = RavenSkills.GiftBlock(a) ?? (p != null ? Why(p, a) : null);
            if (no != null) return "<color=#FF5A5A><b>" + no + "</b></color>";
            if (!RavenAbilities.Unlocked(a)) return RavenAbilities.LockedText(a);
            string cost = a.Id == "pwhirl"
                ? Lang.F("−{0} сили за кожен удар (є {1} і {2}) · ", Whirl.PerBlowNow().ToString("0.#"), Power.Current(false), Power.Current(true))
                : Lang.F("−{0} сили в кожного (є {1} і {2}) · ", Mathf.RoundToInt(CostOf(a)), Power.Current(false), Power.Current(true));
            float left = RavenSkills.LeftOf(a.Id);
            string shortOf = Short(CostOf(a));
            if (left <= 0f && shortOf != null) return cost + "<color=#FF5A5A><b>" + shortOf + "</b></color>";
            return cost + RavenAbilities.ReadyMark(left, Lang.T("готовий"));
        }

        internal static bool Ends(RavenAbility a) { return (a.Id == "pwhirl" && Whirl.On()) || (a.Id == "pload" && Load.On()) || (a.Id == "pclear" && ClearSky.Mine()); } // (pressing it ends it)

        internal static bool Usable(RavenAbility a)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return false;
            if (Ends(a)) return true;
            if (RavenSkills.GiftBlock(a) != null || Why(p, a) != null || !RavenAbilities.Unlocked(a)) return false;
            return RavenSkills.LeftOf(a.Id) <= 0f && Short(CostOf(a)) == null;
        }

        internal static int Dot(RavenAbility a)
        {
            Player p = Player.m_localPlayer;
            if (p == null || Trust.Worn(false) == null || Trust.Worn(true) == null || !RavenAbilities.Unlocked(a)) return RavenSkills.DotNone;
            if (Ends(a)) return RavenSkills.DotReady;
            if (RavenSkills.GiftBlock(a) != null || Why(p, a) != null || RavenSkills.LeftOf(a.Id) > 0f) return RavenSkills.DotWait;
            return Short(CostOf(a)) != null ? RavenSkills.DotWeak : RavenSkills.DotReady;
        }

        // ---------------------------------------------------------------- words
        // the gift's tag in the phrase files (PairPatterns), else one of these (a phrase file without it: an older one, another language)
        private static readonly System.Random Rng = new System.Random();
        internal static void Say(bool munin, string tag, string[] hugin, string[] mun)
        {
            string[] from = munin ? mun : hugin;
            RavenSkills.GiftSay(munin, tag, Lang.T(from[Rng.Next(from.Length)]));
        }
        internal static bool Coin() { return Rng.Next(2) == 1; }
    }

    // ---------------------------------------------------------------- «Вихор»

    internal static class Whirl
    {
        internal static float Radius = 10f, PerBlow = 5f, StartMin = 20f, Cooldown = 2400f, Pace = 1.25f, BossShare = 0.5f;
        // only the first blows of a whirl count as the ravens' deeds (the user, 2026-10-24): kills for Munin's strength,
        // errands, requests, nicknames, «Зграя»; the rest only strike
        internal static int Counted = 10;
        internal static float IdleSeconds = 8f; // no blow this long: it dies down (paid per blow, an idle whirl would wait for ever)
        private static int _blows;
        internal static bool Quiet() { return _on && _blows >= Counted; }
        internal static float PerBlowNow() { return PerBlow * GiftUp.CostMul("pwhirl"); } // (golden runes II: cheaper)

        private static bool _on;
        private static string _dry; // a raven could not pay for its blow: the next Tick ends it (not from inside the flight's callback)
        private static float _lastBlow;
        private static readonly Strike.Volley[] Volleys = new Strike.Volley[2];
        // when each foe was last given to a raven (the two spread over the crowd: the one waited longest goes first)
        private static readonly Dictionary<Character, float> Given = new Dictionary<Character, float>();
        private static readonly Dictionary<Player, Dictionary<Character, float>> GivenThere = new Dictionary<Player, Dictionary<Character, float>>(); // (other players' whirls, seen here)

        internal static bool On() { return _on; }

        internal static void Reset()
        {
            Player p = Player.m_localPlayer;
            if (_on && p != null) { StrikeFlight.WhirlEnd(p, false); StrikeFlight.WhirlEnd(p, true); NetFlag.Set(p, PairFx.WhirlKey, false); }
            _on = false; Given.Clear(); GivenThere.Clear(); Volleys[0] = Volleys[1] = null;
        }

        internal static string Refused(Player p)
        {
            if (Dome.Inside(p.transform.position)) return Lang.T("Під куполом ніхто не б'ється — і ворони теж");
            return null;
        }

        // a foe to whirl at: prey near you you can see, the one waited longest first (then the nearest)
        internal static Character Pick(Player p, Dictionary<Character, float> given)
        {
            if (p == null || p.IsDead()) return null;
            Character best = null;
            float bestKey = float.MaxValue;
            Vector3 at = p.transform.position, eye = p.GetEyePoint();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Strike.Prey(p, c) || !Foe(p, c)) continue;
                float d = Vector3.Distance(at, c.transform.position);
                if (d > Radius || !Strike.SeenFromAny(eye, c)) continue;
                float last;
                if (!given.TryGetValue(c, out last)) last = -999f;
                float key = last + d * 0.01f;
                if (key < bestKey) { bestKey = key; best = c; }
            }
            if (best != null) { if (given.Count > 64) given.Clear(); given[best] = Time.time; }
            return best;
        }

        // a foe, not the deer grazing by: monsters, and any beast that has gone for you
        private static bool Foe(Player p, Character c)
        {
            if (c.GetFaction() != Character.Faction.AnimalsVeg) return true;
            MonsterAI ai = c.GetBaseAI() as MonsterAI;
            return ai != null && ai.GetTargetCreature() == p;
        }

        internal static void Start(Player p)
        {
            Given.Clear();
            _blows = 0;
            bool ok = true;
            for (int i = 0; i < 2 && ok; i++)
            {
                bool munin = i == 1;
                Strike.Volley v = Strike.NewVolley(munin, Trust.Worn(munin));
                v.Blow *= GiftUp.Power("pwhirl"); // (golden runes I: harder)
                if (Facets.On("pwhirl")) v.Frost = Mathf.Max(v.Frost, FrostShare); // (III: the blows chill — the game's frost slows them)
                Volleys[i] = v;
                Func<Character, Strike.Peck> pick = c => Strike.VolleyPeck(v, c);
                ok = StrikeFlight.Whirl(p, munin, () => Pick(p, Given), pick, (k, from) => Hit(p, munin, v, k, from), Pace);
            }
            if (!ok) { StrikeFlight.WhirlEnd(p, false); StrikeFlight.WhirlEnd(p, true); RavenMsg.Center(p, Lang.T("Вихор не вдався: ворони не злетіли")); return; }
            _on = true;
            _dry = null;
            _lastBlow = Time.time;
            NetFlag.Set(p, PairFx.WhirlKey, true); // (every game: the whirl's look and its copies of the birds, PairFx)
            PairGifts.Gesture(p, "pair_whirl"); // you throw them off into it
            PairGifts.Say(PairGifts.Coin(), "pwhirl", Hugin, Munin);
        }

        internal static float FrostShare = 0.25f;

        private static void Hit(Player p, bool munin, Strike.Volley v, int k, Vector3 from)
        {
            Strike.Peck peck = StrikeFlight.PeckOf(p, munin, k);
            if (peck == null || peck.Target == null || peck.Target.IsDead() || _dry != null) return;
            // each blow is paid by the raven that strikes it; the first one dry ends it for both
            float cost = PerBlowNow();
            if (!Power.Afford(munin, cost)) { _dry = Lang.F("Вихор стих: {0} видихся", munin ? Lang.T("Мунін") : Lang.T("Хугін")); return; } // (a hungry one pays more)
            if (!Power.Spend(p, munin, cost)) { _dry = ""; return; } // (Spend said why)
            _lastBlow = Time.time;
            bool counts = _blows < Counted;
            _blows++;
            Strike.VolleyHit(p, munin, v, peck, from, 1f, BossShare, counts);
            Twists.Pull(p, peck.Target); // (II: drawn in)
        }

        // why: the line in the middle (null: pressed again, nothing to say but the word)
        internal static void End(Player p, string why)
        {
            if (!_on) return;
            _on = false;
            StrikeFlight.WhirlEnd(p, false);
            StrikeFlight.WhirlEnd(p, true);
            if (p != null) NetFlag.Set(p, PairFx.WhirlKey, false);
            for (int i = 0; i < 2; i++) { Strike.VolleyDone(p, i == 1, Volleys[i]); Volleys[i] = null; }
            Given.Clear();
            RavenSkills.StartCooldown("pwhirl");
            if (p == null || p.IsDead()) return;
            if (why != null) RavenMsg.Center(p, why);
            PairGifts.Say(PairGifts.Coin(), "pwhirl_end", HuginEnd, MuninEnd);
        }

        internal static void Tick(Player p)
        {
            if (!_on) return;
            if (p == null || p.IsDead()) { End(p, null); return; }
            if (Trust.Worn(false) == null || Trust.Worn(true) == null || !StrikeFlight.Whirling(p, false) || !StrikeFlight.Whirling(p, true)) { End(p, Lang.T("Вихор урвався")); return; }
            if (Dome.Inside(p.transform.position)) { End(p, Lang.T("Під куполом вихор стих")); return; }
            if (_dry != null) { string why = _dry; _dry = null; End(p, why.Length > 0 ? why : null); return; }
            if (Time.time - _lastBlow > IdleSeconds) End(p, Lang.T("Вихор стих: довкола нікого"));
        }

        // ---- another player's whirl, seen here (PairFx, from their flag): this game flies its own copies of the two
        // birds round them, picking their foes by itself; the blows are only the caster's game's
        internal static void Watch(Player owner, bool on)
        {
            if (owner == null || ReferenceEquals(owner, Player.m_localPlayer)) return;
            if (GivenThere.Count > 8) { List<Player> gone = new List<Player>(); foreach (Player k in GivenThere.Keys) if (k == null) gone.Add(k); foreach (Player k in gone) GivenThere.Remove(k); }
            Dictionary<Character, float> given;
            if (!on)
            {
                if (!GivenThere.Remove(owner)) return;
                StrikeFlight.WhirlEnd(owner, false); StrikeFlight.WhirlEnd(owner, true);
                return;
            }
            if (!GivenThere.TryGetValue(owner, out given)) { given = new Dictionary<Character, float>(); GivenThere[owner] = given; }
            for (int i = 0; i < 2; i++)
                if (!StrikeFlight.Whirling(owner, i == 1))
                    StrikeFlight.Whirl(owner, i == 1, () => Pick(owner, given), c => new Strike.Peck { Target = c, Id = c.GetZDOID() }, null, Pace);
        }

        private static readonly string[] Hugin = Lang.Keys("Тримайся ближче. Я не люблю ловити тих, хто тікає.", "Ну, хто перший у черзі?", "Кружляємо. Ти просто стій і виглядай грізно.");
        private static readonly string[] Munin = Lang.Keys("Ану всі сюди, падлюки, по черзі!", "Розлітайтесь, бо зараз буде м'ясо!", "Нарешті! Я все життя цього чекав.");
        private static readonly string[] HuginEnd = Lang.Keys("Досить. Крила не залізні.", "Все. Решту добивай сам.", "Перепочинок. Потім продовжимо.");
        private static readonly string[] MuninEnd = Lang.Keys("Фух... Хто живий — тому пощастило.", "Видихся я. Не дивись так.", "Все, кінчилась сила. Наступного разу більше вбивай, буде більше сили.");
    }

    // ---------------------------------------------------------------- «Подвійна ноша»

    internal static class Load
    {
        internal static float Bonus = 150f, Seconds = 600f, Cost = 40f, Cooldown = 1800f, Fall = 0.5f, FallTop = 0.75f; // (Fall: share of fall damage the ropes take; FallTop with golden runes III)
        internal static float FallNow() { return Facets.On("pload") ? FallTop : Fall; }
        internal static float MoanMin = 40f, MoanMax = 80f;
        internal static float BonusNow() { return Mathf.Round(Bonus * GiftUp.Power("pload")); } // (golden runes I: more)
        internal static float Duration() { return Seconds; }
        internal const string EffectName = "RavenWatch_Load";
        internal static readonly int Hash = EffectName.GetStableHashCode();
        private static SE_Stats _template;
        private static bool _on;
        private static double _until;
        private static float _moanAt, _lookAt;

        internal static bool On() { return _on; }
        internal static float Left() { return _on ? Mathf.Max(0f, (float)(_until - WorldTime.Now)) : 0f; }
        internal static void Reset()
        {
            Player p = Player.m_localPlayer;
            if (_on && p != null) { p.GetSEMan().RemoveStatusEffect(Hash, true); NetFlag.Set(p, PairFx.LoadKey, false); Orders.FlyWithEnd(p, false); Orders.FlyWithEnd(p, true); }
            _on = false;
        }

        internal static string Refused(Player p)
        {
            if (OdinWings.Flying()) return Lang.T("У польоті ношу не беруть");
            return null;
        }

        internal static void Start(Player p)
        {
            _on = true;
            _until = WorldTime.Now + Duration();
            SE_Stats se = RavenFx.Template(ref _template, EffectName);
            se.m_name = Lang.W("Подвійна ноша");
            se.m_tooltip = Facets.On("pload")
                ? Lang.F("Ворони несуть частину вантажу: +{0} до ваги, і перевантаження не сповільнює. Падаєш м'якше: −{1}% шкоди", Mathf.RoundToInt(BonusNow()), Mathf.RoundToInt(FallNow() * 100f))
                : Lang.F("Ворони несуть частину вантажу: +{0} до ваги. Падаєш м'якше: −{1}% шкоди", Mathf.RoundToInt(BonusNow()), Mathf.RoundToInt(FallNow() * 100f));
            se.m_icon = AbilityIcons.ForEffect("pload", false);
            se.m_ttl = Duration();
            se.m_addMaxCarryWeight = BonusNow();
            se.m_fallDamageModifier = -FallNow(); // they hold you up on the ropes as you drop
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
            NetFlag.Set(p, PairFx.LoadKey, true); // (every game: the ropes, the heavy flight, PairFx)
            PairGifts.Gesture(p, "pair_load"); // the bundle lifted up to them
            Fly(p, true);
            _moanAt = Time.time + UnityEngine.Random.Range(MoanMin * 0.5f, MoanMax * 0.5f);
            PairGifts.Say(PairGifts.Coin(), "pload", Hugin, Munin);
        }

        // both beside you, the same flight as the order «Лети поруч», for the load's time (and a little over: it ends it);
        // at the start whatever they were doing, later only one back on the shoulder (not one you sent off meanwhile)
        private static void Fly(Player p, bool start)
        {
            for (int i = 0; i < 2; i++)
            {
                bool munin = i == 1;
                int o = Orders.Of(p, munin);
                if (o == Orders.Follow || (!start && o != Orders.None) || Strike.Busy(p, munin) || !Shoulder.Arrived(p, munin)) continue;
                Orders.FlyWith(p, munin, Left() + 30f);
            }
        }

        // dropped: pressed again (early), its time is up, you died, a raven taken off
        internal static void End(Player p, bool early)
        {
            if (!_on) return;
            _on = false;
            if (p == null) return;
            p.GetSEMan().RemoveStatusEffect(Hash, true);
            NetFlag.Set(p, PairFx.LoadKey, false);
            Orders.FlyWithEnd(p, false); Orders.FlyWithEnd(p, true);
            if (p.IsDead()) return;
            PairGifts.Say(PairGifts.Coin(), early ? "pload_drop" : "pload_end", early ? HuginDrop : HuginEnd, early ? MuninDrop : MuninEnd);
        }

        internal static void Tick(Player p)
        {
            if (!_on) return;
            if (p == null || p.IsDead() || WorldTime.Now >= _until || Trust.Worn(false) == null || Trust.Worn(true) == null) { End(p, false); return; }
            if (Time.time >= _lookAt)
            {
                // back beside you whenever they may (under a roof, swimming or asleep they sit on the shoulders: the load is still theirs)
                _lookAt = Time.time + 1f;
                if (!Outing.Indoors() && !Senses.Swimming && !Senses.Sleeping) Fly(p, false);
            }
            if (Time.time >= _moanAt)
            {
                _moanAt = Time.time + UnityEngine.Random.Range(MoanMin, MoanMax);
                if (Director.May(Director.Voice.Shoulder)) { PairGifts.Say(PairGifts.Coin(), "pload_moan", HuginMoan, MuninMoan); Director.Spoke(Director.Voice.Shoulder, 4f); }
            }
        }

        private static readonly string[] Hugin = Lang.Keys("Давай сюди. Тільки не звикай.", "Гаразд, понесу. Але це записано.");
        private static readonly string[] Munin = Lang.Keys("Ну давай, вантаж. Ішак пернатий до твоїх послуг.", "Знову я тягаю твій мотлох. Чудово. Просто чудово.");
        private static readonly string[] HuginMoan = Lang.Keys("Що ти туди наклав — каміння?", "Я ворон, а не віл.", "Ще крок — і я подам скаргу Одіну.", "Мої крила для неба, не для твого мотлоху.", "Ти колись чув про скриню?");
        private static readonly string[] MuninMoan = Lang.Keys("Бляха, яке ж воно важке!", "Ти що, всю шахту з собою тягнеш?", "Ще трохи — і я це все скину тобі на голову.", "Мені за це ніхто не платить, між іншим.", "Хребет тріщить. У ворона є хребет? Тепер тріщить.");
        private static readonly string[] HuginEnd = Lang.Keys("Все. Крила відвалюються.", "Далі сам. Я своє відніс.");
        private static readonly string[] MuninEnd = Lang.Keys("Тримай своє барахло. Я пас.", "Все, приїхали. Неси сам, герой.");
        private static readonly string[] HuginDrop = Lang.Keys("Вже? Ну, як скажеш.", "Чудово. Крила дякують.");
        private static readonly string[] MuninDrop = Lang.Keys("О, нарешті! Тримай, лови!", "Кинув. І не проси більше.");
    }

    // «Подвійна ноша» III: the load does not slow you, however much you carry
    [HarmonyPatch(typeof(Player), "IsEncumbered")]
    internal static class LoadEncumberedPatch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (__result && Load.On() && ReferenceEquals(__instance, Player.m_localPlayer) && Facets.On("pload")) __result = false;
        }
    }

    // ---------------------------------------------------------------- «Розігнати хмари»
    // The weather is every game's own: the caster writes on its ZDO till when and how wide; every game in that circle
    // (round the caster, wherever it walks) takes its land's calmest weather instead — through the game's own weather
    // override, so a dungeon, a boss's or a place's own weather still wins — and the mist gives way to a demister there.
    internal static class ClearSky
    {
        internal static float Radius = 25f, Seconds = 300f, Cost = 25f, Cooldown = 1800f;
        internal static float RadiusNow() { return Radius * (Facets.On("pclear") ? 2f : 1f); } // (golden runes III: twice as wide)
        internal static float Duration() { return Mathf.Round(Seconds * GiftUp.Power("pclear")); } // (golden runes I: longer)
        internal const string EndKey = "RavenWatch_ClearEnd", RadiusKey = "RavenWatch_ClearR";

        private static bool _mine;
        private static double _until;
        private static float _lookAt;
        internal static string Env; // the weather to take here now (null: the game's own)
        private static readonly Dictionary<Player, GameObject> Demisters = new Dictionary<Player, GameObject>();
        private static readonly Dictionary<Heightmap.Biome, string> CalmOf = new Dictionary<Heightmap.Biome, string>();

        internal static bool Mine() { return _mine; }

        // how high the ravens may circle over this player now: 0 — no clear sky of theirs; else the room over their head
        // up to SkyCheck m (a roof, a tree's crown, an overhang: lower), looked up at most twice a second per player
        internal static float HighHugin = 15f, HighMunin = 13f, RingMin = 9f, RingMax = 14f, SkyCheck = 18f;
        private static readonly Dictionary<Player, Vector2> RoomOf = new Dictionary<Player, Vector2>(); // (when looked, the room)
        private static int _skyMask;
        internal static float Room(Player pl)
        {
            if (pl == null) return 0f;
            Vector2 v;
            if (RoomOf.TryGetValue(pl, out v) && Time.time - v.x < 0.5f) return v.y;
            float room = 0f;
            ZDO z = NetFlag.Zdo(pl, false);
            if (z != null && z.GetFloat(EndKey, 0f) > WorldTime.Now && !pl.InInterior())
            {
                if (_skyMask == 0) _skyMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small", "vehicle");
                Vector3 from = pl.transform.position + Vector3.up * 2f;
                RaycastHit hit;
                room = Physics.SphereCast(from, 0.6f, Vector3.up, out hit, SkyCheck, _skyMask, QueryTriggerInteraction.Ignore) ? hit.distance + 2f : SkyCheck + 2f;
            }
            if (RoomOf.Count > 32) RoomOf.Clear();
            RoomOf[pl] = new Vector2(Time.time, room);
            return room;
        }
        internal static float Left() { return _mine ? Mathf.Max(0f, (float)(_until - WorldTime.Now)) : 0f; }

        internal static void Reset()
        {
            Player p = Player.m_localPlayer;
            ZDO z = _mine && p != null ? NetFlag.Zdo(p, true) : null;
            if (z != null) z.Set(EndKey, 0f);
            _mine = false; Env = null;
            RoomOf.Clear();
            foreach (GameObject go in Demisters.Values) if (go != null) UnityEngine.Object.Destroy(go);
            Demisters.Clear();
            CalmOf.Clear();
        }

        internal static string Refused(Player p)
        {
            if (p.InInterior() || Senses.Underground) return Lang.T("Під землею неба нема — нема й хмар");
            if (Outing.Indoors()) return Lang.T("Вийди з-під даху — ворони мають бачити небо");
            return null;
        }

        internal static void Start(Player p)
        {
            _mine = true;
            _until = WorldTime.Now + Duration();
            ZDO z = NetFlag.Zdo(p, true);
            if (z != null) { z.Set(EndKey, (float)_until); z.Set(RadiusKey, RadiusNow()); }
            Twists.ClearFlags(p, true); // (II: plants grow; III: the moon at night)
            for (int i = 0; i < 2; i++) if (!Strike.Busy(p, i == 1) && Shoulder.Arrived(p, i == 1)) Orders.FlyWith(p, i == 1, Duration() + 30f, Orders.Circle);
            _lookAt = 0f;
            PairGifts.Gesture(p, "pair_clear"); // your arm sweeps the sky
            PairGifts.Say(PairGifts.Coin(), "pclear", Hugin, Munin);
        }

        internal static void End(Player p, bool early)
        {
            if (!_mine) return;
            _mine = false;
            ZDO z = p != null ? NetFlag.Zdo(p, true) : null;
            if (z != null) z.Set(EndKey, 0f);
            if (p == null) return;
            Orders.FlyWithEnd(p, false, Orders.Circle); Orders.FlyWithEnd(p, true, Orders.Circle);
            if (!p.IsDead()) PairGifts.Say(PairGifts.Coin(), early ? "pclear_early" : "pclear_end", early ? HuginEarly : HuginEnd, early ? MuninEarly : MuninEnd);
        }

        // twice a second, every game: am I under anyone's clear sky; the demisters round each caster near
        internal static void Tick(Player me)
        {
            if (Time.time < _lookAt) return;
            _lookAt = Time.time + 0.5f;
            double now = WorldTime.Now;
            if (_mine && (me == null || me.IsDead() || now >= _until)) End(me, false);
            else if (_mine && me != null && !Strike.Busy(me, false) && !Strike.Busy(me, true) && !Outing.Indoors())
                for (int i = 0; i < 2; i++) if (Orders.Of(me, i == 1) == Orders.None && Shoulder.Arrived(me, i == 1)) Orders.FlyWith(me, i == 1, Left() + 30f, Orders.Circle); // (back up after a strike)
            bool under = false;
            List<Player> gone = null;
            foreach (KeyValuePair<Player, GameObject> kv in Demisters) if (kv.Key == null || kv.Value == null) (gone ?? (gone = new List<Player>())).Add(kv.Key);
            if (gone != null) foreach (Player g in gone) { GameObject go; if (Demisters.TryGetValue(g, out go) && go != null) UnityEngine.Object.Destroy(go); Demisters.Remove(g); }
            foreach (Player pl in Player.GetAllPlayers())
            {
                ZDO z = NetFlag.Zdo(pl, false);
                float end = z != null ? z.GetFloat(EndKey, 0f) : 0f, r = z != null ? z.GetFloat(RadiusKey, Radius) : Radius;
                bool on = end > now && !pl.IsDead() && !pl.InInterior();
                bool near = me != null && Vector3.Distance(me.transform.position, pl.transform.position) <= 120f;
                Mist(pl, on && near, r);
                if (on && me != null && Vector3.Distance(me.transform.position, pl.transform.position) <= r) under = true;
            }
            Env = under && me != null ? Calm(Heightmap.FindBiome(me.transform.position)) : null;
        }

        // its name says what the numbers miss (a forest's mist is particles, not fog): a clear one first, a misty or stormy one last
        private static float ByName(string n)
        {
            n = (n ?? "").ToLowerInvariant();
            if (n.Contains("clear")) return -30f;
            return n.Contains("mist") || n.Contains("fog") || n.Contains("storm") || n.Contains("rain") || n.Contains("snow") || n.Contains("thunder") || n.Contains("ash") ? 25f : 0f;
        }

        // the calmest weather this land has: dry first, then not freezing, not cold, the fewest clouds and fog, least wind
        private static string Calm(Heightmap.Biome b)
        {
            string name;
            if (CalmOf.TryGetValue(b, out name)) return name;
            name = "Clear";
            EnvMan em = EnvMan.instance;
            float best = float.MaxValue;
            if (em != null && em.m_biomes != null)
                foreach (BiomeEnvSetup bs in em.m_biomes)
                {
                    if (bs == null || bs.m_biome != b || bs.m_environments == null) continue;
                    foreach (EnvEntry e in bs.m_environments)
                    {
                        EnvSetup s = null;
                        foreach (EnvSetup x in em.m_environments) if (x != null && x.m_name == e.m_environment) { s = x; break; }
                        if (s == null) continue;
                        float score = (s.m_isWet ? 100f : 0f) + (s.m_isFreezing ? 50f : 0f) + (s.m_isFreezingAtNight ? 20f : 0f) + (s.m_isCold ? 10f : 0f) + (s.m_isColdAtNight ? 4f : 0f)
                            + s.m_rainCloudAlpha * 5f + s.m_fogDensityDay * 50f + s.m_windMax * 2f + ByName(s.m_name);
                        if (score < best) { best = score; name = s.m_name; }
                    }
                }
            // every weather of this land misty or wet (the Black Forest): the game's own plain clear sky instead
            if (ByName(name) > 0f && em != null) foreach (EnvSetup x in em.m_environments) if (x != null && x.m_name == "Clear") { name = "Clear"; break; }
            CalmOf[b] = name;
            return name;
        }

        // the Wisplight's own demister (the very thing the mist gives way to), grown to the circle, round the caster
        private static void Mist(Player pl, bool on, float r)
        {
            GameObject go;
            Demisters.TryGetValue(pl, out go);
            if (!on) { if (go != null) { UnityEngine.Object.Destroy(go); Demisters.Remove(pl); } return; }
            if (go == null)
            {
                GameObject ball = RavenLight.Wisp();
                Demister d = ball != null ? ball.GetComponentInChildren<Demister>(true) : null;
                if (d == null) return;
                // built switched off (as the light's wisp: RavenLight.Make), so nothing in the copy wakes up — no
                // network object — before it is stripped down to the demister and its force field
                go = new GameObject("RavenWatch_ClearSky");
                go.SetActive(false);
                GameObject copy = UnityEngine.Object.Instantiate(d.gameObject, go.transform, false);
                copy.transform.localPosition = Vector3.zero;
                foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null && !(mb is Demister)) UnityEngine.Object.DestroyImmediate(mb);
                foreach (ParticleSystem x in copy.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(x);
                foreach (Renderer x in copy.GetComponentsInChildren<Renderer>(true)) UnityEngine.Object.DestroyImmediate(x);
                foreach (Light x in copy.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(x);
                foreach (AudioSource x in copy.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(x);
                foreach (Collider x in copy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(x);
                foreach (Rigidbody x in copy.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(x);
                go.SetActive(true);
                Demisters[pl] = go;
            }
            go.transform.position = pl.transform.position + Vector3.up * 2f;
            foreach (ParticleSystemForceField f in go.GetComponentsInChildren<ParticleSystemForceField>(true)) if (Mathf.Abs(f.endRange - r) > 0.1f) f.endRange = r;
        }

        private static readonly string[] Hugin = Lang.Keys("Тримайся під нами. Небо сьогодні наше.", "Хмари, геть. Наказ Одіна... ну, майже.");
        private static readonly string[] Munin = Lang.Keys("Ану розступились, хмари вонючі!", "Зараз я цим хмарам пір'я повищипую.");
        private static readonly string[] HuginEnd = Lang.Keys("Все. Небо знову саме по собі.", "Хмари вертаються. Я попереджав, що не назавжди.");
        private static readonly string[] MuninEnd = Lang.Keys("Все, далі мокни сам.", "Крила затерпли. Хай ллє.");
        private static readonly string[] HuginEarly = Lang.Keys("Як скажеш. Хмари, можна.");
        private static readonly string[] MuninEarly = Lang.Keys("Що, дощику захотілось? Тримай.");
    }

    // the weather of a clear sky, where the game itself has nothing else to put (a dungeon, a boss, a place keep theirs)
    [HarmonyPatch(typeof(EnvMan), "GetEnvironmentOverride")]
    internal static class ClearSkyEnvPatch
    {
        private static void Postfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result) && ClearSky.Env != null) __result = ClearSky.Env;
        }
    }
}
#endif
