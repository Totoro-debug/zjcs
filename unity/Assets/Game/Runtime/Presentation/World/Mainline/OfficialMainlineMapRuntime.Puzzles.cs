using System;
using System.IO;
using System.Linq;
using Zjcs.Offline.World;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private MapPuzzleRule[] puzzleRules;
        private OfflineMapPuzzleSession puzzles;

        private void InitializePuzzles()
        {
            if (mapClassId != 11) return;
            if (puzzleRules == null || puzzleRules.Count(value => value.TerminalLockIds.Length > 0) != 7)
                throw new InvalidDataException("Map11 official puzzle bindings are incomplete.");
            puzzles = new OfflineMapPuzzleSession(puzzleRules, localSave.puzzleStates,
                id => mapEntities.TryGetValue(id, out Map10RuntimeEntity entity) ? entity.State : 0,
                IsEntityGroupComplete);
            puzzles.RefreshLocks();
            RecoverPuzzleOperation();
            SynchronizePuzzleEntities();
        }

        private bool ProcessPuzzleTerminalAction(int actionType)
        {
            if (storyContextEntity == null || puzzles == null
                || !puzzles.Contains(storyContextEntity.PlacementId)
                || (storyContextEntity.EntityClassId != 6501 && storyContextEntity.EntityClassId != 6502)
                || actionType < 0 || actionType > (storyContextEntity.EntityClassId == 6501 ? 1 : 0)
                || puzzles.State(storyContextEntity.PlacementId) != 0) return false;

            int id = storyContextEntity.PlacementId;
            if (actionType == 1)
            {
                puzzles.RunStage(id, 1);
                SynchronizePuzzleEntities();
                SaveLocalProgress();
                return true;
            }
            if (StoryItemCount(325) < 1) return false;

            // The map intent is durable before inventory changes. The inventory
            // receipt lets a cold load finish the same solve after an interruption.
            var operation = new Map11PendingPuzzleOperation
            {
                placementId = id, subStateIndex = 0,
                inventoryReceipt = MapReceiptPrefix + slotId + ":puzzle:" + id + ":" + Guid.NewGuid().ToString("N")
            };
            localSave.pendingPuzzleOperation = operation;
            localSave.hasPendingPuzzleOperation = true;
            SaveLocalProgress();
            ProgressionResult result = progressionSave.ApplyInventoryTransaction(operation.inventoryReceipt, save =>
            {
                if (!OfflineInventoryOperations.Consume(save.Player, 325, 1,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied)
                    throw new InvalidOperationException("The confirmed puzzle ticket could not be consumed.");
            });
            if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged)
                throw new InvalidOperationException(result.Message);
            RecoverPuzzleOperation();
            SynchronizePuzzleEntities();
            return true;
        }

        private void RecoverPuzzleOperation()
        {
            Map11PendingPuzzleOperation operation = localSave.pendingPuzzleOperation;
            if (!localSave.hasPendingPuzzleOperation) return;
            if (operation == null || !puzzles.Contains(operation.placementId) || operation.subStateIndex != 0
                || !puzzleRules.Any(value => value.PlacementId == operation.placementId && value.TerminalLockIds.Length > 0)
                || string.IsNullOrWhiteSpace(operation.inventoryReceipt))
                throw new InvalidDataException("The saved puzzle operation is invalid.");
            if (progressionSave.Save.Player.AppliedInventoryEventIds.Contains(operation.inventoryReceipt))
                puzzles.RunStage(operation.placementId, operation.subStateIndex);
            localSave.pendingPuzzleOperation = null;
            localSave.hasPendingPuzzleOperation = false;
            SaveLocalProgress();
        }

        private void SynchronizePuzzleEntities()
        {
            bool changed = false;
            foreach (MapPuzzleRule rule in puzzles.Rules)
            {
                changed |= mapEntities[rule.PlacementId].State != puzzles.State(rule.PlacementId);
                mapEntities[rule.PlacementId].State = puzzles.State(rule.PlacementId);
            }
            if (changed) RefreshRegionProgress();
            RefreshEntityActivity();
        }
    }
}
