// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light2D))]
public class MapLight : MapEnvObject
{
	private IMapEnvironmentServices<MapGroundRoot, MapEnvData> Services;
	public void Bind(IMapEnvironmentServices<MapGroundRoot, MapEnvData> services)
	{
		Services = services ?? throw new System.ArgumentNullException(nameof(services));
	}

	public Light2D Light;

	private MapGroundRoot mMapGroundRoot;


	private void Start()
	{
		mMapGroundRoot = Services.MapGroundRoot;
		mMapGroundRoot.EnvEffect.RegisterLight(this);
	}

	private void OnDestroy()
	{
		if ((Object)(object)mMapGroundRoot != (Object)null && (Object)(object)mMapGroundRoot.EnvEffect != (Object)null)
		{
			mMapGroundRoot.EnvEffect.DeregisterLight(this);
		}
		mMapGroundRoot = null;
	}
}

}
