#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The Guide («Провідник»): a red raven, calm and plain, the one raven you talk to (E). He tells a new player about
    // each part of the ravens at the moment it becomes theirs: one topic a visit, short. A topic is done only once the
    // player has talked to him; until then he comes back after RetrySeconds. Each topic once per character.
    // Most topics are noticed by the Guide himself (When, checked on the plugin's tick); a few are sent from the place
    // they happen (Queue: bought, woke, death, the trial lost). Several waiting: the most needed first (the list order).
    internal static class Guide
    {
        internal static bool Enabled = true;
        internal static float RetrySeconds = 720f; // world seconds between tries while he is not talked to
        internal static float GapSeconds = 600f;   // world seconds after a talk before the next topic
        private const string QueueKey = "RavenWatch_guideQueue", DoneKey = "RavenWatch_guideDone", AtKey = "RavenWatch_guideAt", VerKey = "RavenWatch_guideV";
        internal const string Who = "guide", Name = "Провідник";

        internal const int Bought = 1, Woke = 2, Gear = 3, NestTrial = 4, TrialLost = 5, Gifts = 6, Food = 7, Orders = 8, Perches = 9,
            Strong = 10, Rune = 11, Gold = 13, Death = 14, Lost = 15, Mail = 16, NestErrands = 17, Binds = 18; // (12: the old tree topic, gone)

        private sealed class Topic
        {
            public int Id; public string Title, Text;
            public Func<Player, bool> When; // null: sent from elsewhere (Queue)
            public bool Known;              // a player who already had a woken raven before this Guide knows it (told silently)
        }

        // in the order of need: when several wait, the first of them comes
        private static readonly List<Topic> Topics = new List<Topic>
        {
            new Topic { Id = Bought, Known = true, Title = Lang.K("Ворон спить"),
                Text = Lang.K("Ворон ще спить і тебе не знає.\n\nЩоб збудити: повісь трофей Ейктюра на його Жертовний камінь, візьми ще {wake} і проведи обряд у книзі (B → «Розвиток»).\n\nНа плече — ПКМ по ворону в сумці. Хугін сідає праворуч, Мунін — ліворуч.\n\nНе до вподоби благо чи тягар — Камінь долі ворона (у Хальдора) їх перекине.") },
            new Topic { Id = TrialLost, Title = Lang.K("Одін пішов"),
                Text = Lang.K("Випробування не виконано. Вдруге Одін його не дасть.\n\nВорона тепер можна лише купити — у Хальдора.\n\nГніздо не розбирай: з вороном у ньому Одін даватиме доручення.") },
            new Topic { Id = NestTrial, Title = Lang.K("Гніздо для Одіна"),
                When = p => Trial.Open(p),
                Text = Lang.K("Ейктюр упав — тепер можна звести Вороняче гніздо: молоток → RavenWatch. Окрім дерева, живиці й пір'я, треба 2 роги Ейктюра.\n\nЗбудуй і натисни E на гніздо — Одін прийде з випробуванням і дасть тобі книгу воронів (B): там видно, що робити й скільки лишилось. Виконаєш вчасно — дасть ворона.\n\nНе хочеш чекати — у Хальдора ворони продаються.") },
            new Topic { Id = Woke, Known = true, Title = Lang.K("Ворон твій"),
                Text = Lang.K("Ворон прокинувся і тепер твій. Чужих він клює.\n\nКнига воронів — B: стан, дари, довіра, образ.\n\nСпершу він дичиться й інколи відлітає. Звикне.") },
            new Topic { Id = Gifts, Known = true, Title = Lang.K("Дари і сила"),
                When = p => Done(p, Woke) && TopLevel(p) >= 1,
                Text = Lang.K("Дари — у колі Q. Кожен коштує сили.\n\nОбидва ворони щодня набирають трохи самі. Хугін — ще в мирі й роботі, Мунін — у бою.\n\nУдари ворона ростуть із вишколом: B → «Розвиток».") },
            new Topic { Id = Food, Known = true, Title = Lang.K("Ворон голодний"),
                When = p => Hungry(p),
                Text = Lang.K("Ворон голодний. Хугін їсть ягоди, мед, гриби й рибу, Мунін — сире м'ясо. Підходить лише їжа його рівня — яка, видно в книзі.\n\nГодуй з кола J («Погодувати»). Їжа — головний шлях до довіри. Улюблену він має свою й сам натякне на неї.\n\nГолодний ворон краде їжу із сумки, а на нулі слабне й клює тебе.") },
            new Topic { Id = Death, Known = true, Title = Lang.K("Смерть"),
                Text = Lang.K("Смерть забирає в ворона довіру (на 1–2 рівнях — менше).\n\nВорони лягають у могилу з речами й стережуть її. Ворон із плеча, щойно забереш його, сяде назад на плече. Прийдеш по нього за 10 хвилин — він поверне половину втраченої довіри.\n\nЗагубив ворона зовсім — поверни його обрядом: B → «Ворони», сторінка ворона. Раз на 5 днів.") },
            new Topic { Id = Lost, Title = Lang.K("Чужий ворон"),
                When = p => HoldsStranger(p),
                Text = Lang.K("Цей ворон не твій. Дари він дає, але не росте, нічого не пам'ятає й клює тебе.\n\nСвого загубленого ворона поверни обрядом: B → «Ворони», сторінка ворона. Старий тоді розсиплеться пір'ям.") },
            new Topic { Id = Orders, Title = Lang.K("Накази"),
                When = p => TopLevel(p) >= 2,
                Text = Lang.K("Тепер ворон слухає наказів — коло J.\n\n«Туди»: наведи приціл на гравця, тварину, річ чи будову, і ворон полетить туди. Ще є «Поруч», «Кружляй», трюки й «Усіх назад».\n\nЯкщо ситість нижча за 15, ворон наказів не виконує.") },
            new Topic { Id = Gear, Title = Lang.K("Спорядження"),
                When = p => TopLevel(p) >= 3,
                Text = Lang.K("Ворон дасть себе вбрати: B → «Образ». Чотири місця — голова, шия, спина, лапи. Кожна річ дає свою силу.\n\nРечі роблять на столах ворона (молоток → RavenWatch): простий — біля кузні, просунутий — біля столу ґалдра, його речі — з 5-го рівня.\n\nТорба на спині годує ворона сама.") },
            new Topic { Id = Perches, Title = Lang.K("Жердини"),
                When = p => Done(p, Gear),
                Text = Lang.K("Молоток → RavenWatch: жердини для воронів.\n\nE біля жердини садить Хугіна, Shift+E — Муніна. Там ворон відпочиває й набирається сили. Під дахом і в затишку — швидше.\n\nПокликати його назад можна звідусіль.") },
            new Topic { Id = NestErrands, Title = Lang.K("Доручення Одіна"),
                When = p => TopLevel(p) >= 1 && Stage.Has(p, Trial.BossKey),
                Text = Lang.K("Посади воронів у Вороняче гніздо (E угорі), поклади дари в чашу й поклич Одіна. Він дає доручення — раз на день.\n\nГнізда нема — молоток → RavenWatch, треба ще 2 роги Ейктюра.\n\nВиконані доручення запалюють руни. Усе — у книзі (B), «Шлях Одіна».") },
            new Topic { Id = Strong, Title = Lang.K("Ворон береже тебе"),
                When = p => TopLevel(p) >= 4,
                Text = Lang.K("Тепер ворон береже тебе в скруті: Хугін рятує від смертельного удару, Мунін люто б'ється, коли тобі погано.\n\nДалі — більші дари, а з 6-го рівня Крила Одіна і Звір Муніна.\n\nДорогу ти вже знаєш. Прилітатиму рідше.") },
            new Topic { Id = Rune, Title = Lang.K("Перша руна"),
                When = p => RuneBook.Of(p).Opened >= 1,
                Text = Lang.K("Загорілася руна. Руни Фреї фарбують ворона, руни Бальдра дають ефект, руни Геймдаля — слід у польоті.\n\nПовісити руну: B → «Образ», ромби довкола ворона. Усі руни — у «Шляху Одіна».\n\nРуна твоя, не ворона: віддаси ворона — він її не понесе.") },
            new Topic { Id = Binds, Title = Lang.K("Воронячі в'язі"),
                When = p => FoundTree(p) && RuneBook.Of(p).Opened >= 2,
                Text = Lang.K("Ти знайшов Вороняче дерево. Біля його коріння руни сплітаються у в'язь — силу ворона.\n\nРуни Бальдра кажуть, що станеться, руни Геймдаля — коли, руни Фреї — як сильно. Зелене дерево бере 2 руни, крижане — 3, горіле — 4. Слухає воно лише ворона з довірою 2, 4 чи 6.\n\nЩоб сплести — встій на арені довкола дерева. В'язь у тебе одна; вмикає й вимикає її коло Q.") },
            new Topic { Id = Gold, Title = Lang.K("Золото Одіна"),
                When = p => Golden(p),
                Text = Lang.K("Одін кличе на золоті доручення. За кожне — золота руна.\n\nЇї вкладають у дар ворона назавжди: дар сильнішає, дешевшає, а на третьому рівні отримує свою грань.\n\nДо трьох рівнів на дар, до п'яти золотих рун на ворона.") },
            new Topic { Id = Mail, Title = Lang.K("Пошта воронів"),
                When = p => OtherRavenNear(p) && Post.LockedWhy(p) == null && (Trust.Worn(false) != null || Trust.Worn(true) != null), // (a raven of yours old enough to carry a letter)
                Text = Lang.K("Поруч гравець із воронами. Йому можна слати листи й посилки.\n\nКонверт у кутку книги (B). Лист — 20 сили й пергамент (у Хальдора), посилка — 30 сили й одна річ до 2 кг, з 3-го рівня довіри ворона.") },
        };

        private static Topic Find(int id) { foreach (Topic t in Topics) if (t.Id == id) return t; return null; }

        private static int _current; // the topic on its way now, 0 for none

        // ---------------------------------------------------------------- what he notices
        private static List<ItemDrop.ItemData> Owned(Player p)
        {
            List<ItemDrop.ItemData> l = new List<ItemDrop.ItemData>();
            if (p == null) return l;
            bool m;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems()) if (RavenItem.IsRaven(item, out m) && !Trust.Stranger(item, p)) l.Add(item);
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p)) if (!l.Contains(item) && RavenItem.IsRaven(item, out m) && !Trust.Stranger(item, p)) l.Add(item);
            return l;
        }

        // the trust level of your best raven (0: none woken)
        private static int TopLevel(Player p)
        {
            int best = 0;
            foreach (ItemDrop.ItemData item in Owned(p)) best = Mathf.Max(best, Trust.SettledLevel(item));
            return best;
        }

        private static bool Hungry(Player p)
        {
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Trust.Worn(m);
                if (item != null && Trust.Active(item) && !Trust.Stranger(item, p) && Satiety.Zone(item) <= Satiety.Hungry) return true;
            }
            return false;
        }

        private static bool HoldsStranger(Player p)
        {
            bool m;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems()) if (RavenItem.IsRaven(item, out m) && Trust.Stranger(item, p)) return true;
            return false;
        }

        private static bool FoundTree(Player p) { string raw; return p.m_customData.TryGetValue(RavenTree.KnownKey, out raw) && raw.Replace("v2|", "").Trim('|').Length > 0; }

        private static bool Golden(Player p)
        {
            RuneBook.Data d = RuneBook.Of(p);
            if (d.Gold > 0 || d.GoldEver > 0) return true;
            ErrandRun r = Errands.Of(p);
            return r != null && r.Def != null && r.Def.Golden;
        }

        // another player near with a raven on the shoulder: he has the mod (the post is for him)
        private static bool OtherRavenNear(Player p)
        {
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == p || Vector3.Distance(o.transform.position, p.transform.position) > 40f) continue;
                ZNetView nv = o.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid() && nv.GetZDO().GetInt("RavenWatch_Shoulder", 0) != 0) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- the lists in the character
        private static List<int> Read(Player p, string key)
        {
            List<int> list = new List<int>();
            string raw;
            if (p == null || !p.m_customData.TryGetValue(key, out raw) || string.IsNullOrEmpty(raw)) return list;
            foreach (string part in raw.Split(','))
            {
                int n;
                if (int.TryParse(part, out n)) list.Add(n);
            }
            return list;
        }

        private static void Write(Player p, string key, List<int> list)
        {
            List<string> parts = new List<string>();
            foreach (int n in list) parts.Add(n.ToString());
            p.m_customData[key] = string.Join(",", parts.ToArray());
        }

        private static bool Done(Player p, int id) { return Read(p, DoneKey).Contains(id); }

        internal static bool IsGuide(string key) { return key == Plugin.KeyPrefix + Who; }

        // a topic to tell, not before delaySeconds (world time; a longer wait already set stays), once per character
        internal static void Queue(Player p, int id, float delaySeconds)
        {
            if (!Enabled || p == null || p != Player.m_localPlayer || Find(id) == null) return;
            if (Read(p, DoneKey).Contains(id)) return;
            List<int> q = Read(p, QueueKey);
            if (q.Contains(id)) return;
            q.Add(id);
            Write(p, QueueKey, q);
            if (WorldTime.Left(p.m_customData, AtKey) < delaySeconds) WorldTime.Set(p.m_customData, AtKey, delaySeconds);
        }

        // the player died: with a raven of his on the shoulder, the Guide tells about it after
        internal static void Died(Player p)
        {
            if (p == null || p != Player.m_localPlayer) return;
            foreach (bool m in new bool[] { false, true }) { ItemDrop.ItemData item = Trust.Worn(m); if (item != null && Trust.Active(item) && !Trust.Stranger(item, p)) { Queue(p, Death, 60f); return; } }
        }

        // A player who had woken ravens before this Guide (an older version) knows the start and what is already his:
        // once, those topics are marked told without a word. A new player gets everything.
        private static void CatchUp(Player p)
        {
            string v;
            if (p.m_customData.TryGetValue(VerKey, out v) && v == "2") return;
            p.m_customData[VerKey] = "2";
            bool veteran = TopLevel(p) >= 1;
            if (!veteran) foreach (string k in p.m_customData.Keys) if (k.StartsWith("RavenWatch_copy_")) { veteran = true; break; } // (a woken raven of his, now lost)
            if (!veteran) return;
            List<int> done = Read(p, DoneKey), q = Read(p, QueueKey);
            foreach (Topic t in Topics)
            {
                bool now = false;
                try { now = t.When != null && t.When(p); } catch (Exception) { }
                if ((t.Known || now) && !done.Contains(t.Id)) done.Add(t.Id);
                q.Remove(t.Id);
            }
            Write(p, DoneKey, done);
            Write(p, QueueKey, q);
        }

        private static float _nextLook;

        // from the plugin's tick: the Guide comes with the next topic when the way is clear; true when he was sent.
        // The topic stays in the queue until talked, so if he is ignored he comes again after RetrySeconds.
        internal static bool Tick(Plugin plugin, Player p)
        {
            if (!Enabled || plugin == null || p == null || p.IsDead() || Mute.SelfMuted) return false;
            CatchUp(p);
            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + 5f;
                List<int> done = Read(p, DoneKey), q = Read(p, QueueKey);
                foreach (Topic t in Topics)
                {
                    if (t.When == null || done.Contains(t.Id) || q.Contains(t.Id)) continue;
                    bool now = false;
                    try { now = t.When(p); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Guide " + t.Id + ": " + e.Message); }
                    if (now) Queue(p, t.Id, 20f);
                }
            }
            if (plugin.RavenBusy || p.InInterior() || p.IsSleeping() || Senses.Fight) return false;
            List<int> queue = Read(p, QueueKey);
            if (queue.Count == 0 || WorldTime.Left(p.m_customData, AtKey) > 0f) return false;
            List<int> told = Read(p, DoneKey);
            // "it only sleeps" is no news once the raven has woken
            if (!told.Contains(Bought) && (queue.Contains(Woke) || told.Contains(Woke))) { told.Add(Bought); Write(p, DoneKey, told); }
            Topic next = null;
            foreach (Topic t in Topics) if (queue.Contains(t.Id) && !told.Contains(t.Id)) { next = t; break; }
            if (next == null)
            {
                queue.RemoveAll(id => Find(id) == null || told.Contains(id)); // a topic from an older version, or already told
                Write(p, QueueKey, queue);
                return false;
            }
            WorldTime.Set(p.m_customData, AtKey, Mathf.Max(60f, RetrySeconds));
            _current = next.Id;
            plugin.Visit(false, Lang.T(next.Title), Lang.T(next.Text).Replace("{wake}", Trust.WakeText()), Who);
            return true;
        }

        // what he has told, to read again in the book (RavenBookGuide): title and words, in the topics' order
        internal static List<KeyValuePair<string, string>> Told(Player p)
        {
            List<KeyValuePair<string, string>> l = new List<KeyValuePair<string, string>>();
            if (p == null) return l;
            List<int> done = Read(p, DoneKey);
            foreach (Topic t in Topics)
                if (done.Contains(t.Id)) l.Add(new KeyValuePair<string, string>(Grammar.Fix(Lang.T(t.Title)), Grammar.Fix(Lang.T(t.Text).Replace("{wake}", Trust.WakeText()))));
            return l;
        }

        // the player has talked to him (E): this topic is told
        internal static void Talked()
        {
            Player p = Player.m_localPlayer;
            if (_current == 0 || p == null) return;
            List<int> q = Read(p, QueueKey);
            q.Remove(_current);
            Write(p, QueueKey, q);
            List<int> done = Read(p, DoneKey);
            if (!done.Contains(_current)) done.Add(_current);
            Write(p, DoneKey, done);
            _current = 0;
            WorldTime.Set(p.m_customData, AtKey, Mathf.Max(60f, GapSeconds)); // the next topic not right after this one
        }

        // console "raven guide": everything anew, as for a new player
        internal static string Reset(Player p)
        {
            if (p == null) return Lang.T("персонажа нема");
            p.m_customData.Remove(DoneKey);
            p.m_customData.Remove(QueueKey);
            p.m_customData.Remove(AtKey);
            p.m_customData[VerKey] = "2";
            _current = 0;
            foreach (ItemDrop.ItemData item in Owned(p)) { if (!Trust.Active(item)) Queue(p, Bought, 0f); else Queue(p, Woke, 0f); }
            return Lang.F("Провідник розповість усе знову ({0} тем, коли до них дійде).", Topics.Count);
        }
    }

    // While the Guide's text is on a raven, that raven is crimson (redder than Munin) and named «Провідник» — both in
    // the hover and in the talk window. Material copies, made once per original; the originals come back after.
    internal static class GuideLook
    {
        internal static Color Crimson = new Color(0.85f, 0.06f, 0.1f);
        private static readonly Dictionary<Material, Material> Made = new Dictionary<Material, Material>();
        private class Saved { public string Name; public Renderer[] Renderers; public Material[][] Mats; public int Version; public RavenLooks.Painted Paint; }
        private static readonly Dictionary<Raven, Saved> Worn = new Dictionary<Raven, Saved>();

        internal static void Update(Raven r)
        {
            Raven.RavenText cur = Plugin.CurrentText(r);
            bool guide = cur != null && Guide.IsGuide(cur.m_key);
            Saved s;
            bool on = Worn.TryGetValue(r, out s);
            if (guide && on && s.Version != RavenLooks.Version) { Take(r, s); on = false; } // a look saved in «Фарби»: re-dressed at once
            if (guide && !on) Put(r);
            else if (!guide && on) Take(r, s);
        }

        private static void Put(Raven r)
        {
            Saved s = new Saved { Name = r.m_name, Renderers = r.GetComponentsInChildren<Renderer>(true), Version = RavenLooks.Version };
            s.Mats = new Material[s.Renderers.Length][];
            Look look = null;
            try { look = RavenLooks.ForGuide(); if (look != null) s.Paint = RavenLooks.Use(look, r.m_isMunin); }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Guide look: " + e.Message); }
            for (int i = 0; i < s.Renderers.Length; i++)
            {
                Material[] list = s.Renderers[i].sharedMaterials;
                s.Mats[i] = list;
                // the raven itself wears its look (painted feathers, eyes, sheen); the smoke and the rest keep the crimson tint
                Material[] painted = null;
                if (s.Paint != null && !(s.Renderers[i] is ParticleSystemRenderer)) painted = RavenLooks.Dress(s.Paint, list);
                Material[] tinted = new Material[list.Length];
                for (int j = 0; j < list.Length; j++) tinted[j] = painted != null && RavenLooks.IsBody(list[j]) ? painted[j] : Tint(list[j]);
                s.Renderers[i].sharedMaterials = tinted;
            }
            // the things its look hangs on it (after the renderers above were noted: those are its own)
            if (look != null && (look.Things.Count > 0 || look.Fx.Count > 0))
            {
                int layer = r.m_visual != null ? r.m_visual.layer : -1;
                try { LookThings.Hang(r.gameObject, look, r.m_isMunin, layer); FxLib.Hang(r.gameObject, look.Fx, r.m_isMunin, layer); }
                catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Guide look things: " + e); }
            }
            r.m_name = Lang.W(Guide.Name);
            Worn[r] = s;
        }

        private static void Take(Raven r, Saved s)
        {
            LookThings.Unhang(r.gameObject);
            FxLib.Unhang(r.gameObject);
            for (int i = 0; i < s.Renderers.Length; i++) if (s.Renderers[i] != null) s.Renderers[i].sharedMaterials = s.Mats[i];
            RavenLooks.Drop(s.Paint);
            r.m_name = s.Name;
            Worn.Remove(r);
        }

        internal static void Forget(Raven r) { Saved s; if (Worn.TryGetValue(r, out s)) RavenLooks.Drop(s.Paint); Worn.Remove(r); }

        private static Material Tint(Material m)
        {
            if (m == null) return null;
            Material c;
            if (Made.TryGetValue(m, out c) && c != null) return c;
            c = new Material(m);
            c.name = m.name + " (guide)";
            c.hideFlags = HideFlags.HideAndDontSave;
            if (c.HasProperty("_Color"))
            {
                Color col = m.color;
                Color tinted = Color.Lerp(col, Crimson, 0.55f);
                tinted.a = col.a;
                c.color = tinted;
            }
            if (c.HasProperty("_EmissionColor"))
            {
                Color e = m.GetColor("_EmissionColor");
                float k = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
                if (k > 0.01f) c.SetColor("_EmissionColor", Crimson * k * 1.3f);
            }
            Made[m] = c;
            return c;
        }
    }
}
#endif
