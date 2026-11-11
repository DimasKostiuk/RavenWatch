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
    //   raven stage wield <weapon> [shield] · wield none            a real weapon (and shield) in its hands: the game's own stance for it under the clip, as a
    //                                                              guard holds it (a pose for a weapon in hand: NpcStance.Pose.Hands, NpcGear.HandsOf)
    //   raven stage held                                           where the weapon in its right hand points now, in the body's and the hand's frame (NpcGear.HeldAxis)
    //   raven stage fright [s]                                     a fright for that long (NpcFeel): on its guard, then at its ease again — its weapon's stances
    //   raven stage anim [name [value]]                            the animator's own values (statei/statef: the game's stance for a weapon), read or set: a measure
    //   raven stage prop <item> [x y z [rx ry rz]] · prop none       a thing lying before it for the clip's @Prop channels
    //                                                              (metres and degrees in its frame), as a scene's «free» prop
    //   raven stage prop <item> arm                                on its right forearm, as a person carries its own (StoryProps «arm»):
    //                                                              the arm's clips (basket_carry, basket_set) taken with the thing on
    //   raven stage edit <clip>                                    a built-in clip written out as a file, to edit
    //   raven stage rec · rec off                                  the player's own moves recorded, jerks marked (PAnimLog), for
    //                                                              a clip played on him in a scene
    //   raven stage takes · back [n]                               the takes of the clip · the file put back n takes
    //   raven stage check [views]                                  one run of the clip measured every frame by BodyCheck (the same
    //                                                              rules as a scene's check), into studio/out/stage_<clip>.txt: on
    //                                                              top a VERDICT, each line a mistake already made once, with what
    //                                                              to change (and a «fix:» command where the core can reckon it); then what
    //                                                              was asked (the clip's «want» lines), the tempo strips, each key frame's
    //                                                              pose in words. After the run its KEY SHEET, stage_<clip>_key.png (StageKey):
    //                                                              a row a moment — the mistakes, the wants, the marks (KeyMoments) — each
    //                                                              held and filmed in the two views that show that kind of mistake (KeyViews),
    //                                                              labelled in the picture; and, when a second mannequin plays another take
    //                                                              («ab», or by itself the take before this one), stage_<clip>_diff.png: this
    //                                                              take | the other at the same moments. Sheets of the whole run from given
    //                                                              views (stage_<clip>_<view>.png) only when views are asked (review 2
    //                                                              2026-11-10: ten sheets of 19 MB a take and the main thing unseen).
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
                if (what == "status") return "stage down"; // (anim.js status: never made yet)
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
                case "wield":
                    {
                        if (!Bring(p)) return "Манекена нема.";
                        if (arg.Length == 0) return "raven stage wield <зброя> [щит] · wield none";
                        string shield = a.Length > 3 ? a[3] : "";
                        string why = NpcGear.Arm(_dummy, arg == "none" ? "" : arg, shield); // (the core's one way to put a weapon in a body's hands)
                        if (why != null) return why;
                        return arg == "none" ? "Руки порожні." : "У руках: " + arg + (shield.Length > 0 ? " і " + shield : "") + " · стійка гри: " + NpcGear.HandsOf(_dummy) + ". «raven stage held» — куди вона лежить.";
                    }
                case "anim": // (a measure, 2026-11-08: which of the animator's own values picks the game's stance for a weapon, and whether one set stays)
                    {
                        if (_dummy == null) return "Манекена нема.";
                        Animator an = _dummy.GetComponentInChildren<Animator>();
                        if (an == null) return "Нема аніматора.";
                        if (a.Length > 3)
                        {
                            float v = Num(a[3], 0f);
                            foreach (AnimatorControllerParameter q in an.parameters)
                                if (q.name == arg) { if (q.type == AnimatorControllerParameterType.Int) an.SetInteger(arg, Mathf.RoundToInt(v)); else if (q.type == AnimatorControllerParameterType.Float) an.SetFloat(arg, v); else if (q.type == AnimatorControllerParameterType.Bool) an.SetBool(arg, v != 0f); }
                        }
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        foreach (AnimatorControllerParameter q in an.parameters)
                        {
                            if (arg.Length > 0 && a.Length <= 3 && q.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
                            if (arg.Length > 0 && a.Length > 3 && q.name != arg && q.name != "statei" && q.name != "statef") continue;
                            sb.Append(q.name).Append('=').Append(q.type == AnimatorControllerParameterType.Int ? an.GetInteger(q.name).ToString() : q.type == AnimatorControllerParameterType.Float ? an.GetFloat(q.name).ToString("0.##", CultureInfo.InvariantCulture) : q.type == AnimatorControllerParameterType.Bool ? (an.GetBool(q.name) ? "1" : "0") : "t").Append(' ');
                        }
                        return sb.ToString();
                    }
                case "fright": // (on its guard by the core's own way — a fright, NpcFeel: its weapon's stance comes and goes as on anyone, NpcGear.Stance)
                    {
                        if (_dummy == null) return "Манекена нема.";
                        float sec = arg.Length > 0 ? Num(arg, 6f) : 6f;
                        NpcFeel.Feel(_dummy, NpcFeel.Fright, sec, _dummy.transform.position + _dummy.transform.forward * 3f, false);
                        return "Наляканий на " + sec.ToString("0") + " с (насторожі: стійка зброї бойова), далі спокій.";
                    }
                case "held":
                    {
                        if (_dummy == null) return "Манекена нема.";
                        return NpcGear.HeldAxis(_dummy); // (where the weapon in its hand points now, under the clip playing: the pose's wrist reckoned from it)
                    }
                case "prop":
                    {
                        if (!Bring(p)) return "Манекена нема.";
                        if (arg.Length == 0) return "raven stage prop <предмет> [x y z [rx ry rz]] | <предмет> arm · prop none";
                        _dummy.Act("prop -"); // (the stage's own, whatever it was: StoryProps.Put lets a thing on the arm be, as a person's own)
                        if (arg == "none") return "Реквізит прибрано.";
                        if (a.Length > 3 && a[3] == "arm") // (on its right forearm, as a person carries its own — Heid's basket: StoryProps «carry … arm»; the clip moves the arm, the thing follows it)
                        {
                            string onArm = StoryProps.Take(_dummy, new StoryProps.Ask { Item = arg, Hand = "a", Lasting = true });
                            return onArm ?? "«" + arg + "» на правому передпліччі манекена.";
                        }
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
                    _checkViews = arg.Length > 0 ? arg : ""; // (none: no sheets of the run, the key sheet after it; AutoViews names what it would take)
                    {
                        float early = _born + Born - Time.time;
                        if (early > 0f) { _checkAt = Time.time + early; return "Манекен ще з'являється: заміряю за " + F(early) + " с."; }
                        StartCheck();
                        return "Заміряю один прохід.";
                    }
                case "status": // (one line for anim.js status: up or down, on what, playing what)
                    return _dummy == null ? "stage down" : "stage up (" + _template + ")" + (_clip.Length > 0 ? ", plays " + _clip + (_speed != 1f ? " at " + _speed.ToString("0.##", CultureInfo.InvariantCulture) : "") : ", plays nothing") + (_other != null ? ", ab " + _clipB : "") + (_hold >= 0f ? ", held at " + _hold.ToString("0.0", CultureInfo.InvariantCulture) + " s" : "");
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
            if (_prevClip != null) { BodyClips.Clips.Remove(_prevClip); _prevClip = null; }
            ClearFixtures();
        }

        // ---------------------------------------------------------------- the measure
        // The rules of a pose are BodyCheck's (the same as a scene's check). Here only what is the stage's own: did the mannequin stand
        // still, does the clip start from the game's stance, do its parts join, is it what the mod ships.
        private Check _check;
        internal static float Born = 5f; // (its smoke and its first pose: not the clip's)
        private float _born = -99f, _checkAt, _checkPlay;
        internal static float Rest = 1f, StanceNear = 0.08f, FootUp = 0.14f;
        internal const int KeyMost = 4, KeyRows = 6; // (the key sheet: mistakes at most, rows at most)
        private bool _proving;
        private string _prevClip; // (the take before this one, registered as a clip for the second mannequin while the key sheet is made)

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
            _check.Views = _checkViews; // (none: the words of the key frames only; the key sheet after the run shows the picture — AutoViews says what a whole run's sheets would be)
            _check.Keys = KeyTimes(c);
            if (an != null) StartCoroutine(Studio.StageFilm(_dummy, "stage_" + _clip, _check.Views, _check.Keys, () => an.TimeIn(Slot), () => _check == run, _check.Shots, () => run.Body.Words() + "\n  layers: " + an.LayersSaid(), () => MarksOf(run)));
        }

        private static List<Studio.Mark> MarksOf(Check run) { List<Studio.Mark> l = new List<Studio.Mark>(); if (run != null && run.Body != null) run.Body.Marks(l); return l; }

        // the views a clip needs: always the whole body from every side (Around: a front view hid an elbow behind the head, a side
        // view which way it faced — 2026-11-07), and over that hands before the body — the chest close, from the front, both sides and above;
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
            if (c.Touch.Length > 0) return Around + ",contact,chestside,chesttop" + fingers; // (what it touches shows from the side; from the front it stands between the camera and the body)
            return Around + (hands ? ",chest,chestside,chestleft,chesttop" : "") + fingers; // (hands before the body: close from four sides — a front view shows no depth, the whole body from above is too small: a hand 40 cm off the belly passed, 2026-11-09)
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
            public string Views = "", Said = "", Text = "", Other; // (Other: what the second mannequin plays beside it on the diff sheet, null none)
            public List<float> Keys = new List<float>();
            public readonly List<string> Shots = new List<string>(), KeyRows = new List<string>();
            public readonly List<Studio.KeyMoment> Moments = new List<Studio.KeyMoment>();
            public bool KeyDone = true;
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

        // the run over: said at once, then the key sheet — each moment held and filmed in the views for it — and said again with its rows
        private void Finish(Check c)
        {
            c.Said = Verdict(c);
            KeyMoments(c);
            c.KeyDone = false;
            Report(c);
            _proving = true;
            _again = 0f;
            bool first = true;
            NpcActor other = OtherFor(c);
            StartCoroutine(Studio.StageKey(_dummy, other, c.Other, "stage_" + c.Clip, c.Moments, t =>
            {
                _hold = t;
                if (first) { first = false; Begin(Fade); } else { BodyAnimator a = Animator(); if (a != null) a.Hold(Slot, t); }
                Humanoid hb = other != null ? other.GetComponent<Humanoid>() : null; BodyAnimator ab = hb != null ? BodyAnimator.For(hb) : null;
                if (ab != null) ab.Hold(Slot, t);
            }, () => { BodyAnimator a = Animator(); return c.Body.Words() + (a != null ? "\n  layers: " + a.LayersSaid() : ""); }, c.KeyRows, () =>
            {
                _proving = false;
                _hold = -1f;
                if (_clip.Length > 0) Begin(Fade);
                if (_prevClip != null) // (the take before: its mannequin and its clip go with the sheet)
                {
                    if (_other != null) { _other.Remove(); _other = null; }
                    BodyClips.Clips.Remove(_prevClip); _prevClip = null; _clipB = "";
                }
                else { Humanoid hb = _other != null ? _other.GetComponent<Humanoid>() : null; BodyAnimator ab = hb != null ? BodyAnimator.For(hb) : null; if (ab != null) ab.Hold(Slot, -1f); }
                c.KeyDone = true;
                Report(c);
            }, () => MarksOf(c)));
        }

        // the moments of the key sheet: the mistakes the VERDICT names (KeyMost at most, a quarter second apart), then the moments the clip's «want»
        // lines name and its marks (KeyRows in all); none of these — its start, its middle, its end. Each with the two views for its kind
        private void KeyMoments(Check c)
        {
            PAnimClip clip = BodyClips.Find(c.Clip);
            List<Studio.KeyMoment> m = c.Moments;
            m.Clear();
            int mistakes = 0;
            foreach (KeyValuePair<float, string> x in c.Body.Moments)
            {
                if (x.Key < 0f || Near(m, x.Key) || mistakes >= KeyMost) continue;
                int tag = x.Value.IndexOf('[');
                string line = tag >= 0 ? x.Value.Substring(tag) : x.Value;
                int nl = line.IndexOf('\n'); if (nl > 0) line = line.Substring(0, nl);
                m.Add(new Studio.KeyMoment { T = x.Key, Label = line, Views = KeyViews(line) });
                mistakes++;
            }
            if (clip != null)
            {
                foreach (string want in clip.Wants)
                {
                    if (m.Count >= KeyRows) break;
                    string[] w = want.Split(' ');
                    string first = w[0]; int dash = first.IndexOf('-');
                    float at;
                    if (!float.TryParse(dash > 0 ? first.Substring(0, dash) : first, NumberStyles.Float, CultureInfo.InvariantCulture, out at) || Near(m, at)) continue;
                    m.Add(new Studio.KeyMoment { T = at, Label = "want " + want, Views = KeyViews("[want] " + want) });
                }
                foreach (PAnimEvent mk in clip.Marks) { if (m.Count >= KeyRows) break; if (!Near(m, mk.T)) m.Add(new Studio.KeyMoment { T = mk.T, Label = "mark " + mk.Sound, Views = KeyViews("") }); }
                if (m.Count == 0) foreach (float t in new[] { 0.1f, clip.Length * 0.5f, clip.Length - 0.05f }) m.Add(new Studio.KeyMoment { T = t, Label = t < 0.2f ? "start" : t > clip.Length - 0.1f ? "end" : "middle", Views = clip.Bones.Contains("@LeftHand") || clip.Bones.Contains("@RightHand") ? "chestside,three" : "side,three" });
            }
            m.Sort((p, q) => p.T.CompareTo(q.T));
        }
        private static bool Near(List<Studio.KeyMoment> m, float t) { foreach (Studio.KeyMoment x in m) if (Mathf.Abs(x.T - t) < 0.25f) return true; return false; }

        // the two views that show a kind of mistake (by the rule's tag and the hand it names): a wrist, a fist, a grip — the hand close from its
        // side and from its palm's; a miss, a lean — the whole body from the side and the chest; a slide — the feet; a head — the head
        internal static string KeyViews(string line)
        {
            string tag = BodyCheck.TagOf(line) ?? "";
            bool left = line.IndexOf("left", StringComparison.Ordinal) >= 0 || line.IndexOf("@LeftHand", StringComparison.Ordinal) >= 0;
            string hand = left ? "handl,holdl" : "handr,holdr", finger = left ? "fingerl,handl" : "fingerr,handr";
            switch (tag)
            {
                case "bend": case "twist": case "palm": case "fist": case "grip": case "prop-fingers": return hand;
                case "miss": return "side,chestside";
                case "touch": return "contact,side";
                case "slide": case "sole": case "stance": case "knee": case "knee-under": return "feet,side";
                case "fast": case "jerk": return "side,three";
                case "prop-head": case "prop-limb": case "head": case "look": case "neck": return "headside,head";
                case "inside": case "past": case "elbow": case "fold": case "shoulder": case "free": return "chestside,chest";
                case "seat": return "side,back";
                case "want":
                    {
                        string[] w = line.Replace("[want] ", "").Split(' ');
                        string kind = w.Length > 1 ? w[1] : "";
                        if (kind == "lean" || kind == "hips") return "side,chestside";
                        if (kind == "head") return "headside,head";
                        if (kind == "hand") return "chestside,chesttop";
                        if (kind == "palm") return hand;
                        if (kind == "finger") return finger;
                        if (kind == "foot" || kind == "knee" || kind == "weight") return "feet,side";
                        return "side,three";
                    }
            }
            return "side,three";
        }

        // the second mannequin for the diff sheet: the one «ab» has (its clip said), else one brought for the take before this one (its file
        // among the takes, registered as a clip of its own while the sheet is made); null: no take before, or a part plays
        private NpcActor OtherFor(Check c)
        {
            if (_other != null && _clipB.Length > 0) { c.Other = "ab: " + _clipB; return _other; }
            List<string> t = Takes(c.Clip);
            if (c.Clip.IndexOf('.') > 0 || t.Count < 2 || _dummy == null || Player.m_localPlayer == null) return null;
            PAnimClip prev;
            try { prev = BodyClips.Parse(File.ReadAllText(t[t.Count - 2], Encoding.UTF8), Whole(c.Clip) + "~prev"); }
            catch (Exception e) { Story.Log("stage: the take before not read: " + e.Message); return null; }
            if (prev == null) return null;
            prev.Name = Whole(c.Clip) + "~prev";
            BodyClips.Clips[prev.Name] = prev;
            _prevClip = prev.Name;
            Transform r0 = _dummy.transform;
            _other = Npcs.Spawn(_template, r0.position + r0.right * AbAside, r0.rotation, Player.m_localPlayer.GetPlayerID(), Tag);
            if (_other == null) { BodyClips.Clips.Remove(_prevClip); _prevClip = null; return null; }
            if (_other.Zdo != null) _other.Zdo.Persistent = false;
            _other.Order("stay", NpcMind.Layer.Scene);
            Humanoid hb = _other.GetComponent<Humanoid>();
            if (hb != null) BodyClips.Play(hb, Slot, prev.Name, 0.2f, _speed);
            _clipB = prev.Name; _againB = float.MaxValue; // (held by the sheet, never started over by Update)
            c.Other = "take " + (t.Count - 1);
            return _other;
        }

        // how far the solid ground goes up and down round the mannequin's feet (its middle and four points GroundAround off it): take_book's
        // mannequin stood on a stone, 2026-11-10, its feet «slid» 0.46 m and nothing said why. Five reads of the game's height, once a check
        internal static float GroundEven = 0.06f, GroundAround = 0.35f; // m
        private float GroundRough()
        {
            if (_dummy == null || ZoneSystem.instance == null) return 0f;
            Transform r = _dummy.transform;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Vector3 off in new[] { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                float y = ZoneSystem.instance.GetSolidHeight(r.TransformPoint(off * GroundAround));
                lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
            }
            return hi - lo;
        }

        // the mistakes already made once, each a line when it is made again; none: OK. Then what was asked
        private string Verdict(Check c)
        {
            List<string> bad = new List<string>();
            if (c.Moved > 0.05f || c.Turned > 5f) bad.Add("DO NOT TRUST THIS TAKE: the mannequin moved " + F(c.Moved) + " m, turned " + F(c.Turned) + " deg (most at " + F(c.TurnedAt) + " s, the player " + F(c.TurnedNear) + " m from it then)");
            if (c.Near < 2f) bad.Add("the player came within " + F(c.Near) + " m (stand 4-5 m off: he pushes it and blocks the view)");
            float rough = GroundRough();
            if (rough > GroundEven) bad.Add("DO NOT TRUST THIS TAKE: the ground under the mannequin is not flat (" + F(rough) + " m up and down within " + F(GroundAround) + " m of its feet: a stone, a slope) — the feet, the knees, a slide are measured on it; the player takes it to flat ground first (raven stage down, then up there)");
            c.Body.Verdict(bad, "");
            string asked = c.Body.WantsSaid(bad);
            PAnimClip played = BodyClips.Find(c.Clip);
            if (played != null && played.Bones.Contains("@LeftFoot") && played.Bones.Contains("@RightFoot") && !played.IsPart) // (a clip that starts from another stance: its feet slide over the ground on its way in)
            {
                float off = Mathf.Max(Vector3.Distance(played.Value("@LeftFoot", 0f), c.GameL), Vector3.Distance(played.Value("@RightFoot", 0f), c.GameR));
                if (off > StanceNear) bad.Add("[stance] the first pose's feet are " + F(off) + " m from the game's stance (L " + P(c.GameL) + ", R " + P(c.GameR) + "): they slide on the way in" + BodyCheck.FixLine("batch " + Whole(c.Clip) + " \"set @LeftFoot " + F(BodyCheck.KeyNear(Whole(c.Clip), "@LeftFoot", 0f)) + " " + V(c.GameL) + "; set @RightFoot " + F(BodyCheck.KeyNear(Whole(c.Clip), "@RightFoot", 0f)) + " " + V(c.GameR) + "\""));
            }
            PAnimClip whole = BodyClips.Find(Whole(c.Clip));
            if (whole != null)
            {
                if (whole.Bones.Count >= BodyClips.MaxBones) bad.Add("[channels] the clip is at " + BodyClips.MaxBones + " channels: what was over is left out (the log says which)");
                for (int i = 0; i < whole.Parts.Count; i++)
                {
                    PAnimPart pt = whole.Parts[i];
                    PAnimClip one = BodyClips.Find(whole.Name + "." + pt.Name);
                    PAnimClip next = pt.How == "loop" ? one : i + 1 < whole.Parts.Count ? BodyClips.Find(whole.Name + "." + whole.Parts[i + 1].Name) : null;
                    string ch;
                    if (one != null && next != null && BodyClips.JoinGap(one, next, out ch) > BodyClips.JoinNear) bad.Add("[join] part " + pt.Name + ": its join shows (" + ch + ")");
                }
                string baked = BakedDiffers(whole.Name);
                if (baked != null) bad.Add("[built] " + baked);
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
        // coming» while they are being filmed (a reader waits for it to go: tools/anim.js).
        // Two files (review 2 2026-11-10: 12 KB a take, half of it never read): stage_<clip>.txt is the short one — the head with the clip
        // file's hash and time (tools/anim.js holds it against the file: a report of another version is not read), said, VERDICT, asked, the
        // numbers, the tempo, each key frame's pose in words; stage_<clip>.full.txt has all that and the «layers:» of each frame, the
        // fastest bones, the rows every quarter second
        private void Report(Check c)
        {
            StringBuilder s = new StringBuilder(), full = new StringBuilder();
            string file = FileOf(c.Clip);
            s.Append(c.Clip).Append(", take ").Append(_take).Append(", ×").Append(F(_speed)).Append(", ").Append(c.Frames.ToString("0")).Append(" frames, file ").Append(File.Exists(file) ? Hash(File.ReadAllText(file, Encoding.UTF8)) + " saved " + File.GetLastWriteTime(file).ToString("yyyy-MM-dd HH:mm:ss") : "not a file (built in)").Append('\n');
            PAnimClip shown = BodyClips.Find(c.Clip), whole = BodyClips.Find(Whole(c.Clip));
            if (whole != null && whole.Say.Length > 0) s.Append("said: ").Append(whole.Say).Append('\n');
            s.Append(c.Said);
            s.Append("pose: ").Append(c.Body.Numbers()).Append('\n');
            s.Append(c.Body.TempoSaid());
            string keyHead = c.Views.Length > 0 ? "key frames: studio/out/stage_" + c.Clip + "_<view>.png, views " + c.Views + ", 4 a row\n" : "key frames (words only; sheets of the whole run on request: raven stage check <views>, node tools/anim.js clip " + c.Clip + " <views>; views the clip would take: " + ((shown ?? whole) != null ? AutoViews(shown ?? whole) : "-") + "):\n";
            s.Append(keyHead);
            foreach (string shot in c.Shots) { full.Append("  ").Append(shot).Append('\n'); s.Append("  ").Append(Short(shot)).Append('\n'); }
            if (!c.KeyDone) { s.Append("key sheet: coming\n"); full.Append("key sheet: coming\n"); }
            else
            {
                string head = "key sheet: studio/out/stage_" + c.Clip + "_key.png — a row a moment, its two views, labelled" + (c.Other != null ? "; stage_" + c.Clip + "_diff.png — the same, this take | " + c.Other : "") + "\n";
                s.Append(head); full.Append(head);
                foreach (string row in c.KeyRows) { full.Append("  ").Append(row).Append('\n'); s.Append("  ").Append(Short(row)).Append('\n'); }
            }
            StoryPropView view = _dummy != null ? _dummy.GetComponent<StoryPropView>() : null;
            if (view != null && _sizeOf) s.Append("prop size ").Append(P(view.Size)).Append(" m\n");
            if (shown != null && shown.Parts.Count > 0) { s.Append("parts:"); foreach (PAnimPart pt in shown.Parts) s.Append(' ').Append(pt.Name).Append(' ').Append(F(pt.From)).Append('-').Append(F(pt.To)); s.Append('\n'); }
            s.Append("the rest (layers of each frame, the fastest bones, rows every 0.25 s): stage_").Append(c.Clip).Append(".full.txt\n");
            string dir = Path.Combine(Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "studio"), "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "stage_" + c.Clip + ".txt"), s.ToString(), new UTF8Encoding(false));
            // the full one: the short one's head to the key frames, then every frame with its layers, the fastest, the rows
            int cut = s.ToString().IndexOf("key frames", StringComparison.Ordinal);
            StringBuilder f = new StringBuilder(cut > 0 ? s.ToString().Substring(0, cut) : s.ToString());
            f.Append(keyHead).Append(full);
            f.Append("fastest:\n").Append(c.Body.Fastest());
            f.Append("\nevery 0.25 s (m, body frame: x right, y up, z ahead; lean forward,right and head up,right in degrees):\n").Append(c.Rows);
            File.WriteAllText(Path.Combine(dir, "stage_" + c.Clip + ".full.txt"), f.ToString(), new UTF8Encoding(false));
            Say("заміряно");
        }

        // a key frame's line without its «layers:» (the full report keeps it)
        private static string Short(string shot) { int i = shot.IndexOf("\n  layers:", StringComparison.Ordinal); return i >= 0 ? shot.Substring(0, i) : shot; }

        // the clip file's version, as tools/anim.js reckons it too: SHA-1 of its text with no «\r» and no byte-order mark, the first 8 hex digits
        internal static string Hash(string text)
        {
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            byte[] b = Encoding.UTF8.GetBytes(text.Replace("\r", ""));
            using (System.Security.Cryptography.SHA1 sha = System.Security.Cryptography.SHA1.Create())
            {
                byte[] h = sha.ComputeHash(b);
                StringBuilder s = new StringBuilder();
                for (int i = 0; i < 4; i++) s.Append(h[i].ToString("x2"));
                return s.ToString();
            }
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
