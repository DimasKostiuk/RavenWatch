#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person's round menu (npc-plan.md): Q while pointing at an NPC (E: its talk). Drawn as the ravens' circles are
    // (OrdersMenu: its frames and icons, the book's tip plate under it — the user's mockup 2026-11-05): a dark disc in the middle with its name, what
    // it does now and its state (DrawMiddle), round buttons in a ring round it.
    // Its buttons come from the parts of a person (Button below): the core's (Накази: За мною, Стій, Сядь, Туди in a ring of their own; Говорити), the
    // profile's (Вдача), the resident's (Оселя), the worker's (Робота), the gear's (Спорядження).
    // The main ring keeps every button in its own place for every person (the user, 2026-11-04: held Q and a flick
    // without looking): one that is not for this person now leaves its place empty, the others do not move. What is
    // set once (a job's chest, post, house…; the bed, moving out) lives in the rings under Робота and Оселя.
    // An order going on lights its button and «Накази»; that button again, or the middle of the main ring, lets it go («Відпусти»). A button with a
    // ring of its own turns the ring into its choices, the middle into «Назад» (rings in rings: «Назад» one step). A
    // button that undoes much (Виселити) asks a second press. Its way in a fight is not here: each person has its own
    // (its template, the story), never picked by a player. A button about a place: the menu closes, you point at the
    // place and press Q again (within PickSeconds; a right click drops it, PickCancel). One that cannot be pressed now is grey and says why.
    // Held Q: point and let go to pick; a short tap leaves it open for a click; right click, Esc or a click outside closes.
    // Orders go through the NPC (to its owner's game when it is not ours).
    // Its look is the user's mockup (2026-11-04, Downloads/Меню персонажа - центр.html): its signs and frames made by
    // tools/npc_icons.html, its words in Alegreya (BookArt.Words), its numbers and colours here.
    internal sealed class NpcMenu : MonoBehaviour
    {
        private static NpcMenu _inst;
        private const float HoldSeconds = 0.3f, PickSeconds = 30f; // (a place to show: half a minute — fifteen seconds was too short to walk to a table, 2026-11-01)
        private const float DiscR = 78f, RingR = 140f, Btn = 68f, PlateOut = 66f; // reference px (×ScaleNow); the disc 156 and the plate 412 across (the user's mockup, 2026-11-04: the state fits, the lower buttons' words stay on the plate)

        internal static bool IsOpen { get { return _inst != null && _inst._open; } }
        private static int _closedFrame = -10;
        // the game kept off while it is open, the frame it closes, and while the click that closed it is still held (else
        // the game takes that held button for a blow — as the ravens' circles do, OrdersMenu.Shields)
        private static bool _clickHeld;
        internal static bool Shields
        {
            get
            {
                if (IsOpen || Time.frameCount <= _closedFrame + 1) return true;
                if (_clickHeld && !MouseHeld()) _clickHeld = false;
                return _clickHeld;
            }
        }

        private static bool MouseHeld()
        {
            if (RavenKeys.Held(KeyCode.Mouse0) || RavenKeys.Held(KeyCode.Mouse1)) return true;
            try { return Input.GetMouseButton(0) || Input.GetMouseButton(1); } catch { return false; }
        }

        // ---------------------------------------------------------------- a deed done where the person is run
        // What a button changes in a person (its home, bed, job, chest, post…) is done on the game that runs it: its ZDO, its mind and its worker are
        // that game's. Pressed on another game, it goes there (NpcActor.Act «menu»), is done in the name of the one who pressed it (By), and its
        // words come back to that one's screen (the user 2026-11-08: «його зараз веде інша гра» — a second player could give no bed at all). On
        // the game that runs it: at once. A new deed: a line in Deeds (its module's Deeds(), wired in NpcModules) and OnOwner from its button.
        // Its words are in the language of the game that ran it.
        internal delegate string Deed(NpcActor a, Vector3 at, string arg);
        internal static readonly Dictionary<string, Deed> Deeds = new Dictionary<string, Deed>();
        private const string RpcSaid = "RavenWatch_NpcMenuSaid";
        private static Player _by;
        internal static Player By { get { return _by != null ? _by : Player.m_localPlayer; } } // (the one a deed is done for: who pressed it, on whatever game it runs)

        // a button's deed: here when this game runs it (its words back), else sent there (null: its words come later)
        internal static string OnOwner(NpcActor a, string deed, Vector3 at, string arg)
        {
            Player me = Player.m_localPlayer;
            Deed d;
            if (a == null || me == null || !Deeds.TryGetValue(deed, out d)) return null;
            if (a.Owned) return d(a, at, arg ?? "");
            Rpcs();
            a.Act("menu " + deed + " " + me.GetPlayerID().ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Npcs.V(at).Replace(' ', ';') + (string.IsNullOrEmpty(arg) ? "" : " " + arg));
            return null;
        }

        // the act «menu <deed> <player> <x;y;z> [arg…]» where it is run: done for that player, its words sent back to the game it came from
        internal static void Wire() { NpcActor.Acts["menu"] = RunDeed; }
        private static void RunDeed(NpcActor a, string[] w)
        {
            Deed d; long pid;
            if (w.Length < 4 || !Deeds.TryGetValue(w[1], out d) || !long.TryParse(w[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out pid)) { Npcs.Log(a.Id + ": a menu deed it cannot read: " + string.Join(" ", w)); return; }
            if (!NpcActor.SentBy(pid)) return;
            Player by = null;
            foreach (Player p in Player.GetAllPlayers()) if (p != null && p.GetPlayerID() == pid) by = p;
            if (by == null) { Npcs.Log(a.Id + ": a menu deed «" + w[1] + "» for a player not near — not done"); return; }
            string[] v = w[3].Split(';');
            float x, y, z;
            Vector3 at = v.Length == 3 && float.TryParse(v[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) && float.TryParse(v[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y) && float.TryParse(v[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z) ? new Vector3(x, y, z) : by.transform.position;
            string said;
            _by = by;
            try { said = d(a, at, w.Length > 4 ? string.Join(" ", w, 4, w.Length - 4) : ""); }
            finally { _by = null; }
            long from = NpcActor.ActFrom;
            if (string.IsNullOrEmpty(said)) return;
            if (from == 0L || ZRoutedRpc.instance == null) Say(said);
            else { Rpcs(); ZRoutedRpc.instance.InvokeRoutedRPC(from, RpcSaid, said); }
        }

        private static ZRoutedRpc _rpcOn; // (registered with this one: once a session)
        private static void Rpcs()
        {
            if (ZRoutedRpc.instance == null || _rpcOn == ZRoutedRpc.instance) return;
            _rpcOn = ZRoutedRpc.instance;
            ZRoutedRpc.instance.Register<string>(RpcSaid, (sender, said) => Say(said));
        }

        // a button on the menu. Each part of a person gives its own (the core ones below, NpcResident.MenuButtons,
        // NpcWorker.MenuButtons): the menu only lays them out, draws them and calls them — a new part adds its buttons
        // there, the menu is not touched. A new button on the main ring moves every other one: put it in a ring under one
        // (Sub) unless it is pressed often.
        internal sealed class Button
        {
            public int Order;                              // its place round the ring (from the top, clockwise)
            public string Part = "";                       // the main ring's part it is (Parts): a template's «menu: home talk» shows only these
            public string Title, Icon;                     // its word, its picture (assets/menu)
            public Func<NpcActor, string> TitleNow;        // (a word that changes with it: Оселити тут / Оселя)
            public Func<NpcActor, string> IconNow;         // (a picture that changes with it)
            public Func<NpcActor, List<Button>> Sub;       // a ring of its own: pressed, the ring shows these, the middle «Назад» (null for this person now: Do instead)
            public Func<NpcActor, string> Desc;            // the line over the circle
            public Func<NpcActor, bool> Now;               // lit: it does this now
            public Func<NpcActor, string> Not;             // why it cannot be pressed now (null: it can)
            public string Confirm;                         // it undoes much: the first press only asks (these words), a second one within ConfirmSeconds does it
            public Func<NpcActor, bool> Shown;             // for this person now (null: always): on the main ring its place stays, empty; in a ring under one, left out
            public Func<NpcActor, Vector3?> Where;         // the thing it is about (its bed, its chest, its home): marked on the screen while the button is under the pointer
            public Func<NpcActor, Player, string> Do;      // pressed: what to tell you (null: nothing)
            public bool Stay;                              // pressed: the ring stays open (a switch among others: NpcTidy)
            public bool AnyTime;                           // can be pressed while it lies senseless too (its card, its cells); the rest wait till it is up
            public string Place;                           // about a place («на ліжко»): the menu closes, you point and press the key again
            public Func<NpcActor, Vector3, string> AtPlace; // ...then this, at the spot pointed at
        }

        // the main ring's parts by their words (a template's «menu: <parts>» names them: NpcDefs)
        internal static readonly string[] Parts = { "orders", "talk", "home", "work", "gear", "profile" };

        private static List<Button> _all;
        internal static List<Button> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<Button>();
                _all.AddRange(Core());
                _all.AddRange(NpcResident.MenuButtons());
                _all.AddRange(NpcWorker.MenuButtons());
                _all.AddRange(NpcGear.MenuButtons());
                _all.AddRange(NpcProfile.MenuButtons());
                _all.Sort((x, y) => x.Order.CompareTo(y.Order));
                return _all;
            }
        }

        // the core's own: moving it, its talk. The main ring's places, from the top clockwise: Вдача 5, Накази 10,
        // Робота 50, Оселя 60, Спорядження 70, Говорити 80.
        private static IEnumerable<Button> Core()
        {
            // the orders, all under one button (the user 2026-11-08): lit while one goes on, the middle of the main ring lets it go
            yield return new Button { Order = 10, Part = "orders", Title = Lang.K("Накази"), Icon = "n-follow", Now = Releasable, Sub = a => OrderRing(),
                Desc = a => Releasable(a) ? Lang.T("Виконує твій наказ. Центр кола — відпустити.") : Lang.T("За мною, стій, сядь, туди.") };
            // its talk, as E: the same way in (NpcActor.Interact: senseless, an echo… — the modules' own use first)
            yield return new Button { Order = 80, Part = "talk", Title = Lang.K("Говорити"), Icon = "n-talk", Desc = a => Lang.T("Розмова з ним — те саме, що E."),
                Do = (a, p) => { a.Interact(p, false, false); return null; } };
        }

        private static List<Button> _orderRing;
        private static List<Button> OrderRing() { if (_orderRing == null) { _orderRing = new List<Button>(OrderButtons()); _orderRing.Sort((x, y) => x.Order.CompareTo(y.Order)); } return _orderRing; }

        // the ring under «Накази», from the top clockwise
        private static IEnumerable<Button> OrderButtons()
        {
            // an order's button lit while it goes on; pressed again, it lets it go (as the middle does)
            yield return new Button { Order = 10, Title = Lang.K("За мною"), Icon = "n-follow", Now = a => Ordered(a, NpcMind.Task.Follow),
                Desc = a => Ordered(a, NpcMind.Task.Follow) ? Lang.T("Іде за тобою.") + " " + AgainFrees() : Lang.T("Іде за тобою, доки не відпустиш."),
                Do = (a, p) =>
                {
                    if (Ordered(a, NpcMind.Task.Follow)) return Free(a);
                    a.Order("follow " + NpcKit.Ref(p.GetZDOID()) + " 3"); return a.Name + ": " + Lang.T("іде за тобою");
                } };
            // (it waits where it was sent as «Стій» too: «go» that arrived waits as idle)
            yield return new Button { Order = 20, Title = Lang.K("Стій"), Icon = "n-stay", Now = a => Ordered(a, NpcMind.Task.Idle),
                Desc = a => Ordered(a, NpcMind.Task.Idle) ? Lang.T("Стоїть.") + " " + AgainFrees() : Lang.F("Стоїть, де стоїть: до {0} год гри або доки не відпустиш.", StayHours),
                Do = (a, p) => { if (Ordered(a, NpcMind.Task.Idle)) return Free(a); a.Order("stay"); return null; } };
            yield return new Button { Order = 40, Title = Lang.K("Туди"), Icon = "n-there", Place = Lang.K("на місце"), Now = a => Ordered(a, NpcMind.Task.Go), Desc = a => Lang.F("Іде туди, куди покажеш, і чекає там: до {0} год гри або доки не відпустиш.", StayHours),
                AtPlace = (a, at) => { a.Order("go " + Npcs.V(at)); return a.Name + ": " + Lang.T("іде туди"); } };
            yield return new Button { Order = 30, Title = Lang.K("Сядь"), Icon = "n-sit", Now = a => Ordered(a, NpcMind.Task.Sit),
                Desc = a => Ordered(a, NpcMind.Task.Sit) ? Lang.T("Сидить.") + " " + AgainFrees() : Lang.T("Сідає на найближчу лаву чи стілець, а як нема — на землю поруч."),
                Do = (a, p) => { if (Ordered(a, NpcMind.Task.Sit)) return Free(a); a.Order("sit " + Npcs.V(a.transform.position)); return null; } };
        }

        // what an order of this kind is going on (the Order layer's plan; «go» that arrived waits as idle) — on any game: NpcMind.OrderOf
        private static bool Ordered(NpcActor a, NpcMind.Task kind)
        {
            NpcMind.Task? k = NpcMind.OrderOf(a);
            return k.HasValue && k.Value == kind;
        }

        // an order of the menu's going on: the middle lets it go
        private static bool Releasable(NpcActor a)
        {
            NpcMind.Task? k = NpcMind.OrderOf(a);
            return k.HasValue && (k.Value == NpcMind.Task.Follow || k.Value == NpcMind.Task.Idle || k.Value == NpcMind.Task.Go || k.Value == NpcMind.Task.Sit);
        }

        // how long a wait lasts by itself (NpcMind.StayTerm), as the buttons say it
        private static int StayHours { get { return Mathf.RoundToInt(NpcMind.StayHours); } }

        private static string Free(NpcActor a) { a.Order("free"); return a.Name + ": " + Lang.T("повертається до своїх справ"); }
        private static string AgainFrees() { return Lang.T("Натисни ще раз — відпустиш."); }
        private static string Freed(NpcActor a) { return Lang.T("Наказ знято: повертається до своїх справ") + (NpcResident.Is(a) ? Lang.T(" і на свою базу.") : "."); }

        // a place on the ring as it was when it was filled (Empty: the main ring's place of a button not for this person)
        private sealed class Item { internal Button B; internal List<Button> Ring; internal string Title, Desc, Icon; internal bool Now, Off, Empty, Asks; internal Vector3? Where; internal Fitted Label; }

        private bool _open;
        private NpcActor _npc;
        private KeyCode _key;
        private float _held;
        private int _hover = -1;                 // a button, -2 the middle, -1 nothing
        private readonly List<Item> _items = new List<Item>();
        private readonly List<Button> _subs = new List<Button>(); // the rings opened, one in another (none: the main ring)
        private Button Top { get { return _subs.Count > 0 ? _subs[_subs.Count - 1] : null; } }
        private bool _release;                    // the middle lets its order go (the main ring, an order of the menu going on)

        // a button that asks a second press (Confirm): which, for whom, until when
        private const float ConfirmSeconds = 5f;
        private Button _asked;
        private NpcActor _askedOf;
        private float _askedUntil;

        // a place to point at after the menu (Туди, Ліжко, Ящик): for whom, which button, until when
        private static NpcActor _pickFor;
        private static Button _pickButton;
        private static float _pickUntil;
        private static bool _pickAny; // (a place asked for without a person: the heart's dining table)

        // a place to point at for something that is not a person (the heart's «Їдальня»): the same hint, the same key
        internal static void PickPlace(string title, string place, Func<Vector3, string> done)
        {
            if (_inst == null) { GameObject go = new GameObject("RavenWatch_NpcMenu"); DontDestroyOnLoad(go); _inst = go.AddComponent<NpcMenu>(); _inst.enabled = false; }
            _pickFor = null; _pickAny = true; _pickUntil = Time.time + PickSeconds;
            _pickButton = new Button { Title = title, Place = place, AtPlace = (a, at) => done(at) };
            Say(Lang.F("Наведи {0} і натисни {1}", Lang.T(place), KeyName()));
            _inst.enabled = true;
        }

        // ---------------------------------------------------------------- a thing shown after a window (a heart's board, a person's card): the ring and the arrow a button's Where draws
        // (WhereMark) and a mark on the map, for PointSeconds or till you stand by it. One at a time; follow: a person who moves. Local only, nothing sent
        private const float PointSeconds = 30f, PointNear = 3f;
        internal sealed class Point { public Vector3 At; public Transform On; public string What = ""; public Minimap.PinData Pin; }
        private static readonly List<Point> _points = new List<Point>();
        private static float _pointUntil;
        private static bool PointOn { get { return _points.Count > 0 && Time.time < _pointUntil; } }

        internal static void ShowPlace(Vector3 at, Transform follow, string what) { ShowPlaces(new List<Point> { new Point { At = at, On = follow, What = what ?? "" } }); }

        // all of a group at once (a board's «Без ліжка: Сігрід, Ейнар»): each goes as you reach it
        internal static void ShowPlaces(List<Point> points)
        {
            if (_inst == null) { GameObject go = new GameObject("RavenWatch_NpcMenu"); DontDestroyOnLoad(go); _inst = go.AddComponent<NpcMenu>(); _inst.enabled = false; }
            PointsOff();
            if (points == null || points.Count == 0) return;
            _points.AddRange(points);
            _pointUntil = Time.time + PointSeconds;
            if (Minimap.instance != null) foreach (Point p in _points) p.Pin = Minimap.instance.AddPin(p.At, Minimap.PinType.Icon3, p.What, false, false, 0L, default(Splatform.PlatformUserID));
            _inst.enabled = true;
        }

        private static void PointOff(Point p) { if (p.Pin != null && Minimap.instance != null) Minimap.instance.RemovePin(p.Pin); p.Pin = null; }
        private static void PointsOff() { foreach (Point p in _points) PointOff(p); _points.Clear(); }

        private void PointTick()
        {
            if (_points.Count == 0) return;
            Player me = Player.m_localPlayer;
            bool over = !PointOn || me == null;
            for (int i = _points.Count - 1; i >= 0; i--)
            {
                Point p = _points[i];
                if (p.On != null) { p.At = p.On.position; if (p.Pin != null) p.Pin.m_pos = p.At; }
                if (over || Utils.DistanceXZ(me.transform.position, p.At) <= PointNear) { PointOff(p); _points.RemoveAt(i); }
            }
            if (_points.Count == 0 && !_open && !Waiting) enabled = false;
        }

        private static void PointDraw()
        {
            if (Event.current.type != EventType.Repaint || !InWorld() || NpcTalk.ScenePlaying) return;
            Textures();
            float s = OrdersMenu.ScaleNow;
            foreach (Point p in _points) WhereMark(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f + 60f * s), p.At, p.What, s);
        }

        // ---------------------------------------------------------------- the key (RavenKeys: Q, the gifts' key)
        // true when it was ours: the menu open/closed, a place picked, or an NPC under the pointer
        internal static bool Key(KeyCode key)
        {
            if (IsOpen) { _inst.Close(); return true; }
            Player p = Player.m_localPlayer;
            if (p == null) return false;
            if (Waiting) { Pick(p); return true; }
            NpcActor a = Under(p);
            if (a == null || a.Mind == null) return false; // (Odin: no menu)
            if (!MayOrder(a)) return false; // (orders only for the hired ones — a stranger is talked to, E; an admin: all)
            if (NpcTalk.Busy(a, p)) return true; // (another player talks to it or has its menu open: one at a time, as a talk — the user 2026-11-08)
            for (int i = 0; i < KeyInstead.Count; i++) { bool? done = KeyInstead[i](a, p); if (done.HasValue) return done.Value; } // (a module's own Q on it: asleep — woken, no ring)
            Open(a, key);
            return true;
        }

        // a module's own Q on a person instead of its ring (null: not theirs; true: done) — as NpcActor.UseInstead is for E; its hover says what Q does then
        // (NpcActor.HoverInstead). NpcWake: one asleep is woken
        internal static readonly List<Func<NpcActor, Player, bool?>> KeyInstead = new List<Func<NpcActor, Player, bool?>>();

        // the person you look at: the game's own pointing first, else along your look — the menu does not hang on the game
        // counting it as «pointed at». Never farther than a talk (E): the game's own reach from your eyes (the user 2026-11-05:
        // Q opened the menu of someone metres away)
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        internal static NpcActor Under(Player p)
        {
            GameObject h = p != null ? p.GetHoverObject() : null;
            NpcActor a = h != null ? h.GetComponentInParent<NpcActor>() : null;
            if (a != null || p == null || GameCamera.instance == null) return a;
            Transform cam = GameCamera.instance.transform;
            int n = Physics.SphereCastNonAlloc(cam.position, 0.3f, cam.forward, Hits, 6f + Vector3.Distance(cam.position, p.GetEyePoint()), LayerMask.GetMask("character", "character_net", "character_noenv"), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                NpcActor x = Hits[i].collider != null ? Hits[i].collider.GetComponentInParent<NpcActor>() : null;
                Vector3 at = Hits[i].distance > 0f ? Hits[i].point : Hits[i].collider.ClosestPoint(p.GetEyePoint()); // (begun inside it: no point of its own)
                if (x == null || Hits[i].distance >= best || Vector3.Distance(p.GetEyePoint(), at) > p.m_maxInteractDistance) continue;
                best = Hits[i].distance; a = x;
            }
            return a;
        }

        // who may give it orders here: the hired ones, anyone; an admin, all (its hover names the key only then)
        internal static bool MayOrder(NpcActor a) { return a.Def != null && !a.Def.NoMenu && (a.Def.MenuParts != null || NpcTalk.Hired(a) || Plugin.IsLocalAdmin()); } // (no template here: it only stands, so nothing to order — and E has nothing either)

        private static void Open(NpcActor a, KeyCode key)
        {
            if (_inst == null)
            {
                GameObject go = new GameObject("RavenWatch_NpcMenu");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<NpcMenu>();
            }
            _inst._npc = a; _inst._key = key; _inst._held = 0f; _inst._hover = -1; _inst._subs.Clear(); _inst._asked = null;
            _inst._nextHold = 0f; // (held from its first frame: another player is kept out at once, not after the last menu's 2 s)
            _inst._open = true; _inst.enabled = true;
            _inst.Fill();
        }

        private void Close()
        {
            Player me = Player.m_localPlayer;
            if (_npc != null && me != null) _npc.Act("unhold " + NpcKit.Ref(me.GetZDOID())); // (free for another at once, as a talk closed)
            _open = false; _npc = null; _hover = -1; _subs.Clear(); _asked = null;
            _closedFrame = Time.frameCount;
            _clickHeld = MouseHeld();
            enabled = PointOn;
        }

        // ---------------------------------------------------------------- what is on it (as it is when opened)
        // what you have to do with it (the user 2026-11-05: small tasks — «поговорити з кимось для чогось» — seen where you meet it): the lines over its name,
        // its request first, then a module's own (Tasks: your promises, NpcPromises; a small line's word, NpcArcs). One person may give two at once: two lines,
        // more: the first and «і ще N». Read once as the ring is made, never each frame.
        internal static readonly List<Action<NpcActor, Player, List<string>>> Tasks = new List<Action<NpcActor, Player, List<string>>>();
        private const int TaskLines = 2;
        private readonly List<string> _tasks = new List<string>();
        private readonly Fitted[] _fitTasks = new Fitted[TaskLines];

        private void Fill()
        {
            _items.Clear();
            _tasks.Clear();
            Player me = Player.m_localPlayer;
            NpcRequests.Request ask = NpcRequests.Of(_npc); // (its request: read once as the ring is made, not every frame it is drawn)
            if (ask != null) _tasks.Add(me != null && NpcPromises.Has(_npc, me.GetPlayerID(), "request") ? Grammar.Fix(Lang.T("ти обіця{в|ла}: "), me) + NpcRequests.Words(ask.Item, ask.Count) : Lang.F("просить: {0}", NpcRequests.Words(ask.Item, ask.Count)));
            if (me != null) foreach (Action<NpcActor, Player, List<string>> t in Tasks) { try { t(_npc, me, _tasks); } catch (Exception e) { Npcs.Log("npc menu tasks: " + e.Message); } }
            int more = _tasks.Count > TaskLines ? _tasks.Count - 1 : 0; // (two at most: the first, then how many more)
            if (more > 0) _tasks.RemoveRange(1, _tasks.Count - 1);
            for (int i = 0; i < _tasks.Count; i++) _tasks[i] = "<b>!</b> " + _tasks[i];
            if (more > 0) _tasks.Add(Lang.F("і ще {0}", more));
            bool down = NpcVitals.IsDown(_npc);
            List<Button> ring = All;
            while (Top != null && (ring = Top.Sub(_npc)) == null) _subs.RemoveAt(_subs.Count - 1); // (a ring that is no more for it — moved out meanwhile: the one under it)
            if (Top == null) ring = All;
            bool main = Top == null;
            _release = main && !down && Releasable(_npc);
            if (_asked != null && (_askedOf != _npc || Time.time > _askedUntil)) _asked = null;
            foreach (Button b in ring)
            {
                if ((b.Shown != null && !b.Shown(_npc)) || (main && _npc.Def != null && _npc.Def.MenuParts != null && !_npc.Def.MenuParts.Contains(b.Part))) { if (main) _items.Add(new Item { B = b, Empty = true, Off = true }); continue; } // (its place kept: the others do not move; a part its template does not give — the same)
                string not = down && !b.AnyTime ? Lang.T("Без тями — спершу постав його на ноги (медовуха зцілення, E).") : b.Not != null ? b.Not(_npc) : null;
                List<Button> own = not == null && b.Sub != null ? b.Sub(_npc) : null;
                bool asks = not == null && b == _asked;
                string desc = not ?? (asks ? _npc.Form(Lang.T(b.Confirm)) : b.Desc != null ? b.Desc(_npc) : "");
                if (not == null && !asks && b.Place != null && own == null) desc += "\n" + Lang.F("Меню закриється: наведи {0} і натисни {1}.", Lang.T(b.Place), KeyName());
                if (own != null) desc += "\n" + Lang.T("Відкриває свій вибір.");
                string title = b.TitleNow != null ? b.TitleNow(_npc) : Lang.T(b.Title);
                _items.Add(new Item { B = b, Ring = own, Title = asks ? Lang.T("Точно?") : title, Desc = desc, Icon = b.IconNow != null ? b.IconNow(_npc) : b.Icon, Now = b.Now != null && b.Now(_npc), Off = not != null, Asks = asks, Where = b.Where != null ? b.Where(_npc) : null });
            }
        }

        private static string KeyName() { return RavenKeys.LabelOf(RavenKeys.GiftsId); }

        // ---------------------------------------------------------------- doing it
        private void Do(Item it)
        {
            NpcActor a = _npc;
            Player p = Player.m_localPlayer;
            if (it.Empty) return; // (a place kept for a button not for this person)
            if (a != null && !it.Off && it.Ring != null) { _subs.Add(it.B); _hover = -1; _key = KeyCode.None; Fill(); return; } // (its own ring: the menu stays, now for clicks)
            if (a != null && !it.Off && it.B.Confirm != null && !(it.Asks && Time.time <= _askedUntil)) { _asked = it.B; _askedOf = a; _askedUntil = Time.time + ConfirmSeconds; _key = KeyCode.None; Fill(); return; } // (it undoes much: asked first, the menu stays for the second press)
            if (a != null && p != null && !it.Off && it.B.Stay && it.B.Do != null) { Say(it.B.Do(a, p)); Fill(); return; } // (a switch: flipped, the ring shown again as it is now)
            Close();
            if (a == null || p == null) return;
            if (it.Off) { Say(it.Desc); return; }
            if (it.B.Place != null)
            {
                _pickFor = a; _pickButton = it.B; _pickUntil = Time.time + PickSeconds;
                Say(Lang.F("Наведи {0} і натисни {1}", Lang.T(it.B.Place), KeyName()));
                enabled = true; // (its hint by the crosshair while it waits for the place)
                return;
            }
            if (it.B.Do != null) Say(it.B.Do(a, p));
        }

        // the place pointed at after a place's button
        private static void Pick(Player p)
        {
            NpcActor a = _pickFor; Button b = _pickButton;
            bool any = _pickAny;
            PickOff();
            if ((a == null && !any) || b == null || b.AtPlace == null) return;
            Say(b.AtPlace(a, Npcs.Aim(p)));
        }

        private static bool Waiting { get { return (_pickFor != null || _pickAny) && Time.time < _pickUntil; } }

        private static void PickOff()
        {
            _pickFor = null; _pickButton = null; _pickAny = false;
            if (_inst != null && !_inst._open) _inst.enabled = PointOn; // (its hint gone)
        }

        // no window over the world: the game's own question for its hero (Player.TakeInput), and the rings that are also the game's. Ours answer through StoreGui.IsVisible (book, talk, sheet)
        private static bool InWorld()
        {
            return (Chat.instance == null || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible() && !StoreGui.IsVisible() && !InventoryGui.IsVisible() && !Menu.IsVisible()
                && (TextViewer.instance == null || !TextViewer.instance.IsVisible()) && !Minimap.IsOpen() && !Hud.InRadial() && !Hud.IsPieceSelectionVisible();
        }

        // while it waits for a place: time up, the person gone, you dead or a scene begun end it. A right click drops it, unless a window has that click or a hammer is in hand (the game's build menu)
        private static void PickCancel()
        {
            Player p = Player.m_localPlayer;
            if (!Waiting || p == null || p.IsDead() || NpcTalk.ScenePlaying) { PickOff(); return; }
            if (!Input.GetMouseButtonDown(1) || p.InPlaceMode() || !InWorld()) return;
            PickOff();
            _cancelHeld = true;
        }

        // That right click is never the game's block. The game reads its block in a fixed step, which comes before this Update in the frame of the press, so it is not enough to answer once
        // the click is seen: from the first step on, the button held while a place is awaited, and until it is let go after a cancel, is no block (OrdersMenuNoSwingPatch). The hero
        // keeps walking and the camera turning, so not the menu's whole shield. Asked each fixed step of the local player: two fields, then one key.
        private static bool _cancelHeld;
        internal static bool HoldsBlock
        {
            get
            {
                if (!_cancelHeld && !Waiting) return false;
                if (RavenKeys.Held(KeyCode.Mouse1)) return true;
                _cancelHeld = false;
                return false;
            }
        }

        private static void Say(string s) { if (MessageHud.instance != null && !string.IsNullOrEmpty(s)) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, s); }

        // ---------------------------------------------------------------- the key held, the mouse
        private void Update()
        {
            PointTick();
            if (!_open) { PickCancel(); return; }
            if (_npc == null || Player.m_localPlayer == null || Player.m_localPlayer.IsDead()) { Close(); return; }
            if (Time.time >= _nextHold) { _nextHold = Time.time + 2f; _npc.Act("hold " + NpcKit.Ref(Player.m_localPlayer.GetZDOID())); } // (it stands while you give it orders)
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (_key != KeyCode.None)
            {
                if (Input.GetKey(_key)) _held += Time.unscaledDeltaTime;
                else if (_held > 0f)
                {
                    bool longHold = _held >= HoldSeconds;
                    _held = 0f; _key = KeyCode.None; // (let go: from now on, clicks)
                    if (longHold) { if (_hover >= 0 && _hover < _items.Count && !_items[_hover].Empty) Do(_items[_hover]); else if (_hover == -2 && MiddleDoes) Middle(); else Close(); return; }
                }
            }
            if (Input.GetMouseButtonDown(1)) { if (Top != null) Back(); else Close(); return; }
        }

        private float _nextHold;

        private void Back() { if (_subs.Count > 0) _subs.RemoveAt(_subs.Count - 1); _hover = -1; Fill(); }

        // the middle: «Назад» in a ring under one, «Відпусти» on the main ring while an order goes on, else nothing
        private bool MiddleDoes { get { return Top != null || _release; } }
        private void Middle()
        {
            if (Top != null) { Back(); return; }
            if (!_release) return;
            NpcActor a = _npc;
            Close();
            if (a != null) Say(Free(a));
        }

        private void OnGUI()
        {
            if (!_open && PointOn) PointDraw();
            if (!_open && Waiting) { PickHint(); return; }
            if (!_open || _npc == null) return;
            Event e = Event.current;
            if (e.type == EventType.Layout) return;
            float s = OrdersMenu.ScaleNow;
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            int n = _items.Count;
            float ringR = Mathf.Max(RingR, n * 88f / (2f * Mathf.PI)); // (a crowded ring wider: its buttons and their words apart)
            Vector2 m = e.mousePosition - c;
            float r = m.magnitude / s;
            int hover = r <= DiscR ? -2 : -1;
            if (r > DiscR && r < ringR + Btn) { float ang = Mathf.Repeat(Mathf.Atan2(m.x, -m.y) * Mathf.Rad2Deg + 180f / n, 360f); hover = Mathf.Clamp((int)(ang / (360f / n)), 0, n - 1); }
            if (e.type == EventType.MouseMove || e.type == EventType.Repaint) _hover = hover;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (r > ringR + Btn) Close();
                else if (_hover >= 0) Do(_items[_hover]);
                else if (_hover == -2) Middle();
                e.Use();
                return;
            }
            if (e.type != EventType.Repaint) return;
            Textures();
            // the ring's plate and the middle disc
            Disc(c, (ringR + PlateOut) * s, new Color(0.07f, 0.05f, 0.035f, 0.55f), new Color(0.66f, 0.52f, 0.23f, 0.35f));
            bool back = Top != null, backHot = (back || _release) && _hover == -2;
            float ds = DiscR * 2.35f * s; GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(c.x - ds / 2f, c.y - ds / 2f + 4f * s, ds, ds), _soft); GUI.color = Color.white; // (its shadow on the plate)
            if (backHot) { float g = DiscR * 2.3f * s; GUI.color = new Color(0.94f, 0.78f, 0.43f, 0.35f); GUI.DrawTexture(new Rect(c.x - g / 2f, c.y - g / 2f, g, g), _soft); GUI.color = Color.white; }
            Disc(c, DiscR * s, new Color(0.07f, 0.05f, 0.035f, 0.95f), new Color(0.66f, 0.52f, 0.23f, backHot ? 0.95f : 0.6f));
            if (back)
            {
                Fitted top = Fit(ref _fitTop, Top.TitleNow != null ? Top.TitleNow(_npc) : Lang.T(Top.Title), _title, 16f, 11f, 140f, 1, true, s);
                Text(new Rect(c.x - DiscR * s, c.y - 26f * s, DiscR * 2f * s, 24f * s), top.Text, _title, Gold, top.Size);
                Text(new Rect(c.x - DiscR * 0.9f * s, c.y + 2f * s, DiscR * 1.8f * s, 24f * s), "← " + Lang.T("Назад"), _small, backHot ? Bright : Ink, 13f * s);
            }
            else DrawMiddle(c, s, backHot);
            float labelWide = n > 1 ? Mathf.Min(120f, 2f * ringR * Mathf.Sin(Mathf.PI / n) - 8f) : 120f; // (as wide as the way to the next button)
            for (int i = 0; i < n; i++)
            {
                Item it = _items[i];
                float a = i * 360f / n;
                Vector2 at = c + new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), -Mathf.Cos(a * Mathf.Deg2Rad)) * ringR * s;
                if (it.Empty) { float de = Btn * s; GUI.color = new Color(1f, 1f, 1f, 0.22f); GUI.DrawTexture(new Rect(at.x - de / 2f, at.y - de / 2f, de, de), OrdersMenu.Pic("n-frame-grey")); GUI.color = Color.white; continue; } // (its place kept, nothing on it)
                bool hot = i == _hover;
                float d = Btn * s;
                if (it.Now)
                {
                    float g = d * 1.5f;
                    GUI.color = new Color(1f, 0.76f, 0.23f, 0.35f);
                    GUI.DrawTexture(new Rect(at.x - g / 2f, at.y - g / 2f, g, g), _soft);
                }
                float sh = d * 1.2f;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(at.x - sh / 2f, at.y - sh / 2f + 2f * s, sh, sh), _soft); // (its shadow)
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(at.x - d / 2f, at.y - d / 2f, d, d), OrdersMenu.Pic(it.Off ? "n-frame-grey" : it.Now || it.Asks ? "n-frame-green" : hot ? "n-frame-hot" : "n-frame-dark"));
                float ic = d * 0.5f;
                GUI.color = it.Off ? OffIcon : hot ? Bright : IconInk;
                GUI.DrawTexture(new Rect(at.x - ic / 2f, at.y - ic / 2f, ic, ic), OrdersMenu.Pic(it.Icon));
                GUI.color = Color.white;
                Fitted label = Fit(ref it.Label, it.Title, _small, 12f, 9f, labelWide, 2, false, s); // (its word between its neighbours: two lines, smaller, never on theirs)
                Rect word = new Rect(at.x - labelWide * s / 2f, at.y + Btn * s * 0.5f + 1f * s, labelWide * s, 18f * s);
                Text(new Rect(word.x, word.y + 1f * s, word.width, word.height), label.Text, _small, new Color(0f, 0f, 0f, 0.9f), label.Size); // (its shadow: it stays read on any ground)
                Text(word, label.Text, _small, it.Off ? OffWord : hot ? Bright : Gold, label.Size);
                if (hot && it.Where.HasValue) WhereMark(at, it.Where.Value, it.Title, s);
            }
            if (_hover == -2 && _release)
            {
                float w = 380f * s;
                float h = Plate.Set(Lang.T("Відпусти"), Freed(_npc), w, s);
                Plate.Place(c.x - w / 2f, PlateTop(c, ringR, h, s));
            }
            else if (_hover >= 0 && _hover < n && !_items[_hover].Empty)
            {
                float w = 380f * s;
                float h = Plate.Set(_items[_hover].Title, _items[_hover].Desc, w, s);
                Plate.Place(c.x - w / 2f, PlateTop(c, ringR, h, s));
            }
        }

        // ---------------------------------------------------------------- the middle on the main ring (the user's mockup, 2026-11-04)
        // one column centred on the disc, top to bottom: its request, its name, what it does (≤2 lines) — or «← Відпусти»
        // while an order of the menu goes on (its order is told by its lit button) — then its state: an icon and a number
        // each, two by two (health, fullness, mood, strength = 100 − tiredness), one line when only health is known (hired,
        // living nowhere), «без тями» alone when it lies senseless. Read each frame it is drawn: a few numbers of its own.
        private struct Stat { public string Icon, Val; public Color Col; }
        private readonly List<Stat> _stats = new List<Stat>(4);
        private static readonly Color Warn = new Color(0.92f, 0.75f, 0.38f), Bad = new Color(0.95f, 0.45f, 0.35f);
        private const float StatCol = 38f, StatGap = 10f, StatRow = 15f, StatRowGap = 2f, StatIcon = 13f, StatIconGap = 4f;

        private void DrawMiddle(Vector2 c, float s, bool hot)
        {
            Character body = _npc.GetComponent<Character>();
            bool down = body != null && NpcVitals.IsDown(body);
            _stats.Clear();
            if (body != null && NpcTalk.Hired(_npc))
            {
                if (down) _stats.Add(new Stat { Icon = "n-s-health", Val = Lang.T("без тями"), Col = Bad });
                else
                {
                    float hp = body.GetHealthPercentage() * 100f;
                    _stats.Add(Of("n-s-health", hp, hp < 35f ? Bad : Ink));
                    if (NpcResident.Is(_npc))
                    {
                        float full = NpcHunger.Full(_npc), mood = NpcToil.Mood(_npc), strength = 100f - NpcToil.Tired(_npc);
                        _stats.Add(Of("n-s-food", full, full < 15f ? Bad : full < 35f ? Warn : Ink)); // (NpcHunger: red starving, amber hungry)
                        _stats.Add(Of("n-s-mood", mood, mood < 30f ? Bad : Ink));                     // (NpcToil)
                        _stats.Add(Of("n-s-energy", strength, strength < 30f ? Warn : Ink));
                    }
                }
            }
            string doing = _release || down ? null : _npc.StatusText; // (senseless: its state says it)
            const float wide = 112f, line = 136f;
            int asks = _tasks.Count;
            Fitted name = Fit(ref _fitName, _npc.Name, _title, 16f, 12f, line, 1, true, s);
            Fitted act = !string.IsNullOrEmpty(doing) ? Fit(ref _fitDoing, doing, _small, 11f, 9f, wide, 2, true, s) : null;
            int lines = act != null ? act.Lines : 0;
            // the column's height (reference px), then each part from its top
            float h = asks * 13f + (asks > 0 ? 3f : 0f) + 18f + (_release ? 1f + 16f : lines > 0 ? 2f + lines * 13f : 0f)
                + (_stats.Count == 0 ? 0f : 6f + (_stats.Count > 1 ? StatRow * 2f + StatRowGap : StatRow));
            float y = -h / 2f;
            for (int i = 0; i < asks; i++) { Fitted t = Fit(ref _fitTasks[i], _tasks[i], _small, 10.5f, 9f, line, 1, true, s); Text(Row(c, y, line, 13f, s), t.Text, _small, AskInk, t.Size); y += 13f; } // (what you have to do with it: gold «!» lines over its name)
            if (asks > 0) y += 3f;
            Text(Row(c, y, line, 18f, s), name.Text, _title, Gold, name.Size); y += 18f;
            if (_release) { y += 1f; Text(Row(c, y, wide, 16f, s), "← " + Lang.T("Відпусти"), _small, hot ? Bright : Gold, 13f * s); y += 16f; }
            else if (lines > 0) { y += 2f; Text(Row(c, y, wide, lines * 13f, s), act.Text, _small, Ink, act.Size); y += lines * 13f; }
            if (_stats.Count == 0) return;
            y += 6f;
            if (_stats.Count == 1) // one line, centred: no holes
            {
                _num.fontSize = Mathf.Max(8, Mathf.RoundToInt(11.5f * s));
                float tw = _num.CalcSize(new GUIContent(_stats[0].Val)).x / s, all = StatIcon + StatIconGap + tw;
                DrawStat(_stats[0], c.x - all * s / 2f, c.y + y * s, tw + 4f, s);
                return;
            }
            for (int i = 0; i < _stats.Count; i++)
            {
                float left = (i % 2 == 0 ? -(StatCol + StatGap / 2f) : StatGap / 2f);
                DrawStat(_stats[i], c.x + left * s, c.y + (y + (i / 2) * (StatRow + StatRowGap)) * s, StatCol - StatIcon - StatIconGap, s);
            }
        }

        // ---------------------------------------------------------------- a text kept inside its place, in any language
        // its size from its own down to min (screen px) till it fits this wide in at most this many lines — a word is never
        // broken, so a word longer than the line shrinks it; still too long at min: cut at a word with «…» (cut), else left
        // as it is. Worked out once per text, width and scale (kept in its own Fitted): not each frame it is drawn.
        private sealed class Fitted { public string Source, Text; public float Scale, Wide, Size; public int Lines; }
        private Fitted _fitName, _fitDoing, _fitTop;
        private static readonly GUIContent Probe = new GUIContent();

        private static Fitted Fit(ref Fitted kept, string text, GUIStyle st, float size, float min, float wide, int lines, bool cut, float s)
        {
            if (kept != null && kept.Source == text && kept.Scale == s && kept.Wide == wide) return kept;
            Fitted f = kept ?? (kept = new Fitted());
            f.Source = text; f.Scale = s; f.Wide = wide; f.Text = text ?? "";
            float w = wide * s;
            for (f.Size = size * s; ; f.Size -= 1f)
            {
                if (f.Size < min * s) { f.Size = min * s; break; }
                if (Fits(f.Text, st, f.Size, w, lines)) break;
            }
            if (cut && !Fits(f.Text, st, f.Size, w, lines))
            {
                string[] words = f.Text.Split(' ');
                for (int n = words.Length - 1; n >= 1; n--) { string t = string.Join(" ", words, 0, n) + "…"; if (Fits(t, st, f.Size, w, lines)) { f.Text = t; break; } }
            }
            st.fontSize = Mathf.RoundToInt(f.Size);
            f.Lines = !st.wordWrap ? 1 : Mathf.Clamp(Mathf.RoundToInt(Height(st, f.Text, w) / Height(st, "Ag", 10000f)), 1, lines);
            return f;
        }

        private static bool Fits(string text, GUIStyle st, float size, float wide, int lines)
        {
            st.fontSize = Mathf.RoundToInt(size);
            bool wrap = st.wordWrap;
            st.wordWrap = false;
            float longest = 0f;
            foreach (string word in text.Split(' ')) { Probe.text = word; longest = Mathf.Max(longest, st.CalcSize(Probe).x); }
            st.wordWrap = wrap;
            if (longest > wide) return false;
            if (!wrap) { Probe.text = text; return st.CalcSize(Probe).x <= wide; }
            return Height(st, text, wide) <= Height(st, "Ag", 10000f) * lines + 1f;
        }

        private static float Height(GUIStyle st, string text, float wide) { Probe.text = text; return st.CalcHeight(Probe, wide); }

        private static Stat Of(string icon, float v, Color col) { return new Stat { Icon = icon, Val = Mathf.RoundToInt(v).ToString(), Col = col }; }

        // a line of the middle's column: centred, this wide and high (reference px), its top y from the disc's centre
        private static Rect Row(Vector2 c, float top, float wide, float high, float s) { return new Rect(c.x - wide * s / 2f, c.y + top * s, wide * s, high * s); }

        // a state's icon (gold, or its number's colour when it is low) and its number after it, in a row StatRow high
        private void DrawStat(Stat st, float x, float top, float numWide, float s)
        {
            float ic = StatIcon * s, mid = top + StatRow * s / 2f;
            GUI.color = st.Col == Ink ? IconInk : st.Col;
            GUI.DrawTexture(new Rect(x, mid - ic / 2f, ic, ic), OrdersMenu.Pic(st.Icon));
            GUI.color = Color.white;
            Text(new Rect(x + ic + StatIconGap * s, top, numWide * s, StatRow * s), st.Val, _num, st.Col, 11.5f * s);
        }

        // while it waits for a place: by the crosshair, what the key does there and how long it still waits
        private static readonly RavenBook.ScreenPlate PickPlate = new RavenBook.ScreenPlate("RavenWatch_NpcPickHint", 30010);
        private void PickHint()
        {
            if (Event.current.type != EventType.Repaint || _pickButton == null || !InWorld()) return; // (a window over it: the plate hides itself)
            if (Player.m_localPlayer == null || Player.m_localPlayer.InPlaceMode()) return; // (a hammer in hand: neither the key nor the right click is ours, RavenKeys. It still waits: hammer away, it is back)
            float left = _pickUntil - Time.time;
            float s = OrdersMenu.ScaleNow, w = 300f * s;
            string who = _pickFor != null ? _pickFor.Name : "";
            string title = "[" + KeyName() + "] " + Lang.T(_pickButton.Title) + (who.Length > 0 ? " — " + who : "");
            string desc = Lang.F("Наведи {0} і натисни {1}. Ще {2} с.", Lang.T(_pickButton.Place), KeyName(), Mathf.CeilToInt(left)) + "\n" + Lang.T("Правий клік — скасувати.");
            float h = PickPlate.Set(title, desc, w, s);
            PickPlate.Place(Screen.width * 0.5f - w / 2f, Mathf.Min(Screen.height * 0.5f + 110f * s, Screen.height - h - 8f * s)); // (under the crosshair, clear of the game's own hover text that grows to its right)
        }

        // the thing a button is about, on the screen: a gold ring on it, a thin line to it from the button, how far; off the
        // screen (or behind you): an arrow on the screen's edge pointing its way
        private static void WhereMark(Vector3 from2d, Vector3 world, string what, float s)
        {
            Camera cam = Camera.main;
            Player p = Player.m_localPlayer;
            if (cam == null || p == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world + Vector3.up * 0.6f);
            bool behind = sp.z < 0f;
            Vector2 at = new Vector2(sp.x, Screen.height - sp.y);
            if (behind) at = new Vector2(Screen.width - at.x, Screen.height - at.y); // (behind you: the other way round)
            float m = 40f * s;
            bool off = behind || at.x < m || at.y < m || at.x > Screen.width - m || at.y > Screen.height - m;
            Vector2 mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (off)
            {
                Vector2 dir = (at - mid); if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                dir.Normalize();
                float k = Mathf.Min((Screen.width * 0.5f - m) / Mathf.Max(0.001f, Mathf.Abs(dir.x)), (Screen.height * 0.5f - m) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                at = mid + dir * k;
            }
            Line(new Vector2(from2d.x, from2d.y), at, new Color(0.95f, 0.8f, 0.45f, 0.75f), 2f * s);
            float r = (off ? 16f : 22f) * s;
            GUI.color = new Color(1f, 0.82f, 0.4f, 0.45f); GUI.DrawTexture(new Rect(at.x - r * 1.8f, at.y - r * 1.8f, r * 3.6f, r * 3.6f), _soft);
            GUI.color = new Color(1f, 0.85f, 0.5f, 0.95f); GUI.DrawTexture(new Rect(at.x - r, at.y - r, r * 2f, r * 2f), _ring);
            GUI.color = Color.white;
            int metres = Mathf.RoundToInt(Vector3.Distance(p.transform.position, world));
            Text(new Rect(at.x - 90f * s, at.y + r + 2f * s, 180f * s, 20f * s), what + " · " + metres + " м", _small, Bright, 13f * s);
        }

        private static void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            Matrix4x4 keep = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            GUI.color = c;
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), Texture2D.whiteTexture);
            GUI.matrix = keep;
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- drawing
        private static readonly RavenBook.ScreenPlate Plate = new RavenBook.ScreenPlate("RavenWatch_NpcMenuInfo", 30010);

        // the tip plate's top: just under the ring's dark plate (the user's mockup, 2026-11-05), kept on the screen — no room under it: over it
        private static float PlateTop(Vector2 c, float ringR, float h, float s)
        {
            float below = c.y + (ringR + PlateOut) * s + 14f * s;
            return below + h <= Screen.height - 4f ? below : Mathf.Max(4f, c.y - (ringR + PlateOut) * s - h - 14f * s);
        }
        private static readonly Color Gold = new Color(0.9f, 0.78f, 0.49f), Bright = new Color(1f, 0.85f, 0.54f), Ink = new Color(0.86f, 0.8f, 0.68f), AskInk = new Color(1f, 0.82f, 0.36f), IconInk = new Color(0.9f, 0.77f, 0.49f), OffIcon = new Color(0.52f, 0.53f, 0.55f), OffWord = new Color(0.56f, 0.55f, 0.525f);
        private static GUIStyle _title, _small, _num;
        private static Texture2D _disc, _soft, _ring;

        private static void Textures()
        {
            if (_num == null) _num = new GUIStyle(GUI.skin.label) { font = BookArt.Words, alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { font = BookArt.Words, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true, clipping = TextClipping.Overflow };
                _small = new GUIStyle(GUI.skin.label) { font = BookArt.Words, alignment = TextAnchor.UpperCenter, wordWrap = true, richText = true, clipping = TextClipping.Overflow };
            }
            if (_disc == null) _disc = Round(256, false);
            if (_soft == null) _soft = Round(128, true);
            if (_ring == null) _ring = Ring(128, 0.14f);
        }

        private static void Disc(Vector2 c, float r, Color fill, Color rim)
        {
            float rl = r + 1.5f;
            GUI.color = rim; GUI.DrawTexture(new Rect(c.x - rl, c.y - rl, rl * 2f, rl * 2f), _disc);
            GUI.color = fill; GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), _disc);
            GUI.color = Color.white;
        }

        private static void Text(Rect r, string text, GUIStyle st, Color col, float size)
        {
            st.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            st.normal.textColor = col;
            GUI.Label(r, text, st);
        }

        // a white ring (its thickness a share of the radius)
        private static Texture2D Ring(int size, float thick)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f, inner = half * (1f - thick);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(half - 1f - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // a round white picture: a hard-edged disc, or a soft glow fading out
        private static Texture2D Round(int size, bool soft)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = soft ? Mathf.Clamp01(1f - d / half) : Mathf.Clamp01(half - 1f - d + 0.5f);
                    if (soft) a *= a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }
    }
}
#endif
