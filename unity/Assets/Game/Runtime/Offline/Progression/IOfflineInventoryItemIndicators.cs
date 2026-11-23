namespace Zjcs.Offline.Progression
{
    // Inventory presentation and scene tips share the same read-only queries.
    public interface IOfflineInventoryItemIndicators
    {
        bool CanUseRedDot(int itemId, long instanceId, long count);
        int GetRedDotType(int itemId, long instanceId, long count);
    }
}
