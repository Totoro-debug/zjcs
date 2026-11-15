using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeExperienceRules;

public static class HomeExperienceGrowthValidation
{
    public static void Run()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json"))) throw new InvalidOperationException("Owned workspace required");
        string Read(string path) => File.ReadAllText(Path.Combine(repository, path));
        var catalog = OfficialCharacterCatalog.Parse(Read("unity/Assets/Official/Character/character-progression.json"),
            Read("unity/Assets/Official/Character/item-time-limits.json"), Read("unity/Assets/Official/CharacterCreation/wardrobe.json"),
            Read("unity/Assets/Official/CharacterCreation/wardrobe-rules.json"), Read("unity/Assets/Official/Character/character-overview.json"));
        var tables = OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
        var slots = new ProfileSlotCatalog(Path.Combine(work, "saves")); string slot = slots.CreateSlot();
        var profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        bool limited = false; double property = 0;
        CharacterExperienceState State(OfflinePlayerProgress saved, CharacterPropCalculator calculator)
        {
            var source = new OfflineHomeExperienceEnvironmentSource(() => saved, tables, () => limited, _ => property);
            return OfflineHomeExperienceProjection.GrowthState(OfflineHomeExperienceProjection.Capture(saved, tables, source.Capture()));
        }
        Require(profile.ApplyCharacterMutation(saved => {
            saved.SubRank=(int)SubRank.Silver3; saved.Rank=(int)Rank.Silver; saved.SeasonRank=(int)Rank.Silver;
            saved.HasActiveSeasonRecord=true; saved.Experience=tables.LevelExpParser.GetExp(1,100);
            saved.Level=100; saved.SeasonBaseLevel=100; saved.CharacterProperties.TreasurePlaneRank=(int)Rank.Gold;
        }, "growth:seed").IsApplied, "Seed actual profile");
        var growth = new OfflineCharacterProgressionService(profile, catalog, experienceState: State);
        long ordinary = profile.Save.Player.Experience;
        Require(growth.AwardExperience(23,"growth:below-world").IsApplied, "Award below world rank");
        Require(profile.Save.Player.Experience==ordinary+23 && profile.Save.Player.BlessingExperience==0, "Personal highest rank cannot grant blessing below world rank");
        Require(profile.ApplyCharacterMutation(saved => saved.CharacterProperties.TreasurePlaneRank=(int)Rank.Silver,"growth:world-silver").IsApplied, "Set independent local world");
        long blessing = tables.LevelBlessInfoParser.GetExp(Rank.Silver,1);
        Require(blessing>0 && growth.AwardExperience(blessing,"growth:blessing").IsApplied, "Award real first blessing threshold");
        Require(profile.Save.Player.BlessingExperience==blessing && profile.Save.Player.Experience==ordinary+23
            && profile.Save.Player.Level==101 && profile.Save.Player.SeasonBaseLevel==100, "Candidate after award refreshes original base and blessing levels");
        Require(growth.AwardExperience(blessing,"growth:blessing").Status==ProgressionResultStatus.Unchanged, "Receipt prevents duplicate blessing");
        profile.Reload(); Require(growth.Player.Level==101 && growth.Player.SeasonRank==(int)Rank.Silver, "Reload preserves actual season record and original derived level");
        Require(profile.ApplyCharacterMutation(saved => saved.CharacterProperties.TreasurePlaneRank=(int)Rank.Gold,"growth:world-gold").IsApplied, "Advance only world");
        Require(growth.AwardExperience(37,"growth:ordinary-again").IsApplied && profile.Save.Player.Experience==ordinary+60
            && profile.Save.Player.BlessingExperience==blessing && profile.Save.Player.Level==101, "Closed blessing eligibility retains previously earned blessing level");
        Require(profile.ApplyCharacterMutation(saved => saved.HasActiveSeasonRecord=false,"growth:record-end").IsApplied, "End actual record");
        long cap = tables.LevelExpParser.GetExp(1,tables.SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit(SubRank.Gold1));
        Require(growth.AwardExperience(cap-profile.Save.Player.Experience+123,"growth:storage").IsApplied, "Storage award transaction");
        Require(profile.Save.Player.Experience==cap && profile.Save.Player.Level==100 && !profile.Save.Player.HasActiveSeasonRecord
            && profile.Save.Player.SeasonRank==(int)Rank.Silver, "Original next-subrank storage cap; no synthetic season record");
        limited=true; property=5;
        Require(growth.AwardExperience(2,"growth:property").IsApplied && profile.Save.Player.Experience==cap+2
            && profile.Save.Player.Level==105 && profile.Save.Player.SeasonBaseLevel==105, "Original property cap and disabled storage reach candidate refresh");
        string before=JsonUtility.ToJson(profile.Save.Player);
        Require(growth.AwardExperience(long.MaxValue,"growth:overflow").Status==ProgressionResultStatus.InvalidState
            && JsonUtility.ToJson(profile.Save.Player)==before, "Overflow rolls back all fields and receipt");
        profile.Reload(); Require(growth.Player.Level==105 && growth.Player.CharacterProperties.TreasurePlaneRank==(int)Rank.Gold
            && growth.Player.SubRank==(int)SubRank.Silver3, "Reopen world and personal state remain independent");
        Debug.Log("HOME_EXPERIENCE_GROWTH_PASSED: actual saved award transactions, original world blessing predicate, candidate-level refresh, retained earned blessing, original storage cap, receipt idempotency, rollback and reload. HUD and server award allocation are not verified.");
    }
    static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
}

[InitializeOnLoad]
public static class HomeExperienceGrowthPlayValidation
{
    const string Key="Zjcs.HomeExperienceGrowth.Play";
    static HomeExperienceGrowthPlayValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                new GameObject("ActualExperienceTransactions").AddComponent<HomeExperienceGrowthPlayRunner>();
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+".Exit",false))
            {
                SessionState.SetBool(Key+".Exit",false);
                double deadline=EditorApplication.timeSinceStartup+3;
                EditorApplication.CallbackFunction exit=null;
                exit=()=>{if(EditorApplication.timeSinceStartup<deadline || EditorApplication.isUpdating || EditorApplication.isCompiling)return;
                    EditorApplication.update-=exit;EditorApplication.Exit(SessionState.GetInt(Key+".Code",1));};
                EditorApplication.update+=exit;
            }
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    public static void Finish(int code)
    {
        SessionState.SetBool(Key,false);SessionState.SetBool(Key+".Exit",true);SessionState.SetInt(Key+".Code",code);
        EditorApplication.ExitPlaymode();
    }
}
public sealed class HomeExperienceGrowthPlayRunner:MonoBehaviour
{
    void Start()
    {
        AudioListener.volume=0;
        try { HomeExperienceGrowthValidation.Run();HomeExperienceGrowthPlayValidation.Finish(0); }
        catch(Exception error) { Debug.LogException(error);HomeExperienceGrowthPlayValidation.Finish(1); }
    }
}
