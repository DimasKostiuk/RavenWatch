#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person's locker (npc-plan.md, «шафка»): what is heavy and seldom changes — its gear cells (NpcGear) and what it
    // carries (NpcWorker) — kept in a ZDO of its own instead of the person's. The game sends a changed ZDO whole, and a
    // walking person's changes up to twenty times a second to every player near: with ten cells and a full bag in it,
    // that was kilobytes a send. The locker does not walk: it is sent only when its contents change, and moved after
    // the person now and then (when it is Follow m off), so it is always among the ZDOs the person's games have.
    // Made when first needed (where the person is owned); old saves moved over once (Migrate); taken away with it.
    internal static class NpcLocker
    {
        internal const string Prefab = "RavenWatch_npc_locker";
        internal static readonly int KLocker = "RavenWatch_npcLocker".GetStableHashCode(); // string: a lasting link (NpcKit.Save) on the person
        private const float Follow = 24f;
        private static readonly int Hash = Prefab.GetStableHashCode();
        internal static readonly int KOf = "RavenWatch_lockerOf".GetStableHashCode(); // long on the locker: its person's own number (NpcKit.KSid)

        // the prefab: nothing to see, nothing to do — a place for the ZDO to be (an unknown prefab would be complained of)
        internal static GameObject Build(Transform holder)
        {
            GameObject go = new GameObject(Prefab);
            go.transform.SetParent(holder, false);
            ZNetView nv = go.AddComponent<ZNetView>();
            nv.m_persistent = true;
            return go;
        }

        // its locker's ZDO: null while it has none (made when create, where it is owned) or while it is not here yet
        internal static ZDO Of(NpcActor a, bool create)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || ZDOMan.instance == null) return null;
            if (NpcKit.HasRef(z, KLocker)) { ZDOID id = NpcKit.GetRef(z, KLocker); return id.IsNone() ? null : ZDOMan.instance.GetZDO(id); } // (null: not synced here yet — a lasting link not found reads as none: never made again over it)
            if (!create || !a.Owned) return null;
            ZDO l = ZDOMan.instance.CreateNewZDO(a.transform.position, Hash);
            l.Persistent = true;
            l.SetPrefab(Hash);
            NpcKit.Mark(l); l.Set(KOf, NpcKit.Mark(z)); // (both found again after a restart: NpcKit «lasting links»)
            NpcKit.SetRef(z, KLocker, l.m_uid);
            NpcCensus.Added(l);
            return l;
        }

        // its locker can be read and written here now (it has none yet, or it is here): until then nothing of it is
        // written — an empty bag written over a locker not yet synced would lose what is in it
        internal static bool Ready(NpcActor a) { return !Has(a) || (ZDOMan.instance != null && ZDOMan.instance.GetZDO(NpcKit.GetRef(a.Zdo, KLocker)) != null); }

        internal static bool Has(NpcActor a) { return a != null && a.Zdo != null && NpcKit.HasRef(a.Zdo, KLocker); } // (it has a link: whether what it names is here yet is another question — Ready, Of)

        // a string from it: null when it cannot be read here now (not here yet); "" when it has none
        internal static string Get(NpcActor a, int key)
        {
            if (!Has(a)) return "";
            ZDO l = Of(a, false);
            return l != null ? l.GetString(key, "") : null;
        }

        // written into it (made when there is none and there is something to keep)
        internal static bool Set(NpcActor a, int key, string value)
        {
            ZDO l = Of(a, !string.IsNullOrEmpty(value));
            if (l == null) return string.IsNullOrEmpty(value) && !Has(a); // (nothing to keep and nowhere to keep it: fine)
            if (!l.IsOwner()) l.SetOwner(ZDOMan.GetSessionID());
            if (l.GetString(key, "") != value) l.Set(key, value);
            return true;
        }

        // its locker named but not there: on a client maybe not synced yet (it waits: only the server can know a locker is gone, and a
        // client that gave up too soon made a new one and orphaned the old) — on the server (it holds every ZDO) it is gone for good; after
        // half a minute of that the person is let go of it (a new one is made when there is something to keep; what was in the old one is lost)
        private static readonly System.Collections.Generic.Dictionary<NpcActor, float> Missing = new System.Collections.Generic.Dictionary<NpcActor, float>();
        internal static void Check(NpcActor a)
        {
            if (a == null || !a.Owned || !Has(a) || ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (Of(a, false) != null) { Missing.Remove(a); return; }
            float since;
            if (!Missing.TryGetValue(a, out since)) { Missing[a] = Time.time; return; }
            if (Time.time - since < 30f) return;
            Missing.Remove(a);
            Npcs.Log(a.Id + ": its locker " + a.Zdo.GetString(KLocker, "") + " is not found (the server has no such ZDO) — let go of");
            NpcKit.SetRef(a.Zdo, KLocker, ZDOID.None);
            if (Adopt(a)) return; // (one of its own lying by it, its link lost: taken back — else a new one when there is something to keep)
            Npcs.Log(a.Id + ": no locker of its own near it — a new one when needed; what was in the old one is lost");
        }

        // a locker of an old save left without its person (its link was a «user:id», gone with the restart): taken back
        // by the person it lies by — the nearest person here with no locker of its own (the user, 2026-10-31: a guard's
        // bow and arrows were lost so)
        internal static bool Adopt(NpcActor a)
        {
            if (a == null || !a.Owned || ZDOMan.instance == null) return false;
            ZDO mine = Has(a) ? Of(a, false) : null;
            if (Has(a) && (mine == null || !Empty(mine))) return false; // (one with its things in it: kept)
            List<ZDO> all = NpcCensus.Of(Prefab); // (the shared census: NpcCensus)
            ZDO best = null; float bd = 60f;
            long me = NpcKit.Mark(a.Zdo);
            foreach (ZDO l in all)
            {
                long of = l.GetLong(KOf, 0L);
                if (of != 0L && of != me) continue; // (another's)
                float d = Utils.DistanceXZ(l.GetPosition(), a.transform.position);
                if (d >= bd) continue;
                bool closer = false; // (a person nearer to it that has no locker either: its, more likely)
                foreach (NpcActor o in NpcActor.Live) if (o != null && o != a && !Has(o) && Utils.DistanceXZ(o.transform.position, l.GetPosition()) < d) { closer = true; break; }
                if (of == 0L && closer) continue;
                if (of == 0L && Taken(l.m_uid)) continue;
                if (mine != null && (l == mine || Empty(l))) continue; // (it has an empty one: only a full one is worth the change)
                if (Empty(l) && d > 10f) continue; // (an empty one far off: no use, and maybe another's)
                best = l; bd = d;
            }
            if (best == null) return false;
            if (!best.IsOwner()) best.SetOwner(ZDOMan.GetSessionID());
            if (mine != null) { if (!mine.IsOwner()) mine.SetOwner(ZDOMan.GetSessionID()); ZDOMan.instance.DestroyZDO(mine); } // (the empty one taken by mistake: gone)
            NpcKit.Mark(best); best.Set(KOf, me);
            NpcKit.SetRef(a.Zdo, KLocker, best.m_uid);
            Npcs.Log(a.Id + ": took back its locker " + NpcKit.Ref(best.m_uid) + " (" + Mathf.RoundToInt(bd) + " m off)");
            return true;
        }

        // nothing in it at all: no text of any module kept here, no value a person keeps here (Lives) — only such a one is given up for a full one
        private static readonly int[] Texts = { ZDOVars.s_items, NpcWorker.KCarry, NpcRequests.KRequest, NpcRequests.KBrought, NpcMemory.KMemory, NpcAttitude.KAttitude, NpcAttitude.KQuarrel, NpcThreads.KThreads, NpcThreads.KGiven, NpcGoals.KGoal, NpcGoals.KGoalNext, NpcGoals.KGoalDone, NpcPromises.KPromise, NpcTasks.KTask, NpcTasks.KTaskSeen, NpcArcs.KArc, NpcToil.KCraft, NpcNeighbours.KNeighbours };
        private static bool Empty(ZDO l)
        {
            foreach (int k in Texts) if (l.GetString(k, "").Length > 0) return false;
            foreach (KeyValuePair<int, Kind> e in Lives) if (Holds(l, e.Key, e.Value)) return false;
            return true;
        }

        private static bool Taken(ZDOID l) { foreach (NpcActor o in NpcActor.Live) if (o != null && Has(o) && NpcKit.GetRef(o.Zdo, KLocker) == l) return true; return false; }

        // after the person (where it is owned, now and then)
        internal static void Keep(NpcActor a)
        {
            ZDO l = Has(a) ? Of(a, false) : null;
            if (l == null || Utils.DistanceXZ(l.GetPosition(), a.transform.position) < Follow) return;
            if (!l.IsOwner()) l.SetOwner(ZDOMan.GetSessionID());
            l.SetPosition(a.transform.position);
        }

        // what an old save kept on the person itself: into the locker once, then off the person
        internal static bool Migrate(NpcActor a) // (true: nothing left on the person — else asked again later)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || !a.Owned) return false;
            if (!Ready(a)) return false;
            bool left = false;
            foreach (int key in new[] { ZDOVars.s_items, NpcWorker.KCarry, NpcRequests.KRequest })
            {
                string s = z.GetString(key, "");
                if (s.Length == 0) continue;
                if (Set(a, key, s)) { z.Set(key, ""); Npcs.Log(a.Id + ": moved its " + (key == NpcWorker.KCarry ? "carry" : key == NpcRequests.KRequest ? "request" : "gear") + " into its locker"); }
                else left = true;
            }
            int moved = 0;
            foreach (KeyValuePair<int, Kind> e in Lives) if (Move(a, z, e.Key, e.Value)) moved++; else if (Holds(z, e.Key, e.Value)) left = true;
            if (moved > 0) Npcs.Log(a.Id + ": moved " + moved + " of its values off itself into its locker (a lighter person on the net)");
            return !left;
        }

        // ---------------------------------------------------------------- a person's own values, kept here and not on the person
        // The two-player test 2026-11-05: a person's ZDO is sent whole each time anything in it changes, and a walking one's position
        // changes every frame — so all it holds goes to every player near, up to twenty times a second, before the pieces round them
        // (the game sends people first: ZDOMan.ServerSendCompare). With 25 people of 1–1.5 KB the base never reached the second player.
        // So on the person stays only what other games need at once to show it (its look, pose, clip, seat, name, who talks to it, its
        // heart); what changes seldom or is read only now and then lives here (Lives), sent only when it changes, after the pieces.
        //   read     Text · Num · Int · Long · Vec · Flag · Ref · HasRef  — what the person still holds first (written while its locker was not
        //            here, or an old save not moved yet: Put takes it off the person whenever the locker gets it), then the locker's, then the default
        //   write    Put (where the person is run): into the locker and off the person; its locker not here: onto the person (moved later)
        //   off the person (Off): a mark written over the value — «none» to every read here (Gone for a text; NaN, MinValue for a number), never
        //            the game's ZDO.RemoveString…: that takes the key off this game's copy only (no new revision, and ZDO.Deserialize only adds and
        //            overwrites keys — read in the game's code 2026-11-12), so the server and every other game kept the old value, read it first
        //            («what the person still holds») and, at the next owner's Migrate, wrote it back over the locker: one saved from its trouble
        //            was «in need» again after a relog on a dedicated server. A mark is a value like any other: sent once, the same on every game
        //   a new value of a person: here, unless every game must see it the very moment it changes — npcnetcheck stops a new key on
        //   the person that is not in Lives nor in its list of the person's own (tools/npcnetcheck.js)
        internal enum Kind { Text, Num, Int, Long, Vec }
        private static Dictionary<int, Kind> _lives;
        internal static Dictionary<int, Kind> Lives
        {
            get
            {
                return _lives ?? (_lives = new Dictionary<int, Kind>
                {
                    { NpcVoice.KSeen, Kind.Text }, { NpcMind.KPlans, Kind.Text }, { NpcMind.KStayUntil, Kind.Long },
                    { NpcResident.KBed, Kind.Text }, { NpcResident.KSettledAt, Kind.Long }, { NpcResident.KMovedOut, Kind.Vec },
                    { NpcWorker.KJob, Kind.Text }, { NpcWorker.KChest, Kind.Text }, { NpcWorker.KSupply, Kind.Text }, { NpcKitchen.KTier, Kind.Int }, { NpcWorker.KInbox, Kind.Text }, { NpcWorker.KTarget, Kind.Text },
                    { NpcWorker.KNowhere, Kind.Text }, { NpcWorker.KHouse, Kind.Vec }, { NpcWorker.KStore, Kind.Vec }, { NpcWorker.KPost, Kind.Vec },
                    { NpcWorker.KPatrol, Kind.Int }, { NpcHew.KArea, Kind.Vec }, { NpcTidy.KOff, Kind.Int },
                    { NpcLevers.KVars, Kind.Text }, { NpcLevers.KTraits, Kind.Text }, { NpcKnack.KKnack, Kind.Text }, { NpcBackstory.KStory, Kind.Text },
                    { NpcToil.KMood, Kind.Num }, { NpcToil.KTired, Kind.Num }, { NpcToil.KLow, Kind.Num }, { NpcToil.KWhy, Kind.Int }, { NpcToil.KMoodParts, Kind.Text },
                    { NpcToil.KUnpaid, Kind.Int }, { NpcToil.KPayNext, Kind.Long }, { NpcHunger.KFull, Kind.Num }, { NpcHunger.KStarved, Kind.Num },
                    { NpcNews.KKeys, Kind.Int }, { NpcRequests.KAskNext, Kind.Long }, { NpcMind.KFought, Kind.Text }, { NpcTimbre.KTimbre, Kind.Text },
#if RAVEN_STORY
                    { NpcRaven.KHeldUntil, Kind.Long }, // (the story's word to a person's raven)
                    { StoryHold.KThere, Kind.Int }, // (it has taken the place its standing word names: changes when it comes and when it goes, a scene asks it before it begins)
#endif
                });
            }
        }

        // its locker here (null: none, or not here yet). Its gait reads its mood and hunger each frame on every game: the link is
        // resolved once and kept by the person's id, for as long as its link string is the same (a link not found yet: tried again)
        private static readonly Dictionary<ZDOID, KeyValuePair<string, ZDOID>> _here = new Dictionary<ZDOID, KeyValuePair<string, ZDOID>>(); // session: ids of this world load
        private static ZDO Here(ZDO person)
        {
            if (person == null || ZDOMan.instance == null) return null;
            string link = person.GetString(KLocker, "");
            if (link.Length == 0) return null;
            KeyValuePair<string, ZDOID> c;
            if (!_here.TryGetValue(person.m_uid, out c) || (!ReferenceEquals(c.Key, link) && c.Key != link))
            {
                ZDOID id = NpcKit.Resolve(link);
                if (id.IsNone()) return null; // (not found yet: not kept, looked for again)
                if (_here.Count > 512) _here.Clear();
                _here[person.m_uid] = c = new KeyValuePair<string, ZDOID>(link, id);
            }
            return ZDOMan.instance.GetZDO(c.Value);
        }

        private static readonly Vector3 NoVec = new Vector3(float.NaN, float.NaN, float.NaN);
        private const string Gone = "\u0001"; // (a text taken off the person: Off)
        internal static string Text(ZDO z, int key, string def = "") { if (z == null) return def; string s = z.GetString(key, null); if (s != null && s != Gone) return s; ZDO l = Here(z); return l != null ? l.GetString(key, def) : def; }
        internal static float Num(ZDO z, int key, float def) { if (z == null) return def; float v = z.GetFloat(key, float.NaN); if (!float.IsNaN(v)) return v; ZDO l = Here(z); return l != null ? l.GetFloat(key, def) : def; }
        internal static int Int(ZDO z, int key, int def) { if (z == null) return def; int v = z.GetInt(key, int.MinValue); if (v != int.MinValue) return v; ZDO l = Here(z); return l != null ? l.GetInt(key, def) : def; }
        internal static long Long(ZDO z, int key, long def) { if (z == null) return def; long v = z.GetLong(key, long.MinValue); if (v != long.MinValue) return v; ZDO l = Here(z); return l != null ? l.GetLong(key, def) : def; }
        internal static Vector3 Vec(ZDO z, int key, Vector3 def) { if (z == null) return def; Vector3 v = z.GetVec3(key, NoVec); if (!float.IsNaN(v.x)) return v; ZDO l = Here(z); return l != null ? l.GetVec3(key, def) : def; }
        internal static bool Flag(ZDO z, int key) { return Int(z, key, 0) != 0; }
        internal static ZDOID Ref(ZDO z, int key) { return NpcKit.Resolve(Text(z, key)); }
        internal static bool HasRef(ZDO z, int key) { return Text(z, key).Length > 0; }

        // where to write: its locker (taken by this game), made when it has none and is run here; null: onto the person for now
        private static ZDO Into(ZDO z)
        {
            ZDO l = Here(z);
            if (l == null && z != null && !NpcKit.HasRef(z, KLocker))
            {
                NpcActor a = Of(z);
                if (a != null) l = Of(a, true);
            }
            if (l != null && !l.IsOwner()) l.SetOwner(ZDOMan.GetSessionID());
            return l;
        }

        private static NpcActor Of(ZDO z) { foreach (NpcActor a in NpcActor.Live) if (a != null && a.Zdo == z) return a; return null; }

        internal static void Put(ZDO z, int key, string v) { if (z == null) return; v = v ?? ""; ZDO l = Into(z); if (l == null) { if (z.GetString(key, "") != v) z.Set(key, v); return; } if (l.GetString(key, null) != v) l.Set(key, v); Off(z, key, Kind.Text); }
        internal static void Put(ZDO z, int key, float v) { if (z == null) return; ZDO l = Into(z); if (l == null) { if (z.GetFloat(key, float.NaN) != v) z.Set(key, v); return; } if (l.GetFloat(key, float.NaN) != v) l.Set(key, v); Off(z, key, Kind.Num); }
        internal static void Put(ZDO z, int key, int v) { if (z == null) return; ZDO l = Into(z); if (l == null) { if (z.GetInt(key, int.MinValue) != v) z.Set(key, v); return; } if (l.GetInt(key, int.MinValue) != v) l.Set(key, v); Off(z, key, Kind.Int); }
        internal static void Put(ZDO z, int key, long v) { if (z == null) return; ZDO l = Into(z); if (l == null) { if (z.GetLong(key, long.MinValue) != v) z.Set(key, v); return; } if (l.GetLong(key, long.MinValue) != v) l.Set(key, v); Off(z, key, Kind.Long); }
        internal static void Put(ZDO z, int key, Vector3 v) { if (z == null) return; ZDO l = Into(z); if (l == null) { if (z.GetVec3(key, NoVec) != v) z.Set(key, v); return; } if (l.GetVec3(key, NoVec) != v) l.Set(key, v); Off(z, key, Kind.Vec); }
        internal static void Put(ZDO z, int key, bool v) { Put(z, key, v ? 1 : 0); }
        internal static void PutRef(ZDO z, int key, ZDOID id) { Put(z, key, NpcKit.Save(id)); }

        // a value off the person, for every game (the header says why not the game's Remove…): its mark written over it, once — one that holds
        // nothing of it, or the mark already, is not written to (no send). Only where the person is run: a write from elsewhere is lost at
        // its owner's next one, and its revision raised here would shut that owner's news out for a while
        private static void Off(ZDO z, int key, Kind k)
        {
            if (!z.IsOwner() || !Holds(z, key, k)) return;
            switch (k)
            {
                case Kind.Text: z.Set(key, Gone); break;
                case Kind.Num: z.Set(key, float.NaN); break;
                case Kind.Int: z.Set(key, int.MinValue); break;
                case Kind.Long: z.Set(key, long.MinValue); break;
                default: z.Set(key, NoVec); break;
            }
        }

        // the person still holds it (an old save, or written while its locker was away); its mark (Off) is none
        private static bool Holds(ZDO z, int key, Kind k)
        {
            switch (k)
            {
                case Kind.Text: { string s = z.GetString(key, null); return s != null && s != Gone; }
                case Kind.Num: return !float.IsNaN(z.GetFloat(key, float.NaN));
                case Kind.Int: return z.GetInt(key, int.MinValue) != int.MinValue;
                case Kind.Long: return z.GetLong(key, long.MinValue) != long.MinValue;
                default: return !float.IsNaN(z.GetVec3(key, NoVec).x);
            }
        }

        // one value off the person into its locker — the person's wins: Put leaves a value on the person only when its locker was away, so it is the newer one
        private static bool Move(NpcActor a, ZDO z, int key, Kind k)
        {
            if (!Holds(z, key, k)) return false;
            ZDO l = Of(a, true);
            if (l == null) return false;
            if (!l.IsOwner()) l.SetOwner(ZDOMan.GetSessionID());
            switch (k)
            {
                case Kind.Text: l.Set(key, z.GetString(key, "")); break;
                case Kind.Num: l.Set(key, z.GetFloat(key, 0f)); break;
                case Kind.Int: l.Set(key, z.GetInt(key, 0)); break;
                case Kind.Long: l.Set(key, z.GetLong(key, 0L)); break;
                default: l.Set(key, z.GetVec3(key, Vector3.zero)); break;
            }
            Off(z, key, k);
            return true;
        }

        // the person taken away where it is not loaded (the server keeping the story's people): its locker too
        internal static void RemoveOf(ZDO person)
        {
            if (person == null || !NpcKit.HasRef(person, KLocker)) return;
            ZDOID id = NpcKit.GetRef(person, KLocker);
            ZDO l = id.IsNone() ? null : ZDOMan.instance.GetZDO(id);
            if (l == null) return;
            if (!l.IsOwner()) l.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(l);
        }

        // the person taken away: its locker too
        internal static void Remove(NpcActor a)
        {
            ZDO l = Has(a) ? Of(a, false) : null;
            if (l == null) return;
            if (!l.IsOwner()) l.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(l);
        }
    }
}
#endif
