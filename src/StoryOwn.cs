#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Your own raven (story-lore.md «Хто має воронів», the prologue): the one that waited by your name all the centuries
    // and found you again. Not Haldor's stray and not Odin's new one — yours, from your first life (the raven keeps the
    // mark OwnKey; its lines may ask). It lost the old memory in the fog as you did.
    //   given back   you never had a raven of that kind: a new one, awake from the start (no ritual), first level, bound
    //                to you; it flies in to your shoulder a few seconds later (Trust.SendAway → back: Back)
    //   melted       you have one of that kind already (bought, Odin's, any): your own melts into it — the one on your
    //                shoulder at once, else the next time one of yours of that kind sits there (MergePrefix); its bar of
    //                this level fills (the ritual still asks its heads) and it is your own from then on
    // The story's parts (registered by StoryModules): the condition «ownraven hugin|munin|both|any|none» (a raven of that
    // kind is yours: Trial.HadKind), the scene step «own hugin|munin», its landing (Trust.BackFrom).
    internal static class StoryOwn
    {
        internal const string Why = "story_own";                    // its flight in to you (Trust.SendAway)
        internal const string OwnKey = "RavenWatch_own";            // on the raven: your own, from your first life
        private const string MergePrefix = "RavenWatch_ownMerge_";  // on the character: your own waits to melt into the next of its kind you wear (m / h)
        internal static float FlySeconds = 4f;
        private static bool _conds, _steps;
        private static float _next;

        // ---------------------------------------------------------------- the story's words for it
        internal static void Conds()
        {
            if (_conds) return;
            _conds = true;
            StoryCond.Register("ownraven", new StoryCond.Kind { Usage = "ownraven hugin|munin|both|any|none   (ворон цього виду вже твій: був, є чи загубився)", Min = 1,
                Check = a => System.Array.IndexOf(new[] { "hugin", "munin", "both", "any", "none" }, a[1]) < 0 ? "ownraven hugin|munin|both|any|none" : null,
                Test = (p, a) =>
                {
                    bool h = Trial.HadKind(p, false), m = Trial.HadKind(p, true);
                    switch (a[1])
                    {
                        case "hugin": return h;
                        case "munin": return m;
                        case "both": return h && m;
                        case "any": return h || m;
                        default: return !h && !m;
                    }
                } });
        }

        internal static void Steps()
        {
            if (_steps) return;
            _steps = true;
            StoryScenes.Register("own", new StoryScenes.StepKind { Min = 1, Lasting = true, Usage = "own hugin|munin   (твій власний ворон прилітає; вже є такий — зливається з ним)",
                Check = st => st.W[1] != "hugin" && st.W[1] != "munin" ? "own hugin|munin" : null,
                Begin = (p, st) => Return(p, st.W[1] == "munin") });
            Trust.BackFrom[Why] = (p, munin, item) => Back(p, munin); // (landed on your shoulder)
        }

        // ---------------------------------------------------------------- your own raven
        internal static void Return(Player p, bool munin)
        {
            if (p == null) return;
            if (Trial.HadKind(p, munin)) { MeltOrWait(p, munin); return; }
            ItemDrop.ItemData item = RavenItem.NewRaven(munin);
            if (item == null) { StoryScenes.Note("own: нема предмета ворона"); return; }
            item.m_customData[OwnKey] = "1";
            item.m_customData[Breeds.Key] = Breeds.None; // (your own raven is a plain black one, no breed — the user 2026-11-02)
            Trust.WakeOwn(item, p);
            if (!p.GetInventory().AddItem(item))
            {
                ItemDrop.DropItem(item, 1, p.transform.position + p.transform.forward + Vector3.up, Quaternion.identity); // (a full bag: at your feet)
                RavenMsg.Center(p, Lang.F("{0} сів біля тебе — у торбі нема місця.", Plugin.RavenName(munin)));
                return;
            }
            RavenSeat.Sit(p, item, true);
            Trust.SendAway(item, FlySeconds, Why); // (on your shoulder in a moment — it flies in)
            RavenFx.Play("sfx_raven_kaw", p.transform.position + p.transform.forward * 25f + Vector3.up * 12f, Quaternion.identity); // (from afar)
            StoryScenes.Note("own: " + (munin ? "Мунін" : "Хугін") + " летить до гравця");
        }

        // it landed (Trust: back from its flight «story_own»)
        internal static void Back(Player p, bool munin)
        {
            if (p == null) return;
            RavenAura.BackFromOdin(p, munin);
            RavenMsg.Center(p, munin ? Lang.T("Мунін знайшов тебе.") : Lang.T("Хугін знайшов тебе."));
            StoryBus.Raise("own_back", munin ? "munin" : "hugin", 0);
        }

        private static void MeltOrWait(Player p, bool munin)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            ItemDrop.ItemData it = munin ? m : h;
            if (it != null && Trust.IsOwner(it, p) && Trust.Active(it)) { Melt(p, it, munin); return; }
            p.m_customData[MergePrefix + (munin ? "m" : "h")] = "1";
            RavenFx.Play("sfx_raven_kaw", p.transform.position + p.transform.forward * 25f + Vector3.up * 12f, Quaternion.identity);
            RavenMsg.Center(p, munin ? Lang.T("Щось шукало тебе — і не знайшло Муніна на плечі.\nКоли він сяде, згадає.") : Lang.T("Щось шукало тебе — і не знайшло Хугіна на плечі.\nКоли він сяде, згадає."));
            StoryBus.Raise("own_wait", munin ? "munin" : "hugin", 0);
        }

        private static void Melt(Player p, ItemDrop.ItemData item, bool munin)
        {
            p.m_customData.Remove(MergePrefix + (munin ? "m" : "h"));
            if (item.m_customData != null && item.m_customData.ContainsKey(OwnKey)) return; // (your own already)
            item.m_customData[OwnKey] = "1";
            Trust.FillBar(item);
            LastChance.Flash(p, p.transform.position + Vector3.up * 1.2f, munin ? new Color(1f, 0.3f, 0.2f) : new Color(0.6f, 0.8f, 1f));
            RavenFx.Play("sfx_raven_kaw", p.transform.position + Vector3.up * 2f, Quaternion.identity);
            RavenMsg.Center(p, munin ? Lang.T("Мунін здригнувся — і ніби щось пригадав.\nТепер він знає, чий він.") : Lang.T("Хугін здригнувся — і ніби щось пригадав.\nТепер він знає, чий він."));
            Backup.Save(p, item);
            StoryBus.Raise("own_melt", munin ? "munin" : "hugin", 0);
        }

        // ---------------------------------------------------------------- the author's test (raven story own / unown)
        // unown: the character as if it never had a raven of this kind — its ravens of that kind taken away (worn, bag,
        // shoulder cells), their copies, the marks; so the prologue's ways can be walked again
        internal static string Unown(Player p, bool munin)
        {
            if (p == null) return "Нема гравця.";
            List<ItemDrop.ItemData> all = new List<ItemDrop.ItemData>(p.GetInventory().GetAllItems());
            all.AddRange(RavenSeat.Shelved(p));
            int gone = 0;
            foreach (ItemDrop.ItemData item in all)
            {
                bool m;
                if (!RavenItem.IsRaven(item, out m) || m != munin) continue;
                if (item.m_equipped)
                {
                    RavenSeat.Crumbling = true; // (no offence taken: nobody took it off)
                    try { p.UnequipItem(item, false); } finally { RavenSeat.Crumbling = false; }
                }
                if (RavenSeat.OnShelf(item)) RavenSeat.Forget(item, "taken away (raven story unown)"); else p.GetInventory().RemoveItem(item);
                gone++;
            }
            int copies = Backup.ForgetKind(p, munin);
            p.m_customData.Remove(munin ? Trial.HadMuninKey : Trial.HadHuginKey);
            p.m_customData.Remove(MergePrefix + (munin ? "m" : "h"));
            return (munin ? "Мунін" : "Хугін") + ": забрано воронів " + gone + ", копій " + copies + ". Тепер цього виду в персонажа нема (ворони вдома на сідалах лишились у світі).";
        }

        // every pass of the story (StoryQuests.Tick): your own that waits melts into the next one of its kind you wear
        internal static void Tick(Player p)
        {
            if (p == null || Time.time < _next) return;
            _next = Time.time + 2f;
            for (int i = 0; i < 2; i++)
            {
                bool munin = i == 1;
                if (!p.m_customData.ContainsKey(MergePrefix + (munin ? "m" : "h"))) continue;
                ItemDrop.ItemData h, m;
                RavenItem.Worn(p, out h, out m);
                ItemDrop.ItemData it = munin ? m : h;
                if (it != null && Trust.IsOwner(it, p) && Trust.Active(it) && !Trust.Away(it)) Melt(p, it, munin);
            }
        }
    }
}
#endif
