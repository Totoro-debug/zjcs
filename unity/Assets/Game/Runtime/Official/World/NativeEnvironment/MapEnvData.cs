// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using UnityEngine;

public class MapEnvData : ScriptableObject
{
	[Header("地表私区消失表现延迟时间")]
	public float DisappearDelayTime = 0.5f;

	[Header("地表私区消失表现时间")]
	public float DisappearTime = 1f;

	[Header("场景灯光熄灯时长")]
	public float FadeTime = 2f;
}

}
