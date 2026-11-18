using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace HuginWhispers
{
    // The mod's messages in the middle of the screen stay longer than the game's own. The game shows a centre message
    // at full strength and starts fading it out over ~4 s right away; showing it again later made it jump back (a blink).
    // Instead the text is held at full strength, then faded out once, smoothly, unless something else took its place.
    // A title (Title): the game's «new biome» letters and sound, golden and glowing, at least TitleSeconds on the screen — a chapter's start (StorySaga.Look).
    // Put up by us, not through the game's queue: the same with another mod that takes that queue for its own screen (SeneaL UI) and without it.
    internal static class RavenMsg
    {
        private const float GameSeconds = 4f;  // how long the game's own centre message lasts
        private const float FadeSeconds = 2f;
        private static readonly FieldInfo CenterText = AccessTools.Field(typeof(MessageHud), "m_messageCenterText");
        private static string _text;
        private static float _holdUntil;

        // extra: how many times longer than the game's own (each ~4 s more)
        internal static void Center(Character who, string text, int extra = 1)
        {
            if (who == null || string.IsNullOrEmpty(text)) return;
            text = Grammar.Fix(text, who as Player); // ({male|female} forms: this player's)
            who.Message(MessageHud.MessageType.Center, text);
            if (who != Player.m_localPlayer || extra <= 0) { _text = null; return; }
            _text = text;
            _holdUntil = Time.unscaledTime + GameSeconds * (extra + 1) - FadeSeconds;
        }

        // A title (the user 2026-11-14: a chapter's start): the game's own «new biome» letters (MessageHud.m_biomeFoundPrefab: its look, its rise and fade)
        // and its sound, golden and glowing — put up by us, not through the game's queue: another mod that takes that queue for its own screen (SeneaL UI:
        // its banner, 6 s whatever is asked, our sizes cut out — the user 2026-11-17: «напис… так мало живе на екрані, він повинен бути там 10 с щонайменше,
        // і звук відповідно теж») never gets ours. The same title with that mod and without it.
        // On the screen TitleSeconds at least: its animation runs TitleSlow times slower to full sight, stands still there (Animator speed 0), and goes on
        // to fade so that rise, stand and fade come to TitleSeconds (never standing less than TitleStand). Full sight is seen, not known (the game's
        // animation is its own): how much of it shows (Seen: its text's alpha, its canvas groups', its scale) — rising stopped near full, or fallen from its
        // top; nothing of that in TitleIn s: it stands from there. Its course goes to the log once a title («title …: its sight each 0.5 s …»): the first
        // one in the game shows whether this read the animation right.
        // While it is up it holds the game's biome slot (m_biomeMsgInstance): the game puts no biome title of its own over it and ends it as its own once
        // its animation says «done»; a mod that skips the game's biome update leaves that to us (TitleTick: the same «done», or TitleSeconds + TitleCap).
        // Its sound: the game's «new biome» tune SoundSlow times as long (slower, so a little lower — the user 2026-11-14: «на 20% довше») and TitleEchoes
        // echoes of it, each EchoGap s after the one before, EchoVol times as loud and EchoSlow times slower again: it rings as long as the title stands.
        // The game has no longer tune of it (NewBiomeShort, 4.1 s: the only one in its files).
        // One at a time (TitleWaiting). Given only at a quiet moment of the game's messages (Quiet: the user 2026-11-16 — chapter 1 came up amid «Placed …»
        // after «Placed …»), else false and whoever asks tries again. All the numbers live: raven npc tune RavenMsg.<name>.
        private static readonly FieldInfo BiomeShown = AccessTools.Field(typeof(MessageHud), "m_biomeMsgInstance");
        private static readonly FieldInfo MsgQueue = AccessTools.Field(typeof(MessageHud), "m_msgQeue"), MsgQuiet = AccessTools.Field(typeof(MessageHud), "m_msgQueueTimer");
        private static bool Quiet(MessageHud h)
        {
            if (Hud.IsUserHidden()) return false; // (the game shows nothing then)
            if (BiomeShown != null && BiomeShown.GetValue(h) as GameObject != null) return false; // (a biome title of the game's own is up: ours after it)
            System.Collections.ICollection q = MsgQueue != null ? MsgQueue.GetValue(h) as System.Collections.ICollection : null;
            if (q == null || MsgQuiet == null) return true; // (the game changed: its messages not known — put up at once)
            return q.Count == 0 && (float)MsgQuiet.GetValue(h) > 2f;
        }
        internal static float TitleSeconds = 11f, TitleStand = 4f, TitleSlow = 2f, TitleIn = 3f, TitleCap = 20f;  // s on the screen at least · s standing at least · × slower · s to rise at most · s more before it is taken down whatever
        internal static float SoundSlow = 1.2f, EchoGap = 4.6f, EchoVol = 0.5f, EchoSlow = 1.08f;                // the tune × as long · s between its echoes · each × as loud · each × slower again
        internal static int TitleEchoes = 1;
        private const float Rising = 0.12f; // (sight a second: rising slower than this, it has stopped — per second, so the same at any frame rate)
        private static readonly Color TitleFace = new Color(1f, 0.85f, 0.5f, 1f), TitleGlow = new Color(1f, 0.68f, 0.18f, 0.85f);
        private static Material _gold, _goldFrom; // (the title's material made golden: one, made again only if the game's own changes)
        private static bool _soundTold; // (what the game's title sound is made of: in the log once a game)

        // the title up now: its object (Unity's null once its screen is gone), the one asked for, its text, its canvas groups and animation, when and in
        // which frame it came up, how much of it showed at its top and a frame ago (-1: not looked yet), when it began to stand (-1: still rising) and
        // when it went on to fade (0: not yet), its course for the log, whom to tell if it is taken off before its time
        private static readonly System.Collections.Generic.List<CanvasGroup> _upGroups = new System.Collections.Generic.List<CanvasGroup>();
        private static int _upFrame;
        private static System.Action _upLost;
        private static GameObject _up;
        private static bool _upOn;
        private static string _upName;
        private static MessageHud _upHud;
        private static TMP_Text _upText;
        private static Animator _upAnim;
        private static float _upAt, _top, _last, _standAt = -1f, _fadeAt, _sample;
        private static string _why;
        private static System.Text.StringBuilder _course;
        private static int _echoLeft, _echoN;
        private static float _echoAt;

        internal static bool TitleWaiting { get { return _upOn; } }

        // label: the small line over it (none: ""); under: a line under the name 4 px smaller (an act's name over its chapter's: the user 2026-11-17; none: "");
        // lost: called if it is taken off the screen before it stood its time (the player hid the HUD — the game takes its biome slot away then —, or
        // left the world): whoever asked may ask again
        private const int UnderSmaller = 4; // (px under the name's size, TMP's relative «<size=-N>»)
        internal static bool Title(string label, string name, string under = "", System.Action lost = null)
        {
            MessageHud h = MessageHud.instance;
            if (h == null || string.IsNullOrEmpty(name) || _upOn || !Quiet(h)) return false;
            string text = (string.IsNullOrEmpty(label) ? "" : "<size=55%>" + label + "</size>\n") + name + (string.IsNullOrEmpty(under) ? "" : "\n<size=-" + UnderSmaller + ">" + under + "</size>");
            if (Localization.instance != null) text = Localization.instance.Localize(text);
            TitleSound();
            if (h.m_biomeFoundPrefab == null) { Plugin.Instance.Log("title «" + name + "»: the game has no «new biome» title: given to its queue as it is"); h.ShowBiomeFoundMsg(text, false); return true; }
            GameObject made = Object.Instantiate(h.m_biomeFoundPrefab, h.transform);
            TMP_Text tx = null;
            foreach (TMP_Text x in made.GetComponentsInChildren<TMP_Text>(true)) if (tx == null || x.name == "Title") tx = x; // heavy: once a title, its few texts (its «Title», as the game names it; else its first)
            if (tx == null) { Object.Destroy(made); Plugin.Instance.Log("title «" + name + "»: the game's «new biome» title has no text: given to its queue as it is"); h.ShowBiomeFoundMsg(text, false); return true; }
            tx.text = text;
            Gild(tx);
            if (BiomeShown != null) BiomeShown.SetValue(h, made); // (the game's slot: none of its own over ours while it is up)
            _up = made; _upOn = true; _upName = name; _upHud = h; _upText = tx; _upLost = lost; _upFrame = Time.frameCount;
            _upAnim = made.GetComponentInChildren<Animator>(); // heavy: once a title, its few parts
            _upGroups.Clear();
            for (Transform t = tx.transform; t != null; t = t.parent) // (once a title: its few parents, up to its root)
            {
                CanvasGroup g = t.GetComponent<CanvasGroup>();
                if (g != null) _upGroups.Add(g);
                if (t == made.transform) break;
            }
            if (_upAnim != null) _upAnim.speed = 1f / TitleSlow; else Plugin.Instance.Log("title: the game's «new biome» title has no animation: it stands " + TitleSeconds + " s and is taken down");
            _upAt = Time.time; _top = 0f; _last = -1f; _standAt = _upAnim != null ? -1f : _upAt; _fadeAt = 0f; _sample = 0f; _why = _upAnim != null ? "" : "no animation";
            _course = new System.Text.StringBuilder();
            return true;
        }

        // how much of the title shows, 0..1: its text's alpha, the canvas groups from it up to the title's root, its scale against its screen's
        private static float Seen()
        {
            if (_upText == null || _up == null) return 0f;
            float a = _upText.color.a;
            foreach (CanvasGroup g in _upGroups) if (g != null) a *= g.alpha;
            Transform screen = _up.transform.parent;
            float of = screen != null ? Mathf.Abs(screen.lossyScale.y) : 1f;
            return of > 0f ? a * Mathf.Clamp01(Mathf.Abs(_upText.transform.lossyScale.y) / of) : a;
        }

        private static void TitleTick()
        {
            if (_echoLeft > 0 && Time.time >= _echoAt) { _echoN++; _echoLeft--; _echoAt = Time.time + EchoGap; Stinger(SoundSlow * Mathf.Pow(EchoSlow, _echoN), Mathf.Pow(EchoVol, _echoN)); }
            if (!_upOn) return;
            if (_up == null) { if (_fadeAt > 0f) TitleDown("the game took it down, its animation done"); else TitleDown("taken off the screen before its time (the HUD hidden, or left the world): it may come again", true); return; }
            if (Time.frameCount == _upFrame) return; // (not in the frame it was made: its animation has not run yet, and its prefab's own look would read as its top)
            float age = Time.time - _upAt, v = Seen();
            if (_last < 0f) _last = v;
            if (age >= _sample) { _course.Append(' ').Append(v.ToString("0.00")); _sample += 0.5f; }
            if (_standAt < 0f)
            {
                _top = Mathf.Max(_top, v);
                bool settled = age >= 0.3f; // (a rise: its first frames say nothing)
                string why = settled && v >= 0.97f && v - _last < Rising * Time.deltaTime ? "full" : settled && _top >= 0.8f && v < _top - 0.03f ? "past its top" : age >= TitleIn ? "not seen to rise in " + TitleIn + " s" : null;
                _last = v;
                if (why == null) return;
                _standAt = Time.time; _why = why;
                if (_upAnim != null) _upAnim.speed = 0f; // (stands as it is: the animation writes the same each frame)
                return;
            }
            float rose = _standAt - _upAt;
            if (_fadeAt == 0f)
            {
                if (Time.time - _standAt < Mathf.Max(TitleStand, TitleSeconds - 2f * rose)) return; // (its fade as long as its rise: the three come to TitleSeconds)
                _fadeAt = Time.time;
                if (_upAnim == null) { TitleDown("stood its time"); return; }
                _upAnim.speed = 1f / TitleSlow;
                return;
            }
            if (_upAnim.GetCurrentAnimatorStateInfo(0).IsTag("done")) { TitleDown("its animation done"); return; }
            if (age > TitleSeconds + TitleCap) TitleDown("not done " + TitleCap + " s after its time: taken down");
        }

        private static void TitleDown(string how, bool lost = false)
        {
            float age = Time.time - _upAt;
            Plugin.Instance.Log("title «" + _upName + "»: up " + age.ToString("0.0") + " s — rose " + (_standAt >= 0f ? (_standAt - _upAt).ToString("0.0") + " s (" + _why + ")" : "?")
                + ", stood " + (_fadeAt > 0f ? (_fadeAt - _standAt).ToString("0.0") + " s" : "?") + ", faded " + (_fadeAt > 0f ? (Time.time - _fadeAt).ToString("0.0") + " s" : "—")
                + "; " + how + "; its sight each 0.5 s:" + _course);
            if (_up != null) Object.Destroy(_up);
            if (_upHud != null && BiomeShown != null && BiomeShown.GetValue(_upHud) as GameObject == _up) BiomeShown.SetValue(_upHud, null); // (the slot back, if the game has not ended it itself)
            System.Action tell = lost ? _upLost : null;
            _up = null; _upOn = false; _upHud = null; _upText = null; _upAnim = null; _course = null; _upLost = null; _upGroups.Clear();
            if (lost) _echoLeft = 0; // (no echo of a title that is gone)
            if (tell != null) tell();
        }

        private static void Gild(TMP_Text tx)
        {
            Material from = tx.fontSharedMaterial;
            if (from == null) { Plugin.Instance.Log("title: the game's biome text has no material: shown plain"); return; }
            if (_gold == null || _goldFrom != from)
            {
                if (_gold != null) Object.Destroy(_gold);
                _gold = new Material(from) { hideFlags = HideFlags.HideAndDontSave }; _goldFrom = from;
                _gold.SetColor(ShaderUtilities.ID_FaceColor, TitleFace); // (times the text's own colour, which its animation may fade: gold over the game's light letters)
                _gold.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                _gold.SetColor(ShaderUtilities.ID_UnderlayColor, TitleGlow);
                _gold.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.8f);
                _gold.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.45f);
                _gold.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f); _gold.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            }
            _gold.SetTexture(ShaderUtilities.ID_MainTex, from.GetTexture(ShaderUtilities.ID_MainTex)); // (a dynamic font may have grown a new atlas)
            tx.fontSharedMaterial = _gold; tx.extraPadding = true;
        }

        // the title's sound: the tune now, its echoes from TitleTick
        private static void TitleSound()
        {
            Stinger(SoundSlow, 1f);
            _echoN = 0; _echoLeft = TitleEchoes; _echoAt = Time.time + EchoGap;
        }

        // the game's own «new biome» sound, slow times as long and vol times as loud: its sounds' pitch and volume (ZSFX keeps its own times these, every
        // frame); its own fades (ZSFX: in, out, the delay before it fades — seconds, not of its pitch) and the time it is destroyed after, if it has one,
        // stretched with it
        private static void Stinger(float slow, float vol)
        {
            MessageHud h = MessageHud.instance;
            if (h == null) { _echoLeft = 0; return; } // (left the world: no echo of it in the menu)
            GameObject from = h.m_biomeFoundStinger;
            if (from == null) { Plugin.Instance.Log("title: the game has no «new biome» sound: shown silent"); return; }
            GameObject made = Object.Instantiate(from);
            System.Text.StringBuilder told = _soundTold ? null : new System.Text.StringBuilder("title sound «" + from.name + "»:");
            foreach (ZSFX z in made.GetComponentsInChildren<ZSFX>()) // heavy: a few times a title (a chapter's start), the sound's few parts
            {
                z.SetPitchModifier(1f / slow); z.SetVolumeModifier(vol);
                z.m_fadeInDuration *= slow; z.m_fadeOutDuration *= slow; z.m_fadeOutDelay *= slow;
                if (told != null && z.m_fadeOutOnAwake) told.Append(" (fades out after " + (z.m_fadeOutDelay / slow).ToString("0.0") + " s over " + (z.m_fadeOutDuration / slow).ToString("0.0") + " s)");
                if (told != null) foreach (AudioClip c in z.m_audioClips) if (c != null) told.Append(" " + c.name + " " + c.length.ToString("0.0") + " s");
            }
            foreach (AudioSource a in made.GetComponentsInChildren<AudioSource>()) // heavy: as above
                if (a.GetComponent<ZSFX>() == null) { a.pitch /= slow; a.volume *= vol; if (told != null && a.clip != null) told.Append(" " + a.clip.name + " " + a.clip.length.ToString("0.0") + " s (plain)"); }
            foreach (TimedDestruction t in made.GetComponentsInChildren<TimedDestruction>()) // heavy: as above
            {
                if (told != null) told.Append(", gone after " + t.m_timeout.ToString("0.0") + " s" + (t.m_triggerOnAwake ? "" : " (when asked)"));
                if (!t.m_triggerOnAwake) continue;
                t.CancelInvoke("DestroyNow"); // (set going as it was made: again, as long as the sound now is)
                t.Trigger(t.m_timeout * slow);
            }
            if (told != null) { _soundTold = true; Plugin.Instance.Log(told.ToString()); }
        }

        // from the plugin's Update
        internal static void Tick()
        {
            TitleTick();
            if (_text == null || MessageHud.instance == null) return;
            TMP_Text shown = CenterText != null ? CenterText.GetValue(MessageHud.instance) as TMP_Text : null;
            if (shown == null) { _text = null; return; }
            if (shown.text != _text && (Localization.instance == null || shown.text != Localization.instance.Localize(_text))) { _text = null; return; } // another message is up now: leave it
            if (Time.unscaledTime < _holdUntil) { shown.CrossFadeAlpha(1f, 0f, true); return; } // over the game's own fade: still at full strength
            shown.CrossFadeAlpha(0f, FadeSeconds, true);
            _text = null;
        }
    }
}
