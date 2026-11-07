#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A person's feeling now (the user 2026-11-05, «живі NPC»): not its mood, which goes on for days (NpcToil), but what a moment
    // just did to it, for a minute or two — and it shows: its body (a wish of the same name to the body's motor: NpcGait.Moods, the
    // rows «mood_<kind>» of gaits.txt), the moment it comes (React: a start back, a laugh — Moves; a word, theme feel_<kind>), its greeting
    // while it lasts (Variant: feel_<kind>_greet), its eyes (a look at what did it,
    // on its whim layer: an order or a scene over it looks where they say). Kinds:
    //   fright   hit, a fight or a raid round it               hunched, shoulders up, quicker
    //   joy      good news (a boss fallen), you came back      a lighter step, chin up
    //   awe      it saw something the story did (StoryTales)   its head up, slower
    //   grief    one it liked was let go (NpcTalk.Unhire)       its head down, slow, its arms hang
    // One at a time: a stronger one (Rank) takes the place of a weaker one, the same again lasts longer — at most Longest times its
    // own term, and only when that adds at least Refresh of it (blow after blow: one write now and then, not one a blow). Kept in its ZDO (kind, the
    // world second it ends, where it looks): every game shows the same body, a reload or a new owner goes on with it. Set only
    // where it is run (Feel: its owner's own sources — NpcActor.Ticks, the bus events raised there); faded the last FadeShare of it.
    internal static class NpcFeel
    {
        internal const string Fright = "fright", Joy = "joy", Awe = "awe", Grief = "grief";
        internal static float FadeShare = 0.3f, LookFor = 4f; // the last share of its time it fades · s it looks at what did it
        internal static float Longest = 2f, Refresh = 0.4f;   // the same again: at most this many times its own term · added less than this share of it: let be
        private static readonly int KFeel = "RavenWatch_npcFeel".GetStableHashCode(); // string "kind until for x y z" ("" none)
        private const string Whose = "feel";
        private static readonly Dictionary<string, int> Rank = new Dictionary<string, int> { { Joy, 1 }, { Awe, 2 }, { Grief, 3 }, { Fright, 4 } };

        // where it is run: it feels that for so long, its eyes on «at» a moment (look: there is something to look at)
        internal static void Feel(NpcActor a, string kind, float seconds, Vector3 at, bool look)
        {
            if (a == null || !a.Owned || a.Zdo == null || !Rank.ContainsKey(kind)) return;
            double now = NpcRescue.Now;
            string had; float left, whole; Vector3 was;
            if (Read(a, out had, out left, out whole, out was) && had != kind && Rank[had] > Rank[kind]) return; // (a stronger one goes on)
            if (had == kind)
            {
                float longer = Mathf.Min(left + seconds * 0.5f, seconds * Longest); // (the same again: longer, but never past Longest times its own)
                if (longer - left < seconds * Refresh) return; // (little to add: nothing written, no look — a fight's blow after blow wrote every person near each time, 2026-11-05)
                seconds = Mathf.Max(seconds, longer);
            }
            a.Zdo.Set(KFeel, kind + " " + (now + seconds).ToString("0", CultureInfo.InvariantCulture) + " " + seconds.ToString("0", CultureInfo.InvariantCulture) + " " + Npcs.V(at));
            if (look) { a.Order("lookat " + Npcs.V(at), NpcMind.Layer.Whim, Whose); Looking[a] = Time.time + LookFor; }
            Npcs.Log(a.Id + ": feels " + kind + " for " + seconds.ToString("0") + " s");
            if (had != kind) React(a, kind);
        }

        // the moment it comes: its body (Moves: not in a fight, not seated) and a word (its theme feel_<kind>, only with a player near to hear it)
        internal static readonly Dictionary<string, string> Moves = new Dictionary<string, string> { { Fright, "step_back" }, { Joy, "fire_laugh_l|fire_laugh_r" } };
        private static void React(NpcActor a, string kind)
        {
            NpcMind m = a.Mind;
            string move = null;
            string own = NpcNature.Reaction(a, kind); // (its nature's own: the coward shivers)
            if (own != null) move = own;
            if (a.Body != null && m != null && m.NowLayer < NpcMind.Layer.Fight && !m.SeatedNow && a.Body.ClipFree && (own != null || Moves.TryGetValue(kind, out move)))
            {
                string[] pick = move.Split('|');
                string clip = pick[Random.Range(0, pick.Length)];
                PAnimClip c = BodyClips.Find(clip);
                if (a.Body.Clip(clip) && kind == Fright && c != null) m.Still(c.Length); // (its feet step back: it stands for it, then runs or walks on — a raid's first moment)
            }
            if (kind == Fright || kind == Grief) NpcNature.Comfort(a); // (a caring one near soothes it)
            Player p = Player.GetClosestPlayer(a.transform.position, 15f);
            if (p == null || !NpcVoice.Free(a)) return;
            string s = NpcPhrases.Compose(a, "feel_" + kind, p);
            if (!string.IsNullOrEmpty(s)) a.Say(s, p, NpcActor.Voice.Unasked); // (a crowd frightened at once: one voice, not ten)
        }

        // its greeting while it feels it (NpcPhrases.Variants): its feeling's own words before the plain «see_player»
        internal static string Variant(NpcActor a, Player to, string theme)
        {
            string kind; float f;
            if (theme != "see_player" || !Of(a, out kind, out f) || f < 0.3f) return null;
            return NpcPhrases.Has(a, "feel_" + kind + "_greet") ? "feel_" + kind + "_greet" : null;
        }

        // what it feels now and how much (1 … 0 as it fades); false: nothing
        internal static bool Of(NpcActor a, out string kind, out float strength)
        {
            kind = ""; strength = 0f;
            float left, whole; Vector3 at;
            if (!Read(a, out kind, out left, out whole, out at)) return false;
            strength = Mathf.Clamp01(left / Mathf.Max(1f, FadeShare * whole));
            return true;
        }

        private static bool Read(NpcActor a, out string kind, out float left, out float whole, out Vector3 at)
        {
            kind = ""; left = 0f; whole = 1f; at = Vector3.zero;
            string s = a != null && a.Zdo != null ? a.Zdo.GetString(KFeel, "") : "";
            if (s.Length == 0) return false;
            string[] w = s.Split(' ');
            double until;
            if (w.Length < 6 || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out whole) || !double.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out until)) return false;
            left = (float)(until - NpcRescue.Now);
            if (left <= 0f || !Rank.ContainsKey(w[0])) return false;
            kind = w[0];
            float x, y, z;
            if (float.TryParse(w[3], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(w[4], NumberStyles.Float, CultureInfo.InvariantCulture, out y) && float.TryParse(w[5], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) at = new Vector3(x, y, z);
            return true;
        }

        // where it is run: its look given back, its feeling over cleared (NpcActor.Ticks: a time compare a person)
        private static readonly Dictionary<NpcActor, float> Looking = new Dictionary<NpcActor, float>(); // session: until when its eyes are the feeling's
        internal static void Tick(NpcActor a)
        {
            float until;
            if (Looking.TryGetValue(a, out until) && Time.time >= until) { Looking.Remove(a); a.Order("look none", NpcMind.Layer.Whim, Whose); }
            if (a.Zdo == null || a.Zdo.GetString(KFeel, "").Length == 0) return;
            string k; float left, whole; Vector3 at;
            if (!Read(a, out k, out left, out whole, out at)) a.Zdo.Set(KFeel, ""); // (over: written once)
        }

        internal static void Forget(NpcActor a) { Looking.Remove(a); }

        // from any game: sent to where it is run (NpcActor.Acts «feel»)
        internal static void Tell(NpcActor a, string kind, float seconds) { if (a != null && Rank.ContainsKey(kind)) a.Act("feel " + kind + " " + seconds.ToString("0", CultureInfo.InvariantCulture)); }

        // ---------------------------------------------------------------- its sources (where it is run)
        internal static void Wire()
        {
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
            NpcPhrases.Variants.Add(Variant); // its greeting while it feels it
            NpcGait.Moods.Add((a, into) => { string k; float f; if (Of(a, out k, out f)) NpcGait.Wish(into, k, f); }); // its body: the wish of its name (gaits.txt «mood_fright»…), every game from its ZDO
            NpcActor.Acts["feel"] = (a, w) => { float s; if (w.Length > 2 && float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out s)) Feel(a, w[1], s, a.transform.position, false); };
            // a person hit: itself afraid, those near it too, their eyes on it (the bus: raised where it is run)
            StoryBus.Listen("NpcFeel", e =>
            {
                if (e.Name == "npc_hit" && e.HasAt)
                    foreach (NpcActor o in NpcActor.Live)
                    {
                        if (o == null || !o.Owned) continue;
                        float d = Vector3.Distance(o.transform.position, e.At);
                        if (o.Id == e.Arg && d < 3f) Feel(o, Fright, 40f, e.At, false);
                        else if (d < HitNear) Feel(o, Fright, 20f, e.At, true);
                    }
                else if (e.Name.StartsWith("npc_remember_news_boss_", System.StringComparison.Ordinal)) { foreach (NpcActor o in NpcActor.Live) if (o != null && o.Owned && o.Id == e.Arg && (!e.HasAt || (o.transform.position - e.At).sqrMagnitude < 4f)) { Feel(o, Joy, 90f, o.transform.position, false); break; } } // (the bus names its template: two settlers share one, so also where it stands — one was glad twice, the other not, 2026-11-05)
            });
        }
        internal static float HitNear = 15f; // m round a person hit: who sees it is afraid too
    }
}
#endif
