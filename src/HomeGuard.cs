#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Вдома», on guard (see Home.cs): after it has settled in and only when fed, a blow at an angry enemy it sees
    // near the perch, paid from its guard store; the flight (HomeStrike) seen by everyone near, the blow dealt by the
    // perch's holder; a call to you when you are away.
    internal static partial class Home
    {
        // ---------------------------------------------------------------- on guard: a blow at an angry enemy it sees near the perch

        internal static float Radius(string mode) { return mode == OnGuard ? GuardRadius : HomeRadius; }
        internal static float Cost(string mode) { return mode == OnGuard ? GuardCost : HomeCost; } // (a hungry one does not fly at all: Fed)

        // a fight at the perch now (a raid on it, an angry enemy near): no raven stays to guard in the middle of it
        internal static string Fighting(PerchView v, bool munin)
        {
            Vector3 at = v.transform.position;
            RandomEvent raid = RandEventSystem.instance != null ? RandEventSystem.instance.GetActiveEvent() : null;
            bool fight = raid != null && Vector3.Distance(at, raid.m_pos) < 96f;
            if (!fight)
                foreach (Character c in Character.GetAllCharacters())
                    if (Hostile(c) && Angry(c) && Vector3.Distance(c.transform.position, at) < FightNear) { fight = true; break; }
            return fight ? Lang.F("{0} не сяде — довкола бій.\nВорона лишають стерегти заздалегідь, а не посеред бою.", Plugin.RavenName(munin)) : null;
        }

        // an enemy on the hunt (it has seen someone): one passing by quietly is let be
        internal static bool Angry(Character c) { return !Strike.Unaware(c); }

        // an enemy: a monster, not an animal of the meadows, not a tamed one, not a player's
        internal static bool Hostile(Character c)
        {
            if (c == null || c.IsDead() || c is Player || c.IsTamed()) return false;
            Character.Faction f = c.GetFaction();
            if (f == Character.Faction.Players || f == Character.Faction.PlayerSpawned || f == Character.Faction.Dverger || f == Character.Faction.AnimalsVeg) return false;
            if (c.GetBaseAI() == null || Strike.BeingTamed(c)) return false;
            ZNetView nv = c.GetComponent<ZNetView>();
            return nv != null && nv.IsValid();
        }

        // How often it looks round: an enemy was near at the last look — every second; all quiet — every few seconds;
        // it could not fly anyway (settling in, hungry, its store short, off hunting) — it does not look at all.
        internal static float LookBusy = 1f, LookQuiet = 3f, LookIdle = 5f;

        // the angry enemies near a perch: one look shared by its seats in the same moment (nearest first)
        private static readonly List<Character> _near = new List<Character>(8);
        private static readonly List<float> _nearD = new List<float>(8);
        private static PerchView _nearFor;
        private static int _nearFrame = -1;
        private static List<Character> Near(PerchView v)
        {
            if (_nearFor == v && _nearFrame == Time.frameCount) return _near;
            _nearFor = v; _nearFrame = Time.frameCount;
            _near.Clear(); _nearD.Clear();
            Vector3 at = v.transform.position;
            float far = GuardRadius + 3f, rr = far * far; // (the widest any seat strikes, and a little: the seats sit off its middle)
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null) continue;
                float d = (c.transform.position - at).sqrMagnitude;
                if (d > rr || !Hostile(c) || !Angry(c)) continue; // the cheap distance first, then who it is
                int k = 0;
                while (k < _nearD.Count && _nearD[k] <= d) k++;
                _near.Insert(k, c); _nearD.Insert(k, d);
            }
            return _near;
        }

        private static void Guard(PerchView v, int seat, Perches.Occupant o, ItemDrop.ItemData item, Seen s)
        {
            s.NextBlow = Time.time + LookIdle;
            if (!Trust.Active(item) || Hunting(v, seat) || Settling(item) > 0f || !Fed(item)) return; // settling in, not fed, away: it does not fly, whatever its store
            float cost = Cost(o.Mode);
            if (Store(item) < cost) return;
            s.NextBlow = Time.time + LookBusy;
            if (v.Busy(seat)) return; // (off on a blow now: back in a moment)
            List<Character> near = Near(v);
            if (near.Count == 0) { s.NextBlow = Time.time + LookQuiet; return; }
            Vector3 at = v.SeatT(seat).position + Vector3.up * 0.15f;
            float rr = Radius(o.Mode) * Radius(o.Mode);
            Character target = null;
            foreach (Character c in near) // nearest first: the first one in reach it sees is the one
            {
                if (c == null || c.IsDead() || (c.transform.position - at).sqrMagnitude > rr) continue;
                if (Strike.SeenFromAny(at, c)) { target = c; break; } // (not through a wall: indoors it guards only the room)
            }
            if (target == null) return;
            s.NextBlow = Time.time + AttackGap;
            SetStore(item, Store(item) - cost);
            Satiety.Change(item, -cost * Satiety.GiftShare);
            Tally(item, 1, 1f); Tally(item, 5, cost);
            Alarm(v, seat, o, target, item, s);
            s.Serial++;
            ZDOID id = target.GetZDOID();
            v.Zdo.Set(KeyA(seat), s.Serial + "|" + id.UserID + ":" + id.ID);
            s.Dirty = true; s.WriteAt = 0f; // written at once: what it has left shows
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) Hit(v, seat, target); // nobody draws it here: the blow at once
        }

        // every game near: a new blow mark → its bird flies at that one and back (the holder's bird deals the blow)
        internal static void WatchBlow(PerchView v, int seat)
        {
            string mark = v.Zdo.GetString(KeyA(seat), "");
            Seen s = SeenAt(v, seat);
            if (mark == s.Blow) return;
            bool first = s.Blow.Length == 0 && Time.time - v.Born < 2f; // (a mark from before this perch was loaded here: old news)
            s.Blow = mark;
            if (first || mark.Length == 0) return;
            string[] w = mark.Split('|');
            string[] z = w.Length > 1 ? w[1].Split(':') : new string[0];
            long user; uint idn;
            if (z.Length < 2 || !long.TryParse(z[0], out user) || !uint.TryParse(z[1], out idn)) return;
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(new ZDOID(user, idn)) : null;
            Character c = g != null ? g.GetComponent<Character>() : null;
            if (c == null) return;
            // the cry and the crouch first (every game: the same seed from the mark), then the flight
            GameObject bird = v.BirdAt(seat);
            PerchLife life = bird != null ? bird.GetComponent<PerchLife>() : null;
            int serial;
            int.TryParse(w[0], out serial);
            float wind = life != null ? life.Windup(serial * 7 + seat) : 0f;
            if (wind > 0.05f && Plugin.Instance != null) { Plugin.Instance.StartCoroutine(LaunchAfter(v, seat, c, wind * 0.85f)); return; }
            if (!HomeStrike.Launch(v, seat, c) && v.Owner) Hit(v, seat, c); // no bird to fly here: the blow all the same
        }

        private static System.Collections.IEnumerator LaunchAfter(PerchView v, int seat, Character c, float wait)
        {
            yield return new WaitForSeconds(wait);
            if (v == null || !v.Valid || c == null || c.IsDead()) yield break;
            if (!HomeStrike.Launch(v, seat, c) && v.Owner) Hit(v, seat, c);
        }

        // the blow itself: the raven's own (Strike), never more than a small share of a boss
        internal static void Hit(PerchView v, int seat, Character t)
        {
            if (v == null || !v.Owner || t == null || t.IsDead()) return;
            Perches.Occupant o = v.At(seat);
            ItemDrop.ItemData item = Cache(v, seat);
            if (o == null || item == null) return;
            bool munin = o.Munin;
            float blow = Strike.Blow(munin, item);
            bool crit = UnityEngine.Random.value * 100f < Strike.CritChance(munin, item);
            if (crit) blow *= 2f;
            if (t.IsBoss()) blow = Mathf.Min(blow, t.GetMaxHealth() * (munin ? Strike.BossCapMunin : Strike.BossCapHugin));
            HitData hit = new HitData();
            if (munin) hit.m_damage.m_slash = blow; else hit.m_damage.m_pierce = blow;
            hit.m_point = Body.Center(t);
            Vector3 d = Body.Center(t) - v.SeatT(seat).position;
            hit.m_dir = d.sqrMagnitude > 0.001f ? d.normalized : t.transform.forward;
            hit.m_pushForce = munin ? 12f : 4f;
            hit.m_staggerMultiplier = munin ? 1.2f : 0.4f;
            hit.m_blockable = false; hit.m_dodgeable = false; hit.m_ranged = true;
            hit.m_skill = Skills.SkillType.None;
            hit.m_skillRaiseAmount = Strike.Marker - blow; // cut back to its own size in the target's game
            t.Damage(hit);
            Strike.AddFlight(item);
            Seen s = SeenAt(v, seat);
            s.Dirty = true;
            if (crit && DamageText.instance != null) DamageText.instance.ShowText(DamageText.TextType.Weak, hit.m_point + Vector3.up * 0.6f, Lang.T("КРИТ ×2"));
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(KillCheck(v, seat, t));
        }

        private static System.Collections.IEnumerator KillCheck(PerchView v, int seat, Character t)
        {
            yield return new WaitForSeconds(1.5f);
            if (v == null || !v.Owner) yield break;
            if (t == null || t.IsDead()) { ItemDrop.ItemData item = Cache(v, seat); if (item != null) { Tally(item, 0, 1f); SeenAt(v, seat).Dirty = true; } }
        }

        // you are away (in the game, far from the perch): told, a mark on the map
        private static void Alarm(PerchView v, int seat, Perches.Occupant o, Character target, ItemDrop.ItemData item, Seen s)
        {
            if (Time.time < s.AlarmAt || ZRoutedRpc.instance == null) return;
            Player owner = PerchView.PlayerOf(o.Pid);
            if (owner != null && Vector3.Distance(owner.transform.position, v.transform.position) < AlarmFar) return; // you are here: you see it
            s.AlarmAt = Time.time + AlarmGap; // (sent to all: its owner's game, if it is in, shows it; nobody else does)
            Tally(item, 7, 1f);
            ZPackage pkg = new ZPackage();
            pkg.Write(o.Pid); pkg.Write(o.Munin); pkg.Write(v.transform.position); pkg.Write(target.m_name ?? ""); pkg.Write(v.Name); pkg.Write(o.Token);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcAlarm, pkg);
        }

        private static readonly List<Minimap.PinData> _pins = new List<Minimap.PinData>();
        private static void OnAlarm(long sender, ZPackage pkg)
        {
            Player p = Player.m_localPlayer;
            long pid = pkg.ReadLong(); bool munin = pkg.ReadBool(); Vector3 at = pkg.ReadVector3(); string who = pkg.ReadString(); string perch = pkg.ReadString();
            string token = pkg.GetPos() < pkg.Size() ? pkg.ReadString() : "";
            if (p == null || p.GetPlayerID() != pid) return;
            string name = Localization.instance != null ? Lang.Game(who) : who;
            int level = 1;
            foreach (Note n in Notes(p)) if (n.Token == token) level = Mathf.Max(1, n.Level);
            Dictionary<string, string> extra = new Dictionary<string, string>();
            extra["foe"] = name;
            string said = Plugin.Instance != null ? Composer.ComposePerch(Plugin.Instance.PhraseDir, munin, "home_alarm", level, p.GetPlayerName(), Grammar.Female(p), extra) : null;
            p.Message(MessageHud.MessageType.Center, (string.IsNullOrEmpty(said) ? Lang.F("{0}: на хаті гості — {1}!", Plugin.RavenName(munin), name) : Plugin.RavenName(munin) + ": " + said) + Lang.F("\nВін б'ється біля «{0}».", perch));
            if (Minimap.instance == null) return;
            foreach (Minimap.PinData old in _pins) if (old != null) Minimap.instance.RemovePin(old);
            _pins.Clear();
            _pins.Add(Minimap.instance.AddPin(at, Minimap.PinType.Shout, Plugin.RavenName(munin) + ": " + name, false, false, 0L, default(Splatform.PlatformUserID)));
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(PinOff(_pins[0]));
        }

        private static System.Collections.IEnumerator PinOff(Minimap.PinData pin)
        {
            yield return new WaitForSeconds(180f);
            if (Minimap.instance != null && pin != null) Minimap.instance.RemovePin(pin);
            _pins.Remove(pin);
        }

    }
}
#endif
