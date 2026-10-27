#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Munin's «Кровна помста» (passive): you die, and Munin marks the very one that killed you (a creature, a boss or
    // another player — that one, not its kind: «Злопам'ятний» is about the kind). Up again, you have a while (by
    // Munin's trust) to see it die with you near (an ally's blow counts too): Munin gives back the skills the death
    // took, and the rest of the trust the death cost your ravens. Meanwhile your blows on it hit harder, it wears
    // Munin's red runes over its head, a pin on the map follows it, Munin on the shoulder caws at it when near, and
    // Munin lying in the grave rises from the stone and circles over it (every game sees that: the grave says whom).
    //   - the mark lives in the marked one's ZDO (whose vendetta), set by the game that runs it; that game tells
    //     everybody when it dies (where), and the avenger's game judges: near, in time — paid;
    //   - gone without a word (a raid's creature despawns, it never comes back): one of its kind near the place takes
    //     the mark; none — it got away;
    //   - kept in the character (customData), so leaving the game keeps it; the window counts in world time.
    internal static class BloodRevenge
    {
        internal static bool Enabled = true;
        internal static int UnlockLevel = 5;
        internal static float Minutes = 10f, MinutesStep = 5f, MinutesTop = 20f; // the window at the gift's level, + a step a level
        internal static float NearRange = 30f;       // a death counts while you are this near
        internal static float DamagePercent = 20f;   // your blows on the marked one
        internal static float HuntRange = 60f;       // Munin from the grave circles it while it is this near the grave
        internal static float KinRange = 50f;        // the marked one gone: one of its kind this near takes the mark
        internal static float GoneRange = 60f;       // where a missing one is judged gone (its zone surely loaded here)
        internal static float CawRange = 40f;

        private const string Key = "RavenWatch_blood";             // the character: the vendetta
        private const string SkillsKey = "RavenWatch_bloodSkills"; // the character: the skills just before the death
        internal const string ZdoBy = "RavenWatch_bloodBy";        // the marked one's ZDO: whose vendetta
        internal const string GraveTarget = "RavenWatch_bloodT", GraveUntil = "RavenWatch_bloodUntil"; // the grave's ZDO: whom its Munin hunts, till when (ms of world time)
        private const string RpcMark = "RavenWatch_BloodMark", RpcDied = "RavenWatch_BloodDied";
        internal const string EffectName = "RavenWatch_BloodRevenge";

        private sealed class Feud
        {
            public ZDOID Target;
            public string Prefab = "", Token = "", Name = "";
            public bool IsPlayer;
            public Vector3 DeathAt, LastAt;
            public double Started;          // world time the window began (back on your feet); 0: not yet
            public float Minutes;
            public bool Paid;               // done: what is left is the trust of ravens still in the grave
            public readonly Dictionary<string, float[]> Ravens = new Dictionary<string, float[]>(); // raven id → { lost, given back by the grave }
        }

        private static Feud _feud;
        private static long _feudOf;        // the character it belongs to
        private static float _diedAt = -1f, _aliveSince = -1f, _nextLook, _nextPin, _cawAt, _missingSince = -1f;
        private static bool _graveTagged = true;
        private static Minimap.PinData _pin;
        private static SE_Stats _effect;

        internal static bool Is(RavenAbility a) { return a != null && a.Id == "mrevenge"; }

        // the window as it is for Munin now: Minutes at the level the gift opens, a step more each level, at most MinutesTop
        internal static float WindowMinutes(ItemDrop.ItemData munin)
        {
            int lv = munin != null ? Trust.Level(munin) : Trust.LevelWorn(true);
            return Mathf.Min(MinutesTop, Minutes + MinutesStep * Mathf.Max(0, lv - UnlockLevel));
        }

        internal static string ReadyText()
        {
            if (!Enabled) return Lang.T("<color=#9a9a9a>вимкнено в налаштуваннях</color>");
            Feud f = Mine();
            if (f != null && !f.Paid && f.Started > 0.0) return Lang.F("<color=#FF5A5A><b>мітка на {0} · ще {1}</b></color>", Who(f, false), RavenAbilities.Clock(Left(f)));
            return Lang.F("<color=#FFD54F><b>{0} на помсту</b></color>", RavenAbilities.Span(WindowMinutes(Trust.Worn(true)) * 60f));
        }

        private static Feud Mine()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return null;
            if (_feud == null || _feudOf != p.GetPlayerID()) { _feud = Load(p); _feudOf = p.GetPlayerID(); }
            return _feud;
        }

        private static float Left(Feud f) { return f == null || f.Started <= 0.0 ? 0f : Mathf.Max(0f, (float)(f.Started + f.Minutes * 60.0 - WorldTime.Now)); }

        // the one marked, as said: «троль» / «троля» (gen: the form after «позначив», «на»), a player's name
        private static string Who(Feud f, bool gen)
        {
            if (f.IsPlayer) return f.Name;
            string s;
            Composer.Extra.Clear();
            try
            {
                Composer.ExtraCreature("k", f.Token); // (the ravens' own names with their cases)
                if (Composer.Extra.TryGetValue(gen ? "k_gen" : "k", out s) && !string.IsNullOrEmpty(s)) return s;
            }
            finally { Composer.Extra.Clear(); }
            s = Lang.Game("$" + f.Token);
            return string.IsNullOrEmpty(s) || s.StartsWith("[") ? f.Prefab : Lang.LowerFirst(s);
        }

        // ---------------------------------------------------------------- the death

        // From PlayerDeathPatch (your game, before the grave takes the ravens; the trust loss is already written on them)
        internal static void OnDeath(Player p, Character attacker)
        {
            if (p == null || p != Player.m_localPlayer) return;
            Feud old = Mine();
            if (old != null) End(p); // a new death: the old vendetta is over, paid or not
            if (!Enabled || attacker == null || attacker == p || attacker.IsTamed()) return; // (a tame beast: nobody's killer to avenge)
            ItemDrop.ItemData munin = Trust.Worn(true);
            if (munin == null || !Trust.Active(munin) || Trust.Away(munin) || Trust.Stranger(munin, p) || Trust.SettledLevel(munin) < UnlockLevel) return;
            ZNetView nv = attacker.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            Feud f = new Feud();
            f.Target = nv.GetZDO().m_uid;
            f.IsPlayer = attacker is Player;
            f.Prefab = Utils.GetPrefabName(attacker.gameObject);
            f.Token = HitInfo.Token(attacker);
            f.Name = f.IsPlayer ? ((Player)attacker).GetPlayerName() : "";
            f.DeathAt = p.transform.position;
            f.LastAt = attacker.transform.position;
            f.Minutes = WindowMinutes(munin);
            // what the death took from each raven on the shoulder (Trust.OnDeath has just written it on them)
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData r = Trust.Worn(m);
                string id = r != null ? Backup.Id(r) : null;
                float due, lost = r != null ? Trust.DeathLost(r, out due) : 0f;
                if (!string.IsNullOrEmpty(id) && lost > 0f) f.Ravens[id] = new float[] { lost, 0f };
            }
            SnapSkills(p);
            _feud = f; _feudOf = p.GetPlayerID();
            _diedAt = Time.time; _aliveSince = -1f; _graveTagged = false; _missingSince = -1f;
            Save(p);
            Mark(f.Target, p.GetPlayerID());
            if (Plugin.Instance != null) Plugin.Instance.Log("Blood revenge: Munin marks " + f.Prefab + " (" + f.Target + "), " + f.Minutes + " min once back on your feet");
        }

        // the skills as they are just before the death takes its share (Skills.OnDeath runs after this)
        private static void SnapSkills(Player p)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Skills.Skill s in p.GetSkills().GetSkillList())
            {
                if (s == null || s.m_info == null) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append((int)s.m_info.m_skill).Append(':').Append(s.m_level.ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(s.m_accumulator.ToString("R", CultureInfo.InvariantCulture));
            }
            p.m_customData[SkillsKey] = sb.ToString();
        }

        // the skills back to what they were before the death (only those the death lowered; what grew since stays);
        // the levels given back in all
        private static float RestoreSkills(Player p)
        {
            string raw;
            if (!p.m_customData.TryGetValue(SkillsKey, out raw) || string.IsNullOrEmpty(raw)) return 0f;
            Dictionary<int, float[]> was = new Dictionary<int, float[]>();
            foreach (string part in raw.Split(';'))
            {
                string[] v = part.Split(':');
                int t; float lv, acc;
                if (v.Length == 3 && int.TryParse(v[0], out t) && float.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lv)
                    && float.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out acc)) was[t] = new float[] { lv, acc };
            }
            float back = 0f;
            foreach (Skills.Skill s in p.GetSkills().GetSkillList())
            {
                float[] w;
                if (s == null || s.m_info == null || !was.TryGetValue((int)s.m_info.m_skill, out w) || s.m_level >= w[0] - 0.001f) continue;
                back += w[0] - s.m_level;
                s.m_level = w[0];
                s.m_accumulator = w[1];
            }
            p.m_customData.Remove(SkillsKey);
            return back;
        }

        // ---------------------------------------------------------------- the mark on the killer (the game that runs it)

        private static void Mark(ZDOID target, long by)
        {
            if (ZRoutedRpc.instance == null || target == ZDOID.None) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(target);
            pkg.Write(by);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcMark, pkg); // (this game too: it may be the one running it)
        }

        private static void OnMark(long sender, ZPackage pkg)
        {
            ZDOID target = pkg.ReadZDOID();
            long by = pkg.ReadLong();
            ZDO z = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(target) : null;
            if (z != null && z.IsOwner()) z.Set(ZdoBy, by);
        }

        // From CreatureDeathPatch / PlayerDeathPatch, in the game that runs the one dying: someone's vendetta on it — say
        // where it died (the avenger's game judges the rest)
        internal static void Died(Character c)
        {
            if (c == null || ZRoutedRpc.instance == null) return;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            long by = nv.GetZDO().GetLong(ZdoBy, 0L);
            if (by == 0L) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(by);
            pkg.Write(nv.GetZDO().m_uid);
            pkg.Write(c.transform.position);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcDied, pkg);
        }

        private static void OnDied(long sender, ZPackage pkg)
        {
            pkg.ReadLong(); // (the avenger its ZDO named: the last of them only — the creature itself is what counts below)
            ZDOID who = pkg.ReadZDOID();
            Vector3 at = pkg.ReadVector3();
            Player p = Player.m_localPlayer;
            Feud f = Mine();
            // the creature is what counts, not the name in its ZDO: one troll that killed two players holds only the
            // last of them there, and both are avenged by its death
            if (p == null || f == null || f.Paid || who != f.Target) return;
            f.LastAt = at;
            bool near = !p.IsDead() && f.Started > 0.0 && Vector3.Distance(p.transform.position, at) <= NearRange;
            if (near && Left(f) > 0f) Pay(p, f);
            else Lost(p, f, f.Started <= 0.0 || p.IsDead() ? "dead_away" : "far");
        }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcMark, new Action<long, ZPackage>(OnMark));
            ZRoutedRpc.instance.Register<ZPackage>(RpcDied, new Action<long, ZPackage>(OnDied));
        }

        // ---------------------------------------------------------------- every half second (your game)

        internal static void Tick(Player p)
        {
            Feud f = Mine();
            if (f == null || p == null) { ClearPin(); return; }
            if (f.Paid) { ClearPin(); return; } // (only the trust of ravens still in the grave is left: BackFromGrave)
            if (!_graveTagged && _diedAt >= 0f && Time.time - _diedAt > 1f) TagGrave(p, f);
            if (p.IsDead()) { _aliveSince = -1f; return; }
            if (f.Started <= 0.0)
            {
                // back on your feet: the window starts now (a few seconds in, once the world is up)
                if (_aliveSince < 0f) { _aliveSince = Time.time; return; }
                if (Time.time - _aliveSince < 4f) return;
                f.Started = WorldTime.Now;
                Save(p);
                Begin(p, f);
                return;
            }
            if (Left(f) <= 0f) { Lost(p, f, "time"); return; }
            Follow(p, f);
        }

        // the window opens: the word, the effect with its time
        private static void Begin(Player p, Feud f)
        {
            TagGrave(p, f); // (its hunt now lasts as long as the window)
            Effect(p, f);
            ScreamFx.CawAll(p, true, 0.8f, 0.8f);
            Tell(p, "revenge_start", f, Lang.F("Кровна помста: Мунін позначив {0}.\nВбий за {1} — і він поверне навички, втрачені через смерть.", Who(f, true), RavenAbilities.Span(f.Minutes * 60f)));
        }

        // the news in the middle of the screen, and Munin's word with it: from the shoulder, or (in the grave) under the news
        private static void Tell(Player p, string tag, Feud f, string news)
        {
            string line = Line(p, tag, f);
            if (MuninOnShoulder(p)) { if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(p, true, line); RavenMsg.Center(p, news); return; }
            RavenMsg.Center(p, string.IsNullOrEmpty(line) ? news : news + "\n" + Lang.F("{0} з могили: «{1}»", Plugin.RavenName(true), line));
        }

        private static bool MuninOnShoulder(Player p)
        {
            ItemDrop.ItemData m = Trust.Worn(true);
            return m != null && Trust.Active(m) && !Trust.Away(m) && Shoulder.Arrived(p, true);
        }

        // where it is: the runes over its head, the pin, Munin's look and caw; gone — its kin takes the mark, or it got away
        private static void Follow(Player p, Feud f)
        {
            if (Time.time < _nextLook) return;
            _nextLook = Time.time + 0.5f;
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(f.Target) : null;
            Character c = go != null ? go.GetComponent<Character>() : null;
            if (c != null && !c.IsDead())
            {
                _missingSince = -1f;
                f.LastAt = c.transform.position;
                BloodLook.Show(c);
                float d = Vector3.Distance(p.transform.position, f.LastAt);
                if (d < CawRange && MuninOnShoulder(p) && Time.time >= _cawAt)
                {
                    _cawAt = Time.time + UnityEngine.Random.Range(14f, 24f);
                    PerkFx.LookAtFor(true, c.transform, 3f);
                    ScreamFx.CawAll(p, true, 0.85f, 0.75f);
                    if (UnityEngine.Random.value < 0.4f) Say(p, "revenge_near", f);
                }
            }
            else
            {
                ZDO z = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(f.Target) : null;
                if (z != null) { f.LastAt = z.GetPosition(); _missingSince = -1f; }
                else if (Vector3.Distance(p.transform.position, f.LastAt) < GoneRange)
                {
                    // here, and nothing of it: give the word of its death a moment to come, then it is gone
                    if (_missingSince < 0f) _missingSince = Time.time;
                    else if (Time.time - _missingSince > 4f) { _missingSince = -1f; Kin(p, f); return; }
                }
            }
            if (Time.time >= _nextPin) { _nextPin = Time.time + 2f; Pin(f); }
        }

        // the marked one is gone (not killed): one of its kind near the place takes the mark; none — it got away
        private static void Kin(Player p, Feud f)
        {
            Character best = null;
            float bestD = float.MaxValue;
            if (!f.IsPlayer)
                foreach (Character c in Character.GetAllCharacters())
                {
                    if (c == null || c.IsDead() || c is Player || c.IsTamed() || Utils.GetPrefabName(c.gameObject) != f.Prefab) continue;
                    float d = Mathf.Min(Vector3.Distance(c.transform.position, f.LastAt), Vector3.Distance(c.transform.position, f.DeathAt));
                    if (d < KinRange && d < bestD) { bestD = d; best = c; }
                }
            ZNetView nv = best != null ? best.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid()) { Lost(p, f, "gone"); return; }
            f.Target = nv.GetZDO().m_uid;
            f.LastAt = best.transform.position;
            Save(p);
            Mark(f.Target, p.GetPlayerID());
            _graveTagged = false; TagGrave(p, f); // (the grave's Munin hunts the new one)
            if (!Say(p, "revenge_kin", f)) RavenMsg.Center(p, Lang.T("Мунін: «Тварюка здиміла. Але ось її рідня — сойде.»"));
            if (Plugin.Instance != null) Plugin.Instance.Log("Blood revenge: the marked one is gone, one of its kind takes the mark (" + f.Target + ")");
        }

        // ---------------------------------------------------------------- the end

        private static void Pay(Player p, Feud f)
        {
            float skills = RestoreSkills(p);
            // the trust the death cost: whole back for ravens on the shoulder now; ravens still in the grave get theirs
            // when taken out (BackFromGrave)
            float trust = 0f;
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData r = Trust.Worn(m);
                string id = r != null ? Backup.Id(r) : null;
                float[] v;
                if (id == null || !f.Ravens.TryGetValue(id, out v)) continue;
                float give = Mathf.Max(0f, v[0] - v[1]);
                Trust.GiveBackDeath(r, m, give);
                trust += give;
                f.Ravens.Remove(id);
            }
            f.Paid = true;
            Save(p);
            if (f.Ravens.Count == 0) End(p); else { ClearPin(); RemoveEffect(p); UntagGrave(f); }
            ScreamFx.CawAll(p, true, 0.75f, 1f);
            SitEvents.Raise(SitEv.Boss);
            Tell(p, "revenge_paid", f, skills > 0.05f ? Lang.T("Кровну помсту здійснено!\nМунін повернув навички, втрачені через смерть.")
                : Lang.T("Кровну помсту здійснено!\nСмерть цього разу не забрала навичок — Мунін задоволений і так."));
            if (Plugin.Instance != null) Plugin.Instance.Log("Blood revenge paid: skills +" + skills.ToString("0.##", CultureInfo.InvariantCulture) + ", trust +" + trust.ToString("0.#", CultureInfo.InvariantCulture));
        }

        private static void Lost(Player p, Feud f, string why)
        {
            if (Plugin.Instance != null) Plugin.Instance.Log("Blood revenge lost: " + why);
            string tag = why == "time" ? "revenge_late" : why == "gone" ? "revenge_gone" : "revenge_other";
            if (!Say(p, tag, f))
                RavenMsg.Center(p, why == "time" ? Lang.T("Мунін: «Не встиг{|ла}. Гад живе, а ти — як був{|ла}.»") : why == "gone" ? Lang.T("Мунін: «Втекла, падлюка. Нема кого рвати.»") : Lang.T("Мунін: «Ту падлюку завалили без тебе. Не рахується.»"));
            End(p);
        }

        // the vendetta over: the record, the pin, the runes, the effect, the grave's hunt
        private static void End(Player p)
        {
            Feud f = _feud;
            ClearPin();
            if (p != null)
            {
                RemoveEffect(p);
                p.m_customData.Remove(Key);
                p.m_customData.Remove(SkillsKey);
            }
            if (f != null) UntagGrave(f);
            BloodLook.HideAll();
            _feud = null; _diedAt = -1f; _aliveSince = -1f; _graveTagged = true;
        }

        // From RavenSeat (before Trust.BackFromGrave): a raven of this vendetta taken out of the grave. Paid already: its
        // whole loss back (the grave's share is in it). Not yet: the grave's share is noted, the revenge gives the rest.
        internal static void BackFromGrave(Player p, ItemDrop.ItemData item, bool munin)
        {
            Feud f = Mine();
            string id = item != null ? Backup.Id(item) : null;
            float[] v;
            if (f == null || p == null || id == null || !f.Ravens.TryGetValue(id, out v)) return;
            if (f.Paid)
            {
                if (Trust.Active(item) && !Trust.Stranger(item, p)) Trust.GiveBackDeath(item, munin, v[0]);
                f.Ravens.Remove(id);
                if (f.Ravens.Count == 0) End(p); else Save(p);
                return;
            }
            float due;
            Trust.DeathLost(item, out due);
            v[1] = due;
            Save(p);
        }

        // ---------------------------------------------------------------- your blows on it

        internal static float DamageMul(Character target)
        {
            Feud f = _feud;
            if (f == null || f.Paid || f.Started <= 0.0 || target == null) return 1f;
            ZNetView nv = target.GetComponent<ZNetView>();
            return nv != null && nv.IsValid() && nv.GetZDO().m_uid == f.Target ? 1f + DamagePercent / 100f : 1f;
        }

        // ---------------------------------------------------------------- the grave's Munin

        // the grave just made: whom its Munin hunts, till when (every game reads it from the grave)
        private static void TagGrave(Player p, Feud f)
        {
            TombStone t = GraveRavens.OwnGraveNear(p.GetPlayerID(), f.DeathAt, 12f);
            ZNetView nv = t != null ? t.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid()) { if (_diedAt >= 0f && Time.time - _diedAt > 15f) _graveTagged = true; return; } // (no grave: a world that keeps everything)
            _graveTagged = true;
            if (!nv.IsOwner()) return;
            double until = (f.Started > 0.0 ? f.Started : WorldTime.Now + 30.0) + f.Minutes * 60.0;
            nv.GetZDO().Set(GraveTarget, f.Target);
            nv.GetZDO().Set(GraveUntil, (long)(until * 1000.0));
        }

        private static void UntagGrave(Feud f)
        {
            Player p = Player.m_localPlayer;
            TombStone t = p != null ? GraveRavens.OwnGraveNear(p.GetPlayerID(), f.DeathAt, 12f) : null;
            ZNetView nv = t != null ? t.GetComponent<ZNetView>() : null;
            if (nv != null && nv.IsValid() && nv.IsOwner()) nv.GetZDO().Set(GraveUntil, 0L);
        }

        // From GraveBird (any game): the one this grave's Munin hunts now, if near enough
        internal static Character HuntOf(TombStone t)
        {
            ZNetView nv = t != null ? t.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || ZNetScene.instance == null) return null;
            ZDO z = nv.GetZDO();
            long until = z.GetLong(GraveUntil, 0L);
            if (until <= 0L || WorldTime.Now * 1000.0 > until) return null;
            ZDOID id = z.GetZDOID(GraveTarget);
            if (id == ZDOID.None) return null;
            GameObject go = ZNetScene.instance.FindInstance(id);
            Character c = go != null ? go.GetComponent<Character>() : null;
            if (c == null || c.IsDead() || Vector3.Distance(c.transform.position, t.transform.position) > HuntRange) return null;
            return c;
        }

        // ---------------------------------------------------------------- the look

        private static void Pin(Feud f)
        {
            if (Minimap.instance == null) return;
            string label = Lang.F("Помста: {0}", Who(f, false));
            if (_pin != null && _pin.m_name == label) { _pin.m_pos = f.LastAt; return; }
            ClearPin();
            _pin = Minimap.instance.AddPin(f.LastAt, Minimap.PinType.Shout, label, false, false, 0L, default(Splatform.PlatformUserID));
            RavenSkills.Icon(_pin, AbilityIcons.For("mrevenge", true));
        }

        private static void ClearPin()
        {
            if (_pin != null && Minimap.instance != null) Minimap.instance.RemovePin(_pin);
            _pin = null;
        }

        private static void Effect(Player p, Feud f)
        {
            SE_Stats se = RavenFx.Template(ref _effect, EffectName);
            se.m_name = Lang.W("Кровна помста");
            se.m_tooltip = Lang.F("Мунін позначив {0}. Вбий, поки не мине час (ти маєш бути поруч, добити може й друг), — і він поверне навички, втрачені через смерть. По ньому ти б'єш на {1}% сильніше.", Who(f, true), RavenAbilities.R(DamagePercent));
            se.m_icon = AbilityIcons.ForEffect("mrevenge", true);
            se.m_ttl = Mathf.Max(1f, Left(f));
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        private static void RemoveEffect(Player p)
        {
            int hash = EffectName.GetStableHashCode();
            if (p.GetSEMan().HaveStatusEffect(hash)) p.GetSEMan().RemoveStatusEffect(hash, true);
        }

        // ---------------------------------------------------------------- words

        private static string Line(Player p, string tag, Feud f)
        {
            if (Plugin.Instance == null) return null;
            Composer.Extra.Clear();
            try
            {
                if (f.IsPlayer) { Composer.Extra["killer"] = f.Name; Composer.Extra["killer_gen"] = f.Name; }
                else Composer.ExtraCreature("killer", f.Token);
                Composer.Extra["left"] = RavenAbilities.Span(f.Started > 0.0 ? Left(f) : f.Minutes * 60f);
                string line = Plugin.Instance.ComposeTagged(p, true, tag);
                return string.IsNullOrEmpty(line) ? null : Grammar.Fix(line, p);
            }
            finally { Composer.Extra.Clear(); }
        }

        // Munin's word: from the shoulder, or from the grave as a call in the middle of the screen
        private static bool Say(Player p, string tag, Feud f)
        {
            string line = Line(p, tag, f);
            if (string.IsNullOrEmpty(line)) return false;
            if (MuninOnShoulder(p)) RavenSkills.SayFromShoulder(p, true, line);
            else RavenMsg.Center(p, Lang.F("{0} з могили: «{1}»", Plugin.RavenName(true), line));
            return true;
        }

        // ---------------------------------------------------------------- kept in the character

        private static void Save(Player p)
        {
            Feud f = _feud;
            if (p == null || f == null) return;
            StringBuilder sb = new StringBuilder("1|");
            sb.Append(f.Target.UserID).Append(':').Append(f.Target.ID).Append('|');
            sb.Append(Clean(f.Prefab)).Append('|').Append(Clean(f.Token)).Append('|').Append(Clean(f.Name)).Append('|');
            sb.Append(f.IsPlayer ? '1' : '0').Append('|');
            sb.Append(V(f.DeathAt)).Append('|').Append(V(f.LastAt)).Append('|');
            sb.Append(f.Started.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            sb.Append(f.Minutes.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            sb.Append(f.Paid ? '1' : '0').Append('|');
            bool first = true;
            foreach (KeyValuePair<string, float[]> kv in f.Ravens)
            {
                if (!first) sb.Append(';');
                first = false;
                sb.Append(Clean(kv.Key)).Append(':').Append(kv.Value[0].ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(kv.Value[1].ToString("R", CultureInfo.InvariantCulture));
            }
            p.m_customData[Key] = sb.ToString();
        }

        private static Feud Load(Player p)
        {
            string raw;
            if (p == null || !p.m_customData.TryGetValue(Key, out raw) || string.IsNullOrEmpty(raw)) return null;
            string[] v = raw.Split('|');
            if (v.Length < 12 || v[0] != "1") return null;
            try
            {
                Feud f = new Feud();
                string[] id = v[1].Split(':');
                f.Target = new ZDOID(long.Parse(id[0], CultureInfo.InvariantCulture), uint.Parse(id[1], CultureInfo.InvariantCulture));
                f.Prefab = v[2]; f.Token = v[3]; f.Name = v[4]; f.IsPlayer = v[5] == "1";
                f.DeathAt = P(v[6]); f.LastAt = P(v[7]);
                f.Started = double.Parse(v[8], CultureInfo.InvariantCulture);
                f.Minutes = float.Parse(v[9], CultureInfo.InvariantCulture);
                f.Paid = v[10] == "1";
                foreach (string r in v[11].Split(';'))
                {
                    string[] q = r.Split(':');
                    if (q.Length == 3) f.Ravens[q[0]] = new float[] { float.Parse(q[1], CultureInfo.InvariantCulture), float.Parse(q[2], CultureInfo.InvariantCulture) };
                }
                // a window from another world's clock: forgotten (one long over in this world ends as «too late» next tick)
                if (f.Started > WorldTime.Now + 60.0) { p.m_customData.Remove(Key); return null; }
                return f;
            }
            catch (Exception) { p.m_customData.Remove(Key); return null; }
        }

        private static string Clean(string s) { return (s ?? "").Replace('|', '/').Replace(';', ',').Replace(':', '.'); }
        private static string V(Vector3 v) { return v.x.ToString("0.##", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.##", CultureInfo.InvariantCulture) + "," + v.z.ToString("0.##", CultureInfo.InvariantCulture); }
        private static Vector3 P(string s)
        {
            string[] c = s.Split(',');
            return c.Length == 3 ? new Vector3(float.Parse(c[0], CultureInfo.InvariantCulture), float.Parse(c[1], CultureInfo.InvariantCulture), float.Parse(c[2], CultureInfo.InvariantCulture)) : Vector3.zero;
        }

        // a new world or character
        internal static void Reset() { ClearPin(); _feud = null; _feudOf = 0L; _diedAt = -1f; _aliveSince = -1f; _graveTagged = true; BloodLook.HideAll(); }

        // the test panel: the marked one is the creature you look at (or the nearest), the window open at once
        internal static string Test(Player p)
        {
            if (p == null || p.IsDead()) return Lang.T("Персонаж мертвий.");
            Character best = null;
            float bestD = 40f;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == p || c.IsDead() || c.IsTamed()) continue;
                float d = Vector3.Distance(c.transform.position, p.transform.position);
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best == null) return Lang.T("Поруч (40 м) нема істоти, яку позначити.");
            OnDeath(p, best);
            if (_feud == null) return Lang.T("Мунін не позначив: його нема на плечі, він відлетів або довіра замала.");
            _feud.Ravens.Clear(); // (no death here: nothing of an old one to give back)
            _diedAt = -1f; _graveTagged = true;
            _feud.Started = WorldTime.Now;
            Save(p);
            Begin(p, _feud);
            return Lang.F("Позначено: {0}.", Who(_feud, false));
        }
    }

    // Munin's mark over the marked one's head: a slow ring of red runes (only in the avenger's game)
    internal class BloodLook : MonoBehaviour
    {
        private const string Name = "RavenWatch_BloodMark";
        private static readonly List<BloodLook> All = new List<BloodLook>();
        private ParticleSystem _ps;
        private Character _c;
        private float _until;

        internal static void Show(Character c)
        {
            if (c == null) return;
            Transform old = c.transform.Find(Name);
            BloodLook look = old != null ? old.GetComponent<BloodLook>() : null;
            if (look == null)
            {
                GameObject go = new GameObject(Name);
                go.transform.SetParent(c.transform, false);
                look = go.AddComponent<BloodLook>();
                look._c = c;
                look.Build();
                All.Add(look);
            }
            look._until = Time.time + 1.5f; // (shown again every half second while marked)
        }

        internal static void HideAll()
        {
            foreach (BloodLook l in All) if (l != null) l._until = 0f;
        }

        private void Build()
        {
            Material m = DomeLook.RuneMaterialPublic();
            if (m == null) return;
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _ps.main;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            float r = _c != null ? Mathf.Max(0.3f, Body.Radius(_c)) : 0.5f;
            float big = Mathf.Clamp(r / 0.5f, 1f, 4f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f * big, 0.42f * big);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.05f, 0.05f, 1f), new Color(1f, 0.3f, 0.2f, 1f));
            main.maxParticles = 16;
            ParticleSystem.EmissionModule em = _ps.emission; em.rateOverTime = 5f;
            ParticleSystem.ShapeModule sh = _ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = Mathf.Max(0.4f, r * 0.8f); sh.radiusThickness = 0f;
            ParticleSystem.VelocityOverLifetimeModule vel = _ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(-1.2f); // slow, the other way round from Hugin's blinding runes
            ParticleSystem.TextureSheetAnimationModule sheet = _ps.textureSheetAnimation;
            sheet.enabled = true; sheet.numTilesX = 4; sheet.numTilesY = 4; sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
            Vector3 head = _c != null ? Body.Head(_c) : transform.position + Vector3.up * 1.8f;
            transform.position = head + Vector3.up * (0.7f + r * 0.4f);
            transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule col = _ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystemRenderer pr = GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            _ps.Play();
        }

        private void Update()
        {
            if (_c == null || _c.IsDead() || Time.time >= _until)
            {
                if (_ps != null && _ps.isEmitting) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (_c == null || Time.time >= _until + 2.5f) Destroy(gameObject);
            }
            else if (_ps != null && !_ps.isEmitting) _ps.Play();
        }

        private void OnDestroy() { All.Remove(this); }
    }

    // the snapshot of the skills must be taken before the death lowers them: Skills.OnDeath runs inside Player.OnDeath,
    // after PlayerDeathPatch's prefix — so that prefix calls OnDeath (BloodRevenge), and this is only the blows
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class BloodRevengeHitPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            Player me = Player.m_localPlayer;
            if (me == null || hit == null || __instance == me || hit.m_attacker != me.GetZDOID()) return;
            if (hit.m_skillRaiseAmount <= Strike.Marker) return; // a raven's own blow
            if (!Marks.RealBlow(hit)) return;
            float mul = BloodRevenge.DamageMul(__instance);
            if (mul != 1f) hit.ApplyModifier(mul);
        } catch (Exception e) { PatchGuard.Fail("BloodRevengeHitPatch", e); } }
    }
}
#endif
