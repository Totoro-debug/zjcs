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
                if (host.entityView == null || !host.entityView.TrySelectScreenPosition(press.Value))
                    TryMoveToPointer(press.Value);
            }

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
                if (host.mapClassId == 11 && record.TypeId == 202 && host.statues.IsGoddessActivated(record))
                {
                    host.statues.ShowGoddessDialog(record);
                    return;
                }
                StartMovement(record.Grid, record);
            }

            public void StartMovement(MapGridPoint target, Map10RuntimeEntity interact)
            {
                if (movementPath != null || host.battleController != null) return;
                List<MapGridPoint> path = host.session.FindPath(new OfficialMapPathRequest
                {
                    Start = currentGrid,
                    End = target,
                    PreciseTarget = interact == null,
                    // The official world character (3001/3002) occupies its own grid cell.
                    BodyOffsets = new[] { new MapGridPoint(0, 0) },
                    PreferDistance = interact == null ? 0 : 1,
                    CanEnd = interact == null ? null : point => Manhattan(point, target) <= 1
                });
                if (path == null)
                {
                    movementCompletion = null;
                    return;
                }
                if (path.Count == 0)
                {
                    if (interact != null && Manhattan(currentGrid, target) <= 1) host.Interact(interact);
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
                host.SaveLocalProgress();
                Map10RuntimeEntity selected = pendingInteraction;
                pendingInteraction = null;
                if (selected != null) host.Interact(selected);
                Action complete = movementCompletion;
                movementCompletion = null;
                complete?.Invoke();
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
