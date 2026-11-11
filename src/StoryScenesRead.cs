#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (StoryScenes, its head says all), reading: the files into scenes and steps, and the places they name checked.
    internal static partial class StoryScenes
    {
        // ---------------------------------------------------------------- reading
        internal static void Load() { Wire(); if (!_loaded) Reload(); }

        internal static void Reload()
        {
            _loaded = true;
            StoryTrackConds.Register(); // (the conditions and the modules' actions known before any «if»/«do» line is checked)
            Scenes.Clear(); Errors.Clear();
            foreach (KeyValuePair<string, string> kv in StoryFiles.All(StoryFiles.Scenes, Folder, Blueprint.On)) Parse(kv.Key, kv.Value); // (built in; the mod's own folder on the author's machine — StoryFiles)
            foreach (SceneDef sc in Scenes) CheckPlaces(sc);
            foreach (string e in Errors) Story.Log("scenes: " + e);
            StoryShots.Again(); AudioBank.Again(); // (the author's shots and the story's sounds: from their files again too)
            Story.Log("scenes: " + Scenes.Count + " loaded" + (Errors.Count > 0 ? ", " + Errors.Count + " problems" : ""));
        }

        private static void Parse(string file, string text)
        {
            SceneDef s = null;
            foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(text))
            {
                string line = ln.Value, at = file + ":" + ln.Key + ": ";
                if (line.StartsWith("[scene ") && line.EndsWith("]"))
                {
                    string id = line.Substring(7, line.Length - 8).Trim();
                    if (!Story.GoodName(id)) { Errors.Add(at + "погана назва сцени"); s = null; continue; }
                    if (Find0(id) != null) { Errors.Add(at + "сцена «" + id + "» уже є — ця пропущена"); s = null; continue; }
                    s = new SceneDef { Id = id, From = file }; Scenes.Add(s);
                    continue;
                }
                if (s == null) { Errors.Add(at + "рядок поза сценою"); continue; }
                if (line.StartsWith("roams:")) { string v = line.Substring(6).Trim(); s.Roams = v == "yes" || v == "так" || v == "1"; continue; }
                if (line.StartsWith("alone:")) { string v = line.Substring(6).Trim(); s.Alone = v == "yes" || v == "так" || v == "1"; continue; }
                if (line.StartsWith("needs:")) { s.NeedsText = line.Substring(6).Trim(); s.Needs = Conds(s.NeedsText, at); continue; }
                if (line.StartsWith("check:")) { s.Check = line.Substring(6).Trim(); continue; } // (where the studio's check begins it: StudioScene)
                if (line.StartsWith("test:")) { s.Test = s.Test.Length > 0 ? s.Test + "; " + line.Substring(5).Trim() : line.Substring(5).Trim(); continue; } // (the world the studio's check plays it in: StudioScene)
                if (line.StartsWith("place:")) { s.Place = line.Substring(6).Trim(); continue; } // (where it is filmed: its shots are that place's — the window «Режисер»)
                if (line.StartsWith("guard:"))
                {
                    string g = line.Substring(6).Trim();
                    if (g == "off") s.Guard = 0f;
                    else { float r = F(g, -1f); if (r > 0f && r <= 80f) s.Guard = r; else Errors.Add(at + "guard: <метрів 1–80> | off"); }
                    continue;
                }
                Step st = new Step { Line = line, Text = line, Num = ln.Key };
                if (line.StartsWith("if "))
                {
                    int c = line.IndexOf(':');
                    if (c < 0) { Errors.Add(at + "if <умови>: <крок>"); continue; }
                    st.If = Conds(line.Substring(3, c - 3), at);
                    st.Text = line.Substring(c + 1).Trim();
                }
                st.W = st.Text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                string why = Check(st);
                if (why != null) { Errors.Add(at + why); continue; }
                if (st.W[0] == "npc" && st.W.Length > 2 && !st.Kind.NotCast && !s.Cast.Contains(st.W[1]) && !Spawned(s, st.W[1])) s.Cast.Add(st.W[1]); // (wanted from the world; its own «spawn» — the scene makes it: not waited for)
                if (st.W[0] == "until") { int bar = st.Text.LastIndexOf('|'); st.Until = StoryCond.Parse(st.Text.Substring(5, bar - 5), null); st.UntilSec = F(st.Text.Substring(bar + 1).Trim(), 10f); }
                s.Steps.Add(st);
            }
        }

        // the places a scene's steps name (a typo: the step skipped in the game, said only in the log)
        private static void CheckPlaces(SceneDef s)
        {
            string at = s.From + " [" + s.Id + "]: ";
            if (s.Place.Length > 0 && StoryWhere.Why(s.Place) != null) Errors.Add(at + "place — " + StoryWhere.Why(s.Place));
            PAnimClip was = null; // (two clips one after the other: is the join seen? said in the log only)
            foreach (Step st in s.Steps)
            {
                string[] w = st.W;
                if (w[0] == "player" && w.Length > 2 && w[1] == "clip")
                {
                    PAnimClip now = BodyClips.Find(w[2]);
                    string ch;
                    if (was != null && now != null && BodyClips.JoinGap(was, now, out ch) > BodyClips.JoinNear) Story.Log("scene " + s.Id + ":" + st.Num + ": the join " + was.Name + " > " + now.Name + " shows (" + ch + ")");
                    was = now;
                }
                string place = st.Kind != null ? st.Kind.PlaceWord(w) : null;
                if (place == null) continue;
                string why = StoryWhere.Why(place);
                if (why != null) Errors.Add(s.From + ":" + st.Num + ": " + why);
            }
        }

        private static bool Spawned(SceneDef s, string who)
        {
            foreach (Step o in s.Steps) if (o.W.Length > 2 && o.W[0] == "npc" && o.W[1] == who && o.W[2] == "spawn") return true;
            return false;
        }

        private static SceneDef Find0(string id) { foreach (SceneDef s in Scenes) if (s.Id == id) return s; return null; }

        private static List<Cond> Conds(string val, string at)
        {
            List<string> errs = new List<string>();
            List<Cond> l = StoryCond.Parse(val, errs);
            foreach (string e in errs) Errors.Add(at + e);
            return l;
        }
    }
}
#endif
