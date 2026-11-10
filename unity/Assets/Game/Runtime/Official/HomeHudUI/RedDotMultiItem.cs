namespace Zjcs.Official.HomeHudUI
{
using System.Collections.Generic;
using UnityEngine;

public class RedDotMultiItem : RedDotItemBase
{
	public List<RedDotItemBase> myItems;

	private void Reset()
	{
		myItems = new List<RedDotItemBase>();
		RedDotItemBase[] components = ((Component)this).GetComponents<RedDotItemBase>();
		foreach (RedDotItemBase redDotItemBase in components)
		{
			if (!(redDotItemBase is RedDotMultiItem))
			{
				myItems.Add(redDotItemBase);
			}
		}
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		foreach (RedDotItemBase myItem in myItems)
		{
			myItem.OnRedDotChanged += Go_OnRedDotChanged;
		}
	}

	protected override void OnDisableAndInited()
	{
		foreach (RedDotItemBase myItem in myItems)
		{
			myItem.OnRedDotChanged -= Go_OnRedDotChanged;
		}
		base.OnDisableAndInited();
	}

	private void Go_OnRedDotChanged(bool obj)
	{
		SetDirty();
	}

	public override bool ShowRedDot()
	{
		foreach (RedDotItemBase myItem in myItems)
		{
			if (myItem.IsShow)
			{
				return true;
			}
		}
		return false;
	}

	public void RegisterRedDot(RedDotItemBase redDotItem)
	{
		if (!myItems.Contains(redDotItem))
		{
			myItems.Add(redDotItem);
			redDotItem.OnRedDotChanged += Go_OnRedDotChanged;
		}
	}

	public void DeregisterRedDot(RedDotItemBase redDotItem)
	{
		if (myItems.Remove(redDotItem))
		{
			redDotItem.OnRedDotChanged -= Go_OnRedDotChanged;
		}
	}
}

}
