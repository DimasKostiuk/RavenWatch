#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Вдача»: a person's card (the round menu's button; a row of a heart's board). A sheet (RavenBook.ShowSheet), the user's picture 2026-11-06, docs/plans/mockups/npc-card-2026-11-06.png:
    //   on top      who it is in a line or two (winters, body, gait; work, home), what it does now with its feeling and a wound as pills; one living at a base: hunger, rest, mood on three cards (why under the
    //               pointer: what its mood is made of); for one hired or living at a base how it is with you on a line painted by the brush. Its patience stays in its talk
    //   Думки       what is on its mind (the same weighing as its talk: NpcVoice.Concerns, its own words), its wish, what it remembers of you
    //   Вдача       its ways as words, its knack
    //   Свої        who it gets on with among its people and who not, its own place (only when it has any)
    //   Побут       bed, chest, work, craft, pay, health: red what wants mending, a click shows the thing (only one hired or living at a base)
    //   Історія     how it came and a scrap of before; «Розкажи більше →» the rest
    // Only what it has: a story person has no pay, no wish, no quarrels. Words, no numbers (to tune a template: its file). What it reads belongs to its modules: the card only says it.
    internal static class NpcProfile
    {
        internal static readonly List<System.Func<NpcActor, string>> More = new List<System.Func<NpcActor, string>>(); // a module's own line about what it wants (NpcModules: NpcGoals)
        internal static IEnumerable<NpcMenu.Button> MenuButtons()
        {
            yield return new NpcMenu.Button { Order = 5, Title = Lang.K("Вдача"), Icon = "n-persona", AnyTime = true,
                Desc = a => Lang.T("Яка це людина: як почувається, що думає про тебе, чим живе."),
                Do = (a, p) => { Show(a); return null; } };
        }

        internal static void Show(NpcActor a) { Show(a, null, null); }

        // back: a word and what to go back to (a heart's board)
        internal static void Show(NpcActor a, string backWord, System.Action back)
        {
            if (a == null || a.Def == null) return;
            Player me = Player.m_localPlayer;
            bool hired = NpcTalk.Hired(a), lives = NpcResident.Is(a);
            RavenBook.Sheet s = new RavenBook.Sheet { Title = a.Name, Who = Who(a), Now = a.StatusText ?? "", Stay = () => a != null && a.Zdo != null, BackWord = backWord ?? "", Back = back };
            Marks(a, s.Marks);
            if (lives) Cards(a, s.Cards);
            if (me != null && (hired || lives))
            {
                string level = NpcAttitude.Level(a, me);
                s.Above.Add(RavenBook.SheetRow.Attitude(NpcAttitude.Word(level), NpcAttitude.Rank(level), (NpcAttitude.Of(a, me.GetPlayerID()) + 100f) / 200f, Lang.T("ЧОМУ · ДО ТЕБЕ"), AttitudeTip(a, level)));
            }
            List<string> worries = null; // (made once a card: clicking back to this tab burns no new words of its own — NpcPhrases keeps what it said lately)
            s.Tabs.Add(new RavenBook.SheetTab { Title = Lang.T("ДУМКИ"), Rows = () => Thoughts(a, worries ?? (worries = Worries(a, me))) });
            s.Tabs.Add(new RavenBook.SheetTab { Title = Lang.T("ВДАЧА"), Rows = () => Ways(a) });
            if (HasOwn(a)) s.Tabs.Add(new RavenBook.SheetTab { Title = Lang.T("СВОЇ"), Rows = () => Own(a, s) });
            if (hired || lives) s.Tabs.Add(new RavenBook.SheetTab { Title = Lang.T("ПОБУТ"), Rows = () => Daily(a) });
            if (NpcBackstory.Has(a)) s.Tabs.Add(new RavenBook.SheetTab { Title = Lang.T("ІСТОРІЯ"), Rows = () => Past(a) });
            RavenBook.ShowSheet(s);
        }

        // ---------------------------------------------------------------- on top
        private static string Who(NpcActor a)
        {
            NpcPersona w = NpcPersona.Of(a);
            NpcSettlers.Look own = NpcSettlers.Of(a);
            string first = Dots(own != null ? Lang.N(NpcSettlers.Winters(a, own.Age), "зима|зими|зим") : "", NpcPersona.BodyWord(w), NpcGait.Word(a));
            List<string> second = new List<string>();
            if (a.Worker != null && a.Worker.Works) second.Add(Jobs.Word(a.Worker.JobId));
            NpcHeart h = NpcResident.HeartOf(a);
            if (h != null) second.Add(h.Title); else if (NpcResident.Is(a)) second.Add(Lang.T("живе на базі")); else if (NpcTalk.Hired(a)) second.Add(Lang.T("найнятий|найнята, ще без дому"));
            return a.Form(first + (second.Count > 0 ? "\n" + string.Join(" · ", second.ToArray()) : ""));
        }

        private static string Dots(params string[] parts)
        {
            List<string> l = new List<string>();
            foreach (string s in parts) if (!string.IsNullOrEmpty(s)) l.Add(s);
            return string.Join(" · ", l.ToArray());
        }

        // the pills under what it does: only what it has
        private static void Marks(NpcActor a, List<RavenBook.SheetMark> l)
        {
            if (NpcVitals.IsDown(a)) l.Add(new RavenBook.SheetMark { Text = Lang.T("без тями"), Tone = RavenBook.ToneBad });
            string feel; float strength;
            if (NpcFeel.Of(a, out feel, out strength)) l.Add(new RavenBook.SheetMark { Text = a.Form(feel == NpcFeel.Fright ? Lang.T("наляканий|налякана") : feel == NpcFeel.Joy ? Lang.T("радіє") : feel == NpcFeel.Grief ? Lang.T("сумує") : Lang.T("вражений|вражена")), Tone = feel == NpcFeel.Joy ? RavenBook.ToneGood : RavenBook.ToneWarn });
            string hurt = HealthWord(a);
            if (hurt != null && !NpcVitals.IsDown(a)) l.Add(new RavenBook.SheetMark { Text = hurt, Tone = RavenBook.ToneBad });
        }

        // hunger, rest, mood: only of one living at a base (nobody else's are counted)
        private static void Cards(NpcActor a, List<RavenBook.SheetMark> l)
        {
            l.Add(new RavenBook.SheetMark { Label = Lang.T("голод"), Text = NpcHunger.Word(a), Tone = NpcHunger.Tone(a) });
            int tone; string rest = NpcToil.RestWord(a, out tone);
            l.Add(new RavenBook.SheetMark { Label = Lang.T("втома"), Text = rest, Tone = tone });
            float mood = NpcToil.Mood(a);
            string why = NpcToil.MoodTip(a);
            l.Add(new RavenBook.SheetMark { Label = Lang.T("настрій"), Text = NpcToil.MoodWord(a), Tone = mood < NpcToil.MoodPoor ? RavenBook.ToneBad : mood < 45f ? RavenBook.ToneWarn : RavenBook.ToneGood, TipHead = Lang.T("ЧОМУ · НАСТРІЙ"), Tip = why.Length > 0 ? why : null });
        }

        // a wound in a word (null: whole)
        private static string HealthWord(NpcActor a)
        {
            Character c = a.GetComponent<Character>();
            float hp = c != null ? c.GetHealthPercentage() : 1f;
            return a.Form(hp >= 0.95f ? null : hp >= 0.5f ? Lang.T("трохи поранений|поранена") : Lang.T("тяжко поранений|поранена"));
        }

        private static string AttitudeTip(NpcActor a, string level)
        {
            string then = level == "close" ? Lang.T("Близькій людині можна довірити прохання й таємниці.") : level == "warm" ? Lang.T("Довіряє тобі: говорить і про своє.")
                : level == "cold" ? Lang.T("Не довіряє: про своє не говоритиме.") : level == "hostile" ? Lang.T("Ворогує: на добре слово не чекай.") : Lang.T("Поки просто знає тебе. Що зробиш для нього|неї, те й запам'ятає.");
            return a.Form(Lang.T("Одне з п'яти: ворожо · холодно · рівно · тепло · близько.") + "\n" + then);
        }

        // ---------------------------------------------------------------- «Думки»
        private const int MemoriesMost = 5, WorriesMost = 2;

        private static List<RavenBook.SheetRow> Thoughts(NpcActor a, List<string> mind)
        {
            List<RavenBook.SheetRow> l = new List<RavenBook.SheetRow>();
            Player me = Player.m_localPlayer;
            List<string> wishes = new List<string>();
            foreach (System.Func<NpcActor, string> m in More) { string w = m(a); if (!string.IsNullOrEmpty(w)) wishes.Add(a.Form(w)); } // (NpcGoals: its wish)
            if (mind.Count > 0 || wishes.Count > 0) l.Add(RavenBook.SheetRow.Label(Lang.T("на думці")));
            foreach (string w in mind) l.Add(RavenBook.SheetRow.Mind(w));
            foreach (string w in wishes) l.Add(RavenBook.SheetRow.Pair(Lang.T("мрія"), w, RavenBook.ToneNone, null, null, NpcGoals.CardSince(a)));
            List<NpcMemory.Line> mem = NpcMemory.OfYou(a, me);
            if (mem.Count > 0) l.Add(RavenBook.SheetRow.Label(Lang.T("пам'ятає про тебе")));
            for (int i = 0; i < mem.Count && i < MemoriesMost; i++) l.Add(RavenBook.SheetRow.Line(mem[i].Text, !mem[i].Against, NpcMemory.Ago(mem[i].At)));
            if (l.Count == 0) l.Add(RavenBook.SheetRow.Plain(a.Form(Lang.T("Поки нічого не каже й нічого не пам'ятає про тебе."))));
            return l;
        }

        // the worries it has for its talk with you, in its own words (what is on its mind is not told to one it does not trust: NpcVoice.Concerns); its wish stands apart
        private static List<string> Worries(NpcActor a, Player me)
        {
            List<string> l = new List<string>();
            if (me == null) return l;
            foreach (NpcVoice.Concern c in NpcVoice.Concerns(a, me))
            {
                if (l.Count >= WorriesMost) break;
                if (c.Theme.StartsWith("goal_", System.StringComparison.Ordinal)) continue;
                string said = NpcPhrases.Compose(a, c.Theme, me);
                if (!string.IsNullOrEmpty(said)) l.Add("«" + said + "»");
            }
            return l;
        }

        // ---------------------------------------------------------------- «Вдача»
        private static List<RavenBook.SheetRow> Ways(NpcActor a)
        {
            NpcPersona w = NpcPersona.Of(a);
            List<RavenBook.SheetMark> chips = new List<RavenBook.SheetMark>();
            System.Action<string, string> add = (word, tip) => { if (!string.IsNullOrEmpty(word)) chips.Add(new RavenBook.SheetMark { Text = a.Form(Lang.T(word)), Tip = tip != null ? a.Form(tip) : null }); };
            if (w.Nature != null && !w.Nature.Plain) add(w.Nature.Word, Lang.T(w.Nature.Tip)); // (its nature first: NpcNature)
            add(Band(w.Chatty, 0.35f, 0.65f, Lang.K("мовчазний|мовчазна"), "", Lang.K("балакучий|балакуча")), null);
            add(Band(w.Bravery, 0.35f, 0.65f, Lang.K("боязкий|боязка"), "", Lang.K("сміливий|смілива")), null);
            add(Band(w.Diligence, 0.35f, 0.65f, Lang.K("неквапливий|некваплива"), "", Lang.K("старанний|старанна")), null);
            add(Band(w.Laziness, 0.25f, 0.6f, Lang.K("невтомний|невтомна"), "", Lang.K("лінькуватий|лінькувата")), null);
            add(Band(w.Curiosity, 0.35f, 0.65f, Lang.K("сам|сама у собі"), "", Lang.K("усе помічає")), null);
            float up, bed;
            NpcResident.DayOf(a, out up, out bed);
            add(w.Chrono < -0.7f ? Lang.K("жайворонок") : w.Chrono > 0.7f ? Lang.K("сова") : Lang.K("звичайний день"), Lang.F("Встає о {0}, лягає о {1}.", Clock(up), Clock(bed)));
            add(Fight(a), null);
            string knack = NpcKnack.Describe(a);
            chips.Add(new RavenBook.SheetMark { Text = Capital(a.Form(knack)), Hot = true });
            return new List<RavenBook.SheetRow> { RavenBook.SheetRow.Chips(chips) };
        }

        // ---------------------------------------------------------------- «Свої»: who it gets on with among those of its home, and its own place
        private static List<NpcAttitude.Tie> TiesOf(NpcActor a) { return NpcAttitude.Ties(NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart))).FindAll(t => t.A == a || t.B == a); }

        private static bool HasOwn(NpcActor a)
        {
            Vector3 place;
            if (NpcPlaces.Favourite(a, out place)) return true;
            return TiesOf(a).Count > 0;
        }

        private static List<RavenBook.SheetRow> Own(NpcActor a, RavenBook.Sheet from)
        {
            List<RavenBook.SheetRow> own = new List<RavenBook.SheetRow>();
            foreach (NpcAttitude.Tie t in TiesOf(a))
            {
                NpcActor o = t.A == a ? t.B : t.A;
                System.Action open = () => { if (o != null) Show(o, a.Name, () => Show(a, from.BackWord, from.Back)); };
                own.Add(t.Foes ? RavenBook.SheetRow.Pair(Lang.T("не терпить"), o.Name + (t.Why.Length > 0 ? " — " + t.Why : ""), RavenBook.ToneBad, null, open)
                    : RavenBook.SheetRow.Pair(t.Close ? Lang.T("близькі") : Lang.T("дружать"), o.Name, RavenBook.ToneGood, null, open));
            }
            Vector3 place;
            if (NpcPlaces.Favourite(a, out place)) { Vector3 at = place; own.Add(RavenBook.SheetRow.Pair(Lang.T("своє місце"), Lang.T("показати"), RavenBook.ToneNone, null, () => { RavenBook.HideSheet(); NpcMenu.ShowPlace(at, null, a.Form(Lang.F("Місце, де любить бувати {0}", a.Name))); })); }
            return own;
        }

        private static string Band(float v, float lo, float hi, string low, string mid, string high) { return v < lo ? low : v > hi ? high : mid; }
        private static string Capital(string s) { return string.IsNullOrEmpty(s) ? s : s.Substring(0, 1).ToUpperInvariant() + s.Substring(1); }
        private static string Clock(float h) { h = Mathf.Repeat(h, 24f); int m = Mathf.RoundToInt(h * 60f) % 1440; return (m / 60) + ":" + (m % 60).ToString("00"); }

        private static string Fight(NpcActor a)
        {
            switch (a.Mind != null ? a.Mind.Style : NpcMind.Fight.Flee)
            {
                case NpcMind.Fight.Flee: return Lang.K("у бою тікає");
                case NpcMind.Fight.Wary: return Lang.K("у бою відбивається, загнаний|загнана");
                case NpcMind.Fight.Defend: return Lang.K("у бою захищає своїх");
                case NpcMind.Fight.Hunt: return Lang.K("сам|сама шукає ворогів");
                default: return Lang.K("осторонь бою");
            }
        }

        // ---------------------------------------------------------------- «Побут»
        private static List<RavenBook.SheetRow> Daily(NpcActor a)
        {
            List<RavenBook.SheetRow> l = new List<RavenBook.SheetRow>();
            NpcWorker wk = a.Worker;
            bool works = wk != null && wk.Works;
            l.Add(RavenBook.SheetRow.Pair(Lang.T("робота"), works ? Jobs.Word(wk.JobId) : Lang.T("без роботи"), works ? RavenBook.ToneNone : RavenBook.ToneWarn));
            if (works && a.Def != null && a.Def.Generated) l.Add(RavenBook.SheetRow.Pair(Lang.T("ремесло"), a.Form(NpcToil.CraftWord(a))));
            if (NpcResident.Is(a))
            {
                bool bed = NpcLocker.HasRef(a.Zdo, NpcResident.KBed);
                l.Add(RavenBook.SheetRow.Pair(Lang.T("ліжко"), bed ? Lang.T("є") : Lang.T("нема"), bed ? RavenBook.ToneGood : RavenBook.ToneBad, bed ? null : Lang.T("Збудуй ліжко й дай його: Q → Оселя → Ліжко."), bed ? Point(NpcKit.WhereIs(NpcLocker.Ref(a.Zdo, NpcResident.KBed)), a.Form(Lang.F("Ліжко: {0}", a.Name))) : null));
            }
            if (wk != null && !wk.ChestId.IsNone()) l.Add(RavenBook.SheetRow.Pair(wk.JobId == "cook" ? Lang.T("комора") : Lang.T("ящик"), wk.JobId == "cook" ? Lang.T("тримає") : Lang.T("є"), RavenBook.ToneGood, null, Point(NpcKit.WhereIs(wk.ChestId), a.Form(wk.JobId == "cook" ? Lang.T("Комора") : Lang.F("Ящик: {0}", a.Name)))));
            if (works)
            {
                int unpaid = NpcToil.Unpaid(a);
                l.Add(RavenBook.SheetRow.Pair(Lang.T("платня"), unpaid < 0 ? Lang.T("ще не отримував|отримувала") : unpaid == 0 ? Lang.T("платять вчасно") : Lang.T("не заплатили"), unpaid > 0 ? RavenBook.ToneBad : unpaid == 0 ? RavenBook.ToneGood : RavenBook.ToneNone, Lang.F("{0} раз на {1}.", Lang.N(NpcToil.Wage(a), "монета|монети|монет"), Lang.N(Mathf.RoundToInt(NpcToil.PayDays), "день|дні|днів"))));
            }
            string hurt = HealthWord(a);
            if (NpcTalk.Hired(a)) l.Add(RavenBook.SheetRow.Pair(Lang.T("здоров'я"), hurt ?? a.Form(Lang.T("цілий|ціла")), hurt != null ? RavenBook.ToneBad : RavenBook.ToneGood));
            foreach (RavenBook.SheetRow r in l) r.Right = a.Form(r.Right);
            return l;
        }

        private static System.Action Point(Vector3? at, string what)
        {
            if (!at.HasValue) return null;
            return () => { RavenBook.HideSheet(); NpcMenu.ShowPlace(at.Value, null, what); };
        }

        // ---------------------------------------------------------------- «Історія»
        private static List<RavenBook.SheetRow> Past(NpcActor a)
        {
            string shortOne = NpcBackstory.Text(a, false), rest = NpcBackstory.Rest(a);
            return new List<RavenBook.SheetRow> { RavenBook.SheetRow.Fold(shortOne, Lang.T("Розкажи більше"), rest) };
        }
    }
}
#endif
