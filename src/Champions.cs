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
    // and goes when its errand is done or given up. Its drop is cut to about twice a plain one (stars would multiply it).
    internal static class Champions
    {
        internal const string Key = "RavenWatch_champ", PackKey = "RavenWatch_champPack";
        internal const string RpcDead = "RavenWatch_ChampDead";
        internal static float SpawnNear = 70f;    // m: the zone's ring round its place (the step's map ring; in it, it appears)
        internal static float CreditRange = 100f; // m: its death counts for the errand only with its owner this near, alive

        // the creatures of each land (by the land's tier: Meadows 0 … Ashlands 6); only those that walk on land
        private static readonly string[][] ByTier =
        {
            new[] { "Greydwarf", "Boar", "Neck", "Greyling" },
            new[] { "Greydwarf_Elite", "Greydwarf_Shaman", "Troll", "Skeleton" },
            new[] { "Draugr", "Draugr_Elite", "Blob", "Abomination" },
            new[] { "Wolf", "Fenring", "StoneGolem", "Ulv" },
            new[] { "Goblin", "GoblinBrute", "GoblinShaman", "Lox" },
            new[] { "Seeker", "SeekerBrute" },
            new[] { "Charred_Melee", "Charred_Archer", "Morgen", "Asksvin" },
        };
        // how strong a plain creature of each tier is, roughly (its health and blows together)
        private static readonly float[] TierPower = { 1f, 2.2f, 4f, 7f, 11f, 17f, 26f };
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
                case Heightmap.Biome.AshLands: case Heightmap.Biome.DeepNorth: return 6;
                default: return 0;
            }
        }

        // ---------------------------------------------------------------- what one is
        internal sealed class Mods
        {
            public long Owner; public string Id = "", Who = "", Title = ""; // Title: its own name (a lord's), a translation key
            public int Level = 2; public float Hp = 1f, Dmg = 1f;
            public readonly List<string> Aff = new List<string>();

            internal bool Has(string a) { return Aff.Contains(a); }

            internal string Code()
            {
                return "owner=" + Owner + ";id=" + Id + ";who=" + Who + ";lvl=" + Level + ";hp=" + Hp.ToString("0.##", CultureInfo.InvariantCulture)
                    + ";dmg=" + Dmg.ToString("0.##", CultureInfo.InvariantCulture) + ";aff=" + string.Join("+", Aff.ToArray()) + (Title.Length > 0 ? ";t=" + Title : "");
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
        internal static bool Any { get { return Known.Count > 0; } } // (the hot patches: nothing to do while no champion is loaded)

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
            { "tough", Lang.K("броньований") }, { "regen", Lang.K("живучий") }, { "pack", Lang.K("ватажок зграї") },
        };

        internal static string Words(Mods m)
        {
            List<string> l = new List<string>();
            foreach (string a in m.Aff) { string w; if (Affixes.TryGetValue(a, out w)) l.Add(Lang.T(w)); }
            return string.Join(", ", l.ToArray());
        }

        internal static string Name(Mods m)
        {
            string stars = " " + new string('★', Mathf.Max(0, m.Level - 1));
            if (m.Title.Length > 0) return Lang.T(m.Title) + stars;
            return Lang.F("Ватажок {0}", ErrandSteps.Creature(m.Who)) + stars;
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
            m.Owner = p.GetPlayerID(); m.Id = stamp;
            GameObject go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            Character c = go.GetComponent<Character>();
            ZNetView nv = go.GetComponent<ZNetView>();
            if (c == null || nv == null || !nv.IsValid()) { UnityEngine.Object.Destroy(go); return null; }
            nv.GetZDO().Set(Key, m.Code());
            c.SetLevel(m.Level);                    // (its health: SetupMaxHealth, with our factor)
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
            if (m.Has("lord")) DropTrophy(c);
            ZPackage pkg = new ZPackage();
            pkg.Write(m.Owner);
            pkg.Write(m.Id);
            pkg.Write(c.transform.position);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcDead, pkg);
        }

        // a lord's head always falls (its errand may ask for it): its creature's trophy, by its drop list
        private static void DropTrophy(Character c)
        {
            CharacterDrop cd = c.GetComponent<CharacterDrop>();
            if (cd == null) return;
            foreach (CharacterDrop.Drop d in cd.m_drops)
            {
                ItemDrop it = d.m_prefab != null ? d.m_prefab.GetComponent<ItemDrop>() : null;
                if (it == null || it.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy) continue;
                UnityEngine.Object.Instantiate(d.m_prefab, c.transform.position + Vector3.up * 1.5f, Quaternion.identity);
                Errands.Log("lord " + Name(Of(c)) + " left its trophy " + d.m_prefab.name);
                return;
            }
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

        // counted only with the errand's owner there: alive and near where it fell (friends may help, not do it for you)
        private static void OnDead(long sender, ZPackage pkg)
        {
            long owner = pkg.ReadLong();
            string id = pkg.ReadString();
            Player p = Player.m_localPlayer;
            if (p == null || p.GetPlayerID() != owner || pkg.GetPos() >= pkg.Size()) return;
            Vector3 at = pkg.ReadVector3();
            float dist = Vector3.Distance(p.transform.position, at);
            if (p.IsDead() || dist > CreditRange)
            {
                // not counted: on the way back into its ring the errand brings another one
                Errands.Log("champion " + id + " killed " + Mathf.RoundToInt(dist) + " m from you" + (p.IsDead() ? " (you were dead)" : "") + ": not counted");
                if (Errands.Wants(p, id)) RavenMsg.Center(p, Lang.T("Ватажка вбили без тебе — Одін цього не зарахував. Повернись у позначене коло: там з'явиться інший."));
                return;
            }
            if (!HasStruck(p, id))
            {
                // near, but not a single blow of yours (nor of your ravens): the others did it for you
                Errands.Log("champion " + id + " killed near you, but you never hit it: not counted");
                if (Errands.Wants(p, id)) RavenMsg.Center(p, Lang.T("Ватажка вбили інші, а ти його й не зачепи{в|ла} — Одін цього не зарахував. У позначеному колі з'явиться інший."));
                return;
            }
            Errands.OnChampion(id);
#if RAVEN_STORY
            StoryBus.Raise("champion", id, 0);
#endif
        }

        // the champions of your errands you hit (player customData, the last few: it lives through leaving the game)
        internal const string StruckKey = "RavenWatch_champHit";

        internal static void Struck(Player p, string id)
        {
            string raw;
            if (!p.m_customData.TryGetValue(StruckKey, out raw)) raw = "";
            if (("|" + raw + "|").Contains("|" + id + "|")) return;
            List<string> l = new List<string>(raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            l.Add(id);
            while (l.Count > 6) l.RemoveAt(0);
            p.m_customData[StruckKey] = string.Join("|", l.ToArray());
            Errands.Log("you hit the champion " + id);
        }

        internal static bool HasStruck(Player p, string id)
        {
            string raw;
            return p != null && p.m_customData.TryGetValue(StruckKey, out raw) && ("|" + raw + "|").Contains("|" + id + "|");
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

        internal static void Put(Character c)
        {
            if (c == null || c.GetComponent<ChampionLife>() != null) return;
            Champions.Mods m = Champions.Of(c);
            if (m == null) return;
            ChampionLife l = c.gameObject.AddComponent<ChampionLife>();
            l._c = c; l._nv = c.GetComponent<ZNetView>(); l._m = m;
            Champions.Know(c, l);
            c.transform.localScale *= m.Has("lord") ? 1.6f : 1.2f;
            if (m.Has("lord")) c.m_boss = true; // a boss's bar over the screen for everyone (each game sets it on its own copy)
            if (m.Has("fast")) { c.m_speed *= 1.35f; c.m_runSpeed *= 1.35f; c.m_walkSpeed *= 1.2f; }
            l._lastHealth = c.GetHealth();
        }

        private void Update()
        {
            if (_c == null || _nv == null || !_nv.IsValid() || _c.IsDead()) return;
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
        private static void Postfix(Character __instance) { try { ChampionLife.Put(__instance); } catch { } }
    }

    // its health: the game's (base × level) and our factor on top (the game sets it again when it is at full health)
    [HarmonyPatch(typeof(Character), "SetupMaxHealth")]
    internal static class ChampionHealthPatch
    {
        private static void Postfix(Character __instance)
        {
            try
            {
                Champions.Mods m = Champions.Of(__instance);
                if (m == null) return;
                __instance.SetMaxHealth(__instance.GetMaxHealthBase() * __instance.GetLevel() * m.Hp);
            }
            catch { }
        }
    }

    // its blows: the stars' factor and ours
    [HarmonyPatch(typeof(Attack), "GetLevelDamageFactor")]
    internal static class ChampionDamagePatch
    {
        private static void Postfix(Humanoid ___m_character, ref float __result)
        {
            if (!Champions.Any) return;
            try
            {
                Champions.Mods m = Champions.Fast(___m_character);
                if (m != null) __result *= m.Dmg;
            }
            catch { }
        }
    }

    // your blow (or your ravens') on the champion of your own errand: you took part, its death may count for you
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class ChampionStruckPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (!Champions.Any) return;
            try
            {
                Player me = Player.m_localPlayer;
                if (me == null || hit == null || hit.m_attacker != me.GetZDOID()) return;
                Champions.Mods m = Champions.Fast(__instance);
                if (m != null && m.Owner == me.GetPlayerID()) Champions.Struck(me, m.Id);
            }
            catch { }
        }
    }

    // fire and frost in its blows (on the game where it strikes)
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class ChampionTouchPatch
    {
        private static void Prefix(HitData hit)
        {
            if (!Champions.Any) return; // (every hit of the game: the attacker is looked up only while a champion is loaded)
            try
            {
                Champions.Mods m = Champions.Fast(hit != null ? hit.GetAttacker() : null);
                if (m == null) return;
                float total = hit.GetTotalDamage();
                if (m.Has("fire")) hit.m_damage.m_fire += total * 0.35f;
                if (m.Has("frost")) hit.m_damage.m_frost += total * 0.35f;
            }
            catch { }
        }
    }

    // its armour (where it is owned)
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class ChampionArmourPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (!Champions.Any) return;
            try
            {
                Champions.Mods m = Champions.Fast(__instance);
                if (m != null && m.Has("tough") && hit != null) hit.ApplyModifier(0.65f);
            }
            catch { }
        }
    }

    // its name over the bar: «Ватажок Грейдворф ★★★★ · вогняний, швидкий» (in each game's own words)
    [HarmonyPatch(typeof(Character), "GetHoverName")]
    internal static class ChampionNamePatch
    {
        private static void Postfix(Character __instance, ref string __result)
        {
            if (!Champions.Any) return;
            try
            {
                ChampionLife l = Champions.Life(__instance);
                if (l != null) __result = l.Name;
            }
            catch { }
        }
    }

    // its drop: about twice a plain one (stars would double it for each)
    [HarmonyPatch(typeof(CharacterDrop), "GenerateDropList")]
    internal static class ChampionDropPatch
    {
        private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
        {
            try
            {
                if (__result == null || !Champions.Any) return;
                Champions.Mods m = Champions.Fast(__instance.GetComponent<Character>());
                if (m == null) return;
                int pow = Mathf.Max(1, (int)Mathf.Pow(2f, m.Level - 1));
                HashSet<GameObject> starred = new HashSet<GameObject>(); // (only these amounts were multiplied by the stars)
                foreach (CharacterDrop.Drop d in __instance.m_drops) if (d.m_prefab != null && d.m_levelMultiplier && !d.m_levelMultiplierNotAmount) starred.Add(d.m_prefab);
                for (int i = 0; i < __result.Count; i++)
                {
                    if (!starred.Contains(__result[i].Key)) continue;
                    int n = __result[i].Value;
                    int cut = Mathf.Max(1, Mathf.CeilToInt(n * 2f / pow));
                    if (cut < n) __result[i] = new KeyValuePair<GameObject, int>(__result[i].Key, cut);
                }
            }
            catch { }
        }
    }
}
#endif
