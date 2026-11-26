using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.GachaUI
{
    // Local service boundary for the original parser, player, icon manager and RPCs.
    public readonly struct ScoreRewardKey
    {
        public readonly int AchievementType, GroupId;
        public ScoreRewardKey(int type, int group) { AchievementType = type; GroupId = group; }
    }

    public readonly struct ScoreBlackboardKey
    {
        // Original BlackboardDataType.PetGachaScoreCount and default ParamsKey.
        public static readonly ScoreBlackboardKey PetGachaScoreCount = new ScoreBlackboardKey(45, "ALL");
        public readonly int BlackboardDataType;
        public readonly string ParamsKey;
        public ScoreBlackboardKey(int type, string parameters) { BlackboardDataType = type; ParamsKey = parameters; }
    }

    public struct GachaTextKeyArgWrap
    {
        public static readonly GachaTextKeyArgWrap sEmpty = new GachaTextKeyArgWrap("");
        public readonly string Key;
        public object[] Args;
        public bool IsValid => Key != sEmpty.Key || Args != sEmpty.Args;
        public GachaTextKeyArgWrap(string key, params object[] args) { Key = key; Args = args; }
    }

    public sealed class ScoreRewardAchievementInfo
    {
        public int Group, Index;
        public bool IsInvalid, HasProgressTask;
        public long? ScoreRewardCount;
        public ScoreBlackboardKey? BlackboardKey;
        public IReadOnlyDictionary<int, long> Reward;
    }

    public interface IScoreRewardServices
    {
        bool HasPlayer { get; }
        IReadOnlyList<ScoreRewardAchievementInfo> GetAchievementInfoListByType(ScoreRewardKey key);
        ScoreRewardAchievementInfo GetAchievementInfoByGroupAndIndex(int group, int index);
        bool TryGetGotRewardMaxIndex(ScoreRewardKey key, out int index);
        (long maxValue, long currentValue) GetProgress(ScoreRewardAchievementInfo info);
        bool IsCanGetAchievementReward(ScoreRewardAchievementInfo info);
        bool IsAlreadyGotAchievementReward(ScoreRewardAchievementInfo info);
        bool CanBatchGetScoreReward(ScoreRewardKey key);
        bool IsScoreRewardBatchGetLimited { get; }
        object Compact(long value);
        ItemIcon GetIcon(int itemId, Transform parent);
        void RecycleIcon(ItemIcon icon);
        void ShowSystemMessage(string key);
        void Claim(int group, int count, bool receiveSameType, Action completed);
        void ClaimAll(List<int> groupIds, Action completed);
        void PopupScoreRewardDialog(string path, bool rewardCompleted, ScoreRewardKey key, GachaTextKeyArgWrap rule);
        event Action<int> OnRecordAlreadyGotAchievementReward;
        event Action<ScoreRewardKey> OnUpdateAchievementData;
        event Action<int, long> OnOpenChest;
        event Action<ScoreBlackboardKey> OnUpdateBlackboardData;
    }
}
