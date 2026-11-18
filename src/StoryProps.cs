#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The story's props (the user 2026-11-02): a thing taken up in a scene — the shield of Munin's place lifted off the
    // ground in two hands, later a charm, a feather, a key in Heid's hand. Never the thing itself: its look only (HandLook),
    // and the piece it came from hidden meanwhile — by its place (LocState.Held: one owner of a piece's look; let go, the place lays it again by its state). The look comes exactly where the piece lies, the same moment the piece
    // goes, and goes back there before the piece returns: nothing pops.
    //   state:  on the holder's own ZDO (a player's, or a person's of the story) — Key = «item|hand|piece|pose|until|size|rest|free|square|set|left|way|home»
    //           (Held.Of: a code of fewer fields — an older one — reads, what it lacks is «none»); written only by the owner (a person's through its act
    //           «prop»), and only when it changes. Nothing else is sent: every game near reads it and draws it (a hand's look, the piece hidden, the
    //           pose, the fall, the flight), so one who comes in mid-scene sees it too, and a player who leaves takes it with him (his ZDO is gone).
    //   until:  world seconds (a person's: a crashed scene's prop does not stay in its hand for good); 0 — no end
    //   hand:   l | r (none — a shield on the left, as the game holds it; anything else on the right) · a — on the right forearm · «-» — in no
    //           hand: it LIES at its rest (set down, let fall, thrown)
    //   piece:  the place's name of what it came from (prologue_munin.shchyt): each game finds it by that name, or «-»
    //   pose:   a player's clip while held (through Gestures: prop_look_<hand> — looks at it; «-» none)
    //   rest:   x,y,z,rx,ry,rz in the world: where it lay; «-» nowhere (it grows in the hand). Lying: where it lies now
    //   free:   1 — the clip moves it (the channels @Prop, @LeftHand.on, @RightHand.on: BodyClips), both hands left empty;
    //           0 — on the hand's joint, as the game holds it
    //   square: 1 — as «@Prop.w» lifts it, it turns from how it lay to square before the body (a book: the hands' points are on its edges)
    //   set:    a carried thing's clip with the parts «down» and «up»; «-» none
    //   left:   lying: 1 — by a word of the story (its own thing waits for «prop fetch»)
    //   way:    lying: how it came there — 0 set down (it glides) · 1 let fall (straight down, tipping) · 2 thrown (an arc, tumbling); each game
    //           draws that way from where its own look of the thing was to the rest (StoryPropView.Place)
    //   home:   lying, a scene's thing taken from a piece: where that piece lies (x,y,z,rx,ry,rz) — put back, it glides there from the ground; «-» none
    // The words (the scene's steps, the player's and «npc <who> prop …»): take · put · drop (what is held falls from where it is, straight down)
    // · throw <place|player|npc.x> (it flies an arc there and lies) · fetch · carry (a person's own thing: below). A first sketch of drop and
    // throw (the user 2026-11-11: the basket is the first prop, others will fall, be thrown, be laid): no physics, nothing in the way, no catching.
    // What a holder has — in hand, on the arm, lying, in flight — is a word of place too: «npc.<who>.thing», «player.thing» (StoryWhere → ThingAt).
    // The scene puts every prop back when it ends or stops (StoryScenes), whatever its file said — one let fall or thrown too, from where it lies.
    internal static class StoryProps
    {
        internal static readonly int Key = "RavenWatch_prop".GetStableHashCode();
        internal static float PoseFade = 0.5f, Tick = 0.2f, NpcFor = 600f, Glide = 0.5f;

        // ---------------------------------------------------------------- the words of a step
        // take <item> [from <place.piece>] [left|right] [pose <clip>|still] [size <m>] [free]   (w from the word after «take»)
        internal sealed class Ask
        {
            public string Item = "", From = "", Hand = "", Pose = "", Set = ""; // (Set: a carry's clip with the parts «down» and «up»)
            public float Size;
            public bool Square; // («square»: a thing that is not round turns square to the body as it is lifted, so the hands' points on it hold from any side it lay)
            public bool Free, Lasting, HasRest; // (Lasting, HasRest: the animation stage's own mannequin — no term; lies where it says)
            public Vector3 RestAt;
            public Quaternion RestTurn = Quaternion.identity;
        }

        internal static string Usage = "prop take <предмет> [from <місце.частина>] [left|right|arm] [pose <кліп>|still] [size <м>] [free] [square] · prop put · prop drop · prop throw <місце|player|npc.x>";
        // a person's own: «npc <who> prop fetch» (it may go back for its own thing) · «npc <who> prop carry» (its own thing from where it lies onto its arm now: the scene plays its «up» clip itself)
        // the words after the item: alone, and with a value after them (storycheck reads these two lists, so a typo stops the build)
        internal const string TakeStep = "prop take";
        internal static readonly string[] TakeFlags = { "left", "right", "arm", "still", "free", "square" }, TakeValued = { "from", "pose", "size" };

        // load time, the player's step and a person's alike: the words of «prop …» from the one at «at» on (take|put|drop|throw); null — fine, else what is wrong
        internal static string Check(string[] w, int at)
        {
            if (w.Length <= at) return Usage;
            if (w[at] == "put" || w[at] == "drop") return null;
            if (w[at] == "throw") return w.Length > at + 1 ? null : "prop throw <місце|player|npc.x>";
            if (w[at] != "take") return Usage;
            string why;
            return Read(w, at + 1, out why) == null ? why ?? Usage : null;
        }
        // the word of «prop …» that names a place: the piece after «from» (take), where it is thrown (throw); at: where the word after «prop» stands
        // (the step's PlaceOf; storycheck has the same two in its stepPlace)
        internal static string PlaceOf(string[] w, int at)
        {
            if (w.Length > at + 1 && w[at] == "throw") return w[at + 1];
            int from = System.Array.IndexOf(w, "from");
            return from > at && from + 1 < w.Length ? w[from + 1] : null;
        }

        // A thing of a person's OWN (the user 2026-11-10: Heid's basket): the line «carry: <item> [arm|left|right] [clip <loop>] [set <clip>]» of its
        // [person] (StoryPeople). The person leads, the thing follows (Keep): on its feet it carries it — «arm»: hung on the right forearm (ArmX…:
        // where on the bone, tuned live) with the loop clip on its arm; at anything else (sat, in bed, still at its work or meal, a pose) it sets it
        // down beside it of its own doing and goes back for it when free; a fight drops it. A word takes it off too: a scene's «npc <who> prop put», a story «hold» with a
        // clip or a gesture, a visit of its own life with a clip (NpcPlaces.ClipBegins). Then it SETS IT DOWN at its feet (its «set» clip's part
        // «down», the thing glides to the ground) and the thing LIES THERE: hand «-» in the same code, its rest where it was put, no term, drawn
        // by every game near. Free again (StoryLife: its own choices), it walks back to it («fetch», first of its places), the part «up», and the
        // thing glides from the ground onto its arm. Nothing of its own anywhere: it grows in its hand. A scene's end never touches it (Put lets
        // a lasting thing be). A scene's thing, a person's or the player's, lies only by «prop drop» or «prop throw», and only till the scene puts it back.
        internal const string CarryLine = "carry: <предмет> [arm|left|right] [clip <кліп-петля>] [set <кліп з частинами down/up>]";
        // where it hangs on the forearm (tuned live: raven npc tune StoryProps.ArmAlong …): on the line from the elbow to the wrist, ArmAlong of the way
        // (1 the wrist), ArmDrop m below it (the look is centred: half the thing's height for a handle over the arm), ArmOut m away from the body;
        // hanging plumb, its handle along the way the forearm points over the ground, turned ArmRx/Ry/Rz degrees from that. Reckoned from the bones' places each frame, never hung on a bone: the
        // skeleton's bones are scaled and their axes are their own (a look parented to the forearm came out the size of a house, 2026-11-10)
        internal static float ArmAlong = 0.7f, ArmDrop = 0.16f, ArmOut = 0f, ArmRx = 0f, ArmRy = 0f, ArmRz = 0f;
        internal static float SetAhead = 0.35f, SetAside = 0.25f, SetUp = 0f; // where it is set down: before its right foot; SetUp: m its foot rests over what it lies on, set down, fallen or thrown (its look is centred on its middle: Half lifts it by half its height)
        internal static float SetDownFor = 2f, PickAfter = 1f, KeepEvery = 2f, KeepFor = 600f, FetchWant = 3f; // s: the «down» part's length · into the «up» part when the thing leaves the ground · the arm's loop asked again · its term · how much it wants its own thing back over any other place
        internal static float FallTilt = 28f, FallFor = 0.5f; // let fall: degrees it tips over on its way down · seconds to the ground
        // thrown (a first sketch): m/s over the ground · the arc's top over the straight line, a share of the way · never quicker than this, s · whole turns
        // it tumbles on the way · thrown at a body, it lands this many m short of it (nobody catches: it lies before its feet)
        internal static float ThrowSpeed = 7f, ThrowArc = 0.25f, ThrowMin = 0.35f, ThrowTurns = 1f, ThrowShort = 0.6f;
        internal static float GroundOver = 1f, GroundDeep = 8f; // m over a spot the ray to what lies under it begins · m down it looks

        // ---------------------------------------------------------------- a thing's own numbers
        // The numbers above are one set for every thing, tuned to Heid's basket. A thing of ours says its own in its item file (assets/story/items,
        // assets/items: a Workshop model — WsModel.Hang, Lies, Falls), a line each, any word left out:
        //   hang: along 0.7 drop 0.16 out 0 rx 0 ry 0 rz 0    on a forearm                 (ArmAlong, ArmDrop, ArmOut, ArmRx, ArmRy, ArmRz)
        //   lies: ahead 0.35 aside 0.25 up 0                 set down; up (its foot over the ground) for a fall too  (SetAhead, SetAside, SetUp)
        //   falls: tilt 28 s 0.5                             let fall                     (FallTilt, FallFor)
        // What its file does not say is the static of that name (a thing of the game's — a shield — has no file: all statics). A word unknown or with
        // no number: said in the log as the thing is first held, that line read no further; storycheck stops the build on it (it reads the three lists).
        // Tuned live: «raven npc tune StoryProps.TuneAll 1» — every thing goes by the statics, its file passed over; tune them (… StoryProps.ArmDrop 0.2),
        // write what fits into the thing's file, TuneAll 0. Read once a thing a session (the files are built in)
        internal static readonly string[] HangWords = { "along", "drop", "out", "rx", "ry", "rz" }, LiesWords = { "ahead", "aside", "up" }, FallsWords = { "tilt", "s" };
        internal static bool TuneAll = false; // (set live only: raven npc tune StoryProps.TuneAll 1)
        internal struct Nums { public float ArmAlong, ArmDrop, ArmOut, ArmRx, ArmRy, ArmRz, SetAhead, SetAside, SetUp, FallTilt, FallFor; }
        private static readonly Dictionary<string, float[]> _own = new Dictionary<string, float[]>(); // (an item → its own numbers in the order of Nums, NaN where its file says none; null: no line of its own)

        internal static float[] OwnOf(string item)
        {
            float[] own;
            if (_own.TryGetValue(item, out own)) return own;
            WsModel m = WsItems.ModelOf(item);
            if (m != null && (m.Hang.Length > 0 || m.Lies.Length > 0 || m.Falls.Length > 0))
            {
                own = new float[HangWords.Length + LiesWords.Length + FallsWords.Length];
                for (int i = 0; i < own.Length; i++) own[i] = float.NaN;
                ReadOwn(item, "hang", m.Hang, HangWords, own, 0);
                ReadOwn(item, "lies", m.Lies, LiesWords, own, HangWords.Length);
                ReadOwn(item, "falls", m.Falls, FallsWords, own, HangWords.Length + LiesWords.Length);
            }
            _own[item] = own;
            return own;
        }

        private static void ReadOwn(string item, string line, string said, string[] words, float[] into, int at)
        {
            string[] w = said.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < w.Length; i += 2)
            {
                int k = System.Array.IndexOf(words, w[i]);
                float v = 0f;
                if (k < 0 || i + 1 >= w.Length || !float.TryParse(w[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                {
                    Story.Log("items: " + item + ": " + line + ": не розумію «" + w[i] + (k >= 0 ? "» без числа" : "»") + " (" + line + ": " + string.Join(" <число> ", words) + " <число>)");
                    return;
                }
                into[at + k] = v;
            }
        }

        // the numbers a thing goes by now: its own where its file says them, else the statics (own: OwnOf, kept by whoever asks each frame)
        internal static Nums NumsOf(float[] own)
        {
            Nums n;
            n.ArmAlong = Pick(own, 0, ArmAlong); n.ArmDrop = Pick(own, 1, ArmDrop); n.ArmOut = Pick(own, 2, ArmOut);
            n.ArmRx = Pick(own, 3, ArmRx); n.ArmRy = Pick(own, 4, ArmRy); n.ArmRz = Pick(own, 5, ArmRz);
            n.SetAhead = Pick(own, 6, SetAhead); n.SetAside = Pick(own, 7, SetAside); n.SetUp = Pick(own, 8, SetUp);
            n.FallTilt = Pick(own, 9, FallTilt); n.FallFor = Pick(own, 10, FallFor);
            return n;
        }
        private static float Pick(float[] own, int i, float common) { return own == null || TuneAll || float.IsNaN(own[i]) ? common : own[i]; }

        // «<item> [arm|left|right] [clip <loop>] [set <clip>]»; why: what is wrong, null fine
        internal static Ask ReadCarry(string v, out string why)
        {
            why = null;
            string[] w = v.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) { why = CarryLine; return null; }
            Ask a = new Ask { Item = w[0], Hand = "a", Lasting = true };
            for (int i = 1; i < w.Length; i++)
            {
                if (w[i] == "arm") a.Hand = "a";
                else if (w[i] == "left") a.Hand = "l";
                else if (w[i] == "right") a.Hand = "r";
                else if ((w[i] == "clip" || w[i] == "set") && i + 1 < w.Length) { if (w[i] == "clip") a.Pose = w[++i]; else a.Set = w[++i]; }
                else { why = "carry: не розумію «" + w[i] + "» (" + CarryLine + ")"; return null; }
            }
            return a;
        }

        internal static Ask Read(string[] w, int at, out string why)
        {
            why = null;
            Ask a = new Ask();
            if (w.Length <= at) { why = Usage; return null; }
            a.Item = w[at];
            for (int i = at + 1; i < w.Length; i++)
            {
                string x = w[i];
                if (x == "left" || x == "right" || x == "arm") a.Hand = x == "left" ? "l" : x == "right" ? "r" : "a";
                else if (x == "still") a.Pose = "-";
                else if (x == "free") a.Free = true;
                else if (x == "square") a.Square = true;
                else if (System.Array.IndexOf(TakeValued, x) >= 0 && i + 1 < w.Length)
                {
                    string v = w[++i];
                    if (x == "from") a.From = v;
                    else if (x == "pose") a.Pose = v;
                    else if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out a.Size) || a.Size < 0f) { why = "size <метрів>"; return null; }
                }
                else { why = "prop: не розумію «" + x + "» (" + Usage + ")"; return null; }
            }
            return a;
        }

        // ---------------------------------------------------------------- taking, putting back
        // the code for a holder: null and why when the item is not in the game
        private static string Code(Ask a, bool person, out string why)
        {
            why = null;
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(a.Item) : null;
            ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (d == null) { why = "нема предмета «" + a.Item + "»"; return null; }
            string hand = a.Hand.Length > 0 ? a.Hand : d.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield ? "l" : "r";
            string piece = "-", rest = a.HasRest ? Rest(a.RestAt, a.RestTurn) : "-";
            if (a.From.Length > 0)
            {
                ZNetView nv = StoryWhere.Piece(a.From);
                if (nv != null && nv.IsValid()) { piece = a.From; rest = RestOf(nv); }
                else why = "«" + a.From + "» нема поруч — на землі лишиться"; // (still taken: only the ground keeps its own)
            }
            string pose = a.Free ? "-" : person ? (a.Lasting && a.Pose.Length > 0 && a.Pose != "-" ? a.Pose : "-") : a.Pose.Length > 0 ? a.Pose : "prop_look_" + hand; // (a person's pose only on a thing of its own: the loop on its arm)
            if (pose != "-" && BodyClips.Find(pose) == null) { why = "нема пози «" + pose + "» — тримає без неї"; pose = "-"; }
            double until = person && !a.Lasting && ZNet.instance != null ? ZNet.instance.GetTimeSeconds() + NpcFor : 0.0;
            return a.Item + "|" + hand + "|" + piece + "|" + pose + "|" + until.ToString("0", CultureInfo.InvariantCulture) + "|" + a.Size.ToString("0.###", CultureInfo.InvariantCulture) + "|" + rest + "|" + (a.Free ? "1" : "0") + "|" + (a.Square ? "1" : "0") + "|" + (a.Set.Length > 0 ? a.Set : "-");
        }

        // where a piece's look lies: its meshes' middle, its turn (HandLook's copy is made centred, so it lands on it)
        private static string RestOf(ZNetView nv) { return Rest(Middle(nv), nv.transform.rotation); }

        // what the eye sees as its middle, not its object's root («player go» walks to this too, so a clip finds it where it expects)
        internal static Vector3 Middle(ZNetView nv)
        {
            Bounds b = new Bounds(); bool any = false;
            foreach (MeshRenderer r in nv.GetComponentsInChildren<MeshRenderer>()) // heavy: once per prop taken from a piece or walk to it
            {
                if (!r.enabled) continue;
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            }
            return any ? b.center : nv.transform.position;
        }

        private static string Rest(Vector3 at, Quaternion turn)
        {
            Vector3 e = turn.eulerAngles;
            float[] v = { at.x, at.y, at.z, e.x, e.y, e.z };
            string[] t = new string[6];
            for (int i = 0; i < 6; i++) t[i] = v[i].ToString("0.###", CultureInfo.InvariantCulture);
            return string.Join(",", t);
        }

        // the local player takes it (a scene step); a note when something was left out, else null
        internal static string Take(Player p, Ask a)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return "гравця нема";
            string why;
            string code = Code(a, false, out why);
            if (code == null) return why;
            Put(p); // (one thing a hand: the one before goes back first)
            z.Set(Key, code);
            Held h = Held.Of(code);
            if (h.Pose != "-") Gestures.Scene(p, h.Pose, PoseFade);
            if (h.Free && h.HasRest)
            {
                Vector3 before = p.transform.InverseTransformPoint(h.RestAt);
                // (… and how it is turned in his frame, each axis: with these six numbers the stage lays the thing as this scene found it —
                //  «turned N deg» alone could not be laid anywhere, 2026-11-12)
                Vector3 lay = (Quaternion.Inverse(p.transform.rotation) * h.RestTurn).eulerAngles;
                string at3 = before.x.ToString("0.00", CultureInfo.InvariantCulture) + " " + before.y.ToString("0.00", CultureInfo.InvariantCulture) + " " + before.z.ToString("0.00", CultureInfo.InvariantCulture);
                string turn3 = Mathf.DeltaAngle(0f, lay.x).ToString("0.#", CultureInfo.InvariantCulture) + " " + Mathf.DeltaAngle(0f, lay.y).ToString("0.#", CultureInfo.InvariantCulture) + " " + Mathf.DeltaAngle(0f, lay.z).ToString("0.#", CultureInfo.InvariantCulture);
                Story.Log("prop " + h.Item + " lies at " + at3 + " before him (x right, y up, z ahead), turned " + Quaternion.Angle(p.transform.rotation, h.RestTurn).ToString("0") + " deg from him — on the stage: raven stage prop " + h.Item + " " + at3 + " " + turn3 + (h.Square ? " square" : ""));
            }
            return why;
        }

        internal static void Put(Player p)
        {
            ZDO z = NetFlag.Zdo(p, true);
            string code = z != null ? z.GetString(Key, "") : "";
            if (code.Length == 0) return;
            Held h = Held.Of(code);
            if (h.Pose != "-") Gestures.SceneEnd(p, PoseFade);
            z.Set(Key, "");
        }

        // a person of the story (its owner writes: its act «prop»)
        internal static string Take(NpcActor a, Ask ask)
        {
            string why;
            string code = Code(ask, true, out why);
            if (code == null) return why;
            a.Act("prop " + code);
            return why;
        }

        // a thing of the person's OWN: no term and hung on a joint (or set down from one); a scene's thing has a term, the stage's
        // mannequin's lies free before it (AnimStage: Lasting + Free) — those two Put takes away
        private static bool Own(Held h) { return h != null && h.Until <= 0.0 && !h.Free; }

        // a scene's thing back (its end, «free»), the mannequin's prop off; a thing of the person's own (carried or lying) is let be
        internal static void Put(NpcActor a)
        {
            Held h = HeldBy(a);
            if (h != null && !Own(h)) a.Act("prop -");
        }

        // the step «npc <who> prop put»: a scene's thing back; its own thing set down where it stands (the scene plays its own clip for that)
        internal static void PutDown(NpcActor a)
        {
            Held h = HeldBy(a);
            if (h == null) return;
            if (!Own(h)) a.Act("prop -"); else SetDown(a, false, true);
        }

        // ---------------------------------------------------------------- let fall, thrown: whatever a holder holds
        // The words «prop drop», «prop throw <place>» (the player's step) and «npc <who> prop drop|throw …»: the thing in a hand, on the arm or moved
        // by a clip leaves it and comes to LIE — the same lying state as one set down, with the way it got there (Held.Fell, Thrown: every game
        // draws the fall or the flight from where its own look was). A person's own thing lies there till the story says «prop fetch»; a scene's
        // thing — a person's or the player's — till the scene puts it back (Put: it keeps its piece, its term and where the piece lies).
        // Nothing held, or lying already: nothing happens. Each returns the seconds of the way (the step waits them)

        // a held thing's code as it comes to lie at a spot: in no hand, its rest there
        private static string LyingCode(Held h, string pose, Vector3 at, Quaternion turn, bool left, int way)
        {
            string home = h.Piece != "-" && h.HasRest ? Rest(h.RestAt, h.RestTurn) : "-"; // (where it was taken from: back there when the scene ends)
            return h.Item + "|-|" + h.Piece + "|" + pose + "|" + h.Until.ToString("0", CultureInfo.InvariantCulture) + "|" + h.Size.ToString("0.###", CultureInfo.InvariantCulture) + "|" + Rest(at, turn)
                + "|0|0|" + h.Set + "|" + (left ? "1" : "0") + "|" + way.ToString(CultureInfo.InvariantCulture) + "|" + home;
        }

        // what lies under a spot — a floor, a table, the land: one ray down from «over» m above it (nothing hit: the land's own height there)
        private static int _groundMask;
        private static float Ground(Vector3 at, float over)
        {
            if (_groundMask == 0) _groundMask = LayerMask.GetMask("piece", "Default", "static_solid", "terrain");
            RaycastHit hit;
            if (Physics.Raycast(at + Vector3.up * over, Vector3.down, out hit, over + GroundDeep, _groundMask, QueryTriggerInteraction.Ignore)) return hit.point.y; // heavy: one ray, once as a thing is set down, let fall or thrown
            return ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(at) : at.y;
        }

        // Half the height of a thing's look, upright as it lies. The look is centred on its meshes' middle (HandLook.Copy), and a rest is where that middle is:
        // with the middle put on the ground the basket stood half in the earth (the user 2026-11-12: «кошик у землі на половину»). So what is set on the
        // ground is its foot: every lying spot is the ground + this + its «up». Measured once a thing and size a game (a copy of its look made and gone)
        private static readonly Dictionary<string, float> _half = new Dictionary<string, float>();
        internal static float Half(string item, float size)
        {
            string key = item + "|" + size.ToString("0.###", CultureInfo.InvariantCulture);
            float half;
            if (_half.TryGetValue(key, out half)) return half;
            half = 0f;
            GameObject look = HandLook.Copy(item, size);
            if (look != null)
            {
                float low = float.MaxValue, high = float.MinValue;
                foreach (MeshFilter f in look.GetComponentsInChildren<MeshFilter>()) // heavy: once a thing and size a game
                {
                    if (f.sharedMesh == null) continue;
                    Bounds m = f.sharedMesh.bounds;
                    Vector3[] points = f.sharedMesh.isReadable ? f.sharedMesh.vertices : new Vector3[8]; // (its own points: a turned mesh's box overstates it)
                    if (!f.sharedMesh.isReadable) for (int i = 0; i < 8; i++) points[i] = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    foreach (Vector3 c in points)
                    {
                        float y = look.transform.InverseTransformPoint(f.transform.TransformPoint(c)).y;
                        if (y < low) low = y;
                        if (y > high) high = y;
                    }
                }
                if (high > low) half = Mathf.Max(0f, -low); // (from the look's own middle down to its lowest point)
                UnityEngine.Object.Destroy(look);
            }
            else Story.Log("prop: no look of " + item + " to measure: it lies by its middle");
            _half[key] = half;
            return half;
        }
        // how high over what lies under it a thing's rest is: its foot on it (Half), its own «up» over that
        private static float Up(Nums n, Held h) { return Half(h.Item, h.Size) + n.SetUp; }

        // where it is set down: before the holder's right foot, on what lies there
        private static Vector3 SetSpot(Transform t, Nums n, Held h)
        {
            Vector3 at = t.position + t.forward * n.SetAhead + t.right * n.SetAside;
            at.y = Ground(at, GroundOver) + Up(n, h);
            return at;
        }

        // where it comes to lie, let fall: straight under where its look is on this game (no look made here: before the foot, as one set down)
        private static Vector3 FallSpot(Transform holder, Nums n, Held h)
        {
            StoryPropView v = StoryPropView.Of(holder.gameObject);
            Vector3 at;
            if (v == null || !v.Root(out at)) return SetSpot(holder, n, h);
            at.y = Ground(at, 0.05f) + Up(n, h);
            return at;
        }

        // where it lands, thrown at a point, and the seconds of its flight (atBody: ThrowShort m short of the one it is thrown at, on the thrower's side)
        private static Vector3 ThrowSpot(Transform holder, Vector3 to, bool atBody, Nums n, Held h, out float seconds)
        {
            StoryPropView v = StoryPropView.Of(holder.gameObject);
            Vector3 from;
            if (v == null || !v.Root(out from)) from = holder.position + Vector3.up;
            Vector3 back = from - to; back.y = 0f;
            if (atBody && back.magnitude > ThrowShort) to += back.normalized * ThrowShort;
            to.y = Ground(to, GroundOver) + Up(n, h);
            seconds = FlightFor(from, to);
            return to;
        }
        // the seconds of a flight: its way over the ground by ThrowSpeed (the same reckoning where it is written and where it is drawn)
        internal static float FlightFor(Vector3 from, Vector3 to) { return Mathf.Max(ThrowMin, Utils.DistanceXZ(from, to) / Mathf.Max(0.1f, ThrowSpeed)); }

        // the player's thing (his own game writes): he holds it no more, so his look at it ends with it
        private static float Leave(Player p, bool thrown, Vector3 to, bool atBody)
        {
            ZDO z = NetFlag.Zdo(p, true);
            string code = z != null ? z.GetString(Key, "") : "";
            Held h = code.Length > 0 ? Held.Of(code) : null;
            if (h == null || h.Hand == "-") return 0f;
            Nums n = NumsOf(OwnOf(h.Item));
            float seconds = n.FallFor;
            Vector3 at = thrown ? ThrowSpot(p.transform, to, atBody, n, h, out seconds) : FallSpot(p.transform, n, h);
            BodyAnimator on = h.Pose != "-" ? BodyAnimator.For(p) : null;
            if (on != null && on.ClipIn(2) == h.Pose) Gestures.SceneEnd(p, PoseFade); // (his look at the thing in his hand, still on him: off with it; a clip the scene put on him since — the throw's own — plays on)
            z.Set(Key, LyingCode(h, "-", at, p.transform.rotation, true, thrown ? 2 : 1));
            Story.Log("prop: the player " + (thrown ? "throws " : "lets fall ") + h.Item);
            return seconds;
        }
        internal static float Drop(Player p) { return Leave(p, false, Vector3.zero, false); }
        internal static float Throw(Player p, Vector3 to, bool atBody) { return Leave(p, true, to, atBody); }

        // a person's (reckoned on the game of the scene, where its look is; written where the person is run: its act «prop»)
        private static float Leave(NpcActor a, bool thrown, Vector3 to, bool atBody)
        {
            Held h = HeldBy(a);
            if (h == null || !h.Live || h.Hand == "-") return 0f;
            Nums n = NumsOf(OwnOf(h.Item));
            float seconds = n.FallFor;
            Vector3 at = thrown ? ThrowSpot(a.transform, to, atBody, n, h, out seconds) : FallSpot(a.transform, n, h);
            bool own = Own(h);
            string code = LyingCode(h, own ? h.Pose : "-", at, a.transform.rotation, true, thrown ? 2 : 1);
            if (own && MayFetch(a)) StoryWorld.SetText(FetchKey(a.Id), ""); // (an old «fetch» frees no new leaving)
            _pending.Remove(a); // (a setting down or taking up that waited for its clip's moment: the story's word is the later one)
            a.Act("prop " + code);
            Story.Log("prop: " + a.Id + (thrown ? " throws " : " lets fall ") + h.Item + (own ? ", left till the story says fetch" : ", the scene's: put back when it ends"));
            return seconds;
        }
        internal static float Drop(NpcActor a) { return Leave(a, false, Vector3.zero, false); }
        internal static float Throw(NpcActor a, Vector3 to, bool atBody) { return Leave(a, true, to, atBody); }

        // the word «npc <who> prop fetch» (a quest's start:, a scene's step): a thing the story had it set down may be gone back for — when it is
        // free and at home (StoryLife). The world's key, as «hold» and «live»: the person need not be near, nor loaded on this game.
        // Taken off by the story again (SetDown byStory) or taken up (PickUp): the key is cleared, so an old word frees no later thing
        private static string FetchKey(string who) { return "propfetch_" + who; }
        internal static void Fetch(string who) { StoryWorld.SetText(FetchKey(who), "1"); Story.Log("prop: " + who + " may go back for its own thing"); }
        internal static bool MayFetch(NpcActor a) { return StoryWorld.Text(FetchKey(a.Id)) == "1"; }
        // the word «npc <who> prop carry»: its own thing on its arm again at once, wherever it lies (no walk, no clip) — a check's «test:» for a
        // scene that sets it down (heid_meet 2026-11-10: in the author's world, past the prologue, the basket lay, and «if npcprop» skipped it all).
        // The world's key, taken by Keep where the person is run
        private static string CarryKey(string who) { return "propcarry_" + who; }
        internal static void Carry(string who) { StoryWorld.SetText(CarryKey(who), "1"); Story.Log("prop: " + who + " gets its own thing on its arm again"); }

        private static Held HeldBy(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            string code = z != null ? z.GetString(Key, "") : "";
            return code.Length > 0 ? Held.Of(code) : null;
        }

        // A holder's thing as a word of place («npc.<who>.thing», «player.thing»: StoryWhere): the middle of its look on this game, wherever it is —
        // in a hand, on the arm, lying, falling, in flight. False: it has none, or its look is not made here (a view found by its body: no search)
        internal static bool ThingAt(GameObject holder, out Vector3 at)
        {
            StoryPropView v = StoryPropView.Of(holder);
            at = Vector3.zero;
            return v != null && v.Middle(out at);
        }
        // held, or on its way to where it will lie: asked again each frame by what films or looks at it; lying still — found once. None: asked again
        internal static bool ThingMoves(GameObject holder) { StoryPropView v = StoryPropView.Of(holder); return v == null || v.Moving; }

        // holds or has lying anything at all (live)
        internal static bool Holds(NpcActor a) { Held h = HeldBy(a); return h != null && h.Live; }
        // the thing of its own written for this person («carry:» of its [person], StoryPeople); null: none
        internal static Ask CarryOf(NpcActor a)
        {
            if (a == null) return null;
            foreach (StoryPeople.Person x in StoryPeople.All) if (x.Npc == a.Id) return x.Carry;
            return null;
        }
        // a thing in its hand or on its arm now (the condition «npcprop <who>»); one lying on the ground: no
        internal static bool InHand(NpcActor a) { Held h = HeldBy(a); return h != null && h.Live && h.Hand != "-"; }
        // its own thing lies on the ground: where; left — by a word of the story (it waits for «prop fetch»), else by its own doing.
        // A scene's thing let fall or thrown is the scene's: never gone back for
        internal static bool Lying(NpcActor a, out Vector3 at, out Quaternion turn, out bool left)
        {
            Held h = HeldBy(a);
            at = Vector3.zero; turn = Quaternion.identity; left = false;
            if (h == null || !Own(h) || h.Hand != "-" || !h.HasRest) return false;
            at = h.RestAt; turn = h.RestTurn; left = h.Left;
            return true;
        }

        // its own thing off its arm, onto the ground before its right foot, where it stands now (run where it is run: a.Act goes to its owner).
        // withClip: the «down» part of its «set» clip plays first (the thing leaves the arm as the hand is down); returns the seconds that takes,
        // 0 when there was nothing to set down (NpcPlaces.ClipBegins waits that long before its own clip).
        // byStory: a word of the story took it off (a scene, a hold) — it lies till the story says «prop fetch»; else its own doing (a visit): it goes back for it itself
        // (let fall or thrown, not set down: Drop, Throw above — the same lying state, another way to it)
        internal static float SetDown(NpcActor a, bool withClip, bool byStory)
        {
            Held h = HeldBy(a);
            if (h == null)
            {
                // it has nothing yet (made a moment ago, an old save) and a thing of its own is written for it: the thing it would have set down lies at its feet
                Ask own = CarryOf(a);
                if (own == null) return 0f;
                string why; string grown = Code(new Ask { Item = own.Item, Hand = own.Hand, Pose = own.Pose, Set = own.Set, Lasting = true }, true, out why);
                if (grown == null) { Story.Log("prop: " + a.Id + ": " + why); return 0f; }
                h = Held.Of(grown); withClip = false;
            }
            else if (h.Hand == "-" || !Own(h)) return 0f; // (lying already; a scene's thing is the scene's, the mannequin's its own)
            string code = LyingCode(h, h.Pose, SetSpot(a.transform, NumsOf(OwnOf(h.Item)), h), a.transform.rotation, byStory, 0); // (on what lies before its foot — a floor too: by the land's height alone it would lie under the boards)
            float wait = 0f;
            if (withClip && h.Set != "-" && a.Body != null && a.Body.Clip(h.Set + ".down")) wait = SetDownFor;
            if (byStory && MayFetch(a)) StoryWorld.SetText(FetchKey(a.Id), ""); // (an old «fetch» frees no new leaving)
            Later(a, wait > 0f ? PickAfter : 0f, () => a.Act("prop " + code));
            Story.Log("prop: " + a.Id + " sets " + h.Item + " down" + (wait > 0f ? " (" + h.Set + ".down)" : "") + (byStory ? ", left till the story says fetch" : ""));
            return wait;
        }

        // its own thing back: from where it lies (it glides from the ground onto its arm), or, lying nowhere, grown in its hand.
        // withClip: the «up» part of its «set» clip first, the thing leaves the ground PickAfter s into it
        internal static void PickUp(NpcActor a, Ask carry, bool withClip)
        {
            Ask ask = new Ask { Item = carry.Item, Hand = carry.Hand, Pose = carry.Pose, Set = carry.Set, Lasting = true };
            Vector3 lie; Quaternion turn; bool left;
            bool lay = Lying(a, out lie, out turn, out left);
            if (lay) { ask.HasRest = true; ask.RestAt = lie; ask.RestTurn = turn; }
            string why;
            string code = Code(ask, true, out why);
            if (code == null) { Story.Log("prop: " + a.Id + ": " + why); return; }
            if (why != null) Story.Log("prop: " + a.Id + ": " + why);
            float wait = withClip && lay && carry.Set.Length > 0 && a.Body != null && a.Body.Clip(carry.Set + ".up") ? PickAfter : 0f;
            if (MayFetch(a)) StoryWorld.SetText(FetchKey(a.Id), ""); // (the word «fetch» spent)
            Later(a, wait, () => a.Act("prop " + code));
            Story.Log("prop: " + a.Id + " takes " + carry.Item + (lay ? " up from the ground" : " (grows in its hand)"));
        }

        // ---------------------------------------------------------------- where a person is run: what waits, and its arm's loop kept on
        private sealed class Pending { public float At; public System.Action Do; }
        private static readonly Dictionary<NpcActor, Pending> _pending = new Dictionary<NpcActor, Pending>();
        private static readonly Dictionary<NpcActor, float> _kept = new Dictionary<NpcActor, float>();
        private static void Later(NpcActor a, float seconds, System.Action what)
        {
            if (seconds <= 0f) { what(); return; }
            _pending[a] = new Pending { At = Time.time + seconds, Do = what };
        }

        internal static void Keep(NpcActor a)
        {
            Pending w;
            if (_pending.TryGetValue(a, out w) && Time.time >= w.At) { _pending.Remove(a); w.Do(); }
            float next;
            if (_kept.TryGetValue(a, out next) && Time.time < next) return;
            _kept[a] = Time.time + KeepEvery;
            if (StoryWorld.Text(CarryKey(a.Id)) == "1") // («prop carry»: within KeepEvery — heid_meet sets it down 4 s in)
            {
                StoryWorld.SetText(CarryKey(a.Id), "");
                Ask mine = CarryOf(a);
                if (mine != null) PickUp(a, mine, false); else Story.Log("prop: " + a.Id + " has no thing of its own («carry:» of its person)");
                return;
            }
            Held h = HeldBy(a);
            if (a.Body == null || a.Mind == null) return;
            // The person leads, the thing follows (the user 2026-11-10: Heid sat by the fire with her arm up in the basket's loop — «she must be
            // herself, and only at the moments switch to the basket's clips»). On its feet (stands, walks, follows, wanders, travels) it carries
            // it, the arm's loop on; at anything else, once there (sat, lying, still at its work, its meal, a pose) it sets it down beside it —
            // with the «down» clip when it stands and nothing but the loop has its body, else it glides down — and its own life goes on; in a
            // fight or a flight it drops it at once. Lying by its own doing, it goes back for it when free (StoryLife «fetch»).
            NpcMind.Task now = a.Mind.Now;
            bool onFeet = now == NpcMind.Task.Idle || now == NpcMind.Task.Go || now == NpcMind.Task.Follow || now == NpcMind.Task.Wander || now == NpcMind.Task.Travel;
            bool scene = a.Mind.Of(NpcMind.Layer.Scene) != null, seated = a.Body.AttachedSeen.Length > 0 || a.Body.SeatsNow; // (a seat, a bed, the ground by a gesture)
            if (h == null || !h.Live)
            {
                // nothing on it and nothing lying (made a moment ago, an old save): a thing of its own is hers — in her hand, when on her feet and nothing else has her arms
                Ask own = CarryOf(a);
                if (own != null && !scene && onFeet && !seated && a.Body.ClipFree) PickUp(a, own, false);
                return;
            }
            if (!Own(h) || h.Hand == "-") return; // (its own thing on its arm; lying, it waits to be fetched)
            if (scene) return; // (a scene's clips are the scene's: it sets the thing down by its own word, «npc <who> prop put»)
            Character ch = a.GetComponent<Character>();
            BodyAnimator anim = ch != null ? BodyAnimator.Of(ch) : null;
            bool loopOn = anim != null && h.Pose != "-" && anim.ClipIn(NpcBody.ClipSlot) == h.Pose;
            if (!onFeet || seated)
            {
                bool fight = now == NpcMind.Task.Attack || now == NpcMind.Task.Flee;
                bool still = ch == null || ch.GetVelocity().magnitude < StillSpeed;
                if (!fight && !seated && !still) return; // (on its way there: it carries it till it is there)
                bool withClip = !fight && !seated && (a.Body.ClipFree || loopOn); // (a standing body: the «down» clip; sat already — it glides down)
                if (!withClip && loopOn) a.Body.Clip(""); // (the arm's loop off with it)
                SetDown(a, withClip, false);
                return;
            }
            if (h.Pose == "-" || loopOn) return;
            string over = anim != null ? anim.ClipIn(NpcBody.ClipSlot) : null;
            bool ending = over != null && !anim.Holding(NpcBody.ClipSlot, over); // (a move over the loop on its way out: the loop takes the arm from where that leaves it — no arm let down and raised again)
            if (!a.Body.ClipFree && !ending) { _kept[a] = Time.time + KeepSoon; return; } // (another clip on it: the arm follows that one, the thing follows the arm; looked at again soon, for its end)
            a.Body.Clip(h.Pose, KeepFor);
        }
        internal static float KeepSoon = 0.25f; // s: the next look while another clip has the arm of one that carries
        internal static float StillSpeed = 0.3f; // m/s: below it the person is there, at what it went to do

        internal static void Forget(NpcActor a) { _pending.Remove(a); _kept.Remove(a); }

        internal static void Wire() { NpcActor.Acts["prop"] = (n, w) => { ZDO z = n.Zdo; if (z != null) z.Set(Key, w.Length > 1 && w[1] != "-" ? w[1] : ""); }; }

        // ---------------------------------------------------------------- what a code says
        internal sealed class Held
        {
            public string Item = "", Hand = "r", Piece = "-", Pose = "-", Set = "-"; public double Until; public float Size; // (Hand: l · r · a the right forearm · - lying at its rest)
            public bool HasRest, Free, Square; public Vector3 RestAt; public Quaternion RestTurn = Quaternion.identity;
            public bool Fell, Thrown; // (lying, the way it came there — the field «way»: 1 let fall, drawn falling from where it was to where it lies · 2 thrown, drawn flying an arc there: Place; neither: set down)
            public bool Left; // (lying: set down by a word of the story — it stays till the story says «prop fetch»; by its own life — it goes back for it itself)
            public bool HasHome; public Vector3 HomeAt; public Quaternion HomeTurn = Quaternion.identity; // (lying, a scene's thing: where the piece it came from lies — put back, it glides there)
            internal static Held Of(string code)
            {
                Held h = new Held();
                string[] f = (code ?? "").Split('|');
                if (f.Length > 0) h.Item = f[0];
                if (f.Length > 1) h.Hand = f[1];
                if (f.Length > 2) h.Piece = f[2];
                if (f.Length > 3) h.Pose = f[3];
                if (f.Length > 4) double.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out h.Until);
                if (f.Length > 5) float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out h.Size);
                if (f.Length > 6) h.HasRest = Spot(f[6], ref h.RestAt, ref h.RestTurn);
                if (f.Length > 7) h.Free = f[7] == "1";
                if (f.Length > 8) h.Square = f[8] == "1";
                if (f.Length > 9 && f[9].Length > 0) h.Set = f[9];
                if (f.Length > 10) h.Left = f[10] == "1";
                if (f.Length > 11) { h.Fell = f[11] == "1"; h.Thrown = f[11] == "2"; }
                if (f.Length > 12) h.HasHome = Spot(f[12], ref h.HomeAt, ref h.HomeTurn);
                return h;
            }
            // «x,y,z,rx,ry,rz» in the world; «-» or anything else: none (false, nothing written)
            private static bool Spot(string s, ref Vector3 at, ref Quaternion turn)
            {
                string[] g = s.Split(',');
                if (g.Length < 6) return false;
                float[] v = new float[6];
                for (int i = 0; i < 6; i++) if (!float.TryParse(g[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return false;
                at = new Vector3(v[0], v[1], v[2]);
                turn = Quaternion.Euler(v[3], v[4], v[5]);
                return true;
            }
            internal bool Live { get { return Item.Length > 0 && (Until <= 0.0 || ZNet.instance == null || ZNet.instance.GetTimeSeconds() < Until); } }
        }

        // ---------------------------------------------------------------- every game: what it draws

        private static float _next;
        private static ZNet _world;
        private static readonly Dictionary<string, ZNetView> _hidden = new Dictionary<string, ZNetView>(); // (the pieces held now, by name: hidden through the place, LocState.Held)
        private static readonly HashSet<string> _want = new HashSet<string>();

        // a few times a second (StoryQuests.Tick): the holders near, their hands, the pieces they took
        internal static void Look()
        {
            if (Time.time < _next || ZNet.instance == null) return;
            _next = Time.time + Tick;
            if (_world != ZNet.instance) { _world = ZNet.instance; Forget(); } // (another world: nothing of the last one kept)
            _want.Clear();
            foreach (Player o in Player.GetAllPlayers()) See(o);
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null) continue;
                ZDO z = a.Zdo;
                if ((z == null || z.GetString(Key, "").Length == 0) && !StoryPropView.On(a.gameObject)) continue; // (holds nothing and drew nothing: no look at its parts — 25 people, five times a second)
                See(a.GetComponent<Character>());
            }
            Pieces();
        }

        private static void See(Character c)
        {
            if (c == null) return;
            ZNetView nv = c.GetComponent<ZNetView>();
            string code = nv != null && nv.IsValid() ? nv.GetZDO().GetString(Key, "") : "";
            StoryPropView v = c.GetComponent<StoryPropView>();
            Held h = code.Length > 0 ? Held.Of(code) : null;
            if (h != null && !h.Live) h = null;
            if (h == null)
            {
                if (v == null) return;
                v.Set("");
                if (v.Back) v.Clear(); // (back where it lay: it goes the moment the piece returns, below)
                else if (v.Piece != null) _want.Add(v.Piece); // (still on its way back: the piece waits for it)
                return;
            }
            if (v == null) v = c.gameObject.AddComponent<StoryPropView>();
            v.Set(code);
            if (h.Piece != "-" && v.Piece != null) _want.Add(h.Piece); // (its look not made: the piece stays where it lies, or it just vanishes with nothing in its place)
        }

        // the pieces held now: hidden (their look and their touch); the ones let go: back as they were
        private static void Pieces()
        {
            List<string> back = null;
            foreach (KeyValuePair<string, ZNetView> kv in _hidden)
                if (!_want.Contains(kv.Key) || kv.Value == null) (back ?? (back = new List<string>())).Add(kv.Key); // (one unloaded meanwhile: found anew when it is back)
            if (back != null) foreach (string name in back) Show(name);
            foreach (string name in _want) if (!_hidden.ContainsKey(name)) Hide(name);
        }

        // the place hides it and lays it back by its state (LocState.Held): never its looks put off and on here, beside the place's own
        private static void Hide(string name)
        {
            ZNetView nv = StoryWhere.Piece(name);
            if (nv == null) return; // (not loaded here: asked again next look)
            LocState.Held(nv, true);
            _hidden[name] = nv;
        }

        private static void Show(string name)
        {
            ZNetView nv;
            if (!_hidden.TryGetValue(name, out nv)) return;
            if (nv != null) LocState.Held(nv, false);
            _hidden.Remove(name);
        }

        private static void Forget() { _hidden.Clear(); _want.Clear(); }
    }

    // A holder's thing on this game: its look on the hand's joint or where the clip says, what the hands held before hidden
    // meanwhile (the game may put its weapon back there any moment: hidden again each frame while held). Lying: its look at its rest, come
    // there from where it was as the hand let go — set down (a glide), let fall (straight down, tipping), thrown (an arc, tumbling): Place.
    [DefaultExecutionOrder(11000)] // (after every pose, a person's too: NpcPose)
    internal sealed class StoryPropView : MonoBehaviour
    {
        private string _code = "";
        private StoryProps.Held _h;
        private GameObject _look;
        private Transform _joint;
        private BodyAnimator _anim;
        private Vector3 _inHand;
        private Transform _wrist; // (on the forearm: the hand bone, the far end of the line the thing hangs on)
        private Vector3 _fromAt; private Quaternion _fromTurn; private bool _from; // (lying: from where the look was as the hand let go, to its rest; put back from the ground: from where it lay)
        private float[] _own; // (the thing's own numbers, its file's: StoryProps.OwnOf — asked once as its look is made)
        private float _for; // (lying: the seconds of its way to its rest — a fall's, a flight's; 0: StoryProps.Glide, as every other way)
        private float _g; // (0: where it lay, or nothing yet · 1: taken)
        private Quaternion _asLay = Quaternion.identity; // (how it lay against the body as it took it: a clip reckons the thing as lying square before the body)
        private bool _leaving;
        private readonly List<GameObject> _hid = new List<GameObject>();

        // the piece it came from, while its look is out
        internal string Piece { get { return _look != null && _h != null && _h.Piece != "-" ? _h.Piece : null; } }
        // let go and arrived
        internal bool Back { get { return _leaving && _g <= 0f; } }
        // the size of its look in its own frame (the stage says it: the clip's points on it are reckoned from this)
        internal Vector3 Size { get { return _box.size; } }
        private Bounds _box;
        // where it is now: the middle of what the eye sees of it (the word of place «….thing») · its look's own point (what a fall goes straight down from)
        internal bool Middle(out Vector3 at) { at = _look != null ? _look.transform.TransformPoint(_box.center) : Vector3.zero; return _look != null; }
        internal bool Root(out Vector3 at) { at = _look != null ? _look.transform.position : Vector3.zero; return _look != null; }
        // in a hand, on the arm, moved by a clip, or still on its way (falling, in flight, gliding down or back)
        internal bool Moving { get { return _look != null && (_leaving || _h.Hand != "-" || _g < 1f); } }

        // how far a point is from the thing, m; below zero: inside it. Round when its two larger sides are alike (a shield),
        // else a box. For the stage's measure only: no one in play asks
        internal float Distance(Vector3 world)
        {
            if (_look == null) return 99f;
            if (_parts.Count > 1) // (a thing that is not one box — a basket under its handle: the nearest of its boxes)
            {
                Vector3 p = _look.transform.InverseTransformPoint(world); float best = 99f;
                foreach (Bounds part in _parts) best = Mathf.Min(best, BoxDistance(p - part.center, part.extents));
                return best;
            }
            Vector3 q = _look.transform.InverseTransformPoint(world) - _box.center, e = _box.extents;
            int thin = e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;
            float a = thin == 0 ? e.y : e.x, b = thin == 2 ? e.y : e.z, qa = thin == 0 ? q.y : q.x, qb = thin == 2 ? q.y : q.z;
            float out0 = Mathf.Abs(q[thin]) - e[thin];
            if (Mathf.Abs(a - b) < 0.1f * Mathf.Max(a, b))
            {
                float radial = Mathf.Sqrt(qa * qa + qb * qb) - Mathf.Max(a, b);
                return radial > 0f || out0 > 0f ? new Vector2(Mathf.Max(radial, 0f), Mathf.Max(out0, 0f)).magnitude : Mathf.Max(radial, out0);
            }
            return BoxDistance(q, e);
        }
        // a point (from a box's middle) against that box: m off it, below zero inside
        private static float BoxDistance(Vector3 q, Vector3 e)
        {
            Vector3 d = new Vector3(Mathf.Abs(q.x) - e.x, Mathf.Abs(q.y) - e.y, Mathf.Abs(q.z) - e.z);
            return d.x > 0f || d.y > 0f || d.z > 0f ? Vector3.Max(d, Vector3.zero).magnitude : Mathf.Max(d.x, Mathf.Max(d.y, d.z));
        }
        // … and the nearest point of that box's surface to it (inside: out by the nearest face)
        private static Vector3 BoxClosest(Vector3 q, Vector3 e)
        {
            Vector3 c = new Vector3(Mathf.Clamp(q.x, -e.x, e.x), Mathf.Clamp(q.y, -e.y, e.y), Mathf.Clamp(q.z, -e.z, e.z));
            if (c == q) { Vector3 g = e - new Vector3(Mathf.Abs(q.x), Mathf.Abs(q.y), Mathf.Abs(q.z)); int i = g.x <= g.y && g.x <= g.z ? 0 : g.y <= g.z ? 1 : 2; c[i] = Mathf.Sign(q[i]) * e[i]; }
            return c;
        }

        // The thing's shape for the stage's measure: ONE box (or a round) when its points fill their box — a book, a shield —, else the boxes its
        // points fall into when cut where that frees the most room (a basket: its body and the slab of its handle), SplitDeep cuts deep at most.
        // A cut is kept only if the two boxes together are under SplitGain of the one (a half of a round is half its box: no cut). The hole under a
        // handle's arch stays solid: a hand on the handle's top reads right, a hand through the arch does not. Once a look made.
        // Its points are a mesh's corners, not its room: a plain box of 48 corners, all on its two ends, would fall into two end slabs — so only a mesh of
        // SplitPoints points or more is cut at all (a modelled thing; fewer: one box), and a cut is kept only when each side's points reach over SplitFill
        // of that side's own length along the cut (a slab at one end of an empty half is no part of a thing).
        internal static float SplitGain = 0.75f, SplitLeast = 0.05f, SplitFill = 0.6f; internal static int SplitDeep = 3, SplitCuts = 9, SplitPoints = 300;
        private readonly List<Bounds> _parts = new List<Bounds>();
        private static Bounds BoundsOf(List<Vector3> pts) { Bounds b = new Bounds(pts[0], Vector3.zero); for (int i = 1; i < pts.Count; i++) b.Encapsulate(pts[i]); return b; }
        private static float Room(Bounds b) { Vector3 s = Vector3.Max(b.size, Vector3.one * 0.005f); return s.x * s.y * s.z; }
        private static void Split(List<Vector3> pts, int deep, List<Bounds> into)
        {
            Bounds whole = BoundsOf(pts);
            if (deep >= SplitDeep || pts.Count < SplitPoints) { into.Add(whole); return; }
            float best = Room(whole) * SplitGain; int axis = -1; float at = 0f;
            for (int ax = 0; ax < 3; ax++)
                for (int k = 1; k <= SplitCuts; k++)
                {
                    float cut = whole.min[ax] + whole.size[ax] * k / (SplitCuts + 1);
                    Bounds lo = new Bounds(), hi = new Bounds(); int nl = 0, nh = 0;
                    foreach (Vector3 p in pts) { if (p[ax] < cut) { if (nl++ == 0) lo = new Bounds(p, Vector3.zero); else lo.Encapsulate(p); } else { if (nh++ == 0) hi = new Bounds(p, Vector3.zero); else hi.Encapsulate(p); } }
                    if (nl < pts.Count * SplitLeast || nh < pts.Count * SplitLeast) continue;
                    if (lo.size[ax] < SplitFill * (cut - whole.min[ax]) || hi.size[ax] < SplitFill * (whole.max[ax] - cut)) continue;
                    float room = Room(lo) + Room(hi);
                    if (room < best) { best = room; axis = ax; at = cut; }
                }
            if (axis < 0) { into.Add(whole); return; }
            List<Vector3> a = new List<Vector3>(), b = new List<Vector3>();
            foreach (Vector3 p in pts) (p[axis] < at ? a : b).Add(p);
            Split(a, deep + 1, into); Split(b, deep + 1, into);
        }
        // its boxes in the world (one: the whole): each middle and its size in metres — the stage's grip sheet
        internal void Parts(List<Vector3> centres, List<Vector3> sizes)
        {
            if (_look == null) return;
            Transform t = _look.transform;
            foreach (Bounds b in _parts) { centres.Add(t.TransformPoint(b.center)); sizes.Add(Vector3.Scale(b.size, t.lossyScale)); }
        }

        // the nearest point of its surface to a point, in the world (the same shape as Distance: a grip is turned to this, not to its middle — a palm
        // on a wide book's face near its rim read «turned away», 2026-11-07)
        internal Vector3 Closest(Vector3 world)
        {
            if (_look == null) return world;
            Transform t = _look.transform;
            if (_parts.Count > 1)
            {
                Vector3 p = t.InverseTransformPoint(world), nearest = p; float least = 99f;
                foreach (Bounds part in _parts) { Vector3 on = BoxClosest(p - part.center, part.extents) + part.center; float far = (on - p).magnitude; if (far < least) { least = far; nearest = on; } }
                return t.TransformPoint(nearest);
            }
            Vector3 q = t.InverseTransformPoint(world) - _box.center, e = _box.extents;
            int thin = e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;
            float a = thin == 0 ? e.y : e.x, b = thin == 2 ? e.y : e.z;
            Vector3 c;
            if (Mathf.Abs(a - b) < 0.1f * Mathf.Max(a, b))
            {
                Vector3 flat = q; flat[thin] = 0f;
                float r = Mathf.Max(a, b), m = flat.magnitude;
                if (m > r) flat *= r / m;
                c = flat; c[thin] = Mathf.Clamp(q[thin], -e[thin], e[thin]);
                if (m <= r && Mathf.Abs(q[thin]) <= e[thin]) { if (r - m < e[thin] - Mathf.Abs(q[thin])) c = flat.sqrMagnitude > 1e-8f ? flat.normalized * r + Vector3.Scale(q, AxisOnly(thin)) : c; else c[thin] = Mathf.Sign(q[thin]) * e[thin]; } // (inside: to the nearer face)
            }
            else
            {
                c = new Vector3(Mathf.Clamp(q.x, -e.x, e.x), Mathf.Clamp(q.y, -e.y, e.y), Mathf.Clamp(q.z, -e.z, e.z));
                if (c == q) { Vector3 g = e - new Vector3(Mathf.Abs(q.x), Mathf.Abs(q.y), Mathf.Abs(q.z)); int i = g.x <= g.y && g.x <= g.z ? 0 : g.y <= g.z ? 1 : 2; c[i] = Mathf.Sign(q[i]) * e[i]; } // (inside: out by the nearest face)
            }
            return t.TransformPoint(c + _box.center);
        }
        private static Vector3 AxisOnly(int i) { Vector3 v = Vector3.zero; v[i] = 1f; return v; }

        // the way out of its surface at a point near it, in the world: the slope of Distance (so the same shape, a box or a round), by six
        // looks SlopeStep apart — the stage's measure of how a finger lies on it (BodyCheck.GripOf)
        internal static float SlopeStep = 0.004f;
        internal Vector3 Normal(Vector3 world)
        {
            float e = SlopeStep;
            Vector3 g = new Vector3(Distance(world + Vector3.right * e) - Distance(world - Vector3.right * e), Distance(world + Vector3.up * e) - Distance(world - Vector3.up * e), Distance(world + Vector3.forward * e) - Distance(world - Vector3.forward * e));
            return g.sqrMagnitude > 1e-10f ? g.normalized : Vector3.up;
        }

        // its box in the world: the middle, the turn, the size in metres; round: Distance reads it as a round (the stage's grip sheet says
        // it, and tools/grip.js reckons a hand on the same shape)
        internal bool Box(out Vector3 centre, out Quaternion turn, out Vector3 size, out bool round)
        {
            centre = Vector3.zero; turn = Quaternion.identity; size = Vector3.zero; round = false;
            if (_look == null) return false;
            Transform t = _look.transform;
            centre = t.TransformPoint(_box.center); turn = t.rotation; size = Vector3.Scale(_box.size, t.lossyScale);
            Vector3 e = _box.extents;
            int thin = e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;
            float a = thin == 0 ? e.y : e.x, b = thin == 2 ? e.y : e.z;
            round = _parts.Count <= 1 && Mathf.Abs(a - b) < 0.1f * Mathf.Max(a, b);
            return true;
        }

        internal void Set(string code)
        {
            if (code == _code) return;
            if (code.Length == 0)
            {
                _code = ""; _leaving = _look != null;
                if (!_leaving) { Clear(); return; }
                if (_h != null && _h.Hand == "-") { _from = true; _fromAt = _look.transform.position; _fromTurn = _look.transform.rotation; _g = 1f; } // (put back from the ground — or from the air, still on its way: from where it is this moment)
                if (_h != null && _h.Piece != "-" && !LocState.WantsShown(StoryWhere.Piece(_h.Piece))) _g = 0f; // (its piece gone for good — a letter read: it does not glide back to nothing, a second player has no black to hide it)
                return;
            }
            Vector3 wasAt = Vector3.zero; Quaternion wasTurn = Quaternion.identity; bool was = _look != null; // (the same thing going from its hand to the ground: from where it is)
            if (was) { wasAt = _look.transform.position; wasTurn = _look.transform.rotation; }
            Clear();
            _code = code;
            _h = StoryProps.Held.Of(code);
            VisEquipment ve = GetComponent<VisEquipment>();
            if (ve == null) return;
            bool lying = _h.Hand == "-", arm = _h.Hand == "a";
            _joint = lying ? null : _h.Hand == "l" ? ve.m_leftHand : arm ? BodyClips.SkeletonBone(GetComponent<Character>(), null, "RightForeArm") : ve.m_rightHand;
            if (_joint == null && !lying && (!_h.Free || arm)) return; // (on the forearm of a body with no such bone: nothing to hang it on, «free» or not — ArmPose reads that bone each frame)
            _look = _h.Free || _h.Size > 0f || lying || arm ? HandLook.Copy(_h.Item, _h.Size, _h.Free || lying || arm) : HandLook.Make(_h.Item, _h.Size); // (a copy with no grip of the game's: on the ground, or hung on a bone)
            if (_look == null) { Story.Log("prop: no look for " + _h.Item); return; }
            _box = Measure();
            _own = StoryProps.OwnOf(_h.Item);
            if (lying)
            {
                StoryProps.Nums n = StoryProps.NumsOf(_own);
                if (!_h.HasRest) { _h.RestAt = transform.position + transform.forward * n.SetAhead; _h.RestTurn = transform.rotation; }
                _from = was; _fromAt = wasAt; _fromTurn = wasTurn;
                _for = !_from ? 0f : _h.Thrown ? StoryProps.FlightFor(_fromAt, _h.RestAt) : _h.Fell ? n.FallFor : 0f; // (a fall's seconds, a flight's by its way; seen only after it lay: grown there as ever)
                _look.transform.SetPositionAndRotation(_from ? _fromAt : _h.RestAt, _from ? _fromTurn : _h.RestTurn);
                _g = 0f; // (from the hand that let go; or, out of nothing, grown where it lies)
                Place(0f);
                return;
            }
            if (_h.Free)
            {
                if (!_h.HasRest) { _h.RestAt = transform.TransformPoint(new Vector3(0f, 1f, 0.4f)); _h.RestTurn = transform.rotation; Story.Log("prop: " + _h.Item + " has no place it lay (its piece not here): it starts before the holder"); } // (never the world's zero: the hands reached 400 m for a letter hidden in its world, 2026-11-07)
                _look.transform.SetPositionAndRotation(_h.RestAt, _h.RestTurn);
                Humanoid body = GetComponent<Humanoid>();
                _anim = body != null ? BodyAnimator.For(body) : null;
                // from whatever side he came, the clip's frame of the thing is square to his body (the hands' points hold
                // on any side); its look keeps how it really lay, so it does not spin round in the hands
                Vector3 ahead = transform.forward; ahead.y = 0f;
                Quaternion square = ahead.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(ahead) : Quaternion.identity;
                _asLay = Quaternion.Inverse(square) * _h.RestTurn;
                if (_anim != null) _anim.PropRest(true, _h.RestAt, square);
            }
            else if (arm)
            {
                _wrist = BodyClips.SkeletonBone(GetComponent<Character>(), null, "RightHand"); // (the line from the elbow to the wrist; the look is not parented: placed in the world each frame)
                Vector3 at; Quaternion turn;
                ArmPose(out at, out turn);
                _look.transform.SetPositionAndRotation(at, turn);
            }
            else
            {
                _look.transform.SetParent(_joint, false);
                bool copy = _look.transform.childCount > 0 && _look.transform.GetChild(0).name == "look"; // (a copy, not the game's grip: a little into the palm)
                _inHand = copy ? new Vector3(0f, 0.02f, 0.03f) : Vector3.zero;
                _look.transform.localPosition = _inHand;
                _look.transform.localRotation = Quaternion.identity;
            }
            Place(0f);
            HideOthers();
        }

        // where a thing on the forearm is now, in the world: on the elbow-to-wrist line, ArmAlong of the way, ArmDrop below, ArmOut away from the body
        // (the thing's own numbers where its file says them, else the statics: read live each frame, so both are tuned on the stage)
        private void ArmPose(out Vector3 at, out Quaternion turn)
        {
            StoryProps.Nums n = StoryProps.NumsOf(_own);
            Vector3 elbow = _joint.position;
            Vector3 dir = _wrist != null ? _wrist.position - elbow : transform.forward * 0.25f;
            float len = dir.magnitude;
            if (len < 1e-3f) { dir = transform.forward; len = 0.25f; } else dir /= len;
            Vector3 outward = Vector3.ProjectOnPlane(elbow - transform.position, dir); outward.y = 0f;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : transform.right;
            at = elbow + dir * (len * n.ArmAlong) + Vector3.down * n.ArmDrop + outward * n.ArmOut;
            // (it hangs plumb: turned only round the upright, the way the forearm points over the ground — turned with the forearm itself it tilted with every
            //  bend of the arm, and fell from that tilt; the user 2026-11-11: «кошик досі криво на модельці Гейд і від того падає дивно»)
            Vector3 flat = dir; flat.y = 0f; if (flat.sqrMagnitude < 1e-6f) flat = transform.forward;
            turn = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(n.ArmRx, n.ArmRy, n.ArmRz);
        }

        // its meshes' corners in the look's own frame (a mesh's own axes may be turned: its thin side is not always its z)
        private Bounds Measure()
        {
            Bounds b = new Bounds(); bool any = false;
            List<Vector3> all = new List<Vector3>(); bool own = true; // (its own points, for its boxes: a mesh that cannot be read gives only corners — one box then)
            foreach (MeshFilter f in _look.GetComponentsInChildren<MeshFilter>()) // heavy: once per look made
            {
                if (f.sharedMesh == null) continue;
                Bounds m = f.sharedMesh.bounds;
                Vector3[] points = f.sharedMesh.isReadable ? f.sharedMesh.vertices : new Vector3[8]; // (its own points: the corners of its box overstate a turned mesh, a shield by half)
                if (!f.sharedMesh.isReadable) for (int i = 0; i < 8; i++) points[i] = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                foreach (Vector3 c in points)
                {
                    Vector3 p = _look.transform.InverseTransformPoint(f.transform.TransformPoint(c));
                    if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
                    all.Add(p);
                }
                if (!f.sharedMesh.isReadable) own = false;
            }
            _parts.Clear();
            if (any && own) Split(all, 0, _parts); else if (any) _parts.Add(b);
            return b;
        }

        private void LateUpdate()
        {
            if (_look == null) return;
            _g = Mathf.MoveTowards(_g, _leaving ? 0f : 1f, Time.deltaTime / Mathf.Max(0.05f, _leaving || _for <= 0f ? StoryProps.Glide : _for));
            Place(_g * _g * (3f - 2f * _g));
            HideOthers();
        }

        // from where it lay to where it is meant to be (nothing lay anywhere: it grows there)
        private void Place(float k)
        {
            Transform t = _look.transform;
            if (_h.Hand == "-" && _leaving)
            {
                // a scene's thing put back from where it lies (or from the air): to where the piece it came from lies — the piece returns as it arrives; none: it goes small where it is
                if (_h.HasHome) t.SetPositionAndRotation(Vector3.Lerp(_h.HomeAt, _fromAt, k), Quaternion.Slerp(_h.HomeTurn, _fromTurn, k));
                else t.localScale = Vector3.one * Mathf.Max(0.01f, k);
                return;
            }
            if (_h.Hand == "-" && _from && _h.Thrown)
            {
                // thrown: evenly over the ground, and over the straight line an arc as high as ThrowArc of the way (a thrown thing's curve: its top half-way),
                // tumbling whole turns round its own side axis, so it comes down as it will lie. Nothing is in its way: a first sketch
                float g = _g; Vector3 at = Vector3.Lerp(_fromAt, _h.RestAt, g);
                at.y += 4f * g * (1f - g) * Utils.DistanceXZ(_fromAt, _h.RestAt) * StoryProps.ThrowArc;
                t.SetPositionAndRotation(at, Quaternion.Slerp(_fromTurn, _h.RestTurn, g) * Quaternion.AngleAxis(360f * Mathf.Round(StoryProps.ThrowTurns) * g, Vector3.right));
                return;
            }
            if (_h.Hand == "-" && _from && _h.Fell)
            {
                // let fall: straight down faster and faster (the way down by the square of the time), tipping over and righting itself as it lands
                float g = _g; Vector3 at = Vector3.Lerp(_fromAt, _h.RestAt, g); at.y = Mathf.Lerp(_fromAt.y, _h.RestAt.y, g * g);
                t.SetPositionAndRotation(at, Quaternion.Slerp(_fromTurn, _h.RestTurn, g) * Quaternion.AngleAxis(Mathf.Sin(g * Mathf.PI) * StoryProps.NumsOf(_own).FallTilt, Vector3.right));
                return;
            }
            if (_h.Hand == "-") { if (_from) t.SetPositionAndRotation(Vector3.Lerp(_fromAt, _h.RestAt, k), Quaternion.Slerp(_fromTurn, _h.RestTurn, k)); else t.localScale = Vector3.one * Mathf.Max(0.01f, k); return; } // (lying: from the hand that let go to the ground, then still; out of nothing: grown there)
            if (_h.Hand == "a")
            {
                Vector3 at; Quaternion turn;
                ArmPose(out at, out turn); // (from the bones' places this frame; the numbers read live: tuned on the stage)
                if (_h.HasRest) t.SetPositionAndRotation(Vector3.Lerp(_h.RestAt, at, k), Quaternion.Slerp(_h.RestTurn, turn, k));
                else { t.SetPositionAndRotation(at, turn); t.localScale = Vector3.one * Mathf.Max(0.01f, k); }
                return;
            }
            if (_h.Free)
            {
                Vector3 at = _h.RestAt; Quaternion turn = _h.RestTurn;
                if (_anim != null) { _anim.PropAt(out at, out turn); turn = turn * (_h.Square ? Quaternion.Slerp(_asLay, Quaternion.identity, _anim.PropHeld()) : _asLay); }
                t.SetPositionAndRotation(Vector3.Lerp(_h.RestAt, at, k), Quaternion.Slerp(_h.RestTurn, turn, k));
            }
            else if (_h.HasRest) t.SetPositionAndRotation(Vector3.Lerp(_h.RestAt, _joint.TransformPoint(_inHand), k), Quaternion.Slerp(_h.RestTurn, _joint.rotation, k));
            else t.localScale = Vector3.one * Mathf.Max(0.01f, k);
        }

        private void HideOthers()
        {
            if (_h.Hand == "-" || _h.Hand == "a") return; // (on the ground, or on the forearm: the hand keeps what it holds — and the forearm's children are the hand itself)
            VisEquipment ve = _h.Free ? GetComponent<VisEquipment>() : null;
            if (ve != null) { Empty(ve.m_leftHand); Empty(ve.m_rightHand); } // (a clip takes it in both hands)
            else Empty(_joint);
        }

        private void Empty(Transform joint)
        {
            if (joint == null) return;
            for (int i = 0; i < joint.childCount; i++)
            {
                GameObject g = joint.GetChild(i).gameObject;
                if (g == _look || !g.activeSelf) continue;
                g.SetActive(false);
                _hid.Add(g);
            }
        }

        internal void Clear()
        {
            if (_look != null) Destroy(_look);
            _look = null;
            foreach (GameObject g in _hid) if (g != null) g.SetActive(true);
            _hid.Clear();
            _joint = null;
            if (_anim != null) _anim.PropRest(false, Vector3.zero, Quaternion.identity);
            _anim = null;
            _h = null; _g = 0f; _leaving = false; _code = ""; _from = false; _wrist = null; _own = null; _for = 0f;
        }

        private void OnDestroy() { Clear(); _bodies.Remove(_body); }

        // the bodies that have a view, each with its own (a person holding nothing and with none is passed over: StoryProps.Look; a holder's thing
        // asked as a word of place is found here, with no search of its parts: StoryProps.ThingAt)
        private static readonly Dictionary<int, StoryPropView> _bodies = new Dictionary<int, StoryPropView>();
        private int _body;
        private void Awake() { _body = gameObject.GetInstanceID(); _bodies[_body] = this; }
        internal static bool On(GameObject body) { return body != null && _bodies.ContainsKey(body.GetInstanceID()); }
        internal static StoryPropView Of(GameObject body) { StoryPropView v; return body != null && _bodies.TryGetValue(body.GetInstanceID(), out v) ? v : null; }
    }
}
#endif
