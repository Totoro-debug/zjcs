// Original reward-preview sorting; see partner-reward-comparer-source-manifest.json.
using System.Collections.Generic;
namespace Zjcs.Official.PartnersUI
{
    public sealed class PartnerRewardItemInfo
    {
        public int ClassId, ItemType, Quality, Rank;
    }
    public sealed class PartnerRewardPreviewItemComparer : PartnerRewardItemComparerBase
    {
        public PartnerRewardPreviewItemComparer(int playerExpItemId) : base(playerExpItemId) { }
            protected override long GetScore(PartnerRewardItemInfo itemInfo, PartnerRewardItemComparerType comparerType)
    {
        if (comparerType == PartnerRewardItemComparerType.Designated)
        {
            if (playerExpItemId == itemInfo.ClassId)
            {
                return 2L;
            }
            if (itemInfo.ItemType == 1)
            {
                return 1L;
            }
            return 0L;
        }
        return base.GetScore(itemInfo, comparerType);
    }
    }
    public class PartnerRewardItemComparerBase
    {
        protected readonly int playerExpItemId;
        protected PartnerRewardItemComparerBase(int playerExpItemId) { this.playerExpItemId = playerExpItemId; }
            protected virtual List<(PartnerRewardItemComparerType comparerType, PartnerRewardComparerOrderType orderType)> RewardItemComparerPriorityList { get; set; } = new List<(PartnerRewardItemComparerType, PartnerRewardComparerOrderType)>
    {
        (PartnerRewardItemComparerType.Designated, PartnerRewardComparerOrderType.Desc),
        (PartnerRewardItemComparerType.Quality, PartnerRewardComparerOrderType.Desc),
        (PartnerRewardItemComparerType.ClassId, PartnerRewardComparerOrderType.Asc)
    };
            public virtual int RewardItemComparer(PartnerRewardItemInfo x, PartnerRewardItemInfo y)
    {
        return Comparer(x, y, RewardItemComparerPriorityList);
    }

    public static int ComparerResult(long x, long y, PartnerRewardComparerOrderType type)
    {
        switch (type)
        {
        case PartnerRewardComparerOrderType.Asc:
            return ConvertLong2IntForSort(x - y);
        case PartnerRewardComparerOrderType.Desc:
            return ConvertLong2IntForSort(y - x);
        default:
        {
            return 0;
        }
        }
    }

    protected virtual long GetScore(PartnerRewardItemInfo itemInfo, PartnerRewardItemComparerType comparerType)
    {
        return comparerType switch
        {
            PartnerRewardItemComparerType.Designated => (playerExpItemId == itemInfo.ClassId) ? 1 : 0,
            PartnerRewardItemComparerType.Type => (long)itemInfo.ItemType,
            PartnerRewardItemComparerType.ClassId => itemInfo.ClassId,
            PartnerRewardItemComparerType.Quality => (long)itemInfo.Quality,
            PartnerRewardItemComparerType.Rank => itemInfo.Rank,
            _ => 0L,
        };
    }

    protected int Comparer(PartnerRewardItemInfo x, PartnerRewardItemInfo y, List<(PartnerRewardItemComparerType comparerType, PartnerRewardComparerOrderType orderType)> priorityList)
    {
        foreach (var priority in priorityList)
        {
            long score = GetScore(x, priority.comparerType);
            long score2 = GetScore(y, priority.comparerType);
            int num = ComparerResult(score, score2, priority.orderType);
            if (num != 0)
            {
                return num;
            }
        }
        return 0;
    }

    public static int ConvertLong2IntForSort(long longValue)
    {
        if (longValue == 0L)
        {
            return 0;
        }
        if (longValue <= 0)
        {
            return -1;
        }
        return 1;
    }
    }
    public enum PartnerRewardItemComparerType
{
    Type,
    ClassId,
    Rank,
    Level,
    Used,
    Score,
    CombatRating,
    Quality,
    Owned,
    Star,
    Active,
    Count,
    SubRank,
    CanUpgrade,
    CanWakeUp,
    CanActive,
    CanMerge,
    Progress,
    Profession,
    Element,
    Map,
    Region,
    Prop,
    Suit,
    RedDot,
    Designated,
    Friendship,
    ActivateTime,
    MeetStatus,
    NpcRoleType,
    DialogueStatus,
    DialogueTime,
    GiftFriendship,
    GiftPreference,
    GetTime,
    Locked,
    CanEquiped,
    UpdateTime,
    RankIsMatch,
    EquipCabinetScore,
    IsCommonEquipPiece,
    SkillRank,
    Decompose,
    TimeLimit,
    NpcCollected,
    CollectedTime
}

public enum PartnerRewardComparerOrderType
{
    Asc,
    Desc
}

}
