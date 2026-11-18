#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The story's track (StoryQuests, its head says all), reading: the files into quests, stages and ways, and what they name checked.
    internal static partial class StoryQuests
    {
        // ---------------------------------------------------------------- reading
        private static float _loadAgain;
        internal static void Load()
        {
            if (_loaded || Time.time < _loadAgain) return;
            try { Reload(); }
            catch { _loaded = false; _loadAgain = Time.time + 30f; throw; } // (a half read list must not stand for good, nor be read again every frame)
        }

        internal static void Reload()
        {
            _loaded = true;
            StoryTrackConds.Register();
            StorySaga.Reload(); // (the chapters the quests name: read anew with them)
            Quests.Clear(); Errors.Clear();
            foreach (KeyValuePair<string, string> kv in StoryFiles.All(StoryFiles.Quests, Folder, Blueprint.On)) Parse(kv.Key, kv.Value); // (built in; the mod's own folder on the author's machine — StoryFiles)
            foreach (QuestDef q in Quests) Validate(q);
            StoryScenes.Load(); // (the scenes the stages name: checked against them)
            foreach (QuestDef q in Quests) ValidateRefs(q);
            foreach (string e in Errors) Story.Log("quests: " + e);
            Story.Log("quests: " + Quests.Count + " loaded" + (Errors.Count > 0 ? ", " + Errors.Count + " problems" : ""));
            _wanted.Clear();
            _wanted.Add("scene_done");
            foreach (QuestDef q in Quests) foreach (StageDef s in q.Stages) { if (s.OnName.Length > 0) _wanted.Add(s.OnName); foreach (StageDef w in s.Ways) if (w.OnName.Length > 0) _wanted.Add(w.OnName); }
            StoryBus.Listen("story", Hear);
            Version++;
            ReadCount++;
        }

        private static void Parse(string file, string text)
        {
            QuestDef q = null; StageDef s = null, w = null; // (w: the way being read, inside s)
            foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(text))
            {
                string line = ln.Value, at = file + ":" + ln.Key + ": ";
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string[] h = line.Substring(1, line.Length - 2).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (h.Length == 2 && h[0] == "quest" && Story.GoodName(h[1]))
                    {
                        if (Find0(h[1]) != null) { Errors.Add(at + "квест «" + h[1] + "» уже є — цей пропущено"); q = null; s = null; continue; }
                        q = new QuestDef { Id = h[1], From = file, Block = StoryFiles.Block(file) }; Quests.Add(q); s = null; w = null;
                    }
                    else if (h.Length == 2 && h[0] == "stage" && q != null && Story.GoodName(h[1]))
                    {
                        if (q.Stage(h[1]) != null) Errors.Add(at + "етап «" + h[1] + "» двічі");
                        s = new StageDef { Id = h[1] }; q.Stages.Add(s); w = null;
                    }
                    else if (h.Length == 2 && h[0] == "way" && s != null && Story.GoodName(h[1]))
                    {
                        foreach (StageDef o in s.Ways) if (o.Id == h[1]) Errors.Add(at + "шлях «" + h[1] + "» двічі");
                        w = new StageDef { Id = h[1] }; s.Ways.Add(w);
                    }
                    else Errors.Add(at + "не розумію «" + line + "»");
                    continue;
                }
                int c = line.IndexOf(':');
                if (c <= 0 || q == null) { Errors.Add(at + "рядок поза квестом або без «:»"); continue; }
                string key = line.Substring(0, c).Trim().ToLowerInvariant(), val = line.Substring(c + 1).Trim();
                if (s == null)
                {
                    switch (key)
                    {
                        case "name": q.Name = val; break;
                        case "starts": q.StartsText = val; q.Starts = Conds(val, at); break;
                        case "first": q.First = val; break;
                        case "chapter": q.Chapter = val; break;
                        case "owner": if (val == "server") q.Server = true; else Errors.Add(at + "owner: server"); break;
                        case "who": q.Who = Conds(val, at); break;
                        case "scratch": if (val.Length == 0) Errors.Add(at + "scratch: <префікс> …"); else q.Scratch = val.ToLowerInvariant(); break;
                        case "fork":
                            {
                                int arrow = val.LastIndexOf("->");
                                if (arrow < 0) { Errors.Add(at + "fork: <умови> -> <етап>"); break; }
                                q.Forks.Add(new Fork { Text = val, When = Conds(val.Substring(0, arrow), at), To = val.Substring(arrow + 2).Trim() });
                                break;
                            }
                        case "nudge":
                            {
                                string[] n = val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                int minutes = n.Length == 2 ? Errands.I(n[1], -1) : -1;
                                if (minutes <= 0) { Errors.Add(at + "nudge: <сцена> <хвилин>"); break; }
                                q.NudgeScene = n[0]; q.NudgeMinutes = minutes;
                                break;
                            }
                        default: Errors.Add(at + "невідоме поле квесту «" + key + "»"); break;
                    }
                    continue;
                }
                if (w != null && key != "goal" && key != "mark" && key != "wait" && key != "on" && key != "done" && key != "next" && key != "saga")
                {
                    Errors.Add(at + "у шляху лише goal, mark, wait, on, done, next, saga — «" + key + "» тут ні до чого"); continue;
                }
                StageDef d = w ?? s; // (what the line belongs to: the way, else the stage)
                switch (key)
                {
                    case "goal": d.Goal = val; break;
                    case "more": d.More = val; break;
                    case "saga": d.Saga = val; break;
                    case "choice": if (Story.GoodName(val) && val.IndexOf('.') < 0) s.Choice = val; else Errors.Add(at + "choice: назва без крапок і пробілів"); break;
                    case "wait": d.WaitText = val; d.Wait = Conds(val, at); break;
                    case "on":
                        {
                            int sp = val.IndexOf(' ');
                            d.OnName = (sp < 0 ? val : val.Substring(0, sp)).ToLowerInvariant();
                            d.OnArgs = sp < 0 ? "" : val.Substring(sp + 1).Trim();
                            break;
                        }
                    case "scene": d.Scene = val; break;
                    case "music": d.Music = val.Trim(); if (d.Music.Length == 0) Errors.Add(at + "music: off | <трек> [vol V]"); break;
                    case "mark":
                        {
                            string[] m = val.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                            if (m.Length == 0) { Errors.Add(at + "mark: <місце> [радіус] <напис>"); break; }
                            d.MarkAt = m[0];
                            int r = m.Length > 1 ? Errands.I(m[1], -1) : -1;
                            d.MarkRadius = r > 0 ? r : 0f;
                            d.MarkLabel = r > 0 ? (m.Length > 2 ? m[2] : "") : (m.Length > 1 ? val.Substring(val.IndexOf(m[1], m[0].Length)) : "");
                            break;
                        }
                    case "next": d.Next = val; break;
                    case "call": d.Call = val; break;
                    case "seek": d.Seek = val; break;
                    case "stock": { string why = StoryStock.Why(val); if (why == null) d.Stock.Add(val.Trim()); else Errors.Add(at + why); break; }
                    case "lost": if (val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length == 2) d.Lost.Add(val.Trim()); else Errors.Add(at + "lost: <предмет> <місце>"); break;
                    case "start": Actions(val, d.Start, at); break;
                    case "done": Actions(val, d.Done, at); break;
                    case "fork":
                        {
                            int arrow = val.LastIndexOf("->");
                            if (arrow < 0) { Errors.Add(at + "fork: <умови> -> <етап>"); break; }
                            d.Forks.Add(new Fork { Text = val, When = Conds(val.Substring(0, arrow), at), To = val.Substring(arrow + 2).Trim() });
                            break;
                        }
                    default: Errors.Add(at + "невідоме поле етапу «" + key + "»"); break;
                }
            }
        }

        private static QuestDef Find0(string id) { foreach (QuestDef q in Quests) if (q.Id == id) return q; return null; }

        // conditions, their problems with the line's place in front
        private static List<Cond> Conds(string val, string at)
        {
            List<string> errs = new List<string>();
            List<Cond> l = StoryCond.Parse(val, errs);
            foreach (string e in errs) Errors.Add(at + e);
            return l;
        }

        private static void Actions(string val, List<string> into, string at)
        {
            foreach (string a in val.Split(';'))
            {
                string t = a.Trim();
                if (t.Length == 0) continue;
                string why = StoryActions.Check(t);
                if (why != null) Errors.Add(at + why);
                into.Add(t);
            }
        }

        private static void Validate(QuestDef q)
        {
            string at = q.From + " [" + q.Id + "]: ";
            if (q.Stages.Count == 0) { Errors.Add(at + "жодного етапу"); return; }
            if (q.First.Length == 0) q.First = q.Stages[0].Id;
            if (q.Stage(q.First) == null) Errors.Add(at + "first: нема етапу «" + q.First + "»");
            foreach (Fork f in q.Forks) if (f.To != "end" && q.Stage(f.To) == null) Errors.Add(at + "fork — нема етапу «" + f.To + "»");
            if (q.Server && q.NudgeScene.Length > 0) Errors.Add(at + "nudge: лише в особистому квесті");
            for (int i = 0; i < q.Stages.Count; i++)
            {
                StageDef s = q.Stages[i];
                if (s.Next.Length == 0) s.Next = i + 1 < q.Stages.Count ? q.Stages[i + 1].Id : "end";
                if (s.Next != "end" && q.Stage(s.Next) == null) Errors.Add(at + s.Id + ": next — нема етапу «" + s.Next + "»");
                foreach (Fork f in s.Forks) if (f.To != "end" && q.Stage(f.To) == null) Errors.Add(at + s.Id + ": fork — нема етапу «" + f.To + "»");
                foreach (string a in s.Start) StoryActions.CheckGoto(q, a, at + s.Id + ": ", Errors);
                foreach (string a in s.Done) StoryActions.CheckGoto(q, a, at + s.Id + ": ", Errors);
                if (s.Ways.Count > 0) { ValidateWays(q, s, at); continue; }
                if (s.Goal.Length == 0) Errors.Add(at + s.Id + ": без goal (гравець не знатиме, що робити)");
            }
            bool saga = false;
            foreach (StageDef s in q.Stages) { saga |= s.Saga.Length > 0; foreach (StageDef w in s.Ways) saga |= w.Saga.Length > 0; }
            if (saga && q.Chapter.Length == 0) Errors.Add(at + "є рядки saga:, а chapter: нема (сага не знатиме, у яку главу їх писати)");
            else if (q.Chapter.Length > 0 && !StorySaga.HasChapter(q.Chapter)) Errors.Add(at + "chapter: нема глави «" + q.Chapter + "» (assets/story/saga)");
            int unnamed = 0;
            foreach (StageDef s in q.Stages) if (s.Ways.Count > 0 && s.Choice.Length == 0) unnamed++;
            if (unnamed > 1) Errors.Add(at + "кілька етапів зі шляхами без choice: — назви кожен вибір");
        }

        // what a stage names elsewhere: its scene, its marks' places (a typo there — a scene «played» at once, a mark nowhere)
        private static void ValidateRefs(QuestDef q)
        {
            string at = q.From + " [" + q.Id + "]: ";
            if (q.NudgeScene.Length > 0 && StoryScenes.Find(q.NudgeScene) == null) Errors.Add(at + "nudge: нема сцени «" + q.NudgeScene + "»");
            foreach (StageDef s in q.Stages)
            {
                if (s.Scene.Length > 0 && StoryScenes.Find(s.Scene) == null) Errors.Add(at + s.Id + ": нема сцени «" + s.Scene + "» (зіграється одразу, нічого не показавши)");
                if (s.Ways.Count == 0 && s.Scene.Length > 0 && s.OnName.Length > 0) Errors.Add(at + s.Id + ": і scene, і on — етап зі сценою проходить лише її кінцем, а його on забирав би E у речі (дошка серця, розмова) й нічого не робив; подію винеси в окремий етап перед сценою"); // (Passed: scene_done alone; Awaiting would still answer for its «on:»)
                foreach (StageDef d in s.Ways.Count > 0 ? s.Ways : new List<StageDef> { s })
                {
                    string why = d.MarkAt.Length > 0 ? StoryWhere.Why(d.MarkAt) : null;
                    if (why != null) Errors.Add(at + d.Id + ": mark — " + why);
                }
            }
        }

        private static void ValidateWays(QuestDef q, StageDef s, string at)
        {
            at += s.Id + ": ";
            if (s.Scene.Length > 0 || s.OnName.Length > 0 || (s.Wait != null && s.Wait.Count > 0)) Errors.Add(at + "етап зі шляхами чекає лише на шляхи — wait/on/scene перенеси в шлях");
            if (s.Ways.Count > MaxWays) Errors.Add(at + "шляхів більше " + MaxWays);
            foreach (StageDef w in s.Ways)
            {
                if (w.Next.Length == 0) w.Next = s.Next;
                if (w.Next != "end" && q.Stage(w.Next) == null) Errors.Add(at + "шлях " + w.Id + ": next — нема етапу «" + w.Next + "»");
                if (w.Goal.Length == 0) Errors.Add(at + "шлях " + w.Id + ": без goal");
                if (w.OnName.Length == 0 && (w.Wait == null || w.Wait.Count == 0)) Errors.Add(at + "шлях " + w.Id + ": без wait/on — його виберуть одразу");
                foreach (string a in w.Done) StoryActions.CheckGoto(q, a, at + "шлях " + w.Id + ": ", Errors);
            }
        }

        internal const int MaxWays = 6;
        internal const string ChoseFlag = "chose.";

        // the name a stage's choice is kept under (its own, else the quest's)
        internal static string ChoiceOf(QuestDef q, StageDef s) { return s.Choice.Length > 0 ? s.Choice : q.Id; }
    }
}
#endif
