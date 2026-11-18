#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A person's own thing, in its hand or on its belt (the user 2026-11-13, of the story's dwarf and its hammer: «щоб він іноді ходив просто з
    // молотком у руці а коли ховав його то він був видимий на поясі»). Its template's lines:
    //   tool: RavenWatch_item_molot_dvergra     the thing (an item's prefab: ours or the game's); its look only — nothing of it is wielded, dropped or kept
    //   tool_hand: 40                           of its free time, the share (%) it carries the thing in its hand; the rest it hangs on its belt
    //   tool_side: left                         the hand that carries it: left | right (none written: right); its clips are that hand's
    //   tool_grip: 0.07 -0.02 0 90 0 0          where it lies in the hand: metres and degrees in the body's frame as the hand rests in the skin's own
    //   tool_belt: -0.25 0.08 -0.02 10 90 0     pose (x right, y up, z ahead) — and where it hangs on the belt, the same way from the hips' bone
    //   tool_fights: yes                        it is what it fights with (the user 2026-11-15: the dwarf beats foes off with his hammer): the weapon of its
    //                                           «gear:» (a fighter's — NpcGear wields it in a fight, hangs it on its back after) gives the blows and is never
    //                                           seen; while that weapon is in its hand the thing is in its right hand (the game's blows are the right hand's),
    //                                           at the grip mirrored from tool_grip, or its own:
    //   tool_fightgrip: 0.07 -0.02 0 90 0 0     where it lies in the right hand while it fights (as tool_grip); «raven npc tool <who> fight» holds it so
    //                                           with no fight, to see it. In the right hand only while it fights (KFight, its mind's Fight layer) — its weapon
    //                                           still out on its guard after, the thing goes back where it was
    // Not a weapon of its cells (NpcGear gives those by its role, and one who never fights wields nothing): a thing that is part of who it is.
    // Where the thing is now is one word on the person (KHeld… no: KTool, bool: in hand) — every game shows the same; who runs the person decides: by the
    // share, every RollMin…RollMax s while nothing asks anything of it (never in a scene, a fight, a job's work, a clip); a scene or the console says
    // outright («raven npc tool <who> hand|belt|auto», the act «tool …»). Seen by a player it changes hands by a clip of its body — «<body>_tool_draw»,
    // «<body>_tool_stow» (dv_… for a dvergr), the thing passing at the clip's mark «swap»; no such clip, or nobody looking: at once.
    // The look is made once a person (HandLook.Copy) and moved between two bones; nothing per frame but a compare where it is drawn. A template read
    // anew (its numbers tuned: «raven npc reload») puts it in its place again.
    internal static class NpcTool
    {
        internal static readonly string[] Words = { "tool", "tool_hand", "tool_side", "tool_grip", "tool_belt", "tool_fights", "tool_fightgrip" };
        internal static readonly int KTool = "RavenWatch_npcTool".GetStableHashCode(); // bool: in its hand (else on its belt)
        internal static readonly int KFight = "RavenWatch_npcToolFight".GetStableHashCode(); // bool: «tool_fights» — it fights with it now (its right hand): its owner writes it, every game places it so
        internal static float RollMin = 60f, RollMax = 180f;

        private sealed class Kit { public string Item = ""; public float Hand; public bool Left, Fights, FightSaid; public Vector3 FightAt, FightTurn, GripAt = new Vector3(0.07f, -0.02f, 0f), GripTurn = new Vector3(90f, 0f, 0f), BeltAt = new Vector3(-0.25f, 0.08f, -0.02f), BeltTurn = new Vector3(10f, 90f, 0f); }
        private static readonly Dictionary<NpcDef, Kit> Kits = new Dictionary<NpcDef, Kit>(); // (a template read anew is another NpcDef: read again)

        private static Kit Of(NpcDef d)
        {
            Kit k;
            if (d == null) return null;
            if (Kits.TryGetValue(d, out k)) return k;
            foreach (string line in d.Keep)
            {
                int c = line.IndexOf(':');
                if (c <= 0) continue;
                string key = line.Substring(0, c).Trim().ToLowerInvariant(), v = line.Substring(c + 1).Trim();
                if (!key.StartsWith("tool", System.StringComparison.Ordinal)) continue;
                if (k == null) k = new Kit();
                string[] w = v.Split(new[] { ' ', ',', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (key == "tool") k.Item = v;
                else if (key == "tool_hand") k.Hand = Mathf.Clamp(Errands.F(v, 0f), 0f, 100f);
                else if (key == "tool_side") k.Left = v.ToLowerInvariant() == "left";
                else if (key == "tool_grip") Six(w, ref k.GripAt, ref k.GripTurn);
                else if (key == "tool_belt") Six(w, ref k.BeltAt, ref k.BeltTurn);
                else if (key == "tool_fights") k.Fights = v.ToLowerInvariant() == "yes" || v.ToLowerInvariant() == "true";
                else if (key == "tool_fightgrip") { k.FightAt = k.GripAt; k.FightTurn = k.GripTurn; Six(w, ref k.FightAt, ref k.FightTurn); k.FightSaid = true; }
            }
            if (k != null && k.Item.Length == 0) k = null;
            Kits[d] = k;
            return k;
        }
        private static void Six(string[] w, ref Vector3 at, ref Vector3 turn)
        {
            if (w.Length >= 3) at = new Vector3(Errands.F(w[0], at.x), Errands.F(w[1], at.y), Errands.F(w[2], at.z));
            if (w.Length >= 6) turn = new Vector3(Errands.F(w[3], turn.x), Errands.F(w[4], turn.y), Errands.F(w[5], turn.z));
        }

        // ---------------------------------------------------------------- its look, on every game
        private sealed class Look { public GameObject Thing; public NpcDef Def; public bool InHand, Placed, Fight; public float Sought; public GameObject Hid, HidBack, HidBackL; }
        private static readonly Dictionary<NpcActor, Look> Looks = new Dictionary<NpcActor, Look>(); // session: its picture on this game (Forget)
        private static readonly System.Reflection.FieldInfo VRight = HarmonyLib.AccessTools.Field(typeof(VisEquipment), "m_rightItemInstance"),
            VBack = HarmonyLib.AccessTools.Field(typeof(VisEquipment), "m_rightBackItemInstance"), VBackL = HarmonyLib.AccessTools.Field(typeof(VisEquipment), "m_leftBackItemInstance");

        internal static void Show(NpcActor a)
        {
            Kit k = Of(a.Def);
            Look l;
            if (k == null) { if (Looks.TryGetValue(a, out l)) Forget(a); return; }
            ZDO z = a.Zdo;
            if (z == null) return;
            if (!Looks.TryGetValue(a, out l)) Looks[a] = l = new Look();
            if (k.Fights) HideWeapon(a, l); // (its gear's weapon is never seen, drawn or on its back)
            bool fight = k.Fights && z.GetBool(KFight, false); // (it fights now: the thing in its right hand — not merely its weapon still out, on its guard after a fight: the user 2026-11-15, the hammer crooked in its right hand in a scene after the fight)
            bool hand = fight || z.GetBool(KTool, false);
            if (l.Thing != null && l.Placed && l.Def == a.Def && l.InHand == hand && l.Fight == fight) return;
            if (l.Thing == null || l.Def != a.Def)
            {
                if (Time.time < l.Sought) return; // (no such item, or its bones not there yet: asked again in a while, not every frame)
                l.Sought = Time.time + 5f;
                if (l.Thing != null) Object.Destroy(l.Thing);
                l.Thing = HandLook.Copy(k.Item, 0f, true, true); // heavy: once a person, and once a template read anew
                l.Def = a.Def; l.Placed = false;
                if (l.Thing == null) { Npcs.Log(a.Id + ": its tool «" + k.Item + "» is no item here"); return; }
            }
            Humanoid h = a.GetComponent<Humanoid>();
            Animator anim = a.GetComponentInChildren<Animator>(); // heavy: only as its thing is put in its place (it changed hands, a template read anew)
            Transform bone = anim != null ? BodyClips.SkeletonBone(h, anim.transform, fight ? "RightHand" : hand ? (k.Left ? "LeftHand" : "RightHand") : "Hips") : null;
            if (bone == null) return;
            Quaternion frame = bone.rotation * BodyFrame(h, bone); // (the body's frame as this bone carries it now)
            Vector3 at = hand ? k.GripAt : k.BeltAt, turn = hand ? k.GripTurn : k.BeltTurn;
            if (fight && k.FightSaid) { at = k.FightAt; turn = k.FightTurn; } // («tool_fightgrip»: its right hand's own)
            else if (fight && k.Left) { at.x = -at.x; turn.y = -turn.y; turn.z = -turn.z; } // (none written: its left hand's grip, mirrored into the right)
            float size = a.transform.lossyScale.y;
            l.Thing.transform.SetParent(null, true);
            l.Thing.transform.position = bone.position + frame * at * size;
            l.Thing.transform.rotation = frame * Quaternion.Euler(turn);
            l.Thing.transform.SetParent(bone, true);
            int layer = a.gameObject.layer;
            foreach (Transform t in l.Thing.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; // heavy: as above
            l.InHand = hand; l.Fight = fight; l.Placed = true;
        }

        // where its thing's look is on this game (its middle): a scene's close shot of it, «npc.<who>.thing» when the story gave it nothing else to hold
        internal static bool LookAt(NpcActor a, out Vector3 at)
        {
            Look l;
            at = Vector3.zero;
            if (a == null || !Looks.TryGetValue(a, out l) || l.Thing == null) return false;
            Renderer r = l.Thing.GetComponentInChildren<Renderer>(); // heavy: a shot's look at it (a few times a second while it is filmed)
            at = r != null ? r.bounds.center : l.Thing.transform.position;
            return true;
        }

        // «tool_fights»: its gear's weapon's own look, in the hand or on the back, put out whenever the game makes it anew (the game draws it from the
        // person's ZDO on every game: VisEquipment — a look at three fields a frame: one person, its kit)
        private static void HideWeapon(NpcActor a, Look l)
        {
            VisEquipment ve = a.GetComponent<VisEquipment>();
            if (ve == null || VRight == null) return;
            GameObject right = VRight.GetValue(ve) as GameObject, back = VBack != null ? VBack.GetValue(ve) as GameObject : null, backL = VBackL != null ? VBackL.GetValue(ve) as GameObject : null;
            if (right != l.Hid) { l.Hid = right; Unseen(right); }
            if (back != l.HidBack) { l.HidBack = back; Unseen(back); }
            if (backL != l.HidBackL) { l.HidBackL = backL; Unseen(backL); }
        }

        // where it is run: «tool_fights» — it fights now (its mind's Fight layer), written for every game (KFight); «raven npc tool <who> fight» holds it
        // so with no fight (the stage's look at its right hand's grip) till another word
        private static void FightWord(NpcActor a, Kit k, ZDO z, Mind m)
        {
            NpcMind mind = a.Mind;
            bool f = m.ForceFight || (mind != null && mind.NowLayer == NpcMind.Layer.Fight);
            if (z.GetBool(KFight, false) != f) z.Set(KFight, f);
        }
        private static void Unseen(GameObject g)
        {
            if (g == null) return;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) r.enabled = false; // heavy: once each time the game makes its weapon's look anew
        }

        // the body's frame in a bone's own, as the skin rests (its bind pose): what a thing on that bone is placed by, whatever the pose is now
        private static Quaternion BodyFrame(Humanoid h, Transform bone)
        {
            VisEquipment ve = h != null ? h.GetComponent<VisEquipment>() : null;
            SkinnedMeshRenderer smr = ve != null ? ve.m_bodyModel : null;
            if (smr != null && smr.bones != null && smr.sharedMesh != null)
            {
                Matrix4x4[] bind = smr.sharedMesh.bindposes;
                for (int i = 0; i < smr.bones.Length && i < bind.Length; i++)
                    if (smr.bones[i] != null && (smr.bones[i] == bone || BodyBuild.Real(smr.bones[i]) == bone))
                        return bind[i].rotation * Quaternion.Inverse(smr.transform.rotation) * h.transform.rotation;
            }
            return Quaternion.Inverse(bone.rotation) * h.transform.rotation; // (no skin to read: as it stands now)
        }

        // the hand its thing is in now: "left", "right", or "" (on its belt, or it has none) — a pose of its ease that needs that hand waits (NpcStance)
        internal static string HandOf(NpcActor a)
        {
            Kit k = a != null ? Of(a.Def) : null;
            ZDO z = k != null ? a.Zdo : null;
            return z != null && z.GetBool(KTool, false) ? (k.Left ? "left" : "right") : "";
        }

        internal static void Forget(NpcActor a)
        {
            Look l;
            if (Looks.TryGetValue(a, out l)) { if (l.Thing != null) Object.Destroy(l.Thing); Looks.Remove(a); }
            Minds.Remove(a);
        }

        // ---------------------------------------------------------------- where it is run: in hand or on the belt
        private sealed class Mind { public float Next; public int Order = -1; public float SwapAt; public bool SwapTo, ForceFight, Back; } // (Order: −1 by its share, 0 belt, 1 hand · Back: once there, its own share again)
        private static readonly Dictionary<NpcActor, Mind> Minds = new Dictionary<NpcActor, Mind>(); // session: a new owner rolls anew; the word itself is on the person

        internal static void Tick(NpcActor a)
        {
            Kit k = Of(a.Def);
            ZDO z = a.Zdo;
            if (k == null || z == null) return;
            Mind m;
            if (!Minds.TryGetValue(a, out m)) Minds[a] = m = new Mind { Next = Time.time + Random.Range(5f, RollMin) };
            if (k.Fights) FightWord(a, k, z, m); // (it fights now, or not: its right hand — before anything else asks it)
            if (m.SwapAt > 0f) { if (Time.time >= m.SwapAt) { m.SwapAt = 0f; if (z.GetBool(KTool, false) != m.SwapTo) z.Set(KTool, m.SwapTo); } return; } // (the clip's hand is at the belt now)
            bool now = z.GetBool(KTool, false), want;
            if (m.Order >= 0) want = m.Order == 1;
            else
            {
                if (Time.time < m.Next) return;
                m.Next = Time.time + Random.Range(RollMin, RollMax);
                want = Random.value * 100f < k.Hand;
            }
            if (want == now) { if (m.Back) { m.Back = false; m.Order = -1; m.Next = Time.time + Random.Range(RollMin, RollMax); } return; } // (a place's word done: its own share again)
            NpcMind mind = a.Mind; NpcBody body = a.Body;
            bool ordered = m.Order >= 0;
            if (!ordered && (mind == null || mind.NowLayer > NpcMind.Layer.Whim || NpcAlert.On(a) || NpcMoves.Talking(a))) { m.Next = Time.time + 10f; return; } // (busy with something asked of it: later)
            string clip = (a.Def.Dvergr ? "dv" : "npc") + (want ? "_tool_draw" : "_tool_stow");
            PAnimClip c = BodyClips.Find(clip);
            Character ch = a.GetComponent<Character>();
            bool still = ch != null && ch.GetVelocity().magnitude < 0.2f && ch.IsOnGround();
            if (c != null && NpcClock.Seen(a) && body != null)
            {
                if (!still || !body.ClipFree) { if (!ordered) m.Next = Time.time + 5f; return; } // (walking, or a clip on it: when it stands free — an order waits for that too)
                if (body.PoseSaid) { body.Clip(""); body.PoseWord(false); } // (a pose of its ease gives way)
                if (body.Clip(clip, c.Length + 1f))
                {
                    float at = c.Length * 0.5f;
                    foreach (PAnimEvent e in c.Marks) if (e.Sound == "swap") at = e.T;
                    m.SwapAt = Time.time + at; m.SwapTo = want;
                    return;
                }
            }
            z.Set(KTool, want); // (nobody looking, or no clip for its body: at once)
        }

        // a place's word (NpcPlaces.ClipBegins / ClipEnds, «spot … tool»: Lit's hammer at the wall): its thing into its hand for the place's clip — held there
        // till the visit ends —, then back on its belt and its own share again. The s it takes (its clip seen, else at once); 0: it has it already or none
        internal static float Take(NpcActor a, bool hand)
        {
            if (Of(a.Def) == null || a.Zdo == null) return 0f;
            Mind m;
            if (!Minds.TryGetValue(a, out m)) Minds[a] = m = new Mind { Next = Time.time + Random.Range(5f, RollMin) };
            m.Order = hand ? 1 : 0; m.Back = !hand;
            if (a.Zdo.GetBool(KTool, false) == hand && m.SwapAt <= 0f) return 0f;
            PAnimClip c = BodyClips.Find((a.Def.Dvergr ? "dv" : "npc") + (hand ? "_tool_draw" : "_tool_stow"));
            return c != null && NpcClock.Seen(a) ? c.Length + 0.3f : 0.5f;
        }

        // a scene's or the console's word, where the person is run: hand | belt | auto
        internal static void Act(NpcActor a, string[] w)
        {
            if (Of(a.Def) == null || w.Length < 2) { NpcActor.NotDone(); return; }
            Mind m;
            if (!Minds.TryGetValue(a, out m)) Minds[a] = m = new Mind();
            m.ForceFight = w[1] == "fight"; // (the stage's look at its fight grip: held till another word)
            m.Order = w[1] == "hand" || w[1] == "fight" ? 1 : w[1] == "belt" ? 0 : -1;
            if (m.Order < 0) m.Next = Time.time + Random.Range(RollMin, RollMax);
            if (w.Length > 2 && w[2] == "now" && m.Order >= 0 && a.Zdo != null) a.Zdo.Set(KTool, m.Order == 1); // (a scene that cuts to it: no clip)
        }

        internal static string Cmd(NpcActor who, string[] a)
        {
            Kit k = Of(who.Def);
            if (k == null) return who.Name + ": у шаблоні нема рядка tool:.";
            string word = a.Length > 3 ? a[3] : "";
            if (word != "hand" && word != "belt" && word != "auto" && word != "fight") return who.Name + ": " + k.Item + " зараз " + (who.Zdo != null && who.Zdo.GetBool(KTool, false) ? "у руці" : "на поясі") + ", у руці " + k.Hand.ToString("0", CultureInfo.InvariantCulture) + " % часу. raven npc tool <хто> hand|belt|auto|fight [now]   (fight — у правій, як у бою: глянути бойовий хват)";
            who.Act("tool " + word + (a.Length > 4 ? " " + a[4] : ""));
            return who.Name + ": " + (word == "hand" ? "бере в руку" : word == "belt" ? "вішає на пояс" : word == "fight" ? "тримає, як у бою (права рука)" : "сам вирішує") + ".";
        }

        internal static void Wire()
        {
            NpcActor.Everywhere.Add(Show);
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
            NpcActor.Acts["tool"] = Act;
            Npcs.Commands["tool"] = (p, who, a, aim) => Cmd(who, a);
            foreach (string w in Words) NpcDefs.Words.Add(w);
        }
    }
}
#endif
