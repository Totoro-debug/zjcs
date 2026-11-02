// Original condition methods; see partner-condition-source-manifest.json.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Official.Partners
{
    public interface IPartnerConditionPlayer
    {
        int BaseLevel { get; }
        long GetItemAmount(int classId);
        bool IsMapAccessible(int mapClassId);
        bool RegionIsActived(int mapClassId, int region);
        bool HasNpc(int npcClassId);
    }

    // Configuration/player ports for the selected original condition bodies.
    // This reader covers the NPC meet/show/wait and collection-unlock closure.
    public sealed class PartnerConditionRules
    {
        private readonly NpcInfoParser npcs;
        private readonly ConditionInfoParser conditions;

        public PartnerConditionRules(Func<string, IEnumerable<Dictionary<string, object>>> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var npcRows = rows("npc").ToList();
            var conditionRows = rows("conditions").ToDictionary(row => Int(row, "ClassId"));
            var needed = new HashSet<int>();
            var visiting = new HashSet<int>();
            Action<Dictionary<string, object>> visit = null;
            visit = node => {
                if (node == null) throw new FormatException("Selected partner condition is absent.");
                string type = Text(node, "_type");
                if (type != "CompositeAndCondition" && type != "CompositeOrCondition") return;
                var ids = Ids(node);
                if (ids == null) return;
                foreach (int id in ids)
                {
                    if (!visiting.Add(id)) throw new FormatException("Cyclic selected partner condition: " + id);
                    if (!conditionRows.TryGetValue(id, out var row))
                        throw new FormatException("Selected partner condition reference is absent: " + id);
                    if (needed.Add(id)) visit(Map(row, "Condition"));
                    visiting.Remove(id);
                }
            };
            foreach (var row in npcRows)
                foreach (string field in new[] { "CanMeetCondition", "CanShowCondition", "WaitMeetCondition" }) visit(Map(row, field));
            foreach (var row in rows("function_limit").Where(row => Int(row, "Type") == 1011 && Int(row, "Key0") == 0 && Int(row, "Key1") == 0))
                visit(Map(row, "Condition"));
            conditions = new ConditionInfoParser(needed.ToDictionary(id => id,
                id => new ConditionInfo { Condition = Create(Map(conditionRows[id], "Condition")) }));
            npcs = new NpcInfoParser(npcRows.ToDictionary(row => Int(row, "Id"), row => new NpcInfo {
                Id = Int(row, "Id"), NpcType = (NpcType)Int(row, "NpcType"), IsAutoMeet = Bool(row, "IsAutoMeet"),
                CanMeetCondition = Create(Map(row, "CanMeetCondition")), CanShowCondition = Create(Map(row, "CanShowCondition")),
                WaitMeetCondition = Create(Map(row, "WaitMeetCondition")) }));
        }

        public bool IsMet(Dictionary<string, object> node, IPartnerConditionPlayer player) => IsMet(node, new DataHub(new Player(player)));
        public bool IsMet(Dictionary<string, object> node, DataHub hub) => Create(node).IsMet(hub);
        public bool IsMet(int id, IPartnerConditionPlayer player) => conditions.GetCondition(id).IsMet(new Player(player));
        public NpcMeetStatus MeetStatus(int npcClassId, IPartnerConditionPlayer player) => GetNpcMeetStatus(new Player(player), npcClassId);
        public bool HasManualMeetNpc(IPartnerConditionPlayer player) => new HasManualMeetNpcCondition(this).IsMet(new Player(player));
        public MeetRequirement? GetMeetRequirement(int npcClassId)
        {
            var requirement = npcs.Get(npcClassId)?.GetHasItemCanMeetCondition();
            return requirement == null ? (MeetRequirement?)null : new MeetRequirement(requirement.ClassId, requirement.Count, requirement.Op);
        }
        public readonly struct MeetRequirement
        {
            public readonly int ClassId, Count;
            public readonly string Op;
            internal MeetRequirement(int classId, int count, string op) { ClassId = classId; Count = count; Op = op; }
        }

            public List<(int, int)> GetMeetNpcItems(int npcClassId)
    {
        NpcInfo npcInfo = npcs.Get(npcClassId);
        if (npcInfo == null)
        {
            return null;
        }
        if (npcInfo.CanMeetCondition == null || npcInfo.CanMeetCondition.Type == ConditionType.None)
        {
            return null;
        }
        List<HasItemCondition> list = new List<HasItemCondition>();
        if (npcInfo.CanMeetCondition.IsComposite)
        {
            foreach (ICondition condition in (npcInfo.CanMeetCondition as CompositeCondition).GetConditions())
            {
                if (condition.Type == ConditionType.HasItem)
                {
                    list.Add(condition as HasItemCondition);
                }
            }
        }
        else
        {
            if (npcInfo.CanMeetCondition.Type != ConditionType.HasItem)
            {
                return null;
            }
            list.Add(npcInfo.CanMeetCondition as HasItemCondition);
        }
        List<(int, int)> list2 = new List<(int, int)>();
        foreach (HasItemCondition item in list)
        {
            list2.Add((item.ClassId, item.Count));
        }
        return list2;
    }

        private ICondition Create(Dictionary<string, object> node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            switch (Text(node, "_type"))
            {
                case "NoneCondition": return new NoneCondition(this);
                case "AlwaysCondition": return new AlwaysCondition(this);
                case "LevelCondition": return new LevelCondition(this) { Level = Int(node, "Level"), Op = Text(node, "Op", ">=") };
                case "HasItemCondition": return new HasItemCondition(this) { ClassId = Int(node, "ClassId"), Count = Int(node, "Count", 1), Op = Text(node, "Op", ">=") };
                case "MapUnlockCondition": return new MapUnlockCondition(this) { MapClassId = Int(node, "MapClassId") };
                case "MapRegionActivedCondition": return new MapRegionActivedCondition(this) { MapClassId = Int(node, "MapClassId"), Region = Int(node, "Region") };
                case "HasManualMeetNpcCondition": return new HasManualMeetNpcCondition(this);
                case "CompositeAndCondition": return new CompositeAndCondition(this) { Ids = Ids(node) };
                case "CompositeOrCondition": return new CompositeOrCondition(this) { Ids = Ids(node) };
                default: throw new NotSupportedException("Condition is outside the selected partner closure: " + Text(node, "_type"));
            }
        }
        private static Dictionary<string, object> Map(Dictionary<string, object> row, string key) => row.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;
        private static string Text(Dictionary<string, object> row, string key, string fallback = "") => row.TryGetValue(key, out var value) ? value as string : fallback;
        private static int Int(Dictionary<string, object> row, string key, int fallback = 0) => row.TryGetValue(key, out var value) ? Convert.ToInt32(value) : fallback;
        private static bool Bool(Dictionary<string, object> row, string key) => row.TryGetValue(key, out var value) && value is bool result && result;
        private static List<int> Ids(Dictionary<string, object> row) => row.TryGetValue("Ids", out var value) && value is List<object> ids ? ids.Select(Convert.ToInt32).ToList() : null;

        public sealed class Player
        {
            private readonly IPartnerConditionPlayer source;
            public readonly NpcManager NpcManager;
            public Player(IPartnerConditionPlayer source) { this.source = source ?? throw new ArgumentNullException(nameof(source)); NpcManager = new NpcManager(source); }
            public int BaseLevel => source.BaseLevel;
            public long GetItemAmount(int id) => source.GetItemAmount(id);
            public bool IsMapAccessible(int id) => source.IsMapAccessible(id);
            public bool RegionIsActived(int id, int region) => source.RegionIsActived(id, region);
        }
        public sealed class NpcManager
        {
            private readonly IPartnerConditionPlayer source;
            internal NpcManager(IPartnerConditionPlayer source) { this.source = source; }
            public bool ExistByNpcClassId(int id) => source.HasNpc(id);
        }
        public sealed class PlayerItemDataWrap { public int Id; public long Count; }
            public class DataHub
    {
        private Dictionary<ConditionDataType, long> mLongMap;

        private Dictionary<ConditionDataType, double> mDoubleMap;

        private Dictionary<ConditionDataType, object> mObjectMap;

        public DataHub()
        {
        }

        public DataHub(Player player)
        {
            Supply(ConditionDataType.Player, player);
        }

        public void Supply(ConditionDataType type, long value)
        {
            if (mLongMap == null)
            {
                mLongMap = new Dictionary<ConditionDataType, long>();
            }
            mLongMap[type] = value;
        }

        public bool TryGetData(ConditionDataType type, out long value)
        {
            value = 0L;
            if (mLongMap == null)
            {
                return false;
            }
            return mLongMap.TryGetValue(type, out value);
        }

        public void Supply(ConditionDataType type, double value)
        {
            if (mDoubleMap == null)
            {
                mDoubleMap = new Dictionary<ConditionDataType, double>();
            }
            mDoubleMap[type] = value;
        }

        public bool TryGetData(ConditionDataType type, out double value)
        {
            value = 0.0;
            if (mDoubleMap == null)
            {
                return false;
            }
            return mDoubleMap.TryGetValue(type, out value);
        }

        public void Supply(ConditionDataType type, object value)
        {
            if (value is Enum)
            {
                Supply(type, (int)value);
            }
            else
            {
                if (mObjectMap == null)
                {
                    mObjectMap = new Dictionary<ConditionDataType, object>();
                }
                mObjectMap[type] = value;
            }
        }

        public bool TryGetData(ConditionDataType type, out object value)
        {
            value = null;
            if (mObjectMap == null)
            {
                return false;
            }
            return mObjectMap.TryGetValue(type, out value);
        }

        public bool TryGetPlayer(out Player player)
        {
            player = null;
            if (TryGetData(ConditionDataType.Player, out object value))
            {
                player = value as Player;
                return true;
            }
            return false;
        }

        public static implicit operator DataHub(Player player)
        {
            return new DataHub(player);
        }
    }
        public enum ConditionDataType
{
    None,
    Player,
    PlayerLevel,
    PlayerSubRank,
    PlayerProfession,
    PlayerProfessionLevel,
    BaseBattleProps,
    BattleProps,
    NonBattleProps,
    TaskData,
    PlayerMapClassId,
    PlayerMapArea,
    ItemData,
    LegionMapBuildingLevel,
    PetData,
    LegionMapHangUpTimes,
    OpeningActivities,
    NpcTalentTreeNodeId,
    NpcTalentTreeNpcClassId,
    Rank,
    RankUnlockTimes,
    PlanesId,
    PlayerBaseLevel,
    ArenaPlanesCount
}
        public enum ConditionType
{
    None = 0,
    Always = 1,
    Exception = 2,
    Composite = 3,
    CompositeAnd = 4,
    CompositeOr = 5,
    HasItem = 6,
    HasItemType = 7,
    UsedItem = 8,
    HasOrUsedItem = 9,
    NotHasAndUsedItem = 10,
    MapUnlock = 11,
    MapWorldActived = 12,
    Level = 13,
    LevelBetween = 14,
    SubRank = 15,
    SubRankBetween = 16,
    Rank = 17,
    MaxProfessionRank = 18,
    ProfessionLevel = 19,
    ProfessionTotalLevel = 20,
    ProfessionUnlock = 21,
    HighestRankProfession = 22,
    PlayerProfession = 23,
    PlayerProfessionFaction = 24,
    BattleProps = 25,
    NonBattleProps = 26,
    BaseBattleProps = 27,
    TaskComplete = 28,
    TaskTypeComplete = 29,
    RegisteredDays = 30,
    MapRegionGoddess = 31,
    StagePass = 32,
    HomeLevel = 33,
    AnyMapGoddessActived = 34,
    ServerDays = 35,
    ServerHours = 36,
    Battle = 37,
    ChallengeStageCount = 38,
    ActivityChallengeStageCount = 39,
    ChallengeStageListCount = 40,
    CostItem = 41,
    DailyAward = 42,
    EquipLevel = 43,
    EquipLevelCount = 44,
    EquipTotalLevel = 45,
    FriendshipSum = 46,
    HangUpLevel = 47,
    HangUpLevelCount = 48,
    GotItems = 49,
    GotItemTypes = 50,
    HoldEquip = 51,
    HoldSkill = 52,
    EquipSplitTimes = 53,
    LoginCount = 54,
    MapEnter = 55,
    MapEntityEnd = 56,
    MapExplore = 57,
    MapGameCount = 58,
    MapGoddess = 59,
    MaxSkillRank = 60,
    OpenChest = 61,
    ShopBuyCount = 62,
    SkillRankUpTimes = 63,
    TotalOpenChest = 64,
    TouchOpCount = 65,
    WorldMapProgress = 66,
    MapGroupProgress = 67,
    ActivityShopBuyCount = 68,
    MapLocation = 69,
    MapGroupLocation = 70,
    MapArea = 71,
    DemonKingChallengeable = 72,
    HasTask = 73,
    HandInItem = 74,
    AnyMapRegionActived = 75,
    MapRegionActived = 76,
    MapGroupExploredCount = 77,
    MapLogicEntityEnd = 78,
    DemonBattleLayerPass = 79,
    DemonBattlePass = 80,
    HpPercent = 81,
    GameMonsterDestroy = 82,
    CanGetItems = 83,
    DailyDungeonDifficultyCount = 84,
    CombatRating = 85,
    CanFightMonster = 86,
    TaskSubmit = 87,
    MapGameTotalCount = 88,
    SkillLevelCount = 89,
    DailyDungeonGetReward = 90,
    DailyDungeonGetRewardNewRecord = 91,
    StageDemonBattlefieldChallengeCount = 92,
    DailyDungeonUnlock = 93,
    AnyDailyDungeonUnlock = 94,
    NpcFriendshipLevel = 95,
    NpcFriendshipMaxLevel = 96,
    DailyDungeonBattleCount = 97,
    NpcDialogueEnd = 98,
    Arrest = 99,
    TreasureGachaCount = 100,
    AccumulatedPayStarValue = 101,
    WeeklyVoidFissureAwardCount = 102,
    WeeklySecretAreaAwardCount = 103,
    ShopGroupLevel = 104,
    ActivePayJump = 105,
    LegionMapBuildingLevel = 106,
    PetGachaCount = 107,
    ActivityDailyDungeonBattleCount = 108,
    CombatRatingOrRankSuppress = 109,
    StoryCount = 110,
    StoryStatus = 111,
    ProfessionPromotePrerequisites = 112,
    HasNotTask = 113,
    ExploreTimes = 114,
    ExploreTimesNewRecord = 115,
    MopUpCount = 116,
    MopUpBreak = 117,
    MopUpAllBreak = 118,
    InitialWeapon = 119,
    TradingPostBuyItemCount = 120,
    TradingPostListItemCount = 121,
    TradingPostBuyOrListItemCount = 122,
    GemRank = 123,
    HangUpTotalLevel = 124,
    ProfessionSkillRankCount = 125,
    NpcFriendshipLevelCount = 126,
    HasNpcCount = 127,
    HasAnyNpcExclusiveGift = 128,
    TreasureLevelCount = 129,
    InlayGemCount = 130,
    HasPetCount = 131,
    PetLevelCount = 132,
    PetAbilityValue = 133,
    PetLevel = 134,
    PetPhase = 135,
    PetShareLevel = 136,
    SecretAreaRewardAccept = 137,
    SecretAreaChallengeCountOfDays = 138,
    SecretAreaChallengeCountOfDaysNewRecord = 139,
    ArenaFightRank = 140,
    FriendCount = 141,
    InLegion = 142,
    TeamChallengeJoinTimes = 143,
    HasItemTypeWithQuality = 144,
    DailyDungeonPersonalGrade = 145,
    LegionFightJoinCount = 146,
    LegionFightJoinCountNewRecord = 147,
    PlayDialogCount = 148,
    ServerRankUnlockDays = 149,
    PlayerRankAtServerRank = 150,
    JointStrikeDailyBreakWaves = 151,
    JointStrikeAllBreak = 152,
    JointStrikeActiveOpen = 153,
    JointStrikeChallengeCountOfDays = 154,
    NpcAdventureUnlock = 155,
    NpcAdventureFinishedNode = 156,
    NpcAdventureFinishedNodeCount = 157,
    NpcAdventureStartTravelCount = 158,
    DailyDungeonUnlockDifficulty = 159,
    HasAnyPet = 200,
    HasAnyEquipInlySiteCanUnlock = 201,
    NpcDelegateTaskFinishCount = 202,
    LegionMapHangUpTimes = 203,
    TreasureActivate = 204,
    BlessLevel = 205,
    SeasonBlessLevel = 206,
    HandInItemTimes = 207,
    MapLogicEntityEndByTag = 208,
    EquipSeasonLevel = 209,
    PlayerChannel = 210,
    PlayerMedia = 211,
    RegionCode = 212,
    ReleaseType = 213,
    FurnitureUnlock = 214,
    TreasureShareLevel = 215,
    HasManualMeetNpc = 216,
    PlayerLogin = 217,
    SDKLogin = 218,
    SeasonLoginCount = 219,
    EnterpriseWXLinked = 220,
    IsActivityOpen = 221,
    ActivityMopUpCount = 222,
    SeasonScore = 223,
    TreasureCollect = 224,
    MapWorldAloneStatus = 225,
    ServerSeasonUnlockDays = 226,
    NpcMeet = 227,
    SeasonEquipWear = 228,
    ActivityLiveSignUp = 229,
    ScreenShot = 230,
    MapGroupActivedRegion = 231,
    PlayAsYouDownload = 232,
    LogicalNot = 233,
    DomainTeamChallengeSeason = 234,
    EnterChallengeStageCount = 235,
    JointStrikeChallengeCountOfDaysNewRecord = 236,
    EnterStageDemonBattlefieldChallengeCount = 237,
    MapGalaxyIsCleared = 238,
    ActivityDailyPlayGamesCount = 239,
    ActivityPlayGamesScore = 240,
    ActivityPlayGamesAccumulateScore = 241,
    ActivityPlayGamesRateScore = 242,
    ActivityPlayGamesCombosCount = 243,
    MapRegionIsNotLimit = 244,
    SecretAreaRewardAcceptNewRecord = 245,
    JigsawPassLevel = 246,
    JumpGamePassLevel = 247,
    ActivityDifficultyChallengePlayCount = 248,
    ActivityDifficultyChallengeMaxScore = 249,
    AwardRetrieveItemCount = 250,
    AwardRetrieveItemMaxDays = 251,
    CarryFightMemberCount = 252,
    GuaranteedSucGemMergeRankCount = 253,
    ExploreEntityGroupIdCount = 254,
    NpcAdventureTravelQualityCount = 255,
    AccumulatedPay = 256,
    BattleRoyaleJoinTimes = 257,
    BattleRoyaleRankTimes = 258,
    BattleRoyaleSuccessTimes = 259,
    DomainType = 260,
    NoBlessCombatRating = 261,
    ActivityGameDailyPlayGamesCount = 262,
    ActivityGameFirstPlaceCount = 263,
    ActivityGameOutpaceRivalCount = 264,
    PlanesMergeActivityEnable = 265,
    PlanesId = 266,
    ServerStartTime = 267,
    TenGachaSkillRewardQualityCount = 268,
    TreasureAwardQualityCount = 269,
    CostItemCookingQualityMaxCount = 270,
    RecentDaysAccumulatedPay = 271,
    RecentDaysAccumulatedCost = 272,
    CostItemIncludeSource = 273,
    HomeVisitCount = 274,
    HomeBlessCount = 275,
    MultiPlayerDungeonEquipQualityCount = 276,
    HasStatus = 277,
    NpcTalentTreeNodePreNodeActiveCount = 278,
    LimitedTimeGiftLatestBought = 279,
    Sex = 280,
    PerformanceStatus = 281,
    PerformanceStepStatus = 282,
    CelebrationPhotoAlbumWithMapGrid = 283,
    HideAndSeekJoinTimes = 284,
    ActivityDreamDanceRank = 285,
    PlayerRegisteredDuringActivity = 286,
    GeneralRankUnlock = 287,
    LogicDomainServerDays = 288,
    AccountLostDays = 289,
    CelebrationPhotoAlbumWithPersonalNpc = 290,
    Match3GamePassLevel = 291,
    CelebrationPhotoAlbumPersonalNpcInRange = 292,
    MapCurrentRegionActived = 293,
    ArenaGroupPlanesCount = 294
}
        public enum NpcType
{
    None,
    Trader,
    MapExploreBoss,
    ProfessionTutor,
    Normal,
    Story,
    AwardRetrieve
}
        private abstract class ICondition
        {
            protected readonly PartnerConditionRules configuration;
            protected ICondition(PartnerConditionRules configuration) { this.configuration = configuration; }
            public abstract ConditionType Type { get; }
            public virtual bool IsComposite => false;
            public abstract bool IsMet(DataHub hub);
        }
        private sealed class NoneCondition : ICondition
{
public NoneCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.None;

    public override bool IsMet(DataHub dataHub)
    {
        return true;
    }
}

private sealed class AlwaysCondition : ICondition
{
public AlwaysCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.Always;

    public override bool IsMet(DataHub dataHub)
    {
        return false;
    }
}

private sealed class LevelCondition : ICondition
{
    public int Level;

    public string Op = ">=";

public LevelCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.Level;

    public override bool IsMet(DataHub dataHub)
    {
        return Formula.Compare(GetLevel(dataHub), Op, Level);
    }

    private int GetLevel(DataHub dataHub)
    {
        if (dataHub.TryGetData(ConditionDataType.PlayerBaseLevel, out long value))
        {
            return (int)value;
        }
        if (dataHub.TryGetData(ConditionDataType.Player, out object value2))
        {
            return (value2 as Player).BaseLevel;
        }
        return 0;
    }
}

private sealed class HasItemCondition : ICondition
{
    public int ClassId;

    public int Count = 1;

    public string Op = ">=";

public HasItemCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.HasItem;

    public override bool IsMet(DataHub dataHub)
    {
        long lhs = 0L;
        if (dataHub.TryGetPlayer(out var player))
        {
            lhs = player.GetItemAmount(ClassId);
        }
        else
        {
            if (!dataHub.TryGetData(ConditionDataType.ItemData, out object value) || !(value is PlayerItemDataWrap playerItemDataWrap))
            {
                return false;
            }
            if (playerItemDataWrap.Id == ClassId)
            {
                lhs = playerItemDataWrap.Count;
            }
        }
        return Formula.Compare(lhs, Op, Count);
    }
}

private sealed class MapUnlockCondition : ICondition
{
    public int MapClassId;

public MapUnlockCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.MapUnlock;

    public override bool IsMet(DataHub dataHub)
    {
        dataHub.TryGetPlayer(out var player);
        return player.IsMapAccessible(MapClassId);
    }
}

private sealed class MapRegionActivedCondition : ICondition
{
    public int MapClassId;

    public int Region;

public MapRegionActivedCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.MapRegionActived;

    public override bool IsMet(DataHub dataHub)
    {
        return GetValue(dataHub) >= 1;
    }

    private int GetValue(DataHub dataHub)
    {
        if (!dataHub.TryGetPlayer(out var player))
        {
            return 0;
        }
        if (!player.RegionIsActived(MapClassId, Region))
        {
            return 0;
        }
        return 1;
    }
}

private sealed class HasManualMeetNpcCondition : ICondition
{
public HasManualMeetNpcCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.HasManualMeetNpc;

    public override bool IsMet(DataHub dataHub)
    {
        return CheckMeet(dataHub);
    }

    private bool CheckMeet(DataHub hub)
    {
        if (hub.TryGetPlayer(out var player))
        {
            foreach (NpcInfo npc in configuration.npcs.GetNpcs(NpcType.MapExploreBoss))
            {
                if (!npc.IsAutoMeet && (player.NpcManager.ExistByNpcClassId(npc.Id) || npc.CanMeetCondition.IsMet(player)))
                {
                    return true;
                }
            }
            return false;
        }
        return false;
    }
}

private abstract class CompositeCondition : ICondition
{
    public List<int> Ids;

public CompositeCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.Composite;

    public List<ICondition> GetConditions()
    {
        List<ICondition> list = new List<ICondition>();
        if (Ids == null)
        {
            return list;
        }
        for (int i = 0; i < Ids.Count; i++)
        {
            ICondition condition = configuration.conditions.GetCondition(Ids[i]);
            if (condition == null)
            {
            }
            list.Add(condition);
        }
        return list;
    }
}

private sealed class CompositeAndCondition : CompositeCondition
{
public CompositeAndCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.CompositeAnd;

    public override bool IsComposite => true;

    public override bool IsMet(DataHub dataHub)
    {
        List<ICondition> conditions = GetConditions();
        for (int i = 0; i < conditions.Count; i++)
        {
            if (!conditions[i].IsMet(dataHub))
            {
                return false;
            }
        }
        return true;
    }
}

private sealed class CompositeOrCondition : CompositeCondition
{
public CompositeOrCondition(PartnerConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.CompositeOr;

    public override bool IsComposite => true;

    public override bool IsMet(DataHub dataHub)
    {
        foreach (ICondition condition in GetConditions())
        {
            if (condition.IsMet(dataHub))
            {
                return true;
            }
        }
        return false;
    }
}
        private sealed class ConditionInfo { public ICondition Condition; }
        private sealed class ConditionInfoParser
        {
            private readonly Dictionary<int, ConditionInfo> configs;
            public ConditionInfoParser(Dictionary<int, ConditionInfo> configs) { this.configs = configs; }
                public ICondition GetCondition(int id)
    {
        if (!configs.TryGetValue(id, out var value))
        {
            return null;
        }
        return value.Condition;
    }
        }
        private sealed class NpcInfoParser
        {
            private readonly Dictionary<int, NpcInfo> configs;
            public NpcInfoParser(Dictionary<int, NpcInfo> configs) { this.configs = configs; }
            public NpcInfo Get(int id) => configs.TryGetValue(id, out var value) ? value : null;
                public IEnumerable<NpcInfo> GetNpcs(NpcType ignoreType)
    {
        return configs.Values.Where((NpcInfo data) => data.NpcType != ignoreType);
    }
        }
        private sealed class NpcInfo
        {
            public int Id;
            public NpcType NpcType;
            public bool IsAutoMeet;
            public ICondition CanMeetCondition, CanShowCondition, WaitMeetCondition;
                public HasItemCondition GetHasItemCanMeetCondition()
    {
        if (CanMeetCondition == null || CanMeetCondition.Type == ConditionType.None)
        {
            return null;
        }
        if (CanMeetCondition.IsComposite)
        {
            return (CanMeetCondition as CompositeCondition).GetConditions().Find((ICondition data) => data.Type == ConditionType.HasItem) as HasItemCondition;
        }
        return CanMeetCondition as HasItemCondition;
    }
        }
        private static class Formula
        {
                public static bool Compare<T>(T lhs, string op, T rhs) where T : IComparable<T>, IEquatable<T>
    {
        return op switch
        {
            ">" => lhs.CompareTo(rhs) > 0,
            "<" => lhs.CompareTo(rhs) < 0,
            "=" => lhs.Equals(rhs),
            ">=" => lhs.CompareTo(rhs) >= 0,
            "<=" => lhs.CompareTo(rhs) <= 0,
            "!=" => !lhs.Equals(rhs),
            _ => lhs.CompareTo(rhs) >= 0,
        };
    }
        }
            private NpcMeetStatus GetNpcMeetStatus(Player player, int npcClassId)
    {
        NpcInfo npcInfo = npcs.Get(npcClassId);
        if (npcInfo == null)
        {
            return NpcMeetStatus.NotMeet;
        }
        if (player.NpcManager.ExistByNpcClassId(npcClassId))
        {
            return NpcMeetStatus.HadMeet;
        }
        if (npcInfo.CanMeetCondition.IsMet(player))
        {
            return NpcMeetStatus.CanMeet;
        }
        if (npcInfo.CanShowCondition.IsMet(player))
        {
            if (npcInfo.WaitMeetCondition.IsMet(player))
            {
                return NpcMeetStatus.WaitMeet;
            }
            return NpcMeetStatus.UnMeet;
        }
        return NpcMeetStatus.NotMeet;
    }
    }
}
