#if RAVEN_ITEM && RAVEN_STORY
using System;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The player's gait in a scene (scene step «player gait tired|none»; the kinds: assets/gaits.txt, BodyGait): outside scenes his walk is the
    // game's own, a character is his. The kind goes into his ZDO, so those near see it too; every game draws it itself, and a
    // scene that ends or stops takes it off (StoryScenes). A look at the ZDO four times a second, nothing while it says none.
    // His turns on the spot in a scene (BodyTurn) the same way: a word in his ZDO, every game draws them, outside scenes the game's own.
    [DefaultExecutionOrder(-900)] // (after his clips, BodyAnimator -1000; before whatever reads his bones: the ravens on him)
    internal sealed class StoryGait : MonoBehaviour
    {
        private static readonly int KGait = "RavenWatch_gait".GetStableHashCode(); // string: its kind's name ("": none)
        private static readonly int KTurns = "RavenWatch_sceneTurns".GetStableHashCode(); // bool: in a scene, his turns foot to foot
        private BodyTurn _turn;
        private bool _turns;

        // this player's own (the owner writes): a scene begins or ends
        internal static void Turns(Player p, bool on)
        {
            ZDO z = p != null ? NetFlag.Zdo(p, true) : null;
            if (z != null && z.GetBool(KTurns) != on) z.Set(KTurns, on);
        }
        private Player _p;
        private BodyGait _b;
        private BodyGait.Style _style;
        private string _kind = "";
        private float _next;

        // this player's own (the owner writes)
        internal static string Set(Player p, string word)
        {
            ZDO z = p != null ? NetFlag.Zdo(p, true) : null;
            if (z == null) return null;
            if (word == "none") { if (z.GetString(KGait, "").Length > 0) z.Set(KGait, ""); return null; }
            if (!BodyGait.Known(word)) return "player gait none|" + BodyGait.Kinds;
            z.Set(KGait, word.ToLowerInvariant());
            return null;
        }

        // his walk in a scene has ended: his feet settle (StoryWalk)
        internal static void Settle(Player p) { StoryGait g = p != null ? p.GetComponent<StoryGait>() : null; if (g != null && g._turn != null) g._turn.Settle(); }

        private void Awake() { _p = GetComponent<Player>(); if (_p != null) { _b = new BodyGait(this, _p); _turn = new BodyTurn(_p) { SettleOnStop = true }; } } // (his walks in a scene end on his own feet)

        private void LateUpdate()
        {
            if (_b == null) return;
            Gait();
            BodyAnimator clips = BodyAnimator.Of(_p);
            _turn.Pose(_turns && !(clips != null && clips.HoldsLegs) && !_p.InAttack() && !_p.IsDead() && !_p.IsAttached() && !_p.IsSitting() && !_p.IsSwimming() && !_p.IsCrouching() && _p.IsOnGround());
        }

        private void Gait()
        {
            if (Time.time >= _next)
            {
                _next = Time.time + 0.25f;
                ZDO z = _z = NetFlag.Zdo(_p, false);
                _turns = z != null && z.GetBool(KTurns);
                string kind = z != null ? z.GetString(KGait, "") : "";
                if (kind.Length > 0) BodyGait.Look(); // (the author's gaits file looked at only while one is worn)
                if (kind != _kind || _ver != BodyGait.Ver) { _kind = kind; _ver = BodyGait.Ver; _style = kind.Length > 0 ? BodyGait.Made(kind, 0.5f, 0.5f, 0f) : null; }
                bool every = _turns || _style != null;
                BodyAnimator anim = every ? BodyAnimator.For(_p) : BodyAnimator.Of(_p); // (said four times a second: a new animator hears it too; none made only to hear «not needed»)
                if (anim != null) anim.EveryFrame(BodyAnimator.WantGait, every); // (these lie on his bones: BodyAnimator.EveryFrame)
            }
            if (_style == null) { if (_was) { _b.Rest(); _was = false; } return; }
            if (!_b.Ready()) return;
            _was = true;
            BodyAnimator clips = BodyAnimator.Of(_p);
            ZDO zd = _z;
            bool off = (clips != null && clips.CodeBusy) || _p.InAttack() || _p.IsDead() || _p.IsAttached() || _p.IsSitting() || _p.IsSwimming() || _p.IsCrouching() || (zd != null && zd.GetString(ZDOVars.s_emote, "").Length > 0);
            _b.Pose(_style, 0f, null, off); // (legs 0: his speed is the game's; no wishes, no quiet life: his kind only)
        }
        private bool _was;
        private int _ver;
        private ZDO _z; // (his ZDO, read four times a second above: not each frame)
    }

    [HarmonyPatch(typeof(Player), "Awake")]
    internal static class StoryGaitPatch
    {
        private static void Postfix(Player __instance)
        {
            try { if (__instance.GetComponent<StoryGait>() == null) __instance.gameObject.AddComponent<StoryGait>(); }
            catch (Exception e) { PatchGuard.Fail("StoryGaitPatch", e); }
        }
    }
}
#endif
