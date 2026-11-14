#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.WorldUI;

public sealed partial class HomeNativeCurrencyRunner
{
    T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    T HudField<T>(string name) => Field<T>(home.Hud, name);
    OfflineHomeCurrencyWindows Windows => HudField<OfflineHomeCurrencyWindows>("nativeCurrencyWindows");
    MePlayerItemUI[] HomeCurrencies() => HudField<Zjcs.Offline.HomeUI.OfflineHomeTopBar>("nativeCurrencyBar").Root.GetComponentsInChildren<MePlayerItemUI>();
    int ItemId(MePlayerItemUI item) => Field<PlayerItemUIItemIdProvider>(item, "mPlayerItemUIItemIdProvider").ItemId;
    string CountText(MePlayerItemUI item) => System.Text.RegularExpressions.Regex.Replace(Field<Component>(item, "mCountText").GetComponent<TMP_Text>().text, "<[^>]*>", string.Empty);
    IEnumerator WaitWindow(GameObject root)
    {
        var popup=root.GetComponent<WorldPopupWindow>(); float end=Time.realtimeSinceStartup+8;
        while(root && (!root.activeInHierarchy || popup.State!=WorldPopupWindow.EState.Show))
        { Require(Time.realtimeSinceStartup<end,"Original currency popup show completes"); yield return null; }
        Require(root,"Original currency popup exists"); yield return null; Canvas.ForceUpdateCanvases();
    }
    IEnumerator WaitClosed()
    {
        float end=Time.realtimeSinceStartup+8;
        while(Windows.WindowCount>0) { Require(Time.realtimeSinceStartup<end,"Original currency popup hide completes"); yield return null; }
        yield return null;
    }
    void CaptureCurrency(string name)
    {
        var canvases=home.Hud.CanvasTransform.GetComponentsInChildren<Canvas>(true);
        var orders=canvases.Select(value=>value.sortingOrder).ToArray();
        var layers=canvases.Select(value=>value.sortingLayerID).ToArray();
        try { Capture(name); }
        finally { for(int i=0;i<canvases.Length;i++) if(canvases[i]) { canvases[i].sortingOrder=orders[i]; canvases[i].sortingLayerID=layers[i]; } }
    }
    IEnumerator CheckFormalCurrency()
    {
        var before=HomeCurrencies();
        Require(!Field<Button>(before[0],"mAddBtn").gameObject.activeInHierarchy&&!Field<Button>(before[1],"mAddBtn").gameObject.activeInHierarchy,"Initial personal conditions gate currency plus buttons");
        var profile=home.Profile;
        Require(profile.ApplyCharacterMutation(player=>{player.Level=10;if(!player.UnlockedMapClassIds.Contains(11))player.UnlockedMapClassIds.Add(11);},"native-currency-level").IsApplied,"Seed valid purchase level");
        Require(profile.ApplyInventoryTransaction("native-currency-fixture",save=>{
            long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach(var pair in new[]{(id:1,count:100L),(id:2,count:500L),(id:15,count:3L)})
            {
                long current=save.Player.Inventory.Where(item=>item.ItemId==pair.id).Sum(item=>item.Count);
                var result=pair.count>=current?OfflineInventoryOperations.Grant(save.Player,pair.id,pair.count-current,now)
                    :OfflineInventoryOperations.Consume(save.Player,pair.id,current-pair.count,now);
                Require(result.IsApplied,"Seed isolated actual balance");
            }
        }).IsApplied,"Commit real test inventory");
        home.Close();CloseCapture();yield return null;Open();yield return new WaitForSeconds(1);
        profile=home.Profile;
        home.Hud.RefreshAll(); yield return null;
        var items=HomeCurrencies();
        Require(items.Select(ItemId).SequenceEqual(new[]{1,2,57}),"Formal home uses original 1/2/57 serialized currencies");
        Require(CountText(items[0])=="100"&&CountText(items[1])=="500"&&CountText(items[2]).Contains("300/300"),"Formal native counters read actual character quantities");
        Require(home.Hud.CanvasTransform.GetComponentsInChildren<TopBarHandler>(true).Length==1,"No duplicate native currency parent");
        CaptureCurrency("home-formal-currency.png");
        Click(Field<Button>(items[0],"mAddBtn"));
        var quick=home.Hud.CanvasTransform.GetComponentInChildren<QuickBuyDialog>(true);
        yield return WaitWindow(quick.gameObject);
        Require(home.Hud.AnyModalOpen,"Native currency owner blocks scene input");
        var cost=Field<PlayerCostMoneyContent>(quick,"mCostMoneyContent");
        long price=cost.CostItemCount, money=profile.InventoryCount(1), diamond=profile.InventoryCount(2);
        Require(price>0&&price<=diamond,"Original money purchase is affordable");
        Require(profile.ApplyInventoryTransaction("native-currency-other-item",save=>Require(OfflineInventoryOperations.Grant(save.Player,8050,1,DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied,"Actual unrelated item grant")).IsApplied,"Commit non-currency item while popup is open");
        home.Hud.RefreshAll();yield return null;
        Require(cost.CostItemCount==price&&CountText(items[1])==CultureNumberArg.Compact(diamond).ToString(),"Currency popup filters unrelated inventory notification");
        var button=Field<Button>(items[0],"mAddBtn");var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(home.Scene.Camera,rect.TransformPoint(rect.rect.center))};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Require(hits.Count>0&&!hits[0].gameObject.transform.IsChildOf(button.transform),"Original popup mask blocks underlying home plus");
        CaptureCurrency("home-formal-currency-quick-buy.png");
        Click(cost.Btn);yield return WaitClosed();
        Require(profile.InventoryCount(1)>money&&profile.InventoryCount(2)==diamond-price,"Pointer purchase commits original cost and award");
        Require(CountText(items[0])==CultureNumberArg.Compact(profile.InventoryCount(1)).ToString()
            &&CountText(items[1])==CultureNumberArg.Compact(profile.InventoryCount(2)).ToString(),"Committed purchase updates visible home counters");
        Click(Field<Button>(items[1],"mAddBtn"));
        var pay=home.Hud.CanvasTransform.GetComponentInChildren<PayMoneyChangeDialog>(true);yield return WaitWindow(pay.gameObject);
        CaptureCurrency("home-formal-currency-exchange.png");
        var flare=pay.GetComponentsInChildren<MePlayerItemUI>().Single(item=>ItemId(item)==15);
        Click(Field<Button>(flare,"mAddBtn"));
        var confirm=home.Hud.CanvasTransform.GetComponentInChildren<WorldConfirmDialog>(true);yield return WaitWindow(confirm.gameObject);
        Click(confirm.CancelBtn);yield return null;
        float end=Time.realtimeSinceStartup+6;
        while(confirm){Require(Time.realtimeSinceStartup<end,"Nested original cancel completes");yield return null;}
        Require(pay.gameObject.activeInHierarchy&&Windows.WindowCount==1,"Cancel returns to original exchange");
        diamond=profile.InventoryCount(2);long stars=profile.InventoryCount(15);
        Click(pay.SetCountSlider.MaxBtn);Click(pay.ConfirmBtn);yield return WaitClosed();
        Require(profile.InventoryCount(15)==0&&profile.InventoryCount(2)==diamond+stars*OfflineHomeCurrencyAssets.LoadDefault().Pay.DiamonChangeScale,"Pointer exchange commits actual currency transaction");
        Require(!home.Hud.AnyModalOpen,"Last native close releases home scene input");
        long savedMoney=profile.InventoryCount(1),savedDiamond=profile.InventoryCount(2);
        home.Close();CloseCapture();yield return null;Open();yield return new WaitForSeconds(1);
        items=HomeCurrencies();
        Require(home.Profile.InventoryCount(1)==savedMoney&&home.Profile.InventoryCount(2)==savedDiamond
            &&home.Profile.InventoryCount(15)==0&&CountText(items[1])==CultureNumberArg.Compact(savedDiamond).ToString(),"Actual disk reopen restores purchases and native display");
        Click(Field<Button>(items[0],"mAddBtn"));quick=home.Hud.CanvasTransform.GetComponentInChildren<QuickBuyDialog>(true);yield return WaitWindow(quick.gameObject);
        Require(home.Hud.TryHandleBack(),"Home back handles original currency popup");yield return WaitClosed();
        CaptureCurrency("home-formal-currency-reopened.png");
    }
}
#endif
