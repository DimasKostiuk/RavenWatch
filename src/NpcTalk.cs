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
    // («talk: elder»; none: a settler «common», a person written by hand has no window and only greets, NpcDef.TalkId).
    // «{greet}» is said once a talk: coming back to its place it is a short word (talk_again…, NpcPhrases).
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
    //   > Я вже вбив оленя, ось ріг. => horn_have once ?boss eikthyr ?item HardAntler
    //   >| Що за ріг на кресленні? => horn once   «>|»: a way of the answer above it — shown only while no answer of its row above holds its
    //                                     conditions (once read or not: read «I have the horn», you are not asked «what horn?» after). A fork
    //                                     of one question by what you have done (the user 2026-11-07); conditions alone are «and» only
    //   > Звідки ключ? => key once        once: read, it is not offered again (each character's own, kept with its story; the place it
    //                                     leads to is what counts as read). A place the story asks about (the condition «asked <talk> <place>»:
    //                                     the book's «Сага» writes what you learnt there — Note) is kept the same way whichever answer led to
    //                                     it, so a «once» answer to it is not offered after it was heard by another one
    //   > Розкажи щось. => tale_a|tale_b|made*3   one of these places at random (Draw), «*3» three chances; a place kept heard (a «once» one's,
    //                                     one the story asks about: «asked») is drawn only when all are heard. No «once» on it: what is heard is not drawn
    //   ?? npcat hall                     the context: all answers below it in this place hold this too, till the next «??» («??» alone:
    //                                     none). A talk's topics by where it is and what the story is at; asked each time it is shown,
    //                                     so when it walks off they are gone and others come.
    //   @c_low_tired = c_tired            a place that is another place of the same talk (the same answers, written once)
    //   [talk common +]                   more for a talk already there (a story's file for the built-in common): its places «@x +»
    //                                     add their answers first, new places are added
    //   {word} · !deed                    a module's own words and deeds (Words, Deeds: StoryTales «{tale}», «!taletold»)
    //   {~nod}                            its gesture as it says this (NpcBody.Spoken: nod, laugh, proud, shy, shrug, point)
    //   [talk torvald : common]           built on another talk: all its places, and these over them —
    //   @start +                            «+»: added to that place (its words, if given, instead; its answers first,
    //                                       then the other's); without «+»: this place instead of the other's
    //
    //   > {@say_ok} => start              your words made, not written: a theme say_* (NpcMoreSayPatterns: say_ok, say_got, say_told, say_trusted, say_care,
    //                                     say_welcome, say_shrug, say_more, say_bye) — by its ways, its state and how it is with you. A plain «Добре.»,
    //                                     «Зрозумів.», «Бувай.» is never written in a talk: the made one is
    //   > Тримайся. => c_calm !tone warm soft     a kind answer's air — soft, blunt, joke: how it lands is by the person (NpcTalkTone; such rows change places: Mixed)
    //   > Давно кульгаєш? => f_limp ?fact кульгає   a question only to one of whom its story says so; «?hasjob» one with work
    //   > Попроси… => ask dim ?patience >= 75     dim: its conditions failing, it is shown grey with why (a tip), not hidden
    //   ~hugin aside: Занадто легко вона про мерців.   your raven's word here, once for each character (NpcTalkRaven: aside, aloud)
    //
    // A story person's talk is a file of assets/story/talk (StoryFiles; config/RavenWatch/talk only with no source, as before).
    // It flows like a talk (Flows): a reply whose only answers are a made «{@say_…} => start» shows the first screen's questions under its words
    // at once — you ask the next thing, not «Тепер ясно» and back. A reply meant to close or open a line says so: an answer of its own, a deed, a condition.
    // Its questions live in the story's time (the user 2026-11-15: a question out of its time, or one not asked in it, is gone for good). Each one of
    // the first screen («@start») lives only in its file's chapter, not before it and not after it: Parse puts «chnow <its block>» on it (StoryFiles.Block,
    // StoryQuests.ChapterNow) — but a closing «{@say_…} => end» and one with «always». Its end inside the chapter is the data's own, and storycheck stops one with none: «?step …»,
    // «?!reached <quest> <stage>», «?!world …» — or «wholechapter» on purpose (docs/jobs/words-people.md «Скільки живе питання»).
    // A question that by its meaning lives past its chapter says «through» (the user 2026-11-16: a question goes only when it no longer belongs, some run
    // through several chapters): from its file's chapter on with no chapter's end («chfrom <its block>»), till the story ends it — a lasting fact in its own line
    // («?!world …»), or a later chapter's file saying so of the place it leads to, a line of that chapter's «[talk heid +]»:
    //   ?- watch_what watch_me : reached ch1 rite_oath     the «through» questions of the first screen that lead to these places end once this holds
    //                                                       (one condition, not turned round; several lines: any of them ends it)
    // One with no end yet says how far it was judged, «through 1» (open still at chapter 1): storycheck stops the build at the next chapter till each is ended or judged anew.
    // Whether a place can still be asked — now, later, never again — is NpcTalkTime.cs (the book's «може розповісти більше», «вже нікого спитати»).
    // It runs on the game of the one who talks (its window, your items and story); what changes the person (hired, its
    // orders) is sent to the game that runs it.
    //
    // «На думці» (docs/plans/talk-plan.md, the user 2026-11-05: it is a living person, not a menu — you are not the centre of it). A talk
    // with a place «c_any» (the settlers' common and those built on it) opens with what weighs on it most now (NpcVoice.Concerns: the
    // same ranking as its unasked words, leaned by its ways, its own only to one it trusts) — its first words, not a choice — and the
    // window has three parts: its worry's answers (the place «c_<theme>», else «c_<group>», else «c_any»), «ще на думці» (a few lesser
    // ones, by its chattiness; none below NpcPatience.Dry; picked: the place «c:<theme>», words {concern}), then yours (the start's own).
    // A pressing worry (BrushAt) may come first when you ask your own (a brush-off: its words, then the answer — never instead of it).
    // Its patience (NpcPatience) shows as a bar with its reasons. Asked the same again (Repeat: a topic, by what it is now — Sig), it says
    // «я ж казав» and its last words, then made lines (repeat_1…3, by its ways and how it feels to you), then that topic goes grey a while;
    // what you do (hire, let go, its request, its rescue, a deed, the story and the way to it: LeadsToStory) is never counted nor shut. Kept in your character (NpcTalkMemo).
    // A kind answer to a worry warms it once a game day a worry (WarmedToday: said again, it costs what a repeat costs and gives nothing back).
    // What an answer needs is asked again when it is picked (Pick: the window may have stood open a while). The window closes for a scene (Tick; none opens during one). You stand there saying nothing: NpcTalkIdle.
    // More of it, each in its own file (the user 2026-11-07): what it sees of you — its worries you_* (NpcVoice), one of its people near butts in (NpcTalkThird),
    // «Що скажеш про когось із наших?» and carrying it to that one (NpcTalkAbout), «Пам'ятаєш, як ми…?» (NpcTalkRecall), a gift (NpcTalkGift), talking it round after
    // its no (NpcTalkSway). A place made by code (the homes, your clothes, its neighbours, your gifts): Places.
    internal static partial class NpcTalk
    {
        internal sealed class Answer { public string Text = "", To = "end"; public string[] Pool; public bool Once, Dim, Through; public Answer Else; public List<Cond> When = new List<Cond>(); public List<string> Acts = new List<string>(); } // (Else: the answer a «>|» is a way of; Pool: «=> a|b*2|c», the places it draws from — To is then the whole «a|b*2|c», Draw)
        internal sealed class Node { public string Id = "", Says = ""; public bool Adds; public readonly List<Answer> Answers = new List<Answer>(); public readonly List<RavenLine> Ravens = new List<RavenLine>(); }
        internal sealed class Talk { public string Id = "", Base = ""; public readonly Dictionary<string, Node> Nodes = new Dictionary<string, Node>(); public readonly Dictionary<string, bool> Leads = new Dictionary<string, bool>(); public readonly Dictionary<string, string> Alias = new Dictionary<string, string>(); public readonly List<KeyValuePair<string[], Cond>> Ends = new List<KeyValuePair<string[], Cond>>(); } // (Ends: «?- <places> : <condition>» — the places, the condition turned round)

        // a module's own words in a talk's text and its own deeds after «!» (the core names no module)
        internal static readonly Dictionary<string, Func<NpcActor, Player, string>> Words = new Dictionary<string, Func<NpcActor, Player, string>>();
        internal static readonly Dictionary<string, Action<NpcActor, Player, string[]>> Deeds = new Dictionary<string, Action<NpcActor, Player, string[]>>();

        internal static readonly int KHired = "RavenWatch_npcHired".GetStableHashCode(); // long: who hired it (a player id; 0 nobody)

        // ---------------------------------------------------------------- hired, let go (acts on the game that runs it: NpcModules)
        internal static void Hire(NpcActor a, string[] w)
        {
            long id;
            if (w.Length < 2 || a.Zdo == null || !long.TryParse(w[1], out id) || !NpcActor.SentBy(id)) return;
            if (NpcLevers.IsProtected(a)) { Npcs.Log(a.Id + ": the story holds it — not hired"); return; }
            long had = a.Zdo.GetLong(KHired, 0L);
            if (had != 0L && had != id) { Npcs.Log(a.Id + ": hired already by " + had + " — not by " + id); return; } // (two windows open at once: the first word stands)
            a.Zdo.Set(KHired, id);
            NpcMemory.Remember(a, "hired", id, ""); // (who took it in)
            Npcs.Log(a.Id + ": hired by " + w[1]);
        }

        // «walkwith <player> <s>» where it is run: it walks with you a while (a talk's «Ходімо, покажу…»), a whim with its term — not an order kept till you let it go
        internal static float WalkWithFor = 180f;
        internal static void WalkWith(NpcActor a, string[] w)
        {
            float s;
            if (a.Mind == null || w.Length < 3 || !float.TryParse(w[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s)) return;
            GameObject go = NpcKit.Find(NpcKit.Parse(w[1]));
            Player p = go != null ? go.GetComponent<Player>() : null;
            if (p == null) return;
            if (!a.Mind.WalkWith(p, Mathf.Clamp(s, 10f, 600f), "walkwith")) Npcs.Log(a.Id + ": would walk with " + p.GetPlayerName() + ", but something it must do comes first");
        }

        internal static void Unhire(NpcActor a)
        {
            if (a.Zdo == null) return;
            if (NpcLevers.IsProtected(a)) { Npcs.Log(a.Id + ": the story holds it — not sent off"); return; }
            if (a.Gear != null) a.Gear.DropAll(); // (its gear at its feet)
            a.Zdo.Set(KHired, 0L);
            NpcLocker.Set(a, NpcRequests.KRequest, "");
            NpcLocker.Set(a, NpcGoals.KGoal, ""); // (its wish and your words were of its life here: gone with it, nobody to blame)
            NpcLocker.Set(a, NpcPromises.KPromise, "");
            NpcLocker.Set(a, NpcTasks.KTask, "");
            NpcMemory.TellHome(a, "neighbour_left", 0L, a.Key); // (those who stay remember it — before it leaves its heart)
            foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart))) if (o != a && NpcAttitude.OfPerson(o, a) >= NpcAttitude.WarmAt) NpcFeel.Tell(o, NpcFeel.Grief, 600f); // (one who liked it: sad a while)
            NpcResident.Unsettle(a);
            NpcLocker.Put(a.Zdo, NpcResident.KMovedOut, Vector3.zero); // (sent off, not moved out: no heart of yours to stay by)
            if (a.Mind != null) { a.Mind.ForgetWay(); a.Mind.Do("free"); } // (no way kept to take up again)
            Npcs.Log(a.Id + ": let go");
        }

        // ---------------------------------------------------------------- the talks
        internal static readonly List<string> More = new List<string>(); // a module's own places in the built-in talks («[talk common +]»), read right after Builtin (NpcPromises, NpcArcs)
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
            "> Що тут сталося, поки мене не було? => away ?awaynews ?!rescue\n" +
            // (the first screen, the user 2026-11-12: it stood at 11–14 rows. «Хто ти?» here only to one not yours yet; «Як справи?», «Хто ти?» to one of yours and
            // «Ти вільний…» are under «Поговорімо.» — the one row of yours that is never shut by asking again. The door still offers «Ти вільний…»: NpcTalkDoor)
            "> Хто ти? => who ?!hired\n" +
            "> Живи на моїй базі й працюй зі мною. => hire ?!hired ?!npcflag inneed\n" +
            "> Тобі щось потрібно? => need ?hasneed\n" +
            "> Ось те, що ти просив|просила. => thanks ?hasrequest !giverequest\n" +
            "> Ось те, про що ти мріяв|мріяла. => goaldone ?goalitem !goalgive\n" +
            "> Знаєш, що про тебе каже {carried}? => passon ?carried !passon\n" + // (what one of its home said of it: NpcTalkAbout)
            "> Маю дещо для тебе. => gift ?resident ?giftable\n" + // (a gift, once a game day: NpcTalkGift)
            "> Поговорімо. => talk ?hired\n" +
            "> Іди своєю дорогою, у мене нема для тебе місця. => end ?npcflag rescued ?!hired !goaway\n" +
            "> Попросити про щось… => ask dim ?resident ?mayask ?!angry\n" + // (from NpcPatience.AskFrom: sooner with the thread «рука»)
            "> {@say_bye} => end\n" +
            "@who\n{@who_intro} {story_short}\n> Розкажи більше. => more dim ?hasstory ?patience >= 45 ?!angry\n> {@say_got} => start\n" +
            "@more\n{story_more}\n> {@say_trusted} => start\n" +
            "@hire\n{~nod} {@hire_yes}\n" +
            "> Ходімо зі мною. => end !hire !follow\n> Іди сам|сама до оселі. => homes\n> Іншим разом. => end\n" +
            "@how\n{doing}\n> Тобі щось потрібно? => need ?hasneed\n> Ти поранений|поранена. Ось, випий меду. => c_dosed ?hurt ?hasmead !healmead !tone warm 1.5\n> Іди відпочинь, я прикрию. || Перепочинь трохи. => c_rest ?tired ?working !letrest !tone warm\n> А як робота? || Робота йде? || Як тобі твоя робота? => job ?hasjob\n> Поговорімо. => talk\n> {@say_ok} => start\n" +
            "@job\n{jobwords}\n> {@say_more} => talk\n> {@say_ok} => start\n" + // (its own work's words: work_<job>)
            // small talk with one of yours: its words made each time (NpcPhrases: talk_*, about_*)
            "@talk\n{@talk_invite}\n> Як справи? => how\n> Як тобі тут? => home ?resident\n> На що витрачаєш платню? || Куди монети діваєш? => coins ?hasjob\n> Хто ще в оселі живе? => folk ?resident\n> Що скажеш про когось із наших? => folkabout ?resident ?hasfolk\n> Розкажи щось. => tale\n" +
            "> Давно кульгаєш? || Що в тебе з ногою? => f_limp ?fact кульгає\n> Чого тобі не спиться ночами? || Що ти бачиш у тих зорях? => f_stars ?fact зорі\n" +
            "> Чому ти не миєшся біля берега? || Ти обходиш воду. Чому? => f_water ?fact боїться_води\n> Кажуть, ти вмієш розповідати. || Звідки в тебе стільки історій? => f_teller ?fact оповідач\n" +
            "> Пам'ятаєш, як ми…? => recall ?shared !recall\n" +
            "> Що думаєш про воронів? => ravens\n> Про що мрієш? => goal ?hasgoal\n> Чого б тобі хотілося? => wish ?!hasgoal\n> Що думаєш про мене? => me\n> Хто ти? => who\n> Ти вільний|вільна, іди своєю дорогою. => fire ?mine ?!npcflag protected\n> Годі балачок. => start\n" +
            "@f_limp\n{@fact_limp}\n> {@say_trusted} => talk\n> {@say_ok} => start\n@f_stars\n{@fact_stars}\n> {@say_trusted} => talk\n> {@say_ok} => start\n" +
            "@f_water\n{@fact_water}\n> {@say_trusted} => talk\n> {@say_ok} => start\n@f_teller\n{~laugh} {@fact_teller}\n> {@say_told} => talk\n> {@say_ok} => start\n" +
            "@home\n{@about_home}\n> {@say_more} => talk\n> {@say_ok} => start\n" +
            "@coins\n{@about_coins}\n> {@say_more} => talk\n> {@say_got} => start\n" + // (where its pay goes in its life: NpcMoreCoinPatterns)
            "@folk\n{@about_folk}\n> {@say_more} => talk\n> {@say_ok} => start\n" +
            "@tale\n{@tale}\n> Ще щось розкажи. => tale\n> Поговорімо про інше. => talk\n> {@say_told} => start\n" +
            "@ravens\n{@about_ravens}\n> {@say_more} => talk\n> {@say_ok} => start\n" +
            "@wish\n{~shrug} {@about_wish}\n> Подумаю, що можна зробити. => talk\n> {@say_ok} => start\n" +
            "@me\n{@you_remember} {@about_you}\n> {@say_more} => talk\n> {@say_ok} => start\n" +
            "@need\n{request}\n> Принесу. => end !promise request\n> Подумаю. => start\n" +
            "@goal\n{goal}\n> Я подумаю, як допомогти. => talk !promise goal !goalshow\n> {@say_ok} => start\n" +
            "@goaldone\n{~laugh} {goal_thanks}\n> {@say_welcome} => end\n" +
            "@thanks\n{~nod} {reward}\n> {@say_welcome} => end\n" +
            "@recall\n{recall}\n> Таке не забувається. || Добре було. || Еге ж, було діло. => talk\n> Годі спогадів. => start\n" + // (NpcTalkRecall)
            "@passon\n{passon}\n> {@say_ok} => start\n> {@say_bye} => end\n" + // (NpcTalkAbout)
            "@wait\n{@rescue_wait}\n> Тримайся! || Я швидко. || Уже біжу. => end\n" +
            "@saved\n{thanks}\n> Живи на моїй базі й працюй зі мною. => hire ?!hired\n> Хто ти? => who\n> Іди своєю дорогою, у мене нема для тебе місця. => end !goaway\n> {@say_bye} => end\n" +
            "@guard\n{@rescue_guard}\n> Чекай тут, я швидко. => end !rescuewait\n> Сховайся — я покажу, де. => end !rescuehide\n> Ходімо зі мною, я тебе охоронятиму. => end !rescuefollow\n" +
            "@what\n{plea_more}\n> {@say_got} => start\n" +
            "@fire\n{@fire_ask}\n> Так. => end !unhire\n> Ні, лишайся. => start\n" +
            // what is on its mind (NpcVoice.Concerns): the answers to its worry — its theme's own place first (c_hurt: it worries for you), then its group's, c_any last.
            // An answer «a || b || c»: one of its words picked each showing (AddRow), so your words are not the same every time
            "@c_any\n{concern}\n> Еге ж. || Отож. || Твоя правда. => c_chat !tone warm\n> Угу. || Ага. => start\n> Нащо ти мені це кажеш? || І що з того? || Цікаво. Але ні. => c_rude !tone rude\n" +
            "@c_news\n{concern}\n> Та ну? || Серйозно? || От це новина. => c_news_told !tone warm\n> Знаю, чу{в|ла} уже. || Та я ж там бу{в|ла}. => c_news_known\n> А що ще сталося, поки мене не було? => away ?awaynews\n> Мені якось байдуже. || А мені що з того? => c_rude !tone rude\n" +
            "@c_place\n{concern}\n> Твоя правда. Нічого, обживемось. || Є таке. Та ми це виправимо. => c_chat !tone warm soft\n> Отож. Такий край. || Який є. Іншого нам не дали. => c_chat !tone warm blunt\n> Ага. => start\n> Не подобається — шукай інше місце. || Годі нити, скрізь однаково. => c_rude !tone rude\n" +
            "@c_weather\n{concern}\n> Нічого, перебудемо. || Посидимо під дахом, минеться. || Колись та й розвидниться. => c_chat !tone warm soft\n> Хтось там нагорі на нас сердитий. || Погода як мій ворон: робить, що хоче. => c_chat !tone warm joke\n> Ну, дощ. А що поробиш. => c_chat ?weather rain\n> Ну, сніг. А що поробиш. => c_chat ?weather snow\n> Ну, буря. А що поробиш. => c_chat ?weather storm\n> Ну, туман. А що поробиш. => c_chat ?weather fog\n> Я люблю дощ. Не скаржся, дурний|дурна. || Дощ як дощ. Ти що, з цукру? => c_rude ?weather rain !tone rude\n> Я люблю сніг. Не скаржся, дурний|дурна. => c_rude ?weather snow !tone rude\n> Я люблю бурю. Не скаржся, дурний|дурна. => c_rude ?weather storm !tone rude\n> Я люблю туман. Не скаржся, дурний|дурна. => c_rude ?weather fog !tone rude\n> Ага. => start ?weather clear\n" +
            "@c_travels\n{concern}\n> Бу{в|ла}. І ще піду. || Ходи{в|ла}, ага. Цікаво там. || Ще й не таке бачи{в|ла}. => c_travels_told !tone warm\n> Ходімо зі мною, сам|сама побачиш. => c_follow ?hired !walkwith !tone warm\n> Потім розповім. || Довга історія. => start\n> Не твоє діло. || Тобі туди все одно не треба. => c_rude !tone rude\n" +
            "@c_work\n{concern}\n> Добре в тебе виходить. || Без тебе тут усе б стало. => c_work_praised !tone warm soft\n> Оце я розумію, робота. || Бачу. Діло робиться. => c_work_praised !tone warm blunt\n> Не заважатиму. || Працюй, працюй. => start\n> Щось заважає роботі? || Чого бракує для роботи? => how ?working\n> Менше балакай, більше роби. || Я тобі не за балачки плачу. => c_rude !tone rude\n" +
            "@c_people\n{concern}\n> Он воно як. || Та ну? А далі? || Цікаво. Розкажи ще. => c_gossip !tone warm\n> А хто ще тут живе? => folk ?resident\n> Це не моє діло. => start\n> Ви всі тут одне одного варті. || Пліткуєш, як на ярмарку. => c_rude !tone rude\n" +
            "@c_complaint\n{concern}\n> Розумію. Подумаю, що можна зробити. || Чую тебе. Спробую виправити. => c_heard !tone warm soft !promise mood\n> Гаразд, розберусь. || Зрозум{ів|іла}. Виправлю. => c_heard !tone warm blunt !promise mood\n> Чим тобі допомогти? || Що тобі треба? => need ?hasneed\n>| Чим тобі допомогти? || Що тобі треба? => goal ?hasgoal\n>| Чим тобі допомогти? || Що тобі треба? => nohelp\n> Мені зараз не до того. || Потім, добре? => end !tone cold\n> Перестань скиглити. || Всім важко, не тільки тобі. || Поскаржся комусь іншому. => c_rude !tone rude\n" +
            "@c_hungry\n{concern}\n> Тримай, поїж. || На, з'їж ось це. || Ось, бери. Не дякуй. => c_fed ?hasfood !givefood !taskdo feed !tone warm 2\n> Зараз щось принесу. || Буде їжа, обіцяю. => c_heard ?!hasfood !tone warm !promise food\n> У коморі ж є їжа. || Сходи до скрині, там є що поїсти. => c_pantry\n> Менше їж, більше роби. || Потерпиш, не помреш. => c_rude !tone rude\n" +
            "@c_low_hungry = c_hungry\n@c_low_starving = c_hungry\n" + // (its mood low for that: the same answers — a place that is another's, Alias)
            "@c_tired\n{concern}\n> Іди відпочинь, я прикрию. || Сядь, перепочинь трохи. => c_rest !letrest !tone warm soft\n> Годі на сьогодні. Спати. || Кидай усе. Це не прохання. => c_rest !letrest !tone warm blunt\n> Ще трохи, і відпочинеш. => start\n> Втомився|втомилася? А я, по-твоєму, гуляю? || Відпочинеш, коли зробиш. => c_rude !tone rude\n" +
            "@c_low_tired = c_tired\n" +
            "@c_ultimatum = c_others\n" + // (an ultimatum: the same answers as a quarrel — make peace, keep apart: NpcNeighbours)
            "@c_unpaid\n{concern}\n> Скарбничку поповню, не хвилюйся. || Вибач, що затягну{в|ла}. Заплачу. => c_pay !tone warm soft !promise pay\n> Заплачу. Слово. || Борг пам'ятаю. Буде. => c_pay !tone warm blunt !promise pay\n> А нащо тобі ті монети? || На що тобі платня, як тут і дах, і їжа? => coins\n> Розберусь. => start\n> Скажи спасибі, що годую. || Платня буде, коли буде робота. => c_rude !tone rude\n" +
            "@c_low_unpaid = c_unpaid\n" +
            "@c_hurt\n{concern}\n> Подряпина. Заживе. || Жити буду. || Бувало й гірше. => c_hurt_brave !tone warm blunt\n> Троль. Йому гірше. || Вовки. Тепер їх менше. || Сам{|а} перечепи{вся|лася}. Не питай. => c_hurt_told !tone warm joke 0.5\n> Допоможеш перев'язати? || Є чим рану промити? => favour ?favour bandage !favour bandage !tone warm\n> Не квокчи наді мною. || Своїм ділом займайся. => c_rude !tone rude\n" +
            "@c_building\n{concern}\n> Хочеш допомогти? || Як думаєш, гарно виходить? => c_build !tone warm\n> Та так, дрібниця. => start\n> Не стій над душею. || Без порад обійдусь. => c_rude !tone rude\n" +
            "@c_heavy\n{concern}\n> Допоможеш донести? || Не підставиш плече? => c_heavy_help !tone warm\n> Своя ноша не тягне. || Нічого, донесу. => start\n> Я тебе не пита{в|ла}. => c_rude !tone rude\n" +
            "@c_fishing\n{concern}\n> Хочеш, і тебе навчу? || Ловиться, як бачиш. => c_fish !tone warm\n> Ага, рибалю. => start\n> Не лякай мені рибу. => c_rude !tone rude\n" +
            "@c_raven\n{concern}\n> Він розумніший, ніж здається. || Хочеш погладити? Не раджу. => c_raven_talk !tone warm\n> Ворон як ворон. => start\n> Він розумніший за тебе. || Він хоч не балакає стільки. => c_rude !tone rude\n" +
            "@c_new_home\n{concern}\n> Ласкаво просимо. Освоюйся. || Тут добре, ось побачиш. => c_welcome !taskdo greet !tone warm soft\n> Ніхто тут не кусається. Крім мого ворона. || Правило одне: не годуй ворона з руки. Пальці ще знадобляться. => c_welcome !taskdo greet !tone warm joke\n> Ходімо, покажу тобі оселю. || Ходи за мною, все покажу. => c_follow ?hired !walkwith !taskdo greet !tone warm\n> Звикнеш. => start\n> Не подобається — двері там. => c_rude !tone rude\n" +
            // it asks about you, and recalls what you said (NpcTalkAsk)
            "@c_ask_you_rain\n{concern}\n> Люблю. Під дощ добре думається. || Люблю, коли ллє. => c_ask_heard !youare rain like !tone warm 0.5\n> Терпіти не можу. || Ні. Мокро й холодно. => c_ask_heard !youare rain hate !tone warm 0.5\n> Не скажу. || Сам{|а} не знаю. => start !youare rain none\n> Яке тобі діло? || Тебе це не обходить. => c_rude !tone rude\n" +
            "@c_ask_you_fight\n{concern}\n> Битися. Рука сама проситься. || Бій. Там усе просто. => c_ask_heard !youare fight like !tone warm 0.5\n> Будувати. Б'юся, бо мушу. || Краще щось звести, ніж зламати. => c_ask_heard !youare fight build !tone warm 0.5\n> Не скажу. || Сам{|а} не знаю. => start !youare fight none\n> Яке тобі діло? || Тебе це не обходить. => c_rude !tone rude\n" +
            "@c_ask_you_sleep\n{concern}\n> Ніч — мій час. || Я з тих, хто лягає під ранок. => c_ask_heard !youare sleep owl !tone warm 0.5\n> Встаю з сонцем. || Ранок. Уночі люди мають спати. => c_ask_heard !youare sleep lark !tone warm 0.5\n> Не скажу. || Сам{|а} не знаю. => start !youare sleep none\n> Яке тобі діло? || Тебе це не обходить. => c_rude !tone rude\n" +
            "@c_ask_you_food\n{concern}\n> М'ясо. Просто з вогню. || Добрий шмат м'яса. => c_ask_heard !youare food meat !tone warm 0.5\n> Риба. Свіжа, щойно з води. || Рибу люблю. => c_ask_heard !youare food fish !tone warm 0.5\n> Солодке. Мед, ягоди. || Щось із медом. => c_ask_heard !youare food sweet !tone warm 0.5\n> Не скажу. || Сам{|а} не знаю. => start !youare food none\n> Яке тобі діло? || Тебе це не обходить. => c_rude !tone rude\n" +
            "@c_ask_you_land\n{concern}\n> Море. Щоб вітер і сіль. || До моря. Завжди до моря. => c_ask_heard !youare land sea !tone warm 0.5\n> Ліс. Там тихо. || У ліс, під старі дерева. => c_ask_heard !youare land forest !tone warm 0.5\n> Гори. Звідти все видно. || Нагору, де сніг. => c_ask_heard !youare land peak !tone warm 0.5\n> Не скажу. || Сам{|а} не знаю. => start !youare land none\n> Яке тобі діло? || Тебе це не обходить. => c_rude !tone rude\n" +
            "@c_said\n{concern}\n> Ти диви, пам'ятаєш. || Не забув|забула, бач. => c_said_glad !tone warm soft\n> У тебе пам'ять, як у мого ворона. || Усе ти пам'ятаєш. Аж страшно. => c_said_glad !tone warm joke\n> Ага. => start\n> Менше слухай, що я кажу. => c_rude !tone rude\n" +
            Replies("c_ask_heard~nod", "c_said_glad") +
            // what it sees of you now (NpcVoice: you_*)
            "@c_you_wet\n{concern}\n> Злива застала. || Та в річку вскочи{в|ла}. || Бр{ів|ела} там, де мілко. Виявилось — глибоко. => c_you_told !tone warm 0.5\n> Обсохну біля вогню. => c_you_told !tone warm 0.5\n> Тобі яке діло? => c_rude !tone rude\n" +
            "@c_you_trophy\n{concern}\n> Сам{|а} вполюва{в|ла}. || Важко було, але впора{вся|лася}. => c_you_proud !tone warm 1.5\n> Та так, дрібниця. => c_you_told !tone warm 0.5\n> Не чіпай, моє. => c_rude !tone rude\n" +
            "@c_you_fought\n{concern}\n> Відби{вся|лася}. Усе гаразд. || Та так, дрібна сутичка. => c_you_told !tone warm 0.5\n> Поруч ще бродять. Будь обережний|обережна. => c_you_warned !tone warm\n> Не твоє діло. => c_rude !tone rude\n" +
            "@c_you_night\n{concern}\n> Не спиться. || Справи не чекають. => c_you_told !tone warm 0.5\n> А ти чого не спиш? => c_you_back\n> Ти мені не батько|мати. => c_rude !tone rude\n" +
            "@c_you_its_make\n{concern}\n> Твоя робота. Ношу з собою й дякую. || Добра річ вийшла. => c_you_praised !tone warm 1.5\n> Ага. => start\n> Знай{шов|шла} б кращу — носи{в|ла} б кращу. => c_rude !tone rude\n" +
            Replies("c_you_told", "c_you_proud~proud", "c_you_warned~nod", "c_you_praised~shy", "c_you_back~laugh") +
            "@c_fear\n{concern}\n> Усе вже минуло. Ти в безпеці. || Я тут. Нікого не підпущу. || Видихни. Вони пішли. => c_calm !tone warm soft\n> Наступного разу станемо разом. || Тримайся біля мене, як почнеться. => c_calm !tone warm blunt 1.5\n> Ходімо, побудеш біля мене. || Не бійся, тримайся поруч. => c_follow ?hired !walkwith !tone warm 1.5\n> Мені зараз не до того. || Потім, добре? => end !tone cold\n> Годі тремтіти. Соромно дивитись. || Боягузам тут не місце. => c_rude !tone rude 1.5\n" +
            "@c_joy\n{concern}\n> Рад{ий|а} за тебе! || Заслужив|заслужила. => c_glad !tone warm soft\n> Оце діло! З тебе мед. || Диви не лусни від щастя. => c_glad !tone warm joke\n> Ага. => start\n> Нема чого радіти. || Рано радієш. => c_rude !tone rude\n" +
            "@c_past\n{concern}\n> Співчуваю. || Мені шкода. Правда. || Таке не забувається, знаю. => c_sorry !tone warm soft 2\n> Ти це пережив|пережила. Не кожен зміг би. || Ти тут. Живий|Жива. Це головне. => c_sorry !tone warm blunt 2\n> Розкажи більше. || А що було далі? => more ?hasstory\n> Мені зараз не до того. || Потім, добре? => end !tone cold 1.5\n> Усім буває важко. Не ний. || Минуле минуло, годі. => c_rude !tone rude 2\n" +
            "@c_others\n{concern}\n> Ти маєш рацію. || Я б теж розізли{вся|лася}. => c_agree !tone warm\n> Помиріться вже. || Поговоріть, як люди. => c_peace !makepeace\n> Тримайтеся одне від одного подалі. || Не сходьтеся, раз не ладнаєте. => c_apart !keepapart\n> Мені зараз не до того. || Потім, добре? => end !tone cold\n> Обоє ви дурні. || Мені ваші сварки ні до чого. => c_rude !tone rude\n" +
            "@c_dream\n{concern}\n> Я подумаю, як допомогти. || Може, й допоможу. Розкажи. => goal ?hasgoal !tone warm !promise goal !goalshow\n> Гарна мрія. => start\n> Мрій менше. || Мрії хліба не дадуть. => c_rude !tone rude 1.5\n" +
            "@c_mem_insulted\n{concern}\n> Вибач, дарма я так. || Перегну{в|ла} я тоді. Вибач. => c_apology !forget insulted !tone warm 2\n> Забудь. || Проїхали. => start !forget insulted\n> Сказа{в|ла} те, що дума{в|ла}. || І що? => c_rude !tone rude\n" +
            // you stood before it saying nothing, last time (NpcTalkIdle): any answer lets it go
            "@c_mem_zoned_out\n{concern}\n> Вибач, задума{вся|лася}. => c_zoned_sorry !forget zoned_out !patience 3\n" +
            "> Та щось на мене найшло. Сам{|а} не знаю що. => c_zoned_odd !forget zoned_out !patience 2 !attitude 1\n" +
            "> Просто милу{вався|валася} тобою. => c_zoned_joke !forget zoned_out\n> Не твоє діло. => c_zoned_rude !forget zoned_out !irritate 5 !attitude -2\n" +
            "@c_zoned_sorry\n{@zoned_sorry}\n> {@say_ok} => start\n@c_zoned_odd\n{@zoned_odd}\n> {@say_ok} => start\n@c_zoned_joke\n{@zoned_joke}\n> {@say_ok} => start\n@c_zoned_rude\n{@zoned_rude}\n> {@say_ok} => start\n" +
            // its answer to yours: its words (concern_<place>) and the same three ways on (Replies)
            Replies("c_heard:care", "c_chat", "c_apology~nod", "c_news_told", "c_news_known", "c_travels_told", "c_work_praised", "c_gossip", "c_calm:care", "c_glad~laugh", "c_sorry:care", "c_agree",
                "c_fed~nod:welcome", "c_pantry~shrug:care", "c_rest", "c_pay", "c_hurt_brave", "c_hurt_told", "c_build", "c_heavy_help", "c_fish", "c_raven_talk~laugh", "c_welcome~nod") +
            "@c_rude\n{~shrug} {@concern_rude}\n> {@say_shrug} => start\n> {@say_bye} => end\n" +
            "@c_peace\n{peace}\n> {@say_ok} => start\n" +
            "@c_apart\n{apart}\n> {@say_ok} => start\n" + // (kept apart a few days: NpcNeighbours)
            "@c_follow\n{~nod} {@concern_follow}\n> Ходімо. => end\n" +
            "@c_dosed\n{@concern_dosed}\n> Одужуй. || {@say_care} => start\n> {@say_bye} => end\n" +
            "@nohelp\n{@help_none}\n> {@say_ok} => start\n" +
            // what happened while you were away (NpcAway: the story's tales first, then its own memories)
            "@away\n{away}\n> {@say_got} => start !awaytold\n> Дякую, що розповів|розповіла. => start !awaytold\n" +
            // its favours (NpcFavours), only with patience enough: it may say no
            "@ask\n{@favour_ask}\n" +
            "> Станцюй! => favour ?favour dance !favour dance\n> Подивись на мою річ. => favour ?favour examine !favour examine\n" +
            "> Покажи щось цікаве. => favour ?favour show !favour show\n> Посидь зі мною трохи. => favour ?favour sit !favour sit\n" +
            "> Дозволиш тебе перевдягнути? => favour ?favour dress !favour dress\n" +
            "> Поділишся їжею? => favour ?favour food !favour food\n> Подаруй щось на згадку. => favour ?favour gift !favour gift\n" +
            "> Розкажи те, чого нікому не казав|казала. => favour ?favour secret !favour secret\n> Ходімо зі мною, ніч же. => favour ?favour night !favour night\n" +
            "> Нічого, іншим разом. => start\n" +
            "@favour\n{favour}\n> То що ж тобі вдягти? => dress ?dressok\n> {@say_ok} => start\n> {@say_bye} => end\n",
        };

        // «c_x~nod:care»: a place where it answers what you said to its worry — its words concern_x (a gesture before them), then the same three ways on.
        // Your words there are made, not written (the user 2026-11-12: «Добре.» under every reply is a menu, not living speech): the themes say_*
        // (NpcMoreSayPatterns), by whom you say them to. «:care» it told a trouble (say_care), «:welcome» it thanked you (say_welcome); none: say_ok
        private static string Replies(params string[] ids)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string full in ids)
            {
                int c = full.IndexOf(':');
                string e = c < 0 ? full : full.Substring(0, c), close = c < 0 ? "ok" : full.Substring(c + 1);
                int t = e.IndexOf('~');
                string id = t < 0 ? e : e.Substring(0, t);
                sb.Append('@').Append(id).Append('\n').Append(t < 0 ? "" : "{" + e.Substring(t) + "} ").Append("{@concern_").Append(id.Substring(2)).Append("}\n")
                  .Append("> {@say_").Append(close).Append("} => start\n> {@say_more} => talk ?hired\n> {@say_bye} => end\n");
            }
            return sb.ToString();
        }

        // a place made each time by code, not written (the list of homes, of your clothes, of its neighbours, of what you carry for it): its id → what shows it.
        // A module's own goes here too; a made place is never «asked again» (Counts) and is there for the dry walk (Walk)
        internal static readonly Dictionary<string, Action<Player>> Places = new Dictionary<string, Action<Player>>();

        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "talk"); } }
        private static Dictionary<string, Talk> _all;

        internal static Talk Find(string id)
        {
            if (_all == null) Load();
            Talk t;
            return _all.TryGetValue(id ?? "", out t) ? t : null;
        }

        internal static void Reload() { _all = null; }

        private static void Load()
        {
            Register(); // (its conditions known before its words are read)
            _times = null; // (the answers to a place: of this reading)
#if RAVEN_STORY
            StoryTrackConds.Register(); // (the story's own too — «?? step …», the chapter's end put on a story person's questions: Parse — not left to the story's first pass coming sooner)
#endif
            Dictionary<string, Talk> raw = new Dictionary<string, Talk>();
            foreach (string s in Builtin) Parse(raw, s, "built-in");
            foreach (string s in More) Parse(raw, s, "built-in");
            Dictionary<string, string> files = StoryFiles.All(StoryFiles.Talk, Dir, RavenLooks.FromFiles);
            for (int pass = 0; pass < 2; pass++) // (each file's own talks first, then every «[talk x +]»: a chapter's file adds to a person's talk whatever the files' order — StoryFiles.Part)
                foreach (KeyValuePair<string, string> f in files)
                {
                    try { Parse(raw, StoryFiles.Part(f.Value, pass == 1, "talk"), f.Key); }
                    catch (Exception e) { Npcs.Log("talk file " + f.Key + ": " + e.Message); }
                }
            foreach (Talk t in raw.Values) { Aliases(t); EndThrough(t); }
            Dictionary<string, Talk> all = new Dictionary<string, Talk>();
            foreach (string id in raw.Keys) Built(id, raw, all, new HashSet<string>());
            _all = all;
        }

        // «@c_low_tired = c_tired»: a place that is another one of the same talk (the same answers, kept once)
        private static void Aliases(Talk t)
        {
            foreach (KeyValuePair<string, string> e in t.Alias)
            {
                Node n;
                if (t.Nodes.TryGetValue(e.Value, out n)) t.Nodes[e.Key] = n;
                else Npcs.Log("talk «" + t.Id + "»: @" + e.Key + " = " + e.Value + ": no such place");
            }
        }

        // «?- <places> : <condition>» (the header): once all the files are read, each «through» question of the first screen that leads to one of the
        // places holds only while the condition does not. A place no such question leads to: said in the log (storycheck stops it in the build)
        private static void EndThrough(Talk t)
        {
            Node start;
            if (t.Ends.Count == 0) return;
            t.Nodes.TryGetValue("start", out start);
            foreach (KeyValuePair<string[], Cond> e in t.Ends)
                foreach (string place in e.Key)
                {
                    int n = 0;
                    if (start != null) foreach (Answer a in start.Answers) if (a.Through && LeadsTo(a, place)) { a.When.Add(e.Value); n++; }
                    if (n == 0) Npcs.Log("talk «" + t.Id + "»: ?- " + place + ": no «through» question of its first screen leads there");
                }
        }

        // «?- watch_what watch_me : reached ch1 rite_oath»
        private static void ParseEnd(Talk t, string s, List<string> errors)
        {
            int c = s.IndexOf(':');
            string[] places = c > 0 ? s.Substring(0, c).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries) : new string[0];
            string cond = c > 0 ? s.Substring(c + 1).Trim() : "";
            List<Cond> when = places.Length > 0 && cond.Length > 0 && cond[0] != '!' && cond.IndexOf(',') < 0 ? StoryCond.Parse("!" + cond, errors) : null;
            if (when == null || when.Count != 1) { errors.Add("«?-" + s + "»: ?- <place> [place…] : <one condition, not turned round>"); return; }
            t.Ends.Add(new KeyValuePair<string[], Cond>(places, when[0]));
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
                both.Ravens.AddRange(n.Ravens); both.Ravens.AddRange(old.Ravens);
                t.Nodes[n.Id] = both;
            }
            return all[id] = t;
        }

        private static void Parse(Dictionary<string, Talk> into, string text, string from)
        {
            Talk t = null; Node n = null;
            int block = from == "built-in" ? -1 : StoryFiles.Block(from); // (a story file's chapter: its questions live no longer than it — the header)
            List<Cond> context = new List<Cond>();
            int front = -1; // (where the answers of a «@x +» into this very talk go: before the old ones)
            Answer last = null; // (the answer a «>|» right after it is a way of)
            List<string> errors = new List<string>();
            foreach (string raw in text.Split('\n'))
            {
                string line = StoryFiles.Bare(raw); // (a note after « #» is no words: «?? item …, !world x   # (…, …)» read its commas as conditions — the scroll's question never showed, 2026-11-15)
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[talk ") && line.EndsWith("]"))
                {
                    string head = line.Substring(6, line.Length - 7).Trim();
                    n = null;
                    if (head.EndsWith("+")) // (more for one already read: the built-in common from a story's file)
                    {
                        string id = head.Substring(0, head.Length - 1).Trim();
                        if (!into.TryGetValue(id, out t)) { errors.Add("[talk " + id + " +]: no talk «" + id + "» before it"); t = null; }
                        continue;
                    }
                    int colon = head.IndexOf(':');
                    t = new Talk { Id = (colon < 0 ? head : head.Substring(0, colon)).Trim(), Base = colon < 0 ? "" : head.Substring(colon + 1).Trim() };
                    into[t.Id] = t; continue;
                }
                if (t == null) continue;
                if (line.StartsWith("?-")) { ParseEnd(t, line.Substring(2), errors); continue; } // (the end of a «through» question, said by a later chapter's file: EndThrough)
                if (line.StartsWith("@"))
                {
                    string id = line.Substring(1).Trim();
                    int eq = id.IndexOf('=');
                    if (eq > 0) { t.Alias[id.Substring(0, eq).Trim()] = id.Substring(eq + 1).Trim(); n = null; last = null; continue; } // («@c_low_tired = c_tired»: Aliases, once all is read)
                    bool adds = id.EndsWith("+"); last = null;
                    n = new Node { Id = adds ? id.Substring(0, id.Length - 1).Trim() : id, Adds = adds };
                    Node had;
                    if (adds && t.Nodes.TryGetValue(n.Id, out had)) { n = had; front = 0; context = new List<Cond>(); continue; } // (more for a place of this very talk — the built-in common from a story file: its answers go first)
                    front = -1;
                    t.Nodes[n.Id] = n; context = new List<Cond>(); continue;
                }
                if (n == null) continue;
                if (line.StartsWith("~")) { ParseRaven(n, line, errors); continue; }
                if (line.StartsWith("??")) { context = StoryCond.Parse(line.Substring(2).Trim(), errors) ?? new List<Cond>(); continue; }
                if (line.StartsWith(">"))
                {
                    bool way = line.StartsWith(">|");
                    Answer a = ParseAnswer(line.Substring(way ? 2 : 1), errors);
                    if (a == null) continue;
                    a.When.AddRange(context);
#if RAVEN_STORY
                    if (block >= 0 && n.Id == "start")
                    {
                        a.Through = Says(a, "through");
                        if (Ages(a)) a.When.AddRange(StoryCond.Parse((a.Through ? "chfrom " : "chnow ") + block, errors) ?? new List<Cond>()); // (through: from its chapter on, no chapter's end)
                    }
#endif
                    if (way) { if (last == null) errors.Add("«>|" + a.Text + "»: no answer above it to be a way of"); a.Else = last; }
                    if (front >= 0) n.Answers.Insert(front++, a); else n.Answers.Add(a);
                    last = a;
                    continue;
                }
                last = null; // (a way follows its answer: words of the person between end the row; a raven's line, a «??» do not)
                n.Says = n.Says.Length == 0 ? line : n.Says + "\n" + line;
            }
            foreach (string e in errors) Npcs.Log("talk (" + from + "): " + e);
        }

#if RAVEN_STORY
        // a question that ages with the story (the header): all but a closing «{@say_…} => end» and one meant to stand for good («always»)
        private static bool Ages(Answer a)
        {
            if (a.To == "end" && a.Text.StartsWith("{@say_", StringComparison.Ordinal)) return false;
            return !Says(a, "always");
        }
        private static bool Says(Answer a, string word) { foreach (Cond c in a.When) if (!c.Not && c.A.Length > 0 && c.A[0] == word) return true; return false; }
#endif

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
            if (a.To.IndexOf('|') >= 0) a.Pool = PoolOf(a, errors);
            if (w.Length > from && w[from] == "once") { from++; if (a.Pool != null) errors.Add("«" + a.Text + "»: once on a draw — a place heard is not drawn already"); else { a.Once = true; if (a.To == "end") errors.Add("«" + a.Text + "»: once needs a place to lead to, «end» is none"); } }
            if (w.Length > from && w[from] == "dim") { a.Dim = true; from++; }
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

        // «a|b*2|c»: each place as many times as its chances (1–9)
        private static string[] PoolOf(Answer a, List<string> errors)
        {
            List<string> l = new List<string>();
            foreach (string part in a.To.Split('|'))
            {
                int star = part.IndexOf('*'), n = 1;
                string place = PlaceOf(part);
                if (place.Length == 0 || place == "end" || (star >= 0 && (!int.TryParse(part.Substring(star + 1), out n) || n < 1 || n > 9))) { errors.Add("«" + a.Text + "»: «" + part + "» in a draw is no place (place or place*1…9)"); continue; }
                for (int i = 0; i < n; i++) l.Add(place);
            }
            return l.Count > 0 ? l.ToArray() : null;
        }

        private static string PlaceOf(string part) { int star = part.IndexOf('*'); return star < 0 ? part : part.Substring(0, star); } // («b*2» → «b»)

        // where an answer may lead: its one place, or each of its draw's
        private static bool LeadsTo(Answer a, string place) { return a.Pool != null ? Array.IndexOf(a.Pool, place) >= 0 : a.To == place; }

        // «=> a|b*2|c» picked: one of its places at random, by its chances. A place kept heard (a «once» answer's, one the story asks about) is drawn
        // only once all are heard (Torvald's tales, the user 2026-11-16: any order, the true ones among the made-up)
        private static string Draw(Answer a, Player p)
        {
            List<string> fresh = new List<string>();
            foreach (string place in a.Pool) if (_talk == null || !Heard(p, _talk.Id, place)) fresh.Add(place);
            return fresh.Count > 0 ? fresh[UnityEngine.Random.Range(0, fresh.Count)] : a.Pool[UnityEngine.Random.Range(0, a.Pool.Length)];
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

        // a line of the person's comes in on the scroll (RavenBookTalk): its head and chest speak it to you, seen by every game near (NpcSpeech)
        internal static void LineShown(string text) { if (With != null && !Hushed) NpcSpeech.Window(With, text, Player.m_localPlayer); }
        internal static bool Hushed; // (the window's «all of it now», RavenBookTalk.Rush: what comes in then is shown, not spoken)
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
            NpcPatience.Register(); // (?patience, ?angry)
            NpcFavours.Register();  // (?favour)
            NpcAway.Register();     // (?awaynews)
            NpcPromises.Register(); // (?promised)
            NpcArcs.Register();     // (?arc, ?arcabout)
            NpcTasks.Register();    // (?taskabout)
            NpcThreads.Register();  // (?thread)
            Deeds["ravenreply"] = RavenReply; // (your answer to your raven's word: NpcTalkRaven)
            Places[Homes] = ShowHomes;
            Places[DressPlace] = p => ShowDress(p, null);
            WireAbout(); WireRecall(); WireGift(); WireSway(); WireDoor(); WireAsk(); // (its neighbours, what you went through, your gifts, talking it round: NpcTalkAbout, NpcTalkRecall, NpcTalkGift, NpcTalkSway)
            StoryCond.Register("hired", new StoryCond.Kind { Usage = "hired   (його хтось найняв)", Test = (p, a) => Him != null && Hirer(Him) != 0L });
            StoryCond.Register("mine", new StoryCond.Kind { Usage = "mine   (його найняв ти)", Test = (p, a) => Him != null && p != null && Hirer(Him) == p.GetPlayerID() });
            StoryCond.Register("resident", new StoryCond.Kind { Usage = "resident   (живе на базі)", Test = (p, a) => Him != null && NpcResident.Is(Him) });
            // a person in need (NpcRescue): what you have for it
            StoryCond.Register("hasfood", new StoryCond.Kind { Usage = "hasfood   (у тебе є готова їжа)", Test = (p, a) => p != null && NpcMeals.Plainest(p.GetInventory()) != null });
            StoryCond.Register("hasmead", new StoryCond.Kind { Usage = "hasmead   (у тебе є лікувальний мед)", Test = (p, a) => NpcVitals.Mead(p) != null });
            StoryCond.Register("hasitemof", new StoryCond.Kind { Usage = "hasitemof   (у тебе його загублена річ)", Test = (p, a) => NpcRescueSite.LostThing(p, Him) != null });
            StoryCond.Register("rescue", new StoryCond.Kind { Usage = "rescue   (у халепі чи щойно врятований, ще не найнятий)", Test = (p, a) => NpcRescueTalk.State(Him) != null });
            StoryCond.Register("hasstory", new StoryCond.Kind { Usage = "hasstory   (має що розповісти про себе)", Test = (p, a) => NpcBackstory.Has(Him) });
            StoryCond.Register("hasneed", new StoryCond.Kind { Usage = "hasneed   (він про щось просить)", Test = (p, a) => NpcRequests.Of(Him) != null });
            StoryCond.Register("hasrequest", new StoryCond.Kind { Usage = "hasrequest   (у тебе є те, що він просить)", Test = (p, a) => NpcRequests.CanGive(p, Him) });
            StoryCond.Register("job", new StoryCond.Kind { Usage = "job <id>   (його робота)", Min = 1, Test = (p, a) => Him != null && Him.Worker != null && Him.Worker.JobId == a[1] });
            // the moment and its home (a chat of two asks them too: no player in them)
            Deeds["tone"] = Tone; // («!tone warm|cold|rude»: NpcTalkTone)
            StoryCond.Register("daypart", new StoryCond.Kind { Dry = "free", Usage = "daypart morning|day|evening|night", Min = 1, Test = (p, a) => NpcVoice.DayPart() == a[1] });
            StoryCond.Register("weather", new StoryCond.Kind { Usage = "weather clear|rain|snow|storm|fog", Min = 1, Test = (p, a) => NpcVoice.Weather() == (a[1] == "clear" ? "" : a[1]) });
            StoryCond.Register("folk", new StoryCond.Kind { Usage = "folk <id>   (в його оселі хтось має цю роботу)", Min = 1, Test = (p, a) => HomeHasJob(Him, a[1]) });
            StoryCond.Register("raid", new StoryCond.Kind { Usage = "raid   (набіг довкола його оселі)", Test = (p, a) => NpcNews.RaidNear(Him) });
            // its state now (a line's situation: NpcPhrases — any player hears these, they are its own, not a player's story)
            StoryCond.Register("mood", new StoryCond.Kind { Usage = "mood low|mid|high   (його настрій: < 35 · 35–70 · > 70)", Min = 1, Check = w => w[1] == "low" || w[1] == "mid" || w[1] == "high" ? null : "mood low|mid|high",
                Test = (p, a) => { if (Him == null || !NpcResident.Is(Him)) return false; float m = NpcToil.Mood(Him); return a[1] == "low" ? m < 35f : a[1] == "high" ? m > 70f : m >= 35f && m <= 70f; } });
            StoryCond.Register("hungry", new StoryCond.Kind { Usage = "hungry   (він голодний)", Test = (p, a) => NpcHunger.IsHungry(Him) });
            StoryCond.Register("tired", new StoryCond.Kind { Usage = "tired   (він утомлений: від 60)", Test = (p, a) => Him != null && NpcToil.Tired(Him) >= 60f });
            StoryCond.Register("hurt", new StoryCond.Kind { Usage = "hurt   (він поранений: менше 60% здоров'я, при тямі)", Test = (p, a) => { Character c = Him != null ? Him.GetComponent<Character>() : null; return c != null && !NpcVitals.IsDown(Him) && c.GetHealthPercentage() < 0.6f; } });
            StoryCond.Register("seated", new StoryCond.Kind { Usage = "seated   (він сидить)", Test = (p, a) => NpcMind.SeatedOf(Him) });
            StoryCond.Register("fact", new StoryCond.Kind { Usage = "fact <слово>   (що каже про нього його історія: кульгає, зорі, боїться_води, оповідач…)", Min = 1, Test = (p, a) => Him != null && NpcBackstory.Fact(Him, a[1]) });
            StoryCond.Register("hasjob", new StoryCond.Kind { Usage = "hasjob   (він має роботу)", Test = (p, a) => Him != null && Him.Worker != null && Him.Worker.Works });
            StoryCond.Register("working", new StoryCond.Kind { Usage = "working   (він зараз при роботі)", Test = (p, a) => Him != null && NpcMind.LayerNow(Him) == NpcMind.Layer.Job }); // (on any game: the talk runs on yours, its mind on its owner's)
            StoryCond.Register("feel", new StoryCond.Kind { Usage = "feel fright|joy|awe|grief   (що він відчуває зараз)", Min = 1, Test = (p, a) => { string k; float f; return Him != null && NpcFeel.Of(Him, out k, out f) && k == a[1]; } });
            StoryCond.Register("friendnear", new StoryCond.Kind { Usage = "friendnear   (поруч, до 10 м, той, кого він любить)", Test = (p, a) => FriendNear(Him) != null });
        }

        // one it likes (warm and up) within FriendNearM of it now ({friend_near} its name — NpcPhrases values)
        internal const float FriendNearM = 10f;
        internal static NpcActor FriendNear(NpcActor a)
        {
            if (a == null) return null;
            List<NpcActor> near = new List<NpcActor>();
            foreach (NpcActor o in NpcActor.Live) if (o != null && o != a && (o.transform.position - a.transform.position).sqrMagnitude < FriendNearM * FriendNearM) near.Add(o);
            return near.Count > 0 ? NpcAttitude.Friend(a, near) : null;
        }

        private static bool HomeHasJob(NpcActor a, string job)
        {
            ZDOID heart = a != null ? NpcKit.GetRef(a.Zdo, NpcResident.KHeart) : ZDOID.None;
            foreach (NpcActor o in NpcHeart.Residents(heart)) if (o != null && o.Worker != null && o.Worker.JobId == job) return true;
            return false;
        }

        internal static long Hirer(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? z.GetLong(KHired, 0L) : 0L; }
        internal static bool Hired(NpcActor a) { return Hirer(a) != 0L; }

        // ---------------------------------------------------------------- talking
        private static Talk _talk;
        private static bool _greeted; // (this talk's {greet} said: coming back to it, a short word)

        // read («once»): kept among the character's flags — one for all worlds (the talk in another world does not offer it again)
        private static string ReadKey(string talk, string place) { return "talked_" + talk + "_" + place; }
        // heard in this very world (the user 2026-11-16: what one learnt is one's own, and in another world the book has only what that world's story has come to)
        private static string HereKey(string talk, string place) { return "heard." + talk + "." + place + "." + Story.WorldId; }
        // the places of talks the story asks about («asked»): heard, they are kept like a «once» answer's place (session: filled as the conditions are read)
        private static readonly HashSet<string> Noted = new HashSet<string>();
        internal static void Note(string talk, string place) { Noted.Add(talk + " " + place); }
        // that place of the talk heard by this player (its game's own: the flags are his character's) — in the talk, or from the person's own mouth
        // by the fire (StoryMeets: a true tale of Torvald's in a gathering's talk, the user 2026-11-16): kept as a «once» answer's place, and what of
        // the book it opened is said under its words
        internal static void Hear(Player p, string talk, string place)
        {
            if (p == null) return;
            bool knew = Heard(p, talk, place); // (asked again, or known from another world and come to here: the book gains nothing)
            Story.SetFlag(p, ReadKey(talk, place), true);
            Story.SetFlag(p, HereKey(talk, place), true);
#if RAVEN_STORY
            if (!knew) StorySaga.Learnt(p, talk, place); // (what of the book it opened: a line under its words)
#endif
        }

        // the condition «asked»: heard here — or in another world, and this world's story has come to it (Opened)
        internal static bool Heard(Player p, string talk, string place) { return p != null && (Story.Flag(p, HereKey(talk, place)) || (Story.Flag(p, ReadKey(talk, place)) && Opened(p, talk, place))); }
        // any place of that talk heard (a «once» answer read, a noted place, your raven's word in it — here, or elsewhere and come to here): you have talked with that one
        internal static bool HeardAny(Player p, string talk)
        {
            if (p == null) return false;
            string pre = ReadKey(talk, ""), here = "heard." + talk + ".", world = "." + Story.WorldId;
            foreach (string f in Story.Of(p).Flags)
            {
                if (f.StartsWith(here, StringComparison.Ordinal) && f.EndsWith(world, StringComparison.Ordinal)) return true;
                if (!f.StartsWith(pre, StringComparison.Ordinal)) continue;
                string place = f.Substring(pre.Length);
                if (place.StartsWith("raven_", StringComparison.Ordinal)) { int u = place.LastIndexOf('_'); if (u > 6) place = place.Substring(6, u - 6); } // (a raven's word: «raven_<place>_<n>» — NpcTalkRaven)
                if (Opened(p, talk, place)) return true;
            }
            return false;
        }

        // Learnt in another world: open here once this world's story has come to it — the one whose talk it is has come into this world (its «after:»),
        // and an answer that leads there has its time here (Progress). Cost: a talk's answers to a place gathered once a reading (TimesOf), then a few keys each
        private static Dictionary<string, List<List<Cond>>> _times;
        private static bool Opened(Player p, string talk, string place)
        {
#if RAVEN_STORY
            if (!StoryPeople.Came(talk)) return false;
            foreach (List<Cond> w in TimesOf(talk, place)) if (Progress(p, w)) return true;
            return false;
#else
            return true;
#endif
        }

#if RAVEN_STORY
        private static List<List<Cond>> TimesOf(string talk, string place)
        {
            Talk t = Find(talk); // (first: a reading anew empties the times too — Load)
            if (_times == null) _times = new Dictionary<string, List<List<Cond>>>();
            string key = talk + " " + place;
            List<List<Cond>> l;
            if (_times.TryGetValue(key, out l)) return l;
            l = new List<List<Cond>>();
            if (t != null) foreach (Node n in t.Nodes.Values) foreach (Answer a in n.Answers) if (LeadsTo(a, place)) l.Add(a.When);
            return _times[key] = l;
        }

        // an answer's time as far as it is the story's progress, which stays come once come: a stage stood at is a stage reached, the chapter going on is a chapter
        // begun, a quest active is a quest started. The rest — where it stands, what you carry, every «!…» (the end of its time) — is the moment, not the progress
        private static bool Progress(Player p, List<Cond> when)
        {
            foreach (Cond c in when)
            {
                if (c.Not || c.A.Length == 0) continue;
                bool ok;
                switch (c.A[0])
                {
                    case "world": case "wvar": case "boss": case "key": case "stage": case "reached": ok = c.Test(p); break;
                    case "step": ok = c.A.Length > 2 && StoryQuests.Reached(p, c.A[1], c.A[2]); break;
                    case "quest": ok = c.A.Length > 2 && c.A[2] == Story.Active ? StoryQuests.StateOf(p, c.A[1]).Length > 0 : c.Test(p); break;
                    case "chnow": case "chfrom": ok = c.A.Length > 1 && StoryQuests.ChapterNow(p) >= Errands.I(c.A[1], 0); break;
                    default: continue;
                }
                if (!ok) return false;
            }
            return true;
        }
#endif
        // an answer of a row of ways, or one above it, holds its conditions (read or not: the row has its answer)
        private static bool AboveHolds(Answer a, Player p) { for (Answer x = a; x != null; x = x.Else) if (StoryCond.All(p, x.When) || (x.Once && _talk != null && Story.Flag(p, ReadKey(_talk.Id, x.To)))) return true; return false; } // (read: it stays the row's answer, whatever you have since — the horn used up, «what horn?» never comes back)
        private static bool Offered(Answer a, Player p) { return StoryCond.All(p, a.When) && !(a.Once && _talk != null && Story.Flag(p, ReadKey(_talk.Id, a.To))); }

        // E on a person: its talk opens here (true), or it has none (false: it only greets)
        internal static bool Open(NpcActor a, Player p) { return Open(a, p, null); }

        // first: the window opened on this instead of its talk's beginning (its «Спорядження»: no words of its worry, no raven, nothing the story takes for a talk)
        private static bool Open(NpcActor a, Player p, Action<Player> first)
        {
            if (a == null || p == null || a.Def == null) return false;
            Register();
            Talk t = Find(a.Def.TalkId);
            if (t == null && a.Def.Talk.Length > 0) Npcs.Log(a.Id + ": no talk «" + a.Def.Talk + "»");
            if (t == null || !t.Nodes.ContainsKey("start")) return false;
            Close(); // (what an earlier talk left: none of it is this one's)
            if (ScenePlaying || InScene(a)) return true; // (a scene is on, here or holding this person on another game: no window over it — Tick closes one it finds)
            if (Busy(a, p)) return true; // (another player talks to it: one talk at a time, the user 2026-11-05 — its look stays on that one)
            With = a; _talk = t; _greeted = false; _openedAt = _touched = Time.time; _nextHold = 0f;
            if (first != null) first(p);
            else { if (Minded) Mind(p); Show("start", p); }
#if RAVEN_STORY
            if (With == a) TalkCamera.Begin(a); // (the camera over your shoulder: you left, it right — TalkCamera; a talk that closed at once takes none, else no close would give it back)
            if (With == a) StoryLook.Talk(a.GetComponent<Character>()); // (your head on its eyes, the game sends it to the others: StoryLook)
#endif
            a.Order("look " + NpcKit.Ref(p.GetZDOID())); // (it turns to you: session, read at once where it is run)
            if (first == null) StoryBus.Raise("npc_talk", a.Def.Id, 0, a.transform.position);
            return true;
        }

        // ---------------------------------------------------------------- what is on its mind (the talk's own state: one talk at a time)
        internal const float OpenAt = 30f, BrushAt = 70f, MindAt = 18f; // a worry this heavy opens its talk · this pressing comes before your own topic · this heavy is «ще на думці»
        private static List<NpcVoice.Concern> _mind = new List<NpcVoice.Concern>();
        private static readonly Dictionary<string, string> _lines = new Dictionary<string, string>(); // a worry's words this talk (its label and its opening the same)
        private static NpcVoice.Concern _top;
        private static bool _topDone, _brushed;
        private static string _prefix, _instead, _concern; // said before the next place's own words · said instead of them (a repeat) · {concern}
        private static string _youSaid; // (the answer you picked: your line over what it says next)

        // The hero says the answer you picked aloud, wordless, in his own voice (the user 2026-11-16: «щоб він теж бурмотів щось своїм голосом»):
        // the «hero» timbre of his sex (NpcTimbre.OfHero), hummed as a person's line is (NpcBabble). The person hears him out: its sound to your
        // answer (NpcActor.React) and its next words (RavenBookTalk: YouLeft) wait till he is done. Heard on his game only (the talk is his window).
        // Not said: a worry of its own picked from «на думці» (not his words, as it is not shown as his line), an answer cut off (CutIn), one no longer holding,
        // nor with «Instant talks» on (no waits there: its words would come over his). A new answer, or a click that shows the rest at once (YouCut), stops his line.
        internal static float HeroPerChar = 0.03f, HeroLeast = 0.5f, HeardOut = 0.12f, WordsAfter = 0.2f; // (the user 2026-11-17: its first words came too late — tuned live, halved) // s a letter of his line as it is meant to last · at least · s after him before its sound to it · and before its words (room for that sound; raven npc tune NpcTalk.<name>)
        private static float _youUntil; // (Time.unscaledTime its words may come: his line, then room for its sound to it)
        internal static float YouLeft { get { return Mathf.Max(0f, _youUntil - Time.unscaledTime); } }

        private const string HeroClip = "babble_hero";

        // how long the one you talk to hums this line of its (NpcSpeech: the plan its game makes for it), 0 when it is not hummed — the window
        // brings its next words after it (RavenBookTalk: Build)
        internal static float HumFor(string text) { return With != null && NpcBabble.On && !RavenVoice.Available && !RavenBook.TalkInstant ? NpcSpeech.Seconds(With, text) : 0f; } // (his line's name in the sound core: cut short by name)

        // his line stopped: the window shows the rest at once (RavenBookTalk: Rush)
        internal static void YouCut() { if (_youUntil > Time.unscaledTime) NpcBabble.Hush(HeroClip); _youUntil = 0f; }

        private static float HeroSays(Player p, string text)
        {
            YouCut(); // (a second answer picked while he still says the first)
            if (RavenBook.TalkInstant) return 0f;
            NpcTimbre.Card card = NpcTimbre.OfHero(p);
            Transform head = SitOn.HeadBone(p);
            if (card == null || head == null || string.IsNullOrEmpty(text)) return 0f;
            System.Text.StringBuilder plain = new System.Text.StringBuilder(text.Length);
            int tag = 0;
            foreach (char ch in text) { if (ch == '<') tag++; else if (ch == '>' && tag > 0) tag--; else if (tag == 0) plain.Append(ch); } // (a row's rich text: «<size=80%>» is no word)
            string said = plain.ToString().Trim();
            long id = p.GetPlayerID();
            float s = NpcBabble.Say(card, head, said, (uint)id ^ (uint)(id >> 32) ^ 0x4E30B1D5u, Mathf.Max(HeroLeast, said.Length * HeroPerChar), true, HeroClip);
            if (s > 0f) _youUntil = Time.unscaledTime + s + HeardOut + WordsAfter;
            return s > 0f ? s + HeardOut : 0f;
        }
        private static Row _keepWords; // a topic asked the first time: its words kept once shown («я ж казав…» says them)
        private static float _pat = -1f; // its patience as reckoned for this showing (−1: a talk with none)
        private static RavenBook.TalkRel _rel; // how it is with you as reckoned for this showing (the margins: a made place keeps them)
        private static bool Minded { get { return _talk != null && _talk.Nodes.ContainsKey("c_any"); } } // (a settler's talk: built on common)

        private static void Mind(Player p)
        {
            _mind = NpcVoice.Concerns(With, p);
            _top = null;
            if (NpcRescueTalk.State(With) != null) return; // (one in need: its trouble is its first words — NpcRescueTalk)
            foreach (NpcVoice.Concern c in _mind) { if (c.Weight < OpenAt) break; if (Line(c.Theme, p) != null) { _top = c; break; } }
        }

        private static string Line(string theme, Player p)
        {
            string s;
            if (_lines.TryGetValue(theme, out s)) return s;
            s = NpcPhrases.Compose(With, theme, p);
            if (string.IsNullOrEmpty(s)) s = null;
            _lines[theme] = s;
            return s;
        }

        // the place of a worry's answers: its own, its group's, any
        private static Node WorryNode(string theme)
        {
            Node n;
            if (_talk.Nodes.TryGetValue("c_" + theme, out n)) return n;
            NpcVoice.Group g = NpcVoice.GroupOf(theme);
            if (g != null && _talk.Nodes.TryGetValue("c_" + g.Id, out n)) return n;
            return _talk.Nodes.TryGetValue("c_any", out n) ? n : null;
        }

        private const string Worry = "c:"; // a place «c:<theme>»: a lesser worry picked from «ще на думці»

        // A story person's reply whose answers only lead back to its first screen — every one a made «{@say_…} => start», no deed, no condition, not
        // «once»: the talk flows on (the user 2026-11-15, Літ: «на кожну його репліку я відповідаю якось абстрактно… це ж не діалог»). Under its words
        // the first screen's questions at once, the next thing to ask, instead of a bare «Тепер ясно» and its greeting again. A reply with an answer of
        // its own, a deed («!raise»), a condition or a way elsewhere is written so on purpose: it opens or closes a line, and stays as written.
        // Not a settler's (Minded): its places end with an answer to how it feels, never a bare way back
        private static bool Flows(Node n)
        {
            if (n.Answers.Count == 0) return false;
            foreach (Answer a in n.Answers)
                if (a.To != "start" || a.Acts.Count > 0 || a.When.Count > 0 || a.Once || a.Else != null || !a.Text.StartsWith("{@say_", StringComparison.Ordinal)) return false;
            return true;
        }

        private static bool FlowsAt(string node) { Node n; return !Minded && _talk != null && _talk.Nodes.TryGetValue(node, out n) && Flows(n); }

        private static void Show(string node, Player p)
        {
            Node n = null;
            if (node == Out) { ShowOut(p); return; }
            Action<Player> made;
            if (_talk != null && With != null && Places.TryGetValue(node, out made)) { _youSaid = null; made(p); return; } // (a place made by code: the homes, your clothes, a module's own list)
            string worry = node.StartsWith(Worry, StringComparison.Ordinal) ? node.Substring(Worry.Length) : null;
            if (_talk != null && worry != null) n = WorryNode(worry);
            if (node == "end" || _talk == null || With == null || (n == null && !_talk.Nodes.TryGetValue(node, out n))) { Close(); return; }
            bool start = node == "start";
            float patience = -1f, toYou = -1f, now = -1f;
            string tip = Minded ? NpcPatience.Tip(With, p, out patience, out toYou, out now) : null; // (reckoned once a showing: the door, the bar, its box, the grey rows' why)
            _pat = patience;
            string shut = start ? Shut(With, p, toYou) : null; // (only how it is with you: NpcPatience.ToYou)
            RavenBook.TalkRel rel = _rel = Minded ? Relation(p, tip, now, toYou) : null;
            if (shut != null) { ShowDoor(p, shut, rel); return; } // (it will not talk now: NpcTalkDoor)
            bool opening = start && !_greeted && _top != null;
            if (worry != null)
            {
                string again = NpcPhrases.Compose(With, worry, p); // (its own words of it now: not the label you picked)
                _concern = !string.IsNullOrEmpty(again) && again != Line(worry, p) ? again : Line(worry, p) ?? "…";
                Told(p, With, worry);
            }
            else if (opening) { _concern = Line(_top.Theme, p); Told(p, With, _top.Theme); if (IgnoredLately(With, p, _top.Theme)) _prefix = NpcPhrases.Compose(With, "concern_ignored", p); } // (you left it unanswered last time: NpcTalkDoor)
            bool repeated = _instead != null; // (its words again, or an answer to the raven: no new word from it now)
            if (repeated && _afterRaven && start && Minded && _top != null && !_topDone && NpcRescueTalk.State(With) == null) { _prefix = _prefix != null ? _prefix + " " + _instead : _instead; _instead = null; } // (its worry still open after your word to your raven, a must: its word to that, then «так от, про що я…» and its worry — the worry's answers under the worry, not under its word to the raven)
            _afterRaven = false;
            string says = _instead ?? (opening ? _concern : node == Tales && Minded ? Untold(n, p) : start && Minded && _top != null && !_topDone && NpcRescueTalk.State(With) == null ? StillOnMind(p) : Fill(Lang.T(n.Says), p));
            if (opening && n.Says.Contains("{greet}")) _greeted = true;
            if (_keepWords != null) { _keepWords.Words = says != null && says.Length <= MostWords ? Clean(says, MostWords) : ""; SaveRows(p); _keepWords = null; } // (its own words only: not a brush-off before them; too long to keep whole — its story: not kept cut in two, «я ж казав» then says them anew)
            List<RavenBook.TalkSay> lines = new List<RavenBook.TalkSay>(); // (said one by one: what comes first, its own words, your raven among them)
            if (_prefix != null) lines.AddRange(RavenBook.Says(NpcBody.Spoken(With, _prefix)));
            lines.AddRange(RavenBook.Says(NpcBody.Spoken(With, says)));
            _instead = null; _prefix = null;

            List<RavenBook.TalkRow> rows = new List<RavenBook.TalkRow>();
            List<Answer> ans = new List<Answer>();
            bool rescue = NpcRescueTalk.State(With) != null;
            if (start && Minded && _top != null && !_topDone && !rescue) // (its worry: its answers first)
            {
                Node w = WorryNode(_top.Theme);
                if (w != null) foreach (Answer a in Mixed(w)) AddRow(rows, ans, a, p, 0);
            }
            if (start && Minded && !rescue && (patience >= NpcPatience.Dry || Bond(p, "dream") > 0)) // (what else is on its mind: a chatty one tells more; a dream it owes you, even on a dry day)
            {
                float chatty = NpcPersona.Of(With).Chatty;
                int most = chatty > 0.65f ? 3 : chatty < 0.35f ? 1 : 2, shown = 0;
                foreach (NpcVoice.Concern c in _mind)
                {
                    if (shown >= most) break;
                    if (c == _top || c.Weight < MindAt || NpcVoice.GroupOf(c.Theme) == null) continue; // (a worry of its own: not a word of the hour or a look at you)
                    string label = Line(c.Theme, p);
                    if (label == null) continue;
                    Answer a = new Answer { Text = label, To = Worry + c.Theme };
                    if (RepeatOff(a, p)) continue; // (asked too often: not offered now — the next thing on its mind instead, no grey row)
                    ans.Add(a); rows.Add(new RavenBook.TalkRow { Text = label, Ring = 1 });
                    shown++;
                }
            }
            RavenBook.TalkScrap scrap = start && !rescue ? Scrap(p, rows, ans) : null; // (its request: the pinned scrap — its own answers there, the way to it off «твоє»)
            bool insist = start && !rescue && Minded && Insists(p) && rows.Exists(r => r.Ring == 0); // (only while there is an answer to its worry to give)
            Node rowsOf = n, first;
            if (!start && !Minded && worry == null && Flows(n) && _talk.Nodes.TryGetValue("start", out first)) // (a story person's reply that only leads back: the talk flows on)
            {
                int had = rows.Count;
                foreach (Answer a in Mixed(first)) if (a.To != node) AddRow(rows, ans, a, p, 2); // (not the question just asked, under its own answer)
                if (rows.Count > had) rowsOf = null; // (nothing left to ask there: its own «Тепер ясно» after all)
            }
            if (rowsOf != null) foreach (Answer a in Mixed(rowsOf))
            {
                if (scrap != null && (a.To == "need" || a.Acts.Exists(x => x.StartsWith("giverequest", StringComparison.Ordinal)))) continue;
                int had = rows.Count;
                AddRow(rows, ans, a, p, start && !rescue ? 2 : 0);
                if (insist && rows.Count > had && !Essential(a) && !rows[had].Off) { rows[had].Off = true; rows[had].Tip = With.Form(Lang.F("Спершу вислухай: {0} хоче поговорити про своє.", With.Name)); } // (its own first: NpcTalkDoor)
            }
            List<RavenBook.TalkSay> cut = _cutNext != null ? new List<RavenBook.TalkSay> { _cutNext } : repeated ? null : Cut(n, node, worry, opening, rows, ans, p);
            if (cut == null && !repeated) cut = Third(node, worry, opening, p); // (no raven: perhaps one of its people near — NpcTalkThird)
            _cutNext = null;
            if (cut != null) lines.InsertRange(Mathf.Min(1, lines.Count), cut); // (after its first line: your raven does not wait for it to finish)
            string you = start ? null : _youSaid; // (back at the first screen: nothing of yours over it)
            _youSaid = null;
            if (ans.Count == 0) { ans.Add(new Answer { Text = "…", To = "end" }); rows.Add(new RavenBook.TalkRow { Text = "…" }); }
            List<Answer> keep = ans;
            List<RavenBook.TalkRow> keepRows = rows;
            RavenBook.ShowTalk(With.Name, lines, rows, i => Pick(keep, keepRows, i, node), Close, rel, scrap, start, you);
            if (n.Says.Contains("{greet}")) _greeted = true;
        }

        // «Розкажи щось»: a tale it has not told you yet (its words kept a short mark each in your character). A person knows only so many
        // (TalesKnown: by its chattiness) — all told: it says so (tale_none), not a repeat, you asked for a new one — until TalesAgain game days after the last one, when it has some again
        private const string Tales = "tale";
        private const int TaleTries = 8;
        internal static float TalesAgain = 5f;
        private static int TalesKnown(NpcActor a) { return Mathf.RoundToInt(Mathf.Lerp(6f, 20f, NpcPersona.Of(a).Chatty)); }
        private static string Untold(Node n, Player p)
        {
            Row r = PutRow(p, "k", With, "");
            if (Elsewhere(r) || NowSec - r.At > TalesAgain * 24f * GameHour) r.Words = "";
            List<string> told = new List<string>(r.Words.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            for (int i = 0; i < TaleTries && told.Count < TalesKnown(With); i++)
            {
                string s = Fill(Lang.T(n.Says), p);
                string mark = (s.GetStableHashCode() & 0xFFFFFF).ToString("x6");
                if (told.Contains(mark)) continue;
                told.Add(mark);
                r.Words = string.Join(",", told.ToArray()); r.At = NowSec;
                SaveRows(p);
                return s;
            }
            return NpcPhrases.Compose(With, "tale_none", p) ?? With.Form(Lang.T("Та я тобі вже все розповів|розповіла."));
        }

        // a settler's request as its pinned scrap (the user's mockup 2026-11-05): the thing, its pay, how long it waits, «ОБІЦЯНО» once you said
        // you would bring it; its answers on it — «Ось …» when you carry it all («3 / 3»), «Принесу.» until you promised (NpcPromises)
        private static RavenBook.TalkScrap Scrap(Player p, List<RavenBook.TalkRow> rows, List<Answer> ans)
        {
            NpcRequests.Request r = NpcRequests.Of(With);
            if (r == null || p == null) return null;
            bool promised = NpcPromises.Has(With, p.GetPlayerID(), "request");
            int have = NpcRequests.Carried(p, With);
            RavenBook.TalkScrap s = new RavenBook.TalkScrap { Item = r.Item, Words = NpcRequests.Words(r.Item, r.Count), Pay = NpcRequests.PayWords(r), Left = NpcRescue.Left(r.Until - (long)NowSec).TrimEnd('.'), Promised = promised };
            string mine = have >= r.Count ? Lang.T("Ось те, що ти просив|просила.") : Lang.T("Принесу.");
            if (have >= r.Count) { Answer a = new Answer { Text = mine, To = "thanks" }; a.Acts.Add("giverequest"); ans.Add(a); rows.Add(new RavenBook.TalkRow { Text = With.Form(mine), Ring = 3, Scrap = true, Have = have + " / " + r.Count }); }
            else if (!promised) { Answer a = new Answer { Text = mine, To = "start" }; a.Acts.Add("promise request"); ans.Add(a); rows.Add(new RavenBook.TalkRow { Text = mine, Ring = 3, Scrap = true, Have = have > 0 ? have + " / " + r.Count : null }); }
            return s;
        }

        // the story's own answer (its sealed card): one that moves a quest on — asked at a step of one, or raising its event
        private static bool IsStory(Answer a)
        {
            foreach (Cond c in a.When) if (!c.Not && c.A.Length > 0 && (c.A[0] == "step" || c.A[0] == "quest")) return true;
            foreach (string x in a.Acts) if (x.StartsWith("raise", StringComparison.Ordinal) || x.StartsWith("quest", StringComparison.Ordinal)) return true;
            return false;
        }

        // an answer onto the window: shown, grey with why (dim, or asked too often), or left out
        private static void AddRow(List<RavenBook.TalkRow> rows, List<Answer> ans, Answer a, Player p, int ring)
        {
            string tip = null;
            bool off = false;
            if (a.Else != null && AboveHolds(a.Else, p)) return; // (a way of the answer above: that one, or one before it, is the one)
            if (!Offered(a, p))
            {
                if (!a.Dim || !DimOnly(a, p)) return;
                off = true; tip = DimTip(a, p);
            }
            else if (RepeatOff(a, p)) { if (ring == 2) return; off = true; tip = RepeatTip(a, p); } // (yours on the first screen: left out till it may be asked again — the user 2026-11-12; in a place of its own it is grey with why)
            else if (Promises(a) && Bond(p, "word") < 0) { off = true; tip = With.Form(Lang.T("Не вірить твоїм обіцянкам: ти вже не дотрима{в|ла} слова.")); } // (NpcThreads: the thread «слово» cut)
            string text = Fill(Lang.T(OneOf(a.Text)), p);
            if (string.IsNullOrEmpty(text)) return; // (a made answer «{@…}» whose theme has nothing for this person now: not offered; a place left with no answer gets its «…» in Show)
            if (rows.Exists(r => r.Text == text)) return; // (the same words once a window: its worry's answer above, the start's own of it not again — the user's test 2026-11-05)
            ans.Add(a);
            rows.Add(new RavenBook.TalkRow { Text = text, Ring = ring, Off = off, Tip = tip, Story = IsStory(a) });
        }

        // «!tone warm soft|blunt|joke»: the air of a kind answer (NpcTalkTone)
        private static string AirOf(Answer a)
        {
            foreach (string x in a.Acts) if (x.StartsWith("tone warm ", StringComparison.Ordinal)) foreach (string w in x.Split(' ')) if (Array.IndexOf(NpcNature.Airs, w) >= 0) return w;
            return null;
        }

        // a place's answers as they are shown: its kind answers of different airs change places among themselves — by the person, the place and the game day
        // (steady while the window stands, another tomorrow), so that which of them is right is read from the person, not from the row. The rest stay where written
        private static List<Answer> Mixed(Node n)
        {
            List<int> at = null;
            for (int i = 0; i < n.Answers.Count; i++) if (AirOf(n.Answers[i]) != null) (at ?? (at = new List<int>())).Add(i);
            if (at == null || at.Count < 2 || With == null || With.Zdo == null) return n.Answers;
            int shift = (int)(NpcNature.H(NpcKit.Seed(With.Zdo), n.Id + Today) * at.Count) % at.Count;
            if (shift == 0) return n.Answers;
            List<Answer> l = new List<Answer>(n.Answers);
            for (int i = 0; i < at.Count; i++) l[at[i]] = n.Answers[at[(i + shift) % at.Count]];
            return l;
        }

        // your parting word, and your «нема за що», where code makes the row (the shut door, patience out, a gift's rows): made as a talk's «{@say_bye}» is
        internal static string Bye(NpcActor a, Player p) { string s = NpcPhrases.Compose(a, "say_bye", p); return !string.IsNullOrEmpty(s) ? s : Lang.T("Бувай."); }
        internal static string Welcome(NpcActor a, Player p) { string s = NpcPhrases.Compose(a, "say_welcome", p); return !string.IsNullOrEmpty(s) ? s : Lang.T("Нема за що."); }

        // «a || b || c»: your words for this answer, one of them a showing (each its own text to translate)
        private static string OneOf(string text)
        {
            if (text.IndexOf("||", StringComparison.Ordinal) < 0) return text;
            string[] w = text.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
            return w[UnityEngine.Random.Range(0, w.Length)].Trim();
        }

        // «dim»: shown grey only when what holds it back is its patience or its anger (another condition failing: not shown at all)
        private static bool DimOnly(Answer a, Player p)
        {
            NpcActor was = Speaker; Speaker = null;
            try { foreach (Cond c in a.When) if (!c.Test(p) && c.A.Length > 0 && c.A[0] != "patience" && c.A[0] != "mayask" && c.A[0] != "angry") return false; }
            finally { Speaker = was; }
            return true;
        }

        // why a grey answer is grey: the patience that very answer asks — its own «?patience >= N», else the favours' (mayask). It named the
        // favours' for every grey row: «Розкажи більше» asks 45 and said «від 75» (read 2026-11-12)
        private static string DimTip(Answer a, Player p)
        {
            if (NpcPatience.Angry(With)) return With.Form(Lang.T("Він|Вона надто злий|зла, щоб про це говорити."));
            float from = NpcPatience.AskFrom(With, p), n;
            foreach (Cond c in a.When)
                if (c.A.Length > 1 && c.A[0] == "patience" && (c.A.Length == 2 || c.A[1] == ">=" || c.A[1] == ">") && float.TryParse(c.A[c.A.Length - 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out n)) from = n;
            return With.Form(Lang.F("Для цього потрібно більше терпіння разом: від {0} (зараз {1}).", Mathf.RoundToInt(from), Mathf.RoundToInt(_pat)));
        }

        // its patience at the bottom: it ends the talk itself
        private const string Out = "\u0001out";
        private static void ShowOut(Player p)
        {
            string s = NpcPhrases.Compose(With, "patience_out", p) ?? With.Form(Lang.T("Годі. Мені час."));
            float v, toYou, now;
            string tip = NpcPatience.Tip(With, p, out v, out toYou, out now);
            WalkedOff(p); // (its door shut to you a while, and why: NpcTalkDoor)
            RavenBook.ShowTalk(With.Name, NpcBody.Spoken(With, s), new List<RavenBook.TalkRow> { new RavenBook.TalkRow { Text = Bye(With, p) } }, i => Close(), Close, Relation(p, tip, now, toYou), null);
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
                int fullOnes = list.RemoveAll(e => e.Folk.Count >= NpcResident.PerBase);
                foreach (NpcHeart.Entry e in list)
                {
                    if (ans.Count >= 8) break; // (no longer list than the scroll holds; the last one for «with me»)
                    float km = Utils.DistanceXZ(e.Pos, me) / 1000f;
                    Answer a = new Answer { To = "end" };
                    a.When.AddRange(StoryCond.Parse("!hired", null)); a.Acts.Add("hire"); a.Acts.Add("travel " + Npcs.V(e.Pos) + " " + NpcTravel.ToHeart + " " + NpcKit.Ref(e.Id));
                    ans.Add(a);
                    shown.Add(Lang.F("До «{0}»", e.Name) + " <size=80%>(" + (km >= 1f ? Lang.F("{0} км", km.ToString("0.#")) : Lang.F("{0} м", Mathf.RoundToInt(km * 1000f))) + ")</size>");
                }
                Answer with = new Answer { To = "end" }; with.When.AddRange(StoryCond.Parse("!hired", null)); with.Acts.Add("hire"); with.Acts.Add("follow");
                ans.Add(with); shown.Add(Lang.T("Ні, ходімо зі мною."));
                string says = list.Count > 0 ? who.Form(Lang.T("Куди саме? Скажи — дійду сам|сама.")) : fullOnes > 0 ? Lang.T("В усіх осель, які знаю, уже повно людей. Хіба що зі мною.") : Lang.T("Я не знаю жодної оселі. Постав «Серце оселі» й дай йому назву — тоді й скажеш, куди йти.");
                List<Answer> keep = ans;
                RavenBook.ShowTalk(who.Name, says, shown, i => Pick(keep, i), Close);
            });
        }

        private static void Pick(List<Answer> ans, int i) { Pick(ans, null, i, ""); }

        private static void Pick(List<Answer> ans, List<RavenBook.TalkRow> rows, int i, string from)
        {
            Player p = Player.m_localPlayer;
            if (p == null || With == null || i < 0 || i >= ans.Count) { Close(); return; }
            if (rows != null && i < rows.Count && rows[i].Off) { IdleStir(); return; } // (grey: nothing, but you are here)
            string back = IdleBack(p);
            Answer a = ans[i];
            _youSaid = rows != null && i < rows.Count && rows[i].Ring != 1 ? rows[i].Text : null; // (your line on top of what follows: not a worry of its own you picked from «на думці»)
            if (!Offered(a, p)) { Npcs.Log(With.Id + ": «" + a.Text + "» no longer holds (the window stood open a while)"); _prefix = back; Show(from.Length > 0 && from != DoorPlace && !FlowsAt(from) ? from : "start", p); return; } // (hired by another meanwhile, the thing gone: the place shown as it is now — a reply that flows on: not its words again, the first screen)
            string cutIn = CutIn(a, rows, i, from, p);
            if (cutIn != null) { _instead = cutIn; Show(from.Length > 0 && from != DoorPlace ? from : "start", p); return; } // (it cuts in with its own words: your question stands over them, not asked — NpcTalkCutIn.cs)
            float heard = _youSaid != null ? HeroSays(p, _youSaid) : 0f; // (he says it: what it does to the person comes once he is done)
            int ring = rows != null && i < rows.Count ? rows[i].Ring : 0;
            if (from == "start" && ring == 0 && _top != null) { _topDone = true; Answered(p); } // (its worry answered: not offered again this talk, not held against you)
            if (from == "start" && ring == 2 && Brushes(a)) { _brushed = true; _prefix = BrushWords(p); } // (its own first)
            string to = a.Pool != null ? Draw(a, p) : a.To; // (where it leads now: a draw's place, picked here; the answer itself — its repeats, its cut-in — stays the whole draw)
            if (_talk != null && (a.Once || Noted.Contains(_talk.Id + " " + to))) Hear(p, _talk.Id, to);
            bool counted = Counts(a);
            if (counted) Repeat(a, p);
            if (!a.Acts.Exists(x => x.StartsWith("sway ", StringComparison.Ordinal))) _sway = null; // (talking it round is the answer right after its no: NpcTalkSway)
            bool warmed = WarmedToday(a, ring, from, p);
            foreach (string act in a.Acts) if (!warmed || !Warms(act)) Do(act, p);
            if (With != null) With.React(NpcSpeech.ReactionTo(a.Acts, to == "end"), heard); // (what it makes of your answer, aloud and wordless: glad, kind, sour — now and then your goodbye)
            if (Minded && (counted || ring == 1) && to != "end" && Bond(p, "life") <= 0 && NpcPatience.ToYou(With, p) < NpcPatience.Out && UnityEngine.Random.value < WalksOff) { _instead = null; _prefix = null; Show(Out, p); return; } // (its patience to you at the bottom — not its bad day: it may end it; never to one who saved its life)
            if (back != null) _prefix = _prefix != null ? back + " " + _prefix : back;
            Show(to, p);
        }

        // a kind answer to a worry (its «!tone warm», «!attitude +N», «!patience +N») warms it once a game day a worry: its worry's own places only (the opening's answers, one picked from «ще на думці»). Said again the same day: the words, nothing more
        private static bool Warms(string act)
        {
            string[] w = act.Split(' ');
            float by;
            if (w.Length > 1 && w[0] == "tone" && w[1] == "warm") return true;
            return w.Length > 1 && (w[0] == "attitude" || w[0] == "patience") && float.TryParse(w[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out by) && by > 0f;
        }

        private static bool WarmedToday(Answer a, int ring, string from, Player p)
        {
            string theme = from.StartsWith(Worry, StringComparison.Ordinal) ? from.Substring(Worry.Length) : from == "start" && ring == 0 && _top != null ? _top.Theme : null;
            if (theme == null || !a.Acts.Exists(Warms)) return false;
            int today = Today;
            Row r = FindRow(p, "g", With, theme);
            if (r != null && !Elsewhere(r) && r.Count == today) return true;
            r = PutRow(p, "g", With, theme);
            r.Count = today; r.At = NowSec;
            SaveRows(p);
            return false;
        }

        // back at the first screen with its worry still unanswered (you went to your own topic): it brings it up again, in other words, so its answers under them are about something said
        // (2026-11-08: «Ну що, ще щось?» stood over «Тримай, поїж» — answers to words no longer on the screen)
        private static string StillOnMind(Player p)
        {
            string lead = NpcPhrases.Compose(With, "concern_back", p) ?? With.Form(Lang.T("Так от, про що я…"));
            string again = NpcPhrases.Compose(With, _top.Theme, p);
            _concern = !string.IsNullOrEmpty(again) ? again : _lines[_top.Theme];
            return lead + " " + _concern;
        }

        // a brush-off's words: a lead, then its worry again in other words than it opened with (the same words twice: the lead alone)
        private static string BrushWords(Player p)
        {
            string lead = NpcPhrases.Compose(With, "brushoff", p) ?? With.Form(Lang.T("Потім про це."));
            string again = NpcPhrases.Compose(With, _top.Theme, p);
            return !string.IsNullOrEmpty(again) && again != _lines[_top.Theme] ? lead + " " + again : lead;
        }

        internal static float WalksOff = 0.4f; // the chance a person at the bottom of its patience ends the talk after a topic

        // a pressing worry comes before your topic (once a talk): a grumbler always, a timid one never, the rest one time in two
        private static bool Brushes(Answer a)
        {
            if (_top == null || _topDone || _brushed || _top.Weight < MindAt || !_lines.ContainsKey(_top.Theme) || _lines[_top.Theme] == null || NotCounted.Contains(a.To)) return false;
            HashSet<string> t = NpcPhrases.Tones(With);
            if (t.Contains("боязк")) return false;
            return t.Contains("бурч") || UnityEngine.Random.value < 0.5f;
        }

        // ---------------------------------------------------------------- asked again (the user 2026-11-05: a check for every talk)
        // what is never a topic asked again: moving about the window, and what you do (hire, its request, its rescue…: those carry deeds)
        private static readonly HashSet<string> Duties = new HashSet<string> { "hire", "fire", "need", "thanks", "goaldone", "saved", "what", "wait", "guard" }; // (what it must do with you whatever its mood: NpcTalkDoor.Essential)
        private static readonly HashSet<string> NotCounted = new HashSet<string>(Duties) { "start", "talk", "end", "tale", "away", "ask", "favour", "nohelp", "recall", "passon", "swayed" };
        private static bool Counts(Answer a) { return a.Acts.Count == 0 && !a.Once && !NotCounted.Contains(a.To) && !Places.ContainsKey(a.To) && !a.To.StartsWith("c_", StringComparison.Ordinal) && !IsStory(a) && !LeadsToStory(a.To); }

        // the way to what moves the story on (its answer «Гаразд, збудую» has no deed of its own, the next one raises the event): never shut. Back at «start» is no way on: every place leads there
        private static bool LeadsToStory(string to)
        {
            if (_talk == null) return false;
            bool known;
            if (_talk.Leads.TryGetValue(to, out known)) return known;
            return _talk.Leads[to] = Leads(to, new HashSet<string>());
        }

        private static bool Leads(string to, HashSet<string> seen)
        {
            if (to.IndexOf('|') >= 0) { foreach (string part in to.Split('|')) if (Leads(PlaceOf(part), seen)) return true; return false; } // (a draw «a|b*2»: any of its places — so its answer, too, is never shut, cut into or counted)
            Node n;
            if (to == "start" || to == "end" || !seen.Add(to) || !_talk.Nodes.TryGetValue(to, out n)) return false;
            foreach (Answer x in n.Answers) if (IsStory(x) || Leads(x.To, seen)) return true;
            return false;
        }

        // the topic's key: the talk, where it leads, and what it is now (its mood, hunger, tiredness, work, feeling): what it is changed — a new topic
        private static string Sig(NpcActor a, string to)
        {
            if (to.StartsWith(Worry, StringComparison.Ordinal)) return to;
            string feel; float f;
            NpcFeel.Of(a, out feel, out f);
            return to + "." + Mathf.FloorToInt(NpcToil.Mood(a) / 25f) + (NpcHunger.IsHungry(a) ? "h" : "") + (NpcToil.Tired(a) >= 70f ? "t" : "") + (NpcMind.LayerNow(a) == NpcMind.Layer.Job ? "w" : "") + feel;
        }

        private static string RepeatKey(Answer an) { return _talk.Id + "." + Sig(With, an.To); }
        private static int Lately(Row r) { return r == null || Elsewhere(r) ? 0 : Mathf.Max(0, r.Count - Mathf.FloorToInt((NowSec - r.At) / GameHour)); } // (one fewer a game hour)
        private static int OffAt(NpcActor a) { return NpcPhrases.Tones(a).Contains("бурч") ? 3 : 4; } // (asked this many times lately: the topic goes grey)

        private static bool RepeatOff(Answer a, Player p) { return Counts(a) || a.To.StartsWith(Worry, StringComparison.Ordinal) ? Lately(FindRow(p, "r", With, RepeatKey(a))) >= OffAt(With) : false; }

        private static string RepeatTip(Answer a, Player p)
        {
            Row r = FindRow(p, "r", With, RepeatKey(a));
            int hours = Mathf.Max(1, Lately(r) - OffAt(With) + 1);
            return With.Form(Lang.F("Не хоче знову про це говорити (ще ~{0} год).", hours));
        }

        // a topic asked: the first time its words are kept; then «я ж казав» and those words, then made lines, harder by its ways and
        // softer to one dear; each costs some of its patience, the later ones anger it and, once a day, cool it to you
        private static void Repeat(Answer an, Player p)
        {
            Row r = PutRow(p, "r", With, RepeatKey(an));
            int n = Lately(r) + 1;
            r.Count = n; r.At = NowSec;
            int step = n;
            if (n >= 2)
            {
                HashSet<string> t = NpcPhrases.Tones(With);
                if (t.Contains("бурч")) step++;
                if (t.Contains("весел") || NpcPersona.Of(With).Chatty > 0.7f) step--;
                if (NpcPatience.Of(With, p) < NpcPatience.Out) step++;
                step = Mathf.Max(2, step);
            }
            if (step == 1 || (step == 2 && NpcPhrases.Tones(With).Contains("стар") && UnityEngine.Random.value < 0.33f)) // (the first time — or an old one has forgotten it told you)
            {
                if (step == 2) _prefix = NpcPhrases.Compose(With, "repeat_old", p);
                _keepWords = r;
                SaveRows(p);
                return;
            }
            bool dear = NpcAttitude.Level(With, p) == "close";
            if (step == 2) { string lead = NpcPhrases.Compose(With, "repeat_1", p) ?? With.Form(Lang.T("Я ж казав|казала.")); if (r.Words.Length > 0) _instead = lead + " " + r.Words; else _prefix = lead; }
            else _instead = Harder(step >= 4 ? "repeat_3" : "repeat_2", dear, p);
            NpcPatience.Spend(With, p, step == 2 ? 2f : step == 3 ? 5f : 10f);
            if (step >= 3) NpcPatience.Irritate(With, p, step == 3 ? 3f : 8f);
            if (step >= 4)
            {
                Row day = PutRow(p, "a", With, "");
                int today = Today;
                if (day.Count != today) { day.Count = today; NpcAttitude.Tell(With, p.GetPlayerID(), -3f); }
                day.At = NowSec;
            }
            SaveRows(p);
            Npcs.Log(With.Id + ": asked again «" + an.To + "» (" + n + " lately, step " + step + ")");
        }

        // its harder words: one dear teases instead (theme_close), none of those: the softer step
        private static string Harder(string theme, bool dear, Player p)
        {
            string s = null;
            if (dear) s = NpcPhrases.Compose(With, theme + "_close", p);
            if (string.IsNullOrEmpty(s)) s = NpcPhrases.Compose(With, theme, p);
            if (string.IsNullOrEmpty(s) && theme == "repeat_3") return Harder("repeat_2", dear, p);
            return !string.IsNullOrEmpty(s) ? s : With.Form(Lang.T("Я ж казав|казала."));
        }

        private static float _nextHold, _openedAt, _touched;
        private static bool InScene(NpcActor a)
        {
#if RAVEN_STORY
            return StoryScenes.Holder(a) != 0L;
#else
            return false;
#endif
        }

        internal static bool ScenePlaying
        {
            get
            {
#if RAVEN_STORY
                return StoryScenes.Playing.Length > 0;
#else
                return false;
#endif
            }
        }

        // another player holds it now — in a talk or its menu (NpcMind.HeldBy): not opened, a word on the screen instead (the one
        // check for both: NpcTalk.Open, NpcMenu.Key)
        internal static bool Busy(NpcActor a, Player p)
        {
            ZDOID by = NpcMind.HeldBy(a);
            if (by.IsNone() || by == p.GetZDOID()) return false;
            string who = null;
            foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetZDOID() == by) who = o.GetPlayerName();
            if (who == null) return false; // (held by no player here: an order, a scene — its own business)
            if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, Lang.F("{0} зараз розмовляє з: {1}", a.Name, who));
            return true;
        }

        internal static void Close()
        {
            Player me = Player.m_localPlayer;
            MarkIgnored(me); // (its opening worry left unanswered: before the notes go to the corner)
            RavenBook.FlushTalkNotes(me); // (an answer that closed the talk: what it moved, in the corner)
            if (With != null && me != null) With.Act("unhold " + NpcKit.Ref(me.GetZDOID())); // (free for another at once)
#if RAVEN_STORY
            TalkCamera.End();
            StoryLook.TalkEnd();
#endif
            IdleOff();
            NpcFavours.DressFor = null;
            _youUntil = 0f; // (his last line's room is this talk's: a talk opened a moment later has its first words at once — his voice is not cut, YouCut would)
            With = null; _talk = null; RavenBook.HideTalk();
            _mind = new List<NpcVoice.Concern>(); _lines.Clear(); _top = null; _topDone = false; _brushed = false; CutInReset();
            _prefix = null; _instead = null; _concern = null; _keepWords = null; _pat = -1f; _cutRolled = false; _cutNext = null; _afterRaven = false; _youSaid = null; _thirdRolled = false; _sway = null; _doorOpened = false; _sorryTried = false; _doorNoted = false; _doorWhy = null; _rel = null;
        }

        // walked off, it gone or senseless, you hit by a foe (your hero stands while the window is open), a scene began: the talk ends. Too long with no click: IdleTick.
        // «With» may be a person just destroyed, which reads as null: _talk says a talk is open.
        internal static void Tick()
        {
            if (_talk == null) return;
            Player p = Player.m_localPlayer;
            if (With == null || p == null || p.IsDead() || Vector3.Distance(p.transform.position, With.transform.position) > 8f
                || NpcVitals.IsDown(With) || NpcRescueHits.Died(With) || Senses.HurtByFoeAt > _openedAt || ScenePlaying || InScene(With)) { Close(); return; }
            if (IdleTick(p)) return;
#if RAVEN_STORY
            StoryLook.Tick(p); // (your head on its eyes: StoryLook.Talk)
#endif
            if (Time.time >= _nextHold) { _nextHold = Time.time + 2f; With.Act("hold " + NpcKit.Ref(p.GetZDOID())); } // (it stands while you talk)
        }

        private static string Fill(string s, Player p)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = NpcPhrases.Inline(With, s, p); // («{@theme}»: a made line where the author asked for one)
            NpcDef d = With.Def;
            if (_greeted && s.Contains("{greet}")) s = s.Replace("{greet}", Again(p));
            if (s.Contains("{greet}")) { string plea = NpcRescueTalk.Greet(With); if (plea != null) s = s.Replace("{greet}", plea); } // (one in need: its own trouble first — NpcRescueTalk)
            if (s.Contains("{thanks}")) s = s.Replace("{thanks}", NpcRescueTalk.Thanks()); // (just saved by an answer: its flag is still on its way to the game that runs it)
            if (s.Contains("{plea_more}")) s = s.Replace("{plea_more}", NpcRescueTalk.More(With) ?? Lang.T("Та вже нічого."));
            if (s.Contains("{greet}")) s = s.Replace("{greet}", d.Greet.Count > 0 ? NpcPhrases.Inline(With, Lang.T(d.Greet[UnityEngine.Random.Range(0, d.Greet.Count)]), p) : Lang.T("Чого тобі?")); // («greet: {@see_player}»: a made one)
            if (s.Contains("{concern}")) s = s.Replace("{concern}", _concern ?? "…"); // (the worry this place is about: its words)
            foreach (KeyValuePair<string, Func<NpcActor, Player, string>> w in Words) if (s.Contains(w.Key)) s = s.Replace(w.Key, w.Value(With, p));
            if (s.Contains("{doing}")) s = s.Replace("{doing}", Doing(With));
            if (s.Contains("{jobwords}")) s = s.Replace("{jobwords}", (With.Worker != null && With.Worker.Works ? NpcPhrases.Compose(With, "work_" + With.Worker.JobId, p) : null) ?? Lang.T("Робота як робота.")); // (asked of its work: the words of its own job)
            if (s.Contains("{story_short}")) { string st = NpcBackstory.Text(With, false); s = s.Replace("{story_short}", st.Length > 0 ? st : Lang.T("Більше й казати нічого.")); }
            if (s.Contains("{story_more}")) { string st = NpcBackstory.Rest(With); s = s.Replace("{story_more}", st.Length > 0 ? st : Lang.T("Більше й казати нічого.")); }
            if (s.Contains("{story}")) s =s.Replace("{story}", NpcBackstory.Text(With, true));
            if (s.Contains("{request}")) s = s.Replace("{request}", NpcRequests.Text(With));
            if (s.Contains("{reward}")) s = s.Replace("{reward}", NpcRequests.LastPaid);
            s = s.Replace("{npc}", With.Name).Replace("{name}", p.GetPlayerName()); // (a settler's own name, not its template's)
            return With.Form(Grammar.Fix(s, p)); // (your words in your form, then its own: «пропав|пропала»)
        }

        // back at the talk's beginning: a short word, not a new greeting (one in need: still its trouble)
        private static string Again(Player p)
        {
            string state = NpcRescueTalk.State(With);
            string line = NpcPhrases.Compose(With, state != null && state != "saved" ? "talk_again_need" : "talk_again", p);
            return !string.IsNullOrEmpty(line) ? line : "…";
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
            Talk t = Find(a.Def.TalkId);
            if (t == null) return a.Id + ": no talk" + (a.Def.Talk.Length > 0 ? " «" + a.Def.Talk + "»" : " (written by hand: it only greets)");
            NpcActor was = With; With = a;
            Talk wasTalk = _talk; _talk = t; // (what it has read is asked of this talk)
            bool wasTrying = NpcPhrases.Trying; NpcPhrases.Trying = true; // (a dry walk: nothing of it counts as said)
            StringBuilder sb = new StringBuilder("[talk walk] " + a.Name + " (" + a.Id + ", talk «" + t.Id + "»)\n");
            int bad = 0;
            try
            {
                if (t.Nodes.ContainsKey("c_any")) // (what is on its mind now, heaviest first, and its patience to you)
                {
                    sb.Append("patience ").Append(Mathf.RoundToInt(NpcPatience.Of(a, p))).Append(" (").Append(NpcPatience.Level(NpcPatience.Of(a, p))).Append(")\nmind:");
                    foreach (NpcVoice.Concern c in NpcVoice.Concerns(a, p)) sb.Append(' ').Append(c.Theme).Append('=').Append(Mathf.RoundToInt(c.Weight)).Append(c.Personal ? "*" : "");
                    sb.Append('\n');
                }
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
                    foreach (RavenLine rl in n.Ravens) sb.Append("   ~ ").Append(rl.Who).Append(rl.Aloud ? " aloud: " : " aside: ").Append(rl.Text).Append('\n');
                    if (says.Contains("{")) { sb.Append("   ! a {…} left in its words\n"); bad++; }
                    foreach (Answer an in n.Answers)
                    {
                        bool on = Offered(an, p) && !(an.Else != null && AboveHolds(an.Else, p));
                        string[] ways = an.Pool ?? new[] { an.To };
                        bool there = Array.TrueForAll(ways, w => w == "end" || Places.ContainsKey(w) || t.Nodes.ContainsKey(w));
                        if (!there) bad++;
                        string text;
                        try { text = Fill(Lang.T(an.Text), p); } catch (Exception e) { text = "FILL FAILED: " + e.Message; bad++; } // quiet: written into the walk, which goes to the log
                        sb.Append("   ").Append(on ? "> " : "x ").Append(text).Append(" => ").Append(an.To).Append(there ? "" : " (NO SUCH PLACE)");
                        if (an.Acts.Count > 0) sb.Append("  [").Append(string.Join(", ", an.Acts.ToArray())).Append(']');
                        sb.Append('\n');
                        foreach (string w in ways) if (t.Nodes.ContainsKey(w) && !Places.ContainsKey(w) && seen.Add(w)) next.Enqueue(w);
                    }
                }
                List<string> lone = new List<string>();
                foreach (string id in t.Nodes.Keys) if (!seen.Contains(id)) lone.Add(id);
                if (lone.Count > 0) sb.Append("never reached from start: ").Append(string.Join(", ", lone.ToArray())).Append('\n');
            }
            finally { With = was; _talk = wasTalk; NpcPhrases.Trying = wasTrying; }
            Npcs.Log(sb.ToString());
            return a.Name + ": " + t.Nodes.Count + " places, problems " + bad + " (the whole walk: the log, «[talk walk]»)";
        }

        // ---------------------------------------------------------------- what an answer does
        //   hire · unhire · follow · free · say <words> · take <Prefab> <N> · give <Prefab> <N> · flag <name> · unflag <name>
        //   var <name> <+N> · quest <id> <state> · raise <event> · travel x y z heart|hall [user:id]
        //   the story's levers on the person (NpcLevers): npcflag · npcunflag · npcvar · trait · become · protect · unprotect · nojob · okjob
        //   its memory and feeling of you: remember <kind> [what] · forget <kind> · attitude <±N> · news <kind> [what] (it and its heart's people)
        //   a person in need (NpcRescue): giverequest · givefood · givemead · givebackitem · goaway · rescuewait · rescuehide · rescuefollow
        //   one of yours: healmead (a healing mead, hurt on its feet) · letrest (put its work down and rest, when tired) · walkwith (with you a few minutes)
        private static void Do(string act, Player p)
        {
            string[] w = act.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return;
            NpcActor a = With;
            try
            {
                switch (w[0].ToLowerInvariant())
                {
                    case "hire": a.Act("hire " + p.GetPlayerID()); if (a.Zdo != null) NpcRescue.Forget(a.Zdo); break;
                    case "giverequest": NpcRequests.Give(p, a); break;
                    case "givefood": { ItemDrop.ItemData f = NpcMeals.Plainest(p.GetInventory()); if (f != null) Feed(a, p, f); break; }
                    case "givemead": { ItemDrop.ItemData m = NpcVitals.Mead(p); if (m != null) p.GetInventory().RemoveItem(m, 1); break; }
                    case "healmead": { ItemDrop.ItemData m = NpcVitals.Mead(p); if (m == null) break; a.Pay(p, m, "dosed"); break; } // (one hurt on its feet drinks it: half better, where it is run)
                    case "walkwith": a.Act("walkwith " + NpcKit.Ref(p.GetZDOID()) + " " + WalkWithFor.ToString("0", System.Globalization.CultureInfo.InvariantCulture)); break; // (it walks with you a few minutes: WalkWith)
                    case "letrest": a.Act("letrest"); break; // (let off its work to rest now: NpcToil.LetRest — only if it is truly tired)
                    case "givebackitem": { ItemDrop.ItemData it = NpcRescueSite.LostThing(p, a); if (it != null) p.GetInventory().RemoveItem(it); break; }
                    case "unhire": NpcMemory.Tell(a, "fired", p.GetPlayerID(), ""); a.Act("unhire"); break; // (you sent it off: it leaves by itself through the same act, and that is not yours)
                    case "goaway": a.Act("goaway"); break;
                    case "rescuewait": a.Act("rescuemode"); break; // (NpcRescue: waits where it is)
                    case "rescuefollow": a.Act("rescuemode follow " + NpcKit.Ref(p.GetZDOID())); break;
                    case "rescuehide": NpcMenu.PickPlace(Lang.K("Сховок"), Lang.K("на місце, де сховатись"), at => a == null ? null : Utils.DistanceXZ(at, a.transform.position) > HideFar ? Lang.T("Задалеко — покажи ближче.") : SendHide(a, at)); break; // (one in need: where it hides — NpcRescue)
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
                    default:
                        Action<NpcActor, Player, string[]> own;
                        if (Deeds.TryGetValue(w[0].ToLowerInvariant(), out own)) own(a, p, w);
                        else Npcs.Log("talk: unknown deed «" + act + "»");
                        break;
                }
            }
            catch (Exception e) { Npcs.Log("talk deed «" + act + "»: " + e.Message); }
        }

        // a bite from your hands (its worry, one in need, a gift of food): it eats it where it is run, one out of your bag
        private static void Feed(NpcActor a, Player p, ItemDrop.ItemData f)
        {
            a.Pay(p, f, "eat " + NpcMeals.Fill(f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " " + ItemWord(f).ToLowerInvariant()); // (the bite with its act: not eaten where it is run, it is yours again)
        }

        // one in need told to hide where you showed it (NpcRescue): not further than this from where it is
        private const float HideFar = 60f;
        private static string SendHide(NpcActor a, Vector3 at)
        {
            a.Act("rescuemode hide " + Npcs.V(at));
            return a.Name + ": " + Lang.T("іде ховатися");
        }

        private static void Take(Player p, string prefab, int n) { ItemMarks.Take(p, prefab, n); } // (the story takes it: no loss to undo — ItemMarks.Take)

        private static void Give(Player p, string prefab, int n)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (go == null) return;
            if (!p.GetInventory().AddItem(go, n)) { ItemDrop d = go.GetComponent<ItemDrop>(); if (d != null) ItemDrop.DropItem(d.m_itemData.Clone(), n, p.transform.position + p.transform.forward + Vector3.up, Quaternion.identity); }
        }
    }
}
#endif
