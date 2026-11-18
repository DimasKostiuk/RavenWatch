#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Ватажки»: the game's own creatures, made strong for an errand. The creature comes from the land the errand sends
    // you to (a greydwarf in the Meadows), its strength from where you are in the game (Stage): stars (level), and our
    // own health and damage factors on top, with 1–2 traits (affixes). Everything is in the creature's ZDO, so every
    // game shows it alike (name with stars, size) and whoever simulates it applies it (health, damage, traits):
    //   RavenWatch_champ = owner=<player id>;id=<errand stamp>;who=<prefab>;lvl=;hp=;dmg=;aff=fire+fast
    // It is spawned by the errand's owner when near the place (the zone is loaded there), found again on coming back,
    // and goes when its errand is done or given up. It is ours alone (ChampionOwn: no other mod's stars, health, blows
    // or traits), has its own name (Nick), its own bar (RavenBook.ChampionBar), its traits seen on it (ChampionLife.Auras),
    // and its own drop with its trophy and gold when its owner takes it down (ChampionDropPatch).
    internal static class Champions
    {
        internal const string Key = "RavenWatch_champ", PackKey = "RavenWatch_champPack", BornKey = "RavenWatch_champBorn", StruckKey = "RavenWatch_champStruck";
        internal static float OrphanDays = 5f; // game days: one whose owner stays away this long goes (its errand brings it again on return)
        internal const string RpcDead = "RavenWatch_ChampDead";
        internal static float SpawnNear = 70f;    // m: the zone's ring round its place (the step's map ring; in it, it appears)
        internal static float CreditRange = 100f; // m: its death counts for the errand only with its owner this near, alive, and only if the owner struck it (OwnerStruck)

        // the creatures of each land (by the land's tier: Meadows 0 … Ashlands 6, the Deep North 7); only those that walk on land
        private static readonly string[][] ByTier =
        {
            new[] { "Greydwarf", "Boar", "Neck", "Greyling" },
            new[] { "Greydwarf_Elite", "Greydwarf_Shaman", "Troll", "Skeleton" },
            new[] { "Draugr", "Draugr_Elite", "Blob", "Abomination" },
            new[] { "Wolf", "Fenring", "StoneGolem", "Ulv" },
            new[] { "Goblin", "GoblinBrute", "GoblinShaman", "Lox" },
            new[] { "Seeker", "SeekerBrute" },
            new[] { "Charred_Melee", "Charred_Archer", "Morgen", "Asksvin" },
            new[] { "JotunWarrior", "JotunWitch", "TrollFrost", "Skeleton_DeepNorth", "GoblinDeepNorth" },
        };
        // how strong a plain creature of each tier is, roughly (its health and blows together)
        private static readonly float[] TierPower = { 1f, 2.2f, 4f, 7f, 11f, 17f, 26f, 38f };
        internal static float Toughness = 4f;    // a champion is this many plain creatures of YOUR stage's land

        // the ordinary creatures of a tier this game has (the raven tree's arena)
        internal static List<string> Plain(int tier)
        {
            List<string> can = new List<string>();
            for (int t = Mathf.Clamp(tier, 0, ByTier.Length - 1); t >= 0 && can.Count == 0; t--)
                foreach (string pf in ByTier[t]) if (ZNetScene.instance != null && ZNetScene.instance.GetPrefab(pf) != null) can.Add(pf);
            return can;
        }

        internal static int TierOf(Heightmap.Biome b)
        {
            switch (b)
            {
                case Heightmap.Biome.Meadows: return 0;
                case Heightmap.Biome.BlackForest: return 1;
                case Heightmap.Biome.Swamp: return 2;
                case Heightmap.Biome.Mountain: return 3;
                case Heightmap.Biome.Plains: return 4;
                case Heightmap.Biome.Mistlands: return 5;
                case Heightmap.Biome.AshLands: return 6;
                case Heightmap.Biome.DeepNorth: return 7;
                default: return 0;
            }
        }

        // ---------------------------------------------------------------- what one is
        internal sealed class Mods
        {
            public long Owner; public string Id = "", Who = "", Title = ""; // Title: its own name (a lord's), a translation key
            public int Level = 2; public float Hp = 1f, Dmg = 1f;
            public bool Spent; // one risen again after a kill not counted: no gold, no sure trophy (no farming it)
            public readonly List<string> Aff = new List<string>();

            internal bool Has(string a) { return Aff.Contains(a); }

            internal string Code()
            {
                return "owner=" + Owner + ";id=" + Id + ";who=" + Who + ";lvl=" + Level + ";hp=" + Hp.ToString("0.##", CultureInfo.InvariantCulture)
                    + ";dmg=" + Dmg.ToString("0.##", CultureInfo.InvariantCulture) + ";aff=" + string.Join("+", Aff.ToArray()) + (Title.Length > 0 ? ";t=" + Title : "") + (Spent ? ";sp=1" : "");
            }

            internal static Mods Parse(string s)
            {
                if (string.IsNullOrEmpty(s)) return null;
                Mods m = new Mods();
                foreach (string part in s.Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                    switch (k)
                    {
                        case "owner": long.TryParse(v, out m.Owner); break;
                        case "id": m.Id = v; break;
                        case "who": m.Who = v; break;
                        case "lvl": m.Level = Mathf.Clamp(Errands.I(v, 2), 1, 10); break;
                        case "hp": m.Hp = Mathf.Max(0.1f, Errands.F(v, 1f)); break;
                        case "dmg": m.Dmg = Mathf.Max(0.1f, Errands.F(v, 1f)); break;
                        case "aff": foreach (string a in v.Split('+')) if (a.Length > 0) m.Aff.Add(a); break;
                        case "t": m.Title = v; break;
                        case "sp": m.Spent = v == "1"; break;
                    }
                }
                return m;
            }
        }

        // one for this place, at the player's stage: its creature, stars, factors and traits (the slot's value keeps it).
        // force: this creature (its own land's strength counted); lord: a guardian — three times the health, harder
        // blows, two traits, a boss's bar and size (ChampionLife)
        internal static Mods Roll(Player p, Vector3 at) { return Roll(p, at, "", false); }

        internal static Mods Roll(Player p, Vector3 at, string force, bool lord)
        {
            int tier = WorldGenerator.instance != null ? TierOf(WorldGenerator.instance.GetBiome(at.x, at.z)) : 0;
            List<string> can = new List<string>();
            if (!string.IsNullOrEmpty(force) && ZNetScene.instance != null && ZNetScene.instance.GetPrefab(force) != null)
            {
                can.Add(force);
                for (int t = 0; t < ByTier.Length; t++) if (Array.IndexOf(ByTier[t], force) >= 0) { tier = t; break; }
            }
            for (int t = tier; t >= 0 && can.Count == 0; t--)
                foreach (string pf in ByTier[t]) if (ZNetScene.instance != null && ZNetScene.instance.GetPrefab(pf) != null) can.Add(pf);
            if (can.Count == 0) return null;
            Mods m = new Mods { Who = can[UnityEngine.Random.Range(0, can.Count)] };
            int stage = Mathf.Clamp(Stage.Of(p), 0, TierPower.Length - 1);
            float r = Mathf.Max(1f, Toughness * TierPower[stage] / TierPower[tier]); // how many plain ones of this land it is worth
            m.Level = Mathf.Clamp(Mathf.RoundToInt(1f + Mathf.Log(r, 2f)), 2, 6);
            m.Hp = Mathf.Max(1f, r / m.Level);
            float blow = Mathf.Sqrt(r) * 1.5f;
            m.Dmg = Mathf.Max(1f, blow / (1f + 0.5f * (m.Level - 1)));
            List<string> pool = new List<string>(Affixes.Keys);
            int n = stage >= 3 || lord ? 2 : 1;
            if (lord) pool.Remove("pack"); // (a lord stands alone)
            for (int i = 0; i < n && pool.Count > 0; i++) { string a = pool[UnityEngine.Random.Range(0, pool.Count)]; pool.Remove(a); m.Aff.Add(a); }
            if (lord) { m.Aff.Add("lord"); m.Level = Mathf.Max(m.Level, 3); m.Hp *= 3f; } // (its blows as a champion's: three stars already hit hard)
            return m;
        }

        // the champions seen in this game (their ChampionLife): what the hot patches ask, without reading the ZDO
        private static readonly Dictionary<Character, ChampionLife> Known = new Dictionary<Character, ChampionLife>();
        internal static void Know(Character c, ChampionLife l) { if (c != null) Known[c] = l; }
        internal static void Forget(Character c) { if (c != null) Known.Remove(c); }
        internal static ChampionLife Life(Character c) { ChampionLife l; return c != null && Known.TryGetValue(c, out l) && l != null ? l : null; }
        internal static Mods Fast(Character c) { ChampionLife l = Life(c); return l != null ? l.M : null; }
        // a boss of the game, not an errand's lord (a lord carries the boss flag: the ravens' gifts treat it as one; the diary does not)
        internal static bool TrueBoss(Character c) { return c != null && c.IsBoss() && Fast(c) == null; }
        internal static bool Any { get { return Known.Count > 0; } } // (the hot patches: nothing to do while no champion is loaded)

        // the nearest champion alive within this reach of a place (its bar), or null
        internal static Character Nearest(Vector3 at, float reach)
        {
            Character best = null;
            float bestD = reach * reach;
            foreach (KeyValuePair<Character, ChampionLife> kv in Known)
            {
                Character c = kv.Key;
                if (c == null || c.IsDead()) continue;
                float d = (c.transform.position - at).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        // its errand's owner alive and within CreditRange of this place (in any game: the owner's ZDO says who it is)
        internal static bool OwnerNear(Mods m, Vector3 at, bool alive = true)
        {
            foreach (Player o in Player.GetAllPlayers())
                if (o != null && (!alive || !o.IsDead()) && o.GetPlayerID() == m.Owner && (o.transform.position - at).sqrMagnitude <= CreditRange * CreditRange) return true;
            return false;
        }

        // a player's blow reached it (RPC_Damage, where it is simulated): its owner's first one is written once in its ZDO,
        // so a change of who simulates it loses nothing; you or your raven, one blow is enough
        internal static void Struck(Character c, Player by)
        {
            Mods m = Any ? Fast(c) : null;
            if (m == null || by.GetPlayerID() != m.Owner) return;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid() && nv.IsOwner() && !nv.GetZDO().GetBool(StruckKey)) nv.GetZDO().Set(StruckKey, true);
        }

        internal static bool OwnerStruck(Character c)
        {
            ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
            return nv != null && nv.IsValid() && nv.GetZDO().GetBool(StruckKey);
        }

        // from its ZDO (when it wakes; its health): the ZDO read and parsed
        internal static Mods Of(Character c)
        {
            if (c == null || c is Player) return null;
            ZNetView nv = c.GetComponent<ZNetView>();
            ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            return z != null ? Mods.Parse(z.GetString(Key, "")) : null;
        }

        // ---------------------------------------------------------------- traits
        internal static readonly Dictionary<string, string> Affixes = new Dictionary<string, string>
        {
            { "fire", Lang.K("вогняний") }, { "frost", Lang.K("крижаний") }, { "fast", Lang.K("швидкий") },
            { "tough", Lang.K("броньований") }, { "regen", Lang.K("живучий") }, { "pack", Lang.K("очільник зграї") },
        };

        internal static string Words(Mods m)
        {
            List<string> l = new List<string>();
            foreach (string a in m.Aff) { string w; if (Affixes.TryGetValue(a, out w)) l.Add(Lang.T(w)); }
            return string.Join(", ", l.ToArray());
        }

        // its own name (the user, 2026-10-21: «Гнилозуб», «Сивогрив»): two halves by what it is — its creature, stars and
        // traits (never by chance: the book before it rises, every game and its bar say the same), each half its own
        // translation (Rot + fang); a lord's title from its template wins
        internal static readonly string[] NickHead = Lang.Keys("Крово", "Кісто", "Гнило", "Чорно", "Сиво", "Криво", "Залізно", "Мерзло", "Попело", "Гостро", "Рвано", "Сліпо");
        internal static readonly string[] NickTail = Lang.Keys("зуб", "грив", "шкур", "гриз", "рик", "пазур", "хвіст", "лом", "пив", "вий", "лап", "різ");

        internal static string Nick(Mods m)
        {
            if (m.Title.Length > 0) return Lang.T(m.Title);
            int h = (m.Who + "|" + m.Level + "|" + m.Hp.ToString("0.##", CultureInfo.InvariantCulture) + "|" + string.Join("+", m.Aff.ToArray())).GetStableHashCode() & 0x7fffffff;
            return Lang.T(NickHead[h % NickHead.Length]) + Lang.T(NickTail[(h / NickHead.Length) % NickTail.Length]);
        }

        internal static string Stars(Mods m) { return new string('★', Mathf.Max(0, m.Level - 1)); }

        // «Гнилозуб, ватажок ★★★» (a lord: «охоронець»)
        internal static string Name(Mods m)
        {
            return Nick(m) + ", " + (m.Has("lord") ? Lang.T("охоронець") : Lang.T("ватажок")) + " " + Stars(m);
        }

        // ---------------------------------------------------------------- in the world
        // the errand's owner near its place: the one it already has (found), or a new one
        internal static Character Ensure(Player p, string stamp, Vector3 at, Mods m)
        {
            Character have = Find(stamp, p != null ? p.GetPlayerID() : 0L);
            if (have != null) return have;
            if (p == null || ZNetScene.instance == null || !ZNetScene.instance.IsAreaReady(at)) return null;
            GameObject prefab = ZNetScene.instance.GetPrefab(m.Who);
            if (prefab == null) { Errands.Log("champion: no creature " + m.Who); return null; }
            Vector3 pos = at;
            if (ZoneSystem.instance != null) pos.y = ZoneSystem.instance.GetGroundHeight(at) + 0.5f;
            m.Owner = p.GetPlayerID(); m.Id = stamp; m.Spent = WasSpent(p, stamp);
            GameObject go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            Character c = go.GetComponent<Character>();
            ZNetView nv = go.GetComponent<ZNetView>();
            if (c == null || nv == null || !nv.IsValid()) { UnityEngine.Object.Destroy(go); return null; }
            nv.GetZDO().Set(Key, m.Code());
            if (ZNet.instance != null) nv.GetZDO().Set(BornKey, (long)ZNet.instance.GetTimeSeconds());
            ChampionOwn.Claim(nv.GetZDO());         // (other creature mods: hands off, before its stars are set)
            c.SetLevel(m.Level);                    // (its health: ChampionMaxHealthPatch, with our factor)
            c.SetHealth(c.GetMaxHealth());
            ChampionLife.Put(c);
            Errands.Log("champion " + m.Code() + " at " + Mathf.RoundToInt(pos.x) + " " + Mathf.RoundToInt(pos.z) + ", health " + Mathf.RoundToInt(c.GetMaxHealth()));
            return c;
        }

        // this errand's champion among the creatures loaded around you, however far it chased you from its place
        internal static Character Find(string stamp, long owner)
        {
            foreach (KeyValuePair<Character, ChampionLife> kv in Known) // (only the champions: not every creature's ZDO)
            {
                Character c = kv.Key;
                if (c == null || kv.Value == null || c.IsDead()) continue;
                Mods m = kv.Value.M;
                if (m != null && m.Id == stamp && m.Owner == owner) return c;
            }
            return null;
        }

        // died (where it was simulated): everyone is told, the errand's owner counts it
        internal static void Died(Character c)
        {
            Mods m = Of(c);
            if (m == null || ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(m.Owner);
            pkg.Write(m.Id);
            pkg.Write(c.transform.position);
            pkg.Write(OwnerStruck(c));
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcDead, pkg);
        }


        // a lord counted as a boss in the world (the game's «active bosses»: no portals while one fights, in some worlds)
        // taken away without dying: counted off as the game does on a boss's death
        internal static void Uncount(Character c, ZNetView nv)
        {
            if (c == null || !c.IsBoss() || nv == null || !nv.IsValid() || !nv.GetZDO().GetBool(ZDOVars.s_bossCount) || ZoneSystem.instance == null) return;
            float v;
            ZoneSystem.instance.GetGlobalKey(GlobalKeys.activeBosses, out v);
            ZoneSystem.instance.SetGlobalKey(GlobalKeys.activeBosses, Mathf.Max(0f, v - 1f));
            nv.GetZDO().Set(ZDOVars.s_bossCount, false);
        }

        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(RpcDead, new Action<long, ZPackage>(OnDead)); }

        // counted only with the errand's owner in the fight: alive, near where it fell, and with a blow of their own in it
        // (friends may help, not do it for you)
        private static void OnDead(long sender, ZPackage pkg)
        {
            long owner = pkg.ReadLong();
            string id = pkg.ReadString();
            Player p = Player.m_localPlayer;
            if (p == null || p.GetPlayerID() != owner || pkg.GetPos() >= pkg.Size()) return;
            Vector3 at = pkg.ReadVector3();
            bool struck = pkg.GetPos() >= pkg.Size() || pkg.ReadBool(); // (an older game does not say)
            float dist = Vector3.Distance(p.transform.position, at);
            if (p.IsDead() || dist > CreditRange)
            {
                // not counted: on the way back into its ring the errand brings another one
                Errands.Log("champion " + id + " killed " + Mathf.RoundToInt(dist) + " m from you" + (p.IsDead() ? " (you were dead)" : "") + ": not counted");
                if (Errands.Wants(p, id)) RavenMsg.Center(p, Lang.T("Ватажок упав без тебе — Одін зараховує, лише коли ти жив{ий|а} поруч. Повернись у позначене коло: там з'явиться інший, але вже без золота."));
                Spend(p, id);
                return;
            }
            if (!struck)
            {
                Errands.Log("champion " + id + " killed with no blow of yours: not counted");
                if (Errands.Wants(p, id)) RavenMsg.Center(p, Lang.T("Ватажка вбили без твого удару — Одін зараховує, лише коли ти сам{|а} б'єшся з ним. У позначеному колі з'явиться інший, але вже без золота."));
                Spend(p, id);
                return;
            }
            Errands.OnChampion(id);
            Nicks.OnChampion(p); // a deed for the ravens' nicknames
#if RAVEN_STORY
            StoryBus.Raise("champion", id, 0);
#endif
        }

        // the errands whose champion fell once without counting (player customData, the last few): the next is Spent
        internal const string SpentKey = "RavenWatch_champSpent";

        private static void Spend(Player p, string id)
        {
            string raw;
            if (!p.m_customData.TryGetValue(SpentKey, out raw)) raw = "";
            if (("|" + raw + "|").Contains("|" + id + "|")) return;
            List<string> l = new List<string>(raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            l.Add(id);
            while (l.Count > 6) l.RemoveAt(0);
            p.m_customData[SpentKey] = string.Join("|", l.ToArray());
        }

        internal static bool WasSpent(Player p, string id)
        {
            string raw;
            return p != null && p.m_customData.TryGetValue(SpentKey, out raw) && ("|" + raw + "|").Contains("|" + id + "|");
        }

    }

    // on a champion, in every game: its size; where it is simulated: its traits that act by themselves, and it goes
    // when its errand is over (seen by the errand's owner)
    internal sealed class ChampionLife : MonoBehaviour
    {
        private Character _c;
        private ZNetView _nv;
        private Champions.Mods _m;
        private float _lastHurt, _lastHealth, _next, _nextHeal;
        private string _name;
        internal Champions.Mods M { get { return _m; } }

        // its name over the bar, made once (the words of this game's language)
        internal string Name
        {
            get
            {
                if (_name != null) return _name;
                string w = Champions.Words(_m);
                _name = "<color=#FFB040>" + Champions.Name(_m) + "</color>" + (w.Length > 0 ? " · " + w : "");
                return _name;
            }
        }

        private void OnDestroy() { Champions.Forget(_c); }

        // its traits seen on it, in every game: each its own effect at its middle (the game's burning and frost, our runes,
        // smoke and streak tinted), gone with it
        private static readonly Dictionary<string, KeyValuePair<string, string>> AuraOf = new Dictionary<string, KeyValuePair<string, string>>
        {
            { "fire", new KeyValuePair<string, string>("game:vfx_Burning", "") }, { "frost", new KeyValuePair<string, string>("game:vfx_Frost", "") },
            { "regen", new KeyValuePair<string, string>("ours:runes", "7CFF7A") }, { "tough", new KeyValuePair<string, string>("ours:runes", "B8C2CC") },
            { "fast", new KeyValuePair<string, string>("ours:streak", "CFE8FF") }, { "pack", new KeyValuePair<string, string>("ours:smoke", "7A1E1E") },
            { "lord", new KeyValuePair<string, string>("ours:runes", "FFC860") },
        };

        private void Auras()
        {
            try
            {
                Vector3 middle = transform.InverseTransformPoint(_c.GetCenterPoint());
                foreach (string a in _m.Aff)
                {
                    KeyValuePair<string, string> fx;
                    if (!AuraOf.TryGetValue(a, out fx)) continue;
                    GameObject tpl = FxLib.Template(fx.Key);
                    if (tpl == null) continue;
                    GameObject g = UnityEngine.Object.Instantiate(tpl, transform, false);
                    g.name = "RavenWatch_ChampionAura_" + a; g.hideFlags = HideFlags.None;
                    g.transform.localPosition = middle;
                    foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true)) { ParticleSystem.MainModule mm = ps.main; mm.loop = true; } // (a one-shot of the game's goes on)
                    g.SetActive(true);
                    Color tint;
                    if (fx.Value.Length > 0 && ColorUtility.TryParseHtmlString("#" + fx.Value, out tint)) g.AddComponent<WsFx>().Recolor(true, tint);
                }
            }
            catch (Exception e) { Errands.Log("champion aura: " + e.Message); }
        }

        internal static void Put(Character c)
        {
            if (c == null || c.GetComponent<ChampionLife>() != null) return;
            Champions.Mods m = Champions.Of(c);
            if (m == null) return;
            ChampionLife l = c.gameObject.AddComponent<ChampionLife>();
            l._c = c; l._nv = c.GetComponent<ZNetView>(); l._m = m;
            Champions.Know(c, l);
            c.transform.localScale *= m.Has("lord") ? 1.6f : 1.2f;
            if (m.Has("lord")) c.m_boss = true; // (each game on its own copy) the ravens' gifts treat it as a boss: no fear, no decoy, boss marks; the game counts it among active bosses (Uncount) — the diary does not (TrueBoss)
            if (m.Has("fast")) { c.m_speed *= 1.35f; c.m_runSpeed *= 1.35f; c.m_walkSpeed *= 1.2f; }
            l._lastHealth = c.GetHealth();
            l.Auras();
        }

        private float _nextOwn, _nextOrphan;

        private void Update()
        {
            if (_c == null || _nv == null || !_nv.IsValid() || _c.IsDead()) return;
            if (Time.time >= _nextOwn) { _nextOwn = Time.time + 1f; ChampionOwn.Keep(_c, _m); } // (ours alone: stars, health, no other mods' traits)
            if (Time.time >= _next)
            {
                _next = Time.time + 5f;
                Player me = Player.m_localPlayer;
                if (me != null && me.GetPlayerID() == _m.Owner && !Errands.Wants(me, _m.Id)) // its errand is over: it goes
                {
                    Errands.Log("champion of a finished errand gone: " + _m.Who);
                    if (!_nv.IsOwner()) _nv.ClaimOwnership();
                    Champions.Uncount(_c, _nv);
                    ZNetScene.instance.Destroy(gameObject);
                    return;
                }
            }
            if (!_nv.IsOwner()) return; // its traits act where it is simulated
            if (Time.time >= _nextOrphan) // an errand dropped far away, a character gone: whoever holds it lets it go
            {
                _nextOrphan = Time.time + 30f;
                long born = _nv.GetZDO().GetLong(Champions.BornKey, 0L);
                if (born > 0L && ZNet.instance != null && ZNet.instance.GetTimeSeconds() - born > Champions.OrphanDays * (double)Power.GameDaySeconds() && !Champions.OwnerNear(_m, transform.position, false)) // (an owner it just killed is still there)
                {
                    Errands.Log("champion left without its owner gone: " + _m.Who);
                    Champions.Uncount(_c, _nv);
                    ZNetScene.instance.Destroy(gameObject);
                    return;
                }
            }
            float hp = _c.GetHealth(), max = _c.GetMaxHealth();
            if (hp < _lastHealth - 0.01f) _lastHurt = Time.time;
            _lastHealth = hp;
            if (_m.Has("regen") && Time.time - _lastHurt > 8f && hp < max && Time.time >= _nextHeal) { _nextHeal = Time.time + 1f; _c.Heal(max * 0.02f, false); } // 2% a second, in one write a second (each heal is a health write on its ZDO)
            if (_m.Has("pack") && hp < max * 0.5f && !_nv.GetZDO().GetBool(Champions.PackKey, false))
            {
                _nv.GetZDO().Set(Champions.PackKey, true);
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(_m.Who) : null;
                if (prefab != null)
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 at = transform.position + Quaternion.Euler(0f, i * 180f + 90f, 0f) * Vector3.forward * 3f;
                        UnityEngine.Object.Instantiate(prefab, at, transform.rotation);
                    }
            }
        }
    }

    // ---------------------------------------------------------------- the game's numbers, for champions
    [HarmonyPatch(typeof(Character), "Awake")]
    internal static class ChampionAwakePatch
    {
        private static void Postfix(Character __instance) {
            try
            {
                ChampionLife.Put(__instance);
#if RAVEN_NPC
                NpcFade.Creature(__instance); // (a creature marked a ghost: painted see-through as it comes into this game)
#endif
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionAwakePatch", pgx); }
        }
    }

    // its health: the game's base × its stars × our factor — whoever sets its maximum (the game on its stars, another
    // mod scaling every creature), it ends as ours, and as wounded as it was (a share of the old maximum)
    [HarmonyPatch(typeof(Character), "SetMaxHealth")]
    internal static class ChampionMaxHealthPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Character __instance, out float __state)
        {
            __state = -1f;
            try
            {
                if (__instance is Player || Champions.Of(__instance) == null) return;
                float max = __instance.GetMaxHealth();
                __state = max > 0f ? Mathf.Clamp01(__instance.GetHealth() / max) : 1f;
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionMaxHealthPatch", pgx); }
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Character __instance, float __state)
        {
            try
            {
                if (__state < 0f) return;
                Champions.Mods m = Champions.Of(__instance); if (m != null) ChampionOwn.Health(__instance, m, __state);
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionMaxHealthPatch", pgx); }
        }
    }

    // its blows: the game's own per star (+50% a star) and our factor — not another mod's stars
    [HarmonyPatch(typeof(Attack), "GetLevelDamageFactor")]
    internal static class ChampionDamagePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Humanoid ___m_character, ref float __result)
        {
            try
            {
                if (!Champions.Any) return;
                Champions.Mods m = Champions.Fast(___m_character);
                if (m != null) __result = (1f + 0.5f * Mathf.Max(0, m.Level - 1)) * m.Dmg;
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionDamagePatch", pgx); }
        }
    }

    // its stars set (by the game, or by another mod giving stars of its own): back to ours, others' traits off
    [HarmonyPatch(typeof(Character), "SetLevel")]
    internal static class ChampionLevelPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Character __instance)
        {
            try
            {
                if (__instance is Player) return;
                Champions.Mods m = Champions.Of(__instance); if (m != null) ChampionOwn.Keep(__instance, m);
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionLevelPatch", pgx); }
        }
    }

    // its armour: the game's own (a creature has none) — not a mod's armour for every creature
    [HarmonyPatch(typeof(Character), "GetBodyArmor")]
    internal static class ChampionArmorPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Character __instance, ref float __result)
        {
            try
            {
                if (!Champions.Any) return;
                if (Champions.Fast(__instance) != null) __result = 0f;
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionArmorPatch", pgx); }
        }
    }

    // its blows where they land: a mod that makes every creature hit harder (Path of Valheiman's biome ladder) has
    // already scaled the hit — taken back off, so only ours stays
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class ChampionHitPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ref HitData hit)
        {
            try
            {
                if (!Champions.Any || hit == null) return;
                Champions.Mods m = Champions.Fast(hit.GetAttacker());
                if (m == null) return;
                float f = ChampionOwn.ForeignDamage(hit.GetAttacker());
                if (f > 0.0001f && Mathf.Abs(f - 1f) > 0.001f) hit.m_damage.Modify(1f / f);
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionHitPatch", pgx); }
        }
    }

    // ---------------------------------------------------------------- ours alone (the user, 2026-10-21)
    // A champion is ours: other mods that scale or dress every creature keep off it. On the plain game nothing here
    // does anything (the game's numbers are ours already); with Path of Valheiman: no extra stars of its own (its
    // "manual" mark), its rarity and modifiers taken off (through its own methods), its biome ladder off the health
    // (ChampionMaxHealthPatch) and off the blows (ChampionHitPatch); with Elite Creatures: its controller taken off.
    // Others are looked for by name, never needed: absent, nothing happens.
    internal static class ChampionOwn
    {
        private static bool _looked, _busy;
        private static System.Reflection.MethodInfo _povMods, _povRarity, _povDamage;
        private static System.Reflection.PropertyInfo _povCurrent;

        private static void Look()
        {
            if (_looked) return;
            _looked = true;
            try
            {
                System.Reflection.Assembly pov = null; // (on the plain game it is not there, and nothing is asked)
                foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies()) { try { if (a.GetType("PassiveTree.EnemyDifficulty") != null) { pov = a; break; } } catch { } } // (by a class of its own)
                if (pov == null) return;
                Type mods = pov.GetType("PassiveTree.RarityMods"), rarity = pov.GetType("PassiveTree.CreatureRarity");
                Type api = pov.GetType("Valheiman.Api.Creatures"), diff = pov.GetType("PassiveTree.EnemyDifficulty");
                _povMods = Setter(mods);
                _povRarity = Setter(rarity);
                if (api != null) _povDamage = AccessTools.Method(api, "DamageFactor", new[] { typeof(Character) });
                if (diff != null) _povCurrent = AccessTools.Property(diff, "Current");
                if (_povMods != null || _povDamage != null) Errands.Log("champions: Path of Valheiman found — its creature scaling kept off champions");
            }
            catch (Exception e) { Errands.Log("champions: looking for creature mods: " + e.Message); }
        }

        // a static Set(Character, <an enum>) of that type (its first value: none, common)
        private static System.Reflection.MethodInfo Setter(Type t)
        {
            if (t == null) return null;
            foreach (System.Reflection.MethodInfo mi in AccessTools.GetDeclaredMethods(t))
            {
                if (mi.Name != "Set" || !mi.IsStatic) continue;
                System.Reflection.ParameterInfo[] ps = mi.GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType == typeof(Character) && ps[1].ParameterType.IsEnum) return mi;
            }
            return null;
        }

        // before its stars are first set: Path of Valheiman gives no extra stars to a creature marked so
        internal static void Claim(ZDO z) { if (z != null && !z.GetBool("pt.crar.manual", false)) z.Set("pt.crar.manual", true); } // (once: every second it is only looked at)

        internal static float Health(Character c, Champions.Mods m) { return c.GetMaxHealthBase() * Mathf.Max(1, m.Level) * m.Hp; }

        // its maximum as ours, written straight (never through SetMaxHealth: other mods scale that); health: the same share
        internal static void Health(Character c, Champions.Mods m, float share)
        {
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || c.IsDead()) return;
            ZDO z = nv.GetZDO();
            float want = Health(c, m);
            if (Mathf.Abs(z.GetFloat(ZDOVars.s_maxHealth, c.m_health) - want) > 0.5f) z.Set(ZDOVars.s_maxHealth, want);
            if (nv.IsOwner())
            {
                float hp = Mathf.Clamp(want * Mathf.Clamp01(share), 1f, want);
                if (Mathf.Abs(z.GetFloat(ZDOVars.s_health, want) - hp) > 0.5f) z.Set(ZDOVars.s_health, hp);
            }
        }

        // once a second where it lives, and whenever its stars are set: its stars, health and traits ours
        internal static void Keep(Character c, Champions.Mods m)
        {
            if (_busy || c == null || m == null) return;
            _busy = true;
            try
            {
                Component elite = c.GetComponent("EliteController"); // (Elite Creatures: its traits live on this)
                if (elite != null) UnityEngine.Object.Destroy(elite);
                ZNetView nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || !nv.IsOwner() || c.IsDead()) return;
                ZDO z = nv.GetZDO();
                Look();
                if (_povMods != null || _povRarity != null) Claim(z);
                if (c.GetLevel() != m.Level) c.SetLevel(m.Level);
                float max = c.GetMaxHealth();
                if (Mathf.Abs(max - Health(c, m)) > 0.5f) Health(c, m, max > 0f ? c.GetHealth() / max : 1f);
                if (_povMods != null && z.GetInt("pt.crmod", 0) != 0) _povMods.Invoke(null, new object[] { c, Enum.ToObject(_povMods.GetParameters()[1].ParameterType, 0) });
                if (_povRarity != null && z.GetInt("pt.crar", 0) != 0) _povRarity.Invoke(null, new object[] { c, Enum.ToObject(_povRarity.GetParameters()[1].ParameterType, 0) });
            }
            catch (Exception e) { Errands.Log("champion: keeping it ours: " + e.Message); }
            finally { _busy = false; }
        }

        // a person of the story (NpcBody): no other mod's rarity, traits or stars on it — asked of them through their
        // own methods, as for a champion, but with nothing of ours put in their place
        internal static void Disown(Character c)
        {
            if (_busy || c == null) return;
            _busy = true;
            try
            {
                Component elite = c.GetComponent("EliteController");
                if (elite != null) UnityEngine.Object.Destroy(elite);
                ZNetView nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || !nv.IsOwner() || c.IsDead()) return;
                ZDO z = nv.GetZDO();
                Look();
                if (_povMods != null || _povRarity != null) Claim(z);
                if (c.GetLevel() != 1) c.SetLevel(1);
                if (_povMods != null && z.GetInt("pt.crmod", 0) != 0) _povMods.Invoke(null, new object[] { c, Enum.ToObject(_povMods.GetParameters()[1].ParameterType, 0) });
                if (_povRarity != null && z.GetInt("pt.crar", 0) != 0) _povRarity.Invoke(null, new object[] { c, Enum.ToObject(_povRarity.GetParameters()[1].ParameterType, 0) });
            }
            catch (Exception e) { Errands.Log("npc: keeping other mods off: " + e.Message); }
            finally { _busy = false; }
        }

        // how much another mod scaled this creature's blows (1: none)
        internal static float ForeignDamage(Character c)
        {
            Look();
            if (_povDamage == null || c == null) return 1f;
            if (_povCurrent != null && _povCurrent.GetValue(null, null) == null) return 1f; // (its creature scaling off: it did not scale)
            object v = _povDamage.Invoke(null, new object[] { c });
            return v is float ? (float)v : 1f;
        }
    }

    // fire and frost in its blows (on the game where it strikes)
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class ChampionTouchPatch
    {
        private static void Prefix(HitData hit)
        {
            try
            {
                if (!Champions.Any) return; // (every hit of the game: the attacker is looked up only while a champion is loaded)
                Champions.Mods m = Champions.Fast(hit != null ? hit.GetAttacker() : null);
                if (m == null) return;
                float total = hit.GetTotalDamage();
                if (m.Has("fire")) hit.m_damage.m_fire += total * 0.35f;
                if (m.Has("frost")) hit.m_damage.m_frost += total * 0.35f;
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionTouchPatch", pgx); }
        }
    }

    // its armour (where it is owned)
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class ChampionArmourPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            try
            {
                if (!Champions.Any) return;
                Champions.Mods m = Champions.Fast(__instance);
                if (m != null && m.Has("tough") && hit != null) hit.ApplyModifier(0.65f);
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionArmourPatch", pgx); }
        }
    }

    // its name when aimed at: «Гнилозуб, ватажок ★★★ · вогняний, швидкий» (in each game's own words)
    [HarmonyPatch(typeof(Character), "GetHoverName")]
    internal static class ChampionNamePatch
    {
        private static void Postfix(Character __instance, ref string __result)
        {
            try
            {
                if (!Champions.Any) return;
                ChampionLife l = Champions.Life(__instance);
                if (l != null) __result = l.Name;
            }
            catch (System.Exception pgx) { PatchGuard.Fail("ChampionNamePatch", pgx); }
        }
    }

    // its drop, its own (whatever stars or another mod would multiply — never the game's ×2 per star): what it drops,
    // twice a plain one's roll (not counted: one roll); its trophy always (an errand may ask for it) and its gold, by the land it fell in
    // (a lord: twice) — only when its owner is there to have it counted: one killed without them comes back in its
    // ring, and no one farms its gold. An arena's creature drops nothing at all — here, since both ways the game drops
    // (straight, or later from the body) take this list.
    [HarmonyPatch(typeof(CharacterDrop), "GenerateDropList")]
    internal static class ChampionDropPatch
    {
        internal static readonly int[] GoldByTier = { 40, 80, 120, 180, 260, 360, 480, 620 };

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
        {
            try
            {
                if (__result == null) return;
                Character c = __instance.GetComponent<Character>();
                if (BindArena.Of(c)) { __result.Clear(); return; }
                if (!Champions.Any) return;
                Champions.Mods m = Champions.Fast(c);
                if (m == null) return;
                bool near = Champions.OwnerNear(m, c.transform.position);
                bool counted = near && !m.Spent && Champions.OwnerStruck(c); // (not its owner's kill: plain; one risen again: its trophy still — an errand may ask for it — but no gold, no double)
                GameObject trophy = null;
                foreach (CharacterDrop.Drop d in __instance.m_drops)
                {
                    if (d.m_prefab == null) continue;
                    ItemDrop it = d.m_prefab.GetComponent<ItemDrop>();
                    if (it != null && it.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy) trophy = d.m_prefab;
                    if (!d.m_levelMultiplier || d.m_levelMultiplierNotAmount) continue;
                    for (int i = 0; i < __result.Count; i++)
                        if (__result[i].Key == d.m_prefab)
                            __result[i] = new KeyValuePair<GameObject, int>(d.m_prefab, Mathf.Max(1, UnityEngine.Random.Range(d.m_amountMin, d.m_amountMax + 1) * (counted ? 2 : 1)));
                }
                if (!near) return;
                bool hasTrophy = false;
                foreach (KeyValuePair<GameObject, int> kv in __result) if (kv.Key == trophy) hasTrophy = true;
                if (trophy != null && !hasTrophy) __result.Add(new KeyValuePair<GameObject, int>(trophy, 1));
                if (!counted) return;
                GameObject coins = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Coins") : null;
                if (coins != null)
                {
                    int tier = WorldGenerator.instance != null ? Champions.TierOf(WorldGenerator.instance.GetBiome(c.transform.position.x, c.transform.position.z)) : 0;
                    int gold = GoldByTier[Mathf.Clamp(tier, 0, GoldByTier.Length - 1)] * (m.Has("lord") ? 2 : 1);
                    __result.Add(new KeyValuePair<GameObject, int>(coins, gold));
                }
            }
            catch (Exception e) { Errands.Log("champion drop: " + e.Message); }
        }
    }

    // the game's own bar over a champion: none (its own bar shows it, RavenBook.ChampionBar)
    [HarmonyPatch(typeof(EnemyHud), "ShowHud")]
    internal static class ChampionHudPatch
    {
        private static bool Prefix(Character c)
        {
            try { return !Champions.Any || Champions.Fast(c) == null; } catch { return true; }
        }
    }
}
#endif
