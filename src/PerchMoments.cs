#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // «Життя на жердині» (the user, 2026-10-23): what the world and its stay there make a raven on a perch do, decided
    // by the perch's holder and played by the sitting system like everything else on the perch.
    //  - the world's moments, as on a shoulder, by the perch's own place: a foot drawn up in the cold, the head tucked
    //    into its back asleep, a start at thunder, a stretch at dawn, panting in the heat;
    //  - left here (at home, on guard): it looks about (more often on a guard perch, which also turns round now and then
    //    to watch the other side), an enemy near is watched tensely (Munin bristles), a cry and a crouch before a blow, a
    //    shake and a triumph (or a preen) after it; a night watch: never quite asleep, it nods off and jerks awake; two
    //    left on one perch keep watch by turns;
    //  - its owner: glad when you come back after a while (a hungry Munin turns away), bored when you stay away (yawns,
    //    pecks the bar, steps about, preens), hungry eyes at the feeder, the beak wiped after its own hunt.
    internal partial class PerchLife
    {
        internal static float LookoutHome = 40f, LookoutGuard = 16f, GuardTurnEvery = 150f, BoredAfter = 180f, BoredEvery = 60f;
        internal static float HungryEvery = 45f, NodEvery = 28f, ShiftSeconds = 180f, LongAway = 120f, DrowseSeconds = 3.5f;
        internal static float TuckFold = 1.5f, TuckRoll = 5f, ThunderWakes = 0.35f;
        private static readonly string[] Bored = { "Позіхає", "Дзьобає жердину", "Переступає", "Чистить пір'я" };
        private float _mCold, _mHeat, _mThunder, _mTuck, _mLookout, _mTurn, _mBored, _mHungry, _mNod, _mFoe, _sleptAt = -1f, _bornAt = -1f;
        private float _ownerGoneAt = -1000f, _drowseUntil, _thenAt;
        private int _mDawnDay = -1;
        private bool _sleepy, _roof, _ash;
        private float _placeAt;
        private string _then;

        // the way it was left here ("" with you, Home.AtHome, Home.OnGuard)
        private string Mode { get { Perches.Occupant o = View != null ? View.At(Seat) : null; return o != null ? o.Mode : ""; } }

        // the other raven on this perch, left here too
        private PerchLife Mate()
        {
            Transform nb = View != null ? View.Neighbour(Seat) : null;
            PerchLife p = nb != null ? nb.GetComponent<PerchLife>() : null;
            return p != null && p.Mode.Length > 0 ? p : null;
        }

        // asleep at night? On guard alone it never quite sleeps; two left here keep watch by turns (the same turn on
        // every game: the network clock)
        private bool SleepsNow()
        {
            string mode = Mode;
            if (mode.Length == 0) return true;
            PerchLife mate = Mate();
            if (mate == null) return mode != Home.OnGuard;
            long slot = (long)(SitNet.Seconds / ShiftSeconds);
            return ((slot & 1) == 0) == (Seat < mate.Seat);
        }

        // nodding off: a yawn first, then asleep
        private void Drowse()
        {
            _drowseUntil = Time.time + DrowseSeconds;
            if (Composes) _sitDir.React(this, "Позіхає", 0.2f);
        }

        // an act after the one now (a triumph after the shake…): as a reaction, so the rest after the first does not drop it
        private void Then(string template, float after) { _then = template; _thenAt = Time.time + after; }

        // the perch's holder, twice a second, after the sitting director's turn
        private void Moments()
        {
            if (EnvMan.instance == null || View == null || _sitDir.HoldU >= 0f) return;
            float now = Time.time;
            if (_bornAt < 0f)
            {
                _bornAt = now;
                _mLookout = now + Random.Range(5f, 20f); _mTurn = now + Random.Range(0.5f, 1f) * GuardTurnEvery;
                _mHungry = now + Random.Range(5f, 15f); _mNod = now + Random.Range(5f, 20f); _mBored = now + Random.Range(10f, 30f);
            }
            string mode = Mode;
            bool home = mode.Length > 0, guard = mode == Home.OnGuard, munin = Munin;
            // an enemy near: watched tensely now and then (Munin bristles too)
            if (_foe != null)
            {
                _then = null; // (what it meant to do after: forgotten)
                if (now >= _mFoe) { _mFoe = now + Random.Range(5f, 9f) * (munin ? 0.8f : 1.2f); _sitDir.React(this, munin && Random.value < 0.5f ? "Наїжачився" : "Насторожився", Random.Range(0f, 0.3f)); }
                return;
            }
            _mFoe = 0f;
            if (now >= _placeAt) // the place: its roof (the holder measures it, PerchView) and its land, looked at now and then
            {
                _placeAt = now + 5f;
                int comfort;
                View.Env(out _roof, out comfort);
                _ash = Heightmap.FindBiome(transform.position) == Heightmap.Biome.AshLands;
            }
            bool roof = _roof, ash = _ash;
            // thunder: a start (it wakes it)
            if (!roof && now >= _mThunder && Quirks.Env().Contains("thunder"))
            {
                _mThunder = now + Random.Range(15f, 45f);
                if (_asleep && Random.value > ThunderWakes) return; // (asleep it sleeps through most claps of a long storm)
                _asleep = false; // (a sleeping one does not react)
                _sitDir.React(this, "Грім", Random.Range(0.1f, 0.3f));
                return;
            }
            if (_sitDir.Wanted != null || _sitDir.Playing) return;
            if (_then != null) { if (now >= _thenAt && !_asleep) { _sitDir.React(this, _then, 0.1f); _then = null; } return; }
            // asleep: now and then the head tucked into its back
            if (_asleep)
            {
                if (_sleptAt < 0f) _sleptAt = now;
                if (now - _sleptAt > 15f && now >= _mTuck) { _mTuck = now + Random.Range(40f, 80f); _sitDir.Want(SitDirector.TuckHead, 0f, 3f); }
                return;
            }
            _sleptAt = -1f;
            bool night = EnvMan.IsNight();
            int day = EnvMan.instance.GetDay();
            float frac = EnvMan.instance.GetDayFraction();
            if (day != _mDawnDay && frac > 0.24f && frac < 0.32f) { _mDawnDay = day; if (!roof) { _sitDir.Want("Потягується", Random.Range(0f, 15f), 20f); return; } }
            bool heat = ash || (_warmWant > 0.5f && !night && !EnvMan.IsCold());
            bool cold = !heat && !roof && _warmWant < 0.5f && (EnvMan.IsCold() || EnvMan.IsFreezing());
            if (cold && now >= _mCold) { _mCold = now + Random.Range(40f, 90f); _sitDir.Want("Підібрав лапу", 0f, 8f); return; }
            if (heat && now >= _mHeat) { _mHeat = now + (ash ? Random.Range(30f, 60f) : Random.Range(60f, 120f)); _sitDir.Want("Спекотно", 0f, 8f); return; }
            if (!home) return; // (sitting here with you: the rest is for a raven left here)
            // hungry: eyes on the feeder, begging, then drooping
            if (now >= _mHungry)
            {
                _mHungry = now + Random.Range(0.7f, 1.3f) * HungryEvery;
                ItemDrop.ItemData item = Home.Cache(View, Seat);
                if (item != null && Satiety.Value(item) <= Satiety.AskAt)
                {
                    _sitDir.Want("Голодний", 0f, 8f);
                    return;
                }
            }
            // a night watch: it nods off and jerks awake (often shaking its head after)
            if (night && now >= _mNod)
            {
                _mNod = now + Random.Range(0.6f, 1.4f) * NodEvery;
                _sitDir.Want("Клює носом", 0f, 8f);
                if (Random.value < 0.5f) Then(SitTemplates.FlickName, 0.2f);
                return;
            }
            // looking about: on a guard perch often; there it also turns round to watch the other side now and then
            if (now >= _mLookout)
            {
                _mLookout = now + Random.Range(0.6f, 1.4f) * (guard ? LookoutGuard : LookoutHome) * (munin ? 0.8f : 1.2f);
                _sitDir.Want("Озирається", 0f, 10f);
                return;
            }
            if (guard && now >= _mTurn)
            {
                _mTurn = now + Random.Range(0.7f, 1.3f) * GuardTurnEvery;
                Perches.Occupant o = View.At(Seat);
                if (o != null && CanTurn && !AnyoneClose() && !Perches.WallAhead(View, Seat, !o.Flip)) { Perches.RequestTurn(View, Seat); return; } // (someone close: it faces them anyway, PerchView.FaceNearest)
            }
            // bored: its owner away a long while
            if (!_ownerWasNear && now - Mathf.Max(_ownerGoneAt, _bornAt) > BoredAfter && now >= _mBored)
            {
                _mBored = now + Random.Range(0.6f, 1.4f) * BoredEvery;
                _sitDir.Want(Bored[Random.Range(0, Bored.Length)], 0f, 10f);
            }
        }

        // its owner came close (PerchLife.Scan, after the greeting caw): back after a while → glad (a hungry Munin sulks)
        private void Welcome()
        {
            bool away = Time.time - _ownerGoneAt >= LongAway;
            if (!away || !Composes) return;
            _asleep = false; // (woken by you: Scan keeps it so)
            ItemDrop.ItemData item = Home.Cache(View, Seat);
            bool sulk = Munin && item != null && Satiety.Value(item) <= Satiety.AskAt;
            _sitDir.React(this, sulk ? "Відвертається" : "Радіє тобі", Random.Range(0.3f, 0.8f));
            if (!sulk && Random.value < 0.5f) Then("Переступає", 0.3f);
        }

        // a blow from here (every game sees its mark at once): the cry and the crouch; s until it springs off (0: at once)
        internal float Windup(int seed)
        {
            if (!Composes) return 0f;
            _asleep = false;
            _wakeUntil = Mathf.Max(_wakeUntil, Time.time + WakeMin);
            if (!_sitDir.PlayHere(this, "Тривога", seed)) return 0f;
            ((ISitBird)this).BirdDid(_sitDir.Run.Seconds, true);
            return _sitDir.Run.Seconds;
        }

        // back on its seat after a blow: a shake, then a triumph (Munin more often) or a preen
        internal void AfterFight()
        {
            if (!Composes) return;
            _sitDir.React(this, "Струшується", 0.6f);
            float r = Random.value;
            if (r < (Munin ? 0.7f : 0.45f)) Then("Тріумфує", 0.3f);
            else if (r < 0.85f) Then("Чистить пір'я", 0.8f);
        }

        // back from its own hunt: the beak wiped on the bar, fluffed up
        internal void BackFromHunt()
        {
            _sitDir.React(this, "Витирає дзьоб", 1.2f);
            if (Random.value < 0.6f) Then("Розпушився", 0.5f);
        }

        // a sitting template with a foot drawn up (the cold): that leg folded into the feathers, the body over the other
        private void TuckFoot()
        {
            SitRun r = Composes ? _sitDir.Run : null;
            if (r == null || !r.T.Tuck || _turnAt >= 0f || Pose == null) return;
            float w = SitW;
            if (w <= 0.001f) return;
            if ((r.Seed & 1) == 0) Pose.StepL = w * TuckFold; else Pose.StepR = w * TuckFold;
            Pose.Roll = TuckRoll * w;
        }

        private Transform NearestFeeder()
        {
            Transform best = null;
            float d = Home.FeederReach * Home.FeederReach;
            foreach (FeederView f in FeederView.All)
            {
                if (f == null) continue;
                float x = (f.transform.position - transform.position).sqrMagnitude;
                if (x < d) { d = x; best = f.transform; }
            }
            return best;
        }

        private bool AnyoneClose()
        {
            foreach (Player p in Player.GetAllPlayers()) if (p != null && (p.transform.position - transform.position).sqrMagnitude < FaceNear * FaceNear) return true;
            return false;
        }
    }
}
#endif
