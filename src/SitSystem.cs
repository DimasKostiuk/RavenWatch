#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Система сидіння» (the sitting system): how a raven sits, wherever it sits, as the flight system is how it flies.
    // While the bird sits, the game's animator is off and the game's own raven clips (sitting, preening, talking) are
    // played here by hand, on the bird's own clock (RavenSkeleton). Each frame the skeleton starts clean (every bone the
    // clips leave alone back at the raven's own rest), the clips are laid down, then the layers go on in one fixed order
    // (ShoulderLife.Apply): the body on the seat (SitBody), the blend out of a landing, the legs, the posture, the look,
    // the wings, the small motion, a sitting template's pose. What plays when is the sitting director's (SitDirector),
    // the same on every game.
    internal static class SitSystem
    {
        internal static float FadeIn = 0.35f, FadeOut = 0.45f, TalkFadeIn = 0.15f; // s an action comes in and goes out over the sitting
        internal static float FarMetres = 50f;                            // another player's raven farther than this: its clips every third frame

        // ---------------------------------------------------------------- saved tuning (config/RavenWatch/sit.txt, tab «Сидіння»)
        internal static string FilePath { get { return System.IO.Path.Combine(System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "sit.txt"); } }
        private static readonly System.Reflection.FieldInfo[] StyleFields = typeof(SitBodyStyle).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        private static readonly SitBodyStyle HuginBuilt = SitBody.HuginStyle.Copy(), MuninBuilt = SitBody.MuninStyle.Copy();
        private static readonly float[] HChanceBuilt = (float[])SitDirector.HuginChance.Clone(), MChanceBuilt = (float[])SitDirector.MuninChance.Clone(), CooldownBuilt = (float[])SitDirector.Cooldown.Clone();
        private static readonly float[] GlobalsBuilt = Globals();

        // the loose numbers, in one order (the file's keys below)
        private static readonly string[] GlobalNames = { "trustgrip", "maxaccel", "windfrom", "windfull", "habitin", "habitout", "habitgrip", "windwings", "fadein", "fadeout", "talkfadein", "doing.h", "doing.m", "curious", "far" };
        private static float[] Globals()
        {
            return new[] { SitBody.TrustGrip, SitBody.MaxAccel, SitBody.WindFrom, SitBody.WindFull, SitBody.HabitIn, SitBody.HabitOut, SitBody.HabitGrip, SitBody.WindWings,
                FadeIn, FadeOut, TalkFadeIn, SitDirector.DoingEveryHugin, SitDirector.DoingEveryMunin, SitDirector.WorkCuriousSeconds, FarMetres };
        }
        private static void SetGlobals(float[] v)
        {
            SitBody.TrustGrip = v[0]; SitBody.MaxAccel = v[1]; SitBody.WindFrom = v[2]; SitBody.WindFull = v[3]; SitBody.HabitIn = v[4]; SitBody.HabitOut = v[5]; SitBody.HabitGrip = v[6]; SitBody.WindWings = v[7];
            FadeIn = v[8]; FadeOut = v[9]; TalkFadeIn = v[10]; SitDirector.DoingEveryHugin = v[11]; SitDirector.DoingEveryMunin = v[12]; SitDirector.WorkCuriousSeconds = v[13]; FarMetres = v[14];
        }

        internal static void ToBuilt()
        {
            foreach (System.Reflection.FieldInfo fi in StyleFields) { fi.SetValue(SitBody.HuginStyle, fi.GetValue(HuginBuilt)); fi.SetValue(SitBody.MuninStyle, fi.GetValue(MuninBuilt)); }
            System.Array.Copy(HChanceBuilt, SitDirector.HuginChance, HChanceBuilt.Length);
            System.Array.Copy(MChanceBuilt, SitDirector.MuninChance, MChanceBuilt.Length);
            System.Array.Copy(CooldownBuilt, SitDirector.Cooldown, CooldownBuilt.Length);
            SetGlobals(GlobalsBuilt);
        }

        internal static void Save()
        {
            System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
            List<string> lines = new List<string> { "# RavenWatch: how the ravens sit (test window, tab «Сидіння»). Delete the file for the built-in values." };
            foreach (bool m in new[] { false, true })
            {
                SitBodyStyle st = SitBody.StyleOf(m);
                foreach (System.Reflection.FieldInfo fi in StyleFields) lines.Add((m ? "munin." : "hugin.") + fi.Name.ToLowerInvariant() + "=" + ((float)fi.GetValue(st)).ToString("0.####", c));
                float[] ch = m ? SitDirector.MuninChance : SitDirector.HuginChance;
                for (int i = 1; i < ch.Length; i++) lines.Add((m ? "munin." : "hugin.") + "chance" + i + "=" + ch[i].ToString("0.###", c));
            }
            for (int i = 1; i < SitDirector.Cooldown.Length; i++) lines.Add("cooldown" + i + "=" + SitDirector.Cooldown[i].ToString("0.###", c));
            float[] g = Globals();
            for (int i = 0; i < g.Length; i++) lines.Add(GlobalNames[i] + "=" + g[i].ToString("0.####", c));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath));
            System.IO.File.WriteAllLines(FilePath, lines.ToArray());
            if (Plugin.Instance != null) Plugin.Instance.Log("Raven sitting tuning saved: " + FilePath);
        }

        internal static void Load()
        {
            try
            {
                if (!System.IO.File.Exists(FilePath)) return;
                float[] g = Globals();
                foreach (string raw in System.IO.File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    float v;
                    if (!float.TryParse(line.Substring(eq + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) continue;
                    bool m = key.StartsWith("munin."), h = key.StartsWith("hugin.");
                    string k = m || h ? key.Substring(6) : key;
                    int n;
                    if ((m || h) && k.StartsWith("chance") && int.TryParse(k.Substring(6), out n)) { float[] ch = m ? SitDirector.MuninChance : SitDirector.HuginChance; if (n > 0 && n < ch.Length) ch[n] = v; continue; }
                    if (m || h) { foreach (System.Reflection.FieldInfo fi in StyleFields) if (fi.Name.ToLowerInvariant() == k) fi.SetValue(SitBody.StyleOf(m), v); continue; }
                    if (k.StartsWith("cooldown") && int.TryParse(k.Substring(8), out n)) { if (n > 0 && n < SitDirector.Cooldown.Length) SitDirector.Cooldown[n] = v; continue; }
                    int gi = System.Array.IndexOf(GlobalNames, k);
                    if (gi >= 0) g[gi] = v;
                }
                SetGlobals(g);
                if (Plugin.Instance != null) Plugin.Instance.Log("Raven sitting tuning loaded from sit.txt");
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven sitting tuning unreadable: " + e.Message); }
        }

        internal static string Report(Player p)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(Lang.T("Система сидіння:"));
            foreach (bool munin in new[] { false, true })
            {
                ShoulderLife l = Shoulder.LifeOf(p, munin);
                sb.Append("\n  ").Append(munin ? Lang.T("Мунін: ") : Lang.T("Хугін: ")).Append(l != null ? l.SitInfo : Lang.T("не на плечі"));
            }
            return sb.ToString();
        }
    }

    internal enum SitClip { Idle = 0, Idle2 = 1, Idle3 = 2, Idle4 = 3, Talk = 4, TurnLeft = 5, TurnRight = 6 }

    // the game's raven clips by kind, found once for each animator controller (by their names)
    internal static class SitClips
    {
        internal const int Count = 7;
        private static readonly Dictionary<RuntimeAnimatorController, AnimationClip[]> Found = new Dictionary<RuntimeAnimatorController, AnimationClip[]>();

        internal static AnimationClip[] Of(Animator a)
        {
            if (a == null || a.runtimeAnimatorController == null) return null;
            RuntimeAnimatorController rc = a.runtimeAnimatorController;
            AnimationClip[] c;
            if (Found.TryGetValue(rc, out c)) return c;
            c = new AnimationClip[Count];
            foreach (AnimationClip clip in rc.animationClips)
            {
                if (clip == null) continue;
                string n = clip.name.ToLowerInvariant();
                int k = n.EndsWith("idle2") ? 1 : n.EndsWith("idle3") ? 2 : n.EndsWith("idle4") ? 3 : n.EndsWith("idle") ? 0
                    : n.Contains("talk") ? 4 : n == "turnleft" ? 5 : n == "turnright" ? 6 : -1;
                if (k >= 0 && c[k] == null) c[k] = clip;
            }
            Found[rc] = c;
            if (Plugin.Instance != null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder("Sitting clips:");
                for (int i = 0; i < Count; i++) sb.Append(' ').Append((SitClip)i).Append('=').Append(c[i] != null ? c[i].name + " " + c[i].length.ToString("0.00") + " s" : "none");
                Plugin.Instance.Log(sb.ToString());
            }
            return c;
        }

        // the sitting loop is there: the system can run this bird
        internal static bool Ready(Animator a) { AnimationClip[] c = Of(a); return c != null && c[0] != null; }
    }

    // «Скелет ворона»: the one place that lays a raven's clips on its bones, sitting or flying. Each frame the skeleton
    // starts clean (every bone the clips leave alone back at the raven's own rest), then its base is laid down: the
    // sitting (the loop on its own clock, an action such as preening or talking over it, each with its fade), the flight
    // (the fly clip's loop, ShoulderLife.FlyBase) or the landing (the game's landing clip, ShoulderLife.LandBase). A new
    // base eases in from the last over a moment (taking off, the elbow and the hand unfold after the shoulder); a change
    // in the middle of one eases from the very pose it had. The layers go on after it (ShoulderLife.Apply).
    internal sealed class RavenSkeleton
    {
        private sealed class Slot
        {
            public SitClip Kind;
            public AnimationClip Clip;
            public float Start, Shown, In, Cut = -1f; // its start (the clip's time 0), when it began to show, its fade-in, on the skeleton's clock; when it was cut short
        }

        private readonly Animator _anim;
        private readonly GameObject _go;
        private readonly Transform _root;
        private readonly AnimationClip[] _clips;
        private readonly Transform[] _bones;
        private readonly bool[] _hasRest;
        private readonly Quaternion[] _restRot, _accRot, _snapRot;
        private readonly Vector3[] _restPos, _accPos, _restScale, _snapPos;
        private readonly float[] _delay;
        private readonly List<Renderer> _skins = new List<Renderer>();
        private Slot _cur, _prev;
        private float _clock, _idlePhase;
        internal bool Active;

        // the base now and the one it eases out of (null: the pose it had, held)
        private System.Action _base, _from;
        private readonly System.Action _sitD; // its sitting base (kept: asked many times a frame)
        private bool _fromSnap;
        private float _fadeAt = -1f, _fadeLen;

        internal RavenSkeleton(Animator anim, bool munin)
        {
            _anim = anim; _go = anim.gameObject; _root = anim.transform;
            _clips = SitClips.Of(anim);
            List<Transform> list = new List<Transform>();
            foreach (Transform t in anim.GetComponentsInChildren<Transform>(true)) if (t != _root) list.Add(t);
            _bones = list.ToArray();
            int n = _bones.Length;
            _hasRest = new bool[n]; _restRot = new Quaternion[n]; _accRot = new Quaternion[n]; _snapRot = new Quaternion[n];
            _restPos = new Vector3[n]; _accPos = new Vector3[n]; _restScale = new Vector3[n]; _snapPos = new Vector3[n];
            _delay = new float[n];
            // the rest pose from the game's own raven (a copy here may already be bent by a perch or a flight)
            Dictionary<string, Transform> rest = new Dictionary<string, Transform>();
            Raven src = Shoulder.Template(munin);
            Animator srcAnim = src != null && src.m_visual != null ? src.m_visual.GetComponentInChildren<Animator>(true) : null;
            if (srcAnim != null) foreach (Transform t in srcAnim.GetComponentsInChildren<Transform>(true)) if (t != srcAnim.transform && !rest.ContainsKey(t.name)) rest.Add(t.name, t);
            for (int i = 0; i < n; i++)
            {
                Transform r;
                if (!rest.TryGetValue(_bones[i].name, out r)) continue;
                _hasRest[i] = true; _restRot[i] = r.localRotation; _restPos[i] = r.localPosition;
                _restScale[i] = r.localScale.sqrMagnitude > 1e-6f ? r.localScale : Vector3.one;
            }
            foreach (Renderer r in anim.GetComponentsInChildren<SkinnedMeshRenderer>(true)) _skins.Add(r);
            AnimationClip idle = _clips[(int)SitClip.Idle];
            _idlePhase = idle != null ? Random.value * idle.length : 0f;
            _sitD = SitBase;
            _base = _sitD;
        }

        internal bool For(Animator a) { return a == _anim; }
        internal bool Has(SitClip k) { return _clips[(int)k] != null; }
        internal float Length(SitClip k) { AnimationClip c = _clips[(int)k]; return c != null ? c.length : 0f; }

        // seen by some camera (a raven nobody sees is not posed, as the game's animator did not pose it either)
        internal bool Visible
        {
            get
            {
                for (int i = 0; i < _skins.Count; i++) if (_skins[i] != null && _skins[i].isVisible) return true;
                return _skins.Count == 0;
            }
        }

        // the skeleton is ours from now (the game's animator stops)
        internal void Begin()
        {
            if (!Active)
                for (int i = 0; i < _bones.Length; i++) // a copy left squashed by the game's "away" state: opened up, once
                {
                    Transform b = _bones[i];
                    if (b != null && _hasRest[i] && b.localScale.sqrMagnitude < 1e-6f) b.localScale = _restScale[i];
                }
            Active = true;
            if (_anim.enabled) _anim.enabled = false;
        }

        // ---------------------------------------------------------------- the base and its changes
        // how far the change to the base now has come (-1: none going on)
        internal float FadeW { get { return _fadeAt < 0f ? -1f : Mathf.Clamp01((Time.time - _fadeAt) / Mathf.Max(0.01f, _fadeLen)); } }
        internal bool Sitting { get { return _base == _sitD; } }
        internal bool BaseIs(System.Action a) { return _base == a; }

        // A new base (null: the sitting), eased in over 'blend' s from the pose the bird has; 'unfold': the elbow that
        // much after the shoulder, the hand twice that (a wing opening from the body out)
        internal void SetBase(System.Action sample, float blend, float unfold = 0f)
        {
            if (sample == null) sample = _sitD;
            bool fading = _fadeAt >= 0f && FadeW < 1f;
            if (blend > 0f)
            {
                if (fading || _base == null || !Active) { Snap(); _from = null; _fromSnap = true; } // mid-change, or the game's animator had it: from the very pose it shows
                else { _from = _base; _fromSnap = false; }
                _fadeAt = Time.time; _fadeLen = blend;
                Unfold(unfold);
            }
            else { _fadeAt = -1f; _from = null; _fromSnap = false; }
            _base = sample;
        }

        // from the pose it has now, eased into whatever comes next (the game's animator handing over, a pose held by hand)
        internal void EaseFromNow(float seconds)
        {
            if (seconds <= 0f) return;
            Snap(); _from = null; _fromSnap = true;
            _fadeAt = Time.time; _fadeLen = seconds;
            Unfold(0f);
        }

        // the change going on lasts this long in all (the take-off's crouch holds it, then the wings open)
        internal void Stretch(float seconds, float unfold)
        {
            if (_fadeAt < 0f) return;
            _fadeLen = Mathf.Max(0.01f, Time.time - _fadeAt + seconds);
            Unfold(unfold);
        }

        private void Unfold(float d)
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                Transform b = _bones[i];
                string n = b != null ? b.name.ToLowerInvariant() : "";
                _delay[i] = d <= 0f ? 0f : n.StartsWith("wing2") ? d : n.StartsWith("wing3") ? d * 2f : 0f;
            }
        }

        private void Snap()
        {
            for (int i = 0; i < _bones.Length; i++) { Transform b = _bones[i]; if (b != null) { _snapRot[i] = b.localRotation; _snapPos[i] = b.localPosition; } }
        }

        // One frame of the skeleton: clean, the base (eased from the last), the bird's own place and turn kept.
        internal void Frame()
        {
            Vector3 lp = _root.localPosition; Quaternion lr = _root.localRotation; Vector3 ls = _root.localScale;
            float u = FadeW;
            if (u >= 1f) { _fadeAt = -1f; _from = null; _fromSnap = false; u = -1f; }
            Rest();
            if (u >= 0f)
            {
                if (!_fromSnap) { if (_from != null) _from(); Snap(); Rest(); } // the last base, live (its pose kept apart: the sitting mixes in its own buffer)
                if (_base != null) _base();
                float el = Time.time - _fadeAt;
                for (int i = 0; i < _bones.Length; i++)
                {
                    Transform b = _bones[i];
                    if (b == null) continue;
                    float x = Mathf.Clamp01((el - _delay[i]) / Mathf.Max(0.01f, _fadeLen - _delay[i]));
                    float w = x * x * (3f - 2f * x);
                    b.localRotation = Quaternion.Slerp(_snapRot[i], b.localRotation, w);
                    b.localPosition = Vector3.Lerp(_snapPos[i], b.localPosition, w);
                }
            }
            else if (_base != null) _base();
            _root.localPosition = lp; _root.localRotation = lr; _root.localScale = ls;
        }

        private void Rest()
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                Transform b = _bones[i];
                if (b == null || !_hasRest[i]) continue;
                b.localRotation = _restRot[i]; b.localPosition = _restPos[i];
            }
        }

        // ---------------------------------------------------------------- the sitting base: the loop and its actions
        // an action over the sitting (preening, talking): from now, eased in; the one playing fades out under it.
        // Returns its length (0: no such clip).
        internal float Play(SitClip kind, float fadeIn) { return Play(kind, fadeIn, 0f); }

        // … already 'age' s into it (picked up midway: the same moment as on the deciding game)
        internal float Play(SitClip kind, float fadeIn, float age)
        {
            AnimationClip c = _clips[(int)kind];
            if (c == null) return 0f;
            if (_cur != null && Weight(_cur) > 0.01f) { _cur.Cut = _clock; _prev = _cur; }
            _cur = new Slot { Kind = kind, Clip = c, Start = _clock - Mathf.Max(0f, age), Shown = _clock, In = Mathf.Max(0.01f, fadeIn) }; // (late: its own moment, eased in from now)
            return c.length;
        }

        // an action is on (not the plain sitting)
        internal bool Busy { get { return _cur != null && Weight(_cur) > 0.01f; } }
        internal string Now { get { return _cur != null && Weight(_cur) > 0.01f ? _cur.Kind.ToString() : "Idle"; } }

        // how much of a slot shows now: in over its fade-in, out over the last FadeOut s of the clip or after a cut
        private float Weight(Slot s)
        {
            float t = _clock - s.Start, len = s.Clip.length;
            if (t < 0f || t >= len) return 0f;
            float w = Smooth((_clock - s.Shown) / s.In) * Smooth((len - t) / SitSystem.FadeOut);
            if (s.Cut >= 0f) w *= 1f - Smooth((_clock - s.Cut) / SitSystem.FadeOut);
            return w;
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // One frame sitting: the clock on by dt × rate (0 held still, 0.5 drowsy); 'pose': lay the skeleton down this
        // frame (not for a raven nobody sees)
        internal void Step(float dt, float rate, bool pose)
        {
            _clock += dt * rate;
            if (_prev != null && Weight(_prev) <= 0f && _clock - _prev.Start > 0f) _prev = null;
            if (_cur != null && _clock - _cur.Start >= _cur.Clip.length) _cur = null;
            if (pose) Frame();
        }

        // the sitting laid down: the loop, its actions mixed over it
        private void SitBase()
        {
            float wp = _prev != null ? Weight(_prev) : 0f, wc = _cur != null ? Weight(_cur) : 0f;
            AnimationClip idle = _clips[(int)SitClip.Idle];
            if (wc >= 0.999f && wp <= 0f) { Sample(_cur); return; }
            idle.SampleAnimation(_go, Mathf.Repeat(_clock + _idlePhase, idle.length));
            if (wp <= 0f && wc <= 0f) return;
            Capture();
            if (wp > 0f) { Sample(_prev); Mix(wp); }
            if (wc > 0f) { Sample(_cur); Mix(wc); }
            Write();
        }

        private void Sample(Slot s) { s.Clip.SampleAnimation(_go, Mathf.Clamp(_clock - s.Start, 0f, s.Clip.length)); }

        private void Capture()
        {
            for (int i = 0; i < _bones.Length; i++) { Transform b = _bones[i]; if (b != null) { _accRot[i] = b.localRotation; _accPos[i] = b.localPosition; } }
        }

        private void Mix(float w)
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                Transform b = _bones[i];
                if (b == null) continue;
                _accRot[i] = Quaternion.Slerp(_accRot[i], b.localRotation, w);
                _accPos[i] = Vector3.Lerp(_accPos[i], b.localPosition, w);
            }
        }

        private void Write()
        {
            for (int i = 0; i < _bones.Length; i++) { Transform b = _bones[i]; if (b != null) { b.localRotation = _accRot[i]; b.localPosition = _accPos[i]; } }
        }
    }

    // What is around another player's ravens (the boss, the nearest enemy), looked at once for both of them; your own
    // ravens read the same from Senses.
    internal static class SitSense
    {
        private sealed class Seen { public float At = -10f; public Character Boss, Enemy; }
        private static readonly Dictionary<Player, Seen> All = new Dictionary<Player, Seen>();
        private static float _prune;
        internal static float Every = 0.4f;

        internal static void Around(Player p, out Character boss, out Character enemy)
        {
            boss = null; enemy = null;
            if (p == null) return;
            if (Time.time >= _prune) { _prune = Time.time + 30f; Prune(); }
            Seen s;
            if (!All.TryGetValue(p, out s)) { s = new Seen(); All[p] = s; }
            if (Time.time - s.At >= Every)
            {
                s.At = Time.time; s.Boss = null; s.Enemy = null;
                Vector3 me = p.transform.position;
                float enemyDist = ShoulderLife.LookRange;
                foreach (Character c in Character.GetAllCharacters())
                {
                    if (c == null || c == p || c.IsDead()) continue;
                    float d = Vector3.Distance(c.transform.position, me);
                    if (c.IsBoss()) { if (d <= ShoulderLife.BossRange && s.Boss == null) s.Boss = c; continue; }
                    if (d < enemyDist && !c.IsPlayer() && BaseAI.IsEnemy(p, c)) { s.Enemy = c; enemyDist = d; }
                }
            }
            boss = s.Boss != null && !s.Boss.IsDead() ? s.Boss : null;
            enemy = s.Enemy != null && !s.Enemy.IsDead() ? s.Enemy : null;
        }

        private static void Prune()
        {
            List<Player> gone = null;
            foreach (Player p in All.Keys) if (p == null) { if (gone == null) gone = new List<Player>(); gone.Add(p); }
            if (gone != null) foreach (Player p in gone) All.Remove(p);
        }
    }

    // How one raven holds itself on a moving seat (SitBody): its own spring and grip, by its kind and its trust
    internal sealed class SitBodyStyle
    {
        public float Hz, Damp;         // the lean: how quick (1/s) and how damped (1 = no swing back, below: it sways)
        public float Gain;             // ° of lean for each m/s² the seat speeds up, slows or swings sideways
        public float MaxTilt;          // ° it never leans past
        public float DipHz, DipDamp, DipGain, DipMax; // pressed down by a push up (a jump, a landing): m per m/s², at most
        public float YawHz, YawDamp;   // turning after you: how quick, how damped (below 1 it swings a touch past)
        public float Grip;             // ° of lean it holds by its feet alone; more and the wings help
        public float JoltShare;        // its body thrown this far (share of its lean or dip at most): it beats its wings to keep its seat
        public float WindLean;         // ° forward at a full run (into the wind)
        internal SitBodyStyle Copy() { return (SitBodyStyle)MemberwiseClone(); }
    }

    // «Тіло на плечі»: the raven as a body that grips a moving seat, as the flight body is the raven in the air. The
    // seat's own motion (you start, stop, turn, jump, land; any player's, from how they move on every game, nothing
    // sent) leans it back and forth and sideways on a spring and presses it down; it turns after you with its own
    // lag; what its feet cannot hold, its wings take (Balance), and a hard knock makes it beat them (Jolt). Hugin
    // stiff and calm, Munin quicker and swaying more; a young raven grips worse.
    internal sealed class SitBody
    {
        internal static readonly SitBodyStyle HuginStyle = new SitBodyStyle
        {
            Hz = 2.6f, Damp = 0.8f, Gain = 1.5f, MaxTilt = 24f, DipHz = 4.5f, DipDamp = 0.7f, DipGain = 0.0016f, DipMax = 0.045f,
            YawHz = 2.2f, YawDamp = 0.95f, Grip = 9f, JoltShare = 0.8f, WindLean = 10f
        };
        internal static readonly SitBodyStyle MuninStyle = new SitBodyStyle
        {
            Hz = 3.6f, Damp = 0.55f, Gain = 1.9f, MaxTilt = 28f, DipHz = 5.5f, DipDamp = 0.5f, DipGain = 0.002f, DipMax = 0.05f,
            YawHz = 3.4f, YawDamp = 0.72f, Grip = 7f, JoltShare = 0.7f, WindLean = 14f
        };
        internal static float TrustGrip = 0.65f;   // share of its grip a trust-1 raven has (all of it by trust 7)
        internal static float MaxAccel = 45f;      // m/s² read at most (a portal, a hitch: not a blow)
        internal static float WindFrom = 2.8f, WindFull = 9f; // m/s: the run's lean into the wind starts / is full
        // Used to it: on an even run it settles in within HabitIn s (a firmer grip, wings in, only a real knock makes it
        // beat them); stopped, that wears off within HabitOut s
        internal static float HabitIn = 3f, HabitOut = 2f, HabitGrip = 0.5f, WindWings = 0.25f;

        internal float Pitch, Roll, Dip, Yaw;      // ° forward / ° to its left / m down / ° its facing
        internal float PitchVel, RollVel, YawVel;
        internal float Balance;                    // 0..1 how much its wings help it keep its seat now
        internal float Used;                       // 0..1 how used it is to the run now
        private float _throwWas;
        private readonly Queue<float> _jolts = new Queue<float>();
        internal int JoltsPerMinute { get { while (_jolts.Count > 0 && Time.time - _jolts.Peek() > 60f) _jolts.Dequeue(); return _jolts.Count; } }
        internal bool Live { get { return Time.frameCount - _frame <= 3; } } // stepped a moment ago (sitting)
        private float _dipVel;
        private Vector3 _pos, _vel, _acc;
        private int _frame = -10;
        private float _joltAt = -10f, _jolt;

        internal static SitBodyStyle StyleOf(bool munin) { return munin ? MuninStyle : HuginStyle; }

        // a hard knock this frame (0: none), read once: how hard, 0..1
        internal float TakeJolt() { float j = _jolt; _jolt = 0f; return j; }

        // One frame: the seat's owner at 'pos' facing 'yaw' (°). 'asleep': no wings, a looser grip.
        internal void Step(Vector3 pos, float yaw, float dt, bool munin, int level, bool asleep)
        {
            if (dt <= 0f) return;
            SitBodyStyle s = StyleOf(munin);
            bool fresh = Time.frameCount - _frame > 3;
            _frame = Time.frameCount;
            if (fresh || (pos - _pos).sqrMagnitude > 25f) { _pos = pos; _vel = Vector3.zero; _acc = Vector3.zero; Yaw = yaw; YawVel = 0f; if (fresh) { Pitch = Roll = Dip = 0f; PitchVel = RollVel = _dipVel = 0f; } return; } // (just sat down, or a portal)
            // the seat's own motion, smoothed (another player's comes in steps over the network)
            Vector3 v = (pos - _pos) / dt;
            _pos = pos;
            Vector3 vs = Vector3.Lerp(_vel, v, 1f - Mathf.Exp(-dt * 18f));
            Vector3 a = Vector3.ClampMagnitude((vs - _vel) / dt, MaxAccel);
            _vel = vs;
            _acc = Vector3.Lerp(_acc, a, 1f - Mathf.Exp(-dt * 14f));
            Quaternion face = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fwd = face * Vector3.forward, right = face * Vector3.right;
            float aF = Vector3.Dot(_acc, fwd), aR = Vector3.Dot(_acc, right), aU = _acc.y;
            float hs = new Vector3(_vel.x, 0f, _vel.z).magnitude;
            float trust = Mathf.Clamp01((level - 1) / 6f);
            Used = Mathf.MoveTowards(Used, hs > 2f ? 1f : 0f, dt / (hs > 2f ? HabitIn : HabitOut));
            float grip = s.Grip * Mathf.Lerp(TrustGrip, 1f, trust) * (asleep ? 0.6f : 1f) * (1f + HabitGrip * Used);
            float damp = s.Damp * Mathf.Lerp(0.85f, 1f, trust);
            // what the seat does to it: speeding up leans it back, slowing forward, a swing to the side the other way;
            // pushed up (a jump, a landing) it is pressed down and dips its head a little; running, it leans into the wind
            float wind = Mathf.Clamp01((hs - WindFrom) / Mathf.Max(0.1f, WindFull - WindFrom)) * s.WindLean;
            float pitchWant = Mathf.Clamp(-aF * s.Gain + Mathf.Max(0f, aU) * s.Gain * 0.35f + wind, -s.MaxTilt, s.MaxTilt);
            float rollWant = Mathf.Clamp(aR * s.Gain, -s.MaxTilt, s.MaxTilt);
            float dipWant = Mathf.Clamp(-aU * s.DipGain, -s.DipMax, s.DipMax * 0.4f);
            // the springs, in small steps (a long frame must not throw them)
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / 0.01f), 1, 8);
            float h = dt / n;
            float w = 2f * Mathf.PI * s.Hz, wd = 2f * Mathf.PI * s.DipHz, wy = 2f * Mathf.PI * s.YawHz;
            for (int i = 0; i < n; i++)
            {
                PitchVel += (w * w * (pitchWant - Pitch) - 2f * damp * w * PitchVel) * h; Pitch += PitchVel * h;
                RollVel += (w * w * (rollWant - Roll) - 2f * damp * w * RollVel) * h; Roll += RollVel * h;
                _dipVel += (wd * wd * (dipWant - Dip) - 2f * s.DipDamp * wd * _dipVel) * h; Dip += _dipVel * h;
                float dy = Mathf.DeltaAngle(Yaw, yaw);
                YawVel += (wy * wy * dy - 2f * s.YawDamp * wy * YawVel) * h; Yaw += YawVel * h;
            }
            Pitch = Mathf.Clamp(Pitch, -s.MaxTilt, s.MaxTilt); Roll = Mathf.Clamp(Roll, -s.MaxTilt, s.MaxTilt);
            Dip = Mathf.Clamp(Dip, -s.DipMax, s.DipMax * 0.4f);
            if (Mathf.Abs(Mathf.DeltaAngle(Yaw, yaw)) > 120f) { Yaw = yaw - Mathf.Sign(Mathf.DeltaAngle(Yaw, yaw)) * 120f; } // never left facing away
            Yaw = Mathf.Repeat(Yaw + 180f, 360f) - 180f;
            // what the feet cannot hold, the wings take: the lean past its grip (the run's own lean into the wind is no
            // strain), and a little of the wind until it is used to the run
            float lean = Mathf.Sqrt((Pitch - wind) * (Pitch - wind) + Roll * Roll);
            float want = asleep ? 0f : Mathf.Clamp01((lean - grip) / Mathf.Max(1f, grip)) + Mathf.Clamp01((hs - WindFrom) / Mathf.Max(0.1f, WindFull - WindFrom)) * WindWings * (1f - 0.8f * Used);
            Balance = Mathf.MoveTowards(Balance, Mathf.Clamp01(want), dt * (want > Balance ? 4f : 1.5f));
            // thrown hard (a sudden stop, a landing from high, a blow): a few beats of the wings. What counts is how far its
            // body is really thrown on its springs, not how the seat jerks: the springs take the bumps of a run.
            float thrown = Mathf.Max(Mathf.Max(Mathf.Abs(Pitch - wind), Mathf.Abs(Roll)) / s.MaxTilt, -Dip / s.DipMax);
            float at = Mathf.Min(0.97f, s.JoltShare * Mathf.Lerp(0.88f, 1f, trust) * (1f + 0.25f * Used));
            if (!asleep && thrown > at && thrown > _throwWas && Time.time - _joltAt > 1.2f)
            {
                _joltAt = Time.time;
                _jolt = Mathf.Clamp01((thrown - at) / Mathf.Max(0.05f, 1f - at) + 0.4f);
                _jolts.Enqueue(Time.time);
            }
            _throwWas = thrown;
        }

        // a knock from outside (a blow on you): thrown back and up a little, eased by the springs
        internal void Kick(float pitchVel, float rollVel, float dipVel) { PitchVel += pitchVel; RollVel += rollVel; _dipVel += dipVel; }
    }
}
#endif
