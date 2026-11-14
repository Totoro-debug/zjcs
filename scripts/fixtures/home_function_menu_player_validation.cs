using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.HomeFunctionMenuUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

public sealed partial class HomeFunctionMenuPlayerRunner
{
    IEnumerator CheckRealPlayer(Transform parent, TMP_FontAsset font)
    {
        string saves=Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"function-menu-player-"+Guid.NewGuid().ToString("N"));
        var creation=CharacterCreationOfflineService.OpenDefault(saves);
        string slot=creation.CreateSlot(); var draft=creation.OpenDraft(slot); draft.name="菜单条件";
        Require(creation.TryCreateCharacter(slot,draft,out string error),error);
        Require(creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),error);
        var profile=OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saves),slot);
        Require(profile.ApplyCharacterMutation(player=>player.SubRank=1,"menu-player-initial-tier").IsApplied,"fixture uses a configured personal tier");
        var configuration=OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
        var conditions=new OfflineHomeHudConditions(profile,configuration,OfficialPartnerAssetCatalog.LoadDefault().Conditions,
            5,_=>false,()=>false,()=>false);
        var player=new OfflineHomeFunctionMenuPlayer(profile,conditions);
        Require(player.GetFunctionLimit(FunctionType.Task)==null && !player.IsFunctionLimit(FunctionType.Task),"missing original zero-key record is unrestricted");
        var first=player.GetFunctionLimit(FunctionType.Friend);
        Require(first!=null && ReferenceEquals(first.Condition,player.GetFunctionLimit(FunctionType.Friend).Condition),"same selected condition identity across menu reads");
        Require(player.IsFunctionLimit(FunctionType.Friend) && player.IsFunctionLimit(FunctionType.PlayerTop),"personal tiers 2 and 3 remain required");
        int ranks=0,levels=0,lastRank=0,lastLevel=0;
        Action<int> rankChanged=value=>{ranks++;lastRank=value;};
        Action<int> levelChanged=value=>{levels++;lastLevel=value;};
        player.OnSubRankUpdate+=rankChanged; player.OnLevelUpdate+=levelChanged;
        host=new MenuServices{LocalModel=player};
        view=new OfflineHomeFunctionMenu(parent,new HomeFunctionMenuBindings{
            Camera=camera,Screen=screen,Menu=host,Activities=host,Wiki=host,Download=host,Time=host,
            Font=font,PostSound=host.Play}); host.Menu=view.Root;
        view.Init(); yield return WaitState(Zjcs.Official.WorldUI.WorldPopupWindow.EState.Show);
        var friend=view.Root.GetComponentInChildren<FriendBtn>(true);
        var top=view.Root.GetComponentInChildren<TopListBtn>(true);
        Require(!friend.myBtn.gameObject.activeInHierarchy && !top.TopBtn.gameObject.activeInHierarchy,"original native buttons read locked real player");
        Capture("function-menu-real-tier-1.png");
        Require(profile.ApplyCharacterMutation(value=>value.SubRank=2,"menu-player-tier-two").IsApplied,"commit isolated tier-two fixture state");
        Require(!player.IsFunctionLimit(FunctionType.Friend) && player.IsFunctionLimit(FunctionType.PlayerTop),"reads the replaced committed Save");
        conditions.NotifySubRankUpdate(); player.NotifySubRankUpdate(); yield return null;
        Require(friend.myBtn.gameObject.activeInHierarchy && !top.TopBtn.gameObject.activeInHierarchy,"original condition callback reveals friend while ranking remains locked");
        Require(ranks==1 && lastRank==2 && levels==0,"rank event publishes committed value only");
        Require(profile.ApplyCharacterMutation(value=>value.SubRank=3,"menu-player-tier-three").IsApplied,"commit isolated tier-three fixture state");
        conditions.NotifySubRankUpdate(); player.NotifySubRankUpdate(); yield return null;
        Require(friend.myBtn.gameObject.activeInHierarchy && top.TopBtn.gameObject.activeInHierarchy,"original ranking listener reads tier-three unlock");
        Require(ranks==2 && lastRank==3 && levels==0,"no synthetic level event");
        Click(top.TopBtn); Require(host.Calls[host.Calls.Count-1]=="Top:TopPanel_CombatRating:Manual","native unlocked ranking click retains original arguments at controlled endpoint");
        player.NotifyLevelUpdate(); Require(levels==1 && lastLevel==profile.Save.Player.Level,"level event publishes current committed level");
        Capture("function-menu-real-tier-3.png");
        view.Dispose(); yield return null; host.Dispose();
        player.OnSubRankUpdate-=rankChanged; player.OnLevelUpdate-=levelChanged;
        conditions.Dispose();
        var restored=OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saves),slot);
        var reopenedConditions=new OfflineHomeHudConditions(restored,configuration,OfficialPartnerAssetCatalog.LoadDefault().Conditions,
            5,_=>false,()=>false,()=>false);
        var reopened=new OfflineHomeFunctionMenuPlayer(restored,reopenedConditions);
        Require(restored.Save.Player.SubRank==3 && !reopened.IsFunctionLimit(FunctionType.Friend)
            && !reopened.IsFunctionLimit(FunctionType.PlayerTop),"reload persists personal eligibility");
        reopenedConditions.Dispose();
        Debug.Log("HOME_FUNCTION_MENU_REAL_PLAYER_PASSED: selected zero-key lookup, original missing-record branch, real committed personal tiers, native condition/player listeners, pointer dispatch and save reload. Tier mutations are fixture arrangements, not promotion transactions; online features remain controlled endpoints.");
    }
}
