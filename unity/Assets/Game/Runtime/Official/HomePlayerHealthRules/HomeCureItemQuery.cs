// Selected original 884 cure-item query; table and inventory boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomePlayerHealthRules
{
public enum CookingType
{
	Recovery,
	Buff,
	Prop
}
[Serializable] public sealed class CookingInfo { public int Id; public CookingType Type; }
public sealed class HomeCureItemQuery
{
    private readonly Dictionary<CookingType,List<CookingInfo>> CookingTypeDict = new Dictionary<CookingType,List<CookingInfo>>();
    private readonly Func<int,long> inventory;
    public HomeCureItemQuery(IEnumerable<CookingInfo> rows, Func<int,long> inventory)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        foreach (var row in rows) {
            if (!CookingTypeDict.TryGetValue(row.Type, out var list)) CookingTypeDict.Add(row.Type, list = new List<CookingInfo>());
            list.Add(row);
        }
    }
    private long GetItemAmount(int itemId) => inventory(itemId);
	public bool HasCureItem()
	{
		List<CookingInfo> list = CookingTypeDict[CookingType.Recovery];
		if (list == null || list.Count == 0)
		{
			return false;
		}
		foreach (CookingInfo item in list)
		{
			if (this.GetItemAmount(item.Id) > 0)
			{
				return true;
			}
		}
		return false;
	}
}
}
