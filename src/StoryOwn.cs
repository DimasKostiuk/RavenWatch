#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Your own raven (story-lore.md «Хто має воронів», the prologue): the one that waited by your name all the centuries
    // and found you again. Not Haldor's stray and not Odin's new one — yours, from your first life (the raven keeps the
    // mark OwnKey; its lines may ask). It lost the old memory in the fog as you did.
    //   given back   you never had a raven at all: a new one of that kind, yours (Trust.Promise) but asleep, as one
    //                bought (the user 2026-11-03): the ritual at the Sacrificial stones wakes it, as ever. «own <kind>»:
    //                it flies in to your shoulder a few seconds later (Trust.SendAway → back: Back); «own <kind> here»:
    //                the scene's bird that flew onto your shoulder (bird come … as <kind>, bird shoulder) is it at once
    //   none         you have a raven already (any kind, bought or Odin's): the prologue gives no second one — the
    //                scene plays the same, nothing flies in (the user 2026-11-03: everyone walks the same prologue)
    // The book: «book» (an action) — Eirik's house gives it (RavenBook.Give: till then a raven only sits).
    // The story's parts (registered by StoryModules): the condition «ownraven hugin|munin|both|any|none» (a raven of that
    // kind is yours: Trial.HadKind), the scene step «own hugin|munin», its landing (Trust.BackFrom), the action «book»
    // and the condition «book» (he has it: the prologue hurries for one who bought a raven without it).
    internal static class StoryOwn
    {
        internal const string Why = "story_own";                    // its flight in to you (Trust.SendAway)
        internal const string OwnKey = "RavenWatch_own";            // on the raven: your own, from your first life
        internal static float FlySeconds = 4f;
        private static bool _conds, _steps;

        // ---------------------------------------------------------------- the story's words for it
        internal static void Conds()
        {
            if (_conds) return;
            _conds = true;
            StoryCond.Register("ownraven", new StoryCond.Kind { Usage = "ownraven hugin|munin|both|any|none   (ворон цього виду вже твій: був, є чи загубився)", Min = 1,
                Check = a => System.Array.IndexOf(new[] { "hugin", "munin", "both", "any", "none" }, a[1]) < 0 ? "ownraven hugin|munin|both|any|none" : null,
                Test = (p, a) =>
                {
                    bool h = Trial.HadKind(p, false), m = Trial.HadKind(p, true);
                    switch (a[1])
                    {
                        case "hugin": return h;
                        case "munin": return m;
                        case "both": return h && m;
                        case "any": return h || m;
                        default: return !h && !m;
                    }
                } });
            StoryCond.Register("book", new StoryCond.Kind { Usage = "book   (книга воронів уже в гравця: RavenBook.Given)", Test = (p, a) => RavenBook.Given(p) });
            StoryActions.Register("book", new StoryActions.Kind { Min = 0, Usage = "book   (книга воронів — тепер у тебе: відкриває книгу, коло й накази)",
                Do = (p, q, w, rest) => { if (!RavenBook.Given(p)) { RavenBook.Give(p); RavenMsg.Center(p, Lang.T("Книга воронів тепер у тебе.")); StoryBus.Raise("book", "", 0); } return null; } });
        }

        internal static void Steps()
        {
            if (_steps) return;
            _steps = true;
            StoryScenes.Register("own", new StoryScenes.StepKind(StoryScenes.StepSeen.Own) { Min = 1, Lasting = true, Usage = "own hugin|munin [here]   (here: птах сцени на плечі стає ним; твій власний ворон прилітає — лише якщо в тебе ще нема жодного)",
                Check = st => st.W[1] != "hugin" && st.W[1] != "munin" ? "own hugin|munin" : null,
                Begin = (p, st) => Return(p, st.W[1] == "munin", st.W.Length > 2 && st.W[2] == "here") });
            Trust.BackFrom[Why] = (p, munin, item) => Back(p, munin); // (landed on your shoulder)
            StoryScenes.Register("raven", new StoryScenes.StepKind(StoryScenes.StepSeen.Own) { Min = 2, Usage = "raven hugin|munin sit <місце> [side <м>] · raven hugin|munin back   (твій ворон з плеча сідає там і повертається; без ціни)",
                Check = st => (st.W[1] != "hugin" && st.W[1] != "munin") || (st.W[2] != "sit" && st.W[2] != "back") || (st.W[2] == "sit" && st.W.Length < 4) ? "raven hugin|munin sit <місце> | back" : null,
                Begin = (p, st) => Perch(p, st.W[1] == "munin", st.W[2] == "sit" ? st.W[3] : null, st.W.Length > 5 && st.W[4] == "side" ? Errands.F(st.W[5], 0f) : 0f) });
        }

        // your raven off your shoulder onto a place of the story (the perches' own spot, as an order but free) — or back
        // (null); one not on you: nothing (said)
        internal static void Perch(Player p, bool munin, string place, float side = 0f)
        {
            ItemDrop.ItemData item = p != null ? RavenSeat.On(p, munin) : null;
            if (item == null) { StoryScenes.Note("raven: " + (munin ? "Муніна" : "Хугіна") + " нема — крок пропущено"); return; }
            if (place == null) { Perches.CallSilent(p, item, munin); if (munin) _munOut = false; else _hugOut = false; return; }
            Vector3 at;
            if (!StoryWhere.Top(place, p.transform.position, side, out at)) { StoryScenes.Note("raven sit: нема місця «" + place + "»"); return; }
            Vector3 face = p.transform.position - at; face.y = 0f;
            Perches.ForScene = true;
            try
            {
                ItemDrop.ItemData on;
                string why = Perches.WhyNot(p, munin, out on); // (its refusal goes to the screen only: said in the log too)
                if (why == null) why = SitHere.Place(p, munin, at, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward), null, false);
                if (why != null) StoryScenes.Note("raven sit: " + why);
                else if (munin) _munOut = true; else _hugOut = true;
            }
            finally { Perches.ForScene = false; }
        }

        // a scene that ended with your raven still on its seat (stopped between «raven sit» and «raven back»): it comes back, one with no book could not call it
        private static bool _hugOut, _munOut;
        internal static void SceneEnd(bool broken)
        {
            Player p = Player.m_localPlayer;
            if (_hugOut) { _hugOut = false; Perch(p, false, null); }
            if (_munOut) { _munOut = false; Perch(p, true, null); }
        }

        // ---------------------------------------------------------------- your own raven
        internal static void Return(Player p, bool munin, bool here = false)
        {
            if (p == null) return;
            if (Trial.HadKind(p, false) || Trial.HadKind(p, true)) { if (here) StoryScenes.BirdVanish(); StoryScenes.Note("own: у гравця вже є ворон — свій не прилітає"); return; } // (one raven a prologue, and only to one who has none)
            ItemDrop.ItemData item = RavenItem.NewRaven(munin);
            if (item == null) { StoryScenes.Note("own: нема предмета ворона"); return; }
            item.m_customData[OwnKey] = "1";
            item.m_customData[Breeds.Key] = Breeds.None; // (your own raven is a plain black one, no breed — the user 2026-11-02)
            Trust.Promise(item, p); // (yours, asleep: the ritual wakes it)
            if (!p.GetInventory().AddItem(item))
            {
                if (here) StoryScenes.BirdVanish(); // (the bird on your shoulder: the raven is at your feet now)
                ItemDrop.DropItem(item, 1, p.transform.position + p.transform.forward + Vector3.up, Quaternion.identity); // (a full bag: at your feet)
                RavenMsg.Center(p, Lang.F("{0} сів біля тебе — у торбі нема місця.", Plugin.RavenName(munin)));
                return;
            }
            if (here) Shoulder.SeatedNext(munin); // (no fly-in: the picture is on your shoulder already)
            RavenSeat.Sit(p, item, true);
            if (here) { StoryScenes.BirdVanishWhen(() => Shoulder.Present(p, munin)); Back(p, munin); StoryScenes.Note("own: " + (munin ? "Мунін" : "Хугін") + " тепер твій (на плечі)"); return; } // (the bird on your shoulder is it: the same frame)
            Trust.SendAway(item, FlySeconds, Why); // (on your shoulder in a moment — it flies in)
            RavenFx.Play("sfx_raven_kaw", p.transform.position + p.transform.forward * 25f + Vector3.up * 12f, Quaternion.identity); // (from afar)
            StoryScenes.Note("own: " + (munin ? "Мунін" : "Хугін") + " летить до гравця");
        }

        // it landed (Trust: back from its flight «story_own»)
        internal static void Back(Player p, bool munin)
        {
            if (p == null) return;
            RavenAura.BackFromOdin(p, munin);
            RavenMsg.Center(p, munin ? Lang.T("Мунін знайшов тебе. Він ще спить: розбуди його біля Жертовних каменів.") : Lang.T("Хугін знайшов тебе. Він ще спить: розбуди його біля Жертовних каменів."));
            StoryBus.Raise("own_back", munin ? "munin" : "hugin", 0);
        }

        // ---------------------------------------------------------------- the author's test (raven story own / unown)
        // unown: the character as if it never had a raven of this kind — its ravens of that kind taken away (worn, bag,
        // shoulders), their copies, the marks; so the prologue's ways can be walked again
        internal static string Unown(Player p, bool munin)
        {
            if (p == null) return "Нема гравця.";
            List<ItemDrop.ItemData> all = new List<ItemDrop.ItemData>(p.GetInventory().GetAllItems());
            all.AddRange(RavenSeat.Shelved(p));
            int gone = 0;
            foreach (ItemDrop.ItemData item in all)
            {
                bool m;
                if (!RavenItem.IsRaven(item, out m) || m != munin) continue;
                if (item.m_equipped)
                {
                    RavenSeat.Crumbling = true; // (no offence taken: nobody took it off)
                    try { p.UnequipItem(item, false); } finally { RavenSeat.Crumbling = false; }
                }
                if (RavenSeat.OnShelf(item)) RavenSeat.Forget(item, "taken away (raven story unown)"); else p.GetInventory().RemoveItem(item);
                gone++;
            }
            int copies = Backup.ForgetKind(p, munin);
            p.m_customData.Remove(munin ? Trial.HadMuninKey : Trial.HadHuginKey);
            return (munin ? "Мунін" : "Хугін") + ": забрано воронів " + gone + ", копій " + copies + ". Тепер цього виду в персонажа нема (ворони вдома на сідалах лишились у світі).";
        }

    }
}
#endif
