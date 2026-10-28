namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class PartSlot
{
	public IProfessionSkinClipSlot Slot;

	public int DrawOrder;

	public List<IAppearancePart> PartList = new List<IAppearancePart>();

	public void SetParent(List<Transform> list, int index)
	{
		foreach (IAppearancePart part in PartList)
		{
			int num = (part.SkeletonFollower.ZFront ? index : (index - 1));
			if (num < 0)
			{
				num = 0;
			}
			part.GameObject.transform.SetParent(list[num]);
		}
	}

	public void Clear()
	{
		Slot = null;
		DrawOrder = -1;
		PartList.Clear();
	}
}

}
