using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using ConditionType = Zjcs.Official.Partners.PartnerConditionRules.ConditionType;

namespace Zjcs.Offline.PartnersUI
{
    // The original nodes retain their decisions. This owner binds their parser
    // and condition-handler slots to the selected tables and the current save.
    public sealed class OfflinePartnerTipDotServices : IPartnerTipDotServices, IDisposable
    {
        private sealed class ProfilePlayer : IPartnerProfilePlayer
        {
            private readonly IPartnerTipDotPlayer player;
            internal ProfilePlayer(IPartnerTipDotPlayer player) { this.player = player; }
            public bool TryGetNpcFriendshipLevel(int id, out int level)
            {
                var npc = player.NpcManager.GetByNpcClassId(id);
                level = npc?.FriendShipLevel ?? 0;
                return npc != null;
            }
        }
        private readonly OfflinePartnerGiftConfiguration gifts;
        private readonly OfflineCharacterProgressionService progression;
        private readonly PartnerTalentRules talents;
        private readonly OfflinePartnerTalentPlayer talentPlayer;
        private readonly PartnerConditionSubscriptionCatalog subscriptionCatalog;
        private readonly OfflinePartnerTipDotConditions conditions;
        private readonly Func<int, bool, ProfessionTipDotAchievementKey> friendshipKey;
        private readonly Dictionary<int, PartnerTipDotNpc> npcs;
        private readonly List<PartnerTipDotNpc> mapExploreNpcs;
        private readonly Dictionary<int, List<PartnerTipDotProfile>> profiles = new Dictionary<int, List<PartnerTipDotProfile>>();
        private readonly Dictionary<(int, int), PartnerTipDotProfile> chapters = new Dictionary<(int, int), PartnerTipDotProfile>();
        private readonly Dictionary<int, PartnerTipDotTalentGraph> graphs = new Dictionary<int, PartnerTipDotTalentGraph>();
        private readonly Dictionary<int, PartnerTipDotTalentItem> talentItems = new Dictionary<int, PartnerTipDotTalentItem>();
        private readonly Dictionary<PartnerTipDotTalentNode, PartnerTalentRules.TalentNodeInfo> talentNodes = new Dictionary<PartnerTipDotTalentNode, PartnerTalentRules.TalentNodeInfo>();
        private readonly Dictionary<PartnerTipDotCondition, PartnerConditionHandler.ICondition> subscriptions = new Dictionary<PartnerTipDotCondition, PartnerConditionHandler.ICondition>();
        private bool disposed;
        public IPartnerTipDotPlayer Player { get; }

        public OfflinePartnerTipDotServices(OfflineMainlineProgressSession session, OfficialPartnerCatalog catalog,
            OfflinePartnerGiftConfiguration gifts, OfflinePartnerTipDotPlayer player, OfflineCharacterProgressionService progression,
            Func<int, bool, ProfessionTipDotAchievementKey> friendshipKey, Action<Exception, ConditionType> reportFailure)
        {
            if (session == null || catalog == null) throw new ArgumentNullException();
            this.gifts = gifts ?? throw new ArgumentNullException(nameof(gifts));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            if (!progression.Catalog.HasCurrentPartnerRules)
                throw new ArgumentException("Bind the selected current partner rules before creating tip services.", nameof(progression));
            this.friendshipKey = friendshipKey ?? throw new ArgumentNullException(nameof(friendshipKey));
            if (reportFailure == null) throw new ArgumentNullException(nameof(reportFailure));
            subscriptionCatalog = new PartnerConditionSubscriptionCatalog(catalog.Rows("conditions"));
            talents = new PartnerTalentRules(catalog.Rows, gifts.GetMaxLevel);
            talentPlayer = new OfflinePartnerTalentPlayer(() => session.Save.Player, catalog);
            var rawNpcs = catalog.Rows("npc").ToList();
            npcs = rawNpcs.ToDictionary(row => Convert.ToInt32(row["Id"]), row => {
                int id = Convert.ToInt32(row["Id"]);
                var info = gifts.GetNpcInfo(id);
                return new PartnerTipDotNpc { Id = id, IsStarGodNpc = info.IsStarGodNpc,
                    GiftPreferenceDict = info.GiftPreferenceDict == null ? null : new Dictionary<int, double>(info.GiftPreferenceDict) };
            });
            // Original GetNpcs(ignoreType) excludes MapExploreBoss. Keep input
            // order and include star gods; apply only the confirmed closed IDs.
            mapExploreNpcs = rawNpcs.Where(row => Convert.ToInt32(row["NpcType"]) != 2)
                .Select(row => Convert.ToInt32(row["Id"])).Where(id => !Closed(id)).Select(id => npcs[id]).ToList();
            var profileRules = new PartnerProfileRules(catalog.Rows);
            var rawProfiles = catalog.Rows("npc_profile").ToDictionary(row => (Convert.ToInt32(row["Id"]), Convert.ToInt32(row["ProfileId"])));
            foreach (int id in rawNpcs.Select(row => Convert.ToInt32(row["Id"])))
            {
                var source = profileRules.GetNpcProfileInfos(id);
                if (source == null || Closed(id)) continue;
                var list = new List<PartnerTipDotProfile>();
                foreach (var info in source)
                {
                    var value = new PartnerTipDotProfile { Id = info.Id, ProfileId = info.ProfileId,
                        UnlockCondition = Bind((Dictionary<string, object>)rawProfiles[(info.Id, info.ProfileId)]["UnlockCondition"],
                            current => info.UnlockCondition.IsMet(new ProfilePlayer(current))) };
                    chapters.Add((info.Id, info.ProfileId), value); list.Add(value);
                }
                profiles.Add(id, list);
            }
            var rawConditions = catalog.Rows("conditions").ToDictionary(row => Convert.ToInt32(row["ClassId"]),
                row => (Dictionary<string, object>)row["Condition"]);
            var nodeConditions = catalog.Rows("npc_talent_tree_npc_map").ToDictionary(
                row => (Convert.ToInt32(row["NpcClassId"]), Convert.ToInt32(row["NodeId"])), row => Convert.ToInt32(row["UnlockCondition"]));
            foreach (var entry in talents.Graphs)
            {
                var graph = new PartnerTipDotTalentGraph { Nodes = entry.Value.Nodes.ToList(), NodeInfos = new Dictionary<int, PartnerTipDotTalentNode>() };
                foreach (int nodeId in graph.Nodes)
                {
                    var source = entry.Value.GetNodeInfo(nodeId);
                    int conditionId = nodeConditions[(entry.Key, nodeId)];
                    var node = new PartnerTipDotTalentNode { NodeId = nodeId,
                        Condition = Bind(rawConditions[conditionId], current => {
                            RequirePlayer(current); return talents.IsConditionMet(conditionId, talentPlayer, source.NpcId, source.NodeId);
                        }) };
                    graph.NodeInfos.Add(nodeId, node); talentNodes.Add(node, source);
                }
                graphs.Add(entry.Key, graph);
            }
            foreach (var row in catalog.Rows("npc_talent_item"))
            {
                int id = Convert.ToInt32(row["NpcClassId"]), conditionId = Convert.ToInt32(row["TalentTreeUnlockConditionId"]);
                talentItems.Add(id, new PartnerTipDotTalentItem { TalentTreeUnlockCondition = Bind(rawConditions[conditionId], current => {
                    RequirePlayer(current); return talents.IsConditionMet(conditionId, talentPlayer);
                }) });
            }
            // Subscribe only after every immutable projection is ready.
            conditions = new OfflinePartnerTipDotConditions(player, reportFailure);
        }

        private static bool Closed(int id) => id >= 11015 && id <= 11022;
        private void RequirePlayer(IPartnerTipDotPlayer player)
        {
            if (!ReferenceEquals(player, Player)) throw new ArgumentException("Tip query belongs to a different local player.", nameof(player));
        }
        private PartnerTipDotCondition Bind(Dictionary<string, object> raw, Func<IPartnerTipDotPlayer, bool> check)
        {
            var condition = new PartnerTipDotCondition(check);
            subscriptions.Add(condition, subscriptionCatalog.Create(raw));
            return condition;
        }
        public PartnerTipDotNpc GetNpc(int id) => npcs.TryGetValue(id, out var npc) ? npc : null;
        public IEnumerable<PartnerTipDotNpc> MapExploreNpcs() => mapExploreNpcs;
        public NpcMeetStatus GetNpcMeetStatus(IPartnerTipDotPlayer player, int id)
        { RequirePlayer(player); return Closed(id) ? NpcMeetStatus.NotMeet : progression.NpcMeetStatusOf(id); }
        public List<PartnerTipDotProfile> GetNpcProfiles(int id) => profiles.TryGetValue(id, out var list) ? list : null;
        public PartnerTipDotProfile GetNpcProfile(int id, int chapter) => chapters.TryGetValue((id, chapter), out var value) ? value : null;
        public int GetMaxSubRankLevel(int id, int subRank) => gifts.GetMaxSubRankLevel(id, subRank);
        public int GetFriendshipLevelByExp(int id, long exp, int subRank) => gifts.GetFriendshipLevelByExp(id, exp, subRank);
        public int GetAllExpByDict(Dictionary<int, long> values, Dictionary<int, double> preferences) => gifts.GetAllExpByDict(values, preferences);
        public PartnerTipDotTalentGraph GetTalentGraph(int id) => graphs.TryGetValue(id, out var graph) ? graph : null;
        public PartnerTipDotTalentItem GetTalentItem(int id) => talentItems.TryGetValue(id, out var item) ? item : null;
        public bool IsTalentTreeFunctionLimit(IPartnerTipDotPlayer player, int id)
        { RequirePlayer(player); return talents.IsTalentTreeFunctionLimited(talentPlayer, id); }
        public bool IsTransformFunctionLimit(IPartnerTipDotPlayer player, int id)
        { RequirePlayer(player); return talents.IsTransformFunctionLimited(talentPlayer, id); }
        public PartnerTipDotTalentPointState GetTalentPointState(PartnerTipDotTalentNode node, IReadOnlyCollection<int> activated)
            => (PartnerTipDotTalentPointState)(int)talents.GetPointState(talentPlayer, talentNodes[node], activated);
        public bool IsGiftCanTransformTalentPoint(IPartnerTipDotPlayer player, int id)
        { RequirePlayer(player); return talents.CanTransformGift(talentPlayer, id); }
        public void RegisterCondition(PartnerTipDotCondition condition, Action changed)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerTipDotServices));
            conditions.Handler.Register(subscriptions[condition], changed);
        }
        public void DeregisterCondition(PartnerTipDotCondition condition, Action changed)
        { if (!disposed) conditions.Handler.Deregister(subscriptions[condition], changed); }
        public ProfessionTipDotAchievementKey NpcFriendshipKey(int id, bool starGod) => friendshipKey(id, starGod);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; conditions.Dispose();
        }
    }
}
