#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The people's parts put together (npc-plan.md, «ядро + модулі»): the one place that says which modules there are and
    // what each hangs on the core. The core — NpcActor (who it is, its net, its acts), NpcMind (its layers of plans),
    // NpcBody, NpcKit — runs what is registered here. It still names some modules itself (NpcResident, NpcWorker, NpcGear…
    // in NpcMind and NpcActor): debt, not a pattern — anything new hangs here. A whim: NpcMind.TakeWhim, not its own way onto
    // the layer. A part that throws is told in the log and left alone a while on that person (NpcActor.Run).
    //   NpcMind.Tasks    a module's own task on a layer: a step of it, and what is put right when it is left off
    //   NpcActor.Acts    a module's act, run on the game that runs the person (a talk's answer, a menu, another game)
    //   NpcActor.Unhurt  a module's word that no blow reaches it now (down, on its way, in need…)
    //   NpcActor.Hurtable a module's word that it can be hurt (hired, living at a base): else, not mortal by its
    //                    template, nothing reaches it
    //   NpcActor.BeforeDressed / Ticks / Everywhere  its life: steps before it is dressed, then where it is run, everywhere
    //   NpcActor.HoverInstead / HoverMarks / HoverLines / UseInstead  pointing at it, E
    //   NpcMind.Watches / LookOuts / FleeScales  its fighting: where it keeps watch, how far it looks out, how soon it runs
    //   Npcs.Commands    a module's console words for one person («raven npc <word> <who> …»)
    //   NpcVoice.OnBack  a module's answer to you coming back after long (a wave, a nod: NpcLife)
    //   NpcDefs.Words    a module's own template lines, known to the templates' check (NpcRaven's raven…)
    // A new module: its own file, its lines below (its menu buttons: NpcMenu.All; its parts on the body: Npcs.Human).
    internal static class NpcModules
    {
        private static bool _wired;

        internal static void Wire()
        {
            if (_wired) return;
            _wired = true;
            Lang.Changed += NpcPhrases.Reload; // (its words in the language now)
            NpcTalk.Register(); // (the person's conditions — npcflag, npcremembers, npcattitude… — known before any story list is read)

            // ---- tasks of the mind
            NpcMind.Tasks[NpcMind.Task.Eat] = new NpcMind.TaskOf { Step = NpcMeals.Step, Left = (m, p) => { NpcEat.Set(m.Actor, ""); GameObject go = NpcKit.Find(p.Who); NpcLid.Close(go != null ? go.GetComponentInChildren<Container>() : null); } }; // (cut short by the pantry: its lid down)
            NpcMind.Tasks[NpcMind.Task.Travel] = new NpcMind.TaskOf { Step = NpcTravel.Step, Left = (m, p) => NpcTravel.Mark(m.Actor, false), Unkept = m => NpcTravel.Mark(m.Actor, false) }; // (its way left off, or not kept: seen and hurt again)
            NpcMind.Tasks[NpcMind.Task.Chore] = new NpcMind.TaskOf { Step = NpcChores.Step, Left = NpcChores.Left }; // (its hammer away, what it carried put down)

            // ---- acts
            NpcMenu.Wire(); NpcResident.Deeds(); NpcWorker.Deeds(); NpcTidy.Deeds(); NpcKitchen.Deeds(); // (a menu's deed done where the person is run: NpcMenu.Deeds)
            NpcActor.Acts["hire"] = NpcTalk.Hire;
            NpcActor.Acts["unhire"] = (a, w) => NpcTalk.Unhire(a);
            NpcActor.Acts["eat"] = (a, w) => { float fill; if (a.Hunger != null && w.Length > 1 && float.TryParse(w[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fill)) a.Hunger.Eat(Mathf.Clamp(fill, 0f, 90f), w.Length > 2 ? string.Join(" ", w, 2, w.Length - 2) : ""); else NpcActor.NotDone(); }; // (food from your hands in a talk: NpcTalk givefood)
            NpcGait.Moods.Add((a, into) => { if (NpcLevers.Limping(a)) NpcGait.Wish(into, "hurt", 1f); }); // a lever «limp <hours>»: it limps that long (gaits.txt mood_hurt), every game from its ZDO
            NpcActor.Acts["lever"] = (a, w) => { string[] rest = new string[w.Length - 1]; System.Array.Copy(w, 1, rest, 0, rest.Length); Npcs.Log(a.Id + ": " + NpcLevers.Do(a, rest)); };
            NpcActor.Acts["revive"] = (a, w) => { long by = 0L; if (w.Length > 1) long.TryParse(w[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out by); if (a.Vitals != null) a.Vitals.Revive(by); }; // (a healing mead given)
            NpcActor.Acts["dosed"] = (a, w) => { if (a.Vitals != null) a.Vitals.Dosed(); }; // (a healing mead from your hands in a talk, awake: NpcTalk healmead)
            NpcActor.Acts["letrest"] = (a, w) => NpcToil.LetRest(a);
            NpcActor.Acts["stay"] = (a, w) => NpcToil.Stay(a); // (it meant to leave, a talk talked it round: NpcTalkSway)
            NpcActor.Acts["walkwith"] = NpcTalk.WalkWith; // (walks with you a few minutes: a talk's «Ходімо, покажу…») // (let off its work in a talk: «Іди відпочинь, я прикрию»)
            NpcActor.Acts["requestdone"] = (a, w) => { if (w.Length > 1) NpcRequests.Done(a, w[1]); }; // (its request brought)
            NpcActor.Acts["rescuemode"] = (a, w) => NpcRescueSite.Mode(a, w.Length > 1 ? string.Join(" ", w, 1, w.Length - 1) : ""); // (wait / hide there / with you)
            NpcActor.Acts["rescuetime"] = (a, w) => { int st; long until; if (w.Length > 2 && int.TryParse(w[1], out st) && long.TryParse(w[2], out until)) NpcRescue.Keep(a.Zdo, st, until); }; // (the server's word on its time)
            NpcActor.Acts["goaway"] = (a, w) => NpcRescueHits.GoAway(a, Player.GetClosestPlayer(a.transform.position, 12f)); // (saved, not wanted)
            NpcActor.Acts["remember"] = NpcMemory.Act;                                                // a thing to keep in mind (NpcMemory.Tell, a talk's «!remember»)
            NpcActor.Acts["forget"] = NpcMemory.Act;
            NpcActor.Acts["attitude"] = NpcAttitude.Act;                                              // warmer or colder to a player (NpcAttitude.Tell, a talk's «!attitude»)

            // ---- its words: what a module adds to them
            NpcPhrases.ValueSources.Add(NpcMemory.Values);                                            // {m_<kind>} {ago_<kind>} {n_<kind>}: what it remembers
            NpcVoice.Sources.Add(NpcMemory.Themes);                                                   // now and then a word of it (mem_<kind>)
            NpcPhrases.Variants.Add(NpcAttitude.Variant);                                             // how it feels about you: see_player_cold, about_you_close…
            NpcVoice.Sources.Add(NpcNews.Themes);                                                     // a raid round its base now (raid_now)
            NpcPhrases.ValueSources.Add(NpcVoice.YouValues);                                          // {trophy} {its_make}: what it sees on you (NpcVoice: you_*)
            NpcVoice.Sources.Add(NpcTalk.AskThemes);                                                  // it asks about you and recalls your answers (ask_you_*, said_*)
            NpcVoice.Sources.Add(NpcVoice.YouMade);                                                   // a thing of its own making on you (you_its_make)

            NpcMind.FightOver.Add(NpcMemory.FightEnded);                                              // a fight at home remembered
            NpcMind.PoseReached = a => NpcChatter.Mutter(a, a.Mind != null && a.Mind.Current != null && a.Mind.Current.Pose == "rest" ? "nap" : "wash"); // a word as it washes, or lies down for a nap

            // ---- who blows reach
            NpcActor.Unhurt.Add(a => NpcVitals.IsDown(a));                                          // senseless (every game, from its ZDO)
            NpcActor.Unhurt.Add(a => a.Zdo != null && a.Zdo.GetBool(NpcTravel.KTravel, false));      // on its way
            NpcActor.Unhurt.Add(a => NpcTalk.Hired(a) && !NpcResident.Is(a));                        // hired, not living anywhere yet: on its way to a heart
            NpcActor.Unhurt.Add(a => a.Zdo != null && a.Zdo.GetString(NpcRescue.KKind, "").Length > 0 && NpcLevers.Flag(a, "inneed")); // in need: its blows are counted, not its health (NpcRescueHits)
            NpcActor.Unhurt.Add(a => a.Def != null && NpcVitals.UnseenBy(a.Def) && !a.Def.Mortal);                // a story person «unseen» and not «mortal»: nothing hurts it, a camp it lives at or not (Heid burned in her own fire, 2026-11-04)
            NpcActor.Hurtable.Add(NpcTalk.Hired);
            NpcActor.Hurtable.Add(NpcResident.Is);

            // ---- its life: before it is dressed (in order), where it is run, on every game
            NpcActor.BeforeDressed.Add(NpcLocker.Migrate);                                            // an old save's gear and carry into its locker, before anything reads them
            NpcActor.BeforeDressed.Add(a => { if (a.Def.Generated && a.Zdo.GetString(NpcSettlers.KLook, "").Length == 0) { NpcSettlers.Make(a); a.ApplyDef(); } else if (a.Def.Generated) NpcSettlers.KeepName(a); return true; }); // a settler: made once; one made before a story person took its name gets another
            NpcActor.BeforeDressed.Add(a => { if (a.Def.Generated) NpcBackstory.Ensure(a); return true; }); // its story, once its look is made
            NpcActor.Ticks.Add(NpcWalkTest.Tick);                                                     // a walk test going on: its next place (raven npc walk test)
            NpcFailTest.Wire();                                                                       // a failure on purpose (raven npc fail)
            Npcs.Commands["fail"] = (p, who, a, aim) => { return NpcFailTest.Cmd(who, a); };
            Npcs.Commands["voice"] = (p, who, a, aim) => { return NpcSpeech.Report(who); };          // its voice: timbre, its chances, its last sounds and why one was not heard
            Npcs.Commands["talk"] = (p, who, a, aim) => { return NpcTalk.Walk(who, p); };             // its talk walked through, no window (the log)
            Npcs.Commands["words"] = (p, who, a, aim) => { return NpcPhrases.Sample(who, p, a.Length > 3 ? Mathf.Clamp(Errands.I(a[3], 3), 1, 10) : 3); }; // every theme it speaks, a few lines each (the log)
            NpcActor.Ticks.Add(NpcNews.Tick);
            NpcActor.Ticks.Add(NpcGear.Carry);                                                         // a weapon carried its own way at its ease, standing and walking (NpcGear)                                                         // a boss fallen: news to every resident
            NpcActor.Ticks.Add(a => { if (a.NextRemark <= 0f) a.NextRemark = Time.time + Random.Range(30f, 90f); NpcVoice.Tick(a); }); // a word of its own now and then
            NpcActor.Everywhere.Add(a => NpcMap.Tick());                                              // its mark on the map (twice a second for all of them)
            NpcActor.Everywhere.Add(a => NpcNews.Lands());                                            // your first steps in a land: told to your people (your game, now and then)
            NpcActor.Everywhere.Add(a => NpcLid.Tick());                                              // a chest a person opened: shut when its moment is over (cut short too)
            NpcActor.Everywhere.Add(a => NpcNav.Tick());                                               // the way finder kept mapping each base with people in it (it lets an unasked tile go)
            NpcActor.Everywhere.Add(NpcGear.Stance);                                                   // a weapon at its ease: the game's calm stance, its own on its guard (NpcGear, NpcAlert)
            NpcActor.Everywhere.Add(NpcPoseWatch.Ensure);                                              // the black box's eye on its pose (the author's game only: NpcWalk.Watch)
            NpcActor.Everywhere.Add(NpcFade.Keep);                                                    // one only half there (template «echo:») or fading away: its see-through picture, on every game
            NpcActor.Acts["fade"] = NpcFade.Act;                                                      // …its fading away (a scene's «npc <who> fade», smooth by default: Fades), where it is run
            NpcActor.UseInstead.Add((a, p) => a.Def != null && a.Def.Echo > 0f ? true : (bool?)null);              // an echo is not talked to (E does nothing)
            NpcActor.HoverInstead.Add(a => a.Def != null && a.Def.Echo > 0f ? a.Name : null);                      // …nor offers anything: its name only
            NpcForeignCamera.Wire();                                                                  // another mod's camera right by a person (PoV's picture behind its doll): the person not drawn in it
            NpcNature.Wire();                                                                         // a settler's kind of person: its words, its condition, how far a bully looks
            NpcPlaces.Wire();                                                                          // where it goes of its own accord, and why (NpcPlaces; raven npc why)
            NpcFeel.Wire();                                                                           // what it feels now: its body, its eyes, its words
            NpcStance.Wire();                                                                         // how it stands and sits at its ease: poses of its own (NpcStance)
            NpcActor.Unloaded.Add(NpcRequests.Forget);                                                // its request's next look: of this game only
            NpcGoals.Wire();                                                                          // its long wish: its words, its day, its mood (NpcGoals; raven npc goal)
            NpcActor.Ticks.Add(NpcLife.Tick);                                                         // its day answers what happens round it: rain, a raid, the morning (NpcLife)
            NpcPatience.Wire();                                                                       // its patience to each player: the talk's bar, anger, what you spent (NpcPatience; raven npc patience)
            NpcFavours.Wire();                                                                        // what you may ask of it when its patience is high (NpcFavours)
            NpcPromises.Wire();                                                                       // what you promised it: kept, or not
            NpcArcs.Wire();                                                                           // small lines of a settler's life
            NpcTasks.Wire();                                                                          // its small task for the one standing near (NpcTasks; raven npc task)
            NpcThreads.Wire();                                                                        // the threads between it and a player, and what they open (NpcThreads; raven npc thread)
            NpcAway.Wire();                                                                           // what happened while you were away: its absence kept, its answer (NpcAway)
            NpcWake.Wire();                                                                           // one asleep is not talked to: Q wakes it, cross (NpcWake)
            NpcActor.Acts["quarrel"] = NpcAttitude.QuarrelAct;                                        // one of its people did it wrong: colder to that one (NpcAttitude.Quarrel)
            NpcActor.Ticks.Add(NpcAttitude.Tick);                                                     // …a lazy neighbour idling near it at its work
            NpcActor.Unloaded.Add(NpcAttitude.Forget);
            NpcPhrases.ValueSources.Add(NpcAttitude.Values);                                          // {friend} {foe} {feud_a} {feud_b}: the truth of its people
            NpcVoice.Sources.Add(NpcAttitude.Themes);                                                 // two of its people at odds (feud)
            NpcTalk.Deeds["makepeace"] = NpcAttitude.MakePeace;                                       // «Помиріться вже» ({peace} its answer)
            NpcTalk.Words["{peace}"] = (a, p) => NpcAttitude.PeaceSaid;
            NpcNeighbours.Wire();                                                                     // what living together does by how they get on: mood, work, seats, raids, ultimatums, feasts (NpcNeighbours; raven npc neighbours)
            NpcActor.Unloaded.Add(NpcLife.Forget);
            NpcVoice.OnBack.Add(NpcLife.Welcome);                                                     // you back after long: it turns to you, waves or nods
            NpcActor.Unloaded.Add(NpcMap.Forget);                                                      // …and off it when it goes
            NpcActor.Removed.Add(a => { if (a.Worker != null) a.Worker.DropCarry(); });               // what it carried stays in the world
            NpcActor.Removed.Add(a => { if (a.Gear != null) a.Gear.DropAll(); });                     // what it wore at its feet
            NpcActor.Removed.Add(a => NpcResident.SetBed(a.Zdo, ZDOID.None));                         // its bed nobody's again (before its locker goes: the link is there)
            NpcActor.Removed.Add(NpcLocker.Remove);                                                   // its locker with it
#if RAVEN_STORY
            StoryModules.People();                                                                     // a word of the story to a person (StoryHold)
            NpcTool.Wire();                                                                           // a person's own thing, in its hand or on its belt (template «tool:»): its look everywhere, its choice where it is run
            NpcActor.Ticks.Add(NpcCry.Tick);                                                          // what a person calls out in a fight («cry:» lines)
            NpcActor.Unloaded.Add(NpcCry.Forget);
            NpcRaven.Wire();                                                                          // a person's raven (template «raven:»): its picture everywhere, its mind where it is run
            foreach (string w in NpcRaven.Words) NpcDefs.Words.Add(w);                               // its template lines (raven, raven_look…): known to the templates' check (they are read later, lazily)
#endif
            NpcMoves.Wire();                                                                          // a person's own moves and the ones it never makes (template «moves:», «never:», «poses:»): known to the templates' check
            NpcActor.HeardFrom.Add(a => a.Zdo != null && a.Zdo.GetString(NpcRescue.KKind, "") == "astray" ? 90f : 0f); // one lost and calling out: seen from far

            // ---- pointing at it, E
            NpcActor.HoverInstead.Add(a => NpcRescueHits.Died(a) ? a.Name + " " + a.Form(Lang.T("(мертвий|мертва)")) : null); // nobody came in time
            NpcActor.HoverInstead.Add(a => NpcVitals.IsDown(a) ? a.Name + " " + Lang.T("(без тями)") + "\n[<color=yellow><b>$KEY_Use</b></color>] " + (NpcVitals.Mead(Player.m_localPlayer) != null ? Lang.T("Дати медовуху зцілення") : Lang.T("Потрібна медовуха зцілення")) : null);
            NpcActor.HoverMarks.Add(a => NpcRequests.Of(a) != null ? " <color=#ffd15c><b>!</b></color>" : "");   // it asks for something
            NpcActor.HoverLines.Add(Guard.Line);                                                       // a guard's health and armour
            NpcActor.HoverLines.Add(NpcRescueHits.Line);                                               // one in need: blows it still bears
            NpcActor.UseInstead.Add((a, p) => NpcRescueHits.Died(a) ? (bool?)false : null);           // too late: no mead brings it back
            NpcActor.UseInstead.Add((a, p) => NpcVitals.IsDown(a) ? (bool?)(p != null && p == Player.m_localPlayer && NpcVitals.Give(p, a)) : null); // senseless: a healing mead, no talk

            // ---- its fighting
            NpcMind.Watches.Add(Guard.Watch);                                                          // a guard with a post: by its post
            NpcMind.Watches.Add((a, at) => NpcResident.Is(a) ? (bool?)(NpcBase.For(NpcRules.Home(a)).Inside(at) || Utils.DistanceXZ(at, a.transform.position) < 8f) : null); // a resident: its base, and what comes at it
            NpcMind.LookOuts.Add(Guard.LookOut);                                                       // a guard on its watch looks out as far as it sees
            NpcMind.FleeScales.Add(a => NpcBackstory.Fact(a, "ховається") ? 1.3f : 1f);                 // one who says it hides well: runs sooner

            // ---- console words for one person (raven npc <word> <who> …)
            Npcs.Commands["settle"] = (p, who, a, aim) => { return NpcResident.Settle(who, a.Length > 3 && a[3] == "here" ? p.transform.position : who.transform.position, false); };
            Npcs.Commands["unsettle"] = (p, who, a, aim) => { return NpcResident.Unsettle(who); };
            Npcs.Commands["job"] = (p, who, a, aim) => { return NpcWorker.SetJob(who, a.Length > 3 ? a[3] : ""); };
            Npcs.Commands["chest"] = (p, who, a, aim) => { return NpcWorker.SetChest(who, aim); };
            Npcs.Commands["bed"] = (p, who, a, aim) => { return NpcResident.GiveBed(who, aim); };
            Npcs.Commands["base"] = (p, who, a, aim) => { if (a.Length > 3 && a[3] == "heart") return NpcResident.HeartReport(who); NpcResident r = who.GetComponent<NpcResident>(); if (r == null || !NpcResident.Is(who)) return "Ніде не живе (raven npc settle <хто>)."; if (a.Length > 3 && a[3] == "show") r.Base.Show(30f); if (a.Length > 3 && a[3] == "at") return r.Base.Explain(aim); return r.Describe(); };
            Npcs.Commands["whim"] = (p, who, a, aim) => { NpcResident r = who.GetComponent<NpcResident>(); return r != null && NpcResident.Is(who) ? NpcWhims.Force(r, a.Length > 3 ? a[3] : "") : "Примхи — у мешканців."; };
            Npcs.Commands["lever"] = (p, who, a, aim) => { if (a.Length < 4) return NpcLevers.Usage; string[] rest = new string[a.Length - 3]; System.Array.Copy(a, 3, rest, 0, rest.Length); who.Act("lever " + string.Join(" ", rest)); return "Сказано: " + string.Join(" ", rest) + " (у журналі — що вийшло)."; };
            Npcs.Commands["vars"] = (p, who, a, aim) => { return NpcLevers.Describe(who); };
            Npcs.Commands["memory"] = (p, who, a, aim) => { return NpcMemory.Describe(who); };
            Npcs.Commands["news"] = (p, who, a, aim) => { if (a.Length < 4) return "raven npc news <хто> <подія> [що] — йому й усім з його оселі (напр. boss_moder)"; NpcNews.Tell(who, a[3], a.Length > 4 ? string.Join(" ", a, 4, a.Length - 4) : ""); return "Новину сказано."; };
            Npcs.Commands["attitude"] = (p, who, a, aim) => { float by; if (a.Length > 3 && float.TryParse(a[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out by)) { NpcAttitude.Tell(who, p.GetPlayerID(), by); return "Сказано: " + a[3] + " до тебе."; } return NpcAttitude.Describe(who); };
            Npcs.Commands["remember"] = (p, who, a, aim) => { if (a.Length < 4) return "raven npc remember <хто> <подія> [що] — про тебе"; NpcMemory.Tell(who, a[3], p.GetPlayerID(), a.Length > 4 ? string.Join(" ", a, 4, a.Length - 4) : ""); return "Сказано (у журналі — що запам'ятав)."; };
            Npcs.Commands["quarrel"] = (p, who, a, aim) => { if (a.Length < 4) return "raven npc quarrel <хто> <на кого> [food|place|work|table|raid|pay|snore|chest|brawl] — справжня сварка (тест)"; foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(who.Zdo, NpcResident.KHeart))) if (o != null && o != who && (o.Name == a[3] || o.Key == a[3])) { NpcAttitude.Quarrel(who, o, a.Length > 4 ? a[4] : "work"); return "Сказано (раз на день на пару; у журналі)."; } return "У цій оселі нема: " + a[3]; };
            Npcs.Commands["forget"] = (p, who, a, aim) => { if (a.Length < 4) return "raven npc forget <хто> <подія> — забуває її про всіх (тест)"; who.Act("forget " + a[3]); return "Сказано (у журналі — що забув)."; };
            Npcs.Commands["request"] = (p, who, a, aim) => { return NpcRequests.Force(who); };
            Npcs.Commands["chat"] = (p, who, a, aim) => { NpcResident r = who.GetComponent<NpcResident>(); return r != null && NpcResident.Is(who) ? NpcChatter.Force(r) : "Розмовляють лише мешканці."; };
            Npcs.Commands["nav"] = (p, who, a, aim) => { string t = NpcNav.Describe(who); Npcs.Log(t); return t; }; // (a test: what the way finder sees round it)
            Npcs.Commands["gait"] = (p, who, a, aim) => { return NpcGait.Force(who, a.Length > 3 ? a[3] : ""); }; // (a test: its kind of gait on this game, till the next reload)
            Npcs.Commands["toil"] = (p, who, a, aim) => { NpcToil t = who.GetComponent<NpcToil>(); return t != null ? t.Describe() : "Ні."; };
            Npcs.Commands["pay"] = (p, who, a, aim) => { NpcToil t = who.GetComponent<NpcToil>(); return t != null ? t.ForcePay() : "Ні."; };
            Npcs.Commands["gear"] = (p, who, a, aim) => { NpcGear g = who.Gear; List<string> cells = new List<string>(); if (g != null && g.Cells != null) foreach (ItemDrop.ItemData it in g.Cells.GetAllItems()) cells.Add(it.m_shared.m_name + (it.m_stack > 1 ? " x" + it.m_stack : "")); return "На ньому (* — вдягнуте): " + who.Wearing() + ". У комірках: " + (cells.Count > 0 ? string.Join(", ", cells.ToArray()) : "порожньо") + "."; };
            Npcs.Commands["hall"] = (p, who, a, aim) => { who.Order("travel " + Npcs.V(NpcTravel.Refuge) + " " + NpcTravel.ToRefuge); return "Іде до Дому воронів (поки — до початку світу)."; };
            Npcs.Commands["hunger"] = (p, who, a, aim) => { if (!who.Owned || who.Hunger == null) return NpcKit.Busy(who) ?? "Ні."; float f; if (a.Length > 3 && float.TryParse(a[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) who.Hunger.SetFull(f); return "Ситість " + Mathf.RoundToInt(NpcHunger.Full(who)) + " (" + NpcHunger.Word(who) + ")."; };
        }
    }
}
#endif
