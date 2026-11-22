#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A scene's words for the audio core (Audio). All of it is held by the scene: when it ends or stops, everything is let
    // go in one call (SceneEnd). Played from a later step, the music, beds, mix, mutes and ear before it are put at once (Setup).
    //   music <name> [fade] [once] [keep N] [rest N] [vol V] [wait] [all]   ours or the game's; nothing else plays over it. keep: rings on N s after the scene
    //                                                              (with once: till its track is done); rest: then N s of real silence before the game's music
    //   music off [fade] [all]                                     real silence; the game puts nothing in
    //   music game [fade] [all]                                    the game's own music back, mid-scene
    //   bed <name> [place|player|npc.x|bird] [fade] [vol V] [range R] [pitch P] [2d] [wet] [follow] [all] · bed off [name] [fade] [all]
    //                                                              a long loop under the scene; with a place it is heard there
    //   mix world|ambient|music|voice <0..1> [fade] [all] · mix reset [fade] [all]   how loud the rest of the world is
    //   mute <pattern> [all] · unmute <pattern>|all [all]          a game sound by name: sfx_footstep*, *fire*
    //   ear camera|player [fade]                                   where the sound is heard from: the shot or the player
    //   sound <name> [place|player|npc.x|bird] [vol V] [range R] [pitch P] [2d] [wet] [follow] [wait]   a one-shot, heard there
    //         … [delay S] [fadein S] [skip S]                      it starts S seconds after its step (the scene goes on meanwhile) · comes up
    //                                                              from silence over S · its first S seconds left out
    //   sound stop <name>|all [fade]                               one still ringing
    //   sound speech_khm npc.heid                                  a wordless voice sound (a name on «speech_») at a person is said in that
    //                                                              person's own timbre and pitch (NpcTimbre), from its head; one with no timbre, or not
    //                                                              there, is silent. At the catalogue's loudness (vol 1): a scene's sound is an accent,
    //                                                              its talk says the same sounds at half of that (NpcSpeech.SoundVol)
    // «all»: in every game of the world, round each player (let go when the scene ends; a game that never hears it: AirTerm).
    // A «sound» with a place is heard by whoever is within StoryScenes.Seen, unless the scene is «alone: yes», «sound stop» there
    // too, and a scene cut off stops them there as here; music, beds, the mix, mutes and the ear are this player's own and, by the same
    // word, of each one who watches the scene in its picture (StoryWatch, WatchStep).
    internal static class StoryAudio
    {
        private const string Rpc = "RavenWatch_Audio";
        internal static float AirTerm = 600f;
        private static bool _sentAll, _sentSound;
        private static readonly System.Collections.Generic.Dictionary<string, string> Said = new System.Collections.Generic.Dictionary<string, string>(); // («<voice sound> <person>» of the scene → the name it was played by, in that person's timbre: «wait» and «sound stop» find it)

        // the name a «sound» step's sound was played by (a person's voice sound: in its timbre)
        private static string Played(string[] w) { string its; return Said.TryGetValue(w[1] + " " + PlaceOf(w), out its) ? its : w[1]; }

        private static readonly string[] SoundWords = { "vol", "range", "pitch", "2d", "wet", "follow", "wait", "delay", "fadein", "skip" };
        private static readonly string[] BedWords = { "vol", "range", "pitch", "2d", "wet", "follow", "all" };

        internal static void RegisterRpcs()
        {
            ZRoutedRpc.instance.Register<string>(Rpc, (from, line) =>
            {
                if (from == ZDOMan.GetSessionID()) return; // (it comes back here too: done already)
                try { Heard(line); } catch (Exception e) { Story.Log("audio: " + e.Message); }
            });
        }

        private static float F(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }

        // the number after a word, if any
        private static float After(string[] w, string word, float def)
        {
            int i = Array.IndexOf(w, word);
            return i >= 0 && i + 1 < w.Length ? F(w[i + 1], def) : def;
        }

        private static bool Has(string[] w, string word) { return Array.IndexOf(w, word) >= 0; }

        // the first bare number from a place on: the fade
        private static float Fade(string[] w, int from, float def)
        {
            for (int i = from; i < w.Length; i++)
            {
                if (w[i] == "keep" || w[i] == "rest" || w[i] == "vol" || w[i] == "range" || w[i] == "pitch") { i++; continue; }
                float v;
                if (float.TryParse(w[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return Mathf.Clamp(v, 0f, 60f);
            }
            return def;
        }

        // a one-shot's own time: when it starts, how it comes up, what of its start is left out (the same words from another game: «snd»)
        private static void Times(SoundAsk ask, float delay, float fadeIn, float skip)
        {
            ask.Delay = Mathf.Clamp(delay, 0f, 60f); ask.FadeIn = Mathf.Clamp(fadeIn, 0f, 60f); ask.Skip = Mathf.Clamp(skip, 0f, 600f);
        }

        private static AudioMix MixOf(string word)
        {
            return word == "ambient" ? AudioMix.Ambient : word == "music" ? AudioMix.Music : word == "voice" ? AudioMix.Voice : AudioMix.World;
        }

        // the word itself: this game, for this owner
        internal static void Do(string owner, string[] w, bool setup)
        {
            if (w.Length < 2) return; // (it may come from another game; a scene's own lines were checked when read)
            float fadeDef = setup ? 0f : Audio.MusicFade;
            switch (w[0])
            {
                case "music":
                    if (w[1] == "game") { Audio.MusicOff(owner, setup ? 0f : Fade(w, 2, Audio.MusicFade)); return; }
                    bool ok = Audio.Music(owner, AudioLayer.Scene, w[1] == "off" ? null : w[1], setup ? 0f : Fade(w, 2, fadeDef), 0f, Mathf.Clamp01(After(w, "vol", 1f)), Has(w, "once"), Mathf.Clamp(After(w, "keep", 0f), 0f, 600f), null, Mathf.Clamp(After(w, "rest", 0f), 0f, 300f));
                    if (!ok) Note("music: нема такої музики «" + w[1] + "»");
                    return;
                case "bed":
                    if (w[1] == "off") { Audio.BedOff(owner, w.Length > 2 && !IsNum(w[2]) && w[2] != "all" ? w[2] : null, setup ? 0f : Fade(w, 2, fadeDef)); return; }
                    if (!BedNow(owner, w, setup ? 0f : Fade(w, 2, fadeDef))) Note("bed: нема такого звуку «" + w[1] + "»");
                    return;
                case "mix":
                    if (w[1] == "reset") { foreach (AudioMix m in new[] { AudioMix.World, AudioMix.Ambient, AudioMix.Music, AudioMix.Voice }) Audio.Mix(owner, m, 1f, setup ? 0f : Fade(w, 2, fadeDef)); return; }
                    Audio.Mix(owner, MixOf(w[1]), w.Length > 2 ? F(w[2], 1f) : 1f, setup ? 0f : Fade(w, 3, fadeDef));
                    return;
                case "mute": Audio.Mute(owner, w[1]); return;
                case "unmute": Audio.Unmute(owner, w[1] == "all" ? null : w[1]); return;
                case "ear": Audio.Ear(owner, w[1] == "camera", setup ? 0f : Fade(w, 2, 1f)); return;
            }
        }

        // a bed's words: where it is heard, how
        private static bool BedNow(string owner, string[] w, float fade)
        {
            float vol = Mathf.Clamp01(After(w, "vol", 1f));
            Player p = Player.m_localPlayer;
            string place = BedPlaceOf(w);
            Vector3 at = Vector3.zero;
            if (place != null && p != null)
            {
                at = p.transform.position + Vector3.up * 1.4f;
                if (place != "player" && !StoryWhere.Find(place, out at)) { Note("bed: нема місця «" + place + "» — грає без місця"); place = null; }
            }
            else place = null;
            SoundAsk ask = new SoundAsk
            {
                Range = Mathf.Max(0f, After(w, "range", 0f)), Pitch = Mathf.Max(0f, After(w, "pitch", 0f)), Flat = place == null || Has(w, "2d"), Wet = Has(w, "wet"), Owner = owner,
                Follow = place != null && Has(w, "follow") ? StoryScenes.FollowOf(place) : null
            };
            return Audio.Bed(owner, w[1], vol, fade, ask, at);
        }

        // the word of a «bed» that names a place (null: none — the storycheck reads it the same way)
        internal static string BedPlaceOf(string[] w)
        {
            if (w.Length < 3 || w[1] == "off" || IsNum(w[2]) || Array.IndexOf(BedWords, w[2]) >= 0) return null;
            return w[2];
        }

        private static bool IsNum(string s) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }

        // a line from another game
        private static void Heard(string line)
        {
            if (ZNet.instance == null || ZNet.instance.IsDedicated() || AudioMan.instance == null) return; // (a server has nobody to hear it)
            string[] w = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return;
            if (w[0] == "end") { bool broken = w.Length > 1 && w[1] == "broken"; Audio.End(Audio.Air, broken ? Audio.BrokenFade : Audio.EndFade, broken); return; }
            if (w[0] == "stop") { if (w.Length > 2) Audio.StopSounds(Audio.Air, w[1] != "all" ? w[1] : null, Mathf.Clamp(F(w[2], 0.5f), 0f, 60f)); return; } // stop <name>|all fade
            if (w[0] == "snd") // snd <name> x y z vol range pitch wet [what it follows] [delay=S] [fadein=S] [skip=S] [own=N]
            {
                Player me = Player.m_localPlayer;
                if (me == null || w.Length < 9) return;
                Vector3 at = new Vector3(F(w[2], 0f), F(w[3], 0f), F(w[4], 0f));
                if (Vector3.Distance(me.transform.position, at) > StoryScenes.Seen) return;
                SoundAsk heard = new SoundAsk { Volume = Mathf.Clamp(F(w[5], 1f), 0f, 2f), Range = Mathf.Max(0f, F(w[6], 0f)), Pitch = Mathf.Clamp(F(w[7], 0f), 0f, 4f), Wet = w[8] == "1", Owner = Audio.Air };
                float delay = 0f, fadeIn = 0f, skip = 0f;
                for (int i = 9; i < w.Length; i++)
                {
                    if (w[i].StartsWith("delay=", StringComparison.Ordinal)) delay = F(w[i].Substring(6), 0f);
                    else if (w[i].StartsWith("fadein=", StringComparison.Ordinal)) fadeIn = F(w[i].Substring(7), 0f);
                    else if (w[i].StartsWith("skip=", StringComparison.Ordinal)) skip = F(w[i].Substring(5), 0f);
                    else if (w[i].StartsWith("own=", StringComparison.Ordinal)) { uint own; if (uint.TryParse(w[i].Substring(4), out own)) heard.Own = own; }
                    else if (i == 9) heard.Follow = StoryScenes.FollowOf(w[i]);
                }
                Times(heard, delay, fadeIn, skip);
                Audio.Sound(w[1], at, heard);
                return;
            }
            if (w.Length > 1 && (w[0] == "music" || w[0] == "bed" || w[0] == "mix" || w[0] == "mute" || w[0] == "unmute")) { Audio.AirLease(AirTerm); Do(Audio.Air, w, false); }
        }

        private static void Send(string line)
        {
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, line);
        }

        // ---------------------------------------------------------------- the steps

        private static string[] Bare(string[] w, out bool all)
        {
            all = w[w.Length - 1] == "all";
            if (!all) return w;
            string[] r = new string[w.Length - 1];
            Array.Copy(w, r, r.Length);
            return r;
        }

        private static void Step(Player p, StoryScenes.Step st)
        {
            bool all;
            string[] w = Bare(st.W, out all);
            Do(Audio.Scene, w, false);
            if (all && w[0] != "ear") { Send(string.Join(" ", w)); _sentAll = true; }
        }

        private static void SetupStep(Player p, StoryScenes.Step st)
        {
            bool all;
            Do(Audio.Scene, Bare(st.W, out all), true);
        }

        // the same word on the game of one who watches the scene (StoryWatch): held by «scene» there as here, let go when he is out of its picture.
        // «all» is everyone's already (the air's way), and nothing is sent on from there. A place's word «player» is his own there
        private static void WatchStep(StoryScenes.Step st, bool atOnce)
        {
            bool all;
            string[] w = Bare(st.W, out all);
            if (!all || w[0] == "ear") Do(Audio.Scene, w, atOnce);
        }

        // the word of a «sound» that names a place (null: none — the storycheck reads it the same way)
        internal static string PlaceOf(string[] w)
        {
            if (w.Length < 3 || w[1] == "stop" || Array.IndexOf(SoundWords, w[2]) >= 0) return null;
            return w[2];
        }

        private static void SoundStep(Player p, StoryScenes.Step st)
        {
            string[] w = st.W;
            StoryScenes.SceneDef s = StoryScenes.Current;
            if (w[1] == "stop")
            {
                string which = w.Length > 2 ? w[2] : "all";
                float stopFade = w.Length > 3 ? Mathf.Clamp(F(w[3], 0.5f), 0f, 60f) : 0.5f;
                System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string> { which }; // (a voice sound: every name it was said by here, one a person)
                if (which != "all") foreach (System.Collections.Generic.KeyValuePair<string, string> e in Said) if (e.Key.StartsWith(which + " ", StringComparison.Ordinal) && !names.Contains(e.Value)) names.Add(e.Value);
                foreach (string n in names)
                {
                    Audio.StopSounds(Audio.Scene, n != "all" ? n : null, stopFade);
                    if (_sentSound) Send("stop " + n + " " + N(stopFade));
                }
                return;
            }
            Vector3 at = p.transform.position + Vector3.up * 1.4f;
            string place = PlaceOf(w);
            string name = w[1];
            float own = 0f;
            Transform head = null;
            NpcActor who = null;
            if (NpcTimbre.Voice(name) && place != null && place.StartsWith("npc.", StringComparison.Ordinal) && !place.EndsWith(".thing", StringComparison.Ordinal))
            {
                // a person's own sound: said by that person or not at all — never the plain recording from where the player stands (one not there, one with no timbre)
                who = StoryScenes.Actor(place.Substring(4), p.transform.position);
                if (who == null) { Note("sound: «" + w[1] + "» — " + place + " нема поруч: мовчить"); return; }
                if (!NpcTimbre.For(who, name, out name, out own)) { Note("sound: «" + w[1] + "» — у " + place + " нема свого тембру чи цього звуку в ньому: мовчить"); return; }
                Said[w[1] + " " + place] = name;
                head = who.transform; at = who.transform.position + Vector3.up * 1.6f; // (from its head, and with it: a person's sound is the person's)
            }
            else if (place != null && place != "player" && !StoryWhere.Find(place, out at)) { Note("sound: нема місця «" + place + "» — грає біля гравця"); at = p.transform.position + Vector3.up * 1.4f; place = null; }
            SoundAsk ask = new SoundAsk
            {
                Volume = Mathf.Clamp(After(w, "vol", 1f), 0f, 2f), Range = Mathf.Max(0f, After(w, "range", 0f)), Pitch = Mathf.Max(0f, After(w, "pitch", own)),
                Flat = Has(w, "2d"), Wet = Has(w, "wet"), Owner = Audio.Scene,
                Follow = head != null ? head : Has(w, "follow") ? StoryScenes.FollowOf(place) : null
            };
            Times(ask, After(w, "delay", 0f), After(w, "fadein", 0f), After(w, "skip", 0f));
            if (who != null) ask.Own = NpcTimbre.Takes(who); // (its own takes, as in its talk)
            if (!Audio.Sound(name, at, ask)) { Note("sound: «" + name + "» — нема такого звуку в грі"); return; }
            if (ask.Flat || AudioBank.Find(name).Flat || (s != null && s.Alone)) return; // (one's own, or nobody else here to hear it)
            _sentSound = true;
            Send("snd " + name + " " + N(at.x) + " " + N(at.y) + " " + N(at.z) + " " + N(ask.Volume) + " " + N(ask.Range) + " " + N(ask.Pitch) + " " + (ask.Wet ? "1" : "0") + (ask.Follow != null && place != null && place != "player" ? " " + place : "") // (following the player: on another game that word is that game's own player)
                + (ask.Delay > 0f ? " delay=" + N(ask.Delay) : "") + (ask.FadeIn > 0f ? " fadein=" + N(ask.FadeIn) : "") + (ask.Skip > 0f ? " skip=" + N(ask.Skip) : "") + (ask.Own != 0u ? " own=" + ask.Own : ""));
        }

        private static string N(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        internal static void Steps()
        {
            StoryScenes.Register("music", new StoryScenes.StepKind(StoryScenes.StepSeen.Screen)
            {
                Min = 1, Watch = WatchStep, Usage = "music <назва> [с] [once] [keep N] [rest N] [vol V] [wait] [all] · music off|game [с] [all]",
                Begin = Step, Setup = SetupStep, Busy = (p, st) => Has(st.W, "wait") && Audio.MusicWaiting
            });
            StoryScenes.Register("bed", new StoryScenes.StepKind(StoryScenes.StepSeen.Screen)
            {
                PlaceOf = BedPlaceOf,
                Min = 1, Watch = WatchStep, Usage = "bed <назва> [місце|player|npc.x|bird] [с] [vol V] [range R] [pitch P] [2d] [wet] [follow] [all] · bed off [назва] [с] [all]",
                Begin = Step, Setup = SetupStep, Busy = (p, st) => false
            });
            StoryScenes.Register("mix", new StoryScenes.StepKind(StoryScenes.StepSeen.Screen)
            {
                Min = 1, Watch = WatchStep, Usage = "mix world|ambient|music|voice <0..1> [с] [all] · mix reset [с] [all]", Begin = Step, Setup = SetupStep, Busy = (p, st) => false,
                Check = st => Array.IndexOf(new[] { "world", "ambient", "music", "voice", "reset" }, st.W[1]) < 0 ? "mix: невідоме «" + st.W[1] + "»" : st.W[1] != "reset" && (st.W.Length < 3 || !IsNum(st.W[2])) ? "mix " + st.W[1] + ": скільки? 0..1" : null
            });
            StoryScenes.Register("mute", new StoryScenes.StepKind(StoryScenes.StepSeen.Screen)
            {
                Min = 1, Watch = WatchStep, Usage = "mute <шаблон: a, a*, *a, *a*> [all]", Begin = Step, Setup = SetupStep, Busy = (p, st) => false,
                Check = st => st.W[1].Trim('*').IndexOf('*') >= 0 ? "mute: зірочка лише на краях (a, a*, *a, *a*)" : null
            });
            StoryScenes.Register("unmute", new StoryScenes.StepKind(StoryScenes.StepSeen.Screen) { Min = 1, Watch = WatchStep, Usage = "unmute <шаблон>|all [all]", Begin = Step, Setup = SetupStep, Busy = (p, st) => false }); // (the word written out, as every step's: storycheck reads the words from these lines)
            StoryScenes.Register("ear", new StoryScenes.StepKind(StoryScenes.StepSeen.Screen)
            {
                Min = 1, Watch = WatchStep, Usage = "ear camera|player [с]", Begin = Step, Setup = SetupStep, Busy = (p, st) => false,
                Check = st => st.W[1] != "camera" && st.W[1] != "player" ? "ear camera|player" : null
            });
            StoryScenes.Register("sound", new StoryScenes.StepKind(StoryScenes.StepSeen.Shared)
            {
                PlaceOf = PlaceOf,
                Min = 1, Usage = "sound <назва|sfx_…> [місце|player|npc.x|bird] [vol V] [range R] [pitch P] [2d] [wet] [follow] [wait] [delay С] [fadein С] [skip С] · sound stop <назва>|all [с]",
                Begin = SoundStep,
                Busy = (p, st) => Has(st.W, "wait") && st.W[1] != "stop" && Audio.SoundOn(Audio.Scene, Played(st.W))
            });
        }

        private static void Note(string s) { StoryScenes.Note(s); }

        // a scene begins: its files read now, so its steps find them there
        internal static void Prepare(StoryScenes.SceneDef s)
        {
            foreach (StoryScenes.Step st in s.Steps)
            {
                if (st.W.Length < 2 || (st.W[0] != "music" && st.W[0] != "bed" && st.W[0] != "sound")) continue;
                if (st.W[0] == "sound" && NpcTimbre.Voice(st.W[1]) && (PlaceOf(st.W) ?? "").StartsWith("npc.", StringComparison.Ordinal)) continue; // (said in a person's own timbre: which files those are is known when it is said)
                AudioBank.Prepare(st.W[1], st.W[0] != "sound");
            }
        }

        // it ended or stopped: all it held let go (the one place: StoryScenes.Stop and Done)
        internal static void SceneEnd(bool broken)
        {
            Audio.End(Audio.Scene, broken ? Audio.BrokenFade : Audio.EndFade, broken);
            Said.Clear();
            if (_sentAll || (broken && _sentSound)) Send(broken ? "end broken" : "end");
            _sentAll = false; _sentSound = false;
        }

        // the author's window: the same words, held by the window until «Скинути»
        internal static void Panel(string line, bool all)
        {
            string[] w = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length < 2) return;
            Do(Audio.Panel, w, false);
            if (all) Send(line);
        }

        internal static void PanelEnd(bool all)
        {
            Audio.End(Audio.Panel, 1f, true);
            if (all) Send("end broken");
        }

        internal static void PanelSound(string name)
        {
            Player p = Player.m_localPlayer;
            if (p != null) Audio.Sound(name, p.transform.position + Vector3.up * 1.4f, new SoundAsk { Owner = Audio.Panel });
        }
    }
}
#endif
