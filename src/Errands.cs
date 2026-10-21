#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Доручення Одіна»: a raven brings an errand; its steps are done one after another; done, it cuts notches into the
    // rune stick (RuneBook). The core knows no kind of step or slot: both are classes found by name in a registry
    // (ErrandSteps.cs), so a new kind is a new class and a word in the templates.
    //
    // A template (data: Builtin below, and config/RavenWatch/errands/*.txt while tuning):
    //   [errand wolves]                 its id
    //   name: Вовча зграя
    //   text: Одін хоче, щоб ти проріджив вовчу зграю.
    //   who: munin                      hugin | munin | any: which raven brings it
    //   kind: normal                    normal | golden
    //   price: 1 3                      notches, hidden: one is picked when it is given
    //   needs: defeated_bonemass        boss keys (all of them) before it may come
    //   upto: 2                         it stops coming once more bosses than this are beaten (an early-game errand)
    //   when: flag met_odin, trust 3    story conditions (StoryCond), all of them (2.4: RAVEN_STORY)
    //   weight: 1
    //   within: 2000                    places (slots) only this near the world's middle
    //   far: 2500 3                     a place this far from the middle or further: this many notches at least
    //   mods: nodeath                   modifiers it always has; maymods: nodeath 30 — with this chance (ErrandMods)
    //   slot place = around 300 900     a slot filled when given (ErrandSteps: around …)
    //   step kill Wolf 5                the steps, in order (ErrandSteps: kill, bring, go, do …)
    //   step go place 40
    // The running one is the character's (player customData RavenWatch_errand, with its format number; unknown
    // fields kept), so it lives through leaving the game; the map marks are drawn from it again on entering.
    internal sealed class ErrandDef
    {
        public string Id = "", Name = "", Text = "", Who = "any", Kind = "normal", Group = ""; // group: blood | craft | road (Favour; empty: by its first step)
        public int PriceMin = 1, PriceMax = 1, Upto = 99, FarPrice;
        public float Weight = 1f, Within, FarAt;
        public readonly List<string> Needs = new List<string>();
#if RAVEN_STORY
        public List<Cond> When = new List<Cond>(); // story conditions (StoryCond), all must hold
#endif
        public readonly List<string> Mods = new List<string>();
        public readonly List<KeyValuePair<string, float>> MayMods = new List<KeyValuePair<string, float>>();
        public readonly List<string[]> Slots = new List<string[]>(); // name, kind, args…
        public readonly List<string[]> Steps = new List<string[]>(); // kind, args…
        internal bool Golden { get { return Kind == "golden"; } }
        internal bool Trial { get { return Kind == "trial"; } } // Odin's trial for a raven (Trial.cs): never in the common draw

        internal static List<ErrandDef> ParseAll(string text, string from)
        {
            List<ErrandDef> list = new List<ErrandDef>();
            ErrandDef cur = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash).Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("[errand ") && line.EndsWith("]"))
                {
                    cur = new ErrandDef { Id = line.Substring(8, line.Length - 9).Trim() };
                    if (cur.Id.Length > 0 && cur.Id.IndexOfAny(Errands.Bad) < 0) list.Add(cur);
                    else { Errands.Log("errand id «" + cur.Id + "» in " + from + " left out (no ; = , / | or spaces)"); cur = null; }
                    continue;
                }
                if (cur == null) continue;
                int c = line.IndexOf(':');
                string[] w = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (w[0] == "step" && w.Length > 1) { cur.Steps.Add(Tail(w, 1)); continue; }
                if (w[0] == "slot" && w.Length > 3 && w[2] == "=") { string[] s = new string[w.Length - 2]; s[0] = w[1]; Array.Copy(w, 3, s, 1, w.Length - 3); cur.Slots.Add(s); continue; }
                if (c <= 0) continue;
                string k = line.Substring(0, c).Trim().ToLowerInvariant(), v = line.Substring(c + 1).Trim();
                switch (k)
                {
                    case "name": cur.Name = v; break;
                    case "text": cur.Text = v; break;
                    case "who": cur.Who = v.ToLowerInvariant(); break;
                    case "group": cur.Group = v.ToLowerInvariant(); break;
                    case "kind": cur.Kind = v.ToLowerInvariant() == "golden" ? "golden" : v.ToLowerInvariant() == "trial" ? "trial" : "normal"; break;
                    case "weight": cur.Weight = Errands.F(v, 1f); break;
                    case "upto": cur.Upto = Mathf.Max(0, Errands.I(v, 99)); break;
                    case "mods": foreach (string n in v.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)) cur.Mods.Add(n.ToLowerInvariant()); break;
                    case "maymods":
                        string[] mw = v.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int mi = 0; mi + 1 < mw.Length; mi += 2) cur.MayMods.Add(new KeyValuePair<string, float>(mw[mi].ToLowerInvariant(), Mathf.Clamp(Errands.F(mw[mi + 1], 0f), 0f, 100f)));
                        break;
                    case "within": cur.Within = Mathf.Max(0f, Errands.F(v, 0f)); break;
                    case "far":
                        string[] fw = v.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        cur.FarAt = fw.Length > 0 ? Mathf.Max(0f, Errands.F(fw[0], 0f)) : 0f;
                        cur.FarPrice = fw.Length > 1 ? Mathf.Clamp(Errands.I(fw[1], 1), 1, 9) : 0;
                        break;
#if RAVEN_STORY
                    case "when":
                        List<string> errs = new List<string>();
                        cur.When = StoryCond.Parse(v, errs);
                        foreach (string e in errs) Errands.Log("errand " + cur.Id + " in " + from + ": when: " + e);
                        break;
#endif
                    case "needs": foreach (string n in v.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)) cur.Needs.Add(n); break;
                    case "price":
                        string[] p = v.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                        cur.PriceMin = p.Length > 0 ? Mathf.Clamp(Errands.I(p[0], 1), 1, 9) : 1;
                        cur.PriceMax = p.Length > 1 ? Mathf.Clamp(Errands.I(p[1], cur.PriceMin), cur.PriceMin, 9) : cur.PriceMin;
                        break;
                }
            }
            list.RemoveAll(d => { if (d.Steps.Count > 0) return false; Errands.Log("errand " + d.Id + " has no steps: left out"); return true; });
            return list;
        }

        private static string[] Tail(string[] w, int from) { string[] r = new string[w.Length - from]; Array.Copy(w, from, r, 0, r.Length); return r; }
    }

    // the errand being done: its template, the slots as filled, which step, each step's count
    internal sealed class ErrandRun
    {
        internal const int Format = 1;
        public ErrandDef Def;
        public string Id = "";
        public bool Munin;
        public int Price = 1, Step;
        public double Given;
        public int[] Count = new int[0];
        public int[] Need = new int[0]; // each step's count rolled from its range when given (0: the template's own)
        public readonly List<string> Mods = new List<string>(); // its modifiers (ErrandMods), rolled when given
        public readonly Dictionary<string, string> Slot = new Dictionary<string, string>();
        public ErrandStep[] Steps = new ErrandStep[0];
        private readonly List<KeyValuePair<string, string>> _other = new List<KeyValuePair<string, string>>();

        public bool Elsewhere; // given in another world: held (Def null here) until you are back there

        internal ErrandStep Current { get { return Step >= 0 && Step < Steps.Length ? Steps[Step] : null; } }

        // a step's own mark kept with the errand (a guardian has risen…): "" none
        internal string Mark(string k) { foreach (KeyValuePair<string, string> kv in _other) if (kv.Key == k) return kv.Value; return ""; }
        internal void SetMark(string k, string v) { _other.RemoveAll(kv => kv.Key == k); if (!string.IsNullOrEmpty(v)) _other.Add(new KeyValuePair<string, string>(k, v)); }
        internal bool Finished { get { return Step >= Steps.Length; } }

        internal string Code()
        {
            StringBuilder sb = new StringBuilder("v=").Append(Format);
            sb.Append(";id=").Append(Id).Append(";who=").Append(Munin ? "m" : "h").Append(";price=").Append(Price)
              .Append(";step=").Append(Step).Append(";at=").Append(Given.ToString("0", CultureInfo.InvariantCulture)).Append(";c=");
            for (int i = 0; i < Count.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Count[i]); }
            bool rolled = false;
            foreach (int n in Need) if (n > 0) rolled = true;
            if (Mods.Count > 0) sb.Append(";md=").Append(string.Join("+", Mods.ToArray()));
            if (rolled) { sb.Append(";n="); for (int i = 0; i < Need.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Need[i]); } }
            foreach (KeyValuePair<string, string> kv in Slot) sb.Append(";s.").Append(kv.Key).Append('=').Append(kv.Value);
            foreach (KeyValuePair<string, string> kv in _other) sb.Append(';').Append(kv.Key).Append('=').Append(kv.Value);
            return sb.ToString();
        }

        // null when the template is gone (another version of the mod, a removed file): the errand cannot go on
        internal static ErrandRun Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            ErrandRun r = new ErrandRun();
            string counts = "", needs = "";
            foreach (string part in raw.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                switch (k)
                {
                    case "v": break;
                    case "id": r.Id = v; break;
                    case "who": r.Munin = v == "m"; break;
                    case "price": r.Price = Mathf.Max(1, Errands.I(v, 1)); break;
                    case "step": r.Step = Mathf.Max(0, Errands.I(v, 0)); break;
                    case "at": double d; r.Given = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0; break;
                    case "c": counts = v; break;
                    case "n": needs = v; break;
                    case "md": foreach (string m in v.Split('+')) if (m.Length > 0) r.Mods.Add(m); break;
                    default:
                        if (k.StartsWith("s.")) r.Slot[k.Substring(2)] = v;
                        else r._other.Add(new KeyValuePair<string, string>(k, v));
                        break;
                }
            }
            r.Def = Errands.Find(r.Id);
            if (needs.Length > 0) { string[] ns = needs.Split(','); r.Need = new int[ns.Length]; for (int i = 0; i < ns.Length; i++) r.Need[i] = Mathf.Max(0, Errands.I(ns[i], 0)); }
            if (r.Def == null) return r; // kept as it is (Def null): told in the book, can be dropped
            r.Build();
            string[] cs = counts.Split(',');
            for (int i = 0; i < r.Count.Length && i < cs.Length; i++) r.Count[i] = Mathf.Max(0, Errands.I(cs[i], 0));
            return r;
        }

        internal void Build()
        {
            Steps = new ErrandStep[Def.Steps.Count];
            for (int i = 0; i < Steps.Length; i++)
            {
                string[] w = Def.Steps[i];
                int j = i < Need.Length && Need[i] > 0 ? ErrandSteps.RangeArg(w) : -1;
                if (j > 0) { w = (string[])w.Clone(); w[j] = Need[i].ToString(CultureInfo.InvariantCulture); }
                Steps[i] = ErrandSteps.Make(w);
            }
            if (Count.Length != Steps.Length) Count = new int[Steps.Length];
        }
    }

    internal static class Errands
    {
        internal const string Key = "RavenWatch_errand", LogKey = "RavenWatch_errlog";
        internal static readonly char[] Bad = { ';', '=', ',', '/', '|', ' ', '\n' };
        internal static bool ForceNoDeath; // the test window: every errand given with «Без смерті»
        internal static int HistoryKeep = 6; // the last errands done or dropped: not brought again soon

        // ---------------------------------------------------------------- templates
        private static readonly string[] Builtin =
        {
            // starters
            "[errand starter_hunt]\nname: Ловці в лісі\ntext: Одін хоче знати, чи не розучився ти полювати.\nwho: munin\nprice: 1 2\nupto: 2\n" +
            "step kill Boar,Deer,Neck,Greyling,Greydwarf 40\n",
            "[errand starter_trophy]\nname: Знак для Одіна\ntext: Вполюй трьох оленів і принеси ворону трофей оленя: Одін хоче бачити, що ти ходиш його лісами.\nwho: hugin\nprice: 1\nupto: 2\n" +
            "step kill Deer 3\nstep bring TrophyDeer 1\n",
            "[errand starter_walk]\nname: Подивись, що там\ntext: Хугін бачив щось у тому боці. Сходи й глянь сам.\nwho: hugin\nprice: 1 3\n" +
            "slot place = around 200 600\nstep go place 40\n",
            "[errand starter_fish]\nname: Риба для ворона\ntext: Мунін хоче риби. Свіжої. Твоєї.\nwho: munin\nprice: 1\n" +
            "step do Fish 2\n",
            "[errand starter_fish_big]\nname: Улов для Одіна\ntext: Мунін каже, що двох риб мало навіть для ворона. Одін хоче бачити справжній улов.\nwho: munin\nprice: 2\n" +
            "step do Fish 10\n",
            "[errand starter_champion]\nname: Ватажок\ntext: Мунін бачив звіра, що виріс понад міру. Одін хоче, щоб його не стало.\nwho: munin\nprice: 2 3\n" +
            "slot boss = champion around 300 1200\nstep kill_champion boss\n",
            "[errand starter_crypt]\nname: Старий склеп\ntext: Хугін хоче знати, чи спокійно спить той, хто лежить у склепі. Не спить — то впокой його. Зачисть це місце.\nwho: hugin\nprice: 1 3\nneeds: defeated_eikthyr\n" +
            "slot place = location Crypt,SunkenCrypt,TrollCave 300 3000 fresh\nstep clear place 40\n",
            "[errand starter_runestone]\nname: Рунний камінь\ntext: На камені є слова, яких Хугін ще не читав. Дійди й прочитай їх.\nwho: hugin\nprice: 1 2\nfar: 2500 3\n" +
            "slot place = location Runestone 200 2500\nstep read place 30\n",
            // Hugin: the peaceful ones
            "[errand h_berries]\nname: Дари лісу\ntext: Хугін хоче, щоб ти зібрав те, що ліс дає задарма: ягоди, гриби, квіти. Багато.\nwho: hugin\nprice: 1 2\nupto: 3\n" +
            "step do Pick 100 forage\n",
            "[errand h_resin]\nname: Світло в домі\ntext: Одін любить, коли в домі світло. Заправ смолоскипи живицею — хоч свої, хоч чужі.\nwho: hugin\nprice: 1 2\nneeds: defeated_bonemass\n" +
            "step do Fuel 20-40 Resin\n",
            "[errand h_cook]\nname: Стіл для воїна\ntext: Хугін каже, що голодний воїн — поганий воїн. Приготуй щось путнє, і не одну страву.\nwho: hugin\nprice: 1 2\nneeds: defeated_eikthyr\n" +
            "step do Cook 20\n",
            "[errand h_trollcave]\nname: Печера троля\ntext: Хугін бачив печеру, з якої тягне холодом і тролем. Дійди до неї й здолай того, хто там живе.\nwho: hugin\nprice: 2 3\nneeds: defeated_eikthyr\nwithin: 2000\n" +
            "slot place = location TrollCave 300 3000 fresh\nstep go place 25\nstep kill Troll 1 near place 150\n",
            "[errand h_smelt]\nname: Вогонь кузні\ntext: Одін хоче чути, як співає твоя плавильня. Переплав руду — багато руди.\nwho: hugin\nprice: 1 3\nneeds: defeated_gdking\n" +
            "step do Smelt 30\n",
            "[errand h_ooze]\nname: Слиз болотяний\ntext: Хугін збирає дивні речі. Цього разу йому треба болотяного слизу — і такого, що здобуто твоїми руками, а не вимінено.\nwho: hugin\nprice: 2\nneeds: defeated_gdking\nupto: 4\n" +
            "step loot Ooze 20\nstep bring Ooze 20\n",
            "[errand h_crystal]\nname: Холодне світло\ntext: Хугін хоче кришталю з гір: каже, в ньому видно те, чого не видно оком. Тільки той, що вибито з гори твоїми руками.\nwho: hugin\nprice: 2 3\nneeds: defeated_bonemass\nupto: 5\n" +
            "step loot Crystal 10\nstep bring Crystal 10\n",
            "[errand h_needles]\nname: Жала рівнин\ntext: Хугін хоче голок із тих комарів-вбивць. Обережно: вони кусають боляче.\nwho: hugin\nprice: 2 3\nneeds: defeated_dragon\nupto: 6\n" +
            "step kill Deathsquito 10\nstep bring Needle 10\n",
            "[errand h_pelts]\nname: Шкури для гнізда\ntext: Хугін хоче вистелити гніздо вовчими шкурами. Тільки не купленими — знятими з вовків твоїми руками.\nwho: hugin\nprice: 2\nneeds: defeated_bonemass\nupto: 6\n" +
            "step loot WolfPelt 5\nstep bring WolfPelt 5\n",
            "[errand h_trophywall]\nname: Трофеї на стіну\ntext: Одін любить, коли пам'ятають, кого здолано. Повісь трофеї на підставки.\nwho: hugin\nprice: 1\n" +
            "step do TrophyHang 3\n",
            "[errand h_mead]\nname: Мед для гостя\ntext: До тебе може зайти гість, якого ти не впізнаєш. Хай у бродильні буде мед.\nwho: hugin\nprice: 1 2\nneeds: defeated_gdking\n" +
            "step do Ferment 4\n",
            "[errand h_fire]\nname: Вогонь і ворони\ntext: Хугін каже, що найкращі думки приходять уночі біля вогню. Сядь, і хай обидва ворони будуть з тобою.\nwho: hugin\nprice: 1\nupto: 2\n" +
            "step rest 120\n",
            "[errand h_fire_late]\nname: Вогонь і ворони\ntext: Хугін каже, що найкращі думки приходять уночі біля вогню. Сядь, і хай обидва ворони будуть з тобою.\nwho: hugin\nprice: 1\nneeds: defeated_bonemass\nweight: 0.15\n" +
            "step rest 120\n",
            "[errand h_tree]\nname: Вороняче дерево\ntext: Хугін чув, як десь далеко збираються ворони. Там старе дерево, і руни біля нього шепочуться. Знайди його.\nwho: hugin\nprice: 2 3\n" +
            "slot place = location RavenTree 300 6000\nstep go place 15\n",
            // Munin: the bloody ones
            "[errand m_forest]\nname: Тіні Чорнолісся\ntext: Мунін хоче, щоб Чорнолісся стало тихішим. На добрих кілька десятків голів.\nwho: munin\nprice: 2 3\nneeds: defeated_eikthyr\nupto: 3\n" +
            "step kill Greydwarf,Greydwarf_Elite,Greydwarf_Shaman,Skeleton,Troll 20-40\n",
            "[errand m_troll]\nname: Голова троля\ntext: Мунін хоче бачити голову троля. Без троля — ніяк.\nwho: munin\nprice: 3\nneeds: defeated_eikthyr\nupto: 4\n" +
            "step kill Troll 1\nstep bring TrophyFrostTroll 1\n",
            "[errand m_swamp]\nname: Болото не спить\ntext: Мерці на болоті розлізлися. Мунін хоче, щоб їх поменшало.\nwho: munin\nprice: 2 3\nneeds: defeated_gdking\nupto: 4\n" +
            "step kill Draugr,Draugr_Elite,Blob,BlobElite,Leech,Wraith 20-40\n",
            "[errand m_mountain]\nname: Вовки на вершинах\ntext: У горах стало гамірно. Мунін хоче тиші — кривавої.\nwho: munin\nprice: 2 3\nneeds: defeated_bonemass\nupto: 5\n" +
            "step kill Wolf,Fenring,Hatchling,Ulv,StoneGolem 20-40\n",
            "[errand m_plains]\nname: Гоблінський рахунок\ntext: Мунін має з гоблінами давні рахунки. Допоможи їх закрити.\nwho: munin\nprice: 2 3\nneeds: defeated_dragon\nupto: 6\n" +
            "step kill Goblin,GoblinBrute,GoblinShaman,Deathsquito,Lox 20-40\n",
            "[errand m_blackmetal]\nname: Чорне залізо\ntext: Мунін хоче чорного металу з фулінгів. Знятого з них самих, а не з чужої скрині.\nwho: munin\nprice: 2 3\nneeds: defeated_dragon\n" +
            "step loot BlackMetalScrap 20\nstep bring BlackMetalScrap 20\n",
            "[errand m_ravenkill]\nname: Кігті й дзьоб\ntext: Мунін хоче, щоб вороги падали від воронячого удару, а не від твого. Добий їх дзьобом чи кігтями.\nwho: munin\nprice: 1 2\n" +
            "step kill * 10 raven\n",
            "[errand m_nests]\nname: Гнізда грейдворфів\ntext: Грейдворфи плодяться з гнізд у Чорноліссі. Мунін хоче, щоб їх поменшало — гнізд, а не грейдворфів.\nwho: munin\nprice: 1 2\nneeds: defeated_eikthyr\nupto: 3\n" +
            "step destroy Spawner_GreydwarfNest 3\n",
            "[errand m_mist]\nname: Те, що в тумані\ntext: У тумані щось ворушиться. Мунін хоче знати, чи воно помирає.\nwho: munin\nprice: 2 3\nneeds: defeated_goblinking\n" +
            "step kill Seeker,SeekerBrute,Tick,Gjall 10-20\n",
            "[errand m_ash]\nname: Попіл і кістки\ntext: Мунін хоче почути, як тріщать обвуглені кістки. Багато кісток.\nwho: munin\nprice: 2 3\nneeds: defeated_queen\n" +
            "step kill Charred_Melee,Charred_Archer,Charred_Mage,Morgen,Asksvin,FallenValkyrie 20-30\n",
            "[errand m_parry]\nname: Тримай удар\ntext: Мунін хоче бачити, як ти відбиваєш удари вчасно. Не щитом махаєш — а відбиваєш.\nwho: munin\nprice: 1 2\n" +
            "step do Parry 10-20\n",
            "[errand m_champion_far]\nname: Далекий ватажок\ntext: Мунін бачив ватажка далеко звідси. Дорога довга, бій — ще довший.\nwho: munin\nprice: 3\n" +
            "slot boss = champion around 800 2000\nstep kill_champion boss\n",
            // Odin's trial for a raven (Trial.cs): its raven is the one who would bring it — Hugin's peaceful, Munin's bloody
            "[errand trial_hugin]\nname: Випробування Хугіна\ntext: Одін хоче знати, чи вмієш ти бачити. Збери, що дає ліс, вполюй оленів і поклади в чашу гнізда трофей оленя.\nwho: hugin\nkind: trial\n" +
            "step do Pick 20\nstep kill Deer 3\nstep bring TrophyDeer 1\n",
            "[errand trial_munin]\nname: Випробування Муніна\ntext: Одін хоче знати, чи не тремтить твоя рука. Убий грейдворфів у Чорноліссі й поклади в чашу гнізда трофей грейдворфа.\nwho: munin\nkind: trial\n" +
            "step kill Greydwarf,Greydwarf_Elite,Greydwarf_Shaman 10\nstep bring TrophyGreydwarf 1\n",
            // golden
            "[errand golden_saga]\nname: Сага про камінь і звіра\ntext: Одін кличе сам. Прочитай рунний камінь, а тоді здолай звіра, про якого він мовчить.\nwho: any\nkind: golden\n" +
            "slot place = location Runestone 400 3000\nslot boss = champion around 500 1800\nstep read place 30\nstep kill_champion boss\nmaymods: nodeath 30\n",
            "[errand golden_path]\nname: Три камені\ntext: Одін кличе сам. Три камені стоять неподалік один від одного, і кожен каже частину. Прочитай усі три — і побачиш, хто їх стереже.\nwho: any\nkind: golden\nmaymods: nodeath 30\n" +
            "slot a = location Runestone 300 1000\nslot b = location Runestone 0 1500 near a nearest\nslot c = location Runestone 0 1500 near b nearest\nslot boss = champion near c 25 50\n" +
            "step read a 30\nstep read b 30\nstep read c 30\nstep kill_champion boss\n",
            "[errand golden_barrow]\nname: Курган мертвого конунга\ntext: Одін кличе сам. У затопленому кургані спить конунг, якого не впокоїли. Зачисть курган — і коли вийдеш, він вийде за тобою. Принеси Одіну його голову.\nwho: any\nkind: golden\nneeds: defeated_gdking\nmaymods: nodeath 30\n" +
            "slot place = location SunkenCrypt 300 3000 fresh\nslot king = champion near place 10 16 lord who:Draugr_Elite title:Мертвий_конунг\n" +
            "step clear place 40\nstep kill_champion king guard place\nstep bring TrophyDraugrElite 1\n",
            "[errand golden_riddle]\nname: Загадка Одіна\ntext: Одін кличе сам. Він не скаже, чого хоче, — лише загадає. Відгадай і поклади відгадку в чашу гнізда.\nwho: any\nkind: golden\nweight: 1.5\n" +
            "slot r = riddle\nstep riddle r\n",
            "[errand golden_wolfnight]\nname: Ніч вовка\ntext: Одін кличе сам. Уночі вбий десятьох вовків, а тоді сядь біля вогню з воронами й дочекайся ранку. Хто спить — той не бачить, як приходить світанок.\nwho: any\nkind: golden\nneeds: defeated_bonemass\nmaymods: nodeath 30\n" +
            "step kill Wolf 10 night\nstep rest dawn\n",
            "[errand golden_twins]\nname: Двоє ватажків\ntext: Одін кличе сам. Двоє звірів виросли понад міру і ходять разом. Здолай обох.\nwho: any\nkind: golden\n" +
            "slot a = champion around 400 1500\nslot b = champion near a 20 40\nstep kill_champion a,b\nmaymods: nodeath 30\n",
        };

        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "errands"); } }
        private static List<ErrandDef> _all;

        internal static List<ErrandDef> All()
        {
            if (_all != null) return _all;
            Dictionary<string, ErrandDef> by = new Dictionary<string, ErrandDef>();
            foreach (string s in Builtin) foreach (ErrandDef d in ErrandDef.ParseAll(s, "built-in")) by[d.Id] = d;
            if (RavenLooks.FromFiles && Directory.Exists(Dir))
                foreach (string f in Directory.GetFiles(Dir, "*.txt"))
                {
                    try { foreach (ErrandDef d in ErrandDef.ParseAll(File.ReadAllText(f, Encoding.UTF8), Path.GetFileName(f))) by[d.Id] = d; }
                    catch (Exception e) { Log("errands " + f + ": " + e.Message); }
                }
            _all = new List<ErrandDef>(by.Values);
            foreach (ErrandDef d in _all) foreach (string[] st in d.Steps) if (!ErrandSteps.Known(st[0])) Log("errand " + d.Id + ": unknown step «" + st[0] + "»");
            return _all;
        }

        internal static void Reload() { _all = null; }

        internal static ErrandDef Find(string id) { foreach (ErrandDef d in All()) if (d.Id == id) return d; return null; }

        // ---------------------------------------------------------------- the running one
        private static string _raw;
        private static ErrandRun _run;
        private static long _world;

        internal static ErrandRun Of(Player p)
        {
            string raw;
            if (p == null || !p.m_customData.TryGetValue(Key, out raw)) raw = "";
            long world = ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L;
            if (raw == _raw && world == _world) return _run;
            _raw = raw; _world = world; _run = ErrandRun.Parse(raw);
            // given in another world: its places, rings and clears mean nothing here (a crypt there would count as cleared)
            string w = _run != null ? _run.Mark("w") : "";
            if (_run != null && w.Length > 0 && world != 0L && w != world.ToString(CultureInfo.InvariantCulture)) { _run.Def = null; _run.Elsewhere = true; }
            return _run;
        }

        private static void Save(Player p, ErrandRun r)
        {
            if (p == null) return;
            if (r == null) p.m_customData.Remove(Key); else p.m_customData[Key] = r.Code();
            _raw = r != null ? p.m_customData[Key] : ""; _run = r;
            Version++;
        }

        internal static int Version; // goes up on every change (the book, the nest)

        // ---------------------------------------------------------------- giving one
        // the templates that may come now (progress, which raven, not done lately)
        internal static List<ErrandDef> Fitting(Player p, bool munin, bool golden) { return Fitting(p, munin, golden, true); }

        private static List<ErrandDef> Fitting(Player p, bool munin, bool golden, bool notRecent)
        {
            List<string> recent = notRecent ? History(p) : new List<string>();
            List<ErrandDef> l = new List<ErrandDef>();
            foreach (ErrandDef d in All())
            {
                if (d.Golden != golden || d.Trial) continue;
                if (d.Who == "hugin" && munin || d.Who == "munin" && !munin) continue;
                if (recent.Contains(d.Id)) continue;
                if (!Stage.HasAll(p, d.Needs) || Stage.Of(p) > d.Upto) continue;
#if RAVEN_STORY
                if (!StoryCond.All(p, d.When)) continue;
#endif
                l.Add(d);
            }
            return l;
        }

        // one of them at random (by weight); null when none fits. noServer: only those that need no place of the world
        internal static ErrandDef Pick(Player p, bool munin, bool golden) { return Pick(p, munin, golden, false); }

        internal static ErrandDef Pick(Player p, bool munin, bool golden, bool noServer)
        {
            List<ErrandDef> l = Fitting(p, munin, golden);
            if (noServer) l.RemoveAll(NeedsServer);
            if (l.Count == 0) { l = Fitting(p, munin, golden, false); if (noServer) l.RemoveAll(NeedsServer); } // all of them done lately: one again rather than none
            float sum = 0f;
            // (Odin's favour leans the weights: Favour)
            foreach (ErrandDef d in l) sum += Mathf.Max(0.01f, d.Weight) * Favour.Mul(p, d);
            float at = UnityEngine.Random.value * sum;
            foreach (ErrandDef d in l) { at -= Mathf.Max(0.01f, d.Weight) * Favour.Mul(p, d); if (at <= 0f) return d; }
            return l.Count > 0 ? l[l.Count - 1] : null;
        }

        // location … near <slot>: the ring round that slot's place
        private static string NearOf(string[] s) { int i = Array.IndexOf(s, "near"); return i > 4 && i + 1 < s.Length ? s[i + 1] : ""; }

        internal static bool NeedsServer(ErrandDef d) { foreach (string[] s in d.Slots) if (s.Length > 1 && s[1] == "location") return true; return false; }

        // Given at once, or (a place of the world in it) once the server says where: null then too. A place nobody
        // could find: another errand of the same kind that needs none, if there is one. failed: told when nothing came.
        internal static string Give(Player p, ErrandDef d, bool munin) { return Give(p, d, munin, null); }

        internal static string Give(Player p, ErrandDef d, bool munin, Action<string> failed)
        {
            if (p == null || d == null) return Lang.T("Нема такого доручення.");
            if (Of(p) != null) return Lang.T("Спершу закінч або відклади те доручення, що вже маєш.");
            if (!NeedsServer(d)) return GiveNow(p, d, munin, null);
            string lands;
            if (!p.m_customData.TryGetValue(Diary.LandsKey, out lands)) lands = "|";
            List<ErrandNet.Ask> asks = new List<ErrandNet.Ask>();
            foreach (string[] s in d.Slots)
                if (s.Length > 1 && s[1] == "location")
                    asks.Add(new ErrandNet.Ask { Slot = s[0], Names = s.Length > 2 ? s[2] : "", Lands = lands, X = p.transform.position.x, Z = p.transform.position.z, Min = s.Length > 3 ? F(s[3], 200f) : 200f, Max = s.Length > 4 ? F(s[4], 2000f) : 2000f, Within = d.Within, Fresh = Array.IndexOf(s, "fresh") > 4, Nearest = Array.IndexOf(s, "nearest") > 4, Near = NearOf(s) });
            ErrandNet.Find(asks, found =>
            {
                Player me = Player.m_localPlayer;
                if (me == null || me != p) return;
                bool all = found != null;
                if (all) foreach (ErrandNet.Ask a in asks) { string v; if (!found.TryGetValue(a.Slot, out v) || v.Length == 0) all = false; }
                string why = null;
                if (all) why = GiveNow(me, d, munin, found);
                else
                {
                    Log("errand " + d.Id + ": no such place found" + (found == null ? " (no answer)" : ""));
                    ErrandDef other = Pick(me, munin, d.Golden, true);
                    why = other != null ? GiveNow(me, other, munin, null) : Lang.T("Одін не знайшов, куди тебе послати.");
                }
                if (why != null && failed != null) failed(why);
            });
            return null;
        }

        internal static string GiveNow(Player p, ErrandDef d, bool munin, Dictionary<string, string> found)
        {
            if (Of(p) != null) return Lang.T("Спершу закінч або відклади те доручення, що вже маєш.");
            ErrandRun r = new ErrandRun { Def = d, Id = d.Id, Munin = munin, Given = WorldTime.Now };
            if (ZNet.instance != null) r.SetMark("w", ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture));
            r.Price = UnityEngine.Random.Range(d.PriceMin, d.PriceMax + 1);
            // counts given as a range: rolled now; the more it asks, the more notches (between the template's two)
            r.Need = new int[d.Steps.Count];
            float share = 0f;
            int ranged = 0;
            for (int i = 0; i < d.Steps.Count; i++)
            {
                int j = ErrandSteps.RangeArg(d.Steps[i]), lo, hi;
                if (j < 0 || !ErrandSteps.Range(d.Steps[i][j], out lo, out hi)) continue;
                r.Need[i] = ErrandSteps.Roll(lo, hi);
                share += hi > lo ? (r.Need[i] - lo) / (float)(hi - lo) : 0.5f;
                ranged++;
            }
            if (ranged > 0) r.Price = d.PriceMin + Mathf.FloorToInt((d.PriceMax - d.PriceMin) * share / ranged + 0.5f); // (half up: 30 of 20–40 is the upper price)
            foreach (string[] s in d.Slots)
            {
                string why = null, v;
                if (found == null || !found.TryGetValue(s[0], out v)) v = ErrandSteps.Fill(p, s, r.Slot, d.Within, out why);
                if (v == null) { Log("errand " + d.Id + ": slot " + s[0] + " not filled (" + why + ")"); return why ?? Lang.T("Одін не знайшов, куди тебе послати."); }
                r.Slot[s[0]] = v;
            }
            r.Mods.AddRange(ErrandMods.Roll(d));
            if (ForceNoDeath && !r.Mods.Contains("nodeath")) r.Mods.Add("nodeath"); // (the test window's switch)
            if (d.FarAt > 0f && d.FarPrice > 0)
                foreach (string v in r.Slot.Values)
                {
                    Vector3 at;
                    if (ErrandSteps.Point(v, out at) && new Vector2(at.x, at.z).magnitude >= d.FarAt) { r.Price = Mathf.Max(r.Price, d.FarPrice); break; }
                }
            r.Build();
            Save(p, r);
            if (!d.Trial) Favour.Spent(p, d); // the favour that leaned Odin to it is used up
            Start(p, r);
            Log("given " + d.Id + " by " + (munin ? "Munin" : "Hugin") + " to " + p.GetPlayerName() + " (" + p.GetPlayerID() + "), price " + r.Price + ", stage " + Stage.Of(p) + (r.Mods.Count > 0 ? ", mods " + string.Join("+", r.Mods.ToArray()) : ""));
            if (d.Trial) { RavenMsg.Center(p, Lang.F("Одін дав випробування «{0}» і книгу воронів. Що треба — у книзі (B), «Шлях Одіна».", Lang.T(d.Name))); return null; }
            RavenMsg.Center(p, Lang.F("{0} приніс доручення від Одіна: «{1}»", Plugin.RavenName(munin), Lang.T(d.Name)));
            ErrandTalk.Given(munin);
#if RAVEN_STORY
            StoryBus.Raise("errand_given", d.Id, r.Price);
#endif
            return null;
        }

        private static void Start(Player p, ErrandRun r)
        {
            ErrandStep s = r.Current;
            if (s == null) return;
            try { s.Start(r, r.Step, p); } catch (Exception e) { Log("step start " + r.Id + ": " + e); }
        }

        // ---------------------------------------------------------------- what moves it on
        internal static void OnEvent(Ev type, string arg, int value)
        {
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            if (r == null || r.Def == null || r.Current == null) return;
            string broken = ErrandMods.Breaks(r, type);
            if (broken != null) { Fail(p, r, broken); return; }
            try { if (r.Current.OnEvent(r, r.Step, type, arg, value)) Moved(p, r); }
            catch (Exception e) { Log("event " + type + ": " + e.Message); }
        }

        // a champion of yours died (wherever it was simulated)
        internal static void OnChampion(string stamp)
        {
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            if (r == null || r.Def == null) return;
            ChampionStep s = r.Current as ChampionStep;
            if (s != null && s.Killed(r, r.Step, stamp)) Moved(p, r);
        }

        // a champion of this errand stamp is still wanted (its errand goes on)
        internal static bool Wants(Player p, string stamp)
        {
            ErrandRun r = Of(p);
            if (r == null || r.Def == null) return false;
            for (int i = r.Step; i < r.Steps.Length; i++) { ChampionStep c = r.Steps[i] as ChampionStep; if (c != null && c.Wants(r, i, stamp)) return true; }
            return false;
        }

        // a thing you hit is gone (DestroyWatch); asked first whether the errand wants that kind broken
        internal static bool DestroyWants(string prefab) { ErrandRun r = Of(Player.m_localPlayer); DestroyStep s = r != null && r.Def != null ? r.Current as DestroyStep : null; return s != null && s.Wants(prefab); }

        internal static void OnDestroyed(string prefab)
        {
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            DestroyStep s = r != null && r.Def != null ? r.Current as DestroyStep : null;
            if (s != null && s.OnGone(r, r.Step, prefab)) Moved(p, r);
        }

        // Odin's riddle waiting for its answer (the nest's bowl takes anything then)
        internal static bool RiddleOpen(Player p) { ErrandRun r = Of(p); return r != null && r.Def != null && r.Current is RiddleStep; }

        // laid in the bowl and given to Odin: the riddle's answer? pieces taken (0: not it, or too few — why says)
        internal static int TryRiddle(Player p, string prefab, int n, out string why)
        {
            why = null;
            ErrandRun r = Of(p);
            RiddleStep s = r != null && r.Def != null ? r.Current as RiddleStep : null;
            if (s == null) return 0;
            int used = s.Answer(r, r.Step, p, prefab, n, out why);
            if (used > 0) Moved(p, r);
            return used;
        }

        // a runestone read by you
        internal static void OnRead(Vector3 stone)
        {
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            ReadStep s = r != null && r.Def != null ? r.Current as ReadStep : null;
            if (s != null && s.OnRead(r, r.Step, stone)) Moved(p, r);
        }

        // picked up from the world by your own hand (LootPickupPatch); asked first whether the errand waits for it
        internal static bool WantsLoot { get { ErrandRun r = Of(Player.m_localPlayer); return r != null && r.Def != null && r.Current is LootStep; } }
        internal static bool LootWants(string prefab) { ErrandRun r = Of(Player.m_localPlayer); LootStep s = r != null && r.Def != null ? r.Current as LootStep : null; return s != null && s.Wants(prefab); }

        internal static void OnGain(string prefab, int amount)
        {
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            LootStep s = r != null && r.Def != null ? r.Current as LootStep : null;
            if (s != null && s.OnGain(r, r.Step, prefab, amount)) Moved(p, r);
        }

        internal static void OnKill(string prefab, string token, int stars, bool boss, bool raven)
        {
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            if (r == null || r.Def == null || r.Current == null) return;
            try { if (r.Current.OnKill(r, r.Step, prefab, token, stars, boss, raven)) Moved(p, r); }
            catch (Exception e) { Log("kill " + prefab + ": " + e.Message); }
        }

        // twice a second (Shoulder.Tick): places reached, the map marks
        internal static void Tick()
        {
            ErrandNet.Tick();
            DestroyWatch.Tick();    // things you broke for an errand: gone yet?
            ErrandTalk.Tick();
            RavenTree.MapTick();    // the found raven trees' marks
            Nest.Tick();            // ravens in a nest waiting for Odin, his glow
            Trial.Tick();           // Odin coming to one with no ravens
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            if (r != null && r.Def != null && r.Finished && p != null) { Log(r.Id + ": every step done but not counted (its template has fewer steps now): done"); Complete(p, r); return; }
            if (r == null || r.Def == null || r.Current == null) { if (_pins.Count > 0) ClearPins(); return; }
            if (r.Def.Trial && Trial.TimeLeft(r) <= 0f) { Fail(p, r, Lang.T("час вийшов")); return; }
            try { if (r.Current.Tick(r, r.Step, p)) Moved(p, r); }
            catch (Exception e) { Log("tick " + r.Id + ": " + e.Message); }
            if (r.Current != null) Zone(p, r);
            if (r.Current != null) Pins(r);
        }

        // ---------------------------------------------------------------- the step's zone
        // in: within its ring and the step's margin; out: 20 m further (no flicker on the edge). The step is told on the
        // way in, every 2 s while in, on the way out; a new step or errand starts outside again.
        private static ErrandStep _zoneFor;
        private static bool _inZone;
        private static float _zoneNext;

        private static void Zone(Player p, ErrandRun r)
        {
            ErrandStep s = r.Current;
            if (s != _zoneFor) { _zoneFor = s; _inZone = false; } // (a new step or errand: a new step object)
            Vector3 at; float radius;
            if (p == null || !s.Where(r, r.Step, out at, out radius)) return;
            Vector3 d = p.transform.position - at; d.y = 0f;
            float dist = d.magnitude, edge = radius + s.Margin;
            try
            {
                if (!_inZone && dist <= edge)
                {
                    _inZone = true; _zoneNext = 0f;
                    Log("in the zone of " + r.Id + " step " + (r.Step + 1) + " (" + Mathf.RoundToInt(dist) + " m from its middle)");
                    ErrandTalk.Near(r.Munin);
                    if (s.Enter(r, r.Step, p)) { Moved(p, r); return; }
                }
                else if (_inZone && dist > edge + 20f)
                {
                    _inZone = false;
                    Log("out of the zone of " + r.Id + " step " + (r.Step + 1));
                    s.Leave(r, r.Step, p);
                    return;
                }
                if (_inZone && Time.time >= _zoneNext)
                {
                    _zoneNext = Time.time + 2f;
                    if (s.Inside(r, r.Step, p)) Moved(p, r);
                }
            }
            catch (Exception e) { Log("zone " + r.Id + ": " + e.Message); }
        }

        internal static bool InZone { get { return _inZone; } }

        // a step changed its own mark: kept in the character
        internal static void Keep(Player p, ErrandRun r) { if (p != null && r != null && Of(p) == r) Save(p, r); }

        // hand in what a step wants (the nest's «Віддати»); null when taken, else why not
        internal static string HandIn(Player p)
        {
            ErrandRun r = Of(p);
            if (r == null || r.Current == null) return Lang.T("Зараз нема чого віддавати.");
            string why = r.Current.HandIn(r, r.Step, p);
            if (why == null) Moved(p, r);
            return why;
        }

        // a count changed: saved; the step done → the next one; all done → the notches
        private static void Moved(Player p, ErrandRun r)
        {
            // (the log: every count of a short step; of a long one (100 picks, 40 kills) every fifth and the last)
            if (r.Current != null && (r.Current.Need <= 20 || r.Count[r.Step] % 5 == 0 || r.Current.Done(r, r.Step))) Log("counted for " + p.GetPlayerName() + ": " + r.Id + " step " + (r.Step + 1) + " — " + r.Current.Describe(r, r.Step, p));
            if (r.Current != null && !r.Current.Done(r, r.Step)) ErrandTalk.Count(r.Munin);
            else if (r.Step + 1 < r.Steps.Length) ErrandTalk.Step(r.Munin);
            while (r.Current != null && r.Current.Done(r, r.Step))
            {
                r.Step++;
                if (r.Current != null) { Save(p, r); Start(p, r); RavenMsg.Center(p, Lang.F("Доручення «{0}»: далі — {1}", Lang.T(r.Def.Name), r.Current.Describe(r, r.Step, p))); }
            }
            if (r.Finished) { Complete(p, r); return; }
            Save(p, r);
        }

        private static void Complete(Player p, ErrandRun r)
        {
            ErrandMark.Clear(p);
            FightMusic.Stop();
            Remember(p, r.Id);
            Save(p, null);
            ClearPins();
            if (r.Def != null && r.Def.Trial) { Log("done " + r.Id + ": the trial, a raven"); Trial.Passed(p, r.Munin); return; }
            bool golden = r.Def != null && r.Def.Golden;
            RavenMsg.Center(p, Lang.F("Доручення «{0}» виконано. Одін це бачив.", Lang.T(r.Def != null ? r.Def.Name : r.Id)));
            int bonus = ErrandMods.Pay(p, r, golden); // (a modifier's risk: a spark of Odin, or more notches; a golden one, notches over its rune)
            Log("done " + r.Id + (golden ? ", a golden rune" : ", " + r.Price + " notches") + (bonus > 0 ? ", +" + bonus + " for its modifiers" : ""));
            if (golden) { RuneBook.AddGold(p, 1); if (bonus > 0) RuneBook.AddNotches(p, bonus); }
            else RuneBook.AddNotches(p, r.Price + bonus);
            ErrandTalk.Done(r.Munin);
#if RAVEN_STORY
            StoryBus.Raise("errand_done", r.Id, golden ? 0 : r.Price);
#endif
        }

        // lost to a modifier (died with «Без смерті»): like giving it up, said aloud
        private static void Fail(Player p, ErrandRun r, string why)
        {
            ErrandMark.Clear(p);
            FightMusic.Stop();
            Remember(p, r.Id);
            Save(p, null);
            ClearPins();
            Log("lost " + r.Id + ": " + why);
#if RAVEN_STORY
            StoryBus.Raise("errand_failed", r.Id, 0);
#endif
            if (r.Def != null && r.Def.Trial) { Trial.Failed(p, why); return; }
            RavenMsg.Center(p, Lang.F("Доручення «{0}» втрачено: {1}. Одін не прощає.", Lang.T(r.Def != null ? r.Def.Name : r.Id), why));
        }

        // given up (the nest's «Відмовитись»): no notches, nothing lost
        internal static void Drop(Player p)
        {
            ErrandRun r = Of(p);
            if (r == null) return;
            ErrandMark.Clear(p);
            FightMusic.Stop();
            Remember(p, r.Id);
            Save(p, null);
            ClearPins();
            Log("dropped " + r.Id);
#if RAVEN_STORY
            StoryBus.Raise("errand_dropped", r.Id, 0);
#endif
            if (r.Def != null && r.Def.Trial) Trial.Failed(p, null); // a trial given up is a trial lost
        }

        // ---------------------------------------------------------------- not the same errand again soon
        internal static List<string> History(Player p)
        {
            string raw;
            List<string> l = new List<string>();
            if (p != null && p.m_customData.TryGetValue(LogKey, out raw)) foreach (string s in raw.Split(',')) if (s.Length > 0) l.Add(s);
            return l;
        }

        private static void Remember(Player p, string id)
        {
            List<string> l = History(p);
            l.Remove(id);
            l.Add(id);
            while (l.Count > HistoryKeep) l.RemoveAt(0);
            p.m_customData[LogKey] = string.Join(",", l.ToArray());
        }

        // ---------------------------------------------------------------- map marks
        // never saved in the game's map: drawn from the errand (again after entering the world, on a new step)
        private static readonly List<Minimap.PinData> _pins = new List<Minimap.PinData>();
        private static ErrandStep _pinsFor; // (a new step or errand: a new step object)
        private static Minimap _map;

        private static void Pins(ErrandRun r)
        {
            if (Minimap.instance == null) return;
            if (_map != Minimap.instance) { _map = Minimap.instance; _pins.Clear(); ErrandMap.Ours.Clear(); _pinsFor = null; } // a new map (entered the world again): its marks are drawn anew
            if (_pinsFor == r.Current) return;
            ClearPins();
            _pinsFor = r.Current;
            Vector3 at; float radius;
            if (!r.Current.Where(r, r.Step, out at, out radius)) return;
            string label = Lang.T(r.Def.Name);
            Minimap.PinData area = Minimap.instance.AddPin(at, Minimap.PinType.EventArea, "", false, false, 0L, default(Splatform.PlatformUserID));
            area.m_worldSize = radius * 2f;
            area.m_icon = ErrandMap.GreyOf(area.m_icon); // (coloured blue by ErrandMap: not the red of raids)
            _pins.Add(area);
            Minimap.PinData mark = Minimap.instance.AddPin(at, Minimap.PinType.RandomEvent, label, false, false, 0L, default(Splatform.PlatformUserID));
            mark.m_animate = true;
            mark.m_icon = ErrandMap.GreyOf(mark.m_icon);
            _pins.Add(mark);
            ErrandMap.Ours.Clear(); ErrandMap.Ours.AddRange(_pins);
        }

        private static void ClearPins()
        {
            if (Minimap.instance != null) foreach (Minimap.PinData pin in _pins) if (pin != null) Minimap.instance.RemovePin(pin);
            _pins.Clear();
            ErrandMap.Ours.Clear();
            _pinsFor = null;
        }

        internal static void Reset() { ClearPins(); _raw = null; _run = null; }

        // ---------------------------------------------------------------- small things
        internal static readonly List<string> Recent = new List<string>(); // the last lines, for the test window
        internal static void Log(string s)
        {
            if (Plugin.Instance != null) Plugin.Instance.Log("Errands: " + s);
            Recent.Add(System.DateTime.Now.ToString("HH:mm:ss") + "  " + s);
            if (Recent.Count > 60) Recent.RemoveAt(0);
        }
        internal static int I(string s, int def) { int v; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def; }
        internal static float F(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }

        // what the book and the nest show: the name, the words, every step with its state
        internal static string Describe(Player p, ErrandRun r)
        {
            if (r == null) return Lang.T("Доручення нема.");
            if (r.Elsewhere) return Lang.T("Це доручення Одін дав в іншому світі: воно чекає, доки ти туди повернешся.");
            if (r.Def == null) return Lang.F("Доручення «{0}» з іншої версії мода: його можна лише відкласти.", r.Id);
            StringBuilder sb = new StringBuilder();
            sb.Append(Lang.T(r.Def.Name)).Append(" (").Append(Plugin.RavenName(r.Munin)).Append(")");
            if (r.Def.Text.Length > 0) sb.Append('\n').Append(Lang.T(r.Def.Text));
            foreach (string m in ErrandMods.Lines(r)) sb.Append('\n').Append(m);
            for (int i = 0; i < r.Steps.Length; i++)
                sb.Append('\n').Append(i < r.Step ? "✓ " : i == r.Step ? "• " : "  ").Append(r.Steps[i].Describe(r, i, p));
            return sb.ToString();
        }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven errand · list · give <id> [hugin|munin] · hand · step · done · drop · reload
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            ErrandRun r = Of(p);
            switch (what)
            {
                case "list":
                    StringBuilder l = new StringBuilder("Доручення (" + All().Count + "), етап " + Stage.Of(p) + ":");
                    foreach (ErrandDef d in All())
                        l.Append("\n  ").Append(d.Id).Append(" — ").Append(d.Name).Append(" · ").Append(d.Who).Append(d.Golden ? " · золоте" : "")
                         .Append(Stage.HasAll(p, d.Needs) ? "" : " · не на цьому етапі");
                    return l.ToString();
                case "give":
                    if (a.Length < 3) return "raven errand give <id> [hugin|munin]";
                    ErrandDef def = Find(a[2]);
                    if (def == null) return "Нема доручення «" + a[2] + "» (raven errand list).";
                    bool m = a.Length > 3 ? a[3].ToLowerInvariant().StartsWith("m") : def.Who == "munin";
                    if (r != null) Drop(p);
                    return Give(p, def, m) ?? "Дано: " + def.Id + ".";
                case "hand": return HandIn(p) ?? "Віддано.";
                case "step":
                    if (r == null || r.Current == null) return "Доручення нема.";
                    r.Current.Force(r, r.Step); Moved(p, r);
                    return "Крок пройдено.";
                case "done":
                    if (r == null) return "Доручення нема.";
                    if (r.Def == null) { Drop(p); return "Відкладено (шаблону нема)."; }
                    r.Step = r.Steps.Length; Moved(p, r);
                    return "Виконано.";
                case "drop": Drop(p); return "Відкладено.";
                case "reload": Reload(); _raw = null; return "Шаблони перечитано: " + All().Count + ".";
                case "ui": DevPanel.Toggle(DevPanel.ErrandsTab); return DevPanel.Said();
            }
            return Describe(p, r) + "\nraven errand list | give <id> [hugin|munin] | hand | step | done | drop | reload";
        }
    }

    // where the player is in the game: how many of the bosses are beaten (0–7, Eikthyr … Fader). One measure for
    // which errands may come, how strong their foes are, how far they send you.
    internal static class Stage
    {
        internal static readonly string[] BossKeys = { "defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon", "defeated_goblinking", "defeated_queen", "defeated_fader" };

        internal static int Of(Player p)
        {
            int n = 0;
            foreach (string k in BossKeys) if (Has(p, k)) n++;
            return n;
        }

        // a key for the world, or (the "player keys" world setting) for this player
        internal static bool Has(Player p, string key)
        {
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key)) return true;
            return p != null && p.HaveUniqueKey(key);
        }

        internal static bool HasAll(Player p, List<string> keys)
        {
            foreach (string k in keys) if (!Has(p, k)) return false;
            return true;
        }
    }
}
#endif
