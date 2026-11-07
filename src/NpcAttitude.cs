#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // How a person feels about each player (npc-plan.md, «ставлення»): −100…100 per player, kept in its locker. Words only
    // (the user): its greeting, «Що думаєш про мене?», «Як справи?», «Поговорімо» — from cold to dear. What moves it: what
    // it remembers you did (NpcMemory.Kinds Toward: you saved it, brought it round, took it in, brought what it asked, sent it off),
    // a little each day it sees you (no blow of yours: a player's blows never reach the people); left alone it drifts back to even. Hunger and pay do not touch it (the
    // user: those weigh on its mood, not on you).
    //   levels   hostile ≤ −50 · cold < −15 · even · warm ≥ 25 · close ≥ 60
    //   its words   a theme's own for the level is tried first: see_player_cold, about_you_close, how_hostile… (Variant)
    //   the story   ?npcattitude >= 50 [npc]  ·  !attitude +10  ·  NpcAttitude.Add / Of / Level
    //   quarrels    one of its people did it wrong (Quarrel: the last food — NpcMeals, its own place taken — NpcPlaces, idling
    //               near while it works and a bully teasing it — Tick here, words at a table — NpcChatter, hid while it fought — NpcLife, paid when it
    //               was not — NpcToil): colder to that one, and it keeps it in mind (NpcMemory quarrel_<why>); once a game day a
    //               pair at most, harder for two who get on badly (Fit). Made up the way they got on: talks warm them again
    // Written only where the person is owned (Add goes there); read anywhere its locker is.
    internal static class NpcAttitude
    {
        internal static readonly int KAttitude = "RavenWatch_npcAttitude".GetStableHashCode(); // string "player=value@day;…" — in its locker

        private const int Most = 8, MostPeople = 12;    // players kept · other people kept (in each, the one nearest even goes first)
        private const float Seen = 2f, SeenUpTo = 30f;  // a day it sees you: a little warmer, up to this
        private const int DriftAfter = 5;               // days unseen before it drifts back (a point a day)

        private static object _touched;
        private sealed class Feel { public long Player; public float Value; public long Day; }

        // ---------------------------------------------------------------- reading (any game)
        internal static float Of(NpcActor a, long player)
        {
            List<Feel> l = Read(a);
            if (l == null) return 0f;
            Feel f = l.Find(x => x.Player == player);
            return f != null ? Drifted(f) : 0f;
        }

        // ---------------------------------------------------------------- to another person (the user 2026-11-05: they remember each other)
        // kept as a player's is, under the other one's own key — below zero, from its seed (NpcKit.Seed: the same person after a restart,
        // on every game; a player's id is never below zero). The same drift back when they do not meet, the same levels. Two people get on
        // only so far (Fit: from both seeds, the same both ways and after a restart): a talk warms them up to it, never past — so not every
        // neighbour ends up dear, and one it gets on with best stands out. Kept apart from the players' (MostPeople): a crowded base does not
        // push a player out of its memory.
        internal static float PairLeast = 0f, PairMost = 80f; // how far a talk may warm two people: the worst-matched pair · the best
        internal static long PersonKey(NpcActor o) { return o != null && o.Zdo != null ? -1L - NpcKit.Seed(o.Zdo) : 0L; }
        internal static float OfPerson(NpcActor a, NpcActor o) { long k = PersonKey(o); return k != 0L ? Of(a, k) : 0f; }
        internal static void TellPerson(NpcActor a, NpcActor o, float by) { long k = PersonKey(o); if (k != 0L) Tell(a, k, by, Fit(a, o)); }

        // how well these two get on: as far as talks may warm them (PairLeast … PairMost)
        internal static float Fit(NpcActor a, NpcActor o)
        {
            if (a == null || o == null || a.Zdo == null || o.Zdo == null) return PairLeast;
            uint s1 = NpcKit.Seed(a.Zdo), s2 = NpcKit.Seed(o.Zdo), lo = s1 < s2 ? s1 : s2, hi = s1 < s2 ? s2 : s1;
            uint h = lo * 2654435761u ^ hi * 2246822519u; h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12;
            return Mathf.Clamp(Mathf.Lerp(PairLeast, PairMost, (h % 1000u) / 999f) + NpcNature.Match(a, o) * (PairMost - PairLeast), PairLeast, PairMost); // (their natures: NpcNature)
        }

        // ---------------------------------------------------------------- a quarrel (the user 2026-11-05: what they do to each other cools them too)
        internal static readonly int KQuarrel = "RavenWatch_npcQuarrel".GetStableHashCode(); // string "personKey=day;…": the last day it quarrelled with whom — in its locker
        internal static float QuarrelBy = 10f; // points colder a quarrel, for a pair that gets on middling (×1.5 the worst matched … ×0.5 the best)
        private const int QuarrelKeep = 12;

        // from any game: «a» is wronged by «o» (why: food · place · work · table · raid · pay) — sent to the game that runs «a»
        internal static void Quarrel(NpcActor a, NpcActor o, string why)
        {
            if (a == null || o == null || a == o || o.Zdo == null || string.IsNullOrEmpty(why)) return;
            a.Act("quarrel " + NpcKit.Ref(o.Zdo.m_uid) + " " + why); // session: the other one by its id now, read at once there
        }

        // «quarrel <other> <why>» (NpcActor.Acts), where «a» is run
        internal static void QuarrelAct(NpcActor a, string[] w)
        {
            if (w.Length < 3 || a == null || !a.Owned) return;
            GameObject go = NpcKit.Find(NpcKit.Parse(w[1]));
            NpcActor o = go != null ? go.GetComponent<NpcActor>() : null;
            if (o == null || o == a) return;
            string kept = NpcLocker.Get(a, KQuarrel);
            if (kept == null) return; // (its locker not here yet: let it be)
            long key = PersonKey(o), today = Today();
            Dictionary<long, long> days = new Dictionary<long, long>();
            foreach (string e in kept.Split(';')) { int eq = e.IndexOf('='); long k, d; if (eq > 0 && long.TryParse(e.Substring(0, eq), NumberStyles.Integer, CultureInfo.InvariantCulture, out k) && long.TryParse(e.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out d)) days[k] = d; }
            long last;
            if (days.TryGetValue(key, out last) && last == today) return; // (once a day a pair: a quarrel, not a feud by the minute)
            days[key] = today;
            while (days.Count > QuarrelKeep) { long old = long.MaxValue, drop = 0L; foreach (KeyValuePair<long, long> kv in days) if (kv.Key != key && kv.Value < old) { old = kv.Value; drop = kv.Key; } days.Remove(drop); }
            List<string> parts = new List<string>();
            foreach (KeyValuePair<long, long> kv in days) parts.Add(kv.Key.ToString(CultureInfo.InvariantCulture) + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
            NpcLocker.Set(a, KQuarrel, string.Join(";", parts.ToArray()));
            float by = QuarrelBy * Mathf.Lerp(1.5f, 0.5f, Mathf.InverseLerp(PairLeast, PairMost, Fit(a, o)));
            Add(a, key, -by);
            NpcMemory.Remember(a, "quarrel_" + w[2], 0L, o.Name);
            Npcs.Log(a.Id + ": quarrels with " + o.Id + " (" + w[2] + ", −" + by.ToString("0", CultureInfo.InvariantCulture) + ")");
        }

        // where it is run, every LazyEvery s while a player sees it: a diligent one at its work, a lazy neighbour idling near it
        internal static float LazyEvery = 45f, LazyNear = 12f, LazyChance = 0.3f;
        private static readonly Dictionary<NpcActor, float> NextLazy = new Dictionary<NpcActor, float>(); // session: when it looks next
        internal static void Tick(NpcActor a)
        {
            float at;
            if (NextLazy.TryGetValue(a, out at) && Time.time < at) return;
            if (NextLazy.Count > 256) NextLazy.Clear();
            NextLazy[a] = Time.time + LazyEvery * UnityEngine.Random.Range(0.8f, 1.2f);
            Tease(a);
            if (a.Mind == null || a.Worker == null || !a.Worker.Works || a.Mind.NowLayer != NpcMind.Layer.Job || NpcPersona.Of(a).Diligence < 0.65f || !NpcClock.Seen(a)) return;
            foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart)))
            {
                if (o == null || o == a || !o.Owned || o.Mind == null || o.Worker == null || !o.Worker.Works || !o.Mind.AtLeisure || NpcToil.Tired(o) >= 60f) continue; // (one with work of its own, at its ease, not worn out — run here too: another game's plans are not known here, it would always look idle)
                if (NpcPersona.Of(o).Laziness < 0.55f || Utils.DistanceXZ(o.transform.position, a.transform.position) > LazyNear) continue;
                if (UnityEngine.Random.value < LazyChance) Quarrel(a, o, "work");
                return;
            }
        }

        // a bully at its ease, one it can pick on near it (the same look as above, while a player sees it): a word over its head, a grudge in that one
        internal static float TeaseNear = 8f, TeaseChance = 0.2f;
        private static void Tease(NpcActor a)
        {
            if (!NpcNature.Is(a, "bully") || a.Mind == null || !a.Mind.AtLeisure || !NpcClock.Seen(a) || !NpcVoice.Free(a) || !NpcActor.FloorFree(a.transform.position, a) || UnityEngine.Random.value > TeaseChance) return; // (it must be heard: no grudge for a word never said)
            foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart)))
            {
                if (o == null || o == a || Utils.DistanceXZ(o.transform.position, a.transform.position) > TeaseNear) continue; // (the near test first: the nature's lookup after)
                if (!NpcNature.Is(o, "coward") && !NpcNature.Is(o, "lazy") && !NpcNature.Is(o, "loner")) continue;
                NpcPhrases.SayOr(a, "nature_bully_tease", "", new Dictionary<string, string> { { "other", o.Name } }, NpcActor.Voice.Unasked);
                if (a.Body != null && a.Body.ClipFree && a.Mind != null && !a.Mind.SeatedNow) { a.Mind.Face(o.transform.position); a.Body.Clip("fist_palm"); } // (its fist in its palm as it says it)
                Quarrel(o, a, "teased");
                return;
            }
        }

        internal static void Forget(NpcActor a) { NextLazy.Remove(a); }

        // «Помиріться вже» (its talk, !makepeace — {peace} its answer): make it up with the one it last quarrelled with? Its patience to you
        // and its ways say (a grumbler hardly); yes: both warmer, as far as they may be (Fit), and the quarrel let go from its mind
        internal static string PeaceSaid = "";
        internal static float PeaceBy = 12f, PeaceBack = 6f;
        internal static void MakePeace(NpcActor a, Player p, string[] w)
        {
            PeaceSaid = "…";
            if (a == null || p == null) return;
            NpcMemory.Entry last = null;
            foreach (string k in NpcMemory.Kinds.Keys)
            {
                if (!k.StartsWith("quarrel_", StringComparison.Ordinal)) continue;
                NpcMemory.Entry e = NpcMemory.Find(a, k, 0L);
                if (e != null && (last == null || e.At > last.At)) last = e;
            }
            if (last == null) { PeaceSaid = NpcPhrases.Compose(a, "peace_none", p) ?? a.Form(Lang.T("Та ми й не сварилися.")); return; }
            NpcActor o = null;
            foreach (NpcActor x in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart))) if (x != null && x != a && x.Name == last.What) o = x;
            Dictionary<string, string> who = new Dictionary<string, string> { { "other", last.What } };
            bool grumpy = NpcPhrases.Tones(a).Contains("бурч");
            if (o == null || NpcPatience.Of(a, p) < NpcPatience.Dry || UnityEngine.Random.value > (grumpy ? 0.35f : 0.75f))
            {
                PeaceSaid = NpcPhrases.Compose(a, "peace_no", p, who) ?? a.Form(Lang.T("Ні. Хай спершу перше слово скаже."));
                NpcPatience.Spend(a, p, 4f);
                return;
            }
            TellPerson(a, o, PeaceBy); TellPerson(o, a, PeaceBack);
            a.Act("forget " + last.Kind);
            PeaceSaid = NpcPhrases.Compose(a, "peace_yes", p, who) ?? a.Form(Lang.T("Ну… гаразд. Спробую."));
            Npcs.Log(a.Id + ": makes it up with " + o.Id + " (asked)");
        }

        // who of its people it gets on with best ({friend}) and worst ({foe}), and two of them at odds with each other ({feud_a}, {feud_b}):
        // what it can tell truly of its neighbours (NpcPhrases.ValueSources; about_folk, feud)
        // Asked for every line it makes: its own locker read once; the pair at odds is its heart's, looked for at most once in FeudFor s
        internal static void Values(NpcActor a, Player to, Dictionary<string, string> v)
        {
            ZDOID heart = NpcKit.GetRef(a.Zdo, NpcResident.KHeart);
            if (heart.IsNone()) return;
            List<Feel> mine = Read(a);
            if (mine != null && mine.Count > 0)
            {
                NpcActor best = null, worst = null; float hi = 25f, lo = -15f;
                foreach (NpcActor o in NpcHeart.Residents(heart))
                {
                    if (o == null || o == a) continue;
                    long k = PersonKey(o);
                    Feel e = k != 0L ? mine.Find(x => x.Player == k) : null;
                    if (e == null) continue;
                    float f = Drifted(e);
                    if (f >= hi) { hi = f; best = o; }
                    if (f <= lo) { lo = f; worst = o; }
                }
                if (best != null) v["friend"] = best.Name;
                if (worst != null) v["foe"] = worst.Name;
                NpcActor near = null; float most = 25f; // (one it likes, near it now: from the same reading, not one a person)
                foreach (NpcActor o in NpcActor.Live)
                {
                    if (o == null || o == a || (o.transform.position - a.transform.position).sqrMagnitude >= NpcTalk.FriendNearM * NpcTalk.FriendNearM) continue;
                    long k = PersonKey(o);
                    Feel e = k != 0L ? mine.Find(x => x.Player == k) : null;
                    if (e == null) continue;
                    float f = Drifted(e);
                    if (f >= most) { most = f; near = o; }
                }
                if (near != null) v["friend_near"] = near.Name;
            }
            string fa, fb;
            if (Feud(heart, a, out fa, out fb)) { v["feud_a"] = fa; v["feud_b"] = fb; }
        }

        // two of its people at odds: now and then it says so (NpcVoice: what it sees of the others)
        internal static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            string fa, fb;
            if (Feud(NpcKit.GetRef(a.Zdo, NpcResident.KHeart), a, out fa, out fb)) themes.Add(new KeyValuePair<string, float>("feud", 20f));
        }

        // a pair of a heart's people (not «me») at odds with each other, cold one way or both: each one's locker read once, at most once in FeudFor s
        internal static float FeudFor = 30f;
        private sealed class FeudKept { public float At; public string A, B; public NpcActor WithA, WithB; }
        private static readonly Dictionary<ZDOID, FeudKept> Feuds = new Dictionary<ZDOID, FeudKept>(); // session: a look kept a while
        private static bool Feud(ZDOID heart, NpcActor me, out string a, out string b)
        {
            a = b = null;
            if (heart.IsNone()) return false;
            FeudKept k;
            if (!Feuds.TryGetValue(heart, out k) || Time.time - k.At > FeudFor)
            {
                if (Feuds.Count > 64) Feuds.Clear();
                k = new FeudKept { At = Time.time };
                List<NpcActor> folk = new List<NpcActor>(NpcHeart.Residents(heart));
                for (int i = 0; i < folk.Count && k.A == null; i++)
                {
                    NpcActor x = folk[i];
                    List<Feel> l = x != null ? Read(x) : null;
                    if (l == null) continue;
                    foreach (Feel e in l)
                    {
                        if (e.Player >= 0L || Drifted(e) > -15f) continue; // (a person, cold to it)
                        NpcActor y = folk.Find(o => o != null && o != x && PersonKey(o) == e.Player);
                        if (y == null) continue;
                        k.A = x.Name; k.B = y.Name; k.WithA = x; k.WithB = y;
                        break;
                    }
                }
                Feuds[heart] = k;
            }
            if (k.A == null || k.WithA == me || k.WithB == me) return false; // (its own quarrel is its own words: mem_quarrel_*)
            a = k.A; b = k.B;
            return true;
        }

        // the person it likes best among these, at least warm (null: none)
        internal static NpcActor Friend(NpcActor a, IEnumerable<NpcActor> among)
        {
            NpcActor best = null; float most = 25f;
            foreach (NpcActor o in among) { if (o == null || o == a) continue; float v = OfPerson(a, o); if (v >= most) { most = v; best = o; } }
            return best;
        }

        internal static string Level(float v) { return v <= -50f ? "hostile" : v < -15f ? "cold" : v >= 60f ? "close" : v >= 25f ? "warm" : "even"; }
        internal static string Level(NpcActor a, Player p) { return p != null ? Level(Of(a, p.GetPlayerID())) : "even"; }

        // its card, the board: how it is with you, a word — and a place on the scale (0 … 1) and a sign for a row
        internal static string Word(string level)
        {
            switch (level)
            {
                case "hostile": return Lang.T("ворожо");
                case "cold": return Lang.T("холодно");
                case "warm": return Lang.T("тепло");
                case "close": return Lang.T("близько");
                default: return Lang.T("рівно");
            }
        }
        internal static int Rank(string level) { return level == "hostile" ? 0 : level == "cold" ? 1 : level == "warm" ? 3 : level == "close" ? 4 : 2; } // (which of its five words)

        // ---------------------------------------------------------------- who with whom among these (the board, its card): each one's locker read once, when asked
        internal sealed class Tie { public NpcActor A, B; public bool Foes; public bool Close; public string Why = ""; }

        internal static List<Tie> Ties(List<NpcActor> folk)
        {
            List<Tie> l = new List<Tie>();
            Dictionary<NpcActor, List<Feel>> read = new Dictionary<NpcActor, List<Feel>>();
            foreach (NpcActor x in folk) if (x != null) read[x] = Read(x);
            for (int i = 0; i < folk.Count; i++)
                for (int j = i + 1; j < folk.Count; j++)
                {
                    NpcActor a = folk[i], b = folk[j];
                    if (a == null || b == null) continue;
                    float ab = Toward(read[a], b), ba = Toward(read[b], a);
                    if (ab <= -15f || ba <= -15f) l.Add(new Tie { A = a, B = b, Foes = true, Why = QuarrelWord(ab <= ba ? a : b, ab <= ba ? b : a) });
                    else if (ab >= 25f && ba >= 25f) l.Add(new Tie { A = a, B = b, Close = ab >= 60f && ba >= 60f });
                }
            l.Sort((x, y) => x.Foes == y.Foes ? y.Close.CompareTo(x.Close) : y.Foes.CompareTo(x.Foes)); // (the quarrels first)
            return l;
        }

        private static float Toward(List<Feel> mine, NpcActor o)
        {
            long k = PersonKey(o);
            Feel e = mine != null && k != 0L ? mine.Find(x => x.Player == k) : null;
            return e != null ? Drifted(e) : 0f;
        }

        // what it holds against that one, in words ("": nothing it remembers): its last quarrel (NpcMemory quarrel_<why>, what: the name)
        private static string QuarrelWord(NpcActor a, NpcActor o)
        {
            NpcMemory.Entry last = null;
            foreach (NpcMemory.Entry e in NpcMemory.About(a, 0L)) if (e.Kind.StartsWith("quarrel_", StringComparison.Ordinal) && e.What == o.Name && (last == null || e.At > last.At)) last = e;
            if (last == null) return "";
            switch (last.Kind)
            {
                case "quarrel_food": return Lang.T("через останню миску");
                case "quarrel_place": return Lang.T("через своє місце");
                case "quarrel_work": return Lang.T("через те, хто скільки працює");
                case "quarrel_table": return Lang.T("слово за слово за столом");
                case "quarrel_raid": return Lang.T("через набіг: один сховався");
                case "quarrel_pay": return Lang.T("через платню");
                case "quarrel_teased": return Lang.T("через кпини при всіх");
                default: return "";
            }
        }

        // ---------------------------------------------------------------- writing (where the person is owned)
        // from any game: sent to the one that runs it (NpcActor.Acts «attitude»)
        internal static void Tell(NpcActor a, long player, float by, float upTo = 100f)
        {
            if (a == null || player == 0L || by == 0f) return;
            a.Act("attitude " + player.ToString(CultureInfo.InvariantCulture) + " " + by.ToString("0.#", CultureInfo.InvariantCulture) + (upTo < 100f ? " " + upTo.ToString("0.#", CultureInfo.InvariantCulture) : ""));
        }

        internal static void Add(NpcActor a, long player, float by, float upTo = 100f)
        {
            if (a == null || !a.Owned || player == 0L) return;
            List<Feel> l = Read(a);
            if (l == null) return;
            long day = Today();
            Feel f = l.Find(x => x.Player == player);
            if (f == null) { f = new Feel { Player = player }; l.Add(f); }
            float was = Drifted(f);
            f.Value = by > 0f ? Mathf.Max(was, Mathf.Min(was + by, upTo)) : Mathf.Max(-100f, was + by); // (a cap stops a rise only: never pulls it down)
            f.Value = Mathf.Clamp(f.Value, -100f, 100f);
            f.Day = day;
            _touched = f;
            Write(a, l);
            _touched = null;
            if (Level(was) != Level(f.Value))
            {
                Npcs.Log(a.Id + ": feels " + Level(f.Value) + " to " + player + " (" + Mathf.RoundToInt(f.Value) + ")");
                if (player > 0L) StoryBus.Raise("npc_attitude_" + Level(f.Value), a.Id, Mathf.RoundToInt(f.Value), a.transform.position); // (to a player only: the story waiting on it must not hear two neighbours make friends)
            }
        }

        // «attitude <player> <by> [up to]» (NpcActor.Acts)
        internal static void Act(NpcActor a, string[] w)
        {
            long player; float by, upTo = 100f;
            if (w.Length < 3 || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out player) || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out by)) return;
            if (w.Length > 3) float.TryParse(w[3], NumberStyles.Float, CultureInfo.InvariantCulture, out upTo);
            Add(a, player, by, upTo);
        }

        // it sees you (NpcVoice, where it is owned): once a game day a little warmer
        internal static void SawYou(NpcActor a, Player p)
        {
            if (a == null || p == null || !a.Owned) return;
            List<Feel> l = Read(a);
            if (l == null) return;
            Feel f = l.Find(x => x.Player == p.GetPlayerID());
            if (f != null && f.Day == Today()) return;
            Add(a, p.GetPlayerID(), Seen, SeenUpTo);
        }

        // ---------------------------------------------------------------- its words (NpcPhrases.Variants)
        // the theme for how it feels about you: tried before the plain one (none fits: the plain one)
        internal static string Variant(NpcActor a, Player to, string theme)
        {
            if (to == null) return null;
            string level = Level(a, to);
            if (level == "even") return null;
            float chance = level == "hostile" ? 0.9f : level == "cold" ? 0.75f : level == "close" ? 0.7f : 0.5f;
            if (UnityEngine.Random.value > chance) return null;
            if (theme.StartsWith("how_", StringComparison.Ordinal)) return level == "hostile" || level == "cold" ? "how_" + level : null; // (warm or dear: its plain how is warm enough)
            switch (theme)
            {
                case "see_player": case "back": case "about_you": case "talk_invite": case "talk_again": case "hire_yes": case "fire_ask": case "request_thanks": return theme + "_" + level; // (no words for this level: the plain theme)
                default: return null;
            }
        }

        // ---------------------------------------------------------------- the story's condition
        internal static void Register()
        {
            StoryCond.Register("npcattitude", new StoryCond.Kind { Usage = "npcattitude [>=|<|…] N [npc]   (як він до тебе: −100…100; тепло ≥ 25, рідні ≥ 60, холодно < −15, ворожо ≤ −50)", Min = 1,
                Check = w => StoryCond.CmpCheck(w, 1),
                Test = (p, w) =>
                {
                    string last = w[w.Length - 1];
                    int ignored;
                    NpcActor n = w.Length > 2 && !int.TryParse(last, out ignored) && !last.StartsWith(">") && !last.StartsWith("<") && !last.StartsWith("=") ? NpcLevers.Who(w, w.Length - 1) : NpcTalk.Him; // (a person named: that one — not here, no one)
                    return n != null && p != null && StoryCond.Compare(Mathf.RoundToInt(Of(n, p.GetPlayerID())), w, 1);
                } });
        }

        // the console
        internal static string Describe(NpcActor a)
        {
            List<Feel> l = Read(a);
            if (l == null) return Lang.T("Шафка ще не тут.");
            if (l.Count == 0) return a.Name + ": " + Lang.T("до всіх рівно.");
            List<string> s = new List<string>();
            foreach (Feel f in l) s.Add(f.Player + " " + Mathf.RoundToInt(Drifted(f)) + " (" + Level(Drifted(f)) + ")");
            return a.Name + ": " + string.Join(", ", s.ToArray());
        }

        // ---------------------------------------------------------------- kept
        private static long Today() { float day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f; return (long)(NpcRescue.Now / day); }

        // unseen long: a point a day back towards even
        private static float Drifted(Feel f)
        {
            long off = Today() - f.Day - DriftAfter;
            if (off <= 0) return f.Value;
            return f.Value > 0f ? Mathf.Max(0f, f.Value - off) : Mathf.Min(0f, f.Value + off);
        }

        private static List<Feel> Read(NpcActor a)
        {
            string s = NpcLocker.Get(a, KAttitude);
            if (s == null) return null;
            List<Feel> l = new List<Feel>();
            foreach (string e in s.Split(';'))
            {
                int eq = e.IndexOf('='), at = e.IndexOf('@');
                if (eq < 0 || at < eq) continue;
                Feel f = new Feel();
                if (!long.TryParse(e.Substring(0, eq), NumberStyles.Integer, CultureInfo.InvariantCulture, out f.Player)) continue;
                float.TryParse(e.Substring(eq + 1, at - eq - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out f.Value);
                long.TryParse(e.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out f.Day);
                l.Add(f);
            }
            return l;
        }

        private static void Write(NpcActor a, List<Feel> l)
        {
            Trim(l, true, Most); Trim(l, false, MostPeople); // (players and people each in their own room)
            List<string> parts = new List<string>();
            foreach (Feel f in l) parts.Add(f.Player.ToString(CultureInfo.InvariantCulture) + "=" + Mathf.RoundToInt(f.Value).ToString(CultureInfo.InvariantCulture) + "@" + f.Day.ToString(CultureInfo.InvariantCulture));
            NpcLocker.Set(a, KAttitude, string.Join(";", parts.ToArray()));
        }

        // the ones nearest even go first, of players or of people only, till «most» are left (a new face is not let go at once)
        private static void Trim(List<Feel> l, bool players, int most)
        {
            while (l.FindAll(x => (x.Player > 0L) == players).Count > most)
            {
                Feel calm = null;
                foreach (Feel f in l) if ((f.Player > 0L) == players && f != _touched && (calm == null || Mathf.Abs(Drifted(f)) < Mathf.Abs(Drifted(calm)))) calm = f;
                if (calm == null) return;
                l.Remove(calm);
            }
        }
    }
}
#endif
