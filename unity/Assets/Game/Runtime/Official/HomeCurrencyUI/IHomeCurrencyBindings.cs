// Selected original currency count observer; typed player/parser boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyUI
{
public interface IHomeCurrencyBindings
{
    IHomeCurrencyPlayer MainPlayer { get; }
    bool ManagerAvailable { get; }
    SafeAction OnPlayerUpdate { get; }
    HomeCurrencyItemInfo GetItemInfo(int itemId);
    bool IsOutputAdditionItem(int itemId);
}
public interface IHomeCurrencyPlayer
{
    long GetItemAmount(int itemId);
    event Action<HomeCurrencyItem, long, object> OnItemCountUpdate;
    event Action<HomeCurrencyItem> OnAutoIncreaseItemDataUpdate;
}
public sealed class HomeCurrencyItem { public int Id; public long Count; }
public sealed class HomeCurrencyItemInfo { public bool HasAutoIncrease; }

}
