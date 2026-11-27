using System;
using System.Collections.Generic;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeTipDots
{
    public enum TreasureRedDotEnum
    {
        Active = 0,
        Merge = 1,
        LevelUp = 2,
        Upgrade = 3,
        Wakeup = 4,
        None = 1000
    }

    public interface IHomeDynamicTipDotNodeManager : IDynamicTipDotNodeManager { }

    public interface IHomeTipDotNode
    {
        IHomeTipDotServices Services { get; set; }
    }

    public sealed class HomeTipFastHangUpInfo
    {
        public int FastHangUpId;
        public List<int> HangUpIdList;
        public Dictionary<int, int> FastHangUpItemDic;
    }

    public sealed class HomeTipItemInfo
    {
        public int ClassId;
    }

    public sealed class HomeTipItemData
    {
        public HomeTipItemInfo Info;
        public int Id => Info?.ClassId ?? 0;
    }

    public sealed class HomeTipHangUpInfo
    {
        public int Id;
        public bool CanLevelUp;
    }

    public interface IHomeTipDotPlayer
    {
        bool CanFreeFastHangUp(int fastHangUpId);
        long GetItemAmount(int itemId);
        bool HasUnlockedHangUp(int hangUpId);
        bool TryGetUnlockSingleHangUpData(int hangUpId, out int level);
        int GetHangUpMaxLevel();
        bool HasItems(Dictionary<int, long> costs);
        bool TreasureRedDot(ElementType elementType, List<TreasureRedDotEnum> tipTypes, bool autoMergeWhenLevelUp, bool awakeAutoMergeEnable);
        bool AnyTreasureRedDot(List<TreasureRedDotEnum> redDots, bool autoMergeWhenLevelUp, bool awakeAutoMergeEnable);
        bool TreasurePieceRedDot(ElementType elementType);
        bool AwakeAutoMergeEnable { get; }
        event Action<int> OnHangUpUnlock;
        event Action<int, int> OnHangUpLevelUp;
        event Action<int> OnFastHangUp;
        event Action<int> OnLevelUpdate;
        event Action OnGoddessUpdate;
        event Action<long> OnDebugTimeOffset;
        event Action<HomeTipItemData, long, object> OnItemCountUpdate;
        event Action<HomeTipItemData, bool> OnTreasureRefresh;
    }

    public interface IHomeTipDotServices
    {
        IHomeTipDotPlayer Player { get; }
        bool AutoMergeWhenLevelUp { get; }
        bool IsItemEffectTreasurePromote(HomeTipItemData itemData);
        IDisposable SubscribeTreasureSettingUpdate(Action callback);
        bool TryGetFastHangUpInfo(EFurniture furniture, out List<HomeTipFastHangUpInfo> list);
        bool IsFastHangUpItem(int itemId);
        EFurniture GetHangUpBindFurniture(int hangUpId);
        bool CheckHangUpUnlockMet(EFurniture furniture);
        bool TryGetHangUpInfo(int hangUpId, out HomeTipHangUpInfo info);
        bool TryGetHangUpInfos(EFurniture furniture, out List<HomeTipHangUpInfo> list);
        int GetHangUpUpgradeMaxLevel(int hangUpId);
        Dictionary<int, long> GetHangUpUpgradeCosts(int hangUpId, int nextLevel);
        IDisposable SubscribeEnterNewDay(Action callback);
    }
}
