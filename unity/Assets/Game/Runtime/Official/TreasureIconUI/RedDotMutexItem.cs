// Original treasure UI controller; explicit local bindings only.
using Object=UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureCultivation;
namespace Zjcs.Official.TreasureUI {
using System.Collections.Generic;
using UnityEngine;

public class RedDotMutexItem : TreasurePlayerRedDotItemBase
{
	public List<ProfessionRedDotItemBase> myItems;

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

	public override void Refresh()
	{
		bool flag = false;
		ResetActive();
		for (int i = 0; i < myItems.Count; i++)
		{
			ProfessionRedDotItemBase redDotItemBase = myItems[i];
			if (!flag && redDotItemBase.IsShow)
			{
				flag = true;
				if (!redDotItemBase.IsActive)
				{
					SetItemActive(redDotItemBase, isActived: true);
				}
			}
			else if (flag && redDotItemBase.IsActive)
			{
				SetItemActive(redDotItemBase, isActived: false);
			}
		}
	}

	private void ResetActive()
	{
		for (int i = 0; i < myItems.Count; i++)
		{
			ProfessionRedDotItemBase item = myItems[i];
			SetItemActive(item, isActived: true);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		for (int i = 0; i < myItems.Count; i++)
		{
			if (!((Object)(object)myItems[i] == (Object)null))
			{
				myItems[i].IsActive = true;
			}
		}
	}

	public virtual void SetItemActive(ProfessionRedDotItemBase item, bool isActived)
	{
		item.IsActive = isActived;
		item.SetDirty();
	}
}

}
