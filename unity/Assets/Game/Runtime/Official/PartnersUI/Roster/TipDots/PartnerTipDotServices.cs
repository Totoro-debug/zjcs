using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerTipDotNode { IPartnerTipDotServices Services { get; set; } }
    // The exact player/parser operations consumed by the original graph nodes.
    // The owner supplies the current save and condition subscriptions per graph.
    public interface IPartnerDynamicTipDotNodeManager : ITipDotNodeManager
    {
        Action<TipDotNodeKey> OnAddNode { get; set; }
        Action<TipDotNodeKey> OnReduceNode { get; set; }
        void ConnectNodes(TipDotNodeKey parent, List<TipDotNodeKey> children);
    }

    public sealed class PartnerTipDotNpc
    {
        public int Id;
        public bool IsStarGodNpc;
        public Dictionary<int, double> GiftPreferenceDict;
    }

    public sealed class PartnerTipDotNpcRecord
    {
        public int NpcClassId, FriendShipLevel;
        public long FriendShip;
        public HashSet<int> ReceivedProfileRewards;
    }

    public sealed class PartnerTipDotNpcManager : IEnumerable<KeyValuePair<long, PartnerTipDotNpcRecord>>
    {
        private readonly Func<IEnumerable<PartnerTipDotNpcRecord>> records;
        public PartnerTipDotNpcManager(Func<IEnumerable<PartnerTipDotNpcRecord>> records)
        { this.records = records ?? throw new ArgumentNullException(nameof(records)); }
        public PartnerTipDotNpcRecord GetByNpcClassId(int id) => records().FirstOrDefault(value => value.NpcClassId == id);
        public IEnumerator<KeyValuePair<long, PartnerTipDotNpcRecord>> GetEnumerator() => records()
            .Select(value => new KeyValuePair<long, PartnerTipDotNpcRecord>(value.NpcClassId, value)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class PartnerTipDotItemInfo { public ItemType ItemType; }
    public sealed class PartnerTipDotItem { public PartnerTipDotItemInfo Info; }
    public sealed class PartnerTipDotCondition
    {
        private readonly Func<IPartnerTipDotPlayer, bool> check;
        public PartnerTipDotCondition(Func<IPartnerTipDotPlayer, bool> check)
        { this.check = check ?? throw new ArgumentNullException(nameof(check)); }
        public bool IsMet(IPartnerTipDotPlayer player) => check(player);
    }
    public sealed class PartnerTipDotProfile
    {
        public int Id, ProfileId;
        public PartnerTipDotCondition UnlockCondition;
    }
    public sealed class PartnerTipDotTalentItem { public PartnerTipDotCondition TalentTreeUnlockCondition; }
    public sealed class PartnerTipDotTalentNode { public PartnerTipDotCondition Condition; public int NodeId; }
    public sealed class PartnerTipDotTalentGraph
    {
        public List<int> Nodes;
        public Dictionary<int, PartnerTipDotTalentNode> NodeInfos;
        public PartnerTipDotTalentNode GetNodeInfo(int id) => NodeInfos[id];
    }
    // NpcTalentTreeUIUtils.EPointState serialized ordinal order.
    public enum PartnerTipDotTalentPointState { ConditionLimit, TalentPointLimit, CanActive, Actived }

    public interface IPartnerTipDotPlayer
    {
        int SubRank { get; }
        PartnerTipDotNpcManager NpcManager { get; }
        event Action<int, NpcMeetType> OnAddNewNpc;
        event Action<int> OnRemoveNpc;
        event Action<PartnerTipDotItem, long, object> OnItemCountUpdate;
        event Action<int, int> OnReceiveNpcProfileReward;
        event Action<int, int, int> OnNpcFriendshipLevelUpdate;
        bool IsFunctionLimit(int function);
        long GetItemAmount(int id);
        IReadOnlyCollection<int> GetNpcTalentTreeUnlockedNodeIds(int id);
    }

    public interface IPartnerTipDotServices
    {
        IPartnerTipDotPlayer Player { get; }
        PartnerTipDotNpc GetNpc(int id);
        IEnumerable<PartnerTipDotNpc> MapExploreNpcs();
        NpcMeetStatus GetNpcMeetStatus(IPartnerTipDotPlayer player, int id);
        List<PartnerTipDotProfile> GetNpcProfiles(int id);
        PartnerTipDotProfile GetNpcProfile(int id, int profileId);
        int GetMaxSubRankLevel(int id, int subRank);
        int GetFriendshipLevelByExp(int id, long exp, int subRank);
        int GetAllExpByDict(Dictionary<int, long> gifts, Dictionary<int, double> preferences);
        PartnerTipDotTalentGraph GetTalentGraph(int id);
        PartnerTipDotTalentItem GetTalentItem(int id);
        bool IsTalentTreeFunctionLimit(IPartnerTipDotPlayer player, int id);
        bool IsTransformFunctionLimit(IPartnerTipDotPlayer player, int id);
        PartnerTipDotTalentPointState GetTalentPointState(PartnerTipDotTalentNode node, IReadOnlyCollection<int> activated);
        bool IsGiftCanTransformTalentPoint(IPartnerTipDotPlayer player, int id);
        void RegisterCondition(PartnerTipDotCondition condition, Action changed);
        void DeregisterCondition(PartnerTipDotCondition condition, Action changed);
        ProfessionTipDotAchievementKey NpcFriendshipKey(int id, bool starGod);
    }
}
