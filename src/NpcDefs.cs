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
    //   speed: 1                      how fast it walks and runs (×)
    //   walk: 1                       its own stroll, on top of that (×): 0.75 an old man's, 1.15 a brisk youth's
    //   fight: flee                   its character in a fight (NpcMind.Fight): flee (the default: peaceful, only runs) ·
    //                                 wary (runs, fights back when caught or hit) · defend (a fighter: foes near it or whom it
    //                                 follows, or that hit it) · hunt (a fighter: any foe it senses) · off (does nothing)
    //   greet: Здоров був, {name}.    lines it greets you with ({greet} in its talk; without a talk: said on E)
    //   talk: elder                   its talk on E (NpcTalk; none: «common»)
    //   persona: stoop=0.8 tempo=0.8  its own ways (NpcPersona: walk tempo stoop height diligence laziness curiosity
    //                                 chrono chatty bravery); what is not written comes from its id
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
        public float Health = 200f, Speed = 1f, Walk = 1f;
        public float Echo;             // «echo: 0.4»: only half there, see-through (NpcFade: the prologue's «Відлуння»); 0 — a body as any
        public bool Mortal;
        public string Fight = "flee"; // (fighting is for the fighters: their templates say defend or hunt)
        public string Talk = "common"; // its talk (NpcTalk; «talk: elder»)
        public string Story = "";      // «story: …»: a story person's own past (its card, «Хто ти?»); a settler's is made (NpcBackstory)
        public bool Generated;         // «generated: yes»: a settler — its name, look and age made once per person (NpcSettlers)
        public readonly Dictionary<string, float> Knack = new Dictionary<string, float>(); // «knack: cook=1.5 melee=1.3» (NpcKnack)
        public string Gait = "";       // its kind of gait (NpcGait: plain elder soldier spry waddle limp meek; none — from what it is)
        public string Persona = "";    // its own ways as written (NpcPersona; «persona: stoop=0.8 chatty=0.9»), the rest from its id
        public readonly List<string> Greet = new List<string>();
        public readonly List<NpcRule> Rules = new List<NpcRule>();
        public readonly List<string> Keep = new List<string>(); // the lines that are not its look, as written (saved back as they were)
        internal bool Odin { get { return Body == "odin"; } }

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
                if (k.StartsWith("on ")) { NpcRule rule = NpcRule.Parse(k.Substring(3), v); if (rule != null) cur.Rules.Add(rule); else Npcs.Log(cur.Id + ": rule «" + line + "» not understood"); continue; }
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
                    case "echo": cur.Echo = Mathf.Clamp01(Errands.F(v, 0.4f)); break;
                    case "mortal": cur.Mortal = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true"; break;
                    case "greet": cur.Greet.Add(v); break;
                    case "talk": cur.Talk = v; break;
                    case "persona": cur.Persona = v; break;
                    case "gait": cur.Gait = v.Trim().ToLowerInvariant(); break;
                    case "story": cur.Story = v; break;
                    case "generated": cur.Generated = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true"; break;
                    case "knack": foreach (string pair in w) { int eq = pair.IndexOf('='); float kv; if (eq > 0 && float.TryParse(pair.Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out kv)) cur.Knack[pair.Substring(0, eq).ToLowerInvariant()] = kv; } break;
                    case "fight": string fv = v.ToLowerInvariant(); cur.Fight = fv == "off" || fv == "hunt" || fv == "defend" || fv == "wary" ? fv : "flee"; break;
                }
            }
            return list;
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
            foreach (KeyValuePair<string, string> f in StoryFiles.All(StoryFiles.Npc, Dir, RavenLooks.FromFiles)) // (the story's people, Гейд…: built in; the mod's own folder on the author's machine; an admin's tuning copies — StoryFiles)
                foreach (NpcDef d in NpcDef.ParseAll(f.Value.Replace("\r", ""), f.Key)) by[d.Id] = d;
            _all = new List<NpcDef>(by.Values);
            return _all;
        }

        internal static void Reload() { _all = null; }

        internal static NpcDef Find(string id) { if (string.IsNullOrEmpty(id)) return null; foreach (NpcDef d in All()) if (d.Id == id) return d; return null; }
    }
}
#endif
