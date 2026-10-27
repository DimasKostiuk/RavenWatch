#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Рідкісні ворони» (the user, 2026-10-22): now and then a raven is of a breed of its own — rare (RareChance) or
    // legendary (LegendChance). A breed is a look (RavenLooks, «for: breed», tuned in «Фарби») and a trait on top of
    // the raven's knack; the book shows it on the knack's card, in gold.
    //   - Rolled once per raven item (its customData Key: the breed's id, or "-"), the first time the owner's game sees
    //     it — a new raven and every raven from before this came alike. Hidden until the raven wakes (trust 1): then it
    //     is revealed — the look comes on, a word in the middle, the players near are told.
    //   - Revealed, it goes with the raven's wear code (key "r"), so every game dresses it so: the shoulders, a perch, a
    //     grave, other players.
    //   - Over a rune of Freya a breed keeps its marks (Look.Keep…): those zones, the eyes' glow, the glow, the sheen,
    //     the sparks; its effects and things always stay. So a rare raven is known in any rune.
    // A breed is for life: the Fate stone does not touch it.
    internal static class Breeds
    {
        internal static float RareChance = 8f, LegendChance = 1.5f; // %
        internal const string Key = "RavenWatch_breed", WearKey = "r", None = "-";
        private const string Rpc = "RavenWatch_BreedSeen";

        // ---------------------------------------------------------------- the breeds

        // the breeds, looked up again only when a look is saved or edited (the files are read only then, not every call)
        private static List<Look> _all;
        private static int _allAt = -1;
        private static bool _allFiles;
        internal static List<Look> All()
        {
            if (_all != null && _allAt == RavenLooks.Version && _allFiles == RavenLooks.FromFiles) return _all;
            _allAt = RavenLooks.Version; _allFiles = RavenLooks.FromFiles;
            _all = new List<Look>();
            foreach (Look k in RavenLooks.AllLooks()) if (k.For == "breed") _all.Add(k);
            return _all;
        }

        internal static Look Find(string id)
        {
            if (string.IsNullOrEmpty(id) || id == None) return null;
            foreach (Look k in All()) if (k.Id == id) return k;
            return null;
        }

        internal static bool Legend(Look b) { return b != null && b.Tier == "legend"; }
        internal static string TierName(Look b) { return Legend(b) ? Lang.T("легендарний") : Lang.T("рідкісний"); }
        internal static string Hex(Look b) { return Legend(b) ? "#E0A020" : "#B8862A"; }

        // a new raven's breed: none most often; else one of its rarity, any alike
        internal static string Roll()
        {
            float r = UnityEngine.Random.value * 100f;
            string tier = r < LegendChance ? "legend" : r < LegendChance + RareChance ? "rare" : null;
            if (tier == null) return None;
            List<Look> pool = new List<Look>();
            foreach (Look b in All()) if ((b.Tier == "legend") == (tier == "legend")) pool.Add(b);
            return pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)].Id : None;
        }

        // the breed rolled for this raven ("" not rolled yet, None none); and the one it shows (revealed)
        internal static string Rolled(ItemDrop.ItemData item) { string v; return item != null && item.m_customData != null && item.m_customData.TryGetValue(Key, out v) ? v ?? "" : ""; }
        internal static Look Of(ItemDrop.ItemData item) { return item != null ? Find(RavenWear.Of(item).Breed) : null; }

        // ---------------------------------------------------------------- a breed over a rune of Freya

        private static readonly Dictionary<string, Look> Composed = new Dictionary<string, Look>();

        // what a raven of this breed wearing this rune look shows (either may be null); cached by both codes
        internal static Look Compose(Look rune, Look breed)
        {
            if (breed == null) return rune;
            if (rune == null) return Bare(breed);
            string key = rune.Code(false) + "\n#\n" + breed.Code(false);
            Look c;
            if (Composed.TryGetValue(key, out c)) return c;
            if (Composed.Count > 32) Composed.Clear();
            c = ComposeNew(rune, breed);
            Composed[key] = c;
            return c;
        }

        // the same, made anew (the test window: the breed being edited changes all the time)
        internal static Look ComposeNew(Look rune, Look breed)
        {
            if (breed == null) return rune;
            if (rune == null) return Bare(breed);
            Look c = rune.Copy();
            c.Id = rune.Id + "+" + breed.Id;
            for (int z = 1; z < c.Zones.Length; z++)
                if (breed.Keep[z] && breed.Zones[z].On) c.Zones[z] = Part(breed.Zones[z]);
            if (breed.KeepLight && breed.EyesOn) { c.EyesOn = true; c.Eyes = breed.Eyes; c.EyePower = breed.EyePower; }
            if (breed.KeepGlow && breed.GlowOn) { c.GlowOn = true; c.Glow = breed.Glow; c.GlowPower = breed.GlowPower; }
            if (breed.KeepSheen && breed.SheenOn) { c.SheenOn = true; c.Sheen = breed.Sheen; c.Metal = breed.Metal; c.MetalGloss = breed.MetalGloss; c.Gloss = breed.Gloss; }
            if (breed.KeepSparks && breed.SparksOn) { c.SparksOn = true; c.Sparks = breed.Sparks; c.SparkPower = breed.SparkPower; c.SparkSize = breed.SparkSize; c.SparkEdge = breed.SparkEdge; }
            c.OddEyeOn = breed.OddEyeOn; c.OddEye = breed.OddEye; c.OddEyeSize = breed.OddEyeSize; // (one eye: always its own)
            c.Fx.Clear(); c.Things.Clear(); // (the breed's own: hung by RavenWear for any rune)
            return c;
        }

        // the breed's paint alone (its effects and things are hung apart)
        private static Look Bare(Look breed)
        {
            if (breed.Fx.Count == 0 && breed.Things.Count == 0) return breed;
            Look c = breed.Copy();
            c.Fx.Clear(); c.Things.Clear();
            return c;
        }

        private static Look.Part Part(Look.Part p) { return new Look.Part { On = p.On, Dark = p.Dark, Light = p.Light, Mix = p.Mix, Relief = p.Relief }; }

        // ---------------------------------------------------------------- rolled and revealed (the owner's game)

        private static float _next;

        // every two seconds (RavenSkills): every raven of yours gets its roll once; an awake one of a breed
        // shows it
        internal static void Tick(Player p)
        {
            if (p == null || Time.time < _next) return;
            _next = Time.time + 2f;
            Inventory inv = p.GetInventory();
            if (inv == null) return;
            // the bag and the shoulders: with SeneaL UI's shoulder cells a seated raven is not in the bag at all
            List<ItemDrop.ItemData> all = new List<ItemDrop.ItemData>(inv.GetAllItems());
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p)) if (!all.Contains(item)) all.Add(item);
            foreach (ItemDrop.ItemData item in all)
            {
                bool munin;
                if (!RavenItem.IsRaven(item, out munin)) continue;
                string id = Rolled(item);
                if (id.Length == 0) { id = Roll(); item.m_customData[Key] = id; if (id != None) Log("Breeds: " + Plugin.RavenName(munin) + " rolled «" + id + "» (hidden until it wakes)"); }
                if (id == None || !Trust.Active(item) || Trust.Stranger(item, p)) continue;
                Wear w = RavenWear.Of(item);
                if (w.Breed == id) continue;
                Look b = Find(id);
                if (b == null) continue; // (a breed of another version: kept, shown when it is there)
                w.Breed = id;
                RavenWear.Rewrite(item, w);
                Reveal(p, item, munin, b);
            }
        }

        private static void Reveal(Player p, ItemDrop.ItemData item, bool munin, Look b)
        {
            string said = Lang.F("{0} — {1} ворон: <color={2}>{3}</color>!", Plugin.RavenName(munin), TierName(b), Hex(b), Lang.T(b.Name));
            RavenMsg.Center(p, said);
            p.Message(MessageHud.MessageType.TopLeft, said);
            RavenFx.Play("vfx_offering", p.transform.position + Vector3.up * 1.6f, Quaternion.identity);
            ScreamFx.CawAll(p, munin, munin ? 0.9f : 1.3f, 0.7f);
            Log("Breeds: " + Plugin.RavenName(munin) + " revealed «" + b.Id + "»");
            BreedTraits.Revealed(item, munin, b.Id); // (its own word on it)
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetPlayerName());
            pkg.Write(p.transform.position);
            pkg.Write(munin);
            pkg.Write(b.Id);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnSeen));
        }

        // another player's raven showed its breed: told to those near
        private static void OnSeen(long sender, ZPackage pkg)
        {
            string who = pkg.ReadString();
            Vector3 at = pkg.ReadVector3();
            bool munin = pkg.ReadBool();
            Look b = Find(pkg.ReadString());
            Player me = Player.m_localPlayer;
            if (me == null || b == null || sender == ZDOMan.GetSessionID() || Vector3.Distance(me.transform.position, at) > 100f) return;
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("У гравця {0} прокинувся {1} ворон: <color={2}>{3}</color> ({4})", who, TierName(b), Hex(b), Lang.T(b.Name), Plugin.RavenName(munin)));
            BreedTraits.Seen(Lang.T(b.Name)); // (one of yours has a word on it)
        }

        // ---------------------------------------------------------------- the book: the knack's card

        // the lines added to the knack's tip (null: no breed)
        internal static string Tip(ItemDrop.ItemData item)
        {
            Look b = Of(item);
            if (b == null) return null;
            string t = "<color=" + Hex(b) + "><b>" + Lang.F("Унікальний ворон: {0}", Lang.T(b.Name)) + "</b> (" + TierName(b) + ")</color>";
            if (b.Desc.Length > 0) t += "\n" + Lang.T(b.Desc);
            string trait = BreedTraits.Text(b.Id);
            if (!string.IsNullOrEmpty(trait)) t += "\n" + Lang.T("Риса породи:") + " " + trait;
            return t;
        }

        // ---------------------------------------------------------------- the console: raven breed [hugin|munin] [<id>|roll|none]
        internal static string Cmd(Player p, string[] a)
        {
            int at = 1;
            bool munin = false;
            if (a.Length > at && (a[at].ToLowerInvariant() == "munin" || a[at].ToLowerInvariant() == "hugin")) { munin = a[at].ToLowerInvariant() == "munin"; at++; }
            ItemDrop.ItemData item = Trust.Worn(munin);
            List<string> ids = new List<string>();
            foreach (Look b in All()) ids.Add(b.Id + (Legend(b) ? "*" : ""));
            if (item == null) return Lang.F("{0} не на плечі. Породи: {1}", Plugin.RavenName(munin), string.Join(", ", ids.ToArray()));
            if (a.Length <= at)
                return Lang.F("{0}: кинуто «{1}», видно «{2}». raven breed [hugin|munin] [id]|roll|none. Породи (* легендарні): {3}", Plugin.RavenName(munin), Rolled(item), RavenWear.Of(item).Breed, string.Join(", ", ids.ToArray()));
            string want = a[at].ToLowerInvariant();
            if (want == "roll") want = Roll();
            else if (want == "none") want = None;
            else if (Find(want) == null) return Lang.F("Нема породи «{0}». Є: {1}", want, string.Join(", ", ids.ToArray()));
            item.m_customData[Key] = want;
            Wear w = RavenWear.Of(item);
            w.Breed = "";
            RavenWear.Rewrite(item, w);
            _next = 0f; // (revealed at the next look, as a raven waking would be)
            return Lang.F("{0}: порода «{1}».", Plugin.RavenName(munin), want);
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }
    }

    // A breed's trait (on top of the knack; the user, 2026-10-27). Only a revealed breed works (rolled, and the raven
    // awake for its owner). Each is a small multiplier read where the thing itself is reckoned, two are senses of their
    // own (Tick), and each breed has words of its own (BreedPatterns):
    //   Альбінос    «Біла ворона»          — bonds with other players' ravens move BondMul times faster, both ways (Bonds.Change)
    //   Іржавий     «Чує залізо»           — now and then marks an ore deposit near you on the map (Tick → Ore)
    //   Сивий       «Старий мудрець»       — trust grows TrustMulGrey faster (Trust.GainMul)
    //   Строкатий   «Сорочий нахил»        — finds LootMul more often, treasures RareMul more often (RavenLoot)
    //   Золотоокий  «Бачить обома світами» — caws at a foe coming at you from behind (Tick → Behind) (its id br_riznookyi: its name before)
    //   Зоряний     «Нічний птах»          — at night strikes NightStrikePct harder (Strike.Blow) and flies NightWingMul faster (Wing.Now)
    //   Примарний   «Між світами»          — stays on your shoulder after your death LoyalMul more often (Loyal), loses DeathMul of the trust a death takes (Trust.OnDeath)
    //   Жаровий     «Тліюче пір'я»         — FireShare of its blow is fire (Strike.Launch), never cold (Quirks, the cold moments)
    internal static class BreedTraits
    {
        internal const string Albino = "br_albinos", Rusty = "br_irzhavyi", Grey = "br_syvyi", Pied = "br_strokatyi",
            OddEye = "br_riznookyi", Star = "br_zorianyi", Ghost = "br_pryzrachnyi", Ember = "br_zharovyi";
        internal static float BondMul = 1.5f, TrustMulGrey = 1.15f, LootMul = 1.5f, RareMul = 2f, NightStrikePct = 15f, NightWingMul = 1.1f,
            LoyalMul = 2f, DeathMul = 0.5f, FireShare = 0.2f;
        internal static float OreRadius = 50f, OreEveryMinutes = 8f, OrePinMinutes = 5f, OreMoveAgain = 40f; // «Чує залізо»
        internal static float BehindRadius = 25f, BehindAngle = 110f, BehindEvery = 40f;   // «Бачить обома світами»: how near, how far round from where you look, a pause
        internal static float BoastPauseMinutes = 25f, BoastChance = 0.35f;              // its own word now and then

        // the breed this raven shows as its trait: revealed only (rolled, and awake for its owner); a stranger's raven gives nothing
        internal static bool Is(ItemDrop.ItemData item, string id) { return item != null && Breeds.Rolled(item) == id && Trust.Active(item) && !Trust.Stranger(item, Player.m_localPlayer); }

        // ---------------------------------------------------------------- the numbers, where they are reckoned
        internal static float BondMulOf(ItemDrop.ItemData item) { return Is(item, Albino) ? BondMul : 1f; }
        internal static float TrustMul(ItemDrop.ItemData item) { return Is(item, Grey) ? TrustMulGrey : 1f; }
        internal static float LootMulOf(ItemDrop.ItemData item) { return Is(item, Pied) ? LootMul : 1f; }
        internal static float RareMulOf(ItemDrop.ItemData item) { return Is(item, Pied) ? RareMul : 1f; }
        internal static float StrikePct(ItemDrop.ItemData item) { return Is(item, Star) && EnvMan.instance != null && EnvMan.IsNight() ? NightStrikePct : 0f; }
        internal static float WingMul(ItemDrop.ItemData item) { return Is(item, Star) && EnvMan.instance != null && EnvMan.IsNight() ? NightWingMul : 1f; }
        internal static float LoyalMulOf(ItemDrop.ItemData item) { return Is(item, Ghost) ? LoyalMul : 1f; }
        internal static float DeathMulOf(ItemDrop.ItemData item) { return Is(item, Ghost) ? DeathMul : 1f; }
        internal static float FireShareOf(ItemDrop.ItemData item) { return Is(item, Ember) ? FireShare : 0f; }
        internal static bool ColdProof(ItemDrop.ItemData item) { return Is(item, Ember); }

        // the strike's tooltip (Strike.Why)
        internal static string WhyLine(ItemDrop.ItemData item)
        {
            float v = StrikePct(item);
            return v > 0f ? Lang.F("+ «Нічний птах» (ніч): +{0}%\n", Mathf.RoundToInt(v)) : "";
        }

        // the trait in the book (the knack's tip): what it does
        internal static string Text(string id)
        {
            switch (id)
            {
                case Albino: return Lang.T("«Біла ворона»: чужі ворони звикають до нього швидше — дружба й ворожнеча міцніють у півтора раза швидше.");
                case Rusty: return Lang.T("«Чує залізо»: раз на кілька хвилин помічає родовище руди поруч і позначає його на мапі.");
                case Grey: return Lang.T("«Старий мудрець»: довіра росте на 15% швидше.");
                case Pied: return Lang.T("«Сорочий нахил»: приносить здобич у півтора раза частіше, а скарби — удвічі частіше.");
                case OddEye: return Lang.T("«Бачить обома світами»: каркає, коли ворог підкрадається до тебе ззаду.");
                case Star: return Lang.T("«Нічний птах»: уночі б'є на 15% сильніше й літає на 10% швидше.");
                case Ghost: return Lang.T("«Між світами»: після твоєї смерті лишається на плечі вдвічі частіше, а довіри втрачає вдвічі менше.");
                case Ember: return Lang.T("«Тліюче пір'я»: частина удару — вогонь, що підпалює ціль; ніколи не мерзне.");
            }
            return null;
        }

        // its words' tag: breed_<what>_<the breed's id without «br_»>
        internal static string Tag(string what, string id) { return "breed_" + what + "_" + (id.StartsWith("br_") ? id.Substring(3) : id); }

        // ---------------------------------------------------------------- the senses and the words (RavenSkills, twice a second)

        private static readonly float[] NextOre = new float[2], NextBehind = new float[2], NextBoast = new float[2];
        private static readonly bool[] Both = { false, true };
        private static readonly List<Minimap.PinData> OrePins = new List<Minimap.PinData>();
        private static readonly List<Vector3> OreSeen = new List<Vector3>(); // the heaps already shown: not again
        private static readonly Dictionary<Character, float> Warned = new Dictionary<Character, float>(); // a foe warned of: not again for a while
        private static float _orePinsUntil;
        private static Vector3 _oreLookedAt = new Vector3(0f, -99999f, 0f); // where the last look found nothing new

        internal static void Tick(Player p)
        {
            if (p == null || p.IsDead()) return;
            if (OrePins.Count > 0 && Time.time >= _orePinsUntil) ClearOre();
            foreach (bool munin in Both)
            {
                ItemDrop.ItemData item = Trust.Worn(munin);
                if (item == null || !Trust.Active(item) || Trust.Away(item) || Trust.Stranger(item, p)) continue;
                string id = Breeds.Rolled(item);
                if (id.Length == 0 || id == Breeds.None) continue;
                int k = munin ? 1 : 0;
                if (id == Rusty && Time.time >= NextOre[k]) Ore(p, munin, k);
                if (id == OddEye && Time.time >= NextBehind[k]) Behind(p, munin, k);
                if (NextBoast[k] <= 0f) NextBoast[k] = Time.time + UnityEngine.Random.Range(180f, 600f); // (not at once on entering)
                if (Time.time >= NextBoast[k]) // (a roll a minute; PerkFx keeps the long pause after a roll)
                {
                    NextBoast[k] = Time.time + 60f;
                    if (!Senses.Fight && Director.May(Director.Voice.Shoulder)) PerkFx.Say(munin, Tag("boast", id), BoastChance, BoastPauseMinutes); // (idle talk: never over the shoulder's own limit)
                }
            }
        }

        // «Чує залізо»: an ore deposit near you, not shown before, marked on the map for a few minutes
        private static void Ore(Player p, bool munin, int k)
        {
            NextOre[k] = Time.time + 30f; // (a look every half a minute)
            if (Minimap.instance == null) return; // (not held back by foes near: the Black Forest always has some, and a pin hinders no fight)
            if ((p.transform.position - _oreLookedAt).sqrMagnitude < OreMoveAgain * OreMoveAgain) return; // (the scene scan is not cheap, and deposits do not move: again only well away from the last empty look)
            Vector3 at;
            ItemDrop ore;
            if (!RavenSkills.NearestOre(p.transform.position, OreRadius, OreSeen, out at, out ore)) { _oreLookedAt = p.transform.position; return; }
            _oreLookedAt = new Vector3(0f, -99999f, 0f);
            NextOre[k] = Time.time + OreEveryMinutes * 60f;
            OreSeen.Add(at);
            if (OreSeen.Count > 40) OreSeen.RemoveAt(0);
            string name = Lang.Game(ore.m_itemData.m_shared.m_name);
            OrePins.Add(RavenSkills.AddOrePin(at, ore, name));
            _orePinsUntil = Time.time + OrePinMinutes * 60f;
            ScreamFx.CawAll(p, munin, munin ? 0.95f : 1.25f, 0.6f);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} чує залізо: {1} поруч — позначено на мапі", Plugin.RavenName(munin), Lang.Lower(name)));
            PerkFx.Say(munin, "breed_ore", 0.8f, 0f, "ore", Lang.Lower(name));
            Log("Breeds: " + Plugin.RavenName(munin) + " smelt ore (" + ore.name + ") " + Mathf.RoundToInt(Vector3.Distance(p.transform.position, at)) + " m off");
        }

        private static void ClearOre()
        {
            if (Minimap.instance != null) foreach (Minimap.PinData pin in OrePins) if (pin != null) Minimap.instance.RemovePin(pin);
            OrePins.Clear();
        }

        // «Бачить обома світами»: a foe that is after you, near, and round behind where you look
        private static void Behind(Player p, bool munin, int k)
        {
            NextBehind[k] = Time.time + 0.5f;
            if (Warned.Count > 16) Warned.Clear();
            Vector3 look = p.GetLookDir(); look.y = 0f;
            if (look.sqrMagnitude < 0.01f) return;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == p || c.IsPlayer() || c.IsDead() || c.IsTamed() || c.IsBoss() || !BaseAI.IsEnemy(p, c)) continue;
                Vector3 d = c.transform.position - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude > BehindRadius * BehindRadius || d.sqrMagnitude < 9f) continue; // (at arm's length it is a fight already)
                if (Vector3.Angle(look, d) < BehindAngle) continue; // (you can see it)
                MonsterAI ai = c.GetBaseAI() as MonsterAI;
                if (ai == null || ai.GetTargetCreature() != p) continue; // (only one that is coming for you)
                float last;
                if (Warned.TryGetValue(c, out last) && Time.time - last < 180f) continue;
                Warned[c] = Time.time;
                NextBehind[k] = Time.time + BehindEvery;
                ScreamFx.CawAll(p, munin, munin ? 0.85f : 1.2f, 0.9f);
                p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0}: позаду — {1}!", Plugin.RavenName(munin), Lang.Game(c.m_name)));
                PerkFx.Say(munin, "breed_behind", 1f, 0f);
                Log("Breeds: " + Plugin.RavenName(munin) + " saw " + Utils.GetPrefabName(c.gameObject) + " behind you, " + Mathf.RoundToInt(d.magnitude) + " m");
                return;
            }
        }

        // the moment it is revealed: its own word, if it is the one on your shoulder then (not another of its kind)
        internal static void Revealed(ItemDrop.ItemData item, bool munin, string id) { if (ReferenceEquals(Trust.Worn(munin), item)) PerkFx.Say(munin, Tag("reveal", id), 1f, 0f); }

        // another player's raven of a breed woke near: one of yours has a word on it
        internal static void Seen(string name)
        {
            bool munin = Trust.Worn(false) == null || UnityEngine.Random.value < 0.5f && Trust.Worn(true) != null;
            PerkFx.Say(munin, "breed_seen", 0.7f, 10f, "breed", name);
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }
    }
}
#endif
