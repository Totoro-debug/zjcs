using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.World.NativeCamera;
namespace Zjcs.Official.HomeSceneUiControls
{
    public interface IHomeDirectionFocusController { void SetFocusPositionXForce(float x); }
    public interface IHomeDirectionCameraServices
    {
        UnityEvent<Vector3> ViewPosUpdated { get; }
        UnityEngine.Object ControlBrainIdentity { get; }
        Vector3 CameraFollowPosition { get; }
        Vector3 GetViewPos();
        IHomeDirectionFocusController GetHomeController();
    }
    public interface IHomeDirectionFurniture
    { UnityEngine.Object UnityIdentity { get; } Transform transform { get; } int TipLevel { get; } }
    public interface IHomeDirectionNpcData
    { int Id { get; } NpcType NpcType { get; } int HomePositionIndex { get; } }
    public sealed class HomeDirectionNpcInfo { public string FigureModelUI; }
    public interface IHomeSceneUiServices
    {
        IHomeDirectionCameraServices Camera { get; }
        IDragControllerManager DragControllerManager { get; }
        float DirectionGuideFocusDuration { get; }
        IHomeDirectionFurniture GetFurniture(EFurniture type);
        IDisposable SubscribeHangUpTips(Action<HangUpTipLevelChangeData> callback);
        UnityEvent OnHomeNpcRefresh { get; }
        List<IHomeDirectionNpcData> HomeNpcDatas { get; }
        int AwardRetrieveHomeNpcId { get; }
        Transform GetSpawnPoint(int index);
        HomeDirectionNpcInfo GetNpcInfo(int id);
        IHomeMenuPrefabHandle LoadPrefab(string path);
        GameObject InstantiatePrefab(GameObject prefab,Transform slot);
        void FreezeSkeletonGraphic(GameObject instance);
        HomeViewMode HomeViewMode { get; }
        IDisposable SubscribeUiActive(Action<SetHomeSceneUIActiveData> callback);
    }
}
