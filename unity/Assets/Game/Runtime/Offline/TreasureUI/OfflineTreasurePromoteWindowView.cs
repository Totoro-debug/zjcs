using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.TreasurePromoteUI;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasurePromoteWindowContext
    {
        public Func<OfflineTreasurePromoteWindowView,ITreasurePromoteServices> CreateServices;
        public OfflineTreasureDetailConfiguration Configuration;
        public IProfessionPropertyBindings Properties;
        public IProfessionImageResourceLoader Images;
        public IProfessionItemIconContext Items;
        public IItemIconAttachmentManager Attachments;
        public ITreasureIconServices Icons;
        public ITreasurePageServices Pages;
        public IProfessionUpgradeBindings CommonUseItem;
        public OfflineHomeCurrencyWindows Windows;
        public OfficialHomeTopBarAssetCatalog CurrencyCatalog;
        public OfflineHomeCurrencyPlayer CurrencyPlayer;
        public IProfessionSoundHost Sound;
        public TMP_FontAsset Font;
        public Action<GameObject> BindNativeTree;
        public Action<AudioClip,GameObject> PostSound;
    }
    // Complete original windows; shared popup ownership supplies their native masks, safe areas and show/hide animations.
    public sealed class OfflineTreasurePromoteWindowView:MonoBehaviour,IProfessionDebugUIServices
    {
        private OfflineTreasurePromoteWindowContext context;
        private OfflineTreasurePromoteAssets assets;
        private GameObject staging,content;
        private IDisposable poolBinding,uiBinding,propertyBinding,currencyBinding;
        private readonly List<IDisposable> currencyPlayers=new List<IDisposable>();
        private readonly Dictionary<GameObject,IDisposable> nested=new Dictionary<GameObject,IDisposable>();
        private bool mainClosed;
        private bool responsesReady;
        public ITreasurePromoteServices Services { get; private set; }
        public OfflineTreasureMaterialServices Materials { get; private set; }
        public IProfessionUpgradeBindings UseItemBindings { get; private set; }
        public TreasurePromotePanel Panel=>content?content.GetComponent<TreasurePromotePanel>():null;
        public WorldPopupWindow Window=>content?content.GetComponent<WorldPopupWindow>():null;
        public GameObject Content=>content;
        public void Open(OfflineTreasurePromoteWindowContext value,int itemId)
        {
            if(context!=null)throw new InvalidOperationException("Original treasure promotion is already open.");
            if(value==null||value.CreateServices==null||value.Configuration==null||value.Properties==null||value.Images==null||
                value.Items==null||value.Attachments==null||value.Icons==null||value.Pages==null||value.CommonUseItem==null||
                value.Windows==null||!value.CurrencyCatalog||value.CurrencyPlayer==null||value.Sound==null||!value.Font||value.BindNativeTree==null||value.PostSound==null)
                throw new ArgumentException("Bind the complete original treasure promotion owners.");
            context=value;
            try
            {
                assets=OfflineTreasurePromoteAssets.LoadDefault();
                currencyBinding=value.CurrencyCatalog.RegisterCurrencies(assets.Currencies);
                foreach(var currency in assets.CurrencyItems)
                    currencyPlayers.Add(value.CurrencyPlayer.RegisterCurrency(currency.Type,currency.ItemId,currency.HasAutoIncrease));
                staging=new GameObject("Inactive original promotion injection",typeof(RectTransform));staging.SetActive(false);staging.transform.SetParent(transform,false);
                propertyBinding=new OfflineProfessionPropertyScope(value.Properties);
                poolBinding=Register(staging);
                Materials=new OfflineTreasureMaterialServices(assets,value.Configuration,staging.transform,
                    (prefab,parent,reset,active)=>InstantiateAndBind(prefab,parent,false,reset,active));
                UseItemBindings=new OfflineTreasureUseItemBindings(value.CommonUseItem,Materials,value.Icons,()=>Services);
                Services=value.CreateServices(this)??throw new InvalidOperationException("Original promotion services are unbound.");
                value.Windows.WindowClosed+=OnWindowClosed;
                value.Windows.Popup(assets.Prefab,Localize,root=>{
                    content=root;uiBinding=Register(root);BindTree(root);
                    if(!Panel||!Window)throw new InvalidOperationException("Complete original promotion controllers are unavailable.");
                },root=>{Panel.Init(itemId);responsesReady=true;});
            }
            catch{Release();throw;}
        }
        private string Localize(string key)
        {
            foreach(var row in assets.Texts)if(row.Key==key)return new WorldTextReplacer(RawText).ReplaceText(row.Value);
            return context.Configuration.TextValue(key);
        }
        private string RawText(string key)
        {foreach(var row in assets.Texts)if(row.Key==key)return row.Value;return context.Configuration.TextValue(key);}
        private IDisposable Register(GameObject root)=>OfflineUiBindingModule.Register(root.transform,new OfflineUiBindings{
            Localize=Localize,Font=()=>context.Font,Images=_=>context.Images,
            Pool=(scope,prefab,parent,keepWorld)=>InstantiateAndBind(prefab,parent,keepWorld,false)});
        private void BindTree(GameObject root)
        {
            if(root.activeInHierarchy)throw new InvalidOperationException("Inject original promotion services before activation.");
            context.BindNativeTree(root);
            foreach(var progress in root.GetComponentsInChildren<WorldItemProgress>(true))
                progress.Compact=count=>CultureNumberArg.Compact(count).ToString();
            OfflineTreasureIconBinding.Bind(root,context.Items,context.Icons,context.Attachments,Localize,()=>context.Font);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))text.font=context.Font;
            foreach(var row in root.GetComponentsInChildren<ProfessionPlayerPropItem>(true))row.Configure(context.Properties);
            foreach(var button in root.GetComponentsInChildren<ProfessionBlessPropBtn>(true))button.Configure(context.Properties);
            foreach(var item in root.GetComponentsInChildren<PlayerUseItemContent>(true))item.Configure(UseItemBindings);
            foreach(var item in root.GetComponentsInChildren<ProfessionRedDotMultiItem>(true))item.Configure(UseItemBindings);
            foreach(var item in root.GetComponentsInChildren<ProfessionCommonMaterial>(true))item.Configure(Materials);
            foreach(var item in root.GetComponentsInChildren<ProfessionUseItemSliderGroup>(true))item.Configure(Materials);
            foreach(var item in root.GetComponentsInChildren<ProfessionDebugUIHandler>(true))item.Configure(this);
            foreach(var item in root.GetComponentsInChildren<TreasurePromotePanel>(true))item.Configure(Services);
            foreach(var item in root.GetComponentsInChildren<TreasureLevelupPanel>(true))item.Configure(Services);
            foreach(var item in root.GetComponentsInChildren<TreasureUpgradePanel>(true))item.Configure(Services);
            foreach(var item in root.GetComponentsInChildren<TreasureUpgradePanelStarInfo>(true))item.Configure(Services);
            foreach(var item in root.GetComponentsInChildren<TreasureAwakeMaterialPanel>(true))item.Configure(Services);
            foreach(var item in root.GetComponentsInChildren<ItemIconTreasurePiece>(true))item.Configure(Services);
            foreach(var item in root.GetComponentsInChildren<TreasureCanMergeRedDotItem>(true)){item.Configure(Services);((ProfessionPlayerUIBase)item).Configure(UseItemBindings);}
            foreach(var item in root.GetComponentsInChildren<TreasurePageSelectItem>(true))item.Configure(context.Pages);
            foreach(var item in root.GetComponentsInChildren<ItemIconTreasureSelectableItem>(true))item.Configure(context.Pages);
            foreach(var click in root.GetComponentsInChildren<UIClickSound>(true))click.AkEvent.InstanceHost=context.Sound;
            foreach(var effect in root.GetComponentsInChildren<AkEvent>(true))effect.data.InstanceHost=context.Sound;
            foreach(var sound in root.GetComponentsInChildren<WorldButtonSound>(true))sound.Post=context.PostSound;
            foreach(var sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true))sound.Post=context.PostSound;
        }
        private GameObject InstantiateAndBind(GameObject prefab,Transform parent,bool world,bool reset,bool activate=true)
        {
            if(!staging)throw new InvalidOperationException("Original promotion is closed.");
            var clone=UnityEngine.Object.Instantiate(prefab,staging.transform,false);
            try
            {
                BindTree(clone);clone.transform.SetParent(parent,world);
                if(reset){clone.transform.localPosition=Vector3.zero;clone.transform.localRotation=Quaternion.identity;clone.transform.localScale=Vector3.one;}
                clone.SetActive(activate);return clone;
            }
            catch{Destroy(clone);throw;}
        }
        public void PopupAwakeMaterials(Action<TreasureAwakeMaterialPanel> initialize)
        {
            context.Windows.Popup(assets.AwakeMaterialPrefab,Localize,root=>{
                nested.Add(root,Register(root));BindTree(root);
                // The source's property assignment precedes this panel's OnEnable, unlike InitWrapper-based windows.
                initialize(root.GetComponent<TreasureAwakeMaterialPanel>());
            },root=>{});
        }
        public ProfessionUseItemSliderGroup InstantiateMaterialRow(GameObject prefab,Transform parent)
            =>InstantiateAndBind(prefab,parent,false,true).GetComponent<ProfessionUseItemSliderGroup>();
        // Original setting RPC replies arrive after OnEnable; local replies retain that asynchronous boundary.
        public void ScheduleResponse(Action response)=>StartCoroutine(DeliverResponse(response));
        private IEnumerator DeliverResponse(Action response)
        {
            while(context!=null&&!responsesReady)yield return null;
            yield return null;
            if(context!=null)response();
        }
        public bool PlayerAvailable=>context!=null&&context.Icons.PlayerAvailable;
        public bool IsGameMaster=>false;
        public IProfessionImageResourceLoader Resources=>context.Images;
        public void CheckGameMaster(bool invokeByPlayer,Action<bool> completed)=>completed(false);
        public GameObject Instantiate(GameObject prefab,Transform parent)=>InstantiateAndBind(prefab,parent,false,true);
        public void Close()
        {
            if(context==null)return;
            StopAllCoroutines();responsesReady=false;
            foreach(var root in new List<GameObject>(nested.Keys))context.Windows.Close(root);
            if(content)context.Windows.Close(content);else if(nested.Count==0)Release();
        }
        private void Update(){if(context!=null)context.CurrencyPlayer.SynchronizeCommittedPlayer();}
        private void OnWindowClosed(GameObject root)
        {
            if(nested.TryGetValue(root,out var binding)){binding.Dispose();nested.Remove(root);}
            if(root==content){mainClosed=true;content=null;uiBinding?.Dispose();uiBinding=null;}
            if(mainClosed&&nested.Count==0)Release();
        }
        private void Release()
        {
            if(context==null)return;
            StopAllCoroutines();responsesReady=false;
            context.Windows.WindowClosed-=OnWindowClosed;
            (Services as IDisposable)?.Dispose();
            Materials?.Dispose();Materials=null;
            foreach(var binding in nested.Values)binding.Dispose();nested.Clear();
            uiBinding?.Dispose();poolBinding?.Dispose();propertyBinding?.Dispose();uiBinding=poolBinding=propertyBinding=null;
            currencyBinding?.Dispose();currencyBinding=null;
            foreach(var binding in currencyPlayers)binding.Dispose();currencyPlayers.Clear();
            if(staging)Destroy(staging);staging=content=null;Services=null;UseItemBindings=null;context=null;assets=null;mainClosed=false;
        }
        private void OnDestroy(){Close();if(context!=null&&nested.Count==0&&!content)Release();}
    }
}
