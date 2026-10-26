// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class MapRegionLight : MonoBehaviour
{
	private IMapEnvironmentServices<MapGroundRoot, MapEnvData> Services;
	public void Bind(IMapEnvironmentServices<MapGroundRoot, MapEnvData> services)
	{
		Services = services ?? throw new System.ArgumentNullException(nameof(services));
	}

	public int Region;

	public Animation ActiveRegionAnimation;

	public Light2D RegionShadow;

	public GameObject DayNode;

	private MapGroundRoot mMapGroundRoot;


	private void Start()
	{
		mMapGroundRoot = Services.MapGroundRoot;
		mMapGroundRoot.EnvEffect.RegisterRegionLight(this);
	}

	private void OnDestroy()
	{
		mMapGroundRoot.EnvEffect.DeregisterRegionLight(this);
	}

	public void ChangeState(DayNightState state)
	{
		((Component)RegionShadow).gameObject.SetActive(state == DayNightState.Night);
		DayNode.SetActive(state == DayNightState.Day);
	}

	public void Active(Vector3 pos)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)RegionShadow != (Object)null)
		{
			RegionShadow.RecessionCenter = pos;
		}
		if ((Object)(object)ActiveRegionAnimation != (Object)null)
		{
			ActiveRegionAnimation.Play();
		}
	}
}

}
