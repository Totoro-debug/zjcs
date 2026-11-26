using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.GachaUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.GachaUI
{
    public sealed class OfflinePetAchievementPanelSource : IAchievementPanelBlackboardSource, IDisposable
    {
        private readonly OfflinePetGachaService service;
        private readonly OfflinePetScoreRewardServices rewards;
        private readonly OfflineScoreRewardConfiguration configuration;
        public OfflinePetAchievementPanelSource(OfflinePetGachaService service, OfflinePetScoreRewardServices rewards,
            OfflineScoreRewardConfiguration configuration)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (service.Catalog.Gacha.ScoreAchievementType != 66 || configuration.TopAchievementTypes.Contains(66))
                throw new InvalidOperationException("The selected pet score detail must use the original regular achievement path.");
            rewards.OnUpdateBlackboardData += OnBlackboard;
        }
        public int AchievementPanelShowCount => configuration.PanelShowCount;
        public bool AchievementGroupLimited => configuration.IsGroupLimited;
        public IReadOnlyList<AchievementPanelGroup> GetGroups(AchievementPanelKey key, bool receiveSameType)
        {
            if (key.AchievementType != 66 || !string.IsNullOrEmpty(key.JsonParam))
                throw new NotSupportedException("Unbound pet score achievement key.");
            int[] groups = service.Catalog.Achievements.Where(row => row.AchievementType == key.AchievementType)
                .Select(row => row.Group).Distinct().ToArray();
            if (groups.Length != 1 || groups[0] != 16 || key.GroupId != 0 && !groups.Contains(key.GroupId))
                throw new InvalidOperationException("Original pet score achievement groups differ.");
            if (key.GroupId != 0 && !receiveSameType && !configuration.SameTypeAchievementTypes.Contains(key.AchievementType))
                groups = new[] { key.GroupId };
            return groups.Select(Group).ToArray();
        }
        private AchievementPanelGroup Group(int group)
        {
            int count = service.AchievementCount(group);
            rewards.TryGetGotRewardMaxIndex(new ScoreRewardKey(66, group), out int got);
            int left = 0, right = count - 1, can = -1;
            // Targets are the existing official increasing fixed/cyclic score goals.
            while (left <= right)
            {
                int middle = left + (right - left) / 2;
                if (service.GetAchievement(group, middle).IsReady) { can = middle; left = middle + 1; }
                else right = middle - 1;
            }
            return new AchievementPanelGroup(count, got, can, index => Read(group, index));
        }
        private AchievementPanelEntry Read(int group, int index)
        {
            var info = rewards.GetAchievementInfoByGroupAndIndex(group, index);
            return new AchievementPanelEntry {
                Group = group, Index = index, IsInvalid = info.IsInvalid,
                CanGetReward = rewards.IsCanGetAchievementReward(info), AlreadyGotReward = rewards.IsAlreadyGotAchievementReward(info),
                Title = ("ui_achievement_title_PetGachaScoreCount", null),
                Content = ("ui_achievement_content_PetGachaScoreCount", null),
                Progress = rewards.GetProgress(info), ProgressKey = new AchievementPanelBlackboardKey(45, "ALL"), Reward = info.Reward
            };
        }
        public object Compact(long value) => rewards.Compact(value);
        private sealed class Icon : IProfessionRewardIcon
        {
            public readonly ItemIcon Value;
            public Icon(ItemIcon value) { Value = value; }
            public void SetCount(long count) => Value.SetCount(count);
            public void SetClickPopupInfoDialog() => Value.SetClickPopupInfoDialog();
        }
        public IProfessionRewardIcon GetIcon(int item, Transform slot) => new Icon(rewards.GetIcon(item, slot));
        public void RecycleIcon(IProfessionRewardIcon icon) => rewards.RecycleIcon(((Icon)icon).Value);
        public void ShowSystemMessage(string key) => rewards.ShowSystemMessage(key);
        public void Claim(int group, int count, bool same, Action success) => rewards.ClaimPanelGroup(group, count, same, success);
        public event Action<AchievementPanelBlackboardKey> BlackboardChanged;
        private void OnBlackboard(ScoreBlackboardKey key)
            => BlackboardChanged?.Invoke(new AchievementPanelBlackboardKey(key.BlackboardDataType, key.ParamsKey));
        public void Dispose() { rewards.OnUpdateBlackboardData -= OnBlackboard; BlackboardChanged = null; }
    }
}
