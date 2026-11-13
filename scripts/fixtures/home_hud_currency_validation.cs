using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeResultUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeSocialUiControls;
using Zjcs.Official.HomeTaskUI;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object=UnityEngine.Object;

public sealed partial class HomeCurrencyWindowsNativeRunner
{
    IEnumerator ExerciseHomeHudCurrency()
    {
        var oldCanvas=canvas;oldCanvas.gameObject.SetActive(false);
        var holder=new GameObject("FullHomeCurrencyScope");OfflineHomeHud home=null;OfflineHomeTopBarPools pools=null;
        OfflineHomeHudInput hudInput=null;OfflineHomeHudConditions hudConditions=null;OfflineHomeCurrencyWindowServices service=null;
        IDisposable scene=null,entries=null;var hudAssets=OfflineHomeHudAssets.LoadDefault();
        var texts=JsonUtility.FromJson<HudCurrencyLanguageData>(hudAssets.texts.text).texts.ToDictionary(row=>row.key,row=>row.text);
        foreach(var pair in OfflineHomeMainlineTaskData.LoadDefault().Texts)texts[pair.Key]=pair.Value;
        var taskClip=hudAssets.Sound(3772241779u);
        var taskType=HudCurrencyType("HomeTaskUiNativeRunner");taskType.GetField("Clip",BindingFlags.Public|BindingFlags.Static).SetValue(null,taskClip);
        LanguageManager.Initialize((ILocalizationTextService)HudCurrencyNested("HomeTaskUiNativeRunner","Language",texts));
        var tasks=(IHomeTaskServices)HudCurrencyNested("HomeTaskUiNativeRunner","Tasks",texts,fonts,taskClip);
        // Currency integration is under test; the other established HomeUI ports
        // remain controlled and the task table is explicitly empty in this fixture.
        var taskSound=(IDisposable)HudCurrencyNested("HomeTaskUiNativeRunner","Sound");tasks.GetType().GetField("Sound").SetValue(tasks,taskSound);
        var world=new GameObject("ControlledHomeSceneCamera",typeof(Camera)).GetComponent<Camera>();
        world.tag="MainCamera";world.transform.position=new Vector3(0,0,-10);world.orthographic=true;world.orthographicSize=5;world.targetTexture=target;world.cullingMask=0;world.depth=-1;
        scene=(IDisposable)HudCurrencyNested("HomeHudBindingsNativeRunner","SceneServices",world);
        entries=(IDisposable)HudCurrencyNested("HomeHudInfrastructureNativeRunner","EntryServices",(object)Array.Empty<HomeRightEntryUIActivityInfo>());
        var download=HudCurrencyNested("HomeHudInfrastructureNativeRunner","DownloadServices");HudCurrencySet(download,"Enabled",false);HudCurrencySet(download,"State",PaydState.Downloaded);
        var match=HudCurrencyNested("HomeSocialUiNativeRunner","MatchServices",texts);
        var results=HudCurrencyNested("HomeResultUiNativeRunner","Results",texts);HudCurrencySet(results,"ActiveFight",false);
        var tips=HudCurrencyNested("HomeHudBindingsNativeRunner","Tips");
        var configuration=OfficialCurrentHomeRuleAssetCatalog.LoadDefault();var partner=OfficialPartnerAssetCatalog.LoadDefault().Conditions;
        var conditionPlayer=new OfflineHomeConditionPlayer(configuration,()=>profile.Save,_=>false);var conditionRules=configuration.CreateConditions(partner);
        int beforeObservers=Field<Delegate>(profilePlayer,"OnItemCountUpdate").GetInvocationList().Length;
        try {
            hudInput=new OfflineHomeHudInput(new HudCurrencyPointer());
            hudConditions=new OfflineHomeHudConditions(profile,configuration,partner,5,_=>false,()=>false,()=>false);
            var currencyHost=new OfflineHomeCurrencyTopBarHost(profilePlayer,topBarAssets,()=>currencyActions,()=>pointer);
            Require(currencyHost.ColorFormat("x",new Color(1,.5f,0,.25f))=="<color=#FF8000>x</color>","original language color format uses RGB");
            pools=new OfflineHomeTopBarPools(holder.transform,currencyHost,hudAssets.defaultFont,assets.Text,topBarAssets);
            home=new OfflineHomeHud(holder.transform,new HomeHudBindings{
                Camera=cameraUi,TopBar=pools.Manager,Conditions=hudConditions,Input=hudInput,
                Windows=(IHomeMainUiWindows)HudCurrencyNested("HomeHudBindingsNativeRunner","MainServices"),
                RightEntries=(IHomeHudRightEntryServices)entries,Download=(IHomeHudDownloadServices)download,
                Scene=(IHomeSceneUiServices)scene,Match=(IHomeMatchServices)match,Results=(IHomeResultServices)results,FightRoot=(IHomeResultFightRoot)results,
                Tasks=tasks,TipDots=(IProfessionTipDotSystem)tips,TipController=(IProfessionTipDotSystemController)tips,
                Localize=key=>texts.TryGetValue(key,out var text)?text:assets.Text(key),PostSound=Post},hudAssets);
            canvas=home.Root.GetComponent<Canvas>();
            periodic=new OfflineHomePeriodicEvents(assets.CreateQuickBuyCalendar(8),()=>profile?.Save?.Player!=null,()=>quickCounterTime);periodic.Update(0);
            service=new OfflineHomeCurrencyWindowServices(assets,windowPlayer,profilePlayer,moneyQuickBuy,exchange,this,kind=>"full-hud-"+kind+":"+Guid.NewGuid().ToString("N"));
            var mask=OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
            windows=new OfflineHomeCurrencyWindows(new OfflineHomeCurrencyWindowBindings{Canvas=canvas,CoroutineHost=this,TopBar=pools.Manager,
                BlurMaskPrefab=mask.BlurMaskPrefab,BlurManager=this,Fonts=fonts,Screen=screen,Icons=this,PostSound=Post},service,assets);
            windows.WindowOpened+=root=>{foreach(var touch in root.GetComponentsInChildren<LongTouch>(true))touch.PointerPositionOverride=()=>pointer;};
            windows.WindowClosed+=root=>{if(root==active)active=null;};service.AttachWindows(windows);
            currencyActions=new OfflineHomeCurrencyActions(profile,profilePlayer,assets,windows,
                node=>partner.IsMet(node.ToObject<Dictionary<string,object>>(),conditionPlayer),
                ()=>configuration.Rows("function_limit").Where(row=>Convert.ToInt32(row["Type"])==49&&Convert.ToInt32(row["Key0"])==0&&Convert.ToInt32(row["Key1"])==0).Any(row=>!conditionRules.IsMet((Dictionary<string,object>)row["Condition"],conditionPlayer)),
                (id,count)=>{Require(count==0,"original full HUD description count");details++;},()=>directPay++);
            SetBalance(1,100);SetBalance(2,500);SetBalance(15,3);
            var showing=home.Show(CancellationToken.None).AsTask();float deadline=Time.realtimeSinceStartup+5;
            while(!showing.IsCompleted){Require(Time.realtimeSinceStartup<deadline,"full HomeUI original Show completes");yield return null;}showing.GetAwaiter().GetResult();
            yield return null;Canvas.ForceUpdateCanvases();
            var handler=home.Home.GetComponentsInChildren<TopBarHandler>(true).Single();
            var items=home.Home.GetComponentsInChildren<MePlayerItemUI>();
            Require(items.Select(item=>Field<PlayerItemUIItemIdProvider>(item,"mPlayerItemUIItemIdProvider").ItemId).SequenceEqual(new[]{1,2,57}),"complete original HomeUI borrows three real currency widgets in serialized order");
            Require(home.Home.transform.Find("Content/Top/NewPublicTopBarParent")!=null||handler.transform.IsChildOf(home.Home.transform),"original serialized top-bar parent remains inside complete HomeUI");
            Require(holder.GetComponentsInChildren<TopBarHandler>(true).Length==1,"full HUD composition creates no duplicate standalone top-bar parent");
            Require(HudCurrencyText(items[0])=="100"&&HudCurrencyText(items[1])=="500"&&HudCurrencyText(items[2]).Contains("300/300"),"complete HomeUI shows actual profile quantities and natural stamina cap");
            Require(Field<Delegate>(profilePlayer,"OnItemCountUpdate").GetInvocationList().Length==beforeObservers+3,"full HomeUI creates exactly three actual quantity observers");
            foreach(var item in items)RequireHudCurrencyOnScreen(item);
            Capture("home-hud-real-currency-native.png");
            Click(Field<Button>(items[0],"mAddBtn"));yield return Open(canvas.GetComponentsInChildren<QuickBuyDialog>(true).Single().gameObject);
            Require(popup.Canvas.sortingOrder==canvas.sortingOrder+10&&home.Root.GetComponentsInChildren<Canvas>(true).Single(value=>value.name=="CanvasBorder").sortingOrder==32767,"original window sequence excludes and preserves the maximum-order frame border");
            Require(home.Home.GetComponentsInChildren<MePlayerItemUI>().Length==3&&canvas.GetComponentsInChildren<MePlayerItemUI>().Length==4
                &&Field<PlayerItemUIItemIdProvider>(popup.GetComponentsInChildren<MePlayerItemUI>().Single(),"mPlayerItemUIItemIdProvider").ItemId==2,"original quick-buy requests only its diamond cost bar while complete HomeUI keeps all three currencies");
            long money=profilePlayer.GetItemAmount(1),diamond=profilePlayer.GetItemAmount(2);
            var quick=active.GetComponent<QuickBuyDialog>();var cost=Field<PlayerCostMoneyContent>(quick,"mCostMoneyContent");long price=cost.CostItemCount;
            Require(price>0&&price<=diamond,"actual full-HUD quick-buy has an affordable original price");
            var button=Field<Button>(items[0],"mAddBtn");var rect=(RectTransform)button.transform;
            var point=RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(rect.rect.center));var pointerData=new UnityEngine.EventSystems.PointerEventData(events){position=point};
            var hits=new List<UnityEngine.EventSystems.RaycastResult>();events.RaycastAll(pointerData,hits);
            Require(hits.Count>0&&!hits[0].gameObject.transform.IsChildOf(button.transform),"original popup mask blocks the underlying full-HUD plus button");
            Capture("home-hud-real-currency-popup-native.png");Click(cost.Btn);yield return WaitClosed();
            Require(profilePlayer.GetItemAmount(1)>money&&profilePlayer.GetItemAmount(2)==diamond-price&&HudCurrencyText(items[0])==Compact(profilePlayer.GetItemAmount(1)).ToString()
                &&HudCurrencyText(items[1])==Compact(profilePlayer.GetItemAmount(2)).ToString(),"native full-HUD purchase pointer commits actual cost/reward and updates both still-visible counters");
            var restored=Zjcs.Offline.Progression.OfflineMainlineProgressSession.OpenCharacter(slots,profileSlot);
            Require(restored.InventoryCount(1)==profilePlayer.GetItemAmount(1)&&restored.InventoryCount(2)==profilePlayer.GetItemAmount(2),"full-HUD currency transaction survives actual disk reopen");
            Click(Field<Button>(items[1],"mAddBtn"));yield return Open(canvas.GetComponentsInChildren<PayMoneyChangeDialog>(true).Single().gameObject);
            Require(popup.Canvas.sortingOrder==canvas.sortingOrder+10,"closing the last popup restores the original initial window sorting order");
            var pay=active.GetComponent<PayMoneyChangeDialog>();
            Require(pay.GetComponentsInChildren<MePlayerItemUI>().Length==2&&canvas.GetComponentsInChildren<MePlayerItemUI>().Length==5,"original exchange and complete HomeUI share the same pool with their distinct currency lists");
            var flare=pay.GetComponentsInChildren<MePlayerItemUI>().Single(item=>Field<PlayerItemUIItemIdProvider>(item,"mPlayerItemUIItemIdProvider").ItemId==15);
            var flareAdd=Field<Button>(flare,"mAddBtn");int directPayBefore=directPay;
            Click(flareAdd);confirmation=canvas.GetComponentsInChildren<WorldConfirmDialog>(true).Single();yield return WaitConfirmation();
            string payText=assets.Text("UI_Pay_Confirm_Goto_Pay_Panel");
            Require(payText!="UI_Pay_Confirm_Goto_Pay_Panel"&&confirmation.ContentText.GetComponent<TMP_Text>().text==payText,"original flare-star confirmation uses the extracted official localized text");
            Require(confirmation.PopupWindow.Canvas.sortingOrder==popup.Canvas.sortingOrder+10,"original nested confirmation increments only the active window sequence");
            Capture("home-hud-real-currency-confirm-native.png");
            Click(confirmation.CancelBtn);yield return WaitConfirmationClosed();
            Require(directPay==directPayBefore&&pay.gameObject.activeInHierarchy,"original flare-star cancel preserves exchange and does not navigate");
            Click(flareAdd);confirmation=canvas.GetComponentsInChildren<WorldConfirmDialog>(true).Single();yield return WaitConfirmation();
            Require(confirmation.PopupWindow.Canvas.sortingOrder==popup.Canvas.sortingOrder+10,"closing a nested popup restores the remaining original window sorting order");
            Click(confirmation.ConfirmBtn);yield return WaitConfirmationClosed();
            Require(directPay==directPayBefore+1,"original flare-star native confirmation forwards once to the explicit direct-pay page port");
            long flareCount=profilePlayer.GetItemAmount(15);diamond=profilePlayer.GetItemAmount(2);
            Click(pay.SetCountSlider.MaxBtn);Click(pay.ConfirmBtn);yield return WaitClosed();
            Require(profilePlayer.GetItemAmount(15)==0&&profilePlayer.GetItemAmount(2)==diamond+flareCount*assets.Pay.DiamonChangeScale
                &&HudCurrencyText(items[1])==Compact(profilePlayer.GetItemAmount(2)).ToString(),"full-HUD original exchange pointer commits actual flare-star cost and live diamond reward");
            restored=Zjcs.Offline.Progression.OfflineMainlineProgressSession.OpenCharacter(slots,profileSlot);
            Require(restored.InventoryCount(15)==0&&restored.InventoryCount(2)==profilePlayer.GetItemAmount(2),"full-HUD exchange survives disk reopen");
            var identities=holder.GetComponentsInChildren<MePlayerItemUI>(true).Select(item=>item.GetInstanceID()).OrderBy(id=>id).ToArray();
            var hiding=home.Hide(CancellationToken.None).AsTask();deadline=Time.realtimeSinceStartup+5;
            while(!hiding.IsCompleted){Require(Time.realtimeSinceStartup<deadline,"full HomeUI original Hide completes");yield return null;}hiding.GetAwaiter().GetResult();
            Require(Field<Delegate>(profilePlayer,"OnItemCountUpdate").GetInvocationList().Length==beforeObservers,"original full-HUD Hide returns all currency observers");
            showing=home.Show(CancellationToken.None).AsTask();deadline=Time.realtimeSinceStartup+5;
            while(!showing.IsCompleted){Require(Time.realtimeSinceStartup<deadline,"full HomeUI original re-Show completes");yield return null;}showing.GetAwaiter().GetResult();
            Require(identities.SequenceEqual(holder.GetComponentsInChildren<MePlayerItemUI>(true).Select(item=>item.GetInstanceID()).OrderBy(id=>id)),"full-HUD re-Show reuses the existing shared original pool without allocation");
            var rebound=home.Home.GetComponentsInChildren<MePlayerItemUI>();
            Require(rebound.Select(item=>Field<PlayerItemUIItemIdProvider>(item,"mPlayerItemUIItemIdProvider").ItemId).SequenceEqual(new[]{1,2,57})
                &&HudCurrencyText(rebound[0])==Compact(profilePlayer.GetItemAmount(1)).ToString()&&HudCurrencyText(rebound[1])==Compact(profilePlayer.GetItemAmount(2)).ToString(),"full-HUD re-Show rebinds recycled popup widgets to the original HomeUI currency order and real balances");
            home.Dispose();home=null;yield return null;
            Require(Field<Delegate>(profilePlayer,"OnItemCountUpdate").GetInvocationList().Length==beforeObservers,"full-HUD disposal returns the borrowed bar before pool disposal");
            Debug.Log("HOME_HUD_REAL_CURRENCY_PASSED: complete original HomeUI/frame and original single serialized top-bar parent, real 1/2/57 profile quantities, original money/diamond plus pointers, quick-buy/exchange and blur obstruction, flare-star native confirmation cancel/confirm order, actual purchase/debit/reward and live counters, disk reopen, shared popup pool, Show/Hide/re-Show and observer disposal. Other HomeUI ports and the direct-pay page are explicitly controlled; avatar, bottom bar, task data, scene and formal HomeFeatureRuntime composition remain pending.");
        }
        finally {
            windows?.Dispose();service?.Dispose();currencyActions=null;periodic?.Dispose();periodic=null;home?.Dispose();pools?.Dispose();hudInput?.Dispose();hudConditions?.Dispose();
            taskSound.Dispose();scene?.Dispose();entries?.Dispose();Object.Destroy(world.gameObject);Object.Destroy(holder);canvas=oldCanvas;oldCanvas.gameObject.SetActive(true);
        }
        yield return null;
    }
    static string HudCurrencyText(MePlayerItemUI item)=>System.Text.RegularExpressions.Regex.Replace(Field<Component>(item,"mCountText").GetComponent<TMP_Text>().text,"<[^>]*>",string.Empty);
    void RequireHudCurrencyOnScreen(MePlayerItemUI item)
    {
        var corners=new Vector3[4];((RectTransform)item.transform).GetWorldCorners(corners);
        foreach(var corner in corners){var point=RectTransformUtility.WorldToScreenPoint(cameraUi,corner);Require(point.x>=0&&point.x<=target.width&&point.y>=target.height*.9f&&point.y<=target.height,"actual full-HUD currency remains within original upper viewport");}
    }
    static Type HudCurrencyType(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(assembly=>assembly.GetType(name)).First(type=>type!=null);
    static object HudCurrencyNested(string owner,string name,params object[] args)=>Activator.CreateInstance(HudCurrencyType(owner).GetNestedType(name,BindingFlags.NonPublic|BindingFlags.Public),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);
    static void HudCurrencySet(object value,string name,object field)=>value.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(value,field);
    [Serializable] sealed class HudCurrencyLanguageData{public HudCurrencyLanguageRow[] texts;}
    [Serializable] sealed class HudCurrencyLanguageRow{public string key,text;}
    sealed class HudCurrencyPointer:IHomePointerSnapshot
    {public bool LeftDown=>false;public Vector2 Position=>Vector2.zero;public int Width=>608;public int Height=>1080;}
}
