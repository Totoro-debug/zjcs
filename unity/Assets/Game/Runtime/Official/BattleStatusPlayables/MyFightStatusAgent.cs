// Migrated official status application source; see status view runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
public class MyFightStatusAgent : MonoBehaviour
{
	public GAction<MyFightStatus, int, int> OnStatusShowCustomCountUpdate = new GAction<MyFightStatus, int, int>();

	// Supplied by the actual shared-stack component, never computed by the view.
	public Dictionary<int, int> StatusToShareStack = new Dictionary<int, int>();

	public HashSet<MyFightStatus> MyStatuses = new HashSet<MyFightStatus>();

	public Dictionary<int, MyFightStatus> MyDisplayFightStatusEachShareStatus = new Dictionary<int, MyFightStatus>();

	public event Action<MyFightStatus> OnStatusApply;

	public event Action<MyFightStatus> OnStatusEnd;

	public void OnEnd()
	{
		MyStatuses.Clear();
	}

	public void ApplyStatus(MyFightStatus status)
	{
		if (!((Object)(object)status == (Object)null))
		{
			MyStatuses.Add(status);
			OnStatusApply?.Invoke(status);
		}
	}

	public void EndStatus(MyFightStatus status)
	{
		if (!((Object)(object)status == (Object)null))
		{
			if (MyStatuses != null)
			{
				MyStatuses.Remove(status);
			}
			OnStatusEnd?.Invoke(status);
		}
	}
}

}
