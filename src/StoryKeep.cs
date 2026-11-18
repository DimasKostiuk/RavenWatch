#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A piece the story needs whole for a while, and looks at (chapter 1, the user 2026-11-14: Ketil's bed — «щоб це ліжко не можна було зруйнувати у
    // момент коли він встане й піде, і до моменту поки ми не взаємодіємо з ліжком; після E знімати блок і ліжко звичайне»). Words of the data:
    //   keepbed <who> <name>     the bed that person was given (its lasting link, NpcResident.KBed) kept whole from now on: the world's key «keep_<name>»
    //                            (a lasting link: the same bed after a restart). Run where that person is known (the game of the one who talked to him: his
    //                            locker is there). No bed: nothing kept, said in the log — the condition «kept» says so to a fork
    //   unkeep <name>            an ordinary piece again
    //   kept <name>              (a condition of the world) something is kept under that name
    //   on: keep_use <name>      E on the kept piece, while a stage waits for exactly that (StoryQuests.Awaits): the event, nothing else — no sleep, no
    //                            spawn point; it reads «[E] Роздивитись» then (HoverOf)
    //   kept.<name>              (a word of place) where one lies on it: over the bed's own sleeping point, the top of its own colliders (TopOf) — a scene
    //                            films it («camera on kept.ch1_bed close»), a thing is laid there («lay <item> kept.<name>»: StoryFound). A thing lying
    //                            low (StoryWhere.Lying): no height over it. Known while it is kept, on a game that has its piece (not loaded: where it stands)
    // Kept whole: no blow, no fire, no wear, no hammer — the same patches that keep a place of the story whole (Blueprint.cs: they ask Holds
    // as well as Blueprint's own mark). Every game knows what is kept by the world's keys, resolved again each RefreshEvery s (a lasting link
    // finds the piece where it is loaded); nothing kept: one count, no look at all.
#if RAVEN_STORY
    internal static class StoryKeep
    {
        internal const string Keep = "keep_";
        internal static float RefreshEvery = 2f;
        private static readonly Dictionary<ZDOID, string> _kept = new Dictionary<ZDOID, string>(); // piece → its name
        private static float _next;
        private static int _keys;

        internal static void Actions()
        {
            StoryActions.Register("keepbed", new StoryActions.Kind { Dry = "set world keep:{2}", Min = 2, Usage = "keepbed <хто> <назва>   (ліжко цієї людини не ламається, поки не unkeep; E на ньому — подія keep_use <назва>)",
                Check = a => !Story.GoodName(a[2]) ? "погана назва «" + a[2] + "»" : null,
                Do = (p, q, w, rest) =>
                {
                    string link = BedOf(w[1]);
                    if (link.Length == 0) { Story.Log("keep: «" + w[1] + "» has no bed here — nothing kept as «" + w[2] + "»"); return null; }
                    StoryWorld.SetText(Keep + w[2], link);
                    Story.Log("keep: the bed of «" + w[1] + "» kept whole as «" + w[2] + "» (" + link + ")");
                    _next = 0f;
                    return null;
                } });
            StoryActions.Register("unkeep", new StoryActions.Kind { Dry = "unset world keep:{1}", Min = 1, Usage = "unkeep <назва>   (знову звичайна деталь)",
                Do = (p, q, w, rest) => { StoryWorld.Remove(Keep + w[1]); _next = 0f; return null; } });
            StoryCond.Register("kept", new StoryCond.Kind { Dry = "world keep:{1}", Usage = "kept <назва>   (світ тримає цілою деталь під цією назвою: keepbed)", Min = 1, World = true,
                Test = (p, a) => StoryWorld.Text(Keep + a[1]).Length > 0 });
            StoryQuests.Makers["world keep"] = (p, q, n, w) => { if (!w.On) StoryWorld.Remove(Keep + n); else if (w.Act != null && StoryWorld.Text(Keep + n).Length == 0) StoryActions.Do(p, q, w.Act); return null; }; // (the author's jump: kept or not as the quest says)
            StoryWorld.ScratchKinds.Add(Keep); // (a block's «scratch:» takes what it kept away with it)
            StoryWorld.ScratchKinds.Add(Key(Place)); // (and a key named after the word «kept.<name>»: a thing laid on it — «stash_kept_ch1_bed_…», StoryFound)
            StoryWhere.Prefixed[Place] = (string rest, out Vector3 at) => Where(rest, out at);
            StoryWhere.Lying.Add(Place);
            NpcResident.StoryHover = HoverOf;
        }

        internal const string Place = "kept.";
        private static string Key(string word) { return word.Replace('.', '_'); }

        // «kept.<name>»: where one lies on the piece kept under that name. False: nothing kept so, or its piece not known on this game
        private static bool Where(string name, out Vector3 at)
        {
            at = Vector3.zero;
            Refresh();
            foreach (KeyValuePair<ZDOID, string> k in _kept) if (k.Value == name) return TopOf(k.Key, out at);
            return false;
        }

        // over the bed's own sleeping point (Bed.m_spawnPoint: under it, if it stands beside the bed, nothing of the bed is hit — that point, then),
        // the highest of its own colliders; its piece not loaded here: where it stands, and looked at again once it is. Said in the log once a piece
        private static readonly Dictionary<ZDOID, Vector3> _tops = new Dictionary<ZDOID, Vector3>(); // session: a kept piece never moves (no hammer, no blow); ZDOIDs of this session
        private static bool TopOf(ZDOID id, out Vector3 at)
        {
            if (_tops.TryGetValue(id, out at)) return true;
            ZDO z = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            if (z == null) return false;
            at = z.GetPosition();
            ZNetView nv = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(z) : null;
            if (nv == null) return true; // (not loaded here: where it stands)
            Bed bed = nv.GetComponent<Bed>();
            Vector3 over = bed != null && bed.m_spawnPoint != null ? bed.m_spawnPoint.position : at;
            Ray down = new Ray(new Vector3(over.x, at.y + 3f, over.z), Vector3.down);
            float best = float.MinValue;
            foreach (Collider c in nv.GetComponentsInChildren<Collider>()) // heavy: once a kept piece, the first time its place is asked on this game
            {
                RaycastHit hit;
                if (c != null && !c.isTrigger && c.Raycast(down, out hit, 5f) && hit.point.y > best) best = hit.point.y;
            }
            at = new Vector3(over.x, best > float.MinValue ? best : over.y, over.z);
            _tops[id] = at;
            Story.Log("keep: «" + (_kept.ContainsKey(id) ? _kept[id] : "?") + "» — one lies on it at " + at.x.ToString("0.0") + "," + at.y.ToString("0.00") + "," + at.z.ToString("0.0")
                + " (" + (best > float.MinValue ? "the top of its own colliders, " + (best - z.GetPosition().y).ToString("0.00") + " m over its foot" : "nothing of it under its sleeping point: that point") + ")");
            return true;
        }

        // the lasting link of the bed that person was given, as its locker says (the person loaded here, or known: the server knows all)
        private static string BedOf(string who)
        {
            foreach (ZDO z in NpcCensus.People()) if (StoryPeople.Of(z, who)) return NpcLocker.Text(z, NpcResident.KBed); // (the person of the world, never a scene's copy)
            return "";
        }

        private static void Refresh()
        {
            if (Time.time < _next) return;
            _next = Time.time + RefreshEvery;
            _kept.Clear();
            List<KeyValuePair<string, string>> keys = StoryWorld.Texts(Keep);
            _keys = keys.Count;
            foreach (KeyValuePair<string, string> k in keys)
            {
                ZDOID id = NpcKit.Resolve(k.Value);
                if (!id.IsNone()) _kept[id] = k.Key.Substring(Keep.Length);
            }
        }

        // kept whole now (asked by the patches that keep a piece whole: a blow, the weather, the hammer). Nothing kept anywhere: no look
        internal static bool Holds(Component c) { return NameOf(c) != null; }

        // the name it is kept under (null: not kept). Nothing kept anywhere: not even its object looked up
        private static string NameOf(Component c)
        {
            if (_keys == 0 && Time.time < _next) return null;
            Refresh();
            if (_kept.Count == 0 || c == null) return null;
            ZNetView nv = c.GetComponentInParent<ZNetView>();
            string name;
            return nv != null && nv.IsValid() && _kept.TryGetValue(nv.GetZDO().m_uid, out name) ? name : null;
        }

        // E on a kept bed a stage waits for: the event, no sleep (LocInteractPatch, the one guard of the player's E, before a resident's bed is refused there). True: handled
        internal static bool Use(Bed bed, Humanoid human)
        {
            Player p = human as Player;
            if (p == null || p != Player.m_localPlayer) return false;
            string name = NameOf(bed);
            if (name == null || !StoryQuests.Awaits(p, "keep_use", name)) return false;
            StoryBus.Raise("keep_use", name, 0, bed.transform.position);
            return true;
        }

        // its words when pointed at, while a stage waits for E on it (NpcBedHoverPatch: over the bed's own words); null — the bed's own
        private static string HoverOf(Bed bed)
        {
            Player p = Player.m_localPlayer;
            string name = p != null ? NameOf(bed) : null;
            if (name == null || !StoryQuests.Awaits(p, "keep_use", name)) return null;
            return Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] ") + Lang.T("Роздивитись");
        }
    }
#else
    // a build with no story: nothing is kept (the patches that keep a piece whole ask it all the same: Blueprint.cs, LocPatches.cs)
    internal static class StoryKeep
    {
        internal static bool Holds(Component c) { return false; }
        internal static bool Use(Bed bed, Humanoid human) { return false; }
    }
#endif
}
#endif
