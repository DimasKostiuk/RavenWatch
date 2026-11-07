#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Who an NPC is (story-plan.md, «NPC»): a template like the errands'. All of them files, read where StoryFiles says
    // (assets/story/npc/*.txt built in — Гейд, the settler, Odin, the test ones; on the author's machine the mod's own
    // folder, edited in F9 «Вигляд»; an admin's config/RavenWatch/npc/*.txt add or replace them by id).
    //   [npc elder]
    //   name: Старий Торвальд
    //   body: human                   human | odin
    //   model: 0                      0 man · 1 woman
    //   skin: 0.85 0.72 0.6           colours as the game keeps them
    //   haircolor: 0.62 0.28 0.12
    //   hair: Hair7                   the game's hair/beard items (none: no hair)
    //   beard: Beard12
    //   gear: ArmorTunic6 ArmorLeatherLegs HelmetHat1 CapeLox   worn (armour, a cape, things in hands)
    //   health: 200
    //   mortal: no                    yes: it can be hurt and killed (no by default: blows do nothing)
    //   unseen: no                    yes: no monster sees it, it is nobody's foe (a story person under a power's keeping: Гейд,
    //                                 Hel's) — NpcVitals.Unseen
    //   speed: 1                      how fast it walks and runs (×)
    //   walk: 1                       its own stroll, on top of that (×): 0.75 an old man's, 1.15 a brisk youth's
    //   turn: 80                      how fast it turns (deg/s), eased in and out, its feet stepping one by one (BodyTurn); none: the game's own
    //   menu: no                      no round menu on Q, for an admin neither
    //   fight: flee                   its character in a fight (NpcMind.Fight): flee (the default: peaceful, only runs) ·
    //                                 wary (runs, fights back when caught or hit) · defend (a fighter: foes near it or whom it
    //                                 follows, or that hit it) · hunt (a fighter: any foe it senses) · off (does nothing)
    //   greet: Здоров був, {name}.    lines it greets you with ({greet} in its talk; without a talk: said on E)
    //   talk: elder                   its talk on E (NpcTalk; none written: a settler «common», a person written by hand
    //                                 only its greet and «on talk»: the common talk hires, a story person is not for hire)
    //   voice: mixed weather night    its own words unasked (NpcVoice; none written: a settler or one hired speaks, a person written
    //                                 by hand does not): quiet — never · own — only its own themes ([id:theme]) · mixed <groups> — its
    //                                 own first, and the common ones of these groups (NpcVoice.Groups ids, or a theme's own name)
    //   persona: stoop=0.8 build=0.5  its own ways (NpcPersona: walk stoop height build diligence laziness curiosity
    //                                 chrono chatty bravery; nature=<id> its kind of person: NpcNature); what is not written comes from its id
    // Every line is checked as it is read (NpcDefs.Problems, in the log and in «raven npc self»): a word no part knows,
    // a trait, knack, gait or fight that is none — never left out unsaid. A module's own words: NpcDefs.Words (NpcModules).
    //   on near 5: say @greet; emote wave       rules (NpcRules): when → what, see there
    //   on night: sit fire
    //   on day: wander 8 home
    internal sealed class NpcDef
    {
        public string Id = "", Name = "", Body = "human";
        public int Model;
        public Vector3 Skin = new Vector3(0.85f, 0.72f, 0.6f), HairColor = new Vector3(0.45f, 0.34f, 0.22f);
        public string Hair = "", Beard = "";
        public readonly List<string> Gear = new List<string>();
        public float Health = 200f, Speed = 1f, Walk = 1f, Run = 1f; // (Run: × its run only — a body is the player's, and runs as he sprints: Heid ran 13 m/s, 2026-11-04)
        public float Turn;             // «turn: 80»: deg/s it turns at, eased in and out (NpcMind.TurnPace); 0 — the game's own quick turn
        public bool NoMenu;            // «menu: no»: no round menu on Q, for an admin neither (a story person: talked to, or not at all)
        public float Echo;             // «echo: 0.4»: only half there, see-through (NpcFade: the prologue's «Відлуння»); 0 — a body as any
        public bool Mortal;
        public bool Unseen;            // «unseen: yes»: nobody's foe, no monster minds it (NpcVitals.Unseen)
        public string Fight = "flee"; // (fighting is for the fighters: their templates say defend or hunt)
        public string Talk = "";       // its talk as written (NpcTalk; «talk: elder»)
        public string Voice = "";      // «voice: quiet|own|mixed <groups>»: its unasked words (NpcVoice.Speaks); none: as its kind (a settler speaks, a hand-written one not)
        public string TalkId { get { return Talk.Length > 0 ? Talk : Generated ? "common" : ""; } } // the talk it has (""  none: it only greets)
        public bool Settler { get { return TalkId == "common"; } }  // one of the common people (the talk with hiring): needs, jobs, gossip, requests. A person the story wrote by hand is none: it lives in the world, its day and words its own
        public string Story = "";      // «story: …»: a story person's own past (its card, «Хто ти?»); a settler's is made (NpcBackstory)
        public bool Generated;         // «generated: yes»: a settler — its name, look and age made once per person (NpcSettlers)
        public readonly Dictionary<string, float> Knack = new Dictionary<string, float>(); // «knack: cook=1.5 melee=1.3» (NpcKnack)
        public string Gait = "";       // its kind of gait (a name in assets/gaits.txt, shared or its own; none — NpcGait picks from what it is)
        public string Persona = "";    // its own ways as written (NpcPersona; «persona: stoop=0.8 chatty=0.9»), the rest from its id
        public readonly List<string> Greet = new List<string>();
        public readonly List<NpcRule> Rules = new List<NpcRule>();
        public readonly List<string> Keep = new List<string>(); // the lines that are not its look, as written (saved back as they were)
        internal bool Odin { get { return Body == "odin"; } }

        // one trait of its written ways set (the window «Вигляд»: height, build): its «persona:» line kept as one, the other
        // traits in it as written
        internal void SetTrait(string name, float v)
        {
            string put = name + "=" + v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            List<string> pairs = new List<string>();
            bool done = false;
            foreach (string p in Persona.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!p.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) { pairs.Add(p); continue; }
                if (!done) { pairs.Add(put); done = true; }
            }
            if (!done) pairs.Add(put);
            Persona = string.Join(" ", pairs.ToArray());
            int at = Keep.FindIndex(l => l.TrimStart().StartsWith("persona:", StringComparison.OrdinalIgnoreCase));
            if (at >= 0) Keep[at] = "persona: " + Persona; else Keep.Add("persona: " + Persona);
        }

        // the template as a file (the window «Вигляд» saves it): the look written anew, the rest as it was
        internal string Write()
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.Append("[npc ").Append(Id).Append("]\nname: ").Append(Name).Append("\nbody: ").Append(Body).Append('\n');
            if (!Odin)
            {
                sb.Append("model: ").Append(Model).Append('\n');
                sb.Append("skin: ").Append(Skin.x.ToString("0.###", ci)).Append(' ').Append(Skin.y.ToString("0.###", ci)).Append(' ').Append(Skin.z.ToString("0.###", ci)).Append('\n');
                sb.Append("haircolor: ").Append(HairColor.x.ToString("0.###", ci)).Append(' ').Append(HairColor.y.ToString("0.###", ci)).Append(' ').Append(HairColor.z.ToString("0.###", ci)).Append('\n');
                sb.Append("hair: ").Append(Hair.Length > 0 ? Hair : "none").Append('\n');
                sb.Append("beard: ").Append(Beard.Length > 0 ? Beard : "none").Append('\n');
                if (Gear.Count > 0) sb.Append("gear: ").Append(string.Join(" ", Gear.ToArray())).Append('\n');
            }
            foreach (string k in Keep) sb.Append(k).Append('\n');
            return sb.ToString();
        }

        internal static List<NpcDef> ParseAll(string text, string from)
        {
            List<NpcDef> list = new List<NpcDef>();
            NpcDef cur = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("#") || line.Length == 0) continue;
                int note = line.IndexOf(" #", StringComparison.Ordinal); // (a note after the word: «menu: no  # …» read as «no # …» gave Heid the round menu, 2026-11-05)
                if (note > 0) line = line.Substring(0, note).TrimEnd();
                if (line.StartsWith("[npc ") && line.EndsWith("]"))
                {
                    cur = new NpcDef { Id = line.Substring(5, line.Length - 6).Trim() };
                    if (global::HuginWhispers.Story.GoodName(cur.Id)) list.Add(cur);
                    else { Npcs.Log("npc id «" + cur.Id + "» in " + from + " left out (no ; = , + | or spaces)"); cur = null; }
                    continue;
                }
                if (cur == null) continue;
                int c = line.IndexOf(':');
                if (c <= 0) continue;
                string k = line.Substring(0, c).Trim().ToLowerInvariant(), v = line.Substring(c + 1).Trim();
                if (k != "name" && k != "body" && k != "model" && k != "skin" && k != "haircolor" && k != "hair" && k != "beard" && k != "gear") cur.Keep.Add(line);
                if (k.StartsWith("on ")) { NpcRule rule = NpcRule.Parse(k.Substring(3), v); if (rule != null) cur.Rules.Add(rule); else NpcDefs.Problem(cur, from, "правило «" + line + "» не зрозуміле"); continue; }
                string[] w = v.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                switch (k)
                {
                    case "name": cur.Name = v; break;
                    case "body": cur.Body = v.ToLowerInvariant() == "odin" ? "odin" : "human"; break;
                    case "model": cur.Model = Mathf.Clamp(Errands.I(v, 0), 0, 1); break;
                    case "skin": cur.Skin = Vec(w, cur.Skin); break;
                    case "haircolor": cur.HairColor = Vec(w, cur.HairColor); break;
                    case "hair": cur.Hair = v.ToLowerInvariant() == "none" ? "" : v; break;
                    case "beard": cur.Beard = v.ToLowerInvariant() == "none" ? "" : v; break;
                    case "gear": cur.Gear.Clear(); cur.Gear.AddRange(w); break;
                    case "health": cur.Health = Mathf.Max(1f, Errands.F(v, 200f)); break;
                    case "speed": cur.Speed = Mathf.Clamp(Errands.F(v, 1f), 0.2f, 3f); break;
                    case "walk": cur.Walk = Mathf.Clamp(Errands.F(v, 1f), 0.4f, 2f); break;
                    case "run": cur.Run = Mathf.Clamp(Errands.F(v, 1f), 0.3f, 2f); break;
                    case "turn": cur.Turn = Mathf.Clamp(Errands.F(v, 0f), 0f, 400f); break;
                    case "menu": cur.NoMenu = v.Trim() == "no"; break;
                    case "echo": cur.Echo = Mathf.Clamp01(Errands.F(v, 0.4f)); break;
                    case "mortal": cur.Mortal = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true"; break;
                    case "unseen": cur.Unseen = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true"; break;
                    case "greet": cur.Greet.Add(v); break;
                    case "talk": cur.Talk = v; break;
                    case "voice": cur.Voice = v.Trim().ToLowerInvariant(); { string w0 = cur.Voice.Split(' ')[0]; if (w0 != "quiet" && w0 != "own" && w0 != "mixed") NpcDefs.Problem(cur, from, "voice «" + v + "»: quiet, own або mixed <групи>"); } break;
                    case "persona": cur.Persona = v; Pairs(cur, from, k, NpcLevers.NoNature(cur, from, v), NpcLevers.TraitNames); break; // (split as NpcPersona reads it)
                    case "gait": cur.Gait = v.Trim().ToLowerInvariant(); if (cur.Gait.Length > 0 && !BodyGait.Known(cur.Gait)) NpcDefs.Problem(cur, from, "gait «" + v + "»: такої ходи нема"); break;
                    case "story": cur.Story = v; break;
                    case "generated": cur.Generated = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true"; break;
                    case "knack": Pairs(cur, from, k, w, NpcKnack.Keys); foreach (string pair in w) { int eq = pair.IndexOf('='); float kv; if (eq > 0 && float.TryParse(pair.Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out kv)) cur.Knack[pair.Substring(0, eq).ToLowerInvariant()] = kv; } break;
                    case "fight": string fv = v.ToLowerInvariant(); cur.Fight = fv == "off" || fv == "hunt" || fv == "defend" || fv == "wary" ? fv : "flee"; if (cur.Fight != fv) NpcDefs.Problem(cur, from, "fight «" + v + "»: такого нема (flee wary defend hunt off), стане flee"); break;
                    default: if (!NpcDefs.Words.Contains(k)) NpcDefs.Problem(cur, from, "рядок «" + k + ":» ніхто не читає (одрук?)"); break;
                }
            }
            return list;
        }

        // «word=number» pairs (persona, knack): each word one of those known, each with a number
        private static void Pairs(NpcDef d, string from, string key, string[] pairs, string[] known)
        {
            foreach (string pair in pairs)
            {
                int eq = pair.IndexOf('=');
                string word = (eq > 0 ? pair.Substring(0, eq) : pair).ToLowerInvariant();
                float n;
                if (System.Array.IndexOf(known, word) < 0) NpcDefs.Problem(d, from, key + ": «" + word + "» нема (" + string.Join(" ", known) + ")");
                else if (eq <= 0 || !float.TryParse(pair.Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out n)) NpcDefs.Problem(d, from, key + ": «" + pair + "» без числа");
            }
        }

        private static Vector3 Vec(string[] w, Vector3 def)
        {
            if (w.Length < 3) return def;
            return new Vector3(Mathf.Clamp01(Errands.F(w[0], def.x)), Mathf.Clamp01(Errands.F(w[1], def.y)), Mathf.Clamp01(Errands.F(w[2], def.z)));
        }
    }

    internal static class NpcDefs
    {
        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "npc"); } }
        private static List<NpcDef> _all;

        internal static List<NpcDef> All()
        {
            if (_all != null) return _all;
            Dictionary<string, NpcDef> by = new Dictionary<string, NpcDef>();
            Problems.Clear();
            foreach (KeyValuePair<string, string> f in StoryFiles.All(StoryFiles.Npc, Dir, RavenLooks.FromFiles)) // (the story's people, Гейд…: built in; the mod's own folder on the author's machine; an admin's tuning copies — StoryFiles)
                foreach (NpcDef d in NpcDef.ParseAll(f.Value.Replace("\r", ""), f.Key)) by[d.Id] = d;
            _all = new List<NpcDef>(by.Values);
            return _all;
        }

        internal static void Reload() { _all = null; }

        // the template words read by the core (NpcDef above) and by the modules (theirs added in NpcModules: NpcRaven's raven…)
        internal static readonly HashSet<string> Words = new HashSet<string> { "name", "body", "model", "skin", "haircolor", "hair", "beard", "gear", "health", "speed", "walk", "run", "turn", "menu", "echo", "mortal", "unseen", "greet", "talk", "voice", "persona", "gait", "story", "generated", "knack", "fight" };

        // what is wrong in the templates as read (said once a reading, in the log; «raven npc self» lists them)
        internal static readonly List<string> Problems = new List<string>();
        internal static void Problem(NpcDef d, string from, string what) { string s = (d != null ? d.Id : "?") + " (" + from + "): " + what; Problems.Add(s); Npcs.Log("template " + s); }

        internal static NpcDef Find(string id) { if (string.IsNullOrEmpty(id)) return null; foreach (NpcDef d in All()) if (d.Id == id) return d; return null; }
    }
}
#endif
