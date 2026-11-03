using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using AchievementRules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;

namespace Zjcs.Offline.PartnersUI
{
    // Owns the verified partner graph runtime for the lifetime of the home UI.
    public sealed class OfflinePartnerTipDotRuntime : IDisposable
    {
        private sealed class EmptyPetManager : PartnerPanelPetManager
        {
            public override PartnerPanelPetRecord this[long id] => null;
            public override bool TryGetValue(long id, out PartnerPanelPetRecord pet) { pet = null; return false; }
            public override IEnumerable<PartnerPanelPetRecord> GetAllPet() => Array.Empty<PartnerPanelPetRecord>();
        }

        private sealed class PanelPlayer : IPartnerPanelTipDotPlayer
        {
            private readonly OfflinePartnerTipDotPlayer player;
            private readonly EmptyPetManager pets = new EmptyPetManager();
            private IPartnerDialogueUiServices dialogues;
            public PanelPlayer(OfflinePartnerTipDotPlayer player) { this.player = player; }
            public bool HasFurniture(int id) => false;
            public bool HasItems(object items) => false;
            public bool HasAnyNpcDialogueRedDot() => dialogues != null && dialogues.Dialogues.Values
                .Any(value => PartnerDialogueListRules.HasRedDot(value, dialogues));
            public PartnerPanelPetManager PetManager => pets;
            public bool PetCanEvolution(long id) => false;
            public bool PetCanUpgradePotentialLevel(long id) => false;
            public long GetItemAmount(int id) => player.GetItemAmount(id);
            public event Action<int> OnRepairFurniture;
            public event Action<object> OnNpcDialogueAdd, OnNpcDialogueUpdate, OnNpcDialogueRemove;
            public event Action<PartnerPanelPetRecord> OnPetDataUpdate, OnGetPet;
            public event Action<PartnerTipDotItem, long, object> OnItemCountUpdate;
            public void BindDialogues(IPartnerDialogueUiServices value)
            {
                if (ReferenceEquals(dialogues, value)) return;
                if (dialogues != null)
                {
                    dialogues.DialogueAdded -= OnDialogueAdded;
                    dialogues.DialogueUpdated -= OnDialogueUpdated;
                    dialogues.DialogueRemoved -= OnDialogueRemoved;
                }
                dialogues = value;
                if (dialogues != null)
                {
                    dialogues.DialogueAdded += OnDialogueAdded;
                    dialogues.DialogueUpdated += OnDialogueUpdated;
                    dialogues.DialogueRemoved += OnDialogueRemoved;
                }
            }
            public void Dispose() => BindDialogues(null);
            private void OnDialogueAdded(PartnerDialogueData value) => OnNpcDialogueAdd?.Invoke(value);
            private void OnDialogueUpdated(PartnerDialogueData value) => OnNpcDialogueUpdate?.Invoke(value);
            private void OnDialogueRemoved(PartnerDialogueData value) => OnNpcDialogueRemove?.Invoke(value);
        }

        private sealed class PanelServices : IPartnerPanelTipDotServices
        {
            private static readonly IReadOnlyList<(int, long)> NoFoods = Array.Empty<(int, long)>();
            private readonly OfflinePartnerTipDotServices partners;
            private readonly Dictionary<int, NpcRoleType> npcRoles;
            public PanelServices(OfflinePartnerTipDotServices partners, PanelPlayer player, OfficialPartnerCatalog catalog)
            {
                this.partners = partners; Player = player;
                npcRoles = catalog.Rows("npc").ToDictionary(row => Convert.ToInt32(row["Id"]),
                    row => (NpcRoleType)Convert.ToInt32(row["NpcRoleType"]));
            }
            public IPartnerPanelTipDotPlayer Player { get; }
            public PartnerPanelFurnitureInfo GetFurniture(int id) => new PartnerPanelFurnitureInfo {
                UnlockCondition = new PartnerPanelCondition(_ => false), RepairCost = Array.Empty<int>() };
            public IEnumerable<PartnerPanelNpcInfo> MapExploreNpcs() => partners.MapExploreNpcs()
                .Select(npc => new PartnerPanelNpcInfo { Id = npc.Id, NpcRoleType = npcRoles[npc.Id] });
            public PartnerPanelPetInfo GetPet(int id) => null;
            public Dictionary<int, PartnerPanelPetAbilityInfo> GetPetAbilities(int id) => new Dictionary<int, PartnerPanelPetAbilityInfo>();
            public object PetAbilityPropInfoParser => null;
            public PartnerPanelPetEvolutionInfo GetPetEvolution(int id, PetEvolutionPhase phase)
                => new PartnerPanelPetEvolutionInfo { PromotableSkills = new List<int>() };
            public IReadOnlyList<(int, long)> SortedFoods => NoFoods;
            public bool TryCalcLevelUpExpRequire(PartnerPanelPetRecord pet, int levels, out long required)
            { required = 0; return false; }
            public PartnerPanelCondition GetPetPotentialCondition() => new PartnerPanelCondition(_ => false);
            public void RegisterCondition(PartnerPanelCondition condition, Action changed) { }
            public void DeregisterCondition(PartnerPanelCondition condition, Action changed) { }
            public string ItemName(int id) => id.ToString();
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;
            public Subscription(Action dispose) { this.dispose = dispose; }
            public void Dispose() { Action action = dispose; dispose = null; action?.Invoke(); }
        }

        private sealed class AchievementPlayer : IProfessionTipDotPlayer
        {
            private readonly OfflinePartnerAchievementService achievements;
            private readonly AchievementRules rules;
            public AchievementPlayer(OfflinePartnerAchievementService achievements, AchievementRules rules)
            {
                this.achievements = achievements;
                this.rules = rules;
                achievements.ProgressUpdated += OnProgressUpdated;
                achievements.RewardGroupReceived += OnRewardGroupReceived;
                achievements.RewardsReceivedBatch += OnRewardsReceivedBatch;
            }
            public int Profession => 0;
            public bool StartChangeProfession => false;
            public event Action<int> OnProfessionPromote;
            public event Action<int, int> OnProfessionChange, OnProfessionUpgrade;
            public event Action<object> OnTaskAdd;
            public event Action<ProfessionTipDotItem, long, object> OnItemCountUpdate;
            public event Action<int, int> OnSkillRankRewardUpdate;
            public event Action<ProfessionTipDotItem, int, int> OnSkillRankUp;
            public event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch;
            public event Action<int> OnRecordAlreadyGotAchievementRewardByGroupId;
            public event Action OnRecordAlreadyGotAchievementRewardBatch;
            public event Action<ProfessionTipDotAchievementKey> OnUpdateAchievementData;
            public bool IsProfessionUnlocked(int profession) => false;
            public int GetProfessionLevel(int profession) => 0;
            public long GetItemAmount(int itemId) => 0;
            public bool IsFunctionLimit(int function) => false;
            public bool IsAnyCanGetAchievementReward(ProfessionTipDotAchievementKey key)
                => TryGetKey(key, out var value) && achievements.ReadCurrentPlayer().IsAnyCanGetAchievementReward(value);
            public bool IsAnyCanGetAchievementReward(List<ProfessionTipDotAchievementKey> keys)
                => keys != null && keys.Any(IsAnyCanGetAchievementReward);
            private bool TryGetKey(ProfessionTipDotAchievementKey key, out AchievementRules.AchievementDataKey value)
            {
                value = null;
                return key != null && rules.TryGetAchievementGroupToKey(key.GroupId, out value);
            }
            private void OnProgressUpdated(AchievementRules.AchievementDataKey key)
                => OnUpdateAchievementData?.Invoke(ProfessionKey(key, rules));
            private void OnRewardGroupReceived(int group) => OnRecordAlreadyGotAchievementRewardByGroupId?.Invoke(group);
            private void OnRewardsReceivedBatch() => OnRecordAlreadyGotAchievementRewardBatch?.Invoke();
            public void Dispose()
            {
                achievements.ProgressUpdated -= OnProgressUpdated;
                achievements.RewardGroupReceived -= OnRewardGroupReceived;
                achievements.RewardsReceivedBatch -= OnRewardsReceivedBatch;
            }
        }

        private sealed class AchievementRulesPort : IProfessionTipDotRules
        {
            private readonly AchievementRules rules;
            public AchievementRulesPort(AchievementRules rules) { this.rules = rules; }
            public bool AnyPromotionRequirementCompleted() => false;
            public bool HasAvailableHigherPromotion() => false;
            public bool IsPromotionRequirementCompleted(int profession) => false;
            public bool IsPromotableAndInNewRank(int profession) => false;
            public List<int> GetAllProfessions() => new List<int>();
            public ProfessionTipDotBaseInfo GetProfessionBaseInfo(int profession) => null;
            public bool TryGetLevelInfo(int profession, int level, out ProfessionTipDotLevelCost info) { info = null; return false; }
            public long CalcAwardBadge(ProfessionTipDotBaseInfo info, IProfessionTipDotPlayer player) => 0;
            public List<int> GetAllAchievementGroupIds() => rules.GroupIds.ToList();
            public List<int> AchievementTypeListForShowAllInfosInSameType => new List<int> {
                (int)AchievementRules.AchievementType.NpcFriendshipLevel,
                (int)AchievementRules.AchievementType.StarGodNpcFriendshipLevel
            };
            public List<ProfessionTipDotAchievementKey> GetAchievementDataKeysBySameType(ProfessionTipDotAchievementKey key)
            {
                if (key == null) return new List<ProfessionTipDotAchievementKey>();
                return rules.GroupIds.Select(rules.GetAchievementGroupToKey)
                    .Where(value => (int)value.AchievementType == key.AchievementType)
                    .Select(value => ProfessionKey(value, rules)).ToList();
            }
            public ProfessionTipDotAchievementKey CreateAchievementDataKey(int type, string jsonParam)
                => ProfessionKey(rules.CreateKey((AchievementRules.AchievementType)type, jsonParam), rules);
            public void RegisterAchievementGroupLimitCondition(Action onChanged) { }
            public void DeregisterAchievementGroupLimitCondition(Action onChanged) { }
        }

        private readonly OfflinePartnerTipDotPlayer player;
        private readonly OfflinePartnerTipDotServices services;
        private readonly PanelPlayer panelPlayer;
        private readonly PanelServices panelServices;
        private readonly AchievementPlayer achievementPlayer;
        private readonly TipDotSystem system;
        private readonly TipDotSystemController controller;
        private readonly TipDotNodeManager manager;
        private readonly OfflinePartnerAchievementService achievements;
        private bool disposed;

        public OfflinePartnerTipDotRuntime(OfflineMainlineProgressSession session, OfficialPartnerCatalog catalog,
            OfflinePartnerGiftConfiguration gifts, OfflineCharacterProgressionService progression)
        {
            if (session == null || catalog == null || gifts == null || progression == null)
                throw new ArgumentNullException();
            var achievementRules = new AchievementRules(catalog.Rows);
            achievements = new OfflinePartnerAchievementService(session, achievementRules);
            achievementPlayer = new AchievementPlayer(achievements, achievementRules);
            var rulePort = new AchievementRulesPort(achievementRules);
            player = new OfflinePartnerTipDotPlayer(session, catalog, gifts, _ => false, id => {
                var row = catalog.Rows("item").FirstOrDefault(value => Convert.ToInt32(value["ClassId"]) == id);
                return row == null ? (ItemType)0 : (ItemType)Convert.ToInt32(row["ItemType"]);
            });
            services = new OfflinePartnerTipDotServices(session, catalog, gifts, player, progression,
                (id, starGod) => FriendshipKey(achievementRules, id, starGod),
                (error, type) => UnityEngine.Debug.LogException(new InvalidOperationException("Partner tip condition failed: " + type, error)));
            panelPlayer = new PanelPlayer(player);
            panelServices = new PanelServices(services, panelPlayer, catalog);

            var assets = OfficialPartnerTipDotAssetCatalog.LoadDefault();
            var graphHandler = new TipDotGraphHandler(assets.LoadGraph);
            var bans = new TipDotBanManager(Array.Empty<ITipDotBanStrategy>());
            TipDotNodeManager graphManager = null;
            var factory = new TipDotNodeFactory(graphHandler.GetNodeGraph, node => {
                node.NodeManager = graphManager;
                node.TipDotBanManager = bans;
                if (node is IPartnerTipDotNode partnerNode) partnerNode.Services = services;
                if (node is IPartnerPanelTipDotNode panelNode) panelNode.Services = panelServices;
                if (node is TipDotConditionNodeBase conditionNode)
                { conditionNode.Player = achievementPlayer; conditionNode.Rules = rulePort; }
            });
            var bindings = new PartnerTipDotRuntimeBindings(() => session.Save.Player != null,
                assets.GetAllGraphs, assets.GetGraphType,
                (add, remove) => { add?.Invoke(this); return new Subscription(remove); },
                _ => new Subscription(null));
            graphManager = new TipDotNodeManager(new TipDotNodeDataRepository(), factory, graphHandler, bindings);
            manager = graphManager;
            system = new TipDotSystem(manager, bans, bindings);
            controller = new TipDotSystemController(manager);
            system.Initialize();
            system.OnPlayerUpdate();
        }

        public void Refresh()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerTipDotRuntime));
            var refreshed = achievements.RefreshFromSave();
            if (!refreshed.IsApplied && refreshed.Status != ProgressionResultStatus.Unchanged)
                UnityEngine.Debug.LogWarning("Partner achievement tip refresh did not apply: " + refreshed.Message);
            player.Synchronize();
        }

        public void Bind(TipDotNodeLinker linker, int npcId)
        {
            if (linker == null || disposed) return;
            linker.Controller = controller;
            linker.TipDotSystem = system;
            linker.SetNodeInstanceInfo(new NpcNodeInstanceInfo(npcId));
        }

        public void BindDialogues(IPartnerDialogueUiServices dialogues)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerTipDotRuntime));
            panelPlayer.BindDialogues(dialogues);
        }

        public void BindNativeTree(GameObject root)
        {
            if (root == null || disposed) return;
            foreach (var linker in root.GetComponentsInChildren<TipDotNodeLinker>(true))
            {
                linker.Controller = controller;
                linker.TipDotSystem = system;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            system.Dispose();
            manager.Dispose();
            services.Dispose();
            panelPlayer.Dispose();
            achievementPlayer.Dispose();
        }

        private static ProfessionTipDotAchievementKey FriendshipKey(AchievementRules rules, int npcId, bool starGod)
        {
            AchievementRules.AchievementDataKey key = starGod
                ? new AchievementRules.AchievementDataKeyStarGodNpcFriendshipLevel(npcId)
                : new AchievementRules.AchievementDataKeyNpcFriendshipLevel(npcId);
            int group = rules.GetAchievementKeyToGroup(key);
            // Original same-type achievement nodes expand to every configured group;
            // NPCs without a dedicated group still need the original type discriminator.
            return new ProfessionTipDotAchievementKey { AchievementType = (int)key.AchievementType, GroupId = group };
        }

        private static ProfessionTipDotAchievementKey ProfessionKey(AchievementRules.AchievementDataKey key, AchievementRules rules)
            => key == null ? null : new ProfessionTipDotAchievementKey {
                AchievementType = (int)key.AchievementType, GroupId = rules.GetAchievementKeyToGroup(key) };
    }
}
