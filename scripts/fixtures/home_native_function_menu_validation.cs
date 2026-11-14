#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeFunctionMenuUI;

public sealed partial class HomeNativeFunctionMenuRunner
{
    T Field<T>(string name) => (T)home.Hud.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(home.Hud);
    OfflineHomeFunctionMenu Menu => Field<OfflineHomeFunctionMenu>("nativeFunctionMenu");
    Button Entry() => home.Hud.CanvasTransform.GetComponentsInChildren<Button>(true).Single(button =>
        button.name == "EntryBtnFolded" && button.GetComponentsInParent<Transform>().Any(parent => parent.name == "FunctionEntryFunc"));
    IEnumerator WaitMenu(WorldPopupWindow.EState state)
    {
        float end=Time.realtimeSinceStartup+6;
        while(Menu.Popup.State!=state && Time.realtimeSinceStartup<end) yield return null;
        Require(Menu.Popup.State==state,"Original native menu animation completes "+state);
        yield return null;
        Canvas.ForceUpdateCanvases();
    }
    void CloseMenu() => Click(Menu.Root.GetComponentsInChildren<CloseWindowBtn>(true).Single(button=>button.name=="CloseBtn").myButton);
    void ClosePageByMask()
    {
        var root=Field<GameObject>("activeDialog");
        foreach (var point in new[] {new Vector2(8,8),new Vector2(600,8),new Vector2(8,1072),new Vector2(600,1072)})
        {
            var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=root) continue;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
            return;
        }
        throw new InvalidOperationException("Page mask has no exposed pointer target");
    }
    IEnumerator CheckFormalMenu()
    {
        Click(Entry()); yield return WaitMenu(WorldPopupWindow.EState.Show);
        Require(Menu.Root.GetComponentsInChildren<MonoBehaviour>(true).All(component=>component),"No missing original menu components");
        var friend=Menu.Root.GetComponentInChildren<FriendBtn>(true);
        var top=Menu.Root.GetComponentInChildren<TopListBtn>(true);
        Require(!friend.myBtn.gameObject.activeInHierarchy && !top.TopBtn.gameObject.activeInHierarchy,"Real tier-one character gates original menu");
        Capture("home-native-menu-tier1.png");
        Require(home.Profile.ApplyCharacterMutation(player=>player.SubRank=3,"native-menu-tier-three").IsApplied,"Commit isolated tier-three fixture");
        home.Hud.RefreshAll(); yield return null;
        Require(friend.myBtn.gameObject.activeInHierarchy && top.TopBtn.gameObject.activeInHierarchy,"Committed personal tier refreshes enabled native buttons");
        Click(friend.myBtn); yield return null;
        Require(Field<TMPro.TMP_Text>("toast").text=="离线模式不开放好友" && Field<GameObject>("activeDialog")==null && Menu.IsOpen,"Friend follows confirmed offline scope without opening NPC roster");
        Capture("home-native-menu-tier3.png");
        CloseMenu(); yield return WaitMenu(WorldPopupWindow.EState.Hide);
        Require(!Menu.IsOpen && !home.Hud.AnyModalOpen,"Native close completes and releases modal input");
        Click(Entry()); yield return WaitMenu(WorldPopupWindow.EState.Show);
        Click(Menu.Root.GetComponentInChildren<SettingsBtn>(true).Btn); yield return null;
        Require(Field<string>("activePanelKey")=="settings" && Menu.IsOpen,"Original settings dispatch keeps menu beneath its page");
        Capture("home-native-menu-settings.png");
        ClosePageByMask(); yield return null;
        Require(Field<GameObject>("activeDialog")==null && Menu.IsOpen,"Settings mask pointer restores menu");
        Click(Menu.Root.GetComponentInChildren<FunctionEntryTaskBtn>(true).Button); yield return null;
        Require(Field<string>("activePanelKey")=="quests" && Menu.IsOpen,"Original task navigation keeps menu beneath its page");
        Capture("home-native-menu-tasks.png");
        ClosePageByMask(); yield return null;
        Require(Field<GameObject>("activeDialog")==null && Menu.IsOpen,"Task mask pointer restores menu");
        CloseMenu(); yield return WaitMenu(WorldPopupWindow.EState.Hide);
        home.Close(); CloseCapture(); yield return null;
        Open(); yield return new WaitForSeconds(1);
        Click(Entry()); yield return WaitMenu(WorldPopupWindow.EState.Show);
        Require(home.Profile.Save.Player.SubRank==3 && Menu.Root.GetComponentInChildren<FriendBtn>(true).myBtn.gameObject.activeInHierarchy,"Disk reopen preserves native menu eligibility");
        Capture("home-native-menu-reopened.png");
        CloseMenu(); yield return WaitMenu(WorldPopupWindow.EState.Hide);
    }
}
#endif
