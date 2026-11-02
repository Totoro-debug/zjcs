// Selected original state-animation support; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StateAnimationData
{
	[Serializable]
	public class StateAnimationKV
	{
		public WorldStateConfigValue State;

		public string ClipName;
	}

	public Animation Animation;

	public List<StateAnimationKV> ClipsConfig;
}

}
