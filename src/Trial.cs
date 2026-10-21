#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Випробування Одіна»: a raven for free. Who has no raven yet and has seen Eikthyr fall builds the nest and calls
    // Odin from its bowl (no ravens to sit in it: he comes by himself). Under his glow, E on the bowl takes the trial —
    // Hugin's peaceful one or Munin's bloody one (by chance), an errand of kind «trial» (Errands). Done within its time:
    // Odin gives that raven (asleep, like one from Haldor). Given up or out of time: he goes, and never gives it again;
    // the Guide then sends you to Haldor. Once per character, kept in its customData.
    internal static class Trial
    {
        internal const string Key = "RavenWatch_trial"; // "" not yet · done · failed
        internal const string BossKey = "defeated_eikthyr";
        internal static float DaysToDo = 3f;               // game days the trial lasts
        internal static float ComeMin = 8f, ComeMax = 15f; // s after the call before Odin comes
        private const float StayRange = 25f;              // m: walk further and he leaves the nest (call again)

        private static NestView _calling, _over;
        private static float _comeAt;

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

        // Odin may come with the trial: not had it, Eikthyr beaten, no raven, no other errand
        internal static bool Open(Player p) { return p != null && !Over(p) && !HadRaven(p) && Stage.Has(p, BossKey) && !HasRaven(p) && Errands.Of(p) == null; }

        // a raven of your own was ever with you (Perks.Sync marks it): one put away in a chest does not open the trial
        internal static bool HadRaven(Player p) { return p != null && p.m_customData.ContainsKey(HadKey); }
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
        // the hover line, or null when the trial has nothing to say here
        internal static string Hover(Player p, NestView v)
        {
            if (Running(p))
                return "[<color=yellow><b>$KEY_Use</b></color>] " + (Errands.Of(p).Current is BringStep ? Lang.T("Покласти в чашу для випробування") : Lang.T("Подивитися випробування"));
            if (!Open(p)) return null;
            if (_over == v) return "[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Прийняти випробування Одіна");
            if (_calling == v) return "<color=#9A9A9A>" + Lang.T("Ти кличеш Одіна…") + "</color>";
            return "[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Покликати Одіна");
        }

        // one with no raven at all, on the nest (its top or its bowl): the trial's line, or why there is nothing to do
        internal static string NoRavenHover(Player p, NestView v)
        {
            string t = Hover(p, v);
            if (t != null) return t;
            if (!Stage.Has(p, BossKey)) return "<color=#9A9A9A>" + Lang.T("Одін прийде лише до того, хто здолав Ейктюра.") + "</color>";
            if (State(p) == "failed") return "<color=#9A9A9A>" + Lang.T("Одін більше не дасть випробування. Ворона продає Хальдор.") + "</color>";
            return "<color=#9A9A9A>" + Lang.T("Посади сюди своїх воронів, коли вони будуть.") + "</color>";
        }

        internal static void NoRavenUse(Player p, NestView v)
        {
            if (Use(p, v)) return;
            string t = NoRavenHover(p, v);
            int a = t.IndexOf('>'), b = t.LastIndexOf('<');
            RavenMsg.Center(p, a >= 0 && b > a ? t.Substring(a + 1, b - a - 1) : t);
        }

        // E on the bowl; true when the trial took it
        internal static bool Use(Player p, NestView v)
        {
            // during the trial: what it asks is laid in the bowl; anything else — the book on «Шлях Одіна», where it all stands
            if (Running(p))
            {
                if (Errands.Of(p).Current is BringStep)
                {
                    string why = Errands.HandIn(p);
                    if (why == null) { RavenMsg.Center(p, Lang.T("Одін прийняв.")); return true; }
                    RavenMsg.Center(p, why);
                }
                RavenBook.OpenPath();
                return true;
            }
            if (!Open(p)) return false;
            if (_over == v) { Accept(p, v); return true; }
            if (_calling == v) return true;
            _calling = v; _comeAt = Time.time + Random.Range(ComeMin, ComeMax);
            RavenFx.Play("sfx_raven_kaw", v.Seat, Quaternion.identity);
            RavenMsg.Center(p, Lang.T("Ти кличеш Одіна. Зачекай біля гнізда."));
            return true;
        }

        private static void Accept(Player p, NestView v)
        {
            bool munin = Random.value < 0.5f;
            ErrandDef d = Errands.Find(munin ? "trial_munin" : "trial_hugin");
            Glow(p, null);
            if (d == null) { RavenMsg.Center(p, Lang.T("Одін мовчить.")); return; }
            v.Cloud();
            RavenBook.GiveFromOdin(p);
            string why = Errands.GiveNow(p, d, munin, null);
            if (why != null) { RavenMsg.Center(p, why); return; }
            RavenBook.OpenPath(); // Odin's book, open at once on what he wants
        }

        // twice a second (Errands.Tick): Odin comes to the call, leaves when you walk away
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            NestView at = _over ?? _calling;
            if (at == null) return;
            if (p == null || !at.Valid || !Open(p) || Vector3.Distance(p.transform.position, at.transform.position) > StayRange)
            {
                if (_over != null && p != null) RavenMsg.Center(p, Lang.T("Сяйво згасло: Одін пішов від гнізда. Поклич знову, коли будеш готов{ий|а}."));
                _calling = null;
                Glow(p, null);
                return;
            }
            if (_calling != null && Time.time >= _comeAt)
            {
                NestView v = _calling;
                _calling = null;
                Glow(p, v);
                RavenFx.Play("sfx_raven_kaw", v.Seat, Quaternion.identity);
                RavenMsg.Center(p, Lang.T("Над гніздом золоте сяйво: Одін прийшов. E на чашу — прийняти випробування."));
            }
        }

        private static void Glow(Player p, NestView v)
        {
            if (_over == v) return;
            if (_over != null && _over.Valid && p != null) _over.SendTrialGlow(p.GetPlayerID(), false);
            _over = v;
            if (v != null && p != null) v.SendTrialGlow(p.GetPlayerID(), true);
        }

        // ---------------------------------------------------------------- the end of it
        internal static void Passed(Player p, bool munin)
        {
            if (p == null) return;
            p.m_customData[Key] = "done";
            GameObject prefab = munin ? RavenItem.Munin : RavenItem.Hugin;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop != null)
            {
                ItemDrop.ItemData item = drop.m_itemData.Clone();
                item.m_dropPrefab = prefab;
                item.m_stack = 1;
                item.m_customData = new Dictionary<string, string>(item.m_customData); // (never the prefab's own)
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
            Guide.Queue(p, Guide.TrialLost, 20f);
        }

        internal static void Reset() { _calling = null; _over = null; }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven trial · reset · come · done · fail
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            ErrandRun r = Errands.Of(p);
            switch (what)
            {
                case "reset": if (Running(p)) Errands.Drop(p); p.m_customData.Remove(Key); p.m_customData.Remove(HadKey); Reset(); return "Випробування знову можливе.";
                case "come": if (_calling == null) return "Спершу поклич Одіна з чаші."; _comeAt = 0f; return "Одін прийде зараз.";
                case "done": if (!Running(p)) return "Випробування нема."; r.Step = r.Steps.Length; Errands.Keep(p, r); return "Буде виконано за мить.";
                case "fail": if (!Running(p)) return "Випробування нема."; r.Given = WorldTime.Now - DaysToDo * DayLen - 1; Errands.Keep(p, r); return "Час вийде за мить.";
            }
            return "стан «" + State(p) + "», відкрите: " + Open(p) + ", триває: " + Running(p) + (Running(p) ? ", ще " + RavenAbilities.Span(Mathf.Max(0f, TimeLeft(r))) : "") + "\nraven trial reset | come | done | fail";
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
