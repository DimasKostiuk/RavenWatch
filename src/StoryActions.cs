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
    //   npc <who> follow|stay|free                  a person of the story between scenes (its mind's Scene layer: over its
    //                                               day till «free»; a scene that takes it lets it go at its end)
    //   hallgate open|shut                          the Hall's great door unlocked for everyone (HallState: its world key)
    // The kinds are a registry (as StoryCond's and StoryScenes' steps): a new action is one entry, nothing else changes.
    // QuestOnly ones (goto, end) move a quest: a scene's «do» refuses them.
    internal static class StoryActions
    {
        internal sealed class Kind
        {
            public int Min;                       // words it needs after its own
            public string Usage;                  // how it is written (the errors)
            public bool QuestOnly;                // only in a quest's start:/done: (it moves the quest)
            public System.Func<string[], string> Check;                                   // null: fine; else what is wrong (load time)
            public System.Func<Player, StoryQuests.QuestDef, string[], string, string> Do; // (player, quest — null in a scene, words, the text after the word) → a stage to go to, else null
        }

        private static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            { "flag", new Kind { Min = 1, Usage = "flag <назва>", Do = (p, q, w, rest) => { Story.SetFlag(p, w[1], true); return null; } } },
            { "unflag", new Kind { Min = 1, Usage = "unflag <назва>", Do = (p, q, w, rest) => { Story.SetFlag(p, w[1], false); return null; } } },
            { "var", new Kind { Min = 2, Usage = "var <назва> N|+N|-N", Do = (p, q, w, rest) =>
                {
                    if (w[2].StartsWith("+") || w[2].StartsWith("-")) Story.AddVar(p, w[1], Errands.I(w[2].TrimStart('+'), 0));
                    else Story.SetVar(p, w[1], Errands.I(w[2].TrimStart('='), 0));
                    return null;
                } } },
            { "choose", new Kind { Min = 2, Usage = "choose <вибір> <шлях>   (записати вибір, наче гравець пройшов той шлях)",
                Check = w => Story.GoodName(w[1]) && w[1].IndexOf('.') < 0 && Story.GoodName(w[2]) ? null : "choose <вибір без крапок> <шлях>",
                Do = (p, q, w, rest) => { StoryQuests.Choose(p, w[1], w[2]); return null; } } },
            { "give", new Kind { Min = 1, Usage = "give <предмет> [N]   (у торбу гравцеві; нема місця — до ніг)",
                Do = (p, q, w, rest) => { Give(p, w[1], w.Length > 2 ? Errands.I(w[2], 1) : 1); return null; } } },
            { "world", new Kind { Min = 1, Usage = "world <назва>", Do = (p, q, w, rest) => { StoryWorld.SetFlag(w[1], true); return null; } } },
            { "unworld", new Kind { Min = 1, Usage = "unworld <назва>", Do = (p, q, w, rest) => { StoryWorld.SetFlag(w[1], false); return null; } } },
            { "wvar", new Kind { Min = 2, Usage = "wvar <назва> N|+N|-N", Do = (p, q, w, rest) =>
                {
                    if (w[2].StartsWith("+") || w[2].StartsWith("-")) StoryWorld.SetVar(w[1], StoryWorld.Var(w[1]) + Errands.I(w[2].TrimStart('+'), 0));
                    else StoryWorld.SetVar(w[1], Errands.I(w[2].TrimStart('='), 0));
                    return null;
                } } },
            { "goto", new Kind { Min = 1, Usage = "goto <етап>", QuestOnly = true, Do = (p, q, w, rest) => w[1] } },
            { "end", new Kind { Min = 0, Usage = "end [done|failed]", QuestOnly = true,
                Check = w => w.Length > 1 && w[1] != Story.Done && w[1] != Story.Failed ? "end done|failed" : null,
                Do = (p, q, w, rest) => { StoryQuests.Finish(p, q, w.Length > 1 ? w[1] : Story.Done); return ""; } } },
            { "msg", new Kind { Min = 1, Usage = "msg <текст>", Do = (p, q, w, rest) => { if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, Lang.T(rest)); return null; } } },
            { "raise", new Kind { Min = 1, Usage = "raise <подія> [arg]", Do = (p, q, w, rest) => { StoryBus.Raise(w[1], w.Length > 2 ? w[2] : "", 0); return null; } } },
            { "scene", new Kind { Min = 1, Usage = "scene <id>", Do = (p, q, w, rest) => { StoryScenes.TryPlay(w[1]); return null; } } },
            { "npc", new Kind { Min = 2, Usage = "npc <хто> follow|stay|free",
                Check = w => w[2] != "follow" && w[2] != "stay" && w[2] != "free" ? "npc <хто> follow|stay|free" : null,
                Do = (p, q, w, rest) =>
                {
                    NpcActor who = StoryScenes.Actor(w[1], p.transform.position);
                    if (who == null) { StoryQuests.Note("npc: нема «" + w[1] + "» поруч"); return null; }
                    if (StoryScenes.HeldByOther(who)) { StoryQuests.Note("npc: «" + w[1] + "» зараз у сцені іншого гравця — не чіпаю"); return null; } // (one scene a person)
                    Player lead = Leader(who);
                    if (lead != null && lead != p) { StoryQuests.Note("npc: «" + w[1] + "» уже йде за " + lead.GetPlayerName() + " — не перебиваю"); return null; } // (two players on one stage: the first one leads)
                    if (w[2] == "follow") { who.Order("follow " + NpcKit.Ref(p.GetZDOID()) + " 3", NpcMind.Layer.Scene); Lead(who, p.GetPlayerID()); }
                    else if (w[2] == "stay") { who.Order("stay", NpcMind.Layer.Scene); Lead(who, 0L); }
                    else { who.Order("look none", NpcMind.Layer.Scene); who.Order("free", NpcMind.Layer.Scene); Lead(who, 0L); }
                    return null;
                } } },
            { "hallgate", new Kind { Min = 1, Usage = "hallgate open|shut",
                Check = w => w[1] != "open" && w[1] != "shut" ? "hallgate open|shut" : null,
                Do = (p, q, w, rest) =>
                {
                    if (ZoneSystem.instance == null || Locs.Hall == null) return null;
                    if (w[1] == "open") ZoneSystem.instance.SetGlobalKey(Locs.Hall.KeyGate); else ZoneSystem.instance.RemoveGlobalKey(Locs.Hall.KeyGate);
                    return null;
                } } },
        };

        // whom a person of the story follows by a quest's word (its ZDO, written where it is run): that player while he
        // is in the world within LeadNear m of it, else nobody — no renewing, a player gone or far frees it by itself. Kept in
        // the ZDO, so the word outlives the mind's Scene layer (not kept): LeadWatch takes it up again after a reload, a new owner
        private static readonly int KLead = "RavenWatch_npcLead".GetStableHashCode();
        private const float LeadNear = 50f;
        // (every game that runs the story: the person's owner writes it — StoryScenes.Wire)
        internal static void Wire() { NpcActor.Acts["storylead"] = (n, w) => { ZDO z = n.Zdo; long v; if (z != null) z.Set(KLead, w.Length > 1 && long.TryParse(w[1], out v) ? v : 0L); }; }

        private static void Lead(NpcActor a, long player) { a.Act("storylead " + player); }

        // the quest's «follow» still stands, its player here, and it follows no more (where it is run, now and then: NpcMind.SceneWatch)
        internal static void LeadWatch(NpcActor a)
        {
            if (a.Mind == null || a.Mind.Of(NpcMind.Layer.Scene) != null || NpcVitals.IsDown(a)) return;
            Player lead = Leader(a);
            if (lead != null) a.Order("follow " + NpcKit.Ref(lead.GetZDOID()) + " 3", NpcMind.Layer.Scene);
        }

        // a scene took it and let it go: that word is over too («a scene that takes it lets it go at its end»)
        internal static void LeadOff(NpcActor a) { ZDO z = a.Zdo; if (z != null && z.GetLong(KLead, 0L) != 0L) Lead(a, 0L); }

        private static Player Leader(NpcActor a)
        {
            ZDO z = a.Zdo;
            long id = z != null ? z.GetLong(KLead, 0L) : 0L;
            if (id == 0L) return null;
            foreach (Player o in Player.GetAllPlayers())
                if (o != null && o.GetPlayerID() == id && !o.IsDead() && Vector3.Distance(o.transform.position, a.transform.position) < LeadNear) return o;
            return null;
        }

        // a module's own action (the same way as the ones above)
        internal static void Register(string word, Kind k) { Kinds[word] = k; }

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
            if (w.Length == 0 || !Kinds.TryGetValue(w[0].ToLowerInvariant(), out k) || k.Do == null) return null;
            if (k.QuestOnly && q == null) return null; // (a scene: never)
            string rest = w.Length > 1 ? a.Substring(a.IndexOf(w[1], w[0].Length)) : "";
            return k.Do(p, q, w, rest);
        }
    }
}
#endif
