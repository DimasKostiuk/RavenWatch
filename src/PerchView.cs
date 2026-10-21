#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // ---------------------------------------------------------------- the perch in the world

    internal class PerchView : MonoBehaviour, Hoverable, Interactable
    {
        internal static readonly List<PerchView> All = new List<PerchView>();
        private ZNetView _nv;
        private Perches.Def _def;
        private Transform[] _seats;
        private GameObject[] _birds;
        private string[] _shown, _hidden;
        private float[] _waitUntil, _staleAt;
        private PerchFlight[] _incoming;
        private float _next;

        internal bool Valid { get { return _nv != null && _nv.IsValid(); } }
        private Transform[] _flips;
        internal bool Owner { get { return Valid && _nv.IsOwner(); } }
        internal ZDOID Id { get { return Valid ? _nv.GetZDO().m_uid : ZDOID.None; } }
        internal ZDO Zdo { get { return Valid ? _nv.GetZDO() : null; } } // (the sitting system: what its holder decided for the birds on it)
        internal int Count { get { return _seats != null ? _seats.Length : 0; } }
        internal float PerDay { get { return _def != null ? _def.PerDay : Perches.DailyCap; } }
        // a «sit here» spot (SitHere), not a perch someone built
        internal bool IsSpot { get { return _def != null && _def.Id == SitHere.DefId; } }
        // nobody sits on it, nobody flies to it, no bird of it about
        internal bool Idle
        {
            get
            {
                for (int i = 0; i < Count; i++) if (At(i) != null || (_incoming[i] != null && _incoming[i]) || _birds[i] != null) return false;
                return true;
            }
        }
        internal float IdleSince = -1f; // (SitHere: when it was first seen idle)
        // the bird sitting on seat i (landed, not on its way), and its life
        internal bool Settled(int seat) { return seat >= 0 && seat < Count && _birds[seat] != null && _incoming[seat] == null; }
        internal PerchLife LifeAt(int seat) { return Settled(seat) ? _birds[seat].GetComponent<PerchLife>() : null; }
        // words from the bird on seat i, here and for everyone near (a visit's: WorldVisit; a perch speaks by Say)
        internal void SayHere(int seat, string text) { if (!Valid || string.IsNullOrEmpty(text)) return; ShowSay(seat, text); Perches.SendSay(Id, seat, text); }
        // the player a «sit here» spot rides on (SitOn), or null
        internal Player Carrier { get { SpotFollow f = IsSpot ? GetComponent<SpotFollow>() : null; return f != null ? f.Who as Player : null; } }
        private float _envAt;

        // the roof and comfort here: from the perch's data (kept by whoever holds it), else measured now
        internal void Env(out bool roof, out int comfort)
        {
            if (Valid && Perches.ParseEnv(_nv.GetZDO().GetString(Perches.EnvKey, ""), out roof, out comfort)) return;
            Perches.Measure(SeatT(0).position, out roof, out comfort);
        }
        internal float Mul() { bool r; int c; Env(out r, out c); return Perches.Mul(r, c); }
        internal string EnvText() { bool r; int c; Env(out r, out c); return Perches.EnvText(r, c); }
        internal Transform SeatT(int i) { return _seats != null && i >= 0 && i < _seats.Length ? _seats[i] : transform; }

        // the seat as the raven sits on it: as the model has it, or turned round (it came from the other side)
        internal Transform Facing(int i, bool flip)
        {
            Transform s = SeatT(i);
            if (!flip) return s;
            if (_flips == null) _flips = new Transform[Count];
            if (i < 0 || i >= Count) return s;
            if (_flips[i] == null)
            {
                Transform f = new GameObject("flip").transform;
                f.SetParent(s, false);
                f.localRotation = Quaternion.Euler(0f, 180f, 0f);
                _flips[i] = f;
            }
            return _flips[i];
        }

        private void Awake()
        {
            _nv = GetComponent<ZNetView>();
            _def = Perches.DefOf(Utils.GetPrefabName(gameObject));
            List<Transform> seats = new List<Transform>();
            for (int i = 0; ; i++) { Transform t = transform.Find("seat_" + i); if (t == null) break; seats.Add(t); }
            _seats = seats.ToArray();
            _birds = new GameObject[_seats.Length]; _shown = new string[_seats.Length]; _hidden = new string[_seats.Length];
            _waitUntil = new float[_seats.Length]; _staleAt = new float[_seats.Length]; _incoming = new PerchFlight[_seats.Length];
            for (int i = 0; i < _seats.Length; i++) { _shown[i] = ""; _hidden[i] = null; }
            if (!Valid) return; // the hammer's ghost
            _nv.Register<int, string>(Perches.RpcSeat, OnSeat);
            WearNTear w = GetComponent<WearNTear>();
            if (w != null) w.m_onDestroyed = (Action)Delegate.Combine(w.m_onDestroyed, new Action(Broken));
            All.Add(this);
        }

        private void OnDestroy()
        {
            All.Remove(this);
            if (_birds != null) foreach (GameObject b in _birds) if (b != null) Destroy(b);
        }

        private void Broken() { if (Owner) Perches.Broke(_nv.GetZDO().m_uid); }

        // who sits on seat i: the ZDO's string read and parsed once, again only when it changes (asked very often)
        private string[] _rawAt;
        private Perches.Occupant[] _occAt;
        internal Perches.Occupant At(int i)
        {
            if (!Valid || i < 0 || i >= Count) return null;
            if (_rawAt == null) { _rawAt = new string[Count]; _occAt = new Perches.Occupant[Count]; }
            string raw = _nv.GetZDO().GetString(Perches.SeatKey(i), "");
            if (!ReferenceEquals(raw, _rawAt[i]) && raw != _rawAt[i]) { _rawAt[i] = raw; _occAt[i] = Perches.Parse(raw); }
            return _occAt[i];
        }

        internal int SeatOf(long pid, bool munin)
        {
            for (int i = 0; i < Count; i++) { Perches.Occupant o = At(i); if (o != null && o.Pid == pid && o.Munin == munin) return i; }
            return -1;
        }

        // any free seat, picked at random: whoever's raven, of whatever kind (two Hugins of two players fit too)
        internal int FreeSeat()
        {
            int free = 0, pick = -1;
            for (int i = 0; i < Count; i++)
                if (At(i) == null && UnityEngine.Random.Range(0, ++free) == 0) pick = i;
            return pick;
        }

        internal void Send(int seat, string data) { if (Valid) _nv.InvokeRPC(Perches.RpcSeat, seat, data); }
        internal string Outfit(int seat) { return Valid ? _nv.GetZDO().GetString(Perches.SeatKey(seat) + "o", "") : ""; }

        // ---------------------------------------------------------------- speaking (PerchSpeech)
        // The game that holds the perch decides when its ravens speak (one decides, everyone near sees): single lines at
        // moments (landed, greeting, a stranger, an enemy, woken) and now and then, and talks when Hugin and Munin of one
        // owner sit here. Each in the tone of its own trust (the seat's level), the owner's name and forms from the seat.
        // A line at most every GapSeconds per perch, a talk now and then while someone is by.
        internal static float GapSeconds = 40f, TalkMin = 240f, TalkMax = 540f, IdleChance = 0.35f, HearNear = 15f;
        private float _speakFree, _talkAt = -1f;
        private readonly List<string> _recentTalks = new List<string>();

        internal GameObject BirdAt(int seat) { return _birds != null && seat >= 0 && seat < _birds.Length ? _birds[seat] : null; }

        internal GameObject Bird(bool munin)
        {
            for (int i = 0; i < Count; i++) { Perches.Occupant o = At(i); if (o != null && o.Munin == munin && _birds[i] != null) return _birds[i]; }
            return null;
        }

        // over the perch, for the talk bubble
        internal Vector3 TalkPoint()
        {
            float top = transform.position.y;
            for (int i = 0; i < Count; i++) top = Mathf.Max(top, SeatT(i).position.y);
            Vector3 c = transform.position; c.y = top + 0.6f;
            return c;
        }

        // a single line from the raven on this seat (the holder only, now and then; 'chance' that it says anything)
        internal void Say(int seat, string tag, Dictionary<string, string> extra, float chance)
        {
            if (!Owner || IsSpot || Time.time < _speakFree || UnityEngine.Random.value > chance || TalkBubble.PlayingAt(this) || Plugin.Instance == null) return;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            Perches.Occupant o = At(seat);
            if (o == null || _birds[seat] == null) return;
            string text = Composer.ComposePerch(Plugin.Instance.PhraseDir, o.Munin, tag, o.Level, o.Name, o.Female, extra, o.Voice);
            if (string.IsNullOrEmpty(text)) return;
            _speakFree = Time.time + GapSeconds + RavenSpeech.ReadSeconds(text);
            ShowSay(seat, text);
            Perches.SendSay(Id, seat, text);
        }

        internal void ShowSay(int seat, string text)
        {
            Perches.Occupant o = At(seat);
            GameObject bird = seat >= 0 && seat < Count ? _birds[seat] : null;
            if (o == null || bird == null) return;
            Player me = Player.m_localPlayer;
            string name = Plugin.RavenName(o.Munin) + (me != null && o.Pid != me.GetPlayerID() && o.Name.Length > 0 ? " · " + o.Name : "");
            float secs = RavenSpeech.ReadSeconds(text);
            RavenSpeech.Show(bird, new Vector3(0f, 0.35f, 0f), RavenTalk.HearRange, secs, o.Munin, name, text, false);
            PerchLife life = bird.GetComponent<PerchLife>();
            if (life != null) life.Speak(secs * 0.6f);
            Transform other = Neighbour(seat);
            PerchLife ol = other != null ? other.GetComponent<PerchLife>() : null;
            if (ol != null) ol.Listen(bird.transform, secs);
        }

        // the talk bubble's line: the speaker's beak, the other one looks at it
        internal void Speaking(bool munin, float seconds)
        {
            GameObject sp = Bird(munin), li = Bird(!munin);
            PerchLife a = sp != null ? sp.GetComponent<PerchLife>() : null, b = li != null ? li.GetComponent<PerchLife>() : null;
            if (a != null) { a.Speak(seconds); if (li != null) a.Listen(li.transform, seconds); }
            if (b != null && sp != null) b.Listen(sp.transform, seconds);
        }

        // now and then, with someone near: a talk of the two (one owner's Hugin and Munin here), else a thought aloud
        private void Chatter()
        {
            if (_talkAt < 0f) { _talkAt = Time.time + UnityEngine.Random.Range(TalkMin * 0.4f, TalkMax * 0.6f); return; }
            if (Time.time < _talkAt) return;
            _talkAt = Time.time + UnityEngine.Random.Range(TalkMin, TalkMax);
            if (Time.time < _speakFree || TalkBubble.PlayingAt(this) || Plugin.Instance == null) return;
            Player near = null, owner = null; float nd = HearNear;
            Perches.Occupant h = null, m = null; int hs = -1, ms = -1;
            for (int i = 0; i < Count; i++)
            {
                Perches.Occupant o = At(i);
                if (o == null || _birds[i] == null) continue;
                PerchLife l = _birds[i].GetComponent<PerchLife>();
                if (l == null || !l.Calm) continue;
                if (o.Munin) { m = o; ms = i; } else { h = o; hs = i; }
            }
            if (h == null && m == null) return;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || p.IsDead()) continue;
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d < nd) { nd = d; near = p; }
            }
            if (near == null) return; // nobody to hear
            Perches.Occupant any = h ?? m;
            foreach (Player p in Player.GetAllPlayers()) if (p != null && p.GetPlayerID() == any.Pid) owner = p;
            if (h != null && m != null && h.Pid == m.Pid)
            {
                string friend = null;
                foreach (Player p in Player.GetAllPlayers())
                    if (p != null && p.GetPlayerID() != h.Pid && !Shadow.Flagged(p) && Vector3.Distance(p.transform.position, transform.position) < HearNear) { friend = p.GetPlayerName(); break; } // one hidden in the shadow is not named
                List<TalkBubble.Line> lines = null;
                foreach (string reason in TalkReasons(h, m, friend))
                {
                    lines = RavenTalk.PerchTalk(reason, h.Level, m.Level, h.Name, h.Female, friend, _recentTalks, h.Voice, m.Voice);
                    if (lines != null) break;
                }
                if (lines != null)
                {
                    float total = 0f;
                    foreach (TalkBubble.Line l in lines) total += TalkBubble.Pace(l.Text);
                    _speakFree = Time.time + total + GapSeconds;
                    TalkBubble.PlayAt(this, lines, owner == Player.m_localPlayer);
                    Perches.SendTalk(Id, lines);
                    return;
                }
            }
            int seat = h != null && (m == null || UnityEngine.Random.value < 0.5f) ? hs : ms;
            Say(seat, "perch_idle", null, IdleChance);
        }

        // which talks fit now: the weather and the hour first (not always), the place, a friend, the trust gap; then the perch or just talk
        private List<string> TalkReasons(Perches.Occupant h, Perches.Occupant m, string friend)
        {
            List<string> r = new List<string>();
            float f = EnvMan.instance != null ? EnvMan.instance.GetDayFraction() : 0.5f;
            bool roof; int comfort;
            Env(out roof, out comfort);
            bool rain, snow, storm, fog; // as it falls here: no rain under the perch's roof, no rain of ash, a snowstorm is snow
            RavenTalk.Sky(false, roof, WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(transform.position) : Heightmap.Biome.Meadows, out rain, out snow, out storm, out fog);
            if (storm) r.Add("storm");
            if (snow) r.Add("snow");
            if (rain) r.Add("rain");
            if (fog) r.Add("fog");
            if (f >= 0.22f && f < 0.34f) r.Add("dawn");
            if (f >= 0.72f && f < 0.84f) r.Add("dusk");
            if (EnvMan.IsNight()) r.Add("night");
            if (roof && comfort >= 5) r.Add("rest");
            if (friend != null) r.Add("friend");
            if (h.Level - m.Level >= 2) r.Add("gap_h"); else if (m.Level - h.Level >= 2) r.Add("gap_m");
            r.RemoveAll(x => UnityEngine.Random.value > 0.45f); // the weather is not always the talk
            r.Add(UnityEngine.Random.value < 0.5f ? "perch" : "idle");
            r.Add("perch");
            return r;
        }

        // Its raven flies off from here to Odin (a shy one, or after a ritual), not to the shoulder: up and away, gone
        // (ShoulderFlyOff, as from the shoulder). Everyone sees it so (the seat's "g" mark).
        internal void Depart(int seat, string token)
        {
            if (seat >= 0 && seat < Count)
            {
                Perches.Occupant o = At(seat);
                if (o != null && o.Token == token) { FlyAway(seat, o); _hidden[seat] = o.Raw; }
            }
            Send(seat, "!" + token);
        }

        private bool Departed(int seat, string token) { return Valid && token.Length > 0 && _nv.GetZDO().GetString(Perches.SeatKey(seat) + "g", "") == token; }

        private void FlyAway(int seat, Perches.Occupant o)
        {
            PerchFlight f = _incoming[seat];
            GameObject b = f != null ? f.gameObject : _birds[seat];
            _incoming[seat] = null; _birds[seat] = null;
            if (b != null) Perches.FlyAway(b, o.Munin, o.Level, PlayerOf(o.Pid), Facing(seat, o.Flip));
        }

        // let the seat go (called back from here): the bird is gone at once in this game, its way home starts at it
        internal void Release(int seat, string token)
        {
            if (seat >= 0 && seat < Count)
            {
                Perches.Occupant o = At(seat);
                if (o != null && o.Token == token)
                {
                    PerchFlight f = _incoming[seat];
                    if (f != null) { f.TurnBack(o.Pid, o.Munin); _incoming[seat] = null; } // still on its way: it turns back from where it is, still seen
                    else HandOver(seat, o);
                    _hidden[seat] = o.Raw;
                }
            }
            Send(seat, "-" + token);
        }

        // on the perch's owner: sit down ("pid|..\noutfit") or let go ("-token")
        private void OnSeat(long sender, int seat, string data)
        {
            if (!Owner || seat < 0 || seat >= Count || data == null) return;
            ZDO z = _nv.GetZDO();
            Perches.Occupant cur = At(seat);
            if (data.StartsWith("-") || data.StartsWith("!"))
            {
                string token = data.Substring(1);
                if (!Perches.MayChange(sender, cur) || cur == null || cur.Token != token) return;
                if (data[0] == '!') z.Set(Perches.SeatKey(seat) + "g", token); // off to Odin, not to the shoulder
                ClearIf(seat, token);
                return;
            }
            string[] two = data.Split('\n');
            Perches.Occupant want = Perches.Parse(two[0]);
            if (want == null) return;
            if (cur == null)
            {
                if (want.Uid != 0 && want.Uid != sender) return; // only for yourself: a raven sits down in its owner's name
            }
            else
            {
                if (cur.Token != want.Token) return; // taken (also by another raven of yours)
                if (!Perches.MayChange(sender, cur) || want.Pid != cur.Pid || want.Uid != cur.Uid || want.Munin != cur.Munin) return; // the same raven, changed only by its owner or the holder (a turn)
            }
            z.Set(Perches.SeatKey(seat), two[0]);
            z.Set(Perches.SeatKey(seat) + "o", two.Length > 1 ? two[1] : "");
            z.Set(Perches.SeatKey(seat) + "w", two.Length > 2 ? two[2] : "");
        }

        internal void ClearIf(int seat, string token)
        {
            Perches.Occupant cur = At(seat);
            if (cur == null || cur.Token != token) return;
            ZDO z = _nv.GetZDO();
            z.Set(Perches.SeatKey(seat), "");
            z.Set(Perches.SeatKey(seat) + "o", "");
            z.Set(Perches.SeatKey(seat) + "w", "");
        }

        private Vector3 Pos(int seat) { return _birds[seat] != null ? _birds[seat].transform.position : SeatT(seat).position; }

        private void Kill(int seat) { if (_birds[seat] != null) Destroy(_birds[seat]); _birds[seat] = null; }

        // the sitting bird is let go to the shoulder's new model (it stays seen until that takes over)
        private void HandOver(int seat, Perches.Occupant o)
        {
            GameObject b = _birds[seat];
            _birds[seat] = null;
            if (b == null) { Perches.NoteLeft(o.Pid, o.Munin, SeatT(seat).position, SeatT(seat).rotation * Quaternion.Euler(Shoulder.Rotation), null); return; }
            Perches.NoteLeft(o.Pid, o.Munin, b.transform.position, b.transform.rotation, b);
            b.transform.SetParent(null, true);
            PerchLife life = b.GetComponent<PerchLife>();
            if (life != null) { life.Crouch(); life.View = null; } // it crouches, ready to push off; it no longer speaks or turns for this seat
        }

        // its raven is going back to the shoulder in this game (seen before the perch's news): the bird itself goes
        // to the shoulder (bird), the seat stays empty here till the news
        internal Vector3 Give(int seat, out Quaternion rot, out GameObject bird)
        {
            rot = _birds[seat] != null ? _birds[seat].transform.rotation : SeatT(seat).rotation * Quaternion.Euler(Shoulder.Rotation);
            Vector3 at = Pos(seat);
            Perches.Occupant o = At(seat);
            _hidden[seat] = o != null ? o.Raw : null;
            bird = _birds[seat];
            _birds[seat] = null;
            if (_incoming[seat] != null) // still on its way here: that very bird turns back (no second one from the seat)
            {
                bird = _incoming[seat].gameObject;
                at = bird.transform.position; rot = bird.transform.rotation;
                _incoming[seat] = null;
            }
            return at;
        }

        // Every 30 s, on the game that holds the perch: each raven sitting here looks who is near (its owner first, else
        // the nearest other player, within 12 m); someone behind it and no wall on that side: it turns round to them.
        private float[] _faceAt;
        private void FaceNearest()
        {
            if (_faceAt == null) { _faceAt = new float[Count]; for (int i = 0; i < Count; i++) _faceAt[i] = Time.time + PerchLife.FaceEvery * (0.5f + 0.5f * i); }
            for (int i = 0; i < Count; i++)
            {
                if (Time.time < _faceAt[i]) continue;
                _faceAt[i] = Time.time + PerchLife.FaceEvery;
                Perches.Occupant o = At(i);
                if (o == null || _birds[i] == null) continue;
                PerchLife life = _birds[i].GetComponent<PerchLife>();
                if (life == null || !life.CanTurn) continue;
                Transform face = Facing(i, o.Flip);
                Player who = null; float best = PerchLife.FaceNear;
                foreach (Player p in Player.GetAllPlayers())
                {
                    if (p == null || p.IsDead()) continue;
                    float d = Vector3.Distance(p.transform.position, face.position);
                    if (p.GetPlayerID() == o.Pid && d <= PerchLife.FaceNear) { who = p; break; } // its owner first
                    if (d < best) { best = d; who = p; }
                }
                if (who == null) continue;
                Vector3 to = who.transform.position - face.position, fwd = face.forward;
                to.y = 0f; fwd.y = 0f;
                if (to.sqrMagnitude < 0.01f || Vector3.Dot(to.normalized, fwd.normalized) > -0.2f) continue; // not behind it
                if (Perches.WallAhead(this, i, !o.Flip)) continue;
                Perches.RequestTurn(this, i); // the holder: written at once, seen by everyone
            }
        }

        // the other raven on this perch, if one sits there (the two look at each other, now and then a nudge)
        internal Transform Neighbour(int seat)
        {
            for (int i = 0; i < Count; i++) if (i != seat && _birds[i] != null) return _birds[i].transform;
            return null;
        }


        internal void Incoming(int seat, PerchFlight f)
        {
            _incoming[seat] = f; Kill(seat); _hidden[seat] = null;
            Perches.Occupant o = At(seat);
            _shown[seat] = o != null ? o.Raw : ""; // seen as this flight's (else the next look takes it for a change and drops the flight)
        }

        // called back here already, the perch's news not come yet: not a seat to fly to
        internal bool IsHidden(int seat) { Perches.Occupant o = At(seat); return o != null && _hidden[seat] == o.Raw; }

        // only the perch's holder speaks for it
        internal bool HeldBy(long uid) { return Valid && _nv.GetZDO().GetOwner() == uid; }

        // the flight landed: the bird that sits here from now on
        // bird: the one that flew here; it sits down itself (no new copy in its place)
        internal void Landed(int seat, GameObject bird)
        {
            _incoming[seat] = null;
            _waitUntil[seat] = 0f;
            _next = 0f;
            Draw(seat, bird);
            PerchLife life = _birds[seat] != null ? _birds[seat].GetComponent<PerchLife>() : null;
            if (life != null) life.JustLanded(); // it stands on the bar first, then sits down
        }

        private void Update()
        {
            Perches.Sweep();
            if (!Valid || Time.time < _next) return;
            _next = Time.time + 0.25f;
            if (Owner && !IsSpot) { FaceNearest(); Chatter(); } // (a «sit here» spot: no perch talk, no turning on a head)
            if (Owner && !IsSpot && Time.time >= _envAt) // the place measured now and then by whoever holds the perch (a spot gives nothing: not measured)
            {
                _envAt = Time.time + 30f;
                bool roof; int comfort;
                Perches.Measure(SeatT(0).position, out roof, out comfort);
                string env = (roof ? "1" : "0") + "|" + comfort;
                if (_nv.GetZDO().GetString(Perches.EnvKey, "") != env) _nv.GetZDO().Set(Perches.EnvKey, env);
            }
            Player me = Player.m_localPlayer;
            for (int i = 0; i < Count; i++)
            {
                Perches.Occupant o = At(i);
                string raw = o != null ? o.Raw : "";
                Perches.Occupant prev = raw != _shown[i] ? Perches.Parse(_shown[i]) : null;
                if (_incoming[i] != null && raw != _shown[i] && (o == null || prev == null || o.Token != prev.Token))
                {
                    // called back (or the seat changed hands) while it flew here: it turns back from where it is
                    if (prev != null && Departed(i, prev.Token)) FlyAway(i, prev); // off to Odin instead
                    else if (prev != null) _incoming[i].TurnBack(prev.Pid, prev.Munin);
                    else Destroy(_incoming[i].gameObject);
                    _incoming[i] = null;
                }
                if (prev != null && o != null && prev.Token == o.Token && prev.Flip != o.Flip && _birds[i] != null)
                {
                    PerchLife turning = _birds[i].GetComponent<PerchLife>();
                    if (turning != null) turning.TurnTo(Facing(i, o.Flip));
                    else { _birds[i].transform.SetParent(Facing(i, o.Flip), false); }
                    _shown[i] = raw;
                }
                if (raw != _shown[i])
                {
                    Perches.Occupant was = Perches.Parse(_shown[i]);
                    if (was != null && _birds[i] != null && Departed(i, was.Token)) FlyAway(i, was); // off to Odin: it flies away
                    else if (was != null && _birds[i] != null)
                    {
                        Player wp = PlayerOf(was.Pid);
                        if (wp != null && Vector3.Distance(wp.transform.position, transform.position) < Perches.Reach) HandOver(i, was); // called back: it flies to them
                        else { Perches.Puff(_birds[i], was.Munin, "its seat freed, its owner not near"); _birds[i] = null; }                                            // gone without them: a puff
                    }
                    Kill(i);
                    _shown[i] = raw;
                    if (_hidden[i] != raw) _hidden[i] = null;
                    // its owner is here with the raven still on the shoulder: it is coming, the flight draws it
                    if (o != null && _incoming[i] == null)
                    {
                        Player pl = PlayerOf(o.Pid);
                        _waitUntil[i] = pl != null && Shoulder.ModelOf(pl, o.Munin) != null && Vector3.Distance(pl.transform.position, transform.position) < Perches.Reach ? Time.time + 6f : 0f;
                    }
                }
                if (_incoming[i] == null || !_incoming[i]) _incoming[i] = null;
                // its owner has left the game: the raven leaves too (a puff here; the seat freed by whoever holds the perch)
                // (your own old seat from before is not this: your raven flies to you from it, then the seat goes)
                if (o != null && (me == null || o.Pid != me.GetPlayerID()) && !Perches.Online(o.Uid) && PlayerOf(o.Pid) == null)
                {
                    if (_birds[i] != null) { Perches.Puff(_birds[i], o.Munin, "its owner left the game"); _birds[i] = null; }
                    _hidden[i] = raw;
                    if (Owner) ClearIf(i, o.Token);
                    continue;
                }
                if (o != null && _birds[i] == null && _incoming[i] == null && _hidden[i] != raw && Time.time >= _waitUntil[i]) Draw(i);
                // a seat of mine that no raven of mine holds (a lost game, an old copy): let it go
                if (o != null && me != null && o.Pid == me.GetPlayerID() && (Perches.Ready || Perches.FreshDone) && Time.time >= _staleAt[i])
                {
                    _staleAt[i] = Time.time + 5f; // looked at every 5 s (it reads your bag)
                    if (!Perches.HasToken(me, o.Token))
                    {
                        if (_birds[i] != null) { Kill(i); _hidden[i] = raw; } // your seat from before: empty, quietly
                        Send(i, "-" + o.Token);
                    }
                }
            }
        }

        internal static Player PlayerOf(long pid)
        {
            foreach (Player p in Player.GetAllPlayers()) if (p != null && p.GetPlayerID() == pid) return p;
            return null;
        }

        private void Draw(int seat) { Draw(seat, null); }

        private void Draw(int seat, GameObject bird)
        {
            Perches.Occupant o = At(seat);
            if ((ZNet.instance != null && ZNet.instance.IsDedicated()) || o == null || _hidden[seat] == o.Raw) // nobody looks there / called back here already
            {
                if (bird != null) Perches.Puff(bird, o != null ? o.Munin : false, "landed on a seat no longer its own");
                return;
            }
            Kill(seat);
            float size = Shoulder.Scale * Shoulder.SizeFor(o.Level);
            GameObject r = bird != null
                ? Perches.Seat(bird, Facing(seat, o.Flip), o.Munin, size, -1, _def != null ? _def.Pose : null, _def != null ? _def.Stand : null) // the one that flew here
                : Perches.Sitter(Facing(seat, o.Flip), o.Munin, size, -1, _def != null ? _def.Pose : null, _def != null ? _def.Stand : null);  // seen here for the first time
            if (r == null) return;
            _birds[seat] = r;
            PerchLife life = r.AddComponent<PerchLife>();
            life.Munin = o.Munin; life.Level = o.Level; life.Owner = o.Pid; life.VoiceCode = o.Voice; life.View = this; life.Seat = seat;
            PerchPose pose = r.GetComponent<PerchPose>();
            if (pose != null) { pose.Life = life; life.Pose = pose; }
            Outfits.Dress(r, _nv.GetZDO().GetString(Perches.SeatKey(seat) + "o", ""));
            RavenWear.Dress(r, o.Munin, _nv.GetZDO().GetString(Perches.SeatKey(seat) + "w", ""));
        }

        // ---- what you see and what E does

        public string GetHoverName() { return _def != null ? _def.Name : Lang.T("Жердина"); }

        public float GetHoverOffset() { return 0f; }

        // what you see when you look at it: made at most four times a second (it reads the bag and the seats)
        private string _hover = "";
        private float _hoverAt = -1f;
        public string GetHoverText()
        {
            if (Time.time - _hoverAt < 0.25f) return _hover;
            _hoverAt = Time.time;
            _hover = MakeHover();
            return _hover;
        }

        private string MakeHover()
        {
            if (!Valid) return "";
            StringBuilder sb = new StringBuilder(GetHoverName());
            sb.Append("\n<color=#9A9A9A>").Append(EnvText()).Append(Lang.F(" · норма {0} на день", Mathf.RoundToInt(PerDay))).Append("</color>");
            Player p = Player.m_localPlayer;
            for (int i = 0; i < Count; i++)
            {
                Perches.Occupant o = At(i);
                if (o == null || (p != null && o.Pid == p.GetPlayerID())) continue;
                sb.Append("\n<color=#9A9A9A>").Append(Lang.F("{0} гравця {1}", Plugin.RavenName(o.Munin), o.Name)).Append("</color>");
            }
            foreach (bool munin in new[] { false, true })
            {
                string key = munin ? "[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] " : "[<color=yellow><b>$KEY_Use</b></color>] ";
                ItemDrop.ItemData mine = Perches.MineOn(p, this, munin);
                if (mine != null)
                {
                    string state = StateOf(p.GetPlayerID(), munin);
                    sb.Append('\n').Append(key).Append(Lang.F("Забрати {0}", Plugin.RavenNameGen(munin))).Append(" <color=#9A9A9A>(").Append(state.Length > 0 ? state + ", " : "").Append(Perches.DayText(mine)).Append(")</color>");
                    continue;
                }
                ItemDrop.ItemData on;
                string why = Perches.WhyNot(p, munin, out on);
                if (on == null) continue;
                if (why == null && FreeSeat() < 0) why = Lang.T("нема місця");
                sb.Append('\n').Append(key).Append(Lang.F("Посадити {0}", Plugin.RavenNameGen(munin)));
                if (why != null) sb.Append(" <color=#9A9A9A>— ").Append(why.TrimEnd('.')).Append("</color>");
            }
            return Localization.instance != null ? Lang.Game(sb.ToString()) : sb.ToString();
        }

        private string StateOf(long pid, bool munin)
        {
            int seat = SeatOf(pid, munin);
            PerchLife life = seat >= 0 && _birds[seat] != null ? _birds[seat].GetComponent<PerchLife>() : null;
            return life != null ? life.State() : "";
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (hold || p == null || p != Player.m_localPlayer || !Valid) return false;
            bool munin = alt;
            ItemDrop.ItemData mine = Perches.MineOn(p, this, munin);
            if (mine != null) Perches.CallBack(p, this, munin, mine);
            else Perches.Sit(p, this, munin);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }

}
#endif
