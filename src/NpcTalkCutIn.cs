#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person who cuts into your talk with words of its own (the user 2026-11-14, Torvald: «у бесіді він може раптово почати розповідати небилицю,
    // дуже смішну… під час діалогу може просто перебити все і почати говорити цей текст»). Its template's lines:
    //   interrupts: <0..1>     how likely, at a question you pick, it cuts in instead of answering
    //   interrupt: <words>     what it says then, one line a variant (at least CutInLeast, or a made one: NpcDefs says so at load); the last CutInFresh it
    //                          told you not again soon. Its forms and values as a greeting's (NpcPhrases.Inline: {name}, «ходив|ходила», yours {…|…}); a made
    //                          one, «{@tall_tale}», is put together by NpcPhrases from its own theme (Torvald's: assets/story/words/torvald.txt)
    // When: an ordinary question of yours only — never a goodbye, the first screen's way back, a duty, an answer with deeds, what moves the story
    // (IsStory, LeadsToStory), a grey row; not at your first pick of a talk; at most once a talk. Your question is not used up: not marked read, its
    // deeds not done, not counted as asked again — the place you asked from shows again with its words instead of its own (_instead: the one way a
    // person's words stand in a place's stead, as a missed tone's do), so you ask again if you like.
    internal static partial class NpcTalk
    {
        internal const int CutInLeast = 6, CutInFresh = 3;
        private static bool _cutInDone;
        private static int _picksThisTalk;
        private static readonly Dictionary<string, List<int>> CutInTold = new Dictionary<string, List<int>>(); // session: which of a person's lines were told lately (its id → the last ones)

        private static void CutInReset() { _cutInDone = false; _picksThisTalk = 0; }

        // at your pick: its own words instead of an answer (null — it lets you speak)
        private static string CutIn(Answer a, List<RavenBook.TalkRow> rows, int i, string from, Player p)
        {
            _picksThisTalk++;
            NpcDef d = With != null ? With.Def : null;
            if (d == null || d.Interrupts <= 0f || d.Interrupt.Count == 0 || _cutInDone || _picksThisTalk < 2) return null;
            if (a.To == "end" || a.To == "start" || a.Acts.Count > 0 || Duties.Contains(a.To) || IsStory(a) || LeadsToStory(a.To)) return null;
            if (rows != null && i < rows.Count && rows[i].Off) return null;
            if (Random.value >= d.Interrupts) return null;
            List<int> told;
            if (!CutInTold.TryGetValue(d.Id, out told)) CutInTold[d.Id] = told = new List<int>();
            List<int> fresh = new List<int>();
            for (int k = 0; k < d.Interrupt.Count; k++) if (!told.Contains(k)) fresh.Add(k);
            int pick = fresh.Count > 0 ? fresh[Random.Range(0, fresh.Count)] : Random.Range(0, d.Interrupt.Count);
            string said = NpcPhrases.Inline(With, Lang.T(d.Interrupt[pick]), p);
            if (string.IsNullOrEmpty(said) || said.Trim().Length == 0 || said.Contains("{@")) return null; // (a made one with no theme of its own here: it lets you speak, never an empty line)
            told.Add(pick);
            while (told.Count > Mathf.Min(CutInFresh, d.Interrupt.Count - 1)) told.RemoveAt(0);
            _cutInDone = true;
            Npcs.Log(d.Id + ": cuts into your talk (line " + (pick + 1) + " of " + d.Interrupt.Count + "), your «" + a.Text + "» left unasked");
            return said;
        }
    }
}
#endif
