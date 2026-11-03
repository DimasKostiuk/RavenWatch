#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // «Випробування Одіна» — gone from 2.9 (the user 2026-11-02: the first raven is your own, the prologue brings it;
    // or a stranger one from Haldor). What is left: the helpers others ask (a raven of yours, had one, of this kind) and a
    // trial still running in an old save — it ends as it did (Errands: done → that raven, failed / out of time → gone).
    internal static class Trial
    {
        internal const string Key = "RavenWatch_trial"; // "" not yet · done · failed
        internal const string BossKey = "defeated_eikthyr";
        internal static float DaysToDo = 3f;               // game days the trial lasts

        private static float DayLen { get { return EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f; } }

        internal static string State(Player p) { string s; return p != null && p.m_customData.TryGetValue(Key, out s) ? s : ""; }
        internal static bool Over(Player p) { return State(p).Length > 0; }

        // a raven of yours anywhere you hold it: the bag, the shoulders
        internal static bool HasRaven(Player p)
        {
            if (p == null) return false;
            bool m;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems()) if (RavenItem.IsRaven(item, out m)) return true;
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p)) if (RavenItem.IsRaven(item, out m)) return true;
            return false;
        }

        // a raven of your own was ever with you (Perks.Sync marks it)
        internal const string HadKey = "RavenWatch_hadRaven";
        internal const string HadHuginKey = "RavenWatch_hadHugin", HadMuninKey = "RavenWatch_hadMunin";

        // a raven of this kind is yours (had one, or a copy of one kept in you: lost, in a grave, in a chest): the game's
        // big raven of that kind no longer flies to you — two Hugins would break it
        internal static bool HadKind(Player p, bool munin)
        {
            return p != null && (p.m_customData.ContainsKey(munin ? HadMuninKey : HadHuginKey) || Backup.HasCopy(p, munin));
        }

        internal static bool Running(Player p) { ErrandRun r = Errands.Of(p); return r != null && r.Def != null && r.Def.Trial; }

        internal static float TimeLeft(ErrandRun r) { return (float)(r.Given + DaysToDo * DayLen - WorldTime.Now); }

        // the book's corner of the errand card
        internal static string From(ErrandRun r)
        {
            string who = r.Munin ? Lang.T("Муніна") : Lang.T("Хугіна");
            if (r.Def == null || !r.Def.Trial) return Lang.F("від {0}", who);
            return Lang.F("нагорода — {0} · ще {1}", Plugin.RavenName(r.Munin), RavenAbilities.Span(Mathf.Max(0f, TimeLeft(r))));
        }

        // ---------------------------------------------------------------- the bowl, for one with no ravens in the nest
        // the hover line of a trial still running from an old save, or null
        private static string Hover(Player p, NestView v)
        {
            if (!Running(p)) return null;
            return "[<color=yellow><b>$KEY_Use</b></color>] " + (Errands.Of(p).Current is BringStep ? Lang.T("Покласти в чашу для випробування") : Lang.T("Подивитися випробування"));
        }

        // one with no raven at all, on the nest (its top or its bowl): a running trial's line, or why there is nothing to do
        internal static string NoRavenHover(Player p, NestView v)
        {
            string t = Hover(p, v);
            if (t != null) return t;
            return "<color=#9A9A9A>" + Lang.T("Посади сюди своїх воронів, коли вони будуть.") + "</color>";
        }

        internal static void NoRavenUse(Player p, NestView v)
        {
            if (Use(p, v)) return;
            string t = NoRavenHover(p, v);
            int a = t.IndexOf('>'), b = t.LastIndexOf('<');
            RavenMsg.Center(p, a >= 0 && b > a ? t.Substring(a + 1, b - a - 1) : t);
        }

        // E on the bowl during a running trial: what it asks is laid in the bowl; anything else — the book on «Шлях Одіна»
        internal static bool Use(Player p, NestView v)
        {
            if (!Running(p)) return false;
            if (Errands.Of(p).Current is BringStep)
            {
                string why = Errands.HandIn(p);
                if (why == null) { RavenMsg.Center(p, Lang.T("Одін прийняв.")); return true; }
                RavenMsg.Center(p, why);
            }
            RavenBook.OpenPath();
            return true;
        }

        // ---------------------------------------------------------------- the end of a running one
        internal static void Passed(Player p, bool munin)
        {
            if (p == null) return;
            p.m_customData[Key] = "done";
            ItemDrop.ItemData item = RavenItem.NewRaven(munin);
            if (item != null)
            {
                Trust.Promise(item, p);
                if (!p.GetInventory().AddItem(item)) ItemDrop.DropItem(item, 1, p.transform.position + p.transform.forward + Vector3.up, Quaternion.identity); // a full bag: at the feet
            }
            RavenFx.Play("sfx_raven_kaw", p.transform.position + Vector3.up * 2f, Quaternion.identity);
            RavenMsg.Center(p, Lang.F("Одін дотримав слова: {0} тепер твій. Він ще спить.", Plugin.RavenName(munin)));
            Errands.Log("trial passed by " + p.GetPlayerName() + ": " + (munin ? "Munin" : "Hugin") + " given");
        }

        // given up or out of time (why null: given up)
        internal static void Failed(Player p, string why)
        {
            if (p == null || State(p) == "failed") return;
            p.m_customData[Key] = "failed";
            RavenMsg.Center(p, why != null ? Lang.F("Випробування провалено: {0}. Одін пішов.", why) : Lang.T("Ти відмови{вся|лася} від випробування. Одін пішов."));
        }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven trial · done · fail (a running one from an old save)
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            ErrandRun r = Errands.Of(p);
            switch (what)
            {
                case "done": if (!Running(p)) return "Випробування нема."; r.Step = r.Steps.Length; Errands.Keep(p, r); return "Буде виконано за мить.";
                case "fail": if (!Running(p)) return "Випробування нема."; r.Given = WorldTime.Now - DaysToDo * DayLen - 1; Errands.Keep(p, r); return "Час вийде за мить.";
            }
            return "стан «" + State(p) + "», триває: " + Running(p) + (Running(p) ? ", ще " + RavenAbilities.Span(Mathf.Max(0f, TimeLeft(r))) : "") + "\nraven trial done | fail";
        }
    }

    // the nest piece only once Eikthyr is beaten (its antlers are in the cost too)
    [HarmonyLib.HarmonyPatch(typeof(Player), "HaveRequirements", new System.Type[] { typeof(Piece), typeof(Player.RequirementMode) })]
    internal static class NestNeedsEikthyr
    {
        private static void Postfix(Player __instance, Piece piece, ref bool __result)
        {
            if (!__result || piece == null || !piece.name.StartsWith("RavenWatch_nest_", System.StringComparison.Ordinal)) return;
            if (!Stage.Has(__instance, Trial.BossKey)) __result = false;
        }
    }
}
#endif
