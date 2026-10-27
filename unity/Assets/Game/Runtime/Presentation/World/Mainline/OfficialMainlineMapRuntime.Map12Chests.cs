using System;
using System.IO;
using System.Linq;
using Zjcs.Offline.Progression;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap12ChestCatalog map12Chests;
        private bool map12ChestProgressPending;
        private float map12ChestRetryAt;

        private bool TryCollectMap12Chest(Map10RuntimeEntity entity)
        {
            if (mapClassId != 12 || entity.EntityClassId < 8200 || entity.EntityClassId > 8203) return false;
            map12Chests ??= OfficialMap12ChestCatalog.Load();
            if (map12Chests.Find(entity.PlacementId) == null) return false;
            if (IsEntityLocked(entity) || entityView.FindEntityObject(entity.PlacementId)?.activeInHierarchy != true) return true;
            try
            {
                ProgressionResult result = exploration.ExploreService.CollectMap12Chest(map12Chests, entity.PlacementId,
                    regions.IsPublicRegion(entity.Region), DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out var awards);
                if (result.Status == ProgressionResultStatus.InvalidState) throw new InvalidDataException(result.Message);
                if (result.IsApplied || progressionSave.Save.World.CompletedEntityTags.Contains(
                    OfficialMap12ChestCatalog.CompletionTag(entity.PlacementId)))
                {
                    if (!localSave.completedEntityPlacements.Contains(entity.PlacementId))
                        localSave.completedEntityPlacements = localSave.completedEntityPlacements.Concat(new[] { entity.PlacementId }).ToArray();
                    entity.State = map12Chests.Find(entity.PlacementId).CompleteState;
                    RefreshEntityActivity();
                    map12ChestProgressPending = true;
                    FlushMap12ChestProgress();
                    hud.RefreshWorldTopBar();
                    if (result.IsApplied) stamina.ShowStaminaAward(awards);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                hud.ShowOfficialNotice("保存失败，请稍后重试");
            }
            return true;
        }

        private void RecoverMap12ChestCompletions()
        {
            if (mapClassId != 12) return;
            map12Chests ??= OfficialMap12ChestCatalog.Load();
            localSave.completedEntityPlacements = localSave.completedEntityPlacements.Concat(map12Chests.Data.chests
                .Where(v => progressionSave.Save.World.CompletedEntityTags.Contains(OfficialMap12ChestCatalog.CompletionTag(v.PlacementId)))
                .Select(v => v.PlacementId)).Distinct().ToArray();
        }

        private void SynchronizeMap12ClaimedChests()
        {
            if (mapClassId != 12 || map12Chests == null || puzzles == null) return;
            foreach (var chest in map12Chests.Data.chests.Where(v => localSave.completedEntityPlacements.Contains(v.PlacementId)))
            {
                if (!mapEntities.TryGetValue(chest.PlacementId, out var entity)) continue;
                entity.State = chest.CompleteState;
                // Native AutoNextState + DestroyOnEnd also releases occupancy.
                // Keep claimed puzzle rewards absent through terminal resets.
                puzzles.SynchronizeExternalEntity(chest.PlacementId, chest.CompleteState, false, entity.Grid);
            }
        }

        private void FlushMap12ChestProgress()
        {
            if (!map12ChestProgressPending || Time.unscaledTime < map12ChestRetryAt) return;
            try
            {
                SaveLocalProgress();
                regions.RefreshRegionProgress();
                map12ChestProgressPending = false;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                // Reward and completion already committed in the profile.
                // The secondary map file and derived progress retry or rebuild
                // from that receipt on cold load; never grant the reward again.
                map12ChestRetryAt = Time.unscaledTime + 2f;
                hud.ShowOfficialNotice("保存失败，请稍后重试");
            }
        }
    }
}
