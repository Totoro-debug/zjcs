using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Rules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;

namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerAchievementUiServices
    {
        Rules Rules { get; }
        Rules.Player Player { get; }
        int AchievementPanelShowCount { get; }
        bool AchievementGroupLimited { get; }
        bool ShowAllInfosInSameType(Rules.AchievementType type);
        IEnumerable<Rules.AchievementDataKey> GetAchievementDataKeysBySameType(Rules.AchievementDataKey key);
        IReadOnlyList<Rules.AchievementInfo> GetAchievementInfoListByType(Rules.AchievementDataKey key);
        (long maxValue, long currentValue) GetProgress(Rules.AchievementInfo info);
        string GetNpcName(int npcId);
        object Compact(long value);
        ItemIcon GetIcon(int itemId, Transform parent);
        void RecycleIcon(ItemIcon icon);
        void ShowSystemMessage(string key);
        void Claim(int group, int count, bool receiveSameType, Action success);
    }
}
