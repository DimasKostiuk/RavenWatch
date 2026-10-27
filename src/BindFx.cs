#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What a в'язь looks like at work. Every part of it shows itself in its own element, so the player reads it at a
    // glance: fire flares up in a ring, frost leaves snow and mist, a discharge runs from foe to foe, healing rises in
    // green sparks, a shield is a shell of golden runes round you (it flickers when hit, shatters when broken), a stolen
    // life flies back to you as a red streak, the foes losing you see you wrapped in smoke.
    //   - a moment (Cast, Nova, Bolt, Strike, Drain, Heal, ShieldHit, Rune): made, played, gone by itself;
    //   - a state (shield, healing, smoke, blood thirst, rush): hangs on the player while BindRun says it lasts —
    //     the watcher compares what BindRun has with what it had, so no code path has to remember to switch it off.
    // Every game near you sees the same (one RPC per moment / state change); the status bar shows yours with its time
    // (the shield with what it still takes).
    internal static class BindFx
    {
        internal enum Ev { Cast, Nova, Bolt, Strike, Drain, Heal, ShieldHit, ShieldBreak, StateOn, StateOff, Rune }
        internal const int States = 5; // 0 shield, 1 healing, 2 smoke, 3 blood thirst, 4 rush
        internal static readonly string[] StateIds = { "shield", "heal", "vanish", "leech", "rush" };

        private const string Rpc = "RavenWatch_BindFx";
        private const float SeenRange = 100f;

        // ---------------------------------------------------------------- the look of each part
        internal enum Style { Fire, Ice, Lightning, Life, Poison, Shadow, Blood, Runes, Wind }

        internal static Style StyleOf(string action)
        {
            switch (action)
            {
                case "flash": case "sun": return Style.Fire;
                case "frost": case "crown": return Style.Ice;
                case "chain": case "hammer": return Style.Lightning;
                case "heal": case "light": return Style.Life;
                case "poison": case "hel": return Style.Poison;
                case "vanish": case "shadow": return Style.Shadow;
                case "leech": case "sacrifice": return Style.Blood;
                case "shield": case "runeshield": return Style.Runes;
            }
            return Style.Wind;
        }

        // bright for the glow, deep for smoke and mist
        private static readonly Color[] Bright =
        {
            new Color(1f, 0.55f, 0.18f), new Color(0.7f, 0.9f, 1f), new Color(0.78f, 0.7f, 1f), new Color(0.6f, 1f, 0.45f),
            new Color(0.62f, 0.85f, 0.2f), new Color(0.6f, 0.58f, 0.7f), new Color(0.95f, 0.15f, 0.12f), new Color(1f, 0.8f, 0.35f),
            new Color(0.88f, 0.94f, 0.95f),
        };
        private static readonly Color[] Deep =
        {
            new Color(0.75f, 0.2f, 0.05f, 0.7f), new Color(0.75f, 0.88f, 1f, 0.45f), new Color(0.35f, 0.3f, 0.6f, 0.5f), new Color(0.3f, 0.6f, 0.25f, 0.5f),
            new Color(0.4f, 0.55f, 0.12f, 0.6f), new Color(0.1f, 0.1f, 0.13f, 0.75f), new Color(0.45f, 0.03f, 0.05f, 0.6f), new Color(0.6f, 0.45f, 0.15f, 0.45f),
            new Color(0.8f, 0.84f, 0.86f, 0.35f),
        };

        // the part the в'язь is known by: the button's great power, else its first «what», else (rush) the wind
        internal static string MainAction(BindPower w)
        {
            if (w == null) return "gust";
            if (w.Mode == BindMode.Button && w.Active.Length > 0) return w.Active;
            if (w.Mode == BindMode.Motion || w.What.Count == 0) return "gust";
            return w.What[0];
        }

        // ---------------------------------------------------------------- what BindRun calls (yours: played here + sent)
        internal static void Cast(Player p, string action) { Send(p, Ev.Cast, action, Vector3.zero, 0f, 0f, null); }
        internal static void Nova(Player p, string action, Vector3 at, float radius, float seconds) { Send(p, Ev.Nova, action, at, radius, seconds, null); }
        internal static void Bolt(Player p, List<Vector3> targets) { if (targets.Count > 0) Send(p, Ev.Bolt, "chain", Vector3.zero, 0f, 0f, targets); }
        internal static void Strike(Player p, List<Vector3> targets) { if (targets.Count > 0) Send(p, Ev.Strike, "hammer", Vector3.zero, 0f, 0f, targets); }
        internal static void Drain(Player p, Vector3 from) { Send(p, Ev.Drain, "leech", from, 0f, 0f, null); }
        internal static void Rune(Player p, string fx, Vector3 at, float size, float seconds, bool follow) { Send(p, Ev.Rune, fx, at, size, follow ? -seconds : seconds, null); }

        private static float _healShown = -10f;
        internal static void Heal(Player p)
        {
            if (Time.time - _healShown < 2.5f) return; // (Світло Бальдра heals every second: one rise is enough)
            _healShown = Time.time;
            Send(p, Ev.Heal, "heal", Vector3.zero, 0f, 0f, null);
        }

        private static float _hitShown = -10f;
        internal static void ShieldHit(Player p, Vector3 point, bool broken)
        {
            if (broken) { Send(p, Ev.ShieldBreak, "shield", point, 0f, 0f, null); return; }
            if (Time.time - _hitShown < 0.2f) return;
            _hitShown = Time.time;
            Send(p, Ev.ShieldHit, "shield", point, BindRun.ShieldFrac, 0f, null); // (others see it thin too)
        }

        // ---------------------------------------------------------------- the watcher (every frame, yours)
        private static readonly bool[] _was = new bool[States];
        private static readonly float[] _until = new float[States];
        private static Player _for;
        private static BindAura _aura;
        private static int _chargesWas = -1;

        internal static void Watch(Player p)
        {
            if (!ReferenceEquals(p, _for)) { _for = p; for (int i = 0; i < States; i++) { _was[i] = false; _until[i] = 0f; } _chargesWas = -1; }
            for (int i = 0; i < States; i++)
            {
                float left = BindRun.StateLeft(i);
                bool on = left > 0f && !p.IsDead();
                float until = Time.time + left;
                if (on && (!_was[i] || until > _until[i] + 0.5f)) // (begun, or made longer: a new shield over the old one)
                {
                    Send(p, Ev.StateOn, StateIds[i], Vector3.zero, left, 0f, null);
                    Badge(p, i, left);
                }
                else if (!on && _was[i])
                {
                    Send(p, Ev.StateOff, StateIds[i], Vector3.zero, 0f, 0f, null);
                    Badge(p, i, 0f);
                }
                _was[i] = on;
                _until[i] = on ? until : 0f;
            }
            if (_was[0])
            {
                if (_aura == null || _aura.Who != p) _aura = p.GetComponent<BindAura>();
                if (_aura != null) _aura.ShieldFrac = BindRun.ShieldFrac;
            }
            int ch = BindRun.Active && BindRun.On ? BindRun.Charges : -1;
            if (ch > _chargesWas && _chargesWas >= 0 && ch > 0) Ready(p); // a charge came back: a small glint only you see
            _chargesWas = ch;
        }

        // ---------------------------------------------------------------- the status bar: your states with their time
        private static readonly SE_RavenBindState[] _badges = new SE_RavenBindState[States];
        private static readonly string[] BadgeNames = Lang.Keys("Щит в'язі", "Лікування в'язі", "Морок в'язі", "Спрага в'язі", "");
        private static readonly string[] BadgeTips = Lang.Keys("Щит забирає на себе удари, доки не трісне.", "Здоров'я повертається щосекунди.",
            "Вороги довкола тебе не бачать.", "Твої удари лікують тебе.", "");
        private static readonly string[] BadgeIcons = { "ward", "heal", "shadow", "bloodfury", "" };
        private static readonly string[] BadgeIds = { "RavenWatch_BindState_shield", "RavenWatch_BindState_heal", "RavenWatch_BindState_vanish", "RavenWatch_BindState_leech", "" };
        private static readonly int[] BadgeHashes = { BadgeIds[0].GetStableHashCode(), BadgeIds[1].GetStableHashCode(), BadgeIds[2].GetStableHashCode(), BadgeIds[3].GetStableHashCode(), 0 };

        private static void Badge(Player p, int i, float seconds)
        {
            if (BadgeNames[i].Length == 0) return; // (the rush has its own)
            SEMan se = p.GetSEMan();
            int hash = BadgeHashes[i];
            if (se.HaveStatusEffect(hash)) se.RemoveStatusEffect(hash, true); // (made longer: its new time, not the old one reset)
            if (seconds <= 0f) return;
            SE_RavenBindState b = _badges[i];
            if (b == null) { b = _badges[i] = ScriptableObject.CreateInstance<SE_RavenBindState>(); b.name = BadgeIds[i]; b.hideFlags = HideFlags.HideAndDontSave; b.Kind = i; }
            b.m_name = Lang.W(BadgeNames[i]);
            b.m_tooltip = Lang.T(BadgeTips[i]);
            b.m_icon = AbilityIcons.ForEffect(BadgeIcons[i], false);
            b.m_ttl = seconds;
            se.AddStatusEffect(b, true, 0, 0f, 0);
        }

        // ---------------------------------------------------------------- the net
        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnRpc); }

        private static void Send(Player p, Ev ev, string what, Vector3 at, float a, float b, List<Vector3> pts)
        {
            if (p == null) return;
            try { Play(p, ev, what, at, a, b, pts); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Bind fx " + ev + ": " + e.Message); }
            if (ZRoutedRpc.instance == null || !OthersNear(p)) return;
            ZPackage pkg = new ZPackage();
            pkg.Write((int)ev); pkg.Write(what ?? ""); pkg.Write(at); pkg.Write(a); pkg.Write(b);
            int n = pts == null ? 0 : Mathf.Min(pts.Count, 12);
            pkg.Write(n);
            for (int i = 0; i < n; i++) pkg.Write(pts[i]);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        // a game sending more than a в'язь ever makes (a series is 2 s apart; a sacrifice's blows, states) is not drawn
        private const float Near = 60f;
        private const int PerSecond = 25;
        private static readonly Dictionary<long, KeyValuePair<float, int>> Heard = new Dictionary<long, KeyValuePair<float, int>>();

        private static bool TooMany(long sender)
        {
            KeyValuePair<float, int> h;
            if (!Heard.TryGetValue(sender, out h) || Time.time - h.Key >= 1f) h = new KeyValuePair<float, int>(Time.time, 0);
            h = new KeyValuePair<float, int>(h.Key, h.Value + 1);
            Heard[sender] = h;
            if (Heard.Count > 64) Heard.Clear(); // (players come and go: never a growing list)
            return h.Value > PerSecond;
        }

        private static bool OthersNear(Player p)
        {
            foreach (Player o in Player.GetAllPlayers())
                if (o != null && !ReferenceEquals(o, p) && (o.transform.position - p.transform.position).sqrMagnitude <= SeenRange * SeenRange) return true;
            return false;
        }

        private static void OnRpc(long sender, ZPackage pkg)
        {
            try
            {
                Player me = Player.m_localPlayer;
                if (me == null || sender == ZDOMan.GetSessionID()) return;
                Player who = null;
                foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetZDOID().UserID == sender) { who = o; break; }
                if (who == null || (who.transform.position - me.transform.position).sqrMagnitude > SeenRange * SeenRange) return;
                if (TooMany(sender)) return;
                Ev ev = (Ev)pkg.ReadInt();
                string what = pkg.ReadString();
                Vector3 at = pkg.ReadVector3();
                float a = Mathf.Clamp(pkg.ReadSingle(), 0f, 120f), b = Mathf.Clamp(pkg.ReadSingle(), -30f, 30f);
                int n = Mathf.Clamp(pkg.ReadInt(), 0, 12);
                List<Vector3> pts = new List<Vector3>(n);
                Vector3 from = who.transform.position;
                for (int i = 0; i < n; i++) { Vector3 v = pkg.ReadVector3(); if ((v - from).sqrMagnitude <= Near * Near) pts.Add(v); } // (a place far from its caster: not believed)
                if ((at - from).sqrMagnitude > Near * Near) at = from;
                Play(who, ev, what, at, a, b, pts);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Bind fx from the net: " + e.Message); }
        }

        // ---------------------------------------------------------------- playing it (on whoever cast it)
        private static void Play(Player who, Ev ev, string what, Vector3 at, float a, float b, List<Vector3> pts)
        {
            switch (ev)
            {
                case Ev.Cast: PlayCast(who, StyleOf(what)); break;
                case Ev.Nova: PlayNova(at, StyleOf(what), Mathf.Clamp(a, 0.5f, 20f), Mathf.Clamp(b, 0f, 30f)); break; // (a long Hel field: its clouds as long as it)
                case Ev.Bolt: { List<Vector3> l = new List<Vector3> { who.GetCenterPoint() }; l.AddRange(pts); PlayBolt(l, Style.Lightning, 0.35f); foreach (Vector3 t in pts) Sparks(t, Style.Lightning, 14); } break;
                case Ev.Strike:
                    foreach (Vector3 t in pts)
                    {
                        Vector3 sky = t + new Vector3(UnityEngine.Random.Range(-2f, 2f), 18f, UnityEngine.Random.Range(-2f, 2f));
                        PlayBolt(new List<Vector3> { sky, t }, Style.Lightning, 0.45f);
                        Sparks(t, Style.Lightning, 22); // (its light: the bolt's own)
                    }
                    break;
                case Ev.Drain: PlayDrain(at, who, Style.Blood); break;
                case Ev.Heal: PlayHeal(who); break;
                case Ev.ShieldHit: PlayShieldHit(who, at, a); break;
                case Ev.ShieldBreak: PlayShieldBreak(who); break;
                case Ev.StateOn:
                    {
                        int i = Array.IndexOf(StateIds, what);
                        BindAura au = Aura(who);
                        if (i == 0) au.ShieldFrac = 1f; // (begun or made longer: a new shield, full)
                        au.On(i, Mathf.Clamp(a, 0.5f, 120f));
                    }
                    break;
                case Ev.StateOff: { BindAura au = who.GetComponent<BindAura>(); if (au != null) au.Off(Array.IndexOf(StateIds, what)); } break;
                case Ev.Rune: PlayRune(who, what, at, a, b); break;
            }
        }

        internal static BindAura Aura(Player who)
        {
            BindAura a = who.GetComponent<BindAura>();
            if (a == null) { a = who.gameObject.AddComponent<BindAura>(); a.Who = who; }
            return a;
        }

        // ---------------------------------------------------------------- moments
        // the в'язь spoke: a ring of runes rising round your waist, a quick shock ring in its colour
        private static void PlayCast(Player who, Style s)
        {
            GameObject root = Root("cast", who.transform.position + Vector3.up * 1f);
            ParticleSystem r = Sys(root.transform, "runes", Runes(), true);
            if (r != null)
            {
                Sheet(r, 4);
                ParticleSystem.MainModule m = r.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.2f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.24f); m.startColor = Pair(s); m.gravityModifier = -0.12f;
                Disc(r, 0.75f, true);
                ParticleSystem.VelocityOverLifetimeModule v = r.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
                v.orbitalX = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f); v.orbitalY = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); v.orbitalZ = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
                Spin(r, 0.35f, 0.6f);
                Drift(r, 0.08f, 0.7f);
                Burst(r, 10);
            }
            ParticleSystem g = Sys(root.transform, "ring", Glow(), true);
            if (g != null)
            {
                ParticleSystem.MainModule m = g.main;
                m.startLifetime = 0.3f; m.startSpeed = 3.5f; m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.16f); m.startColor = Pair(s);
                Disc(g, 0.2f, true);
                Burst(g, 36);
            }
            UnityEngine.Object.Destroy(root, 2f);
        }

        // a wave over the ground in its element: the ring of glow running out to the edge, and what the element leaves
        private static void PlayNova(Vector3 at, Style s, float radius, float seconds)
        {
            GameObject root = Root("nova_" + s, at + Vector3.up * 0.3f);
            ParticleSystem ring = Sys(root.transform, "ring", Glow(), true);
            if (ring != null)
            {
                ParticleSystem.MainModule m = ring.main;
                m.startLifetime = 0.45f; m.startSpeed = radius / 0.45f; m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); m.startColor = Pair(s);
                Disc(ring, 0.3f, true);
                Shrink(ring, 0.4f);
                Burst(ring, Mathf.Clamp(Mathf.RoundToInt(radius * 18f), 30, 160));
            }
            float life = 2.5f;
            switch (s)
            {
                case Style.Fire:
                    {
                        ParticleSystem f = Sys(root.transform, "flames", Glow(), true);
                        if (f != null)
                        {
                            ParticleSystem.MainModule m = f.main;
                            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                            m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.65f); m.gravityModifier = -0.45f;
                            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.4f, 0.1f));
                            Disc(f, radius * 0.85f, false);
                            Fade(f, new Color(1f, 0.9f, 0.5f), new Color(0.8f, 0.15f, 0.05f));
                            Shrink(f, 0.2f);
                            Burst(f, Mathf.Clamp(Mathf.RoundToInt(radius * 12f), 20, 120));
                        }
                        Flash(at + Vector3.up, s, 3.5f, radius * 1.6f, 0.45f);
                    }
                    break;
                case Style.Ice:
                    {
                        ParticleSystem sn = Sys(root.transform, "snow", Glow(), true);
                        if (sn != null)
                        {
                            ParticleSystem.MainModule m = sn.main;
                            m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                            m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f); m.gravityModifier = 0.35f;
                            m.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.7f, 0.88f, 1f));
                            Disc(sn, radius, false);
                            ParticleSystem.ShapeModule sh = sn.shape; sh.randomDirectionAmount = 0.6f;
                            Burst(sn, Mathf.Clamp(Mathf.RoundToInt(radius * 14f), 30, 140));
                        }
                        Mist(root.transform, s, radius, Mathf.Clamp(Mathf.RoundToInt(radius * 3f), 6, 30), 1.6f, 0.8f, 1.5f);
                        Flash(at + Vector3.up, s, 2.5f, radius * 1.5f, 0.5f);
                    }
                    break;
                case Style.Poison:
                    {
                        Mist(root.transform, s, radius, Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 8, 40), 2.2f, 1f, 1.8f, seconds); // (a field that lasts, Подих Хель: the clouds keep rising over it)
                        if (seconds > 0f) life = seconds + 3f;
                    }
                    break;
                case Style.Wind:
                    {
                        ParticleSystem fe = Sys(root.transform, "feathers", Feathers.Feather(), true);
                        if (fe != null)
                        {
                            Sheet(fe, 2);
                            ParticleSystem.MainModule m = fe.main;
                            m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f); m.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.4f);
                            m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f); m.startColor = Pair(s);
                            Spin(fe, 3f, 2.5f);
                            Disc(fe, 0.6f, true);
                            ParticleSystem.VelocityOverLifetimeModule v = fe.velocityOverLifetime; v.enabled = true; v.orbitalY = new ParticleSystem.MinMaxCurve(4f); v.y = new ParticleSystem.MinMaxCurve(0.6f);
                            Burst(fe, Mathf.Clamp(Mathf.RoundToInt(radius * 6f), 12, 50));
                        }
                        Mist(root.transform, s, radius * 0.7f, 10, 0.9f, 0.6f, 1.1f);
                    }
                    break;
                case Style.Shadow: Mist(root.transform, s, 0.7f, 20, 1.4f, 0.6f, 1.2f); break;
                case Style.Blood:
                    {
                        ParticleSystem d = Sys(root.transform, "drops", Glow(), true);
                        if (d != null)
                        {
                            ParticleSystem.MainModule m = d.main;
                            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f); m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                            m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f); m.startColor = Pair(s); m.gravityModifier = 1.2f;
                            ParticleSystem.ShapeModule sh = d.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.3f; sh.rotation = new Vector3(-90f, 0f, 0f);
                            Burst(d, 26);
                        }
                        Mist(root.transform, s, 0.6f, 8, 1f, 0.5f, 0.9f);
                    }
                    break;
                default: Sparks(at + Vector3.up, s, 20); break;
            }
            UnityEngine.Object.Destroy(root, life);
        }

        // a short burst of glowing sparks at a point
        private static void Sparks(Vector3 at, Style s, int n)
        {
            GameObject root = Root("sparks_" + s, at);
            ParticleSystem g = Sys(root.transform, "sparks", Glow(), true);
            if (g != null)
            {
                ParticleSystem.MainModule m = g.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); m.startColor = Pair(s);
                ParticleSystem.ShapeModule sh = g.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
                Burst(g, n);
            }
            UnityEngine.Object.Destroy(root, 1f);
        }

        // a jagged bolt through the points (flickers a few times, then fades)
        private static void PlayBolt(List<Vector3> pts, Style s, float life)
        {
            GameObject g = new GameObject("RavenWatch_BindFx_bolt");
            BindBolt b = g.AddComponent<BindBolt>();
            b.Pts = pts.ToArray(); b.Color = Bright[(int)s]; b.Life = life;
            Flash(pts[pts.Count - 1] + Vector3.up * 0.5f, s, 3f, 5f, life);
        }

        // a life taken: a red streak flies from the foe back into you
        private static void PlayDrain(Vector3 from, Player to, Style s)
        {
            GameObject tpl = FxLib.Template(FxLib.OursPrefix + "streak");
            if (tpl == null) return;
            GameObject g = UnityEngine.Object.Instantiate(tpl, from, Quaternion.identity);
            g.name = "RavenWatch_BindFx_drain"; g.hideFlags = HideFlags.None;
            WsFx tint = g.AddComponent<WsFx>();
            g.SetActive(true);
            tint.Recolor(true, Bright[(int)s]);
            BindHoming h = g.AddComponent<BindHoming>();
            h.From = from; h.To = to.transform;
        }

        // healing: green sparks and a few runes rising from your feet
        private static void PlayHeal(Player who)
        {
            GameObject root = Root("heal", who.transform.position + Vector3.up * 0.1f);
            root.transform.SetParent(who.transform, true);
            ParticleSystem g = Sys(root.transform, "sparks", Glow(), true);
            if (g != null)
            {
                ParticleSystem.MainModule m = g.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 1f, 0.45f), new Color(0.9f, 1f, 0.6f));
                m.gravityModifier = -0.55f;
                Disc(g, 0.5f, true);
                Drift(g, 0.18f, 0.9f);
                Burst(g, 28);
            }
            ParticleSystem r = Sys(root.transform, "runes", Runes(), true);
            if (r != null)
            {
                Sheet(r, 4);
                ParticleSystem.MainModule m = r.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.4f); m.startSpeed = 0.1f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.2f); m.startColor = Pair(Style.Life); m.gravityModifier = -0.3f;
                Disc(r, 0.45f, false);
                Spin(r, 0.3f, 0.5f);
                Drift(r, 0.1f, 0.6f);
                Burst(r, 6);
            }
            Flash(who.transform.position + Vector3.up, Style.Life, 1.5f, 3f, 0.6f);
            UnityEngine.Object.Destroy(root, 2.5f);
        }

        // a blow on the shield: sparks where it struck, the shell flares
        private static void PlayShieldHit(Player who, Vector3 point, float left)
        {
            Vector3 mid = who.GetCenterPoint();
            Vector3 on = (point - mid).sqrMagnitude > 0.01f && (point - mid).sqrMagnitude < 9f ? mid + (point - mid).normalized * 0.9f : mid;
            Sparks(on, Style.Runes, 16);
            BindAura a = who.GetComponent<BindAura>();
            if (a != null) { a.ShieldFrac = Mathf.Clamp01(left); a.Struck(); }
        }

        // the shield gave way: its runes fly apart and fall
        private static void PlayShieldBreak(Player who)
        {
            Vector3 mid = who.GetCenterPoint();
            GameObject root = Root("shield_break", mid);
            ParticleSystem r = Sys(root.transform, "runes", Runes(), true);
            if (r != null)
            {
                Sheet(r, 4);
                ParticleSystem.MainModule m = r.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f); m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.22f); m.startColor = Pair(Style.Runes); m.gravityModifier = 0.9f;
                ParticleSystem.ShapeModule sh = r.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.9f; sh.radiusThickness = 0f;
                Spin(r, 3f, 4f);
                Burst(r, 24);
            }
            Sparks(mid, Style.Runes, 30);
            Flash(mid, Style.Runes, 3f, 4f, 0.4f);
            UnityEngine.Object.Destroy(root, 2f);
        }

        // a great power shown with its rune's own effect (seconds < 0: on the caster, following)
        private static void PlayRune(Player who, string id, Vector3 at, float size, float seconds)
        {
            RavenFxDef d = FxLib.Find(id);
            GameObject tpl = d != null ? FxLib.Template(d.Base) : null;
            if (tpl == null) return;
            bool follow = seconds < 0f;
            GameObject g = follow ? UnityEngine.Object.Instantiate(tpl, who.transform, false) : UnityEngine.Object.Instantiate(tpl, at, Quaternion.identity);
            if (follow) g.transform.localPosition = Vector3.up;
            g.hideFlags = HideFlags.None;
            g.transform.localScale = Vector3.one * Mathf.Clamp(size, 0.2f, 6f);
            if (follow) BindSway.On(g.transform, 0.6f, 0f); // (it keeps up with you a beat late)
            WsFx tint = g.AddComponent<WsFx>();
            g.SetActive(true);
            tint.Recolor(d.Tinted, d.Tint);
            UnityEngine.Object.Destroy(g, Mathf.Clamp(Mathf.Abs(seconds), 0.5f, 30f));
        }

        // a charge back (only yours): a few sparks of the в'язь's colour at your chest
        private static void Ready(Player p)
        {
            Style s = StyleOf(MainAction(BindRun.Power));
            GameObject root = Root("ready", p.GetCenterPoint());
            root.transform.SetParent(p.transform, true);
            ParticleSystem g = Sys(root.transform, "glint", Glow(), false);
            if (g != null)
            {
                ParticleSystem.MainModule m = g.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f); m.startColor = Pair(s);
                ParticleSystem.ShapeModule sh = g.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
                Burst(g, 12);
            }
            UnityEngine.Object.Destroy(root, 1f);
        }

        // ---------------------------------------------------------------- the pieces
        internal static Color BrightOf(Style s) { return Bright[(int)s]; }
        internal static Color DeepOf(Style s) { return Deep[(int)s]; }
        internal static ParticleSystem.MinMaxGradient Pair(Style s) { Color c = Bright[(int)s]; return new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.35f)); }

        internal static Material Glow() { return FxLib.Glow(true); }
        internal static Material Runes() { return DomeLook.RuneMaterialPublic(); }

        internal static GameObject Root(string name, Vector3 at)
        {
            GameObject g = new GameObject("RavenWatch_BindFx_" + name);
            g.transform.position = at;
            return g;
        }

        // a stopped system of ours on a material: no emission, no shape, fading in and out; the caller fills it in
        internal static ParticleSystem Sys(Transform parent, string name, Material mat, bool world)
        {
            if (mat == null) return null;
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule m = ps.main;
            m.loop = false; m.playOnAwake = false; m.duration = 1f;
            m.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.startSpeed = 0f; m.gravityModifier = 0f; m.maxParticles = 300;
            ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = ps.shape; sh.enabled = false;
            Fade(ps, Color.white, Color.white);
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        internal static void Burst(ParticleSystem ps, int n)
        {
            ParticleSystem.EmissionModule em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)n) });
            ps.Play();
        }

        // a flat ring (edge: from its rim outwards) or disc over the ground
        internal static void Disc(ParticleSystem ps, float r, bool edge)
        {
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = r;
            sh.radiusThickness = edge ? 0f : 1f;
            sh.rotation = new Vector3(90f, 0f, 0f);
        }

        internal static void Fade(ParticleSystem ps, Color from, Color to)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        internal static void Shrink(ParticleSystem ps, float end)
        {
            ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, end)));
        }

        // a slow wander of its own, so nothing hangs as if painted on the air
        internal static void Drift(ParticleSystem ps, float strength, float frequency)
        {
            ParticleSystem.NoiseModule n = ps.noise;
            n.enabled = true; n.strength = new ParticleSystem.MinMaxCurve(strength); n.frequency = frequency;
            n.scrollSpeed = 0.3f; n.damping = true; n.quality = ParticleSystemNoiseQuality.Low;
        }

        // each one tilted its own way and turning slowly (radians, ± this much)
        internal static void Spin(ParticleSystem ps, float tilt, float perSecond)
        {
            ParticleSystem.MainModule m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(-tilt, tilt);
            ParticleSystem.RotationOverLifetimeModule r = ps.rotationOverLifetime; r.enabled = true; r.z = new ParticleSystem.MinMaxCurve(-perSecond, perSecond);
        }

        internal static void Sheet(ParticleSystem ps, int tiles)
        {
            ParticleSystem.TextureSheetAnimationModule t = ps.textureSheetAnimation;
            t.enabled = true; t.numTilesX = tiles; t.numTilesY = tiles;
            t.animation = ParticleSystemAnimationType.WholeSheet;
            t.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            t.startFrame = new ParticleSystem.MinMaxCurve(0f, tiles * tiles - 0.01f);
        }

        // soft clouds of our smoke in the element's deep colour, low over the ground
        // (lasting > 0: they keep coming that long; set before it plays — a playing system's duration cannot change)
        internal static ParticleSystem Mist(Transform parent, Style s, float r, int n, float life, float sizeMin, float sizeMax, float lasting = 0f)
        {
            ParticleSystem c = Sys(parent, "mist", Feathers.Smoke(), true);
            if (c == null) return null;
            Sheet(c, 2);
            ParticleSystem.MainModule m = c.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life); m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            m.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            Spin(c, 3f, 0.4f);
            Color d = Deep[(int)s];
            m.startColor = new ParticleSystem.MinMaxGradient(d, Color.Lerp(d, Bright[(int)s], 0.3f));
            m.gravityModifier = -0.03f;
            if (lasting > 0f) { m.duration = lasting; ParticleSystem.EmissionModule em = c.emission; em.rateOverTime = r * 2.5f; } // (not looping: it emits for its duration, then the clouds fade out)
            Disc(c, r, false);
            Burst(c, n);
            return c;
        }

        // a short light in the element's colour (a big moment only: lights cost)
        internal static void Flash(Vector3 at, Style s, float intensity, float range, float life)
        {
            if (BindFlash.Alive >= 4) return; // (a hammer on five foes: four lights are plenty)
            GameObject g = new GameObject("RavenWatch_BindFx_light");
            g.transform.position = at;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = Bright[(int)s]; l.range = Mathf.Clamp(range, 1f, 15f); l.intensity = intensity; l.shadows = LightShadows.None;
            BindFlash f = g.AddComponent<BindFlash>();
            f.L = l; f.Peak = intensity; f.Life = life;
        }

        internal static string ShieldText() { int v = Mathf.CeilToInt(BindRun.ShieldLeft); return v > 0 ? v.ToString() : ""; }
    }

    // ---------------------------------------------------------------- a state that lasts, hung on the player
    internal sealed class BindAura : MonoBehaviour
    {
        internal Player Who;
        internal float ShieldFrac = 1f;
        private readonly GameObject[] _on = new GameObject[BindFx.States];
        private readonly float[] _until = new float[BindFx.States];
        private ParticleSystem _shell, _shellDots;
        private BindSway _sway;
        private float _struck, _k = -1f, _spread = -1f;

        internal void On(int i, float seconds)
        {
            if (i < 0 || i >= BindFx.States || Who == null) return;
            _until[i] = Time.time + seconds + 0.5f; // (an «off» lost on the way: it ends by itself)
            if (_on[i] != null) return;
            try { _on[i] = Make(i); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Bind state fx " + BindFx.StateIds[i] + ": " + e.Message); }
        }

        internal void Off(int i)
        {
            if (i < 0 || i >= BindFx.States) return;
            _until[i] = 0f;
            GameObject g = _on[i];
            _on[i] = null;
            if (i == 0) { _shell = null; _shellDots = null; _sway = null; ShieldFrac = 1f; _k = -1f; _spread = -1f; }
            if (g == null) return;
            foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); // (what flies, flies out)
            Destroy(g, 1.6f);
        }

        internal void Struck() { _struck = Time.time; if (_shell != null) _shell.Emit(10); }

        private void Update()
        {
            for (int i = 0; i < BindFx.States; i++) if (_on[i] != null && Time.time > _until[i]) Off(i);
            if (_shell != null) // the shell thins as it wears down, flares a moment when struck
            {
                float k = Mathf.Round((Mathf.Clamp01(ShieldFrac) * 0.65f + 0.35f) * 20f) / 20f;
                if (Time.time - _struck < 0.25f) k = 1.6f;
                if (k != _k)
                {
                    _k = k;
                    ParticleSystem.EmissionModule a = _shell.emission; a.rateOverTime = 16f * k;
                    if (_shellDots != null) { ParticleSystem.EmissionModule e = _shellDots.emission; e.rateOverTime = 30f * k; }
                }
                // on the move the runes drift apart and the shell widens a little; standing, they gather back
                float sp = _sway != null ? Mathf.Round(_sway.Pace * 10f) / 10f : 0f;
                if (sp != _spread) { _spread = sp; Spread(_shell, sp, 0.4f); if (_shellDots != null) Spread(_shellDots, sp, 0.55f); }
            }
        }

        private static void Spread(ParticleSystem ps, float pace, float push)
        {
            ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
            v.radial = new ParticleSystem.MinMaxCurve(-0.05f + push * pace, 0.05f + push * pace);
            v.orbitalYMultiplier = 1f + 0.5f * pace;
            ParticleSystem.ShapeModule sh = ps.shape; sh.radius = 1f + 0.15f * pace;
        }

        private void OnDestroy() { for (int i = 0; i < BindFx.States; i++) if (_on[i] != null) Destroy(_on[i]); }

        private GameObject Make(int i)
        {
            GameObject root = new GameObject("RavenWatch_BindState_" + BindFx.StateIds[i]);
            root.transform.SetParent(Who.transform, false);
            if (i != 3) // (the thirst keeps to your hand by itself)
            {
                BindSway sway = BindSway.On(root.transform, i == 0 ? 1f : 0.5f, i == 0 ? 1f : 0f); // (the shell most: it is the one you see whole)
                if (i == 0) _sway = sway;
            }
            switch (i)
            {
                case 0: Shield(root.transform); break;
                case 1: Healing(root.transform); break;
                case 2: Smoke(root.transform); break;
                case 3: Thirst(root.transform); break;
                case 4: Rush(root.transform); break;
            }
            return root;
        }

        // a shell of golden runes round you, each on its own slightly tilted orbit; a faint ring of glow on the ground
        private void Shield(Transform root)
        {
            Vector3 mid = Who.transform.InverseTransformPoint(Who.GetCenterPoint());
            BindFx.Style s = BindFx.Style.Runes;
            ParticleSystem r = BindFx.Sys(root, "shell", BindFx.Runes(), false);
            if (r != null)
            {
                r.transform.localPosition = mid;
                BindFx.Sheet(r, 4);
                ParticleSystem.MainModule m = r.main;
                m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.18f); m.startColor = BindFx.Pair(s);
                ParticleSystem.ShapeModule sh = r.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1f; sh.radiusThickness = 0f;
                Orbits(r, 0.45f, 0.95f);
                BindFx.Spin(r, 0.35f, 0.4f);
                BindFx.Drift(r, 0.07f, 0.5f);
                ParticleSystem.EmissionModule em = r.emission; em.rateOverTime = 16f;
                r.Play();
                _shell = r;
            }
            ParticleSystem d = BindFx.Sys(root, "shell_glow", BindFx.Glow(), false);
            if (d != null)
            {
                d.transform.localPosition = mid;
                ParticleSystem.MainModule m = d.main;
                m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f); m.startColor = BindFx.Pair(s);
                ParticleSystem.ShapeModule sh = d.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1f; sh.radiusThickness = 0f;
                Orbits(d, 0.3f, 1.1f);
                BindFx.Drift(d, 0.1f, 0.8f);
                ParticleSystem.EmissionModule em = d.emission; em.rateOverTime = 30f;
                d.Play();
                _shellDots = d;
            }
            ParticleSystem g = BindFx.Sys(root, "ground", BindFx.Glow(), false);
            if (g != null)
            {
                g.transform.localPosition = Vector3.up * 0.08f;
                ParticleSystem.MainModule m = g.main;
                m.loop = true; m.startLifetime = 0.8f; m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.12f); m.startColor = BindFx.Pair(s);
                BindFx.Disc(g, 0.95f, true);
                ParticleSystem.EmissionModule em = g.emission; em.rateOverTime = 22f;
                g.Play();
            }
        }

        // turning round you on slightly tilted paths (all three orbits one mode, as Unity wants)
        private static void Orbits(ParticleSystem ps, float min, float max)
        {
            ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
            v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.orbitalX = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f); v.orbitalY = new ParticleSystem.MinMaxCurve(min, max); v.orbitalZ = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            v.radial = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        }

        // green sparks rising from your feet all the while
        private void Healing(Transform root)
        {
            ParticleSystem g = BindFx.Sys(root, "rise", BindFx.Glow(), true);
            if (g == null) return;
            g.transform.localPosition = Vector3.up * 0.1f;
            ParticleSystem.MainModule m = g.main;
            m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f); m.startColor = BindFx.Pair(BindFx.Style.Life); m.gravityModifier = -0.4f;
            BindFx.Disc(g, 0.45f, false);
            BindFx.Drift(g, 0.15f, 0.8f);
            ParticleSystem.EmissionModule em = g.emission; em.rateOverTime = 12f;
            g.Play();
        }

        // dark smoke curling round you: the foes have lost you
        private void Smoke(Transform root)
        {
            ParticleSystem c = BindFx.Sys(root, "smoke", Feathers.Smoke(), true);
            if (c == null) return;
            c.transform.localPosition = Vector3.up * 0.9f;
            BindFx.Sheet(c, 2);
            ParticleSystem.MainModule m = c.main;
            m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            BindFx.Spin(c, 3f, 0.4f);
            Color d = BindFx.DeepOf(BindFx.Style.Shadow);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(d.r, d.g, d.b, 0.5f), new Color(d.r + 0.1f, d.g + 0.1f, d.b + 0.12f, 0.35f));
            m.gravityModifier = -0.05f;
            ParticleSystem.ShapeModule sh = c.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            BindFx.Drift(c, 0.2f, 0.35f);
            ParticleSystem.EmissionModule em = c.emission; em.rateOverTime = 9f;
            c.Play();
        }

        // blood dripping from your weapon hand and a red haze about it: your blows drink
        private void Thirst(Transform root)
        {
            Animator an = Who.GetComponentInChildren<Animator>();
            Transform hand = PlayerAnims.SkeletonBone(Who, an != null ? an.transform : Who.transform, "RightHand"); // (the body's own hand)
            Transform at = hand != null ? hand : root;
            Vector3 local = hand != null ? Vector3.zero : Who.transform.InverseTransformPoint(Who.GetCenterPoint());
            BindFx.Style s = BindFx.Style.Blood;
            ParticleSystem d = BindFx.Sys(root, "drops", BindFx.Glow(), true);
            if (d != null)
            {
                Follow(d.transform, at, local);
                ParticleSystem.MainModule m = d.main;
                m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f); m.startColor = BindFx.Pair(s); m.gravityModifier = 0.9f;
                ParticleSystem.ShapeModule sh = d.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.08f;
                ParticleSystem.EmissionModule em = d.emission; em.rateOverTime = 7f;
                d.Play();
            }
            ParticleSystem c = BindFx.Sys(root, "haze", Feathers.Smoke(), true);
            if (c != null)
            {
                Follow(c.transform, at, local);
                BindFx.Sheet(c, 2);
                ParticleSystem.MainModule m = c.main;
                m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f); m.startSpeed = 0.1f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                BindFx.Spin(c, 3f, 0.5f);
                Color dc = BindFx.DeepOf(s);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(dc.r, dc.g, dc.b, 0.4f), new Color(dc.r + 0.2f, dc.g, dc.b, 0.25f));
                ParticleSystem.EmissionModule em = c.emission; em.rateOverTime = 5f;
                c.Play();
            }
        }

        // pale streaks and a feather or two behind your feet
        private void Rush(Transform root)
        {
            GameObject tpl = FxLib.Template(FxLib.OursPrefix + "streak");
            BindFx.Style s = BindFx.Style.Wind;
            if (tpl != null)
                for (int k = -1; k <= 1; k += 2)
                {
                    GameObject g = Instantiate(tpl, root, false);
                    g.hideFlags = HideFlags.None;
                    g.transform.localPosition = new Vector3(0.18f * k, 0.25f, 0f);
                    WsFx tint = g.AddComponent<WsFx>();
                    g.SetActive(true);
                    tint.Recolor(true, BindFx.BrightOf(s));
                }
            ParticleSystem f = BindFx.Sys(root, "feathers", Feathers.Feather(), true);
            if (f != null)
            {
                f.transform.localPosition = Vector3.up * 0.4f;
                BindFx.Sheet(f, 2);
                ParticleSystem.MainModule m = f.main;
                m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f); m.startSpeed = 0.2f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f); m.startColor = BindFx.Pair(s);
                BindFx.Spin(f, 3f, 2f);
                ParticleSystem.EmissionModule em = f.emission; em.rateOverDistance = 1.5f;
                f.Play();
            }
        }

        // a system that keeps to a bone of the player (the hand) without being put under the model
        private static void Follow(Transform t, Transform bone, Vector3 local)
        {
            BindFollow f = t.gameObject.AddComponent<BindFollow>();
            f.Bone = bone; f.Local = local;
            if (bone != null) t.position = bone.TransformPoint(local);
        }
    }

    // Something hung on the player that keeps up with them a beat late, as if it had weight: it lags a little behind
    // when they run and swings back when they stop, turns after them, and breathes and bobs gently standing still.
    // Every limit is soft (the further, the harder it pulls back) — it never stops dead. Pace (0..1) tells how fast
    // they go, for whoever wants the runes to drift apart on the move.
    internal sealed class BindSway : MonoBehaviour
    {
        private const float LagTime = 0.14f, LagMax = 0.22f, TurnTime = 0.2f, TurnMax = 28f, Bob = 0.025f, Breath = 0.035f;
        private float _weight, _breathe;
        internal float Pace { get { return _pace; } }
        private Vector3 _base, _scale, _pos, _vel, _was;
        private float _yaw, _yawVel, _pace, _phase;
        private bool _ready;

        internal static BindSway On(Transform t, float weight, float breathe)
        {
            BindSway s = t.gameObject.AddComponent<BindSway>();
            s._weight = weight; s._breathe = breathe;
            s._base = t.localPosition; s._scale = t.localScale; // (read here, not in Start: a LateUpdate may come first)
            s._phase = UnityEngine.Random.value * 10f;
            return s;
        }

        private static float Soft(float x, float max) { return max * (1f - Mathf.Exp(-x / max)); }

        private void LateUpdate()
        {
            Transform up = transform.parent;
            float dt = Time.deltaTime;
            if (up == null || dt <= 0f) return;
            Vector3 aim = up.TransformPoint(_base);
            float yawAim = up.eulerAngles.y;
            if (!_ready || (aim - _was).sqrMagnitude > 25f) { _ready = true; _pos = aim; _vel = Vector3.zero; _yaw = yawAim; _yawVel = 0f; _pace = 0f; } // (a teleport, a portal: start anew there)
            Vector3 moved = aim - _was; moved.y = 0f;
            _was = aim;
            _pace = Mathf.Lerp(_pace, 1f - Mathf.Exp(-moved.magnitude / dt / 5f), 1f - Mathf.Exp(-dt * 4f));

            _pos = Vector3.SmoothDamp(_pos, aim, ref _vel, LagTime, Mathf.Infinity, dt);
            Vector3 lag = _pos - aim;
            float l = lag.magnitude, max = LagMax * _weight;
            if (l > 0.0001f && max > 0f) { lag *= Soft(l, max) / l; _pos = aim + lag; } else lag = Vector3.zero;

            _yaw = Mathf.SmoothDampAngle(_yaw, yawAim, ref _yawVel, TurnTime, Mathf.Infinity, dt);
            float turn = Mathf.DeltaAngle(yawAim, _yaw), tmax = TurnMax * _weight;
            if (tmax > 0f) { turn = Mathf.Sign(turn) * Soft(Mathf.Abs(turn), tmax); _yaw = yawAim + turn; } else turn = 0f;

            float t = Time.time + _phase, still = 1f - _pace;
            Vector3 drift = new Vector3(Mathf.PerlinNoise(t * 0.3f, 0.5f) - 0.5f, Mathf.Sin(t * 2.6f) * 0.5f, Mathf.PerlinNoise(0.5f, t * 0.3f) - 0.5f) * (2f * Bob * _weight * still);
            transform.position = aim + lag + drift;
            transform.rotation = Quaternion.AngleAxis(turn, Vector3.up) * up.rotation;
            if (_breathe > 0f) transform.localScale = _scale * (1f + Mathf.Sin(t * 1.9f) * Breath * _breathe);
        }
    }

    internal sealed class BindFollow : MonoBehaviour
    {
        internal Transform Bone;
        internal Vector3 Local;
        private void LateUpdate() { if (Bone != null) transform.position = Bone.TransformPoint(Local); }
    }

    // a light that dies down and goes
    internal sealed class BindFlash : MonoBehaviour
    {
        internal static int Alive;
        internal Light L;
        internal float Peak, Life = 0.4f;
        private float _t;
        private void Awake() { Alive++; }
        private void OnDestroy() { Alive--; }
        private void Update()
        {
            _t += Time.deltaTime;
            if (L != null) L.intensity = Peak * Mathf.Clamp01(1f - _t / Life);
            if (_t >= Life) Destroy(gameObject);
        }
    }

    // something flying along an arc from a place into a player (a stolen life), then left to fade
    internal sealed class BindHoming : MonoBehaviour
    {
        internal Vector3 From;
        internal Transform To;
        internal float Life = 0.45f;
        private float _t;
        private bool _done, _seen;
        private Vector3 _end;
        private void Update()
        {
            if (_done) return;
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Life);
            if (To != null) { _end = To.position + Vector3.up * 1.1f; _seen = true; }
            Vector3 end = _seen ? _end : From;
            Vector3 mid = Vector3.Lerp(From, end, 0.5f) + Vector3.up * 0.8f;
            transform.position = Vector3.Lerp(Vector3.Lerp(From, mid, k), Vector3.Lerp(mid, end, k), k);
            if (k < 1f) return;
            _done = true;
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 1.6f);
        }
    }

    // a jagged bolt along points: two lines (a coloured glow, a white core), drawn anew a few times, fading out
    internal sealed class BindBolt : MonoBehaviour
    {
        internal Vector3[] Pts;
        internal Color Color = Color.white;
        internal float Life = 0.35f;
        private LineRenderer _glow, _core;
        private float _t, _next;
        private readonly List<Vector3> _line = new List<Vector3>();

        private void Start()
        {
            _glow = Line(gameObject, 0.16f);
            GameObject c = new GameObject("core");
            c.transform.SetParent(transform, false);
            _core = Line(c, 0.05f);
        }

        private static LineRenderer Line(GameObject g, float width)
        {
            LineRenderer l = g.AddComponent<LineRenderer>();
            l.sharedMaterial = FxLib.Glow(false);
            l.useWorldSpace = true;
            l.widthMultiplier = width;
            l.textureMode = LineTextureMode.Stretch;
            l.numCapVertices = 2;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            return l;
        }

        private void Update()
        {
            if (Pts == null || Pts.Length < 2 || _glow == null) { Destroy(gameObject); return; }
            _t += Time.deltaTime;
            if (_t >= Life) { Destroy(gameObject); return; }
            if (_t >= _next) { _next = _t + 0.05f; Build(); }
            float a = 1f - _t / Life;
            Color g = new Color(Color.r, Color.g, Color.b, a), w = new Color(1f, 1f, 1f, a);
            _glow.startColor = _glow.endColor = g;
            _core.startColor = _core.endColor = w;
        }

        private void Build()
        {
            _line.Clear();
            for (int i = 0; i + 1 < Pts.Length; i++)
            {
                Vector3 a = Pts[i], b = Pts[i + 1];
                int n = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(a, b) / 0.6f), 3, 30);
                for (int k = 0; k < n; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, (float)k / n);
                    if (k > 0) p += UnityEngine.Random.insideUnitSphere * 0.35f;
                    _line.Add(p);
                }
            }
            _line.Add(Pts[Pts.Length - 1]);
            _glow.positionCount = _core.positionCount = _line.Count;
            for (int i = 0; i < _line.Count; i++) { _glow.SetPosition(i, _line[i]); _core.SetPosition(i, _line[i]); }
        }
    }

    // a state of your в'язь in the status bar: its time (the shield: what it still takes)
    internal sealed class SE_RavenBindState : StatusEffect
    {
        public int Kind;
        public override string GetIconText() { return Kind == 0 ? BindFx.ShieldText() : base.GetIconText(); }
    }
}
#endif
