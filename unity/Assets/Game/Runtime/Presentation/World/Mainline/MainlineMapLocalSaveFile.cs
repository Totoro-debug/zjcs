using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Offline.World.Map13Navigation;

namespace Zjcs.Presentation.World
{
    // Loading another map must share the active world's validation and backup
    // recovery without entering that world or writing progression.
    internal static class MainlineMapLocalSaveFile
    {
        internal static Map10LocalSave Read(string path, string slotId, int mapClassId, int width, int height)
        {
            SaveReadResult read = LocalJsonSaveFile.Read(path,
                json => IsValid(json, slotId, mapClassId, width, height));
            if (read.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("Map" + mapClassId + " local save is corrupt; the profile remains unchanged.");
            if (read.Json == null) return null;
            Map10LocalSave save = JsonUtility.FromJson<Map10LocalSave>(read.Json);
            Normalize(save);
            return save;
        }

        internal static void Normalize(Map10LocalSave save)
        {
            save.completedStories ??= Array.Empty<string>();
            save.storyPlayCounts ??= Array.Empty<Map10RuntimeStoryCount>();
            save.completedStages ??= Array.Empty<int>();
            save.completedPeriodicityGroups ??= Array.Empty<int>();
            save.completedEntityPlacements ??= Array.Empty<int>();
            save.mapLogicStates ??= Array.Empty<Map10RuntimeMapLogicState>();
            save.puzzleStates ??= Array.Empty<MapPuzzleEntityState>();
            save.receipts ??= Array.Empty<string>();
            save.nativeDialogAwardReceipts ??= Array.Empty<string>();
            save.battleResults ??= Array.Empty<Map10RuntimeBattleResult>();
            save.aloneWorldClassIds ??= Array.Empty<int>();
            save.interactionRangeOverrides ??= Array.Empty<Map10RuntimeInteractionRangeOverride>();
            save.map13WaterStates ??= Array.Empty<Map13WaterState>();
            save.exploredEntityPlacements ??= Array.Empty<int>();
        }

        internal static bool IsValid(string json, string slotId, int mapClassId, int width, int height)
        {
            try
            {
                Map10LocalSave parsed = JsonUtility.FromJson<Map10LocalSave>(json);
                return parsed != null && parsed.schema == 1 && parsed.slotId == slotId
                    && parsed.gridX >= 0 && parsed.gridX < width && parsed.gridY >= 0 && parsed.gridY < height
                    && ValidVisibility(mapClassId == 13 ? parsed.map13GridVisibility : parsed.gridVisibility, width, height)
                    && (parsed.exploredEntityPlacements == null
                        || parsed.exploredEntityPlacements.All(value => value > 0)
                        && parsed.exploredEntityPlacements.Distinct().Count() == parsed.exploredEntityPlacements.Length)
                    && (mapClassId != 13 || parsed.map13WaterStates == null
                        || parsed.map13WaterStates.All(value => value != null && value.PlacementId > 0
                            && value.Grid.X >= 0 && value.Grid.X < 240 && value.Grid.Y >= 0 && value.Grid.Y < 240
                            && value.State >= 0 && value.NextSegment >= 0 && value.GroundOperation >= -1)
                        && parsed.map13WaterStates.Select(value => value.PlacementId).Distinct().Count()
                            == parsed.map13WaterStates.Length)
                    && (parsed.completedStories == null || parsed.completedStories.All(value => value != null))
                    && (parsed.storyPlayCounts == null || parsed.storyPlayCounts.All(value => value != null
                        && !string.IsNullOrEmpty(value.key) && value.count >= 0))
                    && (parsed.completedEntityPlacements == null || parsed.completedEntityPlacements.All(value => value > 0))
                    && (parsed.aloneWorldClassIds == null || parsed.aloneWorldClassIds.All(value => value > 0)
                        && parsed.aloneWorldClassIds.Distinct().Count() == parsed.aloneWorldClassIds.Length)
                    && (parsed.completedPeriodicityGroups == null
                        || parsed.completedPeriodicityGroups.All(value => value > 0))
                    && (parsed.mapLogicStates == null || parsed.mapLogicStates.All(value => value != null
                        && !string.IsNullOrWhiteSpace(value.Tag)))
                    && (parsed.puzzleStates == null || parsed.puzzleStates.All(value => value != null
                        && value.PlacementId > 0 && value.State >= 0 && value.OverlapTimes >= 0)
                        && parsed.puzzleStates.Select(value => value.PlacementId).Distinct().Count() == parsed.puzzleStates.Length)
                    && (!parsed.hasPendingPuzzleOperation || parsed.pendingPuzzleOperation != null
                        && parsed.pendingPuzzleOperation.placementId > 0
                        && parsed.pendingPuzzleOperation.subStateIndex == 0
                        && !string.IsNullOrWhiteSpace(parsed.pendingPuzzleOperation.inventoryReceipt)
                        && parsed.pendingPuzzleOperation.inventoryReceipt.Length <= 256)
                    && (parsed.receipts == null || parsed.receipts.All(value => value != null));
            }
            catch { return false; }
        }
        private static bool ValidVisibility(string encoded, int width, int height)
        {
            if (string.IsNullOrEmpty(encoded)) return true;
            if (encoded.Length > 87384) return false;
            return OfflineMapGridVisibility.IsValidSnapshot(Convert.FromBase64String(encoded), width, height);
        }

    }
}
