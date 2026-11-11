#if RAVEN_ITEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Студія»: the author's eyes on the ravens' animation from outside the game. It sleeps unless the folder
    // config/RavenWatch/studio exists (players never have it). There a request file is read twice a second
    // (studio/request.txt, one command a line; taken and deleted), and what it asks is shot into studio/out/:
    //   shot <name>                                  — the game's screen as it is
    //   burst <name> <seconds> <fps>                 — the screen, frame after frame, as one sheet
    //   scene <name> <scene id> [fps] [from]          — a story scene played and checked at its place (taken there, touches done): every body measured each frame
    //                                                  by BodyCheck, the picture's own rules, the screen and each body close — StudioScene.cs
    //   pose <name> <template> <h|m> [moments] [views] — your raven holds the template's moments one by one; each from
    //                                                  the views (side,front,top,back) as one sheet (rows: moments)
    //   play <name> <template> <h|m> [fps] [views]   — the template played in time, shot as it goes (rows: frames)
    //   look <name> <h|m> [views]                    — the raven as it sits now, from the views
    //   wait <name> <event> <h|m> [seconds] [fps] [views] [give up after s] — waits for a thing to happen to you (fall,
    //                                                  hurt, boss, kill, work, eat, pick, pet, jump, dodge, swim, any), then
    //                                                  shoots the raven from the views and the screen as it goes on
    //   film <name> <who> [seconds] [fps] [views]    — the raven living as it does, nothing asked of it (rows: frames)
    //   set <Class.Field> <number|true|false>        — a static tuning number changed live (ShoulderLife.ScratchHip -100); get <Class.Field>
    //   fly <name> <h|m> [seconds] [fps] [views] [give up after s] — waits for that raven to take wing (off your shoulder,
    //                                                  or called back from a perch, or flying in), then follows it: the
    //                                                  flight and the landing, on your shoulder or on a perch
    //                                                  give up 0: no waiting, it follows the raven as it is now (in flight already)
    //   moment <h|m> <heal|scream|dress|fury> [s]    — a gift's pose on that raven for a while, with no gift (then film it)
    //   raven <the mod's console words>               — a «raven …» console command, its answer into the log
    //   con <the game's console words>               — a command of the game's console (spawn, god, env, skiptime…)
    //   reload                                       — the sitting templates read again from their files
    // On you (the player): ylook, ypose, yplay, yfilm, ywait, gift — see StudioHero.cs.
    // Any command may end with slow=<share> (0.05–1): the game slowed down while it shoots (play, film: quick moves frame
    // by frame); in play and film the cameras stay where they were put at the first frame.
    // Who (pose, play, film): h, m — your raven on your shoulder; p, ph, pm — the nearest raven on a perch (any, Hugin,
    // Munin; within 30 m). On a perch «side» is its right, «inner» its left from above.
    // Views: side (its outer side), inner (from above by your head: the wing by your head), front, top, back; the head
    // close up: face (between the eyes), eye0, eye1 (each eye from outside).
    // The weather (rain, snow) and the grass are left out of the studio's pictures.
    // Each sheet comes with <name>.txt: what is where on it, and the bones' report — the largest turn between two
    // frames (a jerk), the nearest a wing comes to the head and to yours (a wing through the body). studio/out/log.txt
    // says what was done.
    internal static partial class Studio
    {
        // a line's words after its first one, as written
        private static string Rest(string line)
        {
            string t = (line ?? "").Trim();
            int sp = t.IndexOf(' ');
            return sp > 0 ? t.Substring(sp + 1).Trim() : "";
        }

        // the game's own console, as if typed there (its answer shows in that console)
        private static string Console(string words)
        {
            if (global::Console.instance == null) return "no console";
            if (words.Length == 0) return "con <command>";
            global::Console.instance.TryRunCommand(words);
            return "run: " + words;
        }

        internal const int Cell = 320;
        private static string Root { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "studio"); } }
        private static string Out { get { return Path.Combine(Root, "out"); } }
        private static string Request { get { return Path.Combine(Root, "request.txt"); } }

        private static float _lookAt;
        private static bool _on, _busy;
        private static readonly Queue<string> Todo = new Queue<string>();

        internal static bool On { get { return _on; } }

        // every frame (RavenSkills): a look at the folder twice a second, the next request when free
        internal static void Tick()
        {
            if (Time.unscaledTime < _lookAt) return;
            _lookAt = Time.unscaledTime + 0.5f;
            _on = Directory.Exists(Root);
            if (!_on) return;
            if (File.Exists(Request))
            {
                string[] lines;
                try { lines = File.ReadAllLines(Request, Encoding.UTF8); File.Delete(Request); }
                catch { return; } // quiet: still being written, the next look in half a second
                foreach (string l in lines)
                {
                    string q = l.Trim();
                    if (q.Length == 0 || q.StartsWith("#")) continue;
                    if (q.StartsWith("raven ")) { Log(q, (Commands.Run(Rest(q)) ?? "").Replace('\n', ' ')); continue; } // (at once: no shooting in them)
                    if (q.StartsWith("con ")) { Log(q, Console(Rest(q))); continue; }
                    Todo.Enqueue(q);
                }
            }
            if (_busy && Time.unscaledTime - _busySince > StuckSeconds) { _busy = false; Log("?", "a job hung: the studio is free again"); }
            if (_busy || Todo.Count == 0 || Plugin.Instance == null) return;
            _busy = true;
            _busySince = Time.unscaledTime;
            Plugin.Instance.StartCoroutine(Run(Todo.Dequeue()));
        }

        internal static string Describe()
        {
            return !Directory.Exists(Root) ? "Студія спить: немає теки " + Root
                : "Студія працює: " + Root + (_busy ? " (знімає)" : "") + ", у черзі " + Todo.Count;
        }

        private static IEnumerator Run(string line)
        {
            string[] a = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            // slow=<share>: the game slowed down while it is shot (a quick shiver caught frame by frame)
            float slow = 1f;
            List<string> words = new List<string>();
            foreach (string w in a)
            {
                if (w.StartsWith("slow=", StringComparison.OrdinalIgnoreCase)) { float v; if (float.TryParse(w.Substring(5), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) slow = Mathf.Clamp(v, 0.05f, 1f); }
                else words.Add(w);
            }
            string cmd = words.Count > 0 ? words[0].ToLowerInvariant() : "";
            bool air = (cmd == "ypose" || cmd == "yplay") && words.Remove("air"); // (ypose, yplay: tossed up first; a word anywhere after the name)
            a = words.ToArray();
            IEnumerator job = null;
            try
            {
                Directory.CreateDirectory(Out);
                Prune();
                switch (cmd)
                {
                    case "shot": job = Shot(Arg(a, 1, "shot")); break;
                    case "burst": job = Burst(Arg(a, 1, "burst"), Num(a, 2, 2f), Num(a, 3, 8f)); break;
#if RAVEN_STORY
                    case "scene": job = SceneCheck(Arg(a, 1, "scene"), Arg(a, 2, ""), Num(a, 3, 2f), (int)Num(a, 4, 0f)); break;
#endif
                    case "pose": job = Pose(Arg(a, 1, "pose"), Arg(a, 2, ""), Arg(a, 3, "h"), (int)Num(a, 4, 6f), Arg(a, 5, "side,front,top"), false, 0f); break;
                    case "play": job = Pose(Arg(a, 1, "play"), Arg(a, 2, ""), Arg(a, 3, "h"), 0, Arg(a, 5, "side,front"), true, Num(a, 4, 8f)); break;
                    case "look": job = Look(Arg(a, 1, "look"), Arg(a, 2, "h"), Arg(a, 3, "side,front,top")); break;
                    case "wait": job = Wait(Arg(a, 1, "wait"), Arg(a, 2, "any"), Arg(a, 3, "h"), Num(a, 4, 2.5f), Num(a, 5, 8f), Arg(a, 6, "side,front"), Num(a, 7, 240f)); break;
                    case "fly": job = Fly(Arg(a, 1, "fly"), Arg(a, 2, "h"), Num(a, 3, 8f), Num(a, 4, 6f), Arg(a, 5, "side,front"), Num(a, 6, 240f), slow); slow = 1f; break;
                    case "film": job = Film(Arg(a, 1, "film"), Arg(a, 2, "h"), Num(a, 3, 4f), Num(a, 4, 6f), Arg(a, 5, "side,front")); break;
                    case "ylook": job = HeroLook(Arg(a, 1, "ylook"), Arg(a, 2, "front,side,back,three")); break;
                    case "ypose": job = HeroPose(Arg(a, 1, "ypose"), Arg(a, 2, ""), (int)Num(a, 3, 6f), Arg(a, 4, "front,side"), false, 0f, air); break;
                    case "yplay": job = HeroPose(Arg(a, 1, "yplay"), Arg(a, 2, ""), 0, Arg(a, 4, "front,side"), true, Num(a, 3, 8f), air); break;
                    case "yfilm": job = HeroFilm(Arg(a, 1, "yfilm"), Num(a, 2, 4f), Num(a, 3, 6f), Arg(a, 4, "front,side"), null); break;
                    case "ywait": job = HeroWait(Arg(a, 1, "ywait"), Arg(a, 2, "any"), Num(a, 3, 3f), Num(a, 4, 8f), Arg(a, 5, "front,side"), Num(a, 6, 240f)); break;
                    case "gift": Log(line, Gift(Arg(a, 1, ""), Num(a, 2, 0f))); break;
                    case "moment": Log(line, MomentNow(Arg(a, 1, "m"), Arg(a, 2, ""), Num(a, 3, 5f))); break;
                    case "raven": Log(line, (Commands.Run(Rest(line)) ?? "").Replace('\n', ' ')); break;
                    case "con": Log(line, Console(Rest(line))); break;
                    case "fate": Log(line, FateShowNow(Arg(a, 1, "h"))); break;
                    case "ytakeoff": job = HeroTakeoff(Arg(a, 1, "ytakeoff"), Num(a, 2, 2.6f), Num(a, 3, 10f), Arg(a, 4, "side,front,three")); break;
                    case "armor": Log(line, Armour(Arg(a, 1, "none"))); break;
                    case "armors": Log(line, Armours()); break;
                    case "react": Log(line, PlayerReact.Studio(Arg(a, 1, "ear"), Arg(a, 2, "h").StartsWith("m"))); break;
                    case "arm": Log(line, ArmPerch.Studio(Arg(a, 1, "up"), Arg(a, 2, "h").StartsWith("m"))); break;
                    case "yreload": BodyClips.Load(); Log(line, "player clips read again: " + BodyClips.Clips.Count); break;
                    case "set": Log(line, Field(Arg(a, 1, ""), Arg(a, 2, null))); break;
                    case "get": Log(line, Field(Arg(a, 1, ""), null)); break;
                    case "wear": Log(line, Outfits.StudioWear(Arg(a, 1, "h").StartsWith("m"), Arg(a, 2, "back"), Arg(a, 3, "-"))); break;
                    case "fits": Log(line, Outfits.StudioFits()); break;
                    case "photo": job = Photo(Arg(a, 1, ""), Num(a, 2, float.NaN), Num(a, 3, float.NaN), Num(a, 4, float.NaN)); break;
                    case "reload": SitTemplates.Load(); Log(line, "templates read again: " + SitTemplates.All.Count); break;
#if RAVEN_NPC
                    case "nlook": job = NpcLook(Arg(a, 1, "nlook"), Arg(a, 2, "near"), Arg(a, 3, "front,side,back")); break;
                    case "nprobe": job = NpcProbe(Arg(a, 1, "nprobe"), Arg(a, 2, "near"), Num(a, 3, 2f)); break;
                    case "nskin": job = NpcSkin(Arg(a, 1, "nskin"), Arg(a, 2, "near")); break;
                    case "nattach": Log(line, NpcAttach(Arg(a, 1, ""))); break;
                    case "hover": Log(line, Hover()); break;
                    case "nfilm": job = NpcFilm(Arg(a, 1, "nfilm"), Arg(a, 2, "near"), Num(a, 3, 4f), Num(a, 4, 2f), Arg(a, 5, "front,side")); break;
#endif
                    default: Log(line, "unknown command"); break;
                }
            }
            catch (Exception e) { Log(line, "failed: " + e.Message); job = null; }
            float scaleWas = Time.timeScale;
            if (job != null && slow < 1f) Time.timeScale = slow;
            if (job != null)
            {
                // the job's own steps, each guarded (a broken one must not leave the studio busy for ever); its own
                // cleanup (finally: the held pose let go, the cameras gone) runs when it is disposed, broken or not
                while (true)
                {
                    bool more;
                    try { more = job.MoveNext(); }
                    catch (Exception e) { Log(line, "failed: " + e.Message); break; }
                    if (!more) { Log(line, "done"); break; }
                    yield return job.Current;
                }
                IDisposable end = job as IDisposable;
                if (end != null) try { end.Dispose(); } catch { }
            }
            if (slow < 1f) Time.timeScale = scaleWas;
            _busy = false;
        }

        private static float _busySince;
        internal static float StuckSeconds = 420f; // a job this long: given up (the studio free again; a «wait» may wait up to 5 min)

        private static string Arg(string[] a, int i, string def) { return a.Length > i ? a[i] : def; }

        // a static number (or switch) of the mod's, read or set by «Class.Field» (the author's live tuning; nothing saved)
        internal static string Field(string path, string value)
        {
            int dot = path.LastIndexOf('.');
            if (dot <= 0) return "write Class.Field";
            string cls = path.Substring(0, dot), name = path.Substring(dot + 1);
            Type type = null;
            foreach (Type t in typeof(Studio).Assembly.GetTypes()) if (t.Name == cls) { type = t; break; }
            if (type == null) return "no class " + cls;
            System.Reflection.FieldInfo f = type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f == null || f.IsLiteral) return "no static field " + name + " in " + cls;
            object was = f.GetValue(null);
            if (value == null) return path + " = " + Convert.ToString(was, CultureInfo.InvariantCulture);
            try
            {
                object v = f.FieldType == typeof(bool) ? (object)(value == "1" || value.ToLowerInvariant() == "true")
                         : f.FieldType == typeof(int) ? (object)int.Parse(value, CultureInfo.InvariantCulture)
                         : (object)float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                f.SetValue(null, v);
                return path + ": " + Convert.ToString(was, CultureInfo.InvariantCulture) + " → " + Convert.ToString(v, CultureInfo.InvariantCulture);
            }
            catch (Exception e) { return "could not set " + path + ": " + e.Message; }
        }
        private static float Num(string[] a, int i, float def) { float v; return a.Length > i && float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }

        private static void Log(string line, string what)
        {
            try { File.AppendAllText(Path.Combine(Out, "log.txt"), DateTime.Now.ToString("HH:mm:ss") + "  " + line + "  →  " + what + "\n", new UTF8Encoding(false)); } catch { }
        }

        // ---------------------------------------------------------------- the screen

        private static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D t = ScreenCapture.CaptureScreenshotAsTexture();
            Texture2D s = Shrink(t, 1600);
            Save(name, s);
            UnityEngine.Object.Destroy(t); if (s != t) UnityEngine.Object.Destroy(s);
            Note(name, "the game's screen as it was");
        }

        private static IEnumerator Burst(string name, float seconds, float fps)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(seconds * fps), 1, 24);
            List<Texture2D> frames = new List<Texture2D>();
            for (int i = 0; i < n; i++)
            {
                yield return new WaitForEndOfFrame();
                Texture2D t = ScreenCapture.CaptureScreenshotAsTexture();
                frames.Add(Shrink(t, 640));
                if (frames[frames.Count - 1] != t) UnityEngine.Object.Destroy(t);
                yield return new WaitForSeconds(1f / Mathf.Max(1f, fps));
            }
            int cols = 4, w = frames[0].width, h = frames[0].height, rows = (n + cols - 1) / cols;
            Texture2D sheet = new Texture2D(w * cols, h * rows, TextureFormat.RGB24, false);
            Fill(sheet, new Color32(20, 20, 20, 255));
            for (int i = 0; i < n; i++) { Blit(frames[i], sheet, (i % cols) * w, (rows - 1 - i / cols) * h); UnityEngine.Object.Destroy(frames[i]); }
            Save(name, sheet);
            UnityEngine.Object.Destroy(sheet);
            Note(name, n + " frames of the screen, " + fps.ToString("0.#", CultureInfo.InvariantCulture) + " a second, left to right, top to bottom");
        }

        // ---------------------------------------------------------------- your raven, from around it

        private sealed class Rig
        {
            internal readonly List<Camera> Cams = new List<Camera>();
            internal readonly List<string> Views = new List<string>();
            internal RenderTexture Rt;
            internal Light Lamp;
            internal GameObject Root;
            internal Vector3 Mid;
            internal int Size = Cell; // (px a frame: Cell, bigger for the stage's key frames)
            internal System.Func<List<Mark>> Marks; // (lines drawn over a close view's frame, asked at each shot: the stage's hands and targets — BodyCheck.Marks)
        }

        // a line in the world drawn over a frame (A to B), or a small cross at A: what 20 px of a hand cannot show — where its fingers go, its palm faces
        internal sealed class Mark { public Vector3 A, B; public Color32 C; public bool Cross; }
        private static readonly string[] WholeViews = { "front", "side", "back", "three", "top", "left" }; // (the whole body: too small for marks, they would hide it)

        // the marks over one frame, as that camera sees them (the camera has just rendered to the rig's texture: its pixels are the frame's)
        private static void Draw(Texture2D read, Camera cam, List<Mark> marks)
        {
            int px = read.width;
            foreach (Mark m in marks)
            {
                Vector3 a = cam.WorldToScreenPoint(m.A);
                if (a.z <= 0f) continue;
                if (m.Cross) { Line(read, (int)a.x - 6, (int)a.y, (int)a.x + 6, (int)a.y, m.C); Line(read, (int)a.x, (int)a.y - 6, (int)a.x, (int)a.y + 6, m.C); continue; }
                Vector3 b = cam.WorldToScreenPoint(m.B);
                if (b.z <= 0f) continue;
                Line(read, (int)a.x, (int)a.y, (int)b.x, (int)b.y, m.C);
                Line(read, (int)b.x - 2, (int)b.y - 2, (int)b.x + 2, (int)b.y + 2, m.C); Line(read, (int)b.x - 2, (int)b.y + 2, (int)b.x + 2, (int)b.y - 2, m.C); // (its end marked: which way it points)
            }
        }

        // words on a frame (a time, a rule, a step: what the frame is, read in the picture itself — review 2 2026-11-10): a 5×7 bitmap font, each
        // glyph five columns, the low bit its top row, ASCII only (what is not ASCII is drawn as «?»; a few signs are mapped), scale px a dot,
        // on a dark band so it reads on any background. Text(…) draws one line; Label(…) puts it in a cell of a sheet, at the top
        private static readonly byte[] Font = {
            0x00,0x00,0x00,0x00,0x00, 0x00,0x00,0x5F,0x00,0x00, 0x00,0x07,0x00,0x07,0x00, 0x14,0x7F,0x14,0x7F,0x14, 0x24,0x2A,0x7F,0x2A,0x12, 0x23,0x13,0x08,0x64,0x62, 0x36,0x49,0x55,0x22,0x50, 0x00,0x05,0x03,0x00,0x00,
            0x00,0x1C,0x22,0x41,0x00, 0x00,0x41,0x22,0x1C,0x00, 0x08,0x2A,0x1C,0x2A,0x08, 0x08,0x08,0x3E,0x08,0x08, 0x00,0x50,0x30,0x00,0x00, 0x08,0x08,0x08,0x08,0x08, 0x00,0x60,0x60,0x00,0x00, 0x20,0x10,0x08,0x04,0x02,
            0x3E,0x51,0x49,0x45,0x3E, 0x00,0x42,0x7F,0x40,0x00, 0x42,0x61,0x51,0x49,0x46, 0x21,0x41,0x45,0x4B,0x31, 0x18,0x14,0x12,0x7F,0x10, 0x27,0x45,0x45,0x45,0x39, 0x3C,0x4A,0x49,0x49,0x30, 0x01,0x71,0x09,0x05,0x03,
            0x36,0x49,0x49,0x49,0x36, 0x06,0x49,0x49,0x29,0x1E, 0x00,0x36,0x36,0x00,0x00, 0x00,0x56,0x36,0x00,0x00, 0x00,0x08,0x14,0x22,0x41, 0x14,0x14,0x14,0x14,0x14, 0x41,0x22,0x14,0x08,0x00, 0x02,0x01,0x51,0x09,0x06,
            0x32,0x49,0x79,0x41,0x3E, 0x7E,0x11,0x11,0x11,0x7E, 0x7F,0x49,0x49,0x49,0x36, 0x3E,0x41,0x41,0x41,0x22, 0x7F,0x41,0x41,0x22,0x1C, 0x7F,0x49,0x49,0x49,0x41, 0x7F,0x09,0x09,0x01,0x01, 0x3E,0x41,0x41,0x51,0x32,
            0x7F,0x08,0x08,0x08,0x7F, 0x00,0x41,0x7F,0x41,0x00, 0x20,0x40,0x41,0x3F,0x01, 0x7F,0x08,0x14,0x22,0x41, 0x7F,0x40,0x40,0x40,0x40, 0x7F,0x02,0x04,0x02,0x7F, 0x7F,0x04,0x08,0x10,0x7F, 0x3E,0x41,0x41,0x41,0x3E,
            0x7F,0x09,0x09,0x09,0x06, 0x3E,0x41,0x51,0x21,0x5E, 0x7F,0x09,0x19,0x29,0x46, 0x46,0x49,0x49,0x49,0x31, 0x01,0x01,0x7F,0x01,0x01, 0x3F,0x40,0x40,0x40,0x3F, 0x1F,0x20,0x40,0x20,0x1F, 0x7F,0x20,0x18,0x20,0x7F,
            0x63,0x14,0x08,0x14,0x63, 0x03,0x04,0x78,0x04,0x03, 0x61,0x51,0x49,0x45,0x43, 0x00,0x00,0x7F,0x41,0x41, 0x02,0x04,0x08,0x10,0x20, 0x41,0x41,0x7F,0x00,0x00, 0x04,0x02,0x01,0x02,0x04, 0x40,0x40,0x40,0x40,0x40,
            0x00,0x01,0x02,0x04,0x00, 0x20,0x54,0x54,0x54,0x78, 0x7F,0x48,0x44,0x44,0x38, 0x38,0x44,0x44,0x44,0x20, 0x38,0x44,0x44,0x48,0x7F, 0x38,0x54,0x54,0x54,0x18, 0x08,0x7E,0x09,0x01,0x02, 0x08,0x14,0x54,0x54,0x3C,
            0x7F,0x08,0x04,0x04,0x78, 0x00,0x44,0x7D,0x40,0x00, 0x20,0x40,0x44,0x3D,0x00, 0x00,0x7F,0x10,0x28,0x44, 0x00,0x41,0x7F,0x40,0x00, 0x7C,0x04,0x18,0x04,0x78, 0x7C,0x08,0x04,0x04,0x78, 0x38,0x44,0x44,0x44,0x38,
            0x7C,0x14,0x14,0x14,0x08, 0x08,0x14,0x14,0x18,0x7C, 0x7C,0x08,0x04,0x04,0x08, 0x48,0x54,0x54,0x54,0x20, 0x04,0x3F,0x44,0x40,0x20, 0x3C,0x40,0x40,0x20,0x7C, 0x1C,0x20,0x40,0x20,0x1C, 0x3C,0x40,0x30,0x40,0x3C,
            0x44,0x28,0x10,0x28,0x44, 0x0C,0x50,0x50,0x50,0x3C, 0x44,0x64,0x54,0x4C,0x44, 0x00,0x08,0x36,0x41,0x00, 0x00,0x00,0x7F,0x00,0x00, 0x00,0x41,0x36,0x08,0x00, 0x10,0x08,0x08,0x10,0x08 };
        internal const int TextScale = 2; // (a dot 2×2 px: 12 px a glyph, 40 glyphs across a 480 px frame)
        private static int Glyph(char c)
        {
            if (c >= ' ' && c <= '~') return c - ' ';
            switch (c) { case '×': return 'x' - ' '; case '✗': return 'X' - ' '; case '—': case '–': case '−': return '-' - ' '; case '«': case '»': return '"' - ' '; case '·': return '.' - ' '; case '°': return '^' - ' '; case '→': return '>' - ' '; }
            return '?' - ' ';
        }
        internal static void Text(Texture2D t, int x, int y, string s, Color32 c, int scale = TextScale)
        {
            int w = s.Length * 6 * scale, h = 8 * scale;
            Color32 band = new Color32(0, 0, 0, 255);
            for (int py = y - scale; py < y + h; py++) for (int px = x - scale; px < x + w; px++) if (px >= 0 && py >= 0 && px < t.width && py < t.height) t.SetPixel(px, py, band);
            for (int i = 0; i < s.Length; i++)
            {
                int g = Glyph(s[i]) * 5;
                for (int col = 0; col < 5; col++)
                {
                    int bits = Font[g + col];
                    for (int row = 0; row < 7; row++)
                    {
                        if ((bits & (1 << row)) == 0) continue;
                        for (int dx = 0; dx < scale; dx++) for (int dy = 0; dy < scale; dy++) { int px = x + (i * 6 + col) * scale + dx, py = y + (6 - row) * scale + dy; if (px >= 0 && py >= 0 && px < t.width && py < t.height) t.SetPixel(px, py, c); }
                    }
                }
            }
        }
        // one or two lines of words at the top of a cell of a sheet (cell: left to right, top to bottom, cols a row, rows in all, px a cell)
        internal static void Label(Texture2D sheet, int cell, int cols, int rows, int px, string line1, string line2 = null)
        {
            int x = (cell % cols) * px + 4, top = (rows - 1 - cell / cols) * px + px - 4;
            int most = px / (6 * TextScale) - 1;
            if (line1 != null) Text(sheet, x, top - 8 * TextScale, line1.Length > most ? line1.Substring(0, most) : line1, new Color32(255, 230, 120, 255));
            if (line2 != null) Text(sheet, x, top - 17 * TextScale, line2.Length > most ? line2.Substring(0, most) : line2, new Color32(235, 235, 235, 255));
        }

        private static void Line(Texture2D t, int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            for (int n = 0; n < 4096; n++)
            {
                for (int ox = 0; ox < 2; ox++) for (int oy = 0; oy < 2; oy++) { int x = x0 + ox, y = y0 + oy; if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c); }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private static Rig Make(string views, int size = Cell)
        {
            Rig r = new Rig { Size = size };
            r.Root = new GameObject("RavenWatch_Studio");
            Camera main = Camera.main;
            foreach (string v in views.Split(','))
            {
                string view = v.Trim().ToLowerInvariant();
                if (view.Length == 0) continue;
                Camera c = new GameObject("Cam_" + view).AddComponent<Camera>();
                c.transform.SetParent(r.Root.transform, false);
                c.enabled = false;
                c.fieldOfView = 30f;
                c.nearClipPlane = 0.02f; c.farClipPlane = 60f;
                if (main != null) { c.cullingMask = main.cullingMask; c.clearFlags = main.clearFlags; c.backgroundColor = main.backgroundColor; }
                r.Cams.Add(c); r.Views.Add(view);
            }
            r.Rt = new RenderTexture(r.Size, r.Size, 24, RenderTextureFormat.ARGB32);
            r.Lamp = new GameObject("Lamp").AddComponent<Light>();
            r.Lamp.transform.SetParent(r.Root.transform, false);
            r.Lamp.type = LightType.Point; r.Lamp.range = 4f; r.Lamp.intensity = 2.4f; r.Lamp.color = new Color(1f, 0.97f, 0.92f); r.Lamp.shadows = LightShadows.None; r.Lamp.enabled = false;
            return r;
        }

        private static void Drop(Rig r)
        {
            if (r == null) return;
            foreach (Camera c in r.Cams) if (c != null) c.targetTexture = null;
            if (r.Rt != null) { r.Rt.Release(); UnityEngine.Object.Destroy(r.Rt); }
            if (r.Root != null) UnityEngine.Object.Destroy(r.Root);
        }

        // the cameras round the raven: its outer side, in front of it, from above, from behind (as you see it)
        private static void Place(Rig r, GameObject model, Player p, bool munin)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Place(r, model, fwd, munin ? -right : right);
        }
        private static void Place(Rig r, GameObject model, Vector3 fwd, Vector3 outer, float lift = 0f)
        {
            Bounds b = Bounds(model);
            Vector3 mid = b.center;
            float size = Mathf.Clamp(b.extents.magnitude, 0.12f, 0.6f);
            Transform body = Bone(model, "Spine");
            if (body != null) { mid = body.position; size = Mathf.Min(size, 0.45f); } // (a skinned bound lags behind a flying bird: the body itself is the middle)
            float d = Mathf.Clamp(size * 3.2f, 0.45f, 1.8f); // (the bird fills the picture, a little room round its wings)
            // the eyes as measured (LookDiag), in the world: for the close-ups of the head
            Transform neck = Bone(model, LookDiag.HeadBone), jaw = Bone(model, "Jaw1");
            LookDiag.EyeSpot[] eyes = LookDiag.Eyes(model.name.IndexOf("Munin", StringComparison.Ordinal) >= 0);
            bool haveEyes = neck != null && eyes != null;
            Vector3 e0 = haveEyes ? neck.TransformPoint(eyes[0].At) : mid, e1 = haveEyes ? neck.TransformPoint(eyes[1].At) : mid, between = (e0 + e1) * 0.5f;
            float sep = Mathf.Max(0.01f, Vector3.Distance(e0, e1));
            for (int i = 0; i < r.Cams.Count; i++)
            {
                Vector3 from;
                // the head close up: face (between the eyes, from the beak's side), eye0, eye1 (each eye from outside)
                string hv = r.Views[i];
                if (haveEyes && (hv == "face" || hv == "eye0" || hv == "eye1"))
                {
                    Vector3 at = hv == "eye0" ? e0 : hv == "eye1" ? e1 : between, away;
                    if (hv == "face")
                    {
                        away = jaw != null ? Vector3.ProjectOnPlane(jaw.position - between, e0 - e1) : Vector3.Cross(e0 - e1, Vector3.up);
                        if (away.sqrMagnitude < 1e-8f) away = Vector3.Cross(e0 - e1, Vector3.up);
                    }
                    else away = at - between;
                    from = at + away.normalized * sep * 7f + Vector3.up * sep;
                    r.Cams[i].nearClipPlane = 0.005f;
                    r.Cams[i].transform.position = from;
                    r.Cams[i].transform.rotation = Quaternion.LookRotation(at - from, Vector3.up);
                    continue;
                }
                switch (r.Views[i])
                {
                    case "front": from = mid + fwd * d + Vector3.up * size * 0.4f + outer * size * 0.6f; break;
                    case "top": from = mid + Vector3.up * d + fwd * size * 0.5f + outer * size * 0.3f; break;
                    case "back": from = mid - fwd * d + Vector3.up * size * 0.9f + outer * size * 0.8f; break;
                    case "inner": from = mid - outer * d * 0.55f + Vector3.up * d * 0.85f - fwd * size * 0.4f; break; // (over your head, looking down at its inner side)
                    default: from = mid + outer * d + Vector3.up * size * 0.3f; break; // side
                }
                if (r.Views[i] != "top") from += Vector3.up * size * lift;
                r.Cams[i].transform.position = from;
                r.Cams[i].transform.rotation = Quaternion.LookRotation(mid - from, r.Views[i] == "top" ? fwd : Vector3.up); // (from above: your way is up on the picture)
            }
            r.Mid = mid;
        }

        private static Transform Bone(GameObject model, string name)
        {
            return model != null ? RavenBones.Find(model.transform, name) : null;
        }

        private static Bounds Bounds(GameObject model)
        {
            Renderer[] rs = model.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(model.transform.position, Vector3.one * 0.2f);
            bool first = true;
            foreach (Renderer x in rs)
            {
                if (x == null || !x.enabled || !(x is SkinnedMeshRenderer)) continue; // (the bird's body itself: no things on it, trails, auras, sparks)
                if (first) { b = x.bounds; first = false; } else b.Encapsulate(x.bounds);
            }
            return b;
        }

        // One moment from every camera. One sheet: the cameras side by side in row 'cell' (look). A sheet per camera:
        // each into its own grid of 'cols' columns, at place 'cell' (left to right, top to bottom). The lamp stands by
        // the camera that shoots (the bird lit from where it is seen, whatever the night).
        private static void Shoot(Rig r, Texture2D[] sheets, int cell, int cols, int rows)
        {
            int px = r.Size;
            Texture2D read = new Texture2D(px, px, TextureFormat.RGB24, false);
            RenderTexture was = RenderTexture.active;
            r.Lamp.enabled = true;
            List<Renderer> weather = HideWeather();
            GameObject grass = GrassRoot(); // (the grass between the camera and the feet: left out too)
            bool grassWas = grass != null && grass.activeSelf;
            if (grassWas) grass.SetActive(false);
            List<Mark> marks = r.Marks != null ? r.Marks() : null;
            try
            {
                for (int i = 0; i < r.Cams.Count; i++)
                {
                    Frame(r, i, read, marks);
                    if (sheets.Length < r.Cams.Count) Blit(read, sheets[0], i * px, (rows - 1 - cell) * px); // one strip of all the views (look)
                    else Blit(read, sheets[i], (cell % cols) * px, (rows - 1 - cell / cols) * px);
                }
            }
            finally
            {
                foreach (Renderer w in weather) if (w != null) w.enabled = true;
                if (grassWas && grass != null) grass.SetActive(true);
                r.Lamp.enabled = false;
                RenderTexture.active = was;
                UnityEngine.Object.Destroy(read);
            }
        }

        // every view of the rig into ONE sheet, side by side from column col0 of row 'row' (the stage's key sheet: a row a moment, its two views;
        // beside them the same views of another body — the last take)
        private static void ShootRow(Rig r, Texture2D sheet, int col0, int row, int rows)
        {
            int px = r.Size;
            Texture2D read = new Texture2D(px, px, TextureFormat.RGB24, false);
            RenderTexture was = RenderTexture.active;
            r.Lamp.enabled = true;
            List<Renderer> weather = HideWeather();
            GameObject grass = GrassRoot();
            bool grassWas = grass != null && grass.activeSelf;
            if (grassWas) grass.SetActive(false);
            List<Mark> marks = r.Marks != null ? r.Marks() : null;
            try { for (int i = 0; i < r.Cams.Count; i++) { Frame(r, i, read, marks); Blit(read, sheet, (col0 + i) * px, (rows - 1 - row) * px); } }
            finally
            {
                foreach (Renderer w in weather) if (w != null) w.enabled = true;
                if (grassWas && grass != null) grass.SetActive(true);
                r.Lamp.enabled = false;
                RenderTexture.active = was;
                UnityEngine.Object.Destroy(read);
            }
        }

        // one view of the rig rendered into 'read' (the lamp by that camera; the marks over a close view)
        private static void Frame(Rig r, int i, Texture2D read, List<Mark> marks)
        {
            int px = r.Size;
            Vector3 cam = r.Cams[i].transform.position;
            r.Lamp.transform.position = cam + (r.Mid - cam) * 0.3f + Vector3.up * 0.25f;
            r.Cams[i].targetTexture = r.Rt;
            r.Cams[i].Render();
            RenderTexture.active = r.Rt;
            read.ReadPixels(new Rect(0, 0, px, px), 0, 0);
            if (marks != null && marks.Count > 0 && System.Array.IndexOf(WholeViews, r.Views[i]) < 0) Draw(read, r.Cams[i], marks); // (a close view: the hands' ways and the targets drawn over it)
            read.Apply(false);
            r.Cams[i].targetTexture = null;
        }

        // the weather's particles now (rain, snow, ash: EnvMan's current systems) hidden for the studio's shot; the
        // ones hidden are given back right after
        private static readonly System.Reflection.FieldInfo WeatherField = HarmonyLib.AccessTools.Field(typeof(EnvMan), "m_currentPSystems");
        private static readonly System.Reflection.FieldInfo GrassField = HarmonyLib.AccessTools.Field(typeof(ClutterSystem), "m_grassRoot");
        private static GameObject GrassRoot() { return ClutterSystem.instance != null && GrassField != null ? GrassField.GetValue(ClutterSystem.instance) as GameObject : null; }
        private static List<Renderer> HideWeather()
        {
            List<Renderer> off = new List<Renderer>();
            GameObject[] systems = EnvMan.instance != null && WeatherField != null ? WeatherField.GetValue(EnvMan.instance) as GameObject[] : null;
            if (systems == null) return off;
            foreach (GameObject g in systems)
            {
                if (g == null) continue;
                foreach (Renderer x in g.GetComponentsInChildren<Renderer>()) if (x != null && x.enabled) { x.enabled = false; off.Add(x); }
            }
            return off;
        }

        private static IEnumerator Look(string name, string which, string views)
        {
            bool munin = which.StartsWith("m");
            GameObject model = Shoulder.LocalModel(munin);
            Player p = Player.m_localPlayer;
            if (model == null || p == null) { Note(name, "no such raven on your shoulder"); yield break; }
            Rig r = Make(views);
            Texture2D sheet = new Texture2D(Cell * r.Cams.Count, Cell, TextureFormat.RGB24, false);
            try
            {
                yield return new WaitForEndOfFrame();
                Place(r, model, p, munin);
                Shoot(r, new[] { sheet }, 0, r.Cams.Count, 1);
                Save(name, sheet);
                Note(name, "the raven as it sits now; columns: " + string.Join(", ", r.Views.ToArray()) + "\nfit on what you wear: " + ShoulderFit.Info(p));
            }
            finally { UnityEngine.Object.Destroy(sheet); Drop(r); }
        }

        // who is shot: your raven on your shoulder (h, m) or the nearest on a perch (p, ph, pm)
        private sealed class Subject
        {
            internal GameObject Model;
            internal ISitBird Bird;
            internal SitDirector Dir, OtherDir;
            internal PerchLife Perch;
            internal ShoulderLife Life;
            internal bool Munin;
            internal string Who;
            internal bool Still, Placed;
            internal Vector3 PlacedAt; // Still: the cameras put once (shot in time: the move seen against what it sits on)
            internal void Place(Rig r, Player p)
            {
                if (Still && Placed && Model != null && (Model.transform.position - PlacedAt).sqrMagnitude < 0.25f) return; // (held still for a move on its seat; gone far — you fell, ran — it is followed)
                Placed = true;
                if (Model != null) PlacedAt = Model.transform.position;
                if (Perch == null) { Studio.Place(r, Model, p, Munin); return; }
                Vector3 fwd = Vector3.ProjectOnPlane(Perch.transform.forward, Vector3.up);
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                fwd.Normalize();
                Studio.Place(r, Model, fwd, Vector3.Cross(Vector3.up, fwd), 1.6f);
            }
        }
        private static Subject Find(string which, out string why)
        {
            why = null;
            Player p = Player.m_localPlayer;
            if (p == null) { why = "no player"; return null; }
            which = which.ToLowerInvariant();
            if (which.StartsWith("p"))
            {
                PerchLife best = null; float bd = 900f;
                foreach (PerchLife l in UnityEngine.Object.FindObjectsByType<PerchLife>(FindObjectsSortMode.None))
                {
                    if (l == null || !l.isActiveAndEnabled) continue;
                    if ((which == "ph" && l.Munin) || (which == "pm" && !l.Munin)) continue;
                    float dd = (l.transform.position - p.transform.position).sqrMagnitude;
                    if (dd < bd) { bd = dd; best = l; }
                }
                if (best == null) { why = "no raven on a perch within 30 m"; return null; }
                ISitBird pb = best;
                ISitBird po = pb.BirdPartner;
                return new Subject { Model = best.gameObject, Bird = pb, Dir = best.SitDirectorNow, OtherDir = po != null ? po.BirdDir : null, Perch = best, Munin = best.Munin, Who = (best.Munin ? "Munin" : "Hugin") + " on a perch" };
            }
            bool munin = which.StartsWith("m");
            ShoulderLife life = Shoulder.LocalLife(munin);
            GameObject model = Shoulder.LocalModel(munin);
            if (life == null || model == null || !life.SitNew) { why = "the raven must sit on your shoulder"; return null; }
            ShoulderLife other = Shoulder.LocalLife(!munin);
            return new Subject { Model = model, Bird = life, Life = life, Dir = life.SitDirectorNow, OtherDir = other != null && other.SitNew ? other.SitDirectorNow : null, Munin = munin, Who = munin ? "Munin" : "Hugin" };
        }

        // a flight followed: waits for the raven to leave its seat (or to come flying in), then shoots it as it goes, each
        // frame wherever it is — your shoulder's bird, or its sitter on a perch once it has landed there
        private static IEnumerator Fly(string name, string which, float seconds, float fps, string views, float giveUp, float slow = 1f)
        {
            bool munin = which.ToLowerInvariant().StartsWith("m");
            Player p = Player.m_localPlayer;
            if (p == null) { Note(name, "no player"); yield break; }
            GameObject was = Shoulder.LocalModel(munin);
            RavenAct actWas = AnimDirector.Of(was);
            float until = Time.time + Mathf.Clamp(giveUp, 5f, 300f);
            if (giveUp > 0f) Note(name, "waiting for " + (munin ? "Munin" : "Hugin") + " to take wing…");
            while (giveUp > 0f) // (0: the raven as it is now, in the air already)
            {
                if (Time.time > until) { Note(name, "it did not fly in " + Mathf.RoundToInt(giveUp) + " s"); yield break; }
                GameObject m = Shoulder.LocalModel(munin);
                RavenAct act = AnimDirector.Of(m);
                if ((m != null && was == null) || (m != null && act != RavenAct.Sit && actWas == RavenAct.Sit) || (m != null && m != was) || PerchBird(munin) != null) break;
                was = m; actWas = act;
                yield return null;
            }
            if (seconds * fps > 32f) fps = 32f / Mathf.Max(0.5f, seconds);
            int rows = Mathf.Clamp(Mathf.RoundToInt(seconds * fps), 2, 32), cols = Mathf.Min(4, rows), gridRows = (rows + cols - 1) / cols;
            Rig r = Make(views);
            Texture2D[] sheets = new Texture2D[r.Cams.Count];
            StringBuilder acts = new StringBuilder();
            string last = "";
            float scaleWas = Time.timeScale;
            if (slow < 1f) Time.timeScale = slow; // (off now: slowed from here, the wait was at full pace)
            Stroke stroke = new Stroke();
            try
            {
                for (int v = 0; v < sheets.Length; v++) { sheets[v] = new Texture2D(Cell * cols, Cell * gridRows, TextureFormat.RGB24, false); Fill(sheets[v], new Color32(20, 20, 20, 255)); }
                float start = Time.time;
                for (int i = 0; i < rows; i++)
                {
                    while (Time.time - start < i / Mathf.Max(0.5f, fps)) yield return null;
                    yield return new WaitForEndOfFrame();
                    GameObject m = Shoulder.LocalModel(munin);
                    string what;
                    if (m != null)
                    {
                        ShoulderLife life = m.GetComponent<ShoulderLife>();
                        what = AnimDirector.Of(m) + (life != null && life.WingWhy.Length > 0 ? " · wings: " + life.WingWhy : "") + (life != null ? " · " + life.LegWhy : "") + stroke.Take(m);
                        Place(r, m, p, munin);
                    }
                    else if (PerchBird(munin) != null) { PerchFlight pf = PerchBird(munin); what = "flying to the perch"; Place(r, pf.gameObject, p, munin); }
                    else
                    {
                        string why;
                        Subject s = Find(munin ? "pm" : "ph", out why);
                        if (s == null) { what = "out of sight"; }
                        else { what = "on the perch"; s.Place(r, p); }
                    }
                    Shoot(r, sheets, i, cols, gridRows);
                    if (true) { acts.Append("  frame ").Append(i + 1).Append(" (").Append((Time.time - start).ToString("0.0", CultureInfo.InvariantCulture)).Append(" s): ").Append(what).Append('\n'); last = what; }
                }
                for (int v = 0; v < sheets.Length; v++) Save(name + "_" + r.Views[v], sheets[v]);
            }
            finally
            {
                if (slow < 1f) Time.timeScale = scaleWas;
                foreach (Texture2D t in sheets) if (t != null) UnityEngine.Object.Destroy(t);
                Drop(r);
            }
            acts.Append(stroke.Summary());
            Note(name, (munin ? "Munin" : "Hugin") + " in flight, " + rows + " frames, " + fps.ToString("0.#", CultureInfo.InvariantCulture) + " a second, 4 a row; sheets " + name + "_<view>.png for " + string.Join(", ", r.Views.ToArray()) + "\nwhat it was doing:\n" + acts);
        }

        // the wing beat measured: the tip (wing3) from the shoulder joint (Wing1), in the body's frame (forward, up, out;
        // cm), each frame, and its range over the shots — where the stroke sits along the body
        private sealed class Stroke
        {
            private GameObject _for;
            private Transform _body, _sh, _tip;
            private float _fMin = float.MaxValue, _fMax = float.MinValue, _uMin = float.MaxValue, _uMax = float.MinValue, _fSum;
            private int _n;
            internal string Take(GameObject m)
            {
                if (m != _for)
                {
                    _for = m; _body = _sh = _tip = null;
                    foreach (Transform t in RavenBones.Of(m.transform, false))
                    {
                        string n = t.name.ToLowerInvariant();
                        if (n == "spine") _body = t; else if (n == "wing1.l") _sh = t; else if (n == "wing3.l") _tip = t;
                    }
                }
                if (_body == null || _sh == null || _tip == null) return "";
                Quaternion facing = m.transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
                Vector3 d = Quaternion.Inverse(facing) * (_tip.position - _sh.position);
                float side = Mathf.Max(0.0001f, Mathf.Abs(d.x));
                float fw = Mathf.Atan2(d.z, side) * Mathf.Rad2Deg, up = Mathf.Atan2(d.y, side) * Mathf.Rad2Deg; // (° off straight out sideways: + forward / + up)
                _fMin = Mathf.Min(_fMin, fw); _fMax = Mathf.Max(_fMax, fw); _uMin = Mathf.Min(_uMin, up); _uMax = Mathf.Max(_uMax, up); _fSum += fw; _n++;
                return " · wing sweep " + Mathf.RoundToInt(fw) + "° up " + Mathf.RoundToInt(up) + "°";
            }
            internal string Summary()
            {
                if (_n == 0) return "";
                return "\nleft wing tip seen from its shoulder joint, ° off straight out sideways (+ swept forward / + raised): sweep " + Mathf.RoundToInt(_fMin) + "…" + Mathf.RoundToInt(_fMax) + " (mean " + Mathf.RoundToInt(_fSum / _n) + "), up " + Mathf.RoundToInt(_uMin) + "…" + Mathf.RoundToInt(_uMax) + "\n";
            }
        }

        // your raven on its way to a perch (its own bird, not your shoulder's)
        private static PerchFlight PerchBird(bool munin)
        {
            Player me = Player.m_localPlayer;
            foreach (PerchFlight f in UnityEngine.Object.FindObjectsByType<PerchFlight>(FindObjectsSortMode.None))
                if (f != null && f.isActiveAndEnabled && f.Munin == munin && (f.Owner == null || ReferenceEquals(f.Owner, me))) return f;
            return null;
        }

        // the raven living as it does (its own doings, steps, shakes, looks): nothing asked of it, shot in time
        private static IEnumerator Film(string name, string which, float seconds, float fps, string views)
        {
            string why;
            Subject s = Find(which, out why);
            Player p = Player.m_localPlayer;
            if (s == null) { Note(name, why); yield break; }
            s.Still = true;
            Rig r = Make(views);
            Bones bones = new Bones(s.Model, p);
            if (seconds * fps > 32f) fps = 32f / Mathf.Max(0.5f, seconds); // (the whole time, fewer frames a second: not its first seconds only)
            int rows = Mathf.Clamp(Mathf.RoundToInt(seconds * fps), 2, 32), cols = Mathf.Min(4, rows), gridRows = (rows + cols - 1) / cols;
            Texture2D[] sheets = new Texture2D[r.Cams.Count];
            StringBuilder acts = new StringBuilder();
            string lastAct = "";
            StringBuilder seat = new StringBuilder(); // (on your shoulder: how it rides it)
            try
            {
                for (int v = 0; v < sheets.Length; v++) { sheets[v] = new Texture2D(Cell * cols, Cell * gridRows, TextureFormat.RGB24, false); Fill(sheets[v], new Color32(20, 20, 20, 255)); }
                float start = Time.time;
                for (int i = 0; i < rows; i++)
                {
                    while (Time.time - start < i / Mathf.Max(0.5f, fps)) yield return null;
                    yield return new WaitForEndOfFrame();
                    if (s.Model == null) break; // (gone: flew off)
                    s.Place(r, p);
                    Shoot(r, sheets, i, cols, gridRows);
                    bones.Take(Time.time - start);
                    string act = (s.Dir != null && s.Dir.Run != null ? s.Dir.Run.T.Name : "—") + (s.Dir != null && s.Dir.Doze > 0.5f ? " (dozing)" : "") + (s.Bird != null && s.Bird.BirdDroopy ? " (drooping: starving, sulking or cold)" : "") + (s.Life != null && s.Life.WingWhy.Length > 0 ? " · wings: " + s.Life.WingWhy : "") + (s.Dir != null && s.Dir.Why.Length > 0 ? " · " + s.Dir.Why : "");
                    if (act != lastAct) { acts.Append("  frame ").Append(i + 1).Append(": ").Append(act).Append('\n'); lastAct = act; }
                    ShoulderPin pin = s.Model.GetComponent<ShoulderPin>();
                    if (pin != null && p != null)
                        seat.Append("  ").Append((Time.time - start).ToString("0.00", CultureInfo.InvariantCulture)).Append(" s: seat ").Append(Mathf.RoundToInt((pin.SeatNow.y - p.transform.position.y) * 1000f))
                            .Append(" mm over your feet, shoulder tip ").Append(pin.RideAngle.ToString("0.0", CultureInfo.InvariantCulture)).Append("°\n");
                }
                for (int v = 0; v < sheets.Length; v++) Save(name + "_" + r.Views[v], sheets[v]);
            }
            finally
            {
                foreach (Texture2D t in sheets) if (t != null) UnityEngine.Object.Destroy(t);
                Drop(r);
            }
            Note(name, s.Who + " living as it does, " + rows + " frames, " + fps.ToString("0.#", CultureInfo.InvariantCulture) + " a second, 4 a row, left to right, top to bottom; sheets "
                + name + "_<view>.png for " + string.Join(", ", r.Views.ToArray()) + "\nwhat it was doing:\n" + acts + (seat.Length > 0 ? "\nriding your shoulder:\n" + seat : "") + "\n" + bones.Report(true));
        }

        // a template on your raven: held moment by moment (pose), or played in time (play)
        private static IEnumerator Pose(string name, string template, string which, int moments, string views, bool play, float fps)
        {
            SitTemplate t = SitTemplates.Find(template) ?? SitTemplates.Find(template.Replace('_', ' ')); // (its exact name first: some have _ in them)
            Player p = Player.m_localPlayer;
            if (t == null) { Note(name, "no template «" + template + "» (spaces as _)"); yield break; }
            string why;
            Subject s = Find(which, out why);
            if (s == null) { Note(name, why); yield break; }
            s.Still = play;
            GameObject model = s.Model;
            SitDirector d = s.Dir;
            SitDirector od = s.OtherDir;
            Rig r = Make(views);
            Bones bones = new Bones(model, p);
            List<float> at = new List<float>();
            Texture2D[] sheets = null;
            bool heldOther = false;
            try
            {
                // a calm, still scene: its mood fixed (no drowsy head, no alert hunch), the other raven left idle (no
                // doing of its own in the picture, no answer to this one)
                d.ForceMood = (int)SitMood.Calm;
                if (od != null && !od.Playing && od.HoldU < 0f) { od.HoldU = 0f; heldOther = true; }
                d.HoldU = play ? -1f : 0f;
                d.PlayNow(s.Bird, t);
                int rows;
                if (!play)
                {
                    rows = Mathf.Clamp(moments, 2, 12);
                    for (int i = 0; i < rows; i++) at.Add(i / (float)(rows - 1));
                }
                else
                {
                    float total = (d.Run != null ? d.Run.Seconds : Mathf.Max(0.5f, t.SecMax)) + 0.3f; // (its own length, a clip's too)
                    rows = Mathf.Clamp(Mathf.RoundToInt(total * fps), 2, 16);
                    for (int i = 0; i < rows; i++) at.Add(i / Mathf.Max(1f, fps));
                }
                // a sheet per view, the moments in a grid of four columns
                int cols = Mathf.Min(4, rows), gridRows = (rows + cols - 1) / cols;
                sheets = new Texture2D[r.Cams.Count];
                for (int v = 0; v < sheets.Length; v++) { sheets[v] = new Texture2D(Cell * cols, Cell * gridRows, TextureFormat.RGB24, false); Fill(sheets[v], new Color32(20, 20, 20, 255)); }
                if (!play) { float settle = Time.time + Mathf.Max(0.4f, t.In); while (Time.time < settle) yield return null; } // (what played before has faded out)
                float start = Time.time;
                for (int i = 0; i < rows; i++)
                {
                    if (!play) d.HoldU = at[i];
                    else while (Time.time - start < at[i]) yield return null;
                    yield return null; yield return null; // (the pose is written in the next frames)
                    yield return new WaitForEndOfFrame();
                    s.Place(r, p);
                    Shoot(r, sheets, i, cols, gridRows);
                    bones.Take(play ? Time.time - start : at[i]);
                }
                for (int v = 0; v < sheets.Length; v++) Save(name + "_" + r.Views[v], sheets[v]);
            }
            finally
            {
                d.HoldU = -1f;
                d.ForceMood = -1;
                if (heldOther && od != null) od.HoldU = -1f;
                if (sheets != null) foreach (Texture2D sh in sheets) if (sh != null) UnityEngine.Object.Destroy(sh);
                Drop(r);
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(play ? "«" + t.Name + "» played in time, " + fps.ToString("0.#", CultureInfo.InvariantCulture) + " frames a second; rows: frames at "
                           : "«" + t.Name + "» held moment by moment (fully, no fades); rows: moments at ");
            for (int i = 0; i < at.Count; i++) sb.Append(i > 0 ? ", " : "").Append(play ? at[i].ToString("0.00", CultureInfo.InvariantCulture) + " s" : Mathf.RoundToInt(at[i] * 100f) + "%");
            sb.Append("\none sheet per view: ").Append(name).Append("_<view>.png for ").Append(string.Join(", ", r.Views.ToArray()))
              .Append("; on each, 4 a row, left to right, top to bottom (").Append(s.Who).Append(")\n\n");
            sb.Append(bones.Report(play));
            Note(name, sb.ToString());
        }

        // ---------------------------------------------------------------- a thing happening to you

        private static readonly string[] EventWords = { "hurt", "boss", "kill", "work", "eat", "pick", "pet", "fall" }; // (SitEv's order)

        // waits for it, then shoots the raven from the views and the screen as it goes on (the reaction in real life)
        private static IEnumerator Wait(string name, string what, string which, float seconds, float fps, string views, float giveUp)
        {
            what = what.ToLowerInvariant();
            bool munin = which.StartsWith("m");
            int seen = LastEvent();
            float until = Time.time + Mathf.Clamp(giveUp, 5f, 300f);
            Note(name, "waiting for «" + what + "»…");
            while (true)
            {
                if (Time.time > until) { Note(name, "nothing of «" + what + "» happened in " + Mathf.RoundToInt(giveUp) + " s"); yield break; }
                Player me = Player.m_localPlayer;
                if (me != null && Happened(me, what, ref seen)) break;
                yield return null;
            }
            Player p = Player.m_localPlayer;
            GameObject model = Shoulder.LocalModel(munin);
            if (p == null || model == null) { Note(name, "it happened, but no such raven on your shoulder"); yield break; }
            Rig r = Make(views);
            Bones bones = new Bones(model, p);
            int rows = Mathf.Clamp(Mathf.RoundToInt(seconds * fps), 2, 16), cols = Mathf.Min(4, rows), gridRows = (rows + cols - 1) / cols;
            Texture2D[] sheets = new Texture2D[r.Cams.Count];
            Texture2D screen = null;
            const int ScreenW = 480;
            try
            {
                for (int v = 0; v < sheets.Length; v++) { sheets[v] = new Texture2D(Cell * cols, Cell * gridRows, TextureFormat.RGB24, false); Fill(sheets[v], new Color32(20, 20, 20, 255)); }
                float start = Time.time;
                for (int i = 0; i < rows; i++)
                {
                    while (Time.time - start < i / Mathf.Max(1f, fps)) yield return null;
                    yield return new WaitForEndOfFrame();
                    Texture2D s = ScreenCapture.CaptureScreenshotAsTexture(); // (the screen first: before the studio's cameras touch the frame)
                    Texture2D small = Shrink(s, ScreenW);
                    if (screen == null) { screen = new Texture2D(small.width * cols, small.height * gridRows, TextureFormat.RGB24, false); Fill(screen, new Color32(20, 20, 20, 255)); }
                    Blit(small, screen, (i % cols) * small.width, (gridRows - 1 - i / cols) * small.height);
                    UnityEngine.Object.Destroy(s); if (small != s) UnityEngine.Object.Destroy(small);
                    if (model == null) break; // (gone: flew off, taken off)
                    Place(r, model, p, munin);
                    Shoot(r, sheets, i, cols, gridRows);
                    bones.Take(Time.time - start);
                }
                for (int v = 0; v < sheets.Length; v++) Save(name + "_" + r.Views[v], sheets[v]);
                if (screen != null) Save(name + "_screen", screen);
            }
            finally
            {
                foreach (Texture2D t in sheets) if (t != null) UnityEngine.Object.Destroy(t);
                if (screen != null) UnityEngine.Object.Destroy(screen);
                Drop(r);
            }
            Note(name, "«" + what + "» happened; then " + rows + " frames, " + fps.ToString("0.#", CultureInfo.InvariantCulture) + " a second, 4 a row, left to right, top to bottom.\n"
                + "sheets: " + name + "_<view>.png for " + string.Join(", ", r.Views.ToArray()) + " (" + (munin ? "Munin" : "Hugin") + "), and " + name + "_screen.png (the game's screen)\n\n" + bones.Report(true));
        }

        private static int LastEvent() { int id = 0; foreach (SitEvents.E e in SitEvents.Recent) id = Mathf.Max(id, e.Id); return id; }

        // has it happened since 'seen'? the sitting system's events, or what your body does now (jump, dodge, swim)
        private static bool Happened(Player p, string what, ref int seen)
        {
            bool hit = false;
            foreach (SitEvents.E e in SitEvents.Recent)
            {
                if (e.Id <= seen) continue;
                seen = e.Id;
                int k = (int)e.Kind;
                if (what == "any" || (k >= 0 && k < EventWords.Length && EventWords[k] == what)) hit = true;
            }
            if (hit) return true;
            if ((what == "jump" || what == "any") && !p.IsOnGround() && p.GetVelocity().y > 2.5f) return true;
            if ((what == "dodge" || what == "any") && p.InDodge()) return true;
            if (what == "swim" && p.IsSwimming()) return true;
            return false;
        }

        // ---------------------------------------------------------------- the bones' report

        private sealed class Bones
        {
            private readonly List<Transform> _all = new List<Transform>();
            private readonly List<Transform> _wing = new List<Transform>();    // the wings' ends (to its own head)
            private readonly List<Transform> _wingAll = new List<Transform>(); // every wing bone (to your head: the wing's middle crosses it, not only its end)
            private readonly Transform _head, _root;
            private readonly Player _p;
            private readonly List<float> _times = new List<float>();
            private readonly List<Quaternion[]> _rot = new List<Quaternion[]>();
            private float _nearHead = float.MaxValue, _nearYou = float.MaxValue;
            private string _nearHeadAt = "", _nearYouAt = "";
            private float _restHead = -1f, _restYou = -1f; // the same at the first frame (a sitting raven is close to your head anyway: only what comes closer than that says something)

            internal Bones(GameObject model, Player p)
            {
                _p = p; _root = model.transform;
                // the skeleton itself (the skinned mesh's bones): not the effects' and things' objects hung on it
                HashSet<Transform> skel = new HashSet<Transform>();
                foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr != null && smr.bones != null) foreach (Transform t in smr.bones) if (t != null) skel.Add(t);
                foreach (Transform t in skel)
                {
                    _all.Add(t);
                    string n = t.name.ToLowerInvariant();
                    bool end = true; // a wing bone with no wing bone below it: the wing's end
                    foreach (Transform c in t) if (skel.Contains(c)) { end = false; break; }
                    if (n.StartsWith("wing") && end) _wing.Add(t);
                    if (n.StartsWith("wing")) _wingAll.Add(t);
                    if (_head == null && n == "head") _head = t;
                }
                if (_head == null) foreach (Transform t in _all) if (t.name.ToLowerInvariant().Contains("head")) { _head = t; break; }
                if (_head == null) foreach (Transform t in _all) if (t.name == "Neck") { _head = t; break; } // (this raven's skeleton ends at the neck)
            }

            internal void Take(float time)
            {
                _times.Add(time);
                Quaternion[] q = new Quaternion[_all.Count];
                for (int i = 0; i < _all.Count; i++) q[i] = _all[i] != null ? _all[i].localRotation : Quaternion.identity;
                _rot.Add(q);
                Vector3 you = _p != null ? _p.GetHeadPoint() : Vector3.zero;
                float frameHead = float.MaxValue, frameYou = float.MaxValue;
                if (_head != null)
                    foreach (Transform w in _wing)
                    {
                        if (w == null) continue;
                        float dh = Vector3.Distance(w.position, _head.position);
                        frameHead = Mathf.Min(frameHead, dh);
                        if (dh < _nearHead) { _nearHead = dh; _nearHeadAt = w.name + " @" + time.ToString("0.00", CultureInfo.InvariantCulture); }
                    }
                if (_p != null)
                    foreach (Transform w in _wingAll)
                    {
                        if (w == null) continue;
                        float dy = Vector3.Distance(w.position, you);
                        frameYou = Mathf.Min(frameYou, dy);
                        if (dy < _nearYou) { _nearYou = dy; _nearYouAt = w.name + " @" + time.ToString("0.00", CultureInfo.InvariantCulture); }
                    }
                if (_restHead < 0f) { _restHead = frameHead; _restYou = frameYou; }
            }

            // "12 cm (at rest 15: 3 cm closer)"
            private static string Near(float m, float rest)
            {
                if (m == float.MaxValue) return "—";
                string s = Cm(m);
                if (rest < 0f || rest == float.MaxValue) return s;
                int d = Mathf.RoundToInt((rest - m) * 100f);
                return s + " (at the first frame " + Cm(rest) + (d >= 2 ? ": " + d + " cm closer" : ": no closer") + ")";
            }

            internal string Report(bool play)
            {
                StringBuilder sb = new StringBuilder();
                float scale = _root.lossyScale.y;
                sb.Append("raven scale ").Append(scale.ToString("0.00", CultureInfo.InvariantCulture)).Append("\n");
                sb.Append("nearest wing end to its own head: ").Append(Near(_nearHead, _restHead)).Append(" — ").Append(_nearHeadAt).Append("\n");
                sb.Append("nearest wing bone to your head:   ").Append(Near(_nearYou, _restYou)).Append(" — ").Append(_nearYouAt).Append("\n");
                // the biggest turns of a bone between two frames: a jerk (play) or a big step between moments (pose)
                List<KeyValuePair<float, string>> jumps = new List<KeyValuePair<float, string>>();
                for (int f = 1; f < _rot.Count; f++)
                {
                    float dt = Mathf.Max(0.001f, _times[f] - _times[f - 1]);
                    for (int i = 0; i < _all.Count; i++)
                    {
                        float ang = Quaternion.Angle(_rot[f - 1][i], _rot[f][i]);
                        if (ang < 1f) continue;
                        float v = play ? ang / dt : ang;
                        jumps.Add(new KeyValuePair<float, string>(v, _all[i].name + " " + _times[f - 1].ToString("0.00", CultureInfo.InvariantCulture) + "→" + _times[f].ToString("0.00", CultureInfo.InvariantCulture)
                            + ": " + ang.ToString("0", CultureInfo.InvariantCulture) + "°" + (play ? " (" + v.ToString("0", CultureInfo.InvariantCulture) + "°/s)" : "")));
                    }
                }
                jumps.Sort((x, y) => y.Key.CompareTo(x.Key));
                sb.Append(play ? "fastest turns (a jerk stands out from the rest):\n" : "biggest changes between moments:\n");
                for (int i = 0; i < jumps.Count && i < 12; i++) sb.Append("  ").Append(jumps[i].Value).Append('\n');
                if (jumps.Count == 0) sb.Append("  none (the bones hardly moved)\n");
                return sb.ToString();
            }

            private static string Cm(float m) { return m == float.MaxValue ? "—" : Mathf.RoundToInt(m * 100f) + " cm"; }
        }

        // ---------------------------------------------------------------- pictures

        private static Texture2D Shrink(Texture2D t, int maxW)
        {
            if (t.width <= maxW) return t;
            int w = maxW, h = Mathf.RoundToInt(t.height * (maxW / (float)t.width));
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture was = RenderTexture.active;
            Graphics.Blit(t, rt);
            RenderTexture.active = rt;
            Texture2D s = new Texture2D(w, h, TextureFormat.RGB24, false);
            s.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            s.Apply(false);
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            return s;
        }

        private static void Blit(Texture2D from, Texture2D to, int x, int y)
        {
            int w = Mathf.Min(from.width, to.width - x), h = Mathf.Min(from.height, to.height - y);
            if (w <= 0 || h <= 0) return;
            to.SetPixels(x, y, w, h, from.GetPixels(0, 0, w, h));
        }

        private static void Fill(Texture2D t, Color32 c)
        {
            Color32[] px = new Color32[t.width * t.height];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels32(px);
        }

        private static void Save(string name, Texture2D t)
        {
            t.Apply(false);
            File.WriteAllBytes(Path.Combine(Out, Clean(name) + ".png"), ImageConversion.EncodeToPNG(t));
        }

        // the studio keeps only its latest pictures (they are looked at once and are big): the oldest go past this many
        internal static int KeepFiles = 120;
        private static void Prune()
        {
            try
            {
                List<FileInfo> files = new List<FileInfo>();
                foreach (FileInfo f in new DirectoryInfo(Out).GetFiles()) if (f.Name != "log.txt") files.Add(f);
                if (files.Count <= KeepFiles) return;
                files.Sort((x, y) => x.LastWriteTimeUtc.CompareTo(y.LastWriteTimeUtc));
                for (int i = 0; i < files.Count - KeepFiles; i++) files[i].Delete();
            }
            catch { }
        }

        private static void Note(string name, string text)
        {
            try { File.WriteAllText(Path.Combine(Out, Clean(name) + ".txt"), text + "\n", new UTF8Encoding(false)); } catch { }
        }

        // a line after a report already written (the scene check's «given back»: Note would put it in the report's place)
        private static void NoteMore(string name, string text)
        {
            try { File.AppendAllText(Path.Combine(Out, Clean(name) + ".txt"), text + "\n", new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogWarning("[RavenWatch] studio: " + name + " not written: " + e.Message); }
        }

        private static string Clean(string name)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in name) sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
            return sb.ToString();
        }
    }
}
#endif
