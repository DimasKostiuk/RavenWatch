#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // «Пошта» (the user, 2026-10-17): a raven of yours carries a letter or a small thing to another player on the server.
    //   - a letter: a «Пергамент» from Haldor and LetterCost strength; up to MaxChars characters, MaxLines lines;
    //   - a parcel: one thing (coins: a stack, up to MaxWeight kg like anything else), ParcelCost strength.
    // The raven leaves the shoulder: far from the one it is sent to (NearRange and more), it flies off a hundred metres and
    // is gone in its feathers, and after the way's time it comes to them from a hundred metres off (never out of thin air
    // under their nose); near, it flies straight to them. There it hangs in the air before them and hands it over —
    // a fight or an open window may hold it WaitMax seconds at most. A letter opens on their screen; a parcel falls at
    // their feet. Then it is off, and comes back to you the same way.
    //
    // Only between players with the mod (asked anew each time the post opens). The sender's game keeps the trip in the
    // character (what, to whom, until when) and the raven "away" (Trust, the reason Why); the recipient's game keeps the
    // coming letter only in memory and answers once it is handed over. No answer by the deadline: the raven comes back
    // with it (a parcel back in your bag, a blank sheet back). Every game near either player draws the bird (PostBird) from
    // what the two publish on their ZDOs: the sender "out|…" / "home|…", the recipient the letter on its way to them.
    internal static class Post
    {
        internal const string Why = "post";
        internal static float LetterCost = 20f, ParcelCost = 30f;
        internal static float MaxWeight = 2f;
        internal static float NearRange = 100f;      // m: closer, the raven flies straight there
        internal static float Speed = 25f;           // m/s of the unseen way between far players
        internal static float MinTravel = 15f, MaxTravel = 90f;
        internal static float WaitMax = 20f;         // s it waits by them for a fight or a window to end
        internal const int MaxChars = 300, MaxLines = 8;
        internal static int LetterLevel = 2, ParcelLevel = 3; // the trust level a raven carries a letter / a parcel from (each raven its own)

        private const string RpcPing = "RavenWatch_PostPing", RpcPong = "RavenWatch_PostPong", RpcSend = "RavenWatch_PostSend", RpcDone = "RavenWatch_PostDone";
        private const string RpcAsk = "RavenWatch_PostAsk", RpcGo = "RavenWatch_PostGo"; // a parcel: may it be dropped now? (the sender's word first)
        internal const string KeyH = "RavenWatch_PostH", KeyM = "RavenWatch_PostM", KeyIn = "RavenWatch_PostIn"; // on the player's ZDO
        private const string TripH = "RavenWatch_postTripH", TripM = "RavenWatch_postTripM"; // in the sender's character
        internal const string ParcelKey = "RavenWatch_parcel";                                // the parcel being packed, in the character
        internal const int Letter = 0, Parcel = 1;
        internal const char Sep = '\t';

        // the post to yourself, for testing alone: you are on the list, and your raven flies off and comes back to you from
        // afar as to a far player. Off in releases (players only); turned on by hand for a solo test.
        internal static bool SelfTest = false;
        private static long Me { get { return ZDOMan.GetSessionID(); } }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Post: " + s); }
        private static string Inv(double d) { return d.ToString("R", CultureInfo.InvariantCulture); }
        private static double Dbl(string s) { double d; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0; }
        private static float Flt(string s) { float f; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : 0f; }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcPing, new Action<long, ZPackage>(OnPing));
            ZRoutedRpc.instance.Register<ZPackage>(RpcPong, new Action<long, ZPackage>(OnPong));
            ZRoutedRpc.instance.Register<ZPackage>(RpcSend, new Action<long, ZPackage>(OnSend));
            ZRoutedRpc.instance.Register<ZPackage>(RpcDone, new Action<long, ZPackage>(OnDone));
            ZRoutedRpc.instance.Register<ZPackage>(RpcAsk, new Action<long, ZPackage>(OnAsk));
            ZRoutedRpc.instance.Register<ZPackage>(RpcGo, new Action<long, ZPackage>(OnGo));
        }

        // ---------------------------------------------------------------- who is there (asked when the post opens)

        internal sealed class Peer
        {
            public long Uid, Pid;
            public string Name = "";
            public bool Near;     // within NearRange of you (they say so: where they are is not told)
            public bool Mod;      // answered: has the mod
            public bool Open = true;
            public string Why = ""; // why not, when it does not take ravens now
        }

        internal static readonly List<Peer> Peers = new List<Peer>();
        internal static int Version; // the post's popup draws its list again when this changes
        internal static float AskedAt = -100f;

        // the other players on the server (by the game's own list); null when there is nobody to write to
        internal static string NobodyWhy()
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null) return Lang.T("Пошта — лише між гравцями на одному сервері.");
            if (SelfTest) return null;
            int others = 0;
            long me = ZDOMan.GetSessionID();
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList()) if (info.m_characterID.UserID != 0 && info.m_characterID.UserID != me) others++;
            return others == 0 ? Lang.T("На сервері, крім тебе, нікого — нема кому писати.") : null;
        }

        internal static void Ask()
        {
            Peers.Clear();
            Version++;
            if (ZNet.instance == null || ZRoutedRpc.instance == null) return;
            long me = ZDOMan.GetSessionID();
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                long uid = info.m_characterID.UserID;
                if (uid == 0 || uid == me) continue;
                Peers.Add(new Peer { Uid = uid, Name = info.m_name ?? "?" });
            }
            Player self = Player.m_localPlayer;
            if (SelfTest && self != null) Peers.Insert(0, new Peer { Uid = me, Pid = self.GetPlayerID(), Name = self.GetPlayerName() + " (тест)", Mod = true });
            AskedAt = Time.unscaledTime;
            ZPackage ask = new ZPackage();
            ask.Write(self != null ? self.transform.position : Vector3.zero); // (each answers only whether it is near: no one learns where the others are)
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcPing, ask);
        }

        internal static bool Asking { get { return Time.unscaledTime - AskedAt < 2f; } }

        private static void OnPing(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            if (me == null || sender == ZDOMan.GetSessionID() || ZRoutedRpc.instance == null) return;
            string why = Closed(me);
            Vector3 from = Vector3.zero;
            try { from = pkg.ReadVector3(); } catch { }
            ZPackage a = new ZPackage();
            a.Write(me.GetPlayerID());
            a.Write(me.GetPlayerName());
            a.Write(from != Vector3.zero && Vector3.Distance(from, me.transform.position) < NearRange);
            a.Write(why == null);
            a.Write(why ?? "");
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcPong, a);
        }

        // why a raven cannot be taken here now (null: it can)
        private static string Closed(Player me)
        {
            if (Mute.SelfMuted) return Lang.T("ворони до нього не літають");
            if (Incoming.Count >= 3) return Lang.T("до нього вже летять три ворони");
            return null;
        }

        private static void OnPong(long sender, ZPackage pkg)
        {
            try
            {
                long pid = pkg.ReadLong();
                string name = pkg.ReadString();
                bool near = pkg.ReadBool();
                bool open = pkg.ReadBool();
                string why = Clean(pkg.ReadString(), null);
                Peer peer = Peers.Find(x => x.Uid == sender);
                if (peer == null) return; // (not in the server's list of players: not asked)
                peer.Pid = pid; peer.Near = near; peer.Mod = true; peer.Open = open; peer.Why = why;
                if (string.IsNullOrEmpty(peer.Name) || peer.Name == "?") peer.Name = Clean(name, null); // (the server's list names them; their own word only when it did not)
                Version++;
            }
            catch (Exception e) { Log("pong: " + e.Message); }
        }

        // ---------------------------------------------------------------- what may go into a parcel

        // null when this thing may go; else why not
        internal static string ParcelWhyNot(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return Lang.T("Нема чого слати.");
            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            bool m;
            if (RavenItem.IsRaven(item, out m)) return Lang.T("Ворона ворон не носить.");
            if (prefab.StartsWith(RavenGear.PrefabStart, StringComparison.Ordinal) && prefab != RavenItem.LetterPrefab && prefab != RavenItem.RerollPrefab) return Lang.T("Речі воронів поштою не шлють.");
            if (!item.m_shared.m_teleportable) return Lang.T("Це навіть портал не пропускає — ворон теж не понесе.");
            ItemDrop.ItemData.ItemType t = item.m_shared.m_itemType;
            bool small = t == ItemDrop.ItemData.ItemType.Material || t == ItemDrop.ItemData.ItemType.Consumable || t == ItemDrop.ItemData.ItemType.Trophy
                || t == ItemDrop.ItemData.ItemType.Misc || t == ItemDrop.ItemData.ItemType.Ammo || t == ItemDrop.ItemData.ItemType.AmmoNonEquipable || t == ItemDrop.ItemData.ItemType.Fish;
            if (!small) return Lang.T("Зброю, обладунки й знаряддя ворон не понесе — лише дрібні речі.");
            if (item.m_shared.m_weight > MaxWeight) return Lang.F("Заважке для ворона: {0} кг (до {1}).", Satiety.Num(Mathf.Round(item.m_shared.m_weight * 10f) / 10f), Satiety.Num(MaxWeight));
            return null;
        }

        internal static bool IsCoins(ItemDrop.ItemData item) { return item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == "Coins"; }

        // how many of it one parcel holds: one thing; coins up to MaxWeight
        internal static int MaxOf(ItemDrop.ItemData item)
        {
            if (!IsCoins(item)) return 1;
            float w = item.m_shared.m_weight;
            return w > 0.0001f ? Mathf.Max(1, Mathf.FloorToInt(MaxWeight / w + 0.001f)) : 200;
        }

        // ---------------------------------------------------------------- an item as text (to keep and to send)

        internal static string Pack(ItemDrop.ItemData item)
        {
            if (item == null) return "";
            Inventory inv = new Inventory("post", null, 1, 1);
            ItemDrop.ItemData copy = item.Clone();
            copy.m_equipped = false;
            if (!inv.AddItem(copy, new Vector2i(0, 0))) return "";
            ZPackage pkg = new ZPackage();
            inv.Save(pkg);
            return Convert.ToBase64String(pkg.GetArray());
        }

        internal static ItemDrop.ItemData Unpack(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                Inventory inv = new Inventory("post", null, 1, 1);
                inv.Load(new ZPackage(Convert.FromBase64String(text)));
                return inv.GetItemAt(0, 0);
            }
            catch (Exception e) { Log("unpack: " + e.Message); return null; }
        }

        // to the bag, or at the feet when it is full
        internal static void Give(Player p, ItemDrop.ItemData item)
        {
            if (p == null || item == null) return;
            if (!p.IsDead() && p.GetInventory().CanAddItem(item) && p.GetInventory().AddItem(item)) return;
            ItemDrop.DropItem(item, item.m_stack, p.transform.position + p.transform.forward * 0.7f + Vector3.up * 0.5f, Quaternion.identity);
        }

        internal static string NameOf(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return Lang.T("річ");
            string n = Localization.instance != null ? Lang.Game(item.m_shared.m_name) : item.m_shared.m_name;
            return item.m_stack > 1 ? n + " ×" + item.m_stack : n;
        }

        // the parcel being packed (kept in the character until it is sent or taken out)
        internal static ItemDrop.ItemData ParcelOf(Player p)
        {
            string v;
            return p != null && p.m_customData.TryGetValue(ParcelKey, out v) ? Unpack(v) : null;
        }

        internal static void SetParcel(Player p, ItemDrop.ItemData item)
        {
            if (p == null) return;
            string v = item != null ? Pack(item) : "";
            if (string.IsNullOrEmpty(v)) p.m_customData.Remove(ParcelKey); else p.m_customData[ParcelKey] = v;
        }

        // ---------------------------------------------------------------- the letter's words

        // what may stand in a letter: printable characters the book's font has (no emoji the font would show as boxes,
        // no control characters), at most MaxLines lines and MaxChars characters
        internal static string Clean(string text, TMPro.TMP_FontAsset font)
        {
            if (string.IsNullOrEmpty(text)) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);
            int lines = 1;
            for (int i = 0; i < text.Length && sb.Length < MaxChars; i++)
            {
                char c = text[i];
                if (char.IsSurrogate(c)) continue;                       // emoji and the like: not in the font
                if (c == '\r') continue;
                if (c == '\n') { if (lines >= MaxLines) continue; lines++; sb.Append(c); continue; }
                if (c == '\t') c = ' ';
                if (char.IsControl(c)) continue;
                if (font != null && c != ' ' && !font.HasCharacter(c, true)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- the trip (the sender's game)

        private sealed class Trip
        {
            public string Id = "", TargetName = "", Item = "";
            public long TargetUid, TargetPid;
            public int Kind, State;      // State: 0 on its way, 1 handed over, 2 not (it comes back with it)
            public double Deadline, HomeAt;
            public bool Home, Near;
            public float Dir, Travel;
            public long World;           // the world it was sent from (another world: it comes back at once)

            public string Write()
            {
                return string.Join(Sep.ToString(), new string[] { Id, TargetUid.ToString(CultureInfo.InvariantCulture), TargetPid.ToString(CultureInfo.InvariantCulture),
                    TargetName.Replace(Sep, ' '), Kind.ToString(), State.ToString(), Inv(Deadline), Inv(HomeAt), Home ? "1" : "0", Near ? "1" : "0",
                    Inv(Dir), Inv(Travel), Item, World.ToString(CultureInfo.InvariantCulture) });
            }

            public static Trip Read(string s)
            {
                if (string.IsNullOrEmpty(s)) return null;
                string[] w = s.Split(Sep);
                if (w.Length < 13) return null;
                Trip t = new Trip { Id = w[0], TargetName = w[3], Item = w[12] };
                long.TryParse(w[1], out t.TargetUid); long.TryParse(w[2], out t.TargetPid);
                int.TryParse(w[4], out t.Kind); int.TryParse(w[5], out t.State);
                t.Deadline = Dbl(w[6]); t.HomeAt = Dbl(w[7]); t.Home = w[8] == "1"; t.Near = w[9] == "1";
                t.Dir = Flt(w[10]); t.Travel = Flt(w[11]);
                if (w.Length > 13) long.TryParse(w[13], out t.World);
                return t;
            }
        }

        private static Trip TripOf(Player p, bool munin)
        {
            string v;
            return p != null && p.m_customData.TryGetValue(munin ? TripM : TripH, out v) ? Trip.Read(v) : null;
        }

        private static void Save(Player p, bool munin, Trip t)
        {
            if (p == null) return;
            if (t == null) p.m_customData.Remove(munin ? TripM : TripH);
            else p.m_customData[munin ? TripM : TripH] = t.Write();
        }

        internal static bool Carrying(Player p, bool munin) { return TripOf(p, munin) != null; }

        internal static int Letters(Player p)
        {
            int n = 0;
            if (p == null) return 0;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name == RavenItem.LetterPrefab) n += it.m_stack;
            return n;
        }

        private static bool TakeLetter(Player p)
        {
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
                if (it.m_dropPrefab != null && it.m_dropPrefab.name == RavenItem.LetterPrefab) { p.GetInventory().RemoveOneItem(it); return true; }
            return false;
        }

        private static void GiveLetterBack(Player p)
        {
            GameObject g = RavenItem.Letter;
            ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
            if (d == null || p == null) return;
            ItemDrop.ItemData one = d.m_itemData.Clone();
            one.m_dropPrefab = g; one.m_stack = 1;
            Give(p, one);
        }

        internal static float Cost(int kind) { return kind == Parcel ? ParcelCost : LetterCost; }
        internal static int LevelFor(int kind) { return kind == Parcel ? ParcelLevel : LetterLevel; }

        // this raven of yours (awake, on you) trusts you enough for it
        internal static bool LevelOk(Player p, bool munin, int kind)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            return p != null && item != null && !Trust.Stranger(item, p) && Trust.Active(item) && Trust.Level(item) >= LevelFor(kind);
        }

        // the post locked for now: ravens of yours on you, none yet on the letter's level (null: open, or nothing worn —
        // the card says that itself)
        internal static string LockedWhy(Player p)
        {
            if (p == null || (Trust.Worn(false) == null && Trust.Worn(true) == null)) return null;
            if (LevelOk(p, false, Letter) || LevelOk(p, true, Letter)) return null;
            return Lang.F("Відкриється, коли ворон дійде {0}-го рівня довіри.", LetterLevel);
        }

        // why this raven cannot fly off with this post now (null: it can)
        internal static string RavenWhyNot(Player p, bool munin, int kind)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            string name = Plugin.RavenName(munin);
            if (p == null || item == null) return Lang.F("{0} не сидить у тебе на плечі.", name);
            if (Trust.Stranger(item, p)) return Lang.F("{0} не твій — і пошту не понесе.", name);
            if (!Trust.Active(item)) return Lang.F("{0} ще спить.", name);
            if (Trust.Level(item) < LevelFor(kind)) return kind == Parcel ? Lang.F("Посилку — з {0}-го рівня довіри.", ParcelLevel) : Lang.F("Лист — з {0}-го рівня довіри.", LetterLevel);
            if (Carrying(p, munin)) return Lang.F("{0} ще не повернувся з минулою поштою.", name);
            if (Trust.Away(item)) return Trust.AwayText(munin, item);
            if (Satiety.Zone(item) == Satiety.Starving) return Lang.F("{0} голодує — не долетить. Нагодуй його.", name);
            if (!Shoulder.Free(p, munin)) return Lang.F("{0} зараз не на плечі.", name);
            return null;
        }

        // the same in a word or two, for the raven's card (the full sentence goes in its tooltip)
        internal static string RavenWhyShort(Player p, bool munin, int kind)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (p == null || item == null) return Lang.T("не на плечі");
            if (Trust.Stranger(item, p)) return Lang.T("чужий");
            if (!Trust.Active(item)) return Lang.T("спить");
            if (Trust.Level(item) < LevelFor(kind)) return Lang.F("з {0}-го рівня довіри", LevelFor(kind));
            if (Carrying(p, munin)) return Lang.T("ще з поштою");
            if (Trust.Away(item)) return Perches.On(item) && Trust.AwayLeft(item) <= 0f ? Perches.OnWord(item) : Lang.T("відлетів");
            if (Satiety.Zone(item) == Satiety.Starving) return Lang.T("голодує");
            if (!Shoulder.Free(p, munin)) return Lang.T("не на плечі");
            return null;
        }

        internal static string PlaceWhyShort(Player p)
        {
            if (p == null || p.IsDead()) return Lang.T("після смерті");
            if (p.IsTeleporting()) return Lang.T("портал");
            if (Senses.Underground || p.transform.position.y > 3000f) return Lang.T("під землею");
            if (Senses.Indoors) return Lang.T("під дахом");
            return null;
        }

        // why nothing can be sent from here now (null: it can)
        internal static string PlaceWhyNot(Player p)
        {
            if (p == null || p.IsDead()) return Lang.T("Мертвим пошта не ходить.");
            if (p.IsTeleporting()) return Lang.T("Почекай, доки портал тебе відпустить.");
            if (Senses.Underground || p.transform.position.y > 3000f) return Lang.T("Під землею ворон дороги не знайде. Вийди під небо.");
            if (Senses.Indoors) return Lang.T("Під дахом ворон не злетить. Вийди надвір.");
            return null;
        }

        // Sends it. null when the raven is off; "" when it said why itself (strength); else why not
        internal static string Send(Player p, bool munin, int kind, Peer to, string text)
        {
            string why = PlaceWhyNot(p) ?? RavenWhyNot(p, munin, kind);
            if (why != null) return why;
            if (to == null) return Lang.T("Обери, кому.");
            if (!to.Mod) return Lang.F("У {0} нема RavenWatch — ворон не знайде дороги.", to.Name);
            if (!to.Open) return Lang.F("{0}: {1}.", to.Name, to.Why);
            if (ZNet.instance == null || ZRoutedRpc.instance == null) return Lang.T("Пошта — лише між гравцями на одному сервері.");
            bool online = false;
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList()) if (info.m_characterID.UserID == to.Uid) online = true;
            if (to.Uid == Me) online = true;
            if (!online) return Lang.F("{0} уже нема на сервері.", to.Name);
            ItemDrop.ItemData parcel = null;
            if (kind == Letter)
            {
                text = Clean(text, BookArt.Italic ?? BookArt.Body); // (the font it is typed and read in)
                if (text.Trim().Length == 0) return Lang.T("Лист порожній — напиши щось.");
                if (Letters(p) <= 0) return Lang.T("Нема пергаменту. Його продає Хальдор.");
            }
            else
            {
                parcel = ParcelOf(p);
                if (parcel == null) return Lang.T("Посилка порожня — поклади туди річ.");
                string bad = ParcelWhyNot(parcel);
                if (bad != null) return bad;
                text = "";
            }
            string packed = kind == Letter ? "" : Pack(parcel);
            if (kind != Letter && packed.Length == 0) return Lang.T("Цю річ ворон не підхопить.");
            ItemDrop.ItemData raven = Trust.Worn(munin);
            if (!Power.Spend(p, munin, Cost(kind))) return ""; // (it said itself how much it lacks)
            if (kind == Letter) TakeLetter(p);
            else { PostParcel.Drop(); SetParcel(p, null); } // (a parcel window still open must not write the thing back when it shuts)

            Vector3 me = p.transform.position;
            bool near = to.Near && to.Uid != Me; // (to yourself: as to a far player)
            // it leaves the way you look (where they are is not known here; the way's length their game tells in its answer)
            Vector3 look = GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward;
            float dir = new Vector2(look.x, look.z).sqrMagnitude > 0.01f ? Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg : p.transform.eulerAngles.y;
            double now = WorldTime.Now;
            Trip t = new Trip
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 12), TargetUid = to.Uid, TargetPid = to.Pid, TargetName = to.Name, Kind = kind,
                Deadline = now + (near ? 120f : MaxTravel * 2f + 150f), Near = near, Dir = dir,
                Item = packed, World = WorldUid(),
            };
            Save(p, munin, t);
            Trust.SendAway(raven, (float)(t.Deadline - now) + 60f, Why);
            string carry = kind == Letter ? RavenItem.LetterPrefab : (parcel.m_dropPrefab != null ? parcel.m_dropPrefab.name : "");
            ZDO z = NetFlag.Zdo(p, true);
            if (z != null) z.Set(munin ? KeyM : KeyH, string.Join(Sep.ToString(), new string[] { "out", t.Id, t.TargetPid.ToString(CultureInfo.InvariantCulture), Inv(dir), carry, Inv(now), kind.ToString(), near ? "1" : "0" }));

            ZPackage pkg = new ZPackage();
            pkg.Write(t.Id);
            pkg.Write(p.GetPlayerName());
            pkg.Write(p.GetPlayerID());
            pkg.Write(munin);
            pkg.Write(kind);
            pkg.Write(text);
            pkg.Write(t.Item);
            pkg.Write(carry);
            pkg.Write(Outfits.Code(raven));
            pkg.Write(RavenWear.Code(raven));
            pkg.Write(Mathf.Max(1, Trust.Level(raven)));
            pkg.Write(me);
            pkg.Write(near);
            pkg.Write(t.Deadline); // (the recipient hands it over only well before this: after it, this game takes the thing back)
            if (to.Uid == Me) OnSend(Me, new ZPackage(pkg.GetArray()));
            else ZRoutedRpc.instance.InvokeRoutedRPC(to.Uid, RpcSend, pkg);
            Log((munin ? "Munin" : "Hugin") + " off with " + (kind == Letter ? "a letter" : "a parcel (" + carry + ")") + " to " + to.Name + (near ? " (near)" : " (far)") + ", id " + t.Id);
            RavenMsg.Center(p, kind == Letter ? Lang.F("{0} полетів із листом до {1}.", Plugin.RavenName(munin), to.Name) : Lang.F("{0} полетів із посилкою до {1}.", Plugin.RavenName(munin), to.Name));
            SaveProfile(); // (the thing is out of the bag: a crash now must not bring it back)
            return null;
        }

        private static long WorldUid() { return ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L; }
        private static void SaveProfile() { try { if (Game.instance != null) Game.instance.SavePlayerProfile(false); } catch (Exception e) { Log("save: " + e.Message); } }

        // answers that came while you had no body (dead, loading): used once you are back
        private sealed class Answered { public long Sender; public string Id; public bool Ok; public float Travel; }
        private static readonly List<Answered> Unread = new List<Answered>();

        // the answer: handed over or not
        private static void OnDone(long sender, ZPackage pkg)
        {
            string id; bool ok; float travel = 0f;
            try { id = pkg.ReadString(); ok = pkg.ReadBool(); travel = pkg.ReadSingle(); } catch { return; }
            Player p = Player.m_localPlayer;
            // "handed over" is written into the trip at once, even dead (no thing moves; lost with the session, it would
            // give the thing back at the deadline: one in their hands and one in yours). "Not taken" waits for you alive
            // (the thing back into the bag, not by the corpse) — or, lost, comes back at the deadline all the same.
            if (p == null || (p.IsDead() && !ok)) { if (Unread.Count < 8) Unread.Add(new Answered { Sender = sender, Id = id, Ok = ok, Travel = travel }); return; }
            Apply(p, sender, id, ok, travel);
        }

        private static void Apply(Player p, long sender, string id, bool ok, float travel)
        {
            foreach (bool munin in new bool[] { false, true })
            {
                Trip t = TripOf(p, munin);
                if (t == null || t.Id != id || t.State != 0 || sender != t.TargetUid) continue; // (only the one it was sent to answers for it)
                double now = WorldTime.Now;
                t.Travel = t.Near ? 0f : Mathf.Clamp(travel, MinTravel, MaxTravel);
                if (ok) t.State = 1;
                else { t.State = 2; Refund(p, t); }
                t.HomeAt = now + (t.Near ? 0f : t.Travel);
                Save(p, munin, t);
                ItemDrop.ItemData raven = Trust.Worn(munin);
                if (raven != null && Trust.AwayWhy(raven) == Why) Trust.SendAway(raven, (float)(t.HomeAt - now) + 45f, Why); // home by then at the latest
                Log("answer for " + id + ": " + (ok ? "handed over" : "not taken") + ", home in " + Mathf.RoundToInt((float)(t.HomeAt - now)) + " s");
                if (ok) SaveProfile();
            }
        }

        // A parcel is about to be dropped at the recipient's feet: it asks first. Only a trip still on its way, before its
        // deadline, is marked handed over — and the character saved — before the yes goes back: a sender who leaves the
        // game at that moment never gets the thing back as well (at worst the parcel is lost, never doubled)
        private static void OnAsk(long sender, ZPackage pkg)
        {
            string id; float travel;
            try { id = pkg.ReadString(); travel = pkg.ReadSingle(); } catch { return; }
            Player p = Player.m_localPlayer;
            bool ok = false;
            if (p != null)
                foreach (bool munin in new bool[] { false, true })
                {
                    Trip t = TripOf(p, munin);
                    if (t == null || t.Id != id || t.State != 0 || sender != t.TargetUid || WorldTime.Now > t.Deadline - 5.0) continue;
                    Apply(p, sender, id, true, travel); // (handed over: saved at once)
                    ok = true;
                }
            Log("asked to drop parcel " + id + ": " + (ok ? "yes" : "no"));
            ZPackage a = new ZPackage();
            a.Write(id);
            a.Write(ok);
            if (sender == Me) OnGo(Me, new ZPackage(a.GetArray()));
            else if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcGo, a);
        }

        private static void Refund(Player p, Trip t)
        {
            if (t.Kind == Letter) GiveLetterBack(p);
            else Give(p, Unpack(t.Item));
            t.Item = "";
        }

        // the raven home (Trust.Wander, its time away over): what came of it
        internal static void Back(Player p, bool munin, ItemDrop.ItemData item)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z != null) z.Set(munin ? KeyM : KeyH, "");
            Trip t = TripOf(p, munin);
            Save(p, munin, null);
            if (p == null) return;
            if (t == null) { RavenMsg.Center(p, Lang.F("{0} повернувся на плече.", Plugin.RavenName(munin))); return; }
            if (t.State == 0) { t.State = 2; Refund(p, t); } // no answer at all
            string what = t.Kind == Letter ? Lang.T("лист") : Lang.T("посилка"); // (the subject: «посилка — у Valentina»)
            if (t.State == 1) RavenMsg.Center(p, Lang.F("{0} повернувся: {1} — у {2}.", Plugin.RavenName(munin), what, t.TargetName));
            else RavenMsg.Center(p, t.Kind == Letter ? Lang.F("{0} повернувся з листом: {1} його не взяв. Пергамент знову в тебе.", Plugin.RavenName(munin), t.TargetName)
                : Lang.F("{0} повернувся з посилкою: {1} її не взяв. Річ знову в тебе.", Plugin.RavenName(munin), t.TargetName));
        }

        // what the book and hover say while it is off with the post
        internal static string AwayText(Player p, bool munin, ItemDrop.ItemData item)
        {
            Trip t = TripOf(p, munin);
            if (t == null) return Lang.F("{0} повертається з пошти.", Plugin.RavenName(munin));
            if (t.State == 0) return t.Kind == Letter ? Lang.F("{0} несе лист до {1}.", Plugin.RavenName(munin), t.TargetName) : Lang.F("{0} несе посилку до {1}.", Plugin.RavenName(munin), t.TargetName);
            return Lang.F("{0} летить назад від {1}.", Plugin.RavenName(munin), t.TargetName);
        }

        // the same in a word or two, for status lines (Trust.AwayWord)
        internal static string AwayWord(Player p, bool munin)
        {
            Trip t = TripOf(p, munin);
            if (t == null || t.State != 0) return Lang.T("повертається");
            return t.Kind == Letter ? Lang.T("несе лист") : Lang.T("несе посилку");
        }

        internal static float HomeFlySeconds = 12f; // the last stretch, seen: from a hundred metres off to the shoulder

        // the sender's side, twice a second
        private static void TickTrips(Player p)
        {
            if (Unread.Count > 0) { List<Answered> a = new List<Answered>(Unread); Unread.Clear(); foreach (Answered x in a) Apply(p, x.Sender, x.Id, x.Ok, x.Travel); }
            double now = WorldTime.Now;
            long world = WorldUid();
            foreach (bool munin in new bool[] { false, true })
            {
                Trip t = TripOf(p, munin);
                if (t == null) continue;
                ItemDrop.ItemData raven = Trust.Worn(munin);
                // (a trip from another world, whose clock runs elsewhere — over as well)
                if (t.State == 0 && (now > t.Deadline || t.Deadline - now > WorldTime.MaxAhead || (t.World != 0L && world != 0L && t.World != world)))
                {
                    t.State = 2; Refund(p, t); t.HomeAt = now;
                    Save(p, munin, t);
                    Log("no answer for " + t.Id + " by the deadline: it comes back with it");
                }
                if (t.State == 0) continue;
                if (raven == null) { if (now > t.Deadline + 600.0) Save(p, munin, null); continue; } // (not worn: told when it is worn again)
                if (Trust.AwayWhy(raven) != Why) { Back(p, munin, raven); continue; } // already back on the shoulder (called back, or another raven of its kind put on): the trip is over (a trip still on its way never ends so: its thing may yet be handed over)
                if (!t.Home && now >= t.HomeAt)
                {
                    t.Home = true;
                    Save(p, munin, t);
                    if (!t.Near && PlaceWhyNot(p) != null) { Trust.SendAway(raven, 1f, Why); continue; } // far, and you under a roof or the ground: the shoulder's own fly-in (near: its very bird comes back, from wherever)
                    ZDO z = NetFlag.Zdo(p, true);
                    if (z != null) z.Set(munin ? KeyM : KeyH, string.Join(Sep.ToString(), new string[] { "home", t.Id, t.TargetPid.ToString(CultureInfo.InvariantCulture), Inv(t.Dir), "", Inv(now), t.Kind.ToString(), t.Near ? "1" : "0" }));
                    Trust.SendAway(raven, (t.Near ? 30f : HomeFlySeconds + 8f), Why);
                    continue;
                }
                if (t.Home && Trust.AwayLeft(raven) > 1.5f && PostBird.HomeNear(p.GetPlayerID(), munin, p, 10f)) Trust.SendAway(raven, 1f, Why); // its bird is here: onto the shoulder
            }
        }

        // ---------------------------------------------------------------- the coming letter (the recipient's game)

        internal sealed class Coming
        {
            public string Id = "", From = "", Text = "", Item = "", Carry = "", Outfit = "", Wear = "";
            public long FromUid, FromPid;
            public bool Munin, Near, Close;
            public int Kind, Level, State; // State: 0 on its way, 1 the bird is coming in, 2 handed over
            public Vector3 FromPos;
            public float Dir, ArriveAt, StateAt, Cutoff, Travel;
            public double Deadline;      // the sender's, on the world clock (the same in every game, sleep and all)
        }

        internal static float HandMargin = 25f; // s before the sender's deadline: no later is anything handed over

        // too late now: the sender's game is about to take it back (or has)
        private static bool TooLate(Coming c) { return c.Deadline > 0 && WorldTime.Now > c.Deadline - HandMargin; }

        internal static readonly List<Coming> Incoming = new List<Coming>();

        // parcels waiting for their sender's yes before they drop (Hand → OnAsk → OnGo)
        private sealed class Waits { public Coming C; public ItemDrop.ItemData Item; public Vector3 At; public string Line; public float Until; }
        private static readonly List<Waits> Confirming = new List<Waits>();
        internal static float ConfirmSeconds = 10f;

        private static void OnGo(long sender, ZPackage pkg)
        {
            string id; bool ok;
            try { id = pkg.ReadString(); ok = pkg.ReadBool(); } catch { return; }
            Waits w = Confirming.Find(x => x.C.Id == id && x.C.FromUid == sender);
            if (w == null) return; // (too late: given up already)
            Confirming.Remove(w);
            Player me = Player.m_localPlayer;
            if (!ok || me == null) { if (me != null) GoneBack(me, w.C); Log("parcel " + id + ": its sender said no"); return; }
            ItemDrop.DropItem(w.Item, w.Item.m_stack, w.At, Quaternion.identity);
            RavenMsg.Center(me, Lang.F("Посилка від {0}: {1}.", w.C.From, NameOf(w.Item)) + "\n" + Plugin.RavenName(w.C.Munin) + ": «" + w.Line + "»", 2);
            Log("parcel from " + w.C.From + " dropped: " + NameOf(w.Item));
        }

        private static void GoneBack(Player me, Coming c) { RavenMsg.Center(me, Lang.F("{0} покружляв і полетів назад: {1} уже нема у світі.", Plugin.RavenName(c.Munin), c.From)); }

        // no word from the sender in time: the parcel is not dropped (their game gives it back, or it was lost on the way)
        private static void TickConfirming(Player me)
        {
            for (int i = Confirming.Count - 1; i >= 0; i--)
            {
                if (Time.time < Confirming[i].Until) continue;
                Waits w = Confirming[i];
                Confirming.RemoveAt(i);
                Log("parcel " + w.C.Id + ": no word from its sender in time, not dropped");
                if (me != null) GoneBack(me, w.C);
            }
        }

        private static void OnSend(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            Coming c = new Coming { FromUid = sender };
            try
            {
                c.Id = pkg.ReadString(); c.From = pkg.ReadString(); c.FromPid = pkg.ReadLong(); c.Munin = pkg.ReadBool(); c.Kind = pkg.ReadInt();
                c.Text = pkg.ReadString(); c.Item = pkg.ReadString(); c.Carry = pkg.ReadString(); c.Outfit = pkg.ReadString(); c.Wear = pkg.ReadString();
                c.Level = Mathf.Clamp(pkg.ReadInt(), 1, 7); c.FromPos = pkg.ReadVector3(); c.Near = pkg.ReadBool(); c.Deadline = pkg.ReadDouble();
                c.Text = Clean(c.Text, null); // (the font's own check when it is shown: its fonts may not be loaded yet)
                if (ZNet.instance != null) foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList()) if (info.m_characterID.UserID == sender && !string.IsNullOrEmpty(info.m_name)) c.From = info.m_name; // (the server's name for them, not their own word)
                c.From = Clean(c.From, null);
                if (me == null || Closed(me) != null || Incoming.Exists(x => x.Id == c.Id) || TooLate(c)) { Answer(c, false); return; }
                Vector3 flat = new Vector3(c.FromPos.x - me.transform.position.x, 0f, c.FromPos.z - me.transform.position.z);
                float dist = flat.magnitude;
                if (c.Near && dist > NearRange + 30f) c.Near = false; // (moved apart meanwhile)
                c.Travel = c.Near ? 0f : Mathf.Clamp(dist / Mathf.Max(1f, Speed), MinTravel, MaxTravel);
                c.Dir = flat.sqrMagnitude > 1f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : UnityEngine.Random.Range(0f, 360f);
                c.ArriveAt = Time.time + (c.Near ? 0f : Mathf.Max(3f, c.Travel - 6f)); // (its first seconds were the flight off)
                c.Cutoff = Time.time + (c.Near ? 80f : c.Travel + 100f); // (its own patience; the sender's deadline is kept as well: TooLate)
                Incoming.Add(c);
                me.Message(MessageHud.MessageType.TopLeft, c.Kind == Letter ? Lang.F("{0} від {1} летить до тебе з листом.", Plugin.RavenName(c.Munin), c.From)
                    : Lang.F("{0} від {1} летить до тебе з посилкою.", Plugin.RavenName(c.Munin), c.From), 0, RavenItem.Icon(c.Munin));
                Log("coming: " + (c.Kind == Letter ? "a letter" : "a parcel") + " from " + c.From + " by " + (c.Munin ? "Munin" : "Hugin") + (c.Near ? " (near)" : ", in " + Mathf.RoundToInt(c.ArriveAt - Time.time) + " s") + ", id " + c.Id);
            }
            catch (Exception e) { Log("a letter that could not be read: " + e.Message); }
        }

        private static void Answer(Coming c, bool ok)
        {
            if (ZRoutedRpc.instance == null || c.FromUid == 0) return;
            ZPackage a = new ZPackage();
            a.Write(c.Id);
            a.Write(ok);
            a.Write(c.Travel); // (the way back takes as long)
            if (c.FromUid == Me) OnDone(Me, new ZPackage(a.GetArray()));
            else ZRoutedRpc.instance.InvokeRoutedRPC(c.FromUid, RpcDone, a);
        }

        private static string InCode(Coming c, int state)
        {
            return string.Join(Sep.ToString(), new string[] { c.Id, c.Munin ? "1" : "0", c.Level.ToString(), c.Outfit.Replace(Sep, ' '), c.Wear.Replace(Sep, ' '), c.Carry,
                Inv(c.Dir), c.Close ? "1" : "0", state.ToString(), Inv(WorldTime.Now), c.Near ? "1" : "0", c.FromPid.ToString(CultureInfo.InvariantCulture), c.Kind.ToString() });
        }

        private static void Publish(Player me, string code)
        {
            ZDO z = NetFlag.Zdo(me, true);
            if (z != null) z.Set(KeyIn, code ?? "");
        }

        // a fight, a window, the map: it waits (WaitMax at most)
        // (null: free; else why, for the log)
        private static string BusyWhy(Player me)
        {
            if (Senses.Fight) return "a fight";
            if (me.InAttack() || me.IsBlocking()) return "attacking or blocking";
            if (InventoryGui.IsVisible()) return "the bag open";
            if (Minimap.IsOpen()) return "the map open";
            if (Menu.IsVisible()) return "the menu open";
            if (TextInput.IsVisible() || global::Console.IsVisible()) return "typing";
            if (Chat.instance != null && Chat.instance.HasFocus()) return "the chat";
            if (RavenBook.LetterOpen) return "a letter being read";
            if (StoreGui.IsVisible()) return "a window (trader, the book, a nest)"; // (the book, a nest window: StoreGui says so too)
            return null;
        }

        // the one it came to, told once that it waits for them (a window open, a fight)
        private static string _hoverToldId = "";
        private static void TellWaiting(Player me, Coming c)
        {
            if (_hoverToldId == c.Id) return;
            _hoverToldId = c.Id;
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} від {1} чекає перед тобою, поки ти звільнишся.", Plugin.RavenName(c.Munin), c.From), 0, RavenItem.Icon(c.Munin));
        }

        // dead, in a portal, asleep, the ground not loaded yet: it cannot be handed over at all now (null: it can)
        // (not ZNetScene.IsAreaReady: one object of another mod that cannot be made keeps it false for good)
        private static string Away(Player me)
        {
            if (me.IsDead()) return "dead";
            if (me.IsTeleporting()) return "in a portal";
            if (me.InBed()) return "in bed";
            if (ZoneSystem.instance != null && !ZoneSystem.instance.IsZoneLoaded(me.transform.position)) return "the ground not loaded";
            return null;
        }

        private static float _waitToldAt = -100f;
        private static string _waitWhy = "";
        private static void Waiting(Coming c, string why)
        {
            if (why == _waitWhy && Time.time - _waitToldAt < 30f) return;
            _waitToldAt = Time.time; _waitWhy = why;
            Log("letter " + c.Id + " waits: " + why);
        }

        private static void TickIncoming(Player me)
        {
            if (Incoming.Count == 0) return;
            Coming c = Incoming[0];
            float now = Time.time;
            if (c.State == 0)
            {
                if (now > c.Cutoff || TooLate(c)) { Log("could not be handed over in time (its bird never set out): " + c.Id); Answer(c, false); Incoming.RemoveAt(0); return; }
                if (now < c.ArriveAt) return;
                string away = Away(me);
                if (away != null) { Waiting(c, away); return; }
                c.Close = PlaceWhyNot(me) != null; // a roof or the ground over them: it comes out of a puff close by
                c.State = 1; c.StateAt = now;
                Publish(me, InCode(c, 0));
                Log("letter " + c.Id + ": its bird comes in" + (c.Near ? " (the sender's own, near)" : c.Close ? " (close by: a roof or the ground over you)" : " from a hundred metres"));
                return;
            }
            if (c.State == 1)
            {
                if (now > c.Cutoff || TooLate(c)) { Log("could not be handed over in time: " + c.Id); Answer(c, false); c.State = 2; c.StateAt = now; Publish(me, InCode(c, 1)); return; } // (its bird leaves; the code is cleared a little later)
                string away = Away(me);
                if (away != null) { Waiting(c, away); return; }
                PostBird b = PostBird.ForMail(c.Id);
                float hovered = b != null ? b.HoverTime : -1f;
                bool late = now - c.StateAt > (c.Near ? 35f : 45f); // the bird never got here (a wall, not drawn): handed over all the same
                if (!late && hovered < 0.6f) { Waiting(c, b == null ? "its bird is not drawn here" : "its bird is not before you yet"); return; }
                string busy = late ? null : BusyWhy(me);
                if (busy != null && hovered < WaitMax) { Waiting(c, busy); TellWaiting(me, c); return; }
                if (late) Log("letter " + c.Id + ": handed over late (" + Mathf.RoundToInt(now - c.StateAt) + " s), " + (b == null ? "its bird never drawn here" : "its bird hung " + hovered.ToString("0.#", CultureInfo.InvariantCulture) + " s"));
                Hand(me, c, b);
                return;
            }
            if (now - c.StateAt > 15f) { Publish(me, ""); Incoming.RemoveAt(0); } // (its bird is gone by now)
        }

        private static void Hand(Player me, Coming c, PostBird bird)
        {
            c.State = 2; c.StateAt = Time.time;
            Publish(me, InCode(c, 1));
            if (TooLate(c)) { Log("post " + c.Id + " too late: it goes back"); Answer(c, false); return; }
            string line = Grammar.Fix(Remark(c).Replace("{sender}", c.From), me);
            bool senderHere = c.FromUid == Me;
            if (ZNet.instance != null) foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList()) if (info.m_characterID.UserID == c.FromUid) senderHere = true;
            if (c.Kind == Letter)
            {
                if (!senderHere) { Log("letter " + c.Id + " not handed over: its sender left"); return; } // (their game gives the sheet back by itself)
                Answer(c, true);
                RavenBook.ShowLetter(c.From, c.Munin, c.Text, line);
                Log("letter from " + c.From + " handed over");
                return;
            }
            ItemDrop.ItemData item = Unpack(c.Item);
            // the same rules as the sender's game had (a changed game must not mail a stack of iron or a sword)
            if (item != null && (ParcelWhyNot(item) != null || item.m_stack < 1 || item.m_stack > MaxOf(item))) { Log("parcel " + c.Id + " refused: " + NameOf(item) + " is not what a raven carries"); Answer(c, false); return; }
            if (!senderHere || item == null)
            {
                // the sender is gone from the world: the thing goes back with the raven (their game gives it back by itself)
                RavenMsg.Center(me, Lang.F("{0} покружляв і полетів назад: {1} уже нема у світі.", Plugin.RavenName(c.Munin), c.From));
                Log("parcel " + c.Id + " not handed over: " + (item == null ? "unreadable" : "its sender left"));
                Answer(c, false); // (still there after all: the thing back at once, not at the deadline)
                return;
            }
            Vector3 at = bird != null ? bird.transform.position : me.transform.position + me.transform.forward * 0.8f + Vector3.up * 1.4f;
            Vector3 feet = me.transform.position + me.transform.forward * 0.6f;
            at = new Vector3(feet.x, Mathf.Max(feet.y + 0.6f, Mathf.Min(at.y, feet.y + 1.6f)), feet.z); // dropped from its claws, at the feet
            // the sender's game marks it handed over (and saves) first; only then it drops (OnGo)
            Confirming.Add(new Waits { C = c, Item = item, At = at, Line = line, Until = Time.time + ConfirmSeconds });
            ZPackage q = new ZPackage();
            q.Write(c.Id);
            q.Write(c.Travel);
            if (c.FromUid == Me) OnAsk(Me, new ZPackage(q.GetArray()));
            else if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(c.FromUid, RpcAsk, q);
            Log("parcel " + c.Id + ": asking its sender before dropping it");
        }

        // ---------------------------------------------------------------- what it says on handing over

        private static string Remark(Coming c)
        {
            string[] pool = c.Kind == Letter ? (c.Munin ? MuninLetter : HuginLetter) : (c.Munin ? MuninParcel : HuginParcel);
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        private static string[] HuginLetter { get { return Lang.Pool(HuginLetterSrc); } }
        private static readonly string[] HuginLetterSrc = Lang.Keys(
            "Лист від {sender}. Читай повільно — я ж ніс його швидко.",
            "Від {sender}. Почерк жахливий, але слова, здається, щирі.",
            "Пошта Одіна, особиста доставка. Розпишись пір'їною.",
            "Я пролетів пів світу заради цього аркуша. Тож хоча б удай, що ра{дий|да}.",
            "Від {sender}. Я не читав. Ну, хіба трішечки.",
            "Тримай. Пергамент теплий — я ніс його під крилом. Тільки {sender} не кажи, що я розчулився.",
            "Лист від {sender}. Відповісти можна й самому — у тебе ж є ворон. Чи ти дума{в|ла}, я тут на побігеньках?",
            "Доставлено. Чайових не беру, але шматочок м'яса не образить.",
            "Від {sender}. Наступного разу хай пише коротше — у мене крила, а не віз.",
            "Лист. Від {sender}. Ти диви, хтось про тебе таки згадав.");

        private static string[] MuninLetter { get { return Lang.Pool(MuninLetterSrc); } }
        private static readonly string[] MuninLetterSrc = Lang.Keys(
            "На, лист від {sender}. Летів через пів світу — аж пір'я з сраки сиплеться.",
            "Від {sender}. Я тобі не голуб, щоб оцю писанину тягати. Читай і не скигли.",
            "Хапай. І передай {sender}: ще раз пошле мене в таку діру — дзьобну в тім'я.",
            "Лист від {sender}. Шо там понаписували — мені до одного місця. Я своє відлітав.",
            "Пошта, курва. Розписуватись не треба — забери й не стій, як пень.",
            "Від {sender}. Якшо там про любов — я тебе з висоти обісру.",
            "Тримай свою макулатуру від {sender}. І ніяких «дякую» — тіко м'ясо.",
            "На. Від {sender}. Я чесно не читав... брешу, читав. Нудота, хай йому грець.",
            "Лист від {sender}. Ще раз мене ганятимете через увесь світ — обох у болото вкину.",
            "Тримай. Від {sender}. Наступного разу сам{|а} лети, а я посиджу.");

        private static string[] HuginParcel { get { return Lang.Pool(HuginParcelSrc); } }
        private static readonly string[] HuginParcelSrc = Lang.Keys(
            "Посилка від {sender}. Ловити не треба — вона вже під ногами.",
            "{sender} передає гостинця. Я ніс обережно. Майже весь час.",
            "Від {sender}. Не питай, чи я зазирав усередину. Я ворон, а не злодій. Здебільшого.",
            "Доставлено цілим. Майже. Подряпина — то від вітру, не від мене.",
            "Подарунок від {sender}. Добре, що не ковадло.",
            "Тримай, від {sender}. Лапи затерпли, тож поклав просто тобі під ноги.");

        private static string[] MuninParcel { get { return Lang.Pool(MuninParcelSrc); } }
        private static readonly string[] MuninParcelSrc = Lang.Keys(
            "Лови, від {sender}! Ой, не впійма{в|ла}? Ну то підбирай із землі.",
            "Від {sender}. Важке, як гріхи твої. Більше такого не носитиму.",
            "На, тримай свою дрібноту від {sender}. Я тобі не віз, хай йому біс.",
            "Посилка від {sender}. Якшо щось пропало — то так і було.",
            "Кидаю під ноги, бо в руки тобі давати — шо в болото. Від {sender}.",
            "Від {sender}. Ще раз мене навантажать — ляжу й здохну просто тут.");

        // ---------------------------------------------------------------- the clock

        // From Shoulder.Tick (twice a second, every game)
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p != null)
            {
                if (!p.IsDead()) try { TickTrips(p); } catch (Exception e) { Log("trips: " + e); } // (dead: what comes back waits for you alive)
                try { TickIncoming(p); } catch (Exception e) { Log("incoming: " + e); }
                try { TickConfirming(p); } catch (Exception e) { Log("confirming: " + e); }
            }
            PostBird.Scan();
        }

        internal static void Reset() { Unread.Clear(); Incoming.Clear(); Confirming.Clear(); Peers.Clear(); PostParcel.Reset(); PostBird.Forget(); }
    }
}
#endif
