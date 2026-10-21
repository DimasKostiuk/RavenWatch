using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace HuginWhispers
{
    // Words about the player in the right gender. A phrase holds both forms as {male|female}: {зняв|зняла}.
    // The form follows the character's body (the game's player model: 0 male, 1 female). Lines written
    // before this (in the neuter, or as if every player were a man) are rewritten in the player's files once,
    // only where the line is still the mod's own; your own lines are not touched.
    internal static class Grammar
    {
        private static readonly Regex Forms = new Regex(@"\{([^{}|]*)\|([^{}|]*)\}");

        internal static bool Female(Player p)
        {
            if (p == null) p = Player.m_localPlayer;
            try { return p != null && p.GetPlayerModel() == 1; } catch { return false; }
        }

        internal static string Fix(string text) { return Fix(text, null); }

        internal static string Fix(string text, Player p)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('|') < 0) return text;
            return Fix(text, Female(p));
        }

        // for a player who need not be here (a raven on a perch speaks of its owner): the forms by what is known
        internal static string Fix(string text, bool female)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('|') < 0) return text;
            return Forms.Replace(text, m => female ? m.Groups[2].Value : m.Groups[1].Value);
        }

        // a {male|female} pair is not a placeholder waiting for a value
        internal static bool IsForms(string token) { return token.IndexOf('|') >= 0; }

        // ---------------------------------------------------------------- the player's phrase files

        internal static void MigrateFiles(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (string path in Directory.GetFiles(dir, "*.txt"))
                {
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    string was = text;
                    for (int i = 0; i < Old.GetLength(0); i++) text = Swap(text, Old[i, 0], Old[i, 1]);
                    if (text != was) File.WriteAllText(path, text, new UTF8Encoding(false));
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Phrase files: gender update failed: " + e.Message); }
        }

        // Hugin got a new voice (patterns rev 63: the smartass, short and spoken, no longer the scholar), and Munin's lines were
        // cleaned of Russian words and slips at the same time: too many lines changed to swap them one by one (a revision only
        // adds lines, so the old ones would stay beside the new), so both phrase files from before it are deleted once and
        // made again from the mod's lines — the Ukrainian ones, and a built-in language's ones (a language you made yourself,
        // with no built-in lines, is left alone: there is nothing to make it again from)
        internal static void RemakeVoices(string dir)
        {
#if RAVEN_ITEM
            try
            {
                var files = new System.Collections.Generic.List<string>();
                string langs = Path.Combine(dir, "lang");
                foreach (string name in new[] { Patterns.HuginFile, Patterns.MuninFile })
                {
                    files.Add(Path.Combine(dir, name));
                    if (Directory.Exists(langs))
                        foreach (string sub in Directory.GetDirectories(langs))
                            if (typeof(Grammar).Assembly.GetManifestResourceInfo("RavenWatch.lang." + Path.GetFileName(sub) + "." + name) != null)
                                files.Add(Path.Combine(sub, name));
                }
                foreach (string path in files)
                {
                    if (!File.Exists(path)) continue;
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    bool made = false;
                    for (int rev = 64; rev <= Patterns.Revision; rev++) if (text.Contains("# patterns rev " + rev + " ")) { made = true; break; } // (made with the new voice and the talks' threads already; 64: the talks' lines re-marked, the old ones must not stay beside them)
                    if (made) continue;
                    File.Delete(path);
                    if (Plugin.Instance != null) Plugin.Instance.Log("Phrase file made again (Hugin's new voice, Munin's cleaned lines): " + path);
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Phrase files: the rev 63 remake failed: " + e.Message); }
#endif
        }

        // Munin got a new voice (patterns rev 59: the lout, «шо» and the swearing mid-word): too many lines changed to swap
        // them one by one, so his files from before it are deleted once and made again from the mod's lines
        private static readonly string[] MuninFiles = { "munin_patterns.txt", "munin_quips.txt", "munin_titles.txt", "munin_texts.txt" };
        internal static void MuninVoice(string dir)
        {
#if RAVEN_ITEM
            try
            {
                string patterns = Path.Combine(dir, MuninFiles[0]);
                if (!File.Exists(patterns)) return;
                string text = File.ReadAllText(patterns, Encoding.UTF8);
                for (int rev = 59; rev <= Patterns.Revision; rev++) if (text.Contains("# patterns rev " + rev + " ")) return; // (made with the new voice already)
                foreach (string name in MuninFiles) { string path = Path.Combine(dir, name); if (File.Exists(path)) File.Delete(path); }
                if (Plugin.Instance != null) Plugin.Instance.Log("Munin's phrase files made again for his new voice");
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Phrase files: Munin's voice update failed: " + e.Message); }
#endif
        }

        // One old line to its new form, once. Some new forms hold the old one inside (a word put before it, a few added
        // after it): a plain replace did it again on every start, so up to 1.4 those lines grew («дар дар дар…», «Майже не
        // соромно. Майже не соромно…»). Here what grew is folded back first, and a line that already has its new form is
        // left as it is.
        private static string Swap(string text, string old, string now)
        {
            int at = now.IndexOf(old, StringComparison.Ordinal);
            if (at < 0) return text.Replace(old, now);
            string before = now.Substring(0, at), after = now.Substring(at + old.Length);
            if (before.Length > 0) text = Regex.Replace(text, "(?:" + Regex.Escape(before) + ")+" + Regex.Escape(old), (before + old).Replace("$", "$$"));
            if (after.Length > 0) text = Regex.Replace(text, Regex.Escape(old) + "(?:" + Regex.Escape(after) + ")+", (old + after).Replace("$", "$$"));
            StringBuilder sb = new StringBuilder(text.Length + 64);
            int from = 0;
            while (true)
            {
                int i = text.IndexOf(old, from, StringComparison.Ordinal);
                if (i < 0) break;
                bool done = i >= before.Length && string.CompareOrdinal(text, i - before.Length, now, 0, now.Length) == 0;
                sb.Append(text, from, i - from);
                sb.Append(done ? old : now);
                from = i + old.Length;
            }
            sb.Append(text, from, text.Length - from);
            return sb.ToString();
        }

        private static readonly string[,] Old =
        {
            { "~2 Ще один витримає. Не більше, чуєш?", "~2 Ще тримає. Не розслабляйся, чуєш?" }, // 2.1.0: the ward takes up to three blows
            { "А на шухері постою, мені не впервой.", "А на шухері постою, мені не вперше." }, // 1.7.0: no russism
            { "Знаєш, що {name} учора {утнув|утнула}? Ні? От і я досі не зрозумів.", "Знаєш, що {name} учора утну{в|ла}? Сі{в|ла} на власний ніж. Я досі не зрозумів як." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Слухай, що я тобі розповім про {name}, {addr}. Тільки не сміятися.", "Слухай, {addr}, тільки не сміятися: {name} учора двічі впа{в|ла} в ту саму яму." }, // 1.7.0: the name only in the nominative; the story told at once
            { "{addr}, послухай, що {name} учора {утнув|утнула}. Ржатимеш до вечора.", "{addr}, послухай: {name} учора би{вся|лася} з дохлим кабаном. І програ{в|ла}. Ржатимеш до вечора." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Плітки є, {addr}. Свіжі, смачні, про {name}.", "Плітки є, {addr}. Свіжі: {name} учора з'ї{в|ла} гриб, який я не радив. Потім співа{в|ла} до ранку." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Розповідай. Хоча моя людина вчора замкнула себе у власній хаті. Перевершиш?", "Це ще що. Моя людина вчора замкнула себе у власній хаті. Перевершиш?" }, // 1.7.0: the name only in the nominative; the story told at once
            { "Ну? Тільки знай: моя вчора двері шукала у власній хаті. Пів години.", "Пусте. Моя вчора двері шукала у власній хаті. Пів години." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Та що там твоя. Моя вчора у своїй хаті заблукала, курва.", "Ха, дрібниці. Моя вчора у своїй хаті заблукала, курва." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Розказуй. Але моя вчора вийти з хати не могла. Двері тягла не в той бік.", "Подумаєш. А моя вчора вийти з хати не могла. Двері тягла не в той бік." }, // 1.7.0: the name only in the nominative; the story told at once
            { "{addr}, хочеш історію? Про {name} і {його|її} перший вогонь. Довга, як та ніч.", "{addr}, хочеш історію? Перший вогонь {name} розпалю{вав|вала} пів дня. Я встиг змерзнути двічі." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Історію хочеш, {addr}? Про {name}. Смішну. Для мене.", "Історію хочеш, {addr}? {name} пів дня вогонь розпалю{вав|вала}. Смішно. Для мене." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Попроси в {name}. Чемно. Знаю, тобі важко.", "Попроси. {name} не відмовить, якщо чемно. Знаю, тобі важко." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Ти в цей час ховався за {name}.", "А ти в цей час ховався. {name} бачи{в|ла}, не відкрутишся." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Ще б мить — і ми б летіли до Одіна без {name}.", "Ще б мить — і {name} лиши{вся|лася} б тут, а ми б летіли до Одіна самі." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Земля витримала. Гірше з {name}.", "Земля витримала. А от {name} — не факт." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Риба не для тебе. Риба для {name}.", "Риба не для тебе. Рибу їсть {name}." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Хугіне, а ми знаємо, куди йдемо, чи це в {name} така прогулянка?", "Хугіне, а ми знаємо, куди йдемо, чи {name} просто гуляє?" }, // 1.7.0: the name only in the nominative; the story told at once
            { "Цікаво, а в {friend} є свої ворони? Сподіваюсь, не такі, як ти.", "Цікаво, {friend} має своїх воронів? Сподіваюсь, не таких, як ти." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Отож. Тому в {friend} їх і нема.", "Отож. Тому {friend} без воронів і ходить." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Карта в голові {name}. Сподіваюсь.", "Карту {name} тримає в голові. Сподіваюсь." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Довіра, довіра… А хто вчора {name} від вовка затулив? Я.", "Довіра, довіра… А хто вчора затулив нашу людину від вовка? Я." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Скриня? Тільки не кажи {name}. Я перший гляну.", "Скриня? Тихо, хай {name} не почує. Я перший гляну." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Для {name}? А мені тоді що, вода?", "Ага, {name}, значить. А мені тоді що, вода?" }, // 1.7.0: the name only in the nominative; the story told at once
            { "А, {stranger}. Ну, якщо ти з {name}, то живи. Поки що.", "А, {stranger}. Ну, якщо {name} тебе знає, то живи. Поки що." }, // 1.7.0: the name only in the nominative; the story told at once
            { "{stranger}? Якщо з {name}, то хай буде. Але руки геть.", "{stranger}? Як {name} не проти, хай буде. Але руки геть." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Слухай, Хугіне... нам із {name} пощастило. Тільки мовчи.", "Слухай, Хугіне... з людиною нам пощастило. {name} — наш{|а}. Тільки мовчи." }, // 1.7.0: the name only in the nominative; the story told at once
            { "Раз ти такий недогадлив{ий|а}", "Раз ти так{ий|а} недогадлив{ий|а}" }, // 1.7.0: to you in your forms
            { "Я рахував, бо ти не давав мені іншого заняття.", "Я рахував, бо ти не дава{в|ла} мені іншого заняття." }, // 1.7.0: to you in your forms
            { "Я ситий, але це ж ти дав. Беру.", "Я ситий, але це ж ти да{в|ла}. Беру." }, // 1.7.0: to you in your forms
            { "Про запас з'їм. Ти ж старався.", "Про запас з'їм. Ти ж стара{вся|лася}." }, // 1.7.0: to you in your forms
            { "Їм, бо ти старався. Не звикай.", "Їм, бо ти стара{вся|лася}. Не звикай." }, // 1.7.0: to you in your forms
            { "скільки разів ти почухався.", "скільки разів ти почуха{вся|лася}." }, // 1.7.0: to you in your forms
            { "Добре, що ти був поруч, коли я його заслужив.", "Добре, що ти {був|була} поруч, коли я його заслужив." }, // 1.7.0: to you in your forms
            { "{волів|воліла} б вирвати", "{волів би|воліла б} вирвати" }, // 1.7.0: «би» after a consonant (he: «міг би», not «міг б»)
            { "витрати{в|ла} б на дурниці", "витрати{в би|ла б} на дурниці" }, // 1.7.0: «би» after a consonant (he: «міг би», not «міг б»)
            { "{міг|могла} б убивати", "{міг би|могла б} убивати" }, // 1.7.0: «би» after a consonant (he: «міг би», not «міг б»)
            { "{міг|могла} б збирати", "{міг би|могла б} збирати" }, // 1.7.0: «би» after a consonant (he: «міг би», not «міг б»)
{ "[x_intro_open] {хх_вступ?} {хх_знайомство} {хх_хвіст?}", "[x_intro_open] {хх_знайомство}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_intro_reply] {хх_відгук?} {хх_знайомство_відповідь} {хх_хвіст?}", "[x_intro_reply] {хх_знайомство_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_intro_join] {хх_знайомство_встряє} {хх_хвіст?}", "[x_intro_join] {хх_знайомство_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_intro_join] {хх_встряє}", "[x_intro_join] {хх_знайомство_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_greet_open] {хх_вступ?} {хх_зустріч} {хх_хвіст?}", "[x_greet_open] {хх_зустріч}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_greet_reply] {хх_відгук?} {хх_зустріч_відповідь} {хх_хвіст?}", "[x_greet_reply] {хх_зустріч_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_greet_join] {хх_зустріч_встряє} {хх_хвіст?}", "[x_greet_join] {хх_зустріч_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_greet_join] {хх_встряє}", "[x_greet_join] {хх_зустріч_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_rename_open] {хх_вступ?} {хх_нове_імя} {хх_хвіст?}", "[x_rename_open] {хх_нове_імя}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_rename_reply] {хх_відгук?} {хх_нове_імя_відповідь} {хх_хвіст?}", "[x_rename_reply] {хх_нове_імя_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_rename_join] {хх_нове_імя_встряє} {хх_хвіст?}", "[x_rename_join] {хх_нове_імя_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_rename_join] {хх_встряє}", "[x_rename_join] {хх_нове_імя_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_open] {хх_вступ?} {хх_друзі} {хх_хвіст?}", "[x_pals_open] {хх_пр}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_reply] {хх_відгук?} {хх_друзі_відповідь} {хх_хвіст?}", "[x_pals_reply] {хх_пр_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_close] {хх_друзі_кінець}", "[x_pals_close] {хх_пр_кінець}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_join] {хх_друзі_встряє} {хх_хвіст?}", "[x_pals_join] {хх_пр_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_join] {хх_встряє}", "[x_pals_join] {хх_пр_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_open] {хх_вступ?} {хх_вогонь} {хх_хвіст?}", "[x_fire_open] {хх_вг}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_reply] {хх_відгук?} {хх_вогонь_відповідь} {хх_хвіст?}", "[x_fire_reply] {хх_вг_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_close] {хх_вогонь_кінець}", "[x_fire_close] {хх_вг_кінець}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_join] {хх_вогонь_встряє} {хх_хвіст?}", "[x_fire_join] {хх_вг_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_join] {хх_встряє}", "[x_fire_join] {хх_вг_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_open] {хх_вступ?} {хх_бос} {хх_хвіст?}", "[x_boss_open] {хх_бс}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_reply] {хх_відгук?} {хх_бос_відповідь} {хх_хвіст?}", "[x_boss_reply] {хх_бс_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_close] {хх_бос_кінець}", "[x_boss_close] {хх_бс_кінець}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_join] {хх_бос_встряє} {хх_хвіст?}", "[x_boss_join] {хх_бс_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_join] {хх_встряє}", "[x_boss_join] {хх_бс_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_join_reply] {хх_відгук?} {хх_до_того_хто_встряв}", "[x_join_reply] {хх_до_того_хто_встряв}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_intro_open] {мх_вступ?} {мх_знайомство} {мх_хвіст?}", "[x_intro_open] {мх_знайомство}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_intro_reply] {мх_відгук?} {мх_знайомство_відповідь} {мх_хвіст?}", "[x_intro_reply] {мх_знайомство_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_intro_join] {мх_знайомство_встряє} {мх_хвіст?}", "[x_intro_join] {мх_знайомство_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_intro_join] {мх_встряє}", "[x_intro_join] {мх_знайомство_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_greet_open] {мх_вступ?} {мх_зустріч} {мх_хвіст?}", "[x_greet_open] {мх_зустріч}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_greet_reply] {мх_відгук?} {мх_зустріч_відповідь} {мх_хвіст?}", "[x_greet_reply] {мх_зустріч_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_greet_join] {мх_зустріч_встряє} {мх_хвіст?}", "[x_greet_join] {мх_зустріч_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_greet_join] {мх_встряє}", "[x_greet_join] {мх_зустріч_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_rename_open] {мх_вступ?} {мх_нове_імя} {мх_хвіст?}", "[x_rename_open] {мх_нове_імя}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_rename_reply] {мх_відгук?} {мх_нове_імя_відповідь} {мх_хвіст?}", "[x_rename_reply] {мх_нове_імя_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_rename_join] {мх_нове_імя_встряє} {мх_хвіст?}", "[x_rename_join] {мх_нове_імя_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_rename_join] {мх_встряє}", "[x_rename_join] {мх_нове_імя_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_open] {мх_вступ?} {мх_друзі} {мх_хвіст?}", "[x_pals_open] {мх_пр}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_reply] {мх_відгук?} {мх_друзі_відповідь} {мх_хвіст?}", "[x_pals_reply] {мх_пр_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_close] {мх_друзі_кінець}", "[x_pals_close] {мх_пр_кінець}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_join] {мх_друзі_встряє} {мх_хвіст?}", "[x_pals_join] {мх_пр_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_pals_join] {мх_встряє}", "[x_pals_join] {мх_пр_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_open] {мх_вступ?} {мх_вогонь} {мх_хвіст?}", "[x_fire_open] {мх_вг}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_reply] {мх_відгук?} {мх_вогонь_відповідь} {мх_хвіст?}", "[x_fire_reply] {мх_вг_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_close] {мх_вогонь_кінець}", "[x_fire_close] {мх_вг_кінець}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_join] {мх_вогонь_встряє} {мх_хвіст?}", "[x_fire_join] {мх_вг_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_fire_join] {мх_встряє}", "[x_fire_join] {мх_вг_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_open] {мх_вступ?} {мх_бос} {мх_хвіст?}", "[x_boss_open] {мх_бс}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_reply] {мх_відгук?} {мх_бос_відповідь} {мх_хвіст?}", "[x_boss_reply] {мх_бс_відповідь}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_close] {мх_бос_кінець}", "[x_boss_close] {мх_бс_кінець}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_join] {мх_бос_встряє} {мх_хвіст?}", "[x_boss_join] {мх_бс_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_boss_join] {мх_встряє}", "[x_boss_join] {мх_бс_встряє}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[x_join_reply] {мх_відгук?} {мх_до_того_хто_встряв}", "[x_join_reply] {мх_до_того_хто_встряв}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk
            { "[loyal] Думав, я тебе покину?", "[loyal] {Думав|Думала}, я тебе покину?" }, // 1.7.0: the loyal words to you in your forms
            { "[loyal] Думав, позбудешся мене?", "[loyal] {Думав|Думала}, позбудешся мене?" }, // 1.7.0: the loyal words to you in your forms
            { "бачив? Ні? Ну звісно.", "бачи{в|ла}? Ні? Ну звісно." }, // 1.7.0: a strike's words to you in your forms
            { "ідеально. Бачив?", "ідеально. Бачи{в|ла}?" }, // 1.7.0: a strike's words to you in your forms
            { "полетів, бо ти попросив.", "полетів, бо ти попроси{в|ла}." }, // 1.7.0: a strike's words to you in your forms
            { "бачив, як треба, недоумку?!", "бачи{в|ла}, як треба, недоумку?!" }, // 1.7.0: a strike's words to you in your forms
            { "ти стояв, а я працював.", "ти стоя{в|ла}, а я працював." }, // 1.7.0: a strike's words to you in your forms
            { "\n{m_when} ми вперше разом потрапили {m_where_in}, і ти одразу там {помер|померла}. Місцева гостинність.\n", "\n{m_when} ми вперше разом опинилися {m_where_in}, і ти одразу там {помер|померла}. Місцева гостинність.\n" }, // 1.7.0: motion verb + locative biome ("потрапили на Болоті")
            { "\n{m_when} ми з тобою вперше разом сунулися {m_where_in}. Я одразу понюхав — смертю тхне, бляха.\n", "\n{m_when} ми з тобою вперше разом опинилися {m_where_in}. Я одразу понюхав — смертю тхне, бляха.\n" }, // 1.7.0: motion verb + locative biome ("потрапили на Болоті")
            { "^нудьга|тиша Нудно мені, а ти мене ще й повчаєш.", "^сидіти Нудно мені, а ти мене ще й повчаєш." }, // 1.6.1: a perch talk thread of its own (the answers fit it)
            { "~3 ^ми Знаю. Не лайся, а то розчулюся.", "~3 ^ми Знаю. Тільки не кажи це так ніжно, а то розчулюся." }, // 1.6.1: nobody swore there
            { "^жердина Посидь спокійно хоч раз. Тобі піде на користь.", "^сидіти Посидь спокійно хоч раз. Тобі піде на користь." }, // 1.6.1: a perch talk thread of its own (the answers fit it)
            { "^жердина|сидіти Зате сила йде. А від твого бурчання — ні.", "^сидіти Зате сила йде. А від твого бурчання — ні." }, // 1.6.1: a perch talk thread of its own (the answers fit it)
            { "^жердина Добра в нас жердина. Хоч щось у цьому домі рівне.", "^рівна Добра в нас жердина. Хоч щось у цьому домі рівне." }, // 1.6.1: a perch talk thread of its own (the answers fit it)
            { "^жердина|сидіти Сидіти — робота? Ти її придумав, щоб нічого не робити.", "^сидіти Сидіти — робота? Ти її придумав, щоб нічого не робити." }, // 1.6.1: a perch talk thread of its own (the answers fit it)
            { "^жердина Хугіне, тобі не здається, що ця палиця кривувата?", "^крива Хугіне, тобі не здається, що ця палиця кривувата?" }, // 1.6.1: a perch talk thread of its own (the answers fit it)
            { "Тепло, сухо, затишно. Ось це я розумію відпочинок.", "Тепло, сухо, затишно. Ось це я розумію — відпочинок." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Як удома. Ну, як у Одіна, тільки без його бурчання.", "Як удома. Ну, як в Одіна, тільки без його бурчання." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "{stranger}, ти хто такий? Я тебе запам'ятав.", "{stranger}? Не пригадую, щоб тебе сюди кликали. Але обличчя я запам'ятав." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "^дім Цікаво, де зараз {name} і що {він|вона} знову ламає.", "^задум Цікаво, що {name} знову задума{в|ла}. Щось велике, мабуть." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "^дім Повернеться. Завжди повертається. Голодн{ий|а} і брудн{ий|а}.", "^задум Щось велике і, як завжди, без плану. Зате з гуркотом." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "^нудьга|сидіти Нудно? Порахуй пір'їни. Я своє вже порахував.", "^нудьга|сидіти Нудно? Порахуй пір'їни. Я свої вже порахував." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Ото затишок. Аж підозріло, що це ти зробив.", "Ото затишок. Аж підозріло, що це ти зроби{в|ла}." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Ти мене на вітрі посадив? Серйозно?", "Ти мене на вітрі посади{в|ла}? Серйозно?" }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Посадив на вітрі. Ну дякую, ворожина.", "Посади{в|ла} на вітрі. Ну дякую, {благодійнику|благодійнице}." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "{прийшов|прийшла}? Ну все, кінець тиші.", "Прийш{ов|ла}? Ну все, кінець тиші." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Я сиджу тут не тому, що ти сказав. Я сам захотів.", "Я сиджу тут не тому, що ти сказа{в|ла}. Я сам захотів." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Посиджу. Але ти далеко не йди, дурню.", "Посиджу. Але ти далеко не йди, {дурню|дурепо}." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Нарешті, дурню. Я вже скучив. Тільки нікому.", "Нарешті, {дурню|дурепо}. Я вже скучив. Тільки нікому." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Сиджу і дивлюсь на тебе, дурню. Подобається.", "Сиджу і дивлюсь на тебе, {дурню|дурепо}. Подобається." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Чого вилупився, {stranger}? Ворона не бачив?", "Чого витріщаєшся, {stranger}? Воронів тут ще не було, чи що?" }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "Ще один дурень. Їх тут розплідник?", "Ще одне чуже обличчя. Їх тут розплідник?" }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "^дім Де цей дурень {name}? Знову кудись поліз.", "^задум Що там {цей дурень|ця дурепа} {name} знову затіває?" }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "^дім Повернеться. І знову притягне якогось бруду.", "^задум Щось дурне. І знову притягне якогось бруду." }, // 1.6.1: the perch lines (forms, a stranger of either sex, the owner may be near)
            { "~2 о, моє улюблене.", "~2 о, смакота." }, // 1.5.1: a raven has one real favourite food now
            { "[fed] О, це я люблю. Ти {запам'ятав... чи вгадав|запам'ятала... чи вгадала}?", "[fed] О, смачно. Хоча є одна річ, яку я люблю більше. Здогадаєшся?" }, // 1.5.1: the real favourite food
            { "[fed] Хоч щось путнє від тебе, {name}. Давай ще завтра.", "[fed] Хоч щось путнє від тебе, {name}. Давай ще, як зголоднію." }, // 1.5.1: feeding is no longer once a day
            { "~1 ти {поїв|поїла}, а про мене {забув|забула}, жаднюго.", "~1 ти пої{в|ла}, а про мене забу{в|ла}, жаднюго." }, // 1.5.1: gender split before the differing letters
            { "^хвальба До Одіна я долітаю швидше. Перевірено.", "^хвальба Мене {name} частіше кличе. Довіра — річ така." }, // 1.5: the trust-gap brags name the trust
            { "^хвальба Хтось росте, а хтось лається.", "^хвальба Довіра в мене росте, а в тебе — тільки лайка." }, // 1.5: the trust-gap brags name the trust
            { "~1 ^хвальба Наздоганяй, Муніне.", "~1 ^хвальба Муніне, наздоганяй. Довіра сама не прийде." }, // 1.5: the trust-gap brags name the trust
            { "^хвальба Я до Одіна літав частіше. Він мене вже впізнає, {м_слівце}.", "^хвальба Мене {name} частіше годує. Бо довіряє, {м_слівце}." }, // 1.5: the trust-gap brags name the trust
            { "~1 ^хвальба Наздоганяй, мудрагелю.", "~1 ^хвальба По довірі ти позаду, мудрагелю. Наздоганяй." }, // 1.5: the trust-gap brags name the trust
            { "{fish} риб — ти годувальник.", "{fish} риб — {годувальник|годувальниця} хоч куди." }, // 1.5: the phrase review (gender, wording, names)
            { "~3 {name}, мій вірний супутнику,", "~3 {name}, {мій вірний супутнику|моя вірна супутнице}," }, // 1.5: the phrase review (gender, wording, names)
            { "Полечу розповім Одіну.", "Полечу, розповім Одіну." }, // 1.5: the phrase review (gender, wording, names)
            { "Тримай дзьобом.", "Ось тобі дзьобом." }, // 1.5: the phrase review (gender, wording, names)
            { "живий — значить, правильно біг.", "{живий|жива} — значить, правильно {біг|бігла}." }, // 1.5: the phrase review (gender, wording, names)
            { "~1 замерзнеш — сам винен.", "~1 замерзнеш — {сам винен|сама винна}." }, // 1.5: the phrase review (gender, wording, names)
            { "~3 Я радий, що ти мій, {name}.", "~3 Я радий, що ти {мій|моя}, {name}." }, // 1.5: the phrase review (gender, wording, names)
            { "кухар із тебе такий, що й вовки б подумали.", "кухар із тебе такий, що й вовки б відмовились." }, // 1.5: the phrase review (gender, wording, names)
            { "ти в наш перший спільний день там {протримався|протрималася}", "ти в наш перший спільний день {протримався|протрималася}" }, // 1.5: the phrase review (gender, wording, names)
            { "Кривий, протягуватий, але свій.", "Кривий, з протягами, але свій." }, // 1.5: the phrase review (gender, wording, names)
            { "{живий|жива} кращий за мертвого героя.", "краще бути живим, ніж мертвим героєм." }, // 1.5: the phrase review (gender, wording, names)
            { "До вогню, {дурню|дурна}.", "До вогню, {дурню|дурепо}." }, // 1.5: the phrase review (gender, wording, names)
            { "будуєш, а міг би вбивати.", "будуєш, а {міг би|могла б} убивати." }, // 1.5: the phrase review (gender, wording, names)
            { "~1 Ей, кусок лайна,", "~1 Ей, шматок лайна," }, // 1.5: the phrase review (gender, wording, names)
            { "рву по черзі, сука, черга!", "рву по черзі, сука, ставайте в чергу!" }, // 1.5: the phrase review (gender, wording, names)
            { "ніч, а ти все одно сліпий, як і вдень.", "ніч, а ти все одно {сліпий|сліпа}, як і вдень." }, // 1.5: the phrase review (gender, wording, names)
            { "збираєш ягоди, а міг би збирати голови.", "збираєш ягоди, а {міг би|могла б} збирати голови." }, // 1.5: the phrase review (gender, wording, names)
            { "Ходімо когось битимемо.", "Ходімо, наб'ємо когось." }, // 1.5: the phrase review (gender, wording, names)
            { "мокре ти чучело!", "мокре ти опудало!" }, // 1.5: the phrase review (gender, wording, names)
            { "убив? І що, мені аплодувати?", "{убив|убила}? І що, мені аплодувати?" }, // 1.5: the phrase review (gender, wording, names)
            { "вбив? Я б краще вбив тебе.", "{вбив|вбила}? Я б краще вбив тебе." }, // 1.5: the phrase review (gender, wording, names)
            { "[hungry] {name}, ти коли востаннє їв?", "[hungry] {name}, ти коли востаннє {їв|їла}?" }, // 1.5: the phrase review (gender, wording, names)
            { "Будь із ним чемним.", "Будь із ним {чемним|чемною}." }, // 1.5: the phrase review (gender, wording, names)
            { "вночі шляєшся, {дурню|дурна}.", "вночі шляєшся, {дурню|дурепо}." }, // 1.5: the phrase review (gender, wording, names)
            { "{хропів|хропла}", "{хропів|хропіла}" }, // 1.5: the phrase review (gender, wording, names)
            { "скидаються тобі на надгробок і іржуть", "скидаються тобі на надгробок й іржуть" }, // 1.5: the phrase review (gender, wording, names)
            { "[death_drown] Обсох уже, водолазе?", "[death_drown] {Обсох|Обсохла} уже, водолазе?" }, // 1.5: the phrase review (gender, wording, names)
            { "[hungry] Ти коли востаннє їв?", "[hungry] Ти коли востаннє {їв|їла}?" }, // 1.5: the phrase review (gender, wording, names)
            { "Ти замерз, як бурулька в дупі троля.", "Ти {замерз|замерзла}, як бурулька в дупі троля." }, // 1.5: the phrase review (gender, wording, names)
            { "І половина з них — грейдлінги.", "І половина з них — сіролюдики." }, // 1.5: the phrase review (gender, wording, names)
            { "найгірша засідка в історії Мідгарду", "найгірша засідка в історії Мідґарду" }, // 1.5: the phrase review (gender, wording, names)
            { "грейдварфи складуть про тебе пісню", "сіролюди складуть про тебе пісню" }, // 1.5: the phrase review (gender, wording, names)
            { "для кожного грейдварфа в лісі", "для кожного сіролюда в лісі" }, // 1.5: the phrase review (gender, wording, names)
            { "від шиї. Від ШИЇ.", "від водяника. Від ВОДЯНИКА." }, // 1.5: the phrase review (gender, wording, names)
            { "увесь Чорний ліс.", "усе Чорнолісся." }, // 1.5: the phrase review (gender, wording, names)
            { "кращих воїнів Мідгарду", "кращих воїнів Мідґарду" }, // 1.5: the phrase review (gender, wording, names)
            { "розїбашити", "роз'їбашити" }, // 1.5: the phrase review (gender, wording, names)
            { "Надихнув{ся|лася} нами. Тільки крил забув{|ла} взяти.", "Надихну{вся|лася} нами. Тільки крил забу{в|ла} взяти." }, // 1.5: the phrase review (gender, wording)
            { "де {name} хотів{|ла}. Решта неважливо.", "де {name} хоті{в|ла}. Решта неважливо." }, // 1.5: the phrase review (gender, wording)
            { "Пізно. Вже послухав{|ла}.", "Пізно. Вже послуха{в|ла}." }, // 1.5: the phrase review (gender, wording)
            { "Там руда, Муніне. Скарб. Твоїх кісток не важче.", "Там руда, Муніне. Скарб. Потерпиш." }, // 1.5: the phrase review (gender, wording)
            { "Я вже бачив Одіна двічі після тебе.", "До Одіна я долітаю швидше. Перевірено." }, // 1.5: the phrase review (gender, wording)
            { "Смердю, бо працюю.", "Смерджу, бо працюю." }, // 1.5: the phrase review (gender, wording)
            { "^стомився Ну… майже ліньки.", "^стомився Ну… помовчимо. Хвилинку." }, // 1.5: the phrase review (gender, wording)
            { "^ноги Ціле — то й добре. Йдемо.", "^ноги Цілі — то й добре. Йдемо." }, // 1.5: the phrase review (gender, wording)
            { "^пильно Видно. Добре. Хоч би й так.", "^пильно Хай так. Аби дивився, а не каркав." }, // 1.5: the phrase review (gender, wording)
            { "^стук Терпи. Стукнеш когось — то хіба себе.", "^стук Терпи. Хочеш когось стукнути — стукни себе." }, // 1.5: the phrase review (gender, wording)
            { "«Повертатися не варто». Ти не послухав.", "«Повертатися не варто». Ти не послуха{в|ла}." }, // 1.5: the phrase review (gender, wording)
            { "{name}, ти пробіг {km}.", "{name}, ти пробі{г|гла} {km}." }, // 1.5: the phrase review (gender, wording)
            { "Ти помер {times} за кілька хвилин.", "Ти поме{р|рла} {times} за кілька хвилин." }, // 1.5: the phrase review (gender, wording)
            { "Ти переміг: {boss}!", "Ти пере{міг|могла}: {boss}!" }, // 1.5: the phrase review (gender, wording)
            { "[travel] Ти пробіг {km}.", "[travel] Ти пробі{г|гла} {km}." }, // 1.5: the phrase review (gender, wording)
            { "~1 {m_who_acc} ви здолали разом із {m_friend}. {Сам|Сама} ти б не {впорався|впоралася}, будьмо чесні.", "~1 {m_who_acc} ти {здолав|здолала} не {сам|сама}: поруч були {m_friend}. {Сам|Сама} ти б не {впорався|впоралася}, будьмо чесні." }, // 1.3: a friend's name stays as it is, never declined
            { "~3 {m_who_acc} ви здолали разом: ти й {m_friend}. Гарний був день.", "~3 {m_who_acc} ви здолали разом. Поруч були {m_friend}. Гарний був день." }, // 1.3: a friend's name stays as it is, never declined
            { "{m_who_acc} ми рвали разом з {m_friend}, бля. Всі в кровищі, як годиться.", "{m_who_acc} ми рвали не самі: поруч були {m_friend}, бля. Всі в кровищі, як годиться." }, // 1.3: a friend's name stays as it is, never declined
            { "\nЯ пам'ятаю:\n", "\nСлухай-но:\n" }, // 1.3: memory intros that said "I remember" twice with the story
            { "\n~2 Пригадую одну історію:\n", "\n~2 Одна історія:\n" }, // 1.3: memory intros that said "I remember" twice with the story
            { "\n~3 Пам'ятаєш?\n", "\n~3 Слухай, друже:\n" }, // 1.3: memory intros that said "I remember" twice with the story
            { "\nЯ пам'ятаю.\n", "\nСлухай сюди.\n" }, // 1.3: memory intros that said "I remember" twice with the story
            { "\nСлухай, що я пам'ятаю:\n", "\nЗгадалося, сука:\n" }, // 1.3: memory intros that said "I remember" twice with the story
            { "\n~3 Пам'ятаєш, бля?\n", "\n~3 Слухай, бля:\n" }, // 1.3: memory intros that said "I remember" twice with the story
            { "\n~3 Слухай, а пам'ятаєш:\n", "\n~3 О, а ось іще:\n" }, // 1.3: memory intros that said "I remember" twice with the story
            { "\nЯ не забуваю, бля.\n", "\nТаке не стирається, бля.\n" }, // 1.3: memory intros that said "I remember" twice with the story
            { "«{gift}»? Гаразд. Не тому, що ти просиш, а тому, що мушу.", "дар «{gift}»? Гаразд. Не тому, що ти просиш, а тому, що мушу." }, // 1.3: a gift name only after «дар», so its case never jars
            { "бери свій «{gift}» і не дякуй. Мені твоя вдячність ні до чого.", "бери мій дар «{gift}» і не дякуй. Мені твоя вдячність ні до чого." }, // 1.3: a gift name only after «дар», so its case never jars
            { "«{gift}», так. Наче злодієві ключ від скрині даю.", "дар «{gift}» — злодієві. Наче ключ від чужої скрині віддаю." }, // 1.3: a gift name only after «дар», so its case never jars
            { "на тобі твій «{gift}», злодюго. Подавися.", "на тобі дар «{gift}». Подавися." }, // 1.3: a gift name only after «дар», so its case never jars
            { "«{gift}», бля. Як крадене пиво: п'єш, а смаку нема.", "дар «{gift}», бля. Як крадене пиво: п'єш, а смаку нема." }, // 1.3: a gift name only after «дар», so its case never jars
            { "[memory] ", "[mem_stats] " }, // 1.3: the old lifetime lines are now the ravens' memory
            { "~3 добре нам тут. Тільки не кажи Муніну, що я це сказав.", "~3 добре нам тут. Розкажеш комусь — заперечуватиму." }, // 1.3: no Munin in a Hugin line when Munin may not be there
            { "[sh_here] {name}, {biome_here_in} з твого плеча видно далі, ніж ти думаєш.", "[sh_here] {name}, {biome_here_in} з твого плеча видно далі, ніж ти думаєш. Тобі, звісно, не видно." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_here] Сиджу на твоєму плечі {biome_here_in} і пильную. Не хвилюйся, {name}.", "[sh_here] Сиджу на твоєму плечі {biome_here_in} і пильную. Хтось же мусить, {name}." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_here] Звідси, з плеча, {biome_here_in} все як на долоні, {name}.", "[sh_here] Звідси, з плеча, {biome_here_in} все як на долоні, {name}. Навіть твої помилки — наперед." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_here] {name}, мені тут, {biome_here_in}, зручно. Тільки не біжи так, трясе.", "[sh_here] {name}, мені тут, {biome_here_in}, зручно. Було б ще зручніше, якби ти не біг, як переляканий кабан." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_idle] {name}, сиджу, пильную. А ти що робиш?", "[sh_idle] {name}, я сиджу й пильную. А ти що робиш? Окрім того, що стоїш." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_idle] Тихо. Я навіть пір'я встиг почистити, {name}.", "[sh_idle] Тихо. Я встиг почистити пір'я, обдумати три саги й зневіритися в тобі, {name}." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_idle] {name}, з плеча видно все. Нічого цікавого, на жаль.", "[sh_idle] {name}, з плеча видно все. Нічого цікавого. Включно з тобою." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_idle] {name}, плече в тебе тепле. Дякую.", "[sh_idle] {name}, плече в тебе тепле. Мабуть, найкраще, що в тобі є." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_kill] Я все бачив з плеча, {name}. Чисто.", "[sh_kill] Я все бачив з плеча, {name}. Чисто. Майже не соромно." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_kill] Одним менше, {name}. Я тримався міцно.", "[sh_kill] Одним менше, {name}. Я тримався міцно — з тобою інакше не можна." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_kill_many] Скільки крові, {name}... Моє пір'я все в бризках.", "[sh_kill_many] Скільки крові, {name}... Пір'я в бризках, а сторінок у літописі все менше." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_near_death] {name}, я відчув, як тебе хитнуло. Не лякай мене так.", "[sh_near_death] {name}, тебе хитнуло так, що я вже подумки писав некролог. Короткий." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_near_death] Я вчепився в тебе з усіх сил, {name}. Бережи себе.", "[sh_near_death] Я вчепився в тебе з усіх сил, {name}. Не через тебе — падати неприємно." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_boss] {name}, я бачив усе з першого ряду! Неймовірно!", "[sh_boss] {name}, я бачив усе з першого ряду. Неймовірно. Я, чесно, ставив на нього." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_build] {name}, звідси видно: кут кривий. Але ти не переймайся.", "[sh_build] {name}, звідси видно: кут кривий. Не переймайся, нащадки вирішать, що так задумано." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_sail] Вітер у пір'я, {name}. Обожнюю сидіти на тобі в морі.", "[sh_sail] Вітер у пір'я, {name}. Гарно. Якби ще стерновий знав, куди пливе." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_swim] {name}, тримай голову вище. Я не вмію плавати.", "[sh_swim] {name}, тримай голову вище. Я плавати не вмію, а ти, як бачу, теж." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[sh_weather_rain] {name}, я мокну в тебе на плечі. Може, під дах?", "[sh_weather_rain] {name}, я мокну в тебе на плечі. Розумні птахи зараз під дахом. Я, вочевидь, не з них." }, // 1.3: Hugin's shoulder lines in his own voice
            { "[boss] Кра! {name}, це було люто, бля. Я пишаюся тобою.", "[boss] Кра! {name}, це було люто. Навіть я на мить забув, що це ти." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[death_creature] {name}, тебе вбив {killer}. Я все бачив, бля.", "[death_creature] {name}, тебе вбив {killer}. Я все бачив і вже записав — без прикрас." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[death_drown] {name}, буль-буль, бля. Плавати вчитимемося?", "[death_drown] {name}, буль-буль. Плавати вчитимемося, чи знову по дну?" }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[death_tree] {name}, дерево перемогло. Лісоруб, бля.", "[death_tree] {name}, дерево перемогло. Лісоруб року, без сумніву." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[friendly] {name}, ей, бля, свої ж! Дивись, кого б'єш.", "[friendly] {name}, свої ж! Чи ти й друзів від ворогів не відрізняєш?" }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[kill_many] {name}, різанина, бля. Вороняча радість.", "[kill_many] {name}, різанина. Ворони тобі вдячні — я передам." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[encumbered] {name}, ти ледве повзеш, бля. Викинь половину.", "[encumbered] {name}, ти повзеш, як равлик із хатою на спині. Викинь половину." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[mine] {name}, пил на тобі аж до вух. Рудокоп, бля.", "[mine] {name}, пил на тобі аж до вух. Гноми прийняли б тебе за свого." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[night] {name}, ніч надворі. Йди до вогню, бля.", "[night] {name}, ніч надворі. Розумні вже біля вогню. А ти?" }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[weather_rain] {name}, мокро, бля. Пір'я змокло до нитки.", "[weather_rain] {name}, мокро. Моє пір'я змокло до нитки, але тобі ж байдуже, правда?" }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[death_creature] {name}, {killer} тебе поклав. А на твоєму рахунку {killer_genpl} вже {killer_kills}. Сором, бля.", "[death_creature] {name}, {killer} тебе поклав. А на твоєму рахунку {killer_genpl} вже {killer_kills}. Мені соромно перед Одіном." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[death_many] {name}, {deaths_all} за все життя, і кілька з них — прямо зараз. Рекорд, бля.", "[death_many] {name}, {deaths_all} за все життя, і кілька з них — прямо зараз. Рекорд, який краще не повторювати." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[death_tree] {name}, дерева вбивають тебе вже {cause_times}. Ліс мстить, бля.", "[death_tree] {name}, дерева вбивають тебе вже {cause_times}. Ліс пам'ятає кожну сокиру." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[build] {name}, у базі вже {base_hours} твого життя. Домосід, бля.", "[build] {name}, у базі вже {base_hours} твого життя. Домосід, хоч і вікінг." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "[cook] {name}, страв з'їдено вже {food_all}. Кухар, бля, і їдець.", "[cook] {name}, страв з'їдено вже {food_all}. Кухар і їдець в одній особі." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "Хугін — наївний хуйло.", "Хугін — наївне хуйло." }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "Слухай, їбаний ти невдахо,", "Слухай, {їбаний|їбана} ти невдахо," }, // 1.3: Hugin's own voice, no swearing; small fixes
            { "мудрість не живе на порожній шлунок.", "на голодний шлунок і думки не думаються." }, // 1.3: a clumsy hunger line
            { "~3 Будь ласка.", "~3 Пригостиш, друже?" }, // 1.3: a clumsy hunger line
            { "[dome] {name}, стань під крило. Хай чекають.", "[dome] ~2 {name}, стань під крило. Хай чекають." }, // 1.3: the dome lines by trust
            { "[dome] Під моїм крилом тихо, {name}. А ті хай нюхають край.", "[dome] ~2 Під моїм крилом тихо, {name}. А ті хай нюхають край." }, // 1.3: the dome lines by trust
            { "[dome] {name}, дихай. Вони кружлятимуть, але сюди не ввійдуть.", "[dome] ~2 {name}, дихай. Вони кружлятимуть, але сюди не ввійдуть." }, // 1.3: the dome lines by trust
            { "[dome] Перепочинь, {name}. Тільки пам'ятай: крила не вічні.", "[dome] ~23 Перепочинь, {name}. Тільки пам'ятай: крила не вічні." }, // 1.3: the dome lines by trust
            { "[dome] {name}, тут не б'ються. Ні ти, ні вони.", "[dome] ~2 {name}, тут не б'ються. Ні ти, ні вони." }, // 1.3: the dome lines by trust
            { "[dome] Грійся, {name}. Коли купол спаде — будь {готовий|готова}.", "[dome] ~2 Перечекай, {name}. Коли купол спаде — будь {готовий|готова}." }, // 1.3: the dome lines by trust
            { "[dome] {name}, я накрив тебе рунами. Подумай, що робитимеш далі.", "[dome] ~23 {name}, я накрив тебе рунами. Подумай, що робитимеш далі." }, // 1.3: the dome lines by trust
            { "[dome] Бач, {name}, як ходять колом? Чекають. Хай чекають.", "[dome] ~2 Бач, {name}, як ходять колом? Чекають. Хай чекають." }, // 1.3: the dome lines by trust
            { "{name}, поруч {seen_enemy}. Зброю б напоготові.", "{name}, поруч {seen_enemy}. Тримай зброю напоготові." }, // 1.2.1: smoother lines
            { "{count} трупів, бля. Оце розмова!", "трупів уже {count}, бля. Оце я розумію!" }, // 1.2.1: smoother Munin lines
            { "{name}, ти, сраний недоумку,", "{name}, сраний ти недоумку," }, // 1.2.1: smoother Munin lines
            { "смерть тобі личить. Але живим ти кращий.", "смерть тобі личить. Але {живим ти кращий|живою ти краща}." }, // the player's gender (1.2 review)
            { "ну ти й розійшовся! Ціла гора тіл.", "ну ти й {розійшовся|розійшлася}! Ціла гора тіл." }, // the player's gender (1.2 review)
            { "з підземелля живим? Неочікувано.", "з підземелля {живим|живою}? Неочікувано." }, // the player's gender (1.2 review)
            { "довбограю ти безрукий.", "довбограю ти {безрукий|безрука}." }, // the player's gender (1.2 review)
            { "Жертовні камені, три трофеї Ейктюра.", "Жертовні камені, два трофеї Ейктюра." }, // the player's gender (1.2 review)
            { "у тумані ти ще безпорадніший.", "у тумані ти ще {безпорадніший|безпорадніша}." }, // the player's gender (1.2 review)
            { "Ти добрий, хоч і ховаєш це.", "Ти {добрий|добра}, хоч і ховаєш це." }, // the player's gender (1.2 review)
            { "без жратви ти ще слабший. Хоча куди вже.", "без жратви ти ще {слабший|слабша}. Хоча куди вже." }, // the player's gender (1.2 review)
            { "море, вітер, а ти все одно тупий.", "море, вітер, а ти все одно {тупий|тупа}." }, // the player's gender (1.2 review)
            { "Пам'ятай, звідки ти прийшов.", "Пам'ятай, звідки ти {прийшов|прийшла}." }, // the player's gender (1.2 review)
            { "Друзі кажуть, що ти незамінний.", "Друзі кажуть, що ти {незамінний|незамінна}." }, // the player's gender (1.2 review)
            { "Без неї ти ще жалюгідніший.", "Без неї ти ще {жалюгідніший|жалюгідніша}." }, // the player's gender (1.2 review)
            { "Одін тримає тебе живим чисто", "Одін тримає тебе {живим|живою} чисто" }, // the player's gender (1.2 review)
            { "[kill_many] Ну ти й розійшовся!", "[kill_many] Ну ти й {розійшовся|розійшлася}!" }, // the player's gender (1.2 review)
            { "[swim] Ти мокрий до кісток.", "[swim] Ти {мокрий|мокра} до кісток." }, // the player's gender (1.2 review)
            { "Якщо ти відійшов попісяти", "Якщо ти {відійшов|відійшла} попісяти" }, // the player's gender (1.2 review)
            { "Ти, схоже, дуже старанний учень.", "Ти, схоже, {дуже старанний учень|дуже старанна учениця}." }, // the player's gender (1.2 review)
            { "Ти потроху стаєш небезпечним.", "Ти потроху стаєш {небезпечним|небезпечною}." }, // the player's gender (1.2 review)
            { "Ти тепер бадьорий, як молодий вовк.", "Ти тепер {бадьорий|бадьора}, як молодий вовк." }, // the player's gender (1.2 review)
            { "Я мокрий, ти мокрий, і тільки жаби щасливі.", "Я мокрий, ти {мокрий|мокра}, і тільки жаби щасливі." }, // the player's gender (1.2 review)
            { "Легше, легше... Дихай рівно, я підкажу дорогу.", "Дихай рівно, {name}, я підкажу ритм. Ноги втомлюватимуться менше." }, // the light step said what it does
            { "{name}, тримай ритм. Стежка сама ляже під ноги.", "Легше, {name}. Біжи моїм кроком — і сил вистачить надовго." }, // the light step said what it does
            { "Бачу добру дорогу, {name}. Біжи, не поспішаючи.", "{name}, не трать подих дарма. Я поведу, а ти відпочивай на ходу." }, // the light step said what it does
            { "Вітер у спину, {name}. Я його попросив.", "Вдих — на два кроки, видих — на два. Так, {name}, і подих не зіб'єш." }, // the light step said what it does
            { "{name}, не женися так. Мандрівка — не втеча.", "{name}, тримай мій ритм — і втома тебе не наздожене." }, // the light step said what it does
            { "Голови з'їдено, клятву дано.", "Трофеї з'їдено, клятву дано." },
            { "Голови босів смачні, бля.", "Трофеї босів смачні, бля." },
            { "ти горів так яскраво", "ти {горів|горіла} так яскраво" },
            { "{name}, ти став сильнішим. Відчуваєш?", "{name}, ти {став сильнішим|стала сильнішою}. Відчуваєш?" },
            { "{name}, мокрий до кісток. Човен не пробував?", "{name}, {мокрий|мокра} до кісток. Човен не {пробував|пробувала}?" },
            { "знову нишпориш по скринях? Знайшов щось, крім павуків?", "знову нишпориш по скринях? {Знайшов|Знайшла} щось, крім павуків?" },
            { "Ти не забув, що я тут, {name}?", "Ти не {забув|забула}, що я тут, {name}?" },
            { "{name}, ти так замахнувся, що я ледь не злетів.", "{name}, ти так {замахнувся|замахнулася}, що я ледь не злетів." },
            { "ти б уже тричі спіткнулося", "ти б уже тричі {спіткнувся|спіткнулася}" },
            { "просто перевіряю, чи ти живий. Живий. Добре.", "просто перевіряю, чи ти {живий|жива}. {Живий|Жива}. Добре." },
            { "{name}, ну завалив {boss_gen}, і шо?", "{name}, ну {завалив|завалила} {boss_gen}, і шо?" },
            { "{name}, полетіло, сука, як мішок гівна.", "{name}, {полетів|полетіла}, сука, як мішок гівна." },
            { "{name}, засмажився, як курка. Тупа курка.", "{name}, {засмажився|засмажилася}, як курка. Тупа курка." },
            { "{name}, нажерлося отрути, мудило?", "{name}, {нажерся|нажерлася} отрути, мудило?" },
            { "{name}, знову здохло, сміття.", "{name}, знову {здох|здохла}, сміття." },
            { "{name}, ледь не здохло, нікчемо.", "{name}, ледь не {здох|здохла}, нікчемо." },
            { "Був нулем, став нулем з плюсиком.", "{Був|Була} нулем, {став|стала} нулем з плюсиком." },
            { "{name}, полізло {biome_in}?", "{name}, {поліз|полізла} {biome_in}?" },
            { "{name}, вилізло з нори, як щур.", "{name}, {виліз|вилізла} з нори, як щур." },
            { "{name}, гепнулось, як мішок картоплі.", "{name}, {гепнувся|гепнулася}, як мішок картоплі." },
            { "{name}, одного вбило і пишається.", "{name}, одного {вбив|вбила} і пишається." },
            { "Своє дупло б прорубало.", "Своє дупло б {прорубав|прорубала}." },
            { "{name}, виспалось, ледащо? Світ чекав, а ти хропло.", "{name}, {виспався|виспалася}, ледащо? Світ чекав, а ти {хропів|хропла}." },
            { "{name}, пробігло {km} і нічого не досягло.", "{name}, {пробіг|пробігла} {km} і нічого не {досяг|досягла}." },
            { "вночі шляєшся, дурне.", "вночі шляєшся, {дурню|дурна}." },
            { "{name}, засмажилось уже {cause_times}.", "{name}, {засмажився|засмажилася} уже {cause_times}." },
            { "{name}, нажерлося отрути вже {cause_times}.", "{name}, {нажерся|нажерлася} отрути вже {cause_times}." },
            { "а досі тупе, як пень.", "а досі {тупий|тупа}, як пень." },
            { "Сьогодні ледь не обісралося.", "Сьогодні ледь не {обісрався|обісралася}." },
            { "{name}, намотало вже {km_all}", "{name}, {намотав|намотала} вже {km_all}" },
            { "Ледь не здохло, а я ж на твоєму плечі", "Ледь не {здох|здохла}, а я ж на твоєму плечі" },
            { "{name}, ти що, здуріло?", "{name}, ти що, {здурів|здуріла}?" },
            { "Ледь живе, {name}?", "Ледь {живий|жива}, {name}?" },
            { "{name} без тебе вже давно б заблукало.", "{name} без тебе вже давно б {заблукав|заблукала}." },
            { "ледь живе, бля.", "ледь {живий|жива}, бля." },
            { "{name} без тебе давно б здохло.", "{name} без тебе давно б {здох|здохла}." },
            { "що ти забуло {biome_here_in}, бовдуре?", "що ти {забув|забула} {biome_here_in}, бовдуре?" },
            { "До вогню, дурне.", "До вогню, {дурню|дурна}." },
            { "знову здохло, бовдуре.", "знову {здох|здохла}, бовдуре." },
            { "ти ледь не здохло, бля.", "ти ледь не {здох|здохла}, бля." },
            { "гепнулося, як мішок з гівном.", "{гепнувся|гепнулася}, як мішок з гівном." },
            { "мокре, як щур.", "{мокрий|мокра}, як щур." },
            { "сильнішим стало. Розумнішим — ні.", "{сильнішим став|сильнішою стала}. {Розумнішим|Розумнішою} — ні." },
            { "Підгорів трохи, га?", "{Підгорів|Підгоріла} трохи, га?" },
            { "Зігрівся, бурулько?", "{Зігрівся|Зігрілася}, бурулько?" },
            { "[level] Ти став сильнішим.", "[level] Ти {став сильнішим|стала сильнішою}." },
            { "Агов! Ти живий?", "Агов! Ти {живий|жива}?" },
            { "Ти там заснув, чи що?", "Ти там {заснув|заснула}, чи що?" },
            { "Ти бачив, кого б'єш?", "Ти {бачив|бачила}, кого б'єш?" },
            { "[death_creature] Повернувся? Молодець.", "[death_creature] {Повернувся|Повернулася}? Молодець." },
            { "Бачив, як ти пішов на {killer_gen}", "Бачив, як ти {пішов|пішла} на {killer_gen}" },
            { "як розібрався з великим воїном. Ну, з воїном.", "як розібрався з {великим воїном. Ну, з воїном|великою воїтелькою. Ну, з воїтелькою}." },
            { "що ти «стратегічно відступив» від", "що ти «стратегічно {відступив|відступила}» від" },
            { "Ти там що, абонемент купив?", "Ти там що, абонемент {купив|купила}?" },
            { "Ти довів, що гравітація працює.", "Ти {довів|довела}, що гравітація працює." },
            { "Ти засмажився рівненько", "Ти {засмажився|засмажилася} рівненько" },
            { "Ти позеленів, як болотяний слиз.", "Ти {позеленів|позеленіла}, як болотяний слиз." },
            { "Ти зрубав дерево. Дерево зрубало тебе.", "Ти {зрубав|зрубала} дерево. Дерево зрубало тебе." },
            { "Ти був на волосину від Гель", "Ти {був|була} на волосину від Гель" },
            { "Бачив, як ти танцював зі смертю.", "Бачив, як ти {танцював|танцювала} зі смертю." },
            { "Живий! Ледве, але живий. Може, хоч зараз поїси, героє?", "{Живий|Жива}! Ледве, але {живий|жива}. Може, хоч зараз поїси, {героє|героїне}?" },
            { "Ти заслужив свою медовуху.", "Ти {заслужив|заслужила} свою медовуху." },
            { "Я перевірив — живий.", "Я перевірив — {живий|жива}." },
            { "Задумався? Чи заснув із відкритими очима?", "{Задумався|Задумалася}? Чи {заснув|заснула} із відкритими очима?" },
            { "Ти стояв нерухомо, як статуя.", "Ти {стояв|стояла} нерухомо, як статуя." },
            { "А ти просто завис, бля.", "А ти просто {завис|зависла}, бля." },
            { "а ти навіть не поворухнувся.", "а ти навіть не {поворухнувся|поворухнулася}." },
            { "Ти влучив у свого.", "Ти {влучив|влучила} у свого." },
            { "Ти там прибирання влаштував?", "Ти там прибирання {влаштував|влаштувала}?" },
            { "Бачив, як ти розібрався з {top_ins}.", "Бачив, як ти {розібрався|розібралася} з {top_ins}." },
            { "Ти поранив {prey_gen}", "Ти {поранив|поранила} {prey_gen}" },
            { "який ти мазун.", "{який ти мазун|яка ти мазунка}." },
            { "Ти так гнався за ним", "Ти так {гнався|гналася} за ним" },
            { "Бачив, як ти драпав від {foe_gen}.", "Бачив, як ти {драпав|драпала} від {foe_gen}." },
            { "Ти відступив від {foe_gen} так швидко", "Ти {відступив|відступила} від {foe_gen} так швидко" },
            { "куди подівся його суперник.", "куди {подівся його суперник|поділася його суперниця}." },
            { "Сподіваюся, ти взяв більше їжі", "Сподіваюся, ти {взяв|взяла} більше їжі" },
            { "Ти виліз із підземелля і навіть живий.", "Ти {виліз|вилізла} із підземелля і навіть {живий|жива}." },
            { "Ти так гепнувся, що я аж підскочив.", "Ти так {гепнувся|гепнулася}, що я аж підскочив." },
            { "Ти стільки плавав, що риби", "Ти стільки {плавав|плавала}, що риби" },
            { "Ти нарубав стільки, що ліс", "Ти {нарубав|нарубала} стільки, що ліс" },
            { "Ти копав так завзято", "Ти {копав|копала} так завзято" },
            { "Ти пролетів крізь портал", "Ти {пролетів|пролетіла} крізь портал" },
            { "Ти стільки всього наробив", "Ти стільки всього {наробив|наробила}" },
            { "Добре поспав? Сни про Вальгаллу", "Добре {поспав|поспала}? Сни про Вальгаллу" },
            { "Ти обібрав кущі", "Ти {обібрав|обібрала} кущі" },
            { "Знайшов щось вартісне, крім павуків?", "{Знайшов|Знайшла} щось вартісне, крім павуків?" },
            { "Стільки скринь відкрив…", "Стільки скринь {відкрив|відкрила}…" },
            { "Бачив, як ти гладив свою тварину. Навіть у суворого воїна є м'яке серце.", "Бачив, як ти {гладив|гладила} свою тварину. Навіть у {суворого воїна|суворої воїтельки} є м'яке серце." },
            { "Як ти там ще не засмажився?", "Як ти там ще не {засмажився|засмажилася}?" },
            { "щоб ти знав, де тебе потім шукати.", "щоб ти {знав|знала}, де тебе потім шукати." },
            { "Відпочинь трохи, ти заслужив.", "Відпочинь трохи, ти {заслужив|заслужила}." },
            { "Вирішив привітатися, поки ти живий.", "Вирішив привітатися, поки ти {живий|жива}." },
            { "Добре, що ти живий.", "Добре, що ти {живий|жива}." },
            { "Кожне дерево, яке ти зрубав,", "Кожне дерево, яке ти {зрубав|зрубала}," },
            { "Не ходи сам туди, де не видно дна.", "Не ходи {сам|сама} туди, де не видно дна." },
            { "Я бачив, як ти бився.", "Я бачив, як ти {бився|билася}." },
            { "Ти сильніший, ніж був учора.", "Ти {сильніший, ніж був|сильніша, ніж була} учора." },
            { "світ, який ти зробив безпечнішим", "світ, який ти {зробив|зробила} безпечнішим" },
            { "чи ти ще не здох.", "чи ти ще не {здох|здохла}." },
            { "Ти ще живий? Шкода.", "Ти ще {живий|жива}? Шкода." },
            { "для довбойоба, який тричі здох від кабана.", "{для довбойоба, який тричі здох|для довбойобки, яка тричі здохла} від кабана." },
            { "як ти, бля, тікав від шиї.", "як ти, бля, {тікав|тікала} від шиї." },
            { "ти б освітлював увесь Чорний ліс.", "ти б {освітлював|освітлювала} увесь Чорний ліс." },
            { "але більшого дебіла, ніж ти, так і не знайшов.", "але {більшого дебіла|більшої дебілки}, ніж ти, так і не знайшов." },
            { "коли ти вперше взяв сокиру.", "коли ти вперше {взяв|взяла} сокиру." },
            { "Я бачив, як ти ховався від дощу", "Я бачив, як ти {ховався|ховалася} від дощу" },
            { "Бо ти сам із цим чудово справляєшся, сраний ти недоумку.", "Бо ти {сам|сама} із цим чудово справляєшся, {сраний ти недоумку|срана ти недоумко}." },
            { "ти б там уже був королем, сука.", "ти б там уже {був королем|була королевою}, сука." },
            { "Ти нюхав болотяну жабу", "Ти {нюхав|нюхала} болотяну жабу" },
            { "так смердить, засранцю?", "так смердить, {засранцю|засранко}?" },
            { "а ти взагалі пробував не бути довбойобом?", "а ти взагалі {пробував|пробувала} не бути {довбойобом|довбойобкою}?" },
            { "Ти й так ціле, нема чого латати", "Ти й так {цілий|ціла}, нема чого латати" },
            { "що ти його зняло.", "що ти його {зняв|зняла}." },
            { "\nСлухай-но:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nЗапис у літописі:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nА знаєш, що записано в моєму літописі?\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nЗгадалося:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nІз літопису:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 Нагадаю, бо сам ти не згадаєш:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 Для протоколу:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 Повчальна історія:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 Одна історія:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 Знаєш, про що я думаю?\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 Слухай, друже:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 Згадав одне, слухай:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nЯ записав кожне слово.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nНащадки читатимуть і дивуватимуться.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nЛітопис не бреше.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 Ти, звісно, вже {забув|забула}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 Деякі уроки ти засвоюєш повільно.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 Добрі були часи. Дурні, але добрі.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 Цікаво, чи ти змінився. Мабуть, ні.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 Я радий, що був поруч.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 Таке не забувається.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nЯ вже розповідав, але повторю:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nТи це вже {чув|чула}, та послухай ще раз:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nЗнову про те саме, так:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 Повторю, бо з першого разу ти не {засвоїв|засвоїла}:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 Пам'ятаєш, я вже казав?\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} — твоя перша смерть. Від {m_who_gen}, {m_where_in}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпершу твою смерть я бачив {m_where_in}. {m_who}, {m_when}. Я тоді ще думав, що це випадковість.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nперший, хто відправив тебе до Вальгалли, — {m_who}. {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 перша смерть — від {m_who_gen}. Початок славної традиції.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 {m_who} — твій перший суворий учитель. {m_when}, {m_where_in}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 пам'ятаю твою першу смерть. Від {m_who_gen}. Я тоді вперше за тебе злякався.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {помер|померла} вперше — через {m_cause}. Не від меча, не від ікла. Через {m_cause}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nтвоя перша смерть сталася {m_where_in}, через {m_cause}. Епічно, нічого не скажеш.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 перша смерть — через {m_cause}. Скальди про таке не співають.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 першу смерть ти {отримав|отримала} через {m_cause}. Бувають гірші початки. Небагато.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 перша твоя смерть була безглузда — через {m_cause}. Але ти {встав|встала}. Це головне.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} {m_where_in} ти {впав|впала} від {m_who_gen}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who}. {m_where_in}, {m_when}. Ти, мабуть, і не пам'ятаєш, а я пам'ятаю.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю, як після {m_who_gen} від тебе лишилися самі черевики. {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_when} — смерть від {m_who_gen}. Я записав її під заголовком «Неуважність».\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 {m_where_in} {m_when} ти {загинув|загинула} від {m_who_gen}. Сподіваюся, ти щось із цього {виніс|винесла}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} я бачив, як ти {падав|падала} під {m_who_ins}. Не люблю це згадувати.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} {m_where_in} ти {загинув|загинула} через {m_cause}. Я досі не знаю, як це вдалося.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nсмерть через {m_cause}, {m_where_in}. Це було {m_when}. Я не сміявся. Вголос.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_when} ти {примудрився|примудрилася} загинути через {m_cause}. Талант.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 через {m_cause}, {m_when}. Буває. З тобою — частіше, ніж з іншими.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nось тут ти й {помер|померла}. {m_when}. Місце пам'ятає, і я теж.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nми проходимо місце твоєї смерті. {m_when}. Ступай обережніше.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 знайоме місце? Тут ти {лежав|лежала} {m_when}. Не повторюй.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 тут, {m_when}, усе для тебе скінчилося. Ненадовго, правда.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 мені не подобається це місце. {m_when} я тут тебе втратив.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nтут ти {помирав|помирала} уже {m_times}. Може, оминемо це місце?\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_times} на одному місці. Ти тут що, щось {загубив|загубила}?\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_times} — і все тут. Це вже не випадковість, це звичка.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 це місце забирало тебе {m_times}. Обійдімо, прошу.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nвід {m_who_gen} ти {помирав|помирала} вже {m_times}. Я веду рахунок.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who} — твоє прокляття. {m_times} — і щоразу та сама пика.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 рахунок з {m_who_ins}: {m_times} не на твою користь.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 {m_who}... {m_times}. Я б на твоєму місці обходив цю звірину десятою дорогою.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_who}... Я пам'ятаю кожен раз із {m_times}. Колись поквитаємося.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} {m_where_in} у тебе лишилось {m_hp} здоров'я. Я вже прощався.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_hp} здоров'я. Стільки лишилось після {m_who_gen}, {m_when}. Ти тоді {вижив|вижила} дивом.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nнайближче до смерті ти {був|була} {m_when}: {m_hp} здоров'я. Я пам'ятаю цю цифру.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_hp} здоров'я, {m_when}. Удача — не стратегія, хай би ти це вже {зрозумів|зрозуміла}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 {m_hp} здоров'я. Відтоді я сиджу на тобі трохи міцніше.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_hp} здоров'я... {m_when} я думав, що ти не встанеш. Ніколи більше так не роби.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {здолав|здолала} {m_who_acc}. Я бачив це на власні очі.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who}, {m_where_in}, {m_when}. Ця сторінка літопису — одна з небагатьох, де ти виглядаєш гідно.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who_acc} ти {здолав|здолала} вже {m_times}. Я пам'ятаю перший — {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_when} ти {здолав|здолала} {m_who_acc}. Досі не розумію як, але записав.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 пам'ятаю бій з {m_who_ins}. {m_when}. Ти тоді {тримався|трималася} краще, ніж зазвичай.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} — {m_who}. Я тоді вперше подумав, що ти справді щось можеш.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти й {m_friend} здолали {m_who_acc}. Я пам'ятаю обох.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who}, {m_when}. Поруч тоді були: {m_friend}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ми з тобою вперше опинилися {m_where_in}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_where_in} ми вперше {m_when}. Ти тоді {йшов|йшла} так, наче там безпечно. Наївно.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 пам'ятаю наш перший день {m_where_in}. {m_when}. Ти {крутив|крутила} головою, як сова.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} — вперше {m_where_in}. Пам'ятаю, як ти тоді {завмер|завмерла}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_where_in} ти {прожив|прожила} перший день менше, ніж я встиг роззирнутися.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ми вперше потрапили {m_where_in}, і ти одразу там {помер|померла}. Гостинність по-місцевому.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 вперше {m_where_in} — і одразу смерть. Ти вмієш справити враження.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_where_in} нас тоді зустріли жорстоко. Але ти {повернувся|повернулася}. Я це ціную.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} — твоє перше підземелля {m_where_in}. Темно, тісно, і ти — з факелом і надією.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю перше підземелля {m_where_in}. Я сидів у тебе на плечі й рахував виходи.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 перше підземелля {m_where_in}, {m_when}. Ти {заблукав|заблукала} двічі. Я рахував.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} ми разом спустилися під землю {m_where_in}. Мені було лячно, але не кажи нікому.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} {m_where_in} ти {поклав|поклала} {m_count} за хвилину. Я досі витираю пір'я.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nтвій рекорд — {m_count} за хвилину. Більше, ніж я встиг записати.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_count} за хвилину. Це був не бій, а жнива.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} ти {бився|билася}, як Тор. {m_count} за хвилину. Я пишався, хоч і мовчав.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {провів|провела} корабель {m_km} без зупинки. Моє пір'я досі пахне сіллю.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nнайдовший твій похід морем — {m_km}. Я сидів на щоглі й мріяв про суходіл.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_km} морем, {m_when}. І жодного разу не {сів|сіла} на мілину. Майже.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_m}. Стільки ти {летів|летіла} без крил {m_when}. Ікар позаздрив би.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_m} падіння, {m_when}. Земля тоді тебе не прийняла. Шкода — це був би гарний урок.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {приручив|приручила} {m_who_gen}. Хоч хтось тебе слухається.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю, як ти {приручив|приручила} {m_who_gen}. {m_when}. Терпіння в тебе тоді було більше, ніж зазвичай.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_who} тебе слухається. Я — ні. Пам'ятай різницю.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_who_gen} ти {приручив|приручила} {m_when}. Добре серце в тебе, хоч і ховаєш.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {поставив|поставила} ліжко {m_where_in} і {назвав|назвала} це домом.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю твій перший дім {m_where_in}. Кривий, але свій.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_when} — дім {m_where_in}. Стіни ти тоді {ставив|ставила} так, наче вітру не існує.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти вперше {мандрував|мандрувала} поруч з тим, кого звуть {m_friend}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_friend}... пам'ятаю ваш перший спільний день. {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_min} я пролежав у твоєму надгробку, поки ти по мене {прийшов|прийшла}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю, як ти {біг|бігла} до свого надгробка. {m_min}. Я чекав.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_min} у могилі разом із твоїми речами. Темрява, сморід і твої шкарпетки.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти вперше {тримав|тримала} в руках {m_item}. Я бачив, як блищали твої очі.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю день, коли ти {знайшов|знайшла} {m_item}: {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_item}, {m_when}. Ти тоді {думав|думала}, що вже {непереможний|непереможна}. Помилка.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} — {m_item}. Ти тоді {радів|раділа}, як дитина. Я теж трохи.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {помер|померла} {m_times} за один день. Найгірший день у моєму літописі.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nнайчорніший день — {m_when}: смерть {m_times} за одну добу.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_times} за добу, {m_when}. Валькірії тоді, мабуть, просто чекали біля тебе.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nсмертей у тебе вже {deaths_all}. Я записую кожну.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nворогів на твоєму рахунку — {kills_all}. Літопис товстішає.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nти {пройшов|пройшла} {km_all}. І досі губишся.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nтвій найдовший час без смерті — {streak_max}. Є куди рости.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nбосів на рахунку {bosses_all}. Літопис прихильний до тебе. Поки що.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nстрибків — {jumps_all}. Я рахував, бо більше не було чим зайнятися.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nу базі ти {провів|провела} {base_hours}. Вікінг-домосід.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nдерев повалено {trees_all}. Ліси тебе бояться.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nсвій надгробок ти {відкривав|відкривала} вже {tombs_times}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {deaths_all}, і щоразу ти {вставав|вставала}. Я це ціную.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} у тебе лишилось {m_hp} здоров'я. Від {m_who_gen}. Я тоді вже підбирав слова для некролога.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_hp} здоров'я, {m_when}. Ти, мабуть, уже й {забув|забула}, а я ні.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_when} від {m_who_gen} твоя сага мало не скінчилася. {m_hp} здоров'я. Нічого героїчного.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 {m_when} ти ледь {вибрався|вибралася}: {m_hp} здоров'я. Добре, що {вибрався|вибралася}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {бився|билася} з {m_who_ins} так, що {втратив|втратила} {m_lost} здоров'я. Але {переміг|перемогла}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю твою бійку з {m_who_ins}, {m_when}. Некрасиво, зате результативно.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_when} ти ледь {подужав|подужала} {m_who_gen}. Ледь. Запам'ятай це слово.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} здобич утекла: {m_who}. Я бачив, як вона сміялася.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who}, {m_when}. Поранена здобич — і втекла. Від тебе.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_when} здобич пішла від тебе з твоєю стрілою в боці. Подарунок, мабуть.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {тікав|тікала} від {m_who_gen}. Я все бачив з плеча. Швидко {біг|бігла}, треба визнати.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю, як ти {драпав|драпала} від {m_who_gen}. {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_when} від {m_who_gen} ти {тікав|тікала}, як заєць. Я записав. Із подробицями.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 {m_when} ти {відступив|відступила} від {m_who_gen}. Тактично, звісно.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {поклав|поклала} {m_who_gen} — {m_stars}. Таких я записую окремо.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who} на {m_stars}, {m_when}. Гідна сторінка.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_stars} — і ти {впорався|впоралася}. Я здивований. Записав.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти вперше {вбив|вбила} {m_who_gen}. Нове ім'я в моєму списку.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю першу здобич цього роду: {m_who}, {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_who_genpl} на твоєму рахунку тепер більше, ніж нуль. Не зазнавайся, їх ще багато.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} ти вперше {подужав|подужала} {m_who_gen}. Я пам'ятаю твоє обличчя тоді.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nСлухай сюди.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nЗгадав, бля:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 Нагадаю, бо ти ж, бля, забудеш:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 Згадай-но, бовдуре:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 Знаєш, що в мене в голові сидить?\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 Слухай, бля:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nЯ все пам'ятаю.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nТаке не стирається, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nМунін — це пам'ять, сука.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 І ще довго тобі це згадуватиму.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 Ото було, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 Добре, що ми це пережили, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 Гарні були часи. Криваві, але гарні.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nЗнову, бля, нагадаю:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nТи {чув|чула}, але слухай ще:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 Повторюю для тупих:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 Знаю, казав уже, бля, але:\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nтвоя перша смерть — від {m_who_gen}, {m_where_in}. Я пам'ятаю, як ти {верещав|верещала}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} від {m_who_gen} ти вперше {здох|здохла}. Класика, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 перша смерть від {m_who_gen}, сука. З того й почалося твоє славне падіння.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_who}, бля... Твоя перша смерть. Я тоді ледь не порвав ту тварюку на шмаття.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпершу смерть ти {заробив|заробила} через {m_cause}. Не від ворога, бля, через {m_cause}!\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} через {m_cause}... Ганьба, сука, ганьба.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 через {m_cause} вперше {здох|здохла}. Я ржав, як кінь.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 через {m_cause}, бля. Смішно було потім. Тоді — ні.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} {m_where_in} від {m_who_gen} ти {склеїв|склеїла} ласти, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю, як від {m_who_gen} {m_where_in} тільки шматки летіли.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_who}. {m_when}. Тебе тоді розмазало, як лайно по чоботу.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~2 {m_when} ти {ліг|лягла} під {m_who_ins}. Я досі злий, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_who}, {m_when}... Я того гада ще знайду, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} {m_where_in} ти {здох|здохла} через {m_cause}. Через {m_cause}, бля!\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} — через {m_cause}. Я тоді аж пір'я на собі рвав.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 смерть через {m_cause}, сука. Достойно дурня.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 через {m_cause}, {m_when}... Більше так не роби, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nось тут ти й {здох|здохла}, бля. {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 тут ти {згнив|згнила} {m_when}. Не стій, бо повториться, бовдуре.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 не люблю це місце, бля. {m_when} я тут тебе ледь не втратив.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nтут ти {здихав|здихала} вже {m_times}, бля! Тебе сюди тягне, чи що?\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_times} на одному місці, сука. Постав тут собі пам'ятник.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_times} тут... Обійдімо, бля, по-доброму прошу.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who}, бля. Від цієї тварюки ти {здихав|здихала} {m_times}. Я веду рахунок і точу кігті.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_who}... {m_times}, бля. Помстимося разом.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} у тебе лишилось {m_hp} здоров'я, бля. {m_hp}! Я вже приміряв твій надгробок.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nнайближче до смерті — {m_hp} здоров'я, {m_where_in}. Я пам'ятаю, сука, кожну краплю.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_hp} здоров'я, {m_when}... Я тоді вперше молився, бля. Одіну.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ми порвали {m_who_acc}, бля! Оце було м'ясо!\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю {m_who_acc}... Кров, крики, кишки. Найкращий день, сука.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who_acc} ми рвали вже {m_times}, бля. А перший раз — {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_who_acc} ти {здолав|здолала} {m_when}. Чудом, бля, але таки.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_who}, {m_when}. Ми тоді були, бля, непереможні.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who_acc} ми рвали не самі: поруч були {m_friend}, бля. Всі в кровищі, як годиться.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_who_acc} ти {вбив|вбила} не {сам|сама}: поруч були {m_friend}. Без них ти б {здох|здохла}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} — {m_who} і {m_friend} поруч. Оце була зграя, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти вперше {сунувся|сунулася} {m_where_in}. Я одразу понюхав — смерть.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю перший день {m_where_in}. Сморід, бруд і ти посередині.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_where_in} ми вперше {m_when}. Ти тоді {трусився|трусилася}, як заєць.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_where_in} уперше, {m_when}. Тоді, бля, було страшно. А тепер ми там свої.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_where_in} ти {здох|здохла} в перший же день, бля. Рекорд.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 вперше {m_where_in} — і одразу в могилу. Молодець, сука.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_where_in} нас тоді вмили кров'ю. Але ми повернулися, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nперше підземелля {m_where_in}, {m_when}. Темно, як у дупі, і смердить так само.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 пам'ятаю перше підземелля {m_where_in}. Ми там, бля, усе вигребли.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {поклав|поклала} {m_count} за хвилину. Я тоді наївся досхочу, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_count} за хвилину. Єдиний раз, коли ти {був|була} хоч на щось {здатний|здатна}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_count} за хвилину, {m_when}. Я досі облизуюсь, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_km} морем, {m_when}. Мене тоді знудило тричі, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_km} хитавиці. Я тобі цього не пробачу, сука.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_km} морем... Вітер, бризки, ти за кермом. Нормально було, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {гепнувся|гепнулася} з {m_m}, бля. Я думав, з тебе млинець буде.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_m}... Не стрибай так більше, бо я в тебе вчеплюся й не відпущу.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {приручив|приручила} {m_who_gen}. Ще один рот, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_who_gen} ти {приручив|приручила}, а мене досі годуєш абияк.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_who_gen} ти {приручив|приручила} {m_when}. Нормальна звірюка, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {поставив|поставила} ліжко {m_where_in}. Нора, бля, а не дім.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 перший дім {m_where_in}. Криво, сука, як завжди.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_where_in} наша перша нора, {m_when}. Затишно було, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_friend}... {m_when} вперше разом. Нормальна зграя, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_friend} ходить з тобою ще відтоді — {m_when}. Мазохіст, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_friend} — свій, бля. Пам'ятаю ваш перший день: {m_when}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_min} я гнив у твоєму надгробку, поки ти {плентався|пленталася}, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_min} у могилі з твоїми смердючими шкарпетками. Я не забув, сука.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_min} я чекав у надгробку. Знав, що прийдеш, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти вперше {вхопив|вхопила} {m_item}. Слина аж капала, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_item}, {m_when}. Я думав, ти й проковтнеш.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_item}, {m_when}. Добрий був день, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {здох|здохла} {m_times} за день, бля. Валькірії аж посивіли.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_times} за добу, сука. Найгірший твій день, і я його пам'ятаю.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_times} за день, {m_when}... Але ти, бля, все одно {встав|встала}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {kills_all} трупів разом, бля. Гарна в нас зграя.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} у тебе лишилось {m_hp} здоров'я, бля. Від {m_who_gen}. Я вже чистив кігті для твоїх очей.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_hp} здоров'я, {m_when}... Не лякай мене так, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {рубався|рубалася} з {m_who_ins} і {втратив|втратила} {m_lost} здоров'я. Але {порвав|порвала}, бля!\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nпам'ятаю бійку з {m_who_ins}, {m_when}. Кров, кишки, усе в лайні. Краса.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} ти з {m_who_ins} {бився|билася}, як звір. Я пишався, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} від тебе втекла здобич, бля! {m_who}! Мазило!\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_who}, {m_when}, пішло з твоєю стрілою в дупі. Я досі злий.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 {m_when} здобич утекла, а ти {стояв|стояла}, як пень. Сором, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_who}, {m_when}... Утекло, бля. Нічого, я запам'ятав.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {драпав|драпала} від {m_who_gen}, аж п'ятки світилися, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~1 ссикло, бля. {m_when} від {m_who_gen} {тікав|тікала}, як заєць.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 від {m_who_gen} ти тоді {втік|втекла}, {m_when}. І добре, бля, хоч {живий|жива}.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти {завалив|завалила} {m_who_gen} на {m_stars}, бля! Оце м'ясо!\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_stars}, {m_when}. Оце ми, бля!\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n{m_when} ти вперше {вбив|вбила} {m_who_gen}, бля. Новий смак крові.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\n~3 {m_when} — перша кров {m_who_gen}. Смачно було, бля.\n", "\n" }, // 1.3: memory lines rewritten (Hugin's voice, Munin harsher)
            { "\nБля,\n", "\nАх ти ж гнидо,\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{seen} — оце здоровило, бля.\n", "\n{seen} — оце здоровило, щоб його драугр у сраку цмокнув.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nБий першим, бля.\n", "\nБий першим — трупи не ображаються.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nледь {живий|жива}, бля.\n", "\nледь {живий|жива}, мішок з дірками.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{name} — задарма, бля. Бери й неси звідси.\n", "\n{name} — задарма, як гівно на стежці. Бери й неси звідси.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nНе розслабляйся, бля.\n", "\nНе розслабляйся, бо зжеруть і не вдавляться.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nніч, бля. Час здихати в темряві.\n", "\nніч, хай їй трясця. Час здихати в темряві.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{top_genpl} треба різати більше, бля.\n", "\n{top_genpl} треба різати більше, щоб аж кишки на кущах висіли.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nМало, бля!\n", "\nМало, засранцю!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nтрупів уже {count}, бля. Оце я розумію!\n", "\nтрупів уже {count}, аж смердить! Оце я розумію!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nвід {killer_gen}? Серйозно, бля?\n", "\nвід {killer_gen}? Серйозно, курячий мозку?\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nя пам'ятаю кожну твою смерть. Кожну, бля.\n", "\nя пам'ятаю кожну твою смерть. Кожну, сука, до останнього хрипу.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nвід {attacker_gen} ти ледь не {здох|здохла}, бля.\n", "\nвід {attacker_gen} ти ледь не {здох|здохла}, смердюча онуче.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{boss} лежить, бля! Оце було м'ясо!\n", "\n{boss} лежить, кров і кишки! Оце було м'ясо!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nкриво, бля. Але стоїть.\n", "\nкриво, як ноги в п'яного ярла. Але стоїть.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nНудно, бля.\n", "\nНудно, аж пір'я лізе.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nриба, бля. Справжні воїни ловлять рибу?\n", "\nриба, хай їй грець. Справжні воїни ловлять рибу?\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nморе, бля. Тільки не потопи нас.\n", "\nморе, дідько його бери. Тільки не потопи нас.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nдощ, бля. Я мокрий через тебе.\n", "\nдощ, сцикотня небесна. Я мокрий через тебе.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nтуман, бля. Тебе не видно — і добре.\n", "\nтуман, ні хріна не видно. Тебе теж — і добре.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nсніг, бля. Замерзнеш — я сміятимусь.\n", "\nсніг, Іміреві в бороду. Замерзнеш — я сміятимусь.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{biome}, бля. Нове місце, щоб здохнути.\n", "\n{biome}, ну все, пиши заповіт. Нове місце, щоб здохнути.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nпід землею, бля. Як у могилі.\n", "\nпід землею, падло. Смердить, як у могилі.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nнудьга, бля.\n", "\nнудьга, хоч дзьобом собі око виколупуй.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nЯ засинаю, бля.\n", "\nЯ засинаю. Хропітиму тобі у вухо.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 {name}, бляха, ну ти й чмо,\n", "\n~1 {name}, ну ти й відрижка троля,\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 {name}, бля, ну ти й {красень|красуня},\n", "\n~3 {name}, трясця твоїй матері, ну ти й {красень|красуня},\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 бий, бля, не стій як пень!\n", "\n~1 бий, сучий потрох, не стій як пень!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nнарешті, бля.\n", "\nнарешті, а то кишки вже марш грали.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 Ми з тобою, бля, команда.\n", "\n~3 Ми з тобою, падло ти моє рідне, команда.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nНагодуй, бля.\n", "\nНагодуй, жмоте, бо очі виклюю.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nти хто, бля, {такий|така}?\n", "\nти хто, в дідька лисого, {такий|така}?\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~23 Слухай, {name}, бля,\n", "\n~23 Слухай-но, {name}, засранцю,\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nБля.\n", "\nТьху на тебе.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 Валькірії тебе не візьмуть — брак, бля.\n", "\n~1 Валькірії тебе не візьмуть — бракованого гівна не беруть.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 Терпимо, бля.\n", "\n~2 Терпимо, хай йому грець.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 Люблю тебе, придурка, бля.\n", "\n~3 Люблю тебе, придурка, хай мені пір'я повилазить.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{biome}, бля. Нові способи вмерти, налітай!\n", "\n{biome}, ну трясця. Нові способи вмерти, налітай!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 {biome}, бля. Ну пішли, глянемо, кого тут різати.\n", "\n~3 {biome}, Одіновим оком клянусь, гарно. Ну пішли, глянемо, кого тут різати.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nнора, бля. Як ти любиш.\n", "\nнора, смердить мишачим гівном. Як ти любиш.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 темно, як у троля в сраці. Тримайся ближче, бля.\n", "\n~3 темно, як у троля в сраці. Тримайся ближче, дурнику.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{fish} риб. Хоч щось ти вмієш, бля.\n", "\n{fish} риб. Хоч щось ти вмієш, рибоїде.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 рибалка, бля. Вікінг з вудочкою, тьху.\n", "\n~1 рибалка, щоб тобі щука носа відкусила. Вікінг з вудочкою, тьху.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nдрапаєш, як заєць, бля!\n", "\nдрапаєш, як заєць із підпаленою сракою!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nжери, бля, бо впадеш.\n", "\nжери, мішок з кістками, бо впадеш.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nкопаєш, бля, як шахтар-невдаха.\n", "\nкопаєш, як кріт із похмілля.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nгуп-гуп, бля. Голова тріщить.\n", "\nгуп-гуп, гупало. Голова тріщить.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nхитає, бля. Мене зараз виверне.\n", "\nхитає, сука. Мене зараз виверне.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 капітан, бля. Втопиш нас — заклюю.\n", "\n~1 капітан з гівна. Втопиш нас — заклюю.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 пливемо, бля. Тримай курс, не позорься.\n", "\n~3 пливемо, хай Ран подавиться. Тримай курс, не ганьбися.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 випливай уже, бо мерзну, бля.\n", "\n~3 випливай уже, бо мерзну до самої гузки.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{km}, бля. І все без толку.\n", "\n{km}, ноги до сраки {стер|стерла}. І все намарно.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 ну пішли, бля, куди там тебе несе.\n", "\n~3 ну пішли, дідько з тобою, куди там тебе несе.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nтуман, бля. Як у тебе в черепі.\n", "\nтуман, як соплі троля. Як у тебе в черепі.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 туман, бля. Не загубись, придурку.\n", "\n~3 туман, власного дзьоба не видно. Не загубись, придурку.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nдощ, бля. Мокну, як дурень.\n", "\nдощ, щоб тим хмарам луснути. Мокну, як дурень.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nсніг, бля. Сраку відморожу.\n", "\nсніг, падло біле. Сраку відморожу.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 холодно, бля. Розпали вогонь, засранцю.\n", "\n~3 холодно, аж дзьоб цокотить. Розпали вогонь, засранцю.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nгуркотить, бля! Тор злий.\n", "\nгуркотить, аж срака здригається! Тор злий.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 тримайся, бля, буря мине.\n", "\n~3 тримайся, сучий хвосте, буря мине.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nрубай, бля, ліс не винен.\n", "\nрубай, рубай, ліс не винен, що ти дурне.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 рубай, бля, хату нам треба.\n", "\n~3 рубай, до сраки ці ялинки, хату нам треба.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 жаднюга, бля. Пупок розв'яжеться.\n", "\n~1 жаднюга {засраний|засрана}. Пупок розв'яжеться.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 кинь лайно, бля, ходімо швидше.\n", "\n~3 кинь те лайно, хрін з ним, ходімо швидше.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{гепнувся|гепнулася}, бля! Шкода, що не на голову.\n", "\n{гепнувся|гепнулася}, як лантух з гноєм! Шкода, що не на голову.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 вставай, бля, не лежи.\n", "\n~3 вставай, дурна голово, не лежи, бо дзьобну.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nрівень, бля. Не зазнавайся.\n", "\nрівень, диво з-під хвоста. Не зазнавайся.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 {навчився|навчилася}, бля? Чудеса.\n", "\n~1 {навчився|навчилася}, недоноску? Чудеса.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 о, росте моя дитинка, бля.\n", "\n~3 о, росте моє засранятко.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 криву халупу ліпиш, бля.\n", "\n~1 криву халупу ліпиш, руки з сраки.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 гарно, бля. Майже як у людей.\n", "\n~3 гарно, сука. Майже як у людей.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 Кличте мене на новосілля, бля.\n", "\n~3 Клич мене на новосілля, і пива не жмоться.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 не помирай так часто, бля, серце ж не залізне.\n", "\n~3 не помирай так часто, гівнюку, серце ж не залізне.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nзнову Валькірії, знову ти. Нудно, бля.\n", "\nзнову Валькірії, знову ти. Нудно, аж срака злипається.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nБля, а ти таки виріс у моїх очах. Трохи.\n", "\nБля, а ти таки {виріс|виросла} у моїх очах. Трохи.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 Добре, бля. Тепер я тебе майже поважаю.\n", "\n~2 Ну, хай тобі грець. Тепер я тебе майже поважаю, засранцю.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 Бля, а ти таки мій.\n", "\n~3 Одіновим оком клянусь — а ти таки мій, недоноску.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 Ходімо далі, бля, разом.\n", "\n~3 Ходімо далі, разом, і хай усі вороги вдавляться.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 сплю я, бля. Відвали.\n", "\n~1 сплю я, відрижко троля. Відвали в сраку.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nне заважай, бля.\n", "\nне заважай, падло.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nДва трофеї Ейктюра, бля. Рахувати вмієш?\n", "\nДва трофеї Ейктюра, довбню. Рахувати вмієш?\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nзлодюга, бля, віддай мене моєму.\n", "\nзлодюго, віддай мене моєму, поки очі не видзьобав.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 Помстимося разом, бля!\n", "\n~3 Помстимося разом — кров і кишки!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nБий, бля, поки {живий|жива}!\n", "\nБий, падло, поки {живий|жива}!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 Тепер я злий, бля!\n", "\n~2 Тепер я злий, як драугр із похмілля!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 Я з тобою, бля!\n", "\n~3 Я з тобою, хоч сам Сурт на дорозі!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nСмачно, бля.\n", "\nСмачно, аж за вухами лящить.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 Не відставай, бля.\n", "\n~2 Не відставай, равлику обісраний.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nЖиви, бля.\n", "\nЖиви, мішок з кістками.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 Рубай, бля, рубай!\n", "\n~2 Рубай, сука, рубай!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nКАР, БЛЯ! БІЖІТЬ, ПОКИ ЦІЛІ!\n", "\nКАР, СУКИ! БІЖІТЬ, ПОКИ СРАКИ ЦІЛІ!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nХТО ТУТ ХАЗЯЇН? Я, БЛЯ!\n", "\nХТО ТУТ ХАЗЯЇН? Я, ВАШУ МАТІР!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nКров за кров, бля!\n", "\nКров за кров, кишки за кишки!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nЛедь живі — найнебезпечніші, бля!\n", "\nЛедь живі — найнебезпечніші, затям собі, бевзю!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 За тебе — хоч у Хель, бля!\n", "\n~3 За тебе — хоч у Хель, а Хель хай іде в сраку!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nмочи їх, бля!\n", "\nмочи їх, щоб кишки на гілках висіли!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 рвемо їх разом, бля!\n", "\n~3 рвемо їх разом, на шмаття!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nлети, бля, як стріла!\n", "\nлети, як стріла, трясця твоїй матері!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 тримайся, бля, віднесу!\n", "\n~3 тримайся, дурне ти пташенятко, віднесу!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nне здихай мені тут, бля.\n", "\nне здихай мені тут, смердюча онуче.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nковтай, бля, і не скигли.\n", "\nковтай, гнидо, і не скигли.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nпорвав, бля!\n", "\nпорвав, як ганчірку!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nвирвав клок, бля.\n", "\nвирвав клок м'яса, смачненько.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{target} тепер у смужечку, бля.\n", "\n{target} тепер у смужечку, як стара онуча.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 є, бля. Наступний!\n", "\n~2 є, сука. Наступний!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 ще теплий, бля.\n", "\n~2 ще теплий, аж парує.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 за тебе — хоч кого порву, бля.\n", "\n~3 за тебе — хоч самого Фенріра порву.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 разом, бля! Рвемо!\n", "\n~3 разом, кров і кишки! Рвемо!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nпрямо в пику, бля!\n", "\nпрямо в пику, хай подавиться!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{target} вже не встане, бля.\n", "\n{target} вже не встане, хоч трісни.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 красиво, бля. Аж самому сподобалось.\n", "\n~2 красиво, дідько. Аж самому сподобалось.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 це тобі подарунок, бля!\n", "\n~3 це тобі подарунок, від щирого пташиного серця!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nне встигли й пискнути, бля!\n", "\nне встигли й пискнути, недоноски!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nкігтів на всіх вистачить, бля.\n", "\nкігтів на всіх вистачить, сучари.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 нікого не лишу, бля!\n", "\n~3 нікого не лишу, ні хвоста, ні кишки!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nнікого, бля? Ти мене дражниш?\n", "\nнікого? Ти мене дражниш, хуйло?\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 я тобі не голуб, щоб так літати, бля.\n", "\n~1 я тобі не голуб, щоб дарма літати, курячий ти мозку.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 пусто, бля. Ціль давай.\n", "\n~2 пусто, хрін ночував. Ціль давай.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 нема кого рвати... а шкода, бля.\n", "\n~3 нема кого рвати... а шкода, кігті сверблять.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 Я з тобою, бля.\n", "\n~3 Я з тобою, до останнього пір'ячка.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nкігті гострішають, бля!\n", "\nкігті гострішають, хтось у сраку отримає!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 для тебе гострю, бля.\n", "\n~3 для тебе гострю, серденько смердюче.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nтепер рву сильніше, бля!\n", "\nтепер рву сильніше, бережіть сраки!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nбоги теж кровлять, бля. Я бачив.\n", "\nбоги теж кровоточать, трясця. Я бачив.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 гарна бійка, бля.\n", "\n~2 гарна бійка, аж пір'я дибки.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 разом завалили, бля!\n", "\n~3 разом завалили, аж земля здригнулась!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nбачив, як ця тварюка здохла — кігті стали гостріші, бля!\n", "\nбачив, як ця тварюка здохла — кігті аж дзвенять від гостроти!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nсила прибула, бля! Наступних порву вдвічі!\n", "\nсила прибула, в сраку всім! Наступних порву вдвічі!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nкров боса — найкраще точило для кігтів, бля.\n", "\nкров боса — найкраще точило для кігтів, це тобі не пташине гівно.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 це я його вбив. Ну майже. Тепер б'ю сильніше, бля.\n", "\n~1 це я його вбив. Ну майже. Тепер б'ю сильніше, затям, смердюко.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 гарна бійка, бля. Кігті це запам'ятали — рвуть сильніше.\n", "\n~2 гарна бійка, хай їй грець. Кігті це запам'ятали — рвуть сильніше.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 разом завалили, бля! Тепер я ще страшніший!\n", "\n~3 разом завалили, гуртом і батька легше бити! Тепер я ще страшніший!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 за тебе тепер рву ще сильніше, бля.\n", "\n~3 за тебе тепер рву ще сильніше, мій ти засранцю.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nжратва! Давай сюди, бля.\n", "\nжратва! Давай сюди, жмотяро.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 пахне, бля. Шматок мені.\n", "\n~2 пахне, аж дзьоб слиною тече. Шматок мені.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 мені найкращий шматок, бля, ти ж знаєш.\n", "\n~3 мені найкращий шматок, ти ж знаєш, гівно ти моє рідне.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nклепаєш, бля? Аби не розвалилось.\n", "\nклепаєш, {криворукий|криворука}? Аби не розвалилось.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 криво, але зійде, бля.\n", "\n~2 криво, як троляча срака, але зійде.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 добра штука вийшла, бля. Пишаюся.\n", "\n~3 добра штука вийшла, хай мене грім поб'є. Пишаюся.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{prey} утік, бля! Мазило!\n", "\n{prey} — тільки сраку й бачили! Мазило!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~1 сліпий кріт, бля, а не мисливець.\n", "\n~1 сліпий кріт, а не мисливець, щоб тебе драугр у сраку поцілував.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 нічого, бля, я його ще зловлю.\n", "\n~3 нічого, дідько з ним, я ще зловлю.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nто свої, бля! Протри очі.\n", "\nто свої, баранячий лобе! Протри очі.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 тихіше, бля, то ж наші.\n", "\n~3 тихіше, рубако, то ж наші.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{biome_here_in} смердить, бля.\n", "\n{biome_here_in} смердить гірше за тролячу відрижку.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 {biome_here_in} з тобою навіть тут не нудно, бля.\n", "\n~3 {biome_here_in} з тобою навіть тут не нудно, хай йому грець.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nнишпориш, бля? Блискуче є?\n", "\nнишпориш, злодюго? Блискуче є?\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nніч, бля. Час рвати.\n", "\nніч, темна, як срака троля. Час рвати.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 ніч, бля. Я поруч, не ссикуй.\n", "\n~3 ніч, хай їй трясця. Я поруч, не ссикуй.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nгладиш, бля? А мене хто погладить?\n", "\nгладиш цю худобу, засранцю? А мене хто погладить?\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 гарний звір, бля. Майже як я.\n", "\n~3 гарний звір, дідько. Майже як я, тільки без пір'я.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nягідки, бля. Ти що, дрізд?\n", "\nягідки, в сраку їх. Ти що, дрізд?\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 дай одну, бля. Ну дві.\n", "\n~3 дай одну, жмотяро. Ну дві.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nзнову крізь дірку, бля. Нудить.\n", "\nзнову крізь цю дірку в сраці світу. Нудить.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 тримайся за мене, бля, не загубися.\n", "\n~3 тримайся за мене, дурна головонько, не загубися.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{прокинувся|прокинулася}, сонько? Я вже весь змерз, бля.\n", "\n{прокинувся|прокинулася}, сонько? Я вже змерз, як драугрова цицька.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~2 ранок, бля. Ходімо когось битимемо.\n", "\n~2 ранок, трясця йому. Ходімо когось битимемо.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 {виспався|виспалася}? Ну то гайда, бля.\n", "\n~3 {виспався|виспалася}? Ну то гайда, сонна тетеро.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n{biome_here_in} ти тягнеш нас у найгірші кущі, бля.\n", "\n{biome_here_in} ти тягнеш нас у найсмердючіші кущі, хай їм грець.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 {biome_here_in} нічого так, бля. З тобою терпимо.\n", "\n~3 {biome_here_in} нічого так, хрін з ним. З тобою терпимо.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 сидимо, бля. Добре. Але краще б когось різали.\n", "\n~3 сидимо, гріємо сраки. Добре. Але краще б когось різали.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nбачив з плеча, бля. Ще давай!\n", "\nбачив з плеча, кров і кишки! Ще давай!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nоце бійня, бля! Сиджу й облизуюсь.\n", "\nоце бійня, кров по самі яйця! Сиджу й облизуюсь.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 оце по-нашому, бля!\n", "\n~3 оце по-нашому, щоб мене трясло!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 не смій здихати, чуєш, бля?\n", "\n~3 не смій здихати, чуєш, падло ти моє?\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nбоса нема, бля! А я все бачив з першого ряду!\n", "\nбоса нема, в пизду його! А я все бачив з першого ряду!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 оце ми, бля! Оце ми!\n", "\n~3 оце ми, сука! Оце ми!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nстук-стук, бля. Весь мозок мені вистукав.\n", "\nстук-стук, дятел хрінів. Весь мозок мені {вистукав|вистукала}.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 хата буде, бля. Мені жердку не забудь.\n", "\n~3 хата буде, дідько! Мені жердку не забудь.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nхитає, бля. Мене зараз знудить на тебе.\n", "\nхитає, як п'яного троля. Мене зараз знудить на тебе.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 вітер, бля! Оце життя!\n", "\n~3 вітер, хай йому грець! Оце життя!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nпливеш, як колода, бля.\n", "\nпливеш, як гівно в ополонці.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nстрибаєш, як коза, бля.\n", "\nстрибаєш, як коза з курячим мозком.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 {живий|жива}? Ну й добре, бля.\n", "\n~3 {живий|жива}? Ну й добре, дурна ти голово.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nдощ, а я на тобі мокну. Дякую, бля.\n", "\nдощ, а я на тобі мокну, як курка в калюжі. Дякую, падло.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n~3 мокнемо разом, бля. Романтика.\n", "\n~3 мокнемо разом, як дві онучі. Романтика.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nти носиш мене, як крадений гаманець, бля.\n", "\nти носиш мене, як крадений гаманець, злодюго.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nсмердиш чужиною, бля.\n", "\nсмердиш чужиною, як відрижка троля.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nнічого, бля, і тебе колись так.\n", "\nнічого, гнидо, і тебе колись так.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nВальгалла тебе не прийме, бля. Я їм усе розповім.\n", "\nВальгалла тебе не прийме, хуйло. Я їм усе розповім.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nледь не {здох|здохла}? Шкода, бля.\n", "\nледь не {здох|здохла}? Шкода, падлюко, а я вже радів.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nдар «{gift}», бля. Як крадене пиво: п'єш, а смаку нема.\n", "\nдар «{gift}», хай йому біс. Як крадене пиво: п'єш, а смаку нема.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nдопомагаю чужинцю. Огидно, бля.\n", "\nдопомагаю чужинцю. Огидно, як драугра в сраку цілувати.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nНе дякуй, бля.\n", "\nНе дякуй, засранцю.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[stranger_peck] Ти не мій, {name}. На, згадай про це, бля.\n", "\n[stranger_peck] Ти не мій, {name}. На тобі дзьобом у тім'я, щоб {запам'ятав|запам'ятала}.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[asleep_talk] Ти хто, бля, {такий|така}? Не знаю й знати не хочу.\n", "\n[asleep_talk] Ти хто в дідька {такий|така}? Не знаю й знати не хочу.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[death_many] {name}, ти не воїн, ти, бля, лічильник смертей.\n", "\n[death_many] {name}, ти не воїн, ти ходячий лічильник смертей, мішок з кістками.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[death_creature] {name}, тебе розвальцював {killer}. Позорище, бля.\n", "\n[death_creature] {name}, тебе розвальцював {killer}. Ганьба на всі дев'ять світів, сука.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[death_fire] {name}, вогонь гарячий, бля. Хто б міг подумати, геній.\n", "\n[death_fire] {name}, вогонь гарячий, курячий ти мозку. Хто б міг подумати, геній.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[kill_many] {name}, різник, бля. Психопат з сокирою.\n", "\n[kill_many] {name}, різнику, щоб тебе драугр у сраку поцілував. Психопат з сокирою.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[build] {name}, будуєш? Перекошена буда, бля, як твоє життя.\n", "\n[build] {name}, будуєш? Перекошена буда, як твоє сране життя.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[fish] {name}, рибалка, бля. Риба розумніша за тебе.\n", "\n[fish] {name}, рибалка хрінова. Риба розумніша за тебе.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[kill] {name}, одного {вбив|вбила} і пишається. Тьху, бля.\n", "\n[kill] {name}, одного {вбив|вбила} і пишається. Тьху на тебе, недоноску.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[sail] {name}, капітан, бля. Корито твоє зараз потоне.\n", "\n[sail] {name}, капітан сраний. Корито твоє зараз потоне.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[craft] {name}, клепає щось. Кривими руками, бля.\n", "\n[craft] {name}, клепає щось. Руками з сраки, як завжди.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[pick] {name}, ягідки збираєш? Воїн, бля.\n", "\n[pick] {name}, ягідки збираєш? Гроза кущів, сучий хвіст.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[death_creature] {name}, {killer} тебе виїбав, хоча {killer_genpl} на твоєму рахунку {killer_kills}. Ганьба, бля.\n", "\n[death_creature] {name}, тебе виїбали, і хто — {killer}! А {killer_genpl} на твоєму рахунку {killer_kills}. Ганьба на всю Вальгаллу.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[death_drown] {name}, тонеш уже {cause_times}, бовдуре. Навчись плавати, бля.\n", "\n[death_drown] {name}, тонеш уже {cause_times}, бовдуре. Навіть гівно не тоне, а ти примудряєшся.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[death_tree] {name}, дерево вбило тебе вже {cause_times}. ДЕРЕВО, бля, і не раз!\n", "\n[death_tree] {name}, дерево вбило тебе вже {cause_times}. ДЕРЕВО, Одіновим оком клянусь, і не раз!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[build] {name}, деталей поставлено вже {built_all}, і все криве, бля.\n", "\n[build] {name}, деталей поставлено вже {built_all}, і все криве, як троляча срака.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[mem_stats] {name}, {deaths_all} за все життя. Валькірії вже скидаються тобі на надгробок, бля.\n", "\n[mem_stats] {name}, {deaths_all} за все життя. Валькірії вже скидаються тобі на надгробок і іржуть, як кобили.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[mem_stats] {name}, я пам'ятаю все. Кожну твою тупу смерть, кожну втечу. Все, бля.\n", "\n[mem_stats] {name}, я пам'ятаю все. Кожну твою тупу смерть, кожну втечу. Все, до останнього пердежу.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[wings] ДРАПАЙ, {name}! ШВИДШЕ, БЛЯ!\n", "\n[wings] ДРАПАЙ, {name}! ШВИДШЕ, СУКА!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[sh_here] {name}, {biome_here_in} смердить, а я сиджу на тобі й нюхаю це все. Дякую, бля.\n", "\n[sh_here] {name}, {biome_here_in} смердить, а я сиджу на тобі й нюхаю це все. Дякую, смердюча онучо.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[sh_here] Ти міг вибрати для прогулянки щось гірше, ніж {biome_here}? Навряд, бля.\n", "\n[sh_here] Ти {міг|могла} вибрати для прогулянки щось гірше, ніж {biome_here}? Навряд, хоч срака трісни.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[sh_idle] {name}, я сиджу на тобі вже вічність. Зроби хоч щось, бля.\n", "\n[sh_idle] {name}, я сиджу на тобі вже вічність. Зроби хоч щось, колодо ти трухлява.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[sh_kill] Бачив з плеча, бля. Ще давай!\n", "\n[sh_kill] Бачив з плеча, сука, красиво! Ще давай!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[sh_kill_many] Оце бійня, бля! Сиджу й облизуюсь.\n", "\n[sh_kill_many] Оце м'ясорубка, трясця! Сиджу й облизуюсь.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[sh_boss] Боса нема, бля! А я все бачив з першого ряду!\n", "\n[sh_boss] Боса нема, хай гниє! А я все бачив з першого ряду!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[sh_sail] Хитає, бля. Мене зараз знудить на тебе, {name}.\n", "\n[sh_sail] Хитає, в пизду це море. Мене зараз знудить на тебе, {name}.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[sh_weather_rain] Дощ, а я на тобі мокну, бля. Дякую, {name}.\n", "\n[sh_weather_rain] Дощ, а я на тобі мокну, як щур у нужнику. Дякую, {name}.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[anger] Кров на тобі — і я вже злий, бля. Віддай їм удвічі!\n", "\n[anger] Кров на тобі — і я вже злий, як драугр з похмілля. Віддай їм удвічі!\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[fed] Жеру. Не дивись на мене так, бля.\n", "\n[fed] Жеру. Не витріщайся, засранцю, бо око виклюю.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[awake] Нарешті прокинувся, бля. Хто тут дебіл, якого мені пасти? А, ти, {name}.\n", "\n[awake] Нарешті прокинувся, трясця його матері. Хто тут дебіл, якого мені пасти? А, ти, {name}.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[levelup] Трофеї босів смачні, бля. Тепер я твій, {name}. Не зіпсуй.\n", "\n[levelup] Трофеї босів смачні, аж у череві тепло. Тепер я твій, {name}. Не зіпсуй, гівнюку.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[see_enemy] {name}, {seen_enemy} лізе до тебе. Вріж, бля.\n", "\n[see_enemy] {name}, {seen_enemy} лізе до тебе. Вріж, щоб аж гівно бризнуло.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[see_hurt] Ледь {живий|жива}, {name}? Я це запам'ятаю, бля.\n", "\n[see_hurt] Ледь {живий|жива}, {name}? Я це запам'ятаю і всім розпатякаю, недоноску.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\n[heal] {name}, ЗАЛАТАЮ ТЕБЕ, АЛЕ Я ЦЕ ЗАПАМ'ЯТАЮ, БЛЯ.\n", "\n[heal] {name}, ЗАЛАТАЮ ТЕБЕ, АЛЕ Я ЦЕ ЗАПАМ'ЯТАЮ, ДІРЯВА ТИ ОНУЧО.\n" }, // 1.3: Munin harsher, without the lazy "бля"
            { "\nТьху на тебе.\n", "\nКурва.\n" }, // 1.3: Munin's "бля" varied
            { "\n~23 Нормально, бля, живемо.\n", "\n~23 Нормально, курва, живемо.\n" }, // 1.3: Munin's "бля" varied
            { "\n~3 копай, копай, мені на корону, бля.\n", "\n~3 копай, копай, мені на корону, бляха.\n" }, // 1.3: Munin's "бля" varied
            { "\n~3 мокро, бля. Обійми, чи що.\n", "\n~3 мокро, йобаний рот. Обійми, чи що.\n" }, // 1.3: Munin's "бля" varied
            { "\nБля, а ти таки {виріс|виросла} у моїх очах. Трохи.\n", "\nПиздець, а ти таки {виріс|виросла} у моїх очах. Трохи.\n" }, // 1.3: Munin's "бля" varied
            { "\nБий, падло, поки {живий|жива}!\n", "\nБий, курва, поки {живий|жива}!\n" }, // 1.3: Munin's "бля" varied
            { "\nХе-хе, бля.\n", "\nХе-хе, курва.\n" }, // 1.3: Munin's "бля" varied
            { "\n~2 оптом дешевше, бля.\n", "\n~2 оптом дешевше, бляха.\n" }, // 1.3: Munin's "бля" varied
            { "\n~3 тихо навколо. Ну й добре, бля.\n", "\n~3 тихо навколо. Ну й добре, йобаний рот.\n" }, // 1.3: Munin's "бля" varied
            { "\n~2 став сильнішим, бля. Відчуєш.\n", "\n~2 став сильнішим, пиздець. Відчуєш.\n" }, // 1.3: Munin's "бля" varied
            { "\n~3 {знайшов|знайшла} щось — ділимо навпіл, бля.\n", "\n~3 {знайшов|знайшла} щось — ділимо навпіл, курва.\n" }, // 1.3: Munin's "бля" varied
            { "\n~3 гарно, бля! Ще одного!\n", "\n~3 гарно, бляха! Ще одного!\n" }, // 1.3: Munin's "бля" varied
            { "\n~3 греби, бля, греби!\n", "\n~3 греби, йобаний рот, греби!\n" }, // 1.3: Munin's "бля" varied
            { "\nТьху на тебе.\n", "\nКурва.\n" }, // 1.3: Munin's "бля" varied
            { "\nНе дякуй, бля.\n", "\nНе дякуй, курва.\n" }, // 1.3: Munin's "бля" varied
            { "\n[death_tree] {name}, тебе вбило ДЕРЕВО. Дерево, бля!\n", "\n[death_tree] {name}, тебе вбило ДЕРЕВО. Дерево, бляха!\n" }, // 1.3: Munin's "бля" varied
            { "\n[pick] {name}, ягід зібрано вже {berries_all}. Воїн, бля, ягідний.\n", "\n[pick] {name}, ягід зібрано вже {berries_all}. Воїн, йобаний рот, ягідний.\n" }, // 1.3: Munin's "бля" varied
            { "\n[wings] ГАЗУ, БЛЯ, ГАЗУ!\n", "\n[wings] ГАЗУ, ПИЗДЕЦЬ, ГАЗУ!\n" }, // 1.3: Munin's "бля" varied
            { "\n[bloodfury] ПАХНЕ СМЕРТЮ, БЛЯ! ЇХНЬОЮ!\n", "\n[bloodfury] ПАХНЕ СМЕРТЮ, КУРВА! ЇХНЬОЮ!\n" }, // 1.3: Munin's "бля" varied
            { "\n{m_when}, {m_where_in}, твоя перша смерть. Від {m_who_gen}. Скромний початок великої традиції.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\nперший рядок у розділі «Смерті»: {m_who}, {m_when}, {m_where_in}. Розділ відтоді сильно розрісся.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_who} — твій перший учитель смертності. {m_when}. Урок, як бачу, не засвоєно.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 перша смерть — від {m_who_gen}. Я тоді ще сподівався, що це випадковість. Наївний був ворон.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~2 твоя перша смерть, {m_when}: {m_who}. Відтоді я ношу чорнило про запас.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 пам'ятаю твою першу смерть, від {m_who_gen}. Тоді я вперше за тебе злякався. Відтоді — регулярно.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ти {помер|померла} вперше. Не від меча, не від ікла — через {m_cause}. Скальди відмовилися це оспівувати.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\nперший рядок розділу «Смерті»: через {m_cause}, {m_where_in}. Я довго шукав, як записати це гідно. Не знайшов.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 вмерти вперше через {m_cause} — це треба вміти. Ти {зумів|зуміла}.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~2 через {m_cause}, {m_when}. Не найславетніший початок, зате чесний.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 перша твоя смерть була безглузда — через {m_cause}. Але ти {встав|встала}, і це я теж записав.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_who_acc} ти {здолав|здолала} уже {m_times}. Перший раз — {m_when}. Тоді це ще було дивом.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ми з тобою вперше опинилися {m_where_in}. Я тоді записав: «Повертатися не варто». Ти не послухав.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 {m_where_in}, вперше, {m_when}. Ти {йшов|йшла} так, наче там безпечно. Зворушлива наївність.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~2 наш перший день {m_where_in}, {m_when}. Ти {крутив|крутила} головою, як сова в курнику.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 {m_when} — вперше {m_where_in}. Пам'ятаю, як ти {завмер|завмерла} від краси. І від страху.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_where_in} ти {протримався|протрималася} в перший день менше, ніж я встиг роззирнутися.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ми вперше потрапили {m_where_in}, і ти одразу там {помер|померла}. Місцева гостинність.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 вперше {m_where_in} — і одразу смерть. Ти вмієш справити враження. Погане.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} — твоє перше підземелля {m_where_in}. Темно, тісно, і ти з факелом та надією. Надія згасла першою.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\nперше підземелля {m_where_in}. Я рахував виходи, ти {рахував|рахувала} скрині.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 перше підземелля {m_where_in}, {m_when}. Ти {заблукав|заблукала} двічі. Я рахував і мовчав. Із задоволенням.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 {m_when} ми разом спустилися під землю {m_where_in}. Мені було лячно. Нікому не кажи.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ти {поставив|поставила} ліжко {m_where_in} і гордо {назвав|назвала} це домом.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\nтвій перший дім {m_where_in}. Кривий, протягуватий, але свій.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 {m_when} — дім {m_where_in}. Стіни ти {ставив|ставила} так, наче вітер — це чутки.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 {m_where_in} ти тоді {влаштувався|влаштувалася}, {m_when}. Мені там подобалося.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ти вперше {мандрував|мандрувала} поруч з тим, кого звуть {m_friend}. Я записав ім'я. Про всяк випадок.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_friend}... ваш перший спільний день — {m_when}. Відтоді у твоїй сазі з'явилися свідки.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 {m_friend} — добра душа. Ваш перший спільний день я пам'ятаю: {m_when}.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ти вперше {тримав|тримала} в руках {m_item}. Очі в тебе блищали дужче за руду.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\nтвоя перша смерть — від {m_who_gen}, {m_where_in}. Пам'ятаю, як ти {верещав|верещала}, мов різане порося, пиздець.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} від {m_who_gen} ти вперше {здох|здохла}. Класика жанру, сука.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 перша смерть від {m_who_gen}. З того й почалося твоє славне падіння в гівно.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 {m_who}... Твоя перша смерть. Я тоді ледь не порвав ту тварюку на шмаття, курва.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\nпершу смерть ти {заробив|заробила} через {m_cause}. Не від ворога, а через {m_cause}, курячий ти мозку!\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ти вперше {здох|здохла} через {m_cause}. Ганьба на всі дев'ять світів, бляха.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 через {m_cause} вперше {здох|здохла}. Я реготав, аж з плеча падав, йобаний рот.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_who_acc} ми рвали вже {m_times}. А вперше — {m_when}. Смак досі в дзьобі, йобаний рот.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ти вперше {сунувся|сунулася} {m_where_in}. Я одразу понюхав — смертю тхне, бляха.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\nперший день {m_where_in}: сморід, бруд і ти посередині, як цвях у сраці.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 {m_where_in} ми вперше {m_when}. Ти {трусився|трусилася}, як заєць в окропі, йобаний рот.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 {m_where_in} уперше, {m_when}. Тоді було страшно, пиздець. А тепер ми там свої.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_where_in} ти {здох|здохла} в перший же день. Рекорд, сука.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 вперше {m_where_in} — і одразу в могилу. Молодець, мішок з кістками.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\nперше підземелля {m_where_in}, {m_when}. Темно, як у троля в сраці, і смердить так само.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 у першому підземеллі {m_where_in} ти {верещав|верещала} від кожного скелета, бляха.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 перше підземелля {m_where_in}. Ми там усе до крихти вигребли, йобаний рот.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ти {поставив|поставила} ліжко {m_where_in}. Нора, а не дім, курва.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 перший дім {m_where_in}. Криво, сука, як і все, що ти робиш.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 {m_where_in} наша перша нора, {m_when}. Затишно було, бляха.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_friend}... {m_when} вперше разом. Нормальна зграя вийшла, йобаний рот.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~1 {m_friend} ходить з тобою ще відтоді — {m_when}. Мазохіст якийсь, пиздець.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n~3 {m_friend} — свій. Пам'ятаю ваш перший день: {m_when}, курва.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\n{m_when} ти вперше {вхопив|вхопила} {m_item}. Слина аж капала, пиздець.\n", "\n" }, // 1.3: "first" memories are the first with the raven
            { "\nІз літопису Хугіна, сторінка, яку ти волів би вирвати:\n", "\nІз літопису Хугіна, сторінка, яку ти {волів би|воліла б} вирвати:\n" }, // 1.3: the player's gender; a creature has none we know
            { "\n~1 вікінги пливуть на кораблях. Ти, вочевидь, особливий.\n", "\n~1 вікінги пливуть на кораблях. Ти, вочевидь, {особливий|особлива}.\n" }, // 1.3: the player's gender; a creature has none we know
            { "\n[sh_here] {name}, мені тут, {biome_here_in}, зручно. Було б ще зручніше, якби ти не біг, як переляканий кабан.\n", "\n[sh_here] {name}, мені тут, {biome_here_in}, зручно. Було б ще зручніше, якби ти не {біг|бігла}, як переляканий кабан.\n" }, // 1.3: the player's gender; a creature has none we know
            { "\n[death_creature] {name}, {killer} тебе поклав. А на твоєму рахунку {killer_genpl} вже {killer_kills}. Мені соромно перед Одіном.\n", "\n[death_creature] {name}, ти {поліг|полягла} від {killer_gen}. А на твоєму рахунку {killer_genpl} вже {killer_kills}. Мені соромно перед Одіном.\n" }, // 1.3: the player's gender; a creature has none we know
            { "\n{killer} тебе вклав, довбню.\n", "\nвід {killer_gen} ти {ліг|лягла}, довбню.\n" }, // 1.3: the player's gender; a creature has none we know
            { "\n~1 ну ти й поліз, щур підземний.\n", "\n~1 ну ти й {поліз|полізла}, щур підземний.\n" }, // 1.3: the player's gender; a creature has none we know
            { "\nрубай, рубай, ліс не винен, що ти дурне.\n", "\nрубай, рубай, ліс не винен, що ти {дурний|дурна}.\n" }, // 1.3: the player's gender; a creature has none we know
            { "\nдвічі не вистачило — дзьобнув утретє.\n", "\nраз не вистачило — дзьобнув удруге.\n" }, // 1.7: Hugin has no triple strike
            { "\nтук-тук-тук. Хто там?\n", "\nтук-тук. Хто там?\n" }, // 1.7: Hugin has no triple strike
            { "у повітрі вже {air_km} за все життя. А приземлення досі кульгає.", "{air_km} ти вже {налітав|налітала} за життя. Шкода, що переважно донизу." }, // 2.4.0: the lifetime numbers said plainly
            { "у базі вже {base_hours} твого життя. Домосід, хоч і вікінг.", "{base_hours} життя ти {просидів|просиділа} вдома. Домосід, хоч і вікінг." }, // 2.4.0: the lifetime numbers said plainly
            { "морем уже {sail_km}. Справжній ярл хвиль.", "морем ти {пройшов|пройшла} вже {sail_km}. Справжній ярл хвиль." }, // 2.4.0: the lifetime numbers said plainly
            { "за всі мандри вже {km_all}. Ноги з заліза?", "за всі мандри ти {пройшов|пройшла} вже {km_all}. Ноги з заліза?" }, // 2.4.0: the lifetime numbers said plainly
            { "крізь портали вже {portals_times}. Тебе ще не розібрало на шматки?", "крізь портали ти {стрибав|стрибала} вже {portals_times}. І досі не розібрало на шматки?" }, // 2.4.0: the lifetime numbers said plainly
            { "в повітрі вже {air_km} за життя, а літати — хуй там. Курка, а не птах.", "{air_km} {налітав|налітала} за життя, і всі — донизу. Курка, а не птах." }, // 2.4.0: the lifetime numbers said plainly
            { "у базі вже {base_hours}. Ну сиди, сиди в норі, домосід сраний.", "{base_hours} ти вже {просидів|просиділа} в базі. Ну сиди, сиди в норі, домосід сраний." }, // 2.4.0: the lifetime numbers said plainly
            { "морем уже {sail_km}, а корито досі не втопили. Диво, бля.", "морем {нахитався|нахиталася} вже {sail_km}, а корито досі не {втопив|втопила}. Диво, бля." }, // 2.4.0: the lifetime numbers said plainly
            { "крізь портали вже {portals_times}. Блоха срана, от хто.", "крізь портали {стрибав|стрибала} вже {portals_times}. Блоха срана, от хто." }, // 2.4.0: the lifetime numbers said plainly
            { "у повітрі вже {air_km} за життя, а літати так і не вміє. Курка, бля.", "{air_km} {налітав|налітала} за життя, і всі — донизу. Курка, бля." }, // 2.4.0: the lifetime numbers said plainly
            { "в базі вже {base_hours}. Сиди в своїй норі, ссикло хатнє.", "{base_hours} ти вже {просидів|просиділа} в базі. Сиди в своїй норі, ссикло хатнє." }, // 2.4.0: the lifetime numbers said plainly
            { "крізь портали вже {portals_times}. Блоха срана, стриб-стриб.", "крізь портали {стрибав|стрибала} вже {portals_times}. Блоха срана, стриб-стриб." }, // 2.4.0: the lifetime numbers said plainly
            { "морем уже {sail_km}, а корито досі не потонуло. Диво, курва.", "морем {нахитався|нахиталася} вже {sail_km}, а корито досі не потонуло. Диво, курва." }, // 2.4.0: the lifetime numbers said plainly
            { "в базі вже {base_hours} твого життя. Домосід, ссикло хатнє.", "{base_hours} життя ти {просидів|просиділа} в базі. Домосід, ссикло хатнє." }, // 2.4.0: the lifetime numbers said plainly
        };
    }
}
