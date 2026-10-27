#if RAVEN_ITEM
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The game's big raven that brought our words (a quick raven, a visit, the Guide) and has said its piece: the game's
    // own "flyaway" is only a hop with a flap, then it is gone. Ours really flies off: a copy of it takes off by the
    // motor, as a raven leaves a shoulder or a perch, and the game's raven stays unseen until it is away.
    internal static class BigRavenLeave
    {
        internal static float Seconds = 3f; // in the air before the puff
        private static readonly MethodInfo UnderRoof = AccessTools.Method(typeof(Raven), "IsUnderRoof");

        // from the FlyAway prefix (the game asks every frame until the raven has left: once is enough)
        internal static void Go(Raven r, bool forceTeleport)
        {
            if (r == null || r.m_visual == null || forceTeleport || r.GetComponent<BigRavenHidden>() != null) return;
            if (!Plugin.SaidItsPiece(r)) return;
            if (UnderRoof != null && (bool)UnderRoof.Invoke(r, null)) return; // under a roof: the game's puff
            Transform v = r.m_visual.transform;
            GameObject b = Object.Instantiate(r.m_visual, v.position, v.rotation);
            b.transform.localScale = v.lossyScale;
            b.SetActive(true);
            AppearMute.Quiet(b);
            foreach (Collider c in b.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            Shared.RemoveExclamation(r, b);
            Shoulder.Perch(b.GetComponentInChildren<Animator>()); // (a copy starts hidden, "away")
            ShoulderFlyOff fly = Perches.FlyAway(b, r.m_isMunin, 1, Player.m_localPlayer, r.transform);
            if (fly != null) fly.Flight = Seconds;
            r.gameObject.AddComponent<BigRavenHidden>().Raven = r;
        }
    }

    // the game's raven unseen while it goes "away" in its own way, shown again after (the next visit flies in as usual)
    internal class BigRavenHidden : MonoBehaviour
    {
        internal Raven Raven;
        private Renderer[] _hidden;
        private float _until;

        private void Start()
        {
            _until = Time.time + 10f;
            Renderer[] all = Raven != null && Raven.m_visual != null ? Raven.m_visual.GetComponentsInChildren<Renderer>(false) : new Renderer[0];
            _hidden = System.Array.FindAll(all, x => x.enabled); // (only those it hid: the rest stay as the game keeps them)
            foreach (Renderer x in _hidden) x.enabled = false;
        }

        private void Update()
        {
            if (Raven != null && !Raven.IsAway() && Time.time < _until) return;
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (_hidden == null) return;
            foreach (Renderer x in _hidden) if (x != null) x.enabled = true;
        }
    }
}
#endif
