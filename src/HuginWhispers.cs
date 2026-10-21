using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    [BepInPlugin(Guid, Name, Version)]
    // Jotunn (when installed) shows the settings of the mods that name it in its in-game menu (Esc → Моди): only
    // named, never needed (the ravens fly on a plain game too)
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]
    // SeneaL UI (when installed) loads first, so its own «take all» can be patched (SeneaLTakeAllPatch)
    [BepInDependency("seneaL.valheim.ui", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "ko4erga.ravenwatch";
        public const string Name = "RavenWatch";
#if RAVEN_ITEM
        public const string Version = "2.4.0";
#else
        public const string Version = "1.1.8";
#endif

        internal const string KeyPrefix = "HuginWhispers_";
        internal const string QuipPrefix = "HuginWhispers_quip_";
        private const string NextVisitKey = "HuginWhispers.nextVisit";
        private const string RpcRequest = "HuginWhispers_Request";
        private const string RpcVisit = "HuginWhispers_Visit";
        private const string RpcReply = "HuginWhispers_Reply";
        private const string RpcKill = "HuginWhispers_Kill";
        private const string LastTagKey = "HuginWhispers.lastTag";

        internal static Plugin Instance;

        private ConfigEntry<bool> _visitsEnabled;
        private ConfigEntry<float> _minDays;
        private ConfigEntry<float> _maxDays;
        private ConfigEntry<int> _muninChance;
        private ConfigEntry<float> _unansweredMinutes;
        private ConfigEntry<KeyboardShortcut> _adminKey;
        private ConfigEntry<float> _lookBackMinutes;
        private ConfigEntry<bool> _reactiveEnabled;
        private ConfigEntry<float> _reactiveLookBack;
        private ConfigEntry<float> _reactiveCooldown;
        private ConfigEntry<float> _reactiveCooldownMax;
        private ConfigEntry<float> _busyCooldown;
        private ConfigEntry<int> _busyEvents;
        private float _quietCooldown = -1f; // this round's calm pause, rolled between min and max
        private ConfigEntry<int> _reactiveMinScore;
        private ConfigEntry<float> _reactiveDelay;
        private ConfigEntry<int> _reactiveMuninChance;
        private float QuipSeconds = 15f;
        private string _quipLine;
        private float _nextQuipCheck;
        private const float QuipLandSeconds = 180f; // a quick raven that cannot land by then would come too late
        private Raven.RavenText _pendingText;
        private float _quipEndsAt;
        private Raven _quipRaven;
        private ConfigEntry<bool> _sharedRavens;
        private float _lastReactive = -99999f;
        private float _lastVisit = -99999f;
        private ConfigEntry<float> _minGapMinutes;
        private string _askedTag;
        private static readonly System.Random Rng = new System.Random();

        private static readonly FieldInfo ShownTutorialsField = AccessTools.Field(typeof(Player), "m_shownTutorials");
        private static readonly FieldInfo TempTextsField = AccessTools.Field(typeof(Raven), "m_tempTexts");
        internal static readonly Func<Raven, Raven.RavenText> CurrentText = BuildCurrentTextGetter();

        private static Func<Raven, Raven.RavenText> BuildCurrentTextGetter()
        {
            System.Linq.Expressions.ParameterExpression r = System.Linq.Expressions.Expression.Parameter(typeof(Raven), "r");
            FieldInfo field = AccessTools.Field(typeof(Raven), "m_currentText");
            return System.Linq.Expressions.Expression.Lambda<Func<Raven, Raven.RavenText>>(System.Linq.Expressions.Expression.Field(r, field), r).Compile();
        }

        private string _phraseDir;
        // the phrase files of the language the mod speaks (Ukrainian: config/RavenWatch; others: …/lang/<Language>)
        internal string PhraseDir { get { return _phraseDir != null ? Lang.PhraseDir(_phraseDir) : null; } }
        private float _nextTick;
        private float _lastTickError = -99999f;
        private string _pendingKey;
        private float _pendingSince;
        private bool _pendingScheduled;
        private bool _pendingSpawned;

        // admin window
        private bool _windowOpen;
        private Rect _windowRect = new Rect(80, 80, 540, 700);
        private Vector2 _playerScroll;
        private long _targetUid;
        private string _targetName = "";
        private bool _sendMunin;
        private bool _sendQuick;
        private string _adminTitle = "";
        private string _adminText = "";
        private string _status = "";
        private string _preview = "";
        private Vector2 _previewScroll;

        private void Awake()
        {
            Instance = this;
            Lang.Logger = Log;
            Migrate();
            ConfigEntry<string> lang = Config.Bind("0. Language", "Language", "Auto", "Language of the mod: Auto (the game's language), or a language name as the game calls it (English, Ukrainian, Polish...). A language the mod has no words for speaks English. Translations: config/RavenWatch/lang/<Language>/.");
            Lang.Forced = lang.Value;
            lang.SettingChanged += (o, e) => { Lang.Forced = lang.Value; Lang.Reload(); };
            _visitsEnabled = Config.Bind("1. Visits", "Enabled", true, "Hugin and Munin visit you now and then with a phrase.");
            _minDays = Config.Bind("1. Visits", "Min days between visits", 3f, "Fewest in-game days between two visits.");
            _maxDays = Config.Bind("1. Visits", "Max days between visits", 5f, "Most in-game days between two visits.");
            _muninChance = Config.Bind("1. Visits", "Munin chance percent", 30, new ConfigDescription("Chance that Munin comes instead of Hugin.", new AcceptableValueRange<int>(0, 100)));
            _unansweredMinutes = Config.Bind("1. Visits", "Leaves after minutes", 5f, "If nobody talks to the raven, it gives up after this many real minutes.");
            if (Mathf.Approximately(_unansweredMinutes.Value, 10f)) _unansweredMinutes.Value = 5f; // the old default: move it to the new one
            _adminKey = Config.Bind("2. Admin", "Window key", new KeyboardShortcut(KeyCode.Insert), "Opens the admin window for sending a raven (admins only).");
            _lookBackMinutes = Hide("1. Visits", "Hugin looks back minutes", 10f, "Hugin comments on what you did in this many real minutes before he lands.");
            Diary.WindowSeconds = Mathf.Max(1f, _lookBackMinutes.Value) * 60f;
            _reactiveEnabled = Config.Bind("3. Reactive Hugin", "Enabled", true, "Besides the planned visits, Hugin comes on his own soon after something notable happens (a boss, a death, a narrow escape...).");
            _reactiveLookBack = Hide("3. Reactive Hugin", "Looks back minutes", 20f, "How many real minutes the reactive Hugin considers.");
            _reactiveCooldown = Config.Bind("3. Reactive Hugin", "Cooldown minutes", 30f, "Quick ravens on a calm day: the pause after one is random between this and the max below (real minutes).");
            _reactiveCooldownMax = Config.Bind("3. Reactive Hugin", "Cooldown minutes max", 40f, "Upper end of the calm pause between quick ravens.");
            _busyCooldown = Hide("3. Reactive Hugin", "Busy cooldown minutes", 10f, "When a lot happens, quick ravens may come this soon after the previous raven, but never sooner.");
            _busyEvents = Hide("3. Reactive Hugin", "Busy events", 4, new ConfigDescription("How many notable events (deaths, narrow escapes, a boss, a level, a new biome...) since the last quick raven count as a lot. Fewer events give a pause between the busy and the calm one.", new AcceptableValueRange<int>(2, 20)));
            _reactiveMinScore = Hide("3. Reactive Hugin", "Min importance", 60, new ConfigDescription("How notable an event must be: boss 100, several deaths 92, death 88, narrow escape 80, level 75, friendly fire 68, slaughter 65, prey escaped 62, fled 60, new biome 60. Fresher events count more.", new AcceptableValueRange<int>(1, 100)));
            _reactiveDelay = Hide("3. Reactive Hugin", "Delay seconds", 45f, "He comes only after the event is this old, so not in the middle of a fight.");
            RavenSpeech.Enabled = Config.Bind("3. Reactive Hugin", "Speech cards", true, "The ravens speak in cards of their own (blue frame Hugin, red Munin) over the raven, or under the hotbar with an arrow when your raven is out of view. Off: the game's small speech bubble.").Value;
            ConfigEntry<float> quipSecs = Hide("3. Reactive Hugin", "Talk seconds", 15f, "How long a quick raven's words stay over it before it flies off.");
            QuipSeconds = Mathf.Max(3f, quipSecs.Value);
            quipSecs.SettingChanged += (o, e) => QuipSeconds = Mathf.Max(3f, quipSecs.Value);
            _reactiveMuninChance = Config.Bind("3. Reactive Hugin", "Munin chance percent", 40, new ConfigDescription("Chance that Munin comes instead of Hugin to comment on an event. Either way the raven says one line straight away and flies off.", new AcceptableValueRange<int>(0, 100)));
            if (_reactiveMuninChance.Value == 30) _reactiveMuninChance.Value = 40; // 1.3: Hugin 60 / Munin 40 (the old default moves along)
            UpdateKeep();
            _minGapMinutes = Config.Bind("1. Visits", "Min minutes between any visits", 15f, "No raven (planned or reactive) comes sooner than this many real minutes after the previous one.");
            ConfigEntry<int> groupMax = Hide("3. Reactive Hugin", "Max per group", 2, new ConfigDescription("Server setting: at most this many players standing close together get a reactive Hugin within the group window (a party after the same fight).", new AcceptableValueRange<int>(1, 10)));
            ConfigEntry<float> groupRadius = Hide("3. Reactive Hugin", "Group radius meters", 50f, "Server setting: players within this distance count as one group.");
            ConfigEntry<float> groupWindow = Hide("3. Reactive Hugin", "Group window minutes", 10f, "Server setting: how long a reactive visit counts toward the group limit.");
            Group.MaxPerGroup = groupMax.Value; Group.Radius = groupRadius.Value; Group.WindowSeconds = Mathf.Max(1f, groupWindow.Value) * 60f;
            groupMax.SettingChanged += (o, e) => Group.MaxPerGroup = groupMax.Value;
            groupRadius.SettingChanged += (o, e) => Group.Radius = groupRadius.Value;
            groupWindow.SettingChanged += (o, e) => Group.WindowSeconds = Mathf.Max(1f, groupWindow.Value) * 60f;
            _sharedRavens = Config.Bind("4. Shared ravens", "Enabled", true, "Players nearby see a copy of the raven that landed next to someone, with its words above it.");
            ConfigEntry<bool> excl = Config.Bind("1. Visits", "Exclamation mark", true, "Your own raven shows a ! above its head until you talk to it.");
            Exclamation = excl.Value;
            excl.SettingChanged += (o, e) => { Exclamation = excl.Value; };
            Shared.Enabled = _sharedRavens.Value;
            ConfigEntry<bool> sharedPin = Config.Bind("4. Shared ravens", "Map pin", true, "A temporary map mark shows where a raven landed by another player, until it flies off.");
            DropOldSharedKeys();
            Shared.MapPin = sharedPin.Value;
            sharedPin.SettingChanged += (o, e) => { Shared.MapPin = sharedPin.Value; };
            _sharedRavens.SettingChanged += (o, e) => { Shared.Enabled = _sharedRavens.Value; if (!Shared.Enabled) Shared.Reset(); };
            _lookBackMinutes.SettingChanged += (o, e) => { Diary.WindowSeconds = Mathf.Max(1f, _lookBackMinutes.Value) * 60f; UpdateKeep(); };
            _reactiveLookBack.SettingChanged += (o, e) => UpdateKeep();
#if RAVEN_ITEM
            BindRavenItem();
#endif
            SaveMovedSettings();

            _phraseDir = Path.Combine(Paths.ConfigPath, "RavenWatch");
            // a locked or read-only config folder must not leave the mod without its patches
            try
            {
                Grammar.MigrateFiles(_phraseDir); // first: old lines turn into new ones, so a revision does not add them twice
                Grammar.MuninVoice(_phraseDir); // Munin's new voice: his old files deleted once, made again below
                Grammar.RemakeVoices(_phraseDir); // rev 63: Hugin's new voice and Munin's cleaned lines: both pattern files deleted once, made again below
                using (Lang.Phrase()) { Phrases.EnsureDefaults(PhraseDir); Composer.EnsureDefaults(PhraseDir); } // (their headers in the phrases' language)
            }
            catch (System.Exception e) { Logger.LogError("Phrase files: " + e.Message); }
            // another language: its own phrase files (made from its built-in lines when missing)
            Lang.Changed += () =>
            {
                try { using (Lang.Phrase()) { Phrases.EnsureDefaults(PhraseDir); Composer.EnsureDefaults(PhraseDir); } }
                catch (System.Exception e) { Logger.LogError("Phrase files (" + Lang.PhraseLanguage + "): " + e.Message); }
            };

            RavenVoice.Init(this);

            // one patch class at a time: a game update breaking one patch must not leave all the ones after it out
            Harmony harmony = new Harmony(Guid);
            foreach (System.Type t in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                try { harmony.CreateClassProcessor(t).Patch(); }
                catch (System.Exception e) { Logger.LogError("Patch " + t.FullName + " failed, the rest goes on: " + (e.InnerException ?? e).Message); }
            }
        }

#if RAVEN_ITEM
        // Hugin and Munin as items (1.2): read once at start, the items are built from these
        private void BindRavenItem()
        {
            const string sec = "5. Raven items";
            RavenItem.Enabled = Config.Bind(sec, "Enabled", true, "Haldor sells Hugin and Munin as items; right click puts one on its own shoulder (Hugin right, Munin left, the belt stays free), where it sits and have their own keys. Needs a restart. The server and every player need the same setting.").Value;
            Use(Config.Bind(sec, "Hugin price", 1500, "Coins Haldor asks for Hugin."), v => RavenItem.HuginPrice = v);
            Use(Config.Bind(sec, "Munin price", 1500, "Coins Haldor asks for Munin."), v => RavenItem.MuninPrice = v);
            Use(Config.Bind(sec, "Fate stone price", 1000, "Coins for the stone that rolls the worn raven's bonus and drawback again."), v => RavenItem.RerollPrice = v);
            Use(Config.Bind(sec, "Parchment price", 50, "Coins Haldor asks for one parchment: every letter a raven carries to another player takes one."), v => RavenItem.LetterPrice = v);
            HFloat(sec, "Post letter cost", 20f, "Strength a raven spends to carry a letter to another player.", v => Post.LetterCost = Mathf.Max(0f, v));
            HFloat(sec, "Post parcel cost", 30f, "Strength a raven spends to carry a parcel (one small thing) to another player.", v => Post.ParcelCost = Mathf.Max(0f, v));
            HFloat(sec, "Post parcel max weight", 2f, "Kilograms a parcel may weigh at most (coins: as many as fit in this weight).", v => Post.MaxWeight = Mathf.Max(0.1f, v));
            HFloat(sec, "Post letter trust level", 2f, "The trust level a raven carries a letter from (each raven its own).", v => Post.LetterLevel = Mathf.Clamp(Mathf.RoundToInt(v), 1, 7));
            HFloat(sec, "Post parcel trust level", 3f, "The trust level a raven carries a parcel from (each raven its own).", v => Post.ParcelLevel = Mathf.Clamp(Mathf.RoundToInt(v), 1, 7));
            HFloat(sec, "Post wait seconds", 20f, "How long a raven with the post waits by its receiver for a fight or an open window to end before it hands it over anyway.", v => Post.WaitMax = Mathf.Clamp(v, 0f, 120f));
            RavenKeys.Bind(Config);
            BindTrust();
            RavenSkills.Animations = Config.Bind(sec, "Ability gestures", true, "The hero makes a gesture on some abilities (fury: the boss power gesture, light: a toast, scream: a roar, scouting: pointing, wings: a challenge, healing: drinking). Everyone sees it.").Value;
            RavenRadial.Enabled = Config.Bind(sec, "Radial menu group", true, "A Ravens group in the game's radial menu (G) with the worn ravens' abilities.").Value;
            Use(Hide(sec, "Munin kill strength: strong creatures", Power.KillSpecial, "Strength Munin gets for killing these creatures (PrefabName:strength, comma-separated; ×1.5 for each star). Any other creature gives 1 (+1 a star)."), v => Power.KillSpecial = v);
            Use(Hide(sec, "Munin kill strength: bosses", Power.KillBosses, "Strength Munin gets for each boss (PrefabName:strength). A boss not on the list (another mod's) gives the next setting."), v => Power.KillBosses = v);
            HFloat(sec, "Munin kill strength: other bosses", 30f, "Strength for a boss not on the list above.", v => Power.OtherBoss = Mathf.Max(0f, v));
            HFloat(sec, "Hugin per Path of Valheiman level", 15f, "With Path of Valheiman installed: strength Hugin gets for each new character level (above the highest ever reached). 0 = off.", v => Power.PovLevelGain = v);
            HFloat(sec, "Hugin thought cost", 1f, "Strength (of 100) a thought takes.", v => Power.ThoughtCost = v);
            HFloat(sec, "Munin thought cost", 1f, "Strength Munin's thought takes.", v => Power.MuninThoughtCost = v);
            HFloat(sec, "New runes: Hugin strength", 20f, "Strength Hugin gets from a runestone text (or a Vegvisir boss) this character never read.", v => Power.NewStoneHugin = v);
            HFloat(sec, "New runes: Munin strength", 10f, "Strength Munin gets from the same: he remembers.", v => Power.NewStoneMunin = v);
            HFloat(sec, "Reread runes: Hugin strength", 5f, "Strength Hugin gets for reading a known runestone or Vegvisir again (the first few different stones of a game day).", v => Power.RereadHugin = v);
            HFloat(sec, "Reread runes: stones per day", 5f, "How many different known stones a game day pay for reading again.", v => Power.RereadPerDay = Mathf.RoundToInt(v));
            GainNote.On = Config.Bind(sec, "Show gains in corner", true, "A short line in the top-left corner when a raven gets strength or trust (gathered for a few seconds, one line per raven).").Value;
            HFloat(sec, "Hugin scout cost", 40f, "Strength a scouting flight takes.", v => Power.ScoutCost = v);
            HFloat(sec, "Munin scream cost", 60f, 70f, "Strength a scream takes.", v => Power.ScreamCost = v);
            HFloat(sec, "Hugin thought cooldown seconds", 5f, "Pause between two thoughts.", v => RavenSkills.ThoughtCooldown = v);
            HFloat(sec, "Thought assembled percent", 100f, 60f, "How often a raven's thought is a sentence put together from parts (hugin_patterns.txt, munin_patterns.txt) rather than a whole line, when the theme has both.", v => Patterns.Share = Mathf.Clamp01(v / 100f));
            HFloat(sec, "Munin thought cooldown seconds", 5f, "Pause between two of Munin's thoughts.", v => RavenSkills.MuninThoughtCooldown = v);
            HFloat(sec, "Hugin scout cooldown seconds", 300f, "Pause between two scouting flights.", v => RavenSkills.ScoutCooldown = v);
            HFloat(sec, "Hugin scout seconds", 30f, "How long the enemies stay marked on the map, from Hugin's return.", v => RavenSkills.ScoutSeconds = v);
            HFloat(sec, "Hugin scout away seconds", 12f, "How soon Hugin is back on your shoulder with his report (the marks stay on the map longer).", v => RavenSkills.ScoutAwaySeconds = v);
            HFloat(sec, "Hugin scout map radius", 300f, "Meters of map cleared around you, at trust level 1.", v => RavenSkills.ScoutRevealRadius = v);
            HFloat(sec, "Hugin scout map radius at top level", 450f, "And at the top level; the levels between in equal steps.", v => RavenSkills.ScoutRevealRadiusTop = v);
            HFloat(sec, "Hugin scout enemy radius", 100f, "Enemies this close get marked (with their trophy as the icon).", v => RavenSkills.ScoutEnemyRadius = v);
            HFloat(sec, "Hugin scout ore radius", 100f, "Ore deposits this close get marked on the map (copper, tin, silver veins, scrap piles, flametal...). 0 = off.", v => RavenSkills.ScoutOreRadius = v);
            HFloat(sec, "Hugin scout ore minutes", 5f, "How long the ore marks stay on the map, from Hugin's return.", v => RavenSkills.ScoutOreMinutes = v);
            HFloat(sec, "Hugin scout dungeon radius", 300f, "Ways under the ground (crypts, burial chambers, caves, infested mines...) this close get marked on the map, as long as the ore marks stay. Only what the game has loaded around you can be seen. 0 = off.", v => RavenSkills.ScoutDungeonRadius = v);
            HFloat(sec, "Hugin scout dungeon radius at top level", 450f, "Dungeons marked this far at the top level of trust; the levels between in equal steps.", v => RavenSkills.ScoutDungeonRadiusTop = v);
            HFloat(sec, "Munin scream cooldown minutes at level 1", 40f, 45f, "Pause between two screams at trust level 1.", v => RavenSkills.ScreamCooldown = v * 60f);
            HFloat(sec, "Munin scream cooldown minutes at top level", 25f, 30f, "And at the top level; the levels between in equal steps.", v => RavenSkills.ScreamCooldownTop = v * 60f);
            HFloat(sec, "Munin scream radius", 6f, "Enemies this close lose their balance.", v => RavenSkills.ScreamRadius = v);
            HFloat(sec, "Munin scream fear seconds", 3f, "How long the enemies it reaches run from you (not bosses). 0 = they only lose their balance.", v => Fear.Seconds = Mathf.Clamp(v, 0f, 10f));
            HFloat(sec, "Munin fury cost", 60f, 40f, "Strength Munin's fury takes.", v => Power.FuryCost = v);
            HFloat(sec, "Munin fury cooldown minutes at level 1", 45f, 40f, "Pause between two furies at trust level 1.", v => RavenSkills.FuryCooldown = v * 60f);
            HFloat(sec, "Munin fury cooldown minutes at top level", 25f, 20f, "And at the top level; the levels between in equal steps.", v => RavenSkills.FuryCooldownTop = v * 60f);
            HFloat(sec, "Munin fury seconds at level 1", 10f, "How long the fury lasts at trust level 1.", v => Fury.Seconds = v);
            HFloat(sec, "Munin fury seconds at top level", 20f, "And at the top level.", v => Fury.SecondsTop = v);
            string furyFx = Hide(sec, "Munin fury effect color", "#BDC2CC", "Color of the effect around you on Munin's fury, as #RRGGBB (raven grey; the boss power's own is red).").Value;
            Color fc;
            if (ColorUtility.TryParseHtmlString(furyFx, out fc)) Fury.FxColor = fc;
            HFloat(sec, "Munin fury attack speed percent", 35f, "How much faster you attack in fury.", v => Fury.Multiplier = 1f + Mathf.Max(0f, v) / 100f);
            Use(Config.Bind(sec, "Shoulder change wait", true, "A raven taken off the shoulder takes offence: no raven of that kind sits there again for a while, and one cannot be swapped for another. Each raven on its own; dying and loading do not count. raven ready ends it."), v => RavenSeat.Enabled = v);
            HFloat(sec, "Shoulder change wait minutes", 30f, "How long that lasts.", v => RavenSeat.Minutes = v);
            HFloat(sec, "Shoulder on/off seconds", 1f, "How long the hero takes to put a raven on the shoulder or take it off (the game's equipping move, as with gear). 0: at once.", v => RavenSeat.EquipSeconds = v);
            Float(sec, "Quiet seconds between own lines", 90f, "The ravens' own lines (reactions to events, talks with each other, fireside stories, asking for food) leave this much quiet after one has been read; an event may come after 20 s.", v => Director.Gap = Mathf.Max(0f, v));
            HFloat(sec, "Moment word chance", 50f, "A fall, a heavy load, a dungeon, a portal, a fish, a long go at trees or ore: the chance (%) the ravens say anything at all when it happens (each kind at most every 5–15 minutes).", v => RavenTalk.MomentChance = Mathf.Clamp01(v / 100f));
            Float(sec, "Emote words chance", 35f, "You start an emote (a dance, a wave, a bow, a cry...): the chance (%) a raven has a word about it a moment later. 0: never.", v => { EmoteTalk.Chance = Mathf.Clamp01(v / 100f); EmoteTalk.Enabled = v > 0f; });
            HFloat(sec, "Emote words pause minutes", 4f, "After a word about one kind of emote, the same kind gets no word for this long (any emote: at least 45 s apart).", v => EmoteTalk.KindPauseMinutes = Mathf.Max(0f, v));
            HFloat(sec, "Moment talk chance", 50f, "When they do: the chance (%) it is a talk of both (only with both on your shoulders); otherwise one raven says one line.", v => RavenTalk.TalkChance = Mathf.Clamp01(v / 100f));
            CrossTalk.Enabled = Config.Bind(sec, "Talks with other players ravens", true, "Your ravens talk with the ravens of other players near you: when you meet after a while (the first time they get acquainted), after a boss beaten together, by the same fire. Everyone near sees the talk between the players, and it goes to the chat.").Value;
            Float(sec, "Ravens touch the world", 100f, "How often your ravens fly by themselves to something near and sit on it a while: a sign to read out, a berry bush to peck at (in percent; 100: about once every 10-20 minutes when all is calm). 0: never.", v => WorldVisit.Mul = Mathf.Max(0f, v / 100f));
            SitOn.LetOthers = Config.Bind(sec, "Other players ravens may sit on me", true, "Other players may send their raven to sit on your head or a free shoulder (the orders circle, There). A roll shakes it off. Off: no raven of theirs sits on you.");
            HFloat(sec, "Seat on a head up", 0.19f, "Metres from the head bone of a player to where a raven sits on their head, along the head (it tilts and turns with it).", v => SitOn.HeadUp = v);
            HFloat(sec, "Seat on a head forward", 0.03f, "Metres forward (minus: back) along the head from that point.", v => SitOn.HeadFwd = v);
            HFloat(sec, "Words in passing chance", 30f, "You run past another player with ravens (within a few metres): the chance (%) one raven throws theirs a word (sometimes answered), over the heads and in the chat. Works with the talks with other players' ravens on. 0: never.", v => { PassTalk.Chance = Mathf.Clamp01(v / 100f); PassTalk.Enabled = v > 0f; });
            HFloat(sec, "Words in passing pause minutes", 10f, "The same two players: a word in passing at most this often, and not this soon after a talk of their ravens.", v => PassTalk.PairMinutes = Mathf.Max(0f, v));
            HFloat(sec, "Talks with other players ravens pause minutes", 6f, "At least this long between two such talks your ravens are in (after a boss: half).", v => CrossTalk.GapMinutes = Mathf.Max(1f, v));
            Float(sec, "Own lines per 10 minutes", 4f, "How many own lines both ravens may say in 10 minutes (a talk counts as one; events may go 2 over). What answers your own doing (gifts, thought, feeding) is never held back.", v => Director.PerTenMinutes = Mathf.Max(1, Mathf.RoundToInt(v)));
            SeneaLShoulders.Wanted = Config.Bind(sec, "SeneaL UI shoulder cells", true, "With SeneaL UI: two round shoulder cells on the head cell of its doll (Munin left, Hugin right), and a raven on the shoulder leaves the bag. Off, or without SeneaL UI: the ravens on the shoulders stay in the bag with the worn frame.").Value;
            HFloat(sec, "SeneaL UI shoulder cell size", 30f, 28f, "Size of those cells in pixels (SeneaL UI's cell is 64).", v => SeneaLShoulders.Size = Mathf.Clamp(v, 16f, 40f));
            HFloat(sec, "SeneaL UI shoulder cell out", -10f, 2f, "How far the centre of each cell lies past the head cell's side, pixels (negative: inside it).", v => SeneaLShoulders.OutX = Mathf.Clamp(v, -30f, 30f));
            HFloat(sec, "SeneaL UI shoulder cell down", 2f, 4f, "How far the centre lies below the head cell's bottom edge, pixels (negative: above it).", v => SeneaLShoulders.DownY = Mathf.Clamp(v, -30f, 30f));
            Use(Hide(sec, "Small passives", true, "Munin's anger and Hugin's light step: small passives every awake raven has from the first level of trust."), v => WeakPassive.Enabled = v);
            HFloat(sec, "Small passives cost", 10f, "Strength a small passive takes when it starts.", v => WeakPassive.Cost = v);
            HFloat(sec, "Small passives cooldown minutes", 30f, "How often a small passive can start at most.", v => WeakPassive.CooldownMinutes = v);
            HFloat(sec, "Small passives minutes", 5f, "How long a small passive lasts.", v => WeakPassive.Seconds = v * 60f);
            HFloat(sec, "Munin anger damage percent", 10f, "How much more damage you deal while Munin is angry.", v => WeakPassive.AngerPercent = v);
            HFloat(sec, "Hugin light step stamina percent", 10f, "How much faster stamina comes back during Hugin's light step.", v => WeakPassive.StepPercent = v);
            Use(Hide(sec, "Small passives from trust level", 2, "Hugin's light step and Munin's anger work from this level of trust on."), v => RavenAbilities.WeakUnlock = v);
            ConfigEntry<int> strong = Hide(sec, "Strong passives from trust level", 4, "Hugin's last chance and Munin's blood fury work from this level of trust on.");
            if (strong.Value == 3) strong.Value = 4; // the first default, before the small passives took level 2
            Use(strong, v => RavenAbilities.StrongUnlock = v);
            Use(Hide(sec, "Munin blood fury", true, "Munin's passive: when your health falls low, for a while you hit harder and faster."), v => BloodFury.Enabled = v);
            HFloat(sec, "Munin blood fury cost", 50f, 40f, "Strength the blood fury takes; with less it does not start.", v => BloodFury.Cost = v);
            HFloat(sec, "Munin blood fury cooldown minutes at first level", 30f, "How long before it can start again, at the trust level it opens.", v => BloodFury.CooldownMinutes = v);
            HFloat(sec, "Munin blood fury cooldown minutes at top level", 10f, "And at the top level; the levels between in equal steps.", v => BloodFury.CooldownMinutesTop = v);
            HFloat(sec, "Munin blood fury seconds", 15f, "How long it lasts.", v => BloodFury.Seconds = v);
            HFloat(sec, "Munin blood fury health percent", 30f, "It starts when your health falls below this share.", v => BloodFury.Threshold = Mathf.Clamp(v, 1f, 99f) / 100f);
            HFloat(sec, "Munin blood fury damage percent", 30f, "How much more damage you deal.", v => BloodFury.DamagePercent = v);
            HFloat(sec, "Munin blood fury attack speed percent", 20f, "How much faster you attack (multiplies with Fury).", v => BloodFury.SpeedPercent = v);
            Use(Hide(sec, "Ravens stay after death", true, "Hugin's «Loyalty» and Munin's «Stubbornness»: when you die, each raven on your shoulder may stay there instead of going into the grave."), v => Loyal.Enabled = v);
            HFloat(sec, "Munin death strength minutes", 10f, "Your death gives Munin strength at most once in this many minutes (0: every death).", v => Power.DeathGainMinutes = Mathf.Max(0f, v));
            HFloat(sec, "Ravens stay after death chance", 30f, "The chance (%) for each raven, rolled on its own.", v => Loyal.Chance = Mathf.Clamp01(v / 100f));
            Use(Hide(sec, "Ravens stay after death from trust level", 3, "That passive works from this level of trust on."), v => Loyal.UnlockLevel = Mathf.Clamp(v, 1, 7));
            Use(Hide(sec, "Hugin last chance", true, "Hugin's passive: a blow that would kill you leaves you 1 health and a moment when nothing hurts you."), v => LastChance.Enabled = v);
            HFloat(sec, "Hugin last chance cost", 50f, "Strength the last chance takes; with less it does not save you.", v => LastChance.Cost = v);
            HFloat(sec, "Hugin last chance cooldown minutes", 10f, "How long before Hugin can save you again.", v => LastChance.CooldownMinutes = v);
            HFloat(sec, "Hugin last chance safe seconds", 2f, "How long nothing hurts you after the save.", v => LastChance.SafeSeconds = v);
            HFloat(sec, "Hugin light cost", 20f, "Strength Hugin's light takes.", v => Power.LightCost = v);
            HFloat(sec, "Hugin light cooldown seconds", 300f, "Pause between two lights, counted from the start.", v => RavenSkills.LightCooldown = v);
            HFloat(sec, "Hugin light seconds", 300f, "How long the wisp shines over your head.", v => RavenLight.Seconds = v);
            HFloat(sec, "Hugin light radius percent", 125f, "How far the light reaches, against a torch (100 = as a torch). Takes effect on the next light.", v => RavenLight.RangeMultiplier = Mathf.Max(10f, v) / 100f);
            HFloat(sec, "Hugin light talk chance", 50f, "The chance (%) Hugin says something when you light the wisp.", v => RavenSkills.LightTalkChance = Mathf.Clamp01(v / 100f));
            RavenLight.GameSounds = Hide(sec, "Hugin light game sounds", true, "The sounds the wisp brings from the game (the torch's and the Wisplight ball's own). Off: the wisp is silent. Takes effect on the next light.").Value;
            HFloat(sec, "Hugin light sound volume", 70f, "How loud the wisp's sounds are (switch-on, hum, and the switch-on backwards when it goes out), against the Wisplight's own (100). Takes effect on the next light.", v => RavenLight.SoundVolume = Mathf.Clamp(v, 0f, 200f) / 100f);
            HFloat(sec, "Hugin light follow lag seconds", 0.6f, "How slowly the wisp catches up with its spot over your head when you move off (0: fixed to it, as before).", v => WispLife.FollowLag = Mathf.Clamp(v, 0f, 3f));
            HFloat(sec, "Hugin light runes per second", 1.2f, "How many runes drift off the wisp each second (0: none). Takes effect on the next light.", v => RavenLight.RunesPerSecond = Mathf.Clamp(v, 0f, 10f));
            HFloat(sec, "Hugin leap cost", 50f, 30f, "Strength Hugin's leap takes.", v => Power.LeapCost = v);
            HFloat(sec, "Hugin leap cooldown minutes at level 1", 30f, "Pause between two leaps at trust level 1, counted from the start.", v => RavenSkills.LeapCooldown = v * 60f);
            HFloat(sec, "Hugin leap cooldown minutes at top level", 24f, "And at the top level; the levels between in equal steps.", v => RavenSkills.LeapCooldownTop = v * 60f);
            HFloat(sec, "Hugin leap seconds at level 1", 7.5f, 5f, "How long every jump from the ground goes high and falls do not hurt, at trust level 1. Runs out in the air: the landing hurts as usual.", v => Leap.Seconds = v);
            HFloat(sec, "Hugin leap seconds at top level", 30f, 20f, "And at the top level (the leap opens at level 4: 19, 23, 26, 30 s by default).", v => Leap.SecondsTop = v);
            HFloat(sec, "Hugin shadow cost", 60f, "Strength Hugin's shadow takes.", v => Power.ShadowCost = v);
            HFloat(sec, "Hugin shadow cooldown minutes at level 1", 40f, "Pause between two shadows at trust level 1, counted from the start.", v => RavenSkills.ShadowCooldown = v * 60f);
            HFloat(sec, "Hugin shadow cooldown minutes at top level", 20f, "And at the top level; the levels between in equal steps.", v => RavenSkills.ShadowCooldownTop = v * 60f);
            HFloat(sec, "Hugin shadow seconds at level 1", 10f, "How long monsters and other players do not see you, at trust level 1. A swing, a shot or a cast ends it sooner.", v => Shadow.Seconds = v);
            HFloat(sec, "Hugin shadow seconds at top level", 20f, "And at the top level.", v => Shadow.SecondsTop = v);
            HFloat(sec, "Hugin dome cost", 60f, "Strength Hugin's dome («Under the Wing») takes.", v => Power.DomeCost = v);
            HFloat(sec, "Hugin dome cooldown minutes at level 1", 10f, "Pause between two domes at the trust level it opens on, counted from the start.", v => RavenSkills.DomeCooldown = v * 60f);
            HFloat(sec, "Hugin dome cooldown minutes at top level", 7f, "And at the top level; the levels between in equal steps.", v => RavenSkills.DomeCooldownTop = v * 60f);
            HFloat(sec, "Hugin dome seconds at level 1", 60f, "How long the dome stands at the lowest trust level.", v => Dome.Seconds = v);
            HFloat(sec, "Hugin dome seconds at top level", 90f, "And at the top level.", v => Dome.SecondsTop = v);
            HFloat(sec, "Hugin dome radius", 6f, "Meters from the centre to the edge of the dome.", v => Dome.Radius = Mathf.Clamp(v, 2f, 20f));
            Use(Hide(sec, "Hugin dome boss blows", 3, "How many boss blows (any boss, any damage) the dome takes; it breaks on the last one."), v => Dome.BossBlows = Mathf.Clamp(v, 1, 10));
            Use(Hide(sec, "Hugin dome trust level", 3, "The trust level Hugin's dome opens on."), v => Dome.UnlockLevel = Mathf.Clamp(v, 1, 7));
            Use(Hide(sec, "Gift unlock levels", "", "Trust levels the gifts open on, to change the built-in ones: id:level, comma-separated (thought, scout, hstrike, light, hdecoy, leap, shadow, hflight; mthought, mstrike, scream, wings, heal, fury, mberserk). Empty: as built in (Hugin 1 thought, scout, beak; 2 light; 3 dome, decoy; 4 leap; 5 shadow; 6 flight. Munin 1 thought, claws; 2 scream; 3 wings; 4 heal; 5 fury; 6 the beast)."), v => RavenAbilities.SetLevels(v));
            HFloat(sec, "Flight wing beat speed", 1f, "How fast the game's flying clip plays when a raven flies (the dome, a strike): 1 = as the game has it.", v => ShoulderLife.FlyClipSpeed = Mathf.Clamp(v, 0.2f, 3f));
            ShoulderLife.FlyClipName = Hide(sec, "Flight clip", "HuginFlyAway", "The raven's animation clip used in flight, by name (the log lists them: \"Raven animation clips\"). The game's raven has no looping flight, so the middle of its fly-away clip is played round and round.").Value ?? "";
            ShoulderLife.HoverVanilla = Hide(sec, "Hover uses game flight", true, "While you dance or lie down the ravens hang in the air above the shoulder: with the game's own wing beats (and no preening up there). Off: the mod's own beats.").Value;
            HFloat(sec, "Flight level percent", 85f, "The game's fly-away clip holds the raven nose-up; this much of that is taken out in flight (100 = flat, 0 = as the clip has it).", v => ShoulderLife.FlyLevel = Mathf.Clamp(v, 0f, 150f) / 100f);
            ShoulderLife.FlyAuto = Hide(sec, "Flight loop auto", true, "Find whole wing beats in the flight clip by itself and loop them (the percents below are used only when this is off or nothing is found).").Value;
            HFloat(sec, "Flight clip from percent", 25f, "Where in that clip the loop starts (percent of its length).", v => ShoulderLife.FlyFrom = Mathf.Clamp(v, 0f, 100f) / 100f);
            HFloat(sec, "Flight clip to percent", 80f, "And where it ends; the part between plays round and round.", v => ShoulderLife.FlyTo = Mathf.Clamp(v, 0f, 100f) / 100f);
            HFloat(sec, "Hugin strike cost", 25f, "Strength «Eye Peck» takes.", v => Strike.HuginCost = v);
            HFloat(sec, "Strike flight speed", 12f, "Meters a second a raven flies to its target (a real raven: 12-14).", v => Strike.Speed = Mathf.Clamp(v, 3f, 60f));
            HFloat(sec, "Strike return speed", 10f, "Meters a second on the way back to the shoulder.", v => Strike.ReturnSpeed = Mathf.Clamp(v, 3f, 60f));
            HFloat(sec, "Strike series pause seconds", 0.45f, "Between two blows of a series on the same target: up, and dive again.", v => Strike.SeriesGap = Mathf.Clamp(v, 0.1f, 3f));
            HFloat(sec, "Munin strike reach meters", 8f, "How far from the first target Munin goes on to two more.", v => Strike.MuninReach = Mathf.Clamp(v, 1f, 30f));
            HFloat(sec, "Munin strike cost", 35f, "Strength «Talons» takes.", v => Strike.MuninCost = v);
            HFloat(sec, "Hugin strike cooldown seconds", 45f, "Pause between two strikes of Hugin (trust shortens it, as for other gifts).", v => Strike.HuginCooldown = v);
            HFloat(sec, "Munin strike cooldown seconds", 60f, "Pause between two strikes of Munin.", v => Strike.MuninCooldown = v);
            HFloat(sec, "Strike range at training level 1", 30f, "Meters the raven flies to a target at the first training level.", v => Strike.Range = Mathf.Clamp(v, 5f, 200f));
            HFloat(sec, "Strike range at training level 20", 100f, "And at the top training level; the levels between in equal steps.", v => Strike.RangeTop = Mathf.Clamp(v, 5f, 200f));
            HFloat(sec, "Strike boss cap percent Hugin", 3f, "One flight of Hugin takes at most this share of a boss's health.", v => Strike.BossCapHugin = Mathf.Clamp(v, 0f, 100f) / 100f);
            HFloat(sec, "Strike boss cap percent Munin", 5f, "One flight of Munin takes at most this share of a boss's health.", v => Strike.BossCapMunin = Mathf.Clamp(v, 0f, 100f) / 100f);
            HFloat(sec, "Strike training bonus percent per level", 3f, "Each training level above the first adds this to the blow.", v => Strike.TrainPct = v);
            HFloat(sec, "Strike trust bonus percent per level", 5f, "Each trust level above the first adds this to the blow.", v => Strike.TrustPct = v);
            HFloat(sec, "Strike fed bonus percent", 20f, "A raven fed lately strikes this much harder.", v => Strike.FedPct = v);
            HFloat(sec, "Strike fed minutes", 10f, "How long after feeding the bonus lasts.", v => Strike.FedMinutes = v);
            HFloat(sec, "Strike crit bonus percent", 20f, 10f, "Points of crit chance added to the training's own (with the training and trust at the top: Hugin 70%, Munin 90%).", v => Strike.CritBonus = v);
            HFloat(sec, "Strike miss refund percent", 50f, "A strike that reached nobody gives back this share of its strength and lets off this share of its pause.", v => Strike.MissRefund = Mathf.Clamp(v, 0f, 100f) / 100f);
            HFloat(sec, "Strike crit percent per trust level", 5f, "Points of crit chance for each trust level above the first.", v => Strike.CritPerTrust = v);
            HFloat(sec, "Hugin shadow strike multiplier", 3f, "The first hit from the shadow deals this many times the damage (Path of Valheiman's sneak attack passives add on top).", v => Shadow.StrikeMultiplier = Mathf.Max(1f, v));
            Use(Hide(sec, "Hugin shadow strike staggers", true, "The first hit from the shadow also staggers the enemy."), v => Shadow.Stun = v);
            HFloat(sec, "Hugin shadow self opacity percent", 80f, 60f, "How solid you see your own dark shape in the shadow (others do not see you at all). 100 = not see-through.", v => Shadow.SelfOpacity = Mathf.Clamp(v, 5f, 100f) / 100f);
            Use(Hide(sec, "Hugin shadow fools bosses", false, "Bosses do not see you in the shadow either (and take the strike)."), v => Shadow.FoolsBosses = v);
            HFloat(sec, "Hugin leap height", 12.5f, 25f, "Meters a jump throws you up.", v => Leap.Height = v); // halved in 1.3 (the old 25 is moved to the new default)
            HFloat(sec, "Hugin leap forward meters", 30f, "Jumping on the run: about how far ahead you land (an arc up and forward). Standing still you go straight up. 0 = always straight up.", v => Leap.Forward = v);
            string wisp = Hide(sec, "Hugin light wisp color", "#FFEDB3", "Color of the wisp over your head, as #RRGGBB (the Wisplight's own is blue, about #6EC8FF). Takes effect on the next light.").Value;
            Color wc;
            if (ColorUtility.TryParseHtmlString(wisp, out wc)) RavenLight.WispColor = wc;
            RavenLight.WispOpacity = Mathf.Clamp01(Hide(sec, "Hugin light wisp opacity", 0.7f, "How solid the wisp looks: 1 = as the Wisplight, 0.7 = 30% see-through, 0 = invisible (the light stays). Takes effect on the next light.").Value);
            HFloat(sec, "Munin wings cost", 30f, "Strength Munin's wings take.", v => Power.WingsCost = v);
            HFloat(sec, "Munin wings cooldown minutes at level 1", 20f, "Pause between two wings at trust level 1.", v => RavenSkills.WingsCooldown = v * 60f);
            HFloat(sec, "Munin wings cooldown minutes at top level", 10f, "And at the top level.", v => RavenSkills.WingsCooldownTop = v * 60f);
            HFloat(sec, "Munin wings seconds at level 1", 20f, "How long you run faster at trust level 1.", v => Wings.Seconds = v);
            HFloat(sec, "Munin wings seconds at top level", 40f, "And at the top level.", v => Wings.SecondsTop = v);
            HFloat(sec, "Munin wings speed percent", 50f, "How much faster you move.", v => Wings.SpeedPercent = v);
            HFloat(sec, "Munin wings run stamina percent", 30f, "How much less stamina running costs while the wings carry you.", v => Wings.RunStaminaPercent = v);
            HFloat(sec, "Munin heal cost", 40f, "Strength Munin's healing takes.", v => Power.HealCost = v);
            HFloat(sec, "Munin heal cooldown minutes at level 1", 30f, "Pause between two healings at trust level 1.", v => RavenSkills.HealCooldown = v * 60f);
            HFloat(sec, "Munin heal cooldown minutes at top level", 10f, "And at the top level.", v => RavenSkills.HealCooldownTop = v * 60f);
            HFloat(sec, "Munin heal seconds", 10f, "How long the healing takes to bring you to full health, like a mead.", v => RavenHeal.Seconds = v);
            HFloat(sec, "Hugin decoy cost", 35f, "Strength Hugin's decoy («Обманка») takes.", v => Power.DecoyCost = v);
            HFloat(sec, "Hugin decoy seconds at level 1", 8f, "How long the enemy fights nobody but Hugin, at the lowest trust level the gift has.", v => Decoy.Seconds = Mathf.Clamp(v, 1f, 30f));
            HFloat(sec, "Hugin decoy seconds at top level", 20f, "And at the top level.", v => Decoy.SecondsTop = Mathf.Clamp(v, 1f, 30f));
            HFloat(sec, "Hugin decoy cooldown minutes at level 1", 10f, "Pause between two decoys at the lowest trust level the gift has.", v => Decoy.Cooldown = Mathf.Max(0.1f, v) * 60f);
            HFloat(sec, "Hugin decoy cooldown minutes at top level", 5f, "And at the top level.", v => Decoy.CooldownTop = Mathf.Max(0.1f, v) * 60f);
            HFloat(sec, "Hugin decoy range", 30f, "Meters to the enemy Hugin flies to.", v => Decoy.Range = Mathf.Clamp(v, 5f, 100f));
            Use(Hide(sec, "Hugin decoy hits to break", 3, "Blows from players on a lured enemy before it sees through it."), v => Decoy.HitsToBreak = Mathf.Clamp(v, 1, 20));
            HFloat(sec, "Hugin wings cost", 80f, "Strength Odin's wings (Hugin's flight) take.", v => Power.FlightCost = v);
            HFloat(sec, "Hugin wings cooldown minutes", 20f, "Pause between two flights.", v => RavenSkills.FlightCooldown = v * 60f);
            HFloat(sec, "Hugin wings seconds at level 1", 60f, "How long you fly at the lowest trust level the gift has.", v => OdinWings.Seconds = Mathf.Max(5f, v));
            HFloat(sec, "Hugin wings seconds at top level", 75f, "And at the top level.", v => OdinWings.SecondsTop = Mathf.Max(5f, v));
            HFloat(sec, "Hugin wings speed", 13f, "Metres a second ahead.", v => OdinWings.Speed = Mathf.Clamp(v, 2f, 40f));
            HFloat(sec, "Hugin wings climb speed", 7f, "Metres a second up or down.", v => OdinWings.ClimbSpeed = Mathf.Clamp(v, 1f, 20f));
            HFloat(sec, "Hugin wings max height", 40f, "Metres over the ground (or the sea) you may fly at most.", v => OdinWings.MaxHeight = Mathf.Clamp(v, 5f, 200f));
            HFloat(sec, "Hugin wings acceleration", 9f, "How fast the pace builds up (m/s each second).", v => OdinWings.Accel = Mathf.Clamp(v, 1f, 100f));
            HFloat(sec, "Hugin wings deceleration", 6f, "How fast it fades when you let go (m/s each second).", v => OdinWings.Decel = Mathf.Clamp(v, 1f, 100f));
            HFloat(sec, "Munin beast cost", 100f, "Strength Munin's beast takes.", v => Power.BeastCost = v);
            HFloat(sec, "Munin beast cooldown minutes", 30f, "Pause between two beasts.", v => RavenSkills.BeastCooldown = v * 60f);
            HFloat(sec, "Munin beast seconds at level 1", 30f, "How long the beast lasts at the lowest trust level the gift has.", v => Beast.Seconds = Mathf.Max(5f, v));
            HFloat(sec, "Munin beast seconds at top level", 40f, "And at the top level.", v => Beast.SecondsTop = Mathf.Max(5f, v));
            HFloat(sec, "Munin beast size", 2f, "How many times bigger you grow (a real body).", v => Beast.Size = Mathf.Clamp(v, 1.1f, 3f));
            HFloat(sec, "Munin beast damage percent", 50f, "How much more damage you do.", v => Beast.DamagePercent = Mathf.Max(0f, v));
            HFloat(sec, "Munin beast speed percent", 50f, "How much faster you move as the beast (the big body steps slower).", v => Beast.SpeedPercent = Mathf.Max(0f, v));
            HFloat(sec, "Munin beast armor percent", 50f, "How much more armour you have.", v => Beast.ArmorPercent = Mathf.Max(0f, v));
            HFloat(sec, "Munin beast tired seconds", 30f, "After the beast: how long stamina comes back slower.", v => Beast.TiredSeconds = Mathf.Max(0f, v));
            HFloat(sec, "Munin beast eyes up", 0.115f, "The burning eyes: metres up from the head bone (at normal size).", v => BeastEyes.Up = Mathf.Clamp(v, -0.3f, 0.3f));
            HFloat(sec, "Munin beast eyes ahead", 0.12f, "The burning eyes: metres ahead of the head bone (only when the face cannot be measured).", v => BeastEyes.Ahead = Mathf.Clamp(v, -0.3f, 0.4f));
            HFloat(sec, "Munin beast eyes depth", 0f, "The burning eyes: metres behind the front of the face (0: on it; more hides them inside the head).", v => BeastEyes.Depth = Mathf.Clamp(v, -0.05f, 0.15f));
            HFloat(sec, "Munin beast eyes apart", 0.04f, "The burning eyes: metres from the middle to each eye.", v => BeastEyes.Apart = Mathf.Clamp(v, 0f, 0.15f));
            HFloat(sec, "Munin beast eyes brightness", 1f, "The burning eyes: brightness (1 as made, 0 off).", v => BeastEyes.Power = Mathf.Clamp(v, 0f, 5f));
            HFloat(sec, "Munin ward cost", 40f, "Strength Munin's ward takes.", v => Power.WardCost = v);
            HFloat(sec, "Munin ward cooldown minutes at level 1", 10f, "Pause between two wards at trust level 1.", v => RavenSkills.WardCooldown = v * 60f);
            HFloat(sec, "Munin ward cooldown minutes at top level", 10f, "And at the top level.", v => RavenSkills.WardCooldownTop = v * 60f);
            HFloat(sec, "Munin ward seconds", 120f, "How long the ward stands if no blow breaks it.", v => Ward.Seconds = Mathf.Max(5f, v));
            Use(Hide(sec, "Munin ward trust level", 4, "The trust level Munin's ward opens on."), v => Ward.UnlockLevel = Mathf.Clamp(v, 1, 7));
            Use(Hide(sec, "Munin ward two blows at trust level", 6, "From this trust level the ward takes two blows instead of one."), v => Ward.TwoBlowsAt = Mathf.Clamp(v, 1, 8));
            Use(Hide(sec, "Munin ward three blows at trust level", 7, "From this trust level the ward takes three blows."), v => Ward.ThreeBlowsAt = Mathf.Clamp(v, 1, 8));
            HFloat(sec, "Munin ward front degrees", 180f, "How wide the guarded front is, in degrees (180: the whole front half).", v => Ward.FrontDegrees = Mathf.Clamp(v, 30f, 360f));
            HFloat(sec, "Munin ward push", 40f, "How hard a monster close by is pushed back when the ward takes its blow (0: not at all).", v => Ward.PushForce = Mathf.Max(0f, v));
            Use(Hide(sec, "Munin ward shows damage", true, "Show over you in red the damage the blow taken by the ward would have done."), v => Ward.ShowNumber = v);
            Shoulder.HuginBone = Hide(sec, "Hugin bone", "RightShoulder", "The character bone Hugin sits on.").Value;
            Shoulder.MuninBone = Hide(sec, "Munin bone", "LeftShoulder", "The character bone Munin sits on.").Value;
            _shoulderScale = Hide(sec, "Shoulder scale", 0.3f, "Size of the ravens on the shoulders, against the real Hugin (1 = full size). Easiest to pick in game: raven shoulder <size> [x y z [rx ry rz]].");
            _shoulderOff = new ConfigEntry<float>[]
            {
                Hide(sec, "Shoulder offset X", 0.14f, "Meters sideways from the shoulder bone, outwards (Hugin to the right, Munin to the left)."),
                Hide(sec, "Shoulder offset Y", 0.05f, "Meters up from the shoulder bone."),
                Hide(sec, "Shoulder offset Z", 0f, "Meters forward from the shoulder bone."),
            };
            _shoulderRot = new ConfigEntry<float>[]
            {
                Hide(sec, "Shoulder rotation X", 0f, "Degrees, if the ravens sit crooked."),
                Hide(sec, "Shoulder rotation Y", 180f, "Degrees (180: the raven model faces forward)."),
                Hide(sec, "Shoulder rotation Z", 0f, "Degrees."),
            };
            _huginTilt = Hide(sec, "Hugin tilt", 0f, "Degrees Hugin leans sideways, away from the head (negative: towards it). Easiest to pick in game: raven tilt <munin> [hugin].");
            _muninTilt = Hide(sec, "Munin tilt", 0f, "Degrees Munin leans sideways, away from the head (negative: towards it).");
            Shoulder.HuginTilt = _huginTilt.Value; Shoulder.MuninTilt = _muninTilt.Value;
            LandPanel.Saved = Hide(sec, "Landing tuning v2", "", "Written by the test window's «Landing» tab (console: raven test, button «Save»). Empty: as built in.");
            LandPanel.Unpack(LandPanel.Saved.Value);
            WingKeys.Load(); // the wing beat set by hand (raven wingui), if any
            TakeoffKeys.Load(); // the shoulder take-off pose edited in the test window
            RavenMotor.Load(); // the ravens' flight tuned in the test window (tab «Політ»), if any
            PerchTurn.Load(); // the raven's turn on a perch tuned in the test window (tab «Розворот»), if any
            FlightTemplates.Load(); // the flight templates (built in + config/RavenWatch/templates)
            SitTemplates.Load();    // the sitting templates (built in + config/RavenWatch/sit_templates)
            PlayerAnims.Load();     // the player's own animations (built in + config/RavenWatch/player_anims)
            SitSystem.Load();       // the sitting body's tuning (tab «Сидіння», sit.txt), if any
            BlackBox.Key = Hide(sec, "Flight black box key", KeyCode.None, "For testing (admins): saves the last 15 s of your ravens' flight (a file in config/RavenWatch/rec and a replay in the test window, tab «Flight»). It only listens: the key still works for the game and other mods. None: off.").Value;
            Outing.On = Config.Bind(sec, "Raven outings", true, "«Adaptive ravens»: now and then a raven leaves the shoulder to stretch its wings (flies along behind you or circles above), then comes back. Never underground, under a roof or in a fight.").Value;
            HFloat(sec, "Raven outings check from minutes", 20f, "The animation director looks whether the ravens may stretch their wings once in a random while between this...", v => AnimDirector.OutingMinMinutes = Mathf.Clamp(v, 1f, 240f));
            HFloat(sec, "Raven outings check to minutes", 30f, "...and this many minutes; then a coin toss (see the chance) decides.", v => AnimDirector.OutingMaxMinutes = Mathf.Clamp(v, 1f, 240f));
            HFloat(sec, "Raven outings chance percent", 50f, "The chance an outing begins at each such look. With both ravens on the shoulders, half the time both go, circling opposite ways.", v => AnimDirector.OutingChance = Mathf.Clamp01(v / 100f));
            HFloat(sec, "Shoulder arrival delay seconds", 10f, 8f, "After you enter the world (or come back from death) the ravens fly to your shoulders only this many seconds after the world around you has loaded (a loading screen or teleport holds the count), so the game and other mods are done first. Their gifts open once each has landed.", v => Shoulder.ArriveDelay = Mathf.Clamp(v, 0f, 60f));
            Shoulder.SetHoverEmotes(Hide(sec, "Hover during emotes", "dance,relax,rest", "Emotes during which the ravens leave the shoulder and hover above it, beating their wings, then sit back down. Sleeping in a bed also makes them fly clear; sitting (on the ground or a chair) only with 'sit' in the list. Comma-separated: wave, sit, challenge, cheer, nonono, thumbsup, point, blowkiss, bow, cower, cry, despair, flex, comehere, headbang, kneel, laugh, roar, shrug, dance, relax, toast, rest, vibe, loveyou, count.").Value);
            EyeGlow.SetList(Hide(sec, "Eye glow by trust level", EyeGlow.DefaultList, "How brightly the shoulder ravens' eyes glow, in percent of the game's own: for a raven not yet woken, then for trust level 1, 2 ... 7. Comma-separated; a level past the list takes the last value. Try live: raven eyes <percent>.").Value);
            Outfits.VanillaOnly = Config.Bind(sec, "Outfits only from the game", true, "The book's outfit page offers only the game's own head pieces; off: those added by other mods too.").Value;
            Float(sec, "Book of ravens size percent", 90f, "How big the Book of ravens window is, against the game's own interface scale. It shrinks by itself when it would not fit the screen.", v => RavenBook.SizePct = Mathf.Clamp(v, 40f, 200f));
            HFloat(sec, "Perch strength per day", 30f, "A raven sitting on a perch (built with the hammer; E sends Hugin, Shift+E Munin) gathers strength up to the perch's own day norm; this is the norm of a perch that names none. Under a bare roof the norm comes in over a game day, comfort speeds it up, out in the open and not cosy it is 80% slower.", v => Perches.DailyCap = Mathf.Clamp(v, 0f, 1000f));
            Use(Hide(sec, "Perches from workshop files", DevKit.On, "For testing only: perches made in the Workshop (config/RavenWatch/models, marked for the mod) join the hammer too when the world loads. Other players do not have them, so leave it off on a server."), v => Perches.FromFiles = v);
            Outfits.TuningWanted = Hide(sec, "Outfits all for tuning", DevKit.On, "For server admins only: on, the outfit page opens every piece that can sit on a raven, also those not yet tuned, and every thing for a raven saved in the Workshop (back, neck, legs), with the fitting buttons; off: only the finished pieces, opened by trust.").Value;
            Shoulder.Scale = _shoulderScale.Value;
            Shoulder.Offset = new Vector3(_shoulderOff[0].Value, _shoulderOff[1].Value, _shoulderOff[2].Value);
            Shoulder.Rotation = new Vector3(_shoulderRot[0].Value, _shoulderRot[1].Value, _shoulderRot[2].Value);
        }

        // trust (the ravens' levels): read at start; the food and head lists are item names from the game
        private void BindTrust()
        {
            const string sec = "7. Trust";
            ConfigEntry<string> heads = Hide(sec, "Ritual heads", Trust.Heads, "What each ritual at the Sacrificial stones asks for: the first wakes a raven just bought, each next one raises its level. TrophyName:count, comma-separated; the number of entries is the top level.");
            if (heads.Value == Trust.OldHeads || heads.Value == Trust.ThreeHeads) heads.Value = Trust.Heads; // the lists from before (no waking; 3-4 heads)
            Use(heads, v => Trust.Heads = v);
            Use(Hide(sec, "Hugin foods", Trust.HuginFoods, "What Hugin eats and the trust it gives a hungry raven: ItemName:points or ItemName:points:class (1 Meadows .. 5 Ashlands; satiety = 10 + 5 × class; without a class it is taken from a built-in table), comma-separated. Trust 1–2 accepts classes 1–3, trust 3–5 classes 2–4, trust 6–7 classes 2–5. Other classes are refused without consuming the item."), v => Trust.HuginFoods = v);
            Use(Hide(sec, "Munin foods", Trust.MuninFoods, "What Munin eats and the trust it gives. The same trust-level class limits apply: levels 1–2 accept classes 1–3, levels 3–5 classes 2–4, and levels 6–7 classes 2–5. Other classes are refused without consuming the item."), v => Trust.MuninFoods = v);
            HFloat(sec, "Strength at level 1", 100f, "A raven's most strength at the first level of trust.", v => Trust.MinStrength = v);
            HFloat(sec, "Strength at the top level", 300f, "And at the last; the levels between share the difference equally.", v => Trust.MaxStrength = v);
            HFloat(sec, "Points per level", 100f, "Trust from level 1 to 2; every next level takes this much more.", v => Trust.PerLevelPoints = Mathf.Max(1f, v));
            HFloat(sec, "Deeds: own side share", 0.5f, "Of what a deed gives in strength, this share comes as trust to the raven of that side (Hugin: peace, Munin: fight).", v => Trust.DeedOwnShare = v);
            HFloat(sec, "Deeds: other side share", 0.35f, "And this share to the other worn raven: both learn from both lives.", v => Trust.DeedOtherShare = v);
            HFloat(sec, "Deeds: daily limit", 40f, "Most trust a raven gets from deeds in one game day.", v => Trust.DeedDailyCap = v);
            HFloat(sec, "Trust per thought", 1f, "Trust the raven gets each time you ask its thought (a talk together). Counts toward the daily limit of deeds.", v => Trust.TalkTrust = v);
            HFloat(sec, "Trust from thoughts: daily limit", 10f, "Most trust a raven gets from thoughts in one game day.", v => Trust.TalkDailyCap = v);
            HFloat(sec, "Trust per order", 1f, "Trust the raven gets for each order it flies (play together). Counts toward the daily limit of deeds.", v => Trust.OrderTrust = v);
            HFloat(sec, "Trust from orders: daily limit", 8f, "Most trust a raven gets from orders in one game day.", v => Trust.OrderDailyCap = v);
            HFloat(sec, "Lost on death", 20f, "Points both worn ravens lose when you die.", v => Trust.DeathLoss = v);
            HFloat(sec, "Lost on death at levels 1 and 2", 10f, "Points a worn raven of trust level 1 or 2 loses when you die (new players die most).", v => Trust.DeathLossEarly = Mathf.Max(0f, v));
            HFloat(sec, "Death loss back percent", 50f, "Share of the points a death took that comes back when you take your raven from your grave in time.", v => Trust.GraveBackShare = Mathf.Clamp(v, 0f, 100f) / 100f);
            HFloat(sec, "Death loss back minutes", 10f, "How long after the death the raven still gives that share back.", v => Trust.GraveBackMinutes = Mathf.Max(0f, v));
            HFloat(sec, "Lost when taken off percent", 30f, 5f, "Share of the current level's bar lost when you take the raven off the shoulder.", v => Trust.TakeOffLoss = v / 100f);
            HFloat(sec, "Lost per hungry day", 10f, 5f, "Trust a worn raven loses per game day spent starving (satiety 0).", v => Trust.HungryLossPerDay = v);
            HFloat(sec, "Starving strike damage cut percent", 50f, "At satiety 0 the raven droops on the shoulder and its strike (Eye Peck / Talons) does this much less damage.", v => Trust.StarveStrikePct = Mathf.Clamp(v, 0f, 100f));
            HFloat(sec, "Trust from food: daily limit", 24f, "Most trust a raven gets from food in one game day.", v => Trust.FoodDailyCap = Mathf.Max(0f, v));
            HFloat(sec, "Satiety lost per game day", 25f, 30f, "Satiety (0–100) a raven loses by itself in one game day.", v => Satiety.PerDay = Mathf.Max(0f, v));
            HFloat(sec, "Satiety per gift percent", 25f, 30f, "Every gift used takes this share of its strength cost off the raven's satiety.", v => Satiety.GiftShare = Mathf.Max(0f, v) / 100f);
            HFloat(sec, "Order strength cost", 1f, "Strength an order costs (beside, circling, a trick, the chase).", v => Orders.Cost = Mathf.Max(0f, v));
            HFloat(sec, "Order scouting strength cost", 10f, "Strength the scouting order costs.", v => Orders.ScoutCost = Mathf.Max(0f, v));
            HFloat(sec, "Order satiety: beside or circling", 2.5f, 3f, "Satiety the order to fly beside you or circle over you takes (it lasts 5 minutes).", v => Orders.HungerStay = Mathf.Max(0f, v));
            HFloat(sec, "Order satiety: a trick", 1.25f, 1.5f, "Satiety one trick takes.", v => Orders.HungerTrick = Mathf.Max(0f, v));
            HFloat(sec, "Order satiety: the chase", 5f, 6f, "Satiety the chase takes from each raven.", v => Orders.HungerChase = Mathf.Max(0f, v));
            HFloat(sec, "Hungry gift cost multiplier", 1.5f, "Gifts cost this many times more strength when the raven is hungry (satiety 15 or less).", v => Satiety.HungryCostMul = Mathf.Max(1f, v));
            HFloat(sec, "Starving gift cost multiplier", 2f, "And at satiety 0.", v => Satiety.StarveCostMul = Mathf.Max(1f, v));
            HFloat(sec, "Fed strength gain multiplier", 1.25f, "Strength comes this many times faster while the raven is fed (satiety 41–80). When hungry (15 or less) none comes.", v => Satiety.FedGainMul = Mathf.Max(0f, v));
            HFloat(sec, "Stuffed strike damage cut percent", 25f, "Above satiety 80 the raven is stuffed: it does not fly about on its own and strikes this much weaker.", v => Satiety.StuffedStrikePct = Mathf.Clamp(v, 0f, 100f));
            HFloat(sec, "Favourite food fill multiplier", 1.5f, "Each raven has its own hidden favourite food: it fills this many times more.", v => Satiety.FavFillMul = Mathf.Max(1f, v));
            HFloat(sec, "Favourite food trust multiplier", 2f, "And gives this many times more trust (and the fed strike bonus lasts as many times longer).", v => { Satiety.FavTrustMul = Mathf.Max(1f, v); Satiety.FavFedMul = Satiety.FavTrustMul; });
            HFloat(sec, "Hungry steal minutes", 5f, "A hungry raven (satiety 15 or less) helps itself from your bag this often.", v => Satiety.StealMinutes = Mathf.Max(1f, v));
            HFloat(sec, "Hungry coin steal chance percent", 25f, "Chance that, when it steals, it also takes coins.", v => Satiety.CoinChance = Mathf.Clamp(v, 0f, 100f));
            HFloat(sec, "Hungry coin steal amount", 100f, "Coins taken at once (all of them, if you have fewer).", v => Satiety.CoinTake = Mathf.Max(0, Mathf.RoundToInt(v)));
            HFloat(sec, "Munin corpse peck chance percent", 50f, "A hungry Munin (satiety 40 or less) may peck at what you kill (at most once a minute).", v => Satiety.CorpseChance = Mathf.Clamp(v, 0f, 100f));
            HFloat(sec, "Munin corpse peck satiety", 8f, "Satiety a peck at a kill gives Munin.", v => Satiety.CorpseFill = Mathf.Max(0f, v));
            HFloat(sec, "Starving peck minutes", 30f, "A starving raven pecks you this often (never when your health is this low or lower).", v => Trust.StarvePeckMinutes = Mathf.Max(1f, v));
            HFloat(sec, "Starving peck damage", 10f, "Health a starving raven's peck takes.", v => Trust.StarvePeckDamage = Mathf.Max(0f, v));
            HFloat(sec, "Shy raven check minutes", 10f, "At trust levels 1 and 2 the raven may fly off: this often it decides.", v => Trust.WanderCheckMinutes = Mathf.Max(1f, v));
            HFloat(sec, "Shy raven chance percent at level 1", 45f, "Chance a level 1 raven flies off at each check.", v => Trust.WanderChance[0] = Mathf.Clamp01(v / 100f));
            HFloat(sec, "Shy raven chance percent at level 2", 15f, "Chance a level 2 raven flies off at each check (0: it stays).", v => Trust.WanderChance[1] = Mathf.Clamp01(v / 100f));
            HFloat(sec, "Shy raven away minutes min", 3f, "Shortest time a level 1 raven stays away.", v => Trust.WanderMinMinutes[0] = v);
            HFloat(sec, "Shy raven away minutes max", 6f, "Longest time a level 1 raven stays away.", v => Trust.WanderMaxMinutes[0] = v);
            HFloat(sec, "Shy raven away minutes min at level 2", 2f, "Shortest time a level 2 raven stays away.", v => Trust.WanderMinMinutes[1] = v);
            HFloat(sec, "Shy raven away minutes max at level 2", 4f, "Longest time a level 2 raven stays away.", v => Trust.WanderMaxMinutes[1] = v);
            HFloat(sec, "Waking away seconds", 60f, "After the waking ritual the raven flies to Odin for this long.", v => Trust.WakeAwaySeconds = v);
            HFloat(sec, "Ritual away minutes", 30f, "After every later ritual the raven flies to Odin for this long and comes back grown.", v => Trust.RitualAwaySeconds = v * 60f);
            HFloat(sec, "Raven size at level 1 percent", 56f, 70f, "How big a raven of trust level 1 (or asleep) sits on the shoulder, against the top level; it grows by the same step at every level.", v => { Shoulder.SmallestShare = Mathf.Clamp(v, 10f, 100f) / 100f; Shoulder.Redraw(); });
            HFloat(sec, "Stranger peck minutes", 1f, 5f, "A raven worn by someone other than whoever woke it pecks them this often.", v => Trust.PeckMinutes = v); // 1.3: every minute (the old 5 moves to it)
            HFloat(sec, "Stranger peck damage", 10f, "How much health a peck takes (never when the wearer has this much or less).", v => Trust.PeckDamage = v);
            Guide.Enabled = Config.Bind(sec, "Guide raven", true, "A red raven, the Guide, comes to explain each part of the ravens when it becomes yours (the nest and Odin's trial, waking, gifts, food, orders, gear, runes...). Each topic once per character, once talked to (E).").Value;
            Guide.RetrySeconds = Mathf.Max(1f, Hide(sec, "Guide repeat minutes", 12f, "If you do not talk to the Guide, he comes again after this many minutes.").Value) * 60f;
            HFloat(sec, "Ritual range", 25f, "Meters from the Sacrificial stones (where the boss trophies are left) the ritual works.", v => Trust.RitualRange = v);
        }

        private ConfigEntry<float> _shoulderScale;
        private ConfigEntry<float>[] _shoulderOff, _shoulderRot;
        private ConfigEntry<float> _huginTilt, _muninTilt;

        internal void SaveTilt(float hugin, float munin)
        {
            Shoulder.HuginTilt = hugin; Shoulder.MuninTilt = munin;
            if (_huginTilt == null) return;
            _huginTilt.Value = hugin; _muninTilt.Value = munin;
        }

        internal void SaveShoulder(float scale, Vector3 off, Vector3 rot)
        {
            Shoulder.Scale = scale; Shoulder.Offset = off; Shoulder.Rotation = rot;
            if (_shoulderScale == null) return;
            _shoulderScale.Value = scale;
            _shoulderOff[0].Value = off.x; _shoulderOff[1].Value = off.y; _shoulderOff[2].Value = off.z;
            _shoulderRot[0].Value = rot.x; _shoulderRot[1].Value = rot.y; _shoulderRot[2].Value = rot.z;
        }

        // fine tuning (balance numbers, look, flight, test tools) lives in its own file, config/RavenWatch/tuning.cfg:
        // the mod settings menus list only the plugin's own Config, so a player sees just what matters there
        private ConfigFile _tuning;
        private Dictionary<ConfigDefinition, string> _mainOrphans;
        private bool _orphansMoved;

        internal ConfigEntry<T> Hide<T>(string sec, string key, T def, string desc)
        {
            return Hide(sec, key, def, new ConfigDescription(desc));
        }

        internal ConfigEntry<T> Hide<T>(string sec, string key, T def, ConfigDescription desc)
        {
            if (_tuning == null)
            {
                string dir = Path.Combine(Paths.ConfigPath, "RavenWatch");
                try { Directory.CreateDirectory(dir); } catch (Exception) { }
                _tuning = new ConfigFile(Path.Combine(dir, "tuning.cfg"), true);
                try { _mainOrphans = Traverse.Create(Config).Property("OrphanedEntries").GetValue<Dictionary<ConfigDefinition, string>>(); }
                catch (Exception e) { Logger.LogWarning("Could not read the old settings for tuning.cfg: " + e.Message); }
            }
            ConfigEntry<T> entry = _tuning.Bind(sec, key, def, desc);
            // a value set in the main file before (up to 2.0.0 everything was there) moves over once
            ConfigDefinition d = new ConfigDefinition(sec, key);
            string old;
            if (_mainOrphans != null && _mainOrphans.TryGetValue(d, out old))
            {
                try { entry.SetSerializedValue(old); } catch (Exception) { }
                _mainOrphans.Remove(d);
                _orphansMoved = true;
            }
            return entry;
        }

        // after all binds: the main file forgets what moved to tuning.cfg
        private void SaveMovedSettings()
        {
            if (!_orphansMoved) return;
            _orphansMoved = false;
            try { Config.Save(); if (_tuning != null) _tuning.Save(); }
            catch (Exception e) { Logger.LogWarning("Could not save the moved settings: " + e.Message); }
        }

        // a setting applied now and on every change, through the server's value when it is a synced one (ServerConfig)
        private void Use<T>(ConfigEntry<T> e, Action<T> set)
        {
            Action apply = () => set(ServerConfig.Get(e));
            apply();
            e.SettingChanged += (o, ev) => apply();
            ServerConfig.Add(e, apply);
        }

        private void HFloat(string sec, string key, float def, float oldDef, string desc, Action<float> set)
        {
            ConfigEntry<float> e = Hide(sec, key, def, desc);
            if (Mathf.Approximately(e.Value, oldDef)) e.Value = def;
            Use(e, set);
        }

        private void HFloat(string sec, string key, float def, string desc, Action<float> set)
        {
            Use(Hide(sec, key, def, desc), set);
        }

        // like Float, but a value still at the old default is moved to the new default
        private void Float(string sec, string key, float def, float oldDef, string desc, Action<float> set)
        {
            ConfigEntry<float> e = Config.Bind(sec, key, def, desc);
            if (Mathf.Approximately(e.Value, oldDef)) e.Value = def;
            Use(e, set);
        }

        private void Float(string sec, string key, float def, string desc, Action<float> set)
        {
            Use(Config.Bind(sec, key, def, desc), set);
        }

        // a worn raven's thought: what happened lately or what it sees; each raven remembers its last few topics
        private readonly List<string>[] _thoughtTopics = { new List<string>(), new List<string>() };

        internal string ComposeThought(Player p, bool munin)
        {
            List<string> recent = _thoughtTopics[munin ? 1 : 0];
            try
            {
                Composer.Remark r = Composer.ComposeThought(PhraseDir, p, munin, recent, Mathf.Max(1f, _reactiveLookBack.Value) * 60f);
                if (r != null)
                {
                    recent.Add(r.Tag);
                    while (recent.Count > 3) recent.RemoveAt(0);
                    return r.Title;
                }
            }
            catch (Exception e) { Logger.LogWarning(RavenName(munin) + "'s thought failed: " + e.Message); }
            return p.GetPlayerName() + ", " + Phrases.Pick(PhraseDir, munin, true);
        }

        // a story from this raven's memory (by the fire), or null when it has nothing to tell
        internal string ComposeMemory(Player p, bool munin)
        {
            try { return Composer.ComposeMemory(PhraseDir, p, munin); }
            catch (Exception e) { Logger.LogWarning(RavenName(munin) + "'s memory failed: " + e.Message); return null; }
        }

        internal int MuninChance { get { return _muninChance.Value; } }

        // a line of one exact kind (an ability's: his scream, the fury...) from the quick ravens' phrases
        internal string ComposeTagged(Player p, bool munin, string tag)
        {
            try { return Composer.ComposeTag(PhraseDir, p, munin, tag); }
            catch (Exception e) { Logger.LogWarning("Raven line [" + tag + "] failed: " + e.Message); return null; }
        }
#endif

        // up to 1.1.2 shared ravens were an experiment, off by default: forget that old switch so everyone has them on
        private void DropOldSharedKeys()
        {
            try
            {
                Dictionary<ConfigDefinition, string> orphans = Traverse.Create(Config).Property("OrphanedEntries").GetValue<Dictionary<ConfigDefinition, string>>();
                if (orphans == null) return;
                bool changed = orphans.Remove(new ConfigDefinition("4. Experimental", "Shared ravens"));
                changed |= orphans.Remove(new ConfigDefinition("4. Experimental", "Shared ravens map pin"));
                if (changed) Config.Save();
            }
            catch (Exception e) { Logger.LogWarning("Could not drop the old shared ravens setting: " + e.Message); }
        }

        // the mod used to be called HuginWhispers: carry its settings and phrase files over once
        private void Migrate()
        {
            try
            {
                string oldCfg = Path.Combine(Paths.ConfigPath, "ko4erga.huginwhispers.cfg");
                if (File.Exists(oldCfg) && !File.Exists(Config.ConfigFilePath))
                {
                    File.Copy(oldCfg, Config.ConfigFilePath);
                    Config.Reload();
                }
                string oldDir = Path.Combine(Paths.ConfigPath, "HuginWhispers");
                string newDir = Path.Combine(Paths.ConfigPath, "RavenWatch");
                if (Directory.Exists(oldDir) && !Directory.Exists(newDir)) Directory.Move(oldDir, newDir);
            }
            catch (Exception e) { Logger.LogWarning("Could not move the old HuginWhispers settings: " + e.Message); }
        }

        private void UpdateKeep()
        {
            Diary.KeepSeconds = Mathf.Max(Diary.WindowSeconds, Mathf.Max(1f, _reactiveLookBack.Value) * 60f) + 60f;
        }

        // a new world or character: nothing from the last session may carry over
        internal void ResetSession()
        {
            RemoveOurTexts();
            _quipEndsAt = 0f;
            _quipRaven = null;
#if RAVEN_ITEM
            RavenSkills.Reset();
#endif
            Diary.Reset();
            Mute.ResetClient();
            ServerConfig.ResetClient();
            Shared.Reset();
            Group.ResetClient();
            _windowOpen = false;
            _status = "";
            _targetUid = 0;
            _targetName = "";
        }

        internal void Log(string message) { Logger.LogInfo(message); }
        internal void LogError(string message) { Logger.LogError(message); }

        // the mod's own admin check (the game's alone fails for clients of a dedicated server)
        internal static bool IsLocalAdmin()
        {
            return ZNet.instance != null && (ZNet.instance.LocalPlayerIsAdminOrHost() || Mute.AdminByServer);
        }

        // console: a raven for yourself right now
        internal string AdminQuick(bool munin)
        {
            Quip(munin, Mathf.Max(1f, _reactiveLookBack.Value) * 60f, null, null);
            return Lang.F("{0} (швидкий) летить до тебе.", (munin ? Lang.T("Мунін") : Lang.T("Хугін")));
        }

        // a raven is on its way or talking now: the guide waits
        internal bool RavenBusy { get { return _pendingKey != null || _quipEndsAt > 0f; } }

        internal string AdminVisit(bool munin)
        {
            Visit(munin, "", "");
            return Lang.F("{0} (звичайний) летить до тебе.", (munin ? Lang.T("Мунін") : Lang.T("Хугін")));
        }

        internal string AdminDiary()
        {
            Player p = Player.m_localPlayer;
            Composer.Remark r = Composer.ComposeQuip(PhraseDir, p, false, null, null, Mathf.Max(1f, _reactiveLookBack.Value) * 60f);
            Diary.Summary s = Diary.Summarize(Mathf.Max(1f, _reactiveLookBack.Value) * 60f);
            return Lang.F("Щоденник: {0} замірів, смертей {1}, вбивств {2}, будівництва {3}, афк {4}.\nХугін сказав би: {5}", s.Ticks, s.N(Ev.Death), s.N(Ev.Kill), s.N(Ev.Build), s.N(Ev.Afk), (r != null ? "[" + r.Tag + "] " + r.Title : Lang.T("нічого")));
        }

        // ---------------------------------------------------------------- RPC

        internal static void MarkSpawned()
        {
            if (Instance != null && Instance._pendingKey != null) Instance._pendingSpawned = true;
        }

        internal static void RegisterRpcs()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZPackage>(RpcRequest, new Action<long, ZPackage>(OnRequest));
            ZRoutedRpc.instance.Register<ZPackage>(RpcVisit, new Action<long, ZPackage>(OnVisit));
            ZRoutedRpc.instance.Register<ZPackage>(RpcReply, new Action<long, ZPackage>(OnReply));
            ZRoutedRpc.instance.Register<ZPackage>(RpcKill, new Action<long, ZPackage>(OnKill));
            Mute.Register();
            ServerConfig.Register();
            Shared.Register();
            Group.Register();
            Commands.Register();
#if RAVEN_ITEM
            RavenSkills.Register();
            Perches.RegisterRpcs();
#endif
        }

        // runs on the server: check the sender is an admin, then pass the raven on
        private static void OnRequest(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            long target = pkg.ReadLong();
            bool munin = pkg.ReadBool();
            string title = pkg.ReadString();
            string text = pkg.ReadString();
            bool quick = pkg.GetPos() < pkg.Size() && pkg.ReadBool();

            Instance.Logger.LogInfo("Raven request from " + sender + " to " + target + (quick ? " (quick)" : ""));
            if (!IsAdminSender(sender))
            {
                Reply(sender, Lang.T("Сервер відмовив: тебе немає в adminlist.txt."));
                return;
            }

            if (Mute.IsMutedUid(target))
            {
                Reply(sender, Lang.T("Цьому гравцю ворони вимкнені (галочка «без воронів»)."));
                return;
            }

            ZPackage visit = new ZPackage();
            visit.Write(munin);
            visit.Write(title ?? "");
            visit.Write(text ?? "");
            visit.Write(quick);
            ZRoutedRpc.instance.InvokeRoutedRPC(target, RpcVisit, visit);
            Reply(sender, Lang.F("{0} вирушив.", ((munin ? Lang.T("Мунін") : Lang.T("Хугін")) + (quick ? Lang.T(" (швидкий)") : ""))));
        }

        internal static bool IsAdminSender(long sender)
        {
            if (sender == ZDOMan.GetSessionID()) return true;
            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            return peer != null && peer.m_socket != null && ZNet.instance.IsAdmin(peer.m_socket.GetHostName());
        }

        internal static void ReplyTo(long to, string message) { Reply(to, message); }

        internal static long ServerPeerId()
        {
            if (ZNet.instance == null) return 0;
            if (ZNet.instance.IsServer()) return ZDOMan.GetSessionID();
            ZNetPeer server = ZNet.instance.GetServerPeer();
            return server != null ? server.m_uid : 0;
        }

        private static void Reply(long to, string message)
        {
            ZPackage r = new ZPackage();
            r.Write(message);
            ZRoutedRpc.instance.InvokeRoutedRPC(to, RpcReply, r);
        }

        // runs on the target player: only the server may send a raven
        private static void OnVisit(long sender, ZPackage pkg)
        {
            if (Player.m_localPlayer == null || Instance == null) return;
            if (sender != ServerPeerId() && sender != ZDOMan.GetSessionID())
            {
                Instance.Logger.LogWarning("Raven visit ignored: not from the server (" + sender + ")");
                return;
            }
            if (Mute.SelfMuted) return;
            bool munin = pkg.ReadBool();
            string title = pkg.ReadString();
            string text = pkg.ReadString();
            bool quick = pkg.GetPos() < pkg.Size() && pkg.ReadBool();
            Instance.Logger.LogInfo("Raven visit from server: " + (munin ? "Munin" : "Hugin") + (quick ? " (quick)" : ""));
            if (quick) Instance.Quip(munin, Mathf.Max(1f, Instance._reactiveLookBack.Value) * 60f, title, null);
            else Instance.Visit(munin, title, text);
        }

        // the creature died on another PC: that PC tells the killer
        // the prefab and the tamed mark go last: an older mod on the other side reads what it knows and stops there
        internal static void SendKill(long killerUid, string token, int boss, int stars, string prefab, bool tamed, bool raven)
        {
            if (ZRoutedRpc.instance == null || killerUid == 0) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(token ?? "");
            pkg.Write(boss);
            pkg.Write(stars);
            pkg.Write(prefab ?? "");
            pkg.Write(tamed);
            pkg.Write(raven); // (last: the raven's blow killed it)
            ZRoutedRpc.instance.InvokeRoutedRPC(killerUid, RpcKill, pkg);
        }

        private static void OnKill(long sender, ZPackage pkg)
        {
            if (Player.m_localPlayer == null) return;
            string token = pkg.ReadString();
            int boss = pkg.ReadInt();
            int stars = pkg.GetPos() < pkg.Size() ? pkg.ReadInt() : 0;
            string prefab = pkg.GetPos() < pkg.Size() ? pkg.ReadString() : ""; // from an older mod: none, then only the boss mark counts
            bool tamed = pkg.GetPos() < pkg.Size() && pkg.ReadBool();
            bool raven = pkg.GetPos() < pkg.Size() && pkg.ReadBool();
            Diary.Killed(null, token);
            Diary.Add(Ev.Kill, token, boss);
#if RAVEN_ITEM
            Power.OnKill(boss, stars, prefab, tamed, token);
            if (!tamed) Errands.OnKill(prefab, token, stars, boss == 1, raven);
#if RAVEN_STORY
            if (!tamed) StoryBus.Kill(prefab, stars, boss == 1);
#endif
            if (!tamed) BindRun.OnKill(Player.m_localPlayer); // (the creature was held by another game: a в'язь on a kill fires too)
#endif
        }

        private static void OnReply(long sender, ZPackage pkg)
        {
            if (Instance == null) return;
            Instance._status = pkg.ReadString();
        }

        // ---------------------------------------------------------------- visits

        private void Update()
        {
            ServerConfig.Update();
#if RAVEN_ITEM
            RavenTree.ServerTick(); // (the server: the raven trees into a world that has none yet)
#endif
            RavenMsg.Tick();
            // the «для авторів» build: the test window on its own key (no console needed)
            if (DevKit.On && Player.m_localPlayer != null && Input.GetKeyDown(DevKit.WindowKey) && !global::Console.IsVisible() && (Chat.instance == null || !Chat.instance.HasFocus())) DevPanel.Toggle(-1);
            if (_adminKey.Value.IsDown() && Player.m_localPlayer != null && ZNet.instance != null)
            {
                if (_windowOpen) _windowOpen = false;
                else if (ZNet.instance.LocalPlayerIsAdminOrHost() || Mute.AdminByServer) { _windowOpen = true; _status = ""; Mute.RequestList(); }
                else if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, Lang.T("Панель воронів — лише для адміна сервера"));
            }

            if (_windowOpen && Input.GetKeyDown(KeyCode.Escape)) _windowOpen = false;
            if (Shared.Enabled) Shared.Update();
            if (Input.anyKey || InventoryGui.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() || Menu.IsVisible()) Diary.MarkActive();
            if (_quipEndsAt > 0f && (Time.time >= _quipEndsAt || _quipRaven == null)) QuipDone();
            else if (_quipEndsAt > 0f && Time.time >= _nextQuipCheck) { _nextQuipCheck = Time.time + 0.3f; KeepQuipText(); }
#if RAVEN_ITEM
            if (RavenItem.Enabled)
            {
                long t0 = Perf.On ? Perf.Now : 0;
                RavenSkills.Update(this);
                if (Perf.On) Perf.Add(t0);
                Perf.Tick();
            }
#endif

            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 5f;
            try { Tick(); }
            catch (Exception e)
            {
                if (Time.time - _lastTickError > 300f) { _lastTickError = Time.time; Logger.LogWarning("Raven tick failed: " + e); }
            }
        }

        private void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || ZNet.instance == null || EnvMan.instance == null) return;
            Diary.Sample(p);
            if (!Mute.HelloSent && ZRoutedRpc.instance != null) Mute.SendHello();
            ServerConfig.Hello();
            if (_pendingKey != null && Shared.Enabled) TellRavenAlive();

            if (_pendingKey != null && Time.time - _pendingSince > (IsQuip(_pendingKey) && _quipEndsAt <= 0f ? QuipLandSeconds : _unansweredMinutes.Value * 60f))
            {
                bool retry = _pendingScheduled && !_pendingSpawned;
                RemoveOurTexts();
                // the raven never found a place to land (enemies, dungeon, sea): try again later the same day
                if (retry && EnvMan.instance != null && EnvMan.instance.m_dayLengthSec > 0)
                {
                    double retryAt = ZNet.instance.GetTimeSeconds() + UnityEngine.Random.Range(0.25f, 0.5f) * EnvMan.instance.m_dayLengthSec;
                    p.m_customData[NextVisitKey] = retryAt.ToString("R", CultureInfo.InvariantCulture);
                }
            }

#if RAVEN_ITEM
            if (Guide.Tick(this, p)) return; // the Guide: the one raven you talk to since 1.2
#endif
            if (!_visitsEnabled.Value || p.IsDead() || Mute.SelfMuted) return;

            bool gapPassed = Time.time - _lastVisit >= Mathf.Max(0f, _minGapMinutes.Value) * 60f;

            // reactive Hugin: separate from the planned visits
            float reactWindow = Mathf.Max(1f, _reactiveLookBack.Value) * 60f;
            bool allowed;
            if (Group.Decided(out allowed))
            {
                _lastReactive = Time.time;
                _quietCooldown = -1f;
                if (allowed && _pendingKey == null)
                {
                    bool reactMunin = UnityEngine.Random.Range(0, 100) < _reactiveMuninChance.Value;
                    // the one picked cannot say it (out, or yours but not on you): the other says it; neither: nobody
                    if (!CanSpeak(p, reactMunin))
                    {
                        if (!CanSpeak(p, !reactMunin)) { Logger.LogInfo("No raven can say it (out, or yours but not on you): no reaction to " + _askedTag); Diary.ClearEvents(); _lastVisit = Time.time; return; }
                        reactMunin = !reactMunin;
                    }
                    Logger.LogInfo((reactMunin ? "Munin" : "Hugin") + " reacts to: " + _askedTag);
                    if (OnShoulder(p, reactMunin)) FromShoulder(p, reactMunin, reactWindow, _askedTag);
                    else Quip(reactMunin, reactWindow);
#if RAVEN_ITEM
                    Director.Spoke(Director.Voice.Event, QuipSeconds); // the director counts it; the rest waits while it is said
#endif
                    _lastVisit = Time.time;
                    return;
                }
                // others in this fight already got theirs: forget it
                Logger.LogInfo("Hugin skips (group already visited): " + _askedTag);
                Diary.ClearEvents();
            }
            if (!Group.Waiting && _reactiveEnabled.Value && _pendingKey == null && ReactiveReady(reactWindow) && DirectorAllows())
            {
                string tag;
                if (Composer.ReactiveCandidate(reactWindow, _reactiveMinScore.Value, Mathf.Max(0f, _reactiveDelay.Value), out tag))
                {
                    _askedTag = tag;
                    Group.Ask(p.transform.position);
                    return;
                }
            }

            // planned visits (a raven to talk to every few days): only before 1.2; since then the quick ravens and the guide
#if !RAVEN_ITEM
            double now = ZNet.instance.GetTimeSeconds();
            double day = EnvMan.instance.m_dayLengthSec;
            if (day <= 0) return;

            double next;
            string raw;
            bool known = p.m_customData.TryGetValue(NextVisitKey, out raw)
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out next);
            if (!known)
            {
                ScheduleNext(p, now, day);
                return;
            }
            next = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);

            // another world with an earlier clock: start over instead of waiting for ages
            if (next - now > Math.Max(_maxDays.Value, 1f) * day * 1.5)
            {
                ScheduleNext(p, now, day);
                return;
            }

            if (now < next || _pendingKey != null || !gapPassed || Group.Waiting) return;

            bool munin = UnityEngine.Random.Range(0, 100) < _muninChance.Value;
            _lastVisit = Time.time;
            ScheduleNext(p, now, day);
            if (WornButOut(p, munin)) return;
            if (OnShoulder(p, munin)) { FromShoulder(p, munin, Diary.WindowSeconds, null); return; }
            Visit(munin, "", "");
            _pendingScheduled = true;
#endif
        }

        // a moment (a fall, a dungeon, a fish...) chose a single line over a talk: one raven says it right now, about that.
        // false: nobody could (no raven free, one already speaking)
        internal static bool MomentLine(string tag)
        {
#if RAVEN_ITEM
            Plugin me = Instance;
            Player p = Player.m_localPlayer;
            if (me == null || p == null || p.IsDead() || Mute.SelfMuted || me._pendingKey != null || Group.Waiting) return false;
            if (!me._visitsEnabled.Value || !me._reactiveEnabled.Value) return false; // switched off in the settings
            bool munin = UnityEngine.Random.Range(0, 100) < me._reactiveMuninChance.Value;
            if (!CanSpeak(p, munin))
            {
                if (!CanSpeak(p, !munin)) return false;
                munin = !munin;
            }
            float window = Mathf.Max(1f, me._reactiveLookBack.Value) * 60f;
            me.Logger.LogInfo(RavenName(munin) + " says a line about the moment: " + tag);
            if (me.OnShoulder(p, munin)) me.FromShoulder(p, munin, window, tag);
            else me.Quip(munin, window, null, tag);
            Director.Spoke(Director.Voice.Event, me.QuipSeconds);
            me._lastVisit = Time.time; // the usual quick ravens leave a little room after it
            return true;
#else
            return false;
#endif
        }

        // the director over all the ravens' own lines lets an event's raven speak now (RAVEN_ITEM; always yes without it)
        private static bool DirectorAllows()
        {
#if RAVEN_ITEM
            return Director.May(Director.Voice.Event);
#else
            return true;
#endif
        }

        // quick ravens: 30-40 minutes apart on a calm day, down to 10 when a lot happens, never sooner
        private bool ReactiveReady(float window)
        {
            float busy = Mathf.Max(1f, _busyCooldown.Value) * 60f;
            float sinceAny = Time.time - _lastVisit, sinceQuick = Time.time - _lastReactive;
            if (sinceAny < busy || sinceQuick < busy) return false;
            if (_quietCooldown < 0f)
            {
                float lo = Mathf.Max(_busyCooldown.Value, Mathf.Min(_reactiveCooldown.Value, _reactiveCooldownMax.Value));
                float hi = Mathf.Max(lo, _reactiveCooldownMax.Value);
                _quietCooldown = UnityEngine.Random.Range(lo, hi) * 60f;
            }
            int n = Composer.Intensity(window);
            int many = Mathf.Max(2, _busyEvents.Value);
            float t = n <= 1 ? 0f : Mathf.Clamp01((n - 1f) / (many - 1f));
            return sinceQuick >= Mathf.Lerp(_quietCooldown, busy, t);
        }

        // this raven may say a line now: from your shoulder, or flying in as the game's big raven — but never while yours
        // is out (shy, scouting, striking), and never a big one of a kind that is yours already (in a grave, a chest, lost:
        // a second Hugin or Munin would break it); with one raven of yours the big raven of the other kind still comes
        private static bool CanSpeak(Player p, bool munin)
        {
            if (WornButOut(p, munin)) return false;
#if RAVEN_ITEM
            if (RavenItem.Enabled && Trial.HadKind(p, munin) && (Instance == null || !Instance.OnShoulder(p, munin))) return false;
#endif
            return true;
        }

        // worn, but not on the shoulder right now: flown off (shy), scouting, up over the dome, out on a strike
        private static bool WornButOut(Player p, bool munin)
        {
#if RAVEN_ITEM
            if (!RavenItem.Enabled || p == null) return false;
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            ItemDrop.ItemData raven = munin ? m : h;
            if (raven == null) return false;
            if (Trust.Away(raven) || Strike.Busy(p, munin)) return true;
            if (!munin && (Dome.Mine() || RavenSkills.Scouting())) return true;
            return Shoulder.ModelOf(p, munin) != null && !Shoulder.Present(p, munin); // its bird is out (an outing, flying back)
#else
            return false;
#endif
        }

        // The raven picked already sits on your shoulder (RavenWatch items): it does not fly in, it says its line
        // from there (no "!" and no long text). The admin window and the console send ravens on their own path.
        private bool OnShoulder(Player p, bool munin)
        {
#if RAVEN_ITEM
            if (!RavenItem.Enabled || p == null) return false;
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            ItemDrop.ItemData raven = munin ? m : h;
            return raven != null && !WornButOut(p, munin); // one that is out (shy, scouting, over the dome, striking) cannot speak from there
#else
            return false;
#endif
        }

        private void FromShoulder(Player p, bool munin, float window, string preferTag)
        {
#if RAVEN_ITEM
            string line = null;
            try
            {
                Composer.Remark r = Composer.ComposeShoulderQuip(PhraseDir, p, munin, preferTag, window);
                if (r != null) { line = r.Title; p.m_customData[LastTagKey] = r.Tag; }
            }
            catch (Exception e) { Logger.LogWarning(RavenName(munin) + " could not speak from the shoulder: " + e.Message); }
            Diary.ClearEvents();
            if (string.IsNullOrEmpty(line)) line = p.GetPlayerName() + ", " + Phrases.Pick(PhraseDir, munin, true);
            Logger.LogInfo(RavenName(munin) + " speaks from the shoulder instead of flying in.");
            RavenSkills.SayFromShoulder(p, munin, line);
#endif
        }

        private void ScheduleNext(Player p, double now, double day)
        {
            float lo = Mathf.Max(0.1f, Mathf.Min(_minDays.Value, _maxDays.Value));
            float hi = Mathf.Max(_minDays.Value, _maxDays.Value);
            double next = now + UnityEngine.Random.Range(lo, hi) * day;
            p.m_customData[NextVisitKey] = next.ToString("R", CultureInfo.InvariantCulture);
        }

        // a reactive raven: lands, says one line with the player's name at once, and leaves
        private void Quip(bool munin, float window) { Quip(munin, window, null, _askedTag); }

        private void Quip(bool munin, float window, string line, string preferTag)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (string.IsNullOrEmpty(line))
            {
                string lastTag;
                p.m_customData.TryGetValue(LastTagKey, out lastTag);
                try
                {
                    Composer.Remark remark = Composer.ComposeQuip(PhraseDir, p, munin, preferTag, lastTag, window);
                    if (remark != null) { line = remark.Title; p.m_customData[LastTagKey] = remark.Tag; }
                }
                catch (Exception e) { Logger.LogWarning("Raven could not compose a quick remark: " + e.Message); }
                Diary.ClearEvents();
            }
            _quipEndsAt = 0f;
            _quipRaven = null;
            if (string.IsNullOrEmpty(line)) line = p.GetPlayerName() + ", " + Phrases.Pick(PhraseDir, munin, true);

            RemoveOurTexts();
            string key = QuipPrefix + (munin ? "munin" : "hugin");
            HashSet<string> shown = ShownTutorialsField.GetValue(p) as HashSet<string>;
            if (shown != null) shown.Remove(key);
            Raven.AddTempText(key, Grammar.Fix(line), "", "", munin);
            _pendingKey = key;
            _pendingText = FindTemp(key); NotTutorial(_pendingText);
            _pendingSince = Time.time;
        }

        internal static bool IsQuip(string key) { return key != null && key.StartsWith(QuipPrefix); }
        // the Guide talks to his own player only: other players never see him
        internal static bool IsPrivate(string key) { return key == KeyPrefix + "guide"; }

        // the quick remark's raven has landed: say the line now, over the raven and at the top of the screen
        internal void QuipLanded(Raven raven, Raven.RavenText text)
        {
            if (_quipEndsAt > 0f) return;
            float stay = Mathf.Max(QuipSeconds, RavenSpeech.ReadSeconds(text.m_topic)); // a long line: it waits until it can be read
            _quipEndsAt = Time.time + stay;
            _quipRaven = raven;
            _quipLine = text.m_topic;
            if (Chat.instance != null)
                Chat.instance.SetNpcText(raven.gameObject, Vector3.up * raven.m_textOffset, 60f, stay, RavenName(raven.m_isMunin), text.m_topic, true);
            Animator anim = RavenAnimator(raven);
            if (anim != null) anim.SetTrigger("talk");
            RavenVoice.SayBig(raven, text.m_topic, false);
            Shared.BroadcastSay(text);
        }

        // the game keeps the name as a translation key ($npc_munin); the mod speaks Ukrainian anyway
        internal static string RavenName(bool munin) { return munin ? Lang.T("Мунін") : Lang.T("Хугін"); }
        // whose / of whom: «Хугіна», «Муніна» (another language may need no case at all)
        internal static string RavenNameGen(bool munin) { return munin ? Lang.T("Муніна") : Lang.T("Хугіна"); }

        // the game drops a raven's words once they are off screen: while it still talks, put them back when it is in view
        private void KeepQuipText()
        {
            Raven raven = _quipRaven;
            Camera cam = Utils.GetMainCamera();
            if (raven == null || cam == null || Chat.instance == null || string.IsNullOrEmpty(_quipLine)) return;
            if (Style.HasNpcText(raven.gameObject)) return;
            Vector3 at = raven.transform.position + Vector3.up * raven.m_textOffset;
            Vector3 sp = cam.WorldToScreenPoint(at);
            if (sp.z <= 0f || sp.x < 0f || sp.y < 0f || sp.x > Screen.width || sp.y > Screen.height) return;
            if (Vector3.Distance(cam.transform.position, at) > 60f) return;
            Chat.instance.SetNpcText(raven.gameObject, Vector3.up * raven.m_textOffset, 60f, Mathf.Max(1f, _quipEndsAt - Time.time), RavenName(raven.m_isMunin), _quipLine, true);
        }

        // the others' copies of our raven stay only while it really sits here (see Shared.BroadcastAlive)
        private static void TellRavenAlive()
        {
            foreach (Raven r in UnityEngine.Object.FindObjectsByType<Raven>(FindObjectsSortMode.None))
            {
                if (r == null || !IsPending(CurrentText(r)) || !IsSpawned(r)) continue;
                Shared.BroadcastAlive();
                return;
            }
        }

        // a quick raven stays until it has said its piece, enemies around or not
        internal static bool HoldsQuip(Raven raven)
        {
            return Instance != null && raven != null && raven == Instance._quipRaven && Instance._quipEndsAt > Time.time;
        }

        // our raven (a quick remark, a visit) that has been talked to and leaves now: it flies off, not in a puff
        internal static bool SaidItsPiece(Raven raven)
        {
            if (raven == null || RavenHasTalked == null || !(bool)RavenHasTalked.GetValue(raven)) return false;
            Raven.RavenText cur = CurrentText(raven);
            return cur != null && cur.m_key != null && cur.m_key.StartsWith(KeyPrefix, StringComparison.Ordinal);
        }

        // A raven bringing a visit does not turn back on its own: vanilla sends ravens off when an enemy is near
        // the spot or a raid is on, and calls them again seconds later, so it flickered in the air over and over.
        // It leaves once talked to, once the visit runs out (Leaves after minutes), or when the player walks far off.
        internal static bool HoldsVisit(Raven raven)
        {
            if (Instance == null || raven == null || Instance._pendingKey == null) return false;
            if (!IsPending(CurrentText(raven))) return false;
            if (RavenHasTalked != null && (bool)RavenHasTalked.GetValue(raven)) return false;
            Player p = Player.m_localPlayer;
            return p != null && Vector3.Distance(p.transform.position, raven.transform.position) < raven.m_despawnDistance;
        }

        private void QuipDone()
        {
            Raven raven = _quipRaven;
            _quipEndsAt = 0f;
            _quipRaven = null;
            _quipLine = null;
            if (raven != null) RavenHasTalked.SetValue(raven, true); // vanilla then lets it fly off
            RemoveOurTexts();
        }

        private static readonly FieldInfo RavenHasTalked = AccessTools.Field(typeof(Raven), "m_hasTalked");
        private static readonly FieldInfo RavenAnimatorField = AccessTools.Field(typeof(Raven), "m_animator");
        private static Animator RavenAnimator(Raven r) { return RavenAnimatorField != null ? RavenAnimatorField.GetValue(r) as Animator : null; }

        internal void Visit(bool munin, string title, string text, string who = null)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (!munin && string.IsNullOrEmpty(text))
            {
                string lastTag;
                p.m_customData.TryGetValue(LastTagKey, out lastTag);
                Composer.Remark remark = null;
                try { remark = Composer.Compose(PhraseDir, p, lastTag, Diary.WindowSeconds); }
                catch (Exception e) { Logger.LogWarning("Hugin could not compose a remark: " + e.Message); }
                // a quiet stretch: half the time he just shares an old wisdom
                if (remark != null && !(remark.Tag == "idle" && Rng.Next(100) < 30))
                {
                    text = remark.Text;
                    if (string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(remark.Title)) title = remark.Title;
                    p.m_customData[LastTagKey] = remark.Tag;
                }
                Diary.ClearEvents();
            }
#if RAVEN_ITEM
            if (munin && string.IsNullOrEmpty(text) && Rng.NextDouble() < Composer.VisitAssembled)
            {
                // Munin's letter: put together from what you did lately, like a quick raven's word; the old whole texts stay a spare
                Composer.Remark made = null;
                try { made = Composer.ComposeQuip(PhraseDir, p, true, null, null, Diary.WindowSeconds); }
                catch (Exception e) { Logger.LogWarning("Munin could not compose a letter: " + e.Message); }
                if (made != null && !string.IsNullOrEmpty(made.Title)) { text = made.Title; Diary.ClearEvents(); }
            }
#endif
            if (string.IsNullOrEmpty(title)) title = Phrases.Pick(PhraseDir, munin, true);
            if (string.IsNullOrEmpty(text)) text = Phrases.Pick(PhraseDir, munin, false);

            RemoveOurTexts();
            string key = KeyPrefix + (who ?? (munin ? "munin" : "hugin"));
            HashSet<string> shown = ShownTutorialsField.GetValue(p) as HashSet<string>;
            if (shown != null) shown.Remove(key);
            Raven.AddTempText(key, Grammar.Fix(title), Grammar.Fix(text), "", munin);
            _pendingKey = key;
            _pendingText = FindTemp(key); NotTutorial(_pendingText);
            _pendingSince = Time.time;
            _pendingScheduled = false;
            _pendingSpawned = false;
        }

        // the game hides every tutorial raven when its hints are switched off in the settings; ours (the Guide too) are no
        // tutorials, so with the hints off they come anyway (with them on, nothing changes: the game's own ! stays)
        private static void NotTutorial(Raven.RavenText t) { if (t != null && !Raven.m_tutorialsEnabled) t.m_isTutorial = false; }

        private static Raven.RavenText FindTemp(string key)
        {
            List<Raven.RavenText> temp = TempTextsField.GetValue(null) as List<Raven.RavenText>;
            return temp != null ? temp.FindLast(t => t.m_key == key) : null;
        }

        internal void RemoveOurTexts()
        {
            List<Raven.RavenText> temp = TempTextsField.GetValue(null) as List<Raven.RavenText>;
            if (temp != null) temp.RemoveAll(t => t.m_key != null && t.m_key.StartsWith(KeyPrefix));
            Player p = Player.m_localPlayer;
            HashSet<string> shown = p != null ? ShownTutorialsField.GetValue(p) as HashSet<string> : null;
            if (shown != null) shown.RemoveWhere(k => k.StartsWith(KeyPrefix));
            _pendingKey = null;
            _pendingText = null;
            _pendingScheduled = false;
            _pendingSpawned = false;
        }

        // ---------------------------------------------------------------- admin window

        internal bool WindowOpen { get { return _windowOpen; } }
        internal static bool Exclamation = true;
        // the raven still has something to say to this player
        internal static bool Waiting { get { return Instance != null && Instance._pendingKey != null; } }
        // a raven still holding an older visit's text (same key) is not this visit's raven
        internal static bool IsPending(Raven.RavenText text)
        {
            if (Instance == null || text == null || Instance._pendingKey == null) return false;
            return Instance._pendingText != null ? text == Instance._pendingText : text.m_key == Instance._pendingKey;
        }
        // a raven that flew off keeps its old text and spot, invisible: only a landed one may show the mark
        internal static readonly Func<Raven, bool> IsSpawned = AccessTools.MethodDelegate<Func<Raven, bool>>(AccessTools.Method(typeof(Raven), "IsSpawned"));
        internal static readonly Func<Raven, bool> IsFlying = AccessTools.MethodDelegate<Func<Raven, bool>>(AccessTools.Method(typeof(Raven), "IsFlying"));

        private GUIStyle _windowStyle;
        private Texture2D _windowBg;

        // kept out of the game's asset cleanup on world load, or the window turns see-through
        internal static Texture2D Solid(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (!_windowOpen) return;
            if (Player.m_localPlayer == null) { _windowOpen = false; return; }
            if (_windowStyle == null || _windowBg == null)
            {
                _windowStyle = new GUIStyle(GUI.skin.window);
                _windowBg = Solid(new Color(0.08f, 0.07f, 0.06f, 1f));
                Texture2D bg = _windowBg;
                _windowStyle.normal.background = bg;
                _windowStyle.onNormal.background = bg;
                _windowStyle.focused.background = bg;
                _windowStyle.onFocused.background = bg;
                _windowStyle.normal.textColor = new Color(0.95f, 0.82f, 0.55f);
                _windowStyle.onNormal.textColor = _windowStyle.normal.textColor;
                _windowStyle.fontSize = 15;
                _windowStyle.padding = new RectOffset(12, 12, 28, 12);
            }
            _windowRect.height = Mathf.Min(700f, Screen.height - 40f);
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - 60f));
            // other mods may leave the GUI tinted or faded
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            _windowRect = GUILayout.Window(0x48554749, _windowRect, DrawWindow, Lang.T("Надіслати ворона"), _windowStyle);
        }

        private void DrawWindow(int id)
        {
            GUI.DrawTexture(new Rect(0f, 0f, _windowRect.width, _windowRect.height), _windowBg); // solid backing, whatever the style does
            GUILayout.Label(Lang.T("Кому:"));
            _playerScroll = GUILayout.BeginScrollView(_playerScroll, GUILayout.Height(150));
            List<ZNet.PlayerInfo> players = ZNet.instance != null ? ZNet.instance.GetPlayerList() : new List<ZNet.PlayerInfo>();
            foreach (ZNet.PlayerInfo info in players)
            {
                long uid = info.m_characterID.UserID;
                if (uid == 0) continue;
                bool selected = uid == _targetUid;
                bool muted = Mute.MutedOnline.Contains(uid);
                GUILayout.BeginHorizontal();
                bool newMuted = GUILayout.Toggle(muted, Lang.T(" без воронів"), GUILayout.Width(130));
                if (newMuted != muted) Mute.Set(uid, newMuted);
                if (GUILayout.Toggle(selected, " " + info.m_name, "Button") && !selected)
                {
                    _targetUid = uid;
                    _targetName = info.m_name;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_sendMunin, Lang.T(" Хугін"), "Button")) _sendMunin = false;
            if (GUILayout.Toggle(_sendMunin, Lang.T(" Мунін"), "Button")) _sendMunin = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang.T("Режим:"), GUILayout.Width(55));
            if (GUILayout.Toggle(!_sendQuick, Lang.T(" Звичайний (чекає на E)"), "Button") && _sendQuick) { _sendQuick = false; _preview = ""; }
            if (GUILayout.Toggle(_sendQuick, Lang.T(" Швидкий (одразу каже фразу)"), "Button") && !_sendQuick) { _sendQuick = true; _preview = ""; }
            GUILayout.EndHorizontal();

            if (_sendQuick)
            {
                GUILayout.Label(Lang.T("Фраза (порожньо — сам підбере з ніком гравця, про його останні хвилини):"));
                _adminTitle = GUILayout.TextField(_adminTitle, 200);
            }
            else
            {
                GUILayout.Label(_sendMunin ? Lang.T("Над головою (порожньо — випадкова фраза Муніна):") : Lang.T("Над головою (порожньо — під подію або випадкова):"));
                _adminTitle = GUILayout.TextField(_adminTitle, 120);
                GUILayout.Label(_sendMunin ? Lang.T("Після розмови (порожньо — випадкова фраза Муніна):") : Lang.T("Після розмови (порожньо — коментар про останні хвилини гравця):"));
                _adminText = GUILayout.TextArea(_adminText, 1000, GUILayout.Height(120));
            }

            GUILayout.BeginHorizontal();
            if (!_sendQuick && GUILayout.Button(Lang.T("Випадкові фрази")))
            {
                _adminTitle = Phrases.Pick(PhraseDir, _sendMunin, true);
                _adminText = Phrases.Pick(PhraseDir, _sendMunin, false);
            }
            if (GUILayout.Button(Lang.T("Очистити"))) { _adminTitle = ""; _adminText = ""; _preview = ""; }
            GUILayout.EndHorizontal();

            if (_sendQuick && GUILayout.Button(Lang.F("Перевірити, що скаже {0} (про тебе, нічого не надсилає)", (_sendMunin ? Lang.T("Мунін") : Lang.T("Хугін")))))
            {
                Composer.Remark r = null;
                try { r = Composer.ComposeQuip(PhraseDir, Player.m_localPlayer, _sendMunin, null, null, Mathf.Max(1f, _reactiveLookBack.Value) * 60f); }
                catch (Exception e) { _preview = Lang.F("Помилка: {0}", e.Message); }
                if (r != null) _preview = "[" + r.Tag + "]  " + r.Title;
            }
            if (!_sendQuick && !_sendMunin && GUILayout.Button(Lang.T("Перевірити, що скаже Хугін (про тебе, нічого не надсилає)")))
            {
                Composer.Remark r = null;
                try { r = Composer.Compose(PhraseDir, Player.m_localPlayer, null); }
                catch (Exception e) { _preview = Lang.F("Помилка: {0}", e.Message); }
                if (r != null) _preview = "[" + r.Tag + "]  " + (r.Title ?? Lang.T("(загальний заголовок)")) + "\n" + r.Text;
            }
            if ((_sendQuick || !_sendMunin) && !string.IsNullOrEmpty(_preview))
            {
                _previewScroll = GUILayout.BeginScrollView(_previewScroll, GUILayout.Height(80));
                GUILayout.Label(_preview);
                GUILayout.EndScrollView();
            }

            bool targetMuted = _targetUid != 0 && Mute.MutedOnline.Contains(_targetUid);
            GUI.enabled = _targetUid != 0 && !targetMuted;
            if (GUILayout.Button(_targetUid == 0 ? Lang.T("Вибери гравця") : targetMuted ? Lang.F("{0}: ворони вимкнені", _targetName) : Lang.F("Надіслати: {0}", _targetName), GUILayout.Height(32)))
            {
                ZPackage pkg = new ZPackage();
                pkg.Write(_targetUid);
                pkg.Write(_sendMunin);
                pkg.Write(_adminTitle ?? "");
                pkg.Write(_sendQuick ? "" : _adminText ?? "");
                pkg.Write(_sendQuick);
                ZRoutedRpc.instance.InvokeRoutedRPC(ServerPeerId(), RpcRequest, pkg);
                _status = Lang.T("Надсилаю…");
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
            if (GUILayout.Button(Lang.T("Закрити"))) _windowOpen = false;
            GUI.DragWindow();
        }
    }

    // ---------------------------------------------------------------- phrases

    internal static class Phrases
    {
        private static readonly System.Random Rng = new System.Random();

        private static string FileFor(string dir, bool munin, bool title)
        {
            return Path.Combine(dir, (munin ? "munin" : "hugin") + (title ? "_titles.txt" : "_texts.txt"));
        }

        internal static string Pick(string dir, bool munin, bool title)
        {
            List<string> lines = Read(FileFor(dir, munin, title));
            if (lines.Count == 0) lines = new List<string>(Lang.Lines(Path.GetFileName(FileFor(dir, munin, title)), Defaults.Get(munin, title)));
            if (lines.Count == 0) return munin ? Lang.T("Кра.") : Lang.T("Кра!");
            return Grammar.Fix(lines[Rng.Next(lines.Count)]);
        }

        private static List<string> Read(string path)
        {
            List<string> result = new List<string>();
            if (!File.Exists(path)) return result;
            foreach (string raw in File.ReadAllLines(path, System.Text.Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                result.Add(line.Replace("\\n", "\n"));
            }
            return result;
        }

        internal static void EnsureDefaults(string dir)
        {
            Directory.CreateDirectory(dir);
            Write(FileFor(dir, false, true), Lang.T("Хугін: що він каже над головою, коли прилітає. Одна фраза на рядок."), Defaults.HuginTitles);
            Write(FileFor(dir, false, false), Lang.T("Хугін: що він каже після розмови. Одна фраза на рядок, \\n — новий рядок."), Defaults.HuginTexts);
            Write(FileFor(dir, true, true), Lang.T("Мунін: що він каже над головою, коли прилітає. Одна фраза на рядок."), Defaults.MuninTitles);
            Write(FileFor(dir, true, false), Lang.T("Мунін: що він каже після розмови. Одна фраза на рядок, \\n — новий рядок."), Defaults.MuninTexts);
        }

        private static void Write(string path, string header, string[] lines)
        {
            if (File.Exists(path)) return;
            lines = Lang.Lines(Path.GetFileName(path), lines); // the current language's own lines
            List<string> all = new List<string>();
            all.Add("# " + header);
            all.Add(Lang.T("# Рядки з # на початку пропускаються. Файл можна правити під час гри."));
            all.AddRange(lines);
            File.WriteAllLines(path, all.ToArray(), new System.Text.UTF8Encoding(false));
        }
    }

    // ---------------------------------------------------------------- patches

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class GameStartPatch
    {
        private static void Postfix()
        {
            if (Plugin.Instance != null) Plugin.Instance.ResetSession();
            Plugin.RegisterRpcs();
#if RAVEN_ITEM
            SeneaL.Patch();
            PovMonoliths.Patch();
#endif
        }
    }

    // while our text is up, the raven's "random chatter" above its head is our title
    [HarmonyPatch(typeof(Raven), "Update")]
    internal static class RavenUpdatePatch
    {
        private static readonly Dictionary<Raven, List<string>[]> Saved = new Dictionary<Raven, List<string>[]>();
        private static readonly FieldInfo RandomTextTimer = AccessTools.Field(typeof(Raven), "m_randomTextTimer");

        private static void Prefix(Raven __instance)
        {
            Raven.RavenText cur = Plugin.CurrentText(__instance);
            bool ours = cur != null && cur.m_key != null && cur.m_key.StartsWith(Plugin.KeyPrefix, StringComparison.Ordinal);
            if (ours) Plugin.MarkSpawned(); // m_currentText is only set when the raven lands with this text
#if RAVEN_ITEM
            GuideLook.Update(__instance);
#endif
            // while a quick raven talks, its own idle chatter (seen only up close) must not replace the words
            if (Plugin.HoldsQuip(__instance) && RandomTextTimer != null) RandomTextTimer.SetValue(__instance, 0f);
            List<string>[] orig;
            bool swapped = Saved.TryGetValue(__instance, out orig);
            if (ours)
            {
                if (!swapped)
                {
                    Purge();
                    Saved[__instance] = new List<string>[] { __instance.m_randomTexts, __instance.m_randomTextsImportant };
                }
                if (__instance.m_randomTexts == null || __instance.m_randomTexts.Count != 1 || __instance.m_randomTexts[0] != cur.m_topic)
                {
                    __instance.m_randomTexts = new List<string> { cur.m_topic };
                    __instance.m_randomTextsImportant = new List<string> { cur.m_topic };
                }
            }
            else if (swapped)
            {
                __instance.m_randomTexts = orig[0];
                __instance.m_randomTextsImportant = orig[1];
                Saved.Remove(__instance);
            }
        }

        // ravens destroyed with an old world must not stay referenced
        private static void Purge()
        {
            List<Raven> dead = null;
            foreach (Raven r in Saved.Keys) if (r == null) { if (dead == null) dead = new List<Raven>(); dead.Add(r); }
            if (dead != null) foreach (Raven r in dead) Saved.Remove(r);
        }

        internal static void Forget(Raven r) { Saved.Remove(r); }
    }

    // vanilla lights the ! only for tutorials; our raven wants attention too, until you have talked to it
    [HarmonyPatch(typeof(Raven), "Update")]
    internal static class RavenExclamationPatch
    {
        private static void Postfix(Raven __instance)
        {
            Raven.RavenText cur = Plugin.CurrentText(__instance);
            if (cur == null || cur.m_key == null || !cur.m_key.StartsWith(Plugin.KeyPrefix, StringComparison.Ordinal)) return;
            bool quip = Plugin.IsQuip(cur.m_key);
            // a quick remark speaks on its own as soon as the raven has landed, and never shows the !
            if (quip && Plugin.IsPending(cur) && Plugin.IsSpawned(__instance) && !Plugin.IsFlying(__instance) && Plugin.Instance != null)
                Plugin.Instance.QuipLanded(__instance, cur);
            if (__instance.m_exclamation == null || (!quip && !Plugin.Exclamation)) return;
            bool show = !quip && Plugin.IsPending(cur) && Plugin.IsSpawned(__instance);
            if (__instance.m_exclamation.activeSelf != show) __instance.m_exclamation.SetActive(show);
        }
    }

    // Ours stays while it talks (a quick remark, a visit). Once it has said its piece it really flies off: the game sends
    // a talked raven away in a puff whenever any other text waits for it or the ground it landed on has gone, so ours
    // hopped up and vanished in the air (under a roof it still goes in a puff: the game's own check)
    [HarmonyPatch(typeof(Raven), "FlyAway")]
    internal static class RavenHoldPatch
    {
        private static bool Prefix(Raven __instance, ref bool forceTeleport)
        {
            if (Plugin.HoldsQuip(__instance) || Plugin.HoldsVisit(__instance)) return false;
            if (forceTeleport && Plugin.SaidItsPiece(__instance)) forceTeleport = false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Raven), "OnDestroy")]
    internal static class RavenDestroyPatch
    {
        private static void Prefix(Raven __instance)
        {
            RavenUpdatePatch.Forget(__instance);
#if RAVEN_ITEM
            GuideLook.Forget(__instance);
#endif
        }
    }

    // after the talk the text is used up: drop it so the raven flies off and the key never piles up in the save
    [HarmonyPatch(typeof(Raven), "Talk")]
    internal static class RavenTalkPatch
    {
        private static void Postfix(Raven __instance)
        {
            Raven.RavenText cur = Plugin.CurrentText(__instance);
            if (cur == null || cur.m_key == null || !cur.m_key.StartsWith(Plugin.KeyPrefix, StringComparison.Ordinal)) return;
            bool guide = false;
#if RAVEN_ITEM
            if (Guide.IsGuide(cur.m_key)) { Guide.Talked(); guide = true; }
#endif
            // the words on E aloud (the window shows the title and the text: the text is what he says)
            RavenVoice.SayBig(__instance, string.IsNullOrEmpty(cur.m_text) ? cur.m_topic : cur.m_text, guide);
            if (Plugin.Instance != null) Plugin.Instance.RemoveOurTexts();
        }
    }

    // free the cursor and stop the character while the admin window is open
    [HarmonyPatch(typeof(TextInput), "IsVisible")]
    internal static class TextInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (Plugin.Instance != null && Plugin.Instance.WindowOpen) __result = true;
#if RAVEN_ITEM
            if (DevPanel.Wants) __result = true; // the test window has the mouse (F8 toggles): cursor free, the game's input paused
#endif
        }
    }
}
