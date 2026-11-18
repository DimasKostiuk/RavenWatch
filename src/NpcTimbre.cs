#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A person's timbre: in whose voice it hums, sighs and chuckles — the wordless sounds of its talk (NpcSpeech) and of a scene («sound
    // speech_khm npc.heid»: StoryAudio). The sounds were recorded by one voice (the catalogue's names on «speech_»); the author's tool makes
    // every timbre of them (tools/sound.py, «timbres»): the same takes in another voice, under the names «<sound>__<timbre>». So people sound
    // apart with no voice package at all, and the package is to speak with the same timbre later (the user 2026-11-13: «a card of the sound
    // on the character … the people already feel different by their short sounds; the voice-over comes on top, with the same voice»).
    // A timbre is a line of the sound files, «@timbre <name> …» (assets/story/sounds/timbres.txt). What the game reads of it:
    //   for=man|woman        whose voice it is          age=young,grown,old   whom it suits (a settler has an age; one written by hand: grown)
    //   pitch=0.95..1.05     within these each person has a pitch of its own (played a little faster and higher, or slower and lower)
    //   only                 nobody gets it but the one whose file says «timbre: <name>» (Torvald's)
    //   rec                  a voice recorded by another person (tools/sound.py, «voice»): its own files, under «<sound>__<timbre>» too
    //   hero                 the main hero's voice — the player's own, never dealt to a person (the user 2026-11-16: «нову сущность для
    //                        головного героя, щоб він теж бурмотів своїм голосом … рандомно присвоєний з цього пулу»): OfHero
    // The rest of the line (of, like, tone, throat, air, tilt, tremor) is the tool's; a line with none of them and no «rec» is the first
    // recording itself, and its sounds keep their plain names.
    // Which one a person has: its file's «timbre: <name> [pitch]», else the one it keeps, else one of those that suit it, by its seed. None
    // suits it (no woman's timbre written): it is silent, never another's voice.
    // Kept for good (the user 2026-11-13: «we add new voices later and new people take them»; his rule for the ravens' timbres before, the
    // same: a voice once had is never picked anew): the first pick is written into the person's locker (KTimbre, where it is run), and from
    // then on that name is its timbre. A voice added to the files later goes to people born later — and to one whose kept timbre is no
    // longer written. Until the kept name reaches another game, that game works out the same pick from the same seed and files.
    // The one way round «for good» is the author's own word, a line «@pool <number>» of the same files — the pool's generation (the user
    // 2026-11-13, the pool remade of two recordings: «і старим нпс теж тоді заміни при збірці новій»). A timbre is kept with the
    // generation it was picked in («r1_low@2»; a bare name is generation 1); kept in another generation than the files say now, it is
    // picked anew from the pool as it is, once, and that one kept. A person whose file names its timbre is not touched by it.
    // The hero (OfHero): one of the «hero» timbres of the player's sex, by chance, once — kept in the character (its customData, KHero:
    // «<timbre>@<generation> <pitch>»), so the same voice after a relog and in every world; a kept one no longer written, of the other sex,
    // or of another generation of the pool (@pool) is picked anew, as a settler's is. None of the player's sex written: he is silent.
    // Cost: the lines are read once, and anew when the author reads the sound files again (AudioBank.Again → Forget); a person's card
    // is worked out once a line it says or hears (NpcSpeech), never each frame.
    internal static class NpcTimbre
    {
        internal const string Prefix = "speech_", Mark = "__", LineWord = "@timbre", PoolWord = "@pool";
        private const char GenMark = '@'; // <timbre>@<generation> in the locker (no timbre's name has it)
        internal static readonly int KTimbre = "RavenWatch_npcTimbre".GetStableHashCode(); // string, in its locker: the timbre it keeps

        private sealed class Kind
        {
            public string Name = "";
            public bool Woman, Only, Hero, Plain = true;
            public readonly bool[] Ages = { false, false, false }; // young · grown · old (NpcSettlers.Look.Age)
            public float Lo = 0.95f, Hi = 1.05f;
        }

        // what a person sounds like: the timbre and its own pitch within it
        internal sealed class Card { public string Timbre = ""; public float Pitch = 1f; public bool Plain; }

        private static Dictionary<string, Kind> _all;
        private static readonly List<Kind> Shared = new List<Kind>(); // (in the files' order: a seed picks by place)
        private static readonly List<Kind> Heroes = new List<Kind>(); // (the «hero» ones: the player's, OfHero)
        internal const string KHero = "RavenWatch_heroVoice"; // the character's customData: the hero's timbre «<timbre>@<generation> <pitch>»
        private static readonly string[] ToolWords = { "of", "like", "tone", "throat", "air", "tilt", "tremor", "creak" };
        private static int _pool = 1; // the pool's generation («@pool <n>»; none written: 1)

        internal static void Forget() { _all = null; }

        private static Dictionary<string, Kind> Table()
        {
            if (_all != null) return _all;
            _all = new Dictionary<string, Kind>();
            Shared.Clear(); Heroes.Clear();
            _pool = 1;
            foreach (KeyValuePair<string, string> f in StoryFiles.All(AudioBank.Kind, null, false))
                foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(f.Value))
                {
                    string[] w = ln.Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (w.Length >= 2 && w[0] == PoolWord)
                    {
                        int g;
                        if (int.TryParse(w[1], NumberStyles.None, CultureInfo.InvariantCulture, out g) && g >= 1) _pool = Mathf.Max(_pool, g);
                        else AudioBank.LogOnce("timbre pool " + w[1], "«@pool " + w[1] + "»: the pool's generation is a whole number from 1");
                        continue;
                    }
                    if (w.Length < 2 || w[0] != LineWord) continue;
                    Kind k = new Kind { Name = w[1].ToLowerInvariant() };
                    bool anyAge = false, whose = false;
                    for (int i = 2; i < w.Length; i++)
                    {
                        int eq = w[i].IndexOf('=');
                        string key = eq > 0 ? w[i].Substring(0, eq) : w[i], val = eq > 0 ? w[i].Substring(eq + 1) : "";
                        if (key == "only") k.Only = true;
                        else if (key == "hero") k.Hero = true;
                        else if (key == "for") { k.Woman = val == "woman"; whose = true; if (val != "man" && val != "woman") AudioBank.LogOnce("timbre for " + k.Name, "timbre «" + k.Name + "»: for=man or for=woman, not «" + val + "»"); }
                        else if (key == "age") foreach (string a in val.Split(',')) { int at = a == "young" ? 0 : a == "grown" ? 1 : a == "old" ? 2 : -1; if (at >= 0) { k.Ages[at] = true; anyAge = true; } else AudioBank.LogOnce("timbre age " + k.Name + a, "timbre «" + k.Name + "»: age is young, grown, old — not «" + a + "»"); }
                        else if (key == "pitch")
                        {
                            int dots = val.IndexOf("..", StringComparison.Ordinal);
                            float lo = F(dots >= 0 ? val.Substring(0, dots) : val, 1f), hi = F(dots >= 0 ? val.Substring(dots + 2) : val, 1f);
                            k.Lo = Mathf.Clamp(Mathf.Min(lo, hi), 0.7f, 1.4f); k.Hi = Mathf.Clamp(Mathf.Max(lo, hi), 0.7f, 1.4f);
                        }
                        else if (key == "rec" || Array.IndexOf(ToolWords, key) >= 0) k.Plain = false;
                        else AudioBank.LogOnce("timbre word " + k.Name + key, "timbre «" + k.Name + "»: no word «" + w[i] + "» (for= age= pitch= only hero rec; the tool's: like= tone= throat= air= tilt= tremor= creak=)");
                    }
                    if (!anyAge) k.Ages[0] = k.Ages[1] = k.Ages[2] = true;
                    if (!k.Only && !whose) { AudioBank.LogOnce("timbre whose " + k.Name, "timbre «" + k.Name + "»: neither for=man|woman nor «only»: nobody would get it — left out"); continue; }
                    if (_all.ContainsKey(k.Name)) { AudioBank.LogOnce("timbre twice " + k.Name, "timbre «" + k.Name + "» is written twice: the first counts"); continue; }
                    _all[k.Name] = k;
                    if (k.Hero) Heroes.Add(k);
                    else if (!k.Only) Shared.Add(k);
                }
            return _all;
        }

        private static float F(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }

        internal static bool Known(string name) { return !string.IsNullOrEmpty(name) && Table().ContainsKey(name); }
        internal static bool HeroOnly(string name) { Kind k; return !string.IsNullOrEmpty(name) && Table().TryGetValue(name, out k) && k.Hero; }

        // a wordless voice sound by its plain name (speech_hm): one a person says in its own timbre
        internal static bool Voice(string sound) { return !string.IsNullOrEmpty(sound) && sound.StartsWith(Prefix, StringComparison.Ordinal) && sound.IndexOf(Mark, StringComparison.Ordinal) < 0; }

        private static float Rnd(ref uint s) { if (s == 0u) s = 0x6D2B79F5u; s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 16777216f; }

        // the person's card (null: no timbre suits it — it stays silent)
        internal static Card Of(NpcActor a)
        {
            if (a == null) return null;
            Dictionary<string, Kind> all = Table();
            ZDO z = a.Zdo;
            uint seed = (z != null ? NpcKit.Seed(z) : 0u) ^ 0x7131B2E5u; // (its own run of chances: not the one its gait or its speech draws from)
            float pickRoll = Rnd(ref seed), pitchRoll = Rnd(ref seed);   // (both taken here: its pitch is the same whether its timbre is picked now or was kept before — taken one after
                                                                         // the other only when picking, a kept timbre got the pick's number for its pitch: another pitch after a relog)
            NpcDef def = a.Def;
            Kind k = null;
            if (def != null && def.Timbre.Length > 0) all.TryGetValue(def.Timbre, out k); // (a name that is none was said when its file was read: NpcDefs)
            if (k != null && k.Hero) { AudioBank.LogOnce("timbre hero " + k.Name, "timbre «" + k.Name + "» is the hero's voice (hero): a person is not given it — another of the pool"); k = null; }
            if (k == null && z != null)
            {
                string kept = NpcLocker.Text(z, KTimbre);
                int mark = kept.IndexOf(GenMark), gen = 1;
                if (mark >= 0) { if (!int.TryParse(kept.Substring(mark + 1), NumberStyles.None, CultureInfo.InvariantCulture, out gen)) gen = 0; kept = kept.Substring(0, mark); }
                if (kept.Length > 0 && gen == _pool) all.TryGetValue(kept, out k); // (the one it keeps; no longer written, or kept in another generation of the pool: picked anew below, and that one kept)
                if (k != null && !k.Only && k.Woman != a.Female) k = null; // (kept while its look was not known here yet, or the line's sex changed: not a man's voice on a woman for good)
                if (k != null && k.Hero) k = null; // (a hero's voice is the player's alone: one made a hero's after a person took it is taken back)
            }
            if (k == null)
            {
                NpcSettlers.Look look = NpcSettlers.Of(a);
                int age = look != null ? Mathf.Clamp(look.Age, 0, 2) : 1;
                bool woman = a.Female;
                List<Kind> fit = new List<Kind>();
                foreach (Kind s in Shared) if (s.Woman == woman && s.Ages[age]) fit.Add(s);
                if (fit.Count == 0) foreach (Kind s in Shared) if (s.Woman == woman) fit.Add(s); // (none for its age: any of its own sex rather than silence)
                if (fit.Count == 0) return null;
                k = fit[Mathf.Min(fit.Count - 1, (int)(pickRoll * fit.Count))];
                if (z != null && a.Owned) NpcLocker.Put(z, KTimbre, _pool > 1 ? k.Name + GenMark + _pool.ToString(CultureInfo.InvariantCulture) : k.Name); // (once, where it is run: its voice from now on, whatever is added to the files)
            }
            float pitch = def != null && def.Timbre.Length > 0 && def.TimbrePitch > 0f ? def.TimbrePitch : Mathf.Lerp(k.Lo, k.Hi, pitchRoll);
            return new Card { Timbre = k.Name, Pitch = pitch, Plain = k.Plain };
        }

        // the main hero's card: the voice of the lines the player says (his answers in a talk: NpcTalk.Pick). Null: none of his sex written
        internal static Card OfHero(Player p)
        {
            if (p == null) return null;
            Dictionary<string, Kind> all = Table();
            VisEquipment vis = p.GetComponent<VisEquipment>();
            bool woman = vis != null && vis.GetModelIndex() == 1;
            string kept;
            if (!p.m_customData.TryGetValue(KHero, out kept)) kept = "";
            int mark = kept.IndexOf(GenMark), space = kept.IndexOf(' '), gen;
            Kind k = null;
            float pitch = 0f;
            if (mark > 0 && space > mark && int.TryParse(kept.Substring(mark + 1, space - mark - 1), NumberStyles.None, CultureInfo.InvariantCulture, out gen) && gen == _pool
                && all.TryGetValue(kept.Substring(0, mark), out k) && k.Hero && k.Woman == woman)
                pitch = Mathf.Clamp(F(kept.Substring(space + 1), 1f), k.Lo, k.Hi);
            else k = null; // (none kept, no longer written, the other sex, another generation of the pool: picked anew below)
            if (k == null)
            {
                List<Kind> fit = Heroes.FindAll(h => h.Woman == woman);
                if (fit.Count == 0) return null;
                k = fit[UnityEngine.Random.Range(0, fit.Count)]; // (by chance, once: the user's word)
                pitch = UnityEngine.Random.Range(k.Lo, k.Hi);
                p.m_customData[KHero] = k.Name + GenMark + _pool.ToString(CultureInfo.InvariantCulture) + " " + pitch.ToString("0.000", CultureInfo.InvariantCulture);
            }
            return new Card { Timbre = k.Name, Pitch = pitch, Plain = k.Plain };
        }

        // what its locker keeps as its timbre, as written («r1_low@2»; "" — nothing kept, or its locker not here yet): the author's look (NpcSpeech.Report)
        internal static string Kept(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? NpcLocker.Text(z, KTimbre) : ""; }
        internal static int Pool { get { Table(); return _pool; } }

        // the person's own takes (SoundAsk.Own): its number for them, the same on every game and after a restart
        internal static uint Takes(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null) return 0u;
            uint own = NpcKit.Seed(z) ^ 0x2C1B3C6Du;
            return own == 0u ? 1u : own; // (0 means «any take»; not «| 1»: an odd number never begins with an even take of six)
        }

        // the catalogue's name of that sound in that card's timbre
        internal static string Name(Card c, string sound) { return c == null ? null : c.Plain ? sound : sound + Mark + c.Timbre; }

        // «speech_khm» as this person says it: the name to play and its pitch (false: it has no timbre, or that sound is not made in it)
        internal static bool For(NpcActor a, string sound, out string name, out float pitch)
        {
            Card c = Of(a);
            name = Name(c, sound); pitch = c != null ? c.Pitch : 1f;
            return c != null && AudioBank.Has(name);
        }
    }
}
#endif
