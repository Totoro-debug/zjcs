// Ported from the verified official source; only dependency/serialization adapters differ.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.ProfessionUI
{
    // Read-only projections of the original parser/player operations used by
    // these nodes. Local providers must preserve the original event payloads.
    public sealed class ProfessionTipDotBaseInfo { public int Profession, BadgeClassId; }
    public sealed class ProfessionTipDotLevelCost { public Dictionary<int, long> UpgradeCost; }
    public sealed class ProfessionTipDotItemInfo { public int ItemType; }
    public sealed class ProfessionTipDotItem { public ProfessionTipDotItemInfo Info; }
    public sealed class ProfessionTipDotAchievementKey { public int AchievementType, GroupId; }
    public interface IProfessionTipDotPlayer
    {
        int Profession { get; }
        bool StartChangeProfession { get; }
        event Action<int> OnProfessionPromote;
        event Action<int, int> OnProfessionChange;
        event Action<int, int> OnProfessionUpgrade;
        event Action<object> OnTaskAdd;
        event Action<ProfessionTipDotItem, long, object> OnItemCountUpdate;
        event Action<int, int> OnSkillRankRewardUpdate;
        event Action<ProfessionTipDotItem, int, int> OnSkillRankUp;
        event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch;
        event Action<int> OnRecordAlreadyGotAchievementRewardByGroupId;
        event Action OnRecordAlreadyGotAchievementRewardBatch;
        event Action<ProfessionTipDotAchievementKey> OnUpdateAchievementData;
        bool IsProfessionUnlocked(int profession);
        int GetProfessionLevel(int profession);
        long GetItemAmount(int itemId);
        bool IsFunctionLimit(int function);
        bool IsAnyCanGetAchievementReward(ProfessionTipDotAchievementKey key);
        bool IsAnyCanGetAchievementReward(List<ProfessionTipDotAchievementKey> keys);
    }
    public interface IProfessionTipDotRules
    {
        bool AnyPromotionRequirementCompleted();
        bool HasAvailableHigherPromotion();
        bool IsPromotionRequirementCompleted(int profession);
        bool IsPromotableAndInNewRank(int profession);
        List<int> GetAllProfessions();
        ProfessionTipDotBaseInfo GetProfessionBaseInfo(int profession);
        bool TryGetLevelInfo(int profession, int level, out ProfessionTipDotLevelCost info);
        long CalcAwardBadge(ProfessionTipDotBaseInfo info, IProfessionTipDotPlayer player);
        List<int> GetAllAchievementGroupIds();
        List<int> AchievementTypeListForShowAllInfosInSameType { get; }
        List<ProfessionTipDotAchievementKey> GetAchievementDataKeysBySameType(ProfessionTipDotAchievementKey key);
        ProfessionTipDotAchievementKey CreateAchievementDataKey(int type, string jsonParam);
        void RegisterAchievementGroupLimitCondition(Action onChanged);
        void DeregisterAchievementGroupLimitCondition(Action onChanged);
    }
}
