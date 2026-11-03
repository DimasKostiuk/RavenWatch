#if RAVEN_ITEM && RAVEN_NPC
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A person's health (npc-plan.md, fighters). A hired person can be hurt (its bar shows as an animal's does); a story
    // person only when its template says «mortal». A hired one never dies of it (the user: no deaths but the story's):
    //   at nothing it falls senseless where it is — lies three minutes, no blow reaches it, no monster minds it
    //   a healing mead given it then (E, any player) brings it to at once with half its health; it goes on as it was
    //   else it comes to with a quarter and limps home (a resident away from its base: a slow way, NpcTravel «home» —
    //   on it nothing reaches it either); one not settled anywhere just gets up
    //   out of a fight it heals slowly by itself (on its feet: some fifteen minutes from nothing), sitting — a bench, by the
    //   fire, at a meal — two and a half times as fast, asleep in its bed four times; a meal itself heals a quarter
    //   a fighter low on health falls back from the fight (how low: its bravery, NpcPersona) and comes back healed
    // Runs where it is owned; «senseless» is in its ZDO (written when it falls and when it rises) — every game sees it.
    internal sealed class NpcVitals : MonoBehaviour
    {
        internal static readonly int KDown = "RavenWatch_npcDown".GetStableHashCode(); // bool: senseless
        private const float Heal = 0.0011f, HealEvery = 5f, InBed = 4f, Seated = 2.5f, DownFor = 180f, WakeWith = 0.25f, Revived = 0.5f, Meal = 0.25f;

        private NpcActor _a;
        private Character _c;
        private float _next, _nextHeal, _downSince;
        internal bool Down;

        private void Awake() { _a = GetComponent<NpcActor>(); _c = GetComponent<Character>(); }

        // a fighter low on health: out of the fight until it is better
        internal bool Retreats
        {
            get
            {
                if (_c == null || _a == null || _a.Mind == null || !_a.Mind.Fighter) return false;
                return _c.GetHealthPercentage() < NpcPersona.Of(_a).RetreatAt;
            }
        }

        private void Update()
        {
            if (_a == null || _c == null || !_a.Owned || Time.time < _next) return;
            _next = Time.time + 1f;
            bool kept = IsDown(_a); // (as its ZDO has it: it was lying when its zone slept, or while another game ran it)
            if (!Down && kept) KnockOut(true);
            else if (Down && !kept) { Down = false; if (_a.Body != null) _a.Body.Gesture("", false); } // (brought to on another game while it ran it)
            if (Down)
            {
                if (NpcRescueHits.Died(_a)) return; // (dead: lies till the server takes it away)
                if (Time.time - _downSince > DownFor) { if (NpcRescueHits.InNeed(_a)) NpcRescueHits.Perish(_a); else { GetUp(WakeWith); LimpHome(); } }
                return;
            }
            if (NpcRescueHits.InNeed(_a)) return; // (in need: its health is its blows left, no healing by itself)
            if (_a.Mind != null && _a.Mind.NowLayer == NpcMind.Layer.Fight) { _nextHeal = Time.time + HealEvery; return; } // (no healing in a fight)
            if (Time.time < _nextHeal || _c.GetHealth() >= _c.GetMaxHealth()) return;
            _nextHeal = Time.time + HealEvery; // (healed in a few steps, not every second: each one is sent to every game)
            float rate = Heal * HealEvery * (_a.Mind == null ? 1f : _a.Mind.Asleep ? InBed : _a.Mind.SeatedNow ? Seated : 1f);
            _c.Heal(_c.GetMaxHealth() * rate, false);
        }

        // its health ran out: senseless instead of dead
        internal void KnockOut(bool lying = false)
        {
            if (Down) return;
            Down = true; _downSince = Time.time;
            _c.SetHealth(1f);
            if (_a.Zdo != null && !_a.Zdo.GetBool(KDown, false)) _a.Zdo.Set(KDown, true);
            if (_a.Mind != null) _a.Mind.Downed(); // (first: what it was doing let go of — getting up off a seat or the ground clears its pose)
            if (_a.Body != null) { if (_a.Body.Attached) _a.Body.Detach(true); _a.Body.Gesture("", false); } // (then down it falls: its own fall and lying, NpcFall — not the game's «rest», which lay down calmly as if to sleep)
            if (lying) return; // (lying already: only taken up here — three minutes from now)
            Npcs.Log(_a.Id + ": fell senseless");
            StoryBus.Raise("npc_down", _a.Id, 0, transform.position);
        }

        private void GetUp(float health)
        {
            Down = false;
            _c.SetHealth(_c.GetMaxHealth() * health);
            if (_a.Zdo != null) _a.Zdo.Set(KDown, false);
            if (_a.Body != null) _a.Body.Gesture("", false);
            Npcs.Log(_a.Id + ": came to (" + Mathf.RoundToInt(health * 100f) + "%)");
            StoryBus.Raise("npc_up", _a.Id, 0, transform.position);
        }

        // a healing mead given (on its owner's game: NpcActor «revive»)
        internal void Revive(long by = 0L)
        {
            if (!Down && !IsDown(_a)) return; // (its ZDO too: taken over just now, not looked at here yet)
            if (NpcRescueHits.Died(_a)) return;
            GetUp(Revived);
            NpcRescueHits.Revived(_a); // (in need: half its blows back)
            if (by != 0L) NpcMemory.Remember(_a, "revived", by, "");
            NpcPhrases.SayOr(_a, "revived", _a.Form(Lang.T("Ох… дякую. Думав|Думала, вже не встану.")));
        }

        // a resident away from its base: home, slowly (out of the fight, out of harm's way); at its base: it just goes on
        private void LimpHome()
        {
            if (!NpcResident.Is(_a)) return;
            Vector3 home = NpcRules.Home(_a);
            if (Utils.DistanceXZ(transform.position, home) < NpcBase.Reach) return;
            NpcPhrases.SayOr(_a, "limp_home", Lang.T("Піду додому… потихеньку."));
            _a.Order("travel " + Npcs.V(home) + " " + NpcTravel.ToHome);
        }

        // a meal: a quarter better
        internal void Fed()
        {
            if (Down || _c == null || _c.GetHealth() >= _c.GetMaxHealth()) return;
            _c.Heal(_c.GetMaxHealth() * Meal, false);
        }

        // senseless (every game: from its ZDO)
        internal static bool IsDown(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null && z.GetBool(KDown, false); }
        internal static bool IsDown(Character c) { return IsDown(NpcActor.Of(c)); }

        // nobody's foe now: on its way (NpcTravel) or senseless — one look into its ZDO for both
        internal static bool Unseen(Character c)
        {
            NpcActor a = NpcActor.Of(c);
            ZDO z = a != null ? a.Zdo : null;
            return z != null && (z.GetBool(NpcTravel.KTravel, false) || z.GetBool(KDown, false));
        }

        // ---------------------------------------------------------------- a healing mead (only those: no doubt what it does)
        private static readonly string[] Meads = { "MeadHealthMinor", "MeadHealthMedium", "MeadHealthMajor", "MeadHealthLingering" };

        // the plainest healing mead in the bag (null: none)
        internal static ItemDrop.ItemData Mead(Player p)
        {
            if (p == null) return null;
            ItemDrop.ItemData best = null; int rank = int.MaxValue;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
            {
                string n = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
                if (!n.StartsWith("MeadHealth", System.StringComparison.Ordinal)) continue;
                int r = System.Array.IndexOf(Meads, n); if (r < 0) r = Meads.Length; // (a mod's own healing mead: after the game's)
                if (r < rank) { rank = r; best = it; }
            }
            return best;
        }

        // E on one lying senseless (on the game of the one who gives)
        internal static bool Give(Player p, NpcActor a)
        {
            ItemDrop.ItemData m = Mead(p);
            if (m == null) { p.Message(MessageHud.MessageType.Center, Lang.T("Потрібна медовуха зцілення")); return false; }
            p.GetInventory().RemoveItem(m, 1);
            a.Act("revive " + p.GetPlayerID().ToString(System.Globalization.CultureInfo.InvariantCulture)); // (its owner gets it up — and remembers who, if it did)
            Npcs.Log(a.Id + ": given " + (m.m_dropPrefab != null ? m.m_dropPrefab.name : "a mead"));
            return true;
        }
    }

    // a hired person's health ran out: it falls senseless, it does not die (a story person whose template says
    // «mortal» dies as the game has it)
    [HarmonyPatch(typeof(Character), "CheckDeath")]
    internal static class NpcNoDeathPatch
    {
        private static bool Prefix(Character __instance)
        {
            if (NpcActor.Live.Count == 0 || __instance.GetHealth() > 0f) return true;
            NpcActor a = NpcActor.Of(__instance);
            if (a == null) return true;
            try
            {
                if (a.Def != null && a.Def.Mortal && !NpcTalk.Hired(a)) return true;
                if (a.Vitals != null && a.Owned) a.Vitals.KnockOut(); else __instance.SetHealth(1f);
            }
            catch (System.Exception e) { PatchGuard.Fail("NpcNoDeathPatch", e); } // (one of ours all the same: it does not die of a failure here)
            return false;
        }
    }
}
#endif
