using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.TreasureUI
{
    // Original ItemIconManager selection and reset order, with pools owned by the enclosing local window.
    public sealed class OfflineTreasureMaterialServices:IProfessionMaterialServices,IDisposable
    {
        private readonly OfflineTreasurePromoteAssets assets;
        private readonly OfflineTreasureDetailConfiguration configuration;
        private readonly Transform root;
        private readonly Func<GameObject,Transform,bool,bool,GameObject> instantiate;
        private readonly Dictionary<string,GameObjectPool<ItemIcon>> pools=new Dictionary<string,GameObjectPool<ItemIcon>>();
        private readonly Dictionary<string,RectTransform> defaults=new Dictionary<string,RectTransform>();
        public ItemIconConfig IconConfig=>assets.IconConfig;
        public OfflineTreasureMaterialServices(OfflineTreasurePromoteAssets assets,OfflineTreasureDetailConfiguration configuration,
            Transform root,Func<GameObject,Transform,bool,bool,GameObject> instantiate)
        {this.assets=assets;this.configuration=configuration;this.root=root;this.instantiate=instantiate;}
        private GameObject PrefabForType(int type)
        {
            var entry=assets.ItemTypes.FirstOrDefault(row=>row.Id==type);
            var prefab=entry==null?assets.DefaultIconPrefab:entry.Prefab;
            if(!prefab)throw new InvalidOperationException("Bind the original material icon type: "+type);
            return prefab;
        }
        private GameObjectPool<ItemIcon> Pool(int type)
        {
            var prefab=PrefabForType(type);string key=prefab.name;
            if(!pools.TryGetValue(key,out var pool))pools.Add(key,pool=new GameObjectPool<ItemIcon>(root,
                (parent,unused,active)=>instantiate(prefab,parent,false,active),key));
            return pool;
        }
        public ItemIcon GetIcon(int id,Transform parent)
        {
            var info=configuration.GetItem(id)??throw new FormatException("Unknown original material: "+id);
            var special=assets.ItemOverrides.FirstOrDefault(row=>row.Id==id);
            ItemIcon icon;
            if(special!=null)
            {
                if(!special.Prefab)throw new InvalidOperationException("Bind the original material ID override: "+id);
                icon=instantiate(special.Prefab,parent,true,true).GetComponent<ItemIcon>();
            }
            else
            {
                icon=Pool(info.ItemType).Borrow(parent,true);icon.SetUIGray(false);
                string key=PrefabForType(info.ItemType).name;
                if(icon.transform is RectTransform rect)
                {
                    if(defaults.TryGetValue(key,out var saved))Copy(rect,saved);
                    else
                    {
                        saved=new GameObject("Original default "+key,typeof(RectTransform)).GetComponent<RectTransform>();
                        saved.SetParent(root,false);Copy(saved,rect);defaults.Add(key,saved);
                    }
                }
            }
            return icon.Init(info);
        }
        private static void Copy(RectTransform target,RectTransform source)
        {
            target.pivot=source.pivot;target.sizeDelta=source.sizeDelta;target.anchorMax=source.anchorMax;target.anchorMin=source.anchorMin;
            target.anchoredPosition=source.anchoredPosition;target.localScale=source.localScale;target.localRotation=source.localRotation;target.localPosition=source.localPosition;
        }
        public void RecycleIcon(ItemIcon icon)
        {
            if(!icon){Debug.LogWarning("尝试回收一个已经为null的Icon");return;}
            Pool(icon.myInfo==null?0:configuration.GetItem(icon.myInfo.ClassId).ItemType).Return(icon);
        }
        public string GetItemNameKey(int id)=>"item_"+id+"_name";
        public object DecorateLack(object value)=>new Lack(value,(Color32)assets.LightRed);
        private sealed class Lack
        {
            private readonly object value;private readonly Color32 color;
            public Lack(object value,Color32 color){this.value=value;this.color=color;}
            public override string ToString()=>"<color=#"+ColorUtility.ToHtmlStringRGBA(color)+">"+value+"</color>";
        }
        public void Dispose()
        {foreach(var pool in pools.Values)pool.Destory();pools.Clear();foreach(var rect in defaults.Values)if(rect)UnityEngine.Object.Destroy(rect.gameObject);defaults.Clear();}
    }
}
