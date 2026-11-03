#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The animation stage (author's tool, the user 2026-11-02): a mannequin to make our clips on together, live. It stands
    // before you and plays a clip over and over; you keep your own keys and look from anywhere.
    //   The clip's file (config/RavenWatch/player_anims/<clip>.txt) is watched: saved, it is read and played at once, so
    //   one writes and the other watches, no restart. Every saved version is kept as a take (player_anims_takes/<clip>/).
    //   A file that does not read leaves the last good clip playing and says so on the screen.
    // The clips are the same ones the player and the people play (BodyClips): what is good here is good in a scene.
    // A part of a clip (<clip>.<part>) plays as a clip; its file and its takes are those of the whole clip.
    //   raven stage up | down | here | turn <deg>                  the mannequin: before you, away, here, turned
    //   raven stage play <clip> [speed] · stop · speed <x>         plays it, again and again
    //   raven stage hold <seconds> · go                            held at one moment · on again
    //   raven stage emote <gesture>|none                           the game's own gesture under the clip (kneel, sit …)
    //   raven stage prop <item> [x y z [rx ry rz]] · prop none       a thing lying before it for the clip's @Prop channels
    //                                                              (metres and degrees in its frame), as a scene's «free» prop
    //   raven stage edit <clip>                                    a built-in clip written out as a file, to edit
    //   raven stage rec · rec off                                  the player's own moves recorded, jerks marked (PAnimLog), for
    //                                                              a clip played on him in a scene
    //   raven stage takes · back [n]                               the takes of the clip · the file put back n takes
    //   raven stage check                                          one run of the clip measured every frame, into
    //                                                              studio/out/stage_<clip>.txt: feet sliding or under the
    //                                                              ground, knees, targets missed, the fastest bones, jerks;
    //                                                              on top a VERDICT: each line a mistake already made once,
    //                                                              measured (docs/ANIMATION.md). A mannequin still coming
    //                                                              into the world is measured when it has come. The clip is
    //                                                              taken off first and played from the game's own stance,
    //                                                              so its way in and out are in the measure.
    [DefaultExecutionOrder(20000)] // (after every pose: what it measures is what is shown)
    internal sealed class AnimStage : MonoBehaviour
    {
        private const string Template = "dummy", Tag = "stage";
        private const int Slot = BodyClips.PreviewSlot;
        internal static float Gap = 1.2f, Fade = 0.25f, Ahead = 3f, Look = 0.4f;

        private static AnimStage _me;
        private NpcActor _dummy;
        private string _clip = "", _note = "", _emote = "";
        private float _speed = 1f, _hold = -1f, _again, _nextLook, _noteUntil;
        private DateTime _stamp;
        private int _take;
        private GUIStyle _style, _shade;

        private static string TakesRoot { get { return Path.Combine(Path.GetDirectoryName(BodyClips.Dir), "player_anims_takes"); } }
        private static string Whole(string clip) { int dot = clip.IndexOf('.'); return dot > 0 ? clip.Substring(0, dot) : clip; }
        private static string FileOf(string clip) { return Path.Combine(BodyClips.Dir, Whole(clip) + ".txt"); }
        private static string TakesOf(string clip) { return Path.Combine(TakesRoot, Whole(clip)); }

        internal static string Cmd(Player p, string[] a)
        {
            if (!Blueprint.On) return "Сцена анімацій спить: немає теки " + Blueprint.Root;
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            string arg = a.Length > 2 ? a[2] : "";
            if (what == "rec") // (the local player's black box from the console: PAnimLog, jerks marked; a scene's clips are on the player, not on the mannequin)
            {
                if (arg == "off") return "Запис: " + (PAnimLog.End("stage rec") ?? "нема");
                if (PAnimLog.On) return "Уже пишеться запис «" + PAnimLog.What + "»: спершу raven stage rec off.";
                PAnimLog.Begin("stage rec");
                return "Пишу рухи гравця: raven stage rec off.";
            }
            if (_me == null)
            {
                if (what != "up" && what != "play") return "raven stage up | down | here | turn <град> | play <кліп> [швидкість] | stop | speed <x> | hold <с> | go | emote <жест>|none | prop <предмет>|none | edit <кліп> | takes | back [n]";
                GameObject g = new GameObject("RavenWatch_AnimStage");
                DontDestroyOnLoad(g);
                _me = g.AddComponent<AnimStage>();
            }
            return _me.Do(p, what, arg, a);
        }

        private string Do(Player p, string what, string arg, string[] a)
        {
            switch (what)
            {
                case "up": return Bring(p) ? "Манекен перед тобою." : "Манекен не вдалося зробити (нема шаблону «" + Template + "»?).";
                case "down": Destroy(gameObject); return "Манекена прибрано.";
                case "here":
                    if (!Bring(p)) return "Манекена нема.";
                    _dummy.Order("tp " + V(Before(p)), NpcMind.Layer.Scene);
                    Face(p.transform.position);
                    return "Манекен тут.";
                case "turn":
                    {
                        if (!Bring(p)) return "Манекена нема.";
                        float deg = Num(arg, 90f);
                        Face(_dummy.transform.position + Quaternion.Euler(0f, deg, 0f) * _dummy.transform.forward * 3f);
                        return "Повернуто на " + deg.ToString("0", CultureInfo.InvariantCulture) + "°.";
                    }
                case "play":
                    if (arg.Length == 0) return "raven stage play <кліп> [швидкість]";
                    if (!Bring(p)) return "Манекена нема.";
                    BodyClips.Load(); // (the files as they are now)
                    if (BodyClips.Find(arg) == null) return "Нема кліпу «" + arg + "».";
                    _clip = arg; _hold = -1f; _take = Takes(arg).Count;
                    _speed = a.Length > 3 ? Mathf.Clamp(Num(a[3], 1f), 0.05f, 4f) : _speed;
                    _stamp = StampOf(arg);
                    KeepTake(); // (the version we begin from)
                    Begin(Fade);
                    return "Грає «" + arg + "», ×" + F(_speed) + ".";
                case "stop": Stop(); return "Зупинено.";
                case "speed":
                    _speed = Mathf.Clamp(Num(arg, 1f), 0.05f, 4f);
                    if (_clip.Length > 0) Begin(0.05f);
                    return "Швидкість ×" + F(_speed) + ".";
                case "hold":
                    {
                        BodyAnimator h = Animator();
                        if (h == null || _clip.Length == 0) return "Нічого не грає.";
                        _hold = Mathf.Max(0f, Num(arg, 0f));
                        h.Hold(Slot, _hold);
                        return "Стоїть на " + F(_hold) + " с.";
                    }
                case "go":
                    {
                        BodyAnimator h = Animator();
                        _hold = -1f;
                        if (h != null) h.Hold(Slot, -1f);
                        return "Далі.";
                    }
                case "emote":
                    if (!Bring(p)) return "Манекена нема.";
                    _emote = arg == "none" ? "" : arg;
                    _dummy.Order(_emote.Length > 0 ? "emote " + _emote + " loop" : "emote none", NpcMind.Layer.Scene);
                    return _emote.Length > 0 ? "Жест гри під кліпом: " + _emote + "." : "Без жесту гри.";
                case "prop":
                    {
                        if (!Bring(p)) return "Манекена нема.";
                        if (arg.Length == 0) return "raven stage prop <предмет> [x y z [rx ry rz]] · prop none";
                        StoryProps.Put(_dummy);
                        if (arg == "none") return "Реквізит прибрано.";
                        Vector3 at = new Vector3(a.Length > 5 ? Num(a[3], 0f) : 0f, a.Length > 5 ? Num(a[4], 0f) : PropLow, a.Length > 5 ? Num(a[5], 0f) : PropAhead);
                        Vector3 turn = new Vector3(a.Length > 8 ? Num(a[6], 0f) : 0f, a.Length > 8 ? Num(a[7], 0f) : 0f, a.Length > 8 ? Num(a[8], 0f) : 0f);
                        Transform root = _dummy.transform;
                        string why = StoryProps.Take(_dummy, new StoryProps.Ask { Item = arg, Free = true, Lasting = true, HasRest = true, RestAt = root.TransformPoint(at), RestTurn = root.rotation * Quaternion.Euler(turn) });
                        if (why != null) return why;
                        _sizeOf = true;
                        return "«" + arg + "» лежить перед манекеном. Розмір буде у звіті check.";
                    }
                case "edit":
                    {
                        BodyClips.Load();
                        PAnimClip c = BodyClips.Find(arg);
                        if (c == null) return "Нема кліпу «" + arg + "».";
                        if (File.Exists(FileOf(arg))) return "Файл уже є: " + FileOf(arg);
                        return "Кліп записано: " + BodyClips.Save(c);
                    }
                case "check":
                    if (_clip.Length == 0) return "Нічого не грає.";
                    if (_hold >= 0f) return "Спершу go: зараз пауза.";
                    {
                        float early = _born + Born - Time.time;
                        if (early > 0f) { _checkAt = Time.time + early; return "Манекен ще з'являється: заміряю за " + F(early) + " с."; }
                        StartCheck();
                        return "Заміряю один прохід.";
                    }
                case "takes":
                    {
                        if (_clip.Length == 0) return "Нічого не грає.";
                        List<string> t = Takes(_clip);
                        return "Дублів «" + _clip + "»: " + t.Count + " (останній — поточний).";
                    }
                case "back":
                    {
                        if (_clip.Length == 0) return "Нічого не грає.";
                        List<string> t = Takes(_clip);
                        int n = Mathf.Max(1, (int)Num(arg, 1f));
                        if (t.Count <= n) return "Стільки дублів нема: " + t.Count + ".";
                        File.WriteAllText(FileOf(_clip), File.ReadAllText(t[t.Count - 1 - n], Encoding.UTF8), new UTF8Encoding(false)); // (the watcher plays it, as a new take)
                        return "Повернуто дубль на " + n + " назад.";
                    }
            }
            return "raven stage up | down | here | turn <град> | play <кліп> [швидкість] | stop | speed <x> | hold <с> | go | emote <жест>|none | prop <предмет>|none | edit <кліп> | takes | back [n]";
        }

        // ---------------------------------------------------------------- the mannequin
        private bool Bring(Player p)
        {
            if (_dummy != null) return true;
            Vector3 at = Before(p);
            Vector3 look = p.transform.position - at; look.y = 0f;
            _dummy = Npcs.Spawn(Template, at, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity, p.GetPlayerID(), Tag);
            if (_dummy == null) return false;
            _born = Time.time;
            ZDO z = _dummy.Zdo;
            if (z != null) z.Persistent = false; // (left behind by a crash: gone with the session)
            _dummy.Order("stay", NpcMind.Layer.Scene); // (over everything it might think of)
            return true;
        }

        private static Vector3 Before(Player p)
        {
            Vector3 f = p.transform.forward; f.y = 0f;
            return p.transform.position + (f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward) * Ahead;
        }

        private void Face(Vector3 at) { if (_dummy != null) _dummy.Order("lookat " + V(at + Vector3.up * 1.4f), NpcMind.Layer.Scene); }

        private BodyAnimator Animator() { Humanoid h = _dummy != null ? _dummy.GetComponent<Humanoid>() : null; return h != null ? BodyAnimator.For(h) : null; }

        private void Begin(float fade)
        {
            Humanoid h = _dummy != null ? _dummy.GetComponent<Humanoid>() : null;
            PAnimClip c = BodyClips.Find(_clip);
            if (h == null || c == null) return;
            BodyClips.Play(h, Slot, _clip, fade, _speed);
            BodyAnimator a = BodyAnimator.For(h);
            if (a != null) a.Hold(Slot, _hold);
            _again = c.Loop ? 0f : Time.time + c.Length / Mathf.Max(0.05f, _speed) + Gap; // (one that plays once: again after a breath)
        }

        internal static float PropLow = 0.11f, PropAhead = 0.78f; // (as a shield lies on the ground, as far as the scene's step brings the body)
        private bool _sizeOf;

        private void Stop()
        {
            Humanoid h = _dummy != null ? _dummy.GetComponent<Humanoid>() : null;
            if (h != null) BodyClips.Stop(h, Slot, Fade);
            _clip = ""; _hold = -1f; _again = 0f;
        }

        // ---------------------------------------------------------------- the file watched, the takes kept
        private static DateTime StampOf(string clip) { string f = FileOf(clip); return File.Exists(f) ? File.GetLastWriteTimeUtc(f) : DateTime.MinValue; }

        private static List<string> Takes(string clip)
        {
            string d = TakesOf(clip);
            List<string> l = new List<string>();
            if (Directory.Exists(d)) l.AddRange(Directory.GetFiles(d, "*.txt"));
            l.Sort(StringComparer.Ordinal); // (their names are their numbers, padded)
            return l;
        }

        // the file as it is now, a new take when it differs from the last one
        private void KeepTake()
        {
            string f = FileOf(_clip);
            if (!File.Exists(f)) return;
            string text = File.ReadAllText(f, Encoding.UTF8);
            List<string> t = Takes(_clip);
            if (t.Count > 0 && File.ReadAllText(t[t.Count - 1], Encoding.UTF8) == text) { _take = t.Count; return; }
            int n = 1;
            if (t.Count > 0) int.TryParse(Path.GetFileNameWithoutExtension(t[t.Count - 1]), out n);
            if (t.Count > 0) n++;
            Directory.CreateDirectory(TakesOf(_clip));
            File.WriteAllText(Path.Combine(TakesOf(_clip), n.ToString("000") + ".txt"), text, new UTF8Encoding(false));
            _take = t.Count + 1;
        }

        private void Watch()
        {
            DateTime now = StampOf(_clip);
            if (now == _stamp) return;
            _stamp = now;
            string why = null;
            try
            {
                if (now != DateTime.MinValue && BodyClips.Parse(File.ReadAllText(FileOf(_clip), Encoding.UTF8), _clip) == null) why = "у файлі помилка — грає попередня версія";
            }
            catch (IOException) { _stamp = DateTime.MinValue.AddTicks(1); return; } // (still being written: looked at again)
            if (why != null) { Say(why); return; }
            if (_check != null || _checkPlay > 0f || _checkAt > 0f) { _check = null; _checkPlay = _checkAt = 0f; Say("новий дубль посеред заміру: заміряй знову"); }
            KeepTake();
            BodyClips.Load();
            if (BodyClips.Find(_clip) == null) { Say("кліпу вже нема"); return; }
            Begin(0.05f);
            Say("новий дубль " + _take);
        }

        private void Say(string s) { _note = s; _noteUntil = Time.time + 4f; }

        // ---------------------------------------------------------------- each frame
        private void Update()
        {
            if (Player.m_localPlayer == null || ZNet.instance == null) { Destroy(gameObject); return; } // (the world left: the mannequin went with it)
            if (_clip.Length == 0) return;
            if (_checkAt > 0f && Time.time >= _checkAt) { _checkAt = 0f; StartCheck(); }
            if (_checkPlay > 0f && Time.time >= _checkPlay) { _checkPlay = 0f; CheckRun(); }
            if (Time.time >= _nextLook) { _nextLook = Time.time + Look; Watch(); }
            if (_again > 0f && _hold < 0f && Time.time >= _again) Begin(0.2f);
        }

        private void OnDestroy()
        {
            if (_me == this) _me = null;
            if (_dummy != null) _dummy.Remove();
        }

        // ---------------------------------------------------------------- the measure
        private static readonly string[] Measured = { "Hips", "Spine1", "Spine2", "Neck", "Head", "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };
        private Check _check;
        internal static float Born = 5f; // (its smoke and its first pose: not the clip's)
        private float _born = -99f, _checkAt, _checkPlay;
        internal static float Rest = 1f, FootFast = 1.5f, HandFast = 3f, StanceNear = 0.08f, FingerDeep = 0.02f;

        private void StartCheck()
        {
            Humanoid h = _dummy != null ? _dummy.GetComponent<Humanoid>() : null;
            if (h == null || BodyClips.Find(_clip) == null) return;
            BodyClips.Stop(h, Slot, 0.3f);
            _again = 0f;
            _checkPlay = Time.time + Rest + BodyClips.Find(_clip).FadeOut;
        }

        // at rest in the game's stance now: the run that is measured
        private void CheckRun()
        {
            PAnimClip c = BodyClips.Find(_clip);
            if (c == null) return;
            _check = new Check(_clip, c.Length / Mathf.Max(0.05f, _speed) + (c.Loop ? 0f : c.FadeOut + 0.2f));
            Begin(Fade);
            if (!c.Loop) _again = _check.Until + Gap;
        }

        private sealed class Check
        {
            public string Clip; public float Until, Last;
            public readonly Dictionary<string, Quaternion> Was = new Dictionary<string, Quaternion>();
            public readonly Dictionary<string, float> Speed = new Dictionary<string, float>(), Most = new Dictionary<string, float>(), Jerk = new Dictionary<string, float>(), MostAt = new Dictionary<string, float>(), JerkAt = new Dictionary<string, float>();
            // the verdict's measures
            public Vector3 RootAt; public Quaternion RootTurn; public bool Rooted;
            public readonly Dictionary<string, float> MissAt = new Dictionary<string, float>(), FastAt = new Dictionary<string, float>(), Fast = new Dictionary<string, float>();
            public readonly Dictionary<string, Vector3> Limb = new Dictionary<string, Vector3>();
            public Vector3 GameL, GameR; public bool GameSeen; // (its feet in the game's own stance, before the clip has weight)
            public float Moved, Turned, Near = 99f, HeadToProp = 99f, HipsStood, KneeCrossed, HandBehind, Through, PalmAwayL, PalmAwayR;
            public float LLow = 9f, RLow = 9f, LKnee = 9f, RKnee = 9f, HipsLow = 9f, HipsHigh = -9f, Frames;
            public readonly Dictionary<string, float> Miss = new Dictionary<string, float>();
            public readonly StringBuilder Rows = new StringBuilder();
            public Check(string clip, float seconds) { Clip = clip; Until = Time.time + seconds; }
        }

        private void LateUpdate()
        {
            if (_check == null || _dummy == null) return;
            Humanoid h = _dummy.GetComponent<Humanoid>();
            BodyAnimator an = Animator();
            Animator anim = h != null ? h.GetComponentInChildren<Animator>() : null;
            if (h == null || an == null || anim == null) { _check = null; return; }
            Check c = _check;
            Transform root = _dummy.transform, vis = anim.transform;
            float dt = Mathf.Max(Time.deltaTime, 0.001f), at = an.TimeIn(Slot);
            c.Frames++;
            Watch(c, h, vis, root, an);
            foreach (string name in Measured)
            {
                Transform b = BodyClips.SkeletonBone(h, vis, name);
                if (b == null) continue;
                Quaternion q = Quaternion.Inverse(root.rotation) * b.rotation, was;
                if (c.Was.TryGetValue(name, out was))
                {
                    float sp = Quaternion.Angle(was, q) / dt, last, keep;
                    if (!c.Most.TryGetValue(name, out keep) || sp > keep) { c.Most[name] = sp; c.MostAt[name] = at; }
                    if (c.Speed.TryGetValue(name, out last))
                    {
                        float j = Mathf.Abs(sp - last) / dt; // (how fast its speed changes: a jerk is a spike of this)
                        if (!c.Jerk.TryGetValue(name, out keep) || j > keep) { c.Jerk[name] = j; c.JerkAt[name] = at; }
                    }
                    c.Speed[name] = sp;
                }
                c.Was[name] = q;
            }
            Vector3 lf = Local(h, vis, root, "LeftFoot"), rf = Local(h, vis, root, "RightFoot"), lk = Local(h, vis, root, "LeftLeg"), rk = Local(h, vis, root, "RightLeg"), hips = Local(h, vis, root, "Hips");
            c.LLow = Mathf.Min(c.LLow, lf.y); c.RLow = Mathf.Min(c.RLow, rf.y); c.LKnee = Mathf.Min(c.LKnee, lk.y); c.RKnee = Mathf.Min(c.RKnee, rk.y);
            c.HipsLow = Mathf.Min(c.HipsLow, hips.y); c.HipsHigh = Mathf.Max(c.HipsHigh, hips.y);
            foreach (BodyClips.Limb m in BodyClips.Limbs) { float e, keep; if (an.Missed(m.Name, out e) && (!c.Miss.TryGetValue(m.Name, out keep) || e > keep)) { c.Miss[m.Name] = e; c.MissAt[m.Name] = at; } }
            if (Time.time - c.Last >= 0.25f)
            {
                c.Last = Time.time;
                c.Rows.Append(F(at)).Append("  hips ").Append(F(hips.y)).Append("  Lfoot ").Append(P(lf)).Append("  Rfoot ").Append(P(rf)).Append("  Lknee ").Append(P(lk)).Append("  Rknee ").Append(P(rk))
                      .Append("  Lhand ").Append(P(Local(h, vis, root, "LeftHand"))).Append("  Rhand ").Append(P(Local(h, vis, root, "RightHand"))).Append('\n');
            }
            if (Time.time < c.Until) return;
            _check = null;
            Report(c);
        }

        private static readonly string[] Tips = { "LeftHandIndex3", "LeftHandMiddle3", "LeftHandRing3", "RightHandIndex3", "RightHandMiddle3", "RightHandRing3" };

        // each frame of a measure: the things that went wrong before, as numbers
        private void Watch(Check c, Humanoid h, Transform vis, Transform root, BodyAnimator an)
        {
            Vector3 hips = Local(h, vis, root, "Hips"), lk = Local(h, vis, root, "LeftLeg"), rk = Local(h, vis, root, "RightLeg");
            if (!c.Rooted) { c.Rooted = true; c.RootAt = root.position; c.RootTurn = root.rotation; c.HipsStood = hips.y; }
            if (!c.GameSeen) { c.GameSeen = true; c.GameL = Local(h, vis, root, "LeftFoot"); c.GameR = Local(h, vis, root, "RightFoot"); }
            c.Moved = Mathf.Max(c.Moved, Vector3.Distance(root.position, c.RootAt));
            c.Turned = Mathf.Max(c.Turned, Quaternion.Angle(root.rotation, c.RootTurn));
            if (Player.m_localPlayer != null) c.Near = Mathf.Min(c.Near, Vector3.Distance(Player.m_localPlayer.transform.position, root.position));
            c.KneeCrossed = Mathf.Max(c.KneeCrossed, Mathf.Max(lk.x, -rk.x)); // (each knee on its own side of the body)
            foreach (string limb in new[] { "LeftFoot", "RightFoot", "LeftHand", "RightHand" }) // (how fast each goes through the body's frame: a way in or out too short shows here)
            {
                Vector3 now = Local(h, vis, root, limb), was;
                if (c.Limb.TryGetValue(limb, out was))
                {
                    float sp = (now - was).magnitude / Mathf.Max(Time.deltaTime, 0.001f), most;
                    if (!c.Fast.TryGetValue(limb, out most) || sp > most) { c.Fast[limb] = sp; c.FastAt[limb] = an.TimeIn(Slot); }
                }
                c.Limb[limb] = now;
            }
            float ignore;
            if (hips.y < c.HipsStood - 0.2f) // (bent down, a hand no one holds swings back with the body)
            {
                if (!an.Missed("@LeftHand", out ignore)) c.HandBehind = Mathf.Max(c.HandBehind, -Local(h, vis, root, "LeftHand").z);
                if (!an.Missed("@RightHand", out ignore)) c.HandBehind = Mathf.Max(c.HandBehind, -Local(h, vis, root, "RightHand").z);
            }
            StoryPropView view = an.PropOn ? _dummy.GetComponent<StoryPropView>() : null;
            if (view == null) return;
            Transform head = BodyClips.SkeletonBone(h, vis, "Head");
            if (head != null) c.HeadToProp = Mathf.Min(c.HeadToProp, view.Distance(head.position));
            foreach (string tip in Tips)
            {
                Transform t = BodyClips.SkeletonBone(h, vis, tip);
                if (t != null && view.Distance(t.position) < -FingerDeep) { c.Through++; break; }
            }
            PalmAt(c, h, vis, an, view, true);
            PalmAt(c, h, vis, an, view, false);
        }

        private void PalmAt(Check c, Humanoid h, Transform vis, BodyAnimator an, StoryPropView view, bool left)
        {
            float ignore;
            Transform hand = BodyClips.SkeletonBone(h, vis, left ? "LeftHand" : "RightHand");
            if (hand == null || !an.Missed(left ? "@LeftHand" : "@RightHand", out ignore) || view.Distance(hand.position) > 0.15f) return; // (only a hand on the thing)
            Vector3 along, palm, at; Quaternion turn;
            if (!an.HandFacing(left, hand, out along, out palm)) return;
            an.PropAt(out at, out turn);
            if (Vector3.Dot(palm.normalized, (at - hand.position).normalized) >= -0.2f) return;
            if (left) c.PalmAwayL++; else c.PalmAwayR++;
        }

        // the mistakes already made once, each a line when it is made again; none: OK
        private string Verdict(Check c)
        {
            List<string> bad = new List<string>();
            if (c.Moved > 0.05f || c.Turned > 5f) bad.Add("DO NOT TRUST THIS TAKE: the mannequin moved " + F(c.Moved) + " m, turned " + F(c.Turned) + " deg");
            if (c.Near < 2f) bad.Add("the player came within " + F(c.Near) + " m (stand 4-5 m off: he pushes it and blocks the view)");
            foreach (KeyValuePair<string, float> m in c.Miss) if (m.Value > 0.05f) bad.Add(m.Key + " misses by " + F(m.Value) + " m at " + F(c.MissAt[m.Key]) + " s with its weight on: out of reach");
            foreach (KeyValuePair<string, float> m in c.Fast) if (m.Value > (m.Key.EndsWith("Foot", StringComparison.Ordinal) ? FootFast : HandFast)) bad.Add(m.Key + " flies at " + F(m.Value) + " m/s at " + F(c.FastAt[m.Key]) + " s: too sudden (near the start or end: the way in or out)");
            foreach (string name in Measured) { float most; if (c.Most.TryGetValue(name, out most) && most > 600f) bad.Add(name + " at " + most.ToString("0", CultureInfo.InvariantCulture) + " deg/s at " + F(c.MostAt[name]) + " s: a jerk"); }
            PAnimClip played = BodyClips.Find(c.Clip);
            if (played != null && played.Bones.Contains("@LeftFoot") && played.Bones.Contains("@RightFoot") && !played.IsPart) // (a clip that starts from another stance: its feet slide over the ground on its way in)
            {
                float off = Mathf.Max(Vector3.Distance(played.Value("@LeftFoot", 0f), c.GameL), Vector3.Distance(played.Value("@RightFoot", 0f), c.GameR));
                if (off > StanceNear) bad.Add("the first pose's feet are " + F(off) + " m from the game's stance (L " + P(c.GameL) + ", R " + P(c.GameR) + "): they slide on the way in");
            }
            if (c.KneeCrossed > 0.05f) bad.Add("a knee crossed the body's middle by " + F(c.KneeCrossed) + " m: set @LeftFoot.p / @RightFoot.p");
            if (c.HandBehind > 0.28f) bad.Add("a free hand swings " + F(c.HandBehind) + " m behind the body while bent: give it a target (the knee)");
            if (c.HeadToProp < 0.3f) bad.Add("the head comes within " + F(c.HeadToProp) + " m of the prop: hold it farther");
            if (c.Through > 0f) bad.Add("fingers inside the prop in " + c.Through.ToString("0") + " frames: move the wrist out past its edge");
            if (c.PalmAwayL > 0f) bad.Add("the left palm faces away from the prop in " + c.PalmAwayL.ToString("0") + " frames");
            if (c.PalmAwayR > 0f) bad.Add("the right palm faces away from the prop in " + c.PalmAwayR.ToString("0") + " frames");
            PAnimClip whole = BodyClips.Find(Whole(c.Clip));
            if (whole != null)
            {
                if (whole.Bones.Count >= BodyClips.MaxBones) bad.Add("the clip is at " + BodyClips.MaxBones + " channels: what was over is left out (the log says which)");
                for (int i = 0; i < whole.Parts.Count; i++)
                {
                    PAnimPart pt = whole.Parts[i];
                    PAnimClip one = BodyClips.Find(whole.Name + "." + pt.Name);
                    PAnimClip next = pt.How == "loop" ? one : i + 1 < whole.Parts.Count ? BodyClips.Find(whole.Name + "." + whole.Parts[i + 1].Name) : null;
                    string ch;
                    if (one != null && next != null && BodyClips.JoinGap(one, next, out ch) > BodyClips.JoinNear) bad.Add("part " + pt.Name + ": its join shows (" + ch + ")");
                }
                string baked = BakedDiffers(whole.Name);
                if (baked != null) bad.Add(baked);
            }
            return bad.Count == 0 ? "VERDICT: OK\n" : "VERDICT:\n  - " + string.Join("\n  - ", bad.ToArray()) + "\n";
        }

        // what plays here and what the mod ships: a clip approved only in a config plays nowhere else
        private static string BakedDiffers(string clip)
        {
            if (!File.Exists(FileOf(clip))) return null;
            using (Stream st = typeof(BodyClips).Assembly.GetManifestResourceStream("RavenWatch.clips." + clip + ".txt"))
            {
                if (st == null) return "not in assets/story/clips yet: when approved, copy it there";
                using (StreamReader rd = new StreamReader(st, Encoding.UTF8))
                    return rd.ReadToEnd().Replace("\r", "") == File.ReadAllText(FileOf(clip), Encoding.UTF8).Replace("\r", "") ? null : "this file differs from assets/story/clips: when approved, copy it there";
            }
        }

        private static string P(Vector3 v) { return v.x.ToString("0.00", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.00", CultureInfo.InvariantCulture) + "," + v.z.ToString("0.00", CultureInfo.InvariantCulture); }
        private static Vector3 Local(Humanoid h, Transform vis, Transform root, string bone) { Transform t = BodyClips.SkeletonBone(h, vis, bone); return t != null ? root.InverseTransformPoint(t.position) : Vector3.zero; }

        private void Report(Check c)
        {
            StringBuilder s = new StringBuilder();
            s.Append(c.Clip).Append(", take ").Append(_take).Append(", ×").Append(F(_speed)).Append(", ").Append(c.Frames.ToString("0")).Append(" frames\n");
            s.Append(Verdict(c));
            s.Append("feet lowest: L ").Append(F(c.LLow)).Append(", R ").Append(F(c.RLow)).Append(" m\n");
            StoryPropView view = _dummy.GetComponent<StoryPropView>();
            if (view != null && _sizeOf) s.Append("prop size ").Append(P(view.Size)).Append(" m\n");
            VisEquipment skin = _dummy.GetComponent<VisEquipment>();
            if (skin != null && skin.m_bodyModel != null && skin.m_bodyModel.bones != null) // (what a clip can move on the hands)
            {
                s.Append("hand bones:");
                foreach (Transform b in skin.m_bodyModel.bones) if (b != null && b.name.Contains("Hand")) s.Append(' ').Append(b.name);
                s.Append("\n");
            }
            s.Append("knees lowest: L ").Append(F(c.LKnee)).Append(", R ").Append(F(c.RKnee)).Append(" m · hips ").Append(F(c.HipsLow)).Append(" .. ").Append(F(c.HipsHigh)).Append(" m\n");
            foreach (KeyValuePair<string, float> m in c.Miss) s.Append("target ").Append(m.Key).Append(" missed by at most ").Append(F(m.Value)).Append(" m\n");
            s.Append("fastest (deg/s at second) · largest jerk (at second):\n");
            foreach (string name in Measured)
            {
                float most, jerk;
                if (!c.Most.TryGetValue(name, out most)) continue;
                c.Jerk.TryGetValue(name, out jerk);
                s.Append("  ").Append(name.PadRight(13)).Append(most.ToString("0", CultureInfo.InvariantCulture).PadLeft(6)).Append(" @").Append(F(c.MostAt[name])).Append("   ").Append(jerk.ToString("0", CultureInfo.InvariantCulture).PadLeft(8)).Append(c.JerkAt.ContainsKey(name) ? " @" + F(c.JerkAt[name]) : "").Append('\n');
            }
            s.Append("\nevery 0.25 s (m, body frame: x right, y up, z ahead):\n").Append(c.Rows);
            string dir = Path.Combine(Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "studio"), "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "stage_" + c.Clip + ".txt"), s.ToString(), new UTF8Encoding(false));
            Say("заміряно");
        }

        // what plays and where it is: said by the time when asking for a change
        private void OnGUI()
        {
            if (_clip.Length == 0 || Hud.IsUserHidden()) return;
            BodyAnimator a = Animator();
            PAnimClip c = BodyClips.Find(_clip);
            if (a == null || c == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.026f), fontStyle = FontStyle.Bold };
                _shade = new GUIStyle(_style);
                _style.normal.textColor = new Color(1f, 0.9f, 0.55f);
                _shade.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }
            // in columns of a fixed place: the numbers change, nothing moves (the clip's name before the time, the rest after it)
            float u = _style.fontSize, y = Screen.height * 0.1f, mid = Screen.width * 0.5f;
            Cell(0f, y, mid - u * 3.4f, _clip, TextAnchor.UpperRight);
            Cell(mid - u * 3.2f, y, u * 3f, a.TimeIn(Slot).ToString("0.00", CultureInfo.InvariantCulture), TextAnchor.UpperRight);
            Cell(mid, y, u * 5f, "/ " + c.Length.ToString("0.00", CultureInfo.InvariantCulture) + " с", TextAnchor.UpperLeft);
            Cell(mid + u * 5.2f, y, u * 3f, "×" + _speed.ToString("0.00", CultureInfo.InvariantCulture), TextAnchor.UpperLeft);
            Cell(mid + u * 8.4f, y, u * 5f, "дубль " + _take, TextAnchor.UpperLeft);
            if (_hold >= 0f) Cell(mid + u * 13.6f, y, u * 5f, "ПАУЗА", TextAnchor.UpperLeft);
            if (Time.time < _noteUntil) Cell(0f, y + u * 1.4f, Screen.width, _note, TextAnchor.UpperCenter);
        }

        private void Cell(float x, float y, float w, string text, TextAnchor how)
        {
            _style.alignment = how; _shade.alignment = how;
            float h = _style.fontSize * 1.6f;
            GUI.Label(new Rect(x + 2f, y + 2f, w, h), text, _shade);
            GUI.Label(new Rect(x, y, w, h), text, _style);
        }

        private static string V(Vector3 v) { return F(v.x) + " " + F(v.y) + " " + F(v.z); }
        private static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static float Num(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }
    }
}
#endif
