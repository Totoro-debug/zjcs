// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Spine;
using Spine.Unity;
using Animation = Spine.Animation;
using AnimationState = Spine.AnimationState;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public class HomeWeapon : MonoBehaviour
{
	public string AnimName;

	public AppearancePartSideType Side;

	public float LightAtten;

	private void Awake()
	{
		AppearancePartSideSwitcher componentInChildren = ((Component)this).GetComponentInChildren<AppearancePartSideSwitcher>();
		if ((Object)(object)componentInChildren != (Object)null)
		{
			if (!string.IsNullOrEmpty(AnimName))
			{
				AppearancePartSide[] sides = componentInChildren.Sides;
				for (int i = 0; i < sides.Length; i++)
				{
					sides[i].SkeletonAnimName = AnimName;
				}
			}
			componentInChildren.SetSide(Side);
		}
		SkeletonAnimation componentInChildren2 = ((Component)this).GetComponentInChildren<SkeletonAnimation>();
		AppearanceRendererLite appearanceRendererLite = default;
		if (((Component)componentInChildren2).TryGetComponent<AppearanceRendererLite>(out appearanceRendererLite))
		{
			Material[] independentMaterials = appearanceRendererLite.GetIndependentMaterials();
			for (int i = 0; i < independentMaterials.Length; i++)
			{
				independentMaterials[i].SetFloat(AppearanceMaterialUtils.sLightAtten, LightAtten);
			}
		}
		else
		{
			MeshRenderer component = ((Component)componentInChildren2).GetComponent<MeshRenderer>();
			Material val = new Material(((Renderer)component).sharedMaterial);
			val.SetFloat(AppearanceMaterialUtils.sLightAtten, LightAtten);
			((SkeletonRenderer)componentInChildren2).CustomMaterialOverride.Add(((Renderer)component).sharedMaterial, val);
		}
	}
}

}
