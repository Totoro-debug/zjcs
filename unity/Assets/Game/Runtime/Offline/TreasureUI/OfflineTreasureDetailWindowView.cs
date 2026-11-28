using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasureDetailWindowContext
    {
        public Func<ITreasureSuitViewServices,ITreasureDetailServices> CreateServices;
        public OfflineTreasureDetailConfiguration Configuration;
        public IProfessionPropertyBindings Properties;
        public IProfessionImageResourceLoader Images;
        public IItemDetailBindings ItemDetails;
        public OfflineHomeCurrencyWindows Windows;
        public IProfessionSoundHost Sound;
        public ITreasureItemShareServices Sharing;
        public TMP_FontAsset Font;
        public Action<GameObject> BindNativeTree;
        public Action<AudioClip,GameObject> PostSound;
        public Action<int,bool> PopupTreasure,PopupPiece;
    }
    // Complete original detail hierarchy. The shared window host owns nesting, masks, source navigation and other popups.
    public sealed class OfflineTreasureDetailWindowView:MonoBehaviour,ITreasureSuitViewServices,ITreasureNpcEffectServices
    {
        private OfflineTreasureDetailWindowContext context;
        private ITreasureDetailServices services;
        private GameObject staging,content;
        private IDisposable uiBinding,propertyBinding;
        public TreasureDetailDialog Panel=>content?content.GetComponent<TreasureDetailDialog>():null;
        public WorldPopupWindow Window=>content?content.GetComponent<WorldPopupWindow>():null;
        public GameObject Content=>content;
        public IProfessionImageResourceLoader Resources=>context.Images;
        public LanguageType Language=>LanguageType.zh_CN;
        public void Open(OfflineTreasureDetailWindowContext value,int itemId,bool canOperation=false,bool canShowReplace=false)
        {
            Prepare(value,()=>{Panel.Init(itemId);Panel.SetCanOperation(canOperation);Panel.SetCanShowReplace(canShowReplace);});
        }
        public void Open(OfflineTreasureDetailWindowContext value,SnapshotTreasure snapshot,bool canOperation=false)
        {
            Prepare(value,()=>Panel.Init(snapshot,canOperation));
        }
        private void Prepare(OfflineTreasureDetailWindowContext value,Action initialize)
        {
            if(context!=null)throw new InvalidOperationException("Original treasure detail is already open.");
            if(value==null||value.CreateServices==null||value.Configuration==null||value.Properties==null||value.Images==null||
                value.ItemDetails==null||value.Windows==null||value.Sound==null||value.Sharing==null||!value.Font||value.BindNativeTree==null||
                value.PostSound==null||value.PopupTreasure==null||value.PopupPiece==null)
                throw new ArgumentException("Bind all original treasure detail owners before opening.");
            context=value;
            try
            {
                var assets=OfflineTreasureDetailAssets.LoadDefault();
                staging=new GameObject("Inactive original treasure detail injection",typeof(RectTransform));staging.SetActive(false);
                staging.transform.SetParent(transform,false);
                propertyBinding=new OfflineProfessionPropertyScope(value.Properties);
                services=value.CreateServices(this)??throw new InvalidOperationException("Original detail services are unbound.");
                value.Windows.WindowClosed+=OnWindowClosed;
                value.Windows.Popup(assets.Prefab,Localize,root=>{
                    content=root;
                    uiBinding=OfflineUiBindingModule.Register(root.transform,new OfflineUiBindings{
                        Localize=Localize,Font=()=>value.Font,Images=_=>value.Images,
                        Pool=(scope,prefab,parent,keepWorld)=>InstantiateAndBind(prefab,parent,keepWorld,false)});
                    BindTree(root);
                    if(!Panel||!Window)throw new InvalidOperationException("Complete original treasure detail controller/window is unbound.");
                },root=>initialize());
            }
            catch{Release();throw;}
        }
        private string Localize(string key)
        {
            string text=context.Configuration.TextValue(key);
            return text==key?OfflineTreasureSuitAssets.LoadDefault().Text(key):text;
        }
        private void BindTree(GameObject root)
        {
            if(root.activeInHierarchy)throw new InvalidOperationException("Bind original detail services before activation.");
            context.BindNativeTree(root);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))text.font=context.Font;
            foreach(var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {text.Localizer=Localize;text.FontProvider=()=>context.Font;}
            foreach(var row in root.GetComponentsInChildren<ProfessionPlayerPropItem>(true))row.Configure(context.Properties);
            foreach(var button in root.GetComponentsInChildren<ProfessionBlessPropBtn>(true))button.Configure(context.Properties);
            foreach(var row in root.GetComponentsInChildren<TreasureSuitPropItem>(true))row.Configure(services.Tables);
            foreach(var item in root.GetComponentsInChildren<ItemDetailDialog>(true))item.Bind(context.ItemDetails);
            foreach(var guard in root.GetComponentsInChildren<ItemDetailGmTools>(true))guard.BindReleaseActor(false,false);
            foreach(var item in root.GetComponentsInChildren<TreasureNpcEffectContent>(true))item.Configure(this);
            foreach(var item in root.GetComponentsInChildren<TreasureNpcEffectItem>(true))item.Configure(this);
            foreach(var item in root.GetComponentsInChildren<ShareItemBtn>(true))item.Configure(context.Sharing);
            foreach(var click in root.GetComponentsInChildren<UIClickSound>(true))click.AkEvent.InstanceHost=context.Sound;
            foreach(var sound in root.GetComponentsInChildren<WorldButtonSound>(true))sound.Post=context.PostSound;
            foreach(var sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true))sound.Post=context.PostSound;
            foreach(var detail in root.GetComponentsInChildren<TreasureDetailDialog>(true))detail.Configure(services);
        }
        private GameObject InstantiateAndBind(GameObject prefab,Transform parent,bool worldPositionStays,bool reset,bool activate=true)
        {
            if(!staging)throw new InvalidOperationException("Original treasure detail is closed.");
            var clone=UnityEngine.Object.Instantiate(prefab,staging.transform,false);
            try
            {
                BindTree(clone);clone.transform.SetParent(parent,worldPositionStays);
                if(reset){clone.transform.localPosition=Vector3.zero;clone.transform.localRotation=Quaternion.identity;clone.transform.localScale=Vector3.one;}
                clone.SetActive(activate);return clone;
            }
            catch{Destroy(clone);throw;}
        }
        public bool HasItem(int id,long count)=>services.HasItem(id,count);
        public int GetPieceIdByTreasureId(int id)=>services.GetPieceIdByTreasureId(id);
        public Transform InstantiateSuitMember(Transform prefab,Transform parent)=>InstantiateAndBind(prefab.gameObject,parent,false,false,false).transform;
        public void PopupTreasure(int id,bool operation)=>context.PopupTreasure(id,operation);
        public void PopupPiece(int id,bool buttons)=>context.PopupPiece(id,buttons);
        public TreasureNpcViewInfo GetNpc(int id)=>context.Configuration.GetNpc(id);
        public double GetFriendshipPercent(int id)=>context.Configuration.GetFriendshipPercent(id);
        public GameObject Instantiate(GameObject prefab,Transform parent)=>InstantiateAndBind(prefab,parent,false,true);
        public void Close()
        {
            if(context==null)return;
            if(content)context.Windows.Close(content);
            else Release();
        }
        private void OnWindowClosed(GameObject root){if(root==content)Release();}
        private void Release()
        {
            if(context==null)return;
            context.Windows.WindowClosed-=OnWindowClosed;
            uiBinding?.Dispose();uiBinding=null;propertyBinding?.Dispose();propertyBinding=null;
            if(staging)Destroy(staging);staging=content=null;services=null;context=null;
        }
        private void OnDestroy(){if(context!=null&&content)context.Windows.Close(content);Release();}
    }
}
