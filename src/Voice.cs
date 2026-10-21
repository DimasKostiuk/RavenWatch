#if RAVEN_ITEM
using System.Collections;
using UnityEngine;

namespace HuginWhispers
{
    // Each raven's own voice, told apart by ear: not a shade higher or lower, but a set of plain traits, each one heard on
    // its own (within limits, so a raven still sounds a raven):
    //   pitch   very low · low · plain · high · very high (×0.8 … ×1.26 of its kind's: Hugin stays above Munin)
    //   timbre  clean · hoarse · muffled · thin                (the game's own audio filters on the caw)
    //   manner  a clipped «кр!» · plain · drawn out with an echo
    //   rhythm  one caw · two · three (half the ravens caw once), its own pause between them
    //   loudness quiet · plain · loud (heard further)
    // All of it from one number made from the raven's own id (Backup.Id: it stays with the raven, through a return from
    // its copy too), so the same raven sounds the same in every game:
    //   - the wearer's game puts both ravens' numbers on the player, one int, written only when it changes (Publish);
    //   - a perch keeps its raven's number in the seat's record (field 8), for when the owner is not around;
    //   - every caw goes through ScreamFx.CawAt with that number.
    // A raven with no id yet, or a game that does not publish it: the plain voice.
    internal static class Voice
    {
        internal const string NetKey = "RavenWatch_Voice"; // Hugin's number | Munin's << 15 | 1 << 30 (published)
        private const int Set = 1 << 30, None = 0x7FFF, Kinds = 5 * 4 * 3 * 20 * 3 * 3; // 10800 voices

        internal struct Sound { public float Pitch, Volume, Gap; public int Count, Timbre, Manner; }

        internal const string ItemKey = "RavenWatch_voice"; // a voice set by hand (raven voice), kept in the raven itself
        private static string _hId, _mId;
        private static int _hCode = -1, _mCode = -1, _ver, _seenVer;

        // the raven's number, -1 when it has no id yet
        internal static int Code(ItemDrop.ItemData item)
        {
            string set; int v;
            if (item != null && item.m_customData.TryGetValue(ItemKey, out set) && int.TryParse(set, out v) && v >= 0 && v < Kinds) return v; // chosen by hand
            string id = item != null ? Backup.Id(item) : null;
            if (string.IsNullOrEmpty(id)) return -1;
            uint h = 2166136261u;
            foreach (char c in id) { h ^= c; h *= 16777619u; }
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;         // stirred, so near ids give far voices
            return (int)(h % Kinds);
        }

        // what the number means (each trait from its own part of it)
        internal static Sound Decode(int code)
        {
            Sound s = new Sound();
            int c = code;
            s.Pitch = Pitches[c % 5]; c /= 5;
            s.Timbre = c % 4; c /= 4;
            s.Manner = c % 3; c /= 3;
            int r = c % 20; c /= 20;
            s.Count = r < 10 ? 1 : r < 17 ? 2 : 3;
            s.Gap = Gaps[c % 3]; c /= 3;
            s.Volume = Volumes[c % 3];
            return s;
        }
        private static readonly float[] Pitches = { 0.8f, 0.89f, 1f, 1.12f, 1.26f };
        private static readonly float[] Gaps = { 0.2f, 0.28f, 0.4f };
        private static readonly float[] Volumes = { 0.75f, 1f, 1.2f }; // heard apart, but none lost nor deafening

        // the timbre and the manner change how loud a caw seems (a thin or muffled one loses its body, a hoarse one and an
        // echo add to it): evened out, so they change the character of the voice and the loudness stays its own trait
        internal static float Gain(Sound s)
        {
            float g = s.Timbre == 1 ? 0.8f : s.Timbre == 2 ? 1.3f : s.Timbre == 3 ? 1.3f : 1f;
            return s.Manner == 2 ? g * 0.85f : g;
        }

        // the number for these traits: pitch 0-4, timbre 0-3, manner 0-2, caws 1-3, pause 0-2, loudness 0-2
        internal static int Encode(int pitch, int timbre, int manner, int caws, int gap, int loud)
        {
            int r = caws <= 1 ? 0 : caws == 2 ? 10 : 17;
            return pitch + 5 * (timbre + 4 * (manner + 3 * (r + 20 * (gap + 3 * loud))));
        }

        // the wearer's game, in the twice-a-second sync: a hash only when a raven changes, a write only when the int does
        internal static void Publish(Player p, ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            string h = hugin != null ? Backup.Id(hugin) : null, m = munin != null ? Backup.Id(munin) : null;
            if (h != _hId || _ver != _seenVer) { _hId = h; _hCode = Code(hugin); }
            if (m != _mId || _ver != _seenVer) { _mId = m; _mCode = Code(munin); }
            _seenVer = _ver;
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            int v = (_hCode < 0 ? None : _hCode) | ((_mCode < 0 ? None : _mCode) << 15) | Set;
            if (z.GetInt(NetKey, 0) != v) z.Set(NetKey, v);
        }

        // the int a player carries for these two numbers (the console's test dummy writes it itself)
        internal static int Pack(int hugin, int munin) { return (hugin < 0 ? None : hugin) | ((munin < 0 ? None : munin) << 15) | Set; }
        internal static int RandomCode() { return UnityEngine.Random.Range(0, Kinds); }

        // the number of this player's raven as every game knows it, -1: not published
        internal static int CodeOn(Player p, bool munin)
        {
            ZDO z = p != null ? NetFlag.Zdo(p, false) : null;
            int v = z != null ? z.GetInt(NetKey, 0) : 0;
            if ((v & Set) == 0) return -1;
            int c = (v >> (munin ? 15 : 0)) & None;
            return c == None || c >= Kinds ? -1 : c;
        }

        // ---------------------------------------------------------------- shaping one caw (ScreamFx.CawAt)

        // pitch and loudness the game's way (its ZSFX sets them every frame from its own: a plain set would be undone),
        // the timbre by an audio filter, the manner by an echo or a quick fade
        internal static void Shape(AudioSource a, float pitch, float volume, Sound s, bool voiced)
        {
            ZSFX z = a.GetComponent<ZSFX>();
            if (z != null) { z.SetPitchModifier(pitch); z.SetVolumeModifier(volume); }
            else { a.pitch = pitch; a.volume = Mathf.Clamp01(volume); }
            a.maxDistance = Mathf.Max(a.maxDistance, voiced && s.Volume > 1f ? 90f : 60f);
            if (voiced && Probe >= 0) a.gameObject.AddComponent<Heard>().Begin(a, pitch, volume, Probe);
            if (!voiced) return;
            GameObject g = a.gameObject;
            switch (s.Timbre)
            {
                case 1: g.AddComponent<AudioDistortionFilter>().distortionLevel = 0.45f; g.AddComponent<AudioLowPassFilter>().cutoffFrequency = 4200f; break; // hoarse
                case 2: { AudioLowPassFilter f = g.AddComponent<AudioLowPassFilter>(); f.cutoffFrequency = 1300f; f.lowpassResonanceQ = 1.2f; break; } // muffled
                case 3: g.AddComponent<AudioHighPassFilter>().cutoffFrequency = 1200f; break; // thin
            }
            if (s.Manner == 2) { AudioEchoFilter e = g.AddComponent<AudioEchoFilter>(); e.delay = 110f; e.decayRatio = 0.5f; e.wetMix = 0.55f; e.dryMix = 1f; } // drawn out
            else if (s.Manner == 0 && a.clip != null) g.AddComponent<Clip>().Begin(z, a, volume, a.clip.length * 0.45f); // clipped short
        }

        // the console's caws (raven voice / voices) note in the log what the game really made of them a frame later
        internal static int Probe = -1; // the number being played from the console, -1: none
        private sealed class Heard : MonoBehaviour
        {
            private AudioSource _a; private float _pitch, _vol; private int _code, _frames;
            internal void Begin(AudioSource a, float pitch, float vol, int code) { _a = a; _pitch = pitch; _vol = vol; _code = code; }
            private void LateUpdate()
            {
                if (++_frames < 3) return;
                if (_a != null && Plugin.Instance != null)
                    Plugin.Instance.Log("Raven voice probe №" + _code + ": asked pitch " + _pitch.ToString("0.00") + " volume " + _vol.ToString("0.00")
                        + " | the source has pitch " + _a.pitch.ToString("0.00") + " volume " + _a.volume.ToString("0.00") + " (" + Describe(_code) + ")");
                Destroy(this);
            }
        }

        // a caw cut short with a quick fade (the game's ZSFX would undo a plain volume change)
        private sealed class Clip : MonoBehaviour
        {
            private ZSFX _z; private AudioSource _a; private float _vol, _at;
            internal void Begin(ZSFX z, AudioSource a, float vol, float cutIn) { _z = z; _a = a; _vol = vol; _at = Time.time + cutIn; }
            private void Update()
            {
                if (Time.time < _at) return; // the caw as it is until the cut
                float k = 1f - Mathf.Clamp01((Time.time - _at) / 0.07f);
                if (_z != null) _z.SetVolumeModifier(_vol * k); else if (_a != null) _a.volume = Mathf.Clamp01(_vol) * k;
                if (k <= 0f) { if (_a != null) _a.Stop(); enabled = false; }
            }
        }

        // ---------------------------------------------------------------- the console (raven voice)

        // raven voice                         both of yours: what they sound like, and a caw each
        // raven voice h|m <№>                  this voice for that raven (the numbers raven voices gives)
        // raven voice h|m random               a new one by chance
        // raven voice h|m reset                back to the voice its id gives
        // raven voice h|m p t m c g l          by traits: pitch 0-4, timbre 0-3, manner 0-2, caws 1-3, pause 0-2, loudness 0-2
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return Lang.T("Нема гравця.");
            if (a != null && a.Length >= 3 && a[1].ToLowerInvariant() == "play") // a number by ear, not given to anyone
            {
                int n;
                if (!int.TryParse(a[2], out n) || n < 0 || n >= Kinds) return Lang.F("Номер голосу — від 0 до {0}.", (Kinds - 1));
                bool asMunin = a.Length > 3 && (a[3].ToLowerInvariant().StartsWith("m") || a[3].StartsWith("м"));
                if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(Later(p, asMunin, n, 0.1f));
                return (asMunin ? Lang.T("Як Мунін: ") : Lang.T("Як Хугін: ")) + Describe(n);
            }
            if (a != null && a.Length == 2 && a[1].ToLowerInvariant() == "play") return Lang.T("Послухати номер: raven voice play <№> [h|m].");
            if (a != null && a.Length == 2) // one raven: raven voice h | m
            {
                bool one = a[1].ToLowerInvariant().StartsWith("m") || a[1].StartsWith("м");
                int code = Code(RavenSeat.On(p, one));
                if (code < 0) return Lang.F("{0} не на плечі.", Plugin.RavenName(one));
                if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(Later(p, one, code, 0.1f));
                return Lang.F("{0}: {1}\n   манера: {2}", Plugin.RavenName(one), Describe(code), Manner.Describe(code, one));
            }
            if (a == null || a.Length < 3)
            {
                string said = "";
                for (int i = 0; i < 2; i++)
                {
                    bool m = i == 1;
                    int code = Code(RavenSeat.On(p, m));
                    if (code < 0) continue;
                    if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(Later(p, m, code, i * 2.5f));
                    said += Lang.F("{0}: {1}\n   манера: {2}", ((said.Length > 0 ? "\n" : "") + Plugin.RavenName(m)), Describe(code), Manner.Describe(code, m));
                }
                return said.Length > 0 ? Lang.F("{0}\nЗмінити: raven voice h|m <№ | random | reset | висота тембр манера кар пауза гучність>", said) : Lang.T("Немає ворона з ідентифікатором на плечі.");
            }
            bool munin = a[1].ToLowerInvariant().StartsWith("m") || a[1].StartsWith("м");
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null) return Lang.F("{0} не на плечі.", Plugin.RavenName(munin));
            string what = a[2].ToLowerInvariant();
            int v;
            if (what == "reset") item.m_customData.Remove(ItemKey);
            else
            {
                if (what == "random") v = Random.Range(0, Kinds);
                else if (a.Length >= 8)
                {
                    int[] t = new int[6];
                    for (int i = 0; i < 6; i++) if (!int.TryParse(a[2 + i], out t[i])) return Lang.T("Риси — шість чисел: висота 0-4, тембр 0-3, манера 0-2, кар 1-3, пауза 0-2, гучність 0-2.");
                    v = Encode(Mathf.Clamp(t[0], 0, 4), Mathf.Clamp(t[1], 0, 3), Mathf.Clamp(t[2], 0, 2), Mathf.Clamp(t[3], 1, 3), Mathf.Clamp(t[4], 0, 2), Mathf.Clamp(t[5], 0, 2));
                }
                else if (!int.TryParse(what, out v) || v < 0 || v >= Kinds) return Lang.F("Номер голосу — від 0 до {0}, або random, reset, або шість рис.", (Kinds - 1));
                item.m_customData[ItemKey] = v.ToString();
            }
            _ver++; // published at the next look
            int now = Code(item);
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(Later(p, munin, now, 0.2f));
            return Lang.F("{0} тепер: {1}", Plugin.RavenName(munin), Describe(now));
        }

        // raven voices h|m [n]: n voices by chance, one after another, each told in the console with its number
        internal static string Samples(Player p, string[] a)
        {
            if (p == null || Plugin.Instance == null) return Lang.T("Нема гравця.");
            bool munin = a != null && a.Length > 1 && (a[1].ToLowerInvariant().StartsWith("m") || a[1].StartsWith("м"));
            int n = 5;
            if (a != null && a.Length > 2) int.TryParse(a[2], out n);
            n = Mathf.Clamp(n, 1, 12);
            Plugin.Instance.StartCoroutine(Sample(p, munin, n));
            return Lang.F("Слухай {0} голосів {1}: кожен буде описаний тут, з номером для raven voice.", n, (munin ? Lang.T("Муніна") : Lang.T("Хугіна")));
        }

        private static IEnumerator Sample(Player p, bool munin, int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (p == null) yield break;
                int code = Random.Range(0, Kinds);
                if (Console.instance != null) Console.instance.AddString((i + 1) + ". " + Describe(code));
                Probe = code;
                ScreamFx.CawAt(p.transform, p.transform.position + Vector3.up * 1.4f, munin ? 0.85f : 1.3f, 0.8f, true, code);
                Probe = -1;
                Sound s = Decode(code);
                yield return new WaitForSeconds(2.2f + s.Count * s.Gap);
            }
        }

        internal static string Describe(int code)
        {
            Sound s = Decode(code);
            string[] pitch = { Lang.T("дуже низький"), Lang.T("низький"), Lang.T("звичайна висота"), Lang.T("високий"), Lang.T("дуже високий") };
            string[] timbre = { Lang.T("чистий"), Lang.T("хрипкий"), Lang.T("глухий"), Lang.T("тонкий") };
            string[] manner = { Lang.T("обірване «кр!»"), Lang.T("звичайне «кар»"), Lang.T("протяжне з відлунням") };
            string[] count = { "", Lang.T("одне кар"), Lang.T("кар-кар"), Lang.T("кар-кар-кар") };
            string[] loud = { Lang.T("тихий"), Lang.T("звичайна гучність"), Lang.T("гучний") };
            return pitch[System.Array.IndexOf(Pitches, s.Pitch)] + ", " + timbre[s.Timbre] + ", " + manner[s.Manner] + ", " + count[s.Count]
                + ", " + loud[System.Array.IndexOf(Volumes, s.Volume)] + " (№" + code + ")";
        }

        private static IEnumerator Later(Player p, bool munin, int code, float wait)
        {
            if (wait > 0f) yield return new WaitForSeconds(wait);
            if (p != null) { Probe = code; ScreamFx.CawAt(p.transform, p.transform.position + Vector3.up * 1.4f, munin ? 0.85f : 1.3f, 0.8f, true, code); Probe = -1; } // this voice now, not the one still published
        }
    }
}
#endif
