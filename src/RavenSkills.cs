#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // What the worn ravens do. The list of abilities is in Abilities.cs; they are called from the radial menu
    // (G -> Ravens, RavenRadial), from the optional keys (RavenKeys) and shown in the Book of ravens (RavenBook).
    // Hugin: thought, scouting, light, leap, and the last chance (passive, LastChance).
    // Munin: thought, scream, fury, wings, healing, and the blood fury (passive, BloodFury).
    internal static class RavenSkills
    {
        internal const string RpcSay = "RavenWatch_ShoulderSay";

        // cooldowns in seconds; a pair is "at level 1" and "at the top level" of trust, the rest shrink 4% a level; all rounded
        internal static float FuryCooldown = 2700f, FuryCooldownTop = 1500f;
        internal static float ThoughtCooldown = 5f;
        internal static float MuninThoughtCooldown = 5f;
        internal static float ScoutCooldown = 300f;
        internal static float ScoutSeconds = 30f;
        internal static float ScoutAwaySeconds = 12f;
        internal static float ScoutRevealRadius = 300f, ScoutRevealRadiusTop = 450f; // at trust level 1 and at the top level
        internal static float RevealRadius() { return Trust.Lerp(false, ScoutRevealRadius, ScoutRevealRadiusTop) * GiftUp.Power("scout") * Habits.ScoutMul(Trust.Worn(false)); } // (golden runes: level I)
        internal static float DungeonRadius() { return Trust.Lerp(false, ScoutDungeonRadius, ScoutDungeonRadiusTop) * GiftUp.Power("scout"); }
        internal static float ScoutOreRadius = 100f;
        internal static float ScoutOreMinutes = 5f;
        internal static float ScoutEnemyRadius = 100f;
        internal static float ScreamCooldown = 2400f, ScreamCooldownTop = 1500f;
        internal static float ScreamRadius = 6f;
        internal static float LightCooldown = 600f;
        internal static float WingsCooldown = 1200f, WingsCooldownTop = 600f;
        internal static float LeapCooldown = 1800f, LeapCooldownTop = 1440f;
        internal static float ShadowCooldown = 2400f, ShadowCooldownTop = 1200f;
        internal static float DomeCooldown = 600f, DomeCooldownTop = 420f;
        internal static float HealCooldown = 1800f, HealCooldownTop = 600f;
        internal static float WardCooldown = 600f, WardCooldownTop = 360f;
        internal static float FlightCooldown = 2400f, BeastCooldown = 1800f;
        private const float HearRange = 100f;
        internal static bool Animations = true;
        private static readonly FieldInfo ZAnim = AccessTools.Field(typeof(Character), "m_zanim");
        // how long words stay over the head: long enough to read them (about 20 letters a second), 6 to 18 s
        internal static float SaySeconds(string line) { return Mathf.Max(RavenSpeech.Seconds(line ?? "", RavenSpeech.Pace.Shout), RavenSpeech.ReadSeconds(line)); }

        private static readonly Dictionary<string, float> ReadyAt = new Dictionary<string, float>(); // ability id -> when it can be used again
        // kept in the character too (world time), so leaving and loading the world again does not make them ready
        private const string CooldownKey = "RavenWatch_cd_";
        private static float _scoutUntil, _scoutBackAt, _scoutRunesUntil;
        private static Vector3 _scoutFrom;
        private static float _nextPins;
        private static float _nextSync;
        internal static void SyncSoon() { _nextSync = 0f; } // the shoulders see a change at once (a raven called back)
        private static readonly List<Minimap.PinData> Pins = new List<Minimap.PinData>();
        private static readonly List<Minimap.PinData> OrePins = new List<Minimap.PinData>();
        private static float _orePinsUntil;
        private static string _oreSummary, _dungeonSummary;
        internal static float ScoutDungeonRadius = 300f, ScoutDungeonRadiusTop = 450f; // as far as the map opens (but only what is loaded around you can be seen)
        private static readonly Dictionary<string, Sprite> TrophyIcons = new Dictionary<string, Sprite>();

        private static readonly MethodInfo Explore = AccessTools.Method(typeof(Minimap), "Explore", new Type[] { typeof(Vector3), typeof(float) });

        private static void ForgetKeptCooldowns()
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.m_customData == null) return;
            List<string> keys = new List<string>();
            foreach (string k in p.m_customData.Keys) if (k.StartsWith(CooldownKey, StringComparison.Ordinal)) keys.Add(k);
            foreach (string k in keys) p.m_customData.Remove(k);
        }

        internal static void Reset()
        {
            ReadyAt.Clear(); // read again from the character when asked
            RavenAura.Reset();
            Satiety.Reset();
            Share.Reset();
            BondLog.Reset();
            Pack.Reset();
            People.Reset();
            Gathering.Reset();
            RavenPouch.Reset();
            Post.Reset();
            Fury.Reset();
            EndScout();
            ClearOre();
            RavenLight.Reset();
            WingsFx.Reset();
            Fear.Reset();
            Daze.Reset();
            Decoy.Reset();
            JointPlay.Reset();
            LastChance.Reset();
            Leap.Reset();
            Dome.Reset();
            Ward.Reset();
            OdinWings.Reset();
            Beast.Reset();
            BloodRevenge.Reset();
            Tame.Reset();
            Omen.Reset();
            PairGifts.Reset();
            ShipPlay.Reset();
            BeastPlay.Reset();
            BodyClips.Reset();
            Strike.ResetFights();
            Marks.Reset();
            Mastery.Reset();
            RavenFx.Forget();
            Shadow.Reset();
            Shadow.ResetLook();
            RavenBook.Close();
            Perks.Forget();
            Power.Reset();
            Shoulder.Reset();
            Twists.Reset();
        }

        // the radial menu and the keys
        internal static void Use(RavenAbility a)
        {
            Player p = Player.m_localPlayer;
            Plugin plugin = Plugin.Instance;
            if (a == null || a.Passive || p == null || plugin == null || p.IsDead()) return;
            if (a.Pair) { ArmPerch.Down(p, null); try { PairGifts.Use(p, a); } catch (Exception e) { RavenMsg.Center(p, Lang.F("{0}: щось пішло не так, дар не вдався.", a.Name)); plugin.LogError("Gift " + a.Id + " failed: " + e); } return; } // (both ravens: PairGifts)
            // pressed again while it goes on: it ends — before any other check, so a lit button always does what it shows (Ends)
            if (a.Id == "light" && RavenLight.On(p)) { RavenLight.Stop(p); if (Twists.Two("light")) Twists.Flash(p); return; } // the light again: it goes out (II: in a blinding flash)
            if (a.Id == "hflight" && OdinWings.Flying()) { OdinWings.Sink(p); return; } // the wings again: down gently
            if (a.Id == "mberserk" && Beast.On()) { Beast.End(p, false); return; } // the beast again: back to yourself now
            if (a.Id == "hdecoy" && Decoy.Mine()) { Decoy.Recall(p); return; } // Hugin gives it up and comes home
            if (a.Id == "mtame" && Tame.Mine()) { Tame.Recall(p); return; } // Munin leaves the beast and comes home
            if (a.Id == "dome" && Dome.Mine()) { Dome.Lower(); Animate(p, "comehere"); DomeSay("dome_back", Lang.T("Склав крила. Тепер знову разом.")); return; } // you call him back: a beckoning wave
            if (a.Id == "leap" && Leap.On(p) && !Leap.Stopping(p)) { Leap.Stop(p); RavenMsg.Center(p, Leap.On(p) ? Lang.T("Злет Хугіна скінчиться, щойно приземлишся") : Lang.T("Злет Хугіна зупинено")); return; }
            // every refusal is one list (Why); here it is said, with the raven's own word where it has one
            string mark; int dot, kind;
            string no = Why(a, p, out mark, out dot, out kind);
            if (no != null)
            {
                RavenMsg.Center(p, no);
                if (kind == WhyQuirk && a.Id == "scout" && Quirks.Sulk(p, a.Munin) == null) Quirks.SaySleepy(p, a.Munin); // (asleep, not sulking: its sleepy word)
                if (kind == WhyHabit) Habits.Whine(a.Munin);
                return;
            }
            ItemDrop.ItemData hugin, mun;
            RavenItem.Worn(p, out hugin, out mun);
            if (!Trust.Active(a.Munin ? mun : hugin)) { AsleepThought(p, a.Munin); return; } // (Why lets only the thought through asleep)
            // a gift that throws (an odd creature, another mod in the way) must not just do nothing: it says so, the log says why
            try
            {
                ShoulderLife.WakeUp(p, a.Munin); // (asked for a gift, it does not do it asleep)
                if (a.Id != "thought" && a.Id != "mthought") ArmPerch.GiftUsed(p, a.Munin); // (on your arm: off with it, or back to the shoulder for it)
                FightJudge.Acted(a.Munin); // (in a fight: the more active raven judges it)
                switch (a.Id)
                {
                    case "thought": Thought(plugin, p, false); break;
                    case "mthought": Thought(plugin, p, true); break;
                    case "scout": Scout(p); break;
                    case "light": LightAbility(p); break;
                    case "leap": LeapAbility(p); break;
                    case "shadow": ShadowAbility(p); break;
                    case "dome": DomeAbility(p, hugin); break;
                    case "hstrike": StrikeAbility(p, false, hugin); break;
                    case "mstrike": StrikeAbility(p, true, mun); break;
                    case "scream": Scream(p); break;
                    case "fury": FuryAbility(p); break;
                    case "wings": WingsAbility(p); break;
                    case "heal": HealAbility(p); break;
                    case "ward": WardAbility(p, mun); break;
                    case "hflight": FlightAbility(p); break;
                    case "hdecoy": DecoyAbility(p, hugin); break;
                    case "mberserk": BeastAbility(p); break;
                    case "mtame": TameAbility(p, mun); break;
                }
            }
            catch (Exception e)
            {
                RavenMsg.Center(p, Lang.F("{0}: щось пішло не так, дар не вдався.", a.Name));
                plugin.LogError("Gift " + a.Id + " failed: " + e);
            }
        }

        // Why a gift cannot be used now (null: it can) — the one list of a raven gift's refusals. Use says the long text in
        // the middle of the screen, Status shows the short one (mark, red) under the gift, UsableNow and Dot read it, and the
        // strike nobody pressed for (FreeStrike) asks it too: what the circle shows is what a press does. A new refusal goes
        // here, in its place in the order, and nowhere else (arch_check: gift-why).
        // Not in it: what goes on and a press ends (Ends, handled first by every caller), the wait and the strength (the
        // price, shown by Status, said by Ready and Power.Spend), what a gift needs at the moment of flying (a strike's aim,
        // «Боязкий» at a fresh boss, the decoy's target). The thought is let through asleep: it speaks as a stranger (AsleepThought).
        // kind: WhyQuirk / WhyHabit — the raven's nature refused, so Use adds its own word to it. dot: the book's dot.
        internal const int WhyPlain = 0, WhyQuirk = 1, WhyHabit = 2;
        private static string Red(string s) { return "<color=#FF5A5A><b>" + s + "</b></color>"; }

        // Why for the local player, once a frame per gift: Status, UsableNow and Dot ask the same gift in the same frame
        // (the circle, the book), and a refusal builds its texts; a press (Use) asks Why itself, fresh
        private sealed class WhyMemo { internal int Frame = -1; internal string Say, Mark; internal int Dot; }
        private static readonly Dictionary<string, WhyMemo> WhyMemos = new Dictionary<string, WhyMemo>();
        private static string WhyNow(RavenAbility a, out string mark, out int dot)
        {
            WhyMemo w;
            if (!WhyMemos.TryGetValue(a.Id, out w)) { w = new WhyMemo(); WhyMemos[a.Id] = w; }
            if (w.Frame != Time.frameCount)
            {
                int kind;
                w.Say = Why(a, Player.m_localPlayer, out w.Mark, out w.Dot, out kind);
                w.Frame = Time.frameCount;
            }
            mark = w.Mark; dot = w.Dot;
            return w.Say;
        }
        internal static string Why(RavenAbility a, Player p, out string mark, out int dot, out int kind)
        {
            mark = null; dot = DotWait; kind = WhyPlain;
            if (a == null) return null;
            if (p == null) { mark = ""; dot = DotNone; return ""; } // (no player: nothing can go)
            bool m = a.Munin;
            string name = Plugin.RavenName(m);
            string no = GiftBlock(a); // gifts out of place with the wings on, the beast awake, a pair's or a great gift going
            if (no != null) { mark = Red(OdinWings.InAir() ? Lang.T("не в польоті") : OdinWings.Flying() ? Lang.T("не з крилами Одіна") : PairGifts.Blocks(a) != null ? Lang.LowerFirst(PairGifts.Blocks(a)) : GreatBlock(a) != null ? Lang.LowerFirst(GreatBlock(a)) : Lang.T("не зі звіром")); return no; }
            ItemDrop.ItemData item = Trust.Worn(m);
            if (item == null) { dot = DotNone; mark = Red(Lang.T("не на тобі")); return Lang.F("{0} зараз не на тобі", name); }
            if (!Shoulder.Arrived(p, m)) { mark = Red(Lang.T("летить до тебе")); return Lang.F("{0} ще летить до тебе", name); } // just in the world: not before it sits
            // off on a flight of its own: nothing of that raven's until it is back on the shoulder (the gift that sent it calls it back: Ends)
            if (!m && Decoy.Mine()) { mark = Red(Lang.T("Хугін дражнить ворога")); return Lang.T("Хугін дражнить ворога — зараз повернеться"); }
            if (m && Tame.Mine()) { mark = Red(Lang.T("Мунін приборкує звіра")); return Lang.T("Мунін приборкує звіра.\nНатисни «Приборкання» ще раз — і він повернеться."); }
            if (Strike.Busy(p, m)) { mark = Red(Lang.T("в атаці · зараз повернеться")); return Lang.F("{0} в атаці — зараз повернеться", name); }
            // (an order under way: a gift that sends it flying takes it from there — a strike or the dome take the bird and
            // end the order, AnimDirector → Orders.Interrupted; the scouting ends it itself)
            if (!m && Scouting()) { mark = Red(Lang.F("на розвідці · повернеться за {0}", RavenAbilities.Span(ScoutLeft()))); return Lang.F("Хугін на розвідці.\nПовернеться за {0}.", RavenAbilities.Span(ScoutLeft())); }
            if (!m && Dome.Mine()) { mark = Red(Lang.T("Хугін тримає купол")); return Lang.T("Хугін тримає над тобою купол.\nНатисни «Під крилом» ще раз — і він повернеться."); }
            if (Trust.Away(item)) { dot = DotNone; mark = Red(Perches.On(item) && Trust.AwayLeft(item) <= 0f ? Trust.BackIn(item) : Trust.AwayWhy(item) == Post.Why ? Trust.AwayWord(m, item) : Trust.AwayWord(m, item) + ", " + Trust.BackIn(item)); return Trust.AwayText(m, item); }
            if (a.Passive) return null; // (a passive gift works whatever its mood: it only waits for the raven to be here)
            bool thought = a.Id == "thought" || a.Id == "mthought";
            if (!Trust.Active(item))
            {
                if (thought) return null; // (asleep it still talks, as a stranger: AsleepThought)
                dot = DotNone;
                mark = Red(m ? Lang.T("Мунін спить і клав на тебе — спершу обряд") : Lang.T("Хугін спить — дар відкриється після обряду"));
                return m ? Lang.F("Мунін спить і клав на тебе.\nНеси {0} до каменів.", Trust.WakeText()) : Lang.F("Хугін спить і тебе не чує.\nПринеси {0} до каменів.", Trust.WakeText());
            }
            if (!thought && Trust.Hostile(item)) { dot = DotNone; mark = Red(Lang.T("чужий ворон · не слухає")); return Lang.F("{0} не твій — його дари не для тебе", name); } // (a stranger's raven only talks, and rudely)
            if (!RavenAbilities.Unlocked(a)) { dot = DotNone; mark = RavenAbilities.LockedText(a); return Lang.F("{0}: відкриється на {1}-му рівні довіри", a.Name, RavenAbilities.UnlockLevel(a)); }
            no = Quirks.Blocks(p, m, a.Id); // its quirk: sulking (nothing), asleep at night (no scouting)
            if (no != null) { kind = WhyQuirk; mark = Red(no); return no + "."; }
            no = Habits.Blocks(p, a); // «Хатній»: just away from home, two gifts it will not give
            if (no != null) { kind = WhyHabit; mark = Red(Lang.F("хоче додому («Хатній») · ще {0}", RavenAbilities.Span(Habits.HomeLeft(p, m)))); return no; }
            // the place you stand in (cheap: flags and Senses)
            switch (a.Id)
            {
                case "hstrike": case "mstrike": no = Strike.Refused(p); break;
                case "dome": no = Dome.Refused(p); break;
                case "mberserk": no = Beast.Refused(p); break;
                case "hdecoy": no = Decoy.Refused(p); break;
                case "hflight": no = OdinWings.Refused(p); break;
            }
            if (no != null) { mark = Red(Lang.LowerFirst(no)); return no; }
            return null;
        }

        // "−10 сили (є 40) · готова" / "… · ще 2 хв 30 с": under an ability in the radial menu, in the book and tooltips
        internal static string Status(RavenAbility a)
        {
            if (a.Pair) return PairGifts.Status(a);
            if (a.Id == "light" && RavenLight.On(Player.m_localPlayer)) return Lang.T("<color=#FFD54F><b>світляк світить · натисни, щоб погасити</b></color>"); // the press always works
            if (a.Id == "leap" && Leap.On(Player.m_localPlayer)) return Leap.Stopping(Player.m_localPlayer) ? Lang.T("<color=#FFD54F><b>скінчиться, щойно приземлишся</b></color>") : Lang.T("<color=#FFD54F><b>злет діє · натисни, щоб зупинити</b></color>");
            if (a.Id == "mberserk" && Beast.On()) return Lang.F("<color=#FFD54F><b>звір лютує · ще {0} · натисни, щоб вгамувати · потім втома</b></color>", RavenAbilities.Span(Beast.Left()));
            if (a.Id == "hflight" && OdinWings.Flying()) return "<color=#FFD54F><b>" + (OdinWings.Sinking() ? Lang.T("опускаєшся") : OdinWings.OnGround() ? Lang.F("на землі · пробіл — злетіти · ще {0} · натисни, щоб завершити", RavenAbilities.Span(OdinWings.Left())) : Lang.F("летиш · ще {0} · натисни, щоб сісти", RavenAbilities.Span(OdinWings.Left()))) + "</b></color>";
            // what goes on now and a press ends or calls back (Ends): yellow, before any refusal
            if (a.Munin && a.Id == "mtame" && Tame.Mine()) return Lang.F("<color=#FFD54F><b>приборкує звіра · ще {0} · натисни, щоб повернути</b></color>", RavenAbilities.Span(Tame.Left()));
            if (!a.Munin && a.Id == "hdecoy" && Decoy.Mine()) return Lang.F("<color=#FFD54F><b>дражнить ворога · ще {0} · натисни, щоб повернути</b></color>", RavenAbilities.Span(Decoy.Left()));
            if (!a.Munin && a.Id == "dome" && Dome.Mine()) return Lang.T("<color=#FFD54F><b>купол стоїть · натисни, щоб повернути Хугіна</b></color>");
            if (a.Id == "ward" && Ward.Mine()) return Lang.F("<color=#FFD54F><b>заслона стоїть · ще {0}{1}</b></color>", RavenAbilities.Span(Ward.Left()), (Ward.MaxCharges() > 1 ? Lang.F(", витримає ще {0}", (Ward.Charges() == 4 ? Lang.T("чотири удари") : Ward.Charges() == 3 ? Lang.T("три удари") : Ward.Charges() == 2 ? Lang.T("два удари") : Lang.T("один удар"))) : ""));
            if (RavenAbilities.Stub(a)) return Lang.T("<color=#9a9a9a>ще в роботі</color>");
            ItemDrop.ItemData raven = Trust.Worn(a.Munin);
            // a refusal: its short word, the same list the press says in full (Why; a passive one: only where the raven is)
            string mark; int dot;
            if (WhyNow(a, out mark, out dot) != null) return mark;
            if (RavenAbilities.Free(a)) return RavenAbilities.Unlocked(a) ? Lang.F("без сили · {0}", RavenAbilities.PassiveReady(a)) : RavenAbilities.LockedText(a);
            if (a.Passive) return RavenAbilities.Unlocked(a) ? Lang.F("−{0} сили{1} (є {2}) · {3}", Mathf.RoundToInt(a.Cost()), Satiety.CostNote(a.Munin), Power.Current(a.Munin), RavenAbilities.PassiveReady(a)) : RavenAbilities.LockedText(a);
            if (raven != null && !Trust.Active(raven)) // (only the thought gets here asleep)
                return Lang.F("без сили, ворон ще чужий · {0}", RavenAbilities.ReadyMark(Left(AsleepId(a.Munin)), Lang.T("готовий")));
            float left = Left(a.Id);
            string cost = Lang.F("−{0} сили{1} (є {2}) · ", Mathf.RoundToInt(a.Cost()), Satiety.CostNote(a.Munin), Power.Current(a.Munin));
            if (left <= 0f && Power.Current(a.Munin) < Mathf.RoundToInt(a.Cost()))
                return cost + "<color=#FF5A5A><b>" + (a.Munin ? Lang.T("бракує сили — йди когось убий") : Lang.T("бракує сили — дай Хугіну перепочити")) + "</b></color>";
            return cost + RavenAbilities.ReadyMark(left, Lang.T("готовий"));
        }

        // can it be used this very moment (for the radial menu: the ones that cannot are drawn dim)
        // it goes on now and a press ends it (light, wings, beast, decoy, dome, leap, the pair's): lit, its cooldown not shown over it
        internal static bool Ends(RavenAbility a)
        {
            Player p = Player.m_localPlayer;
            if (a == null || a.Passive || p == null) return false;
            if (a.Pair) return PairGifts.Ends(a);
            switch (a.Id)
            {
                case "light": return RavenLight.On(p);
                case "hflight": return OdinWings.Flying() && !OdinWings.Sinking();
                case "mberserk": return Beast.On();
                case "hdecoy": return Decoy.Mine();
                case "mtame": return Tame.Mine();
                case "dome": return Dome.Mine();
                case "leap": return Leap.On(p) && !Leap.Stopping(p);
            }
            return false;
        }

        internal static bool UsableNow(RavenAbility a)
        {
            if (a == null || a.Passive) return false;
            if (Ends(a)) return true; // pressing it ends it
            if (a.Pair) return PairGifts.Usable(a);
            if (a.Id == "hflight" && OdinWings.Flying()) return false; // (already coming down)
            if (RavenAbilities.Stub(a)) return false;
            string mark; int dot;
            if (WhyNow(a, out mark, out dot) != null) return false;
            if (!Trust.Active(Trust.Worn(a.Munin))) return Left(AsleepId(a.Munin)) <= 0f; // (only the thought gets here asleep)
            return Left(a.Id) <= 0f && Power.Current(a.Munin) >= Mathf.RoundToInt(a.Cost());
        }

        // the book's coloured dot on a gift: 0 none (passive, asleep, away, not worn), 1 ready, 2 waiting, 3 short of strength
        internal const int DotNone = 0, DotReady = 1, DotWait = 2, DotWeak = 3;
        // the raven has left the shoulder for a moment (a strike, scouting, over the dome, an outing of its own — a hunt, a fish —
        // or sitting somewhere near on a visit): nobody to feed, no ritual; why says what it is doing ("в атаці", "на розвідці ще
        // 40 с", "тримає купол", "на полюванні"). The user 2026-11-05: a raven off hunting could be fed
        internal static bool OffShoulder(Player p, bool munin, out string why)
        {
            why = null;
            if (p != null && Strike.Busy(p, munin)) why = Lang.T("в атаці");
            else if (Orders.Busy(p, munin)) why = Orders.BusyText(p, munin);
            else if (!munin && Scouting()) why = Lang.F("на розвідці, ще {0}", RavenAbilities.Span(ScoutLeft()));
            else if (!munin && Dome.Mine()) why = Lang.T("тримає купол");
            else if (p != null && Outing.Of(p, munin) != Outing.None) why = OutingWhy(Outing.Of(p, munin));
            else if (WorldVisit.Active(munin)) why = Lang.T("сидить неподалік");
            return why != null;
        }

        private static string OutingWhy(int mode)
        {
            if (mode == Outing.Hunt) return Lang.T("на полюванні");
            if (mode == Outing.Fish) return Lang.T("ловить рибу");
            if (mode == Outing.Scout) return Lang.T("оглядає місцину");
            return Lang.T("літає довкола");
        }

        internal static int Dot(RavenAbility a)
        {
            if (a == null || a.Passive || RavenAbilities.Stub(a)) return DotNone;
            if (Ends(a)) return DotReady; // pressing it ends it
            if (a.Pair) return PairGifts.Dot(a);
            if (a.Id == "hflight" && OdinWings.Flying()) return DotWait; // (already coming down)
            string mark; int dot;
            if (WhyNow(a, out mark, out dot) != null) return dot;
            if (!Trust.Active(Trust.Worn(a.Munin))) return DotNone; // (the thought asleep: no dot, as before)
            if (Left(a.Id) > 0f) return DotWait;
            return Power.Current(a.Munin) < Mathf.RoundToInt(a.Cost()) ? DotWeak : DotReady;
        }

        internal static void ResetCooldowns() { ReadyAt.Clear(); ForgetKeptCooldowns(); LastChance.ResetCooldown(); BloodFury.ResetCooldown(); WeakPassive.ResetCooldown(); Trust.ResetFeeding(); Trust.ResetAway(); RavenSeat.Reset(Player.m_localPlayer); }

        internal static string CooldownReport()
        {
            List<string> parts = new List<string>();
            foreach (RavenAbility a in RavenAbilities.All)
            {
                if (a.Passive) continue;
                float left = Left(a.Id);
                parts.Add(a.Name + " " + (left <= 0f ? Lang.T("готова") : RavenAbilities.Span(left)));
            }
            return Lang.F("Перезарядки: {0}. Останній шанс: {1}. Кривава лють: {2}", string.Join(", ", parts.ToArray()), LastChance.Status(), BloodFury.Status());
        }

        internal static float LeftOf(string id) { return Left(id); } // its wait still to go (the gifts circle)
        private static float Left(string id)
        {
            float at;
            if (!ReadyAt.TryGetValue(id, out at))
            {
                Player p = Player.m_localPlayer;
                float kept = p != null ? WorldTime.Left(p.m_customData, CooldownKey + id) : 0f;
                at = Time.time + Mathf.Max(0f, kept);
                ReadyAt[id] = at;
            }
            return at - Time.time;
        }

        // how long an ability waits now, with the trust of the worn raven
        internal static float CooldownSeconds(string id) { return Mathf.Round(CooldownBase(id) * GiftUp.CooldownMul(id) * RuneStars.CooldownMulOf(id) * Habits.CooldownMul(id)); } // (golden runes: level II; «Попіл»)

        private static float CooldownBase(string id)
        {
            switch (id)
            {
                case "thought": return Trust.Sooner(false, Mathf.Max(1f, ThoughtCooldown));
                case "mthought": return Trust.Sooner(true, Mathf.Max(1f, MuninThoughtCooldown));
                case "scout": return Trust.Sooner(false, ScoutCooldown);
                case "light": return Trust.Sooner(false, LightCooldown);
                case "leap": return Trust.RoundTime(Trust.LerpGift("leap", LeapCooldown, LeapCooldownTop));
                case "shadow": return Trust.RoundTime(Trust.LerpGift("shadow", ShadowCooldown, ShadowCooldownTop));
                case "dome": return Trust.RoundTime(Trust.LerpGift("dome", DomeCooldown, DomeCooldownTop) * GearFx.GuardCooldownMul(false)); // (Спіраль захисту)
                case "hstrike": return Trust.Sooner(false, Strike.HuginCooldown * Norov.CooldownMul(Trust.Worn(false)) * GearFx.StrikeCooldownMul(Trust.Worn(false))); // (its knack «Швидке крило», «Кігті комара»)
                case "mstrike": return Trust.Sooner(true, Strike.MuninCooldown * Norov.CooldownMul(Trust.Worn(true)) * GearFx.StrikeCooldownMul(Trust.Worn(true)));
                case "scream": return Trust.RoundTime(Trust.LerpGift("scream", ScreamCooldown, ScreamCooldownTop));
                case "fury": return Trust.RoundTime(Trust.LerpGift("fury", FuryCooldown, FuryCooldownTop));
                case "wings": return Trust.RoundTime(Trust.LerpGift("wings", WingsCooldown, WingsCooldownTop));
                case "heal": return Trust.RoundTime(Trust.LerpGift("heal", HealCooldown, HealCooldownTop) * GearFx.GuardCooldownMul(true));
                case "hflight": return Trust.RoundTime(FlightCooldown);
                case "hdecoy": return Trust.RoundTime(Trust.LerpFrom(false, RavenAbilities.UnlockLevel(RavenAbilities.Find("hdecoy")), Decoy.Cooldown, Decoy.CooldownTop)); // (from the gift's own first level)
                case "mberserk": return Trust.RoundTime(BeastCooldown);
                case "mtame": return Trust.RoundTime(Trust.LerpGift("mtame", Tame.CooldownMinutes * 60f, Tame.CooldownMinutesTop * 60f));
                case "ward": return Trust.RoundTime(Trust.LerpGift("ward", WardCooldown, WardCooldownTop) * GearFx.GuardCooldownMul(true)); // (Спіраль захисту)
                case "pwhirl": return Trust.RoundTime(Whirl.Cooldown);
                case "pload": return Trust.RoundTime(Load.Cooldown);
                case "pclear": return Trust.RoundTime(ClearSky.Cooldown);
            }
            return 5f;
        }

        // the pause made longer by this much (a quirk: «Задавака» shows off before the next strike)
        internal static void Delay(string id, float seconds)
        {
            float left = Left(id) + Mathf.Max(0f, seconds);
            ReadyAt[id] = Time.time + left;
            Player p = Player.m_localPlayer;
            if (p != null) WorldTime.Set(p.m_customData, CooldownKey + id, left);
        }

        // a share of the whole pause let off (a strike that found nobody); what is left of it stays
        internal static void LetOff(string id, float share)
        {
            float left = Left(id) - Mathf.Max(1f, CooldownSeconds(id)) * Mathf.Clamp01(share);
            ReadyAt[id] = Time.time + Mathf.Max(0f, left);
            Player p = Player.m_localPlayer;
            if (p != null) WorldTime.Set(p.m_customData, CooldownKey + id, Mathf.Max(0f, left));
        }

        internal static void StartCooldown(string id) { Cooldown(id); } // (the paired gifts: PairGifts)

        private static void Cooldown(string id)
        {
            float seconds = Mathf.Max(1f, CooldownSeconds(id));
            ReadyAt[id] = Time.time + seconds;
            Player p = Player.m_localPlayer;
            if (p != null) WorldTime.Set(p.m_customData, CooldownKey + id, seconds);
        }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcSay, new Action<long, ZPackage>(OnSay));
            RavenTalk.Register();
            Victim.Register();
            Breeds.Register();
            Bonds.Register();
            NearCast.Register();
            Pack.Register();
            JointPlay.Register();
            CrossTalk.Register();
            PassTalk.Register();
            Fury.Register();
            LeapFx.Register();
            RitualFx.Register();
            ScreamFx.Register();
            LastChance.Register();
            BloodFury.Register();
            WingsFx.Register();
            Shadow.Register();
            ShoulderLife.Register();
            GearDress.Register();
            Fear.Register();
            Daze.Register();
            WorldVisit.Register();
            GraveRavens.Register();
            ChaseGame.Register();
            Share.Register();
            People.Register();
            Gathering.Register();
            Decoy.Register();
            Champions.Register();
            BindRun.Register();
            BindFx.Register();
            Facets.Register();
            Twists.Register();
            ErrandNet.Register();
            Post.Register();
            RavenAura.Register();
            Dome.Register();
            Ward.Register();
            OdinWings.Register();
            Beast.Register();
            BodyClips.Register();
            Strike.Register();
            Marks.Register();
            SiegeNet.Register();
            ShipPlay.Register();
            BloodRevenge.Register();
            Tame.Register();
            OmenFate.Register();
            KillCredit.Register();
#if RAVEN_ITEM && RAVEN_NPC
            NpcHeart.Register();
#endif
        }

        // from the plugin's Update
        // each step of the frame on its own: one failing (an odd creature, another mod) must not stop the rest every frame
        private static readonly Dictionary<string, float> Faulted = new Dictionary<string, float>();
        private static void Fault(string step, Exception e)
        {
            float last;
            if (Faulted.TryGetValue(step, out last) && Time.time - last < 300f) return; // once in 5 minutes per step
            Faulted[step] = Time.time;
            if (Plugin.Instance != null) Plugin.Instance.LogError("Raven update step " + step + " failed (the rest goes on): " + e);
        }

        internal static void Update(Plugin plugin)
        {
            { long ts = Perf.On ? Perf.Now : 0; try { RavenItem.TickIcons(); } catch (Exception e) { Fault("RavenItem.TickIcons", e); } if (Perf.On) Perf.Step("RavenItem.TickIcons", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Shoulder.Tick(); } catch (Exception e) { Fault("Shoulder.Tick", e); } if (Perf.On) Perf.Step("Shoulder.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Slow.Frame(); } catch (Exception e) { Fault("Slow.Frame", e); } if (Perf.On) Perf.Step("Slow.Frame", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { RavenLight.Tick(); } catch (Exception e) { Fault("RavenLight.Tick", e); } if (Perf.On) Perf.Step("RavenLight.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { WingsFx.Tick(); } catch (Exception e) { Fault("WingsFx.Tick", e); } if (Perf.On) Perf.Step("WingsFx.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { RavenBook.Update(); } catch (Exception e) { Fault("RavenBook.Update", e); } if (Perf.On) Perf.Step("RavenBook.Update", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Facets.Frame(); } catch (Exception e) { Fault("Facets.Frame", e); } if (Perf.On) Perf.Step("Facets.Frame", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Twists.Frame(); } catch (Exception e) { Fault("Twists.Frame", e); } if (Perf.On) Perf.Step("Twists.Frame", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { RavenKeys.Update(); } catch (Exception e) { Fault("RavenKeys.Update", e); } if (Perf.On) Perf.Step("RavenKeys.Update", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { DropLook.Tick(); } catch (Exception e) { Fault("DropLook.Tick", e); } if (Perf.On) Perf.Step("DropLook.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Shadow.TickOthers(); } catch (Exception e) { Fault("Shadow.TickOthers", e); } if (Perf.On) Perf.Step("Shadow.TickOthers", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Dome.Tick(); } catch (Exception e) { Fault("Dome.Tick", e); } if (Perf.On) Perf.Step("Dome.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Ward.Tick(); } catch (Exception e) { Fault("Ward.Tick", e); } if (Perf.On) Perf.Step("Ward.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { OdinWings.Tick(); } catch (Exception e) { Fault("OdinWings.Tick", e); } if (Perf.On) Perf.Step("OdinWings.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Decoy.Tick(); } catch (Exception e) { Fault("Decoy.Tick", e); } if (Perf.On) Perf.Step("Decoy.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Beast.Tick(); } catch (Exception e) { Fault("Beast.Tick", e); } if (Perf.On) Perf.Step("Beast.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { PairGifts.Tick(); } catch (Exception e) { Fault("PairGifts.Tick", e); } if (Perf.On) Perf.Step("PairGifts.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { BodyClips.Tick(); } catch (Exception e) { Fault("BodyClips.Tick", e); } if (Perf.On) Perf.Step("BodyClips.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Marks.Tick(); } catch (Exception e) { Fault("Marks.Tick", e); } if (Perf.On) Perf.Step("Marks.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Studio.Tick(); } catch (Exception e) { Fault("Studio.Tick", e); } if (Perf.On) Perf.Step("Studio.Tick", ts); } // (asleep without its folder)
#if RAVEN_STORY
            { long ts = Perf.On ? Perf.Now : 0; try { StoryPanel.Tick(); } catch (Exception e) { Fault("StoryPanel.Tick", e); } if (Perf.On) Perf.Step("StoryPanel.Tick", ts); } // (the story window: F9, asleep without the blueprints folder)
            { long ts = Perf.On ? Perf.Now : 0; try { StoryQuests.Tick(); } catch (Exception e) { Fault("StoryQuests.Tick", e); } if (Perf.On) Perf.Step("StoryQuests.Tick", ts); } // (the story's track: the quests' stages, twice a second)
#endif
#if RAVEN_NPC
            { long ts = Perf.On ? Perf.Now : 0; try { LocPanel.Tick(); LocPreview.Tick(); } catch (Exception e) { Fault("LocPanel.Tick", e); } if (Perf.On) Perf.Step("LocPanel.Tick", ts); } // (the Hall's window and the author's look at its states: asleep without the blueprints folder)
#endif
            { long ts = Perf.On ? Perf.Now : 0; try { Pet.Tick(); } catch (Exception e) { Fault("Pet.Tick", e); } if (Perf.On) Perf.Step("Pet.Tick", ts); } // (standing a while: a stroke for a raven now and then)
            { long ts = Perf.On ? Perf.Now : 0; try { Gestures.Tick(); Fireside.Tick(); } catch (Exception e) { Fault("Gestures.Tick", e); } if (Perf.On) Perf.Step("Gestures.Tick", ts); } // (a waiting gesture; the fireside: your head on the teller, palms to the fire)
            { long ts = Perf.On ? Perf.Now : 0; try { ArmPerch.Tick(); } catch (Exception e) { Fault("ArmPerch.Tick", e); } if (Perf.On) Perf.Step("ArmPerch.Tick", ts); } // (a raven on your arm: you first, it hops off)
            { long ts = Perf.On ? Perf.Now : 0; try { Mead.Tick(); } catch (Exception e) { Fault("Mead.Tick", e); } if (Perf.On) Perf.Step("Mead.Tick", ts); } // (a raven tipsy on your mead)
            { long ts = Perf.On ? Perf.Now : 0; try { Mastery.Tick(Player.m_localPlayer); } catch (Exception e) { Fault("Mastery.Tick", e); } if (Perf.On) Perf.Step("Mastery.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { if (_scoutUntil > 0f) TickScout(); } catch (Exception e) { Fault("TickScout", e); } if (Perf.On) Perf.Step("TickScout", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { if (_orePinsUntil > 0f && Time.time >= _orePinsUntil) ClearOre(); } catch (Exception e) { Fault("ClearOre", e); } if (Perf.On) Perf.Step("ClearOre", ts); }
            Player p = Player.m_localPlayer;
            if (p == null) return;
            { long ts = Perf.On ? Perf.Now : 0; try { ShipPlay.Frame(); } catch (Exception e) { Fault("ShipPlay.Frame", e); } if (Perf.On) Perf.Step("ShipPlay.Frame", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Leap.Tick(p); } catch (Exception e) { Fault("Leap.Tick", e); } if (Perf.On) Perf.Step("Leap.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { Shadow.Tick(p); } catch (Exception e) { Fault("Shadow.Tick", e); } if (Perf.On) Perf.Step("Shadow.Tick", ts); }
            { long ts = Perf.On ? Perf.Now : 0; try { WeakPassive.Tick(p); } catch (Exception e) { Fault("WeakPassive.Tick", e); } if (Perf.On) Perf.Step("WeakPassive.Tick", ts); }

            ItemDrop.ItemData hugin, munin;
            RavenItem.Worn(p, out hugin, out munin);
            if (Time.time >= _nextSync)
            {
                _nextSync = Time.time + 0.5f;
                { long ts = Perf.On ? Perf.Now : 0; try { RavenItem.KeepSingle(p); } catch (Exception e) { Fault("RavenItem.KeepSingle", e); } if (Perf.On) Perf.Step("RavenItem.KeepSingle", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Perks.Sync(p); } catch (Exception e) { Fault("Perks.Sync", e); } if (Perf.On) Perf.Step("Perks.Sync", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { PerkFx.Tick(p); } catch (Exception e) { Fault("PerkFx.Tick", e); } if (Perf.On) Perf.Step("PerkFx.Tick", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Power.Tick(p); } catch (Exception e) { Fault("Power.Tick", e); } if (Perf.On) Perf.Step("Power.Tick", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Perks.ShoulderName(p); } catch (Exception e) { Fault("Perks.ShoulderName", e); } if (Perf.On) Perf.Step("Perks.ShoulderName", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Strike.Watch(p); } catch (Exception e) { Fault("Strike.Watch", e); } if (Perf.On) Perf.Step("Strike.Watch", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { ShipPlay.Tick(p); } catch (Exception e) { Fault("ShipPlay.Tick", e); } if (Perf.On) Perf.Step("ShipPlay.Tick", ts); } // «За кормою»: a play behind the ship now and then
                { long ts = Perf.On ? Perf.Now : 0; try { EndOrphaned(p, hugin, munin); } catch (Exception e) { Fault("EndOrphaned", e); } if (Perf.On) Perf.Step("EndOrphaned", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Perches.Tick(p); } catch (Exception e) { Fault("Perches.Tick", e); } if (Perf.On) Perf.Step("Perches.Tick", ts); } // ravens on perches: strength, sent off, called back
                try
                {
                    Shoulder.Publish(p, hugin != null && !Trust.Away(hugin), // over the dome he is still "on you": his own model flies it (DomeRaven)
                        munin != null && !Trust.Away(munin), // a raven away is not drawn
                        hugin != null ? Trust.ShownLevel(false, hugin) : 0, munin != null ? Trust.ShownLevel(true, munin) : 0);
                }
                catch (Exception e) { Fault("Shoulder.Publish", e); }
                { long ts = Perf.On ? Perf.Now : 0; try { RavenLight.Publish(p); } catch (Exception e) { Fault("RavenLight.Publish", e); } if (Perf.On) Perf.Step("RavenLight.Publish", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { WingsFx.Publish(p); } catch (Exception e) { Fault("WingsFx.Publish", e); } if (Perf.On) Perf.Step("WingsFx.Publish", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Shadow.Publish(p); } catch (Exception e) { Fault("Shadow.Publish", e); } if (Perf.On) Perf.Step("Shadow.Publish", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Fury.Publish(p); } catch (Exception e) { Fault("Fury.Publish", e); } if (Perf.On) Perf.Step("Fury.Publish", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Outfits.Publish(p, hugin, munin); } catch (Exception e) { Fault("Outfits.Publish", e); } if (Perf.On) Perf.Step("Outfits.Publish", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { RavenWear.Publish(p, hugin, munin); } catch (Exception e) { Fault("RavenWear.Publish", e); } if (Perf.On) Perf.Step("RavenWear.Publish", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Norov.Publish(p, hugin, munin); } catch (Exception e) { Fault("Norov.Publish", e); } if (Perf.On) Perf.Step("Norov.Publish", ts); } // each raven's character: every game flies it so
                { long ts = Perf.On ? Perf.Now : 0; try { Voice.Publish(p, hugin, munin); } catch (Exception e) { Fault("Voice.Publish", e); } if (Perf.On) Perf.Step("Voice.Publish", ts); } // and its own voice and manner
                { long ts = Perf.On ? Perf.Now : 0; try { Gossip.Publish(p, hugin, munin); } catch (Exception e) { Fault("Gossip.Publish", e); } if (Perf.On) Perf.Step("Gossip.Publish", ts); } // and a few fresh facts for the others' ravens to gossip about
                { long ts = Perf.On ? Perf.Now : 0; try { RavenSeat.Tick(p); } catch (Exception e) { Fault("RavenSeat.Tick", e); } if (Perf.On) Perf.Step("RavenSeat.Tick", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Loyal.Tick(p); } catch (Exception e) { Fault("Loyal.Tick", e); } if (Perf.On) Perf.Step("Loyal.Tick", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Tame.Tick(p); } catch (Exception e) { Fault("Tame.Tick", e); } if (Perf.On) Perf.Step("Tame.Tick", ts); } // Munin's «Приборкання»: on the beast
                { long ts = Perf.On ? Perf.Now : 0; try { BloodRevenge.Tick(p); } catch (Exception e) { Fault("BloodRevenge.Tick", e); } if (Perf.On) Perf.Step("BloodRevenge.Tick", ts); } // Munin's «Кровна помста»: the marked killer
                { long ts = Perf.On ? Perf.Now : 0; try { Omen.Tick(p); Omen.TickLate(p); } catch (Exception e) { Fault("Omen.Tick", e); } if (Perf.On) Perf.Step("Omen.Tick", ts); } // Hugin's «Віщування»: the morning, the sea
                { long ts = Perf.On ? Perf.Now : 0; try { RavenTalk.Tick(p); } catch (Exception e) { Fault("RavenTalk.Tick", e); } if (Perf.On) Perf.Step("RavenTalk.Tick", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { EmoteTalk.Tick(p); } catch (Exception e) { Fault("EmoteTalk.Tick", e); } if (Perf.On) Perf.Step("EmoteTalk.Tick", ts); } // a word about your dance, bow, wave...
                { long ts = Perf.On ? Perf.Now : 0; try { Victim.Tick(p); } catch (Exception e) { Fault("Victim.Tick", e); } if (Perf.On) Perf.Step("Victim.Tick", ts); } // «Жертва»: a spell of mockery now and then
                { long ts = Perf.On ? Perf.Now : 0; try { Breeds.Tick(p); } catch (Exception e) { Fault("Breeds.Tick", e); } if (Perf.On) Perf.Step("Breeds.Tick", ts); } // a raven's breed: rolled once, shown when it wakes
                { long ts = Perf.On ? Perf.Now : 0; try { BreedTraits.Tick(p); } catch (Exception e) { Fault("BreedTraits.Tick", e); } if (Perf.On) Perf.Step("BreedTraits.Tick", ts); } // its trait's senses and its own words
                { long ts = Perf.On ? Perf.Now : 0; try { FightJudge.Tick(p); } catch (Exception e) { Fault("FightJudge.Tick", e); } if (Perf.On) Perf.Step("FightJudge.Tick", ts); } // «Суддя бою»: a fight worth it over, judged
                { long ts = Perf.On ? Perf.Now : 0; try { NewGear.Tick(p); } catch (Exception e) { Fault("NewGear.Tick", e); } if (Perf.On) Perf.Step("NewGear.Tick", ts); } // «Оглядає обнову»: a thing never worn before, looked over
                { long ts = Perf.On ? Perf.Now : 0; try { Habits.Tick(p); } catch (Exception e) { Fault("Habits.Tick", e); } if (Perf.On) Perf.Step("Habits.Tick", ts); } // «Звички»: the day's doings counted, weighed at dawn
                { long ts = Perf.On ? Perf.Now : 0; try { Bath.Tick(p); } catch (Exception e) { Fault("Bath.Tick", e); } if (Perf.On) Perf.Step("Bath.Tick", ts); } // «Купання»: the dirt, the shake on you after
                { long ts = Perf.On ? Perf.Now : 0; try { DirtFx.Tick(); } catch (Exception e) { Fault("DirtFx.Tick", e); } if (Perf.On) Perf.Step("DirtFx.Tick", ts); } // a dirty raven shows it, yours and theirs
                { long ts = Perf.On ? Perf.Now : 0; try { BeastPlay.Tick(p); } catch (Exception e) { Fault("BeastPlay.Tick", e); } if (Perf.On) Perf.Step("BeastPlay.Tick", ts); } // «Гра зі звіром»: the beast steered, a game by itself now and then
                { long ts = Perf.On ? Perf.Now : 0; try { JointPlay.Tick(p); } catch (Exception e) { Fault("JointPlay.Tick", e); } if (Perf.On) Perf.Step("JointPlay.Tick", ts); } // games with other players' ravens: invitations, the end
                { long ts = Perf.On ? Perf.Now : 0; try { Bonds.Tick(p); } catch (Exception e) { Fault("Bonds.Tick", e); } if (Perf.On) Perf.Step("Bonds.Tick", ts); } // how its ravens get along with the others' ravens near
                { long ts = Perf.On ? Perf.Now : 0; try { Pack.Tick(p); } catch (Exception e) { Fault("Pack.Tick", e); } if (Perf.On) Perf.Step("Pack.Tick", ts); } // friend ravens at a foe together
                { long ts = Perf.On ? Perf.Now : 0; try { People.Tick(p); } catch (Exception e) { Fault("People.Tick", e); } if (Perf.On) Perf.Step("People.Tick", ts); } // how its ravens take to the other players near
                { long ts = Perf.On ? Perf.Now : 0; try { Share.Tick(p); } catch (Exception e) { Fault("Share.Tick", e); } if (Perf.On) Perf.Step("Share.Tick", ts); } // a fed raven gives a hungry one a bite
                { long ts = Perf.On ? Perf.Now : 0; try { Gathering.Tick(p); } catch (Exception e) { Fault("Gathering.Tick", e); } if (Perf.On) Perf.Step("Gathering.Tick", ts); } // the ravens' evening gathering by a fire
                { long ts = Perf.On ? Perf.Now : 0; try { CrossTalk.Tick(p); } catch (Exception e) { Fault("CrossTalk.Tick", e); } if (Perf.On) Perf.Step("CrossTalk.Tick", ts); } // with other players' ravens
                { long ts = Perf.On ? Perf.Now : 0; try { Nicks.Tick(p); } catch (Exception e) { Fault("Nicks.Tick", e); } if (Perf.On) Perf.Step("Nicks.Tick", ts); } // their nicknames: the deeds counted, new ones opened, the chosen one shown to others
                { long ts = Perf.On ? Perf.Now : 0; try { Trust.Tick(p); } catch (Exception e) { Fault("Trust.Tick", e); } if (Perf.On) Perf.Step("Trust.Tick", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Backup.Tick(p); } catch (Exception e) { Fault("Backup.Tick", e); } if (Perf.On) Perf.Step("Backup.Tick", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { MuninDeeds.Tick(p); } catch (Exception e) { Fault("MuninDeeds.Tick", e); } if (Perf.On) Perf.Step("MuninDeeds.Tick", ts); }
                { long ts = Perf.On ? Perf.Now : 0; try { Cozy.Tick(p); } catch (Exception e) { Fault("Cozy.Tick", e); } if (Perf.On) Perf.Step("Cozy.Tick", ts); }
            }
        }

        // a gift goes on only while its raven is with you: taken off, or gone (the nest, a letter, a perch, a flight of
        // its own) — what it holds up ends (the wings set you down gently)
        private static void EndOrphaned(Player p, ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            bool h = hugin != null && !Trust.Away(hugin), m = munin != null && !Trust.Away(munin);
            if (!h)
            {
                if (OdinWings.Flying() && !OdinWings.Sinking()) OdinWings.Sink(p);
                if (Dome.Mine()) Dome.Lower();
                if (Decoy.Mine()) Decoy.Recall(p);
                if (RavenLight.On(p)) RavenLight.Stop(p);
            }
            if (!m && Beast.On()) Beast.End(p, true);
            if (!h || !m)
            {
                if (Load.On()) Load.End(p, false);
                if (ClearSky.Mine()) ClearSky.End(p, false);
            }
        }

        private static bool Ready(Player p, string id, string what)
        {
            float left = Left(id);
            if (left <= 0f) return true;
            RavenMsg.Center(p, Lang.F("{0}. Зачекай {1}", what, RavenAbilities.Span(left)));
            return false;
        }

        // the hero's own gesture on an ability, seen by everyone: an emote, or "gpower" (the boss power gesture)
        private static void Animate(Player p, string what)
        {
            if (!Animations || p == null || string.IsNullOrEmpty(what)) return;
            try
            {
                if (what == "gpower" || what == "eat" || what == "equip_hip")
                {
                    ZSyncAnimation z = ZAnim != null ? ZAnim.GetValue(p) as ZSyncAnimation : null;
                    if (z != null && !p.InAttack() && !p.IsDrawingBow()) z.SetTrigger(what);
                }
                else { EmoteTalk.OursUntil = Time.time + 1f; p.StartEmote(what, true); } // a gift's gesture: not for EmoteTalk
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven ability gesture failed: " + e.Message); }
        }

        // ---------------------------------------------------------------- talking from the shoulder

        internal static void SayFromShoulder(Player p, bool munin, string line) { Speak(p, munin, line); }

        // someone is talking now (your two, a talk with others' ravens, any raven's line near): what a raven would say on
        // its own, unasked (hunger, a quirk, an announcement), waits a little instead of talking over it
        internal static bool Hushed(Player p) { return Cozy.Quieted || Silenced() || (p != null && (TalkBubble.Playing(p) || CrossBubble.Busy(p))); }

        // a module wants the ravens quiet now: nothing said unasked, no visits (a story scene playing — its own lines
        // still come: StoryLine). Each module registers its own question (StoryModules)
        internal static readonly System.Collections.Generic.List<System.Func<bool>> Silence = new System.Collections.Generic.List<System.Func<bool>>();
        internal static bool Silenced() { for (int i = 0; i < Silence.Count; i++) if (Silence[i]()) return true; return false; }

        // a passive gift (last chance, blood fury, anger, light step, loyalty) has its say only this often
        internal static float PassiveTalkChance = 0.5f;
        internal static bool PassiveSpeaks() { return UnityEngine.Random.value < PassiveTalkChance; }

        // out on the scouting flight (before he is back on the shoulder with the report)
        internal static bool Scouting() { return Shoulder.HuginAway && Time.time < _scoutBackAt; }
        internal static float ScoutLeft() { return Mathf.Max(0f, _scoutBackAt - Time.time); }

        private static void StrikeAbility(Player p, bool munin, ItemDrop.ItemData item)
        {
            if (item == null) return;
            string id = munin ? "mstrike" : "hstrike";
            if (!Ready(p, id, munin ? Lang.T("Мунін ще чистить кігті") : Lang.T("Хугін ще переводить подих"))) return;
            // it always flies: at the one in your sights if it can, else the nearest (a word in the corner why not yours)
            string aimNote;
            List<Character> targets = Strike.Targets(p, munin, Strike.RangeOf(item), out aimNote, item);
            string scared = Quirks.TimidRefuses(p, munin, targets); // «Боязкий»: not at a fresh boss (nothing spent)
            if (scared != null) { RavenMsg.Center(p, scared); return; }
            float cost = Strike.CostOf(munin, item) * GiftUp.CostMul(id);
            if (!Power.Spend(p, munin, cost)) return;
            Cooldown(id);
            if (aimNote != null) p.Message(MessageHud.MessageType.TopLeft, Plugin.RavenName(munin) + ": " + aimNote + (targets.Count > 0 ? Lang.T(" · бере найближчу") : ""));
            Strike.Launch(p, munin, item, Power.Price(munin, cost), targets); // its words come when it is back on the shoulder
            _nextSync = 0f; // the book and the shoulder see the flight at once
        }

        // a strike nobody pressed for («Відплата»: after a parry): only when the gift itself could fly now, and quietly —
        // no word when it cannot, no strength, no pause of the gift
        // (debt: «Злість» III — what Munin pays back on top of his first blow)
        internal static bool FreeStrike(Player p, bool munin, Character target, float debt = 0f)
        {
            if (p == null || p.IsDead() || target == null || !Strike.Prey(p, target)) return false;
            ItemDrop.ItemData hugin, mun;
            RavenItem.Worn(p, out hugin, out mun);
            ItemDrop.ItemData item = munin ? mun : hugin;
            RavenAbility a = RavenAbilities.Find(munin ? "mstrike" : "hstrike");
            string mark; int dot, kind;
            if (item == null || a == null || Why(a, p, out mark, out dot, out kind) != null || Trust.Stranger(item, p)) return false; // (the same list as a press; quietly)
            string off;
            if (OffShoulder(p, munin, out off)) return false;
            if (Vector3.Distance(p.transform.position, target.transform.position) > Strike.RangeOf(item) || !Strike.Seen(p, target)) return false;
            List<Character> one = new List<Character> { target };
            if (Quirks.TimidRefuses(p, munin, one) != null) return false; // («Боязкий»: not at a fresh boss, as when pressed)
            Strike.Launch(p, munin, item, 0f, one, debt);
            _nextSync = 0f;
            return true;
        }

        // a raven about its strikes, in the tone of its trust (no line: it just flies)
        internal static void StrikeSay(bool munin, string tag)
        {
            Player p = Player.m_localPlayer;
            if (p == null || Plugin.Instance == null) return;
            string line = Plugin.Instance.ComposeTagged(p, munin, tag);
            if (!string.IsNullOrEmpty(line)) Speak(p, munin, line);
        }

        // a scene's line for your own raven (StoryScenes «say hugin|munin»): said as any of its words
        internal static void StoryLine(bool munin, string line)
        {
            Player p = Player.m_localPlayer;
            if (p != null && !string.IsNullOrEmpty(line)) Speak(p, munin, line);
        }

        // Hugin about his dome, in the tone of his trust
        internal static void DomeSay(string tag, string fallback) { GiftSay(false, tag, fallback); }

        // a gift's word said by itself, later than its press (the dome folding, the ward breaking)
        internal static void GiftSay(bool munin, string tag, string fallback)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, munin, tag) : null;
            Speak(p, munin, string.IsNullOrEmpty(line) ? fallback : line);
        }

        private static void Speak(Player p, bool munin, string line)
        {
            line = Words.Season(p, munin, line); // now and then a word you taught it
            line = Mead.Slur(p, munin, line);    // tipsy: a hiccup into it
            Cozy.Quiet(SaySeconds(line)); // the other raven, and the fireside story, wait for these words
            line = Grammar.Fix(line, p);
            if (munin) line = Beast.Rabid(p, line); // the beast awake: Munin yells it, with a curse
            Say(p, munin, line);
            if (!PlayerReact.Word(p, munin)) BodyClips.Listen(p, munin); // you turn your head to it (or, now and then, a palm to the face / a wave)
            Animator anim = Shoulder.LocalAnimator(munin);
            ShoulderLife.TalkOn(anim);
            ShoulderLife life = Shoulder.LocalLife(munin);
            if (life != null) life.Wake(4f); // an unwoken raven lifts its head to answer
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetPlayerName());
            pkg.Write(p.transform.position);
            pkg.Write(munin);
            pkg.Write(line);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcSay, pkg);
        }

        private static void Say(Player who, bool munin, string line)
        {
            if (RavenSpeech.Enabled) { RavenSpeech.ShowShoulder(who, munin, SaySeconds(line), line); return; } // over the raven itself, wherever it flies
            if (Chat.instance == null) return;
            string color = munin ? Style.MuninTitle : Style.HuginTitle;
            Chat.instance.SetNpcText(who.gameObject, Vector3.up * 2.3f, 40f, SaySeconds(line), "<color=" + color + ">" + Nicks.Title(who, munin) + "</color>", line, false);
        }

        // someone else's raven spoke: hear it over their head and in the chat when close
        private static void OnSay(long sender, ZPackage pkg)
        {
            string name = pkg.ReadString();
            Vector3 pos = pkg.ReadVector3();
            bool munin = pkg.ReadBool();
            string line = pkg.ReadString();
            Player me = Player.m_localPlayer;
            if (me == null || sender == ZDOMan.GetSessionID() || Chat.instance == null) return;
            if (Vector3.Distance(me.transform.position, pos) > HearRange) return;
            if (Vector3.Distance(me.transform.position, pos) < 30f) Cozy.Quiet(SaySeconds(line)); // a raven beside you talks: yours waits its turn
            Player speaker = null;
            foreach (Player other in Player.GetAllPlayers())
                if (other != null && other != me && other.GetPlayerName() == name)
                {
                    if (Shadow.Flagged(other)) return; // in the shadow: no words over an unseen head, nor in the chat
                    Say(other, munin, line);
                    ShoulderLife life = Shoulder.LifeOf(other, munin); // its beak and head as it speaks, as on its owner's game
                    if (life != null) { life.Talk(); life.Wake(4f); }
                    speaker = other;
                    break;
                }
            string color = munin ? Style.MuninTitle : Style.HuginTitle;
            Chat.instance.AddString("<color=" + color + ">" + (speaker != null ? Nicks.Name(speaker, munin) + " (" + name + ")" : Plugin.RavenName(munin) + " → " + name) + "</color>", line, Talker.Type.Normal, false);
            Traverse.Create(Chat.instance).Field("m_hideTimer").SetValue(0f);
        }

        // ---------------------------------------------------------------- Hugin

        // dungeons (their inside is far away, high above the world) and caves (troll caves stay in the world:
        // known by their dark cave weather, or by a roof inside a troll cave's place): no sky to fly into
        internal static bool UnderRoof(Player p)
        {
            if (p.InInterior()) return true;
            if (EnvMan.instance != null && EnvMan.instance.IsEnvironment("Caves")) return true;
            if (!p.InShelter()) return false;
            Location loc = Location.GetLocation(p.transform.position, true);
            return loc != null && loc.gameObject.name.StartsWith("TrollCave", StringComparison.Ordinal);
        }

        // a raven's thought: what happened lately or what it sees right now
        private static void Thought(Plugin plugin, Player p, bool munin)
        {
            string id = munin ? "mthought" : "thought";
            // with the voice: not while one of your ravens is still saying something (the voices would only queue up)
            bool talking;
            if (RavenVoice.Busy(p, out talking)) { RavenMsg.Center(p, talking ? Lang.T("Мунін ще говорить") : Lang.T("Хугін ще говорить")); return; }
            if (!Ready(p, id, munin ? Lang.T("Мунін ще думає") : Lang.T("Хугін ще думає"))) return;
            AboutTalk.Shot about = AboutTalk.Try(p, munin); // you look at another player: most often a word about them
            string line = about != null ? about.Line : plugin.ComposeThought(p, munin);
            if (string.IsNullOrEmpty(line) || !Power.Spend(p, munin, munin ? Power.MuninThoughtCost : Power.ThoughtCost)) return;
            Cooldown(id);
            if (AboutTalk.Said(p, munin, about)) { Trust.Talked(munin); return; } // one of their ravens snapped back: a talk of the two
            Speak(p, munin, line);
            RavenTalk.AfterThought(p, munin, line); // the other one may answer
            Trust.Talked(munin); // a talk together builds trust
        }

        // a raven that does not know you yet: its thought costs nothing, and all it says is to go away
        // (or, now and then, what would make it listen: the ritual)
        internal static float AsleepThoughtCooldown = 10f;
        private static string AsleepId(bool munin) { return munin ? "mthought_asleep" : "thought_asleep"; }

        private static void AsleepThought(Player p, bool munin)
        {
            string id = AsleepId(munin);
            if (!Ready(p, id, Lang.F("{0} тебе ігнорує", Plugin.RavenName(munin)))) return;
            ReadyAt[id] = Time.time + Mathf.Max(1f, AsleepThoughtCooldown);
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, munin, "asleep_talk") : null;
            Speak(p, munin, string.IsNullOrEmpty(line) ? (munin ? Lang.T("Відвали.") : Lang.T("Відійди. Я тебе не знаю.")) : line);
        }

        // his going off: the seekers' overlay sound backwards, a second after the press, 40% longer than the original
        internal const string ScoutSound = "sfx_seeker_overlay";
        internal static float ScoutSoundDelay = 1f, ScoutSoundStretch = 1.4f;

        private static void Scout(Player p)
        {
            // only while he is really away (the marks on the map stay a while after he is back: no reason to wait for them)
            if (Scouting()) { RavenMsg.Center(p, Lang.T("Хугін уже на розвідці")); return; }
            if (UnderRoof(p)) { RavenMsg.Center(p, Lang.T("Тут, під землею, Хугіну нікуди злетіти.\nВийди під небо — тоді й розвідаємо.")); return; }
            if (!Ready(p, "scout", Lang.T("Хугін ще відпочиває після розвідки"))) return;
            if (Minimap.instance == null || !Power.Spend(p, false, Power.ScoutCost * GiftUp.CostMul("scout"))) return;
            if (_scoutUntil > 0f) EndScout(); // the last scouting's marks give way to the new ones
            Cooldown("scout");
            Nicks.OnScout(p, false); // a deed for Hugin's nickname
            // the marks come with Hugin's report, and stay ScoutSeconds from then
            _scoutBackAt = Time.time + Mathf.Max(2f, ScoutAwaySeconds);
            _scoutUntil = _scoutBackAt + Mathf.Max(5f, ScoutSeconds);
            _scoutFrom = p.transform.position;
            if (Twists.Two("scout")) // («Розвідка» II: half a reach ahead, where you look)
            {
                Vector3 look = GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) _scoutFrom += look.normalized * RevealRadius() * Twists.ScoutAhead;
            }
            _nextPins = 0f;
            Power.Scouting = true;
            try { if (Explore != null) Explore.Invoke(Minimap.instance, new object[] { _scoutFrom, RevealRadius() }); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven scouting could not clear the map: " + e.Message); }
            finally { Power.Scouting = false; }
            Animate(p, "point");
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, false, "scout") : null;
            Speak(p, false, string.IsNullOrEmpty(line) ? Lang.T("Полетів дивитися. Дивись на мапу.") : line);
            Orders.GiveWay(p, false, Lang.T("лечу на розвідку")); // out on an order: he goes from the air
            Shoulder.HuginAway = true; // he leaves the shoulder, for everyone
            SoundBack.PlayNamed(ScoutSound, p.transform, ScoutSoundDelay, ScoutSoundStretch);
            RavenMsg.Center(p, Lang.T("Хугін полетів на розвідку. Дивись на мапу."));
        }

        private static void TickScout()
        {
            if (Time.time >= _scoutUntil) { EndScout(); return; }
            if (Shoulder.HuginAway)
            {
                if (Time.time < _scoutBackAt) return; // still flying: nothing on the map yet
                _scoutRunesUntil = Facets.On("scout") ? Time.time + Twists.ScoutRunes : 0f; // («Розвідка» III: runes over their heads)
                MarkEnemies();
                MarkOre(_scoutFrom);
                MarkDungeons(_scoutFrom);
                Report();
                return;
            }
            if (Time.time < _nextPins) return;
            MarkEnemies();
        }

        // the enemies around you, again every second while the marks hold (they move)
        private static void MarkEnemies()
        {
            _nextPins = Time.time + 1f;
            ClearPins();
            Player p = Player.m_localPlayer;
            if (p == null || Minimap.instance == null) return;
            float reach = ScoutEnemyRadius * Trust.Mul(false, 0.05f) * Norov.ScoutMul(Trust.Worn(false)) * Habits.HearMul(Trust.Worn(false)); // (its knack «Пильне око»; «Копач» deaf after a pick's blow)
            Vector3 me = p.transform.position;
            bool ahead = Twists.Two("scout"); // (II: around you and around the place ahead he flew over)
            bool runes = Time.time < _scoutRunesUntil;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed()) continue;
                bool near = (c.transform.position - me).sqrMagnitude <= reach * reach || (ahead && (c.transform.position - _scoutFrom).sqrMagnitude <= reach * reach);
                if (!near || !BaseAI.IsEnemy(p, c)) continue;
                if (runes) MarkLook.Show(c, false, 1.3f); // (kept up every second while it lasts)
                string label = Localization.instance != null ? Lang.Game(c.m_name) : c.m_name;
                if (c.IsBoss()) label = "‼ " + label;
                bool starred = !c.IsBoss() && c.GetLevel() > 1 && Facets.On("scout"); // («Розвідка» III: their stars)
                if (starred) label += " " + new string('★', c.GetLevel() - 1);
                Minimap.PinData pin = Minimap.instance.AddPin(c.transform.position, ScoutPin, label, false, false, 0L, default(Splatform.PlatformUserID));
                Icon(pin, Trophy(c));
                if (c.IsBoss() || starred) pin.m_animate = true;
                Pins.Add(pin);
            }
        }

        // Hugin is back on the shoulder well before the marks fade, and says what he saw
        private static void Report()
        {
            Shoulder.HuginAway = false;
            Player p = Player.m_localPlayer;
            if (p == null) return;
            int seen = 0, bosses = 0;
            foreach (Minimap.PinData pin in Pins) if (pin != null) { seen++; if (pin.m_name != null && pin.m_name.StartsWith("‼")) bosses++; }
            string line = seen == 0 ? Lang.T("Повернувся. Довкола тихо, можна дихати.")
                : bosses > 0 ? Lang.F("Повернувся. Ворогів поруч — {0}, і серед них бос! Глянь на мапу.", seen)
                : Lang.F("Повернувся. Ворогів поруч — {0}. Вони на мапі.", seen);
            if (!string.IsNullOrEmpty(_oreSummary)) line += Lang.F(" Руда поруч: {0}.", _oreSummary);
            if (!string.IsNullOrEmpty(_dungeonSummary)) line += Lang.F(" І вхід під землю: {0}.", _dungeonSummary);
            Speak(p, false, line);
            RavenMsg.Center(p, (seen == 0 ? Lang.T("Хугін повернувся: довкола тихо.") : Lang.F("Хугін повернувся: ворогів поруч — {0}.", seen))
                + (string.IsNullOrEmpty(_oreSummary) ? "" : Lang.F("\nРуда поруч: {0}.", _oreSummary))
                + (string.IsNullOrEmpty(_dungeonSummary) ? "" : Lang.F("\nПідземелля: {0}.", _dungeonSummary)));
        }

        // Scouting pins carry their own picture. Map mods restyle pins by type (a UI mod draws Icon3 as a dashed
        // ring, Icon1 as a house); the "shout" type is one they leave alone, so the trophy or the ore shows.
        private const Minimap.PinType ScoutPin = Minimap.PinType.Shout;

        // a pin with our own picture: the creature's trophy, the ore itself
        internal static void Icon(Minimap.PinData pin, Sprite icon)
        {
            if (pin == null || icon == null) return;
            pin.m_icon = icon;
            if (pin.m_iconElement != null) pin.m_iconElement.sprite = icon;
        }

        internal static Sprite Trophy(Character c)
        {
            string key = c.m_name ?? "";
            Sprite s;
            if (TrophyIcons.TryGetValue(key, out s)) return s;
            s = null;
            CharacterDrop drop = c.GetComponent<CharacterDrop>();
            if (drop != null && drop.m_drops != null)
                foreach (CharacterDrop.Drop d in drop.m_drops)
                {
                    ItemDrop item = d.m_prefab != null ? d.m_prefab.GetComponent<ItemDrop>() : null;
                    if (item != null && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy) { s = item.m_itemData.GetIcon(); break; }
                }
            // no trophy of its own (a greyling, a bat, a tick, a ghost): its kin's, by the likest name; else our own mark
            if (s == null) s = KinTrophy(Utils.GetPrefabName(c.gameObject));
            if (s == null) s = AbilityIcons.For("enemy", false);
            TrophyIcons[key] = s;
            return s;
        }

        // the trophy whose name starts most like the creature's (Greyling → TrophyGreydwarf); at least 4 letters alike
        private static Sprite KinTrophy(string prefab)
        {
            if (ObjectDB.instance == null || string.IsNullOrEmpty(prefab)) return null;
            string want = prefab.ToLowerInvariant();
            Sprite best = null;
            int bestLen = 3;
            foreach (GameObject o in ObjectDB.instance.m_items)
            {
                ItemDrop item = o != null ? o.GetComponent<ItemDrop>() : null;
                if (item == null || item.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy || !o.name.StartsWith("Trophy")) continue;
                string t = o.name.Substring(6).ToLowerInvariant();
                int n = 0;
                while (n < t.Length && n < want.Length && t[n] == want[n]) n++;
                if (n > bestLen) { bestLen = n; best = item.m_itemData.GetIcon(); }
            }
            return best;
        }

        // ---------------------------------------------------------------- ore: Hugin sees every deposit around where he flew from (once, on his return)

        private sealed class OreFind { public ItemDrop Item; public Vector3 Pos; }

        private static float _oreReach;

        private static void MarkOre(Vector3 at)
        {
            ClearOre();
            _oreSummary = null;
            if (Minimap.instance == null || ScoutOreRadius <= 0f) return;
            List<OreFind> found = new List<OreFind>();
            _oreReach = ScoutOreRadius * Trust.Mul(false, 0.05f) * Norov.ScoutMul(Trust.Worn(false)) * Habits.OreMul(Trust.Worn(false)); // once for the whole look (knack «Пильне око», habit «Копач»)
            try
            {
                foreach (MineRock5 r in UnityEngine.Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None)) Consider(found, at, r.transform.position, Ore(r.m_dropItems));
                foreach (MineRock r in UnityEngine.Object.FindObjectsByType<MineRock>(FindObjectsSortMode.None)) Consider(found, at, r.transform.position, Ore(r.m_dropItems));
                foreach (DropOnDestroyed d in UnityEngine.Object.FindObjectsByType<DropOnDestroyed>(FindObjectsSortMode.None)) Consider(found, at, d.transform.position, Ore(d.m_dropWhenDestroyed));
                // pieces lying about to be picked up (a bit of scrap, tin on a shore) are not worth a raven's flight: deposits only
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven scouting could not look for ore: " + e.Message); }

            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<string> order = new List<string>();
            foreach (OreFind f in found)
            {
                string name = Localization.instance != null ? Lang.Game(f.Item.m_itemData.m_shared.m_name) : f.Item.m_itemData.m_shared.m_name;
                Minimap.PinData pin = Minimap.instance.AddPin(f.Pos, ScoutPin, name, false, false, 0L, default(Splatform.PlatformUserID));
                Icon(pin, f.Item.m_itemData.GetIcon());
                OrePins.Add(pin);
                if (!counts.ContainsKey(name)) { counts[name] = 0; order.Add(name); }
                counts[name]++;
            }
            if (OrePins.Count > 0) _orePinsUntil = Time.time + Mathf.Max(0.5f, ScoutOreMinutes) * 60f;
            List<string> parts = new List<string>();
            foreach (string n in order) parts.Add(n.ToLowerInvariant() + " ×" + counts[n]);
            _oreSummary = string.Join(", ", parts.ToArray());
            if (Plugin.Instance != null) Plugin.Instance.Log("Raven scouting: " + found.Count + " ore deposits marked" + (found.Count > 0 ? " (" + _oreSummary + ")" : ""));
        }

        // ---------------------------------------------------------------- dungeons: the ways under the ground he flew over

        // the kinds, by the start of the location's name (the longer first); anything else with an inside: "підземелля"
        private static readonly KeyValuePair<string, string>[] DungeonNames =
        {
            new KeyValuePair<string, string>("SunkenCrypt", Lang.K("затоплений склеп")),
            new KeyValuePair<string, string>("Crypt", Lang.K("поховальна камера")),
            new KeyValuePair<string, string>("TrollCave", Lang.K("печера тролів")),
            new KeyValuePair<string, string>("MountainCave", Lang.K("морозна печера")),
            new KeyValuePair<string, string>("Mistlands_DvergrTownEntrance", Lang.K("заражена шахта")),
            new KeyValuePair<string, string>("Hildir", Lang.K("схованка, яку шукає Гільдір")),
        };
        private static Sprite _dungeonIcon;

        // every location with an inside (a dungeon behind its door) that is loaded around where he flew from
        private static void MarkDungeons(Vector3 at)
        {
            _dungeonSummary = null;
            if (ScoutDungeonRadius <= 0f) return;
            _dungeonSummary = PinDungeons(at, DungeonRadius(), Vector3.zero, OrePins); // (they fade with the ore marks)
            if (Facets.On("scout")) PinChests(at, DungeonRadius());
            if (_dungeonSummary == null) return;
            if (_orePinsUntil <= 0f) _orePinsUntil = Time.time + Mathf.Max(0.5f, ScoutOreMinutes) * 60f;
            if (Plugin.Instance != null) Plugin.Instance.Log("Raven scouting: dungeons marked (" + _dungeonSummary + ")");
        }

        // «Розвідка» III: the chests nobody built (the world's, the dungeons') loaded around, with the ore marks
        private static void PinChests(Vector3 at, float radius)
        {
            if (Minimap.instance == null) return;
            Sprite coin = null;
            GameObject coins = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Coins") : null;
            if (coins != null) coin = coins.GetComponent<ItemDrop>().m_itemData.GetIcon();
            int n = 0;
            foreach (Container box in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (box == null || (box.transform.position - at).sqrMagnitude > radius * radius || !WorldChest(box)) continue;
                Minimap.PinData pin = Minimap.instance.AddPin(box.transform.position, ScoutPin, Lang.T("скриня"), false, false, 0L, default(Splatform.PlatformUserID));
                if (coin != null) Icon(pin, coin);
                OrePins.Add(pin);
                n++;
            }
            if (n > 0 && _orePinsUntil <= 0f) _orePinsUntil = Time.time + Mathf.Max(0.5f, ScoutOreMinutes) * 60f;
        }

        // a chest nobody built (the world's, a dungeon's): not someone's own, not a grave, not a ship's or a cart's hold,
        // not ours on a player (the pouch, a parcel). The scouting and «Нюх на скарби» (PerkFx) both ask
        internal static bool WorldChest(Container box)
        {
            Piece pc = box.GetComponentInParent<Piece>();
            if (pc != null && pc.GetCreator() != 0L) return false; // someone's own
            if (box.GetComponent<TombStone>() != null) return false; // a player's grave
            return box.GetComponentInParent<Ship>() == null && box.GetComponentInParent<Vagon>() == null && box.GetComponentInParent<Player>() == null;
        }

        // The ways under the ground loaded within 'radius' of 'at' (only those ahead along 'ahead', when it is given),
        // pinned into 'pins' with the crypt key's picture; what they are, "поховальна камера ×2, …" (null: none).
        // Hugin's gift and the ravens' scouting on your word (Orders) both look so.
        internal static string PinDungeons(Vector3 at, float radius, Vector3 ahead, List<Minimap.PinData> pins)
        {
            if (Minimap.instance == null || radius <= 0f) return null;
            Dictionary<string, int> counts = null;
            List<string> order = null;
            try
            {
                foreach (Location loc in UnityEngine.Object.FindObjectsByType<Location>(FindObjectsSortMode.None))
                {
                    if (loc == null || !(loc.m_hasInterior || loc.m_generator != null)) continue;
                    Vector3 to = loc.transform.position - at;
                    if (to.sqrMagnitude > radius * radius) continue;
                    if (ahead != Vector3.zero && Vector3.Dot(to.normalized, ahead) < 0.3f) continue;
                    string name = DungeonName(loc.gameObject.name);
                    Minimap.PinData pin = Minimap.instance.AddPin(loc.transform.position, ScoutPin, name, false, false, 0L, default(Splatform.PlatformUserID));
                    Icon(pin, DungeonIcon());
                    pins.Add(pin);
                    if (counts == null) { counts = new Dictionary<string, int>(); order = new List<string>(); }
                    if (!counts.ContainsKey(name)) { counts[name] = 0; order.Add(name); }
                    counts[name]++;
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven scouting could not look for dungeons: " + e.Message); }
            if (order == null) return null;
            List<string> parts = new List<string>();
            foreach (string n in order) parts.Add(counts[n] > 1 ? n + " ×" + counts[n] : n);
            return string.Join(", ", parts.ToArray());
        }

        private static string DungeonName(string objectName)
        {
            foreach (KeyValuePair<string, string> kv in DungeonNames) if (objectName.StartsWith(kv.Key, StringComparison.Ordinal)) return Lang.T(kv.Value);
            return Lang.T("підземелля");
        }

        // the crypt key's picture: a way in
        private static Sprite DungeonIcon()
        {
            if (_dungeonIcon != null) return _dungeonIcon;
            GameObject key = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("CryptKey") : null;
            ItemDrop drop = key != null ? key.GetComponent<ItemDrop>() : null;
            _dungeonIcon = drop != null ? drop.m_itemData.GetIcon() : null;
            return _dungeonIcon;
        }

        // Hugin shows the ore of a land only once you have beaten that land's boss (the game's boss keys):
        // copper and tin after the Elder, iron after Bonemass, silver after Moder, and so on. Ore not listed: always.
        private static readonly KeyValuePair<string, string>[] OreBoss =
        {
            new KeyValuePair<string, string>("CopperOre", "defeated_gdking"), new KeyValuePair<string, string>("TinOre", "defeated_gdking"),
            new KeyValuePair<string, string>("IronScrap", "defeated_bonemass"), new KeyValuePair<string, string>("IronOre", "defeated_bonemass"),
            new KeyValuePair<string, string>("SilverOre", "defeated_dragon"),
            new KeyValuePair<string, string>("BlackMetalScrap", "defeated_goblinking"),
            new KeyValuePair<string, string>("CopperScrap", "defeated_queen"),
            new KeyValuePair<string, string>("Flametal", "defeated_fader"), // FlametalOre, FlametalOreNew
        };

        private static bool OreKnown(ItemDrop ore)
        {
            string n = ore.gameObject.name;
            foreach (KeyValuePair<string, string> kv in OreBoss)
                if (n.StartsWith(kv.Key, StringComparison.Ordinal)) return BossBeaten(kv.Value);
            return true;
        }

        // a boss key, for the world or (with the "player keys" world setting) for this player
        private static bool BossBeaten(string key)
        {
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key)) return true;
            Player p = Player.m_localPlayer;
            return p != null && p.HaveUniqueKey(key);
        }

        private static void Consider(List<OreFind> found, Vector3 at, Vector3 pos, ItemDrop ore)
        {
            if (ore == null || found.Count >= 60 || !OreKnown(ore)) return;
            if ((at - pos).sqrMagnitude > _oreReach * _oreReach) return;
            foreach (OreFind f in found) if (f.Item == ore && Vector3.Distance(f.Pos, pos) < 6f) return; // one pin per heap
            OreFind n = new OreFind();
            n.Item = ore; n.Pos = pos;
            found.Add(n);
        }

        // the nearest ore deposit within reach (the breed «Іржавий»: BreedTraits), skipping the heaps near any of skip
        internal static bool NearestOre(Vector3 at, float reach, List<Vector3> skip, out Vector3 pos, out ItemDrop ore)
        {
            pos = Vector3.zero; ore = null;
            float best = reach * reach;
            try
            {
                foreach (MineRock5 r in UnityEngine.Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None)) Nearer(at, r.transform.position, Ore(r.m_dropItems), skip, ref best, ref pos, ref ore);
                foreach (MineRock r in UnityEngine.Object.FindObjectsByType<MineRock>(FindObjectsSortMode.None)) Nearer(at, r.transform.position, Ore(r.m_dropItems), skip, ref best, ref pos, ref ore);
                foreach (DropOnDestroyed d in UnityEngine.Object.FindObjectsByType<DropOnDestroyed>(FindObjectsSortMode.None)) Nearer(at, d.transform.position, Ore(d.m_dropWhenDestroyed), skip, ref best, ref pos, ref ore);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven could not look for ore: " + e.Message); }
            return ore != null;
        }

        private static void Nearer(Vector3 at, Vector3 p, ItemDrop it, List<Vector3> skip, ref float best, ref Vector3 pos, ref ItemDrop ore)
        {
            if (it == null || !OreKnown(it)) return;
            float d = (at - p).sqrMagnitude;
            if (d >= best) return;
            if (skip != null) foreach (Vector3 v in skip) if ((v - p).sqrMagnitude < 64f) return; // (that heap was shown already)
            best = d; pos = p; ore = it;
        }

        // a map pin with the ore's own picture (who made it removes it)
        internal static Minimap.PinData AddOrePin(Vector3 pos, ItemDrop ore, string name)
        {
            Minimap.PinData pin = Minimap.instance.AddPin(pos, ScoutPin, name, false, false, 0L, default(Splatform.PlatformUserID));
            Icon(pin, ore.m_itemData.GetIcon());
            return pin;
        }

        private static ItemDrop Ore(DropTable table)
        {
            if (table == null || table.m_drops == null) return null;
            foreach (DropTable.DropData d in table.m_drops)
            {
                ItemDrop it = d.m_item != null ? d.m_item.GetComponent<ItemDrop>() : null;
                if (IsOre(it)) return it;
            }
            return null;
        }

        // copper, tin, silver, flametal ores; iron, black metal, copper scrap
        private static bool IsOre(ItemDrop it)
        {
            if (it == null) return false;
            string n = it.gameObject.name;
            return n.IndexOf("Ore", StringComparison.Ordinal) >= 0 || n.IndexOf("Scrap", StringComparison.Ordinal) >= 0;
        }

        private static void ClearOre()
        {
            if (Minimap.instance != null) foreach (Minimap.PinData pin in OrePins) if (pin != null) Minimap.instance.RemovePin(pin);
            OrePins.Clear();
            _orePinsUntil = 0f;
        }

        private static void EndScout()
        {
            ClearPins();
            _scoutUntil = 0f;
            Shoulder.HuginAway = false;
        }

        private static void ClearPins()
        {
            if (Minimap.instance != null) foreach (Minimap.PinData pin in Pins) if (pin != null) Minimap.instance.RemovePin(pin);
            Pins.Clear();
        }

        private static void LightAbility(Player p)
        {
            if (!Ready(p, "light", Lang.T("Хугін ще шукає світляка")) || !Power.Spend(p, false, Power.LightCost * GiftUp.CostMul("light"))) return;
            Cooldown("light");
            RavenLight.Start(p);
            RavenLight.Publish(p);
            Animate(p, "toast");
            if (UnityEngine.Random.value >= LightTalkChance) return; // not every time: a light lit is no news
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, false, "light") : null;
            Speak(p, false, string.IsNullOrEmpty(line) ? Lang.T("Тримай світло. Тепер хоч видно, куди ти вступаєш.") : line);
        }

        internal static float LightTalkChance = 0.5f;

        private static void LeapAbility(Player p)
        {
            if (Leap.On(p)) { RavenMsg.Center(p, Lang.T("Злет Хугіна скінчиться, щойно приземлишся")); return; } // (pressed again while it waits for the landing)
            if (!Ready(p, "leap", Lang.T("Хугін ще не готовий підкинути тебе"))) return;
            if (!Power.Spend(p, false, Power.LeapCost * GiftUp.CostMul("leap"))) return;
            Cooldown("leap");
            Leap.Start(p); // no throw now: the next jumps go high
            LeapFx.Feathers(p);
            Animate(p, "cheer"); // arms up to the sky
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, false, "leap") : null;
            Speak(p, false, string.IsNullOrEmpty(line) ? Lang.T("Стрибай, поки закляття тримає. І стеж за часом.") : line);
        }

        private static void ShadowAbility(Player p)
        {
            if (Shadow.On(p)) { if (Facets.On("shadow")) Shadow.Step(p); else RavenMsg.Center(p, Lang.T("Ти вже в тіні")); return; } // («Тінь» III: a step in the shadow)
            if (!Ready(p, "shadow", Lang.T("Хугін ще не готовий сховати тебе"))) return;
            if (!Power.Spend(p, false, Power.ShadowCost * GiftUp.CostMul("shadow"))) return;
            Cooldown("shadow");
            Shadow.Start(p);
            if (Twists.Two("shadow")) Twists.ShareShadow(p, Shadow.Duration()); // («Тінь» II: the friend beside you too)
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, false, "shadow") : null;
            Speak(p, false, string.IsNullOrEmpty(line) ? Lang.T("Тепер тебе ніхто не бачить. Заходь зі спини.") : line);
        }

        private static void DomeAbility(Player p, ItemDrop.ItemData hugin)
        {
            if (!Ready(p, "dome", Lang.T("Хугін ще не відпочив після купола"))) return;
            if (!Power.Spend(p, false, Power.DomeCost * GiftUp.CostMul("dome"))) return;
            Cooldown("dome");
            Dome.Raise(p, hugin != null ? Trust.ShownLevel(false, hugin) : 1);
            Animate(p, "cheer");
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, false, "dome") : null;
            Speak(p, false, string.IsNullOrEmpty(line) ? Lang.T("Стань під крило. Хай чекають.") : line);
        }

        // ---------------------------------------------------------------- Munin

        private static void FuryAbility(Player p)
        {
            if (p.GetSEMan().HaveStatusEffect(Fury.Hash)) { RavenMsg.Center(p, Lang.T("Лють уже палає")); return; }
            if (!Ready(p, "fury", Lang.T("Мунін ще не охолов")) || !Power.Spend(p, true, Power.FuryCost * GiftUp.CostMul("fury"))) return;
            Cooldown("fury");
            Fury.Start(p);
            Twists.FuryStarted();
            Fury.Publish(p);
            Fury.PlaySound(p); // on the press: the flash comes at the gesture's peak
            // the boss power gesture; the effect comes at its peak (see Fury), or right away when there is no gesture
            if (Animations && !p.InAttack() && !p.IsDrawingBow()) { Fury.Gesture(); Animate(p, "gpower"); }
            else Fury.PlayFx(p);
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, true, "fury") : null;
            Speak(p, true, string.IsNullOrEmpty(line) ? Lang.T("РУБАЙ ЇХ, СУКА!") : line);
        }

        private static void WingsAbility(Player p)
        {
            if (p.GetSEMan().HaveStatusEffect(Wings.Hash)) { RavenMsg.Center(p, Lang.T("Попутний вітер уже несе тебе")); return; }
            if (!Ready(p, "wings", Lang.T("Мунін ще не відсапався")) || !Power.Spend(p, true, Power.WingsCost * GiftUp.CostMul("wings"))) return;
            Cooldown("wings");
            Wings.Start(p);
            WingsFx.Burst(p);
            WingsFx.Publish(p);
            Twists.WindFlags(p, true); // (II: the friends behind; III: the sails — read by the others while the wind blows)
            Animate(p, "challenge");
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, true, "wings") : null;
            Speak(p, true, string.IsNullOrEmpty(line) ? Lang.T("НОГИ В РУКИ, СУКА!") : line);
        }

        private static void HealAbility(Player p)
        {
            if (p.GetSEMan().HaveStatusEffect(RavenHeal.Hash)) { RavenMsg.Center(p, Lang.T("Мунін уже тебе латає")); return; }
            bool cleanse = Twists.Two("heal") && Twists.Dirty(p); // («Зцілення» II: worth it for the poison alone)
            if (p.GetHealth() >= p.GetMaxHealth() - 0.5f && !cleanse) { RavenMsg.Center(p, Grammar.Fix(Lang.T("Ти й так {цілий|ціла}, нема чого латати"), p)); return; }
            if (!Ready(p, "heal", Lang.T("Мунін ще не зібрався з силами"))) return;
            if (!Power.Spend(p, true, Power.HealCost * GiftUp.CostMul("heal"))) return;
            Cooldown("heal");
            Twists.Cleanse(p);
            RavenHeal.Start(p);
            ShoulderLife.Hug(p);
            Animate(p, "equip_hip"); // arms out to the sides, as when the weapons go on the back
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, true, "heal") : null;
            Speak(p, true, string.IsNullOrEmpty(line) ? Lang.T("ЖРИ, НЕ ЗДИХАЙ МЕНІ ТУТ!") : line);
        }

        // a gift out of place now (the wings on, the beast awake), or null
        internal static string GiftBlock(RavenAbility a) { return OdinWings.Blocks(a) ?? Beast.Blocks(a) ?? PairGifts.Blocks(a) ?? GreatBlock(a); }

        // one great gift at a time (the user, 2026-10-24): while Odin's wings, the beast or a paired gift goes on, the
        // other great ones wait (the one on stays pressable: it ends it)
        private static readonly string[] Great = { "hflight", "mberserk", "pwhirl", "pload", "pclear" };
        internal static string GreatOn()
        {
            if (OdinWings.Flying()) return "hflight";
            if (Beast.On()) return "mberserk";
            if (Whirl.On()) return "pwhirl";
            if (Load.On()) return "pload";
            if (ClearSky.Mine()) return "pclear";
            return null;
        }
        internal static string GreatBlock(RavenAbility a)
        {
            if (a == null || Array.IndexOf(Great, a.Id) < 0) return null;
            string on = GreatOn();
            if (on == null || on == a.Id) return null;
            RavenAbility g = RavenAbilities.Find(on);
            return Lang.F("Вже діє «{0}»", g != null ? g.Name : on);
        }

        private static void BeastAbility(Player p)
        {
            if (!Ready(p, "mberserk", Lang.T("Звір ще спить після минулого разу"))) return;
            if (!Power.Spend(p, true, Power.BeastCost * GiftUp.CostMul("mberserk"))) return;
            Cooldown("mberserk");
            Beast.Start(p); // (its own waking pose: Beast.WakeClip)
            Twists.Roar(p); // (III: the weak ones run)
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, true, "beast") : null;
            Speak(p, true, string.IsNullOrEmpty(line) ? Lang.T("ПРОКИДАЙСЯ, ЗВІРЕ! РВИ ЇХ!") : line);
        }

        private static void TameAbility(Player p, ItemDrop.ItemData munin)
        {
            if (!Ready(p, "mtame", Lang.T("Мунін ще не відійшов від минулої худоби"))) return;
            Tame.Begin(p, munin);
        }

        private static void DecoyAbility(Player p, ItemDrop.ItemData hugin)
        {
            if (!Ready(p, "hdecoy", Lang.T("Хугін ще не придумав нової витівки"))) return;
            string why;
            List<Character> who = Decoy.Targets(p, hugin != null ? Trust.Level(hugin) : 1, out why);
            if (who == null) { RavenMsg.Center(p, why); return; } // (nobody fit: no strength, no pause)
            if (!Power.Spend(p, false, Power.DecoyCost * GiftUp.CostMul("hdecoy"))) return;
            Cooldown("hdecoy");
            Decoy.Begin(p, who);
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, false, "hdecoy") : null;
            Speak(p, false, string.IsNullOrEmpty(line) ? Lang.T("Гей, бовдуре! Сюди дивись!") : line);
        }

        private static void FlightAbility(Player p)
        {
            if (!Ready(p, "hflight", Lang.T("Хугін ще не відпочив після польоту"))) return;
            if (!Power.Spend(p, false, Power.FlightCost * GiftUp.CostMul("hflight"))) return;
            Cooldown("hflight");
            OdinWings.Start(p);
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, false, "hflight") : null;
            Speak(p, false, string.IsNullOrEmpty(line) ? Lang.T("Тримайся за повітря. Злітаємо!") : line);
        }

        private static void WardAbility(Player p, ItemDrop.ItemData munin)
        {
            if (Ward.Mine()) { RavenMsg.Center(p, Lang.T("Заслона вже стоїть")); return; }
            if (!Ready(p, "ward", Lang.T("Мунін ще не зібрав рун на нову заслону"))) return;
            if (!Power.Spend(p, true, Power.WardCost * GiftUp.CostMul("ward"))) return;
            Cooldown("ward");
            Ward.Raise(p, munin != null ? Trust.Level(munin) : 1);
            Animate(p, "challenge");
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, true, "ward") : null;
            Speak(p, true, string.IsNullOrEmpty(line) ? Lang.T("СТОЮ ПЕРЕД ТОБОЮ! ХАЙ ТІЛЬКИ СПРОБУЮТЬ!") : line);
        }

        // a shove that way, no damage (on the game that drives it: a hit of nothing)
        internal static void Push(Player p, Character c, Vector3 d, float force = 80f)
        {
            HitData h = new HitData();
            h.m_point = c.GetCenterPoint();
            h.m_dir = new Vector3(d.x, 0.3f, d.z).normalized;
            h.m_pushForce = force;
            h.m_skill = Skills.SkillType.None;
            h.m_skillRaiseAmount = Strike.Marker - 1f; // (a raven's: no strike from the shadow, no skill)
            h.SetAttacker(p);
            c.Damage(h);
        }

        private static void Scream(Player p)
        {
            if (!Ready(p, "scream", Lang.T("Мунін ще хрипить після крику")) || !Power.Spend(p, true, Power.ScreamCost * GiftUp.CostMul("scream"))) return;
            Cooldown("scream");
            Animate(p, "roar");
            ScreamFx.Play(p);
            Shadow.Reveal(p, false); // a scream gives you away
            float reach = ScreamRadius * Trust.Mul(true, 0.05f) * GearFx.ScreamMul() * GiftUp.Power("scream"); // (Дзвінкий ошийник: further)
            int hit = ScreamWave(p, p.transform.position, reach, false);
            Twists.Echo(p, p.transform.position, reach); // («Крик» II: it comes again)
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, true, "scream") : null;
            Speak(p, true, string.IsNullOrEmpty(line) ? Lang.T("ГЕТЬ, СУКИ!") : line);
            if (hit == 0) p.Message(MessageHud.MessageType.TopLeft, Lang.T("Мунін накричав на порожнечу"));
        }

        // the scream's wave from a place: the foes in reach stagger, the ones not bosses run (III: thrown back, and
        // their guard broken); the echo (II) is the same wave again, with its own sound
        internal static int ScreamWave(Player p, Vector3 at, float reach, bool echo)
        {
            if (echo) { ScreamFx.Caw(p, true, 0.8f, 1f); BindFx.Nova(p, "gust", at + Vector3.up, reach, 0.5f); }
            int hit = 0;
            List<Character> scared = new List<Character>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed()) continue;
                if (!BaseAI.IsEnemy(p, c)) continue;
                Vector3 d = c.transform.position - at;
                if (d.sqrMagnitude > reach * reach) continue;
                c.Stagger(d.normalized);
                if (!c.IsBoss() && Facets.On("scream")) Push(p, c, d); // («Крик» III: thrown back)
                if (!c.IsBoss()) scared.Add(c); // bosses are not frightened
                hit++;
            }
            Fear.Frighten(at, scared, Fear.ScreamSeconds());
            Twists.BreakGuard(scared); // («Крик» III: no blocking for a while)
            return hit;
        }
    }
}
#endif
