using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Rules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;

namespace Zjcs.Offline.PartnersUI
{
    public interface IOfflinePartnerAchievementPresentation
    {
        ItemIcon GetIcon(int itemId, Transform parent);
        void RecycleIcon(ItemIcon icon);
        string Localize(string key);
        void ShowSystemMessage(string key);
        void ShowFailure(ProgressionResult result);
        void OnAchievementCommitted();
    }

    public sealed class OfflinePartnerAchievementUiServices : IPartnerAchievementUiServices
    {
        private readonly OfflinePartnerAchievementService achievements;
        private readonly IOfflinePartnerAchievementPresentation presentation;
        private readonly Func<bool> groupLimited;
        private readonly HashSet<Rules.AchievementType> allInfos;
        private readonly Dictionary<int, string> npcNames;
        public OfflinePartnerAchievementUiServices(OfflinePartnerAchievementService achievements,
            OfficialPartnerCatalog catalog, Func<bool> achievementGroupLimited, IOfflinePartnerAchievementPresentation presentation)
        {
            this.achievements = achievements ?? throw new ArgumentNullException(nameof(achievements));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            groupLimited = achievementGroupLimited ?? throw new ArgumentNullException(nameof(achievementGroupLimited));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var settings = catalog.Rows("game_settings").Single();
            AchievementPanelShowCount = Convert.ToInt32(settings["AchievementPanelShowCount"]);
            allInfos = new HashSet<Rules.AchievementType>(((List<object>)settings["AchievementTypeListForShowAllInfosInSameType"])
                .Select(value => (Rules.AchievementType)Convert.ToInt32(value)));
            npcNames = catalog.Rows("npc").ToDictionary(row => Convert.ToInt32(row["Id"]), row => (string)row["NameKey"]);
        }
        public Rules Rules => achievements.Rules;
        public Rules.Player Player => achievements.ReadCurrentPlayer();
        public int AchievementPanelShowCount { get; }
        public bool AchievementGroupLimited => groupLimited();
        public bool ShowAllInfosInSameType(Rules.AchievementType type) => allInfos.Contains(type);
        public IEnumerable<Rules.AchievementDataKey> GetAchievementDataKeysBySameType(Rules.AchievementDataKey key)
            => Rules.GroupIds.Select(Rules.GetAchievementGroupToKey).Where(value => value.AchievementType == key.AchievementType);
        public IReadOnlyList<Rules.AchievementInfo> GetAchievementInfoListByType(Rules.AchievementDataKey key)
            => Rules.GetAchievementInfoListByGroupId(Rules.GetAchievementKeyToGroup(key));
        public (long maxValue, long currentValue) GetProgress(Rules.AchievementInfo info)
            => info.AchievementType == Rules.AchievementType.NpcFriendshipLevel
                ? new Rules.AchievementTaskNpcFriendshipLevel(Rules).GetProgressValue(info.Id, Player)
                : new Rules.AchievementTaskStarGodNpcFriendshipLevel(Rules).GetProgressValue(info.Id, Player);
        public string GetNpcName(int id) => presentation.Localize(npcNames[id]);
        public object Compact(long value) => Zjcs.Official.World.NativeLabelUI.Framework.CultureNumberArg.Compact(value);
        public ItemIcon GetIcon(int id, Transform parent) => presentation.GetIcon(id, parent);
        public void RecycleIcon(ItemIcon icon) => presentation.RecycleIcon(icon);
        public void ShowSystemMessage(string key) => presentation.ShowSystemMessage(key);
        public void Claim(int group, int count, bool receiveSameType, Action success)
        {
            var result = achievements.Claim(group, count, receiveSameType);
            if (result.Transaction.IsApplied)
            {
                presentation.OnAchievementCommitted();
                success?.Invoke();
            }
            else presentation.ShowFailure(result.Transaction);
        }
    }
}
