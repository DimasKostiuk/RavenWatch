#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person's own short moves (the user 2026-11-15: «урізноманітнити… для кожного сюжетного нпс набір своїх унікальних анімацій… Гейд часто має
    // анімацію пиття з келиху, для деяких нпс треба вимикати деякі анімації»): the one gate every move of its own life passes before it is played —
    // a place's (NpcPlaces.Now), its nature's, a word's gesture (NpcBody.Spoken), its talk's hands and listening (NpcSpeech), a feeling's (NpcFeel),
    // a whim's, a quarrel's, a favour's, a job's (NpcWorker), its poses at its ease (NpcStance). Not a scene's, a story word's (StoryHold), a carried
    // thing's (StoryProps), the console's: those play exactly what they name.
    //   its body   a clip is made on one kind of body, its hand points metres on it: «dv_…» a dvergr's, any other a man's. A clip of another body
    //              is never played on it (a dwarf's hand would reach for a man's head somewhere above his own). Its life names a move once, by the
    //              man's clip (a place's «talk_hands», a talk's hands, a word's nod): on a dvergr the gate plays its «dv_<that clip>» where one is
    //              made, nothing where none (Its) — a new dvergr clip of that name is a new move of his everywhere, with no word of code or data
    //   its seat   «ground_…» only on the ground, «sit_…» only on a bench or a chair (SeatFits): the knees are elsewhere on each
    //   shuns:     a kind of move that is not this person's (the user 2026-11-17, Heid stretching by the fire: «це треба виправити у ядрі, а не точково»):
    //              a clip says its kind itself, «tone <word>» in its file (Tones: loose — a stretch, a yawn, a scratch; merry — a laugh, a slap on the
    //              knee; drinks — a tankard, a toast; none: plain), a template says «shuns: drinks loose» — a new clip of that kind never reaches it, with
    //              no list to keep up. Its life only: a scene, a story word play what they name
    //   never:     a template's «never: <clip> <prefix*> …»: never played on it by its own life — a pose by its name (arms_cross), a clip's parts with it
    //   moves:     a template's own short moves «moves: a|b|c», seated «moves_sit: a|b» (clips that end by themselves): where its life picks a move
    //              of its own at a place (Pick with own), one of these OwnEach of the time a move it may make now (Own at most), and no more often when the place has none for it (a dvergr at a man's place: its own now and then, not at every turn).
    //              «clip@<need>»: only while that holds (Needs: «carry» — its own thing on its arm, Heid's basket for heid_herbs; «free» — both hands free of its own things, Lit's hammer). A move of one spot
    //              only is that spot's (StoryLife «spot … moves»: NpcPlaces.SpotMoves), not a need here
    //   poses:     a template's own poses at its ease «poses: <pose> <pose>» (NpcStance.Poses): one made for one person (Pose.Own) is taken only by
    //              who names it; a named one is worth Liked more to it
    //   a talk   someone talks to it (NpcMind.InTalk where it is run, NpcMind.HeldBy on any game: Talking) — the user 2026-11-16: «гном бє молотом хоча
    //              дивится на мене… Гейд підняла руки… Торвальд встав різко і почав потирати руки»: its own life plays then only what fits a talk (TalkFits:
    //              a word's gesture NpcBody.Words, its hands as it speaks and listens NpcSpeech, a calm pose — Talk). A long clip of its own (a place's, a story
    //              word's, its work's, a pose not of these) goes out by its way out and is asked again after the talk, each by its own source
    //              (NpcPlaces, StoryHold, NpcWorker.Hush, NpcStance); its stay at a place waits meanwhile (NpcMind: the whim's term does not run in a talk)
    //   variety    of a list it never makes the same move twice running while it may make another (One: the last one a person, a session's)
    // Read from the template's lines once a template (Sets: a template read anew is another NpcDef). Nothing per frame: a pick is a split and a
    // few compares, at the moment a move is made. Every game reads the same template, so nothing is sent.
    internal static class NpcMoves
    {
        internal static readonly string[] Words = { "moves", "moves_sit", "never", "poses", "shuns" };
        internal static readonly string[] Tones = { "loose", "merry", "drinks" }; // (a clip's «tone»; tools/clipcheck.js keeps the same words)
        // a calm pose fits a talk (its arms folded, a hand on its belt, elbows on its knees): «name*» — every one of that start
        internal static readonly string[] TalkCalm = { "arms_cross", "hands_back", "hands_belt", "hand_belt", "sit_hands", "sit_elbows", "ground_back", "ground_knee", "guard_*" }; // (a dvergr's «dv_<one of these>» with them: TalkFits)
        internal static float Own = 0.6f, OwnEach = 0.2f, Liked = 1.5f; // (its own: OwnEach a move it may make now, Own at most — one move alone is not every other one)

        private sealed class Set { public string[] Moves = new string[0], MovesSit = new string[0], Never = new string[0], Poses = new string[0], Shuns = new string[0]; }
        private static readonly Dictionary<NpcDef, Set> Sets = new Dictionary<NpcDef, Set>(); // (a template read anew is another NpcDef: read again)
        private static readonly char[] Bar = { '|' }, Gaps = { ' ', ',', '\t' };

        private static Set Of(NpcDef d)
        {
            Set s;
            if (d == null) return null;
            if (Sets.TryGetValue(d, out s)) return s;
            foreach (string line in d.Keep)
            {
                int c = line.IndexOf(':');
                if (c <= 0) continue;
                string key = line.Substring(0, c).Trim().ToLowerInvariant(), v = line.Substring(c + 1);
                int hash = v.IndexOf('#'); if (hash >= 0) v = v.Substring(0, hash); // (a comment after it)
                v = v.Trim();
                if (System.Array.IndexOf(Words, key) < 0) continue;
                if (s == null) s = new Set();
                if (key == "moves") s.Moves = Clean(v.Split(Bar, System.StringSplitOptions.RemoveEmptyEntries));
                else if (key == "moves_sit") s.MovesSit = Clean(v.Split(Bar, System.StringSplitOptions.RemoveEmptyEntries));
                else if (key == "never") s.Never = Clean(v.Split(Gaps, System.StringSplitOptions.RemoveEmptyEntries));
                else if (key == "poses") s.Poses = Clean(v.Split(Gaps, System.StringSplitOptions.RemoveEmptyEntries));
                else if (key == "shuns")
                {
                    s.Shuns = Clean(v.Split(Gaps, System.StringSplitOptions.RemoveEmptyEntries));
                    foreach (string t in s.Shuns) if (t != "none" && System.Array.IndexOf(Tones, t) < 0) Npcs.Log(d.Id + ": shuns «" + t + "» — no such kind of move (" + string.Join(" ", Tones) + ")");
                }
            }
            Sets[d] = s;
            if (s != null) foreach (string m in s.Moves) Known(d, m);
            if (s != null) foreach (string m in s.MovesSit) Known(d, m);
            return s;
        }
        private static string[] Clean(string[] w) { for (int i = 0; i < w.Length; i++) w[i] = w[i].Trim(); return w; }
        private static void Known(NpcDef d, string entry)
        {
            int at = entry.IndexOf('@');
            string clip = at < 0 ? entry : entry.Substring(0, at);
            if (at >= 0 && !Needs.ContainsKey(entry.Substring(at + 1))) Npcs.Log(d.Id + ": moves «" + entry + "» — no such need (" + string.Join(" ", new List<string>(Needs.Keys).ToArray()) + "): never made");
            if (BodyClips.Find(clip) == null) Npcs.Log(d.Id + ": moves «" + clip + "» — no such clip (assets/story/clips)"); // (said once a template)
        }

        // its body's clip: «dv_…» a dvergr's, any other a man's (the clips' own convention, docs/CLIPS.md «Тіло»)
        internal static bool BodyFits(NpcDef d, string clip) { return d != null && d.Dvergr == clip.StartsWith("dv_", System.StringComparison.Ordinal); }
        // a move named by its clip, as its body makes it: a man's clip on a man; on a dvergr its own «dv_<clip>» (a part with it: «dv_listen_chin.listen»)
        // where one is made; "" — none for this body
        internal static string Its(NpcDef d, string clip)
        {
            if (d == null || string.IsNullOrEmpty(clip)) return "";
            if (BodyFits(d, clip)) return clip;
            if (!d.Dvergr) return "";
            string own = "dv_" + clip;
            return BodyClips.Find(own) != null ? own : "";
        }
        // ...and whether its own life may make it now (May): the clip to play, "" — not now, not on it
        internal static string Clip(NpcActor a, string clip, bool asked = false)
        {
            string c = a != null ? Its(a.Def, clip) : "";
            return c.Length > 0 && May(a, c, asked) ? c : "";
        }

        // a seated clip on its own seat: «ground_…» on the ground (the game's «sit»), «sit_…» on a bench or a chair (after «dv_» on a dvergr's) — the hips half a
        // metre apart, the knees elsewhere: a slap on a knee of the other seat goes into the air (the user 2026-11-15, Torvald). Any other clip: as ever
        internal static bool SeatFits(NpcActor a, string clip)
        {
            string c = clip.StartsWith("dv_", System.StringComparison.Ordinal) ? clip.Substring(3) : clip;
            bool ground = c.StartsWith("ground_", System.StringComparison.Ordinal), bench = c.StartsWith("sit_", System.StringComparison.Ordinal);
            if (!ground && !bench) return true;
            NpcBody b = a.Body;
            if (b == null) return false;
            if (ground) return b.HeldGesture == "sit";
            string at = b.AttachedSeen;
            return at.Length > 0 && at != "attach_bed";
        }

        // may its own life play this clip on it (its body's, on its seat, not one its template says never; in a talk one that fits it — asked: the talk
        // itself asked for it, a favour, NpcFavours)
        internal static bool May(NpcActor a, string clip, bool asked = false)
        {
            if (a == null || a.Def == null || string.IsNullOrEmpty(clip) || clip == "none") return false;
            if (!BodyFits(a.Def, clip) || !SeatFits(a, clip)) return false;
            if (!asked && !TalkFits(clip) && Talking(a)) return false; // (in a talk: only what fits one)
            Set s = Of(a.Def);
            if (s == null) return true;
            if (s.Shuns.Length > 0) { PAnimClip c = BodyClips.Find(clip); if (c != null && c.Tone.Length > 0 && System.Array.IndexOf(s.Shuns, c.Tone) >= 0) return false; } // (a kind of move that is not its own)
            return !Listed(s.Never, clip);
        }
        // a clip in a list of names: «name» (its parts with it), «start*»
        private static bool Listed(string[] list, string clip)
        {
            int dot = clip.IndexOf('.');
            string whole = dot > 0 ? clip.Substring(0, dot) : clip; // (a clip's part goes with its clip)
            foreach (string n in list)
                if (n.EndsWith("*") ? clip.StartsWith(n.Substring(0, n.Length - 1), System.StringComparison.Ordinal) : (clip == n || whole == n)) return true;
            return false;
        }

        // someone talks to it now (its owner: the talk's hold; any other game: who holds it, in its ZDO)
        internal static bool Talking(NpcActor a) { return a != null && ((a.Mind != null && a.Mind.InTalk) || !NpcMind.HeldBy(a).IsNone()); }
        // a clip that fits a talk: the talk's own gestures (its words' NpcBody.Words, its hands and listening NpcSpeech.TalkClips) and a calm pose (TalkCalm)
        internal static bool TalkFits(string clip)
        {
            if (string.IsNullOrEmpty(clip)) return false;
            if (clip.StartsWith("dv_", System.StringComparison.Ordinal)) clip = clip.Substring(3); // (a dvergr's own of the same move)
            if (_talk == null)
            {
                List<string> t = new List<string>(TalkCalm);
                foreach (string[] w in NpcBody.Words.Values) foreach (string side in w) t.AddRange(side.Split(Bar, System.StringSplitOptions.RemoveEmptyEntries));
                t.AddRange(NpcSpeech.TalkClips);
                _talk = t.ToArray();
            }
            return Listed(_talk, clip);
        }
        private static string[] _talk; // (made once: the lists it is made of are fixed at start)

        // a pose at its ease: its own (Pose.Own) only for who names it; how much it likes it (× its worth)
        internal static bool MayPose(NpcActor a, string pose, bool own) { return May(a, pose) && (!own || Names(a, pose)); }
        internal static float PoseLiking(NpcActor a, string pose) { return Names(a, pose) ? Liked : 1f; }
        private static bool Names(NpcActor a, string pose) { Set s = a != null ? Of(a.Def) : null; return s != null && System.Array.IndexOf(s.Poses, pose) >= 0; }

        // one of «a|b|c» it may make, "" none; own: its template's own moves come in (a place's moves: NpcPlaces.Now)
        internal static string Pick(NpcActor a, string list, bool own = false, bool seated = false, bool asked = false)
        {
            string[] from = string.IsNullOrEmpty(list) ? null : list.Split(Bar, System.StringSplitOptions.RemoveEmptyEntries);
            if (own && a != null)
            {
                Set s = Of(a.Def);
                string[] mine = s == null ? null : seated ? s.MovesSit : s.Moves;
                if (mine != null && mine.Length > 0 && Random.value < Mathf.Min(Own, OwnEach * Count(a, mine, asked))) { string m = One(a, mine, asked); if (m.Length > 0) return m; }
            }
            return One(a, from, asked);
        }
        private static int Count(NpcActor a, string[] from, bool asked) { int n = 0; string clip; if (from != null) foreach (string c in from) if (Fits(a, c, asked, out clip)) n++; return n; }
        // one of those it may make now, by chance (a name twice in a list: twice as often) — never the one it made last while there is another
        // (the user 2026-11-17: «урізноманітнити»: by a fire it warmed its hands three times running)
        private static string One(NpcActor a, string[] from, bool asked)
        {
            if (from == null || from.Length == 0) return "";
            string last, clip;
            if (a == null || !Last.TryGetValue(a, out last)) last = "";
            int n = 0, other = 0;
            foreach (string c in from) if (Fits(a, c, asked, out clip)) { n++; if (clip != last) other++; }
            if (n == 0) return "";
            bool fresh = other > 0; // (another one to make: the last one sits this turn out)
            int k = Random.Range(0, fresh ? other : n);
            foreach (string c in from)
            {
                if (!Fits(a, c, asked, out clip) || (fresh && clip == last)) continue;
                if (k-- == 0) { if (a != null) Last[a] = clip; return clip; }
            }
            return "";
        }
        private static readonly Dictionary<NpcActor, string> Last = new Dictionary<NpcActor, string>(); // session: the move it made last, off when it goes (Wire)
        // «clip» or «clip@need»: the clip, if it may make it now (its need holds: Needs)
        private static bool Fits(NpcActor a, string entry, bool asked, out string clip)
        {
            clip = entry.Trim();
            int at = clip.IndexOf('@');
            string need = at < 0 ? "" : clip.Substring(at + 1);
            clip = Clip(a, at < 0 ? clip : clip.Substring(0, at), asked); // (its body's own of it: Its)
            if (clip.Length == 0) return false;
            System.Func<NpcActor, bool> holds;
            return at < 0 || (Needs.TryGetValue(need, out holds) && holds(a));
        }

        // its template names this move for that need («moves: heid_herbs@carry»): made for it
        internal static bool MadeFor(NpcActor a, string clip, string need)
        {
            Set s = a != null ? Of(a.Def) : null;
            if (s == null) return false;
            string entry = clip + "@" + need;
            return System.Array.IndexOf(s.Moves, entry) >= 0 || System.Array.IndexOf(s.MovesSit, entry) >= 0;
        }

        // what a move of its own may need now («moves: clip@<need>»), each from the module that knows it (StoryModules: «carry» — its own thing on its arm)
        internal static readonly Dictionary<string, System.Func<NpcActor, bool>> Needs = new Dictionary<string, System.Func<NpcActor, bool>>();

        internal static void Wire()
        {
            foreach (string w in Words) NpcDefs.Words.Add(w);
            NpcActor.Unloaded.Add(a => Last.Remove(a));
        }
    }
}
#endif
