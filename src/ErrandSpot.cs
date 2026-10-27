#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // «Ворон бере участь» (the user, 2026-10-25): Hugin marks the place of an errand's step. As you come up to it (closer
    // than From, not yet in its ring) he says a word, flies off your shoulder and sits in the middle of it, free and quiet
    // (Perches.Lead, as the guide of «Хугін знає дорогу»); you reach him (Reached), he waits long enough (Stay) or the step
    // changes, and he is called back without a word — once he sits there (on his way, the call waits a tick). Once a step,
    // by chance (Chance), only for steps that are about a place you walk to (go, read, clear, destroy, carry) and only on
    // land — the guide, the watch and the champion have their own ravens' parts. Left on the mark when you leave the game
    // in between, he is called back when you are in again (this session); after a restart his spot is simply gone.
    internal static class ErrandSpot
    {
        internal static float Chance = 0.6f, From = 120f, Reached = 12f, Stay = 90f;
        private static ErrandStep _for;
        private static bool _will, _sent, _recall;
        private static Vector3 _at;
        private static float _until;

        // twice a second (Errands.Tick); r: the running errand, or null
        internal static void Tick(Player p, ErrandRun r)
        {
            if (p == null) return; // (out of the game: kept, called back once you are in)
            ErrandStep s = r != null ? r.Current : null;
            if (s != _for) { _for = s; _will = s != null && Marks(s) && Random.value < Chance; if (_sent) _recall = true; }
            if (_sent)
            {
                if (_recall || Time.time > _until || Flat(p.transform.position, _at) <= Reached || p.IsDead()) Back(p);
                return;
            }
            Vector3 at; float radius;
            if (!_will || s == null || !s.Where(r, r.Step, out at, out radius)) return;
            float d = Flat(p.transform.position, at);
            if (d > From || d <= radius + s.Margin) return; // (on the way there, not yet in its ring)
            _will = false; // (once a step: if he cannot now, not again)
            if (!Shoulder.Present(p, false) || Perches.Busy(false) || Senses.Fight || p.InInterior() || ZoneSystem.instance == null) return;
            Vector3 ground = at;
            ground.y = ZoneSystem.instance.GetSolidHeight(at);
            if (ground.y < ZoneSystem.instance.m_waterLevel + 0.3f) return; // (under water: no sitting on the sea's floor)
            Vector3 face = p.transform.position - ground; face.y = 0f; // (he sits looking back at you)
            string why = Perches.Lead(p, false, ground, face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face.normalized, Vector3.up) : Quaternion.identity);
            if (why != null) { Errands.Log("mark: Hugin did not fly — " + why); return; }
            _sent = true; _recall = false; _at = ground; _until = Time.time + Stay;
            Errands.Log("mark: Hugin flew to the place of step " + (r.Step + 1) + " (" + Mathf.RoundToInt(d) + " m)");
            if (RavenSpeech.Has(p.gameObject) || !Director.May(Director.Voice.Event) || Plugin.Instance == null) return; // (someone talking: he just goes)
            string line = Plugin.Instance.ComposeTagged(p, false, "errand_mark");
            if (string.IsNullOrEmpty(line)) return;
            RavenSkills.SayFromShoulder(p, false, line);
            Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line));
        }

        // called back if he sits on the mark; still on his way there: asked again on the next tick; sitting elsewhere
        // (you sent him somewhere since): left alone
        private static void Back(Player p)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, false);
            if (item != null && Perches.Busy(false)) return;
            _sent = false; _recall = false;
            Vector3 at;
            if (item != null && Perches.On(item) && Perches.SpotPos(item, out at) && Flat(at, _at) < 4f) Perches.CallSilent(p, item, false);
        }

        private static bool Marks(ErrandStep s) { return s is GoStep || s is ReadStep || s is ClearStep || s is DestroyStep || s is CarryStep; }

        private static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
    }
}
#endif
