#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Smooth by default (the user 2026-11-02): whatever leaves the picture — a person, a fire, a light — goes slowly and
    // never just blinks out. Only where a quick end is really wanted is a shorter time asked for, by name, in its place.
    //   a person    NpcFade: see-through, then gone (its owner takes it away at the end); Fades.Person s
    //   a fire      FireFade: its lights dim, its sparks stop and die out, its crackle quietens; Fades.Fire s
    // The story's named sounds go with such moments (StorySounds: «whisper» as a ghost fades), each game its own.
    internal static class Fades
    {
        internal static float Person = 3f;
        internal static float Fire = 5f;
    }

    // A fire going out slowly (the ghost's hearth when the ghost is gone: HallState.LitWhile). It keeps burning for the
    // game the while (the caller holds IsBurning), its look fading; at the end the caller lets it go out and every
    // light, spark and sound is put back as it was (a fire lit again later burns as before).
    internal sealed class FireFade : MonoBehaviour
    {
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<float> _bright = new List<float>();
        private readonly List<AudioSource> _sounds = new List<AudioSource>();
        private readonly List<float> _loud = new List<float>();
        private readonly List<Behaviour> _keepers = new List<Behaviour>(); // (the game's flicker and distance fade: they set the light themselves)
        private float _from, _for;
        private Action _done;

        internal static bool Going(Fireplace f) { return f != null && f.GetComponent<FireFade>() != null; }

        internal static void Out(Fireplace f, float seconds, Action done)
        {
            if (f == null || Going(f)) return;
            FireFade k = f.gameObject.AddComponent<FireFade>();
            k._from = Time.time; k._for = Mathf.Max(0.1f, seconds); k._done = done;
            GameObject fire = f.m_enabledObject != null ? f.m_enabledObject : f.gameObject;
            foreach (LightFlicker b in fire.GetComponentsInChildren<LightFlicker>(true)) if (b.enabled) { b.enabled = false; k._keepers.Add(b); }
            foreach (LightLod b in fire.GetComponentsInChildren<LightLod>(true)) if (b.enabled) { b.enabled = false; k._keepers.Add(b); }
            foreach (Light l in fire.GetComponentsInChildren<Light>(true)) { k._lights.Add(l); k._bright.Add(l.intensity); }
            foreach (AudioSource a in fire.GetComponentsInChildren<AudioSource>(true)) { k._sounds.Add(a); k._loud.Add(a.volume); }
            foreach (ParticleSystem ps in fire.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); // (what flies dies out by itself)
        }

        // lit again while going out: back as it was at once
        internal static void Cancel(Fireplace f)
        {
            FireFade k = f != null ? f.GetComponent<FireFade>() : null;
            if (k == null) return;
            k.Restore();
            foreach (ParticleSystem ps in (f.m_enabledObject != null ? f.m_enabledObject : f.gameObject).GetComponentsInChildren<ParticleSystem>(true)) ps.Play(false);
            Destroy(k);
        }

        private void Update()
        {
            float k = 1f - Mathf.Clamp01((Time.time - _from) / _for);
            float e = k * k; // (slower at the end: the last embers)
            for (int i = 0; i < _lights.Count; i++) if (_lights[i] != null) _lights[i].intensity = _bright[i] * e;
            for (int i = 0; i < _sounds.Count; i++) if (_sounds[i] != null) _sounds[i].volume = _loud[i] * k;
            if (k > 0f) return;
            Restore();
            Action done = _done; _done = null;
            Destroy(this);
            if (done != null) done();
        }

        private void Restore()
        {
            for (int i = 0; i < _lights.Count; i++) if (_lights[i] != null) _lights[i].intensity = _bright[i];
            for (int i = 0; i < _sounds.Count; i++) if (_sounds[i] != null) _sounds[i].volume = _loud[i];
            foreach (Behaviour b in _keepers) if (b != null) b.enabled = true;
            _keepers.Clear();
        }
    }

    // The story's named sounds: assets/story/sounds/*.txt (built in; the author's in the mod's own folder), a line each
    // «name a b …» — the first of them the game has is played. Each is a game sound (sfx_…) or a creature's own sound
    // «Creature/kind» (Ghost/idle, Wraith/alert, Ghost/death…: its effect list m_<kind>Sound / m_<kind>Effects /
    // m_<kind>edEffects) — the creatures' sounds are loaded with them, a lone sfx_ name often is not. A scene says «sound whisper npc.echo»; a ghost
    // fading says it by its fade. Each game plays it itself, where it happens (heard by whoever is near).
    internal static class StorySounds
    {
        internal const string Kind = "sounds";
        private static Dictionary<string, string[]> _all;

        internal static void Again() { _all = null; }

        // «Ghost/idle»: that creature's own effect list of that kind, played here
        private static bool Creature(string what, Vector3 at)
        {
            int s = what.IndexOf('/');
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(what.Substring(0, s)) : null;
            if (prefab == null) return false;
            string kind = what.Substring(s + 1);
            foreach (Component c in prefab.GetComponents<Component>())
            {
                if (c == null) continue;
                foreach (string f in new[] { "m_" + kind + "Sound", "m_" + kind + "Effects", "m_" + kind + "edEffects" })
                {
                    System.Reflection.FieldInfo fi = c.GetType().GetField(f, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.FlattenHierarchy);
                    EffectList el = fi != null ? fi.GetValue(c) as EffectList : null;
                    if (el == null || !el.HasEffects()) continue;
                    el.Create(at, Quaternion.identity);
                    return true;
                }
            }
            return false;
        }

        private static Dictionary<string, string[]> All()
        {
            if (_all != null) return _all;
            _all = new Dictionary<string, string[]>();
            foreach (KeyValuePair<string, string> f in StoryFiles.All(Kind, null, false))
                foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(f.Value))
                {
                    string[] w = ln.Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (w.Length >= 2) { string[] s = new string[w.Length - 1]; Array.Copy(w, 1, s, 0, s.Length); _all[w[0]] = s; }
                }
            return _all;
        }

        // a story name or a game sound (sfx_…) played here; false — none of them in this game
        internal static bool Play(string name, Vector3 at)
        {
            if (string.IsNullOrEmpty(name) || name == "-") return false;
            string[] list;
            if (!All().TryGetValue(name, out list)) list = new[] { name };
            foreach (string sfx in list) if (sfx.IndexOf('/') > 0 ? Creature(sfx, at) : RavenFx.Play(sfx, at, Quaternion.identity)) return true;
            Npcs.Log("story sound «" + name + "»: none of " + string.Join(", ", list) + " in this game");
            return false;
        }
    }
}
#endif
