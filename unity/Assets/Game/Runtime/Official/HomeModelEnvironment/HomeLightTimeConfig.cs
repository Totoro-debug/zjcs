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
public class HomeLightTimeConfig
{
	public HomeDayTime BeginTime;

	public HomeLight[] DefaultLights;

	public HomeLightBehaviourOverride[] Overrides;

	private HomeLight[] mLightsCache;

	public HomeLight[] GetHomeLights(string behaviourType)
	{
		if (DefaultLights.Length == 0)
		{
			return null;
		}
		if (mLightsCache == null)
		{
			mLightsCache = new HomeLight[DefaultLights.Length];
		}
		if (mLightsCache.Length != DefaultLights.Length)
		{
			Array.Resize(ref mLightsCache, DefaultLights.Length);
		}
		Array.Copy(DefaultLights, mLightsCache, DefaultLights.Length);
		HomeLightBehaviourOverride[] overrides = Overrides;
		foreach (HomeLightBehaviourOverride homeLightBehaviourOverride in overrides)
		{
			if (homeLightBehaviourOverride.BehaviourType == behaviourType || homeLightBehaviourOverride.BehaviourType == "None")
			{
				HomeLightOverride[] lightOverrides = homeLightBehaviourOverride.LightOverrides;
				foreach (HomeLightOverride homeLightOverride in lightOverrides)
				{
					mLightsCache[homeLightOverride.Index].IsOn = homeLightOverride.IsOn;
				}
				break;
			}
		}
		return mLightsCache;
	}
}
}
