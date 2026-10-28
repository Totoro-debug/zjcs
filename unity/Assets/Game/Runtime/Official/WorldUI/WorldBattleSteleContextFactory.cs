using System;
using UnityEngine;
using Zjcs.Official.PlayerPlay;
using Zjcs.Official.World.NativeMap12Labels;

namespace Zjcs.Official.WorldUI
{
    // The fields consumed by BattleSteleWorldStageDialog.DoInit and its original team target.
    public sealed class WorldBattleSteleInitData
    {
        public int StageId, PeriodicityGroupId, EntitySubRank, MonsterLevelAdd, TeamMaxPlayer,
            RecommendCombatRatingIndex;
        public float EntityLevel;
        public int? RegionSubRank;
        public bool SupportTeam;
        public long[] CombatRatingLevelValues;
        public int[] RuleIds;
    }

    // Local value projection of Common.TeamTargetBattleStele; no online serialization dependency.
    public sealed class WorldBattleSteleTeamTarget
    {
        public const int TargetCatalog = 9;
        public readonly int StageId, GroupId;
        public WorldBattleSteleTeamTarget(int stageId, int groupId) { StageId = stageId; GroupId = groupId; }
        public override bool Equals(object obj) => obj is WorldBattleSteleTeamTarget other
            && StageId == other.StageId && GroupId == other.GroupId;
        public override int GetHashCode() => (TargetCatalog, StageId, GroupId).GetHashCode();
    }

    public interface IWorldBattleSteleServices
    {
        int PlayerSubRank { get; }
        long PlayerCombatRating { get; }
        // null is the original MainTeam == null branch; an existing empty team stays zero.
        int? TeamMembers { get; }
        event Action TeamPlayerAdded;
        event Action TeamPlayerRemoved;
        event Action TeamUpdated;
        PlayerPlayStateModule PlayStateModule { get; }
        PlayerPlayStageInfo GetStageInfo(int stageId);
        string Compact(long value);
        void OpenTeam(WorldBattleSteleTeamTarget target);
        void Confirm(string key, Action accepted);
        void Notify(string key);
        void PopulateRewardPreview(Transform slot, int[] ruleIds, int level, int subRank);
    }

    public static class WorldBattleSteleContextFactory
    {
        // The local host supplies original ECS/config snapshots and services. The existing
        // StageDialog still owns title/background/icon binding and its original initialization.
        public static WorldStageUiContext Bind(WorldStageUiContext stage, WorldBattleSteleInitData source,
            Map12LabelData labels, IWorldBattleSteleServices services)
        {
            WorldBattleSteleTeamTarget target = null;
            WorldBattleSteleContext info = null;
            info = new WorldBattleSteleContext
            {
                StageId = source.StageId, SupportTeam = source.SupportTeam,
                RecommendCombatRating = source.CombatRatingLevelValues[source.RecommendCombatRatingIndex],
                Compact = services.Compact,
                // Invoke at the corresponding original DoInit step, rather than taking
                // player/team snapshots before StageDialog initializes its content.
                PrepareRankSuppression = () => info.RankSuppressed = source.RegionSubRank.HasValue &&
                    labels.CalcRankSuppression(services.PlayerSubRank, source.RegionSubRank.Value) > 0,
                PrepareTeamTarget = () => target = new WorldBattleSteleTeamTarget(source.StageId, source.PeriodicityGroupId),
                PrepareCombatRating = () =>
                {
                    int level = Map12LabelFormula.GetCombatRatingLevel(services.PlayerCombatRating,
                        source.CombatRatingLevelValues);
                    labels.TryGetLevelColor(CombatRateingColorType.BattleStele, level, out Color color);
                    info.CombatRatingColor = color;
                    info.CombatRating = services.PlayerCombatRating;
                },
                TeamMemberCount = () => (services.TeamMembers ?? 1, source.TeamMaxPlayer),
                HasTeam = () => services.TeamMembers.HasValue,
                OpenTeam = () => services.OpenTeam(target), Confirm = services.Confirm, Notify = services.Notify,
                PlayFightAction = async (stageId, execute) => await services.PlayStateModule.DoAction(
                    new PlayerPlayFightAction(stageId, services.GetStageInfo), execute),
                SubscribeTeamChanges = refresh =>
                {
                    services.TeamPlayerAdded += refresh;
                    services.TeamPlayerRemoved += refresh;
                    services.TeamUpdated += refresh;
                },
                UnsubscribeTeamChanges = refresh =>
                {
                    services.TeamPlayerAdded -= refresh;
                    services.TeamPlayerRemoved -= refresh;
                    services.TeamUpdated -= refresh;
                }
            };
            stage.BattleStele = info;
            stage.Level = (int)source.EntityLevel;
            stage.ShowRewardPreview = slot => services.PopulateRewardPreview(slot, source.RuleIds,
                (int)source.EntityLevel + source.MonsterLevelAdd, source.EntitySubRank);
            return stage;
        }
    }
}
