#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Шаблон сидіння»: one thing a sitting raven does (a doing of its own, or a reaction to what you do), made in the
    // test window (tab «Сидіння») like the flight templates. What it is:
    //   when — the situations it answers (its own doing, a blow, a fall, a boss, a kill, your work, your meal, a find,
    //          your hand on it), the moods it fits, for whom, how often (a weight for each raven), its priority;
    //   how long — seconds (or the game clip's own length), the fade in and out, a rest before it is done again;
    //   the moment's things — a game clip under it (preening, talking, turning), a feather shake, a step aside, a few
    //          beats, a knock on its seat, a caw, where it looks (your face, your hands);
    //   the pose — at five points of its time (start, ¼, ½, ¾, end): the wings (both, mirrored), what the inner one
    //          (by your head) adds, the body, the neck, the head, the tail (the flight templates' axes), the beak, a crouch.
    // Each time it is played from a seed (its length, a little bigger or smaller, which side it tilts), the same on every
    // game. The built-in ones are the user's to change; files in config/RavenWatch/sit_templates override them by name.
    internal sealed class SitTemplate
    {
        internal const int Keys = 5, Bones = 12;
        internal static readonly string[] BoneNames = { "Крило: плече", "Крило: лікоть", "Крило: кисть", "Внутрішнє: плече", "Внутрішнє: лікоть", "Внутрішнє: кисть", "Тулуб", "Шия", "Голова", "Хвіст", "Дзьоб", "Присід" };
        public string Name = "Новий шаблон";
        public bool Builtin;
        public int Only;                                   // 0 both, 1 Hugin, 2 Munin
        public float WeightH = 1f, WeightM = 1f;
        public readonly bool[] Sits = new bool[SitTemplates.SitCount];
        public readonly bool[] Moods = { true, true, true, true }; // calm, alert, curious, drowsy
        public int Prio = 3;
        public float SecMin = 1.5f, SecMax = 2.5f, In = 0.2f, Out = 0.35f, Rest = 20f, Vary = 0.1f;
        public int Clip = -1;                              // -1 none, a SitClip, 7: one of the preening clips (by its seed)
        public int Look;                                   // 0 as it looks, 1 at your face, 2 at your hands, 3 at the other raven, 4 away from it
        public float Tremble, TrembleHz = 7f, Flap, KickPitch, KickRoll, KickDip, BeakHz;
        public float CawH, CawM, CawPitchH = 1.15f, CawPitchM = 0.9f, CawVolume = 0.45f;
        public bool Shake, Step, Mirror;
        // who it suits (Norov ids: characters ×2 as far as its trust shows them, quirks ×3), and whether it is only theirs
        public string[] Suits = new string[0];
        public bool Need;
        // the other raven answers it (a template by name), with this chance, a moment later
        public string Answer = "";
        public float AnswerChance = 0.6f, AnswerMin = 0.3f, AnswerMax = 0.8f;
        // on a perch: it leans this far over to the other raven (a nudge; m). A template with it is a perch's only.
        public float Reach;
        public readonly Vector3[,] Pose = new Vector3[Keys, Bones];

        internal int Id { get { return Name.GetStableHashCode() & 0x3FFF; } }
        internal bool For(bool munin) { return Only == 0 || (Only == 1 && !munin) || (Only == 2 && munin); }
        internal float WeightFor(bool munin) { return Mathf.Max(0f, munin ? WeightM : WeightH); }
        internal bool HasWings { get { for (int k = 0; k < Keys; k++) for (int b = 0; b < 6; b++) if (Pose[k, b] != Vector3.zero) return true; return Tremble > 0f; } }

        // the pose at u (0..1 of its time), smooth between the five keys
        internal Vector3 At(float u, int bone)
        {
            float x = Mathf.Clamp01(u) * (Keys - 1);
            int a = Mathf.Min((int)x, Keys - 2);
            float f = Mathf.Clamp01(x - a);
            f = f * f * (3f - 2f * f);
            return Vector3.Lerp(Pose[a, bone], Pose[a + 1, bone], f);
        }

        internal SitTemplate Copy(string name) { SitTemplate t = SitTemplates.FromText(SitTemplates.ToText(this)); t.Name = name; t.Builtin = false; return t; }
    }

    // One playing of a template: what its seed made of it (the same on every game)
    internal sealed class SitRun
    {
        public SitTemplate T;
        public int Seed, Sit, Clip = -1;
        public float Seconds, Amp = 1f, Sign = 1f, CawPitch;
        public bool Caw, Wings; // (Wings: its template moves the wings; worked out once, asked every frame)
        public readonly float[] Part = new float[SitTemplate.Bones]; // each part its own size this time (± its template's vary)
        public float Pace = 1f;     // >1: early in the move it gets on quicker, <1: slower (the keys' timing this time)
        public float Back;          // share of the wing's opening held back this time (towards the tail, never forward)
        internal static float BackMax = 0.35f;

        internal static SitRun Make(SitTemplate t, int seed, int sit, bool munin, ISitBird life)
        {
            System.Random r = new System.Random(seed * 131 + t.Id * 7 + (munin ? 1 : 0));
            SitRun x = new SitRun { T = t, Seed = seed, Sit = sit };
            x.Clip = t.Clip == 7 ? (int)SitClip.Idle2 + seed % 3 : t.Clip;
            float clipLen = x.Clip >= 0 && life != null ? life.BirdClipLength(x.Clip) : 0f;
            float k = (float)r.NextDouble();
            x.Seconds = t.SecMax > 0.01f ? Mathf.Lerp(t.SecMin, Mathf.Max(t.SecMin, t.SecMax), k) : clipLen;
            if (x.Seconds <= 0.05f) x.Seconds = clipLen > 0f ? clipLen : 1f;
            // its size: this time's touch (wider for a young raven), the raven's character (Norov), a young raven bigger
            int level = life != null ? life.BirdLevel : 7;
            float trust = Mathf.Clamp01((level - 1) / 6f);
            Norov.Character ch = life != null ? Norov.OnPlayer(life.BirdOwner, munin) : null;
            x.Amp = (1f + t.Vary * Mathf.Lerp(1.5f, 1f, trust) * ((float)r.NextDouble() * 2f - 1f)) * Mathf.Lerp(1.12f, 1f, trust) * (ch != null ? Norov.Mul(ch.SitAmp, Norov.Shows(level)) : 1f);
            x.Sign = t.Mirror && r.NextDouble() < 0.5 ? -1f : 1f;
            x.Caw = r.NextDouble() < (munin ? t.CawM : t.CawH);
            x.CawPitch = munin ? t.CawPitchM : t.CawPitchH;
            x.Wings = t.HasWings;
            float pv = t.Vary * Mathf.Lerp(1.5f, 1f, trust);
            for (int b = 0; b < SitTemplate.Bones; b++) x.Part[b] = 1f + pv * ((float)r.NextDouble() * 2f - 1f);
            x.Pace = Mathf.Pow(2f, pv * 2f * ((float)r.NextDouble() * 2f - 1f));
            x.Back = t.HasWings ? BackMax * (float)r.NextDouble() : 0f;
            return x;
        }

        // how much of it shows t s in, shown 'shown' s here (picked up late: eased in from then)
        internal float Weight(float t, float shown)
        {
            if (t < 0f || t >= Seconds) return 0f;
            return S(Mathf.Min(t, shown) / Mathf.Max(0.01f, T.In)) * S((Seconds - t) / Mathf.Max(0.01f, T.Out));
        }
        private static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // the pose of a bone t s in, this playing's size and side
        internal Vector3 Pose(float t, int bone)
        {
            float u = Mathf.Clamp01(t / Mathf.Max(0.01f, Seconds));
            Vector3 p = T.At(Mathf.Pow(u, 1f / Pace), bone) * (Amp * Part[bone]);
            if (bone >= 6 && bone <= 9) { p.y *= Sign; p.z *= Sign; } // the body's turns and tilts to this time's side
            // the wing a little back this time: only its opening shrinks (towards the back), it never sweeps forward of the
            // pose, never past folded; the shoulder all of it, the elbow half (the hand is bent back already)
            if (Back > 0f && p.x > 0f && bone <= 5 && bone % 3 != 2) p.x -= p.x * Back * (bone % 3 == 0 ? 1f : 0.5f);
            return p;
        }
    }

    // A sitting template's pose laid on a bird, on top of everything (the shoulder's ShoulderLife, the perch's PerchLife):
    // the wings (both mirrored, the inner one's extra, a tremble on the shoulders' lift), then the body, the neck, the head
    // and the tail, on the flight templates' axes. The beak and the crouch have their own places in each bird.
    internal sealed class SitPoser
    {
        private Transform[] _wl, _wr, _body;
        private readonly Vector3[] _addL = new Vector3[WingKeys.Bones], _addR = new Vector3[WingKeys.Bones], _bodyAdd = new Vector3[WingKeys.BodyBones];
        private static readonly int[] BodyRow = { 6, 8, 9, 7 }; // WingKeys' body index (body, head, tail, neck) → template row

        internal void Apply(Transform root, SitRun r, float t, float w, Quaternion facing, bool leftInner, bool wings)
        {
            if (r == null || w <= 0.001f || root == null) return;
            if (wings && r.Wings)
            {
                if (_wl == null) WingKeys.Find(root, out _wl, out _wr);
                float tr = r.T.Tremble > 0f ? Mathf.Sin(Time.time * r.T.TrembleHz * Mathf.PI * 2f) * r.T.Tremble : 0f;
                for (int i = 0; i < WingKeys.Bones; i++)
                {
                    Vector3 both = r.Pose(t, i), inner = r.Pose(t, 3 + i);
                    if (i == 0) both.y += tr;
                    _addL[i] = (both + (leftInner ? inner : Vector3.zero)) * w;
                    _addR[i] = (both + (leftInner ? Vector3.zero : inner)) * w;
                }
                WingKeys.ApplyAdds(_wl, _wr, facing, _addL, _addR);
            }
            if (_body == null) _body = WingKeys.FindBody(root);
            for (int i = 0; i < WingKeys.BodyBones; i++) _bodyAdd[i] = r.Pose(t, BodyRow[i]) * w;
            WingKeys.ApplyBody(_body, facing, _bodyAdd);
        }
    }

    internal static class SitTemplates
    {
        internal const int SitCount = 10;
        internal const string SettleName = "Вмощується"; // after a landing (never picked by itself: weight 0)
        internal const int Doing = 0, Hurt = 1, Fall = 2, Boss = 3, Kill = 4, Work = 5, Eat = 6, Loot = 7, Pet = 8, Pair = 9;
        internal static readonly string[] SitNames = { "Власна справа", "Удар по тобі", "Падіння", "Бос", "Вбивство", "Твоя робота", "Їжа", "Знахідка", "Погладили", "Пара" };
        internal static readonly string[] MoodNames = { "Спокійний", "Насторожений", "Цікавий", "Дрімотний" };
        internal static readonly string[] ClipNames = { "Немає", "Сидіння", "Чистить 1", "Чистить 2", "Чистить 3", "Говорить", "Поворот ліворуч", "Поворот праворуч", "Чистить (будь-яке)" }; // shown: -1..7
        internal static readonly string[] LookNames = { "Як дивиться", "На обличчя", "На руки", "На іншого ворона", "Від іншого" };

        // the first ones (2026-10-15), the user's to change in the tab «Сидіння»
        private static readonly string[] Builtin =
        {
            "name=Чистить пір'я\nonly=0\nweight.h=0.45\nweight.m=0.29\nsit=1,0,0,0,0,0,0,0,0\nmood=1,0,1,0\nprio=1\nsec=0,0\nfade=0.35,0.45\nrest=30\nvary=0\nclip=7\n",
            "name=Струшується\nonly=0\nweight.h=0.3\nweight.m=0.23\nsit=1,0,0,0,0,0,0,0,0\nmood=1,1,1,0\nprio=2\nsec=0.55,0.55\nfade=0.05,0.1\nrest=40\nvary=0\nshake=1\nk1=0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;-1,0,0;0,0,0;0,0,0;0,0,0\nk2=0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;-1,0,0;0,0,0;0,0,0;0,0,0\nk3=0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;-1,0,0;0,0,0;0,0,0;0,0,0\nk4=0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;-1,0,0;0,0,0;0,0,0;0,0,0\nk5=0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;-1,0,0;0,0,0;0,0,0;0,0,0\n",
            "name=Переступає\nonly=0\nweight.h=0.25\nweight.m=0.5\nsit=1,0,0,0,0,0,0,0,0\nmood=1,1,1,0\nprio=1\nsec=0.4,0.4\nfade=0.05,0.1\nrest=20\nvary=0\nstep=1\n",
            Waved("Злякався", 1, 1f, 0f, "0,1,1,0,0,0,0,0,0", 9, 1.5f, 2.1f, 0.2f, 0.7f, "tremble=1.2\nkick=-50,40,-0.25\nmirror=1\n",
                new[] { new[] { "18,14,0", "4,0,0", "-12,0,0", "0,0,0", "0,0,0", "0,0,0", "-2,0,0", "0,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "40,27,0", "20,6,0", "-8,0,0", "0,0,0", "0,0,0", "0,0,0", "-4.5,0,0", "0,0,0", "-11,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "38,25,0", "32,8,0", "6,0,0", "0,0,0", "0,0,0", "0,0,0", "-4.5,0,0", "0,0,0", "-11,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "28,17,0", "25,4,0", "10,0,0", "0,0,0", "0,0,0", "0,0,0", "-3,0,0", "0,0,0", "-8,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "14,8,0", "13,0,0", "5,0,0", "0,0,0", "0,0,0", "0,0,0", "-1,0,0", "0,0,0", "-4,0,0", "0,0,0", "0,0,0", "0,0,0" } }),
            Waved("Злякався (Мунін)", 2, 0f, 1f, "0,1,1,0,0,0,0,0,0", 9, 1.3f, 1.8f, 0.16f, 0.6f, "tremble=1.5\nkick=-50,40,-0.25\nmirror=1\n",
                new[] { new[] { "10,8,0", "2,0,0", "-10,0,0", "0,0,0", "0,0,0", "0,0,0", "-2,0,0", "0,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "23,17,0", "11,4,0", "-8,0,0", "0,0,0", "0,0,0", "0,0,0", "-4.5,0,0", "0,0,0", "-11,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "21,15,0", "17,5,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "-4.5,0,0", "0,0,0", "-11,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "15,10,0", "13,2,0", "4,0,0", "0,0,0", "0,0,0", "0,0,0", "-3,0,0", "0,0,0", "-8,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "8,4,0", "7,0,0", "2,0,0", "0,0,0", "0,0,0", "0,0,0", "-1,0,0", "0,0,0", "-4,0,0", "0,0,0", "0,0,0", "0,0,0" } }),
            Waved("Наїжачився", 0, 1f, 1f, "0,0,0,1,0,0,0,0,0", 8, 2.2f, 3f, 0.3f, 0.7f, "tremble=2.4\nbeakhz=0\nsuits=bold,watchful\n",
                new[] { new[] { "6,10,0", "2,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "2,0,0", "0,0,0", "4,0,0", "0,0,0" },
                       new[] { "14,22,0", "6,4,0", "-10,0,0", "0,0,0", "0,0,0", "0,0,0", "7,0,0", "0,0,0", "4,0,0", "0,0,0", "10,0,0", "0,0,0" },
                       new[] { "17,25,0", "8,4,0", "-10,0,0", "0,0,0", "0,0,0", "0,0,0", "8,0,0", "0,0,0", "4,0,0", "0,0,0", "11,0,0", "0,0,0" },
                       new[] { "16,23,0", "8,3,0", "-9,0,0", "0,0,0", "0,0,0", "0,0,0", "8,0,0", "0,0,0", "4,0,0", "0,0,0", "11,0,0", "0,0,0" },
                       new[] { "11,15,0", "5,2,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0", "6,0,0", "0,0,0", "3,0,0", "0,0,0", "8,0,0", "0,0,0" } }),
            Waved("Радіє здобичі", 0, 1f, 1f, "0,0,0,0,1,0,0,0,0", 6, 1.6f, 2.2f, 0.25f, 0.6f, "tremble=1.2\ncaw=0.25,0.5\ncawpitch=1.17,0.9\ncawvolume=0.45\nsuits=bold,fidget\nanswer=Дивиться на іншого\nanswer.chance=0.45\nanswer.delay=0.2,0.6\n",
                new[] { new[] { "20,16,0", "2,0,0", "-12,0,0", "0,0,0", "0,0,0", "0,0,0", "-2,0,0", "0,0,0", "-6,0,0", "0,0,0", "6,0,0", "0,0,0" },
                       new[] { "52,38,0", "28,6,0", "-4,0,0", "0,0,0", "0,0,0", "0,0,0", "-4.5,0,0", "0,0,0", "-12,0,0", "0,0,0", "12,0,0", "0,0,0" },
                       new[] { "64,45,0", "48,10,0", "18,0,0", "0,0,0", "0,0,0", "0,0,0", "-5.5,0,0", "0,0,0", "-14,0,0", "0,0,0", "14,0,0", "0,0,0" },
                       new[] { "60,41,0", "52,8,0", "28,0,0", "0,0,0", "0,0,0", "0,0,0", "-5.5,0,0", "0,0,0", "-14,0,0", "0,0,0", "12,0,0", "0,0,0" },
                       new[] { "42,28,0", "38,4,0", "18,0,0", "0,0,0", "0,0,0", "0,0,0", "-4,0,0", "0,0,0", "-10,0,0", "0,0,0", "6,0,0", "0,0,0" } }),
            Static("Дивиться на руки", 0, 1f, 1f, "0,0,0,0,0,1,0,0,0", 4, 2.5f, 4f, 0.3f, 0.5f, "look=2\nmirror=1\nsuits=watchful,devoted\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "3.5,0,0", "0,0,0", "7,0,15", "0,0,0", "0,0,0", "0,0,0" }),
            Static("Випрошує", 0, 1f, 0.5f, "0,0,0,0,0,0,1,0,0", 5, 2f, 3f, 0.25f, 0.4f, "look=1\nmirror=1\nbeakhz=4.5\nsuits=devoted,homebody\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "0,0,9", "0,0,0", "13,0,0", "0,0,0" }),
            Waved("Випрошує, тремтячи крилами", 2, 0f, 1f, "0,0,0,0,0,0,1,0,0", 5, 2f, 3f, 0.3f, 0.45f, "look=1\nmirror=1\nbeakhz=4.5\ntremble=3\n",
                new[] { new[] { "8,4,0", "2,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "0,0,9", "0,0,0", "13,0,0", "0,0,0" },
                       new[] { "14,6,0", "6,0,0", "-8,0,0", "0,0,0", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "0,0,9", "0,0,0", "13,0,0", "0,0,0" },
                       new[] { "15,6,0", "7,0,0", "-8,0,0", "0,0,0", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "0,0,9", "0,0,0", "13,0,0", "0,0,0" },
                       new[] { "14,6,0", "6,0,0", "-8,0,0", "0,0,0", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "0,0,9", "0,0,0", "13,0,0", "0,0,0" },
                       new[] { "9,4,0", "3,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "0,0,9", "0,0,0", "13,0,0", "0,0,0" } }),
            Waved("Розпушився", 0, 1f, 1f, "0,0,0,0,0,0,0,0,1", 5, 1.6f, 2.3f, 0.35f, 0.55f, "shake=1\nmirror=1\n",
                new[] { new[] { "4,4,0", "1,0,0", "-4,0,0", "0,0,0", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "6,0,10", "0,0,0", "0,0,0", "0.15,0,0" },
                       new[] { "9,8,0", "3,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0", "5.5,0,0", "0,0,0", "10,0,18", "0,0,0", "0,0,0", "0.3,0,0" },
                       new[] { "10,9,0", "4,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0", "5.5,0,0", "0,0,0", "10,0,18", "0,0,0", "0,0,0", "0.3,0,0" },
                       new[] { "8,7,0", "3,0,0", "-5,0,0", "0,0,0", "0,0,0", "0,0,0", "5.5,0,0", "0,0,0", "10,0,18", "0,0,0", "0,0,0", "0.3,0,0" },
                       new[] { "4,4,0", "1,0,0", "-3,0,0", "0,0,0", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "6,0,10", "0,0,0", "0,0,0", "0.15,0,0" } }),
            Static("Розглядає знахідку", 0, 1f, 1f, "0,0,0,0,0,0,0,1,0", 3, 1f, 1.6f, 0.15f, 0.35f, "look=2\nmirror=1\nsuits=wanderer,watchful,magpie,thief\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "11,0,23", "0,0,0", "0,0,0", "0,0,0" }),
            Static("Дрімає", 0, 1.5f, 1f, "1,0,0,0,0,0,0,0,0,0", 1, 4f, 7f, 1f, 1f, "mood=0,0,0,1\nsuits=sleepy,homebody,calm\nrest=30\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "6,0,0", "0,0,0", "18,0,0", "-6,0,0", "0,0,0", "0.3,0,0" }),
            Static("Каркає", 0, 0.12f, 0.15f, "1,0,0,0,0,0,0,0,0,0", 2, 0.7f, 1f, 0.08f, 0.3f, "mood=1,1,1,0\ncaw=1,1\ncawpitch=1.2,0.9\ncawvolume=0.4\nsuits=loud,grumbler,bold\nrest=40\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "-3,0,0", "0,0,0", "-10,0,0", "0,0,0", "20,0,0", "0,0,0" }),
            Static("Бурчить", 0, 0.1f, 0.12f, "1,0,0,0,0,0,0,0,0,0", 2, 2f, 3f, 0.3f, 0.5f, "mood=1,1,1,0\nbeakhz=3\nsuits=grumbler\nrest=60\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "6,0,0", "0,0,0", "8,0,0", "0,0,0", "6,0,0", "0.15,0,0" }),
            Static("Ховається", 0, 3f, 3f, "0,0,0,1,0,0,0,0,0,0", 8, 2.5f, 3.5f, 0.3f, 0.6f, "tremble=1.5\ntremblehz=9\nsuits=timid\nneed=1\n",
                new[] { "-5,-5,0", "-4,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "10,0,0", "0,0,0", "20,0,0", "-8,0,0", "0,0,0", "0.6,0,0" }),
            Static("Воротить дзьоб", 0, 2f, 2f, "0,0,0,0,0,0,1,0,0,0", 5, 1.2f, 1.8f, 0.2f, 0.4f, "look=1\nmirror=1\nsuits=picky\nneed=1\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "-3,0,0", "0,0,0", "-6,35,0", "0,0,0", "0,0,0", "0,0,0" }),
            Waved("Хизується", 0, 3f, 3f, "0,0,0,0,1,0,0,0,0,0", 7, 2.2f, 2.8f, 0.3f, 0.7f, "tremble=0.8\ncaw=0.5,0.6\ncawpitch=1.15,0.9\ncawvolume=0.5\nsuits=showoff\nneed=1\nanswer=Відвертається\nanswer.chance=0.5\nanswer.delay=0.4,0.9\n",
                new[] { new[] { "24,20,0", "4,0,0", "-12,0,0", "0,0,0", "0,0,0", "0,0,0", "-3,0,0", "0,0,0", "-8,0,0", "-4,0,0", "4,0,0", "0,0,0" },
                       new[] { "58,44,0", "32,8,0", "-2,0,0", "0,0,0", "0,0,0", "0,0,0", "-6,0,0", "0,0,0", "-15,0,0", "-8,0,0", "8,0,0", "0,0,0" },
                       new[] { "70,52,0", "54,10,0", "22,0,0", "0,0,0", "0,0,0", "0,0,0", "-7,0,0", "0,0,0", "-18,0,0", "-10,0,0", "10,0,0", "0,0,0" },
                       new[] { "72,52,0", "60,10,0", "32,0,0", "0,0,0", "0,0,0", "0,0,0", "-7,0,0", "0,0,0", "-18,0,0", "-10,0,0", "10,0,0", "0,0,0" },
                       new[] { "62,46,0", "52,8,0", "28,0,0", "0,0,0", "0,0,0", "0,0,0", "-6,0,0", "0,0,0", "-15,0,0", "-8,0,0", "8,0,0", "0,0,0" } }),
            Static("Зазирає в торбу", 0, 1.5f, 1.5f, "1,0,0,0,0,0,0,1,0,0", 3, 1.4f, 2f, 0.2f, 0.4f, "look=2\nmirror=1\nsuits=thief,magpie\nneed=1\nrest=45\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "8,0,0", "0,0,0", "14,0,25", "0,0,0", "0,0,0", "0.2,0,0" }),
            Waved("Розправляє крило", 0, 0.35f, 0.3f, "1,0,0,0,0,0,0,0,0,0", 2, 1.8f, 2.4f, 0.4f, 0.6f, "mood=1,0,1,0\nrest=45\n",
                new[] { new[] { "0,0,0", "0,0,0", "0,0,0", "8,3,0", "0,0,0", "-8,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "0,0,0", "0,0,0", "0,0,0", "30,12,0", "12,0,0", "-2,0,0", "1.5,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "0,0,0", "0,0,0", "0,0,0", "52,19,0", "30,0,0", "14,0,0", "2.5,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "0,0,0", "0,0,0", "0,0,0", "52,19,0", "32,0,0", "18,0,0", "2.5,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0" },
                       new[] { "0,0,0", "0,0,0", "0,0,0", "28,10,0", "16,0,0", "8,0,0", "1,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0" } }),
            Static("Штурхає", 0, 0.6f, 0.9f, "0,0,0,0,0,0,0,0,0,1", 3, 0.8f, 1f, 0.2f, 0.3f, "look=3\nrest=60\nreach=0.04\nsuits=bold,fidget\nanswer=Відвертається\nanswer.chance=0.4\nanswer.delay=0.3,0.6\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "4,0,0", "0,0,0", "0,0,0", "0,0,0", "10,0,0", "0,0,0" }),
            Static("Переглядається", 0, 1f, 1f, "0,0,0,0,0,0,0,0,0,1", 2, 1.2f, 2f, 0.2f, 0.4f, "look=3\nmirror=1\nrest=20\nanswer=Дивиться на іншого\nanswer.chance=0.6\nanswer.delay=0.3,0.8\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,12", "0,0,0", "0,0,0", "0,0,0" }),
            Static("Дивиться на іншого", 0, 1f, 1f, "0,0,0,0,0,0,0,0,0,0", 2, 1f, 1.6f, 0.15f, 0.35f, "look=3\nmirror=1\nrest=4\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,8", "0,0,0", "0,0,0", "0,0,0" }),
            Waved("Дражнить", 0, 0.25f, 0.9f, "0,0,0,0,0,0,0,0,0,1", 3, 1.3f, 1.9f, 0.2f, 0.45f, "look=3\nbeakhz=6\nsuits=bold,fidget\nrest=45\nanswer=Відвертається\nanswer.chance=0.6\nanswer.delay=0.3,0.7\n",
                new[] { new[] { "3,4,0", "0,0,0", "-4,0,0", "0,0,0", "0,0,0", "0,0,0", "2,0,0", "0,0,0", "0,0,0", "0,0,0", "6,0,0", "0,0,0" },
                       new[] { "7,9,0", "2,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0", "4,0,0", "0,0,0", "0,0,0", "0,0,0", "10,0,0", "0,0,0" },
                       new[] { "8,10,0", "3,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0", "4,0,0", "0,0,0", "0,0,0", "0,0,0", "10,0,0", "0,0,0" },
                       new[] { "7,9,0", "2,0,0", "-5,0,0", "0,0,0", "0,0,0", "0,0,0", "4,0,0", "0,0,0", "0,0,0", "0,0,0", "10,0,0", "0,0,0" },
                       new[] { "4,5,0", "1,0,0", "-3,0,0", "0,0,0", "0,0,0", "0,0,0", "2,0,0", "0,0,0", "0,0,0", "0,0,0", "6,0,0", "0,0,0" } }),
            // after a landing: the wings folded once more as a wave (the shoulder, the elbow, the hand last), a flick of
            // the tail, a little shimmy of the body, settling into its legs (played only when asked: ShoulderLife / PerchLife)
            Waved(SettleName, 0, 0f, 0f, "0,0,0,0,0,0,0,0,0,0", 3, 1f, 1.3f, 0.1f, 0.35f, "mirror=1\nrest=0\nvary=0.2\n",
                new[] { new[] { "6,8,0", "2,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0", "2,0,2", "0,0,0", "4,0,0", "-8,0,0", "0,0,0", "0.2,0,0" },
                       new[] { "10,12,0", "6,3,0", "-8,0,0", "0,0,0", "0,0,0", "0,0,0", "1,0,-3", "0,0,0", "-3,0,0", "6,0,0", "0,0,0", "0.12,0,0" },
                       new[] { "4,5,0", "6,2,0", "-4,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,2", "0,0,0", "2,0,0", "-5,0,0", "0,0,0", "0.05,0,0" },
                       new[] { "1,1,0", "2,0,0", "2,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,-1", "0,0,0", "0,0,0", "3,0,0", "0,0,0", "0,0,0" },
                       new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0" } }),
            Static("Відвертається", 0, 0.2f, 0.2f, "1,0,0,0,0,0,0,0,0,0", 2, 1.2f, 2f, 0.25f, 0.5f, "look=4\nsuits=willful,calm\nrest=40\n",
                new[] { "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "0,0,0", "-2,0,0", "0,0,0", "-6,0,0", "0,0,0", "0,0,0", "0,0,0" }),
        };

        // a built-in held the same at all five keys (the fades shape it)
        private static string Static(string name, int only, float wh, float wm, string sits, int prio, float smin, float smax, float fin, float fout, string extra, string[] pose)
        {
            return Head(name, only, wh, wm, sits, prio, smin, smax, fin, fout, extra) + Keys(pose, new[] { 1f, 1f, 1f, 1f, 1f });
        }
        // … each of the five keys its own pose (a move with its own shape: a wave along the wing)
        private static string Waved(string name, int only, float wh, float wm, string sits, int prio, float smin, float smax, float fin, float fout, string extra, string[][] keys)
        {
            StringBuilder sb = new StringBuilder(Head(name, only, wh, wm, sits, prio, smin, smax, fin, fout, extra));
            for (int k = 0; k < SitTemplate.Keys && k < keys.Length; k++) sb.Append(Keys(keys[k], new[] { 1f, 1f, 1f, 1f, 1f }).Split('\n')[k]).Append('\n');
            return sb.ToString();
        }
        private static string Head(string name, int only, float wh, float wm, string sits, int prio, float smin, float smax, float fin, float fout, string extra)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return "name=" + name + "\nonly=" + only + "\nweight.h=" + wh.ToString(c) + "\nweight.m=" + wm.ToString(c) + "\nsit=" + sits + "\nmood=" + (IsDoing(sits) ? "1,1,1,0" : "1,1,1,1") + "\nprio=" + prio
                + "\nsec=" + smin.ToString(c) + "," + smax.ToString(c) + "\nfade=" + fin.ToString(c) + "," + fout.ToString(c) + "\nrest=8\nvary=0.15\n" + extra;
        }
        // its own doings (and the pair's) are not for a drowsy raven unless the template says so (its reactions are)
        private static bool IsDoing(string sits) { string[] p = sits.Split(','); return (p.Length > 0 && p[0].Trim() == "1") || (p.Length > Pair && p[Pair].Trim() == "1"); }
        private static string Keys(string[] pose, float[] scale)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            for (int k = 0; k < SitTemplate.Keys; k++)
            {
                sb.Append('k').Append(k + 1).Append('=');
                for (int b = 0; b < SitTemplate.Bones; b++)
                {
                    string[] xyz = pose[b].Split(',');
                    sb.Append(b > 0 ? ";" : "");
                    for (int i = 0; i < 3; i++) sb.Append(i > 0 ? "," : "").Append((float.Parse(xyz[i], c) * scale[k]).ToString("0.#", c));
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        internal static readonly List<SitTemplate> All = new List<SitTemplate>();
        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "sit_templates"); } }

        internal static SitTemplate Find(string name) { foreach (SitTemplate t in All) if (t.Name == name) return t; return null; }
        internal static SitTemplate FindId(int id) { foreach (SitTemplate t in All) if (t.Id == id) return t; return null; }

        // a template for this situation and mood, by this raven's weights; 'ok' may still say no (its rest); 'r' the
        // chance (another player's game picks with the owner's seed when it lacks the template the owner played)
        internal static SitTemplate Pick(int sit, int mood, bool munin, System.Func<SitTemplate, bool> ok, float r) { return Pick(sit, mood, munin, ok, r, null); }

        // … 'weight': this raven's weight of a template (its character, its quirk; 0: not for it); null: the template's own
        internal static SitTemplate Pick(int sit, int mood, bool munin, System.Func<SitTemplate, bool> ok, float r, System.Func<SitTemplate, float> weight)
        {
            float total = 0f;
            for (int pass = 0; pass < 2; pass++) // a mood none fits: any mood
            {
                if (pass == 1 && mood == (int)SitMood.Drowsy && (sit == Doing || sit == Pair)) break; // (a drowsy raven: nothing rather than a lively doing)
                total = 0f;
                foreach (SitTemplate t in All) if (Fits(t, sit, pass == 0 ? mood : -1, munin, ok)) total += weight != null ? weight(t) : t.WeightFor(munin);
                if (total <= 0f) continue;
                float x = r * total;
                foreach (SitTemplate t in All)
                {
                    if (!Fits(t, sit, pass == 0 ? mood : -1, munin, ok)) continue;
                    x -= weight != null ? weight(t) : t.WeightFor(munin);
                    if (x <= 0f) return t;
                }
            }
            return null;
        }
        private static bool Fits(SitTemplate t, int sit, int mood, bool munin, System.Func<SitTemplate, bool> ok)
        {
            return t.Sits[sit] && t.For(munin) && (mood < 0 || t.Moods[mood]) && t.WeightFor(munin) > 0f && (!t.Need || ok != null) && (ok == null || ok(t)); // (one only theirs: never as another game's stand-in)
        }

        internal static void Load()
        {
            All.Clear();
            foreach (string text in Builtin) { SitTemplate t = FromText(text); if (t != null) { t.Builtin = true; All.Add(t); } }
            try
            {
                if (Directory.Exists(Dir))
                    foreach (string f in Directory.GetFiles(Dir, "*.txt"))
                    {
                        SitTemplate t = FromText(File.ReadAllText(f, Encoding.UTF8));
                        if (t == null) continue;
                        SitTemplate had = Find(t.Name);
                        if (had != null) { t.Builtin = had.Builtin; All[All.IndexOf(had)] = t; } else All.Add(t);
                    }
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Sitting templates unreadable: " + e.Message); }
            if (Plugin.Instance != null) Plugin.Instance.Log("Sitting templates: " + All.Count);
        }

        private static string FileOf(string name)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in name) sb.Append(System.Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
            return Path.Combine(Dir, sb.ToString().Trim() + ".txt");
        }

        internal static bool Save(SitTemplate t)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FileOf(t.Name), ToText(t), new UTF8Encoding(false));
                if (Plugin.Instance != null) Plugin.Instance.Log("Sitting template saved: " + t.Name + " -> " + FileOf(t.Name));
                return true;
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Sitting template not saved: " + e.Message); return false; }
        }

        internal static void Rename(SitTemplate t, string name)
        {
            string old = FileOf(t.Name);
            t.Name = name;
            try { if (File.Exists(old) && old != FileOf(name)) File.Delete(old); } catch { }
        }

        internal static void Delete(SitTemplate t)
        {
            try { string f = FileOf(t.Name); if (File.Exists(f)) File.Delete(f); } catch { }
            int at = All.IndexOf(t);
            All.Remove(t);
            if (!t.Builtin) return;
            foreach (string text in Builtin) // a built-in one comes back as built
            {
                SitTemplate b = FromText(text);
                if (b == null || b.Name != t.Name) continue;
                b.Builtin = true;
                All.Insert(Mathf.Clamp(at, 0, All.Count), b);
                break;
            }
        }

        internal static string ToText(SitTemplate t)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            System.Func<float, string> F = v => v.ToString("0.###", c);
            sb.Append("name=").Append(t.Name).Append('\n');
            sb.Append("only=").Append(t.Only).Append("\nweight.h=").Append(F(t.WeightH)).Append("\nweight.m=").Append(F(t.WeightM)).Append('\n');
            sb.Append("sit="); for (int i = 0; i < SitCount; i++) sb.Append(i > 0 ? "," : "").Append(t.Sits[i] ? '1' : '0'); sb.Append('\n');
            sb.Append("mood="); for (int i = 0; i < 4; i++) sb.Append(i > 0 ? "," : "").Append(t.Moods[i] ? '1' : '0'); sb.Append('\n');
            sb.Append("prio=").Append(t.Prio).Append("\nsec=").Append(F(t.SecMin)).Append(',').Append(F(t.SecMax)).Append("\nfade=").Append(F(t.In)).Append(',').Append(F(t.Out))
              .Append("\nrest=").Append(F(t.Rest)).Append("\nvary=").Append(F(t.Vary)).Append("\nclip=").Append(t.Clip).Append("\nlook=").Append(t.Look)
              .Append("\ntremble=").Append(F(t.Tremble)).Append("\ntremblehz=").Append(F(t.TrembleHz)).Append("\nflap=").Append(F(t.Flap))
              .Append("\nkick=").Append(F(t.KickPitch)).Append(',').Append(F(t.KickRoll)).Append(',').Append(F(t.KickDip))
              .Append("\nbeakhz=").Append(F(t.BeakHz)).Append("\ncaw=").Append(F(t.CawH)).Append(',').Append(F(t.CawM))
              .Append("\ncawpitch=").Append(F(t.CawPitchH)).Append(',').Append(F(t.CawPitchM)).Append("\ncawvolume=").Append(F(t.CawVolume))
              .Append("\nshake=").Append(t.Shake ? 1 : 0).Append("\nstep=").Append(t.Step ? 1 : 0).Append("\nmirror=").Append(t.Mirror ? 1 : 0)
              .Append("\nsuits=").Append(string.Join(",", t.Suits)).Append("\nneed=").Append(t.Need ? 1 : 0)
              .Append("\nreach=").Append(F(t.Reach)).Append("\nanswer=").Append(t.Answer).Append("\nanswer.chance=").Append(F(t.AnswerChance)).Append("\nanswer.delay=").Append(F(t.AnswerMin)).Append(',').Append(F(t.AnswerMax)).Append('\n');
            for (int k = 0; k < SitTemplate.Keys; k++)
            {
                sb.Append('k').Append(k + 1).Append('=');
                for (int b = 0; b < SitTemplate.Bones; b++) { Vector3 v = t.Pose[k, b]; sb.Append(b > 0 ? ";" : "").Append(v.x.ToString("0.#", c)).Append(',').Append(v.y.ToString("0.#", c)).Append(',').Append(v.z.ToString("0.#", c)); }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        internal static SitTemplate FromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            CultureInfo c = CultureInfo.InvariantCulture;
            SitTemplate t = new SitTemplate();
            bool named = false;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0 || raw.TrimStart().StartsWith("#")) continue;
                string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                float f;
                bool num = float.TryParse(v, NumberStyles.Float, c, out f);
                float[] list = Floats(v);
                switch (k)
                {
                    case "name": t.Name = v; named = v.Length > 0; break;
                    case "only": if (num) t.Only = Mathf.Clamp((int)f, 0, 2); break;
                    case "weight.h": if (num) t.WeightH = f; break;
                    case "weight.m": if (num) t.WeightM = f; break;
                    case "sit": { string[] p = v.Split(','); for (int i = 0; i < SitCount && i < p.Length; i++) t.Sits[i] = p[i].Trim() == "1"; break; }
                    case "mood": { string[] p = v.Split(','); for (int i = 0; i < 4 && i < p.Length; i++) t.Moods[i] = p[i].Trim() == "1"; break; }
                    case "prio": if (num) t.Prio = Mathf.Clamp((int)f, 1, 9); break;
                    case "sec": if (list.Length >= 2) { t.SecMin = list[0]; t.SecMax = list[1]; } break;
                    case "fade": if (list.Length >= 2) { t.In = list[0]; t.Out = list[1]; } break;
                    case "rest": if (num) t.Rest = f; break;
                    case "vary": if (num) t.Vary = Mathf.Clamp01(f); break;
                    case "clip": if (num) t.Clip = Mathf.Clamp((int)f, -1, 7); break;
                    case "look": if (num) t.Look = Mathf.Clamp((int)f, 0, 4); break;
                    case "tremble": if (num) t.Tremble = f; break;
                    case "tremblehz": if (num) t.TrembleHz = f; break;
                    case "flap": if (num) t.Flap = f; break;
                    case "kick": if (list.Length >= 3) { t.KickPitch = list[0]; t.KickRoll = list[1]; t.KickDip = list[2]; } break;
                    case "beakhz": if (num) t.BeakHz = f; break;
                    case "caw": if (list.Length >= 2) { t.CawH = list[0]; t.CawM = list[1]; } break;
                    case "cawpitch": if (list.Length >= 2) { t.CawPitchH = list[0]; t.CawPitchM = list[1]; } break;
                    case "cawvolume": if (num) t.CawVolume = f; break;
                    case "shake": t.Shake = v == "1"; break;
                    case "step": t.Step = v == "1"; break;
                    case "mirror": t.Mirror = v == "1"; break;
                    case "suits": { List<string> ids = new List<string>(); foreach (string id in v.Split(',')) if (id.Trim().Length > 0) ids.Add(id.Trim().ToLowerInvariant()); t.Suits = ids.ToArray(); break; }
                    case "need": t.Need = v == "1"; break;
                    case "reach": if (num) t.Reach = Mathf.Clamp(f, 0f, 0.2f); break;
                    case "answer": t.Answer = v; break;
                    case "answer.chance": if (num) t.AnswerChance = Mathf.Clamp01(f); break;
                    case "answer.delay": if (list.Length >= 2) { t.AnswerMin = list[0]; t.AnswerMax = list[1]; } break;
                    default:
                        if (k.Length >= 2 && k[0] == 'k')
                        {
                            int key;
                            if (!int.TryParse(k.Substring(1), out key) || key < 1 || key > SitTemplate.Keys) break;
                            string[] bones = v.Split(';');
                            for (int b = 0; b < SitTemplate.Bones && b < bones.Length; b++)
                            {
                                float[] xyz = Floats(bones[b]);
                                if (xyz.Length == 3) t.Pose[key - 1, b] = new Vector3(xyz[0], xyz[1], xyz[2]);
                            }
                        }
                        break;
                }
            }
            return named ? t : null;
        }

        private static float[] Floats(string v)
        {
            string[] p = v.Split(',');
            List<float> r = new List<float>();
            foreach (string s in p) { float f; if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f)) r.Add(f); else return new float[0]; }
            return r.ToArray();
        }
    }
}
#endif
