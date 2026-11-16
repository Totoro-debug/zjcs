using System;
using System.IO;
using System.Linq;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMainlineMonsterRewardCatalog monsterRewardCatalog;
        private OfficialMainlineMonsterLevelCatalog monsterLevelCatalog;

        private bool SettleMainlineMonsterVictory(Map10RuntimeEntity entity, int stageId)
        {
            if (entity == null || entity.TypeId < 101 || entity.TypeId > 104
                || OfficialMainlineStaminaModule.IsStaminaPoint(entity)) return true;
            try
            {
                monsterRewardCatalog ??= OfficialMainlineMonsterRewardCatalog.Load();
                monsterLevelCatalog ??= OfficialMainlineMonsterLevelCatalog.Load();
                var placed = monsterLevelCatalog.Find(mapClassId, entity.PlacementId);
                if (placed == null || placed.EntityClassId != entity.EntityClassId || placed.Region != entity.Region
                    || !rosters.TryGetValue(stageId, out Map10RuntimeRoster roster) || roster.Waves == null)
                    throw new InvalidDataException("Original placed monster level or roster is missing.");
                var level = placed.Resolve(regions.IsPublicRegion(entity.Region), progression.Player.Level, progression.Player.SubRank);
                var region = Interactions.regions.Single(v => v.MapClassId == mapClassId && v.Region == entity.Region);
                var context = new OfflineExploreContext { MapClassId = mapClassId, MapGroupId = session.Definition.MapGroupId,
                    Region = entity.Region, RegionLevel = region.Level, RegionSubRank = region.SubRank,
                    Level = level.level, SubRank = level.subRank, DropItems = region.DropItems };
                int sequence = checked((localSave.battleResults?.Length ?? 0) + 1);
                string battleReceipt = MapReceiptPrefix + slotId + ":battle:" + entity.PlacementId + ":" + stageId + ":" + sequence;
                string winReceipt = MapReceiptPrefix + slotId + ":stage-win:" + stageId + ":" + sequence;
                string receipt = MapReceiptPrefix + slotId + ":monster-reward:" + entity.PlacementId + ":" + stageId + ":" + sequence;
                var monsters = roster.Waves.Where(wave => wave?.Units != null).OrderBy(wave => wave.Index)
                    .SelectMany(wave => wave.Units).Where(monster => monster != null).Select(monster => monster.MonsterId);
                var result = exploration.ExploreService.SettleMainlineMonsterVictory(monsterRewardCatalog, stageId,
                    monsters, level.level, level.subRank, context, receipt, battleReceipt, winReceipt,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out var awards);
                if (result.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidDataException(result.Message);
                if (result.IsApplied)
                {
                    if (MapProfile.HasNativeUi) stamina.ShowStaminaAward(awards);
                    hud.RefreshWorldTopBar();
                }
                return true;
            }
            catch (InvalidDataException error)
            {
                UnityEngine.Debug.LogError("Monster victory reward configuration is invalid: " + error.Message);
                hud.ShowOfficialNotice("怪物奖励配置缺失，无法结算");
                return false;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                battleStepTimer = 2f;
                hud.ShowOfficialNotice("保存失败，请稍后重试");
                return false;
            }
        }
    }
}
