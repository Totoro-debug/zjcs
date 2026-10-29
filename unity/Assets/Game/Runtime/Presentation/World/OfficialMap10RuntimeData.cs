using System;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.World
{
    [Serializable]
    public sealed class Map10RuntimeBattleData
    {
        public string manifest;
        public int mapClassId;
        public string sourceSha256;
        public Map10RuntimeStage[] stages;
        public Map10RuntimeRoster[] monsterRosters;
        public Map10RuntimeBattleScene[] battleScenes;
        public Map10RuntimeBattleEntityStageBinding[] entityStageBindings;
    }

    [Serializable]
    public sealed class Map10RuntimeStage
    {
        public int Id;
        public int GameId;
        public int Type;
        public int MaxRound;
        public int Repeat;
        public bool CanFastforward;
        public bool IsPlayerRoundLimit;
        public int PlayerRoundLimit;
        public float ShowMainPanelDelay;
        public bool SkipPrepareStatus;
        public bool AwardUsePlayerLevelAndSubRank;
        public bool UserStart;
        public bool LeaveInPreparing;
        public bool LeaveInPlaying;
        public bool Background;
        public bool ChangeCameraMode;
        public bool JoinInMiddle;
        public bool DismissTeamWhenLeaveOrEnd;
        public int[] WaveArrayIds;
        public int[] Awards;
    }

    [Serializable]
    public sealed class Map10RuntimeLevelNumber
    {
        public int id;
        public float[] values;
    }

    [Serializable]
    public sealed class Map10RuntimeRoster
    {
        public int StageId;
        public int GameId;
        public int MaxRound;
        public Map10RuntimeWave[] Waves;
    }

    [Serializable]
    public sealed class Map10RuntimeWave
    {
        public int Index;
        public Map10RuntimeMonster[] Units;
    }

    [Serializable]
    public sealed class Map10RuntimeMonster
    {
        public int MonsterId;
        public int MonsterGroupNo;
        public int MonsterType;
        public int Level;
        public float LevelOffset;
        public int SubRank;
        public int Faction;
        public int WaveIndex;
        public int FormationGroupId;
        public int FormationGroupIndex;
        public int Rotation;
        public bool FlipX;
        public int ViewGroup;
        public int HpPliesId;
        public Map10RuntimePoint Position;
        public Map10RuntimeProperty[] Properties;
        public Map10RuntimeSkill[] ActiveSkills;
        // Passive skill entries are item class ids in the official monster group table.
        public int[] PassiveSkills;
        public int[] DeactivePassiveSkills;
        public Map10RuntimeResilienceInfo ResilienceInfo;
    }

    [Serializable]
    public sealed class Map10RuntimeResilienceInfo
    {
        public int[] DisappearStatusIds;
        public int DisappearSkillId;
        public int SkillMaxHpPercent;
        public int SkillAtkPercent;
        public int ResetRound;
    }

    [Serializable]
    public sealed class Map10RuntimeSkill
    {
        public int EntityId;
        public int ItemId;
        public int Rank;
        public int Slot;
    }

    [Serializable]
    public sealed class Map10RuntimeProperty
    {
        public int Id;
        public long Value;
    }

    [Serializable]
    public sealed class Map10RuntimeBattleScene
    {
        public int GameId;
        public Map10RuntimeFightGridGroundComponent FightGridGroundComponent;
        public Map10RuntimeFightComponent FightComponent;
    }

    [Serializable]
    public sealed class Map10RuntimeFightGridGroundComponent
    {
        public Map10RuntimePoint Size;
        public Map10RuntimeVector3 OriginPos;
        public Map10RuntimeBattleCell[] PosInfos;
    }

    [Serializable]
    public sealed class Map10RuntimeBattleCell
    {
        public Map10RuntimePoint Pos;
        public int GroundType;
        public int Height;
    }

    [Serializable]
    public sealed class Map10RuntimeFightComponent
    {
        public Map10RuntimeFactionInfo[] FactionInfoList;
    }

    [Serializable]
    public sealed class Map10RuntimeFactionInfo
    {
        public int Faction;
        public Map10RuntimePoint[] EntryPosList;
        public int[] EntryDirList;
    }

    [Serializable]
    public sealed class Map10RuntimePoint
    {
        public int X;
        public int Y;
    }

    [Serializable]
    public sealed class Map10RuntimeVector3
    {
        public float X;
        public float Y;
        public float Z;
    }

    [Serializable]
    public sealed class Map10RuntimeStoryData
    {
        public string manifest;
        public int mapClassId;
        public string sourceSha256;
        public Map10RuntimeDialogRole[] dialogRoles;
        public Map10RuntimeLogicTag[] mapLogicTags;
        public Map13RuntimePlayerCondition[] playerConditions;
        public Map10RuntimeMapTalkEntry[] mapTalkEntries;
        public Map10RuntimeTalkConditionInfo[] mapTalkConditions;
        public Map10RuntimeStoryOption[] storyOptions;
        public Map10RuntimeFixedReward[] fixedRewards;
        public Map13RuntimeHandInItem[] handInItems;
        public Map10RuntimeNativeDialog[] nativeDialogs;
        public Map10RuntimePlayable[] playables;
        public Map10RuntimeMainlineTask[] mainlineTasks;
        public Map10RuntimeStory[] stories;
        public Map10RuntimeStory[] subgraphs;
        public Map10RuntimeEntityTemplate[] entityTemplates;
        public Map11RuntimeWitchInfo[] witchInfos;
        public Map11RuntimeWitchResult[] witchResultEffects;
        public Map11RuntimePeriodicityAward[] witchPeriodicityAwards;
        public Zjcs.Offline.World.MapPuzzleRule[] puzzleRules;
        public Map11RuntimeQuickBuy[] quickBuys;
    }

    [Serializable]
    public sealed class Map11RuntimeWitchInfo
    {
        public int ActiveCount;
        public int StoryBookId;
        public bool IsNextState;
        public Map11RuntimeStageResultRef[] StageResults;
    }

    [Serializable]
    public sealed class Map11RuntimeStageResultRef
    {
        public int ComponentType;
        public int ComponentIndex;
    }

    [Serializable] public sealed class Map11RuntimeWitchResult
    {
        public int ComponentType, ComponentIndex;
        public float HpPercentChange;
        public Map10RuntimeAwardItem[] AwardItems;
        public Map11RuntimeStatueStatus[] StatusEffects;
        public OfficialMainlineExtraProp[] ExtraProps;
        public int GroupId, SpawnEntityClassId, SpawnStageId, PlayerActionId, ActionId, AutoInteractionSubStateIndex;
    }

    [Serializable] public sealed class Map11RuntimeStatueStatus
    {
        public int StatusEffectId, EntryId, EffectType;
        public int[] FightStatusClassIds;
    }

    [Serializable] public sealed class Map11RuntimePeriodicityAward
    {
        public int PeriodicityGroupId, AwardGroupId, PeriodicityId, Weight, Action;
        public float ActionTime;
        public bool IsFirstReward;
        public Map11RuntimeStageResultRef[] StageResults;
    }

    [Serializable] public sealed class Map11RuntimeQuickBuy
    {
        public int ItemId, CurrencyId, RefreshType, SubRankScaleId, LevelScaleId;
        public long BaseCount;
        public long[] PriceList, PriceBuyCount;
        public Map11RuntimeQuickBuyCondition Condition;
    }
    [Serializable] public sealed class Map11RuntimeQuickBuyCondition
    {
        public string _type, Op;
        public int ClassId;
        public long Count;
    }

    [Serializable]
    public sealed class Map10RuntimeEntityTemplate
    {
        public int EntityClassId;
        public Map10RuntimeTemplateComponent[] Components;
    }

    [Serializable]
    public sealed class Map10RuntimeTemplateComponent
    {
        public string Type;
        public string Json;
    }

    [Serializable]
    public sealed class Map10RuntimeLogicTag
    {
        public string Tag;
        public int MapClass;
        public int LogicId;
        public int EntityClass;
        public bool IsRecordBlackbroad;
    }

    [Serializable]
    public sealed class Map10RuntimeMapTalkEntry
    {
        public string EntityId;
        public int Scenario;
        public string Tag;
        public Map10RuntimeTalkCondition Condition;
        public string TalkId;
        public int DialogId;
        public bool FouceStopOnTagEqual;
    }

    [Serializable]
    public sealed class Map10RuntimeTalkCondition
    {
        public string _type;
        public int[] Ids;
        public int StageId;
        public string StoryKey;
        public string Tag;
        public int Count;
        public int Status;
        public int MapClassId;
        public int Rank;
        public int Profession;
        public int WeaponType;
        public string Op;
    }

    [Serializable]
    public sealed class Map10RuntimeTalkConditionInfo
    {
        public int ClassId;
        public Map10RuntimeTalkCondition Condition;
    }

    [Serializable]
    public sealed class Map10RuntimeStoryOption
    {
        public string StoryName;
        public int Id;
        public string Text;
        public string Icon;
        public string BlackIcon;
        public string MinorText;
        public int ConditionType;
        public string ConditionParams;
    }

    [Serializable]
    public sealed class Map10RuntimeStoryNode
    {
        public int id;
        public string type;
        public string sourceJson;
        public int dialogId;
        public int awardId;
        public int fixedRewardId;
        public int handInItemId;
        public string storyName;
        public string subgraphName;
        public int storyStatus;
        public int entityState;
        public int count;
        public int playerLevel;
        public int compareOption;
        public int compareTargetInt;
        public int entityClassId;
        public string entityTag;
        public int positionX;
        public int positionY;
        public int destinationX;
        public int destinationY;
        public float moveTargetX;
        public float moveTargetY;
        public float seconds;
        public float moveSpeed;
        public int range;
        public int selectGetType;
        public int loEntityId;
        public int sceneType;
        public int mapClassId;
        public int regionId;
        public int[] followerClassIds;
        public int positionType;
        public int direction;
        public bool movable;
        public string tag;
        public bool needHideUi;
        public bool needShowUi;
        public float viewSizeOffset;
        public float duration;
        public int unlockRadius;
        public string registerKey;
        public float offsetX;
        public float offsetY;
        public string playableAssetGuid;
        public string[] playableAssetGuids;
        public float playableDuration;
        public bool waitPlayableStop;
        public string[] playableKeys;
        public Map10RuntimeMapLogicState[] mapLogicStates;
        public string playerConditionType;
        public Map13RuntimePlayerConditionData playerConditionParam;
        public int[] playerConditionIds;
        public int playerConditionNot;
        public bool endAsStoryFinish;
        public int[] options;
        public int[] removeEntityClassIds;
        public Map10RuntimeStoryPort[] ports;
    }

    [Serializable]
    public sealed class Map13RuntimeHandInItem
    {
        public int Id;
        public int CanHandTimes;
        public Map10RuntimeAwardItem[] Items;
    }

    [Serializable]
    public sealed class Map13RuntimePlayerCondition
    {
        public int ClassId;
        public Map13RuntimePlayerConditionData Condition;
    }

    [Serializable]
    public sealed class Map13RuntimePlayerConditionData
    {
        public string _type;
        public int[] Ids;
        public int ItemId, Count, StageId, MapClassId, Region, Progress;
        public string Op, StoryKey, Status, Tag;
    }

    [Serializable]
    public sealed class Map10RuntimeStoryPort
    {
        public string name;
        public Map10RuntimeStoryConnection[] connections;
    }

    [Serializable]
    public sealed class Map10RuntimeStoryConnection
    {
        public int nodeId;
        public string port;
    }

    [Serializable]
    public sealed class Map10RuntimeNativeDialog
    {
        public int Id;
        public int EntryNodeId;
        public Map10RuntimeNativeDialogNode[] Nodes;
    }

    [Serializable]
    public sealed class Map10RuntimePlayable
    {
        public string AssetGuid;
        public string[] Keys;
        public float Duration;
    }

    [Serializable]
    public sealed class Map10RuntimeNativeDialogNode
    {
        public int NodeId;
        public int NodeType;
        public int[] NextNodes;
        public int RoleId;
        public string RoleFaceType;
        public int DialogType;
        public string VoiceEventKey;
        public string TextKey;
        public string Text;
        public string OptionIconPath;
        public int OptionFilterType;
        public int OptionFilterProfession;
        public int ActionType;
        public string ActionParam;
        public int Profession;
        public int ReturnDialogId;
        public int ReturnNodeId;
        public Map10RuntimeAwardItem[] AwardItems;
        public bool NeedPopup;
        public int AwardLimitedDialogId;
        public int AwardLimitedNodeId;
        public string Story;
        public int NpcEntityClassId;
        public string PlayableKey;
        public bool IsSave;
    }

    [Serializable]
    public sealed class Map10RuntimeMapLogicState
    {
        public string Tag;
        public int State;
    }

    [Serializable]
    public sealed class Map10RuntimeEntityStageBinding
    {
        public string Component;
        public int[] StageIds;
    }

    [Serializable]
    public sealed class Map10RuntimeBattleEntityStageBinding
    {
        public int PlacementId;
        public Map10RuntimeEntityStageBinding[] StageComponents;
        public int[] PeriodicityGroupIds;
        public int[] PlayerPeriodicityGroupIds;
    }

    [Serializable]
    public sealed class Map10RuntimeFixedReward
    {
        public int Id;
        public int CanGetTimes;
        public Map10RuntimeAwardItem[] Items;
    }

    [Serializable]
    public sealed class Map10RuntimeMainlineTask
    {
        public int ClassId;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogPortraitData
    {
        public string manifest;
        public int mapClassId;
        public string sourceSha256;
        public Map10RuntimeDialogSpeakerStyle speakerStyle;
        public int[] voiceOverIds;
        public Map10RuntimeDialogPortrait[] portraits;
        public Map10RuntimeDialogIcon[] icons;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogIcon
    {
        public string uri;
        public string resource;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogSpeakerStyle
    {
        public Map10RuntimeDialogSpeakerSlot left;
        public Map10RuntimeDialogSpeakerSlot right;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogSpeakerSlot
    {
        public string lightAnimation, darkAnimation, moveInAnimation, moveOutAnimation, switchAnimation;
        public int direction;
        public float[] light;
        public float[] dark;
        public float colorDuration;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogPortrait
    {
        public string portraitPath;
        public bool needFlipOnRight;
        public Map10RuntimeDialogPortraitLayer main;
        public Map10RuntimeDialogPortraitFace face;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogPortraitLayer
    {
        public string resource;
        public string nodeName;
        public float[] position;
        public float[] nativeSize;
        public float[] pivot;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogPortraitFace
    {
        public string[] ignoredExpressions;
        public string nodeName;
        public float[] position;
        public float[] nativeSize;
        public float[] pivot;
        public float[] commonOffset;
        public Map10RuntimeDialogExpression[] expressions;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogExpression
    {
        public string rightResource;
        public string type;
        public string resource;
        public float[] position;
        public int[] nativeSize;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogRole
    {
        public int RoleId;
        public int EntityClassId;
        public string RoleName;
        public string PortraitPath;
    }

    [Serializable]
    public sealed class Map10RuntimeStory
    {
        public string key;
        public int maxCount;
        public int entryNodeId;
        public string graphJson;
        public Map10RuntimeStoryNode[] nodes;
        public Map10RuntimeDialogueGroup[] dialogueGroups;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogueGroup
    {
        public int Id;
        public Map10RuntimeDialogueLine[] Lines;
    }

    [Serializable]
    public sealed class Map10RuntimeDialogueLine
    {
        public int Order;
        public string DialogType;
        public string Text;
        public string VoiceEventKey;
        public int RoleId;
        public string RoleFaceType;
        public string PlayablePath;
    }

    [Serializable]
    public sealed class Map10RuntimeAwardItem
    {
        public int ItemId;
        public long Count;
    }

    [Serializable]
    internal sealed class Map10LocalSave
    {
        public int schema = 1;
        public string slotId;
        public int gridX = 24;
        public int gridY = 23;
        public bool firstBattleIntroShown;
        public string[] completedStories = Array.Empty<string>();
        public Map10RuntimeStoryCount[] storyPlayCounts = Array.Empty<Map10RuntimeStoryCount>();
        public int[] completedStages = Array.Empty<int>();
        public int[] completedPeriodicityGroups = Array.Empty<int>();
        public int[] completedEntityPlacements = Array.Empty<int>();
        public Map10RuntimeMapLogicState[] mapLogicStates = Array.Empty<Map10RuntimeMapLogicState>();
        public Zjcs.Offline.World.MapPuzzleEntityState[] puzzleStates = Array.Empty<Zjcs.Offline.World.MapPuzzleEntityState>();
        public Map11PendingPuzzleOperation pendingPuzzleOperation;
        public bool hasPendingPuzzleOperation;
        public string[] receipts = Array.Empty<string>();
        public string[] nativeDialogAwardReceipts = Array.Empty<string>();
        public Map10RuntimeBattleResult[] battleResults = Array.Empty<Map10RuntimeBattleResult>();
        public string activeBattle = string.Empty;
        public int activeBattleStageId;
        public int activeBattlePlacementId;
        public long activeBattleSavedAt;
        public int[] aloneWorldClassIds = Array.Empty<int>();
        public string map13GridVisibility;
    }

    [Serializable]
    internal sealed class Map11PendingPuzzleOperation
    {
        public int placementId;
        public int subStateIndex;
        public string inventoryReceipt;
    }

    [Serializable]
    public sealed class Map10RuntimeStoryCount
    {
        public string key;
        public int count;
    }

    [Serializable]
    public sealed class Map10RuntimeBattleResult
    {
        public int placementId;
        public int stageId;
        public int reason;
        public bool won;
        public int round;
        public long playerHp;
        public long enemyHp;
    }
}
