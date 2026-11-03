#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A raven of the story (StoryScenes' «bird»): nobody's, no item, no network object — only a picture, as the raven in a
    // grave (GraveBird), on the same engine: the motor flies it, the shoulder's life moves its wings, the perch's look
    // seats it. Eirik's raven first (story-lore.md «Пролог»): the memory of the first Watch, a Munin with ash-grey wings.
    // The scene's owner drives it; the players near get the same orders (StoryScenes' RPC) and fly a picture of their own.
    //   Come(from, seat, yaw)   appears far off and flies in, lands on the seat (or hangs in the air there: hover)
    //   Land(seat, yaw)         off from where it is, to another seat
    //   Face(point)             turns where it sits
    //   Say(text)               a line over it
    //   Circle(around, r, up)   round over someone (until told otherwise)
    //   Leave(toward)           off that way and gone in the sky
    // A white-blue streak follows it in the air, so it is seen against trees and sky (Trail).
    // A person's raven (NpcRaven) is one too: made sitting (Sit), its own kind, size and colours (Repaint), no streak,
    // circling its person (Circle takes any body), its flights in the log only when traced.
    //   Gone()                  a puff of feathers
    internal class StoryBird : MonoBehaviour, IRavenUser
    {
        private const string Name = "у сюжеті";
        internal static int Level = 4;
        internal static bool PaintOn = true; // (the author's test: off = the game's own colours)
        internal static readonly List<StoryBird> Live = new List<StoryBird>();
        private int _wasState = -1;
        private static string Where(Vector3 v) { return Mathf.RoundToInt(v.x) + "," + Mathf.RoundToInt(v.y) + "," + Mathf.RoundToInt(v.z); }
        private static void Log(string s) { Story.Log("bird: " + s); }
        private void Note(string s) { if (!_npc || NpcRaven.Trace) Log((_npc ? "(" + name + ") " : "") + s); } // (a person's raven: in the log only when traced)
        internal static float GiveUp = 25f, LeaveGone = 50f, LeaveSeconds = 14f;
        private const int Sits = 0, Flies = 2, Final = 3, Landing = 4, Away = 5, Hover = 6, Circles = 7;
        // the streak (the user 2026-11-02): thin, 30% fainter than it was, from its tail (not from under its feet), and
        // faint right at the bird — it grows out of nothing a little behind it, then fades away
        internal static float TrailSeconds = 1.6f, TrailWidth = 0.1f, TrailPeak = 0.6f, TrailRise = 0.14f;
        internal static Color TrailHead = new Color(0.92f, 0.97f, 1f, 1f), TrailTail = new Color(0.35f, 0.6f, 1f, 0f);
        private Transform _trail;
        private Character _around;
        private float _ringR = 4.5f, _ringUp = 3.5f, _ringPhase;

        // its colours (RavenLooks: one picture for both sides — both wings ash-grey, the rest black)
        internal static string LookText =
            "id: story_eirik\nname: Ворон Ейріка\nfor: any\n" +
            "zone body 08080A 26262C 100 80\nzone head 08080A 2A2A30 100 80\nzone tail 08080A 26262C 100 80\n" +
            "zone wings 34322F B4B0A9 100 85\nzone tips 5E5A55 D6D2CB 100 85\neyes BFD2DC 2.5\n";
        private static readonly Dictionary<string, Look> _looks = new Dictionary<string, Look>();
        // a person's raven (NpcRaven): its kind, size and colours; no streak, not among the scene's birds
        private bool _npc, _munin = true;
        private float _size = -1f;
        private string _lookText = LookText, _lookId = "story_eirik";

        internal Transform Seat;
        internal int State = Sits;
        internal bool Sitting { get { return State == Sits; } }
        // there and still: sitting, or hanging in the air where it was sent (the scene goes on)
        internal bool Settled { get { return State == Sits || (State == Hover && Seat != null && Vector3.Distance(transform.position, Seat.position) < 1.5f); } }
        private bool _hover;
        private Vector3 _faceAt;
        private ShoulderLife _life;
        private RavenMotor _motor;
        private RavenLooks.Painted _paint;
        private float _since, _settle;
        private Vector3 _landOff, _away;
        private Quaternion _landFromRot, _faceTo;
        private bool _clip, _yielded, _turning;

        public void Yield() { _yielded = true; Destroy(this); }

        // nobody's any more — its scene is over (Release), or the one whose scene it is left the game (Owner: another
        // player's, a picture of their scene here): where it sits, hangs or circles, it flies off a little later, or at
        // once when nobody is near
        internal long Owner;
        internal static float FreeSeconds = 60f, FreeNear = 60f;
        private bool _free;
        private float _freeAt, _nextFree;

        internal void Release() { if (!_free) { _free = true; _freeAt = Time.time; } }

        private void Unowned()
        {
            if (Time.time < _nextFree) return;
            _nextFree = Time.time + 1f;
            if (!_free && Owner != 0L && PerchView.PlayerOf(Owner) == null) Release();
            if (!_free || (State != Sits && State != Hover && State != Circles)) return;
            Player me = Player.m_localPlayer;
            bool alone = me == null || Vector3.Distance(me.transform.position, transform.position) > FreeNear;
            if (alone || Time.time - _freeAt > FreeSeconds) Leave(transform.position + transform.forward * 10f);
        }
        private static float Size { get { return Shoulder.Scale * Shoulder.SizeFor(Level); } }
        private float MySize { get { return _size > 0f ? _size : Size; } }

        // a bird far off (from), flying in to a seat (yaw: where it faces sitting)
        internal static StoryBird Come(Vector3 from, Vector3 seat, float yaw, bool hover)
        {
            Transform start = NewSeat(from, yaw);
            GameObject r = Perches.Sitter(start, true, Size, -1, null, null);
            if (r == null) { Destroy(start.gameObject); Log("no picture made (Perches.Sitter)"); return null; }
            Log("comes from " + Where(from) + " to " + Where(seat) + (hover ? " (in the air)" : " (to sit)"));
            StoryBird b = r.AddComponent<StoryBird>();
            b.Seat = start;
            b._hover = hover;
            b.Paint();
            b.Land(seat, yaw);
            b.Streak();
            b._ringPhase = Random.Range(0f, 10f);
            Live.Add(b);
            return b;
        }

        // a person's raven (NpcRaven), sitting there already (it was there before you came near); lookText null: the game's colours
        internal static StoryBird Sit(Vector3 seat, float yaw, bool munin, float size, string lookText, string lookId)
        {
            Transform at = NewSeat(seat, yaw);
            GameObject r = Perches.Sitter(at, munin, size, -1, null, null);
            if (r == null) { Destroy(at.gameObject); return null; }
            StoryBird b = r.AddComponent<StoryBird>();
            b._npc = true; b._munin = munin; b._size = size; b._lookText = lookText; b._lookId = lookId ?? "";
            b.Seat = at;
            b._ringPhase = Random.Range(0f, 10f);
            b.Paint();
            return b;
        }

        // its colours anew (a person's raven grows dim or bright: NpcRaven)
        internal void Repaint(string lookText, string lookId)
        {
            if (_lookId == (lookId ?? "") && _lookText == lookText) return;
            foreach (Renderer x in GetComponentsInChildren<Renderer>(true)) // (back to the game's materials first: painted anew from them)
            {
                if (x == null || x is ParticleSystemRenderer) continue;
                Material[] cur = x.sharedMaterials;
                for (int i = 0; i < cur.Length; i++) cur[i] = OwnOf(EyeGlow.Original(cur[i]));
                x.sharedMaterials = cur;
            }
            RavenLooks.Drop(_paint); _paint = null;
            _lookText = lookText; _lookId = lookId ?? "";
            Paint();
            ShoulderLife life = GetComponent<ShoulderLife>();
            if (life != null) life.EyesAgain();
        }

        private static Transform NewSeat(Vector3 at, float yaw)
        {
            GameObject g = new GameObject("RavenWatch_StorySeat");
            g.transform.position = at;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return g.transform;
        }

        // ---- the orders

        internal void Land(Vector3 seat, float yaw)
        {
            Transform old = Seat, now = NewSeat(seat, yaw);
            if (State == Landing) SitDown(); // (touching down this moment: on the old seat first, then off it)
            if (State == Hover) { _hover = false; State = Flies; } // (from the air to a seat: it lands now)
            Seat = now;
            if (State == Sits) TakeOff(seat + Vector3.up * 2f); // (off the old seat: no longer its child)
            else { if (_motor != null && _motor.Body != null) _motor.Body.NewLeg(); State = Flies; _since = Time.time; }
            if (_motor == null) SitDown(); // (no engine: straight onto the new seat)
            if (old != null) Destroy(old.gameObject);
        }

        internal void Face(Vector3 point)
        {
            if (State == Hover || _hover) { _faceAt = point; return; } // (in the air: the flight turns it)
            _faceAt = point;
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f || Seat == null) return;
            _faceTo = Quaternion.LookRotation(d.normalized, Vector3.up);
            _turning = true;
        }

        internal void Say(string text)
        {
            if (Chat.instance == null || string.IsNullOrEmpty(text)) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * 0.55f, 40f, Mathf.Clamp(2.5f + text.Length * 0.07f, 4f, 10f), "", text, false);
            ScreamFx.CawAt(transform, transform.position, 0.55f, 0.8f, false);
        }

        internal void Leave(Vector3 toward)
        {
            Vector3 d = toward - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 1f) d = transform.forward;
            _away = transform.position + d.normalized * 80f + Vector3.up * 30f;
            if (State == Landing) SitDown();
            if (State == Sits) TakeOff(_away);
            _hover = false;
            if (_motor == null) { Gone(); return; }
            if (_motor.Body != null) _motor.Body.NewLeg();
            State = Away; _since = Time.time;
        }

        internal void Gone() { Perches.Puff(gameObject, _munin, "a story bird gone"); }

        internal void Circle(Character around, float r, float up)
        {
            if (around == null) return;
            _around = around; _ringR = r; _ringUp = up; _hover = false;
            if (State == Landing) SitDown();
            if (State == Sits) TakeOff(around.GetHeadPoint() + Vector3.up * up);
            if (_motor == null) return;
            if (_motor.Body != null) _motor.Body.NewLeg();
            State = Circles; _since = Time.time;
        }

        // the streak: a trail on a follower, never a child of the bird (its life turns every transform under it as a bone)
        private void Streak()
        {
            try
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh == null) return;
                GameObject g = new GameObject("RavenWatch_StoryBirdTrail");
                g.transform.position = transform.position;
                TrailFollow fol = g.AddComponent<TrailFollow>(); fol.Target = transform; fol.At = TailPoint();
                TrailRenderer tr = g.AddComponent<TrailRenderer>();
                Material m = new Material(sh); m.name = "RavenWatch story trail"; m.hideFlags = HideFlags.HideAndDontSave;
                tr.sharedMaterial = m;
                tr.time = TrailSeconds;
                tr.minVertexDistance = 0.15f;
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(TrailRise, 1f), new Keyframe(1f, 0f)); // (narrow where it leaves the bird)
                tr.widthMultiplier = TrailWidth;
                Gradient gr = new Gradient();
                gr.SetKeys(new[] { new GradientColorKey(TrailHead, 0f), new GradientColorKey(new Color(0.6f, 0.8f, 1f), 0.4f), new GradientColorKey(TrailTail, 1f) },
                           new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(TrailPeak, TrailRise), new GradientAlphaKey(TrailPeak * 0.6f, 0.4f), new GradientAlphaKey(0f, 1f) }); // (no hard start at the bird)
                tr.colorGradient = gr;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
                _trail = g.transform;
            }
            catch (System.Exception e) { Log("trail: " + e.Message); }
        }

        // where its tail ends, in its own frame: from its body's size (the model's pivot is between its feet)
        private Vector3 TailPoint()
        {
            Bounds b = new Bounds(); bool any = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return new Vector3(0f, 0.2f, -0.25f); // (none seen yet: about a raven's)
            Vector3 c = transform.InverseTransformPoint(b.center);
            Vector3 e = transform.InverseTransformVector(b.extents); e = new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z));
            return new Vector3(0f, c.y, c.z - e.z * 0.85f); // (its middle height, at the back end)
        }

        // the streak's follower: at a point of the bird (its tail), never its child
        internal sealed class TrailFollow : MonoBehaviour
        {
            internal Transform Target;
            internal Vector3 At;
            private void LateUpdate()
            {
                if (Target != null) { transform.position = Target.TransformPoint(At); return; }
                if (!ReferenceEquals(Target, null)) { Target = null; Destroy(gameObject, 2f); }
            }
        }

        // ---- the engine (GraveBird's way)

        private void TakeOff(Vector3 aim)
        {
            Player life = Player.m_localPlayer;
            if (life == null) return;
            foreach (PerchPose pp in GetComponents<PerchPose>()) DestroyImmediate(pp);
            Raven src = Shoulder.Template(_munin);
            if (src != null && src.m_visual != null) { Perches.ToTemplate(gameObject, src.m_visual); Perches.Reveal(gameObject, false); }
            transform.SetParent(null, true);
            _life = gameObject.AddComponent<ShoulderLife>();
            _life.Init(life, _munin, Level);
            _life.Still = true;
            AnimDirector.Take(gameObject, RavenAct.Outing, this, Name);
            _motor = RavenMotor.Of(gameObject, _munin, null);
            Paint();
            if (_motor == null) return;
            _motor.Launch(_motor.TakeoffVelocity(aim, Vector3.zero, Name), Name, 1f);
            State = Flies; _since = Time.time;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

        private void LateUpdate()
        {
            if (Seat == null) { Destroy(gameObject); return; }
            Unowned();
            if (State != _wasState) { _wasState = State; Note(StateName(State) + " at " + Where(transform.position) + ", seat " + Where(Seat.position) + (Player.m_localPlayer != null ? ", " + Mathf.RoundToInt(Vector3.Distance(transform.position, Player.m_localPlayer.transform.position)) + " m from you" : "")); }
            float dt = Time.deltaTime;
            if (State == Sits)
            {
                if (_turning) { Seat.rotation = Quaternion.RotateTowards(Seat.rotation, _faceTo, 120f * dt); _turning = Quaternion.Angle(Seat.rotation, _faceTo) > 1f; }
                return;
            }
            if (_motor == null || _life == null) return;
            if (State == Away)
            {
                _motor.Steer(_away, _motor.Body != null ? _motor.Body.Style.Cruise : 5f, false, 0f, dt, Vector3.zero, 1f);
                _motor.Finish(dt, 0f);
                if (Vector3.Distance(transform.position, Seat.position) > LeaveGone || Time.time - _since > LeaveSeconds) { Destroy(gameObject); return; }
            }
            else if (State == Circles)
            {
                if (_around == null || _motor.Body == null) { Leave(transform.position + transform.forward * 10f); return; }
                float t = Time.time + _ringPhase;
                Vector3 mid = _around.GetHeadPoint() + Vector3.up * (_ringUp + Mathf.Sin(t * 0.5f) * 0.5f);
                Vector3 aim = CirclePlan.Ring(transform.position, mid, Mathf.Lerp(_ringR * 0.85f, _ringR * 1.15f, Mathf.PerlinNoise(t * 0.12f, _ringPhase)), 34f, 1f);
                _motor.Steer(aim, _motor.Body.Style.Cruise * 0.65f, false, 0f, dt, Vector3.zero, 1f);
                _motor.Finish(dt, 0f);
            }
            else if (State == Hover || (_hover && (State == Flies || State == Final)))
            {
                // in the air before you (the post raven's way): it never lands, it keeps there facing you
                if (_motor.Body == null) { Perches.Puff(gameObject, _munin, "a story bird lost its wings"); return; }
                Vector3 aim = Seat.position, look = (_faceAt != Vector3.zero ? _faceAt : aim + Seat.forward) - transform.position; look.y = 0f;
                float d = Vector3.Distance(transform.position, aim);
                if (d < 8f && look.sqrMagnitude > 0.01f) _motor.Body.FaceHint(look.normalized, 1f - d / 8f * (State == Hover ? 0f : 1f));
                float left = _motor.Steer(aim, d > 15f ? _motor.Body.Style.Cruise : _motor.Body.Style.Cruise * 0.7f, true, d < 4f ? 1f : 0f, dt, Vector3.zero, d < 6f ? 0.3f : 1f);
                _motor.Finish(dt, d < 3f ? Mathf.Clamp01(1f - d / 3f) * 0.5f : 0f);
                if (State != Hover && left < 1.2f) State = Hover;
                if (State != Hover && Time.time - _since > GiveUp) { Perches.Puff(gameObject, _munin, "a story bird never got there"); return; }
            }
            else if (State == Flies || State == Final)
            {
                if (_motor.Body == null) { Perches.Puff(gameObject, _munin, "a story bird lost its wings"); return; }
                if (Time.time - _since > GiveUp)
                {
                    // its seat out of reach (under a roof, behind a wall from where it came): a scene's bird hangs in the air
                    // where it got to — the scene goes on with it there; a person's raven goes (NpcRaven brings it again)
                    if (_npc) { Perches.Puff(gameObject, _munin, "a person's raven never got to its seat"); return; }
                    Note("its seat out of reach: it hangs in the air where it got to");
                    Seat.position = transform.position; _hover = true; State = Hover; _since = Time.time;
                    return;
                }
                Vector3 seat = Seat.position, front = Flat(Seat.forward);
                if (State == Flies)
                {
                    Vector3 pre = seat - front * 1.6f + Vector3.up * 1.2f;
                    _motor.Steer(pre, _motor.Body.Style.Cruise, true, 0f, dt, Vector3.zero, 1f);
                    _motor.Finish(dt, 0f);
                    if (Vector3.Distance(transform.position, pre) < 1.2f || Vector3.Distance(transform.position, seat) < 2f) { _motor.Body.MarkFinal(); State = Final; }
                }
                else
                {
                    Vector3 aim = seat + Vector3.up * FlightBody.LandAbove - front * FlightBody.LandBehind;
                    float near = Vector3.Distance(transform.position, aim);
                    _motor.Body.FaceHint(front, 1f - Mathf.Min(near, RavenFlight.TurnFrom) / RavenFlight.TurnFrom);
                    float left = _motor.Steer(aim, _motor.Body.HomeSpeed(5f, Vector3.zero, 0f), true, near < 4f ? 2f : 0f, dt, Vector3.zero, 1f);
                    _motor.Finish(dt, Mathf.Clamp01(1f - near / Mathf.Max(0.5f, _motor.Landing().Flare)));
                    if (_motor.Body.CanLand(left, Vector3.zero)) { _life.NextLand = _motor.Landing(); BeginLand(); }
                }
            }
            else if (State == Landing) { LandStep(); return; }
            if (this != null && _life != null && State != Landing) _life.Apply();
        }

        private void BeginLand()
        {
            State = Landing;
            _motor.Grounded();
            _life.VanillaFly = false; _life.Still = false;
            Quaternion seatRot = Seat.rotation * Quaternion.Euler(Shoulder.Rotation);
            _landOff = Quaternion.Inverse(seatRot) * (transform.position - Seat.position);
            _landFromRot = Quaternion.Inverse(seatRot) * transform.rotation;
            _clip = _life.BeginLand();
            _settle = Time.time;
        }

        private void LandStep()
        {
            Quaternion seatRot = Seat.rotation * Quaternion.Euler(Shoulder.Rotation);
            float way;
            bool still;
            if (_clip) still = _life.StepLand(out way);
            else { way = Mathf.Clamp01((Time.time - _settle) / 0.35f); still = way < 1f; way = way * way * (3f - 2f * way); }
            transform.position = Seat.position + seatRot * (_landOff * (1f - way));
            transform.rotation = Quaternion.Slerp(seatRot * _landFromRot, seatRot, Mathf.Clamp01(way * ShoulderPin.LandTurnRate));
            if (!still || Time.time - _settle > 4f) SitDown();
        }

        private void SitDown()
        {
            AnimDirector.Release(gameObject, this);
            Perches.Seat(gameObject, Seat, _munin, MySize, -1, null, null);
            _life = null; _motor = null; State = Sits;
            Paint();
        }

        // its colours on whatever body it wears now (the perch's look and the template's are other renderers) — the
        // shoulder's way (RavenWear.Paint): from the GAME's materials under any eye-glow copy, body renderers only
        private void Paint()
        {
            try
            {
                if (!PaintOn) return;
                if (_lookText == null) return; // (a person's raven in the game's own colours)
                Look look;
                if (!_looks.TryGetValue(_lookId, out look)) { look = Look.Parse(_lookText, _lookId); _looks[_lookId] = look; }
                if (_paint == null) _paint = RavenLooks.Use(look, _munin);
                if (_paint == null) { Note("look not painted: " + RavenLooks.Problem); return; }
                int n = 0;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r is ParticleSystemRenderer || r.GetComponentInParent<FxOn>() != null) continue;
                    Material[] cur = r.sharedMaterials;
                    Material[] game = new Material[cur.Length];
                    bool body = false;
                    for (int i = 0; i < cur.Length; i++) { game[i] = OwnOf(EyeGlow.Original(cur[i])); body |= RavenLooks.IsBody(game[i]); } // (under the eyes' copy first, then back from our paint)
                    if (!body) continue;
                    r.sharedMaterials = RavenLooks.Dress(_paint, game);
                    n++;
                }
                ShoulderLife life = GetComponent<ShoulderLife>();
                if (life != null) life.EyesAgain(); // (the eyes' glow made again on the painted body)
                if (n == 0) Note("look: no body renderer found");
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Story bird look: " + e.Message); }
        }

        // a material already painted by us: back to the game's one it was copied from (painted again, never a copy of a copy)
        private Material OwnOf(Material m)
        {
            if (m == null || _paint == null) return m;
            foreach (KeyValuePair<Material, Material> kv in _paint.Mats) if (kv.Value == m) return kv.Key;
            return m;
        }

        internal static string StateName(int s) { return s == Circles ? "circles" : s == Sits ? "sits" : s == Flies ? "flies in" : s == Final ? "final way in" : s == Landing ? "landing" : s == Away ? "away" : s == Hover ? "hangs in the air" : s.ToString(); }

        // the author's look at it (raven story bird)
        internal string Describe()
        {
            Player p = Player.m_localPlayer;
            int on = 0, all = 0;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) { all++; if (r.enabled && r.gameObject.activeInHierarchy) on++; }
            string mats = "";
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r != null && !(r is ParticleSystemRenderer)) foreach (Material m in r.sharedMaterials) if (m != null) mats += " [" + r.name + ": " + m.name + " / " + (m.shader != null ? m.shader.name : "-") + (m.HasProperty("_Color") ? " a=" + m.color.a.ToString("0.00") : "") + "]";
            return StateName(State) + " at " + Where(transform.position) + (p != null ? ", " + Mathf.RoundToInt(Vector3.Distance(transform.position, p.transform.position)) + " m from you" : "")
                + ", seat " + (Seat != null ? Where(Seat.position) : "-") + ", renderers " + on + "/" + all + ", painted " + (_paint != null) + mats;
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_trail != null) { TrailFollow f = _trail.GetComponent<TrailFollow>(); if (f != null) f.Target = null; Destroy(_trail.gameObject, TrailSeconds + 0.2f); } // (the streak fades where it was)
            Note("gone at " + Where(transform.position) + " (" + StateName(State) + ")");
            if (!_yielded) AnimDirector.Release(gameObject, this);
            RavenLooks.Drop(_paint); _paint = null;
            if (Seat != null) Destroy(Seat.gameObject);
        }
    }
}
#endif
