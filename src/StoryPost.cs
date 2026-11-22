#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A place the players must hold (the user 2026-11-18, chapter 2: the night on Torvald's tower — «пробули там явно до 6 ранку»; gone off,
    // dead or slept through, the night is broken and begins again the next one). A quest's actions (the world's: the server runs them):
    //   post <name> <place> <m>   from now the server watches the place: no player's character within m of it for Grace s on end (all went off,
    //                             died — a dead one has no character till he rises far away), or the world's clock leapt more than Skip s at once
    //                             (a night slept through) — the world fact <name>, and the watch is over. The fact of an earlier watch is
    //                             forgotten when one begins anew
    //   postlost <name>           (a condition) that watch was broken — the quest answers it with a fork on its stages: «fork: postlost <name> -> …».
    //                             A condition of its own, not «world <name>»: whether a post is held is the players' to say, so quest_dry and
    //                             the jump take it as «may be either» (Dry free), never as a fact the action makes
    //   postoff <name>            the watch over, nothing set (the post was held)
    // Kept as the world's text «post_<name>» (the place and the metres): it outlives a restart of the server; its clocks do not (a watch
    // after a restart begins its Grace anew, its first pass never counts a leap). The author's jump that takes the fact either way takes the
    // watch too (StoryQuests.Taken). Many players: held while any one of them stands there.
    // Cost: the server, once a second while a watch stands: one pass over the players' characters (ZNet.GetAllCharacterZDOS — as StoryWaves).
    internal static class StoryPost
    {
        internal static float Grace = 10f, Skip = 600f, Every = 1f;
        private const string Def = "post_";
        private static float _next;
        private static double _clock = -1; // session: the world's time at the last pass (-1: none yet)
        private static readonly Dictionary<string, float> _empty = new Dictionary<string, float>(); // session: a watch → since when nobody is there

        internal const string Usage = "post <назва> <місце> <м>   (пост: нікого з гравців ближче м довше 10 с чи ніч проспано — факт світу <назва>; postoff <назва> — пост утримано)";

        internal static void Actions()
        {
            StoryActions.Register("post", new StoryActions.Kind { Dry = "none", Min = 3, Usage = Usage,
                Check = a =>
                {
                    if (!Story.GoodName(a[1])) return "погана назва «" + a[1] + "»";
                    string why = StoryWhere.Why(a[2]);
                    if (why != null) return why;
                    if (Errands.F(a[3], 0f) <= 0f) return "post: м — число";
                    string name = a[1];
                    StoryQuests.Taken[name] = on => Off(name); // (the jump's answer for this post's fact)
                    return null;
                },
                Do = (p, q, w, rest) =>
                {
                    StoryWorld.SetFlag(w[1], false); // (a watch begun anew: the last one's breaking forgotten)
                    StoryWorld.SetText(Def + w[1], w[2] + " " + w[3]);
                    _empty.Remove(w[1]);
                    return null;
                } });
            StoryActions.Register("postoff", new StoryActions.Kind { Dry = "none", Min = 1, Usage = "postoff <назва>   (пост утримано: нагляд скінчено, факту нема)",
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Do = (p, q, w, rest) => { Off(w[1]); return null; } });
            StoryCond.Register("postlost", new StoryCond.Kind { Dry = "free", Min = 1, World = true, Usage = "postlost <назва>   (пост «post <назва> …» зірвано: усі пішли, загинули чи проспали ніч)",
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Test = (p, a) => StoryWorld.Flag(a[1]) });
        }

        private static void Off(string name) { StoryWorld.SetText(Def + name, ""); _empty.Remove(name); }

        internal static void ServerTick()
        {
            if (Time.time < _next || ZNet.instance == null || !ZNet.instance.IsServer()) return;
            _next = Time.time + Every;
            double now = ZNet.instance.GetTimeSeconds();
            bool leapt = _clock >= 0 && now - _clock > Skip; // (a night slept through: the world's clock jumps hours at once)
            _clock = now;
            foreach (KeyValuePair<string, string> kv in StoryWorld.Texts(Def))
            {
                string name = kv.Key.Substring(Def.Length);
                string[] w = kv.Value.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                Vector3 at;
                if (w.Length < 2 || !StoryWhere.Find(w[0], out at)) continue; // (its place not known here yet: asked again next pass)
                float m = Errands.F(w[1], 80f);
                bool held = false;
                foreach (ZDO c in ZNet.instance.GetAllCharacterZDOS()) // (the players' characters, every game's)
                    if (c != null && StoryWhere.Apart(at, c.GetPosition()) <= m) { held = true; break; }
                float since;
                if (held && !leapt) { _empty.Remove(name); continue; }
                if (!leapt && !_empty.TryGetValue(name, out since)) { _empty[name] = Time.time; continue; }
                if (!leapt && Time.time - _empty[name] < Grace) continue;
                Story.Log("post «" + name + "»: " + (leapt ? "the night was slept through" : "nobody within " + m + " m for " + Grace + " s") + " — broken");
                Off(name);
                StoryWorld.SetFlag(name, true);
            }
        }
    }
}
#endif
