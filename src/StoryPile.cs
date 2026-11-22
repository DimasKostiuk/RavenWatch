#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // What a story person laid down in its own life and left (the user 2026-11-22: Eyvind carries planks to the wall — «лежить купою біля стіни і
    // зникає згодом»). A person holds one thing at a time (StoryProps: one code on its ZDO), so the plank it let fall cannot stay its thing while it
    // carries the next; at the end of that visit it is taken off its hands (StoryLife.HaulLaid) and kept on here as a look only — a pile, a few
    // things at most, each for a while.
    //   kept     in the person's locker (NpcLocker, KPile): «item|size|x,y,z,rx,ry,rz|born|until;…» — world seconds, the place where it lay; written
    //            only by the game that runs the person (Add), and only as a thing is added. Nothing else is sent: every game near reads it and draws it
    //   Add      where the thing lay (StoryProps.Lain: its rest), a little aside and askew (from its own moment: kept in the line, so the same on
    //            every game); more than Most — the oldest goes now
    //   drawn    HandLook's copy of the item, no collider, nothing to pick up. How high each lies is reckoned where it is drawn, from what lies
    //            under it now (the things of the line laid before it within Near m: Rise m each) — so a second pile by another wall starts on the
    //            floor, and a thing whose lower one is gone comes down. It settles there (Settle s), lies, and in its last Sink s sinks and shrinks
    //            away. Looked at every Every s a person (its locker's line; a change: the looks made anew), moved each frame only while one
    //            settles, comes down or goes. Its person out of sight (unloaded): the looks go with it, and come back with it while their time lasts
    // Not a thing of the world: no ZDO of its own, no item — a player cannot take it, a save keeps only the locker's line. A place's state changed
    // under it (the Hall rebuilt): it lies on till its time is out.
    internal static class StoryPile
    {
        internal static readonly int KPile = "RavenWatch_npcPile".GetStableHashCode(); // string, in the person's locker
        internal static float Rise = 0.07f, Near = 1f, Aside = 0.06f, Askew = 7f, Settle = 0.4f, Sink = 3f, SinkDeep = 0.25f, Every = 0.25f; // m a thing lies over the one under it · m round it that is its pile · m and degrees it lies off where it fell · s it settles · s it goes · m it sinks going · s between looks at the locker
        internal const int Most = 6;

        private sealed class Lain
        {
            public string Item = ""; public float Size; public Vector3 At; public Quaternion Turn = Quaternion.identity; public double Born, Until;
            public GameObject Look; public Vector3 Scale = Vector3.one;
            public float Lift, Want; public bool Still; // (how high it is drawn now · how high it is to lie · lying still: not touched)
        }
        private sealed class View { public float Next; public string Text = ""; public readonly List<Lain> All = new List<Lain>(); }
        private static readonly Dictionary<NpcActor, View> Views = new Dictionary<NpcActor, View>(); // session: the looks of this game

        internal static void Wire()
        {
            NpcActor.Everywhere.Add(Draw);
            NpcActor.Unloaded.Add(Forget);
        }

        private static double Now { get { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0; } }
        private static string Stamp(double t) { return t.ToString("0.#", CultureInfo.InvariantCulture); }

        // one more thing on its pile, where it lay; for s seconds. Where the person is run
        internal static void Add(NpcActor a, string item, float size, Vector3 at, Quaternion turn, float seconds)
        {
            if (a == null || a.Zdo == null || !a.Owned) return;
            string kept = NpcLocker.Get(a, KPile);
            if (kept == null) return; // (its locker not here yet: this one is not kept)
            double now = Now;
            List<Lain> live = new List<Lain>();
            foreach (string e in kept.Split(';')) { Lain l = Read(e); if (l != null && l.Until > now) live.Add(l); }
            live.Sort((x, y) => x.Born.CompareTo(y.Born));
            for (int i = 0; i + Most <= live.Count; i++) if (live[i].Until > now + Sink) live[i].Until = now + Sink; // (the oldest go now: room for this one)
            uint h = (uint)Mathf.FloorToInt((float)(now % 100000.0) * 10f) * 2654435761u; // (its own moment: the line keeps the place itself, so every game draws the same)
            float side = ((h >> 8 & 0xFF) / 255f - 0.5f) * 2f * Aside, yaw = ((h >> 16 & 0xFF) / 255f - 0.5f) * 2f * Askew;
            live.Add(new Lain { Item = item, Size = size, At = at + turn * Vector3.right * side, Turn = Quaternion.Euler(0f, yaw, 0f) * turn, Born = now, Until = now + Mathf.Max(Sink + 1f, seconds) });
            System.Text.StringBuilder t = new System.Text.StringBuilder();
            foreach (Lain l in live) t.Append(Write(l)).Append(';');
            NpcLocker.Set(a, KPile, t.ToString());
            View v;
            if (Views.TryGetValue(a, out v)) v.Next = 0f; // (drawn here at this game's next frame: the thing just left its hands)
            Story.Log("pile: " + a.Id + " lays " + item + " on its pile (" + live.Count + " lying), for " + Mathf.RoundToInt(seconds) + " s");
        }

        private static string Write(Lain l)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            Vector3 e = l.Turn.eulerAngles;
            return l.Item + "|" + l.Size.ToString("0.###", ci) + "|" + l.At.x.ToString("0.###", ci) + "," + l.At.y.ToString("0.###", ci) + "," + l.At.z.ToString("0.###", ci) + "," + e.x.ToString("0.#", ci) + "," + e.y.ToString("0.#", ci) + "," + e.z.ToString("0.#", ci)
                + "|" + Stamp(l.Born) + "|" + Stamp(l.Until);
        }

        private static Lain Read(string code)
        {
            string[] f = (code ?? "").Split('|');
            if (f.Length < 5) return null;
            CultureInfo ci = CultureInfo.InvariantCulture;
            Lain l = new Lain { Item = f[0] };
            string[] p = f[2].Split(',');
            float[] v = new float[6];
            if (p.Length < 6) return null;
            for (int i = 0; i < 6; i++) if (!float.TryParse(p[i], NumberStyles.Float, ci, out v[i])) return null;
            if (!float.TryParse(f[1], NumberStyles.Float, ci, out l.Size) || !double.TryParse(f[3], NumberStyles.Float, ci, out l.Born) || !double.TryParse(f[4], NumberStyles.Float, ci, out l.Until)) return null;
            l.At = new Vector3(v[0], v[1], v[2]); l.Turn = Quaternion.Euler(v[3], v[4], v[5]);
            return l;
        }

        private static void Forget(NpcActor a)
        {
            View v;
            if (!Views.TryGetValue(a, out v)) return;
            foreach (Lain l in v.All) if (l.Look != null) UnityEngine.Object.Destroy(l.Look);
            Views.Remove(a);
        }

        // every game, each frame a person: nothing kept and nothing drawn — a look at its locker's line every Every s, no more
        private static void Draw(NpcActor a)
        {
            View v;
            if (!Views.TryGetValue(a, out v))
            {
                if (a.Zdo == null || !StoryPeople.Wears(a.Id)) return; // (only the story's people lay things down in their own life)
                Views[a] = v = new View { Next = Time.time + UnityEngine.Random.Range(0f, Every) };
            }
            if (Time.time >= v.Next)
            {
                v.Next = Time.time + Every;
                string text = NpcLocker.Text(a.Zdo, KPile);
                if (text != v.Text) { v.Text = text; Remake(v); }
            }
            if (v.All.Count == 0) return;
            double now = Now;
            bool gone = false;
            for (int i = v.All.Count - 1; i >= 0; i--)
            {
                Lain l = v.All[i];
                if (now >= l.Until) { UnityEngine.Object.Destroy(l.Look); v.All.RemoveAt(i); gone = true; continue; }
                float left = Sink > 0f ? Mathf.Clamp01((float)(l.Until - now) / Sink) : 1f;
                if (l.Still && left >= 1f) continue;
                l.Lift = Settle > 0f ? Mathf.MoveTowards(l.Lift, l.Want, Mathf.Max(Rise, Mathf.Abs(l.Want - l.Lift)) / Settle * Time.deltaTime) : l.Want;
                float k = Mathf.SmoothStep(0f, 1f, left);
                l.Look.transform.position = l.At + Vector3.up * (l.Lift - SinkDeep * (1f - k));
                l.Look.transform.localScale = l.Scale * k;
                l.Still = left >= 1f && l.Lift == l.Want;
            }
            if (gone) Heights(v, false);
        }

        // how high each is to lie: Rise over every thing of the line laid before it that lies within Near m of it. fresh: the looks just made —
        // one laid a moment ago settles up from where it fell, one that lay there already (its person back in sight) is put where it lies
        private static void Heights(View v, bool fresh)
        {
            double now = Now;
            foreach (Lain l in v.All)
            {
                int under = 0;
                foreach (Lain o in v.All) if (o != l && o.Born < l.Born && (o.At - l.At).sqrMagnitude <= Near * Near) under++; // (where each fell — on the floor, whatever lies there: not its drawn height)
                float want = under * Rise;
                if (fresh && l.Lift < 0f) l.Lift = now - l.Born < 2.0 ? 0f : want; // (made now: Remake marks it with -1)
                if (want != l.Want || l.Lift != want) l.Still = false;
                l.Want = want;
            }
        }

        // its line changed: the looks of what is no longer in it go, what is new is made (a thing is known by what it is and when it was laid)
        private static void Remake(View v)
        {
            Dictionary<string, Lain> was = new Dictionary<string, Lain>();
            foreach (Lain l in v.All) was[l.Item + "|" + Stamp(l.Born)] = l;
            v.All.Clear();
            double now = Now;
            foreach (string e in v.Text.Split(';'))
            {
                Lain l = Read(e);
                if (l == null || l.Until <= now) continue;
                string key = l.Item + "|" + Stamp(l.Born);
                Lain old;
                if (was.TryGetValue(key, out old)) { old.Until = l.Until; was.Remove(key); v.All.Add(old); continue; } // (the same thing, its end maybe sooner now)
                l.Look = HandLook.Make(l.Item, l.Size); // heavy: once as a thing comes onto the pile, or as its person comes into sight
                if (l.Look == null) { Story.Log("pile: no look for " + l.Item); continue; }
                l.Scale = l.Look.transform.localScale; l.Lift = -1f;
                l.Look.transform.SetPositionAndRotation(l.At, l.Turn);
                v.All.Add(l);
            }
            foreach (Lain gone in was.Values) if (gone.Look != null) UnityEngine.Object.Destroy(gone.Look);
            Heights(v, true);
        }
    }
}
#endif
