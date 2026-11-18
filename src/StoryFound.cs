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
    //   plant <item prefab> <place> <count> <m>   that item's plants («grows:» in its file, WsItems) set once round the place within m, the same
    //                                     way (the first game near): Heid's flowers at the forest's edge (Plant: each on the ground under it); a talk's
    //                                     «!replant <place>» grows them again up to that count (Replant: all picked and thrown away — no soft lock)
    //   pickglade <name> <m> near <place> ring <min> <max> [south|north|east|west] [in <land>] [edge <land> <m>] [apart <place> <m> …] [wild]
    //                                     apart: no nearer than m to that place too, several may be said («start» — the world's middle; the user 2026-11-18:
    //                                     Eyvind's meadow «300 m from the middle and from the Hall, so they do not cross»); kept in every step but the last (any open land)
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
    internal static class StoryFound
    {
        internal const string Found = "found_", Wish = "fw_", Stash = "stash_", Stashed = "stashed_", Claim = "stashby_", MadeChest = "stashchest_", Glade = "glade_", Gladed = "gladed_";
        internal static float GladeArc = 60f, ClearUnseen = 90f;
        internal static string ChestPrefab = "TreasureChest_meadows"; // (a chest of the game's own, when the place has none)
        internal static float SettleFor = 4f, LookEvery = 2f; // (s: the place's things come in over frames — its chest looked for once they are there)
        internal static float BaseClear = WorldSpot.Clear * 2f; // (m from a heart of a home or a ward: the server's own way to know a base — it sees no beds nor map marks)
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
                    string key = ThingKey(w[2], w[1]); // (a thing's own key, as lay and plant: two things into one chest — the archivist's — are two; by the place alone the second was dropped unsaid, 2026-11-15)
                    if (StoryWorld.Flag(Stashed + key) || StoryWorld.Text(Stash + key).Length > 0) return null;
                    int ci = Array.IndexOf(w, "count"), count = ci > 2 && ci + 1 < w.Length ? Errands.I(w[ci + 1], 1) : 1;
                    string m = w.Length > 3 && ci != 3 ? w[3] : AtPoint(w[2]) ? PointChest.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "12";
                    StoryWorld.SetText(Stash + key, w[1] + "|" + w[2] + "|" + m + (count > 1 ? "|" + PutWord + "|" + count.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""));
                    return null;
                } });
            StoryActions.Register("plant", new StoryActions.Kind { Dry = "set world stash:{2}:{1}", Min = 4, Usage = "plant <предмет> <місце> <скільки> <м>   (рослини цього предмета (рядок grows: у його файлі) раз довкола місця, коли хтось буде поруч)",
                Check = a => StoryWhere.Why(a[2]) ?? (Errands.I(a[3], 0) < 1 || Errands.F(a[4], 0f) <= 0f ? "plant: скільки — ціле число, м — число" : null),
                Do = (p, q, w, rest) =>
                {
                    string key = ThingKey(w[2], w[1]);
                    if (StoryWorld.Flag(Stashed + key) || StoryWorld.Text(Stash + key).Length > 0) return null;
                    StoryWorld.SetText(Stash + key, w[1] + "|" + w[2] + "|" + w[4] + "|" + PlantWord + "|" + w[3]);
                    return null;
                } });
            NpcTalk.Deeds["replant"] = (a, p, w) => { if (w.Length > 1) Replant(w[1]); }; // («!replant <place>» in a talk: the plants there grown again, up to their count)
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
        // a thing's own key at a place (lay, plant: a few things may be at one place): «<place>__<item>» — the place's key, StoryWorld.ThingSep, the item
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

        // a glade's wild taken away, once: its land made (its trees are in the world's list), nobody near to see them go
        private static void ClearGlade(string name, float r)
        {
            Vector3 at;
            if (r <= 0f || StoryWorld.Flag(Gladed + name) || !Where(name, out at) || !BlueprintGround.Made(at, r + 4f)) return;
            foreach (ZDO c in ZNet.instance.GetAllCharacterZDOS()) if (c != null && Utils.DistanceXZ(c.GetPosition(), at) < ClearUnseen) return; // (someone there: later)
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
            bool got = false;
            // every step below first only where one walks from <place> (WorldSpot.DryWay, a river swum at most: the user 2026-11-15 — the thistle edge
            // went 1753 m off across the sea); nothing anywhere so — the same steps over water
            int seaFrom = WorldSpot.SeaAll;
            for (int pass = 0; pass < 2 && !got; pass++)
            {
                if (pass == 1 && WorldSpot.SeaAll == seaFrom) break; // (nothing was turned down for the sea: over water would find the same nothing — no second round of asks)
                bool foot = pass == 0, none = Heightmap.Biome.None == edge;
                got = WorldSpot.Open(near, rmin, rmax, r, land, toward, GladeArc, taken, BaseClear, out at, out uneven, edge, edgeM, foot, fits);
                if (!got && toward != Vector3.zero) got = WorldSpot.Open(near, rmin, rmax, r, land, Vector3.zero, 180f, taken, BaseClear, out at, out uneven, edge, edgeM, foot, fits); // (none that side: any side)
                if (!got && !none) got = WorldSpot.Open(near, rmin, rmax * 3f, r, land, Vector3.zero, 180f, taken, BaseClear, out at, out uneven, edge, edgeM, foot, fits); // (an edge asked: the same edge, farther)
                if (!got && !none) got = WorldSpot.Open(near, rmin, rmax * 3f, r, land, Vector3.zero, 180f, taken, BaseClear, out at, out uneven, Heightmap.Biome.None, 0f, foot, fits); // (no such edge at all: that land, farther — a quest waits on this place, never nothing)
                if (!got) got = WorldSpot.Open(near, rmin, rmax * 2f, r, Heightmap.Biome.None, Vector3.zero, 180f, taken, BaseClear, out at, out uneven, Heightmap.Biome.None, 0f, foot, fits); // (none of that land: any, farther)
                if (!got) got = WorldSpot.Open(near, rmin, rmax * 4f, r, Heightmap.Biome.None, Vector3.zero, 180f, null, 0f, out at, out uneven, Heightmap.Biome.None, 0f, foot); // (the last: any dry open land, by a base or not)
            }
            if (!got) { Story.Log("found: no glade for «" + name + "» (" + string.Join(" ", w) + "): " + WorldSpot.Why); return true; }
            StoryWorld.SetPos(Found + name, at);
            if (!wild) StoryWorld.SetText(Glade + name, r.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            Story.Log("found: glade «" + name + "» at " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z) + " (" + Mathf.RoundToInt(Utils.DistanceXZ(at, near)) + " m from " + w[ni + 1] + ", " + WorldGenerator.instance.GetBiome(at) + ", uneven " + uneven.ToString("0.0") + " m, forest factor " + WorldGenerator.GetForestFactor(at).ToString("0.00") + "; turned down: " + WorldSpot.Why + ")");
            return true;
        }

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
            foreach (Loc place in Locs.All) { Vector3 at; Quaternion rot; if (place.Where(out at, out rot)) l.Add(at); }
            foreach (KeyValuePair<string, string> k in StoryWorld.Texts(Found)) { Vector3 at; if (StoryWorld.Pos(k.Key, out at)) l.Add(at); }
            return l;
        }

        // «<prefabs,…> [from m] [near place [ring min max]]» → the one found, kept by the world. True: settled (found, or there is none: said in the log); false —
        // «near» names a place not known here yet (the first heart's word not come): asked again, never «anywhere» instead
        private static readonly HashSet<string> _toldWait = new HashSet<string>();
        private static bool Answer(string name, string ask)
        {
            if (ZoneSystem.instance == null) return false;
            string[] w = ask.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return true;
            if (w[0] == "glade") return AnswerGlade(name, w);
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
            bool found = false; Vector3 best = Vector3.zero; float bd = float.MaxValue;
            foreach (ZoneSystem.LocationInstance li in ZoneSystem.instance.m_locationInstances.Values)
            {
                if (li.m_location == null) continue;
                string pf = li.m_location.m_prefabName ?? "";
                bool named = false;
                foreach (string n in names) if (pf.StartsWith(n, StringComparison.OrdinalIgnoreCase)) { named = true; break; }
                if (!named) continue;
                Vector3 at = li.m_position;
                if (at.x * at.x + at.z * at.z < from * from || bases.Exists(b => Utils.DistanceXZ(b, at) < BaseClear)) continue; // (not by the middle, not on anyone's base)
                float d = hasNear ? Utils.DistanceXZ(at, near) : UnityEngine.Random.value;
                if (d < rmin) continue;
                if (ring && d <= rmax) inRing.Add(at);
                if (hasNear && !(ring && d <= rmax)) past.Add(new KeyValuePair<float, Vector3>(d, at)); // (the ring's own are asked apart)
                if (d < bd) { bd = d; best = at; found = true; }
            }
            // first of those one walks to from <place> (the user 2026-11-15: not across the sea), asked as cheaply as it goes — the line is up to d/8 asks of
            // the height, and there are hundreds of houses: the ring's in a random order till the first that holds (one at random of them); none — the
            // DryNearest nearest past the inner edge, nearest first; none of those either — as before: one of the ring at random, else the nearest
            Vector3 dryAt;
            if (hasNear && FirstDry(near, Shuffled(inRing), out dryAt)) { best = dryAt; bd = Utils.DistanceXZ(best, near); }
            else if (hasNear && FirstDry(near, Nearest(past, DryNearest), out dryAt)) { best = dryAt; bd = Utils.DistanceXZ(best, near); }
            else if (inRing.Count > 0) { best = inRing[UnityEngine.Random.Range(0, inRing.Count)]; bd = hasNear ? Utils.DistanceXZ(best, near) : 0f; found = true; }
            if (!found) { Story.Log("found: no location «" + w[0] + "» for «" + name + "» (" + ask + ")"); return true; }
            StoryWorld.SetPos(Found + name, best);
            Story.Log("found: «" + name + "» at " + Mathf.RoundToInt(best.x) + "," + Mathf.RoundToInt(best.z) + (hasNear ? " (" + Mathf.RoundToInt(bd) + " m from " + w[ni + 1] + ")" : ""));
            return true;
        }

        internal static int DryNearest = 12;
        private static bool FirstDry(Vector3 from, List<Vector3> l, out Vector3 at)
        {
            foreach (Vector3 q in l) if (WorldSpot.DryWay(from, q, WorldSpot.SwimMost)) { at = q; return true; }
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
                if (Utils.DistanceXZ(at, p.transform.position) > 40f || !ZoneSystem.instance.IsZoneLoaded(at)) { _nearSince.Remove(key); continue; }
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
                bool done = how == LayWord ? Lay(w[0], at) : how == PlantWord ? Plant(w[0], at, Errands.F(w[2], 12f), w.Length > 4 ? Errands.I(w[4], 1) : 1) : Put(w[0], at, w.Length > 2 ? Errands.F(w[2], 12f) : 12f, key, how == PutWord && w.Length > 4 ? Errands.I(w[4], 1) : 1);
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

        // The plants of an item («grows:», WsItems: RavenWatch_grow_<id>) set round a point within r m, as many as asked, each on the ground under it
        // (the user 2026-11-14: the black forest is hilly — «не під землею і не в повітрі»): its stem where the land itself is under its middle
        // (the heightmap: no tree's root or rock taken for ground), PlantSink m into it; a spot too steep under its leaves (more than PlantSlope m between
        // PlantFoot m round it), in water, in a trunk, a rock or a wall, or nearer than PlantApart m to another: another spot. At most count × 12 tries;
        // too few found so — a second round, the ground PlantLoose times steeper allowed, nearer together, a little farther out (a quest waits on them:
        // never none for a hillside). Each one made is told to the census at once (NpcCensus.Added), so a replant soon after counts it. Once a place.
        // True: done — what could not be set is said in the log, never tried again
        private const string PlantWord = "plant";
        internal static float PlantApart = 1.6f, PlantSlope = 0.35f, PlantFoot = 0.35f, PlantSink = 0.05f, PlantLoose = 2f;
        private static bool Plant(string item, Vector3 at, float r, int count)
        {
            GameObject it = Item(item);
            string growName = it != null ? WsItems.GrowOf(it.name) : null;
            GameObject pf = growName != null ? ZNetScene.instance.GetPrefab(growName) : null;
            if (pf == null) { Story.Log("found: «" + item + "» has no plant to set (its file has no «grows:» line?)"); return true; }
            int mask = LayerMask.GetMask("Default", "static_solid", "piece");
            float water = ZoneSystem.instance.m_waterLevel, steepest = 0f;
            List<Vector3> put = new List<Vector3>();
            int standing = 0; // (grown again — Replant: only as many as were picked; the ones still there stay, and new ones keep apart from them)
            foreach (ZDO z in NpcCensus.Of(growName)) if (Utils.DistanceXZ(z.GetPosition(), at) <= r + 1f && !z.GetBool(ZDOVars.s_picked, false)) { standing++; put.Add(z.GetPosition()); }
            count -= standing;
            for (int round = 0; round < 2; round++)
            {
                float slope = round == 0 ? PlantSlope : PlantSlope * PlantLoose, apart = round == 0 ? PlantApart : PlantApart * 0.6f, wide = round == 0 ? r : r * 1.5f;
                for (int tries = 0; put.Count - standing < count && tries < count * 12; tries++)
                {
                    Vector2 c = UnityEngine.Random.insideUnitCircle * wide;
                    Vector3 q = new Vector3(at.x + c.x, 0f, at.z + c.y);
                    if (put.Exists(o => Utils.DistanceXZ(o, q) < apart)) continue;
                    float mid, lo, hi;
                    if (!Ground(q, out mid, out lo, out hi) || lo < water + 0.3f || hi - lo > slope) continue;
                    q.y = mid - PlantSink;
                    if (Physics.CheckCapsule(q + Vector3.up * 0.3f, q + Vector3.up * 1f, 0.25f, mask, QueryTriggerInteraction.Ignore)) continue; // heavy: at most count × 24 checks, once a place (a trunk, a root, a rock where it would stand)
                    GameObject made = UnityEngine.Object.Instantiate(pf, q, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                    ZNetView nv = made.GetComponent<ZNetView>();
                    if (nv != null && nv.IsValid()) NpcCensus.Added(nv.GetZDO());
                    put.Add(q);
                    steepest = Mathf.Max(steepest, hi - lo);
                }
            }
            Story.Log("found: " + (put.Count - standing) + " of " + Mathf.Max(0, count) + " plants of «" + item + "» set (" + standing + " still there) within " + Mathf.RoundToInt(r) + " m of " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z)
                + " (the steepest under one: " + steepest.ToString("0.00") + " m across " + (PlantFoot * 2f).ToString("0.0") + " m)");
            return true;
        }

        // The plants at a place grown again (a talk's «!replant <place>», the user 2026-11-14: all picked and thrown away — no soft lock while the quest
        // still needs them): its «plant» made undone, so the first game near sets them again — only as many as are picked (Plant counts the ones
        // standing). Its key gone with the block (scratch): after the quest, nothing to grow
        internal static void Replant(string place)
        {
            int n = 0;
            foreach (KeyValuePair<string, string> k in StoryWorld.Texts(Stash))
            {
                string[] w = k.Value.Split('|');
                if (w.Length < 4 || w[3] != PlantWord || !string.Equals(w[1], place, StringComparison.OrdinalIgnoreCase)) continue;
                string key = k.Key.Substring(Stash.Length);
                StoryWorld.SetFlag(Stashed + key, false);
                StoryWorld.Remove(Claim + key);
                n++;
            }
            Story.Log("found: replant at «" + place + "» — " + (n > 0 ? n + " kind(s) of plants will grow again as soon as someone is near" : "nothing planted there (the quest is over, or no «plant» named it)"));
        }

        // the land itself under a point (its middle) and the lowest and highest of it PlantFoot m round. False: its land not made here
        private static bool Ground(Vector3 q, out float mid, out float lo, out float hi)
        {
            lo = hi = 0f;
            if (!ZoneSystem.instance.GetGroundHeight(q, out mid)) return false;
            lo = hi = mid;
            for (int a = 0; a < 4; a++)
            {
                float deg = a * 90f * Mathf.Deg2Rad, h;
                if (!ZoneSystem.instance.GetGroundHeight(new Vector3(q.x + Mathf.Cos(deg) * PlantFoot, 0f, q.z + Mathf.Sin(deg) * PlantFoot), out h)) return false;
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
            }
            return true;
        }

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
