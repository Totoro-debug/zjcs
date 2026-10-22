using System;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.World
{
    [Serializable]
    internal sealed class Map10RuntimeBattleData
    {
        public string manifest;
        public int mapClassId;
        public string sourceSha256;
        public Map10RuntimeStage[] stages;
        public Map10RuntimeRoster[] monsterRosters;
        public Map10RuntimeBattleScene[] battleScenes;
    }

    [Serializable]
    internal sealed class Map10RuntimeStage
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
        public bool UserStart;
        public bool LeaveInPreparing;
        public bool LeaveInPlaying;
        public bool Background;
        public bool ChangeCameraMode;
        public bool JoinInMiddle;
        public bool DismissTeamWhenLeaveOrEnd;
        public int[] WaveArrayIds;
    }

    [Serializable]
    internal sealed class Map10RuntimeRoster
    {
        public int StageId;
        public int GameId;
        public int MaxRound;
        public Map10RuntimeWave[] Waves;
    }

    [Serializable]
    internal sealed class Map10RuntimeWave
    {
        public int Index;
        public Map10RuntimeMonster[] Units;
    }

    [Serializable]
    internal sealed class Map10RuntimeMonster
    {
        public int MonsterId;
        public int MonsterGroupNo;
        public int MonsterType;
        public int Level;
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
    internal sealed class Map10RuntimeResilienceInfo
    {
        public int[] DisappearStatusIds;
        public int DisappearSkillId;
        public int SkillMaxHpPercent;
        public int SkillAtkPercent;
        public int ResetRound;
    }

    [Serializable]
    internal sealed class Map10RuntimeSkill
    {
        public int EntityId;
        public int ItemId;
        public int Rank;
        public int Slot;
    }

    [Serializable]
    internal sealed class Map10RuntimeProperty
    {
        public int Id;
        public long Value;
    }

    [Serializable]
    internal sealed class Map10RuntimeBattleScene
    {
        public int GameId;
        public Map10RuntimeFightGridGroundComponent FightGridGroundComponent;
        public Map10RuntimeFightComponent FightComponent;
    }

    [Serializable]
    internal sealed class Map10RuntimeFightGridGroundComponent
    {
        public Map10RuntimePoint Size;
        public Map10RuntimeVector3 OriginPos;
        public Map10RuntimeBattleCell[] PosInfos;
    }

    [Serializable]
    internal sealed class Map10RuntimeBattleCell
    {
        public Map10RuntimePoint Pos;
        public int GroundType;
        public int Height;
    }

    [Serializable]
    internal sealed class Map10RuntimeFightComponent
    {
        public Map10RuntimeFactionInfo[] FactionInfoList;
    }

    [Serializable]
    internal sealed class Map10RuntimeFactionInfo
    {
        public int Faction;
        public Map10RuntimePoint[] EntryPosList;
        public int[] EntryDirList;
    }

    [Serializable]
    internal sealed class Map10RuntimePoint
    {
        public int X;
        public int Y;
    }

    [Serializable]
    internal sealed class Map10RuntimeVector3
    {
        public float X;
        public float Y;
        public float Z;
    }

    [Serializable]
    internal sealed class Map10RuntimeStoryData
    {
        public string manifest;
        public int mapClassId;
        public string sourceSha256;
        public Map10RuntimeDialogRole[] dialogRoles;
        public Map10RuntimeLogicTag[] mapLogicTags;
        public Map10RuntimeMapTalkEntry[] mapTalkEntries;
        public Map10RuntimeTalkConditionInfo[] mapTalkConditions;
        public Map10RuntimeStoryOption[] storyOptions;
        public Map10RuntimeNativeDialog[] nativeDialogs;
        public Map10RuntimePlayable[] playables;
        public Map10RuntimeStory[] stories;
    }

    [Serializable]
    internal sealed class Map10RuntimeLogicTag
    {
        public string Tag;
        public int MapClass;
        public int LogicId;
        public int EntityClass;
        public bool IsRecordBlackbroad;
    }

    [Serializable]
    internal sealed class Map10RuntimeMapTalkEntry
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
    internal sealed class Map10RuntimeTalkCondition
    {
        public string _type;
        public int[] Ids;
        public int Rank;
        public int Profession;
        public int WeaponType;
        public string Op;
    }

    [Serializable]
    internal sealed class Map10RuntimeTalkConditionInfo
    {
        public int ClassId;
        public Map10RuntimeTalkCondition Condition;
    }

    [Serializable]
    internal sealed class Map10RuntimeStoryOption
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
    internal sealed class Map10RuntimeStoryNode
    {
        public int id;
        public string type;
        public int dialogId;
        public string storyName;
        public int count;
        public int compareOption;
        public int compareTargetInt;
        public int entityClassId;
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
        public float playableDuration;
        public bool waitPlayableStop;
        public string[] playableKeys;
        public Map10RuntimeMapLogicState[] mapLogicStates;
        public int[] options;
        public int[] removeEntityClassIds;
        public Map10RuntimeStoryPort[] ports;
    }

    [Serializable]
    internal sealed class Map10RuntimeStoryPort
    {
        public string name;
        public Map10RuntimeStoryConnection[] connections;
    }

    [Serializable]
    internal sealed class Map10RuntimeStoryConnection
    {
        public int nodeId;
        public string port;
    }

    [Serializable]
    internal sealed class Map10RuntimeNativeDialog
    {
        public int Id;
        public int EntryNodeId;
        public Map10RuntimeNativeDialogNode[] Nodes;
    }

    [Serializable]
    internal sealed class Map10RuntimePlayable
    {
        public string AssetGuid;
        public string[] Keys;
        public float Duration;
    }

    [Serializable]
    internal sealed class Map10RuntimeNativeDialogNode
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
    internal sealed class Map10RuntimeMapLogicState
    {
        public string Tag;
        public int State;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogPortraitData
    {
        public string manifest;
        public int mapClassId;
        public string sourceSha256;
        public Map10RuntimeDialogSpeakerStyle speakerStyle;
        public Map10RuntimeDialogPortrait[] portraits;
        public Map10RuntimeDialogIcon[] icons;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogIcon
    {
        public string uri;
        public string resource;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogSpeakerStyle
    {
        public Map10RuntimeDialogSpeakerSlot left;
        public Map10RuntimeDialogSpeakerSlot right;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogSpeakerSlot
    {
        public int direction;
        public float[] light;
        public float[] dark;
        public float colorDuration;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogPortrait
    {
        public string portraitPath;
        public bool needFlipOnRight;
        public Map10RuntimeDialogPortraitLayer main;
        public Map10RuntimeDialogPortraitFace face;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogPortraitLayer
    {
        public string resource;
        public string nodeName;
        public float[] position;
        public float[] nativeSize;
        public float[] pivot;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogPortraitFace
    {
        public string nodeName;
        public float[] position;
        public float[] nativeSize;
        public float[] pivot;
        public float[] commonOffset;
        public Map10RuntimeDialogExpression[] expressions;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogExpression
    {
        public string type;
        public string resource;
        public float[] position;
        public int[] nativeSize;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogRole
    {
        public int RoleId;
        public int EntityClassId;
        public string RoleName;
        public string PortraitPath;
    }

    [Serializable]
    internal sealed class Map10RuntimeStory
    {
        public string key;
        public int maxCount;
        public int entryNodeId;
        public string graphJson;
        public Map10RuntimeStoryNode[] nodes;
        public Map10RuntimeDialogueGroup[] dialogueGroups;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogueGroup
    {
        public int Id;
        public Map10RuntimeDialogueLine[] Lines;
    }

    [Serializable]
    internal sealed class Map10RuntimeDialogueLine
    {
        public int Order;
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
        public int[] completedEntityPlacements = Array.Empty<int>();
        public Map10RuntimeMapLogicState[] mapLogicStates = Array.Empty<Map10RuntimeMapLogicState>();
        public string[] receipts = Array.Empty<string>();
        public string[] nativeDialogAwardReceipts = Array.Empty<string>();
        public Map10RuntimeBattleResult[] battleResults = Array.Empty<Map10RuntimeBattleResult>();
    }

    [Serializable]
    internal sealed class Map10RuntimeStoryCount
    {
        public string key;
        public int count;
    }

    [Serializable]
    internal sealed class Map10RuntimeBattleResult
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
