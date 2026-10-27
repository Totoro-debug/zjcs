using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // DailyDungeonMapGroupInfoParser: coordinates, visibility and incoming roads.
    // The offline adapter supplies the original CSV rows and local start-data fields.
    public sealed class WorldDailyDungeonRoute
    {
        public sealed class Room
        {
            public int Id, Type;
            public int[] NextRoomIds;
        }
        public sealed class RoomStyle
        {
            public int Type;
            public Sprite PassIcon, NoPassIcon, PassFrame, NoPassFrame, Select, Reward;
        }
        public int ColumnCount, RowCount, CurrentRoomId;
        public int[] RoomArray;
        public HashSet<int> FinishedRoomIdList;
        public readonly Dictionary<int, Room> Rooms = new Dictionary<int, Room>();
        public readonly Dictionary<int, RoomStyle> Styles = new Dictionary<int, RoomStyle>();
        public readonly Dictionary<int, Color> RoadColors = new Dictionary<int, Color>();

        public int GetIndexByCoordinates(int x, int y) => x * ColumnCount + y;
        public (int x, int y) GetCoordinatesByRoomId(int roomId)
        {
            int index = Array.IndexOf(RoomArray, roomId);
            return (index / ColumnCount, index % ColumnCount);
        }
        public int GetRoomIdByIndex(int index)
        {
            int x = index / ColumnCount, y = index % ColumnCount;
            if (x < 0 || x >= RowCount || y < 0 || y >= ColumnCount) return 0;
            return RoomArray[index];
        }
        public bool IsRoomVisual(int targetRoomId, bool isShowNextRoom)
        {
            if (FinishedRoomIdList.Contains(targetRoomId)) return true;
            if (CurrentRoomId == targetRoomId) return true;
            Room target = Rooms[targetRoomId];
            if (target.Type == 5 || target.Type == 6) return true;
            if (isShowNextRoom)
            {
                Room current = Rooms[CurrentRoomId];
                if (current.NextRoomIds.Contains(targetRoomId)) return true;
                foreach (int next in current.NextRoomIds)
                    if (Rooms[next].NextRoomIds.Contains(targetRoomId)) return true;
            }
            return false;
        }
        public List<(int preRoomId, int roadStatusType)> GetRoomPreRoadStatusList(int targetRoomId)
        {
            var list = new List<(int, int)>();
            bool finished = FinishedRoomIdList.Contains(targetRoomId);
            foreach (Room room in Rooms.Values.Where(room => room.NextRoomIds.Contains(targetRoomId)))
            {
                if (!finished && CurrentRoomId != targetRoomId) list.Add((room.Id, 1));
                else if (FinishedRoomIdList.Contains(room.Id)) list.Add((room.Id, 0));
                else list.Add((room.Id, 1));
            }
            return list;
        }
        public int GetRoomDirectionOffsetType(int currentRoomId, int targetRoomId)
        {
            if (currentRoomId == targetRoomId) return 0;
            var current = GetCoordinatesByRoomId(currentRoomId);
            var target = GetCoordinatesByRoomId(targetRoomId);
            if (current.y - target.y == 1) return current.x - target.x == 1 ? 5 : 2;
            if (current.y - target.y == 0) return current.x - target.x == 1 ? 4 : 1;
            if (current.y - target.y == -1) return current.x - target.x == 1 ? 6 : 3;
            return 0;
        }
    }

}
