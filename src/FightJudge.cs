#if RAVEN_ITEM
using System;
using UnityEngine;

namespace HuginWhispers
{
    // «Суддя бою» (the user, 2026-10-27): a fight worth it over, a raven on your shoulder judges it — ★ to ★★★★★, a word
    // in the corner and a line of its own (Munin mocks, Hugin names the main mistake). Worth it: MinKills kills, or a
    // strong foe (starred, a boss), or HurtShare of your health lost. At most once every PauseMinutes.
    //   ★★★★★ — a little trust (the deeds' daily limit: Trust.Deed); ★★★★ and up — now and then the raven asks you to find
    //   its cache (Requests «cache»); the best fight is kept in its memory (Memory «fight») and told later.
    // The fight is the owner's alone (the local player's hits, kills, dodges): nothing goes over the network but the
    // raven's word, as any line.
    internal static class FightJudge
    {
        internal static float ExchangeGap = 4f, EndAfter = 5f, WordWait = 40f, PauseMinutes = 20f, HurtShare = 0.3f, TreasureChance = 0.15f, TopTrust = 2f, BehindAngle = 110f;
        internal static int MinKills = 3;

        // the grades, ★ to ★★★★★
        internal static readonly string[] Grades =
        {
            Lang.K("Як ведмідь у посудній лавці"), Lang.K("Як п'яний на весіллі"), Lang.K("Як годиться"), Lang.K("Як воїн"), Lang.K("Як у сагах"),
        };

        internal static string Grade(int stars) { return Lang.T(Grades[Mathf.Clamp(stars, 1, 5) - 1]); }
        internal static string Stars(int stars) { stars = Mathf.Clamp(stars, 1, 5); return new string('★', stars) + new string('☆', 5 - stars); }

        // ---------------------------------------------------------------- the fight going on
        private static bool _on;
        private static float _start, _last, _taken, _nextJudge, _hurtAt = -100f;
        private static int _kills, _strong, _parries, _dodges, _back, _potions, _hits;
        private static bool _fled, _near;
        private static readonly int[] Acts = new int[2];
        // the verdict's word waits for the director (the fight's own lines are often just said): its tag, whose, until when
        private static string _word;
        private static bool _wordMunin;
        private static float _wordUntil;

        private static void Begin(Player p)
        {
            _on = true;
            _start = _last = Time.time;
            _taken = 0f;
            _kills = _strong = _parries = _dodges = _back = _potions = _hits = 0;
            _fled = _near = false;
            Acts[0] = Acts[1] = 0;
        }

        // twice a second (RavenSkills): the fight is blows exchanged with monsters (yours or theirs within ExchangeGap; a
        // foe merely near is no fight — the Black Forest always has some; hunting deer is none), it ends EndAfter after the last
        internal static void Tick(Player p)
        {
            if (p == null) return;
            if (p.IsDead()) { _on = false; _word = null; return; } // (a death is no fight to judge)
            float now = Time.time;
            bool blows = now - Senses.HitMonsterAt < ExchangeGap || now - _hurtAt < ExchangeGap;
            if (_word != null)
            {
                if (now > _wordUntil) _word = null; // (the director never let it: no word, the corner line was enough)
                else if (!blows && Director.May(Director.Voice.Event) && PerkFx.Say(_wordMunin, _word, 1f, 0f)) _word = null; // (not in the middle of a new fight)
            }
            if (blows) { if (!_on) Begin(p); _last = now; return; }
            if (_on && now - _last >= EndAfter) { _on = false; End(p); }
        }

        // the diary's deeds (Diary.Add)
        internal static void On(Ev type)
        {
            if (!_on) return;
            switch (type)
            {
                case Ev.Parry: _parries++; break;
                case Ev.Dodge: _dodges++; break;
                case Ev.Potion: _potions++; break;
                case Ev.Flee: _fled = true; break;
                case Ev.NearDeath: _near = true; break;
            }
        }

        // a monster you killed (Diary's kill patch; not tamed, not a beast of the meadows)
        internal static void Kill(int stars, bool boss)
        {
            if (!_on) return;
            _kills++;
            if (stars > 0 || boss) _strong++;
        }

        // a monster's blow on you (Diary's hurt patch; not a beast of the meadows): what it took, and whether it came from behind
        internal static void Hurt(Player p, Character attacker, float damage)
        {
            if (p == null || attacker == null) return;
            if (!_on) Begin(p);
            _hurtAt = Time.time;
            _taken += Mathf.Max(0f, damage);
            _hits++;
            Vector3 look = p.GetLookDir(); look.y = 0f;
            Vector3 d = attacker.transform.position - p.transform.position; d.y = 0f;
            if (look.sqrMagnitude > 0.01f && d.sqrMagnitude > 0.01f && Vector3.Angle(look, d) > BehindAngle) _back++;
        }

        // a raven's gift used in the fight (RavenSkills.Use): the more active one judges
        internal static void Acted(bool munin) { if (_on) Acts[munin ? 1 : 0]++; }

        // ---------------------------------------------------------------- the verdict

        private static void End(Player p)
        {
            float max = Mathf.Max(1f, p.GetMaxHealth());
            bool worth = _kills >= MinKills || _strong > 0 || _taken >= max * HurtShare;
            if (!worth || Time.time < _nextJudge) return;
            bool munin;
            if (!Judge(p, out munin)) return;
            float lost = _taken / max, seconds = _last - _start;
            int score = Score(lost, seconds), stars = score < 35 ? 1 : score < 50 ? 2 : score < 65 ? 3 : score < 80 ? 4 : 5;
            _nextJudge = Time.time + PauseMinutes * 60f;
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} оцінює бій: {1} — {2}", Plugin.RavenName(munin), Stars(stars), Grade(stars)));
            Guide.Queue(p, Guide.Judge, 120f); // what the stars are, told once
            string tip = !munin && stars <= 3 ? Mistake(lost, seconds) : null; // (Hugin names the main mistake of a poor fight)
            _word = tip != null ? "judge_tip_" + tip : "judge_" + stars; _wordMunin = munin; _wordUntil = Time.time + WordWait; // (said when the director lets it: Tick)
            HuginChores.OnFightJudged(stars); // (Hugin: no blood, but a good fight thought over)
            if (stars == 5) { Trust.Deed(munin, TopTrust, Lang.T("бій як у сагах")); Nicks.OnSaga(p, munin); Nest.Deed(p, Nest.SagaMinutes, Lang.T("бій як у сагах")); }
            if (stars >= 4) Memory.OnFight(p, score, stars);
            if (stars >= 4 && UnityEngine.Random.value < TreasureChance && Requests.Give(p, munin, "cache") == null) Log("a fight so good " + Plugin.RavenName(munin) + " shows you its cache");
            Log(Plugin.RavenName(munin) + " judged a fight of " + Mathf.RoundToInt(seconds) + " s: " + score + " (" + stars + "★; kills " + _kills + ", strong " + _strong + ", lost " + Mathf.RoundToInt(lost * 100f) + "%, parries " + _parries + ", dodges " + _dodges + ", behind " + _back + ", potions " + _potions + (_fled ? ", fled" : "") + (_near ? ", near death" : "") + (tip != null ? ", tip " + tip : "") + ")");
        }

        // 0..100: a clean, quick fight high; blood lost, blows in the back, running and drinking low
        private static int Score(float lost, float seconds)
        {
            float s = 60f - 45f * Mathf.Min(lost, 1.5f);
            if (_near) s -= 15f;
            if (_fled) s -= 12f;
            s += Mathf.Min(_parries * 4f, 20f) + Mathf.Min(_dodges * 2f, 10f) + Mathf.Min(_kills * 3f, 15f) + (_strong > 0 ? 8f : 0f);
            s -= Mathf.Min(_back * 3f, 12f) + Mathf.Min(_potions * 3f, 9f);
            if (seconds < 25f && _kills >= MinKills) s += 5f;
            if (seconds > 180f) s -= 5f;
            return Mathf.Clamp(Mathf.RoundToInt(s), 0, 100);
        }

        // the worst of it, for Hugin's word; null: nothing stands out
        private static string Mistake(float lost, float seconds)
        {
            if (_near) return "close";
            if (_back >= 3) return "back";
            if (lost > 0.6f) return "hurt";
            if (_parries + _dodges == 0 && _hits >= 4) return "noparry";
            if (_potions >= 3) return "potions";
            if (_fled) return "fled";
            if (seconds > 180f) return "slow";
            return null;
        }

        // the raven that judges: awake on your shoulder; of two, the one more active in the fight, else by its heart
        private static bool Judge(Player p, out bool munin)
        {
            bool h = Ready(p, false), m = Ready(p, true);
            munin = m;
            if (!h && !m) return false;
            if (h && m)
            {
                if (Acts[0] != Acts[1]) munin = Acts[1] > Acts[0];
                else
                {
                    Norov.Character c = Norov.CharOf(Trust.Worn(true));
                    munin = UnityEngine.Random.value < (c != null && (c.Id == "bold" || c.Id == "grumpy" || c.Id == "huntsman") ? 0.7f : 0.5f); // (a bold, grumpy or hunting Munin judges more often)
                }
            }
            return true;
        }

        private static bool Ready(Player p, bool munin)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            return item != null && Trust.Active(item) && !Trust.Away(item) && !Trust.Stranger(item, p);
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Fight judge: " + s); }
    }
}
#endif
