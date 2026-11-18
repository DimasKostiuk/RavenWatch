# Файли коду без свого рядка в таблицях: чий розділ

Не читай цілим: шукай назву свого файлу. Рядок починається з розділу, який треба прочитати перед правкою цього файлу. Новий файл у `src/` мусить бути названий у таблиці свого розділу або тут: правило `arch-file` у `tools/arch_check.js` зупиняє збірку.

## Ворон

- `raven.md` · тіло, політ, посадка, вигляд: `AppearMute.cs`, `BigRavenLeave.cs`, `BlackBox.cs`, `DropLook.cs`, `EyeGlow.cs`, `FlightTemplates.cs`, `Landings.cs`, `OddEye.cs`, `RavenAura.cs`, `ShoulderFit.cs`, `TakeoffKeys.cs`, `Wing.cs`, `WingMods.cs`
- `raven.md` · жердина й сидіння: `PerchBath.cs`, `PerchCrowd.cs`, `PerchFlight.cs`, `PerchMoments.cs`, `PerchPose.cs`, `PerchTurn.cs`, `PerchWaves.cs`, `SitHere.cs`
- `raven.md` · життя на плечі й удома: `ArmPerch.cs`, `ArmTricks.cs`, `Bath.cs`, `Combat.cs`, `DirtFx.cs`, `Feeder.cs`, `FightJudge.cs`, `Fireside.cs`, `FishFlight.cs`, `HomeMeal.cs`, `HomeStrike.cs`, `HuginChores.cs`, `Loot.cs`, `Loyal.cs`, `Mead.cs`, `MuninDeeds.cs`, `NewGear.cs`, `Pet.cs`, `PlayerReact.cs`, `RavenJournal.cs`, `Share.cs`, `ShipPlay.cs`, `WaterFx.cs`, `WorldBirds.cs`, `WorldNotice.cs`, `WorldReact.cs`
- `raven.md` · ворони й інші гравці: `BondLog.cs`, `BondMoments.cs`, `CrossBubble.cs`, `Gathering.cs`, `Gossip.cs`, `JointPlay.cs`, `PassTalk.cs`, `People.cs`, `PeopleMoments.cs`
- `raven.md` · дари, пасивні й парні: `AiTarget.cs`, `BloodFury.cs`, `Facets.cs`, `Fear.cs`, `Fury.cs`, `GiftTwists.cs`, `GiftUp.cs`, `Heal.cs`, `LastChance.cs`, `Leap.cs`, `PairFx.cs`, `PairGifts.cs`, `PerkFx.cs`, `RavenLight.cs`, `ScreamFx.cs`, `Tame.cs`, `WeakPassives.cs`, `WingsFx.cs`
- `raven.md` · камінь долі: `FateRite.cs`, `FateStoneLook.cs`
- `raven.md` · доручення Одіна: `ErrandFollow.cs`, `ErrandHold.cs`, `ErrandHow.cs`, `ErrandItems.cs`, `ErrandMap.cs`, `ErrandMods.cs`, `ErrandNet.cs`, `ErrandSpot.cs`, `ErrandTalk.cs`, `ErrandTrail.cs`, `Favour.cs`, `OdinCall.cs`, `Riddles.cs`, `RuneStars.cs`, `Trial.cs`
- `raven.md` · пошта: `Post.cs`, `PostBird.cs`, `PostParcel.cs`
- `raven.md` · руни, вигляд, речі ворона: `Elements.cs`, `FxMix.cs`, `GearDress.cs`, `LookDiag.cs`, `LookThings.cs`, `RavenGear.cs`
- `raven.md` · слова й голос (як писати: `docs/jobs/words-ravens.md`): `AboutTalk.cs`, `Defaults.cs`, `DefaultsEvents.cs`, `EmoteTalk.cs`, `Gutter.cs`, `LineMood.cs`, `RavenVoiceApi.cs`, `Style.cs`, `Tone.cs`, `Voice.cs`

## Світ, звук, інтерфейс

- `world.md` · гра, мережа, інші моди: `Body.cs`, `Group.cs`, `NearCast.cs`, `NetSize.cs`, `RavenMsg.cs`, `RavenVisitLog.cs`, `RawComfort.cs`, `SkinGuard.cs`, `Stats.cs`
- `audio.md` · музика бою: `FightMusic.cs`
- `ui.md` · картинки, книга, меню: `AbilityIcons.cs`, `ItemMarks.cs`, `BandFill.cs`, `BookArtBoard.cs`, `BookArtTalk.cs`, `BuildTab.cs`, `ContentWords.cs`, `GainNote.cs`, `IconRender.cs`

## Інструменти автора

- `tools.md` · вкладки тестового вікна: `ErrandPanel.cs`, `FlightPanel.cs`, `FlightRec.cs`, `FlightStage.cs`, `LandPanel.cs`, `PerchTurnStage.cs`, `PhotoPanel.cs`, `Photos.cs`, `PlayerAnimLog.cs`, `PlayerAnimPanel.cs`, `RunePanel.cs`, `SitPanel.cs`, `TakeoffPanel.cs`, `TemplatePanel.cs`, `WingPanel.cs`, `WsAssets.cs`
- `tools.md` · Студія: `StudioHero.cs`, `StudioNpc.cs`, `StudioPhoto.cs`, `StudioFalcon.cs`
- `tools.md` · вікно сюжету й місць: `StoryPanel.cs`, `StoryPanelActors.cs`, `StoryPanelBuild.cs`, `StoryPanelDirector.cs`, `StoryPanelFiles.cs`, `StoryPanelLook.cs`, `StoryPanelWardrobe.cs`, `StoryPanelScenes.cs`, `StoryPanelShots.cs`, `StoryPanelTrack.cs`, `LocPanel.cs`, `AuthorUi.cs`, `BlueprintFrame.cs`, `NpcPreview.cs`, `AuthorNoIntro.cs`
- `tools.md` · тести: `Dummy.cs`, `NpcFailTest.cs`, `NpcSelf.cs`, `NpcStress.cs`

## Люди

- `npc-core.md` · тіло, річ у руці, шлях, мапа: `HandLook.cs`, `HeadLook.cs`, `NpcCloth.cs`, `NpcFade.cs`, `NpcFall.cs`, `NpcMap.cs`, `NpcNav.cs`
- `npc-core.md` · звички й важелі сюжету: `NpcLevers.cs`, `NpcRules.cs`
- `npc-talk.md` · розмова: `NpcFireside.cs`, `NpcRescueTalk.cs`
- `npc-life.md` · оселя й побут: `NpcChores.cs`, `NpcHew.cs`, `NpcHouse.cs`, `NpcHunger.cs`, `NpcPrivate.cs`, `NpcTidy.cs`
- `npc-life.md` · поселенці, порятунок, новини: `NpcBackstory.cs`, `NpcKeepsake.cs`, `NpcNews.cs`, `NpcRescue.cs`, `NpcRescueHits.cs`, `NpcSettlers.cs`

## Сюжет

- `story.md` · квести й світ: `StoryFound.cs`, `StoryMap.cs`, `StoryOwn.cs`, `StoryPlaces.cs`, `StoryQuestsRead.cs`, `StorySpot.cs`, `StoryTrackConds.cs`, `StoryWorld.cs`
- `story.md` · сцена: `StoryAir.cs`, `StoryAside.cs`, `StoryWaves.cs`, `StoryHush.cs`, `StoryBeasts.cs`, `StoryScenesBird.cs`, `StoryScenesCamera.cs`, `StoryScenesCast.cs`, `StoryScenesKinds.cs`, `StoryScenesLane.cs`, `StoryScenesPlayer.cs`, `StoryScenesRead.cs`
