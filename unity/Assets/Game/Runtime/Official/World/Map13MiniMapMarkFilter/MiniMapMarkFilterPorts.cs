using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.World.Map13MiniMap
{
    public sealed class MiniMapFilterMarkInfo
    { public string Name,Image,Description;public bool ShowOnFilterEntry;public List<int> ShowOnMapGroupId,ShowOnMapClassId; }
    public interface IMiniMapFilterMapContext { MiniMapResourcePointManager ResourcePointManager { get; } }
    public interface IMiniMapFilterPersistentState
    { MiniMapMarkUIFilterSetting State { get; } UniTask WriteStateAsync(); }
    public interface IMiniMapMarkFilterServices
    {
        MiniMapFilterMarkInfo[] GetAllMarkInfos();
        GameObjectPool<T> CreatePool<T>(Transform root,GameObject prefab) where T:Component;
        IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path);
        IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 position);
        void HookOnInput(Action<OnGlobalPointerDownResult> receiver);
        void UnhookOnInput(Action<OnGlobalPointerDownResult> receiver);
    }
}
