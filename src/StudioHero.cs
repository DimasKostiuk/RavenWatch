#if RAVEN_ITEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Студія» on you (the player's own animation, our clips over the game's):
    //   ylook <name> [views]                              — you as you stand now
    //   ypose <name> <clip> [moments=6] [views] [air]     — one of our clips (BodyClips) held on you moment by moment
    //   yplay <name> <clip> [fps=8] [views] [air]         — the clip played on you in time
    //   yfilm <name> [seconds=4] [fps=6] [views]          — you as you move now (the game's animation, the rules, the gifts)
    //   ywait <name> <event> [seconds=3] [fps=8] [views] [give up after s] — waits for it (the raven events, jump, dodge,
    //                                                       swim, attack, air, land, wings, beast), then films you
    //   gift owings|obeast|end                            — Odin's wings / Munin's beast started on you (no cost, no checks)
    //   gift <raven gift id> [delay s]                    — a raven gift used (pauses reset, strength full; with a delay, a «fly» may wait next)
    //   fate <h|m>                                        — the Fate stone's show and your gesture, no stone spent, no fate changed
    //   ytakeoff <name> [seconds=2.6] [fps=10] [views]  — Odin's wings started and the take-off filmed from its first frame
    //   armor <ItemPrefab|none>, armors                  — put armour on you (none: what the studio put on, off and away); the list
    //   yreload                                          — our clips and rules read again (config/RavenWatch/player_anims)
    //   react <ear|steal|shake|sip|steady|palm|wave|quarrel|boss|story|warm|fire_<mood>|down|up> [h|m] — your answer to the raven
    //                                                       (PlayerReact, Fireside) played at once
    // air: you are tossed up first (16 m/s) and shot on the way up: the clip over the game's falling pose (the wings).
    // Views: front, back, side (your right), left, top, three (from the front right), feet (low, from the front).
    // The report with every sheet: per frame what plays on each layer, the motion, where each limb points in the body's
    // frame (the clips' «aim» numbers: up, aside), a hand through the body, a foot under the ground, the legs crossed,
    // and the fastest bone turns (a jerk).
    internal static partial class Studio
    {
        private static Animator HeroAnim(Player p) { return p != null ? p.GetComponentInChildren<Animator>() : null; }

        private static readonly string[] HeroFrame = { "Head", "LeftHand", "RightHand", "LeftToeBase", "RightToeBase", "LeftFoot", "RightFoot", "Hips", "Spine2" };

        // the cameras round you: framed on your bones (not the things you carry or the ravens on you)
        private static void PlaceHero(Rig r, Player p)
        {
            Animator an = HeroAnim(p);
            Transform v = an != null ? an.transform : p.transform;
            Bounds b = new Bounds(v.position + Vector3.up, Vector3.one * 0.4f);
            bool first = true;
            foreach (string n in HeroFrame)
            {
                Transform t = HeroBone(p, n);
                if (t == null) continue;
                if (first) { b = new Bounds(t.position, Vector3.zero); first = false; } else b.Encapsulate(t.position);
            }
            Transform headB = HeroBone(p, "Head");
            if (headB != null) b.Encapsulate(headB.position + v.up * 0.3f * v.lossyScale.y); // (the head bone is its bottom: the top of it, the hair, above)
            b.Expand(0.35f);
            Vector3 mid = b.center;
            float size = Mathf.Clamp(Mathf.Max(b.size.y, Mathf.Max(b.size.x, b.size.z)), 1f, 5f);
            float d = size * 2.1f; // (field of view 30°: the whole of you with a little room)
            Vector3 fwd = Vector3.ProjectOnPlane(v.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up);
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            // close views (c + a view: cfront, cside, ctop, cthree, cleft): framed on your hands, chest and a raven on your arm
            Vector3 near = mid, sum = Vector3.zero; int nn = 0;
            foreach (string n in new[] { "LeftHand", "RightHand", "Spine2" }) { Transform t = HeroBone(p, n); if (t != null) { sum += t.position; nn++; } }
            foreach (bool mu in new[] { false, true }) { GameObject am = ArmPerch.Holding(p, mu) ? Shoulder.ModelOf(p, mu) : null; if (am != null) { sum += am.transform.position + Vector3.up * 0.1f; nn++; } }
            if (nn > 0) near = sum / nn;
            for (int i = 0; i < r.Cams.Count; i++)
            {
                Vector3 from;
                string view = r.Views[i];
                bool close = view.Length > 1 && view[0] == 'c';
                if (close) view = view.Substring(1);
                Vector3 at = close ? near : mid;
                float cs = close ? 0.85f : size, cd = close ? cs * 2.1f : d;
                switch (view)
                {
                    case "back": from = at - fwd * cd + Vector3.up * cs * 0.15f; break;
                    case "left": from = at - right * cd + Vector3.up * cs * 0.1f; break;
                    case "top": from = at + Vector3.up * cd * 1.05f + fwd * cs * 0.05f; break;
                    case "three": from = at + (fwd + right).normalized * cd + Vector3.up * cs * 0.35f; break;
                    case "feet": from = at + fwd * cd * 0.8f - Vector3.up * cs * 0.3f; break;
                    case "side": from = at + right * cd + Vector3.up * cs * 0.1f; break;
                    default: from = at + fwd * cd + Vector3.up * cs * 0.15f; break; // front
                }
                r.Cams[i].farClipPlane = 80f;
                if (close) r.Cams[i].nearClipPlane = 0.05f;
                r.Cams[i].transform.position = from;
                r.Cams[i].transform.rotation = Quaternion.LookRotation(at - from, view == "top" ? fwd : Vector3.up); // (from above: your way is up on the picture)
            }
            r.Mid = mid;
            r.Lamp.range = size * 3.5f;
        }

        private static readonly Dictionary<string, Transform> HeroBones = new Dictionary<string, Transform>();
        private static Animator _heroBonesOf;
        private static Transform HeroBone(Player p, string name)
        {
            Animator an = HeroAnim(p);
            if (an == null) return null;
            if (an != _heroBonesOf)
            {
                HeroBones.Clear();
                _heroBonesOf = an;
            }
            Transform b;
            if (HeroBones.TryGetValue(name, out b) && b != null && b.IsChildOf(an.transform) && b.gameObject.activeInHierarchy) return b; // (a body rebuilt by new armour: looked up again)
            b = BodyClips.SkeletonBone(p, an.transform, name);
            if (b != null) HeroBones[name] = b;
            return b;
        }

        // one sheet per view, the frames in a grid of four
        private static Texture2D[] HeroSheets(Rig r, int frames, out int cols, out int gridRows)
        {
            cols = Mathf.Min(4, frames); gridRows = (frames + cols - 1) / cols;
            Texture2D[] sheets = new Texture2D[r.Cams.Count];
            for (int v = 0; v < sheets.Length; v++) { sheets[v] = new Texture2D(Cell * cols, Cell * gridRows, TextureFormat.RGB24, false); Fill(sheets[v], new Color32(20, 20, 20, 255)); }
            return sheets;
        }

        private static void HeroSave(string name, Rig r, Texture2D[] sheets)
        {
            for (int v = 0; v < sheets.Length; v++) Save(name + "_" + r.Views[v], sheets[v]);
        }

        private static void HeroFree(Texture2D[] sheets, Rig r)
        {
            if (sheets != null) foreach (Texture2D t in sheets) if (t != null) UnityEngine.Object.Destroy(t);
            Drop(r);
        }

        private static void Toss(Player p)
        {
            // the game's own push up (a velocity set on the body is overwritten by its motion the next frame)
            System.Reflection.MethodInfo jump = HarmonyLib.AccessTools.Method(typeof(Character), "ForceJump");
            if (jump != null)
            {
                object[] args = jump.GetParameters().Length == 2 ? new object[] { Vector3.up * 16f, false } : new object[] { Vector3.up * 16f };
                jump.Invoke(p, args);
                return;
            }
            Rigidbody rb = p.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = rb.linearVelocity + Vector3.up * 16f;
        }

        // ---------------------------------------------------------------- the commands

        private static IEnumerator HeroLook(string name, string views)
        {
            Player p = Player.m_localPlayer;
            if (p == null) { Note(name, "no player"); yield break; }
            Rig r = Make(views);
            Texture2D sheet = new Texture2D(Cell * r.Cams.Count, Cell, TextureFormat.RGB24, false);
            HeroReport rep = new HeroReport(p);
            try
            {
                yield return new WaitForEndOfFrame();
                PlaceHero(r, p);
                Shoot(r, new[] { sheet }, 0, r.Cams.Count, 1);
                rep.Take(0f);
                Save(name, sheet);
            }
            finally { UnityEngine.Object.Destroy(sheet); Drop(r); }
            Note(name, "you as you stand now; columns: " + string.Join(", ", r.Views.ToArray()) + "\n\n" + rep.Report(false));
        }

        // our clip on you: held moment by moment (pose) or played in time (play), on the test layer (over all the rest)
        private static IEnumerator HeroPose(string name, string clip, int moments, string views, bool play, float fps, bool air)
        {
            Player p = Player.m_localPlayer;
            PAnimClip c = BodyClips.Find(clip);
            if (p == null) { Note(name, "no player"); yield break; }
            if (c == null) { Note(name, "no clip «" + clip + "»; there are: " + string.Join(", ", new List<string>(BodyClips.Clips.Keys).ToArray())); yield break; }
            BodyAnimator a = BodyAnimator.For(p);
            const int Slot = BodyClips.PreviewSlot;
            List<float> at = new List<float>();
            int rows;
            if (!play)
            {
                rows = Mathf.Clamp(moments, 2, 12);
                for (int i = 0; i < rows; i++) at.Add(c.Length * i / (rows - 1));
            }
            else
            {
                if (c.Length * fps > 16f) fps = 16f / Mathf.Max(0.3f, c.Length); // (the whole clip, fewer frames a second)
                rows = Mathf.Clamp(Mathf.RoundToInt(c.Length * fps) + 1, 2, 16);
                for (int i = 0; i < rows; i++) at.Add(i / Mathf.Max(0.5f, fps));
            }
            Rig r = Make(views);
            int cols, gridRows;
            Texture2D[] sheets = HeroSheets(r, rows, out cols, out gridRows);
            HeroReport rep = new HeroReport(p);
            try
            {
                if (air) { Toss(p); float up = Time.time + 0.25f; while (Time.time < up) yield return null; }
                BodyClips.Play(p, Slot, c.Name, play ? 0.01f : 0.15f, 1f);
                if (!play) { a.Hold(Slot, 0f); float settle = Time.time + 0.35f; while (Time.time < settle) yield return null; } // (faded in fully)
                float start = Time.time;
                for (int i = 0; i < rows; i++)
                {
                    if (!play) a.Hold(Slot, at[i]);
                    else while (Time.time - start < at[i]) yield return null;
                    yield return null; yield return null; // (the pose is written in the next frames)
                    yield return new WaitForEndOfFrame();
                    PlaceHero(r, p);
                    Shoot(r, sheets, i, cols, gridRows);
                    rep.Take(play ? Time.time - start : at[i]);
                }
                HeroSave(name, r, sheets);
            }
            finally
            {
                if (a != null) a.Hold(Slot, -1f);
                BodyClips.Stop(p, Slot, 0.3f);
                HeroFree(sheets, r);
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("«").Append(c.Name).Append("» (").Append(BodyClips.N(c.Length)).Append(" s").Append(c.Loop ? ", loop" : "").Append(c.Frozen ? ", frozen base" : "").Append(") ")
              .Append(play ? "played in time, " + fps.ToString("0.#", CultureInfo.InvariantCulture) + " frames a second" : "held moment by moment").Append(air ? ", tossed up (over the game's falling pose)" : "")
              .Append("; frames at ");
            for (int i = 0; i < at.Count; i++) sb.Append(i > 0 ? ", " : "").Append(at[i].ToString("0.00", CultureInfo.InvariantCulture)).Append(" s");
            sb.Append("\none sheet per view: ").Append(name).Append("_<view>.png for ").Append(string.Join(", ", r.Views.ToArray())).Append("; 4 a row, left to right, top to bottom\n\n");
            sb.Append(rep.Report(play));
            Note(name, sb.ToString());
        }

        // you as you move: whatever plays (the game's animation, the rules, a gift), nothing asked
        private static IEnumerator HeroFilm(string name, float seconds, float fps, string views, string why)
        {
            Player p = Player.m_localPlayer;
            if (p == null) { Note(name, "no player"); yield break; }
            if (seconds * fps > 32f) fps = 32f / Mathf.Max(0.5f, seconds);
            int rows = Mathf.Clamp(Mathf.RoundToInt(seconds * fps), 2, 32);
            Rig r = Make(views);
            int cols, gridRows;
            Texture2D[] sheets = HeroSheets(r, rows, out cols, out gridRows);
            HeroReport rep = new HeroReport(p);
            try
            {
                float start = Time.time;
                for (int i = 0; i < rows; i++)
                {
                    while (Time.time - start < i / Mathf.Max(0.5f, fps)) yield return null;
                    yield return new WaitForEndOfFrame();
                    PlaceHero(r, p);
                    Shoot(r, sheets, i, cols, gridRows);
                    rep.Take(Time.time - start);
                }
                HeroSave(name, r, sheets);
            }
            finally { HeroFree(sheets, r); }
            Note(name, (why != null ? why + "; then " : "") + "you as you moved, " + rows + " frames, " + fps.ToString("0.#", CultureInfo.InvariantCulture) + " a second, 4 a row, left to right, top to bottom; sheets "
                + name + "_<view>.png for " + string.Join(", ", r.Views.ToArray()) + "\n\n" + rep.Report(true));
        }

        private static IEnumerator HeroWait(string name, string what, float seconds, float fps, string views, float giveUp)
        {
            what = what.ToLowerInvariant();
            int seen = LastEvent();
            float until = Time.time + Mathf.Clamp(giveUp, 5f, 300f);
            Player p0 = Player.m_localPlayer;
            bool wasAir = p0 != null && !p0.IsOnGround();
            bool wasWings = OdinWings.Flying(), wasBeast = Beast.On(); // (a new start only: not the gift still on)
            Note(name, "waiting for «" + what + "»…");
            while (true)
            {
                if (Time.time > until) { Note(name, "nothing of «" + what + "» happened in " + Mathf.RoundToInt(giveUp) + " s"); yield break; }
                Player me = Player.m_localPlayer;
                if (me != null)
                {
                    bool air = !me.IsOnGround();
                    if (what == "attack" && (me.InAttack() || me.IsDrawingBow())) break; // (a bow drawn counts: the draw is its own state)
                    if (what == "air" && air) break;
                    if (what == "land" && wasAir && !air) break;
                    if (what == "wings" && OdinWings.Flying() && !wasWings) break;
                    if (what == "beast" && Beast.On() && !wasBeast) break;
                    wasWings = OdinWings.Flying(); wasBeast = Beast.On();
                    if (Happened(me, what, ref seen)) break;
                    wasAir = air;
                }
                yield return null;
            }
            IEnumerator film = HeroFilm(name, seconds, fps, views, "«" + what + "» happened");
            while (film.MoveNext()) yield return film.Current;
        }

        private static string FateShowNow(string which)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return "no player";
            bool munin = which.StartsWith("m");
            GameObject m = Shoulder.ModelOf(p, munin);
            bool right = m != null ? Vector3.Dot(m.transform.position - p.transform.position, p.transform.right) >= 0f : !munin;
            BodyClips.Play(p, 2, right ? "fate_offer_r" : "fate_offer_l", 0.25f, 1f);
            FateShow.Make(p, munin, right, false, false);
            return "fate shown on the " + (right ? "right" : "left");
        }

        // Odin's wings started and filmed from that very frame (the take-off whole)
        private static IEnumerator HeroTakeoff(string name, float seconds, float fps, string views)
        {
            string said = Gift("owings", 0f);
            IEnumerator film = HeroFilm(name, seconds, fps, views, said);
            while (film.MoveNext()) yield return film.Current;
        }

        // armour put on you for the studio (a test of how the ravens sit on it): what it added is taken back on «none»
        private static readonly List<ItemDrop.ItemData> Dressed = new List<ItemDrop.ItemData>();
        private static string Armour(string name)
        {
            Player p = Player.m_localPlayer;
            if (p == null || ObjectDB.instance == null) return "no player";
            if (name == "none")
            {
                int n = 0;
                foreach (ItemDrop.ItemData it in Dressed) { if (p.IsItemEquiped(it)) p.UnequipItem(it, false); if (p.GetInventory().ContainsItem(it)) { p.GetInventory().RemoveItem(it); n++; } }
                Dressed.Clear();
                return "taken off and away: " + n;
            }
            GameObject prefab = ObjectDB.instance.GetItemPrefab(name);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null) return "no item " + name + " (armors lists them)";
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab; item.m_stack = 1; item.m_durability = item.GetMaxDurability();
            if (!p.GetInventory().AddItem(item)) return "no room in your bag";
            Dressed.Add(item);
            p.EquipItem(item, false);
            return "wearing " + name + " (" + item.m_shared.m_itemType + ")";
        }

        private static string Armours()
        {
            if (ObjectDB.instance == null) return "no ObjectDB";
            List<string> chest = new List<string>(), cape = new List<string>();
            foreach (GameObject g in ObjectDB.instance.m_items)
            {
                ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
                if (d == null) continue;
                if (d.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Chest) chest.Add(g.name);
                else if (d.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shoulder) cape.Add(g.name);
            }
            return "chest: " + string.Join(" ", chest.ToArray()) + " | capes: " + string.Join(" ", cape.ToArray());
        }

        // a gift's pose on a raven of yours, no gift used (the heal's hug at full health, a thing put on)
        private static string MomentNow(string which, string pose, float seconds)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return "no player";
            bool munin = which.ToLowerInvariant().StartsWith("m");
            if (!ShoulderLife.Poses.ContainsKey(pose)) return "moment h|m " + string.Join("|", new List<string>(ShoulderLife.Poses.Keys).ToArray()) + " [s]";
            ShoulderLife.Moment(p, munin, pose, Mathf.Clamp(seconds, 0.5f, 30f));
            return (munin ? "Munin" : "Hugin") + ": pose «" + pose + "» for " + seconds + " s";
        }

        // a raven gift used after a delay (so a «fly» that waits for the raven to take wing can come next in the file)
        private static IEnumerator GiftLater(RavenAbility a, float delay)
        {
            yield return new WaitForSeconds(delay);
            RavenSkills.Use(a);
        }

        private static string Gift(string which, float delay)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return "no player";
            switch (which.ToLowerInvariant())
            {
                case "owings": if (OdinWings.Flying()) return "already flying"; OdinWings.Start(p); return "Odin's wings on";
                case "obeast": if (Beast.On()) return "already the beast"; Beast.Start(p); return "Munin's beast on";
                case "end":
                    if (OdinWings.Flying()) OdinWings.Sink(p);
                    if (Beast.On()) Beast.End(p, false);
                    return "ended (wings sink down)";
                default:
                    // any raven gift by its id (fury, dome, hstrike, mstrike, hdecoy, ward, scream, heal…): pauses
                    // reset and both ravens' strength full first, so a take can be filmed again at once
                    RavenAbility a = RavenAbilities.Find(which.ToLowerInvariant());
                    if (a == null) return "gift owings|obeast|end|<raven gift id>";
                    RavenSkills.ResetCooldowns();
                    foreach (bool munin in new[] { false, true }) { ItemDrop.ItemData item = Trust.Worn(munin); if (item != null) Power.SetValue(item, 9999f); }
                    if (delay > 0f && Plugin.Instance != null) { Plugin.Instance.StartCoroutine(GiftLater(a, delay)); return "raven gift «" + a.Id + "» in " + delay + " s"; }
                    RavenSkills.Use(a);
                    return "raven gift «" + a.Id + "» used (see the screen for a refusal)";
            }
        }

        // ---------------------------------------------------------------- the report

        private sealed class HeroReport
        {
            private static readonly string[] Limbs = { "LeftArm", "LeftForeArm", "RightArm", "RightForeArm", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot", "Neck", "Head" };
            private static readonly string[] Short = { "lArm", "lFore", "rArm", "rFore", "lThigh", "lShin", "lFoot", "rThigh", "rShin", "rFoot", "neck", "head" };
            private readonly Player _p;
            private readonly List<Transform> _all = new List<Transform>();
            private readonly List<float> _times = new List<float>();
            private readonly List<Quaternion[]> _rot = new List<Quaternion[]>();
            private readonly StringBuilder _frames = new StringBuilder();
            private readonly List<string> _faults = new List<string>();

            internal HeroReport(Player p)
            {
                _p = p;
                HeroBones.Clear(); // (fresh: the armour may have rebuilt the body since)
                foreach (string[] g in BodyClips.Groups) foreach (string n in g) { Transform t = HeroBone(p, n); if (t != null) _all.Add(t); }
            }

            // a bone's direction (to the next in its chain) in the body's frame, as the clips' aim numbers
            private string Aim(Transform v, string bone)
            {
                Transform t = HeroBone(_p, bone);
                string nx = BodyClips.NextOf(bone);
                Transform n = nx != null ? HeroBone(_p, nx) : null;
                if (t == null || n == null) return "—";
                Vector3 d = Quaternion.Inverse(v.rotation) * (n.position - t.position);
                if (d.sqrMagnitude < 1e-6f) return "—";
                d.Normalize();
                float up = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg, aside = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                return Mathf.RoundToInt(up) + "/" + Mathf.RoundToInt(aside);
            }

            internal void Take(float time)
            {
                _times.Add(time);
                Quaternion[] q = new Quaternion[_all.Count];
                for (int i = 0; i < _all.Count; i++) q[i] = _all[i] != null ? _all[i].localRotation : Quaternion.identity;
                _rot.Add(q);
                Animator an = HeroAnim(_p);
                if (an == null) return;
                Transform v = an.transform;
                string at = time.ToString("0.00", CultureInfo.InvariantCulture) + " s";
                _frames.Append("  ").Append(at).Append(":");
                // the body itself: tilted from upright (pitch + forward, roll + left side down)
                Vector3 e = (Quaternion.Inverse(_p.transform.rotation) * v.rotation).eulerAngles;
                _frames.Append(" body ").Append(Mathf.RoundToInt(Mathf.DeltaAngle(0f, e.x))).Append("/").Append(Mathf.RoundToInt(Mathf.DeltaAngle(0f, e.z)));
                for (int i = 0; i < Limbs.Length; i++) _frames.Append(" ").Append(Short[i]).Append(" ").Append(Aim(v, Limbs[i]));
                // what plays
                BodyAnimator a = BodyAnimator.Of(_p);
                if (a != null)
                {
                    for (int s = 0; s < BodyClips.Slots; s++)
                    {
                        string c = a.ClipIn(s);
                        if (c == null || a.WeightIn(s) < 0.01f) continue;
                        string rule = a.RuleIn(s);
                        _frames.Append(" | L").Append(s).Append(" ").Append(c).Append(rule != null ? " (rule " + rule + ")" : "").Append(" w").Append(a.WeightIn(s).ToString("0.0", CultureInfo.InvariantCulture))
                               .Append(" @").Append(a.TimeIn(s).ToString("0.00", CultureInfo.InvariantCulture));
                    }
                    _frames.Append(" | speed ").Append(a.ValueOf("speed").ToString("0.0", CultureInfo.InvariantCulture)).Append(" vy ").Append(a.ValueOf("vy").ToString("0.0", CultureInfo.InvariantCulture));
                }
                // each hand to the head of the raven on that side (the feeding, the stone: does it reach the beak)
                foreach (bool munin in new[] { false, true })
                {
                    GameObject m = Shoulder.ModelOf(_p, munin);
                    Transform rh = m != null ? RavenHead(m) : null;
                    if (rh == null) continue;
                    bool rightSide = Vector3.Dot(m.transform.position - _p.transform.position, _p.transform.right) >= 0f;
                    Transform hand = HeroBone(_p, rightSide ? "RightHand" : "LeftHand");
                    if (_times.Count == 1) _frames.Append(" [").Append(munin ? "Munin" : "Hugin").Append(" head bone: ").Append(rh.name).Append(" under ").Append(rh.parent != null ? rh.parent.name : "-").Append("]");
                    if (hand != null) _frames.Append(" | ").Append(rightSide ? "rHand" : "lHand").Append("→").Append(munin ? "Munin" : "Hugin").Append(" head ").Append(Mathf.RoundToInt(Vector3.Distance(hand.position, rh.position) * 100f)).Append(" cm");
                }
                // a raven on your arm: the other hand's fingertips (a hand ahead of the wrist) to the nearest of its bones — under 5 cm: INSIDE (in its body), to 9: touch
                foreach (bool munin in new[] { false, true })
                {
                    if (!ArmPerch.Holding(_p, munin)) continue;
                    GameObject am = Shoulder.ModelOf(_p, munin);
                    if (am == null) continue;
                    bool birdRight = Vector3.Dot(am.transform.position - _p.transform.position, _p.transform.right) >= 0f;
                    Transform fh = HeroBone(_p, birdRight ? "LeftHand" : "RightHand"), ff = HeroBone(_p, birdRight ? "LeftForeArm" : "RightForeArm");
                    if (fh == null || ff == null) continue;
                    Vector3 tip = fh.position + (fh.position - ff.position).normalized * 0.09f;
                    float best = 99f; string bn = "-";
                    float foot = 99f, spine = 99f;
                    foreach (Transform bt in am.GetComponentsInChildren<Transform>())
                    {
                        float dd = Vector3.Distance(tip, bt.position);
                        if (dd < best) { best = dd; bn = bt.name; }
                        string ln = bt.name.ToLowerInvariant();
                        if ((ln.Contains("talon") || ln.Contains("toe") || ln.Contains("foot")) && dd < foot) foot = dd;
                        if ((ln.Contains("spine") || ln.Contains("hip") || ln.Contains("chest")) && dd < spine) spine = dd;
                    }
                    Transform bh = RavenHead(am);
                    _frames.Append(" | free hand tip→arm bird ").Append(Mathf.RoundToInt(best * 100f)).Append(" cm (").Append(bn).Append(")").Append(best < 0.05f ? " INSIDE" : best < 0.09f ? " touch" : "")
                           .Append(" head ").Append(bh != null ? Mathf.RoundToInt(Vector3.Distance(tip, bh.position) * 100f) : -1).Append(" back ").Append(Mathf.RoundToInt(spine * 100f)).Append(" feet ").Append(Mathf.RoundToInt(foot * 100f));
                }
                _frames.Append(_p.IsOnGround() ? " ground" : " air").Append(_p.InWater() ? " water" : "").Append('\n');
                Faults(v, at);
            }

            // a hand in the body, a foot under the ground, the legs crossed, a hand in the head
            private void Faults(Transform v, string at)
            {
                Transform hips = HeroBone(_p, "Hips"), chest = HeroBone(_p, "Spine2"), head = HeroBone(_p, "Head");
                float scale = Mathf.Max(0.3f, v.lossyScale.y);
                foreach (string side in new[] { "Left", "Right" })
                {
                    Transform hand = HeroBone(_p, side + "Hand"), fore = HeroBone(_p, side + "ForeArm");
                    if (hand != null && hips != null && chest != null)
                    {
                        float dist = DistToSegment(hand.position, hips.position, chest.position) / scale;
                        if (dist < 0.11f) _faults.Add(at + ": " + side.ToLowerInvariant() + " hand in the body (" + Mathf.RoundToInt(dist * 100f) + " cm from the spine)");
                    }
                    if (fore != null && hips != null && chest != null)
                    {
                        float dist = DistToSegment(fore.position, hips.position, chest.position) / scale;
                        if (dist < 0.1f) _faults.Add(at + ": " + side.ToLowerInvariant() + " elbow in the body (" + Mathf.RoundToInt(dist * 100f) + " cm from the spine)");
                    }
                    if (hand != null && head != null)
                    {
                        float dist = Vector3.Distance(hand.position, head.position + v.up * 0.08f * scale) / scale;
                        if (dist < 0.1f) _faults.Add(at + ": " + side.ToLowerInvariant() + " hand in the head");
                    }
                    Transform foot = HeroBone(_p, side + "ToeBase") ?? HeroBone(_p, side + "Foot");
                    RaycastHit hit;
                    if (foot != null && _p.IsOnGround() && Physics.Raycast(foot.position + Vector3.up * 1.5f, Vector3.down, out hit, 3f, LayerMask.GetMask("Default", "static_solid", "terrain", "piece"), QueryTriggerInteraction.Ignore))
                    {
                        float under = hit.point.y - foot.position.y;
                        if (under > 0.08f) _faults.Add(at + ": " + side.ToLowerInvariant() + " foot " + Mathf.RoundToInt(under * 100f) + " cm under the ground");
                    }
                }
                Transform lk = HeroBone(_p, "LeftLeg"), rk = HeroBone(_p, "RightLeg"), lf = HeroBone(_p, "LeftFoot"), rf = HeroBone(_p, "RightFoot");
                if (lk != null && rk != null && lf != null && rf != null)
                {
                    float knees = Vector3.Dot(lk.position - rk.position, v.right), feet = Vector3.Dot(lf.position - rf.position, v.right);
                    if (knees > 0.02f) _faults.Add(at + ": knees crossed (left knee " + Mathf.RoundToInt(knees * 100f) + " cm right of the right one)");
                    if (feet > 0.02f) _faults.Add(at + ": feet crossed (left foot " + Mathf.RoundToInt(feet * 100f) + " cm right of the right one)");
                }
            }

            private static Transform RavenHead(GameObject m)
            {
                Transform neck = null;
                foreach (Transform t in m.GetComponentsInChildren<Transform>())
                {
                    string n = t.name.ToLowerInvariant();
                    if (n == "head") return t;
                    if (neck == null && n == "neck") neck = t;
                }
                return neck;
            }

            private static float DistToSegment(Vector3 x, Vector3 a, Vector3 b)
            {
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(x - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                return Vector3.Distance(x, a + ab * t);
            }

            internal string Report(bool play)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("per frame: body pitch/roll from upright (+ forward / + left side down), then each bone's aim in the body = up/aside (up −90 down .. 90 up; aside 0 ahead, 90 your right, −90 left, 180 back), what plays on each layer (weight, moment):\n");
                sb.Append(_frames);
                sb.Append(_faults.Count == 0 ? "faults: none (hands clear of the body and head, feet over the ground, legs not crossed)\n" : "faults:\n");
                for (int i = 0; i < _faults.Count && i < 30; i++) sb.Append("  ").Append(_faults[i]).Append('\n');
                if (_faults.Count > 30) sb.Append("  … ").Append(_faults.Count - 30).Append(" more\n");
                List<KeyValuePair<float, string>> jumps = new List<KeyValuePair<float, string>>();
                for (int f = 1; f < _rot.Count; f++)
                {
                    float dt = Mathf.Max(0.001f, _times[f] - _times[f - 1]);
                    for (int i = 0; i < _all.Count; i++)
                    {
                        float ang = Quaternion.Angle(_rot[f - 1][i], _rot[f][i]);
                        if (ang < 1f) continue;
                        float s = play ? ang / dt : ang;
                        jumps.Add(new KeyValuePair<float, string>(s, _all[i].name + " " + _times[f - 1].ToString("0.00", CultureInfo.InvariantCulture) + "→" + _times[f].ToString("0.00", CultureInfo.InvariantCulture)
                            + ": " + ang.ToString("0", CultureInfo.InvariantCulture) + "°" + (play ? " (" + s.ToString("0", CultureInfo.InvariantCulture) + "°/s)" : "")));
                    }
                }
                jumps.Sort((x, y) => y.Key.CompareTo(x.Key));
                sb.Append(play ? "fastest bone turns (a jerk stands out from the rest):\n" : "biggest changes between moments:\n");
                for (int i = 0; i < jumps.Count && i < 12; i++) sb.Append("  ").Append(jumps[i].Value).Append('\n');
                if (jumps.Count == 0) sb.Append("  none\n");
                return sb.ToString();
            }
        }
    }
}
#endif
