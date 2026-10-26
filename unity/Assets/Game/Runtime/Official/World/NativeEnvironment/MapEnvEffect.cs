// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class MapEnvEffect : MonoBehaviour
{
	private IMapEnvironmentServices<MapGroundRoot, MapEnvData> Services;
	public void Bind(IMapEnvironmentServices<MapGroundRoot, MapEnvData> services)
	{
		Services = services ?? throw new System.ArgumentNullException(nameof(services));
	}

	public class RegionEffectHub
	{
		public List<GameObject> DayEffects = new List<GameObject>();

		public List<GameObject> NightEffects = new List<GameObject>();
	}

	public class RegionObjectHub
	{
		public List<MapEnvObject> DayObjects = new List<MapEnvObject>();

		public List<MapEnvObject> NightObjects = new List<MapEnvObject>();
	}

	public RegionEffectHub[] RegionVfxs;

	public RegionLightHub[] RegionLights;

	public RegionObjectHub[] RegionObjects;

	public GameObject Day;

	public GameObject Night;

	private List<MapRegionStatus> mRegionStatuses;




	private void Awake()
	{
		Day.SetActive(true);
		Night.SetActive(true);
		Init();
	}

	private void OnDisable()
	{
		int i = 0;
		for (int num = RegionLights.Length; i < num; i++)
		{
			RegionLights[i].Clear();
		}
	}

	public void UnlockRegion(Vector3 worldPos)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		Pos2d pos = Services.PosToGrid(worldPos);
		int num = Services.QueryRegion(pos);
		ChangeEffectState(num, DayNightState.Day);
		ChangeObjectState(num, DayNightState.Day);
		RegionLightHub regionLightHub = RegionLights[num];
		regionLightHub.FadeOut(Services.EnvironmentData.FadeTime);
		if ((Object)(object)regionLightHub.RegionLight != (Object)null)
		{
			regionLightHub.RegionLight.Active(worldPos);
		}
		Services.UnlockAllRegionPositions(Services.MapClassId, num);
	}

	public void Init()
	{
		mRegionStatuses = Services.GetCurrentMapStatus();
		RegionVfxs = new RegionEffectHub[mRegionStatuses.Count];
		RegionLights = new RegionLightHub[mRegionStatuses.Count];
		RegionObjects = new RegionObjectHub[mRegionStatuses.Count];
		int i = 0;
		for (int count = mRegionStatuses.Count; i < count; i++)
		{
			RegionVfxs[i] = new RegionEffectHub();
			RegionLights[i] = new RegionLightHub();
			RegionObjects[i] = new RegionObjectHub();
			DayNightState state = ((mRegionStatuses[i] != MapRegionStatus.Public) ? DayNightState.Night : DayNightState.Day);
			ChangeEffectState(i, state);
			ChangeLightState(i, state);
		}
	}

	public void ChangeEffectState(int region, DayNightState state)
	{
		RegionEffectHub regionEffectHub = RegionVfxs[region];
		int i = 0;
		for (int count = regionEffectHub.DayEffects.Count; i < count; i++)
		{
			regionEffectHub.DayEffects[i].SetActive(state == DayNightState.Day);
		}
		int j = 0;
		for (int count2 = regionEffectHub.NightEffects.Count; j < count2; j++)
		{
			regionEffectHub.NightEffects[j].SetActive(state == DayNightState.Night);
		}
	}

	public void ChangeLightState(int region, DayNightState state)
	{
		RegionLights[region].ChangeLightState(state);
	}

	public void ChangeObjectState(int region, DayNightState state)
	{
		RegionObjectHub regionObjectHub = RegionObjects[region];
		int i = 0;
		for (int count = regionObjectHub.DayObjects.Count; i < count; i++)
		{
			MapEnvObject mapEnvObject = regionObjectHub.DayObjects[i];
			if (state == DayNightState.Day)
			{
				mapEnvObject.Appear();
			}
			else
			{
				mapEnvObject.Disappear();
			}
		}
		int j = 0;
		for (int count2 = regionObjectHub.NightObjects.Count; j < count2; j++)
		{
			MapEnvObject mapEnvObject2 = regionObjectHub.NightObjects[j];
			if (state == DayNightState.Day)
			{
				mapEnvObject2.Disappear();
			}
			else
			{
				mapEnvObject2.Appear();
			}
		}
	}

	public void RegisterEffect(MapEffect effect)
	{
		int region = effect.Region;
		DayNightState state = effect.State;
		RegionEffectHub regionEffectHub = RegionVfxs[region];
		List<GameObject> list = ((state != DayNightState.Day) ? regionEffectHub.NightEffects : regionEffectHub.DayEffects);
		list.Add(((Component)effect).gameObject);
		if (mRegionStatuses != null)
		{
			DayNightState dayNightState = ((mRegionStatuses[region] != MapRegionStatus.Public) ? DayNightState.Night : DayNightState.Day);
			((Component)effect).gameObject.SetActive(dayNightState == state);
			return;
		}
		UnityEngine.Debug.LogError("Map environment registration error");
	}

	public void DeregisterEffect(MapEffect effect)
	{
		int region = effect.Region;
		DayNightState state = effect.State;
		RegionEffectHub regionEffectHub = RegionVfxs[region];
		List<GameObject> list = null;
		list = ((state != DayNightState.Day) ? regionEffectHub.NightEffects : regionEffectHub.DayEffects);
		list.Remove(((Component)effect).gameObject);
	}

	public void RegisterLight(MapLight light)
	{
		int region = light.Region;
		DayNightState state = light.State;
		RegionLightHub regionLightHub = RegionLights[region];
		List<Light2D> list = null;
		list = ((state != DayNightState.Day) ? regionLightHub.NightLights : regionLightHub.DayLights);
		list.Add(light.Light);
		if (mRegionStatuses != null)
		{
			DayNightState dayNightState = ((mRegionStatuses[region] != MapRegionStatus.Public) ? DayNightState.Night : DayNightState.Day);
			((Component)light).gameObject.SetActive(dayNightState == state);
			return;
		}
		UnityEngine.Debug.LogError("Map environment registration error");
	}

	public void DeregisterLight(MapLight light)
	{
		int region = light.Region;
		DayNightState state = light.State;
		RegionLightHub regionLightHub = RegionLights[region];
		List<Light2D> list = null;
		list = ((state != DayNightState.Day) ? regionLightHub.NightLights : regionLightHub.DayLights);
		list.Remove(light.Light);
	}

	public void RegisterRegionLight(MapRegionLight regionLight)
	{
		int region = regionLight.Region;
		RegionLights[region].RegionLight = regionLight;
		if (mRegionStatuses != null)
		{
			DayNightState state = ((mRegionStatuses[region] != MapRegionStatus.Public) ? DayNightState.Night : DayNightState.Day);
			regionLight.ChangeState(state);
			return;
		}
		UnityEngine.Debug.LogError("Map environment registration error");
	}

	public void DeregisterRegionLight(MapRegionLight regionLight)
	{
		int region = regionLight.Region;
		RegionLights[region].RegionLight = null;
	}

	public void RegisterECLight(IMapRegionEntityLight<Light2D> lightView)
	{
		int region = lightView.Region;
		RegionLights[region].NightLights.Add(lightView.Light);
	}

	public void DeregisterECLight(IMapRegionEntityLight<Light2D> lightView)
	{
		int region = lightView.Region;
		RegionLights[region].NightLights.Remove(lightView.Light);
	}

	public void RegisterObject(MapEnvObject envObject)
	{
		int region = envObject.Region;
		if (region < 0 || region >= RegionObjects.Length)
		{
			UnityEngine.Debug.LogError("Map environment registration error");
			return;
		}
		RegionObjectHub regionObjectHub = RegionObjects[region];
		DayNightState state = envObject.State;
		((state == DayNightState.Day) ? regionObjectHub.DayObjects : regionObjectHub.NightObjects).Add(envObject);
		if (mRegionStatuses != null)
		{
			DayNightState dayNightState = ((mRegionStatuses[region] != MapRegionStatus.Public) ? DayNightState.Night : DayNightState.Day);
			((Component)envObject).gameObject.SetActive(dayNightState == state);
			return;
		}
		UnityEngine.Debug.LogError("Map environment registration error");
	}

	public void DeregisterObject(MapEnvObject envObject)
	{
		int region = envObject.Region;
		if (region < 0 || region >= RegionObjects.Length)
		{
			UnityEngine.Debug.LogError("Map environment registration error");
		}
		else
		{
			DayNightState state = envObject.State;
			RegionObjectHub regionObjectHub = RegionObjects[region];
			((state == DayNightState.Day) ? regionObjectHub.DayObjects : regionObjectHub.NightObjects).Remove(envObject);
		}
	}
}

}
