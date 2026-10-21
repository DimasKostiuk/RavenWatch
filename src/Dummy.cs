#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A test dummy for the talks between players' ravens, alone in the world: a player figure of the game's own (the
    // "Player" prefab, nobody steering it) standing in front of you, with what a real player's game would publish on it:
    // its ravens on the shoulders, their nicknames, voices, a few gossip facts. For every other system it is just another
    // player with ravens near you, so `raven cross pals` makes a talk of 4 ravens, two dummies of 4-6, and running past
    // one throws a word in passing. Never saved: it goes with `raven dummy remove`, when you leave, or the world closes.
    internal static class Dummy
    {
        private static readonly List<GameObject> Made = new List<GameObject>();
        private static readonly string[] Names = { "Сигурд", "Астрід", "Бйорн", "Фрейя", "Ульф", "Гельга" };
        private static readonly string[] NicksH = { "Сивопер|Сивопере|0", "Всезнайко|Всезнайку|0", "Богоклюй|Богоклюю|3", "Книжник|Книжнику|0" };
        private static readonly string[] NicksM = { "Кусака|Кусако|0", "Шибеник|Шибенику|0", "Тарганобій|Тарганобію|2", "Головоріз|Головорізе|0" };

        // raven dummy [h|m|both] [pals] · raven dummy remove
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null || ZNetScene.instance == null) return "Нема гравця.";
            string what = a != null && a.Length > 1 ? a[1].ToLowerInvariant() : "both";
            if (what == "remove") return Remove();
            bool pals = false;
            for (int i = 1; a != null && i < a.Length; i++) if (a[i].ToLowerInvariant() == "pals") pals = true;
            bool h = what != "m", m = what != "h";
            GameObject prefab = ZNetScene.instance.GetPrefab("Player");
            if (prefab == null) return "Не знайшов фігуру гравця в грі.";
            Made.RemoveAll(g => g == null);
            int n = Made.Count;
            Vector3 side = p.transform.right * ((n % 2 == 0 ? 1.6f : -1.6f) * (1 + n / 2));
            Vector3 at = p.transform.position + p.transform.forward * 4f + side;
            float ground;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(at, out ground, 1000)) at.y = ground; // on the ground, not in it
            GameObject go = Object.Instantiate(prefab, at, Quaternion.LookRotation(-p.transform.forward));
            // the game destroys a player of yours that is not you («Destroying old local player»): its own life is switched
            // off before it starts (still in the list of players, with its name and id), and Keep below guards it
            Player body = go.GetComponent<Player>();
            if (body != null) body.enabled = false;
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }
            ZNetView nv = go.GetComponent<ZNetView>();
            ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (z == null) { Object.Destroy(go); return "Фігура не вийшла (нема мережевого об'єкта)."; }
            long id = 777000000L + n + 1;
            string name = Names[n % Names.Length];
            z.Set(ZDOVars.s_playerID, id);
            z.Set(ZDOVars.s_playerName, name);
            z.Set(ZDOVars.s_health, 100f); // alive and well for everyone looking (hurt / dead checks)
            VisEquipment vis = go.GetComponent<VisEquipment>();
            if (vis != null) vis.SetModel(n % 2); // Астрід, Фрейя, Гельга: a woman's figure, so the ravens speak of her as one
            // what a player's game with ravens would publish (CrossTalk, Shoulder, Nicks, Voice, Gossip)
            z.Set(CrossTalk.FlagKey, 1 | (h ? 2 : 0) | (m ? 4 : 0));
            z.Set("RavenWatch_Shoulder", (h ? 1 : 0) | (m ? 2 : 0));
            z.Set("RavenWatch_ShoulderLv", 4 | (5 << 4));
            z.Set("RavenWatch_NickH", h ? NicksH[n % NicksH.Length] : "");
            z.Set("RavenWatch_NickM", m ? NicksM[n % NicksM.Length] : "");
            z.Set(Voice.NetKey, Voice.Pack(h ? Voice.RandomCode() : -1, m ? Voice.RandomCode() : -1));
            int today = Memory.Day();
            z.Set(Gossip.NetKey, "d," + today + ",c,$enemy_draugr,2;b," + Mathf.Max(0, today - 1) + ",Bonemass;h,42;m,17;k,1530");
            Made.Add(go);
            if (Plugin.Instance != null) Plugin.Instance.Log("Test dummy " + name + " (id " + id + ") at " + at + ", player component " + (body != null ? "off" : "missing"));
            if (pals) CrossTalk.Befriend(p, id, CrossTalk.PalsAt);
            return name + " стоїть перед тобою" + (h && m ? " з Хугіном і Муніном" : h ? " з Хугіном" : " з Муніном") + (pals ? ", ваші ворони — давні приятелі" : "")
                + ". Дай воронам кілька секунд сісти, тоді: raven cross pals (intro, greet, fire, boss). Прибрати: raven dummy remove.";
        }

        internal static bool Removing;
        internal static bool Holds(GameObject g) { return g != null && Made.Contains(g); }
        internal static bool Is(Player p) { return p != null && Made.Count > 0 && Made.Contains(p.gameObject); }

        private static string Remove()
        {
            int n = 0;
            Removing = true;
            foreach (GameObject g in Made)
                if (g != null)
                {
                    ZNetView nv = g.GetComponent<ZNetView>();
                    if (nv != null && nv.IsValid() && ZNetScene.instance != null) ZNetScene.instance.Destroy(g); else Object.Destroy(g);
                    n++;
                }
            Made.Clear();
            Removing = false;
            return n > 0 ? "Манекенів прибрано: " + n + "." : "Манекенів нема.";
        }
    }

    // the game would clear a dummy away as a stale player of yours: not while the console keeps it
    [HarmonyPatch(typeof(ZNetScene), "Destroy")]
    internal static class DummyKeep
    {
        private static bool Prefix(GameObject go) { return Dummy.Removing || !Dummy.Holds(go); }
    }
}
#endif
