#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Who an NPC is (story-plan.md, «NPC»): a template like the errands'. Built-in ones below; while tuning, files in
    // config/RavenWatch/npc/*.txt add or replace them by id.
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
    //   fight: defend                 off · defend (foes near it or whom it follows, or that hit it) · hunt (any foe it senses)
    //   greet: Здоров був, {name}.    lines said on E (one at random), until scripts give it more to say
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
        public float Health = 200f, Speed = 1f;
        public bool Mortal;
        public string Fight = "defend";
        public readonly List<string> Greet = new List<string>();
        public readonly List<NpcRule> Rules = new List<NpcRule>();
        internal bool Odin { get { return Body == "odin"; } }

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
                    if (Story.GoodName(cur.Id)) list.Add(cur);
                    else { Npcs.Log("npc id «" + cur.Id + "» in " + from + " left out (no ; = , + | or spaces)"); cur = null; }
                    continue;
                }
                if (cur == null) continue;
                int c = line.IndexOf(':');
                if (c <= 0) continue;
                string k = line.Substring(0, c).Trim().ToLowerInvariant(), v = line.Substring(c + 1).Trim();
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
                    case "mortal": cur.Mortal = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true"; break;
                    case "greet": cur.Greet.Add(v); break;
                    case "fight": string fv = v.ToLowerInvariant(); cur.Fight = fv == "off" || fv == "hunt" ? fv : "defend"; break;
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
        // (test people for now; the story's own come with its files)
        private static readonly string[] Builtin =
        {
            "[npc test_elder]\nname: Старий Торвальд\nbody: human\nmodel: 0\nhaircolor: 0.75 0.75 0.72\nhair: Hair7\nbeard: Beard12\n" +
            "gear: ArmorTunic6 ArmorLeatherLegs CapeLox\ngreet: А, це ти, {name}. Ворони про тебе каркали.\ngreet: Сідай біля вогню. Ноги в мене вже не ті, щоб стояти.\nfight: off\n" +
            "on near 6: look player; say @greet\non talk: say @greet\non hit: say Гей! Я старий, а не мертвий.\non day: wander 6 home\non night: sit fire\n",
            "[npc test_shieldmaiden]\nname: Сігрід\nbody: human\nmodel: 1\nhaircolor: 0.62 0.28 0.12\nhair: Hair3\n" +
            "gear: ArmorBronzeChest ArmorBronzeLegs HelmetBronze SwordBronze ShieldBronzeBuckler\nhealth: 400\nspeed: 1.1\n" +
            "greet: Тримай щит вище, {name}.\ngreet: Мунін каже, ти б'єшся як мокра курка. Я так не думаю. Поки що.\nfight: defend\n" +
            "on near 5: emote wave\non fight_over: say Ще один. Хто наступний?\non spawn: wander 10 home\n",
            "[npc odin]\nname: Мандрівник\nbody: odin\ngreet: …\n",
        };

        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "npc"); } }
        private static List<NpcDef> _all;

        internal static List<NpcDef> All()
        {
            if (_all != null) return _all;
            Dictionary<string, NpcDef> by = new Dictionary<string, NpcDef>();
            foreach (string s in Builtin) foreach (NpcDef d in NpcDef.ParseAll(s, "built-in")) by[d.Id] = d;
            if (RavenLooks.FromFiles && Directory.Exists(Dir))
                foreach (string f in Directory.GetFiles(Dir, "*.txt"))
                {
                    try { foreach (NpcDef d in NpcDef.ParseAll(File.ReadAllText(f, Encoding.UTF8), Path.GetFileName(f))) by[d.Id] = d; }
                    catch (Exception e) { Npcs.Log("npc file " + f + ": " + e.Message); }
                }
            _all = new List<NpcDef>(by.Values);
            return _all;
        }

        internal static void Reload() { _all = null; }

        internal static NpcDef Find(string id) { if (string.IsNullOrEmpty(id)) return null; foreach (NpcDef d in All()) if (d.Id == id) return d; return null; }
    }
}
#endif
