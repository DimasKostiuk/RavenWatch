using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace HuginWhispers
{
    // The ravens speak aloud — when a voice engine is there (RavenVoiceApi: the RavenWatch_Voice package connects one;
    // RavenWatch itself starts no program). The card is shown together with the sound: the line waits for its voice
    // (about a second), and if the voice is late the card goes out on its own. Each raven speaks with the voice of its
    // character (Norov).
    internal static class RavenVoice
    {
        private static ConfigEntry<bool> _on;
        private static ConfigEntry<float> _volume, _maxWait;
        private static ConfigEntry<int> _threads;

        private static IRavenVoiceEngine _engine;
        private static string _engineBy;
        private static bool _started, _ready, _dead;
        private static readonly Queue<Msg> Inbox = new Queue<Msg>();
        private static readonly Dictionary<int, Pending> Waiting = new Dictionary<int, Pending>();
        private static int _nextId = 1;
        private static readonly Dictionary<string, float> Spoken = new Dictionary<string, float>(); // talker+line -> when voiced: not again within a minute
        private static VoiceDriver _driver;
        private static float _nextAlive;

        private sealed class Pending
        {
            public GameObject Talker; public bool Munin; public string Text; public float Asked; public Func<float, bool> Show; public bool Shown;
            public AudioClip Clip; public float Queued; // ready and waiting for its turn
            public string Kind; public float Wait; // the engine's profile (h / m / n); how long the card waits for the sound (0 = Max wait)
        }

        private const string Section = "8. Voice";
        private const string OnDesc = "Hugin and Munin say their lines aloud with their own voices. The line's card appears together with the voice.";

        internal static void Init(Plugin p)
        {
            // «Ravens speak aloud» and the volume show in the menu only once a voice engine connects (Connect): without
            // one they would do nothing
            _threads = p.Hide(Section, "Voice threads", 0, new ConfigDescription("CPU threads the voice may use (it runs at low priority). 0 = by the computer: half its cores, 2 to 4.", new AcceptableValueRange<int>(0, 8)));
            _maxWait = p.Hide(Section, "Max wait seconds", 4f, "If the voice is not ready this long after the line, the card is shown without it.");
            GameObject go = new GameObject("RavenWatch_Voice"); UnityEngine.Object.DontDestroyOnLoad(go); _driver = go.AddComponent<VoiceDriver>();
            if (_engine != null) BindMenu(p);
        }

        private static void BindMenu(Plugin p)
        {
            if (_on != null || p == null) return;
            _on = p.Config.Bind(Section, "Ravens speak aloud", true, OnDesc);
            _volume = p.Config.Bind(Section, "Voice volume", 0.9f, new ConfigDescription("Loudness of the ravens' voices.", new AcceptableValueRange<float>(0f, 1f)));
        }

        // a voice engine connects (RavenVoiceApi.Connect)
        internal static bool Connect(IRavenVoiceEngine engine, string by)
        {
            if (engine == null) return false;
            if (_engine != null) { Log("Voice: " + (by ?? "?") + " wants to connect, but " + _engineBy + " already speaks: refused"); return false; }
            _engine = engine; _engineBy = by ?? "?";
            _dead = false; _started = false;
            Log("Voice: " + _engineBy + " connected (" + (engine.Language ?? "?") + ")");
            if (Plugin.Instance != null && _driver != null) BindMenu(Plugin.Instance); // (before Init: Init binds it)
            return true;
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }

        private static int _headless = -1;
        private static bool Headless()
        {
            if (_headless < 0) _headless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ? 1 : 0;
            return _headless == 1;
        }
        // measured: 4 threads 0.24 s of work per second of voice, 8 only 13% faster; a 4-core computer keeps 2 for the game
        private static int Threads { get { return _threads != null && _threads.Value > 0 ? _threads.Value : Mathf.Clamp(SystemInfo.processorCount / 2, 2, 4); } }

        // the language the engine speaks (Ukrainian if it does not say)
        private static string VoiceLanguage
        {
            get
            {
                string l = null;
                try { l = _engine != null ? _engine.Language : null; } catch { }
                return string.IsNullOrEmpty(l) ? Lang.Source : l.Trim();
            }
        }

        // the ravens can speak aloud here and now: an engine is connected, switched on, and speaks the phrases' language
        internal static bool Available
        {
            get
            {
                if (_engine == null || _on == null || !_on.Value || _dead || Headless()) return false;
                return string.Equals(VoiceLanguage, Lang.PhraseLanguage, StringComparison.OrdinalIgnoreCase);
            }
        }

        // the engine started (once, on the first line or early from the driver); off on servers, when switched off or
        // in another language
        private static bool Running()
        {
            if (_engine == null || _on == null || !_on.Value || _dead || Headless()) return false;
            if (!_started && !Available) return false; // another language: silent, and the engine is not started
            if (_started)
            {
                bool alive;
                try { alive = _engine.Running; } catch { alive = false; }
                return alive || Fail("the voice stopped");
            }
            try
            {
                if (!_engine.Start(Threads)) return Fail("the voice could not start");
                _started = true;
                return true;
            }
            catch (Exception e) { return Fail("cannot start the voice: " + e.Message); }
        }

        private static bool Fail(string why)
        {
            _dead = true; Log("Voice: " + why + ", the ravens stay silent");
            if (_started && _engine != null) { try { _engine.Stop(); } catch { } } // (whatever is left of it does not run on unused)
            _started = false;
            Flush(); return false;
        }

        // nothing more will come from the engine: waiting cards go out alone, the sounds already made are freed
        private static void Flush()
        {
            _ready = false;
            foreach (Pending w in Waiting.Values) if (w.Show != null && !w.Shown) { w.Shown = true; w.Show(0f); }
            foreach (Pending t in Turns) { t.Show(0f); if (t.Clip != null) UnityEngine.Object.Destroy(t.Clip); }
            foreach (AudioClip c in Ready.Values) if (c != null) UnityEngine.Object.Destroy(c);
            Turns.Clear(); Waiting.Clear(); Ready.Clear(); ReadyAt.Clear(); Loudness.Clear();
            lock (Inbox) Inbox.Clear();
        }

        // what the engine tells (RavenVoiceApi: from any thread), taken on the game's thread in Tick
        internal enum MsgKind { Ready, Result, Lost }
        private sealed class Msg { public MsgKind Kind; public int Id; public float[] Samples; public int Rate; public string Text; }

        internal static void Post(MsgKind kind, int id, float[] samples, int rate, string text)
        {
            Msg m = new Msg(); m.Kind = kind; m.Id = id; m.Samples = samples; m.Rate = rate; m.Text = text;
            lock (Inbox) Inbox.Enqueue(m);
        }

        // a raven line: true = the voice takes it and will call show(seconds of sound) when the sound is ready;
        // show answers whether the sound is still wanted (false: its card is gone, the raven stays silent)
        // kind: the delivery (null = the raven's own; "n" = plain, the Guide); wait: seconds the sound may take (0 = Max wait)
        internal static bool Say(GameObject talker, bool munin, int voice, string text, Func<float, bool> show, string kind = null, float wait = 0f)
        {
            if (talker == null || string.IsNullOrEmpty(text) || !Running()) return false;
            string plain = Plain(text);
            if (plain.Length < 2 || !InVoiceLanguage(plain)) return false;
            foreach (Pending w in Waiting.Values) if (w.Talker == talker && w.Text == plain && !w.Shown) return true; // the same line asked twice
            string key = talker.GetInstanceID() + "|" + plain; float when;
            if (Spoken.TryGetValue(key, out when) && Time.time - when < 60f) return false; // the game repeats a line: card only
            if (Spoken.Count > 200) Spoken.Clear();
            Spoken[key] = Time.time;
            Pending p = new Pending(); p.Talker = talker; p.Munin = munin; p.Text = plain; p.Show = show; p.Kind = kind; p.Wait = wait;
            return Ask(p, munin, voice) >= 0;
        }

        // the engine skips a line nobody waits for any more (its card went out alone, its talk was cut short), so a
        // pile of asked lines never holds up the next one
        private static void Forget(int id)
        {
            if (!_started || _engine == null) return;
            try { _engine.Drop(id); } catch { }
        }

        // a player's raven still has something to say: being made, waiting for its turn, or sounding now (a thought
        // asked meanwhile would only wait and then come without its voice). munin: which of the two
        internal static bool Busy(Player owner, out bool munin)
        {
            munin = false;
            if (owner == null || !_started) return false;
            GameObject go = owner.gameObject;
            foreach (Pending w in Waiting.Values) if (w.Talker == go && w.Show != null && !w.Shown) { munin = w.Munin; return true; }
            foreach (Pending t in Turns) if (t.Talker == go) { munin = t.Munin; return true; }
            // (a raven of a talk sounds from its model: on the shoulder bone, or just off it in flight)
            foreach (Speaking s in Speakers)
                if (s.At != null && Time.time < s.Until && (s.At.IsChildOf(owner.transform) || (s.At.position - owner.transform.position).sqrMagnitude < 9f)) { munin = s.Munin; return true; }
            return false;
        }

        private static int Ask(Pending p, bool munin, int voice)
        {
            int id = _nextId++;
            p.Asked = Time.time;
            Waiting[id] = p;
            bool sent;
            try { sent = _engine.Say(id, p.Kind ?? (munin ? "m" : "h"), voice, UnityEngine.Random.Range(1, int.MaxValue), p.Text); }
            catch (Exception e) { Log("Voice: " + e.Message); sent = false; }
            if (!sent) { Waiting.Remove(id); Fail("lost the voice"); return -1; }
            return id;
        }

        // another player's raven speaks in that player's language (its line comes over the net as text): a line in Latin
        // letters (a friend playing in English) read by the Ukrainian voice came out as broken «English» — card only
        internal static bool InVoiceLanguage(string plain)
        {
            if (!string.Equals(VoiceLanguage, Lang.Source, StringComparison.OrdinalIgnoreCase)) return true;
            int cyr = 0, lat = 0;
            foreach (char c in plain)
            {
                if (c >= 'Ѐ' && c <= 'ӿ') cyr++;
                else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) lat++;
            }
            return cyr >= lat; // (a Latin nick inside a Ukrainian line is fine)
        }

        // the card text without rich tags and line breaks
        internal static string Plain(string text)
        {
            string t = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
            return System.Text.RegularExpressions.Regex.Replace(t, "[\\r\\n\\t]+", " ").Trim();
        }

        // the visiting (big) Hugin and the Guide: voices.bin after the shoulder ravens' fourteen (voice-proto/scripts/voices_bin.mjs)
        internal const int BigHugin = 14, GuideVoice = 15;
        // big Munin: after the 1.2.0 timbres (RavenWatch_Voice 1.2.0); an older voice package has none: he stays silent
        internal const int BigMunin = 58;
        // where each raven's timbres start in voices.bin (seven characters each): Hugin his own 0, «Зає» 23, «Морф» 30, «Свід» 37;
        // Munin Iago 7, «Голум» 16, «Вал» 44, «Сміт» 51 (the last two of each from RavenWatch_Voice 1.2.0)
        private static readonly int[] HuginTimbres = { 0, 23, 30, 37 }, MuninTimbres = { 7, 16, 44, 51 };
        // how many voices the voice package has (it tells: RavenVoiceApi.Voices); 30 = RavenWatch_Voice 1.0–1.1, which never tells
        internal static int VoiceCount = 30;
        // A raven's timbre is picked once and kept on the raven for good (TimbreKey: in its item, so in its copies too), so a
        // later voice package never changes an old raven's voice. The pick goes over Pool timbres — the 1.2.0 set; it is made
        // the first time the raven is worn with a package that has them all. A later package with more timbres: raise the
        // pick for ravens WITHOUT one only (a new constant beside Pool), never re-pick a kept one.
        internal const int Pool = 4;
        internal const string TimbreKey = "RavenWatch_timbre";
        internal const string TimbreNet = "RavenWatch_Timbre"; // on the player's ZDO: Hugin's kept timbre + 1 | Munin's + 1 << 4

        // a big raven's words: the quick remark when it lands, the text on E (big Munin only with a voice package that has
        // his voice). The words are on screen already; the sound may come a while later (a long text takes the model a few seconds)
        internal static void SayBig(Raven raven, string text, bool guide)
        {
            bool bigMunin = raven != null && raven.m_isMunin && !guide;
            if (raven == null || (bigMunin && BigMunin >= VoiceCount) || !Available) return;
            BigRavenHush hush = raven.GetComponent<BigRavenHush>();
            if (hush == null) hush = raven.gameObject.AddComponent<BigRavenHush>();
            hush.Until = Time.time + 40f; // (the sound may come seconds late and runs up to 15 s)
            // (the sound comes seconds later: by then he may have flown off — then no voice out of the empty air)
            Say(raven.gameObject, bigMunin, guide ? GuideVoice : bigMunin ? BigMunin : BigHugin, text, s => raven != null && Plugin.IsSpawned(raven) && !Plugin.IsFlying(raven), guide ? "n" : bigMunin ? "m" : "h", 15f);
        }

        // a voice per character of Norov, in this order (voices.bin: Hugin's seven, then Munin's seven)
        private static readonly string[] VoiceChars = { "calm", "watchful", "fidget", "wanderer", "bold", "homebody", "devoted" };

        // the voice of a raven: the manner by the character of the player's raven (Norov), the timbre its own — kept on it
        // (TimbreKey), else picked by its caw number (Voice: made from its id, the same in every game); a raven with no number
        // yet: steady by its player's name. A timbre the voice package does not have (an older one): one of those it has.
        internal static int VoiceOf(Player owner, bool munin, GameObject talker)
        {
            int code = CodeOf(owner, munin, talker);
            Norov.Character c = owner != null ? Norov.OnPlayer(owner, munin) : null;
            int v = c != null ? Array.IndexOf(VoiceChars, c.Id) : -1;
            if (v < 0) v = code >= 0 ? code % VoiceChars.Length : Mathf.Abs((talker != null ? talker.name : "raven").GetHashCode() + (owner != null ? owner.GetPlayerName().GetHashCode() : 0)) % VoiceChars.Length;
            int[] timbres = munin ? MuninTimbres : HuginTimbres;
            int t = KeptTimbre(owner, munin, talker);
            if (t < 0) t = code >= 0 ? FirstPick(code) : Mathf.Abs(((owner != null ? owner.GetPlayerName() : talker != null ? talker.name : "raven") + (munin ? "|munin voice" : "|hugin voice")).GetHashCode()) % Pool;
            int have = 0; foreach (int b in timbres) if (b + VoiceChars.Length <= VoiceCount) have++;
            if (t >= have) t %= Math.Max(1, have); // (an older voice package: one of the timbres it has)
            return timbres[t] + v;
        }

        // the timbre a raven gets the first time: from its caw number, over the 1.2.0 set
        private static int FirstPick(int code) { return (int)(Stir(code) % (uint)Pool); }

        // a raven's kept timbre as every game knows it (a raven on a perch: its seat's record; a worn one: its player's), -1: none
        internal static int KeptTimbre(Player owner, bool munin, GameObject talker)
        {
#if RAVEN_ITEM
            PerchLife perch = talker != null ? talker.GetComponent<PerchLife>() : null;
            if (perch != null) return perch.Timbre;
            owner = OwnerOf(owner, talker);
            ZDO z = owner != null ? NetFlag.Zdo(owner, false) : null;
            return z != null ? ((z.GetInt(TimbreNet, 0) >> (munin ? 4 : 0)) & 15) - 1 : -1;
#else
            return -1;
#endif
        }

        // the timbre kept in the raven itself, -1: not picked yet
        internal static int KeptTimbre(ItemDrop.ItemData item)
        {
            string s; int t;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(TimbreKey, out s) && int.TryParse(s, out t) && t >= 0 && t < 15 ? t : -1;
        }

        // the wearer's game (twice a second): a raven with no kept timbre gets it now — only once the voice package has the
        // whole set (so the first pick is from all of it: the one re-pick of the ravens players have); then both kept
        // timbres go onto the player, written only when they change
        internal static void PublishTimbres(Player p, ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            int v = 0;
            for (int i = 0; i < 2; i++)
            {
                bool m = i == 1; ItemDrop.ItemData item = m ? munin : hugin;
                int t = KeptTimbre(item);
#if RAVEN_ITEM
                if (t < 0 && item != null && HasWholeSet())
                {
                    int code = Voice.Code(item);
                    if (code >= 0) { t = FirstPick(code); item.m_customData[TimbreKey] = t.ToString(); Log("Voice: " + Plugin.RavenName(m) + " keeps timbre " + t + " for good"); }
                }
#endif
                if (t >= 0) v |= (t + 1) << (m ? 4 : 0);
            }
            if (z.GetInt(TimbreNet, 0) != v) z.Set(TimbreNet, v);
        }

        // the voice package has every timbre of the pick (RavenWatch_Voice 1.2.0 or later)
        internal static bool HasWholeSet()
        {
            for (int i = 0; i < Pool; i++) if (HuginTimbres[i] + VoiceChars.Length > VoiceCount || MuninTimbres[i] + VoiceChars.Length > VoiceCount) return false;
            return true;
        }

        // the raven's caw number (Voice) as every game knows it: a raven on a perch keeps its own, a shoulder raven its
        // player's; -1: none
        internal static int CodeOf(Player owner, bool munin, GameObject talker)
        {
#if RAVEN_ITEM
            PerchLife perch = talker != null ? talker.GetComponent<PerchLife>() : null;
            if (perch != null) return perch.VoiceCode;
            owner = OwnerOf(owner, talker);
            return owner != null ? Voice.CodeOn(owner, munin) : -1;
#else
            return -1;
#endif
        }

        // whose raven it is: the owner given, else the player of a shoulder raven, else the talker itself if it is a player
        private static Player OwnerOf(Player owner, GameObject talker)
        {
            if (owner != null || talker == null) return owner;
            ShoulderLife life = talker.GetComponent<ShoulderLife>();
            return life != null ? life.Player : talker.GetComponent<Player>();
        }

        // the caw number stirred again: the speech's own picks from it do not follow the caw's traits
        private static uint Stir(int code) { uint h = (uint)code * 2654435761u; h ^= h >> 13; h *= 0x5BD1E995u; h ^= h >> 15; return h; }

        // main thread: what the engine told, late lines, the sound itself
        internal static void Tick()
        {
            while (true)
            {
                Msg m;
                lock (Inbox) { if (Inbox.Count == 0) break; m = Inbox.Dequeue(); }
                if (m.Kind == MsgKind.Ready) { _ready = true; Log("Voice ready" + (string.IsNullOrEmpty(m.Text) ? "" : ": " + m.Text)); continue; }
                if (m.Kind == MsgKind.Lost) { if (!_dead) Fail(string.IsNullOrEmpty(m.Text) ? "the voice was lost" : m.Text); continue; }
                Pending p;
                if (!Waiting.TryGetValue(m.Id, out p)) continue; // dropped meanwhile
                Waiting.Remove(m.Id);
                if (m.Samples == null) Log("Voice: line " + m.Id + " could not be made");
                if (p.Show != null && p.Shown) continue; // too late: the card went out on its own
                AudioClip clip = m.Samples != null && m.Samples.Length > 1 && m.Rate > 0 ? MakeClip(m.Samples, m.Rate) : null;
                if (p.Show == null) { if (clip != null) { Ready[m.Id] = clip; ReadyAt[m.Id] = Time.time; } continue; } // asked ahead (a talk): kept until the talk takes it
                p.Shown = true;
                if (clip == null) { p.Show(0f); continue; }
                p.Clip = clip; p.Queued = Time.time; Turns.Add(p); // card and voice together, when it is this raven's turn
            }
            // one raven at a time NEAR each other: a card with its voice waits while another raven is speaking within
            // NearRange of it (a while; then the card goes out alone, so a backlog never builds up). Ravens far apart speak
            // at once, and if they then meet mid-line the voices overlap: that is lively, not a fault.
            for (int i = 0; i < Turns.Count; i++)
            {
                Pending t = Turns[i];
                bool near = t.Talker != null && BusyNear(t.Talker.transform.position);
                if (near && Time.time - t.Queued < TurnWait) continue;
                Turns.RemoveAt(i--);
                if (t.Talker == null || near) { t.Show(0f); UnityEngine.Object.Destroy(t.Clip); continue; }
                if (t.Show(t.Clip.length)) Play(t.Talker, t.Clip, t.Munin); else UnityEngine.Object.Destroy(t.Clip);
            }
            // the voice is late (the first line also waits for the model to load): the card goes out without it
            float wait = _ready ? _maxWait.Value : _maxWait.Value + 12f;
            List<int> late = null;
            foreach (KeyValuePair<int, Pending> kv in Waiting)
                if (kv.Value.Show != null && !kv.Value.Shown && Time.time - kv.Value.Asked > (kv.Value.Wait > 0f ? kv.Value.Wait : wait)) { if (late == null) late = new List<int>(); late.Add(kv.Key); }
            // sounds asked ahead that nobody took (the talk was cut short): freed after two minutes
            if (ReadyAt.Count > 0 && Time.time >= _nextSweep)
            {
                _nextSweep = Time.time + 10f; List<int> old = null;
                foreach (KeyValuePair<int, float> kv in ReadyAt) if (Time.time - kv.Value > 120f) { if (old == null) old = new List<int>(); old.Add(kv.Key); }
                if (old != null) foreach (int id in old) Drop(id);
            }
            if (late != null) foreach (int id in late) { Pending p = Waiting[id]; Waiting.Remove(id); Forget(id); p.Shown = true; p.Show(0f); }
            if (_started && !Available) { Stop(); Log("Voice: switched off (or another language)"); } // frees its memory; on again = starts again
            if (_started && !_dead && Time.time >= _nextAlive)
            {
                _nextAlive = Time.time + 1f;
                bool alive;
                try { alive = _engine.Running; } catch { alive = false; }
                if (!alive) Fail("the voice stopped");
            }
        }

        // ---- a talk of the ravens (TalkBubble): all its lines are asked at once, each sound is taken when its line comes
        private static readonly Dictionary<int, AudioClip> Ready = new Dictionary<int, AudioClip>();
        private static readonly Dictionary<int, float> ReadyAt = new Dictionary<int, float>();
        private static float _nextSweep;
        internal static float MaxWait { get { return _maxWait != null ? _maxWait.Value : 4f; } }

        // ---- turns: one voice at a time
        private static readonly List<Pending> Turns = new List<Pending>();
        internal const float TurnWait = 6f;   // the longest a line waits for the one speaking near it to finish
        internal const float NearRange = 12f; // ravens closer than this take turns; farther apart they may speak at once
        private struct Speaking { public Transform At; public float Until; public bool Munin; public ShoulderLife Life; } // (Life: its beak, stopped when the line is cut)
        private static readonly List<Speaking> Speakers = new List<Speaking>();

        // is a raven speaking near this point right now
        internal static bool BusyNear(Vector3 pos)
        {
            for (int i = Speakers.Count - 1; i >= 0; i--)
            {
                Speaking s = Speakers[i];
                if (s.At == null || Time.time >= s.Until) { Speakers.RemoveAt(i); continue; }
                if ((s.At.position - pos).sqrMagnitude < NearRange * NearRange) return true;
            }
            return false;
        }

        // asks a line's sound ahead; -1 = no voice
        internal static int Ahead(bool munin, int voice, string text)
        {
            if (string.IsNullOrEmpty(text) || !Running()) return -1;
            string plain = Plain(text);
            if (plain.Length < 2 || !InVoiceLanguage(plain)) return -1;
            Pending p = new Pending(); p.Text = plain;
            return Ask(p, munin, voice);
        }

        // 1 = ready (the clip is the caller's now), 0 = still being made, -1 = none (failed, or no voice)
        internal static int Take(int id, out AudioClip clip)
        {
            clip = null;
            if (id < 0) return -1;
            if (Ready.TryGetValue(id, out clip)) { Ready.Remove(id); ReadyAt.Remove(id); return 1; }
            return Waiting.ContainsKey(id) && !_dead ? 0 : -1;
        }

        // a sound asked ahead and no longer wanted
        internal static void Drop(int id)
        {
            AudioClip c;
            if (Ready.TryGetValue(id, out c)) { if (c != null) UnityEngine.Object.Destroy(c); Ready.Remove(id); ReadyAt.Remove(id); }
            if (Waiting.Remove(id)) Forget(id);
        }

        internal const float Fade = 40f; // m: a raven's voice is gone by here
        // the curve's time is distance / Fade
        private static readonly AnimationCurve Rolloff = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(3f / Fade, 1f), new Keyframe(10f / Fade, 0.5f), new Keyframe(20f / Fade, 0.2f),
            new Keyframe(30f / Fade, 0.07f), new Keyframe(1f, 0f));

        internal static GameObject Play(GameObject talker, AudioClip clip, bool munin)
        {
            GameObject go = new GameObject("RavenWatch_VoiceLine");
            go.transform.SetParent(talker.transform, false);
            go.transform.localPosition = Vector3.up * (talker.GetComponent<Player>() != null ? 1.7f : 0.5f); // a shoulder raven: by the head
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip; src.volume = _volume != null ? _volume.Value : 0.9f; src.spatialBlend = 1f; src.dopplerLevel = 0f; src.priority = 64; src.spread = 0f;
            // a voice fades like one: full up to 3 m, half at 10, a fifth at 20, gone by 40 (the line keeps playing:
            // come back while it lasts and you hear the rest)
            src.rolloffMode = AudioRolloffMode.Custom; src.minDistance = 1f; src.maxDistance = Fade;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            // dry: the game's sfx group and the world's reverb zones put a strong echo on a voice
            src.reverbZoneMix = 0f; src.bypassReverbZones = true; src.bypassListenerEffects = true;
            Mood m = MoodOf(talker, munin);
            if (m.Calling && m.Life != null && !ReferenceEquals(m.Life.gameObject, talker)) { go.transform.SetParent(m.Life.transform, false); go.transform.localPosition = Vector3.zero; } // (calling from afar: from the raven itself, not your head)
            src.volume *= m.Volume; src.pitch = m.Pitch;
            if (m.Calling) src.maxDistance = Fade * CallReach; // (the same fall-off, stretched: heard farther)
            float hp = Mathf.Max(m.HighPass, m.Timbre == 3 ? ThinCut : 0f);
            if (hp > 0f) go.AddComponent<AudioHighPassFilter>().cutoffFrequency = hp;
            if (m.Timbre == 1) go.AddComponent<AudioDistortionFilter>().distortionLevel = HoarseGrit; // hoarse: the grit, then the low-pass below smooths it
            // ONE low-pass (Unity allows no second filter of a kind on an object: AddComponent gives null): the timbre's
            // own cut (hoarse, muffled) and the distance's (VoiceMuffle), whichever is lower
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
            go.AddComponent<VoiceMuffle>().Top = m.Timbre == 1 ? HoarseCut : m.Timbre == 2 ? MuffledCut : 22000f; // far away it also goes dull (the highs go first), so the distance is heard
            if (m.Echo) { AudioEchoFilter e = go.AddComponent<AudioEchoFilter>(); e.delay = EchoDelay; e.decayRatio = EchoDecay; e.wetMix = EchoWet; e.dryMix = 1f; }
            if (m.Wobble) go.AddComponent<VoiceWobble>();
            src.Play();
            float len = clip.length / m.Pitch; // (a higher pitch plays it quicker)
            Speaking sp; sp.At = go.transform; sp.Until = Time.time + len + 0.25f; sp.Munin = munin; sp.Life = m.Life; Speakers.Add(sp);
            float[] loud;
            if (Loudness.TryGetValue(clip, out loud)) { Loudness.Remove(clip); if (m.Life != null) m.Life.SayBeak(loud, LoudPerSecond * m.Pitch); }
            UnityEngine.Object.Destroy(go, len + (m.Echo ? 0.6f : 0.3f)); // (the echo's tail)
            UnityEngine.Object.Destroy(clip, len + 1f); // a clip is not freed with its object
            if (m.Life == null || m.Quiet || Plugin.Instance == null) return go;
            if (UnityEngine.Random.value < (munin ? CawAfterM : CawAfterH)) Plugin.Instance.StartCoroutine(CawAfter(go, munin, m, len));
            ShoulderLife other = m.Player != null ? Shoulder.LifeOf(m.Player, !munin) : null; // its pair on the other shoulder: now and then it agrees
            if (other != null && other != m.Life && UnityEngine.Random.value < NodChance && len > 1.2f) Plugin.Instance.StartCoroutine(Nod(go, other, m.Life.transform, !munin, len));
            return go;
        }

        // a line cut short (the other raven breaks in): its voice dies away at once and it no longer counts as speaking
        internal static void Cut(GameObject line)
        {
            if (line == null) return;
            for (int i = Speakers.Count - 1; i >= 0; i--) if (Speakers[i].At == line.transform) { if (Speakers[i].Life != null) Speakers[i].Life.SayBeak(null, 0f); Speakers.RemoveAt(i); } // (the beak shuts with it)
            AudioSource src = line.GetComponent<AudioSource>();
            if (src != null) line.AddComponent<VoiceFade>();
            else UnityEngine.Object.Destroy(line);
        }

        // How it says it, looked at once as the line starts (nothing every frame): you sneaking or in Hugin's shadow —
        // quiet and breathy; your fight or a thunderstorm out in the open — louder, a touch quicker and higher; the raven
        // drooping (starving, sulking, asleep) — slower and lower; your mead in the last minutes — tipsy, the pitch
        // swaying; under stone (a cave, a dungeon) — an echo; far from you (scouting, an order, a strike) — it calls:
        // louder, higher, heard farther. Each player's raven a touch its own (Tint). Another player's raven: what this
        // game sees of it (its sneak, its droop, the stone round it, how far it is); the fight and the mead only for yours.
        private struct Mood { public float Volume, Pitch, HighPass, Own; public bool Echo, Wobble, Quiet, Calling; public int Timbre; public ShoulderLife Life; public Player Player; }
        // a raven's own voice (from its caw number, Voice): five heights, each a little spread, and the caw's timbre on the
        // words, light enough that every word stays clear: hoarse · muffled · thin
        internal static readonly float[] OwnPitches = { 0.91f, 0.955f, 1f, 1.045f, 1.09f };
        internal static float OwnSpread = 0.015f;
        internal static float HoarseGrit = 0.18f, HoarseCut = 7000f, MuffledCut = 3400f, ThinCut = 420f;
        // (all of it light: the voice stays clear and natural — a little quieter, a short slap of stone, not a hall)
        internal static float QuietVolume = 0.75f, QuietHighPass = 250f, LoudVolume = 1.15f, LoudPitch = 1.03f, DroopPitch = 0.95f;
        internal static float FightSeconds = 6f, MeadSeconds = 150f, EchoDelay = 65f, EchoDecay = 0.15f, EchoWet = 0.18f;
        internal static float CallFrom = 10f, CallVolume = 1.2f, CallPitch = 1.04f, CallReach = 1.6f; // m off its owner: it calls
        internal static float Tint = 0.03f; // each player's Hugin and Munin: their own pitch, up to this much off
        internal static float CawAfterH = 0.1f, CawAfterM = 0.2f; // a caw of its own after a line now and then (never when quiet)
        internal static float NodChance = 0.3f; // the other raven on you agrees now and then (a nod, a look, a soft croak)

        private static Mood MoodOf(GameObject talker, bool munin)
        {
            Mood m; m.Volume = 1f; m.Pitch = 1f; m.HighPass = 0f; m.Echo = false; m.Wobble = false; m.Quiet = false; m.Calling = false; m.Timbre = 0; m.Own = 1f;
            ShoulderLife life = talker.GetComponent<ShoulderLife>();
            Player p = life != null ? life.Player : talker.GetComponent<Player>();
            if (life == null && p != null) life = Shoulder.LifeOf(p, munin);
            m.Life = life; m.Player = p;
            int code = CodeOf(p, munin, talker);
#if RAVEN_ITEM
            if (code >= 0) // the raven's own: its caw's pitch, narrowed for words, and the caw's timbre, softened
            {
                Voice.Sound s = Voice.Decode(code);
                int step = s.Pitch < 0.85f ? 0 : s.Pitch < 0.95f ? 1 : s.Pitch < 1.05f ? 2 : s.Pitch < 1.2f ? 3 : 4;
                m.Pitch = OwnPitches[step] * (1f + OwnSpread * (((int)((Stir(code) >> 8) % 201u)) - 100) / 100f);
                m.Timbre = s.Timbre; m.Own = m.Pitch;
            }
#endif
            if (p == null) return m; // (a raven on a perch, a big raven, the Guide: as it is)
            if (code < 0) m.Pitch = 1f + Tint * ((int)((uint)(p.GetPlayerID() * 2 + (munin ? 1 : 0)).GetHashCode() % 201u) - 100) / 100f;
            bool mine = ReferenceEquals(p, Player.m_localPlayer);
            if ((life != null && life.Crouched) || (mine ? Shadow.On(p) : Shadow.Flagged(p))) { m.Quiet = true; m.Volume = QuietVolume; m.HighPass = QuietHighPass; }
            else if (life != null && (life.transform.position - p.transform.position).sqrMagnitude > CallFrom * CallFrom) { m.Calling = true; m.Volume = CallVolume; m.Pitch *= CallPitch; }
            else if ((mine && Time.time - Combat.SwingAt < FightSeconds) || (Quirks.Raining() && Quirks.Env().Contains("thunder") && !p.InShelter())) { m.Volume = LoudVolume; m.Pitch *= LoudPitch; }
            if (life != null && life.Droopy) m.Pitch *= DroopPitch;
            m.Wobble = mine && Time.time - Combat.DrankAt < MeadSeconds;
#if RAVEN_ITEM
            if (Mead.Of(p, munin) > 0f) m.Wobble = true; // a raven tipsy on its owner's mead, in every game
#endif
            m.Echo = RavenSkills.UnderRoof(p);
            return m;
        }

        // its own caw a moment after the line, in its voice, a little softer (the beak opens with it)
        // (a line cut short by the other raven: no caw after it)
        private static bool CutShort(GameObject line) { return line == null || line.GetComponent<VoiceFade>() != null; }
        private static System.Collections.IEnumerator CawAfter(GameObject line, bool munin, Mood m, float len)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, len - 0.1f));
            if (CutShort(line)) yield break;
            Transform on = line.transform.parent;
            Vector3 at = line.transform.position - (on != null ? on.position : Vector3.zero);
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.25f, 0.55f));
            if (on == null) yield break;
            ScreamFx.CawAt(on, on.position + at, m.Pitch / m.Own, 0.55f, true, m.Player != null ? Voice.CodeOn(m.Player, munin) : -1);
            if (m.Life != null) m.Life.Talk();
        }

        // the other raven while one speaks: somewhere in the middle of the line a nod, a look at it, or a soft croak
        private static System.Collections.IEnumerator Nod(GameObject line, ShoulderLife other, Transform speaker, bool munin, float len)
        {
            yield return new WaitForSeconds(len * UnityEngine.Random.Range(0.35f, 0.75f));
            if (CutShort(line) || other == null || speaker == null || other.Hovering || (other.transform.position - speaker.position).sqrMagnitude > 9f) yield break; // (both on you, near each other)
            float r = UnityEngine.Random.value;
            if (r < 0.7f && other.SitNew && other.SitDirectorNow != null) other.SitDirectorNow.React(other, r < 0.4f ? "Киває в такт" : "Дивиться на іншого", 0f);
            else if (other.Player != null) { ScreamFx.CawAt(other.transform, other.transform.position + Vector3.up * 0.1f, 1.05f, 0.3f, true, Voice.CodeOn(other.Player, munin)); other.Talk(); }
        }

        // a big raven that flies off mid-line falls silent: its voice line goes with it. Lives while a line may be
        // coming or playing (Until), then goes; the next line puts it back
        internal sealed class BigRavenHush : MonoBehaviour
        {
            internal float Until;
            private Raven _r;
            private float _next;
            private void Awake() { _r = GetComponent<Raven>(); }
            private void Update()
            {
                if (Time.time < _next) return;
                _next = Time.time + 0.2f;
                if (_r == null || Time.time > Until) { Destroy(this); return; }
                if (Plugin.IsSpawned(_r) && !Plugin.IsFlying(_r)) return;
                for (int i = 0; i < transform.childCount; i++) { Transform t = transform.GetChild(i); if (t.name == "RavenWatch_VoiceLine") UnityEngine.Object.Destroy(t.gameObject); }
            }
        }

        // a line's loudness, LoudPerSecond times a second (0 in the pauses, 1 on its loudest syllable): its raven's beak
        // follows it (ShoulderLife.SayBeak). Made once with the clip, from the samples already at hand
        internal const float LoudPerSecond = 30f;
        private static readonly Dictionary<AudioClip, float[]> Loudness = new Dictionary<AudioClip, float[]>();
        private static float[] LoudOf(float[] s, int rate)
        {
            int win = Math.Max(1, (int)(rate / LoudPerSecond)), n = s.Length / win + 1;
            float[] l = new float[n + 1];
            float top = 0f;
            for (int i = 0; i < n; i++)
            {
                float peak = 0f;
                for (int k = i * win, e = Math.Min(s.Length, k + win); k < e; k++) { float v = s[k] < 0f ? -s[k] : s[k]; if (v > peak) peak = v; }
                l[i] = peak; if (peak > top) top = peak;
            }
            if (top <= 0f) return l;
            for (int i = 0; i < n; i++) { float v = l[i] / top; l[i] = v < 0.12f ? 0f : Mathf.Pow((v - 0.12f) / 0.88f, 0.7f); }
            return l;
        }

        private static AudioClip MakeClip(float[] s, int rate)
        {
            if (Loudness.Count > 32) { List<AudioClip> gone = null; foreach (AudioClip c in Loudness.Keys) if (c == null) { if (gone == null) gone = new List<AudioClip>(); gone.Add(c); } if (gone != null) foreach (AudioClip c in gone) Loudness.Remove(c); } // (clips dropped unplayed)
            AudioClip made = MakeClipRaw(s, rate);
            Loudness[made] = LoudOf(s, rate);
            return made;
        }

        private static AudioClip MakeClipRaw(float[] s, int rate)
        {
            // filled through the reader callback: SetData has a Span overload the C# 5 compiler cannot resolve here
            int n = s.Length, at = 0;
            return AudioClip.Create("RavenVoice", n, 1, rate, false, buf => { int k = Math.Min(buf.Length, n - at); Array.Copy(s, at, buf, 0, k); for (int i = k; i < buf.Length; i++) buf[i] = 0f; at += k; });
        }

        // warm the voice up as soon as a player is in the world, so the first line does not wait for the load
        internal static void Warm() { if (Player.m_localPlayer != null) Running(); }

        internal static void Stop()
        {
            bool was = _started;
            _started = false;
            if (was && _engine != null) { try { _engine.Stop(); } catch { } }
            Flush();
        }
    }

    // the words of a line open one by one along its voice: each word gets a share of the sound by its syllables, plus a
    // pause after a comma or a full stop; the name before «:» is there from the start. The text keeps its full layout
    // (maxVisibleCharacters), so the capsule does not grow while it opens.
    internal sealed class WordReveal : MonoBehaviour
    {
        private TMP_Text _t;
        private int _head;
        private int[] _ends;
        private float[] _at;
        private float _start, _len;

        internal static void Begin(TMP_Text t, float seconds)
        {
            if (t == null || seconds <= 0f) return;
            WordReveal r = t.GetComponent<WordReveal>();
            if (r == null) r = t.gameObject.AddComponent<WordReveal>();
            r.Set(t, seconds);
        }

        private void Set(TMP_Text t, float seconds)
        {
            _t = t; _start = Time.time; _len = seconds;
            t.ForceMeshUpdate();
            TMP_TextInfo info = t.textInfo;
            int n = info.characterCount;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(n);
            for (int i = 0; i < n; i++) sb.Append(info.characterInfo[i].character);
            string s = sb.ToString();
            int colon = s.IndexOf(": ");
            _head = colon >= 0 && colon < 40 ? colon + 2 : 0;
            List<int> ends = new List<int>(); List<float> weight = new List<float>();
            int at = _head;
            while (at < n)
            {
                while (at < n && char.IsWhiteSpace(s[at])) at++;
                if (at >= n) break;
                int e = at, vowels = 0;
                while (e < n && !char.IsWhiteSpace(s[e])) { if ("аеєиіїоуюяАЕЄИІЇОУЮЯaeiouyAEIOUY".IndexOf(s[e]) >= 0) vowels++; e++; }
                char last = s[e - 1];
                float pause = last == ',' ? 1.2f : last == '…' ? 2.4f : ".!?".IndexOf(last) >= 0 ? 2f : last == '—' || last == ':' ? 1.5f : 0f;
                ends.Add(e); weight.Add(Mathf.Max(1, vowels) + pause);
                at = e;
            }
            _ends = ends.ToArray(); _at = new float[_ends.Length];
            float total = 0f; foreach (float w in weight) total += w;
            float acc = 0f, speak = seconds * 0.92f; // the voice ends a little before its sound does
            for (int i = 0; i < _at.Length; i++) { _at[i] = total > 0f ? acc / total * speak : 0f; acc += weight[i]; }
            t.maxVisibleCharacters = _head;
        }

        // cut short: the words stop where the voice did, a dash after the last one shown
        internal static void Stop(TMP_Text t)
        {
            WordReveal r = t != null ? t.GetComponent<WordReveal>() : null;
            if (r == null) return;
            int vis = t.maxVisibleCharacters;
            r.enabled = false; // (not one more frame of it: it would hide the dash)
            Destroy(r);
            TMP_TextInfo info = t.textInfo;
            if (vis <= r._head || vis > info.characterCount) return;
            int at = info.characterInfo[vis - 1].index + info.characterInfo[vis - 1].stringLength; // (in the text with its tags)
            if (at > 0 && at <= t.text.Length) { t.text = t.text.Insert(at, "—"); t.maxVisibleCharacters = vis + 1; }
        }

        private void Update()
        {
            if (_t == null) { Destroy(this); return; }
            float e = Time.time - _start;
            int vis = _head;
            for (int i = 0; i < _at.Length; i++) if (e >= _at[i]) vis = _ends[i];
            _t.maxVisibleCharacters = vis;
            if (e > _len) { _t.maxVisibleCharacters = 99999; Destroy(this); }
        }
    }

    // a voice far away sounds dull: its highs are cut more the farther the listener (8 m: clear, 35 m: 2.2 kHz)
    internal sealed class VoiceMuffle : MonoBehaviour
    {
        private static AudioListener _ear;
        private static float _earLooked = -10f;
        internal float Top = 22000f; // the timbre's own cut: never brighter than this
        private AudioLowPassFilter _lp;
        private void Start() { _lp = GetComponent<AudioLowPassFilter>(); if (_lp != null) _lp.cutoffFrequency = Top; }
        private void Update()
        {
            if (_lp == null) return;
            // the listener looked for in the scene at most once a second (a search every frame stalls the game)
            if ((_ear == null || !_ear.isActiveAndEnabled) && Time.unscaledTime - _earLooked >= 1f) { _earLooked = Time.unscaledTime; _ear = UnityEngine.Object.FindAnyObjectByType<AudioListener>(); }
            if (_ear == null) return;
            float d = Vector3.Distance(_ear.transform.position, transform.position);
            _lp.cutoffFrequency = Mathf.Min(Top, Mathf.Lerp(22000f, 2200f, Mathf.InverseLerp(8f, 35f, d)));
        }
    }

    // a voice cut off: down to nothing in a tenth of a second (no click), then gone
    internal sealed class VoiceFade : MonoBehaviour
    {
        private AudioSource _src;
        private float _from, _t;
        private void Start() { _src = GetComponent<AudioSource>(); _from = _src != null ? _src.volume : 0f; }
        private void Update()
        {
            _t += Time.deltaTime;
            if (_src != null) _src.volume = _from * Mathf.Clamp01(1f - _t / 0.1f);
            if (_t >= 0.12f) Destroy(gameObject);
        }
    }

    // after your mead: the voice's pitch sways a little, slowly (only on such a line)
    internal sealed class VoiceWobble : MonoBehaviour
    {
        internal static float Amount = 0.02f, Hz = 0.5f;
        private AudioSource _src;
        private float _base, _phase;
        private void Start() { _src = GetComponent<AudioSource>(); _base = _src != null ? _src.pitch : 1f; _phase = UnityEngine.Random.value * 6.28f; }
        private void Update() { if (_src != null) _src.pitch = _base * (1f + Amount * Mathf.Sin(_phase + Time.time * Hz * 6.2832f)); }
    }

    internal sealed class VoiceDriver : MonoBehaviour
    {
        private bool _warmed;
        private void Update()
        {
            if (!_warmed && Player.m_localPlayer != null) { _warmed = true; RavenVoice.Warm(); }
            RavenVoice.Tick();
        }
        private void OnApplicationQuit() { RavenVoice.Stop(); }
        private void OnDestroy() { RavenVoice.Stop(); }
    }
}
