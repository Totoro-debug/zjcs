using System.Collections.Generic;
using Zjcs.Official.BagItemQueries;
using Zjcs.Official.BagTipDots;

namespace Zjcs.Official.BagRecycleQueries
{
    public interface IBagRecycleQueryServices
    {
        IBagRecycleQueryTables Tables { get; }
        // Numeric value of the original Rank identity, supplied by its local owner.
        int PlanesRank { get; }
    }
    public interface IBagRecycleQueryTables
    {
        object NpcGiftInfoParser { get; }
        IBagRecycleNpcTable NpcInfoParser { get; }
        IBagRecycleNpcLevelTable NpcFriendshipLevelInfoParser { get; }
        IBagRecycleItemTagTable ItemTagInfoParser { get; }
        IBagRecycleWorldTagTable WorldStageTagMapInfoParser { get; }
    }
    public interface IBagRecycleQueryPlayer
    {
        IBagRecycleNpcManager NpcManager { get; }
        IReadOnlyCollection<PlayerItemDataWrap> FindItemsByItemType(ItemType type);
        IReadOnlyCollection<PlayerItemDataWrap> FindItemsByType(ItemType type);
        bool RegionIsActived(int mapClassId, int region);
    }
    public interface IBagRecycleNpcManager { PlayerNpcDataWrap GetByNpcClassId(int npcClassId); }
    public interface IBagRecycleNpcTable { bool TryGetNpcByExclusiveGift(int giftId, out int npcId); }
    public interface IBagRecycleNpcLevelTable { int GetMaxLevel(int id); }
    public interface IBagRecycleItemTagTable { bool TryGet(int id, out ItemTagInfo config); }
    public interface IBagRecycleWorldTagTable { List<WorldStageTagEntityInfo> GetWorldStageTagEntityInfos(string tag); }
    public sealed class PlayerNpcDataWrap { public int NpcClassId, FriendShipLevel; }
    public sealed class ItemTagInfo { public int Id; public string Tag; }
    public sealed class GemPartInfo { public int Rank; }
    public sealed class WorldStageTagEntityInfo { public int MapClassId, Region, LoEntityId, EntityClassId; }
}
