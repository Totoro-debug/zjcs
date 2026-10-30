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
        private sealed partial class OfficialMainlineNavigationModule
        {
            // Desktop adapters may supply a screen-space press. All sources pass
            // through UI occlusion, official entity picking and the same pathfinder.
            public Func<Vector2?> PointerPressProvider { private get; set; }

            public void ProcessMapPointerPress()
            {
                Vector2? press = PointerPressProvider != null ? PointerPressProvider()
                    : Input.GetMouseButtonDown(0) ? (Vector2?)Input.mousePosition : null;
                if (!press.HasValue) return;
                if (EventSystem.current != null)
                {
                    if (PointerPressProvider == null && EventSystem.current.IsPointerOverGameObject()) return;
                    if (PointerPressProvider != null)
                    {
                        var hits = new List<RaycastResult>();
                        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = press.Value }, hits);
                        if (hits.Count > 0) return;
                    }
                }
                if (storyMovementLocked) return;
                if (host.entityView == null || !host.entityView.TrySelectScreenPosition(press.Value))
                    TryMoveToPointer(press.Value);
            }

            public void LockPlayerMovementForStory() => storyMovementLocked = true;

            public void UnlockPlayerMovementForStory() => storyMovementLocked = false;

            private void TryMoveToPointer(Vector2 pointer)
            {
                Vector3 screen = pointer;
                screen.z = Mathf.Abs(host.worldCamera.transform.position.z);
                Vector3 world = host.worldCamera.ScreenToWorldPoint(screen);
                OfficialMapNavigationView.WorldToGrid(host.session.Definition.Navigation,
                    new OfficialMapVector3 { X = world.x, Y = world.y, Z = 0f }, out int x, out int y);
                StartMovement(new MapGridPoint(x, y), null);
            }

            public void OnEntitySelected(OfficialMap10EntityInfo entity)
            {
                if (entity == null || entity.Grid == null || entity.Grid.Length != 2 || host.battleController != null)
                    return;
                if (!host.mapEntities.TryGetValue(entity.PlacementId, out Map10RuntimeEntity record)) return;
                if (host.puzzles != null && host.puzzles.Contains(record.PlacementId)
                    && host.puzzleRules.First(value => value.PlacementId == record.PlacementId).HasOverlapDetector)
                {
                    StartMovement(record.Grid, null);
                    return;
                }
                if (host.MapProfile.HasNativeUi && record.TypeId == 202 && host.statues.IsGoddessActivated(record))
                {
                    host.statues.ShowGoddessDialog(record);
                    return;
                }
                StartMovement(record.Grid, record);
            }

            public void ClickLogicalGrid(MapGridPoint target)
            {
                bool previousStoryTransfer = storyTransferActive;
                storyTransferActive = true;
                try
                {
                    Vector3 screen = host.worldCamera.WorldToScreenPoint(GridToWorld(target));
                    if (host.entityView == null || !host.entityView.TrySelectScreenPosition(new Vector2(screen.x, screen.y)))
                        StartMovement(target, null);
                }
                finally
                {
                    if (movementPath == null) storyTransferActive = previousStoryTransfer;
                }
            }

            public void StartMovement(MapGridPoint target, Map10RuntimeEntity interact)
            {
                if (movementPath != null || host.battleController != null) return;
                bool originalRange = (host.mapClassId == 12 || host.mapClassId == 13) && interact != null;
                Func<MapGridPoint, bool> canInteract = interact == null ? null
                    : originalRange ? point => InOriginalInteractionRange(interact, point)
                    : point => Manhattan(point, target) <= 1;
                if (originalRange && canInteract(currentGrid))
                {
                    host.Interact(interact);
                    return;
                }
                List<MapGridPoint> path = host.session.FindPath(new OfficialMapPathRequest
                {
                    Start = currentGrid,
                    End = target,
                    PreciseTarget = interact == null || originalRange,
                    // The official world character (3001/3002) occupies its own grid cell.
                    BodyOffsets = new[] { new MapGridPoint(0, 0) },
                    PreferDistance = interact == null || originalRange ? 0 : 1,
                    CanEnd = canInteract
                });
                if (path == null)
                {
                    movementCompletion = null;
                    return;
                }
                if (path.Count == 0)
                {
                    if (canInteract != null && canInteract(currentGrid)) host.Interact(interact);
                    if (currentGrid.Equals(target))
                    {
                        Action complete = movementCompletion;
                        movementCompletion = null;
                        complete?.Invoke();
                    }
                    return;
                }
                pendingInteraction = interact;
                movementPath = path;
                movementIndex = path[0].Equals(currentGrid) ? 1 : 0;
                if (movementIndex >= movementPath.Count) FinishMovement();
            }

            public void StartStoryInteraction(Map10RuntimeEntity interact, System.Action complete)
            {
                if (interact == null)
                {
                    complete?.Invoke();
                    return;
                }
                bool completed = false;
                System.Action wrapped = () =>
                {
                    completed = true;
                    complete?.Invoke();
                };
                movementCompletion = wrapped;
                StartMovement(interact.Grid, interact);
                // StartMovement invokes Interact synchronously when the player is already
                // in the original range, and leaves no path for FinishMovement to call.
                if (!completed && movementPath == null)
                {
                    movementCompletion = null;
                    // A missing local path is the same terminal boundary as the
                    // server's failed movement request; the story must not remain hung.
                    wrapped();
                }
            }

            public void StopMovement()
            {
                movementPath = null;
                movementIndex = 0;
                pendingInteraction = null;
                movementCompletion = null;
                storyTransferActive = false;
            }

            public void StartStoryTransfer(Map10RuntimeEntity target, int direction, Action complete)
            {
                if (target == null || movementPath != null || host.battleController != null)
                {
                    complete?.Invoke();
                    return;
                }
                MapGridPoint destination = target.Grid;
                List<MapGridPoint> path;
                if (currentGrid.Equals(destination))
                {
                    MapGridPoint[] directions =
                    {
                        new MapGridPoint(0, -1), new MapGridPoint(-1, 0),
                        new MapGridPoint(0, 1), new MapGridPoint(1, 0)
                    };
                    MapGridPoint? adjacent = directions.Select(offset => destination + offset)
                        .Where(host.session.Movement.IsFit).Select(point => (MapGridPoint?)point).FirstOrDefault();
                    if (!adjacent.HasValue)
                    {
                        complete?.Invoke();
                        return;
                    }
                    path = new List<MapGridPoint> { currentGrid, adjacent.Value };
                }
                else path = host.session.FindPath(new OfficialMapPathRequest
                {
                    Start = currentGrid,
                    End = destination,
                    PreferDistance = 1,
                    PreciseTarget = true,
                    BodyOffsets = new[] { new MapGridPoint(0, 0) }
                });
                if (path == null || path.Count < 2)
                {
                    complete?.Invoke();
                    return;
                }
                Vector3 scale = host.avatar.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (direction == 1 ? -1f : 1f);
                host.avatar.transform.localScale = scale;
                movementPath = path;
                movementIndex = path[0].Equals(currentGrid) ? 1 : 0;
                movementCompletion = complete;
                storyTransferActive = true;
                if (movementIndex >= movementPath.Count) FinishMovement();
            }

            public void AdvanceMovement(float deltaTime)
            {
                if (movementPath == null || movementIndex >= movementPath.Count)
                {
                    FinishMovement();
                    return;
                }
                MapGridPoint point = movementPath[movementIndex];
                Vector3 target = GridToWorld(point);
                host.avatar.transform.position = Vector3.MoveTowards(host.avatar.transform.position, target,
                    MovementSpeed * deltaTime);
                if (Vector2.Distance(host.avatar.transform.position, target) < 0.025f)
                {
                    MapGridPoint previous = currentGrid;
                    if (host.mapClassId == 12 || host.mapClassId == 13)
                    {
                        if (!host.SavePuzzleMutation(() =>
                        {
                            currentGrid = point;
                            host.puzzles.CharacterMoved(currentGrid, previous);
                            host.SynchronizePuzzleEntities();
                        }))
                        {
                            movementPath = null;
                            movementIndex = 0;
                            pendingInteraction = null;
                            movementCompletion = null;
                            return;
                        }
                        movementIndex++;
                        if (movementIndex >= movementPath.Count) FinishMovement();
                        return;
                    }
                    currentGrid = point;
                    if (host.puzzles != null)
                    {
                        host.puzzles.CharacterMoved(currentGrid, previous);
                        host.SynchronizePuzzleEntities();
                    }
                    movementIndex++;
                    host.SaveLocalProgress();
                    if (movementIndex >= movementPath.Count) FinishMovement();
                }
            }

            private void FinishMovement()
            {
                if (movementPath != null && movementPath.Count > 0)
                    currentGrid = movementPath[movementPath.Count - 1];
                movementPath = null;
                movementIndex = 0;
                if (host.mapClassId != 12 && host.mapClassId != 13) host.SaveLocalProgress();
                Map10RuntimeEntity selected = pendingInteraction;
                pendingInteraction = null;
                if (selected != null && ((host.mapClassId != 12 && host.mapClassId != 13) || InOriginalInteractionRange(selected, currentGrid)))
                    host.Interact(selected);
                Action complete = movementCompletion;
                movementCompletion = null;
                storyTransferActive = false;
                complete?.Invoke();
            }

            private bool InOriginalInteractionRange(Map10RuntimeEntity entity, MapGridPoint point)
            {
                if (host.mapClassId == 13 && host.HasStoryInteractionRangeOverride(entity, point)) return true;
                // WorldStageComponent.InInteractiveRange uses the configured offsets from GridTransform.Pos.
                IReadOnlyList<OfficialStageInteractionOffset> range = host.puzzles != null && host.puzzles.Contains(entity.PlacementId)
                    ? host.puzzles.InteractionRange(entity.PlacementId)
                    : host.puzzleRules.FirstOrDefault(value => value.PlacementId == entity.PlacementId)?.Stage?.InteractiveRange;
                return range != null && range.Any(offset =>
                    point.X == entity.Grid.X + offset.X && point.Y == entity.Grid.Y + offset.Y);
            }

            private void StartMovementToCurrentGrid()
            {
                SetAvatarPosition(currentGrid);
                host.localSave.gridX = currentGrid.X;
                host.localSave.gridY = currentGrid.Y;
            }

            public void SetAvatarPosition(MapGridPoint point)
            {
                currentGrid = point;
                if (host.avatar != null) host.avatar.transform.position = GridToWorld(point);
                if (host.localSave != null)
                {
                    host.localSave.gridX = point.X;
                    host.localSave.gridY = point.Y;
                }
            }

            public Vector3 GridToWorld(MapGridPoint point)
            {
                OfficialMapVector3 world = OfficialMapNavigationView.GridToWorld(host.session.Definition.Navigation, point.X, point.Y);
                return new Vector3(world.X, world.Y, world.Z - 0.1f);
            }
        }
    }
}
