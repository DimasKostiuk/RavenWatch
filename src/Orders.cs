#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Накази»: what you tell a raven to do (the orders face of OrdersMenu: J, the gifts circle's centre, G → Ravens → Накази).
    // An order is a plan of the motor with its cost in strength, its wait and how long it lasts: fly beside you,
    // circle above you, a short scouting ahead (enemies marked on the map for a while), a barrel roll, a loop, a dive.
    // «Повернись» calls it back for free. The owner's game writes the order on the player's ZDO; every game flies it
    // (ShoulderPin, air mode 4, act Command: stronger than an outing or the dance, weaker than a strike or the dome).
    // Called back early, as outings are: you died, went in, swim, sleep, went far at once.
    internal static class Orders
    {
        internal const int None = 0, Follow = 1, Circle = 2, Scout = 3, Roll = 4, Loop = 5, Dive = 6,
            Eight = 7, Candle = 8, Leaf = 9, Immelmann = 10, Hammer = 11, Skim = 12, Waves = 13, Chase = 14, Fetch = 15;
        internal const int Count = 16; // (Fetch: «Принести», given by «Туди» on a thing you look at: RavenFetch)
        // one raven's figures (TrickPlan), in the menu's order; the chase is for both together
        internal static readonly int[] Tricks = { Roll, Loop, Dive, Eight, Candle, Leaf, Immelmann, Hammer, Skim, Waves };
        internal const string KeyH = "RavenWatch_OrdH", KeyM = "RavenWatch_OrdM";

        // What an order costs (the user, 2026-10-14): strength 1, the scouting 10 (a quarter of Hugin's own scouting gift,
        // for a quarter of what it shows), × its knack «Ощадливий»; and food, by how much it flies (the play is paid for
        // in hunger, so it never eats the strength of the gifts), × its knack «Невибагливий»; every order is a deed
        // together (Trust.Ordered, a little a day)
        internal static float Cost = 1f, ScoutCost = 10f;
        internal static float HungerStay = 2.5f, HungerTrick = 1.25f, HungerChase = 5f; // satiety: beside / circling (5 min), a figure, the chase (each)
        internal static float CostOf(bool munin, int order) { return (order == Scout ? ScoutCost : Cost) * Norov.CostMul(Trust.Worn(munin)); }
        internal static float HungerOf(int order) { return order == Follow || order == Circle ? HungerStay : order == Chase ? HungerChase : IsTrick(order) || order == Fetch ? HungerTrick : 0f; }
        // "1 сили · ситість −1,8 · чекати 5 с" (the food: its own and the strength's share, as it will come off)
        internal static string PriceText(bool munin, int order)
        {
            float cost = CostOf(munin, order), food = HungerOf(order) * Satiety.HungerMul(Trust.Worn(munin)) + cost * Satiety.GiftShare;
            float wait = WaitOf(order);
            return Lang.F("{0} сили · ситість −{1}{2}", Mathf.RoundToInt(Power.Price(munin, cost)), Satiety.Num(Mathf.Round(food * 10f) / 10f), (wait > 0f ? Lang.F(" · чекати {0} с", Mathf.RoundToInt(wait)) : ""));
        }
        internal static float LastSeconds = 300f;        // beside / circling: 5 min unless called back
        // the scouting: out as far as its training lets it fly (Strike.RangeOf), straight on; where it turns it sees the
        // enemies and the ways under the ground ahead of you within ScoutRange, marked for ScoutMarkSeconds; then home
        internal static float ScoutRange = 95f, ScoutMarkSeconds = 10f, ScoutPace = 7f; // (ScoutPace: m/s at least, for its time limit)
        internal const string DistH = "RavenWatch_OrdDH", DistM = "RavenWatch_OrdDM";
        private static readonly Vector3[] ScoutDir = new Vector3[2];
        internal static float ScoutWait = 180f, TrickWait = 5f, ChaseWait = 120f; // (tricks: not a button to hammer)
        internal static float ChaseMin = 90f, ChaseMax = 150f; // «Догонялки»: about two minutes, the same for both
        internal static float ChaseTalk = 0.4f, ChaseTalkGap = 20f; // a word at a catch: this often, never sooner than this (both)
        private static float _chaseSaidAt = -100f;
        private static float _chaseFor;

        internal static string[] Names { get { return Lang.Arr(NamesSrc); } } // (translated on every read)
        internal static readonly string[] NamesSrc = Lang.Keys("", "Лети поруч", "Кружляй наді мною", "Облетіти", "Бочка", "Петля", "Пікірування", "Вісімка", "Свічка", "Падаючий лист", "Імельман", "Гірка", "Бриючий проліт", "Хвилі", "Догонялки", "Принести");
        // what each order does, in a line (the orders menu's line under the circle)
        internal static string[] Desc { get { return Lang.Arr(DescSrc); } } // (translated on every read)
        private static readonly string[] DescSrc = Lang.Keys("", "Летить поруч, трохи позаду й вище свого плеча, скільки триває наказ або доки не скажеш «Повернись».", "Кружляє над тобою, скільки триває наказ або доки не скажеш «Повернись».", "Облітає місцину: летить майже по прямій туди, куди ти дивишся, так далеко, як дозволяє вишкіл, і плавно повертається на плече. Вороги й входи під землю попереду — на карті на 10 с.", "Бочка: летить уздовж твого шляху, обертаючись навколо себе.", "Повне коло вгору й через голову.", "Злітає високо, круто пірнає перед тобою й виходить над самою землею.", "Вісімка над тобою, з креном у кожному повороті.", "Тугою спіраллю вгору метрів на дванадцять, а тоді довго ширяє донизу широким колом.", "Падає з висоти, гойдаючись з боку в бік, як листок, і перед самою землею ловить повітря.", "Півпетлі вгору й переворот нагорі: летить назад, вище, ніж був.", "Стрімко вгору до зупинки, розворот на місці, донизу й назад до тебе.", "Низько над землею далеко вперед, розворот із підйомом і назад до тебе.", "Кружляє довкола тебе низько, пірнаючи й злітаючи, як на гойдалці.", "Обидва ворони грають у квача довкола тебе: хто наздогнав — розвертається й тікає, тепер ловлять його. Близько двох хвилин.", "Летить до дрібниці, на яку ти дивишся, хапає одну штуку й приносить тобі в сумку.");
        internal static readonly string[] Plans = { "", "слідом", "коло", "розвідка", "бочка", "петля", "пікірування",
            "вісімка", "свічка", "лист", "імельман", "гірка", "бриючий", "хвилі", "догонялки", "принести" };
        // a line in the corner from this raven, with its picture (the corner card has a place for one)
        private static void Say(Player p, bool munin, string text) { if (p != null) p.Message(MessageHud.MessageType.TopLeft, Grammar.Fix(text, p), 0, RavenItem.Icon(munin)); }

        // hungry or starving (satiety up to the hungry mark): no orders
        private static bool Hungry(ItemDrop.ItemData item) { return Satiety.Zone(Satiety.Value(item)) <= Satiety.Hungry; }

        internal static bool IsTrick(int o) { return o >= Roll && o < Count && o != Chase && o != Fetch; } // (the chase is a game of its own: ChasePlan)

        // the raven's own word on an order (assembled from parts, Patterns: order_*): not more often than SpeakGap per
        // raven, a trick only now and then (they are given one after another)
        internal static float SpeakGap = 8f, TrickSpeaks = 0.5f;
        private static readonly float[] SpokeAt = { -100f, -100f };
        private static void Voice(Player p, bool munin, int order)
        {
            string tag = order == Follow ? "order_follow" : order == Circle ? "order_circle" : order == Scout ? "order_scout" : order == Chase ? "order_chase" : "order_trick";
            Speak(p, munin, tag, IsTrick(order) ? Names[order] : null, IsTrick(order) ? TrickSpeaks : 1f);
        }
        private static void Speak(Player p, bool munin, string tag, string trick, float chance)
        {
            int i = munin ? 1 : 0;
            if (p == null || Time.time - SpokeAt[i] < SpeakGap || Random.value >= chance) return;
            string line = trick != null ? Satiety.Say(p, munin, tag, "trick", trick) : Satiety.Say(p, munin, tag);
            if (string.IsNullOrEmpty(line)) return;
            SpokeAt[i] = Time.time;
            RavenSkills.SayFromShoulder(p, munin, line);
        }

        internal static bool Busy(Player p, bool munin) { return p != null && Of(p, munin) != None; }
        internal static string BusyText(Player p, bool munin) { int o = p != null ? Of(p, munin) : None; return o != None ? Lang.F("виконує «{0}»", Names[o]) : ""; }

        private static readonly float[] Until = { 0f, 0f }, Span = { 1f, 1f }; // when the order ends, and how long it was given for

        // how long the raven's order still goes, and that as a share of all of it (the circle's ring round the button)
        internal static float TimeLeft(bool munin) { return Mathf.Max(0f, Until[munin ? 1 : 0] - Time.time); }
        internal static float TimeShare(bool munin) { int i = munin ? 1 : 0; return Mathf.Clamp01(TimeLeft(munin) / Mathf.Max(0.01f, Span[i])); }
        private static readonly Dictionary<int, float> ReadyAt = new Dictionary<int, float>(); // (raven, order) → when it may be given again

        internal static int Of(Player p, bool munin)
        {
            ZDO z = NetFlag.Zdo(p, false);
            return z != null ? z.GetInt(munin ? KeyM : KeyH, None) : None;
        }

        private static void Set(Player p, bool munin, int order)
        {
            ZDO z = NetFlag.Zdo(p, true);
            string key = munin ? KeyM : KeyH;
            if (z != null && z.GetInt(key, None) != order) z.Set(key, order);
        }

        private static int Slot(bool munin, int order) { return (munin ? 100 : 0) + order; }

        internal static float WaitOf(int order) { return order == Scout ? ScoutWait : order == Chase ? ChaseWait : IsTrick(order) || order == Fetch ? TrickWait : 0f; }

        internal static float LeftOf(bool munin, int order)
        {
            float at;
            return ReadyAt.TryGetValue(Slot(munin, order), out at) ? Mathf.Max(0f, at - Time.time) : 0f;
        }

        // why this order cannot be given now (null: it can)
        internal static string Why(Player p, bool munin, int order)
        {
            if (p == null || p.IsDead()) return Lang.T("ти не тут");
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null) return Lang.T("не на плечі");
            if (!Trust.Active(item)) return Lang.T("ще спить");
            if (Trust.Stranger(item, p)) return Lang.T("не твій");
            if (Perches.On(item) && Trust.AwayLeft(item) <= 0f) return Perches.OnSpot(item) ? Lang.T("сидить окремо") : Perches.OnWord(item); // (a «sit here» spot: a stone, the ground — no perch)
            if (Trust.Away(item)) return Trust.AwayWord(munin, item);
            if (!munin && Shoulder.HuginAway) return Lang.T("на розвідці");
            if (Hungry(item)) return Trust.Starving(item) ? Lang.T("голодує — не полетить") : Lang.T("голодний — не полетить");
            string quirk = Quirks.OrderBlock(p, munin, order); // sulking; asleep at night (no scouting)
            if (quirk != null) return quirk;
            int cur = Of(p, munin);
            if (cur == order) return Lang.T("уже виконує"); // another order: it switches in the air (ShoulderPin)
            GameObject model = Shoulder.ModelOf(p, munin);
            if (model == null) return Lang.T("не видно");
            ShoulderPin pin = model.GetComponent<ShoulderPin>();
            if (pin == null || !pin.enabled) return Lang.T("зайнятий");
            if (pin.Arriving) return Lang.T("ще прилітає");
            RavenAct act = AnimDirector.Of(model);
            if (act > RavenAct.Command) return Lang.F("зайнятий ({0})", (act == RavenAct.Strike ? Lang.T("удар") : act == RavenAct.Dome ? Lang.T("купол") : Lang.T("відлітає")));
            if (Outing.Indoors()) return Lang.T("під дахом не полетить");
            float left = LeftOf(munin, order);
            if (left > 0f) return Lang.F("ще {0} с", Mathf.CeilToInt(left));
            if (Power.Get(item) < Power.Price(munin, CostOf(munin, order))) return Lang.T("мало сили");
            return null;
        }

        internal static bool Give(Player p, bool munin, int order)
        {
            string why = Why(p, munin, order);
            if (why != null)
            {
                Say(p, munin, Lang.F("{0}: {1} — {2}", Plugin.RavenName(munin), Lang.Lower(Names[order]), why));
                ItemDrop.ItemData it = p != null ? RavenSeat.On(p, munin) : null;
                if (it != null && Hungry(it)) Trust.OrderPeck(p, munin, it); // hungry and still ordered about: a peck (below trust 6)
                if (order == Scout && Quirks.AsleepForScout(p, munin) && why == Quirks.OrderBlock(p, munin, order)) Quirks.SaySleepy(p, munin); // (the night was the reason)
                return false;
            }
            if (!Power.Spend(p, munin, CostOf(munin, order))) return false;
            if (Quirks.Willful(p, munin, order)) return false; // «Свавільний»: not done, the strength spent
            if (order == Scout) Nicks.OnScout(p, munin); // a deed for its nickname
            Satiety.Flew(munin, HungerOf(order)); // the flying makes it hungry
            Trust.Ordered(munin);                 // and it is something done together
            // a figure under way is flown to its end first (ShoulderPin): the new order's time starts after it
            GameObject model = Shoulder.ModelOf(p, munin);
            ShoulderPin pin = model != null ? model.GetComponent<ShoulderPin>() : null;
            float figure = pin != null ? pin.FigureLeft : 0f;
            if (order == Chase && !munin) _chaseFor = Random.Range(ChaseMin, ChaseMax); // (Hugin is given it first: GiveBoth)
            float reach = 0f;
            if (order == Scout)
            {
                // how far: its training (every game flies it as far: on the player's ZDO with the order)
                reach = Strike.RangeOf(RavenSeat.On(p, munin));
                ZDO z = NetFlag.Zdo(p, true);
                if (z != null) z.Set(munin ? DistM : DistH, reach);
                Vector3 f = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up);
                ScoutDir[munin ? 1 : 0] = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            }
            bool leftChase = order != Chase && Of(p, munin) == Chase;
            Set(p, munin, order);
            if (leftChase) Return(p, !munin, false); // taken off the game by another order: no tag alone
            Outing.LastAt = Time.time; // it has just been out on your word: the director counts its own outings from here
            Until[munin ? 1 : 0] = Time.time + figure + (order == Follow || order == Circle ? LastSeconds : order == Scout ? 8f + reach / ScoutPace : order == Chase ? _chaseFor : order == Fetch ? RavenFetch.Seconds(p, munin) : TrickPlan.Seconds(order));
            Span[munin ? 1 : 0] = Until[munin ? 1 : 0] - Time.time;
            float wait = WaitOf(order);
            if (wait > 0f) ReadyAt[Slot(munin, order)] = Time.time + wait;
            Say(p, munin, Plugin.RavenName(munin) + ": " + Names[order]); // which order it took
            if (order != Chase && order != Fetch) Voice(p, munin, order); // and its own word on it (the chase: one of the two, GiveBoth; the fetching: none yet)
            return true;
        }

        // «Догонялки»: both ravens at once, or neither (each pays its own strength)
        internal static string WhyBoth(Player p, int order)
        {
            string h = Why(p, false, order), m = Why(p, true, order);
            if (h == null && m == null) return null;
            return (h != null ? Lang.F("Хугін: {0}", h) : "") + (h != null && m != null ? "; " : "") + (m != null ? Lang.F("Мунін: {0}", m) : "");
        }

        internal static bool GiveBoth(Player p, int order)
        {
            string why = WhyBoth(p, order);
            if (why != null) { Say(p, Why(p, false, order) == null, Names[order] + " — " + why); return false; } // (the picture of the one that will not)
            bool h = Give(p, false, order), m = h && Give(p, true, order);
            if (h && !m) { Return(p, false, false); return false; } // Munin would not («Свавільний»): no chase alone
            if (!h) return false;
            Voice(p, Random.value < 0.5f, order);
            return true;
        }

        // a catch in the chase (ChaseGame, your own ravens): now and then one of them has its say, the one that caught or
        // the one caught (chase_caught / chase_got), over itself in the air
        internal static void ChaseCaught(Player p, bool catcherMunin)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer) || Time.time - _chaseSaidAt < ChaseTalkGap || Random.value >= ChaseTalk) return;
            _chaseSaidAt = Time.time;
            bool caught = Random.value < 0.4f;
            Speak(p, caught ? !catcherMunin : catcherMunin, caught ? "chase_got" : "chase_caught", null, 1f);
        }

        // «Крила Одіна»: the raven flies at your back for the flight, free (no strength, no food, no word); the gift
        // ends it itself when you land
        internal static void FlyWith(Player p, bool munin, float seconds)
        {
            if (p == null) return;
            string who = munin ? Lang.T("Мунін") : Lang.T("Хугін");
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null || !Trust.Active(item) || Trust.Away(item)) { FlyNote(munin, Lang.F("{0} не летить за спину: {1}", who, (item == null ? Lang.T("не на плечі") : !Trust.Active(item) ? Lang.T("спить") : Trust.AwayWord(munin, item)))); return; }
            GameObject model = Shoulder.ModelOf(p, munin);
            ShoulderPin pin = model != null ? model.GetComponent<ShoulderPin>() : null;
            if (pin == null || !pin.enabled || AnimDirector.Of(model) > RavenAct.Command) { FlyNote(munin, Lang.F("{0} не летить за спину: {1}", who, (pin == null ? Lang.T("нема моделі") : !pin.enabled ? Lang.T("зайнятий") : Lang.F("зайнятий ({0})", AnimDirector.Of(model))))); return; }
            FlyNote(munin, Lang.F("{0} летить за спину", who));
            Set(p, munin, Follow);
            Until[munin ? 1 : 0] = Time.time + seconds;
            Span[munin ? 1 : 0] = seconds;
            Outing.LastAt = Time.time;
        }

        internal static void FlyWithEnd(Player p, bool munin)
        {
            if (p == null || Of(p, munin) != Follow) return;
            Set(p, munin, None);
            Until[munin ? 1 : 0] = 0f;
        }

        // into the flight's log, once per change (the wings ask again every half second)
        private static readonly string[] _flyNote = new string[2];
        private static void FlyNote(bool munin, string text) { int i = munin ? 1 : 0; if (_flyNote[i] == text) return; _flyNote[i] = text; PAnimLog.Event(text); }

        // «Повернись»: home now, free
        internal static void Return(Player p, bool munin) { Return(p, munin, true); }
        private static void Return(Player p, bool munin, bool speak)
        {
            if (p == null) return;
            int o = Of(p, munin);
            if (o == None) return;
            Set(p, munin, None); Until[munin ? 1 : 0] = 0f;
            Ended(p, munin, o, Lang.T("— повертаюсь на плече"));
            if (speak) Speak(p, munin, "order_return", null, 1f);
        }
        // both home: one of them says so (not two lines at once)
        internal static void ReturnBoth(Player p)
        {
            bool first = Random.value < 0.5f, spoken = false;
            foreach (bool munin in new[] { first, !first })
            {
                bool had = Of(p, munin) != None;
                Return(p, munin, !spoken);
                spoken |= had;
            }
        }

        // something else takes the raven now (Hugin's scouting): the order is over, and you are told why
        internal static void GiveWay(Player p, bool munin, string what)
        {
            int o = Of(p, munin);
            if (o == None) return;
            Set(p, munin, None); Until[munin ? 1 : 0] = 0f;
            if (!IsTrick(o)) Ended(p, munin, o, Lang.F("перервано — {0}", what));
            if (o == Chase) Return(p, !munin, false); // no tag alone
        }

        // a stronger act took the bird: the order is over, and you are told why
        internal static void Interrupted(GameObject model, RavenAct by)
        {
            ShoulderPin pin = model != null ? model.GetComponent<ShoulderPin>() : null;
            if (pin == null || !ReferenceEquals(pin.Player, Player.m_localPlayer)) return;
            int o = Of(pin.Player, pin.Munin);
            Set(pin.Player, pin.Munin, None);
            Until[pin.Munin ? 1 : 0] = 0f;
            if (o == None || IsTrick(o)) return; // a trick is over in a moment anyway
            if (o == Chase) Return(pin.Player, !pin.Munin, false); // no tag alone
            Ended(pin.Player, pin.Munin, o, Lang.F("перервано — {0}", (by == RavenAct.Strike ? Lang.T("лечу в атаку") : by == RavenAct.Dome ? Lang.T("кружляю над куполом") : by == RavenAct.FlyOff ? Lang.T("відлітаю") : by.ToString())));
        }

        // a line in the corner: which raven, which order, how it ended
        private static void Ended(Player p, bool munin, int o, string how)
        {
            if (p == null || o <= None || o >= Count) return;
            Say(p, munin, Plugin.RavenName(munin) + ": «" + Names[o] + "» " + how);
        }

        // From Shoulder.Tick (twice a second, the owner's game): ends on time or sooner when something happens (the scouting
        // ends itself where it turns: ScoutTurned)
        internal static void Tick()
        {
            if (Time.time >= _pinsUntil && Pins.Count > 0) ClearPins();
            Player p = Player.m_localPlayer;
            RavenFetch.Tick(p); // what was brought, into the bag once it is home
            if (p == null) return;
            string why = null;
            if (p.IsDead()) why = Lang.T("ти загину{в|ла}");
            else if (Outing.Indoors()) why = Lang.T("ти зайш{ов|ла} під дах");
            else if (Senses.Jumped) why = Lang.T("ти раптом далеко");
            else if (Senses.Swimming) why = Lang.T("ти плаваєш");
            else if (Senses.Sleeping) why = Lang.T("ти спиш");
            bool chaseSaid = false;
            for (int i = 0; i < 2; i++)
            {
                bool munin = i == 1;
                int o = Of(p, munin);
                if (o == None) continue;
                if (why == null && Time.time < Until[i]) continue;
                Set(p, munin, None);
                if (why != null)
                {
                    if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Raven anim: " + (munin ? "Munin" : "Hugin") + " order called off: " + why);
                    if (!p.IsDead()) Ended(p, munin, o, Lang.F("скасовано — {0}", why)); // (dead: the death says enough)
                }
                else if (o == Scout) Report(p, munin); // (never got there in time: what is ahead all the same)
                else if (o == Fetch) { RavenFetch.Missed(munin); Ended(p, munin, o, Lang.T("— не дістав, повертаюсь")); }
                else if (o == Chase) { if (!chaseSaid) Say(p, munin, Lang.F("{0} — повертаємось на плечі", Names[o])); chaseSaid = true; }
                else if (o == Follow || o == Circle) { Ended(p, munin, o, Lang.T("— повертаюсь на плече")); Speak(p, munin, "order_end", null, 0.6f); }
            }
        }

        // ---------------------------------------------------------------- the scouting
        private static readonly List<Minimap.PinData> Pins = new List<Minimap.PinData>();
        private static float _pinsUntil;

        // how far this scouting flies (every game: from the player's ZDO; the training's least if not there yet)
        internal static float ScoutReach(Player p, bool munin)
        {
            ZDO z = NetFlag.Zdo(p, false);
            return z != null ? z.GetFloat(munin ? DistM : DistH, Strike.Range) : Strike.Range;
        }

        // «Принести»: it has the thing in its beak (or it is gone): the order is over, home it flies (RavenFetch.Grab)
        internal static void Fetched(Player p, bool munin)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer) || Of(p, munin) != Fetch) return;
            Set(p, munin, None); Until[munin ? 1 : 0] = 0f;
        }

        // the raven is out there (ScoutRunPlan, the owner's game): it looks about, and the order is over (home it flies)
        internal static void ScoutTurned(Player p, bool munin)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer) || Of(p, munin) != Scout) return;
            Set(p, munin, None); Until[munin ? 1 : 0] = 0f;
            Report(p, munin);
        }

        // out there: the enemies and the ways under the ground ahead of you (along the way it flew), marked a while
        private static void Report(Player p, bool munin)
        {
            ClearPins();
            Vector3 me = p.transform.position, f = ScoutDir[munin ? 1 : 0];
            if (f == Vector3.zero) f = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
            int n = 0;
            string first = null;
            bool boss = false;
            float range = ScoutRange * Norov.ScoutMul(RavenSeat.On(p, munin)); // (its knack «Пильне око»)
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed() || !BaseAI.IsEnemy(p, c)) continue;
                Vector3 to = c.transform.position - me;
                if (to.sqrMagnitude > range * range || Vector3.Dot(to.normalized, f) < 0.3f) continue;
                string label = Localization.instance != null ? Lang.Game(c.m_name) : c.m_name;
                if (c.IsBoss()) { label = "‼ " + label; boss = true; }
                if (first == null || c.IsBoss()) first = label;
                n++;
                if (Minimap.instance != null)
                {
                    Minimap.PinData pin = Minimap.instance.AddPin(c.transform.position, Minimap.PinType.Shout, label, false, false, 0L, default(Splatform.PlatformUserID));
                    RavenSkills.Icon(pin, RavenSkills.Trophy(c));
                    if (c.IsBoss()) pin.m_animate = true;
                    Pins.Add(pin);
                }
            }
            string under = RavenSkills.PinDungeons(me, range, f, Pins);
            _pinsUntil = Time.time + ScoutMarkSeconds;
            string who = Plugin.RavenName(munin) + ": ";
            string seen = n == 0 ? Lang.T("попереду чисто") : Lang.F("попереду {0}{1}", (n == 1 ? first : Lang.F("{0} і ще {1}", first, (n - 1))), (boss ? Lang.T(". Обережно!") : ""));
            Say(p, munin, who + seen + (under != null ? Lang.F("{0}вхід під землю: {1}", (n == 0 ? Lang.T(", але ") : "; "), under) : "") + (n > 0 || under != null ? Lang.F(" (на карті {0} с)", Mathf.RoundToInt(ScoutMarkSeconds)) : "."));
        }

        private static void ClearPins()
        {
            if (Minimap.instance != null) foreach (Minimap.PinData pin in Pins) if (pin != null) Minimap.instance.RemovePin(pin);
            Pins.Clear();
        }
    }
}
#endif
