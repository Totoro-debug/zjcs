namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System.Collections.Generic;

public class PropUpgradeEffectAgent : APropEffectPlayAgent
{
	private List<ProfessionPlayerPropItem> mPropItems = new List<ProfessionPlayerPropItem>();

	public void ClearPropItem()
	{
		mPropItems.Clear();
	}

	public void AddPropItem(ProfessionPlayerPropItem playerPropItem)
	{
		mPropItems.Add(playerPropItem);
	}

	public void AddPropItemRange(List<ProfessionPlayerPropItem> playerPropItems)
	{
		mPropItems.AddRange(playerPropItems);
	}

	public int GetPropItemCount()
	{
		return mPropItems.Count;
	}

	protected override IEnumerable<ProfessionPlayerPropItem> GetPropItems()
	{
		return mPropItems;
	}
}

}
