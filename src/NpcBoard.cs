#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A heart's board (NpcHeart, E; a resident's ring «Оселя» → «Дошка оселі»): how this settlement lives, the user's design 2026-11-06 (RavenBook.ShowBoard draws it). Nothing is kept here: it says what its modules know.
    //   Look        what is wrong now, group by group (Trouble: red it leaves or drops its work · amber not happy · grey could be better), the pantry against who eats, the coin box against the pays. One
    //               look for all who ask: the notes on the board in the world, the hover, the window. Made at most once in Every s a heart, only while you are near it. No ways asked of the path finder,
    //               nothing searched in the world: NpcMeals.Supply, NpcBase.Things, lockers
    //   on the left Треба зробити (the first TodoMost rows, «і ще N»; an arrow shows the thing, all of a group, on the screen and the map: NpcMenu.ShowPlaces); Комора і скарбничка.
    //               A person in need (NpcRescue) heads it in a group of its own, «поклик» (the user 2026-11-15): a click marks it on your map till you find it and opens the map there
    //   tabs        Мешканці (a row a person, a click its card, «←» back here; who is far: NpcHeart.FarOf) · Твої справи (the story's stage when it is about this place, your words, requests, wishes) ·
    //               Життя оселі (who gets on with whom: NpcAttitude.Ties — two at odds: whom to speak to, a click shows them; what happened: NpcMemory.HomeEvents) · Місця (only to read and show). A page's rows are made when it is opened
    //   the buttons the dining table, its edges, its name, a feast (NpcNeighbours.Feast); an ultimatum in «Треба зробити» (NpcNeighbours)
    internal static class NpcBoard
    {
        internal const float Every = 5f;
        private const int TodoMost = 10, TiesMost = 8, EventsMost = 7, PlacesMost = 10;
        private static readonly int PerfLook = NpcPerf.Id("heart: look (дошка)");

        internal sealed class Trouble { public int Tone; public string Text = "", Sub = "", Far = "", Tip; public Action Go; public NpcRescue.Short Call; } // (Call: a person in need — its own group)

        internal sealed class Look
        {
            public List<NpcActor> Who = new List<NpcActor>();
            public List<Trouble> Troubles = new List<Trouble>();
            public int Asks, Coins, Beds, HasBed, Burning, Fires;
            public bool BaseReady;
            public float Days;
            public NpcToil.PayLook Pay;
            public float Mood; // (the average: its word)
            public int Worst { get { int w = RavenBook.ToneNone; foreach (Trouble t in Troubles) if (t.Tone > w) w = t.Tone; return w; } }
            public int Notes { get { return Troubles.Count + Asks; } }
        }

        // window: the board itself is open (an empty heart's base is looked over only then: not for walking past it)
        internal static Look Make(NpcHeart h, bool window = false)
        {
            long perf = NpcPerf.Now();
            Look l = new Look { Who = NpcHeart.Residents(h.Id), Coins = h.Coins };
            float mood = 0f;
            foreach (NpcActor a in l.Who) { mood += NpcToil.Mood(a); if (NpcRequests.Of(a) != null) l.Asks++; if (NpcLocker.HasRef(a.Zdo, NpcResident.KBed)) l.HasBed++; }
            l.Mood = l.Who.Count > 0 ? mood / l.Who.Count : 60f;
            NpcBase b = l.Who.Count > 0 || window ? NpcBase.For(h.transform.position) : null;
            l.BaseReady = b != null && b.Ready;
            if (l.BaseReady)
            {
                if (l.Who.Count > 0) l.Days = NpcMeals.DaysOf(NpcMeals.Supply(b), l.Who);
                List<Component> beds = b.Things(typeof(Bed)), fires = b.Things(typeof(Fireplace));
                l.Beds = beds != null ? beds.Count : 0;
                if (fires != null) foreach (Component c in fires) { Fireplace f = c as Fireplace; if (f == null) continue; l.Fires++; if (f.IsBurning()) l.Burning++; } // (only those that stand: one gone this very frame is not a fire that went out)
            }
            l.Pay = NpcToil.PayAhead(l.Who, l.Coins);
            Troubles(h, l, b);
            NpcPerf.Add(PerfLook, perf);
            return l;
        }

        private static string Names(List<NpcActor> who) { List<string> n = new List<string>(); foreach (NpcActor a in who) n.Add(a.Name); return string.Join(", ", n.ToArray()); }

        private static Action ToPeople(List<NpcActor> who)
        {
            List<NpcActor> all = new List<NpcActor>(who);
            return () => { RavenBook.HideSheet(); List<NpcMenu.Point> l = new List<NpcMenu.Point>(); foreach (NpcActor a in all) if (a != null) l.Add(new NpcMenu.Point { At = a.transform.position, On = a.transform, What = a.Name }); NpcMenu.ShowPlaces(l); };
        }
        private static Action ToSpot(Vector3 at, string what) { return () => { RavenBook.HideSheet(); NpcMenu.ShowPlace(at, null, what); }; }
        private static Action ToChest(Container c, string what) { return c != null ? ToSpot(c.transform.position, what) : null; }
        private static Action ToCall(NpcRescue.Short s) { return () => { RavenBook.HideSheet(); NpcRescue.Mark(s); NpcMap.RescueNow(); if (Minimap.instance != null) Minimap.instance.ShowPointOnMap(s.At); }; }
        private static string CallWord(int seen) { return seen == 2 ? Lang.T("знайдено: тепер допоможи") : seen == 1 ? Lang.T("позначено на мапі, доки не знайдеш") : Lang.T("клік: позначити на мапі"); }

        // a group of people of one trouble: one line, a click shows them all
        private static void Group(Look l, int tone, List<NpcActor> who, string text, string sub, string tip)
        {
            if (who.Count == 0) return;
            l.Troubles.Add(new Trouble { Tone = tone, Text = Lang.F(text, Names(who)), Sub = sub, Tip = tip, Go = ToPeople(who) });
        }

        // the base's first fire that stands (one taken down this very frame is still in its list: NpcBase.Live takes it out a frame later); null — none
        private static Component FirstFire(NpcBase b)
        {
            List<Component> fires = b != null ? b.Things(typeof(Fireplace)) : null;
            if (fires != null) foreach (Component c in fires) if (c != null) return c;
            return null;
        }

        private static void Troubles(NpcHeart h, Look l, NpcBase b)
        {
            List<NpcActor> starving = new List<NpcActor>(), down = new List<NpcActor>(), edge = new List<NpcActor>(), hungry = new List<NpcActor>(), poor = new List<NpcActor>(), noBed = new List<NpcActor>(), full = new List<NpcActor>(), unpaid = new List<NpcActor>(), idle = new List<NpcActor>(), lacking = new List<NpcActor>();
            List<string> poorWhy = new List<string>(), lacks = new List<string>();
            foreach (NpcActor a in l.Who)
            {
                if (NpcHunger.IsStarving(a)) starving.Add(a); else if (NpcHunger.IsHungry(a)) hungry.Add(a);
                if (NpcVitals.IsDown(a)) down.Add(a);
                float mood = NpcToil.Mood(a);
                if (mood < NpcToil.MoodLow) edge.Add(a);
                else if (mood < NpcToil.MoodPoor) { poor.Add(a); string why = NpcToil.WhyWord(a); poorWhy.Add(a.Name + (why.Length > 0 ? ": " + a.Form(why) : "")); }
                if (!NpcLocker.HasRef(a.Zdo, NpcResident.KBed)) noBed.Add(a);
                if (a.StatusNow.Contains("ящик повний")) full.Add(a); // (its status stays Ukrainian where it is kept)
                if (a.Worker == null || !a.Worker.Works) idle.Add(a);
                else if (NpcToil.Unpaid(a) > 0) unpaid.Add(a);
                List<string> lack = NpcWorker.Lacks(a); // (what its work wants: no chest, a full one, its job's own — a tool, a plot, fuel, a kitchen's tools and fires. One answer with its card: NpcWorker.Lacks)
                if (lack != null) { lacking.Add(a); lacks.Add(a.Name + ": " + string.Join("; ", lack.ToArray())); }
            }
            Group(l, RavenBook.ToneBad, starving, Lang.K("Голодують: {0}"), Lang.T("без їжі кинуть роботу, а згодом підуть"), null);
            Group(l, RavenBook.ToneBad, down, Lang.K("Без тями: {0}"), Lang.T("постав на ноги медовухою зцілення"), null);
            Group(l, RavenBook.ToneBad, edge, Lang.K("На межі: {0}"), Lang.T("якщо нічого не зміниться, підуть"), null);
            foreach (NpcActor a in l.Who) // (one hostile to one of its home: «або я, або …» — NpcNeighbours, its days running out)
            {
                NpcNeighbours.Kept k = NpcNeighbours.Of(a);
                if (k == null || k.Ult == 0L) continue;
                List<NpcActor> pair = new List<NpcActor> { a };
                foreach (NpcActor o in l.Who) if (NpcAttitude.PersonKey(o) == k.Ult) pair.Add(o);
                l.Troubles.Add(new Trouble { Tone = RavenBook.ToneBad, Text = Lang.F("{0}: «Або я, або {1}»", a.Name, NpcNeighbours.NameOf(a, k.Ult)), Far = NpcRescue.Left((long)NpcNeighbours.UltimatumLeft(k)), Go = ToPeople(pair),
                    Tip = Lang.T("Ця людина не терпить сусіда й поставила умову. Якщо до строку нічого не зміниться, вона піде з оселі.\nЩо можна зробити: поговорити з нею й сказати «Помиріться вже»; розвести їх («Тримайтеся одне від одного подалі»: це лише дає час); відпустити чи переселити когось із двох.") });
            }
            NpcRescue.Short rumour = NpcRescue.BoardShort(h.Zdo, h.transform.position);
            if (rumour != null) l.Troubles.Add(new Trouble { Tone = RavenBook.ToneWarn, Text = rumour.Text, Sub = CallWord(rumour.Seen), Far = rumour.Left, Call = rumour, Go = rumour.Seen == 2 ? null : ToCall(rumour) }); // (found: no mark to put or show any more)
            Player me = Player.m_localPlayer;
            float day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f;
            foreach (NpcActor a in l.Who)
                foreach (KeyValuePair<string, long> w in NpcPromises.Yours(a, me))
                    if (w.Value < day) l.Troubles.Add(new Trouble { Tone = RavenBook.ToneWarn, Text = Lang.F("Обіцянка: {0}, {1}", a.Name, w.Key), Far = NpcRescue.Left(w.Value), Go = ToPeople(new List<NpcActor> { a }) });
            if (l.Who.Count > 0 && !h.DiningId.IsNone() && h.Dining == null) l.Troubles.Add(new Trouble { Tone = RavenBook.ToneWarn, Text = Lang.T("Нема столу їдальні: обери інший") });
            bool cook = l.Who.Exists(a => a.Worker != null && a.Worker.JobId == "cook");
            if (l.Who.Count > 0 && l.BaseReady && l.Days <= 0f) l.Troubles.Add(new Trouble { Tone = RavenBook.ToneWarn, Text = Lang.T("Нема що їсти"), Sub = NpcMeals.Why(b), Go = h.Dining != null ? ToSpot(h.Dining.position, Lang.T("Їдальня")) : null });
            else if (l.Who.Count > 0 && l.BaseReady && l.Days < 1f) l.Troubles.Add(new Trouble { Tone = RavenBook.ToneWarn, Text = Lang.T("Їжі менше ніж на день") });
            if (l.Who.Count > 0 && l.BaseReady && l.Days < 1f && !cook) l.Troubles.Add(new Trouble { Tone = RavenBook.ToneWarn, Text = Lang.T("Бракує кухаря") });
            Group(l, RavenBook.ToneWarn, hungry, Lang.K("Голодні: {0}"), "", null);
            Group(l, RavenBook.ToneWarn, unpaid, Lang.K("Без платні: {0}"), "", Lang.T("Поклади монети в скарбничку."));
            if (unpaid.Count > 0) l.Troubles[l.Troubles.Count - 1].Go = ToChest(h.Box, Lang.T("Скарбничка"));
            Group(l, RavenBook.ToneWarn, poor, Lang.K("Невдоволені: {0}"), "", poorWhy.Count > 0 ? string.Join("\n", poorWhy.ToArray()) : null);
            Group(l, RavenBook.ToneWarn, lacking, Lang.K("Нема чим працювати: {0}"), "", lacks.Count > 0 ? string.Join("\n", lacks.ToArray()) : null);
            Group(l, RavenBook.ToneNone, noBed, Lang.K("Без ліжка: {0}"), "", Lang.T("Збудуй ліжко й дай його: Q на людині → Оселя → Ліжко."));
            foreach (NpcActor a in full) l.Troubles.Add(new Trouble { Tone = RavenBook.ToneNone, Text = Lang.F("Ящик повний: {0}", a.Name), Go = a.Worker != null ? ToChest(a.Worker.Chest, Lang.F("Ящик: {0}", a.Name)) : null });
            Group(l, RavenBook.ToneNone, idle, Lang.K("Без роботи: {0}"), "", Lang.T("Дай роботу: Q на людині → Робота."));
            if (l.BaseReady && l.Fires > 0 && l.Burning == 0 && l.Who.Count > 0)
            {
                Component first = FirstFire(b);
                l.Troubles.Add(new Trouble { Tone = RavenBook.ToneNone, Text = Lang.T("Згасли всі вогнища"), Go = first != null ? ToSpot(first.transform.position, Lang.T("Вогнище")) : null });
            }
            if (l.Who.Count >= NpcResident.PerBase) l.Troubles.Add(new Trouble { Tone = RavenBook.ToneNone, Text = Lang.T("Місця більше нема") });
        }

        // ---------------------------------------------------------------- how they live, in a word
        private static int StateRank(Look l)
        {
            int worst = l.Worst;
            return worst == RavenBook.ToneBad || l.Mood < NpcToil.MoodPoor ? 3 : worst == RavenBook.ToneWarn || l.Mood < 45f ? 2 : l.Mood >= 60f ? 1 : 0;
        }

        internal static string StateWord(Look l)
        {
            if (l.Who.Count == 0) return Lang.T("щойно збудована");
            switch (StateRank(l)) { case 3: return Lang.T("на межі"); case 2: return Lang.T("нарікають"); case 1: return Lang.T("живуть добре"); default: return Lang.T("живуть спокійно"); }
        }

        private static int StateTone(Look l)
        {
            if (l.Who.Count == 0) return RavenBook.BoardGreyDot;
            int rank = StateRank(l);
            return rank == 3 ? RavenBook.ToneBad : rank == 2 ? RavenBook.ToneWarn : RavenBook.ToneGood;
        }

        private static string StateTip(Look l)
        {
            if (l.Who.Count == 0) return Lang.T("Ще ніхто тут не живе.");
            List<string> why = new List<string>();
            foreach (Trouble t in l.Troubles) if (t.Tone >= RavenBook.ToneWarn && why.Count < 4) why.Add("− " + t.Text);
            if (why.Count == 0 && l.HasBed == l.Who.Count && !l.Who.Exists(a => NpcHunger.IsHungry(a))) why.Add("+ " + Lang.T("усі ситі й мають ліжко"));
            if (l.Burning > 0) why.Add("+ " + Lang.T("вогнища горять"));
            if (l.Pay.Workers > 0 && l.Pay.Short == 0) why.Add("+ " + Lang.T("на платню вистачає"));
            return string.Join("\n", why.ToArray());
        }

        private static string DaysWord(float days) { return days < 1f ? Lang.T("менше ніж на день") : Lang.F("на {0}", Lang.N(Mathf.FloorToInt(days), "день|дні|днів")); }
        private static RavenBook.BoardRow Row(string label, string text, int dot = RavenBook.BoardNoDot, string far = "") { return new RavenBook.BoardRow { Label = label, Text = text, Dot = dot, Far = far }; }

        // ---------------------------------------------------------------- the window
        internal static void Show(NpcHeart h, int tab = 0)
        {
            if (h == null) return;
            Look l = Make(h, true);
            RavenBook.Board b = new RavenBook.Board { Title = h.Title, Open = tab, Stay = () => h != null };
            b.CountNow = () => Lang.F("{0} із {1} мешканців", NpcHeart.HowMany(h.Id, null), NpcResident.PerBase);
            b.Mood = StateWord(l); b.MoodTone = StateTone(l); b.MoodTip = StateTip(l);
            b.Notes.Add(Todo(l));
            if (l.Who.Count > 0) b.Notes.Add(Store(l, h));
            b.Dining = h.DiningState();
            b.Choose = () => { RavenBook.HideSheet(); h.PickDining(); };
            b.Remove = () => { RavenBook.HideSheet(); h.ClearDining(); };
            Transform table = h.Dining;
            if (table != null) b.ShowTable = ToSpot(table.position, Lang.T("Їдальня"));
            NpcBase nb = NpcBase.For(h.transform.position);
            b.Edges = () => { RavenBook.HideSheet(); ShowEdges(nb); };
            b.Rename = () => { RavenBook.HideSheet(); h.AskName(); };
            if (l.Who.Count >= 2) // (a feast for its people: NpcNeighbours — why not now, under the pointer)
            {
                int need;
                string no = NpcNeighbours.FeastWhyNot(h, Player.m_localPlayer, out need);
                b.FeastTip = Lang.F("Пригости всю оселю: по готовій страві з твоєї торби на кожного ({0}). Усі трохи потеплішають одне до одного, а день буде в доброму настрої. Не частіше ніж раз на три дні.", need) + (no != null ? "\n<color=#8a3416>" + no + "</color>" : "");
                b.Feast = () => { RavenBook.HideSheet(); NpcNeighbours.Feast(h, Player.m_localPlayer); };
            }
            bool people = l.Who.Count > 0 || NpcHeart.FarOf(h.Id, l.Who).Count > 0; // (its people's tabs only with people: none known yet — the server's list may still be on its way)
            if (people)
            {
                b.Tabs.Add(new RavenBook.BoardTab { Title = Lang.T("МЕШКАНЦІ"), Page = () => People(h, b) });
                int deeds = DeedsCount(h, l);
                b.Tabs.Add(new RavenBook.BoardTab { Title = deeds > 0 ? Lang.T("ТВОЇ СПРАВИ") + " · " + deeds : Lang.T("ТВОЇ СПРАВИ"), Page = () => Deeds(h, b) });
                b.Tabs.Add(new RavenBook.BoardTab { Title = Lang.T("ЖИТТЯ ОСЕЛІ"), Page = () => Life(h) });
            }
            b.Tabs.Add(new RavenBook.BoardTab { Title = Lang.T("МІСЦЯ"), Page = () => Places(h, l) });
            RavenBook.ShowBoard(b);
            int farWas = NpcHeart.FarOf(h.Id, l.Who).Count;
            NpcHeart.Ask(list => // (who lives here but is far: the server's list, once a board opened)
            {
                if (h == null) return;
                List<NpcActor> now = NpcHeart.Residents(h.Id);
                bool known = now.Count > 0 || NpcHeart.FarOf(h.Id, now).Count > 0;
                if (known != people) { if (RavenBook.BoardOpen(b)) Show(h, 0); return; } // (opened before its people were known — just after a load: the board anew with their tabs, 2026-11-08 «не завантажились одразу»)
                if (NpcHeart.FarOf(h.Id, now).Count != farWas) RavenBook.BoardRefresh(b);
            });
        }

        private static RavenBook.BoardNote Todo(Look l)
        {
            RavenBook.BoardNote note = new RavenBook.BoardNote { Title = Lang.T("ТРЕБА ЗРОБИТИ") };
            if (l.Who.Count == 0)
            {
                note.Title = Lang.T("МЕШКАНЦІ");
                Calls(l, note); // (a rumour of someone in need: the first heart's first news)
                RavenBook.BoardGroup none = new RavenBook.BoardGroup { Text = Lang.T("Ще ніхто тут не живе. Найми когось і вибери цю оселю або оселі тут того, хто вже з тобою.") };
                foreach (Trouble t in l.Troubles) if (t.Call == null) none.Rows.Add(new RavenBook.BoardRow { Text = t.Text, Far = t.Far, Click = t.Go, Arrow = t.Go != null });
                note.Groups.Add(none);
                return note;
            }
            if (l.Troubles.Count == 0) { note.Groups.Add(new RavenBook.BoardGroup { Text = Lang.T("Поки тихо: усе гаразд.") }); return note; }
            string[] heads = { Lang.T("згодом"), "", Lang.T("важливо"), Lang.T("горить") };
            int calls = Calls(l, note), shown = calls, all = l.Troubles.Count;
            foreach (int level in new[] { RavenBook.ToneBad, RavenBook.ToneWarn, RavenBook.ToneNone })
            {
                RavenBook.BoardGroup g = new RavenBook.BoardGroup { Head = heads[level], Level = level, Diamond = true };
                foreach (Trouble t in l.Troubles)
                {
                    if (t.Tone != level || t.Call != null) continue;
                    if (shown >= TodoMost) continue;
                    g.Rows.Add(new RavenBook.BoardRow { Text = t.Text, Sub = t.Sub, Far = t.Far, Tip = t.Tip, Click = t.Go, Arrow = t.Go != null });
                    shown++;
                }
                if (g.Rows.Count > 0) note.Groups.Add(g);
            }
            if (all > shown)
            {
                List<string> rest = new List<string>(); int i = calls;
                foreach (int level in new[] { RavenBook.ToneBad, RavenBook.ToneWarn, RavenBook.ToneNone }) foreach (Trouble t in l.Troubles) if (t.Tone == level && t.Call == null) { if (i++ >= shown) rest.Add(t.Text); }
                note.Groups[note.Groups.Count - 1].Rows.Add(new RavenBook.BoardRow { Text = Lang.F("і ще {0}", all - shown), Ink = RavenBook.ToneBad, Tip = string.Join("\n", rest.ToArray()) });
            }
            return note;
        }

        // people in need first, in a group of their own (its look: BoardGroup.Call); how many
        private static int Calls(Look l, RavenBook.BoardNote note)
        {
            RavenBook.BoardGroup g = new RavenBook.BoardGroup { Head = Lang.T("поклик"), Call = true };
            foreach (Trouble t in l.Troubles) if (t.Call != null) g.Rows.Add(new RavenBook.BoardRow { Text = t.Text, Sub = t.Sub, Far = t.Far, Click = t.Go });
            if (g.Rows.Count > 0) note.Groups.Add(g);
            return g.Rows.Count;
        }

        private static RavenBook.BoardNote Store(Look l, NpcHeart h)
        {
            RavenBook.BoardNote note = new RavenBook.BoardNote { Title = Lang.T("КОМОРА І СКАРБНИЧКА") };
            RavenBook.BoardGroup g = new RavenBook.BoardGroup();
            if (l.BaseReady)
            {
                RavenBook.BoardRow food = Row(Lang.T("їжа"), l.Days <= 0f ? Lang.T("нема готових страв") : DaysWord(l.Days), l.Days < 1f ? RavenBook.ToneBad : l.Days < 3f ? RavenBook.ToneWarn : RavenBook.ToneGood);
                food.TipHead = Lang.T("ЗАПАС ПРОТИ ТОГО, СКІЛЬКИ З'ЇДАЮТЬ"); food.Tip = Lang.T("Лише готові страви у звичайних скринях оселі: сирого не їдять, особисті скрині не чіпають.");
                g.Rows.Add(food);
            }
            bool cook = l.Who.Exists(a => a.Worker != null && a.Worker.JobId == "cook");
            if (!cook) { RavenBook.BoardRow kitchen = Row(Lang.T("кухня"), Lang.T("бракує кухаря"), RavenBook.ToneWarn); kitchen.Tip = Lang.T("Без кухаря їдять лише те готове, що покладеш сам{|а}."); g.Rows.Add(kitchen); }
            RavenBook.BoardRow coins = Row(Lang.T("монети"), l.Pay.Short > 0 ? Lang.F("{0} із {1}", l.Coins, l.Pay.Sum) : l.Coins.ToString(), l.Pay.Short > 0 ? RavenBook.ToneWarn : l.Pay.Workers > 0 ? RavenBook.ToneGood : RavenBook.BoardNoDot);
            if (l.Pay.Workers > 0) { coins.TipHead = Lang.T("СКАРБНИЧКА"); coins.Tip = Lang.F("За {0} усім разом: {1}. У скарбничці: {2}.", Lang.N(Mathf.RoundToInt(NpcToil.PayDays), "день|дні|днів"), Lang.N(l.Pay.Sum, "монета|монети|монет"), l.Coins); }
            coins.Click = ToChest(h.Box, Lang.T("Скарбничка"));
            g.Rows.Add(coins);
            if (l.Pay.NextIn >= 0L) g.Rows.Add(Row(Lang.T("платня"), Lang.F("за {0}: {1}", NpcRescue.Left(l.Pay.NextIn), l.Pay.Next)));
            if (l.Pay.Short > 0) { RavenBook.BoardRow shortOf = Row(Lang.T("не вистачить"), l.Pay.FirstShort, RavenBook.ToneBad); shortOf.Tip = Lang.F("Бракує {0}. Перша така платня за {1}.", Lang.N(l.Pay.Short, "монети|монет|монет"), NpcRescue.Left(l.Pay.ShortIn)); g.Rows.Add(shortOf); }
            note.Groups.Add(g);
            return note;
        }

        // ---- «Мешканці»
        private static RavenBook.BoardPage People(NpcHeart h, RavenBook.Board b)
        {
            RavenBook.BoardPage page = new RavenBook.BoardPage { People = new List<RavenBook.BoardPerson>() };
            List<NpcActor> who = NpcHeart.Residents(h.Id);
            Player me = Player.m_localPlayer;
            foreach (NpcActor a in who)
            {
                NpcActor person = a;
                string level = me != null ? NpcAttitude.Level(a, me) : "even";
                int rest; string restWord = NpcToil.RestWord(a, out rest);
                float mood = NpcToil.Mood(a);
                bool works = a.Worker != null && a.Worker.Works, down = NpcVitals.IsDown(a);
                string why = NpcToil.MoodTip(a);
                List<NpcMemory.Line> mem = NpcMemory.OfYou(a, me);
                string tip = NpcHunger.Word(a) + " · " + restWord + " · " + NpcToil.MoodWord(a) + (why.Length > 0 ? "\n" + why : "") + (mem.Count > 0 ? "\n" + (mem[0].Against ? "− " : "+ ") + mem[0].Text : "") + "\n<i><color=#8a3416>" + Lang.T("клік: відкрити вдачу") + "</color></i>";
                page.People.Add(new RavenBook.BoardPerson
                {
                    Name = a.Name,
                    Job = (works ? a.Form(Jobs.Word(a.Worker.JobId)) : Lang.T("без роботи")) + (down ? " · " + Lang.T("без тями") : ""),
                    JobBad = !works || down,
                    Food = NpcHunger.Tone(a) == 0 ? RavenBook.ToneGood : NpcHunger.Tone(a), Rest = rest == 0 ? RavenBook.ToneGood : rest,
                    Mood = mood < NpcToil.MoodPoor ? RavenBook.ToneBad : mood < 45f ? RavenBook.ToneWarn : RavenBook.ToneGood,
                    You = NpcAttitude.Word(level), YouRank = NpcAttitude.Rank(level),
                    TipHead = Lang.F("ЧОМУ · {0}", a.Name), Tip = tip,
                    Click = () => { if (person != null) NpcProfile.Show(person, h.Title, () => Show(h, b.Open)); },
                });
            }
            foreach (NpcHeart.Folk f in NpcHeart.FarOf(h.Id, who))
                page.People.Add(new RavenBook.BoardPerson { Name = NpcActor.Shown(f.Name), Job = (f.Job.Length > 0 ? Jobs.Word(f.Job) + " · " : "") + Lang.T("далеко"), Far = true, You = "", Tip = Lang.T("Зараз далеко від оселі: як почувається, стане видно, коли повернеться.") });
            RavenBook.BoardGroup work = new RavenBook.BoardGroup { Head = Lang.T("робота") };
            List<string> nobody = new List<string>();
            foreach (string job in new[] { "cook", "guard" }) if (!who.Exists(a => a.Worker != null && a.Worker.JobId == job)) nobody.Add(Jobs.Word(job));
            List<string> free = new List<string>();
            foreach (NpcActor a in who) if (a.Worker == null || !a.Worker.Works) free.Add(a.Name);
            if (nobody.Count > 0) { RavenBook.BoardRow none = Row(Lang.T("нема"), string.Join(", ", nobody.ToArray()), RavenBook.ToneWarn); none.Tip = Lang.T("Без кухаря їдять лише те готове, що покладеш сам{|а}. Без вартового оселю ніхто не стереже."); work.Rows.Add(none); }
            if (free.Count > 0) work.Rows.Add(Row(Lang.T("без роботи"), string.Join(", ", free.ToArray()), RavenBook.BoardGreyDot));
            if (work.Rows.Count > 0) page.Groups.Add(work);
            return page;
        }

        // ---- «Твої справи»
        private static int DeedsCount(NpcHeart h, Look l)
        {
            int n = 0; Player me = Player.m_localPlayer;
            foreach (NpcActor a in l.Who) { n += NpcPromises.Yours(a, me).Count; NpcRequests.Request r = NpcRequests.Of(a); if (r != null && (me == null || !NpcPromises.Has(a, me.GetPlayerID(), "request"))) n++; if (NpcGoals.ForYou(a)) n++; n += NpcTasks.Yours(a, me).Count; }
            if (StoryCard(h) != null) n++;
            return n;
        }

        // the story's stage when it is about this place: its mark stands within this base (the rest of the story is in the book: this is no second journal)
        private static RavenBook.BoardRow StoryCard(NpcHeart h)
        {
#if RAVEN_STORY
            Player p = Player.m_localPlayer;
            if (p == null) return null;
            foreach (StoryQuests.Standing a in StoryQuests.NowAll(p)) // (the first quest he stands in whose mark is in this base: a quest beside the chapter's may be the one here)
            {
                Vector3 at;
                if (!StoryQuests.MarkNear(a.Quest, a.Stage, h.transform.position, out at) || Utils.DistanceXZ(at, h.transform.position) > NpcBase.Reach) continue; // (of several places, the one by this base)
                string goal = StoryQuests.GoalText(a.Stage);
                if (string.IsNullOrEmpty(goal)) continue;
                return new RavenBook.BoardRow { Text = goal, Arrow = true, Click = ToSpot(at, Lang.T("Сюжет")) };
            }
            return null;
#else
            return null;
#endif
        }

        private static RavenBook.BoardPage Deeds(NpcHeart h, RavenBook.Board b)
        {
            RavenBook.BoardPage page = new RavenBook.BoardPage();
            Player me = Player.m_localPlayer;
            RavenBook.BoardRow story = StoryCard(h);
            if (story != null) { RavenBook.BoardGroup g = new RavenBook.BoardGroup { Head = Lang.T("сюжет") }; g.Rows.Add(story); page.Groups.Add(g); }
            RavenBook.BoardGroup words = new RavenBook.BoardGroup { Head = Lang.T("обіцянки") }, asks = new RavenBook.BoardGroup { Head = Lang.T("прохання") }, small = new RavenBook.BoardGroup { Head = Lang.T("дрібні справи") }, wishes = new RavenBook.BoardGroup { Head = Lang.T("мрії") };
            foreach (NpcActor a in NpcHeart.Residents(h.Id))
            {
                NpcActor person = a;
                Action open = ToPeople(new List<NpcActor> { a });
                foreach (KeyValuePair<string, long> w in NpcPromises.Yours(a, me))
                    words.Rows.Add(new RavenBook.BoardRow { Label = a.Name, Text = w.Key, Far = w.Value > 0L ? Lang.F("лишилось {0}", NpcRescue.Left(w.Value)) : "", Dot = RavenBook.ToneWarn, Click = open, Arrow = true });
                NpcRequests.Request r = NpcRequests.Of(a);
                if (r != null && (me == null || !NpcPromises.Has(a, me.GetPlayerID(), "request")))
                {
                    int have = NpcRequests.Carried(me, a);
                    bool can = NpcRequests.CanGive(me, a);
                    asks.Rows.Add(new RavenBook.BoardRow { Label = a.Name, Text = NpcRequests.Words(r.Item, r.Count), Sub = Lang.F("віддячить: {0}", NpcRequests.PayWords(r)), Far = NpcRescue.Left(r.Until - (long)NpcRescue.Now), Link = can ? Lang.T("можна віддати") : have > 0 ? have + " / " + r.Count : "", Click = open, Arrow = true });
                }
                foreach (KeyValuePair<string, long> w in NpcTasks.Yours(a, me))
                    small.Rows.Add(new RavenBook.BoardRow { Label = a.Name, Text = w.Key, Far = Lang.F("лишилось {0}", NpcRescue.Left(w.Value)), Click = open, Arrow = true });
                if (NpcGoals.ForYou(a)) wishes.Rows.Add(new RavenBook.BoardRow { Label = a.Name, Text = a.Form(NpcGoals.Card(a)), Far = NpcGoals.CardSince(a), Click = () => { if (person != null) NpcProfile.Show(person, h.Title, () => Show(h, b.Open)); } });
            }
            foreach (RavenBook.BoardGroup g in new[] { words, asks, small, wishes }) if (g.Rows.Count > 0) page.Groups.Add(g);
            string rumour = h.RumourLine();
            if (rumour != null) { RavenBook.BoardGroup g = new RavenBook.BoardGroup { Head = Lang.T("чутки") }; g.Rows.Add(Row("", rumour)); page.Groups.Add(g); } // (when the next one comes; one going on stands in «Треба зробити», its own group «поклик»)
            if (page.Groups.Count == 0) page.Groups.Add(new RavenBook.BoardGroup { Text = Lang.T("Поки нічого не чекає на тебе.") });
            return page;
        }

        // ---- «Життя оселі»
        private static RavenBook.BoardPage Life(NpcHeart h)
        {
            RavenBook.BoardPage page = new RavenBook.BoardPage();
            List<NpcActor> who = NpcHeart.Residents(h.Id);
            RavenBook.BoardGroup friends = new RavenBook.BoardGroup { Head = Lang.T("дружать") }, foes = new RavenBook.BoardGroup { Head = Lang.T("сваряться") }, events = new RavenBook.BoardGroup { Head = Lang.T("сталося") };
            List<NpcAttitude.Tie> ties = NpcAttitude.Ties(who);
            for (int i = 0; i < ties.Count && i < TiesMost; i++)
            {
                NpcAttitude.Tie t = ties[i];
                string pair = Lang.F("{0} і {1}", t.A.Name, t.B.Name);
                if (t.Foes)
                {
                    RavenBook.BoardRow row = Row("", pair, RavenBook.ToneBad, t.Why);
                    row.TipHead = Lang.T("ЯК ПОМИРИТИ"); // (whom to speak to: the one who holds it — NpcAttitude.Grudge goes by the same)
                    row.Tip = Lang.F("Образу тримає {0}.\nПоговори з цією людиною: вона сама заговорить про образу — відповідай «Помиріться вже». Бурчуни погоджуються рідше; мириться раз на день, і спершу з тим, на кого ображена найбільше.\nЯкщо не сваритимуться знову, образа згасне й сама, але повільно.", t.Holds.Name);
                    row.Click = ToPeople(new List<NpcActor> { t.A, t.B }); row.Arrow = true;
                    foes.Rows.Add(row);
                }
                else friends.Rows.Add(Row("", pair, RavenBook.BoardNoDot, t.Close ? Lang.T("близькі") : ""));
            }
            foreach (KeyValuePair<string, long> e in NpcMemory.HomeEvents(who, EventsMost)) events.Rows.Add(Row("", e.Key, RavenBook.BoardNoDot, NpcMemory.Ago(e.Value)));
            foreach (RavenBook.BoardGroup g in new[] { friends, foes, events }) if (g.Rows.Count > 0) page.Groups.Add(g);
            if (page.Groups.Count == 0) page.Groups.Add(new RavenBook.BoardGroup { Text = Lang.T("Поки нічого такого: живуть собі.") });
            return page;
        }

        // ---- «Місця»: only to read and to show (what is done to them: the wooden buttons)
        private static RavenBook.BoardPage Places(NpcHeart h, Look l)
        {
            RavenBook.BoardPage page = new RavenBook.BoardPage();
            RavenBook.BoardGroup g = new RavenBook.BoardGroup();
            NpcBase b = NpcBase.For(h.transform.position);
            if (b != null && b.Ready)
            {
                Component first = FirstFire(b);
                RavenBook.BoardRow fire = Row(Lang.T("вогнища"), l.Fires == 0 ? Lang.T("нема") : Lang.F("горить {0} із {1}", l.Burning, l.Fires), l.Fires == 0 ? RavenBook.BoardGreyDot : l.Burning == 0 ? RavenBook.ToneBad : l.Burning < l.Fires ? RavenBook.ToneWarn : RavenBook.ToneGood);
                if (first != null) { fire.Link = Lang.T("показати"); fire.Click = ToSpot(first.transform.position, Lang.T("Вогнище")); }
                g.Rows.Add(fire);
                g.Rows.Add(Row(Lang.T("ліжка"), l.Beds == 0 ? Lang.T("нема") : Lang.F("{0}, своє мають {1}", l.Beds, l.HasBed), l.Beds == 0 ? RavenBook.BoardGreyDot : l.HasBed < l.Who.Count ? RavenBook.ToneWarn : RavenBook.ToneGood));
            }
            RavenBook.BoardRow box = Row(Lang.T("скарбничка"), Lang.N(h.Coins, "монета|монети|монет"));
            if (h.Box != null) { box.Link = Lang.T("показати"); box.Click = ToChest(h.Box, Lang.T("Скарбничка")); }
            g.Rows.Add(box);
            page.Groups.Add(g);
            RavenBook.BoardGroup own = new RavenBook.BoardGroup { Head = Lang.T("улюблені місця") };
            foreach (NpcActor a in l.Who)
            {
                Vector3 at;
                if (own.Rows.Count >= PlacesMost || !NpcPlaces.Favourite(a, out at)) continue;
                Vector3 spot = at;
                own.Rows.Add(new RavenBook.BoardRow { Label = a.Name, Text = Lang.T("своє місце"), Link = Lang.T("показати"), Click = ToSpot(spot, a.Form(Lang.F("Місце, де любить бувати {0}", a.Name))) });
            }
            if (own.Rows.Count > 0) page.Groups.Add(own);
            return page;
        }

        // its map not made on this game yet (its people run by a friend's game: made when the board opened, some seconds on a big base): shown by
        // itself the moment it is, not «try again» (the user 2026-11-08 pressed it and saw nothing). One waiting at a time, at most EdgesWait s
        private const float EdgesWait = 60f;
        private static NpcBase _edgesFor;
        private static void ShowEdges(NpcBase b)
        {
            Player p = Player.m_localPlayer;
            if (b == null) return;
            if (!b.Ready)
            {
                if (p != null) p.Message(MessageHud.MessageType.Center, Lang.T("Межі рахуються — покажу, щойно будуть."));
                if (_edgesFor == null && Plugin.Instance != null) Plugin.Instance.StartCoroutine(EdgesWhenReady(b));
                _edgesFor = b;
                return;
            }
            b.Show(30f);
            if (p != null) p.Message(MessageHud.MessageType.Center, Lang.T("Межі оселі: золоті вогники на землі й позначки на мапі."));
        }

        private static System.Collections.IEnumerator EdgesWhenReady(NpcBase b)
        {
            float until = Time.time + EdgesWait;
            while (_edgesFor != null && !_edgesFor.Ready && Time.time < until)
            {
                NpcBase.For(_edgesFor.Centre); // (asked: kept, and a making that broke off is begun again)
                yield return new WaitForSeconds(0.5f);
            }
            NpcBase got = _edgesFor; _edgesFor = null;
            Player p = Player.m_localPlayer;
            if (got == null || p == null) yield break;
            if (got.Ready) ShowEdges(got);
            else p.Message(MessageHud.MessageType.Center, Lang.T("Межі оселі не вдалося порахувати."));
        }
    }
}
#endif
