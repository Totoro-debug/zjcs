// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StateGameObjectConfig : IStateConfig
{
	[SerializeField]
	private StateGameObjectData mStateGameObjectData;

	public void SetState(StateConfigValue state)
	{
		List<GameObject> list = null;
		foreach (StateGameObjectData.StateGameObjectKV item in mStateGameObjectData.GoConfig)
		{
			if ((int)item.State == (int)state)
			{
				list = item.GameObjects;
				break;
			}
		}
		foreach (StateGameObjectData.StateGameObjectKV item2 in mStateGameObjectData.GoConfig)
		{
			if ((int)item2.State == (int)state)
			{
				continue;
			}
			foreach (GameObject gameObject in item2.GameObjects)
			{
				if ((Object)(object)gameObject == (Object)null)
				{
					throw new Exception($"StateSwitcher 中有空预制体 state: {state}");
				}
				if (list == null || !list.Contains(gameObject))
				{
					gameObject.SetActive(false);
				}
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (GameObject item3 in list)
		{
			if ((Object)(object)item3 == (Object)null)
			{
				throw new Exception($"StateSwitcher 中有空预制体 tempList: {JsonUtility.ToJson((object)list)} tempList.Count: {list.Count}");
			}
			item3.SetActive(true);
		}
	}

	public void Clear()
	{
	}
}

}
