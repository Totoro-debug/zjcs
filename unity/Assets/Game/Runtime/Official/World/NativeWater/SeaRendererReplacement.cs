// Migrated from selected official CustomWater.dll; see Map13 source receipt.
namespace Zjcs.Official.World.NativeWater
{
using UnityEngine;

public class SeaRendererReplacement : MonoBehaviour
{
	public MeshRenderer[] SeaPatchs;

	public bool IsVisible()
	{
		int i = 0;
		for (int num = SeaPatchs.Length; i < num; i++)
		{
			if (SeaPatchs[i].isVisible)
			{
				return true;
			}
		}
		return false;
	}

	public void ApplyPropertyBlock(MaterialPropertyBlock mpb)
	{
		int i = 0;
		for (int num = SeaPatchs.Length; i < num; i++)
		{
			SeaPatchs[i].SetPropertyBlock(mpb);
		}
	}
}

}
