#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Серце оселі» (npc-plan.md): the centre of a settlement — a carved post with a raven on it, a board of notes under a
    // little roof, a lantern and a shelf with bread (a Workshop model marked «use: heart», Perches builds it; the cost
    // asks for Eikthyr's antler: proof you are worth following). What it is for:
    //   its name     Shift+E names it (as a portal); a person hired anywhere is sent to one of the named hearts of the
    //                world (the hire's list, NpcTalk) and walks there by itself (NpcTravel)
    //   its base     the residents settled by it all live at it: their home is the heart, so they share one base map
    //                (NpcBase) — up to ten of them
    //   its board    E opens its card: who lives here and how they are, and the notes — what is wrong at this base (no
    //                bed, no work, hunger, an empty pantry); later the quests go up on it too
    // No blow breaks it (no WearNTear): only the hammer takes it down. Taken down, its residents stay where they lived.
    internal sealed class NpcHeart : MonoBehaviour, Hoverable, Interactable, TextReceiver
    {
        internal static readonly int KName = "RavenWatch_heartName".GetStableHashCode(); // string
        internal static readonly int KBorn = "RavenWatch_heartBorn".GetStableHashCode(); // bool: its first rescue time decided (Start)
        internal static readonly int KDining = "RavenWatch_heartDining".GetStableHashCode(); // string "user:id": its dining table ("" none)
        internal const int NameMax = 24;
        internal static readonly List<NpcHeart> Loaded = new List<NpcHeart>();

        private ZNetView _nv;
        private Piece _piece;

        private void Awake()
        {
            _nv = GetComponent<ZNetView>(); _piece = GetComponent<Piece>();
            _box = GetComponentInChildren<Container>(true);
            if (_nv == null || _nv.GetZDO() == null) return; // (the hammer's ghost)
            Loaded.Add(this);
            MakePapers();
        }

        // just built by you (its creator is set after Awake: so here): the first heart you build in a world hears of
        // someone in need at once — its name asked for straight away (an unnamed heart hears nothing); a heart built
        // after it waits as usual (NpcRescue: five to seven days). A heart already standing (an old save) is left as it is.
        private void Start()
        {
            if (_nv == null || !_nv.IsValid() || !_nv.IsOwner() || _piece == null) return;
            ZDO z = _nv.GetZDO();
            Player me = Player.m_localPlayer;
            if (z.GetBool(KBorn, false) || me == null || _piece.GetCreator() != me.GetPlayerID()) return;
            if (z.GetLong(NpcRescue.KNext, 0L) != 0L || !NpcKit.GetRef(z, NpcRescue.KNpc).IsNone() || Name.Length > 0) { z.Set(KBorn, true); return; } // (one that has lived already)
            z.Set(KBorn, true);
            string key = "RavenWatch_firstHeart_" + (ZNet.instance != null ? ZNet.instance.GetWorldUID().ToString() : "");
            bool first = !me.m_customData.ContainsKey(key);
            me.m_customData[key] = "1";
            NpcRescue.Born(z, first);
            if (!first) return;
            if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, Lang.T("Дай оселі назву — і на дошці з'явиться перша чутка про людину в біді."));
            if (TextInput.instance != null) TextInput.instance.RequestText(this, Lang.T("Назва оселі"), NameMax);
        }

        // ---------------------------------------------------------------- its coin box («Скарбничка»): one cell, the workers' pay
        // (NpcToil: each walks up and takes its own). A part of the heart's model itself (sertse_oseli.txt: the game's
        // personal chest by the stones), made a chest of the game once, in the heart's prefab (Perches → Prepare): its own
        // spot to look at and press, the game's chest window, its keeping in the heart's ZDO. A heart with coins in it cannot
        // be taken down (the game's rule for a chest that is not empty).
        internal const string BoxPart = "piece_chest_private";
        private Container _box;
        internal Container Box { get { return _box; } } // (a worker walks up to it for its pay: NpcToil)

        internal static void Prepare(GameObject root, Transform look, ZNetScene scene)
        {
            Transform part = look != null ? look.Find(BoxPart) : null;
            if (part == null) { Npcs.Log("heart: no " + BoxPart + " part in its model — no coin box"); return; }
            // the model's part copies every look of the chest, near and far: only the nearest is kept
            GameObject src = scene.GetPrefab(BoxPart);
            HashSet<string> far = new HashSet<string>();
            if (src != null)
                foreach (LODGroup lg in src.GetComponentsInChildren<LODGroup>(true))
                {
                    LOD[] lods = lg.GetLODs();
                    for (int i = 1; i < lods.Length; i++) if (lods[i].renderers != null) foreach (Renderer r in lods[i].renderers) if (r != null) far.Add(r.name);
                }
            Bounds b = new Bounds(); bool any = false;
            foreach (MeshRenderer mr in part.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (far.Contains(mr.name)) { UnityEngine.Object.DestroyImmediate(mr.gameObject); continue; }
                mr.sharedMaterials = Yellower(mr.sharedMaterials);
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds m = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 p = part.InverseTransformPoint(mr.transform.TransformPoint(m.center + Vector3.Scale(m.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            BoxCollider bc = part.gameObject.AddComponent<BoxCollider>();
            if (any) { bc.center = b.center; bc.size = b.size; } else { bc.center = new Vector3(0f, 0.4f, 0f); bc.size = new Vector3(1f, 0.8f, 0.6f); }
            Container c = part.gameObject.AddComponent<Container>(); // (the prefab sleeps: it wakes in each heart, told whose ZDO it keeps in)
            c.m_name = "Скарбничка"; c.m_width = 1; c.m_height = 1;
            c.m_rootObjectOverride = root.GetComponent<ZNetView>(); c.m_privacy = Container.PrivacySetting.Public; c.m_checkGuardStone = false;
            Container like = src != null ? src.GetComponent<Container>() : null;
            if (like != null) { c.m_bkg = like.m_bkg; c.m_openEffects = like.m_openEffects; c.m_closeEffects = like.m_closeEffects; }
        }

        // its wood a little yellower — nearer the plain wooden chest; and no snow on its lid (the game's piece shader lays
        // snow on what faces up; a copied look is no piece of the game's, so it is put out here)
        private static readonly Dictionary<Material, Material> Yellow = new Dictionary<Material, Material>();

        private static Material[] Yellower(Material[] ms)
        {
            Material[] o = new Material[ms.Length];
            for (int i = 0; i < ms.Length; i++)
            {
                Material from = ms[i];
                if (from == null || !from.HasProperty("_Color")) { o[i] = from; continue; }
                Material m;
                if (!Yellow.TryGetValue(from, out m) || m == null)
                {
                    m = new Material(from) { name = from.name + " (heart)" };
                    Color k = from.color;
                    if (!from.name.EndsWith(" (perch)", StringComparison.Ordinal)) m.color = new Color(k.r * 1.25f, k.g * 1.12f, k.b * 0.75f, k.a); // (a tint of the model's own: kept as the author chose it)
                    foreach (string n in m.GetPropertyNames(MaterialPropertyType.Float)) if (n.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0) m.SetFloat(n, 0f);
                    foreach (string kw in m.shaderKeywords) if (kw.IndexOf("SNOW", StringComparison.OrdinalIgnoreCase) >= 0) m.DisableKeyword(kw);
                    Yellow[from] = m;
                }
                o[i] = m;
            }
            return o;
        }

        private static string CoinName { get { GameObject p = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Coins") : null; ItemDrop d = p != null ? p.GetComponent<ItemDrop>() : null; return d != null ? d.m_itemData.m_shared.m_name : "$item_coins"; } }

        internal int Coins { get { return _box != null && _box.GetInventory() != null ? _box.GetInventory().CountItems(CoinName) : 0; } }

        // a worker's pay out of it (on its owner's game): −1 the box is open now, 0 not enough there (none taken), else n
        internal int TakeCoins(int n)
        {
            if (_box == null || _nv == null || !_nv.IsValid() || _box.GetInventory() == null) return 0;
            if (_nv.GetZDO().GetInt(ZDOVars.s_inUse, 0) != 0) return -1;
            if (!_nv.IsOwner()) _nv.ClaimOwnership(); // (its owner keeps the box's contents: ours for the moment)
            if (!_nv.IsOwner()) return -1;
            Inventory inv = _box.GetInventory();
            if (inv.CountItems(CoinName) < n) return 0;
            inv.RemoveItem(CoinName, n);
            return n;
        }

        // ---------------------------------------------------------------- its dining table («Їдальня»): picked by the player
        // from the board. Its residents eat at it (NpcMeals) and spend most evenings by it (NpcResident); gone, they find a
        // table by themselves again. Only a table in this heart's base.
        internal ZDOID DiningId { get { return _nv != null && _nv.IsValid() ? NpcKit.GetRef(_nv.GetZDO(), KDining) : ZDOID.None; } }

        internal Transform Dining
        {
            get
            {
                ZDOID id = DiningId;
                GameObject go = NpcKit.Find(id);
                return go != null ? go.transform : null;
            }
        }

        // the dining table of the base round a spot (its heart stands at the base's centre)
        internal static Transform DiningOf(NpcBase b)
        {
            NpcHeart h = b != null ? Near(b.Centre, 3f) : null;
            return h != null ? h.Dining : null;
        }

        private string SetDining(Vector3 at)
        {
            if (_nv == null || !_nv.IsValid()) return null;
            NpcBase b = NpcBase.For(transform.position);
            Transform t = NpcMeals.TableAt(b, at, 3f);
            if (t == null) return Lang.T("Тут нема столу для їжі в межах цієї оселі.");
            ZNetView tv = t.GetComponent<ZNetView>();
            if (tv == null || !tv.IsValid()) return null;
            _nv.ClaimOwnership();
            NpcKit.SetRef(_nv.GetZDO(), KDining, tv.GetZDO().m_uid);
            _hover = null;
            return Lang.T("Їдальня тут: мешканці їстимуть за цим столом і вечорами збиратимуться біля нього.");
        }

        private void ClearDining()
        {
            if (_nv == null || !_nv.IsValid()) return;
            _nv.ClaimOwnership();
            NpcKit.SetRef(_nv.GetZDO(), KDining, ZDOID.None);
            if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, Lang.T("Їдальню знято: їстимуть за найближчим столом."));
        }

        // ---------------------------------------------------------------- notes pinned on its board: one more for every trouble
        // at the base (up to four), on every game alike (each counts them itself, once in a few seconds while you are near)
        private const float Size = 4.5f / 2.7f; // (the model grew from 2.7 m to 4.5 m: the notes with it)
        private static readonly Vector3[] PaperAt = { new Vector3(-0.27f, 1.19f, -0.165f) * Size, new Vector3(0.12f, 1.21f, -0.165f) * Size, new Vector3(-0.07f, 0.88f, -0.165f) * Size, new Vector3(-0.29f, 0.9f, -0.165f) * Size };
        private static readonly float[] PaperTilt = { 6f, -8f, 3f, -4f };
        private static Material _paper, _seal;
        private readonly List<GameObject> _papers = new List<GameObject>();
        private readonly List<GameObject> _seals = new List<GameObject>(); // (a red seal: a paper that asks for something)
        private float _nextPapers;

        private void MakePapers()
        {
            if (_paper == null)
            {
                Shader sh = Shader.Find("Standard");
                if (sh == null) return;
                _paper = new Material(sh) { color = new Color(0.86f, 0.78f, 0.6f) };
                _paper.SetFloat("_Glossiness", 0.05f);
                _seal = new Material(sh) { color = new Color(0.62f, 0.08f, 0.06f) };
                _seal.SetFloat("_Glossiness", 0.4f);
            }
            for (int i = 0; i < PaperAt.Length; i++)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Collider c = q.GetComponent<Collider>(); if (c != null) Destroy(c);
                q.name = "note_" + i;
                q.transform.SetParent(transform, false);
                q.transform.localPosition = PaperAt[i];
                q.transform.localRotation = Quaternion.Euler(0f, 0f, PaperTilt[i]); // (a quad faces −Z: the board's front)
                q.transform.localScale = new Vector3(0.13f * Size, 0.16f * Size, 1f);
                q.GetComponent<MeshRenderer>().sharedMaterial = _paper;
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Collider sc = s.GetComponent<Collider>(); if (sc != null) Destroy(sc);
                s.name = "seal";
                s.transform.SetParent(q.transform, false);
                s.transform.localPosition = new Vector3(0f, 0.32f, -0.02f); // (at the top, a hair in front)
                s.transform.localScale = new Vector3(0.28f, 0.22f, 1f);
                s.GetComponent<MeshRenderer>().sharedMaterial = _seal;
                s.SetActive(false);
                _seals.Add(s);
                q.SetActive(false);
                _papers.Add(q);
            }
        }

        private void Update()
        {
            if (_papers.Count == 0 || Time.time < _nextPapers) return;
            _nextPapers = Time.time + 5f;
            Player me = Player.m_localPlayer;
            if (me == null || (me.transform.position - transform.position).sqrMagnitude > 40f * 40f) return; // (nobody to see them)
            List<NpcActor> who = Residents(Id);
            int n = Mathf.Min(_papers.Count, Notes(who).Count), asks = 0;
            foreach (NpcActor a in who) if (NpcRequests.Note(a) != null) asks++;
            for (int i = 0; i < _papers.Count; i++)
            {
                if (_papers[i].activeSelf != i < n) _papers[i].SetActive(i < n);
                if (_seals[i].activeSelf != i < asks) _seals[i].SetActive(i < asks); // (the asks first: never crowded out)
            }
        }
        private void OnDestroy() { Loaded.Remove(this); }

        internal ZDOID Id { get { return _nv != null && _nv.IsValid() ? _nv.GetZDO().m_uid : ZDOID.None; } }
        internal string Name { get { return _nv != null && _nv.IsValid() ? _nv.GetZDO().GetString(KName, "") : ""; } }
        internal string Title { get { string n = Name; return n.Length > 0 ? n : Lang.T("Серце оселі"); } }

        // the heart near a spot (settling: its residents live at it)
        internal static NpcHeart Near(Vector3 at, float range)
        {
            NpcHeart best = null; float bd = range * range;
            foreach (NpcHeart h in Loaded) { if (h == null) continue; float d = (h.transform.position - at).sqrMagnitude; if (d < bd) { bd = d; best = h; } }
            return best;
        }

        // who lives at it (those loaded here: at the heart itself, that is all of them)
        internal static List<NpcActor> Residents(ZDOID heart)
        {
            List<NpcActor> l = new List<NpcActor>();
            if (heart.IsNone()) return l;
            foreach (NpcActor a in NpcActor.Live) if (a != null && NpcResident.Is(a) && NpcKit.GetRef(a.Zdo, NpcResident.KHeart) == heart) l.Add(a);
            return l;
        }

        // ---------------------------------------------------------------- the hover, E (its card), Shift+E (its name)
        public string GetHoverName() { return Title; }
        public float GetHoverOffset() { return 0f; }

        // the hover is asked every frame while you look at it: made once a second
        private string _hover; private float _hoverAt = -10f;

        public string GetHoverText()
        {
            if (_hover != null && Time.time - _hoverAt < 1f) return _hover;
            _hoverAt = Time.time;
            List<NpcActor> who = Residents(Id);
            List<string> notes = Notes(who);
            string s = Title + "\n<size=80%>" + Lang.F("Живе: {0}/{1}", who.Count, NpcResident.PerBase) + " · " + Lang.F("Скарбничка: {0}", Coins) + (notes.Count > 0 ? "\n<color=#e0b860>" + notes[0] + "</color>" : "") + "</size>";
            s += "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Дошка") + "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] " + Lang.T("Назва");
            _hover = Localization.instance != null ? Localization.instance.Localize(s) : s;
            return _hover;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || user != Player.m_localPlayer) return false;
            if (alt) { if (TextInput.instance != null) TextInput.instance.RequestText(this, Lang.T("Назва оселі"), NameMax); return true; }
            ShowBoard();
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        public string GetText() { return Name; }

        // a typed name goes into hover texts (rich text, the game's $words), the talk's lists and the board: none of the
        // signs those read as their own — a tag, a $word, the talk's «|», «{…}» forms or «=>» — only words
        private static string Clean(string text)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char ch in text ?? "") if ("<>$|{}[]\\".IndexOf(ch) < 0 && !char.IsControl(ch)) sb.Append(ch);
            return sb.ToString().Replace("=>", "").Trim();
        }

        public void SetText(string text)
        {
            if (_nv == null || !_nv.IsValid()) return;
            text = Clean(text);
            if (text.Length > NameMax) text = text.Substring(0, NameMax);
            _nv.ClaimOwnership();
            _nv.GetZDO().Set(KName, text);
        }

        // ---------------------------------------------------------------- its board: the residents and the notes
        private void ShowBoard()
        {
            List<NpcActor> who = Residents(Id);
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
            if (who.Count == 0) rows.Add(new KeyValuePair<string, string>(Lang.T("Мешканці"), Lang.T("Ще ніхто тут не живе. Найми когось і вибери цю оселю — або оселі тут того, хто вже з тобою.")));
            else
            {
                List<string> lines = new List<string>();
                foreach (NpcActor a in who) lines.Add("<b>" + a.Name + "</b> — " + State(a));
                rows.Add(new KeyValuePair<string, string>(Lang.F("Мешканці {0}/{1}", who.Count, NpcResident.PerBase), string.Join("\n", lines.ToArray())));
            }
            string rumour = NpcRescue.BoardLine(_nv.GetZDO(), transform.position); // (people in need: read here, marked on your map)
            if (rumour != null) rows.Add(new KeyValuePair<string, string>(Lang.T("Чутки"), rumour));
            int pay = 0; foreach (NpcActor a in who) if (a.Worker != null && a.Worker.Works) pay += NpcToil.Wage(a);
            if (pay > 0) rows.Add(new KeyValuePair<string, string>(Lang.T("Скарбничка"), Lang.F("Скринька внизу: {0} монет. Робітники самі беруть платню раз на 5 днів — разом {1}. Платять вчасно — працюють охочіше й вправнішають.", Coins, pay)));
            List<string> notes = Notes(who);
            rows.Add(new KeyValuePair<string, string>(Lang.T("Записки"), notes.Count == 0 ? Lang.T("Поки тихо: усе гаразд.") : "• " + string.Join("\n• ", notes.ToArray())));
            bool broken = NpcKit.HasRef(_nv.GetZDO(), KDining) && (DiningId.IsNone() || (ZDOMan.instance != null && ZDOMan.instance.GetZDO(DiningId) == null)); // (a link kept, its table found nowhere — you stand at the heart, its base is loaded: gone, not far) // (its table gone from the world — broken, taken down: not merely far)
            if (broken) { _nv.ClaimOwnership(); NpcKit.SetRef(_nv.GetZDO(), KDining, ZDOID.None); Npcs.Log("heart «" + Name + "»: its dining table is gone — dining let go"); }
            bool dining = !DiningId.IsNone();
            rows.Add(new KeyValuePair<string, string>(Lang.T("Їдальня"), broken ? Lang.T("Стіл їдальні зламано — їдальню знято. Обери новий стіл.") : !dining ? Lang.T("Не обрано: їдять за столом біля комори, вечорами сидять біля вогню.")
                : Dining != null ? Lang.T("Обрано стіл: тут їдять і вечорами здебільшого сидять. Інколи хтось вибере вогонь надворі.")
                : Lang.T("Стіл їдальні зник: поки що їдять де знайдуть. Обери інший.")));
            if (Name.Length == 0) rows.Add(new KeyValuePair<string, string>(Lang.T("Назва"), Lang.T("Без назви сюди не пошлеш нікого з найманих. Shift+E — дати назву.")));
            List<KeyValuePair<string, Action>> buttons = new List<KeyValuePair<string, Action>>();
            List<string> tips = new List<string>();
            buttons.Add(new KeyValuePair<string, Action>(dining ? Lang.T("Інший стіл їдальні") : Lang.T("Обрати їдальню"), () => NpcMenu.PickPlace("Їдальня", "на стіл їдальні", SetDining)));
            tips.Add(dining ? Lang.T("Перенести їдальню: покажи інший стіл. Їжу братимуть зі скринь довкола нього, а сідатимуть на лави й стільці поруч.")
                : Lang.T("Покажи стіл — тут мешканці обідатимуть і збиратимуться вечорами. Їжу братимуть зі скринь довкола, сідатимуть на лави й стільці поруч."));
            if (dining) { buttons.Add(new KeyValuePair<string, Action>(Lang.T("Зняти їдальню"), ClearDining)); tips.Add(Lang.T("Без їдальні мешканці їдять де випаде: за столом біля комори чи біля скрині з готовим, а вечорами сидять біля вогню.")); }
            RavenBook.ShowCard(Title, rows, buttons, tips);
        }

        private static string State(NpcActor a)
        {
            List<string> l = new List<string>();
            NpcWorker w = a.Worker;
            l.Add(w != null && w.Works ? Jobs.Word(w.JobId) : Lang.T("без роботи"));
            l.Add(NpcHunger.Word(a));
            float mood = NpcToil.Mood(a); l.Add(Lang.F("настрій {0}%", Mathf.RoundToInt(mood)));
            if (NpcToil.Tired(a) >= 90f) l.Add(a.Form(Lang.T("втомлений|втомлена")));
            if (!NpcKit.HasRef(a.Zdo, NpcResident.KBed)) l.Add(Lang.T("без ліжка"));
            string now = a.StatusNow; if (now.Length > 0) l.Add(now); // (its owner's words on any game)
            return string.Join(", ", l.ToArray());
        }

        // what is wrong at this base, the worst first
        private List<string> Notes(List<NpcActor> who)
        {
            List<string> starving = new List<string>(), hungry = new List<string>(), notes = new List<string>();
            if (_nv != null && _nv.IsValid() && _nv.GetZDO().GetString(NpcRescue.KInfo, "").Length > 0) notes.Add(Lang.T("Чутка: людина в біді (подробиці — на дошці)"));
            foreach (NpcActor a in who)
            {
                string n = a.Name;
                if (NpcHunger.IsStarving(a)) starving.Add(Lang.F("{0} голодує — без їжі кине роботу, а згодом піде", n));
                else if (NpcHunger.IsHungry(a)) hungry.Add(a.Form(Lang.F("{0} голодний|голодна", n)));
            }
            notes.AddRange(starving);
            foreach (NpcActor a in who) { string req = NpcRequests.Note(a); if (req != null) notes.Add(req); } // (what they ask for, and what they pay)
            NpcBase b = who.Count > 0 ? NpcBase.For(transform.position) : null;
            if (who.Count > 0 && !DiningId.IsNone() && Dining == null) notes.Add(Lang.T("Нема столу їдальні — обери інший на дошці"));
            if (b != null && b.Ready && NpcMeals.Pantry(b, transform.position) == null) notes.Add(Lang.T("Нема що їсти") + ": " + NpcMeals.Why(b));
            notes.AddRange(hungry);
            foreach (NpcActor a in who) if (!NpcKit.HasRef(a.Zdo, NpcResident.KBed)) notes.Add(Lang.F("{0} не має ліжка", a.Name));
            foreach (NpcActor a in who) if (a.StatusNow.Contains("ящик повний")) notes.Add(Lang.F("{0}: ящик повний", a.Name));
            foreach (NpcActor a in who) if (a.Worker != null && a.Worker.Works && NpcToil.Unpaid(a) > 0) notes.Add(a.Form(Lang.F("{0} без платні — поклади монети в скарбничку (бере {1} раз на 5 днів)", a.Name, NpcToil.Wage(a))));
            foreach (NpcActor a in who) if (NpcToil.Mood(a) < 30f) { string why = NpcToil.WhyWord(a); notes.Add(a.Form(Lang.F("{0} невдоволений|невдоволена", a.Name)) + (why.Length > 0 ? ": " + why : "")); }
            foreach (NpcActor a in who) { string j = a.Worker != null ? a.Worker.JobId : ""; string lacks = j == "lumber" || j == "miner" ? NpcHew.Lacks(a, j == "lumber") : null; if (lacks != null) notes.Add(a.Name + ": " + lacks); } // (no tool, a broken one, no plot)
            foreach (NpcActor a in who) if (a.Worker == null || !a.Worker.Works) notes.Add(Lang.F("{0} без роботи", a.Name));
            if (who.Count >= NpcResident.PerBase) notes.Add(Lang.T("Місця більше нема"));
            return notes;
        }

        // ---------------------------------------------------------------- every named heart of the world (the hire's list)
        // The server knows them all (a game knows only those near it): asked once per hire, answered with names and places.
        internal sealed class Entry { public ZDOID Id; public string Name; public Vector3 Pos; }
        private const string RpcAsk = "RavenWatch_HeartsAsk", RpcList = "RavenWatch_HeartsList";
        private static Action<List<Entry>> _waiting;
        private static int _asked;

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcAsk, new Action<long, ZPackage>(OnAsk));
            ZRoutedRpc.instance.Register<ZPackage>(RpcList, new Action<long, ZPackage>(OnList));
        }

        internal static void Ask(Action<List<Entry>> done)
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null) { done(new List<Entry>()); return; }
            if (ZNet.instance.IsServer()) { done(All()); return; }
            _waiting = done;
            ZNetPeer server = ZNet.instance.GetServerPeer();
            if (server == null) { _waiting = null; done(Here()); return; }
            ZRoutedRpc.instance.InvokeRoutedRPC(server.m_uid, RpcAsk, new ZPackage());
            int ask = ++_asked;
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(GiveUp(ask));
        }

        private static void OnAsk(long sender, ZPackage none)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            List<Entry> all = All();
            ZPackage pkg = new ZPackage();
            pkg.Write(all.Count);
            foreach (Entry e in all) { pkg.Write(e.Id); pkg.Write(e.Name); pkg.Write(e.Pos); }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcList, pkg);
        }

        private static void OnList(long sender, ZPackage pkg)
        {
            List<Entry> l = new List<Entry>();
            try { int n = pkg.ReadInt(); for (int i = 0; i < n; i++) l.Add(new Entry { Id = pkg.ReadZDOID(), Name = pkg.ReadString(), Pos = pkg.ReadVector3() }); }
            catch (Exception e) { Npcs.Log("hearts list: " + e.Message); }
            Action<List<Entry>> done = _waiting; _waiting = null;
            if (done != null) done(l);
        }

        private static System.Collections.IEnumerator GiveUp(int ask)
        {
            yield return new WaitForSecondsRealtime(5f);
            if (ask != _asked || _waiting == null) yield break;
            Action<List<Entry>> done = _waiting; _waiting = null;
            Npcs.Log("hearts: no answer from the server — only those near are listed");
            done(Here());
        }

        // the named ones loaded here (no server answer)
        private static List<Entry> Here()
        {
            List<Entry> l = new List<Entry>();
            foreach (NpcHeart h in Loaded) if (h != null && h.Name.Length > 0) l.Add(new Entry { Id = h.Id, Name = h.Name, Pos = h.transform.position });
            return l;
        }

        // is there a heart anywhere in the world, named or not (server: HallState's kindling)
        internal static bool AnyInWorld()
        {
            if (ZDOMan.instance == null) return false;
            foreach (Perches.Def d in Perches.Hearts)
            {
                List<ZDO> zdos = NpcCensus.Of(d.Prefab); // (the shared census: NpcCensus)
                if (zdos.Count > 0) return true;
            }
            return false;
        }

        // the named ones, from the server's own ZDOs (all of the world)
        private static List<Entry> All()
        {
            List<Entry> l = new List<Entry>();
            foreach (Perches.Def d in Perches.Hearts)
            {
                List<ZDO> zdos = NpcCensus.Of(d.Prefab); // (the shared census: NpcCensus)
                foreach (ZDO z in zdos)
                {
                    string name = z.GetString(KName, "");
                    if (name.Length > 0) l.Add(new Entry { Id = z.m_uid, Name = name, Pos = z.GetPosition() });
                }
            }
            l.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return l;
        }
    }
}
#endif
