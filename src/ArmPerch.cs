#if RAVEN_ITEM
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «На руку» (the user, 2026-10-29: a falconer's arm): you hold out the forearm on your raven's side (Hugin the right,
    // Munin the left) and it hops onto it from the shoulder; the arm is its perch then. From J (trust 3 and up) a small
    // button by its wing, «На руку»; once it sits there the row opens: back to the shoulder, food from your hand, a toss
    // up, and «Звички» for its own ways (ArmTricks). Standing or sitting on a seat.
    // Whatever flies it off while it sits there (an order, an outing, a strike, a toss) starts from the arm with a swing of
    // it. Now and then by itself (by trust, your hand free and you calm) it comes home onto the arm and sits a moment, or
    // steps onto it before it flies off.
    // You always come first: anything taken in hand, a swing, a block, a dodge, a run, a jump, a blow, water, a ride — it
    // hops back to the shoulder at once and the arm is yours. The button puts away what is in your hands (R takes it out).
    // Up in the air, «На руку» calls it back onto the arm, held up higher to catch it.
    // Everyone sees it: the arm by your animation's data (BodyClips), the bird by the bits on your ZDO (ShoulderPin).
    internal static class ArmPerch
    {
        internal const string Key = "RavenWatch_Arm"; // bits per raven (Hugin, Munin): 1/2 on the arm, 4/8 stepping on to fly off from it, 16/32 up to your eyes, 64/128 on your other arm
        internal const int OnBit = 1, StageBit = 4, EyesBit = 16, OtherBit = 64;
        internal static int Bit(bool munin, int kind) { return munin ? kind * 2 : kind; }

        internal static int MinTrust = 3;                    // the arm's row in J from this trust on («На руку»)
        internal static int HintTrust = 2;                   // a hint now and then a level before
        // (the arm row's own — on the arm, food from your hand, the toss — open with the row: the user, 2026-10-29; the
        // stroke and eye to eye are in the pool of its own tricks, ArmTricks, opened one by one)
        internal const int ActMore = 20; // the arm row's «Звички»: drops its own tricks' row (OrdersMenu only)
        // sitting on your arm it now and then does one of the ten by itself (open or not), a try every SelfEvery s, by trust
        internal static float[] SelfChance = { 0f, 0f, 0f, 0.03f, 0.05f, 0.07f, 0.09f, 0.12f };
        internal static float SelfEvery = 10f, SelfAfter = 6f; // (not before it has sat this long)
        internal static float[] AutoChance = { 0f, 0f, 0f, 0.05f, 0.1f, 0.15f, 0.22f, 0.3f }; // by trust: home onto the arm / off from it by itself
        internal static float AutoSitMin = 1.5f, AutoSitMax = 3.5f; // came by itself: this long on the arm, then up to the shoulder
        internal static float HopSeconds = 0.45f, HopUp = 0.12f; // the hop between the shoulder and the arm
        internal static float Along = 0.85f, SeatUp = 0.05f, Yaw = -45f; // the seat: by the wrist, this far from the elbow to the wrist, this high over it, turned (− in towards you: the user, 2026-10-29)
        internal static float Across = 1f; // 1: it stands across the forearm as on a branch, facing you (both feet on it, its tail away from you); 0: by Yaw alone
        internal static float SetOff = 0.35f;               // sitting on the arm this long before it flies off from it
        internal static float FoeRange = 10f, FoeWait = 3f; // an enemy this close this long: back to the shoulder
        internal static float WalkMax = 4.5f;               // m/s: faster and it goes back to the shoulder
        internal static float EyesSeconds = 4.2f;
        internal const int HoldSlot = 1;

        internal const int ActToggle = 0, ActPet = 1, ActFeed = 2, ActEyes = 3, ActToss = 4;
        internal const int ActTrick = 10; // + slot: its own tricks' row (ArmTricks)

        private static int _on = -1;          // your raven on the arm now: 0 Hugin, 1 Munin, −1 none
        private static bool _auto;            // it came by itself: a moment, then back to the shoulder
        private static float _autoUntil = -1f, _foeSince = -1f, _airSince = -1f, _lowerAt = -1f, _eyesUntil = -1f, _look;
        private static bool _other;           // it sits on your other arm (ArmTricks «На іншу руку»)
        private static float _selfAt;          // its next try at a trick of its own
        private static int _pending = -1;     // an action asked before it sat on the arm: done once it does
        private static float _pendingUntil;
        private static string _holdClip;

        // ---------------------------------------------------------------- the bits (any player)

        internal static int Bits(Player p)
        {
            ZDO zdo = NetFlag.Zdo(p, false);
            return zdo != null ? zdo.GetInt(Key, 0) : 0;
        }

        private static void SetBit(Player p, bool munin, int kind, bool on)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null) return;
            int v = zdo.GetInt(Key, 0), b = Bit(munin, kind), n = on ? v | b : v & ~b;
            if (n != v) zdo.Set(Key, n);
        }

        private static void Clear(Player p)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo != null && zdo.GetInt(Key, 0) != 0) zdo.Set(Key, 0);
        }

        // ---------------------------------------------------------------- may it

        // the arm on your raven's side (its shoulder's side: Hugin the right, unless the shoulders were swapped)
        internal static bool LeftArm(Player p, bool munin)
        {
            ShoulderPin pin = PinOf(p, munin);
            return pin != null && pin.Bone != null ? pin.Bone.name.StartsWith("Left") : munin;
        }

        private static ShoulderPin PinOf(Player p, bool munin)
        {
            GameObject m = Shoulder.ModelOf(p, munin);
            return m != null ? m.GetComponent<ShoulderPin>() : null;
        }

        // what that hand holds (a two-handed weapon counts for both)
        private static readonly FieldInfo RightF = AccessTools.Field(typeof(Humanoid), "m_rightItem"), LeftF = AccessTools.Field(typeof(Humanoid), "m_leftItem");
        private static ItemDrop.ItemData Held(Player p, FieldInfo f) { return f != null ? f.GetValue(p) as ItemDrop.ItemData : null; }
        // anything in either hand (a weapon, a shield, a torch, a tool): a falconer's arm with an axe in the other looks wrong (the user)
        private static bool HandsFull(Player p) { return Held(p, RightF) != null || Held(p, LeftF) != null; }

        // your body free for it: on your feet or sitting (a chair, a bench, the sitting emote: the user, 2026-10-29), not in
        // a swing, a block or a dodge, not running, swimming, riding or at a helm
        private static string BodyBusy(Player p)
        {
            if (p.IsDead()) return Lang.T("ти не тут");
            if (p.InWater() || p.IsSwimming()) return Lang.T("ти у воді");
            bool seated = Sits(p);
            if (p.IsRiding() || (!seated && (p.IsAttached() || p.IsSitting() || p.InEmote()))) return Lang.T("руки зайняті");
            if (p.InAttack() || p.IsBlocking() || p.InDodge()) return Lang.T("ти б'єшся");
            if (!seated && (p.IsRunning() || p.GetVelocity().magnitude > WalkMax)) return Lang.T("ти біжиш"); // (a bench on a sailing ship moves)
            if (OdinWings.Flying() || Beast.On() || FateRite.Busy) return Lang.T("не зараз");
            return null;
        }

        // sitting, hands free (not the helm: the hands are on it)
        internal static bool Sits(Player p) { return Gestures.Seated(p) && !(ReferenceEquals(p, Player.m_localPlayer) && Senses.AtHelm); }

        // the arm's gestures, standing or sitting (a seated clip is the same: only the arms and the trunk move)
        internal static bool Gesture(Player p, bool munin, string clip, Gestures.Pri pri) { return Gestures.Play(p, munin, clip, pri, true, Sits(p)); }

        // out in the air on its toss, an outing or an order (not a strike, the dome, a perch): «На руку» calls it back onto the arm
        internal static bool Away(Player p, bool munin)
        {
            GameObject m = Shoulder.ModelOf(p, munin);
            ShoulderPin pin = m != null ? m.GetComponent<ShoulderPin>() : null;
            if (pin == null || !pin.enabled || !pin.InAir || pin.Arriving) return false;
            RavenAct a = AnimDirector.Of(m);
            return a == RavenAct.Outing || a == RavenAct.Command || a == RavenAct.Land;
        }

        // why your raven cannot go onto your arm now (null: it can); callBack: out in the air it may be called back onto it
        internal static string WhyNot(Player p, bool munin, bool callBack = false)
        {
            if (p == null) return Lang.T("ти не тут");
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null) return Lang.T("не на плечі");
            if (!Trust.Active(item)) return Lang.T("ще спить");
            if (Trust.Stranger(item, p)) return Lang.T("не твій");
            if (Trust.Level(item) < MinTrust) return Lang.F("довіра {0}+", MinTrust);
            if (!RavenSkills.Animations) return Lang.T("не зараз"); // (no arm held out without the player's animations)
            string sulk = Quirks.Sulk(p, munin);
            if (sulk != null) return sulk;
            string body = BodyBusy(p);
            if (body != null) return body;
            if (Senses.Fight) return Lang.T("поруч бій");
            ShoulderPin pin = PinOf(p, munin);
            GameObject m = Shoulder.ModelOf(p, munin);
            if (pin == null || !pin.enabled || m == null) return Lang.T("не видно");
            if (Away(p, munin)) return callBack ? null : Lang.T("у польоті");
            if (pin.InAir || pin.Arriving || AnimDirector.Of(m) != RavenAct.Sit) return Lang.T("зайнятий");
            return null;
        }

        // a slot of its own tricks not open yet at its trust (null: it is; the arm row's own: WhyNot)
        internal static string Locked(Player p, bool munin, int what)
        {
            ItemDrop.ItemData item = p != null && what >= ActTrick ? RavenSeat.On(p, munin) : null;
            return item != null && ArmTricks.At(item, munin, what - ActTrick) < 0 ? Lang.F("довіра {0}+", ArmTricks.TrustOfSlot(what - ActTrick)) : null;
        }

        internal static bool Holding(Player p) { return p != null && ReferenceEquals(p, Player.m_localPlayer) && _on >= 0; }
        internal static bool Holding(Player p, bool munin) { return Holding(p) && _on == (munin ? 1 : 0); }
        internal static bool OnOther { get { return _on >= 0 && _other; } }

        // ---------------------------------------------------------------- onto the arm and off

        // the arm out, the bird onto it; auto: by itself (only with your hands empty, nothing put away)
        private static bool Up(Player p, bool munin, bool auto)
        {
            if (_on == (munin ? 1 : 0)) { if (!auto) _auto = false; return true; }
            if (_on >= 0) Down(p, null);
            bool left = LeftArm(p, munin);
            if (HandsFull(p))
            {
                if (auto) return false;
                p.HideHandItems(); // as the game's own R: both hands put away, R takes them out again
            }
            _on = munin ? 1 : 0; _auto = auto; _other = false; _selfAt = Time.time + SelfEvery; _autoUntil = -1f; _foeSince = -1f; _airSince = -1f; _lowerAt = -1f;
            ShoulderPin pin = PinOf(p, munin);
            _holdClip = ArmClip(left, pin != null && (pin.InAir || pin.Landing));
            BodyClips.Play(p, HoldSlot, _holdClip, 0.45f, 1f);
            SetBit(p, munin, OnBit, true);
            return true;
        }

        // the arm held out: up and straighter to catch it while it flies to it (and lands), the hold once it sits (the
        // user: «я ж ловлю його»)
        private static string ArmClip(bool left, bool catching) { return catching ? (left ? "arm_catch_l" : "arm_catch_r") : (left ? "arm_hold_l" : "arm_hold_r"); }
        internal static float CatchFade = 0.35f, LowerFade = 0.7f; // up to catch quick; down with the bird slow

        // back to the shoulder (why: a word for it, null none); the arm comes down as it hops off
        internal static void Down(Player p, string why)
        {
            if (p == null || _on < 0) return;
            bool munin = _on == 1;
            _on = -1; _auto = false; _other = false; _pending = -1; _eyesUntil = -1f;
            Clear(p);
            _lowerAt = Time.time + HopSeconds * 0.6f;
            StopArmGesture(p);
            if (why != null) PerkFx.Say(munin, why, 0.6f, 3f);
        }

        // «На іншу руку» (ArmTricks): it flutters over to your other forearm and stays; again: back
        internal static bool SwapArm(Player p, bool munin)
        {
            if (!Holding(p, munin)) return false;
            _other = !_other;
            SetBit(p, munin, OtherBit, _other);
            ShoulderPin pin = PinOf(p, munin);
            if (pin != null && pin.Life != null) pin.Life.Flap(HopSeconds * 1.2f);
            return true;
        }

        // the stroke, the food or the eyes on the gestures' layer: gone with the bird
        private static void StopArmGesture(Player p)
        {
            BodyAnimator a = BodyAnimator.Of(p);
            string c = a != null ? a.ClipIn(2) : null;
            if (c != null && c.StartsWith("arm_", System.StringComparison.Ordinal) && !c.StartsWith("arm_toss", System.StringComparison.Ordinal)) BodyClips.Stop(p, 2, 0.25f);
        }

        // the director gave the bird to something that flies it (AnimDirector.Take, from sitting): off the arm with a toss
        internal static void Launched(GameObject m)
        {
            ShoulderPin pin = m != null ? m.GetComponent<ShoulderPin>() : null;
            if (pin == null || !ReferenceEquals(pin.Player, Player.m_localPlayer)) return;
            Player p = pin.Player;
            if (_on != (pin.Munin ? 1 : 0)) { if ((Bits(p) & Bit(pin.Munin, StageBit)) != 0) SetBit(p, pin.Munin, StageBit, false); return; }
            bool on = pin.ArmW > 0.4f;
            _on = -1; _auto = false; _other = false; _pending = -1; _eyesUntil = -1f;
            Clear(p);
            if (on && Gesture(p, pin.Munin, "arm_toss_", Gestures.Pri.Moment)) _lowerAt = Time.time + 0.3f; // (the toss holds the arm: the hold fades under it)
            else _lowerAt = Time.time;
        }

        // a gift asked of the raven on the arm: if it does not fly off with it at once, it goes back to the shoulder for it
        internal static void GiftUsed(Player p, bool munin)
        {
            if (!Holding(p, munin)) return;
            _giftCheck = Time.time + 0.3f;
        }
        private static float _giftCheck = -1f;

        // ---------------------------------------------------------------- by itself (the pin and the orders ask)

        // your hand, your pace and the place let it come onto the arm by itself (your own game)
        private static bool AutoMay(Player p, bool munin)
        {
            if (!ReferenceEquals(p, Player.m_localPlayer) || _on >= 0 || !RavenSkills.Animations) return false;
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null || !Trust.Active(item) || Trust.Stranger(item, p)) return false;
            int lvl = Trust.Level(item);
            float chance = lvl >= 0 && lvl < AutoChance.Length ? AutoChance[lvl] : AutoChance[AutoChance.Length - 1];
            if (Random.value >= chance) return false;
            if (BodyBusy(p) != null || Senses.Fight || (!p.IsOnGround() && !Sits(p)) || p.IsCrouching() || HandsFull(p)) return false;
            return Quirks.Sulk(p, munin) == null;
        }

        // an order or an outing about to fly it off (its owner's game, the same moment it is written): now and then it steps
        // onto your arm first and goes from there; on the arm already, it goes from there anyway. True: from the arm.
        internal static bool Stage(Player p, bool munin)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer)) return false;
            if (_on == (munin ? 1 : 0)) { SetBit(p, munin, StageBit, true); return true; }
            ShoulderPin pin = PinOf(p, munin);
            GameObject m = Shoulder.ModelOf(p, munin);
            if (pin == null || m == null || pin.InAir || AnimDirector.Of(m) != RavenAct.Sit || !AutoMay(p, munin)) return false;
            if (!Up(p, munin, true)) return false;
            SetBit(p, munin, StageBit, true);
            return true;
        }

        // every game's pin, before it flies off for an order or an outing: still stepping onto the arm (or just sat on it)
        internal static bool Waiting(ShoulderPin pin)
        {
            if (pin.StagedSeen < 0f || Time.time - pin.StagedSeen > 2.5f) return false; // (never held for long: the owner's game may have gone on)
            return pin.ArmW < 1f || Time.time - pin.ArmFullAt < SetOff;
        }

        // the pin on its way home (an outing, an order): now and then it comes onto the arm you hold out for it
        internal static void Homing(ShoulderPin pin)
        {
            if (pin == null || pin.Player == null || !AutoMay(pin.Player, pin.Munin)) return;
            Up(pin.Player, pin.Munin, true);
        }

        // ---------------------------------------------------------------- what you do with it there (J)

        // a button of the row: onto the arm or back; the rest put it on the arm first if it is not there yet
        internal static void Act(Player p, bool munin, int what)
        {
            if (p == null) return;
            string locked = Locked(p, munin, what);
            if (locked != null) { RavenMsg.Center(p, Lang.F("{0}: {1}", Plugin.RavenName(munin), locked)); return; }
            if (what == ActToggle)
            {
                if (Holding(p, munin)) { Down(p, null); return; }
                string why = WhyNot(p, munin, true);
                if (why != null) { RavenMsg.Center(p, Lang.F("{0}: {1}", Plugin.RavenName(munin), why)); return; }
                bool away = Away(p, munin);
                if (!Up(p, munin, false)) return;
                if (away) CallBack(p, munin); // (its way home ends on the arm held out for it)
                else PerkFx.Say(munin, "arm_up", 0.35f, 2f);
                return;
            }
            if (!Holding(p, munin))
            {
                string why = WhyNot(p, munin);
                if (why != null) { RavenMsg.Center(p, Lang.F("{0}: {1}", Plugin.RavenName(munin), why)); return; }
                if (!Up(p, munin, false)) return;
                _pending = what; _pendingUntil = Time.time + 3f;
                return;
            }
            _auto = false; // (you took it in hand: it stays)
            Do(p, munin, what);
        }

        // out on an order or an outing: it ends now and the raven comes home (onto the arm: its bit is set)
        private static void CallBack(Player p, bool munin)
        {
            if (Orders.Of(p, munin) != Orders.None) Orders.Return(p, munin);
            else if (Outing.Of(p, munin) != Outing.None) Outing.Interrupted(Shoulder.ModelOf(p, munin));
        }

        internal static float StrokeCounts = 60f; // a stroke counts for its habit at most this often
        private static readonly float[] _strokedAt = { 0f, 0f };

        private static void Do(Player p, bool munin, int what)
        {
            if (what >= ActTrick) { ArmTricks.Do(p, munin, ArmTricks.At(RavenSeat.On(p, munin), munin, what - ActTrick), true); return; }
            Core(p, munin, what);
        }

        // the arm row's own (and the stroke and the eyes, which are tricks of the pool now: ArmTricks); yours: not its own
        // whim (a stroke counts for its habit only when you give it)
        internal static bool Core(Player p, bool munin, int what, bool yours = true)
        {
            ShoulderLife life = Shoulder.LifeOf(p, munin);
            switch (what)
            {
                case ActPet:
                    if (!Gesture(p, munin, "arm_pet_", Gestures.Pri.Answer)) return false;
                    if (life != null && life.SitNew) life.SitDirectorNow.React(life, "Підставляє шию", 0.6f);
                    if (yours && Time.time >= _strokedAt[munin ? 1 : 0]) { _strokedAt[munin ? 1 : 0] = Time.time + StrokeCounts; Habits.Stroked(p, munin); } // («Мазунчик»: not a button to hammer)
                    PerkFx.Say(munin, "arm_pet", 0.5f, 2f);
                    break;
                case ActFeed:
                {
                    Trust.Food food = FoodInBag(p, munin);
                    if (food == null) { RavenMsg.Center(p, Lang.F("Нема в сумці нічого, що {0} їсть.", Plugin.RavenName(munin))); return false; }
                    Gesture(p, munin, "arm_feed_", Gestures.Pri.Answer);
                    string msg = Trust.Feed(p, munin, food);
                    if (!string.IsNullOrEmpty(msg)) RavenMsg.Center(p, msg);
                    break;
                }
                case ActEyes:
                    if (!Gesture(p, munin, "arm_eyes_", Gestures.Pri.Answer)) return false;
                    _eyesUntil = Time.time + EyesSeconds;
                    SetBit(p, munin, EyesBit, true);
                    if (life != null && life.SitNew) life.SitDirectorNow.React(life, "Досліджує тебе", 1f);
                    PerkFx.Say(munin, Random.value < 0.5f ? "arm_eyes_warm" : "arm_eyes", 1f, 0.5f); // (open only at the top trust: warm or wry, by turns)
                    break;
                case ActToss:
                {
                    string no = TossWhy(p, munin);
                    if (no != null) { RavenMsg.Center(p, Lang.F("{0}: {1}", Plugin.RavenName(munin), no)); return false; }
                    if (Outing.Of(p, munin) != Outing.None || Orders.Of(p, munin) != Orders.None) return false;
                    int mode = LooksFar(p) ? Outing.Scout : Random.value < 0.5f ? Outing.CircleRight : Outing.CircleLeft;
                    if (!Outing.Start(p, munin, mode, true)) return false; // (the pin flies it off; the toss on its way: Launched)
                    Power.Spend(p, munin, Orders.CostOf(munin, Orders.Circle)); // as «Кружляй» (the user, 2026-10-29): its strength (TossWhy saw it has it), its hunger (paid once it is off)
                    Satiety.Flew(munin, Orders.HungerOf(Orders.Circle));
                    PerkFx.Say(munin, "arm_toss", 0.4f, 1f);
                    break;
                }
            }
            return true;
        }

        // why the toss would not fly now: an outing is called off at once under a roof, in a fight, with your health low
        // (Outing.Tick), and the bird would just sit there
        internal static string TossWhy(Player p, bool munin)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item != null && Satiety.Zone(item) <= Satiety.Hungry) return Trust.Starving(item) ? Lang.T("голодує — не полетить") : Lang.T("голодний — не полетить");
            if (item != null && Power.Get(item) < Power.Price(munin, Orders.CostOf(munin, Orders.Circle))) return Lang.T("мало сили");
            if (Outing.Indoors()) return Lang.T("під дахом не полетить");
            if (Senses.Fight) return Lang.T("поруч бій");
            if (Senses.Health01 < 0.5f) return Lang.T("мало здоров'я");
            return null;
        }

        // the food it eats that you have: its favourite if you know it, else what you have most of
        private static Trust.Food FoodInBag(Player p, bool munin)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null) return null;
            Trust.Food best = null;
            int most = 0;
            foreach (Trust.Food f in Trust.EdibleFoods(munin, item))
            {
                int n = Trust.Have(p, f.Prefab);
                if (n <= 0) continue;
                if (Satiety.KnownFav(item, f.Prefab)) return f;
                if (n > most) { most = n; best = f; }
            }
            return best;
        }

        // you look far off (nothing within 25 m where you aim): the toss sends it scouting ahead, else round over you
        private static bool LooksFar(Player p)
        {
            Transform cam = GameCamera.instance != null ? GameCamera.instance.transform : null;
            if (cam == null) return false;
            RaycastHit hit;
            return !Physics.Raycast(cam.position, cam.forward, out hit, 25f, RavenMotor.Mask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- every frame (your own game)

        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null) { _on = -1; return; }
            if (_lowerAt > 0f && Time.time >= _lowerAt)
            {
                _lowerAt = -1f;
                BodyAnimator a = BodyAnimator.Of(p);
                if (_on < 0 && a != null && a.ClipIn(HoldSlot) != null && (a.ClipIn(HoldSlot).StartsWith("arm_hold", System.StringComparison.Ordinal) || a.ClipIn(HoldSlot).StartsWith("arm_catch", System.StringComparison.Ordinal))) BodyClips.Stop(p, HoldSlot, 0.45f);
            }
            if (_on < 0) { Hints(p); return; }
            bool munin = _on == 1;
            GameObject m = Shoulder.ModelOf(p, munin);
            ShoulderPin pin = m != null ? m.GetComponent<ShoulderPin>() : null;
            if (pin == null || RavenSeat.On(p, munin) == null) { Down(p, null); return; }
            // you first: your hand, your body
            if (HandsFull(p)) { Down(p, "arm_off_weapon"); return; } // (anything taken in hand: R, the hotbar)
            if (Time.time - Senses.HurtByFoeAt < 0.4f) { if (pin.Life != null) pin.Life.Flap(0.6f); Down(p, "arm_off_hurt"); return; } // (a blow from someone: not the cold or a poison tick, which knocked it off over and over)
            if (BodyBusy(p) != null || p.IsCrouching()) { Down(p, null); return; }
            if (!p.IsOnGround() && !Sits(p)) { if (_airSince < 0f) _airSince = Time.time; else if (Time.time - _airSince > 0.25f) { Down(p, null); return; } }
            else _airSince = -1f;
            bool foe = Senses.Foe != null && Vector3.Distance(Senses.Foe.transform.position, p.transform.position) < FoeRange;
            if (!foe) _foeSince = -1f;
            else if (_foeSince < 0f) _foeSince = Time.time;
            else if (Time.time - _foeSince > FoeWait) { Down(p, "arm_off_foe"); return; }
            if (_eyesUntil > 0f && Time.time >= _eyesUntil) { _eyesUntil = -1f; SetBit(p, munin, EyesBit, false); }
            bool sits = !pin.InAir && !pin.Landing && pin.ArmW >= 1f && AnimDirector.Of(m) == RavenAct.Sit;
            string arm = ArmClip(LeftArm(p, munin) ^ _other, pin.InAir || pin.Landing);
            if (arm != _holdClip) { _holdClip = arm; BodyClips.Play(p, HoldSlot, arm, arm.StartsWith("arm_catch", System.StringComparison.Ordinal) ? CatchFade : LowerFade, 1f); }
            if (_giftCheck > 0f && Time.time >= _giftCheck) { _giftCheck = -1f; if (sits) { Down(p, null); return; } }
            if (_pending >= 0)
            {
                if (Time.time > _pendingUntil) _pending = -1;
                else if (sits && Time.time - pin.ArmFullAt > 0.2f) { int w = _pending; _pending = -1; Do(p, munin, w); }
            }
            if (!_auto && sits && Time.time >= _selfAt) // its own whim: one of the ten, open or not, now and then
            {
                _selfAt = Time.time + SelfEvery;
                ItemDrop.ItemData item = RavenSeat.On(p, munin);
                int lvl = item != null ? Trust.Level(item) : 0;
                BodyAnimator pa = BodyAnimator.Of(p);
                if (Time.time - pin.ArmFullAt > SelfAfter && (pa == null || !pa.Playing(2)) && Random.value < SelfChance[Mathf.Clamp(lvl, 0, SelfChance.Length - 1)])
                    ArmTricks.Do(p, munin, Random.Range(0, ArmTricks.Count), false);
            }
            if (_auto && sits && (Bits(p) & Bit(munin, StageBit)) == 0)
            {
                if (_autoUntil < 0f) { _autoUntil = Time.time + Random.Range(AutoSitMin, AutoSitMax); PerkFx.Say(munin, "arm_land", 0.3f, 3f); if (pin.Life != null && pin.Life.SitNew) pin.Life.SitDirectorNow.React(pin.Life, "Обтрушується", 0.3f); }
                else if (Time.time >= _autoUntil) Down(p, null);
            }
        }

        // a level before: now and then it hints at your arm; from the level on: once, that it may sit on it now
        private const string ToldKey = "RavenWatch_armTold";
        private static void Hints(Player p)
        {
            if (Time.time < _look) return;
            _look = Time.time + 5f;
            for (int k = 0; k < 2; k++)
            {
                bool munin = k == 1;
                ItemDrop.ItemData item = RavenSeat.On(p, munin);
                GameObject m = Shoulder.ModelOf(p, munin);
                if (item == null || m == null || !RavenSkills.Animations || !Trust.Active(item) || Trust.Stranger(item, p) || AnimDirector.Of(m) != RavenAct.Sit || Senses.Fight) continue;
                int lvl = Trust.Level(item);
                if (lvl >= MinTrust)
                {
                    string told;
                    if (item.m_customData != null && item.m_customData.TryGetValue(ToldKey, out told)) { if (ArmTricks.Announce(p, munin, item)) return; continue; }
                    if (!PerkFx.Say(munin, "arm_open", 1f, 0f)) continue;
                    if (item.m_customData == null) item.m_customData = new System.Collections.Generic.Dictionary<string, string>();
                    item.m_customData[ToldKey] = "1";
                    return;
                }
                if (lvl == HintTrust && PerkFx.Say(munin, "arm_hint", 0.35f, 20f)) return; // (a try every 20 min: about once an hour)
            }
        }

        // the studio: arm <up|down|toggle|pet|feed|eyes|toss|t0..t4|why|bits> [h|m] (toggle: the button itself, calling back from the air too; tN: its own trick in slot N)
        internal static string Studio(string what, bool munin)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return "no player";
            switch (what)
            {
                case "up": return Up(p, munin, false) ? "on the arm" : "could not";
                case "down": Down(p, null); return "back to the shoulder";
                case "toggle": Act(p, munin, ActToggle); return "toggle: " + (Holding(p, munin) ? "on the arm" : "off");
                case "pet": Act(p, munin, ActPet); return "pet";
                case "feed": Act(p, munin, ActFeed); return "feed";
                case "eyes": Act(p, munin, ActEyes); return "eyes";
                case "toss": Act(p, munin, ActToss); return "toss";
                case "t0": case "t1": case "t2": case "t3": case "t4": Act(p, munin, ActTrick + (what[1] - '0')); return "trick slot " + what[1] + ": " + ArmTricks.At(RavenSeat.On(p, munin), munin, what[1] - '0');
                case "why": return WhyNot(p, munin) ?? "may";
                case "bits": return "bits " + Bits(p) + ", on " + _on + (_auto ? " (auto)" : "");
            }
            return "arm up|down|toggle|pet|feed|eyes|toss|t0..t4|why|bits [h|m]";
        }
    }
}
#endif
