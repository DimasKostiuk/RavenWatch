#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A word of the story to a person, and it stands (the user 2026-11-04: «a person given a command does it till the command "free",
    // or another command; globally: there will be many such scenes»). A scene ending, a reload, another game running the person, the
    // person not even in the world yet: the word holds. It is the world's (a key, StoryWorld.Text: every game reads it), and written:
    //   npc heid hold <place[.piece]> [stand]                      be there (a quest's start:/done:, a scene's «do»), looking at it
    //   npc heid hold <place[.piece]> by <place[.piece]> …         …on the side of the thing towards that (by her bed, on the post's side)
    //   npc heid hold <place[.piece]> clip <clip> [from <clip>]    …in that clip of ours («from»: its way in first, then the loop)
    //   npc heid hold <a> [by …] then <b> [by …] [every <s>] clip <clip> from <clip> out <clip>
    //                                                              …at these in turn («out»: its clip's way out before it walks on)
    //   npc heid hold <place[.piece]> here clip <clip> …           …a clip that touches nothing (arms folded): no step up to the thing, where a plain «stand» stops
    //                                                              is enough, and it looks at whom it likes, not at the thing
    //   npc heid hold <place[.piece]> emote <gesture>              …in the game's gesture, held
    //   npc heid free                                              «вільно»: its own day again (a scene's own step too)
    // «By» a thing means beside it: a step out from its edge, on the side the person comes from (StoryWhere.Approach), never its middle.
    // A new «hold» is the next command: it goes on from wherever it is. Over its day and its whims (its Order layer, kept in its ZDO);
    // a scene still takes it (the Scene layer over it) and, over, leaves it to this word. The story's word wins over a player's order.
    // Which of its places: from the world's clock (every game, an owner change, a reload reads the same one), nothing kept.
    // Where it is run (NpcActor.Ticks: StoryModules), each HoldEvery s a person. What its clip is at is read from its body, not only kept:
    // a clip already on it (another game ran it, a reload: its ZDO brings it back) goes on, no way in again.
    internal static class StoryHold
    {
        internal static float ClipNear = 0.4f; // m from its spot, for a clip whose hands work on the thing
        internal static float PlaceFor = 60f; // s at each place, when «every» does not say
        internal static float HoldEvery = 2f, Near = 2.1f, ClipFor = 600f; // s · m there (past where a «go» stops: NpcMind.Reach of its 1.2, or it is told again and again) · s a clip is asked for (again a minute before it ends: its loop's own pose, unseen)
        private const string Whose = "storyhold";
        private static readonly int KHeld = "RavenWatch_npcHeld".GetStableHashCode(); // (in its ZDO: it was given a word — let go when the word is gone, after a reload too)
        private sealed class State { public float Next, Asked; public string Text = "", SpotFor = ""; public bool Clip, Looping, Leaving; public Vector3 Spot; public Word Told; public int Place = -1; public float LeftAt, Seen; }
        private static readonly Dictionary<NpcActor, State> States = new Dictionary<NpcActor, State>(); // session: a body's clock and what its clip is at
        private static string Key(string who) { return "hold_" + who; }

        internal const string Usage = "npc <хто> hold <місце> [by <місце>] [then <місце> [by <місце>]]… [every <с>] [here] [stand|clip <кліп> [from <кліп>] [out <кліп>]|emote <жест>]";
        internal static string Set(string who, string text) { string why = Why(text); if (why != null) return why; StoryWorld.SetText(Key(who), text); return null; }

        // what is wrong with a word (the load check too), null: fine
        internal static string Why(string text)
        {
            Word w = Parse((text ?? "").ToLowerInvariant());
            if (!w.Good) return Usage;
            foreach (Spot s in w.Spots)
            {
                string place = StoryWhere.Why(s.Thing);
                if (place != null) return "npc hold: " + place;
                if (s.By.Length > 0 && StoryWhere.Why(s.By) != null) return "npc hold by: " + StoryWhere.Why(s.By);
            }
            if (w.Kind == "clip" && BodyClips.Find(w.Pose) == null) return "npc hold: нема кліпу «" + w.Pose + "»";
            if (w.From.Length > 0 && BodyClips.Find(w.From) == null) return "npc hold: нема кліпу «" + w.From + "»";
            if (w.Out.Length > 0 && BodyClips.Find(w.Out) == null) return "npc hold: нема кліпу «" + w.Out + "»";
            return null;
        }
        internal static void Clear(string who) { StoryWorld.SetText(Key(who), ""); }
        internal static string Of(string who) { return StoryWorld.Text(Key(who)); }

        private sealed class Spot { public string Thing = "", By = ""; }
        private sealed class Word { public bool Good, Here; public string Text = "", Kind = "stand", Pose = "", From = "", Out = ""; public float Every; public readonly List<Spot> Spots = new List<Spot>(); }
        private static Word Parse(string text)
        {
            string[] w = (text ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            Word r = new Word { Text = text ?? "" };
            if (w.Length == 0) return r;
            int i = 0;
            r.Every = PlaceFor;
            while (true)
            {
                if (i >= w.Length) return r;
                Spot s = new Spot { Thing = w[i++] };
                if (i + 1 < w.Length && w[i] == "by") { s.By = w[i + 1]; i += 2; } // (on which side of the thing it stands: the side towards that)
                r.Spots.Add(s);
                if (i + 1 < w.Length && w[i] == "then") { i++; continue; }
                break;
            }
            if (i + 1 < w.Length && w[i] == "every") { if (!float.TryParse(w[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out r.Every) || r.Every <= 0f) return r; i += 2; }
            if (i < w.Length && w[i] == "here") { r.Here = true; i++; }
            if (w.Length == i || w[i] == "stand") { r.Good = true; return r; }
            if ((w[i] != "clip" && w[i] != "emote") || w.Length < i + 2) return r;
            r.Kind = w[i]; r.Pose = w[i + 1];
            for (i += 2; i + 1 < w.Length; i += 2)
            {
                if (w[i] == "from") r.From = w[i + 1];
                else if (w[i] == "out") r.Out = w[i + 1];
                else return r;
            }
            r.Good = i == w.Length;
            return r;
        }

        internal static void Forget(NpcActor a) { States.Remove(a); }

        internal static void Tick(NpcActor a)
        {
            if (a.Def == null || a.Def.Generated) return; // (a settler is given no word: a base of ten asks nothing here)
            State st;
            if (!States.TryGetValue(a, out st)) States[a] = st = new State();
            else if (Time.time - st.Seen > HoldEvery * 3f) States[a] = st = new State(); // (run by another game meanwhile — this runs only where it is owned: what its clip was at here is stale, read afresh from its body)
            st.Seen = Time.time;
            if (Time.time < st.Next) return;
            st.Next = Time.time + HoldEvery;
            NpcMind m = a.Mind;
            ZDO zdo = a.Zdo;
            if (m == null || zdo == null) return;
            string text = Of(a.Id);
            NpcMind.Plan held = m.Of(NpcMind.Layer.Order);
            bool scene = m.Of(NpcMind.Layer.Scene) != null;
            if (scene) st.Clip = st.Looping = false; // (a scene has it: its clips are the scene's, and its end takes them off — this word's clip is asked again after it)
            if (text != st.Text)
            {
                st.Leaving = false;
                if (st.Clip) { st.Clip = st.Looping = false; a.Order("clip " + (st.Told != null && st.Told.Out.Length > 0 ? st.Told.Out : "none"), NpcMind.Layer.Order, Whose); } // (its clip's word over: by its way out, if it has one)
                if (st.Text.Length > 0) a.Order("look none", NpcMind.Layer.Order, Whose); // (nor does it stare where the old word had it look)
            }
            st.Text = text;
            bool was = zdo.GetBool(KHeld, false);
            if (text.Length == 0)
            {
                if (!was) return;
                zdo.Set(KHeld, false);
                if (held != null) a.Order("free", NpcMind.Layer.Order, Whose); // (its look at the spot goes with it: set by the same owner)
                Story.Log("hold: " + a.Id + " free");
                return;
            }
            if (st.Told == null || st.Told.Text != text) st.Told = Parse(text); // (read once a word: this runs for each person held, every HoldEvery s)
            Word word = st.Told;
            if (!word.Good) return;
            if (!was) { zdo.Set(KHeld, true); Story.Log("hold: " + a.Id + " told: " + text); }
            int want = word.Spots.Count > 1 && ZNet.instance != null ? (int)((long)(ZNet.instance.GetTimeSeconds() / word.Every) % word.Spots.Count) : 0;
            if (want != st.Place && (st.Place < 0 || !m.InTalk) && !Leave(a, st, word, want)) return; // (someone talks to it: its next place waits)
            Spot spot = word.Spots[Mathf.Min(st.Place, word.Spots.Count - 1)];
            Vector3 at;
            if (!StoryWhere.Standing(spot.Thing, out at)) return; // (its place not standing yet: the word waits)
            // by the thing, not in it: a step out from its edge on the side it comes from (a fire: she walked into it and burned; a bed: knelt on
            // it) — reckoned once a word, so it does not slide as she comes
            if (st.SpotFor != text && (spot.Thing.IndexOf('.') < 0 || StoryWhere.Piece(spot.Thing) != null)) { Vector3 by; st.Spot = StoryWhere.StandBy(spot.Thing, Side(spot, a), at, word.Kind == "clip" && !word.Here, out by) ? by : at; st.SpotFor = text; } // (a clip there works with its hands: a step nearer) // (its piece loaded here: else its middle till it is)
            Vector3 look = at + Vector3.up * 0.5f; // (it looks at the thing, not at the spot it stands on: it stared at its own feet, 2026-11-04)
            if (st.SpotFor == text) at = st.Spot;
            float far = Utils.DistanceXZ(a.transform.position, at);
            bool close = word.Kind == "clip" && !word.Here; // (its hands work on the thing: to the very spot)
            if (far > (close ? ClipNear : Near))
            {
                if (word.Kind == "clip" && !scene && OfWord(word, ClipOn(a))) { st.Clip = st.Looping = false; a.Order("clip none", NpcMind.Layer.Order, Whose); } // (a clip brought back by its ZDO, its place another by now: it does not walk bent)
                if (held == null || held.Kind != NpcMind.Task.Go || Utils.DistanceXZ(held.To, at) > 0.3f) a.Order(Go(at, close), NpcMind.Layer.Order, Whose);
                return;
            }
            bool posing = held != null && held.Kind == NpcMind.Task.Pose;
            if (word.Kind == "stand") { if (held == null || posing) a.Order(Go(at, close), NpcMind.Layer.Order, Whose); return; } // (there: idle on its layer; out of the gesture of the word before)
            if (word.Kind == "emote")
            {
                if (held == null || held.Kind != NpcMind.Task.Pose) { StoryProps.SetDown(a, false, true); a.Order("lookat " + V(look), NpcMind.Layer.Order, Whose); a.Order("emote " + word.Pose + " loop", NpcMind.Layer.Order, Whose); } // (a thing of its own off its arm first, here where it stands)
                return;
            }
            // our clip over an idle there (a body thing, not a plan): once, and again only a minute before its term
            if (held == null || posing) a.Order(Go(at, close), NpcMind.Layer.Order, Whose);
            if (scene) return; // (a scene has it: its own clips)
            string on = ClipOn(a);
            if (!st.Clip && on == word.Pose) { st.Clip = st.Looping = true; st.Asked = Time.time - ClipFor; } // (already in it, before this game's first look: its term renewed below, the loop goes on)
            else if (!st.Clip && word.From.Length > 0 && on == word.From) { st.Clip = true; st.Looping = false; st.Asked = Time.time; } // (already on its way in)
            if (!st.Clip) StoryProps.SetDown(a, false, true); // (a word for its arms, here where it stands: a thing of its own off its arm first — it glides to the ground as the clip begins)
            if (!st.Clip && word.From.Length > 0)
            {
                if (!word.Here) a.Order("lookat " + V(look), NpcMind.Layer.Order, Whose);
                a.Order("clip " + word.From, NpcMind.Layer.Order, Whose); // (its way in: held at its end till the loop takes over)
                st.Clip = true; st.Looping = false; st.Asked = Time.time;
                return;
            }
            PAnimClip from = word.From.Length > 0 ? BodyClips.Find(word.From) : null;
            if (st.Clip && !st.Looping && from != null && Time.time - st.Asked < from.Length) return; // (still on its way in)
            if (st.Clip && st.Looping && Time.time - st.Asked < ClipFor - 60f) return;
            if (!word.Here) a.Order("lookat " + V(look), NpcMind.Layer.Order, Whose);
            a.Order("clip " + word.Pose + " " + ClipFor.ToString(CultureInfo.InvariantCulture), NpcMind.Layer.Order, Whose);
            st.Clip = st.Looping = true; st.Asked = Time.time;
        }

        // a scene lets it go: a clip on it that is this word's own stays on (its arms folded in the scene and after it: not dropped and folded again)
        internal static bool KeepsClip(NpcActor a)
        {
            string text = Of(a.Id);
            if (text.Length == 0) return false;
            Word w = Parse(text);
            string on = ClipOn(a);
            return w.Good && w.Kind == "clip" && on != null && (on == w.Pose || on == w.From);
        }

        private static bool OfWord(Word w, string clip) { return clip != null && (clip == w.Pose || clip == w.From || clip == w.Out); }

        // time for its next place: its clip's way out first (true: it may go on to that place)
        private static bool Leave(NpcActor a, State st, Word word, int want)
        {
            if (st.Place >= 0 && st.Clip)
            {
                PAnimClip way = word.Out.Length > 0 ? BodyClips.Find(word.Out) : null;
                if (!st.Leaving)
                {
                    st.Leaving = true; st.LeftAt = Time.time;
                    if (way != null) { a.Order("clip " + word.Out, NpcMind.Layer.Order, Whose); return false; }
                }
                else if (way != null && Time.time - st.LeftAt < way.Length) return false;
                st.Clip = st.Looping = false;
                a.Order("clip none", NpcMind.Layer.Order, Whose);
            }
            if (st.Place >= 0) a.Order("look none", NpcMind.Layer.Order, Whose); // (it does not walk on staring at the place it left)
            st.Place = want; st.Leaving = false; st.SpotFor = "";
            return true;
        }

        // the side of the thing it stands on: towards «by» (Heid by her bed, on the side of the post), else the side it comes from
        private static Vector3 Side(Spot w, NpcActor a) { Vector3 s; return w.By.Length > 0 && StoryWhere.Find(w.By, out s) ? s : a.transform.position; }

        // is a clip of that name (or a part of it) on this person: the scene that takes it out of the clip asks («if npcclip heid heid_search:»)
        internal static bool InClip(NpcActor a, string clip) { string on = a != null ? ClipOn(a) : null; return on != null && (on == clip || on.StartsWith(clip + ".", System.StringComparison.Ordinal)); }

        private static string ClipOn(NpcActor a) { BodyAnimator b = BodyAnimator.Of(a.GetComponent<Character>()); return b != null && b.Holding(NpcBody.ClipSlot, b.ClipIn(NpcBody.ClipSlot)) ? b.ClipIn(NpcBody.ClipSlot) : null; }

        private static string Go(Vector3 at, bool close) { return "go " + V(at) + (close ? " near 0.3 exact" : ""); }

        private static string V(Vector3 v) { return v.x.ToString("0.00", CultureInfo.InvariantCulture) + " " + v.y.ToString("0.00", CultureInfo.InvariantCulture) + " " + v.z.ToString("0.00", CultureInfo.InvariantCulture); }
    }
}
#endif
