using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Admin console commands: "raven <what> ...". Only for the server's admins (the mod's own check,
    // the same one the admin panel uses). A last argument "@Name" runs the command on that player's game:
    // the request goes through the server, which checks the admin again.
    internal static class Commands
    {
        private const string RpcAsk = "RavenWatch_AdminCmd";   // admin -> server
        private const string RpcRun = "RavenWatch_AdminRun";   // server -> target player
        private const string RpcOut = "RavenWatch_AdminOut";   // target player -> admin

        private static readonly string[] Words =
        {
            "help", "quick", "visit", "diary",
#if RAVEN_ITEM
            "book", "give", "power", "reroll", "perk", "perks", "ready", "info", "shoulder", "tilt", "icons", "trust", "guide", "wings", "stroke", "pose", "eyes", "gift", "perf", "cells", "talk", "cross", "emote", "world", "pass", "voice", "voices", "gossip", "dummy", "nick", "director", "starve", "sat", "legs", "fit", "perch", "sit", "situi", "land", "anim", "flight", "templates", "wingui", "panim", "forget", "workshop", "photo", "roost", "test", "sounds", "norov", "looks", "wear", "runes", "bind", "errand", "nest", "trial", "tree", "back", "home", "favour",
#endif
        };

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcAsk, new Action<long, ZPackage>(OnAsk));
            ZRoutedRpc.instance.Register<ZPackage>(RpcRun, new Action<long, ZPackage>(OnRun));
            ZRoutedRpc.instance.Register<ZPackage>(RpcOut, new Action<long, ZPackage>(OnOut));
        }

        internal static void AddToTerminal()
        {
            new Terminal.ConsoleCommand("raven", "[RavenWatch] admin commands for the ravens, see: raven help",
                new Terminal.ConsoleEvent(OnConsole), false, false, false, false, false, false,
                new Terminal.ConsoleOptionsFetcher(() => new List<string>(Words)));
        }

        private static void OnConsole(Terminal.ConsoleEventArgs args)
        {
            Terminal t = args.Context;
#if RAVEN_ITEM
            // the book for everybody: with no raven worn it is the way to a lost raven's return
            if (args.Length == 2 && args[1].ToLowerInvariant() == "book")
            {
                if (Player.m_localPlayer == null) { t.AddString("raven: персонаж ще не в світі"); return; }
                RavenBook.Open();
                return;
            }
#endif
            if (!Plugin.IsLocalAdmin()) { t.AddString("raven: лише для адміна сервера"); return; }
            List<string> words = new List<string>();
            for (int i = 1; i < args.Length; i++) words.Add(args[i]);
            string target = null;
            if (words.Count > 0 && words[words.Count - 1].StartsWith("@"))
            {
                target = words[words.Count - 1].Substring(1);
                words.RemoveAt(words.Count - 1);
            }
            string line = string.Join(" ", words.ToArray());
            if (string.IsNullOrEmpty(target) || (Player.m_localPlayer != null && target == Player.m_localPlayer.GetPlayerName()))
            {
                t.AddString(Run(line));
                return;
            }
            if (ZRoutedRpc.instance == null) { t.AddString("raven: немає з'єднання з сервером"); return; }
            ZPackage pkg = new ZPackage();
            pkg.Write(target);
            pkg.Write(line);
            ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcAsk, pkg);
            t.AddString("raven: надіслано гравцю " + target + "…");
        }

        // on the server: an admin wants something done on another player's game
        private static void OnAsk(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            string target = pkg.ReadString();
            string line = pkg.ReadString();
            if (!Plugin.IsAdminSender(sender)) { Reply(sender, "raven: сервер відмовив — тебе немає в adminlist.txt"); return; }
            long uid = 0;
            if (Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerName() == target) uid = ZDOMan.GetSessionID(); // the host himself
            else
            {
                ZNetPeer peer = ZNet.instance.GetPeerByPlayerName(target);
                if (peer != null) uid = peer.m_uid;
            }
            if (uid == 0) { Reply(sender, "raven: гравця " + target + " немає на сервері"); return; }
            ZPackage run = new ZPackage();
            run.Write(sender);
            run.Write(line);
            ZRoutedRpc.instance.InvokeRoutedRPC(uid, RpcRun, run);
        }

        // on the target player's game: only the server may ask
        private static void OnRun(long sender, ZPackage pkg)
        {
            if (sender != Plugin.ServerPeerId() && sender != ZDOMan.GetSessionID()) return;
            long admin = pkg.ReadLong();
            string line = pkg.ReadString();
            string name = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "?";
            Reply(admin, "[" + name + "] " + Run(line));
        }

        private static void Reply(long to, string text)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(text);
            ZRoutedRpc.instance.InvokeRoutedRPC(to, RpcOut, pkg);
        }

        private static void OnOut(long sender, ZPackage pkg)
        {
            string text = pkg.ReadString();
            if (global::Console.instance != null) global::Console.instance.AddString(text);
            if (Chat.instance != null) Chat.instance.AddString(text);
        }

        // ---------------------------------------------------------------- the commands themselves

        internal static string Run(string line)
        {
            string[] a = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = a.Length > 0 ? a[0].ToLowerInvariant() : "help";
            Player p = Player.m_localPlayer;
            if (p == null && cmd != "help") return "raven: персонаж ще не в світі";
            try
            {
                switch (cmd)
                {
                    case "quick": return Plugin.Instance.AdminQuick(Raven(a, 1));
                    case "visit": return Plugin.Instance.AdminVisit(Raven(a, 1));
                    case "diary": return Plugin.Instance.AdminDiary();
#if RAVEN_ITEM
                    case "give": return Give(p, a);
                    case "power": return PowerCmd(p, a);
                    case "reroll": return Reroll(p, a);
                    case "norov": return NorovCmd(p, a);
                    case "perk": return Perk(p, a);
                    case "perks": return Perks.ListIds();
                    case "ready": RavenSkills.ResetCooldowns(); return "Перезарядки дарів і образи воронів (очікування, щоб знову посадити на плече) скинуто.";
                    case "trust": return TrustCmd(a);
                    case "icons": AbilityIcons.Reload(); return "Іконки воронів з моделей: " + RavenItem.RetryIcons();
                    case "info": return Info(p);
                    case "shoulder": return ShoulderCmd(a);
                    case "tilt": return TiltCmd(a);
                    case "wings": return WingsCmd(a);
                    case "stroke": return StrokeCmd(p, a);
                    case "cells": return CellsCmd(a);
                    case "director": return Director.Describe();
                    case "talk": return RavenTalk.Force(p, a.Length > 1 ? a[1].ToLowerInvariant() : "idle");
                    case "dummy": return Dummy.Cmd(p, a);
                    case "gossip": return Gossip.Cmd(p);
                    case "voice": return Voice.Cmd(p, a);
                    case "voices": return Voice.Samples(p, a);
                    case "pass": return PassTalk.Force(p, a.Length > 1 ? a[1].ToLowerInvariant() : null);
                    case "emote": return EmoteTalk.Force(p, a.Length > 1 ? a[1].ToLowerInvariant() : null);
                    case "world": return WorldVisit.Force(p, a.Length > 1 && a[1].ToLowerInvariant().StartsWith("m"));
                    case "cross": return CrossTalk.Force(p, a.Length > 1 ? a[1].ToLowerInvariant() : "greet");
                    case "nick": return Nicks.Cmd(p, a.Length > 1 && a[1].ToLowerInvariant().StartsWith("m"), a.Length > 2 ? a[2] : null);
                    case "guide": return Guide.Reset(p);
                    case "eyes": return EyesCmd(a);
                    case "looks": if (a.Length > 1 && a[1].ToLowerInvariant() == "diag") return LookDiag.Cmd(); DevPanel.Toggle(DevPanel.LooksTab); return DevPanel.Said();
                    case "wear": return RavenWear.Cmd(p, a);
                    case "runes": return RuneBook.Cmd(p, a);
                    case "bind": return BindRun.Cmd(p, a);
                    case "errand": return Errands.Cmd(p, a);
#if RAVEN_STORY
                    case "story": return Story.Cmd(p, a);
                    case "npc": return Npcs.Cmd(p, a);
#endif
                    case "nest": return Nest.Cmd(p, a);
                    case "trial": return Trial.Cmd(p, a);
                    case "tree": return RavenTree.Cmd(p, a);
                    case "favour": return Favour.Cmd(p, a);
                    case "back": return ErrandMap.Back(p);
                    case "home": return ErrandMap.Home(p);
                    case "pose": return PoseCmd(p, a);
                    case "legs": return LegsCmd(a);
                    case "sit": return SitCmd(p, a);
                    case "fit": return FitCmd(p, a);
                    case "perch": return PerchCmd(a);
                    case "anim": if (a.Length > 1 && a[1].ToLowerInvariant() == "log") AnimDirector.LogOn = !AnimDirector.LogOn; return AnimDirector.Report(p);
                    case "test": DevPanel.Toggle(-1); return DevPanel.Said();
                    case "sounds": DevPanel.Toggle(DevPanel.SoundsTab); return DevPanel.Said();
                    case "wingui": DevPanel.Toggle(DevPanel.WingsTab); return DevPanel.Said();
                    case "panim": DevPanel.Toggle(DevPanel.PlayerTab); return DevPanel.Said();
                    case "land": DevPanel.Toggle(DevPanel.AnimTab); return DevPanel.Said();
                    case "flight": DevPanel.Toggle(DevPanel.FlightTab); return DevPanel.Said();
                    case "templates": DevPanel.Toggle(DevPanel.TemplatesTab); return DevPanel.Said();
                    case "situi": DevPanel.Toggle(DevPanel.SitTab); return DevPanel.Said();
                    case "gift": return GiftCmd(p, a);
                    case "forget": return ForgetCmd(p, a);
                    case "workshop": DevPanel.Toggle(DevPanel.WorkshopTab); return DevPanel.Said();
                    case "photo": DevPanel.Toggle(DevPanel.PhotoTab); return DevPanel.Said();
                    case "roost": return Perches.CallAll(p);
                    case "starve": return StarveCmd(a);
                    case "sat": return SatCmd(a);
                    case "perf": return Perf.Start(10f);
#endif
                    default: return Help();
                }
            }
            catch (Exception e) { return "raven: помилка — " + e.Message; }
        }

        // a character taken to another world by mistake: the ravens (and the game) forget the lands seen there
        private static readonly System.Reflection.FieldInfo KnownBiome = AccessTools.Field(typeof(Player), "m_knownBiome");

        private static string ForgetCmd(Player p, string[] a)
        {
            if (a.Length < 2) return "raven forget <біом> [біом…] — напр. raven forget Mistlands AshLands\nБіоми: " + string.Join(", ", Enum.GetNames(typeof(Heightmap.Biome)));
            List<ItemDrop.ItemData> ravens = new List<ItemDrop.ItemData>();
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            if (h != null) ravens.Add(h);
            if (m != null) ravens.Add(m);
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
            {
                bool mun;
                if (RavenItem.IsRaven(it, out mun) && !ravens.Contains(it)) ravens.Add(it);
            }
            HashSet<string> known = KnownBiome != null ? KnownBiome.GetValue(p) as HashSet<string> : null;
            List<string> said = new List<string>();
            for (int i = 1; i < a.Length; i++)
            {
                if (a[i].StartsWith("@")) continue;
                string biome = null;
                foreach (string n in Enum.GetNames(typeof(Heightmap.Biome)))
                    if (string.Equals(n, a[i], StringComparison.OrdinalIgnoreCase)) biome = n;
                if (biome == null || biome == "None" || biome == "All") { said.Add(a[i] + ": такого біому нема"); continue; }
                int mems = 0;
                foreach (ItemDrop.ItemData r in ravens) mems += Memory.Forget(r, biome);
                bool place = Power.ForgetPlace(p, biome);
                string token = "$biome_" + biome.ToLowerInvariant(); // the game keeps the key and the name in the language of the day
                string local = Localization.instance != null ? Localization.instance.Localize(token) : token;
                int game = known != null ? known.RemoveWhere(k => k == token || k == local || string.Equals(k, biome, StringComparison.OrdinalIgnoreCase)) : 0;
                said.Add(biome + ": спогадів воронів " + mems + ", перший візит " + (place ? "скинуто" : "не було") + ", гра забула біом: " + (game > 0 ? "так" : "ні"));
            }
            return "Ворони забули (" + ravens.Count + " ворон(и) у тебе):\n" + string.Join("\n", said.ToArray()) + "\nЗбережеться разом із персонажем (вихід чи автозбереження).";
        }

        private static string Help()
        {
            return "RavenWatch, команди адміна (в кінці можна дописати @Нік — виконати в іншого гравця):\n"
                + "  raven quick [hugin|munin]  — швидкий ворон прямо зараз\n"
                + "  raven visit [hugin|munin]  — звичайний ворон прямо зараз\n"
                + "  raven diary                — що Хугін зараз знає про останні хвилини\n"
#if RAVEN_ITEM
                + "  raven book                — відкрити Книгу воронів (може будь-хто, і без ворона)\n"
                + "  raven workshop            — Майстерня (вкладка тестового вікна): своя модель з деталей гри\n"
                + "  raven give hugin|munin|stone [кількість] — видати предмет\n"
                + "  raven power hugin|munin|all <число>       — встановити силу ворона (до межі його рівня довіри)\n"
                + "  raven trust hugin|munin <рівень> [очки]  — встановити рівень довіри й очки\n"
                + "  raven reroll [hugin|munin]              — перекинути благо й тягар\n"
                + "  raven perk hugin|munin <благо> <тягар>  — задати точні благо й тягар (список: raven perks)\n"
                + "  raven ready                             — скинути перезарядки, Останній шанс, образи воронів і денне годування\n"
                + "  raven info                              — ворони на тобі: благо й тягар, сила, перезарядки\n"
                + "  raven forget <біом> [біом…]             — ворони й гра забувають землю (напр. AshLands): знову «вперше тут»\n"
                + "  raven shoulder <розмір> [x y z [rx ry rz]] — підібрати, як ворон сидить на плечі (зберігається в конфіг)\n"
                + "  raven tilt <мунін°> [хугін°]             — нахил воронів убік від голови (мінус — до голови)\n"
                + "  raven pose [fury|scream|heal [назовні вгору лікоть [внутр. назовні вгору лікоть [тремтіння]]]] — крила Муніна під дари: показати 3 с, з числами — змінити (до перезапуску)\n"
                + "  raven gift [hugin|munin]                — дарунок від Одіна прямо зараз, як після ритуалу (рівні 3–6)\n"
                + "  raven starve [hugin|munin]              — ситість 0: ворон голодує, перше клювання за 15 с\n"
                + "  raven sat <0-100> [hugin|munin]         — задати ситість (без числа: показати її й улюблену їжу)\n"
                + "  raven fit [on|off|again]                — посадка на плечі по броні/одягу: скільки піднято зараз; again — переміряти\n"
                + "  raven perch [стегно коліно гомілка [опустити см]|swap] — зігнута лапа біля шиї, коли ворон сидить; swap — інша лапа\n"
                + "  raven anim [log]                        — режисер анімацій: що зараз робить кожен ворон і хто його веде; log — лог переходів увімк/вимк\n"
                + "  raven test                              — тестове вікно: Посадка, Політ, Шаблони, Змах крил, Майстерня, Звуки, Пасивки (F8 — мишка: вікно / гра)\n"
                + "  raven land / flight / templates / wingui / workshop / sounds — те саме вікно одразу на своїй вкладці\n"
                + "  raven legs [стегно коліно гомілка пальці|off|on] — лапи в польоті (купол, удар): підтягнуті назад; числа — спробувати (до перезапуску)\n"
                + "  raven eyes [відсоток|off]                — світіння очей воронів: без числа — що в матеріалі (і в лог), з числом — усім стільки %, off — назад за рівнями довіри\n"
                + "  raven icons                             — заново зняти іконки воронів (або взяти твої з config/RavenWatch/icons)\n"
#endif
                ;
        }

        // "hugin" by default, "munin" when asked
        private static bool Raven(string[] a, int i)
        {
            return a.Length > i && a[i].ToLowerInvariant().StartsWith("m");
        }

#if RAVEN_ITEM
        private static string Give(Player p, string[] a)
        {
            if (a.Length < 2) return "raven give hugin|munin|stone [кількість]";
            string what = a[1].ToLowerInvariant();
            GameObject prefab = what.StartsWith("h") ? RavenItem.Hugin : what.StartsWith("m") ? RavenItem.Munin : what.StartsWith("s") ? RavenItem.Reroll : null;
            if (prefab == null) return "raven give: hugin, munin або stone";
            int n = 1;
            if (a.Length > 2) int.TryParse(a[2], out n);
            n = Mathf.Clamp(n, 1, 20);
            int given = 0;
            for (int i = 0; i < n; i++)
            {
                if (p.GetInventory().AddItem(prefab, 1)) { given++; continue; }
                UnityEngine.Object.Instantiate(prefab, p.transform.position + p.transform.forward + Vector3.up, Quaternion.identity); // a full bag: at the feet
                given++;
            }
            return "Видано: " + prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name + " ×" + given;
        }

        // the raven of that kind: the worn one first, else one from the bag
        private static ItemDrop.ItemData Find(Player p, bool munin)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            ItemDrop.ItemData worn = munin ? m : h;
            if (worn != null) return worn;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems())
            {
                bool isMunin;
                if (RavenItem.IsRaven(item, out isMunin) && isMunin == munin) return item;
            }
            return null;
        }

        // SeneaL UI's shoulder cells, tried live (not saved: the config keeps the numbers)
        private static string CellsCmd(string[] a)
        {
            float v;
            System.Globalization.CultureInfo c = CultureInfo.InvariantCulture;
            if (a.Length > 1 && float.TryParse(a[1], NumberStyles.Float, c, out v)) SeneaLShoulders.Size = Mathf.Clamp(v, 16f, 40f);
            if (a.Length > 2 && float.TryParse(a[2], NumberStyles.Float, c, out v)) SeneaLShoulders.OutX = Mathf.Clamp(v, -30f, 30f);
            if (a.Length > 3 && float.TryParse(a[3], NumberStyles.Float, c, out v)) SeneaLShoulders.DownY = Mathf.Clamp(v, -30f, 30f);
            return "Плечі SeneaL UI: розмір " + SeneaLShoulders.Size.ToString("0.#", c) + ", назовні " + SeneaLShoulders.OutX.ToString("0.#", c) + ", вниз " + SeneaLShoulders.DownY.ToString("0.#", c)
                + " (raven cells <розмір> <назовні> <вниз>; не зберігається — впиши числа в налаштування)";
        }

        private static string PowerCmd(Player p, string[] a)
        {
            float v;
            if (a.Length < 3 || !float.TryParse(a[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return "raven power hugin|munin|all <число>";
            string who = a[1].ToLowerInvariant();
            List<string> done = new List<string>();
            foreach (bool munin in new[] { false, true })
            {
                if (who != "all" && Raven(a, 1) != munin) continue;
                ItemDrop.ItemData item = Find(p, munin);
                if (item == null) continue;
                Power.SetValue(item, v);
                done.Add(Plugin.RavenName(munin) + ": " + Mathf.FloorToInt(Power.Get(item)));
            }
            return done.Count > 0 ? "Сила — " + string.Join(", ", done.ToArray()) : "Такого ворона немає ні на тобі, ні в сумці.";
        }

        private static string Reroll(Player p, string[] a)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            bool munin = a.Length > 1 ? Raven(a, 1) : (h == null && m != null);
            ItemDrop.ItemData item = Find(p, munin);
            if (item == null) return "Такого ворона немає ні на тобі, ні в сумці.";
            Perks.Roll(item, munin);
            return Plugin.RavenName(munin) + ": " + Perks.Short(item);
        }

        // «raven norov h|m [character|-] [knack|-] [quirk|-]»: set this raven's norov (a dash or nothing: rolled anew); for tests
        private static string NorovCmd(Player p, string[] a)
        {
            if (a.Length < 2)
            {
                string cs = "", ks = "", qs = "";
                foreach (Norov.Character c in Norov.Characters) cs += (cs.Length > 0 ? ", " : "") + c.Id + " (" + c.Name + ")";
                foreach (Norov.Knack k in Norov.Knacks) ks += (ks.Length > 0 ? ", " : "") + k.Id + " (" + k.NameH + ")";
                foreach (Norov.Quirk q in Norov.QuirkList) qs += (qs.Length > 0 ? ", " : "") + q.Id + " (" + q.Name + ")";
                return "raven norov h|m [характер|-] [хист|-] [примха|-]\nХарактери: " + cs + "\nХисти: " + ks + "\nПримхи: " + qs;
            }
            bool munin = Raven(a, 1);
            ItemDrop.ItemData item = Find(p, munin);
            if (item == null) return "Такого ворона немає ні на тобі, ні в сумці.";
            if (Trust.Stranger(item, p)) return "Це чужий ворон: його норов міняє лише господар.";
            string ch = a.Length > 2 && a[2] != "-" ? a[2] : null, kn = a.Length > 3 && a[3] != "-" ? a[3] : null, qu = a.Length > 4 && a[4] != "-" ? a[4] : null;
            if (ch != null && Norov.CharById(ch) == null) return "Нема характеру «" + ch + "».";
            if (kn != null && Norov.KnackById(kn) == null) return "Нема хисту «" + kn + "».";
            if (qu != null && Norov.QuirkById(qu) == null) return "Нема примхи «" + qu + "».";
            Norov.Reroll(p, item, munin);
            if (ch != null) item.m_customData[Norov.CharKey] = ch;
            if (kn != null) item.m_customData[Norov.KnackKey] = kn;
            if (qu != null) { item.m_customData[Norov.QuirksKey] = qu; Quirks.Rolled(item, munin); }
            Backup.Save(p, item);
            return Plugin.RavenName(munin) + ": " + Norov.CharOf(item).Name + " · " + Norov.KnackOf(item).Name(munin) + " · " + Norov.QuirkOf(item).Name
                + " (характер розкрито на " + Mathf.RoundToInt(Norov.Shows(item) * 100f) + "%)";
        }

        private static string Perk(Player p, string[] a)
        {
            if (a.Length < 4) return "raven perk hugin|munin <благо> <тягар> — список назв: raven perks";
            bool munin = Raven(a, 1);
            ItemDrop.ItemData item = Find(p, munin);
            if (item == null) return "Такого ворона немає ні на тобі, ні в сумці.";
            string err = Perks.SetExact(item, munin, a[2], a[3]);
            return err ?? Plugin.RavenName(munin) + ": " + Perks.Short(item);
        }

        private static string Info(Player p)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            if (h == null && m == null) return "На тобі немає воронів.";
            string s = "";
            if (h != null) s += "Хугін: " + Perks.Short(h) + "; сила " + Mathf.FloorToInt(Power.Get(h)) + "/" + Mathf.RoundToInt(Trust.Strength(h)) + "\n";
            if (m != null) s += "Мунін: " + Perks.Short(m) + "; сила " + Mathf.FloorToInt(Power.Get(m)) + "/" + Mathf.RoundToInt(Trust.Strength(m)) + "\n";
            return s + RavenSkills.CooldownReport();
        }

        private static string TrustCmd(string[] a)
        {
            int level;
            if (a.Length < 3 || !int.TryParse(a[2], out level)) return "raven trust hugin|munin <рівень 1-" + Trust.MaxLevel + "> [очки]";
            bool munin = Raven(a, 1);
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return Plugin.RavenName(munin) + " зараз не на тобі.";
            float points = 0f;
            if (a.Length >= 4) float.TryParse(a[3], NumberStyles.Float, CultureInfo.InvariantCulture, out points);
            Trust.SetLevel(item, level, points);
            return Plugin.RavenName(munin) + ": довіра рівня " + Trust.Level(item) + ", очки " + Mathf.FloorToInt(Trust.Points(item)) + "/" + Mathf.RoundToInt(Trust.Need(item)) + ", запас сили " + Mathf.RoundToInt(Trust.Strength(item)) + ".";
        }

        // try a wing pose on your own ravens: raven wings <out> <up> [elbow]; with nothing, back to the game's own
        private static string WingsCmd(string[] a)
        {
            float o, u, e = 0f;
            if (a.Length < 3 || !float.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out o) || !float.TryParse(a[2], NumberStyles.Float, CultureInfo.InvariantCulture, out u))
            {
                ShoulderLife.TestWings = false;
                return "raven wings <назовні°> <вгору°> [лікоть°] — показати крила в цій позі; без чисел — вимкнути. Зараз: махи при стрибку й ударі, розправлені у Злеті, відведені назад у Муніна на бігу під Крилами.";
            }
            if (a.Length >= 4) float.TryParse(a[3], NumberStyles.Float, CultureInfo.InvariantCulture, out e);
            ShoulderLife.TestWings = true; ShoulderLife.TestOut = o; ShoulderLife.TestUp = u; ShoulderLife.TestElbow = e;
            return "Крила: назовні " + o + "°, вгору " + u + "°, лікоть " + e + "°. Вимкнути: raven wings";
        }

        // raven stroke: the beat on the shoulder as a wave along the wing (ShoulderLife.Stroke*), tried live
        private static string StrokeCmd(Player p, string[] a)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            string w = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            float v = 0f;
            bool num = a.Length > 2 && float.TryParse(a[2], NumberStyles.Float, c, out v);
            if (w == "test") { foreach (bool m in new[] { false, true }) { ShoulderLife l = Shoulder.LifeOf(p, m); if (l != null) l.Flap(num ? v : 3f); } }
            else if (num)
            {
                switch (w)
                {
                    case "lag": ShoulderLife.StrokeLagElbow = v; break; case "laghand": ShoulderLife.StrokeLagHand = v; break;
                    case "flex": ShoulderLife.StrokeFlexElbow = v; break; case "flexhand": ShoulderLife.StrokeFlexHand = v; break;
                    case "fold": ShoulderLife.StrokeFoldElbow = v; break; case "foldhand": ShoulderLife.StrokeFoldHand = v; break;
                    case "sweep": ShoulderLife.StrokeSweep = v; break; case "twist": ShoulderLife.StrokeTwist = v; break; case "twistsign": ShoulderLife.StrokeTwistSign = v; break;
                    case "beat": ShoulderLife.FlapBeat = v; break; case "hz": ShoulderLife.FlapHz = v; break;
                }
            }
            return "Змах на плечі: lag " + ShoulderLife.StrokeLagElbow.ToString("0.##", c) + " laghand " + ShoulderLife.StrokeLagHand.ToString("0.##", c)
                + " flex " + ShoulderLife.StrokeFlexElbow.ToString("0.##", c) + " flexhand " + ShoulderLife.StrokeFlexHand.ToString("0.##", c)
                + " fold " + ShoulderLife.StrokeFoldElbow.ToString("0.#", c) + " foldhand " + ShoulderLife.StrokeFoldHand.ToString("0.#", c)
                + " sweep " + ShoulderLife.StrokeSweep.ToString("0.#", c) + " twist " + ShoulderLife.StrokeTwist.ToString("0.#", c) + " twistsign " + ShoulderLife.StrokeTwistSign.ToString("0", c)
                + " beat " + ShoulderLife.FlapBeat.ToString("0.#", c) + " hz " + ShoulderLife.FlapHz.ToString("0.##", c)
                + ". raven stroke <назва> <число> — змінити; raven stroke test [с] — помахати.";
        }

        // raven fit: the ravens' seats on what is worn (ShoulderFit)
        private static string FitCmd(Player p, string[] a)
        {
            string w = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            if (w == "off") ShoulderFit.On = false;
            else if (w == "on") ShoulderFit.On = true;
            else if (w == "again") ShoulderFit.Remeasure();
            return "Посадка по броні: " + (ShoulderFit.On ? "увімкнено" : "вимкнено") + ". " + ShoulderFit.Info(p) + ". Межі " + Mathf.RoundToInt(ShoulderFit.MinLift * 100f) + "…+" + Mathf.RoundToInt(ShoulderFit.MaxLift * 100f) + " см.";
        }

        // raven perch: the bent inner leg on the shoulder and how much lower it sits (tried live, not saved)
        private static string PerchCmd(string[] a)
        {
            if (a.Length > 1 && a[1].ToLowerInvariant() == "swap") ShoulderLife.PerchSwap = !ShoulderLife.PerchSwap;
            else if (a.Length > 1)
            {
                float[] v = { ShoulderLife.PerchHip, ShoulderLife.PerchKnee, ShoulderLife.PerchAnkle, Shoulder.PerchDrop * 100f };
                for (int i = 1; i < a.Length && i - 1 < v.Length; i++) float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i - 1]);
                ShoulderLife.PerchHip = v[0]; ShoulderLife.PerchKnee = v[1]; ShoulderLife.PerchAnkle = v[2]; Shoulder.PerchDrop = v[3] / 100f;
            }
            return "Лапа біля шиї: стегно " + ShoulderLife.PerchHip + "°, коліно " + ShoulderLife.PerchKnee + "°, гомілка " + ShoulderLife.PerchAnkle + "°, ворон нижче на " + Mathf.RoundToInt(Shoulder.PerchDrop * 100f) + " см" + (ShoulderLife.PerchSwap ? " (інша лапа)" : "") + ". Вимкнути згин: raven perch 0 0 0";
        }

        // raven sit: what each raven is doing now (the sitting system), its body on the seat
        private static string SitCmd(Player p, string[] a) { return SitSystem.Report(p); }

        // raven legs: how the legs are drawn in, in flight; numbers tried live (not saved)
        private static string LegsCmd(string[] a)
        {
            if (a.Length > 1 && a[1].ToLowerInvariant() == "off") ShoulderLife.LegsOff = true;
            else if (a.Length > 1 && a[1].ToLowerInvariant() == "on") ShoulderLife.LegsOff = false;
            else if (a.Length > 1)
            {
                float[] v = { ShoulderLife.LegHip, ShoulderLife.LegKnee, ShoulderLife.LegAnkle, ShoulderLife.LegToes };
                for (int i = 1; i < a.Length && i - 1 < v.Length; i++) float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i - 1]);
                ShoulderLife.LegHip = v[0]; ShoulderLife.LegKnee = v[1]; ShoulderLife.LegAnkle = v[2]; ShoulderLife.LegToes = v[3];
                ShoulderLife.LegsOff = false;
            }
            return "Лапи в польоті: " + (ShoulderLife.LegsOff ? "як у грі (вимкнено)" : "стегно " + ShoulderLife.LegHip + "°, коліно " + ShoulderLife.LegKnee + "°, гомілка " + ShoulderLife.LegAnkle + "°, пальці " + ShoulderLife.LegToes + "°")
                + ". Подивитися: потанцюй (ворони зависнуть над плечем) або купол.";
        }

        // raven pose: the gifts' wing poses; with a name, shown on your Munin for 3 s; with numbers, changed first
        private static string PoseCmd(Player p, string[] a)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (a.Length < 2)
            {
                sb.Append("Пози крил (назовні/вгору/лікоть, внутрішнє крило +назовні/вгору/лікоть, тремтіння):");
                foreach (KeyValuePair<string, ShoulderLife.GiftPose> kv in ShoulderLife.Poses) sb.Append("\n  ").Append(kv.Key).Append(": ").Append(PoseText(kv.Value));
                return sb.ToString();
            }
            string name = a[1].ToLowerInvariant();
            ShoulderLife.GiftPose g;
            if (!ShoulderLife.Poses.TryGetValue(name, out g)) return "Нема такої пози. Є: " + string.Join(", ", new List<string>(ShoulderLife.Poses.Keys).ToArray());
            float[] v = { g.Out, g.Up, g.Elbow, g.InOut, g.InUp, g.InElbow, g.Shake };
            for (int i = 2; i < a.Length && i - 2 < v.Length; i++) float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i - 2]);
            g.Out = v[0]; g.Up = v[1]; g.Elbow = v[2]; g.InOut = v[3]; g.InUp = v[4]; g.InElbow = v[5]; g.Shake = v[6];
            ShoulderLife.Moment(p, true, name, 3f);
            return name + ": " + PoseText(g) + (Shoulder.LifeOf(p, true) == null ? " (Мунін не на плечі — нема на кому показати)" : "");
        }

        private static string PoseText(ShoulderLife.GiftPose g)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return g.Out.ToString("0.#", c) + " " + g.Up.ToString("0.#", c) + " " + g.Elbow.ToString("0.#", c)
                + " | " + g.InOut.ToString("0.#", c) + " " + g.InUp.ToString("0.#", c) + " " + g.InElbow.ToString("0.#", c)
                + " | " + g.Shake.ToString("0.#", c);
        }

        // raven sat 25 munin: set satiety; without a number: show it, the zone and the favourite food
        private static string SatCmd(string[] a)
        {
            float v = -1f;
            bool hasNum = a.Length > 1 && float.TryParse(a[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);
            bool munin = Raven(a, hasNum ? 2 : 1);
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return Plugin.RavenName(munin) + " не на плечі.";
            if (v >= 0f) Satiety.Set(item, Mathf.Clamp(v, 0f, 100f));
            Trust.Food fav = Satiety.FavFood(item, munin);
            return Plugin.RavenName(munin) + ": ситість " + Mathf.RoundToInt(Satiety.Value(item)) + " (" + Satiety.ZoneWord(item) + ")"
                + ", улюблена: " + (fav != null ? fav.Name + (Satiety.FavKnown(item) ? "" : " (ще прихована)") : "ще не вибрана");
        }

        // raven starve: satiety 0 at once
        private static string StarveCmd(string[] a)
        {
            bool munin = Raven(a, 1);
            if (Trust.Worn(munin) == null) return Plugin.RavenName(munin) + " не на плечі.";
            Trust.StarveNow(munin);
            return Plugin.RavenName(munin) + " голодує (ситість 0): понурий, удар −" + Mathf.RoundToInt(Trust.StarveStrikePct) + "%, дари ×" + Satiety.Num(Satiety.StarveCostMul) + ", клюне за 15 с.";
        }

        // raven gift: Odin's gift as after a ritual, without the trip
        private static string GiftCmd(Player p, string[] a)
        {
            bool munin = Raven(a, 1);
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return Plugin.RavenName(munin) + " не на плечі.";
            int level = Trust.Level(item);
            if (level < Outfits.WardrobeLevel || level >= Outfits.TopLevel) return "Дарунки від Одіна бувають на рівнях " + Outfits.WardrobeLevel + "–" + (Outfits.TopLevel - 1) + ", а в " + Plugin.RavenName(munin) + " " + level + "-й.";
            Outfits.OdinGift(munin, item);
            string gift = Outfits.TakeNewGift(item);
            if (gift == null) return Plugin.RavenName(munin) + " уже має всі дарунки.";
            ItemDrop.ItemData hugin, mun;
            RavenItem.Worn(p, out hugin, out mun);
            Outfits.Publish(p, hugin, mun);
            Outfits.Dress(Shoulder.LocalModel(munin), Outfits.Code(item));
            return "Одін передав " + Plugin.RavenName(munin) + ": " + gift + ".";
        }

        // raven eyes: what the glow is made of; raven eyes <percent>: the same glow for every raven; raven eyes off: by trust again
        private static string EyesCmd(string[] a)
        {
            float pct;
            if (a.Length >= 2 && float.TryParse(a[1].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out pct))
            {
                EyeGlow.Override = Mathf.Clamp(pct, 0f, 1000f);
                EyeGlow.Version++;
                return "Очі воронів: " + EyeGlow.Override.ToString("0.#", CultureInfo.InvariantCulture) + "% від рідного світіння. Назад за рівнями: raven eyes off";
            }
            if (a.Length >= 2)
            {
                EyeGlow.Override = -1f;
                EyeGlow.Version++;
                return "Очі воронів знову світяться за рівнем довіри: " + EyeGlow.Levels();
            }
            return EyeGlow.Describe();
        }

        private static string TiltCmd(string[] a)
        {
            float munin, hugin = Shoulder.HuginTilt;
            if (a.Length < 2 || !float.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out munin))
                return "raven tilt <мунін°> [хугін°]   зараз: Мунін " + Shoulder.MuninTilt + "°, Хугін " + Shoulder.HuginTilt + "°";
            if (a.Length >= 3) float.TryParse(a[2], NumberStyles.Float, CultureInfo.InvariantCulture, out hugin);
            Plugin.Instance.SaveTilt(Mathf.Clamp(hugin, -60f, 60f), Mathf.Clamp(munin, -60f, 60f));
            Shoulder.Redraw();
            return "Нахил: Мунін " + Shoulder.MuninTilt + "°, Хугін " + Shoulder.HuginTilt + "° (збережено в конфіг)";
        }

        private static string ShoulderCmd(string[] a)
        {
            float scale;
            if (a.Length < 2 || !float.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
                return "raven shoulder <розмір> [x y z [rx ry rz]]   зараз: " + Shoulder.Describe();
            float[] f = new float[6];
            Vector3 off = Shoulder.Offset, rot = Shoulder.Rotation;
            for (int i = 0; i < 6 && i + 2 < a.Length; i++) float.TryParse(a[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i]);
            if (a.Length >= 5) off = new Vector3(f[0], f[1], f[2]);
            if (a.Length >= 8) rot = new Vector3(f[3], f[4], f[5]);
            Plugin.Instance.SaveShoulder(Mathf.Clamp(scale, 0.05f, 2f), off, rot);
            Shoulder.Redraw();
            return "Ворони на плечах тепер: " + Shoulder.Describe() + " (збережено в конфіг)";
        }
#endif
    }

    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    internal static class CommandsTerminalPatch
    {
        private static void Postfix() { Commands.AddToTerminal(); }
    }
}
