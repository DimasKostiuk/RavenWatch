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
    //   The clip's file (BodyClips.Dir: on the author's machine assets/story/clips/<clip>.txt, the one copy) is watched: saved, it is read
    //   and played at once, so one writes and the other watches, no restart. Every saved version is kept as a take (config/RavenWatch/player_anims_takes/<clip>/).
    //   A file that does not read leaves the last good clip playing and says so on the screen.
    // The clips are the same ones the player and the people play (BodyClips): what is good here is good in a scene.
    // A part of a clip (<clip>.<part>) plays as a clip; its file and its takes are those of the whole clip.
    //   raven stage up [template] | down | here | turn <deg>       the mannequin: before you (its body: a template — heid: a clip
    //                                                              made for her on her body), away, here, turned
    //   raven stage play <clip> [speed] · stop · speed <x>         plays it, again and again (a whole clip: all its parts in
    //                                                              turn; on the screen the part playing now)
    //   raven stage ab <clip> | ab off                             a second mannequin beside it plays another take (two
    //                                                              versions in one look)
    //   raven stage wall | table | seat | step | point | piece … · clear   what its body touches, as in its scene: AnimStageFixtures
    //   raven stage walk [m] | walk off                            it walks to and fro (a real walk: the core's slowing, its gait)
    //   raven stage gait <kind> | all [s]                          its kind of gait (BodyGait), to compare; all: each in turn, its name shown
    //   raven stage hold <seconds> · go                            held at one moment · on again
    //   raven stage emote <gesture>|none                           the game's own gesture under the clip (kneel, sit …)
    //   raven stage prop <item> [x y z [rx ry rz]] · prop none       a thing lying before it for the clip's @Prop channels
    //                                                              (metres and degrees in its frame), as a scene's «free» prop
    //   raven stage edit <clip>                                    a built-in clip written out as a file, to edit
    //   raven stage rec · rec off                                  the player's own moves recorded, jerks marked (PAnimLog), for
    //                                                              a clip played on him in a scene
    //   raven stage takes · back [n]                               the takes of the clip · the file put back n takes
    //   raven stage check [views]                                  one run of the clip measured every frame by BodyCheck (the same
    //                                                              rules as a scene's check), into studio/out/stage_<clip>.txt: on
    //                                                              top a VERDICT, each line a mistake already made once, with what
    //                                                              to change; then what was asked (the clip's «want» lines), the
    //                                                              tempo strips, each key frame's pose in words. Filmed in the same
    //                                                              run at its key moments (StageFilm: 480 px, the whole body from every
    //                                                              side and the close views it needs — AutoViews — or the ones asked), stage_<clip>_<view>.png; then
    //                                                              each mistake's moment held and filmed close, stage_<clip>_worst_<view>.png.
    //                                                              A clip's «touch» is set up as a fixture by «play». A mannequin still coming
    //                                                              into the world is measured when it has come. The clip is
    //                                                              taken off first and played from the game's own stance,
    //                                                              so its way in and out are in the measure.
    [DefaultExecutionOrder(20000)] // (after every pose: what it measures is what is shown)
    internal sealed partial class AnimStage : MonoBehaviour
    {
        private const string Template = "dummy", Tag = "stage"; // (the author's template, not in the mod: assets/story/npc/_dummy.txt, read only on his machine: StoryFiles)
        private const int Slot = BodyClips.PreviewSlot;
        internal static float Gap = 1.2f, Fade = 0.25f, Ahead = 3f, Look = 0.4f;

        private static AnimStage _me;
        internal const string TagName = Tag; // (the story's people never count a mannequin as one of them: StoryPeople)
        private string _template = Template;
        private NpcActor _dummy, _other; // (_other: a second one beside it, playing another take to compare — «ab»)
        private string _clipB = "", _checkViews = "";
        private float _againB;
        internal static float AbAside = 1.4f;
        private string _clip = "", _note = "", _emote = "";
        private float _speed = 1f, _hold = -1f, _again, _nextLook, _noteUntil;
        private DateTime _stamp;
        private int _take;
        private GUIStyle _style, _shade;

        private static string TakesRoot { get { return Path.Combine(BodyClips.ConfigRoot, "player_anims_takes"); } }
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
                if (what != "up" && what != "play") return "raven stage up [шаблон] | down | here | turn <град> | play <кліп> [швидкість] | ab <кліп>|off | wall|table|seat|step|point|piece … · clear | stop | speed <x> | hold <с> | go | emote <жест>|none | prop <предмет>|none | edit <кліп> | check [види] | takes | back [n]";
                GameObject g = new GameObject("RavenWatch_AnimStage");
                DontDestroyOnLoad(g);
                _me = g.AddComponent<AnimStage>();
            }
            return _me.Do(p, what, arg, a);
        }

        private string Do(Player p, string what, string arg, string[] a)
        {
            if (_proving && what != "down") return "Ще знімаю кадри помилок останнього заміру."; // (it holds the clip at those moments, and says when it is done)
            switch (what)
            {
                case "up":
                    if (arg.Length > 0 && arg != _template) // (another body: the template a scene plays it on — heid, a woman, her build)
                    {
                        if (NpcDefs.Find(arg) == null) return "Нема шаблону «" + arg + "» (raven npc list).";
                        if (_dummy != null) { _dummy.Remove(); _dummy = null; }
                        _template = arg;
                    }
                    return Bring(p) ? "Манекен перед тобою (" + _template + ")." : "Манекен не вдалося зробити (нема шаблону «" + _template + "»?).";
                case "down": Destroy(gameObject); return "Манекена прибрано.";
                case "wall":
                case "table":
                case "seat":
                case "step":
                case "point":
                case "piece":
                case "clear":
                    if (!Bring(p)) return "Манекена нема.";
                    return FixtureCmd(what, a); // (AnimStageFixtures)
                case "ab":
                    if (!Bring(p)) return "Манекена нема.";
                    if (arg == "off" || arg.Length == 0) { if (_other != null) _other.Remove(); _other = null; _clipB = ""; return "Другого манекена прибрано."; }
                    BodyClips.Load();
                    if (BodyClips.Find(arg) == null) return "Нема кліпу «" + arg + "».";
                    if (_other == null)
                    {
                        Transform r0 = _dummy.transform;
                        _other = Npcs.Spawn(_template, r0.position + r0.right * AbAside, r0.rotation, p.GetPlayerID(), Tag);
                        if (_other == null) return "Другого манекена не вдалося зробити.";
                        if (_other.Zdo != null) _other.Zdo.Persistent = false;
                        _other.Order("stay", NpcMind.Layer.Scene);
                    }
                    _clipB = arg; _againB = 0f;
                    return "Праворуч другий манекен грає «" + arg + "»: порівняти два варіанти за один прогін (raven stage ab off — прибрати).";
                case "gait":
                    if (!Bring(p)) return "Манекена нема.";
                    if (arg == "all") { _gaitEvery = Mathf.Clamp(Num(a.Length > 3 ? a[3] : "", 12f), 4f, 60f); _gaitAt = 0; _gaitNext = 0f; return "Ходи по черзі, кожна " + F(_gaitEvery) + " с (raven stage gait <хода> — спинити на одній)."; }
                    _gaitEvery = 0f;
                    return NpcGait.Force(_dummy, arg);
                case "walk":
                    if (!Bring(p)) return "Манекена нема.";
                    if (arg == "off") { _walkFar = 0f; _dummy.Order("stay", NpcMind.Layer.Scene); return "Стоїть."; }
                    Stop();
                    _walkFar = Mathf.Clamp(Num(arg, 10f), 2f, 40f);
                    _walkA = _dummy.transform.position;
                    _walkB = _walkA + _dummy.transform.forward * _walkFar;
                    _walkToB = true;
                    _dummy.Order("go " + V(_walkB) + " near 0.5", NpcMind.Layer.Scene);
                    return "Ходить туди й назад, " + F(_walkFar) + " м. raven stage gait <хода>: " + BodyGait.Kinds;
                case "here":
                    if (!Bring(p)) return "Манекена нема.";
                    _dummy.Order("tp " + V(Before(p)), NpcMind.Layer.Scene);
                    _born = Time.time; // (a check waits till it stands again: just moved, its feet still settle — a take measured at once lied, 2026-11-04)
                    Face(p.transform.position);
                    return "Манекен тут.";
                case "turn":
                    {
                        if (!Bring(p)) return "Манекена нема.";
                        float deg = Num(arg, 90f);
                        Face(_dummy.transform.position + Quaternion.Euler(0f, deg, 0f) * _dummy.transform.forward * 3f);
                        _born = Time.time; // (as after «here»: measured once it has turned and stands)
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
                    TouchFixture(BodyClips.Find(Whole(arg))); // (what it says it touches: AnimStageFixtures)
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
                        // where it lies: as asked; else where the clip itself says it lies before it is taken (its «@Prop» at the start: the clip was made
                        // for that spot — 2026-11-07 the stage put a book elsewhere and every take measured a reach the scene never had); else before its feet
                        PAnimClip c = _clip.Length > 0 ? BodyClips.Find(_clip) : null;
                        bool told = c != null && c.Bones.Contains(BodyClips.PropAt);
                        Vector3 at = a.Length > 5 ? new Vector3(Num(a[3], 0f), Num(a[4], 0f), Num(a[5], 0f)) : told ? c.Value(BodyClips.PropAt, 0f) : new Vector3(0f, PropLow, PropAhead);
                        Vector3 turn = a.Length > 8 ? new Vector3(Num(a[6], 0f), Num(a[7], 0f), Num(a[8], 0f)) : told && c.Bones.Contains(BodyClips.PropTurn) ? c.Value(BodyClips.PropTurn, 0f) : Vector3.zero;
                        Transform root = _dummy.transform;
                        string why = StoryProps.Take(_dummy, new StoryProps.Ask { Item = arg, Free = true, Lasting = true, HasRest = true, RestAt = root.TransformPoint(at), RestTurn = root.rotation * Quaternion.Euler(turn) });
                        if (why != null) return why;
                        _sizeOf = true;
                        return "«" + arg + "» лежить перед манекеном. Розмір буде у звіті check.";
                    }
                case "edit":
                    {
                        BodyClips.Load();
                        PAnimClip c = BodyClips.Find(Whole(arg)); // (a part's file is its whole clip's)
                        if (c == null) return "Нема кліпу «" + arg + "».";
                        if (File.Exists(FileOf(arg))) return "Файл уже є: " + FileOf(arg);
                        string saved = BodyClips.Save(c);
                        return File.Exists(saved) ? "Кліп записано: " + saved : saved;
                    }
                case "check":
                    if (_clip.Length == 0) return "Нічого не грає.";
                    if (_hold >= 0f) return "Спершу go: зараз пауза.";
                    _checkViews = arg.Length > 0 ? arg : ""; // (none: the views the clip needs — AutoViews)
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
            return "raven stage up [шаблон] | down | here | turn <град> | play <кліп> [швидкість] | ab <кліп>|off | wall|table|seat|step|point|piece … · clear | stop | speed <x> | hold <с> | go | emote <жест>|none | prop <предмет>|none | edit <кліп> | check [види] | takes | back [n]";
        }

        // ---------------------------------------------------------------- the mannequin
        private bool Bring(Player p)
        {
            if (_dummy != null) return true;
            Vector3 at = Before(p);
            Vector3 look = p.transform.position - at; look.y = 0f;
            _dummy = Npcs.Spawn(_template, at, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity, p.GetPlayerID(), Tag);
            if (_dummy == null) return false;
            _born = Time.time;
            ZDO z = _dummy.Zdo;
            if (z != null) z.Persistent = false; // (left behind by a crash: gone with the session)
            _dummy.Order("stay", NpcMind.Layer.Scene); // (over everything it might think of)
            _dummy.Order("lookat " + V(at + _dummy.transform.forward * 10f + Vector3.up * 1.5f), NpcMind.Layer.Scene); // (its eyes held ahead: an idle one turns to a player close by — it turned 24° in a take, 2026-11-04)
            return true;
        }

        private static Vector3 Before(Player p)
        {
            Vector3 f = p.transform.forward; f.y = 0f;
            return p.transform.position + (f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward) * Ahead;
        }

        private void Face(Vector3 at) { if (_dummy == null) return; BodyTurn.ReportFor = _dummy.GetComponent<Character>(); _dummy.Order("lookat " + V(at + Vector3.up * 1.4f), NpcMind.Layer.Scene); }

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

        private void WatchFile()
        {
            if (_proving) return; // (its moments are being held and filmed: the file looked at after)
            DateTime now = StampOf(_clip);
            if (now == _stamp) return;
            _stamp = now;
            string why = null;
            try
            {
                if (now != DateTime.MinValue && BodyClips.Parse(File.ReadAllText(FileOf(_clip), Encoding.UTF8), _clip) == null) why = "у файлі помилка — грає попередня версія";
            }
            catch (IOException) { _stamp = DateTime.MinValue.AddTicks(1); return; } // quiet: still being written, looked at again in Look s
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
        private float _walkFar, _walkLook, _gaitEvery, _gaitNext;
        private int _gaitAt;
        private Vector3 _walkA, _walkB;
        private bool _walkToB;

        private void Update()
        {
            if (Player.m_localPlayer == null || ZNet.instance == null) { Destroy(gameObject); return; } // (the world left: the mannequin went with it)
            if (_gaitEvery > 0f && _dummy != null && Time.time >= _gaitNext)
            {
                _gaitNext = Time.time + _gaitEvery;
                string[] all = BodyGait.Names; // (the table's now: a kind added is in the round)
                string g = all[_gaitAt++ % all.Length];
                NpcGait.Force(_dummy, g);
                _dummy.Say(g + " (" + ((_gaitAt - 1) % all.Length + 1) + "/" + all.Length + ")"); // (over its head: the stage's own line shows only with a clip)
            }
            if (_walkFar > 0f && _dummy != null && Time.time >= _walkLook)
            {
                _walkLook = Time.time + 0.5f;
                Vector3 goal = _walkToB ? _walkB : _walkA;
                if (WorldSpot.Flat(_dummy.transform.position, goal) < NpcMind.Reach(0.5f) + 0.3f) { _walkToB = !_walkToB; _dummy.Order("go " + V(_walkToB ? _walkB : _walkA) + " near 0.5", NpcMind.Layer.Scene); }
            }
            if (_other != null && _clipB.Length > 0 && Time.time >= _againB) // (the second take, round and round beside it)
            {
                Humanoid hb = _other.GetComponent<Humanoid>();
                PAnimClip cb = BodyClips.Find(_clipB);
                if (hb != null && cb != null) { BodyClips.Play(hb, Slot, _clipB, 0.2f, _speed); _againB = cb.Loop ? float.MaxValue : Time.time + cb.Length / Mathf.Max(0.05f, _speed) + Gap; }
            }
            if (_fixtures.Count > 0) PlaceFixtures();
            if (_clip.Length == 0) return;
            if (_checkAt > 0f && Time.time >= _checkAt) { _checkAt = 0f; StartCheck(); }
            if (_checkPlay > 0f && Time.time >= _checkPlay) { _checkPlay = 0f; CheckRun(); }
            if (Time.time >= _nextLook) { _nextLook = Time.time + Look; WatchFile(); }
            if (_again > 0f && _hold < 0f && Time.time >= _again) Begin(0.2f);
        }

        private void OnDestroy()
        {
            if (_me == this) _me = null;
            if (_dummy != null) _dummy.Remove();
            if (_other != null) _other.Remove();
            ClearFixtures();
        }

        // ---------------------------------------------------------------- the measure
        // The rules of a pose are BodyCheck's (the same as a scene's check). Here only what is the stage's own: did the mannequin stand
        // still, does the clip start from the game's stance, do its parts join, is it what the mod ships.
        private Check _check;
        internal static float Born = 5f; // (its smoke and its first pose: not the clip's)
        private float _born = -99f, _checkAt, _checkPlay;
        internal static float Rest = 1f, StanceNear = 0.08f, FootUp = 0.14f;
        internal const int MostProofs = 6;
        private bool _proving;

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
            Humanoid h = _dummy != null ? _dummy.GetComponent<Humanoid>() : null;
            if (h == null) return;
            _check = new Check(_clip, c.Length / Mathf.Max(0.05f, _speed) + (c.Loop ? 0f : c.FadeOut + 0.2f));
            _check.Body = new BodyCheck(h);
            FixturesInto(_check.Body);
            _check.Body.Wants(c);
            Begin(Fade);
            if (!c.Loop) _again = _check.Until + Gap;
            // filmed in this same run, at its key moments, from the views it needs (one run, not one to measure and one to film)
            Check run = _check;
            BodyAnimator an = Animator();
            _check.Views = _checkViews.Length > 0 ? _checkViews : AutoViews(c);
            _check.Keys = KeyTimes(c);
            if (an != null) StartCoroutine(Studio.StageFilm(_dummy, "stage_" + _clip, _check.Views, _check.Keys, () => an.TimeIn(Slot), () => _check == run, _check.Shots, () => run.Body.Words() + "\n  layers: " + an.LayersSaid()));
        }

        // the views a clip needs: always the whole body from every side (Around: a front view hid an elbow behind the head, a side
        // view which way it faced — 2026-11-07), and over that hands before the body — the chest close, from the front and the side;
        // feet — from the side; fingers, a hold, a touch — their own close views
        internal const string Around = "front,side,back,three,top";
        internal static float HeadFrom = 1.3f; // m: a hand target this high films the head close
        internal static string AutoViews(PAnimClip c)
        {
            bool hands = false, feet = false, head = false; // (head: a hand goes up to the head — its own close views: an arm sank into it unseen, 2026-11-07)
            string fingers = ""; // (a clip that moves fingers: their own close view — 2026-11-06, a crooked finger the hand views did not show)
            foreach (string b in c.Bones)
            {
                if (b.StartsWith("RightHand", System.StringComparison.Ordinal) && b.Length > 9 && !b.Contains(".") && !fingers.Contains("fingerr")) fingers += ",fingerr";
                if (b.StartsWith("LeftHand", System.StringComparison.Ordinal) && b.Length > 8 && !b.Contains(".") && !fingers.Contains("fingerl")) fingers += ",fingerl";
                if (b == "@RightHand.on" && !fingers.Contains("holdr")) fingers += ",holdr"; // (a hand on the thing: how it holds it, from its palm's side)
                if (b == "@LeftHand.on" && !fingers.Contains("holdl")) fingers += ",holdl";
                if (b == "@LeftHand" || b == "@RightHand") { float hi = 0f; foreach (PAnimKey k in c.Keys) { Vector3 v; if (k.V.TryGetValue(b, out v)) hi = Mathf.Max(hi, v.y); } if (hi > 0.8f) hands = true; if (hi > HeadFrom) head = true; }
                if (b == "@LeftFoot" || b == "@RightFoot" || b == BodyClips.Move) feet = true;
            }
            if (head) fingers += ",head,headside,headtop";
            if (c.Touch.Length > 0) return Around + ",contact,chestside" + fingers; // (what it touches shows from the side; from the front it stands between the camera and the body)
            return Around + (hands ? ",chest,chestside" : "") + fingers;
        }

        // the moments to look at: its start, each part's end and middle, where a hand or a foot takes hold or lets go, where a foot
        // leaves the ground and lands, its end — at most 12, spread over the whole clip, a quarter of a second apart at least; and
        // every moment a «want» names
        internal static List<float> KeyTimes(PAnimClip c)
        {
            List<float> t = new List<float> { 0.1f, c.Length - 0.05f };
            foreach (PAnimPart pt in c.Parts) { t.Add(pt.From + 0.05f); t.Add((pt.From + pt.To) * 0.5f); t.Add(pt.To - 0.05f); }
            foreach (string b in c.Bones)
            {
                if (!b.StartsWith("@", StringComparison.Ordinal)) continue;
                bool weight = b.EndsWith(".w", StringComparison.Ordinal), foot = b == "@LeftFoot" || b == "@RightFoot";
                if (!weight && !foot) continue;
                bool seen = false, was = false;
                foreach (PAnimKey k in c.Keys)
                {
                    Vector3 v;
                    if (!k.V.TryGetValue(b, out v)) continue;
                    bool on = weight ? v.x >= 0.95f : v.y > FootUp;
                    if (seen && was != on) t.Add(k.T);
                    was = on; seen = true;
                }
            }
            if (c.Parts.Count == 0) for (int i = 1; i < 4; i++) t.Add(c.Length * i / 4f);
            t.Sort();
            List<float> near = new List<float>();
            foreach (float x in t) if (x >= 0f && x <= c.Length && (near.Count == 0 || x - near[near.Count - 1] >= 0.25f)) near.Add(x);
            List<float> kept = new List<float>();
            if (near.Count <= 12) kept.AddRange(near);
            else for (int i = 0; i < 12; i++) kept.Add(near[i * (near.Count - 1) / 11]); // (spread: the whole clip, not its two ends)
            foreach (string want in c.Wants)
            {
                string first = want.Split(' ')[0];
                int dash = first.IndexOf('-');
                float at;
                if (!float.TryParse(dash > 0 ? first.Substring(0, dash) : first, NumberStyles.Float, CultureInfo.InvariantCulture, out at)) continue;
                bool have = false;
                foreach (float x in kept) if (Mathf.Abs(x - at) < 0.2f) have = true;
                if (!have && at <= c.Length) kept.Add(at);
            }
            kept.Sort();
            if (kept.Count > 16) kept.RemoveRange(16, kept.Count - 16);
            return kept;
        }

        private sealed class Check
        {
            public string Clip; public float Until, Last;
            public Vector3 RootAt; public Quaternion RootTurn; public bool Rooted;
            public Vector3 GameL, GameR; public bool GameSeen; // (its feet in the game's own stance, before the clip has weight)
            public float TurnedAt, TurnedNear = 99f, Moved, Turned, Near = 99f, Frames;
            public readonly StringBuilder Rows = new StringBuilder();
            public BodyCheck Body;
            public string Views = "", Said = "", Text = "";
            public List<float> Keys = new List<float>();
            public readonly List<string> Shots = new List<string>(), Proofs = new List<string>();
            public bool ProofsDone = true;
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
            float at = an.TimeIn(Slot);
            c.Frames++;
            Stood(c, h, vis, root, an);
            c.Body.Measure(at);
            if (Time.time - c.Last >= 0.25f)
            {
                c.Last = Time.time;
                c.Rows.Append(F(at)).Append("  turned ").Append(F(Mathf.DeltaAngle(c.RootTurn.eulerAngles.y, root.eulerAngles.y))).Append("  ").Append(c.Body.Row()).Append('\n');
            }
            if (Time.time < c.Until) return;
            _check = null;
            Finish(c);
        }

        // each frame of a measure: did it stand as it should
        private void Stood(Check c, Humanoid h, Transform vis, Transform root, BodyAnimator an)
        {
            if (!c.Rooted) { c.Rooted = true; c.RootAt = root.position; c.RootTurn = root.rotation; }
            if (!c.GameSeen) { c.GameSeen = true; c.GameL = Local(h, vis, root, "LeftFoot"); c.GameR = Local(h, vis, root, "RightFoot"); }
            c.Moved = Mathf.Max(c.Moved, Vector3.Distance(root.position, c.RootAt));
            float nearNow = Player.m_localPlayer != null ? Vector3.Distance(Player.m_localPlayer.transform.position, root.position) : 99f;
            float turnNow = Quaternion.Angle(root.rotation, c.RootTurn);
            if (turnNow > c.Turned) { c.Turned = turnNow; c.TurnedAt = an.TimeIn(Slot); c.TurnedNear = nearNow; } // (when, and how near the player was: what turned it)
            c.Near = Mathf.Min(c.Near, nearNow);
        }

        // the run over: said at once, then each mistake's moment held and filmed, and said again with those frames
        private void Finish(Check c)
        {
            c.Said = Verdict(c);
            List<float> times = new List<float>();
            foreach (KeyValuePair<float, string> m in c.Body.Moments)
            {
                bool have = false;
                foreach (float x in times) if (Mathf.Abs(x - m.Key) < 0.25f) have = true;
                if (!have && times.Count < MostProofs) times.Add(m.Key);
            }
            c.ProofsDone = times.Count == 0;
            Report(c);
            if (times.Count == 0) return;
            _proving = true;
            _again = 0f;
            bool first = true;
            StartCoroutine(Studio.StageProof(_dummy, "stage_" + c.Clip + "_worst", ProofViews, times, t =>
            {
                _hold = t;
                if (first) { first = false; Begin(Fade); } else { BodyAnimator a = Animator(); if (a != null) a.Hold(Slot, t); }
            }, () => { BodyAnimator a = Animator(); return c.Body.Words() + (a != null ? "\n  layers: " + a.LayersSaid() : ""); }, c.Proofs, () =>
            {
                _proving = false;
                _hold = -1f;
                if (_clip.Length > 0) Begin(Fade);
                c.ProofsDone = true;
                Report(c);
            }));
        }
        private const string ProofViews = "contact,chestside,side";

        // the mistakes already made once, each a line when it is made again; none: OK. Then what was asked
        private string Verdict(Check c)
        {
            List<string> bad = new List<string>();
            if (c.Moved > 0.05f || c.Turned > 5f) bad.Add("DO NOT TRUST THIS TAKE: the mannequin moved " + F(c.Moved) + " m, turned " + F(c.Turned) + " deg (most at " + F(c.TurnedAt) + " s, the player " + F(c.TurnedNear) + " m from it then)");
            if (c.Near < 2f) bad.Add("the player came within " + F(c.Near) + " m (stand 4-5 m off: he pushes it and blocks the view)");
            c.Body.Verdict(bad, "");
            string asked = c.Body.WantsSaid(bad);
            PAnimClip played = BodyClips.Find(c.Clip);
            if (played != null && played.Bones.Contains("@LeftFoot") && played.Bones.Contains("@RightFoot") && !played.IsPart) // (a clip that starts from another stance: its feet slide over the ground on its way in)
            {
                float off = Mathf.Max(Vector3.Distance(played.Value("@LeftFoot", 0f), c.GameL), Vector3.Distance(played.Value("@RightFoot", 0f), c.GameR));
                if (off > StanceNear) bad.Add("the first pose's feet are " + F(off) + " m from the game's stance (L " + P(c.GameL) + ", R " + P(c.GameR) + "): they slide on the way in");
            }
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
            return (bad.Count == 0 ? "VERDICT: OK\n" : "VERDICT:\n  - " + string.Join("\n  - ", bad.ToArray()) + "\n") + asked;
        }

        // what plays here and what the built mod has: a clip changed since the last build plays nowhere else till it is built
        private static string BakedDiffers(string clip)
        {
            if (!File.Exists(FileOf(clip))) return null;
            try
            {
                using (Stream st = typeof(BodyClips).Assembly.GetManifestResourceStream("RavenWatch.clips." + clip + ".txt"))
                {
                    if (st == null) return "not in the built mod yet: build it to ship";
                    using (StreamReader rd = new StreamReader(st, Encoding.UTF8))
                        return rd.ReadToEnd().Replace("\r", "") == File.ReadAllText(FileOf(clip), Encoding.UTF8).Replace("\r", "") ? null : "changed since the last build: build it to ship";
                }
            }
            catch (System.Exception e) { return "its file not read: " + e.Message; } // quiet: not hidden — the reason goes into the VERDICT line it returns
        }

        // the part a moment of a whole clip falls in (« (loop)»), none: ""
        private static string PartAt(PAnimClip c, float t)
        {
            if (c == null || c.IsPart) return "";
            foreach (PAnimPart pt in c.Parts) if (t >= pt.From && t < pt.To + 0.001f) return " (" + pt.Name + ")";
            return "";
        }

        private static string P(Vector3 v) { return v.x.ToString("0.00", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.00", CultureInfo.InvariantCulture) + "," + v.z.ToString("0.00", CultureInfo.InvariantCulture); }
        private static Vector3 Local(Humanoid h, Transform vis, Transform root, string bone) { Transform t = BodyClips.SkeletonBone(h, vis, bone); return t != null ? root.InverseTransformPoint(t.position) : Vector3.zero; }

        // first thing in the file what it was asked to be; then the verdict; the rest for looking things up. «frames of the mistakes:
        // coming» while they are being filmed (a reader waits for it to go: tools/take.js)
        private void Report(Check c)
        {
            StringBuilder s = new StringBuilder();
            s.Append(c.Clip).Append(", take ").Append(_take).Append(", ×").Append(F(_speed)).Append(", ").Append(c.Frames.ToString("0")).Append(" frames\n");
            PAnimClip shown = BodyClips.Find(c.Clip), whole = BodyClips.Find(Whole(c.Clip));
            if (whole != null && whole.Say.Length > 0) s.Append("said: ").Append(whole.Say).Append('\n');
            s.Append(c.Said);
            s.Append("pose: ").Append(c.Body.Numbers()).Append('\n');
            s.Append(c.Body.TempoSaid());
            s.Append("key frames: studio/out/stage_").Append(c.Clip).Append("_<view>.png, views ").Append(c.Views).Append(", 4 a row\n");
            foreach (string shot in c.Shots) s.Append("  ").Append(shot).Append('\n');
            if (!c.ProofsDone) s.Append("frames of the mistakes: coming\n");
            else if (c.Proofs.Count > 0)
            {
                s.Append("frames of the mistakes: studio/out/stage_").Append(c.Clip).Append("_worst_<view>.png, views ").Append(ProofViews).Append(", 4 a row\n");
                foreach (string shot in c.Proofs) s.Append("  ").Append(shot).Append('\n');
            }
            StoryPropView view = _dummy != null ? _dummy.GetComponent<StoryPropView>() : null;
            if (view != null && _sizeOf) s.Append("prop size ").Append(P(view.Size)).Append(" m\n");
            if (shown != null && shown.Parts.Count > 0) { s.Append("parts:"); foreach (PAnimPart pt in shown.Parts) s.Append(' ').Append(pt.Name).Append(' ').Append(F(pt.From)).Append('-').Append(F(pt.To)); s.Append('\n'); }
            s.Append("fastest:\n").Append(c.Body.Fastest());
            s.Append("\nevery 0.25 s (m, body frame: x right, y up, z ahead; lean forward,right and head up,right in degrees):\n").Append(c.Rows);
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
            Cell(0f, y, mid - u * 3.4f, _clip + PartAt(c, a.TimeIn(Slot)), TextAnchor.UpperRight); // (the part playing now: a whole clip plays all of them, round)
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
