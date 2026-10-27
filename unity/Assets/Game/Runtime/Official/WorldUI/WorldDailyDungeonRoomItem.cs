using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // Port of DailyDungeonRoomItem.RefreshView / RefreshRoomRoad. The original
    // prefab references and ScriptableObject sprites are supplied by the binder.
    public sealed class WorldDailyDungeonRoomItem : MonoBehaviour
    {
        public sealed class Road
        {
            public GameObject Object;
            public Image Image;
            public int OffsetType;
            public Animation Animation;
        }
        public Image SelectImage, FrameImage, IconImage, RewardIconImage;
        public GameObject ShowContentObj;
        public Road[] RoomRoadDatas;
        public Button ClickBtn;
        public Animation Animation, VoteAnimation;
        public bool IsEmptyRoom => ShowContentObj == null || !ShowContentObj.activeSelf;

        public void RefreshView(int roomId, WorldDailyDungeonRoute route, bool isShowNextRoom)
        {
            StopAllRoomAnimation();
            if (Animation != null) Animation.Stop();
            if (VoteAnimation != null) VoteAnimation.Stop();
            foreach (Road road in RoomRoadDatas) road.Object.SetActive(false);
            SelectImage.gameObject.SetActive(false);
            if (!route.Rooms.TryGetValue(roomId, out var room) || room.Type == 0)
            {
                ShowContentObj.SetActive(false);
                return;
            }
            WorldDailyDungeonRoute.RoomStyle style = route.Styles[room.Type];
            bool undiscovered = roomId != route.CurrentRoomId && !route.FinishedRoomIdList.Contains(roomId);
            if (route.IsRoomVisual(roomId, isShowNextRoom))
            {
                IconImage.gameObject.SetActive(true);
                IconImage.sprite = undiscovered ? style.NoPassIcon : style.PassIcon;
                RewardIconImage.gameObject.SetActive(style.Reward != null);
                if (style.Reward != null) RewardIconImage.sprite = style.Reward;
            }
            else
            {
                IconImage.gameObject.SetActive(false);
                RewardIconImage.gameObject.SetActive(false);
            }
            FrameImage.sprite = undiscovered ? style.NoPassFrame : style.PassFrame;
            SelectImage.sprite = style.Select;
            ShowContentObj.SetActive(true);
            foreach (var preRoad in route.GetRoomPreRoadStatusList(roomId))
            {
                int offset = route.GetRoomDirectionOffsetType(roomId, preRoad.preRoomId);
                Road road = Array.Find(RoomRoadDatas, value => value.OffsetType == offset);
                if (road == null) throw new InvalidOperationException("Official dungeon incoming road is missing: " + offset);
                road.Object.SetActive(true);
                road.Image.color = route.RoadColors[preRoad.roadStatusType];
                if (preRoad.roadStatusType == 0) road.Object.transform.SetAsLastSibling();
            }
        }
        // DailyDungeonBigMapRoomItem.ShowSelectedRoom: apply selection before
        // dispatching the local room request, preserving the original order.
        public void ShowSelectedRoom(int roomId, WorldDailyDungeonRoute route)
        {
            SelectImage.gameObject.SetActive(true);
            StopAllRoomAnimation();
            int offset = route.GetRoomDirectionOffsetType(roomId, route.CurrentRoomId);
            Road road = Array.Find(RoomRoadDatas, value => value.OffsetType == offset);
            if (road == null) return;
            FrameImage.sprite = route.Styles[route.Rooms[roomId].Type].PassFrame;
            road.Image.color = route.RoadColors[2];
            road.Object.transform.SetAsLastSibling();
            if (road.Animation != null) road.Animation.Play();
            if (Animation != null) Animation.Play();
        }
        public void StopAllRoomAnimation()
        {
            foreach (Road road in RoomRoadDatas)
                if (road.Animation != null) road.Animation.Stop();
        }
    }
}
