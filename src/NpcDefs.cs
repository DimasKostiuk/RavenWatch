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
    //   body: human                   human | odin | dvergr (the game's Dvergr: its own skeleton and animations, our clips over them by the
    //                                 bone names of BodyClips.SkeletonBone; its look is its «gear:» alone — the game's own DvergerSuit…/DvergerHair… and
    //                                 a thing in its hand; no model, skin, hair, beard, build)
    //   tint_skin: 0.36 0.47 0.48     a dvergr's own colours (BodyTint): tint_skin, tint_hair, tint_suit, tint_eyes — r g b, none written: as the game made it
    //   tint_eyes: 0.92 0.64 0.16     a person's own (BodyTint): its eyes, «tint_cape: r g b» whatever cape is on its shoulders, «tint_head: r g b» whatever is on its head, «tint_chest: r g b» and «tint_legs: r g b» what it wears on its chest and its legs; none written: as the game made it
    //   model: 0                      0 man · 1 woman
    //   skin: 0.85 0.72 0.6           colours as the game keeps them
    //   haircolor: 0.62 0.28 0.12
    //   hair: Hair7                   the game's hair/beard items (none: no hair)
    //   beard: Beard12
    //   gear: ArmorTunic6 ArmorLeatherLegs HelmetHat1 CapeLox   worn (armour, a cape, things in hands)
    //   hat: kazanok 0.02 0 1 0       a raven's hat of ours on its head (RavenGear, slot head: NpcHat) — up, forward (m), size (×), tilt (°) from where it sits by itself
    //   health: 200
    //   mortal: no                    yes: it can be hurt and killed (no by default: blows do nothing)
    //   armed: no                     yes: the weapon of its «gear:» is in its hand always, a part of its look, though it never fights (a story person)
    //   unseen: no                    yes: no monster sees it, it is nobody's foe (a story person under a power's keeping: Гейд,
    //                                 Hel's) — NpcVitals.Unseen. Not written: a story person of the world (assets/story/people) is unseen all the
    //                                 same (NpcVitals.UnseenBy, the user 2026-11-15) — «unseen: no» where its story wants monsters on it
    //   speed: 1                      how fast it walks and runs (×)
    //   walk: 1                       its own stroll, on top of that (×): 0.75 an old man's, 1.15 a brisk youth's
    //   turn: 80                      how fast it turns (deg/s), eased in and out, its feet stepping one by one (BodyTurn); none: the game's own
    //   menu: no                      no round menu on Q, for an admin neither
    //   menu: home talk               a round menu on Q for anyone, of these parts of the main ring only (NpcMenu.Button.Part: orders, talk, home, work,
    //                                 gear, profile) — a story person given a bed by a player (chapter 1: Ketil), never ordered about
    //   fight: flee                   its character in a fight (NpcMind.Fight): flee (the default: peaceful, only runs) ·
    //                                 wary (runs, fights back when caught or hit) · defend (a fighter: foes near it or whom it
    //                                 follows, or that hit it) · hunt (a fighter: any foe it senses) · off (does nothing)
    //   greet: Здоров був, {name}.    lines it greets you with ({greet} in its talk; without a talk: said on E)
    //   interrupts: 0.25               how likely it cuts into your talk with words of its own (NpcTalkCutIn.cs), and
    //   interrupt: Стривай, слухай…    what it says then, a line a variant (at least NpcTalk.CutInLeast)
    //   cry: Тримай лінію! Рівно!      what it calls out in a fight, a line a variant (NpcCry)
    //   talk: elder                   its talk on E (NpcTalk; none written: a settler «common», a person written by hand
    //                                 only its greet and «on talk»: the common talk hires, a story person is not for hire)
    //   voice: mixed weather night    its own words unasked (NpcVoice; none written: a settler or one hired speaks, a person written
    //                                 by hand does not): quiet — never · own — only its own themes ([id:theme]) · mixed <groups> — its
    //                                 own first, and the common ones of these groups (NpcVoice.Groups ids, or a theme's own name)
    //   stance: often                 a person written by hand stands in poses of its ease (NpcStance), as a resident does: yes | often (its hands seldom just hang)
    //   persona: stoop=0.8 build=0.5  its own ways (NpcPersona: walk stoop height build diligence laziness curiosity
    //                                 chrono chatty bravery lively; nature=<id> its kind of person: NpcNature); what is not written comes from its id
    //   color: #7FD6A8               its own colour in the capsule of words over heads (NpcBubble); none — one of NpcBubble.Palette by its seed
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
        public string Hat = "";        // «hat:» a raven's hat of ours (its Workshop id): NpcHat
        public Vector4 HatFit = new Vector4(0f, 0f, 1f, 0f); // its fit: up, forward (m), size (×), tilt (°)
        public float Health = 200f, Speed = 1f, Walk = 1f, Run = 1f; // (Run: × its run only — a body is the player's, and runs as he sprints: Heid ran 13 m/s, 2026-11-04)
        public float Turn;             // «turn: 80»: deg/s it turns at, eased in and out (NpcMind.TurnPace); 0 — the game's own quick turn
        public bool NoMenu;            // «menu: no»: no round menu on Q, for an admin neither (a story person: talked to, or not at all)
        public HashSet<string> MenuParts; // «menu: home talk»: its ring for anyone, these parts only (null: all of them, for the hired and an admin)
        public float Echo;             // «echo: 0.4»: only half there, see-through (NpcFade: the prologue's «Відлуння»); 0 — a body as any
        public bool Mortal;
        public bool Armed;             // «armed: yes»: its weapon is part of its look — in its hand always, whatever its way of fighting (the sisters' staff and knives: NpcGear.InHand)
        public bool Unseen, UnseenSaid; // «unseen: yes»: nobody's foe, no monster minds it (NpcVitals.Unseen) · written at all (else a story person's default: NpcVitals.UnseenBy)
        public string Fight = "flee"; // (fighting is for the fighters: their templates say defend or hunt)
        public string Talk = "";       // its talk as written (NpcTalk; «talk: elder»)
        public string Voice = "";      // «voice: quiet|own|mixed <groups>»: its unasked words (NpcVoice.Speaks); none: as its kind (a settler speaks, a hand-written one not)
        public string TalkId { get { return Talk.Length > 0 ? Talk : Generated ? "common" : ""; } } // the talk it has (""  none: it only greets)
        public bool Settler { get { return TalkId == "common"; } }  // one of the common people (the talk with hiring): needs, jobs, gossip, requests. A person the story wrote by hand is none: it lives in the world, its day and words its own
        public string Story = "";      // «story: …»: a story person's own past (its card, «Хто ти?»); a settler's is made (NpcBackstory)
        public bool Generated;         // «generated: yes»: a settler — its name, look and age made once per person (NpcSettlers)
        public readonly Dictionary<string, float> Knack = new Dictionary<string, float>(); // «knack: cook=1.5 melee=1.3» (NpcKnack)
        public int Stance;             // «stance: yes|often»: a person written by hand stands in poses of its own at its ease, as a resident does (NpcStance); often: seldom with its hands just hanging
        public string Timbre = "";     // «timbre: torvald [0.98]»: the voice of its wordless sounds, and its own pitch in it (NpcTimbre); none — one that suits it, by its seed
        public float TimbrePitch;
        public string Hue = "";        // «color: #7FD6A8»: its own colour in the capsule of words over heads (NpcBubble.ColorOf); none — one of NpcBubble.Palette by its seed
        public string Gait = "";       // its kind of gait (a name in assets/gaits.txt, shared or its own; none — NpcGait picks from what it is)
        public string Persona = "";    // its own ways as written (NpcPersona; «persona: stoop=0.8 chatty=0.9»), the rest from its id
        public readonly List<string> Greet = new List<string>();
        public float Interrupts;        // «interrupts:»: the chance it cuts into your talk at a question (NpcTalkCutIn.cs)
        public readonly List<string> Interrupt = new List<string>(); // «interrupt:»: its words then
        public readonly List<string> Cry = new List<string>(); // «cry:»: what it calls out in a fight, a line a variant (NpcCry)
        public readonly List<NpcRule> Rules = new List<NpcRule>();
        public readonly List<string> Keep = new List<string>(); // the lines that are not its look, as written (saved back as they were)
        internal bool Odin { get { return Body == "odin"; } }
        internal bool Dvergr { get { return Body == "dvergr"; } } // (the game's Dvergr body: Npcs.BuildDvergr)
        // a dvergr's own colours (BodyTint); NoTint: as the game made it
        internal static readonly Vector3 NoTint = new Vector3(-1f, -1f, -1f);
        internal static bool HasTint(Vector3 v) { return v.x >= 0f; }
        public Vector3 TintSkin = NoTint, TintHair = NoTint, TintSuit = NoTint, TintEyes = NoTint, TintCape = NoTint, TintHead = NoTint, TintChest = NoTint, TintLegs = NoTint; // (a dvergr: skin, hair, suit, eyes · a person: eyes, cape, head, chest, legs)
        private static Vector3 Tint(string[] w) { return w.Length < 3 ? NoTint : Vec(w, Vector3.zero); }
        private static void TintLine(StringBuilder sb, string key, Vector3 v) { if (!HasTint(v)) return; System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture; sb.Append(key).Append(": ").Append(v.x.ToString("0.###", ci)).Append(' ').Append(v.y.ToString("0.###", ci)).Append(' ').Append(v.z.ToString("0.###", ci)).Append('\n'); }

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
            if (Dvergr) // (its look: its gear and its own colours)
            {
                if (Gear.Count > 0) sb.Append("gear: ").Append(string.Join(" ", Gear.ToArray())).Append('\n');
                TintLine(sb, "tint_skin", TintSkin); TintLine(sb, "tint_hair", TintHair); TintLine(sb, "tint_suit", TintSuit); TintLine(sb, "tint_eyes", TintEyes);
            }
            else if (!Odin)
            {
                sb.Append("model: ").Append(Model).Append('\n');
                sb.Append("skin: ").Append(Skin.x.ToString("0.###", ci)).Append(' ').Append(Skin.y.ToString("0.###", ci)).Append(' ').Append(Skin.z.ToString("0.###", ci)).Append('\n');
                sb.Append("haircolor: ").Append(HairColor.x.ToString("0.###", ci)).Append(' ').Append(HairColor.y.ToString("0.###", ci)).Append(' ').Append(HairColor.z.ToString("0.###", ci)).Append('\n');
                sb.Append("hair: ").Append(Hair.Length > 0 ? Hair : "none").Append('\n');
                sb.Append("beard: ").Append(Beard.Length > 0 ? Beard : "none").Append('\n');
                if (Gear.Count > 0) sb.Append("gear: ").Append(string.Join(" ", Gear.ToArray())).Append('\n');
                TintLine(sb, "tint_eyes", TintEyes); TintLine(sb, "tint_cape", TintCape); TintLine(sb, "tint_head", TintHead); TintLine(sb, "tint_chest", TintChest); TintLine(sb, "tint_legs", TintLegs);
                if (Hat.Length > 0) sb.Append("hat: ").Append(Hat).Append(' ').Append(HatFit.x.ToString("0.###", ci)).Append(' ').Append(HatFit.y.ToString("0.###", ci)).Append(' ').Append(HatFit.z.ToString("0.##", ci)).Append(' ').Append(HatFit.w.ToString("0.#", ci)).Append('\n');
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
                if (k != "name" && k != "body" && k != "model" && k != "skin" && k != "haircolor" && k != "hair" && k != "beard" && k != "gear" && k != "hat" && !k.StartsWith("tint_", StringComparison.Ordinal)) cur.Keep.Add(line);
                if (k.StartsWith("on ")) { NpcRule rule = NpcRule.Parse(k.Substring(3), v); if (rule != null) cur.Rules.Add(rule); else NpcDefs.Problem(cur, from, "правило «" + line + "» не зрозуміле"); continue; }
                string[] w = v.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                switch (k)
                {
                    case "name": cur.Name = v; break;
                    case "body": { string bv = v.ToLowerInvariant(); cur.Body = bv == "odin" || bv == "dvergr" ? bv : "human"; if (cur.Body != bv) NpcDefs.Problem(cur, from, "body «" + v + "»: такого нема (human odin dvergr), стане human"); } break;
                    case "model": cur.Model = Mathf.Clamp(Errands.I(v, 0), 0, 1); break;
                    case "skin": cur.Skin = Vec(w, cur.Skin); break;
                    case "haircolor": cur.HairColor = Vec(w, cur.HairColor); break;
                    case "hair": cur.Hair = v.ToLowerInvariant() == "none" ? "" : v; break;
                    case "beard": cur.Beard = v.ToLowerInvariant() == "none" ? "" : v; break;
                    case "gear": cur.Gear.Clear(); cur.Gear.AddRange(w); break;
                    case "hat": cur.Hat = w.Length > 0 && w[0] != "none" ? w[0] : ""; cur.HatFit = new Vector4(w.Length > 1 ? Errands.F(w[1], 0f) : 0f, w.Length > 2 ? Errands.F(w[2], 0f) : 0f, w.Length > 3 ? Mathf.Clamp(Errands.F(w[3], 1f), 0.2f, 4f) : 1f, w.Length > 4 ? Errands.F(w[4], 0f) : 0f); break;
                    case "tint_skin": cur.TintSkin = Tint(w); break;
                    case "tint_hair": cur.TintHair = Tint(w); break;
                    case "tint_suit": cur.TintSuit = Tint(w); break;
                    case "tint_eyes": cur.TintEyes = Tint(w); break;
                    case "tint_cape": cur.TintCape = Tint(w); break;
                    case "tint_head": cur.TintHead = Tint(w); break;
                    case "tint_chest": cur.TintChest = Tint(w); break;
                    case "tint_legs": cur.TintLegs = Tint(w); break;
                    case "health": cur.Health = Mathf.Max(1f, Errands.F(v, 200f)); break;
                    case "speed": cur.Speed = Mathf.Clamp(Errands.F(v, 1f), 0.2f, 3f); break;
                    case "walk": cur.Walk = Mathf.Clamp(Errands.F(v, 1f), 0.4f, 2f); break;
                    case "run": cur.Run = Mathf.Clamp(Errands.F(v, 1f), 0.3f, 2f); break;
                    case "turn": cur.Turn = Mathf.Clamp(Errands.F(v, 0f), 0f, 400f); break;
                    case "menu":
                        {
                            string m = v.Trim().ToLowerInvariant();
                            cur.NoMenu = m == "no";
                            cur.MenuParts = m == "no" || m == "yes" || m.Length == 0 ? null : new HashSet<string>(m.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                            if (cur.MenuParts != null) foreach (string part in cur.MenuParts) if (Array.IndexOf(NpcMenu.Parts, part) < 0) NpcDefs.Problem(cur, from, "menu: «" + part + "» — такої частини кола нема (" + string.Join(" ", NpcMenu.Parts) + "; або no)");
                            break;
                        }
                    case "echo": cur.Echo = Mathf.Clamp01(Errands.F(v, 0.4f)); break;
                    case "mortal": cur.Mortal = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true"; break;
                    case "armed": cur.Armed = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true"; break;
                    case "unseen": cur.Unseen = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true"; cur.UnseenSaid = true; break;
                    case "greet": cur.Greet.Add(v); break;
                    case "interrupts": cur.Interrupts = Mathf.Clamp01(Errands.F(v, 0f)); break;
                    case "interrupt": cur.Interrupt.Add(v); break;
                    case "cry": cur.Cry.Add(v); break;
                    case "talk": cur.Talk = v; break;
                    case "voice": cur.Voice = v.Trim().ToLowerInvariant(); { string w0 = cur.Voice.Split(' ')[0]; if (w0 != "quiet" && w0 != "own" && w0 != "mixed") NpcDefs.Problem(cur, from, "voice «" + v + "»: quiet, own або mixed <групи>"); } break;
                    case "persona": cur.Persona = v; Pairs(cur, from, k, NpcLevers.NoNature(cur, from, v), NpcLevers.TraitNames); break; // (split as NpcPersona reads it)
                    case "timbre":
                        {
                            string[] tw = v.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            cur.Timbre = tw.Length > 0 ? tw[0] : "";
                            cur.TimbrePitch = tw.Length > 1 ? Mathf.Clamp(Errands.F(tw[1], 0f), 0f, 1.4f) : 0f;
                            if (cur.Timbre.Length > 0 && !NpcTimbre.Known(cur.Timbre)) { NpcDefs.Problem(cur, from, "timbre «" + v + "»: такого тембру нема (рядки «@timbre» в assets/story/sounds)"); cur.Timbre = ""; }
                            else if (NpcTimbre.HeroOnly(cur.Timbre)) { NpcDefs.Problem(cur, from, "timbre «" + v + "»: це голос героя (hero), людині не дається"); cur.Timbre = ""; }
                            break;
                        }
                    case "color": { Color hc; if (v.StartsWith("#", StringComparison.Ordinal) && ColorUtility.TryParseHtmlString(v, out hc)) cur.Hue = v; else NpcDefs.Problem(cur, from, "color «" + v + "»: колір як #RRGGBB"); } break;
                    case "gait": cur.Gait = v.Trim().ToLowerInvariant(); if (cur.Gait.Length > 0 && !BodyGait.Known(cur.Gait)) NpcDefs.Problem(cur, from, "gait «" + v + "»: такої ходи нема"); break;
                    case "story": cur.Story = v; break;
                    case "stance": { string sv = v.ToLowerInvariant(); cur.Stance = sv == "often" ? 2 : sv == "yes" ? 1 : 0; if (cur.Stance == 0 && sv != "no") NpcDefs.Problem(cur, from, "stance «" + v + "»: yes, often або no"); } break;
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
            foreach (NpcDef d in _all) if (d.Interrupts > 0f && d.Interrupt.Count < NpcTalk.CutInLeast && !d.Interrupt.Exists(x => x.Contains("{@"))) Problem(d, "npc", "interrupts без щонайменше " + NpcTalk.CutInLeast + " рядків interrupt: (є " + d.Interrupt.Count + ") — те саме вклинюватиметься знову й знову");
            return _all;
        }

        internal static void Reload() { _all = null; }

        // the template words read by the core (NpcDef above) and by the modules (theirs added in NpcModules: NpcRaven's raven…)
        internal static readonly HashSet<string> Words = new HashSet<string> { "name", "body", "model", "skin", "haircolor", "hair", "beard", "gear", "hat", "tint_skin", "tint_hair", "tint_suit", "tint_eyes", "tint_cape", "tint_head", "tint_chest", "tint_legs", "health", "speed", "walk", "run", "turn", "menu", "echo", "mortal", "unseen", "armed", "greet", "interrupts", "interrupt", "cry", "talk", "voice", "persona", "gait", "timbre", "color", "stance", "story", "generated", "knack", "fight" };

        // what is wrong in the templates as read (said once a reading, in the log; «raven npc self» lists them)
        internal static readonly List<string> Problems = new List<string>();
        internal static void Problem(NpcDef d, string from, string what) { string s = (d != null ? d.Id : "?") + " (" + from + "): " + what; Problems.Add(s); Npcs.Log("template " + s); }

        internal static NpcDef Find(string id) { if (string.IsNullOrEmpty(id)) return null; foreach (NpcDef d in All()) if (d.Id == id) return d; return null; }
    }
}
#endif
