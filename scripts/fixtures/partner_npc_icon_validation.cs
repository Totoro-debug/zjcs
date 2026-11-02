using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.PartnersNpc;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Object = UnityEngine.Object;
using TMPro;

// Added to the existing full-window pointer fixture by the preparation script.
// The actual player-avatar, full-body and popup-window implementations are
// explicit boundaries: any accidental use fails this NPC address-book check.
public static class PartnerNpcIconValidation
{
    static OfflinePartnerNpcRuntime runtime;
    static Presentation presentation;
    static OfflinePartnerDialogueUiServices dialogues;
    static OfflinePartnerNpcIconBindings icons;
    static PlayerPorts players;
    static OfflineProfessionImageCatalog images;
    static OfflineMainlineProgressSession session;
    static long selected;
    static int popupCount;
    [Serializable] sealed class Words { public Word[] texts; }
    [Serializable] sealed class Word { public string key, text; }
    static Dictionary<string, string> words;
    public static string Localize(string key, Func<string, string> fallback)
    {
        if (words == null) words = JsonUtility.FromJson<Words>(File.ReadAllText(Path.Combine(Application.dataPath,
            "Editor/npc-icon-dialogue-texts.json"))).texts.ToDictionary(row => row.key, row => row.text);
        return words.TryGetValue(key, out var value) ? value : fallback(key);
    }
    static NotSupportedException Outside() => new NotSupportedException("Outside the NPC address-book fixture.");
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(owner);

    public static IPartnerDialogueUiServices Create(OfflineMainlineProgressSession current,
        OfficialCharacterCatalog character, OfficialPartnerCatalog catalog)
    {
        session = current;
        images = Resources.Load<OfflineProfessionImageCatalog>("Partners/OfficialPartnerNpcIconImages");
        Require(images && images.Entries.Length == 151, "All selected original icon addresses are explicitly packaged");
        foreach (var row in images.Entries)
        {
            var sprite = row.Asset as Sprite;
            Require(sprite && sprite.texture && sprite.vertices.Length > 0 && sprite.triangles.Length > 0,
                "Native Windows Sprite imports with its original texture and mesh: " + row.Path);
        }
        runtime = new OfflinePartnerNpcRuntime(OfficialPartnerNpcRuntimeCatalog.Load());
        var localize = OfficialPartnerRosterAssetCatalog.LoadDefault().CreateLocalizer();
        players = new PlayerPorts(new OfflineProfessionImageResourceLoader(images), localize);
        icons = new OfflinePartnerNpcIconBindings(runtime, players, new HpBoundary(), players.Resources, localize,
            (id, sex) => throw Outside(), (id, type) => throw Outside());
        var conditions = new PartnerConversationConditionRules(catalog.Conditions, catalog.Rows("conditions"));
        var configuration = OfficialPartnerConversationAssetCatalog.LoadDefault().Load(conditions.Bind);
        var player = new OfflinePartnerConversationPlayer(session, character, configuration, new WorldBoundary(),
            () => new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc), _ => {});
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterProperties.NpcFriendships.Single(row => row.NpcClassId == 8402).Level = 40;
            value.CharacterProperties.NpcConversations.Records.Add(OfflineNpcConversation.FromRecord(8402,
                PartnerConversationRecord.Create(7001, 8001, 2021, 1, 9001, player.Now)));
        }).IsApplied, "Seed one isolated saved original conversation, owned by the actually met partner");
        presentation = new Presentation();
        dialogues = new OfflinePartnerDialogueUiServices(session, player, presentation);
        Require(dialogues.Dialogues.Count == 1 && dialogues.Npcs.Count == 1, "Actual saved conversation drives the nonempty address book");
        return dialogues;
    }

    public static void CheckFolded(GameObject root, Action<GameObject> click)
    {
        var row = root.GetComponentsInChildren<NpcPanelDialogueListNpcItem>().Single();
        var wrapper = Field<Component>(row, "mIcon") as TeamPlayerIcon;
        Require(wrapper && !wrapper.IsSelfPlayer && wrapper.InitFinished, "Original icon selects the NPC branch and finishes from real lite info");
        var icon = wrapper.myIconOther;
        var info = icon.mInfoData;
        Require(info.Id == 8001 && info.TargetData.IsNpc && info.Level == 1 && info.SubRank == runtime.Norank
            && info.Profession == 10 && info.CombatRating == 1045, "Address-book actor identity and original level/rank/profession/combat rating");
        Require(info.Name != "ui_npc_name_8402" && !string.IsNullOrEmpty(info.Name), "Actual original NPC name is localized");
        Require(icon.InfoUI.PlayerIcon.sprite == images.Entries.Single(r => r.Path == "UI/NPC/Npc_Icon/Map_11/npc_icon_8402").Asset,
            "Original NPC appearance branch uses the selected native Sprite");
        Require(icon.InfoUI.ProfessionIconGo == null, "Original address-book prefab leaves the profession icon's activation root unset; retain that branch");
        Require(icon.InfoUI.PlayerFigure == null && icon.InfoUI.NpcAppearanceSlot == null,
            "This original NPC row uses the Sprite branch without a full-body model slot");
        // The original full-row button covers the avatar here; use that actual
        // pointer handler, which opens the contact's conversation list.
        click(Field<Button>(row, "mBtn").gameObject);
        Require(popupCount == 0, "Original contact button opens its list without an NPC popup");
    }

    public static void CheckUnfolded(GameObject root, Action<GameObject> click)
    {
        var item = root.GetComponentsInChildren<NpcPanelDialogueListDialogueItem>().Single();
        Require(item.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("嘟嘟，嘟嘟~")),
            "Original saved dialogue preview resolves its actual Chinese node text");
        var button = item.GetComponentsInChildren<Button>().Single();
        click(button.gameObject);
        Require(selected == 9001, "Simulated original dialogue entry selects the saved original conversation");
        var held = dialogues.Dialogues[9001];
        session.Reload(); dialogues.SynchronizeCommittedState();
        Require(ReferenceEquals(held, dialogues.Dialogues[9001]) && session.Save.Player.CharacterProperties.NpcConversations.Records.Count == 1,
            "UI clicks/reload retain the real saved record and stable view identity");
        foreach (int id in runtime.CombatNpcClasses().Where(id => id < 11015 || id > 11022))
        {
            var info = runtime.CreateLiteInfo(8001, id, 7001, 1, runtime.Norank, 0);
            var data = icons.PlayerInfo.BuildLiteData(info, icons.CreateTarget(info));
            using (var operation = icons.PlayerInfo.Resources.LoadAsset<Sprite>(data.Icon)) Require(operation.Result, "Packaged icon for NPC " + id);
            if (data.Profession != 0) foreach (bool third in new[] { false, true })
                using (var operation = icons.PlayerInfo.Resources.LoadAsset<Sprite>(runtime.ProfessionIcon(data.Profession, third)))
                    Require(operation.Result, "Packaged selected profession icon for NPC " + id);
        }
        bool rejected = false;
        var first = runtime.CreateLiteInfo(8001, 8402, 7001, 1, runtime.Norank, 0);
        var other = runtime.CreateLiteInfo(8002, 8402, 7001, 1, runtime.Norank, 0);
        try { icons.PlayerInfo.BuildLiteData(first, icons.CreateTarget(other)); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Mismatched NPC identity is rejected before painting another actor's icon");
    }

    public static void Dispose() { runtime?.Dispose(); runtime = null; }
    sealed class WorldBoundary : IOfflinePartnerConversationWorld
    {
        public int CalculateWorldMapProgress(int map, int region) => throw Outside();
        public bool IsRegionGoddessActive(int map, int region) => throw Outside();
    }
    sealed class HpBoundary : ITeamPlayerHpServices
    {
        // No player-avatar/world service participates in this NPC-only check.
        // Preserve the original unavailable-player branch without inventing HP.
        public bool MainPlayerAvailable => false;
        public bool ManagerAvailable => false;
        public int MapCount => throw Outside();
        public float HpPercent => throw Outside();
        public SafeAction OnPlayerUpdate => throw Outside();
        public event Action<float> HpUpdated { add { throw Outside(); } remove { throw Outside(); } }
    }
    sealed class Presentation : IOfflinePartnerDialoguePresentation
    {
        public GameObject Instantiate(GameObject prefab, Transform parent, bool reset)
        {
            var result = Object.Instantiate(prefab, parent, false);
            // Original UIUtils.ResetUI: Transform for pooled contact rows;
            // All (including AutoActive) for an inactive dialogue template.
            result.transform.localPosition = Vector3.zero;
            result.transform.localRotation = Quaternion.identity;
            result.transform.localScale = Vector3.one;
            if (!reset) result.SetActive(true);
            return result;
        }
        public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc) => icons.InitializeDialogueIcon(icon, npc, 0);
        public void ShowError(int code) => throw new Exception("Unexpected original dialogue error: " + code);
        public void ShowDialogue(PartnerDialogueData value) { selected = value.DialogueId; }
    }
    sealed class PlayerPorts : ITeamIconServices, IProfessionPlayerInfoBindings
    {
        readonly Func<string, string> localize;
        public PlayerPorts(IProfessionImageResourceLoader resources, Func<string, string> localize) { Resources = resources; this.localize = localize; }
        public IProfessionPlayerInfoBindings PlayerInfo => this;
        public IProfessionImageResourceLoader Resources { get; }
        public IProfessionInfoPlayer MainPlayer => null;
        public long MainPlayerId => 7001;
        public bool ManagerAvailable => true;
        public bool MainPlayerAvailable => true;
        public bool RelationshipAvailable => throw Outside();
        public bool IsFriend(long id) => throw Outside();
        public bool? TeamPlayerOnline(long id) => throw Outside();
        public event Action<long> TeamPlayerOffline { add { throw Outside(); } remove { throw Outside(); } }
        public event Action<long> TeamPlayerOnlineChanged { add { throw Outside(); } remove { throw Outside(); } }
        public event Action<long> RelationAdded { add { throw Outside(); } remove { throw Outside(); } }
        public event Action<long> RelationRemoved { add { throw Outside(); } remove { throw Outside(); } }
        public Task<object> GetLiteInfo(IProfessionInfoTarget target, CancellationToken token, bool latest = false) => throw Outside();
        public long LiteInfoId(object info) => throw Outside();
        public Task WaitForSeconds(float seconds, CancellationToken token) => throw Outside();
        public Task<object> GetPlayerBriefInfo(long id) => throw Outside();
        public void PopupNpc(Component owner, IProfessionInfoNpcTarget target) { Require(target.NpcClassId == 8402 && target.TargetId == 8001, "Actual original NPC popup arguments"); popupCount++; }
        public void PopupPlayer(Component owner, object info, int scene, int skills) => throw Outside();
        public SafeAction OnPlayerUpdate => throw Outside();
        public PlayerInfoData BuildData(IProfessionInfoPlayer player) => throw Outside();
        public PlayerInfoData BuildLiteData(object info, IProfessionInfoTarget target) => throw Outside();
        public object BuildBriefInfo(IProfessionInfoPlayer player, bool online) => throw Outside();
        public object CreateNameArg(string name, bool translate) => translate ? localize(name) : name;
        public object CompactNumber(long value) => throw Outside();
        public string SubRankKey(int rank) => throw Outside();
        public string ProfessionNameKey(int profession) => throw Outside();
        public string GetFullBodyGuid(int profession, Sex sex) => throw Outside();
        public string FirstProfessionIcon(int profession) => throw Outside();
        public string ThirdProfessionIcon(int profession) => throw Outside();
        public ProfessionInfoText NameWithPlanes(object name, long server) => throw Outside();
        public bool HasHonorTitle(object appearance) => throw Outside();
        public ProfessionInfoNpcDefinition GetNpc(int classId) => throw Outside();
        public GameObject InstantiateKeepScale(GameObject prefab, Transform parent) => throw Outside();
        public void OverrideNpcInitialAnimation(GameObject model, int npcId) => throw Outside();
        public TeamTargetCatalog? TeamCatalog(Component component) => throw Outside();
        public ProfessionInfoRobotInvite RobotInvite(Component component) => throw Outside();
        public ProfessionInfoRobotInvite DefaultRobotInvite() => throw Outside();
        public bool CanJointTogether(int target, int main) => throw Outside();
        public int DuelSkillGroup(long player, long target) => throw Outside();
        public void Popup(Component owner, Action<IProfessionPlayerInfoDetailPanel> initialized) => throw Outside();
    }
}
