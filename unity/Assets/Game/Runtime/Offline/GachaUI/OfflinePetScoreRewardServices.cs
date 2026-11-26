using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.GachaUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.GachaUI
{
    public interface IOfflineScoreRewardPresentation
    {
        ItemIcon GetIcon(int itemId, Transform parent);
        void RecycleIcon(ItemIcon icon);
        void ShowSystemMessage(string key);
        void ShowFailure(PetGachaResult result);
        void OnAchievementCommitted();
        void PopupScoreRewardDialog(string path, bool completed, ScoreRewardKey key, GachaTextKeyArgWrap rule);
    }

    public sealed class OfflinePetScoreRewardServices : IScoreRewardServices, IDisposable
    {
        private readonly OfflinePetGachaService service;
        private readonly IOfflineScoreRewardPresentation presentation;
        private readonly Func<bool> batchLimited;
        private readonly HashSet<int> batchTypes;
        private HashSet<(int group, int index)> claims;
        private long score, draws;
        private bool disposed;
        public OfflinePetScoreRewardServices(OfflinePetGachaService service, IEnumerable<int> batchTypes,
            Func<bool> batchLimited, IOfflineScoreRewardPresentation presentation)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            this.batchLimited = batchLimited ?? throw new ArgumentNullException(nameof(batchLimited));
            this.batchTypes = new HashSet<int>(batchTypes ?? Array.Empty<int>());
            score = service.State.Score; draws = service.State.TotalDrawCount; claims = ReadClaims();
        }
        public bool HasPlayer => !disposed;
        public bool IsScoreRewardBatchGetLimited => batchLimited();
        public bool CanBatchGetScoreReward(ScoreRewardKey key) => batchTypes.Contains(key.AchievementType);
        public IReadOnlyList<ScoreRewardAchievementInfo> GetAchievementInfoListByType(ScoreRewardKey key)
        {
            if (!service.Catalog.Achievements.Any(row => row.Group == key.GroupId && row.AchievementType == key.AchievementType))
                throw new NotSupportedException("Unbound pet score achievement key.");
            return new InfoList(this, key.GroupId);
        }
        public ScoreRewardAchievementInfo GetAchievementInfoByGroupAndIndex(int group, int index)
        {
            var entry = service.GetAchievement(group, index);
            return entry == null ? new ScoreRewardAchievementInfo { IsInvalid = true }
                : new ScoreRewardAchievementInfo { Group = group, Index = index, HasProgressTask = true,
                    ScoreRewardCount = group == 16 ? entry.Target : (long?)null,
                    BlackboardKey = group == 16 ? ScoreBlackboardKey.PetGachaScoreCount : (ScoreBlackboardKey?)null,
                    Reward = entry.Rewards.ToDictionary(row => row.ItemId, row => row.Count) };
        }
        public bool TryGetGotRewardMaxIndex(ScoreRewardKey key, out int index)
        {
            var got = new HashSet<int>(service.State.AchievementClaims.Where(row => row.Group == key.GroupId).Select(row => row.Index));
            index = -1;
            while (got.Contains(index + 1)) index++;
            return got.Count != 0;
        }
        public (long maxValue, long currentValue) GetProgress(ScoreRewardAchievementInfo info)
        { var entry = service.GetAchievement(info.Group, info.Index); return (entry.Target, entry.CurrentValue); }
        public bool IsCanGetAchievementReward(ScoreRewardAchievementInfo info)
        { var entry = service.GetAchievement(info.Group, info.Index); return entry != null && entry.IsReady && !entry.IsClaimed; }
        public bool IsAlreadyGotAchievementReward(ScoreRewardAchievementInfo info) => service.GetAchievement(info.Group, info.Index)?.IsClaimed == true;
        public object Compact(long value) => Zjcs.Official.World.NativeLabelUI.Framework.CultureNumberArg.Compact(value);
        public ItemIcon GetIcon(int id, Transform parent) => presentation.GetIcon(id, parent);
        public void RecycleIcon(ItemIcon icon) => presentation.RecycleIcon(icon);
        public void ShowSystemMessage(string key) => presentation.ShowSystemMessage(key);
        public void PopupScoreRewardDialog(string path, bool completed, ScoreRewardKey key, GachaTextKeyArgWrap rule)
            => presentation.PopupScoreRewardDialog(path, completed, key, rule);
        public void Claim(int group, int count, bool receiveSameType, Action completed)
        {
            if (count != 1 || receiveSameType) throw new NotSupportedException("Only the original score single-claim request is bound here.");
            int type = service.Catalog.Achievements.First(row => row.Group == group).AchievementType;
            TryGetGotRewardMaxIndex(new ScoreRewardKey(type, group), out int previous);
            Finish(service.ClaimAchievement(group, previous + 1, Receipt(), DateTimeOffset.UtcNow.ToUnixTimeSeconds()), completed);
        }
        public void ClaimAll(List<int> groups, Action completed)
        {
            if (groups == null || groups.Count != 1) throw new NotSupportedException("Only the original score single-group batch request is bound here.");
            Finish(service.ClaimAvailableAchievements(groups[0], Receipt(), DateTimeOffset.UtcNow.ToUnixTimeSeconds()), completed);
        }
        public void ClaimPanelGroup(int group, int count, bool receiveSameType, Action success)
        {
            if (group != 16 || count != -1 || service.Catalog.Achievements
                .Where(row => row.AchievementType == service.Catalog.Gacha.ScoreAchievementType).Select(row => row.Group).Distinct().Count() != 1)
                throw new NotSupportedException("Only the original pet score detail group claim is bound here.");
            // This selected type has one group, including ReceiveSameType requests.
            // AchievementPanelItem subscribes to OnSuccess; ScoreRewardUI uses OnComplete.
            Finish(service.ClaimAvailableAchievements(group, Receipt(), DateTimeOffset.UtcNow.ToUnixTimeSeconds()), success, true);
        }
        private void Finish(PetGachaResult result, Action completed, bool successOnly = false)
        {
            if (result.IsApplied) { Synchronize(); presentation.OnAchievementCommitted(); }
            else presentation.ShowFailure(result);
            // Original RPC OnComplete refreshes on either result.
            if (!successOnly || result.IsApplied) completed?.Invoke();
        }
        private static string Receipt() => "pet-score:" + Guid.NewGuid().ToString("N");
        private HashSet<(int, int)> ReadClaims() => new HashSet<(int, int)>(service.State.AchievementClaims.Select(row => (row.Group, row.Index)));
        public void Synchronize()
        {
            if (disposed) return;
            long oldScore = score, oldDraws = draws;
            var oldClaims = claims;
            score = service.State.Score; draws = service.State.TotalDrawCount; claims = ReadClaims();
            if (score != oldScore)
            {
                OnUpdateBlackboardData?.Invoke(ScoreBlackboardKey.PetGachaScoreCount);
                OnUpdateAchievementData?.Invoke(new ScoreRewardKey(service.Catalog.Gacha.ScoreAchievementType, 16));
            }
            if (draws != oldDraws)
            {
                OnUpdateAchievementData?.Invoke(new ScoreRewardKey(service.Catalog.Gacha.CountAchievementType, 15));
                OnOpenChest?.Invoke(service.Catalog.Chest.ClassId, draws - oldDraws);
            }
            foreach (var claim in claims.Except(oldClaims).OrderBy(row => row.group).ThenBy(row => row.index))
                OnRecordAlreadyGotAchievementReward?.Invoke(claim.index);
        }
        public event Action<int> OnRecordAlreadyGotAchievementReward;
        public event Action<ScoreRewardKey> OnUpdateAchievementData;
        public event Action<int, long> OnOpenChest;
        public event Action<ScoreBlackboardKey> OnUpdateBlackboardData;
        public void Dispose()
        { disposed = true; OnRecordAlreadyGotAchievementReward = null; OnUpdateAchievementData = null; OnOpenChest = null; OnUpdateBlackboardData = null; }
        private sealed class InfoList : IReadOnlyList<ScoreRewardAchievementInfo>
        {
            private readonly OfflinePetScoreRewardServices owner;
            private readonly int group;
            public InfoList(OfflinePetScoreRewardServices owner, int group) { this.owner = owner; this.group = group; }
            public int Count => owner.service.AchievementCount(group);
            public ScoreRewardAchievementInfo this[int index] => index >= 0 && index < Count
                ? owner.GetAchievementInfoByGroupAndIndex(group, index) : throw new ArgumentOutOfRangeException(nameof(index));
            public IEnumerator<ScoreRewardAchievementInfo> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
