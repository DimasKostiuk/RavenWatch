#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The в'язь at work (binds-plan.md §7): it lives only while the raven wearing it sits on your shoulder. It has
    // charges (2/3/4 by its tree); each firing spends one, each spent one comes back on its own timer. A switch in the
    // gifts circle (Q) turns it off — then it neither fires nor recharges (no «off to save up»), the charges kept.
    //   Б + Г: fires by itself when a Геймдаль condition happens; only Б: a button (Q); only Г: «рух» (speed).
    // Everything in percent: damage of the weapon in hand, healing and shield of your health — felt at every stage.
    // The game of the wearer counts it all; damage goes as the player's own hits, healing of friends by an RPC.
    internal static class BindRun
    {
        internal const string StateKey = "RavenWatch_bindrun"; // player customData: b=<bind id>;on=1;t=<s left>,… (spent charges)
        internal const float Gap = 2f;                         // s between two firings (a series, not all at once)
        private const string RpcHeal = "RavenWatch_BindHeal";

        private static string _bind = "";
        private static Bind _b;
        private static BindPower _pow;
        private static bool _on = true;
        private static readonly List<float> _spent = new List<float>(); // seconds left for each spent charge
        private static float _lastFire = -100f, _saveAt;
        private static bool _dirty;
        private static int _wearer = -1; // 0 Hugin, 1 Munin, -1 none on the shoulder
        private static float _lookAt;

        internal static int Version; // goes up when what the window / book shows changed

        // ---------------------------------------------------------------- what there is
        internal static BindPower Power { get { return _pow; } }
        internal static string Plain { get { return _plain; } } // its sentence without tags (made once per в'язь)
        internal static bool Shielded { get { return _shield > 0f || Time.time < _lightUntil; } }
        private static string _plain = "";
        private static Player _player; // whose state this is (another character: everything anew)
        internal static bool On { get { return _on; } }
        internal static bool Active { get { return _pow != null && _wearer >= 0; } }
        internal static int Charges { get { return _pow == null ? 0 : Mathf.Max(0, _pow.Charges - _spent.Count); } }
        internal static float NextIn { get { float m = float.MaxValue; foreach (float t in _spent) m = Mathf.Min(m, t); return _spent.Count == 0 ? 0f : m; } }

        internal static BindPower PowerOf(string id) { Bind b = Bind.Parse(id); return b != null ? BindPower.Of(b) : null; }

        // ---------------------------------------------------------------- the record
        private static void Load(Player p, string bind)
        {
            _bind = bind; _b = Bind.Parse(bind); _pow = _b != null ? BindPower.Of(_b) : null;
            _plain = _pow != null ? StripTags(_pow.Sentence()) : "";
            _spent.Clear(); _on = true;
            _shield = 0f; _leechHits = 0; _vanishUntil = _hotUntil = _lightUntil = _helUntil = _shadowStrikeUntil = 0f; _wearer = -1; _lookAt = 0f;
            _badgeStale = true; _badgeKey = -1;
            string raw;
            if (p != null && p.m_customData.TryGetValue(StateKey, out raw))
            {
                string had = "";
                List<float> t = new List<float>();
                bool on = true;
                foreach (string part in raw.Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                    if (k == "b") had = v;
                    else if (k == "on") on = v != "0";
                    else if (k == "t") foreach (string s in v.Split(',')) { float f; if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) t.Add(Mathf.Max(0f, f)); }
                }
                if (had == bind) { _on = on; _spent.AddRange(t); } // (a new в'язь starts full)
            }
            if (_pow != null) while (_spent.Count > _pow.Charges) _spent.RemoveAt(_spent.Count - 1);
            _dirty = true;
            Version++;
        }

        private static void Save(Player p)
        {
            _dirty = false;
            if (p == null) return;
            if (_bind.Length == 0) { p.m_customData.Remove(StateKey); return; }
            StringBuilder sb = new StringBuilder("b=").Append(_bind).Append(";on=").Append(_on ? 1 : 0);
            if (_spent.Count > 0)
            {
                sb.Append(";t=");
                for (int i = 0; i < _spent.Count; i++) { if (i > 0) sb.Append(','); sb.Append(_spent[i].ToString("0.#", CultureInfo.InvariantCulture)); }
            }
            p.m_customData[StateKey] = sb.ToString();
        }

        internal static void SetOn(Player p, bool on)
        {
            if (_on == on) return;
            _on = on; _dirty = true; Version++;
            if (p != null) RavenMsg.Center(p, on ? Lang.T("В'язь увімкнено.") : Lang.T("В'язь вимкнено: не спрацьовує й не відновлює заряди."));
        }

        // ---------------------------------------------------------------- every frame (yours)
        private static float _run, _maxAir = float.MinValue;
        private static bool _ran, _dodged, _air;
        private static float _hpWas = 1f;

        internal static void Tick(Player p, float dt)
        {
            RuneBook.Data d = RuneBook.Of(p);
            if (d.Bind != _bind || !ReferenceEquals(p, _player)) { _player = p; Load(p, d.Bind); }
            if (_pow == null) { Badge(p, false); if (_dirty) Save(p); return; } // (unwoven: its old state goes, a new one starts full)
            if (Time.time >= _lookAt)
            {
                _lookAt = Time.time + 0.5f;
                int w = -1;
                for (int i = 0; i < 2 && w < 0; i++)
                {
                    bool m = i == 1;
                    ItemDrop.ItemData it = Trust.Worn(m);
                    if (it != null && RavenWear.Of(it).Bind == _bind && Shoulder.NotOnShoulder(p, m) == null) w = i;
                }
                if (w != _wearer) { _wearer = w; Version++; }
                if (RuneStars.BindStars(_b) != _pow.Stars) { _pow = BindPower.Of(_b); _plain = StripTags(_pow.Sentence()); Version++; _badgeStale = true; } // (a spark of Odin on one of its runes)
            }
            bool live = _wearer >= 0 && _on && !p.IsDead();
            if (live && _spent.Count > 0)
            {
                for (int i = _spent.Count - 1; i >= 0; i--)
                {
                    _spent[i] -= dt;
                    if (_spent[i] <= 0f) { _spent.RemoveAt(i); Version++; }
                }
                _dirty = true;
            }
            Badge(p, _wearer >= 0);
            States(p, dt);
            if (live) Watch(p, dt);
            else { _hpWas = p.GetMaxHealth() > 0f ? p.GetHealth() / p.GetMaxHealth() : 1f; _air = false; _run = 0f; _ran = false; } // (a fall or a run while it was off counts for nothing)
            if (_dirty && Time.time >= _saveAt) { _saveAt = Time.time + 2f; Save(p); }
        }

        // the conditions that the frame shows: running, a dodge, health falling under 30%, a landing from height
        private static void Watch(Player p, float dt)
        {
            bool running = p.IsRunning() && p.GetVelocity().magnitude > 2f;
            if (running) { _run += dt; if (_run >= 3f && !_ran) { _ran = true; Trigger(p, "sprint"); } }
            else { _run = 0f; _ran = false; }
            bool dodge = p.InDodge();
            if (dodge && !_dodged) Trigger(p, "dodge");
            _dodged = dodge;
            float hp = p.GetMaxHealth() > 0f ? p.GetHealth() / p.GetMaxHealth() : 1f;
            if (hp < 0.3f && _hpWas >= 0.3f && hp > 0f) Trigger(p, "lowhp");
            _hpWas = hp;
            float y = p.transform.position.y;
            if (!p.IsOnGround()) { if (!_air) { _air = true; _maxAir = y; } else _maxAir = Mathf.Max(_maxAir, y); }
            else if (_air) { _air = false; if (_maxAir - y >= 4f) Trigger(p, "land"); }
        }

        // ---------------------------------------------------------------- firing
        internal static void Trigger(Player p, string what)
        {
            if (_pow == null || p == null || p != Player.m_localPlayer) return;
            if (!_pow.When.Contains(what)) return;
            if (_pow.Mode == BindMode.Auto || _pow.Mode == BindMode.Motion) Fire(p);
        }

        // the button (only Бальдр): from the gifts circle or the console; null when it fired
        internal static string Press(Player p)
        {
            if (_pow == null) return Lang.T("У тебе нема в'язі.");
            if (_pow.Mode != BindMode.Button) return Lang.T("Ця в'язь спрацьовує сама.");
            return Fire(p);
        }

        internal static string Fire(Player p)
        {
            if (_pow == null || p == null || p.IsDead()) return Lang.T("У тебе нема в'язі.");
            if (_wearer < 0) return Lang.T("Ворон із в'яззю не на плечі.");
            if (!_on) return Lang.T("В'язь вимкнено.");
            if (Charges <= 0) return Lang.F("Заряди ще не відновились ({0} с).", Mathf.CeilToInt(NextIn));
            if (Time.time - _lastFire < Gap) return Lang.T("Зарано.");
            float night = _pow.Night && (EnvMan.IsNight() || p.InInterior()) ? 1.6f : 1f;
            if (_pow.Mode != BindMode.Motion)
            {
                bool foes = Foes(p, (_pow.Active == "hammer" ? 30f : 10f) * _pow.Radius).Count > 0;
                bool useful = _pow.Active.Length > 0 && Useful(p, _pow.Active, foes);
                foreach (string a in _pow.What) useful |= Useful(p, a, foes);
                if (!useful) return foes ? Lang.T("Зараз це нічого не дасть.") : Lang.T("Поруч нема ворогів."); // (no charge spent on nothing)
            }
            _lastFire = Time.time;
            _spent.Add(_pow.Recharge);
            _dirty = true; Version++;
            bool twice = _pow.Double > 0f && UnityEngine.Random.value < _pow.Double;
            if (_pow.Mode == BindMode.Motion) Rush(p, twice);
            else
            {
                if (_pow.Active.Length > 0) Great(p, _pow.Active, night, twice);
                foreach (string a in _pow.What) Do(p, a, night, twice);
            }
            if (_pow.AllyHeal) HealNear(p, 0.05f);
            if (_pow.BloodCost)
            {
                float max = p.GetMaxHealth(), cost = max * 0.1f, hp = p.GetHealth();
                if (hp - cost >= max * 0.3f) p.UseHealth(cost);
            }
            RavenFx.Play("vfx_offering", p.transform.position + Vector3.up, Quaternion.identity);
            RavenFx.Play("sfx_raven_kaw", p.transform.position, Quaternion.identity);
            return null;
        }

        // a part is worth a charge now: the ones against foes with foes near, healing with health missing
        private static bool Useful(Player p, string a, bool foes)
        {
            switch (a)
            {
                case "flash": case "frost": case "chain": case "poison": case "gust": case "vanish": case "leech":
                case "sun": case "crown": case "hammer": case "hel": case "shadow": case "sacrifice":
                    return foes;
                case "heal": return p.GetMaxHealth() > 0f && p.GetHealth() / p.GetMaxHealth() < 0.9f;
            }
            return true;
        }

        // ---------------------------------------------------------------- what Бальдр does
        private static bool _ours; // our own hits pass the hit patches without counting as yours
        internal static bool Ours { get { return _ours; } }
        private static float _vanishUntil, _leechUntil, _shieldUntil, _hotUntil, _hotPerSec;
        private static int _leechHits;
        private static float _leechPct, _shield;

        private static void Do(Player p, string a, float night, bool twice)
        {
            BindPower w = _pow;
            float pw = w.Pow * night, dmg = Weapon(p), max = p.GetMaxHealth();
            Vector3 at = p.transform.position;
            switch (a)
            {
                case "flash":
                    foreach (Character c in Foes(p, 4f * w.Radius)) { HitData h = Hit(p, c); h.m_damage.m_fire = dmg * 0.4f * pw * w.Instant; Blow(c, h); }
                    if (twice) foreach (Character c in Foes(p, 4f * w.Radius)) { HitData h = Hit(p, c); h.m_damage.m_fire = dmg * 0.4f * pw * w.Instant; Blow(c, h); }
                    break;
                case "frost":
                    foreach (Character c in Foes(p, 5f * w.Radius)) { HitData h = Hit(p, c); h.m_damage.m_frost = dmg * 0.15f * pw * (twice ? 2f : 1f); Blow(c, h); }
                    break;
                case "chain":
                    {
                        List<Character> l = Foes(p, 10f * w.Radius);
                        l.Sort((x, y) => Vector3.Distance(at, x.transform.position).CompareTo(Vector3.Distance(at, y.transform.position)));
                        for (int i = 0; i < l.Count && i < w.Targets; i++) { HitData h = Hit(p, l[i]); h.m_damage.m_lightning = dmg * 0.5f * pw * w.Instant * (twice ? 2f : 1f); Blow(l[i], h); }
                    }
                    break;
                case "poison":
                    foreach (Character c in Foes(p, 4f * w.Radius)) { HitData h = Hit(p, c); h.m_damage.m_poison = dmg * 0.6f * pw * (twice ? 2f : 1f); Blow(c, h); }
                    break;
                case "gust":
                    foreach (Character c in Foes(p, 4f * w.Radius)) { HitData h = Hit(p, c); h.m_damage.m_blunt = dmg * 0.1f * pw * w.Instant; h.m_pushForce = 60f * (twice ? 1.6f : 1f); Blow(c, h); }
                    break;
                case "heal":
                    p.Heal(max * 0.15f * pw * w.Personal * (twice ? 2f : 1f), true);
                    _hotUntil = Time.time + 5f * w.Dur; _hotPerSec = max * 0.10f * pw * w.Personal / (5f * w.Dur);
                    break;
                case "vanish":
                    _vanishUntil = Time.time + 3f * w.Dur * (twice ? 2f : 1f);
                    break;
                case "leech":
                    _leechHits = 3; _leechPct = 0.15f * pw * w.Personal; _leechUntil = Time.time + 8f * w.Dur * (twice ? 2f : 1f);
                    break;
                case "shield":
                    _shield = max * 0.2f * pw * w.Personal; _shieldUntil = Time.time + 6f * w.Dur * (twice ? 2f : 1f);
                    break;
            }
        }

        // ---------------------------------------------------------------- the great powers (a button of Бальдр)
        private static float _lightUntil, _lightTick, _helUntil, _helTick, _shadowStrikeUntil;
        private static float _greatPw = 1f; // the power when it was cast (the night's too): the lasting ones keep it
        private static Vector3 _helAt;

        private static void Great(Player p, string a, float night, bool twice)
        {
            BindPower w = _pow;
            float pw = w.Pow * night, dmg = Weapon(p), max = p.GetMaxHealth(), more = twice ? 1.5f : 1f;
            _greatPw = pw;
            Vector3 at = p.transform.position;
            switch (a)
            {
                case "sun":
                    Burst(at, 3f, 1.5f, null);
                    foreach (Character c in Foes(p, 6f * w.Radius)) { HitData h = Hit(p, c); h.m_damage.m_fire = dmg * 1.2f * pw * w.Instant * more; h.m_staggerMultiplier = 100f; Blow(c, h); }
                    break;
                case "crown":
                    Burst(at, 3.5f, 2f, null);
                    foreach (Character c in Foes(p, 8f * w.Radius)) { HitData h = Hit(p, c); h.m_damage.m_frost = dmg * 0.3f * pw * more; h.m_staggerMultiplier = 100f; Blow(c, h); }
                    break;
                case "hammer":
                    {
                        Character first = Aimed(p, 30f * w.Radius);
                        if (first == null) break;
                        List<Character> l = Foes(p, 40f * w.Radius);
                        Vector3 t0 = first.transform.position;
                        l.Remove(first);
                        l.RemoveAll(c => (c.transform.position - t0).sqrMagnitude > 64f);
                        l.Sort((x, y) => (x.transform.position - t0).sqrMagnitude.CompareTo((y.transform.position - t0).sqrMagnitude));
                        l.Insert(0, first);
                        for (int i = 0; i < l.Count && i <= w.Targets; i++)
                        {
                            HitData h = Hit(p, l[i]); h.m_damage.m_lightning = dmg * 1.0f * pw * w.Instant * more; Blow(l[i], h);
                            Burst(l[i].transform.position, 1.5f, 1f, null);
                        }
                    }
                    break;
                case "light":
                    _lightUntil = Time.time + 8f * w.Dur * more; _lightTick = 0f;
                    Burst(Vector3.zero, 2f, 8f * w.Dur * more, p.transform);
                    break;
                case "hel":
                    _helUntil = Time.time + 6f * w.Dur * more; _helAt = at; _helTick = 0f;
                    Burst(at, 3f, 6f * w.Dur * more, null);
                    break;
                case "shadow":
                    _vanishUntil = Time.time + 5f * w.Dur * more; _shadowStrikeUntil = _vanishUntil + 0.5f;
                    Burst(Vector3.zero, 1.5f, 2f, p.transform);
                    break;
                case "sacrifice":
                    {
                        float cost = Mathf.Min(max * 0.2f, Mathf.Max(0f, p.GetHealth() - max * 0.1f)); // (never down to death)
                        if (cost > 0f) p.UseHealth(cost);
                        _leechHits = 999; _leechPct = 0.25f * pw * w.Personal; _leechUntil = Time.time + 10f * w.Dur * more;
                        Burst(Vector3.zero, 1.5f, 2f, p.transform);
                    }
                    break;
                case "runeshield":
                    _shield = max * 0.3f * pw * w.Personal; _shieldUntil = Time.time + 8f * w.Dur * more;
                    ShieldNear(p, 0.3f * pw * w.Personal, 8f * w.Dur * more, 8f * w.Radius);
                    Burst(Vector3.zero, 2f, 2f, p.transform);
                    break;
            }
        }

        // the foe under the crosshair (within a narrow cone), else the nearest
        private static Character Aimed(Player p, float r)
        {
            Transform cam = GameCamera.instance != null ? GameCamera.instance.transform : p.transform;
            Character best = null, near = null;
            float bestA = 12f, nearD = float.MaxValue;
            foreach (Character c in Foes(p, r))
            {
                Vector3 to = c.GetCenterPoint() - cam.position;
                float ang = Vector3.Angle(cam.forward, to);
                if (ang < bestA) { bestA = ang; best = c; }
                float d = (c.transform.position - p.transform.position).sqrMagnitude;
                if (d < nearD) { nearD = d; near = c; }
            }
            return best ?? near;
        }

        // the great power shown with its rune's own effect: a burst at a place, or on you, for a while
        private static void Burst(Vector3 at, float size, float seconds, Transform follow)
        {
            RavenFxDef d = FxLib.Find(_pow.ActiveFx);
            GameObject tpl = d != null ? FxLib.Template(d.Base) : null;
            if (tpl == null) return;
            GameObject g = follow != null ? UnityEngine.Object.Instantiate(tpl, follow, false) : UnityEngine.Object.Instantiate(tpl, at, Quaternion.identity);
            if (follow != null) g.transform.localPosition = Vector3.up;
            g.hideFlags = HideFlags.None;
            g.transform.localScale = Vector3.one * size;
            WsFx tint = g.AddComponent<WsFx>();
            g.SetActive(true);
            tint.Recolor(d.Tinted, d.Tint);
            UnityEngine.Object.Destroy(g, seconds);
        }

        // the states that last: healing over time, the foes losing you
        private static float _hotTick;
        private static void States(Player p, float dt)
        {
            if (Time.time < _hotUntil && Time.time >= _hotTick) { _hotTick = Time.time + 1f; p.Heal(_hotPerSec, false); }
            if (Time.time < _lightUntil && Time.time >= _lightTick && _pow != null) // Світло Бальдра: you and friends near, every second
            {
                _lightTick = Time.time + 1f;
                float pct = 0.03f * _greatPw * _pow.Personal;
                p.Heal(p.GetMaxHealth() * pct, false);
                foreach (Player o in Player.GetAllPlayers())
                    if (o != null && !ReferenceEquals(o, p) && !o.IsDead() && (o.transform.position - p.transform.position).sqrMagnitude <= 64f * _pow.Radius * _pow.Radius)
                        ZRoutedRpc.instance.InvokeRoutedRPC(o.GetZDOID().UserID, RpcHeal, pct);
            }
            if (Time.time < _helUntil && Time.time >= _helTick && _pow != null) // Подих Хель: poison every second on the field
            {
                _helTick = Time.time + 1f;
                float dmg = Weapon(p) * 1.2f * _greatPw / Mathf.Max(1f, 6f * _pow.Dur);
                foreach (Character c in Foes(p, 60f))
                    if ((c.transform.position - _helAt).sqrMagnitude <= 36f * _pow.Radius * _pow.Radius) { HitData h = Hit(p, c); h.m_damage.m_poison = dmg; Blow(c, h); }
            }
            if (Time.time < _vanishUntil && Time.time >= _vanishLook)
            {
                _vanishLook = Time.time + 0.25f;
                foreach (Character c in Foes(p, 25f))
                {
                    MonsterAI ai = c.GetComponent<MonsterAI>();
                    if (ai == null || !c.IsOwner()) continue;
                    if ((Character)TargetOf.GetValue(ai) == p) { TargetOf.SetValue(ai, null); try { SetAlerted.Invoke(ai, NotAlerted); } catch { } }
                }
            }
        }

        private static float _vanishLook;
        private static readonly object[] NotAlerted = { false };

        private static readonly System.Reflection.FieldInfo TargetOf = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");
        private static readonly System.Reflection.MethodInfo SetAlerted = AccessTools.Method(typeof(BaseAI), "SetAlerted");

        // «рух» (only Геймдаль): faster and lighter on the legs a few seconds
        private static SE_Stats _rushTemplate;
        private const string RushName = "RavenWatch_BindRush";
        private static void Rush(Player p, bool twice)
        {
            SE_Stats se = RavenFx.Template(ref _rushTemplate, RushName);
            se.m_name = Lang.W("Рух в'язі");
            se.m_tooltip = Lang.T("+25% до швидкості, удвічі менше витривалості на біг.");
            se.m_icon = AbilityIcons.ForEffect("bind", _wearer == 1);
            se.m_ttl = 4f * _pow.Dur * (twice ? 2f : 1f);
            se.m_speedModifier = 0.25f * _pow.Pow;
            se.m_runStaminaDrainModifier = -0.5f;
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        // ---------------------------------------------------------------- the hits
        private static float Weapon(Player p)
        {
            ItemDrop.ItemData w = p.GetCurrentWeapon();
            if (w == null && p.m_unarmedWeapon != null) w = p.m_unarmedWeapon.m_itemData;
            float d = w != null ? w.GetDamage().GetTotalDamage() : 0f;
            return Mathf.Max(5f, d);
        }

        private static List<Character> Foes(Player p, float r)
        {
            List<Character> l = new List<Character>();
            Vector3 at = p.transform.position;
            float r2 = r * r;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || ReferenceEquals(c, p) || (c.transform.position - at).sqrMagnitude > r2) continue; // (the distance first: the cheapest)
                if (c.IsDead() || c.IsPlayer() || c.IsTamed() || !BaseAI.IsEnemy(p, c)) continue;
                l.Add(c);
            }
            return l;
        }

        private static HitData Hit(Player p, Character c)
        {
            HitData h = new HitData();
            h.m_point = c.GetCenterPoint();
            Vector3 dir = c.transform.position - p.transform.position; dir.y = 0f;
            h.m_dir = dir.sqrMagnitude > 0.001f ? dir.normalized : p.transform.forward;
            h.SetAttacker(p);
            return h;
        }

        private static void Blow(Character c, HitData h)
        {
            _ours = true;
            try { c.Damage(h); }
            finally { _ours = false; }
        }

        // ---------------------------------------------------------------- healing friends
        internal static void Register()
        {
            ZRoutedRpc.instance.Register<float>(RpcHeal, OnHeal);
            ZRoutedRpc.instance.Register<float, float>(RpcShield, OnShield);
        }

        private static void OnHeal(long sender, float pct)
        {
            Player me = Player.m_localPlayer;
            if (me == null || me.IsDead()) return;
            me.Heal(me.GetMaxHealth() * Mathf.Clamp(pct, 0f, 0.5f), true);
        }

        private const string RpcShield = "RavenWatch_BindShield";

        private static void OnShield(long sender, float pct, float seconds)
        {
            Player me = Player.m_localPlayer;
            if (me == null || me.IsDead()) return;
            _shield = Mathf.Max(_shield, me.GetMaxHealth() * Mathf.Clamp(pct, 0f, 0.6f));
            _shieldUntil = Mathf.Max(_shieldUntil, Time.time + Mathf.Clamp(seconds, 0f, 20f));
        }

        private static void ShieldNear(Player p, float pct, float seconds, float r)
        {
            foreach (Player o in Player.GetAllPlayers())
                if (o != null && !ReferenceEquals(o, p) && !o.IsDead() && (o.transform.position - p.transform.position).sqrMagnitude <= r * r)
                    ZRoutedRpc.instance.InvokeRoutedRPC(o.GetZDOID().UserID, RpcShield, pct, seconds);
        }

        private static void HealNear(Player p, float pct)
        {
            p.Heal(p.GetMaxHealth() * pct, true);
            foreach (Player o in Player.GetAllPlayers())
                if (o != null && o != p && !o.IsDead() && Vector3.Distance(o.transform.position, p.transform.position) <= 8f)
                    ZRoutedRpc.instance.InvokeRoutedRPC(o.GetZDOID().UserID, RpcHeal, pct);
        }

        // ---------------------------------------------------------------- from the patches
        private static float _heavyAt;
        internal static void OnOutgoing(Player me, Character target, HitData hit)
        {
            if (_ours || _pow == null || !Active || target == null || target.IsPlayer() || target.IsTamed() || !BaseAI.IsEnemy(me, target)) return;
            if (hit.m_skillRaiseAmount <= Strike.Marker) return; // (a raven's blow)
            bool heavy = IsSecondary(me) || hit.m_skill == Skills.SkillType.Bows || hit.m_skill == Skills.SkillType.Crossbows;
            if (heavy && Time.time - _heavyAt > 0.6f) { _heavyAt = Time.time; Trigger(me, "heavy"); }
            if (_shadowStrikeUntil > 0f && Time.time < _shadowStrikeUntil && hit.GetTotalDamage() > 0f) { _shadowStrikeUntil = 0f; hit.m_damage.Modify(2f); } // Тінь Одіна: the first blow from the shadow
            BaseAI ai = target.GetBaseAI();
            if (ai != null && !ai.IsAlerted() && hit.m_backstabBonus > 1f) Trigger(me, "sneak");
            if (_leechHits > 0 && Time.time < _leechUntil && hit.GetTotalDamage() > 0f) { _leechHits--; me.Heal(hit.GetTotalDamage() * _leechPct, true); }
        }

        private static readonly System.Reflection.FieldInfo SecondaryOf = AccessTools.Field(typeof(Humanoid), "m_currentAttackIsSecondary");
        private static bool IsSecondary(Player p) { try { return (bool)SecondaryOf.GetValue(p); } catch { return false; } }

        internal static void OnKill(Player me) { if (_pow != null && Active) Trigger(me, "kill"); }

        internal static void OnParry(Player me) { if (_pow != null && Active) Trigger(me, "parry"); }

        // the shield takes what it can of a blow
        internal static void Absorb(HitData hit)
        {
            if (Time.time < _lightUntil && hit.GetAttacker() != null && !hit.GetAttacker().IsPlayer()) hit.m_damage.Modify(0.8f); // Світло Бальдра
            if (_shield <= 0f || Time.time > _shieldUntil) { _shield = 0f; return; }
            float total = hit.GetTotalDamage();
            if (total <= 0f) return;
            float take = Mathf.Min(_shield, total);
            _shield -= take;
            hit.m_damage.Modify((total - take) / total);
        }

        // ---------------------------------------------------------------- the badge (the status bar)
        private static SE_RavenBind _badge;
        private static readonly int BadgeHash = SE_RavenBind.Id.GetStableHashCode();

        private static bool _badgeShown, _badgeStale = true;
        private static Player _badgeFor;
        private static float _badgeLook;

        // (looked at the status bar only when it should change, and every 2 s: the game may clear it, a death)
        private static void Badge(Player p, bool show)
        {
            if (!_badgeStale && ReferenceEquals(p, _badgeFor) && show == _badgeShown && Time.time < _badgeLook) return;
            _badgeLook = Time.time + 2f; _badgeFor = p; _badgeShown = show;
            SEMan se = p.GetSEMan();
            bool has = se.HaveStatusEffect(BadgeHash);
            if (_badgeStale && has) { se.RemoveStatusEffect(BadgeHash, true); has = false; } // (another в'язь: its words anew)
            _badgeStale = false;
            if (show == has) return;
            if (!show) { se.RemoveStatusEffect(BadgeHash, true); return; }
            if (_badge == null) { _badge = ScriptableObject.CreateInstance<SE_RavenBind>(); _badge.name = SE_RavenBind.Id; _badge.hideFlags = HideFlags.HideAndDontSave; }
            _badge.m_name = Lang.W("В'язь");
            _badge.m_tooltip = _plain;
            _badge.m_icon = AbilityIcons.ForEffect("bind", _wearer == 1);
            _badge.m_ttl = 0f;
            se.AddStatusEffect(_badge, true, 0, 0f, 0);
        }

        // asked by the status bar every frame: made anew only when what it says changes
        private static int _badgeKey = -1;
        private static string _badgeText = "";
        internal static string BadgeText()
        {
            if (_pow == null) return "";
            int next = _spent.Count > 0 ? Mathf.CeilToInt(NextIn) : 0;
            int key = (_on ? 1 : 0) + Charges * 2 + _pow.Charges * 32 + next * 1024;
            if (key == _badgeKey) return _badgeText;
            _badgeKey = key;
            if (!_on) _badgeText = Lang.T("вимк.");
            else _badgeText = Charges + "/" + _pow.Charges + (next > 0 ? " " + next + Lang.T("с") : "");
            return _badgeText;
        }

        private static string StripTags(string s) { return System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", ""); }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven bind                         what you have, charges
        // raven bind on|off · press · fill   the switch · the button · all charges back
        // raven bind make <tree> <rune> …    weave without the arena (tree: green|icy|charred); unweave: raven bind clear
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            RuneBook.Data d = RuneBook.Of(p);
            switch (what)
            {
                case "on": SetOn(p, true); return "В'язь увімкнено.";
                case "off": SetOn(p, false); return "В'язь вимкнено.";
                case "press": { string why = Press(p); return why ?? "Спрацювала."; }
                case "fill": _spent.Clear(); _dirty = true; Version++; return "Заряди відновлено.";
                case "clear": RuneBook.Unweave(p); return "В'язь розплетено.";
                case "wear": { bool m = a.Length > 2 && a[2].ToLowerInvariant().StartsWith("m"); string why = RuneBook.PutBind(p, m); return why ?? "В'язь на " + Plugin.RavenName(m) + "."; }
                case "make":
                    {
                        if (a.Length < 4) return "raven bind make green|icy|charred <rune> <rune> [...]";
                        string t = a[2].ToLowerInvariant();
                        int tree = t.StartsWith("g") || t.StartsWith("з") ? 1 : t.StartsWith("i") || t.StartsWith("к") || t.StartsWith("м") ? 0 : 2;
                        List<string> runes = new List<string>();
                        for (int i = 3; i < a.Length; i++) { string r = a[i].ToLowerInvariant(); if (!d.Has(r)) return "Руна «" + r + "» не горить (raven runes)."; runes.Add(r); }
                        if (d.Bind.Length > 0) RuneBook.Unweave(p);
                        Bind b = Bind.From(RuneBook.Of(p), runes, tree);
                        string no = b.Problem();
                        if (no != null) return no;
                        RuneBook.SetBind(p, b, runes);
                        RuneBook.PutBind(p, Trust.Worn(false) == null);
                        return "Сплетено: " + b.Id() + "\n" + StripTags(BindPower.Of(b).Sentence());
                    }
            }
            if (d.Bind.Length == 0) return "В'язі нема. raven bind make green|icy|charred <rune> <rune> …";
            BindPower w = BindPower.Of(Bind.Parse(d.Bind));
            int on = RuneBook.BindOn(p);
            return d.Bind + " (руни " + string.Join(", ", d.BindRunes.ToArray()) + ")\n" + StripTags(w.Sentence())
                + "\nЗаряди " + Charges + "/" + w.Charges + (NextIn > 0f ? ", наступний за " + Mathf.CeilToInt(NextIn) + " с" : "") + (_on ? "" : ", ВИМКНЕНО")
                + "\nНосить: " + (on == 1 ? "Хугін" : on == 2 ? "Мунін" : "ніхто") + (Active ? "" : " (не на плечі — не діє)")
                + "\nraven bind on|off|press|fill|clear|wear [hugin|munin]|make";
        }
    }

    // the в'язь's place in the status bar: its charges and the next one's time
    internal sealed class SE_RavenBind : StatusEffect
    {
        internal const string Id = "RavenWatch_Bind";
        public override string GetIconText() { return BindRun.BadgeText(); }
    }

    // ---------------------------------------------------------------- the patches
    [HarmonyPatch(typeof(Player), "Update")]
    internal static class BindTickPatch
    {
        private static void Postfix(Player __instance)
        { try {
            if (!ReferenceEquals(__instance, Player.m_localPlayer) || __instance == null) return; // (every player, every frame: yours only)
            BindRun.Tick(__instance, Time.deltaTime);
            RuneStars.Tick(__instance);
            BindArena.Tick(__instance, Time.deltaTime);
            BindArena.RingsTick();
        } catch (Exception e) { PatchGuard.Fail("BindTickPatch", e); } }
    }

    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class BindOutgoingPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (BindRun.Power == null || !BindRun.Active) return; // (every hit of this game passes here: nothing to do without a в'язь)
            Player me = Player.m_localPlayer;
            if (me == null || hit == null || ReferenceEquals(__instance, me) || hit.m_attacker != me.GetZDOID()) return;
            BindRun.OnOutgoing(me, __instance, hit);
        } catch (Exception e) { PatchGuard.Fail("BindOutgoingPatch", e); } }
    }

    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class BindShieldPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (!BindRun.Shielded || hit == null || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
            BindRun.Absorb(hit);
        } catch (Exception e) { PatchGuard.Fail("BindShieldPatch", e); } }
    }

    // a parry: the game's own test (a timed block within a quarter second)
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class BindParryPatch
    {
        private static readonly System.Reflection.FieldInfo BlockTimer = AccessTools.Field(typeof(Humanoid), "m_blockTimer");
        private static readonly System.Reflection.MethodInfo Blocker = AccessTools.Method(typeof(Humanoid), "GetCurrentBlocker");

        private static void Prefix(Humanoid __instance, ref bool __state)
        {
            __state = false;
            try {
                if (BindRun.Power == null || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
                ItemDrop.ItemData b = (ItemDrop.ItemData)Blocker.Invoke(__instance, null);
                float t = (float)BlockTimer.GetValue(__instance);
                __state = b != null && b.m_shared.m_timedBlockBonus > 1f && t != -1f && t < 0.25f;
            } catch (Exception e) { PatchGuard.Fail("BindParryPatch", e); }
        }

        private static void Postfix(Humanoid __instance, bool __result, bool __state)
        { try {
            if (__state && __result) BindRun.OnParry((Player)__instance);
        } catch (Exception e) { PatchGuard.Fail("BindParryPatch", e); } }
    }
}
#endif
