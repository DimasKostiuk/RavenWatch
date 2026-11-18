#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Серце оселі» (npc-plan.md): the centre of a settlement — a carved post with a raven on it, a board of notes under a
    // little roof, a lantern and a shelf with bread (a Workshop model marked «use: heart», Perches builds it; the cost
    // asks for Eikthyr's antler: proof you are worth following). What it is for:
    //   its name     a new one takes a plain name of its own at once («Оселя Nfr», «Оселя Nfr 2»: the user 2026-11-04, no window in the middle
    //                of building); the board's button or Shift+E renames it (as a portal); a person hired anywhere is sent to one of the named hearts of the
    //                world (the hire's list, NpcTalk) and walks there by itself (NpcTravel)
    //   its base     the residents settled by it all live at it: their home is the heart, so they share one base map
    //                (NpcBase) — up to ten of them, near or far: a game sees only those near it, the server all, so the server keeps their count on the heart (ServerTick, HowMany)
    //   its board    E opens how this settlement lives (NpcBoard, a sheet); its papers in the world count what is wrong there —
    //                unless a module takes that E (UseInstead, its words in the hover: HoverInstead; the story's «on: heart_use»)
    // No blow breaks it (no WearNTear): only the hammer takes it down. Taken down, its residents stay where they lived; a heart put up again within NpcBase.Reach
    // of their home takes them (NpcResident.Rehome, if there is room) and its dining table (the table says whose it was: KDiningOf, TableLook) — not its name.
    //   its lantern  burns while LampLit says so of this heart (always; the story: each heart dark till an ember of the Hall's fire is put in
    //                it — Light, KLit in its ZDO, every game sees it: StoryHeart, the user 2026-11-15),
    //                looked at every 2 s; Flare: a flash in it that dies out at once (the prologue's first heart). Its light
    //                is the game's own (Perches.MakeLight: LightLod switches it by distance, LightFlicker sets its
    //                brightness each frame), so it is put out by its whole object, and the flicker rests while it flares
    internal sealed class NpcHeart : MonoBehaviour, Hoverable, Interactable, TextReceiver
    {
        internal static readonly int KName = "RavenWatch_heartName".GetStableHashCode(); // string
        internal static readonly int KBorn = "RavenWatch_heartBorn".GetStableHashCode(); // bool: its first rescue time decided (Start)
        internal static readonly int KDining = "RavenWatch_heartDining".GetStableHashCode(); // string "user:id": its dining table ("" none)
        internal static readonly int KFolk = "RavenWatch_heartFolk".GetStableHashCode();     // int: how many live at it, as the server last counted (-1: not yet)
        internal static readonly int KFolkAt = "RavenWatch_heartFolkAt".GetStableHashCode(); // long: world second the last of those settled (one settled later is not counted yet)
        internal static readonly int KLit = "RavenWatch_heartLit".GetStableHashCode(); // bool: its lantern lit for good (Light: an ember of the Hall's fire put in it — the story's)
        internal const int NameMax = 20; // (the board's plate grows with it: twenty keep the head's ends in sight; an old longer name is left as it is)
        internal static readonly List<NpcHeart> Loaded = new List<NpcHeart>();

        private ZNetView _nv;
        private Piece _piece;
        internal static Func<NpcHeart, bool> LampLit = h => true; // (who says a heart's lantern burns: the story sets it — StoryHeart: its own KLit)
        internal bool Lit { get { return _nv != null && _nv.IsValid() && _nv.GetZDO().GetBool(KLit, false); } }
        // its lantern lit for good (the story's ember put in it): written into its ZDO — every game lights it; a flash first
        internal void Light()
        {
            if (_nv == null || !_nv.IsValid()) return;
            if (!_nv.IsOwner()) _nv.ClaimOwnership(); // (a piece nobody moves: the one who lights it writes it)
            _nv.GetZDO().Set(KLit, true);
            Flare(2.5f);
        }
        // a module's own E on a heart instead of its board (null: not theirs; true: done) and the words of that E in its hover
        // (null: the board's) — as NpcActor.UseInstead / HoverInstead are for a person. The story: «on: heart_use» (StoryHeart)
        internal static readonly List<Func<NpcHeart, Player, bool?>> UseInstead = new List<Func<NpcHeart, Player, bool?>>();
        internal static readonly List<Func<NpcHeart, Player, string>> HoverInstead = new List<Func<NpcHeart, Player, string>>();
        private Light[] _lamps = new Light[0];
        private float[] _lampBright = new float[0];
        // where its lantern hangs (its first light; none — over its middle): a scene's close shot of the lantern («heart.lamp»), not of the middle
        // of the little house, where the camera had no room (the user 2026-11-15: «кадр зверху — у текстурах»)
        internal Vector3 LampAt { get { return _lamps.Length > 0 && _lamps[0] != null ? _lamps[0].transform.position : transform.position + Vector3.up * 1.5f; } }
        private LightFlicker[] _flickers = new LightFlicker[0];
        private float _nextLamp, _flareUntil = -1f;

        private void Awake()
        {
            _nv = GetComponent<ZNetView>(); _piece = GetComponent<Piece>();
            _box = GetComponentInChildren<Container>(true);
            if (_nv == null || _nv.GetZDO() == null) return; // (the hammer's ghost)
            Loaded.Add(this);
            MakePapers();
            _lamps = GetComponentsInChildren<Light>(true); // heavy: once, when a heart comes in
            _lampBright = new float[_lamps.Length];
            _flickers = new LightFlicker[_lamps.Length];
            for (int i = 0; i < _lamps.Length; i++) { _lampBright[i] = _lamps[i].intensity; _flickers[i] = _lamps[i].GetComponent<LightFlicker>(); }
            FindGlows();
            Lamp(LampLit(this), -1f);
        }

        // just built by you (its creator is set after Awake: so here): it takes its plain name; the first heart you build in
        // a world hears of someone in need at once, a heart built after it waits as usual (NpcRescue: five to seven days). A
        // heart already standing (an old save) is left as it is: unnamed, it hears nothing till you name it.
        private void Start()
        {
            if (_nv == null || !_nv.IsValid() || !_nv.IsOwner() || _piece == null) return;
            ZDO z = _nv.GetZDO();
            Player me = Player.m_localPlayer;
            if (z.GetBool(KBorn, false) || me == null || _piece.GetCreator() != me.GetPlayerID()) return;
            if (z.GetLong(NpcRescue.KNext, 0L) != 0L || !NpcKit.GetRef(z, NpcRescue.KNpc).IsNone() || Name.Length > 0) { z.Set(KBorn, true); return; } // (one that has lived already)
            NpcHeart near = Crowding(transform.position, this);
            if (near != null) { Refuse(me, near); return; } // (put down past the hammer's ghost — another mod's building: taken back whole, its materials too)
            z.Set(KBorn, true);
            string key = "RavenWatch_firstHeart_" + (ZNet.instance != null ? ZNet.instance.GetWorldUID().ToString() : "");
            Plugin.Instance.StartCoroutine(HammerAway(me)); // (built: the hammer away — its build menu, the story's scene of the first heart and the board all came at once, 2026-11-04)
            string had;
            int built = (me.m_customData.TryGetValue(key, out had) ? Errands.I(had, 1) : 0) + 1; // (how many you built in this world: two of one name in the hire's list read as one)
            bool first = built == 1;
            me.m_customData[key] = built.ToString();
            string number = first ? "" : " " + built, plain = Lang.F("Оселя {0}", me.GetPlayerName()); // (cut before its number, not after: two hearts stay two names)
            SetText((plain.Length + number.Length > NameMax ? plain.Substring(0, NameMax - number.Length) : plain) + number); // (a name as its builder would type it, in his language: what is written is a name, never read back as a word)
            NpcRescue.Born(z, first);
            if (first && MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, Lang.T("На дошці оселі з'явилась перша чутка про людину в біді."));
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
            Perches.ChestOf(root, part, "Скарбничка", 1, 1, Yellower); // (its nearest look, a box to press, the game's chest window: one cell, yellower wood)
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
            string outside = b.Refuse(at); if (outside != null) return outside; // (its base's own answer: not made yet, just built there — «wait», its update now; out of it — why; the user 2026-11-08 could pick no table for an hour)
            Transform t = NpcMeals.TableAt(b, at, 3f);
            if (t == null) return Lang.T("Тут нема столу для їжі в межах цієї оселі.");
            ZNetView tv = t.GetComponent<ZNetView>();
            if (tv == null || !tv.IsValid()) return null;
            _nv.ClaimOwnership();
            ZDOID was = DiningId;
            NpcKit.SetRef(_nv.GetZDO(), KDining, tv.GetZDO().m_uid);
            _nv.GetZDO().Set(KDiningSet, true);
            if (was != tv.GetZDO().m_uid) SignTable(was, false);
            SignTable(tv.GetZDO().m_uid, true);
            _hover = null;
            return Lang.T("Їдальня тут: мешканці їстимуть за цим столом і вечорами збиратимуться біля нього.");
        }

        internal void PickDining() { NpcMenu.PickPlace(Lang.K("Їдальня"), Lang.K("на стіл їдальні"), SetDining); }

        // the table says whose dining table it is (as a bed says whose bed: NpcResident.SignBed), so a heart put up anew within the base takes it
        // (the user 2026-11-14: the heart taken down and built again — its people and its dining go over to the new one; its name does not)
        internal static readonly int KDiningOf = "RavenWatch_diningOf".GetStableHashCode(); // string on a table: a lasting link to the heart it is the dining table of ("" none)
        internal static readonly int KDiningSet = "RavenWatch_heartDiningSet".GetStableHashCode(); // bool on a heart: its dining picked or let go by hand, or taken over once — never taken over again
        private void SignTable(ZDOID table, bool on)
        {
            ZDO t = ZDOMan.instance != null && !table.IsNone() ? ZDOMan.instance.GetZDO(table) : null;
            if (t == null) return;
            string had = t.GetString(KDiningOf, "");
            if (on ? NpcKit.Resolve(had) == Id : had.Length == 0) return; // (a link names its session's id too: «it is ours» asked, not the words — else rewritten each world load)
            if (!t.IsOwner()) t.SetOwner(ZDOMan.GetSessionID());
            t.Set(KDiningOf, on ? NpcKit.Save(Id) : "");
        }

        // every TableEvery s with you near and someone living here: its dining table says so (an old save's: signed now, once); none picked and never chosen
        // here (KDiningSet: a dining let go by hand stays let go) — a table of its base signed by a heart that is gone, or by this one (put up again just where
        // the old one stood: the link finds it), is its own now. Its base's own list of tables (NpcBase.Tables), a link looked up each (kept once found)
        private const float TableEvery = 30f;
        private float _nextTable;
        private void TableLook()
        {
            if (Time.time < _nextTable || _nv == null || !_nv.IsValid()) return;
            _nextTable = Time.time + TableEvery;
            Player me = Player.m_localPlayer;
            if (me == null || (me.transform.position - transform.position).sqrMagnitude > NpcBase.Reach * NpcBase.Reach || Residents(Id).Count == 0) return; // (its base is made only where someone lives: not for this)
            if (NpcKit.HasRef(_nv.GetZDO(), KDining)) { if (Dining != null) SignTable(DiningId, true); return; }
            if (_nv.GetZDO().GetBool(KDiningSet, false)) return;
            NpcBase b = NpcBase.For(transform.position);
            if (!b.Ready || b.Tables == null) return;
            foreach (Transform t in b.Tables)
            {
                ZNetView tv = t != null ? t.GetComponent<ZNetView>() : null;
                ZDO tz = tv != null && tv.IsValid() ? tv.GetZDO() : null;
                string of = tz != null ? tz.GetString(KDiningOf, "") : "";
                if (of.Length == 0) continue; // (nobody's)
                ZDOID whose = NpcKit.Resolve(of);
                if (!whose.IsNone() && whose != Id) continue; // (another heart's, standing: not ours)
                _nv.ClaimOwnership();
                NpcKit.SetRef(_nv.GetZDO(), KDining, tz.m_uid);
                _nv.GetZDO().Set(KDiningSet, true);
                SignTable(tz.m_uid, true);
                _hover = null;
                Npcs.Log("heart «" + Name + "»: took the dining table of a heart that is gone");
                return;
            }
        }

        // its dining table for the board: 0 none · 1 picked · 2 its table is gone. Gone from the world (broken, taken down: you stand by the heart, its base is loaded — gone, not far): let go here.
        // A link that found nothing just now (loading in, looked for a little ago: NpcKit.Resolve) is not «gone»: only a table found and then gone from the world lets it go
        internal int DiningState()
        {
            if (_nv == null || !_nv.IsValid()) return 0;
            if (NpcKit.HasRef(_nv.GetZDO(), KDining) && !DiningId.IsNone() && ZDOMan.instance != null && ZDOMan.instance.GetZDO(DiningId) == null)
            {
                _nv.ClaimOwnership();
                NpcKit.SetRef(_nv.GetZDO(), KDining, ZDOID.None);
                Npcs.Log("heart «" + Name + "»: its dining table is gone — dining let go");
                return 2;
            }
            return DiningId.IsNone() ? 0 : Dining != null ? 1 : 2;
        }

        internal void ClearDining()
        {
            if (_nv == null || !_nv.IsValid()) return;
            _nv.ClaimOwnership();
            SignTable(DiningId, false);
            NpcKit.SetRef(_nv.GetZDO(), KDining, ZDOID.None);
            _nv.GetZDO().Set(KDiningSet, true);
            _hover = null;
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

        // off till their look is drawn (the user 2026-11-15: once the shader was found they showed for the first time — flat white squares behind
        // the heart, one on its roof, «шось непонятне»; they were placed for the 2.7 m model). The count of troubles goes on: the board's sheet says it
        internal static bool PapersShown = false;

        private void MakePapers()
        {
            if (!PapersShown) return;
            if (_paper == null)
            {
                Shader sh = FbxLook.Standard; // (Shader.Find("Standard") is null in this game: the papers never showed — 2026-11-15)
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

        // the hammer goes when the button that placed the heart is let go: with the button still down the empty hand struck the air (the user 2026-11-05)
        internal static float HammerWait = 3f;
        private static System.Collections.IEnumerator HammerAway(Player me)
        {
            float until = Time.time + HammerWait;
            while (me != null && Time.time < until && (ZInput.GetButton("Attack") || ZInput.GetButton("JoyPlace"))) yield return null;
            if (me != null) me.HideHandItems();
        }

        // the lantern painted into its model glows by itself (its emission map, the model's «<name>_glow» picture: WsAssets): a light put
        // out left it burning (the user 2026-11-05). Only that glow: a bell or a mace in it keep theirs. Each heart its own copies (the
        // model's materials are shared by every heart), all of them let go with it
        private readonly List<Material> _glows = new List<Material>(), _copies = new List<Material>();
        private readonly List<Color> _glowBright = new List<Color>();
        private static bool ModelGlow(Material m) { Texture t; return m != null && m.HasProperty("_EmissionColor") && m.HasProperty("_EmissionMap") && (t = m.GetTexture("_EmissionMap")) != null && t.name.EndsWith("_glow", StringComparison.Ordinal); }
        private void FindGlows()
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) // heavy: once, when a heart comes in
            {
                if (r is ParticleSystemRenderer || !Array.Exists(r.sharedMaterials, ModelGlow)) continue;
                foreach (Material own in r.materials) // (a copy of each: the renderer's own now)
                {
                    _copies.Add(own);
                    if (ModelGlow(own)) { _glows.Add(own); _glowBright.Add(own.GetColor("_EmissionColor")); }
                }
            }
        }

        // its lantern on or off (the light's whole object: the game's LightLod and LightFlicker on it rest with it; the glow in its model with it);
        // share >= 0: a flare's own brightness, the game's flicker resting; < 0: the game's own again
        private void Lamp(bool on, float share)
        {
            for (int i = 0; i < _glows.Count; i++) if (_glows[i] != null) _glows[i].SetColor("_EmissionColor", on ? _glowBright[i] * (share >= 0f ? share : 1f) : Color.black);
            for (int i = 0; i < _lamps.Length; i++)
            {
                if (_lamps[i] == null) continue;
                GameObject g = _lamps[i].gameObject;
                if (g.activeSelf != on) g.SetActive(on);
                if (_flickers[i] != null) _flickers[i].enabled = share < 0f;
                if (share >= 0f) { _lamps[i].enabled = true; _lamps[i].intensity = _lampBright[i] * share; }
                else _lamps[i].intensity = _lampBright[i];
            }
        }

        // a flash in its lantern that dies out at once (seconds): the fire is not yet its own
        internal void Flare(float seconds) { _flareUntil = Time.time + Mathf.Max(0.5f, seconds); _flareFor = Mathf.Max(0.5f, seconds); }
        private float _flareFor = 1f;
        internal static float FlareBright = 2.5f;

        private void LampTick()
        {
            if (_lamps.Length == 0) return;
            if (_flareUntil > 0f)
            {
                float t = 1f - (_flareUntil - Time.time) / _flareFor; // 0 → 1
                if (t >= 1f) { _flareUntil = -1f; Lamp(LampLit(this), -1f); return; }
                Lamp(true, FlareBright * (t < 0.15f ? t / 0.15f : Mathf.SmoothStep(1f, 0f, (t - 0.15f) / 0.85f))); // (up at once, dies down slowly)
                return;
            }
            if (Time.time < _nextLamp) return;
            _nextLamp = Time.time + 2f;
            bool lit = LampLit(this);
            for (int i = 0; i < _lamps.Length; i++) if (_lamps[i] != null && _lamps[i].gameObject.activeSelf != lit) { Lamp(lit, -1f); break; } // (only a change: the game's flicker left alone)
        }

        private void Update()
        {
            LampTick();
            TableLook();
            if (_papers.Count == 0 || Time.time < _nextPapers) return;
            _nextPapers = Time.time + NpcBoard.Every;
            Player me = Player.m_localPlayer;
            if (me == null || (me.transform.position - transform.position).sqrMagnitude > 40f * 40f) return; // (nobody to see them)
            _seen = NpcBoard.Make(this); // heavy: lockers and chest lists of one base, once in Every s, only with you within 40 m (the user 2026-11-06: it was worked out each second under the pointer, a way asked of the path finder chest by chest)
            int n = Mathf.Min(_papers.Count, _seen.Notes), asks = _seen.Asks;
            for (int i = 0; i < _papers.Count; i++)
            {
                if (_papers[i].activeSelf != i < n) _papers[i].SetActive(i < n);
                if (_seals[i].activeSelf != i < asks) _seals[i].SetActive(i < asks); // (the asks first: never crowded out)
            }
        }
        private void OnDestroy() { Loaded.Remove(this); foreach (Material m in _copies) if (m != null) Destroy(m); }

        private NpcBoard.Look _seen; // (the last look: the hover reads it, nothing more is worked out under the pointer)

        internal ZDO Zdo { get { return _nv != null && _nv.IsValid() ? _nv.GetZDO() : null; } }
        internal ZDOID Id { get { return _nv != null && _nv.IsValid() ? _nv.GetZDO().m_uid : ZDOID.None; } }

        // when the next rumour of someone in need comes (null while one goes on: the board's «поклик»)
        internal string RumourLine() { return _nv != null && _nv.IsValid() ? NpcRescue.BoardLine(_nv.GetZDO()) : null; }
        internal string Name { get { return _nv != null && _nv.IsValid() ? _nv.GetZDO().GetString(KName, "") : ""; } }
        internal string Title { get { string n = Name; return n.Length > 0 ? n : Lang.T("Серце оселі"); } }

        // Hearts stand apart (the user 2026-11-05): two at one place would be one settlement twice over — two rumours of people in
        // need, twenty residents, two maps of the same ground. Apart = two bases (NpcBase.Reach) that never overlap. Asked by the
        // hammer's ghost each frame it is shown (Perches: red ghost; a few hearts loaded at most) and once at a heart's birth (Start:
        // whatever put it down past the ghost). Hearts that stood close before this rule are left as they are.
        internal const float Apart = 2f * NpcBase.Reach;
        internal static NpcHeart Crowding(Vector3 at, NpcHeart self)
        {
            foreach (NpcHeart h in Loaded) if (h != null && h != self && (h.transform.position - at).sqrMagnitude < Apart * Apart) return h;
            return null;
        }
        internal static string TooNear(NpcHeart h) { return Lang.F("Тут уже б'ється серце оселі «{0}». Нове серце став далі, ніж за {1} кроків від нього.", h.Title, Mathf.RoundToInt(Apart)); }

        // a heart born too near another: its materials back on the ground, the heart gone
        private void Refuse(Player me, NpcHeart near)
        {
            Npcs.Log("heart at " + transform.position + " refused: «" + near.Title + "» stands " + Mathf.RoundToInt(Vector3.Distance(near.transform.position, transform.position)) + " m away");
            me.Message(MessageHud.MessageType.Center, TooNear(near));
            _piece.DropResources();
            ZNetScene.instance.Destroy(gameObject);
        }

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
            if (_seen == null) _seen = NpcBoard.Make(this); // heavy: once, when a heart is looked at before its first look (the papers' own, every NpcBoard.Every s, keeps it fresh)
            string first = _seen.Troubles.Count > 0 ? _seen.Troubles[0].Text : null;
            string s = Title + "\n<size=80%>" + Lang.F("Живе: {0}/{1}", _seen.Who.Count, NpcResident.PerBase) + " · " + NpcBoard.StateWord(_seen) + " · " + Lang.F("Скарбничка: {0}", Coins) + (first != null ? "\n<color=#e0b860>" + first + "</color>" : "") + "</size>";
            string own = null;
            for (int i = 0; i < HoverInstead.Count && own == null; i++) own = HoverInstead[i](this, Player.m_localPlayer);
            s += "\n[<color=yellow><b>$KEY_Use</b></color>] " + (own != null ? "<color=#e0b860>" + own + "</color>" : Lang.T("Дошка")) + "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] " + Lang.T("Назва");
            _hover = Localization.instance != null ? Localization.instance.Localize(s) : s;
            return _hover;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || user != Player.m_localPlayer) return false;
            if (alt) { AskName(); return true; }
            for (int i = 0; i < UseInstead.Count; i++) { bool? done = UseInstead[i](this, (Player)user); if (done.HasValue) return done.Value; }
            // the board asks a dozen modules about everyone here: one of them failing must not leave E doing nothing with no word (2026-11-14: a fire that was
            // gone — the press was lost, only the log knew). The cause goes to the log whole, the player is told; a tab's page is kept the same way (RavenBookBoard.cs)
            try { NpcBoard.Show(this); }
            catch (Exception e) { Npcs.Log("heart «" + Name + "»: its board failed: " + e); user.Message(MessageHud.MessageType.Center, Lang.T("Не вдалося це прочитати.")); }
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        internal void AskName() { if (TextInput.instance != null) TextInput.instance.RequestText(this, Lang.T("Назва оселі"), NameMax); }

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

        // ---------------------------------------------------------------- every named heart of the world (the hire's list)
        // The server knows them all (a game knows only those near it): asked once per hire, answered with names and places.
        internal sealed class Folk { public string Name = "", Job = ""; }
        internal sealed class Entry { public ZDOID Id; public string Name; public Vector3 Pos; public List<Folk> Folk = new List<Folk>(); }
        // the hearts of the world as the last answer counted them, named or not: all · not lit yet (the Watch's fire gives an ember only while one is
        // dark: LocHearth). −1: not known (no answer from the server, or one of an older build)
        internal static int Counted = -1, Dark = -1;

        // who lives at each heart, as the server last told: it sees the whole world, a game only what is loaded near it. Asked when a board opens and for the hire's list
        private static readonly Dictionary<ZDOID, List<Folk>> KnownFolk = new Dictionary<ZDOID, List<Folk>>(); // session: ids of this world load
        private static object _folkWorld;
        private static void Keep(List<Entry> l)
        {
            if (!ReferenceEquals(_folkWorld, ZNet.instance)) { KnownFolk.Clear(); _folkWorld = ZNet.instance; }
            foreach (Entry e in l) KnownFolk[e.Id] = e.Folk;
        }

        // those of it not loaded here now, by name (a story person has none on this list: it is only counted)
        internal static List<Folk> FarOf(ZDOID heart, List<NpcActor> here)
        {
            List<Folk> far = new List<Folk>(), all;
            if (KnownFolk.TryGetValue(heart, out all)) foreach (Folk f in all) if (f.Name.Length > 0 && !here.Exists(a => a.Key == f.Name)) far.Add(f);
            return far;
        }

        // how many live at it, «but» left out (NpcResident.NoRoom, the board): the server's count and those who settled since, never fewer than are loaded here.
        // Errs towards full for a moment after someone left, until the next count: never past ten
        internal static int HowMany(ZDOID heart, NpcActor but)
        {
            List<NpcActor> here = Residents(heart); here.Remove(but);
            ZDO z = ZDOMan.instance != null && !heart.IsNone() ? ZDOMan.instance.GetZDO(heart) : null;
            int counted = z != null ? z.GetInt(KFolk, -1) : -1;
            if (counted < 0) return here.Count; // (no count yet: a world's first moments)
            long at = z.GetLong(KFolkAt, 0L);
            int since = 0;
            foreach (NpcActor o in here) if (NpcLocker.Long(o.Zdo, NpcResident.KSettledAt, 0L) > at) since++;
            if (but != null && but.Zdo != null && NpcResident.Is(but) && NpcKit.GetRef(but.Zdo, NpcResident.KHeart) == heart && NpcLocker.Long(but.Zdo, NpcResident.KSettledAt, 0L) <= at) counted--; // (it is one of those counted)
            return Mathf.Max(here.Count, counted + since);
        }

        // ---------------------------------------------------------------- the server keeps the count (every CountEvery s; the shared census, written on a heart only when changed)
        private const float CountEvery = 20f;
        private static float _nextCount;

        internal static void ServerTick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || Time.time < _nextCount) return;
            _nextCount = Time.time + CountEvery;
            try
            {
                Dictionary<ZDOID, KeyValuePair<int, long>> folk = new Dictionary<ZDOID, KeyValuePair<int, long>>();
                foreach (ZDO p in NpcCensus.People())
                {
                    if (!p.GetBool(NpcResident.KResident, false)) continue;
                    ZDOID at = NpcKit.GetRef(p, NpcResident.KHeart);
                    if (at.IsNone()) continue;
                    KeyValuePair<int, long> had;
                    folk.TryGetValue(at, out had);
                    folk[at] = new KeyValuePair<int, long>(had.Key + 1, System.Math.Max(had.Value, NpcLocker.Long(p, NpcResident.KSettledAt, 0L)));
                }
                foreach (Perches.Def d in Perches.Hearts)
                    foreach (ZDO h in NpcCensus.Of(d.Prefab))
                    {
                        KeyValuePair<int, long> n;
                        folk.TryGetValue(h.m_uid, out n);
                        if (h.GetInt(KFolk, -1) == n.Key && h.GetLong(KFolkAt, 0L) == n.Value) continue;
                        if (h.GetOwner() != ZDOMan.GetSessionID()) h.SetOwner(ZDOMan.GetSessionID());
                        h.Set(KFolk, n.Key); h.Set(KFolkAt, n.Value);
                        if (n.Key > NpcResident.PerBase) Npcs.Log("heart «" + h.GetString(KName, "") + "»: " + n.Key + " live at it, more than " + NpcResident.PerBase); // (an old save's: left as they are)
                    }
            }
            catch (Exception e) { Npcs.Log("hearts' count: " + e.Message); }
        }

        private const string RpcAsk = "RavenWatch_HeartsAsk", RpcList = "RavenWatch_HeartsList";
        // session: all who wait for the server's answer — one asking serves them all (a board, a hire's list, a move, the Watch's fire may ask at once:
        // one slot let a later ask drop an earlier one's answer)
        private static readonly List<Action<List<Entry>>> _waiting = new List<Action<List<Entry>>>();
        private static int _asked;

        private static void Answer(List<Entry> l)
        {
            List<Action<List<Entry>>> all = new List<Action<List<Entry>>>(_waiting);
            _waiting.Clear();
            foreach (Action<List<Entry>> done in all) { try { done(new List<Entry>(l)); } catch (Exception e) { Npcs.Log("hearts: an answer's taker failed: " + e); } } // (each its own list: they sort it and take from it; one that throws does not take the answer from the rest)
        }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcAsk, new Action<long, ZPackage>(OnAsk));
            ZRoutedRpc.instance.Register<ZPackage>(RpcList, new Action<long, ZPackage>(OnList));
        }

        internal static void Ask(Action<List<Entry>> done)
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null) { done(new List<Entry>()); return; }
            if (ZNet.instance.IsServer()) { List<Entry> mine = All(); Keep(mine); done(mine); return; }
            ZNetPeer server = ZNet.instance.GetServerPeer();
            if (server == null) { Counted = Dark = -1; done(Here()); return; } // (no server to ask: those near only, the count not known)
            _waiting.Add(done);
            if (_waiting.Count > 1) return; // (asked already: that answer is for this one too)
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
            foreach (Entry e in all) { pkg.Write(e.Id); pkg.Write(e.Name); pkg.Write(e.Pos); pkg.Write(e.Folk.Count); foreach (Folk f in e.Folk) { pkg.Write(f.Name); pkg.Write(f.Job); } }
            pkg.Write(Counted); pkg.Write(Dark); // (after the entries: an older build reads them as before and leaves these)
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcList, pkg);
        }

        private static void OnList(long sender, ZPackage pkg)
        {
            List<Entry> l = new List<Entry>();
            try { int n = pkg.ReadInt(); for (int i = 0; i < n; i++) { Entry e = new Entry { Id = pkg.ReadZDOID(), Name = pkg.ReadString(), Pos = pkg.ReadVector3() }; int m = pkg.ReadInt(); for (int k = 0; k < m; k++) e.Folk.Add(new Folk { Name = pkg.ReadString(), Job = pkg.ReadString() }); l.Add(e); } Keep(l); Counted = Dark = -1; if (pkg.GetPos() < pkg.Size()) { Counted = pkg.ReadInt(); Dark = pkg.ReadInt(); } }
            catch (Exception e) { Npcs.Log("hearts list: " + e.Message); }
            Answer(l);
        }

        private static System.Collections.IEnumerator GiveUp(int ask)
        {
            yield return new WaitForSecondsRealtime(5f);
            if (ask != _asked || _waiting.Count == 0) yield break;
            Npcs.Log("hearts: no answer from the server — only those near are listed");
            Counted = Dark = -1;
            Answer(Here());
        }

        // the named ones loaded here (no server answer)
        private static List<Entry> Here()
        {
            List<Entry> l = new List<Entry>();
            foreach (NpcHeart h in Loaded) if (h != null && h.Name.Length > 0) l.Add(new Entry { Id = h.Id, Name = h.Name, Pos = h.transform.position });
            return l;
        }

        // is there a heart anywhere in the world, named or not, and where one of them stands (the server: the story's «heart_first» and «heart.first»
        // in a world whose hearts stood before those words — StoryHeart.ServerTick)
        internal static bool AnyInWorld(out Vector3 at)
        {
            at = Vector3.zero;
            if (ZDOMan.instance == null) return false;
            foreach (Perches.Def d in Perches.Hearts)
            {
                List<ZDO> zdos = NpcCensus.Of(d.Prefab); // (the shared census: NpcCensus)
                if (zdos.Count > 0) { at = zdos[0].GetPosition(); return true; }
            }
            return false;
        }

        // the named ones, from the server's own ZDOs (all of the world)
        private static List<Entry> All()
        {
            List<Entry> l = new List<Entry>();
            Dictionary<ZDOID, List<Folk>> people = new Dictionary<ZDOID, List<Folk>>();
            foreach (ZDO p in NpcCensus.People()) // (the shared census: once an asking)
            {
                if (!p.GetBool(NpcResident.KResident, false)) continue;
                ZDOID at = NpcKit.GetRef(p, NpcResident.KHeart);
                if (at.IsNone()) continue;
                List<Folk> folk;
                if (!people.TryGetValue(at, out folk)) people[at] = folk = new List<Folk>();
                folk.Add(new Folk { Name = p.GetString(NpcSettlers.KName, ""), Job = NpcLocker.Text(p, NpcWorker.KJob) });
            }
            Counted = Dark = 0;
            foreach (Perches.Def d in Perches.Hearts)
            {
                List<ZDO> zdos = NpcCensus.Of(d.Prefab); // (the shared census: NpcCensus)
                foreach (ZDO z in zdos)
                {
                    Counted++; if (!z.GetBool(KLit, false)) Dark++; // (every heart, named or not: an ember is for any dark one)
                    string name = z.GetString(KName, "");
                    if (name.Length > 0) { List<Folk> folk; if (!people.TryGetValue(z.m_uid, out folk)) folk = new List<Folk>(); l.Add(new Entry { Id = z.m_uid, Name = name, Pos = z.GetPosition(), Folk = folk }); }
                }
            }
            l.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return l;
        }
    }
}
#endif
