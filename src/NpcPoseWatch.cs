#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The pose's eye for the black box (NpcWalk.Watch: the author's own game): after every bone has been put this frame
    // (the game's animation, then ours — gait, a fall, a meal, a scene), a few bones are looked at in the body's own frame
    // (the head, the hands, the shoulders, the hips): one whose step in a frame breaks its motion (Break times its pace of
    // late, and over PopAt at 60 frames a second — a hand swinging fast in the walk is no jump) is a jump of the pose — counted with what the person was doing (raven npc walk report) and
    // written to halts.txt. A body put somewhere (a jump, a seat) moves the whole frame: no pose jump.
    [DefaultExecutionOrder(30000)]
    internal sealed class NpcPoseWatch : MonoBehaviour
    {
        internal static float PopAt = 0.12f, Near = 40f, Break = 3f; // (Break: a frame's step this many times its pace of late)
        private static readonly string[] Names = { "Head", "LeftHand", "RightHand", "LeftShoulder", "RightShoulder", "Hips" };
        private NpcActor _a;
        private Transform[] _bones;
        private Vector3[] _was;
        private float[] _pace;
        private bool _set;
        private float _logAt;
        private NpcGait _gait;
        private SkinnedMeshRenderer _skin;
        private bool _seenNow, _seenWas;

        internal static void Ensure(NpcActor a)
        {
            if (!NpcWalk.Watch || a == null || a.Def == null || a.Def.Odin || Given.Contains(a)) return; // (a look into a set, not a component search each frame)
            a.gameObject.AddComponent<NpcPoseWatch>()._a = a;
            Given.Add(a);
        }
        private static readonly HashSet<NpcActor> Given = new HashSet<NpcActor>();
        private void OnDestroy() { if (_a != null) Given.Remove(_a); }

        // what else was going on in that frame: its turn (BodyTurn), our clip, and each layer of the game's animator (its state, a change under way):
        // a jump of the hand with no clip and no walk (Heid, 2026-11-04: «the hand jerks as she looks at me and turns») says here whose it is
        private string Why()
        {
            NpcPose pose = GetComponent<NpcPose>();
            BodyAnimator clips = BodyAnimator.Of(GetComponent<Character>());
            Animator anim = GetComponentInChildren<Animator>();
            System.Text.StringBuilder t = new System.Text.StringBuilder(pose != null ? pose.TurnTrace() : "turn -");
            t.Append(" · clip ").Append(clips != null ? clips.ClipIn(NpcBody.ClipSlot) ?? "-" : "-");
            NpcSpeech speech = GetComponent<NpcSpeech>();
            if (speech != null) t.Append(" · ").Append(speech.Now()); // (speaking, listening, where its head turns: NpcSpeech)
            if (anim != null)
                for (int l = 0; l < anim.layerCount; l++)
                    t.Append(" · layer ").Append(l).Append(" w").Append(anim.GetLayerWeight(l).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(" state ").Append(anim.GetCurrentAnimatorStateInfo(l).shortNameHash).Append(anim.IsInTransition(l) ? " changing" : "");
            return t.ToString();
        }

        private void LateUpdate()
        {
            Player me = Player.m_localPlayer;
            if (!NpcWalk.Watch || _a == null || me == null || Vector3.Distance(me.transform.position, transform.position) > Near) { _set = false; return; }
            if (_bones == null)
            {
                _bones = new Transform[Names.Length]; _was = new Vector3[Names.Length]; _pace = new float[Names.Length];
                Animator anim = GetComponentInChildren<Animator>();
                if (anim == null) { _bones = null; return; }
                for (int i = 0; i < Names.Length; i++) _bones[i] = BodyClips.SkeletonBone(this, anim.transform, Names[i]); // (the body's own bones — what it wears carries bones of the same names)
            }
            float limit = PopAt * Mathf.Max(1f, Time.deltaTime * 60f);
            if (_skin == null) { VisEquipment ve = GetComponent<VisEquipment>(); _skin = ve != null ? ve.m_bodyModel : GetComponentInChildren<SkinnedMeshRenderer>(); }
            _seenWas = _seenNow; _seenNow = _skin != null && _skin.isVisible;
            for (int i = 0; i < _bones.Length; i++)
            {
                Transform b = _bones[i];
                if (b == null) continue;
                Vector3 local = transform.InverseTransformPoint(b.position);
                float step = _set ? (local - _was[i]).magnitude : 0f;
                // a jump is a break in its motion, not a quick motion: a step far over the bone's own pace of late (a hand
                // swinging in the walk moves fast every frame — that is no jump), and over PopAt at the least
                bool jump = _set && step > limit && step > _pace[i] * Break;
                if (jump && !(_seenNow && _seenWas)) { jump = false; NpcWalk.Popped(_a.Name, "не на екрані"); } // (off the screen the game may skip its animation: a catch-up nobody sees — counted apart)
                if (_set) _pace[i] = Mathf.Lerp(_pace[i], step, 0.2f);
                if (jump)
                {
                    string doing = _a.StatusNow;
                    int cut = doing.IndexOfAny(new[] { ' ', '(', ',' });
                    NpcWalk.Popped(_a.Name, Names[i] + " · " + (cut > 0 ? doing.Substring(0, cut) : doing));
                    if (Time.time - _logAt > 1f) { _logAt = Time.time; NpcWalk.Write("halts.txt", System.DateTime.Now.ToString("HH:mm:ss") + "\t" + _a.Name + "\tPOSE " + Names[i] + " " + (local - _was[i]).magnitude.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m in a frame\t" + doing + "\t" + (_gait != null || (_gait = GetComponent<NpcGait>()) != null ? _gait.Trace() : "-") + "\t" + Why()); }
                }
                _was[i] = local;
            }
            _set = true;
        }
    }
}
#endif
