// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using UnityEngine;

[DisallowMultipleComponent]
public class MapEffect : MapEnvObject
{
	private IMapEnvironmentServices<MapGroundRoot, MapEnvData> Services;
	public void Bind(IMapEnvironmentServices<MapGroundRoot, MapEnvData> services)
	{
		Services = services ?? throw new System.ArgumentNullException(nameof(services));
	}

	private MapGroundRoot mMapGroundRoot;


	private void Start()
	{
		mMapGroundRoot = Services.MapGroundRoot;
		mMapGroundRoot.EnvEffect.RegisterEffect(this);
	}

	private void OnDestroy()
	{
		if ((Object)(object)mMapGroundRoot != (Object)null)
		{
			mMapGroundRoot.EnvEffect.DeregisterEffect(this);
		}
	}
}

}
