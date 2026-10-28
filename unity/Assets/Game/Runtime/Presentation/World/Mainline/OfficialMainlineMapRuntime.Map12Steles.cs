using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap12SteleCatalog map12Steles;

        private OfficialMap12Stele FindMap12Stele(Map10RuntimeEntity entity)
        {
            if (mapClassId != 12 || entity == null || entity.TypeId != 218) return null;
            map12Steles ??= OfficialMap12SteleCatalog.Load();
            var source = map12Steles.Find(entity.PlacementId)
                ?? throw new InvalidDataException("Original Map12 stele placement is unbound: " + entity.PlacementId);
            if (ResolveEntityStageId(entity) != source.StageId || entity.EntityClassId != source.EntityClassId
                || !entity.PeriodicityGroupIds.SequenceEqual(new[] { source.PeriodicityGroupId }))
                throw new InvalidDataException("Original placed Map12 stele binding differs from its rule catalog.");
            return source;
        }

        private bool SettleMap12SteleBattle(Map10RuntimeEntity entity, int stageId, bool won)
        {
            var source = FindMap12Stele(entity);
            if (source == null || !won) return true;
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var props = progression.EvaluateProps(now).NonBattleProps;
                long addition = props.TryGetValue(29, out long value) ? value : 0;
                var result = exploration.ExploreService.SettleMap12Stele(map12Steles, entity.PlacementId,
                    stageId, won, addition, now, out var awards);
                if (result.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidDataException(result.Message);
                if (result.IsApplied) stamina.ShowStaminaAward(awards);
                hud.RefreshWorldTopBar();
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                // Keep the finished checkpoint and all local attempt flags for
                // the same retry path used by the mountain building battle.
                battleStepTimer = 2f;
                hud.ShowOfficialNotice("保存失败，请稍后重试");
                return false;
            }
        }

        private void RecoverMap12SteleCompletions()
        {
            if (mapClassId != 12) return;
            map12Steles ??= OfficialMap12SteleCatalog.Load();
            foreach (var source in map12Steles.Data.Steles.Where(v => progressionSave.Save.World.CompletedEntityTags
                .Contains(OfficialMap12SteleCatalog.CompletionTag(v.PlacementId))))
            {
                // A profile award commit may precede the separate map-file
                // write. Restore once-only eligibility from the profile first.
                if (!localSave.completedStages.Contains(source.StageId))
                    localSave.completedStages = localSave.completedStages.Concat(new[] { source.StageId }).ToArray();
                if (!localSave.completedPeriodicityGroups.Contains(source.PeriodicityGroupId))
                    localSave.completedPeriodicityGroups = localSave.completedPeriodicityGroups.Concat(new[] { source.PeriodicityGroupId }).ToArray();
                if (!localSave.completedEntityPlacements.Contains(source.PlacementId))
                    localSave.completedEntityPlacements = localSave.completedEntityPlacements.Concat(new[] { source.PlacementId }).ToArray();
            }
        }
    }
}
