using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    // Shared DailyDungeonBigMapDialog rendering; callers retain room/exit decisions.
    public sealed class OfficialDungeonRoutePresenter : MonoBehaviour, LoopScrollDataSource
    {
        private DailyDungeonModule module;
        private Action<int> selectRoom;
        private UnityEngine.Events.UnityAction closeHandler;
        private WorldDailyDungeonRoute route;
        private int mapGroupId;
        private bool initialized, choosing;

        public static void Refresh(GameObject root, DailyDungeonModule module,
            Action<int> selectRoom, Action close)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (selectRoom == null) throw new ArgumentNullException(nameof(selectRoom));
            if (close == null) throw new ArgumentNullException(nameof(close));
            var presenter = root.GetComponent<OfficialDungeonRoutePresenter>()
                ?? root.AddComponent<OfficialDungeonRoutePresenter>();
            if (!presenter.initialized)
            {
                OfficialDungeonUiBindingRuntime.Apply(root, "DailyDungeonBigMapDialog");
                presenter.initialized = true;
            }
            Button closeButton = root.GetComponentsInChildren<Button>(true).Single(value => value.name == "CloseBtn");
            if (presenter.closeHandler != null) closeButton.onClick.RemoveListener(presenter.closeHandler);
            presenter.closeHandler = () => close();
            closeButton.onClick.AddListener(presenter.closeHandler);
            presenter.module = module;
            presenter.selectRoom = selectRoom;
            presenter.Render();
        }

        private void Render()
        {
            DailyDungeonRouteHudModel hud = module.BuildRouteHudModel();
            if (hud == null) return;
            DailyDungeonRunSave run = hud.Run;
            DailyDungeonRuntimeMap map = hud.Map;
            if (map == null) throw new InvalidDataException("Original dungeon route map is missing.");
            choosing = hud.IsSelectingNextRoom;
            var window = OfficialDungeonUiBindingRuntime.ConfigureRouteWindow(gameObject, choosing);
            if (window.Grid == null || window.Scroll == null)
                throw new InvalidDataException("Original dungeon route grid/scroll bindings are incomplete.");
            if (route == null || mapGroupId != run.mapGroupId)
            {
                route = new WorldDailyDungeonRoute {
                    ColumnCount = map.columnCount, RowCount = map.rowCount, RoomArray = map.roomArray };
                foreach (int roomId in map.roomArray.Where(value => value > 0).Distinct())
                {
                    DailyDungeonRuntimeRoom room = module.FindRoom(roomId);
                    if (room == null) throw new InvalidDataException("Original route room is missing: " + roomId);
                    route.Rooms.Add(roomId, new WorldDailyDungeonRoute.Room {
                        Id = roomId, Type = room.type, NextRoomIds = room.nextRoomIds ?? Array.Empty<int>() });
                }
                OfficialDungeonUiBindingRuntime.BindRouteStyles(route);
                mapGroupId = run.mapGroupId;
            }
            route.CurrentRoomId = run.currentRoomId;
            route.FinishedRoomIdList = new HashSet<int>(run.clearedRoomIds);
            window.Grid.constraintCount = map.columnCount;
            window.Scroll.dataSource = this;
            window.Scroll.totalCount = map.rowCount * map.columnCount;
            var coordinates = route.GetCoordinatesByRoomId(run.currentRoomId);
            int index = route.GetIndexByCoordinates(coordinates.x + (choosing ? -1 : -2), coordinates.y);
            window.Scroll.RefillCells(Mathf.Clamp(index, 0, window.Scroll.totalCount));
            OfficialDungeonUiBindingRuntime.RefreshRouteGrades(gameObject, window,
                hud.GradeRows, map.rowCount);
        }

        public void ProvideData(Transform cell, int index)
        {
            int roomId = route.GetRoomIdByIndex(index);
            WorldDailyDungeonRoomItem view = OfficialDungeonUiBindingRuntime.BindRouteRoom(cell.gameObject);
            view.RefreshView(roomId, route, choosing);
            cell.name = "Room_" + roomId;
            DailyDungeonRunSave run = module.ActiveRun;
            view.ClickBtn.interactable = run != null && roomId > 0 && !run.privateRouteComplete
                && !run.clearedRoomIds.Contains(roomId) && (roomId == run.currentRoomId
                    || run.clearedRoomIds.Contains(run.currentRoomId)
                    && route.Rooms[run.currentRoomId].NextRoomIds.Contains(roomId));
            view.ClickBtn.onClick.RemoveAllListeners();
            view.ClickBtn.onClick.AddListener(() => {
                if (roomId != route.CurrentRoomId) view.ShowSelectedRoom(roomId, route);
                selectRoom(roomId);
            });
        }
    }
}
