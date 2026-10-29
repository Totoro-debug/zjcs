using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
public static class MiniMapMarkUIUtils
{
	public static void ResetUI(GameObject go, EUIInstantiateFlag flag = EUIInstantiateFlag.All)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		Transform transform = go.transform;
		if ((flag & EUIInstantiateFlag.Position) != 0)
		{
			transform.localPosition = Vector3.zero;
		}
		if ((flag & EUIInstantiateFlag.Rotation) != 0)
		{
			transform.localRotation = Quaternion.identity;
		}
		if ((flag & EUIInstantiateFlag.Scale) != 0)
		{
			transform.localScale = Vector3.one;
		}
		if ((flag & EUIInstantiateFlag.AutoActive) != 0)
		{
			go.SetActive(true);
		}
	}
	public static GameObject Instantiate(GameObject prefab, Transform parentTs, MiniMapMarkContext miniMap, EUIInstantiateFlag flag = EUIInstantiateFlag.All)
	{
		GameObject val = miniMap.InstantiateDisplay(prefab, parentTs);
		ResetUI(val, flag);
		return val;
	}
	public static void DestroyChildren(this Transform transform, bool includeInactive = true, bool detachChildren = false)
	{
		for (int num = transform.childCount - 1; num >= 0; num--)
		{
			Transform child = transform.GetChild(num);
			if (includeInactive || ((Component)child).gameObject.activeSelf)
			{
				Object.Destroy((Object)(object)((Component)child).gameObject);
			}
			if (detachChildren)
			{
				child.SetParent((Transform)null);
			}
		}
	}
}
}
