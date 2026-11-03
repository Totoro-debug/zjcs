using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Official.World;
using Zjcs.Presentation.OfflineWorld;
using Entity = Zjcs.Presentation.World.OfficialMainlineMapRuntime.Map10RuntimeEntity;

namespace Zjcs.Presentation.World
{
    internal static class MainlineMapEntityRestore
    {
        internal static OfficialMap10EntityView.EntityRecord ExploreRecord(OfficialWorldExplorationCatalog catalog,
            OfflineExploreEntity entity)
        {
            var template = catalog.Data.entities.Single(value => value.entityClassId == entity.EntityClassId);
            return new OfficialMap10EntityView.EntityRecord
            {
                placementId = entity.PlacementId, entityClassId = entity.EntityClassId,
                typeId = template.parameters.WorldStageType, grid = new[] { entity.X, entity.Y },
                region = entity.Region, area = entity.Area, state = entity.State, active = true
            };
        }

        internal static Dictionary<int, int> RegionPoints(OfficialMapDefinition definition, Map10LocalSave local,
            IEnumerable<Entity> entities, IReadOnlyDictionary<int, Map10RuntimeLogicTag> tags)
        {
            var completed = new HashSet<int>(local.completedEntityPlacements);
            var states = local.puzzleStates.ToDictionary(value => value.PlacementId, value => value.State);
            foreach (Entity entity in entities) states[entity.PlacementId] = entity.State;
            foreach (var state in local.mapLogicStates)
                foreach (var tag in tags.Values.Where(value => value.Tag == state.Tag)) states[tag.LogicId] = state.State;
            return OfficialMapProgressAdapter.CalculateRegionPoints(definition, states, completed);
        }

        internal static HashSet<int> SelectPuzzles(int mapClassId, MapPuzzleRule[] puzzleRules, ISet<int> entities)
        {
            var puzzleEntityIds = new HashSet<int>(puzzleRules.Where(value => value.TerminalPuzzleType > 0
                || value.HasPushResult || value.HasOverlapDetector || value.LockType > 1
                || value.EntityClassId == 6503).Select(value => value.PlacementId));
            if (mapClassId == 11) puzzleEntityIds.UnionWith(puzzleRules.Select(value => value.PlacementId));
            if (mapClassId == 12) puzzleEntityIds.UnionWith(new[] { 503, 3501 });
            bool added;
            do
            {
                added = false;
                foreach (MapPuzzleRule rule in puzzleRules.Where(value => puzzleEntityIds.Contains(value.PlacementId)).ToArray())
                    foreach (int id in rule.TerminalLockIds.Concat(rule.TerminalRemoveIds).Concat(rule.TerminalBoxIds)
                        .Concat(rule.PushBoxDetectorIds).Concat(rule.PushBoxIds).Concat(rule.SpawnIds)
                        .Concat(rule.LockConditions.Select(value => value.PlacementId))
                        .Concat(rule.Notify.SelectMany(value => value.Infos).Select(value => value.LoEntId))
                        .Concat(rule.OtherStates.SelectMany(value => value.StageDatas).Select(value => value.LoEntId)))
                        if (entities.Contains(id)) added |= puzzleEntityIds.Add(id);
            } while (added);
            return puzzleEntityIds;
        }

        internal static void RecoverChests(OfficialMap12ChestCatalog map12Chests, Map10LocalSave localSave, OfflineMainlineSave profile)
        {
            localSave.completedEntityPlacements = localSave.completedEntityPlacements.Concat(map12Chests.Data.chests
                .Where(v => profile.World.CompletedEntityTags.Contains(OfficialMap12ChestCatalog.CompletionTag(v.PlacementId)))
                .Select(v => v.PlacementId)).Distinct().ToArray();
        }

        internal static void RecoverSteles(OfficialMap12SteleCatalog map12Steles, Map10LocalSave localSave, OfflineMainlineSave profile)
        {
            foreach (var source in map12Steles.Data.Steles.Where(v => profile.World.CompletedEntityTags
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

        internal static void SynchronizeChests(OfficialMap12ChestCatalog map12Chests, Map10LocalSave localSave,
            IDictionary<int, Entity> mapEntities, OfflineMapPuzzleSession puzzles)
        {
            foreach (var chest in map12Chests.Data.chests.Where(v => localSave.completedEntityPlacements.Contains(v.PlacementId)))
            {
                if (!mapEntities.TryGetValue(chest.PlacementId, out var entity)) continue;
                entity.State = chest.CompleteState;
                // Native AutoNextState + DestroyOnEnd also releases occupancy.
                // Keep claimed puzzle rewards absent through terminal resets.
                puzzles.SynchronizeExternalEntity(chest.PlacementId, chest.CompleteState, false, entity.Grid);
            }
        }

        internal static bool WitchNextState(Map11RuntimeWitchInfo info, Map10LocalSave local, int placement) =>
            info.IsNextState || local.battleResults.Any(value => value.placementId == placement && value.stageId == 20001 && value.won);
    }
}
