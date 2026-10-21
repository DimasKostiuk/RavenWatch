#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Where a raven is flying (a flight template may be allowed in some of these): from the motor's plan
    internal enum FlightSit { Attack = 0, Home = 1, Follow = 2, Circle = 3, Scout = 4, Dome = 5, Hover = 6, TakeOff = 7, Arrive = 8, Dive = 9, Land = 10 }

    // «Шаблон польоту»: a named flight style made in the test window (tab «Шаблони»). Two layers, each can be off:
    //   the motion — multipliers on the raven's own style (speed, turns, banks, glides, beat tempo) and waves up and down;
    //   the pose  — at the ten points of a wing stroke (as the wing keys), turns of the wing bones (shoulder, elbow,
    //               hand: swept, lifted, twisted; the right wing mirrors), the body, the head and the tail (pitch, yaw, roll),
    //               all on top of everything else.
    // Ticks say where it may be used; it can be for both ravens or one. The flight director in the motor picks.
    internal sealed class FlightTemplate
    {
        internal const int PoseBones = 7; // 0..2 wing shoulder/elbow/hand, 3 body, 4 head, 5 tail, 6 neck (= 3 + WingKeys' body index)
        public string Name = "Новий шаблон";
        public bool Builtin;
        public int Only;                  // 0 both, 1 Hugin only, 2 Munin only
        public readonly bool[] Allowed = new bool[FlightTemplates.SitCount];
        public float Weight = 1f;
        public float WeightH = -1f, WeightM = -1f; // how often for Hugin / Munin (-1: as Weight; old files)
        public bool UseMotion = true, UsePose = true;
        public float Speed = 1f, Turn = 1f, Bank = 1f, Glide = 1f, Beat = 1f, Wave = 0f, WaveHz = 0.5f;
        public readonly Vector3[,] Pose = new Vector3[WingKeys.Keys, PoseBones];

        internal bool For(bool munin) { return Only == 0 || (Only == 1 && !munin) || (Only == 2 && munin); }
        // how often this raven picks it (each its own: Hugin glides, Munin dashes)
        internal float WeightFor(bool munin) { float w = munin ? WeightM : WeightH; return Mathf.Max(0f, w >= 0f ? w : Weight); }

        // the pose at key coordinate x (0..10, round), smooth between keys
        internal Vector3 At(float x, int bone)
        {
            x = Mathf.Repeat(x, WingKeys.Keys);
            int a = Mathf.Clamp((int)x, 0, WingKeys.Keys - 1), b = (a + 1) % WingKeys.Keys;
            float f = Mathf.Clamp01(x - a);
            f = f * f * (3f - 2f * f);
            return Vector3.Lerp(Pose[a, bone], Pose[b, bone], f);
        }

        internal FlightTemplate Copy(string name)
        {
            FlightTemplate t = FlightTemplates.FromText(FlightTemplates.ToText(this));
            t.Name = name; t.Builtin = false;
            return t;
        }
    }

    internal static class FlightTemplates
    {
        internal const int SitCount = 11;
        internal static readonly string[] SitNames = { "Атака", "Повернення додому", "Слідом", "Кружляння", "Розвідка", "Купол", "Зависання / танець", "Зліт", "Приліт", "Ниряння", "Посадка" };

        // the director: a pick every SwitchMin..SwitchMax s; it may keep what it has (KeepChance) or fly plain
        // (no template: NoneWeight among the weights); a new one eases in over BlendSeconds, from a stroke's top
        internal static float SwitchMin = 3f, SwitchMax = 8f, KeepChance = 0.35f, NoneWeight = 1f, BlendSeconds = 0.8f;
        internal static bool On = true;
        internal static string ForceName = "";              // «Грати в грі»: every raven of yours flies this one, everywhere
        internal static string DiveTestName = "";           // «Майстерня»: force only on a commanded dive while testing
        internal static bool DiveTestPoseOnly = true;       // show the authored pose without motor wing shaping
        internal static FlightTemplate PreviewForce;        // the picture shows this one (the editor's selection)
        internal static FlightSit PreviewSit = FlightSit.Circle; // else the picture plays as this situation

        // made by the user and baked in (as the wing beat): the files in config/RavenWatch/templates override them by name
        // (baked 2026-10-13 from RavenTest: 9 templates)
        private static readonly string[] Builtin =
        {
            @"name=Мисливець — ковзання
only=0
weight=1
weight.h=1.4
weight.m=0.3
sit=1,0,0,0,0,0,0,0,0,0,0
motion=1
speed=1
turn=0.9
bank=1.2
glide=1.6
beat=0.8
wave=0.15
wavehz=0.4
pose=1
k1=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k3=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k4=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k5=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k7=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=0,3,0;4,0,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Забіяка — шквал
only=0
weight=1
weight.h=0.3
weight.m=1.4
sit=1,0,0,0,0,0,0,0,0,0,0
motion=1
speed=1.15
turn=1.2
bank=1.15
glide=0.3
beat=1.4
wave=0.1
wavehz=0.8
pose=1
k1=-4,0,0;-4,-1.1,0;0,-1.4,-3.1;0,0,0;0,0,0;0,0,0;0,0,0
k2=-4,2.4,0;-4,-0.4,0;0,-0.5,-1.1;0,0,0;0,0,0;0,0,0;0,0,0
k3=-4,3.8,0;-4,0.5,0;0,0.6,1.4;0,0,0;0,0,0;0,0,0;0,0,0
k4=-4,3.8,0;-4,1.2,0;0,1.5,3.3;0,0,0;0,0,0;0,0,0;0,0,0
k5=-4,2.4,0;-4,1.4,0;0,1.8,4;0,0,0;0,0,0;0,0,0;0,0,0
k6=-4,0,0;-4,1.1,0;0,1.4,3.1;0,0,0;0,0,0;0,0,0;0,0,0
k7=-4,-2.4,0;-4,0.4,0;0,0.5,1.1;0,0,0;0,0,0;0,0,0;0,0,0
k8=-4,-3.8,0;-4,-0.5,0;0,-0.6,-1.4;0,0,0;0,0,0;0,0,0;0,0,0
k9=-4,-3.8,0;-4,-1.2,0;0,-1.5,-3.3;0,0,0;0,0,0;0,0,0;0,0,0
k10=-4,-2.4,0;-4,-1.4,0;0,-1.8,-4;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Важкий старт
only=0
weight=1
weight.h=1.3
weight.m=0.5
sit=0,0,0,0,0,0,0,1,0,0,0
motion=1
speed=0.95
turn=0.9
bank=0.9
glide=0.2
beat=0.85
wave=0
wavehz=0.5
pose=1
k1=0,0,0;5,-1.6,0;3,-2.1,-2.3;0,0,0;0,0,0;0,0,0;0,0,0
k2=0,3.5,0;5,-0.6,0;3,-0.7,-0.8;0,0,0;0,0,0;0,0,0;0,0,0
k3=0,5.7,0;5,0.7,0;3,0.9,1;0,0,0;0,0,0;0,0,0;0,0,0
k4=0,5.7,0;5,1.7,0;3,2.2,2.5;0,0,0;0,0,0;0,0,0;0,0,0
k5=0,3.5,0;5,2.1,0;3,2.7,3;0,0,0;0,0,0;0,0,0;0,0,0
k6=0,0,0;5,1.6,0;3,2.1,2.3;0,0,0;0,0,0;0,0,0;0,0,0
k7=0,-3.5,0;5,0.6,0;3,0.7,0.8;0,0,0;0,0,0;0,0,0;0,0,0
k8=0,-5.7,0;5,-0.7,0;3,-0.9,-1;0,0,0;0,0,0;0,0,0;0,0,0
k9=0,-5.7,0;5,-1.7,0;3,-2.2,-2.5;0,0,0;0,0,0;0,0,0;0,0,0
k10=0,-3.5,0;5,-2.1,0;3,-2.7,-3;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Стрімкий старт
only=0
weight=1
weight.h=0.5
weight.m=1.3
sit=0,0,0,0,0,0,0,1,0,0,0
motion=1
speed=1.1
turn=1.1
bank=1
glide=0.1
beat=1.35
wave=0
wavehz=0.5
pose=1
k1=-3,-3,0;0,-0.8,0;0,-1.1,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=-3,-1.2,0;0,-0.3,0;0,-0.4,0;0,0,0;0,0,0;0,0,0;0,0,0
k3=-3,-0.1,0;0,0.4,0;0,0.5,0;0,0,0;0,0,0;0,0,0;0,0,0
k4=-3,-0.1,0;0,0.9,0;0,1.1,0;0,0,0;0,0,0;0,0,0;0,0,0
k5=-3,-1.2,0;0,1,0;0,1.3,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=-3,-3,0;0,0.8,0;0,1.1,0;0,0,0;0,0,0;0,0,0;0,0,0
k7=-3,-4.8,0;0,0.3,0;0,0.4,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=-3,-5.9,0;0,-0.4,0;0,-0.5,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=-3,-5.9,0;0,-0.9,0;0,-1.1,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=-3,-4.8,0;0,-1,0;0,-1.3,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Ширяння над куполом
only=0
weight=1
weight.h=1.3
weight.m=0.6
sit=0,0,0,0,0,1,0,0,0,0,0
motion=1
speed=0.9
turn=0.9
bank=1.1
glide=1.8
beat=0.7
wave=0.4
wavehz=0.3
pose=1
k1=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k3=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k4=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k5=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k7=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=0,4,0;3,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Вартовий
only=0
weight=1
weight.h=0.7
weight.m=1.2
sit=0,0,0,0,0,1,0,0,0,0,0
motion=1
speed=1.05
turn=1.05
bank=0.9
glide=0.6
beat=1.1
wave=0.1
wavehz=0.6
pose=1
k1=-2,0,0;0,-0.5,0;0,-0.7,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=-2,1.2,0;0,-0.2,0;0,-0.2,0;0,0,0;0,0,0;0,0,0;0,0,0
k3=-2,1.9,0;0,0.2,0;0,0.3,0;0,0,0;0,0,0;0,0,0;0,0,0
k4=-2,1.9,0;0,0.6,0;0,0.7,0;0,0,0;0,0,0;0,0,0;0,0,0
k5=-2,1.2,0;0,0.7,0;0,0.9,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=-2,0,0;0,0.5,0;0,0.7,0;0,0,0;0,0,0;0,0,0;0,0,0
k7=-2,-1.2,0;0,0.2,0;0,0.2,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=-2,-1.9,0;0,-0.2,0;0,-0.3,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=-2,-1.9,0;0,-0.6,0;0,-0.7,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=-2,-1.2,0;0,-0.7,0;0,-0.9,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Ліниве зависання
only=0
weight=1
weight.h=1.3
weight.m=0.4
sit=0,0,0,0,0,0,1,0,0,0,0
motion=1
speed=0.8
turn=1
bank=1
glide=0
beat=0.85
wave=0
wavehz=0.5
pose=1
k1=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0
k2=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0
k3=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0
k4=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0
k5=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0
k6=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0
k7=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0
k8=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0
k9=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0
k10=0,5,0;4,0,0;0,0,0;0,0,0;0,0,0;3,0,0;0,0,0",
            @"name=Нетерплячий
only=0
weight=1
weight.h=0.4
weight.m=1.3
sit=0,0,0,0,0,0,1,0,0,0,0
motion=1
speed=1
turn=1
bank=1
glide=0
beat=1.35
wave=0
wavehz=0.5
pose=1
k1=-2,0,0;0,-0.8,0;0,-1.1,-1.6;0,0,0;0,0,0;0,0,0;0,0,0
k2=-2,1.8,0;0,-0.3,0;0,-0.4,-0.5;0,0,0;0,0,0;0,0,0;0,0,0
k3=-2,2.9,0;0,0.4,0;0,0.5,0.7;0,0,0;0,0,0;0,0,0;0,0,0
k4=-2,2.9,0;0,0.9,0;0,1.1,1.7;0,0,0;0,0,0;0,0,0;0,0,0
k5=-2,1.8,0;0,1,0;0,1.3,2;0,0,0;0,0,0;0,0,0;0,0,0
k6=-2,0,0;0,0.8,0;0,1.1,1.6;0,0,0;0,0,0;0,0,0;0,0,0
k7=-2,-1.8,0;0,0.3,0;0,0.4,0.5;0,0,0;0,0,0;0,0,0;0,0,0
k8=-2,-2.9,0;0,-0.4,0;0,-0.5,-0.7;0,0,0;0,0,0;0,0,0;0,0,0
k9=-2,-2.9,0;0,-0.9,0;0,-1.1,-1.7;0,0,0;0,0,0;0,0,0;0,0,0
k10=-2,-1.8,0;0,-1,0;0,-1.3,-2;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Зниження планером
only=0
weight=1
weight.h=1.3
weight.m=0.6
sit=0,0,0,0,0,0,0,0,1,0,0
motion=1
speed=0.95
turn=0.95
bank=1
glide=1.7
beat=0.75
wave=0.2
wavehz=0.35
pose=1
k1=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k3=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k4=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k5=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k7=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=0,3,0;2,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Пірнання до плеча
only=0
weight=1
weight.h=0.5
weight.m=1.3
sit=0,0,0,0,0,0,0,0,1,0,0
motion=1
speed=1.15
turn=1.05
bank=1.05
glide=0.4
beat=1.2
wave=0
wavehz=0.5
pose=1
k1=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k3=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k4=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k5=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k7=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=-6,0,0;-8,0,0;-5,0,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Каменем
only=0
weight=1
weight.h=0.7
weight.m=1.3
sit=0,0,0,0,0,0,0,0,0,1,0
motion=1
speed=1.05
turn=1
bank=1
glide=1
beat=1
wave=0
wavehz=0.5
pose=1
k1=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0
k2=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0
k3=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0
k4=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0
k5=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0
k6=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0
k7=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0
k8=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0
k9=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0
k10=-12,0,0;-14,0,0;-10,0,0;2,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Напівкрило
only=0
weight=1
weight.h=1.3
weight.m=0.7
sit=0,0,0,0,0,0,0,0,0,1,0
motion=1
speed=1
turn=1
bank=1
glide=1
beat=1
wave=0
wavehz=0.5
pose=1
k1=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k3=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k4=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k5=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k7=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=-5,2,0;-6,0,0;-4,0,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Посадка — V угору
only=0
weight=1
weight.h=1.4
weight.m=0.5
sit=0,0,0,0,0,0,0,0,0,0,1
motion=0
speed=1
turn=1
bank=1
glide=1
beat=1
wave=0
wavehz=0.5
pose=1
k1=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k3=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k4=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k5=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k7=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=5,28,0;-10,0,0;-6,0,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Посадка — широке гальмо
only=0
weight=1
weight.h=1.4
weight.m=0.6
sit=0,0,0,0,0,0,0,0,0,0,1
motion=0
speed=1
turn=1
bank=1
glide=1
beat=1
wave=0
wavehz=0.5
pose=1
k1=18,8,0;22,-3.3,0;13,-4.2,-3.9;0,0,0;0,0,0;0,0,0;0,0,0
k2=18,15.1,0;22,-1.1,0;13,-1.4,-1.3;0,0,0;0,0,0;0,0,0;0,0,0
k3=18,19.4,0;22,1.5,0;13,1.9,1.7;0,0,0;0,0,0;0,0,0;0,0,0
k4=18,19.4,0;22,3.5,0;13,4.5,4.2;0,0,0;0,0,0;0,0,0;0,0,0
k5=18,15.1,0;22,4.2,0;13,5.4,5;0,0,0;0,0,0;0,0,0;0,0,0
k6=18,8,0;22,3.3,0;13,4.2,3.9;0,0,0;0,0,0;0,0,0;0,0,0
k7=18,0.9,0;22,1.1,0;13,1.4,1.3;0,0,0;0,0,0;0,0,0;0,0,0
k8=18,-3.4,0;22,-1.5,0;13,-1.9,-1.7;0,0,0;0,0,0;0,0,0;0,0,0
k9=18,-3.4,0;22,-3.5,0;13,-4.5,-4.2;0,0,0;0,0,0;0,0,0;0,0,0
k10=18,0.9,0;22,-4.2,0;13,-5.4,-5;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Посадка — тремтіння
only=0
weight=1
weight.h=0.4
weight.m=1.4
sit=0,0,0,0,0,0,0,0,0,0,1
motion=1
speed=1
turn=1
bank=1
glide=1
beat=1.4
wave=0
wavehz=0.5
pose=1
k1=4,10,0;8,-2.5,0;5,-3.2,-3.1;0,0,0;0,0,0;0,0,0;0,0,0
k2=4,15.3,0;8,-0.8,0;5,-1.1,-1.1;0,0,0;0,0,0;0,0,0;0,0,0
k3=4,18.6,0;8,1.1,0;5,1.4,1.4;0,0,0;0,0,0;0,0,0;0,0,0
k4=4,18.6,0;8,2.6,0;5,3.4,3.3;0,0,0;0,0,0;0,0,0;0,0,0
k5=4,15.3,0;8,3.1,0;5,4,4;0,0,0;0,0,0;0,0,0;0,0,0
k6=4,10,0;8,2.5,0;5,3.2,3.1;0,0,0;0,0,0;0,0,0;0,0,0
k7=4,4.7,0;8,0.8,0;5,1.1,1.1;0,0,0;0,0,0;0,0,0;0,0,0
k8=4,1.4,0;8,-1.1,0;5,-1.4,-1.4;0,0,0;0,0,0;0,0,0;0,0,0
k9=4,1.4,0;8,-2.6,0;5,-3.4,-3.3;0,0,0;0,0,0;0,0,0;0,0,0
k10=4,4.7,0;8,-3.1,0;5,-4,-4;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Посадка — напівскладені
only=0
weight=1
weight.h=0.4
weight.m=1.2
sit=0,0,0,0,0,0,0,0,0,0,1
motion=0
speed=1
turn=1
bank=1
glide=1
beat=1
wave=0
wavehz=0.5
pose=1
k1=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k3=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k4=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k5=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k7=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=-12,-6,0;-18,0,0;-11,0,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Планер — спокійний
only=0
weight=1
weight.h=1.6
weight.m=0.5
sit=0,1,1,0,1,0,0,0,0,0
motion=1
speed=0.92
turn=0.85
bank=0.8
glide=1.8
beat=0.72
wave=0.25
wavehz=0.35
pose=1
k1=1,4,0;0,2,0;0,0,0;0,0,-1;0,0,0;1,0,0;0,0,0
k2=2,3,0;1,1,0;0,0,0;0,0,-1;0,0,0;1,0,0;0,0,0
k3=2,1,0;1,0,0;0,0,0;0,0,-1;0,0,0;2,0,0;0,0,0
k4=1,0,0;0,-1,0;0,0,0;0,0,-1;0,0,0;2,0,0;0,0,0
k5=0,-1,0;0,-1,0;0,0,0;0,0,-1;0,0,0;1,0,0;0,0,0
k6=0,-2,0;0,-1,0;0,0,0;0,0,-1;0,0,0;1,0,0;0,0,0
k7=-1,-1,0;0,0,0;0,0,0;0,0,-1;0,0,0;1,0,0;0,0,0
k8=-1,1,0;0,1,0;0,0,0;0,0,-1;0,0,0;1,0,0;0,0,0
k9=0,3,0;0,2,0;0,0,0;0,0,-1;0,0,0;1,0,0;0,0,0
k10=1,4,0;0,2,0;0,0,0;0,0,-1;0,0,0;1,0,0;0,0,0",
            @"name=Ривок — стрімкий
only=0
weight=1
weight.h=0.35
weight.m=1.5
sit=1,0,0,0,1,0,0,1,0,0
motion=1
speed=1.25
turn=1.15
bank=1.1
glide=0.35
beat=1.35
wave=0.2
wavehz=0.75
pose=1
k1=0,1,0;0,0,0;0,0,0;2,0,0;0,0,0;-2,0,0;0,0,0
k2=-2,0,0;-1,0,0;0,0,0;2,0,0;0,0,0;-2,0,0;0,0,0
k3=-3,-3,0;-2,-2,0;-1,0,0;3,0,0;0,0,0;-3,0,0;0,0,0
k4=-2,-4,0;-1,-2,0;0,0,0;3,0,0;0,0,0;-3,0,0;0,0,0
k5=0,-3,0;0,-1,0;0,0,0;2,0,0;0,0,0;-2,0,0;0,0,0
k6=2,-2,0;1,-1,0;0,0,0;1,0,0;0,0,0;-1,0,0;0,0,0
k7=2,0,0;1,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=1,1,0;0,1,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=0,2,0;0,1,0;0,0,0;1,0,0;0,0,0;-1,0,0;0,0,0
k10=0,1,0;0,0,0;0,0,0;2,0,0;0,0,0;-2,0,0;0,0,0",
            @"name=Віраж — широкий
only=0
weight=1
sit=0,0,0,0,1,1,0,0,0,0
motion=1
speed=0.92
turn=1.55
bank=1.5
glide=0.95
beat=1.05
wave=0.45
wavehz=0.5
pose=1
k1=0,2,0;0,1,0;0,0,0;0,0,0;0,0,0;0,1,0;0,0,0
k2=1,1,0;0,1,0;0,0,0;0,0,-1;0,0,0;0,2,0;0,0,0
k3=1,0,0;1,0,0;0,0,0;0,0,-2;0,0,0;0,3,0;0,0,0
k4=0,-1,0;0,0,0;0,0,0;0,0,-2;0,0,0;0,3,0;0,0,0
k5=-1,-1,0;0,-1,0;0,0,0;0,0,-1;0,0,0;0,2,0;0,0,0
k6=-1,-2,0;0,-1,0;0,0,0;0,0,1;0,0,0;0,1,0;0,0,0
k7=0,-1,0;0,0,0;0,0,0;0,0,2;0,0,0;0,0,0;0,0,0
k8=1,0,0;0,1,0;0,0,0;0,0,2;0,0,0;0,-1,0;0,0,0
k9=1,1,0;1,1,0;0,0,0;0,0,1;0,0,0;0,-1,0;0,0,0
k10=0,2,0;0,1,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Зависання — активні крила
only=0
weight=1
sit=0,0,0,0,0,0,1,0,0,0
motion=1
speed=0.7
turn=1
bank=0.25
glide=0
beat=1.4
wave=0
wavehz=0.5
pose=1
k1=2,8,0;1,5,0;0,2,0;0,0,0;0,0,0;0,0,0;0,0,0
k2=3,5,0;2,3,0;1,1,0;0,0,0;0,1,0;0,0,0;0,0,0
k3=2,1,0;1,0,0;0,0,0;0,0,0;0,2,0;0,0,0;0,0,0
k4=0,-2,0;0,-1,0;0,0,0;0,0,0;0,1,0;0,0,0;0,0,0
k5=-2,-4,0;-1,-2,0;0,-1,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=-1,-3,0;0,-2,0;0,0,0;0,0,0;0,-1,0;0,0,0;0,0,0
k7=1,-1,0;1,0,0;0,0,0;0,0,0;0,-2,0;0,0,0;0,0,0
k8=3,3,0;2,2,0;1,0,0;0,0,0;0,-1,0;0,0,0;0,0,0
k9=3,7,0;2,4,0;1,1,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=2,8,0;1,5,0;0,2,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Захід — м'яке гальмування
only=0
weight=1
sit=0,1,0,0,0,0,0,0,1,0
motion=1
speed=0.88
turn=1.25
bank=0.65
glide=0.55
beat=1.15
wave=0.1
wavehz=0.35
pose=1
k1=0,2,0;0,1,0;0,0,0;0,0,0;0,0,0;1,0,0;0,0,0
k2=1,1,0;0,1,0;0,0,0;1,0,0;0,0,0;1,0,0;0,0,0
k3=2,-1,0;1,-1,0;0,0,0;2,0,0;0,0,0;2,0,0;0,0,0
k4=2,-3,0;1,-2,0;0,-1,0;2,0,0;0,0,0;2,0,0;0,0,0
k5=1,-2,0;0,-1,0;0,0,0;1,0,0;0,0,0;1,0,0;0,0,0
k6=0,-1,0;0,0,0;0,0,0;0,0,0;0,0,0;1,0,0;0,0,0
k7=-1,0,0;0,1,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k8=-1,1,0;0,1,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k9=0,2,0;0,1,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k10=0,2,0;0,1,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Коло — живий птах
only=0
weight=1
weight.h=1.2
weight.m=0.9
sit=0,0,0,1,0,0,0,0,0,0
motion=1
speed=0.94
turn=1.12
bank=1.25
glide=1.25
beat=0.9
wave=0.18
wavehz=0.4
pose=1
k1=0,2,0;0,1,0;0,0,0;-1,0,0;0,0,0;0,0,0;0,0,0
k2=1,1,0;0,1,0;0,0,0;-1,0.5,-0.5;0,-0.5,0;0,-0.5,0;0,0.5,0
k3=1,0,0;1,0,0;0,0,0;0,0.5,-1;0,-1,0;0,-1,0;0,1,0
k4=0,-1,0;0,0,0;0,0,0;1,0.5,-1.5;0,-1,0;0,-1.5,0;0,1,0
k5=-1,-1,0;0,-1,0;0,0,0;1,0,1;0,-0.5,0;0,-1,0;0,0.5,0
k6=-1,-2,0;0,-1,0;0,0,0;0,-0.5,1.5;0,0,0;0,0,0;0,0,0
k7=0,-1,0;0,0,0;0,0,0;-1,-0.5,1;0,0.5,0;0,1,0;0,-0.5,0
k8=1,0,0;0,1,0;0,0,0;-1,-0.5,0;0,1,0;0,1.5,0;0,-1,0
k9=1,1,0;1,1,0;0,0,0;-1,0,0;0,0.5,0;0,1,0;0,-0.5,0
k10=0,2,0;0,1,0;0,0,0;-1,0,0;0,0,0;0,0,0;0,0,0",
            @"name=Ниряння — складені крила
only=0
weight=1
sit=0,0,0,0,0,0,0,0,0,1
motion=1
speed=1.02
turn=1.05
bank=1.1
glide=0.45
beat=0.78
wave=0.08
wavehz=0.55
pose=1
k1=-7,2,0;-11,1,0;-7,0,0;4,0,-0.5;0,0,0;1,0,0;0,0,0
k2=-8,2,0;-12,1,0;-8,0,0;5,0,-0.5;0,-0.5,0;1,0,0;0,0,0
k3=-9,1,0;-14,0,0;-9,0,0;6,0,-0.5;0,-1,0;2,0,0;0,0,0
k4=-10,1,0;-15,0,0;-10,0,0;7,0,-0.5;0,-1,0;2,0,0;0,0,0
k5=-9,1,0;-14,0,0;-9,0,0;6,0,-0.5;0,-0.5,0;2,0,0;0,0,0
k6=-8,1,0;-12,0,0;-8,0,0;5,0,0;0,0,0;1,0,0;0,0,0
k7=-9,1,0;-13,0,0;-9,0,0;5,0,0;0,0,0;1,0,0;0,0,0
k8=-10,1,0;-15,0,0;-10,0,0;5,0,0;0,0,0;1,0,0;0,0,0
k9=-9,2,0;-14,1,0;-9,0,0;5,0,0;0,0,0;1,0,0;0,0,0
k10=-7,2,0;-11,1,0;-7,0,0;4,0,-0.5;0,0,0;1,0,0;0,0,0",
            @"name=Висхідний пошук
only=0
weight=1
weight.h=1.2
weight.m=0.8
sit=0,0,0,1,1,0,0,0,0,0
motion=1
speed=1.02
turn=1.08
bank=1.15
glide=0.7
beat=1.22
wave=0.85
wavehz=0.28
pose=1
k1=0,2,0;0,1,0;0,0,0;-1,0,-0.5;0,0.5,0;0,0.5,0;0,0.5,0
k2=1,1,0;0,1,0;0,0,0;-1,0,-1;0,0.5,0;1,0.5,0;0,0.5,0
k3=1,0,0;1,0,0;0,0,0;0,0,-1.5;0,0.5,0;2,0.5,0;0,0.5,0
k4=0,-1,0;0,0,0;0,0,0;1,0,0.5;0,0,0;2,0,0;0,0,0
k5=-1,-1,0;0,-1,0;0,0,0;1,0,1;0,-0.5,0;1,0,0;0,-0.5,0
k6=-1,-2,0;0,-1,0;0,0,0;0,0,1.5;0,-0.5,0;0,-0.5,0;0,-0.5,0
k7=0,-1,0;0,0,0;0,0,0;-1,0,1;0,-0.5,0;-1,-0.5,0;0,-0.5,0
k8=1,0,0;0,1,0;0,0,0;-1,0,0.5;0,-0.5,0;-1,-0.5,0;0,-0.5,0
k9=1,1,0;1,1,0;0,0,0;-1,0,0;0,0,0;0,0,0;0,0,0
k10=0,2,0;0,1,0;0,0,0;-1,0,-0.5;0,0.5,0;0,0.5,0;0,0.5,0",
            @"name=Перемінний супровід
only=0
weight=1
weight.h=0.8
weight.m=1.2
sit=0,0,1,0,0,0,0,0,0,0
motion=1
speed=0.98
turn=1.08
bank=1.12
glide=1.5
beat=1.18
wave=0.12
wavehz=0.32
pose=1
k1=0,2,0;0,1,0;0,0,0;-0.5,0,-0.5;0,0.25,0;0,0.5,0;0,0,0
k2=1,1,0;0,1,0;0,0,0;-0.5,0,-1;0,0.25,0;1,0.5,0;0,0,0
k3=1,0,0;1,0,0;0,0,0;0,0,-1.5;0,0.5,0;1.5,0.5,0;0,0.5,0
k4=0,-1,0;0,0,0;0,0,0;0,0,-1;0,0.25,0;1,0,0;0,0.5,0
k5=-1,-1,0;0,-1,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0
k6=-1,-2,0;0,-1,0;0,0,0;0,0,1;0,-0.25,0;-0.5,-0.5,0;0,-0.5,0
k7=0,-1,0;0,0,0;0,0,0;0,0,1.5;0,-0.5,0;-1,-0.5,0;0,-0.5,0
k8=1,0,0;0,1,0;0,0,0;-0.5,0,1;0,-0.25,0;-1,0,0;0,-0.5,0
k9=1,1,0;1,1,0;0,0,0;-0.5,0,0.5;0,-0.25,0;-0.5,0,0;0,0,0
k10=0,2,0;0,1,0;0,0,0;-0.5,0,-0.5;0,0.25,0;0,0.5,0;0,0,0",
        };

        internal static readonly List<FlightTemplate> All = new List<FlightTemplate>();
        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "templates"); } }

        internal static FlightTemplate Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (FlightTemplate t in All) if (t.Name == name) return t;
            return null;
        }

        internal static void Load()
        {
            All.Clear();
            foreach (string text in Builtin) { FlightTemplate t = FromText(text); if (t != null) { t.Builtin = true; All.Add(t); } }
            try
            {
                if (Directory.Exists(Dir))
                    foreach (string f in Directory.GetFiles(Dir, "*.txt"))
                    {
                        FlightTemplate t = FromText(File.ReadAllText(f, Encoding.UTF8));
                        if (t == null) continue;
                        FlightTemplate had = Find(t.Name);
                        if (had != null) { t.Builtin = had.Builtin; All[All.IndexOf(had)] = t; } else All.Add(t);
                    }
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Flight templates unreadable: " + e.Message); }
            if (Plugin.Instance != null && All.Count > 0) Plugin.Instance.Log("Flight templates: " + All.Count);
        }

        private static string FileOf(string name)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in name) sb.Append(System.Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
            return Path.Combine(Dir, sb.ToString().Trim() + ".txt");
        }

        internal static bool Save(FlightTemplate t)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FileOf(t.Name), ToText(t), new UTF8Encoding(false));
                if (Plugin.Instance != null) Plugin.Instance.Log("Flight template saved: " + t.Name + " -> " + FileOf(t.Name));
                return true;
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Flight template not saved: " + e.Message); return false; }
        }

        // a file renamed with the template: the old file goes
        internal static void Rename(FlightTemplate t, string name)
        {
            string old = FileOf(t.Name);
            bool diveTest = DiveTestName == t.Name;
            t.Name = name;
            if (diveTest) DiveTestName = name;
            try { if (File.Exists(old) && old != FileOf(name)) File.Delete(old); } catch { }
        }

        internal static void Delete(FlightTemplate t)
        {
            if (DiveTestName == t.Name) DiveTestName = "";
            try { string f = FileOf(t.Name); if (File.Exists(f)) File.Delete(f); } catch { }
            int at = All.IndexOf(t);
            All.Remove(t);
            if (!t.Builtin) return;
            // a built-in one comes back as built, in its place (only your changes are gone; the rest stays as it is, saved or not)
            foreach (string text in Builtin)
            {
                FlightTemplate b = FromText(text);
                if (b == null || b.Name != t.Name) continue;
                b.Builtin = true;
                All.Insert(Mathf.Clamp(at, 0, All.Count), b);
                break;
            }
        }

        internal static string ToText(FlightTemplate t)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.Append("name=").Append(t.Name).Append('\n');
            sb.Append("only=").Append(t.Only).Append('\n');
            sb.Append("weight=").Append(t.Weight.ToString("0.###", c)).Append('\n');
            sb.Append("weight.h=").Append(t.WeightFor(false).ToString("0.###", c)).Append("\nweight.m=").Append(t.WeightFor(true).ToString("0.###", c)).Append('\n');
            sb.Append("sit=");
            for (int i = 0; i < SitCount; i++) sb.Append(i > 0 ? "," : "").Append(t.Allowed[i] ? '1' : '0');
            sb.Append('\n');
            sb.Append("motion=").Append(t.UseMotion ? 1 : 0).Append('\n');
            sb.Append("speed=").Append(t.Speed.ToString("0.###", c)).Append("\nturn=").Append(t.Turn.ToString("0.###", c)).Append("\nbank=").Append(t.Bank.ToString("0.###", c))
              .Append("\nglide=").Append(t.Glide.ToString("0.###", c)).Append("\nbeat=").Append(t.Beat.ToString("0.###", c))
              .Append("\nwave=").Append(t.Wave.ToString("0.###", c)).Append("\nwavehz=").Append(t.WaveHz.ToString("0.###", c)).Append('\n');
            sb.Append("pose=").Append(t.UsePose ? 1 : 0).Append('\n');
            for (int k = 0; k < WingKeys.Keys; k++)
            {
                sb.Append('k').Append(k + 1).Append('=');
                for (int b = 0; b < FlightTemplate.PoseBones; b++)
                {
                    Vector3 v = t.Pose[k, b];
                    sb.Append(b > 0 ? ";" : "").Append(v.x.ToString("0.#", c)).Append(',').Append(v.y.ToString("0.#", c)).Append(',').Append(v.z.ToString("0.#", c));
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        internal static FlightTemplate FromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            CultureInfo c = CultureInfo.InvariantCulture;
            FlightTemplate t = new FlightTemplate();
            bool named = false;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0 || raw.TrimStart().StartsWith("#")) continue;
                string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                float f;
                bool num = float.TryParse(v, NumberStyles.Float, c, out f);
                switch (k)
                {
                    case "name": t.Name = v; named = v.Length > 0; break;
                    case "only": if (num) t.Only = Mathf.Clamp((int)f, 0, 2); break;
                    case "weight": if (num) t.Weight = f; break;
                    case "weight.h": if (num) t.WeightH = f; break;
                    case "weight.m": if (num) t.WeightM = f; break;
                    case "sit": { string[] p = v.Split(','); for (int i = 0; i < SitCount && i < p.Length; i++) t.Allowed[i] = p[i].Trim() == "1"; break; }
                    case "motion": t.UseMotion = v == "1"; break;
                    case "pose": t.UsePose = v == "1"; break;
                    case "speed": if (num) t.Speed = f; break;
                    case "turn": if (num) t.Turn = f; break;
                    case "bank": if (num) t.Bank = f; break;
                    case "glide": if (num) t.Glide = f; break;
                    case "beat": if (num) t.Beat = f; break;
                    case "wave": if (num) t.Wave = f; break;
                    case "wavehz": if (num) t.WaveHz = f; break;
                    default:
                        if (k.Length >= 2 && k[0] == 'k')
                        {
                            int key;
                            if (!int.TryParse(k.Substring(1), out key) || key < 1 || key > WingKeys.Keys) break;
                            string[] bones = v.Split(';');
                            for (int b = 0; b < FlightTemplate.PoseBones && b < bones.Length; b++)
                            {
                                string[] xyz = bones[b].Split(',');
                                float x, y, z;
                                if (xyz.Length == 3 && float.TryParse(xyz[0], NumberStyles.Float, c, out x) && float.TryParse(xyz[1], NumberStyles.Float, c, out y) && float.TryParse(xyz[2], NumberStyles.Float, c, out z))
                                    t.Pose[key - 1, b] = new Vector3(x, y, z);
                            }
                        }
                        break;
                }
            }
            return named ? t : null;
        }

        // where it is flying, from the motor's plan (the final approach and the landing: no template, kept clean)
        internal static int SitOf(RavenMotor m)
        {
            if (m == null || m.Body == null || m.Body.Final) return -1;
            if (m.Diving) return (int)FlightSit.Dive; // (with a margin and a hold: RavenMotor.TrackDive)
            switch (m.Plan)
            {
                case "удар": return (int)FlightSit.Attack;
                case "додому": return m.Body.Age < m.Body.LaunchDuration ? (int)FlightSit.TakeOff : (int)FlightSit.Home;
                case "слідом": case "за тобою": return m.Body.Age < m.Body.LaunchDuration ? (int)FlightSit.TakeOff : (int)FlightSit.Follow;
                case "догонялки": return m.Body.Age < m.Body.LaunchDuration ? (int)FlightSit.TakeOff : (int)FlightSit.Circle; // (flown as a ring: the circle's ways of flying)
                case "коло": case "розвідка": return m.Body.Age < m.Body.LaunchDuration ? (int)FlightSit.TakeOff : m.Plan == "коло" ? (int)FlightSit.Circle : (int)FlightSit.Scout;
                case "купол": return m.Body.Age < m.Body.LaunchDuration ? (int)FlightSit.TakeOff : (int)FlightSit.Dome;
                case "танець": return m.Body.Age < m.Body.LaunchDuration ? (int)FlightSit.TakeOff : (int)FlightSit.Hover;
                case "відлітає": return (int)FlightSit.TakeOff;
                case "приліт": return (int)FlightSit.Arrive;
                case "пікірування": return m.Body.Vel.y < -0.5f ? (int)FlightSit.Dive : m.Body.Age < m.Body.LaunchDuration ? (int)FlightSit.TakeOff : (int)FlightSit.Attack;
                case "на жердину": return m.Body.Age < m.Body.LaunchDuration ? (int)FlightSit.TakeOff : (int)FlightSit.Home; // a perch (Perches): like flying home
                case "перегляд": return (int)PreviewSit;
                // the orders' figures fly their own way: no flight templates on them (the commanded dive above keeps its own)
                default: return -1;
            }
        }

        // a template for this situation, by weight (or none: plain flight); never 'except' unless it is the only one
        internal static FlightTemplate Pick(int sit, bool munin, FlightTemplate except)
        {
            if (!On || sit < 0) return null;
            float noneWeight = sit == (int)FlightSit.Dive ? 0f : NoneWeight;
            float total = noneWeight;
            foreach (FlightTemplate t in All) if (t != except && t.Allowed[sit] && t.For(munin)) total += t.WeightFor(munin);
            float r = Random.value * total;
            if (r < noneWeight) return null;
            r -= noneWeight;
            foreach (FlightTemplate t in All)
            {
                if (t == except || !t.Allowed[sit] || !t.For(munin)) continue;
                r -= t.WeightFor(munin);
                if (r <= 0f) return t;
            }
            return null;
        }
    }

    // One raven's template now: the one it comes from and the one it goes to, eased in. A new pick waits for a
    // stroke's top (ShoulderLife) so the wings never jump; with no stroke (a glide) it comes in after a second.
    internal sealed class TemplateMix
    {
        internal FlightTemplate From, To;
        internal float W = 1f;
        private FlightTemplate _pending;
        private bool _hasPending;
        private float _pendingAge; // its own time waiting (a slowed picture waits longer too)

        internal bool Active { get { return From != null || To != null || _hasPending; } }
        internal bool Waiting { get { return _hasPending; } }
        internal bool Due { get { return _hasPending && _pendingAge > 1f; } }
        internal FlightTemplate Wanted { get { return _hasPending ? _pending : To; } }

        internal void Request(FlightTemplate t)
        {
            if (_hasPending ? t == _pending : t == To) return;
            _pending = t; _hasPending = true; _pendingAge = 0f;
        }

        internal void Commit()
        {
            if (!_hasPending) return;
            From = W >= 0.5f ? To : From; // mid-blend: from whichever shows more
            To = _pending; W = 0f; _hasPending = false;
            if (From == To) W = 1f;
        }

        internal void Clear() { From = To = _pending = null; _hasPending = false; W = 1f; }

        internal void Tick(float dt) { if (_hasPending) _pendingAge += dt; if (W < 1f) W = Mathf.Min(1f, W + dt / Mathf.Max(0.05f, FlightTemplates.BlendSeconds)); }

        private float E { get { return W * W * (3f - 2f * W); } }

        internal Vector3 PoseAt(float x, int bone)
        {
            Vector3 a = From != null && From.UsePose ? From.At(x, bone) : Vector3.zero;
            Vector3 b = To != null && To.UsePose ? To.At(x, bone) : Vector3.zero;
            return Vector3.Lerp(a, b, E);
        }

        private float Mix(float from, float to, bool fromOn, bool toOn, float plain)
        {
            return Mathf.Lerp(From != null && fromOn ? from : plain, To != null && toOn ? to : plain, E);
        }
        internal float Speed { get { return Mix(From != null ? From.Speed : 1f, To != null ? To.Speed : 1f, From != null && From.UseMotion, To != null && To.UseMotion, 1f); } }
        internal float Turn { get { return Mix(From != null ? From.Turn : 1f, To != null ? To.Turn : 1f, From != null && From.UseMotion, To != null && To.UseMotion, 1f); } }
        internal float Bank { get { return Mix(From != null ? From.Bank : 1f, To != null ? To.Bank : 1f, From != null && From.UseMotion, To != null && To.UseMotion, 1f); } }
        internal float Glide { get { return Mix(From != null ? From.Glide : 1f, To != null ? To.Glide : 1f, From != null && From.UseMotion, To != null && To.UseMotion, 1f); } }
        internal float Beat { get { return Mix(From != null ? From.Beat : 1f, To != null ? To.Beat : 1f, From != null && From.UseMotion, To != null && To.UseMotion, 1f); } }
        internal float Wave { get { return Mix(From != null ? From.Wave : 0f, To != null ? To.Wave : 0f, From != null && From.UseMotion, To != null && To.UseMotion, 0f); } }
        internal float WaveHz { get { return Mix(From != null ? From.WaveHz : 0.5f, To != null ? To.WaveHz : 0.5f, From != null && From.UseMotion, To != null && To.UseMotion, 0.5f); } }

        internal string Describe() { return (To != null ? To.Name : "без шаблону") + (W < 1f ? " (перехід " + Mathf.RoundToInt(W * 100f) + "%)" : "") + (_hasPending ? " → " + (_pending != null ? _pending.Name : "без шаблону") : ""); }
    }
}
#endif
