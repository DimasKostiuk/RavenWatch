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
    //   text2: …                        its words when the raven trusts you more (trust 4+); text otherwise
    //   land: Swamp                     the land it is about: it comes more often while you are there
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
        public string Id = "", Name = "", Text = "", Text2 = "", Who = "any", Kind = "normal", Group = ""; // group: blood | craft | road (Favour; empty: by its first step)
        public int PriceMin = 1, PriceMax = 1, Upto = 99, FarPrice;
        public float Weight = 1f, Within, FarAt;
        public Heightmap.Biome Land = Heightmap.Biome.None;
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
                    case "text2": cur.Text2 = v; break;
                    case "land":
                        try { cur.Land = (Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), v, true); }
                        catch { Errands.Log("errand " + cur.Id + " in " + from + ": unknown land «" + v + "»"); }
                        break;
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
                for (int k = 1; k < w.Length; k++) // @<slot>: that slot's value (a way of killing…)
                {
                    string sv;
                    if (w[k].Length > 1 && w[k][0] == '@' && Slot.TryGetValue(w[k].Substring(1), out sv)) { if (w == Def.Steps[i]) w = (string[])w.Clone(); w[k] = sv; }
                }
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
        internal static string ForceMod; // the test window: every errand given with this modifier (null: as it falls)
        internal static int HistoryKeep = 6; // the last errands done or dropped: not brought again soon

        // ---------------------------------------------------------------- templates
        private static readonly string[] Builtin =
        {
            // starters
            "[errand starter_hunt]\nname: Ловці в лісі\ntext: Одін хоче знати, чи не розучи{вся|лася} ти полювати.\ntext2: Мунін побився з Хугіном об заклад, що ти ще не розучи{вся|лася} полювати. Не підведи його.\nwho: munin\nprice: 1 2\nupto: 2\n" +
            "slot how = manner 50 bow,sneak\nstep kill Boar,Deer,Neck,Greyling,Greydwarf 40 @how\n",
            "[errand starter_trophy]\nname: Знак для Одіна\ntext: Вполюй трьох оленів і принеси ворону трофей оленя: Одін хоче бачити, що ти ходиш його лісами.\ntext2: Хугін хоче розповісти Одіну, як ти ходиш його лісами. Для розповіді бракує лише трофея оленя.\nwho: hugin\nprice: 1\nupto: 2\n" +
            "slot how = manner 40 bow,sneak\nstep kill Deer 3 @how\nstep bring TrophyDeer 1\n",
            "[errand starter_walk]\nname: Подивись, що там\ntext: Хугін бачив щось у тому боці. Сходи й глянь сам{|а}.\ntext2: Хугін бачив щось у тому боці. Каже, з тобою дивитися цікавіше. Сходи глянь.\nwho: hugin\nprice: 1 3\n" +
            "slot place = around 200 600\nstep go place 40\n",
            "[errand starter_fish]\nname: Риба для ворона\ntext: Мунін хоче риби. Свіжої. Твоєї.\ntext2: Мунін хоче риби. Каже, твоя смачніша за ту, що сама на берег вилазить.\nwho: munin\nprice: 1\n" +
            "step do Fish 2\n",
            "[errand starter_fish_big]\nname: Улов для Одіна\ntext: Мунін каже, що двох риб мало навіть для ворона. Одін хоче бачити справжній улов.\ntext2: Мунін розповідав Одіну, який з тебе рибалка. Тепер треба довести, що він не брехав.\nwho: munin\nprice: 2\n" +
            "step do Fish 10\n",
            "[errand starter_champion]\nname: Ватажок\ntext: Мунін бачив звіра, що виріс понад міру. Одін хоче, щоб його не стало.\ntext2: Мунін знайшов звіра, що виріс понад міру, і вже всім розтріпав, що ти його покладеш. Не роби з нього брехуна.\nwho: munin\nprice: 2 3\n" +
            "slot boss = champion around 300 1200\nstep kill_champion boss\n",
            "[errand starter_crypt]\nname: Старий склеп\ntext: Хугін хоче знати, чи спокійно спить той, хто лежить у склепі. Не спить — то впокой його. Зачисть це місце.\ntext2: Хугін хоче знати, чи спокійно спить той, хто лежить у склепі. Каже, з тобою туди не страшно. Майже. Зачисть це місце.\nwho: hugin\nprice: 1 3\nneeds: defeated_eikthyr\n" +
            "slot place = location Crypt,TrollCave 300 3000 fresh\nstep clear place 40\n",
            "[errand starter_runestone]\nname: Рунний камінь\ntext: На камені є слова, яких Хугін ще не читав. Дійди й прочитай їх.\ntext2: На камені є слова, яких Хугін ще не читав. Каже, читати — твоя справа, а розуміти — його.\nwho: hugin\nprice: 1 2\nfar: 2500 3\n" +
            "slot place = location Runestone 200 2500\nstep read place 30\n",
            // Hugin: the peaceful ones
            "[errand h_berries]\nname: Дари лісу\ntext: Хугін хоче, щоб ти зібра{в|ла} те, що ліс дає задарма: ягоди, гриби, квіти. Багато.\ntext2: Хугін хоче, щоб ти зібра{в|ла} те, що ліс дає задарма. Каже, хоч раз побудь корисн{им|ою} без меча.\nwho: hugin\nprice: 1 2\nupto: 3\n" +
            "step do Pick 100 forage\n",
            "[errand h_resin]\nname: Світло в домі\ntext: Одін любить, коли в домі світло. Заправ смолоскипи живицею — хоч свої, хоч чужі.\ntext2: Хугін каже, що в світлому домі навіть Мунін менше бурчить. Заправ смолоскипи живицею.\nwho: hugin\nprice: 1 2\nneeds: defeated_bonemass\n" +
            "step do Fuel 20-40 Resin\n",
            "[errand h_cook]\nname: Стіл для воїна\ntext: Хугін каже, що голодний воїн — поганий воїн. Приготуй щось путнє, і не одну страву.\ntext2: Хугін каже, що ти вже не та голодна потороча, що колись. Доведи: приготуй щось путнє, і не одну страву.\nwho: hugin\nprice: 1 2\nneeds: defeated_eikthyr\n" +
            "step do Cook 20\n",
            "[errand h_trollcave]\nname: Печера троля\ntext: Хугін бачив печеру, з якої тягне холодом і тролем. Дійди до неї й здолай того, хто там живе.\ntext2: Хугін знайшов печеру, з якої тягне тролем, і хоче знати, хто з вас двох голосніше ричить. Дійди й здолай того, хто там живе.\nwho: hugin\nprice: 2 3\nneeds: defeated_eikthyr\nwithin: 2000\n" +
            "slot place = location TrollCave 300 3000 fresh\nstep go place 25\nstep kill Troll 1 near place 150\n",
            "[errand h_smelt]\nname: Вогонь кузні\ntext: Одін хоче чути, як співає твоя плавильня. Переплав руду — багато руди.\ntext2: Хугін любить, як співає твоя плавильня. Каже, краще за Муніна. Переплав руду — багато руди.\nwho: hugin\nprice: 1 3\nneeds: defeated_gdking\n" +
            "step do Smelt 30\n",
            "[errand h_ooze]\nname: Слиз болотяний\ntext: Хугін збирає дивні речі. Цього разу йому треба болотяного слизу — і такого, що здобуто твоїми руками, а не вимінено.\ntext2: Хугін знову збирає дивні речі. Йому треба болотяного слизу, здобутого твоїми руками. Не питай навіщо.\nwho: hugin\nprice: 2\nneeds: defeated_gdking\nupto: 4\n" +
            "step loot Ooze 20\nstep bring Ooze 20\n",
            "[errand h_crystal]\nname: Холодне світло\ntext: Хугін хоче кришталю з гір: каже, в ньому видно те, чого не видно оком. Тільки той, що вибито з гори твоїми руками.\ntext2: Хугін каже, що в кришталі видно те, чого не видно оком, і хоче тобі це показати. Тільки кришталь вибий сам{|а}.\nwho: hugin\nprice: 2 3\nneeds: defeated_bonemass\nupto: 5\n" +
            "step loot Crystal 10\nstep bring Crystal 10\n",
            "[errand h_needles]\nname: Жала рівнин\ntext: Хугін хоче голок із тих комарів-вбивць. Обережно: вони кусають боляче.\ntext2: Хугін хоче голок із тих комарів-вбивць. Каже, ти вже досить товстошкір{ий|а}, щоб їх не боятися.\nwho: hugin\nprice: 2 3\nneeds: defeated_dragon\nupto: 6\n" +
            "slot how = manner 40 bow,fire,frost\nstep kill Deathsquito 10 @how\nstep bring Needle 10\n",
            "[errand h_pelts]\nname: Шкури для гнізда\ntext: Хугін хоче вистелити гніздо вовчими шкурами. Тільки не купленими — знятими з вовків твоїми руками.\ntext2: Хугін хоче вистелити гніздо вовчими шкурами, щоб тобі було де погрітися. Тільки знятими твоїми руками.\nwho: hugin\nprice: 2\nneeds: defeated_bonemass\nupto: 6\n" +
            "step loot WolfPelt 5\nstep bring WolfPelt 5\n",
            "[errand h_trophywall]\nname: Трофеї на стіну\ntext: Одін любить, коли пам'ятають, кого здолано. Вполюй звірів і повісь їхні трофеї на підставки.\ntext2: Хугін хоче, щоб на стіні висіло те, про що він потім розповідатиме. Вполюй звірів і повісь їхні свіжі трофеї на підставки.\nwho: hugin\nprice: 1\n" +
            "step do TrophyHang 3 fresh\n",
            "[errand h_mead]\nname: Мед для гостя\ntext: До тебе може зайти гість, якого ти не впізнаєш. Хай у бродильні буде мед.\ntext2: Хугін натякає, що гість, якого ти не впізнаєш, уже в дорозі. Хай у бродильні буде мед.\nwho: hugin\nprice: 1 2\nneeds: defeated_gdking\n" +
            "step do Ferment 4\n",
            "[errand h_fire]\nname: Вогонь і ворони\ntext: Хугін каже, що найкращі думки приходять уночі біля вогню. Сядь, і хай обидва ворони будуть з тобою.\ntext2: Хугін каже, що найкращі думки — біля вогню з тобою. Сядь, і хай обидва ворони будуть поруч.\nwho: hugin\nprice: 1\nupto: 2\n" +
            "step rest 120\n",
            "[errand h_fire_late]\nname: Вогонь і ворони\ntext: Хугін каже, що найкращі думки приходять уночі біля вогню. Сядь, і хай обидва ворони будуть з тобою.\ntext2: Хугін каже, що найкращі думки — біля вогню з тобою. Сядь, і хай обидва ворони будуть поруч.\nwho: hugin\nprice: 1\nneeds: defeated_bonemass\nweight: 0.15\n" +
            "step rest 120\n",
            "[errand h_tree]\nname: Вороняче дерево\ntext: Хугін чув, як десь далеко збираються ворони. Там старе дерево, і руни біля нього шепочуться. Знайди його.\ntext2: Хугін чув, як далеко збираються ворони біля старого дерева, і хоче, щоб ти це побачи{в|ла}. Знайди його.\nwho: hugin\nprice: 2 3\n" +
            "slot place = location RavenTree 300 6000\nstep go place 15\n",
            // Munin: the bloody ones
            "[errand m_forest]\nname: Тіні Чорнолісся\ntext: Мунін хоче, щоб Чорнолісся стало тихішим. На добрих кілька десятків голів.\ntext2: Мунін каже, що Чорнолісся надто розбалакалося. Ти знаєш, як його заткнути. На добрих кілька десятків голів.\nland: BlackForest\nwho: munin\nprice: 2 3\nneeds: defeated_eikthyr\nupto: 3\n" +
            "slot how = manner 60\nstep kill Greydwarf,Greydwarf_Elite,Greydwarf_Shaman,Skeleton,Troll 20-40 @how\n",
            "[errand m_troll]\nname: Голова троля\ntext: Мунін хоче бачити голову троля. Без троля — ніяк.\ntext2: Мунін хоче голову троля. Каже, повісить над гніздом і всім розповідатиме, що то він. Ти не заперечуй.\nwho: munin\nprice: 3\nneeds: defeated_eikthyr\nupto: 4\n" +
            "slot how = manner 35 stagger,bow,fire,sneak\nstep kill Troll 1 @how\nstep bring TrophyFrostTroll 1\n",
            "[errand m_swamp]\nname: Болото не спить\ntext: Мерці на болоті розлізлися. Мунін хоче, щоб їх поменшало.\ntext2: Мерці на болоті знову повилазили. Мунін каже, що з тобою їх класти — одне задоволення.\nland: Swamp\nwho: munin\nprice: 2 3\nneeds: defeated_gdking\nupto: 4\n" +
            "slot how = manner 60\nstep kill Draugr,Draugr_Elite,Blob,BlobElite,Leech,Wraith 20-40 @how\n",
            "[errand m_mountain]\nname: Вовки на вершинах\ntext: У горах стало гамірно. Мунін хоче тиші — кривавої.\ntext2: У горах знову гамірно. Мунін хоче тиші, і хоче, щоб її зроби{в|ла} саме ти.\nland: Mountain\nwho: munin\nprice: 2 3\nneeds: defeated_bonemass\nupto: 5\n" +
            "slot how = manner 60\nstep kill Wolf,Fenring,Hatchling,Ulv,StoneGolem 20-40 @how\n",
            "[errand m_plains]\nname: Гоблінський рахунок\ntext: Мунін має з гоблінами давні рахунки. Допоможи їх закрити.\ntext2: Мунін каже, що рахунки з гоблінами не закриваються ніколи. Зате з тобою рахувати весело.\nland: Plains\nwho: munin\nprice: 2 3\nneeds: defeated_dragon\nupto: 6\n" +
            "slot how = manner 60\nstep kill Goblin,GoblinBrute,GoblinShaman,Deathsquito,Lox 20-40 @how\n",
            "[errand m_blackmetal]\nname: Чорне залізо\ntext: Мунін хоче чорного металу з фулінгів. Знятого з них самих, а не з чужої скрині.\ntext2: Мунін хоче чорного металу з фулінгів. Каже, з тебе вийде непоганий злодій — тільки красти треба з них самих.\nland: Plains\nwho: munin\nprice: 2 3\nneeds: defeated_dragon\n" +
            "step loot BlackMetalScrap 20\nstep bring BlackMetalScrap 20\n",
            "[errand m_ravenkill]\nname: Кігті й дзьоб\ntext: Мунін хоче, щоб вороги падали від воронячого удару, а не від твого. Добий їх дзьобом чи кігтями.\ntext2: Мунін хоче, щоб вороги падали від його удару. Ти їх тільки підготуй — і не забирай собі останнього.\nwho: munin\nprice: 1 2\n" +
            "step kill * 10 raven\n",
            "[errand m_nests]\nname: Гнізда грейдворфів\ntext: Грейдворфи плодяться з гнізд у Чорноліссі. Мунін хоче, щоб їх поменшало — гнізд, а не грейдворфів.\ntext2: Мунін каже, що грейдворфи плодяться швидше, ніж ти їх б'єш. Тож бий гнізда. Він рахуватиме.\nland: BlackForest\nwho: munin\nprice: 1 2\nneeds: defeated_eikthyr\nupto: 3\n" +
            "step destroy Spawner_GreydwarfNest 3\n",
            "[errand m_mist]\nname: Те, що в тумані\ntext: У тумані щось ворушиться. Мунін хоче знати, чи воно помирає.\ntext2: У тумані щось ворушиться. Мунін не бачить що, і його це бісить. Піди й зроби так, щоб не ворушилось.\nland: Mistlands\nwho: munin\nprice: 2 3\nneeds: defeated_goblinking\n" +
            "slot how = manner 50\nstep kill Seeker,SeekerBrute,Tick,Gjall 10-20 @how\n",
            "[errand m_ash]\nname: Попіл і кістки\ntext: Мунін хоче почути, як тріщать обвуглені кістки. Багато кісток.\ntext2: Мунін хоче почути, як тріщать обвуглені кістки. Каже, з тобою це найкраща музика в Попелищах.\nland: AshLands\nwho: munin\nprice: 2 3\nneeds: defeated_queen\n" +
            "slot how = manner 50\nstep kill Charred_Melee,Charred_Archer,Charred_Mage,Morgen,Asksvin,FallenValkyrie 20-30 @how\n",
            "[errand m_parry]\nname: Тримай удар\ntext: Мунін хоче бачити, як ти відбиваєш удари вчасно. Не щитом махаєш — а відбиваєш.\ntext2: Мунін хоче бачити, як ти вчасно відбиваєш удари. Каже, поки ти тримаєш, він спокійно дзьобає.\nwho: munin\nprice: 1 2\n" +
            "step do Parry 10-20\n",
            "[errand m_champion_far]\nname: Далекий ватажок\ntext: Мунін бачив ватажка далеко звідси. Дорога довга, бій — ще довший.\ntext2: Мунін бачив ватажка далеко звідси й уже уявляє, як ти його покладеш. Дорога довга, бій — ще довший.\nwho: munin\nprice: 3\n" +
            "slot boss = champion around 800 2000\nstep kill_champion boss\n",
            // the later lands
            "[errand h_tame_wolf]\nname: Вовк біля вогню\ntext: Хугін хоче знати, чи вмієш ти не лише вбивати. Приручи вовка.\ntext2: Хугін каже, що вовк і ворон — давні друзі Одіна. Хай і в тебе буде свій. Приручи вовка.\nland: Mountain\nwho: hugin\nprice: 2\nneeds: defeated_bonemass\nstep do Tame 1 Wolf\n",
            "[errand h_tame_lox]\nname: Гора, що слухається\ntext: Хугін хоче побачити, як ти приручиш локса. Каже, хто вмовить гору м'яса, той умовить будь-кого.\ntext2: Хугін хоче покататися на локсі. Ну, на тобі на локсі. Приручи його.\nland: Plains\nwho: hugin\nprice: 2 3\nneeds: defeated_dragon\nstep do Tame 1 Lox\n",
            "[errand h_tame_asksvin]\nname: Вершник попелу\ntext: Хугін бачив, як Попелищами бігають асксвіни. Каже, дикий звір у тій землі — найкращий друг. Приручи одного.\ntext2: Хугін каже, що асксвін схожий на Муніна: такий самий злий і такий самий вірний, якщо прикормити. Приручи одного.\nland: AshLands\nwho: hugin\nprice: 2 3\nneeds: defeated_queen\nstep do Tame 1 Asksvin\n",
            "[errand h_field]\nname: Поле\ntext: Хугін хоче бачити не грядку, а поле. Посади, дочекайся й збери врожай — багато.\ntext2: Хугін каже, що з тебе вийшов би непоганий хлібороб, якби не меч. Посади, дочекайся й збери — поле, не грядку.\nwho: hugin\nprice: 1 2\nneeds: defeated_dragon\nstep do Pick 30 Barley,Flax,Carrot,Turnip,Onion,Kale,Oat\n",
            "[errand h_mist_shrooms]\nname: Гриби Туману\ntext: Хугін каже, що в Тумані ростуть гриби, від яких видно більше, ніж треба. Збери їх.\ntext2: Хугін хоче грибів з Туману. Каже, Мунін від них дуже смішно бачить. Збери, а він розкаже.\nland: Mistlands\nwho: hugin\nprice: 2\nneeds: defeated_goblinking\nstep do Pick 20 MushroomJotunPuffs,MushroomMagecap\n",
            "[errand h_ash_berries]\nname: Ягоди з попелу\ntext: Хугін не вірить, що в Попелищах щось росте. Доведи йому: збери, що там дає земля.\ntext2: Хугін каже, що як уже й у Попелищах щось росте, то й Мунін колись подобрішає. Збери, що там дає земля.\nland: AshLands\nwho: hugin\nprice: 2\nneeds: defeated_queen\nstep do Pick 20 Vineberry,MushroomSmokePuff,Fiddleheadfern\n",
            "[errand h_sail]\nname: Довга вода\ntext: Хугін хоче бачити море з корабля, а не з берега. Стань до стерна й веди — довго.\ntext2: Хугін каже, що з тобою за стерном навіть море не таке страшне. Веди корабель — довго.\nwho: hugin\ngroup: road\nprice: 1 2\nneeds: defeated_gdking\nstep do Sail 120\n",
            "[errand m_serpent]\nname: Морський змій\ntext: Мунін бачив у морі змія завдовжки з корабель. Убий його й принеси голову.\ntext2: Мунін каже, що морський змій — то просто довгий вугор. Доведи йому, що ні: убий і принеси голову.\nland: Ocean\nwho: munin\nprice: 3\nneeds: defeated_bonemass\nslot how = manner 40 bow,fire,frost\nstep kill Serpent 1 @how\nstep bring TrophySerpent 1\n",
            // one of two ways, a day of peace
            "[errand m_any_head]\nname: Голова для Муніна\ntext: Мунін хоче бачити, як падає щось велике. Що — йому байдуже: троль чи двоє драугрів-ватажків.\ntext2: Мунін каже, що він не перебірливий: троль чи двоє драугрів-ватажків — аби впали від твоєї руки.\nwho: munin\ngroup: blood\nprice: 2 3\nneeds: defeated_gdking\nupto: 4\nstep any kill Troll 1 | kill Draugr_Elite 2\n",
            "[errand m_any_plains]\nname: Рахунок рівнин\ntext: Мунін хоче, щоб рівнини тебе запам'ятали: три гобліни-громили або двоє локсів.\ntext2: Мунін каже, що рівнини мусять тебе боятися. Вибери сам{|а}, кого: трьох громил чи двох локсів.\nland: Plains\nwho: munin\ngroup: blood\nprice: 2 3\nneeds: defeated_dragon\nupto: 6\nstep any kill GoblinBrute 3 | kill Lox 2\n",
            "[errand h_any_feast]\nname: Стіл для Одіна\ntext: Хугін хоче, щоб Одіну було що покласти на стіл: налови риби або приготуй страв.\ntext2: Хугін каже, що Одін любить і рибу, і печене. Вибирай сам{|а}: налови риби або приготуй страв.\nwho: hugin\nprice: 1 2\nneeds: defeated_eikthyr\nstep any do Fish 5 | do Cook 15\n",
            "[errand h_any_gift]\nname: Дар у чашу\ntext: Хугін хоче покласти Одіну дар: шкури троля чи трофей вовка.\ntext2: Хугін каже, що Одін рад і шкурі, і трофею, аби від тебе. Віддай шкури троля чи трофей вовка.\nwho: hugin\nprice: 1 2\nneeds: defeated_bonemass\nupto: 5\nstep any bring TrollHide 5 | bring TrophyWolf 1\n",
            "[errand h_spare_deer]\nname: Олені Хугіна\ntext: Хугін жаліє оленів. Добу, від найближчого світанку, не вбивай жодного — ні оленя, ні кабана.\ntext2: Хугін каже, що олені вже впізнають тебе й тікають. Дай їм добу спокою — ні оленя, ні кабана.\nwho: hugin\nprice: 1\nupto: 3\nstep spare Deer,Boar 1\n",
            "[errand m_spare_greydwarf]\nname: Слово Муніна\ntext: Мунін програв грейдворфам суперечку й мусить тримати слово: добу не чіпай жодного грейдворфа.\ntext2: Мунін пообіцяв грейдворфам добу миру й уже шкодує. Не роби з нього брехуна: добу жодного грейдворфа.\nwho: munin\nprice: 1 2\nneeds: defeated_eikthyr\nupto: 3\nstep spare Greydwarf,Greydwarf_Elite,Greydwarf_Shaman 1\n",
            "[errand h_spare_lox]\nname: Мир локсам\ntext: Хугін просить за локсів: добу не вбивай жодного.\ntext2: Хугін каже, що локси — найспокійніші звірі в цих краях, і просить за них. Добу жодного локса.\nland: Plains\nwho: hugin\nprice: 1 2\nneeds: defeated_dragon\nupto: 7\nstep spare Lox 1\n",
            // Munin's beasts of the later lands
            "[errand m_fenring]\nname: Ніч фенрінґів\ntext: Мунін чув, як у горах виють фенрінґи. Уночі, коли вони найзліші, поклади п'ятьох.\ntext2: Мунін каже, що фенрінґи виють фальшиво, і хоче, щоб ти їх заткну{в|ла}. Уночі, п'ятьох.\nland: Mountain\nwho: munin\nprice: 2 3\nneeds: defeated_bonemass\nupto: 6\nslot how = manner 40 bow,fire,stagger\nstep kill Fenring 5 night @how\n",
            "[errand m_golem]\nname: Кам'яне серце\ntext: Мунін хоче почути, як тріскає камінь. Розбий двох кам'яних ґолемів.\ntext2: Мунін каже, що кам'яні ґолеми — просто скелі, які загордилися. Нагадай двом із них, хто тут головний.\nland: Mountain\nwho: munin\nprice: 2 3\nneeds: defeated_bonemass\nupto: 6\nstep kill StoneGolem 2\n",
            "[errand m_frostcave]\nname: Морозні печери\ntext: У морозних печерах засіли культисти з ульвами. Мунін хоче, щоб їх поменшало: шестеро ульвів або двоє культистів.\ntext2: Мунін каже, що культисти моляться не тому, кому треба. Поясни їм: шестеро ульвів або двоє культистів.\nland: Mountain\nwho: munin\ngroup: blood\nprice: 2 3\nneeds: defeated_bonemass\nupto: 6\nstep any kill Ulv 6 | kill Fenring_Cultist 2\n",
            "[errand m_abomination]\nname: Гидь з болота\ntext: На болоті з коріння піднялася гидь. Мунін хоче бачити, як вона падає.\ntext2: Мунін каже, що гидь — це болото, яке забагато про себе думає. Поклади її.\nland: Swamp\nwho: munin\nprice: 2 3\nneeds: defeated_gdking\nupto: 5\nslot how = manner 40 fire,stagger,bow\nstep kill Abomination 1 @how\n",
            "[errand m_seekers]\nname: Солдати шукачів\ntext: У Тумані ходять шукачі-солдати. Мунін хоче, щоб їх поменшало, і щоб ти не ста{в|ла} їхньою вечерею.\ntext2: Мунін каже, що шукачі-солдати — то просто великі таргани. Розчави кількох.\nland: Mistlands\nwho: munin\nprice: 2 3\nneeds: defeated_goblinking\nslot how = manner 50 stagger,fire,frost,magic,bow\nstep kill SeekerBrute 3-5 @how\n",
            "[errand m_gjall]\nname: Ґьялл над Туманом\ntext: Мунін бачив ґьялла — ту летючу торбу з кислотою. Збий двох або принеси його трофей, якщо вже маєш.\ntext2: Мунін каже, що ґьялл літає гірше за нього, а гордиться більше. Збий двох або принеси трофей.\nland: Mistlands\nwho: munin\ngroup: blood\nprice: 3\nneeds: defeated_goblinking\nstep any kill Gjall 2 | bring TrophyGjall 1\n",
            "[errand m_valkyrie]\nname: Занепала валькірія\ntext: Над Попелищами кружляють занепалі валькірії. Поклади двох або принеси Муніну трофей однієї.\ntext2: Мунін каже, що валькірії колись носили воїнів до Одіна, а тепер тільки верещать. Поклади двох або принеси трофей.\nland: AshLands\nwho: munin\ngroup: blood\nprice: 3\nneeds: defeated_queen\nstep any kill FallenValkyrie 2 | bring TrophyFallenValkyrie 1\n",
            "[errand m_bonemaw]\nname: Кісткозмій\ntext: У гарячому морі біля Попелищ плаває кісткозмій. Мунін хоче двох таких або трофей одного.\ntext2: Мунін каже, що кісткозмій смачно хрумтить, коли падає. Поклади двох або принеси трофей.\nland: Ocean\nwho: munin\ngroup: blood\nprice: 3\nneeds: defeated_queen\nstep any kill BonemawSerpent 2 | bring TrophyBonemawSerpent 1\n",
            "[errand m_ash_beasts]\nname: Звірі Попелищ\ntext: Мунін хоче, щоб у Попелищах стало тихіше: двоє морґенів або шестеро вулкальників.\ntext2: Мунін каже, що морґени страшні лише на вигляд. Перевір: двоє морґенів або шестеро вулкальників.\nland: AshLands\nwho: munin\ngroup: blood\nprice: 2 3\nneeds: defeated_queen\nstep any kill Morgen 2 | kill Volture 6\n",
            // Odin's feather carried
            "[errand h_carry_stone]\nname: Перо до каменя\ntext: Одін хоче, щоб перо його ворона лягло біля рунного каменя. Візьми його в чаші гнізда й донеси — пішки, бо крізь портал воно не піде.\ntext2: Хугін каже, що довірив би Одінове перо лише тобі. Візьми його в чаші гнізда й донеси до рунного каменя — без порталів.\nwho: hugin\ngroup: road\nprice: 2 3\nslot place = location Runestone 300 1500\nstep carry place 8\n",
            "[errand m_carry_far]\nname: Перо в чужий край\ntext: Мунін хоче, щоб Одінове перо лежало далеко звідси, там, куди він сам лінується летіти. Візьми його в чаші гнізда й донеси. Портал перо не пустить.\ntext2: Мунін каже, що з тобою Одінове перо хоч світ побачить. Візьми його в чаші гнізда й донеси, куди сказано, — своїми ногами.\nwho: munin\ngroup: road\nprice: 2 3\nslot place = around 600 1500\nstep carry place 8\n",
            "[errand golden_feather]\nname: Перо для ватажка\ntext: Одін кличе сам. Донеси перо його ворона туди, де звір виріс понад міру, і поклади його на землю. Звір прийде на нього.\nwho: any\nkind: golden\nslot place = around 500 1500\nslot boss = champion near place 15 30\nstep carry place 8\nstep kill_champion boss\nmaymods: nodeath 30\n",
            // a watch kept at night
            "[errand m_hold]\nname: Нічна варта\ntext: Мунін хоче побачити, як ти тримаєш коло вночі. Стань біля рунного кілка й вартуй, а він подивиться, хто прийде.\ntext2: Мунін каже, що з тобою й ніч не така довга. Стань у рунне коло й вартуй — він уже кличе гостей.\nwho: munin\ngroup: blood\nprice: 2 3\nneeds: defeated_eikthyr\nslot place = around 150 500\nstep hold place 180 10\n",
            "[errand h_hold_stone]\nname: Варта біля каменя\ntext: Хугін каже, що вночі біля рунного каменя ходять ті, кому не слід. Постій там на варті.\ntext2: Хугін каже, що камінь уночі шепоче, а слухати його краще удвох. Постій на варті біля нього.\nwho: hugin\ngroup: blood\nprice: 2 3\nneeds: defeated_eikthyr\nslot place = location Runestone 200 1500\nstep hold place 240 12\n",
            // a raven leads you
            "[errand h_follow_stone]\nname: Хугін знає дорогу\ntext: Хугін знайшов рунний камінь, якого ти ще не бачи{в|ла}, і поведе тебе до нього сам. Іди за ним і прочитай, що там написано.\ntext2: Хугін каже, що ти ходиш як заблуканий віл, тож цього разу дорогу покаже він. Іди за ним до рунного каменя.\nwho: hugin\ngroup: road\nprice: 2 3\nslot place = location Runestone 300 900\nslot path = route place\nstep follow path\nstep read place 30\n",
            "[errand m_follow_champion]\nname: Мунін веде на звіра\ntext: Мунін вистежив звіра, що виріс понад міру. Він поведе тебе до нього — не відставай.\ntext2: Мунін каже, що знайшов тобі здобич до пари. Іди за ним і не відставай — він не чекатиме вічно.\nwho: munin\ngroup: blood\nprice: 2 3\nslot path = route 300 600\nslot boss = champion near path 15 30\nstep follow path\nstep kill_champion boss\n",
            // Hugin's late lands (2026-10-25: Mistlands and Ashlands were thin for him)
            "[errand h_mist_sap]\nname: Сік старих коренів\ntext: Хугін чув, що в Тумані зі старих коренів сочиться сік. Добудь його й принеси в чашу гнізда.\ntext2: Хугін каже, що сік із туманних коренів пахне так, ніби дерево пам'ятає більше за нього. Добудь і принеси.\nland: Mistlands\nwho: hugin\nprice: 2\nneeds: defeated_goblinking\nstep loot Sap 10\nstep bring Sap 10\n",
            "[errand h_spare_dvergr]\nname: Мир із дверграми\ntext: Хугін просить за двергрів: добу, від найближчого світанку, не чіпай жодного.\ntext2: Хугін каже, що двергри — єдині в Тумані, з ким можна поговорити. Дай їм добу спокою.\nland: Mistlands\nwho: hugin\nprice: 1 2\nneeds: defeated_goblinking\nstep spare Dverger,DvergerMage,DvergerMageFire,DvergerMageIce,DvergerMageSupport 1\n",
            "[errand h_ash_wood]\nname: Дерево з попелу\ntext: Хугін хоче на гніздо дерева, що виросло в Попелищах і не згоріло. Нарубай сам{|а} й принеси в чашу.\ntext2: Хугін каже, що дерево, яке не згоріло в Попелищах, — найкраще для гнізда. Нарубай і принеси.\nland: AshLands\nwho: hugin\nprice: 2\nneeds: defeated_queen\nstep loot Blackwood 30\nstep bring Blackwood 30\n",
            "[errand h_spare_asksvin]\nname: Мир попелозаврам\ntext: Хугін просить за попелозаврів: добу, від найближчого світанку, не вбивай жодного.\ntext2: Хугін каже, що попелозаври дивляться на нього, як на рівню. Дай їм добу спокою.\nland: AshLands\nwho: hugin\nprice: 1 2\nneeds: defeated_queen\nstep spare Asksvin 1\n",
            // the Deep North (2026-10-25)
            "[errand m_jotun]\nname: Криґні на снігу\ntext: Мунін хоче побачити, як падають криґні. Велике падає гучно.\ntext2: Мунін каже, що криґні високі лише доти, доки стоять. Поклади кількох.\nland: DeepNorth\nwho: munin\nprice: 2 3\nneeds: defeated_fader\nslot how = manner 50 stagger,fire,bow,magic,sneak\nstep kill JotunWarrior,JotunWarriorDualWield 3-5 @how\n",
            "[errand m_frosttroll]\nname: Троль із Півночі\ntext: Мунін бачив тролів, що вросли в кригу. Одін хоче, щоб вони більше не вставали.\ntext2: Мунін каже, що північні тролі — ті самі лісові, тільки замерзлі й ще дурніші. Поклади кількох.\nland: DeepNorth\nwho: munin\nprice: 2 3\nneeds: defeated_fader\nslot how = manner 40 stagger,fire,bow,magic\nstep kill TrollFrost 2-3 @how\n",
            "[errand m_north_beast]\nname: Звір під кригою\ntext: Мунін хоче голову великого північного звіра. Котрого — вирішиш сам{|а}.\ntext2: Мунін каже, що на Півночі під снігом сидить таке, від чого навіть йому лячно. Тим більше хоче його голову.\nland: DeepNorth\nwho: munin\ngroup: blood\nprice: 3\nneeds: defeated_fader\nstep any kill Elaking 1 | kill Writhan 2 | bring TrophyElaking 1\n",
            "[errand m_north_dead]\nname: Мерзлі мерці\ntext: Мунін хоче, щоб на Півночі стало менше тих, хто не вміє лежати тихо: десятеро кістяків або восьмеро гоблінів.\ntext2: Мунін каже, що мерзлим мерцям однаково холодно — хоч живим, хоч мертвим. Допоможи їм визначитися.\nland: DeepNorth\nwho: munin\ngroup: blood\nprice: 2 3\nneeds: defeated_fader\nstep any kill Skeleton_DeepNorth 10 | kill GoblinDeepNorth 8\n",
            "[errand m_witch]\nname: Гекса\ntext: Мунін ненавидить гекс: кажуть, вони перекривлюють воронів. Поклади двох або принеси трофей.\ntext2: Мунін каже, що гекса каркає гірше за нього, а гордиться більше. Поклади двох або принеси трофей.\nland: DeepNorth\nwho: munin\ngroup: blood\nprice: 3\nneeds: defeated_fader\nstep any kill JotunWitch 2 | bring TrophyJotunWitch 1\n",
            "[errand m_hold_north]\nname: Варта в заметіль\ntext: Мунін хоче, щоб ти постоя{в|ла} в рунному колі серед північної ночі. Хто прийде з заметілі — того й бий.\ntext2: Мунін каже, що з тобою й північна ніч не така довга. Стань у коло — він уже кличе гостей із заметілі.\nland: DeepNorth\nwho: munin\ngroup: blood\nprice: 3\nneeds: defeated_fader\nslot place = around 150 500 in:DeepNorth\nstep hold place 180 10\n",
            "[errand h_lingon]\nname: Брусниця з-під снігу\ntext: Хугін каже, що на Півночі під снігом ростуть кислі червоні ягоди. Збери їх.\ntext2: Хугін каже, що брусниця з-під снігу — єдине, що на Півночі не намагається тебе вбити. Збери її.\nland: DeepNorth\nwho: hugin\nprice: 2\nneeds: defeated_fader\nstep do Pick 30 Lingonberry\n",
            "[errand h_frostcore]\nname: Морозні ядра\ntext: Хугін бачив, як на Півночі висять ядра, у яких спить мороз. Зніми кілька.\ntext2: Хугін каже, що морозне ядро холодне, як Мунінові жарти. Зніми кілька, а він подивиться.\nland: DeepNorth\nwho: hugin\nprice: 2 3\nneeds: defeated_fader\nstep do Pick 3-5 FrostCore\n",
            "[errand h_tame_moose]\nname: Лось біля вогню\ntext: Хугін хоче, щоб на Півночі в тебе був друг, а не тільки вороги. Приручи лося.\ntext2: Хугін каже, що лось і ворон добре мовчать разом. Приручи лося.\nland: DeepNorth\nwho: hugin\nprice: 2\nneeds: defeated_fader\nstep do Tame 1 Moose\n",
            "[errand h_spare_seal]\nname: Мир ластоногам\ntext: Хугін просить за ластоногів: добу, від найближчого світанку, не вбивай жодного.\ntext2: Хугін каже, що ластоноги смішніші за Муніна і значно чемніші. Дай їм добу спокою.\nland: DeepNorth\nwho: hugin\nprice: 1 2\nneeds: defeated_fader\nstep spare Seal,Seal_Pup 1\n",
            "[errand h_north_wood]\nname: Мерзле дерево\ntext: Хугін хоче на гніздо дерева з Півночі — того, що не боїться морозу. Нарубай сам{|а} й принеси в чашу.\ntext2: Хугін каже, що в мерзлому дереві гніздо не промерзне навіть у заметіль. Нарубай і принеси.\nland: DeepNorth\nwho: hugin\nprice: 2\nneeds: defeated_fader\nstep loot Frostwood 30\nstep bring Frostwood 30\n",
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
            "[errand golden_jotuns]\nname: Криґень і гекса\ntext: Одін кличе сам. На Півночі криґень і гекса виросли понад міру і ходять разом. Здолай обох.\nland: DeepNorth\nwho: any\nkind: golden\nneeds: defeated_fader\n" +
            "slot a = champion around 300 1200 who:JotunWarrior in:DeepNorth\nslot b = champion near a 20 40 who:JotunWitch\nstep kill_champion a,b\nmaymods: nodeath 30\n",
            "[errand golden_trail]\nname: Хугінів слід до скарбу\ntext: Одін кличе сам. Хугін натрапив на старий слід: прикмета за прикметою, а наприкінці — чиясь схованка. Іди за ним. Хто ховав — той, може, досі стереже.\nwho: hugin\nkind: golden\nmaymods: nodeath 30\n" +
            "slot l1 = route 200 300\nslot l2 = route 200 300 from:l1\nslot l3 = route 200 300 from:l2\nslot keeper = champion near l3 8 14 lord title:Вартовий_схованки\n" +
            "step follow l1\nstep clue l1 any\nstep follow l2 auto\nstep clue l2 any\nstep follow l3 auto\nstep cache l3\nstep kill_champion keeper guard l3\n",
            "[errand golden_siege]\nname: Облога гнізда\ntext: Одін кличе сам. Звірі зачули, що в твоєму гнізді сидять Одінові ворони. Уночі вони прийдуть по нього — три хвилі, і з останньою їхній ватажок. Стань біля гнізда й не дай його розламати.\nwho: any\nkind: golden\nmaymods: nodeath 30\n" +
            "slot nest = nest\nstep siege nest\n",
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
            if (p == null || p.m_customData == null || !p.m_customData.TryGetValue(Key, out raw)) raw = ""; // (no data yet while the character loads)
            long world = ZNet.instance != null && ZNet.World != null ? ZNet.instance.GetWorldUID() : 0L; // (no world yet while it loads)
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
        internal static List<ErrandDef> Fitting(Player p, bool munin, bool golden) { return Fitting(p, munin, golden, true, false); }

        // ahead: instead, those of the land after the one you open (Ahead)
        private static List<ErrandDef> Fitting(Player p, bool munin, bool golden, bool notRecent, bool ahead)
        {
            List<string> recent = notRecent ? History(p) : new List<string>();
            List<ErrandDef> l = new List<ErrandDef>();
            foreach (ErrandDef d in All())
            {
                if (d.Golden != golden || d.Trial) continue;
                if (d.Who == "hugin" && munin || d.Who == "munin" && !munin) continue;
                if (recent.Contains(d.Id)) continue;
                if ((ahead ? !Ahead(p, d) : !Stage.HasAll(p, d.Needs)) || Stage.Of(p) > d.Upto) continue;
                if (d.Land == Heightmap.Biome.DeepNorth && Stage.NorthKey.Length == 0) continue; // (a game without the Deep North)
                if (Fishes(d) && !HasRod(p)) continue; // (no rod yet: Haldor sells it)
                Heightmap.Biome only = InLand(d);
                if (only != Heightmap.Biome.None && (p == null || p.GetCurrentBiome() != only)) continue; // (its place is found only in that land: given while you are there)
#if RAVEN_STORY
                if (!StoryCond.All(p, d.When)) continue;
#endif
                l.Add(d);
            }
            return l;
        }

        // a step that is fishing itself (a choice of «any» has another way: not this)
        private static bool Fishes(ErrandDef d)
        {
            foreach (string[] s in d.Steps) if (s.Length > 1 && s[0] == "do" && string.Equals(s[1], "Fish", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool HasRod(Player p)
        {
            if (p == null || p.GetInventory() == null) return false;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name == "FishingRod") return true;
            return false;
        }

        // an errand of the land after the one you open: about a land (not the sea), short of only the next boss. Such one
        // comes this rarely, for a notch more
        internal static float AheadChance = 0.1f;
        internal static bool Ahead(Player p, ErrandDef d)
        {
            if (d.Golden || d.Land == Heightmap.Biome.None || d.Land == Heightmap.Biome.Ocean || LandOf(d) > Stage.Land(p) + 1) return false;
            // (only what is done in that land itself: sparing its beasts, or a watch, a feather, a guide found round you,
            // could be done without going there — a free notch more)
            foreach (string[] s in d.Steps) if (s.Length > 0 && (s[0] == "spare" || s[0] == "hold" || s[0] == "carry" || s[0] == "follow")) return false;
            string missing = null;
            foreach (string k in d.Needs) if (!Stage.Has(p, k)) { if (missing != null) return false; missing = k; }
            int i = missing != null ? Stage.IndexOf(missing) : -1;
            return i >= 0 && i <= Stage.Of(p); // (the next boss; or one skipped while later ones fell)
        }

        // the land a slot's place must be in (in:<Biome>), None when any
        internal static Heightmap.Biome InLand(ErrandDef d)
        {
            foreach (string[] s in d.Slots) { Heightmap.Biome b = ErrandSteps.InOf(s); if (b != Heightmap.Biome.None) return b; }
            return Heightmap.Biome.None;
        }

        // the land an errand is about: its own, else the land after its last boss; 1 (Meadows) without either
        internal static int LandOf(ErrandDef d)
        {
            int land = d.Land != Heightmap.Biome.Ocean ? Stage.LandOf(d.Land) : 0;
            if (land > 0) return land;
            foreach (string k in d.Needs) land = Mathf.Max(land, Stage.IndexOf(k) + 2);
            return Mathf.Max(1, land);
        }

        // more than «kill, bring, pick»: a choice, a way of killing, a watch, a guide, a champion, a beast to tame…
        internal static bool Curious(ErrandDef d)
        {
            foreach (string[] s in d.Slots) if (s.Length > 1 && s[1] == "manner") return true;
            foreach (string[] s in d.Steps)
            {
                if (s.Length == 0) continue;
                if (s[0] == "any" || s[0] == "spare" || s[0] == "carry" || s[0] == "hold" || s[0] == "follow" || s[0] == "kill_champion" || s[0] == "riddle") return true;
                if (s[0] == "do" && s.Length > 1 && (s[1] == "Tame" || s[1] == "Sail")) return true;
            }
            return false;
        }

        // one of them at random (by weight); null when none fits. noServer: only those that need no place of the world
        // raven: the one that brings it, wherever it sits (null: the worn one)
        internal static ErrandDef Pick(Player p, bool munin, bool golden) { return Pick(p, munin, golden, false, null); }

        internal static ErrandDef Pick(Player p, bool munin, bool golden, ItemDrop.ItemData raven) { return Pick(p, munin, golden, false, raven); }

        internal static ErrandDef Pick(Player p, bool munin, bool golden, bool noServer, ItemDrop.ItemData raven)
        {
            if (raven == null) raven = Trust.Worn(munin);
            int level = raven != null ? Trust.Level(raven) : Trust.LevelWorn(munin);
            Norov.Character c = Norov.CharOf(raven);
            Norov.Quirk q = Norov.QuirkOf(raven);
            string ch = c != null ? c.Id : "", qu = q != null ? q.Id : "";
            float shows = Norov.Shows(level);
            List<ErrandDef> l = null;
            if (!golden && UnityEngine.Random.value < AheadChance) { l = Fitting(p, munin, false, true, true); if (noServer) l.RemoveAll(NeedsServer); }
            if (l == null || l.Count == 0) { l = Fitting(p, munin, golden); if (noServer) l.RemoveAll(NeedsServer); }
            if (l.Count == 0) { l = Fitting(p, munin, golden, false, false); if (noServer) l.RemoveAll(NeedsServer); } // all of them done lately: one again rather than none
            float sum = 0f;
            // (Odin's favour leans the weights: Favour)
            float[] w = new float[l.Count];
            for (int i = 0; i < l.Count; i++) { w[i] = Weigh(p, l[i], level) * Norov.Mul(Lean(ch, Bent(l[i])), shows) * Habit(qu, l[i]); sum += w[i]; }
            float at = UnityEngine.Random.value * sum;
            for (int i = 0; i < l.Count; i++) { at -= w[i]; if (at <= 0f) return l[i]; }
            return l.Count > 0 ? l[l.Count - 1] : null;
        }

        // its weight now: the template's, Odin's favour, more while you stand in the land it is about, and by the raven's
        // trust: the more it trusts you, the more it brings errands of the land you open and the curious ones (Stage.Fresh;
        // a curious one about no land is never old)
        internal static float LandMul = 2.5f;
        private static float Weigh(Player p, ErrandDef d, int level)
        {
            float w = Mathf.Max(0.01f, d.Weight) * Favour.Mul(p, d);
            bool curious = Curious(d);
            int age = d.Land == Heightmap.Biome.None && curious ? 0 : Stage.Land(p) - LandOf(d);
            w *= Stage.Fresh(age, level, curious);
            return d.Land != Heightmap.Biome.None && p != null && p.GetCurrentBiome() == d.Land ? w * LandMul : w;
        }

        // ---------------------------------------------------------------- the raven's heart in the draw (Norov)
        // what an errand is, roughly: champion, watch, gentle (spare, tame), road (go, follow, carry, read, sail),
        // fire (rest), craft (gather, cook, smelt…), hunt (the rest: kill, clear, destroy)
        internal static string Bent(ErrandDef d)
        {
            bool champion = false, watch = false, road = d.Group == "road", fire = false, craft = false, gentle = false;
            foreach (string[] w in Ways(d))
            {
                switch (w[0])
                {
                    case "kill_champion": champion = true; break;
                    case "hold": watch = true; break;
                    case "spare": gentle = true; break;
                    case "go": case "follow": case "carry": case "read": road = true; break;
                    case "rest": fire = true; break;
                    case "loot": case "bring": craft = true; break;
                    case "do":
                        string what = w.Length > 1 ? w[1] : "";
                        if (what == "Tame") gentle = true; else if (what == "Sail") road = true; else if (what != "Parry") craft = true;
                        break;
                }
            }
            return champion ? "champion" : watch ? "watch" : gentle ? "gentle" : road ? "road" : fire ? "fire" : craft ? "craft" : "hunt";
        }

        // every way its steps are done, each as its words: a step, or each choice of an «any» (kill … | bring …)
        private static List<string[]> Ways(ErrandDef d)
        {
            List<string[]> l = new List<string[]>();
            foreach (string[] s in d.Steps)
            {
                if (s.Length == 0) continue;
                if (s[0] != "any") { l.Add(s); continue; }
                List<string> part = new List<string>();
                for (int i = 1; i <= s.Length; i++)
                {
                    if (i < s.Length && s[i] != "|") { part.Add(s[i]); continue; }
                    if (part.Count > 0) l.Add(part.ToArray());
                    part = new List<string>();
                }
            }
            return l;
        }

        // what a character brings more, or less (Norov.Characters; shown by trust: Norov.Shows)
        private static float Lean(string character, string bent)
        {
            switch (character + "/" + bent)
            {
                case "bold/champion": return 1.6f;
                case "bold/hunt": case "bold/watch": return 1.3f;
                case "bold/gentle": return 0.7f;
                case "wanderer/road": return 1.6f;
                case "wanderer/champion": return 1.1f;
                case "wanderer/fire": case "wanderer/craft": return 0.7f;
                case "homebody/fire": return 1.5f;
                case "homebody/craft": return 1.4f;
                case "homebody/road": return 0.7f;
                case "fidget/hunt": case "fidget/road": return 1.2f;
                case "fidget/fire": return 0.6f;
                case "fidget/watch": return 0.8f;
                case "calm/fire": return 1.3f;
                case "calm/craft": case "calm/gentle": return 1.2f;
                case "calm/champion": return 0.8f;
                case "watchful/watch": return 1.5f;
                case "watchful/road": return 1.2f;
                case "devoted/gentle": return 1.3f;
                case "devoted/fire": return 1.2f;
            }
            return 1f;
        }

        // what a quirk brings more, or less (Norov.QuirkList; always, whatever the trust)
        private static float Habit(string quirk, ErrandDef d)
        {
            string bent = Bent(d);
            switch (quirk)
            {
                case "sleepy": return bent == "watch" ? 0.5f : bent == "fire" ? 1.3f : 1f;
                case "timid": return bent == "champion" ? 0.4f : 1f;
                case "showoff": return bent == "champion" ? 1.4f : 1f;
                case "chilly": return d.Land == Heightmap.Biome.Mountain || d.Land == Heightmap.Biome.DeepNorth ? 0.7f : 1f;
                case "wet": return d.Land == Heightmap.Biome.Ocean || bent == "road" ? 1.2f : 1f;
                case "magpie": case "thief":
                    foreach (string[] w in Ways(d)) if (w[0] == "bring" || w[0] == "loot") return 1.3f;
                    return 1f;
            }
            return 1f;
        }

        // the errand's words as shown: warmer once its raven trusts you (text2), and the way of killing it asks for
        internal static string TextOf(ErrandRun r)
        {
            if (r == null || r.Def == null) return "";
            string t = r.Def.Text2.Length > 0 && Tone.TierOf(Trust.LevelWorn(r.Munin)) >= 2 ? r.Def.Text2 : r.Def.Text;
            t = t.Length > 0 ? Lang.T(t) : "";
            foreach (string[] s in r.Def.Slots)
            {
                string v;
                if (s.Length < 2 || s[1] != "manner" || !r.Slot.TryGetValue(s[0], out v) || !ErrandHow.Is(v)) continue;
                t = (t.Length > 0 ? t + " " : "") + ErrandHow.Line(v, r.Munin);
            }
            return t;
        }

        // location … near <slot>: the ring round that slot's place
        private static string NearOf(string[] s) { int i = Array.IndexOf(s, "near"); return i > 4 && i + 1 < s.Length ? s[i + 1] : ""; }

        internal static bool NeedsServer(ErrandDef d) { foreach (string[] s in d.Slots) if (s.Length > 1 && s[1] == "location") return true; return false; }

        // Given at once, or (a place of the world in it) once the server says where: null then too. A place nobody
        // could find: another errand of the same kind that needs none, if there is one. failed: told when nothing came.
        internal static string Give(Player p, ErrandDef d, bool munin) { return Give(p, d, munin, null, null); }

        internal static string Give(Player p, ErrandDef d, bool munin, Action<string> failed) { return Give(p, d, munin, failed, null); }

        internal static string Give(Player p, ErrandDef d, bool munin, Action<string> failed, ItemDrop.ItemData raven)
        {
            if (p == null || d == null) return Lang.T("Нема такого доручення.");
            if (Of(p) != null) return Lang.T("Спершу закінч або відклади те доручення, що вже маєш.");
            if (!NeedsServer(d))
            {
                string w = GiveNow(p, d, munin, null);
                if (w == null || Of(p) != null) return w;
                ErrandDef other = Pick(p, munin, d.Golden, true, raven); // (a slot it could not fill here — no nest: another of its kind)
                return other != null && other != d && !NeedsServer(other) ? GiveNow(p, other, munin, null) : w;
            }
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
                    ErrandDef other = Pick(me, munin, d.Golden, true, raven);
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
            float far = 0f; // how far it sends you: its farthest place from where you are now («Пішки», «Обітниця Одіна»)
            foreach (string v in r.Slot.Values)
            {
                Vector3 at;
                if (ErrandSteps.Point(v, out at)) far = Mathf.Max(far, new Vector2(at.x - p.transform.position.x, at.z - p.transform.position.z).magnitude);
            }
            if (far > 0f) r.SetMark(ErrandMods.FarKey, Mathf.RoundToInt(far).ToString(CultureInfo.InvariantCulture));
            r.Mods.AddRange(ErrandMods.Roll(d, p, far));
            if (!string.IsNullOrEmpty(ForceMod) && ErrandMods.Known(ForceMod) && !r.Mods.Contains(ForceMod)) // (the test window's choice)
            {
                r.Mods.Add(ForceMod);
                ErrandMods.Tidy(r.Mods);
            }
            if (d.FarAt > 0f && d.FarPrice > 0)
                foreach (string v in r.Slot.Values)
                {
                    Vector3 at;
                    if (ErrandSteps.Point(v, out at) && new Vector2(at.x, at.z).magnitude >= d.FarAt) { r.Price = Mathf.Max(r.Price, d.FarPrice); break; }
                }
            bool ahead = !Stage.HasAll(p, d.Needs) && Ahead(p, d); // (one of the land after the one you open; not one the test window forced)
            if (ahead) r.Price = Mathf.Min(9, r.Price + 1);
            r.Build();
            if (r.Mods.Contains("sparktest")) // its time, from the steps now built; one that would need too long becomes a vow (far off) or nothing
            {
                double t = ErrandMods.TrialSeconds(r, p.transform.position);
                if (t <= ErrandMods.TrialMax) r.SetMark(ErrandMods.LimitKey, Mathf.RoundToInt((float)t).ToString(CultureInfo.InvariantCulture));
                else
                {
                    r.Mods.Remove("sparktest");
                    if (far >= ErrandMods.Far) { r.Mods.Add("vow"); ErrandMods.Tidy(r.Mods); }
                    Log("errand " + d.Id + ": the trial of the spark would need " + Mathf.RoundToInt((float)t / 60f) + " min — " + (r.Mods.Contains("vow") ? "a vow instead" : "none"));
                }
            }
            Save(p, r);
            if (!d.Trial) Favour.Spent(p, d); // the favour that leaned Odin to it is used up
            Start(p, r);
            Log("given " + d.Id + " by " + (munin ? "Munin" : "Hugin") + " to " + p.GetPlayerName() + " (" + p.GetPlayerID() + "), price " + r.Price + ", stage " + Stage.Of(p) + (ahead ? ", from the land ahead" : "") + (r.Mods.Count > 0 ? ", mods " + string.Join("+", r.Mods.ToArray()) : ""));
            if (d.Trial) { RavenMsg.Center(p, Lang.F("Одін дав випробування «{0}» і книгу воронів. Що треба — у книзі (B), «Шлях Одіна».", Lang.T(d.Name))); return null; }
            RavenMsg.Center(p, ahead ? Lang.F("{0} приніс доручення від Одіна: «{1}» — з дальшої землі, тому й ціна вища", Plugin.RavenName(munin), Lang.T(d.Name))
                : Lang.F("{0} приніс доручення від Одіна: «{1}»", Plugin.RavenName(munin), Lang.T(d.Name)));
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
            string broken;
            if (ErrandMods.Breaks(p, r, type, out broken)) Save(p, r);
            if (broken != null) { Fail(p, r, broken); return; }
            try { if (r.Current.OnEvent(r, r.Step, type, arg, value)) Moved(p, r); }
            catch (Exception e) { Log("event " + type + ": " + e.Message); }
        }

        // a trophy picked up fresh off a kill (fell at world time 'at'): one more of its kind the errand may see hung
        // (DoStep TrophyHang fresh) — only one won since the errand was given
        internal static void OnFreshTrophy(string prefab, double at)
        {
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            if (r == null || r.Def == null || r.Finished || string.IsNullOrEmpty(prefab) || at + 1.0 < r.Given) return;
            r.SetMark(DoStep.FreshMark, DoStep.AddFresh(r.Mark(DoStep.FreshMark), prefab, 1));
            Save(p, r);
        }

        // a champion of yours died (wherever it was simulated)
        internal static void OnChampion(string stamp)
        {
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            if (r == null || r.Def == null) return;
            ChampionStep s = r.Current as ChampionStep;
            if (s != null && s.Killed(r, r.Step, stamp)) Moved(p, r);
            SiegeStep sg = r.Current as SiegeStep;
            if (sg != null) sg.Killed(r, stamp);
        }

        // a champion of this errand stamp is still wanted (its errand goes on)
        internal static bool Wants(Player p, string stamp)
        {
            ErrandRun r = Of(p);
            if (r == null || r.Def == null) return false;
            for (int i = r.Step; i < r.Steps.Length; i++) { ChampionStep c = r.Steps[i] as ChampionStep; if (c != null && c.Wants(r, i, stamp)) return true; }
            SiegeStep sg = r.Current as SiegeStep;
            if (sg != null && sg.Wants(stamp)) return true;
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
            RuneRing.Tick();        // a step's ring or stake no step asks for any more: gone
            HoldWaves.Late();       // a watch's beasts no watch keeps: gone
            SiegeWaves.Late();      // a siege's beasts no siege keeps: gone
            FeatherTick();          // Odin's feathers no errand wants: gone
            DestroyWatch.Tick();    // things you broke for an errand: gone yet?
            ErrandTalk.Tick();
            RavenTree.MapTick();    // the found raven trees' marks
            Nest.Tick();            // ravens in a nest waiting for Odin, his glow
            Trial.Tick();           // Odin coming to one with no ravens
            Requests.Tick();        // the ravens' own wishes
            Words.Tick();           // and the words you taught them
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            if (r != null && r.Def != null && r.Finished && p != null) { Log(r.Id + ": every step done but not counted (its template has fewer steps now): done"); Complete(p, r); return; }
            if (r == null || r.Def == null || r.Current == null) { if (_pins.Count > 0) ClearPins(); ErrandSpot.Tick(p, null); return; }
            if (r.Def.Trial && Trial.TimeLeft(r) <= 0f) { Fail(p, r, Lang.T("час вийшов")); return; }
            if (ErrandMods.Tick(p, r)) Save(p, r); // (a modifier broken: «Без подряпин», «Поспіх»)
            try { if (r.Current.Tick(r, r.Step, p)) Moved(p, r); }
            catch (Exception e) { Log("tick " + r.Id + ": " + e.Message); }
            if (r.Current != null) Zone(p, r);
            ErrandSpot.Tick(p, r); // (Hugin marks the place: B3)
            if (r.Current != null) Pins(r);
        }

        // Odin's feathers in the bag while no step carries one (done, lost, given up; one found in an old grave)
        private static float _featherNext;
        private static void FeatherTick()
        {
            if (Time.time < _featherNext) return;
            _featherNext = Time.time + 3f;
            Player p = Player.m_localPlayer;
            ErrandRun r = Of(p);
            if (r != null && r.Def == null) return; // (one given in another world: kept for it)
            string keep = r != null && r.Current is CarryStep ? CarryStep.Stamp(r, r.Step) : null; // (the one that counts now stays)
            if (RavenItem.DropFeathers(p, keep)) Log("Odin's feathers taken out of the bag: no step wants them");
        }

        // a step whose count is things you can count aloud (kills, finds, picks): not seconds or samples
        private static bool Counted(ErrandStep s) { return (s is KillStep || s is LootStep || s is DestroyStep || s is DoStep) && s.Wait <= 0f && s.Need > 1 && s.Need <= 60; } // (Wait: a step of time — sailing samples, taming — is not counted aloud)

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
                    if (s is ChampionStep) ErrandTalk.NearChampion(r.Munin); else ErrandTalk.Near(r.Munin);
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

        // ---------------------------------------------------------------- the step's own deed
        // (take the feather, lay it down…: the same from E at its place, the J circle and the book's button)
        // its name ("" the step has none), and why it can't be done here and now (null: it can)
        internal static string ActLabel(Player p, out string why)
        {
            why = null;
            ErrandRun r = Of(p);
            if (r == null || r.Def == null || r.Current == null) return "";
            try { return r.Current.ActLabel(r, r.Step, p, out why) ?? ""; }
            catch (Exception e) { Log("act label " + r.Id + ": " + e.Message); return ""; }
        }

        // the step has a deed that can be done right now (the J circle shows it only then)
        internal static bool CanAct(Player p) { string why; return ActLabel(p, out why).Length > 0 && why == null; }

        // do it; true when done (why not is told otherwise)
        internal static bool Act(Player p)
        {
            ErrandRun r = Of(p);
            string why, label = ActLabel(p, out why);
            if (label.Length == 0 || p == null) return false;
            if (why != null) { RavenMsg.Center(p, Lang.F("{0} — лише {1}.", label, why)); return false; }
            string fail;
            try { fail = r.Current.Act(r, r.Step, p); }
            catch (Exception e) { Log("act " + r.Id + ": " + e); return false; }
            if (fail != null) { RavenMsg.Center(p, fail); return false; }
            Log("act " + r.Id + " step " + (r.Step + 1) + ": " + label);
            if (r.Current.Done(r, r.Step)) Moved(p, r); else Keep(p, r);
            return true;
        }

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
            if (r.Current != null && !r.Current.Done(r, r.Step)) ErrandTalk.Count(r.Munin, r.Step < r.Count.Length ? r.Count[r.Step] : 0, r.Current.Need, Counted(r.Current));
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

        // lost by its step (the nest broken in a siege)
        internal static void Lose(Player p, ErrandRun r, string why) { if (p != null && r != null && Of(p) == r) Fail(p, r, why); }

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
            ErrandTalk.Failed(r.Munin);
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
            string text = TextOf(r);
            if (text.Length > 0) sb.Append('\n').Append(text);
            foreach (string m in ErrandMods.Lines(r)) sb.Append('\n').Append(m);
            for (int i = 0; i < r.Steps.Length; i++)
                sb.Append('\n').Append(i < r.Step ? "✓ " : i == r.Step ? "• " : "  ").Append(r.Steps[i].Describe(r, i, p));
            return sb.ToString();
        }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven errand · list · give <id> [hugin|munin] · hand · step · done · drop · reload
        // «Пожертва»: coins laid at the nest for the errand you have (saved with it); null when done
        internal static string Offer(Player p, int coins)
        {
            ErrandRun r = Of(p);
            string why = ErrandMods.Offer(p, r, coins);
            if (why == null) Save(p, r);
            return why;
        }

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
            string north = NorthKey;
            if (north.Length > 0 && Has(p, north)) n++;
            return n;
        }

        // the Deep North's boss: its key read from the boss itself (not guessed); "" until the game has it
        private static string _north;
        internal static string NorthKey
        {
            get
            {
                if (_north != null) return _north;
                GameObject boss = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("FrozenKing") : null;
                Character c = boss != null ? boss.GetComponent<Character>() : null;
                if (c == null || string.IsNullOrEmpty(c.m_defeatSetGlobalKey)) return ""; // (not kept: asked again till the game has it)
                _north = c.m_defeatSetGlobalKey;
                Errands.Log("the Deep North's boss key: «" + _north + "»");
                return _north;
            }
        }

        // the land you are opening now: the one after the last boss beaten (1 Meadows … 7 Ashlands, 8 the Deep North)
        internal static int Land(Player p) { return Mathf.Clamp(Of(p) + 1, 1, NorthKey.Length > 0 ? 8 : 7); }

        // the land after that one (None: none, or the Deep North in a game without it)
        internal static Heightmap.Biome NextLand(Player p)
        {
            int n = Land(p) + 1;
            return n == 8 && NorthKey.Length == 0 ? Heightmap.Biome.None : BiomeOf(n);
        }

        // a boss key in the order of the lands: 0 Eikthyr … 6 Fader, 7 the Deep North's; -1 unknown
        internal static int IndexOf(string key)
        {
            int i = Array.IndexOf(BossKeys, key);
            return i >= 0 ? i : key == NorthKey && key.Length > 0 ? 7 : -1;
        }

        internal static int LandOf(Heightmap.Biome b)
        {
            switch (b)
            {
                case Heightmap.Biome.Meadows: return 1;
                case Heightmap.Biome.BlackForest: return 2;
                case Heightmap.Biome.Swamp: return 3;
                case Heightmap.Biome.Mountain: return 4;
                case Heightmap.Biome.Plains: return 5;
                case Heightmap.Biome.Mistlands: return 6;
                case Heightmap.Biome.AshLands: return 7;
                case Heightmap.Biome.DeepNorth: return 8;
                default: return 0;
            }
        }

        internal static Heightmap.Biome BiomeOf(int land)
        {
            switch (land)
            {
                case 1: return Heightmap.Biome.Meadows;
                case 2: return Heightmap.Biome.BlackForest;
                case 3: return Heightmap.Biome.Swamp;
                case 4: return Heightmap.Biome.Mountain;
                case 5: return Heightmap.Biome.Plains;
                case 6: return Heightmap.Biome.Mistlands;
                case 7: return Heightmap.Biome.AshLands;
                case 8: return Heightmap.Biome.DeepNorth;
                default: return Heightmap.Biome.None;
            }
        }

        // how much a raven of this trust wants a thing of a land this far behind the one you open (age 0: that land, or
        // ahead): a trusting raven asks for the fresh and the curious; the old ones still come now and then
        internal static float Fresh(int age, int level, bool curious)
        {
            float k = Mathf.Clamp01((level - 1) / 6f);
            float m = age <= 0 ? 1f + 0.5f * k : age == 1 ? 1f - 0.3f * k : 1f - 0.8f * k;
            return curious ? m * (1f + 0.6f * k) : m;
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
