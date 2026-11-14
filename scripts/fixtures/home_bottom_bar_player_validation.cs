using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Sex=Zjcs.Official.HomeHudUI.Sex;

public sealed partial class HomeBottomBarPlayerRunner
{
    private Zjcs.Official.ProfessionUI.FontAssetFontService playerFonts;

    private IEnumerator CheckLocalPlayer(Transform parent,TMP_FontAsset font,OfficialHomeBottomBarAssetCatalog assets)
    {
        Require(font.sourceFontFile&&font.HasCharacters("人物游记家园公会大陆",out uint[] missing,false,true),
            "selected original font source generates all native tab labels");
        string saves=Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"bottom-player-saves-"+Guid.NewGuid().ToString("N"));
        var creation=CharacterCreationOfflineService.OpenDefault(saves);
        string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="底栏验证";
        Require(creation.TryCreateCharacter(slot,draft,out string error),error);
        Require(creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),error);
        var profile=OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saves),slot);
        var configuration=OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
        var local=new OfflineHomeBottomBarPlayer(profile,configuration);
        var caps=configuration.Rows("system_level_limit").Where(row=>Convert.ToInt32(row["PlayerBlessLevel"])==0)
            .GroupBy(row=>Convert.ToInt32(row["SubRank"]))
            .ToDictionary(group=>group.Key,group=>group.Max(row=>Convert.ToInt32(row["PlayerLevel"])));
        var ranks=new HashSet<int>(configuration.Rows("player_subrank").Select(row=>Convert.ToInt32(row["SubRank"])));
        int rank=ranks.OrderBy(value=>value).First(value=>caps.ContainsKey(value)&&caps.ContainsKey(value+1)
            &&ranks.Contains(value+1)&&caps[value+1]>caps[value]);
        var character=OfficialCharacterAssetCatalog.LoadDefault().Load();
        int sex=profile.Save.Player.Wardrobe.Sex;
        Require(profile.ApplyCharacterMutation(player=>{
            player.SubRank=rank;player.Level=caps[rank]-1;player.Experience=character.CumulativeLevelExperience[player.Level];
        },"bottom:below-cap").IsApplied,"commit real selected rank below cap");
        Require(local.HasPlayer&&local.PlayerSex==(Sex)sex&&!local.CheckSubRankUpgradeShow(),"real player's level gate");
        view=new OfflineHomeBottomBar(parent,new HomeBottomBarBindings{Bar=local,Entry=this,PlayerUi=this,Pets=this,
            TipDots=this,TipController=this,Font=font,PostSound=(clip,owner)=>soundPosts++},assets);
        var marker=(WorldStateSwitcher<bool>)typeof(PublicBottomBar)
            .GetField("mUpgradeStateSwitcher",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view.Bar);
        yield return new WaitForSeconds(.6f);
        Require(!marker.State,"original native marker reads real profile below cap");
        Capture("bottom-bar-real-below-cap.png");
        Require(profile.ApplyCharacterMutation(player=>{
            player.Level=caps[rank];player.Experience=character.CumulativeLevelExperience[player.Level];
        },"bottom:at-cap").IsApplied,"commit real selected cap");
        Require(local.CheckSubRankUpgradeShow(),"personal cap suffices without server rank or promotion material balance");
        // Original Awake reads the committed cap. A level-only transaction
        // must not masquerade as the original sub-rank event.
        view.Dispose();yield return null;
        view=new OfflineHomeBottomBar(parent,new HomeBottomBarBindings{Bar=local,Entry=this,PlayerUi=this,Pets=this,
            TipDots=this,TipController=this,Font=font,PostSound=(clip,owner)=>soundPosts++},assets);
        marker=(WorldStateSwitcher<bool>)typeof(PublicBottomBar)
            .GetField("mUpgradeStateSwitcher",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view.Bar);
        yield return new WaitForSeconds(.6f);
        Require(marker.State,"original native Awake reads real player at the cap");
        Capture("bottom-bar-real-upgrade.png");
        Require(profile.ApplyCharacterMutation(player=>player.SubRank=rank+1,"bottom:promoted-rank").IsApplied,
            "commit an actual sub-rank change");
        Require(!local.CheckSubRankUpgradeShow(),"new personal cap is not reached after promotion");
        local.NotifySubRankUpdate();yield return null;
        Require(!marker.State,"original actual rank-change notification clears native upgrade marker");
        var sexNode=view.Root.GetComponentInChildren<PublicBottomBarCharacterBtn>(true);
        var before=sexNode.GetComponentsInChildren<Image>(true).Select(row=>row.sprite).ToArray();
        Require(profile.ApplyCharacterMutation(player=>player.Wardrobe.Sex=1-sex,"bottom:sex").IsApplied,"commit isolated alternate sex");
        local.NotifyPlayerUpdate();yield return null;
        var after=sexNode.GetComponentsInChildren<Image>(true).Select(row=>row.sprite).ToArray();
        Require(local.PlayerSex==(Sex)(1-sex)&&before.Zip(after,(a,b)=>a!=b).Any(value=>value),"original sex proxy reads real saved appearance");
        Capture("bottom-bar-real-sex.png");
        view.Dispose();yield return null;
        profile=OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saves),slot);
        local=new OfflineHomeBottomBarPlayer(profile,configuration);
        Require(!local.CheckSubRankUpgradeShow()&&local.PlayerSex==(Sex)(1-sex),"disk reopen restores rank, level and sex queries");
        // Exercise terminal and blessing-only transitions against the actual
        // selected table, rather than inventing a higher rank or level cap.
        foreach(int current in ranks.OrderBy(value=>value))
        {
            int cap=caps.TryGetValue(current,out int value)?value:0;
            bool expected=ranks.Contains(current+1)&&caps.TryGetValue(current+1,out int next)&&next>cap;
            Require(profile.ApplyCharacterMutation(player=>{player.SubRank=current;player.Level=cap;},
                "bottom:rank-boundary:"+current).IsApplied,"commit selected rank boundary "+current);
            Require(local.CheckSubRankUpgradeShow()==expected,"actual selected cap/next-rank boundary "+current);
        }
        local.NotifySubRankUpdate();local.NotifyPlayerUpdate();
        Require(PublicBottomBar.Instance==null,"destroyed native owner does not receive local notifications");
        Debug.Log("HOME_BOTTOM_BAR_PLAYER_PASSED: selected original cap queries, real local profile, native upgrade/sex states, owner notifications, every selected rank boundary and disk reopen; entry/tip services remain fixture endpoints.");
    }
}
