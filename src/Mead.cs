#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // «Медовуха» (the user, 2026-10-27): you drink a mead, and a raven of yours on you or by you sips it now and then
    // (Munin more often, its norov too; never twice within GapMinutes). For a minute it is tipsy: it flies crooked (Motor:
    // its line sways, RavenFlight: it rolls; from the owner's ZDO, so every game flies it alike), it hiccups on the shoulder
    // (Здригається), a hiccup breaks into its lines (Slur), and it says drunk things (MeadPatterns). Right after the sip,
    // sitting idle, it takes a short crooked circle over you. Its sips count for its nickname (Сомельє / Алкаш).
    internal static class Mead
    {
        internal static float Seconds = 60f, FadeSeconds = 8f;        // tipsy this long, sobering over the last seconds
        internal static float ChanceH = 0.25f, ChanceM = 0.5f;        // a sip of one mead (times its norov's thirst)
        internal static float GapMinutes = 15f;                       // after a sip: no more for this raven so soon
        internal static float CircleAfter = 3f, CircleSeconds = 18f;  // the crooked circle over you after the sip
        internal static float SwayMetres = 1.4f, RollDegrees = 16f;   // how far its line sways, how hard it rolls
        internal static float SayMin = 12f, SayMax = 22f, SayChance = 0.6f; // a drunk line now and then
        internal static float HiccupMin = 6f, HiccupMax = 14f;        // a hiccup on the shoulder
        internal static float SlurChance = 0.45f;                     // a hiccup into one of its lines
        internal const int NickAt = 10;                               // sips for the nickname
        internal const string NetKey = "RavenWatch_Mead";             // the owner's ZDO: Hugin's end (world seconds) low, Munin's high

        private static readonly float[] Until = new float[2], GapUntil = { -1f, -1f }, NextSay = new float[2], NextHic = new float[2];
        private static readonly float[] CircleAt = { -1f, -1f }, CircleEnd = { -1f, -1f };
        private static readonly string[] Who = { "", "" }; // the raven that sipped (Backup id): another one put on is sober
        private static long _player;
        private static float _look;

        // how much the norov likes a drink
        private static float Thirst(ItemDrop.ItemData item)
        {
            Norov.Character c = Norov.CharOf(item);
            if (c == null) return 1f;
            switch (c.Id)
            {
                case "bold": case "fidget": case "loud": return 1.4f;
                case "wanderer": case "showoff": case "thief": return 1.2f;
                case "calm": case "timid": case "picky": return 0.6f;
                case "homebody": case "watchful": case "sleepy": return 0.8f;
                default: return 1f;
            }
        }

        // a mead drunk (Diary's PotionPatch, your own game)
        internal static void Drank(Player p)
        {
            if (p == null || p != Player.m_localPlayer || p.IsDead()) return;
            bool h = Sip(p, false, 0f);
            Sip(p, true, h ? 1.5f : 0f); // (both: Munin a moment later)
        }

        // console: `raven mead [h|m]` — it sips now (no chance, no gap)
        internal static string Cmd(Player p, string[] a)
        {
            bool munin = a.Length > 1 && (a[1] == "m" || a[1] == "munin");
            return Sip(p, munin, 0f, true) ? Plugin.RavenName(munin) + " sipped: tipsy for " + Mathf.RoundToInt(Seconds) + " s" : "raven mead: no " + Plugin.RavenName(munin) + " on you or by you";
        }

        private static bool Sip(Player p, bool munin, float later, bool force = false)
        {
            int i = munin ? 1 : 0;
            if (!force && Time.time < GapUntil[i]) return false;
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null || !Trust.Active(item) || Trust.Away(item) || Trust.Stranger(item, p)) return false;
            ShoulderLife life = Shoulder.LifeOf(p, munin);
            if (life == null) return false; // (not out with you)
            if (!force && Random.value >= (munin ? ChanceM : ChanceH) * Thirst(item)) return false;
            GapUntil[i] = Time.time + GapMinutes * 60f;
            Until[i] = Time.time + Seconds;
            Who[i] = Backup.Id(item) ?? "";
            Publish(p);
            if (!force) { Nicks.Add(item, "sips", 1); Habits.Sipped(item); } // (the console's sips earn no nickname, nor a habit)
            RavenJournal.Note(p, item, "sipped your mead");
            if (life.SitNew) life.SitDirectorNow.React(life, "Витирає дзьоб", 1.2f + later);
            PerkFx.Say(munin, "mead_sip", 0.8f, 0f);
            NextSay[i] = Time.time + later + Random.Range(8f, 14f);
            NextHic[i] = Time.time + later + Random.Range(4f, 8f);
            CircleAt[i] = Time.time + CircleAfter + later;
            return true;
        }

        // the ends of both ravens' minutes on your ZDO, in world seconds: every game sees the same tipsy bird
        private static void Publish(Player p)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            double now = WorldTime.Now;
            long h = Until[0] > Time.time ? (uint)(now + (Until[0] - Time.time)) : 0u;
            long m = Until[1] > Time.time ? (uint)(now + (Until[1] - Time.time)) : 0u;
            z.Set(NetKey, h | (m << 32));
        }

        // this player's raven tipsy now: 1 drunk … 0 sober (it sobers over the last FadeSeconds)
        internal static float Of(Player owner, bool munin)
        {
            ZDO z = owner != null ? NetFlag.Zdo(owner, false) : null;
            if (z == null) return 0f;
            long v = z.GetLong(NetKey, 0L);
            if (v == 0L) return 0f;
            double end = (v >> (munin ? 32 : 0)) & 0xFFFFFFFFL;
            double left = end - WorldTime.Now;
            if (left <= 0.0 || left > Seconds + 5f) return 0f; // (gone, or a clock that jumped)
            return Mathf.Clamp01((float)left / FadeSeconds);
        }

        internal static bool Tipsy(bool munin) { return Until[munin ? 1 : 0] > Time.time; }

        // a hiccup into a line of a tipsy raven of yours: between two words, now and then
        internal static string Slur(Player p, bool munin, string line)
        {
            if (p != Player.m_localPlayer || !Tipsy(munin) || string.IsNullOrEmpty(line) || line.IndexOf('<') >= 0 || Random.value >= SlurChance) return line;
            int a = line.IndexOf(' '), b = line.LastIndexOf(' ');
            if (a <= 0) return line + " " + Lang.T("Ик.");
            int at = line.IndexOf(' ', Random.Range(a, b + 1)); // (b is a space: always found)
            string hic = Lang.T("ик") + "… ";
            return char.IsLetterOrDigit(line[at - 1]) ? line.Substring(0, at) + "… " + hic + line.Substring(at + 1) : line.Substring(0, at + 1) + hic + line.Substring(at + 1);
        }

        // from the skills' Update (your own game), four times a second
        internal static void Tick()
        {
            if (Time.time < _look) return;
            _look = Time.time + 0.25f;
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (p.GetPlayerID() != _player) // another character: nothing of the last one's mead
            {
                _player = p.GetPlayerID();
                for (int i = 0; i < 2; i++) { Until[i] = 0f; GapUntil[i] = CircleAt[i] = CircleEnd[i] = -1f; }
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                bool munin = i == 1;
                // the circle over: quietly home (no «order over» word), unless you have given it an order of your own since
                if (CircleEnd[i] >= 0f && Time.time >= CircleEnd[i]) { CircleEnd[i] = -1f; if (Orders.TimeLeft(munin) <= 6f) Orders.FlyWithEnd(p, munin, Orders.Circle); }
                if (Until[i] <= 0f) continue;
                if (Time.time >= Until[i])
                {
                    Until[i] = 0f; CircleAt[i] = -1f;
                    if (!p.IsDead()) PerkFx.Say(munin, "mead_sober", 0.5f, 0f);
                    continue;
                }
                ItemDrop.ItemData worn = Trust.Worn(munin);
                if (p.IsDead() || (worn != null && (Backup.Id(worn) ?? "") != Who[i])) { Sober(p, i); continue; } // (dead, or another raven of that kind on you)
                if (CircleAt[i] >= 0f && Time.time >= CircleAt[i]) { CircleAt[i] = -1f; Circle(p, munin); }
                ShoulderLife life = Shoulder.LifeOf(p, munin);
                if (Time.time >= NextHic[i])
                {
                    NextHic[i] = Time.time + Random.Range(HiccupMin, HiccupMax);
                    if (life != null && life.SitNew && AnimDirector.Of(Shoulder.ModelOf(p, munin)) == RavenAct.Sit) life.SitDirectorNow.React(life, "Здригається", 0f);
                }
                if (Time.time >= NextSay[i])
                {
                    NextSay[i] = Time.time + Random.Range(SayMin, SayMax);
                    PerkFx.Say(munin, "mead_drunk", SayChance, 0f);
                }
            }
        }

        // sober at once, in every game too
        private static void Sober(Player p, int i)
        {
            Until[i] = 0f; CircleAt[i] = -1f;
            Publish(p);
        }

        // a short crooked circle over you, if it sits idle and nothing is going on
        private static void Circle(Player p, bool munin)
        {
            if (Orders.Of(p, munin) != Orders.None || Time.time - Combat.SwingAt < 10f || p.InAttack() || p.IsAttached() || p.IsRiding() || p.InWater() || Outing.Indoors()) return;
            if (OdinWings.Flying() || Beast.On() || FateRite.Busy) return;
            Orders.FlyWith(p, munin, CircleSeconds + 5f, Orders.Circle);
            if (Orders.Of(p, munin) == Orders.Circle) CircleEnd[munin ? 1 : 0] = Time.time + CircleSeconds;
        }
    }
}
#endif
