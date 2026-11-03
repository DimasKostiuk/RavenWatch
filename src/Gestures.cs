#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // The director of your own gestures with the ravens (the user, 2026-10-28: «малий режисер»). Every gesture on the
    // player's gesture layer (slot 2) asks here: who may play now, who waits, who cuts in.
    //   - weight: Ambient (a stroke, warming your hands) < Answer (an order, feeding, a call, the fireside) < React (a
    //     peck, a theft, a splash) < Moment (a boss down; also anything playing on slot 2 not asked here: a gift, the Fate stone)
    //   - a heavier one cuts a lighter one off (soft fade); an Answer or heavier finding the layer busy waits up to
    //     QueueWait for it (one waiting, the heaviest kept); Ambient never waits
    //   - Ambient only after a quiet AmbientGap since the last gesture, and only standing calm
    //   - your body free for it: not swimming, riding, at a helm or in a chair, mid-dodge, in an emote, sitting; while
    //     running only a React or a Moment (Ambient/Answer wait or drop)
    //   - never in Odin's wings, mid-swing, while blocking; your own game only (others see it by your data)
    internal static class Gestures
    {
        internal enum Pri { Ambient = 0, Answer = 1, React = 2, Moment = 3 }

        internal static float QueueWait = 1.5f, AmbientGap = 8f, RunSpeed = 4.5f, Fade = 0.2f;

        private static Pri _cur;
        private static float _until = -1f, _endedAt = -999f;
        private static Ask _wait;

        private sealed class Ask { public bool Munin, Sided, Seated; public string Clip; public Pri Pri; public float Until; }

        // a gesture: clip + "r"/"l" by the raven's side (sided), else the clip as it is; seated: a clip made for sitting
        // (the fireside), allowed in a chair or the sitting emote. True: it plays now.
        internal static bool Play(Player p, bool munin, string clip, Pri pri, bool sided = true, bool seated = false)
        {
            if (!Basic(p)) return false;
            BodyAnimator a = BodyAnimator.For(p);
            if (a == null) return false;
            // a raven on your arm (ArmPerch): only the arm's own gestures; a reaction or a moment sends it to the shoulder first
            if (ArmPerch.Holding(p) && !clip.StartsWith("arm_", System.StringComparison.Ordinal)) { if (pri < Pri.React) return false; ArmPerch.Down(p, null); }
            if (!BodyFree(p, pri, seated)) { if (pri >= Pri.Answer && !seated && Moving(p)) Queue(munin, clip, pri, sided, seated); return false; }
            bool busy = a.Playing(2);
            bool ours = busy && Time.time < _until;
            if (busy && (!ours || pri <= _cur)) // (a gift's or the stone's pose on it: as a Moment)
            {
                if (pri >= Pri.Answer) Queue(munin, clip, pri, sided, seated);
                return false;
            }
            if (pri == Pri.Ambient && Time.time < _endedAt + AmbientGap) return false; // (a quiet while after the last one)
            return Start(p, a, munin, clip, pri, sided);
        }

        // a story scene's pose: the scene has already taken the body, so nothing is asked of it, and nothing cuts in or
        // starts on the layer till the scene lets go (a clip after a clip of the scene: a crossfade)
        internal static bool Scene(Player p, string clip, float fade)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer) || BodyClips.Find(clip) == null) return false;
            Stand(p);
            if (ArmPerch.Holding(p)) ArmPerch.Down(p, null);
            BodyClips.Play(p, 2, clip, fade, 1f);
            _cur = Pri.Moment;
            _until = float.MaxValue;
            _wait = null;
            return true;
        }

        // a scene takes the body as it is: up from a crouch (the toggle too, or the game crouches him again), out of an emote
        // and off a seat — a clip is made on a body standing as the game stands it
        private static readonly System.Reflection.MethodInfo SetCrouchM = HarmonyLib.AccessTools.Method(typeof(Character), "SetCrouch"), StopEmoteM = HarmonyLib.AccessTools.Method(typeof(Player), "StopEmote");
        private static readonly System.Reflection.FieldInfo CrouchToggled = HarmonyLib.AccessTools.Field(typeof(Player), "m_crouchToggled");
        internal static void Stand(Player p)
        {
            if (p == null) return;
            if (p.IsCrouching() || (CrouchToggled != null && (bool)CrouchToggled.GetValue(p)))
            {
                if (CrouchToggled != null) CrouchToggled.SetValue(p, false);
                if (SetCrouchM != null) SetCrouchM.Invoke(p, new object[] { false });
            }
            StopEmote(p);
            if (p.IsAttached()) p.AttachStop();
        }

        internal static void StopEmote(Player p) { if (p != null && p.InEmote() && StopEmoteM != null) StopEmoteM.Invoke(p, null); }

        internal static void SceneEnd(Player p, float fade)
        {
            if (_until != float.MaxValue) return; // (no scene's pose on)
            _until = _endedAt = Time.time;
            if (p != null) BodyClips.Stop(p, 2, fade);
        }

        // from the skills' Update: the waiting one, once the layer and your body are free
        internal static void Tick()
        {
            if (_until > 0f && Time.time >= _until) { _endedAt = _until; _until = -1f; }
            Ask w = _wait;
            if (w == null) return;
            if (Time.time > w.Until) { _wait = null; return; }
            Player p = Player.m_localPlayer;
            if (!Basic(p) || !BodyFree(p, w.Pri, w.Seated)) return;
            BodyAnimator a = BodyAnimator.For(p);
            if (a == null || (a.Playing(2) && (Time.time >= _until || w.Pri <= _cur))) return;
            _wait = null;
            Start(p, a, w.Munin, w.Clip, w.Pri, w.Sided);
        }

        private static bool Start(Player p, BodyAnimator a, bool munin, string clip, Pri pri, bool sided)
        {
            string name = sided ? clip + (RightSide(p, munin) ? "r" : "l") : clip;
            PAnimClip c = BodyClips.Find(name);
            if (c == null) return false;
            float speed = Random.Range(0.92f, 1.08f);
            BodyClips.Play(p, 2, name, Fade, speed);
            _cur = pri;
            _until = Time.time + c.Length / speed;
            if (_wait != null && _wait.Pri <= pri) _wait = null;
            return true;
        }

        private static void Queue(bool munin, string clip, Pri pri, bool sided, bool seated)
        {
            if (_wait != null && _wait.Pri > pri && Time.time < _wait.Until) return;
            _wait = new Ask { Munin = munin, Clip = clip, Pri = pri, Sided = sided, Seated = seated, Until = Time.time + QueueWait };
        }

        private static bool Basic(Player p)
        {
            return p != null && ReferenceEquals(p, Player.m_localPlayer) && !p.IsDead() && RavenSkills.Animations && !OdinWings.Flying() && !p.InAttack() && !p.IsBlocking();
        }

        private static bool BodyFree(Player p, Pri pri, bool seated)
        {
            if (p.InWater() || p.IsRiding() || p.InDodge()) return false;
            bool sits = Seated(p);
            if (seated != sits) return false; // (a standing clip never over a seat, a seated one never standing)
            if (!sits && (p.InEmote() || p.IsAttached())) return false; // (another emote, a saddle-like hold: the hands are busy)
            if (!sits && pri <= Pri.Answer && Moving(p)) return false; // (a bench on a sailing ship moves too: no running)
            return true;
        }

        // in a chair, on a bench, at a helm, or the sitting emote (as the fireside counts it; riding is no seat)
        internal static bool Seated(Player p) { return Cozy.Sitting(p); }

        private static bool Moving(Player p) { return p.GetVelocity().magnitude > RunSpeed; }

        internal static bool RightSide(Player p, bool munin)
        {
            GameObject m = Shoulder.ModelOf(p, munin);
            return m != null ? Vector3.Dot(m.transform.position - p.transform.position, p.transform.right) >= 0f : !munin;
        }
    }
}
#endif
