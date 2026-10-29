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
        private void LoadLocalProgress()
        {
            SaveReadResult read = LocalJsonSaveFile.Read(mapSavePath, ValidLocalSave);
            if (read.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("Map" + mapClassId + " local save is corrupt; the profile remains unchanged.");
            localSaveCreated = read.Json == null;
            localSave = read.Json == null
                ? new Map10LocalSave { slotId = slotId, gridX = 24, gridY = 23 }
                : JsonUtility.FromJson<Map10LocalSave>(read.Json);
            if (localSave == null || localSave.slotId != slotId)
                throw new InvalidDataException("Map" + mapClassId + " save belongs to another profile slot.");
            localSave.completedStories ??= Array.Empty<string>();
            localSave.storyPlayCounts ??= Array.Empty<Map10RuntimeStoryCount>();
            localSave.completedStages ??= Array.Empty<int>();
            localSave.completedPeriodicityGroups ??= Array.Empty<int>();
            localSave.completedEntityPlacements ??= Array.Empty<int>();
            localSave.mapLogicStates ??= Array.Empty<Map10RuntimeMapLogicState>();
            localSave.puzzleStates ??= Array.Empty<MapPuzzleEntityState>();
            localSave.receipts ??= Array.Empty<string>();
            localSave.nativeDialogAwardReceipts ??= Array.Empty<string>();
            localSave.battleResults ??= Array.Empty<Map10RuntimeBattleResult>();
            localSave.aloneWorldClassIds ??= Array.Empty<int>();
            RecoverMap12ChestCompletions();
            RecoverMap12SteleCompletions();
        }

        private void SaveLocalProgress()
        {
            if (localSave == null || string.IsNullOrEmpty(mapSavePath)) return;
            localSave.gridX = navigation.currentGrid.X;
            localSave.gridY = navigation.currentGrid.Y;
            if (puzzles != null) localSave.puzzleStates = puzzles.Save();
            SnapshotMap13Visibility();
            LocalJsonSaveFile.Write(mapSavePath, JsonUtility.ToJson(localSave, true), ValidLocalSave);
        }

        private bool ValidLocalSave(string json)
        {
            try
            {
                Map10LocalSave parsed = JsonUtility.FromJson<Map10LocalSave>(json);
                int width = session == null ? (mapClassId == 11 ? 256 : 60)
                    : Mathf.CeilToInt(session.Definition.Navigation.Width);
                int height = session == null ? (mapClassId == 11 ? 256 : 60)
                    : Mathf.CeilToInt(session.Definition.Navigation.Height);
                return parsed != null && parsed.schema == 1 && parsed.slotId == slotId
                    && parsed.gridX >= 0 && parsed.gridX < width && parsed.gridY >= 0 && parsed.gridY < height
                    && (mapClassId != 13 || ValidMap13Visibility(parsed.map13GridVisibility))
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
    }
}
