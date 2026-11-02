using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;

[InitializeOnLoad]
public static class PartnerDialogueLocalUiValidation
{
    const string Key = "PartnerDialogueLocalUiValidation.Active";
    static PartnerDialogueLocalUiValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Saved original dialogue notifications").AddComponent<PartnerDialogueLocalUiRunner>();
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
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "dialogue-local-ui-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerDialogueLocalUiRunner : MonoBehaviour
{
    sealed class ExplicitWorldBoundary : IOfflinePartnerConversationWorld
    {
        public int CalculateWorldMapProgress(int map, int region) => throw new NotSupportedException("World UI is outside this notification fixture.");
        public bool IsRegionGoddessActive(int map, int region) => throw new NotSupportedException("World UI is outside this notification fixture.");
    }
    sealed class PresentationBoundary : IOfflinePartnerDialoguePresentation
    {
        public PartnerDialogueData Selected;
        public void ShowDialogue(PartnerDialogueData dialogue) { Selected = dialogue; }
        public GameObject Instantiate(GameObject prefab, Transform parent, bool reset) => throw new NotSupportedException("Native panel is outside this state fixture.");
        public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc) => throw new NotSupportedException("Native avatar is outside this state fixture.");
        public void ShowError(int code) => throw new NotSupportedException("Native text is outside this state fixture.");
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    void Start()
    {
        try { Validate(); PartnerDialogueLocalUiValidation.Finish(null); }
        catch (Exception error) { PartnerDialogueLocalUiValidation.Finish(error); }
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
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var player = new OfflinePartnerConversationPlayer(session, character, conversations,
            new ExplicitWorldBoundary(), () => new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc), _ => {});
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "通讯录隔离存档"; value.Level = 1;
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402, Level = 40 });
        }).IsApplied, "Create isolated player");
        var presentation = new PresentationBoundary();
        var service = new OfflinePartnerDialogueUiServices(session, player, presentation);
        Require(service.MainPlayerAvailable && service.PlayerName == "通讯录隔离存档" && service.DialogueStartNodeId == 1
            && service.Npcs.Count == 0 && service.Dialogues.Count == 0, "Original empty address book has no invented identities");
        var events = new List<string>();
        var grouped = new Dictionary<long, List<long>>();
        string disk = slots.GetProgressPath(slot);
        // Same externally observable add consumption as the original address
        // book: a first NPC scans all published rows; an existing NPC appends.
        service.DialogueAdded += row => {
            Require(File.ReadAllText(disk).Contains(row.DialogueId.ToString()), "Added notification follows successful disk commit");
            Require(service.Npcs.ContainsKey(row.NpcId) && ReferenceEquals(service.Dialogues[row.DialogueId], row), "Notify only published owned views");
            if (!grouped.TryGetValue(row.NpcId, out var list))
                grouped[row.NpcId] = service.Dialogues.Values.Where(x => x.NpcId == row.NpcId).Select(x => x.DialogueId).ToList();
            else list.Add(row.DialogueId);
            events.Add("add:" + row.DialogueId);
        };
        service.DialogueUpdated += row => events.Add("update:" + row.DialogueId);
        service.DialogueRemoved += row => {
            Require(!service.Dialogues.ContainsKey(row.DialogueId) && service.Npcs.ContainsKey(row.NpcId), "Removed row unpublished before original refresh, NPC survives callback");
            events.Add("remove:" + row.DialogueId);
        };
        var first = PartnerConversationRecord.Create(7001, 8001, 2021, 1, 9001, player.Now);
        var second = PartnerConversationRecord.Create(7001, 8001, 2022, 1, 9002, player.Now);
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterProperties.NpcConversations.Records.Add(OfflineNpcConversation.FromRecord(8402, first));
            value.CharacterProperties.NpcConversations.Records.Add(OfflineNpcConversation.FromRecord(8402, second));
        }).IsApplied, "Persist two conversations for one NPC");
        Require(events.Count == 0, "No UI event before explicit committed-state synchronization");
        service.SynchronizeCommittedState();
        Require(events.SequenceEqual(new[] { "add:9001", "add:9002" }) && grouped[8001].SequenceEqual(new long[] { 9001, 9002 }),
            "First-NPC scan and subsequent add do not duplicate a dialogue");
        var held = service.Dialogues[9001];
        var heldNpc = service.Npcs[8001];
        service.Reload();
        Require(events.Count == 2 && ReferenceEquals(held, service.Dialogues[9001]) && ReferenceEquals(heldNpc, service.Npcs[8001]),
            "Unchanged disk reload retains row references without duplicate notification");
        Require(service.GetNextNodes(2021, 1).SequenceEqual(conversations.Get(2021, 1).NextNodes)
            && service.IsNodeUnlocked(2021, 1), "Node UI queries use actual original configuration and saved friendship");
        var detached = service.GetRecord(9001); detached.DialogueNodes.Add(7777);
        Require(service.GetRecord(9001).DialogueNodes.SequenceEqual(new[] { 1 }), "Playback receives detached persisted history");
        int next = conversations.Get(2021, 1).NextNodes[0];
        first.SetCurrentNodeAndUpdateStatus(next, player);
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records[0]
            = OfflineNpcConversation.FromRecord(8402, first)).IsApplied, "Commit original node transition");
        service.SynchronizeCommittedState();
        Require(ReferenceEquals(held, service.Dialogues[9001]) && held.CurrentNode == next && events.Last() == "update:9001",
            "Sorted original row keeps its object when saved state changes");
        service.ShowDialogue(held);
        Require(ReferenceEquals(presentation.Selected, held), "Original row routes owned saved dialogue to presentation");
        int count = events.Count;
        byte[] before = File.ReadAllBytes(disk);
        string temporary = disk + ".tmp";
        Require(!File.Exists(temporary) && !Directory.Exists(temporary), "Own temporary path unused");
        Directory.CreateDirectory(temporary);
        bool failed = false;
        try { session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records.Clear()); }
        catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        finally { Directory.Delete(temporary); }
        service.SynchronizeCommittedState();
        Require(failed && events.Count == count && service.Dialogues.Count == 2 && File.ReadAllBytes(disk).SequenceEqual(before),
            "Failed real disk write leaves UI state and notification stream unchanged");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records.Clear()).IsApplied, "Commit removal");
        service.Reload();
        Require(service.Dialogues.Count == 0 && service.Npcs.Count == 0
            && events.Skip(count).SequenceEqual(new[] { "remove:9001", "remove:9002" }), "Removal refresh and final NPC cleanup");
        bool stale = false;
        try { service.ShowDialogue(held); } catch (InvalidOperationException) { stale = true; }
        Require(stale, "Recycled stale original row cannot open another profile's history");
        // Schema-valid but inconsistent NPC identity mapping must be rejected
        // before any partially published row or event reaches the original UI.
        second.NpcId = 8002;
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterProperties.NpcConversations.Records.Add(OfflineNpcConversation.FromRecord(8402, first));
            value.CharacterProperties.NpcConversations.Records.Add(OfflineNpcConversation.FromRecord(8402, second));
        }).IsApplied, "Persist malformed relation for explicit boundary test");
        count = events.Count;
        bool rejected = false;
        try { service.SynchronizeCommittedState(); } catch (InvalidDataException) { rejected = true; }
        Require(rejected && events.Count == count && service.Dialogues.Count == 0 && service.Npcs.Count == 0,
            "Prevalidate complete NPC identity map before publishing any delta");
        Debug.Log("PARTNER_DIALOGUE_LOCAL_UI_PASSED: actual profile, incremental original add consumption, stable references, reload, removal, rollback and malformed identity rejection. Native avatar/panel/playback not exercised.");
    }
}
