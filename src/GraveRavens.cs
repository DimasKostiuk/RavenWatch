#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Ворон у могилі» (the user, 2026-10-16): a raven that went into a grave with the rest of the things is seen at that
    // very grave — the grave it lies in, never another: what is in the grave says it, not where you died. Every game
    // looks into the graves near it (their contents come over the network with the grave), so all see the same bird
    // without a word sent. It is only a picture of the raven (no item, no network object): the raven itself stays in
    // the grave until it is taken.
    //   - it circles over the stone, or sits on it: by a die rolled for each StateSeconds of the world's clock (the same
    //     in every game), the share of sitting by its character (one that goes out less sits more) and when stuffed;
    //   - it sits for sure: «Мерзляк» in rain, a thunderstorm or snow (not with the frost medallion), «Сонько» at night,
    //     starving, not yet woken, and wherever there is no sky over the stone (a roof, underground);
    //   - its owner takes it out of the grave: the very bird flies onto the shoulder (Perches.NoteLeft → Shoulder.Make);
    //   - anyone else touching the grave (each E on it) is pecked for PeckShare of their health, to death if it comes
    //     to that, and can never take the raven out (a click, a drag, «take all»: the rest of the things they may take).
    internal static class GraveRavens
    {
        internal static float ViewRange = 70f, SeeEvery = 1f, StateSeconds = 45f;
        internal static float SitBase = 0.3f, StuffedSit = 0.25f;  // the share of the time it sits (÷ its character's eagerness to go out)
        internal static float PeckShare = 0.2f, HandRange = 30f;
        private const string OwnerKey = "owner"; // the game keeps whose grave it is there (ZDOVars.s_owner)

        private static readonly List<TombStone> Graves = new List<TombStone>();
        private static readonly Dictionary<string, GraveBird> Birds = new Dictionary<string, GraveBird>();
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static readonly List<string> Drop = new List<string>();
        private static float _next, _peckAt;

        internal static void Add(TombStone t) { if (t != null && !Graves.Contains(t)) Graves.Add(t); }

        internal static long OwnerOf(TombStone t)
        {
            ZNetView nv = t != null ? t.GetComponent<ZNetView>() : null;
            return nv != null && nv.IsValid() ? nv.GetZDO().GetLong(OwnerKey, 0L) : 0L;
        }

        // From Shoulder.Tick (twice a second, every game)
        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + SeeEvery;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            Camera cam = Utils.GetMainCamera();
            Player me = Player.m_localPlayer;
            if (cam == null && me == null) return;
            Vector3 eye = cam != null ? cam.transform.position : me.transform.position;
            Seen.Clear();
            for (int i = Graves.Count - 1; i >= 0; i--)
            {
                TombStone t = Graves[i];
                if (t == null) { Graves.RemoveAt(i); continue; }
                if ((t.transform.position - eye).sqrMagnitude > ViewRange * ViewRange) continue;
                ZNetView nv = t.GetComponent<ZNetView>();
                Container c = t.GetComponent<Container>();
                if (nv == null || !nv.IsValid() || c == null || c.GetInventory() == null) continue;
                long owner = nv.GetZDO().GetLong(OwnerKey, 0L);
                int n = 0;
                foreach (ItemDrop.ItemData item in c.GetInventory().GetAllItems())
                {
                    bool munin;
                    if (!RavenItem.IsRaven(item, out munin)) continue;
                    if (!RavenSeat.WasWorn(item)) continue; // only one that sat on the shoulder at the death; a raven carried in the bag lies quietly
                    string id = Backup.Id(item);
                    if (string.IsNullOrEmpty(id)) id = (munin ? "m" : "h") + n;
                    string key = nv.GetZDO().m_uid + "/" + id;
                    Seen.Add(key);
                    GraveBird b;
                    if (Birds.TryGetValue(key, out b) && b != null) b.Item = item; // (the grave reads its things anew: new item objects)
                    else { b = GraveBird.Make(t, item, munin, owner, id, n); if (b != null) Birds[key] = b; }
                    n++;
                }
            }
            // the ones no longer there: taken out (to the shoulder, if its owner is near), the grave gone, or out of sight
            Drop.Clear();
            foreach (KeyValuePair<string, GraveBird> kv in Birds) if (!Seen.Contains(kv.Key)) Drop.Add(kv.Key);
            foreach (string key in Drop)
            {
                GraveBird b = Birds[key];
                Birds.Remove(key);
                if (b == null) continue;
                if (b.Grave != null && (b.Grave.transform.position - eye).sqrMagnitude > ViewRange * ViewRange) { Object.Destroy(b.gameObject); continue; } // far: quietly
                Gone(b);
            }
        }

        // out of its grave: onto its owner's shoulder when the owner is near and that shoulder is free; another raven
        // of its kind there already: straight up and away (it never takes that one's place); else a puff
        private static void Gone(GraveBird b)
        {
            Player owner = PerchView.PlayerOf(b.Owner);
            if (owner == null || owner.IsDead() || Vector3.Distance(owner.transform.position, b.transform.position) >= HandRange)
            {
                Perches.Puff(b.gameObject, b.Munin, owner == null ? "out of its grave, its owner not here" : owner.IsDead() ? "out of its grave, its owner dead" : "out of its grave, its owner too far");
                return;
            }
            if (ShoulderTaken(owner, b)) b.LeaveUp();
            else if (Shoulder.ModelOf(owner, b.Munin) != null) Perches.Puff(b.gameObject, b.Munin, "out of its grave, already drawn on the shoulder"); // (its shoulder made it first: no second one left at the grave)
            else b.HandOver();
        }

        // that shoulder already has another raven of this kind: sitting there, or out flying (it comes back to its own place).
        // Your own: by the raven worn (the one just taken out of the grave counts as free); anyone's: by the raven drawn there
        private static bool ShoulderTaken(Player owner, GraveBird b)
        {
            if (owner == Player.m_localPlayer)
            {
                ItemDrop.ItemData worn = RavenSeat.On(owner, b.Munin);
                if (worn != null) return Backup.Id(worn) != b.RavenId;
            }
            return Shoulder.ModelOf(owner, b.Munin) != null;
        }

        // From RavenSeat.Reseat: this raven, just taken out of the grave, goes back on the shoulder: its bird flies there
        // (a raven still drawn on that shoulder: the grave's bird flies up and away, the one there stays as it is)
        internal static void HandOff(Player p, ItemDrop.ItemData item)
        {
            string id = Backup.Id(item);
            if (p == null || string.IsNullOrEmpty(id)) return;
            string found = null;
            foreach (KeyValuePair<string, GraveBird> kv in Birds) if (kv.Value != null && kv.Value.RavenId == id && kv.Value.Owner == p.GetPlayerID()) { found = kv.Key; break; }
            if (found == null) return;
            GraveBird b = Birds[found];
            Birds.Remove(found);
            if (Shoulder.ModelOf(p, b.Munin) != null) b.LeaveUp();
            else b.HandOver();
        }

        // ---------------------------------------------------------------- a thief at the grave

        private static GraveBird BirdAt(TombStone t, bool munin)
        {
            GraveBird any = null;
            foreach (GraveBird b in Birds.Values) if (b != null && b.Grave == t) { if (b.Munin == munin) return b; any = b; }
            return any;
        }

        // the ravens in this grave (false: Hugin, true: Munin)
        private static List<bool> RavensIn(TombStone t)
        {
            List<bool> l = new List<bool>(2);
            Container c = t != null ? t.GetComponent<Container>() : null;
            if (c == null || c.GetInventory() == null) return l;
            foreach (ItemDrop.ItemData item in c.GetInventory().GetAllItems()) { bool munin; if (RavenItem.IsRaven(item, out munin)) l.Add(munin); }
            return l;
        }

        private static bool Foreign(TombStone t, Player p) { long o = OwnerOf(t); return p != null && o != 0L && o != p.GetPlayerID(); }

        private static string[] HuginPeck { get { return Lang.Pool(HuginPeckSrc); } } // (translated on every read)
        private static readonly string[] HuginPeckSrc = Lang.Keys("Не твоє. І не твоя могила.", "Я пам'ятаю обличчя злодіїв.", "Поклади, де взяв, — і йди собі.", "Ще крок — і дзьобну в око.");
        private static string[] MuninPeck { get { return Lang.Pool(MuninPeckSrc); } } // (translated on every read)
        private static readonly string[] MuninPeckSrc = Lang.Keys("Руки геть від могили, падлюко!", "Ще раз — і ляжеш поруч!", "Це не твій мотлох, щур!", "Нишпориш по мерцях? На!");

        // From the TombStone.Interact patch (the toucher's game): someone else's grave with a raven in it — a peck
        internal static void Touched(TombStone t, Player p)
        {
            if (t == null || p == null || p != Player.m_localPlayer || p.IsDead() || !Foreign(t, p)) return;
            List<bool> ravens = RavensIn(t);
            if (ravens.Count == 0 || Time.time - _peckAt < 0.8f) return;
            _peckAt = Time.time;
            bool munin = ravens.Count == 1 ? ravens[0] : Random.value < 0.6f; // Munin, with his temper, the more often
            float dmg = p.GetMaxHealth() * PeckShare;
            p.SetHealth(p.GetHealth() - dmg); // to death, if it comes to that (the game ends a life at 0 by itself)
            if (DamageText.instance != null) DamageText.instance.ShowText(HitData.DamageModifier.Normal, p.GetTopPoint(), dmg, true);
            GraveBird b = BirdAt(t, munin);
            Transform from = b != null ? b.transform : t.transform;
            ScreamFx.CawAt(from, from.position + Vector3.up * 0.2f, munin ? 0.85f : 1.3f, 0.6f, false);
            if (b != null) b.Swoop(p);
            string[] lines = munin ? MuninPeck : HuginPeck;
            RavenMsg.Center(p, Plugin.RavenName(munin) + ": «" + lines[Random.Range(0, lines.Length)] + "»");
        }

        // the grave whose things these are, when it is someone else's (with a raven in it) and opened here
        internal static TombStone ForeignGraveOf(Inventory inv)
        {
            if (inv == null) return null;
            foreach (TombStone t in Graves)
            {
                if (t == null) continue;
                Container c = t.GetComponent<Container>();
                if (c != null && c.GetInventory() == inv) return Foreign(t, Player.m_localPlayer) ? t : null;
            }
            return null;
        }

        // your own grave (not someone else's): its container's inventory
        internal static bool OwnGrave(Inventory inv)
        {
            if (inv == null) return false;
            foreach (TombStone t in Graves)
            {
                if (t == null) continue;
                Container c = t.GetComponent<Container>();
                if (c != null && c.GetInventory() == inv) return !Foreign(t, Player.m_localPlayer);
            }
            return false;
        }

        // your own grave's ravens onto their shoulders, past a full bag (RavenSeat.FromGrave): before the game's take-all
        internal static void RavensOut(Inventory from)
        {
            Player p = Player.m_localPlayer;
            if (p == null || !OwnGrave(from)) return;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(from.GetAllItems())) RavenSeat.FromGrave(p, from, item, false);
        }

        internal static void NoTaking(bool munin)
        {
            Player p = Player.m_localPlayer;
            if (p != null) RavenMsg.Center(p, Lang.F("{0} не дасться: з чужої могили ворона не взяти.", Plugin.RavenName(munin)));
        }

        private static readonly System.Reflection.MethodInfo AddAt = AccessTools.Method(typeof(Inventory), "AddItem", new System.Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) });

        // «take all» from someone else's grave with a raven in it: the game's own MoveAll, the ravens left out
        internal static bool TakeAllBut(Inventory to, Inventory from)
        {
            if (to == null || from == null) return true;
            if (ForeignGraveOf(from) == null) { if (Player.m_localPlayer != null && to == Player.m_localPlayer.GetInventory()) RavensOut(from); return true; } // your own: ravens to the shoulders, the rest the game's way
            List<ItemDrop.ItemData> rest = new List<ItemDrop.ItemData>(), later = new List<ItemDrop.ItemData>();
            bool raven = false, munin = false;
            foreach (ItemDrop.ItemData item in from.GetAllItems())
            {
                bool m;
                if (RavenItem.IsRaven(item, out m)) { raven = true; munin = m; } else rest.Add(item);
            }
            if (!raven) return true; // nothing to guard: the game's own way
            foreach (ItemDrop.ItemData item in rest)
            {
                // into the same cell first, as the game does (its own private way: stacks join there)
                bool there = false;
                if (AddAt != null) try { there = (bool)AddAt.Invoke(to, new object[] { item, item.m_stack, item.m_gridPos.x, item.m_gridPos.y, false }); } catch { there = false; }
                if (there) from.RemoveItem(item);
                else later.Add(item);
            }
            foreach (ItemDrop.ItemData item in later) if (to.AddItem(item)) from.RemoveItem(item);
            InvRedraw.Changed(to);
            InvRedraw.Changed(from); // (a stack moved in part is saved too)
            NoTaking(munin);
            return false;
        }

        // a raven of this grave seen on it (the loyal one with you does not sit down on the same stone)
        internal static bool HasBird(TombStone t)
        {
            foreach (GraveBird b in Birds.Values) if (b != null && b.Grave == t) return true;
            return false;
        }
    }

    // One raven's picture at its grave: sitting on the stone (the perch's sitting look) or circling over it (the dome's
    // way: the motor with the shoulder's life on the bird), landing on the stone and taking off from it as the perches do.
    internal class GraveBird : MonoBehaviour, IRavenUser
    {
        internal TombStone Grave;
        internal ItemDrop.ItemData Item;
        internal string RavenId;
        internal long Owner;
        internal bool Munin;
        private int _index, _level;
        private Transform _seat;
        private ShoulderLife _life;
        private RavenMotor _motor;
        private int _state;         // 0 sits, 1 circles, 2 to the point before the stone, 3 the final way in, 4 the landing, 5 up and away
        private Vector3 _upAim;
        internal static float UpHeight = 40f, UpGone = 30f, UpSeconds = 9f;
        private float _since, _settle, _decideAt, _ring, _swoopUntil = -1f, _lostAt = -1f;
        private bool _roofed, _clip, _yielded;
        private Vector3 _landOff, _graveAt;
        private float _diagAt;
        private Quaternion _landFromRot;
        private Player _swoop;
        private const string Name = "над могилою";
        internal static float GiveUp = 25f;

        public void Yield() { _yielded = true; Destroy(this); }

        private float Size { get { return Shoulder.Scale * Shoulder.SizeFor(Mathf.Max(1, _level)); } }

        internal static GraveBird Make(TombStone t, ItemDrop.ItemData item, bool munin, long owner, string id, int index)
        {
            Transform seat = SeatOn(t, index, id);
            if (seat == null) return null;
            int level = Trust.Level(item);
            GameObject r = Perches.Sitter(seat, munin, Shoulder.Scale * Shoulder.SizeFor(Mathf.Max(1, level)), -1, null, null);
            if (r == null) return null;
            Outfits.Dress(r, Outfits.Code(item));
            RavenWear.Dress(r, munin, RavenWear.Code(item));
            GraveBird b = r.AddComponent<GraveBird>();
            b.Grave = t; b.Item = item; b.RavenId = id; b.Owner = owner; b.Munin = munin; b._index = index; b._level = level; b._seat = seat;
            b._ring = Mathf.Abs(id.GetStableHashCode() % 1000) / 100f;
            b._roofed = seat.position.y > 3000f // the dungeons lie far up in the sky of the world
                || Physics.Raycast(seat.position + Vector3.up * 0.4f, Vector3.up, 8f, RavenMotor.Mask, QueryTriggerInteraction.Ignore);
            b._graveAt = t.transform.position;
            b._decideAt = Time.time + Random.Range(0.5f, 2f); // seen sitting first; off a moment later if it is to circle
            return b;
        }

        // a spot on top of the stone (one each when two lie there), facing a way of its own. Not a part of the grave: the
        // game takes an emptied grave away at once, and the bird on it must be left to fly to the shoulder (the bird's own)
        private static Vector3 SeatPoint(TombStone t, int index)
        {
            Collider best = null;
            foreach (Collider c in t.GetComponentsInChildren<Collider>())
                if (c != null && !c.isTrigger && (best == null || c.bounds.size.sqrMagnitude > best.bounds.size.sqrMagnitude)) best = c;
            Vector3 at;
            if (best == null) at = t.transform.position + Vector3.up * 0.8f;
            else if (!SitHere.TopOf(best, out at)) at = new Vector3(best.bounds.center.x, best.bounds.max.y, best.bounds.center.z);
            return at + t.transform.right * (index == 0 ? -0.12f : 0.12f);
        }

        // the game throws a new grave up and lets it fall (and water lifts it): the seat goes with it, measured anew
        // on the stone wherever it comes to rest (a seat measured once, on a grave still in the air, left the bird
        // sitting on nothing metres over it)
        private void FollowGrave()
        {
            Vector3 now = Grave.transform.position;
            if ((now - _graveAt).sqrMagnitude < 0.03f * 0.03f) return;
            _graveAt = now;
            _seat.position = SeatPoint(Grave, _index);
            _roofed = _seat.position.y > 3000f || Physics.Raycast(_seat.position + Vector3.up * 0.4f, Vector3.up, 8f, RavenMotor.Mask, QueryTriggerInteraction.Ignore);
        }

        private static Transform SeatOn(TombStone t, int index, string id)
        {
            string name = "RavenWatch_GraveSeat" + index;
            float yaw = Mathf.Abs(id.GetStableHashCode() % 360);
            GameObject g = new GameObject(name);
            g.transform.position = SeatPoint(t, index);
            g.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.LookRotation(Vector3.ProjectOnPlane(t.transform.forward, Vector3.up).normalized + Vector3.forward * 0.001f, Vector3.up);
            return g.transform;
        }

        // ---- sit or circle

        private bool WantSit()
        {
            ItemDrop.ItemData it = Item;
            if (it == null || _roofed) return true;
            if (Trust.Level(it) <= 0 || Satiety.Zone(it) == Satiety.Starving) return true; // not woken / too weak to fly
            Norov.Quirk q = Norov.QuirkOf(it);
            if (q != null && q.Id == "chilly" && (Quirks.Raining() || Quirks.Snowing()) && !GearFx.ColdProof(it)) return true; // «Мерзляк»: rain, a storm, snow
            if (q != null && q.Id == "sleepy" && EnvMan.IsNight()) return true; // «Сонько»: asleep at night
            // the world's clock (the same in every game): one roll for each StateSeconds
            double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time;
            long bucket = (long)(now / GraveRavens.StateSeconds);
            float u = ((RavenId + ":" + bucket).GetStableHashCode() & 0x7fffffff) / (float)int.MaxValue;
            float share = GraveRavens.SitBase / Mathf.Max(0.25f, Norov.OutChance(it)) + (Satiety.Zone(it) == Satiety.Stuffed ? GraveRavens.StuffedSit : 0f);
            return u < Mathf.Clamp(share, 0.1f, 0.85f);
        }

        private void Update()
        {
            if (_seat == null) { Destroy(gameObject); return; }
            if (_state == 5) return; // on its way up: neither the grave nor the dice matter any more
            if (Grave == null) { if (_lostAt < 0f) _lostAt = Time.time; else if (Time.time - _lostAt > 10f) Perches.Puff(gameObject, Munin, "its grave gone"); return; } // (the look round hands it over first)
            FollowGrave();
            if (Time.time < _decideAt) return;
            _decideAt = Time.time + 1f;
            bool sit = WantSit();
            if (_state == 0 && !sit) TakeOff();
            else if (_state == 1 && sit && _swoopUntil < Time.time) { _state = 2; _since = Time.time; }
        }

        private Player OwnerOrMe { get { Player o = PerchView.PlayerOf(Owner); return o != null ? o : Player.m_localPlayer; } }

        // off the stone: the shoulder's life on the bird, the motor's take-off (its crouch and spring), round the ring
        private void TakeOff() { TakeOff(RingAim()); }

        private void TakeOff(Vector3 aim)
        {
            Player life = OwnerOrMe;
            if (life == null) return;
            foreach (PerchPose pp in GetComponents<PerchPose>()) DestroyImmediate(pp);
            Raven src = Shoulder.Template(Munin);
            if (src != null && src.m_visual != null) { Perches.ToTemplate(gameObject, src.m_visual); Perches.Reveal(gameObject, false); }
            transform.SetParent(null, true);
            _life = gameObject.AddComponent<ShoulderLife>();
            _life.Init(life, Munin, Mathf.Max(1, _level));
            _life.Still = true;
            AnimDirector.Take(gameObject, RavenAct.Outing, this, "over its grave");
            _motor = RavenMotor.Of(gameObject, Munin, null);
            if (_motor == null) { SitDown(); return; }
            _motor.Launch(_motor.TakeoffVelocity(aim, Vector3.zero, Name), Name, 1f);
            _state = 1; _since = Time.time;
        }

        // the ring over the stone: Hugin wide and high, Munin lower and tighter; two of them opposite ways
        private Vector3 RingAim()
        {
            float t = Time.time + _ring;
            Vector3 mid = _seat.position + Vector3.up * (Munin ? 3.2f : 3.8f);
            float r = Munin ? Mathf.Lerp(2.8f, 3.6f, Mathf.PerlinNoise(t * 0.15f, _ring)) : Mathf.Lerp(3.6f, 4.6f, Mathf.PerlinNoise(t * 0.08f, _ring));
            float h = Mathf.Sin(t * (Munin ? 0.6f : 0.4f)) * (Munin ? 0.4f : 0.6f);
            return CirclePlan.Ring(transform.position, mid, r, Munin ? 38f : 30f, _index == 0 ? 1f : -1f) + Vector3.up * h;
        }

        // a thief touched the grave: at their head for a moment, then back round
        internal void Swoop(Player p)
        {
            if (p == null) return;
            if (_state == 0) TakeOff();
            if (_state == 0 || _state == 4) return; // (touching down this moment: it stays)
            if (_state != 1 && _motor != null && _motor.Body != null) _motor.Body.NewLeg(); // it was coming in to the stone: off that way
            _swoop = p; _swoopUntil = Time.time + 1.4f; _state = 1;
        }

        private void Feathers(Vector3 at)
        {
            Raven tpl = Shoulder.Template(Munin);
            if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, at, transform.rotation, null, 0.5f, -1);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

        private void LateUpdate()
        {
            if (_state == 0 || _motor == null || _life == null) return;
            float dt = Time.deltaTime;
            if (_state == 5)
            {
                _motor.Steer(_upAim, _motor.Body != null ? _motor.Body.Style.Cruise : 5f, false, 0f, dt, Vector3.zero, 1f);
                _motor.Finish(dt, 0f);
                if (transform.position.y - _seat.position.y > UpGone) { Destroy(gameObject); return; } // a dot in the sky: gone
                if (Time.time - _since > UpSeconds) { Perches.Puff(gameObject, Munin, "flew up from its grave: that shoulder has a raven"); return; }
            }
            else if (_state == 1)
            {
                bool swooping = _swoop != null && Time.time < _swoopUntil;
                Vector3 aim = swooping ? _swoop.GetHeadPoint() : RingAim();
                _motor.Steer(aim, _motor.Body != null ? _motor.Body.Style.Cruise * (swooping ? 1.2f : Munin ? 0.7f : 0.55f) : 5f, false, swooping ? 1.5f : 0f, dt, Vector3.zero, swooping ? 0.5f : 1f);
                _motor.Finish(dt, 0f);
                if (Time.time >= _diagAt && Plugin.Instance != null) // (wings seen standing still over a grave: what its flight does)
                {
                    _diagAt = Time.time + 5f;
                    Plugin.Instance.Log("Grave raven " + Plugin.RavenName(Munin) + " circling: act " + AnimDirector.Of(gameObject) + ", body " + (_motor.Body != null) + ", " + _life.FlyState());
                }
            }
            else if (_state == 2 || _state == 3)
            {
                if (_motor.Body == null || Time.time - _since > GiveUp) { Feathers(transform.position); Feathers(_seat.position); SitDown(); return; } // never got there: a puff here and on the stone
                Vector3 seat = _seat.position, front = Flat(_seat.forward);
                if (_state == 2)
                {
                    // behind the stone on the side it will face from, a little above: then the final way in
                    Vector3 pre = seat - front * 1.6f + Vector3.up * 1.2f;
                    _motor.Steer(pre, _motor.Body.Style.Cruise, true, 0f, dt, Vector3.zero, 1f);
                    _motor.Finish(dt, 0f);
                    if (Vector3.Distance(transform.position, pre) < 1.2f || Vector3.Distance(transform.position, seat) < 2f) { _motor.Body.MarkFinal(); _state = 3; }
                }
                else
                {
                    Vector3 aim = seat + Vector3.up * FlightBody.LandAbove - front * FlightBody.LandBehind;
                    float near = Vector3.Distance(transform.position, aim);
                    _motor.Body.FaceHint(front, 1f - Mathf.Min(near, RavenFlight.TurnFrom) / RavenFlight.TurnFrom);
                    float left = _motor.Steer(aim, _motor.Body.HomeSpeed(5f, Vector3.zero, 0f), true, near < 4f ? 2f : 0f, dt, Vector3.zero, 1f);
                    _motor.Finish(dt, Mathf.Clamp01(1f - near / Mathf.Max(0.5f, _motor.Landing().Flare)));
                    // (not the landing dispatcher: that is for the owner's shoulder pair, and this bird is not on them)
                    if (_motor.Body.CanLand(left, Vector3.zero)) { _life.NextLand = _motor.Landing(); BeginLand(); }
                }
            }
            else if (_state == 4) { Land(); return; }
            if (this != null && _life != null && _state != 4) _life.Apply();
        }

        private void BeginLand()
        {
            _state = 4;
            _motor.Grounded();
            _life.VanillaFly = false; _life.Still = false;
            Quaternion seatRot = _seat.rotation * Quaternion.Euler(Shoulder.Rotation);
            _landOff = Quaternion.Inverse(seatRot) * (transform.position - _seat.position);
            _landFromRot = Quaternion.Inverse(seatRot) * transform.rotation;
            _clip = _life.BeginLand();
            _settle = Time.time;
        }

        private void Land()
        {
            Quaternion seatRot = _seat.rotation * Quaternion.Euler(Shoulder.Rotation);
            float way;
            bool still;
            if (_clip) still = _life.StepLand(out way);
            else { way = Mathf.Clamp01((Time.time - _settle) / 0.35f); still = way < 1f; way = way * way * (3f - 2f * way); }
            transform.position = _seat.position + seatRot * (_landOff * (1f - way));
            transform.rotation = Quaternion.Slerp(seatRot * _landFromRot, seatRot, Mathf.Clamp01(way * ShoulderPin.LandTurnRate));
            if (!still || Time.time - _settle > 4f) SitDown();
        }

        // on the stone: the perch's sitting look on this same bird (its flying parts go)
        private void SitDown()
        {
            AnimDirector.Release(gameObject, this);
            Perches.Seat(gameObject, _seat, Munin, Size, -1, null, null);
            _life = null; _motor = null; _state = 0; _swoopUntil = -1f;
        }

        // its owner took it out of the grave, but that shoulder already has a raven of its kind: no place for it there,
        // and the one sitting is left alone — this bird goes straight up, a little aside, and is gone in the sky
        internal void LeaveUp()
        {
            if (_state == 5) return;
            const string why = "out of its grave, that shoulder already has a raven";
            if (_roofed) { Perches.Puff(gameObject, Munin, why + " (no sky over the stone to fly up into)"); return; }
            Vector2 side = Random.insideUnitCircle * 5f;
            _upAim = _seat.position + Vector3.up * UpHeight + new Vector3(side.x, 0f, side.y);
            if (_state == 4) SitDown(); // (touching down this moment: off the stone again)
            if (_state == 0) TakeOff(_upAim); // (its spring already upwards)
            else if (_state != 1 && _motor != null && _motor.Body != null) _motor.Body.NewLeg(); // it was coming in to the stone: not that way any more
            if (_state == 0 || _motor == null || _life == null) { Perches.Puff(gameObject, Munin, why); return; }
            _swoop = null; _swoopUntil = -1f;
            _state = 5; _since = Time.time;
            Player owner = PerchView.PlayerOf(Owner);
            if (owner != null && owner == Player.m_localPlayer && Plugin.Instance != null) Plugin.Instance.Log(Plugin.RavenName(Munin) + " leaves its grave upwards: that shoulder already has a raven");
        }

        // its owner took it out of the grave: this very bird flies onto the shoulder (sitting or in the air)
        internal void HandOver()
        {
            Player owner = PerchView.PlayerOf(Owner);
            AnimDirector.Release(gameObject, this);
            transform.SetParent(null, true); // (the grave may go as soon as it is empty: not with it)
            Perches.NoteLeft(Owner, Munin, transform.position, transform.rotation, gameObject);
            if (owner != null && owner == Player.m_localPlayer && Plugin.Instance != null) Plugin.Instance.Log(Plugin.RavenName(Munin) + " leaves its grave for the shoulder");
            _yielded = true;
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (!_yielded) AnimDirector.Release(gameObject, this);
            if (_seat != null) Destroy(_seat.gameObject); // (its spot on the stone: nobody else's)
        }
    }

    [HarmonyPatch(typeof(TombStone), "Awake")]
    internal static class GraveRavensSeePatch
    {
        private static void Postfix(TombStone __instance) { try { GraveRavens.Add(__instance); } catch { } }
    }

    // someone else's grave opened here: a raven in it is never taken out (a click, a right click, a drag)
    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    internal static class GraveRavensClickPatch
    {
        private static readonly System.Reflection.FieldInfo Grid = AccessTools.Field(typeof(InventoryGui), "m_containerGrid");
        private static readonly System.Reflection.FieldInfo Current = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");
        internal static bool Blocked(InventoryGui gui, InventoryGrid grid, ItemDrop.ItemData item)
        {
            bool munin;
            if (gui == null || grid == null || item == null || !RavenItem.IsRaven(item, out munin)) return false;
            if (Grid == null || Current == null || !ReferenceEquals(Grid.GetValue(gui), grid)) return false;
            Container c = Current.GetValue(gui) as Container;
            if (c == null || GraveRavens.ForeignGraveOf(c.GetInventory()) == null) return false;
            GraveRavens.NoTaking(munin);
            return true;
        }
        // your own grave: a raven right-clicked sits straight on its shoulder when the bag is full (or it sat there at the
        // death); a left click only picks it up, as ever (dropped on a shoulder cell: SeneaLShoulders.DropRaven)
        internal static bool ToShoulder(InventoryGui gui, InventoryGrid grid, ItemDrop.ItemData item)
        {
            bool munin;
            if (gui == null || grid == null || item == null || !RavenItem.IsRaven(item, out munin)) return false;
            if (Grid == null || !ReferenceEquals(Grid.GetValue(gui), grid)) return false;
            Inventory inv = grid.GetInventory();
            return GraveRavens.OwnGrave(inv) && RavenSeat.FromGrave(Player.m_localPlayer, inv, item, false);
        }
        private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item)
        {
            try { return !Blocked(__instance, grid, item); } catch { return true; }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnRightClickItem")]
    internal static class GraveRavensRightClickPatch
    {
        private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item)
        {
            try { return !GraveRavensClickPatch.Blocked(__instance, grid, item) && !GraveRavensClickPatch.ToShoulder(__instance, grid, item); } catch { return true; }
        }
    }

    // «take all» (the button, the grave's own loot-all): the rest, but not the raven
    [HarmonyPatch(typeof(Inventory), "MoveAll")]
    internal static class GraveRavensTakeAllPatch
    {
        private static bool Prefix(Inventory __instance, Inventory fromInventory)
        {
            try { return GraveRavens.TakeAllBut(__instance, fromInventory); } catch { return true; }
        }
    }
}
#endif
