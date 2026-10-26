// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using UnityEngine;

public class MapGroundRoot : MonoBehaviour
{
	[HideInInspector]
	public long LastModifyTime;

	public GameObject Sea;

	public PartitionController PartitionController;

	public MapEnvEffect EnvEffect;

	private void ShowEnvEffect()
	{
		if ((Object)(object)EnvEffect != (Object)null)
		{
			((Component)EnvEffect).gameObject.SetActive(true);
		}
		if ((Object)(object)PartitionController != (Object)null)
		{
			PartitionController.TogglePartitionGameObjects(value: true);
		}
	}

	private void HideEnvEffect()
	{
		if ((Object)(object)EnvEffect != (Object)null)
		{
			((Component)EnvEffect).gameObject.SetActive(false);
		}
		if ((Object)(object)PartitionController != (Object)null)
		{
			PartitionController.TogglePartitionGameObjects(value: false);
		}
	}

	public void OnSceneDeactivate()
	{
		HideEnvEffect();
		if ((Object)(object)Sea != (Object)null)
		{
			Sea.SetActive(false);
		}
	}

	public void OnSceneActivate()
	{
		ShowEnvEffect();
		if ((Object)(object)Sea != (Object)null)
		{
			Sea.SetActive(true);
		}
	}
}

}
