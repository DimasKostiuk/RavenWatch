#if RAVEN_ITEM
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace HuginWhispers
{
    // «Сісти на гравця / на тварину» (the user, 2026-10-15): «Туди» on another player or a tamed animal sends the raven
    // onto them. A player: the head, or a shoulder of theirs that is free (no raven of their own there, nobody else's
    // either), by an even chance among the free places; anyone may be sat on, they shake it off with a roll (a dodge),
    // and with «Other players ravens may sit on me» off no raven sits on them at all (one sitting there is shaken off).
    // A tamed animal: its back (not while someone rides it) or its head, the same even chance; wild ones: no.
    // It is a «sit here» spot (SitHere) that follows: the spot's ZDO says whom and where (FollowKey "user:id|slot", for
    // an animal "|x|y|z" too: the place in the animal's own space, measured once on its body), and every game puts the
    // spot there each frame (SpotFollow); the perches do the rest — the flight to a seat that moves, the sitting bird a
    // child of the seat. The raven comes back when they are gone (dead, unloaded, out of the game) or you are far from
    // them (Perches.Tick).
    internal static class SitOn
    {
        internal const string FollowKey = "RavenWatch_follow", NoSitKey = "RavenWatch_noSit";
        internal const int Head = 0, Right = 1, Left = 2, Back = 3, BeastHead = 4, Thing = 5, Slots = 6; // Thing: a ship, a cart (SitHere)
        internal static ConfigEntry<bool> LetOthers;
        internal static float HeadUp = 0.19f;   // m from a player's head bone along the head's own up: on top of the head
        internal static float HeadFwd = 0.03f;  // m along the head's own forward
        internal static float Lost = 3f;        // s the one it sits on not found before the raven comes back

        private static int _mask;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        // another player or a creature under the crosshair (a fat ray: a body is easy to hit, but not exactly)
        internal static Character CharacterAt(Transform cam, Player me, float far)
        {
            if (_mask == 0) _mask = LayerMask.GetMask("character", "character_net", "character_ghost", "character_noenv");
            int n = Physics.SphereCastNonAlloc(cam.position, 0.3f, cam.forward, Hits, far, _mask, QueryTriggerInteraction.Ignore);
            Character best = null;
            float bd = far;
            for (int i = 0; i < n; i++)
            {
                Character c = Hits[i].collider != null ? Hits[i].collider.GetComponentInParent<Character>() : null;
                if (c == null || c == me || Hits[i].distance >= bd) continue;
                bd = Hits[i].distance; best = c;
            }
            return best;
        }

        internal static string NameOf(Character c)
        {
            Player pl = c as Player;
            if (pl != null) return pl.GetPlayerName();
            string n = c != null ? c.m_name ?? "" : "";
            return Localization.instance != null ? Lang.Game(n) : n;
        }

        // why not onto this one (null: there is room); wild: a wild beast too (Munin's «Приборкання»)
        internal static string WhyNot(Character c) { return WhyNot(c, false); }
        internal static string WhyNot(Character c, bool wild)
        {
            if (c == null) return Lang.T("Нема кого.");
            string name = NameOf(c);
            if (c.IsDead()) return Lang.F("{0} зараз не тут.", name);
            Player pl = c as Player;
            if (pl != null)
            {
                ZDO z = NetFlag.Zdo(pl, false);
                if (z != null && z.GetBool(NoSitKey, false)) return Lang.F("{0} не пускає на себе чужих воронів.", name);
            }
            else if (!c.IsTamed() && !wild) return Lang.F("{0} — не ручна тварина. Ворон на диких не сідає.", name);
            if (Free(c).Count == 0) return Lang.F("{0}: уже нема вільного місця.", name);
            return null;
        }

        // the free places on them: a player's head and the shoulders without a raven of theirs; an animal's back (nobody
        // riding it) and head; none taken by another spot
        private static readonly List<int> _free = new List<int>(3);
        private static readonly bool[] _taken = new bool[Slots];
        private static List<int> Free(Character c)
        {
            _free.Clear();
            System.Array.Clear(_taken, 0, Slots);
            ZDOID id = c.GetZDOID();
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || !v.IsSpot || v.Idle) continue;
                SpotFollow f = v.GetComponent<SpotFollow>();
                if (f != null && f.Target == id && f.Slot >= 0 && f.Slot < Slots) _taken[f.Slot] = true;
            }
            Player pl = c as Player;
            if (pl != null)
            {
                if (!_taken[Head]) _free.Add(Head);
                if (!_taken[Right] && Shoulder.ModelOf(pl, false) == null) _free.Add(Right);
                if (!_taken[Left] && Shoulder.ModelOf(pl, true) == null) _free.Add(Left);
                return _free;
            }
            Tameable tame = c.GetComponent<Tameable>();
            if (!_taken[Back] && (tame == null || !tame.HaveRider())) _free.Add(Back);
            if (!_taken[BeastHead]) _free.Add(BeastHead);
            return _free;
        }

        // the order (Perches.GoHere for a player or an animal under the crosshair)
        internal static string Go(Player p, bool munin, Character c) { PerchView v; return Go(p, munin, c, true, -1, out v); }

        // paid: an order (strength); unpaid: the raven's own doing (Victim: onto the one it mocks), on the slot it
        // wants when that one is free (-1: any)
        internal static string Go(Player p, bool munin, Character c, bool paid, int want, out PerchView v) { return Go(p, munin, c, paid, want, false, out v); }
        internal static string Go(Player p, bool munin, Character c, bool paid, int want, bool wild, out PerchView v)
        {
            v = null;
            string why = WhyNot(c, wild) ?? (paid ? SitHere.WhyNotNow(p, munin) : null); // (WhyNot counted the free places: Free's list is that)
            if (why != null) return why;
            List<int> free = _free;
            int slot = want >= 0 && free.Contains(want) ? want : free[Random.Range(0, free.Count)];
            ZDOID id = c.GetZDOID();
            string follow = id.UserID + ":" + id.ID + "|" + slot;
            Vector3 local = Vector3.zero;
            if (slot == Back || slot == BeastHead)
            {
                Vector3 top;
                if (slot == Back ? !OnBody(c, Body.Center(c), out top) : !OnHead(c, out top)) return Lang.T("Не видно, куди сісти.");
                local = c.transform.InverseTransformPoint(top);
                follow += "|" + F(local.x) + "|" + F(local.y) + "|" + F(local.z);
            }
            Transform bone = null;
            Vector3 at; Quaternion rot;
            if (!Place(c, slot, local, ref bone, out at, out rot)) return Lang.T("Не видно, куди сісти.");
            return SitHere.Place(p, munin, at, rot, follow, paid, out v);
        }

        private static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }

        // the top of the head: the body's colliders often stand far over a head held low in front (a lox, a boar), so a
        // collider top counts only a little over the head point; else the head's own size by the beast's girth
        private static bool OnHead(Character c, out Vector3 at)
        {
            Vector3 head = Body.Head(c);
            float reach = Mathf.Clamp(Body.Radius(c) * 0.35f, 0.12f, 0.6f);
            Vector3 hit;
            OnBody(c, head, out hit);
            float up = hit.y - head.y;
            at = up > 0.03f && up < reach * 1.6f ? hit : head + Vector3.up * reach;
            if (Plugin.Instance != null) Plugin.Instance.Log("Seat on " + Utils.GetPrefabName(c.gameObject) + "'s head: " + (up > 0.03f && up < reach * 1.6f ? "its collider top " : "its girth ") + (at.y - head.y).ToString("0.00") + " m over the head point (girth " + Body.Radius(c).ToString("0.00") + ")");
            return true;
        }

        // the top of the animal's body right over a point of it (its back over the middle, its head over the head point)
        private static bool OnBody(Character c, Vector3 near, out Vector3 at)
        {
            at = near;
            Collider[] cols = c.GetComponentsInChildren<Collider>();
            float top = near.y;
            foreach (Collider col in cols) if (col != null && !col.isTrigger) top = Mathf.Max(top, col.bounds.max.y);
            Ray down = new Ray(new Vector3(near.x, top + 1f, near.z), Vector3.down);
            bool any = false;
            foreach (Collider col in cols)
            {
                RaycastHit h;
                if (col == null || col.isTrigger || !col.Raycast(down, out h, top + 1f - (near.y - 2f))) continue;
                if (!any || h.point.y > at.y) { at = h.point; any = true; }
            }
            if (!any) at = near + Vector3.up * 0.2f; // (no body there to stand on: just over the point)
            return true;
        }

        // where the seat is on them now (bone: a player's shoulder, found once and kept by the caller; local: an
        // animal's place in its own space)
        internal static bool Place(Character c, int slot, Vector3 local, ref Transform bone, out Vector3 at, out Quaternion rot)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            Quaternion facing = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            rot = facing;
            if (slot == Back) { at = c.transform.TransformPoint(local); return true; }
            if (slot == BeastHead)
            {
                // on the animal's head bone: it bows to graze and looks round, the bird goes with it (a point fixed in the
                // body hung in the air over a bowed head). The point in the bone's own space is taken from the pose of the
                // first moment and kept
                if (bone == null) bone = HeadBone(c);
                if (bone == null) { at = c.transform.TransformPoint(local); return true; }
                int key = bone.GetInstanceID() ^ local.GetHashCode();
                Vector3 onBone;
                if (!OnHeadBone.TryGetValue(key, out onBone))
                {
                    if (OnHeadBone.Count > 64) OnHeadBone.Clear();
                    onBone = bone.InverseTransformPoint(c.transform.TransformPoint(local));
                    OnHeadBone[key] = onBone;
                }
                at = bone.TransformPoint(onBone);
                return true;
            }
            if (slot == Head)
            {
                // on the head bone itself: it bows, tilts and turns with the head (a point straight up in the world from
                // the head floated over a bowed head and stood off it to a side), the bird upright, facing where the head does
                Transform head = HeadBone(c);
                if (head == null) { at = Body.Head(c) + Vector3.up * HeadUp; return true; }
                Quaternion frame = head.rotation * Quaternion.Inverse(HeadRest());
                at = head.position + frame * new Vector3(0f, HeadUp, HeadFwd);
                Vector3 look = Vector3.ProjectOnPlane(frame * Vector3.forward, Vector3.up);
                if (look.sqrMagnitude > 0.0001f) rot = Quaternion.LookRotation(look.normalized, Vector3.up);
                return true;
            }
            bool left = slot == Left;
            if (bone == null)
            {
                string name = left ? Shoulder.MuninBone : Shoulder.HuginBone;
                foreach (Transform t in c.GetComponentsInChildren<Transform>(true)) if (t.name == name) { bone = t; break; }
            }
            if (bone == null) { at = Vector3.zero; return false; }
            Vector3 off = left ? new Vector3(-Shoulder.Offset.x, Shoulder.Offset.y, Shoulder.Offset.z) : Shoulder.Offset;
            at = bone.position + facing * off + Vector3.up * (0.03f - Shoulder.PerchDrop);
            return true;
        }

        // the head bone's turn in the body's own space as the player stands at rest (the game's Player prefab, the
        // same rig for every player): what turns past it is the head's own bowing and turning
        private static readonly System.Reflection.FieldInfo HeadF = HarmonyLib.AccessTools.Field(typeof(Character), "m_head");
        private static Transform HeadBone(Character c) { return c != null && HeadF != null ? HeadF.GetValue(c) as Transform : null; }

        private static readonly Dictionary<int, Vector3> OnHeadBone = new Dictionary<int, Vector3>(); // an animal's head seat in its head bone's space

        private static bool _restKnown;
        private static Quaternion _headRest = Quaternion.identity;
        private static Quaternion HeadRest()
        {
            if (_restKnown) return _headRest;
            GameObject pre = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Player") : null;
            Character pc = pre != null ? pre.GetComponent<Character>() : null;
            Transform ph = HeadBone(pc);
            if (ph == null) return Quaternion.identity; // (not loaded yet: again next time)
            _headRest = Quaternion.Inverse(pre.transform.rotation) * ph.rotation;
            _restKnown = true;
            if (Plugin.Instance != null) Plugin.Instance.Log("Seat on a head: head bone " + ph.name + " at rest turned " + _headRest.eulerAngles + " in the body");
            return _headRest;
        }

        // the follow string for a spot on a thing that moves (a ship, a cart): the thing and the point in its own space
        internal static string OnThing(ZNetView thing, Vector3 at)
        {
            if (thing == null || !thing.IsValid()) return null;
            ZDOID id = thing.GetZDO().m_uid;
            Vector3 local = thing.transform.InverseTransformPoint(at);
            return id.UserID + ":" + id.ID + "|" + Thing + "|" + F(local.x) + "|" + F(local.y) + "|" + F(local.z);
        }

        // a tamed animal's back for a raven's own visit (WorldVisit): where, turned how, and the follow string; false when
        // it is not free (ridden, taken)
        internal static bool BackOf(Character c, out Vector3 at, out Quaternion rot, out string follow)
        {
            at = Vector3.zero; rot = Quaternion.identity; follow = null;
            if (c == null || c.IsDead() || !c.IsTamed() || c is Player || !Free(c).Contains(Back)) return false;
            Vector3 top;
            if (!OnBody(c, Body.Center(c), out top)) return false;
            Vector3 local = c.transform.InverseTransformPoint(top);
            ZDOID id = c.GetZDOID();
            follow = id.UserID + ":" + id.ID + "|" + Back + "|" + F(local.x) + "|" + F(local.y) + "|" + F(local.z);
            Transform bone = null;
            return Place(c, Back, local, ref bone, out at, out rot);
        }

        internal static bool Follows(PerchView v) { SpotFollow f = v != null ? v.GetComponent<SpotFollow>() : null; return f != null && f.Target != ZDOID.None; }

        // «the spot rides on the sender's player»: they may shake its raven off (Perches.OnGone)
        internal static bool TargetIs(PerchView v, long sender)
        {
            SpotFollow f = v != null ? v.GetComponent<SpotFollow>() : null;
            return f != null && f.Target != ZDOID.None && f.Slot <= Left && f.Target.UserID == sender;
        }

        // the raven's owner: why it comes back from them now (null: it stays)
        internal static string Gone(PerchView v)
        {
            SpotFollow f = v != null ? v.GetComponent<SpotFollow>() : null;
            if (f == null || f.Target == ZDOID.None || !f.IsLost) return null;
            return Lang.F("{0}: уже не поруч", (f.LastName.Length > 0 ? f.LastName : f.Slot <= Left ? Lang.T("Гравець") : f.Slot == Thing ? Lang.T("Те, на чому він сидів") : Lang.T("Тварина")));
        }

        // the corner line when it flies there: to whom, onto what
        internal static string Text(PerchView v)
        {
            SpotFollow f = v != null ? v.GetComponent<SpotFollow>() : null;
            if (f == null || f.Target == ZDOID.None) return null;
            if (f.Slot == Thing) return null; // (a ship, a cart: the «sit here» line, as on a building)
            string where = f.Slot == Head || f.Slot == BeastHead ? Lang.T(" полетів на голову") : f.Slot == Back ? Lang.T(" полетів на спину") : Lang.T(" полетів на плече");
            return where + (f.LastName.Length > 0 ? ": " + f.LastName + "." : ".");
        }

        // From SitHere.Tick (twice a second): your «not on me» told to everyone, and with it off every raven on you shaken off
        internal static void Tick()
        {
            Player me = Player.m_localPlayer;
            if (me == null) return;
            bool let = LetOthers == null || LetOthers.Value;
            ZDO z = NetFlag.Zdo(me, true);
            if (z != null && z.GetBool(NoSitKey, false) == let) z.Set(NoSitKey, !let);
            if (let) return;
            foreach (PerchView v in PerchView.All)
            {
                SpotFollow f = v != null && v.IsSpot ? v.GetComponent<SpotFollow>() : null;
                if (f != null && f.Target == me.GetZDOID()) Shake(v, me, false);
            }
        }

        // the ravens on this spot riding on you: off (asked of the spot's holder, OnGone lets you); each one once
        private static readonly HashSet<string> _shaken = new HashSet<string>();
        internal static void Shake(PerchView v, Player me, bool say)
        {
            if (v == null || me == null || ZRoutedRpc.instance == null) return;
            for (int i = 0; i < v.Count; i++)
            {
                Perches.Occupant o = v.At(i);
                if (o == null || !_shaken.Add(o.Token)) continue;
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Perches.RpcGone, v.Id, i, o.Token);
                if (say) People.SendShook(o); // shaken off by a roll: its owner's ravens take it ill
                if (say) me.Message(MessageHud.MessageType.TopLeft, Grammar.Fix(Lang.F("Ти струси{{в|ла}} з себе {0} гравця {1}.", (o.Munin ? Lang.T("Муніна") : Lang.T("Хугіна")), o.Name), me));
            }
            if (_shaken.Count > 64) _shaken.Clear();
        }
    }

    // On the «sit here» spot: if its ZDO names a player or an animal and a place on them, the spot is put there every
    // frame (every game alike); the spot's holder also moves its ZDO along now and then, so the others keep it loaded.
    internal class SpotFollow : MonoBehaviour
    {
        private ZNetView _nv;
        private string _raw = "";
        internal ZDOID Target = ZDOID.None;
        internal int Slot = -1;
        internal string LastName = "";
        private Vector3 _local;
        private Character _who;
        private Transform _anchor; // what it rides on (the character, or the ship / cart)
        private PerchView _view;
        internal Character Who { get { return _who; } }
        private Transform _bone;
        private Transform _head; private bool _headLooked; private Vector3 _onHead; private Quaternion _headRest; // a trader's head (WorldVisit)
        private float _readAt, _posAt, _seenAt;
        private int _peck = -1;
        private string _told;
        // on an animal: the bird's inertia (the seat's last place and speed, the lean on its spring, the turn lagging)
        internal static float LeanPerSpeed = 5f, LeanMax = 24f, LeanSpring = 70f, LeanDamp = 7f, TurnLag = 7f, FastFollow = 12f, SlowFollow = 2.5f;
        private Vector3 _lastAt, _velFast, _velSlow, _lean, _leanVel;
        private Quaternion _yaw = Quaternion.identity;
        private bool _haveLast; // the raven on you that you were told about (its seat's token)
        internal const string PeckKey = "RavenWatch_peck"; // a count: one more, one more pecking (WorldVisit)
        internal const string BatheKey = "RavenWatch_bathe"; // a bath (WorldVisit): its count << 8 | its seconds
        private int _bathe = -1;

        private void Awake() { _nv = GetComponent<ZNetView>(); _view = GetComponent<PerchView>(); _seenAt = Time.time; }

        internal bool IsLost { get { return Target != ZDOID.None && Time.time - _seenAt > SitOn.Lost; } }

        private void Read()
        {
            if (Time.time < _readAt || _nv == null || !_nv.IsValid()) return;
            _readAt = Time.time + 0.25f;
            int peck = _nv.GetZDO().GetInt(PeckKey, 0);
            if (_peck >= 0 && peck != _peck && _view != null) { PerchLife life = _view.LifeAt(0); if (life != null) life.Peck(WorldVisit.PeckSeconds); }
            _peck = peck;
            int bathe = _nv.GetZDO().GetInt(BatheKey, 0);
            if (_bathe >= 0 && bathe != _bathe && bathe != 0 && _view != null) { PerchLife life = _view.LifeAt(0); if (life != null) life.Bathe(bathe & 0xFF); }
            _bathe = bathe;
            string raw = _nv.GetZDO().GetString(SitOn.FollowKey, "");
            if (raw != _raw)
            {
                _raw = raw; _who = null; _bone = null; Target = ZDOID.None; Slot = -1; _local = Vector3.zero;
                string[] w = raw.Split('|');
                string[] id = w[0].Split(':');
                long user; uint n; int slot;
                if (w.Length >= 2 && id.Length == 2 && long.TryParse(id[0], out user) && uint.TryParse(id[1], out n) && int.TryParse(w[1], out slot))
                {
                    Target = new ZDOID(user, n); Slot = slot; _seenAt = Time.time;
                    if (w.Length >= 5)
                    {
                        System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
                        float x, y, z;
                        float.TryParse(w[2], System.Globalization.NumberStyles.Float, ci, out x);
                        float.TryParse(w[3], System.Globalization.NumberStyles.Float, ci, out y);
                        float.TryParse(w[4], System.Globalization.NumberStyles.Float, ci, out z);
                        _local = new Vector3(x, y, z);
                    }
                }
            }
            // it landed on you: you are told whose it is and how to shake it off (once a raven; four times a second is enough)
            if (Slot <= SitOn.Left && _who != null && ReferenceEquals(_who, Player.m_localPlayer) && _view != null && _view.Settled(0)) Tell((Player)_who);
            // your own raven of that side is back (from a perch, the post, put on again): the stranger makes room for it.
            // Your game alone decides it — its own shoulders are what it knows for sure, the others' only see them late
            if ((Slot == SitOn.Right || Slot == SitOn.Left) && _who != null && ReferenceEquals(_who, Player.m_localPlayer)
                && Shoulder.ModelOf((Player)_who, Slot == SitOn.Left) != null) SitOn.Shake(_view, (Player)_who, false);
            if (Target != ZDOID.None && _who == null && ZNetScene.instance != null)
            {
                GameObject g = ZNetScene.instance.FindInstance(Target);
                _who = g != null ? g.GetComponent<Character>() : null;
                _anchor = g != null ? g.transform : null;
                _bone = null; _head = null; _headLooked = false;
                if (_who != null) LastName = SitOn.NameOf(_who);
                else if (g != null) { Piece pc = g.GetComponent<Piece>(); LastName = pc != null && Localization.instance != null ? Lang.Game(pc.m_name) : ""; }
            }
        }

        // the seat stays on the beast's point (the feet must not leave it); the bird turns about its feet: its heading
        // follows the beast's a moment late, and it leans against the beast's pull — back when it starts, forward when it
        // stops, outward in a turn — swinging a little before it settles
        private Quaternion Inertia(Vector3 at, Quaternion rot)
        {
            float dt = Time.deltaTime;
            if (!_haveLast || dt <= 0f || (at - _lastAt).sqrMagnitude > 4f) // first, or a jump (loaded again, teleported): no swing
            {
                _haveLast = true; _lastAt = at; _velFast = _velSlow = _lean = _leanVel = Vector3.zero; _yaw = rot;
                return rot;
            }
            // the pull, without the frame-to-frame jitter: the speed followed quickly against the speed followed slowly
            // (their gap grows while the beast speeds up, slows down or swerves, and is nothing while it goes evenly)
            Vector3 vel = (at - _lastAt) / dt;
            _lastAt = at;
            vel.y = 0f;
            _velFast = Vector3.Lerp(_velFast, vel, 1f - Mathf.Exp(-FastFollow * dt));
            _velSlow = Vector3.Lerp(_velSlow, vel, 1f - Mathf.Exp(-SlowFollow * dt));
            Vector3 want = Vector3.ClampMagnitude(-(_velFast - _velSlow) * LeanPerSpeed, LeanMax); // degrees, toward where it is thrown
            _leanVel += ((want - _lean) * LeanSpring - _leanVel * LeanDamp) * dt;
            _lean = Vector3.ClampMagnitude(_lean + _leanVel * dt, LeanMax);
            _yaw = Quaternion.Slerp(_yaw, rot, 1f - Mathf.Exp(-TurnLag * dt));
            float deg = _lean.magnitude;
            if (deg < 0.05f) return _yaw;
            return Quaternion.AngleAxis(deg, Vector3.Cross(Vector3.up, _lean / deg)) * _yaw;
        }

        private void Tell(Player me)
        {
            Perches.Occupant o = _view.At(0);
            if (o == null || o.Token == _told) return;
            _told = o.Token;
            string where = Slot == SitOn.Head ? Lang.T("на голову") : Lang.T("на плече");
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} гравця {1} сів тобі {2}. Перекат — струсити.", (o.Munin ? Lang.T("Мунін") : Lang.T("Хугін")), o.Name, where), 0, RavenItem.Icon(o.Munin));
        }

        private void LateUpdate()
        {
            Read();
            if (Target == ZDOID.None) return;
            if (Slot == SitOn.Thing)
            {
                // on a ship or a cart: its point, turned with it. On a trader's head: on the head bone, so it nods and
                // turns with the head (the point in the bone's space from the first moment's pose, as on an animal's head)
                if (_anchor == null) return;
                _seenAt = Time.time;
                if (!_headLooked)
                {
                    _headLooked = true;
                    _head = _anchor.GetComponent<Trader>() != null ? WorldVisit.HeadBone(_anchor) : null;
                    if (_head != null) { _onHead = _head.InverseTransformPoint(_anchor.TransformPoint(_local)); _headRest = Quaternion.Inverse(_anchor.rotation) * _head.rotation; }
                }
                Vector3 fw = _anchor.forward, spot;
                if (_head != null) { spot = _head.TransformPoint(_onHead); fw = _head.rotation * Quaternion.Inverse(_anchor.rotation * _headRest) * fw; } // (the head's own turn past its rest)
                else spot = _anchor.TransformPoint(_local);
                fw = Vector3.ProjectOnPlane(fw, Vector3.up);
                transform.SetPositionAndRotation(spot, Quaternion.LookRotation(fw.sqrMagnitude > 0.0001f ? fw.normalized : Vector3.forward, Vector3.up));
                if (_nv != null && _nv.IsValid() && _nv.IsOwner() && Time.time >= _posAt) { _posAt = Time.time + 1f; _nv.GetZDO().SetPosition(transform.position); }
                return;
            }
            if (_who == null || _who.IsDead()) { _who = null; return; }
            _seenAt = Time.time;
            // it rides on you and you roll: shaken off (looked at every frame: a roll is short)
            if (Slot <= SitOn.Left && ReferenceEquals(_who, Player.m_localPlayer) && ((Player)_who).InDodge()) SitOn.Shake(_view, (Player)_who, true);
            Vector3 at; Quaternion rot;
            if (!SitOn.Place(_who, Slot, _local, ref _bone, out at, out rot)) return;
            if (Slot == SitOn.Back || Slot == SitOn.BeastHead) rot = Inertia(at, rot);
            transform.SetPositionAndRotation(at, rot);
            if (_nv != null && _nv.IsValid() && _nv.IsOwner() && Time.time >= _posAt) { _posAt = Time.time + 1f; _nv.GetZDO().SetPosition(at); }
        }
    }
}
#endif
