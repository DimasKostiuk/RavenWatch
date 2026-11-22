#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A place the world finds among the game's own (chapter 1, the user 2026-11-14: Ketil's errand — «a house generated from the map's middle,
    // 250 m and more; a house with a chest, and in that chest a thing for him»), and a thing put into a chest there. Words of the data:
    //   pickloc <name> <prefabs,…> [from <m>] [near <place> [ring <min> <max>]]   the server picks one of the game's locations whose prefab begins so
    //                                     (WoodHouse,WoodFarm), at least m from the world's middle, never by a base (a heart of a home or a ward within
    //                                     BaseClear m: the user 2026-11-14 — the nearest house to the first heart stood on his base, 67 m off): one at
    //                                     random min..max m from <place>, else the nearest farther than min (no place: any) — each first of those one
    //                                     walks to from <place> (WorldSpot.DryWay: not across the sea); kept by the world as «found_<name>»
    //                                     («x y z»), once — a stage entered again keeps the first. Only the server knows every location
    //                                     (ZoneSystem.m_locationInstances): a client's action leaves the wish («fw_<name>») and the server answers it
    //   place found.<name>                where it was found (every game, the server too: StoryWhere.Prefixed — a map mark, a people's home)
    //   stash <item prefab> <place> [m] [count N]   that thing (count N: the chest topped up to N of it — chapter 2, the user 2026-11-17: Lit counts what
    //                                     already lies in the Hall's chest, «stash Wood zala.skrynia 4 count 48»; stacks of the game's size) put once into a chest of that place, within m (12) of it: the first game that has the place
    //                                     loaded near its player does it (a chest of the game's there, not one a player built; none — the
    //                                     game's meadow chest is put in the middle of the place). Kept: «stash_<key>» (what, where), «stashed_<key>» done —
    //                                     the key a thing's at its place, as lay's: a few things go into one chest (the archivist's: the scroll, his note).
    //                                     Two players come together: the first to claim it (the world's «stashby_<key>» = its player — the server keeps one
    //                                     answer for all) puts it, the other waits, so one thing, one chest
    //   lay <item prefab> <place>         that thing laid once on the place's point, a thing of the world anyone picks up (chapter 1, the user 2026-11-14: the
    //                                     necklace Ketil leaves on his bed — «kept.<name>», StoryKeep: on its mattress). The same way as stash: the first
    //                                     game near claims it and lays it (ItemDrop.DropItem LayUp m over the point: the game lays it down); one key a thing
    //                                     and a place («stash_<place>_<item>»), so a few things may lie at one place
    //   (plants to pick at a place — Heid's thistle, Eyvind's mushrooms: a stage's «stock:», StoryStock — the one way; the action «plant» and
    //    the talk's «!replant» it replaced are gone, 2026-11-18)
    //   pickglade <name> <m> near <place> ring <min> <max> [south|north|east|west] [in <land>] [edge <land> <m>] [apart <place> <m> …] [wild]
    //                                     apart: no nearer than m to that place too, several may be said («start» — the world's middle; the user 2026-11-18:
    //                                     Eyvind's meadow «300 m from the middle and from the Hall, so they do not cross»); kept in every step, the next to last too (any open land, still apart); only the very last (any open land at all) drops it
    //                                     edge: that other land within m of it (the black forest's edge: «in BlackForest edge Meadows 40»: its spots found
    //                                     along rays from <place> where the one land turns into the other — WorldSpot.EdgeSpots — the nearer the better;
    //                                     none so near: the same edge up to three times as far, never another kind of spot); wild: nothing taken away from it.
    //                                     Every step first only where one walks from <place> (not across the sea), then anywhere
    //                                     a glade (chapter 1, the ambush, the user 2026-11-14: «локація без нічого, просто поляна», never on a base): the server picks
    //                                     open land m across its middle min..max m from <place> (within GladeArc° of that side), of one land (none said: the
    //                                     land at <place>), the flattest and least wooded by the world's own numbers (WorldSpot.Open), never by a base, a place of
    //                                     the world or of the story (Taken); kept as «found_<name>» — the place found.<name> — and «glade_<name>» (its m). Its
    //                                     trees, stones and bushes are taken away once (BlueprintGround.ClearRound, «gladed_<name>»), by the server as soon as its
    //                                     land is made and no player is within ClearUnseen m (nobody sees them go); the ground is left as it is
    //   pickway <name> <from> <to> <n> [side <m>]   n points on the way between two places (chapter 2: Torvald's tracks from the Hall to his post), each a
    //                                     little aside, on dry land (AnswerWay); the places found.<name>1 … found.<name>n
    internal static class StoryFound
    {
        internal const string Found = "found_", Wish = "fw_", Stash = "stash_", Stashed = "stashed_", Claim = "stashby_", MadeChest = "stashchest_", Glade = "glade_", Gladed = "gladed_";
        internal static float GladeArc = 60f, ClearUnseen = 90f;
        internal static string ChestPrefab = "TreasureChest_meadows"; // (a chest of the game's own, when the place has none)
        internal static float SettleFor = 4f, LookEvery = 2f; // (s: the place's things come in over frames — its chest looked for once they are there)
        internal static float BaseClear = WorldSpot.Clear * 2f; // (m from a heart of a home or a ward: a base the server knows from afar, cheaply — it sees no beds nor map marks)
        internal static float BuiltClear = 40f; // (m from anything a player built: a base with neither — WorldSpot.Settled, asked of the last candidates only)
        internal static float AheadWait = 60f, AheadAgain = 30f; // (s: a glade's land made ahead of anyone — at most so long a try, so long between two)
        internal static float ClaimFor = 30f; // (s of the world's time: a claim to put a thing not done by then — its game quit — is taken over)

        internal static void Actions()
        {
            StoryActions.Register("pickloc", new StoryActions.Kind { Dry = "set world found:{1}", Min = 2, Usage = "pickloc <назва> <префаби,…> [from <м від середини світу>] [near <місце>]   (сервер вибирає місце гри: місце «found.<назва>»)",
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : null,
                Do = (p, q, w, rest) =>
                {
                    if (StoryWorld.Text(Found + w[1]).Length > 0) return null; // (found already: kept)
                    string ask = string.Join(" ", w, 2, w.Length - 2);
                    if (ZNet.instance == null || !ZNet.instance.IsServer() || !Answer(w[1], ask)) StoryWorld.SetText(Wish + w[1], ask.Replace(' ', '|')); // (the server, or a wish it answers)
                    return null;
                } });
            StoryActions.Register("pickway", new StoryActions.Kind { Dry = "set world found:{1}", Min = 4, Usage = "pickway <назва> <звідки> <куди> <N> [side <м>]   (N точок на шляху між двома місцями, рівно розкиданих, трохи вбік, на суші: місця «found.<назва>1» … «found.<назва>N»)",
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryWhere.Why(a[2]) ?? StoryWhere.Why(a[3]) ?? (Errands.I(a[4], 0) < 1 || Errands.I(a[4], 0) > 9 ? "pickway: N — від 1 до 9" : null),
                Do = (p, q, w, rest) =>
                {
                    if (StoryWorld.Text(Found + w[1]).Length > 0) return null; // (found already: kept)
                    string ask = "way " + string.Join(" ", w, 2, w.Length - 2);
                    if (ZNet.instance == null || !ZNet.instance.IsServer() || !Answer(w[1], ask)) StoryWorld.SetText(Wish + w[1], ask.Replace(' ', '|'));
                    return null;
                } });
            StoryActions.Register("pickglade", new StoryActions.Kind { Dry = "set world found:{1}", Min = 7, Usage = "pickglade <назва> <м> near <місце> ring <мін> <макс> [south|north|east|west] [in <земля>] [edge <земля> <м>] [apart <місце> <м> …] [wild]   (сервер вибирає відкриту поляну й прибирає з неї дерева (wild — не прибирає); edge — інша земля поруч; apart — не ближче м до іншого місця (start — середина світу): місце «found.<назва>»)",
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : Errands.F(a[2], -1f) <= 0f ? "pickglade: м — число" : Array.IndexOf(a, "near") != 3 || Array.IndexOf(a, "ring") != 5 ? "pickglade <назва> <м> near <місце> ring <мін> <макс> …" : Array.IndexOf(a, "edge") > 0 && (Array.IndexOf(a, "edge") + 2 >= a.Length || Errands.F(a[Array.IndexOf(a, "edge") + 2], -1f) <= 0f) ? "pickglade … edge <земля> <м>" : ApartWhy(a),
                Do = (p, q, w, rest) =>
                {
                    if (StoryWorld.Text(Found + w[1]).Length > 0) return null; // (found already: kept)
                    string ask = "glade " + string.Join(" ", w, 2, w.Length - 2);
                    if (ZNet.instance == null || !ZNet.instance.IsServer() || !Answer(w[1], ask)) StoryWorld.SetText(Wish + w[1], ask.Replace(' ', '|'));
                    return null;
                } });
            StoryActions.Register("stash", new StoryActions.Kind { Dry = "set world stash:{2}:{1}", Min = 2, Usage = "stash <предмет> <місце> [м=12] [count N]   (покласти раз (N штук) у скриню того місця, коли хтось буде поруч; точка нутра in.<розклад>.<точка> — лише скриня на ній, м=" + PointChest + ")",
                Do = (p, q, w, rest) =>
                {
                    string key = ThingKey(w[2], w[1]); // (a thing's own key, as lay's: two things into one chest — the archivist's — are two; by the place alone the second was dropped unsaid, 2026-11-15)
                    if (StoryWorld.Flag(Stashed + key) || StoryWorld.Text(Stash + key).Length > 0) return null;
                    int ci = Array.IndexOf(w, "count"), count = ci > 2 && ci + 1 < w.Length ? Errands.I(w[ci + 1], 1) : 1;
                    string m = w.Length > 3 && ci != 3 ? w[3] : AtPoint(w[2]) ? PointChest.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "12";
                    StoryWorld.SetText(Stash + key, w[1] + "|" + w[2] + "|" + m + (count > 1 ? "|" + PutWord + "|" + count.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""));
                    return null;
                } });
            StoryActions.Register("lay", new StoryActions.Kind { Dry = "set world stash:{2}:{1}", Min = 2, Usage = "lay <предмет> <місце>   (покласти раз на місце річ світу, яку можна підняти, коли хтось буде поруч)",
                Check = a => StoryWhere.Why(a[2]),
                Do = (p, q, w, rest) =>
                {
                    string key = ThingKey(w[2], w[1]);
                    if (StoryWorld.Flag(Stashed + key) || StoryWorld.Text(Stash + key).Length > 0) return null;
                    StoryWorld.SetText(Stash + key, w[1] + "|" + w[2] + "|0|" + LayWord);
                    return null;
                } });
            StoryWhere.WorldKept.Add("found.");
            StoryWorld.ScratchKinds.AddRange(new[] { Found, Wish, Stash, Stashed, Claim, MadeChest, Glade, Gladed }); // (a block's «scratch:» takes its finds and chests away with it)
            StoryWhere.Prefixed["found."] = (string rest, out Vector3 at) => Where(rest, out at);
        }

        private static string Key(string place) { return place.Replace('.', '_').ToLowerInvariant(); }
        // a thing's own key at a place (stash, lay: a few things may be at one place): «<place>__<item>» — the place's key, StoryWorld.ThingSep, the item
        // (StoryWorld.Forget with «whole» takes these with their place; Unstash too)
        private static string ThingKey(string place, string item) { return Key(place) + StoryWorld.ThingSep + item.ToLowerInvariant(); }

        // the author's jump back over what put things at a place (StoryRebuild: «world stash» turned off): every stash, lay and plant there undone, so
        // the stage that comes again does it again (the things already put stay where they are)
        // «<place>» — everything there; «<place>:<item>» — that one thing (lay, plant: each its own fact — four things laid at one place are four)
        internal static void Unstash(string place)
        {
            int colon = place.IndexOf(':');
            string k0 = colon > 0 ? ThingKey(place.Substring(0, colon), place.Substring(colon + 1)) : Key(place);
            string old = colon > 0 ? Key(place.Substring(0, colon)) : null; // (a stash written before 2026-11-15 went by its place alone: one thing a place)
            Vector3 at;
            if (StoryWhere.Find(colon > 0 ? place.Substring(0, colon) : place, out at)) TakeMadeAt(at); // (one put before its chest was kept by its id: the stash's own kind on the very spot, if loaded here)
            foreach (string k in StoryWorld.All())
            {
                int sp = k.IndexOf(' ');
                string name = sp < 0 ? k : k.Substring(0, sp);
                foreach (string kind in new[] { Stash, Stashed, Claim, MadeChest })
                {
                    if (!name.StartsWith(kind, StringComparison.Ordinal)) continue;
                    string key = name.Substring(kind.Length);
                    if (key == k0 || key == old || (colon < 0 && key.StartsWith(k0 + StoryWorld.ThingSep, StringComparison.Ordinal)))
                    {
                        if (kind == MadeChest) TakeMade(StoryWorld.Text(name)); // (a chest the stash put itself, where the place had none: gone with it)
                        StoryWorld.Remove(name);
                    }
                    break;
                }
            }
        }

        // a chest the stash put itself: taken out of the world (loaded or not: its ZDO), with what is in it — the stage that comes again puts the thing anew
        private static void TakeMade(string zdoid)
        {
            ZDOID id = NpcKit.Resolve(zdoid);
            ZDO z = !id.IsNone() && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            if (z == null) return;
            z.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(z);
            Story.Log("found: the chest the stash put itself (" + zdoid + ") taken away");
        }

        private static void TakeMadeAt(Vector3 at)
        {
            foreach (Collider c in Physics.OverlapSphere(at, PointChest)) // heavy: once, the author's jump back
            {
                Container k = c != null ? c.GetComponentInParent<Container>() : null;
                ZNetView nv = k != null ? k.GetComponent<ZNetView>() : null;
                if (nv == null || !nv.IsValid() || Utils.GetPrefabName(k.gameObject) != ChestPrefab) continue;
                TakeMade(NpcKit.Ref(nv.GetZDO().m_uid));
            }
        }

        internal static bool Where(string name, out Vector3 at) { return StoryWorld.Pos(Found + name, out at); }

        // every place found so far (a place of the story placed later keeps off them: LocBuild.NearStory)
        internal static void AllFound(List<Vector3> into) { foreach (KeyValuePair<string, string> k in StoryWorld.Texts(Found)) { Vector3 at; if (StoryWorld.Pos(k.Key, out at)) into.Add(at); } }

        // the server: the wishes the clients left, answered (a few seconds apart: a world key's write costs — StoryWorld)
        private static float _nextWish;
        internal static void ServerTick()
        {
            if (Time.time < _nextWish || ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            _nextWish = Time.time + 3f;
            foreach (KeyValuePair<string, string> k in StoryWorld.Texts(Wish))
            {
                string name = k.Key.Substring(Wish.Length);
                if (StoryWorld.Text(Found + name).Length == 0 && !Answer(name, k.Value.Replace('|', ' '))) continue; // (its place not known yet: asked again)
                StoryWorld.Remove(k.Key);
            }
            foreach (KeyValuePair<string, string> k in StoryWorld.Texts(Glade)) ClearGlade(k.Key.Substring(Glade.Length), Errands.F(k.Value, 0f));
        }

        // a glade's wild taken away, once: its land made (its trees are in the world's list), nobody near to see them go. Land never made yet
        // (nobody has been there): the server makes it itself as soon as a player is within Loc.BuildFrom m — the same lead a place of the story
        // is built with (BlueprintGround.ZonesIn, one glade at a time) — so its trees go long before anyone can see them (the user 2026-11-21:
        // made only by the player's own coming, they went at 100–190 m from him, in sight)
        private static bool _gladeAhead;
        private static float _gladeNext;
        private static void ClearGlade(string name, float r)
        {
            Vector3 at;
            if (r <= 0f || StoryWorld.Flag(Gladed + name) || !Where(name, out at)) return;
            if (!BlueprintGround.Made(at, r + 4f))
            {
                if (_gladeAhead || Time.time < _gladeNext || !Loc.SomeoneWithin(at, Loc.BuildFrom)) return;
                _gladeAhead = true;
                Plugin.Instance.StartCoroutine(BlueprintGround.ZonesIn(BlueprintGround.ZonesOf(at, r + 4f), AheadWait, came =>
                {
                    _gladeAhead = false; _gladeNext = Time.time + AheadAgain;
                    Story.Log("found: glade «" + name + "»: its land " + (came == BlueprintGround.ZonesOk ? "made ahead of anyone" : "not made ahead (" + (came == BlueprintGround.ZonesLate ? "not in " + AheadWait + " s" : came == BlueprintGround.ZonesNoGame ? "the game changed: PokeLocalZone" : "the world was left") + "): tried again later"));
                }));
                return;
            }
            foreach (ZDO c in ZNet.instance.GetAllCharacterZDOS()) if (c != null && StoryWhere.Apart(at, c.GetPosition()) < ClearUnseen) return; // (someone there: later)
            int gone = BlueprintGround.ClearRound(at, r);
            StoryWorld.SetFlag(Gladed + name, true);
            Story.Log("found: glade «" + name + "» cleared — " + gone + " trees, stones, bushes taken away within " + Mathf.RoundToInt(r) + " m");
        }

        // «glade <m> near <place> ring <min> <max> [side] [in <land>]» → the most open land there, kept by the world. True: settled; false — <place> not known yet
        private static bool AnswerGlade(string name, string[] w)
        {
            float r = Errands.F(w[1], 20f);
            int ni = Array.IndexOf(w, "near"), ri = Array.IndexOf(w, "ring"), li = Array.IndexOf(w, "in");
            Vector3 near;
            if (ni < 0 || w.Length <= ni + 1 || !StoryWhere.Find(w[ni + 1], out near)) { if (_toldWait.Add(name)) Story.Log("found: glade «" + name + "» waits — «" + (ni >= 0 && w.Length > ni + 1 ? w[ni + 1] : "") + "» is not known yet"); return false; }
            float rmin = ri >= 0 && w.Length > ri + 2 ? Errands.F(w[ri + 1], 200f) : 200f, rmax = ri >= 0 && w.Length > ri + 2 ? Errands.F(w[ri + 2], 350f) : 350f;
            Vector3 toward = Vector3.zero;
            if (Array.IndexOf(w, "south") >= 0) toward = Vector3.back; else if (Array.IndexOf(w, "north") >= 0) toward = Vector3.forward;
            else if (Array.IndexOf(w, "east") >= 0) toward = Vector3.right; else if (Array.IndexOf(w, "west") >= 0) toward = Vector3.left;
            Heightmap.Biome land = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(near) : Heightmap.Biome.None, edge = Heightmap.Biome.None;
            if (li >= 0 && w.Length > li + 1) land = Land(name, w[li + 1], land);
            int ei = Array.IndexOf(w, "edge");
            float edgeM = ei >= 0 && w.Length > ei + 2 ? Errands.F(w[ei + 2], 40f) : 0f;
            if (ei >= 0 && w.Length > ei + 1) edge = Land(name, w[ei + 1], Heightmap.Biome.None);
            bool wild = Array.IndexOf(w, "wild") >= 0; // (its trees and bushes left as they are: the flowers at the forest's edge)
            List<KeyValuePair<Vector3, float>> apart = new List<KeyValuePair<Vector3, float>>();
            for (int i = 0; i < w.Length - 2; i++)
            {
                if (w[i] != "apart") continue;
                Vector3 there = Vector3.zero;
                if (w[i + 1] != "start" && !StoryWhere.Find(w[i + 1], out there)) { if (_toldWait.Add(name)) Story.Log("found: glade «" + name + "» waits — «" + w[i + 1] + "» (apart) is not known yet"); return false; }
                apart.Add(new KeyValuePair<Vector3, float>(there, Errands.F(w[i + 2], 0f)));
            }
            System.Func<Vector3, bool> fits = apart.Count == 0 ? (System.Func<Vector3, bool>)null : q => apart.TrueForAll(a => Utils.DistanceXZ(a.Key, q) >= a.Value); // flat: land kept apart on the map
            Vector3 at = Vector3.zero; float uneven = 0f;
            List<Vector3> taken = Taken();
            // not kept off the very place it is asked by: how far from that one, its ring says (the user 2026-11-21: «near bad_house ring 30 90» and
            // «near found.dozor ring 35 90» had no spot at all within BaseClear of their own place, and the glade went 140 m and more off)
            taken.RemoveAll(b => WorldSpot.Flat(b, near) < 1f);
            // nor on anything a player built (WorldSpot.Settled: a base with no heart and no ward) — in every step but the last two, as the bases
            System.Func<Vector3, bool> free = q => (fits == null || fits(q)) && !WorldSpot.Settled(q, r + BuiltClear);
            bool got = false;
            // every step below first only where one walks from <place> (WorldSpot.DryWay, a river swum at most: the user 2026-11-15 — the thistle edge
            // went 1753 m off across the sea); nothing anywhere so — the same steps over water
            int seaFrom = WorldSpot.SeaAll;
            for (int pass = 0; pass < 2 && !got; pass++)
            {
                if (pass == 1 && WorldSpot.SeaAll == seaFrom) break; // (nothing was turned down for the sea: over water would find the same nothing — no second round of asks)
                bool foot = pass == 0, none = Heightmap.Biome.None == edge;
                got = WorldSpot.Open(near, rmin, rmax, r, land, toward, GladeArc, taken, BaseClear, out at, out uneven, edge, edgeM, foot, free);
                if (!got && toward != Vector3.zero) got = WorldSpot.Open(near, rmin, rmax, r, land, Vector3.zero, 180f, taken, BaseClear, out at, out uneven, edge, edgeM, foot, free); // (none that side: any side)
                if (!got && !none) got = WorldSpot.Open(near, rmin, rmax * 3f, r, land, Vector3.zero, 180f, taken, BaseClear, out at, out uneven, edge, edgeM, foot, free); // (an edge asked: the same edge, farther)
                if (!got && !none) got = WorldSpot.Open(near, rmin, rmax * 3f, r, land, Vector3.zero, 180f, taken, BaseClear, out at, out uneven, Heightmap.Biome.None, 0f, foot, free); // (no such edge at all: that land, farther — a quest waits on this place, never nothing)
                if (!got) got = WorldSpot.Open(near, rmin, rmax * 2f, r, Heightmap.Biome.None, Vector3.zero, 180f, taken, BaseClear, out at, out uneven, Heightmap.Biome.None, 0f, foot, free); // (none of that land: any, farther)
                if (!got && fits != null) got = WorldSpot.Open(near, rmin, rmax * 4f, r, Heightmap.Biome.None, Vector3.zero, 180f, null, 0f, out at, out uneven, Heightmap.Biome.None, 0f, foot, fits); // (any dry open land, by a base or not — still apart: chapter 2's third place to look for Eyvind must not fall on the two others)
                if (!got) got = WorldSpot.Open(near, rmin, rmax * 4f, r, Heightmap.Biome.None, Vector3.zero, 180f, null, 0f, out at, out uneven, Heightmap.Biome.None, 0f, foot); // (the last: any dry open land, by a base or not)
            }
            if (!got) { Story.Log("found: no glade for «" + name + "» (" + string.Join(" ", w) + "): " + WorldSpot.Why); return true; }
            StoryWorld.SetPos(Found + name, at);
            if (!wild) StoryWorld.SetText(Glade + name, r.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            Story.Log("found: glade «" + name + "» at " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z) + " (" + Mathf.RoundToInt(Utils.DistanceXZ(at, near)) /* flat: the log's map distance */ + " m from " + w[ni + 1] + ", " + WorldGenerator.instance.GetBiome(at) + ", uneven " + uneven.ToString("0.0") + " m, forest factor " + WorldGenerator.GetForestFactor(at).ToString("0.00") + "; turned down: " + WorldSpot.Why + ")");
            return true;
        }

        // «way <from> <to> <n> [side <m>]» → n points on the way between two places (the user 2026-11-18: three tracks of Torvald from the Hall to his post):
        // evenly along the straight line, each a little aside (up to side m, by its own number: the same in every world of the same places, no straight
        // row of dots), on dry land — a point over water slides along the line to the nearest dry spot. Kept as «found_<name><i>»; «found_<name>» = the last
        internal static float WaySide = 25f, WayStep = 8f;
        private static bool AnswerWay(string name, string[] w)
        {
            Vector3 a, b;
            if (w.Length < 4 || !StoryWhere.Find(w[1], out a) || !StoryWhere.Find(w[2], out b)) { if (_toldWait.Add(name)) Story.Log("found: way «" + name + "» waits — «" + (w.Length > 2 ? w[1] + "» or «" + w[2] : "") + "» is not known yet"); return false; }
            int n = Mathf.Clamp(Errands.I(w[3], 3), 1, 9), si = Array.IndexOf(w, "side");
            float side = si >= 0 && w.Length > si + 1 ? Errands.F(w[si + 1], WaySide) : WaySide;
            Vector3 along = b - a; along.y = 0f;
            Vector3 across = new Vector3(-along.z, 0f, along.x).normalized;
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel + 0.5f : 30.5f, len = along.magnitude;
            Vector3 last = b;
            for (int i = 1; i <= n; i++)
            {
                System.Random r = new System.Random(name.GetStableHashCode() * 31 + i);
                Vector3 p = a + along * ((float)i / (n + 1)) + across * (float)((r.NextDouble() * 2.0 - 1.0) * side);
                for (int k = 1; k <= 12 && Ground(p) < water; k++) p += along.normalized * (WayStep * (k % 2 == 1 ? k : -k)); // (wet: back and forth along the way, farther each time)
                p.y = Ground(p);
                StoryWorld.SetPos(Found + name + i, p);
                last = p;
            }
            StoryWorld.SetPos(Found + name, last);
            Story.Log("found: way «" + name + "» — " + n + " point(s) over " + Mathf.RoundToInt(len) + " m from " + w[1] + " to " + w[2]);
            return true;
        }
        private static float Ground(Vector3 p) { return WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(p.x, p.z) : p.y; }

        // pickglade's «apart <place> <m>» words, at load: a known place (or «start») and a number
        private static string ApartWhy(string[] a)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != "apart") continue;
                if (i + 2 >= a.Length || Errands.F(a[i + 2], -1f) <= 0f) return "pickglade … apart <місце> <м>";
                if (a[i + 1] != "start") { string why = StoryWhere.Why(a[i + 1]); if (why != null) return why; }
            }
            return null;
        }

        // a land by its game name (BlackForest), any case; not one: «otherwise», said in the log
        private static Heightmap.Biome Land(string name, string word, Heightmap.Biome otherwise)
        {
            try { return (Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), word, true); }
            catch (Exception) { Story.Log("found: glade «" + name + "»: no land «" + word + "»"); return otherwise; }
        }

        // where a glade must not be: the bases, the places of the story that stand in this world, and what the world found already (the thistles'
        // edge, the ambush's glade, the house: a stash picked by the thistles would be found while picking them — the review of 2026-11-14)
        private static List<Vector3> Taken()
        {
            List<Vector3> l = Bases();
            Places(l);
            return l;
        }
        // the places of the story that have their spot in this world, and what the world found already
        private static void Places(List<Vector3> into)
        {
            foreach (Loc place in Locs.All) { Vector3 at; Quaternion rot; if (place.Where(out at, out rot)) into.Add(at); }
            AllFound(into);
        }

        // «<prefabs,…> [from m] [near place [ring min max]]» → the one found, kept by the world. True: settled (found, or there is none: said in the log); false —
        // «near» names a place not known here yet (the first heart's word not come): asked again, never «anywhere» instead
        private static readonly HashSet<string> _toldWait = new HashSet<string>();
        // (what a pick cost is said in the log: once a pick, in one frame — ms, and the zones gone through for players' buildings)
        private static bool Answer(string name, string ask)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            int looks0 = WorldSpot.BuiltLooks;
            bool settled = AnswerNow(name, ask);
            if (settled) Story.Log("found: «" + name + "» answered in " + sw.ElapsedMilliseconds + " ms (zones looked through for buildings " + (WorldSpot.BuiltLooks - looks0) + ")");
            return settled;
        }
        private static bool AnswerNow(string name, string ask)
        {
            if (ZoneSystem.instance == null) return false;
            string[] w = ask.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return true;
            if (w[0] == "glade") return AnswerGlade(name, w);
            if (w[0] == "way") return AnswerWay(name, w);
            string[] names = w[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int fi = Array.IndexOf(w, "from"), ni = Array.IndexOf(w, "near"), ri = Array.IndexOf(w, "ring");
            float from = fi >= 0 && w.Length > fi + 1 ? Errands.F(w[fi + 1], 0f) : 0f;
            float rmin = ri >= 0 && w.Length > ri + 2 ? Errands.F(w[ri + 1], 0f) : 0f, rmax = ri >= 0 && w.Length > ri + 2 ? Errands.F(w[ri + 2], float.MaxValue) : float.MaxValue;
            Vector3 near = Vector3.zero;
            bool hasNear = ni >= 0 && w.Length > ni + 1 && StoryWhere.Find(w[ni + 1], out near);
            if (ni >= 0 && !hasNear) { if (_toldWait.Add(name)) Story.Log("found: «" + name + "» waits — «" + (w.Length > ni + 1 ? w[ni + 1] : "") + "» is not known yet"); return false; }
            bool ring = ri >= 0; // (no ring: the nearest)
            List<Vector3> bases = Bases(), inRing = new List<Vector3>();
            List<KeyValuePair<float, Vector3>> past = new List<KeyValuePair<float, Vector3>>(); // (every one past the inner edge, with its m: the nearest few asked «on foot?»)
            foreach (ZoneSystem.LocationInstance li in ZoneSystem.instance.m_locationInstances.Values)
            {
                if (li.m_location == null) continue;
                string pf = li.m_location.m_prefabName ?? "";
                bool named = false;
                foreach (string n in names) if (pf.StartsWith(n, StringComparison.OrdinalIgnoreCase)) { named = true; break; }
                if (!named) continue;
                Vector3 at = li.m_position;
                if (at.x * at.x + at.z * at.z < from * from || bases.Exists(b => Utils.DistanceXZ(b, at) < BaseClear)) continue; // flat: a map pick of land among the game's locations // (not by the middle, not on anyone's base)
                if (!hasNear) { inRing.Add(at); continue; } // (no place to count from: any of them, at random)
                float d = Utils.DistanceXZ(at, near); // flat: the same map pick
                if (d < rmin) continue;
                if (ring && d <= rmax) inRing.Add(at);
                else past.Add(new KeyValuePair<float, Vector3>(d, at)); // (the ring's own are asked apart)
            }
            // Which one: the ring's in a random order, then the DryNearest nearest past the inner edge, nearest first — and of each the first that
            // holds, asked as cheaply as it goes (the cheapest test first; the line is up to d/8 asks of the height, a look for buildings the objects
            // of a few zones, and there are hundreds of houses): first one that is free and that one walks to from <place> (the user 2026-11-15: not
            // across the sea); none — a free one, over water or not; none of those either — any, and the log says so. Free: not within BaseClear m
            // of a place of the story or of what the world found already (the user 2026-11-21: «щоб pickloc теж тримався від місць сюжету» — as a
            // glade does; but for <place> itself: how far from that one, the ring says), and nobody has built by it (WorldSpot.Settled: a player
            // living in the game's own house with no heart and no ward — Ketil's errand must not lead into his home)
            List<Vector3> order = Shuffled(inRing), places = new List<Vector3>();
            order.AddRange(Nearest(past, DryNearest));
            Places(places);
            if (hasNear) places.RemoveAll(b => WorldSpot.Flat(b, near) < 1f);
            System.Func<Vector3, bool> free = q => !places.Exists(b => WorldSpot.Flat(b, q) < BaseClear) && !WorldSpot.Settled(q, BuiltClear);
            Vector3 best; string how = "";
            bool found = (hasNear && First(order, q => free(q) && WorldSpot.DryWay(near, q, WorldSpot.SwimMost), out best)) || First(order, free, out best);
            if (!found) { found = First(order, q => true, out best); how = ", though every one of them is by a place of the story or by something a player built"; }
            if (!found) { Story.Log("found: no location «" + w[0] + "» for «" + name + "» (" + ask + ")"); return true; }
            StoryWorld.SetPos(Found + name, best);
            float off = hasNear ? Utils.DistanceXZ(best, near) : 0f; // flat: the log's map distance
            Story.Log("found: «" + name + "» at " + Mathf.RoundToInt(best.x) + "," + Mathf.RoundToInt(best.z) + (hasNear ? " (" + Mathf.RoundToInt(off) + " m from " + w[ni + 1] + ")" : "") + how);
            return true;
        }

        internal static int DryNearest = 12;
        private static bool First(List<Vector3> l, System.Func<Vector3, bool> holds, out Vector3 at)
        {
            foreach (Vector3 q in l) if (holds(q)) { at = q; return true; }
            at = Vector3.zero;
            return false;
        }
        private static List<Vector3> Shuffled(List<Vector3> l)
        {
            List<Vector3> s = new List<Vector3>(l);
            for (int i = s.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); Vector3 t = s[i]; s[i] = s[j]; s[j] = t; }
            return s;
        }
        private static List<Vector3> Nearest(List<KeyValuePair<float, Vector3>> l, int n)
        {
            l.Sort((a, b) => a.Key.CompareTo(b.Key));
            List<Vector3> r = new List<Vector3>();
            for (int i = 0; i < l.Count && i < n; i++) r.Add(l[i].Value);
            return r;
        }

        // the world's bases as the server knows them: its hearts of a home and its wards (the census of all the world; once a pick)
        private static List<Vector3> Bases()
        {
            List<Vector3> l = new List<Vector3>();
            foreach (Perches.Def d in Perches.Hearts) foreach (ZDO z in NpcCensus.Of(d.Prefab)) l.Add(z.GetPosition());
            foreach (ZDO z in NpcCensus.Of(WardPrefab)) l.Add(z.GetPosition());
            return l;
        }
        internal static string WardPrefab = "guard_stone";

        // every game, every LookEvery s (StoryQuests.Each): a thing waiting at a place loaded near this player — into a chest (stash), laid there (lay),
        // plants set round it (plant). One game does it: the first to claim it (the world's «stashby_<key>» = its game @ the world's second; a claim not
        // done in ClaimFor s — its game quit — is taken over). Cost: a pass over the world's story keys (StoryWorld.Texts copies the game's list) — none
        // left once its block is over (scratch)
        private static readonly Dictionary<string, float> _nearSince = new Dictionary<string, float>();
        private static float _nextLook;
        internal static void Look(Player p)
        {
            if (p == null || Time.time < _nextLook || ZoneSystem.instance == null || ZNetScene.instance == null) return;
            _nextLook = Time.time + LookEvery;
            foreach (KeyValuePair<string, string> k in StoryWorld.Texts(Stash))
            {
                string key = k.Key.Substring(Stash.Length);
                if (StoryWorld.Flag(Stashed + key)) continue;
                string[] w = k.Value.Split('|');
                Vector3 at;
                if (w.Length < 2 || !StoryWhere.Find(w[1], out at)) continue;
                if (StoryWhere.Apart(at, p.transform.position) > 40f || !ZoneSystem.instance.IsZoneLoaded(at)) { _nearSince.Remove(key); continue; } // (an inside's point hangs over its door: near only to one inside — the review 2026-11-18)
                float since;
                if (!_nearSince.TryGetValue(key, out since)) { _nearSince[key] = Time.time; continue; }
                if (Time.time - since < SettleFor) continue;
                string me = ZNet.GetUID().ToString(System.Globalization.CultureInfo.InvariantCulture), by = StoryWorld.Text(Claim + key);
                double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0d;
                int atSign = by.IndexOf('@');
                string whose = atSign > 0 ? by.Substring(0, atSign) : by;
                double claimed = atSign > 0 ? Errands.F(by.Substring(atSign + 1), 0f) : 0d;
                if (by.Length == 0 || (whose != me && now - claimed > ClaimFor)) { StoryWorld.SetText(Claim + key, me + "@" + now.ToString("0", System.Globalization.CultureInfo.InvariantCulture)); continue; } // (claimed: the next look, once the server has said whose it is)
                if (whose != me) continue; // (another game puts it)
                string how = w.Length > 3 ? w[3] : "";
                bool done = how == LayWord ? Lay(w[0], at) : how == PlantWord || Put(w[0], at, w.Length > 2 ? Errands.F(w[2], 12f) : 12f, key, how == PutWord && w.Length > 4 ? Errands.I(w[4], 1) : 1);
                if (done) { StoryWorld.SetFlag(Stashed + key, true); _nearSince.Remove(key); }
            }
        }

        // an item by its prefab's name, whatever its case (the world keeps its texts in lower case: StoryWorld.SetText)
        private static GameObject Item(string name)
        {
            if (ObjectDB.instance == null) return null;
            GameObject g = ObjectDB.instance.GetItemPrefab(name);
            if (g != null) return g;
            foreach (GameObject i in ObjectDB.instance.m_items) if (i != null && string.Equals(i.name, name, StringComparison.OrdinalIgnoreCase)) return i; // (once a stash: the list of all items)
            return null;
        }

        // One thing of the world, anyone may pick it up, let go LayUp m over a point: the game lays it down. The one place of it — the quest's «lay»,
        // a scene's «drop» (StoryScenesCast.cs). False: no such item (said in the log)
        private const string LayWord = "lay";
        internal static float LayUp = 0.4f;
        internal static bool Leave(string item, Vector3 at)
        {
            GameObject prefab = Item(item);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null) { Story.Log("found: no item «" + item + "» to lay"); return false; }
            ItemDrop.ItemData d0 = drop.m_itemData.Clone();
            d0.m_dropPrefab = prefab; d0.m_stack = 1;
            float ground;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out ground) && at.y < ground) at.y = ground; // (a place kept as a point of no height of its own — a glade's: never under the land)
            ItemDrop.DropItem(d0, 1, at + Vector3.up * LayUp, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            Story.Log("found: «" + item + "» laid at " + at.x.ToString("0.0") + "," + at.y.ToString("0.00") + "," + at.z.ToString("0.0"));
            return true;
        }
        private static bool Lay(string item, Vector3 at) { Leave(item, at); return true; } // (tried once: no such item is said, never looked for again)

        private const string PlantWord = "plant"; // (a world begun before 2026-11-18 may keep a «plant» of the old action: nothing to do — the stage's «stock:» grows them)

        // a point of an inside («in.<layout>.<point>»: where the layout says, to the decimetre) — its chest is the one standing on it, never another of the
        // room's within the usual 12 m (the user 2026-11-15: the archivist's note went into the wrong chest)
        internal static float PointChest = 1.5f;
        private static bool AtPoint(string place) { return place.StartsWith(LocInside.Place, StringComparison.Ordinal) && place.IndexOf('.', LocInside.Place.Length) > 0; }

        // into the place's own chest, else into one of the game's put there; count of it, in stacks no bigger than the game's own (a chest's slot)
        private const string PutWord = "put";
        private static bool Put(string item, Vector3 at, float r, string key, int count)
        {
            GameObject prefab = Item(item);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null) { Story.Log("found: no item «" + item + "» to put in a chest"); return true; } // (said once: nothing to wait for)
            Container chest = null; float bd = r * r;
            foreach (Collider c in Physics.OverlapSphere(at, r)) // heavy: once, the moment the place is loaded near you
            {
                Container k = c != null ? c.GetComponentInParent<Container>() : null;
                if (k == null || k.GetInventory() == null) continue;
                Piece pc = k.GetComponentInParent<Piece>(); // (a chest that is a part of a building of ours — the barrow's, «script: chest» — keeps its Piece on the building: 2026-11-15 it was passed over and a chest of the game put beside it)
                if (!(pc != null ? pc.GetCreator() == 0L : Utils.GetPrefabName(k.gameObject).StartsWith("TreasureChest", StringComparison.Ordinal))) continue; // (the place's own chest: a piece nobody built, or the game's treasure chest — never a grave, a cart, a person's cells)
                float d = (k.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; chest = k; }
            }
            if (chest == null)
            {
                GameObject pf = ZNetScene.instance.GetPrefab(ChestPrefab);
                if (pf == null) { Story.Log("found: no chest prefab «" + ChestPrefab + "»"); return true; }
                Vector3 put = at;
                RaycastHit hit;
                if (Physics.Raycast(at + Vector3.up * 1.2f, Vector3.down, out hit, 4f, LayerMask.GetMask("piece", "Default", "static_solid", "terrain"), QueryTriggerInteraction.Ignore)) put.y = hit.point.y;
                chest = UnityEngine.Object.Instantiate(pf, put, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f)).GetComponent<Container>();
                if (chest == null) return true;
                Story.Log("found: a chest put at " + Mathf.RoundToInt(put.x) + "," + Mathf.RoundToInt(put.z) + " (the place had none)");
                ZNetView made = chest.GetComponent<ZNetView>();
                if (made != null && made.IsValid()) StoryWorld.SetText(MadeChest + key, NpcKit.Ref(made.GetZDO().m_uid)); // (its own chest: the author's jump back takes it away again — Unstash)
            }
            ZNetView nv = chest.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid() && !nv.IsOwner()) nv.ClaimOwnership(); // (its owner writes its things)
            // (what is in counted before and after each stack: the game's AddItem tops up a stack already there and changes the stack it was given —
            // counted by its own m_stack, 48 planks into a chest with some wood in it came out more)
            Inventory inv = chest.GetInventory();
            string shared = drop.m_itemData.m_shared.m_name;
            // («count N» tops the chest up to N of it — what is there is counted (Lit counts what lies in the Hall's chest): the author's jump back,
            // which never takes the things out again, and the stage entered once more do not put 48 planks on 48; one thing, as ever, one more)
            // (counted whatever their world level — the game's own count takes only its world's; ours made of the world's level, as the game's
            // AddItem(GameObject) makes them: in a world of a higher level a copy of the prefab's data was of level 0, and «nothing went in»)
            int had = inv.CountItems(shared, -1, false), max = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize), want = count > 1 ? count - had : 1, inside = 0;
            if (want <= 0) { Story.Log("found: «" + item + "» — " + had + " in the chest already (" + count + " asked): nothing put"); return true; }
            while (inside < want)
            {
                ItemDrop.ItemData d0 = drop.m_itemData.Clone();
                d0.m_dropPrefab = prefab; d0.m_stack = Mathf.Min(want - inside, max); d0.m_worldLevel = (byte)Game.m_worldLevel;
                inv.AddItem(d0);
                int now = inv.CountItems(shared, -1, false) - had;
                if (now <= inside) break; // (nothing more went in: full)
                inside = now;
            }
            bool ok = inside >= want;
            Story.Log("found: «" + item + "» ×" + inside + " " + (ok ? "put" : "NOT all put (the chest is full: " + want + " asked)") + " in a chest at " + Mathf.RoundToInt(chest.transform.position.x) + "," + Mathf.RoundToInt(chest.transform.position.z));
            return true; // (tried once: a full chest is said, never looked into again each second)
        }
    }
}
#endif
