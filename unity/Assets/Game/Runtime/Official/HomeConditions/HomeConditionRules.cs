// Original predicates and explicit player/configuration ports; see home-condition-source-manifest.json.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.Partners;
using SubRank = Zjcs.Official.HomeConditions.HomeConditionRules.SubRank;
using Rank = Zjcs.Official.HomeConditions.HomeConditionRules.Rank;
using Profession = Zjcs.Official.HomeConditions.HomeConditionRules.Profession;

namespace Zjcs.Official.HomeConditions
{
    public readonly struct HomeConditionItem
    {
        public readonly int ItemType;
        public readonly long Count;
        public HomeConditionItem(int itemType, long count) { ItemType=itemType; Count=count; }
    }

    public interface IHomeConditionPlayer : IPartnerConditionPlayer
    {
        int SubRank { get; }
        int GetHighestUnlockedProfessionRank();
        IEnumerable<HomeConditionItem> GetInventoryItems();
        int GetStoryCount(string storyKey);
        bool IsAnyGoddessActived();
        int GetStageCount(int stageId);
    }

    public interface IHomeProfessionConditionPlayer : IHomeConditionPlayer
    {
        int Profession { get; }
    }

    public sealed class HomeConditionRules
    {
        private readonly PartnerConditionRules partnerRules;
        private readonly Dictionary<int, Dictionary<string, object>> conditionRows;
        private readonly Dictionary<int, ICondition> cached = new Dictionary<int, ICondition>();
        private readonly Func<IReadOnlyDictionary<string, object>, bool?> policy;
        private readonly ProfessionBaseInfoParser professionBaseInfoParser;

        public HomeConditionRules(IEnumerable<Dictionary<string, object>> conditions,
            PartnerConditionRules partnerRules, Func<IReadOnlyDictionary<string, object>, bool?> policy = null,
            IEnumerable<Dictionary<string, object>> professions = null)
        {
            this.partnerRules=partnerRules ?? throw new ArgumentNullException(nameof(partnerRules));
            this.policy=policy;
            professionBaseInfoParser=professions==null ? null : new ProfessionBaseInfoParser(professions);
            conditionRows=conditions.ToDictionary(row=>Number(row,"ClassId"), row=>(Dictionary<string,object>)row["Condition"]);
        }

        public bool IsMet(Dictionary<string,object> node, IHomeConditionPlayer player)
        {
            if (player==null) throw new ArgumentNullException(nameof(player));
            return Create(node).IsMet(new DataHub(new Player(player)));
        }

        public bool IsMet(int id, IHomeConditionPlayer player)
        {
            if (player==null) throw new ArgumentNullException(nameof(player));
            var condition=GetCondition(id);
            if (condition==null) throw new KeyNotFoundException("Original home condition is unavailable: "+id);
            return condition.IsMet(new DataHub(new Player(player)));
        }

        private ICondition GetCondition(int id)
        {
            if (cached.TryGetValue(id,out var condition)) return condition;
            if (!conditionRows.TryGetValue(id,out var row)) return null;
            condition=Create(row);cached.Add(id,condition);return condition;
        }

        private ICondition Create(Dictionary<string,object> node)
        {
            if (node==null) throw new ArgumentNullException(nameof(node));
            bool? result=policy?.Invoke(node);
            if (result.HasValue) return new PolicyCondition(this,result.Value);
            switch ((string)node["_type"])
            {
                case "NoneCondition": case "AlwaysCondition": case "LevelCondition":
                case "HasItemCondition": case "MapUnlockCondition": case "MapRegionActivedCondition":
                case "HasManualMeetNpcCondition": return new PartnerCondition(this,node);
                case "CompositeAndCondition": return new CompositeAndCondition(this) { Ids=Ids(node) };
                case "CompositeOrCondition": return new CompositeOrCondition(this) { Ids=Ids(node) };
                case "HasItemTypeCondition": return new HasItemTypeCondition(this) { ItemType=(ItemType)Number(node,"ItemType"),Count=Number(node,"Count") };
                case "RankCondition": return new RankCondition(this) { Rank=(Rank)Number(node,"Rank"),Op=(string)node["Op"] };
                case "SubRankCondition": return new SubRankCondition(this) { SubRank=(SubRank)Number(node,"SubRank"),Op=(string)node["Op"] };
                case "MaxProfessionRankCondition": return new MaxProfessionRankCondition(this) { Rank=Number(node,"Rank"),Op=(string)node["Op"] };
                case "StoryCountCondition": return new StoryCountCondition(this) { StoryKey=(string)node["StoryKey"],Count=Number(node,"Count"),Op=(string)node["Op"] };
                case "AnyMapGoddessActivedCondition": return new AnyMapGoddessActivedCondition(this);
                case "StagePassCondition": return new StagePassCondition(this) { StageId=Number(node,"StageId"),Op=(string)node["Op"] };
                case "PlayerProfessionCondition": return new PlayerProfessionCondition(this) { Profession=(Profession)Number(node,"Profession") };
                case "PlayerProfessionFactionCondition": return new PlayerProfessionFactionCondition(this) { Profession=(Profession)Number(node,"Profession") };
                default: throw new NotSupportedException("Condition outside migrated home closure: "+node["_type"]);
            }
        }

        private static int Number(Dictionary<string,object> row,string field)=>Convert.ToInt32(row[field]);
        private static List<int> Ids(Dictionary<string,object> row)=>row["Ids"]==null ? null : ((List<object>)row["Ids"]).Select(Convert.ToInt32).ToList();

        private abstract class ICondition
        {
            protected readonly HomeConditionRules configuration;
            protected ICondition(HomeConditionRules configuration) { this.configuration=configuration; }
            public abstract ConditionType Type { get; }
            public virtual bool IsComposite=>false;
            public abstract bool IsMet(DataHub hub);
            public virtual (long current,long target) GetProgress(DataHub hub)=>throw new NotSupportedException();
        }

        private sealed class PartnerCondition : ICondition
        {
            private readonly Dictionary<string,object> node;
            internal PartnerCondition(HomeConditionRules configuration,Dictionary<string,object> node):base(configuration) { this.node=node; }
            public override ConditionType Type=>throw new NotSupportedException("Delegated leaf type is not used by home evaluation.");
            public override bool IsMet(DataHub hub)
            {
                hub.TryGetPlayer(out var player);
                return configuration.partnerRules.IsMet(node,player.Source);
            }
        }

        private sealed class PolicyCondition : ICondition
        {
            private readonly bool value;
            internal PolicyCondition(HomeConditionRules configuration,bool value):base(configuration) { this.value=value; }
            public override ConditionType Type=>throw new NotSupportedException("Offline policy leaf has no replacement official type.");
            public override bool IsMet(DataHub hub)=>value;
        }

        private sealed class Player
        {
            internal readonly IHomeConditionPlayer Source;
            internal Player(IHomeConditionPlayer source) { Source=source; }
            private ItemManager itemManager;
            public ItemManager ItemManager=>itemManager ?? (itemManager=new ItemManager(Source));
            public SubRank SubRank=>(SubRank)Source.SubRank;
            public Profession Profession=>(Profession)((Source as IHomeProfessionConditionPlayer)?.Profession
                ?? throw new InvalidOperationException("The current profession owner is required by this home condition."));
            public int GetHighestUnlockedProfessionRank()=>Source.GetHighestUnlockedProfessionRank();
            public int GetStoryCount(string key)=>Source.GetStoryCount(key);
            public bool IsAnyGoddessActived()=>Source.IsAnyGoddessActived();
            public int GetStageCount(int id)=>Source.GetStageCount(id);
        }

        private sealed class ItemManager
        {
            internal readonly Dictionary<ItemType,List<PlayerItemDataWrap>> ItemTypeDict;
            internal ItemManager(IHomeConditionPlayer source)
            {
                ItemTypeDict=source.GetInventoryItems().GroupBy(item=>(ItemType)item.ItemType)
                    .ToDictionary(group=>group.Key,group=>group.Select(item=>new PlayerItemDataWrap { Count=item.Count }).ToList());
            }
        }
        private sealed class PlayerItemDataWrap { public long Count; }
        private abstract class PlayerTaskDataWrap
        {
            public abstract bool TryGetAddtionParam<T>(out T value);
        }

            private class DataHub
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
        public enum SubRank
{
    None,
    Norank,
    Blackiron1,
    Blackiron2,
    Blackiron3,
    Bronze1,
    Bronze2,
    Bronze3,
    Silver1,
    Silver2,
    Silver3,
    Gold1,
    Gold2,
    Gold3,
    Saint1,
    Saint2,
    Saint3,
    Legend1,
    Legend2,
    Legend3,
    Angel1,
    Angel2,
    Angel3,
    Godtouched1_1,
    Godtouched1_2,
    Godtouched1_3,
    Godtouched2_1,
    Godtouched2_2,
    Godtouched2_3,
    Godtouched3_1,
    Godtouched3_2,
    Godtouched3_3,
    Demigod1_1,
    Demigod1_2,
    Demigod1_3,
    Demigod2_1,
    Demigod2_2,
    Demigod2_3,
    Demigod3_1,
    Demigod3_2,
    Demigod3_3,
    Stargod1_1,
    Stargod1_2,
    Stargod1_3,
    Stargod2_1,
    Stargod2_2,
    Stargod2_3,
    Stargod3_1,
    Stargod3_2,
    Stargod3_3,
    Lordgod1_1,
    Lordgod1_2,
    Lordgod1_3,
    Lordgod2_1,
    Lordgod2_2,
    Lordgod2_3,
    Lordgod3_1,
    Lordgod3_2,
    Lordgod3_3,
    Eternal1_1,
    Eternal1_2,
    Eternal1_3,
    Eternal2_1,
    Eternal2_2,
    Eternal2_3,
    Eternal3_1,
    Eternal3_2,
    Eternal3_3,
    Chaos1_1,
    Chaos1_2,
    Chaos1_3,
    Chaos2_1,
    Chaos2_2,
    Chaos2_3,
    Chaos3_1,
    Chaos3_2,
    Chaos3_3,
    Nihility1_1,
    Nihility1_2,
    Nihility1_3,
    Nihility2_1,
    Nihility2_2,
    Nihility2_3,
    Nihility3_1,
    Nihility3_2,
    Nihility3_3
}
        public enum Rank
{
    None,
    Norank,
    Blackiron,
    Bronze,
    Silver,
    Gold,
    Saint,
    Legend,
    Angel,
    Godtouched1,
    Godtouched2,
    Godtouched3,
    Demigod1,
    Demigod2,
    Demigod3,
    Stargod1,
    Stargod2,
    Stargod3,
    Lordgod1,
    Lordgod2,
    Lordgod3,
    Eternal1,
    Eternal2,
    Eternal3,
    Chaos1,
    Chaos2,
    Chaos3,
    Nihility1,
    Nihility2,
    Nihility3
}
        public enum ItemType
{
    None = 0,
    Money = 1,
    ActivePayMoney = 10,
    BingoActivityMoney = 11,
    WrapItem = 12,
    Prop = 50,
    ActiveSkill = 100,
    PassiveSkill = 101,
    Furniture = 103,
    PromoteMedicine = 104,
    Material = 105,
    WuxingSpirit = 106,
    Plant = 107,
    MonsterProduct = 108,
    Mission = 109,
    RandomReward = 110,
    ActiveSkillPiece = 111,
    PassiveSkillPiece = 112,
    MonsterActiveSkill = 113,
    MonsterPassiveSkill = 114,
    PromotePlant = 115,
    CurePlant = 116,
    PropPlant = 117,
    SwordPiece = 118,
    WandPiece = 119,
    HelmetPiece = 120,
    ClothesPiece = 121,
    BootPiece = 122,
    ShieldPiece = 123,
    GlovePiece = 124,
    BookPiece = 125,
    BallPiece = 126,
    Sword = 200,
    Wand = 201,
    Shield = 202,
    Glove = 203,
    Book = 204,
    Ball = 205,
    Helmet = 210,
    Clothes = 211,
    Boot = 212,
    StrengthenMat = 300,
    ComplexMat = 301,
    Chest = 302,
    HomeFurnitureMat = 303,
    Food = 304,
    Treasure = 305,
    TreasurePiece = 306,
    TreasureSuit = 307,
    Key = 308,
    Gem = 309,
    Skin = 311,
    Score = 312,
    CureMedicine = 313,
    PropMedicine = 314,
    FastHangUp = 315,
    Badge = 316,
    MainWeaponAppearance = 317,
    SecondWeaponAppearance = 318,
    ClothesAppearance = 319,
    ExploreToken = 320,
    SkillLvUpMat = 321,
    CookingFormula = 322,
    Cooking = 323,
    CookingMaterial = 324,
    SecretAreaUnlockKey = 325,
    CommonEquipPiece = 326,
    DailyDungeonKey = 327,
    NpcGift = 328,
    NpcFriendship = 329,
    HeadAppearance = 330,
    BackAppearance = 331,
    HairAppearance = 332,
    FaceAppearance = 333,
    CommonItemPiece = 334,
    Privilege = 335,
    PeriodCard = 336,
    AppearanceGroup = 337,
    ActivityFestivalCollect = 338,
    HonorTitle = 339,
    RandomTreasurePiece = 340,
    RandomTreasureChest = 341,
    TreasureChest = 342,
    SkillPieceChest = 343,
    FacePaintAppearance = 344,
    ResetHangUpLevel = 345,
    Seed = 350,
    Bait = 351,
    LegionHangUpTool = 352,
    LegionHangUpMaterial = 353,
    LegionResource = 354,
    Pet = 400,
    PetFood = 401,
    PetPiece = 402,
    PetAbilityFood = 403,
    EntitySkill = 404,
    GeneralPetPiece = 405,
    GoddessCard = 410,
    DoorKey = 411,
    BlessItem = 412,
    KnotRope = 413,
    MopUpMaterial = 414,
    MainWeaponAppearanceChest = 415,
    SecondWeaponAppearanceChest = 416,
    ClothesAppearanceChest = 417,
    HeadAppearanceChest = 418,
    BackAppearanceChest = 419,
    HairAppearanceChest = 420,
    FaceAppearanceChest = 421,
    PropPassiveSkill = 422,
    PropActiveSkill = 423,
    ScratchCard = 424,
    SkillSkin = 425,
    SeasonBadge = 426,
    CommonTreasureMaterial = 427,
    FurnitureSkin = 428,
    HomeThemeSkin = 429,
    HomeEnvironmentSkin = 430,
    NpcTalentTreePoint = 431,
    NpcTalentTreeExp = 432,
    NpcUnlockItem = 433,
    EquipAttributeReplaceItem = 434,
    FacePaintAppearanceChest = 435,
    ActivityMoney = 436,
    ChatActionBook = 437,
    Other = 500
}
        public enum Profession
{
    None,
    Zhanshi,
    Huwei,
    Dunjiashi,
    Shouhuzhe,
    LegendShouhuzhe,
    Doushi,
    Kuangzhanshi,
    Zhengfuzhe,
    LegendZhengfuzhe,
    Fashi,
    Shushi,
    Modaoshi,
    Huimiezhe,
    LegendHuimiezhe,
    Xianzhe,
    Mishushi,
    Zhangkongzhe,
    LegendZhangkongzhe,
    Shouhuzhe6,
    Zhengfuzhe6,
    Huimiezhe6,
    Zhangkongzhe6,
    Shouhuzhe7,
    Zhengfuzhe7,
    Huimiezhe7,
    Zhangkongzhe7,
    Shouhuzhe8,
    Zhengfuzhe8,
    Huimiezhe8,
    Zhangkongzhe8,
    Shouhuzhe9,
    Zhengfuzhe9,
    Huimiezhe9,
    Zhangkongzhe9,
    Shouhuzhe10,
    Zhengfuzhe10,
    Huimiezhe10,
    Zhangkongzhe10
}
        private sealed class ProfessionBaseInfo { public Profession PrePro; }
private sealed class ProfessionBaseInfoParser
{
    private readonly Dictionary<Profession,ProfessionBaseInfo> configs;
    public ProfessionBaseInfoParser(IEnumerable<Dictionary<string,object>> rows)
    {
        configs=rows.ToDictionary(row=>(Profession)Number(row,"Profession"),
            row=>new ProfessionBaseInfo { PrePro=(Profession)Number(row,"PrePro") });
    }
    private ProfessionBaseInfo Get(Profession type)=>configs.TryGetValue(type,out var row) ? row : null;
    public bool ExistProfession(Profession pro)
    {
        return configs.ContainsKey(pro);
    }
    public bool IsPreProfession(Profession pro, Profession pre, bool recursive = true)
    {
        if (!ExistProfession(pro) || !ExistProfession(pre))
        {
            return false;
        }
        ProfessionBaseInfo professionBaseInfo = Get(pro);
        if (professionBaseInfo.PrePro == pre)
        {
            return true;
        }
        if (recursive)
        {
            while (professionBaseInfo.PrePro != Profession.None)
            {
                professionBaseInfo = Get(professionBaseInfo.PrePro);
                if (professionBaseInfo.PrePro == pre)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
        private class HasItemTypeCondition : ICondition
{
    public ItemType ItemType;

    public int Count;

public HasItemTypeCondition(HomeConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.HasItemType;

    public override bool IsMet(DataHub dataHub)
    {
        (long, long) progress = GetProgress(dataHub);
        return progress.Item1 >= progress.Item2;
    }

    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        return (current: GetValue(dataHub), target: Count);
    }

    protected long GetValue(DataHub dataHub)
    {
        if (dataHub.TryGetPlayer(out var player))
        {
            if (!player.ItemManager.ItemTypeDict.TryGetValue(ItemType, out var value))
            {
                return 0L;
            }
            long num = 0L;
            {
                foreach (PlayerItemDataWrap item in value)
                {
                    num += item.Count;
                }
                return num;
            }
        }
        return 0L;
    }
}

private class RankCondition : ICondition
{
    public Rank Rank;

    public string Op = ">=";

public RankCondition(HomeConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.Rank;

    public override bool IsMet(DataHub dataHub)
    {
        return Formula.Compare((int)GetData(dataHub), Op, (int)Rank);
    }

    private Rank GetData(DataHub hub)
    {
        if (hub.TryGetData(ConditionDataType.PlayerSubRank, out long value))
        {
            return ((SubRank)value).ToRank();
        }
        if (hub.TryGetData(ConditionDataType.Player, out object value2))
        {
            return (value2 as Player).SubRank.ToRank();
        }
        return Rank.None;
    }
}

private class SubRankCondition : ICondition
{
    public SubRank SubRank;

    public string Op = ">=";

public SubRankCondition(HomeConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.SubRank;

    public override bool IsMet(DataHub dataHub)
    {
        return Formula.Compare((int)GetSubRank(dataHub), Op, (int)SubRank);
    }

    protected SubRank GetSubRank(DataHub hub)
    {
        if (hub.TryGetData(ConditionDataType.PlayerSubRank, out long value))
        {
            return (SubRank)value;
        }
        if (hub.TryGetData(ConditionDataType.Player, out object value2))
        {
            return (value2 as Player).SubRank;
        }
        return SubRank.None;
    }
}

private class MaxProfessionRankCondition : ICondition
{
    public int Rank;

    public string Op = ">=";

public MaxProfessionRankCondition(HomeConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.MaxProfessionRank;

    public override bool IsMet(DataHub dataHub)
    {
        return Formula.Compare(GetValue(dataHub), Op, Rank);
    }

    private int GetValue(DataHub dataHub)
    {
        if (dataHub.TryGetData(ConditionDataType.TaskData, out object value) && value is PlayerTaskDataWrap playerTaskDataWrap)
        {
            playerTaskDataWrap.TryGetAddtionParam<int>(out var param);
            return param;
        }
        if (dataHub.TryGetPlayer(out var player))
        {
            return player.GetHighestUnlockedProfessionRank();
        }
        return 0;
    }
}

private class StoryCountCondition : ICondition
{
    public string StoryKey;

    public int Count;

    public string Op = "==";

public StoryCountCondition(HomeConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.StoryCount;

    public override bool IsMet(DataHub dataHub)
    {
        if (dataHub.TryGetPlayer(out var player))
        {
            return Formula.Compare(player.GetStoryCount(StoryKey), Op, Count);
        }
        return false;
    }
}

private class AnyMapGoddessActivedCondition : ICondition
{
public AnyMapGoddessActivedCondition(HomeConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.AnyMapGoddessActived;

    public override bool IsMet(DataHub dataHub)
    {
        return IsAnyActived(dataHub);
    }

    private bool IsAnyActived(DataHub dataHub)
    {
        if (!dataHub.TryGetPlayer(out var player))
        {
            return false;
        }
        return player.IsAnyGoddessActived();
    }
}

private class StagePassCondition : ICondition
{
    public int StageId;

    public string Op = ">=";

public StagePassCondition(HomeConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.StagePass;

    public override bool IsMet(DataHub dataHub)
    {
        return Formula.Compare(GetCount(dataHub), Op, 1);
    }

    private int GetCount(DataHub dataHub)
    {
        dataHub.TryGetPlayer(out var player);
        return player.GetStageCount(StageId);
    }
}

private abstract class CompositeCondition : ICondition
{
    public List<int> Ids;

public CompositeCondition(HomeConditionRules configuration) : base(configuration) { }

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
            ICondition condition = configuration.GetCondition(Ids[i]);
            if (condition == null)
            {
            }
            list.Add(condition);
        }
        return list;
    }
}

private class CompositeAndCondition : CompositeCondition
{
public CompositeAndCondition(HomeConditionRules configuration) : base(configuration) { }

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

private class CompositeOrCondition : CompositeCondition
{
public CompositeOrCondition(HomeConditionRules configuration) : base(configuration) { }

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

private class PlayerProfessionCondition : ICondition
{
    public Profession Profession;

public PlayerProfessionCondition(HomeConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.PlayerProfession;

    public override bool IsMet(DataHub dataHub)
    {
        return GetValue(dataHub) == Profession;
    }

    private Profession GetValue(DataHub dataHub)
    {
        if (dataHub.TryGetPlayer(out var player))
        {
            return player.Profession;
        }
        if (dataHub.TryGetData(ConditionDataType.PlayerProfession, out long value))
        {
            return (Profession)value;
        }
        return Profession.None;
    }
}

private class PlayerProfessionFactionCondition : ICondition
{
    public Profession Profession;

public PlayerProfessionFactionCondition(HomeConditionRules configuration) : base(configuration) { }

    public override ConditionType Type => ConditionType.PlayerProfessionFaction;

    public override bool IsMet(DataHub dataHub)
    {
        ProfessionBaseInfoParser professionBaseInfoParser = configuration.professionBaseInfoParser ?? throw new InvalidOperationException("Selected profession configuration is required.");
        Profession value = GetValue(dataHub);
        if (value == Profession)
        {
            return true;
        }
        return professionBaseInfoParser.IsPreProfession(value, Profession);
    }

    private Profession GetValue(DataHub dataHub)
    {
        if (dataHub.TryGetPlayer(out var player))
        {
            return player.Profession;
        }
        return Profession.None;
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
    }

    internal static class HomeConditionEnumExt
    {
            public static Rank ToRank(this SubRank subRank)
    {
        switch (subRank)
        {
        case SubRank.None:
            return Rank.None;
        case SubRank.Norank:
            return Rank.Norank;
        case SubRank.Blackiron1:
        case SubRank.Blackiron2:
        case SubRank.Blackiron3:
            return Rank.Blackiron;
        case SubRank.Bronze1:
        case SubRank.Bronze2:
        case SubRank.Bronze3:
            return Rank.Bronze;
        case SubRank.Silver1:
        case SubRank.Silver2:
        case SubRank.Silver3:
            return Rank.Silver;
        case SubRank.Gold1:
        case SubRank.Gold2:
        case SubRank.Gold3:
            return Rank.Gold;
        case SubRank.Saint1:
        case SubRank.Saint2:
        case SubRank.Saint3:
            return Rank.Saint;
        case SubRank.Legend1:
        case SubRank.Legend2:
        case SubRank.Legend3:
            return Rank.Legend;
        case SubRank.Angel1:
        case SubRank.Angel2:
        case SubRank.Angel3:
            return Rank.Angel;
        case SubRank.Godtouched1_1:
        case SubRank.Godtouched1_2:
        case SubRank.Godtouched1_3:
            return Rank.Godtouched1;
        case SubRank.Godtouched2_1:
        case SubRank.Godtouched2_2:
        case SubRank.Godtouched2_3:
            return Rank.Godtouched2;
        case SubRank.Godtouched3_1:
        case SubRank.Godtouched3_2:
        case SubRank.Godtouched3_3:
            return Rank.Godtouched3;
        case SubRank.Demigod1_1:
        case SubRank.Demigod1_2:
        case SubRank.Demigod1_3:
            return Rank.Demigod1;
        case SubRank.Demigod2_1:
        case SubRank.Demigod2_2:
        case SubRank.Demigod2_3:
            return Rank.Demigod2;
        case SubRank.Demigod3_1:
        case SubRank.Demigod3_2:
        case SubRank.Demigod3_3:
            return Rank.Demigod3;
        case SubRank.Stargod1_1:
        case SubRank.Stargod1_2:
        case SubRank.Stargod1_3:
            return Rank.Stargod1;
        case SubRank.Stargod2_1:
        case SubRank.Stargod2_2:
        case SubRank.Stargod2_3:
            return Rank.Stargod2;
        case SubRank.Stargod3_1:
        case SubRank.Stargod3_2:
        case SubRank.Stargod3_3:
            return Rank.Stargod3;
        case SubRank.Lordgod1_1:
        case SubRank.Lordgod1_2:
        case SubRank.Lordgod1_3:
            return Rank.Lordgod1;
        case SubRank.Lordgod2_1:
        case SubRank.Lordgod2_2:
        case SubRank.Lordgod2_3:
            return Rank.Lordgod2;
        case SubRank.Lordgod3_1:
        case SubRank.Lordgod3_2:
        case SubRank.Lordgod3_3:
            return Rank.Lordgod3;
        case SubRank.Eternal1_1:
        case SubRank.Eternal1_2:
        case SubRank.Eternal1_3:
            return Rank.Eternal1;
        case SubRank.Eternal2_1:
        case SubRank.Eternal2_2:
        case SubRank.Eternal2_3:
            return Rank.Eternal2;
        case SubRank.Eternal3_1:
        case SubRank.Eternal3_2:
        case SubRank.Eternal3_3:
            return Rank.Eternal3;
        case SubRank.Nihility1_1:
        case SubRank.Nihility1_2:
        case SubRank.Nihility1_3:
            return Rank.Nihility1;
        case SubRank.Nihility2_1:
        case SubRank.Nihility2_2:
        case SubRank.Nihility2_3:
            return Rank.Nihility2;
        case SubRank.Nihility3_1:
        case SubRank.Nihility3_2:
        case SubRank.Nihility3_3:
            return Rank.Nihility3;
        case SubRank.Chaos1_1:
        case SubRank.Chaos1_2:
        case SubRank.Chaos1_3:
            return Rank.Chaos1;
        case SubRank.Chaos2_1:
        case SubRank.Chaos2_2:
        case SubRank.Chaos2_3:
            return Rank.Chaos2;
        case SubRank.Chaos3_1:
        case SubRank.Chaos3_2:
        case SubRank.Chaos3_3:
            return Rank.Chaos3;
        default:
        {
            return Rank.None;
        }
        }
    }
    }
}
