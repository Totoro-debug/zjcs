using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World;
using Zjcs.Official.World;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private MapPuzzleRule[] puzzleRules;
        private OfflineMapPuzzleSession puzzles;
        private HashSet<int> puzzleEntityIds;
        private bool puzzleMutationActive;
        private bool puzzleRegionProgressPending;
        private float puzzleRegionProgressRetryAt;

        private void InitializePuzzles()
        {
            if (!MapProfile.HasNativeUi) return;
            if (puzzleRules == null || (mapClassId == 11 && puzzleRules.Count(value => value.TerminalLockIds.Length > 0) != 7)
                || (mapClassId == 12 && puzzleRules.Count(value => value.TerminalPuzzleType > 0) != 11))
                throw new InvalidDataException("Map" + mapClassId + " official puzzle bindings are incomplete.");
            puzzleEntityIds = new HashSet<int>(puzzleRules.Where(value => value.TerminalPuzzleType > 0
                || value.HasPushResult || value.HasOverlapDetector || value.LockType > 1
                || value.EntityClassId == 6503).Select(value => value.PlacementId));
            if (mapClassId == 11) puzzleEntityIds.UnionWith(puzzleRules.Select(value => value.PlacementId));
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
                        if (mapEntities.ContainsKey(id)) added |= puzzleEntityIds.Add(id);
            } while (added);
            CreatePuzzleSession(localSave.puzzleStates);
            if (mapClassId == 12) entityView.ContainsGridInput = (id, position) =>
            {
                OfficialMapNavigationView.WorldToGrid(session.Definition.Navigation,
                    new OfficialMapVector3 { X = position.x, Y = position.y }, out int x, out int y);
                return puzzles.Contains(id) && puzzles.ContainsClickableBody(id, new MapGridPoint(x, y));
            };
            RecoverPuzzleOperation();
            SynchronizePuzzleEntities();
        }

        private void CreatePuzzleSession(MapPuzzleEntityState[] saved)
        {
            puzzles = new OfflineMapPuzzleSession(puzzleRules, saved,
                id => mapEntities.TryGetValue(id, out Map10RuntimeEntity entity) ? entity.State : 0,
                IsEntityGroupComplete);
            puzzles.RefreshLocks();
        }

        private bool IsPuzzleEntity(int id) => puzzles != null && puzzleEntityIds.Contains(id);

        private bool ProcessPuzzleTerminalAction(int actionType)
        {
            if (storyContextEntity == null || puzzles == null
                || !puzzles.Contains(storyContextEntity.PlacementId)
                || (storyContextEntity.EntityClassId != 6501 && storyContextEntity.EntityClassId != 6502)
                || actionType < 0 || actionType > (storyContextEntity.EntityClassId == 6501 ? 1 : 0)
                || puzzles.State(storyContextEntity.PlacementId) != 0) return false;

            if (localSave.hasPendingPuzzleOperation) return SavePuzzleMutation(ApplyPendingPuzzleOperation);

            int id = storyContextEntity.PlacementId;
            if (actionType == 1)
            {
                return SavePuzzleMutation(() =>
                {
                    puzzles.RunStage(id, 1);
                    SynchronizePuzzleEntities();
                    if (mapClassId == 12) MoveBesidePuzzleTerminal(id);
                });
            }
            if (StoryItemCount(325) < 1) return false;

            // The map intent is durable before inventory changes. The inventory
            // receipt lets a cold load finish the same solve after an interruption.
            var operation = new Map11PendingPuzzleOperation
            {
                placementId = id, subStateIndex = 0,
                inventoryReceipt = MapReceiptPrefix + slotId + ":puzzle:" + id + ":" + Guid.NewGuid().ToString("N")
            };
            if (!SavePuzzleMutation(() =>
            {
                localSave.pendingPuzzleOperation = operation;
                localSave.hasPendingPuzzleOperation = true;
            })) return false;
            ProgressionResult result;
            try
            {
                result = progressionSave.ApplyInventoryTransaction(operation.inventoryReceipt, save =>
                {
                    if (!OfflineInventoryOperations.Consume(save.Player, 325, 1,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied)
                        throw new InvalidOperationException("The confirmed puzzle ticket could not be consumed.");
                });
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                SavePuzzleMutation(() => { localSave.pendingPuzzleOperation = null; localSave.hasPendingPuzzleOperation = false; });
                hud.ShowOfficialNotice("保存失败，请稍后重试");
                return false;
            }
            if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged)
            {
                SavePuzzleMutation(() => { localSave.pendingPuzzleOperation = null; localSave.hasPendingPuzzleOperation = false; });
                return false;
            }
            return SavePuzzleMutation(ApplyPendingPuzzleOperation);
        }

        private void RecoverPuzzleOperation()
        {
            if (!localSave.hasPendingPuzzleOperation) return;
            SavePuzzleMutation(ApplyPendingPuzzleOperation);
        }

        private void ApplyPendingPuzzleOperation()
        {
            Map11PendingPuzzleOperation operation = localSave.pendingPuzzleOperation;
            if (!localSave.hasPendingPuzzleOperation) return;
            if (operation == null || !puzzles.Contains(operation.placementId) || operation.subStateIndex != 0
                || !puzzleRules.Any(value => value.PlacementId == operation.placementId
                    && (value.TerminalPuzzleType > 0 || value.TerminalLockIds.Length > 0))
                || string.IsNullOrWhiteSpace(operation.inventoryReceipt))
                throw new InvalidDataException("The saved puzzle operation is invalid.");
            if (progressionSave.Save.Player.AppliedInventoryEventIds.Contains(operation.inventoryReceipt))
                puzzles.RunStage(operation.placementId, operation.subStateIndex);
            localSave.pendingPuzzleOperation = null;
            localSave.hasPendingPuzzleOperation = false;
            SynchronizePuzzleEntities();
        }

        private void SynchronizePuzzleEntities()
        {
            bool changed = false;
            foreach (MapPuzzleRule rule in puzzles.Rules.Where(value => puzzleEntityIds.Contains(value.PlacementId)))
            {
                changed |= mapEntities[rule.PlacementId].State != puzzles.State(rule.PlacementId);
                mapEntities[rule.PlacementId].State = puzzles.State(rule.PlacementId);
                if (mapClassId == 12)
                {
                    mapEntities[rule.PlacementId].Grid = puzzles.Position(rule.PlacementId);
                    entityView.SetEntityGrid(rule.PlacementId, puzzles.Position(rule.PlacementId));
                }
            }
            if (changed)
            {
                regions.RefreshRegionProgress(!puzzleMutationActive);
                if (puzzleMutationActive) puzzleRegionProgressPending = true;
            }
            RefreshEntityActivity();
        }

        private bool TryPushPuzzleEntity(Map10RuntimeEntity entity)
        {
            if (mapClassId != 12 || !IsPuzzleEntity(entity.PlacementId)
                || !puzzleRules.First(value => value.PlacementId == entity.PlacementId).HasPushResult) return false;
            SavePuzzleMutation(() =>
            {
                if (!puzzles.TryPush(entity.PlacementId, navigation.currentGrid, session.Definition.Navigation,
                    out MapGridPoint after)) return;
                navigation.SetAvatarPosition(after);
                SynchronizePuzzleEntities();
            });
            return true;
        }

        private bool SavePuzzleMutation(Action mutation)
        {
            string before = JsonUtility.ToJson(localSave);
            MapPuzzleEntityState[] states = puzzles.Save();
            MapGridPoint player = navigation.currentGrid;
            bool progressPendingBefore = puzzleRegionProgressPending;
            puzzleMutationActive = true;
            try { mutation(); SaveLocalProgress(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                localSave = JsonUtility.FromJson<Map10LocalSave>(before);
                CreatePuzzleSession(states);
                navigation.SetAvatarPosition(player);
                SynchronizePuzzleEntities();
                puzzleRegionProgressPending = progressPendingBefore;
                if (hud.ui != null) hud.ShowOfficialNotice("保存失败，请稍后重试");
                return false;
            }
            finally { puzzleMutationActive = false; }
            FlushPuzzleRegionProgress();
            return true;
        }

        private void FlushPuzzleRegionProgress()
        {
            if (!puzzleRegionProgressPending || puzzleMutationActive
                || Time.unscaledTime < puzzleRegionProgressRetryAt) return;
            try
            {
                // The map is authoritative. Never publish derived completion
                // before its entity states and player position are durable.
                regions.RefreshRegionProgress();
                puzzleRegionProgressPending = false;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                // Keep the committed puzzle. A retry or cold load rebuilds the
                // profile's monotonic region totals from those saved states.
                puzzleRegionProgressRetryAt = Time.unscaledTime + 2f;
                if (hud.ui != null) hud.ShowOfficialNotice("保存失败，请稍后重试");
            }
        }

        private void MoveBesidePuzzleTerminal(int id)
        {
            MapGridPoint terminal = mapEntities[id].Grid;
            MapGridPoint[] offsets = { new MapGridPoint(0, -1), new MapGridPoint(-1, 0),
                new MapGridPoint(0, 1), new MapGridPoint(1, 0) };
            MapGridPoint[] candidates = offsets.Select(value => new MapGridPoint(terminal.X + value.X, terminal.Y + value.Y))
                .Where(session.Movement.IsFit).OrderBy(value => Math.Abs(value.X - navigation.currentGrid.X)
                    + Math.Abs(value.Y - navigation.currentGrid.Y)).ToArray();
            if (candidates.Length == 0) throw new InvalidDataException("Puzzle terminal has no adjacent standable cell: " + id);
            navigation.SetAvatarPosition(candidates[0]);
        }

        private void SynchronizePuzzleGround()
        {
            if (mapClassId != 12 || puzzles == null) return;
            foreach (MapPuzzleRule rule in puzzleRules.Where(value => !puzzleEntityIds.Contains(value.PlacementId)))
                puzzles.SynchronizeExternalEntity(rule.PlacementId, mapEntities[rule.PlacementId].State,
                    entityView.FindEntityObject(rule.PlacementId).activeSelf, mapEntities[rule.PlacementId].Grid);
            session.SetMovement(puzzles.CreateGround(session.Definition.Navigation, session.Definition.CharacterMover.MoveMask));
        }
    }
}
