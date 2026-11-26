using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public sealed class AchievementPanelKey
    {
        public readonly int AchievementType, GroupId;
        public readonly string JsonParam;
        public AchievementPanelKey(int type, int group = 0, string json = null)
        { AchievementType = type; GroupId = group; JsonParam = json; }
    }

    public readonly struct AchievementPanelBlackboardKey
    {
        public readonly int Type;
        public readonly string Params;
        public AchievementPanelBlackboardKey(int type, string parameters) { Type = type; Params = parameters; }
        public bool Matches(AchievementPanelBlackboardKey other) => Type == other.Type && Params == other.Params;
    }

    // Snapshot of the original regular achievement info/player/task inputs. A
    // group's reader supplies live player values without materializing cyclic lists.
    public sealed class AchievementPanelEntry
    {
        public int Group, Index;
        public bool IsInvalid, CanGetReward, AlreadyGotReward;
        public (string key, object[] args) Title, Content;
        public (long maxValue, long currentValue)? Progress;
        public AchievementPanelBlackboardKey? ProgressKey;
        public IReadOnlyDictionary<int, long> Reward;
    }

    public sealed class AchievementPanelGroup
    {
        private readonly Func<int, AchievementPanelEntry> read;
        public readonly int Count, GotRewardMaxIndex, CanRewardsMaxIndex;
        public AchievementPanelGroup(int count, int gotMax, int canMax, Func<int, AchievementPanelEntry> read)
        {
            Count = count; GotRewardMaxIndex = gotMax; CanRewardsMaxIndex = canMax;
            this.read = read ?? throw new ArgumentNullException(nameof(read));
        }
        public AchievementPanelEntry this[int index] => index >= 0 && index < Count
            ? read(index) : throw new ArgumentOutOfRangeException(nameof(index));
    }

    // Shared regular (non-ranking) AchievementPanel path. Adapters resolve the
    // original data keys, same-type setting, player records, tasks and local claims.
    public interface IAchievementPanelSource
    {
        int AchievementPanelShowCount { get; }
        bool AchievementGroupLimited { get; }
        IReadOnlyList<AchievementPanelGroup> GetGroups(AchievementPanelKey key, bool receiveSameType);
        object Compact(long value);
        IProfessionRewardIcon GetIcon(int itemId, Transform slot);
        void RecycleIcon(IProfessionRewardIcon icon);
        void ShowSystemMessage(string key);
        void Claim(int group, int count, bool receiveSameType, Action success);
    }

    public interface IAchievementPanelRefreshSource : IAchievementPanelSource
    {
        event Action Changed;
    }

    public interface IAchievementPanelBlackboardSource : IAchievementPanelSource
    {
        event Action<AchievementPanelBlackboardKey> BlackboardChanged;
    }
}
