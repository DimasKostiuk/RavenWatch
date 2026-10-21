#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // «Вороняче гніздо»: a piece built with the hammer (a Workshop model marked "use: nest", Perches builds it), a perch
    // of two seats with two zones of its own (unseen boxes in the model: "box:top", "box:bowl"):
    //   the TOP — E sends both your ravens into the nest, E again calls them back (SeatZone, shared with the raven trees);
    //   the BOWL — E opens the nest's own window (NestWindow), only while your ravens sit in it (NestBowl).
    // In the window, first the gifts to Odin (Favour), then «Призвати милість Одіна» (Summon): the ravens caw from the
    // nest and call him. He answers at a moment of his own (10–60 s of the ravens sitting, kept in the CHARACTER, so
    // another nest or taking them off changes nothing) — or not at all today: a roll of Chance (60%, the gifts lift it;
    // never shown as a number, only as an omen, Omen). Answered: a golden glow over the nest (every player sees it) and
    // «Прийняти волю Одіна» / «Відхилити»: what he wants is not said. Accepted: one of the ravens (by chance, leaning by
    // the favour) flies up into the clouds and comes back to your shoulder some minutes later WITH the errand (Back;
    // golden by chance, once three runes burn). Not answered or declined: the next call only the next game day.
    internal static class Nest
    {
        internal const string Why = "errand";                 // the reason a raven is away (Trust)
        internal const string NextKey = "RavenWatch_nestNext"; // player customData: world time of the next errand
        internal const string OdinKey = "RavenWatch_odin";    // player customData: call=1;need=<s>;sat=<s>;came=1
        internal static float AwayMin = 300f, AwayMax = 480f; // s at Odin's
        internal static float UseRange = 6f;                  // m: the window stays open this near
        internal static float MinWait = 10f, WaitMin = 10f, WaitMax = 60f; // s of sitting before Odin comes
        internal static float BaseChance = 0.6f, GiftChance = 0.35f; // he answers the call: 60%, gifts of FullGifts add up to +35%
        internal static float FullGifts = 100f;               // favour points given since he last came (all groups together) for the whole gift share
        internal const string GiftsKey = "RavenWatch_odinGifts"; // player customData: those points (kept when he did not come, gone once he comes)

        private static float DayLen { get { return EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f; } }

        internal static float CooldownLeft(Player p) { return p != null ? WorldTime.LeftHere(p.m_customData, NextKey) : 0f; }

        // the raven now at Odin's for an errand (and how long yet); false when none
        internal static bool Flying(out bool munin, out float left)
        {
            munin = false; left = 0f;
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Trust.Worn(m);
                if (item == null || Trust.AwayWhy(item) != Why || Trust.AwayLeft(item) <= 0f) continue;
                munin = m; left = Trust.AwayLeft(item);
                return true;
            }
            return false;
        }

        // null when Odin may come now; else why not
        internal static string Blocked(Player p)
        {
            if (p == null) return Lang.T("Нема гравця.");
            if (Errands.Of(p) != null) return Lang.T("У тебе вже є доручення. Нове — коли виконаєш або відмовишся.");
            bool m; float left;
            if (Flying(out m, out left)) return Lang.F("{0} у Одіна. Повернеться за {1} — з дорученням.", Plugin.RavenName(m), RavenAbilities.Span(left));
            float wait = CooldownLeft(p);
            if (wait > 0f) return Lang.F("Одін сьогодні вже дав доручення. Наступне — за {0}.", RavenAbilities.Span(wait));
            return null;
        }

        // ---------------------------------------------------------------- the wait, kept in the character
        private sealed class Call { public float Need, Sat; public bool Called, Came; }

        private static Call Read(Player p)
        {
            Call c = new Call();
            string raw;
            if (p == null || !p.m_customData.TryGetValue(OdinKey, out raw)) return c;
            foreach (string part in raw.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                if (k == "need") c.Need = Errands.F(v, 0f);
                else if (k == "sat") c.Sat = Errands.F(v, 0f);
                else if (k == "came") c.Came = v == "1";
                else if (k == "call") c.Called = v == "1";
            }
            if (!c.Called) { c.Came = false; c.Sat = 0f; } // (a record from before the call button: that old wait counts for nothing, he comes only when called)
            c.Need = Mathf.Clamp(c.Need, 0f, WaitMax); c.Sat = Mathf.Max(0f, c.Sat);
            return c;
        }

        private static void Write(Player p, Call c)
        {
            p.m_customData[OdinKey] = (c.Called ? "call=1;" : "") + "need=" + c.Need.ToString("0.#", CultureInfo.InvariantCulture) + ";sat=" + c.Sat.ToString("0.#", CultureInfo.InvariantCulture) + (c.Came ? ";came=1" : "");
        }

        internal static bool Came(Player p) { return Read(p).Came; }

        // the ravens are calling him now (or he has come)
        internal static bool Called(Player p) { return Read(p).Called; }

        // ---------------------------------------------------------------- the call
        // how likely he answers it: 60%, and what was given since he last came lifts it up to 95% (not the whole favour:
        // old favour stays for long — gold is spent only by golden errands — and would keep the chance high for ever)
        internal static float Chance(Player p) { return Mathf.Clamp01(BaseChance + GiftChance * Mathf.Clamp01(Gifts(p) / Mathf.Max(1f, FullGifts))); }

        internal static float Gifts(Player p)
        {
            string raw;
            return p != null && p.m_customData.TryGetValue(GiftsKey, out raw) ? Mathf.Max(0f, Errands.F(raw, 0f)) : 0f;
        }

        // the bowl took favour (Favour.Give): toward the next call
        internal static void Gifted(Player p, float points)
        {
            if (p == null || points <= 0f) return;
            p.m_customData[GiftsKey] = Mathf.Min(FullGifts, Gifts(p) + points).ToString("0.##", CultureInfo.InvariantCulture);
        }

        // the chance as the player may see it: never a number, an omen and 1–5 marks
        internal static int OmenLevel(Player p)
        {
            float t = Mathf.Clamp01((Chance(p) - BaseChance) / Mathf.Max(0.01f, GiftChance));
            return t < 0.05f ? 1 : t < 0.35f ? 2 : t < 0.65f ? 3 : t < 0.95f ? 4 : 5;
        }

        internal static readonly string[] Omens = Lang.Keys(
            "Знаки мовчать. Одін ще не дивиться в бік твого гнізда.",
            "Вітер ледь ворухнув пір'я в гнізді: Одін, може, й почує.",
            "Ворони неспокійні, дзьобами до неба — Одін прислухається.",
            "Дари в чаші ледь теплі: Одін прихильний до тебе.",
            "Чаша мовби жевріє: Одін чекає на твій поклик.");

        internal static string Omen(Player p) { return Lang.T(Omens[OmenLevel(p) - 1]); }

        // «Призвати милість Одіна»: the ravens call him from the nest; null when they do, else why not
        internal static string Summon(Player p, NestView v)
        {
            string b = Blocked(p);
            if (b != null) return b;
            if (v == null || !v.Valid) return Lang.T("Гнізда тут нема.");
            if (MineIn(p, v).Count == 0) return Lang.T("Твоїх воронів у гнізді нема.");
            Call c = Read(p);
            if (c.Called) return c.Came ? Lang.T("Одін уже прийшов.") : Lang.T("Ворони вже кличуть Одіна.");
            c.Called = true; c.Came = false; c.Sat = 0f;
            c.Need = UnityEngine.Random.Range(WaitMin, Mathf.Max(WaitMin, WaitMax));
            Write(p, c);
            v.Caw();
            Errands.Version++;
            Errands.Log(p.GetPlayerName() + " called Odin from the nest (answer after " + Mathf.RoundToInt(c.Need) + " s, chance " + Mathf.RoundToInt(Chance(p) * 100f) + "%)");
            return null;
        }

        // the call that came to nothing, or Odin's will declined: nothing lost, the gifts stay; the next call — tomorrow
        private static void NotToday(Player p)
        {
            WorldTime.SetHere(p.m_customData, NextKey, DayLen);
            p.m_customData.Remove(OdinKey);
            SetGlow(p, null);
            _session = 0f; _toldCame = false;
            Errands.Version++;
        }

        // «Відхилити» under the glow
        internal static string Decline(Player p)
        {
            if (p == null) return Lang.T("Нема гравця.");
            if (!Read(p).Came) return Lang.T("Одін ще не прийшов. Ворони чекають.");
            NotToday(p);
            Errands.Log(p.GetPlayerName() + " declined Odin's will at the nest");
            return null;
        }

        // your ravens sitting in this nest (Hugin false, Munin true)
        internal static List<bool> MineIn(Player p, NestView v) { return SeatZone.Mine(p, v != null && v.Valid ? v.Perch : null); }

        // the nest your ravens sit in, when it is loaded here
        internal static NestView Where(Player p)
        {
            foreach (NestView v in NestView.All) if (v != null && v.Valid && MineIn(p, v).Count > 0) return v;
            return null;
        }

        private static NestView _at, _glowOn;
        private static float _session, _last;
        private static bool _toldCame;

        // twice a second (Errands.Tick): the ravens wait, Odin comes, the glow goes on and off
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null) { _at = null; _glowOn = null; return; }
            float now = Time.time, dt = _last > 0f ? Mathf.Clamp(now - _last, 0f, 2f) : 0f;
            _last = now;
            NestView v = Where(p);
            if (v != _at) { _at = v; _session = 0f; _toldCame = false; }
            bool glow = false;
            Call c = v != null && Blocked(p) == null ? Read(p) : null;
            if (c != null && c.Called)
            {
                _session += dt;
                if (c.Need <= 0f) c.Need = UnityEngine.Random.Range(WaitMin, Mathf.Max(WaitMin, WaitMax));
                if (!c.Came)
                {
                    c.Sat += dt;
                    if (c.Sat >= c.Need)
                    {
                        float chance = Chance(p);
                        if (UnityEngine.Random.value >= chance)
                        {
                            Errands.Log("Odin did not answer " + p.GetPlayerName() + " today (chance was " + Mathf.RoundToInt(chance * 100f) + "%)");
                            NotToday(p);
                            RavenMsg.Center(p, Lang.T("Ворони кликали й кликали, та Одін сьогодні не озвався. Спробуй завтра — а дари в чаші не пропадуть."));
                            RavenFx.Play("sfx_raven_kaw", v.Seat, Quaternion.identity);
                            return;
                        }
                        c.Came = true;
                        p.m_customData.Remove(GiftsKey); // (the gifts have done their part)
                        Favour.Version++;
                        Errands.Log("Odin came to " + p.GetPlayerName() + " after " + Mathf.RoundToInt(c.Sat) + " s of the ravens calling (chance " + Mathf.RoundToInt(chance * 100f) + "%)");
                    }
                }
                Write(p, c);
                glow = c.Came && _session >= MinWait;
                if (glow && !_toldCame)
                {
                    _toldCame = true;
                    RavenMsg.Center(p, Lang.T("Над гніздом золоте сяйво: Одін змилувався. Зазирни в чашу гнізда."));
                    RavenFx.Play("sfx_raven_kaw", v.Seat, Quaternion.identity);
                }
            }
            SetGlow(p, glow ? v : null);
        }

        private static void SetGlow(Player p, NestView v)
        {
            if (_glowOn == v) return;
            if (_glowOn != null && _glowOn.Valid) _glowOn.SendGlow(p.GetPlayerID(), false);
            _glowOn = v;
            if (v != null) v.SendGlow(p.GetPlayerID(), true);
            Errands.Version++;
        }

        // Odin is over this nest now, for you
        internal static bool OdinOver(NestView v) { return v != null && _glowOn == v; }

        // how long the ravens have been waiting here (the window's «…»)
        internal static float Waited { get { return _session; } }

        // ---------------------------------------------------------------- Odin's will taken
        // null when a raven flew up to him; else why not
        internal static string Accept(Player p, NestView v)
        {
            string b = Blocked(p);
            if (b != null) return b;
            if (v == null || !v.Valid) return Lang.T("Гнізда тут нема.");
            if (!OdinOver(v)) return Lang.T("Одін ще не прийшов. Ворони чекають.");
            List<bool> mine = MineIn(p, v);
            if (mine.Count == 0) return Lang.T("Твоїх воронів у гнізді нема.");
            bool munin = mine.Count == 1 ? mine[0] : UnityEngine.Random.value < Favour.MuninChance(p);
            ItemDrop.ItemData item = Perches.MineOn(p, v.Perch, munin);
            if (item == null) return Lang.T("Твоїх воронів у гнізді нема.");
            float away = UnityEngine.Random.Range(AwayMin, Mathf.Max(AwayMin, AwayMax));
            Trust.SendAway(item, away, Why);
            WorldTime.SetHere(p.m_customData, NextKey, DayLen);
            p.m_customData.Remove(OdinKey); // the next wait is a new one
            SetGlow(p, null);
            _session = 0f; _toldCame = false;
            v.Cloud();
            Perches.ToOdin(p, item, munin, Lang.F("{0} злетів у хмари до Одіна. Повернеться на плече за {1} — з дорученням.", Plugin.RavenName(munin), RavenAbilities.Span(away)));
            Errands.Version++;
            ItemDrop.ItemData worn = Trust.Worn(munin);
            Errands.Log((munin ? "Munin" : "Hugin") + " flew up to Odin from the nest for " + Mathf.RoundToInt(away) + " s (after: away " + Mathf.RoundToInt(Trust.AwayLeft(item)) + " s, why «" + Trust.AwayWhy(item) + "», the worn one is it: " + ReferenceEquals(worn, item) + (worn != null && !ReferenceEquals(worn, item) ? ", the worn one's away " + Mathf.RoundToInt(Trust.AwayLeft(worn)) + " s" : "") + ")");
            return null;
        }

        // back from Odin (Trust's watch): with an errand — golden by chance, once three runes burn (gold favour adds)
        internal static void Back(Player p, bool munin, ItemDrop.ItemData item)
        {
            if (p == null) return;
            Errands.Version++;
            if (Errands.Of(p) != null) { RavenMsg.Center(p, Lang.F("{0} повернувся від Одіна. Доручення в тебе вже є.", Plugin.RavenName(munin))); return; }
            float chance = RuneBook.GoldChance(RuneBook.Of(p).Opened);
            if (chance > 0f) chance = Mathf.Clamp01(chance + Favour.GoldBonus(p));
            ErrandDef d = chance > 0f && UnityEngine.Random.value < chance ? Errands.Pick(p, munin, true) : null;
            if (d == null) d = Errands.Pick(p, munin, false);
            if (d == null)
            {
                WorldTime.ClearHere(p.m_customData, NextKey); // nothing came of it: Odin may be asked again
                RavenMsg.Center(p, Lang.F("{0} повернувся від Одіна ні з чим: цього разу Одін мовчить. Посади воронів у гніздо ще раз.", Plugin.RavenName(munin)));
                return;
            }
            // (a place of the world in it: given once the server answers; nothing came of it — Odin may be asked again)
            System.Action<string> failed = w => { Player me = Player.m_localPlayer; if (me == null) return; WorldTime.ClearHere(me.m_customData, NextKey); Errands.Version++; RavenMsg.Center(me, w); };
            string why = Errands.Give(p, d, munin, failed);
            if (why != null) failed(why);
        }

        // the errand given up: nothing lost, and the next only the next day
        internal static void Refuse(Player p)
        {
            if (p == null || Errands.Of(p) == null) return;
            ErrandTalk.Refuse(Errands.Of(p).Munin);
            Errands.Drop(p);
            if (CooldownLeft(p) < DayLen) WorldTime.SetHere(p.m_customData, NextKey, DayLen);
            Errands.Version++;
        }

        // hand in what the step asks (in the nest's bowl); null when taken
        internal static string HandIn(Player p) { return Errands.HandIn(p); }

        internal static bool AsksToHandIn(Player p)
        {
            ErrandRun r = Errands.Of(p);
            return r != null && r.Current is BringStep;
        }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven nest · raven nest back (the raven back from Odin now) · raven nest ready (no wait for the next one) · raven nest come (Odin comes now)
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            bool m; float left;
            if (what == "back")
            {
                if (!Flying(out m, out left)) return "Жоден ворон зараз не летить до Одіна.";
                Trust.SendAway(Trust.Worn(m), 1f, Why);
                return Plugin.RavenName(m) + " повернеться за секунду.";
            }
            if (what == "ready") { WorldTime.ClearHere(p.m_customData, NextKey); Errands.Version++; return "Одін може прийти знову (денна пауза знята)."; }
            if (what == "come") { Call c = Read(p); c.Called = true; c.Came = true; c.Need = Mathf.Max(c.Need, 1f); Write(p, c); return "Одін прийде, щойно ворони посидять у гнізді " + Mathf.RoundToInt(MinWait) + " с."; }
            Call s = Read(p);
            string b = Blocked(p);
            return Status(p) + "\nшанс " + Mathf.RoundToInt(Chance(p) * 100f) + "% (свіжі дари " + Mathf.RoundToInt(Gifts(p)) + "/" + Mathf.RoundToInt(FullGifts) + "), знак " + OmenLevel(p) + "/5" + (s.Called ? ", ворони кличуть" : "") + "\nчекання: " + Mathf.RoundToInt(s.Sat) + "/" + Mathf.RoundToInt(s.Need) + " с" + (s.Came ? ", Одін прийшов" : "") + (b != null ? "\n" + b : "") + "\nraven nest back | ready | come";
        }

        // what the nest says under its name (hover)
        internal static string Status(Player p)
        {
            bool m; float left;
            if (Flying(out m, out left)) return Lang.F("{0} у Одіна, ще {1}", Plugin.RavenName(m), RavenAbilities.Span(left));
            ErrandRun r = Errands.Of(p);
            if (r != null) return r.Def != null ? Lang.F("Доручення: «{0}»", Lang.T(r.Def.Name)) : Lang.T("Доручення з іншої версії мода");
            float wait = CooldownLeft(p);
            if (wait > 0f) return Lang.F("Наступне доручення — за {0}", RavenAbilities.Span(wait));
            if (Called(p)) return Came(p) ? Lang.T("Одін над гніздом") : Lang.T("Ворони кличуть Одіна");
            if (!Trial.HasRaven(p) && !Trial.Running(p)) return Trial.Open(p) ? Lang.T("Ворона в тебе ще нема — поклич Одіна: він дасть випробування") : Lang.T("Ворона в тебе нема — його можна купити в Хальдора");
            return Lang.T("Посади воронів у гніздо, поклади дари в чашу й поклич Одіна");
        }
    }

    // the nest in the world: its zones, the golden glow when Odin is over it
    internal sealed class NestView : MonoBehaviour
    {
        internal static readonly List<NestView> All = new List<NestView>();
        internal const string GlowKey = "RavenWatch_odinGlow";   // the nest's ZDO: whose Odin is over it (player id; 0 none)
        private const string RpcGlow = "RavenWatch_NestGlow";
        internal const string TrialKey = "RavenWatch_trialGlow"; // the nest's ZDO: whose trial Odin is over it for (player id; 0 none)
        private const string RpcTrial = "RavenWatch_NestTrialGlow";
        private ZNetView _nv;
        private Perches.Def _def;
        private PerchView _perch;
        private GameObject _glow;
        private Light _light;
        private float _next;

        private void Awake()
        {
            _nv = GetComponent<ZNetView>();
            _perch = GetComponent<PerchView>();
            _def = Perches.DefOf(Utils.GetPrefabName(gameObject));
            All.Add(this);
            if (_nv != null && _nv.GetZDO() != null) { _nv.Register<long, bool>(RpcGlow, OnGlow); _nv.Register<long, bool>(RpcTrial, OnTrialGlow); }
            int zones = 0;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "box:top") { SeatZone z = t.gameObject.AddComponent<SeatZone>(); z.Perch = _perch; z.Nest = this; z.Title = () => Name; z.Line = Nest.Status; zones++; }
                else if (t.name == "box:bowl") { t.gameObject.AddComponent<NestBowl>().View = this; zones++; }
            }
            if (zones < 2 && Plugin.Instance != null) Plugin.Instance.Log("Nest: the model has no box:top / box:bowl zones (only seats by E on it)");
        }

        private void OnDestroy() { All.Remove(this); }

        internal bool Valid { get { return this != null && _nv != null && _nv.IsValid(); } }
        internal PerchView Perch { get { return _perch; } }
        internal string Name
        {
            get
            {
                if (_def == null) return Lang.T("Вороняче гніздо");
                string w = Lang.W(_def.Name);
                return Localization.instance != null ? Localization.instance.Localize(w) : _def.Name;
            }
        }

        // where the ravens sit (its first seat; the top when it has none)
        internal Vector3 Seat
        {
            get
            {
                Transform s = transform.Find("seat_0");
                return s != null ? s.position : transform.position + Vector3.up * 2f;
            }
        }

        internal static float GlowLight = 1.92f; // the golden glow's light

        // the middle of the nest's top: over the centre of the model, at its seats' height (the seats are on the rim)
        internal Vector3 Middle
        {
            get
            {
                Transform s = transform.Find("seat_0");
                return transform.position + Vector3.up * (s != null ? s.localPosition.y : 2f);
            }
        }

        internal bool Near(Player p) { return Valid && p != null && Vector3.Distance(p.transform.position, transform.position) <= Nest.UseRange; }

        // the ravens calling Odin: a caw from each seat, the second a little later
        internal void Caw()
        {
            Transform a = transform.Find("seat_0"), b = transform.Find("seat_1");
            RavenFx.Play("sfx_raven_kaw", a != null ? a.position : Seat, Quaternion.identity);
            if (b != null) StartCoroutine(CawLater(b.position, 0.7f));
        }

        private System.Collections.IEnumerator CawLater(Vector3 at, float after)
        {
            yield return new WaitForSeconds(after);
            RavenFx.Play("sfx_raven_kaw", at, Quaternion.identity);
        }

        // a puff of feathers on it
        internal void Puff()
        {
            Raven tpl = Shoulder.Template(false);
            if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, Seat, Quaternion.identity, null, 0.8f, -1);
            RavenFx.Play("sfx_raven_flap", Seat, Quaternion.identity);
        }

        // the raven gone up into the clouds: a golden burst over the nest
        internal void Cloud()
        {
            Puff();
            try
            {
                GameObject g = new GameObject("RavenWatch_OdinCloud");
                g.transform.position = Middle + Vector3.up * 1.0f;
                ParticleSystem ps = Sparks(g, 60, 0.08f, 0.22f, 2.5f, new Color(1f, 0.85f, 0.45f, 0.9f), new Color(1f, 0.95f, 0.75f, 0.7f));
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });
                ParticleSystem.MainModule main = ps.main;
                main.loop = false; main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
                ps.Play();
                Destroy(g, 4f);
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Nest cloud: " + e.Message); }
        }

        // ---------------------------------------------------------------- the glow (Odin over it)
        internal void SendGlow(long pid, bool on) { if (Valid) _nv.InvokeRPC(ZNetView.Everybody, RpcGlow, pid, on); }

        // Odin over it for one with no ravens (Trial): shown while that player is near
        internal void SendTrialGlow(long pid, bool on) { if (Valid) _nv.InvokeRPC(ZNetView.Everybody, RpcTrial, pid, on); }

        private void OnTrialGlow(long sender, long pid, bool on)
        {
            if (!Valid || !_nv.IsOwner()) return;
            ZDO z = _nv.GetZDO();
            if (on) z.Set(TrialKey, pid);
            else if (z.GetLong(TrialKey, 0L) == pid) z.Set(TrialKey, 0L);
        }

        private void OnGlow(long sender, long pid, bool on)
        {
            if (!Valid || !_nv.IsOwner()) return;
            ZDO z = _nv.GetZDO();
            if (on) z.Set(GlowKey, pid);
            else if (z.GetLong(GlowKey, 0L) == pid) z.Set(GlowKey, 0L);
        }

        // shown while the player whose Odin it is still has a raven sitting here (a player gone: it goes out by itself)
        private void Update()
        {
            if (_glow != null && _light != null && _glow.activeSelf) _light.intensity = GlowLight + Mathf.Sin(Time.time * 1.7f) * GlowLight * 0.22f;
            if (Time.time < _next) return;
            _next = Time.time + 0.4f;
            if (!Valid || _perch == null) return;
            long pid = _nv.GetZDO().GetLong(GlowKey, 0L);
            bool show = false;
            if (pid != 0L && Here(pid)) for (int i = 0; i < _perch.Count; i++) { Perches.Occupant o = _perch.At(i); if (o != null && o.Pid == pid) { show = true; break; } }
            long trial = _nv.GetZDO().GetLong(TrialKey, 0L);
            if (trial != 0L && Here(trial)) show = true;
            if (show && _glow == null) MakeGlow();
            if (_glow != null && _glow.activeSelf != show) _glow.SetActive(show);
        }

        // the player whose Odin it is, near (gone from the game: the glow goes out, though the ravens still sit)
        private bool Here(long pid)
        {
            foreach (Player pl in Player.GetAllPlayers()) if (pl != null && pl.GetPlayerID() == pid && Vector3.Distance(pl.transform.position, transform.position) < 120f) return true;
            return false;
        }

        // warm gold light and slow golden motes rising over the nest (not Hugin's cold ball over a head)
        private void MakeGlow()
        {
            try
            {
                _glow = new GameObject("RavenWatch_OdinGlow");
                _glow.transform.SetParent(transform, false);
                _glow.transform.position = Middle + Vector3.up * 0.7f; // (over the middle of the nest, not over a seat)
                _light = _glow.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = new Color(1f, 0.78f, 0.35f);
                _light.range = 8f;
                _light.intensity = GlowLight;
                _light.shadows = LightShadows.None;
                ParticleSystem ps = Sparks(_glow, 80, 0.05f, 0.14f, 3.2f, new Color(1f, 0.8f, 0.35f, 1f), new Color(1f, 0.93f, 0.6f, 0.72f));
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = 17f;
                ps.Play();
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Nest glow: " + e.Message); }
        }

        private static ParticleSystem Sparks(GameObject g, int max, float sizeMin, float sizeMax, float life, Color a, Color b)
        {
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.gravityModifier = -0.04f; // rising
            main.maxParticles = max;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.6f;
            Gradient fade = new Gradient();
            fade.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true; noise.strength = 0.15f; noise.frequency = 0.6f;
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            Material m = RavenAura.Dot();
            if (m != null) r.sharedMaterial = m;
            return ps;
        }
    }

    // the bowl below: the nest's window, while your ravens sit in it
    internal sealed class NestBowl : MonoBehaviour, Hoverable, Interactable
    {
        internal NestView View;

        public string GetHoverName() { return Lang.T("Чаша гнізда"); }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            Player p = Player.m_localPlayer;
            if (View == null || p == null) return "";
            bool mine = Nest.MineIn(p, View).Count > 0;
            string trial = mine || Trial.HasRaven(p) ? null : Trial.NoRavenHover(p, View);
            if (trial != null) { string t = GetHoverName() + "\n" + trial; return Localization.instance != null ? Localization.instance.Localize(t) : t; }
            string s = GetHoverName() + (mine
                ? "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Зазирнути в чашу")
                : "\n<color=#9A9A9A>" + Lang.T("Спершу посади воронів у гніздо (угорі).") + "</color>");
            return Localization.instance != null ? Localization.instance.Localize(s) : s;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (hold || p == null || p != Player.m_localPlayer || View == null || !View.Valid) return false;
            if (Nest.MineIn(p, View).Count == 0 && !Trial.HasRaven(p)) { Trial.NoRavenUse(p, View); return true; }
            if (Nest.MineIn(p, View).Count == 0 && Trial.Use(p, View)) return true;
            if (Nest.MineIn(p, View).Count == 0) { RavenMsg.Center(p, Lang.T("Чаша порожня, поки в гнізді нема твоїх воронів. Посади їх (угорі).")); return true; }
            RavenBook.OpenNestWindow(View);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
#endif
