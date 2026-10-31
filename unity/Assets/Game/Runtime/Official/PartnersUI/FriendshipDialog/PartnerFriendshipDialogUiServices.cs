using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    // Read-only projections of the fields consumed by the original dialog.
    public sealed class PartnerFriendshipDialogNpc
    {
        public PartnerFriendshipNpcInfo Properties;
        public IReadOnlyDictionary<int, double> GiftPreferenceDict;
        public bool IsStarGodNpc;
        public object Name;
        // Present exactly when CanMeetCondition is NpcFriendshipLevelCondition.
        public int? AssistFriendshipLevel;
    }
    public sealed class PartnerFriendshipDialogLevel
    {
        public int SubRankLimit;
    }
    public interface IPartnerFriendshipDialog
    {
        void Init(PartnerFriendshipDialogNpc npcInfo);
    }
    public interface IPartnerFriendshipDialogUiServices : IPartnerFriendshipUiServices
    {
        int SubRank { get; }
        IReadOnlyDictionary<int, PartnerFriendshipDialogLevel> GetFriendshipLevels(int npcId);
        int CompareRewardItems(int firstId, int secondId);
        ItemIcon BorrowGiftIcon(int itemId, Transform slot);
        void RecycleGiftIcon(ItemIcon icon);
    }
}
