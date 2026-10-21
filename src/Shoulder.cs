#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The small ravens on the shoulders. The wearer writes which ones sit there into the player's own
    // network data; every game reads it for every player nearby and draws the ravens itself. This works for
    // the ravens' own shoulders (RavenSeat), which are no slot of the game at all.
    internal static class Shoulder
    {
        private const string Key = "RavenWatch_Shoulder"; // bit 1 Hugin, bit 2 Munin
        private const string LevelKey = "RavenWatch_ShoulderLv"; // Hugin's trust level + Munin's x16: how big each is drawn
        private const string HuginName = "RavenWatch_ShoulderHugin";
        private const string MuninName = "RavenWatch_ShoulderMunin";

        internal static string HuginBone = "RightShoulder";
        internal static string MuninBone = "LeftShoulder";
        internal static float Scale = 0.3f; // the size at the top level of trust
        internal static float SmallestShare = 0.56f; // at level 1 (and asleep): this share of it

        // A raven grows with trust: SmallestShare of the full size at level 1, the full size at the top level,
        // the same step in percent at every level in between (each ritual makes it a little bigger than the last did).
        internal static float SizeFor(int level)
        {
            float t = Mathf.Clamp01((Mathf.Max(1, level) - 1f) / Mathf.Max(1, Trust.MaxLevel - 1));
            return Mathf.Pow(Mathf.Clamp(SmallestShare, 0.1f, 1f), 1f - t);
        }
        internal static Vector3 Offset = new Vector3(0.14f, 0.05f, 0f);
        internal static Vector3 Rotation = new Vector3(0f, 180f, 0f); // the raven model faces the other way
        internal static float HuginTilt = 0f, MuninTilt = 0f; // degrees sideways, away from the head

        // Some emotes throw the shoulders about (a dance, lying down): then the ravens hover above instead (the ones
        // listed; asleep in a bed always).
        private static readonly HashSet<string> HoverEmotes = new HashSet<string>();
        internal static void SetHoverEmotes(string list)
        {
            HoverEmotes.Clear();
            foreach (string s in (list ?? "").Split(',')) { string n = s.Trim().ToLowerInvariant(); if (n.Length > 0) HoverEmotes.Add(n); }
        }

        // the wearer is in one of those emotes now (any player: the game keeps the looping emote in the player's net data)
        internal static bool Hovering(Player pl)
        {
            if (pl == null) return false;
            if (pl.IsSleeping()) return true; // lying in a bed: no shoulder to sit on
            if (pl.IsSitting() && HoverEmotes.Contains("sit")) return true; // sitting (on the ground, on a chair): only when "sit" is in the list
            if (HoverEmotes.Count == 0) return false;
            ZDO zdo = NetFlag.Zdo(pl, false);
            if (zdo == null) return false;
            string e = zdo.GetString(ZDOVars.s_emote, "");
            return e.Length > 0 && HoverEmotes.Contains(e.ToLowerInvariant());
        }

        private sealed class Pair { public GameObject Hugin, Munin; public int HuginLv = -1, MuninLv = -1; }
        private static readonly Dictionary<Player, Pair> Drawn = new Dictionary<Player, Pair>();
        private static readonly List<Animator> Landing = new List<Animator>();
        private static readonly HashSet<Animator> FlyingIn = new HashSet<Animator>(); // landing with the fly-in, not the puff
        private static float _next;
        private static int _madeAt = -1;
        private static bool _bonesLogged;
        internal static bool HuginAway; // out scouting

        internal static void Reset()
        {
            foreach (Pair pr in Drawn.Values) { Kill(pr.Hugin); Kill(pr.Munin); }
            Drawn.Clear();
            Landing.Clear();
            FlyingIn.Clear();
            HuginAway = false;
        }

        // drop every drawn raven: the next tick draws them again with the new size and place
        internal static void Redraw()
        {
            foreach (Pair pr in Drawn.Values) { Kill(pr.Hugin); Kill(pr.Munin); pr.Hugin = null; pr.Munin = null; }
            _next = 0f;
        }

        internal static string Describe()
        {
            return Lang.F("розмір {0}, зсув {1}, поворот {2}, нахил Хугін {3}°, Мунін {4}°", Scale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), V(Offset), V(Rotation), HuginTilt.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), MuninTilt.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string V(Vector3 v)
        {
            System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
            return v.x.ToString("0.##", c) + " " + v.y.ToString("0.##", c) + " " + v.z.ToString("0.##", c);
        }

        // the local wearer tells everyone which ravens sit on the shoulders
        internal static void Publish(Player p, bool hugin, bool munin, int huginLevel, int muninLevel)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null) return;
            int mask = (hugin && !HuginAway ? 1 : 0) | (munin ? 2 : 0);
            if (zdo.GetInt(Key, 0) != mask) { zdo.Set(Key, mask); _next = 0f; } // drawn at once (a raven back from a perch: no gap)
            int levels = Mathf.Clamp(huginLevel, 0, 15) | (Mathf.Clamp(muninLevel, 0, 15) << 4);
            if (zdo.GetInt(LevelKey, 0) != levels) zdo.Set(LevelKey, levels);
        }

        // Just in the world (or back from death): the game and the other mods are still loading, and a raven flying
        // in now would stutter. The ravens come this many seconds after the world is there around you (not while a
        // loading screen or a teleport is on, nor before the ground around you is loaded); the gifts wait for them.
        internal static float ArriveDelay = 10f;
        internal static float TurnFollowHugin = 4f, TurnFollowMunin = 7f; // how quickly the bird turns after you on the seat while the sitting system does not hold it (landing, 1/s)
        internal static float PerchDrop = 0.02f; // m lower on the shoulder, its inner leg bent (ShoulderLife.PerchLegs)
        private static Player _me;
        private static float _meSince;
        private static bool _ready; // the arrival wait is over for this life of your character (a portal later changes nothing)

        // A raven of this player is with them: the arrival wait is over (yours), and its fly-in has landed. One not
        // drawn at all, or flying off, has nothing to wait for. The gifts and the passives ask this.
        internal static bool Arrived(Player p, bool munin)
        {
            if (p == null) return false;
            if (p == _me && !_ready) return false;
            GameObject g = ModelOf(p, munin);
            if (g == null) return true;
            ShoulderPin pin = g.GetComponent<ShoulderPin>();
            return pin == null || !pin.Arriving;
        }

        // twice a second: draw what every player nearby has on the shoulders
        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.5f;
            Senses.Tick();          // what is going on around you, once for everyone
            Perches.Sweep();        // birds left by a perch and taken by nobody: off in a puff (also with no perch loaded)
            GraveRavens.Tick();     // ravens lying in a grave near: seen over it
            BlackBox.Ensure();      // the flight's black box (the last seconds, saved on a key)
            RavenJournal.Watch();   // a raven of yours gone from both shoulder and bag: noted
            Outing.Tick();
            Orders.Tick();          // the orders run their time (and the short scouting's marks)
            Errands.Tick();         // Odin's errand: places reached, its map marks
            RavenFetch.Visuals();   // what a raven brings, seen in its beak
            Post.Tick();            // the post: trips, letters coming, their birds
            Quirks.Tick();          // the ravens' quirks (Norov)
            GearFx.Tick();          // the things they wear (the bell's warning)
            Trust.WatchStones(Player.m_localPlayer); // what hangs on the boss stones, remembered for the book
            AnimDirector.Schedule();
            if (Time.time >= _nextMatCheck) { _nextMatCheck = Time.time + 5f; foreach (Player pl in Player.GetAllPlayers()) if (pl != null) { FixPink(ModelOf(pl, false), false); FixPink(ModelOf(pl, true), true); } }
            Player me = Player.m_localPlayer;
            if (me != _me) { _me = me; _meSince = Time.time; _ready = false; }
            if (me != null && !_ready)
            {
                // still loading (a loading screen, a teleport, the ground around not there yet): the wait starts again
                if (me.IsTeleporting() || (ZNetScene.instance != null && !ZNetScene.instance.IsAreaReady(me.transform.position))) _meSince = Time.time;
                else if (Time.time - _meSince >= ArriveDelay) _ready = true;
            }
            if (me == null || !_ready) return;
            List<Player> gone = null;
            foreach (Player pl in Drawn.Keys) if (pl == null) { if (gone == null) gone = new List<Player>(); gone.Add(pl); }
            if (gone != null) foreach (Player pl in gone) Drawn.Remove(pl);

            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null) continue;
                ZDO zdo = NetFlag.Zdo(pl, false);
                int mask = zdo != null ? zdo.GetInt(Key, 0) : 0;
                int levels = zdo != null ? zdo.GetInt(LevelKey, 0) : 0;
                Pair pr;
                if (!Drawn.TryGetValue(pl, out pr)) { if (mask == 0) continue; pr = new Pair(); Drawn[pl] = pr; }
                int hl = levels & 15, ml = (levels >> 4) & 15;
                // a new level while it stays on the shoulder (a console command): drawn again at its new size;
                // one that is leaving (after a ritual) flies off first, and comes back at the new size
                if (pr.Hugin != null && pr.HuginLv != hl && (mask & 1) != 0) { Kill(pr.Hugin); pr.Hugin = null; }
                if (pr.Munin != null && pr.MuninLv != ml && (mask & 2) != 0) { Kill(pr.Munin); pr.Munin = null; }
                pr.Hugin = Keep(pl, pr.Hugin, (mask & 1) != 0, false, hl);
                pr.Munin = Keep(pl, pr.Munin, (mask & 2) != 0, true, ml);
                pr.HuginLv = hl; pr.MuninLv = ml;
                if (zdo != null) // what each raven wears (Outfits)
                {
                    long t0 = Slow.Start();
                    Outfits.Dress(pr.Hugin, zdo.GetString(Outfits.KeyH, ""));
                    Outfits.Dress(pr.Munin, zdo.GetString(Outfits.KeyM, ""));
                    Slow.End(t0, "raven outfits");
                    RavenWear.Dress(pr.Hugin, false, zdo.GetString(RavenWear.KeyH, "")); // and what its runes give it
                    RavenWear.Dress(pr.Munin, true, zdo.GetString(RavenWear.KeyM, ""));
                }
            }

            for (int i = Landing.Count - 1; i >= 0; i--)
            {
                Animator a = Landing[i];
                if (a == null) { Landing.RemoveAt(i); FlyingIn.Remove(a); continue; }
                if (a.isInitialized && (a.GetCurrentAnimatorStateInfo(0).IsTag("away") || a.GetNextAnimatorStateInfo(0).IsTag("away"))) a.SetTrigger(FlyingIn.Contains(a) ? "flyin" : "teleportin");
                else { Landing.RemoveAt(i); FlyingIn.Remove(a); }
            }
        }

        private static GameObject Keep(Player pl, GameObject current, bool want, bool munin, int level)
        {
            if (!want) { FlyOff(current, munin); return null; }
            if (current != null) return current;
            if (_madeAt == Time.frameCount) return null; // one raven made in this tick already: this one the next (half a second on)
            _madeAt = Time.frameCount;
            long t0 = Slow.Start();
            GameObject made = Make(pl, munin, level);
            Slow.End(t0, (munin ? "Munin" : "Hugin") + " drawn on the shoulder");
            return made;
        }

        private static void Kill(GameObject g) { if (g != null) Object.Destroy(g); }

        // the landing panel: this raven made again at once, so it flies in as on entering the world
        internal static bool Respawn(Player pl, bool munin)
        {
            Pair pr;
            if (pl == null || !Drawn.TryGetValue(pl, out pr)) return false;
            GameObject g = munin ? pr.Munin : pr.Hugin;
            if (g == null) return false;
            Kill(g);
            if (munin) pr.Munin = null; else pr.Hugin = null;
            _next = 0f;
            return true;
        }

        // Leaving the shoulder (off to scout, or taken off the belt): the raven lets go of the bone, flaps up and
        // away like the game's own ravens ("flyaway"), and a moment later vanishes in a puff of feathers.
        private static void FlyOff(GameObject g, bool munin)
        {
            if (g == null) return;
            if (g.GetComponent<ShoulderFlyOff>() != null || g.GetComponent<PerchFlight>() != null) return; // already on its way
            ShoulderPin pin = g.GetComponent<ShoulderPin>();
            ShoulderLife life = g.GetComponent<ShoulderLife>();
            Animator anim = g.GetComponentInChildren<Animator>();
            if (anim != null) Landing.Remove(anim);
            if (PostBird.TakeOff(g, pin, life, munin)) return; // off with the post (Post): its own flight
            if (Perches.FlyTo(g, pin, life, munin)) return; // off to sit on a perch (Perches): its own flight there
            ShoulderFlyOff fly = g.AddComponent<ShoulderFlyOff>();
            fly.Pin = pin; fly.Life = life; fly.Anim = anim;
            fly.Owner = pin != null ? pin.Player : null;
            fly.Side = pin != null && pin.Player != null ? pin.Player.transform.right * (munin ? -1f : 1f) : Vector3.zero;
            fly.Fwd = pin != null && pin.Player != null ? Vector3.ProjectOnPlane(pin.Player.transform.forward, Vector3.up).normalized : Vector3.zero;
            fly.Munin = munin;
            fly.Motor = RavenMotor.Of(g, munin, pin != null ? pin.Player : null);
            bool inAir = fly.Motor != null && fly.Motor.Airborne;
            fly.Wait = inAir ? 0f : ShoulderFlyOff.Delay;
            AnimDirector.Take(g, RavenAct.FlyOff, fly, "leaves the shoulder");
            if (inAir && pin != null) pin.enabled = false; // already flying: it goes on from where it is, not back onto the seat first
        }

        // the raven really sits on the shoulder, and has for a few seconds: not out on a strike or the dome, not on an outing,
        // not hovering, not flying back or landing (what the ravens say together waits for that)
        internal static bool Settled(Player pl, bool munin, float seconds)
        {
            GameObject m = ModelOf(pl, munin);
            if (m == null || !m.activeInHierarchy) return false;
            ShoulderPin pin = m.GetComponent<ShoulderPin>();
            if (pin == null || !pin.enabled || pin.InAir || pin.Landing) return false;
            if (AnimDirector.Of(m) != RavenAct.Sit) return false;
            return Time.time - pin.CalmSince >= seconds;
        }

        // Free for a new task right now: on the shoulder, or in the air on something it drops at once (an order, an
        // outing, the dance, flying home); not while it is still flying in to you or on a strike, the dome, away.
        internal static bool Free(Player pl, bool munin)
        {
            GameObject m = ModelOf(pl, munin);
            if (m == null || !m.activeInHierarchy) return false;
            ShoulderPin pin = m.GetComponent<ShoulderPin>();
            if (pin == null || !pin.enabled || pin.Arriving) return false;
            RavenAct act = AnimDirector.Of(m);
            return act != RavenAct.Arrive && act <= RavenAct.Command;
        }

        // still with you: on the shoulder, landing on it, or hovering over it (not off on a strike, the dome, an outing, away)
        internal static bool Present(Player pl, bool munin)
        {
            GameObject m = ModelOf(pl, munin);
            if (m == null || !m.activeInHierarchy) return false;
            ShoulderPin pin = m.GetComponent<ShoulderPin>();
            if (pin == null || !pin.enabled) return false;
            RavenAct a = AnimDirector.Of(m);
            return a == RavenAct.Sit || a == RavenAct.Land || a == RavenAct.Hover;
        }

        // What it wears (things, runes) changes only while the raven really sits on your shoulder: not away at Odin's,
        // on a perch, out on a strike, an order, scouting, the dome or an outing, nor while it is still flying in.
        // null when it does; else why not, to be said.
        internal static string NotOnShoulder(Player p, bool munin)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            string name = Plugin.RavenName(munin);
            if (p == null || item == null) return Lang.F("{0} не сидить у тебе на плечі.", name);
            if (Perches.OnSpot(item)) return Lang.F("{0} сидить не на тобі. Поклич його на плече, тоді й перевдягнеш.", name);
            if (Perches.On(item)) return Lang.F("{0} сидить на жердині. Поклич його на плече, тоді й перевдягнеш.", name);
            if (Trust.Away(item)) return Lang.F("{0} зараз далеко. Речі й руни — коли повернеться на плече.", name);
            string off;
            if (RavenSkills.OffShoulder(p, munin, out off)) return Lang.F("{0} зараз не на плечі ({1}). Речі й руни — коли сяде.", name, off);
            if (!Present(p, munin)) return Lang.F("{0} зараз не на плечі. Речі й руни — коли сяде.", name);
            return null;
        }

        // the trust level this player's raven is drawn with (anyone's, as published), 0 when none
        internal static int LevelOn(Player pl, bool munin)
        {
            ZDO zdo = NetFlag.Zdo(pl, false);
            if (zdo == null) return 0;
            int levels = zdo.GetInt(LevelKey, 0);
            return munin ? (levels >> 4) & 15 : levels & 15;
        }

        // the raven drawn on this player's shoulder (anyone's), or null while it flies off
        internal static GameObject ModelOf(Player pl, bool munin)
        {
            Pair pr;
            if (pl == null || !Drawn.TryGetValue(pl, out pr)) return null;
            GameObject g = munin ? pr.Munin : pr.Hugin;
            return g != null && g.GetComponent<ShoulderFlyOff>() == null ? g : null;
        }

        // where this player's raven is now, to hang its words on: the shoulder model itself, wherever it flies
        // (the dome and the strikes borrow it, it hovers in emotes, it flies off), else a strike's stand-in; null when
        // it is not to be seen (scouting, away at Odin's)
        internal static Transform SpeakerOf(Player pl, bool munin)
        {
            Pair pr;
            GameObject g = null;
            if (pl != null && Drawn.TryGetValue(pl, out pr)) g = munin ? pr.Munin : pr.Hugin;
            if (g != null && g.activeInHierarchy)
            {
                // sitting (or hovering) on the shoulder: the words hang over the player, still; only a raven really in the air
                // (the dome, a strike, flying off) carries them along, or they would twitch with every turn of its head
                ShoulderPin pin = g.GetComponent<ShoulderPin>();
                if (pin != null && pin.enabled && !pin.OutAway && g.GetComponent<ShoulderFlyOff>() == null) return null; // (out on an order or an outing: over the bird)
                return g.transform;
            }
            GameObject f = StrikeFlight.FlyerOf(pl, munin);
            return f != null && f.activeInHierarchy ? f.transform : null;
        }

        // the raven drawn on your own shoulder right now (landed, in a pose you can see), or null
        internal static GameObject LocalModel(bool munin)
        {
            Pair pr;
            Player me = Player.m_localPlayer;
            if (me == null || !Drawn.TryGetValue(me, out pr)) return null;
            return munin ? pr.Munin : pr.Hugin;
        }

        // your own raven's life: woken for a moment, or startled by a blow
        internal static ShoulderLife LocalLife(bool munin)
        {
            GameObject g = LocalModel(munin);
            return g != null ? g.GetComponent<ShoulderLife>() : null;
        }

        // the life of the raven drawn on any player's shoulder (a gift's wing pose seen by everyone), or null
        internal static ShoulderLife LifeOf(Player pl, bool munin)
        {
            Pair pr;
            if (pl == null || !Drawn.TryGetValue(pl, out pr)) return null;
            GameObject g = munin ? pr.Munin : pr.Hugin;
            return g != null ? g.GetComponent<ShoulderLife>() : null;
        }

        internal static Animator LocalAnimator(bool munin)
        {
            Pair pr;
            Player me = Player.m_localPlayer;
            if (me == null || !Drawn.TryGetValue(me, out pr)) return null;
            GameObject g = munin ? pr.Munin : pr.Hugin;
            return g != null ? g.GetComponentInChildren<Animator>() : null;
        }

        private static GameObject Make(Player pl, bool munin, int level)
        {
            Raven source = Template(munin);
            if (source == null || source.m_visual == null) return null;
            Transform bone = Bone(pl, munin);
            if (bone == null) return null;

            // back from a perch: the very bird from there (or turned back on its way) comes on; a new one only when
            // there is none in this game (first seen, out of the bag, entering the world, a far perch not drawn here)
            Vector3 perch, perchVel; Quaternion perchRot; GameObject reuse;
            bool fromPerch = Perches.TakeFrom(pl, munin, out perch, out perchRot, out perchVel, out reuse);
            GameObject go;
            bool inAir = false;
            if (reuse != null)
            {
                go = reuse;
                foreach (PerchFlight f in go.GetComponents<PerchFlight>()) Object.DestroyImmediate(f);
                foreach (PerchLife l in go.GetComponents<PerchLife>()) Object.DestroyImmediate(l);
                foreach (PerchPose pp in go.GetComponents<PerchPose>()) Object.DestroyImmediate(pp);
                foreach (ShoulderPin p in go.GetComponents<ShoulderPin>()) Object.DestroyImmediate(p);
                foreach (PostBird pb in go.GetComponents<PostBird>()) Object.DestroyImmediate(pb); // back with the post: the shoulder flies it from here
                inAir = go.GetComponent<ShoulderLife>() != null; // turned back on its way to the perch: still flying
                if (!inAir) Perches.ToTemplate(go, source.m_visual); // off the perch: its legs' pose and lean off its bones, as a new copy
                go.transform.SetParent(bone, false);
            }
            else go = Object.Instantiate(source.m_visual, bone, false);
            go.name = munin ? MuninName : HuginName;
            go.SetActive(true);
            AppearMute.Quiet(go); // (the game's appearing only on its fly-in, let through there)
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            Shared.RemoveExclamation(source, go);

            // Size, place and turn are set in the world, from the real raven and the character's facing:
            // a skeleton bone has its own scale and arbitrarily turned axes, so its local space is useless here.
            Vector3 want = source.m_visual.transform.lossyScale * (Scale * SizeFor(level));
            Vector3 parent = go.transform.parent.lossyScale;
            go.transform.localScale = new Vector3(
                want.x / Mathf.Max(0.0001f, Mathf.Abs(parent.x)),
                want.y / Mathf.Max(0.0001f, Mathf.Abs(parent.y)),
                want.z / Mathf.Max(0.0001f, Mathf.Abs(parent.z)));
            // The bone sways and turns with every animation, so the raven is placed again each frame after it:
            // it follows the shoulder but keeps upright against the body, whatever pose the character had when it appeared.
            ShoulderPin pin = go.AddComponent<ShoulderPin>();
            pin.Player = pl; pin.Bone = bone; pin.Munin = munin;
            if (!fromPerch) { pin.Arrive(); PuffNote(pl, munin, "appears with the game's fly-in (no perch to come from: " + Perches.NoPerchWhy(pl, munin) + ")"); }
            pin.LateUpdate();

            if (!fromPerch) Perch(go.GetComponentInChildren<Animator>(), true); // it flies in and lands on the shoulder, as the game's ravens fly in to you
            ShoulderLife life = go.GetComponent<ShoulderLife>();
            if (life == null) life = go.AddComponent<ShoulderLife>();
            life.Init(pl, munin, level);
            pin.Life = life;
            if (fromPerch) pin.ArriveFrom(perch, perchRot, perchVel, reuse != null, inAir); // seen where the perch's bird was, and off at once by the motor
            return go;
        }

        // a raven of yours comes or goes in the game's puff of feathers: one line in the log, why (rare, kept for good)
        internal static void PuffNote(Player pl, bool munin, string why)
        {
            if (pl == null || !ReferenceEquals(pl, Player.m_localPlayer) || Plugin.Instance == null) return;
            Plugin.Instance.Log("Raven puff: " + Plugin.RavenName(munin) + " " + why);
        }

        // a copied raven model starts "away" (hidden): wake its animator and land it in place
        internal static void Perch(Animator anim) { Perch(anim, false); }

        internal static void Perch(Animator anim, bool flyIn)
        {
            if (anim == null) return;
            anim.enabled = true;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            anim.Rebind();
            anim.Update(0f);
            anim.SetTrigger(flyIn ? "flyin" : "teleportin");
            Landing.Add(anim);
            if (flyIn) FlyingIn.Add(anim);
        }

        // the game's own Hugin / Munin, looked up among everything loaded once (a costly search: every caw and effect asks)
        private static Raven _hugin, _munin;
        // The game's own raven to copy for the shoulder. Not just the first raven found: other mods bring ravens of their
        // own, and live ones may wear a tint (the Guide's crimson); a raven drawn with a shader this graphics card cannot
        // run comes out pink. So: the game's own asset (not a live copy), with every shader working here, Munin or Hugin
        // as asked; the choice and its materials go to the log once.
        // A shoulder raven wearing a material whose shader this card cannot run is drawn pink: it gets the game's own
        // material back (by renderer name, from the template), and the log says which material it was (its name tells
        // who made it: "_RavenEyes" the eye glow, "(shadow)", "(guide)", "(fate)", "(perch)"), once each.
        private static float _nextMatCheck;
        private static readonly HashSet<string> PinkTold = new HashSet<string>();
        private static void FixPink(GameObject go, bool munin)
        {
            if (go == null) return;
            Raven src = Template(munin);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Material[] mats = r.sharedMaterials;
                bool bad = false;
                foreach (Material m in mats) if (m != null && (m.shader == null || !m.shader.isSupported || m.shader.name == "Hidden/InternalErrorShader")) { bad = true; if (PinkTold.Add(m.name) && Plugin.Instance != null) Plugin.Instance.LogError("Shoulder " + (munin ? "Munin" : "Hugin") + ": material " + m.name + " has a shader this graphics card cannot run (" + (m.shader != null ? m.shader.name : "none") + "), the game's own goes back on. Template: " + (src != null ? Materials(src.m_visual) : "none")); }
                if (!bad || src == null) continue;
                foreach (Renderer o in src.m_visual.GetComponentsInChildren<Renderer>(true))
                    if (o.name == r.name && ShadersWork(o.gameObject)) { r.sharedMaterials = o.sharedMaterials; break; }
            }
        }

        internal static Raven Template(bool munin)
        {
            Raven known = munin ? _munin : _hugin;
            if (known != null && known.m_visual != null) return known;
            // other mods carry their own copies of the ravens (a Jotunn bundle with its mock shaders never swapped
            // back, a copy on plain Standard): their smoke and feather puffs draw as flat squares. The game's own one
            // wins: its model and its vanishing puff on the game's shaders
            Raven best = null;
            int bestScore = -1, seen = 0;
            foreach (Raven r in Resources.FindObjectsOfTypeAll<Raven>())
            {
                if (r == null || r.m_visual == null) continue;
                if (r.m_isMunin == munin) seen++;
                bool works = ShadersWork(r.m_visual) && EffectsWork(r.m_despawnEffect);
                int score = (r.m_isMunin == munin ? 32 : 0) + (works ? 16 : 0) + (GameShaders(r.m_visual) ? 8 : 0) + (!r.gameObject.scene.IsValid() ? 2 : 0);
                if (score > bestScore) { best = r; bestScore = score; }
            }
            if (best != null && best.m_isMunin == munin)
            {
                if (munin) _munin = best; else _hugin = best;
                if (Plugin.Instance != null) Plugin.Instance.Log("Shoulder " + (munin ? "Munin" : "Hugin") + " copied from " + best.name + (best.gameObject.scene.IsValid() ? " (live, scene " + best.gameObject.scene.name + ")" : " (asset)") + ", " + seen + " found, score " + bestScore + ": " + Materials(best.m_visual));
            }
            return best;
        }

        // every material of a model has a shader that runs on this card (a Jotunn mock is a stand-in, not a shader)
        internal static bool ShadersWork(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && !ShaderWorks(m.shader)) return false;
            return true;
        }

        private static bool ShaderWorks(Shader s)
        {
            return s != null && s.isSupported && s.name != "Hidden/InternalErrorShader" && !s.name.StartsWith("JVLmock_");
        }

        // the puffs of an effect list draw with working shaders
        private static bool EffectsWork(EffectList list)
        {
            if (list == null || list.m_effectPrefabs == null) return true;
            foreach (EffectList.EffectData d in list.m_effectPrefabs)
                if (d != null && d.m_prefab != null && !ShadersWork(d.m_prefab)) return false;
            return true;
        }

        // the model is on the game's own shaders (Custom/..., the Lux particles), as the game's ravens are
        private static bool GameShaders(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && (m.shader == null || !(m.shader.name.StartsWith("Custom/") || m.shader.name.StartsWith("Lux Lit Particles")))) return false;
            return true;
        }

        internal static string Materials(GameObject go)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null) sb.Append(r.name).Append(" / ").Append(m.name).Append(" [").Append(m.shader != null ? m.shader.name + (m.shader.isSupported ? "" : ", NOT SUPPORTED") : "no shader").Append("] ");
            return sb.ToString();
        }

        private static Transform Bone(Player pl, bool munin)
        {
            foreach (string name in new string[] { munin ? MuninBone : HuginBone, munin ? "LeftShoulder" : "RightShoulder", "Spine2", "Spine1" })
            {
                if (string.IsNullOrEmpty(name)) continue;
                Transform t = Utils.FindChild(pl.transform, name, Utils.IterativeSearchType.DepthFirst);
                if (t != null) return t;
            }
            if (!_bonesLogged && Plugin.Instance != null)
            {
                _bonesLogged = true;
                System.Text.StringBuilder sb = new System.Text.StringBuilder("Raven shoulder: bone not found. Bones:");
                foreach (Transform t in pl.GetComponentsInChildren<Transform>(true)) sb.Append(' ').Append(t.name);
                Plugin.Instance.Log(sb.ToString());
            }
            return null;
        }
    }

    // Leaving: a short moment on the shoulder with the wings opening, then a jump up and away, beating the wings,
    // and a puff of feathers once out of sight.
    internal class ShoulderFlyOff : MonoBehaviour
    {
        internal static float Delay = 0.05f; // (the motor's take-off crouches and springs off by itself: RavenMotor.BeginTakeoff)
        internal ShoulderPin Pin;
        internal ShoulderLife Life;
        internal Animator Anim;
        internal Vector3 Side;
        internal Vector3 Fwd; // where you look: the raven leaves that way
        internal bool Munin;
        internal Player Owner; // whose shoulder it left (a perch it was sent to is looked up by them)
        private float _perchLook;
        private Vector3 _dir = Vector3.up;
        private bool _off;
        private const float Flight = 1.5f;
        private float _t;
        private Vector3 _goal;
        internal RavenMotor Motor;   // the raven's motor: a jump, hard beats, speeding up and climbing away (or on, if already flying)
        internal float Wait = Delay; // 0 when it was already in the air
        private ParticleSystem _trail; // the feathers it sheds as it goes

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t < Wait) return; // still held on the shoulder by its pin, wings opening
            if (!_off)
            {
                _off = true;
                if (Pin != null) { Pin.enabled = false; Object.Destroy(Pin); }
                transform.SetParent(null, true);
                // no game "flyaway" animation: its own flapping added to ours made the wings jerk back and forth
                // forward and up, where it was looking; a little to its own side so the two do not collide
                _dir = (Fwd * 1.5f + Vector3.up * 0.9f + Side * 0.25f).normalized;
                if (Motor == null) Motor = RavenMotor.Of(gameObject, Munin, null);
                Motor.Launch(_dir * 2f + Vector3.up * 1.5f, "відлітає", 1f);
                if (Life != null) { Life.VanillaFly = true; Life.Still = true; }
                _goal = transform.position + _dir * 40f + Vector3.up * 8f;
                _trail = Feathers.Trail(transform, Munin);
            }
            float ft = _t - Wait;
            // the perch's news came after the player's here: it turns to the perch it was sent to (Perches)
            if (Owner != null && Time.time >= _perchLook)
            {
                _perchLook = Time.time + 0.25f;
                if (Perches.Redirect(this, Owner, Munin, Life, Motor)) { Destroy(this); return; }
            }
            Motor.Steer(_goal, Motor.Body.Style.Top, false, 0f, Time.deltaTime, Vector3.zero, 1f);
            Motor.Finish(Time.deltaTime, 0f);
            if (ft < Flight) return;
            Raven template = Shoulder.Template(Munin);
            if (template != null && template.m_despawnEffect != null) RavenFx.Own(template.m_despawnEffect, transform.position, transform.rotation, null, 0.6f, -1);
            Shoulder.PuffNote(Owner, Munin, "flew off the shoulder and vanished");
            Destroy(gameObject);
        }

        private void OnDestroy() { Feathers.EndTrail(_trail); _trail = null; } // the last feathers stay where it vanished and fade

        // off the shoulder the pin no longer turns the bones: the wings keep beating from here
        private void LateUpdate() { if (_off && Life != null) Life.Apply(); }
    }

    // In flight (the dome, a strike) the shoulder's raven must not hang on a bone of the body: the game turns the
    // body's bones again late in the frame (head, feet on the ground...), and it would move with them. It lets go for
    // the flight and is hung back on its bone at the end, its size kept.
    internal sealed class Unhooked
    {
        private readonly Transform _t, _parent;
        private readonly Vector3 _scale;
        private Unhooked(Transform t) { _t = t; _parent = t.parent; _scale = t.localScale; }

        internal static Unhooked From(Transform t)
        {
            if (t == null || t.parent == null) return null;
            Unhooked u = new Unhooked(t);
            t.SetParent(null, true);
            return u;
        }

        internal void Back()
        {
            if (_t == null || _parent == null) return;
            // where it is in the world stays: SetParent(.., false) took its world place as a local one, and for a
            // frame (until the pin placed it) the bird stood far away and turned (the flicker at every landing)
            Vector3 p = _t.position; Quaternion r = _t.rotation;
            _t.SetParent(_parent, false);
            _t.localScale = _scale;
            _t.position = p; _t.rotation = r;
        }
    }

    internal class ShoulderPin : MonoBehaviour, IRavenUser
    {
        // flying in after you appeared, until it sits (the fly-in, then its landing)
        private bool _arrival;
        internal bool Arriving
        {
            get
            {
                if (_arrival && !_arriveWaiting && _arriveStart < 0f && !_landOn && _airMode != 1) _arrival = false;
                return _arrival;
            }
        }

        // a stronger act (a strike, the dome) takes the bird out of the pin's hands
        public void Yield()
        {
            if (_airMode != 0 && Life != null) Life.Hovering = false;
            // taken during the game's fly-in arc: its clip stops midway; after the next landing it goes to its end (no second landing)
            if (_arriveStart >= 0f && !_arriveWaiting && Life != null)
            {
                Animator a = _arriveAnim != null ? _arriveAnim : GetComponentInChildren<Animator>();
                if (a != null && a.enabled && a.isInitialized) Life.PendingState = a.GetCurrentAnimatorStateInfo(0).fullPathHash;
            }
            _airMode = 0; _landOn = false; _arriveStart = -1f; _arrival = false;
            if (_arriveWaiting) { _arriveWaiting = false; ArriveHide(false); } // it was waiting, hidden, to fly in: seen again
        }

        internal Player Player;
        internal Transform Bone;
        internal bool Munin;
        internal ShoulderLife Life; // turns the head once the raven is placed

        // It does not just appear on the shoulder: it shows up in the air behind you with a puff of feathers and flies
        // down to it. The first moment is the game's own fly-in (its animator wakes the model out of hiding: the old
        // attempts that skipped it flew an unseen bird); then the motor takes it at that speed (FlyIn 1): a dive to the
        // shoulder, braking smoothly to a hover over it, turning your way, and only then the game's landing.
        internal static float FlyInSeconds = 1.6f;   // the game arc's pace (its first part is flown); 0: no fly-in
        internal static float FlyInUp = 5f, FlyInBack = 4f, FlyInSide = 3f; // where the fly-in starts (it is seen coming)
        internal static float ArriveWake = 0.3f;     // s of the game's fly-in before the motor takes it (longer if still hidden)
        internal static float ArriveWakeMax = 1f;    // s at most
        internal static float FlyInSpeed = 7f;       // m/s at least on its way down to you (more if you run)
        private Vector3 _arcPrev;
        private float _arriveStart = -1f;
        private static readonly System.Collections.Generic.List<Renderer> Pieces = new System.Collections.Generic.List<Renderer>();

        private Vector3 _from;

        // The new flight hands the bird over about a metre off: the game's own landing is played (ShoulderLife),
        // the way left kept in the shoulder's own frame, so it lands on the shoulder even if you run.
        internal void LandFrom(Vector3 from, Quaternion rot) { LandFrom(from, rot, -1f); }

        // back from a flight that ran out of time far off (stuck by a cliff, lost): not the landing clip swept across
        // the whole way in a moment, but gone there in a puff and flown in again, as after a portal; near: the landing
        internal static float LandFar = 4f;
        internal void ComeBack(Vector3 from, Quaternion rot)
        {
            // near and in sight: the landing; a wall between (stuck behind it, a safety ran out): never dragged through it
            if (Bone == null || FlyInSeconds <= 0f || (Vector3.Distance(from, Bone.position) <= LandFar && !RavenMotor.Walled(from, Bone.position))) { LandFrom(from, rot); return; }
            Raven tpl = Shoulder.Template(Munin);
            if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, from, Quaternion.identity, null, 0.6f, -1);
            Shoulder.PuffNote(Player, Munin, "came back " + Mathf.RoundToInt(Vector3.Distance(from, Bone.position)) + " m off the shoulder: gone in a puff, flies in again");
            if (Life != null && Life.Motor != null) Life.Motor.Grounded();
            Arrive();
        }

        internal void LandFrom(Vector3 from, Quaternion rot, float clipAt)
        {
            AnimDirector.Take(gameObject, RavenAct.Land, this, "landing");
            if (Life != null) Motor.Grounded(); // it sits down: the next flight starts afresh
            ShoulderFit.Again(Player); // the armour under it, measured now: the landing aims at it
            if (Life == null || !Life.BeginLand(clipAt)) { SettleFrom(from, rot, 0.3f); if (Life != null) Life.SettleLater(); return; }
            _landFrom = from; _landRot = rot; _landFresh = true; _landOn = true;
            _settleStart = -1f; _arriveStart = -1f;
        }
        private bool _landOn, _landFresh;
        internal Vector3 SeatNow;
        internal bool InAir { get { return _airMode != 0 || _arriveStart >= 0f; } }
        internal bool OutAway { get { return _airMode == 3 || _airMode == 4; } } // out on an outing or an order (not hanging over the shoulder)
        internal bool Landing { get { return _landOn; } }
        internal float CalmSince; // since when it has sat still on the shoulder (not in the air, not landing, not taken away)
        private void OnEnable() { CalmSince = Time.time; } // back from a strike or the dome: it only now sits again
        internal static float LandTurnRate = 1.4f; // how soon in the landing it is turned as it sits (× the way done)
        // hand-tuned nudges over the landing (LandPanel): at 0, 25, 50, 75, 100 % of the clip part, metres in the
        // owner's frame (x right, y up, z forward), blended between
        internal static readonly Vector3[] LandFixes = new Vector3[5];
        internal static Vector3 LandFix(float u)
        {
            float x = Mathf.Clamp01(u) * 4f;
            int a = Mathf.Min((int)x, 4), b = Mathf.Min(a + 1, 4);
            Vector3 v = Vector3.Lerp(LandFixes[a], LandFixes[b], x - a);
            return v;
        }
        private Vector3 _landFrom, _landOff;
        private Quaternion _landRot, _landTurn;

        // the new flight lands it already beside the shoulder: from there onto it, its turn eased, no arc
        internal void SettleFrom(Vector3 from, Quaternion rot, float seconds)
        {
            _settleFrom = from; _settleRot = rot; _settleFresh = true;
            _settleStart = Time.time; _settleLen = Mathf.Max(0.05f, seconds);
            _arriveStart = -1f;
        }
        private Vector3 _settleFrom, _settleOff;
        private Quaternion _settleRot;
        private float _settleStart = -1f, _settleLen;
        private bool _settleFresh;

        internal void Arrive()
        {
            if (Player == null || FlyInSeconds <= 0f) return;
            _arrival = true;
            Vector3 fwd = Vector3.ProjectOnPlane(Player.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd) * (Munin ? -1f : 1f);
            // each its own way in: not two robots side by side (a delay, where it shows up, how high and how long)
            _from = Player.transform.position + Vector3.up * (FlyInUp + Random.Range(-1f, 1.5f)) - fwd * (FlyInBack + Random.Range(-1f, 1.5f)) + side * (FlyInSide + Random.Range(-1f, 1.5f));
            // under a roof, by a cliff, in a cave: it shows up this side of it, not inside the rock (the arc's first part flies through anything)
            Vector3 eye = Player.GetEyePoint(), way = _from - eye;
            RaycastHit hit;
            if (way.sqrMagnitude > 0.01f && Physics.SphereCast(eye, 0.2f, way.normalized, out hit, way.magnitude, RavenMotor.Mask, QueryTriggerInteraction.Ignore))
                _from = eye + way.normalized * Mathf.Max(0.8f, hit.distance - 0.4f);
            _arriveLen = FlyInSeconds * Random.Range(0.85f, 1.2f);
            _arriveArc = Random.Range(0.35f, 1f);
            _arriveStart = Time.time + (Munin ? Random.Range(0.4f, 1.2f) : Random.Range(0f, 0.4f));
            _arriveWaiting = true;
            ArriveHide(true);
            AnimDirector.Take(gameObject, RavenAct.Land, this, "fly-in");
        }

        // Back from a perch (Perches): not the game's fly-in (a hidden bird that appears near you with its smoke), but
        // the bird the perch had, seen exactly there, off at once by the motor. Its animator is first brought out of the
        // copy's hidden "away" state into its idle (teleportin, run through at once), so it is visible and lands right.
        // same: the perch's own bird (no appearing to hide at all); inAir: it was turned back on its way there and
        // flies on as it was (its life, motor and wings go on, nothing restarts)
        internal void ArriveFrom(Vector3 perch, Quaternion rot, Vector3 vel, bool same, bool inAir)
        {
            if (Player == null || Life == null) { Arrive(); return; }
            _arrival = true;
            if (inAir)
            {
                bool moving = vel.sqrMagnitude > 0.25f;
                ArriveFlight(perch, rot, moving ? vel : Vector3.up * 2f, !moving);
                return;
            }
            int sidle = ShoulderLife.LearnIdle(Munin); // (not sat on a shoulder yet this session: learned now, unseen)
            if (sidle == 0) sidle = ShoulderLife.KnownIdle[Munin ? 0 : 1];
            Animator sa = same ? GetComponentInChildren<Animator>(true) : null;
            if (sa != null && sidle != 0) // (its idle not known yet: below, the way a new model comes out of its start)
            {
                // the sitting bird: its animator was off on the perch; on again straight in its idle, then up
                sa.enabled = true; sa.applyRootMotion = false;
                bool ev = sa.fireEvents;
                sa.fireEvents = false;
                sa.Play(sidle, 0, 0f);
                sa.Update(0f);
                sa.fireEvents = ev;
                Perches.Reveal(gameObject, false); // its parts as they were on the perch, whatever the animator's first state
                Vector3 sf = Vector3.ProjectOnPlane((rot * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation))) * Vector3.forward, Vector3.up).normalized;
                ArriveFlight(perch, rot, sf * 1.2f + Vector3.up * 2.2f, true);
                return;
            }
            // a new model (the perch's bird is not in this game), or the perch's before any raven idled: out of its
            // hidden start without its appearing
            Animator a = GetComponentInChildren<Animator>();
            if (a != null)
            {
                a.enabled = true; a.applyRootMotion = false;
                a.fireEvents = false; // the clips' own events (a voice and a puff of feathers as the game's raven appears): not now
                _quietAnim = a;
                a.Rebind(); a.Update(0f);
                int idle = ShoulderLife.LearnIdle(Munin);
                if (idle == 0) idle = ShoulderLife.KnownIdle[Munin ? 0 : 1];
                if (idle != 0) { a.Play(idle, 0, 0f); a.Update(0f); Perches.Reveal(gameObject, false); } // straight into its idle: no appearing (shown by hand)
                else { a.SetTrigger("teleportin"); for (int i = 0; i < 6; i++) a.Update(1f); } // never seen sitting yet: through the appearing, muted below
            }
            // the game's fly-in also lights the model's own smoke (odinsmoke): not for a bird that only hops over
            _quietSmoke = new System.Collections.Generic.List<ParticleSystem>();
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
                if (AppearMute.OwnSmoke(ps)) _quietSmoke.Add(ps); // the model's own (smoke, the feather puff), not ours (aura) nor a worn piece's
            _quietSmokeUntil = Time.time + 2.5f; // the take-off only: its own landing puff and voice later are its own
            _quietAudio = GetComponentsInChildren<AudioSource>(true);
            QuietSmoke();
            // off the perch the way it faces, forward and up; the turn to you comes in the air (the motor's bank)
            Vector3 facing = Vector3.ProjectOnPlane((rot * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation))) * Vector3.forward, Vector3.up).normalized;
            ArriveFlight(perch, rot, facing * 1.2f + Vector3.up * 2.2f, true);
        }
        private Animator _quietAnim;
        private AudioSource[] _quietAudio;
        private System.Collections.Generic.List<ParticleSystem> _quietSmoke;
        private float _quietSmokeUntil;

        private void QuietSmoke()
        {
            if (Time.time > _quietSmokeUntil) { _quietSmoke = null; if (_quietAnim != null) _quietAnim.fireEvents = true; _quietAnim = null; return; }
            foreach (ParticleSystem ps in _quietSmoke)
                if (ps != null && (ps.isPlaying || ps.particleCount > 0)) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Clear(true); }
            if (_quietAudio != null) foreach (AudioSource au in _quietAudio) if (au != null && au.isPlaying) au.Stop(); // its appearing caw too
        }

        private bool _arriveWaiting;
        private float _arriveFar; // m it set off from to fly in (a perch far off): neither the portal snap nor the way home's safety cut that short
        private float _arriveLen = 1.6f, _arriveArc = 0.6f;
        private System.Collections.Generic.List<Renderer> _hidden;

        // waiting for its turn to fly in: not seen yet
        private void ArriveHide(bool hide)
        {
            if (hide)
            {
                _hidden = new System.Collections.Generic.List<Renderer>();
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; _hidden.Add(r); }
            }
            else if (_hidden != null) { foreach (Renderer r in _hidden) if (r != null) r.enabled = true; _hidden = null; }
        }

        // the game's animator still in (or going back to) its hidden "away" state: the model shows nothing yet
        private Animator _arriveAnim;
        private bool StillHidden()
        {
            if (_arriveAnim == null) _arriveAnim = GetComponentInChildren<Animator>();
            Animator a = _arriveAnim;
            if (a == null || !a.enabled || !a.isInitialized) return false;
            return a.GetCurrentAnimatorStateInfo(0).IsTag("away") || (a.IsInTransition(0) && a.GetNextAnimatorStateInfo(0).IsTag("away"));
        }

        // the fly-in goes on by the motor (FlyIn 1 in AirStep): from where the game's arc has it, at its speed
        private void ArriveFlight(Vector3 pos, Quaternion rot, Vector3 vel) { ArriveFlight(pos, rot, vel, false); }

        private void ArriveFlight(Vector3 pos, Quaternion rot, Vector3 vel, bool fromStill)
        {
            _arriveStart = -1f;
            _arriveFar = Player != null ? Vector3.Distance(pos, Player.transform.position) : 0f; // (from a far perch: its whole way is flown)
            // the animator's fly-in state is stopped midway now; after the landing it is sent to its end, or it would play the landing again
            Animator a = _arriveAnim != null ? _arriveAnim : GetComponentInChildren<Animator>();
            if (a != null && a.enabled && a.isInitialized) Life.PendingState = a.GetCurrentAnimatorStateInfo(0).fullPathHash;
            AnimDirector.Take(gameObject, RavenAct.Arrive, this, "fly-in: the motor");
            transform.position = pos; transform.rotation = rot;
            _airMode = 1; _airSince = Time.time; _airPos = pos; _airTurn = rot; _homeSince = -1f;
            if (Motor.Launch(vel, "приліт", 1f) && !fromStill) Motor.Body.Airborne(); // already flying: no gentle take-off (from a perch: it does push off)
        }

        internal void LateUpdate()
        {
            if (Player == null || Bone == null) return;
            long t0 = Perf.On ? Perf.Now : 0;
            Place();
            if (Perf.On) Perf.Add(t0);
        }

        private void Place()
        {
            if (_quietSmoke != null) QuietSmoke();
            if (_landOn || _airMode != 0 || _arriveStart >= 0f) CalmSince = Time.time;
            Vector3 fwd = Vector3.ProjectOnPlane(Player.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) return;
            Quaternion facing = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            // leaning away from the head: Munin (left shoulder) tops to the left, Hugin to the right
            float tilt = Munin ? Shoulder.MuninTilt : -Shoulder.HuginTilt;
            // the body follows your turns a moment late (Hugin calmer, Munin quicker); the seat itself does not lag
            float yawNow = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            if (Life != null && Life.SitNew) _bodyYaw = Life.SitYaw(yawNow); // the sitting system: its body turns after you on its own spring
            else _bodyYaw = _bodyYawSet && Time.frameCount - _bodyYawFrame < 3 ? Mathf.LerpAngle(_bodyYaw, yawNow, Mathf.Clamp01(Time.deltaTime * (Munin ? Shoulder.TurnFollowMunin : Shoulder.TurnFollowHugin))) : yawNow;
            _bodyYawSet = true; _bodyYawFrame = Time.frameCount;
            transform.rotation = Quaternion.Euler(0f, _bodyYaw, 0f) * Quaternion.AngleAxis(tilt, Vector3.forward) * Quaternion.Euler(Shoulder.Rotation);
            // Munin sits on the left: the sideways offset is mirrored for him
            Vector3 off = Munin ? new Vector3(-Shoulder.Offset.x, Shoulder.Offset.y, Shoulder.Offset.z) : Shoulder.Offset;
            float big = Player != null ? Player.transform.localScale.x : 1f; // (a grown body: «Звір Муніна»)
            transform.position = Bone.position + facing * (off * big);
            // on what is worn: in the air no lift; the landing aims straight at the armour (measured as it begins,
            // in your pose then), so it sits down right onto it, nothing to settle afterwards
            bool busy = _airMode != 0 || _arriveStart >= 0f;
            float lift = busy ? 0f : ShoulderFit.Lift(Player, Munin, transform.position);
            _lift = _liftSet ? Mathf.MoveTowards(_lift, lift, Time.deltaTime * (busy || _landOn ? 1f : 0.6f)) : lift;
            _liftSet = true;
            transform.position += Vector3.up * (_lift - Shoulder.PerchDrop);
            SeatNow = transform.position; // where it sits (the landing record measures from here)
            if (!busy && !_landOn && Life != null) transform.position += Life.PinNudge; // its own little hops and steps on the shoulder
            if (Life != null && AirStep(facing)) { Life.Apply(); return; }
            if (_landOn && Life != null)
            {
                Quaternion seatRot = transform.rotation;
                if (_landFresh)
                {
                    _landOff = Quaternion.Inverse(seatRot) * (_landFrom - transform.position);
                    _landTurn = Quaternion.Inverse(seatRot) * _landRot;
                    _landFresh = false;
                }
                float way;
                bool still = Life.StepLand(out way);
                transform.position += seatRot * (_landOff * (1f - way)) + facing * LandFix(Life.LandU);
                transform.rotation = Quaternion.Slerp(seatRot * _landTurn, seatRot, Mathf.Clamp01(way * LandTurnRate));
                if (!still) { _landOn = false; AnimDirector.Release(gameObject, this); } // sitting again
                return;
            }
            if (_settleStart >= 0f)
            {
                // the gap is kept relative to the shoulder, so a running owner does not drag it
                if (_settleFresh)
                {
                    _settleOff = _settleFrom - transform.position; _settleFresh = false;
                    _settleLen += Quaternion.Angle(_settleRot, transform.rotation) / 180f * 0.35f; // a big turn left: a little longer
                }
                float st = (Time.time - _settleStart) / _settleLen;
                if (st >= 1f) { _settleStart = -1f; if (!_landOn) AnimDirector.Release(gameObject, this); } // sat down without the landing clip: the bird is free again
                else
                {
                    float se = st * st * (3f - 2f * st);
                    transform.position += _settleOff * (1f - se);
                    transform.rotation = Quaternion.Slerp(_settleRot, transform.rotation, se);
                }
            }
            if (_arriveStart >= 0f && _arriveWaiting)
            {
                if (Time.time < _arriveStart)
                {
                    // its turn has not come yet: nothing of it shows (pieces worn on it come a little later: hidden too)
                    transform.position = _from;
                    if (_hidden != null) // every frame (a piece put on now must not show for a moment), into one kept list: no garbage
                    {
                        GetComponentsInChildren<Renderer>(true, Pieces);
                        foreach (Renderer r in Pieces) if (r.enabled) { r.enabled = false; _hidden.Add(r); }
                        Pieces.Clear();
                    }
                    return;
                }
                // now: a puff of feathers there in the air, as the game's ravens show up, and the fly-in clip from its start
                _arriveWaiting = false;
                ArriveHide(false);
                Raven tpl = Shoulder.Template(Munin);
                if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, _from, Quaternion.identity, null, 0.7f, -1);
                AppearMute.Allow(gameObject, _arriveLen + 1.5f); // it really appears: the game's appearing shows for its fly-in
                Shoulder.Perch(GetComponentInChildren<Animator>(), true);
                _arriveStart = Time.time;
                _arcPrev = _from;
            }
            if (_arriveStart >= 0f)
            {
                float t = (Time.time - _arriveStart) / _arriveLen;
                if (t >= 1f) { _arriveStart = -1f; if (!_landOn) AnimDirector.Release(gameObject, this); }
                else
                {
                    float e = 1f - (1f - t) * (1f - t); // fast, then gently down onto the shoulder
                    Vector3 target = transform.position;
                    Vector3 pos = Vector3.Lerp(_from, target, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * _arriveArc;
                    Vector3 dir = Vector3.ProjectOnPlane(target - pos, Vector3.up);
                    if (dir.sqrMagnitude > 0.0004f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(Shoulder.Rotation);
                    transform.position = pos;
                    // the game's clip carries the bird in from far away by itself: held, it is drawn where it flies
                    if (Life != null) Life.HoldPerchRoots();
                    Vector3 vel = Time.deltaTime > 0f ? (pos - _arcPrev) / Time.deltaTime : Vector3.zero;
                    _arcPrev = pos;
                    // awake (out of the game's hidden state): the motor flies it on from here, at this speed
                    float since = Time.time - _arriveStart;
                    if (Life != null && since >= ArriveWake && (!StillHidden() || since >= ArriveWakeMax)) ArriveFlight(pos, transform.rotation, vel);
                    return; // the head and wings are left to the game's flying animation meanwhile
                }
            }
            if (Life != null) Life.Apply();
        }

        // During a dance or lying down the raven lifts off and hangs in the air above where the shoulder stands,
        // bobbing with its wing beats, out of the way of the moving body; then it settles back.
        // HoverSide: meters out from the middle for the smallest raven, HoverSideTop for a full-size one
        // (bigger wings need more room, or the two would beat into each other)
        internal static float HoverHeight = 0.65f, HoverSide = 0.72f, HoverSideTop = 0.95f;
        internal static float DriftUp = 0.12f, DriftSide = 0.06f, DriftHz = 0.35f; // the slow floating in the air, meters and beats a second
        private float _hoverCheck; // the emote is read a few times a second, not every frame
        private bool _hoverWant;

        // ---------------------------------------------------------------- the flight system in the pin's hands
        // Hanging over the shoulder during an emote (mode 2) and the outings (mode 3) are plans (FlightPlans.cs) flown
        // by the raven's motor (RavenMotor): a take-off with speed and inertia, following you with banks when you
        // move, then home through the landing.
        private float _lift;
        private bool _liftSet;
        private float _bodyYaw;
        private bool _bodyYawSet;
        private int _bodyYawFrame;
        private RavenMotor _motor;
        private int _airMode;          // 0 on the shoulder, 1 flying in (after the game's first moment), 2 hanging over it (an emote), 3 out stretching its wings, 4 an order
        private int _ordNow, _ordMode;  // «накази»: the order asked for now / the one it flies
        private readonly ScoutRunPlan _orderScout = new ScoutRunPlan();
        private readonly TrickPlan[] _tricks = MakeTricks(); // by order id (the figures)
        private static TrickPlan[] MakeTricks() { TrickPlan[] t = new TrickPlan[Orders.Count]; for (int o = 0; o < Orders.Count; o++) if (Orders.IsTrick(o)) t[o] = new TrickPlan(o); return t; }
        // the order is over, but a figure begun is flown to its end first (no flip upright mid-loop)
        private bool Finishing { get { return _airMode == 4 && _ordNow == Orders.None && Orders.IsTrick(_ordMode) && _tricks[_ordMode] != null && _tricks[_ordMode].InFigure; } }
        // s of a figure still to fly before a new order can begin (Orders adds it to the new order's time)
        internal float FigureLeft { get { return _airMode == 4 && Orders.IsTrick(_ordMode) && _tricks[_ordMode] != null ? _tricks[_ordMode].Left : 0f; } }
        private FlightPlan OrderPlan(int o)
        {
            if (o == Orders.Follow) return _follow;
            if (o == Orders.Circle) return _circle;
            if (o == Orders.Scout) return _orderScout;
            if (o == Orders.Chase) return _chase;
            if (o == Orders.Fetch) return _fetch;
            return _tricks[o];
        }
        // an order begins (from the shoulder or taking over the dance / an outing in the air)
        private void BeginOrder(Vector3 carry, Quaternion facing)
        {
            _ordMode = _ordNow; _outEnd = -1f; _homeSince = -1f; _airSince = Time.time;
            _outMode = Outing.CircleRight; // the circle plan's way round
            _ring = Random.Range(0f, 10f);
            FlightPlan plan = OrderPlan(_ordMode);
            plan.Begin();
            Motor.Launch(Motor.TakeoffVelocity(plan.TakeoffAim(this, facing), carry, plan.Name), plan.Name, 1f);
        }
        private float _airSince;
        private Vector3 _airPos;       // where it is in the air (the seat is written over it each frame first)
        private Quaternion _airTurn = Quaternion.identity;
        private int _outNow, _outMode; // «адаптивні ворони»: the outing asked for now / the one it is on
        private float _outCheck, _outEnd = -1f, _ring;
        internal float Ring { get { return _ring; } }      // this flight's own random phase (the plans' drift and rings)
        internal int OutMode { get { return _outMode; } }
        private readonly HoverPlan _hover = new HoverPlan();
        private readonly FollowPlan _follow = new FollowPlan();
        private readonly CirclePlan _circle = new CirclePlan();
        private readonly ScoutPlan _scout = new ScoutPlan();
        private readonly TrailPlan _trail = new TrailPlan();
        private readonly ChasePlan _chase = new ChasePlan();
        private readonly FetchPlan _fetch = new FetchPlan();
        internal RavenMotor Motor { get { if (_motor == null) _motor = RavenMotor.Of(gameObject, Munin, Player); return _motor; } }
        private readonly HuntPlan _hunt = new HuntPlan();
        private FlightPlan OutPlan(int mode) { return mode == Outing.Follow ? (FlightPlan)_follow : mode == Outing.Scout ? (FlightPlan)_scout : mode == Outing.Hunt ? (FlightPlan)_hunt : _circle; }

        internal static float FarAway = 150f; // m: farther than this from you it is brought close at once (a portal; a scouting goes up to 100 m)
        internal static float OutUp = 1f, OutBack = 2f; // flying along: how far above and behind
        internal static float ScoutAhead = 24f, ScoutUp = 5f; // scouting: how far ahead and how high it darts
        private float _followUntil = -1f; // after the dance, flying along a while if you are off at once (-1: not decided)
        internal static float FollowMin = 2.5f, FollowMax = 4f, FollowUp = 0.35f, FollowBack = 0.9f;

        private bool Following(Vector3 carry)
        {
            float run = carry.magnitude;
            if (_followUntil < 0f) _followUntil = run > 2f ? Time.time + Random.Range(FollowMin, FollowMax) : 0f;
            return Time.time < _followUntil && run > 1f;
        }
        private float _hoverEnd = -1f, _homeSince = -1f, _lingerUntil = -1f;

        private void OnDisable() { if (_quietAnim != null) { _quietAnim.fireEvents = true; _quietAnim = null; } if (_airMode != 0 && Life != null) Life.Hovering = false; _airMode = 0; _landOn = false; } // a strike or the dome takes the bird: they bring it back (the motor keeps it flying)

        // true while it is in the air here (then this frame's place and turn are the flight's)
        private bool AirStep(Quaternion facing)
        {
            float dt = Time.deltaTime;
            Vector3 seat = transform.position;
            Quaternion seatRot = transform.rotation;
            if (Time.time >= _hoverCheck) { _hoverCheck = Time.time + 0.25f; _hoverWant = Shoulder.Hovering(Player); }
            Vector3 carry = Player.GetVelocity();

            if (Time.time >= _outCheck) { _outCheck = Time.time + 0.25f; _outNow = Outing.Of(Player, Munin); _ordNow = Orders.Of(Player, Munin); }
            RavenMotor m = Motor;
            // an order while it hangs over the shoulder or is out on its own: it takes over in the air
            if ((_airMode == 2 || _airMode == 3) && _ordNow != Orders.None && AnimDirector.Take(gameObject, RavenAct.Command, this, "order: " + Orders.Names[_ordNow]))
            {
                _airMode = 4; BeginOrder(carry, facing); // (the hover flag: the director's, as it took the bird)
            }
            if (_airMode == 0)
            {
                bool arriving = _arriveStart >= 0f; // still flying in (hidden wait or the game's arc): it comes first
                if (_ordNow != Orders.None && !_landOn && !arriving && AnimDirector.Take(gameObject, RavenAct.Command, this, "order: " + Orders.Names[_ordNow]))
                {
                    _airMode = 4; _airPos = seat; _airTurn = seatRot;
                    BeginOrder(carry, facing);
                }
                else if (_hoverWant && !arriving)
                {
                    // an emote: up off the shoulder
                    if (!AnimDirector.Take(gameObject, RavenAct.Hover, this, "dance")) return false;
                    _airMode = 2; _airSince = Time.time; _airPos = seat; _airTurn = seatRot;
                    m.Launch(m.TakeoffVelocity(_hover.TakeoffAim(this, facing), carry, _hover.Name), _hover.Name, 1f);
                }
                else if (_outNow != Outing.None && !_landOn && !arriving && AnimDirector.Take(gameObject, RavenAct.Outing, this, _outNow == Outing.Follow ? "outing: along" : _outNow == Outing.Scout ? "outing: scouting" : "outing: circling"))
                {
                    // «адаптивні ворони»: off the shoulder to stretch its wings (up and a little out, then its way)
                    _airMode = 3; _airSince = Time.time; _airPos = seat; _airTurn = seatRot; _outMode = _outNow; _outEnd = -1f;
                    _ring = Random.Range(0f, 10f);
                    FlightPlan plan = OutPlan(_outMode);
                    plan.Begin();
                    m.Launch(m.TakeoffVelocity(plan.TakeoffAim(this, facing), carry, plan.Name), plan.Name, 1f);
                }
                else return false;
            }
            transform.position = _airPos;
            transform.rotation = _airTurn;
            if (m.Body == null) m.Launch(carry, "", 1f); // (never left without a body in the air)
            // left far behind (a portal, a teleport): it does not cross half the world, it shows up close behind you
            float farAway = _airMode == 1 ? Mathf.Max(FarAway, _arriveFar + 30f) : FarAway;
            if ((_airPos - Player.transform.position).sqrMagnitude > farAway * farAway)
            {
                Vector3 f = Vector3.ProjectOnPlane(Player.transform.forward, Vector3.up).normalized;
                _airPos = Player.transform.position + Vector3.up * 5f - f * 6f;
                transform.position = _airPos;
                m.Body.Vel = carry;
            }

            // an order or an outing over while you still dance, or flying in to you (back from a perch, out of the bag)
            // while you dance: into the hover from where it is, no landing in between
            if (_hoverWant && (_airMode == 1 || (_airMode == 4 && _ordNow == Orders.None && !Finishing) || (_airMode == 3 && _outNow == Outing.None)) && AnimDirector.Take(gameObject, RavenAct.Hover, this, "dance"))
            {
                _airMode = 2; _lingerUntil = -1f; _homeSince = -1f; _hoverEnd = -1f; _followUntil = -1f;
                if (m.Body != null) m.Body.NewLeg(); // it was coming home (final): now it hangs about, its templates and the way down "up and back first" again
            }

            // the emote is over: each lingers its own little while over the shoulder before coming down (not both at once)
            if (_airMode == 2 && _hoverWant) _lingerUntil = -1f;
            else if (_airMode == 2 && _lingerUntil < 0f) _lingerUntil = Time.time + (Munin ? Random.Range(0.3f, 1.4f) : Random.Range(0f, 0.6f));
            bool hanging = _airMode == 2 && (_hoverWant || Time.time < _lingerUntil);
            // still flying on (hanging, an outing, an order, trailing you): the way home has not begun
            if (hanging || (_airMode == 3 && _outNow != Outing.None) || (_airMode == 4 && (_ordNow != Orders.None || Finishing)) || (_airMode == 2 && _followUntil > Time.time)) _homeSince = -1f;
            if (hanging)
            {
                _hoverEnd = -1f; _followUntil = -1f;
                m.SetPlan(_hover.Name);
                _hover.Step(m, this, facing, carry, dt);
            }
            else if (_airMode == 4 && (_ordNow != Orders.None || Finishing))
            {
                // «накази»: another order given meanwhile: from here, on the same flight
                // (a figure begun is flown to its end first: no flip upright mid-loop)
                bool inFigure = Orders.IsTrick(_ordMode) && _tricks[_ordMode] != null && _tricks[_ordMode].InFigure;
                if (_ordNow != Orders.None && _ordNow != _ordMode && !inFigure) { _ordMode = _ordNow; _outEnd = -1f; OrderPlan(_ordMode).Begin(); }
                FlightPlan plan = OrderPlan(_ordMode);
                m.SetPlan(plan.Name);
                plan.Step(m, this, facing, carry, dt);
            }
            else if (_airMode == 3 && _outNow != Outing.None)
            {
                FlightPlan plan = OutPlan(_outMode);
                m.SetPlan(plan.Name);
                plan.Step(m, this, facing, carry, dt);
            }
            else if (_airMode == 2 && Following(carry))
            {
                m.SetPlan(_trail.Name);
                _trail.Step(m, this, facing, carry, dt);
            }
            else
            {
                // onto the seat: back from an outing it comes round to your side first; down from hovering, a little up
                // and back first, then down onto it (not a lift straight down)
                if (_homeSince < 0f) _homeSince = Time.time;
                bool pre = _airMode == 2 && !m.Body.Final;
                Vector3 preAim = Vector3.zero;
                if (pre)
                {
                    if (_hoverEnd < 0f) _hoverEnd = Time.time;
                    Vector3 f = Vector3.ProjectOnPlane(Player.transform.forward, Vector3.up).normalized;
                    preAim = seat - f * 0.7f + Vector3.up * 0.5f;
                    if (Time.time - _hoverEnd > 1.5f) { m.Body.MarkFinal(); pre = false; }
                }
                if ((_airMode == 3 || _airMode == 4) && _outEnd < 0f) _outEnd = Time.time;
                float speed = _airMode == 3 || _airMode == 4 ? m.Body.HomeSpeed(7f, carry, Time.time - _outEnd) : _airMode == 1 ? m.Body.HomeSpeed(FlyInSpeed, carry, Time.time - _airSince) : 4f;
                if (m.Home(seat, Player, speed, dt, false, _ring, pre, preAim) || m.Stuck || Time.time - _homeSince > 25f + (_airMode == 1 ? _arriveFar / 7f : 0f)) // a safety: never longer than this on the way home (a far fly-in: its way's time on top)
                {
                    if (ShoulderLife.LogLandings && ReferenceEquals(Player, Player.m_localPlayer) && Plugin.Instance != null) Plugin.Instance.Log("Landing (pin " + _airMode + "): facing " + Mathf.RoundToInt(m.Body.YawOff(Player.transform.forward)) + "° off the owner's way, " + Mathf.RoundToInt(Vector3.Distance(transform.position, seat) * 100f) + " cm from the seat, speed " + m.Body.Speed.ToString("0.0"));
                    Vector3 from = transform.position;
                    Quaternion turn = transform.rotation;
                    _airMode = 0; _homeSince = -1f;
                    transform.position = seat; transform.rotation = seatRot;
                    ComeBack(from, turn); // near: the landing; stuck far off (the safety ran out): gone in a puff and flown in again, not dragged across by the landing clip
                    return false;
                }
            }
            _airPos = transform.position;
            _airTurn = transform.rotation;
            return true;
        }

        // the spot over the shoulder where it hangs: above where the shoulder is when standing, apart from the
        // other raven, drifting slowly (the old hover's place)
        internal Vector3 HoverPoint(Quaternion facing)
        {
            float shoulderY = Player.transform.position.y + 1.55f * Player.transform.localScale.y;
            float size = Life != null ? Shoulder.SizeFor(Life.Level) : 1f;
            float apart = Mathf.Lerp(HoverSide, HoverSideTop, Mathf.InverseLerp(Mathf.Clamp(Shoulder.SmallestShare, 0.1f, 1f), 1f, size));
            Vector3 side = facing * new Vector3(Munin ? -apart : apart, 0f, -0.05f);
            float ph = Munin ? 2.1f : 0f;
            float drift = Mathf.Sin(Time.time * DriftHz * Mathf.PI * 2f + ph) * DriftUp + Mathf.Sin(Time.time * DriftHz * 1.73f * Mathf.PI * 2f + ph * 1.7f) * DriftUp * 0.4f;
            Vector3 sway = facing * new Vector3(Mathf.Sin(Time.time * DriftHz * 0.61f * Mathf.PI * 2f + ph) * DriftSide, 0f, Mathf.Sin(Time.time * DriftHz * 0.83f * Mathf.PI * 2f + ph * 0.5f) * DriftSide);
            return new Vector3(Player.transform.position.x, shoulderY + HoverHeight + drift, Player.transform.position.z) + side + sway;
        }
    }
}
#endif
