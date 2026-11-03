#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Settlers (npc-plan.md, «згенеровані»): plain people of the world, each its own — the story's people are written by
    // hand (NpcDefs), these come of a template marked «generated: yes» and get, once, where they are owned:
    //   a man or a woman, a name of the north (the story's own names left out), an age — young, grown, old — and by it
    //   the look: skin, hair and its colour (grey with years), a beard for the men; and their ways (NpcPersona: the old
    //   walk slower and stoop, the young are brisk)
    //   clothes only, never armour (the user): Hildir's shirts and dresses, now and then a plain cape, a hat, or rags
    // Kept in its ZDO (KName, KLook: small strings): every game shows the same person; the owner's dressing puts it on.
    // Universal: any job, the guard's too (its fighting by its role: NpcWorker.SetJob); no knack of its own (NpcKnack).
    internal static class NpcSettlers
    {
        internal static readonly int KName = "RavenWatch_npcName".GetStableHashCode(); // string: its own name
        internal static readonly int KLook = "RavenWatch_npcLook".GetStableHashCode(); // string: its look and age

        internal sealed class Look
        {
            public int Model, Age; // Age: 0 young · 1 grown · 2 old
            public Vector3 Skin, HairColor;
            public string Hair = "", Beard = "";
            public readonly List<string> Gear = new List<string>();
        }

        private static readonly string[] Men = { "Ейнар", "Гуннар", "Б'єрн", "Ульф", "Сігурд", "Торстейн", "Гаральд", "Рагнар", "Лейф", "Свен", "Кнуд", "Арне", "Торкель", "Інгвар", "Гальвдан", "Асгейр", "Бруні", "Даг", "Ейвінд", "Гейр", "Грім", "Гудмунд", "Гаук", "Івар", "Кетіль", "Колль", "Одд", "Орм", "Рольф", "Снорре", "Стейн", "Торбйорн", "Трюгве", "Вальгард", "Агні", "Фроді", "Гаскульд", "Отар", "Скегі", "Вермунд" };
        private static readonly string[] Women = { "Астрід", "Сігрун", "Інгрід", "Тове", "Гельга", "Рагнгільд", "Фрейдіс", "Сольвейг", "Тюра", "Ауд", "Бера", "Брюнгільд", "Дагні", "Ейдіс", "Ерна", "Грета", "Інга", "Йорунн", "Лів", "Оса", "Руна", "Сігне", "Свангільд", "Торгільд", "Торунн", "Ульфгільд", "Вальдіс", "Відіс", "Аслауг", "Боргільд", "Герд", "Гердіс", "Альфгільд", "Гудню", "Сіґні", "Тордіс", "Унн", "Ейр", "Фріда", "Гальдора" };
        private static readonly Vector3[] Skins = { new Vector3(0.98f, 0.85f, 0.74f), new Vector3(0.92f, 0.78f, 0.66f), new Vector3(0.85f, 0.72f, 0.6f), new Vector3(0.76f, 0.61f, 0.49f), new Vector3(0.62f, 0.47f, 0.36f), new Vector3(0.47f, 0.34f, 0.26f) };
        private static readonly Vector3[] Hairs = { new Vector3(0.85f, 0.72f, 0.45f), new Vector3(0.72f, 0.58f, 0.36f), new Vector3(0.6f, 0.45f, 0.28f), new Vector3(0.45f, 0.34f, 0.22f), new Vector3(0.2f, 0.15f, 0.1f), new Vector3(0.08f, 0.07f, 0.06f), new Vector3(0.62f, 0.28f, 0.12f), new Vector3(0.5f, 0.22f, 0.12f) };
        private static readonly Vector3[] Greys = { new Vector3(0.75f, 0.75f, 0.72f), new Vector3(0.9f, 0.9f, 0.88f), new Vector3(0.6f, 0.58f, 0.55f) };

        // ---------------------------------------------------------------- made once (where it is owned, before its dressing)
        internal static void Make(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || !a.Owned || z.GetString(KLook, "").Length > 0) return;
            System.Random rnd = new System.Random((int)(z.m_uid.ID * 2654435761u) ^ (int)z.m_uid.UserID ^ Environment.TickCount);
            Look l = new Look();
            l.Model = rnd.NextDouble() < 0.5 ? 0 : 1;
            double age = rnd.NextDouble();
            l.Age = age < 0.3 ? 0 : age < 0.82 ? 1 : 2;
            string[] names = l.Model == 0 ? Men : Women;
            string name = names[rnd.Next(names.Length)];
            l.Skin = Skins[rnd.Next(Skins.Length)];
            l.HairColor = l.Age == 2 && rnd.NextDouble() < 0.8 ? Greys[rnd.Next(Greys.Length)] : Hairs[rnd.Next(Hairs.Length)];
            List<string> hair = Existing("Hair", 1, 40), beard = Existing("Beard", 1, 40);
            bool bald = l.Model == 0 && l.Age == 2 && rnd.NextDouble() < 0.3;
            if (!bald && hair.Count > 0) l.Hair = hair[rnd.Next(hair.Count)];
            if (l.Model == 0 && beard.Count > 0 && rnd.NextDouble() < (l.Age == 0 ? 0.45 : 0.9)) l.Beard = beard[rnd.Next(beard.Count)];
            // clothes only: a shirt or a dress (rags for a poor one), sometimes rag trousers, a plain cape, a hat
            List<string> tunics = Existing("ArmorTunic", 1, 10), dresses = Existing("ArmorDress", 1, 10), hats = Existing("HelmetHat", 1, 10), capes = Some("CapeDeerHide", "CapeLinen");
            double poor = rnd.NextDouble();
            if (poor < 0.12 || (tunics.Count == 0 && dresses.Count == 0)) { l.Gear.AddRange(Some("ArmorRagsChest", "ArmorRagsLegs")); }
            else
            {
                List<string> top = l.Model == 1 && dresses.Count > 0 && rnd.NextDouble() < 0.8 ? dresses : (tunics.Count > 0 ? tunics : dresses);
                l.Gear.Add(top[rnd.Next(top.Count)]);
                if (top == tunics && rnd.NextDouble() < 0.4) l.Gear.AddRange(Some("ArmorRagsLegs"));
            }
            if (capes.Count > 0 && rnd.NextDouble() < 0.35) l.Gear.Add(capes[rnd.Next(capes.Count)]);
            if (hats.Count > 0 && rnd.NextDouble() < 0.2) l.Gear.Add(hats[rnd.Next(hats.Count)]);
            z.Set(KName, name);
            z.Set(KLook, Write(l));
            a.LookKept = null;
            Npcs.Log("a settler made: " + name + " (" + (l.Model == 0 ? "man" : "woman") + ", " + AgeWord(l.Age) + "): " + string.Join(" ", l.Gear.ToArray()));
        }

        private static List<string> Existing(string start, int from, int to)
        {
            List<string> l = new List<string>();
            if (ObjectDB.instance == null) return l;
            for (int i = from; i <= to; i++) if (ObjectDB.instance.GetItemPrefab(start + i) != null) l.Add(start + i);
            return l;
        }

        private static List<string> Some(params string[] names)
        {
            List<string> l = new List<string>();
            if (ObjectDB.instance != null) foreach (string n in names) if (ObjectDB.instance.GetItemPrefab(n) != null) l.Add(n);
            return l;
        }

        // ---------------------------------------------------------------- kept and read
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string V(Vector3 v) { return v.x.ToString("0.###", Inv) + "," + v.y.ToString("0.###", Inv) + "," + v.z.ToString("0.###", Inv); }

        private static string Write(Look l)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("m=").Append(l.Model).Append(";a=").Append(l.Age).Append(";s=").Append(V(l.Skin)).Append(";c=").Append(V(l.HairColor));
            sb.Append(";h=").Append(l.Hair).Append(";b=").Append(l.Beard).Append(";g=").Append(string.Join(",", l.Gear.ToArray()));
            return sb.ToString();
        }

        // its look (null: not a settler, or not made yet)
        internal static Look Of(NpcActor a)
        {
            if (a == null || a.Def == null || !a.Def.Generated) return null;
            ZDO z = a.Zdo;
            string s = z != null ? z.GetString(KLook, "") : "";
            if (s.Length == 0) return null;
            if (a.LookKept != null && a.LookRaw == s) return a.LookKept;
            Look l = new Look();
            foreach (string part in s.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                switch (k)
                {
                    case "m": l.Model = Errands.I(v, 0); break;
                    case "a": l.Age = Errands.I(v, 1); break;
                    case "s": l.Skin = Vec(v); break;
                    case "c": l.HairColor = Vec(v); break;
                    case "h": l.Hair = v; break;
                    case "b": l.Beard = v; break;
                    case "g": foreach (string g in v.Split(',')) if (g.Length > 0) l.Gear.Add(g); break;
                }
            }
            a.LookKept = l; a.LookRaw = s;
            return l;
        }

        private static Vector3 Vec(string v)
        {
            string[] p = v.Split(',');
            float x, y, zz;
            if (p.Length < 3 || !float.TryParse(p[0], NumberStyles.Float, Inv, out x) || !float.TryParse(p[1], NumberStyles.Float, Inv, out y) || !float.TryParse(p[2], NumberStyles.Float, Inv, out zz)) return new Vector3(0.85f, 0.72f, 0.6f);
            return new Vector3(x, y, zz);
        }

        internal static string NameOf(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? z.GetString(KName, "") : ""; }

        // its age in its ways (over its own: NpcPersona)
        internal static string Ways(NpcActor a)
        {
            Look l = Of(a);
            if (l == null) return "";
            if (l.Age == 1) return "";
            ZDO z = a.Zdo;
            float j = z != null ? ((z.m_uid.ID * 2654435761u) % 1000u) / 1000f : 0.5f; // (0…1, its own)
            CultureInfo ci = CultureInfo.InvariantCulture;
            if (l.Age == 0) return "walk=" + (1.05f + j * 0.1f).ToString("0.###", ci) + " tempo=" + (1.03f + j * 0.07f).ToString("0.###", ci) + " stoop=" + (j * 0.08f).ToString("0.###", ci);
            return "walk=" + (0.76f + j * 0.12f).ToString("0.###", ci) + " tempo=" + (0.82f + j * 0.08f).ToString("0.###", ci) + " stoop=" + (0.45f + (1f - j) * 0.3f).ToString("0.###", ci);
        }

        internal static string AgeWord(int age) { return Lang.T(age == 0 ? "молодий|молода" : age == 2 ? "старий|стара" : "дорослий|доросла"); }
    }

    // A person's knack (npc-plan.md, «хист»): how good it is at a job or a way of fighting, ×0.5 … ×2 — the story's people
    // have theirs in their template («knack: cook=1.5 guard=0.8 melee=1.3»), the story may change them (NpcLevers
    // «knack»); a settler has none (×1 at everything). Keys: the jobs' ids (stoker smelter gatherer cleaner cook keeper
    // guard) and melee, bow. A job's knack: its pace at it; a guard's: how far it looks out; melee/bow: its blows'
    // weight (and a bow drawn quicker).
    internal static class NpcKnack
    {
        internal static readonly int KKnack = "RavenWatch_npcKnack".GetStableHashCode(); // string "cook=1.5 bow=0.8": the story's
        internal const float Min = 0.5f, Max = 2f;
        internal static readonly string[] Keys = { "stoker", "smelter", "gatherer", "cleaner", "cook", "keeper", "guard", "melee", "bow" };

        internal static float Of(NpcActor a, string key)
        {
            if (a == null || string.IsNullOrEmpty(key)) return 1f;
            ZDO z = a.Zdo;
            string story = z != null ? z.GetString(KKnack, "") : "";
            if (story.Length > 0)
                foreach (string pair in story.Split(' '))
                {
                    int eq = pair.IndexOf('=');
                    float v;
                    if (eq > 0 && pair.Substring(0, eq) == key && float.TryParse(pair.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return Mathf.Clamp(v, Min, Max);
                }
            float d;
            float own = a.Def != null && a.Def.Knack.TryGetValue(key, out d) ? d : 1f;
            return Mathf.Clamp(own * (1f + NpcBackstory.Knack(a, key)), Min, Max); // (what it says it can do: NpcBackstory)
        }

        // the story's change (NpcLevers «knack <key> <value|+x|-x>»)
        internal static string Set(NpcActor a, string key, string value)
        {
            if (Array.IndexOf(Keys, key) < 0) return "нема такого хисту: " + key + " (" + string.Join(", ", Keys) + ")";
            float now = Of(a, key), v;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return "хист: число";
            if (value[0] == '+' || value[0] == '-') v = now + v;
            v = Mathf.Clamp(v, Min, Max);
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (string pair in a.Zdo.GetString(KKnack, "").Split(' ')) { int eq = pair.IndexOf('='); if (eq > 0) d[pair.Substring(0, eq)] = pair.Substring(eq + 1); }
            d[key] = v.ToString("0.##", CultureInfo.InvariantCulture);
            List<string> l = new List<string>();
            foreach (KeyValuePair<string, string> e in d) l.Add(e.Key + "=" + e.Value);
            a.Zdo.Set(KKnack, string.Join(" ", l.ToArray()));
            return "хист " + key + " " + now.ToString("0.##", CultureInfo.InvariantCulture) + " → " + v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // for its card: what it is good or poor at (none: «на все руки»)
        internal static string Describe(NpcActor a)
        {
            List<string> good = new List<string>(), poor = new List<string>();
            foreach (string k in Keys)
            {
                float v = Of(a, k);
                string w = k == "melee" ? Lang.T("ближній бій") : k == "bow" ? Lang.T("лук") : Jobs.Word(k);
                if (v >= 1.15f) good.Add(w + (v >= 1.6f ? Lang.T(" (майстер)") : ""));
                else if (v <= 0.85f) poor.Add(w);
            }
            if (good.Count == 0 && poor.Count == 0) return Lang.T("на все руки — без особливого хисту");
            string s = good.Count > 0 ? Lang.T("вдається: ") + string.Join(", ", good.ToArray()) : "";
            if (poor.Count > 0) s += (s.Length > 0 ? "; " : "") + Lang.T("не його: ") + string.Join(", ", poor.ToArray());
            return s;
        }
    }

    // a person's blow weighs by its knack (melee, bow) — where it strikes (its owner's game)
    [HarmonyLib.HarmonyPatch(typeof(Character), "Damage")]
    internal static class NpcKnackHitPatch
    {
        private static void Prefix(HitData hit)
        {
            if (hit == null || NpcActor.Live.Count == 0) return;
            try
            {
                NpcActor a = NpcActor.Of(hit.GetAttacker());
                if (a == null) return;
                float k = NpcKnack.Of(a, hit.m_ranged ? "bow" : "melee");
                if (Mathf.Abs(k - 1f) > 0.01f) hit.m_damage.Modify(k);
            }
            catch (System.Exception e) { PatchGuard.Fail("NpcKnackHitPatch", e); }
        }
    }
}
#endif
