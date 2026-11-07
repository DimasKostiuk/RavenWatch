#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // What you may ask of a person when its patience is high (docs/plans/talk-plan.md, the user 2026-11-05: «станцюй — і якщо він дійсно
    // захоче, станцює»). Its talk's «Попросити про щось…» (NpcTalk «ask», from NpcPatience.Easy, not while it is angry): each favour is a
    // row of All — whether it may be asked now (Can), how its ways take to it (Fit), only of one dear (Dear), once ever (Once), its words
    // and what it does on the game that runs it (There: true if it could). Whether it will: a roll of its patience (from 60 up) × Fit (× more for one dear).
    // Yes costs Cost of its patience, no costs NoCost and the favour is shut a while (NpcTalk.FavourShut: your character keeps it).
    // It said yes and then could not (its duty took it, the pantry is empty): it says so over its head (Cant, a talk's asked words: not NpcVoice's unasked), the cost comes back.
    // A thing given once for good is given on your own game, so a favour is never marked done without it (gift).
    //   dance    a dance (the game's own gesture for now: our clip later)        a cheerful one gladly, a grumbler hardly, a timid one shy
    //   examine  it looks your weapon over (look_examine) and says what it thinks
    //   show     it points at its own place in the base (NpcPlaces.Favourite) and marks it on your map
    //   sit      it sits by you a while
    //   food     a bite from its base's pantry (NpcMeals) at your feet, its chest opened as any is (NpcMind.Open: its owner's game keeps what is in it)
    //   gift     a keepsake of its own, its name on it (NpcKeepsake): one dear, once
    //   secret   what it never told anyone (NpcBackstory.Secret): one dear, once, not angry
    //   night    it walks with you at night a while, afraid or not
    //   dress    it lets you change its clothes or take them back, for now (DressFor; NpcTalkDress): anyone not on guard duty
    // Not yet: a song (needs its voice). In a talk: ?favour <id> · !favour <id> · {favour} its answer.
    internal static class NpcFavours
    {
        internal sealed class Favour
        {
            public string Id = "";
            public bool Dear, Once;
            public Func<NpcActor, Player, bool> Can = (a, p) => true;
            public Func<NpcActor, HashSet<string>, float> Fit = (a, t) => 1f;
            public Func<NpcActor, Player, string> Yes;            // its words when it will (null: the favour's theme «favour_<id>»)
            public Func<NpcActor, Player, string[], bool> There;  // what it does, on the game that runs it: false, it could not
            public string Cant = "favour_cant";                   // its words then
        }

        internal static float From = 60f, Over = 30f, DearMore = 1.2f, Cost = 8f, NoCost = 6f, SitFor = 150f, WalkFor = 300f, DanceFor = 7f, FoodLid = 1f;
        internal static string Said = "";
        internal static float DressFrom = 80f; // (its patience: a little more than the other favours ask)
        internal static NpcActor DressFor; // (it said yes to your changing its clothes, this talk)

        internal static readonly Dictionary<string, Favour> All = new Dictionary<string, Favour>
        {
            { "dance", new Favour { Id = "dance", Fit = (a, t) => t.Contains("весел") ? 1.3f : t.Contains("бурч") ? 0.4f : t.Contains("боязк") ? 0.5f : 1f,
                There = (a, p, w) => { if (a.Body == null) return false; a.Body.Gesture("dance", true); if (a.Mind != null) a.Mind.Still(DanceFor); a.StartCoroutine(StopDance(a)); return true; } } },
            { "examine", new Favour { Id = "examine", Can = (a, p) => Held(p) != null, Fit = (a, t) => NpcPersona.Of(a).Curiosity > 0.6f ? 1.2f : 1f,
                Yes = Examined, There = (a, p, w) => { if (a.Body == null) return false; a.Body.Clip("look_examine"); return true; } } },
            { "show", new Favour { Id = "show", Can = (a, p) => { Vector3 at; return NpcPlaces.Favourite(a, out at); }, Fit = (a, t) => NpcPersona.Of(a).Curiosity > 0.6f ? 1.2f : 1f,
                Yes = Shown, There = (a, p, w) => { Vector3 at; if (!NpcPlaces.Favourite(a, out at)) return false; if (a.Mind != null) a.Mind.Face(at); if (a.Body != null) a.Body.Clip("point_at"); return true; } } },
            { "sit", new Favour { Id = "sit", Can = (a, p) => NpcResident.Is(a), Fit = (a, t) => NpcPersona.Of(a).Chatty > 0.6f ? 1.2f : 1f,
                There = (a, p, w) => a.Mind != null && p != null && a.Mind.TakeWhim(new NpcMind.Plan { Kind = NpcMind.Task.Sit, To = p.transform.position + p.transform.right * 1.2f }, SitFor, Whose) } },
            { "food", new Favour { Id = "food", Can = (a, p) => NpcResident.Is(a), Fit = (a, t) => NpcHunger.IsHungry(a) ? 0.4f : 1f, There = Food, Cant = "favour_food_none" } },
            { "gift", new Favour { Id = "gift", Dear = true, Once = true, Can = (a, p) => NpcResident.Is(a) && GiftOf(a) != null, Yes = Gift } },
            { "secret", new Favour { Id = "secret", Dear = true, Once = true, Can = (a, p) => !NpcPatience.Angry(a) && NpcBackstory.Secret(a).Length > 0, Yes = Secret } },
            { "dress", new Favour { Id = "dress", Can = (a, p) => NpcGear.RoleOf(a) != NpcGear.Role.Full && (NpcResident.Is(a) || NpcTalk.Hired(a)) && NpcPatience.Of(a, p) >= DressFrom, Fit = (a, t) => t.Contains("весел") ? 1.2f : t.Contains("бурч") ? 0.7f : 1f } },
            { "night", new Favour { Id = "night", Can = (a, p) => EnvMan.IsNight() && NpcResident.Is(a), Fit = (a, t) => t.Contains("смілив") ? 1.2f : t.Contains("боязк") ? 0.4f : 1f,
                There = (a, p, w) => a.Mind != null && p != null && a.Mind.TakeWhim(new NpcMind.Plan { Kind = NpcMind.Task.Follow, Who = p.GetZDOID(), Leader = p.GetPlayerID(), Keep = 3f }, WalkFor, Whose) } }, // (not over a duty of its own: a meal, its rest)
        };

        private const string Whose = "favour"; // (its whim's owner: what it does for you, with its term)

        // ---------------------------------------------------------------- asked (the talker's game)
        internal static bool May(NpcActor a, Player p, string id)
        {
            Favour f;
            if (a == null || p == null || !All.TryGetValue(id, out f)) return false;
            if (f.Dear && NpcAttitude.Level(a, p) != "close") return false;
            return !NpcTalk.FavourShut(a, p, id, f.Once) && f.Can(a, p);
        }

        internal static void Ask(NpcActor a, Player p, string id)
        {
            Favour f;
            Said = "…";
            if (a == null || p == null || !All.TryGetValue(id, out f)) return;
            bool dear = NpcAttitude.Level(a, p) == "close";
            float chance = Mathf.Clamp01((NpcPatience.Of(a, p) - From) / Over) * f.Fit(a, NpcPhrases.Tones(a)) * (dear ? DearMore : 1f);
            if (f.Dear && !dear) chance = 0f;
            if (UnityEngine.Random.value < chance)
            {
                Said = (f.Yes != null ? f.Yes(a, p) : null) ?? NpcPhrases.Compose(a, "favour_" + id, p) ?? a.Form(Lang.T("Ну гаразд."));
                NpcPatience.Spend(a, p, Cost);
                NpcTalk.FavourAsked(a, p, id, f.Once);
                if (id == "dress") DressFor = a;
                if (f.There != null) a.Act("favour " + id + " " + NpcKit.Ref(p.GetZDOID()) + " " + p.GetPlayerID().ToString(CultureInfo.InvariantCulture)); // session: you, found at once on the game that runs it
                Npcs.Log(a.Id + ": does a favour «" + id + "» (" + Mathf.RoundToInt(chance * 100f) + "%)");
            }
            else
            {
                Said = NpcPhrases.Compose(a, "favour_no_" + id, p) ?? NpcPhrases.Compose(a, "favour_no", p) ?? a.Form(Lang.T("Ні. Не зараз."));
                NpcPatience.Spend(a, p, NoCost);
                NpcTalk.FavourAsked(a, p, id, false);
                Npcs.Log(a.Id + ": refuses a favour «" + id + "» (" + Mathf.RoundToInt(chance * 100f) + "%)");
            }
        }

        // «favour <id> <you> <your player id>» on the game that runs it
        private static void Act(NpcActor a, string[] w)
        {
            Favour f;
            long id = 0L;
            if (w.Length < 2 || !All.TryGetValue(w[1], out f) || f.There == null) return;
            if (w.Length > 3 && (!long.TryParse(w[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out id) || !NpcActor.SentBy(id))) return;
            GameObject go = w.Length > 2 ? NpcKit.Find(NpcKit.Parse(w[2])) : null;
            Player p = go != null ? go.GetComponent<Player>() : null;
            if (f.There(a, p, w)) return;
            a.Say(NpcPhrases.Compose(a, f.Cant, p) ?? a.Form(Lang.T("Зараз не вийде. Вибач.")));
            if (id != 0L) a.Act("patience " + w[3] + " " + (-Cost).ToString("0.#", CultureInfo.InvariantCulture)); // (it said yes and could not: not yours to pay for)
            Npcs.Log(a.Id + ": could not do the favour «" + w[1] + "»");
        }

        private static IEnumerator StopDance(NpcActor a)
        {
            yield return new WaitForSeconds(DanceFor);
            if (a != null && a.Body != null) a.Body.Gesture("", false);
        }

        // ---------------------------------------------------------------- its words and deeds
        private static ItemDrop.ItemData Held(Player p)
        {
            if (p == null) return null;
            ItemDrop.ItemData any = null;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetEquippedItems()) { if (it.IsWeapon()) return it; if (any == null) any = it; } // (a weapon in hand first, else anything worn)
            return any;
        }

        private static string Examined(NpcActor a, Player p)
        {
            ItemDrop.ItemData it = Held(p);
            if (it == null) return null;
            string name = Localization.instance != null ? Localization.instance.Localize(it.m_shared.m_name) : it.m_shared.m_name;
            float max = it.GetMaxDurability();
            string theme = it.m_shared.m_useDurability && max > 0f && it.m_durability / max < 0.3f ? "favour_examine_worn" : it.m_quality >= 3 ? "favour_examine_good" : "favour_examine";
            return NpcPhrases.Compose(a, theme, p, new Dictionary<string, string> { { "item", name } }) ?? NpcPhrases.Compose(a, "favour_examine", p, new Dictionary<string, string> { { "item", name } });
        }

        private static string Shown(NpcActor a, Player p)
        {
            Vector3 at;
            if (!NpcPlaces.Favourite(a, out at)) return null;
            string pin = "pin" + Mathf.RoundToInt(at.x / 4f) + "_" + Mathf.RoundToInt(at.z / 4f); // (a place marked once: shown again, it only points)
            if (Minimap.instance != null && p == Player.m_localPlayer && !NpcTalk.FavourShut(a, p, pin, true)) { Minimap.instance.AddPin(at, Minimap.PinType.Icon3, a.Name, true, false); NpcTalk.FavourAsked(a, p, pin, true); } // (on your map: its own place)
            return NpcPhrases.Compose(a, "favour_show", p);
        }

        private static string Secret(NpcActor a, Player p)
        {
            string s = NpcBackstory.Secret(a);
            if (s.Length == 0) return null;
            NpcMemory.Tell(a, "secret_told", p.GetPlayerID(), "");
            string lead = NpcPhrases.Compose(a, "favour_secret", p);
            return string.IsNullOrEmpty(lead) ? s : lead + " " + s;
        }

        private static bool Food(NpcActor a, Player p, string[] w)
        {
            NpcResident r = a.GetComponent<NpcResident>();
            Container c = r != null ? NpcMeals.Pantry(r.Base, a.transform.position) : null;
            if (c == null || p == null || a.Mind == null || !a.Mind.Open(c, FoodLid)) return false; // (the one way into a chest: someone has it open, or it is not ours to take — nothing)
            ItemDrop.ItemData bite = NpcMeals.Plainest(c.GetInventory());
            if (bite == null) { NpcLid.Close(c); return false; }
            ItemDrop.ItemData one = bite.Clone(); one.m_stack = 1;
            c.GetInventory().RemoveItem(bite, 1);
            NpcLid.Close(c);
            ItemDrop.DropItem(one, 1, p.transform.position + p.transform.forward * 0.8f + Vector3.up * 0.5f, Quaternion.identity);
            a.Say(NpcPhrases.Compose(a, "favour_food_given", p) ?? a.Form(Lang.T("Тримай. Тільки нікому.")));
            return true;
        }

        private static GameObject GiftOf(NpcActor a) { return WorldGenerator.instance != null ? NpcKeepsake.Any(WorldGenerator.instance.GetBiome(NpcRules.Home(a))) : null; }

        // its keepsake at your feet: on your game, so that «once» is written only for one really given
        private static string Gift(NpcActor a, Player p)
        {
            GameObject prefab = GiftOf(a);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop != null)
            {
                ItemDrop.ItemData it = drop.m_itemData.Clone();
                it.m_dropPrefab = prefab;
                it.m_crafterName = a.Name; // (its name on it: «made by»)
                ItemDrop.DropItem(it, 1, p.transform.position + p.transform.forward * 0.8f + Vector3.up * 0.5f, Quaternion.identity);
            }
            return NpcPhrases.Compose(a, "favour_gift", p);
        }

        // ---------------------------------------------------------------- wired once (NpcModules)
        internal static void Wire()
        {
            NpcActor.Acts["favour"] = Act;
            NpcTalk.Deeds["favour"] = (a, p, w) => { if (w.Length > 1) Ask(a, p, w[1]); };
            NpcTalk.Words["{favour}"] = (a, p) => Said;
            Register();
        }

        private static bool _registered;
        internal static void Register()
        {
            if (_registered) return;
            _registered = true;
            StoryCond.Register("favour", new StoryCond.Kind { Usage = "favour <id>   (про це можна попросити зараз: dance, examine, show, sit, food, gift, secret, night)", Min = 1, Test = (p, w) => May(NpcTalk.Him, p, w[1]) });
            StoryCond.Register("dressok", new StoryCond.Kind { Usage = "dressok   (він щойно дозволив себе перевдягнути)", Test = (p, w) => DressFor != null && DressFor == NpcTalk.Him });
        }
    }
}
#endif
