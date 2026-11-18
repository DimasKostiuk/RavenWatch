#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Вдома»: a raven left on a perch for as long as you like. R looking at a perch (Shift: Munin) sends it there as E
    // does, and once it sits there it is off you for good: no offence (it was not taken off), the shoulder free for
    // another raven. The raven itself (all its data) lives in the perch then (the seat's "b"), the character keeps a
    // note of where it is and a copy (Backup), so the book shows it. Only E at that perch takes it back, at home as on
    // guard (the book only shows where it is). A perch broken while you are not near: it flies to Odin, and comes
    // back as any lost raven does (Backup.Restore at the stones).
    // On a guard perch («Вартова жердина», use: guard) there is no other mode: it stays on guard.
    // Whoever holds the perch counts its life there: hunger, the feeder, its own hunts, its guard store and then its
    // strength (slower than a rest on a perch, but up to the top), a find a day into the feeder, and its blows: after it
    // has settled in, only when fed, at an angry enemy it sees near the perch, each blow paid from its guard store.
    // When nobody is near, nothing happens; the next look counts the time.
    // Three files: here the raven going home, taken back, called, its note in your character and its perch broken;
    // HomeLife.cs its life on the perch (what the holder counts, the tally, the hover); HomeGuard.cs its blows and the alarm.
    internal static partial class Home
    {
        internal const string AtHome = "h", OnGuard = "g";
        internal const string StoreKey = "RavenWatch_guardStore", SinceKey = "RavenWatch_homeSince";
        internal const string PairKey = "RavenWatch_homePair";
        internal const string FedDayKey = "RavenWatch_homeFed", FavDayKey = "RavenWatch_homeFav", NeglectKey = "RavenWatch_homeNeglect";
        internal const string HomeKey = "RavenWatch_home", SimKey = "RavenWatch_homeAt", LogKey = "RavenWatch_homeLog", FindKey = "RavenWatch_homeFind", HuntKey = "RavenWatch_homeHunt";
        private const string NotePrefix = "RavenWatch_home_";
        private const string RpcGone = "RavenWatch_HomeGone", RpcAlarm = "RavenWatch_HomeAlarm";

        // ---- settings (tuning)
        internal static float HomeRadius = 12f, GuardRadius = 25f;   // m round the perch it strikes at
        internal static float HomeCost = 15f, GuardCost = 10f;      // a blow, out of its guard store
        internal static float HomePace = 0.5f, GuardPace = 0.5f;    // it gathers this much slower than on a rest, but up to the top
        internal static float StoreShare = 0.5f;                    // its guard store: up to this share of its strength, filled first
        internal static float SettleSeconds = 600f;                 // it settles in first: no blows till then
        internal static float FightNear = 30f;                      // m: a fight this near the perch (or a raid on it): it will not stay now
        internal static float AttackGap = 8f;                       // s between two flights of one raven
        internal static float FeederReach = 8f;                     // m from the perch to a feeder it eats from
        internal static float HuntSeconds = 360f, HuntFillMin = 10f, HuntFillMax = 15f; // its own hunt keeps it alive, never fed (no flights)
        internal static float HungryPace = 0.5f;                    // asking for food (Satiety.Empty): it gathers at half the pace
        internal static float NeglectLoss = 5f;                     // trust a day once it has gone a whole day without the feeder
        internal static float FavFindChance = 0.75f;                // fed its favourite today: a precious find this often
        internal static float PairHours = 2f; internal static int PairPerDay = 4; // your two at home spar: a training flight each this often, at most so many a game day
        internal static float AlarmGap = 300f, AlarmFar = 60f;      // s between two alarms of one perch; you this far: told
        internal static float BackNear = 60f;                       // a broken perch with you this near: it flies to you, not to Odin
        internal static int FindCoins = 50;
        internal static float FindGoodChance = 0.5f, FindCoinShare = 0.5f;
        internal static readonly string[] Valuables = { "Amber", "AmberPearl", "Ruby", "SilverNecklace" };
        internal static readonly string[] Junk = { "Feathers:3", "Resin:2", "Flint:1", "Stone:3", "BoneFragments:2", "LeatherScraps:2", "Wood:3" };

        internal static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Home: " + s); }

        internal static bool Is(ItemDrop.ItemData item) { return item != null && item.m_customData != null && item.m_customData.ContainsKey(HomeKey); }

        // a raven out of its home again (taken, called, back from its copy): its home marks go, its guard store stays
        internal static void Strip(Dictionary<string, string> data)
        {
            if (data == null) return;
            foreach (string k in new[] { HomeKey, SimKey, LogKey, HuntKey, SinceKey, FedDayKey, FavDayKey, NeglectKey, FindKey, PairKey }) data.Remove(k);
        }

        internal static string ModeWord(string mode) { return mode == OnGuard ? Lang.T("на варті") : Lang.T("вдома"); }

        private static string World { get { return ZNet.instance != null ? ZNet.instance.GetWorldUID().ToString() : ""; } }
        private static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static float PF(string s) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f; }
        private static double PD(string s) { double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0; }

        // ---------------------------------------------------------------- who sends a message (a raven at home is let go by its owner in any later session)

        internal static long PidOf(long sender)
        {
            Player me = Player.m_localPlayer;
            if (me != null && sender == ZDOMan.GetSessionID()) return me.GetPlayerID();
            foreach (Player pl in Player.GetAllPlayers())
                if (pl != null && pl.GetZDOID().UserID == sender) return pl.GetPlayerID();
            if (ZNet.instance == null || ZDOMan.instance == null) return 0L;
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                if (info.m_characterID.UserID != sender) continue;
                ZDO z = ZDOMan.instance.GetZDO(info.m_characterID);
                return z != null ? z.GetLong(ZDOVars.s_playerID, 0L) : 0L;
            }
            return 0L;
        }

        // ---------------------------------------------------------------- the note in the character

        internal sealed class Note
        {
            internal string Token = "", Rid = "", World = "", Mode = AtHome, Perch = "", State = "";
            internal bool Munin;
            internal Vector3 Pos;
            internal float Strength, Max, Sat, Store;
            internal int Level;
            internal string Log = "";
            internal double Since;
            internal bool Odin { get { return State == "odin"; } }   // its perch broke without you: it went to Odin
            internal bool Here { get { return World.Length == 0 || World == Home.World; } }
        }

        private static string Pack(Note n)
        {
            return (n.Munin ? "1" : "0") + "|" + n.Rid + "|" + n.World + "|" + F(n.Pos.x) + "|" + F(n.Pos.y) + "|" + F(n.Pos.z) + "|" + n.Mode + "|" + n.Perch.Replace("|", "/") + "|" + n.State
                + "|" + F(Mathf.Floor(n.Strength)) + "|" + F(n.Max) + "|" + F(Mathf.Round(n.Sat)) + "|" + n.Level + "|" + n.Log.Replace("|", "/") + "|" + n.Since.ToString("R", CultureInfo.InvariantCulture) + "|" + F(Mathf.Floor(n.Store));
        }

        private static Note Unpack(string token, string raw)
        {
            string[] w = (raw ?? "").Split('|');
            if (w.Length < 9) return null;
            Note n = new Note { Token = token, Munin = w[0] == "1", Rid = w[1], World = w[2], Mode = w[6], Perch = w[7], State = w[8] };
            n.Pos = new Vector3(PF(w[3]), PF(w[4]), PF(w[5]));
            if (w.Length > 13) { n.Strength = PF(w[9]); n.Max = PF(w[10]); n.Sat = PF(w[11]); int.TryParse(w[12], out n.Level); n.Log = w[13]; }
            if (w.Length > 14) n.Since = PD(w[14]);
            if (w.Length > 15) n.Store = PF(w[15]);
            return n;
        }

        internal static List<Note> Notes(Player p)
        {
            List<Note> list = new List<Note>();
            if (p == null) return list;
            foreach (KeyValuePair<string, string> kv in p.m_customData)
            {
                if (!kv.Key.StartsWith(NotePrefix, StringComparison.Ordinal)) continue;
                Note n = Unpack(kv.Key.Substring(NotePrefix.Length), kv.Value);
                if (n != null) list.Add(n);
            }
            list.Sort((a, b) => a.Munin.CompareTo(b.Munin));
            return list;
        }

        private static void Save(Player p, Note n)
        {
            if (p == null || n == null) return;
            string packed = Pack(n), was;
            if (p.m_customData.TryGetValue(NotePrefix + n.Token, out was) && was == packed) return;
            Note old = was != null ? Unpack(n.Token, was) : null;
            p.m_customData[NotePrefix + n.Token] = packed;
            if (old == null || Shape(old) != Shape(n)) Version++; // (its numbers and its tale: live in the book's rows, no new page for them)
        }
        private static void Drop(Player p, string token) { if (p != null && p.m_customData.Remove(NotePrefix + token)) Version++; }
        internal static int Version; // the book draws its page again when it changes: one comes or goes, its mode, its state, its level
        private static string Shape(Note n) { return n.Munin + "|" + n.Mode + "|" + n.State + "|" + n.Level + "|" + n.World + "|" + n.Rid + "|" + (n.Log.Length > 0); }

        // a note of mine by its token, as it is now (the book's live rows), or null
        internal static Note NoteOf(Player p, string token)
        {
            string raw;
            return p != null && p.m_customData.TryGetValue(NotePrefix + token, out raw) ? Unpack(token, raw) : null;
        }

        // a seat of mine at home that my character knows (or is being settled now)
        internal static bool Noted(Player p, string token)
        {
            if (p == null || string.IsNullOrEmpty(token)) return false;
            foreach (Commit c in _commits) if (c.Token == token) return true;
            return p.m_customData.ContainsKey(NotePrefix + token);
        }

        // the ravens at home count as with you (Backup's book: no «return» row for them) — not one gone to Odin
        internal static void Ids(Player p, HashSet<string> here)
        {
            foreach (Note n in Notes(p)) if (!n.Odin && n.Rid.Length > 0) here.Add(n.Rid);
        }

        // brought back at the stones: its note of the broken perch goes
        internal static void Forget(Player p, string rid)
        {
            foreach (Note n in Notes(p)) if (n.Rid == rid) Drop(p, n.Token);
        }

        // ---------------------------------------------------------------- R: to stay there

        // R looking at a perch (true: it was a perch, the key is used up)
        // looking at a perch one can leave a raven on (R is ours there: the game's own use of it waits, see HomeKeyPatch)
        internal static bool LookingAtPerch(Player p) { return Looked(p) != null; }

        // The perch you look at: the one under the crosshair, or the one that was there a moment ago — at its edges the
        // crosshair slips off it for a frame or two, and R pressed in such a frame went to the game (the weapons hid or
        // came back) instead of to the raven. Not when you look at something else you could use.
        internal static float HoverGrace = 0.3f;
        private static PerchView _lastPerch;
        private static float _lastPerchAt = -10f;
        private static bool Leavable(PerchView v) { return v != null && v.Valid && !v.IsSpot && !v.IsNest && !v.IsTree; }
        internal static void Hovered(PerchView v) { if (Leavable(v)) { _lastPerch = v; _lastPerchAt = Time.time; } } // (its hover text, asked every frame it is looked at)
        private static PerchView Looked(Player p)
        {
            if (p == null) return null;
            GameObject h = p.GetHoverObject();
            PerchView v = h != null ? h.GetComponentInParent<PerchView>() : null;
            if (v != null) return Leavable(v) ? v : null;
            bool other = h != null && (h.GetComponentInParent<Hoverable>() != null || h.GetComponentInParent<Interactable>() != null);
            if (!other && Leavable(_lastPerch) && Time.time - _lastPerchAt < HoverGrace && Vector3.Distance(p.transform.position, _lastPerch.transform.position) < 6f) return _lastPerch;
            return null;
        }

        // the game's hide went through right by a perch: why, in the log (a few times a session)
        private static int _leakTold;
        internal static void HideLeaked(Player p, BepInEx.Configuration.KeyboardShortcut k)
        {
            if (_leakTold >= 5 || p == null || _lastPerch == null || Time.time - _lastPerchAt > 1f) return;
            _leakTold++;
            GameObject h = p.GetHoverObject();
            Log("R at a perch went to the game (weapons hide): key " + k + " held " + RavenKeys.Held(k.MainKey) + ", hover " + (h != null ? h.name : "none")
                + ", perch seen " + (Time.time - _lastPerchAt).ToString("0.00") + " s ago, " + Vector3.Distance(p.transform.position, _lastPerch.transform.position).ToString("0.0") + " m");
        }

        internal static bool Key(Player p, bool munin)
        {
            PerchView v = Looked(p);
            if (v == null) return false;
            if (HideEaten != Time.frameCount) HideMark = Time.unscaledTime; // the game's hide may come later (on release): it is ours
            Leave(p, v, munin);
            return true;
        }

        // our key at a perch: the game's next hide-weapons press (HomeKeyPatch) is swallowed once, the whole frame
        internal static float HideMark;
        internal static int HideEaten = -1;

        internal static void Leave(Player p, PerchView v, bool munin)
        {
            string mode = v.IsGuard ? OnGuard : AtHome;
            int mine = v.SeatOf(p.GetPlayerID(), munin);
            Perches.Occupant o = mine >= 0 ? v.At(mine) : null;
            if (o != null && o.Home) { RavenMsg.Center(p, Lang.F("{0} і так тут {1}.", Plugin.RavenName(munin), ModeWord(o.Mode))); return; }
            ItemDrop.ItemData item;
            string why = Perches.WhyNot(p, munin, out item);
            // resting on this very perch already: it stays from where it sits
            if (why != null && item != null && Perches.On(item) && Perches.MineOn(p, v, munin) == item && o != null)
            {
                string busy = Fighting(v, munin);
                if (busy != null) { RavenMsg.Center(p, busy); return; }
                Flying(p, item, munin, v, mine, o.Token, mode);
                return;
            }
            if (why != null) { RavenMsg.Center(p, why); return; }
            string fight = Fighting(v, munin);
            if (fight != null) { RavenMsg.Center(p, fight); return; }
            if (string.IsNullOrEmpty(Backup.Id(item))) { RavenMsg.Center(p, Lang.F("{0} ще не освоївся — зачекай хвилину.", Plugin.RavenName(munin))); return; }
            int seat = v.FreeSeat();
            if (seat < 0) { RavenMsg.Center(p, Lang.T("На жердині нема вільного місця.")); return; }
            Perches.Sit(p, v, munin, seat, mode);
        }

        // it is on its way there (the perch took it): it stays once it sits
        private sealed class Commit { internal ItemDrop.ItemData Item; internal bool Munin; internal PerchView View; internal int Seat; internal string Token, Mode; internal float Since, Sent = -1f; }
        private static readonly List<Commit> _commits = new List<Commit>(2);

        internal static void Flying(Player p, ItemDrop.ItemData item, bool munin, PerchView v, int seat, string token, string mode)
        {
            _commits.RemoveAll(c => c.Munin == munin);
            _commits.Add(new Commit { Item = item, Munin = munin, View = v, Seat = seat, Token = token, Mode = mode, Since = Time.time });
            p.Message(MessageHud.MessageType.TopLeft, mode == OnGuard
                ? Lang.F("{0} летить на варту. Звідти він не повернеться, доки ти не прийдеш по нього.", Plugin.RavenName(munin))
                : Lang.F("{0} летить додому, на жердину. Там і лишиться, доки не забереш.", Plugin.RavenName(munin)), 0, RavenItem.Icon(munin));
        }

        internal static float SettleWait = 12f; // s: its flight there done (or too long): it stays

        // the owner's game: each one on its way there, once it sits — the perch is told it stays (with all of it), and
        // once the perch has that, the raven is off you
        private static void Commits(Player p)
        {
            for (int i = _commits.Count - 1; i >= 0; i--)
            {
                Commit c = _commits[i];
                if (c.View == null || !c.View.Valid || !Perches.On(c.Item) || RavenSeat.On(p, c.Munin) != c.Item)
                {
                    _commits.RemoveAt(i);
                    if (c.Sent > 0f) RavenMsg.Center(p, Lang.F("{0} не лишився на жердині.", Plugin.RavenName(c.Munin)));
                    continue;
                }
                Perches.Occupant o = c.View.At(c.Seat);
                if (o == null || o.Token != c.Token) { _commits.RemoveAt(i); continue; }
                if (c.Sent < 0f)
                {
                    if (!c.View.Settled(c.Seat) && Time.time - c.Since < SettleWait) continue;
                    Send(p, c, o);
                    continue;
                }
                if (o.Mode == c.Mode && c.View.Zdo.GetString(KeyB(c.Seat), "").Length > 0) { _commits.RemoveAt(i); Done(p, c); continue; }
                if (Time.time - c.Sent > 6f) { _commits.RemoveAt(i); RavenMsg.Center(p, Lang.F("Жердина не відповіла — {0} просто сидить на ній.", Plugin.RavenName(c.Munin))); }
            }
        }

        // the seat again: the same raven, now with its mode, its id and all its data
        private static void Send(Player p, Commit c, Perches.Occupant o)
        {
            Perches.Charge(c.Item); // what it gained resting so far
            Dictionary<string, string> data = new Dictionary<string, string>(c.Item.m_customData);
            foreach (string k in new[] { Perches.PerchKey, Perches.AtKey, Perches.GotKey, Perches.MulKey, "RavenWatch_worn" }) data.Remove(k);
            data[HomeKey] = c.Mode;
            data[SimKey] = WorldTime.Now.ToString("R", CultureInfo.InvariantCulture);
            data[SinceKey] = data[SimKey]; // it settles in from now
            data[FedDayKey] = (EnvMan.instance != null ? EnvMan.instance.GetDay() : 0).ToString(); // (its day without the feeder counts from here)
            data.Remove(LogKey);
            string[] w = o.Raw.Split('|');
            string[] x = new string[13];
            for (int i = 0; i < 13; i++) x[i] = i < w.Length ? w[i] : "";
            if (x[8].Length == 0) x[8] = "-1";
            x[9] = c.Mode; x[10] = Backup.Id(c.Item) ?? ""; x[11] = RavenVoice.KeptTimbre(c.Item).ToString(); x[12] = Norov.CharIdOf(c.Item); // (11: its speaking timbre, 12: its character)
            c.View.Send(c.Seat, string.Join("|", x) + "\n" + c.View.Outfit(c.Seat) + "\n" + c.View.Zdo.GetString(Perches.SeatKey(c.Seat) + "w", "") + "\n" + Backup.Pack(data));
            c.Sent = Time.time;
        }

        // the perch has it: off you, quietly (no offence, no journal alarm), noted in the character
        private static void Done(Player p, Commit c)
        {
            ItemDrop.ItemData item = c.Item;
            Backup.Save(p, item); // the copy as it left
            string said = Satiety.Say(p, c.Munin, c.Mode == OnGuard ? "guard_leave" : "home_leave"); // (worn still: its own tone)
            if (!string.IsNullOrEmpty(said)) c.View.SayHere(c.Seat, said);
            Note n = new Note { Token = c.Token, Rid = Backup.Id(item) ?? "", Munin = c.Munin, World = World, Mode = c.Mode, Pos = c.View.transform.position, Perch = c.View.Name, Since = WorldTime.Now };
            n.Strength = Power.Get(item); n.Max = Trust.Strength(item); n.Sat = Satiety.Value(item); n.Level = Trust.Level(item);
            Save(p, n);
            foreach (string k in new[] { Perches.PerchKey, Perches.AtKey, Perches.GotKey, Perches.MulKey }) item.m_customData.Remove(k);
            RavenSeat.Forget(item, "forgotten: left at home on a perch");
            if (p.GetInventory().ContainsItem(item)) p.GetInventory().RemoveItem(item);
            RavenItem.ForgetWorn();
            RavenSkills.SyncSoon();
            RavenMsg.Center(p, c.Mode == OnGuard
                ? Lang.F("{0} на варті на «{1}».\nЗабрати його можна лише тут (E).", Plugin.RavenName(c.Munin), c.View.Name)
                : Lang.F("{0} лишився вдома на «{1}».\nЗабрати його можна лише тут (E).", Plugin.RavenName(c.Munin), c.View.Name));
        }

        // ---------------------------------------------------------------- E: taken back

        private sealed class Take { internal PerchView View; internal int Seat; internal string Token, Blob; internal bool Munin; internal float Until; }
        private static readonly List<Take> _takes = new List<Take>(2);

        // why it cannot come to you now, or null
        private static string NoRoom(Player p, bool munin)
        {
            if (RavenSeat.On(p, munin) == null && !RavenSeat.Waiting(p, munin)) return null; // its shoulder is free and it may sit: no bag needed
            return p.GetInventory().HaveEmptySlot() ? null : Lang.F("У сумці нема місця для {0}.", Plugin.RavenNameGen(munin)); // (its shoulder is taken, or it may not sit yet: it goes into the bag)
        }

        internal static void TakeBack(Player p, PerchView v, int seat)
        {
            Perches.Occupant o = v != null ? v.At(seat) : null;
            if (p == null || o == null || !o.Home || o.Pid != p.GetPlayerID()) return;
            foreach (Take t in _takes) if (t.Token == o.Token) return;
            string why = NoRoom(p, o.Munin);
            if (why != null) { RavenMsg.Center(p, why); return; }
            string blob = v.Zdo.GetString(KeyB(seat), "");
            if (blob.Length == 0) { RavenMsg.Center(p, Lang.T("Жердина ще не відповіла — спробуй за мить.")); return; }
            _takes.Add(new Take { View = v, Seat = seat, Token = o.Token, Blob = blob, Munin = o.Munin, Until = Time.time + 5f });
            v.Release(seat, o.Token); // its bird flies to you; the seat goes
        }

        private static void Takes(Player p)
        {
            for (int i = _takes.Count - 1; i >= 0; i--)
            {
                Take t = _takes[i];
                Perches.Occupant o = t.View != null && t.View.Valid ? t.View.At(t.Seat) : null;
                if (o != null && o.Token == t.Token)
                {
                    if (Time.time < t.Until) { string b = t.View.Zdo.GetString(KeyB(t.Seat), ""); if (b.Length > 0) t.Blob = b; continue; } // the latest of it, till the seat goes
                    _takes.RemoveAt(i);
                    RavenMsg.Center(p, Lang.T("Жердина не відпустила ворона — спробуй ще раз."));
                    continue;
                }
                if (t.View == null || !t.View.Valid) { _takes.RemoveAt(i); continue; } // (unloaded meanwhile: the seat stays, try again)
                _takes.RemoveAt(i);
                Back(p, t.Munin, t.Token, Backup.Unpack(t.Blob), Lang.F("{0} знову з тобою.", Plugin.RavenName(t.Munin)));
            }
        }

        // its data made into the raven again: in the bag, onto its shoulder if that is free
        private static void Back(Player p, bool munin, string token, Dictionary<string, string> data, string said) { Back(p, munin, token, data, said, "home_take"); }

        private static void Back(Player p, bool munin, string token, Dictionary<string, string> data, string said, string tag)
        {
            Note n = null;
            foreach (Note x in Notes(p)) if (x.Token == token) n = x;
            // it is with you already (it came to you from its copy while its perch seemed gone, and this one sat on
            // meanwhile): the one on the perch was only its echo — the seat is free, nothing new in the bag
            string rid;
            data.TryGetValue("RavenWatch_id", out rid);
            if (Backup.Stale(data) || Backup.With(p, rid) != null)
            {
                Drop(p, token);
                Log(Plugin.RavenName(munin) + " taken from a perch is with you already (" + (Backup.Stale(data) ? "an older generation" : "the same raven in the bag") + "): only its echo sat there");
                RavenMsg.Center(p, Lang.F("{0} уже з тобою.\nНа жердині сиділо лише його відлуння.", Plugin.RavenName(munin)));
                return;
            }
            string log = data.ContainsKey(LogKey) ? data[LogKey] : "";
            string mode, since;
            bool guard = data.TryGetValue(HomeKey, out mode) && mode == OnGuard;
            double sat = data.TryGetValue(SinceKey, out since) ? WorldTime.Now - PD(since) : 0; // («Нічний сторож»: how long it sat there)
            Strip(data);
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(munin ? RavenItem.MuninPrefab : RavenItem.HuginPrefab) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null) { Log("no raven prefab: " + token + " kept in the note"); return; }
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab; item.m_stack = 1; item.m_equipped = false;
            item.m_customData = data;
            Habits.FromPerch(p, item, munin, guard, sat);
            bool shoulderFree = RavenSeat.On(p, munin) == null;
            if (!shoulderFree || !RavenSeat.OntoShoulder(p, item)) // its shoulder free: straight onto it, the bag never asked
            {
                if (!p.GetInventory().AddItem(item)) { ItemDrop.DropItem(item, 1, p.transform.position + Vector3.up, Quaternion.identity); said += Lang.T("\nСумка повна — він сів поруч на землю."); }
            }
            Drop(p, token);
            Backup.Save(p, item);
            RavenItem.ForgetWorn();
            RavenSkills.SyncSoon();
            string report = Report(log, munin, n != null ? n.Mode : AtHome);
            RavenMsg.Center(p, said + (report.Length > 0 ? "\n" + report : ""));
            if (RavenSeat.On(p, munin) == item && Plugin.Instance != null) Plugin.Instance.StartCoroutine(SayLater(p, munin, tag));
        }

        // its word once it is on your shoulder (its flight in takes a moment)
        private static System.Collections.IEnumerator SayLater(Player p, bool munin, string tag)
        {
            yield return new WaitForSeconds(3.5f);
            if (p == null || p.IsDead() || RavenSeat.On(p, munin) == null) yield break;
            string line = Satiety.Say(p, munin, tag);
            if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(p, munin, line);
        }

        // ---------------------------------------------------------------- the owner's game, twice a second

        private static float _noteAt, _gonePass;
        internal static void Tick(Player p)
        {
            if (p == null || p.IsDead()) return;
            if (_commits.Count > 0) Commits(p);
            if (_takes.Count > 0) Takes(p);
            if (Perches.Ready) Guests.Tick(p); // your ravens' visits to friend ravens on perches near
            if (Time.time < _noteAt || !Perches.Ready) return;
            _noteAt = Time.time + 5f;
            bool saveCopies = Time.time >= _gonePass;
            if (saveCopies) _gonePass = Time.time + 60f;
            foreach (Note n in Notes(p))
            {
                if (n.Odin || !n.Here) continue;
                // its perch broken while you were away: the world keeps a word of it
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GoneKey(n.Token))) { ToOdin(p, n, null); continue; }
                PerchView v; int seat;
                if (!Find(n.Token, out v, out seat)) { Missing(p, n); continue; }
                _missing.Remove(n.Token);
                ItemDrop.ItemData item = Cache(v, seat);
                if (item == null) continue;
                // what it is like now, for the book; its copy kept fresh while you are about
                n.Strength = Power.Get(item); n.Max = Trust.Strength(item); n.Sat = Satiety.Value(item); n.Level = Trust.Level(item);
                n.Log = Read(item, LogKey);
                n.Store = Store(item);
                Save(p, n);
                if (saveCopies) Backup.SaveData(p, n.Rid, n.Munin, item.m_customData);
                // you came back (away a while before): what it did meanwhile; standing by it, you see it yourself
                bool near = Vector3.Distance(p.transform.position, v.transform.position) < 10f;
                float wasNear;
                bool back = near && (!_nearAt.TryGetValue(n.Token, out wasNear) || Time.time - wasNear > AwayFor);
                if (near) _nearAt[n.Token] = Time.time;
                if (back) Greet(v, seat, item, n.Log);
                if (n.Log.Length > 0 && back)
                {
                    string r = Report(n.Log, n.Munin, n.Mode);
                    if (r.Length > 0) p.Message(MessageHud.MessageType.TopLeft, Plugin.RavenName(n.Munin) + ": " + r, 0, RavenItem.Icon(n.Munin));
                    v.Send(seat, "#" + n.Token); // its holder starts a new tally
                }
            }
        }

        internal static float AwayFor = 300f; // s away from it before its tale is told

        // you came back to it: hungry — it says so; it went days without the feeder — it says that; else its tale (if any)
        private static void Greet(PerchView v, int seat, ItemDrop.ItemData item, string log)
        {
            Perches.Occupant o = v.At(seat);
            if (o == null || Plugin.Instance == null) return;
            string tag = !Fed(item) ? "home_hungry" : LogCount(log, 9) > 0 ? "home_forgotten" : log.Length > 0 ? "home_report" : null;
            if (tag == null) return;
            string text = Composer.ComposePerch(Plugin.Instance.PhraseDir, o.Munin, tag, o.Level, o.Name, o.Female, null, o.Voice);
            if (!string.IsNullOrEmpty(text)) v.SayHere(seat, text);
        }

        private static int LogCount(string raw, int slot)
        {
            if (string.IsNullOrEmpty(raw)) return 0;
            int bar = raw.IndexOf('/');
            string[] w = (bar >= 0 ? raw.Substring(0, bar) : raw).Split(',');
            return slot < w.Length ? Mathf.RoundToInt(PF(w[slot])) : 0;
        }
        private static readonly Dictionary<string, float> _nearAt = new Dictionary<string, float>();

        // you stand where it lives, all loaded, and no perch here has it (the world was not saved after it stayed, a
        // mod took the perch away): it comes to you from its copy rather than being lost
        private static readonly Dictionary<string, int> _missing = new Dictionary<string, int>();
        private static void Missing(Player p, Note n)
        {
            if (Vector3.Distance(p.transform.position, n.Pos) > 25f || ZNetScene.instance == null || !ZNetScene.instance.IsAreaReady(n.Pos)) { _missing.Remove(n.Token); return; }
            int miss;
            _missing.TryGetValue(n.Token, out miss);
            _missing[n.Token] = ++miss;
            if (miss < 4 || NoRoom(p, n.Munin) != null) return; // (20 s: a big base streams in slowly)
            _missing.Remove(n.Token);
            ItemDrop.ItemData item = Backup.FromCopy(p, n.Rid);
            if (item == null) { Log("no copy for " + n.Rid + ": its note dropped"); Drop(p, n.Token); return; }
            int near = 0; string seen = "";
            foreach (PerchView v in PerchView.All)
                if (v != null && Vector3.Distance(v.transform.position, n.Pos) < 30f) { near++; seen += " «" + v.Name + "» " + Vector3.Distance(v.transform.position, n.Pos).ToString("0.0") + " m valid " + v.Valid + " seats " + v.Count + ";"; }
            Log(Plugin.RavenName(n.Munin) + " not found on any perch where it lived («" + n.Perch + "», token " + n.Token + "): back from its copy; perches within 30 m: " + near + seen);
            Back(p, n.Munin, n.Token, item.m_customData, Lang.F("Жердини, де жив {0}, тут уже нема.\nВін прилетів до тебе.", Plugin.RavenName(n.Munin)));
        }

        private static bool Find(string token, out PerchView view, out int seat)
        {
            view = null; seat = -1;
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || !v.Valid) continue;
                for (int i = 0; i < v.Count; i++) { Perches.Occupant o = v.At(i); if (o != null && o.Token == token) { view = v; seat = i; return true; } }
            }
            return false;
        }

        private static string GoneKey(string token) { return ("rw_hg_" + token).ToLowerInvariant(); }

        // its perch broke without you near: off to Odin (back from its copy at the stones, as any lost raven)
        private static void ToOdin(Player p, Note n, Dictionary<string, string> data)
        {
            if (data != null) Backup.SaveData(p, n.Rid, n.Munin, data); // the copy as it was on the perch
            n.State = "odin";
            Save(p, n);
            RuneBook.Lost(p, n.Rid); // lost: its runes come off it
            if (ZoneSystem.instance != null) ZoneSystem.instance.RemoveGlobalKey(GoneKey(n.Token));
            RavenMsg.Center(p, Lang.F("Жердину «{0}», де {1} {2}, зламали.\nТебе не було поруч — {3} полетів до Одіна.\nПовернути його можна біля Жертовних каменів.", n.Perch, Plugin.RavenName(n.Munin), (n.Mode == OnGuard ? Lang.T("стояв на варті") : Lang.T("жив")), Plugin.RavenName(n.Munin)));
        }

        // ---------------------------------------------------------------- the perch broke (its holder's game)

        internal static void Broken(PerchView v)
        {
            if (v == null || !v.Valid) return;
            for (int i = 0; i < v.Count; i++)
            {
                Perches.Occupant o = v.At(i);
                if (o == null || !o.Home) continue;
                string blob = v.Zdo.GetString(KeyB(i), "");
                if (ZoneSystem.instance != null) ZoneSystem.instance.SetGlobalKey(GoneKey(o.Token)); // its owner learns it whenever they come
                if (ZRoutedRpc.instance == null) continue;
                ZPackage pkg = new ZPackage();
                pkg.Write(o.Pid); pkg.Write(o.Munin); pkg.Write(o.Token); pkg.Write(blob); pkg.Write(v.transform.position);
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcGone, pkg);
            }
        }

        // its bird where the perch was (sitting, or out on a meal or a blow): yours, you near — the shoulder's raven
        // starts from that very spot and flies to you (it comes back by OnGone); else it is gone in a puff
        internal static void PerchLost(GameObject bird, long pid, bool munin, bool puff)
        {
            if (bird == null) return;
            Player me = Player.m_localPlayer;
            if (me != null && me.GetPlayerID() == pid && !me.IsDead() && Vector3.Distance(me.transform.position, bird.transform.position) < BackNear)
            {
                Perches.NoteLeft(pid, munin, bird.transform.position, bird.transform.rotation, null);
                return; // (the bird itself goes with the perch, or its flight ends it)
            }
            if (!puff) return;
            Raven tpl = Shoulder.Template(munin);
            if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, bird.transform.position, bird.transform.rotation, null, 0.6f, -1);
        }

        internal static void RegisterRpcs()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZPackage>(RpcGone, OnGone);
            ZRoutedRpc.instance.Register<ZPackage>(RpcAlarm, OnAlarm);
        }

        private static void OnGone(long sender, ZPackage pkg)
        {
            Player p = Player.m_localPlayer;
            long pid = pkg.ReadLong(); bool munin = pkg.ReadBool(); string token = pkg.ReadString(); string blob = pkg.ReadString(); Vector3 at = pkg.ReadVector3();
            if (p == null || p.GetPlayerID() != pid) return;
            Note n = null;
            foreach (Note x in Notes(p)) if (x.Token == token) n = x;
            if (n == null || n.Odin) return;
            Dictionary<string, string> data = blob.Length > 0 ? Backup.Unpack(blob) : null;
            // you are by it, alive: it just flies to you
            if (data != null && !p.IsDead() && Vector3.Distance(p.transform.position, at) < BackNear && NoRoom(p, munin) == null)
            {
                if (ZoneSystem.instance != null) ZoneSystem.instance.RemoveGlobalKey(GoneKey(token));
                Back(p, munin, token, data, Lang.F("Жердину зламали — {0} перелетів до тебе.", Plugin.RavenName(munin)), "home_broken");
                return;
            }
            ToOdin(p, n, data);
        }

    }

    // Our key at a perch is ours alone: the game's own action on it (R hides the weapons) does not go too. The game's
    // hide may come on the press or on the release, so: held at a perch, or the one right after our press (3 s), and
    // every ask in that frame. Asked very often: only a pressed button is looked at.
    [HarmonyPatch(typeof(ZInput), "GetButtonDown", new[] { typeof(string) })]
    internal static class HomeKeyPatch
    {
        private static void Postfix(string __0, ref bool __result)
        {
            if (!__result || __0 != "Hide") return;
            try
            {
                if (Home.HideEaten == Time.frameCount) { __result = false; return; }
                Player p = Player.m_localPlayer;
                BepInEx.Configuration.KeyboardShortcut k = RavenKeys.Get(RavenKeys.HomeId);
                bool held = p != null && k.MainKey != KeyCode.None && RavenKeys.Held(k.MainKey) && Home.LookingAtPerch(p);
                bool after = Home.HideMark > 0f && Time.unscaledTime - Home.HideMark < 3f;
                if (!held && !after) { Home.HideLeaked(p, k); return; }
                __result = false;
                Home.HideMark = 0f;
                Home.HideEaten = Time.frameCount;
            }
            catch { }
        }
    }
}
#endif
