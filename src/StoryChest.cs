#if RAVEN_ITEM && RAVEN_NPC && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A chest of a place the story fills (chapter 2, the user 2026-11-16: «скриня в залі в яку треба складати матеріали. Після певної кількості Літ
    // каже… і через деякий час зала стає в новий стан»). The chest is a named piece of the place's blueprint; nothing of it is in the code.
    //   chest <place>.<piece> <Prefab> <N>       (a condition) that chest holds N of it now. Holds only on a game that has the chest loaded
    //                                            (far from it: no). One name may be pieces of different states: the one the place's state has now
    //                                            (LocState.Absent). A stage's «wait:» names each thing: «wait: chest zala.skrynia Wood 50, chest zala.skrynia Stone 30»
    //   chesttake <place>.<piece> <Prefab> <N>   (an action) N of it are taken out of that chest. A quest of the server runs its actions on one game,
    //                                            which may not be the one by the chest: the action only writes the world's word
    //                                            «chesttake_<place>_<piece>__<prefab>» = N — a word a thing: a client's world key comes back from the
    //                                            server a moment later, so two actions of one stage writing one word would read it empty and the second
    //                                            would put the first out —, and the game that owns the chest's ZDO (the one by it) takes the things
    //                                            and removes the word (Look). Fewer than N there (somebody took some out in between): what there is
    // Looked at, the chest says what the stage you stand on still wants in it (HoverOf, asked by LocChestHoverPatch: the hover of the game's chest,
    // our lines under it): «Сюди треба скласти:» and a line a thing, «have / need». Only for a stage that waits on «chest» — no such stage: one count.
    // One name over a place's states (the Hall's «skrynia»: a plain chest in 1.4 «літ», the fine one from 2 — the user 2026-11-18: «більша гарніша
    // скриня по суті заміняє її»): what a player left in the chest its state no longer has goes into the chest of that name it has now, and the old
    // one is hidden at once (Carry) — else LocState.PieceShows would keep it in sight for its things. The game that owns both does it, neither
    // open; the new one full: what did not fit stays, and the old chest in sight with it.
    // What is gathered is kept (the user 2026-11-18: «запобіжник для скрині… блокувати після цього взаємодію зі скринею, щоб інший гравець не зміг
    // вийняти в цей момент звідти матеріали»): a chest that holds all its stage wants of it (a stage that waits for its chests alone: one that
    // waits for more would keep a player's things shut away for long), or whose things the story is taking out (its word stands), is shut (Locked) —
    // the places' guard of E says no and why (LocInteractPatch, before any mod's own Container.Interact): nobody opens it; one this game has open as it fills up is closed (Look, Shut). Open again once the stage has moved on and its things are taken.
    // Cost: once in Every s the stages' few conditions, the world's word of each chest the data names (Known: a handful) and the pieces of each
    // such name (LocParts.Named: kept per look, two or three); the chest itself is looked up only when its word stands, it is hovered or opened.
    internal static class StoryChest
    {
        internal const string Take = "chesttake_";
        internal static float Every = 1f;
        private sealed class Want { public string Name, Prefab; public int N; public bool Shuts; } // (Name: «<place id>.<piece>», as NameOf says a chest · Shuts: its stage waits on chests alone — full, it is shut)
        private static readonly List<Want> _want = new List<Want>();               // session: what the stages of this player wait for in chests, read again each Every s
        private static readonly HashSet<string> _taking = new HashSet<string>();  // session: the chests («<place id>.<piece>») whose things the story is taking out now, read again each Every s
        private static readonly Dictionary<string, KeyValuePair<string, string>> Known = new Dictionary<string, KeyValuePair<string, string>>(); // session: the world's word → its chest and thing, for every «chest …» and «chesttake …» the data names (filled as the data is read)
        private static readonly Dictionary<string, string> _took = new Dictionary<string, string>(); // session: a word this game has acted on, till the world drops it (its removal comes back from the server a moment later)
        private static readonly Dictionary<int, string> _names = new Dictionary<int, string>();     // session: a chest hovered → its «<place id>.<piece>» («»: not a named piece of a place)
        private static float _next;

        internal static void Actions()
        {
            StoryCond.Register("chest", new StoryCond.Kind { Dry = "free", Usage = "chest <місце>.<деталь> <Prefab> <N>   (у скрині місця лежить стільки цього; лише там, де скриню видно)", Min = 3,
                Check = a => Why(a),
                Test = (p, a) => { Container c = Find(a[1]); return c != null && ItemMarks.InBag(c.GetInventory(), a[2]) >= Num(a[3]); } });
            StoryActions.Register("chesttake", new StoryActions.Kind { Dry = "none", Min = 3, Usage = "chesttake <місце>.<деталь> <Prefab> <N>   (забрати стільки цього зі скрині місця)",
                Check = a => Why(a),
                Do = (p, q, w, rest) => { StoryWorld.SetText(WordOf(w[1], w[2]), Num(w[3]).ToString()); return null; } });
        }

        private static string Key(string chest) { return chest.Replace('.', '_'); }
        private static string WordOf(string chest, string prefab) { return Take + Key(chest) + "__" + prefab.ToLowerInvariant(); }
        private static int Num(string s) { int n; return int.TryParse(s, out n) ? n : 0; }

        // what is wrong with «<place>.<piece> <Prefab> <N>» (null: fine); the chest is remembered for Look
        private static string Why(string[] a)
        {
            int dot = a[1].IndexOf('.');
            if (dot <= 0 || dot == a[1].Length - 1 || !Story.GoodName(WordOf(a[1], a[2]))) return a[0] + " <місце>.<деталь> <Prefab> <N> — «" + a[1] + " " + a[2] + "»";
            if (Num(a[3]) <= 0) return a[0] + ": скільки? «" + a[3] + "»";
            Known[WordOf(a[1], a[2])] = new KeyValuePair<string, string>(a[1], a[2]);
            return null;
        }

        // the chest of that name, loaded on this game (null: not here, or no such piece)
        private static Container Find(string chest)
        {
            int dot = chest.IndexOf('.');
            Loc l = dot > 0 ? Locs.Get(chest.Substring(0, dot)) : null;
            if (l == null) return null;
            foreach (ZNetView nv in LocParts.Named(l.Id, chest.Substring(dot + 1)))
            {
                if (nv == null || !nv.IsValid() || LocState.Absent(nv)) continue; // (one name, a piece a state: the abandoned Hall's chest is gone in the repaired one)
                Container c = nv.GetComponent<Container>();
                if (c != null) return c;
            }
            return null;
        }

        // once in Every s: what this player's stages wait for in chests (the hover), and the world's words «take out of this chest» for the chests
        // this game owns
        internal static void Look(Player p)
        {
            if (p == null || Time.time < _next) return;
            _next = Time.time + Every;
            _want.Clear();
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                StoryQuests.StageDef s = StoryQuests.StageNow(p, q);
                if (s == null || s.Wait == null) continue;
                bool shuts = s.OnName.Length == 0 && s.Wait.TrueForAll(IsChest); // (a stage that waits for more than its chest — an event, a quest: its things not shut away for so long)
                foreach (Cond c in s.Wait)
                    if (IsChest(c)) _want.Add(new Want { Name = DataName(c.A[1]), Prefab = c.A[2], N = Num(c.A[3]), Shuts = shuts });
            }
            _taking.Clear();
            foreach (KeyValuePair<string, KeyValuePair<string, string>> k in Known)
            {
                string word = StoryWorld.Text(k.Key), done;
                if (word.Length == 0) { _took.Remove(k.Key); continue; }
                _taking.Add(DataName(k.Value.Key));
                if (_took.TryGetValue(k.Key, out done) && done == word) continue; // (taken by this game already: the world has not dropped the word yet)
                Container c = Find(k.Value.Key);
                ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
                if (nv == null || !nv.IsValid() || !nv.IsOwner()) continue; // (the game that holds the chest does it: one game, no thing taken twice)
                _took[k.Key] = word;
                Story.Log("chest: " + k.Value.Key + ": taken out " + k.Value.Value + " " + TakeOut(c.GetInventory(), k.Value.Value, Num(word)) + "/" + word);
                StoryWorld.Remove(k.Key);
            }
            _carried.Clear();
            foreach (KeyValuePair<string, string> k in Known.Values)
                if (_carried.Add(k.Key)) { Carry(k.Key); Shut(p, k.Key); }
        }

        // a chest of that name this game has open (opened, the game gives its ZDO to the one who opened it) that is Locked now: closed, said why
        private static void Shut(Player p, string chest)
        {
            if (InventoryGui.instance == null || !InventoryGui.IsVisible() || !InventoryGui.instance.IsContainerOpen()) return;
            Container c = Find(chest);
            ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || !nv.IsOwner() || !c.IsInUse() || !Locked(c)) return;
            InventoryGui.instance.Hide();
            SayShut(p);
        }

        internal static void SayShut(Player p) { if (p != null) p.Message(MessageHud.MessageType.Center, Gathered()); }
        private static string Gathered() { return Lang.K("Зібрано все, що треба. Скриню зачинено, доки це не заберуть."); }

        // shut: it holds all its stage wants of it, or the story is taking its things out (the head: «What is gathered is kept»). Asked by the
        // places' guard of E (LocInteractPatch), the hover and Shut — a chest of no place's name answers at once
        internal static bool Locked(Container c)
        {
            if (c == null) return false;
            string name = NameOf(c);
            if (name.Length == 0) return false;
            if (_taking.Contains(name)) return true; // (being taken out: till the game by it has done it)
            bool any = false;
            foreach (Want w in _want)
            {
                if (w.Name != name) continue;
                if (!w.Shuts || ItemMarks.InBag(c.GetInventory(), w.Prefab) < w.N) return false;
                any = true;
            }
            return any;
        }

        private static bool IsChest(Cond c) { return !c.Not && c.A.Length >= 4 && c.A[0] == "chest"; }

        // «<place>.<piece>» of the data (a place's alias too) as NameOf says a chest: «<place id>.<piece>» («»: no such place)
        private static string DataName(string chest)
        {
            int dot = chest.IndexOf('.');
            Loc l = dot > 0 ? Locs.Get(chest.Substring(0, dot)) : null;
            return l != null ? l.Id + chest.Substring(dot) : "";
        }

        // the things of a chest of that name its place's state has not now into the one it has (the head: «One name over a place's states»)
        private static readonly HashSet<string> _carried = new HashSet<string>(); // (one pass's: each name once)
        private static readonly Dictionary<int, string> _tried = new Dictionary<int, string>(); // session: an old chest whose things did not all fit → both chests' counts then (tried again only when one changes)
        private static void Carry(string chest)
        {
            Container to = Find(chest);
            ZNetView tv = to != null ? to.GetComponent<ZNetView>() : null;
            if (tv == null || !tv.IsOwner() || to.IsInUse()) return;
            int dot = chest.IndexOf('.');
            Loc l = Locs.Get(chest.Substring(0, dot));
            foreach (ZNetView nv in LocParts.Named(l.Id, chest.Substring(dot + 1)))
            {
                if (nv == tv || !nv.IsValid() || !nv.IsOwner() || !LocState.Absent(nv)) continue; // (the game that owns both: no thing moved twice)
                Container from = nv.GetComponent<Container>();
                if (from == null || from.IsInUse()) continue; // (open: later)
                int had = Things(from.GetInventory()), id = nv.GetInstanceID();
                if (had == 0) { _tried.Remove(id); continue; } // (empty — or its inventory not filled yet: next time)
                string counts = had + "/" + Things(to.GetInventory()), was;
                if (_tried.TryGetValue(id, out was) && was == counts) continue; // (full last time, nothing changed since: no MoveAll and no log line a second)
                to.GetInventory().MoveAll(from.GetInventory()); // (the game's own: what fits; each chest saves its ZDO as its owner)
                int left = Things(from.GetInventory());
                if (left > 0) _tried[id] = left + "/" + Things(to.GetInventory()); else _tried.Remove(id);
                Story.Log("chest: " + chest + ": " + (had - left) + " of " + had + " thing(s) carried from the chest its state has not" + (left > 0 ? " — the rest has no room: that chest stays in sight" : ""));
                LocState.LookAgain(nv); // (emptied: hidden by its layer this moment; what did not fit: still in sight)
            }
        }

        // how many things an inventory holds (stacks summed: a stack topped up by MoveAll counts)
        private static int Things(Inventory inv)
        {
            int n = 0;
            foreach (ItemDrop.ItemData it in inv.GetAllItems()) n += it.m_stack;
            return n;
        }

        // up to N of that thing out of the inventory; how many went
        private static int TakeOut(Inventory inv, string prefab, int want)
        {
            int left = want;
            List<ItemDrop.ItemData> of = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData it in inv.GetAllItems())
                if (it.m_dropPrefab != null && it.m_dropPrefab.name == prefab) of.Add(it);
            foreach (ItemDrop.ItemData it in of) // (a list of its own: removing changes the inventory's)
            {
                if (left <= 0) break;
                int n = Mathf.Min(left, it.m_stack);
                inv.RemoveItem(it, n);
                left -= n;
            }
            return want - left;
        }

        // the lines under the hover of a chest the player's stage wants filled («»: not such a chest, or no stage waits on one)
        internal static string HoverOf(Container c)
        {
            if (_want.Count == 0 || c == null) return "";
            string name = NameOf(c);
            if (name.Length == 0) return "";
            string lines = "";
            foreach (Want w in _want)
            {
                if (w.Name != name) continue;
                int have = Mathf.Min(ItemMarks.InBag(c.GetInventory(), w.Prefab), w.N);
                lines += "\n" + ItemName(w.Prefab) + ": <color=" + (have >= w.N ? "#9BD08B" : "orange") + ">" + have + " / " + w.N + "</color>";
            }
            if (lines.Length > 0 && Locked(c)) lines += "\n<color=#9BD08B>" + Gathered() + "</color>"; // (shut: why it does not open)
            return lines.Length == 0 ? "" : "\n" + Lang.K("Сюди треба скласти:") + lines;
        }

        // «<place id>.<piece>» of a chest of a place's blueprint, found once a chest
        private static string NameOf(Container c)
        {
            string name;
            int id = c.GetInstanceID();
            if (_names.TryGetValue(id, out name)) return name;
            name = "";
            ZNetView nv = c.GetComponent<ZNetView>();
            if (Blueprint.Copy(nv)) // blueprint only: a named piece of a place's own blueprint
            {
                string of = nv.GetZDO().GetString(LocParts.KOf, ""), piece = of.Length > 0 ? LocParts.NameOf(of, nv) : "";
                if (piece.Length > 0) name = of + "." + piece;
            }
            if (_names.Count > 200) _names.Clear(); // (chests come and go with the zones)
            _names[id] = name;
            return name;
        }

        // the game's own name of the thing, in the player's language (no such item: its prefab)
        private static string ItemName(string prefab)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            return d != null && Localization.instance != null ? Localization.instance.Localize(d.m_itemData.m_shared.m_name) : prefab;
        }
    }
}
#endif
