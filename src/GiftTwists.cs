#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The gifts' second golden level (GiftUp: «II — a move of its own», besides a little cheaper and sooner): each gift
    // gets a new way to be used. The ones that sit inside a gift are written there (RavenSkills, Shadow, Leap, Decoy,
    // Dome, Strike, Ward, PairGifts…); the ones that act by themselves, the shared messages and the patches are here:
    //   scout     — Hugin scouts half a reach ahead, where you look (RavenSkills.Scout)
    //   light     — put out: a blinding flash
    //   leap      — a second wing beat in the air
    //   shadow    — the friend next to you hides too
    //   dome      — the foes inside are thrown out
    //   hstrike   — Hugin snatches a thing off a living foe (Strike.Hit)
    //   hdecoy    — Hugin leads the fooled one where you looked (Decoy)
    //   hflight   — bows, crossbows and staffs shoot in flight
    //   lightstep — the second wind: once, out of breath, half the stamina back
    //   lastchance— the one who nearly killed you hits you half as hard for a while
    //   mstrike   — Munin tears a trophy off the one his blow killed (Strike.Hit)
    //   anger     — catching: the friends near are angry too
    //   bloodfury — nothing staggers you in it
    //   scream    — an echo two seconds later
    //   fury      — every third blow in a row, untouched, hits harder
    //   wings     — the wind behind you carries the friends near
    //   heal      — it washes off poison, burning, frost and wet
    //   ward      — a close blow it took comes half back into the striker
    //   mberserk  — the running beast tramples the small ones down
    //   mtame     — a beast tamed with him is tougher
    //   pwhirl    — the whirl draws foes in
    //   pclear    — the plants under the clear sky grow faster
    //   pload     — the ravens pick up what lies around
    // And what level III adds to the facets of Facets.cs where it is one more thing (scout runes, lit foes, glide
    // breath, dome's end, decoy's peck, blood for blood, broken guard, fury fed by kills, sails, heal into stamina,
    // the ward's shards, the beast's roar, the clear moon).
    internal static class Twists
    {
        internal static bool Two(string id) { return GiftUp.Level(id) >= 2; }

        // ---------------------------------------------------------------- the numbers (the balance, in one place)
        internal static float ScoutAhead = 0.5f;                  // scout: the circle's middle this share of its reach ahead
        internal static float ScoutRunes = 10f;                   // scout III: s of runes over the marked ones
        internal static float FlashReach = 8f, FlashBlind = 3f, FlashBlindBoss = 1f; // light II
        internal static float LitReach = 12f, LitMul = 1.1f;      // light III: your blows on the ones in your light
        internal static float SecondFlap = 0.6f;                  // leap II: the second beat, of the first jump's speed
        internal static float GlideBreath = 6f;                   // leap III: stamina a second while gliding
        internal static float ShareReach = 6f;                    // shadow II, anger II: how near the friend stands (anger: AngerReach)
        internal static float AngerReach = 15f, AngerShareSeconds = 60f;
        internal static float StealChance = 0.2f;                 // hstrike II: once per creature
        internal static float TrophyChance = 0.25f;               // mstrike II: once per creature
        internal static float LessonSeconds = 10f, LessonMul = 0.5f; // lastchance II
        internal static float BreathShare = 0.5f;                 // lightstep II
        internal static float EchoAfter = 2f;                     // scream II
        internal static float GuardBreak = 10f;                   // scream III: s without a block
        internal static int RhythmEvery = 3; internal static float RhythmMul = 1.5f, RhythmGap = 4f; // fury II
        internal static float FuryKillAdd = 3f, FuryKillMax = 15f; // fury III
        internal static float TrailReach = 8f, TrailShare = 0.5f; // wings II
        internal static float SailMul = 1.2f;                     // wings III
        internal static float HealBreath = 0.03f;                 // heal III: of max stamina every half second once whole
        internal static float Thorns = 0.5f, ThornsReach = 4f;    // ward II
        internal static float ShardReach = 6f, ShardShare = 2f;   // ward III: twice Munin's blow, ahead
        internal static float TrampleReach = 1.8f, TrampleSpeed = 5f, TrampleShare = 0.5f, TrampleGap = 2f; // mberserk II
        internal static float RoarReach = 10f;                    // mberserk III
        internal static float TameTough = 1.2f;                   // mtame II: tamed with Munin takes damage / this
        internal static float WhirlPull = 25f;                    // pwhirl II
        internal static float GrowMul = 1.5f;                     // pclear II
        internal static float PickupReach = 12f;                  // pload II
        internal static float DomeOut = 3f;                       // dome III: the burst reaches this past the edge

        private const string RpcShadow = "RavenWatch_ShadowShare", RpcAnger = "RavenWatch_AngerShare", RpcGuard = "RavenWatch_NoGuard";
        internal const string TrailKey = "RavenWatch_WindTrail", SailKey = "RavenWatch_WindSail", TameKey = "RavenWatch_MuninTamed";
        internal const string GrowKey = "RavenWatch_ClearGrow", MoonKey = "RavenWatch_ClearMoon";

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcShadow, new Action<long, ZPackage>(OnShadow));
            ZRoutedRpc.instance.Register<ZPackage>(RpcAnger, new Action<long, ZPackage>(OnAnger));
            ZRoutedRpc.instance.Register<ZPackage>(RpcGuard, new Action<long, ZPackage>(OnGuard));
        }

        internal static void Reset() { _lessonFoe = null; _rhythm = 0; _furyAdded = 0f; _breathUsed = false; NoGuard.Clear(); Robbed.Clear(); Trophied.Clear(); Trampled.Clear(); Moon(false); }

        // every frame (RavenSkills.Update)
        internal static void Frame()
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            Leap.Second(p);
            Breath(p);
            Trample(p);
            Trail(p);
            MoonTick(p);
        }

        private static bool Friend(Player me, Player pl)
        {
            return pl != null && pl != me && !pl.IsDead() && !(me.IsPVPEnabled() && pl.IsPVPEnabled());
        }

        // ---------------------------------------------------------------- light

        // II: the light put out in a flash — the foes near are blinded
        internal static void Flash(Player p)
        {
            int n = 0;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Strike.Prey(p, c) || Vector3.Distance(c.transform.position, p.transform.position) > FlashReach) continue;
                Marks.BlindFor(c, c.IsBoss() ? FlashBlindBoss : FlashBlind);
                n++;
            }
            BindFx.Nova(p, "light", p.transform.position + Vector3.up * 1.6f, FlashReach, 0.35f);
            if (n > 0) p.Message(MessageHud.MessageType.TopLeft, Lang.F("Сліпучий блиск: осліплено {0}", n));
        }

        // ---------------------------------------------------------------- shadow

        // II: the nearest friend within ShareReach is hidden as long as you
        internal static void ShareShadow(Player p, float seconds)
        {
            if (ZRoutedRpc.instance == null) return;
            Player best = null; float bd = ShareReach;
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (!Friend(p, pl)) continue;
                float d = Vector3.Distance(pl.transform.position, p.transform.position);
                if (d < bd) { bd = d; best = pl; }
            }
            if (best == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(best.GetPlayerID());
            pkg.Write(seconds);
            pkg.Write(p.GetPlayerName());
            ZRoutedRpc.instance.InvokeRoutedRPC(best.GetOwner(), RpcShadow, pkg);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Тінь накрила й {0}", best.GetPlayerName()));
        }

        private static void OnShadow(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            long pid = pkg.ReadLong();
            float seconds = Mathf.Clamp(pkg.ReadSingle(), 1f, 60f);
            string from = pkg.ReadString();
            if (me == null || me.IsDead() || me.GetPlayerID() != pid || !FromNear(me, sender, ShareReach + 6f)) return;
            if (Shadow.On(me)) return; // (a shadow of your own, or the в'язь's: it keeps its own time)
            Shadow.Start(me, seconds);
            me.Message(MessageHud.MessageType.Center, Lang.F("Хугін гравця {0} сховав у тінь і тебе", from));
        }

        // the message came from a player standing by you
        private static bool FromNear(Player me, long sender, float reach)
        {
            foreach (Player pl in Player.GetAllPlayers())
                if (pl != null && pl != me && pl.GetOwner() == sender && Vector3.Distance(pl.transform.position, me.transform.position) <= reach) return true;
            return false;
        }

        // ---------------------------------------------------------------- dome

        // II: whoever is inside when it rises is thrown out to the edge
        internal static void DomeClear(Player p, Vector3 at, float radius)
        {
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Strike.Prey(p, c) || c.IsBoss()) continue;
                Vector3 d = c.transform.position - at; d.y = 0f;
                if (d.magnitude > radius) continue;
                RavenSkills.Push(p, c, d.sqrMagnitude > 0.01f ? d : c.transform.forward, 80f);
                c.Stagger(d.normalized);
            }
        }

        // III: the dome ends in a burst of runes — the ones prowling round the edge are knocked down and thrown back
        internal static void DomeFinale(Player p, Vector3 at, float radius)
        {
            float r = radius + DomeOut;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Strike.Prey(p, c)) continue;
                Vector3 d = c.transform.position - at; d.y = 0f;
                if (d.magnitude > r) continue;
                c.Stagger(d.sqrMagnitude > 0.01f ? d.normalized : c.transform.forward);
                if (!c.IsBoss()) RavenSkills.Push(p, c, d.sqrMagnitude > 0.01f ? d : c.transform.forward, 60f);
            }
            BindFx.Nova(p, "runeshield", at + Vector3.up * 0.5f, r, 0.6f);
        }

        // ---------------------------------------------------------------- the strikes' spoils

        private static readonly HashSet<ZDOID> Robbed = new HashSet<ZDOID>(), Trophied = new HashSet<ZDOID>();

        // hstrike II: Hugin snatches one of its things off a living foe (once per creature; not a boss, not its trophy)
        internal static void Snatch(Player p, Character c)
        {
            if (c == null || c.IsBoss() || c.IsDead() || !Two("hstrike") || Robbed.Contains(c.GetZDOID())) return;
            Robbed.Add(c.GetZDOID());
            if (Robbed.Count > 256) Robbed.Clear();
            if (UnityEngine.Random.value >= StealChance) return;
            GameObject prefab = Drop(c, false);
            if (prefab == null) return;
            Spawn(prefab, c);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Хугін поцупив: {0}", Name(prefab)));
        }

        // mstrike II: the creature Munin struck fell — now and then he tears a trophy off it (once per creature)
        internal static void TrophyWatch(Player p, Character c)
        {
            if (c == null || c.IsBoss() || !Two("mstrike") || Trophied.Contains(c.GetZDOID()) || Plugin.Instance == null) return;
            Trophied.Add(c.GetZDOID());
            if (Trophied.Count > 256) Trophied.Clear();
            GameObject prefab = Drop(c, true);
            if (prefab == null) return;
            Vector3 at = c.transform.position;
            Plugin.Instance.StartCoroutine(TrophyLater(p, c, prefab, at));
        }

        private static System.Collections.IEnumerator TrophyLater(Player p, Character c, GameObject prefab, Vector3 at)
        {
            yield return new WaitForSeconds(0.6f);
            bool dead = c == null || c.GetHealth() <= 0f;
            if (!dead || p == null || UnityEngine.Random.value >= TrophyChance) yield break;
            UnityEngine.Object.Instantiate(prefab, at + Vector3.up * 0.8f, Quaternion.identity);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Мунін вирвав трофей: {0}", Name(prefab)));
        }

        // one of its drops: a trophy, or anything but its trophy
        private static GameObject Drop(Character c, bool trophy)
        {
            CharacterDrop cd = c.GetComponent<CharacterDrop>();
            if (cd == null || cd.m_drops == null) return null;
            List<GameObject> pick = new List<GameObject>();
            foreach (CharacterDrop.Drop d in cd.m_drops)
            {
                if (d == null || d.m_prefab == null || d.m_chance <= 0f) continue;
                bool isTrophy = d.m_prefab.name.StartsWith("Trophy", StringComparison.Ordinal);
                if (isTrophy == trophy) pick.Add(d.m_prefab);
            }
            return pick.Count > 0 ? pick[UnityEngine.Random.Range(0, pick.Count)] : null;
        }

        private static void Spawn(GameObject prefab, Character c)
        {
            Vector3 at = c.transform.position + Vector3.up * 1f + UnityEngine.Random.insideUnitSphere * 0.3f;
            UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity);
        }

        private static string Name(GameObject prefab)
        {
            ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return d != null ? Lang.Game(d.m_itemData.m_shared.m_name) : prefab != null ? prefab.name : "";
        }

        // ---------------------------------------------------------------- light step, last chance

        private static bool _breathUsed;

        // lightstep II: out of breath in the light step — once a time, half of it back
        private static void Breath(Player p)
        {
            if (!p.GetSEMan().HaveStatusEffect(LightStepHash)) { _breathUsed = false; return; }
            if (_breathUsed || p.GetStamina() > 1f || !Two("lightstep")) return;
            _breathUsed = true;
            p.AddStamina(p.GetMaxStamina() * BreathShare);
            p.Message(MessageHud.MessageType.TopLeft, Lang.T("Друге дихання: Хугін підказав ритм"));
        }
        private static readonly int LightStepHash = WeakPassive.StepName.GetStableHashCode();

        // lastchance II: the one who nearly killed you hits you half as hard for a while
        private static Character _lessonFoe;
        private static float _lessonUntil;
        internal static void Lesson(Player p, Character foe)
        {
            if (foe == null || !Two("lastchance")) return;
            _lessonFoe = foe;
            _lessonUntil = Time.time + LessonSeconds;
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Наука Хугіна: {0} б'є тебе вдвічі слабше, {1} с", Lang.Game(foe.m_name), Mathf.RoundToInt(LessonSeconds)));
        }
        internal static float TakenMul(Character attacker) { return attacker != null && attacker == _lessonFoe && Time.time < _lessonUntil ? LessonMul : 1f; }

        // ---------------------------------------------------------------- blood fury

        // III: blood for blood — the one whose blow threw you into the fury starts to bleed (Munin's wound)
        internal static void BloodForBlood(Player p)
        {
            if (!Facets.On("bloodfury")) return;
            Character foe = WeakPassive.LastFoe;
            if (foe == null || foe.IsDead() || Time.time - WeakPassive.LastHit > 2f || !Strike.Prey(p, foe)) return;
            ItemDrop.ItemData munin = Trust.Worn(true);
            if (munin == null) return;
            Marks.Put(foe, true, null, false);
            Marks.StartBleed(p, foe, Strike.Blow(true, munin), new Dictionary<Character, float>(), null, null);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Кров за кров: {0} стікає кров'ю", Lang.Game(foe.m_name)));
        }

        // ---------------------------------------------------------------- anger

        // II: Munin's anger catches: the friends near get it too (their own game makes it)
        internal static void ShareAnger(Player p, float pct, float seconds)
        {
            if (!Two("anger") || ZRoutedRpc.instance == null) return;
            int n = 0;
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (!Friend(p, pl) || Vector3.Distance(pl.transform.position, p.transform.position) > AngerReach) continue;
                ZPackage pkg = new ZPackage();
                pkg.Write(pl.GetPlayerID());
                pkg.Write(pct);
                pkg.Write(Mathf.Min(seconds, AngerShareSeconds));
                pkg.Write(p.GetPlayerName());
                ZRoutedRpc.instance.InvokeRoutedRPC(pl.GetOwner(), RpcAnger, pkg);
                n++;
            }
            if (n > 0) p.Message(MessageHud.MessageType.TopLeft, Lang.F("Злість Муніна передалась ще {0}", n));
        }

        private static void OnAnger(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            long pid = pkg.ReadLong();
            float pct = Mathf.Clamp(pkg.ReadSingle(), 0f, 50f), seconds = Mathf.Clamp(pkg.ReadSingle(), 1f, AngerShareSeconds);
            string from = pkg.ReadString();
            if (me == null || me.IsDead() || me.GetPlayerID() != pid || !FromNear(me, sender, AngerReach + 6f)) return;
            if (WeakPassive.AngerOn(me)) return; // (your own anger is not cut short)
            WeakPassive.AngerFrom(me, pct, seconds, from);
        }

        // ---------------------------------------------------------------- scream

        // II: the scream comes again from the same place
        internal static void Echo(Player p, Vector3 at, float reach)
        {
            if (!Two("scream") || Plugin.Instance == null) return;
            Plugin.Instance.StartCoroutine(EchoLater(p, at, reach));
        }

        private static System.Collections.IEnumerator EchoLater(Player p, Vector3 at, float reach)
        {
            yield return new WaitForSeconds(EchoAfter);
            if (p == null || p.IsDead()) yield break;
            RavenSkills.ScreamWave(p, at, reach, true);
        }

        // III: the ones who heard it cannot block for a while (their owners' games hold it)
        private static readonly Dictionary<ZDOID, float> NoGuard = new Dictionary<ZDOID, float>();
        internal static void BreakGuard(List<Character> who)
        {
            if (!Facets.On("scream") || who.Count == 0 || ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(GuardBreak);
            pkg.Write(Mathf.Min(who.Count, 32));
            for (int i = 0; i < who.Count && i < 32; i++) pkg.Write(who[i].GetZDOID());
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcGuard, pkg);
        }

        private static void OnGuard(long sender, ZPackage pkg)
        {
            float secs = Mathf.Clamp(pkg.ReadSingle(), 0f, 30f);
            int n = Mathf.Min(pkg.ReadInt(), 32);
            if (NoGuard.Count > 64) NoGuard.Clear();
            for (int i = 0; i < n; i++) NoGuard[pkg.ReadZDOID()] = Time.time + secs;
        }
        internal static bool GuardBroken(Character c) { float t; return c != null && NoGuard.TryGetValue(c.GetZDOID(), out t) && Time.time < t; }

        // ---------------------------------------------------------------- your blows (fury, the light)

        private static int _rhythm;
        private static float _rhythmAt, _furyAdded;

        // a blow of yours at a foe, before it lands: what it is worth now
        internal static void YourBlow(Player me, Character target, HitData hit)
        {
            float mul = 1f;
            if (RavenLight.On(me) && Facets.On("light") && Vector3.Distance(target.transform.position, me.transform.position) <= LitReach) mul *= LitMul; // light III
            if (Fury.On(me) && Two("fury"))
            {
                if (Time.time - _rhythmAt > RhythmGap || WeakPassive.LastHit > _rhythmAt) _rhythm = 0; // (a pause, or a blow taken: the rhythm is lost)
                if (Time.time - _rhythmAt > 0.3f) // a new swing (one swing through many foes is one beat)
                {
                    _rhythm++;
                    _rhythmAt = Time.time;
                    _rhythmBeat = _rhythm >= RhythmEvery;
                    if (_rhythmBeat)
                    {
                        _rhythm = 0;
                        if (DamageText.instance != null) DamageText.instance.ShowText(DamageText.TextType.Weak, hit.m_point + Vector3.up * 0.6f, Lang.T("РИТМ"));
                    }
                }
                if (_rhythmBeat) mul *= RhythmMul;
            }
            if (mul != 1f) hit.ApplyModifier(mul);
            if (Fury.On(me) && Facets.On("fury")) FuryWatch(me, target, hit);
        }
        private static bool _rhythmBeat;

        // fury III: a foe your blow kills feeds the fury a few seconds more
        private static void FuryWatch(Player me, Character target, HitData hit)
        {
            if (Plugin.Instance == null || _furyAdded >= FuryKillMax || target.IsDead()) return;
            if (hit.GetTotalDamage() < target.GetHealth() * 0.5f) return; // (no chance it is the last blow)
            Plugin.Instance.StartCoroutine(FuryLater(me, target));
        }

        private static System.Collections.IEnumerator FuryLater(Player me, Character target)
        {
            yield return new WaitForSeconds(0.4f);
            if (me == null || !(target == null || target.GetHealth() <= 0f)) yield break;
            StatusEffect se = me.GetSEMan().GetStatusEffect(Fury.Hash);
            if (se == null || _furyAdded >= FuryKillMax) yield break;
            float add = Mathf.Min(FuryKillAdd, FuryKillMax - _furyAdded);
            _furyAdded += add;
            se.m_ttl += add;
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("Лють жере кров: +{0} с", Mathf.RoundToInt(add)));
        }
        internal static void FuryStarted() { _furyAdded = 0f; _rhythm = 0; }

        // ---------------------------------------------------------------- the beast

        private static readonly Dictionary<Character, float> Trampled = new Dictionary<Character, float>();
        private static float _nextTrample;

        // II: the beast running knocks down the small ones in its way
        private static void Trample(Player p)
        {
            if (Time.time < _nextTrample || !Beast.On() || !Two("mberserk")) return;
            _nextTrample = Time.time + 0.15f;
            Vector3 v = p.GetVelocity(); v.y = 0f;
            if (v.magnitude < TrampleSpeed) return;
            float reach = TrampleReach * Beast.Size;
            ItemDrop.ItemData munin = Trust.Worn(true);
            float blow = (munin != null ? Strike.Blow(true, munin) : 10f) * TrampleShare;
            if (Trampled.Count > 32) Trampled.Clear();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (!Strike.Prey(p, c) || c.IsBoss()) continue;
                Vector3 d = c.transform.position - p.transform.position; d.y = 0f;
                if (d.magnitude > reach || Vector3.Dot(d.normalized, v.normalized) < 0.3f) continue;
                float t;
                if (Trampled.TryGetValue(c, out t) && Time.time - t < TrampleGap) continue;
                Trampled[c] = Time.time;
                Mastery.Splash(p, Body.Center(c), 0.1f, blow, false, null, null, true, true);
                c.Stagger(v.normalized);
            }
        }

        // III: waking, the beast roars: the weak ones run
        internal static void Roar(Player p)
        {
            if (!Facets.On("mberserk")) return;
            List<Character> who = new List<Character>();
            foreach (Character c in Character.GetAllCharacters())
                if (Strike.Prey(p, c) && !c.IsBoss() && Vector3.Distance(c.transform.position, p.transform.position) <= RoarReach) who.Add(c);
            if (who.Count > 0) Fear.Frighten(p.transform.position, who);
        }

        // ---------------------------------------------------------------- the wind

        private static float _nextTrail;
        private static SE_Stats _trail;
        internal const string TrailName = "RavenWatch_WindTrail";

        // II: a friend's tailwind with the trail (seen on their ZDO) carries you too while you keep near
        private static void Trail(Player me)
        {
            if (Time.time < _nextTrail) return;
            _nextTrail = Time.time + 1f;
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (!Friend(me, pl) || !WingsFx.On(pl) || Vector3.Distance(pl.transform.position, me.transform.position) > TrailReach) continue;
                ZDO z = NetFlag.Zdo(pl, false);
                if (z == null || !z.GetBool(TrailKey, false)) continue;
                SE_Stats se = RavenFx.Template(ref _trail, TrailName);
                se.m_name = Lang.W("Слід вітру");
                se.m_tooltip = Lang.F("Вітер Муніна гравця {0} несе й тебе", pl.GetPlayerName());
                se.m_icon = AbilityIcons.ForEffect("wings", true);
                se.m_ttl = 2f;
                se.m_speedModifier = Wings.SpeedNow() / 100f * TrailShare;
                me.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
                return;
            }
        }

        // the caster's flags for the others (wings II trail, III sails): on its own ZDO while the wind blows
        internal static void WindFlags(Player p, bool on)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            z.Set(TrailKey, on && Two("wings"));
            z.Set(SailKey, on && Facets.On("wings"));
        }

        // ---------------------------------------------------------------- heal

        // II: washes off poison, burning, frost and wet
        internal static void Cleanse(Player p)
        {
            if (!Two("heal")) return;
            SEMan se = p.GetSEMan();
            int n = 0;
            foreach (int h in new[] { SEMan.s_statusEffectPoison, SEMan.s_statusEffectBurning, SEMan.s_statusEffectFreezing, SEMan.s_statusEffectFrost, SEMan.s_statusEffectWet })
                if (se.HaveStatusEffect(h)) { se.RemoveStatusEffect(h, true); n++; }
            if (n > 0) p.Message(MessageHud.MessageType.TopLeft, Lang.T("Мунін вичистив з тебе всю гидоту"));
        }
        internal static bool Dirty(Player p)
        {
            SEMan se = p.GetSEMan();
            return se.HaveStatusEffect(SEMan.s_statusEffectPoison) || se.HaveStatusEffect(SEMan.s_statusEffectBurning) || se.HaveStatusEffect(SEMan.s_statusEffectFreezing) || se.HaveStatusEffect(SEMan.s_statusEffectFrost) || se.HaveStatusEffect(SEMan.s_statusEffectWet);
        }

        // ---------------------------------------------------------------- ward

        // II: a close blow it took: half of it back into the striker
        internal static void Thorn(Player p, HitData hit, Character attacker)
        {
            if (!Two("ward") || hit.m_ranged || attacker == null || attacker.IsPlayer() || Vector3.Distance(attacker.transform.position, p.transform.position) > ThornsReach) return;
            HitData back = new HitData();
            back.m_damage = hit.m_damage.Clone();
            back.m_damage.Modify(Thorns * (attacker.IsBoss() ? 0.5f : 1f)); // (a boss: a quarter)
            back.m_point = attacker.GetCenterPoint();
            back.m_dir = (attacker.GetCenterPoint() - p.GetCenterPoint()).normalized;
            back.m_skill = Skills.SkillType.None;
            back.m_skillRaiseAmount = Strike.Marker - Mathf.Max(1f, back.GetTotalDamage());
            back.SetAttacker(p);
            attacker.Damage(back);
        }

        // III: broken (or faded: share 0.5), its runes fly ahead and cut the foes in front — twice Munin's blow as it is now
        internal static void Shards(Player p, float share = 1f)
        {
            if (!Facets.On("ward")) return;
            ItemDrop.ItemData munin = Trust.Worn(true);
            float blow = (munin != null ? Strike.Blow(true, munin) : 10f) * ShardShare * share;
            Vector3 fwd = p.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 at = p.transform.position + fwd * (ShardReach * 0.5f) + Vector3.up;
            Mastery.Splash(p, at, ShardReach * 0.5f, blow, false, null, new Dictionary<Character, float>(), true, true);
            BindFx.Nova(p, "runeshield", at, ShardReach * 0.5f, 0.5f);
        }

        // mtame II: tamed with Munin — the beast keeps his mark (on its ZDO), and takes less from every blow
        internal static void MarkTamed(Character c)
        {
            if (c == null || !Two("mtame")) return;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            nv.GetZDO().Set(TameKey, true);
            Player p = Player.m_localPlayer;
            if (p != null) p.Message(MessageHud.MessageType.TopLeft, Lang.F("Мітка Муніна на звірові «{0}»: удари по ньому слабші", Lang.Game(c.m_name)));
        }

        // ---------------------------------------------------------------- the pair: whirl, clear sky, load

        // pwhirl II: a blow of the whirl draws its foe a little in (not right into you)
        internal static void Pull(Player p, Character c)
        {
            if (c == null || c.IsBoss() || !Two("pwhirl")) return;
            Vector3 d = p.transform.position - c.transform.position; d.y = 0f;
            if (d.magnitude < 2.5f) return;
            RavenSkills.Push(p, c, d, WhirlPull);
        }

        // pclear II: under a clear sky (of a caster with the second rune) the plants count time faster
        internal static bool Growing(Vector3 at)
        {
            double now = WorldTime.Now;
            foreach (Player pl in Player.GetAllPlayers())
            {
                ZDO z = NetFlag.Zdo(pl, false);
                if (z == null || !z.GetBool(GrowKey, false) || z.GetFloat(ClearSky.EndKey, 0f) <= now) continue;
                if (Vector3.Distance(pl.transform.position, at) <= z.GetFloat(ClearSky.RadiusKey, ClearSky.Radius)) return true;
            }
            return false;
        }

        // pclear III: at night under a clear sky with the third rune, a pale moonlight
        private static Light _moon;
        private static float _moonAt, _moonWant;
        private static void MoonTick(Player me)
        {
            if (Time.time >= _moonAt)
            {
                _moonAt = Time.time + 0.5f;
                _moonWant = 0f;
                if (EnvMan.IsNight() && !me.InInterior())
                {
                    double now = WorldTime.Now;
                    foreach (Player pl in Player.GetAllPlayers())
                    {
                        ZDO z = NetFlag.Zdo(pl, false);
                        if (z == null || !z.GetBool(MoonKey, false) || z.GetFloat(ClearSky.EndKey, 0f) <= now) continue;
                        if (Vector3.Distance(pl.transform.position, me.transform.position) <= z.GetFloat(ClearSky.RadiusKey, ClearSky.Radius)) { _moonWant = 1f; break; }
                    }
                }
            }
            if (_moonWant <= 0f && _moon == null) return;
            if (_moon == null) Moon(true);
            _moon.intensity = Mathf.MoveTowards(_moon.intensity, _moonWant * 0.35f, Time.deltaTime * 0.2f);
            if (_moonWant <= 0f && _moon.intensity <= 0.001f) Moon(false);
        }

        private static void Moon(bool on)
        {
            if (!on) { if (_moon != null) UnityEngine.Object.Destroy(_moon.gameObject); _moon = null; return; }
            GameObject go = new GameObject("RavenWatch_ClearMoon");
            go.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            _moon = go.AddComponent<Light>();
            _moon.type = LightType.Directional;
            _moon.color = new Color(0.62f, 0.72f, 0.95f);
            _moon.intensity = 0f;
            _moon.shadows = LightShadows.None;
        }

        // the caster's flags for the others (pclear II growing, III moon): on its own ZDO
        internal static void ClearFlags(Player p, bool on)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            z.Set(GrowKey, on && Two("pclear"));
            z.Set(MoonKey, on && Facets.On("pclear"));
        }
    }

    // ---------------------------------------------------------------- the patches

    // your blows at a foe: the light III, the fury's rhythm (II) and its kills (III)
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class TwistBlowPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            Player me = Player.m_localPlayer;
            if (me == null || hit == null || __instance == me || hit.m_attacker != me.GetZDOID() || hit.m_skillRaiseAmount <= Strike.Marker) return; // (a raven's blow: not yours)
            if (hit.GetTotalDamage() <= 0f || __instance.IsTamed() || !BaseAI.IsEnemy(me, __instance)) return;
            Twists.YourBlow(me, __instance, hit);
        } catch (Exception e) { PatchGuard.Fail("TwistBlowPatch", e); } }
    }

    // a blow on you (the game that owns you): the last chance's lesson (II); on a tamed beast of Munin's: tougher
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class TwistTakenPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (hit == null || __instance == null) return;
            if (ReferenceEquals(__instance, Player.m_localPlayer)) { float m = Twists.TakenMul(hit.GetAttacker()); if (m != 1f) hit.ApplyModifier(m); return; }
            if (!__instance.IsTamed()) return;
            ZNetView nv = __instance.GetComponent<ZNetView>();
            ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (z != null && z.GetBool(Twists.TameKey, false)) hit.ApplyModifier(1f / Twists.TameTough);
        } catch (Exception e) { PatchGuard.Fail("TwistTakenPatch", e); } }
    }

    // bloodfury II: nothing staggers you in the fury
    [HarmonyPatch(typeof(Character), "Stagger")]
    internal static class TwistStaggerPatch
    {
        private static bool Prefix(Character __instance)
        { try {
            return !(ReferenceEquals(__instance, Player.m_localPlayer) && BloodFury.On(__instance) && Twists.Two("bloodfury"));
        } catch (Exception e) { PatchGuard.Fail("TwistStaggerPatch", e); return true; } }
    }

    // scream III: the ones who heard it do not block
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class TwistGuardPatch
    {
        private static bool Prefix(Humanoid __instance, ref bool __result)
        { try {
            if (__instance == null || __instance.IsPlayer() || !Twists.GuardBroken(__instance)) return true;
            __result = false;
            return false;
        } catch (Exception e) { PatchGuard.Fail("TwistGuardPatch", e); return true; } }
    }

    // wings III: a ship with a tailwind of Munin's aboard sails faster (on the game that steers it)
    [HarmonyPatch(typeof(Ship), "GetSailForce")]
    internal static class TwistSailPatch
    {
        private static readonly System.Reflection.FieldInfo Players = AccessTools.Field(typeof(Ship), "m_players");
        private static void Postfix(Ship __instance, ref Vector3 __result)
        { try {
            List<Player> aboard = Players != null ? Players.GetValue(__instance) as List<Player> : null;
            if (aboard == null) return;
            foreach (Player pl in aboard)
            {
                if (pl == null || !WingsFx.On(pl)) continue;
                ZDO z = NetFlag.Zdo(pl, false);
                if (z != null && z.GetBool(Twists.SailKey, false)) { __result *= Twists.SailMul; return; }
            }
        } catch (Exception e) { PatchGuard.Fail("TwistSailPatch", e); } }
    }

    // pclear II: plants under the clear sky count their time faster
    [HarmonyPatch(typeof(Plant), "TimeSincePlanted")]
    internal static class TwistGrowPatch
    {
        private static void Postfix(Plant __instance, ref double __result)
        { try {
            if (__result > 0 && Twists.Growing(__instance.transform.position)) __result *= Twists.GrowMul;
        } catch (Exception e) { PatchGuard.Fail("TwistGrowPatch", e); } }
    }

    // pload II: the ravens pick up what lies around (the game's own pickup, wider; after the perk's)
    [HarmonyPatch(typeof(Player), "AutoPickup")]
    internal static class TwistPickupPatch
    {
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(Player __instance, out float __state)
        { __state = -1f; try {
            if (__instance != Player.m_localPlayer || !Load.On() || !Twists.Two("pload")) return;
            __state = __instance.m_autoPickupRange;
            __instance.m_autoPickupRange = Mathf.Max(__instance.m_autoPickupRange, Twists.PickupReach);
        } catch (Exception e) { PatchGuard.Fail("TwistPickupPatch", e); } }

        [HarmonyPriority(Priority.High)] // (back before the perk's own postfix puts its saved range back)
        private static void Postfix(Player __instance, float __state) { if (__state >= 0f && __instance != null) __instance.m_autoPickupRange = __state; }
    }
}
#endif
