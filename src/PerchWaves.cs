#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // A bathing raven on the water (PerchBath): it rides the waves, and its rings and drops sit on the water's face.
    internal partial class PerchLife
    {
        // ---------------------------------------------------------------- the waves (the user, 2026-10-28: the water goes up and down)
        // A raven in the water (a bath) rides the water's face as it rises and falls with the waves,
        // measured where it is every frame; one standing on a shallow bottom never sinks into it.
        private WaterVolume _wave;
        private float _waveStart, _waveFloor;
        private bool _waveCalm; // the water of a building (the hot tub): no splashing there

        private void WaveBegin(float ground)
        {
            _wave = null; _waveCalm = false;
            Vector3 at = transform.position;
            _wave = WaterFx.VolumeAt(at, out _waveCalm);
            if (_wave == null) return;
            _waveStart = _wave.GetWaterSurface(at, 1f);
            _waveFloor = ground - at.y; // (no lower than the bottom under it)
        }

        // the water's face right under it now (the rings and drops sit on it)
        private Vector3 WaterFace(float fallback)
        {
            Vector3 p = transform.position;
            return new Vector3(p.x, _wave != null ? _wave.GetWaterSurface(p, _waveCalm ? 0f : 1f) : fallback, p.z);
        }

        // how far the water's face has risen (or fallen) where it is since it came
        private float WaveLift()
        {
            if (_wave == null || _waveCalm || (_bathFrom < 0f && Time.time >= _paddleUntil)) return 0f; // (a tub's water stands still: the game would give it the sea's waves; the bath over, it rides them on until it flies off — never a sudden drop)
            return Mathf.Max(_wave.GetWaterSurface(transform.position, 1f) - _waveStart, _waveFloor);
        }
    }
}
#endif
