using System.Collections.Generic;
namespace Zjcs.Official.HomeHudInfrastructureUI
{
public sealed class HomeRightEntryUIActivityInfoParser
{
    readonly HomeRightEntryUIActivityInfo[] all;
    List<HomeRightEntryUIActivityInfo> mSortedEntries;
    public HomeRightEntryUIActivityInfoParser(HomeRightEntryUIActivityInfo[] values){all=values;PostParse();}
    HomeRightEntryUIActivityInfo[] GetAll()=>all;
	public void PostParse()
	{
		mSortedEntries = new List<HomeRightEntryUIActivityInfo>();
		HashSet<int> hashSet = new HashSet<int>();
		HomeRightEntryUIActivityInfo[] all = GetAll();
		foreach (HomeRightEntryUIActivityInfo homeRightEntryUIActivityInfo in all)
		{
			hashSet.Add(homeRightEntryUIActivityInfo.SortedId);
			if (!string.IsNullOrEmpty(homeRightEntryUIActivityInfo.EntryPath))
			{
				mSortedEntries.Add(homeRightEntryUIActivityInfo);
			}
		}
		mSortedEntries.Sort((HomeRightEntryUIActivityInfo a, HomeRightEntryUIActivityInfo b) => a.SortedId.CompareTo(b.SortedId));
	}

	public IReadOnlyList<HomeRightEntryUIActivityInfo> GetSortedEntries()
	{
		return mSortedEntries;
	}

}
}
