// Selected original home overlap/light relation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Zjcs.Official.HomeRelations
{
using Object = UnityEngine.Object;
public class HomeRelationManager : IHomeRelationManager
{
	private Dictionary<EHomeRelationGroup, List<HomeMutex>> mGroupMutexes = new Dictionary<EHomeRelationGroup, List<HomeMutex>>();

	public void RegisterHomeMutex(HomeMutex homeMutex)
	{
		if (homeMutex.RelationGroup == EHomeRelationGroup.None)
		{

			Debug.LogError("Official HomeRelationManager.RegisterHomeMutex", ((Component)homeMutex).gameObject);
			return;
		}
		if (!mGroupMutexes.TryGetValue(homeMutex.RelationGroup, out var value))
		{
			value = new List<HomeMutex>();
			mGroupMutexes[homeMutex.RelationGroup] = value;
		}
		value.Add(homeMutex);
		CheckChange(value, homeMutex);
	}

	public void UnregisterHomeMutex(HomeMutex homeMutex)
	{
		List<HomeMutex> value;
		if (homeMutex.RelationGroup == EHomeRelationGroup.None)
		{

			Debug.LogError("Official HomeRelationManager.UnregisterHomeMutex", ((Component)homeMutex).gameObject);
		}
		else if (mGroupMutexes.TryGetValue(homeMutex.RelationGroup, out value))
		{
			value.Remove(homeMutex);
			if (value.Count == 0)
			{
				mGroupMutexes.Remove(homeMutex.RelationGroup);
			}
			CheckRestore(value, homeMutex);
		}
	}

	public void ForceCheck()
	{
		foreach (KeyValuePair<EHomeRelationGroup, List<HomeMutex>> mGroupMutex in mGroupMutexes)
		{
			List<HomeMutex> value = mGroupMutex.Value;
			int i = 0;
			for (int count = value.Count; i < count; i++)
			{
				HomeMutex homeMutex = value[i];
				bool flag = false;
				for (int j = 0; j < count; j++)
				{
					if (i != j && homeMutex.IsMutex(value[j]))
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					homeMutex.OnChange();
				}
				else
				{
					homeMutex.OnRestore();
				}
			}
		}
	}

	private void CheckChange(List<HomeMutex> list, HomeMutex newObj)
	{
		int i = 0;
		for (int count = list.Count; i < count; i++)
		{
			HomeMutex homeMutex = list[i];
			if (!((Object)(object)homeMutex == (Object)(object)newObj) && newObj.IsMutex(homeMutex))
			{
				newObj.OnChange();
				homeMutex.OnChange();
			}
		}
	}

	private void CheckRestore(List<HomeMutex> list, HomeMutex toRemove)
	{
		int i = 0;
		for (int count = list.Count; i < count; i++)
		{
			HomeMutex homeMutex = list[i];
			if (!((Object)(object)homeMutex == (Object)(object)toRemove) && toRemove.IsMutex(homeMutex))
			{
				toRemove.OnRestore();
				homeMutex.OnRestore();
			}
		}
	}
}
}
