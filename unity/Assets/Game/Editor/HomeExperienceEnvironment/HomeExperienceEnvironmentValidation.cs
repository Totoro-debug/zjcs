using System;
using System.IO;
using UnityEngine;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeExperienceRules;

public static class HomeExperienceEnvironmentValidation
{
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        string repository = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"));
        Require(work.StartsWith(Path.Combine(repository,"output","validation")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
            && File.Exists(Path.Combine(work,".zjcs-validation.json")), "Owned validation workspace required");
        var tables = OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
        string root = Path.Combine(work,"saves");
        var creation = CharacterCreationOfflineService.OpenDefault(root);
        string slot = creation.CreateSlot(); var draft = creation.OpenDraft(slot); draft.name = "本地位阶";
        Require(creation.TryCreateCharacter(slot,draft,out string error),error);
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root),slot);
        bool limited = false; double property = 0;
        var source = new OfflineHomeExperienceEnvironmentSource(()=>profile.Save.Player,tables,()=>limited,_=>property);
        var state = source.Capture();
        Require(state.PlanesRank==Rank.Norank && state.ServerRecordSeason==-1 && state.CurrentRecordData==null,"new local world and original absent-season sentinel");
        profile.ApplyCharacterMutation(saved=>{
            saved.SubRank=(int)SubRank.Silver3;saved.Rank=(int)Rank.Silver;saved.SeasonRank=(int)Rank.Silver;
            saved.HasActiveSeasonRecord=true;saved.CharacterProperties.TreasurePlaneRank=(int)Rank.Silver;
            saved.Experience=tables.LevelExpParser.GetExp(1,100);saved.Level=100;saved.SeasonBaseLevel=100;
        },"environment:silver");
        using(var player=new OfflineHomeExperiencePlayer(profile,tables,source.Capture))
        {
            Require(player.IsBlessLevelOpen(),"original blessing at local world highest subrank");
            Require(source.Capture().ServerRecordSeason==1 && source.Capture().CurrentRecordData.RecordRank==Rank.Silver,"personal Rank to Season, actual saved record");
            profile.ApplyCharacterMutation(saved=>saved.CharacterProperties.TreasurePlaneRank=(int)Rank.Gold,"environment:world");
            profile.Reload();player.SynchronizeCommittedPlayer();
            Require(!player.IsBlessLevelOpen() && player.PlanesRank==(int)Rank.Gold,"reloaded world stage independently closes original blessing predicate");
            Require(profile.Save.Player.SubRank==(int)SubRank.Silver3 && source.Capture().ServerRecordSeason==1,"world advancement does not change personal rank or season");
            profile.ApplyCharacterMutation(saved=>saved.HasActiveSeasonRecord=false,"environment:no-record");
            player.SynchronizeCommittedPlayer();
            Require(source.Capture().CurrentRecordData==null && !player.IsBlessLevelOpen() && player.ShowPlayerStoreExpLimit(),"no invented record; original storage fallback uses independent world rank");
            limited=true;player.SynchronizeCommittedPlayer();
            Require(!player.ShowPlayerStoreExpLimit(),"real function-173 port disables original storage");
            limited=false;property=5;player.SynchronizeCommittedPlayer();
            Require(player.LevelLimit==105,"non-battle level-limit property reaches original level cap");
            string before=JsonUtility.ToJson(profile.Save.Player);
            for(int i=0;i<3;i++){source.Capture();player.SynchronizeCommittedPlayer();}
            Require(JsonUtility.ToJson(profile.Save.Player)==before,"environment reads never rewrite local growth or world stage");
        }
        Zjcs.Editor.ProjectTools.Validate();
        Debug.Log("HOME_EXPERIENCE_ENVIRONMENT_PASSED: real isolated profile, local world rank persistence, personal season mapping, original blessing/storage predicates, function 173, property port and read-only capture. HUD and award transactions remain outside this check.");
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
