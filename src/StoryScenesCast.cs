#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (StoryScenes), its people: taken, locked for the scene (one scene a person), ordered, let go.
    internal static partial class StoryScenes
    {
        // ---------------------------------------------------------------- the people of the scene
        private static readonly Dictionary<string, NpcActor> _cast = new Dictionary<string, NpcActor>(); // (its word → the one taken)
        private static readonly List<NpcActor> _made = new List<NpcActor>(); // (made for this scene: «npc <who> spawn» — taken away after it)
        internal static float RunFrom = 12f; // (m: a person of a scene farther off than this runs)
        private static NpcActor _walker;
        private static float _limit; // (this step's own time limit; 0 — the general one)
        private static Vector3 _walkTo;
        private static float _walkNear;

        // ---------------------------------------------------------------- one scene a person (the user 2026-11-02)
        // A scene that takes a person locks it: «<player>@<until>» in its ZDO (KScene), written where it is run (the act
        // «scenelock»), renewed every LockRenew s while the scene holds it, taken off when the scene lets it go. Another
        // player's scene meeting a person under a fresh lock stops (and tries again later, as after any stop); a lock not
        // renewed (its player left) is free after LockFor s.
        internal static readonly int KScene = "RavenWatch_npcScene".GetStableHashCode();
        internal static float LockFor = 15f, LockRenew = 5f;
        private static float _renewAt;
        private static bool _wired;

        private static void Wire()
        {
            if (_wired) return;
            _wired = true;
            StoryActions.Wire(); // («storylead»: whom a person follows)
            StoryProps.Wire();   // («prop»: a thing in a person's hand)
            NpcActor.Acts["scenelock"] = (a, w) => { ZDO z = a.Zdo; if (z != null) z.Set(KScene, w.Length > 1 ? w[1] : ""); }; // (where it is run: its owner writes)
            NpcMind.SceneWatch = a =>
            {
                ZDO z = a != null ? a.Zdo : null;
                if (z == null || Holder(a) != 0L) return;
                if (z.GetString(KScene, "").Length > 0) { Npcs.Log(a.Id + ": its scene's lock ran out, nobody renews it — let go"); LetGo(a); return; } // (its player left, the game closed, its owner changed: nobody else would)
                StoryActions.LeadWatch(a);
            };
        }

        private static double Now { get { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0; } }
        private static long Me { get { return Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L; } }

        // who holds a person for a scene now (0 — nobody)
        internal static long Holder(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            string v = z != null ? z.GetString(KScene, "") : "";
            int at = v.IndexOf('@');
            long who; double until;
            if (at <= 0 || !long.TryParse(v.Substring(0, at), out who) || !double.TryParse(v.Substring(at + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out until)) return 0L;
            return until > Now ? who : 0L;
        }

        // held by another player's scene?
        internal static bool HeldByOther(NpcActor a) { long h = Holder(a); return h != 0L && h != Me; }

        private static void Lock(NpcActor a, bool on)
        {
            if (a == null) return;
            a.Act("scenelock " + (on ? Me + "@" + (Now + LockFor).ToString("0", System.Globalization.CultureInfo.InvariantCulture) : ""));
        }

        // a person of the scene by its word (its template id or its tag): the one taken already, else the nearest loaded here
        internal static NpcActor Actor(string who, Vector3 near)
        {
            NpcActor a;
            if (_cast.TryGetValue(who, out a) && a != null) return a;
            NpcActor best = null; float bd = Seen;
            foreach (NpcActor n in NpcActor.Live)
            {
                if (n == null || n.Def == null || (n.Def.Id != who && n.Tag != who)) continue;
                float d = Vector3.Distance(n.transform.position, near);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        // where a person walks to: you, or a place — a piece of a building to its edge, from where the person comes
        private static bool Target(Player p, string what, Vector3 from, out Vector3 at)
        {
            if (what == "player") { at = p.transform.position; return true; }
            return StoryWhere.Approach(what, from, out at);
        }

        // a door of the story's places opened or shut — its state in the world (the door plays it on every game)
        private static void Door(string place, bool open)
        {
            int done = 0;
            foreach (ZNetView nv in StoryWhere.Pieces(place)) // (every piece of that name: a gate's two halves)
            {
                Door d = nv.GetComponentInChildren<Door>();
                if (d == null || !nv.IsValid()) continue;
                if (open && HallState.Locked(d) != null) Note("door: «" + place + "» ще замкнені (do hallgate open перед цим) — відчиняться на мить і зачиняться знову");
                if (!nv.IsOwner()) nv.ClaimOwnership();
                nv.GetZDO().Set(ZDOVars.s_state, open ? 1 : 0);
                done++;
            }
            if (done == 0) Note("door: нема дверей «" + place + "» поруч — крок пропущено");
        }

        private static void NpcStep(Player p, Step st, string[] w)
        {
            string who = w[1];
            if (w[2] == "spawn") { Spawn(p, who, w[3]); return; }
            NpcActor a = Actor(who, p.transform.position);
            if (a == null)
            {
                if (w[2] == "free") return; // (gone already: nothing to let go)
                Stop("«" + who + "» зник зі сцени"); // (never half a scene without its person: again when it is near — TryPlay waits for the cast)
                return;
            }
            if (!_cast.ContainsKey(who))
            {
                if (HeldByOther(a)) { Stop("«" + who + "» зараз у сцені іншого гравця"); return; } // (one scene a person: this one again later)
                Lock(a, true);
            }
            _cast[who] = a;
            switch (w[2])
            {
                case "take": case "stay": a.Order("stay", NpcMind.Layer.Scene); break;
                case "free": Free(who, a); break;
                case "prop":
                    {
                        if (w[3] == "put") { StoryProps.Put(a); break; }
                        string why;
                        StoryProps.Ask ask = w[3] == "take" ? StoryProps.Read(w, 4, out why) : null;
                        if (ask == null) { Note("npc prop: " + StoryProps.Usage); break; }
                        why = StoryProps.Take(a, ask);
                        if (why != null) Note("npc prop: " + why);
                        _waitFor = Time.time + 0.6f;
                        break;
                    }
                case "go":
                    {
                        Vector3 to;
                        if (!Target(p, w[3], a.transform.position, out to)) { Note("npc go: нема місця «" + w[3] + "» — крок пропущено"); break; }
                        int ni = Array.IndexOf(w, "near");
                        float near = ni >= 0 && w.Length > ni + 1 ? F(w[ni + 1], 1.6f) : 1.6f;
                        bool run = Array.IndexOf(w, "run") > 0 || Utils.DistanceXZ(a.transform.position, to) > RunFrom; // (far off: it hurries — the user 2026-11-01)
                        a.Order("go " + V(to) + (run ? " run" : "") + " near " + F(near), NpcMind.Layer.Scene); // (it stops that far off: its own Near)
                        _walker = a; _walkTo = to; _walkNear = NpcMind.Reach(near);
                        float way = Utils.DistanceXZ(a.transform.position, to);
                        _limit = Time.time + way / (run ? 3.5f : 1.3f) + 8f; // (as long as the walk takes, and some: stuck — the scene goes on)
                        break;
                    }
                case "face":
                    if (w[3] == "player") a.Order("look " + NpcKit.Ref(p.GetZDOID()) + " keep", NpcMind.Layer.Scene);
                    else { Vector3 at; if (StoryWhere.Find(w[3], out at)) a.Order("lookat " + V(at + Vector3.up * 1.2f), NpcMind.Layer.Scene); else Note("npc face: нема місця «" + w[3] + "»"); }
                    break;
                case "say":
                    {
                        string line = Lang.T(Rest(st.Text, 3));
                        _waitFor = Time.time + ReadTime(line);
                        a.Say(line, p);
                        if (StoryCamera.Active) StoryScreen.Sub(a.Name + ": " + line, ReadTime(line) + 0.4f); // (in a shot: as a subtitle too — the bubble is small in a wide picture)
                        break;
                    }
                case "emote": a.Order("emote " + w[3] + (Array.IndexOf(w, "loop") > 3 ? " loop" : ""), NpcMind.Layer.Scene); break;
                case "clip": a.Order("clip " + w[3] + (w.Length > 4 ? " " + w[4] : ""), NpcMind.Layer.Scene); break; // (one of our clips: BodyClips — npc <who> clip <name>|none [seconds])
                case "fade":
                    {
                        float s = w.Length > 3 ? F(w[3], Fades.Person) : Fades.Person;
                        NpcFade.Out(a, s, w.Length > 4 ? w[4] : "-");
                        _waitFor = Time.time + s;
                        break;
                    }
                case "sit":
                    {
                        Vector3 at;
                        if (!Target(p, w[3], a.transform.position, out at)) { Note("npc sit: нема місця «" + w[3] + "» — крок пропущено"); break; }
                        a.Order("sit " + V(at), NpcMind.Layer.Scene);
                        break;
                    }
            }
        }

        // a person made for this scene at a place, facing you (not kept in the world: gone with the scene)
        private static void Spawn(Player p, string who, string place)
        {
            Vector3 at;
            if (!StoryWhere.Find(place, out at)) { Note("npc spawn: нема місця «" + place + "» — крок пропущено"); return; }
            Vector3 look = p.transform.position - at; look.y = 0f;
            NpcActor a = Npcs.Spawn(who, at, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity, p.GetPlayerID(), "scene");
            if (a == null) { Note("npc spawn: «" + who + "» не вийшло зробити (нема шаблону?) — крок пропущено"); return; }
            ZDO z = a.Zdo;
            if (z != null) z.Persistent = false; // (left behind by a crash: gone with the owner's session)
            _made.Add(a);
            _cast[who] = a;
            Lock(a, true);
            a.Order("stay", NpcMind.Layer.Scene);
        }

        // a thing left on the ground at a place (the echo's charm): a real item of the world, anyone may pick it up
        private static void Drop(Player p, string prefabName, string place)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (d == null) { Note("drop: нема предмета «" + prefabName + "» — крок пропущено"); return; }
            Vector3 at;
            if (place == "player") at = p.transform.position + p.transform.forward * 1.2f;
            else if (!StoryWhere.Find(place, out at)) { Note("drop: нема місця «" + place + "» — крок пропущено"); return; }
            ItemDrop.ItemData item = d.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = 1;
            ItemDrop.DropItem(item, 1, at + Vector3.up * 0.4f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        }

        private static void Free(string who, NpcActor a)
        {
            if (a != null) LetGo(a);
            _cast.Remove(who);
            if (_walker == a) _walker = null;
        }

        // all a scene holds a person by, off: «free» takes its clip too
        private static void LetGo(NpcActor a)
        {
            StoryProps.Put(a); // (its prop back too)
            a.Order("look none", NpcMind.Layer.Scene);
            a.Order("free", NpcMind.Layer.Scene);
            StoryActions.LeadOff(a);
            Lock(a, false);
        }

        // the scene over or stopped: everyone taken back to its own day, where it is
        private static void FreeCast()
        {
            foreach (KeyValuePair<string, NpcActor> kv in new List<KeyValuePair<string, NpcActor>>(_cast)) Free(kv.Key, kv.Value);
            foreach (NpcActor a in _made) if (a != null) NpcFade.Out(a, Fades.Person, "-"); // (made for the scene: fades away with it — smooth by default; one faded already is gone already)
            _made.Clear();
            _cast.Clear(); _walker = null;
        }
    }
}
#endif
