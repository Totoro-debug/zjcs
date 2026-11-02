using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;

[InitializeOnLoad]
public static class PartnerConversationWorldValidation
{
    const string Key = "PartnerConversationWorldValidation.Active";
    static PartnerConversationWorldValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Actual conversation world state").AddComponent<PartnerConversationWorldRunner>();
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) ||
            !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "conversation-world-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerConversationWorldRunner : MonoBehaviour
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    void Start()
    {
        try { Validate(); PartnerConversationWorldValidation.Finish(null); }
        catch (Exception error) { PartnerConversationWorldValidation.Finish(error); }
    }
    static void Validate()
    {
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var character = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),
            read("Character/item-time-limits.json"), read("CharacterCreation/wardrobe.json"),
            read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"));
        var partner = OfficialPartnerCatalog.Parse(read("Partners/partner-rules.json"), read("Partners/partner-properties.json"));
        var conditions = new PartnerConversationConditionRules(partner.Conditions, partner.Rows("conditions"));
        var conversations = OfficialPartnerConversationAssetCatalog.LoadDefault().Load(conditions.Bind);
        var maps = OfficialPartnerConversationWorldAssetCatalog.LoadDefault();
        Require(maps.MapClassIds.OrderBy(value => value).SequenceEqual(new[] { 11, 12, 13, 14, 17, 19, 21, 23 })
            && maps.GetMaximum(12, 27) > 0, "Actual selected map data binding");
        Require(maps.GetGoddessEntityId(11, 56) == 0 && maps.GetGoddessEntityId(17, 0) == 110,
            "Actual absent/present original goddess identities");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var world = new OfflinePartnerConversationWorld(session, maps);
        var player = new OfflinePartnerConversationPlayer(session, character, conversations, world,
            () => new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
            _ => throw new InvalidOperationException("World condition fixture does not send dialogue updates."));
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "通话地图隔离档案"; value.Level = 1;
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8406, Level = 5 });
        }).IsApplied, "Create isolated profile");
        Require(world.CalculateWorldMapProgress(12, 27) == 0 && !world.IsRegionGoddessActive(17, 0),
            "Unvisited saved map has no invented progress or activation");
        Require(session.InitializeMap(12, new[] { 27 }).IsApplied && session.SetMapRegionProgress(12, 27, 9999).IsApplied,
            "Save actual fixed-point progress boundary");
        Require(world.CalculateWorldMapProgress(12, 27) == 9999 && !conversations.Get(1001, 1).UnlockCondition.IsMet(player),
            "Saved fixed point is not divided by maximum a second time");
        Require(session.SetMapRegionProgress(12, 27, 10000).IsApplied, "Complete saved region progress");
        session.Reload();
        Require(world.CalculateWorldMapProgress(12, 27) == 10000 && conversations.Get(1001, 1).UnlockCondition.IsMet(player),
            "Actual profile reload satisfies original world and friendship AND");
        Require(session.InitializeMap(11, new[] { 56 }).IsApplied
            && session.SetMapRegionProgress(11, 56, 10000).IsApplied
            && session.RecordGoddessActivated(11, 56, "conversation-map11-region56").IsApplied,
            "Save activation bit for the no-goddess regression");
        Require(player.RegionIsActived(11, 56) && !world.IsRegionGoddessActive(11, 56),
            "Original absent goddess ID remains false despite a regional activation bit");
        Require(session.InitializeMap(17, new[] { 0 }).IsApplied
            && session.SetMapRegionProgress(17, 0, 10000).IsApplied
            && session.RecordGoddessActivated(17, 0, "conversation-map17-region0").IsApplied,
            "Save actual unique goddess activation");
        session.Reload();
        Require(world.IsRegionGoddessActive(17, 0), "Actual unique goddess survives reload");
        int roots = 0;
        foreach (var node in conversations.Nodes)
        {
            node.UnlockCondition.IsMet(player); roots++;
            if (node.ChainSequenceCondition != null) { node.ChainSequenceCondition.IsMet(player); roots++; }
        }
        Require(roots == 6912, "Execute complete original condition closure against actual profile adapters");
        Debug.Log("PARTNER_CONVERSATION_WORLD_PASSED: actual eight-map Resources, 6912 condition roots, saved fixed-point boundary and original goddess identities.");
    }
}
