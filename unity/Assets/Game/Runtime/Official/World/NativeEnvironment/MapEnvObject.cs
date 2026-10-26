// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using UnityEngine;

public class MapEnvObject : MonoBehaviour
{
	public enum RegionMode
	{
		Auto,
		Fix,
		RefPos
	}

	public RegionMode Mode;

	public int Region;

	public DayNightState State;

	public Vector3 RefPos;

	public virtual void Appear()
	{
	}

	public virtual void Disappear()
	{
	}
}

}
