#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Munin's scream, heard and seen by everyone nearby: the raven's own croak (lower and louder),
    // a burst of black feathers (the raven's vanishing puff, bigger) and a short shake of the camera.
    internal static class ScreamFx
    {
        internal const string RpcFx = "RavenWatch_ScreamFx";
        private const float ShakeRange = 20f;
        private static GameObject _sound;
        private static EffectList _soundList;
        private static bool _searched;

        internal const string GateSound = "sfx_metalgate_close";
        internal static float GateStretch = 2f;
        // and a crow's chatter backwards, at 0.9 of its speed (a little longer and lower)
        internal const string CrowSound = "sfx_crow_idle";
        internal static float CrowSpeed = 0.9f;
        internal static void Register() { RavenFx.Register(RpcFx, Spawn); }
        internal static void Play(Player p) { RavenFx.Broadcast(RpcFx, p, Spawn); }

        // A raven's voice from the game's sounds. Found once, and written to the log, so it is clear what plays.
        private static void FindSound(Raven munin)
        {
            if (_searched) return;
            _searched = true;
            List<string> ravenLike = new List<string>();
            if (ZNetScene.instance != null)
                foreach (GameObject g in ZNetScene.instance.m_prefabs)
                {
                    if (g == null) continue;
                    string n = g.name.ToLowerInvariant();
                    if (!n.StartsWith("sfx_") || g.GetComponent<ZNetView>() != null || g.GetComponentInChildren<AudioSource>(true) == null) continue;
                    if (n.Contains("raven") || n.Contains("crow") || n.Contains("bird")) ravenLike.Add(g.name);
                }
            foreach (string n in ravenLike) if (n.ToLowerInvariant().Contains("raven")) { _sound = ZNetScene.instance.GetPrefab(n); break; }
            if (_sound == null && munin != null && munin.m_idleEffect != null && munin.m_idleEffect.m_effectPrefabs != null)
                foreach (EffectList.EffectData d in munin.m_idleEffect.m_effectPrefabs)
                    if (d != null && d.m_prefab != null && d.m_prefab.GetComponentInChildren<AudioSource>(true) != null) { _sound = d.m_prefab; break; }
            if (_sound == null && ravenLike.Count > 0) _sound = ZNetScene.instance.GetPrefab(ravenLike[0]);
            if (_sound == null && ZNetScene.instance != null)
            {
                // no raven voice in the game: a drake hatchling's screech
                GameObject drake = ZNetScene.instance.GetPrefab("Hatchling");
                BaseAI ai = drake != null ? drake.GetComponent<BaseAI>() : null;
                if (ai != null && ai.m_alertedEffects != null && ai.m_alertedEffects.m_effectPrefabs != null && ai.m_alertedEffects.m_effectPrefabs.Length > 0) _soundList = ai.m_alertedEffects;
            }
            if (Plugin.Instance != null)
                Plugin.Instance.Log("Munin's scream sound: " + (_sound != null ? _sound.name : _soundList != null ? "drake hatchling alert" : "none found")
                    + ". Raven-like sounds in the game: " + (ravenLike.Count > 0 ? string.Join(", ", ravenLike.ToArray()) : "none"));
        }

        // the raven's voice at the player, with its own pitch and loudness (Munin's wings use it too)
        internal static void Caw(Player p, float pitch, float volume)
        {
            CawAt(p.transform, p.transform.position + Vector3.up * 1.4f, pitch, volume, false, -1);
        }

        // ... in this raven's own voice (Voice: its pitch, timbre, manner, rhythm and loudness, the same in every game)
        internal static void Caw(Player p, bool munin, float pitch, float volume)
        {
            CawAt(p.transform, p.transform.position + Vector3.up * 1.4f, pitch, volume, false, Voice.CodeOn(p, munin));
        }

        // the same voice from anywhere (a raven on a perch), carried along by 'on'; voice: the raven's number, -1 plain
        internal static void CawAt(Transform on, Vector3 chest, float pitch, float volume, bool voiceOnly) { CawAt(on, chest, pitch, volume, voiceOnly, -1); }
        internal static void CawAt(Transform on, Vector3 chest, float pitch, float volume, bool voiceOnly, int voice)
        {
            if (on == null) return;
            if (voice < 0) { One(on, chest, pitch, volume, voiceOnly, default(Voice.Sound), false); return; }
            Voice.Sound s = Voice.Decode(voice);
            pitch *= s.Pitch * (s.Manner == 2 ? 0.95f : 1f);
            volume *= s.Volume * Voice.Gain(s);
            One(on, chest, pitch, volume, voiceOnly, s, true);
            if (s.Count > 1 && Plugin.Instance != null) Plugin.Instance.StartCoroutine(Again(on, chest - on.position, pitch, volume, s)); // its «кар-кар»
        }

        private static System.Collections.IEnumerator Again(Transform on, Vector3 offset, float pitch, float volume, Voice.Sound s)
        {
            for (int i = 1; i < s.Count; i++)
            {
                yield return new WaitForSeconds(s.Gap);
                if (on == null) yield break;
                One(on, on.position + offset, pitch * (1f - 0.04f * i), volume * (1f - 0.12f * i), true, s, true); // each a touch lower and softer
            }
        }

        private static void One(Transform on, Vector3 chest, float pitch, float volume, bool voiceOnly, Voice.Sound s, bool voiced)
        {
            if (!_searched) FindSound(Shoulder.Template(true));
            GameObject[] made = null;
            if (_sound != null)
            {
                GameObject g = UnityEngine.Object.Instantiate(_sound, chest, on.rotation, on);
                if (g.GetComponent<TimedDestruction>() == null) UnityEngine.Object.Destroy(g, 4f); // a voice without its own end would stay on you
                if (voiceOnly) foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true)) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>(); if (pr != null) pr.enabled = false; } // only the voice: no feathers with it
                made = new GameObject[] { g };
            }
            else if (_soundList != null) made = RavenFx.Own(_soundList, chest, on.rotation, on, 1f, -1);
            if (made == null) return;
            foreach (GameObject g in made)
                if (g != null)
                    foreach (AudioSource a in g.GetComponentsInChildren<AudioSource>(true)) Voice.Shape(a, pitch, volume, s, voiced);
        }

        private static void Spawn(Player p)
        {
            try
            {
                Raven munin = Shoulder.Template(true);
                if (munin == null) return;
                Vector3 chest = p.transform.position + Vector3.up * 1.4f;

                // the croak: lower, louder, carried further than a raven's chatter
                Caw(p, true, 0.75f, 1f);
                RavenFx.PlayStretched(GateSound, chest, GateStretch); // and an iron gate slamming shut under it, slowed: twice as long
                SoundBack.PlayNamed(CrowSound, p.transform, 0f, 1f / Mathf.Max(0.1f, CrowSpeed));
                ShoulderLife.Moment(p, true, "scream", ShoulderLife.ScreamSeconds); // Munin's wings wide open

                // the feathers: the raven's vanishing puff, three times as big
                if (munin.m_despawnEffect != null) RavenFx.Own(munin.m_despawnEffect, chest, p.transform.rotation, null, 3f, -1);

                Player me = Player.m_localPlayer;
                if (me != null && GameCamera.instance != null && Vector3.Distance(me.transform.position, p.transform.position) < ShakeRange)
                    GameCamera.instance.AddShake(p.transform.position, ShakeRange, 1.5f, false);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Scream effect failed: " + e.Message); }
        }
    }
}
#endif
