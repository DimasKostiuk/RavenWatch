#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Hugin's «Вірність» and Munin's «Впертість»: when you die, each awake raven on the shoulder may stay there instead
    // of going into the grave (each its own roll). It stays worn, so the grave leaves it in the bag (or on its shoulder
    // cell), it sits on the shoulder again after the respawn and says so.
    internal static class Loyal
    {
        internal static bool Enabled = true;
        internal static float Chance = 0.3f;
        internal static int UnlockLevel = 3;
        private static readonly List<bool> Stayed = new List<bool>();
        private static long _stayedFor; // the character they stayed with: another one loaded hears nothing
        private const string StayedKey = "RavenWatch_stayed"; // on the raven: it did not lie in the last grave

        // A world that keeps what you wear: every raven on the shoulder stays anyway, the gift adds nothing. The world's
        // own setting is read straight from its list, not through GetGlobalKey: a mod may answer that one differently
        // for the length of a death (Path of Valheiman's «Прив'язане спорядження» says "keep what you wear" while the
        // grave is made — the ravens would then never go to the grave, and the gift would mean nothing).
        internal static bool WorldKeeps()
        {
            ZoneSystem z = ZoneSystem.instance;
            return z != null && z.m_globalKeysEnums != null && (z.m_globalKeysEnums.Contains(GlobalKeys.DeathKeepInventory) || z.m_globalKeysEnums.Contains(GlobalKeys.DeathKeepEquip));
        }

        // a new death: what the last grave was is forgotten
        internal static void Forget(Player p)
        {
            if (p == null) return;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems())
                if (item.m_customData != null) item.m_customData.Remove(StayedKey);
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p)) // on SeneaL UI's shoulders: not in the bag
                if (item.m_customData != null) item.m_customData.Remove(StayedKey);
        }

        internal static void Mark(ItemDrop.ItemData item) { if (item != null && item.m_customData != null) item.m_customData[StayedKey] = "1"; }

        // the grave opened: this raven was not in it (and the mark goes)
        internal static bool TakeStayed(ItemDrop.ItemData item)
        {
            return item != null && item.m_customData != null && item.m_customData.Remove(StayedKey);
        }

        internal static bool Is(RavenAbility a) { return a != null && (a.Id == "hloyal" || a.Id == "mloyal"); }

        // at the grave: true when this raven stays on the shoulder
        internal static bool Stays(Player p, ItemDrop.ItemData item)
        {
            bool munin;
            if (!Enabled || p == null || p != Player.m_localPlayer || !RavenItem.IsRaven(item, out munin)) return false;
            if (Trust.Away(item) || Trust.Stranger(item, p) || Trust.SettledLevel(item) < UnlockLevel) return false; // a stranger's raven owes you nothing
            if (UnityEngine.Random.value >= GearFx.LoyalChance(item, Chance)) return false; // (Захисний ошейник: likelier)
            Mark(item);
            if (_stayedFor != p.GetPlayerID()) Stayed.Clear();
            _stayedFor = p.GetPlayerID();
            if (!Stayed.Contains(munin)) Stayed.Add(munin);
            if (Plugin.Instance != null) Plugin.Instance.Log(Plugin.RavenName(munin) + " stays on the shoulder after the death");
            return true;
        }

        // Back from the dead, in three beats, so it is plain the gift worked: once the world is up (4 s) a caw, the word
        // in the middle of the screen and the gift's mark among the effects; then the raven flies in from up behind and
        // lands (the usual fly-in, Shoulder.ArriveDelay after the world is loaded); once it sits, it says its line.
        internal static float NoticeAfter = 4f, LineAfterLanding = 2.5f;
        private static float _aliveSince = -1f;
        private static bool _noticed, _hugin, _munin;
        private static float _satAt = -1f; // when the ones that stayed were back on the shoulder
        private static SE_Stats _markH, _markM;

        internal static void Tick(Player p)
        {
            TickLate(p);
            if (Stayed.Count == 0 || p == null || p.IsDead()) { _aliveSince = -1f; return; }
            if (p.GetPlayerID() != _stayedFor) { Stayed.Clear(); _aliveSince = -1f; return; }
            if (_aliveSince < 0f) { _aliveSince = Time.time; _noticed = false; _satAt = -1f; return; }
            float t = Time.time - _aliveSince;
            if (!_noticed)
            {
                if (t < NoticeAfter) return;
                _noticed = true;
                // only the ones really back on the shoulder
                ItemDrop.ItemData wh, wm;
                RavenItem.Worn(p, out wh, out wm);
                _hugin = Stayed.Contains(false) && wh != null;
                _munin = Stayed.Contains(true) && wm != null;
                if (!_hugin && !_munin) { Stayed.Clear(); return; }
                RavenFx.Play("sfx_raven_kaw", p.transform.position + Vector3.up * 4f, Quaternion.identity);
                RavenMsg.Center(p, _hugin && _munin ? Lang.T("Вірність і Впертість!\nХугін і Мунін не пішли в могилу — летять до тебе.")
                    : _hugin ? Lang.T("Вірність!\nХугін не пішов у могилу — летить до тебе.")
                    : Lang.T("Впертість!\nМунін не пішов у могилу — летить до тебе."));
                if (_hugin) Mark(p, false);
                if (_munin) Mark(p, true);
                return;
            }
            // the line once each has flown in and landed, a moment after
            if ((_hugin && !Shoulder.Arrived(p, false)) || (_munin && !Shoulder.Arrived(p, true))) { _satAt = -1f; return; }
            if (_satAt < 0f) { _satAt = Time.time; return; }
            if (Time.time - _satAt < LineAfterLanding) return;
            Stayed.Clear();
            if (_hugin) Say(p, false); // back from the dead: always a word (Munin's comes a moment later, over Hugin's)
            if (_munin) { if (_hugin) _muninAt = Time.time + 4f; else Say(p, true); }
        }

        private static float _muninAt = -1f;

        // Munin's word after Hugin's (from the tick, while no other scene runs)
        internal static void TickLate(Player p)
        {
            if (_muninAt < 0f || Time.time < _muninAt || p == null || p.IsDead()) return;
            _muninAt = -1f;
            Say(p, true);
        }

        // the test panel: the scene after a respawn, now, with the ravens on your shoulders (they vanish and fly in
        // again; the word in the middle at once, their lines once they sit)
        internal static string Test(Player p, bool hugin, bool munin)
        {
            if (p == null || p.IsDead()) return Lang.T("Персонаж мертвий.");
            ItemDrop.ItemData wh, wm;
            RavenItem.Worn(p, out wh, out wm);
            hugin &= wh != null; munin &= wm != null;
            if (!hugin && !munin) return Lang.T("Потрібного ворона нема на плечі.");
            Stayed.Clear();
            if (hugin) { Stayed.Add(false); Shoulder.Respawn(p, false); }
            if (munin) { Stayed.Add(true); Shoulder.Respawn(p, true); }
            _stayedFor = p.GetPlayerID();
            _aliveSince = Time.time - NoticeAfter; // the word now
            _noticed = false;
            _satAt = -1f;
            return hugin && munin ? Lang.T("Обидва прилітають, як після смерті.") : Lang.F("{0} прилітає, як після смерті.", (hugin ? Lang.T("Хугін") : Lang.T("Мунін")));
        }

        // the gift's picture among the effects for a while: what happened, at a glance
        private static void Mark(Player p, bool munin)
        {
            SE_Stats se = munin ? RavenFx.Template(ref _markM, "RavenWatch_LoyalM") : RavenFx.Template(ref _markH, "RavenWatch_LoyalH");
            se.m_name = munin ? Lang.W("Впертість Муніна") : Lang.W("Вірність Хугіна");
            se.m_tooltip = munin ? Lang.T("Мунін вчепився в плече й не пішов у могилу.") : Lang.T("Хугін не покинув тебе й не пішов у могилу.");
            se.m_icon = AbilityIcons.ForEffect(munin ? "mloyal" : "hloyal", munin);
            se.m_ttl = 30f;
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        private static void Say(Player p, bool munin)
        {
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, munin, "loyal") : null;
            RavenSkills.SayFromShoulder(p, munin, !string.IsNullOrEmpty(line) ? line
                : munin ? Lang.T("{Думав|Думала}, позбудешся мене? Не дочекаєшся.") : Lang.T("Я тут. Я нікуди не йшов."));
        }

        internal static string ReadyText(bool munin)
        {
            if (!Enabled) return Lang.T("<color=#9a9a9a>вимкнено в налаштуваннях</color>");
            if (WorldKeeps()) return Lang.T("<color=#9a9a9a>у цьому світі спорядження й так лишається після смерті</color>");
            return Lang.F("<color=#FFD54F><b>шанс {0}%</b></color>", RavenAbilities.R(GearFx.LoyalChance(Trust.Worn(munin), Chance) * 100f)); // (with «Захисний ошейник»: more)
        }
    }
}
#endif
