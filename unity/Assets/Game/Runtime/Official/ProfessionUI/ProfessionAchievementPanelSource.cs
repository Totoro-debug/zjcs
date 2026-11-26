using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;

namespace Zjcs.Official.ProfessionUI
{
    // Retain the existing profession interface while sharing the original list
    // and item controllers with other regular achievement types.
    internal sealed class ProfessionAchievementPanelSource : IAchievementPanelRefreshSource
    {
        private readonly IProfessionAchievementData data;
        public ProfessionAchievementPanelSource(IProfessionAchievementData data)
        { this.data = data ?? throw new ArgumentNullException(nameof(data)); }
        public int AchievementPanelShowCount => data.AchievementPanelShowCount;
        public bool AchievementGroupLimited => data.AchievementGroupLimited;
        public event Action Changed { add => data.Changed += value; remove => data.Changed -= value; }
        public IReadOnlyList<AchievementPanelGroup> GetGroups(AchievementPanelKey key, bool receiveSameType)
        {
            if (key.AchievementType != 56 || key.GroupId != 0 && key.GroupId != 10 || !string.IsNullOrEmpty(key.JsonParam))
                throw new InvalidOperationException("Unbound original profession achievement key.");
            var rows = data.Rules.Milestones;
            int got = rows.Where(row => data.Recorded.Contains(row.EventId)).Select(row => row.Index).DefaultIfEmpty(0).Max();
            int can = rows.Where(row => data.TotalLevel >= row.RequiredLevel).Select(row => row.Index).DefaultIfEmpty(0).Max();
            return new[] { new AchievementPanelGroup(rows.Count, got, can, Read) };
        }
        internal AchievementPanelEntry Read(int index)
        {
            var row = data.Rules.Milestones[index];
            return new AchievementPanelEntry {
                Group = 10, Index = row.Index, CanGetReward = data.CanGetReward(row), AlreadyGotReward = data.AlreadyGotReward(row),
                Title = ("ui_achievement_title_TotalProfessionLevel", null),
                Content = ("ui_achievement_content_TotalProfessionLevel", null),
                Progress = (row.RequiredLevel, data.TotalLevel), Reward = row.Reward
            };
        }
        public object Compact(long value) => OfficialPropFormat.Compact(value);
        public IProfessionRewardIcon GetIcon(int itemId, Transform slot) => data.GetIcon(itemId, slot);
        public void RecycleIcon(IProfessionRewardIcon icon) => data.RecycleIcon(icon);
        public void ShowSystemMessage(string key) => data.ShowSystemMessage(key);
        public void Claim(int group, int count, bool receiveSameType, Action success)
        { if (data.ClaimGroup(group, count, receiveSameType)) success?.Invoke(); }
    }
}
