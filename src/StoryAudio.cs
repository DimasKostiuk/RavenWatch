#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A scene's words for the audio core (Audio). All of it is held by the scene: when it ends or stops, everything is let
    // go in one call (SceneEnd). Played from a later step, the music, beds, mix, mutes and ear before it are put at once (Setup).
    //   music <name> [fade] [once] [keep N] [vol V] [wait] [all]   ours or the game's; nothing else plays over it. keep: rings on N s after the scene
    //   music off [fade] [all]                                     real silence; the game puts nothing in
    //   music game [fade] [all]                                    the game's own music back, mid-scene
    //   bed <name> [fade] [vol V] [all] · bed off [name] [fade] [all]   a long loop under the scene
    //   mix world|ambient|music|voice <0..1> [fade] [all] · mix reset [fade] [all]   how loud the rest of the world is
    //   mute <pattern> [all] · unmute <pattern>|all [all]          a game sound by name: sfx_footstep*, *fire*
    //   ear camera|player [fade]                                   where the sound is heard from: the shot or the player
    //   sound <name> [place|player|npc.x|bird] [vol V] [range R] [pitch P] [2d] [wet] [follow] [wait]   a one-shot, heard there
    //   sound stop <name>|all [fade]                               one still ringing
    // «all»: in every game of the world, round each player (let go when the scene ends; a game that never hears it: AirTerm).
    // A «sound» with a place is heard by whoever is within StoryScenes.Seen, unless the scene is «alone: yes»; music, beds
    // and the mix are this player's own.
    internal static class StoryAudio
    {
        private const string Rpc = "RavenWatch_Audio";
        internal static float AirTerm = 600f;
        private static bool _sentAll;

        private static readonly string[] SoundWords = { "vol", "range", "pitch", "2d", "wet", "follow", "wait" };

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
                if (w[i] == "keep" || w[i] == "vol") { i++; continue; }
                float v;
                if (float.TryParse(w[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return Mathf.Clamp(v, 0f, 60f);
            }
            return def;
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
                    bool ok = Audio.Music(owner, AudioLayer.Scene, w[1] == "off" ? null : w[1], setup ? 0f : Fade(w, 2, fadeDef), 0f, Mathf.Clamp01(After(w, "vol", 1f)), Has(w, "once"), Mathf.Clamp(After(w, "keep", 0f), 0f, 600f));
                    if (!ok) Note("music: нема такої музики «" + w[1] + "»");
                    return;
                case "bed":
                    if (w[1] == "off") { Audio.BedOff(owner, w.Length > 2 && !IsNum(w[2]) && w[2] != "all" ? w[2] : null, setup ? 0f : Fade(w, 2, fadeDef)); return; }
                    if (!Audio.Bed(owner, w[1], Mathf.Clamp01(After(w, "vol", 1f)), setup ? 0f : Fade(w, 2, fadeDef))) Note("bed: нема такого звуку «" + w[1] + "»");
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

        private static bool IsNum(string s) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }

        // a line from another game
        private static void Heard(string line)
        {
            string[] w = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return;
            if (w[0] == "end") { Audio.End(Audio.Air, Audio.EndFade, false); return; }
            if (w[0] == "snd") // snd <name> x y z vol range pitch wet
            {
                Player me = Player.m_localPlayer;
                if (me == null || w.Length < 9) return;
                Vector3 at = new Vector3(F(w[2], 0f), F(w[3], 0f), F(w[4], 0f));
                if (Vector3.Distance(me.transform.position, at) > StoryScenes.Seen) return;
                Audio.Sound(w[1], at, new SoundAsk { Volume = Mathf.Clamp(F(w[5], 1f), 0f, 2f), Range = F(w[6], 0f), Pitch = F(w[7], 0f), Wet = w[8] == "1", Owner = Audio.Air });
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

        // the word of a «sound» that names a place (null: none — the storycheck reads it the same way)
        internal static string PlaceOf(string[] w)
        {
            if (w.Length < 3 || w[1] == "stop" || Array.IndexOf(SoundWords, w[2]) >= 0) return null;
            return w[2];
        }

        private static void SoundStep(Player p, StoryScenes.Step st)
        {
            string[] w = st.W;
            if (w[1] == "stop")
            {
                Audio.StopSounds(Audio.Scene, w.Length > 2 && w[2] != "all" ? w[2] : null, w.Length > 3 ? Mathf.Clamp(F(w[3], 0.5f), 0f, 60f) : 0.5f);
                return;
            }
            Vector3 at = p.transform.position + Vector3.up * 1.4f;
            string place = PlaceOf(w);
            if (place != null && place != "player" && !StoryWhere.Find(place, out at)) { Note("sound: нема місця «" + place + "» — грає біля гравця"); at = p.transform.position + Vector3.up * 1.4f; place = null; }
            SoundAsk ask = new SoundAsk
            {
                Volume = Mathf.Clamp(After(w, "vol", 1f), 0f, 2f), Range = Mathf.Max(0f, After(w, "range", 0f)), Pitch = Mathf.Max(0f, After(w, "pitch", 0f)),
                Flat = Has(w, "2d"), Wet = Has(w, "wet"), Owner = Audio.Scene,
                Follow = Has(w, "follow") ? StoryScenes.FollowOf(place) : null
            };
            if (!Audio.Sound(w[1], at, ask)) { Note("sound: «" + w[1] + "» — нема такого звуку в грі"); return; }
            StoryScenes.SceneDef s = StoryScenes.Current;
            if (ask.Flat || (s != null && s.Alone)) return; // (one's own, or nobody else here to hear it)
            Send("snd " + w[1] + " " + N(at.x) + " " + N(at.y) + " " + N(at.z) + " " + N(ask.Volume) + " " + N(ask.Range) + " " + N(ask.Pitch) + " " + (ask.Wet ? "1" : "0"));
        }

        private static string N(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        internal static void Steps()
        {
            Action<string, int, string> reg = (word, min, usage) => StoryScenes.Register(word, new StoryScenes.StepKind(StoryScenes.StepSeen.Own) { Min = min, Usage = usage, Begin = Step, Setup = SetupStep, Busy = (p, st) => false });
            StoryScenes.Register("music", new StoryScenes.StepKind(StoryScenes.StepSeen.Own)
            {
                Min = 1, Usage = "music <назва> [с] [once] [keep N] [vol V] [wait] [all] · music off|game [с] [all]",
                Begin = Step, Setup = SetupStep, Busy = (p, st) => Has(st.W, "wait") && Audio.MusicWaiting
            });
            reg("bed", 1, "bed <назва> [с] [vol V] [all] · bed off [назва] [с] [all]");
            StoryScenes.Register("mix", new StoryScenes.StepKind(StoryScenes.StepSeen.Own)
            {
                Min = 1, Usage = "mix world|ambient|music|voice <0..1> [с] [all] · mix reset [с] [all]", Begin = Step, Setup = SetupStep, Busy = (p, st) => false,
                Check = st => Array.IndexOf(new[] { "world", "ambient", "music", "voice", "reset" }, st.W[1]) < 0 ? "mix: невідоме «" + st.W[1] + "»" : st.W[1] != "reset" && (st.W.Length < 3 || !IsNum(st.W[2])) ? "mix " + st.W[1] + ": скільки? 0..1" : null
            });
            StoryScenes.Register("mute", new StoryScenes.StepKind(StoryScenes.StepSeen.Own)
            {
                Min = 1, Usage = "mute <шаблон: a, a*, *a, *a*> [all]", Begin = Step, Setup = SetupStep, Busy = (p, st) => false,
                Check = st => st.W[1].Trim('*').IndexOf('*') >= 0 ? "mute: зірочка лише на краях (a, a*, *a, *a*)" : null
            });
            reg("unmute", 1, "unmute <шаблон>|all [all]");
            StoryScenes.Register("ear", new StoryScenes.StepKind(StoryScenes.StepSeen.Own)
            {
                Min = 1, Usage = "ear camera|player [с]", Begin = Step, Setup = SetupStep, Busy = (p, st) => false,
                Check = st => st.W[1] != "camera" && st.W[1] != "player" ? "ear camera|player" : null
            });
            StoryScenes.Register("sound", new StoryScenes.StepKind(StoryScenes.StepSeen.Shared)
            {
                PlaceOf = PlaceOf,
                Min = 1, Usage = "sound <назва|sfx_…> [місце|player|npc.x|bird] [vol V] [range R] [pitch P] [2d] [wet] [follow] [wait] · sound stop <назва>|all [с]",
                Begin = SoundStep,
                Busy = (p, st) => Has(st.W, "wait") && st.W[1] != "stop" && Audio.SoundOn(Audio.Scene, st.W[1])
            });
        }

        private static void Note(string s) { StoryScenes.Note(s); }

        // a scene begins: its files read now, so its steps find them there
        internal static void Prepare(StoryScenes.SceneDef s)
        {
            foreach (StoryScenes.Step st in s.Steps)
                if (st.W.Length > 1 && (st.W[0] == "music" || st.W[0] == "bed" || st.W[0] == "sound")) AudioBank.Prepare(st.W[1], st.W[0] != "sound");
        }

        // it ended or stopped: all it held let go (the one place: StoryScenes.Stop and Done)
        internal static void SceneEnd(bool broken)
        {
            Audio.End(Audio.Scene, broken ? Audio.BrokenFade : Audio.EndFade, broken);
            if (_sentAll) { _sentAll = false; Send("end"); }
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
            if (all) Send("end");
        }

        internal static void PanelSound(string name)
        {
            Player p = Player.m_localPlayer;
            if (p != null) Audio.Sound(name, p.transform.position + Vector3.up * 1.4f, new SoundAsk { Owner = Audio.Panel });
        }
    }
}
#endif
