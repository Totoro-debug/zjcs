// Selected original state-animation support; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.WorldUI;
using System;
using UnityEngine;

[Serializable]
public class StateAnimationConfig : IWorldStateConfig
{
	[SerializeField]
	private StateAnimationData mStateAnimatorData;

	public void SetState(WorldStateConfigValue state)
	{
		foreach (StateAnimationData.StateAnimationKV item in mStateAnimatorData.ClipsConfig)
		{
			if ((int)item.State == (int)state)
			{
				mStateAnimatorData.Animation.Stop();
				mStateAnimatorData.Animation.Play(item.ClipName);
				break;
			}
		}
	}

	public void Clear()
	{
	}
}

}
