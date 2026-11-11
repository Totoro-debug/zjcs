using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Zjcs.Official.HomeFunctionMenuUI
{
    // These projections expose only fields consumed by the original menu UI.
    // The owning activity module supplies records, configuration and events.
    public interface IHomeMenuActivityEntryData { ActivityEntryType Type { get; } int Id { get; } }
    public sealed class HomeMenuActivityRecommendInfo
    { public ActivityEntryType ActivityEntryType; public int Id; public int Order; public bool ShowInMap; }
    public sealed class HomeMenuActivityEntryInfo { public string EntryPrefabPath; }
    public interface IHomeMenuPrefabHandle:IDisposable { GameObject Result { get; } }
    public interface IHomeMenuActivityServices
    {
        IEnumerable<IHomeMenuActivityEntryData> ActivityEntryDatas { get; }
        event Action<object> OnActivityOpen;
        event Action<object> OnActivityClose;
        bool ActivityDataManagerExists { get; }
        bool IsMapScene { get; }
        HomeMenuActivityRecommendInfo GetRecommend(ActivityEntryType type,int id);
        IReadOnlyDictionary<int,HomeMenuActivityEntryInfo> GetEntries(ActivityEntryType type);
        int RandomRange(int minimum,int maximum);
        IHomeMenuPrefabHandle LoadPrefab(string path);
        // Match the original scene-container Instantiate(prefab,parent,false):
        // bind dependencies before original Awake/OnEnable and preserve layout.
        GameObject Instantiate(GameObject prefab,Transform parent);
        IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 position);
        void ReportRecommendationClick(string panel,string eventId,Dictionary<string,string> extend);
    }
    public static class HomeMenuUIUtils
    {
        public static GameObject Instantiate(IHomeMenuActivityServices services,GameObject prefab,Transform parent)
        {
            var go=services.Instantiate(prefab,parent);
            ResetUI(go,EUIInstantiateFlag.All);return go;
        }
        public static T Instantiate<T>(IHomeMenuActivityServices services,GameObject prefab,Transform parent)
        { return Instantiate(services,prefab,parent).GetComponent<T>(); }
        // Original UIUtils.ResetUI; no layout or geometry approximation.
        public static void ResetUI(GameObject go,EUIInstantiateFlag flag=EUIInstantiateFlag.All)
        {
            Transform transform=go.transform;
            if((flag&EUIInstantiateFlag.Position)!=0)transform.localPosition=Vector3.zero;
            if((flag&EUIInstantiateFlag.Rotation)!=0)transform.localRotation=Quaternion.identity;
            if((flag&EUIInstantiateFlag.Scale)!=0)transform.localScale=Vector3.one;
            if((flag&EUIInstantiateFlag.AutoActive)!=0)go.SetActive(true);
        }
    }
}
