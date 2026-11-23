// Selected original home model light/clock implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Spine;
using Spine.Unity;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.ProfessionUI;
using IHomeEntityManager = Zjcs.Official.HomeEntityRuntime.IHomeEntityManager;
namespace Zjcs.Official.HomeModelEnvironment
{
using Object = UnityEngine.Object;
[Serializable]
public sealed class HomeModelLightConfiguration
{
public List<HomeLightTimeConfig> HomeLightTimeConfigs;
public IEnumerable<HomeLight> GetHomeLightStatus(HomeDayTime dayTime, string behaviourType)
	{
		if (HomeLightTimeConfigs.Count == 0)
		{
			return null;
		}
		int i = 1;
		for (int count = HomeLightTimeConfigs.Count; i < count; i++)
		{
			if (HomeLightTimeConfigs[i].BeginTime > dayTime)
			{
				return HomeLightTimeConfigs[i - 1].GetHomeLights(behaviourType);
			}
		}
		return HomeLightTimeConfigs[HomeLightTimeConfigs.Count - 1].GetHomeLights(behaviourType);
	}
}
}
