#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // «Купання» (the user, 2026-10-27): now and then a raven flies off to wash at the water's edge near you — when, by its
    // character and by how dirty it is. In the water it crouches, beats its wings, dips its head, the water splashing;
    // then back on your shoulder it shakes itself off — onto you (you are wet a while). A little trust for it.
    //   - The dirt (0–100, hidden, kept in the raven), of four kinds (2026-10-28): the swamp's mud, the Ashlands' ash, the
    //     blood of a fight (Munin's kills), the bones it dug in. Rain washes a little; a bath, all of it. A dirty raven
    //     wants the water sooner and hints at it in a word now and then (by its kind; «no water here» where there is
    //     none), the neat characters early, the bold one late and proud of it (Munin of the blood). A clump of it falls
    //     off now and then, a fly buzzes round the filthiest (DirtFx); the other players' ravens notice it (Bonds).
    //   - After a bath that washed real dirt off, «Свіже пір'я» a while: its strike harder (the dirtier it was, the more;
    //     the hot tub more still), a little more trust. Dirty long, it asks for the hot tub (Requests «tub»).
    //   - The visit itself is WorldVisit's (kind Bathe, its own moment): the spot by the shore, the flight
    //     there and back seen by all; the bath on the spot (PerchLife.Bathe) by a count on the spot's ZDO, as the pecking.
    //     A hot tub near (the game's) is better than the shore; you sitting in it, the raven joins you there. Standing
    //     in the water, you may wash it yourself (the perch's button: «Помити»).
    //   - Not at night (but a «Нічна пташка»), not in the cold (but the breed «Жаровий»), not hungry, not in a fight — a
    //     heated tub minds neither the night nor the cold.
    internal static class Bath
    {
        internal const string DirtKey = "RavenWatch_dirt", FreshKey = "RavenWatch_fresh", NetKey = "RavenWatch_Dirt";
        internal const int Mud = 0, Ash = 1, Blood = 2, Bones = 3;
        internal static readonly string[] KindIds = { "mud", "ash", "blood", "bones" };
        internal static float SwampPerMin = 1f, AshPerMin = 1.25f, FightPerMin = 0.75f, RainPerMin = -1f, BonesDirt = 7.5f, KillDirt = 0.75f; // (halved 2026-10-28: too fast)
        internal static float AgainMin = 25f, AgainMax = 40f;  // min between two baths (clean, an eager character: see Pause)
        internal static float ShakeAfter = 3.5f, ShakeWindow = 20f, WetSeconds = 30f;
        internal static float FreshMinutes = 20f, FreshTrustMul = 1.15f, FreshUntilDirt = 50f;
        // the fresh feathers' strike (the user, 2026-10-28): only for real dirt washed off — from FreshFrom +FreshPct, from
        // FreshDirtyFrom +FreshDirtyPct; in the hot tub +FreshTubPct more. A clean raven's bath gives nothing (no free buff)
        internal static float FreshFrom = 25f, FreshPct = 10f, FreshDirtyFrom = 50f, FreshDirtyPct = 20f, FreshTubPct = 5f;
        internal const string FreshPctKey = "RavenWatch_freshPct", DirtyMinKey = "RavenWatch_dirtyMin", TubSeenKey = "RavenWatch_tubSeen";
        internal static float TubWishShare = 0.15f; // a hint now and then is the wish for the hot tub instead (once you have one)
        internal static float HintProud = 20f, HintNeat = 30f, HintAt = 50f, HintBold = 70f, HintMinMin = 6f, HintMaxMin = 10f, ProudShare = 0.5f;
        internal static float TubMul = 1.5f, HandMul = 2f, HandFrom = 10f;
        internal static float Sink = 0.12f; // m a bathing raven sits below the water's face (in it, not on it)
        private const float SampleEvery = 10f;

        // ---------------------------------------------------------------- the dirt, kind by kind ("mud;ash;blood;bones")

        private static readonly float[] Scratch = new float[4];
        private static float[] Mix(ItemDrop.ItemData item)
        {
            for (int i = 0; i < 4; i++) Scratch[i] = 0f;
            string raw;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(DirtKey, out raw)) return Scratch;
            string[] s = raw.Split(';');
            for (int i = 0; i < 4 && i < s.Length; i++) { float v; if (float.TryParse(s[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) Scratch[i] = Mathf.Max(0f, v); } // (an old single number: all mud)
            return Scratch;
        }

        private static void Store(ItemDrop.ItemData item, float[] m)
        {
            if (item == null || item.m_customData == null) return;
            // (clean only when there is next to nothing: the dirt comes in small steps — a fight 0.125 every 10 s — that a higher
            // threshold would wipe each time; hundredths, so the steps are not rounded away)
            if (m[0] + m[1] + m[2] + m[3] < 0.05f) { item.m_customData.Remove(DirtKey); return; }
            item.m_customData[DirtKey] = m[0].ToString("0.##", CultureInfo.InvariantCulture) + ";" + m[1].ToString("0.##", CultureInfo.InvariantCulture) + ";" + m[2].ToString("0.##", CultureInfo.InvariantCulture) + ";" + m[3].ToString("0.##", CultureInfo.InvariantCulture);
        }

        internal static float DirtOf(ItemDrop.ItemData item) { float[] m = Mix(item); return m[0] + m[1] + m[2] + m[3]; }

        // what it is mostly covered in
        internal static int KindOf(ItemDrop.ItemData item)
        {
            float[] m = Mix(item);
            int best = 0;
            for (int i = 1; i < 4; i++) if (m[i] > m[best]) best = i;
            return best;
        }

        // 0 clean, 1 dusty, 2 dirty, 3 filthy
        internal static int Stage(float dirt) { return dirt < 25f ? 0 : dirt < 50f ? 1 : dirt < 80f ? 2 : 3; }

        // more dirt of a kind on it (up to 100 in all)
        internal static void Soil(ItemDrop.ItemData item, float by, int kind)
        {
            if (item == null || by <= 0f) return;
            float[] m = Mix(item);
            float room = 100f - (m[0] + m[1] + m[2] + m[3]);
            if (room <= 0f) return;
            m[Mathf.Clamp(kind, 0, 3)] += Mathf.Min(by, room);
            Store(item, m);
            if (DirtOf(item) >= FilthyAt) Nicks.OnFilthy(item, KindOf(item)); // filthy: a deed for its nickname (once until clean again)
        }

        // some of it off (the rain), every kind alike
        private static void Rinse(ItemDrop.ItemData item, float by)
        {
            float[] m = Mix(item);
            float all = m[0] + m[1] + m[2] + m[3];
            if (all <= 0f) return;
            float keep = Mathf.Clamp01((all - by) / all);
            for (int i = 0; i < 4; i++) m[i] *= keep;
            Store(item, m);
            if ((all - by) < 25f) Nicks.OnClean(item); // (the rain washed it nearly clean)
        }

        private static void Set(ItemDrop.ItemData item, float v, int kind)
        {
            float[] m = Mix(item);
            for (int i = 0; i < 4; i++) m[i] = 0f;
            m[Mathf.Clamp(kind, 0, 3)] = Mathf.Clamp(v, 0f, 100f);
            Store(item, m);
        }

        internal static void Killed() { ItemDrop.ItemData m = With(true); if (m != null) Soil(m, KillDirt, Blood); } // (Munin: the blood)

        // ---------------------------------------------------------------- «Свіже пір'я»: a while after a bath

        internal static float FreshLeft(ItemDrop.ItemData item) { return item != null ? WorldTime.Left(item.m_customData, FreshKey) : 0f; }
        internal static float StrikePct(ItemDrop.ItemData item)
        {
            if (FreshLeft(item) <= 0f) return 0f;
            string v; float pct;
            return item.m_customData.TryGetValue(FreshPctKey, out v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out pct) ? pct : FreshPct;
        }

        // what a bath gives for the dirt it washed off (0: nothing)
        internal static float FreshFor(float washed, string how)
        {
            if (washed < FreshFrom) return 0f;
            return (washed >= FreshDirtyFrom ? FreshDirtyPct : FreshPct) + (how == Tub ? FreshTubPct : 0f);
        }

        // minutes it has stayed dirty (from FreshDirtyFrom) on end: its wish for the hot tub (Requests «tub»)
        internal static float DirtyMinutes(ItemDrop.ItemData item) { string v; float m; return item != null && item.m_customData.TryGetValue(DirtyMinKey, out v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out m) ? m : 0f; }

        // a hot tub seen in this world (the wish for one and its words only then)
        internal static bool TubKnown(Player p) { string w; return p != null && ZNet.instance != null && p.m_customData.TryGetValue(TubSeenKey, out w) && w == ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture); }
        private static void NoFresh(ItemDrop.ItemData item) { item.m_customData.Remove(FreshKey); item.m_customData.Remove(FreshPctKey); }
        internal static float TrustMul(ItemDrop.ItemData item) { return FreshLeft(item) > 0f ? FreshTrustMul : 1f; }

        // filthy (the user, 2026-10-28): its strength comes slower
        internal static float FilthyAt = 80f, FilthyGainPct = 30f;
        internal static float GainMul(ItemDrop.ItemData item) { return item != null && DirtOf(item) >= FilthyAt ? 1f - FilthyGainPct / 100f : 1f; }

        // ---------------------------------------------------------------- the book's bath medallion (on the Ravens page's margin)

        private static readonly string[] StageWords = Lang.Keys("чистий", "запорошений", "брудний", "дуже брудний");
        private static readonly string[] StageInk = { "#8FD18F", "#E8D27A", "#FFA552", "#FF6B6B" }; // (clean green → filthy red)
        internal static string BookNumbers(ItemDrop.ItemData item)
        {
            float d = DirtOf(item), l = FreshLeft(item);
            int st = Stage(d);
            string s = "<color=" + StageInk[st] + ">" + Lang.T(StageWords[st]) + "</color> · " + Mathf.RoundToInt(d) + " / 100";
            return l > 0f ? "<color=#FFD54F>" + Lang.F("свіже пір'я +{0}% · ще {1}", Mathf.RoundToInt(StrikePct(item)), RavenAbilities.Clock(l)) + "</color>\n" + s : s; // (the bonus gold, on its own line)
        }

        internal static string BookTip(ItemDrop.ItemData item, bool munin)
        {
            int k = KindOf(item);
            return Lang.F("<b>Бруд</b>\n\n{0} брудниться в болотах, у попелі, у бою{1} і в кістках. Дощ трохи змиває, купання — усе.\n\nПомити: сам летить до води чи в купіль; або стань у воду — «Жердина» в наказах стане «Помити».\n\n<b>Свіже пір'я</b> на {2} хв, якщо змито бруд: від {3} — удар +{4}%, від {5} — +{6}%. У гарячій купелі ще +{7}%.{8}",
                Plugin.RavenName(munin), (munin ? Lang.T(", від крові вбитих") : ""), Mathf.RoundToInt(FreshMinutes), Mathf.RoundToInt(FreshFrom), Mathf.RoundToInt(FreshPct), Mathf.RoundToInt(FreshDirtyFrom), Mathf.RoundToInt(FreshDirtyPct), Mathf.RoundToInt(FreshTubPct),
                DirtOf(item) >= 1f ? Lang.F("\n\nЗараз найбільше на ньому: {0}.", (k == Ash ? Lang.T("попіл") : k == Blood ? Lang.T("кров") : k == Bones ? Lang.T("кістяний пил") : Lang.T("болото"))) : "")
                + Lang.F("\n\n<color={0}>Дуже брудний (від {1}) набирає силу на {2}% повільніше.</color>", GainMul(item) < 1f ? "#FF6B6B" : "#9A9A9A", Mathf.RoundToInt(FilthyAt), Mathf.RoundToInt(FilthyGainPct));
        }

        // ---------------------------------------------------------------- what makes it want the water

        private static string CharId(ItemDrop.ItemData item) { Norov.Character c = Norov.CharOf(item); return c != null ? c.Id : ""; }
        private static bool Neat(string id) { return id == "homebody" || id == "calm" || id == "fidget" || id == "proud"; }
        private static bool Rough(string id) { return id == "bold" || id == "huntsman"; } // (dirt does not bother them; a little blood they are proud of)

        // its character's eagerness (the neat ones often, the bold one seldom)
        internal static float CharEager(ItemDrop.ItemData item)
        {
            string id = CharId(item);
            return id == "proud" ? 1.6f : Neat(id) ? 1.3f : id == "bold" ? 0.5f : id == "huntsman" ? 0.7f : 1f;
        }

        // ...and with its dirt: which of the two goes
        internal static float Eager(ItemDrop.ItemData item)
        {
            float d = DirtOf(item);
            return CharEager(item) * (d >= 80f ? 4f : d >= 50f ? 2f : d < 15f ? 0.5f : 1f);
        }

        // dirty enough to go before its pause is over (but never two baths within MinGap)
        internal static float DirtyGo = 50f, MinGap = 300f;
        internal static bool Dirty(ItemDrop.ItemData item) { return item != null && DirtOf(item) >= DirtyGo; }

        // the pause before the next bath after this one, by its character (it comes clean out of the water) (min → s)
        internal static float Pause(ItemDrop.ItemData item) { return Random.Range(AgainMin, AgainMax) * 60f / Mathf.Max(0.2f, CharEager(item)); }

        // how long it washes: the calm and the homebody long, the fidget a quick splash; in the tub with you a good while
        internal static float StayOf(ItemDrop.ItemData item, string how)
        {
            if (how == Tub) return Random.Range(18f, 28f);
            if (how == Hand) return Random.Range(7f, 8f);
            string id = CharId(item);
            return id == "calm" || id == "homebody" || id == "proud" ? Random.Range(10f, 12f) : id == "fidget" ? Random.Range(5f, 6f) : id == "bold" ? Random.Range(6f, 7f) : Random.Range(7f, 9f);
        }

        // may it go now (the weather and the hour: the rest is WorldVisit's)
        internal static bool Weather(ItemDrop.ItemData item)
        {
            if (EnvMan.instance == null) return false;
            if (EnvMan.IsNight() && !Habits.Has(item, "night")) return false;
            if ((EnvMan.IsCold() || EnvMan.IsFreezing()) && !BreedTraits.ColdProof(item)) return false;
            return true;
        }

        // ---------------------------------------------------------------- the bath over: clean, fresh, a little trust; the shake on you

        internal const string Tub = "tub", Hand = "hand"; // (WorldVisit's Thing.Text for a bath: in the hot tub with you, by your own hands)
        private static readonly float[] ShakeAt = { -1f, -1f };
        internal static void Done(Player p, bool munin, ItemDrop.ItemData item, string how)
        {
            float was = DirtOf(item);
            Set(item, 0f, Mud);
            Nicks.OnBath(p, item, how, was); // a deed for its nickname
            Nicks.OnClean(item);
            bool tub = how == Tub || (how == Hand && InTub(p, TubNear(p))); // (washed by your hands while you sit in the tub: the tub's too)
            float pct = FreshFor(was, tub ? Tub : how);
            if (item != null && !Trust.Stranger(item, p) && FreshMinutes > 0f && pct > 0f && pct >= StrikePct(item)) // (never a weaker one over a stronger still on; not for another's raven)
            {
                WorldTime.Set(item.m_customData, FreshKey, FreshMinutes * 60f);
                item.m_customData[FreshPctKey] = pct.ToString("0.#", CultureInfo.InvariantCulture);
            }
            if (item != null) item.m_customData.Remove(DirtyMinKey);
            if (tub) Requests.OnTubBath(p, munin, item); // its wish, if it was this
            Trust.Played(munin, how == Hand ? Lang.T("миття") : how == Tub ? Lang.T("купання з тобою") : Lang.T("купання"), how == Hand ? HandMul : how == Tub ? TubMul : 1f);
            if (how != Tub) ShakeAt[munin ? 1 : 0] = Time.time + ShakeAfter; // (out of the tub with you: you are wet anyway)
            Publish(p);
            if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Raven world: " + Plugin.RavenName(munin) + " had a bath" + (string.IsNullOrEmpty(how) ? "" : " (" + how + ")"));
        }

        // ---------------------------------------------------------------- twice a second (RavenSkills)
        private static float _nextSample;
        private static readonly float[] HintNext = { -1f, -1f };
        internal static void Tick(Player p)
        {
            ScrubTick(p); // (first: a death mid-wash ends it too)
            if (p == null || p.IsDead()) return;
            float now = Time.time;
            for (int k = 0; k < 2; k++)
            {
                if (ShakeAt[k] < 0f || now < ShakeAt[k]) continue;
                bool munin = k == 1;
                if (now > ShakeAt[k] + ShakeWindow) { ShakeAt[k] = -1f; continue; } // (never sat back down: no shake)
                ShoulderLife life = Shoulder.LocalLife(munin);
                if (life == null || !life.SitNew || life.Hovering || !Shoulder.Arrived(p, munin)) continue;
                ShakeAt[k] = -1f;
                ShoulderLife.ShakeOff(p, munin); // (the wings beating the water off, seen by all)
                Wet(p);
                PlayerReact.Splashed(p, munin);
                PerkFx.Say(munin, "bath_shake", 0.6f, 0f);
            }
            if (now < _nextSample) return;
            _nextSample = now + SampleEvery;
            float min = SampleEvery / 60f;
            Heightmap.Biome land = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(p.transform.position) : Heightmap.Biome.None;
            bool rain = EnvMan.IsWet() && !Senses.Indoors, open = !Senses.Indoors && !Senses.Underground;
            foreach (bool m in new[] { false, true })
            {
                ItemDrop.ItemData it = With(m);
                if (it == null) continue;
                if (open && land == Heightmap.Biome.Swamp) Soil(it, SwampPerMin * min, Mud);
                if (open && land == Heightmap.Biome.AshLands) Soil(it, AshPerMin * min, Ash);
                if (Senses.Fight) Soil(it, FightPerMin * min, Blood);
                if (rain) Rinse(it, -RainPerMin * min);
                if (DirtOf(it) >= FreshUntilDirt) NoFresh(it); // (dirty again: the fresh feathers gone)
                if (DirtOf(it) >= FreshDirtyFrom) it.m_customData[DirtyMinKey] = (DirtyMinutes(it) + min).ToString("0.##", CultureInfo.InvariantCulture);
                else it.m_customData.Remove(DirtyMinKey);
                Hint(m, it, land);
            }
            Publish(p);
        }

        // a word now and then that it wants a wash: from its dirt (the neat early, the bold late), proud of it sometimes
        private static void Hint(bool munin, ItemDrop.ItemData item, Heightmap.Biome land)
        {
            int k = munin ? 1 : 0;
            if (HintNext[k] < 0f) { HintNext[k] = Time.time + Random.Range(HintMinMin, HintMaxMin) * 30f; return; } // (not as soon as you come in)
            if (Time.time < HintNext[k] || WorldVisit.Active(munin)) return;
            string id = CharId(item);
            int kind = KindOf(item);
            if (DirtOf(item) < (id == "proud" ? HintProud : Neat(id) ? HintNeat : Rough(id) ? HintBold : HintAt)) return;
            bool proud = id != "proud" && (Rough(id) || (munin && kind == Blood)) && Random.value < ProudShare; // («Гордій» is never proud of dirt)
            bool dry = land == Heightmap.Biome.AshLands || WorldVisit.NoShore;
            bool tub = !proud && DirtOf(item) >= FreshDirtyFrom && TubKnown(Player.m_localPlayer) && Random.value < TubWishShare;
            bool said = tub ? PerkFx.Say(munin, "dirt_tub_wish", 1f, 0f) : proud ?PerkFx.Say(munin, "dirt_proud", 1f, 0f, "dirt", DirtWord(kind)) : PerkFx.Say(munin, "dirt_hint_" + KindIds[kind] + (dry ? "_dry" : ""), 1f, 0f);
            HintNext[k] = Time.time + (said ? Random.Range(HintMinMin, HintMaxMin) * 60f : 30f); // (a busy moment: tried again soon)
        }

        // what it is covered in, as the lines put it ({dirt}: «вимазаний попелом»)
        internal static string DirtWord(int kind) { return kind == Ash ? Lang.T("попелом") : kind == Blood ? Lang.T("кров'ю") : kind == Bones ? Lang.T("мертвечиною") : Lang.T("болотом"); }

        // ---------------------------------------------------------------- seen by every game: the stage and kind on your ZDO

        // per raven 4 bits (stage << 2 | kind), Munin's above Hugin's
        private static void Publish(Player p)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            int v = 0;
            foreach (bool m in new[] { false, true })
            {
                ItemDrop.ItemData it = RavenSeat.On(p, m);
                if (it == null || Perches.On(it)) continue;
                int s = Stage(DirtOf(it));
                if (s > 0) v |= ((s << 2) | KindOf(it)) << (m ? 4 : 0);
            }
            if (z.GetInt(NetKey, 0) != v) z.Set(NetKey, v);
        }

        // how dirty the raven on that player's shoulder is (0..3), and with what
        internal static int Seen(Player pl, bool munin, out int kind)
        {
            ZDO z = NetFlag.Zdo(pl, false);
            int v = z != null ? z.GetInt(NetKey, 0) >> (munin ? 4 : 0) : 0;
            kind = v & 3;
            return (v >> 2) & 3;
        }

        // the spray of its shake: you are wet a while
        private static void Wet(Player p)
        {
            SEMan se = p.GetSEMan();
            if (se == null) return;
            StatusEffect w = se.AddStatusEffect("Wet".GetStableHashCode(), true);
            if (w != null) w.m_ttl = Mathf.Max(w.m_ttl - w.GetRemaningTime(), 0f) + WetSeconds; // (a short wetting, not a soaking)
        }

        private static ItemDrop.ItemData With(bool munin)
        {
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData item = Trust.Worn(munin);
            return p != null && item != null && Trust.Active(item) && !Trust.Away(item) && !Trust.Stranger(item, p) ? item : null;
        }

        // ---------------------------------------------------------------- the hot tub (the game's: water of its own, a fire under it)

        internal sealed class TubAt { internal Piece Piece; internal WaterVolume Water; internal Bounds Box; internal bool Warm; }
        private static readonly List<Piece> Near = new List<Piece>();
        private static readonly Dictionary<string, bool> HasWater = new Dictionary<string, bool>(); // a kind of piece (its name) → has water of its own (the game's hot tub, a mod's pool)
        private static TubAt _tub;
        private static float _tubAt = -99f;
        private static Vector3 _tubFrom;

        // the nearest tub within 20 m (looked for once in 3 s, or when you have moved)
        internal static TubAt TubNear(Player p)
        {
            if (p == null) return null;
            Vector3 me = p.transform.position;
            if (Time.time - _tubAt < 3f && (me - _tubFrom).sqrMagnitude < 4f && (_tub == null || _tub.Piece != null)) return _tub;
            _tubAt = Time.time; _tubFrom = me; _tub = null;
            Near.Clear();
            Piece.GetAllPiecesInRadius(me, 20f, Near);
            float best = float.MaxValue;
            foreach (Piece pc in Near)
            {
                if (pc == null) continue;
                float d = (pc.transform.position - me).sqrMagnitude;
                if (d >= best) continue;
                bool wet;
                if (!HasWater.TryGetValue(pc.m_name ?? "", out wet)) { wet = pc.GetComponentInChildren<WaterVolume>() != null; HasWater[pc.m_name ?? ""] = wet; } // (asked once a kind of piece: a big base has thousands near you)
                if (!wet) continue;
                ZNetView nv = pc.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || pc.gameObject.name.IndexOf("_planned", System.StringComparison.Ordinal) >= 0) continue; // (really built: not the ghost you are placing, not a mod's plan of one — PlanBuild)
#if RAVEN_NPC
                if (LocState.Absent(nv)) continue; // (a piece of a place of the story its state has not now: no tub — the game's list of pieces has it all the same)
#endif
                WaterVolume w = pc.GetComponentInChildren<WaterVolume>();
                Collider c = w != null ? w.GetComponent<Collider>() : null;
                if (c == null) continue;
                Fireplace f = pc.GetComponentInChildren<Fireplace>();
                best = d;
                _tub = new TubAt { Piece = pc, Water = w, Box = c.bounds, Warm = f != null && f.IsBurning() };
            }
            Near.Clear();
            if (_tub != null && !TubKnown(p) && ZNet.instance != null) p.m_customData[TubSeenKey] = ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture); // (now it may wish for one)
            return _tub;
        }

        // over the tub's water (its box is far taller than the water: only the ground plan counts, and up to a little over it)
        private static bool OverTub(TubAt t, Vector3 at)
        {
            Bounds b = t.Box;
            return at.x > b.min.x && at.x < b.max.x && at.z > b.min.z && at.z < b.max.z && at.y > b.min.y - 0.3f && at.y < t.Water.GetWaterSurface(at, 0f) + 0.5f;
        }

        // that spot in the nearest tub's water (no great splash there)
        internal static bool InTubAt(Player p, Vector3 at)
        {
            TubAt t = TubNear(p);
            return t != null && t.Piece != null && t.Water != null && OverTub(t, at);
        }

        // you sitting in it
        internal static bool InTub(Player p, TubAt t)
        {
            if (p == null || t == null || t.Piece == null || t.Water == null) return false;
            return p.InWater() && OverTub(t, p.transform.position); // (by its side on the ground is not in it)
        }

        // a spot on its water: beside you when you are in it, else in the middle
        internal static bool TubSpot(Player p, TubAt t, bool beside, out Vector3 at)
        {
            at = Vector3.zero;
            if (t == null || t.Piece == null || t.Water == null) return false;
            Vector3 c = t.Box.center, me = p.transform.position;
            Vector3 side = Vector3.ProjectOnPlane(c - me, Vector3.up);
            Vector3 dir = side.sqrMagnitude > 0.04f ? side.normalized : p.transform.right;
            for (int i = 0; i < 6; i++)
            {
                Vector3 pt = beside ? me + Quaternion.Euler(0f, (i % 2 == 0 ? 1f : -1f) * 25f * ((i + 1) / 2), 0f) * dir * 0.7f
                                    : c + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * (i == 0 ? 0f : 0.35f);
                if (!t.Box.Contains(new Vector3(pt.x, c.y, pt.z))) continue;
                pt.y = t.Water.GetWaterSurface(pt, 0f) - Sink; // (sat in it, the water up to its breast)
                if (SitHere.Taken(pt) || !SitHere.Free(pt)) continue;
                at = pt;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- your own hands: standing in the water

        // from the first step into the water up to nearly your neck (not swimming), or in the tub (the user, 2026-10-28)
        internal static float WadeFrom = 0.05f, WadeTo = 1.45f, KneelTo = 0.6f;
        internal static float Depth(Player p) { return p.GetLiquidLevel() - p.transform.position.y; }
        internal static bool Wading(Player p)
        {
            if (p == null || p.IsDead()) return false;
            if (InTub(p, TubNear(p))) return true;
            float d = Depth(p);
            return p.InWater() && !p.IsSwimming() && d > WadeFrom && d < WadeTo;
        }

        // your hands at it while it washes (the clip «wash_raven»): stopped when the bath is over
        private static float _scrubFrom = -1f;
        private static bool _scrubMunin;
        private static bool _knelt;
        private static System.Reflection.MethodInfo _stopEmote;
        internal static void Knelt() { _knelt = true; }
        // the game's own way out of an emote (protected in Player)
        private static void Unkneel(Player p)
        {
            if (_stopEmote == null) _stopEmote = HarmonyLib.AccessTools.Method(typeof(Player), "StopEmote");
            try { if (_stopEmote != null) _stopEmote.Invoke(p, null); } catch { }
        }
        internal static void Scrub(Player p, bool munin)
        {
            if (!RavenSkills.Animations) return;
            BodyClips.Play(p, 2, "wash_raven", 0.3f, 1f);
            _scrubFrom = Time.time; _scrubMunin = munin;
        }
        private static void ScrubTick(Player p)
        {
            if (_scrubFrom < 0f || p == null) return;
            float t = Time.time - _scrubFrom;
            bool on = !p.IsDead() && !p.IsSwimming() && p.GetVelocity().sqrMagnitude < 2f; // (you walked off, swam or died: your hands stop)
            if (on && (t < 2.5f || (t < 12f && WorldVisit.Active(_scrubMunin)))) return; // (the first seconds it is still on its way down to you)
            _scrubFrom = -1f;
            BodyClips.Stop(p, 2, 0.4f);
            if (_knelt) { _knelt = false; Unkneel(p); } // (washed: up off your knee)
        }

        // why not now (null: it may)
        internal static string HandWhyNot(Player p, bool munin)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null || Perches.On(item)) return Lang.F("{0} не на плечі.", Plugin.RavenName(munin));
            if (Trust.Stranger(item, p)) return Lang.T("Це не твій ворон.");
            if (Senses.Fight) return Lang.T("Не під час бою.");
            if (WorldVisit.Active(munin)) return Lang.F("{0} зараз літає у своїх справах.", Plugin.RavenName(munin));
            // (the perch's own reasons: on its way to you, sulking, out on an order; a sleepy, stuffed or chilly raven still
            // lets you wash it — those keep it from flying off on its own, not from your hands)
            ItemDrop.ItemData it;
            string why = Perches.WhyNot(p, munin, out it);
            if (why != null) return why;
            if (DirtOf(item) < HandFrom) return Lang.F("{0} і так чистий.", Plugin.RavenName(munin));
            return null;
        }

        // the console: raven bath [h|m] [dirt <0-100> [mud|ash|blood|bones] | now | hand | tub | fx | fish]
        internal static string Cmd(Player p, string[] a)
        {
            bool munin = a.Length > 1 && (a[1] == "m" || a[1] == "munin");
            int at = a.Length > 1 && (a[1] == "h" || a[1] == "m" || a[1] == "hugin" || a[1] == "munin") ? 2 : 1;
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return Plugin.RavenName(munin) + " не на плечі.";
            float v;
            if (a.Length > at + 1 && a[at] == "dirt" && float.TryParse(a[at + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            {
                int kind = a.Length > at + 2 ? System.Array.IndexOf(KindIds, a[at + 2]) : Mud;
                Set(item, v, kind < 0 ? Mud : kind);
                if (v >= FreshUntilDirt) NoFresh(item);
                Publish(p);
                HintNext[munin ? 1 : 0] = Time.time + 2f; // (a word about it soon)
            }
            else if (a.Length > at && a[at] == "now") return WorldVisit.BathNow(p, munin);
            else if (a.Length > at && a[at] == "fx") return DirtFx.DropNow(munin);
            else if (a.Length > at && a[at] == "fish") return WorldVisit.FishNow(p);
            else if (a.Length > at && a[at] == "hand") return WorldVisit.HandWash(p, munin) ?? Plugin.RavenName(munin) + ": washing by hand";
            else if (a.Length > at && a[at] == "tub")
            {
                TubAt t = TubNear(p);
                return t == null ? "no tub within 20 m" : "tub " + Utils.GetPrefabName(t.Piece.gameObject) + ", warm " + t.Warm + ", you in it " + InTub(p, t) + ", box " + t.Box.size.ToString("0.00") + " from y " + t.Box.min.y.ToString("0.00") + ", water at y " + t.Water.GetWaterSurface(t.Box.center, 0f).ToString("0.00");
            }
            float[] m = Mix(item);
            return Plugin.RavenName(munin) + ": dirt " + Mathf.RoundToInt(DirtOf(item)) + " (mud " + Mathf.RoundToInt(m[0]) + ", ash " + Mathf.RoundToInt(m[1]) + ", blood " + Mathf.RoundToInt(m[2]) + ", bones " + Mathf.RoundToInt(m[3]) + "), stage " + Stage(DirtOf(item)) + ", fresh " + Mathf.RoundToInt(FreshLeft(item)) + " s +" + Mathf.RoundToInt(StrikePct(item)) + "%, dirty " + Mathf.RoundToInt(DirtyMinutes(item)) + " min, eager ×" + Eager(item).ToString("0.0", CultureInfo.InvariantCulture) + ". raven bath [h|m] [dirt <n> [mud|ash|blood|bones] | now | hand | tub | fx | fish]";
        }
    }
}
#endif
