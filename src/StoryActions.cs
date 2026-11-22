#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What a stage does on entering and on passing (StoryQuests «start:» / «done:»), one word and its own words:
    //   flag x · unflag x · var x N|+N|-N          the character's own
    //   world x · unworld x · wvar x N              the world's, for everyone (StoryWorld)
    //   goto <stage> · end [done|failed]            go elsewhere (stops the row)
    //   msg <текст>                                 a line in the middle of the screen
    //   raise <подія> [arg]                         an event on the bus
    //   scene <id>                                  play a scene (StoryScenes)
    //   npc <who> hold <place> …                    a word that stands till «free» or the next word, whatever comes between: StoryHold
    //   npc <who> follow|stay|free                  a person of the story between scenes (its mind's Scene layer: over its
    //                                               day till «free»; a scene that takes it lets it go at its end); whom it follows is
    //                                               the world's word (lead_<who>, as hold), so it may be given with the person far off
    //   gate <place> open|shut                      a place's great door unlocked for everyone, or locked again (LocState: its world key)
    //   give <item> [N] · take <item> [N]           into the player's bag (no room: at his feet) / out of it — the story's own thing taken on
    //                                               purpose (ItemMarks.Take: not put back as a loss; the feather into the fire of the rite)
    // The kinds are a registry (as StoryCond's and StoryScenes' steps): a new action is one entry, nothing else changes.
    // QuestOnly ones (goto, end) move a quest: a scene's «do» refuses them.
    internal static class StoryActions
    {
        internal sealed class Kind
        {
            public int Min;                       // words it needs after its own
            public string Usage;                  // how it is written (the errors)
            public bool QuestOnly;                // only in a quest's start:/done: (it moves the quest)
            public bool Fight;                    // it sets foes on the player at a place, its first word the fight's name and fact: a stage that starts it or waits for that fact is a fight — its ring on the map the game's red, not gold (StageDef.Fight)
            // What it leaves behind, for a quest walked with no game (tools/quest_dry.js reads these lines, as StoryCond.Kind.Dry; in the game only the author's jump reads them — StoryQuests.Rebuild):
            //   set|clear me|world <fact>   a fact of the character or of the world made or taken away ({1}, {2}: the action's own words)
            //   later me|world <fact>       a fact the game makes in time, not the action itself (the last wave fallen: «waves») — the dry walk counts it
            //                               made; the jump makes it only where a passed stage's wait asked for it (a jump to the fight stage: the fight is fought)
            //   order {1} <verb>            a word to a person that stands after the stage
            //   go · raise · scene · none   moves the quest · an event on the bus · plays a scene (its «do» lines count) · leaves nothing
            public string Dry;
            public System.Func<string[], string> Check;                                   // null: fine; else what is wrong (load time)
            public Dictionary<string, Kind> Verbs; public int VerbAt;  // (an action with verbs: the word at VerbAt picks its own entry, with all of the above)
            public System.Func<Player, StoryQuests.QuestDef, string[], string, string> Do; // (player, quest — null in a scene, words, the text after the word) → a stage to go to, else null
        }

        private static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            { "flag", new Kind { Dry = "set me flag:{1}", Min = 1, Usage = "flag <назва>", Do = (p, q, w, rest) => { Story.SetFlag(p, w[1], true); return null; } } },
            { "unflag", new Kind { Dry = "clear me flag:{1}", Min = 1, Usage = "unflag <назва>", Do = (p, q, w, rest) => { Story.SetFlag(p, w[1], false); return null; } } },
            { "var", new Kind { Dry = "set me var:{1}", Min = 2, Usage = "var <назва> N|+N|-N", Do = (p, q, w, rest) =>
                {
                    if (w[2].StartsWith("+") || w[2].StartsWith("-")) Story.AddVar(p, w[1], Errands.I(w[2].TrimStart('+'), 0));
                    else Story.SetVar(p, w[1], Errands.I(w[2].TrimStart('='), 0));
                    return null;
                } } },
            { "choose", new Kind { Dry = "set me chose:{1}", Min = 2, Usage = "choose <вибір> <шлях>   (записати вибір, наче гравець пройшов той шлях)",
                Check = w => Story.GoodName(w[1]) && w[1].IndexOf('.') < 0 && Story.GoodName(w[2]) ? null : "choose <вибір без крапок> <шлях>",
                Do = (p, q, w, rest) => { StoryQuests.Choose(p, w[1], w[2]); return null; } } },
            { "give", new Kind { Dry = "none", Min = 1, Usage = "give <предмет> [N]   (у торбу гравцеві; нема місця — до ніг)",
                Do = (p, q, w, rest) => { Give(p, w[1], w.Length > 2 ? Errands.I(w[2], 1) : 1); return null; } } },
            { "take", new Kind { Dry = "none", Min = 1, Usage = "take <предмет> [N]   (з торби гравця: сюжет забирає свою річ — не вертається, як загублена)",
                Do = (p, q, w, rest) => { if (p != null && !ItemMarks.Take(p, w[1], w.Length > 2 ? Errands.I(w[2], 1) : 1)) Story.Log("take: no " + w[1] + " in the bag"); return null; } } },
            { "world", new Kind { Dry = "set world world:{1}", Min = 1, Usage = "world <назва>", Do = (p, q, w, rest) => { StoryWorld.SetFlag(w[1], true); return null; } } },
            { "unworld", new Kind { Dry = "clear world world:{1}", Min = 1, Usage = "unworld <назва>", Do = (p, q, w, rest) => { StoryWorld.SetFlag(w[1], false); return null; } } },
            { "wvar", new Kind { Dry = "set world wvar:{1}", Min = 2, Usage = "wvar <назва> N|+N|-N", Do = (p, q, w, rest) =>
                {
                    if (w[2].StartsWith("+") || w[2].StartsWith("-")) StoryWorld.SetVar(w[1], StoryWorld.Var(w[1]) + Errands.I(w[2].TrimStart('+'), 0));
                    else StoryWorld.SetVar(w[1], Errands.I(w[2].TrimStart('='), 0));
                    return null;
                } } },
            { "goto", new Kind { Dry = "go", Min = 1, Usage = "goto <етап>", QuestOnly = true, Do = (p, q, w, rest) => w[1] } },
            { "end", new Kind { Dry = "go", Min = 0, Usage = "end [done|failed]", QuestOnly = true,
                Check = w => w.Length > 1 && w[1] != Story.Done && w[1] != Story.Failed ? "end done|failed" : null,
                Do = (p, q, w, rest) => { StoryQuests.Finish(p, q, w.Length > 1 ? w[1] : Story.Done); return ""; } } },
            { "msg", new Kind { Dry = "none", Min = 1, Usage = "msg <текст>", Do = (p, q, w, rest) => { if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, Lang.T(rest)); return null; } } },
            { "raise", new Kind { Dry = "raise", Min = 1, Usage = "raise <подія> [arg]", Do = (p, q, w, rest) => { StoryBus.Raise(w[1], w.Length > 2 ? w[2] : "", 0); return null; } } },
            { "scene", new Kind { Dry = "scene", Min = 1, Usage = "scene <id>", Do = (p, q, w, rest) => { StoryScenes.TryPlay(w[1]); return null; } } },
            { "npc", new Kind { Min = 2, VerbAt = 2, Usage = "npc <хто> follow|stay|free|hold <місце> …|prop fetch|carry", Verbs = new Dictionary<string, Kind>
                {
                    { "prop", new Kind { Dry = "none", Min = 1, Usage = "npc <хто> prop fetch|carry   (своя річ: fetch — сюжет змусив поставити, може йти по неї, коли вільна; carry — одразу знову на руці, де б не лежала: «test:» сцени, що її ставить)",
                        Check = w => w.Length < 4 || (w[3] != "fetch" && w[3] != "carry") ? "npc <хто> prop fetch|carry" : null,
                        Do = (p, q, w, rest) => { if (w[3] == "carry") StoryProps.Carry(w[1]); else StoryProps.Fetch(w[1]); return null; } } }, // (the world keeps it, as «hold»: the person need not be near)
                    { "hold", new Kind { Dry = "order {1} hold", Min = 1, Usage = StoryHold.Usage,
                        Check = w => StoryHold.Why(string.Join(" ", w, 3, w.Length - 3)),
                        Do = (p, q, w, rest) => { string bad = StoryHold.Set(w[1], string.Join(" ", w, 3, w.Length - 3)); if (bad != null) StoryQuests.Note(bad); return null; } } }, // (the world keeps it: the person need not be near)
                    { "follow", new Kind { Dry = "order {1} follow", Do = (p, q, w, rest) =>
                        {
                            long had = LeadOf(w[1]);
                            if (had != 0L && had != p.GetPlayerID() && LeaderHere(had)) { StoryQuests.Note("npc: «" + w[1] + "» уже йде за іншим гравцем — не перебиваю"); return null; } // (two players on one stage: the first one leads)
                            Lead(w[1], p.GetPlayerID()); // (the world keeps it: the person need not be near — a stage's actions handed to a game far off, LeadWatch takes it up when he comes)
                            NpcActor who = StoryScenes.Actor(w[1], p.transform.position);
                            if (who != null && !StoryScenes.HeldByOther(who)) who.Order("follow " + NpcKit.Ref(p.GetZDOID()) + " 3", NpcMind.Layer.Scene);
                            else StoryQuests.Note("npc: «" + w[1] + "» " + (who == null ? "не поруч" : "у сцені іншого гравця") + " — піде за тобою, коли будеш ближче " + LeadNear + " м");
                            return null;
                        } } },
                    { "lever", new Kind { Dry = "none", Min = 1, Usage = "npc <хто> lever <важіль …>   (важіль людини, NpcLevers: limp <годин гри>, npcflag <назва>…; людина мусить бути поруч)",
                        Check = w => System.Array.IndexOf(NpcLevers.Deeds, w[3].ToLowerInvariant()) < 0 ? "npc <хто> lever: нема важеля «" + w[3] + "» (" + string.Join(", ", NpcLevers.Deeds) + ")" : null,
                        Do = (p, q, w, rest) => PersonWord((pl, who) => who.Act("lever " + string.Join(" ", w, 3, w.Length - 3)))(p, q, w, rest) } }, // (on the game that runs it: NpcActor.Acts «lever»)
                    { "stay", new Kind { Dry = "order {1} stay", Do = (p, q, w, rest) => { LeadMineOff(p, w[1]); return PersonWord((pl, who) => who.Order("stay", NpcMind.Layer.Scene))(p, q, w, rest); } } },
                    { "free", new Kind { Dry = "order {1} free", Do = (p, q, w, rest) =>
                        {
                            StoryHold.Clear(w[1]); // («вільно», wherever it is)
                            LeadMineOff(p, w[1]);
                            NpcActor who = StoryScenes.Actor(w[1], p.transform.position);
                            if (who == null || StoryScenes.InCast(who)) return null; // (a scene's «do» on one it holds: the standing word is gone, the scene keeps it till its end)
                            return PersonWord((pl, a) => { a.Order("look none", NpcMind.Layer.Scene); a.Order("free", NpcMind.Layer.Scene); })(p, q, w, rest);
                        } } },
                } } },
            { "gate", new Kind { Dry = "set world gate:{1}={2}", Min = 2, Usage = "gate <місце> open|shut   (головна брама місця: відімкнути для всього світу чи знову замкнути)",
                Check = w => Locs.Get(w[1]) == null ? "нема місця «" + w[1] + "»" : w[2] != "open" && w[2] != "shut" ? "gate <місце> open|shut" : null,
                Do = (p, q, w, rest) => { LocState.SetGate(Locs.Get(w[1]), w[2] == "open"); return null; } } },
            { "hearth", new Kind { Dry = "set world hearth:{1}", Min = 2, Usage = "hearth <місце> 0|1|2   (вогнище місця для всього світу: 0 холодне, 1 Хель, 2 Варти — обряд глави 1)",
                Check = w => Locs.Get(w[1]) == null ? "нема місця «" + w[1] + "»" : w[2] != "0" && w[2] != "1" && w[2] != "2" ? "hearth <місце> 0|1|2" : null,
                Do = (p, q, w, rest) => { LocHearth.SetLevel(Locs.Get(w[1]), w[2][0] - '0'); return null; } } },
        };

        // a word to a person near: not one another player's scene holds, nor one that already goes after another player
        private static System.Func<Player, StoryQuests.QuestDef, string[], string, string> PersonWord(System.Action<Player, NpcActor> say)
        {
            return (p, q, w, rest) =>
            {
                NpcActor who = StoryScenes.Actor(w[1], p.transform.position);
                if (who == null) { StoryQuests.Note("npc: нема «" + w[1] + "» поруч"); return null; }
                if (StoryScenes.HeldByOther(who)) { StoryQuests.Note("npc: «" + w[1] + "» зараз у сцені іншого гравця — не чіпаю"); return null; } // (one scene a person)
                Player lead = Leader(who);
                if (lead != null && lead != p) { StoryQuests.Note("npc: «" + w[1] + "» уже йде за " + lead.GetPlayerName() + " — не перебиваю"); return null; } // (two players on one stage: the first one leads)
                say(p, who);
                return null;
            };
        }

        // whom a person of the story follows by a quest's word: the world's text «lead_<who>» = that player's number (as «hold», by the template's
        // id: any game writes it, the person need not be near nor even loaded). It follows him while he is in the world within LeadNear m of it,
        // else nobody — no renewing, a player gone or far frees it by itself. The word outlives the mind's Scene layer (not kept): LeadWatch,
        // where the person is run, takes it up again after a reload, a new owner, or when he comes near
        private const float LeadNear = 50f;
        private static string LeadKey(string who) { return "lead_" + who; }
        private static long LeadOf(string who) { long v; return long.TryParse(StoryWorld.Text(LeadKey(who)), out v) ? v : 0L; }
        private static void Lead(string who, long player) { StoryWorld.SetText(LeadKey(who), player != 0L ? player.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""); }

        // «stay», «free»: the lead is let go, unless another player here leads it (two players on one stage: the first one leads)
        private static void LeadMineOff(Player p, string who) { long had = LeadOf(who); if (had != 0L && (had == p.GetPlayerID() || !LeaderHere(had))) Lead(who, 0L); }

        // the quest's «follow» still stands, its player here, and it follows no more (where it is run, now and then: NpcMind.SceneWatch)
        internal static void LeadWatch(NpcActor a)
        {
            if (a.Mind == null || a.Mind.Of(NpcMind.Layer.Scene) != null || NpcVitals.IsDown(a)) return;
            Player lead = Leader(a);
            if (lead != null) a.Order("follow " + NpcKit.Ref(lead.GetZDOID()) + " 3", NpcMind.Layer.Scene);
        }

        // whom it follows by the story's word now (0: nobody) — a scene notes it as it takes the person
        internal static long LeadNow(NpcActor a) { return a != null ? LeadOf(a.Id) : 0L; }
        // a scene took it and let it go: the word it cut across is over too («a scene that takes it lets it go at its end»); one given meanwhile stands
        internal static void LeadOff(NpcActor a, long led) { long had = LeadOf(a.Id); if (had != 0L && had == led) Lead(a.Id, 0L); }

        // the player of that number in this game, alive (a lead of one gone from the world stands for nobody)
        private static Player PlayerHere(long id)
        {
            foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetPlayerID() == id && !o.IsDead()) return o;
            return null;
        }
        private static bool LeaderHere(long id) { return PlayerHere(id) != null; }

        private static Player Leader(NpcActor a)
        {
            long id = LeadOf(a.Id);
            Player o = id != 0L ? PlayerHere(id) : null;
            return o != null && Vector3.Distance(o.transform.position, a.transform.position) < LeadNear ? o : null;
        }

        // a module's own action (the same way as the ones above)
        internal static void Register(string word, Kind k) { Kinds[word] = k; }

        // the kind of an action as written (its verb's own) and its words: what it leaves behind is the kind's «Dry» (the author's jump: StoryQuests.Rebuild)
        internal static Kind KindOf(string a, out string[] w)
        {
            w = Words(a);
            Kind k;
            if (w.Length == 0 || !Kinds.TryGetValue(w[0].ToLowerInvariant(), out k)) return null;
            if (k.Verbs == null) return k;
            Kind v;
            return w.Length > k.VerbAt && k.Verbs.TryGetValue(w[k.VerbAt], out v) ? v : null;
        }

        // nobody leads that person any more, whoever did (the author's jump: the world as if walked to a stage)
        internal static void LeadClear(string who) { Lead(who, 0L); }

        // a thing into the player's bag (the game's pickup line), at the feet when it is full
        private static void Give(Player p, string prefab, int n)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            if (d == null) { Story.Log("give: no item " + prefab); return; }
            ItemDrop.ItemData item = d.m_itemData.Clone();
            item.m_dropPrefab = go;
            item.m_stack = Mathf.Clamp(n, 1, Mathf.Max(1, item.m_shared.m_maxStackSize));
            if (p.GetInventory().AddItem(item)) p.ShowPickupMessage(item, item.m_stack);
            else ItemDrop.DropItem(item, item.m_stack, p.transform.position + p.transform.forward + Vector3.up, Quaternion.identity);
        }

        private static string[] Words(string a) { return a.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries); }

        // null: fine; else what is wrong (load time). inScene: a scene's «do» — no quest-only ones
        internal static string Check(string a) { return Check(a, false); }
        internal static string Check(string a, bool inScene)
        {
            string[] w = Words(a);
            Kind k;
            if (w.Length == 0) return null;
            if (!Kinds.TryGetValue(w[0].ToLowerInvariant(), out k)) return "невідома дія «" + w[0] + "» (є: " + string.Join(", ", new List<string>(Kinds.Keys).ToArray()) + ")";
            if (inScene && k.QuestOnly) return w[0] + " — лише в квесті, не в сцені";
            if (w.Length - 1 < k.Min) return "дія «" + a + "»: мало слів (" + k.Usage + ")";
            if (k.Verbs != null)
            {
                Kind v;
                if (!k.Verbs.TryGetValue(w[k.VerbAt], out v)) return w[0] + ": невідоме «" + w[k.VerbAt] + "» (" + k.Usage + ")";
                if (w.Length - 1 - k.VerbAt < v.Min) return "дія «" + a + "»: мало слів (" + (v.Usage ?? k.Usage) + ")";
                return v.Check != null ? v.Check(w) : null;
            }
            return k.Check != null ? k.Check(w) : null;
        }

        internal static void CheckGoto(StoryQuests.QuestDef q, string a, string at, List<string> errors)
        {
            string[] w = Words(a);
            if (w.Length >= 2 && w[0] == "goto" && w[1] != "end" && q.Stage(w[1]) == null) errors.Add(at + "goto — нема етапу «" + w[1] + "»");
        }

        // does it; a stage's id to go to (goto/end), else null
        internal static string Do(Player p, StoryQuests.QuestDef q, string a)
        {
            string[] w = Words(a);
            Kind k;
            if (w.Length == 0 || !Kinds.TryGetValue(w[0].ToLowerInvariant(), out k)) return null;
            if (k.QuestOnly && q == null) return null; // (a scene: never)
            if (k.Verbs != null) { Kind v; if (w.Length <= k.VerbAt || !k.Verbs.TryGetValue(w[k.VerbAt], out v)) return null; k = v; } // (its verb's own)
            if (k.Do == null) return null;
            string rest = w.Length > 1 ? a.Substring(a.IndexOf(w[1], w[0].Length)) : "";
            return k.Do(p, q, w, rest);
        }
    }
}
#endif
