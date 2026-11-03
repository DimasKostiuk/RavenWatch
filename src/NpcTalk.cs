#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Talking to a person (npc-plan.md, stage 3): E opens its talk — a window in the book's card style (RavenBookTalk)
    // with what it says and your answers. The talks are plain text (built in below; the author's own files in
    // config/RavenWatch/talk/*.txt, read while tuning, replace them by id). A person's template says which is its own
    // («talk: elder»; none: «common»).
    //
    //   [talk elder]                      a talk
    //   @start                            a place in it (where it begins: «start»)
    //   А, це ти, {name}.                 what it says there (lines join; {name} you, {npc} it, {greet} one of its
    //                                     greet lines, {doing} what it does now)
    //   > Хто ти? => who                  an answer: your words => where it leads («end» closes the talk)
    //   > Живи з нами. => hire ?!hired    ?… a condition (the story's own — flag, var, item, quest, boss, night… —
    //   > Ось монети. => ok ?item Coins 20 !take Coins 20 !hire       and a person's: hired, mine, resident, job <id>);
    //                                     !… what happens when it is picked (Acts below). An answer whose conditions do
    //                                     not hold is not shown.
    //   [talk torvald : common]           built on another talk: all its places, and these over them —
    //   @start +                            «+»: added to that place (its words, if given, instead; its answers first,
    //                                       then the other's); without «+»: this place instead of the other's
    //
    // It runs on the game of the one who talks (its window, your items and story); what changes the person (hired, its
    // orders) is sent to the game that runs it.
    internal static class NpcTalk
    {
        internal sealed class Answer { public string Text = "", To = "end"; public List<Cond> When = new List<Cond>(); public List<string> Acts = new List<string>(); }
        internal sealed class Node { public string Id = "", Says = ""; public bool Adds; public readonly List<Answer> Answers = new List<Answer>(); }
        internal sealed class Talk { public string Id = "", Base = ""; public readonly Dictionary<string, Node> Nodes = new Dictionary<string, Node>(); }

        internal static readonly int KHired = "RavenWatch_npcHired".GetStableHashCode(); // long: who hired it (a player id; 0 nobody)

        // ---------------------------------------------------------------- hired, let go (acts on the game that runs it: NpcModules)
        internal static void Hire(NpcActor a, string[] w)
        {
            long id;
            if (w.Length < 2 || a.Zdo == null || !long.TryParse(w[1], out id)) return;
            a.Zdo.Set(KHired, id);
            NpcMemory.Remember(a, "hired", id, ""); // (who took it in)
            Npcs.Log(a.Id + ": hired by " + w[1]);
        }

        internal static void Unhire(NpcActor a)
        {
            if (a.Zdo == null) return;
            if (NpcLevers.IsProtected(a)) { Npcs.Log(a.Id + ": the story holds it — not sent off"); return; }
            if (a.Gear != null) a.Gear.DropAll(); // (its gear at its feet)
            a.Zdo.Set(KHired, 0L);
            NpcLocker.Set(a, NpcRequests.KRequest, "");
            NpcMemory.TellHome(a, "neighbour_left", 0L, a.Name); // (those who stay remember it — before it leaves its heart)
            NpcResident.Unsettle(a);
            if (a.Mind != null) { a.Mind.ForgetWay(); a.Mind.Do("free"); } // (no way kept to take up again)
            Npcs.Log(a.Id + ": let go");
        }

        // ---------------------------------------------------------------- the talks
        private static readonly string[] Builtin =
        {
            "[talk common]\n" +
            "@start\n{greet}\n" +
            // a person in need (NpcRescue): what saves it
            "> Тримайся, я їх прожену! => wait ?npcflag foes ?!npcflag lost\n" +
            "> Розв'яжу тебе. => saved ?npcflag captive ?!npcflag foes !npcunflag captive !npcflag rescued !remember rescued\n" +
            "> Ось, поїж. => saved ?npcflag hungry ?hasfood !givefood !npcunflag hungry !npcflag rescued !remember rescued\n" +
            "> Випий меду, полегшає. => saved ?npcflag wounded ?!npcflag foes ?hasmead !givemead !npcunflag wounded !npcflag rescued !remember rescued\n" +
            "> Я знайш{ов|ла} твою річ. => saved ?npcflag lost ?hasitemof !givebackitem !npcunflag lost !npcflag rescued !remember rescued\n" +
            "> Ось ти де! Ходімо звідси. => saved ?npcflag astray !npcunflag astray !npcflag rescued !remember rescued\n" +
            // ...or what to do while you go for what saves it: wait, hide where you show, walk with you
            "> Я знайду твою річ. => guard ?npcflag lost ?!hasitemof\n" +
            "> Зачекай, я принесу тобі поїсти. => guard ?npcflag hungry ?!hasfood\n" +
            "> Зачекай, я пошукаю лікувального меду. => guard ?npcflag wounded ?!npcflag foes ?!hasmead\n" +
            "> Що сталося? => what ?rescue\n" +
            "> Хто ти? => who\n" +
            "> Живи на моїй базі й працюй зі мною. => hire ?!hired ?!npcflag inneed\n" +
            "> Тобі щось потрібно? => need ?hasneed\n" +
            "> Ось те, що ти просив|просила. => thanks ?hasrequest !giverequest\n" +
            "> Як справи? => how ?hired\n" +
            "> Поговорімо. => talk ?hired\n" +
            "> Ти вільний|вільна, іди своєю дорогою. => fire ?mine ?!npcflag protected\n" +
            "> Іди своєю дорогою, у мене нема для тебе місця. => end ?npcflag rescued ?!hired !goaway\n" +
            "> Бувай. => end\n" +
            "@who\nМене звати {npc}. {story_short}\n> Розкажи більше. => more ?hasstory\n> Зрозум{ів|іла}. => start\n" +
            "@more\n{story_more}\n> Дякую, що розповів|розповіла. => start\n" +
            "@hire\nЖити під дахом і бути при ділі? Чом би й ні. Куди мені йти?\n" +
            "> Ходімо зі мною. => end !hire !follow\n> Іди сам|сама до оселі. => homes\n> Іншим разом. => end\n" +
            "@how\n{doing}\n> Добре. => start\n" +
            // small talk with one of yours: its words made each time (NpcPhrases: talk_*, about_*)
            "@talk\n{@talk_invite}\n> Як тобі тут? => home ?resident\n> Хто ще в оселі живе? => folk ?resident\n> Розкажи щось. => tale\n" +
            "> Що думаєш про воронів? => ravens\n> Чого б тобі хотілося? => wish\n> Що думаєш про мене? => me\n> Годі балачок. => start\n" +
            "@home\n{@about_home}\n> Поговорімо ще. => talk\n> Добре. => start\n" +
            "@folk\n{@about_folk}\n> Поговорімо ще. => talk\n> Добре. => start\n" +
            "@tale\n{@tale}\n> Ще щось розкажи. => tale\n> Поговорімо про інше. => talk\n> Дякую. => start\n" +
            "@ravens\n{@about_ravens}\n> Поговорімо ще. => talk\n> Добре. => start\n" +
            "@wish\n{@about_wish}\n> Подумаю, що можна зробити. => talk\n> Добре. => start\n" +
            "@me\n{@you_remember} {@about_you}\n> Поговорімо ще. => talk\n> Добре. => start\n" +
            "@need\n{request}\n> Принесу. => end\n> Подумаю. => start\n" +
            "@thanks\n{reward}\n> Нема за що. => end\n" +
            "@wait\nСпершу прожени їх! Я… я тут, нікуди не дінусь.\n> Добре. => end\n" +
            "@saved\n{thanks}\n> Живи на моїй базі й працюй зі мною. => hire ?!hired\n> Хто ти? => who\n> Іди своєю дорогою, у мене нема для тебе місця. => end !goaway\n> Бувай. => end\n" +
            "@guard\nА мені що робити, поки тебе нема? Сам|Сама я тут довго не протримаюсь.\n> Чекай тут, я швидко. => end !rescuewait\n> Сховайся — я покажу, де. => end !rescuehide\n> Ходімо зі мною, я тебе охоронятиму. => end !rescuefollow\n" +
            "@what\n{plea_more}\n> Зрозум{ів|іла}. => start\n" +
            "@fire\nЩо ж… Піду своєю дорогою. Ти {певен|певна}?\n> Так. => end !unhire\n> Ні, лишайся. => start\n",
        };

        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "talk"); } }
        private static Dictionary<string, Talk> _all;

        internal static Talk Find(string id)
        {
            if (_all == null) Load();
            Talk t;
            return _all.TryGetValue(id ?? "", out t) ? t : (_all.TryGetValue("common", out t) ? t : null);
        }

        internal static void Reload() { _all = null; }

        private static void Load()
        {
            Register(); // (its conditions known before its words are read)
            Dictionary<string, Talk> raw = new Dictionary<string, Talk>();
            foreach (string s in Builtin) Parse(raw, s, "built-in");
            if (RavenLooks.FromFiles && Directory.Exists(Dir))
                foreach (string f in Directory.GetFiles(Dir, "*.txt"))
                {
                    try { Parse(raw, File.ReadAllText(f, Encoding.UTF8), Path.GetFileName(f)); }
                    catch (Exception e) { Npcs.Log("talk file " + f + ": " + e.Message); }
                }
            Dictionary<string, Talk> all = new Dictionary<string, Talk>();
            foreach (string id in raw.Keys) Built(id, raw, all, new HashSet<string>());
            _all = all;
        }

        // a talk with what it is built on put under it («[talk torvald : common]»; the base itself first, a loop cut)
        private static Talk Built(string id, Dictionary<string, Talk> raw, Dictionary<string, Talk> all, HashSet<string> on)
        {
            Talk done, own;
            if (all.TryGetValue(id, out done)) return done;
            if (!raw.TryGetValue(id, out own)) return null;
            if (own.Base.Length == 0) return all[id] = own;
            if (!on.Add(id)) { Npcs.Log("talk «" + id + "»: built on itself — its base left out"); return own; }
            Talk under = Built(own.Base, raw, all, on);
            if (under == null) { Npcs.Log("talk «" + id + "»: no talk «" + own.Base + "» to build on"); return all[id] = own; }
            Talk t = new Talk { Id = id };
            foreach (KeyValuePair<string, Node> e in under.Nodes) t.Nodes[e.Key] = e.Value;
            foreach (Node n in own.Nodes.Values)
            {
                Node old;
                if (!n.Adds || !t.Nodes.TryGetValue(n.Id, out old)) { t.Nodes[n.Id] = n; continue; }
                Node both = new Node { Id = n.Id, Says = n.Says.Length > 0 ? n.Says : old.Says };
                both.Answers.AddRange(n.Answers); both.Answers.AddRange(old.Answers);
                t.Nodes[n.Id] = both;
            }
            return all[id] = t;
        }

        private static void Parse(Dictionary<string, Talk> into, string text, string from)
        {
            Talk t = null; Node n = null;
            List<string> errors = new List<string>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[talk ") && line.EndsWith("]"))
                {
                    string head = line.Substring(6, line.Length - 7);
                    int colon = head.IndexOf(':');
                    t = new Talk { Id = (colon < 0 ? head : head.Substring(0, colon)).Trim(), Base = colon < 0 ? "" : head.Substring(colon + 1).Trim() };
                    into[t.Id] = t; n = null; continue;
                }
                if (t == null) continue;
                if (line.StartsWith("@"))
                {
                    string id = line.Substring(1).Trim();
                    bool adds = id.EndsWith("+");
                    n = new Node { Id = adds ? id.Substring(0, id.Length - 1).Trim() : id, Adds = adds };
                    t.Nodes[n.Id] = n; continue;
                }
                if (n == null) continue;
                if (line.StartsWith(">")) { Answer a = ParseAnswer(line.Substring(1), errors); if (a != null) n.Answers.Add(a); continue; }
                n.Says = n.Says.Length == 0 ? line : n.Says + "\n" + line;
            }
            foreach (string e in errors) Npcs.Log("talk (" + from + "): " + e);
        }

        // "Ось монети. => ok ?item Coins 20 !take Coins 20 !hire"
        private static Answer ParseAnswer(string s, List<string> errors)
        {
            Answer a = new Answer();
            int arrow = s.IndexOf("=>");
            if (arrow < 0) { a.Text = s.Trim(); return a; }
            a.Text = s.Substring(0, arrow).Trim();
            string[] w = s.Substring(arrow + 2).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            int from = 0;
            if (w.Length > 0 && w[0][0] != '?' && w[0][0] != '!') { a.To = w[0]; from = 1; }
            StringBuilder cur = null; bool isAct = false;
            for (int i = from; i < w.Length; i++)
            {
                string t = w[i];
                if (t[0] == '?' || t[0] == '!') // (a new condition «?…» — «?!x» is «not x» — or a deed «!…»)
                {
                    Flush(a, cur, isAct, errors);
                    isAct = t[0] == '!';
                    cur = new StringBuilder(t.Substring(1));
                }
                else if (cur != null) cur.Append(' ').Append(t);
            }
            Flush(a, cur, isAct, errors);
            return a;
        }

        private static void Flush(Answer a, StringBuilder cur, bool isAct, List<string> errors)
        {
            if (cur == null) return;
            string s = cur.ToString().Trim();
            if (s.Length == 0) return;
            if (isAct) a.Acts.Add(s);
            else a.When.AddRange(StoryCond.Parse(s, errors));
        }

        // ---------------------------------------------------------------- a person's own conditions (into the story's registry)
        internal static NpcActor With; // the person being talked to (for its conditions)
        internal static NpcActor Speaker; // a person whose made words are being picked (NpcPhrases: a line's conditions are about it)
        internal static NpcActor Him { get { return Speaker ?? With; } } // «he» of a person's conditions: the one speaking, else the one talked to
        private static bool _registered;

        internal static void Register()
        {
            if (_registered) return;
            _registered = true;
            NpcLevers.Register();
            NpcMemory.Register();
            NpcAttitude.Register();
            StoryCond.Register("hired", new StoryCond.Kind { Usage = "hired   (його хтось найняв)", Test = (p, a) => Him != null && Hirer(Him) != 0L });
            StoryCond.Register("mine", new StoryCond.Kind { Usage = "mine   (його найняв ти)", Test = (p, a) => Him != null && p != null && Hirer(Him) == p.GetPlayerID() });
            StoryCond.Register("resident", new StoryCond.Kind { Usage = "resident   (живе на базі)", Test = (p, a) => Him != null && NpcResident.Is(Him) });
            // a person in need (NpcRescue): what you have for it
            StoryCond.Register("hasfood", new StoryCond.Kind { Usage = "hasfood   (у тебе є готова їжа)", Test = (p, a) => p != null && NpcMeals.Plainest(p.GetInventory()) != null });
            StoryCond.Register("hasmead", new StoryCond.Kind { Usage = "hasmead   (у тебе є лікувальний мед)", Test = (p, a) => Mead(p) != null });
            StoryCond.Register("hasitemof", new StoryCond.Kind { Usage = "hasitemof   (у тебе його загублена річ)", Test = (p, a) => NpcRescueSite.LostThing(p, Him) != null });
            StoryCond.Register("rescue", new StoryCond.Kind { Usage = "rescue   (у халепі чи щойно врятований, ще не найнятий)", Test = (p, a) => NpcRescueTalk.State(Him) != null });
            StoryCond.Register("hasstory", new StoryCond.Kind { Usage = "hasstory   (має що розповісти про себе)", Test = (p, a) => NpcBackstory.Has(Him) });
            StoryCond.Register("hasneed", new StoryCond.Kind { Usage = "hasneed   (він про щось просить)", Test = (p, a) => NpcRequests.Of(Him) != null });
            StoryCond.Register("hasrequest", new StoryCond.Kind { Usage = "hasrequest   (у тебе є те, що він просить)", Test = (p, a) => NpcRequests.CanGive(p, Him) });
            StoryCond.Register("job", new StoryCond.Kind { Usage = "job <id>   (його робота)", Min = 1, Test = (p, a) => Him != null && Him.Worker != null && Him.Worker.JobId == a[1] });
        }

        internal static long Hirer(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? z.GetLong(KHired, 0L) : 0L; }
        internal static bool Hired(NpcActor a) { return Hirer(a) != 0L; }

        // ---------------------------------------------------------------- talking
        private static Talk _talk;

        // E on a person: its talk opens here (true), or it has none (false: it only greets)
        internal static bool Open(NpcActor a, Player p)
        {
            if (a == null || p == null || a.Def == null) return false;
            Register();
            Talk t = Find(a.Def.Talk);
            if (t == null || !t.Nodes.ContainsKey("start")) return false;
            With = a; _talk = t;
            Show("start", p);
            return true;
        }

        private static void Show(string node, Player p)
        {
            Node n;
            if (node == Homes && _talk != null && With != null) { ShowHomes(p); return; }
            if (node == "end" || _talk == null || With == null || !_talk.Nodes.TryGetValue(node, out n)) { Close(); return; }
            List<string> shown = new List<string>();
            List<Answer> ans = new List<Answer>();
            foreach (Answer a in n.Answers) if (StoryCond.All(p, a.When)) { ans.Add(a); shown.Add(Fill(Lang.T(a.Text), p)); }
            if (ans.Count == 0) { ans.Add(new Answer { Text = "…", To = "end" }); shown.Add("…"); }
            List<Answer> keep = ans;
            RavenBook.ShowTalk(With.Name, Fill(Lang.T(n.Says), p), shown, i => Pick(keep, i), Close);
        }

        // «homes»: a place of its own in every talk — the named «Серце оселі» of the world (the server's list, NpcHeart),
        // nearest first; picked, it is hired and goes there by itself (NpcTravel)
        internal const string Homes = "homes";

        private static void ShowHomes(Player p)
        {
            NpcActor who = With;
            RavenBook.ShowTalk(who.Name, Lang.T("Зараз пригадаю, які оселі знаю…"), new List<string> { "…" }, i => { }, Close);
            NpcHeart.Ask(list =>
            {
                if (With != who || who == null || Player.m_localPlayer == null) return; // (the talk was closed or moved on meanwhile)
                Vector3 me = Player.m_localPlayer.transform.position;
                list.Sort((x, y) => Utils.DistanceXZ(x.Pos, me).CompareTo(Utils.DistanceXZ(y.Pos, me)));
                List<Answer> ans = new List<Answer>();
                List<string> shown = new List<string>();
                foreach (NpcHeart.Entry e in list)
                {
                    if (ans.Count >= 8) break; // (the keys 1–9: the last one for «with me»)
                    float km = Utils.DistanceXZ(e.Pos, me) / 1000f;
                    Answer a = new Answer { To = "end" };
                    a.Acts.Add("hire"); a.Acts.Add("travel " + Npcs.V(e.Pos) + " " + NpcTravel.ToHeart + " " + NpcKit.Ref(e.Id));
                    ans.Add(a);
                    shown.Add(Lang.F("До «{0}»", e.Name) + " <size=80%>(" + (km >= 1f ? Lang.F("{0} км", km.ToString("0.#")) : Lang.F("{0} м", Mathf.RoundToInt(km * 1000f))) + ")</size>");
                }
                Answer with = new Answer { To = "end" }; with.Acts.Add("hire"); with.Acts.Add("follow");
                ans.Add(with); shown.Add(Lang.T("Ні, ходімо зі мною."));
                string says = list.Count > 0 ? who.Form(Lang.T("Куди саме? Скажи — дійду сам|сама.")) : Lang.T("Я не знаю жодної оселі. Постав «Серце оселі» й дай йому назву — тоді й скажеш, куди йти.");
                List<Answer> keep = ans;
                RavenBook.ShowTalk(who.Name, says, shown, i => Pick(keep, i), Close);
            });
        }

        private static void Pick(List<Answer> ans, int i)
        {
            Player p = Player.m_localPlayer;
            if (p == null || With == null || i < 0 || i >= ans.Count) { Close(); return; }
            Answer a = ans[i];
            foreach (string act in a.Acts) Do(act, p);
            Show(a.To, p);
        }

        private static float _nextHold;

        internal static void Close() { With = null; _talk = null; RavenBook.HideTalk(); }

        // walked off, it gone: the talk ends
        internal static void Tick()
        {
            if (With == null) return;
            Player p = Player.m_localPlayer;
            if (p == null || Vector3.Distance(p.transform.position, With.transform.position) > 8f) { Close(); return; }
            if (Time.time >= _nextHold) { _nextHold = Time.time + 2f; With.Act("hold " + NpcKit.Ref(p.GetZDOID())); } // (it stands while you talk)
        }

        private static string Fill(string s, Player p)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = NpcPhrases.Inline(With, s, p); // («{@theme}»: a made line where the author asked for one)
            NpcDef d = With.Def;
            if (s.Contains("{greet}")) { string plea = NpcRescueTalk.Greet(With); if (plea != null) s = s.Replace("{greet}", plea); } // (one in need: its own trouble first — NpcRescueTalk)
            if (s.Contains("{thanks}")) s = s.Replace("{thanks}", NpcRescueTalk.Thanks()); // (just saved by an answer: its flag is still on its way to the game that runs it)
            if (s.Contains("{plea_more}")) s = s.Replace("{plea_more}", NpcRescueTalk.More(With) ?? Lang.T("Та вже нічого."));
            if (s.Contains("{greet}")) s = s.Replace("{greet}", d.Greet.Count > 0 ? NpcPhrases.Inline(With, Lang.T(d.Greet[UnityEngine.Random.Range(0, d.Greet.Count)]), p) : Lang.T("Чого тобі?")); // («greet: {@see_player}»: a made one)
            if (s.Contains("{doing}")) s = s.Replace("{doing}", Doing(With));
            if (s.Contains("{story_short}")) { string st = NpcBackstory.Text(With, false); s = s.Replace("{story_short}", st.Length > 0 ? st : Lang.T("Більше й казати нічого.")); }
            if (s.Contains("{story_more}")) { string st = NpcBackstory.Rest(With); s = s.Replace("{story_more}", st.Length > 0 ? st : Lang.T("Більше й казати нічого.")); }
            if (s.Contains("{story}")) s =s.Replace("{story}", NpcBackstory.Text(With, true));
            if (s.Contains("{request}")) s = s.Replace("{request}", NpcRequests.Text(With));
            if (s.Contains("{reward}")) s = s.Replace("{reward}", NpcRequests.LastPaid);
            s = s.Replace("{npc}", With.Name).Replace("{name}", p.GetPlayerName()); // (a settler's own name, not its template's)
            return With.Form(Grammar.Fix(s, p)); // (your words in your form, then its own: «пропав|пропала»)
        }

        // what it says it does now: what weighs on it first, then how it is, then its work or its day (made words — NpcPhrases
        // how_*; none fit: the plain ones below)
        private static string Doing(NpcActor a)
        {
            NpcWorker w = a.Worker;
            Player p = Player.m_localPlayer;
            List<string> themes = new List<string>();
            if (NpcResident.Is(a))
            {
                float mood = NpcToil.Mood(a);
                if (mood < 30f) themes.Add("how_low");
                else if (NpcHunger.IsHungry(a)) themes.Add("how_hungry");
                else if (NpcToil.Tired(a) >= 70f) themes.Add("how_tired");
                else if (mood > 75f) themes.Add("how_good");
            }
            themes.Add(w != null && w.Works ? "how_work" : NpcResident.Is(a) ? "how_idle" : "how_free");
            foreach (string t in themes)
            {
                string s = NpcPhrases.Compose(a, t, p);
                if (string.IsNullOrEmpty(s)) continue;
                string low = NpcVoice.LowTheme(a);
                string lvl = NpcAttitude.Level(a, p);
                if (t == "how_low" && low != "mood_low" && lvl != "hostile" && lvl != "cold") { string why = NpcPhrases.Compose(a, low, p); if (!string.IsNullOrEmpty(why)) s += " " + why; } // (and what it is)
                return s;
            }
            if (w != null && w.Works) return Lang.F("Я — {0}. Роблю своє.", Jobs.Word(w.JobId));
            if (NpcResident.Is(a)) return Lang.T("Живу, ходжу, дивлюсь. Дай мені роботу — не відмовлюсь.");
            return Lang.T("Та нічого. Йду, куди веде дорога.");
        }

        // a dry walk of its talk (raven npc talk <who>, for the author): every place from «start», its words as you would see
        // them, each answer shown or hidden by its conditions, where it leads (a place that is not there: told); no window,
        // no deed done. Places never reached from «start» are listed at the end.
        internal static string Walk(NpcActor a, Player p)
        {
            if (a == null || p == null || a.Def == null) return "Ні.";
            Register();
            Talk t = Find(a.Def.Talk);
            if (t == null) return a.Id + ": no talk «" + a.Def.Talk + "»";
            NpcActor was = With; With = a;
            StringBuilder sb = new StringBuilder("[talk walk] " + a.Name + " (" + a.Id + ", talk «" + t.Id + "»)\n");
            int bad = 0;
            try
            {
                HashSet<string> seen = new HashSet<string>();
                Queue<string> next = new Queue<string>(); next.Enqueue("start"); seen.Add("start");
                while (next.Count > 0)
                {
                    string id = next.Dequeue();
                    Node n;
                    if (!t.Nodes.TryGetValue(id, out n)) { sb.Append("@").Append(id).Append(": NO SUCH PLACE\n"); bad++; continue; }
                    string says;
                    try { says = Fill(Lang.T(n.Says), p); } catch (Exception e) { says = "FILL FAILED: " + e.Message; bad++; } // quiet: written into the walk, which goes to the log
                    sb.Append("@").Append(id).Append(": ").Append(says.Replace('\n', ' ')).Append('\n');
                    if (says.Contains("{")) { sb.Append("   ! a {…} left in its words\n"); bad++; }
                    foreach (Answer an in n.Answers)
                    {
                        bool on = StoryCond.All(p, an.When);
                        bool there = an.To == "end" || an.To == Homes || t.Nodes.ContainsKey(an.To);
                        if (!there) bad++;
                        string text;
                        try { text = Fill(Lang.T(an.Text), p); } catch (Exception e) { text = "FILL FAILED: " + e.Message; bad++; } // quiet: written into the walk, which goes to the log
                        sb.Append("   ").Append(on ? "> " : "x ").Append(text).Append(" => ").Append(an.To).Append(there ? "" : " (NO SUCH PLACE)");
                        if (an.Acts.Count > 0) sb.Append("  [").Append(string.Join(", ", an.Acts.ToArray())).Append(']');
                        sb.Append('\n');
                        if (there && an.To != "end" && an.To != Homes && seen.Add(an.To)) next.Enqueue(an.To);
                    }
                }
                List<string> lone = new List<string>();
                foreach (string id in t.Nodes.Keys) if (!seen.Contains(id)) lone.Add(id);
                if (lone.Count > 0) sb.Append("never reached from start: ").Append(string.Join(", ", lone.ToArray())).Append('\n');
            }
            finally { With = was; }
            Npcs.Log(sb.ToString());
            return a.Name + ": " + t.Nodes.Count + " places, problems " + bad + " (the whole walk: the log, «[talk walk]»)";
        }

        // ---------------------------------------------------------------- what an answer does
        //   hire · unhire · follow · free · say <words> · take <Prefab> <N> · give <Prefab> <N> · flag <name> · unflag <name>
        //   var <name> <+N> · quest <id> <state> · raise <event> · travel x y z heart|hall [user:id]
        //   the story's levers on the person (NpcLevers): npcflag · npcunflag · npcvar · trait · become · protect · unprotect · nojob · okjob
        //   its memory and feeling of you: remember <kind> [what] · forget <kind> · attitude <±N> · news <kind> [what] (it and its heart's people)
        //   a person in need (NpcRescue): giverequest · givefood · givemead · givebackitem · goaway · rescuewait · rescuehide · rescuefollow
        private static void Do(string act, Player p)
        {
            string[] w = act.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return;
            NpcActor a = With;
            try
            {
                switch (w[0].ToLowerInvariant())
                {
                    case "hire": a.Act("hire " + p.GetPlayerID()); if (a.Zdo != null) NpcRescue.Forget(a.Zdo.m_uid); break;
                    case "giverequest": NpcRequests.Give(p, a); break;
                    case "givefood": { ItemDrop.ItemData f = NpcMeals.Plainest(p.GetInventory()); if (f != null) p.GetInventory().RemoveItem(f, 1); break; }
                    case "givemead": { ItemDrop.ItemData m = Mead(p); if (m != null) p.GetInventory().RemoveItem(m, 1); break; }
                    case "givebackitem": { ItemDrop.ItemData it = NpcRescueSite.LostThing(p, a); if (it != null) p.GetInventory().RemoveItem(it); break; }
                    case "unhire": a.Act("unhire"); break;
                    case "goaway": a.Act("goaway"); break;
                    case "rescuewait": a.Act("rescuemode"); break; // (NpcRescue: waits where it is)
                    case "rescuefollow": a.Act("rescuemode follow " + NpcKit.Ref(p.GetZDOID())); break;
                    case "rescuehide": NpcMenu.PickPlace("Сховок", "на місце, де сховатись", at => a == null ? null : Utils.DistanceXZ(at, a.transform.position) > HideFar ? Lang.T("Задалеко — покажи ближче.") : SendHide(a, at)); break; // (one in need: where it hides — NpcRescue)
                    case "follow": a.Order("follow " + NpcKit.Ref(p.GetZDOID()) + " 3"); break;
                    case "free": a.Order("free"); break;
                    case "npcflag": case "npcunflag": case "npcvar": case "trait": case "knack": case "become": case "protect": case "unprotect": case "nojob": case "okjob":
                        a.Act("lever " + act); break; // (the story's levers: NpcLevers, where it is owned)
                    case "travel": if (w.Length >= 5) { a.Order(act); a.Say(a.Form(Lang.T("Добре. Дійду сам|сама."))); } break; // travel x y z heart|hall [user:id] (NpcTravel)
                    case "say": a.Say(string.Join(" ", w, 1, w.Length - 1)); break;
                    case "take": if (w.Length >= 3) Take(p, w[1], Errands.I(w[2], 1)); break;
                    case "give": if (w.Length >= 3) Give(p, w[1], Errands.I(w[2], 1)); break;
                    case "flag": if (w.Length >= 2) Story.SetFlag(p, w[1], true); break;
                    case "unflag": if (w.Length >= 2) Story.SetFlag(p, w[1], false); break;
                    case "var": if (w.Length >= 3) Story.AddVar(p, w[1], Errands.I(w[2], 0)); break;
                    case "quest": if (w.Length >= 3) Story.SetQuest(p, w[1], w[2]); break;
                    case "raise": if (w.Length >= 2) StoryBus.Raise(w[1], a.Id, 0, a.transform.position); break;
                    case "remember": if (w.Length >= 2) NpcMemory.Tell(a, w[1], p.GetPlayerID(), w.Length > 2 ? string.Join(" ", w, 2, w.Length - 2) : ""); break; // remember <kind> [what] — about you (NpcMemory)
                    case "forget": if (w.Length >= 2) a.Act("forget " + w[1] + " " + p.GetPlayerID()); break; // (what it remembers of you — not of others)
                    case "news": if (w.Length >= 2) NpcNews.Tell(a, w[1], w.Length > 2 ? string.Join(" ", w, 2, w.Length - 2) : ""); break; // news <kind> [what] — to it and its heart's people (NpcNews)
                    case "attitude": { float by; if (w.Length >= 2 && float.TryParse(w[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out by)) NpcAttitude.Tell(a, p.GetPlayerID(), by); break; } // attitude <±N> — to you (NpcAttitude)
                    default: Npcs.Log("talk: unknown deed «" + act + "»"); break;
                }
            }
            catch (Exception e) { Npcs.Log("talk deed «" + act + "»: " + e.Message); }
        }

        // a healing mead in your bag (the weakest first)
        private static readonly string[] Meads = { "MeadHealthMinor", "MeadHealthMedium", "MeadHealthMajor", "MeadHealthLingering" };
        private static ItemDrop.ItemData Mead(Player p)
        {
            if (p == null) return null;
            foreach (string m in Meads) foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name == m) return it;
            return null;
        }

        // one in need told to hide where you showed it (NpcRescue): not further than this from where it is
        private const float HideFar = 60f;
        private static string SendHide(NpcActor a, Vector3 at)
        {
            a.Act("rescuemode hide " + Npcs.V(at));
            return a.Name + ": " + Lang.T("іде ховатися");
        }

        private static void Take(Player p, string prefab, int n)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            if (d != null) p.GetInventory().RemoveItem(d.m_itemData.m_shared.m_name, n);
        }

        private static void Give(Player p, string prefab, int n)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (go == null) return;
            if (!p.GetInventory().AddItem(go, n)) { ItemDrop d = go.GetComponent<ItemDrop>(); if (d != null) ItemDrop.DropItem(d.m_itemData.Clone(), n, p.transform.position + p.transform.forward + Vector3.up, Quaternion.identity); }
        }
    }
}
#endif
