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
        private sealed partial class OfficialMainlineRegionsModule
        {
            public void RefreshRegionProgress(bool persist = true)
            {
                Dictionary<int, int> points = MainlineMapEntityRestore.RegionPoints(
                    host.session.Definition, host.localSave, host.mapEntities.Values, host.mapLogicTags);
                regionPoints.Clear();
                foreach (KeyValuePair<int, int> region in points) regionPoints.Add(region.Key, region.Value);
                foreach (KeyValuePair<int, int> region in points)
                {
                    if (!regionPointLimits.TryGetValue(region.Key, out int maximum) || maximum < 0 || region.Value > maximum)
                        throw new InvalidDataException("Official Map10 region progress exceeds its pinned maximum: " + region.Key);
                    int fixedProgress = OfficialMapProgressCalculator.CalculateRegionProgress(
                        host.session.Definition, region.Key, region.Value);
                    if (!persist) continue;
                    ProgressionResult result = host.progressionSave.SetMapRegionProgress(host.mapClassId, region.Key, fixedProgress);
                    if (result.Status == ProgressionResultStatus.InvalidState)
                        throw new InvalidOperationException(result.Message);
                }
            }

            public void LoadRegionConditions()
            {
                regionPolicy = new OfficialMainlineRegionPolicy(host.session.Definition, host.MapProfile);
                regionPointLimits = regionPolicy.regionPointLimits;
            }

            public bool IsRegionAccessible(int regionId) =>
                regionPolicy.IsAccessible(regionId, regionPoints, host.GetStoryPlayCount, host.progressionSave.StoryStatus,
                    tag => host.progressionSave.Save.World.CompletedEntityTags.Contains(tag)
                        || host.localSave.mapLogicStates.Any(value => value != null && value.Tag == tag && value.State < 0));
        }
    }
}
