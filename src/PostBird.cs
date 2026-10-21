#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The post's raven as every game sees it (Post). Three ways it comes to be:
    //   - off your shoulder (Shoulder.FlyOff, your ZDO says "out"): near the one it is sent to it flies straight there,
    //     else a hundred metres off along the way and gone in its feathers;
    //   - at the one it is sent to (their ZDO says a letter is coming): a hundred metres off on the side it comes from
    //     (in a puff close by when they are under a roof or the ground), in to them;
    //   - back to you (your ZDO says "home"): from a hundred metres off to your shoulder, which takes this very bird
    //     (Perches.NoteLeft → Shoulder.Make).
    // By the one it came to it hangs in the air before them, looking at them, its letter in the beak or the thing under it,
    // until their game hands it over (their ZDO says so): then off again, or (near) straight back to you.
    internal class PostBird : MonoBehaviour, IRavenUser
    {
        internal static float Far = 100f, ComeHeight = 25f, LeaveHeight = 30f;
        internal const int Off = 0, ToThem = 1, Hang = 2, Leave = 3, Home = 4;

        private static readonly Dictionary<string, PostBird> Outs = new Dictionary<string, PostBird>(); // off a shoulder, by the letter
        private static readonly Dictionary<string, PostBird> Ins = new Dictionary<string, PostBird>();  // come in at the one it is for
        private static readonly Dictionary<long, PostBird> Homes = new Dictionary<long, PostBird>();    // on the way back, by owner and raven
        // birds already made here for a letter ("i" + id) or its way home ("h" + id): once each. Taken by the shoulder or
        // gone, the ZDO still says so for a moment — a second one must not come in from a hundred metres.
        private static readonly HashSet<string> Made = new HashSet<string>();
        private static bool FirstTime(string key) { if (Made.Count > 200) Made.Clear(); return Made.Add(key); }
        internal static void Forget() { Made.Clear(); }

        internal string Id = "";
        internal bool Munin;
        internal long OwnerPid, TargetPid;
        internal int Mode;
        private bool _in, _given, _yielded, _close, _near;
        private float _dir, _since, _hoverSince = -1f, _noteAt;
        private Vector3 _start, _leaveAim;
        private ShoulderLife _life;
        private RavenMotor _motor;
        private Transform _carry;
        private ParticleSystem _trail;
        private bool _farewell; // (a leaving bird's goodbye cry, once)

        public void Yield() { _yielded = true; Destroy(this); }

        // how long it has hung before them (-1: not there yet)
        internal float HoverTime { get { return _hoverSince >= 0f ? Time.time - _hoverSince : -1f; } }

        internal static PostBird ForMail(string id)
        {
            PostBird b;
            if (Ins.TryGetValue(id, out b) && b != null) return b;
            return Outs.TryGetValue(id, out b) && b != null ? b : null;
        }

        private static long HomeKey(long pid, bool munin) { return pid * 2 + (munin ? 1 : 0); }

        // this player's raven, on its way home, is this near them (their own game: onto the shoulder now)
        internal static bool HomeNear(long pid, bool munin, Player p, float reach)
        {
            PostBird b;
            if (p == null || !Homes.TryGetValue(HomeKey(pid, munin), out b) || b == null) return false;
            return Vector3.Distance(b.transform.position, p.transform.position) < reach;
        }

        private static Vector3 Way(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)); }
        private static double Dbl(string s) { double d; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0; }
        private static float Flt(string s) { float f; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : 0f; }

        // ---------------------------------------------------------------- off the shoulder (Shoulder.FlyOff, every game)

        internal static bool TakeOff(GameObject g, ShoulderPin pin, ShoulderLife life, bool munin)
        {
            Player owner = pin != null ? pin.Player : null;
            ZDO z = owner != null ? NetFlag.Zdo(owner, false) : null;
            if (z == null || life == null) return false;
            string s = z.GetString(munin ? Post.KeyM : Post.KeyH, "");
            if (s.Length == 0) return false;
            string[] w = s.Split(Post.Sep);
            if (w.Length < 8 || w[0] != "out" || WorldTime.Now - Dbl(w[5]) > 20.0) return false;
            PostBird known;
            if (Outs.TryGetValue(w[1], out known) && known != null) return false;
            PostBird b = g.AddComponent<PostBird>();
            b.Id = w[1]; b.Munin = munin; b.OwnerPid = owner.GetPlayerID(); long.TryParse(w[2], out b.TargetPid); b._dir = Flt(w[3]);
            b._near = w[7] == "1";
            b._life = life; b._start = g.transform.position; b._since = Time.time; b.Mode = Off;
            Outs[b.Id] = b;
            AnimDirector.Take(g, RavenAct.FlyOff, b, "carries the post");
            Object.DestroyImmediate(pin); // it lets go of the shoulder
            g.transform.SetParent(null, true);
            Vector3 fwd = Way(b._dir);
            b._motor = RavenMotor.Of(g, munin, owner);
            if (b._motor != null) b._motor.Launch(fwd * 2f + Vector3.up * 1.5f, "відлітає", 1f);
            b.Flying(false);
            b.Hold(w[4], w[6] == Post.Parcel.ToString());
            Shoulder.PuffNote(owner, munin, "flew off with the post");
            return true;
        }

        // ---------------------------------------------------------------- made here (at the one it comes to, or back to you)

        private static PostBird Make(Vector3 at, Vector3 face, bool munin, int level, string outfit, string wear, Player life, bool puff)
        {
            GameObject seat = new GameObject("RavenWatch_PostSeat");
            seat.transform.position = at;
            seat.transform.rotation = Quaternion.LookRotation(face.sqrMagnitude > 0.0001f ? face : Vector3.forward, Vector3.up);
            GameObject r = Perches.Sitter(seat.transform, munin, Shoulder.Scale * Shoulder.SizeFor(Mathf.Max(1, level)), -1, null, null);
            if (r == null) { Object.Destroy(seat); return null; }
            Outfits.Dress(r, outfit);
            RavenWear.Dress(r, munin, wear);
            foreach (PerchPose pp in r.GetComponents<PerchPose>()) Object.DestroyImmediate(pp);
            Raven src = Shoulder.Template(munin);
            if (src != null && src.m_visual != null) { Perches.ToTemplate(r, src.m_visual); Perches.Reveal(r, false); }
            r.transform.SetParent(null, true);
            Object.Destroy(seat);
            ShoulderLife l = r.AddComponent<ShoulderLife>();
            l.Init(life, munin, Mathf.Max(1, level));
            PostBird b = r.AddComponent<PostBird>();
            b.Munin = munin; b._life = l; b._start = at; b._since = Time.time;
            AnimDirector.Take(r, RavenAct.FlyOff, b, "the post");
            b._motor = RavenMotor.Of(r, munin, null);
            if (b._motor == null) { Object.Destroy(r); return null; }
            b._motor.Launch(face * 6f, "приліт", 1f); // already in the air: no take-off
            b.Flying(false);
            if (puff && src != null && src.m_despawnEffect != null) RavenFx.Own(src.m_despawnEffect, at, r.transform.rotation, null, 0.6f, -1);
            return b;
        }

        // at the one it is for: in from where it comes (their ZDO, every game near them)
        private static void ComeIn(Player them, string[] w)
        {
            // id, munin, level, outfit, wear, carry, dir, close, state, stamp, near, fromPid, kind
            bool munin = w[1] == "1", close = w[7] == "1";
            int level; int.TryParse(w[2], out level);
            float dir = Flt(w[6]);
            Vector3 me = them.transform.position;
            Vector3 at = close ? me + Way(dir + Random.Range(-40f, 40f)) * 4f + Vector3.up * 2.2f : me + Way(dir) * Far + Vector3.up * ComeHeight;
            PostBird b = Make(at, (me - at).normalized, munin, level, w[3], w[4], Player.m_localPlayer != null ? Player.m_localPlayer : them, close);
            if (b == null) return;
            b.Id = w[0]; b._in = true; b._close = close; b._dir = dir; b.TargetPid = them.GetPlayerID(); long.TryParse(w[11], out b.OwnerPid);
            b.Mode = ToThem;
            b.Hold(w[5], w.Length > 12 && w[12] == "1");
            Ins[b.Id] = b;
        }

        // back to its owner from far (their ZDO "home", every game near them)
        private static void ComeHome(Player owner, bool munin, string[] w, ZDO z)
        {
            float dir = Flt(w[3]);
            Vector3 me = owner.transform.position;
            Vector3 at = me + Way(dir) * (Far * 0.8f) + Vector3.up * ComeHeight;
            PostBird b = Make(at, (me - at).normalized, munin, Shoulder.LevelOn(owner, munin), z.GetString(munin ? Outfits.KeyM : Outfits.KeyH, ""),
                z.GetString(munin ? RavenWear.KeyM : RavenWear.KeyH, ""), owner, false);
            if (b == null) return;
            b.Id = w[1]; b.OwnerPid = owner.GetPlayerID(); b._dir = dir;
            b.GoHome();
        }

        // ---------------------------------------------------------------- the world's word (twice a second, every game)

        private static readonly bool[] Both = { false, true };

        internal static void Scan()
        {
            Player me = Player.m_localPlayer;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            Camera cam = Utils.GetMainCamera();
            if (cam == null && me == null) return;
            Vector3 eye = cam != null ? cam.transform.position : me.transform.position;
            double now = WorldTime.Now;
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null || (pl.transform.position - eye).sqrMagnitude > 200f * 200f) continue;
                ZDO z = NetFlag.Zdo(pl, false);
                if (z == null) continue;
                // a letter coming to this player
                string s = z.GetString(Post.KeyIn, "");
                if (s.Length > 0)
                {
                    string[] w = s.Split(Post.Sep);
                    if (w.Length >= 13)
                    {
                        PostBird b = ForMail(w[0]);
                        if (w[8] == "0" && w[10] != "1" && b == null && now - Dbl(w[9]) < 90.0 && FirstTime("i" + w[0])) ComeIn(pl, w);
                        else if (w[8] == "1" && b != null && !b._given) b.Given();
                    }
                }
                // this player's ravens off with the post
                foreach (bool munin in Both)
                {
                    string h = z.GetString(munin ? Post.KeyM : Post.KeyH, "");
                    if (h.Length == 0) continue; // (the usual case: one read, nothing more)
                    string[] w = h.Split(Post.Sep);
                    if (w.Length < 8 || w[0] != "home") continue;
                    PostBird b;
                    if (Outs.TryGetValue(w[1], out b) && b != null) { if (b.Mode != Home) b.GoHome(); continue; } // (near: the very bird that took it)
                    if (Homes.TryGetValue(HomeKey(pl.GetPlayerID(), munin), out b) && b != null) continue;
                    if (w[7] == "1" || now - Dbl(w[5]) > 20.0) continue; // near and its bird not drawn here, or too late: the shoulder's own fly-in
                    if (!FirstTime("h" + w[1])) continue; // (its bird already came: on the shoulder now, or gone)
                    ComeHome(pl, munin, w, z);
                }
            }
        }

        // ---------------------------------------------------------------- what it holds

        private void Hold(string prefab, bool parcel)
        {
            if (string.IsNullOrEmpty(prefab)) return;
            _carry = RavenFetch.Hold(gameObject, prefab, parcel);
        }

        private void Drop()
        {
            if (_carry != null) Object.Destroy(_carry.gameObject);
            _carry = null;
        }

        // handed over (the one it came to says so)
        internal void Given()
        {
            _given = true;
            Drop();
            if (_in) StartLeave(); // (off a shoulder near by: it waits for its owner's "home")
        }

        internal void GoHome()
        {
            Drop();
            Mode = Home; _since = Time.time; _hoverSince = -1f;
            if (_motor != null && _motor.Body != null) _motor.Body.NewLeg();
            Flying(false);
            Homes[HomeKey(OwnerPid, Munin)] = this;
        }

        private void StartLeave()
        {
            Mode = Leave; _since = Time.time; _hoverSince = -1f;
            _start = transform.position;
            _leaveAim = transform.position + Way(_dir) * Far + Vector3.up * LeaveHeight;
            if (_motor != null && _motor.Body != null) _motor.Body.NewLeg();
            Flying(false);
        }

        private void Flying(bool hover)
        {
            if (_life == null) return;
            _life.VanillaFly = true; _life.Still = !hover; _life.Hovering = hover; _life.Soaring = false;
        }

        private void Gone(string why)
        {
            Feathers.EndTrail(_trail); _trail = null;
            Perches.Puff(gameObject, Munin, "the post: " + why);
        }

        // where it hangs before them: in front of the face, a little to its side
        private Vector3 Before(Player t)
        {
            Vector3 f = t.transform.forward; f.y = 0f;
            f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, f) * (Munin ? -0.35f : 0.35f);
            return t.transform.position + f * 1.4f + side + Vector3.up * 1.75f;
        }

        // ---------------------------------------------------------------- the flight

        private void LateUpdate()
        {
            if (_motor == null || _life == null) { Destroy(gameObject); return; }
            if (GetComponent<ShoulderPin>() != null) { Destroy(this); return; } // the shoulder has it now
            float dt = Time.deltaTime, age = Time.time - _since;
            FlightStyle st = _motor.Body != null ? _motor.Body.Style : null;
            float top = st != null ? st.Top : 13f, cruise = st != null ? st.Cruise : 9f;
            Player them = TargetPid != 0 ? PerchView.PlayerOf(TargetPid) : null;
            switch (Mode)
            {
                case Off:
                {
                    // near them (seen here): straight to them; else a hundred metres off and gone
                    if (_near && them != null) { Mode = ToThem; _since = Time.time; break; }
                    if (_trail == null && age > 1f) _trail = Feathers.Trail(transform, Munin);
                    Vector3 aim = _start + Way(_dir) * Far + Vector3.up * LeaveHeight;
                    _motor.Steer(aim, top, false, 0f, dt, Vector3.zero, 1f);
                    _motor.Finish(dt, 0f);
                    Vector3 flat = transform.position - _start; flat.y = 0f;
                    if (flat.magnitude > Far * 0.9f || age > 16f) { Gone("off a hundred metres"); return; }
                    break;
                }
                case ToThem:
                {
                    if (them == null) { if (_in || age > 3f) { Gone("the one it was sent to is not here"); return; } break; }
                    Vector3 aim = Before(them);
                    float d = Vector3.Distance(transform.position, aim);
                    Vector3 look = them.transform.position - transform.position; look.y = 0f;
                    if (d < 8f && _motor.Body != null && look.sqrMagnitude > 0.01f) _motor.Body.FaceHint(look.normalized, 1f - d / 8f);
                    float left = _motor.Steer(aim, d > 20f ? top : cruise, d < 20f, d < 4f ? 1.5f : 0f, dt, d < 20f ? them.GetVelocity() : Vector3.zero, d < 6f ? 0.3f : 1f);
                    _motor.Finish(dt, d < 3f ? Mathf.Clamp01(1f - d / 3f) * 0.5f : 0f);
                    if (left < 1.2f) { Mode = Hang; _hoverSince = Time.time; Flying(true); }
                    else if (age > 60f) { Gone("never got to them"); return; }
                    break;
                }
                case Hang:
                {
                    if (them == null) { if (_in) StartLeave(); else Gone("the one it waited by is gone"); break; }
                    Vector3 aim = Before(them);
                    Vector3 look = them.transform.position - transform.position; look.y = 0f;
                    if (_motor.Body != null && look.sqrMagnitude > 0.01f) _motor.Body.FaceHint(look.normalized, 1f);
                    _motor.Steer(aim, cruise * 0.7f, true, 1f, dt, them.GetVelocity(), 0.3f);
                    _motor.Finish(dt, 0.3f);
                    if (!_in && _given && Time.time - _hoverSince > 45f) { Gone("its owner never called it home"); return; }
                    if (Time.time - _hoverSince > 300f) { Gone("waited too long"); return; }
                    break;
                }
                case Leave:
                {
                    if (!_farewell) { _farewell = true; ScreamFx.CawAt(transform, transform.position, Munin ? 0.9f : 1.35f, 0.5f, false); }
                    if (_trail == null && age > 1f) _trail = Feathers.Trail(transform, Munin);
                    if (_close && age > 1.5f) { Gone("off from under a roof"); return; }
                    _motor.Steer(_leaveAim, top, false, 0f, dt, Vector3.zero, 1f);
                    _motor.Finish(dt, 0f);
                    Vector3 flat = transform.position - _start; flat.y = 0f;
                    if (flat.magnitude > Far * 0.9f || age > 16f) { Gone("off after handing it over"); return; }
                    break;
                }
                case Home:
                {
                    Player owner = PerchView.PlayerOf(OwnerPid);
                    if (owner == null) { Gone("its owner is not here"); return; }
                    Vector3 aim = owner.transform.position + Vector3.up * 2.4f;
                    float d = Vector3.Distance(transform.position, aim);
                    _motor.Steer(aim, d > 15f ? top : cruise, d < 6f, 0f, dt, d < 15f ? owner.GetVelocity() : Vector3.zero, d < 4f ? 0f : 1f);
                    _motor.Finish(dt, 0f);
                    // the shoulder takes this very bird when it is drawn again (from however far: it flies on)
                    if (Time.time >= _noteAt) { _noteAt = Time.time + 0.5f; Perches.NoteLeft(OwnerPid, Munin, transform.position, transform.rotation, gameObject); }
                    if (age > 35f) { Gone("never taken back onto the shoulder"); return; }
                    break;
                }
            }
            if (this != null && _life != null) _life.Apply();
            if (_carry != null) { ClawGrip grip = _carry.GetComponent<ClawGrip>(); if (grip != null) grip.Follow(); } // (the legs just posed: the parcel in the claws this very frame)
        }

        private void OnDestroy()
        {
            PostBird b;
            if (Outs.TryGetValue(Id, out b) && b == this) Outs.Remove(Id);
            if (Ins.TryGetValue(Id, out b) && b == this) Ins.Remove(Id);
            long k = HomeKey(OwnerPid, Munin);
            if (Homes.TryGetValue(k, out b) && b == this) Homes.Remove(k);
            Feathers.EndTrail(_trail); _trail = null;
            if (_carry != null) Object.Destroy(_carry.gameObject); // (handed to the shoulder: nothing in its beak any more)
            if (!_yielded) AnimDirector.Release(gameObject, this);
        }
    }
}
#endif
