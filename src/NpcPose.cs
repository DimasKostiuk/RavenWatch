#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // A person's pose, in one pass (the user 2026-11-02: roots, not patches). The game animates the body; then its look-at
    // turns the head; then this puts our layers on its bones, always in this order, each once a frame:
    //   clips  our own clips (BodyAnimator, Driven: the player's own player of clips — gestures, a laugh, a toast;
    //          NpcBody.Clip plays them where it is owned, its ZDO brings them to the other games, read here)
    //   gait   its own carriage and step (NpcGait) — eased in and out, never snapped; it gives way while a clip plays
    //   fall   lying senseless, the fall, the getting up (NpcFall) — over the gait
    //   meal   the bite raised to its mouth (NpcEat) — the last, its hand and head where the rest left them
    // Before this every layer had its own end of the frame in no set order, and what read a bone (a raven on a shoulder)
    // read it before some of them: a shoulder that jumped. Whatever reads the pose runs after this (a later order:
    // NpcRaven 20000, the black box's eye 30000). A new layer of the pose: its Pose() called here, in its place.
    [DefaultExecutionOrder(10000)]
    internal sealed class NpcPose : MonoBehaviour
    {
        private NpcGait _gait;
        private NpcFall _fall;
        private NpcEat _eat;
        private Humanoid _body;
        private ZNetView _nv;
        private BodyAnimator _clips;
        private string _clipsSeen = "";
        private float _clipsLook;
        private bool _ownWas;

        private bool _looked;

        private void LateUpdate()
        {
            if (!_looked) { _looked = true; _gait = GetComponent<NpcGait>(); _fall = GetComponent<NpcFall>(); _eat = GetComponent<NpcEat>(); _body = GetComponent<Humanoid>(); _nv = GetComponent<ZNetView>(); } // (on the body made: all its parts there by now)
            Clips();
            if (_gait != null) _gait.Pose();
            if (_fall != null) _fall.Pose();
            if (_eat != null) _eat.Pose();
        }

        // its clips: on another game what plays on it comes from its ZDO (five times a second); here they are stepped
        private void Clips()
        {
            if (_body == null) return;
            if (_nv != null && _nv.IsValid())
            {
                bool own = _nv.IsOwner();
                if (own ? !_ownWas : Time.time >= _clipsLook) // (run elsewhere: as its owner tells; come to run it here — a restart, its owner gone: once, what its ZDO says is on it, so that it can be stopped)
                {
                    _clipsLook = Time.time + 0.2f;
                    string code = _nv.GetZDO().GetString(BodyClips.NetKey, "");
                    if (code != _clipsSeen) { _clipsSeen = code; BodyClips.TakeFrom(_body, code); }
                }
                _ownWas = own;
            }
            if (_clips == null) { _clips = BodyAnimator.Of(_body); if (_clips == null) return; }
            _clips.Step();
        }
    }
}
#endif
