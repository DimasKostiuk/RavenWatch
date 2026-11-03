#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Вдома», its life there (see Home.cs): the raven's data on the perch as each game reads it, and what the
    // perch's holder counts: hunger, the feeder, its hunts, the guard store and its strength, sparring with your other raven,
    // the feeling of being forgotten, a find a day, the tally of it all for you, and what you see on the perch.
    internal static partial class Home
    {
        // ---------------------------------------------------------------- its data on the perch, as each game reads it

        // a seat's keys: its raven's data ("b"), a blow ("a"), a hunt ("x") — made once per seat number
        private static string[] _kb = new string[0], _ka = new string[0], _kx = new string[0];
        private static string Key(ref string[] keys, int i, string end)
        {
            if (i >= keys.Length) { string[] k = new string[Mathf.Max(i + 1, 4)]; System.Array.Copy(keys, k, keys.Length); keys = k; }
            return keys[i] ?? (keys[i] = Perches.SeatKey(i) + end);
        }
        internal static string KeyB(int i) { return Key(ref _kb, i, "b"); }
        internal static string KeyA(int i) { return Key(ref _ka, i, "a"); }
        internal static string KeyX(int i) { return Key(ref _kx, i, "x"); }
        private static string[] _kf = new string[0];
        internal static string KeyF(int i) { return Key(ref _kf, i, "f"); }

        private sealed class Seen { internal string Raw, HuntRaw; internal double HuntUntil; internal ItemDrop.ItemData Item; internal bool Dirty; internal float WriteAt, SimAt, NextBlow, AlarmAt; internal string Blow = "", Meal = ""; internal int Serial = UnityEngine.Random.Range(1, 1 << 24); }
        private static readonly Dictionary<PerchView, Seen[]> Seats = new Dictionary<PerchView, Seen[]>();

        private static Seen SeenAt(PerchView v, int seat)
        {
            Seen[] a;
            if (!Seats.TryGetValue(v, out a) || a.Length != v.Count) { a = new Seen[v.Count]; Seats[v] = a; }
            if (a[seat] == null) a[seat] = new Seen();
            return a[seat];
        }

        // the raven on this seat as an item to read (never in any bag); null when nobody at home sits there
        internal static ItemDrop.ItemData Cache(PerchView v, int seat)
        {
            if (v == null || !v.Valid || seat < 0 || seat >= v.Count) return null;
            Perches.Occupant o = v.At(seat);
            if (o == null || !o.Home) return null;
            Seen s = SeenAt(v, seat);
            string raw = v.Zdo.GetString(KeyB(seat), "");
            if (raw.Length == 0) return null;
            if (s.Item == null || (!ReferenceEquals(raw, s.Raw) && raw != s.Raw && !s.Dirty))
            {
                s.Raw = raw;
                s.Item = new ItemDrop.ItemData { m_customData = Backup.Unpack(raw) };
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(o.Munin ? RavenItem.MuninPrefab : RavenItem.HuginPrefab) : null;
                ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (d != null) s.Item.m_shared = d.m_itemData.m_shared;
            }
            return s.Item;
        }

        private static void Write(PerchView v, int seat, Seen s)
        {
            string packed = Backup.Pack(s.Item.m_customData);
            s.Raw = packed; s.Dirty = false; s.WriteAt = Time.time + WriteEvery;
            v.Zdo.Set(KeyB(seat), packed);
        }

        internal static float WriteEvery = 30f; // s: its data to everyone near at most this often (a blow, a meal, your visit: at once)
        private static void Soon(Seen s) { s.Dirty = true; s.WriteAt = Mathf.Min(s.WriteAt, Time.time + 1f); }

        private static string Read(ItemDrop.ItemData item, string key) { string v; return item != null && item.m_customData.TryGetValue(key, out v) ? v : ""; }

        // ---------------------------------------------------------------- its life there (the perch's holder)

        private static float _prune;
        internal static void HolderTick(PerchView v)
        {
            if (Time.time > _prune) { _prune = Time.time + 30f; List<PerchView> gone = null; foreach (PerchView k in Seats.Keys) if (k == null) { if (gone == null) gone = new List<PerchView>(); gone.Add(k); } if (gone != null) foreach (PerchView k in gone) Seats.Remove(k); }
            for (int i = 0; i < v.Count; i++)
            {
                Perches.Occupant o = v.At(i);
                if (o == null || !o.Home) continue;
                ItemDrop.ItemData item = Cache(v, i);
                if (item == null) continue;
                Seen s = SeenAt(v, i);
                if (Time.time >= s.SimAt)
                {
                    s.SimAt = Time.time + 5f;
                    // brought back from its copy meanwhile (called from afar): this one is an echo (it reads the world's keys: not often)
                    if (Backup.Stale(item.m_customData)) { v.ClearIf(i, o.Token); Log(Plugin.RavenName(o.Munin) + " at home was an echo (called back from its copy): its seat freed"); continue; }
                    StaleRunes(v, i, o, item, s);
                    Live(v, i, o, item, s);
                }
                if (Time.time >= s.NextBlow) Guard(v, i, o, item, s);
                if (s.Dirty && Time.time >= s.WriteAt) Write(v, i, s);
            }
        }

        // a rune of yours hung on another raven since it was left here (or woven into the в'язь): off this one, its look on
        // the perch too (only your own game knows your runes: someone else's raven is left as it is)
        private static void StaleRunes(PerchView v, int seat, Perches.Occupant o, ItemDrop.ItemData item, Seen s)
        {
            Player p = Player.m_localPlayer;
            if (p == null || o.Pid != p.GetPlayerID()) return;
            string code = RavenWear.Code(item), shown = RuneBook.ShownCode(item, o.Pid);
            if (shown == code) return;
            if (shown.Length == 0) item.m_customData.Remove(RavenWear.ItemKey); else item.m_customData[RavenWear.ItemKey] = shown;
            Soon(s);
            v.Zdo.Set(Perches.SeatKey(seat) + "w", shown);
            Log(Plugin.RavenName(o.Munin) + " at home took off a rune that hangs on another raven now");
        }

        private static double Now { get { return WorldTime.Now; } }

        // the time since the last count: hunger, the feeder, a hunt, strength, a find
        private static void Live(PerchView v, int seat, Perches.Occupant o, ItemDrop.ItemData item, Seen s)
        {
            double now = Now, at = PD(Read(item, SimKey));
            if (at <= 0 || at > now) at = now;
            float day = Power.GameDaySeconds();
            float dt = (float)Math.Min(now - at, day * 10.0);
            item.m_customData[SimKey] = now.ToString("R", CultureInfo.InvariantCulture);
            s.Dirty = true;
            if (!Trust.Active(item)) return;
            bool munin = o.Munin;
            // a hunt that ended
            double hunt = PD(Read(item, HuntKey));
            if (hunt > 0 && hunt <= now)
            {
                item.m_customData.Remove(HuntKey);
                v.Zdo.Set(KeyX(seat), "");
                Satiety.Change(item, UnityEngine.Random.Range(HuntFillMin, HuntFillMax));
                Tally(item, 3, 1f);
            }
            bool hunting = hunt > now;
            // gone empty while nobody was by: it fed itself meanwhile (a raven at home never starves)
            if (Satiety.SyncNow(item) > 0f) { Satiety.Change(item, UnityEngine.Random.Range(HuntFillMin, HuntFillMax)); Tally(item, 3, 1f); }
            // the feeder near: it eats when it asks for food, up to fed
            int ate = 0;
            Feeder.Meal shown = null; // (the first one of this look: its flight to the feeder)
            if (!hunting)
                while (Satiety.Value(item) <= Satiety.AskAt && ate < 4)
                {
                    Feeder.Meal meal = Feeder.Take(v.transform.position, munin, item);
                    if (meal == null) break;
                    if (shown == null) shown = meal;
                    Satiety.Change(item, meal.Fill);
                    int day0 = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
                    item.m_customData[FedDayKey] = day0.ToString();
                    if (meal.Fav) item.m_customData[FavDayKey] = day0.ToString();
                    Tally(item, 4, 1f);
                    Soon(s);
                    ate++;
                }
            if (shown != null) ShowMeal(v, seat, shown, s);
            // not food in the feeder: it flies there and throws it out
            else if (!hunting && v.BirdAt(seat) != null && !v.Busy(seat))
            {
                string junk;
                FeederView tossed = Feeder.TossJunk(v.transform.position, out junk);
                if (tossed != null)
                {
                    ShowMeal(v, seat, new Feeder.Meal { From = tossed }, s);
                    Dictionary<string, string> extra = new Dictionary<string, string>();
                    extra["item"] = junk;
                    v.Say(seat, "home_junk", extra, 1f);
                    Log(Plugin.RavenName(o.Munin) + " of " + o.Name + " threw " + junk + " out of a feeder");
                }
            }
            // nothing there and hungry: it hunts by itself (a long gap: it has hunted meanwhile)
            if (!hunting && Satiety.Value(item) <= Satiety.HungryAt)
            {
                if (dt > HuntSeconds * 3f) { Satiety.Change(item, UnityEngine.Random.Range(HuntFillMin, HuntFillMax)); Tally(item, 3, 1f); }
                else
                {
                    double until = now + HuntSeconds;
                    item.m_customData[HuntKey] = until.ToString("R", CultureInfo.InvariantCulture);
                    v.Zdo.Set(KeyX(seat), until.ToString("R", CultureInfo.InvariantCulture));
                    hunting = true;
                    Soon(s);
                }
            }
            // strength: as on a rest, slower, but up to the top — its guard store first, then its own; not while hungry
            // or away hunting. A guard perch stands out in the open: the open sky takes nothing off there
            if (!hunting && Satiety.Zone(item) > Satiety.Hungry && dt > 0f)
            {
                float fed = Satiety.Zone(item) == Satiety.Fed ? Satiety.FedGainMul : Satiety.Zone(item) == Satiety.Empty ? HungryPace : 1f;
                float mul = o.Mode == OnGuard ? Mathf.Max(1f, v.Mul()) : v.Mul();
                float add = v.PerDay / Mathf.Max(60f, day) * mul * fed * (o.Mode == OnGuard ? GuardPace : HomePace) * dt;
                float store = Store(item), toStore = Mathf.Min(add, StoreCap(item) - store);
                if (toStore > 0f) { SetStore(item, store + toStore); Tally(item, 8, toStore); add -= toStore; }
                float before = Power.Get(item), own = Mathf.Min(add, Trust.Strength(item) - before);
                if (own > 0f) { Power.SetValue(item, before + own); Tally(item, 6, own); }
            }
            // a find a day, into the feeder
            int today = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            int last;
            if (!hunting) Spar(v, seat, o, item, s, now, today);
            if (Fed(item)) item.m_customData[FedDayKey] = today.ToString(); // still full from the feeder: not forgotten
            Neglect(item, today);
            if (!hunting && Satiety.Zone(item) >= Satiety.Fed && (!int.TryParse(Read(item, FindKey), out last) || last < today))
            {
                if (!int.TryParse(Read(item, FindKey), out last)) { item.m_customData[FindKey] = today.ToString(); return; } // (the first day there: none yet)
                string got = Feeder.PutFind(v.transform.position, Find(Read(item, FavDayKey) == today.ToString() ? FavFindChance : FindGoodChance)); // (a fed one only)
                if (got != null) { item.m_customData[FindKey] = today.ToString(); Tally(item, 2, 1f, got); Soon(s); Log(Plugin.RavenName(o.Munin) + " of " + o.Name + " brought " + got + " into a feeder (day " + today + ")"); }
                else if (_noFeederTold.Add(o.Token + "|" + today) && Feeder.Near(v.transform.position).Count == 0) Log(Plugin.RavenName(o.Munin) + " of " + o.Name + " has a find but no feeder within " + FeederReach + " m");
            }
        }

        // Your two at home together (one perch, or perches side by side), both fed and settled: they spar now and then, and
        // each gets a training flight (вишкіл) from it — slower than a real fight, a few a game day.
        internal static float PairNear = 10f;
        private static void Spar(PerchView v, int seat, Perches.Occupant o, ItemDrop.ItemData item, Seen s, double now, int today)
        {
            if (!Fed(item) || Settling(item) > 0f || !Partner(v, o)) return;
            string[] w = Read(item, PairKey).Split('|'); // "last sparring (world time)|day|how many that day"
            double last = w.Length > 0 ? PD(w[0]) : 0;
            int day = -1, count = 0;
            if (w.Length > 2) { int.TryParse(w[1], out day); int.TryParse(w[2], out count); }
            if (day != today) count = 0;
            if (count >= PairPerDay || (last > 0 && now - last < Power.GameDaySeconds() / 24f * PairHours)) return;
            item.m_customData[PairKey] = now.ToString("R", CultureInfo.InvariantCulture) + "|" + today + "|" + (count + 1);
            Strike.AddFlight(item);
            Tally(item, 10, 1f);
            Soon(s);
            v.Say(seat, "home_pair", null, 0.35f); // (someone near hears it now and then)
        }

        // its other raven of the same owner, at home here or on a perch beside, fed and not off hunting
        private static bool Partner(PerchView v, Perches.Occupant o)
        {
            foreach (PerchView pv in PerchView.All)
            {
                if (pv == null || !pv.Valid || Vector3.Distance(pv.transform.position, v.transform.position) > PairNear) continue;
                for (int j = 0; j < pv.Count; j++)
                {
                    Perches.Occupant po = pv.At(j);
                    if (po == null || !po.Home || po.Pid != o.Pid || po.Munin == o.Munin || Hunting(pv, j)) continue;
                    ItemDrop.ItemData pi = Cache(pv, j);
                    if (pi != null && Fed(pi)) return true;
                }
            }
            return false;
        }

        // the meal seen: its bird flies there (a free spot on that feeder, its bird here and not busy; else it just ate)
        private static void ShowMeal(PerchView v, int seat, Feeder.Meal meal, Seen s)
        {
            if (meal.From == null || meal.From.Spots == 0 || v.Busy(seat) || v.BirdAt(seat) == null) return;
            int spot = meal.From.TakeSpot(HomeMeal.EatSeconds + HomeMeal.GiveUp + 4f);
            ZNetView fv = meal.From.GetComponent<ZNetView>();
            if (spot < 0 || fv == null || !fv.IsValid()) return;
            ZDOID id = fv.GetZDO().m_uid;
            s.Serial++;
            v.Zdo.Set(KeyF(seat), s.Serial + "|" + id.UserID + ":" + id.ID + "|" + spot);
        }

        // every game near: a new meal mark → its bird flies to that feeder's spot, eats, and back
        internal static void WatchMeal(PerchView v, int seat)
        {
            string mark = v.Zdo.GetString(KeyF(seat), "");
            Seen s = SeenAt(v, seat);
            if (ReferenceEquals(mark, s.Meal) || mark == s.Meal) return;
            bool first = s.Meal.Length == 0 && Time.time - v.Born < 2f; // (from before this perch was loaded here: old news)
            s.Meal = mark;
            if (first || mark.Length == 0) return;
            string[] w = mark.Split('|');
            string[] z = w.Length > 2 ? w[1].Split(':') : new string[0];
            long user; uint idn; int spot;
            if (z.Length < 2 || !long.TryParse(z[0], out user) || !uint.TryParse(z[1], out idn) || !int.TryParse(w[2], out spot)) return;
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(new ZDOID(user, idn)) : null;
            FeederView f = g != null ? g.GetComponent<FeederView>() : null;
            if (f != null) HomeMeal.Launch(v, seat, f, spot);
        }

        // A whole game day hungry without the feeder (not fed, not one meal from it): trust melts a little each day
        // after — it feels forgotten. Counted once a day.
        private static void Neglect(ItemDrop.ItemData item, int today)
        {
            int seen, fed;
            if (int.TryParse(Read(item, NeglectKey), out seen) && seen >= today) return;
            item.m_customData[NeglectKey] = today.ToString();
            if (!int.TryParse(Read(item, FedDayKey), out fed)) { item.m_customData[FedDayKey] = today.ToString(); return; }
            int days = today - fed - 1;
            if (days <= 0) return;
            Trust.Melt(item, NeglectLoss * Mathf.Min(days, 3)); // (a long absence: at most three days of it at once)
            Tally(item, 9, 1f);
        }

        private static readonly HashSet<string> _noFeederTold = new HashSet<string>(); // (told once a day per raven)

        // what it brings: coins or something precious this often (good), else some rubbish it took a liking to
        private static string Find(float good)
        {
            if (UnityEngine.Random.value < good)
                return UnityEngine.Random.value < FindCoinShare ? "Coins:" + FindCoins : Valuables[UnityEngine.Random.Range(0, Valuables.Length)] + ":1";
            return Junk[UnityEngine.Random.Range(0, Junk.Length)];
        }

        // ---- the tally of what it did since you last came: kills, blows, finds, hunts, meals, strength spent, gained, alarms | the finds
        private const int Slots = 11; // (8: its guard store, 9: days it felt forgotten, 10: sparring)
        private static void Tally(ItemDrop.ItemData item, int slot, float by, string find = null)
        {
            string raw = Read(item, LogKey);
            int bar = raw.IndexOf('/');
            string[] w = (bar >= 0 ? raw.Substring(0, bar) : raw).Split(',');
            float[] n = new float[Slots];
            for (int i = 0; i < Slots && i < w.Length; i++) n[i] = PF(w[i]);
            n[slot] += by;
            string finds = bar >= 0 ? raw.Substring(bar + 1) : "";
            if (find != null)
            {
                List<string> f = new List<string>(finds.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                f.Add(find);
                while (f.Count > 6) f.RemoveAt(0);
                finds = string.Join(";", f.ToArray());
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Slots; i++) { if (i > 0) sb.Append(','); sb.Append(F(n[i])); }
            item.m_customData[LogKey] = sb + "/" + finds;
        }

        // «поки тебе не було: 2 вбивства, 5 ударів, знахідки: Бурштин, Монети ×50; полював 1 раз, їв 3 рази»
        internal static string Report(string raw, bool munin, string mode)
        {
            List<string> parts = ReportParts(raw);
            return parts.Count > 0 ? Lang.T("поки тебе не було — ") + string.Join(" · ", parts.ToArray()) : "";
        }

        // the same, each a part of its own (the book's journal card: a row each)
        internal static List<string> ReportParts(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return new List<string>();
            int bar = raw.IndexOf('/');
            string[] w = (bar >= 0 ? raw.Substring(0, bar) : raw).Split(',');
            int[] n = new int[Slots];
            for (int i = 0; i < Slots && i < w.Length; i++) n[i] = Mathf.RoundToInt(PF(w[i]));
            List<string> parts = new List<string>();
            if (n[1] > 0) parts.Add(Lang.F("ударів: {0}{1}", n[1], (n[0] > 0 ? Lang.F(", убив {0}", n[0]) : "")));
            if (n[5] > 0) parts.Add(Lang.F("запасу на бій: {0}", n[5]));
            if (n[8] > 0) parts.Add(Lang.F("запас варти: +{0}", n[8]));
            if (n[6] > 0) parts.Add(Lang.F("набрався сили: +{0}", n[6]));
            if (n[4] > 0) parts.Add(Lang.F("їв із годівниці: {0}", n[4]));
            if (n[3] > 0) parts.Add(Lang.F("полював сам: {0}", n[3]));
            string finds = bar >= 0 ? raw.Substring(bar + 1) : "";
            if (finds.Length > 0)
            {
                List<string> names = new List<string>();
                foreach (string f in finds.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) names.Add(ItemName(f));
                parts.Add(Lang.F("знахідки в годівниці: {0}", string.Join(", ", names.ToArray())));
            }
            if (n[7] > 0) parts.Add(Lang.F("кликав тебе на поміч: {0}", n[7]));
            if (n[9] > 0) parts.Add(Lang.F("сумував без годівниці днів: {0} (довіра тане)", n[9]));
            if (n[10] > 0) parts.Add(Lang.F("тренувався в парі: {0}", n[10]));
            return parts;
        }

        internal static string ItemName(string spec)
        {
            string[] kv = spec.Split(':');
            GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(kv[0]) : null;
            ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
            string name = d != null ? (Localization.instance != null ? Lang.Game(d.m_itemData.m_shared.m_name) : d.m_itemData.m_shared.m_name) : kv[0];
            return kv.Length > 1 && kv[1] != "1" ? name + " ×" + kv[1] : name;
        }

        // hunting now (away from the perch: not drawn, not on guard)
        internal static bool Hunting(PerchView v, int seat)
        {
            if (v == null || !v.Valid || seat < 0 || seat >= v.Count) return false;
            string x = v.Zdo.GetString(KeyX(seat), "");
            if (x.Length == 0) return false;
            Seen s = SeenAt(v, seat);
            if (!ReferenceEquals(x, s.HuntRaw)) { s.HuntRaw = x; s.HuntUntil = PD(x); } // (read again only when it changes)
            return s.HuntUntil > Now;
        }

        // ---- the guard store: what it gathered on a perch to fight from there (never the strength it came with)
        internal static float Store(ItemDrop.ItemData item) { return Mathf.Clamp(PF(Read(item, StoreKey)), 0f, StoreCap(item)); }
        internal static float StoreCap(ItemDrop.ItemData item) { return Mathf.Round(Trust.Strength(item) * StoreShare); }
        private static void SetStore(ItemDrop.ItemData item, float v) { item.m_customData[StoreKey] = F(Mathf.Clamp(v, 0f, StoreCap(item))); }

        // seconds before it settles in and starts guarding (0: on guard)
        internal static float Settling(ItemDrop.ItemData item)
        {
            double since = PD(Read(item, SinceKey));
            return since <= 0 ? 0f : Mathf.Max(0f, (float)(SettleSeconds - (Now - since)));
        }

        // fed enough to fly out (above asking for food): what the feeder is for
        internal static bool Fed(ItemDrop.ItemData item) { return Satiety.Value(item) > Satiety.AskAt; }

        // you came: its holder starts a new tally (the "#token" word on the seat, from its owner)
        internal static void Ack(PerchView v, int seat, string token, long sender)
        {
            Perches.Occupant o = v.At(seat);
            if (o == null || o.Token != token || !o.Home || PidOf(sender) != o.Pid) return;
            ItemDrop.ItemData item = Cache(v, seat);
            if (item == null || !item.m_customData.Remove(LogKey)) return;
            Seen s = SeenAt(v, seat);
            s.Dirty = true; s.WriteAt = 0f;
        }

        // ---------------------------------------------------------------- who eats at a feeder (its window: FeederUi)

        internal sealed class Eater
        {
            internal bool Munin, Fed;
            internal string Name = "", State = "", ZoneWord = "", Fav = "";
            internal float Sat, Store, StoreMax, Strength, Max;
            internal int Zone;
        }

        // the ravens at home within reach of this feeder (up to four, yours first)
        internal static List<Eater> EatersAt(Vector3 at)
        {
            List<Eater> list = new List<Eater>(4);
            Player me = Player.m_localPlayer;
            long mine = me != null ? me.GetPlayerID() : 0L;
            float r = FeederReach * FeederReach;
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || !v.Valid || (v.transform.position - at).sqrMagnitude > r) continue;
                for (int i = 0; i < v.Count; i++)
                {
                    Perches.Occupant o = v.At(i);
                    ItemDrop.ItemData item = o != null && o.Home ? Cache(v, i) : null;
                    if (item == null) continue;
                    Eater e = new Eater { Munin = o.Munin };
                    Nicks.Nick nick = Nicks.Current(item);
                    e.Name = (nick != null ? nick.Name : Plugin.RavenName(o.Munin)) + (o.Pid != mine && o.Name.Length > 0 ? " · " + o.Name : "");
                    float settle = Settling(item);
                    e.State = Hunting(v, i) ? Lang.T("полює") : settle > 0f ? Lang.F("обживається {0} хв", Mathf.CeilToInt(settle / 60f)) : ModeWord(o.Mode);
                    e.Sat = Satiety.Value(item); e.Zone = Satiety.Zone(item); e.ZoneWord = Satiety.ZoneWord(item); e.Fed = Fed(item);
                    e.Store = Store(item); e.StoreMax = StoreCap(item); e.Strength = Power.Get(item); e.Max = Trust.Strength(item);
                    e.Fav = Satiety.FavKnown(item) ? ItemName(Satiety.FavOf(item)) : "";
                    if (o.Pid == mine) list.Insert(0, e); else list.Add(e);
                }
            }
            if (list.Count > 4) list.RemoveRange(4, list.Count - 4);
            return list;
        }

        // ---------------------------------------------------------------- what you see on the perch

        internal static string Hover(PerchView v, int seat, Perches.Occupant o, bool mine)
        {
            ItemDrop.ItemData item = Cache(v, seat);
            string state = Hunting(v, seat) ? Lang.T("полює") : ModeWord(o.Mode);
            if (item == null) return state;
            float settle = Settling(item);
            if (settle > 0f) state += " · " + Lang.F("обживається ще {0} хв", Mathf.CeilToInt(settle / 60f));
            if (!Fed(item)) state += " · " + Lang.T("голодний — не вилітає");
            string s = state + " · " + Lang.F("запас варти {0}/{1}", Mathf.FloorToInt(Store(item)), Mathf.RoundToInt(StoreCap(item))) + " · " + Lang.F("сила {0}/{1}", Mathf.FloorToInt(Power.Get(item)), Mathf.RoundToInt(Trust.Strength(item)));
            if (!mine) return s;
            return s + " · " + Lang.F("ситість {0} ({1})", Mathf.RoundToInt(Satiety.Value(item)), Satiety.ZoneWord(item)) + " · " + Lang.F("удар −{0} запасу, до {1} м", Mathf.RoundToInt(Cost(o.Mode)), Mathf.RoundToInt(Radius(o.Mode)));
        }
    }
}
#endif
