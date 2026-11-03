#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person's round menu (npc-plan.md): Q while pointing at an NPC (E stays for quick orders later). Drawn as the
    // ravens' circles are (OrdersMenu: its frames and icons, the book's tip plate over it): a dark disc in the middle
    // with its name and what it does now, round buttons in a ring round it.
    // Its buttons come from the parts of a person (Button below): the core's (За мною, Стій, Туди, Сядь), the
    // resident's (Оселити/Виселити, Ліжко), the worker's (Робота, Ящик). A button follows what it does now (as the
    // ravens' circles): «За мною» while it follows you is «Відпусти». A button with a ring of its own (Робота) turns
    // the ring into its choices, the middle into «Назад». Its way in a fight is not here: each person has its own (its
    // template, the story), never picked by a player. A button about a place: the menu
    // closes, you point at the place and press Q again (within PickSeconds). One that cannot be pressed now is grey and
    // says why.
    // Held Q: point and let go to pick; a short tap leaves it open for a click; right click, Esc or a click outside closes.
    // Orders go through the NPC (to its owner's game when it is not ours).
    internal sealed class NpcMenu : MonoBehaviour
    {
        private static NpcMenu _inst;
        private const float HoldSeconds = 0.3f, PickSeconds = 30f; // (a place to show: half a minute — fifteen seconds was too short to walk to a table, 2026-11-01)
        private const float DiscR = 70f, RingR = 140f, Btn = 68f; // reference px (×ScaleNow)

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

        // a button on the menu. Each part of a person gives its own (the core ones below, NpcResident.MenuButtons,
        // NpcWorker.MenuButtons): the menu only lays them out, draws them and calls them — a new part adds its buttons
        // there, the menu is not touched.
        internal sealed class Button
        {
            public int Order;                              // its place round the ring (from the top, clockwise)
            public string Title, Icon;                     // its word, its picture (assets/menu)
            public Func<NpcActor, string> TitleNow;        // (a word that changes with it: Оселити / Виселити)
            public Func<NpcActor, string> IconNow;         // (a picture that changes with it)
            public Func<NpcActor, List<Button>> Sub;       // a ring of its own: pressed, the ring shows these, the middle «Назад»
            public Func<NpcActor, string> Desc;            // the line over the circle
            public Func<NpcActor, bool> Now;               // lit: it does this now
            public Func<NpcActor, string> Not;             // why it cannot be pressed now (null: it can)
            public Func<NpcActor, bool> Shown;             // on the ring at all (null: always) — e.g. an admin's own tool
            public Func<NpcActor, Vector3?> Where;         // the thing it is about (its bed, its chest, its home): marked on the screen while the button is under the pointer
            public Func<NpcActor, Player, string> Do;      // pressed: what to tell you (null: nothing)
            public bool Stay;                              // pressed: the ring stays open (a switch among others: NpcTidy)
            public bool AnyTime;                           // can be pressed while it lies senseless too (its card, its cells); the rest wait till it is up
            public string Place;                           // about a place («на ліжко»): the menu closes, you point and press the key again
            public Func<NpcActor, Vector3, string> AtPlace; // ...then this, at the spot pointed at
        }

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

        // the core's own: moving it and its way in a fight
        private static IEnumerable<Button> Core()
        {
            // «За мною» / while it follows: «Відпусти»
            yield return new Button { Order = 10, Title = "За мною", Icon = "n-follow", Now = a => Ordered(a, NpcMind.Task.Follow),
                TitleNow = a => Ordered(a, NpcMind.Task.Follow) ? Lang.T("Відпусти") : Lang.T("За мною"),
                IconNow = a => Ordered(a, NpcMind.Task.Follow) ? "n-release" : "n-follow",
                Desc = a => Ordered(a, NpcMind.Task.Follow) ? Freed(a) : Lang.T("Іде за тобою, доки не відпустиш."),
                Do = (a, p) =>
                {
                    if (Ordered(a, NpcMind.Task.Follow)) { a.Order("free"); return a.Name + ": " + Lang.T("повертається до своїх справ"); }
                    a.Order("follow " + NpcKit.Ref(p.GetZDOID()) + " 3"); return a.Name + ": " + Lang.T("іде за тобою");
                } };
            // «Стій» / while it stands by an order (or waits where it was sent): «Відпусти»
            yield return new Button { Order = 20, Title = "Стій", Icon = "n-stay", Now = a => Ordered(a, NpcMind.Task.Idle),
                TitleNow = a => Ordered(a, NpcMind.Task.Idle) ? Lang.T("Відпусти") : Lang.T("Стій"),
                IconNow = a => Ordered(a, NpcMind.Task.Idle) ? "n-release" : "n-stay",
                Desc = a => Ordered(a, NpcMind.Task.Idle) ? Freed(a) : Lang.T("Стоїть, де стоїть, доки не відпустиш."),
                Do = (a, p) => { a.Order(Ordered(a, NpcMind.Task.Idle) ? "free" : "stay"); return null; } };
            yield return new Button { Order = 30, Title = "Туди", Icon = "n-there", Place = "на місце", Now = a => Ordered(a, NpcMind.Task.Go), Desc = a => Lang.T("Іде туди, куди покажеш, і чекає там."),
                AtPlace = (a, at) => { a.Order("go " + Npcs.V(at)); return a.Name + ": " + Lang.T("іде туди"); } };
            // «Сядь» / while it sits by an order: «Встань»
            yield return new Button { Order = 40, Title = "Сядь", Icon = "n-sit", Now = a => Ordered(a, NpcMind.Task.Sit),
                TitleNow = a => Ordered(a, NpcMind.Task.Sit) ? Lang.T("Встань") : Lang.T("Сядь"),
                IconNow = a => Ordered(a, NpcMind.Task.Sit) ? "n-stand" : "n-sit",
                Desc = a => Ordered(a, NpcMind.Task.Sit) ? Freed(a) : Lang.T("Сідає на найближчу лаву чи стілець, а як нема — на землю поруч."),
                Do = (a, p) => { a.Order(Ordered(a, NpcMind.Task.Sit) ? "free" : "sit " + Npcs.V(a.transform.position)); return null; } };
        }

        // what an order of this kind is going on (the Order layer's plan; «go» that arrived waits as idle)
        private static bool Ordered(NpcActor a, NpcMind.Task kind)
        {
            NpcMind.Plan p = a.Mind != null ? a.Mind.Of(NpcMind.Layer.Order) : null;
            return p != null && p.Kind == kind;
        }

        private static string Freed(NpcActor a) { return Lang.T("Наказ знято: повертається до своїх справ") + (NpcResident.Is(a) ? Lang.T(" і на свою базу.") : "."); }

        private sealed class Item { internal Button B; internal string Title, Desc, Icon; internal bool Now, Off; internal Vector3? Where; }

        private bool _open;
        private NpcActor _npc;
        private KeyCode _key;
        private float _held;
        private int _hover = -1;                 // a button, -2 the middle, -1 nothing
        private readonly List<Item> _items = new List<Item>();
        private Button _sub;                      // the button whose ring is shown (null: the main ring)

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

        // ---------------------------------------------------------------- the key (RavenKeys: Q, the gifts' key)
        // true when it was ours: the menu open/closed, a place picked, or an NPC under the pointer
        internal static bool Key(KeyCode key)
        {
            if (IsOpen) { _inst.Close(); return true; }
            Player p = Player.m_localPlayer;
            if (p == null) return false;
            if ((_pickFor != null || _pickAny) && Time.time < _pickUntil) { Pick(p); return true; }
            _pickFor = null; _pickAny = false;
            NpcActor a = Under(p);
            if (a == null || a.Mind == null) return false; // (Odin: no menu)
            if (!MayOrder(a)) return false; // (orders only for the hired ones — a stranger is talked to, E; an admin: all)
            Open(a, key);
            return true;
        }

        // the person you look at: the game's own pointing first, else along your look (6 m) — the menu does not hang on
        // the game counting it as «pointed at»
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
                if (x != null && Hits[i].distance < best) { best = Hits[i].distance; a = x; }
            }
            return a;
        }

        // who may give it orders here: the hired ones, anyone; an admin, all (its hover names the key only then)
        internal static bool MayOrder(NpcActor a) { return NpcTalk.Hired(a) || Plugin.IsLocalAdmin(); }

        private static void Open(NpcActor a, KeyCode key)
        {
            if (_inst == null)
            {
                GameObject go = new GameObject("RavenWatch_NpcMenu");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<NpcMenu>();
            }
            _inst._npc = a; _inst._key = key; _inst._held = 0f; _inst._hover = -1; _inst._sub = null;
            _inst._open = true; _inst.enabled = true;
            _inst.Fill();
        }

        private void Close()
        {
            _open = false; _npc = null; _hover = -1; _sub = null;
            _closedFrame = Time.frameCount;
            _clickHeld = MouseHeld();
            enabled = false;
        }

        // ---------------------------------------------------------------- what is on it (as it is when opened)
        private string _ask; // (the line over its name: what it asks for)

        private void Fill()
        {
            _items.Clear();
            NpcRequests.Request ask = NpcRequests.Of(_npc); // (its request: read once as the ring is made, not every frame it is drawn)
            _ask = ask != null ? "<b>!</b> " + Lang.F("просить: {0}", NpcRequests.Words(ask.Item, ask.Count)) : null;
            bool down = NpcVitals.IsDown(_npc);
            foreach (Button b in _sub != null ? _sub.Sub(_npc) : All)
            {
                if (b.Shown != null && !b.Shown(_npc)) continue;
                string not = down && !b.AnyTime ? Lang.T("Без тями — спершу постав його на ноги (медовуха зцілення, E).") : b.Not != null ? b.Not(_npc) : null;
                string desc = not ?? (b.Desc != null ? b.Desc(_npc) : "");
                if (not == null && b.Place != null) desc += "\n" + Lang.F("Меню закриється: наведи {0} і натисни {1}.", Lang.T(b.Place), KeyName());
                if (not == null && b.Sub != null) desc += "\n" + Lang.T("Відкриває свій вибір.");
                _items.Add(new Item { B = b, Title = b.TitleNow != null ? b.TitleNow(_npc) : Lang.T(b.Title), Desc = desc, Icon = b.IconNow != null ? b.IconNow(_npc) : b.Icon, Now = b.Now != null && b.Now(_npc), Off = not != null, Where = b.Where != null ? b.Where(_npc) : null });
            }
        }

        private static string KeyName() { return RavenKeys.LabelOf(RavenKeys.GiftsId); }

        // ---------------------------------------------------------------- doing it
        private void Do(Item it)
        {
            NpcActor a = _npc;
            Player p = Player.m_localPlayer;
            if (a != null && !it.Off && it.B.Sub != null) { _sub = it.B; _hover = -1; _key = KeyCode.None; Fill(); return; } // (its own ring: the menu stays, now for clicks)
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
            _pickFor = null; _pickButton = null; _pickAny = false;
            if (_inst != null && !_inst._open) _inst.enabled = false; // (its hint gone)
            if ((a == null && !any) || b == null || b.AtPlace == null) return;
            Say(b.AtPlace(a, Npcs.Aim(p)));
        }

        private static void Say(string s) { if (MessageHud.instance != null && !string.IsNullOrEmpty(s)) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, s); }

        // ---------------------------------------------------------------- the key held, the mouse
        private void Update()
        {
            if (!_open) return;
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
                    if (longHold) { if (_hover >= 0 && _hover < _items.Count) Do(_items[_hover]); else if (_hover == -2 && _sub != null) Back(); else Close(); return; }
                }
            }
            if (Input.GetMouseButtonDown(1)) { if (_sub != null) Back(); else Close(); return; }
        }

        private float _nextHold;

        private void Back() { _sub = null; _hover = -1; Fill(); }

        private void OnGUI()
        {
            if (!_open && (_pickFor != null || _pickAny)) { PickHint(); return; }
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
                else if (_hover == -2 && _sub != null) Back();
                e.Use();
                return;
            }
            if (e.type != EventType.Repaint) return;
            Textures();
            // the ring's plate and the middle disc
            Disc(c, (ringR + Btn * 0.62f) * s, new Color(0.07f, 0.05f, 0.035f, 0.55f), new Color(0.66f, 0.52f, 0.23f, 0.35f));
            bool back = _sub != null, backHot = back && _hover == -2;
            if (backHot) { float g = DiscR * 2.3f * s; GUI.color = new Color(0.94f, 0.78f, 0.43f, 0.35f); GUI.DrawTexture(new Rect(c.x - g / 2f, c.y - g / 2f, g, g), _soft); GUI.color = Color.white; }
            Disc(c, DiscR * s, new Color(0.07f, 0.05f, 0.035f, 0.95f), new Color(0.66f, 0.52f, 0.23f, backHot ? 0.95f : 0.6f));
            if (back)
            {
                Text(new Rect(c.x - DiscR * s, c.y - 26f * s, DiscR * 2f * s, 24f * s), Lang.T(_sub.Title), _title, Gold, 16f * s);
                Text(new Rect(c.x - DiscR * 0.9f * s, c.y + 2f * s, DiscR * 1.8f * s, 24f * s), "← " + Lang.T("Назад"), _small, backHot ? Bright : Ink, 13f * s);
            }
            else
            {
                Text(new Rect(c.x - DiscR * s, c.y - 26f * s, DiscR * 2f * s, 24f * s), _npc.Name, _title, Gold, 16f * s);
                Text(new Rect(c.x - DiscR * 0.9f * s, c.y + 0f * s, DiscR * 1.8f * s, 34f * s), _npc.StatusNow, _small, Ink, 11f * s);
                Character body = _npc.GetComponent<Character>();
                if (body != null && NpcTalk.Hired(_npc)) Text(new Rect(c.x - DiscR * s, c.y + 30f * s, DiscR * 2f * s, 18f * s), Lang.T("здоров'я ") + Mathf.RoundToInt(body.GetHealthPercentage() * 100f) + "%" + (NpcVitals.IsDown(body) ? Lang.T(" · без тями") : ""), _small, body.GetHealthPercentage() < 0.35f ? new Color(0.95f, 0.45f, 0.35f) : Ink, 11f * s);
                if (NpcResident.Is(_npc))
                {
                    float mood = NpcToil.Mood(_npc); Text(new Rect(c.x - DiscR * s, c.y + 44f * s, DiscR * 2f * s, 18f * s), Lang.F("настрій {0}% · втома {1}%", Mathf.RoundToInt(mood), Mathf.RoundToInt(NpcToil.Tired(_npc))), _small, mood < 30f ? new Color(0.95f, 0.45f, 0.35f) : Ink, 11f * s); // (NpcToil)
                    float full = NpcHunger.Full(_npc); Text(new Rect(c.x - DiscR * s, c.y + 58f * s, DiscR * 2f * s, 18f * s), Lang.F("ситість {0}%", Mathf.RoundToInt(full)), _small, full < 15f ? new Color(0.95f, 0.45f, 0.35f) : full < 35f ? new Color(0.92f, 0.75f, 0.38f) : Ink, 11f * s); // (NpcHunger: red starving, amber hungry)
                }
                if (_ask != null) Text(new Rect(c.x - DiscR * 0.95f * s, c.y - 50f * s, DiscR * 1.9f * s, 18f * s), _ask, _small, AskInk, 10.5f * s); // (its request: a gold «!» over its name)
            }
            for (int i = 0; i < n; i++)
            {
                Item it = _items[i];
                float a = i * 360f / n;
                Vector2 at = c + new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), -Mathf.Cos(a * Mathf.Deg2Rad)) * ringR * s;
                bool hot = i == _hover;
                float d = Btn * s * (hot ? 1.06f : 1f);
                if (hot || it.Now)
                {
                    float g = d * 1.5f;
                    GUI.color = new Color(it.Now ? 1f : 0.94f, it.Now ? 0.76f : 0.78f, it.Now ? 0.23f : 0.43f, hot ? 0.55f : 0.35f);
                    GUI.DrawTexture(new Rect(at.x - g / 2f, at.y - g / 2f, g, g), _soft);
                }
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(at.x - d / 2f, at.y - d / 2f, d, d), OrdersMenu.Pic(it.Off ? "n-frame-grey" : it.Now ? "n-frame-green" : "n-frame-dark"));
                float ic = d * 0.5f;
                GUI.color = it.Off ? new Color(0.5f, 0.5f, 0.5f, 0.8f) : IconInk;
                GUI.DrawTexture(new Rect(at.x - ic / 2f, at.y - ic / 2f, ic, ic), OrdersMenu.Pic(it.Icon));
                GUI.color = Color.white;
                Text(new Rect(at.x - 60f * s, at.y + d * 0.5f + 1f * s, 120f * s, 18f * s), it.Title, _small, hot ? Bright : Gold, 12f * s);
                if (hot && it.Where.HasValue) WhereMark(at, it.Where.Value, it.Title, s);
            }
            if (_hover >= 0 && _hover < n)
            {
                float w = 380f * s;
                float h = Plate.Set(_items[_hover].Title, _items[_hover].Desc, w, s);
                Plate.Place(c.x - w / 2f, Mathf.Max(4f, c.y - (ringR + Btn) * s - h - 8f * s));
            }
        }

        // while it waits for a place: by the crosshair, what the key does there and how long it still waits
        private static readonly RavenBook.ScreenPlate PickPlate = new RavenBook.ScreenPlate("RavenWatch_NpcPickHint", 30010);
        private void PickHint()
        {
            if (Event.current.type != EventType.Repaint) return;
            float left = _pickUntil - Time.time;
            if (left <= 0f || (_pickFor == null && !_pickAny) || _pickButton == null) { _pickFor = null; _pickButton = null; _pickAny = false; enabled = false; return; }
            float s = OrdersMenu.ScaleNow, w = 300f * s;
            string who = _pickFor != null ? _pickFor.Name : "";
            string title = "[" + KeyName() + "] " + Lang.T(_pickButton.Title) + (who.Length > 0 ? " — " + who : "");
            string desc = Lang.F("Наведи {0} і натисни {1}. Ще {2} с.", Lang.T(_pickButton.Place), KeyName(), Mathf.CeilToInt(left));
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
        private static readonly Color Gold = new Color(0.9f, 0.78f, 0.49f), Bright = new Color(1f, 0.85f, 0.54f), Ink = new Color(0.86f, 0.8f, 0.68f), AskInk = new Color(1f, 0.82f, 0.36f), IconInk = new Color(0.9f, 0.77f, 0.49f);
        private static GUIStyle _title, _small;
        private static Texture2D _disc, _soft, _ring;

        private static void Textures()
        {
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true, clipping = TextClipping.Overflow };
                _small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true, richText = true, clipping = TextClipping.Overflow };
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
