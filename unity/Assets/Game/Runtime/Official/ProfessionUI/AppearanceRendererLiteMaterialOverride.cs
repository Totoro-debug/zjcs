using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;

[ExecuteAlways]
public class AppearanceRendererLiteMaterialOverride : MonoBehaviour
{
	[Serializable]
	private struct SkeletonMaterialOverride
	{
		public Material OriginalMaterial;

		public Material OverrideMaterial;
	}

	public Component SkeletonRenderer;

	[SerializeField]
	private SkeletonMaterialOverride[] mMaterialOverrides;

	public Material GetOverrideMaterial(Material originalMaterial)
	{
		if (mMaterialOverrides != null)
		{
			SkeletonMaterialOverride[] array = mMaterialOverrides;
			for (int i = 0; i < array.Length; i++)
			{
				SkeletonMaterialOverride skeletonMaterialOverride = array[i];
				if ((Object)(object)skeletonMaterialOverride.OriginalMaterial == (Object)(object)originalMaterial)
				{
					return skeletonMaterialOverride.OverrideMaterial;
				}
			}
		}
		return originalMaterial;
	}
}
}
