using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.OfflineWorld;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private void RefreshRegionProgress()
        {
            var completed = new HashSet<int>(localSave.completedEntityPlacements);
            var privateStates = (localSave.puzzleStates ?? Array.Empty<MapPuzzleEntityState>())
                .ToDictionary(value => value.PlacementId, value => value.State);
            foreach (Map10RuntimeEntity entity in mapEntities.Values) privateStates[entity.PlacementId] = entity.State;
            foreach (Map10RuntimeMapLogicState state in localSave.mapLogicStates)
                foreach (Map10RuntimeLogicTag tag in mapLogicTags.Values.Where(value => value.Tag == state.Tag))
                    privateStates[tag.LogicId] = state.State;
            Dictionary<int, int> points = OfficialMapProgressAdapter.CalculateRegionPoints(
                session.Definition, privateStates, completed);
            regionPoints.Clear();
            foreach (KeyValuePair<int, int> region in points) regionPoints.Add(region.Key, region.Value);
            foreach (KeyValuePair<int, int> region in points)
            {
                if (!regionPointLimits.TryGetValue(region.Key, out int maximum) || maximum < 0 || region.Value > maximum)
                    throw new InvalidDataException("Official Map10 region progress exceeds its pinned maximum: " + region.Key);
                int fixedProgress = OfficialMapProgressCalculator.CalculateRegionProgress(
                    session.Definition, region.Key, region.Value);
                ProgressionResult result = progressionSave.SetMapRegionProgress(mapClassId, region.Key, fixedProgress);
                if (result.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidOperationException(result.Message);
            }
        }

        private void LoadRegionConditions()
        {
            regionPolicy = new OfficialMainlineRegionPolicy(session.Definition, MapProfile);
            regionPointLimits = regionPolicy.regionPointLimits;
        }

        private bool IsRegionAccessible(int regionId) =>
            regionPolicy.IsAccessible(regionId, regionPoints, GetStoryPlayCount);
    }
}
