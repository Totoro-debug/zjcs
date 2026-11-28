// Complete original common promotion control; explicit local material services.
using System;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.ProfessionUI {
using Object=UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class ProfessionRedDotMultiItem : ProfessionRedDotItemBase
{
	public List<ProfessionRedDotItemBase> myItems;

	private void Reset()
	{
		myItems = new List<ProfessionRedDotItemBase>();
		ProfessionRedDotItemBase[] components = ((Component)this).GetComponents<ProfessionRedDotItemBase>();
		foreach (ProfessionRedDotItemBase redDotItemBase in components)
		{
			if (!(redDotItemBase is ProfessionRedDotMultiItem))
			{
				myItems.Add(redDotItemBase);
			}
		}
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		foreach (ProfessionRedDotItemBase myItem in myItems)
		{
			myItem.OnRedDotChanged += Go_OnRedDotChanged;
		}
	}

	protected override void OnDisableAndInited()
	{
		foreach (ProfessionRedDotItemBase myItem in myItems)
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
		foreach (ProfessionRedDotItemBase myItem in myItems)
		{
			if (myItem.IsShow)
			{
				return true;
			}
		}
		return false;
	}

	public void RegisterRedDot(ProfessionRedDotItemBase redDotItem)
	{
		if (!myItems.Contains(redDotItem))
		{
			myItems.Add(redDotItem);
			redDotItem.OnRedDotChanged += Go_OnRedDotChanged;
		}
	}

	public void DeregisterRedDot(ProfessionRedDotItemBase redDotItem)
	{
		if (myItems.Remove(redDotItem))
		{
			redDotItem.OnRedDotChanged -= Go_OnRedDotChanged;
		}
	}
}

}
