namespace HuginWhispers
{
    // The door for the ravens' voice. RavenWatch itself never starts or touches any program: a voice engine — another
    // plugin, the RavenWatch_Voice package — connects here once (RavenVoiceApi.Connect, from its Awake), is handed the
    // lines as text and gives their sound back. Without one every line is a card, as always. Who starts what, where its
    // files are and how it makes the sound is all the engine's own business.
    // The voice package is built against this door once and must keep working with every later RavenWatch: never change
    // or remove what is here (names, parameters, namespace), only add. Together with the frozen dll name in rw_build.ps1.
    public interface IRavenVoiceEngine
    {
        // the language its voice speaks, as the game names it ("Ukrainian"): it is used only for lines in that language
        string Language { get; }
        // get ready to speak (it may load a while: call RavenVoiceApi.Ready when done); false: it cannot (tell why with Lost)
        bool Start(int threads);
        // started and still able to speak
        bool Running { get; }
        // make the sound of a line — kind: "h" Hugin, "m" Munin, "n" plain (the Guide); voice and seed as RavenWatch picks
        // them — and answer later with RavenVoiceApi.Result(id, …). false: lost, no answer will come
        bool Say(int id, string kind, int voice, int seed, string text);
        // that line is not wanted any more (skip it if it is not made yet)
        void Drop(int id);
        // stop and free everything (switched off, another language, the game quits); Start may come again later
        void Stop();
    }

    // What a voice engine calls. Ready, Result and Lost may be called from any thread.
    public static class RavenVoiceApi
    {
        public const int Version = 1;

        // a voice engine connects (one at a time: a second one is refused); by: who it is, for the log
        public static bool Connect(IRavenVoiceEngine engine, string by) { return RavenVoice.Connect(engine, by); }

        // the engine has loaded and speaks from now on (info: for the log, e.g. how long it took)
        public static void Ready(string info) { RavenVoice.Post(RavenVoice.MsgKind.Ready, 0, null, 0, info); }

        // the sound of line id: 16-bit mono samples as floats −1…1 at this rate; samples null — it could not be made
        public static void Result(int id, float[] samples, int rate) { RavenVoice.Post(RavenVoice.MsgKind.Result, id, samples, rate, null); }

        // the engine cannot speak any more (its program stopped, it broke): the ravens go on as cards
        public static void Lost(string why) { RavenVoice.Post(RavenVoice.MsgKind.Lost, 0, null, 0, why); }

        // how many voices the engine has (RavenWatch 2.8.0+; tell it once, after Connect). Without it RavenWatch counts on
        // the first 30 (RavenWatch_Voice 1.0–1.1); with more, the ravens get the newer timbres too (RavenVoice.VoiceOf)
        public static void Voices(int count) { if (count > 0) RavenVoice.VoiceCount = count; }
    }
}
